param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

function Decode-Text([string]$value) {
    return [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($value))
}

$toolbarName = Decode-Text "5Y+C5pWw5YyW57Sn5Zu65Lu2"
$labels = @(
    (Decode-Text "6K+75Y+W57uE5Lu2"),
    (Decode-Text "5pS+572uL+e7keWumg=="),
    (Decode-Text "5bqU55So5pu05paw"),
    (Decode-Text "5Yi35pawL+a4heeQhg=="),
    (Decode-Text "5pS+5YWlIFJoaW5v"),
    (Decode-Text "5a+85Ye6IFNURVA="),
    (Decode-Text "57Sn5Zu65Lu257uf6K6h"),
    (Decode-Text "5pu05aSa4oCm")
)
$rhinoTooltip = Decode-Text "5bem5Ye777ya5LuF5pS+5YWl5biD5bCU5a6/5Li777yb5Y+z5Ye777ya5pS+5YWl5biD5bCU5a6/5Li75ZKM57Sn5Zu65Lu25a6e5L2T"
$rightRhinoMacroId = "e6c663ce-7f3f-4e57-9a54-4c9760523a01"

$iconIds = @(
    "9c0684e5-48b5-4f5f-b2aa-1894b118cc20",
    "40e48fe9-0e2b-4428-a92e-f94ec6b0bba6",
    "944862fe-bf89-4b90-87c9-b79d22f217ce",
    "9a4f7502-7f13-407e-81c3-2d565e41c0b8",
    "4c2bcc5b-d424-469c-bb56-d511f9b840f3",
    "a06c5622-f60d-427e-b1a6-0f3886837afe",
    "bc97cf66-dfce-49de-a56a-56627fa56a78",
    "1b990f9f-337e-4cab-9f96-fd02b463907c",
    "20df3482-04e5-49e0-a8be-14a6eec67239"
)

$root = Split-Path -Parent $PSScriptRoot
$iconDirectory = Join-Path $root "src\RhinoMM.Plugin\UI\Icons\Source"
$iconStems = @("rhino", "read", "place", "apply", "refresh", "rhino", "step", "statistics", "more")
$lightIconColor = "#34495E"
$darkIconColor = "#D7DEE8"

function Get-ThemedSvg([string]$stem, [string]$color) {
    $sourcePath = Join-Path $iconDirectory "$stem.svg"
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Missing toolbar SVG icon: $sourcePath"
    }

    return ([System.IO.File]::ReadAllText($sourcePath).Trim()).Replace("currentColor", $color)
}

function IconItems {
    $items = for ($index = 0; $index -lt $iconIds.Count; $index++) {
        $light = Get-ThemedSvg $iconStems[$index] $lightIconColor
        $dark = Get-ThemedSvg $iconStems[$index] $darkIconColor
        @"
    <icon guid="$($iconIds[$index])">
      <light>$light</light>
      <dark>$dark</dark>
    </icon>
"@
    }
    return ($items -join [Environment]::NewLine)
}

$items = IconItems
$toolbarItems = @(
    @("1c6499e1-00cc-4754-83c3-feaf3fb44d61", "8f72ca22-3e3a-41c4-9914-aa5cc1c681d5", $labels[0]),
    @("61d7dd38-0058-4f6c-b0ac-208691758f07", "d37963f4-fe65-40ea-8a2a-2fdc1b2a2f05", $labels[1]),
    @("87f6ebf4-073e-4608-a5df-98cae9a90ef8", "c4e43f54-28c6-46d4-8708-2b1d3386388d", $labels[2]),
    @("66da8dc8-8217-462f-8a4b-cf59da169f18", "5e662cc5-228f-477c-aecf-682ad906a066", $labels[3]),
    @("c4f75327-77cb-4190-b960-39a930324e62", "770017fd-e778-4f5f-8264-e6cc7b787849", $labels[4]),
    @("1ca05aa0-327b-4f7b-a049-696f40db2b1e", "837fe927-d611-45db-ad09-dda3a1331319", $labels[5]),
    @("8de90af4-380f-4681-9188-26771a37c961", "95e686a2-9fe0-49ed-8ac6-cf5cd28123fd", $labels[6]),
    @("a20c4548-a9d7-48ba-a816-a602691325fe", "3844e878-b48c-4b4a-a095-a18cf918a4bb", $labels[7])
)
$commands = @(
    "ParametricFastenersEdit",
    "ParametricFastenersPlace",
    "ParametricFastenersApplyUpdate",
    "ParametricFastenersRefresh",
    "ParametricFastenersExportToRhino",
    "ParametricFastenersExportStep",
    "ParametricFastenersStatistics",
    "ParametricFastenersMore"
)
$toolXml = for ($i = 0; $i -lt $toolbarItems.Count; $i++) {
    $rightMacro = if ($i -eq 4) { "<right_macro_id>$rightRhinoMacroId</right_macro_id>" } else { "" }
    "      <tool_bar_item guid=`"$($toolbarItems[$i][0])`"><text><locale_2052>$($toolbarItems[$i][2])</locale_2052></text><left_macro_id>$($toolbarItems[$i][1])</left_macro_id>$rightMacro</tool_bar_item>"
}
$macroXml = for ($i = 0; $i -lt $toolbarItems.Count; $i++) {
    $iconId = $iconIds[$i + 1]
    $tooltip = if ($i -eq 4) { $rhinoTooltip } else { $toolbarItems[$i][2] }
    "    <macro_item guid=`"$($toolbarItems[$i][1])`" bitmap_id=`"$iconId`"><text><locale_2052>$($toolbarItems[$i][2])</locale_2052></text><tooltip><locale_2052>$tooltip</locale_2052></tooltip><script>! _$($commands[$i])</script></macro_item>"
}
$macroXml += "    <macro_item guid=`"$rightRhinoMacroId`" bitmap_id=`"$($iconIds[5])`"><text><locale_2052>$($labels[4])</locale_2052></text><tooltip><locale_2052>$rhinoTooltip</locale_2052></tooltip><script>! _ParametricFastenersExportToRhinoWithFasteners</script></macro_item>"

$xml = @"
<?xml version="1.0" encoding="utf-8"?>
<RhinoUI major_ver="8" minor_ver="0" guid="5c120a44-494a-4973-a495-e8c4218b0c22" plug_in_guid="ddc747eb-360e-4629-b65b-6bb1ddb4dc8f" localize="False" default_language_id="2052">
  <extend_rhino_menus />
  <menus />
  <tool_bar_groups />
  <tool_bars>
    <tool_bar guid="cfbd6692-bca7-4bb9-bd8a-9070f2a4ab08" bitmap_id="$($iconIds[0])">
      <text><locale_2052>$toolbarName</locale_2052></text>
$($toolXml -join [Environment]::NewLine)
    </tool_bar>
  </tool_bars>
  <macros>
$($macroXml -join [Environment]::NewLine)
  </macros>
  <icons>
$items
  </icons>
  <bitmaps />
</RhinoUI>
"@

$fullPath = [System.IO.Path]::GetFullPath($OutputPath)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($fullPath)) | Out-Null
[System.IO.File]::WriteAllText($fullPath, $xml, (New-Object System.Text.UTF8Encoding($true)))
Write-Host "Toolbar generated: $fullPath"
