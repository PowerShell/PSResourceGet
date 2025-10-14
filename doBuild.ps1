# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

<#
.DESCRIPTION
Implement build and packaging of the package and place the output $OutDirectory/$ModuleName
#>
function DoBuild {
    Write-Verbose -Verbose -Message "Starting DoBuild for $ModuleName with configuration: $BuildConfiguration, framework: $BuildFramework"

    # Module build out path
    $BuildOutPath = [System.IO.Path]::Combine($OutDirectory, $ModuleName)
    Write-Verbose -Verbose -Message "Module output file path: '$BuildOutPath'"

    # Module build source path
    $BuildSrcPath = [System.IO.Path]::Combine($SrcPath, 'code', 'bin', $BuildConfiguration, $BuildFramework, 'publish')
    Write-Verbose -Verbose -Message "Module build source path: '$BuildSrcPath'"

    # Copy files
    $FilesToCopy = [string[]](
        # Module .psd1 file
        [System.IO.Path]::Combine($SrcPath, $ModuleName + '.psd1'),
        # Module .psm1 file
        [System.IO.Path]::Combine($SrcPath, $ModuleName + '.psm1'),
        # Module format ps1xml file
        [System.IO.Path]::Combine($SrcPath, $FormatFileName + '.ps1xml'),
        # License
        [System.IO.Path]::Combine('.', 'LICENSE'),
        # Notice
        [System.IO.Path]::Combine('.', 'Notice.txt'),
        # Group Policy files
        [System.IO.Path]::Combine($SrcPath, 'InstallPSResourceGetPolicyDefinitions.ps1'),
        [System.IO.Path]::Combine($SrcPath, 'PSResourceRepository.adml'),
        [System.IO.Path]::Combine($SrcPath, 'PSResourceRepository.admx')
    )
    foreach ($File in $FilesToCopy) {
        Write-Verbose -Message ('Copying "{0}" to "{1}"' -f $File, $BuildOutPath)
        Copy-Item -Path $File -Destination $BuildOutPath -Force
    }

    # Build and place binaries
    if (Test-Path -Path "${SrcPath}/code") {
        Write-Verbose -Verbose -Message "Building assembly and copying to '$BuildOutPath'"
        # Build code and place it in the staging location
        Push-Location "${SrcPath}/code"
        try {
            # Get dotnet.exe command path.
            $dotnetCommand = Get-Command -Name 'dotnet' -ErrorAction Ignore

            # Check for dotnet for Windows (we only build on Windows platforms).
            if ($null -eq $dotnetCommand) {
                Write-Verbose -Verbose -Message 'dotnet.exe cannot be found in current path. Looking in ProgramFiles path.'
                $dotnetCommandPath = [System.IO.Path]::Combine($env:ProgramFiles, 'dotnet', 'dotnet.exe')
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
                'Azure.Core'
                'Azure.Identity'
                'Microsoft.Bcl.AsyncInterfaces'
                'Microsoft.Extensions.FileProviders.Abstractions'
                'Microsoft.Extensions.FileSystemGlobbing'
                'Microsoft.Extensions.Primitives'
                'Microsoft.Identity.Client'
                'Microsoft.Identity.Client.Extensions.Msal'
                'Microsoft.IdentityModel.Abstractions'
                'Newtonsoft.Json'
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
                'System.Buffers'
                'System.Diagnostics.DiagnosticSource'
                'System.IO.FileSystem.AccessControl'
                'System.Memory.Data'
                'System.Memory'
                'System.Numerics.Vectors'
                'System.Runtime.CompilerServices.Unsafe'
                'System.Security.AccessControl'
                'System.Security.Cryptography.ProtectedData'
                'System.Security.Principal.Windows'
                'System.Text.Encodings.Web'
                'System.Text.Json'
                'System.Threading.Tasks.Extensions'
                'System.ValueTuple'
            )

            $buildSuccess = $true

            # Copy module binaries
            foreach ($fileName in $assemblyNames) {
                # Copy bin file
                $filePath = Join-Path -Path $BuildSrcPath -ChildPath "${fileName}.dll"
                if (-not (Test-Path -Path $filePath)) {
                    Write-Error "Expected file $filePath is missing from build output."
                    $BuildSuccess = $false
                    continue
                }

                Copy-Item -Path $filePath -Dest $BuildOutPath -Verbose -Force

                # Copy pdb file if available
                $filePathPdb = Join-Path -Path $BuildSrcPath -ChildPath "${fileName}.pdb"
                if (Test-Path -Path $filePathPdb) {
                    Copy-Item -Path $filePathPdb -Dest $BuildOutPath -Verbose -Force
                }
            }

            $depsOutputBinPath = Join-Path -Path $BuildOutPath -ChildPath 'dependencies'

            if (-not (Test-Path $depsOutputBinPath)) {
                Write-Verbose -Verbose -Message "Creating output path for dependencies: $depsOutputBinPath"
                $null = New-Item -ItemType Directory -Path $depsOutputBinPath
            }

            # Copy dependencies
            foreach ($fileName in $depAssemblyNames) {
                # Copy bin file
                $filePath = Join-Path -Path $BuildSrcPath -ChildPath "${fileName}.dll"
                if (-not (Test-Path -Path $filePath)) {
                    Write-Error "Expected file $filePath is missing from build output."
                    $BuildSuccess = $false
                    continue
                }

                Copy-Item -Path $filePath -Dest $depsOutputBinPath -Verbose -Force
            }

            if (-not $buildSuccess) {
                throw 'Build failed to create expected binaries.'
            }

            if (-not (Test-Path -Path "$BuildSrcPath/${ModuleName}.dll")) {
                throw "Expected binary was not created: $BuildSrcPath/${ModuleName}.dll"
            }
        } catch {
            Write-Verbose -Verbose -Message "dotnet build failed with error: $_"
            Write-Error "dotnet build failed with error: $_"
        } finally {
            Pop-Location
        }
    } else {
        Write-Verbose -Verbose -Message "No code to build in '${SrcPath}/code'"
    }

    ## Add build and packaging here
    Write-Verbose -Verbose -Message 'Ending DoBuild'
}
