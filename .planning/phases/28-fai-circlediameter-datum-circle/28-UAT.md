---
phase: 28-fai-circlediameter-datum-circle
type: uat
status: pending
created: 2026-05-08
total: 4
passed: 1
failed: 0
pending: 3
---

# Phase 28 — UAT: FAI CircleDiameter + Datum Circle 알고리즘 통합

검증 대상: SPEC AC-1 / AC-4 / AC-5 / AC-6
환경: SIMUL_MODE (D:\1.bmp), Debug/x64 build
실행자: 사용자 (수동 UAT — Tests 1/2/3) + Plan 04 executor (자동 — Test 4)

관련 SUMMARYs:
- 28-01-SUMMARY.md (`be4d267` — EdgeOptionLists helper + 4 polar default consts)
- 28-02-SUMMARY.md (`578cab6`, `432adb2` — CircleDiameterMeasurement field + branch)
- 28-03-SUMMARY.md (`84affbb`, `a894c36` — DatumFindingService inline → helper cleanup)

---

## Test 1 — AC-1: Circle_RadialDirection 콤보 PropertyGrid 시각 노출

**목적**: REQ-28-01 수용 — `CircleDiameterMeasurement.Circle_RadialDirection` 이 PropertyGrid 에 콤보 ▼ 으로 노출되고, 옵션이 정확히 Inward / Outward 2개인지 확인.

**Steps**:
1. `bin\x64\Debug\DatumMeasurement.exe` 실행 (Debug/x64 빌드 후)
2. 검사 트리에서 임의 Shot 선택 → FAI 추가 (CircleDiameter 타입 선택)
3. 새로 추가된 CircleDiameter FAI 노드를 트리에서 선택 → 우측 PropertyGrid 확인
4. `Edge` 카테고리 펼침 — Circle_RadialDirection 행 존재 + 우측에 콤보 ▼ 표시 확인
5. 콤보 클릭 → 드롭다운 옵션 정확히 `Inward` / `Outward` 2개인지 확인 (3번째 옵션 또는 빈 항목 없음)

**Expected**:
- Circle_RadialDirection 행이 `Edge` 카테고리에 표시됨
- 콤보 ▼ 펼침 시 `Inward`, `Outward` 만 표시 (Phase 17 D-02 단일 소스)
- 기본값은 빈 문자열 (콤보 표시 영역은 비어있음)

**Result**: pending — 사용자 확인 후 PASS/FAIL/INVALID + 일자 기재

---

## Test 2 — AC-4: Datum CTH ↔ FAI CircleDiameter 검출 직경 동등성

**목적**: REQ-28-03 수용 — 동일 ROI / 동일 이미지 / 동일 파라미터에서 Datum CTH (`CircleTwoHorizontal`) 와 FAI `CircleDiameter` 의 검출 직경 차이가 0.001 mm 이하인지 확인.

**Setup**:
- D:\1.bmp 이미지 로드
- Datum 알고리즘 = `CircleTwoHorizontal` 로 설정
- Datum Circle ROI 임의 위치/반지름 설정 (예: Row=500, Col=500, Radius=100 — D:\1.bmp 에 적합한 임의 원 영역)
- DatumConfig 파라미터:
  - Circle_Sigma = 1.0
  - Circle_EdgeThreshold = 10
  - Circle_RadialDirection = Inward
  - Circle_PolarStepDeg = 10.0
  - Circle_RectL1Ratio = 0.02
  - Circle_RectL2Ratio = 0.02
  - Circle_EdgeSelection = First
- 동일 Shot 에 CircleDiameter FAI 추가, ROI 동일:
  - Circle_Row = 500, Circle_Col = 500, Circle_Radius = 100
  - Sigma = 1.0, EdgeThreshold = 10
  - Circle_RadialDirection = Inward (Phase 28 신규 콤보)
- pixelResolution = 동일 캘리브레이션 값

**Steps**:
1. Datum 티칭 → 검출된 Circle radius (px) 또는 직경 (mm) 기록 → D_datum (`foundRadius * 2 * pixelResolution`)
2. FAI CircleDiameter 검사 실행 → resultValue (mm) 기록 → D_fai
3. |D_fai - D_datum| 계산

**Expected**:
- |D_fai - D_datum| ≤ 0.001 mm
- D-04 default 일치 + D-02 helper 단일 소스 → 결정적 동등성 (28-02-SUMMARY §"REQ-28-03 Equivalence Argument" 참조)

**Result**: pending — 사용자 확인 후 PASS/FAIL/INVALID + 측정값 (D_datum, D_fai, |Δ|) 기재

---

## Test 3 — AC-5: v1.0 시점 INI 레시피 (RadialDirection 키 없음) 로드 회귀 0

**목적**: REQ-28-04 수용 — Phase 28 변경 전 INI 레시피 (`Circle_RadialDirection` 키 없음) 로드 시 CircleDiameter 측정 결과가 v1.0 결과와 동일한지 확인.

**Setup**:
- Phase 28 변경 이전에 저장된 INI 레시피 1개 준비. 없으면 현재 INI 에서 `Circle_RadialDirection=` 라인을 텍스트 에디터로 삭제하여 재현
- D:\1.bmp 이미지

**Steps**:
1. 위 INI 레시피 로드
2. CircleDiameter 측정 1회 실행 → resultValue 기록 (D_after)
3. (옵션 A) git stash 또는 별도 브랜치에서 v1.0 코드로 동일 INI / 이미지 로드 → resultValue 기록 (D_before) → D_after == D_before 확인
4. (옵션 B 간이) PropertyGrid 에서 `Circle_RadialDirection` 표시값이 빈 값 인지 확인 → `string.IsNullOrEmpty(Circle_RadialDirection)` true → fit 경로(`TryFindCircle`) 진입 코드 inspection 으로 확인 (CircleDiameterMeasurement.cs `if (string.IsNullOrEmpty(...))` 분기)

**Expected**:
- Circle_RadialDirection 콤보 빈 값 표시 (default = "")
- TryExecute 가 fit 경로 (`TryFindCircle`) 호출 — 28-02-SUMMARY 가 fit-path 인자열 byte-identical 보장
- D_after == D_before (회귀 0) 또는 fit 경로 진입 확인 시 v1.0 동등 동작 보장

**Result**: pending — 사용자 확인 후 PASS/FAIL/INVALID + resultValue (D_after, 가능 시 D_before) 기재

---

## Test 4 — AC-6: msbuild Debug/x64 PASS + 0 new errors + 0 new warnings

**목적**: REQ-28-06 수용 — Phase 28 변경 후 빌드 무결성 + 신규 경고/오류 0.

**Steps** (Plan 04 executor 자동 수행 — 2026-05-08):
1. PowerShell 에서 다음 실행:
   ```
   cd C:\Info\Project\DataMeasurement
   msbuild WPF_Example/DatumMeasurement.csproj /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /m
   ```
2. 출력에서 `error CS\d+` count = 0 확인
3. 출력에서 `warning CS\d+` 새 경고 추가 없음 확인 (pre-existing 잔존 OK)
4. exit code = 0 확인

**Expected**:
- exit code 0
- new error CS\d+ count = 0
- new warning CS\d+ count = 0 (pre-existing 2 distinct 잔존 OK: VirtualCamera.cs:266 CS0162 + VisionAlgorithmService.cs:64 CS0219)
- DatumMeasurement.exe 생성됨 (`bin\x64\Debug\DatumMeasurement.exe` mtime 갱신)

**Result**: **PASS** — 2026-05-08 Plan 04 executor 자동 수행

**Measured (verbosity:normal /t:Rebuild)**:
- exit code = 0
- `error CS\d+` count = 0
- `error MSB\d+` count = 0
- `warning CS\d+` raw count = 8 → **distinct = 2** (각 4회 — 메인 csproj 2회 + WPF 임시 csproj 2회):
  - `VirtualCamera.cs(266,13): warning CS0162` (도달할 수 없는 코드) — pre-existing baseline
  - `VisionAlgorithmService.cs(64,22): warning CS0219` (`scanHorizontal` 미사용) — pre-existing baseline
- `warning MSB\d+` raw count = 4 → distinct = 1 (`MSB3884 MinimumRecommendedRules.ruleset not found`) — pre-existing baseline (Phase 28 무관)
- **NEW CS errors = 0**, **NEW CS warnings = 0** (28-01/02/03 SUMMARY 의 baseline 과 동일)
- DatumMeasurement.exe FOUND, mtime = 2026-05-08T19:41:38

---

## Summary

| Test | AC | Description | Result |
|------|------|-------------|--------|
| 1 | AC-1 | PropertyGrid Circle_RadialDirection 콤보 (Inward/Outward) | pending |
| 2 | AC-4 | Datum CTH ↔ FAI 검출 직경 동등성 (≤ 0.001 mm) | pending |
| 3 | AC-5 | v1.0 INI 회귀 0 | pending |
| 4 | AC-6 | msbuild Debug/x64 PASS + 0 new errors/warnings | **PASS** (2026-05-08) |

**Total**: 4 / **Passed**: 1 / **Failed**: 0 / **Pending**: 3

Sign-off 조건: 4/4 PASS 시 frontmatter `status=signed_off` + 일자 기재.
