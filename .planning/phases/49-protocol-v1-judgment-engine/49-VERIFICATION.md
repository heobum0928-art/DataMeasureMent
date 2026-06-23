---
phase: 49-protocol-v1-judgment-engine
verified: 2026-06-23T00:00:00Z
status: human_needed
score: 7/7 truths code-verified (WR-01 fixed 2026-06-23 commit bc6252b)
overrides_applied: 0
gaps:
  - truth: "마지막 Index에서 사이클 누적 NG 유무로 정확히 P/F 산출 (오판정 없음)"
    status: resolved
    resolution: "WR-01 fixed 2026-06-23 (commit bc6252b) — nMatchedShots 를 ApplyCycleJudgement 로 전달, 마지막 Index 매칭 0건이면 bEmptyLastScope → F 강제(fail-safe). silent false-PASS 차단. 빌드 PASS. wire 동작은 UAT-7 로 확증 예정."
    reason: "(해소 전) WR-01 — ZIndex 미설정 레시피(전 Shot=0)에서 측정 Index(z>=1) 수신 시 ComputeLastZIndex=0 → bIsLastIndex(1>=0)=true + 매칭 Shot 0건 → m_bCycleHasNG=false → ApplyCycleJudgement 가 'P'(false PASS) 송신. WarnIfEmptyScope 가 PrintErrLog 경고는 남기나 패킷은 PASS 로 Enqueue 됨. 검사 시스템 최악 방향(silent false-accept)."
    artifacts:
      - path: "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs"
        issue: "BuildScopedResponse/ApplyCycleJudgement (388~391, 495~532) — 빈 scope 마지막 Index 가 P 로 통과. nMatchedShots 가 판정 단계로 전달되지 않아 '전부 OK' 와 '아무것도 측정 안 함' 구분 불가."
    missing:
      - "ApplyCycleJudgement(또는 BuildScopedResponse)에 매칭 Shot 0건 && 마지막 Index 면 P 금지(F 강제 또는 B 유지) 분기 추가 — nMatchedShots 를 판정 단계로 전달."
deferred: []
human_verification:
  - test: "UAT-1: $TEST z_index=0 정상 수신"
    expected: "RESULT:site;B;0; (빈 항목 버퍼 응답) byte 확인"
    why_human: "wire-level RESULT 바이트 직렬화는 실 핸들러/SIMUL 송수신 필요 — 코드 경로만으로 byte 확증 불가"
  - test: "UAT-2: $TEST z_index=0 Datum 검출 실패 (UseProtocolV1=true)"
    expected: "즉시 RESULT:site;F;... (후속 Index 핸들러 주도 skip)"
    why_human: "Datum 실패 상태(_failedDatums)는 실 검사 실행에서만 발생 — 런타임 측정 필요"
  - test: "UAT-3: 중간 Index 에서 NG 발생"
    expected: "응답 B, 사이클 계속 진행(즉시 종료 안 함)"
    why_human: "멀티 Index 사이클 진행은 실 핸들러 시퀀스 송신 필요"
  - test: "UAT-4: 마지막 Index 도달"
    expected: "사이클 누적 NG 있으면 F, 없으면 P 1회"
    why_human: "사이클 누적 상태 + wire 결과는 실행 시나리오 필요"
  - test: "UAT-5: 다음 자재 Index 0 재수신"
    expected: "이전 사이클 NG 미잔류 (ResetCycleState 자동 리셋 확인)"
    why_human: "연속 자재 사이클 전이는 실 핸들러 흐름 필요"
  - test: "UAT-6: UseProtocolV1=false 회귀"
    expected: "v2.6 응답 포맷 무변경"
    why_human: "wire 포맷 동일성은 실 송수신 비교 필요(코드상 v2.6 블록 무변경 확인됨)"
  - test: "UAT-7: ZIndex 미설정(전부 0) 레시피서 측정 Index 1+ 수신"
    expected: "PrintErrLog 경고 로그 + (WR-01 수정 후) P 아닌 안전 응답"
    why_human: "운용 오류 시나리오 — 실행 + 로그 확인 필요. WR-01 갭과 직결."
  - test: "UAT-8(선택): UseProtocolV1=true 한글/UTF-8 메시지 인코딩"
    expected: "Phase 48 baseline 과 동일 인코딩 (CO-48-01 회귀 0)"
    why_human: "인코딩 회귀는 실 송수신 byte 비교 필요"
---

# Phase 49: 제어 프로토콜 v1.0 P/F/B 판정 엔진 Verification Report

**Phase Goal:** 멀티샷 z_index 사이클을 가로질러 무엇을 B/P/F로 채울지 결정하는 상태 엔진 — 중간 Index=B(NG 포함 가능), 마지막 Index에서만 종합 P/F 1회, Datum 샷(z_index=0)=빈 응답·실패 시 즉시 F.
**Verified:** 2026-06-23
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | 중간 Index 응답은 항상 B(IsBuffer=true), NG 있어도 B | ✓ VERIFIED | `ApplyCycleJudgement` (InspectionSequence.cs:514-519) `!bIsLastIndex → IsBuffer=true; Result=OK`. `MapCycleJudgement` (VisionResponsePacket.cs:458-461) IsBuffer 최우선 → `B`. |
| 2 | 마지막 Index(z_index 최댓값)에서만 종합 P/F 1회 | ✓ VERIFIED | `bIsLastIndex = m_nCurrentZIndex >= m_nLastZIndex` (:389) + `ApplyCycleJudgement` 마지막 분기 IsBuffer=false (:522-531). `ComputeLastZIndex` 이 시퀀스 소유 Shot 최댓값(:259-287). |
| 3 | 사이클 중 NG 누적 → 마지막 Index 종합 F 반영 | ✓ VERIFIED | `ClassifyFai` (:476-491) WasDatumSkipped/!IsPass → `m_bCycleHasNG=true`. 멤버 리셋 전까지 유지. `bCycleFail = m_bCycleHasNG \|\| m_bCycleDatumFailed` (:523). |
| 4 | Index 0(Datum) 정상 시 빈 응답 RESULT:site;B;0; | ✓ VERIFIED | `BuildDatumShotResponse` (:327-350) 정상 → IsBuffer=true+OK, FAIResults 비움 → FAICount=0 → 직렬화 `B;0;`. |
| 5 | Index 0 Datum 실패 시 즉시 F (UseProtocolV1 한정, v2.6 N 유지) | ✓ VERIFIED | `bImmediateFail = bUseV1 && bDatumFailed` (:338) → IsBuffer=false+NG → `F`. v2.6 경로(UseProtocolV1=false)는 진입 전 폴백(:100-104), NotExist 'N' 유지. |
| 6 | Index 0 수신 시 사이클 상태 자동 리셋 | ✓ VERIFIED | `HandleDatumIndexResponse` (:396-404) → `ResetCycleState()` (:290-296) 4 멤버 0/false + 직후 `m_nLastZIndex` 재산출. D-08 사이클 시작 리셋. |
| 7 | 마지막 Index 정확 P/F 산출 (오판정 없음) | ✗ PARTIAL | WR-01: ZIndex 미설정 레시피 + 측정 Index 수신 → `1>=0=true` + 매칭 0건 → false PASS. PrintErrLog 경고만, 패킷 'P' Enqueue. **inspection 최악 방향(silent false-accept).** |

**보조 (PROTO-05 enum / CO-48-01):**

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 8 | ECycleResult { Buffer, Pass, Fail } enum 존재 | ✓ VERIFIED | VisionResponsePacket.cs:34-38. **단 IN-01: 선언만, 미소비(dead code).** PROTO-05 "enum 신설" 형식 요구는 충족하나 엔진은 IsBuffer+EVisionResultType 사용. |
| 9 | CO-48-01: EncodingType static→instance, 인코딩 회귀 0 | ✓ VERIFIED | TcpServer.cs:78 instance 필드, :83 instance 메서드, :162/182 `Parent.EncodingType`. VisionServer ApplyEncoding(Utf8) 무변경. |

**Score:** 6/7 핵심 truths 코드 검증 (1 partial — WR-01). 보조 2/2 (IN-01 caveat).

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `WPF_Example/TcpServer/VisionResponsePacket.cs` | ECycleResult enum + MapCycleJudgement 소비 | ⚠️ ORPHANED(enum) | enum 존재(:34-38)이나 미참조(IN-01). MapCycleJudgement(:456-471)는 IsBuffer/Result 정확 소비 — 직렬화 계약 충족. |
| `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` | ZIndex 멤버(default 0) | ✓ VERIFIED | :58 `public int ZIndex { get; set; } = 0` + 엣지케이스 주석. ParamBase 자동 직렬화. |
| `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` | 사이클 멤버 4 + 헬퍼 + AddResponseV1Cycle 엔진 | ✓ VERIFIED | 멤버 4(:66-70), ComputeLastZIndex/ResetCycleState(:259/290), AddResponseV1Cycle 11 메서드(:377~560). |
| `WPF_Example/TcpServer/TcpServer.cs` | EncodingType instance 화 | ✓ VERIFIED | :78/83/162/182. static 제거 + Parent 한정 2곳. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| AddResponse | AddResponseV1Cycle | UseProtocolV1 게이트 | ✓ WIRED | :96-104 — v1.0 위임 후 return, v2.6 폴백 무변경. |
| AddResponseV1Cycle | ShotConfig.ZIndex | AggregateIndexFais 필터 | ✓ WIRED | :443 `shot.ZIndex == nZIndex` + OwnerSequenceName 한정. |
| AddResponseV1Cycle | TestResultPacket.IsBuffer | ApplyCycleJudgement | ✓ WIRED | :517/522 IsBuffer true/false 양쪽 설정. |
| ApplyCycleJudgement | MapCycleJudgement(48-03) | IsBuffer 최우선 직렬화 | ✓ WIRED | VisionResponsePacket.cs:458 IsBuffer→B, :464 OK→P, :470→F. |
| RequestPacket.TestID | m_nCurrentZIndex | ParseCurrentZIndex (int.TryParse) | ✓ WIRED | :380 파싱, TryParse + >=0 가드. |
| ResetCycleState | m_nLastZIndex 재산출 | HandleDatumIndexResponse 호출부 의무 | ✓ WIRED | :398-400 — 리셋 직후 ComputeLastZIndex 재산출(IN-02 brittle but currently correct). |
| DetectDatumFailure | _failedDatums | IsDatumFailed | ✓ WIRED | :354-371 DatumConfigs 순회 + IsDatumFailed. |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| AddResponseV1Cycle | packet.FAIResults | AggregateIndexFais → shot.FAIList (recipeManager.Shots) | 레시피 ZIndex 설정 시 ✓ / 미설정 시 빈 | ⚠️ STATIC(미설정 레시피) — 정상 레시피는 FLOWING. WR-01 의 근본. |
| BuildDatumShotResponse | m_bCycleDatumFailed | DetectDatumFailure → IsDatumFailed(_failedDatums) | Action_FAIMeasurement 가 런타임 채움 | ✓ FLOWING (런타임) |

### Behavioral Spot-Checks

WinExe / TCP 서버 / 실 핸들러 의존 → 정적 spot-check 부적합. 빌드 산출물만 확인:

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| 빌드 산출물 존재 | ls bin/x64/Debug/DatumMeasurement.exe | 1493504 bytes, 09:43 (49-02 commit 이후) | ✓ PASS |
| 신규 삼항/null병합 0건 | 코드 인스펙션 (D-10) | if/else only — 신규 0건 | ✓ PASS |
| v2.6 폴백 보존 | grep anyDatumSkip | 7건 (>=4 임계) | ✓ PASS |
| 모든 phase commit 존재 | git log | 653bc92/9d547da/e41fdc3/8e97aa3/c12f4d3 모두 존재 | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| PROTO-03 | 49-02 | P/F/B 3-state 엔진 (NG 즉시종료 금지, 마지막까지 진행) | ✓ SATISFIED (code) / ? UAT | ApplyCycleJudgement + 불변식 구현. WR-01 한 edge 제외. wire 결과 UAT-3/4 human. |
| PROTO-04 | 49-02 | Datum 빈응답 + 즉시 F | ✓ SATISFIED (code) / ? UAT | BuildDatumShotResponse. UAT-1/2 human. |
| PROTO-05 | 49-01/02/03 | CycleState/ECycleResult enum + NG mark + 자동 리셋 + CO-48-01 | ⚠️ PARTIAL | ECycleResult 존재하나 미소비(IN-01). NG 누적/리셋 구현. ROADMAP `CycleState` enum 은 D-07 의도적 미도입(멤버 bool 대체). CO-48-01 종결. |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| InspectionSequence.cs | 388-391, 495-532 | 빈 scope 마지막 Index → false PASS | ⚠️ Warning (WR-01) | 미설정 레시피서 silent false-accept. 경고 로그는 남으나 'P' 송신. |
| VisionResponsePacket.cs | 34-38 | ECycleResult 선언만, 미참조 | ℹ️ Info (IN-01) | dead code — 향후 유지보수 오해 소지. PROTO-05 형식 요구는 충족. |
| InspectionSequence.cs | 290-296 | ResetCycleState 외부 재산출 계약(comment-only) | ℹ️ Info (IN-02) | 현재 단일 호출부는 정확. 2번째 호출부가 누락하면 WR-01 경로 유발. |
| InspectionSequence.cs | 540 | IndexNumber sentinel `-1` 하드코딩 | ℹ️ Info (WR-03) | SENTINEL_NO_MATERIAL 미참조. 기존 :160 smell 전파. |

### Human Verification Required

UAT 8건 (frontmatter `human_verification` 참조). 핵심: UAT-1~5 (B/P/F/Datum/리셋 wire 동작), UAT-6 (v2.6 회귀), UAT-7 (WR-01 미설정 레시피 — 갭 직결), UAT-8 (인코딩 회귀). 49-HUMAN-UAT.md 미생성 — UAT 시나리오는 49-02-SUMMARY 에 기록됨.

### Gaps Summary

엔진의 정상 경로(레시피 ZIndex 정상 설정)는 코드 레벨에서 goal 을 완전히 달성한다 — 중간 B / 마지막 종합 P/F / Datum 빈응답·즉시F / 자동 리셋 모두 구현·배선되었고 빌드 PASS, v2.6 회귀 0.

단 **WR-01 (truth 7 partial)** 1건이 미해결 상태로 남는다: ZIndex 미설정 레시피(전 Shot=0)에서 측정 Index(z>=1) 수신 시 `ComputeLastZIndex=0` → `1>=0=true` 로 마지막 Index 오인 + 매칭 0건 → 누적 NG 없음 → 종합 `P`(false PASS)를 핸들러에 송신한다. WarnIfEmptyScope 가 PrintErrLog 경고는 남기지만(D-01/BLOCKER 1 결정 = warning-only) **응답 자체는 PASS 로 Enqueue** 되어, 검사 시스템에서 가장 위험한 방향(측정 0건인데 합격 통보)으로 빠진다. 플랜의 BLOCKER 1 결정은 "조용한 빈 B 금지(경고 추가)"였으나, 그 결정은 *중간 Index 빈 B* 가시화에 초점이 있었고 *마지막 Index false-PASS* edge 까지는 닫지 못했다.

수정 권고(코드리뷰 WR-01 fix): `nMatchedShots` 를 `ApplyCycleJudgement` 로 전달하여, 마지막 Index 에서 매칭 0건이면 `P` 금지(F 강제 또는 B 유지). 운용상 모든 레시피에 ZIndex 를 설정하면 이 경로는 발생하지 않으므로, "documented edge + 수정" 또는 "override 수용" 중 개발자 판단 필요.

부수 항목 IN-01(ECycleResult dead code) / IN-02(reset 계약 brittle) / WR-03(-1 sentinel)은 비차단 — 향후 정리 후보.

**상태 판정:** WR-01 은 정상 레시피에서는 goal 을 막지 않으나 오설정 시 inspection-critical false-accept 를 유발하는 partial 이며, 동시에 핵심 acceptance(중간 B / 마지막 P/F / Datum / 리셋 / 인코딩 회귀) 전부가 wire-level UAT (human) 를 요구한다. 따라서 status = **human_needed** (UAT 미수행 + WR-01 개발자 결정 대기). WR-01 을 정식 gap 으로 frontmatter 에 구조화하여 `/gsd-plan-phase --gaps` 또는 override 경로로 처리 가능.

---

_Verified: 2026-06-23_
_Verifier: Claude (gsd-verifier)_
