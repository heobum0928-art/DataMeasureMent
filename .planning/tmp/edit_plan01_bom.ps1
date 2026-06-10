[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$utf8 = New-Object System.Text.UTF8Encoding($false)

$f = 'C:\Info\Project\DataMeasurement\.planning\phases\23-top-1-a-simul-end-to-end-2026-05-11\23-01-PLAN.md'
$c = [System.IO.File]::ReadAllText($f, $utf8)
$LF = [char]10

# ============================================================
# Task 1 acceptance — split the `, "all",` grep into 2 method-scoped greps
# ============================================================
$old_acc1 = '    - VisionAlgorithmService.cs §TryFitLine 의 MeasurePos 호출 인자 4번째 위치에 `measureSel` (이전 하드코딩 `"all"` 제거 — grep 으로 본 메서드 영역 내 `, "all",` 부재 확인)'
$new_acc1 = '    - VisionAlgorithmService.cs §TryFitLine 의 MeasurePos 호출 인자 4번째 위치에 `measureSel` 사용 (이전 하드코딩 `"all"` 제거). 메서드 영역 한정 2-grep 검증:' + $LF + '      - `findstr /c:''pol, "all",'' WPF_Example\Halcon\Algorithms\VisionAlgorithmService.cs` → 0 matches (TryFitLine 본문 내 하드코딩 제거 확인)' + $LF + '      - `findstr /c:''pol, measureSel,'' WPF_Example\Halcon\Algorithms\VisionAlgorithmService.cs` → 1 match (신규 measureSel 호출 확인)'
if (-not $c.Contains($old_acc1)) { Write-Host "Task 1 acceptance anchor not found"; exit 1 }
$c = $c.Replace($old_acc1, $new_acc1)

# ============================================================
# Task 2 — D-11 literal guard in TryExecute body
# Insert guard right after the resultValue init block ("overlays = new List<EdgeInspectionOverlay>();" comment line)
# Replace the existing TryExecute snippet so the guard appears at entry, before svc.TryFitLine
# ============================================================
$old_t2body = '    {' + $LF + '        resultValue = 0;' + $LF + '        error = null;' + $LF + '        overlays = new List<EdgeInspectionOverlay>(); //260511 hbk Phase 23 ALG-01 — PointToLineDistance 패턴 (overlay 빈 리스트, Phase 7-01 D-03)' + $LF + '' + $LF + '        var svc = new VisionAlgorithmService();'
$new_t2body = '    {' + $LF + '        resultValue = 0;' + $LF + '        error = null;' + $LF + '        overlays = new List<EdgeInspectionOverlay>(); //260511 hbk Phase 23 ALG-01 — PointToLineDistance 패턴 (overlay 빈 리스트, Phase 7-01 D-03)' + $LF + '' + $LF + '        //260511 hbk Phase 23 ALG-01 — D-11 Datum 찾기 실패 가드 (literal 구현, upstream gating 은 보조 이중 안전망)' + $LF + '        if (datumTransform == null || datumTransform.Length == 0)' + $LF + '        {' + $LF + '            error = "Datum not found";' + $LF + '            return false;' + $LF + '        }' + $LF + '' + $LF + '        var svc = new VisionAlgorithmService();'
if (-not $c.Contains($old_t2body)) { Write-Host "Task 2 TryExecute body anchor not found"; exit 1 }
$c = $c.Replace($old_t2body, $new_t2body)

# ============================================================
# Task 2 — Rewrite "D-11 처리" 단락 (action 주의 사항 안의 D-11 단락)
# ============================================================
$old_d11 = '    - D-11 (Datum 찾기 실패) — `EdgeToLineDistance.TryExecute` 가 직접 처리하지 않음. 호출측 (Action_FAIMeasurement.EStep.DatumPhase) 이 FinishAction(Error) 으로 Measure 단계 도달 차단 (RESEARCH Code Examples 주석 참조). 본 task 는 별도 error 메시지 분기 추가 안 함.'
$new_d11 = '    - D-11 (Datum 찾기 실패) — **literal 구현** = `TryExecute` 진입부에 `datumTransform == null || datumTransform.Length == 0` 가드 → `error = "Datum not found"; return false;` (위 TryExecute snippet 의 가드 블록 참조). upstream gating (Action_FAIMeasurement.EStep.DatumPhase 의 FinishAction(Error)) 은 **보조 이중 안전망** — 정상 흐름에서는 Measure 단계 도달 안 하지만, datumTransform 이 null/empty 로 전달되는 경계 케이스 (DatumPhase 통과 후 transform 누락) 도 본 가드로 차단.'
if (-not $c.Contains($old_d11)) { Write-Host "Task 2 D-11 paragraph anchor not found"; exit 1 }
$c = $c.Replace($old_d11, $new_d11)

# ============================================================
# Task 2 — Update <action> first sentence to mention D-11 explicit
# Insert "D-11" reference: change "D-06, D-02, D-10, D-11" already present — no-op.
# Add acceptance_criteria grep for D-11 literal
# ============================================================
$old_acc2_anchor = '    - 파일 내용에 `try { ... } catch { }` AffineTransPoint2d 보호'
$new_acc2_anchor = '    - 파일 내용에 `try { ... } catch { }` AffineTransPoint2d 보호' + $LF + '    - 파일 내용에 D-11 literal guard: `datumTransform == null || datumTransform.Length == 0` 분기 1회 매치 (grep `datumTransform == null`)' + $LF + '    - 파일 내용에 `error = "Datum not found"` 문자열 정확히 1회 매치 (D-11 literal 메시지)'
if (-not $c.Contains($old_acc2_anchor)) { Write-Host "Task 2 acceptance anchor not found"; exit 1 }
$c = $c.Replace($old_acc2_anchor, $new_acc2_anchor)

# ============================================================
# Task 2 — Update <verify> automated to include the D-11 grep
# ============================================================
$old_verify = '<automated>findstr /n /c:"class EdgeToLineDistanceMeasurement : MeasurementBase" WPF_Example\Custom\Sequence\Inspection\Measurements\EdgeToLineDistanceMeasurement.cs &amp;&amp; findstr /n /c:"EdgeSelection" WPF_Example\Custom\Sequence\Inspection\Measurements\EdgeToLineDistanceMeasurement.cs &amp;&amp; findstr /n /c:"AffineTransPoint2d" WPF_Example\Custom\Sequence\Inspection\Measurements\EdgeToLineDistanceMeasurement.cs &amp;&amp; findstr /n /c:"-datumRow * pixelResolution" WPF_Example\Custom\Sequence\Inspection\Measurements\EdgeToLineDistanceMeasurement.cs &amp;&amp; findstr /n /c:"EdgeToLineDistanceMeasurement.cs" WPF_Example\DatumMeasurement.csproj</automated>'
$new_verify = '<automated>findstr /n /c:"class EdgeToLineDistanceMeasurement : MeasurementBase" WPF_Example\Custom\Sequence\Inspection\Measurements\EdgeToLineDistanceMeasurement.cs &amp;&amp; findstr /n /c:"EdgeSelection" WPF_Example\Custom\Sequence\Inspection\Measurements\EdgeToLineDistanceMeasurement.cs &amp;&amp; findstr /n /c:"AffineTransPoint2d" WPF_Example\Custom\Sequence\Inspection\Measurements\EdgeToLineDistanceMeasurement.cs &amp;&amp; findstr /n /c:"-datumRow * pixelResolution" WPF_Example\Custom\Sequence\Inspection\Measurements\EdgeToLineDistanceMeasurement.cs &amp;&amp; findstr /n /c:"datumTransform == null" WPF_Example\Custom\Sequence\Inspection\Measurements\EdgeToLineDistanceMeasurement.cs &amp;&amp; findstr /n /c:"Datum not found" WPF_Example\Custom\Sequence\Inspection\Measurements\EdgeToLineDistanceMeasurement.cs &amp;&amp; findstr /n /c:"EdgeToLineDistanceMeasurement.cs" WPF_Example\DatumMeasurement.csproj</automated>'
if (-not $c.Contains($old_verify)) { Write-Host "Task 2 verify anchor not found"; exit 1 }
$c = $c.Replace($old_verify, $new_verify)

[System.IO.File]::WriteAllText($f, $c, $utf8)
Write-Host "OK"
