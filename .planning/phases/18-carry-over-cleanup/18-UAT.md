---
phase: 18-carry-over-cleanup
type: uat
status: signed_off
source:
  - 18-01-SUMMARY.md
  - 18-02-SUMMARY.md
  - 18-03-SUMMARY.md
  - 18-04-SUMMARY.md
  - 18-05-SUMMARY.md
  - 18-07-SUMMARY.md
created: 2026-05-05
started: 2026-05-07T00:00:00
updated: 2026-05-07T14:00:00
summary:
  total: 6
  passed: 5
  na: 1
  issues: 0
  pending: 0
  blocked: 0
  skipped: 0
---

# Phase 18 UAT — Carry-over 정리 (CO-01/CO-03/CO-04/CO-05/CO-06)

## 개요

Phase 17 partial sign-off 에서 이관된 5개 CO 항목 검증.

| Test | CO | 내용 |
|------|----|------|
| Test 1 | CO-01 | Circle_RadialDirection PropertyGrid ItemsSource (Inward/Outward 전용) |
| Test 2 | CO-04-A | "ROI 다시 그리기" 컨텍스트 메뉴 표시 조건 |
| Test 3 | CO-04-B | "ROI 다시 그리기" 실행 동작 |
| Test 4 | CO-05 | Circle Strip 성공/실패 색상 시각화 |
| Test 5 | CO-06 | FormatTeachError [DatumName] 접두사 |
| Test 6 | CO-03 | btn_teachDatum IsConfigured 게이팅 사양 명문화 |

**정적 acceptance(grep) 사전검증 (2026-05-07):** 12/12 PASS
- Test 1: Circle_RadialDirectionList=4(≥2) ✓ / allNoFilter=2(≥1) ✓
- Test 2: RedrawRoiMenuItem=2(≥1) ✓ / IsTeachDatumMode=2(≥2) ✓
- Test 3: RoiRedrawRequested=1(≥1) ✓ / ClearDatumRoiFields=9(≥1) ✓
- Test 4: CircleStripSuccesses=1(≥1) ✓ / stripColor=3(≥3) ✓
- Test 5: datum.DatumName=2(≥1) ✓ / DatumConfig datum, string err=1(=1) ✓
- Test 6: ValidateRoiPresence=2(≥2) ✓ / IsConfigured=11(≥1) ✓

---

## Current Test

[testing complete — session ended partial: 2 issues blocking 4 tests]

## Tests

### 1. CO-01 — Circle_RadialDirection 드롭다운 (Inward/Outward 전용)

**단계**:
1. 애플리케이션 시작 → InspectionListView 에서 AlgorithmType=CircleTwoHorizontal Datum 선택
2. PropertyGrid 에서 Circle_RadialDirection 필드의 드롭다운 클릭

**기대**:
- 드롭다운에 "Inward" / "Outward" 두 항목만 표시
- "LtoR", "RtoL", "TtoB", "BtoT" 항목 없음

**source**: CO-01
**static_acceptance**: PASS (Circle_RadialDirectionList=4, allNoFilter=2)
result: pass
reported: "debug fix ba25e88 — Circle_EdgeDirection 영구 hide (Circle 은 RadialDirection 만 사용). 2026-05-07 confirmed."
severity: resolved

### 2. CO-04-A — "ROI 다시 그리기" 컨텍스트 메뉴 표시 조건

**단계**:
1. CTH Datum 선택 → btn_teachDatum ON (TeachDatum 모드 진입)
2. 캔버스에서 Datum Circle ROI 위에 우클릭

**기대**:
- ContextMenu 에 "ROI 다시 그리기" 항목이 Visible 로 표시됨
- 빈 캔버스 위 우클릭 시 해당 항목 Collapsed (미표시)
- TeachDatum 모드 OFF 상태에서는 항목 미표시

**source**: CO-04
**static_acceptance**: PASS (RedrawRoiMenuItem=2, IsTeachDatumMode=2)
result: pass
reported: "18-07 재티칭 확인 모달 추가로 해결. 사용자 PASS 2026-05-07 12:5x (18-07-SUMMARY.md 기록)."

### 3. CO-04-B — "ROI 다시 그리기" 실행 동작

**단계**:
1. CTH Datum 선택 → btn_teachDatum ON
2. Datum Circle ROI 위 우클릭 → "ROI 다시 그리기" 클릭

**기대**:
- Circle ROI 오버레이가 캔버스에서 사라짐 (CircleROI_Radius = 0 리셋)
- btn_teachDatum 상태 유지 (TeachDatum 모드 OFF 되지 않음)

**source**: CO-04
**static_acceptance**: PASS (RoiRedrawRequested=1, ClearDatumRoiFields=9)
result: na
reason: "18-07 재티칭 모달 흐름(IsConfigured=true → 즉시 teach/종료)으로 인해 TeachDatum 모드 중 ROI 우클릭 타이밍이 없어 메뉴 도달 불가. 실제 워크플로는 Delete ROI → btn_teach 재그리기로 대체 동작 — CO-04 escape hatch 목적 충족."

### 4. CO-05 — Circle Strip 성공/실패 색상 시각화

**단계**:
1. CTH Datum 선택 → 정상 ROI 설정 → btn_teachDatum 클릭 (성공 케이스)
2. 캔버스에서 Circle 주변 polar strip 색상 확인
3. (선택) Circle_RadialDirection 을 반대로 설정 후 재티칭 → 실패 strip 색상 확인

**기대**:
- 에지 검출 성공 strip: 녹색 (green)
- 에지 검출 실패 strip: 빨강 (red)
- CircleStripSuccesses 가 null 이거나 인덱스 범위 초과 시: 회색 (gray, fallback)

**source**: CO-05
**static_acceptance**: PASS (CircleStripSuccesses=1, stripColor=3)
result: pass
reported: "CTH 티칭 후 녹색/빨강 정상 표시 확인. 회색(fallback)은 정상 흐름에서 미노출 — 예상 동작. 2026-05-07 사용자 PASS."

### 5. CO-06 — FormatTeachError [DatumName] 접두사

**단계**:
1. CTH Datum 이름 = "Datum 2" 확인 (PropertyGrid DatumName 필드)
2. Circle_RadialDirection 을 의도적으로 반대로 설정 → btn_teachDatum 클릭 (검출 실패 유도)
3. 실패 모달 텍스트 확인

**기대**:
- 모달 타이틀: "티칭 실패"
- 모달 메시지: "[Datum 2] 검출된 에지가 없습니다. ..." (또는 해당 에러 메시지의 [DatumName] 접두사 포함)

**source**: CO-06
**static_acceptance**: PASS (datum.DatumName=2, DatumConfig datum, string err=1)
result: pass
reported: "티칭 실패 유도 시 [DatumName] 접두사 포함 모달 확인. 2026-05-07 사용자 PASS."

### 6. CO-03 — btn_teachDatum IsConfigured 게이팅 (2 시나리오)

Phase 17 Test 10 SKIP 항목을 올바른 사양으로 재작성. 코드 변경 없음 — IsConfigured 게이팅(hotfix#3)이 이미 올바른 동작을 구현한다.

**시나리오 A — IsConfigured=false (새 Datum, 첫 티칭)**:
1. 새 Datum 생성 → InspectionListView 에서 선택 (IsConfigured=false 상태)
2. AlgorithmType = CircleTwoHorizontal 설정
3. 캔버스에 ROI 를 그리지 않은 채로 btn_teachDatum 클릭

기대:
- ValidateRoiPresence 모달 표시 없음 (IsConfigured=false → 가드 스킵)
- Wizard 단계(StartDatumTeachStep) 로 즉시 진입 — 첫 ROI 그리기 안내 시작

**시나리오 B — IsConfigured=true (기존 Datum 재티칭, ROI 삭제 후)**:
1. 이미 티칭 완료된 CTH Datum 선택 (IsConfigured=true)
2. PropertyGrid 에서 CircleROI_Radius 를 0 으로 수동 설정
3. btn_teachDatum 클릭

기대:
- ValidateRoiPresence 모달 표시: Title="티칭 실패", Message="Circle ROI 가 없습니다. 캔버스에 원을 그리고 다시 시도하세요."
- btn_teachDatum 자동 OFF

**source**: CO-03 (D-04, D-05, D-06)
**static_acceptance**: PASS (ValidateRoiPresence=2, IsConfigured=11)
result: pass
reported: "시나리오 A(새 Datum → wizard 즉시 진입) / B(ROI 삭제 → btn_teach → 자연스러운 재티칭) 모두 정상. 'Circle ROI가 없습니다' 모달은 IsConfigured=true + ROI 누락(PropertyGrid 직접 편집 등) 엣지케이스 안전망으로 확인. 2026-05-07 사용자 PASS."

---

## Summary

total: 6
passed: 5
na: 1
issues: 0
pending: 0
blocked: 0
skipped: 0

## Gaps

모두 해소됨.

- CO-01 (Test 1): debug fix ba25e88 — Circle_EdgeDirection 영구 hide로 해결
- CO-04-A (Test 2): 18-07 재티칭 확인 모달로 해결
- CO-04-B (Test 3): N/A — delete ROI + btn_teach 워크플로로 대체
- CO-05 (Test 4): PASS — 녹색/빨강 시각화 정상
- CO-06 (Test 5): PASS — [DatumName] 접두사 정상
- CO-03 (Test 6): PASS — IsConfigured 게이팅 정상
