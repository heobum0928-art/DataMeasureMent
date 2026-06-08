$p = 'C:\Info\Project\DataMeasurement\.planning\phases\39.3-dualimage-fai-ux-redesign-2026-05-30\39.3-RESEARCH.md'
$s = [System.IO.File]::ReadAllText($p, [System.Text.UTF8Encoding]::new($false))
$bt = [char]96
$ko1 = [char]44032+[char]47196+[char]32+[char]49845+[char]47215  # 가로 슬롯
$ko2 = [char]45824+[char]52845  # 대칭
$old = 'RESOLVED: ' + $ko1 + ' = ShotConfig.SimulImagePath (D-34.1-08 ' + $ko2 + ')'
$new = 'RESOLVED: ' + $ko1 + ' = ' + $bt + 'ShotConfig.SimulImagePath' + $bt + ' (D-34.1-08 ' + $ko2 + ')'
if (-not $s.Contains($old)) { Write-Host ('NOT_FOUND: <' + $old + '>'); exit 2 }
$s = $s.Replace($old, $new)
[System.IO.File]::WriteAllText($p, $s, [System.Text.UTF8Encoding]::new($false))
Write-Host 'OK'