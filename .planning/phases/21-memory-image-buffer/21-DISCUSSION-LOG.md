# Phase 21: 메모리 이미지 버퍼 — Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-10
**Phase:** 21-memory-image-buffer
**Areas discussed:** 버퍼 범위, Lifetime 트리거, API 표면, AC#1 디버그 뷰 경로

---

## 영역 선택 (Gray Area Selection)

| Option | Description | Selected |
|--------|-------------|----------|
| 버퍼 범위 (대상 캡쳐 액션) | Inspection-only / Top+Bottom 통일 / Datum 단계 포함 — 셋 중 결정 | ✓ |
| Lifetime 트리거 (명시 해제 시점) | 레시피 변경 / 시퀀스 Reset / 검사 시작 / shutdown — 어느 이벤트에서 해제 | ✓ |
| API 표면 / 추상화 | 기존 ShotConfig 메서드 유지 vs ImageBuffer 클래스 vs IImageBuffer 인터페이스 | ✓ |
| AC#1 디버그 뷰 경로 | 기존 DisplayShotImage 재사용 vs 신규 디버그 뷰 vs MainResultViewerControl 플래그 | ✓ |

**User's choice:** 4 영역 모두 multiSelect

---

## Area 1: 버퍼 범위 (대상 캡쳐 액션)

| Option | Description | Selected |
|--------|-------------|----------|
| Inspection Shot 만 유지 (권장) | 현 패턴 공식화. ShotConfig._image 가 유일한 메모리 버퍼. TopInspection / Datum 단계는 무관. 스코프 최소화. | ✓ |
| Inspection + TopInspection 통일 | TopInspectionParam._latestHalconImage 도 동일 계약 적용. 일관성 개선, 추가 테스트 필요. | |
| Inspection + Datum 캡쳐도 포함 | GrabOrLoadDatumImage 의 datumImage 도 ShotConfig 에 보관 (현재 finally Dispose). 재조회 가능하지만 중복 가능성. | |

**User's choice:** Inspection Shot 만 유지 (권장)
**Notes:** Phase 26 헝가리안 리팩토링과 충돌 회피 + 스코프 최소화 정당화 → D-01 로 잠금.

---

## Area 2: Lifetime 트리거 (명시 해제 시점)

| Option | Description | Selected |
|--------|-------------|----------|
| OnRecipeChanged subscriber 명시 추가 (권장) | SequenceHandler.OnRecipeChanged 에 RecipeManager.ClearShots 호출 subscriber 추가. Sequence reset 은 EStep.Init 경로 유지 + 마킹. App shutdown 은 SystemHandler.Release() 명시 호출. | ✓ |
| 문서화만 + 기존 경로 유지 | 코드 수정 없이 ShotConfig.ClearImage / RecipeManager.ClearShots 의 XML doc 에 "recipe 변경 시점에 보장" 명시 + 골드명 도식. AC#3 충족 근거. | |
| 전용 BufferLifecycleManager 신규 | ImageBufferManager 싱글턴 도입 + 레시피/리셋/shutdown 3 채널 모두 hook. 원스탑 제어 명확하지만 Phase 21 범위 초과. | |

**User's choice:** OnRecipeChanged subscriber 명시 추가 (권장)
**Notes:** 3 채널 (recipe / sequence reset / shutdown) 모두 D-02 에 명시. Wire 위치는 planner 결정 (D-03).

---

## Area 3: 버퍼 API 추상화 형태

| Option | Description | Selected |
|--------|-------------|----------|
| 기존 ShotConfig 메서드 유지 + XML doc 강화 (권장) | 신규 클래스/인터페이스 0. SetImage/GetImage/ClearImage 의 수명 계약 XML doc 추가. AC#3 "코드 주석 또는 명시 함수" 에 주석 경로로 충족. | ✓ |
| ImageBuffer 클래스 추출 | ShotConfig._image + lock 을 별도 ImageBuffer 클래스로 분리. ShotConfig 는 소유 + 대리 호출. Phase 26 리팩토링과 충돌 가능. | |
| IImageBuffer 인터페이스 도입 | 인터페이스 추상 + ShotConfig 구현. 테스트 mock 용이하지만 Phase 21 계약 (단일 구현) 대비 과잉. | |

**User's choice:** 기존 ShotConfig 메서드 유지 + XML doc 강화 (권장)
**Notes:** D-05 신규 추상화 0. D-06 XML doc 강화 대상 6 멤버 명시.

---

## Area 4: AC#1 충족 방식

| Option | Description | Selected |
|--------|-------------|----------|
| 기존 DisplayShotImage 재사용 + 명시 검증 (권장) | FAI 트리 클릭→DisplayShotImage→ShotConfig.GetImage 경로가 이미 디스크 거치지 않음. SIMUL UAT 에서 fileio API 부재 명시 검증. UI 신규 0. | ✓ |
| 전용 디버그 뷰 신규 추가 | 메뉴에 "Buffer Inspector" 창 추가 — RecipeManager.Shots 순회하며 HasImage / 메모리 상태 표시. Phase 25 OUT-01 와 중복 우려. | |
| MainResultViewerControl 에 메모리 표시 플래그 | 기존 메인 뷰어에 "Memory / Disk" 토글 추가 — 현재 소스 시각적 구분. 명세 논의 추가 필요. | |

**User's choice:** 기존 DisplayShotImage 재사용 + 명시 검증 (권장)
**Notes:** D-07 UI 신규 0. D-08 입증 도구 (Process Monitor / grep / 둘 다) 는 planner 결정.

---

## Area 5: 추가 논의 영역

| Option | Description | Selected |
|--------|-------------|----------|
| 충분 — CONTEXT.md 작성 진행 (권장) | 4 결정으로 계획 충분. 구현 세부 (subscriber 위치, XML doc 명세, 검증 도구) 는 planner 위임. | ✓ |
| AC#2 누수 검증 방식 | GC.GetTotalMemory 차이 / HImage 필드 null 확인 / 메모리 프로파일러 — planner 결정으로 위임 | |
| Sequence Reset 의 정의 | SequenceBase 에 명시 OnReset 훅 추가 여부 — D-04 로 Phase 21 거부 | |
| Phase 25 OUT-01 경계 명문화 | Phase 21 메모리 vs Phase 25 디스크 경로 소유주/트리거 경계를 CONTEXT 에 포함 | |

**User's choice:** 충분 — CONTEXT.md 작성 진행 (권장)
**Notes:** AC#2 검증 도구는 D-11 에 우선순위만 명시 (planner 결정). Phase 25 경계는 deferred 섹션에 포함.

---

## Claude's Discretion

- subscriber wire-up 정확한 위치 (Custom/SystemHandler.cs Initialize / MainWindow Loaded / SequenceHandler 자체 — D-03)
- XML doc 문장의 실제 표현 (영문/한글 혼용, summary/remarks 분리 등)
- AC#2 dispose 입증 단위 시퀀스 횟수 (5 vs 10) 와 검증 위치 (UAT 수동 vs SIMUL 자동 — D-11)
- subscriber 등록/해제 lifecycle 보호 (App shutdown unsubscribe)

## Deferred Ideas

- Phase 26 헝가리안 리팩토링 시 ImageBuffer / IImageBuffer 추상화 재검토
- TopInspectionParam._latestHalconImage 통일 (carry-over 후보)
- Datum 단계 datumImage 의 ShotConfig 패턴 적용
- SequenceBase.OnReset 명시 hook 도입
- Phase 25 OUT-01 결과 이미지 리뷰어 시 Phase 21 메모리 경로 fallback 활용 가능성
