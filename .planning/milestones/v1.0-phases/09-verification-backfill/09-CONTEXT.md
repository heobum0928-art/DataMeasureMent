---
phase: 09-verification-backfill
status: ready-for-planning
gathered: 2026-04-23
milestone: v1.0
gap_closure: [G3, G4, G5, G7]
---

# Phase 9: VERIFICATION 문서 보강 — Context

**Gathered:** 2026-04-23
**Status:** Ready for planning

<domain>
## Phase Boundary

감사 Gap G3/G4/G5/G7을 해소할 **문서 산출물만** 생성한다:

- 신설 VERIFICATION.md 3건 — `01-VERIFICATION.md`, `03-VERIFICATION.md`, `06-VERIFICATION.md`
- 기존 human_needed UAT 사인오프 2건 — Phase 2 (5 tests) + Phase 5 (4 tests)

**In scope:**
- Observable Truths 검증 (코드/파일 grep 기반)
- Requirements Coverage 갱신 (Phase 8에서 Complete된 상태 반영)
- Human UAT 사인오프 기록 (2026-04-23 user-confirmed)
- Phase 6에 quick 260417-kzd UAT 결과 + Phase 7-02 regression 복구 통합 명시
- Phase 3에 ALG-04 Phase 7 복구 이력 명시

**Out of scope (다른 phase로 이관):**
- 새로운 코드 수정 — Phase 10 (Datum 결함) 또는 별도 phase
- 남아있는 tech_debt (WR-01, WR-03, WR-05) — Phase 10 범위
- Phase 6 Runtime lighting 미연결 건 — backlog 또는 v2

</domain>

<decisions>
## Implementation Decisions

### Plan 분할 전략

- **D-01:** 5개 plan으로 분할한다. 각 plan = 단일 산출물.
  - `09-01-PLAN.md` — 01-VERIFICATION.md 신설 (UI-01..UI-05)
  - `09-02-PLAN.md` — 03-VERIFICATION.md 신설 (ALG-01/ALG-02/ALG-04)
  - `09-03-PLAN.md` — 06-VERIFICATION.md 신설 (RC-01..RC-06 + quick UAT + Phase 7 복구 이력)
  - `09-04-PLAN.md` — 02-HUMAN-UAT.md 사인오프 (5 tests)
  - `09-05-PLAN.md` — 05-HUMAN-UAT.md 신설 및 사인오프 (4 tests, 기존 파일 없음 → 신설)
  - 이유: 각 산출물이 독립 파일 = 병렬 실행 가능, 감사 gap별 추적성 명확.

### Wave 구성

- **D-02:** 전체 5개 plan을 Wave 1에 배치하고 병렬 실행한다. 각 plan은 다른 파일을 생성하므로 충돌 없음. worktree parallelization=true 활용.

### UAT 사인오프 기록 위치

- **D-03:** 기존 UAT 파일에 직접 사인오프 result를 채운다.
  - `02-HUMAN-UAT.md`: 기존 존재. 5개 test의 `result: [pending]` → `result: PASS (2026-04-23 user-confirmed)` 형식으로 업데이트. frontmatter `status: partial` → `status: signed_off`, `updated: 2026-04-23` 갱신.
  - `05-HUMAN-UAT.md`: **기존 파일 없음**. 05-VERIFICATION.md frontmatter `human_verification` 4개 항목을 기반으로 신규 작성 + 사인오프. 02-HUMAN-UAT.md와 동일 포맷 따름.
  - 이유: UAT 이력이 원전 파일에 직접 남아 추적 용이. 별도 SIGNOFF 파일 남기지 않아 파일 수 최소화.

### VERIFICATION.md 포맷

- **D-04:** 기존 02-VERIFICATION.md / 05-VERIFICATION.md 구조를 그대로 복제한다:
  - frontmatter: `phase`, `verified`, `status` (human_needed 또는 verified), `score`, `re_verification`, `human_verification[]` (필요 시)
  - 본문: `## Goal Achievement` → `### Observable Truths` (표) → `### Required Artifacts` (표) → `### Key Link Verification` (표) → `### Data-Flow Trace` (선택) → `### Requirements Coverage` (표) → `### Human Verification Required` (있으면) → `### Gaps Summary`
  - 코드 증거는 실제 grep/Read로 확보 — "파일:line" 또는 "메서드/필드명" 인용.

### Phase 6 VERIFICATION 통합 범위

- **D-05:** `06-VERIFICATION.md`는 세 가지를 통합한다:
  1. **RC-01..RC-06 Observable Truths** — Phase 6 goal 검증 (Sequence/Datum 승격, MeasurementBase 6종, 조명 INI, 새 INI 포맷, 4계층 UI 트리).
  2. **quick 260417-kzd UAT 결과** — 2026-04-22 user-approved (DisplayName UI + Shot 실행 경로 매핑/지연 동기화). frontmatter `quick_refs: ["260417-kzd"]` + 본문에 통합 기록.
  3. **Phase 7 regression 복구 이력** — Gap I1 (Action_FAIMeasurement.cs:190 overlay clear) → Phase 7-02에서 per-Measurement overlay 누적 구조로 해소됨을 명시. `status: verified` (blocker 해소) + Notes 섹션에 타임라인 기록.
  - frontmatter `gap_closure: [I1]` 포함.

### Phase 3 ALG-04 복구 이력 반영

- **D-06:** `03-VERIFICATION.md`는 ALG-01/ALG-02/ALG-04 세 건을 모두 satisfied로 기록하되, ALG-04에는 복구 이력을 명시한다:
  - Requirements Coverage 표의 ALG-04 Evidence 열: `"03-02에서 구현 → 06-01 Measure 루프에서 regression 발생 → 07-02에서 per-Measurement overlay 누적 구조로 복구. 최종 상태: InspectionOverlays가 Measurement별로 AddRange되고 SIMUL_MODE 육안 UAT 통과 (07-02-SUMMARY.md)"`
  - frontmatter `cross_phase_refs: ["07-02-SUMMARY.md"]` 포함.
  - status: `verified` (최종 상태 기준).

### 코드 변경 0건 원칙

- **D-07:** 본 phase는 문서 산출물만 다룬다. `WPF_Example/`, `Test/`, `Setting/` 하위 어떤 파일도 touch 하지 않는다. 검증 과정에서 발견되는 새로운 결함은 **기록만 하고** Phase 10 또는 backlog로 이관한다.

### Claude's Discretion

- 각 VERIFICATION.md의 Observable Truth 개수와 Key Link 개수는 플랜 시점에 phase 내용에 맞춰 결정 (plan-phase에서 구체화).
- Phase 1의 Human Verification 섹션 필요 여부는 researcher가 Phase 1 UI 범위를 scout 후 결정 (이미 UI-01..UI-05가 [x]로 표시되어 있으므로 대부분 코드 기반 검증 가능할 것으로 예상).
- 커밋 메시지 포맷은 Phase 8과 동일한 `docs(09-0X): ...` 패턴 사용.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 감사/목표 산출물 기준

- `.planning/v1.0-MILESTONE-AUDIT.md` §Key Gaps — G3/G4/G5/G7 해소 대상 정의
- `.planning/v1.0-MILESTONE-AUDIT.md` §Requirements Coverage — 각 phase의 REQ 매핑 최종본
- `.planning/REQUIREMENTS.md` (Phase 8 동기화 완료본) — 22개 v1 요구사항 + Complete 상태 기준

### 포맷 참조 (그대로 복제)

- `.planning/phases/02-teaching-calibration/02-VERIFICATION.md` — VERIFICATION.md 포맷 원본 (9/9 truths, Key Link 7행, Human Verification 5건)
- `.planning/phases/05-tcp/05-VERIFICATION.md` — human_verification frontmatter 사용 예시
- `.planning/phases/04-datum/04-VERIFICATION.md` — gaps_found 포맷 참조 (안티패턴 섹션 포함)
- `.planning/phases/02-teaching-calibration/02-HUMAN-UAT.md` — UAT 사인오프 파일 포맷 (result/expected 필드 구조)

### Phase 1 (UI) 산출물 추적

- `.planning/phases/01-ui/01-01-PLAN.md`, `01-01-SUMMARY.md`
- `.planning/phases/01-ui/01-02-PLAN.md`, `01-02-SUMMARY.md`
- `.planning/phases/01-ui/01-UAT.md` — 기존 UAT (참조용)

### Phase 3 (Edge Measurement) 산출물 + 복구 이력

- `.planning/phases/03-edge-measurement/03-01-PLAN.md`, `03-01-SUMMARY.md`
- `.planning/phases/03-edge-measurement/03-02-PLAN.md`, `03-02-SUMMARY.md`
- `.planning/phases/03-edge-measurement/03-VALIDATION.md` — 기존 draft (참고만)
- `.planning/phases/03-edge-measurement/03-UAT.md` — 기존 UAT
- `.planning/phases/07-overlay-regression-fix/07-02-SUMMARY.md` — ALG-04 복구 최종 상태 증거

### Phase 6 (Rapid City) 산출물 + UAT

- `.planning/phases/06-rapid-city/06-RESEARCH.md` — RC-01..RC-06 원전
- `.planning/phases/06-rapid-city/06-01-SUMMARY.md` ~ `06-04-SUMMARY.md` — 4개 plan 구현 증거
- `.planning/phases/06-rapid-city/06-VALIDATION.md` — 기존 draft (참고만)
- `.planning/quick/260417-kzd-phase-6-04-uat-displayname-ui-shot/260417-kzd-SUMMARY.md` — quick UAT 결과

### Phase 2/5 UAT 사인오프 기반

- `.planning/phases/02-teaching-calibration/02-VERIFICATION.md` — human_verification 5개 원전
- `.planning/phases/02-teaching-calibration/02-HUMAN-UAT.md` — 업데이트 대상
- `.planning/phases/05-tcp/05-VERIFICATION.md` — human_verification 4개 원전 (05-HUMAN-UAT.md 신설 시 기반)

### Phase 8 완료본 (참조)

- `.planning/phases/08-requirements-sync/08-01-SUMMARY.md` — Traceability/체크박스 최신 상태 근거

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **VERIFICATION.md 포맷**: 02, 04, 05의 세 파일이 안정된 포맷 제공. Observable Truths + Key Link + Requirements Coverage + Human Verification 구조 그대로 복제 가능.
- **UAT 포맷**: 02-HUMAN-UAT.md와 01-UAT.md, 03-UAT.md, 04-UAT.md가 간단한 list/result 구조 제공. 05-HUMAN-UAT.md 신설 시 이 포맷 따름.
- **Code evidence grep 패턴**: 기존 02-VERIFICATION.md는 "file:line" 형태로 증거 제시. 동일 방식 사용 가능.

### Established Patterns

- **Observable Truth 증거 포맷**: `"{메서드/필드명} 확인 ({파일}:line)"` — 간결하고 재현 가능.
- **Key Link "From → To Via"**: 시그니처가 아닌 실제 호출 체인 기술 (예: "Rect ROI button click → StartRectangleDrawing").
- **Requirements Coverage 표**: Requirement / Source Plan / Description / Status / Evidence 5열 구조 고정.
- **human_verification frontmatter 배열**: 각 항목에 `test`, `expected`, `why_human` 세 필드. 사인오프 시 `result`, `signed_off_at` 추가 가능.

### Integration Points

- **REQUIREMENTS.md Traceability** (Phase 8 완료): Phase 9 VERIFICATION.md의 Requirements Coverage는 Traceability 표와 1:1 대응 필요. 불일치 시 플랜에서 지적.
- **ROADMAP.md Progress 표**: Phase 9 완료 시 status: In progress → Complete 변경. executor가 처리.
- **v1.0-MILESTONE-AUDIT.md**: Phase 9 완료 후 감사 재실행(`/gsd-audit-milestone`) 가능 상태. 본 phase 산출물이 재감사 대상.

</code_context>

<specifics>
## Specific Ideas

- **06-VERIFICATION.md는 통합 문서**: RC-01..RC-06 검증 + quick 260417-kzd UAT + Phase 7 regression 복구 기록 + Phase 6 Runtime lighting 미연결(backlog 이관) 네 가지를 모두 다룸. 하나의 phase가 여러 단계를 거쳤음을 명시.
- **ALG-04 복구 이력 표기법**: "3-02 구현 → 06-01 regression → 07-02 fix" 타임라인을 Evidence 열에 직접 기술. 별도 섹션 대신 표 내부에 압축.
- **UAT 사인오프 한국어 마커**: `result: PASS (2026-04-23 user-confirmed)` — 날짜 + 유저 확인 표기로 감사 가능. 각 테스트별 간단한 코멘트(필요 시)는 기존 expected 아래 추가.
- **05-HUMAN-UAT.md 신설 근거**: 05-VERIFICATION.md frontmatter에 human_verification 4항목이 이미 정의되어 있음 → 이를 UAT 파일로 승격.

</specifics>

<deferred>
## Deferred Ideas

- **tech_debt 해소 (WR-01, WR-03, WR-05)**: Phase 10 범위 — Datum 정확성 결함 수정.
- **Phase 6 Runtime lighting 연결 (Ring/Back/Coax/Side brightness)**: v1.0 이후 backlog. 06-VERIFICATION.md에는 "미연결 상태 확인" 으로만 기록.
- **TestResultPacket.FAIResults multi-Measurement 지원**: Phase 5 tech_debt. v2 또는 별도 phase로 이관.
- **Nyquist compliance 전면화 (01/02/04/05 missing)**: 본 phase 범위 밖 — 문서 추가 작업이 별도로 필요.
- **Phase 6 06-VALIDATION.md draft 처리**: 06-VERIFICATION.md가 생성되면 VALIDATION.md는 deprecated 표시 가능하지만, 본 phase 범위 밖. 필요 시 cleanup phase로 이관.

</deferred>

---

*Phase: 09-verification-backfill*
*Context gathered: 2026-04-23*
