# Phase 14: Datum carry-over - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-26
**Phase:** 14-datum-carry-over-circle-vertical-roi-2-out-of-range-ux
**Areas discussed:** A (Circle resize 핸들), B (INI 마이그레이션), C (PropertyGrid 가시성), D (14-05 진입), E (좌표계 부호)
**Mode:** SPEC.md loaded — HOW only

---

## A. Circle ROI Resize 핸들

| Option | Description | Selected |
|--------|-------------|----------|
| (1) 별도 신호선 | 신규 `RoiResizeCompleted` 이벤트, 기존 move 와 분리 | |
| (2) 한 길로 통합 | 기존 `RoiMoveCompletedArgs` 에 `EditHandle?` 옵션 추가, 단일 이벤트 확장 | (선호, plan-phase 최종) |
| (3) Datum 전용 helper | FAI Circle 무영향, Datum 만 처리 | |

**User's choice:** UX 만 명시 — "동서남북에다가 작은 사각형으로 보여줘 그걸 조절하면서 사이즈 조정"
**Notes:** 코드 경로는 사용자 미답 → Claude's Discretion (D-04). 선호 (2) 단일 이벤트 확장. 핸들은 시각적인 작은 사각형 마커 (visible square) 4개. 4 핸들 모두 동일 로직 (drag 종점-center 거리 = 새 반경).

---

## B. INI 마이그레이션 정책 (Vertical 그룹 신설)

| Option | Description | Selected |
|--------|-------------|----------|
| (1) 양쪽 다 채움 | Line1 → Vertical 복사하되 Line1 도 그대로 유지 | ✓ |
| (2) Line1 zero out | Vertical 채우고 Line1 0 으로 초기화 | |
| (3) Vertical 만 사용 | Line1 INI 키 유지하되 코드 미참조 | |

**User's choice:** (1) 양쪽 다 채움
**Notes:** 회귀 위험 0, 데이터 손실 0. 알고리즘 전환 시 Line1 즉시 사용 가능. 마이그레이션은 sentinel 검출(Vertical_EdgeThreshold==0) 기준 idempotent.

---

## C. PropertyGrid 알고리즘별 가시성

| Option | Description | Selected |
|--------|-------------|----------|
| (1) 알고리즘별 동적 숨김 | AlgorithmType 따라 관련 그룹만 노출 | ✓ |
| (2) 항상 모두 노출 | 6 그룹 항상 표시, 사용자가 알아서 | |
| (3) Category 이름에 prefix | [VTH]/[CTH] 시각 구분만 | (fallback) |

**User's choice:** (1) 동적 숨김 — "너무 정보가 많아"
**Notes:** PropertyTools 3.1.0 의 `[Browsable]` 정적 한계로 구현 까다로움 인지. Plan-phase researcher 가 3 후보 중 (Browsable proxy / wrapper view-model / TypeDescriptor 동적 attribute) 가장 단순한 방식 채택. 너무 까다로우면 fallback (3) prefix 채택 가능 — 사용자 추가 승인 필요.

---

## D. 14-05 진입 방식 (CircleTwoH/VerticalTwoH 정상화)

| Option | Description | Selected |
|--------|-------------|----------|
| (1) 단순 verify 우선 | 14-03+14-04 합친 후 retest, FAIL 시 진단 로그 | ✓ |
| (2) 처음부터 audit | Phi/per-ROI/strip-loop 일괄 진단 로그 + fix | |
| (3) Circle polar 만으로 자동 해결 가정 | 14-04 만 적용, Vertical 만 별도 fix | |

**User's choice:** (1) 단순 verify 우선
**Notes:** 14-05 plan 은 default PASS path, FAIL contingency 메모 (진단 로그 추가 → 결함 식별 → fix → 재 retest).

---

## E. Circle Polar 좌표계 부호 컨벤션

| Option | Description | Selected |
|--------|-------------|----------|
| (1) 화면 시점 CCW | 0°=오른쪽, 90°=위(row-), 180°=왼쪽, 270°=아래(row+) | ✓ |
| (2) Halcon 표준 (수학) | 0°=오른쪽, 90°=아래(row+), 180°=왼쪽 | |

**User's choice:** (1) 화면 시점 CCW
**Notes:** 사용자 직관 일치 ("0° 에서 반시계로 회전" = 화면상 위로). Halcon image 좌표 변환식: rect 중심 row = `centerRow - radius * sin(theta)`, col = `centerCol + radius * cos(theta)`. Halcon Rectangle2 phi 부호는 SDK 문서 재검증 필수 (D-13 메모).

---

## Claude's Discretion

- D-04: Circle resize 코드 경로 (단일 이벤트 확장 선호, plan researcher 최종 결정)
- D-08: PropertyGrid 동적 가시성 구현 패턴 (3 후보 중 PropertyTools 실험 후 선택)
- D-13: Halcon Rectangle2 phi 부호 (SDK 문서 재검증)

## Deferred Ideas

- ROI Edit 모드 전반 재설계 (Polygon/Rect)
- Strategy 패턴 추상화 (Phase 13 deferred 유지)
- Halcon MeasureCircle 비교 spike
- PropertyGrid prefix fallback (D-08 후보 3)
- Vertical_* / Line1_* 양방향 동기화 (D-06 에서 안 함)
