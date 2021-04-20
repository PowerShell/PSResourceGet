// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    internal static class Utils
    {
        #region Public methods

        public static string TrimQuotes(string name)
        {
            return name.Trim('\'', '"');
        }

        public static string QuoteName(string name)
        {
            bool quotesNeeded = false;
            foreach (var c in name)
            {
                if (Char.IsWhiteSpace(c))
                {
                    quotesNeeded = true;
                    break;
                }
            }

            if (!quotesNeeded)
            {
                return name;
            }

            return "'" + CodeGeneration.EscapeSingleQuotedStringContent(name) + "'";
        }

        /// <summary>
        /// Reads a 'PSGetModuleInfo.xml' PowerShell serialized file and returns
        /// a PSGetInfo object containing the file contents.
        /// </summary>
        public static bool TryGetPSGetInfo(
            string filePath,
            out PSGetInfo psGetInfo,
            out string errorMsg)
        {
            psGetInfo = null;
            errorMsg = string.Empty;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                errorMsg = "GetPSGetInfo: Invalid file path. Filepath cannot be empty or whitespace.";
                return false;
            }

            try
            {
                // Read and deserialize information xml file.
                var psObjectInfo = (PSObject) PSSerializer.Deserialize(
                    System.IO.File.ReadAllText(
                        filePath));

                psGetInfo = new PSGetInfo
                {
                    AdditionalMetadata = GetProperty<Dictionary<string,string>>(nameof(PSGetInfo.AdditionalMetadata), psObjectInfo),
                    Author = GetProperty<string>(nameof(PSGetInfo.Author), psObjectInfo),
                    CompanyName = GetProperty<string>(nameof(PSGetInfo.CompanyName), psObjectInfo),
                    Copyright = GetProperty<string>(nameof(PSGetInfo.Copyright), psObjectInfo),
                    Dependencies = GetStringArray(GetProperty<ArrayList>(nameof(PSGetInfo.Dependencies), psObjectInfo)),
                    Description = GetProperty<string>(nameof(PSGetInfo.Description), psObjectInfo),
                    IconUri = GetProperty<Uri>(nameof(PSGetInfo.IconUri), psObjectInfo),
                    Includes = new PSGetInclude(GetProperty<Hashtable>(nameof(PSGetInfo.Includes), psObjectInfo)),
                    InstalledDate = GetProperty<DateTime>(nameof(PSGetInfo.InstalledDate), psObjectInfo),
                    InstalledLocation = GetProperty<string>(nameof(PSGetInfo.InstalledLocation), psObjectInfo),
                    LicenseUri = GetProperty<Uri>(nameof(PSGetInfo.LicenseUri), psObjectInfo),
                    Name = GetProperty<string>(nameof(PSGetInfo.Name), psObjectInfo),
                    PackageManagementProvider = GetProperty<string>(nameof(PSGetInfo.PackageManagementProvider), psObjectInfo),
                    PowerShellGetFormatVersion = GetProperty<string>(nameof(PSGetInfo.PowerShellGetFormatVersion), psObjectInfo),
                    ProjectUri = GetProperty<Uri>(nameof(PSGetInfo.ProjectUri), psObjectInfo),
                    PublishedDate = GetProperty<DateTime>(nameof(PSGetInfo.PublishedDate), psObjectInfo),
                    ReleaseNotes = GetProperty<string>(nameof(PSGetInfo.ReleaseNotes), psObjectInfo),
                    Repository = GetProperty<string>(nameof(PSGetInfo.Repository), psObjectInfo),
                    RepositorySourceLocation = GetProperty<string>(nameof(PSGetInfo.RepositorySourceLocation), psObjectInfo),
                    Tags = GetStringArray(GetProperty<ArrayList>(nameof(PSGetInfo.Tags), psObjectInfo)),
                    Type = GetProperty<string>(nameof(PSGetInfo.Type), psObjectInfo),
                    UpdatedDate = GetProperty<DateTime>(nameof(PSGetInfo.UpdatedDate), psObjectInfo),
                    Version = GetProperty<Version>(nameof(PSGetInfo.Version), psObjectInfo)
                };

                return true;
            }
            catch(Exception ex)
            {
                errorMsg = string.Format(
                    CultureInfo.InvariantCulture,
                    @"GetPSGetInfo: Cannot read the PowerShellGet information file with error: {0}",
                    ex.Message);

                return false;
            }
        }

        /// <summary>
        /// Converts an ArrayList of object types to a string array.
        /// </summary>
        public static string[] GetStringArray(ArrayList list)
        {
            if (list == null) { return null; }

            var strArray = new string[list.Count];
            for (int i=0; i<list.Count; i++)
            {
                strArray[i] = list[i] as string;
            }

            return strArray;
        }

        #endregion

        #region Private methods

        private static T ConvertToType<T>(PSObject psObject)
        {
            // We only convert Dictionary<string, string> types.
            if (typeof(T) != typeof(Dictionary<string, string>))
            {
                return default(T);
            }

            var dict = new Dictionary<string, string>();
            foreach (var prop in psObject.Properties)
            {
                dict.Add(prop.Name, prop.Value.ToString());
            }

            return (T)Convert.ChangeType(dict, typeof(T));
        }

        private static T GetProperty<T>(
            string Name,
            PSObject psObjectInfo)
        {
            var val = psObjectInfo.Properties[Name]?.Value;
            if (val == null)
            {
                return default(T);
            }

            switch (val)
            {
                case T valType:
                    return valType;

                case PSObject valPSObject:
                    switch (valPSObject.BaseObject)
                    {
                        case T valBase:
                            return valBase;

                        case PSCustomObject _:
                            // A base object of PSCustomObject means this is additional metadata
                            // and type T should be Dictionary<string,string>.
                            return ConvertToType<T>(valPSObject);

                        default:
                            return default(T);
                    }

                default:
                    return default(T);
            }
        }

        #endregion
    }

    #region PSGetInfo classes

    internal sealed class PSGetInclude
    {
        #region Properties

        public string[] Cmdlet { get; }

        public string[] Command { get; }

        public string[] DscResource { get; }

        public string[] Function { get; }

        public string[] RoleCapability { get; }

        public string[] Workflow { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor
        /// 
        /// Provided hashtable has form:
        ///     Key: Cmdlet
        ///     Value: ArrayList of Cmdlet name strings
        ///     Key: Command
        ///     Value: ArrayList of Command name strings
        ///     Key: DscResource
        ///     Value: ArrayList of DscResource name strings
        ///     Key: Function
        ///     Value: ArrayList of Function name strings
        ///     Key: RoleCapability (deprecated for PSGetV3)
        ///     Value: ArrayList of RoleCapability name strings
        ///     Key: Workflow (deprecated for PSGetV3)
        ///     Value: ArrayList of Workflow name strings
        /// </summary>
        /// <param name="includes">Hashtable of PSGet includes</param>
        public PSGetInclude(Hashtable includes)
        {
            if (includes == null) { return; }

            Cmdlet = GetHashTableItem(includes, nameof(Cmdlet));
            Command = GetHashTableItem(includes, nameof(Command));
            DscResource = GetHashTableItem(includes, nameof(DscResource));
            Function = GetHashTableItem(includes, nameof(Function));
            RoleCapability = GetHashTableItem(includes, nameof(RoleCapability));
            Workflow = GetHashTableItem(includes, nameof(Workflow));
        }

        #endregion

        #region Private methods

        private string[] GetHashTableItem(
            Hashtable table,
            string name)
        {
            if (table.ContainsKey(name) &&
                table[name] is PSObject psObjectItem)
            {
                return Utils.GetStringArray(psObjectItem.BaseObject as ArrayList);
            }

            return null;
        }

        #endregion
    }

    internal sealed class PSGetInfo
    {
        #region Properties

        public Dictionary<string, string> AdditionalMetadata { get; set; }

        public string Author { get; set; }

        public string CompanyName { get; set; }

        public string Copyright { get; set; }

        public string[] Dependencies { get; set; }

        public string Description { get; set; }

        public Uri IconUri { get; set; }

        public PSGetInclude Includes { get; set; }

        public DateTime InstalledDate { get; set; }

        public string InstalledLocation { get; set; }

        public Uri LicenseUri { get; set; }

        public string Name { get; set; }

        public string PackageManagementProvider { get; set; }

        public string PowerShellGetFormatVersion { get; set; }

        public Uri ProjectUri { get; set; }

        public DateTime PublishedDate { get; set; }

        public string ReleaseNotes { get; set; }

        public string Repository { get; set; }

        public string RepositorySourceLocation { get; set; }

        public string[] Tags { get; set; }

        public string Type { get; set; }

        public DateTime UpdatedDate { get; set; }

        public Version Version { get; set; }

        #endregion
    }

    #endregion

    #region Test Hooks

    public static class TestHooks
    {
        public static PSObject ReadPSGetInfo(string filePath)
        {
            if (Utils.TryGetPSGetInfo(filePath, out PSGetInfo psGetInfo, out string errorMsg))
            {
                return PSObject.AsPSObject(psGetInfo);
            }
            else
            {
                throw new PSInvalidOperationException(errorMsg);
            }
        }
    }

    #endregion
}
