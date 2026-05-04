# Phase 18: Carry-over 정리 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-05
**Phase:** 18-carry-over-cleanup
**Areas discussed:** CO-04 ROI 재그리기 메뉴, CO-05 Strip 색상 분기, CO-03 Spec 정리 형식

---

## CO-04: ROI 재그리기 메뉴

| Option | Description | Selected |
|--------|-------------|----------|
| Edit 모드 ON + Datum 티칭구에서만 | btn_teachDatum 상태일 때만 표시 | ✓ |
| Edit 모드 ON 시 항상 표시 | FAI ROI + Datum ROI 모두 대상 | |

| Option | Description | Selected |
|--------|-------------|----------|
| Length=0 리셋만 | ROI 필드 0 초기화, 수동 재그리기 | ✓ |
| Length=0 + 자동 그리기 모드 진입 | 리셋 후 StartDatumTeachStep 자동 실행 | |

| Option | Description | Selected |
|--------|-------------|----------|
| ROI 다시 그리기 (한국어) | 직관적, 연습 데이터 제거 의미 명확 | ✓ |
| Re-draw ROI (영어) | UAT 원안 텍스트 | |
| ROI 초기화 | 더 테크니컬한 느낌 | |

| Option | Description | Selected |
|--------|-------------|----------|
| 메뉴 목록에서 숨김 | ROI 없으면 항목 비표시 | ✓ |
| 항목 표시하되 비활성화 | 메뉴는 보이지만 클릭 불가 | |

**User's choice:** Datum 티칭 모드 + ROI hit-test 통과 시만 표시. "ROI 다시 그리기". Length=0 리셋만. ROI 없으면 숨김.

---

## CO-05: Strip 색상 분기 범위

| Option | Description | Selected |
|--------|-------------|----------|
| Circle polar strip만 | CTH 알고리즘 전용 | ✓ |
| Circle + Horizontal A/B 모두 | CTH/VTH 전체 적용 | |

| Option | Description | Selected |
|--------|-------------|----------|
| DatumConfig bool[] transient 필드 | Phase 17 transient 패턴 재사용 | ✓ |
| DatumFindingService 반환값에 overlay data 포함 | 더 명확하지만 시그니처 변경 큼 | |

| Option | Description | Selected |
|--------|-------------|----------|
| 티칭(TryTeach) 시에만 | TryTeachCircleTwoHorizontal 완료 시 갱신 | ✓ |
| Test Find(TryFind) 시에만 | btn_testFindDatum 후 색상 표시 | |
| 둘 다 (Teach + Find) | 양쪽 경로에서 갱신 | |

**User's choice:** Circle polar strip만. DatumConfig.CircleStripSuccesses bool[] transient. TryTeach 시에만 갱신.

---

## CO-03: Spec 정리 형식

| Option | Description | Selected |
|--------|-------------|----------|
| 코드 변경 없음 — 현재 hotfix#3 동작이 올음 | IsConfigured 게이팅 그대로 유지 | ✓ |
| 코드 변경 필요 — 로직 부족 | 새 Datum + ROI 미생성 시 모달 필요 | |

| Option | Description | Selected |
|--------|-------------|----------|
| UAT 문서에 spec 명시 + grep 검증 명령어 | 18-UAT.md Test 10 재작성 | ✓ |
| 별도 Spec 파일 작성 | 18-SPEC.md 신규 생성 | |

**User's choice:** 코드 변경 없음. 18-UAT.md Test 10에 현재 IsConfigured 게이팅 동작 명문화 + grep Acceptance 추가.

---

## 알고리즘 3종 배치 (Deferred)

**추가 논의:** v1.1-MILESTONE_add.md 파일에서 3개 신규 알고리즘 발견.

| Option | Description | Selected |
|--------|-------------|----------|
| Phase 18에 포함 | Carry-over와 합산 | |
| v1.1 로드맵 신규 Phase 삽입 | Phase 19 이후 19.5번으로 | ✓ |
| v2.0으로 이연 | 현재 로드맵 유지 | |

| Option | Description | Selected |
|--------|-------------|----------|
| Phase 19 이후 (19.5번) | PropertyGrid 기반 안정 후 추가 | ✓ |
| Phase 18 직후 (18.5번) | Carry-over 후 바로 추가 | |
| Phase 20 이후 | 코드 스타일 정리 후 | |

**User's choice:** v1.1 로드맵에 Phase 19 이후 신규 Phase로 삽입. `/gsd-insert-phase` 사용 예정.

---

## Claude's Discretion

- CO-01 버그 root cause 탐색 방식
- CO-04 hit-test 로직 구현 방식 (기존 HitTestSelectedRoi 재사용 vs 신규)

## Deferred Ideas

- 알고리즘 3종 (ArcEdgeDistance/CompoundAngle/LineConstructDistance) → Phase 19.5
- GR&R 엑셀 AIAG 표준 전체 — Phase 25 discuss 시 재검토
- Manual + Verify 워크플로우 — v1.1 백로그
