---
phase: 73-side-4-jig-split
reviewed: 2026-08-26T00:00:00Z
depth: deep
diff_base: a525717
files_reviewed: 12
files_reviewed_list:
  - WPF_Example/Custom/Define/ID.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/Custom/Sequence/SequenceHandler.cs
  - WPF_Example/Custom/SystemHandler.cs
  - WPF_Example/Custom/TcpServer/ResourceMap.cs
  - WPF_Example/Sequence/SequenceHandler.cs
  - WPF_Example/TcpServer/VisionRequestPacket.cs
  - WPF_Example/TcpServer/VisionResponsePacket.cs
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
  - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
  - WPF_Example/VersionDefine.cs
findings:
  blocker: 0
  warning: 5
  nit: 4
  total: 9
status: issues_found
---

# Phase 73 — 코드 리뷰 (SIDE 4지그 분리 + $PREP Type)

**blocker 0건.** 지정 6항목은 전부 통과했다. 아래 warning 5건은 동작은 하지만
전제가 깨지면 조용히 틀어지는 지점이고, 그 중 W1 은 이번 phase 가 닫으려던 R2 의
남은 절반이다.

---

## 지정 6항목 검증 결과

| # | 항목 | 결과 | 근거 |
|---|---|---|---|
| 1 | 삼항/축약 잔존 | **PASS (0건)** | 추가 라인 전체에 `?:` / `??` / `?.` / switch expression 0건. `(datum.DatumName ?? "")` 는 context 라인(미변경) |
| 2 | `ESequence.Side` 전환 누락 | **PASS** | 잔존 3곳 전부 주석 또는 의도적 레거시 case. 실제 등록/라우팅 경로 잔존 0 |
| 3 | Datum 소실 경로 | **PASS** | 4섹션 대칭 + 구 `[FIXTURE_SIDE]` carry-over 가드 존재, 실제 레시피 4 Datum 온전 |
| 4 | `.shm` 경로 대칭 | **PASS** | `ResolveDatumModelPath` 4개 오버로드 전부 `NormalizeModelFolderName` 통과 |
| 5 | 크로스-Z 완성 index 계약 | **PASS** | 4지그 모두 ZIndexA/B=0/1, 완성 index=1 < 첫 측정 z=2. `ComputeLastZIndex` 2/3/4/3 |
| 6 | `TryGetBlockingSequence` 상호배타 | **PASS (UI 한정)** | 4지그가 같은 `VirtualCamera` 인스턴스 → `ReferenceEquals` true. TCP 비대칭은 W3 |

### 추가 지정 항목

| 항목 | 결과 |
|---|---|
| R1 조명 스코핑 — 스코프 "안"의 `Enabled=false → OFF` | **PASS** |
| R1 점등/소등 같은 기준 | **PASS** (양쪽 `CollectOwnedChannelScope()` 공유) |
| M13 범위 밖 z 방어 ↔ WR-01 정합 | **PASS** |
| `$PREP` 무응답 (`ProcessPrep`) | **PASS** — 그러나 파서 상위에 별도 무응답 경로 존재 (**W2**) |
| `_lastPrepZIndex` → 시퀀스별 사전 | **PASS** (잔존 참조 0, lock 전 경로 적용) |

---

## Warnings

### WR-01. PC2 에서 Type 0/1 이 SIDE_1 을 가로챈다 (R2 의 남은 절반)

**파일:** `WPF_Example/Custom/TcpServer/ResourceMap.cs:411~419`, `:479~486`, `:113~114`

`TryResolveSequenceNameByType` 의 PC2 분기 주석은 이렇게 선언한다:

```
//   PC2(Side)  : 0/1 은 이 PC 대상이 아니므로 실패 반환 / 2~5 = SIDE_1~4
```

그러나 `false` 반환 직후의 폴백이 그 의도를 되돌린다. `$PREP` 폴백(:479~486)은
`TryResolveSlotByType` 성공을 게이트로 쓰는데, `TryResolveSlotByType` 은
Type **0~5 전부**에 대해 `true` 를 돌려준다(:163~175). 그리고 PC2 자원맵은

```
Add(EResource.Sequence, ESite.Top,  SequenceHandler.SEQ_SIDE_1);   // :113
Add(EResource.Sequence, ESite.Side, SequenceHandler.SEQ_SIDE_1);   // :114
```

두 슬롯 모두 `SEQ_SIDE_1` 이다. 결과:

- `$PREP:site,0,z@` (TOP) → Type 미해석 → `ESite.Top` → **Identifier = SIDE_1**
- `$PREP:site,1,z@` (BOTTOM) → `ESite.Side` → **Identifier = SIDE_1**

→ `StorePrepZIndex("SIDE_1", z)` 로 **SIDE_1 의 z 기억이 덮어써지고**,
`ApplyPrepToSequence("SIDE_1", z)` 로 SIDE_1 조명이 재적용되며, `OK` 로 응답한다.
이후 진짜 SIDE_1 `$TEST` 는 오염된 z 로 실행된다(z=0 이면 Datum 경로로 통째 오라우팅).

이번 phase 가 `_lastPrepZIndex` 를 시퀀스별 사전으로 승격시킨 이유(R2 = "대상이 섞이면
조용히 오염")가 Type 필드 자체로 되살아난 셈이다. 73-04-SUMMARY 의
`gated fallback ... 남의 지그 접근 차단(B-2)` 은 **미지 Type 에만** 성립한다.

발생 조건은 "PC2 가 Type 0/1 짜리 `$PREP` 을 받는가" 이며, 이는 제어 송신 설계에
달려 있어 코드가 보장하지 않는다(D-73-08 의 공정 순서와 같은 종류의 미보장 전제다).

**Fix:**
```csharp
// SetIdentifier 의 case VisionRequestType.Prep — 폴백을 "이 PC 의 대상 Type" 으로 한 번 더 좁힌다.
bool bIsPc2Side = (int)SystemSetting.Handle.PcRole == (int)EPcRole.PC2_Side;
bool bIsSideType = /* Type 2~5 */;
bool bFallbackAllowed;
if (bIsPc2Side) { bFallbackAllowed = bIsSideType; }
else            { bFallbackAllowed = !bIsSideType; }
if (bFallbackAllowed && bPrepSlotResolved) { prepPacket.Identifier = Find(EResource.Sequence, ePrepSlot); }
// 그 외 → Identifier 미설정 → ProcessPrep 이 FAIL ACK (무응답 아님)
```
`$TEST` 쪽(:448~455)도 같은 게이트가 필요하다 — 지금은 Type 0/1 이 PC2 에서
`Find(EResource.Sequence, eSlot)` = `SEQ_SIDE_1` 로 흘러 SIDE_1 사이클을 실제로 시작시킨다.

---

### WR-02. `$PREP@` (필드 없는 요청)은 여전히 무응답 — PLC 무한 대기

**파일:** `WPF_Example/TcpServer/VisionRequestPacket.cs:171`

`ProcessPrep` 의 `return null` 은 `packet == null` 1건뿐이다(확인 완료,
`Custom/SystemHandler.cs:929~933`). 그러나 그 위 파싱 계층에 무응답 경로가 남아 있다:

```csharp
// :169  RESET 은 예외 처리됨
if (msgList[0] == CMD_RECV_RESET && msgList.Length < 2) { return new ResetPacket(); }
// :171  PREP 은 여기서 걸린다
if (msgList.Length < 2) return null;
```

`$PREP@` 처럼 `:` 가 없는 요청은 `Convert` 가 `null` 을 돌려주고,
`MainRun`(`Custom/SystemHandler.cs:134~136`)이 `responsePacket == null` 을 "응답 없음"
으로 처리해 **ACK 자체가 안 나간다.** 73-02 가 세운 계약("$PREP 은 절대 무응답이 아니다")이
여기서만 성립하지 않는다. 파서 주석(:433~436)이 스스로 경고한 라인 정지 시나리오와 같다.

**Fix:** :169 의 RESET 선례를 그대로 복제.
```csharp
if (msgList[0] == CMD_RECV_PREP && msgList.Length < 2) {
    PrepPacket p = new PrepPacket();
    p.IsRequestValid = false;   // ProcessPrep 이 FAIL ACK 회신
    return p;
}
```

---

### WR-03. TCP `$TEST` 경로에는 SIDE_1~4 상호배타 게이트가 없다 (지정항목 6의 비대칭)

**파일:** `WPF_Example/Custom/Sequence/SequenceHandler.cs:83~126` (UI 전용),
`WPF_Example/Device/DeviceHandler.cs:345~351`

`TryGetBlockingSequence` 는 SIDE_1~4 를 정확히 잡아낸다 — 4개 모두 마스터 파라미터
`DeviceName = CAMERA_SIDE` 라 `TryCollectSequenceCameras` 가 같은 `VirtualCamera` 참조를
돌려주고 `ReferenceEquals` 가 true 가 된다(fail-closed 확인). 문제는 이 게이트가
**UI RUN 버튼에만** 걸려 있다는 것이다. TCP `$TEST` 는

```
ProcessTest → StartV1Scoped → seq.StartAll/StartSubset
  → SequenceBase.StartCore : State != Idle 인 "자기 자신"만 검사
```

즉 **형제 시퀀스가 검사 중이어도 자기가 Idle 이면 시작한다.** Phase 73 이전에는
SIDE 시퀀스가 1개뿐이라 두 번째 `$TEST` 가 `State != Idle` 로 자동 거부됐다 —
그 우연한 보호가 이번 분리로 사라졌다. 겹치면 `GrabHalconImage` 가

```csharp
if (!cam.Properties.ApplyFromParam(param)) return null;   // 설정
return cam.GrabHalconImage(requestIdentifier);            // 촬영  ← 사이에 락 없음
```

비원자 구간을 두 스레드가 통과해 **A 의 설정으로 B 가 찍는** 창이 생긴다.

실사용에서 겹칠 가능성은 낮다 — `112de7f` 이후 `$RESULT` 는 시퀀스가 Idle 로 돌아온 뒤
나가고 PLC 는 그 응답을 받고서 다음 지그를 트리거한다. 하지만 그 순서는 제어 설계에만
있고 코드에 없다(H3 가 확인하려는 바로 그 전제다).

**Fix:** `ProcessTest` 에서 dynamic FAI 경로 진입 직전에 UI 와 같은 게이트를 재사용한다.
```csharp
string szBlocking;
if (Sequences.TryGetBlockingSequence(seqIdOfTarget, out szBlocking)) {
    Logging.PrintLog((int)ELogType.Error,
        "[TEST] {0} 시작 거부 — {1} 이(가) 같은 카메라로 검사 중", seqName, szBlocking);
    return false;   // → SendTestError 로 F 응답. 무응답 아님
}
```
거부가 F 로 나가는 부작용은 이미 알려진 미수정 건
(`project_plc_rapid_retest_start_reject`)과 같은 클래스이므로, 도입 시 그쪽과 함께 볼 것.

---

### WR-04. 레거시 `Param0..N` 가드가 정작 보호 대상 레시피에는 걸리지 않는다

**파일:** `WPF_Example/Sequence/SequenceHandler.cs:206~223`

```csharp
int nSavedSeqCount = loadFile["Info"]["ParamSequenceCount"].ToInt();
bool bHasSavedSeqCount = nSavedSeqCount > 0;
bool bSeqCountMismatch = bHasSavedSeqCount && nSavedSeqCount != Sequences.Count;
```

`ParamSequenceCount` 는 **이번 phase 가 새로 쓰기 시작한 키**다. Phase 73 이전에 저장된
구 포맷 레시피에는 이 키가 없다 → `nSavedSeqCount = 0` → `bHasSavedSeqCount = false`
→ `bSeqCountMismatch = false` → **가드를 통과해 그대로 로드된다.**

R4 가 지목한 위험(시퀀스 1→4 증가로 `Param0..N` 위치 인덱스가 밀림)은 정확히
"키가 없는 구 레시피"에서 발생하는데, 가드는 그 경우를 못 막는다.
현재 운용 레시피(`FAI_1`)는 SHOTS 신포맷이라 `TryLoadNewFormat` 이 :198 에서 조기 반환하므로
실피해는 없다. 하지만 구 포맷 레시피를 하나라도 열면 `reference_parambase_missing_key_zeroes_default`
와 겹쳐 조용한 0-클로버가 난다.

**Fix:** 키 부재를 "알 수 없음 = 신뢰 불가"로 취급한다.
```csharp
bool bHasSavedSeqCount = loadFile["Info"].ContainsKey("ParamSequenceCount"); // 값이 아니라 키 존재로 판정
bool bSeqCountUnknown = !bHasSavedSeqCount;
bool bSkipLegacyLoad = bSeqCountUnknown || (nSavedSeqCount != Sequences.Count);
```
(키가 없는 구 레시피는 로드를 건너뛰고 Error 로그를 남긴다 — 오매핑보다 미로드가 안전하다.)

---

### WR-05. 티칭 경로: datum 소유 시퀀스가 아닌 인스턴스로 조명이 걸릴 수 있다

**파일:** `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs:1335`,
`WPF_Example/UI/ContentItem/MainView.xaml.cs:1302`, `:1394`

스코핑 도입으로 `ApplyDatumLights` 는 **호출 대상 인스턴스의** 소유 채널 집합 안에서만
동작한다(`InspectionSequence.cs:1076~1090`). 그런데 MainView 는 조명 시퀀스를
`Sequences[param.SequenceName]` 로 잡고, 그 `param` 은 `ResolveDatumCameraParam(datum)`
의 결과다. 이 함수의 최종 폴백이 문제다:

```csharp
return shots[0];   // :1335 — 전역 첫 Shot (현 레시피에서는 TOP 소유)
```

SIDE Datum 4개는 모두 `SourceShotName=` **빈 값**이다(실 레시피 확인).
지금은 트리 선택 노드의 `SequenceName` 폴백(:1328~1334)이 SIDE_1~4 의 첫 Shot 을 찾아내
정상 동작한다. 그러나 어떤 SIDE 시퀀스에 Shot 이 하나도 없는 상태(새 지그 추가 직후 등)가
되면 `shots[0]` = TOP Shot → `lightSeq` = TOP → SIDE Datum 의 BACK 요청이
TOP 스코프 밖 → **조명이 안 켜진 채 티칭 grab.**

무음은 아니다 — `WarnIfEnabledOutOfScope`(`InspectionSequence.cs:1092~1103`)가 Error 로그를
남긴다. `InspectionListView.xaml.cs:1467~1470` 주석도 이 변화를 명시했다. 그래도
"어두운 티칭 이미지"는 되돌리기 비싼 오염이라 폴백 자체를 막는 편이 낫다.

**Fix:** `ResolveDatumCameraParam` 의 최종 `shots[0]` 폴백을 제거하고 `null` 을 반환한다
(호출부 `:1166`, `:1193` 이 이미 `null` 을 정상 처리한다). 소유 시퀀스 Shot 이 없으면
그 datum 은 애초에 grab 할 카메라가 정해지지 않은 상태다.

---

## Nits

### NIT-01. `GetPrepZIndex` 의 빈 이름 경로만 로그가 없다

**파일:** `WPF_Example/Custom/SystemHandler.cs:33~37`
사전 미등록 경로(:47~49)는 Error 로그를 남기는데, 이름이 빈 경우는 조용히 `0` 을 돌려준다.
`$TEST` 의 Identifier 해석이 실패해 z=0(Datum 경로)로 떨어지는 것은 눈에 띄어야 한다.
같은 문구의 로그 1줄 추가 권장.

### NIT-02. `_lastPrepZIndex` 를 가리키는 stale 주석 2곳

**파일:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:334`,
`WPF_Example/TcpServer/VisionRequestPacket.cs:360`
필드는 `_lastPrepZIndexBySeq` 로 대체됐다. 로그↔코드 추적(`reference_seq_log_tag_to_code_map`)
관점에서 이름만 갱신 권장.

### NIT-03. `ShotConfig.cs:53` 주석이 3시퀀스 시절 값을 나열

`// 값 = SequenceHandler.SEQ_TOP / SEQ_SIDE / SEQ_BOTTOM` — 실제 값은 SIDE_1~4 다.
`OwnerSequenceName` 의 유일한 문서라 갱신 권장.

### NIT-04. `TYPE_CODE_TOP_SIDE1` / `TYPE_CODE_BOTTOM_SIDE2` 이름이 의미와 불일치

**파일:** `WPF_Example/Custom/TcpServer/ResourceMap.cs:137~138`
값 2/3 은 이제 SIDE_1/SIDE_2 다. 주석으로 "이름은 구 스펙 잔재, 변경 금지"를 명시해
두었으므로 이번엔 손대지 않는 것이 맞다. 다음 프로토콜 개정 때 함께 정리할 후보로만 기록.

---

## 판정 제외 확인 (요청대로 결함으로 세지 않음)

- `$RESULT` 3필드 — `f7ed10c` 의도적 제거, 이번 phase 무관 ✔
- "조건부 안전" 주석 잔존 — D-73-09 지시대로 존치 ✔
- `[Param0..7]` 의 `OwnerSequenceName=SIDE` 8건 — 죽은 경로, 무수정 ✔
  (참고: 동적 FAI 레시피는 `TryLoadNewFormat` 조기 반환으로 이 섹션을 읽지 않고,
   다음 저장 때 `IsDynamicFAIMode` 분기로 자연 소멸한다 — 73-01 예고대로 동작)
- 빌드 경고 18/16줄 baseline ✔

## 이미 추적 중이라 중복 보고하지 않은 항목

- K1 스코프 밖 조명 잔광 (`73-HUMAN-UAT.md`) — SIDE_1 만 `ALIGN_COAX` 를 켜는
  현 레시피 구성을 코드로 재확인했다. 지적 자체는 정확하다.
- K2 `$SITE_STATUS` 대상 특정 불가 — `ResourceMap.cs:429~434` 주석과 일치.
- H1~H6 실기 항목.

---

_Reviewed: 2026-08-26_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: deep (diff a525717..HEAD, WPF_Example 12파일 + 실 레시피 `D:\Data\Recipe\FAI_1\main.ini` 대조)_
