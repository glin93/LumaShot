$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$frames = @()
foreach ($size in @(16,20,24,32,40,48,64,128,256)) {
    $canvas = New-Object System.Drawing.Bitmap ($size*4),($size*4)
    $g = [Drawing.Graphics]::FromImage($canvas)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.ScaleTransform(($size*4/256.0),($size*4/256.0))
    $shape = New-Object Drawing.Drawing2D.GraphicsPath
    $shape.AddArc(8,8,96,96,180,90); $shape.AddArc(152,8,96,96,270,90)
    $shape.AddArc(152,152,96,96,0,90); $shape.AddArc(8,152,96,96,90,90); $shape.CloseFigure()
    $fill = New-Object Drawing.Drawing2D.LinearGradientBrush ([Drawing.Point]::new(32,8)),([Drawing.Point]::new(224,248)),([Drawing.Color]::FromArgb(153,127,250)),([Drawing.Color]::FromArgb(99,77,209))
    $g.FillPath($fill,$shape)
    $pen = New-Object Drawing.Pen ([Drawing.Color]::White),13
    $pen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
    $points = [Drawing.PointF[]]@([Drawing.PointF]::new(128,55),[Drawing.PointF]::new(201,128),[Drawing.PointF]::new(128,201),[Drawing.PointF]::new(55,128))
    $g.DrawPolygon($pen,$points)
    $inner = [Drawing.PointF[]]@([Drawing.PointF]::new(128,91),[Drawing.PointF]::new(165,128),[Drawing.PointF]::new(128,165),[Drawing.PointF]::new(91,128))
    $g.FillPolygon([Drawing.Brushes]::White,$inner)
    $g.Dispose(); $shape.Dispose(); $fill.Dispose(); $pen.Dispose()
    $bitmap = New-Object Drawing.Bitmap $size,$size
    $scaled = [Drawing.Graphics]::FromImage($bitmap)
    $scaled.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $scaled.DrawImage($canvas,0,0,$size,$size); $scaled.Dispose(); $canvas.Dispose()
    $stream = New-Object IO.MemoryStream
    $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $frames += [pscustomobject]@{Size=$size;Bytes=$stream.ToArray()}
    if($size -eq 256){$bitmap.Save((Join-Path $PSScriptRoot 'LumaShot-icon.png'),[Drawing.Imaging.ImageFormat]::Png)}
    $stream.Dispose(); $bitmap.Dispose()
}
$out = [IO.File]::Create((Join-Path $PSScriptRoot 'LumaShot.ico'))
$writer = New-Object IO.BinaryWriter $out
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach($frame in $frames){
        $dimension = if($frame.Size -eq 256){0}else{$frame.Size}
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach($frame in $frames){$writer.Write([byte[]]$frame.Bytes)}
} finally {$writer.Dispose(); $out.Dispose()}
