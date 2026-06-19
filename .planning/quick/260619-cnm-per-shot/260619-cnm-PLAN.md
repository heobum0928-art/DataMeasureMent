---
quick_id: 260619-cnm
slug: per-shot
title: "per-shot 측정 보정계수 (CorrectionFactor) 백엔드"
date: 2026-06-19
status: in-progress
---

# PLAN — per-shot 측정 보정계수 백엔드

## 목표
비전 측정값 ↔ 현미경 공칭 간 ~0.5% 균일 캘리브레이션 간극을, `PixelResolution`(1회 캘리브 후 고정)을 건드리지 않고 별도 **per-shot 보정계수 layer** 로 흡수.
측정 mm = `pixelDist × PixelResolution × CorrectionFactor`. 기본 1.0 = 무보정(회귀 0).

## 설계 근거 (에이전트 패널 3인 합의)
- **per-shot 곱셈** 단위: 오차가 균일·등방(X=Y) → 카메라 배율오차. 표본 부족으로 per-FAI 는 과적합. 규모 26샷/71FAI/112측정.
- per-shot 곱셈 = `PixelResolution` 재스케일과 수학적 동치 → 별도 필드로 두어 "분해능 고정" 정책 보존.
- per-FAI override 는 데이터 모델상 후속 무리없이 추가 가능(탈출구) — 이번 미구현.

## 소비 경로 (전수 확인)
- `Action_FAIMeasurement.cs:265` → 전 측정 타입에 `pixRes` 파라미터 전달 (DualImage/EdgeToLine/PointToPoint/LineToLine/Circle*/Compound*/Arc* 등 14종).
- `EdgePairDistanceMeasurement.cs:74` **만** 전달 param 무시, `ownerShot.PixelResolution` 재도출 → 별도 적용 필요.
- 각도 2종(EdgeToLineAngle/LineToLineAngle) pixelResolution 미적용 → 자동 제외.
- 단일소스 메서드 `CameraSlaveParam.GetEffectivePixelResolution()` 로 양 경로 통합.

## Tasks

### Task 1 — CameraSlaveParam: CorrectionFactor + GetEffectivePixelResolution()
- files: `WPF_Example/Sequence/Param/CameraSlaveParam.cs`
- action: PixelResolution(line 25) 직후 `public double CorrectionFactor { get; set; } = 1.0;` (ParamBase INI 자동 직렬화, 키 미존재 1.0 폴백). `ConvertPixelToMM` 부근에 `GetEffectivePixelResolution() => PixelResolution * CorrectionFactor` (메서드 = 미직렬화 → 저장값 불변).
- verify: grep `CorrectionFactor` + `GetEffectivePixelResolution` in CameraSlaveParam.cs
- done: 속성/메서드 존재, 빌드 통과.

### Task 2 — Action_FAIMeasurement: 적용 + 가드레일
- files: `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs`
- action: `:265` `ShotParam.PixelResolution` → `ShotParam.GetEffectivePixelResolution()`. 직후 `|CorrectionFactor−1|>0.02` 경고 로그(ELogType.Error, 정상 0.5%=factor 0.995 미발동).
- verify: grep `GetEffectivePixelResolution` + `CorrectionFactor` in Action_FAIMeasurement.cs
- done: 빌드 통과.

### Task 3 — EdgePairDistance: 재도출 경로 적용
- files: `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgePairDistanceMeasurement.cs`
- action: `:74` `ownerShot.PixelResolution` → `ownerShot.GetEffectivePixelResolution()`.
- verify: grep `GetEffectivePixelResolution` in EdgePairDistanceMeasurement.cs
- done: 빌드 통과 (MSBuild Debug/x64).

## 비범위 (후속 phase)
- 보정값 입력 전용 UI + `RepeatMeasurementStats.Mean` 기반 자동산출(공칭/평균).
- per-FAI override (`MeasurementBase.CorrectionFactor`).
- 원측정 raw 별도 컬럼/표시 (현재 raw = corrected/factor 복원 가능).

## 회귀 안전
- 기본 1.0 → 기존 동작 동일. 각도 미영향. PixelResolution 저장값 불변. CorrectionFactor 는 CameraSlaveParam(PixelResolution 동일 클래스)이라 ShotConfig 상속 = 두 소비점 캐스팅 0.
