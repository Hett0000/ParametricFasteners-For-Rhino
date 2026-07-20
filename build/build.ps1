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
$canonical = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    Join-Path $artifactsRoot "plugin"
} elseif ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
} else {
    Join-Path $root $OutputDirectory
}
$isCanonicalDeploy = [string]::IsNullOrWhiteSpace($OutputDirectory)
$stagingRoot = Join-Path $artifactsRoot ".staging"
$staging = Join-Path $stagingRoot ("plugin-" + [Guid]::NewGuid().ToString("N"))
$pluginBaseName = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String("5Y+C5pWw5YyW57Sn5Zu65Lu2"))
$env:DOTNET_CLI_HOME = Join-Path $root ".tools\dotnet-home"
$env:NUGET_PACKAGES = Join-Path $root ".tools\nuget"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

function Assert-InArtifacts([string]$path) {
    $rootPath = [System.IO.Path]::GetFullPath($artifactsRoot).TrimEnd('\') + '\'
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

New-Item -ItemType Directory -Force -Path $staging | Out-Null
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

    if ($isCanonicalDeploy) {
        Assert-InArtifacts $canonical
        Assert-NotLocked $canonical
        $backup = $null
        if (Test-Path -LiteralPath $canonical) {
            $version = "unknown"
            $oldManifest = Join-Path $canonical "manifest.yml"
            if (Test-Path -LiteralPath $oldManifest) {
                $match = Select-String -LiteralPath $oldManifest -Pattern '^version:\s*(.+)$' | Select-Object -First 1
                if ($match) { $version = $match.Matches[0].Groups[1].Value.Trim() }
            }
            $backupRoot = Join-Path $artifactsRoot "backups"
            New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
            $backup = Join-Path $backupRoot ((Get-Date -Format "yyyyMMdd-HHmmss") + "-v" + $version)
            if (Test-Path -LiteralPath $backup) { $backup += "-" + [Guid]::NewGuid().ToString("N").Substring(0, 6) }
            Assert-InArtifacts $backup
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
        $pluginRegistryPath = "HKCU:\Software\McNeel\Rhinoceros\8.0\Plug-Ins\ddc747eb-360e-4629-b65b-6bb1ddb4dc8f\PlugIn"
        if (Test-Path -LiteralPath $pluginRegistryPath) {
            Set-ItemProperty -LiteralPath $pluginRegistryPath -Name "FileName" -Value (Join-Path $canonical ($pluginBaseName + ".rhp"))
            Write-Host "Rhino registration updated to the canonical plugin path."
        }
        Write-Host "Plugin updated: $canonical"
        if ($backup) { Write-Host "Previous plugin backed up: $backup" }
    } else {
        New-Item -ItemType Directory -Force -Path $canonical | Out-Null
        Copy-Item -Path (Join-Path $staging "*") -Destination $canonical -Force
        Write-Host "Plugin build complete: $canonical"
    }
} finally {
    if (Test-Path -LiteralPath $staging) {
        Assert-InArtifacts $staging
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
}
