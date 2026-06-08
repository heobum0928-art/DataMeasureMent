$p = 'C:\Info\Project\DataMeasurement\.planning\phases\39.3-dualimage-fai-ux-redesign-2026-05-30\39.3-04-PLAN.md'
$b = [System.IO.File]::ReadAllBytes($p)
$utf8 = [System.Text.UTF8Encoding]::new($false)
$s = $utf8.GetString($b)

# Korean anchors via UTF-8 bytes
$ko_verifyA = [System.Text.Encoding]::UTF8.GetString([byte[]](0xEC,0x8B,0x9C,0xED,0x80,0x80,0xEC,0x8A,0xA4))  # 시퀀스
# Use a stable ASCII-rich anchor: "Verify C:" line + following Pass line
$anchor = "    - Verify C: Bottom E5 DualImage"
if (-not $s.Contains($anchor)) { Write-Host 'ANCHOR_NOT_FOUND'; exit 2 }

# Build replacement: original "- Pass: 3 항목 ALL OK" line becomes "- Pass: 3 항목 ALL OK" + new Verify D + Verify E (then UAT.md 결과 section header next)
$ko_passLine = "    - Pass: 3 " + [System.Text.Encoding]::UTF8.GetString([byte[]](0xED,0x95,0xAD,0xEB,0xAA,0xA9)) + " ALL OK"
if (-not $s.Contains($ko_passLine)) { Write-Host 'PASS_LINE_NOT_FOUND'; exit 3 }

# Insert new verify blocks after the Pass line (before next blank+heading "**7) UAT.md ...")
$nl = "`r`n"
$add = $nl + $nl + "    **회귀 D — Phase 39.1 4항목 smoke test** (CONTEXT.md Anti-Goal #7 가드):" + $nl + "    - (1) CircleDiameter 검사 — Bottom 시퀀스의 CircleDiameter Measurement 노드 1개 클릭 → PropertyGrid 에 파라미터 (Radius/Center) 정상 표시 확인 (1 클릭)" + $nl + "    - (2) EdgeToLineDistance projection_pl — 기존 EdgeToLineDistance Measurement 1개 클릭 → SIMUL 검사 1회 실행 → 측정값 표시 (1 클릭 + 1 실행)" + $nl + "    - (3) FAI 개별 검사 토글 — FAI 노드 우클릭 메뉴 또는 토글 버튼 1회 클릭 → 개별 검사 동작 확인 (1 클릭)" + $nl + "    - (4) Datum CircleTwoHorizontal Edit 모드 — Side 시퀀스의 CircleTwoHorizontal Datum 1개 선택 → Edit 모드 진입 1회 → ROI 표시 정상 (1 클릭)" + $nl + "    - Pass: 4 항목 ALL OK (각 1 클릭/표시 확인 정도, 최소 부담)" + $nl + $nl + "    **회귀 E — Phase 39.2 D-G2~G4 smoke test** (CONTEXT.md Anti-Goal #7 가드):" + $nl + "    - (1) I10 close-point variant — Bottom 시퀀스의 I10 close-point variant Measurement 1개로 SIMUL 검사 1 시퀀스 실행 → 측정값 정상 (1 실행)" + $nl + "    - (2) Tree Move ▲▼ 버튼 — InspectionListView 트리에서 임의 노드 선택 → Move ▲ 또는 Move ▼ 버튼 1회 클릭 → 노드 순서 변경 동작 확인 (1 클릭)" + $nl + "    - (3) Tree 18 Geometry 아이콘 — InspectionListView 트리 18 Geometry 아이콘 시각 확인 (각 Measurement 타입별 아이콘이 정상 표시되는지 1 회 시각 확인)" + $nl + "    - Pass: 3 항목 ALL OK (smoke 수준, 최소 부담)"

$s2 = $s.Replace($ko_passLine, $ko_passLine + $add)
[System.IO.File]::WriteAllBytes($p, $utf8.GetBytes($s2))
Write-Host 'OK'
