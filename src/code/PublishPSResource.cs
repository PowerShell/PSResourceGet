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
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Net.Http;
using System.Text.RegularExpressions;
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

        private string _path;
        private NuGetVersion _pkgVersion;
        private string _pkgName;
        private static char[] _PathSeparators = new [] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar };
        public const string PSDataFileExt = ".psd1";
        public const string PSScriptFileExt = ".ps1";
        private const string PSScriptInfoCommentString = "<#PSScriptInfo";

        #endregion

        #region Method overrides

        protected override void BeginProcessing()
        {
            // Create a respository story (the PSResourceRepository.xml file) if it does not already exist
            // This is to create a better experience for those who have just installed v3 and want to get up and running quickly
            RepositorySettings.CheckRepositoryStore();

            string resolvedPath = SessionState.Path.GetResolvedPSPathFromPSPath(Path).First().Path;

            if (Directory.Exists(resolvedPath) || 
                (File.Exists(resolvedPath) && resolvedPath.EndsWith(PSScriptFileExt, StringComparison.OrdinalIgnoreCase)))
            {
                // condition 1: we point to a folder when publishing a module
                // condition 2: we point to a .ps1 file directly when publishing a script, but not to .psd1 file (for publishing a module)
                _path = resolvedPath;
            }
            else
            {
                // unsupported file path
                var exMessage = string.Format("Either the path to the resource to publish does not exist or is not in the correct format, for scripts point to .ps1 file and for modules point to folder containing .psd1");
                var ex = new ArgumentException(exMessage);
                var InvalidSourcePathError = new ErrorRecord(ex, "InvalidSourcePath", ErrorCategory.InvalidArgument, null);
                ThrowTerminatingError(InvalidSourcePathError);
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
            var pkgFileOrDir = new DirectoryInfo(_path);
            bool isScript = _path.EndsWith(PSScriptFileExt, StringComparison.OrdinalIgnoreCase);

            if (!ShouldProcess(string.Format("Publish resource '{0}' from the machine", _path)))
            {
                WriteVerbose("ShouldProcess is set to false.");
                return;
            }

            string resourceFilePath;
            Hashtable parsedMetadata;
            if (isScript)
            {
                // this converts from System.IO.DirectoryInfo to string. resourceFilePath needs to be set for later use though
                // alternatively could use resourceFilePath = _path
                resourceFilePath = pkgFileOrDir.FullName;

                // Check that script metadata is valid
                if (!TryParseScriptMetadata(
                    out parsedMetadata,
                    resourceFilePath,
                    out ErrorRecord[] errors))
                {
                    foreach (ErrorRecord err in errors)
                    {
                        WriteError(err);
                    }

                    return;
                }

                // remove '.ps1' extension from file name
                _pkgName = pkgFileOrDir.Name.Remove(pkgFileOrDir.Name.Length - 4);
            }
            else
            {
                _pkgName = pkgFileOrDir.Name;
                resourceFilePath = System.IO.Path.Combine(_path, _pkgName + PSDataFileExt);
                parsedMetadata = new Hashtable();

                // Validate that there's a module manifest
                if (!File.Exists(resourceFilePath))
                {
                    var message = String.Format("No file with a .psd1 extension was found in {0}.  Please specify a path to a valid modulemanifest.", resourceFilePath);
                    var ex = new ArgumentException(message);
                    var moduleManifestNotFound = new ErrorRecord(ex, "moduleManifestNotFound", ErrorCategory.ObjectNotFound, null);
                    WriteError(moduleManifestNotFound);

                    return;
                }

                // validate that the module manifest has correct data
                if (!IsValidModuleManifest(resourceFilePath))
                {
                    return;
                }
            }

            // Create a temp folder to push the nupkg to and delete it later
            string outputDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            if (!Directory.Exists(outputDir))
            {
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
                        filePath: resourceFilePath,
                        isScript: isScript,
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
                else if(repository.Uri.Scheme == Uri.UriSchemeFile && !Directory.Exists(repository.Uri.LocalPath))
                {
                    //TODO: Anam would this include localhost? Test this.
                    var message = String.Format("The repository '{0}' with uri: {1} is not a valid folder path which exists. If providing a file based repository, provide a repository with a path that exists.", Repository, repository.Uri.AbsoluteUri);
                    var ex = new ArgumentException(message);
                    var fileRepositoryPathDoesNotExistError = new ErrorRecord(ex, "repositoryPathDoesNotExist", ErrorCategory.ObjectNotFound, null);
                    WriteError(fileRepositoryPathDoesNotExistError);

                    return;
                }

                string repositoryUri = repository.Uri.AbsoluteUri;

                // Check if dependencies already exist within the repo if:
                // 1) the resource to publish has dependencies and
                // 2) the -SkipDependenciesCheck flag is not passed in
                if (dependencies != null && !SkipDependenciesCheck)
                {
                    // If error gets thrown, exit process record
                    if (!CheckDependenciesExist(dependencies, repositoryUri))
                    {
                        return;
                    }
                }

                if (isScript)
                {
                    // copy the script file to the temp directory
                    File.Copy(_path, System.IO.Path.Combine(outputDir, _pkgName + PSScriptFileExt), true);
                }
                else
                {
                    // Create subdirectory structure in temp folder
                    foreach (string dir in System.IO.Directory.GetDirectories(_path, "*", System.IO.SearchOption.AllDirectories))
                    {
                        var dirName = dir.Substring(_path.Length).Trim(_PathSeparators);
                        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(outputDir, dirName));
                    }

                    // Copy files over to temp folder
                    foreach (string fileNamePath in System.IO.Directory.GetFiles(_path, "*", System.IO.SearchOption.AllDirectories))
                    {
                        var fileName = fileNamePath.Substring(_path.Length).Trim(_PathSeparators);

                        // The user may have a .nuspec defined in the module directory
                        // If that's the case, we will not use that file and use the .nuspec that is generated via PSGet
                        // The .nuspec that is already in in the output directory is the one that was generated via the CreateNuspec method
                        var newFilePath = System.IO.Path.Combine(outputDir, fileName);
                        if (!File.Exists(newFilePath))
                        {
                            System.IO.File.Copy(fileNamePath, newFilePath);
                        }
                    }
                }

                var outputNupkgDir = System.IO.Path.Combine(outputDir, "nupkg");

                // pack into .nupkg
                if (!PackNupkg(outputDir, outputNupkgDir, nuspec, out ErrorRecord error))
                {
                    WriteError(error);
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

                // This call does not throw any exceptions, but it will write unsuccessful responses to the console
                PushNupkg(outputNupkgDir, repository.Name, repositoryUri);

            }
            finally
            {
                WriteVerbose(string.Format("Deleting temporary directory '{0}'", outputDir));

                Utils.DeleteDirectory(outputDir);
            }

        }
        #endregion

        #region Private methods

        private bool IsValidModuleManifest(string moduleManifestPath)
        {
            var isValid = true;
            using (System.Management.Automation.PowerShell pwsh = System.Management.Automation.PowerShell.Create())
            {
                // use PowerShell cmdlet Test-ModuleManifest
                // TODO: Test-ModuleManifest will throw an error if RequiredModules specifies a module that does not exist
                // locally on the machine. Consider adding a -Syntax param to Test-ModuleManifest so that it only checks that
                // the syntax is correct. In build/release pipelines for example, the modules listed under RequiredModules may
                // not be locally available, but we still want to allow the user to publish.
                var results = pwsh.AddCommand("Test-ModuleManifest").AddParameter("Path", moduleManifestPath).Invoke();

                if (pwsh.HadErrors)
                {
                    var message = string.Empty;
                    if (string.IsNullOrWhiteSpace((results[0].BaseObject as PSModuleInfo).Author))
                    {
                        message = "No author was provided in the module manifest. The module manifest must specify a version, author and description.";
                    }
                    else if (string.IsNullOrWhiteSpace((results[0].BaseObject as PSModuleInfo).Description))
                    {
                        message = "No description was provided in the module manifest. The module manifest must specify a version, author and description.";
                    }
                    else
                    {
                        // This will handle version errors
                        var error = pwsh.Streams.Error;
                        message = error[0].ToString();
                    }
                    var ex = new ArgumentException(message);
                    var InvalidModuleManifest = new ErrorRecord(ex, "InvalidModuleManifest", ErrorCategory.InvalidData, null);
                    WriteError(InvalidModuleManifest);
                    isValid = false;
                }
            }

            return isValid;
        }

        private string CreateNuspec(
            string outputDir,
            string filePath,
            bool isScript,
            Hashtable parsedMetadataHash,
            out Hashtable requiredModules)
        {
            WriteVerbose("Creating new nuspec file.");
            requiredModules = new Hashtable();

            // A script will already have the metadata parsed into the parsedMetadatahash,
            // a module will still need the module manifest to be parsed.
            if (!isScript)
            {
                // Parse the module manifest and *replace* the passed-in metadata with the module manifest metadata.
                if (!Utils.TryParsePSDataFile(
                    moduleFileInfo: filePath,
                    cmdletPassedIn: this,
                    parsedMetadataHashtable: out parsedMetadataHash))
                {
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
                        if (psData.ContainsKey("Prerelease") && psData["Prerelease"] is string preReleaseVersion)
                        {
                            version = string.Format(@"{0}-{1}", version, preReleaseVersion);
                        }
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

            // defaults to false
            var requireLicenseAcceptance = parsedMetadataHash.ContainsKey("requirelicenseacceptance") ? parsedMetadataHash["requirelicenseacceptance"].ToString().ToLower().Trim()
                : "false";
            metadataElementsDictionary.Add("requireLicenseAcceptance", requireLicenseAcceptance);

            if (parsedMetadataHash.ContainsKey("description"))
            {
                metadataElementsDictionary.Add("description", parsedMetadataHash["description"].ToString().Trim());
            }

           if (parsedMetadataHash.ContainsKey("releasenotes"))
            {
                metadataElementsDictionary.Add("releaseNotes", parsedMetadataHash["releasenotes"].ToString().Trim());
            }

            if (parsedMetadataHash.ContainsKey("copyright"))
            {
                metadataElementsDictionary.Add("copyright", parsedMetadataHash["copyright"].ToString().Trim());
            }

            string tags = isScript ?  "PSScript" : "PSModule";
            if (parsedMetadataHash.ContainsKey("tags"))
            {
                if (parsedMetadataHash["tags"] != null)
                {
                    tags += " " + parsedMetadataHash["tags"].ToString().Trim();
                }
            }
            metadataElementsDictionary.Add("tags", tags);

            if (parsedMetadataHash.ContainsKey("licenseurl"))
            {
                metadataElementsDictionary.Add("licenseUrl", parsedMetadataHash["licenseurl"].ToString().Trim());
            }

            if (parsedMetadataHash.ContainsKey("projecturl"))
            {
                metadataElementsDictionary.Add("projectUrl", parsedMetadataHash["projecturl"].ToString().Trim());
            }

            if (parsedMetadataHash.ContainsKey("iconurl"))
            {
                metadataElementsDictionary.Add("iconUrl", parsedMetadataHash["iconurl"].ToString().Trim());
            }

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
                <licenseUrl>https://licenses.nuget.org/MIT</licenseUrl>
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

        private bool TryParseScriptMetadata(
            out Hashtable parsedMetadata,
            string filePath,
            out ErrorRecord[] errors)
        {
            parsedMetadata = new Hashtable();
            List<ErrorRecord> parseMetadataErrors = new List<ErrorRecord>();
            // Parse the script file
            var ast = Parser.ParseFile(
                filePath,
                out System.Management.Automation.Language.Token[] tokens,
                out ParseError[] parserErrors);

            if (parserErrors.Length > 0)
            {
                foreach (ParseError err in parserErrors)
                {
                    // we ignore WorkFlowNotSupportedInPowerShellCore errors, as this is common in scripts currently on PSGallery
                    if (!String.Equals(err.ErrorId, "WorkflowNotSupportedInPowerShellCore", StringComparison.OrdinalIgnoreCase))
                    {
                        var message = String.Format("Could not parse '{0}' as a PowerShell script file due to {1}.", filePath, err.Message);
                        var ex = new ArgumentException(message);
                        var psScriptFileParseError = new ErrorRecord(ex, err.ErrorId, ErrorCategory.ParserError, null);
                        parseMetadataErrors.Add(psScriptFileParseError);
                    }
                }

                errors = parseMetadataErrors.ToArray();
                return false;
            }

            if (ast == null)
            {
                var astNullMessage = String.Format(".ps1 file was parsed but AST was null");
                var astNullEx = new ArgumentException(astNullMessage);
                var astCouldNotBeCreatedError = new ErrorRecord(astNullEx, "ASTCouldNotBeCreated", ErrorCategory.ParserError, null);

                parseMetadataErrors.Add(astCouldNotBeCreatedError);
                errors = parseMetadataErrors.ToArray();
                return false;

            }

            // Get the block/group comment beginning with <#PSScriptInfo
            List<System.Management.Automation.Language.Token> commentTokens = tokens.Where(a => String.Equals(a.Kind.ToString(), "Comment", StringComparison.OrdinalIgnoreCase)).ToList();
            string commentPattern = PSScriptInfoCommentString;
            Regex rg = new Regex(commentPattern);
            List<System.Management.Automation.Language.Token> psScriptInfoCommentTokens = commentTokens.Where(a => rg.IsMatch(a.Extent.Text)).ToList();

            if (psScriptInfoCommentTokens.Count() == 0 || psScriptInfoCommentTokens[0] == null)
            {
                var message = String.Format("PSScriptInfo comment was missing or could not be parsed");
                var ex = new ArgumentException(message);
                var psCommentMissingError = new ErrorRecord(ex, "psScriptInfoCommentMissingError", ErrorCategory.ParserError, null);
                parseMetadataErrors.Add(psCommentMissingError);
                errors = parseMetadataErrors.ToArray();
                return false;
            }

            string[] commentLines = Regex.Split(psScriptInfoCommentTokens[0].Text, "[\r\n]").Where(x => !String.IsNullOrEmpty(x)).ToArray();
            string keyName = String.Empty;
            string value = String.Empty;

            /**
            If comment line count is not more than two, it doesn't have the any metadata property
            comment block would look like:
            <#PSScriptInfo
            #>
            */

            if (commentLines.Count() > 2)
            {
                for (int i = 1; i < commentLines.Count(); i++)
                {
                    string line = commentLines[i];
                    if (String.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    // A line is starting with . conveys a new metadata property
                    if (line.Trim().StartsWith("."))
                    {
                        string[] parts = line.Trim().TrimStart('.').Split();
                        keyName = parts[0].ToLower();
                        value = parts.Count() > 1 ? String.Join(" ", parts.Skip(1)) : String.Empty;
                        parsedMetadata.Add(keyName, value);
                    }
                }
            }

            // get .DESCRIPTION comment
            CommentHelpInfo scriptCommentInfo = ast.GetHelpContent();
            if (scriptCommentInfo != null)
            {
                if (!String.IsNullOrEmpty(scriptCommentInfo.Description) && !scriptCommentInfo.Description.Contains("<#") && !scriptCommentInfo.Description.Contains("#>"))
                {
                    parsedMetadata.Add("description", scriptCommentInfo.Description);
                }
                else
                {
                    var message = String.Format("PSScript is missing the required Description property or Description value contains '<#' or '#>' which is invalid");
                    var ex = new ArgumentException(message);
                    var psScriptMissingDescriptionOrInvalidPropertyError = new ErrorRecord(ex, "psScriptDescriptionMissingOrInvalidDescription", ErrorCategory.ParserError, null);
                    parseMetadataErrors.Add(psScriptMissingDescriptionOrInvalidPropertyError);
                    errors = parseMetadataErrors.ToArray();
                    return false;
                }
            }

            // Check that the mandatory properites for a script are there (version, author, guid, in addition to description)
            if (!parsedMetadata.ContainsKey("version") || String.IsNullOrWhiteSpace(parsedMetadata["version"].ToString()))
            {
                var message = "No version was provided in the script metadata. Script metadata must specify a version, author, description, and Guid.";
                var ex = new ArgumentException(message);
                var MissingVersionInScriptMetadataError = new ErrorRecord(ex, "MissingVersionInScriptMetadata", ErrorCategory.InvalidData, null);
                parseMetadataErrors.Add(MissingVersionInScriptMetadataError);
                errors = parseMetadataErrors.ToArray();
                return false;
            }

            if (!parsedMetadata.ContainsKey("author") || String.IsNullOrWhiteSpace(parsedMetadata["author"].ToString()))
            {
                var message = "No author was provided in the script metadata. Script metadata must specify a version, author, description, and Guid.";
                var ex = new ArgumentException(message);
                var MissingAuthorInScriptMetadataError = new ErrorRecord(ex, "MissingAuthorInScriptMetadata", ErrorCategory.InvalidData, null);
                parseMetadataErrors.Add(MissingAuthorInScriptMetadataError);
                errors = parseMetadataErrors.ToArray();
                return false;
            }

            if (!parsedMetadata.ContainsKey("guid") || String.IsNullOrWhiteSpace(parsedMetadata["guid"].ToString()))
            {
                var message = "No guid was provided in the script metadata. Script metadata must specify a version, author, description, and Guid.";
                var ex = new ArgumentException(message);
                var MissingGuidInScriptMetadataError = new ErrorRecord(ex, "MissingGuidInScriptMetadata", ErrorCategory.InvalidData, null);
                parseMetadataErrors.Add(MissingGuidInScriptMetadataError);
                errors = parseMetadataErrors.ToArray();
                return false;
            }

            errors = parseMetadataErrors.ToArray();
            return true;
        }

        private bool CheckDependenciesExist(Hashtable dependencies, string repositoryUri)
        {
            // Check to see that all dependencies are in the repository
            // Searches for each dependency in the repository the pkg is being pushed to,
            // If the dependency is not there, error
            foreach (var dependency in dependencies.Keys)
            {
                // Need to make individual calls since we're look for exact version numbers or ranges.
                var depName = new[] { (string)dependency };
                var depVersion = (string)dependencies[dependency];
                var type = new[] { "module", "script" };
                var repository = new[] { repositoryUri };

                // Search for and return the dependency if it's in the repository.
                // TODO: When find is complete, uncomment beginFindHelper method below  (resourceNameParameterHelper)
                //var dependencyFound = findHelper.beginFindHelper(depName, type, depVersion, true, null, null, repository, Credential, false, false);
                // TODO: update the type from PSObject to PSResourceInfo
                List<PSObject> dependencyFound = null;
                if (dependencyFound == null || !dependencyFound.Any())
                {
                    var message = String.Format("Dependency '{0}' was not found in repository '{1}'.  Make sure the dependency is published to the repository before publishing this module.", dependency, repositoryUri);
                    var ex = new ArgumentException(message);  // System.ArgumentException vs PSArgumentException
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

        private void PushNupkg(string outputNupkgDir, string repoName, string repoUri)
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
            ILogger log = new NuGetLogger();
            var success = true;
            try
            {
                PushRunner.Run(
                        settings: Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null),
                        sourceProvider: new PackageSourceProvider(settings),
                        packagePath: fullNupkgFile,
                        source: publishLocation,
                        apiKey: ApiKey,
                        symbolSource: null,
                        symbolApiKey: null,
                        timeoutSeconds: 0,
                        disableBuffering: false,
                        noSymbols: false,
                        noServiceEndpoint: false,  // enable server endpoint
                        skipDuplicate: false, // if true-- if a package and version already exists, skip it and continue with the next package in the push, if any.
                        logger: log // nuget logger
                        ).GetAwaiter().GetResult();
            }
            catch (HttpRequestException e)
            {
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
                        WriteError(ApiKeyError);
                    }
                    else
                    {
                        var Error401 = new ErrorRecord(ex, "401Error", ErrorCategory.PermissionDenied, null);
                        WriteError(Error401);
                    }
                }
                else if (e.Message.Contains("403"))
                {
                    var Error403 = new ErrorRecord(ex, "403Error", ErrorCategory.PermissionDenied, null);
                    WriteError(Error403);
                }
                else if (e.Message.Contains("409"))
                {
                    var Error409 = new ErrorRecord(ex, "409Error", ErrorCategory.PermissionDenied, null);
                    WriteError(Error409);
                }
                else
                {
                    var HTTPRequestError = new ErrorRecord(ex, "HTTPRequestError", ErrorCategory.PermissionDenied, null);
                    WriteError(HTTPRequestError);
                }

                success = false;
            }
            catch (Exception e)
            {
                var ex = new ArgumentException(e.Message);
                var PushNupkgError = new ErrorRecord(ex, "PushNupkgError", ErrorCategory.InvalidResult, null);
                WriteError(PushNupkgError);

                success = false;
            }

            if (success)
            {
                WriteVerbose(string.Format("Successfully published the resource to '{0}'", repoUri));
            }
            else
            {
                WriteVerbose(string.Format("Not able to publish resource to '{0}'", repoUri));
            }
        }
    }

    #endregion
}
