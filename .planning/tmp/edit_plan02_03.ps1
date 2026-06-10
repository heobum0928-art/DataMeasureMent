[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$utf8 = New-Object System.Text.UTF8Encoding($false)
$LF = [char]10

# ============================================================
# Plan 23-02: add D-09 F3 precision grep sub-check
# Add to Task 1 acceptance_criteria (since Task 1 is small Factory wiring, attach there;
# alternatively could add to Task 3 build verification. Per revision context: "Plan 23-02 (또는 23-01 Task 2)".
# We add to Plan 23-02 Task 1 acceptance — minimal scope, single grep against MeasurementResultRow.cs)
# ============================================================
$f2 = 'C:\Info\Project\DataMeasurement\.planning\phases\23-top-1-a-simul-end-to-end-2026-05-11\23-02-PLAN.md'
$c2 = [System.IO.File]::ReadAllText($f2, $utf8)

$old2a = '    - `//260511 hbk Phase 23 ALG-01` 마커 grep ≥ 2 매치 (case 라인 + 배열 원소 라인)'
$new2a = '    - `//260511 hbk Phase 23 ALG-01` 마커 grep ≥ 2 매치 (case 라인 + 배열 원소 라인)' + $LF + '    - **D-09 정밀도 검증 (0.001mm = F3 format):** MeasurementResultRow.cs 의 MeasuredValue 표시 포맷이 `F3` 사용 — `findstr /c:"ToString(""F3"")" WPF_Example\UI\ViewModel\MeasurementResultRow.cs` ≥ 2 매치 (L54 ResultDisplay + L60 MeasuredValueText 확인). **기존 코드에 이미 F3 적용 완료 (Phase 6 Plan 04, 260417)** — Phase 23 신규 코드 변경 없이 D-09 충족. 매치 < 2 시 carry-over 등록 (별도 quick task).'
if (-not $c2.Contains($old2a)) { Write-Host "Plan 02 acceptance anchor not found"; exit 1 }
$c2 = $c2.Replace($old2a, $new2a)

[System.IO.File]::WriteAllText($f2, $c2, $utf8)
Write-Host "Plan 23-02 OK"

# ============================================================
# Plan 23-03: Test 1 — annotate "12.345 mm" expected with format source
# Actually Test 1 expected uses "예: `12.345 mm`" — add format source note
# ============================================================
$f3 = 'C:\Info\Project\DataMeasurement\.planning\phases\23-top-1-a-simul-end-to-end-2026-05-11\23-03-PLAN.md'
$c3 = [System.IO.File]::ReadAllText($f3, $utf8)

$old3a = '      - 정밀도 3자릿 (예: `12.345 mm`)'
$new3a = '      - 정밀도 3자릿 (예: `12.345 mm`) **(format = F3 확인 완료 — MeasurementResultRow.cs L54/L60 `ToString("F3")` 기존 적용, 23-02 Task 1 acceptance grep)**'
if (-not $c3.Contains($old3a)) { Write-Host "Plan 03 Test 1 anchor not found"; exit 1 }
$c3 = $c3.Replace($old3a, $new3a)

[System.IO.File]::WriteAllText($f3, $c3, $utf8)
Write-Host "Plan 23-03 OK"
