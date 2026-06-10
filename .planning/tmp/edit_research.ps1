[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$utf8 = New-Object System.Text.UTF8Encoding($false)

$f = 'C:\Info\Project\DataMeasurement\.planning\phases\23-top-1-a-simul-end-to-end-2026-05-11\23-RESEARCH.md'
$c = [System.IO.File]::ReadAllText($f, $utf8)
$LF = [char]10

# Replace header
$old = "## Open Questions" + $LF + $LF + "1. **PPT"
$new = "## Open Questions (RESOLVED)" + $LF + $LF + "1. **PPT"
if (-not $c.Contains($old)) { Write-Host "Header anchor not found"; exit 1 }
$c = $c.Replace($old, $new)

# Q1 RESOLVED
$old1 = "   - Fallback: A1 (CTH 1개) 로 lock 후 진행. UAT 시 결과 부적합하면 carry-over." + $LF + $LF + "2. **SC#2"
$new1 = "   - Fallback: A1 (CTH 1개) 로 lock 후 진행. UAT 시 결과 부적합하면 carry-over." + $LF + "   - RESOLVED: D-01 lock to CircleTwoHorizontal per CONTEXT.md (사용자 답변 2026-05-11)" + $LF + $LF + "2. **SC#2"
if (-not $c.Contains($old1)) { Write-Host "Q1 anchor not found"; exit 1 }
$c = $c.Replace($old1, $new1)

# Q2 RESOLVED
$old2 = '   - Recommendation: Pitfall 4 의 옵션 (A) — minimal overlay (`RoiId="FAI-Edge1"`, Line = fit 결과) 추가. PLAN 에 명시.' + $LF + $LF + "3. **EdgeSelection"
$new2 = '   - Recommendation: Pitfall 4 의 옵션 (A) — minimal overlay (`RoiId="FAI-Edge1"`, Line = fit 결과) 추가. PLAN 에 명시.' + $LF + '   - RESOLVED: PointToLineDistance pattern (empty overlay) — Plan 23-01 Task 2 + Plan 23-03 Test 2 trust-based PASS' + $LF + $LF + "3. **EdgeSelection"
if (-not $c.Contains($old2)) { Write-Host "Q2 anchor not found"; exit 1 }
$c = $c.Replace($old2, $new2)

# Q3 RESOLVED
$old3 = '   - Recommendation: Phase 23 범위는 TryFitLine 시그니처 확장만 + 기존 caller 는 default "all" 유지. EdgePairDistance 등의 EdgeSelection 명시는 별도 phase 또는 carry-over.' + $LF + $LF + "4. **Datum 찾기"
$new3 = '   - Recommendation: Phase 23 범위는 TryFitLine 시그니처 확장만 + 기존 caller 는 default "all" 유지. EdgePairDistance 등의 EdgeSelection 명시는 별도 phase 또는 carry-over.' + $LF + '   - RESOLVED: TryFitLine 시그니처 확장 (string selection = "all" default) — Plan 23-01 Task 1' + $LF + $LF + "4. **Datum 찾기"
if (-not $c.Contains($old3)) { Write-Host "Q3 anchor not found"; exit 1 }
$c = $c.Replace($old3, $new3)

# Q4 RESOLVED
$old4 = '   - Recommendation: Datum 실패 시 `EdgeToLineDistance` 가 false 반환 → Action_FAIMeasurement 가 NG 처리 → CO-05 자동 빨강. 단, overlay 가 채워졌을 때만 시각 빨강. discuss-phase 에서 사용자 기대 확인.' + $LF + $LF + "## Environment Availability"
$new4 = '   - Recommendation: Datum 실패 시 `EdgeToLineDistance` 가 false 반환 → Action_FAIMeasurement 가 NG 처리 → CO-05 자동 빨강. 단, overlay 가 채워졌을 때만 시각 빨강. discuss-phase 에서 사용자 기대 확인.' + $LF + '   - RESOLVED: D-11 literal 구현 (EdgeToLineDistance.TryExecute guard) — Plan 23-01 Task 2' + $LF + $LF + "## Environment Availability"
if (-not $c.Contains($old4)) { Write-Host "Q4 anchor not found"; exit 1 }
$c = $c.Replace($old4, $new4)

[System.IO.File]::WriteAllText($f, $c, $utf8)
Write-Host "OK"
