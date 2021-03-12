
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using System;
using System.Collections.Generic;
using static System.Environment;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Collections;
using NuGet.Versioning;

namespace Microsoft.PowerShell.PowerShellGet
{
    /// <summary>
    /// Internal functions
    /// </summary>

    static class Utilities
    {

        public static VersionRange GetPkgVersion(string versionPassedIn)
        {
            // Check if exact version
            NuGetVersion nugetVersion;
            NuGetVersion.TryParse(versionPassedIn, out nugetVersion);
            //NuGetVersion.TryParse(pkg.Identity.Version.ToString(), out nugetVersion);

            VersionRange versionRange = null;
            if (nugetVersion != null)
            {
                versionRange = new VersionRange(nugetVersion, true, nugetVersion, true, null, null);
            }
            else
            {
                // Check if version range
                VersionRange.TryParse(versionPassedIn, out versionRange);
            }

            return versionRange;
        }


        public static Hashtable GetInstallationPaths(PSCmdlet cmdletPassedIn, string scope)
        {
            //this.WriteDebug(string.Format("Parameters passed in >>> Name: '{0}'; Version: '{1}'; Prerelease: '{2}'; Repository: '{3}'; Scope: '{4}'; AcceptLicense: '{5}'; Quiet: '{6}'; Reinstall: '{7}'; TrustRepository: '{8}'; NoClobber: '{9}';", string.Join(",", _name), _version != null ? _version : string.Empty, _prerelease.ToString(), _repository != null ? string.Join(",", _repository) : string.Empty, _scope != null ? _scope : string.Empty, _acceptLicense.ToString(), _quiet.ToString(), _reinstall.ToString(), _trustRepository.ToString(), _noClobber.ToString()));
            Hashtable hash = new Hashtable();
            //string osPlatform = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
            string programFilesPath;
            string myDocumentsPath;

            bool consoleIsElevated = false;
            bool isWindowsPS = false;
            cmdletPassedIn.WriteDebug("Entering GetPackageInstallationPaths");
#if NET472
            // If WindowsPS
            var id = System.Security.Principal.WindowsIdentity.GetCurrent();
            consoleIsElevated = (id.Owner != id.User);
            isWindowsPS = true;

            myDocumentsPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.MyDocuments), "WindowsPowerShell");
            programFilesPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.ProgramFiles), "WindowsPowerShell");
#else
            // If PS6+ on Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var id = System.Security.Principal.WindowsIdentity.GetCurrent();
                consoleIsElevated = (id.Owner != id.User);

                myDocumentsPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.MyDocuments), "PowerShell");
                programFilesPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.ProgramFiles), "PowerShell");
            }
            else
            {
                // Paths are the same for both Linux and MacOS
                myDocumentsPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "powershell");
                programFilesPath = Path.Combine("/usr", "local", "share", "powershell");

                using (System.Management.Automation.PowerShell pwsh = System.Management.Automation.PowerShell.Create())
                {
                    var uID = pwsh.AddScript("id -u").Invoke();
                    foreach (var item in uID)
                    {
                        cmdletPassedIn.WriteDebug(string.Format("UID is: '{0}'", item));
                        consoleIsElevated = (String.Equals(item.ToString(), "0"));
                    }
                }
            }
#endif
            hash.Add("myDocumentsPath", myDocumentsPath);
            hash.Add("programFilesPath", programFilesPath);

            cmdletPassedIn.WriteDebug(string.Format("Console is elevated: '{0}'", consoleIsElevated));
            cmdletPassedIn.WriteDebug(string.Format("Console is Windows PowerShell: '{0}'", isWindowsPS));
            cmdletPassedIn.WriteDebug(string.Format("Current user scope installation path: '{0}'", myDocumentsPath));
            cmdletPassedIn.WriteDebug(string.Format("All users scope installation path: '{0}'", programFilesPath));

            scope = string.IsNullOrEmpty(scope) ? "CurrentUser" : scope;
            cmdletPassedIn.WriteVerbose(string.Format("Scope is: {0}", scope));

            string psPath = string.Equals(scope, "AllUsers") ? programFilesPath : myDocumentsPath;
            hash.Add("psPath", psPath);

            string psModulesPath = Path.Combine(psPath, "Modules");
            hash.Add("psModulesPath", psModulesPath);

            string psScriptsPath = Path.Combine(psPath, "Scripts");
            hash.Add("psScriptsPath", psScriptsPath);

            string psInstalledScriptsInfoPath = Path.Combine(psScriptsPath, "InstalledScriptInfos");
            hash.Add("psInstalledScriptsInfoPath", psInstalledScriptsInfoPath);

            cmdletPassedIn.WriteDebug("Checking to see if paths exist");
            cmdletPassedIn.WriteDebug(string.Format("Path: '{0}'  >>> exists? '{1}'", psModulesPath, Directory.Exists(psModulesPath)));
            cmdletPassedIn.WriteDebug(string.Format("Path: '{0}'  >>> exists? '{1}'", psScriptsPath, Directory.Exists(psScriptsPath)));
            cmdletPassedIn.WriteDebug(string.Format("Path: '{0}'  >>> exists? '{1}'", psInstalledScriptsInfoPath, Directory.Exists(psInstalledScriptsInfoPath)));


            // Create PowerShell modules and scripts paths if they don't already exist
            try {
                if (!Directory.Exists(psModulesPath))
                {
                    cmdletPassedIn.WriteVerbose(string.Format("Creating PowerShell modules path '{0}'", psModulesPath));
                    Directory.CreateDirectory(psModulesPath);

                }
                if (!Directory.Exists(psScriptsPath))
                {
                    cmdletPassedIn.WriteVerbose(string.Format("Creating PowerShell scripts path '{0}'", psScriptsPath));
                    Directory.CreateDirectory(psScriptsPath);
                }
                if (!Directory.Exists(psInstalledScriptsInfoPath))
                {
                    cmdletPassedIn.WriteVerbose(string.Format("Creating PowerShell installed scripts info path '{0}'", psInstalledScriptsInfoPath));
                    Directory.CreateDirectory(psInstalledScriptsInfoPath);
                }
            }
            catch
            {

            }


            List<string> psModulesPathAllDirs = (Directory.GetDirectories(psModulesPath)).ToList();
            hash.Add("psModulesPathAllDirs", psModulesPathAllDirs);

            // Get the script metadata XML files from the 'InstalledScriptInfos' directory
            List<string> psScriptsPathAllDirs = (Directory.GetFiles(psInstalledScriptsInfoPath)).ToList();
            hash.Add("psScriptsPathAllDirs", psScriptsPathAllDirs);


            return hash;
        }
    }
}
