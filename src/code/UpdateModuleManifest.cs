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
        [Parameter (Position = 0, Mandatory = true, HelpMessage = "Path (including file name) to the module manifest (.psd1 file) to update.")]
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
        [Parameter]
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
        [SupportsWildcards]
        [Parameter]
        public string[] FunctionsToExport { get; set; }

        /// <summary>
        /// Specifies the aliases that the module exports.
        /// </summary>
        [SupportsWildcards]
        [Parameter]
        public string[] AliasesToExport { get; set; }

        /// <summary>
        /// Specifies the variables that the module exports.
        /// </summary>
        [SupportsWildcards]
        [Parameter]
        public string[] VariablesToExport { get; set; }

        /// <summary>
        /// Specifies the cmdlets that the module exports.
        /// </summary>
        [SupportsWildcards]
        [Parameter]
        public string[] CmdletsToExport { get; set; }

        /// <summary>
        /// Specifies the Desired State Configuration (DSC) resources that the module exports.
        /// </summary>
        [SupportsWildcards]
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
        /// Specifies a string that contains release notes or comments that you want available for this version of the script.

        /// </summary>
        [Parameter]
        public string ReleaseNotes { get; set; }

        /// <summary>
        /// Indicates the prerelease label of the module.
        /// </summary>
        [Parameter]
        public string Prerelease { get; set; }

        /// <summary>
        /// Specifies the internet address of the module's HelpInfo XML file.
        /// </summary>
        [Parameter]
        public Uri HelpInfoUri { get; set; }

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

        /// <summary>
        /// Specifies data that is passed to the module when it's imported.
        /// </summary>
        [Parameter]
        public Hashtable PrivateData { get; set; }
        #endregion

        #region Methods

        protected override void EndProcessing()
        {
             string resolvedManifestPath = SessionState.Path.GetResolvedPSPathFromPSPath(Path).First().Path;

            // Test the path of the module manifest to see if the file exists
            if (!File.Exists(resolvedManifestPath) || !resolvedManifestPath.EndsWith(".psd1"))
            {
                var message = $"The provided file path was not found: '{resolvedManifestPath}'.  Please specify a valid module manifest (.psd1) file path.";

                ThrowTerminatingError(
                    new ErrorRecord(
                         new ArgumentException(message),
                        "moduleManifestPathNotFound",
                         ErrorCategory.ObjectNotFound,
                         this));
            }

            // Parse the module manifest
            if(!Utils.TryReadManifestFile(
                        manifestFilePath: resolvedManifestPath,
                        manifestInfo: out Hashtable parsedMetadata,
                        error: out Exception manifestReadError))
            {
                ThrowTerminatingError(
                  new ErrorRecord(
                       exception: manifestReadError,
                       errorId: "ModuleManifestParseFailure",
                       errorCategory: ErrorCategory.ParserError,
                       targetObject: this));
            }

            // Prerelease, ReleaseNotes, Tags, ProjectUri, LicenseUri, IconUri, RequireLicenseAcceptance,
            // and ExternalModuleDependencies are all properties within a hashtable property called 'PSData'
            // which is within another hashtable property called 'PrivateData'
            // All of the properties mentioned above have their own parameter in 'New-ModuleManifest', so
            // we will parse out these values from the parsedMetadata and create entries for each one in individualy.
            // This way any values that were previously specified here will get transfered over to the new manifest.
            // Example of the contents of PSData:
            // PrivateData = @{
            //         PSData = @{
            //                  # Tags applied to this module. These help with module discovery in online galleries.
            //                  Tags = @('Tag1', 'Tag2')
            //
            //                  # A URL to the license for this module.
            //                  LicenseUri = 'https://www.licenseurl.com/'
            //
            //                  # A URL to the main website for this project.
            //                  ProjectUri = 'https://www.projecturi.com/'
            //
            //                  # A URL to an icon representing this module.
            //                  IconUri = 'https://iconuri.com/'
            //
            //                  # ReleaseNotes of this module.
            //                  ReleaseNotes = 'These are the release notes of this module.'
            //
            //                  # Prerelease string of this module.
            //                  Prerelease = 'preview'
            //
            //                  # Flag to indicate whether the module requires explicit user acceptance for install/update/save.
            //                  RequireLicenseAcceptance = $false
            //
            //                  # External dependent modules of this module
            //                  ExternalModuleDependencies = @('ModuleDep1, 'ModuleDep2')
            //
            //         } # End of PSData hashtable
            //
            // } # End of PrivateData hashtable
            var PrivateData = parsedMetadata["PrivateData"] as Hashtable;
            var PSData = PrivateData["PSData"] as Hashtable;

            if (PSData.ContainsKey("Prerelease"))
            {
                parsedMetadata["Prerelease"] = PSData["Prerelease"];
            }

            if (PSData.ContainsKey("ReleaseNotes"))
            {
                parsedMetadata["ReleaseNotes"] = PSData["ReleaseNotes"];
            }

            if (PSData.ContainsKey("Tags"))
            {
                parsedMetadata["Tags"] = PSData["Tags"];
            }

            if (PSData.ContainsKey("ProjectUri"))
            {
                parsedMetadata["ProjectUri"] = PSData["ProjectUri"];
            }

            if (PSData.ContainsKey("LicenseUri"))
            {
                parsedMetadata["LicenseUri"] = PSData["LicenseUri"];
            }

            if (PSData.ContainsKey("IconUri"))
            {
                parsedMetadata["IconUri"] = PSData["IconUri"];
            }

            if (PSData.ContainsKey("RequireLicenseAcceptance"))
            {
                parsedMetadata["RequireLicenseAcceptance"] = PSData["RequireLicenseAcceptance"];
            }

            if (PSData.ContainsKey("ExternalModuleDependencies"))
            {
                parsedMetadata["ExternalModuleDependencies"] = PSData["ExternalModuleDependencies"];
            }

            // Now we need to remove 'PSData' becaues if we leave this value in the hashtable,
            // New-ModuleManifest will keep this value and also attempt to create a new value for 'PSData'
            // and then complain that there's two keys within the PrivateData hashtable.
            PrivateData.Remove("PSData");

            // After getting the original module manifest contents, migrate all the fields to the new module manifest,

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
            }

            string tmpModuleManifestPath = System.IO.Path.Combine(tmpParentPath, System.IO.Path.GetFileName(resolvedManifestPath));
            parsedMetadata["Path"] = tmpModuleManifestPath;
            WriteVerbose($"Temp path created for new module manifest is: {tmpModuleManifestPath}");

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

            try
            {
                // Move to the new module manifest back to the original location
                WriteVerbose($"Moving '{tmpModuleManifestPath}' to '{resolvedManifestPath}'");
                Utils.MoveFiles(tmpModuleManifestPath, resolvedManifestPath, overwrite: true);
            }
            finally {
                // Clean up temp file if move fails
                if (File.Exists(tmpModuleManifestPath))
                {
                    File.Delete(tmpModuleManifestPath);
                }
            }
        }

        #endregion
    }
}
