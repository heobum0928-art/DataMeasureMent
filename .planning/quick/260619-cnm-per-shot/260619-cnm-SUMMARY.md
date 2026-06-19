---
quick_id: 260619-cnm
slug: per-shot
title: "per-shot 측정 보정계수 (CorrectionFactor) 백엔드"
date: 2026-06-19
status: complete
commit: d6c95a7
---

# SUMMARY — per-shot 측정 보정계수 백엔드

## 무엇을 / 왜
비전 측정값이 현미경 공칭(=CAD/도면이 아니라 현미경 실측 기준)과 **~0.5% 균일 비율로 어긋남**(큰 측정 NG, 작은 측정 OK). 근본원인은 ALIGN/회전/strip 아닌 **측정값↔공칭 캘리브레이션 간극**(quick 260618-o2m 확정). `PixelResolution=0.00265`는 수동 대략값.

사용자 정책: **PixelResolution = 1회 제대로 캘리브 후 고정(불변 물리 진실)**. 그 위에 **별도 보정계수 layer** 를 둬서 실데이터로 측정을 공칭에 맞춤. → 이번 작업 = 그 layer 백엔드.

## 설계 결정 (에이전트 패널 3인)
per-shot vs per-FAI 를 변호인 2 + 중립 1 로 토론 → **per-shot 곱셈 합의**:
- 오차가 균일·등방(X=Y) → 카메라 배율오차이지 항목별 잔차 아님.
- per-shot 곱셈 = `PixelResolution` 재스케일과 수학적 동치 → 별도 필드로 두면 "분해능 고정" 정책 보존하며 균일오차 흡수.
- 표본 부족(한두 점)으로 per-FAI(112개)는 과적합/관리지옥. 규모 26샷/71FAI/112측정.
- per-FAI override 는 데이터 모델상 후속 무리없이 추가 가능 → **탈출구로 남김**(이번 미구현).

## 변경 (3 파일, 회귀 0)
1. **`CameraSlaveParam.cs`** (PixelResolution 과 동일 클래스 → ShotConfig 상속 = 두 소비점 캐스팅 0):
   - `public double CorrectionFactor { get; set; } = 1.0;` — ParamBase reflection INI 자동 직렬화, **키 미존재 시 1.0 폴백**(하위호환).
   - `public double GetEffectivePixelResolution() => PixelResolution * CorrectionFactor;` — **메서드라 INI 직렬화 안 됨** → PixelResolution 저장값 불변, 런타임 곱만.
2. **`Action_FAIMeasurement.cs:265`**: `ShotParam.PixelResolution` → `ShotParam.GetEffectivePixelResolution()`. 이 단일 주입점이 14종 측정 타입에 전파. 직후 **가드레일**: `|CorrectionFactor−1|>0.02` 면 ELogType.Error 경고 1줄(정상 0.5%=factor 0.995 는 **미발동**, >2% 만 = 분해능/공칭/왜곡 신호).
3. **`EdgePairDistanceMeasurement.cs:74`**: 이 타입**만** 전달 param 무시·`ownerShot.PixelResolution` 재도출 → 동일 `GetEffectivePixelResolution()` 호출로 누락 방지.

## 소비 경로 전수 확인 (왜 두 곳인가)
- `:265` → DualImage/EdgeToLine/PointToPoint/PointToLine/LineToLine/Circle*/Compound*/Arc*/ArcLineIntersect 등 14종이 전달 `pixelResolution` 파라미터 사용 → 단일 전환으로 일괄 적용.
- `EdgePairDistance:74` 만 예외(Phase 42 D-06 재도출 코드) → 별도 적용.
- 각도 2종(EdgeToLineAngle/LineToLineAngle)은 `pixelResolution` 미사용(deg) → **자동 제외**(분기 불필요).
- `FAIEdgeMeasurementService:294`의 `fai.PixelResolutionX` 는 EdgePairDistance 의 temp FAIConfig 경유(=`resolvedPixelRes`) → 이미 커버됨.

## 검증
- **빌드: 컴파일 PASS (0 에러).** MSBuild Debug/x64 가 copy 단계(MSB3027)까지 도달 = CSC 0 에러 통과 증거. exe-copy 만 실패(앱 PID 16044 실행 중 — 정상). obj exe 31초 전 생성 = 내 변경 포함 신규 컴파일 확증.
- 정합: 양 소비점 `GetEffectivePixelResolution()` 호출 연결, CorrectionFactor 직렬화 경로(public double → ParamBase case "Double").

## UAT (사용자 수동) — 미수행
1. 레시피 main.ini 한 Shot 의 `CorrectionFactor` 를 `0.995`(≈ −0.5%) 로 설정 → 그 Shot 의 큰 측정값(예 A1 20.77→20.67)이 공칭(20.681) 근처로 이동 + 판정 OK 전환 확인.
2. `CorrectionFactor=1.0`(미설정 포함) → 측정값 기존과 동일(회귀 0) 확인.
3. `CorrectionFactor=0.95`(>2%) → Error 로그에 가드레일 경고 1줄 출력 확인.
4. 보정 후에도 raw = corrected / factor 로 복원 가능(데이터 손실 0).

## 비범위 (후속 phase)
- **보정값 입력 전용 UI** + `RepeatMeasurementStats.Mean` 기반 **자동산출**(공칭/반복측정평균). 현재는 PropertyGrid(General|AOI, PixelResolution 옆)에서 수동 입력 가능 — 전용 화면 아님.
- **per-FAI override** (`MeasurementBase.CorrectionFactor`, 항목별 잔차 확인 시) — 데이터 모델 탈출구 설계됨.
- 원측정 raw 별도 컬럼/Export 표시.
- (선택) X/Y 이방성 확인 시 `CorrectionFactorX/Y` 2스칼라 확장.

## 환경/제약
- 앱 실행 중이라 in-place exe-copy 실패(컴파일 무관). 최종 exe 반영은 앱 종료 후 재빌드 필요.
- GSD 서브에이전트 Edit/Write/Bash 권한 차단 → 오케스트레이터 인라인 실행(Phase 51/54 동일 패턴).
- 커밋 **d6c95a7** (code, 3 files / +16 −2).
