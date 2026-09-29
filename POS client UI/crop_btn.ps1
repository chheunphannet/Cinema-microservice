Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Bitmap]::FromFile('CinemaPOS.App/bin/Debug/net10.0-windows/main_max_screenshot.png')
$crop = New-Object System.Drawing.Bitmap(368, 103)
$g = [System.Drawing.Graphics]::FromImage($crop)
$srcRect = New-Object System.Drawing.Rectangle(1561, 912, 368, 103)
$destRect = New-Object System.Drawing.Rectangle(0, 0, 368, 103)
$g.DrawImage($img, $destRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
$crop.Save('button_crop.png', [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$crop.Dispose()
$img.Dispose()
Write-Host "button_crop.png saved"
