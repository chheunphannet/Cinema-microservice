Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Bitmap]::FromFile('c:\Users\chheu\Pictures\Screenshots\Screenshot 2026-09-27 203553.png')
for ($y = 0; $y -lt $img.Height; $y += 30) {
    $c = $img.GetPixel(1700, $y)
    Write-Host "y=${y} R=$($c.R) G=$($c.G) B=$($c.B)"
}
