
# Examples for `Install-PSResource` searching through repositories.

These examples will go through a number of scenarios related to `Install-PSResource` searching through repositories to install from to show what the expected outcome will be. `Install-PSResource` will install the resource from the repository with the highest priority (i.e. the smallest number) that matches the criteria specified.
In all these examples, the repositories registered and their priorities are as follows:

```
Name         Uri                                      Trusted Priority
----         ---                                      ------- --------
PSGallery    https://www.powershellgallery.com/api/v2 True    50
NuGetGallery https://api.nuget.org/v3/index.json      True    60
```

1) Installing with only a package name specified, eg: `Install-PSResource 'TestModule' -PassThru` or `Install-PSResource 'TestModule' -Repository '*' -PassThru`
    * When the package exists in both repositories:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
       Should install 'TestModule' from 'PSGallery'.
       
    * When the package exists in the first repository (PSGallery), but not the second (NuGetGallery):
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should install 'TestModule' from 'PSGallery'.

    * When the package exists in the second repository (NuGetGallery), but not the first (PSGallery):
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            NuGetGallery 
        ```
        Should install 'TestModule' from 'NuGetGallery'.

    * When the package exists in neither repository:
        ```
        Install-PSResource: Package(s) 'TestModule' could not be installed from registered repositories 'PSGallery, NuGetGallery'.
        ```
2) Installing with a package name and a repository specified, eg: `Install-PSResource 'TestModule' -Repository PSGallery -PassThru`

    * When the package exists in both repositories:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should install 'TestModule' from 'PSGallery'.

    * When the package exists in the first repository (PSGallery), but not the second (NuGetGallery):
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should install 'TestModule' from 'PSGallery'.
        
    * When the package exists in the second repository (NuGetGallery), but not the first (PSGallery):
        ```
        Install-PSResource: Package(s) 'TestModule' could not be installed from repository 'PSGallery'.
        ```
    * When the package exists in neither repository:
        ```
        Install-PSResource: Package(s) 'TestModule' could not be installed from repository 'PSGallery'.
        ```
        
3) Installing with a package name specified and wildcard repository, eg: `Install-PSResource 'TestModule' -Repository *Gallery -PassThru`
    * When the package exists in both repositories:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should install 'TestModule' from 'PSGallery'.
        
    * When the package exists in the first repository (PSSGallery), but not the second (NuGetGallery):
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should install 'TestModule' from 'PSGallery'.
        
    * When the package exists in the second repository (PSGallery), but not the first (NuGetGallery):
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            NuGetGallery 
        ```
        Should install 'TestModule' from 'NuGetGallery'.
        
    * When the package exists in neither repository:
        ```
        Install-PSResource: Package(s) 'TestModule' could not be installed from registered repositories 'PSGallery, NuGetGallery'.
        ```
        
4) Installing with a package name specified and multiple repository names specified, eg: `Install-PSResource 'TestModule' -Repository PSGallery, NuGetGallery -PassThru`

    * When the package exists in both repositories:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should install 'TestModule' from 'PSGallery'.
        
    * When the package exists in the first repository (PSGallery), but not the second (NuGetGallery):
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should install 'TestModule' from 'PSGallery'.
        
    * When the package exists in the second repository (NuGetGallery), but not the first (PSGallery):
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            NuGetGallery 
        ```
        Should install 'TestModule' from 'NuGetGallery'.
        
    * When the package exists in neither repository:
        ```
        Install-PSResource: Package(s) 'TestModule' could not be installed from registered repositories 'PSGallery, NuGetGallery'.
        ```
        
5) Installing with a package name specified and both a repository name specified AND a repository name with a wildcard, eg: `Install-PSResource 'TestModule' -Repository *Gallery, otherRepository`.
    * This scenario is not supported due to the ambiguity that arises when a repository with a wildcard in its name is specified as well as a repository with a specific name. The command will display the following error:
        ```
        Install-PSResource: Repository name with wildcard is not allowed when another repository without wildcard is specified.
        ```