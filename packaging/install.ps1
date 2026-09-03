param(
    [string]$PackagePath = ""
)

$ErrorActionPreference = "Stop"
$packageId = "parametric-fasteners"
$version = "0.40.4"
$pluginGuid = "ddc747eb-360e-4629-b65b-6bb1ddb4dc8f"
$pluginFileName = "ParametricFasteners.rhp"
$toolbarFileName = "ParametricFasteners.rui"

function Stop-WithMessage([string]$message) {
    Write-Host ""
    Write-Host "Installation did not complete: $message" -ForegroundColor Red
    Write-Host "Templates, favorites, and model data were not modified."
    exit 1
}

function Find-Yak {
    $candidates = @(
        (Join-Path $env:ProgramFiles "Rhino 8\System\Yak.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Rhino 8\System\Yak.exe")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }

    foreach ($root in @(
        "HKLM:\SOFTWARE\McNeel\Rhinoceros\8.0\Install",
        "HKLM:\SOFTWARE\WOW6432Node\McNeel\Rhinoceros\8.0\Install"
    )) {
        if (-not (Test-Path -LiteralPath $root)) { continue }
        $properties = Get-ItemProperty -LiteralPath $root
        foreach ($name in @("Path", "InstallPath")) {
            $base = $properties.$name
            if ([string]::IsNullOrWhiteSpace($base)) { continue }
            $candidate = Join-Path $base "System\Yak.exe"
            if (Test-Path -LiteralPath $candidate) { return $candidate }
        }
    }
    return $null
}

function Test-SystemRegistrationConflict {
    foreach ($root in @(
        "HKLM:\SOFTWARE\McNeel\Rhinoceros\8.0\Plug-Ins\$pluginGuid",
        "HKLM:\SOFTWARE\WOW6432Node\McNeel\Rhinoceros\8.0\Plug-Ins\$pluginGuid"
    )) {
        foreach ($key in @($root, (Join-Path $root "PlugIn"))) {
            if (-not (Test-Path -LiteralPath $key)) { continue }
            $fileName = (Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue).FileName
            if (-not [string]::IsNullOrWhiteSpace($fileName) -and
                $fileName -notmatch "[\\/]McNeel[\\/]Rhinoceros[\\/]packages[\\/]") {
                return $fileName
            }
        }
    }
    return $null
}

if (Get-Process -Name Rhino -ErrorAction SilentlyContinue) {
    Stop-WithMessage "Rhino is running. Save your work, close every Rhino process, and retry."
}

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $packages = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter "$packageId-$version-*.yak" -File)
    if ($packages.Count -ne 1) {
        Stop-WithMessage "The installer folder must contain exactly one $packageId-$version Yak package."
    }
    $PackagePath = $packages[0].FullName
}
$PackagePath = [IO.Path]::GetFullPath($PackagePath)
if (-not (Test-Path -LiteralPath $PackagePath)) {
    Stop-WithMessage "Yak package not found: $PackagePath"
}

$yak = Find-Yak
if (-not $yak) {
    Stop-WithMessage "Rhino 8 or Yak.exe was not found. Install Rhino 8 first."
}

$systemConflict = Test-SystemRegistrationConflict
if ($systemConflict) {
    Stop-WithMessage "A conflicting machine-wide registration was found: $systemConflict. Remove it in Rhino PluginManager or ask an administrator to remove it."
}

try { Unblock-File -LiteralPath $PackagePath -ErrorAction SilentlyContinue } catch { }

$checksumFile = Join-Path $PSScriptRoot "SHA256SUMS.txt"
if (Test-Path -LiteralPath $checksumFile) {
    $packageName = [IO.Path]::GetFileName($PackagePath)
    $line = Get-Content -LiteralPath $checksumFile | Where-Object { $_ -match "\s+$([regex]::Escape($packageName))$" } | Select-Object -First 1
    if ($line) {
        $expected = ($line -split '\s+')[0].ToUpperInvariant()
        $actual = (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash.ToUpperInvariant()
        if ($actual -ne $expected) { Stop-WithMessage "Yak SHA256 verification failed; the package may be damaged." }
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($PackagePath)
try {
    $entryNames = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\\', '/') })
    foreach ($required in @("manifest.yml", $pluginFileName, $toolbarFileName, "RhinoMM.Core.dll")) {
        if ($entryNames -notcontains $required) { Stop-WithMessage "Yak package is missing: $required" }
    }
    $topLevelRhp = @($entryNames | Where-Object { $_ -match '^[^/]+\.rhp$' })
    if ($topLevelRhp.Count -ne 1 -or $topLevelRhp[0] -ne $pluginFileName) {
        Stop-WithMessage "Yak must contain exactly one top-level $pluginFileName."
    }
} finally {
    $archive.Dispose()
}

$userRegistry = "HKCU:\Software\McNeel\Rhinoceros\8.0\Plug-Ins\$pluginGuid"
$backupDirectory = Join-Path $env:LOCALAPPDATA "ParametricFasteners\RegistrationBackups"
if (Test-Path -LiteralPath $userRegistry) {
    New-Item -ItemType Directory -Force -Path $backupDirectory | Out-Null
    $backupFile = Join-Path $backupDirectory ("Rhino8-registration-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".reg")
    & reg.exe export "HKCU\Software\McNeel\Rhinoceros\8.0\Plug-Ins\$pluginGuid" $backupFile /y | Out-Null
}

Write-Host "Installing Parametric Fasteners $version with Rhino Yak..."
& $yak install $PackagePath
if ($LASTEXITCODE -ne 0) { Stop-WithMessage "Yak returned error code $LASTEXITCODE." }

$listOutput = (& $yak list 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or $listOutput -notmatch "(?im)^\s*$([regex]::Escape($packageId))\s+.*$([regex]::Escape($version))") {
    Stop-WithMessage "Yak did not report $packageId $version as installed."
}

$packageRoots = @(
    (Join-Path $env:APPDATA "McNeel\Rhinoceros\packages"),
    (Join-Path $env:LOCALAPPDATA "McNeel\Rhinoceros\packages")
) | Where-Object { Test-Path -LiteralPath $_ }
$installedRhp = $packageRoots |
    ForEach-Object { Get-ChildItem -LiteralPath $_ -Filter $pluginFileName -File -Recurse -ErrorAction SilentlyContinue } |
    Where-Object { $_.FullName -match "[\\/]$([regex]::Escape($packageId))[\\/]$([regex]::Escape($version))([\\/]|$)" } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if (-not $installedRhp) {
    Stop-WithMessage "Yak registered the package, but $pluginFileName was not found in the stable package directory."
}

$installedDirectory = $installedRhp.Directory.FullName
foreach ($requiredPath in @(
    $installedRhp.FullName,
    (Join-Path $installedDirectory $toolbarFileName),
    (Join-Path $installedDirectory "RhinoMM.Core.dll")
)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        Stop-WithMessage "The stable installation directory is missing: $requiredPath"
    }
    try { Unblock-File -LiteralPath $requiredPath -ErrorAction SilentlyContinue } catch { }
}

$installedVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($installedRhp.FullName).ProductVersion
if ([string]::IsNullOrWhiteSpace($installedVersion) -or
    -not $installedVersion.StartsWith($version, [StringComparison]::Ordinal)) {
    Stop-WithMessage "Installed RHP version '$installedVersion' does not match package version '$version'."
}

# 0.38.3 created a short-form root FileName registration in addition to Yak.
# Rhino expands that shortcut into the PlugIn child on first launch, which can
# leave commands unavailable until a manual load or a second restart.
# Yak is now the only registration authority. Remove only our known package shortcut;
# preserve the full PlugIn child, CommandList, settings, and non-Yak installs.
if (Test-Path -LiteralPath $userRegistry) {
    $registration = Get-ItemProperty -LiteralPath $userRegistry -ErrorAction SilentlyContinue
    $rootFileName = [string]$registration.FileName
    if (-not [string]::IsNullOrWhiteSpace($rootFileName) -and
        $rootFileName -match "[\\/]McNeel[\\/]Rhinoceros[\\/]packages[\\/]8\.0[\\/]$([regex]::Escape($packageId))[\\/]" -and
        [IO.Path]::GetFileName($rootFileName) -eq $pluginFileName) {
        Remove-ItemProperty -LiteralPath $userRegistry -Name FileName -ErrorAction SilentlyContinue
        Write-Host "Removed the legacy 0.38.3 short-form registration. Rhino Package Manager will register the Yak package."
    }
}

Write-Host ""
Write-Host "Installation succeeded." -ForegroundColor Green
Write-Host "Version: $version"
Write-Host "RHP: $($installedRhp.FullName)"
Write-Host "Minimum: Rhino 8.18 for Windows"
Write-Host "Registration: managed by Rhino Package Manager (Yak)"
Write-Host "Start Rhino 8 once after installation. A Yak package cannot register commands in an already-running installation session."
