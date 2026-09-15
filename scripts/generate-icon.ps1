param(
    [string]$Output = (Join-Path $PSScriptRoot '..\src\UsageNotch.App\Assets\UsageNotch.ico')
)
$ErrorActionPreference = 'Stop'

# Icône de l'application : carré arrondi noir, piste d'anneau gris foncé, arc vert Codenotch rempli aux trois quarts.
# Dessinée avec WPF et empaquetée en .ico (entrées PNG). À relancer seulement si le motif change ; le fichier est commité.
Add-Type -AssemblyName PresentationCore, WindowsBase

function New-Frozen($brush) { $brush.Freeze(); $brush }

$background = New-Frozen (New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.Color]::FromRgb(0x00, 0x00, 0x00)))
$track = New-Frozen (New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.Color]::FromRgb(0x3A, 0x3A, 0x3A)))
$level = New-Frozen (New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.Color]::FromRgb(0x28, 0xE0, 0x7B)))
$fraction = 0.75

function Get-IconPng([int]$size) {
    $s = [double]$size
    # Aux petites tailles, un anneau plus épais reste lisible.
    $thickness = if ($size -le 24) { $s * 0.19 } else { $s * 0.14 }
    $radius = $s * 0.30
    $center = New-Object System.Windows.Point ($s / 2), ($s / 2)

    $visual = New-Object System.Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()
    $corner = $s * 0.22
    $dc.DrawRoundedRectangle($background, $null, (New-Object System.Windows.Rect 0, 0, $s, $s), $corner, $corner)
    $dc.DrawEllipse($null, (New-Object System.Windows.Media.Pen $track, $thickness), $center, $radius, $radius)

    $angle = $fraction * 2 * [Math]::PI
    $start = New-Object System.Windows.Point $center.X, ($center.Y - $radius)
    $end = New-Object System.Windows.Point ($center.X + $radius * [Math]::Sin($angle)), ($center.Y - $radius * [Math]::Cos($angle))
    $geometry = New-Object System.Windows.Media.StreamGeometry
    $ctx = $geometry.Open()
    $ctx.BeginFigure($start, $false, $false)
    $ctx.ArcTo($end, (New-Object System.Windows.Size $radius, $radius), 0, ($fraction -gt 0.5), [System.Windows.Media.SweepDirection]::Clockwise, $true, $false)
    $ctx.Close()
    $geometry.Freeze()
    $pen = New-Object System.Windows.Media.Pen $level, $thickness
    $pen.StartLineCap = [System.Windows.Media.PenLineCap]::Round
    $pen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
    $dc.DrawGeometry($null, $pen, $geometry)
    $dc.Close()

    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap $size, $size, 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    if ($size -ge 256) {
        $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
        $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
        $stream = New-Object System.IO.MemoryStream
        $encoder.Save($stream)
        return , $stream.ToArray()
    }
    , (ConvertTo-IconDib $bitmap $size)
}

# Entrée ICO classique : BITMAPINFOHEADER (hauteur doublée), pixels BGRA de bas en haut en alpha non prémultiplié, masque ET vide.
# Les petites tailles en PNG ne sont pas lues par tous les composants Windows.
function ConvertTo-IconDib($bitmap, [int]$size) {
    $stride = $size * 4
    $pixels = New-Object byte[] ($stride * $size)
    $bitmap.CopyPixels($pixels, $stride, 0)
    $maskRow = [int][Math]::Ceiling($size / 32.0) * 4

    $stream = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $stream
    $w.Write([UInt32]40)
    $w.Write([Int32]$size)
    $w.Write([Int32]($size * 2))
    $w.Write([UInt16]1)
    $w.Write([UInt16]32)
    $w.Write([UInt32]0)
    $w.Write([UInt32]($stride * $size + $maskRow * $size))
    $w.Write([Int32]0); $w.Write([Int32]0); $w.Write([UInt32]0); $w.Write([UInt32]0)
    for ($y = $size - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $size; $x++) {
            $i = ($y * $size + $x) * 4
            $a = $pixels[$i + 3]
            for ($c = 0; $c -lt 3; $c++) {
                $v = if ($a -eq 0) { 0 } else { [Math]::Min(255, [int][Math]::Round($pixels[$i + $c] * 255.0 / $a)) }
                $w.Write([byte]$v)
            }
            $w.Write([byte]$a)
        }
    }
    $w.Write((New-Object byte[] ($maskRow * $size)))
    $w.Flush()
    , $stream.ToArray()
}

$sizes = 16, 24, 32, 48, 256
$images = @(foreach ($size in $sizes) { , [byte[]](Get-IconPng $size) })

# En-tête ICO, puis une entrée de 16 octets par image, puis les PNG.
$out = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $out
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dimension = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dimension)
    $writer.Write([byte]$dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$images[$i].Length)
    $writer.Write([UInt32]$offset)
    $offset += $images[$i].Length
}
foreach ($image in $images) { $writer.Write([byte[]]$image) }
$writer.Flush()

$full = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force (Split-Path $full) | Out-Null
[IO.File]::WriteAllBytes($full, $out.ToArray())
Write-Host "Icône écrite dans $full ($($sizes -join ', ') px)"
