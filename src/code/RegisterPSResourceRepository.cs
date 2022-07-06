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
    /// The Register-PSResourceRepository cmdlet replaces the Register-PSRepository from V2.
    /// It registers a repository for PowerShell modules.
    /// The repository is registered to the current user's scope and does not have a system-wide scope.
    /// </summary>

    [Cmdlet(VerbsLifecycle.Register,
        "PSResourceRepository",
        DefaultParameterSetName = NameParameterSet,
        SupportsShouldProcess = true)]
    public sealed
    class RegisterPSResourceRepository : PSCmdlet
    {
        #region Members

        private readonly string PSGalleryRepoName = "PSGallery";
        private readonly string PSGalleryRepoUri = "https://www.powershellgallery.com/api/v2";
        private const int defaultPriority = 50;
        private const bool defaultTrusted = false;
        private const string NameParameterSet = "NameParameterSet";
        private const string PSGalleryParameterSet = "PSGalleryParameterSet";
        private const string RepositoriesParameterSet = "RepositoriesParameterSet";
        private Uri _uri;

        #endregion

        #region Parameters

        /// <summary>
        /// Specifies name for the repository to be registered.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        /// <summary>
        /// Specifies the location of the repository to be registered.
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = NameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string Uri { get; set; }

        /// <summary>
        /// When specified, registers PSGallery repository.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = PSGalleryParameterSet)]
        public SwitchParameter PSGallery { get; set; }

        /// <summary>
        /// Specifies a hashtable of repositories and is used to register multiple repositories at once.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = RepositoriesParameterSet)]
        [ValidateNotNullOrEmpty]
        public Hashtable[] Repository {get; set;}

        /// <summary>
        /// Specifies whether the repository should be trusted.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = PSGalleryParameterSet)]
        public SwitchParameter Trusted { get; set; }

        /// <summary>
        /// Specifies the priority ranking of the repository, such that repositories with higher ranking priority are searched
        /// before a lower ranking priority one, when searching for a repository item across multiple registered repositories.
        /// Valid priority values range from 0 to 50, such that a lower numeric value (i.e 10) corresponds
        /// to a higher priority ranking than a higher numeric value (i.e 40). Has default value of 50.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = PSGalleryParameterSet)]
        [ValidateRange(0, 50)]
        public int Priority { get; set; } = defaultPriority;

        /// <summary>
        /// Specifies vault and secret names as PSCredentialInfo for the repository.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public PSCredentialInfo CredentialInfo { get; set; }

        /// <summary>
        /// Specifies a proxy server for the request, rather than a direct connection to the internet resource.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public Uri Proxy { get; set; }

        /// <summary>
        /// Specifies a user account that has permission to use the proxy server that is specified by the Proxy parameter.
        /// </summary>
        [Parameter]
        public PSCredential ProxyCredential { get; set; }

        /// <summary>
        /// When specified, displays the succcessfully registered repository and its information
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        #endregion

        #region Methods

        protected override void BeginProcessing()
        {
            if (Proxy != null || ProxyCredential != null)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new PSNotImplementedException("Proxy and ProxyCredential are not yet implemented. Please rerun cmdlet with other parameters."),
                    "ParametersNotImplementedYet",
                    ErrorCategory.NotImplemented,
                    this));
            }

            RepositorySettings.CheckRepositoryStore();
        }
        protected override void ProcessRecord()
        {
            List<PSRepositoryInfo> items = new List<PSRepositoryInfo>();

            switch (ParameterSetName)
            {
                case NameParameterSet:
                    if (!Utils.TryCreateValidUri(uriString: Uri,
                        cmdletPassedIn: this,
                        uriResult: out _uri,
                        errorRecord: out ErrorRecord errorRecord))
                    {
                        ThrowTerminatingError(errorRecord);
                    }

                    try
                    {
                        items.Add(NameParameterSetHelper(Name, _uri, Priority, Trusted, CredentialInfo));
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

                case PSGalleryParameterSet:
                    try
                    {
                        items.Add(PSGalleryParameterSetHelper(Priority, Trusted));
                    }
                    catch (Exception e)
                    {
                        ThrowTerminatingError(new ErrorRecord(
                            new PSInvalidOperationException(e.Message),
                            "ErrorInPSGalleryParameterSet",
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
                foreach (PSRepositoryInfo repo in items)
                {
                    WriteObject(repo);
                }
            }
        }

        private PSRepositoryInfo AddToRepositoryStoreHelper(string repoName, Uri repoUri, int repoPriority, bool repoTrusted, PSCredentialInfo repoCredentialInfo)
        {
            // remove trailing and leading whitespaces, and if Name is just whitespace Name should become null now and be caught by following condition
            repoName = repoName.Trim(' ');
            if (String.IsNullOrEmpty(repoName) || repoName.Contains("*"))
            {
                throw new ArgumentException("Name cannot be null/empty, contain asterisk or be just whitespace");
            }

            if (repoUri == null || !(repoUri.Scheme == System.Uri.UriSchemeHttp || repoUri.Scheme == System.Uri.UriSchemeHttps || repoUri.Scheme == System.Uri.UriSchemeFtp || repoUri.Scheme == System.Uri.UriSchemeFile))
            {
                throw new ArgumentException("Invalid Uri, must be one of the following Uri schemes: HTTPS, HTTP, FTP, File Based");
            }

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

            WriteVerbose("All required values to add to repository provided, calling internal Add() API now");
            if (!ShouldProcess(repoName, "Register repository to repository store"))
            {
                return null;
            }

            return RepositorySettings.Add(repoName, repoUri, repoPriority, repoTrusted, repoCredentialInfo);
        }

        private PSRepositoryInfo NameParameterSetHelper(string repoName, Uri repoUri, int repoPriority, bool repoTrusted, PSCredentialInfo repoCredentialInfo)
        {
            if (repoName.Equals("PSGallery", StringComparison.OrdinalIgnoreCase))
            {
                WriteVerbose("Provided Name (NameParameterSet) but with invalid value of PSGallery");
                throw new ArgumentException("Cannot register PSGallery with -Name parameter. Try: Register-PSResourceRepository -PSGallery");
            }

            return AddToRepositoryStoreHelper(repoName, repoUri, repoPriority, repoTrusted, repoCredentialInfo);
        }

        private PSRepositoryInfo PSGalleryParameterSetHelper(int repoPriority, bool repoTrusted)
        {
            Uri psGalleryUri = new Uri(PSGalleryRepoUri);
            WriteVerbose("(PSGallerySet) internal name and uri values for Add() API are hardcoded and validated, priority and trusted values, if passed in, also validated");
            return AddToRepositoryStoreHelper(PSGalleryRepoName, psGalleryUri, repoPriority, repoTrusted, repoCredentialInfo: null);
        }

        private List<PSRepositoryInfo> RepositoriesParameterSetHelper()
        {
            List<PSRepositoryInfo> reposAddedFromHashTable = new List<PSRepositoryInfo>();
            foreach (Hashtable repo in Repository)
            {
                if (repo.ContainsKey(PSGalleryRepoName))
                {
                    if (repo.ContainsKey("Name") || repo.ContainsKey("Uri") || repo.ContainsKey("CredentialInfo"))
                    {
                        WriteError(new ErrorRecord(
                                new PSInvalidOperationException("Repository hashtable cannot contain PSGallery key with -Name, -Uri and/or -CredentialInfo key value pairs"),
                                "NotProvideNameUriCredentialInfoForPSGalleryRepositoriesParameterSetRegistration",
                                ErrorCategory.InvalidArgument,
                                this));
                        continue;
                    }

                    try
                    {
                        WriteVerbose("(RepositoriesParameterSet): on repo: PSGallery. Registers PSGallery repository");
                        reposAddedFromHashTable.Add(PSGalleryParameterSetHelper(
                            repo.ContainsKey("Priority") ? (int)repo["Priority"] : defaultPriority,
                            repo.ContainsKey("Trusted") ? (bool)repo["Trusted"] : defaultTrusted));
                    }
                    catch (Exception e)
                    {
                        WriteError(new ErrorRecord(
                            new PSInvalidOperationException(e.Message),
                            "ErrorParsingIndividualRepoPSGallery",
                            ErrorCategory.InvalidArgument,
                            this));
                    }
                }
                else
                {
                    PSRepositoryInfo parsedRepoAdded = RepoValidationHelper(repo);
                    if (parsedRepoAdded != null)
                    {
                        reposAddedFromHashTable.Add(parsedRepoAdded);
                    }
                }
            }

            return reposAddedFromHashTable;
        }

        private PSRepositoryInfo RepoValidationHelper(Hashtable repo)
        {
            if (!repo.ContainsKey("Name") || String.IsNullOrEmpty(repo["Name"].ToString()))
            {
                WriteError(new ErrorRecord(
                        new PSInvalidOperationException("Repository name cannot be null"),
                        "NullNameForRepositoriesParameterSetRegistration",
                        ErrorCategory.InvalidArgument,
                        this));
                return null;
            }

            if (repo["Name"].ToString().Equals("PSGallery"))
            {
                WriteError(new ErrorRecord(
                        new PSInvalidOperationException("Cannot register PSGallery with -Name parameter. Try: Register-PSResourceRepository -PSGallery"),
                        "PSGalleryProvidedAsNameRepoPSet",
                        ErrorCategory.InvalidArgument,
                        this));
                return null;
            }

            if (!repo.ContainsKey("Uri") || String.IsNullOrEmpty(repo["Uri"].ToString()))
            {
                WriteError(new ErrorRecord(
                        new PSInvalidOperationException("Repository Uri cannot be null"),
                        "NullUriForRepositoriesParameterSetRegistration",
                        ErrorCategory.InvalidArgument,
                        this));
                return null;
            }

            if (!Utils.TryCreateValidUri(uriString: repo["Uri"].ToString(),
                cmdletPassedIn: this,
                uriResult: out Uri repoUri,
                errorRecord: out ErrorRecord errorRecord))
            {
                WriteError(errorRecord);
                return null;
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
                WriteVerbose(String.Format("(RepositoriesParameterSet): on repo: {0}. Registers Name based repository", repo["Name"]));
                return NameParameterSetHelper(repo["Name"].ToString(),
                    repoUri,
                    repo.ContainsKey("Priority") ? Convert.ToInt32(repo["Priority"].ToString()) : defaultPriority,
                    repo.ContainsKey("Trusted") ? Convert.ToBoolean(repo["Trusted"].ToString()) : defaultTrusted,
                    repoCredentialInfo);
            }
            catch (Exception e)
            {
                if (!(e is ArgumentException || e is PSInvalidOperationException))
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new PSInvalidOperationException(e.Message),
                        "TerminatingErrorParsingAddingIndividualRepo",
                        ErrorCategory.InvalidArgument,
                        this));
                }

                WriteError(new ErrorRecord(
                        new PSInvalidOperationException(e.Message),
                        "ErrorParsingIndividualRepo",
                        ErrorCategory.InvalidArgument,
                        this));
                return null;
            }
        }

        #endregion
    }
}
