using System.Management.Automation;

public class PkgParams
{
    public string Name { get; set; }
    public string Version { get; set; }
    public string Repository { get; set; }
    public PSCredential Credential { get; set; }
    public bool AcceptLicense { get; set; } 
    public bool Prerelease { get; set; }
    public string Scope { get; set; }
    public bool Quiet { get; set; }
    public bool Reinstall { get; set; }
    public bool Force { get; set; }
    public bool TrustRepository { get; set; }
    public bool NoClobber { get; set; }
}