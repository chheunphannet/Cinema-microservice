Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Bitmap]::FromFile('CinemaPOS.App/bin/Debug/net10.0-windows/main_max_screenshot.png')
for ($x = 1570; $x -lt 1928; $x++) {
    for ($y = 930; $y -lt 990; $y++) {
        $c = $img.GetPixel($x, $y)
        if ($c.R -gt 240 -and $c.G -gt 240 -and $c.B -gt 240) {
            # Find the max x of white text
            $maxX = $x
            break
        }
    }
}
Write-Host "Max X of white text in button: $maxX"
