# Phase 34: Datum VerticalTwoHorizontal 듀얼 티칭 이미지 — Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in 34-CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-27
**Phase:** 34-datum-verticaltwohorizontal-2026-05-26
**Areas discussed:** ROI↔이미지 매핑 방향, EDatumAlgorithm enum + 필드 명명, 티칭 UI step + 이미지 전환 UX, INI 호환 + D-06 가드 충돌

---

## ROI↔이미지 매핑 방향

| Option | Description | Selected |
|--------|-------------|----------|
| A. ROADMAP 채택 — 가로 2 ROI=TeachingImagePath, 세로 ROI=신규 필드 | 기존 TeachingImagePath = 'Horizontal A+B' 이미지, 신규 TeachingImagePath_Vertical = 'Vertical' 이미지. ROADMAP §SC 3 일치. | ✓ |
| B. 메모리 채택 — 세로 ROI=TeachingImagePath, 가로 2 ROI=신규 필드 | 기존 TeachingImagePath = 'Vertical' 이미지, 신규 필드(예: TeachingImagePath_Horizontal) = 'Horizontal A+B'. project_phase35_progress 2026-05-27 사용자 보고와 일치. | |
| C. 대칭 설계 — 각 ROI 그룹이 자기 이미지 필드 보유 | TeachingImagePath_Horizontal + TeachingImagePath_Vertical 두 신규 필드. 기존 TeachingImagePath 는 1-image 타입 전용. | |

**User's choice:** A — ROADMAP 대로
**Notes:** ROADMAP §SC 3 "가로 ROI 2개는 image1, 세로 ROI 1개는 image2" 확정. memory project_phase35_progress 의 역방향 기록(2026-05-27 사용자 보고로 적혀 있음)은 본 결정으로 정정 대상 — write_context 후 메모리 업데이트 필요.

---

## EDatumAlgorithm enum 명명 + DatumConfig 필드명

### Q1: 신규 enum 값 명명

| Option | Description | Selected |
|--------|-------------|----------|
| VerticalTwoHorizontalDualImage | ROADMAP 본문 가칭 그대로. 명시적 'DualImage' 접미사 — 향후 dual 변형 일관성. | ✓ |
| VerticalTwoHorizontal_2Image | _2Image 표기 — 이름 길어 헷갈릴 수 있음. | |
| VerticalTwoHorizontalSplit | 'Split' = ROI 두 이미지로 나눠 계측. 짧고 읽기 좋음. | |

**User's choice:** VerticalTwoHorizontalDualImage

### Q2: 두 번째 티칭 이미지 필드명

| Option | Description | Selected |
|--------|-------------|----------|
| TeachingImagePath_Vertical | 역할 명시 (Vertical ROI 전용). ROI 이름과 1:1. | ✓ |
| TeachingImagePath_2 | 인덱스 기반. 일반화 가능. | |
| TeachingImagePath_B | A/B 쌍 — 그러나 기존 TeachingImagePath 와 비대칭. 비추천. | |

**User's choice:** TeachingImagePath_Vertical

---

## 티칭 UI step 머신 + 이미지 전환 UX

### Q1: 이미지 전환 시점

| Option | Description | Selected |
|--------|-------------|----------|
| A. 자동 전환 (HorizontalB 완료 시 TeachingImagePath_Vertical 자동 로드) | step 이 Vertical 로 넘어가면서 HalconViewer 가 자동 교체. 조작 간소화. | ✓ |
| B. 명시적 — 'Vertical 이미지 로드' 전용 두 번째 Load 버튼 신설 | 신규 algorithm 일 때만 버튼 노출. | |
| C. step 파이프라인 확장 — image-switch step 명시 구분 | EDatumTeachStep.SwitchToVerticalImage enum 추가. 사용자 안내 명확하나 step 머신 복잡. | |

**User's choice:** A — 자동

### Q2: TeachingImagePath_Vertical 경로 비어 있을 때 가드 시점

| Option | Description | Selected |
|--------|-------------|----------|
| A. 검사 시점 가드 — EContextResult.Error 처리 (런타임) | algorithm=DualImage + 빈 경로 → 즉시 Error. | ✓ |
| B. 저장 시점 가드 — MessageBox 경고 + 커밋 차단 | Validate hook 필요. | |
| C. 둘 다 — 저장 경고 + 검사 시점 Error | 두 계층 동시 가드. | |

**User's choice:** A — 검사 시점만
**Notes:** 티칭 중 자동 전환 시점에 TeachingImagePath_Vertical 가 비어 있으면 캔버스 클리어 + 안내 텍스트만 표시(저장 막지 않음). Claude 재량 — D-34-08.

---

## INI 호환 정규화 + D-06 가드 충돌 처리

### Q1: INI 정규화 정책

| Option | Description | Selected |
|--------|-------------|----------|
| A. 일률 '' 정규화 (Phase 22 IMG-01 패턴 계승) | algorithm 무관, null→'' 가드. DatumConfig.cs:563 패턴 그대로 1줄 추가. | ✓ |
| B. 신규 algorithm 일 때만 강제 | 기존 algorithm 은 null 유지 — NullReferenceException 위험. 비추천. | |
| C. TeachingImagePath 폴백 | null 시 TeachingImagePath 와 동일 값 — 신규 algorithm 단일 이미지 오작동 위험. 비추천. | |

**User's choice:** A — 일률 '' 정규화

### Q2: Phase 33 D-06 가드 충돌 처리

| Option | Description | Selected |
|--------|-------------|----------|
| A. D-06 가드 'Phase 34에 한정 해제' 명시 + 변경 범위 최소화 | GrabOrLoadDatumImage 안 ROI ID 기반 switch 하나만 허용, CONTEXT/PLAN 양쪽 명시. 1-image 회귀 0 검증 필수. | ✓ |
| B. 우회 — DatumFindingService 내부에서 두 이미지 로드/적용 완결 | Action_FAIMeasurement 변경 0 라인 유지. 단, DatumFindingService 가 file I/O 책임 떠안아 아키텍처 일관성 깨짐. | |
| C. D-06 완전 해제 (Phase 34 이후) | 속도 우선. Phase 33 안정성 레버리지 상실 가능성. | |

**User's choice:** A — 한정 해제 + 변경 범위 최소화

---

## Claude's Discretion

- HalconViewerControl 이미지 자동 전환 hook 위치 (MainView step change handler 안 vs HalconViewerControl 직접 호출)
- ICustomTypeDescriptor IsHiddenForAlgorithm 안 case 추가 위치 (Phase 17 패턴 자명)
- AlgorithmType 콤보박스 라벨 한글 표기 여부 (현재 ToString() 그대로 따름)
- 두 이미지 해상도/orientation 일치성 가정 검증 (researcher 영역)

## Deferred Ideas

- 다른 algorithm 의 dual-image 변형 (CircleTwoHorizontalDualImage 등) — 향후 phase
- DatumFindingService dual-image variant 가 늘어나면 인터페이스 일반화 — v1.2 후보
- 두 이미지 해상도/orientation 불일치 자동 검증 — 운영 발견 시 별도 phase
- 티칭 중 두 이미지 동시 미리보기 (split-view) — UI 확장 후보
