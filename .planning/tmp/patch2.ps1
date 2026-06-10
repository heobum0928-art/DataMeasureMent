$p = 'C:\Info\Project\DataMeasurement\.planning\phases\39.3-dualimage-fai-ux-redesign-2026-05-30\39.3-RESEARCH.md'
$b = [System.IO.File]::ReadAllBytes($p)
$utf8 = [System.Text.UTF8Encoding]::new($false)
$s = $utf8.GetString($b)
# Use only ASCII anchor + Korean unicode escapes
$bt = [char]96
# Build search/replace using surrogate-safe characters from file
# Search for byte sequence: "RESOLVED: " + EA B0 80 EB A1 9C + " " + EC 8A AC EB A1 AF + " = ShotConfig.SimulImagePath (D-34.1-08 " + EB 8C 80 EC B9 AD + ")"
$ko_garo = [System.Text.Encoding]::UTF8.GetString([byte[]](0xEA,0xB0,0x80,0xEB,0xA1,0x9C))
$ko_slot = [System.Text.Encoding]::UTF8.GetString([byte[]](0xEC,0x8A,0xAC,0xEB,0xA1,0xAF))
$ko_sym  = [System.Text.Encoding]::UTF8.GetString([byte[]](0xEB,0x8C,0x80,0xEC,0xB9,0xAD))
$old = 'RESOLVED: ' + $ko_garo + ' ' + $ko_slot + ' = ShotConfig.SimulImagePath (D-34.1-08 ' + $ko_sym + ')'
$new = 'RESOLVED: ' + $ko_garo + ' ' + $ko_slot + ' = ' + $bt + 'ShotConfig.SimulImagePath' + $bt + ' (D-34.1-08 ' + $ko_sym + ')'
if (-not $s.Contains($old)) { Write-Host 'NOT_FOUND'; exit 2 }
$s = $s.Replace($old, $new)
[System.IO.File]::WriteAllBytes($p, $utf8.GetBytes($s))
Write-Host 'OK'
