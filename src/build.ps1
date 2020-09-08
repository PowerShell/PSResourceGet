param(
    [ValidateSet("netstandard2.0")]
    [string]$Framework = "netstandard2.0",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$EmbedProviderManifest
)

Function Test-DotNetRestore {
    param(
        [string] $projectPath
    )
    Test-Path (Join-Path $projectPath 'project.lock.json')
}

Function CopyToDestinationDir($itemsToCopy, $destination) {
    if (-not (Test-Path $destination)) {
        New-Item -ItemType Directory $destination -Force
    }
    foreach ($file in $itemsToCopy) {
        if (Test-Path $file) {
            Copy-Item -Path $file -Destination (Join-Path $destination (Split-Path $file -Leaf)) -Verbose -Force
        }
    }
}

Function CopyBinariesToDestinationDir($itemsToCopy, $destination, $framework, $configuration, $ext, $solutionDir) {
    if (-not (Test-Path $destination)) {
        Write-Host ("$destination does not yet exist")
        $null = New-Item -ItemType Directory $destination -Force
    }
    foreach ($file in $itemsToCopy) {
        # Set by AppVeyor
        $platform = [System.Environment]::GetEnvironmentVariable('platform')
        if (-not $platform) {
            # If not set at all, try Any CPU
            $platform = 'Any CPU'
        }

        Write-Host("item to copy is: $file")

        $fullPath = Join-Path -Path $solutionDir -ChildPath 'bin' | Join-Path -ChildPath $configuration | Join-Path -ChildPath $framework | Join-Path -ChildPath "$file$ext"
        Write-Host("Full path is: $fullPath")

        $fullPathWithPlatform = Join-Path -Path $solutionDir -ChildPath 'bin' | Join-Path -ChildPath $platform | Join-Path -ChildPath $configuration | Join-Path -ChildPath $framework | Join-Path -ChildPath "$file$ext"
        Write-Host("Full path with platform is: $fullPathWithPlatform")

        if (Test-Path $fullPath) {
            Write-Host("In full path, copying item over to:  $(Join-Path $destination "$file$ext")")

            Copy-Item -Path $fullPath -Destination (Join-Path $destination "$file$ext") -Verbose -Force
        }
        elseif (Test-Path $fullPathWithPlatform) {
            Write-Host("In full path with platform, copying item over to:  $(Join-Path $destination "$file$ext")")

            Copy-Item -Path $fullPathWithPlatform -Destination (Join-Path $destination "$file$ext") -Verbose -Force
        }
        else {
            return $false
        }
    }
    return $true
}

$currentFramework = $framework

$solutionPath = Split-Path $MyInvocation.InvocationName
$solutionDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($solutionPath)

write-host ("solutionDir: $($solutionDir)")


$assemblyNames = @(
    "PowerShellGet"
)



$itemsToCopyBinaries = $assemblyNames | % { "$solutionDir\$_\bin\$Configuration\$currentFramework\$_.dll" }
$itemsToCopyPdbs = $assemblyNames | % { "$solutionDir\$_\bin\$Configuration\$currentFramework\$_.pdb" }

$projectPath = Split-Path $solutionDir -Parent
$itemsToCopyCommon = @("$projectPath/PowerShellGet.psd1","$projectPath/PSModule.psm1")

$destinationDir = "$solutionDir/out/PowerShellGet"

$destinationDirBinaries = "$destinationDir/$currentFramework"

try {
    Push-Location $solutionPath
    dotnet restore
    dotnet build --configuration $Configuration
    dotnet publish --framework $framework --configuration $Configuration
}
finally {
    Pop-Location
}

CopyToDestinationDir $itemsToCopyCommon $destinationDir

if (-not (CopyBinariesToDestinationDir $assemblyNames $destinationDirBinaries $currentFramework $Configuration '.dll' $solutionDir)) {
    throw 'Build failed'
}

CopyBinariesToDestinationDir $assemblyNames $destinationDirBinaries $currentFramework $Configuration '.pdb' $solutionDir

#Packing
$sourcePath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($destinationDir)
$packagePath = Split-Path -Path $sourcePath
$zipFilePath = Join-Path $packagePath "PowerShellGet.zip"

Write-Host("Zip file path is: $zipFilePath")

$packageFileName = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($zipFilePath)
Write-Host("Zip package file name is: $packageFileName")

if (test-path $packageFileName) {
    Write-Host("In test path:  $packageFileName")
    Remove-Item $packageFileName -force
}

Add-Type -assemblyname System.IO.Compression.FileSystem
Write-Verbose "Zipping $sourcePath into $packageFileName" -verbose
[System.IO.Compression.ZipFile]::CreateFromDirectory($sourcePath, $packageFileName)
