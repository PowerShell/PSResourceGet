using Microsoft.VisualBasic.CompilerServices;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Management.Automation;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    /// <summary>
    /// This class contains information for a repository item.
    /// </summary>
    public sealed class PSScriptFileInfo
    {
        #region Properties

        /// <summary>
        /// the Version of the script
        /// </summary>
        public Version Version { get; }

        /// <summary>
        /// the GUID for the script
        /// </summary>
        public Guid Guid { get; }

        /// <summary>
        /// the author for the script
        /// </summary>
        public string Author { get; }

        /// <summary>
        /// the name of the company owning the script
        /// </summary>
        [ValidateRange(0, 50)]
        public string CompanyName { get; }

        /// <summary>
        /// the copyright information for the script
        /// </summary>
        public string Copyright { get; }

        /// <summary>
        /// the tags for the script
        /// </summary>
        public string[] Tags { get; }

        /// <summary>
        /// the Uri for the license of the script
        /// </summary>
        public Uri LicenseUri { get; }

        /// <summary>
        /// the Uri for the project relating to the script
        /// </summary>
        public Uri ProjectUri { get; }

        /// <summary>
        /// the Uri for the icon relating to the script
        /// </summary>
        public Uri IconUri { get; }

        /// <summary>
        /// the list of external module dependencies for the script
        /// </summary>
        public string[] ExternalModuleDependencies { get; }

        /// <summary>
        /// the list of required scripts for the parent script
        /// </summary>
        public string[] RequiredScripts { get; }

        /// <summary>
        /// the list of external script dependencies for the script
        /// </summary>
        public string[] ExternalScriptDependencies { get; }

        /// <summary>
        /// the release notes relating to the script
        /// </summary>
        public string[] ReleaseNotes { get; }

        /// <summary>
        /// The private data associated with the script
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public string PrivateData { get; set; }

        /// <summary>
        /// The description of the script
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string Description { get; set; }

        #endregion


        #region Constructor
        private PSScriptFileInfo() {}
        public PSScriptFileInfo(
            string version,
            Guid guid,
            string author,
            string companyName,
            string copyright,
            string[] tags,
            Uri licenseUri,
            Uri projectUri,
            Uri iconUri,
            string[] externalModuleDependencies,
            string[] requiredScripts,
            string[] externalScriptDependencies,
            string[] releaseNotes,
            string privateData,
            string description
        )
        {
            if (String.IsNullOrEmpty(author))
            {
                author = Environment.UserName;
            }
            Version = !String.IsNullOrEmpty(version) ? new Version (version) : new Version("1.0.0.0");
            Guid = (guid == null || guid == Guid.Empty) ? new Guid() : guid;
            Author = !String.IsNullOrEmpty(author) ? author : Environment.UserName;
            CompanyName = companyName;
            Copyright = copyright;
            Tags = tags ?? Utils.EmptyStrArray;
            LicenseUri = licenseUri;
            ProjectUri = projectUri;
            IconUri = iconUri;
            ExternalModuleDependencies = externalModuleDependencies ?? Utils.EmptyStrArray;
            RequiredScripts = requiredScripts ?? Utils.EmptyStrArray;
            ExternalScriptDependencies = externalScriptDependencies ?? Utils.EmptyStrArray;
            ReleaseNotes = releaseNotes ?? Utils.EmptyStrArray;
            PrivateData = privateData;
            Description = description;
        }
        /***

                    <#PSScriptInfo
                        .VERSION 1.0
                        .GUID 544238e3-1751-4065-9227-be105ff11636
                        .AUTHOR manikb
                        .COMPANYNAME Microsoft Corporation
                        .COPYRIGHT (c) 2015 Microsoft Corporation. All rights reserved.
                        .TAGS Tag1 Tag2 Tag3
                        .LICENSEURI https://contoso.com/License
                        .PROJECTURI https://contoso.com/
                        .ICONURI https://contoso.com/Icon
                        .EXTERNALMODULEDEPENDENCIES ExternalModule1
                        .REQUIREDSCRIPTS Start-WFContosoServer,Stop-ContosoServerScript
                        .EXTERNALSCRIPTDEPENDENCIES Stop-ContosoServerScript
                        .RELEASENOTES
                        contoso script now supports following features
                        Feature 1
                        Feature 2
                        Feature 3
                        Feature 4
                        Feature 5
                        #>
                        */

        #endregion

    }

    #region Public Static Methods

    public static bool TryParseScriptFileInfo(
        string scriptFileInfo,
        PSCmdlet cmdletPassedIn,
        out Hashtable parsedPSScriptInfoHashtable)
    {
        parsedPSScriptInfoHashtable = new Hashtable();
        bool successfullyParsed = false;

        if (scriptFileInfo.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            // Parse the script file
            var ast = Parser.ParseFile(
                scriptFileInfo,
                out Token[] tokens,
                out ParseError[] errors);

            if (errors.Length > 0 && !String.Equals(errors[0].ErrorId, "WorkflowNotSupportedInPowerShellCore", StringComparison.OrdinalIgnoreCase))
            {
                var message = String.Format("Could not parse '{0}' as a PowerShell script file.", scriptFileInfo);
                var ex = new ArgumentException(message);
                var psScriptFileParseError = new ErrorRecord(ex, "psScriptFileParseError", ErrorCategory.ParserError, null);
                cmdletPassedIn.WriteError(psScriptFileParseError);
                return successfullyParsed;
            }
            else if (ast != null)
            {
                // TODO: Anam do we still need to check if ast is not null?
                // Get the block/group comment beginning with <#PSScriptInfo
                List<Token> commentTokens = tokens.Where(a => String.Equals(a.Kind.ToString(), "Comment", StringComparison.OrdinalIgnoreCase)).ToList();
                string commentPattern = "<#PSScriptInfo";
                Regex rg = new Regex(commentPattern);
                List<Token> psScriptInfoCommentTokens = commentTokens.Where(a => rg.IsMatch(a.Extent.Text)).ToList();

                if (psScriptInfoCommentTokens.Count() == 0 || psScriptInfoCommentTokens[0] == null)
                {
                    // TODO: Anam change to error from V2
                    var message = String.Format("PSScriptInfo comment was missing or could not be parsed");
                    var ex = new ArgumentException(message);
                    var psCommentParseError = new ErrorRecord(ex, "psScriptInfoCommentParseError", ErrorCategory.ParserError, null);
                    cmdletPassedIn.WriteError(psCommentParseError);
                    return successfullyParsed;  
                }

                string[] commentLines = Regex.Split(psScriptInfoCommentTokens[0].Text, "[\r\n]");
                // TODO: Anam clean above up as we don't need to store empty lines for CF and newline, I think?
                string keyName = String.Empty;
                string value = String.Empty;

                /**
                PSScriptInfo comment will be in following format:
                <#PSScriptInfo
                    .VERSION 1.0
                    .GUID 544238e3-1751-4065-9227-be105ff11636
                    .AUTHOR manikb
                    .COMPANYNAME Microsoft Corporation
                    .COPYRIGHT (c) 2015 Microsoft Corporation. All rights reserved.
                    .TAGS Tag1 Tag2 Tag3
                    .LICENSEURI https://contoso.com/License
                    .PROJECTURI https://contoso.com/
                    .ICONURI https://contoso.com/Icon
                    .EXTERNALMODULEDEPENDENCIES ExternalModule1
                    .REQUIREDSCRIPTS Start-WFContosoServer,Stop-ContosoServerScript
                    .EXTERNALSCRIPTDEPENDENCIES Stop-ContosoServerScript
                    .RELEASENOTES
                    contoso script now supports following features
                    Feature 1
                    Feature 2
                    Feature 3
                    Feature 4
                    Feature 5
                    #>
                */

                /**
                If comment line count is not more than two, it doesn't have the any metadata property
                comment block would look like:

                <#PSScriptInfo
                #>
                */
                cmdletPassedIn.WriteVerbose("total comment lines: " + commentLines.Count());
                if (commentLines.Count() > 2)
                {
                    // TODO: Anam is it an error if the metadata property is empty?
                    for (int i = 1; i < commentLines.Count(); i++)
                    {
                        string line = commentLines[i];
                        cmdletPassedIn.WriteVerbose("i: " + i + "line: " + line);
                        if (String.IsNullOrEmpty(line))
                        {
                            continue;
                        }
                        // A line is starting with . conveys a new metadata property
                        // __NEWLINE__ is used for replacing the value lines while adding the value to $PSScriptInfo object
                        if (line.Trim().StartsWith("."))
                        {
                            // string partPattern = "[.\s+]";
                            string[] parts = line.Trim().TrimStart('.').Split();
                            keyName = parts[0];
                            value = parts.Count() > 1 ? String.Join(" ", parts.Skip(1)) : String.Empty;
                            parsedPSScriptInfoHashtable.Add(keyName, value);
                        }
                    }
                    successfullyParsed = true;
                }

                // get .DESCRIPTION comment
                CommentHelpInfo scriptCommentInfo = ast.GetHelpContent();
                if (scriptCommentInfo != null && !String.IsNullOrEmpty(scriptCommentInfo.Description))
                {
                    parsedPSScriptInfoHashtable.Add("DESCRIPTION", scriptCommentInfo.Description);
                }

                // get RequiredModules
                ScriptRequirements parsedScriptRequirements = ast.ScriptRequirements;
                if (parsedScriptRequirements != null && parsedScriptRequirements.RequiredModules != null)
                {
                    ReadOnlyCollection<Commands.ModuleSpecification> parsedRequiredModules = parsedScriptRequirements.RequiredModules;
                    parsedPSScriptInfoHashtable.Add("RequiredModules", parsedRequiredModules);
                }

                // get all defined functions and populate DefinedCommands, DefinedFunctions, DefinedWorkflow
                // TODO: Anam DefinedWorkflow is no longer supported as of PowerShellCore 6+, do we ignore error or reject script?
                // List<Ast> parsedFunctionAst = ast.FindAll(a => a.GetType().Name == "FunctionDefinitionAst", true).ToList();
                // foreach (var p in parsedFunctionAst)
                // {
                //     Console.WriteLine(p.Parent.Extent.Text);
                //     p.
                // }
                var parsedFunctionAst = ast.FindAll(a => a is FunctionDefinitionAst, true);
                List<FunctionDefinitionAst> allCommands = new List<FunctionDefinitionAst>();
                if (allCommands.Count() > 0)
                {
                    foreach (var function in parsedFunctionAst)
                    {
                        allCommands.Add((FunctionDefinitionAst) function);
                    }

                    List<string> allCommandNames = allCommands.Select(a => a.Name).Distinct().ToList();
                    parsedPSScriptInfoHashtable.Add("DefinedCommands", allCommandNames);

                    // b.Body.Extent.Text to get actual content. So "Function b.Name c.Body.Extent.Text" is that whole line lol
                    List<string> allFunctionNames = allCommands.Where(a => !a.IsWorkflow).Select(b => b.Name).Distinct().ToList();
                    parsedPSScriptInfoHashtable.Add("DefinedFunctions", allFunctionNames);

                    List<string> allWorkflowNames = allCommands.Where(a => a.IsWorkflow).Select(b => b.Name).Distinct().ToList();
                    parsedPSScriptInfoHashtable.Add("DefinedWorkflows", allWorkflowNames);
                }
            }
        }

        return successfullyParsed;
    }

    #endregion
}
