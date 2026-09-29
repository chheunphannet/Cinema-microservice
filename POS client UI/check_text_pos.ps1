Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Bitmap]::FromFile('CinemaPOS.App/bin/Debug/net10.0-windows/main_max_screenshot.png')
$c = $img.GetPixel(1700, 940)
Write-Host "Color at (1700, 940): R=$($c.R) G=$($c.G) B=$($c.B)"
