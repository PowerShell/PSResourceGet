// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Xml;
using System.Xml.Linq;
using Microsoft.PowerShell.PowerShellGet.RepositorySettings;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Versioning;


namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// Publishes a module, script, or nupkg to a designated repository.
    /// </summary>
    [Cmdlet(VerbsData.Publish, "PSResource", SupportsShouldProcess = true,
        HelpUri = "<add>", RemotingCapability = RemotingCapability.None)]
    public sealed
    class PublishPSResource : PSCmdlet
    {
        /// <summary>
        /// Specifies the API key that you want to use to publish a module to the online gallery.
        /// </summary>
        [Parameter()]
        [ValidateNotNullOrEmpty]
        public string APIKey
        {
            get
            { return _APIKey; }

            set
            { _APIKey = value; }
        }
        private string _APIKey;


        /// <summary>
        /// Specifies the repository to publish to.
        /// </summary>
        [Parameter()]
        [ValidateNotNullOrEmpty]
        public string Repository
        {
            get
            { return _repository; }

            set
            { _repository = value; }
        }
        private string _repository;


        /// <summary>
        /// Can be used to publish a nupkg locally.
        /// </summary>
        [Parameter()]
        [ValidateNotNullOrEmpty]
        public string DestinationPath
        {
            get
            { return _destinationPath; }

            set
            { _destinationPath =  SessionState.Path.GetResolvedPSPathFromPSPath(_destinationPath).First().Path; }
        }
        private string _destinationPath;


        /// <summary>
        /// Specifies the path to the resource that you want to publish. This parameter accepts the path to the folder that contains the resource.
        /// Specifies a path to one or more locations. Wildcards are permitted. The default location is the current directory (.).
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "PathParameterSet")]
        [ValidateNotNullOrEmpty]
        public string Path
        {
            get
            { return _path; }

            set
            {
                string resolvedPath = string.Empty;
                if (!string.IsNullOrEmpty(value))
                {
                    resolvedPath = SessionState.Path.GetResolvedPSPathFromPSPath(value).First().Path;
                }

                if (Directory.Exists(resolvedPath))
                {
                    _path = resolvedPath;
                }
                else if (File.Exists(resolvedPath) && resolvedPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
                {
                    isScript = true;
                    _path = resolvedPath;
                }
            }
        }
        private string _path;
        

        /// <summary>
        /// Specifies a path to one or more locations. Unlike the Path parameter, the value of the LiteralPath parameter is used exactly as entered.
        /// No characters are interpreted as wildcards. If the path includes escape characters, enclose them in single quotation marks.
        /// Single quotation marks tell PowerShell not to interpret any characters as escape sequences.
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "PathLiteralParameterSet")]
        [ValidateNotNullOrEmpty]
        public string LiteralPath
        {
            get
            { return _literalPath; }

            set
            {
                if (Directory.Exists(value))
                {
                    _literalPath = value;
                }
                else if (File.Exists(value) && value.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
                {
                    isScript = true;
                    _literalPath = value;
                }
            }
        }
        private string _literalPath;


        /// <summary>
        /// Specifies a user account that has rights to a specific repository (used for finding dependencies).
        /// </summary>
        [Parameter()]
        [ValidateNotNullOrEmpty]
        public PSCredential Credential
        {
            get
            { return _credential; }

            set
            { _credential = value; }
        }
        private PSCredential _credential;


        /// <summary>
        /// Bypasses the default check that all dependencies are present.
        /// </summary>
        [Parameter()]
        [ValidateNotNullOrEmpty]
        public SwitchParameter SkipDependenciesCheck
        {
            get
            { return _skipDependenciesCheck; }

            set
            { _skipDependenciesCheck = value; }
        }
        private bool _skipDependenciesCheck;


        /// <summary>
        ///  Updates nuspec: specifies a string containing release notes or comments that you want to be available to users of this version of the resource.
        /// </summary>
        [Parameter(ParameterSetName = "CreateNuspecParameterSet")]
        [Parameter(ParameterSetName = "PathParameterSet")]
        [Parameter(ParameterSetName = "PathLiteralParameterSet")]
        [ValidateNotNullOrEmpty]
        public string ReleaseNotes
        {
            get
            { return _releaseNotes; }

            set
            { _releaseNotes = value; }
        }
        private string _releaseNotes;


        /// <summary>
        ///  Updates nuspec: adds one or more tags to the resource that you are publishing.
        ///  Note-- this applies only to the nuspec.
        /// </summary>
        [Parameter(ParameterSetName = "CreateNuspecParameterSet")]
        [Parameter(ParameterSetName = "PathParameterSet")]
        [Parameter(ParameterSetName = "PathLiteralParameterSet")]
        [ValidateNotNullOrEmpty]
        public string[] Tags
        {
            get
            { return _tags; }

            set
            { _tags = value; }
        }
        private string[] _tags;


        /// <summary>
        ///  Updates nuspec: specifies the URL of licensing terms for the resource you want to publish.
        /// </summary>
        [Parameter(ParameterSetName = "CreateNuspecParameterSet")]
        [Parameter(ParameterSetName = "PathParameterSet")]
        [Parameter(ParameterSetName = "PathLiteralParameterSet")]
        [ValidateNotNullOrEmpty]
        public string LicenseUrl
        {
            get
            { return _licenseUrl; }

            set
            { _licenseUrl = value; }
        }
        private string _licenseUrl;


        /// <summary>
        ///  Updates nuspec: specifies the URL of an icon for the resource.
        /// </summary>
        [Parameter(ParameterSetName = "CreateNuspecParameterSet")]
        [Parameter(ParameterSetName = "PathParameterSet")]
        [Parameter(ParameterSetName = "PathLiteralParameterSet")]
        [ValidateNotNullOrEmpty]
        public string IconUrl
        {
            get
            { return _iconUrl; }

            set
            { _iconUrl = value; }
        }
        private string _iconUrl;


        /// <summary>
        /// Updates nuspec: specifies the URL of a webpage about this project.
        /// </summary>
        [Parameter(ParameterSetName = "CreateNuspecParameterSet")]
        [Parameter(ParameterSetName = "PathParameterSet")]
        [Parameter(ParameterSetName = "PathLiteralParameterSet")]
        [ValidateNotNullOrEmpty]
        public string ProjectUrl
        {
            get
            { return _projectUrl; }

            set
            { _projectUrl = value; }
        }
        private string _projectUrl;


        [Parameter(ParameterSetName = "ModuleNameParameterSet")]
        /// <summary>
        /// Excludes files from a nuspec
        /// </summary>
        [Parameter()]
        [ValidateNotNullOrEmpty]
        public string[] Exclude
        {
            get
            { return _exclude; }

            set
            { _exclude = value; }
        }
        private string[] _exclude = System.Array.Empty<string>();


        /// <summary>
        /// Specifies a nuspec file rather than relying on this module to produce one.
        /// </summary>
        [Parameter(ParameterSetName = "NuspecParameterSet")]
        [Parameter(ParameterSetName = "PathParameterSet")]
        [Parameter(ParameterSetName = "PathLiteralParameterSet")]
        [ValidateNotNullOrEmpty]
        public string Nuspec
        {
            get
            { return _nuspec; }

            set
            { _nuspec = value; }
        }
        private string _nuspec;


        /// <summary>
        /// Specifies a proxy server for the request, rather than a direct connection to the internet resource.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public Uri Proxy
        {
            get
            { return _proxy; }

            set
            { _proxy = value; }
        }
        private Uri _proxy;

        /// <summary>
        /// Specifies a user account that has permission to use the proxy server that is specified by the Proxy parameter.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        public PSCredential ProxyCredential
        {
            get
            { return _proxyCredential; }

            set
            { _proxyCredential = value; }
        }
        private PSCredential _proxyCredential;

        private NuGetVersion pkgVersion = null;
        private bool isScript;
        private string pkgName;

        private static char[] PathSeparators = new [] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar };

        protected override void ProcessRecord()
        {
            _path = string.IsNullOrEmpty(_path) ? _literalPath : _path;

            // Get the .psd1 file or .ps1 file
            // Returns the name of the file or the name of the directory, depending on path
            var pkgFileOrDir = new DirectoryInfo(_path);
            string moduleManifestOrScriptPath;
            if (isScript)
            {
                moduleManifestOrScriptPath = pkgFileOrDir.FullName;
                pkgName = pkgFileOrDir.Name.Remove(pkgFileOrDir.Name.Length - 4);
            }
            else { 
                moduleManifestOrScriptPath = System.IO.Path.Combine(_path, pkgFileOrDir.Name + ".psd1");
                // Validate that there's a module manifest 
                if (!File.Exists(moduleManifestOrScriptPath))
                {
                    var message = String.Format("No file with a .psd1 extension was found in {0}.  Please specify a path to a valid modulemanifest.", moduleManifestOrScriptPath);
                    var ex = new ArgumentException(message);
                    var moduleManifestNotFound = new ErrorRecord(ex, "moduleManifestNotFound", ErrorCategory.ObjectNotFound, null);

                    this.ThrowTerminatingError(moduleManifestNotFound);
                }
                pkgName = pkgFileOrDir.Name;
            }

            FileInfo moduleFileInfo;
            moduleFileInfo = new FileInfo(moduleManifestOrScriptPath);
            // if there's no specified destination path to publish the nupkg, we'll just create a temp folder and delete it later
            string outputDir = !string.IsNullOrEmpty(_destinationPath) ? _destinationPath : System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // if user does not specify that they want to use a nuspec they've created, we'll create a nuspec
            var dependencies = new Hashtable();
            if (string.IsNullOrEmpty(_nuspec))
            {
                _nuspec = createNuspec(outputDir, moduleFileInfo);
            }
            else
            {
                // Read the nuspec passed in to pull out the dependency information
                XDocument doc = XDocument.Load(_nuspec);

                // ex: <version>2.2.1</version>
                var versionNode = doc.Descendants("version");
                NuGetVersion.TryParse(versionNode.FirstOrDefault().Value, out NuGetVersion version);

                if (version == null)
                {
                    var message = "Version is not specified in the .nuspec provided. Please provide a valid version in the .nuspec.";
                    var ex = new ArgumentException(message);
                    var versionNotFound = new ErrorRecord(ex, "versionNotFound", ErrorCategory.NotSpecified, null);

                    this.ThrowTerminatingError(versionNotFound);
                }

                // ex: <dependency id="Carbon" version="2.9.2" /> 
                var dependencyNode = doc.Descendants("dependency");
                foreach (var dep in dependencyNode)
                { 
                    dependencies.Add(dep.Attribute("id"), dep.Attribute("version"));
                }
            }

            // find repository
            var r = new RespositorySettings();
            var repositoryUrl = r.Read(new[] { _repository });

            if (!repositoryUrl.Any())
            {
                var message = String.Format("The resource repository '{0}' is not a registered. Please run 'Register-PSResourceRepository' in order to publish to this repository.", _repository);
                var ex = new ArgumentException(message);
                var repositoryNotFound = new ErrorRecord(ex, "repositoryNotFound", ErrorCategory.ObjectNotFound, null);

                this.ThrowTerminatingError(repositoryNotFound);
            }

            if (!_skipDependenciesCheck)
            {
                // Check to see that all dependencies are in the repository 
                var findHelper = new FindHelper();

                foreach (var dependency in dependencies.Keys)
                {
                    // Need to make individual calls since we're look for exact version numbers or ranges.
                    var depName = new[] { (string)dependency };
                    var depVersion = (string)dependencies[dependency];
                    var type = new[] { "module", "script" };
                    var repository = new[] { _repository };

                    // Search for and return the dependency if it's in the repository.
                    var dependencyFound = findHelper.beginFindHelper(depName, type, depVersion, true, null, null, repository, _credential, false, false);

                    if (!dependencyFound.Any())
                    {
                        var message = String.Format("Dependency {0} was not found in repository {1}.  Make sure the dependency is published to the repository before publishing this module.", depName, _repository);
                        var ex = new ArgumentException(message);  // System.ArgumentException vs PSArgumentException
                        var dependencyNotFound = new ErrorRecord(ex, "DependencyNotFound", ErrorCategory.ObjectNotFound, null);

                        this.ThrowTerminatingError(dependencyNotFound);
                    }
                }
            }

            if (isScript)
            {
                File.Copy(_path, System.IO.Path.Combine(outputDir, pkgName + ".ps1"), true);
            }
            else 
            {
                // Create subdirectory structure in temp folder
                foreach (string dir in System.IO.Directory.GetDirectories(_path, "*", System.IO.SearchOption.AllDirectories))
                {
                    var dirName = dir.Substring(_path.Length).Trim(PathSeparators);
                    System.IO.Directory.CreateDirectory(System.IO.Path.Combine(outputDir, dirName));
                }
                // Copy files over to temp folder
                foreach (string fileNamePath in System.IO.Directory.GetFiles(_path, "*", System.IO.SearchOption.AllDirectories))
                {
                    var fileName = fileNamePath.Substring(_path.Length).Trim(PathSeparators);
                    System.IO.File.Copy(fileNamePath, System.IO.Path.Combine(outputDir, fileName));
                }
            }

            var outputDirectory = System.IO.Path.Combine(outputDir, "nupkg");
            // Pack the module or script into a nupkg given a nuspec.
            var builder = new PackageBuilder();
            var runner = new PackCommandRunner(
                    new PackArgs
                    {
                        CurrentDirectory = outputDir,
                        OutputDirectory = outputDirectory, 
                        Path = _nuspec,
                        Exclude = _exclude,
                        Symbols = false,
                        Logger = NullLogger.Instance
                    },
                    MSBuildProjectFactory.ProjectCreator,
                    builder);

            runner.BuildPackage();

            
            // Push the nupkg to the appropriate repository 
            // Pkg version is parsed from .ps1 file or .psd1 file 
            var fullNupkgPath = System.IO.Path.Combine(outputDirectory, pkgName + "." + pkgVersion.ToNormalizedString() + ".nupkg" );

            var repoURL = repositoryUrl.First().Properties["Url"].Value.ToString();
            var publishLocation = repoURL.EndsWith("/v2", StringComparison.OrdinalIgnoreCase) ? repoURL + "/package" : repoURL;

            var settings = NuGet.Configuration.Settings.LoadDefaultSettings(null, null, null);
            NuGet.Common.ILogger log = new NuGetLogger();
            PushRunner.Run(
                    Settings.LoadDefaultSettings(root:null, configFileName:null, machineWideSettings:null),
                    new PackageSourceProvider(settings), 
                    fullNupkgPath,
                    publishLocation, 
                    _APIKey, // api key
                    null, // symbols source
                    null, // symbols api key
                    0, // timeout
                    false, // disable buffering
                    false, // no symbols
                           // Skip duplicate: if a package and version already exists, skip it and continue with the next package in the push, if any.
                    false, // no skip duplicate  
                    false, // enable server endpoint
                    log).GetAwaiter().GetResult();
        }


        private string createNuspec(string outputDir, FileInfo moduleFileInfo)
        {
            WriteVerbose("Creating new nuspec file.");
            Hashtable parsedMetadataHash = new Hashtable();
            
            if (moduleFileInfo.Extension.Equals(".psd1", StringComparison.OrdinalIgnoreCase))
            {
                System.Management.Automation.Language.Token[] tokens;
                ParseError[] errors;
                var ast = Parser.ParseFile(moduleFileInfo.FullName, out tokens, out errors);

                if (errors.Length > 0)
                {
                    var message = String.Format("Could not parse '{0}' as a PowerShell data file.", moduleFileInfo.FullName);
                    var ex = new ArgumentException(message);
                    var psdataParseError = new ErrorRecord(ex, "psdataParseError", ErrorCategory.ParserError, null);

                    this.ThrowTerminatingError(psdataParseError);
                }
                else
                {
                    var data = ast.Find(a => a is HashtableAst, false);
                    if (data != null)
                    {
                        parsedMetadataHash = (Hashtable) data.SafeGetValue();
                    }
                    else
                    {
                        var message = String.Format("Could not parse as PowerShell data file-- no hashtable root for file '{0}'", moduleFileInfo.FullName);
                        var ex = new ArgumentException(message);
                        var psdataParseError = new ErrorRecord(ex, "psdataParseError", ErrorCategory.ParserError, null);

                        this.ThrowTerminatingError(psdataParseError);
                    }
                }
            }
            else if (moduleFileInfo.Extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase))
            {
                ParseScriptMetadata(parsedMetadataHash, moduleFileInfo);
            }

            /// now we have parsedMetadatahash to fill out the nuspec information
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
            metadataElementsDictionary.Add("id", pkgName);

            string version = String.Empty;
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

                this.ThrowTerminatingError(NoVersionFound);
            }

            // Look for Prerelease tag
            if (parsedMetadataHash.ContainsKey("PrivateData"))
            {
                if (parsedMetadataHash["PrivateData"] is Hashtable privateData &&
                    privateData.ContainsKey("PSData"))
                {
                    if (privateData["PSData"] is Hashtable psData &&
                        psData.ContainsKey("Prerelease"))
                    {
                        if (psData["Prerelease"] is string preReleaseVersion)
                        {
                            version = string.Format(@"{0}-{1}", version, preReleaseVersion);
                        }
                    }
                }
            }

            NuGetVersion.TryParse(version, out pkgVersion);

            metadataElementsDictionary.Add("version", pkgVersion.ToNormalizedString());


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

           if (parsedMetadataHash.ContainsKey("releasenotes") || !String.IsNullOrEmpty(_releaseNotes))
            {
                var releaseNotes = string.IsNullOrEmpty(_releaseNotes) ? parsedMetadataHash["releasenotes"].ToString().Trim() : _releaseNotes;
                metadataElementsDictionary.Add("releaseNotes", releaseNotes);
            }

            if (parsedMetadataHash.ContainsKey("copyright"))
            {
                metadataElementsDictionary.Add("copyright", parsedMetadataHash["copyright"].ToString().Trim());
            }

            string tags = string.Empty;
            if (parsedMetadataHash.ContainsKey("tags") || _tags != null)
            {
                tags = _tags == null ? (parsedMetadataHash["tags"].ToString().Trim() + " ") : (_tags.ToString().Trim() + " ");
            }
            tags += moduleFileInfo.Extension.Equals(".psd1", StringComparison.OrdinalIgnoreCase) ? "PSModule" : "PSScript";
            metadataElementsDictionary.Add("tags", tags);

            if (parsedMetadataHash.ContainsKey("licenseurl") || !String.IsNullOrEmpty(_licenseUrl))
            {
                var licenseUrl = string.IsNullOrEmpty(_licenseUrl) ? parsedMetadataHash["licenseurl"].ToString().Trim() : _licenseUrl;
                metadataElementsDictionary.Add("licenseUrl", licenseUrl);
            }

            if (parsedMetadataHash.ContainsKey("projecturl") || !String.IsNullOrEmpty(_projectUrl))
            {
                var projectUrl = string.IsNullOrEmpty(_projectUrl) ? parsedMetadataHash["projecturl"].ToString().Trim() : _projectUrl;
                metadataElementsDictionary.Add("projectUrl", projectUrl);
            }

            if (parsedMetadataHash.ContainsKey("iconurl") || !String.IsNullOrEmpty(_iconUrl))
            {
                var iconUrl = string.IsNullOrEmpty(_iconUrl) ? parsedMetadataHash["iconurl"].ToString().Trim() : _iconUrl;
                metadataElementsDictionary.Add("iconUrl", iconUrl);
            }


            foreach (var key in metadataElementsDictionary.Keys)
            {
                XmlElement element = doc.CreateElement(key, nameSpaceUri);

                string elementInnerText;
                metadataElementsDictionary.TryGetValue(key, out elementInnerText);
                element.InnerText = elementInnerText;

                metadataElement.AppendChild(element);
            }

            var requiredModules = ParseRequiredModules(parsedMetadataHash);
            if (requiredModules != null)
            {
                XmlElement dependenciesElement = doc.CreateElement("dependencies", nameSpaceUri);

                foreach (Hashtable dependency in requiredModules)
                {
                    XmlElement element = doc.CreateElement("dependency", nameSpaceUri);

                    element.SetAttribute("id", dependency["ModuleName"].ToString());
                    if (!string.IsNullOrEmpty(dependency["ModuleVersion"].ToString()))
                    {
                        element.SetAttribute("version", dependency["ModuleVersion"].ToString());
                    }

                    dependenciesElement.AppendChild(element);
                }
                metadataElement.AppendChild(dependenciesElement);
            }
            
            packageElement.AppendChild(metadataElement);
            doc.AppendChild(packageElement);

            var nuspecFullName = System.IO.Path.Combine(outputDir, pkgName + ".nuspec");
            doc.Save(nuspecFullName);

            this.WriteVerbose("The newly created nuspec is: " + nuspecFullName);

            return nuspecFullName;
        }

        private Hashtable[] ParseRequiredModules(Hashtable parsedMetadataHash)
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

            if (LanguagePrimitives.TryConvertTo<Hashtable[]>(requiredModules, out Hashtable[] moduleList))
            {
                return moduleList;
            }

            if (LanguagePrimitives.TryConvertTo<string[]>(requiredModules, out string[] moduleNames))
            {
                var listHashtable = new List<Hashtable>();
                foreach (var modName in moduleNames)
                {
                    listHashtable.Add(
                        new Hashtable() {
                            { "ModuleName", modName },
                            { "ModuleVersion", string.Empty }
                        });
                }

                return listHashtable.ToArray();
            }

            return null;
        }

        private void ParseScriptMetadata(Hashtable parsedMetadataHash, FileInfo moduleFileInfo)
        {
            // parse .ps1 - example .ps1 metadata:
            /* <#PSScriptInfo
                .VERSION 1.6
                .GUID abf490023 - 9128 - 4323 - sdf9a - jf209888ajkl
                .AUTHOR Jane Doe
                .COMPANYNAME Microsoft
                .COPYRIGHT
                .TAGS Windows MacOS 
                #>

                <#
                .SYNOPSIS
                 Synopsis description here
                .DESCRIPTION
                 Description here
                .PARAMETER Name
                .EXAMPLE
                 Example cmdlet here
                #>
            */

            using (StreamReader sr = File.OpenText(moduleFileInfo.FullName))
            {
                string endOfMetadata = "#>";

                // metadata for scripts are divided into two parts
                string str = String.Empty;

                // read until the beginning of the metadata is hit "<#PSScriptInfo"
                do
                {
                    str = sr.ReadLine();
                }
                while (str != null && !string.Equals(str.Trim(), "<#PSScriptInfo", StringComparison.OrdinalIgnoreCase));

                string key = String.Empty;
                string value;
                // Then start reading metadata
                do
                {
                    str = sr.ReadLine();
                    value = String.Empty;

                    if (str != null && str.StartsWith(".", StringComparison.OrdinalIgnoreCase))
                    {
                        // Create new key
                        if (str.IndexOf(" ") > 0)
                        {
                            key = str.Substring(1, str.IndexOf(" ") - 1).ToLower();
                            var startIndex = str.IndexOf(" ") + 1;
                            value = str.Substring(startIndex, str.Length - startIndex);
                        }
                        else
                        {
                            key = str.Substring(1, str.Length - 1).ToLower();
                        }

                        try
                        {
                            parsedMetadataHash.Add(key, value);
                        }
                        catch (Exception e)
                        {
                            var message = String.Format("Failed to add key '{0}' and value '{1}' to hashtable", key, value);
                            var ex = new ArgumentException(message);
                            var metadataCannotBeAdded = new ErrorRecord(ex, "metadataCannotBeAdded", ErrorCategory.MetadataError, null);

                            this.ThrowTerminatingError(metadataCannotBeAdded);
                        }
                    }
                    else
                    {
                        if (!String.IsNullOrEmpty(key))
                        {
                            // Append to existing key/value
                            parsedMetadataHash[key] = parsedMetadataHash[key] + " " + str;
                        }
                    }
                }
                while (str != null && str.Trim() != endOfMetadata);

                // Read until the beginning of the next metadata section
                // Note there may only be one metadata section
                try
                {
                    do
                    {
                        str = sr.ReadLine();
                    }
                    while (str != null && str.Trim() != "<#");
                }
                catch
                {
                    var message = "Error parsing metadata for script.";
                    var ex = new ArgumentException(message);
                    var errorParsingScriptMetadata = new ErrorRecord(ex, "errorParsingScriptMetadata", ErrorCategory.ParserError, null);

                    this.ThrowTerminatingError(errorParsingScriptMetadata);
                }

                // Then start reading metadata again.
                str = String.Empty;
                key = String.Empty;

                try
                {
                    do
                    {
                        str = sr.ReadLine();
                        value = String.Empty;

                        if (str != null && str.StartsWith(".", StringComparison.OrdinalIgnoreCase))
                        {
                            // create new key
                            if (str.IndexOf(" ") > 0)
                            {
                                key = str.Substring(1, str.IndexOf(" ") - 1).ToLower();
                                var startIndex = str.IndexOf(" ") + 1;
                                value = str.Substring(startIndex, str.Length - startIndex);
                            }
                            else
                            {
                                key = str.Substring(1, str.Length - 1).ToLower();
                            }

                            try
                            {
                                parsedMetadataHash.Add(key, value);
                            }
                            catch
                            {
                                var message = String.Format("Failed to add key '{0}' and value '{1}' to hashtable", key, value);
                                var ex = new ArgumentException(message);
                                var errorParsingScriptMetadata = new ErrorRecord(ex, "errorParsing", ErrorCategory.ParserError, null);

                                this.ThrowTerminatingError(errorParsingScriptMetadata);
                            }
                        }
                        else
                        {
                            // append to existing key/value
                            if (!String.IsNullOrEmpty(key))
                            {
                                parsedMetadataHash[key] = parsedMetadataHash[key] + " " + str;
                            }
                        }
                    }
                    while (str != null && str.Trim() != endOfMetadata);
                }
                catch
                {
                    var message = "Error parsing metadata for script";
                    var ex = new ArgumentException(message);
                    var errorParsingScriptMetadata = new ErrorRecord(ex, "errorParsingScriptMetadata", ErrorCategory.ParserError, null);

                    this.ThrowTerminatingError(errorParsingScriptMetadata);
                }
            }
        }
    }
}
