$p = 'C:\Info\Project\DataMeasurement\.planning\phases\39.3-dualimage-fai-ux-redesign-2026-05-30\39.3-02-PLAN.md'
$utf8 = [System.Text.UTF8Encoding]::new($false)
$b = [System.IO.File]::ReadAllBytes($p)
$s = $utf8.GetString($b)

# Detect target line-ending convention
$targetCRLF = $false
for ($i = 0; $i -lt [Math]::Min(4096, $b.Length - 1); $i++) {
  if ($b[$i] -eq 13 -and $b[$i+1] -eq 10) { $targetCRLF = $true; break }
}
if ($targetCRLF) { $targetEOL = "`r`n"; $eolName = 'CRLF' } else { $targetEOL = "`n"; $eolName = 'LF' }
Write-Host ('target EOL: ' + $eolName)

function Read-Text([string]$f, [string]$eol) {
  $bb = [System.IO.File]::ReadAllBytes($f)
  $t = ([System.Text.UTF8Encoding]::new($false)).GetString($bb)
  $t = $t -replace "`r`n", "`n"
  if ($eol -ne "`n") {
    $t = $t -replace "`n", $eol
  }
  return $t
}

$base = 'C:\Info\Project\DataMeasurement\.planning\tmp'

$rf1_anchor      = Read-Text "$base\plan02_02_01_rf_anchor.txt"      $targetEOL
$rf1_payload     = Read-Text "$base\plan02_02_01_rf_payload.txt"     $targetEOL
$rf1_replacement = $rf1_payload + $targetEOL + $rf1_anchor

$act1_anchor      = Read-Text "$base\plan02_02_01_act_anchor.txt"      $targetEOL
$act1_replacement = Read-Text "$base\plan02_02_01_act_replacement.txt" $targetEOL

$rf4_anchor      = Read-Text "$base\plan02_02_04_rf_anchor.txt"      $targetEOL
$rf4_payload     = Read-Text "$base\plan02_02_04_rf_payload.txt"     $targetEOL
$rf4_replacement = $rf4_payload + $targetEOL + $rf4_anchor

$act4_anchor      = Read-Text "$base\plan02_02_04_act_anchor.txt"      $targetEOL
$act4_replacement = Read-Text "$base\plan02_02_04_act_replacement.txt" $targetEOL

$rf5_anchor      = Read-Text "$base\plan02_02_05_rf_anchor.txt"      $targetEOL
$rf5_payload     = Read-Text "$base\plan02_02_05_rf_payload.txt"     $targetEOL
$rf5_replacement = $rf5_payload + $targetEOL + $rf5_anchor

$act5_anchor      = Read-Text "$base\plan02_02_05_act_anchor.txt"      $targetEOL
$act5_replacement = Read-Text "$base\plan02_02_05_act_replacement.txt" $targetEOL

$patches = @(
  @('Task 02-01 read_first', $rf1_anchor, $rf1_replacement),
  @('Task 02-01 action',     $act1_anchor, $act1_replacement),
  @('Task 02-04 read_first', $rf4_anchor, $rf4_replacement),
  @('Task 02-04 action',     $act4_anchor, $act4_replacement),
  @('Task 02-05 read_first', $rf5_anchor, $rf5_replacement),
  @('Task 02-05 action',     $act5_anchor, $act5_replacement)
)

foreach ($pp in $patches) {
  $name = $pp[0]; $anc = $pp[1]; $rep = $pp[2]
  $cnt = ([regex]::Matches($s, [regex]::Escape($anc))).Count
  if ($cnt -eq 0) { Write-Host ("MISS: " + $name); exit 2 }
  if ($cnt -gt 1) { Write-Host ("AMBIGUOUS (" + $cnt + "x): " + $name); exit 3 }
  $s = $s.Replace($anc, $rep)
  Write-Host ("OK: " + $name)
}

[System.IO.File]::WriteAllBytes($p, $utf8.GetBytes($s))
Write-Host 'DONE'
