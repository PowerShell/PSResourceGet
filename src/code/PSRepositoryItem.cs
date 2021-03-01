using System;
using System.Management.Automation;

namespace Microsoft.PowerShell.PowerShellGet.PSRepositoryItem
{
    /// <summary>
    /// This class contains information for a repository item.
    public class PSRespositoryItem
    {
        private string _name;
        private Uri _url;
        private bool _trusted= false;
        private int _priority = 50;


        public PSRespositoryItem(string name, Uri url)
        {
            _name = name;
            _url = url;
        }
        public PSRespositoryItem(string name, Uri url, int priority, bool trusted)
        {
            _name = name;
            _url = url;
            _priority = priority;
            _trusted = trusted;
        }

        /// <summary>
        /// the Name of the repository
        /// </summary>
        public string Name
        {
            get
            { return _name; }

            set
            { _name = value; }
        }

        /// <summary>
        /// the Url for the repository
        /// </summary>
        public Uri Url
        {
            get
            { return _url; }

            set
            { _url = value; }
        }

        /// <summary>
        /// whether the repository is trusted
        public bool Trusted
        {
            get
            { return _trusted; }

            set
            { _trusted = value; }
        }

        /// <summary>
        /// the priority of the repository
        /// </summary>
        [ValidateRange(0, 50)]
        public int Priority
        {
            get
            { return _priority; }

            set
            { _priority = value; }
        }
    }
}
