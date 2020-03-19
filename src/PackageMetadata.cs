
using System;


public class PackageMetadata
{
    public string Name { get; set; }
    public string Version { get; set; }
    public string Type { get; set; }
    public string Description { get; set; }
    //public string Author { get; set; }
    //public string[] CompanyName { get; set; }
    public string Copyright { get; set; }
    public DateTime PublishedDate { get; set; }
    public object InstalledDate { get; set; }
    public object UpdatedDate { get; set; }
    public string LicenseUri { get; set; }
    public string ProjectUri { get; set; }
    public object IconUri { get; set; }
    public string[] Tags { get; set; }
    public Includes Includes { get; set; }
    public object PowerShellGetFormatVersion { get; set; }
    public string ReleaseNotes { get; set; }
    public Dependency[] Dependencies { get; set; }
    public string RepositorySourceLocation { get; set; }
    public string Repository { get; set; }
    public string PackageManagementProvider { get; set; }
    public Additionalmetadata AdditionalMetadata { get; set; }
}

public class Includes
{
    public string[] Command { get; set; }
    public string[] RoleCapability { get; set; }
    public string[] Function { get; set; }
    public string[] Cmdlet { get; set; }
    //public string Workflow { get; set; }
    public string[] DscResource { get; set; }
}


public class Dependency
{
    public string Name { get; set; }
    public string MinimumVersion { get; set; }
    public string MaximumVersion { get; set; }
}

public class Additionalmetadata
{
    public string summary { get; set; }
    public string isLatestVersion { get; set; }
    public string PackageManagementProvider { get; set; }
    public string releaseNotes { get; set; }
    public string DscResources { get; set; }
    public string tags { get; set; }
    public string downloadCount { get; set; }
    public string isAbsoluteLatestVersion { get; set; }
    public string GUID { get; set; }
    public string Functions { get; set; }
    public string ItemType { get; set; }
    public string lastUpdated { get; set; }
    public string developmentDependency { get; set; }
    public string CompanyName { get; set; }
    public string SourceName { get; set; }
    public string requireLicenseAcceptance { get; set; }
    public string created { get; set; }
    public string description { get; set; }
    public DateTime updated { get; set; }
    public string NormalizedVersion { get; set; }
    public string packageSize { get; set; }
    public string published { get; set; }
    public string PowerShellVersion { get; set; }
    public string Authors { get; set; }
    public string copyright { get; set; }
    public string IsPrerelease { get; set; }
    public string FileList { get; set; }
    public string versionDownloadCount { get; set; }
    public string Cmdlets { get; set; }
    public string CLRVersion { get; set; }
}
