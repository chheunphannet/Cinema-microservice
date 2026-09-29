Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Bitmap]::FromFile('CinemaPOS.App/bin/Debug/net10.0-windows/main_max_screenshot.png')
# Check where the left border of _rightPanel is at y = 500
for ($x = 1350; $x -lt 1650; $x++) {
    $c = $img.GetPixel($x, 500)
    # The left border color is #CBD5E1: R=203, G=213, B=225
    if ($c.R -eq 203 -and $c.G -eq 213 -and $c.B -eq 225) {
        Write-Host "Found left border of _rightPanel at x=${x}"
    }
}
