param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $root ".dotnet\dotnet.exe"
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { "dotnet" }
$artifactsRoot = Join-Path $root "artifacts"
$deploymentRoot = $root
try {
    $gitCommonDir = (& git -C $root rev-parse --git-common-dir 2>$null | Select-Object -First 1)
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($gitCommonDir)) {
        $gitCommonPath = if ([System.IO.Path]::IsPathRooted($gitCommonDir)) {
            [System.IO.Path]::GetFullPath($gitCommonDir)
        } else {
            [System.IO.Path]::GetFullPath((Join-Path $root $gitCommonDir))
        }
        if ([System.IO.Path]::GetFileName($gitCommonPath) -eq ".git") {
            $deploymentRoot = Split-Path -Parent $gitCommonPath
        }
    }
} catch {
    # Non-Git source archives keep deploying beside the build script.
}
$deploymentArtifactsRoot = Join-Path $deploymentRoot "artifacts"
$canonical = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    Join-Path $deploymentArtifactsRoot "plugin"
} elseif ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
} else {
    Join-Path $root $OutputDirectory
}
$isCanonicalDeploy = [string]::IsNullOrWhiteSpace($OutputDirectory)
$stagingRoot = Join-Path $artifactsRoot ".staging"
$staging = Join-Path $stagingRoot ("plugin-" + [Guid]::NewGuid().ToString("N"))
$packageBuildStaging = Join-Path $stagingRoot ("yak-source-" + [Guid]::NewGuid().ToString("N"))
$packageOutputStaging = Join-Path $stagingRoot ("packages-" + [Guid]::NewGuid().ToString("N"))
$pluginBaseName = "ParametricFasteners"
$productDisplayName = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String("5Y+C5pWw5YyW57Sn5Zu65Lu2"))
$installReadmeName = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String("5a6J6KOF6K+05piOLm1k"))
$installLauncherName = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String("5a6J6KOF5Y+C5pWw5YyW57Sn5Zu65Lu2LmNtZA=="))
$offlineSuffix = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String("56a757q/5a6J6KOF"))
$diagnosticLauncherName = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String("6K+K5pat5a6J6KOFLmNtZA=="))
$packageVersion = "0.38.7"
$env:DOTNET_CLI_HOME = Join-Path $root ".tools\dotnet-home"
$env:NUGET_PACKAGES = Join-Path $root ".tools\nuget"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

function Assert-InArtifacts([string]$path, [string]$allowedArtifactsRoot = $artifactsRoot) {
    $rootPath = [System.IO.Path]::GetFullPath($allowedArtifactsRoot).TrimEnd('\') + '\'
    $candidate = [System.IO.Path]::GetFullPath($path).TrimEnd('\') + '\'
    if (-not $candidate.StartsWith($rootPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the workspace artifacts directory: $path"
    }
}

function Assert-NotLocked([string]$directory) {
    if (-not (Test-Path -LiteralPath $directory)) { return }
    foreach ($file in Get-ChildItem -LiteralPath $directory -Recurse -File) {
        try {
            $stream = [System.IO.File]::Open($file.FullName, 'Open', 'ReadWrite', 'None')
            $stream.Dispose()
        } catch {
            throw "The installed plugin is locked: $($file.FullName). Close Rhino and run the build again; the installed directory was not modified."
        }
    }
}

function Find-Yak {
    $candidates = @(
        "E:\Program Files\Rhino 8\System\Yak.exe",
        "C:\Program Files\Rhino 8\System\Yak.exe",
        (Join-Path $env:ProgramFiles "Rhino 8\System\Yak.exe")
    ) | Select-Object -Unique
    return $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

function Assert-YakPackage([System.IO.FileInfo]$yakFile) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($yakFile.FullName)
    try {
        $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        $rhpEntries = @($entries | Where-Object { $_ -match '^[^/]+\.rhp$' })
        if ($rhpEntries.Count -ne 1 -or $rhpEntries[0] -ne "$pluginBaseName.rhp") {
            throw "Yak must contain exactly one top-level $pluginBaseName.rhp. Found: $($rhpEntries -join ', ')"
        }
        foreach ($required in @(
            "manifest.yml",
            "$pluginBaseName.rhp",
            "$pluginBaseName.rui",
            "RhinoMM.Core.dll"
        )) {
            if ($entries -notcontains $required) { throw "Yak package is missing $required." }
        }
        $legacyRhp = $productDisplayName + ".rhp"
        $legacyRui = $productDisplayName + ".rui"
        if ($entries -contains $legacyRhp -or $entries -contains $legacyRui) {
            throw "Yak package contains legacy non-ASCII RHP/RUI payload names."
        }
        if ([IO.Path]::GetFileNameWithoutExtension("$pluginBaseName.rhp") -ne
            [IO.Path]::GetFileNameWithoutExtension("$pluginBaseName.rui")) {
            throw "RHP and RUI base names must match."
        }
    } finally {
        $archive.Dispose()
    }

    $fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $packageBuildStaging "$pluginBaseName.rhp")).ProductVersion
    if ([string]::IsNullOrWhiteSpace($fileVersion) -or -not $fileVersion.StartsWith($packageVersion, [StringComparison]::Ordinal)) {
        throw "RHP product version '$fileVersion' does not match package version '$packageVersion'."
    }
    $manifestText = Get-Content -LiteralPath (Join-Path $packageBuildStaging "manifest.yml") -Raw
    if ($manifestText -notmatch "(?m)^version:\s*$([regex]::Escape($packageVersion))\s*$" -or
        $manifestText -notmatch "guid:ddc747eb-360e-4629-b65b-6bb1ddb4dc8f") {
        throw "Yak manifest version or plugin GUID keyword is invalid."
    }
    $assemblyInfo = Get-Content -LiteralPath (Join-Path $root "src\RhinoMM.Plugin\Properties\AssemblyInfo.cs") -Raw
    if ($assemblyInfo -notmatch '(?i)Guid\("DDC747EB-360E-4629-B65B-6BB1DDB4DC8F"\)') {
        throw "The RHP assembly GUID does not match the Yak manifest GUID keyword."
    }
}

function Replace-DirectoryAtomically([string]$source, [string]$target, [string]$allowedRoot) {
    Assert-InArtifacts $source $artifactsRoot
    Assert-InArtifacts $target $allowedRoot
    $old = $null
    if (Test-Path -LiteralPath $target) {
        Assert-NotLocked $target
        $old = Join-Path $stagingRoot ("old-packages-" + [Guid]::NewGuid().ToString("N"))
        Assert-InArtifacts $old $artifactsRoot
        Move-Item -LiteralPath $target -Destination $old
    }
    try {
        $parent = Split-Path -Parent $target
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
        Move-Item -LiteralPath $source -Destination $target
        if ($old -and (Test-Path -LiteralPath $old)) {
            Assert-InArtifacts $old $artifactsRoot
            Remove-Item -LiteralPath $old -Recurse -Force
        }
    } catch {
        if ($old -and (Test-Path -LiteralPath $old) -and -not (Test-Path -LiteralPath $target)) {
            Move-Item -LiteralPath $old -Destination $target
        }
        throw
    }
}

New-Item -ItemType Directory -Force -Path $staging | Out-Null
New-Item -ItemType Directory -Force -Path $packageBuildStaging | Out-Null
New-Item -ItemType Directory -Force -Path $packageOutputStaging | Out-Null
try {
    & $dotnet restore (Join-Path $root "RhinoMM.sln")
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }
    & $dotnet test (Join-Path $root "tests\RhinoMM.Core.Tests\RhinoMM.Core.Tests.csproj") --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Core tests failed." }
    & $dotnet build (Join-Path $root "src\RhinoMM.Plugin\RhinoMM.Plugin.csproj") --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Plugin build failed." }

    $output = Join-Path $root "src\RhinoMM.Plugin\bin\$Configuration\net7.0-windows"
    $rhp = Join-Path $output ($pluginBaseName + ".rhp")
    if (-not (Test-Path -LiteralPath $rhp)) { throw "The plugin RHP was not produced." }
    Copy-Item -LiteralPath $rhp -Destination $staging -Force
    Copy-Item -LiteralPath (Join-Path $output "RhinoMM.Core.dll") -Destination $staging -Force
    Copy-Item -LiteralPath (Join-Path $root "packaging\manifest.yml") -Destination $staging -Force
    Copy-Item -LiteralPath (Join-Path $root "THIRD_PARTY_NOTICES.md") -Destination $staging -Force
    $windowsPowerShell = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
    & $windowsPowerShell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "generate-toolbar.ps1") -OutputPath (Join-Path $staging ($pluginBaseName + ".rui"))
    if ($LASTEXITCODE -ne 0) { throw "Toolbar generation failed." }

    $compatCandidates = @(
        "E:\Program Files\Rhino 8\System\Compat.exe",
        "C:\Program Files\Rhino 8\System\Compat.exe"
    )
    $compat = $compatCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if ($compat) {
        $rhinoCommon = Join-Path (Split-Path -Parent $compat) "RhinoCommon.dll"
        & $compat --quiet (Join-Path $staging ($pluginBaseName + ".rhp")) $rhinoCommon
        if ($LASTEXITCODE -ne 0) { throw "Rhino compat.exe check failed." }
    } else {
        Write-Warning "Rhino Compat.exe was not found; compatibility check skipped."
    }

    $yak = Find-Yak
    if (-not $yak) { throw "Rhino 8 Yak.exe was not found; release package cannot be built." }
    Copy-Item -LiteralPath (Join-Path $staging ($pluginBaseName + ".rhp")) -Destination $packageBuildStaging -Force
    Copy-Item -LiteralPath (Join-Path $staging ($pluginBaseName + ".rui")) -Destination $packageBuildStaging -Force
    Copy-Item -LiteralPath (Join-Path $staging "RhinoMM.Core.dll") -Destination $packageBuildStaging -Force
    Copy-Item -LiteralPath (Join-Path $staging "manifest.yml") -Destination $packageBuildStaging -Force
    Copy-Item -LiteralPath (Join-Path $staging "THIRD_PARTY_NOTICES.md") -Destination $packageBuildStaging -Force
    Copy-Item -LiteralPath (Join-Path $root ("packaging\" + $installReadmeName)) -Destination $packageBuildStaging -Force

    Push-Location $packageBuildStaging
    try {
        & $yak build --platform win --version $packageVersion
        if ($LASTEXITCODE -ne 0) { throw "Yak package build failed." }
    } finally {
        Pop-Location
    }
    $yakFile = Get-ChildItem -LiteralPath $packageBuildStaging -Filter "parametric-fasteners-$packageVersion-*.yak" -File | Select-Object -First 1
    if (-not $yakFile) { throw "Yak did not produce the expected package." }
    if ($yakFile.Name -notmatch "-rh8_18-win\.yak$") {
        throw "Unexpected Yak distribution tag '$($yakFile.Name)'; expected Rhino 8.18 Windows."
    }
    Assert-YakPackage $yakFile
    Copy-Item -LiteralPath $yakFile.FullName -Destination $packageOutputStaging -Force

    $offlineName = "$productDisplayName-$packageVersion-$offlineSuffix"
    $offlineStaging = Join-Path $stagingRoot ("offline-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force -Path $offlineStaging | Out-Null
    try {
        Copy-Item -LiteralPath $yakFile.FullName -Destination $offlineStaging -Force
        Copy-Item -LiteralPath (Join-Path $root ("packaging\" + $installLauncherName)) -Destination $offlineStaging -Force
        Copy-Item -LiteralPath (Join-Path $root "packaging\install.ps1") -Destination $offlineStaging -Force
        Copy-Item -LiteralPath (Join-Path $root "packaging\diagnose-install.ps1") -Destination $offlineStaging -Force
        Copy-Item -LiteralPath (Join-Path $root ("packaging\" + $diagnosticLauncherName)) -Destination $offlineStaging -Force
        Copy-Item -LiteralPath (Join-Path $root ("packaging\" + $installReadmeName)) -Destination $offlineStaging -Force
        $hash = Get-FileHash -LiteralPath (Join-Path $offlineStaging $yakFile.Name) -Algorithm SHA256
        [IO.File]::WriteAllText(
            (Join-Path $offlineStaging "SHA256SUMS.txt"),
            "$($hash.Hash.ToLowerInvariant())  $($yakFile.Name)$([Environment]::NewLine)",
            (New-Object Text.UTF8Encoding($false)))
        $offlineZip = Join-Path $packageOutputStaging ($offlineName + ".zip")
        Compress-Archive -Path (Join-Path $offlineStaging "*") -DestinationPath $offlineZip -CompressionLevel Optimal
        $zipHash = Get-FileHash -LiteralPath $offlineZip -Algorithm SHA256
        [IO.File]::WriteAllText(
            (Join-Path $packageOutputStaging "SHA256SUMS.txt"),
            "$($hash.Hash.ToLowerInvariant())  $($yakFile.Name)$([Environment]::NewLine)$($zipHash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($offlineZip))$([Environment]::NewLine)",
            (New-Object Text.UTF8Encoding($false)))
    } finally {
        if (Test-Path -LiteralPath $offlineStaging) {
            Assert-InArtifacts $offlineStaging
            Remove-Item -LiteralPath $offlineStaging -Recurse -Force
        }
    }

    $packagesTarget = if ($isCanonicalDeploy) {
        Join-Path $deploymentArtifactsRoot "packages"
    } else {
        Join-Path $artifactsRoot "packages"
    }
    $packagesAllowedRoot = if ($isCanonicalDeploy) { $deploymentArtifactsRoot } else { $artifactsRoot }
    Replace-DirectoryAtomically $packageOutputStaging $packagesTarget $packagesAllowedRoot
    Write-Host "Yak and offline installer packages generated: $packagesTarget"

    if ($isCanonicalDeploy) {
        Assert-InArtifacts $canonical $deploymentArtifactsRoot
        Assert-NotLocked $canonical
        $backup = $null
        if (Test-Path -LiteralPath $canonical) {
            $version = "unknown"
            $oldManifest = Join-Path $canonical "manifest.yml"
            if (Test-Path -LiteralPath $oldManifest) {
                $match = Select-String -LiteralPath $oldManifest -Pattern '^version:\s*(.+)$' | Select-Object -First 1
                if ($match) { $version = $match.Matches[0].Groups[1].Value.Trim() }
            }
            $backupRoot = Join-Path $deploymentArtifactsRoot "backups"
            New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
            $backup = Join-Path $backupRoot ((Get-Date -Format "yyyyMMdd-HHmmss") + "-v" + $version)
            if (Test-Path -LiteralPath $backup) { $backup += "-" + [Guid]::NewGuid().ToString("N").Substring(0, 6) }
            Assert-InArtifacts $backup $deploymentArtifactsRoot
            Move-Item -LiteralPath $canonical -Destination $backup
        }
        try {
            Move-Item -LiteralPath $staging -Destination $canonical
        } catch {
            if ($backup -and (Test-Path -LiteralPath $backup) -and -not (Test-Path -LiteralPath $canonical)) {
                Move-Item -LiteralPath $backup -Destination $canonical
            }
            throw
        }
        $pluginRegistryPath = "HKCU:\Software\McNeel\Rhinoceros\8.0\Plug-Ins\ddc747eb-360e-4629-b65b-6bb1ddb4dc8f"
        $pluginRegistrationPath = Join-Path $pluginRegistryPath "PlugIn"
        if (Test-Path -LiteralPath $pluginRegistryPath) {
            $registeredPath = if (Test-Path -LiteralPath $pluginRegistrationPath) {
                (Get-ItemProperty -LiteralPath $pluginRegistrationPath -ErrorAction SilentlyContinue).FileName
            } else {
                (Get-ItemProperty -LiteralPath $pluginRegistryPath -ErrorAction SilentlyContinue).FileName
            }
            if ($registeredPath -match "[\\/]McNeel[\\/]Rhinoceros[\\/]packages[\\/]") {
                Write-Host "Rhino registration uses a stable Yak package path; development build did not replace it."
            } else {
                $targetRegistrationPath = if (Test-Path -LiteralPath $pluginRegistrationPath) {
                    $pluginRegistrationPath
                } else {
                    $pluginRegistryPath
                }
                Set-ItemProperty -LiteralPath $targetRegistrationPath -Name "FileName" -Value (Join-Path $canonical ($pluginBaseName + ".rhp"))
                Write-Host "Rhino development registration updated to the canonical plugin path."
            }
        }
        Write-Host "Plugin updated: $canonical"
        if ($backup) { Write-Host "Previous plugin backed up: $backup" }
    } else {
        Replace-DirectoryAtomically $staging $canonical $artifactsRoot
        Write-Host "Plugin build complete: $canonical"
    }
} finally {
    if (Test-Path -LiteralPath $staging) {
        Assert-InArtifacts $staging
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
    if (Test-Path -LiteralPath $packageBuildStaging) {
        Assert-InArtifacts $packageBuildStaging
        Remove-Item -LiteralPath $packageBuildStaging -Recurse -Force
    }
    if (Test-Path -LiteralPath $packageOutputStaging) {
        Assert-InArtifacts $packageOutputStaging
        Remove-Item -LiteralPath $packageOutputStaging -Recurse -Force
    }
}
