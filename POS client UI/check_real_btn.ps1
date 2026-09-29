Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Bitmap]::FromFile('CinemaPOS.App/bin/Debug/net10.0-windows/main_max_screenshot.png')
$minX = 9999; $maxX = 0; $minY = 9999; $maxY = 0
for ($x = 1550; $x -lt $img.Width; $x++) {
    for ($y = 800; $y -lt $img.Height; $y++) {
        $c = $img.GetPixel($x, $y)
        if ($c.G -gt 140 -and $c.R -lt 50) {
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}
Write-Host "Real button box: X=[$minX, $maxX], Y=[$minY, $maxY], Width=$($maxX - $minX + 1), Height=$($maxY - $minY + 1)"
