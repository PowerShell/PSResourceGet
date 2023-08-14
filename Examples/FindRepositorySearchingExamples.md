
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
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        TestModule  1.0.0.0            NuGetGallery 
        ```
       Should return 'TestModule' from both 'PSGallery' and 'NuGetGallery'.
       
    * When the package exists in the first repository, but not the second:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should return 'TestModule' from 'PSGallery'.

    * When the package exists in the second repository, but not the first:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            NuGetGallery 
        ```
        Should return 'TestModule' from 'NuGetGallery'.

    * When the package is in neither repository:
        ```
        Find-PSResource: Package 'TestModule' could not be found in any registered repositories.
        ```
2) Searching with a package name and a repository specified, eg: `Find-PSResource 'TestModule' -Repository PSGallery`
    * When the package exists in both repositories:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should return 'TestModule' from 'PSGallery'.

    * When the package is in the first repository, but not the second:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should return 'TestModule' from 'PSGallery'.
        
    * When the package is the second repository, but not the first:
        ```
        Find-PSResource: Package 'TestModule' could not be found.
        ```
    * When the package is in neither repository:
        ```
        Find-PSResource: Package 'TestModule' could not be found.
        ```
        
3) Searching with a package name specified and wildcard repository, eg: `Find-PSResource 'TestModule' -Repository *Gallery`
    * When the package exists in both repositories:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        TestModule  1.0.0.0            NuGetGallery 
        ```
        Should return 'TestModule' from 'PSGallery' and 'NuGetGallery'.
        
    * When the package is in the first repository, but not the second:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should return 'TestModule' from 'PSGallery'.
        
    * When the package is the second repository, but not the first:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            NuGetGallery 
        ```
        Should return 'TestModule' from 'NuGetGallery'.
        
    * When the package is in neither repository:
        ```
        Find-PSResource: Package 'TestModule' could not be found.
        ```
        
4) Searching with a package name specified and multiple repository names specified, eg: `Find-PSResource 'TestModule' -Repository PSGallery, NuGetGallery`

    * When the package exists in both repositories:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        TestModule  1.0.0.0            NuGetGallery 
        ```
        Should return 'TestModule' from 'PSGallery' and 'NuGetGallery'.
        
    * When the package is in the first repository, but not the second:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should return 'TestModule' from 'PSGallery'.
        
    * When the package is the second repository, but not the first:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            NuGetGallery 
        ```
        Should return 'TestModule' from 'NuGetGallery'.
        
    * When the package is in neither repository:
        ```
        Find-PSResource: Package 'TestModule' could not be found.
        ```
        
5) Searching with a package name specified and both a repository name specified AND a repository name with a wildcard, eg: `Find-PSResource 'TestModule' -Repository *Gallery, LocalRepo`

    * For all scenarios:
        ```
        Find-PSResource: Package 'TestModule' could not be found.
        ```