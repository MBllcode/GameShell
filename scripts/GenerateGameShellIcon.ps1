$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$assetDir = Join-Path $root "src\GameShell.Desktop\Assets"
$appPngPath = Join-Path $assetDir "GameShell.png"
$appIcoPath = Join-Path $assetDir "GameShell.ico"
$shortcutIcoPath = Join-Path $assetDir "GameShellShortcut.ico"

function Recolor-PngToGreenV2([string]$pngPath) {
    if (-not (Test-Path -LiteralPath $pngPath)) {
        throw "Missing source PNG: $pngPath"
    }

    $source = [System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $pngPath))
    $recolored = New-Object System.Drawing.Bitmap $source.Width, $source.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $background = [System.Drawing.Color]::FromArgb(13, 17, 16)
        $surface = [System.Drawing.Color]::FromArgb(21, 26, 23)
        $greenDark = [System.Drawing.Color]::FromArgb(30, 81, 40)
        $greenPrimary = [System.Drawing.Color]::FromArgb(78, 159, 61)
        $greenBright = [System.Drawing.Color]::FromArgb(122, 201, 67)

        for ($y = 0; $y -lt $source.Height; $y++) {
            for ($x = 0; $x -lt $source.Width; $x++) {
                $pixel = $source.GetPixel($x, $y)
                if ($pixel.A -le 10) {
                    $recolored.SetPixel($x, $y, $pixel)
                    continue
                }

                $luminance = (0.2126 * $pixel.R + 0.7152 * $pixel.G + 0.0722 * $pixel.B) / 255
                $target = if ($luminance -lt 0.08) { $background } elseif ($luminance -lt 0.18) { $surface } elseif ($luminance -lt 0.38) { $greenDark } elseif ($luminance -lt 0.55) { $greenPrimary } else { $greenBright }
                $recolored.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($pixel.A, $target.R, $target.G, $target.B))
            }
        }

        $temporaryPath = "$pngPath.green-v2.tmp.png"
        $recolored.Save($temporaryPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $source.Dispose()
        Move-Item -LiteralPath $temporaryPath -Destination $pngPath -Force
    }
    finally {
        $recolored.Dispose()
        $source.Dispose()
    }
}

function Convert-PngToIco([string]$pngPath, [string]$icoPath) {
    if (-not (Test-Path -LiteralPath $pngPath)) {
        throw "Missing source PNG: $pngPath"
    }

    $source = [System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $pngPath))
    try {
        $minX = $source.Width
        $minY = $source.Height
        $maxX = -1
        $maxY = -1

        for ($y = 0; $y -lt $source.Height; $y++) {
            for ($x = 0; $x -lt $source.Width; $x++) {
                if ($source.GetPixel($x, $y).A -gt 10) {
                    if ($x -lt $minX) { $minX = $x }
                    if ($x -gt $maxX) { $maxX = $x }
                    if ($y -lt $minY) { $minY = $y }
                    if ($y -gt $maxY) { $maxY = $y }
                }
            }
        }

        if ($maxX -ge $minX -and $maxY -ge $minY) {
            $cropPadding = 0
            $cropX = [Math]::Max(0, $minX - $cropPadding)
            $cropY = [Math]::Max(0, $minY - $cropPadding)
            $cropRight = [Math]::Min($source.Width - 1, $maxX + $cropPadding)
            $cropBottom = [Math]::Min($source.Height - 1, $maxY + $cropPadding)
            $sourceRect = New-Object System.Drawing.Rectangle $cropX, $cropY, ($cropRight - $cropX + 1), ($cropBottom - $cropY + 1)
        }
        else {
            $sourceRect = New-Object System.Drawing.Rectangle 0, 0, $source.Width, $source.Height
        }

        $iconSizes = @(16, 24, 32, 48, 64, 128, 256)
        $entries = @()

        foreach ($iconSize in $iconSizes) {
            $scaled = New-Object System.Drawing.Bitmap $iconSize, $iconSize, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $graphics = [System.Drawing.Graphics]::FromImage($scaled)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

                $padding = if ($iconSize -le 32) { 0 } else { [int][Math]::Ceiling($iconSize * 0.02) }
                $targetSize = $iconSize - ($padding * 2)
                $scale = [Math]::Min($targetSize / $sourceRect.Width, $targetSize / $sourceRect.Height)
                $width = [int][Math]::Round($sourceRect.Width * $scale)
                $height = [int][Math]::Round($sourceRect.Height * $scale)
                $x = [int][Math]::Floor(($iconSize - $width) / 2)
                $y = [int][Math]::Floor(($iconSize - $height) / 2)
                $targetRect = New-Object System.Drawing.Rectangle $x, $y, $width, $height
                $graphics.DrawImage($source, $targetRect, $sourceRect, [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally {
                $graphics.Dispose()
            }

            try {
                [byte[]]$dibBytes = Convert-BitmapToIconDib $scaled
                $entries += [pscustomobject]@{ Size = $iconSize; Bytes = $dibBytes }
            }
            finally {
                $scaled.Dispose()
            }
        }

        $stream = [System.IO.File]::Create($icoPath)
        $writer = New-Object System.IO.BinaryWriter $stream
        try {
            $writer.Write([UInt16]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]$entries.Count)

            $offset = 6 + ($entries.Count * 16)
            foreach ($entry in $entries) {
                $sizeByte = if ($entry.Size -eq 256) { 0 } else { $entry.Size }
                $writer.Write([byte]$sizeByte)
                $writer.Write([byte]$sizeByte)
                $writer.Write([byte]0)
                $writer.Write([byte]0)
                $writer.Write([UInt16]1)
                $writer.Write([UInt16]32)
                $writer.Write([UInt32]$entry.Bytes.Length)
                $writer.Write([UInt32]$offset)
                $offset += $entry.Bytes.Length
            }

            foreach ($entry in $entries) {
                $writer.Write($entry.Bytes)
            }
        }
        finally {
            $writer.Dispose()
            $stream.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }
}

function Convert-BitmapToIconDib([System.Drawing.Bitmap]$bitmap) {
    $width = $bitmap.Width
    $height = $bitmap.Height
    $xorStride = $width * 4
    $andStride = [int]([Math]::Ceiling($width / 32) * 4)
    $xorBytes = $xorStride * $height
    $andBytes = $andStride * $height

    $memory = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter $memory
    try {
        $writer.Write([UInt32]40)
        $writer.Write([Int32]$width)
        $writer.Write([Int32]($height * 2))
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]0)
        $writer.Write([UInt32]$xorBytes)
        $writer.Write([Int32]0)
        $writer.Write([Int32]0)
        $writer.Write([UInt32]0)
        $writer.Write([UInt32]0)

        for ($y = $height - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $width; $x++) {
                $pixel = $bitmap.GetPixel($x, $y)
                $writer.Write([byte]$pixel.B)
                $writer.Write([byte]$pixel.G)
                $writer.Write([byte]$pixel.R)
                $writer.Write([byte]$pixel.A)
            }
        }

        $writer.Write((New-Object byte[] $andBytes))
        return $memory.ToArray()
    }
    finally {
        $writer.Dispose()
        $memory.Dispose()
    }
}

Recolor-PngToGreenV2 $appPngPath
Convert-PngToIco $appPngPath $appIcoPath
Copy-Item -LiteralPath $appIcoPath -Destination $shortcutIcoPath -Force

Get-ChildItem -LiteralPath $appPngPath, $appIcoPath, $shortcutIcoPath | Select-Object FullName, Length
