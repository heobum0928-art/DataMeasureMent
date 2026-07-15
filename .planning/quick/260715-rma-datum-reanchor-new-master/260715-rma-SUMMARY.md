---
phase: quick-260715-rma
plan: 01
subsystem: inspection-teaching (datum 재앵커)
tags: [halcon, wpf, datum, roi-migration, rigid-transform]

# Dependency graph
requires:
  - phase: quick-260714(측정 ROI write-back/WYSIWYG 편집) — CollectShotRois/BuildPointRoiDefinitions/TransformRoiCenterInPlace/GetAnyInspectionSequence 재사용
provides:
  - MainView 재앵커 파이프라인 — 옛 datum 으로 새 마스터 Find(T) → 미리보기 → 확인 → 레시피 백업 → 측정/FAI/datum ROI 일괄 강체 이전
  - 지오메트리 변환 헬퍼: TransformPointInPlace / RotAngleOf / TransformMeasurementGeometry(전 측정타입) / TransformFaiRoi / TransformDatumOwnRois
  - "Re-anchor" 버튼(Datum 노드 선택 시 활성)
affects: [마스터 샘플 교체 워크플로]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "강체 재앵커: T = 옛datum으로 새마스터 Find한 CurrentTransform(=옛기준→새마스터). 중심=AffineTransPoint2d(T), 각도=+Atan2(-T[1],T[0]) 라디안, 길이/반경 불변. 런타임 VisionAlgorithmService.TryFitLine 과 동일 규약(조사 wf_c044c03a-65e 확정)."
    - "파괴적 일괄 변경 안전장치 4종: Find실패 무변경중단 + 미리보기(원본 불변, 노란 오버레이) + 확인모달 + 레시피 자동 백업(.reanchor_bak_*)."

key-files:
  created: []
  modified:
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/UI/ContentItem/MainView.xaml
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs

key-decisions:
  - "v1 범위 = 현재 선택된 Datum 노드 1개(멀티 datum 일괄은 후속). datum 별 T 필수(전역 T 금지) 원칙은 CollectMeasurementsForDatum 이 DatumRef==DatumName 로 필터해 자연 준수."
  - "재티칭은 자동 호출 안 함 — 순서(Find→이전→재티칭) 위반 시 T 소실 위험 + 재티칭 실패 처리 복잡. 커밋 후 '재티칭하세요' 수동 안내(패턴모델생성/Teach 버튼)."
  - "DualImage datum 은 두-이미지 find 필요 → v1 자동 재앵커 미지원(명시적 안내 후 중단)."
  - "EdgePairDistance 는 자체 지오메트리 없이 Owner FAI.ROI_* 사용 → 그 FAI rect 를 FAI당 1회 이전(HashSet 중복가드). 일반 point-ROI 측정은 자체 필드 이전."
  - "미리보기는 중심만 이전(TransformRoiCenterInPlace, 시인용). 실제 커밋은 각 타입 필드에 중심+Phi 정확 적용(TransformMeasurementGeometry)."

requirements-completed: [REANCHOR-01]

# Metrics
completed: 2026-07-15
---

# Quick Task 260715-rma: Datum 재-앵커(마스터 교체) Summary

**기준 마스터 교체 시 측정 ROI 를 재작업 없이 새 마스터 위치로 일괄 이전. 옛 datum 으로 새 마스터를 Find 한 강체 변환 T 를 모든 참조 측정 + FAI rect + datum 검색 ROI 에 적용. 미리보기·백업·Find실패-중단 안전장치 포함.**

## Accomplishments
- **지오메트리 변환 헬퍼(MainView)**: `TransformPointInPlace`(AffineTransPoint2d), `RotAngleOf`(Atan2(-T[1],T[0])), `TransformMeasurementGeometry`(13개 측정타입 전체 — Point_/Rect_/Circle_/EdgeA1~B2_/PointROI_·LineROI_/Line1_·Line2_/Point_·Line_/Point1_·Point2_), `TransformFaiRoi`(EdgePair/FAI rect), `TransformDatumOwnRois`(Line/Vertical/Horizontal_A·B/CircleROI/PatternRoi1·2). 길이/반경/Nominal/공차/보정계수 불변.
- **T 획득**: `TryFindTransformForReanchor` — Test Find 와 동일 경로(align=TryComposeAlign / non-align=TryFindDatum) 재사용. 실패 시 false+무변경. DualImage 는 미지원 안내.
- **오케스트레이션**: `BtnReanchor_Click` — Find→미리보기(노란 오버레이, 원본 불변)→확인모달→레시피 백업(.reanchor_bak_타임스탬프)→커밋(측정/FAI/datum ROI 이전)→재티칭 안내. Cancel/Find실패 시 완전 무변경.
- **UI**: MainView 툴바에 "Re-anchor" 버튼(Datum 노드 선택 시만 활성, InspectionListView 게이팅에 배선).
- **CollectMeasurementsForDatum**: 전 Shot 순회, DatumRef==DatumName 필터 → datum 별 대상 측정 수집.

## Build Verification
Task 1~3 각 단계 + 최종 Debug|x64 Build **0 errors**. 신규 warning 0.

## Regression Guard
- 재앵커 실제 변경 파일: MainView.xaml.cs / MainView.xaml / InspectionListView.xaml.cs 뿐.
- 측정/FAI/DatumConfig/MeasurementBase 클래스 무변경(git diff 의 그 파일 변경분은 이번 세션 앞선 작업 = 조명필드/측정별 보정계수 각도제외).
- BtnTestFindDatum_Click 무변경(find 로직은 신규 메서드로 별도 분리).

## Rule Audit
삼항 `?:` 0 / C# 8+ 0 / 신규 `//YYMMDD hbk` 0.

## 안전장치(파괴적 변경 방어)
1. Find 실패 → "재앵커 불가" + ROI 절대 무변경
2. 미리보기(노란 오버레이) — 확인 전 원본 좌표 불변, 사용자가 특징 정합 육안 확인
3. 확인 모달 OK 전 무변경, Cancel 시 원선택 재렌더
4. 커밋 직전 레시피 자동 백업(.reanchor_bak_*) — 실패 시 복원 가능

## HUMAN-UAT 대기 (실 하드웨어/실 마스터 필요)
1. **미리보기 정합**: 새 마스터 Grab → Re-anchor → 노란 미리보기 ROI 가 새 마스터 실제 특징 위에 얹히는지 육안 확인
2. **이전 정확도**: 확인 진행 → datum 재티칭 → Test Find → 검사 시 각 측정값이 공칭 범위(PASS)인지 (이중보정/오이전 없음 실증)
3. **Find 실패 케이스**: 새 마스터를 크게 이탈 배치 → "재앵커 불가" 뜨고 ROI 안 바뀌는지
4. **배율 상이 감지**: (전제 위반 테스트) 다른 배율 마스터로 시도 시 측정값이 체계적으로 어긋나는지 — 전제(동일 배율) 위반은 사용자 책임 영역
5. **각도 측정**: LineToLineAngle 등 각도 측정의 ROI 도 위치는 이전되고 측정값(각도)은 정상인지

## 후속(범위 밖)
- 멀티 datum 일괄 재앵커(현재 선택 1개 → 시퀀스 전체)
- DualImage datum 자동 재앵커(두-이미지 find)
- 폴리곤 ROI 자동 이전(현재 경고만)
- 재티칭 자동 연결(순서·실패 처리 안전 설계 후)

---
*Phase: quick-260715-rma*
*Completed: 2026-07-15*
