param(
    [string]$ReportPath = ""
)

$ErrorActionPreference = "Continue"
$packageId = "parametric-fasteners"
$expectedVersion = "0.38.7"
$pluginGuid = "ddc747eb-360e-4629-b65b-6bb1ddb4dc8f"
$pluginFileName = "ParametricFasteners.rhp"
$toolbarFileName = "ParametricFasteners.rui"
$coreFileName = "RhinoMM.Core.dll"
$lines = [Collections.Generic.List[string]]::new()

function Add-Line([string]$text = "") {
    $lines.Add($text)
    Write-Host $text
}

function Find-Yak {
    foreach ($candidate in @(
        (Join-Path $env:ProgramFiles "Rhino 8\System\Yak.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Rhino 8\System\Yak.exe")
    )) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
    }
    return $null
}

function Safe-Path([string]$path) {
    if ([string]::IsNullOrWhiteSpace($path)) { return "(empty)" }
    foreach ($item in @(
        [pscustomobject]@{ Path = $env:LOCALAPPDATA; Token = "%LOCALAPPDATA%" },
        [pscustomobject]@{ Path = $env:APPDATA; Token = "%APPDATA%" },
        [pscustomobject]@{ Path = $env:USERPROFILE; Token = "%USERPROFILE%" }
    )) {
        if (-not [string]::IsNullOrWhiteSpace($item.Path) -and
            $path.StartsWith($item.Path, [StringComparison]::OrdinalIgnoreCase)) {
            return $item.Token + $path.Substring($item.Path.Length)
        }
    }
    return $path
}

function Test-PathSafe([string]$path) {
    if ([string]::IsNullOrWhiteSpace($path)) { return $false }
    return Test-Path -LiteralPath $path -ErrorAction SilentlyContinue
}

function Read-Value($properties, [string]$name) {
    if ($null -eq $properties) { return $null }
    $property = $properties.PSObject.Properties[$name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Display-Value($value) {
    if ($null -eq $value -or [string]::IsNullOrWhiteSpace([string]$value)) { return "(not set)" }
    return [string]$value
}

function Get-RhinoRuntime {
    $process = Get-Process -Name Rhino -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $process) {
        return "Rhino is not running; verify with _SetDotNetRuntime (NETCore is required)."
    }
    try {
        $moduleNames = @($process.Modules | ForEach-Object { $_.ModuleName })
        if ($moduleNames -contains "coreclr.dll") { return ".NET Core / .NET 7 or 8" }
        if ($moduleNames -contains "clr.dll") { return ".NET Framework (unsupported for this package)" }
    } catch {
        return "Rhino is running, but its runtime modules could not be inspected."
    }
    return "Rhino runtime could not be identified."
}

Add-Line "Parametric Fasteners installation diagnostics"
Add-Line ("Time: " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"))
Add-Line ("Expected version: " + $expectedVersion)
Add-Line ("Rhino runtime: " + (Get-RhinoRuntime))
Add-Line ""

$yak = Find-Yak
Add-Line ("Yak: " + $(if ($yak) { Safe-Path $yak } else { "not found" }))
$yakList = ""
if ($yak) {
    $yakList = (& $yak list 2>&1 | Out-String).Trim()
    Add-Line "Yak installed packages:"
    Add-Line $(if ([string]::IsNullOrWhiteSpace($yakList)) { "(none)" } else { $yakList })
}
$yakHasExpected = $yakList -match "(?im)^\s*$([regex]::Escape($packageId))\s+.*$([regex]::Escape($expectedVersion))"

$packageRoots = @(
    (Join-Path $env:APPDATA "McNeel\Rhinoceros\packages"),
    (Join-Path $env:LOCALAPPDATA "McNeel\Rhinoceros\packages")
) | Where-Object { Test-Path -LiteralPath $_ -ErrorAction SilentlyContinue }
$rhps = @($packageRoots | ForEach-Object {
    Get-ChildItem -LiteralPath $_ -Filter $pluginFileName -File -Recurse -ErrorAction SilentlyContinue
})
$expectedRhps = @($rhps | Where-Object {
    $candidateVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($_.FullName).ProductVersion
    -not [string]::IsNullOrWhiteSpace($candidateVersion) -and
        $candidateVersion.StartsWith($expectedVersion, [StringComparison]::Ordinal)
})

$legacyName = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String("5Y+C5pWw5YyW57Sn5Zu65Lu2LnJocA=="))
$legacyRhps = @($packageRoots | ForEach-Object {
    Get-ChildItem -LiteralPath $_ -Filter $legacyName -File -Recurse -ErrorAction SilentlyContinue
})

Add-Line ""
Add-Line "Discovered plug-in files:"
if ($rhps.Count -eq 0) { Add-Line "$pluginFileName not found" }
$requiredFilesHealthy = $false
foreach ($rhp in $rhps) {
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($rhp.FullName).ProductVersion
    $ruiPath = Join-Path $rhp.Directory.FullName $toolbarFileName
    $corePath = Join-Path $rhp.Directory.FullName $coreFileName
    $rhpBlocked = $null -ne (Get-Item -LiteralPath $rhp.FullName -Stream Zone.Identifier -ErrorAction SilentlyContinue)
    $ruiBlocked = $null -ne (Get-Item -LiteralPath $ruiPath -Stream Zone.Identifier -ErrorAction SilentlyContinue)
    $coreBlocked = $null -ne (Get-Item -LiteralPath $corePath -Stream Zone.Identifier -ErrorAction SilentlyContinue)
    Add-Line ("RHP: " + (Safe-Path $rhp.FullName))
    Add-Line ("Version: " + $version)
    Add-Line ("RUI: " + $(if (Test-PathSafe $ruiPath) { "present" } else { "missing" }))
    Add-Line ("Core: " + $(if (Test-PathSafe $corePath) { "present" } else { "missing" }))
    Add-Line ("Download block marker: RHP=" + $rhpBlocked + "; RUI=" + $ruiBlocked + "; Core=" + $coreBlocked)
    if (-not [string]::IsNullOrWhiteSpace($version) -and
        $version.StartsWith($expectedVersion, [StringComparison]::Ordinal) -and
        (Test-PathSafe $ruiPath) -and (Test-PathSafe $corePath) -and
        -not $rhpBlocked -and -not $ruiBlocked -and -not $coreBlocked) {
        $requiredFilesHealthy = $true
    }
}
foreach ($legacy in $legacyRhps) {
    Add-Line ("Legacy non-ASCII RHP: " + (Safe-Path $legacy.FullName))
}

$registryPath = "HKCU:\Software\McNeel\Rhinoceros\8.0\Plug-Ins\$pluginGuid"
$pluginPath = Join-Path $registryPath "PlugIn"
$commandListPath = Join-Path $registryPath "CommandList"
$rootRegistration = if (Test-Path -LiteralPath $registryPath) {
    Get-ItemProperty -LiteralPath $registryPath -ErrorAction SilentlyContinue
} else { $null }
$pluginRegistration = if (Test-Path -LiteralPath $pluginPath) {
    Get-ItemProperty -LiteralPath $pluginPath -ErrorAction SilentlyContinue
} else { $null }
$rootFileName = [string](Read-Value $rootRegistration "FileName")
$pluginFileNameValue = [string](Read-Value $pluginRegistration "FileName")
$effectiveFileName = if (-not [string]::IsNullOrWhiteSpace($pluginFileNameValue)) {
    $pluginFileNameValue
} else {
    $rootFileName
}
$commandCount = if (Test-Path -LiteralPath $commandListPath) {
    @((Get-Item -LiteralPath $commandListPath).Property).Count
} else { 0 }
$commandValues = if (Test-Path -LiteralPath $commandListPath) {
    Get-ItemProperty -LiteralPath $commandListPath -ErrorAction SilentlyContinue
} else { $null }
$hasPanelCommand = $null -ne (Read-Value $commandValues "ParametricFasteners")
$hasMoreCommand = $null -ne (Read-Value $commandValues "ParametricFastenersMore")
$loadMode = Read-Value $rootRegistration "LoadMode"
$enabled = Read-Value $rootRegistration "Enabled"
$protectionMode = Read-Value $rootRegistration "ProtectionMode"
$loadProtection = Read-Value $rootRegistration "LoadProtection"

Add-Line ""
Add-Line ("Registration root: " + $registryPath)
Add-Line ("Name: " + (Display-Value (Read-Value $rootRegistration "Name")))
Add-Line ("Root shortcut FileName: " + (Safe-Path $rootFileName))
Add-Line ("PlugIn FileName: " + (Safe-Path $pluginFileNameValue))
Add-Line ("Effective registered file exists: " + (Test-PathSafe $effectiveFileName))
Add-Line ("LoadMode: " + (Display-Value $loadMode))
Add-Line ("Enabled: " + (Display-Value $enabled))
Add-Line ("ProtectionMode: " + (Display-Value $protectionMode))
Add-Line ("LoadProtection: " + (Display-Value $loadProtection))
Add-Line ("CommandList entries: " + $commandCount)
Add-Line ("Key commands registered: ParametricFasteners=" + $hasPanelCommand + "; ParametricFastenersMore=" + $hasMoreCommand)

$systemRoots = @(
    "HKLM:\SOFTWARE\McNeel\Rhinoceros\8.0\Plug-Ins\$pluginGuid",
    "HKLM:\SOFTWARE\WOW6432Node\McNeel\Rhinoceros\8.0\Plug-Ins\$pluginGuid"
)
foreach ($root in $systemRoots) {
    if (Test-Path -LiteralPath $root) {
        $value = Get-ItemProperty -LiteralPath $root -ErrorAction SilentlyContinue
        $child = Join-Path $root "PlugIn"
        $childValue = if (Test-Path -LiteralPath $child) { Get-ItemProperty -LiteralPath $child -ErrorAction SilentlyContinue } else { $null }
        Add-Line ("Machine-wide root registration: " + $root + " -> " + (Safe-Path ([string](Read-Value $value "FileName"))))
        Add-Line ("Machine-wide PlugIn registration: " + (Safe-Path ([string](Read-Value $childValue "FileName"))))
    }
}

$disabled = ($null -ne $loadMode -and [int]$loadMode -eq 0) -or
    ($null -ne $enabled -and [int]$enabled -eq 0)
$effectiveExists = Test-PathSafe $effectiveFileName
$registeredVersionMatches = $false
if ($effectiveExists) {
    $registeredVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($effectiveFileName).ProductVersion
    $registeredVersionMatches = -not [string]::IsNullOrWhiteSpace($registeredVersion) -and
        $registeredVersion.StartsWith($expectedVersion, [StringComparison]::Ordinal)
}

Add-Line ""
if (-not $yakHasExpected -or -not $requiredFilesHealthy) {
    Add-Line "Registration status: files or dependencies are missing"
    Add-Line "Action: reinstall the complete offline package while Rhino is closed."
} elseif ($disabled) {
    Add-Line "Registration status: plug-in is disabled"
    Add-Line "Action: enable the plug-in in Rhino PlugInManager, then restart Rhino once."
} elseif (-not [string]::IsNullOrWhiteSpace($pluginFileNameValue) -and
    $effectiveExists -and $registeredVersionMatches -and
    $commandCount -gt 0 -and $hasPanelCommand -and $hasMoreCommand) {
    Add-Line "Registration status: fully registered and path is healthy"
    Add-Line "Action: no registry repair is required. If commands are unavailable, restart Rhino once and inspect PlugInManager load errors."
} elseif (-not [string]::IsNullOrWhiteSpace($effectiveFileName) -and
    (-not $effectiveExists -or -not $registeredVersionMatches)) {
    Add-Line "Registration status: registered path is stale"
    Add-Line "Action: completely exit Rhino and start it once so Package Manager can activate the installed version."
} else {
    Add-Line "Registration status: waiting for first Rhino restart"
    Add-Line "Action: start Rhino once after installation and wait for package registration to finish."
}

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $PSScriptRoot "ParametricFasteners-install-diagnostic.txt"
}
[IO.File]::WriteAllLines($ReportPath, $lines, (New-Object Text.UTF8Encoding($true)))
Write-Host ""
Write-Host "Diagnostic report saved: $ReportPath" -ForegroundColor Green
