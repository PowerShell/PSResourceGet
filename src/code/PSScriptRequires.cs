// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Linq;
using System.Collections.ObjectModel;
using Microsoft.PowerShell.Commands;
using Newtonsoft.Json;

namespace Microsoft.PowerShell.PSResourceGet.UtilClasses
{
    /// <summary>
    /// This class contains information for a PSScriptFileInfo (representing a .ps1 file contents).
    /// </summary>
    public sealed class PSScriptRequires
    {
        #region Properties

        /// <summary>
        /// The modules this script requires, specified like: #requires -Module NetAdapter#requires -Module @{Name="NetAdapter"; Version="1.0.0.0"}
        /// Hashtable keys: GUID, MaxVersion, ModuleName (Required), RequiredVersion, Version.
        /// </summary>
        public ModuleSpecification[] RequiredModules { get; private set; } = Array.Empty<ModuleSpecification>();

        /// <summary>
        ///  Specifies if this script requires elevated privileges, specified like: #requires -RunAsAdministrator
        /// </summary>
        public bool IsElevationRequired { get; private set; }

        /// <summary>
        /// The application id this script requires, specified like: #requires -Shellid Shell
        /// </summary>
        public string RequiredApplicationId { get; private set; }

        /// <summary>
        /// The assemblies this script requires, specified like: #requires -Assembly path\to\foo.dll#requires -Assembly "System.Management.Automation, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" 
        /// </summary>
        public string[] RequiredAssemblies { get; private set; } = Utils.EmptyStrArray;

        /// <summary>
        /// The PowerShell Edition this script requires, specified like: #requires -PSEdition Desktop
        /// </summary>
        public string[] RequiredPSEditions { get; private set; } = Utils.EmptyStrArray;

        /// <summary>
        /// The PowerShell version this script requires, specified like: #requires -Version 3
        /// </summary>
        public Version RequiredPSVersion { get; private set; }

        #endregion

        #region Constructor

        /// <summary>
        /// This constructor creates a new PSScriptRequires instance with specified required modules.
        /// </summary>
        public PSScriptRequires(ModuleSpecification[] requiredModules)
        {
            RequiredModules = requiredModules ?? Array.Empty<ModuleSpecification>();
        }

        /// <summary>
        /// This constructor is called by internal cmdlet methods and creates a PSScriptHelp with default values
        /// for the parameters. Calling a method like PSScriptRequires.ParseConentIntoObj() would then populate those properties.
        /// </summary>
        internal PSScriptRequires() {}

        #endregion

        #region Internal Methods

        /// <summary>
        /// Parses RequiredModules out of comment lines and validates during parse process.
        /// </summary>
        internal bool ParseContentIntoObj(string[] commentLines, out ErrorRecord[] errors)
        {
            /**
            When Requires comment lines are obtained from .ps1 file they will have this format:

            #Requires -Module RequiredModule1
            #Requires -Module @{ ModuleName = 'RequiredModule2'; ModuleVersion = '2.0' }
            #Requires -Module @{ ModuleName = 'RequiredModule3'; RequiredVersion = '2.5' }
            #Requires -Module @{ ModuleName = 'RequiredModule4'; ModuleVersion = '1.1'; MaximumVersion = '2.0' }
            #Requires -Module @{ ModuleName = 'RequiredModule5'; MaximumVersion = '1.5' }

            */

            errors = Array.Empty<ErrorRecord>();
            List<ErrorRecord> errorsList = new List<ErrorRecord>();
            string requiresComment = String.Join(Environment.NewLine, commentLines);

            try
            {
                var ast = Parser.ParseInput(
                    requiresComment,
                    out Token[] tokens,
                    out ParseError[] parserErrors);

                if (parserErrors.Length > 0)
                {
                    foreach (ParseError err in parserErrors)
                    {
                        errorsList.Add(new ErrorRecord(
                            new InvalidOperationException($"Could not requires comments as valid PowerShell input due to {err.Message}."),
                            err.ErrorId,
                            ErrorCategory.ParserError,
                            null));
                    }

                    errors = errorsList.ToArray();
                    return false;
                }

                // get .REQUIREDMODULES property, by accessing the System.Management.Automation.Language.ScriptRequirements object ScriptRequirements.RequiredModules property
                ScriptRequirements parsedScriptRequirements = ast.ScriptRequirements;
                ReadOnlyCollection<ModuleSpecification> parsedModules = new List<ModuleSpecification>().AsReadOnly();

                if (parsedScriptRequirements != null)
                {
                    // System.Management.Automation.Language.ScriptRequirements properties are listed here:
                    // https://learn.microsoft.com/en-us/dotnet/api/system.management.automation.language.scriptrequirements?view=powershellsdk-7.4.0

                    if (parsedScriptRequirements.IsElevationRequired)
                    {
                        IsElevationRequired = true;
                    }

                    if (parsedScriptRequirements.RequiredApplicationId != null)
                    {
                        RequiredApplicationId = parsedScriptRequirements.RequiredApplicationId;
                    }

                    if (parsedScriptRequirements.RequiredAssemblies != null)
                    {
                        RequiredAssemblies = parsedScriptRequirements.RequiredAssemblies.ToArray();
                    }

                    if (parsedScriptRequirements.RequiredModules != null)
                    {
                        RequiredModules = parsedScriptRequirements.RequiredModules.ToArray();
                    }

                    if (parsedScriptRequirements.RequiredPSEditions != null)
                    {
                        RequiredPSEditions = parsedScriptRequirements.RequiredPSEditions.ToArray();
                    }

                    if (parsedScriptRequirements.RequiredPSVersion != null)
                    {
                        RequiredPSVersion = parsedScriptRequirements.RequiredPSVersion;
                    }
                }
            }
            catch (Exception e)
            {
                errorsList.Add(new ErrorRecord(
                    new ArgumentException($"Parsing RequiredModules failed due to {e.Message}"),
                    "requiredModulesAstParseThrewError",
                    ErrorCategory.ParserError,
                    null));
                errors = errorsList.ToArray();

                return false;
            }

            return true;
        }

        /// <summary>
        /// Emits string representation of '#Requires ...' comment(s).
        /// </summary>
        internal string[] EmitContent()
        {
            List<string> psRequiresLines = new List<string>();
            if (IsElevationRequired)
            {
                psRequiresLines.Add(String.Empty);
                psRequiresLines.Add("#Requires -RunAsAdministrator");
            }

            if (!String.IsNullOrEmpty(RequiredApplicationId))
            {
                psRequiresLines.Add(String.Empty);
                psRequiresLines.Add(String.Format("#Requires -ShellId {0}", RequiredApplicationId));
            }

            if (RequiredAssemblies.Length > 0)
            {
                psRequiresLines.Add(String.Empty);
                foreach (string assembly in RequiredAssemblies)
                {
                    psRequiresLines.Add(String.Format("#Requires -Assembly {0}", assembly));
                }
            }

            if (RequiredPSEditions.Length > 0)
            {
                psRequiresLines.Add(String.Empty);
                foreach (string psEdition in RequiredPSEditions)
                {
                    psRequiresLines.Add(String.Format("#Requires -PSEdition {0}", psEdition));
                }
            }

            if (RequiredPSVersion != null)
            {
                psRequiresLines.Add(String.Empty);
                psRequiresLines.Add(String.Format("#Requires -Version {0}", RequiredPSVersion.ToString()));
            }

            if (RequiredModules.Length > 0)
            {
                psRequiresLines.Add(String.Empty);
                foreach (ModuleSpecification moduleSpec in RequiredModules)
                {
                    psRequiresLines.Add(String.Format("#Requires -Module {0}", moduleSpec.ToString()));
                }

                psRequiresLines.Add(String.Empty);
            }

            return psRequiresLines.ToArray();
        }

        /// <summary>
        /// Updates the current Requires content with another (passed in), effectively replaces it.
        /// </summary>
        internal void UpdateContent(ModuleSpecification[] requiredModules)
        {
            if (requiredModules != null && requiredModules.Length != 0){
                RequiredModules = requiredModules;
            }
        }

        #endregion
    }
}
