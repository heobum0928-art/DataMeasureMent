---
phase: 23
plan: 02
subsystem: inspection
tags: [factory, dispatch, teaching-image, simul-mode, wiring, datum-relative]
requirements_completed: [ALG-01 wiring complete — pending UAT (Plan 23-03)]
provides:
  - "MeasurementFactory.Create('EdgeToLineDistance', owner) dispatch (7번째 case)"
  - "MeasurementFactory.GetTypeNames() 7-element array — FAIConfig.EdgeMeasureType PropertyGrid 드롭다운 + INI Type dispatch 자동 노출 (D-13)"
  - "Action_FAIMeasurement.GrabOrLoadDatumImage 3-tier fallback chain: TeachingImagePath → SimulImagePath → GrabHalconImage (D-04 Phase 22 carry-over 해소)"
affects:
  - "WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs (MOD)"
  - "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs (MOD)"
dependency_graph:
  requires:
    - "Plan 23-01: EdgeToLineDistanceMeasurement class + TryFitLine signature extension"
    - "Phase 22-01: DatumConfig.TeachingImagePath public string 속성 + EnsurePerRoiDefaults null 가드"
  provides:
    - "MeasurementFactory dispatch 등록 완료 — Plan 23-03 UAT 의 Simul end-to-end 진행 가능"
    - "TeachingImagePath 실 소비 (Phase 22 carry-over #2 해소)"
  affects:
    - "Plan 23-03 (Wave 3): UAT SC#1~SC#5 + Datum CTH INI seed + A1~A5 ROI 사용자 셋업"
tech_stack:
  added: []
  patterns:
    - "Factory dispatch + GetTypeNames single-source (Phase 19 Plan 02 패턴 — FAIConfig.EdgeMeasureType ItemsSource 캐시 자동 갱신)"
    - "Phase 20 D-12 marker stacking — 기존 //260511 hbk Phase 22 IMG-02 보존 + 위에 //260512 hbk Phase 23 ALG-01 누적"
    - "3-tier fallback chain — TeachingImagePath (우선) → SimulImagePath (폴백 회귀 0) → GrabHalconImage (최종)"
    - "Pitfall 3 2-step guard — !string.IsNullOrEmpty(path) && File.Exists(path)"
    - "K&R brace style 유지 (Action_FAIMeasurement.cs 파일 스타일 일치)"
key_files:
  created: []
  modified:
    - "WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs"
    - "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
decisions:
  - "MeasurementFactory.cs switch case 위치 = LineToLineDistance ↔ default 사이 (insertion order 보존, 6 기존 case 무수정)"
  - "GetTypeNames 배열 7번째 원소 = 'EdgeToLineDistance' 마지막 추가 — FAIConfig L60 ItemsSource 캐시가 새 인덱스 자동 노출 (코드 변경 0)"
  - "GrabOrLoadDatumImage parentSeq.DatumConfigs[0].TeachingImagePath 채택 (Plan 23-02 plan + RESEARCH A6 — 첫 번째 Datum 만 사용; 다중 Datum 확장은 Plan 23-03 후속 결정)"
  - "Phase 22 IMG-02 주석 (L226) 보존 + 위에 Phase 23 ALG-01 주석 stacking — Phase 20 D-12 패턴 준수 (기존 마커 보존, 신규 마커 누적)"
  - "Pitfall 3 2-step 가드 채택 — EnsurePerRoiDefaults 가 null 가드만 처리하므로 빈 문자열 케이스도 IsNullOrEmpty 로 검사"
  - "hbk 마커 날짜 = 260512 (Plan 23-01 SUMMARY 와 일관 — 작업 당일 2026-05-12)"
metrics:
  duration_minutes: 2
  completed_date: "2026-05-12"
  tasks_count: 3
  files_created: 0
  files_modified: 2
  commits_count: 2
---

# Phase 23 Plan 02: Top #1 A시리즈 Measurement Wiring Summary

**One-liner:** EdgeToLineDistance Factory dispatch (case + GetTypeNames 7번째) + Action_FAIMeasurement.GrabOrLoadDatumImage TeachingImagePath 우선순위 분기 — ALG-01 의 wiring wave 완료, Phase 22 IMG-02 carry-over 해소.

## Accomplishments

- **2 files modified** (0 new):
  - `MeasurementFactory.cs` — switch 7번째 case + GetTypeNames 7번째 원소 (4 insertions, 1 deletion, 3 markers)
  - `Action_FAIMeasurement.cs` — GrabOrLoadDatumImage 진입부에 DatumConfigs[0].TeachingImagePath 우선순위 분기 (13 insertions, 1 deletion, 5 markers)
- **msbuild Debug/x64 Rebuild PASS** — 0 errors, 6 warnings (Phase 21 baseline preserved: MSB3884×2 + CS0162×2 + CS0219×2), 신규 warning 0
- **8 `//260512 hbk Phase 23 ALG-01` markers** in this plan (MeasurementFactory: 3, Action_FAIMeasurement: 5) — 누적 Phase 23 markers: 24 across 3 files
- **DatumMeasurement.exe mtime 갱신** — Plan 23-01 + 23-02 누적 변경 반영
- **Phase 22 IMG-02 marker (L226) 보존** — Phase 20 D-12 stacking 패턴 준수 (`grep //260511 hbk Phase 22 IMG-02 → 1 match`)
- **회귀 0 검증 경로:** TeachingImagePath="" 인 경우 (Phase 22 EnsurePerRoiDefaults default) → 첫 분기 skip → SimulImagePath 폴백 진입 → Phase 22 baseline byte-identical

## Task Commits

| Task | Description | Commit | Files |
|------|-------------|--------|-------|
| 1 | Register EdgeToLineDistance in MeasurementFactory (switch case + GetTypeNames) | `250d8ad` | MeasurementFactory.cs |
| 2 | Wire TeachingImagePath fallback in GrabOrLoadDatumImage (D-04) | `a476300` | Action_FAIMeasurement.cs |
| 3 | msbuild Debug/x64 Rebuild verification (PASS, baseline 6 warnings preserved) | (verification only — no commit, build_23_w2.log gitignored) | — |

## Acceptance Criteria Verification

| AC | Criterion | Status | Evidence |
|----|-----------|--------|----------|
| 1 | MeasurementFactory.Create has `case "EdgeToLineDistance"` returning `new EdgeToLineDistanceMeasurement(owner)` | PASS | MeasurementFactory.cs:28-29 |
| 2 | MeasurementFactory.GetTypeNames array includes `"EdgeToLineDistance"` (count = 7) | PASS | MeasurementFactory.cs:45 (line 7번째 원소) |
| 3 | 기존 6 case 모두 unchanged | PASS | EdgePairDistance/PointToLineDistance/PointToPointDistance/LineToLineAngle/CircleDiameter/LineToLineDistance 무수정 |
| 4 | `default: return null;` 보존 | PASS | MeasurementFactory.cs:30-31 |
| 5 | Action_FAIMeasurement.GrabOrLoadDatumImage uses `DatumConfigs[0].TeachingImagePath` priority | PASS | Action_FAIMeasurement.cs:230 |
| 6 | `teachingPath` 로컬 변수 ≥ 3 사용 | PASS | L228 declaration, L229 condition, L230 assign, L233 IsNullOrEmpty+Exists, L235 new HImage = 5 매치 |
| 7 | `new HImage(teachingPath)` try/catch 보호 1회 | PASS | L235 (try/catch L234-238) |
| 8 | `new HImage(ShotParam.SimulImagePath)` 존재 (회귀 0 baseline) | PASS | L242 |
| 9 | 2-step 가드 `!string.IsNullOrEmpty(teachingPath) && File.Exists(teachingPath)` | PASS | L233 |
| 10 | 폴백 가드 `image == null && !string.IsNullOrEmpty(ShotParam.SimulImagePath)` | PASS | L240 |
| 11 | 마지막 `image == null` GrabHalconImage 폴백 보존 | PASS | L247-249 |
| 12 | Phase 22 IMG-02 마커 보존 (Phase 20 D-12 stacking) | PASS | L227 1회 매치 |
| 13 | `//260512 hbk Phase 23 ALG-01` 마커 ≥ 4 (GrabOrLoadDatumImage) | PASS | L226, L228, L229, L233, L240 = 5 매치 |
| 14 | K&R brace `private HImage GrabOrLoadDatumImage(InspectionSequence parentSeq) {` | PASS | L223 |
| 15 | msbuild Debug/x64 PASS, 0 errors, 6 warnings (baseline preserved) | PASS | build_23_w2.log: EXIT=0, errors=0, warnings=6 (MSB3884×2 + CS0162×2 + CS0219×2) |
| 16 | DatumMeasurement.exe mtime 갱신 | PASS | `WPF_Example/bin/x64/Debug/DatumMeasurement.exe` May 12 00:10 |
| 17 | `//260512 hbk Phase 23 ALG-01` 마커 누적 ≥ 6 (Factory 2 + Action 4+ — plan success_criteria) | PASS | Factory 3 + Action_FAIMeasurement 5 = 8 매치 (Plan 23-01 16 markers 누적 시 24 across 3 files) |

## Decisions Made

1. **MeasurementFactory switch case 위치:** `LineToLineDistance` 와 `default` 사이 마지막 case 로 추가. insertion order 보존 + 6 기존 case 무수정 → 회귀 0.
2. **GetTypeNames 7번째 원소 = 'EdgeToLineDistance' 마지막 추가:** FAIConfig L59-60 ItemsSource 캐시 (`new List<string>(MeasurementFactory.GetTypeNames())`) 단일 소스이므로 FAIConfig.cs 무수정 자동 갱신 (RESEARCH Finding #4 의 핵심 가치 — D-13 INI + UI 둘 다 채널 자동 충족).
3. **DatumConfigs[0] 첫 번째 Datum 만 채택:** RESEARCH A6 (Phase 23 Top #1 단일 Datum 가정, D-01 CTH 1개 lock-in). 다중 Datum 확장은 Plan 23-03 UAT 결과에 따라 결정.
4. **Phase 22 IMG-02 주석 (L226) 보존:** Phase 20 D-12 stacking 패턴 — 기존 `//260511 hbk Phase 22 IMG-02` 라인 그대로 유지 + 위에 `//260512 hbk Phase 23 ALG-01` 신규 마커 누적. grep 검증: 두 마커 모두 본 메서드 내 존재.
5. **Pitfall 3 2-step 가드:** `!string.IsNullOrEmpty(teachingPath) && File.Exists(teachingPath)`. EnsurePerRoiDefaults (Plan 22-01 L519) 가 `== null` 만 가드하므로 빈 문자열도 IsNullOrEmpty 로 명시 검사 — 회귀 0 baseline 보장.
6. **hbk 마커 날짜 = 260512:** 작업 당일 (2026-05-12) 기준. Plan 23-01 SUMMARY (`260512 hbk`) 와 일관. Plan 본문은 `260511` 로 작성되었으나 orchestrator commit_protocol 가 `260512` 명시.
7. **K&R brace 유지:** Action_FAIMeasurement.cs 파일 스타일 K&R (opening brace on same line) — `private HImage GrabOrLoadDatumImage(InspectionSequence parentSeq) {` 그대로 유지, 신규 분기 라인도 K&R style.

## Deviations from Plan

**[Rule N/A - 날짜 정정]** Plan 본문은 marker date 를 `260511 hbk Phase 23 ALG-01` 로 명시했으나 orchestrator commit_protocol section (시스템 프롬프트) 는 `260512 hbk Phase 23 ALG-01` 을 명시 — 작업 당일 (2026-05-12) 기준. Plan 23-01 SUMMARY 도 `260512` 사용. 두 출처 충돌 시 orchestrator + Plan 23-01 consistency 채택 — 모든 신규 마커 = `260512`. grep 검증 모두 PASS (acceptance criteria 의 `260511` literal 은 의도된 marker date 변수로 해석).

기능적 deviations 없음 — 코드 동작은 plan spec 그대로.

## Authentication Gates

None — 코드 wiring 만, 외부 입력/세션/네트워크 변경 0.

## Known Stubs

None — 본 plan 의 신규 코드 (Factory case + GrabOrLoadDatumImage 분기) 모두 실 데이터 경로. EdgeToLineDistanceMeasurement.TryExecute 본체는 Plan 23-01 에서 완전 구현 완료.

## Carry-overs to Plan 23-03 (Wave 3 — UAT)

- **SC#1 — TeachingImagePath INI 라운드트립:** Plan 22-01 UAT Test 1 재확인 + Plan 23-02 신규 분기로 실제 이미지 로드 검증
- **SC#2 — A1~A5 ROI Simul end-to-end:** EdgeToLineDistance 7번째 algorithm 으로 Y 거리 측정 결과 (mm) + 공차 판정 시각 확인
- **SC#3 — Datum CTH INI seed:** Top Fixture #1 의 단일 CTH DatumConfig 시드 (D-01 lock-in)
- **SC#4 — TeachingImagePath="" 회귀 0:** SimulImagePath 분기로 byte-identical 회귀 (Phase 22 baseline 보존)
- **SC#5 — A6 확장성 검증:** 다중 Datum 시나리오 (DatumConfigs[1+]) carry-over 결정 (Plan 23-02 = [0] 만 사용)
- **D-09 정밀도 검증 (0.001mm = F3):** MeasurementResultRow.cs ResultDisplay/MeasuredValueText (Phase 6 Plan 04 260417 적용 완료) 시각 확인

## Build Verification

```
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo -t:Rebuild > build_23_w2.log 2>&1
```

- **EXIT:** 0
- **Errors:** 0
- **Warnings:** 6 (Phase 21 baseline)
  - MSB3884 × 2 (MinimumRecommendedRules.ruleset 누락 — pre-existing csproj/wpftmp 페어)
  - CS0162 × 2 (VirtualCamera.cs:266 unreachable code — pre-existing)
  - CS0219 × 2 (VisionAlgorithmService.cs:65 unused 'scanHorizontal' — pre-existing)
- **신규 warning:** 0
- **CS0117 (DatumConfigs member missing) 등 신규 컴파일 에러:** 부재 — InspectionSequence.cs L49 `public List<DatumConfig> DatumConfigs { get; private set; } = new List<DatumConfig>()` 정상 접근
- **출력:** `WPF_Example/bin/x64/Debug/DatumMeasurement.exe` mtime 갱신 (May 12 00:10)

## Self-Check: PASSED

**Files verified:**
- FOUND: WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs (modified L28-29, L45)
- FOUND: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs (modified L226-246)
- FOUND: WPF_Example/bin/x64/Debug/DatumMeasurement.exe (rebuild output)

**Commits verified:**
- FOUND: 250d8ad (Task 1: MeasurementFactory dispatch)
- FOUND: a476300 (Task 2: TeachingImagePath fallback)

**Marker count verified:**
- MeasurementFactory.cs: 3 markers ✓
- Action_FAIMeasurement.cs (this plan): 5 markers (≥4 required) ✓
- Plan 23-02 total: 8 markers (plan success_criteria ≥6) ✓
- Phase 23 cumulative (Plan 23-01 + 23-02): 24 markers across 3 files ✓

**Baseline preservation verified:**
- `new HImage(ShotParam.SimulImagePath)` still present (L242, Phase 22 baseline preserved) ✓
- `//260511 hbk Phase 22 IMG-02` marker preserved (L227, Phase 20 D-12 stacking pattern) ✓
- `private HImage GrabOrLoadDatumImage(InspectionSequence parentSeq) {` K&R brace preserved (L223) ✓
- 6 기존 MeasurementFactory cases byte-identical ✓
