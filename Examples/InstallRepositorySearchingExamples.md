
# Examples for `Install-PSResource` searching through repositories.

These examples will go through a number of scenarios related to `Install-PSResource` searching through repositories to show what the expected outcome will be.
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
       
    * When the package is in the first repository, but not the second:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should install 'TestModule' from 'PSGallery'.

    * When the package is the second repository, but not the first:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            NuGetGallery 
        ```
        Should install 'TestModule' from 'NuGetGallery'.

    * When the package is in neither repository:
        ```
        Install-PSResource: Package 'TestModule' could not be found.
        ```
2) Installing with a package name and a repository specified, eg: `Install-PSResource 'TestModule' -Repository PSGallery -PassThru`

    * When the package exists in both repositories:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should install 'TestModule' from 'PSGallery'.

    * When the package is in the first repository, but not the second:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should install 'TestModule' from 'PSGallery'.
        
    * When the package is the second repository, but not the first:
        ```
        Install-PSResource: Package 'TestModule' could not be found.
        ```
    * When the package is in neither repository:
        ```
        Install-PSResource: Package 'TestModule' could not be found.
        ```
        
3) Installing with a package name specified and wildcard repository, eg: `Install-PSResource 'TestModule' -Repository *Gallery -PassThru`
    * When the package exists in both repositories:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should install 'TestModule' from 'PSGallery'.
        
    * When the package is in the first repository, but not the second:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should install 'TestModule' from 'PSGallery'.
        
    * When the package is the second repository, but not the first:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            NuGetGallery 
        ```
        Should install 'TestModule' from 'NuGetGallery'.
        
    * When the package is in neither repository:
        ```
        Install-PSResource: Package 'TestModule' could not be found.
        ```
        
4) Installing with a package name specified and multiple repository names specified, eg: `Install-PSResource 'TestModule' -Repository PSGallery, NuGetGallery -PassThru`

    * When the package exists in both repositories:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should install 'TestModule' from 'PSGallery'.
        
    * When the package is in the first repository, but not the second:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should install 'TestModule' from 'PSGallery'.
        
    * When the package is the second repository, but not the first:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            NuGetGallery 
        ```
        Should install 'TestModule' from 'NuGetGallery'.
        
    * When the package is in neither repository:
        ```
        Install-PSResource: Package 'TestModule' could not be found.
        ```
        
5) Installing with a package name specified and both a repository name specified AND a repository name with a wildcard, eg: `Install-PSResource 'TestModule' -Repository *Gallery, LocalRepo`.
    * For all scenarios:
        ```
        Install-PSResource: Package 'TestModule' could not be found.
        ```