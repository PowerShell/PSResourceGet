// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Net.Http;
using System.Xml;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;
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
        #region Parameters

        /// <summary>
        /// Specifies the API key that you want to use to publish a module to the online gallery.
        /// </summary>
        [Parameter()]
        [ValidateNotNullOrEmpty]
        public string APIKey { get; set; }

        /// <summary>
        /// Specifies the repository to publish to.
        /// </summary>
        [Parameter()]
        [ValidateNotNullOrEmpty]
        public string Repository { get; set; }

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
            { _destinationPath =  SessionState.Path.GetResolvedPSPathFromPSPath(value).First().Path; }
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
        public PSCredential Credential { get; set; }

        /// <summary>
        /// Bypasses the default check that all dependencies are present.
        /// </summary>
        [Parameter()]
        [ValidateNotNullOrEmpty]
        public SwitchParameter SkipDependenciesCheck { get; set; }

        /// <summary>
        ///  Updates nuspec: specifies a string containing release notes or comments that you want to be available to users of this version of the resource.
        /// </summary>
        [Parameter(ParameterSetName = "CreateNuspecParameterSet")]
        [Parameter(ParameterSetName = "PathParameterSet")]
        [Parameter(ParameterSetName = "PathLiteralParameterSet")]
        [ValidateNotNullOrEmpty]
        public string ReleaseNotes { get; set; }

        /// <summary>
        ///  Updates nuspec: adds one or more tags to the resource that you are publishing.
        ///  Note-- this applies only to the nuspec.
        /// </summary>
        [Parameter(ParameterSetName = "CreateNuspecParameterSet")]
        [Parameter(ParameterSetName = "PathParameterSet")]
        [Parameter(ParameterSetName = "PathLiteralParameterSet")]
        [ValidateNotNullOrEmpty]
        public string[] Tags { get; set; }

        /// <summary>
        ///  Updates nuspec: specifies the URL of licensing terms for the resource you want to publish.
        /// </summary>
        [Parameter(ParameterSetName = "CreateNuspecParameterSet")]
        [Parameter(ParameterSetName = "PathParameterSet")]
        [Parameter(ParameterSetName = "PathLiteralParameterSet")]
        [ValidateNotNullOrEmpty]
        public string LicenseUrl { get; set; }

        /// <summary>
        ///  Updates nuspec: specifies the URL of an icon for the resource.
        /// </summary>
        [Parameter(ParameterSetName = "CreateNuspecParameterSet")]
        [Parameter(ParameterSetName = "PathParameterSet")]
        [Parameter(ParameterSetName = "PathLiteralParameterSet")]
        [ValidateNotNullOrEmpty]
        public string IconUrl { get; set; }

        /// <summary>
        /// Updates nuspec: specifies the URL of a webpage about this project.
        /// </summary>
        [Parameter(ParameterSetName = "CreateNuspecParameterSet")]
        [Parameter(ParameterSetName = "PathParameterSet")]
        [Parameter(ParameterSetName = "PathLiteralParameterSet")]
        [ValidateNotNullOrEmpty]
        public string ProjectUrl { get; set; }

        [Parameter(ParameterSetName = "ModuleNameParameterSet")]
        /// <summary>
        /// Excludes files from a nuspec
        /// </summary>
        [Parameter()]
        [ValidateNotNullOrEmpty]
        public string[] Exclude { get; set; } = System.Array.Empty<string>();

        /// <summary>
        /// Specifies a nuspec file rather than relying on this module to produce one.
        /// </summary>
        [Parameter(ParameterSetName = "NuspecParameterSet")]
        [Parameter(ParameterSetName = "PathParameterSet")]
        [Parameter(ParameterSetName = "PathLiteralParameterSet")]
        [ValidateNotNullOrEmpty]
        public string Nuspec { get; set; }

        /// <summary>
        /// Specifies a proxy server for the request, rather than a direct connection to the internet resource.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public Uri Proxy { get; set; }

        /// <summary>
        /// Specifies a user account that has permission to use the proxy server that is specified by the Proxy parameter.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true)]
        public PSCredential ProxyCredential { get; set; }

        #endregion

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
            FileInfo moduleFileInfo;
            Hashtable parsedMetadataHash = new Hashtable(StringComparer.InvariantCultureIgnoreCase);

            if (isScript)
            {
                moduleManifestOrScriptPath = pkgFileOrDir.FullName;
                moduleFileInfo = new FileInfo(moduleManifestOrScriptPath);

                // check that script metadata is valid
                // ParseScriptMetadata will throw terminating error if it's unsucessfull in parsing
                ParseScriptMetadata(moduleFileInfo, parsedMetadataHash);

                if (!parsedMetadataHash.ContainsKey("Version") || !parsedMetadataHash.ContainsKey("GUID") || !parsedMetadataHash.ContainsKey("Author"))
                {
                    var exMessage = "Script metadata must specify a Version, GUID, and Author";
                    var ex = new ArgumentException(exMessage);
                    var InvalidScriptMetadata = new ErrorRecord(ex, "InvalidScriptMetadata", ErrorCategory.InvalidData, null);
                    this.ThrowTerminatingError(InvalidScriptMetadata);
                }

                // remove '.ps1' extension from file name 
                pkgName = pkgFileOrDir.Name.Remove(pkgFileOrDir.Name.Length - 4);
            }
            else {
                pkgName = pkgFileOrDir.Name;
                moduleManifestOrScriptPath = System.IO.Path.Combine(_path, pkgName + ".psd1");
                moduleFileInfo = new FileInfo(moduleManifestOrScriptPath);

                // Validate that there's a module manifest 
                if (!File.Exists(moduleManifestOrScriptPath))
                {
                    var message = String.Format("No file with a .psd1 extension was found in {0}.  Please specify a path to a valid modulemanifest.", moduleManifestOrScriptPath);
                    var ex = new ArgumentException(message);
                    var moduleManifestNotFound = new ErrorRecord(ex, "moduleManifestNotFound", ErrorCategory.ObjectNotFound, null);

                    this.ThrowTerminatingError(moduleManifestNotFound);
                }

                // validate that the module manifest has correct data 
                // throws terminating error if module manifest is invalid
                IsValidModuleManifest(moduleManifestOrScriptPath);
            }

            // Create a temp folder to push the nupkg to and delete it later
            string outputDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // if user does not specify that they want to use a nuspec they've created, we'll create a nuspec
            Hashtable dependencies = new Hashtable ();
            if (string.IsNullOrEmpty(Nuspec))
            {
                // right now parsedMetadataHash will be empty for modules and will contain metadata for scripts
                Nuspec = createNuspec(outputDir, moduleFileInfo, out dependencies, parsedMetadataHash);
            }
            else
            {
                // Read the nuspec passed in to pull out the dependency information
                XmlDocument doc = new XmlDocument();
                doc.Load(Nuspec);

                // ex: <version>2.2.1</version>
                XmlNodeList versionNode = doc.GetElementsByTagName("version");
                if (!NuGetVersion.TryParse(versionNode[0].InnerText, out pkgVersion))
                {
                    var message = "Version is not specified in the .nuspec provided. Please provide a valid version in the .nuspec.";
                    var ex = new ArgumentException(message);
                    var versionNotFound = new ErrorRecord(ex, "versionNotFound", ErrorCategory.NotSpecified, null);

                    this.ThrowTerminatingError(versionNotFound);
                }

                // ex: <dependency id="Carbon" version="2.9.2" /> 
                // var dependencyNode = doc.Descendants("dependency");
                XmlNodeList dependencyNode = doc.GetElementsByTagName("dependency");

                foreach (XmlNode dep in dependencyNode)
                {
                    XmlAttributeCollection test = dep.Attributes;
                    var something = test["id"];

                    var depID = dep.Attributes["id"];
                    var depVersion = dep.Attributes["version"];
                    dependencies.Add(dep.Attributes["id"].InnerText, dep.Attributes["version"].InnerText);
                }
            }

            // find repository
            PSRepositoryInfo repository = RepositorySettings.Read(new[] { Repository }, out string[] errorList).FirstOrDefault();
            if (repository == null)
            {
                var message = String.Format("The resource repository '{0}' is not a registered. Please run 'Register-PSResourceRepository' in order to publish to this repository.", Repository);
                var ex = new ArgumentException(message);
                var repositoryNotFound = new ErrorRecord(ex, "repositoryNotFound", ErrorCategory.ObjectNotFound, null);

                this.ThrowTerminatingError(repositoryNotFound);
            }
            string repositoryUrl = repository.Url.AbsoluteUri;

            // Check if dependencies already exist within the repo if:
            // 1) the resource to publish has dependencies and 
            // 2) the -SkipDependenciesCheck flag is not passed in
            if (dependencies != null && !SkipDependenciesCheck)
            {
                SearchForDependencies(dependencies, repositoryUrl);
            }

            if (isScript)
            {
                // copy the script file to the temp directory or 'destination path' directory
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

            var outputDirectory = System.IO.Path.Combine(outputDir, "nupkg");
            // pack into a nupkg
            if (!PackNupkg(outputDir, outputDirectory))
            {
                // throw terminating error, unable to pack
                // Unfortunately the NuGet API does not provide any exception if packing fails 
                // If it fails, it'll require deeper debuggging
                var message = "Unable to pack the resource.";
                var ex = new ArgumentException(message);
                var ErrorPackingIntoNupkg = new ErrorRecord(ex, "ErrorPackingIntoNupkg", ErrorCategory.NotSpecified, null);

                this.ThrowTerminatingError(ErrorPackingIntoNupkg);
            }
            
            PushNupkg(outputDirectory, repositoryUrl);
        }

        private bool IsValidModuleManifest(string moduleManifestPath)
        {
            var isValid = false;
            using (System.Management.Automation.PowerShell pwsh = System.Management.Automation.PowerShell.Create())
            {
                // use PowerShell cmdlet Test-ModuleManifest
                // TODO:  can invoke throw?
                var results = pwsh.AddCommand("Test-ModuleManifest").AddParameter("Path", moduleManifestPath).Invoke();

                if (pwsh.HadErrors)
                { 
                    var error = pwsh.Streams.Error;
                    var exMessage = error[0].ToString();
                    var ex = new ArgumentException(exMessage);
                    var InvalidModuleManifest = new ErrorRecord(ex, "InvalidModuleManifest", ErrorCategory.InvalidData, null);
                    WriteError(InvalidModuleManifest);
                }
                isValid = true;
            }

            return isValid;
        }

        private string createNuspec(string outputDir, FileInfo moduleFileInfo, out Hashtable requiredModules, Hashtable parsedMetadataHash)
        {
            WriteVerbose("Creating new nuspec file.");
            
            if (moduleFileInfo.Extension.Equals(".psd1", StringComparison.OrdinalIgnoreCase))
            {
                // Parse the module manifest 
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

            if (NuGetVersion.TryParse(version, out pkgVersion))
            {
                metadataElementsDictionary.Add("version", pkgVersion.ToNormalizedString());
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

           if (parsedMetadataHash.ContainsKey("releasenotes") || !String.IsNullOrEmpty(ReleaseNotes))
            {
                var releaseNotes = string.IsNullOrEmpty(ReleaseNotes) ? parsedMetadataHash["releasenotes"].ToString().Trim() : ReleaseNotes;
                metadataElementsDictionary.Add("releaseNotes", releaseNotes);
            }

            if (parsedMetadataHash.ContainsKey("copyright"))
            {
                metadataElementsDictionary.Add("copyright", parsedMetadataHash["copyright"].ToString().Trim());
            }

            string tags = moduleFileInfo.Extension.Equals(".psd1", StringComparison.OrdinalIgnoreCase) ? "PSModule" : "PSScript";
            if (parsedMetadataHash.ContainsKey("tags") || Tags != null)
            {
                if (parsedMetadataHash["tags"] != null)
                {
                    tags += " " + parsedMetadataHash["tags"].ToString().Trim();
                }
                if (Tags != null)
                {
                    tags += " " + string.Join(" ", Tags);
                }
            }
            metadataElementsDictionary.Add("tags", tags);

            if (parsedMetadataHash.ContainsKey("licenseurl") || !String.IsNullOrEmpty(LicenseUrl))
            {
                var licenseUrl = string.IsNullOrEmpty(LicenseUrl) ? parsedMetadataHash["licenseurl"].ToString().Trim() : LicenseUrl;
                metadataElementsDictionary.Add("licenseUrl", licenseUrl);
            }

            if (parsedMetadataHash.ContainsKey("projecturl") || !String.IsNullOrEmpty(ProjectUrl))
            {
                var projectUrl = string.IsNullOrEmpty(ProjectUrl) ? parsedMetadataHash["projecturl"].ToString().Trim() : ProjectUrl;
                metadataElementsDictionary.Add("projectUrl", projectUrl);
            }

            if (parsedMetadataHash.ContainsKey("iconurl") || !String.IsNullOrEmpty(IconUrl))
            {
                var iconUrl = string.IsNullOrEmpty(IconUrl) ? parsedMetadataHash["iconurl"].ToString().Trim() : IconUrl;
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

            var nuspecFullName = System.IO.Path.Combine(outputDir, pkgName + ".nuspec");
            doc.Save(nuspecFullName);

            this.WriteVerbose("The newly created nuspec is: " + nuspecFullName);

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
                //loop through the array and add each element of 
                foreach (Hashtable hash in moduleList)
                {
                    dependenciesHash.Add(hash["ModuleName"], hash["ModuleVersion"]);
                }
            }
            else if (LanguagePrimitives.TryConvertTo<string[]>(requiredModules, out string[] moduleNames))
            {
                var listHashtable = new Hashtable();
                foreach (var modName in moduleNames)
                {
                    listHashtable.Add(modName, string.Empty);
                }
            }

            return dependenciesHash;
        }

        private void ParseScriptMetadata(FileInfo moduleFileInfo, Hashtable parsedMetadataHash)
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
                            var message = String.Format("Failed to add key '{0}' and value '{1}' to hashtable.  Error: {2}", key, value, e.Message);
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

        private void SearchForDependencies(Hashtable dependencies, string repositoryUrl)
        {
            // Check to see that all dependencies are in the repository 
            // Searches for each dependency in the repository the pkg is being pushed to, 
            // If the dependency is not there, throw a terminating error
            foreach (var dependency in dependencies.Keys)
            {
                // Need to make individual calls since we're look for exact version numbers or ranges.
                var depName = new[] { (string)dependency };
                var depVersion = (string)dependencies[dependency];
                var type = new[] { "module", "script" };
                var repository = new[] { repositoryUrl };

                // Search for and return the dependency if it's in the repository.
                // TODO: When find is complete, uncomment beginFindHelper method below
                // var dependencyFound = findHelper.beginFindHelper(depName, type, depVersion, true, null, null, repository, Credential, false, false);
                List<PSObject> dependencyFound = null;
                if (dependencyFound == null || !dependencyFound.Any())
                {
                    var message = String.Format("Dependency '{0}' was not found in repository '{1}'.  Make sure the dependency is published to the repository before publishing this module.", dependency, repositoryUrl);
                    var ex = new ArgumentException(message);  // System.ArgumentException vs PSArgumentException
                    var dependencyNotFound = new ErrorRecord(ex, "DependencyNotFound", ErrorCategory.ObjectNotFound, null);

                    this.ThrowTerminatingError(dependencyNotFound);
                }
            }
        }

        private bool PackNupkg(string outputDir, string outputDirectory)
        {
            // Pack the module or script into a nupkg given a nuspec.
            var builder = new PackageBuilder();
            var runner = new PackCommandRunner(
                    new PackArgs
                    {
                        CurrentDirectory = outputDir,
                        OutputDirectory = outputDirectory,
                        Path = Nuspec,
                        Exclude = Exclude,
                        Symbols = false,
                        Logger = NullLogger.Instance
                    },
                    MSBuildProjectFactory.ProjectCreator,
                    builder);

            return runner.RunPackageBuild();
        }

        private void PushNupkg(string outputDirectory, string repoUrl)
        {
            // Push the nupkg to the appropriate repository 
            // Pkg version is parsed from .ps1 file or .psd1 file 
            var fullNupkgPath = System.IO.Path.Combine(outputDirectory, pkgName + "." + pkgVersion.ToNormalizedString() + ".nupkg");
            string publishLocation = repoUrl.EndsWith("/v2", StringComparison.OrdinalIgnoreCase) ? repoUrl + "/package" : repoUrl;

            var settings = NuGet.Configuration.Settings.LoadDefaultSettings(null, null, null);
            
            ILogger log = new NuGetLogger();
            try
            {
                PushRunner.Run(
                        Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null),
                        new PackageSourceProvider(settings),
                        fullNupkgPath,
                        publishLocation,
                        APIKey, // api key
                        null, // symbols source
                        null, // symbols api key
                        0, // timeout
                        false, // disable buffering
                        false, // no symbols
                               // Skip duplicate: if a package and version already exists, skip it and continue with the next package in the push, if any.
                        false, // no skip duplicate  
                        false, // enable server endpoint
                        log // logger
                        ).GetAwaiter().GetResult();
            }
            catch (HttpRequestException e)
            {
                var ex = new ArgumentException(e.Message);
                if (e.Message.Contains("401"))
                {
                    if (e.Message.Contains("An API key must be provided"))
                    {
                        var message = String.Format("Response status code does not indicate success: 401 (An API key must be provided). Please try running again with the -APIKey parameter and specific API key for the repository specified.");
                        ex = new ArgumentException(message);
                        var APIKeyError = new ErrorRecord(ex, "APIKeyError", ErrorCategory.AuthenticationError, null);
                        this.ThrowTerminatingError(APIKeyError);
                    }
                    else
                    {
                        var Error401 = new ErrorRecord(ex, "401Error", ErrorCategory.PermissionDenied, null);
                        this.ThrowTerminatingError(Error401);
                    }
                }
                else if (e.Message.Contains("403"))
                {
                    if (e.Message.Contains("The specified API key is invalid, has expired, or does not have permission"))
                    {
                        var message = String.Format("Response status code does not indicate success: 403 (The specified API key is invalid, has expired, or does not have permission to access the specified package).");
                        ex = new ArgumentException(message);
                        var APIKeyError = new ErrorRecord(ex, "APIKeyError", ErrorCategory.InvalidArgument, null);
                        this.ThrowTerminatingError(APIKeyError);
                    }
                    else
                    {
                        var Error403 = new ErrorRecord(ex, "403Error", ErrorCategory.PermissionDenied, null);
                        this.ThrowTerminatingError(Error403);
                    }
                }
                else if (e.Message.Contains("409"))
                {
                    if (e.Message.Contains("already exists and cannot be modified"))
                    {
                        var message = String.Format("Response status code does not indicate success: 409 (A package with id '{0}' and version '{1}' already exists and cannot be modified).", pkgName, pkgVersion);
                        ex = new ArgumentException(message);
                        var ResourceAlreadyExists = new ErrorRecord(ex, "ResourceAlreadyExists", ErrorCategory.ResourceExists, null);
                        this.ThrowTerminatingError(ResourceAlreadyExists);
                    }
                    else
                    {
                        var Error409 = new ErrorRecord(ex, "409Error", ErrorCategory.PermissionDenied, null);
                        this.ThrowTerminatingError(Error409);
                    }
                }
                else {
                    var HTTPRequestError = new ErrorRecord(ex, "HTTPRequestError", ErrorCategory.PermissionDenied, null);
                    this.ThrowTerminatingError(HTTPRequestError);
                }
            }
        }
    }
}
