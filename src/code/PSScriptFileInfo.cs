using System.Reflection;
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.ObjectModel;
using Microsoft.PowerShell.Commands;

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

        // /// <summary>
        // /// The list of modules required by the script
        // /// TODO: in V2 this had type Object[]
        // /// </summary>
        // [Parameter]
        // [ValidateNotNullOrEmpty()]
        // public string[] RequiredModules { get; set; }

        /// <summary>
        /// The list of modules required by the script
        /// Hashtable keys: GUID, MaxVersion, Name (Required), RequiredVersion, Version
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty()]
        public ModuleSpecification[] RequiredModules { get; set; }

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

        // TODO: seem to be optional keys for help comment, where would they be passed in from? constructor/New-X cmdlet?
        /// <summary>
        /// The synopsis of the script
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string Synopsis { get; set; }

        /// <summary>
        /// The example(s) relating to the script's usage
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string[] Example { get; set; }

        /// <summary>
        /// The inputs to the script
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string[] Inputs { get; set; }

        /// <summary>
        /// The outputs to the script
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string[] Outputs { get; set; }

        /// <summary>
        /// The notes for the script
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string[] Notes { get; set; }

        /// <summary>
        /// The links for the script
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string[] Links { get; set; }

        /// <summary>
        /// TODO: what is this?
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string[] Component { get; set; }

        /// <summary>
        /// TODO: what is this?
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string[] Role { get; set; }

        /// <summary>
        /// TODO: what is this?
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty()]
        public string[] Functionality { get; set; }

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
            // ModuleSpecification[] requiredModules, // TODO: in V2 this was Object[]
            Hashtable[] requiredModules,
            string[] externalModuleDependencies,
            string[] requiredScripts,
            string[] externalScriptDependencies,
            string[] releaseNotes,
            string privateData,
            string description,
            PSCmdlet cmdletPassedIn
        )
        {
            if (String.IsNullOrEmpty(author))
            {
                author = Environment.UserName;
            }

            List<ModuleSpecification> validatedModuleSpecs = new List<ModuleSpecification>();
            if (requiredModules.Length > 0)
            {
                CreateModuleSpecification(requiredModules, out validatedModuleSpecs, out ErrorRecord[] errors);
                foreach (ErrorRecord err in errors)
                {
                    cmdletPassedIn.WriteError(err);
                } 
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
            RequiredModules = validatedModuleSpecs.ToArray(); // TODO: ANAM need a default value?
            ExternalModuleDependencies = externalModuleDependencies ?? Utils.EmptyStrArray;
            RequiredScripts = requiredScripts ?? Utils.EmptyStrArray;
            ExternalScriptDependencies = externalScriptDependencies ?? Utils.EmptyStrArray;
            ReleaseNotes = releaseNotes ?? Utils.EmptyStrArray;
            PrivateData = privateData;
            Description = description;
        }

        #endregion

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

        #region Public Methods
        public bool TryCreateScriptFileInfoString(
            out string PSScriptFileString
        )
        {
            PSScriptFileString = String.Empty;
            bool fileContentsSuccessfullyCreated = false;
            if (!GetPSScriptInfoString(out string psScriptInfoCommentString))
            {
                return fileContentsSuccessfullyCreated;
            }

            PSScriptFileString = psScriptInfoCommentString;

            GetRequiresString(out string psRequiresString);
            if (!String.IsNullOrEmpty(psRequiresString))
            {
                PSScriptFileString += "\n";
                PSScriptFileString += psRequiresString;
            }

            return fileContentsSuccessfullyCreated;
        }

        public bool GetPSScriptInfoString(
            out string pSScriptInfoString
        )
        {
            bool pSScriptInfoSuccessfullyCreated = false;
            pSScriptInfoString = String.Empty;
            if (String.IsNullOrEmpty(Author) || String.IsNullOrEmpty(Description) || Version == null)
            {
                // write/throw error?
                return pSScriptInfoSuccessfullyCreated;
            }

            List<string> psScriptInfoLines = new List<string>();
            psScriptInfoLines.Add("<#PSScriptInfo");
            psScriptInfoLines.Add(String.Format(".VERSION {0}", Version.ToString()));
            psScriptInfoLines.Add(String.Format(".GUID {0}", Guid.ToString()));
            psScriptInfoLines.Add(String.Format(".AUTHOR {0}", Author));
            psScriptInfoLines.Add(String.Format(".COMPANYNAME {0}", CompanyName));
            psScriptInfoLines.Add(String.Format(".COPYRIGHT {0}", Copyright));
            psScriptInfoLines.Add(String.Format(".TAGS {0}", String.Join(" ", Tags)));
            psScriptInfoLines.Add(String.Format(".LICENSEURI {0}", LicenseUri == null ? String.Empty : LicenseUri.ToString()));
            psScriptInfoLines.Add(String.Format(".PROJECTURI {0}", ProjectUri == null ? String.Empty : ProjectUri.ToString()));
            psScriptInfoLines.Add(String.Format(".ICONURI {0}", IconUri == null ? String.Empty : IconUri.ToString()));
            psScriptInfoLines.Add(String.Format(".EXTERNALMODULEDEPENDENCIES {0}", String.Join(" ", ExternalModuleDependencies)));
            psScriptInfoLines.Add(String.Format(".REQUIREDSCRIPTS {0}", String.Join(" ", RequiredScripts)));
            psScriptInfoLines.Add(String.Format(".EXTERNALSCRIPTDEPENDENCIES {0}", String.Join(" ", ExternalScriptDependencies)));
            psScriptInfoLines.Add(String.Format(".RELEASENOTES\n{0}", String.Join("\n", ReleaseNotes)));
            psScriptInfoLines.Add("#>");

            pSScriptInfoString = String.Join("\n\n", psScriptInfoLines);
            pSScriptInfoSuccessfullyCreated = true;
            return pSScriptInfoSuccessfullyCreated;
        }

        public void GetRequiresString(
            out string psRequiresString
        )
        {
            psRequiresString = String.Empty;

            if (RequiredModules.Length > 0)
            {
                List<string> psRequiresLines = new List<string>();
                psRequiresLines.Add("<#\n");
                foreach (ModuleSpecification moduleSpec in RequiredModules)
                {
                    psRequiresLines.Add(String.Format("Requires -Module {0}", moduleSpec.ToString()));
                }
                psRequiresLines.Add("#>");
                psRequiresString = String.Join("\n", psRequiresLines);
                // TODO: where does the GUID come in?
            }
        }

        public bool GetScriptCommentHelpInfo(
            out string psHelpInfo
        )
        {
            psHelpInfo = String.Empty;
            bool psHelpInfoSuccessfullyCreated = false;
            List<string> psHelpInfoLines = new List<string>();
            psHelpInfoLines.Add("<#\n");
            psHelpInfoLines.Add(String.Format(".DESCRIPTION {0}", Description));
            if (!String.IsNullOrEmpty(Synopsis))
            {
                psHelpInfoLines.Add(String.Format(".SYNOPSIS {0}", Synopsis));
            }

            foreach (string currentExample in Example)
            {
                psHelpInfoLines.Add(String.Format(".EXAMPLE {0}", currentExample));
            }

            foreach (string input in Inputs)
            {
                psHelpInfoLines.Add(String.Format(".INPUTS {0}", input));
            }

            foreach (string output in Outputs)
            {
                psHelpInfoLines.Add(String.Format(".OUTPUTS {0}", output));
            }

            if (Notes.Length > 0)
            {
                psHelpInfoLines.Add(String.Format(".NOTES\n{0}", String.Join("\n", Notes)));
            }

            foreach (string link in Links)
            {
                psHelpInfoLines.Add(String.Format(".LINK {0}", link));
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

            psHelpInfoLines.Add("#>");
            return psHelpInfoSuccessfullyCreated;
        }

        public static void CreateModuleSpecification(
            Hashtable[] moduleSpecHashtables,
            out List<ModuleSpecification> validatedModuleSpecs,
            out ErrorRecord[] errors
        )
        {
            // bool successfullyCreatedModuleSpecs = false;
            List<ErrorRecord> errorList = new List<ErrorRecord>();
            List<ModuleSpecification> moduleSpecsList = new List<ModuleSpecification>();

            foreach(Hashtable moduleSpec in moduleSpecHashtables)
            {
                if (!moduleSpec.ContainsKey("ModuleName") || String.IsNullOrEmpty((string) moduleSpec["ModuleName"]))
                {
                    var exMessage = "RequiredModules Hashtable entry is missing a key 'ModuleName' and associated value, which is required for each module specification entry";
                    var ex = new ArgumentException(exMessage);
                    var NameMissingModuleSpecError = new ErrorRecord(ex, "NameMissingInModuleSpecification", ErrorCategory.InvalidArgument, null);
                    errorList.Add(NameMissingModuleSpecError);
                    continue;
                }

                // at this point it must contain ModuleName key.
                string moduleSpecName = (string) moduleSpec["ModuleName"];
                ModuleSpecification currentModuleSpec = null;
                if (moduleSpec.Keys.Count == 1 || (!moduleSpec.ContainsKey("MaximumVersion") && !moduleSpec.ContainsKey("ModuleVersion") && !moduleSpec.ContainsKey("RequiredVersion") && !moduleSpec.ContainsKey("Guid")))
                {
                    // pass to ModuleSpecification(string) constructor
                    currentModuleSpec = new ModuleSpecification(moduleSpecName);
                    if (currentModuleSpec != null)
                    {
                        moduleSpecsList.Add(currentModuleSpec);
                    }
                    else
                    {
                        var exMessage = String.Format("ModuleSpecification object was not able to be created for {0}", moduleSpecName);
                        var ex = new ArgumentException(exMessage);
                        var ModuleSpecNotCreatedError = new ErrorRecord(ex, "ModuleSpecificationNotCreated", ErrorCategory.InvalidArgument, null);
                        errorList.Add(ModuleSpecNotCreatedError);
                    }
                }
                else
                {
                    // TODO: ANAM perhaps not else
                    string moduleSpecMaxVersion = moduleSpec.ContainsKey("MaximumVersion") ? (string) moduleSpec["MaxiumumVersion"] : String.Empty;
                    string moduleSpecModuleVersion = moduleSpec.ContainsKey("ModuleVersion") ? (string) moduleSpec["ModuleVersion"] : String.Empty;
                    string moduleSpecRequiredVersion = moduleSpec.ContainsKey("ModuleVersion") ? (string) moduleSpec["RequiredVersion"] : String.Empty;
                    Guid moduleSpecGuid = moduleSpec.ContainsKey("Guid") ? (Guid) moduleSpec["Guid"] : Guid.Empty; // TODO: ANAM this can be the default

                    Hashtable moduleSpecHash = new Hashtable();

                    moduleSpecHash.Add("ModuleName", moduleSpecName);
                    if (moduleSpecGuid != Guid.Empty)
                    {
                        moduleSpecHash.Add("Guid", moduleSpecGuid);
                    }

                    if (!String.IsNullOrEmpty(moduleSpecMaxVersion))
                    {
                        moduleSpecHash.Add("MaximumVersion", moduleSpecMaxVersion);
                    }

                    if (!String.IsNullOrEmpty(moduleSpecModuleVersion))
                    {
                        moduleSpecHash.Add("ModuleVersion", moduleSpecModuleVersion);
                    }

                    if (!String.IsNullOrEmpty(moduleSpecRequiredVersion))
                    {
                        moduleSpecHash.Add("RequiredVersion", moduleSpecRequiredVersion);
                    }

                    currentModuleSpec = new ModuleSpecification(moduleSpecHash);
                    if (currentModuleSpec != null)
                    {
                        moduleSpecsList.Add(currentModuleSpec);
                    }
                    else
                    {
                        var exMessage = String.Format("ModuleSpecification object was not able to be created for {0}", moduleSpecName);
                        var ex = new ArgumentException(exMessage);
                        var ModuleSpecNotCreatedError = new ErrorRecord(ex, "ModuleSpecificationNotCreated", ErrorCategory.InvalidArgument, null);
                        errorList.Add(ModuleSpecNotCreatedError);
                    }
                }
            }

            errors = errorList.ToArray();
            validatedModuleSpecs = moduleSpecsList;
            // return successfullyCreatedModuleSpecs;
        }

        #endregion

    }
}