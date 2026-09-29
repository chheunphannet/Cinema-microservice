Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$font12 = New-Object System.Drawing.Font("Segoe UI", 12.0, [System.Drawing.FontStyle]::Bold)
$font11 = New-Object System.Drawing.Font("Segoe UI", 11.0, [System.Drawing.FontStyle]::Bold)
$font10 = New-Object System.Drawing.Font("Segoe UI", 10.0, [System.Drawing.FontStyle]::Bold)

$text = "🛒  PAY / BUY [F12]  •  $9,999.00"

$sz12 = [System.Windows.Forms.TextRenderer]::MeasureText($text, $font12)
$sz11 = [System.Windows.Forms.TextRenderer]::MeasureText($text, $font11)
$sz10 = [System.Windows.Forms.TextRenderer]::MeasureText($text, $font10)

Write-Host "Width at 12pt: $($sz12.Width)"
Write-Host "Width at 11pt: $($sz11.Width)"
Write-Host "Width at 10pt: $($sz10.Width)"
