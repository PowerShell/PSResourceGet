// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Management.Automation;
using System.Globalization;
using System.Collections.Generic;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;

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
        public Uri URL
        {
            get
            { return _url; }

            set
            {
                Uri url;
                if(!(Uri.TryCreate(value, string.Empty, out url)
                    && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeFtp || url.Scheme == Uri.UriSchemeFile)))
                    {
                        var message = string.Format(CultureInfo.InvariantCulture, "The URL provided is not valid: {0}", value);
                        var ex = new ArgumentException(message);
                        var moduleManifestNotFound = new ErrorRecord(ex, "InvalidUrl", ErrorCategory.InvalidArgument, null);
                        ThrowTerminatingError(moduleManifestNotFound);
                    }
                _url = url;
            }
        }
        private Uri _url;

        /// <summary>
        /// When specified, registers PSGallery repository.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = PSGalleryParameterSet)]
        public SwitchParameter PSGallery { get; set; }

        /// <summary>
        /// Specifies a hashtable of repositories and is used to register multiple repositories at once.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "RepositoriesParameterSet")]
        [ValidateNotNullOrEmpty]
        public List<Hashtable> Repositories { get; set; }

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
        [ValidateNotNullOrEmpty]
        [ValidateRange(0, 50)]
        public int Priority { get; set; } = defaultPriority;

        /// <summary>
        /// Specifies a proxy server for the request, rather than a direct connection to the internet resource.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public Uri Proxy { get; set; }

        /// <summary>
        /// Specifies a user account that has permission to use the proxy server that is specified by the Proxy parameter.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        public PSCredential ProxyCredential { get; set; }

        /// <summary>
        /// When specified, displays the succcessfully registered repository and its information
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        #endregion

        #region NewWay
        protected override void ProcessRecord()
        {
            List<PSRepositoryItem> items = new List<PSRepositoryItem>();

            switch(ParameterSetName)
            {
                case NameParameterSet:
                    items.Add(NameParameterSetHelper(Name, URL, Priority, Trusted));
                    break;

                case PSGalleryParameterSet:
                    items.Add(PSGalleryParameterSetHelper(Priority, Trusted));
                    break;

                case RepositoriesParameterSet:
                    items = RepositoriesParameterSetHelper(); //potential problem if users are allowed to use multiple param sets at a time...?
                    break;

                default:
                    WriteDebug("Invalid parameter set");
                    break;

            }
            if(PassThru)
            {
                foreach(PSRepositoryItem repo in items)
                {
                    WriteObject(repo);
                }
            }
        }

        private PSRepositoryItem NameParameterSetHelper(string repoName, Uri repoUrl, int repoPriority, bool repoTrusted)
        {
            PSRepositoryItem repoItem = RepositorySettings.Add(repoName, repoUrl, repoPriority, repoTrusted);
            return repoItem;
        }

        private PSRepositoryItem PSGalleryParameterSetHelper(int repoPriority, bool repoTrusted)
        {
            Uri psGalleryUri = new Uri(PSGalleryRepoURL);
            PSRepositoryItem repoItem = RepositorySettings.Add(PSGalleryRepoName, psGalleryUri, repoPriority, repoTrusted);
            return repoItem;
        }

        private List<PSRepositoryItem> RepositoriesParameterSetHelper()
        {
            List<PSRepositoryItem> reposAddedFromHashTable = new List<PSRepositoryItem>();
            foreach(Hashtable repo in Repositories)
            {
                if(repo.ContainsKey(PSGalleryRepoName))
                {
                    reposAddedFromHashTable.Add(PSGalleryParameterSetHelper(repo.ContainsKey("Priority") ? (int)repo["Priority"] : defaultPriority,
                        repo.ContainsKey("Trusted") ? (bool)repo["Trusted"] : defaultTrusted));
                }
                else
                {
                    try{
                        PSRepositoryItem parsedRepoAdded = NameHashTableHelper(repo);
                        reposAddedFromHashTable.Add(parsedRepoAdded);
                    }
                    catch(Exception e)
                    {
                        WriteDebug("some error happened with repo x" + e.Message); // non terminating error?
                    }
                }
            }
            return reposAddedFromHashTable;
        }

        private PSRepositoryItem NameHashTableHelper(Hashtable repo)
        {
                if(!repo.ContainsKey("Name") || String.IsNullOrEmpty(repo["Name"].ToString()))
                {
                    throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Repository name cannot be null"));
                }
                if(!repo.ContainsKey("Url") || String.IsNullOrEmpty(repo["Url"].ToString()))
                {
                    throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Repository url cannot be null"));
                }

                Uri repoURL;
                if(!(Uri.TryCreate(repo["URL"].ToString(), UriKind.Absolute, out repoURL)
                    && (repoURL.Scheme == Uri.UriSchemeHttp || repoURL.Scheme == Uri.UriSchemeHttps || repoURL.Scheme == Uri.UriSchemeFtp || repoURL.Scheme == Uri.UriSchemeFile)))
                {
                        throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Invalid Url"));
                }

                return NameParameterSetHelper(repo["Name"].ToString(),
                    repoURL,
                    repo.ContainsKey("Priority") ? Convert.ToInt32(repo["Priority"].ToString()) : defaultPriority,
                    repo.ContainsKey("Trusted") ? Convert.ToBoolean(repo["Trusted"].ToString()) : defaultTrusted);
        }
        #endregion


    }
}
