$f = 'C:\Info\Project\DataMeasurement\.planning\phases\23-top-1-a-simul-end-to-end-2026-05-11\23-RESEARCH.md'
$bytes = [System.IO.File]::ReadAllBytes($f)
$hasCRLF = $false
$hasLFOnly = $false
for ($i = 0; $i -lt $bytes.Length - 1; $i++) {
    if ($bytes[$i] -eq 13 -and $bytes[$i+1] -eq 10) { $hasCRLF = $true; break }
    if ($bytes[$i] -eq 10 -and ($i -eq 0 -or $bytes[$i-1] -ne 13)) { $hasLFOnly = $true }
}
Write-Host "CRLF: $hasCRLF, LFonly: $hasLFOnly"
