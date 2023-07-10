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

    # Copy module .psd1 file
    Write-Verbose -Verbose "Copy-Item ${SrcPath}/${ModuleName}.psd1 to $BuildOutPath"
    Copy-Item -Path "${SrcPath}/${ModuleName}.psd1" -Dest "$BuildOutPath" -Force

    # Copy module .psm1 file
    Write-Verbose -Verbose "Copy-Item ${SrcPath}/${ModuleName}.psm1 to $BuildOutPath"
    Copy-Item -Path "${SrcPath}/${ModuleName}.psm1" -Dest "$BuildOutPath" -Force

    #Copy module format ps1xml file
    Write-Verbose -Verbose -Message "Copy-Item ${SrcPath}/${FormatFileName}.ps1xml to $BuildOutPath"
    Copy-Item -Path "${SrcPath}/${FormatFileName}.ps1xml" -Dest "$BuildOutPath" -Force

    # Copy license
    Write-Verbose -Verbose -Message "Copying LICENSE file to '$BuildOutPath'"
    Copy-Item -Path "./LICENSE" -Dest "$BuildOutPath"

    # Copy notice
    Write-Verbose -Verbose -Message "Copying ThirdPartyNotices.txt to '$BuildOutPath'"
    Copy-Item -Path "./Notice.txt" -Dest "$BuildOutPath"

    # Build and place binaries
    if ( Test-Path "${SrcPath}/code" ) {
        Write-Verbose -Verbose -Message "Building assembly and copying to '$BuildOutPath'"
        # Build code and place it in the staging location
        Push-Location "${SrcPath}/code"
        try {
            # Get dotnet.exe command path.
            $dotnetCommand = Get-Command -Name 'dotnet' -ErrorAction Ignore

            # Check for dotnet for Windows (we only build on Windows platforms).
            if ($null -eq $dotnetCommand) {
                Write-Verbose -Verbose -Message "dotnet.exe cannot be found in current path. Looking in ProgramFiles path."
                $dotnetCommandPath = Join-Path -Path $env:ProgramFiles -ChildPath "dotnet\dotnet.exe"
                $dotnetCommand = Get-Command -Name $dotnetCommandPath -ErrorAction Ignore
                if ($null -eq $dotnetCommand) {
                    throw "Dotnet.exe cannot be found: $dotnetCommandPath is unavailable for build."
                }
            }

            Write-Verbose -Verbose -Message "dotnet.exe command found in path: $($dotnetCommand.Path)"

            # Check dotnet version
            Write-Verbose -Verbose -Message "DotNet version: $(& ($dotnetCommand) --version)"

            # Build source
            Write-Verbose -Verbose -Message "Build location: PSScriptRoot: $PSScriptRoot, PWD: $pwd"
            $buildCommand = "$($dotnetCommand.Name) publish --configuration $BuildConfiguration --framework $BuildFramework --output $BuildSrcPath"
            Write-Verbose -Verbose -Message "Starting dotnet build command: $buildCommand"
            Invoke-Expression -Command $buildCommand
            if ($LASTEXITCODE -ne 0) {
                throw "Build failed with exit code: $LASTEXITCODE"
            }

            # Place build results
            $assemblyNames = @(
                'Microsoft.PowerShell.PSResourceGet'
            )

            $depAssemblyNames = @(
                'NuGet.Commands'
                'NuGet.Common'
                'NuGet.Configuration'
                'NuGet.Credentials'
                'NuGet.DependencyResolver.Core'
                'NuGet.Frameworks'
                'NuGet.LibraryModel'
                'NuGet.Packaging'
                'NuGet.ProjectModel'
                'NuGet.Protocol'
                'NuGet.Versioning'
                'Newtonsoft.Json'
                'System.Text.Json'
                'System.Buffers'
                'System.Memory'
                'System.Numerics.Vectors'
                'System.Runtime.CompilerServices.Unsafe'
                'System.Text.Encodings.Web'
                'System.Text.Json'
                'System.Threading.Tasks.Extensions'
                'Microsoft.Bcl.AsyncInterfaces'
                'System.ValueTuple'
            )

            $buildSuccess = $true

            # Copy module binaries
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

                Copy-Item -Path $filePath -Dest $BuildOutPath -Verbose -Force

                # Copy pdb file if available
                $filePathPdb = Join-Path -Path $BuildSrcPath -ChildPath "${fileName}.pdb"
                if (Test-Path -Path $filePathPdb)
                {
                    Copy-Item -Path $filePathPdb -Dest $BuildOutPath -Verbose -Force
                }
            }

            $depsOutputBinPath = Join-Path -Path $BuildOutPath -ChildPath "dependencies"

            if (-not (Test-Path $depsOutputBinPath)) {
                Write-Verbose -Verbose -Message "Creating output path for dependencies: $depsOutputBinPath"
                $null = New-Item -ItemType Directory -Path $depsOutputBinPath
            }

            # Copy dependencies
            foreach ($fileName in $depAssemblyNames)
            {
                # Copy bin file
                $filePath = Join-Path -Path $BuildSrcPath -ChildPath "${fileName}.dll"
                if (! (Test-Path -Path $filePath))
                {
                    Write-Error "Expected file $filePath is missing from build output."
                    $BuildSuccess = $false
                    continue
                }

                Copy-Item -Path $filePath -Dest $depsOutputBinPath -Verbose -Force
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
            Write-Verbose -Verbose -Message "dotnet build failed with error: $_"
            Write-Error "dotnet build failed with error: $_"
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
