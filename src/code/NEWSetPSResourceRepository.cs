using System;
using System.Collections;
using System.Management.Automation;
using System.Globalization;
using Microsoft.PowerShell.PowerShellGet.NEWRepositorySettings;
using Microsoft.PowerShell.PowerShellGet.NEWPSRepositoryItem;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Set-PSResourceRepository cmdlet replaces the Set-PSRepository cmdlet from V2.
    /// It sets the values for an already registered module repository. Specifically, it sets values for either the -URL, -Trusted and -Priority parameter arguments by additionally providing the -Name parameter argument.
    /// The settings are persistent for the current user and apply to all versions of PowerShell installed for that user.
    /// </summary>

    [Cmdlet(VerbsCommon.Set,
        "NEWPSResourceRepository",
        DefaultParameterSetName = "NameParameterSet",
        SupportsShouldProcess = true,
        HelpUri = "<add>",
        RemotingCapability = RemotingCapability.None)]
    public sealed
    class NEWSetPSResourceRepository : PSCmdlet
    {
        #region Parameters
        // the default value for priority, if this value is used for the priority that means the priority wasn't requested to be set
        private const int unchangedPriority = -1;

        /// <summary>
        /// Specifies name for the repository to be registered.
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
            {
                _name = value;
            }
        }
        private string _name;

        /// <summary>
        /// Specifies the location of the repository to be registered.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
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
            } //todo: actually set code here
        }
        private Uri _url;

        /// <summary>
        /// Specifies whether the repository should be trusted.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        public SwitchParameter Trusted
        {
            get
            { return _trusted; }

            set
            { _trusted = value; isSet = true; }
        }
        private SwitchParameter _trusted;

        //represents whether trusted's value has been set, false by default
        private bool isSet = false;

        /// <summary>
        /// Specifies the priority ranking of the repository, such that repositories with higher ranking priority are searched
        /// before a lower ranking priority one, when searching for a repository item across multiple registered repositories.
        /// Valid priority values range from 0 to 50, such that a lower numeric value (i.e 10) corresponds
        /// to a higher priority ranking than a higher numeric value (i.e 40). Has default value of 50.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        [ValidateRange(0, 50)]
        public int Priority
        {
            get
            { return _priority; }

            set
            { _priority = value; }
        }
        private int _priority = unchangedPriority;

        /// <summary>
        /// When specified, displays the succcessfully registered repository and its information
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru
        {
            get
            { return _passThru; }

            set
            { _passThru = value; }
        }
        private SwitchParameter _passThru;

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
        /// Specifies a proxy server for the request, rather than a direct connection to the internet resource.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
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
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        public PSCredential ProxyCredential
        {
            get
            { return _proxyCredential; }

            set
            { _proxyCredential = value; }
        }
        private PSCredential _proxyCredential;

        /// <summary>
        /// Specifies a hashtable of repositories and is used to register multiple repositories at once.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "RepositoriesParameterSet")]
        [ValidateNotNullOrEmpty]
        public List<Hashtable> Repositories
        {
            get
            { return _repositories; }

            set
            { _repositories = value; }
        }
        private List<Hashtable> _repositories;

        #endregion

        #region Methods
        protected override void ProcessRecord()
        {
            var r = new NEWRespositorySettings();
            List<NEWPSRespositoryItem> items = new List<NEWPSRespositoryItem>();
            if(ParameterSetName.Equals("NameParameterSet"))
            {
                if (String.IsNullOrEmpty(_name))
                {
                    throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Repository name cannot be null."));
                }
                else {
                    NameParameterSetHelper(r, items);
                }
            }
            else if(ParameterSetName.Equals("RepositoriesParameterSet"))
            {
                WriteDebug("in repo parameterSet");
                RepositoryParameterSetHelper(r, items);
            }

            if(_passThru)
            {
                items = items.OrderBy(x => x.Priority).ThenBy(x => x.Name).ToList();
                foreach(var item in items)
                {
                    WriteObject(item);
                }
            }
        }

        private void NameParameterSetHelper(NEWRespositorySettings rs, List<NEWPSRespositoryItem> items)
        {
            bool? _trustedNullable = isSet ? new bool?(_trusted) : new bool?();
            if(_name.Equals("PSGallery"))
            {
                if(_url != null)
                {
                    throw new System.ArgumentException("The PSGallery repository has a pre-defined URL.  The -URL parmeter is not allowed, try running 'Register-PSResourceRepository -PSGallery'.");
                }
                Uri galleryURL = new Uri("https://www.powershellgallery.com/api/v2");

                // name is the only thing that won't get updated
                WriteDebug("in nameparamsethelper(), ps gallery check, priority is: " + _priority);
                items.Add(rs.Update("PSGallery", galleryURL, _priority, _trustedNullable));
            }
            //name not PSGallery
            else {
                // https://docs.microsoft.com/en-us/dotnet/api/system.uri.trycreate?view=netframework-4.8#System_Uri_TryCreate_System_Uri_System_Uri_System_Uri__
                // check to see if the url is formatted correctly
                WriteDebug("_url is null?: " + (_url == null));
                if (_url != null && !(Uri.TryCreate(_url.ToString(), UriKind.Absolute, out _url)
                    && (_url.Scheme == Uri.UriSchemeHttp || _url.Scheme == Uri.UriSchemeHttps || _url.Scheme == Uri.UriSchemeFile || _url.Scheme == Uri.UriSchemeFtp)))
                {
                    throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Invalid Url"));
                }
                WriteDebug("after trycreate, _url is null?: " + (_url == null) + ", name: " + _name + ", priority: " + _priority);
                items.Add(rs.Update(_name, _url, _priority, _trustedNullable));
            }
        }

        private void RepositoryParameterSetHelper(NEWRespositorySettings rs, List<NEWPSRespositoryItem> items)
        {
            foreach(var repo in _repositories)
            {
                if(repo.ContainsKey("PSGallery"))
                {
                    if(repo.ContainsKey("Url"))
                    {
                        throw new System.ArgumentException("The PSGallery repository has a pre-defined URL.  The -URL parmeter is not allowed, try again after removing -URL.");
                    }
                    _name = "PSGallery";
                    _priority = repo.ContainsKey("Priority") ? (int)repo["Priority"] : unchangedPriority;
                    _trusted = repo.ContainsKey("Trusted") ? (bool)repo["Trusted"] : isSet = false;
                    Uri _repoURL = null;
                    _url = _repoURL;
                    NameParameterSetHelper(rs, items);
                }
                // repo doesn't contain PSGallery, some other Name will be used
                else
                {
                    WriteDebug("in non PSGallery check");
                    // check if key exists
                    if (!repo.ContainsKey("Name") || String.IsNullOrEmpty(repo["Name"].ToString()))
                    {
                        throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Repository name cannot be null."));
                    }
                    // if url exists, it's value should be non-null and non-empty
                    if (repo.ContainsKey("Url") && String.IsNullOrEmpty(repo["Url"].ToString()))
                    {
                        throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "If specified then repository url cannot be null."));
                    }

                    // https://docs.microsoft.com/en-us/dotnet/api/system.uri.trycreate?view=netframework-4.8#System_Uri_TryCreate_System_Uri_System_Uri_System_Uri__
                    // convert the string to a url and check to see if the url is formatted correctly
                    // Checked URL
                    Uri _repoURL = null;
                    if (repo.ContainsKey("Url")  && !(Uri.TryCreate(repo["URL"].ToString(), UriKind.Absolute, out _repoURL)
                         && (_repoURL.Scheme == Uri.UriSchemeHttp || _repoURL.Scheme == Uri.UriSchemeHttps || _repoURL.Scheme == Uri.UriSchemeFtp || _repoURL.Scheme == Uri.UriSchemeFile)))
                    {
                        throw new System.ArgumentException(string.Format(CultureInfo.InvariantCulture, "Invalid Url"));
                    }

                    // _url = repo["URL"];

                    _name = repo["Name"].ToString();
                    _url = _repoURL;
                    _priority = repo.ContainsKey("Priority") ? Convert.ToInt32(repo["Priority"].ToString()) : unchangedPriority;
                    _trusted = repo.ContainsKey("Trusted") ? (bool)repo["Trusted"] : isSet = false;
                    WriteDebug("able to get here");
                    NameParameterSetHelper(rs, items);
                }
            }
        }
        #endregion
    }

}