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
using Microsoft.Extensions.Logging;
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
            {
                string resolvedPath = "";
                if (!string.IsNullOrEmpty(_path))
                {
                    resolvedPath = SessionState.Path.GetResolvedPSPathFromPSPath(_path).FirstOrDefault().Path;
                }

                // Get the .psd1 file or .ps1 file
                dirName = new DirectoryInfo(resolvedPath).Name;
                if (File.Exists(System.IO.Path.Combine(resolvedPath, dirName + ".psd1")))
                {
                    // Pkg to publish is a module
                    moduleFileInfo = new FileInfo(System.IO.Path.Combine(resolvedPath, dirName + ".psd1"));
                }
                else if (File.Exists(resolvedPath) && resolvedPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
                {
                    // Pkg to publish is a script
                    moduleFileInfo = new FileInfo(resolvedPath);
                    isScript = true;
                }
                _path = resolvedPath;
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
                // Get the .psd1 file or .ps1 file
                dirName = new DirectoryInfo(value).Name;
                if (File.Exists(System.IO.Path.Combine(value, dirName + ".psd1")))
                {
                    // Pkg to publish is a module
                    moduleFileInfo = new FileInfo(System.IO.Path.Combine(value, dirName + ".psd1"));
                }
                else if (File.Exists(value) && value.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
                {
                    // Pkg to publish is a script
                    moduleFileInfo = new FileInfo(value);
                    isScript = true;
                }
                _literalPath = value;
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
        bool isScript;
        FileInfo moduleFileInfo;
        string dirName;

        /// <summary>
        /// </summary>
        protected override void ProcessRecord()
        {
            var resolvedPath = !string.IsNullOrEmpty(_path) ? _path : _literalPath;

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
            }

            // if user does not specify that they want to use a nuspec they've created, we'll create a nuspec
            if (string.IsNullOrEmpty(_nuspec))
            {
                _nuspec = createNuspec(outputDir, moduleFileInfo, dirName);
            }
            else
            {
                // Read the nuspec passed in to pull out the dependency information
                XDocument doc = XDocument.Load(_nuspec);

                // ex: <version>2.2.1</version>
                var versionNode = doc.Descendants("version");
                NuGetVersion version;
                NuGetVersion.TryParse(versionNode.FirstOrDefault().Value, out version);

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

            var pkgName = dirName.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) ? dirName.Remove(dirName.Length - 4) : dirName;
            if (isScript)
            {
                File.Copy(resolvedPath, System.IO.Path.Combine(outputDir, pkgName + ".ps1"), true);
            }
            else 
            {
                // copy the directory into the temp folder
                foreach (string newPath in Directory.GetFiles(resolvedPath, "*.*", SearchOption.AllDirectories))
                {
                    File.Copy(newPath, newPath.Replace(resolvedPath, outputDir), true);
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

            // to get the pkg version we need to open the .ps1 file or .psd1 file and parse out the version 
            // if .ps1 file, version gets parsed already
            // if .psd1 file and we create nuspec, version gets parsed already,

            var fullNupkgPath = System.IO.Path.Combine(outputDirectory, pkgName + "." + pkgVersion.ToNormalizedString() + ".nupkg" );

            var repoURL = repositoryUrl.FirstOrDefault().Properties["Url"].Value.ToString();
            var publishLocation = repoURL.EndsWith("/v2", StringComparison.OrdinalIgnoreCase) ? repoURL + "/package" : repoURL;

            var settings = NuGet.Configuration.Settings.LoadDefaultSettings(null, null, null);
            NuGet.Common.ILogger log = new NuGetLogger();
            PushRunner.Run(
                    Settings.LoadDefaultSettings(null, null, null),
                    new PackageSourceProvider(settings), 
                    fullNupkgPath,
                    publishLocation, 
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


        private string createNuspec(string outputDir, FileInfo moduleFileInfo, string pkgName)
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
                    WriteDebug("Could not parse '" + moduleFileInfo.FullName + "' as a powershell data file.");
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
                        WriteDebug("Could not parse as PowerShell data file-- no hashtable root for file '" + moduleFileInfo.FullName + "'");
                    }
                }
            }
            else if (moduleFileInfo.Extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase))
            {
                // parse script metadata
                ParseScriptMetadata(parsedMetadataHash, moduleFileInfo);
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
            }
            else if (parsedMetadataHash.ContainsKey("version"))
            {
                version = parsedMetadataHash["version"].ToString();
            }
            else 
            {
                // no version is specified for the nuspec
                var message = "There is no package version specified. Please specify a verison before publishing.";
                var ex = new ArgumentException(message);  
                var NoVersionFound = new ErrorRecord(ex, "NoVersionFound", ErrorCategory.InvalidArgument, null);

                this.ThrowTerminatingError(NoVersionFound);
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
                    str = sr.ReadLine().Trim();
                }
                while (!string.Equals(str, "<#PSScriptInfo", StringComparison.OrdinalIgnoreCase));

                string key = String.Empty;
                string value;
                // Then start reading metadata
                do
                {
                    str = sr.ReadLine().Trim();
                    value = String.Empty;

                    if (str.StartsWith(".", StringComparison.OrdinalIgnoreCase))
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
                try
                {
                    do
                    {
                        str = sr.ReadLine().Trim();
                    }
                    while (str != "<#");
                }
                catch
                {
                    WriteDebug("Error parsing metadata for script.");
                }

                // Then start reading metadata again.
                str = String.Empty;
                key = String.Empty;

                try
                {
                    do
                    {
                        str = sr.ReadLine().Trim();
                        value = String.Empty;

                        if (str.StartsWith(".", StringComparison.OrdinalIgnoreCase))
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
                catch
                {
                    WriteDebug("Error parsing metadata for script.");
                }
            }
        }
    }
}