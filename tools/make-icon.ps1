# Generates the app icon from programIcon.png:
# 1) removes the solid background (flood fill from the edges with color tolerance)
# 2) trims transparent margins and fits the image into a square
# 3) builds Assets/app.ico with 16/24/32/48/64/256 sizes
param(
    [string]$InputPath  = (Join-Path $PSScriptRoot "..\programIcon.png"),
    [string]$OutputIco  = (Join-Path $PSScriptRoot "..\Assets\app.ico"),
    [string]$OutputPng  = (Join-Path $PSScriptRoot "..\Assets\app-icon.png"),
    [int]$Tolerance     = 45
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$bmp = New-Object System.Drawing.Bitmap($InputPath)
$w = $bmp.Width
$h = $bmp.Height
Write-Host "Input image: ${w}x${h} ($($bmp.PixelFormat))"

# --- read pixels into a byte array ---
$rect  = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
$bd    = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$stride = $bd.Stride
$data  = New-Object byte[] ($stride * $h)
[System.Runtime.InteropServices.Marshal]::Copy($bd.Scan0, $data, 0, $data.Length)
$bmp.UnlockBits($bd)
$bmp.Dispose()

function Idx([int]$x, [int]$y) { $y * $stride + $x * 4 }

# --- background color: average of the 4 corners ---
$cornerIdx = @( (Idx 0 0), (Idx ($w-1) 0), (Idx 0 ($h-1)), (Idx ($w-1) ($h-1)) )
$bgB = 0; $bgG = 0; $bgR = 0
foreach ($ci in $cornerIdx) { $bgB += $data[$ci]; $bgG += $data[$ci+1]; $bgR += $data[$ci+2] }
$bgB = [int]($bgB / 4); $bgG = [int]($bgG / 4); $bgR = [int]($bgR / 4)
Write-Host "Background color (corners): R=$bgR G=$bgG B=$bgB (tolerance $Tolerance)"

function Is-Background([int]$i) {
    ([Math]::Abs($data[$i]     - $bgB) -le $Tolerance -and
     [Math]::Abs($data[$i + 1] - $bgG) -le $Tolerance -and
     [Math]::Abs($data[$i + 2] - $bgR) -le $Tolerance)
}

# --- flood fill from the edges ---
$queue = New-Object 'System.Collections.Generic.Queue[int]'
$visited = New-Object bool[] ($w * $h)
for ($x = 0; $x -lt $w; $x++) {
    foreach ($y in @(0, ($h - 1))) {
        $i = Idx $x $y
        if ((Is-Background $i) -and -not $visited[$y * $w + $x]) {
            $visited[$y * $w + $x] = $true; $queue.Enqueue($i)
        }
    }
}
for ($y = 0; $y -lt $h; $y++) {
    foreach ($x in @(0, ($w - 1))) {
        $i = Idx $x $y
        if ((Is-Background $i) -and -not $visited[$y * $w + $x]) {
            $visited[$y * $w + $x] = $true; $queue.Enqueue($i)
        }
    }
}

$removed = 0
$alphaIndex = @{}   # indices of pixels made transparent (for edge softening)
while ($queue.Count -gt 0) {
    $i = $queue.Dequeue()
    $data[$i + 3] = 0
    $removed++
    $alphaIndex[$i] = $true
    $pi = [int]($i / 4)
    $px = [int]($pi % $w); $py = [int]($pi / $w)
    foreach ($d in @(-1, 1)) {
        $nx = $px + $d; $ny = $py + $d
        if ($nx -ge 0 -and $nx -lt $w) {
            $ni = Idx $nx $py; $k = $py * $w + $nx
            if ((Is-Background $ni) -and -not $visited[$k]) { $visited[$k] = $true; $queue.Enqueue($ni) }
        }
        if ($ny -ge 0 -and $ny -lt $h) {
            $ni = Idx $px $ny; $k = $ny * $w + $px
            if ((Is-Background $ni) -and -not $visited[$k]) { $visited[$k] = $true; $queue.Enqueue($ni) }
        }
    }
}
Write-Host "Removed background pixels: $removed of $($w * $h)"

# --- edge softening (anti-halo): pixels next to transparent ones get
#     alpha scaled by how close their color is to the background ---
$soft = $Tolerance * 2
foreach ($i in @($alphaIndex.Keys)) {
    $pi = [int]($i / 4)
    $px = [int]($pi % $w); $py = [int]($pi / $w)
    foreach ($d in @(-1, 1)) {
        foreach ($n in @(@(($px + $d), $py), @($px, ($py + $d)))) {
            $nx = $n[0]; $ny = $n[1]
            if ($nx -lt 0 -or $nx -ge $w -or $ny -lt 0 -or $ny -ge $h) { continue }
            $ni = Idx $nx $ny
            if ($data[$ni + 3] -eq 0) { continue }
            $dist = [Math]::Max([Math]::Max([Math]::Abs($data[$ni] - $bgB), [Math]::Abs($data[$ni+1] - $bgG)), [Math]::Abs($data[$ni+2] - $bgR))
            if ($dist -lt $soft) {
                $a = [int]($data[$ni + 3] * $dist / $soft)
                if ($a -lt $data[$ni + 3]) { $data[$ni + 3] = $a }
            }
        }
    }
}

# --- trim transparent margins and fit into a square ---
$minX = $w; $minY = $h; $maxX = -1; $maxY = -1
for ($y = 0; $y -lt $h; $y++) {
    for ($x = 0; $x -lt $w; $x++) {
        if ($data[(Idx $x $y) + 3] -gt 10) {
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}
if ($maxX -lt 0) { throw "All pixels became transparent - check the tolerance." }
Write-Host "Object bounds: [$minX,$minY] - [$maxX,$maxY]"

$cw = $maxX - $minX + 1
$ch = $maxY - $minY + 1
$side = [Math]::Max($cw, $ch)

$pin = [System.Runtime.InteropServices.GCHandle]::Alloc($data, 'Pinned')
$src = New-Object System.Drawing.Bitmap($w, $h, $stride, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb, $pin.AddrOfPinnedObject())

$square = New-Object System.Drawing.Bitmap($side, $side)
$g = [System.Drawing.Graphics]::FromImage($square)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode  = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.DrawImage($src, [System.Drawing.Rectangle]::new([int](($side - $cw) / 2), [int](($side - $ch) / 2), $cw, $ch),
             [System.Drawing.Rectangle]::new($minX, $minY, $cw, $ch), [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()
$pin.Free()
$src.Dispose()

# --- save the square PNG ---
New-Item -ItemType Directory -Force -Path (Split-Path $OutputPng) | Out-Null
$square.Save($OutputPng, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "PNG saved: $OutputPng (${side}x${side})"

# --- build the ICO ---
function Get-PngBytes([System.Drawing.Bitmap]$bm) {
    $ms = New-Object System.IO.MemoryStream
    $bm.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $ms.ToArray()
}

function Get-BmpEntry([System.Drawing.Bitmap]$bm, [int]$s) {
    # ICO BMP entry: BITMAPINFOHEADER + BGRA rows (bottom-up) + AND mask
    $bmpData = $bm.LockBits((New-Object System.Drawing.Rectangle(0, 0, $s, $s)),
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $px = New-Object byte[] ($s * $s * 4)
    [System.Runtime.InteropServices.Marshal]::Copy($bmpData.Scan0, $px, 0, $px.Length)
    $bm.UnlockBits($bmpData)

    $xor = New-Object byte[] ($s * $s * 4)
    for ($y = 0; $y -lt $s; $y++) {
        $srcRow = $y * $s * 4
        $dstRow = ($s - 1 - $y) * $s * 4
        [Array]::Copy($px, $srcRow, $xor, $dstRow, $s * 4)
    }

    $maskStride = [int]([Math]::Ceiling($s / 32.0) * 4)
    $mask = New-Object byte[] ($maskStride * $s)   # all zeros - transparency via alpha channel

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    $bw.Write([uint32]40); $bw.Write([int32]$s); $bw.Write([int32]($s * 2))
    $bw.Write([uint16]1);  $bw.Write([uint16]32); $bw.Write([uint32]0)
    $bw.Write([uint32]($xor.Length + $mask.Length)); $bw.Write([uint32]0); $bw.Write([uint32]0)
    $bw.Write([uint32]0);  $bw.Write([uint32]0)
    $bw.Write($xor); $bw.Write($mask)
    $bw.Flush()
    $ms.ToArray()
}

$sizes  = @(16, 24, 32, 48, 64, 256)
$imageList = New-Object 'System.Collections.Generic.List[byte[]]'
foreach ($s in $sizes) {
    $bm = New-Object System.Drawing.Bitmap($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bm)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($square, 0, 0, $s, $s)
    $g.Dispose()
    if ($s -le 64) { $imageList.Add((Get-BmpEntry $bm $s)) } else { $imageList.Add((Get-PngBytes $bm)) }
    $bm.Dispose()
}

New-Item -ItemType Directory -Force -Path (Split-Path $OutputIco) | Out-Null
$fs = [System.IO.File]::Create($OutputIco)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$imageList.Count)

$offset = 6 + 16 * $imageList.Count
for ($k = 0; $k -lt $imageList.Count; $k++) {
    $s = $sizes[$k]
    $bw.Write([byte]($s % 256)); $bw.Write([byte]($s % 256))   # 256 is encoded as 0
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$imageList[$k].Length)
    $bw.Write([uint32]$offset)
    $offset += $imageList[$k].Length
}
foreach ($img in $imageList) { $bw.Write($img) }
$bw.Flush(); $bw.Dispose()
$square.Dispose()

$icoSize = (Get-Item $OutputIco).Length
Write-Host "ICO saved: $OutputIco ($icoSize bytes)"
Write-Host "Done."
