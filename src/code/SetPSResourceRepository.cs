// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Management.Automation;
using System.Globalization;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System.Collections.Generic;
using Dbg = System.Diagnostics.Debug;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// The Set-PSResourceRepository cmdlet is used to set information for a repository.
    /// </summary>
    [Cmdlet(VerbsCommon.Set,
        "PSResourceRepository",
        DefaultParameterSetName = "NameParameterSet",
        SupportsShouldProcess = true,
        HelpUri = "<add>")]
    public sealed
    class SetPSResourceRepository : PSCmdlet
    {
        #region Members

        private const string NameParameterSet = "NameParameterSet";
        private const string RepositoriesParameterSet = "RepositoriesParameterSet";
        private const int DefaultPriority = -1;
        #endregion

        #region Parameters

        /// <summary>
        /// Specifies the name of the repository to be set.
        /// </sumamry>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        /// <summary>
        /// Specifies the location of the repository to be set.
        /// </sumamry>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public Uri URL
        {
            get
            { return _url; }

            set
            {
                if (!Uri.TryCreate(value, string.Empty, out Uri url))
                {
                    var message = string.Format(CultureInfo.InvariantCulture, "The URL provided is not a valid url: {0}", value);
                    var ex = new ArgumentException(message);
                    var urlErrorRecord = new ErrorRecord(ex, "InvalidUrl", ErrorCategory.InvalidArgument, null);
                    ThrowTerminatingError(urlErrorRecord);
                }

                _url = url;
            }
        }
        private Uri _url = null;

        /// <summary>
        /// Specifies a user account that has rights to find a resource from a specific repository.
        /// </sumamry>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        public PSCredential Credential { get; set; } = null;

        /// <summary>
        /// Specifies a hashtable of repositories and is used to register multiple repositories at once.
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "RepositoriesParameterSet")]
        [ValidateNotNullOrEmpty]
        public Hashtable[] Repositories { get; set; }

        /// <summary>
        /// Specifies whether the repository should be trusted.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
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
        private bool isSet = false;

        /// <summary>
        /// Specifies the priority ranking of the repository, such that repositories with higher ranking priority are searched
        /// before a lower ranking priority one, when searching for a repository item across multiple registered repositories.
        /// Valid priority values range from 0 to 50, such that a lower numeric value (i.e 10) corresponds
        /// to a higher priority ranking than a higher numeric value (i.e 40). Has default value of 50.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        [ValidateRange(0, 50)]
        public int Priority { get; set; } = DefaultPriority;

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
            try
            {
                WriteDebug("Calling API to check repository store exists in non-corrupted state");
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
            if (Proxy != null || ProxyCredential != null)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new PSNotImplementedException("Proxy and ProxyCredential are not yet implemented. Please rerun cmdlet with other parameters."),
                    "ParametersNotImplementedYet",
                    ErrorCategory.NotImplemented,
                    this));
            }

            List<PSRepositoryInfo> items = new List<PSRepositoryInfo>();

            switch(ParameterSetName)
            {
                case NameParameterSet:
                    try
                    {
                        items.Add(UpdateRepositoryStoreHelper(Name, URL, Priority, Trusted));
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

        private PSRepositoryInfo UpdateRepositoryStoreHelper(string repoName, Uri repoUrl, int repoPriority, bool repoTrusted)
        {
            if (repoUrl != null && !(repoUrl.Scheme == Uri.UriSchemeHttp || repoUrl.Scheme == Uri.UriSchemeHttps || repoUrl.Scheme == Uri.UriSchemeFtp || repoUrl.Scheme == Uri.UriSchemeFile))
            {
                throw new ArgumentException("Invalid url, must be one of the following Uri schemes: HTTPS, HTTP, FTP, File Based");
            }

            // check repoName can't contain * or just be whitespace
            // remove trailing and leading whitespaces, and if Name is just whitespace Name should become null now and be caught by following condition
            repoName = repoName.Trim(' ');
            if (String.IsNullOrEmpty(repoName) || repoName.Contains("*"))
            {
                throw new ArgumentException("Name cannot be null/empty, contain asterisk or be just whitespace");
            }

            // check PSGallery URL is not trying to be set
            if (repoName.Equals("PSGallery", StringComparison.OrdinalIgnoreCase) && repoUrl != null)
            {
                throw new ArgumentException("The PSGallery repository has a pre-defined URL.  Setting the -URL parmeter for this repository is not allowed, instead try running 'Register-PSResourceRepository -PSGallery'.");
            }

            // determine trusted value to pass in (true/false if set, null otherwise, hence the nullable bool variable)
            bool? _trustedNullable = isSet ? new bool?(repoTrusted) : new bool?();

            // determine if either 1 of 3 values are attempting to be set: URL, Priority, Trusted.
            // if none are (i.e only Name parameter was provided, write error)
            if(repoUrl == null && repoPriority == DefaultPriority && _trustedNullable == null)
            {
                throw new ArgumentException("Either URL, Priority or Trusted parameters must be requested to be set");
            }

            WriteDebug("All required values to set repository provided, calling internal Update() API now");
            if (!ShouldProcess(repoName, "Set repository's value(s) in repository store"))
            {
                return null;
            }
            return RepositorySettings.Update(repoName, repoUrl, repoPriority, _trustedNullable);
        }

        private List<PSRepositoryInfo> RepositoriesParameterSetHelper()
        {
            List<PSRepositoryInfo> reposUpdatedFromHashtable = new List<PSRepositoryInfo>();
            foreach (Hashtable repo in Repositories)
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
            Uri repoURL = null;
            if (repo.ContainsKey("Url")  && !Uri.TryCreate(repo["URL"].ToString(), UriKind.Absolute, out repoURL))
            {
                WriteError(new ErrorRecord(
                    new PSInvalidOperationException("Invalid Url, unable to parse and create Uri"),
                    "InvalidUrl",
                    ErrorCategory.InvalidArgument,
                    this));
                return null;
            }

            bool repoTrusted = false;
            isSet = false;
            if(repo.ContainsKey("Trusted"))
            {
                repoTrusted = (bool) repo["Trusted"];
                isSet = true;
            }
            try
            {
                return UpdateRepositoryStoreHelper(repo["Name"].ToString(),
                    repoURL,
                    repo.ContainsKey("Priority") ? Convert.ToInt32(repo["Priority"].ToString()) : DefaultPriority,
                    repoTrusted);
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