param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function Decode-Text([string]$value) {
    return [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($value))
}

$toolbarName = Decode-Text "5Y+C5pWw5YyW57Sn5Zu65Lu2"
$labels = @(
    (Decode-Text "5omT5byA5Y+C5pWw5YyW57Sn5Zu65Lu2"),
    (Decode-Text "5pS+572u5LiO57uR5a6a5a2U"),
    (Decode-Text "6K+75Y+W5LiO57yW6L6R57uE5Lu2"),
    (Decode-Text "6L2s5o2i546w5pyJ5qih5Z6L"),
    (Decode-Text "5qCh6aqM5paH5qGj"),
    (Decode-Text "5biD5bCU5bm25a+85Ye6")
)

$bitmapIds = @(
    "9c0684e5-48b5-4f5f-b2aa-1894b118cc20",
    "40e48fe9-0e2b-4428-a92e-f94ec6b0bba6",
    "944862fe-bf89-4b90-87c9-b79d22f217ce",
    "9a4f7502-7f13-407e-81c3-2d565e41c0b8",
    "4c2bcc5b-d424-469c-bb56-d511f9b840f3",
    "a06c5622-f60d-427e-b1a6-0f3886837afe",
    "bc97cf66-dfce-49de-a56a-56627fa56a78"
)

function New-IconStripBase64([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap($size, ($size * $bitmapIds.Count), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(($size / 32.0), ($size / 32.0))
    $blue = [System.Drawing.Color]::FromArgb(255, 43, 108, 176)
    $orange = [System.Drawing.Color]::FromArgb(255, 235, 126, 34)
    $pen = New-Object System.Drawing.Pen($blue, 2.2)
    $accent = New-Object System.Drawing.Pen($orange, 2.2)
    $pen.StartCap = $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $accent.StartCap = $accent.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    for ($index = 0; $index -lt $bitmapIds.Count; $index++) {
        $graphics.ResetTransform()
        $graphics.ScaleTransform(($size / 32.0), ($size / 32.0))
        $graphics.TranslateTransform(0, ($index * 32))
        switch ($index) {
            0 {
                $graphics.DrawPolygon($pen, @([Drawing.PointF]::new(7, 8), [Drawing.PointF]::new(14, 4), [Drawing.PointF]::new(21, 8), [Drawing.PointF]::new(21, 16), [Drawing.PointF]::new(14, 20), [Drawing.PointF]::new(7, 16)))
                $graphics.DrawLine($accent, 14, 20, 14, 28)
                $graphics.DrawLine($accent, 10, 24, 18, 24)
            }
            1 {
                $graphics.DrawRectangle($pen, 4, 5, 24, 22)
                $graphics.DrawLine($accent, 8, 11, 24, 11)
                $graphics.DrawEllipse($accent, 13, 9, 4, 4)
                $graphics.DrawLine($accent, 8, 20, 24, 20)
                $graphics.DrawEllipse($accent, 19, 18, 4, 4)
            }
            2 {
                $graphics.DrawEllipse($pen, 4, 4, 12, 12)
                $graphics.DrawLine($pen, 10, 1, 10, 19)
                $graphics.DrawLine($pen, 1, 10, 19, 10)
                $graphics.DrawLine($accent, 16, 16, 28, 28)
                $graphics.DrawLine($accent, 20, 16, 16, 20)
            }
            3 {
                $graphics.DrawPolygon($pen, @([Drawing.PointF]::new(5, 7), [Drawing.PointF]::new(11, 4), [Drawing.PointF]::new(17, 7), [Drawing.PointF]::new(17, 13), [Drawing.PointF]::new(11, 16), [Drawing.PointF]::new(5, 13)))
                $graphics.DrawLine($pen, 11, 16, 11, 27)
                $graphics.DrawLine($accent, 18, 24, 27, 15)
                $graphics.DrawLine($accent, 17, 28, 18, 24)
                $graphics.DrawLine($accent, 27, 15, 30, 18)
            }
            4 {
                $graphics.DrawRectangle($pen, 3, 7, 11, 11)
                $graphics.DrawLine($accent, 15, 13, 24, 13)
                $graphics.DrawLine($accent, 21, 10, 24, 13)
                $graphics.DrawLine($accent, 21, 16, 24, 13)
                $graphics.DrawEllipse($pen, 23, 8, 6, 6)
                $graphics.DrawLine($pen, 26, 14, 26, 27)
            }
            5 {
                $graphics.DrawEllipse($pen, 4, 4, 24, 24)
                $graphics.DrawLine($accent, 9, 16, 14, 21)
                $graphics.DrawLine($accent, 14, 21, 24, 10)
            }
            6 {
                $graphics.DrawRectangle($pen, 3, 5, 15, 19)
                $graphics.DrawLine($accent, 18, 15, 29, 15)
                $graphics.DrawLine($accent, 25, 11, 29, 15)
                $graphics.DrawLine($accent, 25, 19, 29, 15)
                $graphics.DrawLine($pen, 21, 25, 29, 25)
            }
        }
    }

    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $result = [Convert]::ToBase64String($stream.ToArray())
    $stream.Dispose()
    $pen.Dispose()
    $accent.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
    return $result
}

function BitmapItems {
    $items = for ($index = 0; $index -lt $bitmapIds.Count; $index++) {
        "      <bitmap_item guid=`"$($bitmapIds[$index])`" index=`"$index`" />"
    }
    return ($items -join [Environment]::NewLine)
}

$items = BitmapItems
$small = New-IconStripBase64 16
$normal = New-IconStripBase64 24
$large = New-IconStripBase64 32
$toolbarItems = @(
    @("1c6499e1-00cc-4754-83c3-feaf3fb44d61", "8f72ca22-3e3a-41c4-9914-aa5cc1c681d5", $labels[0]),
    @("61d7dd38-0058-4f6c-b0ac-208691758f07", "d37963f4-fe65-40ea-8a2a-2fdc1b2a2f05", $labels[1]),
    @("87f6ebf4-073e-4608-a5df-98cae9a90ef8", "c4e43f54-28c6-46d4-8708-2b1d3386388d", $labels[2]),
    @("66da8dc8-8217-462f-8a4b-cf59da169f18", "5e662cc5-228f-477c-aecf-682ad906a066", $labels[3]),
    @("c4f75327-77cb-4190-b960-39a930324e62", "770017fd-e778-4f5f-8264-e6cc7b787849", $labels[4]),
    @("1ca05aa0-327b-4f7b-a049-696f40db2b1e", "837fe927-d611-45db-ad09-dda3a1331319", $labels[5])
)
$commands = @(
    "ParametricFasteners",
    "ParametricFastenersPlace",
    "ParametricFastenersEdit",
    "ParametricFastenersAdopt",
    "ParametricFastenersValidate",
    "ParametricFastenersExport"
)
$toolXml = for ($i = 0; $i -lt $toolbarItems.Count; $i++) {
    "      <tool_bar_item guid=`"$($toolbarItems[$i][0])`"><text><locale_2052>$($toolbarItems[$i][2])</locale_2052></text><left_macro_id>$($toolbarItems[$i][1])</left_macro_id></tool_bar_item>"
}
$macroXml = for ($i = 0; $i -lt $toolbarItems.Count; $i++) {
    $bitmapId = $bitmapIds[$i + 1]
    "    <macro_item guid=`"$($toolbarItems[$i][1])`" bitmap_id=`"$bitmapId`"><text><locale_2052>$($toolbarItems[$i][2])</locale_2052></text><tooltip><locale_2052>$($toolbarItems[$i][2])</locale_2052></tooltip><script>! _$($commands[$i])</script></macro_item>"
}

$xml = @"
<?xml version="1.0" encoding="utf-8"?>
<RhinoUI major_ver="3" minor_ver="0" guid="5c120a44-494a-4973-a495-e8c4218b0c22" localize="False" default_language_id="2052" dpi_scale="100">
  <extend_rhino_menus />
  <menus />
  <tool_bar_groups />
  <tool_bars>
    <tool_bar guid="cfbd6692-bca7-4bb9-bd8a-9070f2a4ab08" bitmap_id="$($bitmapIds[0])">
      <text><locale_2052>$toolbarName</locale_2052></text>
$($toolXml -join [Environment]::NewLine)
    </tool_bar>
  </tool_bars>
  <macros>
$($macroXml -join [Environment]::NewLine)
  </macros>
  <bitmaps>
    <small_bitmap item_width="16" item_height="16">
$items
      <bitmap>$small</bitmap>
    </small_bitmap>
    <normal_bitmap item_width="24" item_height="24">
$items
      <bitmap>$normal</bitmap>
    </normal_bitmap>
    <large_bitmap item_width="32" item_height="32">
$items
      <bitmap>$large</bitmap>
    </large_bitmap>
  </bitmaps>
</RhinoUI>
"@

$fullPath = [System.IO.Path]::GetFullPath($OutputPath)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($fullPath)) | Out-Null
[System.IO.File]::WriteAllText($fullPath, $xml, (New-Object System.Text.UTF8Encoding($true)))
Write-Host "Toolbar generated: $fullPath"
