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
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ParameterSetName = NameParameterSet)]
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
        /// Valid priority values range from 0 to 50, such that a lower numeric value (i.e 10) corresponds
        /// to a higher priority ranking than a higher numeric value (i.e 40).
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        [ValidateRange(0, 50)]
        public int Priority { get; set; } = DefaultPriority;

        /// <summary>
        /// Specifies vault and secret names as PSCredentialInfo for the repository.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public PSCredentialInfo CredentialInfo { get; set; }

        /// <summary>
        /// When specified, displays the successfully registered repository and its information
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
                        items.Add(UpdateRepositoryStoreHelper(Name, _uri, Priority, Trusted, CredentialInfo));
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

        private PSRepositoryInfo UpdateRepositoryStoreHelper(string repoName, Uri repoUri, int repoPriority, bool repoTrusted, PSCredentialInfo repoCredentialInfo)
        {
            if (repoUri != null && !(repoUri.Scheme == System.Uri.UriSchemeHttp || repoUri.Scheme == System.Uri.UriSchemeHttps || repoUri.Scheme == System.Uri.UriSchemeFtp || repoUri.Scheme == System.Uri.UriSchemeFile))
            {
                throw new ArgumentException("Invalid Uri, must be one of the following Uri schemes: HTTPS, HTTP, FTP, File Based");
            }

            // check repoName can't contain * or just be whitespace
            // remove trailing and leading whitespaces, and if Name is just whitespace Name should become null now and be caught by following condition
            repoName = repoName.Trim();
            if (String.IsNullOrEmpty(repoName) || repoName.Contains("*"))
            {
                throw new ArgumentException("Name cannot be null/empty, contain asterisk or be just whitespace");
            }

            // check PSGallery Uri is not trying to be set
            if (repoName.Equals("PSGallery", StringComparison.OrdinalIgnoreCase) && repoUri != null)
            {
                throw new ArgumentException("The PSGallery repository has a pre-defined Uri. Setting the -Uri parameter for this repository is not allowed, instead try running 'Register-PSResourceRepository -PSGallery'.");
            }

            // check PSGallery CredentialInfo is not trying to be set
            if (repoName.Equals("PSGallery", StringComparison.OrdinalIgnoreCase) && repoCredentialInfo != null)
            {
                throw new ArgumentException("The PSGallery repository does not require authentication. Setting the -CredentialInfo parameter for this repository is not allowed, instead try running 'Register-PSResourceRepository -PSGallery'.");
            }

            // determine trusted value to pass in (true/false if set, null otherwise, hence the nullable bool variable)
            bool? _trustedNullable = isSet ? new bool?(repoTrusted) : new bool?();

            if (repoCredentialInfo != null)
            {
                bool isSecretManagementModuleAvailable = Utils.IsSecretManagementModuleAvailable(repoName, this);

                if (repoCredentialInfo.Credential != null)
                {
                    if (!isSecretManagementModuleAvailable)
                    {
                        ThrowTerminatingError(new ErrorRecord(
                            new PSInvalidOperationException($"Microsoft.PowerShell.SecretManagement module is not found, but is required for saving PSResourceRepository {repoName}'s Credential in a vault."),
                            "RepositoryCredentialSecretManagementUnavailableModule",
                            ErrorCategory.ResourceUnavailable,
                            this));
                    }
                    else
                    {
                        Utils.SaveRepositoryCredentialToSecretManagementVault(repoName, repoCredentialInfo, this);
                    }
                }

                if (!isSecretManagementModuleAvailable)
                {
                    WriteWarning($"Microsoft.PowerShell.SecretManagement module cannot be found. Make sure it is installed before performing PSResource operations in order to successfully authenticate to PSResourceRepository \"{repoName}\" with its CredentialInfo.");
                }
            }

            // determine if either 1 of 4 values are attempting to be set: Uri, Priority, Trusted, CredentialInfo.
            // if none are (i.e only Name parameter was provided, write error)
            if (repoUri == null && repoPriority == DefaultPriority && _trustedNullable == null && repoCredentialInfo == null)
            {
                throw new ArgumentException("Either Uri, Priority, Trusted or CredentialInfo parameters must be requested to be set");
            }

            WriteVerbose("All required values to set repository provided, calling internal Update() API now");
            if (!ShouldProcess(repoName, "Set repository's value(s) in repository store"))
            {
                return null;
            }
            return RepositorySettings.Update(repoName, repoUri, repoPriority, _trustedNullable, repoCredentialInfo);
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
                return UpdateRepositoryStoreHelper(repo["Name"].ToString(),
                    repoUri,
                    repo.ContainsKey("Priority") ? Convert.ToInt32(repo["Priority"].ToString()) : DefaultPriority,
                    repoTrusted,
                    repoCredentialInfo);
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
