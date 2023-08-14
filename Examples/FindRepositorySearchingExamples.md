
# Examples for `Find-PSResource` searching through repositories.

These examples will go through a number of scenarios related to `Find-PSResource` searching through repositories to show what the expected outcome will be. `Find-PSResource` will return all resources that match the criteria specified.
In all these examples, the repositories registered and their priorities are as follows:

```
Name         Uri                                      Trusted Priority
----         ---                                      ------- --------
PSGallery    https://www.powershellgallery.com/api/v2 True    50
NuGetGallery https://api.nuget.org/v3/index.json      True    60
```

Note that PSGallery is a lower priority than NuGetGallery.

1) Searching with only a package name specified, eg: `Find-PSResource 'TestModule'` or `Find-PSResource 'TestModule' -Repository '*'`
    * When the package exists in both repositories:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        TestModule  1.0.0.0            NuGetGallery 
        ```
       Should return 'TestModule' from both 'PSGallery' and 'NuGetGallery'.
       
    * When the package exists in the first repository (PSGallery), but not the second (NuGetGallery):
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should return 'TestModule' from 'PSGallery'.

    * When the package exists in the second repository (NuGetGallery), but not the first (PSGallery):
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            NuGetGallery 
        ```
        Should return 'TestModule' from 'NuGetGallery'.

    * When the package exists in neither repository:
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

    * When the package exists in the first repository (PSGallery), but not the second (NuGetGallery):
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should return 'TestModule' from 'PSGallery'.
        
    * When the package exists in the second repository (NuGetGallery), but not the first (PSGallery):
        ```
        Package with name 'TestModule' could not be found in repository 'PSGallery'.
        ```
    * When the package exists in neither repository:
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
        
    * When the package exists in the first repository (PSGallery), but not the second (NuGetGallery):
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should return 'TestModule' from 'PSGallery'.
        
    * When the package exists in the second repository (NuGetGallery), but not the first (PSGallery):
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            NuGetGallery 
        ```
        Should return 'TestModule' from 'NuGetGallery'.
        
    * When the package exists in neither repository:
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
        
    * When the package exists in the first repository (PSGallery), but not the second (NuGetGallery):
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        
        Find-PSResource: Package with name 'TestModule' could not be found in repository 'NuGetGallery'.
        ```
        Should return 'TestModule' from 'PSGallery'.
       
        
    * When the package exists the second repository (NuGetGallery), but not the first (PSGallery):
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            NuGetGallery 
        
        Find-PSResource: Package with name 'TestModule' could not be found in repository 'PSGallery'.
        ```
        Should return 'TestModule' from 'NuGetGallery'.
        
        
    * When the package is in neither repository:
        ```
        Find-PSResource: Package with name 'TestModule' could not be found in repository 'PSGallery'.
        Find-PSResource: Package with name 'TestModule' could not be found in repository 'NuGetGallery'.
        ```
        
5) Searching with a package name specified and both a repository name specified AND a repository name with a wildcard, eg: `Find-PSResource 'TestModule' -Repository *Gallery, PSGallery`

    * This scenario is not supported due to the ambiguity that arises when a repository with a wildcard in its name is specified as well as a repository with a specific name. The command will display the following error:
        ```
        Find-PSResource: Repository name with wildcard is not allowed when another repository without wildcard is specified.
        ```
