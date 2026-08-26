---
phase: 73-side-4-jig-split
plan: 04
subsystem: tcp-routing
tags: [protocol, routing, prep, z-index, side-jig-split, phase73]
requires:
  - "73-01 — SEQ_SIDE_1~4 상수 + SIDE_1~4 InspectionSequence 등록"
  - "73-02 — PrepPacket.Type / PrepPacket.IsRequestValid / PrepAckPacket.Type 직렬화 계층"
provides:
  - "TryResolveSequenceNameByType — Type 코드 → 시퀀스 이름 직접 해석(PC2: 2~5=SIDE_1~4 / PC1: 0=TOP, 1=BOTTOM)"
  - "SetIdentifier 의 case VisionRequestType.Prep — $PREP 도 Identifier 를 갖는다(M10 완결)"
  - "_lastPrepZIndexBySeq — 시퀀스별 $PREP z_index 사전(+ _prepZIndexLock)"
  - "ApplyPrepToSequence(string,int) — $PREP 대상 시퀀스 1개에만 조명 적용(M1 소멸)"
  - "$PREP_ACK 의 Type echo + 규격위반/미해석 조기 FAIL(D-73-08 FAIL 정의 완결)"
  - "ResolveTypeCodeBySequenceName — 수동 트리거용 시퀀스명→Type 역매핑(M2)"
affects:
  - "WPF_Example/Custom/TcpServer/ResourceMap.cs"
  - "WPF_Example/Custom/SystemHandler.cs"
  - "73-05 (ApplyShotLights 반환 계약) — 이 plan 은 호출부만 정리, 반환 의미 변경은 73-05 소관"
  - "73-07 (검증) — $PREP_ACK OK/FAIL 최종 확인 + $SITE_STATUS 제약 HUMAN-UAT 이관"
tech-stack:
  added: []
  patterns:
    - "whitelist traditional switch + logged default — 미지 Type 은 조용히 폴백하지 않고 Error 로그 후 실패 반환"
    - "gated fallback — 폴백을 TryResolveSlotByType 성공(Type 0~5)으로 게이트해 미지 Type 의 남의 지그 접근 차단(B-2)"
    - "named-bool extraction — bIsPc2Side / bIsRequestValid / bHasSeqName / bPrepSlotResolved (삼항 0건)"
    - "parameter-threaded state — 전역 volatile 대신 조회 1회 후 파라미터로 전달(StartV1Scoped nPrepZIndex)"
key-files:
  created: []
  modified:
    - "WPF_Example/Custom/TcpServer/ResourceMap.cs"
    - "WPF_Example/Custom/SystemHandler.cs"
decisions:
  - "ESite(3슬롯)를 확장하지 않고 시퀀스 라우팅만 문자열 해석 경로로 분리 — 카메라/조명 슬롯 매핑은 무변경(회귀 0)"
  - "PC1 의 Type 0/1 은 폴백이 아니라 명시 해석(SEQ_TOP/SEQ_BOTTOM) — $PREP 에는 $TEST 같은 슬롯 폴백이 없어 명시하지 않으면 TOP/BOTTOM 조명 예열이 통째로 사라진다(B5)"
  - "$PREP 폴백을 TryResolveSlotByType 성공 조건으로 게이트 — 미지 Type(9 등)은 Identifier 미설정 → FAIL. $TEST 보다 엄격한 이유는 $PREP 이 조명·z_index 상태를 실제로 바꾸는 명령이기 때문"
  - "$TEST 쪽 무조건 슬롯 폴백(ResolveSiteSlot)은 조이지 않는다 — v2.6/PC1 회귀 위험, 이번 phase 범위 밖"
  - "$RESET 은 대상을 가리지 않고 전 시퀀스 z_index 기억을 Clear — 클린 슬레이트가 목적"
  - "TYPE_CODE_* 상수 이름/값 무변경, 주석만 SIDE_1~4 의미로 갱신(외부 참조 회귀 방지)"
metrics:
  duration: "약 40분"
  completed: "2026-08-26"
  tasks: 2
  commits: 2
  files: 2
---

# Phase 73 Plan 04: Type 기반 $PREP/$TEST 라우팅 + 시퀀스별 z_index Summary

`$PREP`/`$TEST` 의 Type 필드를 실제 시퀀스 라우팅에 연결하고, 전역 단일 변수였던 `_lastPrepZIndex` 를
시퀀스 이름별 사전으로 승격했다. `$PREP_ACK` 의 FAIL 을 "조명 세팅 실패 또는 요청 규격 위반" 전용으로
재정의해 Shot 유무 기반 예외 분기(M1)를 전부 없앴다.

## 무엇을 왜 했는가

`ESite` 는 `Top/Side/Bottom` 3슬롯이라 SIDE_1~4 를 담을 수 없다. 슬롯 enum 을 확장하면 카메라·조명
매핑까지 전부 흔들리므로, **시퀀스 라우팅만** Type → 시퀀스 이름 문자열 해석 경로로 분리했다.
카메라/조명 슬롯 매핑(`Find(EResource.Camera/Light/Action, eSlot)`)은 한 줄도 바꾸지 않았다.

`_lastPrepZIndex` 는 "z 값이 대상을 암시한다"는 전제 위에 서 있었다(D-73-06). Phase 73 이 그 전제를
깬다 — 지그마다 z 가 0 부터 독립 시작하므로 값이 겹친다. 시퀀스 이름을 키로 하는 사전으로 바꿔
`$PREP:2,2,0@` 직후 `$PREP:2,3,0@` 가 와도 SIDE_1 과 SIDE_2 의 z 가 서로 덮어쓰지 않는다.

## Tasks 완료

| Task | 내용 | 커밋 |
| ---- | ---- | ---- |
| 1 | ResourceMap Type→시퀀스 이름 해석 + `case VisionRequestType.Prep` 신설 (M3 / M10 후반) | `442ceb7` |
| 2 | 시퀀스별 z_index 사전(R2/R3) + 단일 시퀀스 조명 적용(M1) + `$RESET`(M11) + 수동 트리거(M2) | `80e6443` |

## 변경 상세

### WPF_Example/Custom/TcpServer/ResourceMap.cs (Task 1)

- **`using ReringProject.Utility;` 추가** — 이 파일은 `Logging.` 을 한 번도 쓰지 않아 using 이 없었다.
  이번에 `Logging.PrintLog` 를 2곳 추가하므로 없으면 첫 빌드가 CS0103 으로 깨진다. 가장 먼저 넣었다.
  (`ELogType` 은 `ReringProject.Setting` 이라 이미 있는 using 으로 해결)
- `TYPE_CODE_*` 상수 **주석만** SIDE_1~4 의미로 갱신. 이름·값 무변경.
- `TryResolveSequenceNameByType(string, out string)` 신설 — 비숫자 가드를 코드 비교보다 먼저 배치.
  PC2 는 화이트리스트 전통 switch(2~5 → SEQ_SIDE_1~4), default 는 Error 로그 후 `false`.
  PC1 은 0/1 → `SEQ_TOP`/`SEQ_BOTTOM` 명시 해석, 2~5 는 Error 로그 후 `false`(기존 슬롯 폴백 유지).
- `SetIdentifier` 의 `case VisionRequestType.Test` v1 분기 — Type 시퀀스 해석을 앞에 두고,
  성공하면 `Identifier` 를 그 이름으로. 실패하면 **기존 `TryResolveSlotByType` + `ResolveSiteSlot`
  폴백 그대로**. `Identifier2`(Action)는 슬롯 경로 무변경.
- `case VisionRequestType.Prep` 신설 — 폴백을 `TryResolveSlotByType` **성공 시에만** 돌린다(B-2).
  미지 Type 은 `Identifier` 를 세팅하지 않고 그대로 둔다 → `ProcessPrep` 이 FAIL ACK.
- `case VisionRequestType.SiteStatus` 위에 **알려진 제약 주석**(W10, 아래 별도 항목).
- `MapPc2Resources()` 2줄 + `InitializeV26()` 1줄의 Sequence 슬롯 `SEQ_SIDE` → `SEQ_SIDE_1`
  (`SEQ_SIDE` 시퀀스는 73-01 이후 더 이상 등록되지 않는다). Camera/Light 줄 무변경.

`TryResolveSlotByType` 본문은 **한 줄도 건드리지 않았다**(git diff hunk 0건 — 아래 검증 참조).

### WPF_Example/Custom/SystemHandler.cs (Task 2)

- `private volatile int _lastPrepZIndex` → `Dictionary<string,int> _lastPrepZIndexBySeq` +
  `object _prepZIndexLock`. 모든 읽기/쓰기를 lock 으로 감쌌다(T-73-12).
- `StorePrepZIndex(string,int)` / `GetPrepZIndex(string)` 헬퍼 추가.
  미기록 시퀀스는 z=0 보수적 폴백 + Error 로그(선행 `$PREP` 누락 진단).
- `ProcessTest` — `szTargetSeqName = packet.Identifier` → `GetPrepZIndex` 1회 조회 →
  `packet.TestID` 대입 + `StartV1Scoped` 로 전달.
- `StartV1Scoped(SequenceBase, TestPacket)` → `StartV1Scoped(SequenceBase, TestPacket, int nPrepZIndex)`.
  본문의 전역 변수 읽기 5곳을 파라미터로 치환. **Datum 대표트리거 / StartSubset / StartEmptyScope
  로직은 한 줄도 바꾸지 않았다.**
- `ApplyPrepToSequences(int)` (전 시퀀스 순회, 마지막이 이김) → `ApplyPrepToSequence(string,int)`
  (대상 1개). `bIsDatumOnlyZero` / `bIsDatumOnlyForSomeSeq` / `bTreatAsDatumOnly` 예외 분기 **전부 삭제**(M1 소멸).
- `ProcessPrep` 재작성 — `ackPacket.Type = packet.Type` echo(73-02 가 남긴 갭 완결),
  `IsRequestValid=false` → 조기 FAIL ACK + 로그, `Identifier` 미해석 → 조기 FAIL ACK + 로그,
  그 뒤에야 `StorePrepZIndex` + `ApplyPrepToSequence`. `return null` 은 `packet == null` 1건뿐.
- `ProcessReset` — `_lastPrepZIndexBySeq.Clear()` (lock 안), 로그 문구도 갱신(M11).
- `TriggerInspectionCycleManually` — `prepPacket.Identifier/Type/IsRequestValid` 직접 설정(M2).
  TCP 를 거치지 않아 `SetIdentifier` 가 돌지 않기 때문이다. `ResolveTypeCodeBySequenceName` 역매핑 헬퍼 추가.
- 전역 변수를 언급하던 **주석 4곳**(`:223` ProcessTest 헤더 / `:242` StartV1Scoped 헤더 /
  `:327` StartAll 폴백 설명 / `:907` ProcessReset 헤더)도 새 구조에 맞게 다시 썼다.

## Verification — 실제 실행 결과

### Task 1 acceptance (전 항목 통과)

```
TryResolveSequenceNameByType (want 3): 3
case VisionRequestType.Prep: (want 1): 1
using ReringProject.Utility; (want 1): 1
TryResolveSlotByType(prepPacket.Type (want 1): 1
ResolveSiteSlot(prepPacket.Site (want 0): 0
prepPacket.Identifier = null (want 0): 0
SequenceHandler\.SEQ_SIDE_1 (want 4): 4          ← 코드 참조만(switch 1 + MapPc2Resources 2 + InitializeV26 1)
bare SEQ_SIDE_1 (want 6): 6                      ← 위 4 + 설명 주석 2(B-2 게이트 근거 / W10 제약)
SequenceHandler.SEQ_SIDE) (want 0): 0            ← 편집 전 실측 3건 → 0건
알려진 제약(Phase 73) (want 1): 1
switch expression '=>' (want 0): 0
case TYPE_CODE_TOP:    → SequenceHandler.SEQ_TOP     (1건, B5)
case TYPE_CODE_BOTTOM: → SequenceHandler.SEQ_BOTTOM  (1건, B5)
```

`TryResolveSlotByType` 본문 무변경 확인 — `git diff` 에서 `eSlot` / `bIsBottomSlot` / `bIsTopSlot`
을 포함한 변경 라인이 해당 메서드 내부에 0건(잡힌 3건은 전부 새 Prep case 또는 새 주석).

### Task 2 acceptance (전 항목 통과)

```
_lastPrepZIndex\b (want 0): 0                    ← 선언1+코드8+로그문자열1+주석4 = 14곳 전부 처리
_lastPrepZIndexBySeq (want >=4): 7
StorePrepZIndex|GetPrepZIndex (want >=4): 4
ApplyPrepToSequences (want 0): 0
ApplyPrepToSequence( (want 2): 2                 ← 정의 1 + 호출 1
bTreatAsDatumOnly|bIsDatumOnlyForSomeSeq|bIsDatumOnlyZero (want 0): 0
ResolveTypeCodeBySequenceName (want 2): 2
prepPacket.IsRequestValid = true (want 1): 1
_lastPrepZIndexBySeq.Clear() (want 1): 1
StartV1Scoped 시그니처: private bool StartV1Scoped(SequenceBase seq, TestPacket packet, int nPrepZIndex)
ProcessPrep 본문 'return null;' (want 1): 1
```

### 빌드 (73-BUILD-VERIFY.md §2~4 규격 그대로)

MSBuild 는 PATH 에 없어 절대경로 호출:
`C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`
(그 외 인자는 규격 동일 — 대시 형식 / `-t:Rebuild` / 스크래치 `OutDir`·`IntermediateOutputPath`)

| 시점 | 구성 | exit | error | warning 줄 | 코드 분포 | 기준 |
| ---- | ---- | ---- | ----- | ---------- | --------- | ---- |
| Task 1 후 | SIMUL-ON (`Debug\|x64`) | 0 | 0 | **18** | CS0618×16 + CS0162×2 | 18 ✅ |
| Task 2 후 | SIMUL-ON | 0 | 0 | **18** | CS0618×16 + CS0162×2 | 18 ✅ |
| Task 2 후 | SIMUL-OFF (`-p:DefineConstants=TRACE%3BDEBUG`) | 0 | 0 | **16** | CS0618×16 | 16 ✅ |

- **새 경고 코드 종류 0건** (1순위 통과)
- CS0162 가 ON 2줄 → OFF 0줄로 사라져 **SIMUL-OFF 가 실제로 적용됨**을 교차 확인
- `[Obsolete]` 제거 / `#pragma warning disable` / `NoWarn` 사용 0건

### [W4] 코딩 규칙 — 두 파일 diff 추가 라인 전수 검사

```
?? / ?.        : 0줄
삼항 후보(주석 제외) : 0줄
switch expression '=>' : 0줄
```

### 라우팅 매트릭스 — 코드 경로 추적 결과 (계획 `<routing_matrix>` 대조)

**PC2 (PcRole=2)**

| 입력 | 1차 해석 | 폴백 | Identifier | ACK |
| ---- | -------- | ---- | ---------- | --- |
| `$PREP:2,2,2@` | SIDE_1 | — | SIDE_1 | 조명 결과 따름 |
| `$PREP:2,4,1@` | SIDE_3 | — | SIDE_3 | z 저장 확인, ACK 는 [W9] 참조 |
| `$PREP:2,9,0@` | 실패 + Error 로그 | **차단**(`TryResolveSlotByType(9)`=false) | 미설정 | **FAIL** |
| `$PREP:2,0,0@` | 실패 + Error 로그 | Top 슬롯 → SEQ_SIDE_1 | SIDE_1 | `$TEST` 와 동일 결과 |
| 필드 2/4개, Type 빈값 | — | — | — | **FAIL**(`IsRequestValid=false`) |

**PC1 (PcRole=1)**

| 입력 | 1차 해석 | 폴백 | Identifier |
| ---- | -------- | ---- | ---------- |
| `$PREP:1,0,0@` | **TOP**(명시) | — | TOP |
| `$PREP:1,1,0@` | **BOTTOM**(명시) | — | BOTTOM |
| `$PREP:1,3,0@` | 실패 + Error 로그 | Side 슬롯 → SEQ_BOTTOM | BOTTOM (`$TEST` 와 동일) |
| `$PREP:1,9,0@` | 실패 + Error 로그 | **차단** | 미설정 → **FAIL** |

`$TEST` PC2 Type 2~5 → `Identifier` = SEQ_SIDE_1~4, `Identifier2` = 슬롯 경로의 `ACT_INSPECT`
(Type 3 은 `ESite.Side` 슬롯, 나머지는 `ESite.Top` 슬롯 — 둘 다 `MapPc2Resources` 에 등록돼 있다).
v2.6 경로(`bUseV1=false`)는 무변경.

## Deviations from Plan

계획대로 실행됐다. 아래 2건은 계획이 명시하지 않은 부수 판단이다.

**1. [Rule 2 - 문서 정확성] `ProcessPrep` / `ProcessReset` 헤더 주석의 stale 문장 갱신**
- **발생 위치:** Task 2
- **내용:** 두 주석이 "실제 시퀀스 라우팅은 이 PC 소속 InspectionSequence 전부 대상" /
  "`ApplyPrepToSequences($PREP)`와 동일하게 …" 라고 적고 있어, 이 plan 의 변경으로 즉시 거짓
  문서가 된다(`ApplyPrepToSequences` 는 심볼 자체가 사라졌다). 새 동작으로 고쳐 썼다.
  `$RESET` 은 여전히 전체 대상이라는 점을 명시해 `$PREP` 과의 차이를 남겼다.
- **acceptance 영향:** 없음(`ApplyPrepToSequences` == 0 조건에 오히려 필요한 정리였다)
- **커밋:** `80e6443`

**2. [보고] MSBuild.exe 가 PATH 에 없음 — 절대경로 호출**
- 73-01/73-02 와 동일. 인자는 `73-BUILD-VERIFY.md` 규격 그대로다.

## 알려진 제약 (73-07 이 `73-HUMAN-UAT.md` 로 옮길 것)

**1. [W10] `$SITE_STATUS` 가 PC2 에서 SIDE_1 상태만 보고한다**

`SetIdentifier` 의 `case VisionRequestType.SiteStatus` 는 `Find(EResource.Sequence, (ESite)packet.Site)`
를 쓴다. 이 plan 이 PC2 슬롯을 `SEQ_SIDE_1` 로 바꿨으므로 **SIDE_1 하나만 조회**한다 —
SIDE_2~4 가 검사 중이어도 Idle 로 보고될 수 있다. 근본 원인은 `$SITE_STATUS` 에 Type 필드가 없어
대상을 특정할 수 없다는 것이다. **동작은 바꾸지 않고 코드 주석으로만 남겼다**(이번 phase 범위 M3 밖).
제어와 재협의 필요.

**2. [W9] `$PREP:2,4,1@` 의 ACK 는 이 시점에 `FAIL` 이 정상이다**

`InspectionSequence.ApplyShotLights(int)` 가 아직 "이 z 에 Shot 없음 → false"
(`InspectionSequence.cs:751~763`)이기 때문이다. 이 plan 은 **호출부만** 단일 시퀀스로 정리했고,
반환 의미를 "조명 세팅 성공 여부"로 바꾸는 것은 **73-05 Task 2(D) 소관**이다.
이 시점에 확인할 것은 **라우팅(로그의 `seq=SIDE_3`)과 z 저장**뿐이며, `OK` 전환 검증은 73-07 S2~S5 가 한다.

**3. `$TEST` 의 미지 Type 폴백은 의도적으로 조이지 않았다**

`$TEST:2,9,…` 는 `ResolveSiteSlot(Site)` 폴백으로 SIDE_1 을 실행하지만(Phase 73 이전 동작 보존),
`$PREP:2,9,…` 는 FAIL 이다. `$PREP` 이 조명·z_index 상태를 실제로 **바꾸는** 명령이라 더 엄격하게 뒀다.
`$TEST` 까지 함께 조이면 v2.6/PC1 회귀 위험이 있어 범위 밖으로 뒀다.

## Known Stubs

없음. 이 plan 이 만든 경로는 전부 실제 동작에 연결돼 있다.

## Threat Model 반영

| Threat ID | 반영 |
| --------- | ---- |
| T-73-11 (Spoofing) | `TryResolveSequenceNameByType` 화이트리스트 전통 switch, default 는 Error 로그 + `false` |
| T-73-25 (EoP, 폴백) | Prep 폴백을 `TryResolveSlotByType` 성공(Type 0~5)으로 게이트 — 미지 Type 이 SEQ_SIDE_1 을 prep 하고 그 z_index 를 세팅하는 경로 차단(B-2) |
| T-73-12 (Tampering) | `_lastPrepZIndexBySeq` 의 모든 읽기/쓰기(Store/Get/Clear)를 `_prepZIndexLock` 으로 감쌈 |
| T-73-13 (DoS) | `ProcessPrep` 의 `return null` 은 `packet == null` 1건뿐 — 나머지 전 경로가 ACK 반환 |
| T-73-14 | accept(계획대로) — 조명은 물리 안전 영향 없고 PC2 SIDE 4개로 범위가 닫혀 있다 |

## Threat Flags

없음 — 신규 네트워크 표면/인증 경로/파일 접근 패턴/스키마 변경 없음.
기존 `$PREP`/`$TEST` 경로의 대상 해석만 바뀌었다.

## Follow-up (후속 plan 확인 필요)

1. **73-05** — `ApplyShotLights` 반환 계약을 "조명 세팅 성공 여부"로 변경(위 [W9]).
   그 전까지 Shot 없는 z 의 `$PREP_ACK` 은 FAIL 로 나간다.
2. **73-05 / R1** — SIDE_1~4 가 같은 `LIGHT_BAR` 그룹을 공유하는 소등 충돌은 이 plan 범위 밖.
3. **M4 미완** — `FIXTURE_SIDE` 분할 마이그레이션 전이므로 **앱에서 레시피 저장 금지**
   (3faa91b 데이터 손실 패턴). 이 plan 은 컴파일 검증까지만 수행했고 앱 기동 UAT 는 하지 않았다.
4. **`$SITE_STATUS` 제약** — 위 "알려진 제약 1" 을 `73-HUMAN-UAT.md` 로 이관.

## csproj

`git status --porcelain WPF_Example/DatumMeasurement.csproj` → ` M` (앞칸 공백, 끝까지 unstaged).
두 커밋 어디에도 포함되지 않았고, 두 커밋 모두 파일 삭제 0건이다.

## Self-Check: PASSED

- `WPF_Example/Custom/TcpServer/ResourceMap.cs` — FOUND
- `WPF_Example/Custom/SystemHandler.cs` — FOUND
- `.planning/phases/73-side-4-jig-split/73-04-SUMMARY.md` — FOUND
- 커밋 `442ceb7` — FOUND
- 커밋 `80e6443` — FOUND
