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
    public sealed class PSScriptHelp
    {
        #region Properties

        /// <summary>
        /// The description of the script.
        /// </summary>
        public string Description { get; private set; }     

        /// <summary>
        /// The synopsis of the script.
        /// </summary>
        public string Synopsis { get; private set; }

        /// <summary>
        /// The example(s) relating to the script's usage.
        /// </summary>
        public string[] Example { get; private set; } = new string[]{};

        /// <summary>
        /// The inputs to the script.
        /// </summary>
        public string[] Inputs { get; private set; } = new string[]{};

        /// <summary>
        /// The outputs to the script.
        /// </summary>
        public string[] Outputs { get; private set; } = new string[]{};

        /// <summary>
        /// The notes for the script.
        /// </summary>
        public string[] Notes { get; private set; } = new string[]{};

        /// <summary>
        /// The links for the script.
        /// </summary>
        public string[] Links { get; private set; } = new string[]{};

        /// <summary>
        /// The components for the script.
        /// </summary>
        public string[] Component { get; private set; } = new string[]{};

        /// <summary>
        /// The roles for the script.
        /// </summary>
        public string[] Role { get; private set; } = new string[]{};

        /// <summary>
        /// The functionality components for the script.
        /// </summary>
        public string[] Functionality { get; private set; } = new string[]{};

        #endregion

        #region Constructor

        public PSScriptHelp (string description)
        {
            this.Description = description;
        }

        public PSScriptHelp (
            string description,
            string synopsis,
            string[] example,
            string[] inputs,
            string[] outputs,
            string[] notes,
            string[] links,
            string[] component,
            string[] role,
            string[] functionality)
        {
            this.Description = description;
            this.Synopsis = synopsis;
            this.Example = example;
            this.Inputs = inputs;
            this.Outputs = outputs;
            this.Notes = notes;
            this.Links = links;
            this.Component = component;
            this.Role = role;
            this.Functionality = functionality;
        }

        #endregion

        #region Internal Methods
        
        internal bool ParseContentIntoObj(string[] commentLines)
        {
            bool successfullyParsed = false;
            char[] spaceDelimeter = new char[]{' '};
            char[] newlineDelimeter = new char[]{'\n'};
            
            // parse content into a hashtable
            Hashtable parsedHelpMetadata = ParseContent(commentLines);
            
            // populate object
            Description = (string) parsedHelpMetadata["DESCRIPTION"] ?? String.Empty;
            
            Synopsis = (string) parsedHelpMetadata["SYNOPSIS"] ?? String.Empty;
            Example = Utils.GetStringArrayFromString(newlineDelimeter, (string) parsedHelpMetadata["EXAMPLE"]);
            Inputs = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedHelpMetadata["INPUT"]);
            Outputs = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedHelpMetadata["OUTPUTS"]);
            Notes = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedHelpMetadata["NOTES"]);
            Links = Utils.GetStringArrayFromString(newlineDelimeter, (string) parsedHelpMetadata["LINKS"]);
            Component = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedHelpMetadata["COMPONENT"]);
            Role = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedHelpMetadata["ROLE"]);
            Functionality = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedHelpMetadata["FUNCTIONALITY"]);

            // TODO: validate obj here?

            
            return successfullyParsed;
        }

        internal Hashtable ParseContent(string[] commentLines)
        {
            Hashtable parsedHelpMetadata = new Hashtable();
            string keyName = "";
            string value = "";

            for (int i = 1; i < commentLines.Count(); i++)
            {
                string line = commentLines[i];

                // scenario where line is: .KEY VALUE
                // this line contains a new metadata property.
                if (line.Trim().StartsWith("."))
                {
                    // check if keyName was previously populated, if so add this key value pair to the metadata hashtable
                    if (!String.IsNullOrEmpty(keyName))
                    {
                        parsedHelpMetadata.Add(keyName, value);
                    }

                    string[] parts = line.Trim().TrimStart('.').Split();
                    keyName = parts[0];
                    value = parts.Count() > 1 ? String.Join(" ", parts.Skip(1)) : String.Empty;
                }
                else if (!String.IsNullOrEmpty(line))
                {
                    // scenario where line contains text that is a continuation of value from previously recorded key
                    // this line does not starting with .KEY, and is also not an empty line.
                    if (value.Equals(String.Empty))
                    {
                        value += line;
                    }
                    else
                    {
                        value += Environment.NewLine + line;
                    }
                }
            }

            // this is the case where last key value had multi-line value.
            // and we've captured it, but still need to add it to hashtable.
            if (!String.IsNullOrEmpty(keyName) && !parsedHelpMetadata.ContainsKey(keyName))
            {
                // only add this key value if it hasn't already been added
                parsedHelpMetadata.Add(keyName, value);
            }

            return parsedHelpMetadata;
        }

        internal bool ValidateContent(out ErrorRecord error)
        {
            error = null;
            if (String.IsNullOrEmpty(Description))
            {
                var exMessage = "PSScript file must contain value for Description. Ensure value for Description is passed in and try again.";
                var ex = new ArgumentException(exMessage);
                var PSScriptInfoMissingDescriptionError = new ErrorRecord(ex, "PSScriptInfoMissingDescription", ErrorCategory.InvalidArgument, null);
                error = PSScriptInfoMissingDescriptionError;
                return false;
            }

            if (StringContainsComment(Description))
            {
                var exMessage = "PSScript file's value for Description cannot contain '<#' or '#>'. Pass in a valid value for Description and try again.";
                var ex = new ArgumentException(exMessage);
                var DescriptionContainsCommentError = new ErrorRecord(ex, "DescriptionContainsComment", ErrorCategory.InvalidArgument, null);
                error = DescriptionContainsCommentError;
                return false; 
            }

            return true;
        }

        internal string EmitContent()
        {
            string psHelpInfo;
            List<string> psHelpInfoLines = new List<string>();

            psHelpInfoLines.Add("<#\n");
            psHelpInfoLines.Add(String.Format(".DESCRIPTION\n{0}", Description));

            if (!String.IsNullOrEmpty(Synopsis))
            {
                psHelpInfoLines.Add(String.Format(".SYNOPSIS\n{0}", Synopsis));
            }

            foreach (string currentExample in Example)
            {
                psHelpInfoLines.Add(String.Format(".EXAMPLE\n{0}", currentExample));
            }

            foreach (string input in Inputs)
            {
                psHelpInfoLines.Add(String.Format(".INPUTS\n{0}", input));
            }

            foreach (string output in Outputs)
            {
                psHelpInfoLines.Add(String.Format(".OUTPUTS\n{0}", output));
            }

            if (Notes.Length > 0)
            {
                psHelpInfoLines.Add(String.Format(".NOTES\n{0}", String.Join("\n", Notes)));
            }

            foreach (string link in Links)
            {
                psHelpInfoLines.Add(String.Format(".LINK\n{0}", link));
            }

            if (Component.Length > 0)
            {
                psHelpInfoLines.Add(String.Format(".COMPONENT\n{0}", String.Join("\n", Component)));
            }
            
            if (Role.Length > 0)
            {
                psHelpInfoLines.Add(String.Format(".ROLE\n{0}", String.Join("\n", Role)));
            }
            
            if (Functionality.Length > 0)
            {
                psHelpInfoLines.Add(String.Format(".FUNCTIONALITY\n{0}", String.Join("\n", Functionality)));
            }

            psHelpInfo = String.Join("\n", psHelpInfoLines);
            psHelpInfo = psHelpInfo.TrimEnd('\n');
            psHelpInfo += "\n\n#>";

            return psHelpInfo;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Ensure description field (passed as stringToValidate) does not contain '<#' or '#>'
        /// </summary>
        private bool StringContainsComment(string stringToValidate)
        {
            return stringToValidate.Contains("<#") || stringToValidate.Contains("#>");
        }

        #endregion
    }
}