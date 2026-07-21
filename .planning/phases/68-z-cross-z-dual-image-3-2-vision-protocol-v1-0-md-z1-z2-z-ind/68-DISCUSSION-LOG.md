# Phase 68: Z축 교차(Cross-Z) Dual-Image 측정 지원 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-21
**Phase:** 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind
**Areas discussed:** z_index 실행 스코프 수정 범위, Z1→Z2 상태 보존 위치/lifecycle, ZIndexA/B 필드 검증·배치, 스코프 경계(기존 static 파일 경로 + Datum 레벨 포함 여부)

---

## z_index 실행 스코프 수정 범위

| Option | Description | Selected |
|--------|-------------|----------|
| 완전 수정 | v1.0(UseProtocolV1) 경로에만 그침(v2.6/legacy 무수정, 회귀 0), $TEST(z=N) 도착 시 이 index에 매핑된 Shot만 StartSubset으로 실행. Phase 49 D-01을 실제로 닫고, 다른 z의 Shot이 불필요하게 재-grab되는 기존 문제도 같이 해결(조명/하드웨어 부담 감소 부수효과) | ✓ |
| 우회 | 실행 스코프는 안 건드리고, Z1에서 찾은 값만 즉시 별도 저장소로 스냅샷 떠서 안전하게 보존. 더 작고 안전하나, 다른 Shot들이 매 $TEST마다 불필요하게 재-grab되는 기존 갭(조명/하드웨어 부담)은 그대로 남음 | |

**User's choice:** 완전 수정
**Notes:** 처음엔 기술적 설명("Phase 49 D-01... SequenceBase.StartSubset...")이 어려워 두 차례 더 쉬운 설명(사진사가 10개 방 중 3번방만 가야 하는데 신호를 잘못 해석해서 10개 다 도는 비유)을 요청받음. 비유 후 "완전수정으로" 확정.

---

## Z1→Z2 상태 보존 위치/lifecycle

| Option | Description | Selected |
|--------|-------------|----------|
| 시퀀스 공유함(상자) + 자동 리셋 | InspectionSequence 멤버 상태(Phase 49 D-02와 동일 패턴)에 임시 보관, 새 사이클 시작(z=0 도착) 시 자동으로 비워짐. Z2가 안 와도 다음 부품 시작 시 저절로 새 상태로 시작 — 이전에 이미 같은 방식으로 구현된 것들이 있어 안전 | ✓ |
| 측정 자체에 직접 저장 | 측정/Datum 객체(레시피에 하나뿐인 재사용 객체)에 값을 두는 방식. 더 직관적이지만 사이클 경계에서 자동 비워줄 명확한 타이밍이 없어, 이전 부품의 값이 다음 부품으로 새면 위험성 있음 | |

**User's choice:** 시퀀스 공유함(상자) + 자동 리셋 (Recommended)
**Notes:** 별다른 추가 질문 없이 바로 추천안 채택.

---

## ZIndexA/B 필드 검증·배치

| Option | Description | Selected |
|--------|-------------|----------|
| 측정에 두고, 실행 시점에 NG | ZIndexA/B를 DualImageEdgeDistanceMeasurement 자체(기존 TeachingImagePath_Horizontal/Vertical과 같은 자리)에 배치. 잘못된 index(A==B, 존재하지 않는 index)는 DatumRef 미해결 패턴처럼 실행 시점에 명시적 NG(이유 로그) — 조용한 Shots[0] 폴백 금지 | ✓ |
| Shot에 두고, 저장 시점에 검증 | Shot에 필드를 두고 PropertyGrid 저장 시 미리 검증. 더 안전하지만 UI 검증 로직 추가 필요하고 Shot 구조가 달라짐 | |

**User's choice:** 측정에 두고, 실행 시점에 NG (Recommended)
**Notes:** 초기 질문이 기술 용어(측정/Shot 구분, silent fallback)로 어려워 "무슨말인지모르겠어" 피드백 받음 → SHOT_E5 예시로 재설명("가로축/세로축 사진 경로 칸 옆에 A는 몇번 Z, B는 몇번 Z 적는 칸 2개 추가") 후 "응그렇게해"로 확정. 이 답변에 이어 사용자가 자발적으로 Side 쪽 Datum dual-image도 포함 여부를 질문 — 다음 영역(스코프 경계)으로 자연스럽게 연결됨.

---

## 스코프 경계 — 기존 static 파일 경로 + Datum 레벨 포함 여부

| Option | Description | Selected |
|--------|-------------|----------|
| Datum 포함: 네, 같이 포함 | DatumConfig(VerticalTwoHorizontalDualImage, Side/Bottom 사용 중 — Phase 37 확인)도 이번 phase에 ZIndexA/B + 라이브 캡처 적용. 측정과 근본원인 동일(고정 static 파일 재사용) | ✓ |
| Datum 포함: 아니요, 측정만 먼저 | 이번 phase는 FAI 측정 레벨만, Datum은 별도 phase로 이연 | |
| 레거시/버그: 기존레시피 그대로 + 버그도 같이 수정 | ZIndexA/B 미설정 시 기존 static 파일 경로 100% 유지(회귀 0). 추가로 발견된 버그(측정 imageA가 라이브 grab 이미지 무시하고 항상 파일 재로드, Action_FAIMeasurement.cs TryGrabOrLoadFaiDualImages/TryExecuteMeasurement)도 같이 수정 — 어차피 같은 코드를 만지는 작업 | ✓ |
| 레거시/버그: 레시피는 그대로, 버그는 따로 | ZIndexA/B 기능만 이번에, 발견된 버그는 별도 디버그 세션으로 분리 | |

**User's choice:** Datum 포함(네) + 레거시 유지·버그도 같이 수정
**Notes:** 사용자가 먼저 "side 쪽도 측정말고 datum도 두개 이미지 찍는거있는데 이것도 반영하는거지?"라고 자발적으로 질문 — 이미 논의 예정이던 스코프 경계 질문과 정확히 일치해서 바로 이어서 진행. 두 하위 질문 모두 추천안(Recommended) 그대로 채택.

---

## Claude's Discretion

- 사이클 공유 저장소의 정확한 자료구조/필드명 (헝가리언 표기법 준수 하에)
- `StartSubset` 배선 위치 (`Custom/SystemHandler.ProcessTest` vs `SequenceHandler`)
- 신규 `SkipReason` 상수명
- ZIndexA/B INI 직렬화 기본값(sentinel) 및 Load 오버라이드 필요 여부
- 측정/Datum 두 곳의 ZIndexA/B를 공유 헬퍼로 추출할지 여부

## Deferred Ideas

- `ApplyPrepToSequences` 결정론적 결함(동일 nZIndex TOP/BOTTOM 충돌) — 이미 `shared-lighthandler-race.md`에 기록, 별도 세션
- v1.0 NG 누적 index-0-only 리셋 취약점 — 별도 MEDIUM 리스크 항목(`auto-mode-risk-audit-260721`), 이번 phase는 그 위에 편승만 함
- PROTO-06 통신 회귀 시험 — Phase 50, 제어팀 동기화 후
- legacy `$LIGHT`/수동 UI grab/`ProcessAlignTest` 레이스 — 계속 범위 밖
