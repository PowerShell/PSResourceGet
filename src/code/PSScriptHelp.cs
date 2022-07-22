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
        public string Description { get; private set; } = String.Empty;

        /// <summary>
        /// This contains all help content aside from Description
        /// </summary>
        public List<string> HelpContent { get; private set; } = new List<string>();

        #endregion

        #region Constructor

        /// <summary>
        /// This constructor takes a value for description and creates a new PSScriptHelp instance.
        /// </summary>
        public PSScriptHelp (string description)
        {
            Description = description;
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
        internal bool ParseContentIntoObj(string[] commentLines, out ErrorRecord[] errors)
        {
            string[] spaceDelimeter = new string[]{" "};
            string[] newlineDelimeter = new string[]{Environment.NewLine};
            
            // parse content into a hashtable
            Hashtable parsedHelpMetadata = ParseHelpContentHelper(commentLines);

            if (!ValidateParsedContent(parsedHelpMetadata, out ErrorRecord validationError))
            {
                errors = new ErrorRecord[]{validationError};
                return false;
            }
            
            // populate object
            List<string> descriptionValue = (List<string>) parsedHelpMetadata["DESCRIPTION"];
            Description = String.Join(Environment.NewLine, descriptionValue);
            if (parsedHelpMetadata.ContainsKey("HELPCONTENT"))
            {
                HelpContent = (List<string>) parsedHelpMetadata["HELPCONTENT"];
            }

            errors = Array.Empty<ErrorRecord>();
            return true;
        }

        /// <summary>
        /// Parses metadata out of PSScriptCommentInfo comment block's lines (which are passed in) into a hashtable.
        /// This comment block cannot have duplicate keys.
        /// </summary>
        public static Hashtable ParseHelpContentHelper(string[] commentLines)
        {
            /**
            Comment lines can look like this:

            .KEY1 value

            .KEY2 value

            .KEY2 value2

            .KEY3
            value

            .KEY4 value
            value continued

            */

            // loop through lines
            // if line is .DESCRIPTION say we found descfiption
            // otherwise if line just starts with '.' set descriptionFound to false
            // else if line is not empty,
            //     if descriptionFound --> add lines to descriptionValue
            //     else                --> add lines to helpContentValue
            // descriptionFound -> rename to onDescripton

            List<string> helpContent = new List<string>();
            List<string> descriptionValue = new List<string>();
            bool parsingDescription = false;

            for(int i = 0; i < commentLines.Length; i++)
            {
                string line = commentLines[i];
                if (line.Trim().StartsWith(".DESCRIPTION"))
                {
                    parsingDescription = true;
                }
                else if (line.Trim().StartsWith("."))
                {
                    parsingDescription = false;
                    helpContent.Add(line);
                }
                else if (!String.IsNullOrEmpty(line))
                {
                    if (parsingDescription)
                    {
                        descriptionValue.Add(line);
                    }
                    else
                    {
                        helpContent.Add(line);
                    }
                }
            }

            Hashtable parsedHelpMetadata = new Hashtable();
            parsedHelpMetadata.Add("DESCRIPTION", descriptionValue);
            if (helpContent.Count != 0)
            {
                parsedHelpMetadata.Add("HELPCONTENT", helpContent);
            }

            return parsedHelpMetadata;
        }
        
        /// <summary>
        /// Valides parsed help info content from the hashtable to ensure required help metadata (Description) is present
        /// and does not contain empty values.
        /// </summary>
        internal bool ValidateParsedContent(Hashtable parsedHelpMetadata, out ErrorRecord error)
        {
            error = null;
            if (!parsedHelpMetadata.ContainsKey("DESCRIPTION"))
            {
                var exMessage = "PSScript file must contain value for Description. Ensure value for Description is passed in and try again.";
                var ex = new ArgumentException(exMessage);
                var PSScriptInfoMissingDescriptionError = new ErrorRecord(ex, "PSScriptInfoMissingDescription", ErrorCategory.InvalidArgument, null);
                error = PSScriptInfoMissingDescriptionError;
                return false;
            }

            List<string> descriptionValue = (List<string>) parsedHelpMetadata["DESCRIPTION"];
            string descriptionString = String.Join("", descriptionValue);
            if (descriptionValue.Count == 0 || (String.IsNullOrEmpty(descriptionString)) || String.IsNullOrWhiteSpace(descriptionString))
            {
                var exMessage = "PSScript file value for Description cannot be null, empty or whitespace. Ensure value for Description meets these conditions and try again.";
                var ex = new ArgumentException(exMessage);
                var PSScriptInfoMissingDescriptionError = new ErrorRecord(ex, "PSScriptInfoMissingDescription", ErrorCategory.InvalidArgument, null);
                error = PSScriptInfoMissingDescriptionError;
                return false;
            }

            if (StringContainsComment(descriptionString))
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

            if (HelpContent.Count != 0)
            {
                psHelpInfoLines.AddRange(HelpContent);
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
