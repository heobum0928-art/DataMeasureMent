---
phase: 06-rapid-city
plan: 03
status: complete
date: 2026-04-13
requirements: [RC-05]
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs
tags: [phase6, multi-datum, multi-algorithm, ini-format]
---

# Plan 06-03 Summary

## Goal
Action_FAIMeasurement의 Datum 실행 흐름을 InspectionSequence Multi-Datum 기반으로 재설계하고, InspectionRecipeManager의 INI 저장/로드를 Phase 6 새 포맷([FORMAT] Version=6, Fixture-Datum-Shot-FAI-Measurement 계층)으로 전면 재작성. 기존 Phase 1~5 INI 포맷 로드 시 안내 메시지 표시 후 거부 (D-09, D-10, D-17, D-22, RC-05).

## Delivered

### Task 1 — Action_FAIMeasurement Multi-Datum + Measurement 루프 재설계
`WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs`
- `EStep` enum에 `DatumPhase` 단계 추가 (Init → MoveZ → **DatumPhase** → Grab → Measure → End)
- `EStep.DatumPhase` 블록: `ShotParam.Parent as InspectionSequence` → `parentSeq.TryRunDatumPhase(datumImage, out error)` 호출. DatumConfigs 비어있으면 즉시 pass-through (D-10). 실패 시 에러 로그 + `FinishAction(Error)` (D-09, T-06-09).
- `EStep.Measure` 블록 전면 재작성:
  - `ShotParam.FAIList` → `fai.Measurements` 중첩 루프
  - 각 Measurement에 대해 `parentSeq2.TryGetDatumTransform(meas.DatumRef, out transform)` 조회 (null/미등록 시 HomMat2dIdentity fallback)
  - `meas.TryExecute(image, transform, fai.PixelResolutionX, ...)` 호출 → 성공 시 `meas.EvaluateJudgement()`, 실패 시 로그 + `ClearResult`
  - FAIConfig legacy `IsPass`/`MeasuredValue` 필드에 대표 결과 집계(첫 Measurement 기준) — UI/TCP 호환 유지
- `FAIEdgeMeasurementService` 직접 호출 제거
- `GrabOrLoadDatumImage` 헬퍼 추가 (Dedicated 모드, SIMUL_MODE에서 SimulImagePath 사용)
- 모든 Halcon/예외 호출에 try/catch 적용 — Run() 밖으로 예외 전파 없음 (CLAUDE.md 준수)

### Task 2 — InspectionRecipeManager Phase 6 포맷 저장/로드
`WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs`
- `ERecipeFormatVersion` enum (Unknown/Phase5/Phase6) + `DetectFormatVersion(IniFile)` 추가
- `CurrentFormatVersion = 6` 상수
- `ResolveFixtureSequence()`: 현재 Fixture를 `SystemHandler.Sequences[ESequence.Top] as InspectionSequence`로 해석
- `SavePhase6Format`:
  - `[FORMAT] Version=6`
  - `[FIXTURE] DisplayName, DatumCount` + `[FIXTURE_DATUM_{d}]` (ParamBase.Save)
  - `[SHOTS] Count`, `[SHOT_{s}]` ShotName/ZPosition/DelayMs/SimulImagePath/FAICount, `[SHOT_{s}_CAM]` (CameraSlaveParam+조명 8필드 자동)
  - `[SHOT_{s}_FAI_{f}]` + `FAIName` + `MeasurementCount`
  - `[SHOT_{s}_FAI_{f}_MEAS_{m}]` → `meas.Save(...)` + `Type = meas.TypeName` 수동 저장
- `Load` 진입점에서 `DetectFormatVersion` 호출:
  - Phase6 → `LoadPhase6Format`
  - 그 외 → `CustomMessageBox.Show("Legacy Recipe", ...)` + `return false` (D-22)
- `LoadPhase6Format`:
  - Fixture 섹션에서 DisplayName/DatumCount + DatumConfigs.Clear() 후 `AddDatum()` → `datum.Load(...)`
  - Shots 루프 — 기존 흐름 유지 + `_CAM` 자동 로드
  - 각 FAI마다 `MeasurementCount` 루프 → `MeasurementFactory.Create(typeName, fai)` null이면 skip + 로그 (T-06-07), 성공 시 `meas.Load(...)` 후 `fai.Measurements.Add(meas)`
  - DatumCount/ShotCount/FAICount/MeasurementCount 음수는 0으로 clamp (T-06-07)
- `HasNewFormatData(iniFile)`는 `DetectFormatVersion == Phase6`만 true로 반환 (SequenceHandler의 `TryLoadNewFormat` 게이팅을 Phase 6 전용으로 자연스럽게 전환)

## Verification
- `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` → **성공** (`DatumMeasurement.exe` 생성)
- 수락 기준 확인:
  - Action_FAIMeasurement: `DatumPhase` enum ✓, `TryRunDatumPhase` ✓, `TryGetDatumTransform` ✓, `meas.TryExecute(` ✓, `meas.EvaluateJudgement(` ✓, `fai.Measurements` 루프 ✓, `FAIEdgeMeasurementService.*TryMeasure` 호출 없음 ✓
  - InspectionRecipeManager: `ERecipeFormatVersion` ✓, `DetectFormatVersion` ✓, `Version = CurrentFormatVersion (=6)` ✓, `MeasurementFactory.Create` ✓, `meas.TypeName` ✓, `CustomMessageBox.Show` ✓, `FIXTURE_DATUM_` ✓, `_MEAS_` ✓

## Design Notes
- **Parent 경로**: `SequenceBase`가 `act.Param.Parent = this`를 설정하므로 `ShotParam.Parent as InspectionSequence`로 Fixture(상위 시퀀스)를 얻는다. 추가 와이어링 불필요.
- **FAIConfig legacy result 집계**: MeasurementBase가 per-Measurement 결과를 가지지만, 기존 UI/TCP 응답이 `FAIConfig.IsPass`/`MeasuredValue`를 사용한다. 첫 Measurement의 값으로 대표 집계하여 하위 호환 유지. 향후 Plan 04에서 UI를 Measurement 레벨로 확장하면 이 대표 집계 로직은 제거 가능.
- **PixelResolution 소스**: MeasurementBase.TryExecute는 per-Measurement pixel resolution을 받는다. 현재는 `fai.PixelResolutionX`를 전달 (FAIConfig에만 Calibration 필드가 존재). D-16은 camera-level이지만 FAIConfig에도 호환 필드가 있어 그대로 활용.
- **Fixture 해석 전략**: 현 단계에서는 Fixture가 사실상 "Top InspectionSequence" 1개이다. `ResolveFixtureSequence`는 Top만 조회. 향후 Multi-Fixture 확장 시 RecipeManager에 `FixtureId` 프로퍼티를 추가하면 된다 (Phase 6 Plan 04 범위).
- **HasNewFormatData 의미 변경**: 기존에는 "SHOTS 섹션 존재 = 신규 포맷"이었으나, Phase 6에서는 "[FORMAT] Version=6 = 신규 포맷"으로 엄격해졌다. 이에 따라 `SequenceHandler.TryLoadNewFormat`은 Phase 5 SHOTS-only INI를 더 이상 로드하지 않고 자동으로 IsDynamicFAIMode=false로 떨어진다. `Load` 내부의 `CustomMessageBox`는 사용자가 구 포맷을 명시적으로 Load 시도할 때만 표시된다.
- **Measurement Type 저장 위치**: `ParamBase.Save`가 추상 프로퍼티를 serialize하지 않으므로 `TypeName`을 수동으로 `saveFile[measSection]["Type"]`에 기록한다. Load 시 이 키를 읽어 Factory로 파생 타입을 생성한다.
- **예외 안전성**: `Run()`은 예외를 상위로 던지지 않는다. `HOperatorSet.HomMat2dIdentity`와 `meas.TryExecute`를 try/catch로 감싸 Halcon/측정 런타임 오류가 sequence thread를 죽이지 않도록 보장 (CLAUDE.md Error Handling 섹션 준수).

## Deviations from Plan
- **None (Rule 1-3)** — 플랜이 지시한 두 Task를 그대로 수행. 추가 수정(Rule 2/3) 없음.
- 주석 날짜: 플랜은 `//260410 hbk`로 언급했으나 STATE.md/오늘 날짜가 2026-04-13이므로 `//260413 hbk`를 사용 (사용자 컨벤션 준수).

## Commits
- `63ba1e7 feat(06-03): Action_FAIMeasurement Multi-Datum + Measurement 루프 재설계`
- `35c9f21 feat(06-03): InspectionRecipeManager Phase 6 INI 포맷 재작성`

## Self-Check: PASSED
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` FOUND
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` FOUND
- `.planning/phases/06-rapid-city/06-03-SUMMARY.md` FOUND
- Commits 63ba1e7, 35c9f21 FOUND in git log
- Build: msbuild Debug x64 → 성공 (DatumMeasurement.exe 생성) FOUND
- Acceptance grep targets (`DatumPhase`, `TryRunDatumPhase`, `TryGetDatumTransform`, `meas.TryExecute`, `meas.EvaluateJudgement`, `fai.Measurements`, `ERecipeFormatVersion`, `DetectFormatVersion`, `MeasurementFactory.Create`, `CustomMessageBox.Show`, `FIXTURE_DATUM_`, `_MEAS_`, `TypeName`) 모두 존재 FOUND
