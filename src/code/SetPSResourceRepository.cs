
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using System;
using System.Collections;
using System.Management.Automation;
using System.Globalization;
using Microsoft.PowerShell.PowerShellGet.RepositorySettings;
using System.Collections.Generic;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Set-PSResourceRepository cmdlet sets properties for repositories.
    /// After a repository is registered, you can reference it from the Find-PSResource, Install-PSResource, and Publish-PSResource cmdlets.
    /// The registered repository becomes the default repository in Find-Module and Install-Module.
    /// It returns nothing.
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "PSResourceRepository", DefaultParameterSetName = "NameParameterSet", SupportsShouldProcess = true,
        HelpUri = "<add>", RemotingCapability = RemotingCapability.None)]
    public sealed
    class SetPSResourceRepository : PSCmdlet
    {
      //  private string PSGalleryRepoName = "PSGallery";

        /// <summary>
        /// Specifies the desired name for the repository to be set.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string Name
        {
            get
            { return _name; }

            set
            { _name = value; }
        }
        private string _name;


        /// <summary>
        /// Specifies the location of the repository to be set.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public Uri URL
        {
            get
            { return _url; }

            set
            { _url = value; }
        }
        private Uri _url = null;


        /// <summary>
        /// Specifies a user account that has rights to find a resource from a specific repository.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        public PSCredential Credential
        {
            get
            { return _credential; }

            set
            { _credential = value; }
        }
        private PSCredential _credential = null;


        /// <summary>
        /// Repositories is a hashtable and is used to register multiple repositories at once.
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "RepositoriesParameterSet")]
        [ValidateNotNullOrEmpty]
        public List<Hashtable> Repositories
        {
            get { return _repositories; }

            set { _repositories = value; }
        }
        private List<Hashtable> _repositories;


        /// <summary>
        /// Specifies whether the repository should be trusted.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        public SwitchParameter Trusted
        {
            get { return _trusted; }

            set { _trusted = value; isSet = true; }
        }
        private SwitchParameter _trusted;
        private bool isSet = false;                  /***************************************************/


        /// <summary>
        /// Specifies a proxy server for the request, rather than a direct connection to the internet resource.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public Uri Proxy
        {
            get
            { return _proxy; }

            set
            { _proxy = value; }
        }
        private Uri _proxy;


        /// <summary>
        /// Specifies a user account that has permission to use the proxy server that is specified by the Proxy parameter.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        public PSCredential ProxyCredential
        {
            get
            { return _proxyCredential; }

            set
            { _proxyCredential = value; }
        }
        private PSCredential _proxyCredential;


        /// <summary>
        /// The following is the definition of the input parameter "Port".
        /// Specifies the port to be used when connecting to the ws management service.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [ValidateRange(0, 50)]
        public int Priority
        {
            get { return _priority; }

            set { _priority = value; }
        }
        private int _priority = -1;




        /// <summary>
        /// </summary>
        protected override void ProcessRecord()
        {
            var r = new RespositorySettings();

            if (ParameterSetName.Equals("NameParameterSet"))
            {
                bool? _trustedNullable = isSet ? new bool?(_trusted) : new bool?();

                if (_name.Equals("PSGallery"))
                {
                    // if attempting to set -url with PSGallery, throw an error
                    if (_url != null)
                    {
                        throw new System.ArgumentException("The PSGallery repository has a pre-defined URL.  The -URL parmeter is not allowed, try running 'Register-PSResourceRepository -PSGallery'.");
                    }

                    Uri galleryURL = new Uri("https://www.powershellgallery.com/api/v2");

                    // name is the only thing that won't get updated
                    r.Update("PSGallery", galleryURL, _priority, _trustedNullable);

                }

                if (String.IsNullOrEmpty(_name))
                {
                    throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Repository name cannot be null."));
                }

                // https://docs.microsoft.com/en-us/dotnet/api/system.uri.trycreate?view=netframework-4.8#System_Uri_TryCreate_System_Uri_System_Uri_System_Uri__
                // check to see if the url is formatted correctly
                if (_url != null && !(Uri.TryCreate(_url.ToString(), UriKind.Absolute, out _url)
                     && (_url.Scheme == Uri.UriSchemeHttp || _url.Scheme == Uri.UriSchemeHttps || _url.Scheme == Uri.UriSchemeFile || _url.Scheme == Uri.UriSchemeFtp)))
                {
                    throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Invalid Url"));
                }

                r.Update(_name, _url, _priority, _trustedNullable);
            }
            else if (ParameterSetName.Equals("RepositoriesParameterSet"))
            {
                foreach (var repo in _repositories)
                {
                    if (repo.ContainsKey("PSGallery"))
                    {
                        // if attempting to set -URL with -PSGallery, throw an error
                        if (_url != null)
                        {
                            throw new System.ArgumentException("The PSGallery repository has a pre-defined URL.  The -URL parmeter is not allowed, try again after removing -URL.");
                        }

                        var _psGalleryRepoName = "PSGallery";
                        Uri _psGalleryRepoURL = new Uri("https://www.powershellgallery.com/api/v2");
                        int _psGalleryRepoPriority = repo.ContainsKey("Priority") ? (int)repo["Priority"] : 50;

                        bool? _psGalleryRepoTrusted = repo.ContainsKey("Trusted") ? (bool?)repo["Trusted"] : null;

                        Uri galleryURL = new Uri("https://www.powershellgallery.com");

                        r.Update(_psGalleryRepoName, _psGalleryRepoURL, _psGalleryRepoPriority, _psGalleryRepoTrusted);

                        continue;
                    }

                    // check if key exists
                    if (!repo.ContainsKey("Name") || String.IsNullOrEmpty(repo["Name"].ToString()))
                    {
                        throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Repository name cannot be null."));
                    }
                    if (!repo.ContainsKey("Url") || String.IsNullOrEmpty(repo["Url"].ToString()))
                    {
                        throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Repository url cannot be null."));
                    }

                    // https://docs.microsoft.com/en-us/dotnet/api/system.uri.trycreate?view=netframework-4.8#System_Uri_TryCreate_System_Uri_System_Uri_System_Uri__
                    // convert the string to a url and check to see if the url is formatted correctly
                    /// Checked URL
                    Uri _repoURL;
                    if (!(Uri.TryCreate(repo["URL"].ToString(), UriKind.Absolute, out _repoURL)
                         && (_repoURL.Scheme == Uri.UriSchemeHttp || _repoURL.Scheme == Uri.UriSchemeHttps || _repoURL.Scheme == Uri.UriSchemeFtp || _repoURL.Scheme == Uri.UriSchemeFile)))
                    {
                        throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Invalid Url"));
                    }

                    int _repoPriority = 50;
                    if (repo.ContainsKey("Priority"))
                    {
                        _repoPriority = Convert.ToInt32(repo["Priority"].ToString());
                    }

                    bool? _repoTrusted = repo.ContainsKey("Trusted") ? (bool?)repo["Trusted"] : null;

                    r.Update(repo["Name"].ToString(), _repoURL, _repoPriority, _repoTrusted);
                }
            }
        }


        /// <summary>
        /// </summary>
        protected override void EndProcessing()
        {

        }
    }
}



