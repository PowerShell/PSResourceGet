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

        internal PSScriptRequires() {}

        #endregion

        #region Internal Methods

        /// <summary>
        /// Parses RequiredModules out of comment lines and validates during
        /// </summary>
        internal bool ParseContent(string[] commentLines, out ErrorRecord[] errors)
        {
            /**
            When Requires comment lines are obtained from .ps1 file they will have this format:

            #Requires -Module RequiredModule1
            #Requires -Module @{ ModuleName = 'RequiredModule2'; ModuleVersion = '2.0' }
            #Requires -Module @{ ModuleName = 'RequiredModule3'; RequiredVersion = '2.5' }
            #Requires -Module @{ ModuleName = 'RequiredModule4'; ModuleVersion = '1.1'; MaximumVersion = '2.0' }
            #Requires -Module @{ ModuleName = 'RequiredModule5'; MaximumVersion = '1.5' }

            */

            errors = new ErrorRecord[]{};
            List<ErrorRecord> errorsList = new List<ErrorRecord>();
            string requiresComment = String.Join("\n", commentLines);

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
                        var message = String.Format("Could not requires comments as valid PowerShell input due to {1}.", err.Message);
                        var ex = new InvalidOperationException(message);
                        var requiresCommentParseError = new ErrorRecord(ex, err.ErrorId, ErrorCategory.ParserError, null);
                        errorsList.Add(requiresCommentParseError);
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
                var message = $"Parsing RequiredModules failed due to {e.Message}";
                var ex = new ArgumentException(message);
                var requiredModulesAstParseError = new ErrorRecord(ex, "requiredModulesAstParseThrewError", ErrorCategory.ParserError, null);
                errorsList.Add(requiredModulesAstParseError);
                errors = errorsList.ToArray();
                return false;
            }

            return true;
        }

        // internal bool ValidateContent()
        // {
        //     // use AST parser
        //     return false;
        // }

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