param(
    [Parameter(Mandatory)][string]$Path,
    [string]$Output,
    [double]$Radius = 0.04
)

Add-Type -AssemblyName System.Drawing

$full = (Resolve-Path $Path).Path
if (-not $Output) {
    $Output = Join-Path (Split-Path $full) ("{0}-rounded.png" -f [IO.Path]::GetFileNameWithoutExtension($full))
}

$src = [System.Drawing.Image]::FromFile($full)
try {
    $w = $src.Width; $h = $src.Height
    $d = [int]([Math]::Min($w, $h) * $Radius) * 2
    $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.InterpolationMode = 'HighQualityBicubic'
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc(0, 0, $d, $d, 180, 90)
    $p.AddArc($w - $d - 1, 0, $d, $d, 270, 90)
    $p.AddArc($w - $d - 1, $h - $d - 1, $d, $d, 0, 90)
    $p.AddArc(0, $h - $d - 1, $d, $d, 90, 90)
    $p.CloseFigure()
    $g.SetClip($p)
    $g.DrawImage($src, 0, 0, $w, $h)
    $bmp.Save($Output, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Host "Saved: $Output ($w x $h)"
}
finally { $src.Dispose() }
