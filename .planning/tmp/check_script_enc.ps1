$bytes = [System.IO.File]::ReadAllBytes('C:\Info\Project\DataMeasurement\.planning\tmp\edit_research.ps1')
Write-Host ("Length: " + $bytes.Length)
$first = $bytes[0..10]
Write-Host ("First bytes: " + ($first -join ' '))
# Read as UTF-8
$utf8 = New-Object System.Text.UTF8Encoding($false)
$c = [System.IO.File]::ReadAllText('C:\Info\Project\DataMeasurement\.planning\tmp\edit_research.ps1', $utf8)
$idx = $c.IndexOf('CTH 1개')
Write-Host ("CTH idx: " + $idx)
$idx2 = $c.IndexOf('Fallback')
Write-Host ("Fallback idx: " + $idx2)
