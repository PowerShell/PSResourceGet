using System;
using System.Management.Automation;

namespace Microsoft.PowerShell.PowerShellGet.PSRepositoryItem
{
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
            // _trusted = false; //todo: use default/initializer perhaps
            // _priority = 50; //todo: use default/initializer perhaps
        }
        public PSRespositoryItem(string name, Uri url, int priority, bool trusted)
        {
            _name = name;
            _url = url;
            _priority = priority;
            _trusted = trusted;
        }

        public string Name
        {
            get
            { return _name; }

            set
            { _name = value; }
        }

        public Uri Url
        {
            get
            { return _url; }

            set
            { _url = value; }
        }

        public bool Trusted
        {
            get
            { return _trusted; }

            set
            { _trusted = value; }
        }

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
