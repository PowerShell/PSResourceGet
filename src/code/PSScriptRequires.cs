// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.ObjectModel;
using Microsoft.PowerShell.Commands;
using NuGet.Versioning;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    /// <summary>
    /// This class contains information for a PSScriptFileInfo (representing a .ps1 file contents).
    /// </summary>
    public sealed class PSScriptRequires
    {
        #region Properties

        /// <summary>
        /// The list of modules required by the script.
        /// Hashtable keys: GUID, MaxVersion, ModuleName (Required), RequiredVersion, Version.
        /// </summary>
        public ModuleSpecification[] RequiredModules { get; private set; } = new ModuleSpecification[]{};

        #endregion

        #region Constructor

        public PSScriptRequires(ModuleSpecification[] requiredModules)
        {
            this.RequiredModules = requiredModules ?? new ModuleSpecification[]{};
        }

        #endregion

        #region Internal Methods

        internal bool ParseContent()
        {
            // use AST parser
            return false;
        }

        internal bool ValidateContent()
        {
            // use AST parser
            return false;
        }

        internal string EmitContent()
        {   
            if (RequiredModules.Length > 0)
            {
                List<string> psRequiresLines = new List<string>();
                psRequiresLines.Add("\n");
                foreach (ModuleSpecification moduleSpec in RequiredModules)
                {
                    psRequiresLines.Add(String.Format("#Requires -Module {0}", moduleSpec.ToString()));
                }

                psRequiresLines.Add("\n");
                return String.Join("\n", psRequiresLines);
            }

            return String.Empty;
        }

        #endregion
    }
}