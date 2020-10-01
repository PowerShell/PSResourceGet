
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Management.Automation;
using System.Globalization;
using Microsoft.PowerShell.PowerShellGet.RepositorySettings;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Register-PSResourceRepository cmdlet registers the default repository for PowerShell modules.
    /// After a repository is registered, you can reference it from the Find-PSResource, Install-PSResource, and Publish-PSResource cmdlets.
    /// The registered repository becomes the default repository in Find-Module and Install-Module.
    /// It returns nothing.
    /// </summary>

    [Cmdlet(VerbsLifecycle.Register, "PSResourceRepository", DefaultParameterSetName = "NameParameterSet", SupportsShouldProcess = true,
        HelpUri = "<add>", RemotingCapability = RemotingCapability.None)]
    public sealed
    class RegisterPSResourceRepository : PSCmdlet
    {
        private string PSGalleryRepoName = "PSGallery";
        private string PSGalleryRepoURL = "https://www.powershellgallery.com/api/v2";

        /// <summary>
        /// Specifies the desired name for the repository to be registered.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "NameParameterSet")]
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
        /// Specifies the location of the repository to be registered.
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public Uri URL
        {
            get
            { return _url; }

            set
            {
                Uri url;
                if (!Uri.TryCreate(value, string.Empty, out url))
                {
                    // Try the URL as a file path
                    var resolvedPath = string.Format(CultureInfo.InvariantCulture, "{0}{1}{2}", Uri.UriSchemeFile, Uri.SchemeDelimiter, SessionState.Path.GetResolvedPSPathFromPSPath(value.ToString()).FirstOrDefault().Path);
                    if (!Uri.TryCreate(resolvedPath, UriKind.Absolute, out url))
                    {
                        var message = string.Format(CultureInfo.InvariantCulture, "The URL provided is not valid: {0}", value);
                        var ex = new ArgumentException(message);
                        var moduleManifestNotFound = new ErrorRecord(ex, "InvalidUrl", ErrorCategory.InvalidArgument, null);
                        ThrowTerminatingError(moduleManifestNotFound);
                    }
                }

                _url = url;
            }
        }
        private Uri _url;

        /// <summary>
        /// Registers the PowerShell Gallery.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "PSGalleryParameterSet")]
        public SwitchParameter PSGallery
        {
            get
            { return _psgallery; }

            set
            { _psgallery = value; }
        }
        private SwitchParameter _psgallery;

        /// <summary>
        /// Repositories is a hashtable and is used to register multiple repositories at once.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "RepositoriesParameterSet")]
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
        [Parameter(ParameterSetName = "PSGalleryParameterSet")]
        [Parameter(ParameterSetName = "NameParameterSet")]
        public SwitchParameter Trusted
        {
            get { return _trusted; }

            set { _trusted = value; }
        }
        private SwitchParameter _trusted;

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
        [Parameter(ParameterSetName = "PSGalleryParameterSet")]
        [Parameter(ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        [ValidateRange(0, 50)]
        public int Priority
        {
            get { return _priority; }

            set { _priority = value; }
        }
        private int _priority = 50;

        /// <summary>
        /// </summary>
        protected override void ProcessRecord()
        {
            var r = new RespositorySettings();

            if (ParameterSetName.Equals("PSGalleryParameterSet"))
            {
                if (!_psgallery)
                {
                    return;
                }

                /// collect parameters and make one call 
                var psGalleryUri = new Uri(PSGalleryRepoURL);

                try
                {
                    r.Add(PSGalleryRepoName, psGalleryUri, _priority, _trusted);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message);
                }

            }
            else if (ParameterSetName.Equals("NameParameterSet"))
            {
                if (String.IsNullOrEmpty(_name))
                {
                    throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Repository name cannot be null"));
                }
                if (String.IsNullOrEmpty(_url.ToString()))
                {
                    throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Repository url cannot be null"));
                }

                try
                {
                    r.Add(_name, _url, _priority, _trusted);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message);
                }
            }
            else if (ParameterSetName.Equals("RepositoriesParameterSet"))
            {      
                foreach (var repo in _repositories)
                {
                    if (repo.ContainsKey(PSGalleryRepoName)   )
                    {
                        var _psGalleryRepoURL = new Uri(PSGalleryRepoURL);
                        int _psGalleryRepoPriority = repo.ContainsKey("Priority") ? (int)repo["Priority"] : 50;
                        var _psGalleryRepoTrusted = repo.ContainsKey("Trusted") ? (bool)repo["Trusted"] : false;

                        r.Add(PSGalleryRepoName, _psGalleryRepoURL, _psGalleryRepoPriority, _psGalleryRepoTrusted);
                        continue;
                    }

                    // check if key exists
                    if (!repo.ContainsKey("Name") || String.IsNullOrEmpty(repo["Name"].ToString()))
                    {
                        throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Repository name cannot be null"));
                    }
                    if (!repo.ContainsKey("Url") || String.IsNullOrEmpty(repo["Url"].ToString()))
                    {
                        throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Repository url cannot be null"));
                    }

                    // https://docs.microsoft.com/en-us/dotnet/api/system.uri.trycreate?view=netframework-4.8#System_Uri_TryCreate_System_Uri_System_Uri_System_Uri__
                    // convert the string to a url and check to see if the url is formatted correctly  
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

                    bool _repoTrusted = false;
                    if (repo.ContainsKey("Trusted"))
                    {
                        _repoTrusted = Convert.ToBoolean(repo["Trusted"].ToString());
                    }

                    r.Add(repo["Name"].ToString(), _repoURL, _repoPriority, _repoTrusted);                    
                }      
            }
            else if (_name.Equals(PSGalleryRepoName))
            {
                //throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, Messages.UsePSGalleryParameterSetOnRegister));
                throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Use PSGallery Parmenter Set on Register"));
            }
        }
    }
}