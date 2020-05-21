// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using NuGet.Configuration;
using NuGet.Commands;
using NuGet.Packaging;
using NuGet.Common;
using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Xml;

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



        /*
        /// <summary>
        /// Specifies the path to the resource that you want to publish. This parameter accepts the path to the folder that contains the resource.
        /// Specifies a path to one or more locations. Wildcards are permitted. The default location is the current directory (.).
        /// </summary>
        [Parameter()]
        [ValidateNotNullOrEmpty]
        public string Path
        {
            get
            { return _path; }

            set
            { _path = value; }
        }
        private string _path;
        */


        /*

     # Specifies a path to one or more locations. Unlike the Path parameter, the value of the LiteralPath parameter is used exactly as entered.
     # No characters are interpreted as wildcards. If the path includes escape characters, enclose them in single quotation marks.
     # Single quotation marks tell PowerShell not to interpret any characters as escape sequences.
             [Parameter(Mandatory = $true,
                 ParameterSetName = 'ModuleLiteralPathParameterSet',
                 ValueFromPipelineByPropertyName = $true)]
             [Parameter(Mandatory = $true,
                 ParameterSetName = 'ScriptLiteralPathParameterSet',
                 ValueFromPipelineByPropertyName = $true)]
             [Alias('PSPath')]
             [ValidateNotNullOrEmpty()]
             [string]
             $LiteralPath,



             # Specifies the exact version of a single resource to publish.
             [Parameter()]
             [ValidateNotNullOrEmpty()]
             [string]
             $RequiredVersion,





             # Specifies a user account that has rights to a specific repository.
             [Parameter(ValueFromPipelineByPropertyName = $true)]
             [PSCredential]
             $Credential,

             # Specifies a string containing release notes or comments that you want to be available to users of this version of the resource.
             [Parameter()]
             [string[]]
             $ReleaseNotes,

             # Adds one or more tags to the resource that you are publishing.
             [Parameter()]
             [ValidateNotNullOrEmpty()]
             [string[]]
             $Tags,

             # Specifies the URL of licensing terms for the resource you want to publish.
             [Parameter()]
             [ValidateNotNullOrEmpty()]
             [Uri]
             $LicenseUri,

             # Specifies the URL of an icon for the resource.
             [Parameter()]
             [ValidateNotNullOrEmpty()]
             [Uri]
             $IconUri,

             # Specifies the URL of a webpage about this project.
             [Parameter()]
             [ValidateNotNullOrEmpty()]
             [Uri]
             $ProjectUri,

             # Excludes files from a nuspec
             [Parameter(ParameterSetName = "ModuleNameParameterSet")]
             [ValidateNotNullOrEmpty()]
             [string[]]
             $Exclude,

             # Forces the command to run without asking for user confirmation.
             [Parameter()]
             [switch]
             $Force,

             # Allows resources marked as prerelease to be published.
             [Parameter()]
             [switch]
             $Prerelease,

             # Bypasses the default check that all dependencies are present.
             [Parameter()]
             [switch]
             $SkipDependenciesCheck,

             # 
             [Parameter()]
             [switch]
             $Nuspec


         )

             */
        //// TODO!
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






        /// <summary>
        /// </summary>
        protected override void ProcessRecord()
        {


            if (string.IsNullOrEmpty(_nuspec))
            {
                /////////////////// create a nuspec 
                WriteVerbose("Creating new nuspec file.");

                var nameSpaceUri = "http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd";
                var doc = new XmlDocument();

                // xml declaration is recommended, but not mandatory
                XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "utf-8", null);
                XmlElement root = doc.DocumentElement;
                doc.InsertBefore(xmlDeclaration, root);

                // create top-level elements
                XmlElement packageElement = doc.CreateElement("package", "package", nameSpaceUri);
                XmlElement metadataElement = doc.CreateElement("metadata", "metadata", nameSpaceUri);

                //using (StreamWriter sw = new StreamWriter(fullinstallPath))

                SortedDictionary<string, string> metadataElementsDictionary = new SortedDictionary<string, string>();

                metadataElementsDictionary.Add("id", _id);
                metadataElementsDictionary.Add("version", _version);
                metadataElementsDictionary.Add("authors", _authors // join "," );
                metadataElementsDictionary.Add("owners", _owners // join "," );
                metadataElementsDictionary.Add("requireLicenseAcceptance", _requireLicenseAcceptance.ToString().ToLower());
                metadataElementsDictionary.Add("description", _description);
                metadataElementsDictionary.Add("releaseNotes", _releaseNotes);
                metadataElementsDictionary.Add("copyright", _copyright);
                metadataElementsDictionary.Add("tags", _tagsString);

                if (_licenseUrl) { metadataElementsDictionary.Add("licenseUrl", _licenseUrl) }
                if (_projectUrl) { metadataElementsDictionary.Add("projectUrl", _projectUrl) }
                if (_iconUrl) { metadataElementsDictionary.Add("iconUrl", _iconUrl) }


                foreach (var key in metadataElementsDictionary.Keys) {
                    XmlElement element = doc.CreateElement(key, key, nameSpaceUri);

                    string elementInnerText;
                    metadataElementsDictionary.TryGetValue(key, out elementInnerText);
                    element.InnerText = elementInnerText;

                    metadataElement.AppendChild(element);
                }
            }




            if (_dependencies) {
                XmlElement dependenciesElement = doc.CreateElement("dependencies", "dependencies", nameSpaceUri);

                foreach (var dependency in _dependencies) {
                    XmlElement element = doc.CreateElement("dependency", "dependency", nameSpaceUri);

                    element.SetAttribute("id", dependency.id);
                    if (dependency.version) { element.SetAttribute("version", dependency.version); }

                    dependenciesElement.AppendChild(element);
                }
                metadataElement.AppendChild(dependenciesElement);
            }


            XmlElement filesElement = null;
            if (_files) {

                filesElement = doc.CreateElement("files", "files", nameSpaceUri);

                foreach (var file in _files) {
                    XmlElement element = doc.CreateElement("file", "file", nameSpaceUri);

                    element.SetAttribute("src", file.src);
                    if (file.target) { element.SetAttribute("target", file.target); }
                    if (file.exclude) { element.SetAttribute("exclude", file.exclude); }

                    filesElement.AppendChild(element);
                }
            }

            packageElement.AppendChild(metadataElement);
            if (filesElement != null) { packageElement.AppendChild(filesElement); }

            doc.AppendChild(packageElement);



            var nuspecFullName = Path.Combine(_outputPath, _id + ".nuspec");
            doc.Save(nuspecFullName);

            this.WriteVerbose("The newly created nuspec is: " + nuspecFullName);
              /////////////////  done creating nuspec 







































              var testDirectory = "C:\\code\\testdirectory";



            ///////   PACK INTO A NUPKG GIVEN A NUSPEC
            var builder = new PackageBuilder();
                var runner = new PackCommandRunner(
                    new PackArgs
                    {
                        CurrentDirectory = "C:\\Users\\americks\\Desktop\\newpsgettestmodule.2.0.0", //"C:\\code\\TestPackage1",
                        OutputDirectory = testDirectory,
                        Path = "NewpsGetTestModule.nuspec",
                        Exclude = System.Array.Empty<string>(),
                        Symbols = false,
                        Logger = NullLogger.Instance
                    },
                    MSBuildProjectFactory.ProjectCreator,
                    builder);

                runner.BuildPackage();
            ///// END PACK



            //////  PUSH
            ///
            /// 

            var fullNupkgPath = Path.Combine(testDirectory, "newpsgettestmodule.2.1.0.nupkg");

            var settings = NuGet.Configuration.Settings.LoadDefaultSettings(null, null, null); ///.LoadSettings(rootPath,

            ILogger log = new TestLogger();
            PushRunner.Run(
                    Settings.LoadDefaultSettings(null, null, null),
                    new PackageSourceProvider(settings),  //new TestPackageSourceProvider(packageSources),
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
            ///////



            // resolve this path
            var nupkgToPush = "C:\\code\\TestPackage1\\TestPackage1.1.0.0.nupkg";

            var args = new string[]
            {
                "push", nupkgToPush,  //push foo.nupkg
                "--api-key", _APIKey
            };

           // NuGet.Commands.AddSourceArgs
            //NuGet.CommandLine.XPlat.Program.Main(args);




            // Act
            /*
            PushRunner.Run(
                null, //Settings.LoadDefaultSettings(null, null, null),
                IEnumerable<PackageSource> packageSources,
                "PackageName",   //packageInfo.FullName,
                packagePushDest.FullName,
                null, // api key
                null, // symbols source
                null, // symbols api key
                0, // timeout
                false, // disable buffering
                false, // no symbols,
                false, // no skip duplicate
                false, // enable server endpoint
                null); //new TestLogger());

                // Assert
                //var destFile = Path.Combine(packagePushDest.FullName, packageInfo.Name);
                //Assert.Equal(true, File.Exists(destFile));
            //}
            */
        }

    }
}





