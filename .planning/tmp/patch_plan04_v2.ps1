$p = 'C:\Info\Project\DataMeasurement\.planning\phases\39.3-dualimage-fai-ux-redesign-2026-05-30\39.3-04-PLAN.md'
$b = [System.IO.File]::ReadAllBytes($p)
$utf8 = [System.Text.UTF8Encoding]::new($false)
$s = $utf8.GetString($b)

$anchorBytes = [System.IO.File]::ReadAllBytes('C:\Info\Project\DataMeasurement\.planning\tmp\plan04_anchor.txt')
$payloadBytes = [System.IO.File]::ReadAllBytes('C:\Info\Project\DataMeasurement\.planning\tmp\plan04_insert.txt')

$anchor = $utf8.GetString($anchorBytes)
$payload = $utf8.GetString($payloadBytes)

# Normalize line endings to match the target file (CRLF)
$anchor = $anchor -replace "`r`n", "`n"
$anchor = $anchor -replace "`n", "`r`n"
$payload = $payload -replace "`r`n", "`n"
$payload = $payload -replace "`n", "`r`n"

if (-not $s.Contains($anchor)) {
  Write-Host 'ANCHOR_NOT_FOUND'
  # Diagnostic: show first 100 bytes of anchor and search for prefix
  Write-Host ('anchor bytes (first 80): ' + (([System.Text.Encoding]::UTF8.GetBytes($anchor))[0..79] -join ','))
  exit 2
}

$s2 = $s.Replace($anchor, $anchor + $payload)
[System.IO.File]::WriteAllBytes($p, $utf8.GetBytes($s2))
Write-Host 'OK'
