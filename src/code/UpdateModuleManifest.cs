// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// Updates the module manifest (.psd1) for a resource.
    /// </summary>
    [Cmdlet(VerbsData.Update, "ModuleManifest")]
    public sealed class UpdateModuleManifest : PSCmdlet
    {
        #region Parameters

        /// <summary>
        /// Specifies the path and file name of the module manifest.
        /// </summary>
        [Parameter (Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string Path { get; set; }

        /// <summary>
        /// Specifies script modules (.psm1) and binary modules (.dll) that are imported into the module's session state.
        /// </summary>
        [Parameter]
        public object[] NestedModules { get; set; }

        /// <summary>
        /// Specifies a unique identifier for the module, can be used to distinguish among modules with the same name.
        /// </summary>
        [Parameter]
        public Guid Guid { get; set; }

        /// <summary>
        /// Specifies the module author.
        /// </summary>
        [Parameter]
        public string Author { get; set; }

        /// <summary>
        /// Specifies the company or vendor who created the module.
        /// </summary>
        [Parameter]
        public string CompanyName { get; set; }

        /// <summary>
        /// Specifies a copyright statement for the module.
        /// </summary>
        [Parameter]
        public string Copyright { get; set; }

        /// <summary>
        /// Specifies the primary or root file of the module.
        /// </summary>
        [Parameter]
        public string RootModule { get; set; }

        /// <summary>
        /// Specifies the version of the module.
        /// </summary>
        [Parameter]
        public Version ModuleVersion { get; set; }

        /// <summary>
        /// Specifies a description of the module.
        /// </summary>
        [Parameter]
        public string Description { get; set; }

        /// <summary>
        /// Specifies the processor architecture that the module requires.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true)]
        [ValidateNotNullOrEmpty]
        public ProcessorArchitecture ProcessorArchitecture { get; set; }

        /// <summary>
        /// Specifies the compatible PSEditions of the module.
        /// </summary>
        [Parameter]
        public string[] CompatiblePSEditions { get; set; }

        /// <summary>
        /// Specifies the minimum version of PowerShell that will work with this module.
        /// </summary>
        [Parameter]
        public Version PowerShellVersion { get; set; }

        /// <summary>
        /// Specifies the minimum version of the Common Language Runtime (CLR) of the Microsoft .NET Framework that the module requires.
        /// </summary>
        [Parameter]
        public Version ClrVersion { get; set; }

        /// <summary>
        /// Specifies the minimum version of the Microsoft .NET Framework that the module requires.
        /// </summary>
        [Parameter]
        public Version DotNetFrameworkVersion { get; set; }

        /// <summary>
        /// Specifies the name of the PowerShell host program that the module requires.
        /// </summary>
        [Parameter]
        public string PowerShellHostName { get; set; }

        /// <summary>
        /// Specifies the minimum version of the PowerShell host program that works with the module.
        /// </summary>
        [Parameter]
        public Version PowerShellHostVersion { get; set; }

        /// <summary>
        /// Specifies modules that must be in the global session state.
        /// </summary>
        [Parameter]
        public Object[] RequiredModules { get; set; }

        /// <summary>
        /// Specifies the type files (.ps1xml) that run when the module is imported.
        /// </summary>
        [Parameter]
        public string[] TypesToProcess { get; set; }

        /// <summary>
        /// Specifies the formatting files (.ps1xml) that run when the module is imported.
        /// </summary>
        [Parameter]
        public string[] FormatsToProcess { get; set; }

        /// <summary>
        /// Specifies script (.ps1) files that run in the caller's session state when the module is imported. 
        /// </summary>
        [Parameter]
        public string[] ScriptsToProcess { get; set; }

        /// <summary>
        /// Specifies the assembly (.dll) files that the module requires.
        /// </summary>
        [Parameter]
        public string[] RequiredAssemblies { get; set; }

        /// <summary>
        /// Specifies all items that are included in the module.
        /// </summary>
        [Parameter]
        public string[] FileList { get; set; }

        /// <summary>
        /// Specifies an array of modules that are included in the module.
        /// </summary>
        [Parameter]
        public Object[] ModuleList { get; set; }

        /// <summary>
        /// Specifies the functions that the module exports. 
        /// </summary>
        [Parameter]
        public string[] FunctionsToExport { get; set; }

        /// <summary>
        /// Specifies the aliases that the module exports.
        /// </summary>
        [Parameter]
        public string[] AliasesToExport { get; set; }

        /// <summary>
        /// Specifies the variables that the module exports.
        /// </summary>
        [Parameter]
        public string[] VariablesToExport { get; set; }

        /// <summary>
        /// Specifies the cmdlets that the module exports. 
        /// </summary>
        [Parameter]
        public string[] CmdletsToExport { get; set; }

        /// <summary>
        /// Specifies the Desired State Configuration (DSC) resources that the module exports. 
        /// </summary>
        [Parameter]
        public string[] DscResourcesToExport { get; set; }

        /// <summary>
        /// Specifies an array of tags.
        /// </summary>
        [Parameter]
        public string[] Tags { get; set; }

        /// <summary>
        /// Specifies the URL of a web page about this project.
        /// </summary>
        [Parameter]
        public Uri ProjectUri { get; set; }

        /// <summary>
        /// Specifies the URL of licensing terms for the module.
        /// </summary>
        [Parameter]
        public Uri LicenseUri { get; set; }

        /// <summary>
        /// Specifies the URL of an icon for the module. 
        /// </summary>
        [Parameter]
        public Uri IconUri { get; set; }

        /// <summary>
        /// Specifies a string array that contains release notes or comments that you want available for this version of the script.
        /// </summary>
        [Parameter]
        public string ReleaseNotes { get; set; }

        /// <summary>
        /// Indicates the module is prerelease.
        /// </summary>
        [Parameter]
        public string Prerelease { get; set; }

        /// <summary>
        /// Specifies the internet address of the module's HelpInfo XML file. 
        /// </summary>
        [Parameter]
        public Uri HelpInfoUri { get; set; }

        /// <summary>
        /// Returns an object representing the item with which you're working. 
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        /// <summary>
        /// Specifies the default command prefix.
        /// </summary>
        [Parameter]
        public string DefaultCommandPrefix { get; set; }

        /// <summary>
        /// Specifies an array of external module dependencies.
        /// </summary>
        [Parameter]
        public string[] ExternalModuleDependencies { get; set; }

        /// <summary>
        /// Specifies that a license acceptance is required for the module. 
        /// </summary>
        [Parameter]
        public SwitchParameter RequireLicenseAcceptance { get; set; }

        #endregion

        #region Members

        string ResolvedManifestPath;

        #endregion

        #region Methods

        protected override void BeginProcessing()
        {
            ResolvedManifestPath = SessionState.Path.GetResolvedPSPathFromPSPath(Path).First().Path;
            
            // Test the path of the module manifest to see if the file exists
            if (!File.Exists(ResolvedManifestPath))
            {
                var message = $"No file with a .psd1 extension was found in '{ResolvedManifestPath}'.  Please specify a path to a valid modulemanifest.";
                ThrowTerminatingError(
                    new ErrorRecord(
                         new ArgumentException(message),
                        "moduleManifestPathNotFound",
                         ErrorCategory.ObjectNotFound,
                         this));

                return;
            }
        }

        protected override void EndProcessing()
        {
            // Run Test-ModuleManifest, throw an error only if Test-ModuleManifest did not return the PSModuleInfo object
            if (!Utils.TestModuleManifestReturnsPSObject(ResolvedManifestPath, this))
            {
                ThrowTerminatingError(
                    new ErrorRecord(
                         new ArgumentException(String.Format("File '{0}' does not return a valid PSModuleInfo object. Please run 'Test-ModuleManifest' to validate the file.", ResolvedManifestPath)),
                        "invalidModuleManifest",
                         ErrorCategory.InvalidData,
                         this));
            }

            // Parse the module manifest
            if (!Utils.TryParsePSDataFile(ResolvedManifestPath, this, out Hashtable parsedMetadata))
            {
                ThrowTerminatingError(
                  new ErrorRecord(
                       new ArgumentException(String.Format("Unable to successfully parse file '{0}'.", ResolvedManifestPath)),
                      "moduleManifestParseFailure",
                       ErrorCategory.ParserError,
                       this));
            }

            // After getting the original module manifest contentes, migrate all the fields to the new module manifest, 
            // adding in any new values specified via cmdlet parameters.
            // Set up params to pass to New-ModuleManifest module
            // For now this will be parsedMetadata hashtable and we will just add to it as needed

            if (NestedModules != null)
            {
                parsedMetadata["NestedModules"] = NestedModules;
            }
            if (Guid != Guid.Empty)
            {
                parsedMetadata["Guid"] = Guid;
            }
            if (!string.IsNullOrWhiteSpace(Author))
            {
                parsedMetadata["Author"] = Author;
            }
            if (CompanyName != null)
            {
                parsedMetadata["CompanyName"] = CompanyName;
            }
            if (Copyright != null)
            {
                parsedMetadata["Copyright"] = Copyright;
            }
            if (RootModule != null)
            {
                parsedMetadata["RootModule"] = RootModule;
            }
            if (ModuleVersion != null)
            {
                parsedMetadata["ModuleVersion"] = ModuleVersion;
            }
            if (Description != null)
            {
                parsedMetadata["Description"] = Description;
            }
            if (ProcessorArchitecture != ProcessorArchitecture.None)
            {
                parsedMetadata["ProcessorArchitecture"] = ProcessorArchitecture;
            }
            if (PowerShellVersion != null)
            {
                parsedMetadata["PowerShellVersion"] = PowerShellVersion;
            }
            if (ClrVersion != null)
            {
                parsedMetadata["ClrVersion"] = ClrVersion;
            }
            if (DotNetFrameworkVersion != null)
            {
                parsedMetadata["DotNetFrameworkVersion"] = DotNetFrameworkVersion;
            }

            if (PowerShellHostName != null)
            {
                parsedMetadata["PowerShellHostName"] = PowerShellHostName;
            }
            if (PowerShellHostVersion != null)
            {
                parsedMetadata["PowerShellHostVersion"] = PowerShellHostVersion;
            }
            if (RequiredModules != null)
            {
                parsedMetadata["RequiredModules"] = RequiredModules;
            }
            if (TypesToProcess != null)
            {
                parsedMetadata["TypesToProcess"] = TypesToProcess;
            }
            if (FormatsToProcess != null)
            {
                parsedMetadata["FormatsToProcess"] = FormatsToProcess;
            }
            if (ScriptsToProcess != null)
            {
                parsedMetadata["ScriptsToProcess"] = ScriptsToProcess;
            }
            if (RequiredAssemblies != null)
            {
                parsedMetadata["RequiredAssemblies"] = RequiredAssemblies;
            }
            if (FileList != null)
            {
                parsedMetadata["FileList"] = FileList;
            }
            if (ModuleList != null)
            {
                parsedMetadata["ModuleList"] = ModuleList;
            }
            if (FunctionsToExport != null)
            {
                parsedMetadata["FunctionsToExport"] = FunctionsToExport;
            }
            if (AliasesToExport != null)
            {
                parsedMetadata["AliasesToExport"] = AliasesToExport;
            }
            if (VariablesToExport != null)
            {
                parsedMetadata["VariablesToExport"] = VariablesToExport;
            }
            if (CmdletsToExport != null)
            {
                parsedMetadata["CmdletsToExport"] = CmdletsToExport;
            }
            if (DscResourcesToExport != null)
            {
                parsedMetadata["DscResourcesToExport"] = DscResourcesToExport;
            }
            if (CompatiblePSEditions != null)
            {
                parsedMetadata["CompatiblePSEditions"] = CompatiblePSEditions;
            }
            if (HelpInfoUri != null)
            {
                parsedMetadata["HelpInfoUri"] = HelpInfoUri;
            }
            if (DefaultCommandPrefix != null)
            {
                parsedMetadata["DefaultCommandPrefix"] = DefaultCommandPrefix;
            }
            if (Tags != null)
            {
                parsedMetadata["Tags"] = Tags;
            }
            if (LicenseUri != null)
            {
                parsedMetadata["LicenseUri"] = LicenseUri;
            }
            if (ProjectUri != null)
            {
                parsedMetadata["ProjectUri"] = ProjectUri;
            }
            if (IconUri != null)
            {
                parsedMetadata["IconUri"] = IconUri;
            }
            if (ReleaseNotes != null)
            {
                parsedMetadata["ReleaseNotes"] = ReleaseNotes;
            }
            if (Prerelease != null)
            {
                parsedMetadata["Prerelease"] = Prerelease;
            }
            if (RequireLicenseAcceptance != null)
            {
                parsedMetadata["RequireLicenseAcceptance"] = RequireLicenseAcceptance;
            }
            if (ExternalModuleDependencies != null)
            {
                parsedMetadata["ExternalModuleDependencies"] = ExternalModuleDependencies;
            }
            
            // create a tmp path to create the module manifest
            string tmpParentPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            if (!Directory.Exists(tmpParentPath))
            {
                try
                {
                    Directory.CreateDirectory(tmpParentPath);
                }
                catch (Exception e)
                {
                    ThrowTerminatingError(
                            new ErrorRecord(
                                  new ArgumentException(e.Message),
                                            "ErrorCreatingTempDir",
                                            ErrorCategory.InvalidData,
                                            this));

                    return;
                }
            }
            string tmpModuleManifestPath = System.IO.Path.Combine(tmpParentPath, System.IO.Path.GetFileName(ResolvedManifestPath));
            
            WriteVerbose($"Temp path created for new module manifest is: {tmpModuleManifestPath}");
            parsedMetadata["Path"] = tmpModuleManifestPath;

            parsedMetadata.Remove("PrivateData");
            
            using (System.Management.Automation.PowerShell pwsh = System.Management.Automation.PowerShell.Create())
            {
                try
                {
                    var results = PowerShellInvoker.InvokeScriptWithHost<object>(
                          cmdlet: this,
                          script: @"
                                param (
                                    [hashtable] $params
                                )

                                New-ModuleManifest @params
                            ",
                          args: new object[] { parsedMetadata },
                          out Exception terminatingErrors);
                }
                catch (Exception e)
                {
                    ThrowTerminatingError(
                        new ErrorRecord(
                           new ArgumentException($"Error occured while running 'New-ModuleManifest': {e.Message}"),
                            "ErrorExecutingNewModuleManifest",
                            ErrorCategory.InvalidArgument,
                            this));
                }
            }

            // copy the contents of the parent directory into the temp path
            DirectoryInfo parentDirInfo = new DirectoryInfo(System.IO.Path.GetDirectoryName(ResolvedManifestPath));
            var filesInParentDir = parentDirInfo.GetFiles();
            foreach (var fileInfo in filesInParentDir)
            {

                var fileName = System.IO.Path.GetFileName(fileInfo.Name);
                if (string.Equals(fileName, System.IO.Path.GetFileName(parsedMetadata["Path"].ToString())))
                {
                    continue;
                }


                WriteVerbose($"FileName is: {fileName}");
                var parentTempDir = System.IO.Path.GetDirectoryName(parsedMetadata["Path"].ToString());
                WriteVerbose($"parentTempDir is: {parentTempDir}");
                var tempFile = System.IO.Path.Combine(parentTempDir, fileName);
                WriteVerbose($"Copying file from {fileInfo.FullName} to {tempFile}");

                File.Copy(fileInfo.FullName, tempFile, false);
            }

            // Test that new module manifest is valid
            if (!Utils.IsValidModuleManifest(parsedMetadata["Path"].ToString(), this))
            {
                ThrowTerminatingError(
                    new ErrorRecord(
                         new ArgumentException($"File '{ResolvedManifestPath}' does not return a valid PSModuleInfo object. Updating the module manifest has failed."),
                        "invalidModuleManifest",
                         ErrorCategory.InvalidData,
                         this));
            }

            // Move to the new module manifest back to the original location
            WriteVerbose($"Moving '{tmpModuleManifestPath}' to '{ResolvedManifestPath}'");
            Utils.MoveFiles(tmpModuleManifestPath, ResolvedManifestPath, overwrite:true);
        }

        #endregion
    }
}
