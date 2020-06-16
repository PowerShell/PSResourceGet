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
        /// Specifies the name of the resource to be published.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public string Name
        {
            get
            { return _name; }

            set
            { _name = value; }
        }
        private string _name;


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
        /// Can be used to publish the a nupkg locally.
        /// </summary>
        [Parameter()]
        [ValidateNotNullOrEmpty]
        public string DestinationPath
        {
            get
            { return _destinationPath; }

            set
            { _destinationPath = value; }
        }
        private string _destinationPath;


        /// <summary>
        /// Specifies the path to the resource that you want to publish. This parameter accepts the path to the folder that contains the resource.
        /// Specifies a path to one or more locations. Wildcards are permitted. The default location is the current directory (.).
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "PathsParameterSet")]
        [ValidateNotNullOrEmpty]
        public string Path
        {
            get
            { return _path; }

            set
            { _path = value; }
        }
        private string _path;
        

        /// <summary>
        /// Specifies a path to one or more locations. Unlike the Path parameter, the value of the LiteralPath parameter is used exactly as entered.
        /// No characters are interpreted as wildcards. If the path includes escape characters, enclose them in single quotation marks.
        /// Single quotation marks tell PowerShell not to interpret any characters as escape sequences.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "PathLiteralParameterSet")]
        [ValidateNotNullOrEmpty]
        public string LiteralPath
        {
         get
         { return _literalPath; }

         set
         { _literalPath = value; }
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
        ///  Specifies a string containing release notes or comments that you want to be available to users of this version of the resource.
        ///  Note-- this applies only to the nuspec.
        /// </summary>
        [Parameter()]
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
        ///  Adds one or more tags to the resource that you are publishing.
        ///  Note-- this applies only to the nuspec.
        /// </summary>
        [Parameter()]
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
        ///  Specifies the URL of licensing terms for the resource you want to publish.
        ///  Note-- this applies only to the nuspec.
        /// </summary>
        [Parameter()]
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
        ///  Specifies the URL of an icon for the resource.
        ///  Note-- this applies only to the nuspec.
        /// </summary>
        [Parameter()]
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
        /// Specifies the URL of a webpage about this project.
        /// Note-- this applies only to the nuspec.
        /// </summary>
        [Parameter()]
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
        [Parameter()]
        [ValidateNotNullOrEmpty]
        public string Nuspec
        {
            get
            { return _nuspec; }

            set
            { _nuspec = value; }
        }
        private string _nuspec;


        Hashtable dependencies = new Hashtable();
        NuGetVersion pkgVersion = null;

        /// <summary>
        /// </summary>
        protected override void ProcessRecord()
        {
            string resolvedPath = "";
            // come back here
            if (!string.IsNullOrEmpty(_name))
            {
                // Name param set - returns path to the module or script
                resolvedPath = publishNameParamSet();
            }
            else {
                // LiteralPath or Path parameter set - returns path to the module or script
                if (!string.IsNullOrEmpty(_literalPath))
                {
                    resolvedPath = _literalPath;
                }
                else if (!string.IsNullOrEmpty(_path))
                { 
                    resolvedPath = SessionState.Path.GetResolvedPSPathFromPSPath(_path).FirstOrDefault().Path;
                }

                if (string.IsNullOrEmpty(resolvedPath))
                {
                    var message = String.Format("The path {0} could not be resolved. Please provide a valid path.", resolvedPath);
                    var ex = new ArgumentException(message);  // System.ArgumentException vs PSArgumentException
                    var PathNotFound = new ErrorRecord(ex, "PathNotFound", ErrorCategory.ResourceUnavailable, null);

                    this.ThrowTerminatingError(PathNotFound);
                }
            }

            // Get the .psd1 file or .ps1 file
            string dirName = new DirectoryInfo(resolvedPath).Name;

            string moduleFile = "";
            if (File.Exists(System.IO.Path.Combine(resolvedPath, dirName + ".psd1")))
            {
                // Pkg to publish is a module
                moduleFile = System.IO.Path.Combine(resolvedPath, dirName + ".psd1");
            }
            else if (File.Exists(resolvedPath) && resolvedPath.EndsWith(".ps1"))
            {
                // Pkg to publish is a script
                moduleFile = resolvedPath;
            }
            else {
                var message = String.Format("A .psd1 file or .ps1 file does not exist in the path '{0}'.", resolvedPath);
                var ex = new ArgumentException(message);  // System.ArgumentException vs PSArgumentException
                var PathNotFound = new ErrorRecord(ex, "PathNotFound", ErrorCategory.ResourceUnavailable, null);

                this.ThrowTerminatingError(PathNotFound);
            }


            string outputDir = "";
            // if there's no specified destination path to publish the nupkg, we'll just create a temp folder and delete it later
            if (!string.IsNullOrEmpty(_destinationPath))
            {
                outputDir = SessionState.Path.GetResolvedPSPathFromPSPath(_destinationPath).FirstOrDefault().Path;
            }
            else
            {
                // create temp directory 
                outputDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
                var dir = Directory.CreateDirectory(outputDir);
            }

            // if user does not specify that they want to use a nuspec they've created, we'll create a nuspec
            if (string.IsNullOrEmpty(_nuspec))
            {
                _nuspec = createNuspec(outputDir, moduleFile, dirName);
            }
            else
            {
                // Read the nuspec passed in to pull out the dependency information
                using (StreamReader sr = File.OpenText(_nuspec))
                {
                    string str = String.Empty;

                    // read until the beginning of the dependency metadata is hit
                    while ((str = sr.ReadLine()) != null)
                    {
                        if (str.Trim().StartsWith("<version>"))
                        {
                            // ex: <version>2.2.1</version>
                            var splitStr = str.Split('<','>');

                            NuGetVersion.TryParse(splitStr[2], out pkgVersion);
                        }
                        if (str.Trim().StartsWith("<dependency "))
                        {
                            // ex: <dependency id="Carbon" version="2.9.2" /> 
                            var splitStr = str.Split('"');

                            var moduleName = splitStr[1];
                            var moduleVersion = splitStr[3];

                            dependencies.Add(moduleName, moduleVersion);
                        }
                    }
                }
            }

            // find repository
            var r = new RespositorySettings();
            var repositoryUrl = r.Read(new[] { _repository });

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

                        var dependencyNotFound = new ErrorRecord(ex, "DependencyNotFound", ErrorCategory.ResourceUnavailable, null);

                        this.ThrowTerminatingError(dependencyNotFound);
                    }
                }
            }


            // Pack the module or script into a nupkg given a nuspec.
            var builder = new PackageBuilder();
                var runner = new PackCommandRunner(
                    new PackArgs
                    {
                        CurrentDirectory = resolvedPath,
                        OutputDirectory = outputDir, 
                        Path = _nuspec,
                        Exclude = _exclude,
                        Symbols = false,
                        Logger = NullLogger.Instance
                    },
                    MSBuildProjectFactory.ProjectCreator,
                    builder);

            runner.BuildPackage();


            // Push the nupkg to the appropriate repository 
            var pkgName = dirName.EndsWith(".ps1") ? dirName.Remove(dirName.Length - 4) : dirName;
            // to get the pkg version we need to open the .ps1 file or .psd1 file and parse out the version 
            // if .ps1 file, version gets parsed already
            // if .psd1 file and we create nuspec, version gets parsed already,

            var fullNupkgPath = System.IO.Path.Combine(outputDir, pkgName + pkgVersion + ".nupkg" );


            var settings = NuGet.Configuration.Settings.LoadDefaultSettings(null, null, null);
            ILogger log = new TestLogger();
            PushRunner.Run(
                    Settings.LoadDefaultSettings(null, null, null),
                    new PackageSourceProvider(settings), 
                    fullNupkgPath,
                    repositoryUrl.FirstOrDefault().Properties["Url"].Value.ToString(), 
                    _APIKey, // api key
                    null, // symbols source
                    null, // symbols api key
                    0, // timeout
                    false, // disable buffering
                    false, // no symbols,
                    false, // no skip duplicate
                    false, // enable server endpoint
                    log).GetAwaiter().GetResult();
        }


        private string publishNameParamSet()
        {
            var modulePath = "";
            // 1) run get-module to find the right package version
            // _name, _requiredVersion, _prerelease


            NuGetVersion nugetVersion;
            /*
            // parse version,
            // throw if version is in incorrect format
            if (!string.IsNullOrEmpty(_requiredVersion))
            {
                // check if exact version

                //VersionRange versionRange = VersionRange.Parse(version);
                NuGetVersion.TryParse(_requiredVersion, out nugetVersion);

                // if the version didn't parse correctly, throw error 
                if (string.IsNullOrEmpty(nugetVersion.ToNormalizedString()))
                {
                    var message = $"-RequiredVersion is not in a proper package version.  Please specify a version number, for example: '2.0.0'";
                    var ex = new ArgumentException(message);  // System.ArgumentException vs PSArgumentException

                    var requiredVersionError = new ErrorRecord(ex, "RequiredVersionError", ErrorCategory.InvalidArgument, null);

                    this.ThrowTerminatingError(requiredVersionError);
                }
            }

            if (_prerelease)
            { 
                // add prerelease flag    
            }
            */

            // return the full path 
            return modulePath;
        }


        private string createNuspec(string outputDir, string moduleFile, string pkgName)
        {
            WriteVerbose("Creating new nuspec file.");

            Hashtable parsedMetadataHash = new Hashtable();

            if (moduleFile.EndsWith(".psd1"))
            {
                System.Management.Automation.Language.Token[] tokens;
                ParseError[] errors;
                var ast = Parser.ParseFile(moduleFile, out tokens, out errors);

                if (errors.Length > 0)
                {
                    WriteDebug("Could not parse '" + moduleFile + "' as a powershell data file.");
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
                        WriteDebug("Could not parse as PowerShell data file-- no hashtable root for file '" + moduleFile + "'");
                    }
                }
            }
            else if (moduleFile.EndsWith(".ps1"))
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

                pkgName = pkgName.Remove(pkgName.Length - 4);

                using (StreamReader sr = File.OpenText(moduleFile))
                {
                    string endOfMetadata = "#>";

                    // metadata for scripts are divided into two parts
                    string str = String.Empty;

                    // read until the beginning of the metadata is hit "<#PSScriptInfo"
                    do
                    {
                        str = sr.ReadLine().Trim();
                    }
                    while (str != "<#PSScriptInfo");

                    string key = String.Empty;
                    string value;
                    // Then start reading metadata
                    do
                    {
                        str = sr.ReadLine().Trim();
                        value = String.Empty;

                        if (str.StartsWith("."))
                        {
                            // Create new key
                            if (str.IndexOf(" ") > 0)
                            {
                                key = str.Substring(1, str.IndexOf(" ")-1).ToLower();
                                var startIndex = str.IndexOf(" ") + 1;
                                value = str.Substring(startIndex, str.Length - startIndex);
                            }
                            else { 
                                key = str.Substring(1, str.Length-1).ToLower();
                            }
                            

                            try
                            {
                                parsedMetadataHash.Add(key, value);
                            }
                            catch (Exception e)
                            {
                                WriteDebug(String.Format("Failed to add key '{0}' and value '{1}' to hashtable", key, value));
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
                    while (str != endOfMetadata);

                    // Read until the beginning of the next metadata section
                    // Note there may only be one metadata section
                    do
                    {
                        str = sr.ReadLine().Trim();
                    }
                    while (str != "<#");

                    // Then start reading metadata again.
                    str = String.Empty;
                    key = String.Empty;

                    do
                    {
                        str = sr.ReadLine().Trim();
                        value = String.Empty;

                        if (str.StartsWith("."))
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
                            catch (Exception e)
                            {
                                WriteDebug(String.Format("Failed to add key '{0}' and value '{1}' to hashtable", key, value));
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
                    while (str != endOfMetadata);                    
                }
            }
            else {
                WriteDebug("File to be parsed does not have a .psd1 or .ps1 extension.");
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
                NuGetVersion.TryParse(version, out pkgVersion);
            }
            else if (parsedMetadataHash.ContainsKey("version"))
            {
                version = parsedMetadataHash["version"].ToString();
                NuGetVersion.TryParse(version, out pkgVersion);
            }
            else 
            {
                // no version is specified for the nuspec
                var message = String.Format("There is no package version specified. Please specify a verison before publishing.");
                var ex = new ArgumentException(message);  
                var NoVersionFound = new ErrorRecord(ex, "NoVersionFound", ErrorCategory.InvalidArgument, null);

                this.ThrowTerminatingError(NoVersionFound);
            }
            NuGetVersion nugetVersion;
            NuGetVersion.TryParse(version, out nugetVersion);

            metadataElementsDictionary.Add("version", nugetVersion.ToFullString());


            if (parsedMetadataHash.ContainsKey("author"))
            {
                metadataElementsDictionary.Add("author", parsedMetadataHash["author"].ToString().Trim());
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
                tags = _tags == null ? (parsedMetadataHash["tags"].ToString().Trim() + " ") : (_tags.ToString().Trim() + " ");   ///////////    ??????????
            }
            tags += moduleFile.EndsWith(".psd1") ? "PSModule" : "PSScript";
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

            if (parsedMetadataHash.ContainsKey("requiredmodules"))
            {
                dependencies = (Hashtable)parsedMetadataHash["requiredmodules"];

                if (dependencies != null)
                {
                    XmlElement dependenciesElement = doc.CreateElement("dependencies", nameSpaceUri);

                    foreach (Hashtable dependency in dependencies)
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
            }
            
            packageElement.AppendChild(metadataElement);
            
            doc.AppendChild(packageElement);


            var nuspecFullName = System.IO.Path.Combine(outputDir, pkgName + ".nuspec");
            doc.Save(nuspecFullName);

            this.WriteVerbose("The newly created nuspec is: " + nuspecFullName);

            return nuspecFullName;
        }
    }
}