# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

<#
.DESCRIPTION
Implement build and packaging of the package and place the output $OutDirectory/$ModuleName
#>
function DoBuild
{
    Write-Verbose -Verbose -Message "Starting DoBuild  for $ModuleName with configuration: $BuildConfiguration, framework: $BuildFramework"

    # Module build out path
    $BuildOutPath = "${OutDirectory}/${ModuleName}"
    Write-Verbose -Verbose -Message "Module output file path: '$BuildOutPath'"

    # Module build source path
    $BuildSrcPath = "bin/${BuildConfiguration}/${BuildFramework}/publish"
    Write-Verbose -Verbose -Message "Module build source path: '$BuildSrcPath'"

    # Copy module script files
    Write-Verbose -Verbose "Copy-Item ${SrcPath}/${ModuleName}.psd1 to ${OutDirectory}/${ModuleName}"
    Copy-Item -Path "${SrcPath}/${ModuleName}.psd1" -Dest "${OutDirectory}/${ModuleName}"
    Write-Verbose -Verbose "Copy Item ${SrcPath}/PSModule.psm1 to ${OutDirectory}/${ModuleName}"
    Copy-Item -Path "${SrcPath}/PSModule.psm1" -Dest "${OutDirectory}/${ModuleName}"

    # Create BuildFramework directory for binary location
    $BuildOutputBin = Join-Path -Path $BuildOutPath -ChildPath $BuildFramework
    if (! (Test-Path -Path $BuildOutputBin)) {
        Write-Verbose -Verbose "Creating output path for binaries: $BuildOutputBin"
        $null = New-Item -ItemType Directory -Path $BuildOutputBin
    }

    # Copy help
    Write-Verbose -Verbose -Message "Copying help files to '$BuildOutPath'"
    Copy-Item -Path "${HelpPath}/${Culture}" -Dest "$BuildOutPath" -Recurse

    if ( Test-Path "${SrcPath}/code" ) {
        Write-Verbose -Verbose -Message "Building assembly and copying to '$BuildOutPath'"
        # Build code and place it in the staging location
        Push-Location "${SrcPath}/code"
        try {
            # Build source
            Write-Verbose -Verbose -Message "Building with configuration: $BuildConfiguration, framework: $BuildFramework"
            Write-Verbose -Verbose -Message "Build location: PSScriptRoot: $PSScriptRoot, PWD: $pwd"
            dotnet publish --configuration $BuildConfiguration --framework $BuildFramework --output $BuildSrcPath -warnaserror
            if ($LASTEXITCODE -ne 0) {
                throw "Build failed with exit code: $LASTEXITCODE"
            }

            # Place build results
            if ($BuildFramework -eq "netstandard2.0") {
                $assemblyNames = @(
                    'PowerShellGet'
                    'Microsoft.Extensions.Logging.Abstractions'
                    'MoreLinq'
                    'NuGet.Commands'
                    'NuGet.Common'
                    'NuGet.Configuration'
                    'NuGet.Frameworks'
                    'NuGet.Packaging'
                    'NuGet.ProjectModel'
                    'NuGet.Protocol'
                    'NuGet.Repositories'
                    'NuGet.Versioning'
                )
            } elseif ($BuildFramework -eq 'net472') {
                $assemblyNames = @(
                    'PowerShellGet'
                    'Microsoft.Extensions.Logging.Abstractions'
                    'MoreLinq'
                    'NuGet.Commands'
                    'NuGet.Common'
                    'NuGet.Configuration'
                    'NuGet.Frameworks'
                    'NuGet.Packaging'
                    'NuGet.ProjectModel'
                    'NuGet.Protocol'
                    'NuGet.Repositories'
                    'NuGet.Versioning'
                    'Newtonsoft.Json'
                    'System.Security.Principal.Windows'
                )
            }

            $buildSuccess = $true
            foreach ($fileName in $assemblyNames)
            {
                # Copy bin file
                $filePath = Join-Path -Path $BuildSrcPath -ChildPath "${fileName}.dll"
                if (! (Test-Path -Path $filePath))
                {
                    Write-Error "Expected file $filePath is missing from build output."
                    $BuildSuccess = $false
                    continue
                }

                Copy-Item -Path $filePath -Dest $BuildOutputBin -Verbose -Force

                # Copy pdb file if available
                $filePathPdb = Join-Path -Path $BuildSrcPath -ChildPath "${fileName}.pdb"
                if (Test-Path -Path $filePathPdb)
                {
                    Copy-Item -Path $filePathPdb -Dest $BuildOutputBin -Verbose -Force
                }
            }

            if (! $buildSuccess)
            {
                throw "Build failed to create expected binaries."
            }

            if (! (Test-Path -Path "$BuildSrcPath/${ModuleName}.dll"))
            {
                throw "Expected binary was not created: $BuildSrcPath/${ModuleName}.dll"
            }
        }
        catch {
            # Write-Error "dotnet build failed with error: $_"
            Write-Verbose -Verbose -Message "dotnet build failed with error: $_"
        }
        finally {
            Pop-Location
        }
    }
    else {
        Write-Verbose -Verbose -Message "No code to build in '${SrcPath}/code'"
    }

    ## Add build and packaging here
    Write-Verbose -Verbose -Message "Ending DoBuild"
}
