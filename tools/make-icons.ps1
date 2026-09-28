# Draws Flylet's icon (two stacked rounded cards, like the flyouts it shows) at every size
# the app and package need. Run tools/make-icons.ps1 from the repo. Originals are in git history.
param(
    [string]$Repo = (Split-Path $PSScriptRoot -Parent),
    # Render one image to this path instead of regenerating the app's icons, e.g. a Store listing logo
    [string]$SingleImage,
    [int]$SingleSize = 300,
    [ValidateSet('tile', 'white', 'black')][string]$SingleMode = 'tile'
)

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
    # lightunplated = drawn on a light background, so the glyph itself must be dark
    if ($name -match 'lightunplated') { return 'black' }
    if ($name -match 'unplated|LockScreen|White') { return 'white' }
    if ($name -match 'Black') { return 'black' }
    if ($name -match 'SplashScreen|Wide') { return 'white' }
    return 'tile'
}

# Packs a 32bpp bitmap as the classic ICO/BMP DIB frame format (BITMAPINFOHEADER + bottom-up
# BGRA pixels + a 1bpp AND mask). Some icon consumers, including the taskbar's small window-icon
# path, don't render PNG-compressed frames below 256px and show a blank icon instead.
function Get-DibBytes([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width; $h = $bmp.Height
    $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    # 1bpp mask, rows padded to a 4-byte boundary. Consumers that honor 32bpp alpha ignore this and
    # get real per-pixel transparency; consumers that don't (older shell/ImageList paths) fall back to
    # it, so a fully-zero mask would show the transparent background as solid black for them - a bit
    # is only set (masked out) where the source pixel is fully transparent.
    $maskRowBytes = [Math]::Ceiling($w / 32.0) * 4
    $mask = New-Object byte[] ($maskRowBytes * $h)

    $xor = New-Object byte[] ($w * $h * 4)
    $row = New-Object byte[] $data.Stride
    for ($y = 0; $y -lt $h; $y++) {
        # DIB rows are stored bottom-up
        $srcPtr = [IntPtr]::Add($data.Scan0, $y * $data.Stride)
        [System.Runtime.InteropServices.Marshal]::Copy($srcPtr, $row, 0, $data.Stride)
        $destRow = $h - 1 - $y
        [Array]::Copy($row, 0, $xor, $destRow * $w * 4, $w * 4)

        $maskRowOffset = $destRow * $maskRowBytes
        for ($x = 0; $x -lt $w; $x++) {
            if ($row[$x * 4 + 3] -eq 0) {
                $byteIndex = $maskRowOffset + [Math]::Floor($x / 8)
                $mask[$byteIndex] = $mask[$byteIndex] -bor (1 -shl (7 - ($x % 8)))
            }
        }
    }
    $bmp.UnlockBits($data)

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    $bw.Write([uint32]40)                       # biSize
    $bw.Write([int32]$w)                        # biWidth
    $bw.Write([int32]($h * 2))                  # biHeight (XOR + AND)
    $bw.Write([uint16]1)                        # biPlanes
    $bw.Write([uint16]32)                       # biBitCount
    $bw.Write([uint32]0)                        # biCompression = BI_RGB
    $bw.Write([uint32]($xor.Length + $mask.Length))
    $bw.Write([int32]0); $bw.Write([int32]0)    # biXPelsPerMeter, biYPelsPerMeter
    $bw.Write([uint32]0); $bw.Write([uint32]0)  # biClrUsed, biClrImportant
    $bw.Write($xor)
    $bw.Write($mask)
    $bw.Flush()
    # No `return`: it would unroll the byte[] into individual bytes on the pipeline.
    , $ms.ToArray()
}

function Save-Ico([string]$path, [int[]]$sizes, [string]$mode) {
    $frames = foreach ($size in $sizes) {
        $bmp = New-IconBitmap $size $size $mode
        if ($size -ge 256) {
            $ms = New-Object System.IO.MemoryStream
            $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
            $bytes = $ms.ToArray()
        }
        else {
            $bytes = Get-DibBytes $bmp
        }
        $bmp.Dispose()
        , $bytes
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

if ($SingleImage) {
    $bmp = New-IconBitmap $SingleSize $SingleSize $SingleMode
    $bmp.Save($SingleImage, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    "wrote $SingleImage ($SingleSize x $SingleSize, $SingleMode)"
    return
}

$count = 0

Get-ChildItem (Join-Path $Repo 'Flylet.Package\Images') -Filter *.png |
    Where-Object Name -notlike '*backup*' | ForEach-Object {
        $img = [System.Drawing.Image]::FromFile($_.FullName)
        $w = $img.Width; $h = $img.Height
        $img.Dispose()
        $bmp = New-IconBitmap $w $h (Get-Mode $_.Name)
        $bmp.Save($_.FullName, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        $count++
    }

Get-ChildItem (Join-Path $Repo 'Flylet\Assets\Images') -Filter 'Flylet_*.png' | ForEach-Object {
    $img = [System.Drawing.Image]::FromFile($_.FullName)
    $w = $img.Width; $h = $img.Height
    $img.Dispose()
    $bmp = New-IconBitmap $w $h (Get-Mode $_.Name)
    $bmp.Save($_.FullName, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $count++
}

Save-Ico (Join-Path $Repo 'Flylet\Assets\Logo.ico') @(16, 24, 32, 48, 64, 128, 256) 'tile'
Save-Ico (Join-Path $Repo 'Flylet\Assets\Logo_Tray_White.ico') @(16, 20, 24, 32, 48) 'white'
Save-Ico (Join-Path $Repo 'Flylet\Assets\Logo_Tray_Black.ico') @(16, 20, 24, 32, 48) 'black'

"redrew $count png files plus 3 ico files"

