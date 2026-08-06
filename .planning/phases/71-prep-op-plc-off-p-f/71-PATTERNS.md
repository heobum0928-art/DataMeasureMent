# Phase 71: $PREP Op 필드 제거 + 조명 소등 자동화 - Pattern Map

**Mapped:** 2026-08-06
**Files analyzed:** 4 (all modifications, no new files)
**Analogs found:** 4 / 4 — all exact matches (3 via direct git-history precedent in this same repo, 1 via self-referential hook site named explicitly in CONTEXT.md)

**Note on line numbers:** CONTEXT.md's line numbers were captured against a slightly earlier revision of the working tree. I re-verified every location against the current file state (as of this mapping) — actual current line numbers differ by roughly +55~+65 lines in `InspectionSequence.cs` (drift from unrelated recent edits) but the referenced functions/logic are otherwise unchanged. Current, verified line numbers are used throughout this document. Planner should treat these as more reliable than CONTEXT.md's, but re-check with Grep before editing since more drift may occur between now and execution.

## File Classification

| Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `WPF_Example/TcpServer/VisionRequestPacket.cs` (`PrepPacket`, `TryParsePrepFields`) | model (request DTO + static field parser) | request-response (TCP field parse) | Same file, `TryParseTestFieldsV1`/`ParseZIndexField` as changed in commit `fbe05c8` (z_index field removal from `$TEST`) | exact — direct precedent, same repo, same kind of change |
| `WPF_Example/Custom/SystemHandler.cs` (`ProcessPrep`, `DebugManualZTrigger`) | controller (TCP command dispatcher / handler) | request-response | Same file, `ProcessPrep`/`ProcessTest` as changed in commit `fbe05c8` | exact — direct precedent, same function even |
| `WPF_Example/TcpServer/VisionResponsePacket.cs` (`PrepAckPacket`, `BuildPrepAckMessage`) | model (response DTO + static serializer) | request-response | Same file, `BuildPrepAckMessage`/`PrepAckPacket` pre-Op state shown in commit `7e8f7c6`'s diff (the "-" side is literally the target end state) | exact — mirror-image precedent |
| `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` (`BuildScopedResponse`, `BuildDatumShotResponse`) | sequence / state-machine hook (cycle-completion side effect) | event-driven (cycle P/F confirmation → light-off side effect) | Same file, `BuildScopedResponse` itself (self-hosting — CONTEXT.md names this exact function as the recommended hook site) | exact — self-referential |

## Pattern Assignments

### `WPF_Example/TcpServer/VisionRequestPacket.cs` (model, request-response)

**Analog:** same file, `TryParseTestFieldsV1` / `ParseZIndexField`, changed in commit `fbe05c8` ("$PREP/$TEST 분리 — LIGHT응답제거 + TEST z_index 제거"). This is the exact precedent for removing an optional trailing TCP field from a parser in this codebase.

**Current state to modify — `PrepPacket` class** (lines 577-585):
```csharp
//260625 hbk Phase 64 LIGHT-01: $PREP 수신 패킷. ZIndex = 조명 세팅 대상 Shot z_index.
//260626 hbk v3.0: Op 추가 — 1=ON(z_index 샷 조명 점등) / 0=OFF(사이클 종료 소등). 미수신 시 1(하위호환).
public class PrepPacket : VisionRequestPacket {
    public int ZIndex { get; set; }
    public int Op { get; set; } = 1;   //260626 hbk 1=ON / 0=OFF (기본 ON = 구 $PREP:site,z_index@ 호환)

    public PrepPacket() : base(VisionRequestType.Prep) {
    }
}
```
Target shape (mirrors the pre-v3.0 comment/state, per commit `7e8f7c6`'s "-" side): drop the `Op` property and its comment line entirely, restore the single-purpose class comment.

**Current state to modify — `TryParsePrepFields`** (lines 413-439):
```csharp
//260626 hbk v3.0: $PREP 수신 파서. dataList[0]=site, [1]=z_index, [2]=Op(1=ON/0=OFF, 선택).
//  Op 미수신 시 1(ON) — 구 $PREP:site,z_index@ 하위호환. 필드 부족/비정수 → false(null 응답).
private static bool TryParsePrepFields(string[] dataList, PrepPacket prepPacket)
{
    bool bHasFields = dataList != null && dataList.Length >= 2;
    if (!bHasFields) { return false; }

    int nSite = 0;
    bool bSiteOk = Int32.TryParse(dataList[0], out nSite);
    if (!bSiteOk) { return false; }
    prepPacket.Site = nSite;

    int nZIndex = 0;
    bool bZIndexOk = Int32.TryParse(dataList[1], out nZIndex);
    if (!bZIndexOk) { return false; }
    prepPacket.ZIndex = nZIndex;

    bool bHasOp = dataList.Length >= 3;   //260626 hbk Op 선택 필드
    if (bHasOp)
    {
        int nOp = 1;
        bool bOpOk = Int32.TryParse(dataList[2], out nOp);
        if (bOpOk) { prepPacket.Op = nOp; }
    }

    return true;
}
```
CONTEXT.md removes lines 430-436 (the `bHasOp` block) and leaves the `bHasFields` check as `dataList.Length >= 2`. **Backward-compat decision (Claude's Discretion in CONTEXT.md):** the precedent commit `fbe05c8` chose to silently ignore the extra field rather than fail parsing — see `AlignTestPacket` parser in the same file (`TryParseAlignFields`, line ~388): `// dataList[2]=모드(skip)` — a field is present in the wire format but the parser comment explicitly documents "received but ignored." Recommend the planner follow this exact convention: keep `bHasFields = dataList.Length >= 2` (so old 3-field `$PREP:site,z_index,Op@` clients still parse successfully), and add a one-line comment noting the 3rd field (if present) is now ignored — do NOT add code that reads `dataList[2]` at all.

**Field-removal comment-breadcrumb convention** (from commit `fbe05c8`, applies to constant/property removal):
```csharp
-        private const int TEST_FIELD_ZINDEX = 4;             //260624 hbk Phase 63 필드 인덱스: z_index (3→4 시프트)
         private const int TEST_MIN_FIELD_SITE = 1;           // site 만 있으면 파싱 시작
-        private const int TEST_MIN_FIELD_ZINDEX = 5;         //260624 hbk Phase 63 z_index 필드 존재 최소 길이 (4→5 시프트)
+        // TEST_FIELD_ZINDEX/TEST_MIN_FIELD_ZINDEX 제거 //260626 hbk z_index=$PREP 분리 → $TEST에서 z_index 삭제
...
-        //260622 hbk Phase 48
-        // PROTO-01: z_index 필드 파싱. 누락/비정수 → SENTINEL_Z_INDEX_STR.
-        private static string ParseZIndexField(string[] dataList)
-        {
-            ...
-        }
+        // ParseZIndexField 제거 //260626 hbk z_index=$PREP 분리 — $TEST z_index 파싱 불필요
```
Apply the same convention here: when the `Op` property and any now-dead helper logic are deleted, leave a one-line `// XXX 제거 //260806 hbk ...` breadcrumb comment at the deletion site rather than silently vanishing the code — this is the established project convention for traceable removals.

---

### `WPF_Example/Custom/SystemHandler.cs` (controller, request-response)

**Analog:** same file, `ProcessPrep`/`ProcessTest`, changed in commit `fbe05c8`. That commit's diff shows `ProcessPrep` in its **pre-Op-branch state** (single path, no `if/else` on Op) — this is exactly the target shape for Task 71's `ProcessPrep`.

**Current state to modify — `ProcessPrep`** (lines 784-821):
```csharp
//260625 hbk Phase 64 LIGHT-01 (D-12): $PREP 처리.
//260626 hbk v3.0: Op 분기 — 1=ON(z_index 샷 조명 점등) / 0=OFF(사이클 종료 소등). $LIGHT 폐기 대체.
//  HW 트리거 전환 대비: 조명 ON/OFF 가 $PREP(준비 단계)에 통합 → $TEST(트리거)는 조명 무관.
//  Site 필드는 ACK 에 echo만 함. 실제 시퀀스 라우팅은 이 PC 소속 InspectionSequence 전부 대상.
private PrepAckPacket ProcessPrep(PrepPacket packet)
{
    PrepAckPacket ackPacket = new PrepAckPacket();
    bool bHasPacket = packet != null;
    if (!bHasPacket)
    {
        return null;
    }
    ackPacket.Target = packet.Sender;
    ackPacket.Site = packet.Site;
    ackPacket.ZIndex = packet.ZIndex;
    ackPacket.Op = packet.Op;          //260626 hbk Op echo (1=ON / 0=OFF)
    ackPacket.IsOk = false; // 기본값 FAIL — 성공 시 true 로 덮어씀

    bool bIsOn = packet.Op != 0;       //260626 hbk Op!=0 → ON (미수신 기본 1=ON)
    if (bIsOn)
    {
        _lastPrepZIndex = packet.ZIndex; //260626 hbk ON 일 때만 z_index 저장 → ProcessTest 주입용
        bool bApplied = ApplyPrepToSequences(packet.ZIndex);
        if (bApplied)
        {
            ackPacket.IsOk = true;
        }
    }
    else
    {
        bool bOff = TurnOffPrepLights(); //260626 hbk Op==0 → 전 시퀀스 소등
        if (bOff)
        {
            ackPacket.IsOk = true;
        }
    }
    return ackPacket;
}
```

**Target shape** (from commit `fbe05c8`'s post-diff `ProcessPrep`, the literal pre-Op version of this same method — this is the codebase's own historical single-path form):
```csharp
private PrepAckPacket ProcessPrep(PrepPacket packet)
{
    PrepAckPacket ackPacket = new PrepAckPacket();
    bool bHasPacket = packet != null;
    if (!bHasPacket)
    {
        return null;
    }
    ackPacket.Target = packet.Sender;
    ackPacket.Site = packet.Site;
    ackPacket.ZIndex = packet.ZIndex;
    ackPacket.IsOk = false; // 기본값 FAIL — 성공 시 true 로 덮어씀

    _lastPrepZIndex = packet.ZIndex; //260626 hbk z_index 저장 → ProcessTest 주입용
    bool bApplied = ApplyPrepToSequences(packet.ZIndex);
    if (bApplied)
    {
        ackPacket.IsOk = true;
    }
    return ackPacket;
}
```
Keep `_lastPrepZIndex = packet.ZIndex` unconditional (no more Op-gating) — matches the always-ON semantics decided in CONTEXT.md.

**`TurnOffPrepLights()` helper (lines 886-905)** — CONTEXT.md says reuse, do not delete. It currently is only called from `ProcessPrep`'s OFF branch; after this change nothing in `SystemHandler.cs` calls it anymore (the InspectionSequence-level `TurnOffShotLights()` is called directly from Task 3's new hook instead — see below). Planner should decide whether to keep `TurnOffPrepLights()` as dead code with a breadcrumb comment, or remove it — CONTEXT.md is explicit that the two *lower-level* methods (`TurnOffPrepLights` and `InspectionSequence.TurnOffShotLights`) must not be deleted, but doesn't address this specific dead-caller scenario; flag as a planner decision.

**Extra usage site NOT mentioned in CONTEXT.md — must also be fixed or the build breaks (CS1061):**
`DebugManualZTrigger` (lines 823-860), line 841:
```csharp
PrepPacket prepPacket = new PrepPacket();
prepPacket.ZIndex = zIndex;
prepPacket.Op = 1;
PrepAckPacket ack = ProcessPrep(prepPacket);
```
Once `PrepPacket.Op` is removed, `prepPacket.Op = 1;` must be deleted too (this is a temporary manual-Z-trigger bridge, comment at lines 823-829 explicitly says "ProcessPrep/ProcessTest 는 프로덕션 TCP 경로 — 시그니처/로직 변경 금지, 호출만 한다" — only this local caller line needs to change, not `ProcessPrep` itself beyond what's already planned).

---

### `WPF_Example/TcpServer/VisionResponsePacket.cs` (model, request-response)

**Analog:** same file, `BuildPrepAckMessage`/`PrepAckPacket`. Commit `7e8f7c6`'s diff shows the exact pre-Op text as the "-" side — this is the literal target end state for both the class and the builder method.

**Current state to modify — `PrepAckPacket` class** (lines 729-736):
```csharp
public class PrepAckPacket : VisionResponsePacket {
    public int ZIndex { get; set; }
    public int Op { get; set; } = 1;   //260626 hbk 1=ON / 0=OFF echo
    public bool IsOk { get; set; }

    public PrepAckPacket() : base(EVisionResponseType.PrepAck) {
    }
}
```
Target (per commit `7e8f7c6`'s pre-Op state): drop the `Op` property line and restore the single-line class comment `//260625 hbk Phase 64 LIGHT-01: $PREP_ACK 응답 패킷. IsOk=true → $PREP_ACK:site,z_index,OK@ / IsOk=false → $PREP_ACK:site,z_index,FAIL@`.

**Current state to modify — `BuildPrepAckMessage`** (lines 436-459):
```csharp
//260626 hbk v3.0: $PREP_ACK:site,z_index,Op,OK|FAIL@ 직렬화.
//  Op echo(1=ON점등 / 0=OFF소등). IsOk=true → OK, false → FAIL. 헝가리언 + if-else.
private static string BuildPrepAckMessage(PrepAckPacket packet)
{
    string szMsg = "";
    szMsg += CMD_SEND_PREP_ACK;
    szMsg += VisionServer.MSG_CMD_SEPERATOR;       // ':'
    szMsg += packet.Site.ToString();
    szMsg += VisionServer.MSG_CONTENTS_SEPERATOR;  // ','
    szMsg += packet.ZIndex.ToString();
    szMsg += VisionServer.MSG_CONTENTS_SEPERATOR;  // ','
    szMsg += packet.Op.ToString();                 //260626 hbk Op echo
    szMsg += VisionServer.MSG_CONTENTS_SEPERATOR;  // ','
    bool bIsOk = packet.IsOk;
    if (bIsOk)
    {
        szMsg += "OK";
    }
    else
    {
        szMsg += "FAIL";
    }
    return szMsg;
}
```
Target (per CONTEXT.md, matches commit `7e8f7c6`'s pre-Op state exactly): remove the two `Op`-related lines (the `szMsg += packet.Op.ToString();` line and its preceding `MSG_CONTENTS_SEPERATOR` line), producing `$PREP_ACK:site,z_index,OK@` / `$PREP_ACK:site,z_index,FAIL@`.

---

### `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` (sequence hook, event-driven)

**Analog:** self — `BuildScopedResponse` is the exact function CONTEXT.md names as the recommended single hook point. No external analog needed; the surrounding call structure already demonstrates the project's "call two side-effect-producing helpers in sequence, then check a shared flag once" idiom, which the new light-off hook should follow.

**Methods to reuse verbatim (do not delete, do not modify their internals):**
- `TurnOffShotLights()` (lines 700-709):
```csharp
//260626 hbk v3.0: $PREP Op==0(사이클 종료) → 전 조명 그룹 소등. ApplyShotLightsInternal 의 4그룹 OFF.
//  $LIGHT OFF 명령 폐기 대체. HW 트리거 전환 시에도 $PREP 가 OFF 담당.
public void TurnOffShotLights()
{
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_RING, false);
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BACK, false);
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false);
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BAR, false);
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_RING7, false);   //260626 hbk Phase 66: Ring7 소등 정합 — 점등(ApplyShotLightsInternal)/소등 대칭
}
```
This method is already `public` (called cross-class from `SystemHandler.TurnOffPrepLights`), so it can be called directly from within `InspectionSequence` itself without any visibility change.

**Hook site 1 (covers both normal-end and cross-Z-datum-early-end paths) — `BuildScopedResponse`** (lines 1400-1420):
```csharp
//260623 hbk Phase 49 PROTO-03 (D-01/D-02/D-03): Index-scoped 집계 + B/P/F 응답 조립.
//  이 시퀀스 소유 AND shot.ZIndex == nZIndex 인 Shot 의 FAI 만 집계(전체 재검사 금지 D-01).
//  중간 Index → B(IsBuffer=true, NG 있어도 B). 마지막 Index → 종합 P/F(m_bCycleHasNG||Datum실패).
//  불변식: NG 발견돼도 마지막 Index 까지 측정 진행(측정은 Action_FAIMeasurement, 여기는 집계만). 종료 판정은 마지막에서만.
private TestResultPacket BuildScopedResponse(InspectionRecipeManager recipeManager, int nZIndex, bool bIsLastIndex)
{
    var packet = new TestResultPacket
    {
        Target = RequestPacket.Sender,
        Site = RequestPacket.Site,
        InspectionType = RequestPacket.TestType,
        IsDynamicFAI = true,
        Type = RequestPacket.Type,   //260624 hbk Phase 63 PROTO-Type: 수신 Type echo
    };
    int nMatchedShots = AggregateIndexFais(recipeManager, nZIndex, packet);
    WarnIfEmptyScope(packet, nMatchedShots, nZIndex);   // BLOCKER 1: ZIndex 매칭 0건 경고(조용한 빈 B 금지)
    ApplyCycleJudgement(packet, bIsLastIndex, nMatchedShots);   // B vs 종합 P/F (D-03/불변식), WR-01: 매칭 0건 전달
    TryApplyCrossZDatumImmediateFail(packet, nZIndex);   //260722 hbk Phase 68 GAP-3(68-10): 완성 index 크로스-Z Datum 실패 재평가(게이팅, 기본 OFF no-op)
    pMyContext.ResultInfo = packet.Result;
    return packet;
}
```
Both `ApplyCycleJudgement` (lines 1657-1678) and `TryApplyCrossZDatumImmediateFail` (lines 1459-1490) mutate `packet.IsBuffer`/`packet.Result` in place. CONTEXT.md's recommended shape: after both calls (i.e., right before `pMyContext.ResultInfo = packet.Result;`), add one `if (!packet.IsBuffer) { TurnOffShotLights(); }` check — a single gate that transparently covers whichever of the two functions actually flipped `IsBuffer` to false, without touching either function's internals.

**Gap CONTEXT.md explicitly flags — `BuildDatumShotResponse` is a SEPARATE early-termination path, not reachable via `BuildScopedResponse`:**
```csharp
private TestResultPacket BuildDatumShotResponse()
{
    var packet = new TestResultPacket
    {
        Target = RequestPacket.Sender,
        Site = RequestPacket.Site,
        InspectionType = RequestPacket.TestType,
        IsDynamicFAI = true,
        Type = RequestPacket.Type,   //260624 hbk Phase 63 PROTO-Type: 수신 Type echo
    };
    bool bUseV1 = SystemHandler.Handle.Setting.UseProtocolV1;
    bool bDatumFailed = m_bCycleDatumFailed;
    bool bImmediateFail = bUseV1 && bDatumFailed;
    if (bImmediateFail)
    {
        // 즉시 F — 후속 Index skip 은 핸들러 주도(D-05). IsBuffer=false + Result=NG → 직렬화 'F'.
        packet.IsBuffer = false;
        packet.Result = EVisionResultType.NG;
        m_bImmediateFailSent = true;   //260722 hbk Phase 68 GAP-3(68-10, 지침 #6/T-68-11): z=0 즉시-F 도 latch 세팅 —
                                        //  완성 index 재평가(TryApplyCrossZDatumImmediateFail)가 중복 F 를 또 보내지 않도록.
        return packet;
    }
    // 정상 Datum 샷 → 빈 응답 RESULT:site;B;0; (FAIResults 비어있음 → FAICount=0).
    packet.IsBuffer = true;
    packet.Result = EVisionResultType.OK;
    return packet;
}
```
This is called from `HandleDatumIndexResponse` (lines 1390-1398), not from `BuildScopedResponse` — it is the actual "Index 0 Datum 즉시실패" path CONTEXT.md's decisions section refers to (note: this is a different function from `TryApplyCrossZDatumImmediateFail`, which handles a *different* case — cross-Z datum failure detected at a later completion index, already covered by hook site 1). **This path needs its own, second `if (!packet.IsBuffer) { TurnOffShotLights(); }` check** — either inline in the `bImmediateFail` branch (right after `m_bImmediateFailSent = true;`, before `return packet;`), or by wrapping the call site in `HandleDatumIndexResponse` with the same `if (!datumPacket.IsBuffer)` gate used at hook site 1, for consistency. CONTEXT.md's completion checklist explicitly calls out this path as "이 경로가 누락되기 쉬우니 반드시 별도 테스트" (easy to miss, must be tested separately) — the two hook sites are NOT redundant, both are required.

---

## Shared Patterns

### Field-removal convention (this repo's established idiom)
**Source:** commit `fbe05c8` (`WPF_Example/TcpServer/VisionRequestPacket.cs`, `WPF_Example/Custom/SystemHandler.cs`)
**Apply to:** all 4 files in this phase
- Delete the property/constant/parsing-branch entirely (do not just stop using it — it must not compile-reference the removed wire field).
- Leave a one-line breadcrumb comment at the deletion site: `// <NAME> 제거 //YYMMDD hbk <reason>` — e.g. `// ParseZIndexField 제거 //260626 hbk z_index=$PREP 분리 — $TEST z_index 파싱 불필요`. This project's convention per `feedback_comment_convention.md` (2026-06-11 policy) no longer *requires* date-stamped comments universally, but this specific "removal breadcrumb" pattern remains standard practice at deletion sites in the git history reviewed here — keep it for traceability since a protocol field is involved.
- If a helper becomes fully dead after the removal (no other caller), delete the helper method too, not just its call site.

### If-else → single-path consolidation
**Source:** commit `fbe05c8`, `ProcessPrep`/`ProcessLightSet` diffs
**Apply to:** `WPF_Example/Custom/SystemHandler.cs` `ProcessPrep`
When an `if/else` branch existed only to distinguish two protocol-field values (Op ON vs OFF) and one branch is being eliminated by contract, collapse to the surviving branch's body unconditionally rather than leaving a vestigial `if (true)`.

### Cross-cutting coding conventions (CLAUDE.md + CONTEXT.md, reinforced by every excerpt above)
- No ternary (`?:`) — use `if/else`.
- Hungarian-prefixed local bools for every condition (`bHasPacket`, `bIsOn`, `bApplied`, ...) — every excerpt above follows this; new code must too.
- `try/catch` only where the existing pattern already wraps risk (none of the touched methods currently have try/catch — don't introduce new ones unless genuinely needed).
- Comments explain "why," not "what" — see the `260722 hbk Phase 68 GAP-3` comment block above `TryApplyCrossZDatumImmediateFail` as the house style for non-obvious latch/gating logic.

## No Analog Found

None — all 4 files have exact analogs (3 via direct git-history precedent of the identical prior change in this same repo, 1 via the self-referential hook site CONTEXT.md names directly).

## Reference doc location (for the "완료 기준" excel-update item)

CONTEXT.md's "완료 기준" requires updating "프로토콜 문서(디팜스테크_Vision_Protocol_vX.X.xlsx)". Search results (outside the git repo, so not visible to Grep/Glob against the repo root):
- Most recent local copy found: `C:\Info\Doc\2.디팜스테크\02_설계\SOP\디팜스테크_Vision_Protocol_v1.3.xlsx` (siblings v1.0–v1.2 in same folder — versioning here is v1.x, not the "v3.3" mentioned in `.planning/ROADMAP.md` line 792 for the Phase 63 Type-field work; that `D:\디팜스테크_Vision_Protocol_v3_3.xlsx` path was not found on disk — likely renamed/moved or the ROADMAP reference predates a rename. Planner/executor should confirm the current canonical file with the user before editing.)
- In-repo markdown mirror (stale, still describes pre-`$PREP`/`$LIGHT`-era v1.0 protocol, NOT updated for Op removal): `.planning/refs/Vision-Protocol-v1.0.md` — explicitly marked as a "v1.0 참조" snapshot, not the live spec. Do not treat it as authoritative for this phase; it still shows `$LIGHT:site,type,OP@` and `$TEST:site,null,z_index@`, both already superseded by v3.0's `$PREP`/`$TEST` split (commit `fbe05c8`/`7e8f7c6`). Not in scope to update as part of this phase (it documents v1.0, not v3.x) unless the planner decides otherwise.

## Metadata

**Analog search scope:** `WPF_Example/TcpServer/`, `WPF_Example/Custom/SystemHandler.cs`, `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`, plus git history (`git log --all --oneline | grep 260626`, `git show fbe05c8`, `git show 7e8f7c6`) for direct prior-change precedent.
**Files scanned:** 4 target files (full relevant sections read, non-overlapping ranges) + 2 precedent commits' diffs (4 files touched across both) + 1 additional grep sweep for stray `.Op` usages (found 1 extra site not listed in CONTEXT.md: `SystemHandler.cs:841` `DebugManualZTrigger`).
**Pattern extraction date:** 2026-08-06
