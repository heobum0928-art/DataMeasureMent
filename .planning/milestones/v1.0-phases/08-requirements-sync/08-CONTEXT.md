# Phase 8: 요구사항 & 트레이서빌리티 동기화 - Context

**Gathered:** 2026-04-23
**Status:** Ready for planning

<domain>
## Phase Boundary

REQUIREMENTS.md 문서만 수정하는 doc-only 동기화 작업. 구체적으로:

1. **정의 추가** — REQUIREMENTS.md v1 Requirements 본문에 "Rapid City 확장" 섹션 신설, RC-01..RC-06 정의를 한 줄 요약 형식으로 기재
2. **Traceability 표 정합화** — Phase 2/3/4/5/6 실제 상태와 Status 값을 맞춤 (Pending → Complete 다수)
3. **Coverage 주석 갱신** — 총계/구성 ID 나열을 현행 상태와 일치시킴

Out-of-scope:
- 코드 변경 (WPF_Example/ 무관)
- Phase 9 몫인 VERIFICATION.md 생성
- 신규 요구사항 추가
- v2 요구사항(TCH-03/04, UI-06/07) 조정
</domain>

<decisions>
## Implementation Decisions

### RC 정의 본문 형식
- **D-01:** RC-01..RC-06 본문은 기존 UI/TCH/ALG/SEQ와 동일한 **한 줄 요약 스타일**로 기재. 내용은 `.planning/phases/06-rapid-city/06-RESEARCH.md` L90-95의 한 줄 요약을 기반으로 문서 톤에 맞게 경량 리라이트. Acceptance criteria/Gap 메타는 포함하지 않음 — 기존 문서 경량성 유지.

### RC 체크박스 / Traceability Status
- **D-02:** RC-01..RC-06 체크박스는 **[x]**, Traceability Status는 **"Complete"**. Phase 6 구현+UAT(quick 260417-kzd)가 완료됐고, 기존 Phase 1/3도 VERIFICATION.md 없이 Complete 상태인 패턴과 일치. Phase 9에서 VERIFICATION 문서를 작성해도 Status 변경은 없음(이미 Complete).

### Phase 2/3/5 Status 정합화
- **D-03:** Phase 2(TCH-01, TCH-02, ALG-03) 세 건 모두 **"Complete"**로 갱신. Phase 2 UAT FAI 저장 버그는 별도 quick 토도 경로로 이관됐고, 요구사항 트레이서빌리티 오염을 피하기 위해 Complete로 표기. Phase 1/3과 동일 기준 적용.
- **D-04:** Phase 5(SEQ-01..SEQ-04) 네 건 모두 **"Complete"**. Shot-FAI 데이터 모델 + Action_FAIMeasurement 루프 완성으로 요구사항 충족.
- **D-05:** ALG-04 Traceability 행의 표기는 **"Phase 3 → Phase 7 (gap closure)"** 문자열 유지하되 Status를 **"Complete"**로 갱신. Phase 7 Plan 02가 오늘(2026-04-23) SIMUL_MODE UAT 통과.

### 섹션 배치 / Coverage
- **D-06:** "Rapid City 확장" 섹션은 기존 **"검사 시퀀스"** 섹션 **다음**에 독립 섹션으로 신설. v1 Requirements 그룹 안에, v2 Requirements 앞에.
- **D-07:** Coverage 주석은 기존 포맷(단순 총합 + 구성 ID 나열) 유지. 총계 표기는 "22 total (UI-01..UI-05, TCH-01..TCH-02, ALG-01..ALG-05, SEQ-01..SEQ-04, RC-01..RC-06)" 유지 — 이미 현재 REQUIREMENTS.md L90의 22 카운트는 RC 포함 계산된 상태이므로 큰 수 변경은 없음. 하단 주석 "RC-01..RC-06은 Phase 6에서 구현되었으나 본 문서 정의/본문 체크박스 등록은 Phase 8..." 문장은 Phase 8 완료로 의미가 없어지므로 **삭제**.
- **D-08:** Last-updated 꼬리표 갱신: "2026-04-23 — Phase 8: RC-01..RC-06 정의 등록 + traceability 정합화".

### Claude's Discretion
- RC-01..RC-06 한 줄 정의의 정확한 한국어 표현(RESEARCH.md 문장을 직접 복사할지, 약간 리라이트할지) — planner 재량.
- 섹션 제목 정확 표현("Rapid City 확장" / "Rapid City 확장 (Phase 6)") — planner 재량.
- Traceability 표 내 RC 행의 "Phase" 열 표기를 "Phase 6 (등록은 Phase 8)" → "Phase 6"으로 단순화할지 여부 — planner 재량(Phase 8 완료 시점에서는 "등록은 Phase 8" 꼬리말이 의미 없음).
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 기준 문서 (무엇을 바꿀 것인가)
- `.planning/REQUIREMENTS.md` — 수정 대상 유일 파일. 현재 97 lines. Traceability 표 L60-87, Coverage L89-93, 하단 주석 L95-97.

### 상태 판단 근거 (무엇을 Complete로 볼 것인가)
- `.planning/v1.0-MILESTONE-AUDIT.md` — 2026-04-22 감사 결과. requirements 10/22 satisfied + 6 partial + 6 orphaned. RC-01..RC-06 orphaned 판정 근거.
- `.planning/phases/06-rapid-city/06-RESEARCH.md` §L90-95 — RC-01..RC-06 한 줄 정의 원전 (D-01 기반).
- `.planning/phases/06-rapid-city/06-02-SUMMARY.md` — RC-01/RC-02/RC-04 완료 증거.
- `.planning/phases/06-rapid-city/06-03-SUMMARY.md` — RC-05 완료 증거.
- `.planning/phases/06-rapid-city/06-04-SUMMARY.md` — RC-06 완료 증거 (+ quick 260417-kzd UAT 통과).
- `.planning/phases/07-overlay-regression-fix/07-02-SUMMARY.md` — ALG-04 완료 증거 (Phase 3 → Phase 7 gap closure, 2026-04-23 UAT 사인오프).
- `.planning/STATE.md` — 현재 phase 진행률 및 진행 중 결정 로그.

### 로드맵 컨텍스트
- `.planning/ROADMAP.md` §Phase 8 — Goal/Requirements/Gap Closure/Success Criteria 정의.
- `.planning/PROJECT.md` — Core Value 및 제약(문서 톤 일관성).
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- 해당 없음 (코드 변경 없는 doc-only 작업).

### Established Patterns
- REQUIREMENTS.md 기존 포맷: `- [x] **ID**: 한 줄 설명` — 유지 (D-01).
- Traceability 표 포맷: `| Requirement | Phase | Status |` 3열 — 유지.
- Coverage 주석 포맷: "v1 requirements: N total (ID 나열) / Mapped to phases: N / Unmapped: N" — 유지 (D-07).

### Integration Points
- 해당 없음 (단일 파일 수정).
</code_context>

<specifics>
## Specific Ideas

- Phase 6 RESEARCH.md L90-95의 RC 한 줄 정의는 "어떻게 구현하는가"에 가까운 기술 설명 — REQUIREMENTS.md 톤("무엇을 보장하는가")에 맞게 경량 리라이트 필요할 수 있음. 예: "InspectionSequence에 DatumConfigs 프로퍼티 추가. SequenceBase.Name은 private set이므로..." → "Sequence가 Fixture(한 면)로 동작하며 자신의 DatumConfig 목록을 소유한다" (원문 첫 문장만 발췌해도 충분).
- Traceability L82-87의 RC 행 "Phase" 열 표기 "Phase 6 (등록은 Phase 8)"은 Phase 8 완료 후 의미 없어짐 — "Phase 6"으로 단순화 권장(Claude 재량, D-07 참고).
</specifics>

<deferred>
## Deferred Ideas

- **Phase별 Status breakdown 테이블** — Coverage 아래에 Phase 1~6별 Complete/Pending 건수 요약 테이블. 감사 편의성 ↑이지만 문서 경량성 해침. Phase 9 이후 문서 고도화 필요 시 재검토.
- **Gap Closure 메타 (Gap G1/G2 ID)** — RC 정의에 "Gap Closure: G1" 문자열 부착. 감사 트레이서빌리티 강화되지만 현 문서 톤과 어긋남. v2 요구사항/감사 포맷 정립 시 재검토.
- **Acceptance Criteria 줄** — RC 정의 하단에 "Verify: ..." 검증 포인트 추가. Phase 9 VERIFICATION.md 작성이 확정된 지금은 중복.
- **Phase 2 FAI 저장 버그 추적** — project_phase2_bugs.md 메모리에 기록된 미해결 버그는 별도 quick 토도/debug 세션으로 처리. REQUIREMENTS.md Status에는 반영하지 않음(D-03).

### Reviewed Todos (not folded)
없음 — cross-reference 토도 매칭 수행하지 않음(doc-only 경량 phase).
</deferred>

---

*Phase: 08-requirements-sync*
*Context gathered: 2026-04-23*
