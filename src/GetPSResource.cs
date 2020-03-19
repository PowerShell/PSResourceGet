// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//using System;
//using System.Management.Automation;
//using Microsoft.PowerShell.Commands;
//using Microsoft.PowerShell.PowerShellGet.RepositorySettings;


using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;



namespace Microsoft.PowerShell.PowerShellGet.Cmdlets
{
    /// <summary>
    /// It retrieves a resource that was installEd with Install-PSResource
    /// Returns a single resource or multiple resource.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "PSResource", SupportsShouldProcess = true,
        HelpUri = "<add>", RemotingCapability = RemotingCapability.None)]
    public sealed
    class GetPSResource : PSCmdlet
    {
        /// <summary>
        /// Specifies the desired name for the resource to look for.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true, ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty]
        public string[] Name
        {
            get
            { return _name; }

            set
            { _name = value; }
        }
        private string[] _name;


        /// <summary>
        /// Specifies the version of the resource to include to look for. 
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        [ValidateNotNullOrEmpty()]
        public string Version
        {
            get
            { return _version; }

            set
            { _version = value; }
        }
        private string _version;


        /// <summary>
        /// Specifies to include prerelease versions
        /// </summary>
        [Parameter(ParameterSetName = "NameParameterSet")]
        public SwitchParameter Prerelease
        {
            get
            { return _prerelease; }

            set
            { _prerelease = value; }
        }
        private SwitchParameter _prerelease;



        /// <summary>
        /// </summary>
        protected override void ProcessRecord()
        {
            // list all installation locations:
            // TODO: update paths
            var psModulePath = "C:\\code\\temp\\sinstalltestpath";



            // 1) Create a list of either
            // Of all names
            var versionDirs = Directory.GetDirectories(psModulePath).ToList();

            // Or a list of the passed in names
            if (_name != null)
            {
                versionDirs = versionDirs.FindAll(p => _name.Contains(p));
            }



            NuGetVersion nugetVersion;
            var bleh = NuGetVersion.TryParse(_version, out nugetVersion);

            VersionRange versionRange = null;
            if (nugetVersion == null)
            {
                VersionRange.TryParse(_version, out versionRange);
            }



            List<string> dirsToReturn = new List<string>();

            // check whether we want to include prerelease versions or not
            // If prereleaseOnly is not specified, we'll only take into account stable versions of pkgs
            if (!_prerelease)
            {
                List<string> stableVersionDirs = new List<string>();
                foreach (var dir in versionDirs)
                {
                    var nameOfDir = Path.GetFileName(dir);
                    var nugVersion = NuGetVersion.Parse(nameOfDir);

                    if (nugVersion.IsPrerelease)
                    {
                        stableVersionDirs.Remove(dir);
                    }
                }
                versionDirs = stableVersionDirs;
            }




            //2) use above list to check 
            // if the version specificed is a version range
            if (versionRange != null)
            {

                foreach (var versionDirPath in versionDirs)
                {
                    var nameOfDir = Path.GetFileName(versionDirPath);
                    var nugVersion = NuGetVersion.Parse(nameOfDir);

                    if (versionRange.Satisfies(nugVersion))
                    {
                        dirsToReturn.Add(versionDirPath);
                    }
                }
            }
            else if (nugetVersion != null)
            {
                // if the version specified is a version

                dirsToReturn.Add(nugetVersion.ToNormalizedString());
            }
            else
            {
                // if no version specified (just the latest version)
                // if no version is specified, just get the latest version
                versionDirs.Sort();

                dirsToReturn.Add(versionDirs.First());
            }



            /// list all the modules, or the specific name / version
            /// output:  version  |    name     |   repository      |   description



            /*

            foreach (var repo in listOfRepositories)
            {
                var repoPSObj = new PSObject();
                repoPSObj.Members.Add(new PSNoteProperty("Name", repo.Name));
                repoPSObj.Members.Add(new PSNoteProperty("Url", repo.Url));
                repoPSObj.Members.Add(new PSNoteProperty("Trusted", repo.Trusted));
                repoPSObj.Members.Add(new PSNoteProperty("Priority", repo.Priority));
                WriteObject(repoPSObj);
            }

    */
        }



    }
}
