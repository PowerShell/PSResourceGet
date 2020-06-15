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
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Versioning;



namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// It retrieves a resource that was installEd with Install-PSResource
    /// Returns a single resource or multiple resource.
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



        /// TODO 
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
        


        /// TODO
        /// <summary>
        /// Specifies a path to one or more locations. Unlike the Path parameter, the value of the LiteralPath parameter is used exactly as entered.
        /// No characters are interpreted as wildcards. If the path includes escape characters, enclose them in single quotation marks.
        /// Single quotation marks tell PowerShell not to interpret any characters as escape sequences.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "PathLiteralParameterSet")]
        [ValidateNotNullOrEmpty]
        //   [Alias('PSPath')]
        public string LiteralPath
        {
         get
         { return _literalPath; }

         set
         { _literalPath = value; }
        }
        private string _literalPath;


        /// TODO
        /// <summary>
        ///  Specifies the exact version of a single resource to publish.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public string RequiredVersion
        {
            get
            { return _requiredVersion; }

            set
            { _requiredVersion = value; }
        }
        private string _requiredVersion;


        // TODO
        /// <summary>
        /// Allows resources marked as prerelease to be published.
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public SwitchParameter Prerelease
        {
            get
            { return _prerelease; }

            set
            { _prerelease = value; }
        }
        private bool _prerelease;









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

        /*
         # Forces the command to run without asking for user confirmation.
         [Parameter()]
         [switch]
         $Force,
         */

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



      


    

        /// TODO:   (should this be parsed form the psd1??)   >>>  consider removing
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




        /// TODO:   (should this be parsed form the psd1??)   >>>  consider removing
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






        /// TODO:   (should this be parsed form the psd1??)   >>>  consider removing
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




        /// TODO:   (should this be parsed form the psd1??)   >>>  consider removing
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


        /// TODO:   (should this be parsed form the psd1??)  >>>  consider removing 
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

        // done
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

        // done
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




        /// <summary>
        /// </summary>
        protected override void ProcessRecord()
        {

            string resolvedPath = "";
            if (!string.IsNullOrEmpty(_name))
            {
                // name parameterset

                // should return path to the module or script
                resolvedPath = publishNameParamSet();

            }
            else {
                // one of the paths parameter set 

                // resolve path 

                // this should be path:
                //"C:\\Users\\americks\\Desktop\\newpsgettestmodule.2.0.0", //"C:\\code\\TestPackage1",
                 
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
                    //sThrowTerminatingError();
                }

            }



            // first do modules, then do scripts  (could be module or script)
            // get the psd1 file or .ps1 file
            string dirName = new DirectoryInfo(resolvedPath).Name;

            string moduleFile = "";
            if (File.Exists(System.IO.Path.Combine(resolvedPath, dirName + ".psd1")))
            {
                // if a module
                moduleFile = System.IO.Path.Combine(resolvedPath, dirName + ".psd1");
            }
            else if (File.Exists(resolvedPath) && resolvedPath.EndsWith(".ps1"))
            {
                // if a script
                moduleFile = resolvedPath;
            }
            else {
                  //ThrowTerminatingError(); // .psd1 or .ps1 does not exist           
            }




            string outputDir = "";
            // if there's no specified destination path to publish the nupkg, we'll just create a temp folder and delete it later
            if (!string.IsNullOrEmpty(_destinationPath))
            {
                // check if there's an extenssion... the directory should be passed in
                outputDir = SessionState.Path.GetResolvedPSPathFromPSPath(_destinationPath).FirstOrDefault().Path;
            }
            else
            {
                // TODO: create temp directory 
                outputDir = "C:\\code\\testdirectory";
            }



            // _nuspec = "C:\\code\\testmodule99\\testmodule99.nuspec";

            // if user specifies they want to use a nuspec they've created
            if (string.IsNullOrEmpty(_nuspec))
            {
                _nuspec = createNuspec(outputDir, moduleFile, dirName);

                // check if nuspec gets created successfully
            }
            else
            {
                // read the nuspec passed in to pull out the dependency information

                using (StreamReader sr = File.OpenText(_nuspec))
                {
                    string str = String.Empty;

                    // read until the beginning of the dependency metadata is hit
                    while ((str = sr.ReadLine()) != null)
                    {
                        if (str.Trim().StartsWith("<dependency "))
                        {
                            // <dependency id="PackageManagement" version="1.4.4" />  1 ,  3
                            var splitStr = str.Split('"');

                            var moduleName = splitStr[1];
                            var moduleVersion = splitStr[3];

                            //var hash = new Hashtable();
                            //hash.Add(moduleName, moduleVersion);

                            dependencies.Add(moduleName, moduleVersion);
                        }
                    }


                }
            }


            if (!_skipDependenciesCheck)
            {
                /// check to see that all dependencies are in the repository 
                ///  For each dependency that's located in the dependency hash 
                ///     * if we created the nuspec we should have a list of dependencies and versions
                ///     * if the nuspec was provided, we'll check if there are any dependencies and versions

                var findHelper = new FindHelper();

                foreach (var dependency in dependencies.Keys)
                {
                    // need to make individual calls since we have version numbers 
                    // make a call to find the dep in the repo...
                    var depName = new[] { (string)dependency };
                    var depVersion = (string)dependencies[dependency];
                    var type = new[] { "module", "script" };
                    var repository = new[] { _repository };

                   
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






            ///////   PACK INTO A NUPKG GIVEN A NUSPEC
            var builder = new PackageBuilder();
                var runner = new PackCommandRunner(
                    new PackArgs
                    {
                        CurrentDirectory = resolvedPath,
                        OutputDirectory = outputDir,  // make temp directory for this
                        Path = _nuspec, //"NewpsGetTestModule.nuspec",
                        Exclude = _exclude,
                        Symbols = false,
                        Logger = NullLogger.Instance
                    },
                    MSBuildProjectFactory.ProjectCreator,
                    builder);;

                runner.BuildPackage();
            ///// END PACK



            //////  PUSH

            var fullNupkgPath = System.IO.Path.Combine(outputDir, "testmodule99.0.0.3.nupkg");
            // check if nupkg successfully installed, if so continue on, if not throw error


            var settings = NuGet.Configuration.Settings.LoadDefaultSettings(null, null, null);

            if (_repository.Equals("poshtest", StringComparison.OrdinalIgnoreCase))
            {
                _repository = "https://www.poshtestgallery.com/api/v2/package";
            }

            if (_repository.Equals("psgallery", StringComparison.OrdinalIgnoreCase))
            {
                _repository = "https://www.powershellgallery.com/api/v2/package";
            }

            ILogger log = new TestLogger();
            PushRunner.Run(
                    Settings.LoadDefaultSettings(null, null, null),
                    new PackageSourceProvider(settings), 
                    fullNupkgPath, //packagePath ////packageInfo.FullName,   
                    _repository, //"https://www.poshtestgallery.com/api/v2/", // packagePushDest.FullName,  
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

            // return the full path 
            return modulePath;
        }


        private string createNuspec(string outputDir, string moduleFile, string pkgName)
        {
            /////////////////// create a nuspec 
            WriteVerbose("Creating new nuspec file.");

            Hashtable parsedMetadataHash = new Hashtable();

            if (moduleFile.EndsWith(".psd1"))
            {
                //FileStream fs = File.Open(moduleFile, FileMode.Open, FileAccess.Read);

                System.Management.Automation.Language.Token[] tokens;
                ParseError[] errors;
                var ast = Parser.ParseFile(moduleFile, out tokens, out errors);

                if (errors.Length > 0)
                {
                    // WriteInvalidDataFileError(resolved, "CouldNotParseAsPowerShellDataFile");
                    WriteDebug("Could not parse '" + moduleFile + "' as a powershell data file.");
                }
                else
                {
                    var data = ast.Find(a => a is HashtableAst, false);
                    if (data != null)
                    {
                        // WriteObject(data.SafeGetValue());
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

                /// parse .ps1  
                /* <#PSScriptInfo
                    .VERSION 1.6
                    .GUID ebf446a3 - 3362 - 4774 - 83c0 - b7299410b63f
                    .AUTHOR Michael Niehaus
                    .COMPANYNAME Microsoft
                    .COPYRIGHT
                    .TAGS Windows AutoPilot
                    .LICENSEURI
                    .PROJECTURI
                    .ICONURI
                    .EXTERNALMODULEDEPENDENCIES
                    .REQUIREDSCRIPTS
                    .EXTERNALSCRIPTDEPENDENCIES
                    .RELEASENOTES
                    #>

                    <#
                    .SYNOPSIS
                     Synopsis description here
                    .DESCRIPTION
                     Description here
                    .PARAMETER Name
                     Parameter description here
                    .PARAMETER Credential
                     Parameter description here
                    .PARAMETER Partner
                     Parameter description here2
                    .EXAMPLE
                     Example cmdlet here
                    #>
                */

                pkgName = pkgName.Remove(pkgName.Length - 4);

                using (StreamReader sr = File.OpenText(moduleFile))
                {
                   // StringBuilder sb = new StringBuilder();
                    //Hashtable parsedMetadataHash = new Hashtable();


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
                            // create new key
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
                                // append to existing key/value
                                parsedMetadataHash[key] = parsedMetadataHash[key] + " " + str;
                            }
                        }
                        //sb.Append(str);
                    }
                    while (str != endOfMetadata);

                    WriteObject(parsedMetadataHash.ToString());
                    // read until the beginning of the next metadata section
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
                        //sb.Append(str);
                    }
                    while (str != endOfMetadata);

                    WriteObject("here");
                    WriteObject(parsedMetadataHash);
                    
                }

            }
            else {
                WriteDebug("File to be parsed does not have a .psd1 or .ps1 extension.");
            }





            // safeData;

            WriteObject(parsedMetadataHash);

            WriteObject("-----------------------");

            
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

            //using (StreamWriter sw = new StreamWriter(fullinstallPath))

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

       
            ///// TUESDAY:  1)  test the above nuspec additions
            //  2) check dependencies below 
            // 3) look into what exactly needs to be passed in for file stuff 

            foreach (var key in metadataElementsDictionary.Keys)
            {
                XmlElement element = doc.CreateElement(key, nameSpaceUri);

                string elementInnerText;
                metadataElementsDictionary.TryGetValue(key, out elementInnerText);
                element.InnerText = elementInnerText;

                metadataElement.AppendChild(element);
            }



            ////// CREATE DEPENDENCY TABLE HERE
            ///
            if (parsedMetadataHash.ContainsKey("requiredmodules"))
            {
                dependencies = (Hashtable)parsedMetadataHash["requiredmodules"];

                if (dependencies != null)
                {
                    XmlElement dependenciesElement = doc.CreateElement("dependencies", nameSpaceUri);

                    foreach (Hashtable dependency in dependencies)
                    {
                        XmlElement element = doc.CreateElement("dependency", nameSpaceUri);

                        element.SetAttribute("id", dependency["ModuleName"].ToString());  /// may need to be lower case
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
            //  done creating nuspec 
        }
    }
}





