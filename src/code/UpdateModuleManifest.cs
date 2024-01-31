// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PSResourceGet.UtilClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

namespace Microsoft.PowerShell.PSResourceGet.Cmdlets
{
    /// <summary>
    /// Updates the module manifest (.psd1) for a resource.
    /// </summary>
    [Cmdlet(VerbsData.Update, "PSModuleManifest")]
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
        [Alias("Tag")]
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
        [ValidateNotNullOrEmpty]
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
            if (MyInvocation.BoundParameters.ContainsKey(nameof(Prerelease)))
            {
                // get rid of any whitespace on prerelease label string.
                Prerelease = Prerelease.Trim();
                if (string.IsNullOrWhiteSpace(Prerelease))
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new ArgumentException("Prerelease value cannot be empty or whitespace. Please re-run cmdlet with valid value."),
                        "PrereleaseValueCannotBeWhiteSpace",
                        ErrorCategory.InvalidArgument,
                        this));
                }
            }

            string resolvedManifestPath = GetResolvedProviderPathFromPSPath(Path, out ProviderInfo provider).First();

            // Test the path of the module manifest to see if the file exists
            if (!File.Exists(resolvedManifestPath) || !resolvedManifestPath.EndsWith(".psd1"))
            {
                ThrowTerminatingError(new ErrorRecord(
                    new ArgumentException($"The provided file path was not found: '{resolvedManifestPath}'. Please specify a valid module manifest (.psd1) file path."),
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
                ThrowTerminatingError(new ErrorRecord(
                    manifestReadError,
                    "ModuleManifestParseFailure",
                    ErrorCategory.ParserError,
                    this));
            }

            // Due to a PowerShell New-ModuleManifest bug with the PrivateData entry when it's a nested hashtable (https://github.com/PowerShell/PowerShell/issues/5922)
            // we have to handle PrivateData entry, and thus module manifest creation, differently on PSCore than on WindowsPowerShell.
            ErrorRecord errorRecord = null;
            bool successfulManifestCreation = Utils.GetIsWindowsPowerShell(this) ? TryCreateModuleManifestForWinPSHelper(parsedMetadata, resolvedManifestPath, out errorRecord) : TryCreateModuleManifestHelper(parsedMetadata, resolvedManifestPath, out errorRecord);
            if (errorRecord != null)
            {
                WriteError(errorRecord);
            }
        }

        /// <summary>
        /// Handles module manifest creation for non-WindowsPowerShell platforms.
        /// </summary>
        private bool TryCreateModuleManifestHelper(Hashtable parsedMetadata, string resolvedManifestPath, out ErrorRecord errorRecord)
        {
            errorRecord = null;
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

            Hashtable privateData = new Hashtable();

            if (PrivateData != null && PrivateData.Count != 0)
            {
                privateData = PrivateData;
            }
            else
            {
                privateData = parsedMetadata["PrivateData"] as Hashtable;
            }

            var psData = privateData["PSData"] as Hashtable;

            if (psData.ContainsKey("Prerelease"))
            {
                parsedMetadata["Prerelease"] = psData["Prerelease"];
            }

            if (psData.ContainsKey("ReleaseNotes"))
            {
                parsedMetadata["ReleaseNotes"] = psData["ReleaseNotes"];
            }

            if (psData.ContainsKey("Tags"))
            {
                parsedMetadata["Tags"] = psData["Tags"];
            }

            if (psData.ContainsKey("ProjectUri"))
            {
                parsedMetadata["ProjectUri"] = psData["ProjectUri"];
            }

            if (psData.ContainsKey("LicenseUri"))
            {
                parsedMetadata["LicenseUri"] = psData["LicenseUri"];
            }

            if (psData.ContainsKey("IconUri"))
            {
                parsedMetadata["IconUri"] = psData["IconUri"];
            }

            if (psData.ContainsKey("RequireLicenseAcceptance"))
            {
                parsedMetadata["RequireLicenseAcceptance"] = psData["RequireLicenseAcceptance"];
            }

            if (psData.ContainsKey("ExternalModuleDependencies"))
            {
                parsedMetadata["ExternalModuleDependencies"] = psData["ExternalModuleDependencies"];
            }

            // Now we need to remove 'PSData' becaues if we leave this value in the hashtable,
            // New-ModuleManifest will keep this value and also attempt to create a new value for 'PSData'
            // and then complain that there's two keys within the PrivateData hashtable.
            // This is due to the issue of New-ModuleManifest when the PrivateData entry is a nested hashtable (https://github.com/PowerShell/PowerShell/issues/5922).
            privateData.Remove("PSData");

            // After getting the original module manifest contents, migrate all the fields to the parsedMetadata hashtable which will be provided as params for New-ModuleManifest.
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

            if (RequireLicenseAcceptance != null && RequireLicenseAcceptance.IsPresent)
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
                Utils.DeleteDirectory(tmpParentPath);

                errorRecord = new ErrorRecord(
                    new ArgumentException(e.Message),
                    "ErrorCreatingTempDir",
                    ErrorCategory.InvalidData,
                    this);

                return false;
            }

            string tmpModuleManifestPath = System.IO.Path.Combine(tmpParentPath, System.IO.Path.GetFileName(resolvedManifestPath));
            parsedMetadata["Path"] = tmpModuleManifestPath;
            WriteVerbose($"Temp path created for new module manifest is: {tmpModuleManifestPath}");

            using (System.Management.Automation.PowerShell pwsh = System.Management.Automation.PowerShell.Create())
            {
                try
                {
                    var results = pwsh.AddCommand("Microsoft.PowerShell.Core\\New-ModuleManifest").AddParameters(parsedMetadata).Invoke<Object>();
                    if (pwsh.HadErrors || pwsh.Streams.Error.Count > 0)
                    {
                        foreach (var err in pwsh.Streams.Error)
                        {
                            WriteError(err);
                        }
                    }
                }
                catch (Exception e)
                {
                    errorRecord = new ErrorRecord(
                        new ArgumentException($"Error occured while running 'New-ModuleManifest': {e.Message}"),
                        "ErrorExecutingNewModuleManifest",
                        ErrorCategory.InvalidArgument,
                        this);

                    return false;
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

                Utils.DeleteDirectory(tmpParentPath);
            }

            return true;
        }

        /// <summary>
        /// Handles module manifest creation for Windows PowerShell platform.
        /// Since the code calls New-ModuleManifest and the Windows PowerShell version of the cmdlet did not have Prerelease, ExternalModuleDependencies and RequireLicenseAcceptance parameters,
        /// we can't simply call New-ModuleManifest with all parameters. Instead, create the manifest without PrivateData parameter (and the keys usually inside it) and then update the lines for PrivateData later.
        /// </summary>
        private bool TryCreateModuleManifestForWinPSHelper(Hashtable parsedMetadata, string resolvedManifestPath, out ErrorRecord errorRecord)
        {
            // Note on priority of values:
            // If -PrivateData parameter was provided with the cmdlet & .psd1 file PrivateData already had values, the passed in -PrivateData values replace those previosuly there.
            // any direct parameters supplied by the user (i.e ProjectUri) [takes priority over but in mix-and-match fashion] over -> -PrivateData parameter [takes priority over but in replacement fashion] over -> original .psd1 file's PrivateData values (complete replacement)
            errorRecord = null;
            string[] tags = Utils.EmptyStrArray;
            Uri licenseUri = null;
            Uri iconUri = null;
            Uri projectUri = null;
            string prerelease = String.Empty;
            string releaseNotes = String.Empty;
            bool? requireLicenseAcceptance = null;
            string[] externalModuleDependencies = Utils.EmptyStrArray;

            Hashtable privateData = new Hashtable();
            if (PrivateData != null && PrivateData.Count != 0)
            {
                privateData = PrivateData;
            }
            else
            {
                privateData = parsedMetadata["PrivateData"] as Hashtable;
            }

            var psData = privateData["PSData"] as Hashtable;

            if (psData.ContainsKey("Prerelease"))
            {
                prerelease = psData["Prerelease"] as string;
            }

            if (psData.ContainsKey("ReleaseNotes"))
            {
                releaseNotes = psData["ReleaseNotes"] as string;
            }

            if (psData.ContainsKey("Tags"))
            {
                tags = psData["Tags"] as string[];
            }

            if (psData.ContainsKey("ProjectUri") && psData["ProjectUri"] is string projectUriString)
            {
                if (!Uri.TryCreate(projectUriString, UriKind.Absolute, out projectUri))
                {
                    projectUri = null;
                }
            }

            if (psData.ContainsKey("LicenseUri") && psData["LicenseUri"] is string licenseUriString)
            {
                if (!Uri.TryCreate(licenseUriString, UriKind.Absolute, out licenseUri))
                {
                    licenseUri = null;
                }
            }

            if (psData.ContainsKey("IconUri") && psData["IconUri"] is string iconUriString)
            {
                if (!Uri.TryCreate(iconUriString, UriKind.Absolute, out iconUri))
                {
                    iconUri = null;
                }
            }

            if (psData.ContainsKey("RequireLicenseAcceptance"))
            {
                requireLicenseAcceptance = psData["RequireLicenseAcceptance"] as bool?;
            }

            if (psData.ContainsKey("ExternalModuleDependencies"))
            {
                externalModuleDependencies = psData["ExternalModuleDependencies"] as string[];
            }

            // the rest of the parameters can be directly provided to New-ModuleManifest, so add it parsedMetadata hashtable used for cmdlet parameters.
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

            // if values were passed in for these parameters, they will be prioritized over values retrieved from PrivateData
            // we need to populate the local variables with their values to use for PrivateData entry creation later.
            // and parameters that can be passed to New-ModuleManifest are added to the parsedMetadata hashtable.
            if (Tags != null)
            {
                tags = Tags;
                parsedMetadata["Tags"] = tags;
            }

            if (LicenseUri != null)
            {
                licenseUri = LicenseUri;
                parsedMetadata["LicenseUri"] = licenseUri;
            }

            if (ProjectUri != null)
            {
                projectUri = ProjectUri;
                parsedMetadata["ProjectUri"] = projectUri;
            }

            if (IconUri != null)
            {
                iconUri = IconUri;
                parsedMetadata["IconUri"] = iconUri;
            }

            if (ReleaseNotes != null)
            {
                releaseNotes = ReleaseNotes;
                parsedMetadata["ReleaseNotes"] = releaseNotes;
            }

            // New-ModuleManifest on WinPS doesn't support parameters: Prerelease, RequireLicenseAcceptance, and ExternalModuleDependencies so we don't add those to parsedMetadata hashtable.
            if (Prerelease != null)
            {
                prerelease = Prerelease;
            }

            if (RequireLicenseAcceptance != null && RequireLicenseAcceptance.IsPresent)
            {
                requireLicenseAcceptance = RequireLicenseAcceptance;
            }

            if (ExternalModuleDependencies != null)
            {
                externalModuleDependencies = ExternalModuleDependencies;
            }

            // create a tmp path to create the module manifest
            string tmpParentPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                Directory.CreateDirectory(tmpParentPath);
            }
            catch (Exception e)
            {
                Utils.DeleteDirectory(tmpParentPath);

                errorRecord = new ErrorRecord(
                    new ArgumentException(e.Message),
                    "ErrorCreatingTempDir",
                    ErrorCategory.InvalidData,
                    this);
                
                return false;
            }

            string tmpModuleManifestPath = System.IO.Path.Combine(tmpParentPath, System.IO.Path.GetFileName(resolvedManifestPath));
            parsedMetadata["Path"] = tmpModuleManifestPath;
            WriteVerbose($"Temp path created for new module manifest is: {tmpModuleManifestPath}");

            using (System.Management.Automation.PowerShell pwsh = System.Management.Automation.PowerShell.Create())
            {
                try
                {
                    var results = pwsh.AddCommand("Microsoft.PowerShell.Core\\New-ModuleManifest").AddParameters(parsedMetadata).Invoke<Object>();
                    if (pwsh.HadErrors || pwsh.Streams.Error.Count > 0)
                    {
                        foreach (var err in pwsh.Streams.Error)
                        {
                            WriteError(err);
                        }
                    }
                }
                catch (Exception e)
                {
                    Utils.DeleteDirectory(tmpParentPath);

                    errorRecord = new ErrorRecord(
                        new ArgumentException($"Error occured while running 'New-ModuleManifest': {e.Message}"),
                        "ErrorExecutingNewModuleManifest",
                        ErrorCategory.InvalidArgument,
                        this);

                    return false;
                }
            }

            string privateDataString = GetPrivateDataString(tags, licenseUri, projectUri, iconUri, releaseNotes, prerelease, requireLicenseAcceptance, externalModuleDependencies);

            // create new file in tmp path for updated module manifest (i.e updated with PrivateData entry)  
            string newTmpModuleManifestPath = System.IO.Path.Combine(tmpParentPath, "Updated" + System.IO.Path.GetFileName(resolvedManifestPath));
            if (!TryCreateNewPsd1WithUpdatedPrivateData(privateDataString, tmpModuleManifestPath, newTmpModuleManifestPath, out errorRecord))
            {
                return false;
            }

            try
            {
                // Move to the new module manifest back to the original location
                WriteVerbose($"Moving '{newTmpModuleManifestPath}' to '{resolvedManifestPath}'");
                Utils.MoveFiles(newTmpModuleManifestPath, resolvedManifestPath, overwrite: true);
            }
            finally {
                // Clean up temp file if move fails
                if (File.Exists(tmpModuleManifestPath))
                {
                    File.Delete(tmpModuleManifestPath);
                }

                if (File.Exists(newTmpModuleManifestPath))
                {
                    File.Delete(newTmpModuleManifestPath);
                }

                Utils.DeleteDirectory(tmpParentPath);
            }

            return  true;
        }

        /// <summary>
        /// Returns string representing PrivateData entry for .psd1 file. This used for WinPS .psd1 creation as these values could not be populated otherwise.
        /// </summary>
        private string GetPrivateDataString(string[] tags, Uri licenseUri, Uri projectUri, Uri iconUri, string releaseNotes, string prerelease, bool? requireLicenseAcceptance, string[] externalModuleDependencies)
        {
            /**
            Example PrivateData
            
            PrivateData = @{
                PSData = @{
                    # Tags applied to this module. These help with module discovery in online galleries.
                    Tags = @('Tag1', 'Tag2')

                    # A URL to the license for this module.
                    LicenseUri = 'https://www.licenseurl.com/'

                    # A URL to the main website for this project.
                    ProjectUri = 'https://www.projecturi.com/'

                    # A URL to an icon representing this module.
                    IconUri = 'https://iconuri.com/'

                    # ReleaseNotes of this module.
                    ReleaseNotes = 'These are the release notes of this module.'

                    # Prerelease string of this module.
                    Prerelease = 'preview'

                    # Flag to indicate whether the module requires explicit user acceptance for install/update/save.
                    RequireLicenseAcceptance = $false

                    # External dependent modules of this module
                    ExternalModuleDependencies = @('ModuleDep1, 'ModuleDep2')
            
                } # End of PSData hashtable
            
            } # End of PrivateData hashtable
            */

            string tagsString = string.Join(", ", tags.Select(item => "'" + item + "'"));
            string tagLine = tags.Length != 0 ? $"Tags = @({tagsString})"  : "# Tags = @()";
            
            string licenseUriLine = licenseUri == null ? "# LicenseUri = ''" : $"LicenseUri = '{licenseUri.ToString()}'";
            string projectUriLine = projectUri == null ? "# ProjectUri = ''" : $"ProjectUri = '{projectUri.ToString()}'";
            string iconUriLine = iconUri == null ? "# IconUri = ''" : $"IconUri = '{iconUri.ToString()}'";

            string releaseNotesLine = String.IsNullOrEmpty(releaseNotes) ? "# ReleaseNotes = ''": $"ReleaseNotes = '{releaseNotes}'";
            string prereleaseLine = String.IsNullOrEmpty(prerelease) ? "# Prerelease = ''" : $"Prerelease = '{prerelease}'";

            string requireLicenseAcceptanceLine = requireLicenseAcceptance == null? "# RequireLicenseAcceptance = $false" : (requireLicenseAcceptance == false ? "RequireLicenseAcceptance = $false": "RequireLicenseAcceptance = $true");

            string externalModuleDependenciesString = string.Join(", ", externalModuleDependencies.Select(item => "'" + item + "'"));
            string externalModuleDependenciesLine = externalModuleDependencies.Length == 0 ? "# ExternalModuleDependencies = @()" : $"ExternalModuleDependencies = @({externalModuleDependenciesString})";
    
            string initialPrivateDataString = "PrivateData = @{" + "\n" + "PSData = @{" + "\n";

            string privateDataString = $@"
                # Tags applied to this module. These help with module discovery in online galleries.
                {tagLine}

                # A URL to the license for this module.
                {licenseUriLine}

                # A URL to the main website for this project.
                {projectUriLine}

                # A URL to an icon representing this module.
                {iconUriLine}

                # ReleaseNotes of this module
                {releaseNotesLine}

                # Prerelease string of this module
                {prereleaseLine}

                # Flag to indicate whether the module requires explicit user acceptance for install/update/save
                {requireLicenseAcceptanceLine}

                # External dependent modules of this module
                {externalModuleDependenciesLine}";

            string endingPrivateDataString = "\n" + "} # End of PSData hashtable" + "\n" + "} # End of PrivateData hashtable";

            return initialPrivateDataString + privateDataString + endingPrivateDataString;
        }

        /// <summary>
        /// Replaces the default PrivateData entry in the .psd1 created with the values as parameters (either direct i.e as -Prerelease or via -PrivateData i.e PrivateData.PSData.Prerelease)
        /// This used for WinPS .psd1 creation as the correct PrivateData entry could not be populated otherwise.
        /// </summary>
        private bool TryCreateNewPsd1WithUpdatedPrivateData(string privateDataString, string tmpModuleManifestPath, string newTmpModuleManifestPath, out ErrorRecord errorRecord)
        {
            errorRecord = null;
            string[] psd1FileLines = File.ReadAllLines(tmpModuleManifestPath);

            int privateDataStartLine = 0;
            int privateDataEndLine = 0;

            // find line that is start of PrivateData entry
            for (int i = 0; i < psd1FileLines.Length; i++)
            {
                if (psd1FileLines[i].Trim().StartsWith("PrivateData =")){
                    privateDataStartLine = i;
                    break;
                }
            }

            // next find line that is end of the PrivateData entry
            int leftBracket = 0;
            for (int i = privateDataStartLine; i < psd1FileLines.Length; i++)
            {
                if (psd1FileLines[i].Contains("{"))
                {
                    leftBracket++;
                }
                else if(psd1FileLines[i].Contains("}"))
                {
                    if (leftBracket > 0)
                    {
                        leftBracket--;
                    }
                    
                    if (leftBracket == 0)
                    {
                        privateDataEndLine = i;
                        break;
                    }
                }
            }

            if (privateDataEndLine == 0)
            {
                errorRecord = new ErrorRecord(
                    new InvalidOperationException($"Could not locate/parse ending bracket for the PrivateData hashtable entry in module manifest (.psd1 file)."),
                    "PrivateDataEntryParsingError",
                    ErrorCategory.InvalidOperation,
                    this);
                
                return false;
            }

            List<string> newPsd1Lines = new List<string>();
            for (int i = 0; i < privateDataStartLine; i++)
            {
                newPsd1Lines.Add(psd1FileLines[i]);
            }

            newPsd1Lines.Add(privateDataString);
            for (int i = privateDataEndLine+1; i < psd1FileLines.Length; i++)
            {
                newPsd1Lines.Add(psd1FileLines[i]);
            }

            File.WriteAllLines(newTmpModuleManifestPath, newPsd1Lines);
            return true;
        }

        #endregion
    }
}
