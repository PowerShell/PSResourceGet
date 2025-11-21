// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

using Dbg = System.Diagnostics.Debug;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    /// <summary>
    /// The Set-PSResourceRepository cmdlet is used to set information for a repository.
    /// </summary>
    [Cmdlet(VerbsCommon.Set,
        "PSResourceRepository",
        DefaultParameterSetName = NameParameterSet,
        SupportsShouldProcess = true)]
    public sealed class SetPSResourceRepository : PSCmdlet, IDynamicParameters
    {
        #region Members

        private const string NameParameterSet = "NameParameterSet";
        private const string RepositoriesParameterSet = "RepositoriesParameterSet";
        private const int DefaultPriority = -1;
        private Uri _uri;
        private CredentialProviderDynamicParameters _credentialProvider;

        #endregion

        #region Parameters

        /// <summary>
        /// Specifies the name of the repository to be set.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = NameParameterSet, HelpMessage = "Name of the repository to set properties for.")]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        /// <summary>
        /// Specifies the location of the repository to be set.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string Uri { get; set; }

        /// <summary>
        /// Specifies a hashtable of repositories and is used to register multiple repositories at once.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = RepositoriesParameterSet, HelpMessage = "Hashtable including information on single or multiple repositories to set specified information for.")]
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
        /// Specifies the Api version of the repository to be set.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateSet("V2", "V3", "Local", "NugetServer", "ContainerRegistry")]
        public PSRepositoryInfo.APIVersion ApiVersion { get; set; }

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

        #region DynamicParameters

        public object GetDynamicParameters()
        {
            PSRepositoryInfo repository = RepositorySettings.Read(new[] { Name }, out string[] _).FirstOrDefault();
            // Dynamic parameter '-CredentialProvider' should not appear for PSGallery, or any container registry repository.
            // It should also not appear when using the 'Repositories' parameter set.
            if (repository is not null &&
                (repository.Name.Equals("PSGallery", StringComparison.OrdinalIgnoreCase) ||
                ParameterSetName.Equals(RepositoriesParameterSet) ||
                repository.IsContainerRegistry()))
            {
                return null;
            }

            _credentialProvider = new CredentialProviderDynamicParameters();
            return _credentialProvider;
        }

        #endregion

        #region Private methods

        protected override void BeginProcessing()
        {
            RepositorySettings.CheckRepositoryStore();
        }

        protected override void ProcessRecord()
        {
            // determine if either 1 of 5 values are attempting to be set: Uri, Priority, Trusted, APIVersion, CredentialInfo.
            // if none are (i.e only Name parameter was provided, write error)
            if (ParameterSetName.Equals(NameParameterSet) &&
                !MyInvocation.BoundParameters.ContainsKey(nameof(Uri)) &&
                !MyInvocation.BoundParameters.ContainsKey(nameof(Priority)) &&
                !MyInvocation.BoundParameters.ContainsKey(nameof(Trusted)) &&
                !MyInvocation.BoundParameters.ContainsKey(nameof(ApiVersion)) &&
                !MyInvocation.BoundParameters.ContainsKey(nameof(CredentialInfo)) &&
                !MyInvocation.BoundParameters.ContainsKey(nameof(CredentialProvider)))
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException("Must set Uri, Priority, Trusted, ApiVersion, CredentialInfo, or CredentialProvider parameter"),
                    "SetRepositoryParameterBindingFailure",
                    ErrorCategory.InvalidArgument,
                    this));
            }

            if (MyInvocation.BoundParameters.ContainsKey(nameof(Uri)))
            {
                if (!Utils.TryCreateValidUri(Uri, this, out _uri, out ErrorRecord errorRecord))
                {
                    ThrowTerminatingError(errorRecord);
                }
            }

            PSRepositoryInfo.APIVersion? repoApiVersion = null;
            if (MyInvocation.BoundParameters.ContainsKey(nameof(ApiVersion)))
            {
                repoApiVersion = ApiVersion;
            }

            PSRepositoryInfo.CredentialProviderType? credentialProvider = _credentialProvider?.CredentialProvider;

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
                            repoApiVersion,
                            CredentialInfo,
                            credentialProvider,
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
            WriteDebug("In SetPSResourceRepository::RepositoriesParameterSetHelper()");
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
            WriteDebug("In SetPSResourceRepository::RepoValidationHelper()");
            WriteDebug($"Parsing through repository '{repo["Name"]}'");

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

            PSRepositoryInfo.CredentialProviderType? credentialProvider = _credentialProvider?.CredentialProvider;

            try
            {
                var updatedRepo = RepositorySettings.UpdateRepositoryStore(repo["Name"].ToString(),
                    repoUri,
                    repo.ContainsKey("Priority") ? Convert.ToInt32(repo["Priority"].ToString()) : DefaultPriority,
                    repoTrusted,
                    isSet,
                    DefaultPriority,
                    ApiVersion,
                    repoCredentialInfo,
                    credentialProvider,
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
