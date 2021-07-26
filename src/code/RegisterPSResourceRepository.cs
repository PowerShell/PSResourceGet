// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
        SupportsShouldProcess = true,
        HelpUri = "<add>")]
    public sealed
    class RegisterPSResourceRepository : PSCmdlet
    {
        #region Members

        private readonly string PSGalleryRepoName = "PSGallery";
        private readonly string PSGalleryRepoURL = "https://www.powershellgallery.com/api/v2";
        private const int defaultPriority = 50;
        private const bool defaultTrusted = false;
        private const string NameParameterSet = "NameParameterSet";
        private const string PSGalleryParameterSet = "PSGalleryParameterSet";
        private const string RepositoriesParameterSet = "RepositoriesParameterSet";

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
        public string URL { get; set; }

        // /// <summary>
        // /// Specifies the location of the repository to be registered.
        // /// </summary>
        // [Parameter(Mandatory = true, Position = 1, ParameterSetName = NameParameterSet)]
        // [ValidateNotNullOrEmpty]
        // public Uri URL {
        //     get
        //     { return _url; }

        //     set
        //     {
        //         string urlString = SessionState.Path.GetResolvedPSPathFromPSPath(value.AbsoluteUri)[0].Path;
        //         // tryCreateResult = Uri.TryCreate(url, UriKind.Absolute, out _url);

        //         if (!Uri.TryCreate(urlString, UriKind.Absolute, out Uri url))
        //         {
        //             var message = string.Format(CultureInfo.InvariantCulture, "The URL provided is not valid: {0}", value);
        //             var ex = new ArgumentException(message);
        //             var moduleManifestNotFound = new ErrorRecord(ex, "InvalidUrl", ErrorCategory.InvalidArgument, null);
        //             ThrowTerminatingError(moduleManifestNotFound);
        //         }

        //         _url = url;
        //     }
        // }


        /// <summary>
        /// When specified, registers PSGallery repository.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = PSGalleryParameterSet)]
        public SwitchParameter PSGallery { get; set; }

        /// <summary>
        /// Specifies a hashtable of repositories and is used to register multiple repositories at once.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "RepositoriesParameterSet", ValueFromPipeline = true)]
        [ValidateNotNullOrEmpty]
        public Hashtable[] Repositories {get; set;}

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
        private Uri _url;

        protected override void BeginProcessing()
        {
            // bool tryCreateResult = false;
            // try
            // {
            //     tryCreateResult = Uri.TryCreate(url, UriKind.Absolute, out _url);
            // }
            // catch (Exception e)
            // {
            //     var message = string.Format("Uri.TryCreate on provided Url threw error: " + e.Message);
            //     var ex = new ArgumentException(message);
            //     var tryCreateFailure = new ErrorRecord(ex, "TryCreateFails", ErrorCategory.InvalidArgument, null);
            //     ThrowTerminatingError(tryCreateFailure);
            // }

            // if (!tryCreateResult)
            // {
            //     var message = string.Format(CultureInfo.InvariantCulture, "The URL provided is not valid: {0}", url);
            //     var ex = new ArgumentException(message);
            //     var moduleManifestNotFound = new ErrorRecord(ex, "InvalidUrl", ErrorCategory.InvalidArgument, null);
            //     ThrowTerminatingError(moduleManifestNotFound);
            // }

            if (Proxy != null || ProxyCredential != null)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new PSNotImplementedException("Proxy and ProxyCredential are not yet implemented. Please rerun cmdlet with other parameters."),
                    "ParametersNotImplementedYet",
                    ErrorCategory.NotImplemented,
                    this));
            }

            try
            {
                RepositorySettings.CheckRepositoryStore();
            }
            catch (PSInvalidOperationException e)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new PSInvalidOperationException(e.Message),
                    "RepositoryStoreException",
                    ErrorCategory.ReadError,
                    this));
            }
        }
        protected override void ProcessRecord()
        {
            List<PSRepositoryInfo> items = new List<PSRepositoryInfo>();

            switch (ParameterSetName)
            {
                case NameParameterSet:
                    string url = URL;
                    if (!URL.StartsWith(Uri.UriSchemeHttp) && !URL.StartsWith(Uri.UriSchemeHttps) && !URL.StartsWith(Uri.UriSchemeFtp))
                    {
                        url = SessionState.Path.GetResolvedPSPathFromPSPath(URL)[0].Path;
                    }

                    bool tryCreateResult = Utils.CreateUrl(url, out _url, out ErrorRecord errorRecord);

                    if (!tryCreateResult || _url == null)
                    {
                        ThrowTerminatingError(errorRecord);
                    }

                    try
                    {
                        items.Add(NameParameterSetHelper(Name, _url, Priority, Trusted));
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

        private PSRepositoryInfo AddToRepositoryStoreHelper(string repoName, Uri repoUrl, int repoPriority, bool repoTrusted)
        {
            // remove trailing and leading whitespaces, and if Name is just whitespace Name should become null now and be caught by following condition
            repoName = repoName.Trim(' ');
            if (String.IsNullOrEmpty(repoName) || repoName.Contains("*"))
            {
                throw new ArgumentException("Name cannot be null/empty, contain asterisk or be just whitespace");
            }

            if (repoUrl == null || !(repoUrl.Scheme == Uri.UriSchemeHttp || repoUrl.Scheme == Uri.UriSchemeHttps || repoUrl.Scheme == Uri.UriSchemeFtp || repoUrl.Scheme == Uri.UriSchemeFile))
            {
                throw new ArgumentException("Invalid url, must be one of the following Uri schemes: HTTPS, HTTP, FTP, File Based");
            }

            WriteDebug("All required values to add to repository provided, calling internal Add() API now");
            if (!ShouldProcess(repoName, "Register repository to repository store"))
            {
                return null;
            }

            return RepositorySettings.Add(repoName, repoUrl, repoPriority, repoTrusted);
        }

        private PSRepositoryInfo NameParameterSetHelper(string repoName, Uri repoUrl, int repoPriority, bool repoTrusted)
        {
            if (repoName.Equals("PSGallery", StringComparison.OrdinalIgnoreCase))
            {
                WriteDebug("Provided Name (NameParameterSet) but with invalid value of PSGallery");
                throw new ArgumentException("Cannot register PSGallery with -Name parameter. Try: Register-PSResourceRepository -PSGallery");
            }

            return AddToRepositoryStoreHelper(repoName, repoUrl, repoPriority, repoTrusted);
        }

        private PSRepositoryInfo PSGalleryParameterSetHelper(int repoPriority, bool repoTrusted)
        {
            Uri psGalleryUri = new Uri(PSGalleryRepoURL);
            WriteDebug("(PSGallerySet) internal name and uri values for Add() API are hardcoded and validated, priority and trusted values, if passed in, also validated");
            return AddToRepositoryStoreHelper(PSGalleryRepoName, psGalleryUri, repoPriority, repoTrusted);
        }

        private List<PSRepositoryInfo> RepositoriesParameterSetHelper()
        {
            List<PSRepositoryInfo> reposAddedFromHashTable = new List<PSRepositoryInfo>();
            foreach (Hashtable repo in Repositories)
            {
                if (repo.ContainsKey(PSGalleryRepoName))
                {
                    if (repo.ContainsKey("Name") || repo.ContainsKey("Url"))
                    {
                        WriteError(new ErrorRecord(
                                new PSInvalidOperationException("Repository hashtable cannot contain PSGallery key with -Name and/or -URL key value pairs"),
                                "NotProvideNameUrlForPSGalleryRepositoriesParameterSetRegistration",
                                ErrorCategory.InvalidArgument,
                                this));
                        continue;
                    }

                    try
                    {
                        WriteDebug("(RepositoriesParameterSet): on repo: PSGallery. Registers PSGallery repository");
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

            if (!repo.ContainsKey("Url") || String.IsNullOrEmpty(repo["Url"].ToString()))
            {
                WriteError(new ErrorRecord(
                        new PSInvalidOperationException("Repository url cannot be null"),
                        "NullURLForRepositoriesParameterSetRegistration",
                        ErrorCategory.InvalidArgument,
                        this));
                return null;
            }

            // string url = repo["Url"].ToString();

            // if (!repo["Url"].ToString().StartsWith(Uri.UriSchemeHttp) &&
            //     !repo["Url"].ToString().StartsWith(Uri.UriSchemeHttps) &&
            //     !repo["Url"].ToString().StartsWith(Uri.UriSchemeFtp))
            // {
            //     url = SessionState.Path.GetResolvedPSPathFromPSPath(url)[0].Path;
            // }

            // bool tryCreateResult = Utils.CreateUrl(url, out Uri repoURL, out ErrorRecord errorRecord);

            // if (!tryCreateResult || repoURL == null)
            // {
            //     WriteError(errorRecord);
            //     return null;
            // }


            string url = repo["Url"].ToString();
            if (!repo["Url"].ToString().StartsWith(Uri.UriSchemeHttp) && !repo["Url"].ToString().StartsWith(Uri.UriSchemeHttps) && !repo["Url"].ToString().StartsWith(Uri.UriSchemeFtp))
            {
                url = SessionState.Path.GetResolvedPSPathFromPSPath(repo["Url"].ToString())[0].Path;
            }

            bool tryCreateResult = Utils.CreateUrl(url, out Uri repoURL, out ErrorRecord errorRecord);

            if (!tryCreateResult || repoURL == null)
            {
                WriteError(errorRecord);
                return null;
            }

            // if (!Uri.TryCreate(repo["URL"].ToString(), UriKind.Absolute, out Uri repoURL))
            // {
            //     WriteError(new ErrorRecord(
            //         new PSInvalidOperationException("Invalid url, unable to create"),
            //         "InvalidUrlScheme",
            //         ErrorCategory.InvalidArgument,
            //         this));
            //     return null;
            // }

            try
            {
                WriteDebug(String.Format("(RepositoriesParameterSet): on repo: {0}. Registers Name based repository", repo["Name"]));
                return NameParameterSetHelper(repo["Name"].ToString(),
                    repoURL,
                    repo.ContainsKey("Priority") ? Convert.ToInt32(repo["Priority"].ToString()) : defaultPriority,
                    repo.ContainsKey("Trusted") ? Convert.ToBoolean(repo["Trusted"].ToString()) : defaultTrusted);
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
