// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;

using Dbg = System.Diagnostics.Debug;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    /// <summary>
    /// The Register-PSResourceRepository cmdlet replaces the Register-PSRepository from V2.
    /// It registers a repository for PowerShell modules.
    /// The repository is registered to the current user's scope and does not have a system-wide scope.
    /// </summary>

    [Cmdlet(VerbsLifecycle.Register,
        "PSResourceRepository",
        DefaultParameterSetName = NameParameterSet,
        SupportsShouldProcess = true,
        ConfirmImpact = ConfirmImpact.Low)]
    public sealed
    class RegisterPSResourceRepository : PSCmdlet
    {
        #region Members

        private readonly string PSGalleryRepoName = "PSGallery";
        private readonly string PSGalleryRepoUri = "https://www.powershellgallery.com/api/v2";
        private const int DefaultPriority = 50;
        private const bool DefaultTrusted = false;
        private const string NameParameterSet = "NameParameterSet";
        private const string PSGalleryParameterSet = "PSGalleryParameterSet";
        private const string RepositoriesParameterSet = "RepositoriesParameterSet";
        private Uri _uri;

        #endregion

        #region Parameters

        /// <summary>
        /// Specifies name for the repository to be registered.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = NameParameterSet, HelpMessage = "Name of the repository that is to be registered.")]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        /// <summary>
        /// Specifies the location of the repository to be registered.
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = NameParameterSet, HelpMessage = "Location of the repository that is to be registered.")]
        [ValidateNotNullOrEmpty]
        public string Uri { get; set; }

        /// <summary>
        /// When specified, registers PSGallery repository.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = PSGalleryParameterSet, HelpMessage = "PSGallery switch used to indicate registering PSGallery repository.")]
        public SwitchParameter PSGallery { get; set; }

        /// <summary>
        /// Specifies a hashtable of repositories and is used to register multiple repositories at once.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = RepositoriesParameterSet, HelpMessage = "Hashtable including information on single or multiple repositories to be registered.")]
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
        /// Valid priority values range from 0 to 100, such that a lower numeric value (i.e 10) corresponds
        /// to a higher priority ranking than a higher numeric value (i.e 40). Has default value of 50.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        [Parameter(ParameterSetName = PSGalleryParameterSet)]
        [ValidateRange(0, 100)]
        public int Priority { get; set; } = DefaultPriority;

        /// <summary>
        /// Specifies the Api version of the repository to be set.
        /// </sumamry>
        [Parameter(ParameterSetName = NameParameterSet)]
        public PSRepositoryInfo.APIVersion ApiVersion { get; set; }

        /// <summary>
        /// Specifies vault and secret names as PSCredentialInfo for the repository.
        /// </summary>
        [Parameter(ParameterSetName = NameParameterSet)]
        public PSCredentialInfo CredentialInfo { get; set; }

        /// <summary>
        /// When specified, displays the succcessfully registered repository and its information.
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }
        
        /// <summary>
        /// When specified, will overwrite information for any existing repository with the same name.
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        #endregion

        #region Methods

        protected override void BeginProcessing()
        {
            RepositorySettings.CheckRepositoryStore();
        }
        protected override void ProcessRecord()
        {
            List<PSRepositoryInfo> items = new List<PSRepositoryInfo>();

            PSRepositoryInfo.APIVersion? repoApiVersion = null;
            if (MyInvocation.BoundParameters.ContainsKey(nameof(ApiVersion)))
            {
                repoApiVersion = ApiVersion;
            }

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
                        items.Add(RepositorySettings.AddRepository(Name, _uri, Priority, Trusted, repoApiVersion, CredentialInfo, Force, this, out string errorMsg));

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


        private PSRepositoryInfo PSGalleryParameterSetHelper(int repoPriority, bool repoTrusted)
        {
            WriteDebug("In RegisterPSResourceRepository::PSGalleryParameterSetHelper()");
            Uri psGalleryUri = new Uri(PSGalleryRepoUri);
            WriteDebug("Internal name and uri values for PSGallery are hardcoded and validated. Priority and trusted values, if passed in, also validated");
            var addedRepo = RepositorySettings.AddToRepositoryStore(PSGalleryRepoName, 
                psGalleryUri, 
                repoPriority, 
                repoTrusted, 
                apiVersion: null,
                repoCredentialInfo: null, 
                Force, 
                this, 
                out string errorMsg);

            if (!string.IsNullOrEmpty(errorMsg))
            {
                ThrowTerminatingError(new ErrorRecord(
                    new PSInvalidOperationException(errorMsg),
                    "RepositoryCredentialSecretManagementUnavailableModule",
                    ErrorCategory.ResourceUnavailable,
                    this));
            }

            return addedRepo;
        }

        private List<PSRepositoryInfo> RepositoriesParameterSetHelper()
        {
            WriteDebug("In RegisterPSResourceRepository::RepositoriesParameterSetHelper()");
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
                        WriteDebug("Registering PSGallery repository");
                        reposAddedFromHashTable.Add(PSGalleryParameterSetHelper(
                            repo.ContainsKey("Priority") ? (int)repo["Priority"] : DefaultPriority,
                            repo.ContainsKey("Trusted") ? (bool)repo["Trusted"] : DefaultTrusted));
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
            WriteDebug("In RegisterPSResourceRepository::RepoValidationHelper()");
            if (!repo.ContainsKey("Name") || repo["Name"] == null || String.IsNullOrWhiteSpace(repo["Name"].ToString()))
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

            if (!repo.ContainsKey("Uri") || repo["Uri"] == null || String.IsNullOrEmpty(repo["Uri"].ToString()))
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

            if (repo.ContainsKey("ApiVersion") && 
                (repo["ApiVersion"] == null || String.IsNullOrEmpty(repo["ApiVersion"].ToString()) ||
                !(repo["ApiVersion"].ToString().Equals("local") || repo["ApiVersion"].ToString().Equals("v2") || 
                repo["ApiVersion"].ToString().Equals("v3") || repo["ApiVersion"].ToString().Equals("nugetServer") || repo["ApiVersion"].ToString().Equals("unknown"))))
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException("Repository ApiVersion must be either 'local', 'v2', 'v3', 'nugetServer' or 'unknown'"),
                    "IncorrectApiVersionForRepositoriesParameterSetRegistration",
                    ErrorCategory.InvalidArgument,
                    this));

                return null;
            }

            try
            {
                WriteDebug($"Registering repository '{repo["Name"]}' with uri '{repoUri}'");
                var addedRepo = RepositorySettings.AddRepository(repo["Name"].ToString(),
                    repoUri,
                    repo.ContainsKey("Priority") ? Convert.ToInt32(repo["Priority"].ToString()) : DefaultPriority,
                    repo.ContainsKey("Trusted") ? Convert.ToBoolean(repo["Trusted"].ToString()) : DefaultTrusted,
                    apiVersion: repo.ContainsKey("Trusted") ? (PSRepositoryInfo.APIVersion?) repo["ApiVersion"] : null,
                    repoCredentialInfo,
                    Force,
                    this,
                    out string errorMsg);

                if (!string.IsNullOrEmpty(errorMsg))
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new PSInvalidOperationException(errorMsg),
                        "RegisterRepositoryError",
                        ErrorCategory.ResourceUnavailable,
                        this));
                }

                return addedRepo;
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
