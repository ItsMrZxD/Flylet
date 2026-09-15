# Draws Flylet's icon (two stacked rounded cards, like the flyouts it shows) at every size
# the app and package need. Run tools/make-icons.ps1 from the repo. Originals are in git history.
param([string]$Repo = (Split-Path $PSScriptRoot -Parent))

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$GradientFrom = '#2563EB'
$GradientTo = '#22D3EE'

function Add-Round($path, $x, $y, $w, $h, $r) {
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
}

function New-IconBitmap([int]$w, [int]$h, [string]$mode) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [Math]::Min($w, $h)
    $fg = [System.Drawing.Color]::White

    if ($mode -eq 'tile') {
        $p = New-Object System.Drawing.Drawing2D.GraphicsPath
        Add-Round $p 0 0 $w $h ($s * 0.22)
        $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point $w, $h),
            [System.Drawing.ColorTranslator]::FromHtml($GradientFrom),
            [System.Drawing.ColorTranslator]::FromHtml($GradientTo))
        $g.FillPath($brush, $p); $brush.Dispose(); $p.Dispose()
        $cardW = $s * 0.50; $rearX = 0.18; $rearY = 0.34; $frontX = 0.32; $frontY = 0.22
    }
    else {
        if ($mode -eq 'black') { $fg = [System.Drawing.Color]::Black }
        $cardW = $s * 0.58; $rearX = 0.10; $rearY = 0.38; $frontX = 0.32; $frontY = 0.20
    }

    # Two stacked cards: the rear one only shows along its bottom-left edge
    $cardH = $cardW * 0.76
    $radius = [Math]::Max(1.5, $cardW * 0.20)
    $offsetX = ($w - $s) / 2.0
    $offsetY = ($h - $s) / 2.0

    $solid = New-Object System.Drawing.SolidBrush $fg
    $faded = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(125, $fg.R, $fg.G, $fg.B))

    $rear = New-Object System.Drawing.Drawing2D.GraphicsPath
    Add-Round $rear ($offsetX + $s * $rearX) ($offsetY + $s * $rearY) $cardW $cardH $radius
    $g.FillPath($faded, $rear); $rear.Dispose()

    $front = New-Object System.Drawing.Drawing2D.GraphicsPath
    Add-Round $front ($offsetX + $s * $frontX) ($offsetY + $s * $frontY) $cardW $cardH $radius
    $g.FillPath($solid, $front); $front.Dispose()

    $solid.Dispose(); $faded.Dispose(); $g.Dispose()
    return $bmp
}

function Get-Mode([string]$name) {
    if ($name -match 'unplated|LockScreen|White') { return 'white' }
    if ($name -match 'Black') { return 'black' }
    if ($name -match 'SplashScreen|Wide') { return 'white' }
    return 'tile'
}

function Save-Ico([string]$path, [int[]]$sizes, [string]$mode) {
    $frames = foreach ($size in $sizes) {
        $bmp = New-IconBitmap $size $size $mode
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        , $ms.ToArray()
    }
    $fs = [System.IO.File]::Create($path)
    $bw = New-Object System.IO.BinaryWriter $fs
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    for ($i = 0; $i -lt $frames.Count; $i++) {
        $size = $sizes[$i]
        $bw.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))
        $bw.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))
        $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$frames[$i].Length); $bw.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $bw.Write($frame) }
    $bw.Flush(); $bw.Dispose(); $fs.Dispose()
}

$count = 0

Get-ChildItem (Join-Path $Repo 'ModernFlyouts.Package\Images') -Filter *.png |
    Where-Object Name -notlike '*backup*' | ForEach-Object {
        $img = [System.Drawing.Image]::FromFile($_.FullName)
        $w = $img.Width; $h = $img.Height
        $img.Dispose()
        $bmp = New-IconBitmap $w $h (Get-Mode $_.Name)
        $bmp.Save($_.FullName, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        $count++
    }

Get-ChildItem (Join-Path $Repo 'ModernFlyouts\Assets\Images') -Filter 'ModernFlyouts_*.png' | ForEach-Object {
    $img = [System.Drawing.Image]::FromFile($_.FullName)
    $w = $img.Width; $h = $img.Height
    $img.Dispose()
    $bmp = New-IconBitmap $w $h (Get-Mode $_.Name)
    $bmp.Save($_.FullName, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $count++
}

Save-Ico (Join-Path $Repo 'ModernFlyouts\Assets\Logo.ico') @(16, 24, 32, 48, 64, 128, 256) 'tile'
Save-Ico (Join-Path $Repo 'ModernFlyouts\Assets\Logo_Tray_White.ico') @(16, 20, 24, 32, 48) 'white'
Save-Ico (Join-Path $Repo 'ModernFlyouts\Assets\Logo_Tray_Black.ico') @(16, 20, 24, 32, 48) 'black'

"redrew $count png files plus 3 ico files"

