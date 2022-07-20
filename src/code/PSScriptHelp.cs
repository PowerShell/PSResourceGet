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
        /// The parameter(s) for the script.
        /// </summary>
        public string[] Parameter { get; private set; }

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
            Description = description;
        }

        /// <summary>
        /// This constructor takes values for description as well as other properties and creates a new PSScriptHelp instance.
        /// Currently, the New-PSScriptFileInfo and Update-PSScriptFileInfo cmdlets don't support the user providing these values.
        /// </summary>
        public PSScriptHelp (
            string description,
            string synopsis,
            string[] parameter,
            string[] example,
            string[] inputs,
            string[] outputs,
            string[] notes,
            string[] links,
            string[] component,
            string[] role,
            string[] functionality)
        {
            Description = description;
            Synopsis = synopsis;
            Parameter = parameter;
            Example = example;
            Inputs = inputs;
            Outputs = outputs;
            Notes = notes;
            Links = links;
            Component = component;
            Role = role;
            Functionality = functionality;
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
            bool successfullyParsed = true;
            string[] spaceDelimeter = new string[]{" "};
            string[] newlineDelimeter = new string[]{Environment.NewLine};
            
            // parse content into a hashtable
            Hashtable parsedHelpMetadata = ParseHelpContentHelper(commentLines, out errors);
            if (errors.Length != 0)
            {
                return false;
            }

            if (!ValidateParsedContent(parsedHelpMetadata, out ErrorRecord validationError))
            {
                errors = new ErrorRecord[]{validationError};
                return false;
            }
            
            // populate object
            Description = (string) parsedHelpMetadata["DESCRIPTION"];
            Synopsis = (string) parsedHelpMetadata["SYNOPSIS"] ?? String.Empty;

            List<string> parameterList = parsedHelpMetadata.ContainsKey("PARAMETER") ? (List<string>)parsedHelpMetadata["PARAMETER"]: new List<string>();
            Parameter = parameterList.ToArray();

            List<string> exampleList =  parsedHelpMetadata.ContainsKey("EXAMPLE") ? (List<string>)parsedHelpMetadata["EXAMPLE"] : new List<string>();
            Example = exampleList.ToArray();

            List<string> inputList = parsedHelpMetadata.ContainsKey("INPUT") ? (List<string>)parsedHelpMetadata["INPUT"] : new List<string>();
            Inputs = inputList.ToArray();

            List<string> outputList = parsedHelpMetadata.ContainsKey("OUTPUT") ? (List<string>)parsedHelpMetadata["OUTPUT"] : new List<string>();
            Outputs = outputList.ToArray();

            List<string> notesList = parsedHelpMetadata.ContainsKey("NOTES") ? (List<string>)parsedHelpMetadata["NOTES"] : new List<string>();
            Notes = notesList.ToArray();

            List<string> linksList = parsedHelpMetadata.ContainsKey("LINKS") ? (List<string>)parsedHelpMetadata["LINKS"] : new List<string>();
            Links = linksList.ToArray();

            List<string> componentList = parsedHelpMetadata.ContainsKey("COMPONENT") ? (List<string>)parsedHelpMetadata["COMPONENT"] : new List<string>();
            Component = componentList.ToArray();

            List<string> roleList = parsedHelpMetadata.ContainsKey("ROLE") ? (List<string>)parsedHelpMetadata["ROLE"] : new List<string>();
            Role = roleList.ToArray();

            List<string> functionalityList = parsedHelpMetadata.ContainsKey("FUNCTIONALITY") ? (List<string>)parsedHelpMetadata["FUNCTIONALITY"] : new List<string>();
            Functionality = functionalityList.ToArray();
            
            return successfullyParsed;
        }

        /// <summary>
        /// Parses metadata out of PSScriptCommentInfo comment block's lines (which are passed in) into a hashtable.
        /// This comment block cannot have duplicate keys.
        /// </summary>
        public static Hashtable ParseHelpContentHelper(string[] commentLines, out ErrorRecord[] errors)
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

            errors = Array.Empty<ErrorRecord>();
            List<ErrorRecord> errorsList = new List<ErrorRecord>();

            Hashtable parsedHelpMetadata = new Hashtable();
            char[] spaceDelimeter = new char[]{' '};
            string keyName = "";
            string value = "";

            bool keyNeedsToBeAdded = false;

            for (int i = 0; i < commentLines.Length; i++)
            {
                string line = commentLines[i];

                // scenario where line is: .KEY VALUE
                // this line contains a new metadata property.
                if (line.Trim().StartsWith("."))
                {
                    // check if keyName was previously populated, if so add this key value pair to the metadata hashtable
                    if (!String.IsNullOrEmpty(keyName))
                    {
                        keyNeedsToBeAdded = false; // we'l end up adding the key,value to hashtable in this code flow
                        if (parsedHelpMetadata.ContainsKey(keyName))
                        {
                            if (keyName.Equals("DESCRIPTION") || keyName.Equals("SYNOPSIS"))
                            {
                                var message = String.Format("PowerShell script 'HelpInfo' comment block metadata cannot contain duplicate keys (i.e .KEY) for Description or Synopsis");
                                var ex = new InvalidOperationException(message);
                                var psHelpInfoDuplicateKeyError = new ErrorRecord(ex, "psHelpInfoDuplicateKeyError", ErrorCategory.ParserError, null);
                                errorsList.Add(psHelpInfoDuplicateKeyError);
                                continue;
                            }

                            List<string> currentValues = (List<string>) parsedHelpMetadata[keyName];
                            currentValues.Add(value);
                            parsedHelpMetadata[keyName] = currentValues;
                        }
                        else
                        {
                            // adding key for first time
                            if (keyName.Equals("DESCRIPTION") || keyName.Equals("SYNOPSIS"))
                            {
                                parsedHelpMetadata.Add(keyName, value);
                            }
                            else
                            {
                                // the other keys will have values of type string[]
                                List<string> valueList = new List<string>();
                                valueList.Add(value);
                                parsedHelpMetadata.Add(keyName, valueList);
                            }
                        }
                    }

                    // setting count to 2 will get 1st separated string (key) into part[0] and the rest (value) into part[1] if any
                    string[] parts = line.Trim().TrimStart('.').Split(separator: spaceDelimeter, count: 2);
                    keyName = parts[0];
                    value = parts.Length == 2 ? parts[1] : String.Empty;
                    keyNeedsToBeAdded = true;
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

                    keyNeedsToBeAdded = true;
                }
            }

            // this is the case where last key value had multi-line value.
            // and we've captured it, but still need to add it to hashtable.
            if (!String.IsNullOrEmpty(keyName) && keyNeedsToBeAdded)
            {
                if (parsedHelpMetadata.ContainsKey(keyName))
                {
                    if (keyName.Equals("DESCRIPTION") || keyName.Equals("SYNOPSIS"))
                    {
                        var message = String.Format("PowerShell script 'HelpInfo' comment block metadata cannot contain duplicate keys (i.e .KEY) for Description or Synopsis");
                        var ex = new InvalidOperationException(message);
                        var psHelpInfoDuplicateKeyError = new ErrorRecord(ex, "psHelpInfoDuplicateKeyError", ErrorCategory.ParserError, null);
                        errorsList.Add(psHelpInfoDuplicateKeyError);
                    }
                    else
                    {
                        List<string> currentValues = (List<string>)parsedHelpMetadata[keyName];
                        currentValues.Add(value);
                        parsedHelpMetadata[keyName] = currentValues;
                    }
                }
                else
                {
                    // only add this key value if it hasn't already been added
                    if (keyName.Equals("DESCRIPTION") || keyName.Equals("SYNOPSIS"))
                    {
                        parsedHelpMetadata.Add(keyName, value);
                    }
                    else
                    {
                        List<string> valueList = new List<string>();
                        valueList.Add(value);
                        parsedHelpMetadata.Add(keyName, valueList);
                    }
                }
            }

            errors = errorsList.ToArray();

            return parsedHelpMetadata;
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
