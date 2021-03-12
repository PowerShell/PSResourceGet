// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Threading;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// It retrieves a resource that was installEd with Install-PSResource
    /// Returns a single resource or multiple resource.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "InstalledPSResource", HelpUri = "<add>", RemotingCapability = RemotingCapability.None)]
    public sealed
    class GetInstalledPSResource : PSCmdlet
    {
        /// <summary>
        /// Specifies the desired name for the resource to look for.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        public string[] Name { get; set; }

        /// <summary>
        /// Specifies the version of the resource to include to look for. 
        /// </summary>
        [Parameter()]
        [ValidateNotNullOrEmpty()]
        public string Version { get; set; }

        /// <summary>
        /// Specifies the path to look in. 
        /// </summary>
        [Parameter()]
        [ValidateNotNullOrEmpty()]
        public string Path { get; set; }
        
        protected override void ProcessRecord()
        {
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken cancellationToken = source.Token;

            WriteDebug("Entering GetInstalledPSResource");

            GetHelper getHelper = new GetHelper(cancellationToken, this);
            foreach (PSResourceInfo psObject in getHelper.ProcessGetParams(Name, Version, Path))
            {
                WriteObject(psObject);
            }
        }
    }
}