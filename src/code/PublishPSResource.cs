// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using MoreLinq;
using MoreLinq.Extensions;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Versioning;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// Publishes a module, script, or nupkg to a designated repository.
    /// </summary>
    [Cmdlet(VerbsData.Publish, "PSResource", SupportsShouldProcess = true)]
    public sealed class PublishPSResource : PSCmdlet
    {
        #region Parameters

        /// <summary>
        /// Specifies the API key that you want to use to publish a module to the online gallery.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string ApiKey { get; set; }

        /// <summary>
        /// Specifies the repository to publish to.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        [ArgumentCompleter(typeof(RepositoryNameCompleter))]
        public string Repository { get; set; }

        /// <summary>
        /// Specifies the path to the resource that you want to publish. This parameter accepts the path to the folder that contains the resource.
        /// Specifies a path to one or more locations. Wildcards are permitted. The default location is the current directory (.).
        /// </summary>
        [Parameter (Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string Path { get; set; }

        /// <summary>
        /// Specifies the path to where the resource (as a nupkg) should be saved to. This parameter can be used in conjunction with the
        /// -Repository parameter to publish to a repository and also save the exact same package to the local file system.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string DestinationPath { get; set; }

        /// <summary>
        /// Specifies a user account that has rights to a specific repository (used for finding dependencies).
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public PSCredential Credential { get; set; }

        /// <summary>
        /// Bypasses the default check that all dependencies are present.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public SwitchParameter SkipDependenciesCheck { get; set; }

        /// <summary>
        /// Specifies a proxy server for the request, rather than a direct connection to the internet resource.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public Uri Proxy {
            set
            {
                if (value != null)
                {
                    var ex = new ArgumentException("Not yet implemented.");
                    var ProxyNotImplemented = new ErrorRecord(ex, "ProxyNotImplemented", ErrorCategory.InvalidData, null);
                    WriteError(ProxyNotImplemented);
                }
            }
        }

        /// <summary>
        /// Specifies a user account that has permission to use the proxy server that is specified by the Proxy parameter.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public PSCredential ProxyCredential {
            set
            {
                if (value != null)
                {
                    var ex = new ArgumentException("Not yet implemented.");
                    var ProxyCredentialNotImplemented = new ErrorRecord(ex, "ProxyCredentialNotImplemented", ErrorCategory.InvalidData, null);
                    WriteError(ProxyCredentialNotImplemented);
                }
            }
        }

        #endregion

        #region Members

        private string resolvedPath;
        private CancellationToken _cancellationToken;
        private NuGetVersion _pkgVersion;
        private string _pkgName;
        private static char[] _PathSeparators = new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar };
        public const string PSDataFileExt = ".psd1";
        public const string PSScriptFileExt = ".ps1";
        private const string PSScriptInfoCommentString = "<#PSScriptInfo";
        private string pathToScriptFileToPublish = string.Empty;
        private string pathToModuleManifestToPublish = string.Empty;
        private string pathToModuleDirToPublish = string.Empty;
        private ResourceType resourceType = ResourceType.None;
        private NetworkCredential _networkCredential;
        #endregion

        #region Method overrides

        protected override void BeginProcessing()
        {
            _cancellationToken = new CancellationToken();

            _networkCredential = Credential != null ? new NetworkCredential(Credential.UserName, Credential.Password) : null;

            // Create a respository story (the PSResourceRepository.xml file) if it does not already exist
            // This is to create a better experience for those who have just installed v3 and want to get up and running quickly
            RepositorySettings.CheckRepositoryStore();

            try
            {
                resolvedPath = SessionState.Path.GetResolvedPSPathFromPSPath(Path).First().Path;
            }
            catch (MethodInvocationException)
            {
                // path does not exist
                ThrowTerminatingError(
                    new ErrorRecord(
                        new ArgumentException(
                            "The path to the resource to publish does not exist, point to an existing path or file of the module or script to publish."), 
                            "SourcePathDoesNotExist", 
                            ErrorCategory.InvalidArgument, 
                            this));
            }

            // Condition 1: path is to the root directory of the module to be published
            // Condition 2: path is to the .psd1 or .ps1 of the module/script to be published  
            if (string.IsNullOrEmpty(resolvedPath))
            {
                // unsupported file path
                ThrowTerminatingError(
                    new ErrorRecord(
                        new ArgumentException(
                            "The path to the resource to publish is not in the correct format, point to a path or file of the module or script to publish."),
                            "InvalidSourcePath",
                            ErrorCategory.InvalidArgument,
                            this));
            }
            else if (Directory.Exists(resolvedPath))
            {
                pathToModuleDirToPublish = resolvedPath;
                resourceType = ResourceType.Module;
            }
            else if (resolvedPath.EndsWith(PSDataFileExt, StringComparison.OrdinalIgnoreCase))
            {
                pathToModuleManifestToPublish = resolvedPath;
                resourceType = ResourceType.Module;
            }
            else if (resolvedPath.EndsWith(PSScriptFileExt, StringComparison.OrdinalIgnoreCase))
            {
                pathToScriptFileToPublish = resolvedPath;
                resourceType = ResourceType.Script;
            }

            if (!String.IsNullOrEmpty(DestinationPath))
            {
                string resolvedDestinationPath = SessionState.Path.GetResolvedPSPathFromPSPath(DestinationPath).First().Path;

                if (Directory.Exists(resolvedDestinationPath))
                {
                    DestinationPath = resolvedDestinationPath;
                }
                else
                {
                    try
                    {
                        Directory.CreateDirectory(resolvedDestinationPath);
                    }
                    catch (Exception e)
                    {
                        var exMessage = string.Format("Destination path does not exist and cannot be created: {0}", e.Message);
                        var ex = new ArgumentException(exMessage);
                        var InvalidDestinationPath = new ErrorRecord(ex, "InvalidDestinationPath", ErrorCategory.InvalidArgument, null);
                        ThrowTerminatingError(InvalidDestinationPath);
                    }
                }
            }
        }

        protected override void EndProcessing()
        {
            // Returns the name of the file or the name of the directory, depending on path
            if (!ShouldProcess(string.Format("Publish resource '{0}' from the machine", resolvedPath)))
            {
                WriteVerbose("ShouldProcess is set to false.");
                return;
            }

            Hashtable parsedMetadata = new Hashtable(StringComparer.OrdinalIgnoreCase);
            if (resourceType == ResourceType.Script)
            {
                if (!PSScriptFileInfo.TryTestPSScriptFile(
                    scriptFileInfoPath: pathToScriptFileToPublish,
                    parsedScript: out PSScriptFileInfo scriptToPublish,
                    out ErrorRecord[] errors,
                    out string[] _
                ))
                {
                    foreach (ErrorRecord error in errors)
                    {
                        WriteError(error);
                    }

                    return;
                }

                parsedMetadata = scriptToPublish.ToHashtable();

                _pkgName = System.IO.Path.GetFileNameWithoutExtension(pathToScriptFileToPublish);
            }
            else
            {
                // parsedMetadata needs to be initialized for modules, will later be passed in to create nuspec
                if (!string.IsNullOrEmpty(pathToModuleManifestToPublish))
                {
                    _pkgName = System.IO.Path.GetFileNameWithoutExtension(pathToModuleManifestToPublish);
                }
                else {
                    // directory
                    // search for module manifest
                    List<FileInfo> childFiles = new DirectoryInfo(pathToModuleDirToPublish).EnumerateFiles().ToList();

                    foreach (FileInfo file in childFiles)
                    {
                        if (file.Name.EndsWith(PSDataFileExt, StringComparison.OrdinalIgnoreCase))
                        {
                            pathToModuleManifestToPublish = file.FullName;
                            _pkgName = System.IO.Path.GetFileNameWithoutExtension(file.Name);

                            break;
                        }
                    }
                }

                // Validate that there's a module manifest
                if (!File.Exists(pathToModuleManifestToPublish))
                {
                    var message = String.Format("No file with a .psd1 extension was found in {0}.  Please specify a path to a valid modulemanifest.", pathToModuleManifestToPublish);

                    var ex = new ArgumentException(message);
                    var moduleManifestNotFound = new ErrorRecord(ex, "moduleManifestNotFound", ErrorCategory.ObjectNotFound, null);
                    WriteError(moduleManifestNotFound);

                    return;
                }

                // validate that the module manifest has correct data
                string[] errorMsgs = null;
                try
                {
                    Utils.ValidateModuleManifest(pathToModuleManifestToPublish, out errorMsgs);

                }
                finally {
                    if (errorMsgs.Length > 0)
                    {
                        ThrowTerminatingError(new ErrorRecord(
                            new PSInvalidOperationException(errorMsgs.First()),
                            "InvalidModuleManifest",
                            ErrorCategory.InvalidOperation,
                            this));
                    }
                }
            }

            // Create a temp folder to push the nupkg to and delete it later
            string outputDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            try 
            {
                Directory.CreateDirectory(outputDir);
            }
            catch (Exception e)
            {
                var ex = new ArgumentException(e.Message);
                var ErrorCreatingTempDir = new ErrorRecord(ex, "ErrorCreatingTempDir", ErrorCategory.InvalidData, null);
                WriteError(ErrorCreatingTempDir);

                return;
            }
          
            try
            {
                // Create a nuspec
                // Right now parsedMetadataHash will be empty for modules and will contain metadata for scripts
                Hashtable dependencies;
                string nuspec = string.Empty;
                try
                {
                    nuspec = CreateNuspec(
                        outputDir: outputDir,
                        filePath: (resourceType == ResourceType.Script) ? pathToScriptFileToPublish : pathToModuleManifestToPublish,
                        parsedMetadataHash: parsedMetadata,
                        requiredModules: out dependencies);
                }
                catch (Exception e)
                {
                    var message = string.Format("Nuspec creation failed: {0}", e.Message);
                    var ex = new ArgumentException(message);
                    var nuspecCreationFailed = new ErrorRecord(ex, "NuspecCreationFailed", ErrorCategory.ObjectNotFound, null);
                    WriteError(nuspecCreationFailed);

                    return;
                }

                if (string.IsNullOrEmpty(nuspec))
                {
                    // nuspec creation failed.
                    WriteVerbose("Nuspec creation failed.");
                    return;
                }

                // Find repository
                PSRepositoryInfo repository = RepositorySettings.Read(new[] { Repository }, out string[] _).FirstOrDefault();
                if (repository == null)
                {
                    var message = String.Format("The resource repository '{0}' is not a registered. Please run 'Register-PSResourceRepository' in order to publish to this repository.", Repository);
                    var ex = new ArgumentException(message);
                    var repositoryNotFound = new ErrorRecord(ex, "repositoryNotFound", ErrorCategory.ObjectNotFound, null);
                    WriteError(repositoryNotFound);

                    return;
                }
                else if(repository.Uri.Scheme == Uri.UriSchemeFile && !repository.Uri.IsUnc && !Directory.Exists(repository.Uri.LocalPath))
                {
                    // this check to ensure valid local path is not for UNC paths (which are server based, instead of Drive based)
                    var message = String.Format("The repository '{0}' with uri: {1} is not a valid folder path which exists. If providing a file based repository, provide a repository with a path that exists.", Repository, repository.Uri.AbsoluteUri);
                    var ex = new ArgumentException(message);
                    var fileRepositoryPathDoesNotExistError = new ErrorRecord(ex, "repositoryPathDoesNotExist", ErrorCategory.ObjectNotFound, null);
                    WriteError(fileRepositoryPathDoesNotExistError);

                    return;
                }

                // Check if dependencies already exist within the repo if:
                // 1) the resource to publish has dependencies and
                // 2) the -SkipDependenciesCheck flag is not passed in
                if (dependencies != null && !SkipDependenciesCheck)
                {
                    // If error gets thrown, exit process record
                    if (!CheckDependenciesExist(dependencies, repository.Name))
                    {
                        return;
                    }
                }

                if (resourceType == ResourceType.Script)
                {
                    // copy the script file to the temp directory
                    File.Copy(pathToScriptFileToPublish, System.IO.Path.Combine(outputDir, _pkgName + PSScriptFileExt), true);

                }
                else
                {
                    try
                    {
                        // If path is pointing to a file, get the parent directory, otherwise assumption is that path is pointing to the root directory
                        string rootModuleDir = !string.IsNullOrEmpty(pathToModuleManifestToPublish) ? System.IO.Path.GetDirectoryName(pathToModuleManifestToPublish) : pathToModuleDirToPublish;

                        // Create subdirectory structure in temp folder
                        foreach (string dir in System.IO.Directory.GetDirectories(rootModuleDir, "*", System.IO.SearchOption.AllDirectories))
                        {
                            DirectoryInfo dirInfo = new DirectoryInfo(dir);
                            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(outputDir, dirInfo.Name));
                        }

                        // Copy files over to temp folder
                        foreach (string fileNamePath in System.IO.Directory.GetFiles(rootModuleDir, "*", System.IO.SearchOption.AllDirectories))
                        {
                            FileInfo fileInfo = new FileInfo(fileNamePath);

                            var newFilePath = System.IO.Path.Combine(outputDir, fileInfo.Name);
                            // The user may have a .nuspec defined in the module directory
                            // If that's the case, we will not use that file and use the .nuspec that is generated via PSGet
                            // The .nuspec that is already in in the output directory is the one that was generated via the CreateNuspec method
                            if (!File.Exists(newFilePath))
                            {
                                System.IO.File.Copy(fileNamePath, newFilePath);
                            }
                        }


                    }
                    catch (Exception e)
                    {
                       ThrowTerminatingError(new ErrorRecord(
                           new ArgumentException("Error occured while creating directory to publish: " + e.Message),
                           "ErrorCreatingDirectoryToPublish",
                           ErrorCategory.InvalidOperation,
                           this));
                    }
                }

                var outputNupkgDir = System.IO.Path.Combine(outputDir, "nupkg");

                // pack into .nupkg
                if (!PackNupkg(outputDir, outputNupkgDir, nuspec, out ErrorRecord packNupkgError))
                {
                    WriteError(packNupkgError);
                    // exit out of processing
                    return;
                }

                // If -DestinationPath is specified then also publish the .nupkg there
                if (!string.IsNullOrWhiteSpace(DestinationPath))
                {
                    try
                    {
                        var nupkgName = _pkgName + "." + _pkgVersion.ToNormalizedString() + ".nupkg";
                        File.Copy(System.IO.Path.Combine(outputNupkgDir, nupkgName), System.IO.Path.Combine(DestinationPath, nupkgName));
                    }
                    catch (Exception e) {
                        var message = string.Format("Error moving .nupkg into destination path '{0}' due to: '{1}'.", DestinationPath, e.Message);

                        var ex = new ArgumentException(message);
                        var ErrorMovingNupkg = new ErrorRecord(ex, "ErrorMovingNupkg", ErrorCategory.NotSpecified, null);
                        WriteError(ErrorMovingNupkg);

                        // exit process record
                        return;
                    }
                }

                string repositoryUri = repository.Uri.AbsoluteUri;
                
                // This call does not throw any exceptions, but it will write unsuccessful responses to the console
                if (!PushNupkg(outputNupkgDir, repository.Name, repositoryUri, out ErrorRecord pushNupkgError))
                {
                    WriteError(pushNupkgError);
                    // exit out of processing
                    return;
                }
            }
            finally
            {
                WriteVerbose(string.Format("Deleting temporary directory '{0}'", outputDir));

                Utils.DeleteDirectory(outputDir);
            }

        }
        #endregion

        #region Private methods

        private string CreateNuspec(
            string outputDir,
            string filePath,
            Hashtable parsedMetadataHash,
            out Hashtable requiredModules)
        {
            WriteVerbose("Creating new nuspec file.");
            requiredModules = new Hashtable();

            // A script will already have the metadata parsed into the parsedMetadatahash,
            // a module will still need the module manifest to be parsed.
            if (resourceType == ResourceType.Module)
            {
                // Use the parsed module manifest data as 'parsedMetadataHash' instead of the passed-in data.
                if (!Utils.TryReadManifestFile(
                    manifestFilePath: filePath,
                    manifestInfo: out parsedMetadataHash,
                    error: out Exception manifestReadError))
                {
                    WriteError(
                        new ErrorRecord(
                            exception: manifestReadError,
                            errorId: "ManifestFileReadParseForNuspecError",
                            errorCategory: ErrorCategory.ReadError,
                            this));
                    
                    return string.Empty;
                }
            }

            // now we have parsedMetadatahash to fill out the nuspec information
            var nameSpaceUri = "http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd";
            var doc = new XmlDocument();

            // xml declaration is recommended, but not mandatory
            XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "utf-8", null);
            XmlElement root = doc.DocumentElement;
            doc.InsertBefore(xmlDeclaration, root);

            // create top-level elements
            XmlElement packageElement = doc.CreateElement("package", nameSpaceUri);
            XmlElement metadataElement = doc.CreateElement("metadata", nameSpaceUri);

            Dictionary<string, string> metadataElementsDictionary = new Dictionary<string, string>();

            // id is mandatory
            metadataElementsDictionary.Add("id", _pkgName);

            string version;
            if (parsedMetadataHash.ContainsKey("moduleversion"))
            {
                version = parsedMetadataHash["moduleversion"].ToString();
            }
            else if (parsedMetadataHash.ContainsKey("version"))
            {
                version = parsedMetadataHash["version"].ToString();
            }
            else
            {
                // no version is specified for the nuspec
                var message = "There is no package version specified. Please specify a version before publishing.";
                var ex = new ArgumentException(message);
                var NoVersionFound = new ErrorRecord(ex, "NoVersionFound", ErrorCategory.InvalidArgument, null);
                WriteError(NoVersionFound);

                return string.Empty;
            }

            // Look for Prerelease tag and then process any Tags in PrivateData > PSData
            if (parsedMetadataHash.ContainsKey("PrivateData"))
            {
                if (parsedMetadataHash["PrivateData"] is Hashtable privateData &&
                    privateData.ContainsKey("PSData"))
                {
                    if (privateData["PSData"] is Hashtable psData)
                    {
                        if (psData.ContainsKey("prerelease") && psData["prerelease"] is string preReleaseVersion)
                        {
                            version = string.Format(@"{0}-{1}", version, preReleaseVersion);
                        }

                        if (psData.ContainsKey("licenseuri") && psData["licenseuri"] is string licenseUri)

                        {
                            metadataElementsDictionary.Add("license", licenseUri.Trim());
                        }

                        if (psData.ContainsKey("projecturi") && psData["projecturi"] is string projectUri)
                        {
                            metadataElementsDictionary.Add("projectUrl", projectUri.Trim());
                        }

                        if (psData.ContainsKey("iconuri") && psData["iconuri"] is string iconUri)
                        {
                            metadataElementsDictionary.Add("iconUrl", iconUri.Trim());
                        }

                        if (psData.ContainsKey("releasenotes"))
                        {
                            if (psData["releasenotes"] is string releaseNotes)
                            {
                                metadataElementsDictionary.Add("releaseNotes", releaseNotes.Trim());
                            }
                            else if (psData["releasenotes"] is string[] releaseNotesArr)
                            {
                                metadataElementsDictionary.Add("releaseNotes", string.Join("\n", releaseNotesArr));
                            }
                        }

                        // defaults to false
                        string requireLicenseAcceptance = psData.ContainsKey("requirelicenseacceptance") ? psData["requirelicenseacceptance"].ToString() : "false";

                        metadataElementsDictionary.Add("requireLicenseAcceptance", requireLicenseAcceptance);


                        if (psData.ContainsKey("Tags") && psData["Tags"] is Array manifestTags)
                        {
                            var tagArr = new List<string>();
                            foreach (string tag in manifestTags)
                            {
                                tagArr.Add(tag);
                            }
                            parsedMetadataHash["tags"] = string.Join(" ", tagArr.ToArray());
                        }
                    }
                }
            }

            if (NuGetVersion.TryParse(version, out _pkgVersion))
            {
                metadataElementsDictionary.Add("version", _pkgVersion.ToNormalizedString());
            }

            if (parsedMetadataHash.ContainsKey("author"))
            {
                metadataElementsDictionary.Add("authors", parsedMetadataHash["author"].ToString().Trim());
            }

            if (parsedMetadataHash.ContainsKey("companyname"))
            {
                metadataElementsDictionary.Add("owners", parsedMetadataHash["companyname"].ToString().Trim());
            }

            if (parsedMetadataHash.ContainsKey("description"))
            {
                metadataElementsDictionary.Add("description", parsedMetadataHash["description"].ToString().Trim());
            }

            if (parsedMetadataHash.ContainsKey("copyright"))
            {
                metadataElementsDictionary.Add("copyright", parsedMetadataHash["copyright"].ToString().Trim());
            }

            string tags = (resourceType == ResourceType.Script) ? "PSScript" : "PSModule";
            if (parsedMetadataHash.ContainsKey("tags"))
            {
                if (parsedMetadataHash["tags"] != null)
                {
                    tags += " " + parsedMetadataHash["tags"].ToString().Trim();
                }
            }
            metadataElementsDictionary.Add("tags", tags);


            // Example nuspec:
            /*
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>System.Management.Automation</id>
                <version>1.0.0</version>
                <authors>Microsoft</authors>
                <owners>Microsoft,PowerShell</owners>
                <requireLicenseAcceptance>false</requireLicenseAcceptance>
                <license type="expression">MIT</license>
                <license>https://licenses.nuget.org/MIT</license>
                <icon>Powershell_black_64.png</icon>
                <projectUrl>https://github.com/PowerShell/PowerShell</projectUrl>
                <description>Example description here</description>
                <copyright>Copyright (c) Microsoft Corporation. All rights reserved.</copyright>
                <language>en-US</language>
                <tags>PowerShell</tags>
                <dependencies>
                  <group targetFramework="net5.0">
                    <dependency id="Microsoft.PowerShell.CoreCLR.Eventing" version="7.1.3" />
                    <dependency id="Microsoft.PowerShell.Native" version="7.1.0" />
                  </group>
                </dependencies>
              </ metadata >
            </ package >
            */

            foreach (var key in metadataElementsDictionary.Keys)
            {
                if (metadataElementsDictionary.TryGetValue(key, out string elementInnerText))
                {
                    XmlElement element = doc.CreateElement(key, nameSpaceUri);
                    element.InnerText = elementInnerText;
                    metadataElement.AppendChild(element);
                }
                else {
                    WriteVerbose(string.Format("Creating XML element failed. Unable to get value from key '{0}'.", key));
                }
            }

            requiredModules = ParseRequiredModules(parsedMetadataHash);
            if (requiredModules != null)
            {
                XmlElement dependenciesElement = doc.CreateElement("dependencies", nameSpaceUri);

                foreach (string dependencyName in requiredModules.Keys)
                {
                    XmlElement element = doc.CreateElement("dependency", nameSpaceUri);

                    element.SetAttribute("id", dependencyName);
                    string dependencyVersion = requiredModules[dependencyName].ToString();
                    if (!string.IsNullOrEmpty(dependencyVersion))
                    {
                        element.SetAttribute("version", requiredModules[dependencyName].ToString());
                    }

                    dependenciesElement.AppendChild(element);
                }
                metadataElement.AppendChild(dependenciesElement);
            }

            packageElement.AppendChild(metadataElement);
            doc.AppendChild(packageElement);

            var nuspecFullName = System.IO.Path.Combine(outputDir, _pkgName + ".nuspec");
            doc.Save(nuspecFullName);

            WriteVerbose("The newly created nuspec is: " + nuspecFullName);

            return nuspecFullName;
        }

        private Hashtable ParseRequiredModules(Hashtable parsedMetadataHash)
        {
            if (!parsedMetadataHash.ContainsKey("requiredmodules"))
            {
                return null;
            }

            var requiredModules = parsedMetadataHash["requiredmodules"];

            // Required modules can be:
            //  a. An array of hash tables of module name and version
            //  b. A single hash table of module name and version
            //  c. A string array of module names
            //  d. A single string module name

            var dependenciesHash = new Hashtable();
            if (LanguagePrimitives.TryConvertTo<Hashtable[]>(requiredModules, out Hashtable[] moduleList))
            {
                // instead of returning an array of hashtables,
                // loop through the array and add each element of
                foreach (Hashtable hash in moduleList)
                {
                    dependenciesHash.Add(hash["ModuleName"], hash["ModuleVersion"]);
                }
            }
            else if (LanguagePrimitives.TryConvertTo<string[]>(requiredModules, out string[] moduleNames))
            {
                foreach (var modName in moduleNames)
                {
                    dependenciesHash.Add(modName, string.Empty);
                }
            }

            return dependenciesHash;
        }

        private bool CheckDependenciesExist(Hashtable dependencies, string repositoryName)
        {
            // Check to see that all dependencies are in the repository
            // Searches for each dependency in the repository the pkg is being pushed to,
            // If the dependency is not there, error
            foreach (var dependency in dependencies.Keys)
            {
                // Need to make individual calls since we're look for exact version numbers or ranges.
                var depName = new[] { (string)dependency };
                // test version 
                string depVersion = dependencies[dependency] as string;
                depVersion = string.IsNullOrWhiteSpace(depVersion) ? "*" : depVersion;

                VersionRange versionRange = null;
                if (!Utils.TryParseVersionOrVersionRange(depVersion, out versionRange))
                {
                    // This should never be true because Test-ModuleManifest will throw an error if dependency versions are incorrectly formatted
                    // This is being left as a safeguard for parsing a version from a string to a version range.
                    ThrowTerminatingError(new ErrorRecord(
                        new ArgumentException(string.Format("Error parsing dependency version {0}, from the module {1}", depVersion, depName)),
                        "IncorrectVersionFormat",
                        ErrorCategory.InvalidArgument,
                        this));
                }
                
                // Search for and return the dependency if it's in the repository.
                FindHelper findHelper = new FindHelper(_cancellationToken, this, _networkCredential);
                bool depPrerelease = depVersion.Contains("-");

                var repository = new[] { repositoryName };
                var dependencyFound = findHelper.FindByResourceName(depName, ResourceType.Module, depVersion, depPrerelease, null, repository, false);
                if (dependencyFound == null || !dependencyFound.Any())
                {
                    var message = String.Format("Dependency '{0}' was not found in repository '{1}'.  Make sure the dependency is published to the repository before publishing this module.", dependency, repositoryName);
                    var ex = new ArgumentException(message);
                    var dependencyNotFound = new ErrorRecord(ex, "DependencyNotFound", ErrorCategory.ObjectNotFound, null);

                    WriteError(dependencyNotFound);
                    return false;
                }
            }
            return true;
        }

        private bool PackNupkg(string outputDir, string outputNupkgDir, string nuspecFile, out ErrorRecord error)
        {
            // Pack the module or script into a nupkg given a nuspec.   
            var builder = new PackageBuilder();
            try
            {
                var runner = new PackCommandRunner(
                    new PackArgs
                    {
                        CurrentDirectory = outputDir,
                        OutputDirectory = outputNupkgDir,
                        Path = nuspecFile,
                        Exclude = System.Array.Empty<string>(),
                        Symbols = false,
                        Logger = NullLogger.Instance
                    },
                    MSBuildProjectFactory.ProjectCreator,
                    builder);
                bool success = runner.RunPackageBuild();
                
                if (success)
                {
                    WriteVerbose("Successfully packed the resource into a .nupkg");
                }
                else
                {
                    var message = String.Format("Not able to successfully pack the resource into a .nupkg");
                    var ex = new InvalidOperationException(message);
                    var failedToPackIntoNupkgError = new ErrorRecord(ex, "failedToPackIntoNupkg", ErrorCategory.ObjectNotFound, null);
                    error = failedToPackIntoNupkgError;
                    return false;
                }
            }
            catch (Exception e)
            {
                var message =  string.Format("Unexpectd error packing into .nupkg: '{0}'.", e.Message);
                var ex = new ArgumentException(message);
                var ErrorPackingIntoNupkg = new ErrorRecord(ex, "ErrorPackingIntoNupkg", ErrorCategory.NotSpecified, null);

                error = ErrorPackingIntoNupkg;
                // exit process record
                return false;
            }

            error = null;
            return true;
        }

        private bool PushNupkg(string outputNupkgDir, string repoName, string repoUri, out ErrorRecord error)
        {
            // Push the nupkg to the appropriate repository
            // Pkg version is parsed from .ps1 file or .psd1 file
            var fullNupkgFile = System.IO.Path.Combine(outputNupkgDir, _pkgName + "." + _pkgVersion.ToNormalizedString() + ".nupkg");

            // The PSGallery uses the v2 protocol still and publishes to a slightly different endpoint:
            // "https://www.powershellgallery.com/api/v2/package"
            // Until the PSGallery is moved onto the NuGet v3 server protocol, we'll modify the repository uri
            // to accommodate for the approprate publish location.
            string publishLocation = repoUri.EndsWith("/v2", StringComparison.OrdinalIgnoreCase) ? repoUri + "/package" : repoUri;

            var settings = NuGet.Configuration.Settings.LoadDefaultSettings(null, null, null);
            var success = false;
            try
            {
                PushRunner.Run(
                        settings: Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null),
                        sourceProvider: new PackageSourceProvider(settings),
                        packagePaths: new List<string> { fullNupkgFile },
                        source: publishLocation,
                        apiKey: ApiKey,
                        symbolSource: null,
                        symbolApiKey: null,
                        timeoutSeconds: 0,
                        disableBuffering: false,
                        noSymbols: false,
                        noServiceEndpoint: false,  // enable server endpoint
                        skipDuplicate: false, // if true-- if a package and version already exists, skip it and continue with the next package in the push, if any.
                        logger: NullLogger.Instance // nuget logger
                        ).GetAwaiter().GetResult();
            }
            catch (HttpRequestException e)
            {
                WriteVerbose(string.Format("Not able to publish resource to '{0}'", repoUri));
                //  look in PS repo for how httpRequestExceptions are handled

                // Unfortunately there is no response message  are no status codes provided with the exception and no
                var ex = new ArgumentException(String.Format("Repository '{0}': {1}", repoName, e.Message));
                if (e.Message.Contains("401"))
                {
                    if (e.Message.Contains("API"))
                    {
                        var message = String.Format("{0} Please try running again with the -ApiKey parameter and specific API key for the repository specified.", e.Message);
                        ex = new ArgumentException(message);
                        var ApiKeyError = new ErrorRecord(ex, "ApiKeyError", ErrorCategory.AuthenticationError, null);
                        error = ApiKeyError;
                    }
                    else
                    {
                        var Error401 = new ErrorRecord(ex, "401Error", ErrorCategory.PermissionDenied, null);
                        error = Error401;
                    }
                }
                else if (e.Message.Contains("403"))
                {
                    var Error403 = new ErrorRecord(ex, "403Error", ErrorCategory.PermissionDenied, null);
                    error = Error403;
                }
                else if (e.Message.Contains("409"))
                {
                    var Error409 = new ErrorRecord(ex, "409Error", ErrorCategory.PermissionDenied, null);
                    error = Error409;
                }
                else
                {
                    var HTTPRequestError = new ErrorRecord(ex, "HTTPRequestError", ErrorCategory.PermissionDenied, null);
                    error = HTTPRequestError;
                }

                return success;
            }
            catch (Exception e)
            {
                WriteVerbose(string.Format("Not able to publish resource to '{0}'", repoUri));
                var ex = new ArgumentException(e.Message);
                var PushNupkgError = new ErrorRecord(ex, "PushNupkgError", ErrorCategory.InvalidResult, null);
                error = PushNupkgError;

                return success;
            }


            WriteVerbose(string.Format("Successfully published the resource to '{0}'", repoUri));
            error = null;
            success = true;
            return success;

        }
    }

    #endregion
}
