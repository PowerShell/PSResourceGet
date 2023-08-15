
# Examples for `Find-PSResource` searching through repositories.

These examples will go through a number of scenarios related to `Find-PSResource` searching through repositories to show what the expected outcome will be. `Find-PSResource` will return resources from all repositories that match the criteria specified.
In all these examples, the repositories registered and their priorities are as follows:

```
Name         Uri                                      Trusted Priority
----         ---                                      ------- --------
PSGallery    https://www.powershellgallery.com/api/v2 True    50
NuGetGallery https://api.nuget.org/v3/index.json      True    60
```

Note that PSGallery has a lower priority than NuGetGallery.

## `Find-PSResource` with `-Name` parameter ##

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
        Find-PSResource: Package with name 'TestModule' could not be found in repository 'PSGallery'.
        ```
    * When the package exists in neither repository:
        ```
        Find-PSResource: Package with name 'TestModule' could not be found in repository 'PSGallery'.
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
        Find-PSResource: Package 'TestModule' could not be found in registered repositories: 'PSGallery, NuGetGallery'.
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
        
5) Searching with a package name specified and both a repository name specified AND a repository name with a wildcard, eg: `Find-PSResource 'TestModule' -Repository *Gallery, otherRepository`

    * This scenario is not supported due to the ambiguity that arises when a repository with a wildcard in its name is specified as well as a repository with a specific name. The command will display the following error:
        ```
        Find-PSResource: Repository name with wildcard is not allowed when another repository without wildcard is specified.
        ```

## `Find-PSResource` with `-Tag` parameter ##

In these examples, the package TestModule has the following tags: Tag1, Tag2.

1) Searching with only a tag specified, eg: `Find-PSResource -Tag 'Tag1'` or `Find-PSResource -Tag 'Tag1' -Repository '*'`
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
        Find-PSResource: Package with Tags 'Tag1' could not be found in any registered repositories.
        ```

    * When the package exists in both repositories and multiple existing tags are specified:

        eg: `Find-PSResource -Tag 'Tag1','Tag2'` or `Find-PSResource -Tag 'Tag1','Tag2' -Repository '*'`
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        TestModule  1.0.0.0            NuGetGallery 
        ```
       Should return 'TestModule' from both 'PSGallery' and 'NuGetGallery'.
       

    * When the package exists in both repositories and multiple tags (existing and non-existant) are specified:

        eg: `Find-PSResource -Tag 'Tag1','NonExistantTag'` or `Find-PSResource -Tag 'Tag1','NonExistantTag' -Repository '*'`
        ```
        Find-PSResource: Package with Tags 'Tag1, NonExistantTag' could not be found in any registered repositories.
        ```

2) Searching with a tag and a repository specified, eg: `Find-PSResource -Tag 'Tag1' -Repository PSGallery`
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
        Package with Tags 'Tag1' could not be found in repository 'PSGallery'.
        ```

    * When the package exists in neither repository:
        ```
        Package with Tags 'Tag1' could not be found in repository 'PSGallery'.
        ```

    * When the package exists in the first repository (PSGallery), but not the second (NuGetGallery) and multiple existing tags are specified:

        eg: `Find-PSResource -Tag 'Tag1','Tag2' -Repository PSGallery`
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
       Should return 'TestModule' from both 'PSGallery'.

    * When the package exists in the first repository (PSGallery), but not the second (NuGetGallery) and multiple tags (existing and non-existant) are specified:

        eg: `Find-PSResource -Tag 'Tag1','NonExistantTag' -Repository PSGallery`
        ```
        Find-PSResource: Package with Tags 'Tag1, NonExistantTag' could not be found in repository 'PSGallery'.
        ```

3) Searching with a tag specified and wildcard repository, eg: `Find-PSResource -Tag 'Tag1' -Repository *Gallery`
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
        Find-PSResource: Package with Tags 'Tag1' could not be found in registered repositories: 'NuGetGallery, PSGallery'.
        ```

    * When the package exists in both repositories and multiple existing tags are specified:

        eg: `Find-PSResource -Tag 'Tag1','Tag2' -Repository *Gallery`
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        TestModule  1.0.0.0            NuGetGallery 
        ```
        Should return 'TestModule' from 'PSGallery' and 'NuGetGallery'.

    * When the package exists in both repositories and multiple tags (existing and non-existant) are specified:

        eg: `Find-PSResource -Tag 'Tag1','NonExistantTag' -Repository *Gallery`
        ```
        Find-PSResource: Package with Tags 'Tag1, NonExistantTag' could not be found in registered repositories: 'PSGallery, NuGetGallery'.
        ```        
        
4) Searching with a tag specified and multiple repository names specified, eg: `Find-PSResource -Tag 'Tag1' -Repository PSGallery, NuGetGallery`

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
        
        Find-PSResource: Package with Tags 'Tag1' could not be found in repository 'NuGetGallery'.
        ```
        Should return 'TestModule' from 'PSGallery'.
       
        
    * When the package exists the second repository (NuGetGallery), but not the first (PSGallery):
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            NuGetGallery 
        
        Find-PSResource: Package with Tags 'Tag1' could not be found in repository 'PSGallery'.
        ```
        Should return 'TestModule' from 'NuGetGallery'.
        
        
    * When the package is in neither repository:
        ```
        Find-PSResource: Package with Tags 'Tag1' could not be found in repository 'PSGallery'.
        Find-PSResource: Package with Tags 'Tag1' could not be found in repository 'NuGetGallery'.
        ```

    * When the package exists in both repositories and multiple existing tags are specified:

        eg: `Find-PSResource -Tag 'Tag1','Tag2' -Repository PSGallery, NuGetGallery`
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        TestModule  1.0.0.0            NuGetGallery 
        ```
        Should return 'TestModule' from 'PSGallery' and 'NuGetGallery'.

    * When the package exists in both repositories and multiple tags (existing and non-existant) are specified:

        eg: `Find-PSResource -Tag 'Tag1','NonExistantTag' -Repository PSGallery, NuGetGallery`
        ```
        Find-PSResource: Package with Tags 'Tag1, NonExistantTag' could not be found in registered repositories: 'PSGallery, NuGetGallery'.
        ```        
        
5) Searching with a tag specified and both a repository name specified AND a repository name with a wildcard, eg: `Find-PSResource -Tag 'Tag1' -Repository *Gallery, otherRepository`

    * This scenario is not supported due to the ambiguity that arises when a repository with a wildcard in its name is specified as well as a repository with a specific name. The command will display the following error:
        ```
        Find-PSResource: Repository name with wildcard is not allowed when another repository without wildcard is specified.
        ```

## `Find-PSResource` with `-CommandName` parameter ##

In these examples, the package TestModule has the following command names (i.e tag prepended with "PSCommand_"): Get-MyCommand.

1) Searching with only a tag specified, eg: `Find-PSResource -CommandName 'Get-MyCommand'` or `Find-PSResource -CommandName 'Get-MyCommand' -Repository '*'`
    * When the package exists in both repositories:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
       Should return 'TestModule' from both 'PSGallery'. Since searching with `-CommandName` for NuGetGallery repository, it will be skipped.

    * When the package exists in the first repository (PSGallery), but not the second (NuGetGallery):
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should return 'TestModule' from 'PSGallery'. Since searching with `-CommandName` for NuGetGallery repository, it will be skipped.

    * When the package exists in the second repository (NuGetGallery), but not the first (PSGallery):
        ```
        Find-PSResource: Package with CommandName 'Get-TargetResource' could not be found in any registered repositories.
        ```
        Since searching with `-CommandName` for NuGetGallery repository, it will be skipped.

    * When the package exists in neither repository:
        ```
        Find-PSResource: Package with CommandName 'Get-TargetResource' could not be found in any registered repositories.
        ```
        Since searching with `-CommandName` for NuGetGallery repository, it will be skipped.

    * When the package exists in both repositories and multiple existing Command names are specified:

        eg: `Find-PSResource -CommandName 'Get-MyCommand1','Get-MyCommand2'` or `Find-PSResource -CommandName 'Get-MyCommand1','Get-MyCommand2' -Repository '*'`
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery
        ```
       Should return 'TestModule' from both 'PSGallery'. Since searching with `-CommandName` for NuGetGallery repository, it will be skipped.
       

    * When the package exists in both repositories and multiple tags (existing and non-existant) are specified:

        eg: `Find-PSResource -CommandName 'Get-MyCommand1','NonExistantCommand'` or `Find-PSResource -CommandName 'Get-MyCommand1','NonExistantCommand' -Repository '*'`
        ```
        Find-PSResource: Package with CommandName 'Get-MyCommand1, NonExistantCommand' could not be found in any registered repositories.
        ```

2) Searching with a tag and a repository specified, eg: `Find-PSResource -CommandName 'Get-MyCommand1' -Repository PSGallery`
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
        Package with CommandName 'Get-MyCommand1' could not be found in repository 'PSGallery'.
        ```

    * When the package exists in neither repository:
        ```
        Package with CommandName 'Get-MyCommand1' could not be found in repository 'PSGallery'.
        ```

    * When the package exists in the first repository (PSGallery), but not the second (NuGetGallery) and multiple existing tags are specified:

        eg: `Find-PSResource -CommandName 'Get-MyCommand1','Get-MyCommand2' -Repository PSGallery`
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
       Should return 'TestModule' from both 'PSGallery'.

    * When the package exists in the first repository (PSGallery), but not the second (NuGetGallery) and multiple tags (existing and non-existant) are specified:

        eg: `Find-PSResource -CommandName 'Get-MyCommand1','NonExistantCommand' -Repository PSGallery`
        ```
        Find-PSResource: Package with CommandName 'Get-MyCommand1, Get-MyCommand2' could not be found in repository 'PSGallery'.
        ```

3) Searching with a tag specified and wildcard repository, eg: `Find-PSResource -CommandName 'Get-MyCommand1' -Repository *Gallery`
    * When the package exists in both repositories:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery
        ```
        Should return 'TestModule' from 'PSGallery'. Since searching with `-CommandName` for NuGetGallery repository, it will be skipped.
        
    * When the package exists in the first repository (PSGallery), but not the second (NuGetGallery):
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        ```
        Should return 'TestModule' from 'PSGallery'.
        
    * When the package exists in the second repository (NuGetGallery), but not the first (PSGallery):
        ```
        Find-PSResource: Package with CommandName 'Get-TargetResource' could not be found in any registered repositories.
        ```
        Since searching with `-CommandName` for NuGetGallery repository, it will be skipped.
        
    * When the package exists in neither repository:
        ```
        Find-PSResource: Package with CommandName 'Get-MyCommand1' could not be found in registered repositories: 'PSGallery, NuGetGallery'.
        ```

    * When the package exists in both repositories and multiple existing tags are specified:

        eg: `Find-PSResource -CommandName 'Get-MyCommand1','Tag2' -Repository *Gallery`
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 
        TestModule  1.0.0.0            NuGetGallery 
        ```
        Should return 'TestModule' from 'PSGallery' and 'NuGetGallery'.

    * When the package exists in both repositories and multiple tags (existing and non-existant) are specified:

        eg: `Find-PSResource -Tag 'Tag1','NonExistantTag' -Repository *Gallery`
        ```
        Find-PSResource: Package with Tags 'Tag1, NonExistantTag' could not be found in registered repositories: 'PSGallery, NuGetGallery'.
        ```        
        
4) Searching with a tag specified and multiple repository names specified, eg: `Find-PSResource -CommandName 'Get-MyCommand1' -Repository PSGallery, NuGetGallery`

    * When the package exists in both repositories:
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery
        ```
        Should return 'TestModule' from 'PSGallery'. Since searching with `-CommandName` for NuGetGallery repository, it will be skipped.
        
    * When the package exists in the first repository (PSGallery), but not the second (NuGetGallery):
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery 

        Find-PSResource: Find by CommandName or DSCResource is not supported for the V3 server protocol repository 'NuGetGallery'.
        ```
        Should return 'TestModule' from 'PSGallery'. Since searching with `-CommandName` for NuGetGallery repository, it will not be searched and error written out.
        
    * When the package exists the second repository (NuGetGallery), but not the first (PSGallery):
        ```        
        Find-PSResource: Find by CommandName or DSCResource is not supported for the V3 server protocol repository 'NuGetGallery'.
        ```
        Since searching with `-CommandName` for NuGetGallery repository, it will not be searched and error written out.
        
        
    * When the package is in neither repository:
        ```
        Find-PSResource: Package with Command 'Get-MyCommand1' could not be found in repository 'PSGallery'.
        Find-PSResource: Find by CommandName or DSCResource is not supported for the V3 server protocol repository 'NuGetGallery'.
        ```
        Since searching with `-CommandName` for NuGetGallery repository, it will not be searched and error written out.

    * When the package exists in both repositories and multiple existing Command names are specified:

        eg: `Find-PSResource -CommandName 'Get-MyCommand1','Get-MyCommand2' -Repository PSGallery, NuGetGallery`
        ```
        Name        Version Prerelease Repository
        ----        ------- ---------- ----------
        TestModule  1.0.0.0            PSGallery

        Find-PSResource: Find by CommandName or DSCResource is not supported for the V3 server protocol repository 'NuGetGallery'.
        ```
        Since searching with `-CommandName` for NuGetGallery repository, it will not be searched and error written out.


    * When the package exists in both repositories and multiple Command names (existing and non-existant) are specified:

        eg: `Find-PSResource -CommandName 'Get-MyCommand1','NonExistantCommand' -Repository PSGallery, NuGetGallery`
        ```
        Find-PSResource: Package with Command 'Get-MyCommand1' could not be found in repository 'PSGallery'.
        Find-PSResource: Find by CommandName or DSCResource is not supported for the V3 server protocol repository 'NuGetGallery'.
        ```
        Since searching with `-CommandName` for NuGetGallery repository, it will not be searched and error written out.
        
5) Searching with a tag specified and both a repository name specified AND a repository name with a wildcard, eg: `Find-PSResource -CommandName 'Get-MyCommand1' -Repository *Gallery, otherRepository`

    * This scenario is not supported due to the ambiguity that arises when a repository with a wildcard in its name is specified as well as a repository with a specific name. The command will display the following error:
        ```
        Find-PSResource: Repository name with wildcard is not allowed when another repository without wildcard is specified.
        ```