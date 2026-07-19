# make-icon.ps1 — 程序化生成 RoboCopyGUI 应用图标
# 设计：蓝色圆角方块底 + 白色双文档 + 蓝色箭头（拷贝主题）
# 用法: powershell -File make-icon.ps1
# 注意：本文件必须带 UTF-8 BOM 保存，否则 PS5.1 按 GBK 误读中文注释
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$outPath = Join-Path $PSScriptRoot 'app.ico'

function Get-RoundedRectPath([System.Drawing.RectangleF]$rect, [float]$radius) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = $radius * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap([int]$size) {
    $bmp = [System.Drawing.Bitmap]::new($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $s = [float]($size / 256.0)

    # 背景：蓝色渐变圆角方块
    $c1 = [System.Drawing.Color]::FromArgb(0x2B, 0x9A, 0xE4)
    $c2 = [System.Drawing.Color]::FromArgb(0x10, 0x6E, 0xBE)
    $bgRect = [System.Drawing.RectangleF]::new((16 * $s), (16 * $s), (224 * $s), (224 * $s))
    $bgBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($bgRect, $c1, $c2, [float]90)
    $g.FillPath($bgBrush, (Get-RoundedRectPath $bgRect (44 * $s)))

    # 后层文档（半透明白，右上）
    $backBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(110, 255, 255, 255))
    $backRect = [System.Drawing.RectangleF]::new((100 * $s), (52 * $s), (76 * $s), (100 * $s))
    $g.FillPath($backBrush, (Get-RoundedRectPath $backRect (12 * $s)))

    # 前层文档（纯白，左下）
    $frontBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $frontRect = [System.Drawing.RectangleF]::new((66 * $s), (96 * $s), (76 * $s), (100 * $s))
    $g.FillPath($frontBrush, (Get-RoundedRectPath $frontRect (12 * $s)))

    # 前层文档上的蓝色右箭头
    $arrowBrush = [System.Drawing.SolidBrush]::new($c2)
    $pts = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new((82 * $s), (138 * $s)),
        [System.Drawing.PointF]::new((106 * $s), (138 * $s)),
        [System.Drawing.PointF]::new((106 * $s), (128 * $s)),
        [System.Drawing.PointF]::new((126 * $s), (146 * $s)),
        [System.Drawing.PointF]::new((106 * $s), (164 * $s)),
        [System.Drawing.PointF]::new((106 * $s), (154 * $s)),
        [System.Drawing.PointF]::new((82 * $s), (154 * $s))
    )
    $g.FillPolygon($arrowBrush, $pts)

    $g.Dispose()
    return $bmp
}

# 多分辨率打包为 ICO（PNG 压缩条目，Vista+ 支持）
$sizes = @(16, 32, 48, 256)
$pngData = @{}
foreach ($sz in $sizes) {
    $bmp = New-IconBitmap $sz
    $png = [System.IO.MemoryStream]::new()
    $bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngData[$sz] = $png.ToArray()
    $bmp.Dispose()
    $png.Dispose()
}

$ms = [System.IO.MemoryStream]::new()
$bw = [System.IO.BinaryWriter]::new($ms)
$bw.Write([uint16]0)             # reserved
$bw.Write([uint16]1)             # type: icon
$bw.Write([uint16]$sizes.Count)  # image count

$offset = 6 + 16 * $sizes.Count
foreach ($sz in $sizes) {
    $dim = [byte]($sz -band 0xFF)   # 256 在 ICO 中记为 0
    $bw.Write([byte]$dim)           # width
    $bw.Write([byte]$dim)           # height
    $bw.Write([byte]0)              # palette colors
    $bw.Write([byte]0)              # reserved
    $bw.Write([uint16]1)            # color planes
    $bw.Write([uint16]32)           # bits per pixel
    $bw.Write([uint32]$pngData[$sz].Length)
    $bw.Write([uint32]$offset)
    $offset += $pngData[$sz].Length
}
foreach ($sz in $sizes) { $bw.Write($pngData[$sz]) }
$bw.Flush()

[System.IO.File]::WriteAllBytes($outPath, $ms.ToArray())
$bw.Dispose()
$ms.Dispose()

$kb = [math]::Round((Get-Item $outPath).Length / 1KB, 1)
Write-Host "图标已生成: $outPath ($kb KB, 分辨率: $($sizes -join '/'))" -ForegroundColor Green
