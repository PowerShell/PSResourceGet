using System.Management.Automation;
public class PSRepositoryItemInfo
{
    private string _name;
    private string _url;
    private bool _trusted= false;
    private int _priority = 50;


    public PSRepositoryItemInfo(string name, string url)
    {
        _name = name;
        _url = url;
        // _trusted = false; //todo: use default/initializer perhaps
        // _priority = 50; //todo: use default/initializer perhaps
    }
    public PSRepositoryItemInfo(string name, string url, bool trusted, int priority)
    {
        _name = name;
        _url = url;
        _trusted = trusted;
        _priority = priority;
    }

    public string Name
    {
        get
        { return _name; }

        set
        { _name = value; }
    }

    public string Url
    {
        get
        { return _url; }

        set
        { _url = value; }
    }

    public bool Trusted
    {
        get
        { return _trusted; }

        set
        { _trusted = value; }
    }

    [ValidateRange(0, 50)]
    public int Priority
    {
        get
        { return _priority; }

        set
        { _priority = value; }
    }
}