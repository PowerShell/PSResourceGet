// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;

using Dbg = System.Diagnostics.Debug;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Set-PSResourceRepository cmdlet is used to set information for a repository.
    /// </summary>
    [Cmdlet(VerbsCommon.Set,
        "PSResourceRepository",
        DefaultParameterSetName = NameParameterSet,
        SupportsShouldProcess = true)]
    public sealed class SetPSResourceRepository : PSCmdlet
    {
        #region Members

        private const string NameParameterSet = "NameParameterSet";
        private const string RepositoriesParameterSet = "RepositoriesParameterSet";
        private const int DefaultPriority = -1;
        private Uri _uri;

        #endregion

        #region Parameters

        /// <summary>
        /// Specifies the name of the repository to be set.
        /// </sumamry>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = NameParameterSet)]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        /// <summary>
        /// Specifies the location of the repository to be set.
        /// </sumamry>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string Uri { get; set; }

        /// <summary>
        /// Specifies a hashtable of repositories and is used to register multiple repositories at once.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = RepositoriesParameterSet)]
        [ValidateNotNullOrEmpty]
        public Hashtable[] Repository { get; set; }

        /// <summary>
        /// Specifies whether the repository should be trusted.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public SwitchParameter Trusted
        {
            get
            { return _trusted; }

            set
            {
                _trusted = value;
                isSet = true;
            }
        }
        private SwitchParameter _trusted;
        private bool isSet;

        /// <summary>
        /// Specifies the priority ranking of the repository, such that repositories with higher ranking priority are searched
        /// before a lower ranking priority one, when searching for a repository item across multiple registered repositories.
        /// Valid priority values range from 0 to 100, such that a lower numeric value (i.e 10) corresponds
        /// to a higher priority ranking than a higher numeric value (i.e 40).
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        [ValidateRange(0, 100)]
        public int Priority { get; set; } = DefaultPriority;

        /// <summary>
        /// Specifies vault and secret names as PSCredentialInfo for the repository.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public PSCredentialInfo CredentialInfo { get; set; }

        /// <summary>
        /// When specified, displays the successfully registered repository and its information.
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        #endregion

        #region Private methods

        protected override void BeginProcessing()
        {
            RepositorySettings.CheckRepositoryStore();
        }

        protected override void ProcessRecord()
        {
            if (MyInvocation.BoundParameters.ContainsKey(nameof(Uri)))
            {
                bool isUriValid = Utils.TryCreateValidUri(Uri, this, out _uri, out ErrorRecord errorRecord);
                if (!isUriValid)
                {
                    ThrowTerminatingError(errorRecord);
                }
            }

            List<PSRepositoryInfo> items = new List<PSRepositoryInfo>();

            switch(ParameterSetName)
            {
                case NameParameterSet:
                    try
                    {
                        items.Add(RepositorySettings.UpdateRepositoryStore(Name, 
                            _uri, 
                            Priority,
                            Trusted, 
                            isSet,
                            DefaultPriority,
                            CredentialInfo,
                            this,
                            out string errorMsg));

                        if (!string.IsNullOrEmpty(errorMsg))  
                        {
                            ThrowTerminatingError(new ErrorRecord(
                                new PSInvalidOperationException(errorMsg),
                                "ErrorInNameParameterSet",
                                ErrorCategory.InvalidArgument,
                                this));
                        }
                    }
                    catch (Exception e)
                    {
                        ThrowTerminatingError(new ErrorRecord(
                            new PSInvalidOperationException(e.Message),
                            "ErrorInNameParameterSet",
                            ErrorCategory.InvalidArgument,
                            this));
                    }
                    break;

                case RepositoriesParameterSet:
                    try
                    {
                        items = RepositoriesParameterSetHelper();
                    }
                    catch (Exception e)
                    {
                        ThrowTerminatingError(new ErrorRecord(
                            new PSInvalidOperationException(e.Message),
                            "ErrorInRepositoriesParameterSet",
                            ErrorCategory.InvalidArgument,
                            this));
                    }
                    break;

                default:
                    Dbg.Assert(false, "Invalid parameter set");
                    break;
            }

            if (PassThru)
            {
                foreach(PSRepositoryInfo item in items)
                {
                    WriteObject(item);
                }
            }
        }

        private List<PSRepositoryInfo> RepositoriesParameterSetHelper()
        {
            List<PSRepositoryInfo> reposUpdatedFromHashtable = new List<PSRepositoryInfo>();
            foreach (Hashtable repo in Repository)
            {
                if (!repo.ContainsKey("Name") || repo["Name"] == null || String.IsNullOrEmpty(repo["Name"].ToString()))
                {
                    WriteError(new ErrorRecord(
                            new PSInvalidOperationException("Repository hashtable must contain Name key value pair"),
                            "NullNameForRepositoriesParameterSetRepo",
                            ErrorCategory.InvalidArgument,
                            this));
                    continue;
                }

                PSRepositoryInfo parsedRepoAdded = RepoValidationHelper(repo);
                if (parsedRepoAdded != null)
                {
                    reposUpdatedFromHashtable.Add(parsedRepoAdded);
                }
            }
            return reposUpdatedFromHashtable;
        }

        private PSRepositoryInfo RepoValidationHelper(Hashtable repo)
        {
            WriteVerbose(String.Format("Parsing through repository: {0}", repo["Name"]));

            Uri repoUri = null;
            if (repo.ContainsKey("Uri"))
            {
                if (String.IsNullOrEmpty(repo["Uri"].ToString()))
                {
                    WriteError(new ErrorRecord(
                            new PSInvalidOperationException("Repository Uri cannot be null if provided"),
                            "NullUriForRepositoriesParameterSetUpdate",
                            ErrorCategory.InvalidArgument,
                            this));
                    return null;
                }

                if (!Utils.TryCreateValidUri(uriString: repo["Uri"].ToString(),
                    cmdletPassedIn: this,
                    uriResult: out repoUri,
                    errorRecord: out ErrorRecord errorRecord))
                {
                    WriteError(errorRecord);
                    return null;
                }
            }

            bool repoTrusted = false;
            isSet = false;
            if (repo.ContainsKey("Trusted"))
            {
                repoTrusted = (bool) repo["Trusted"];
                isSet = true;
            }

            PSCredentialInfo repoCredentialInfo = null;
            if (repo.ContainsKey("CredentialInfo") &&
                !Utils.TryCreateValidPSCredentialInfo(credentialInfoCandidate: (PSObject) repo["CredentialInfo"],
                    cmdletPassedIn: this,
                    repoCredentialInfo: out repoCredentialInfo,
                    errorRecord: out ErrorRecord errorRecord1))
            {
                WriteError(errorRecord1);
                return null;
            }

            try
            {
                var updatedRepo = RepositorySettings.UpdateRepositoryStore(repo["Name"].ToString(),
                    repoUri,
                    repo.ContainsKey("Priority") ? Convert.ToInt32(repo["Priority"].ToString()) : DefaultPriority,
                    repoTrusted,
                    isSet,
                    DefaultPriority,
                    repoCredentialInfo,
                    this,
                    out string errorMsg);

                if (!string.IsNullOrEmpty(errorMsg))
                {
                    WriteError(new ErrorRecord(
                        new PSInvalidOperationException(errorMsg),
                        "ErrorSettingRepository",
                        ErrorCategory.InvalidData,
                        this));
                }

                return updatedRepo;
            }
            catch (Exception e)
            {
                WriteError(new ErrorRecord(
                        new PSInvalidOperationException(e.Message),
                        "ErrorSettingIndividualRepoFromRepositories",
                        ErrorCategory.InvalidArgument,
                        this));
                return null;
            }
        }

        #endregion
    }
}
