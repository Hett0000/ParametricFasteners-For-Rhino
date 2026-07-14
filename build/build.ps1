param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $root ".dotnet\dotnet.exe"
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { "dotnet" }
$artifacts = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    Join-Path $root "artifacts\plugin"
} elseif ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
} else {
    Join-Path $root $OutputDirectory
}
$env:DOTNET_CLI_HOME = Join-Path $root ".tools\dotnet-home"
$env:NUGET_PACKAGES = Join-Path $root ".tools\nuget"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

& $dotnet restore (Join-Path $root "RhinoMM.sln")
& $dotnet test (Join-Path $root "tests\RhinoMM.Core.Tests\RhinoMM.Core.Tests.csproj") --configuration $Configuration --no-restore
& $dotnet build (Join-Path $root "src\RhinoMM.Plugin\RhinoMM.Plugin.csproj") --configuration $Configuration --no-restore

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$output = Join-Path $root "src\RhinoMM.Plugin\bin\$Configuration\net7.0-windows"
Copy-Item -LiteralPath (Join-Path $output "RhinoMM.rhp") -Destination $artifacts -Force
Copy-Item -LiteralPath (Join-Path $output "RhinoMM.dll") -Destination $artifacts -Force
Copy-Item -LiteralPath (Join-Path $output "RhinoMM.Core.dll") -Destination $artifacts -Force
Copy-Item -LiteralPath (Join-Path $root "packaging\manifest.yml") -Destination $artifacts -Force

Write-Host "RhinoMM build complete: $artifacts"
