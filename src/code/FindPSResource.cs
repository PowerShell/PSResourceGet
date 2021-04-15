// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Management.Automation;
using System.Collections.Generic;
using NuGet.Configuration;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System.Threading;
using LinqKit;
using MoreLinq.Extensions;
using NuGet.Versioning;
using System.Data;
using System.Linq;
using System.Net;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System.Net.Http;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static System.Environment;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// todo:
    /// </summary>

    [Cmdlet(VerbsCommon.Find,
        "PSResource",
        DefaultParameterSetName = "ResourceNameParameterSet",
        SupportsShouldProcess = true,
        HelpUri = "<add>")]
    public sealed
    class FindPSResource : PSCmdlet
    {
        # region Enums
        public enum ResourceType {
            Module,
            Script,
            DscResource,
            Command,
            RoleCapability
        };
        #endregion

        #region Members
        private const string ResourceNameParameterSet = "ResourceNameParameterSet";
        private const string CommandNameParameterSet = "CommandNameParameterSet";
        private const string DscResourceNameParameterSet = "DscResourceNameParameterSet";

        #endregion

        #region Parameters

        /// <summary>
        /// todo
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = ResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Name { get; set; }

        // todo: Type param here
        /// <summary>
        /// todo
        /// </summary>
        [Parameter(ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = ResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public ResourceType Type { get; set; }


        /// <summary>
        /// todo
        /// </summary>
        [Parameter(ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ParameterSetName = CommandNameParameterSet)]
        [Parameter(ParameterSetName = DscResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; }

        /// <summary>
        /// todo
        /// </summary>
        [Parameter(ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ParameterSetName = CommandNameParameterSet)]
        [Parameter(ParameterSetName = DscResourceNameParameterSet)]
        public SwitchParameter Prerelease { get; set; }

        /// <summary>
        /// todo
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = CommandNameParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = DscResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string ModuleName { get; set; }

        /// <summary>
        /// todo
        /// </summary>
        [Parameter(ValueFromPipeline = true, ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ValueFromPipeline = true, ParameterSetName = CommandNameParameterSet)]
        [Parameter(ValueFromPipeline = true, ParameterSetName = DscResourceNameParameterSet)]
        [ValidateNotNull]
        public string[] Tags { get; set; }

        /// <summary>
        /// todo
        /// </summary>
        [Parameter(ValueFromPipeline = true, ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ValueFromPipeline = true, ParameterSetName = CommandNameParameterSet)]
        [Parameter(ValueFromPipeline = true, ParameterSetName = DscResourceNameParameterSet)]
        [ValidateNotNullOrEmpty]
        public string[] Repository { get; set; }

        /// <summary>
        /// todo
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = CommandNameParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = DscResourceNameParameterSet)]
        public PSCredential Credential { get; set; }

        /// <summary>
        /// todo
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = ResourceNameParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = CommandNameParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = DscResourceNameParameterSet)]
        public SwitchParameter IncludeDependencies { get; set; }

        #endregion

        #region Methods
        protected override void ProcessRecord()
        {
            Console.WriteLine(Type);
        }
        #endregion
    }
}