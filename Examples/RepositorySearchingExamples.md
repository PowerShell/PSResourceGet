
# Examples for `Find-PSResource` searching through repositories.

These examples will go through a number of scenarios related to `Find-PSResource` searching through repositories to show what the expected outcome will be.
In all these examples, the repositories registered and their priorities are as follows:

```
Name         Uri                                      Trusted Priority
----         ---                                      ------- --------
PSGallery    https://www.powershellgallery.com/api/v2 True    50
NuGetGallery https://api.nuget.org/v3/index.json      True    60
```

1) Searching with only a package name specified, eg: `Find-PSResource 'TestModule'` or `Find-PSResource 'TestModule' -Repository '*'`
    * When the package exists in both repositories:
      
    * When the package is in the first repository, but not the second:
    
    * When the package is the second repository, but not the first:
    
    * When the package is in neither repository:

2) Searching with a package name and a repository specified, eg: `Find-PSResource 'TestModule' -Repository PSGallery`
    * When the package exists in both repositories:
      
    * When the package is in the first repository, but not the second:
    
    * When the package is the second repository, but not the first:
    
    * When the package is in neither repository:
    
3) Searching with a package name specified and wildcard repository, eg: `Find-PSResource 'TestModule' -Repository *Gallery`
    * When the package exists in both repositories:
      
    * When the package is in the first repository, but not the second:
    
    * When the package is the second repository, but not the first:
    
    * When the package is in neither repository:

4) Searching with a package name specified and multiple repository names specified, eg: `Find-PSResource 'TestModule' -Repository PSGallery, NuGetGallery`

    * When the package exists in both repositories:
      
    * When the package is in the first repository, but not the second:
    
    * When the package is the second repository, but not the first:
    
    * When the package is in neither repository:

5) Searching with a package name specified and both a repository name specified AND a repository name with a wildcard, eg: `Find-PSResource 'TestModule' -Repository *Gallery, LocalRepo`
    