// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using static System.Environment;
using MoreLinq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// It retrieves a resource that was installEd with Install-PSResource
    /// Returns a single resource or multiple resource.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "PSResource", SupportsShouldProcess = true,
        HelpUri = "<add>", RemotingCapability = RemotingCapability.None)]
    public sealed
    class GetPSResource : PSCmdlet
    {
        /// <summary>
        /// Specifies the desired name for the resource to look for.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public string[] Name
        {
            get
            { return _name; }

            set
            { _name = value; }
        }
        private string[] _name;


        /// <summary>
        /// Specifies the version of the resource to include to look for. 
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty()]
        public string Version
        {
            get
            { return _version; }

            set
            { _version = value; }
        }
        private string _version;

        /// <summary>
        /// Specifies the path to look in. 
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty()]
        public string Path
        {
            get
            { return _path; }

            set
            { _path = value; }
        }
        private string _path;
        

        /*
        /// <summary>
        /// Specifies to include prerelease versions
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        public SwitchParameter Prerelease
        {
            get
            { return _prerelease; }

            set
            { _prerelease = value; }
        }
        private SwitchParameter _prerelease;
        */

        public static readonly string OsPlatform = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        private CancellationToken cancellationToken;
        private readonly PSCmdlet cmdletPassedIn;
        private string programFilesPath;
        private string myDocumentsPath;


        protected override void ProcessRecord()
        {
            // Define the cancellation token.
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken cancellationToken = source.Token;

            WriteDebug("Entering GetPSResource");

            // Flatten returned pkgs before displaying output returnedPkgsFound.Flatten().ToList()[0]
            GetHelper getHelper = new GetHelper(cancellationToken, this);
            List<PSObject> flattenedPkgs = getHelper.ProcessGetParams(_name, _version, prerelease:false, _path);

            foreach (PSObject psObject in flattenedPkgs)
            {
                // Temporary PSObject for output purposes
                PSObject temp = new PSObject();

                temp.Members.Add(new PSNoteProperty("Name", psObject.Properties["Name"].Value.ToString()));
                temp.Members.Add(new PSNoteProperty("Version", psObject.Properties["Version"].Value.ToString()));
                temp.Members.Add(new PSNoteProperty("Repository", psObject.Properties["Repository"].Value.ToString()));
                temp.Members.Add(new PSNoteProperty("Description", psObject.Properties["Description"].Value.ToString()));
                WriteObject(temp);
            }
        }
    }
}
