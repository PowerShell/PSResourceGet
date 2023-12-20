// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Linq;
using System.Collections.ObjectModel;
using Microsoft.PowerShell.Commands;

namespace Microsoft.PowerShell.PSResourceGet.UtilClasses
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
        public ModuleSpecification[] RequiredModules { get; private set; } = Array.Empty<ModuleSpecification>();

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

                if (parsedScriptRequirements != null && parsedScriptRequirements.RequiredModules != null)
                {
                    RequiredModules = parsedScriptRequirements.RequiredModules.ToArray();
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
