---
quick_task: 260517-l5e
title: EdgeToLineDistance datum 교점 기준 Y거리 계산 수정
date: 2026-05-17
status: complete
commits:
  - hash: 74d0d15
    task: 1
    message: "feat(quick-260517-l5e-01): EdgeToLineDistanceMeasurement datum 교점 좌표 프로퍼티 추가 + 교점 기준 Y거리 계산/overlay 교체"
  - hash: 19e5663
    task: 2
    message: "feat(quick-260517-l5e-02): Action_FAIMeasurement EStep.Measure 에서 EdgeToLineDistance datum 교점 주입"
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
build: PASS (Debug/x64, 0 errors, 0 new warnings)
---

# Quick Task 260517-l5e: EdgeToLineDistance datum 교점 기준 Y거리 계산 수정

**One-liner:** datum 교점(DatumConfig.DetectedOriginRow) 직접 주입으로 이미지 row 0 기준 오측정 근본 원인 제거.

## 근본 원인 요약

`EdgeToLineDistanceMeasurement.TryExecute` 가 `datumTransform` (part-drift 보정 델타) 을
좌표 변환 행렬로 오용했다. SIMUL 모드에서 `curOrigin ≈ RefOrigin` → `transform ≈ Identity`
→ `tRow ≈ pRow` (에지의 raw image row) → 측정값이 이미지 row 0 기준 거리가 되어버리는 버그.

진짜 datum 교점 좌표는 `DatumConfig.DetectedOriginRow/Col` 에 있으나
측정 객체에 전달되지 않고 있었다.

## 수정 내용

### Task 1 — EdgeToLineDistanceMeasurement.cs (commit 74d0d15)

**datum 교점 좌표 입력 프로퍼티 3개 추가:**
- `DatumOriginRow` — datum 교점 row (image 좌표), 미주입 시 0
- `DatumOriginCol` — datum 교점 col (image 좌표), 미주입 시 0
- `DatumAngleRad` — datum 기준선 각도(rad), 향후 회전 보정 확장 지점
- 모두 `[Browsable(false)]` + `[JsonIgnore]` 부착 (DatumConfig.DetectedOrigin* 패턴 일치)

**Y거리 계산 교체 (datumOriginInjected 분기):**
- 정상 경로: `resultValue = -(pRow - DatumOriginRow) * pixelResolution` — D-02 +Y 부호 보존
- 레거시/무보정 폴백: 기존 `AffineTransPoint2d(datumTransform, ...)` 경로 유지 (try/catch 포함)

**FAI-DistLine overlay 시작점 교체:**
- 정상 경로: `originRow = DatumOriginRow; originCol = DatumOriginCol` (역행렬 불필요)
- 레거시 폴백: 기존 `HomMat2dInvert → AffineTransPoint2d(invMat, 0.0, 0.0)` 경로 유지

**보존된 제약:**
- D-02: `-(pRow - DatumOriginRow) * pixelResolution` 앞 마이너스 보존
- D-08: TryFitLine 에 리터럴 `"All"` 전달 (EdgeSelection 필드 무시) — 무수정
- D-09: ICustomTypeDescriptor EdgeSelection 숨김 — 무수정
- MeasurementBase.TryExecute 시그니처 — 무수정

### Task 2 — Action_FAIMeasurement.cs (commit 19e5663)

**EStep.Measure measurement 루프에 EdgeToLineDistance 타입 분기 추가:**
- `transform` 결정 블록 직후, `double resultValue;` 선언 직전에 삽입
- `meas as EdgeToLineDistanceMeasurement` → null 체크 (C# 7.2, 파일 기존 패턴 일치)
- `DatumRef` 매칭 DatumConfig 검색 → `DetectedOriginRow/Col/RefAngle` → `DatumOriginRow/Col/AngleRad` 주입
- DatumRef 빈 문자열/매칭 실패 시 0 명시 주입 (폴백 경로 사용)
- 나머지 5종 (`etld == null`) 분기 미진입 — 회귀 0
- `parentSeq2 == null` 시 NRE 없음 (null-safe 가드)
- using 추가 없음 (동일 namespace)

### Task 3 — 빌드 검증 (코드 변경 없음)

```
MSBuild Debug/x64 → DatumMeasurement.exe
경고 2건: MSB3884(ruleset) + CS0162(VirtualCamera.cs:266) — 모두 기존 baseline 경고
신규 경고: 0
오류: 0
결과: Build succeeded
```

## 보존 확인

| 제약 | 확인 |
|------|------|
| D-02 +Y 부호 | `resultValue = -datumRelRow * pixelResolution` 앞 마이너스 보존 |
| D-08 리터럴 "All" | TryFitLine 마지막 인자 `"All"` 무수정 |
| D-09 EdgeSelection 숨김 | ICustomTypeDescriptor BuildFilteredProperties 무수정 |
| MeasurementBase 시그니처 | 6-param TryExecute 무수정 |
| 나머지 5종 측정 | etld == null → 분기 미진입, 회귀 0 |

## Deviations from Plan

없음 — 계획대로 정확히 실행됨.

## Known Stubs

없음.

## Threat Flags

없음 (네트워크 엔드포인트, 파일시스템 접근 패턴, 스키마 변경 없음).

## Self-Check: PASSED

- `74d0d15` commit 존재: FOUND
- `19e5663` commit 존재: FOUND
- `EdgeToLineDistanceMeasurement.cs` DatumOriginRow 프로퍼티: FOUND (line 61)
- `Action_FAIMeasurement.cs` etld 타입 분기: FOUND (line 161)
- msbuild Build succeeded: CONFIRMED
