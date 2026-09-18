$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
# Rasterize the simple geometry of Bootstrap Icons speaker-fill.svg.
# Original geometry: 16x16 rounded cabinet, circles at (8,4) and (8,10.5).
# Add a blue Windows-style tile and use white foreground for dark/light trays.
function Write-SpeakerIcon([string]$Destination, [System.Drawing.Color]$Tile) {
    $speakerImages = @()
    foreach ($speakerSize in @(16,20,24,32,40,48,64,128,256)) {
        $speakerBitmap = New-Object System.Drawing.Bitmap($speakerSize,$speakerSize)
        $speakerGraphics = [System.Drawing.Graphics]::FromImage($speakerBitmap)
        $speakerGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $speakerGraphics.Clear([System.Drawing.Color]::Transparent)
        $speakerGraphics.ScaleTransform(($speakerSize / 20.0),($speakerSize / 20.0))
        $speakerBrush = New-Object System.Drawing.SolidBrush($Tile)
        $speakerGraphics.FillEllipse($speakerBrush,0,0,20,20)
        $speakerGraphics.TranslateTransform(4,2)
        $speakerGraphics.ScaleTransform(0.75,1.0)
        $speakerPath = New-Object System.Drawing.Drawing2D.GraphicsPath
        $speakerPath.AddArc(2,0,4,4,180,90)
        $speakerPath.AddArc(10,0,4,4,270,90)
        $speakerPath.AddArc(10,12,4,4,0,90)
        $speakerPath.AddArc(2,12,4,4,90,90)
        $speakerPath.CloseFigure()
        $speakerGraphics.FillPath([System.Drawing.Brushes]::White,$speakerPath)
        $speakerGraphics.FillEllipse($speakerBrush,6,2,4,4)
        $speakerGraphics.FillEllipse([System.Drawing.Brushes]::White,7,3,2,2)
        $speakerGraphics.FillEllipse($speakerBrush,4.5,7,7,7)
        $speakerGraphics.FillEllipse([System.Drawing.Brushes]::White,6.5,9,3,3)
        $speakerStream = New-Object System.IO.MemoryStream
        $speakerBitmap.Save($speakerStream,[System.Drawing.Imaging.ImageFormat]::Png)
        $speakerImages += ,@{ Size=$speakerSize; Bytes=$speakerStream.ToArray() }
        $speakerStream.Dispose(); $speakerPath.Dispose(); $speakerBrush.Dispose(); $speakerGraphics.Dispose(); $speakerBitmap.Dispose()
    }
    $speakerFile = [System.IO.File]::Create($Destination)
    $speakerWriter = New-Object System.IO.BinaryWriter($speakerFile)
    try {
        $speakerWriter.Write([uint16]0); $speakerWriter.Write([uint16]1); $speakerWriter.Write([uint16]$speakerImages.Count)
        $speakerOffset = 6 + 16 * $speakerImages.Count
        foreach ($speakerImage in $speakerImages) {
            $speakerDimension = if ($speakerImage.Size -eq 256) { 0 } else { $speakerImage.Size }
            $speakerWriter.Write([byte]$speakerDimension); $speakerWriter.Write([byte]$speakerDimension)
            $speakerWriter.Write([byte]0); $speakerWriter.Write([byte]0)
            $speakerWriter.Write([uint16]1); $speakerWriter.Write([uint16]32)
            $speakerWriter.Write([uint32]$speakerImage.Bytes.Length); $speakerWriter.Write([uint32]$speakerOffset)
            $speakerOffset += $speakerImage.Bytes.Length
        }
        foreach ($speakerImage in $speakerImages) { $speakerWriter.Write([byte[]]$speakerImage.Bytes) }
    } finally { $speakerWriter.Dispose(); $speakerFile.Dispose() }
}
Write-SpeakerIcon (Join-Path $PSScriptRoot 'assets\speaker.ico') ([System.Drawing.Color]::FromArgb(0,103,192))
Write-SpeakerIcon (Join-Path $PSScriptRoot 'assets\speaker-paused.ico') ([System.Drawing.Color]::FromArgb(105,105,105))
