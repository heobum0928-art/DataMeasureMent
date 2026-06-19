---
phase: 55
slug: align-02-two-pattern-baseline
title: "ALIGN-02 — 2-패턴 baseline 각도 보정"
date: 2026-06-19
status: in-progress
---

# Phase 55 — ALIGN-02: 2-패턴 baseline 각도 보정

## 배경 / 문제
Phase 54 단일 패턴 alignRigid(8e0bdee)는 tilt 측정 "—"는 해소했으나, **단일 패턴 각도 정밀도 한계로 먼 측정점에 ~400µm(0.4mm) 잔차**(lever-arm: 각도 ~0.x° 오차 × 7000px). 단일 find_shape_model 각도(±0.1~0.5°, 미묘 패턴 ±1°)가 부족.

## 해결 원리 (사용자 설계, 잠금)
부품 양 대각 끝에 **패턴 ROI 2개** → 두 매칭 **중심점**으로 각도 산출:
```
티칭:   P1ref=ROI1중심, P2ref=ROI2중심 → refBaseline = atan2(P2ref − P1ref)
런타임: P1cur, P2cur (각 패턴 매칭 중심) → curBaseline = atan2(P2cur − P1cur)
θ = curBaseline − refBaseline           (각도 = 두 점 baseline 변화)
X/Y = P1cur − P1ref                      (점1 기준 이동)
T = identity → rotate(θ, pivot=P1ref) → translate(X/Y)   ← 기존 alignRigid 구조 재사용
```
**핵심**: 각 패턴 자체 회전각(find_shape_model angle)은 **미사용**. 위치(중심)만 사용 → 긴 baseline 으로 각도 정밀(±0.01~0.05°).

## 잠긴 결정
| 항목 | 결정 |
|---|---|
| 폴백 | 한쪽 패턴 매칭 실패 → **단일 패턴**(성공한 쪽 각도, 기존 8e0bdee 경로) + 경고 |
| 각도 | 두 ROI 중심 baseline 각 (cur−ref). 패턴 자체각 미사용 |
| X/Y | 점1(먼저 그린 PatternRoi=기존) 기준 |
| 회전중심 | P1ref |
| 옵션 | PatternRoi2 미설정(Length=0) → 단일 패턴 자동 폴백 (하위호환, 회귀 0) |
| 변환 | 명시식 (rotate θ about P1 + translate), 기존 InspectionSequence:469-471 구조 |
| 모델2 | `Datum{name}_2.shm` (ResolveDatumModelPath2) |
| 매칭 파라미터 | PatternEngine/MinScore/AngleExtent/SearchMargin 점1·점2 공유 |
| 직선 ROI | 제거 (AlignLineRoi 필드/UI/메서드 전부) |

## Waves

### Wave 1 — 데이터 모델 (DatumConfig)
- `PatternRoi2_Row/Col/Phi/PhiDeg/Length1/Length2` + `RefMatch2Row/Col` 추가 (점1 미러).
- ParamBase INI 자동 직렬화. 미설정 시 0 → 단일 폴백.

### Wave 2 — 모델 경로 + 런타임 (InspectionSequence + PatternMatchService)
- `ResolveDatumModelPath2`(propertyName=DatumName+"_2") — 2번째 .shm 경로.
- `TryComposeAlign`: PatternRoi2_Length>0 이면 패턴2 매칭 → 두 점 baseline θ 산출 → alignRigid 의 θ 교체. 점2 실패/미설정 → 단일(현 θ) 폴백 + 경고 로그.

### Wave 3 — 티칭 UI (MainView + InspectionListView)
- "패턴 2 그리기" 버튼 + 모델2 생성(InvokeCreatePatternModel 미러) + RefMatch2 write-back.
- 양 대각 끝 안내 힌트.

### Wave 4 — 직선 ROI 제거
- DatumConfig AlignLineRoi_* 필드 + AlignLineRefAngleDeg 제거.
- DatumFindingService.TryGetAlignLineAngle 제거.
- MainView: ECanvasMode.AlignLineRoi, DrawAlignLineRoiButton_Click, HalconViewer_AlignLineRectCompleted, 티칭 각도캡처 블록 제거.
- MainView.xaml: btn_drawAlignLineRoi 버튼 제거. InspectionListView btn_drawAlignLineRoi 참조 제거.
- INI 호환: 구 AlignLineRoi_* 키는 ParamBase 가 무시(프로퍼티 없음) → 무해.

## UAT (사용자 SIMUL)
1. Datum 에 패턴 2개(대각 끝) 티칭 → tilt 이미지 검사 → **먼 측정점 잔차 400µm → 수십µm 이하**로 감소 확인.
2. 패턴2 미설정 datum → 기존 단일 패턴 동작 그대로 (회귀 0).
3. 패턴2 일부러 가림/실패 → 단일 폴백 + 경고 로그 확인.
4. CorrectionFactor 병행 → 큰 값 판정 정상.

## 회귀 안전
- PatternRoi2 미설정 = 단일 폴백(현 8e0bdee 동작) → 기존 align datum 회귀 0.
- 직선 ROI 는 이미 미사용(CO-54-04) → 제거 무영향.

## 환경
- 빌드 VS2022 MSBuild Debug/x64. 앱 실행 중이면 exe-copy만 실패(컴파일 OK). 하위에이전트 차단 → 인라인.
- atan2/HomMat2dRotate 부호 규약은 **SIMUL UAT 로 검증**(θ 방향 = 측정−Ref).
