// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;

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
        public string[] Example { get; private set; } = Utils.EmptyStrArray;

        /// <summary>
        /// The inputs to the script.
        /// </summary>
        public string[] Inputs { get; private set; } = Utils.EmptyStrArray;

        /// <summary>
        /// The outputs to the script.
        /// </summary>
        public string[] Outputs { get; private set; } = Utils.EmptyStrArray;

        /// <summary>
        /// The notes for the script.
        /// </summary>
        public string[] Notes { get; private set; } = Utils.EmptyStrArray;

        /// <summary>
        /// The links for the script.
        /// </summary>
        public string[] Links { get; private set; } = Utils.EmptyStrArray;

        /// <summary>
        /// The components for the script.
        /// </summary>
        public string[] Component { get; private set; } = Utils.EmptyStrArray;

        /// <summary>
        /// The roles for the script.
        /// </summary>
        public string[] Role { get; private set; } = Utils.EmptyStrArray;

        /// <summary>
        /// The functionality components for the script.
        /// </summary>
        public string[] Functionality { get; private set; } = Utils.EmptyStrArray;

        #endregion

        #region Constructor

        /// <summary>
        /// This constructor takes a value for description and creates a new PSScriptHelp instance.
        /// </summary>
        public PSScriptHelp (string description)
        {
            this.Description = description;
        }

        /// <summary>
        /// This constructor takes values for description as well as other properties and creates a new PSScriptHelp instance.
        /// Currently, the New-PSScriptFileInfo and Update-PSScriptFileInfo cmdlets don't support the user providing these values.
        /// </summary>
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

        /// <summary>
        /// This constructor is called by internal cmdlet methods and creates a PSScriptHelp with default values
        /// for the parameters. Calling a method like PSScriptHelp.ParseConentIntoObj() would then populate those properties.
        /// </summary>
        internal PSScriptHelp() {}

        #endregion

        #region Internal Methods
        
        /// <summary>
        /// Parses HelpInfo metadata out of the HelpInfo comment lines found while reading the file
        /// and populates PSScriptHelp properties from that metadata.
        /// </summary>
        internal bool ParseContentIntoObj(string[] commentLines, out ErrorRecord error)
        {
            bool successfullyParsed = true;
            string[] spaceDelimeter = new string[]{" "};
            string[] newlineDelimeter = new string[]{Environment.NewLine};
            
            // parse content into a hashtable
            Hashtable parsedHelpMetadata = Utils.ParseCommentBlockContent(commentLines);

            if (!ValidateParsedContent(parsedHelpMetadata, out error))
            {
                return false;
            }
            
            // populate object
            Description = (string) parsedHelpMetadata["DESCRIPTION"];
            Synopsis = (string) parsedHelpMetadata["SYNOPSIS"] ?? String.Empty;
            Example = Utils.GetStringArrayFromString(newlineDelimeter, (string) parsedHelpMetadata["EXAMPLE"]);
            Inputs = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedHelpMetadata["INPUT"]);
            Outputs = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedHelpMetadata["OUTPUTS"]);
            Notes = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedHelpMetadata["NOTES"]);
            Links = Utils.GetStringArrayFromString(newlineDelimeter, (string) parsedHelpMetadata["LINKS"]);
            Component = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedHelpMetadata["COMPONENT"]);
            Role = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedHelpMetadata["ROLE"]);
            Functionality = Utils.GetStringArrayFromString(spaceDelimeter, (string) parsedHelpMetadata["FUNCTIONALITY"]);
            
            return successfullyParsed;
        }

        /// <summary>
        /// Valides parsed help info content from the hashtable to ensure required help metadata (Description) is present
        /// and does not contain empty values.
        /// </summary>
        internal bool ValidateParsedContent(Hashtable parsedHelpMetadata, out ErrorRecord error)
        {
            error = null;
            if (!parsedHelpMetadata.ContainsKey("DESCRIPTION") || String.IsNullOrEmpty((string) parsedHelpMetadata["DESCRIPTION"]) || String.Equals(((string) parsedHelpMetadata["DESCRIPTION"]).Trim(), String.Empty))
            {
                var exMessage = "PSScript file must contain value for Description. Ensure value for Description is passed in and try again.";
                var ex = new ArgumentException(exMessage);
                var PSScriptInfoMissingDescriptionError = new ErrorRecord(ex, "PSScriptInfoMissingDescription", ErrorCategory.InvalidArgument, null);
                error = PSScriptInfoMissingDescriptionError;
                return false;
            }

            if (StringContainsComment((string) parsedHelpMetadata["DESCRIPTION"]))
            {
                var exMessage = "PSScript file's value for Description cannot contain '<#' or '#>'. Pass in a valid value for Description and try again.";
                var ex = new ArgumentException(exMessage);
                var DescriptionContainsCommentError = new ErrorRecord(ex, "DescriptionContainsComment", ErrorCategory.InvalidArgument, null);
                error = DescriptionContainsCommentError;
                return false; 
            }

            return true;
        }

        /// <summary>
        /// Validates help info properties contain required script Help properties
        /// i.e Description.
        /// </summary>
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

        /// <summary>
        /// Emits string representation of 'HelpInfo <# ... #>' comment and its metadata contents.
        /// </summary>
        internal string[] EmitContent()
        {
            // Note: we add a newline to the end of each property entry in HelpInfo so that there's an empty line separating them.
            List<string> psHelpInfoLines = new List<string>();

            psHelpInfoLines.Add($"<#{Environment.NewLine}");
            psHelpInfoLines.Add($".DESCRIPTION");
            psHelpInfoLines.Add($"{Description}{Environment.NewLine}");

            if (!String.IsNullOrEmpty(Synopsis))
            {
                psHelpInfoLines.Add($".SYNOPSIS");
                psHelpInfoLines.Add($"{Synopsis}{Environment.NewLine}");
            }

            foreach (string currentExample in Example)
            {
                psHelpInfoLines.Add($".EXAMPLE");
                psHelpInfoLines.Add($"{currentExample}{Environment.NewLine}");
            }

            foreach (string input in Inputs)
            {
                psHelpInfoLines.Add($".INPUTS");
                psHelpInfoLines.Add($"{input}{Environment.NewLine}");
            }

            foreach (string output in Outputs)
            {
                psHelpInfoLines.Add($".OUTPUTS");
                psHelpInfoLines.Add($"{output}{Environment.NewLine}");
            }

            if (Notes.Length > 0)
            {
                psHelpInfoLines.Add($".NOTES");
                psHelpInfoLines.Add($"{String.Join(Environment.NewLine, Notes)}{Environment.NewLine}");
            }

            foreach (string link in Links)
            {
                psHelpInfoLines.Add($".LINK");
                psHelpInfoLines.Add($"{link}{Environment.NewLine}");
            }

            if (Component.Length > 0)
            {
                psHelpInfoLines.Add($".COMPONENT");
                psHelpInfoLines.Add($"{String.Join(Environment.NewLine, Component)}{Environment.NewLine}");
            }
            
            if (Role.Length > 0)
            {
                psHelpInfoLines.Add($".ROLE");
                psHelpInfoLines.Add($"{String.Join(Environment.NewLine, Role)}{Environment.NewLine}");
            }
            
            if (Functionality.Length > 0)
            {
                psHelpInfoLines.Add($".FUNCTIONALITY");
                psHelpInfoLines.Add($"{String.Join(Environment.NewLine, Functionality)}{Environment.NewLine}");
            }

            psHelpInfoLines.Add("#>");

            return psHelpInfoLines.ToArray();
        }

        /// <summary>
        /// Updates contents of the HelpInfo properties from any (non-default) values passed in.
        /// </summary>
        internal bool UpdateContent(string description, out ErrorRecord error)
        {
            error = null;

            if (!String.IsNullOrEmpty(description))
            {
                if (String.Equals(description.Trim(), String.Empty))
                {
                    var exMessage = "Description value can't be updated to whitespace as this would invalidate the script.";
                    var ex = new ArgumentException(exMessage);
                    var descriptionUpdateValueIsWhitespaceError = new ErrorRecord(ex, "descriptionUpdateValueIsWhitespaceError", ErrorCategory.InvalidArgument, null);
                    error = descriptionUpdateValueIsWhitespaceError;
                    return false;
                }

                if (StringContainsComment(description))
                {
                    var exMessage = "Description value can't be updated to value containing comment '<#' or '#>' as this would invalidate the script.";
                    var ex = new ArgumentException(exMessage);
                    var descriptionUpdateValueContainsCommentError = new ErrorRecord(ex, "descriptionUpdateValueContainsCommentError", ErrorCategory.InvalidArgument, null);
                    error = descriptionUpdateValueContainsCommentError;
                    return false;
                }

                Description = description;
            }

            return true;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Ensure description field (passed as stringToValidate) does not contain '<#' or '#>'.
        /// </summary>
        private bool StringContainsComment(string stringToValidate)
        {
            return stringToValidate.Contains("<#") || stringToValidate.Contains("#>");
        }

        #endregion
    }
}
