$src = 'C:\Info\Project\DataMeasurement\.planning\tmp\edit_plan01.ps1'
$dst = 'C:\Info\Project\DataMeasurement\.planning\tmp\edit_plan01_bom.ps1'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$content = [System.IO.File]::ReadAllText($src, $utf8)
$utf8bom = New-Object System.Text.UTF8Encoding($true)
[System.IO.File]::WriteAllText($dst, $content, $utf8bom)
Write-Host "Wrote BOM copy: $dst"
