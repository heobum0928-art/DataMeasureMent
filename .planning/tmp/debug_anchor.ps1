[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$utf8 = New-Object System.Text.UTF8Encoding($false)
$f = 'C:\Info\Project\DataMeasurement\.planning\phases\23-top-1-a-simul-end-to-end-2026-05-11\23-RESEARCH.md'
$c = [System.IO.File]::ReadAllText($f, $utf8)
$LF = [char]10

# Check first 200 chars after Q1 marker
$idx = $c.IndexOf("Fallback: A1")
if ($idx -lt 0) { Write-Host "Fallback: A1 not found"; exit 1 }
Write-Host "Fallback idx: $idx"
$snippet = $c.Substring($idx, 200)
Write-Host "Snippet: [$snippet]"
