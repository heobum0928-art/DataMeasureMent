---
phase: 73-side-4-jig-split
plan: 02
subsystem: tcp-protocol
tags: [protocol, prep, parser, ack, phase73]
requires: []
provides:
  - "PrepPacket.Type — $PREP 수신 대상 코드(0=TOP/1=BOTTOM/2~5=SIDE_1~4)"
  - "PrepPacket.IsRequestValid — 규격 위반 요청 표시(무응답 대신 FAIL ACK 유도)"
  - "PrepAckPacket.Type — $PREP_ACK Type echo"
  - "$PREP_ACK:site,Type,z_index,OK|FAIL@ 4필드 응답 포맷"
affects:
  - "73-04 (라우팅): PrepPacket.Type / IsRequestValid 를 읽어 대상 시퀀스 결정 + 조기 FAIL 처리"
  - "제어(PLC) 측 $PREP 송신부 — 동시 교체 필요"
tech-stack:
  added: []
  patterns:
    - "명명 상수 필드 인덱스 파서(TryParseTestFieldsV1 선례)"
    - "파서는 절대 false 를 반환하지 않는다 — 실패는 플래그로 전달(TryParseResetFields 선례)"
key-files:
  created: []
  modified:
    - WPF_Example/TcpServer/VisionRequestPacket.cs
    - WPF_Example/TcpServer/VisionResponsePacket.cs
decisions:
  - "D-73-05/D-73-08 확정 포맷 $PREP:site,Type,z_index@ 3필드 전용 구현 — v1/v2 필드 개수 분기 없음(구버전 펌웨어 부재)"
  - "$PREP_ACK Type 미확인 시 임의 기본값을 채우지 않고 빈 필드 송신 — 제어가 '보내지 않은 Type 을 받았다'고 오해하는 것을 막기 위함"
  - "M8(와이어 site ↔ ESequence 직접 비교)는 동작 무변경, 주석으로만 결함 명시"
metrics:
  duration: "약 35분"
  completed: 2026-08-26
  tasks: 2
  commits: 2
  files: 2
---

# Phase 73 Plan 02: $PREP Type 필드 도입 Summary

`$PREP` 수신 파서를 `dataList[1]=z_index` 고정 인덱스에서 명명 상수 기반 3필드 전용
(`$PREP:site,Type,z_index@`) 파서로 전면 재작성하고, 실패 시 무응답(=PLC ACK 무한 대기 → 라인 정지)
경로를 코드에서 제거했으며, `$PREP_ACK` 응답에 Type echo 필드를 추가했다.

## 무엇을 왜 했는가

기존 파서는 두 번째 필드를 **무조건 z_index 로** `Int32.TryParse` 했다. 제어가 Type 을
두 번째에 넣어 보내면 Type 이 숫자이므로 **파싱이 "성공"하면서 z_index 로 오인**된다 —
예외도 FAIL 도 나지 않는 조용한 오동작이다. 이걸 명명 상수 인덱스 + 필드 개수 정확 비교
(`== PREP_FIELD_COUNT`)로 다시 썼다.

두 번째로, 기존 구조는 파싱 실패 → 파서 `false` → 호출부 `return null` → **응답 자체가 안 나감**이었다.
PLC 는 ACK 를 기다리므로 그대로 라인이 멈춘다. 파서가 절대 `false` 를 반환하지 않도록 바꾸고,
규격 위반은 `IsRequestValid=false` 로 표시해서 넘긴다. `ProcessPrep` 의 `ackPacket.IsOk` 기본값이
`false` 이므로 **잘못된 입력은 자동으로 FAIL ACK 가 된다.**

## Tasks 완료

| Task | 내용 | 커밋 |
| ---- | ---- | ---- |
| 1 | $PREP 파서 3필드 전용 재작성 + 무응답 경로 제거 (M10) | `b5a66be` |
| 2 | $PREP_ACK Type echo 추가 (M12) + M8 커플링 주석 명시 | `6e2f506` |

## 변경 상세

### WPF_Example/TcpServer/VisionRequestPacket.cs

- `using ReringProject.Setting;` / `using ReringProject.Utility;` 추가 (ELogType / Logging)
- 명명 상수 4개 추가: `PREP_FIELD_SITE=0`, `PREP_FIELD_TYPE=1`, `PREP_FIELD_ZINDEX=2`, `PREP_FIELD_COUNT=3`
- `PrepPacket` 에 `Type`(string, 기본 `""`) / `IsRequestValid`(bool, 기본 `true`) 프로퍼티 추가
- `TryParsePrepFields` 전면 교체 — 항상 `true` 반환, 실패 시 Error 로그 + `IsRequestValid=false`
- 호출부 `if (!bPrepOk) { return null; }` 삭제
- D-71-01 하위호환 주석("3번째 필드를 아예 읽지 않고 무시한다") 폐기, 대신
  "⚠ 알려진 제약(Phase 73)" 주석으로 구 3필드 오파싱 위험 기록

### WPF_Example/TcpServer/VisionResponsePacket.cs

- `PrepAckPacket` 에 `Type` 프로퍼티 추가
- `BuildPrepAckMessage` → `$PREP_ACK:site,Type,z_index,OK|FAIL@` (구분자 3개)
- `IsOk` 의미(D-73-08 확정: FAIL = 조명 세팅 실패 또는 요청 규격 위반)를 메서드 주석에 명문화
- M8 커플링 결함 주석 추가 (`testPacket.Site == (int)ESequence.Bottom`) — **동작 무변경**

## 검증 결과

### Task 1 acceptance (전부 실행 확인)

```
PREP_FIELD_TYPE:   2   (기대 >=2)
PREP_FIELD_ZINDEX: 3   (기대 >=2)
PREP_FIELD_COUNT:  3   (기대 >=3)
IsRequestValid:    4   (기대 ==4)
bPrepOk:           0건
CMD_RECV_PREP case 블록 내 return null: 0건
파서 본문 return false: 0건
파서 본문 dataList[1] 리터럴: 0건
파서 본문 Length >= 형태 분기: 0건 / == PREP_FIELD_COUNT: 1건
알려진 제약(Phase 73): 1
구 D-71-01 문장 "3번째 필드를 아예 읽지 않고 무시한다": 0건
```

### Task 2 acceptance

```
BuildPrepAckMessage 본문 MSG_CONTENTS_SEPERATOR: 3   (기대 ==3)
packet.Type: 1                                       (기대 >=1)
PrepAckPacket 범위 public string Type: 1             (기대 ==1)
커플링 결함: 1                                        (기대 ==1)
BuildResetAckMessage diff hunk: 0                    (기대 ==0)
```

### [W4] 코딩 규칙 (두 파일 diff, 추가 라인 기준)

```
?? / ?. : 0줄
삼항 후보(주석 제외) : 0줄
```

### 빌드 (73-BUILD-VERIFY.md §2~4)

MSBuild 경로: `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`
(PATH 에 없어 절대경로 사용. 그 외 인자는 규격 그대로 — 대시 형식 / `-t:Rebuild` /
스크래치 `OutDir`·`IntermediateOutputPath`)

| 빌드 | exit | 에러 | 경고 줄 | 경고 코드 종류 | 기준 |
| ---- | ---- | ---- | ------- | -------------- | ---- |
| SIMUL-ON (`Debug\|x64`) | 0 | 0 | **18** | CS0618×16 + CS0162×2 | 18 ✅ |
| SIMUL-OFF (`-p:DefineConstants=TRACE%3BDEBUG`) | 0 | 0 | **16** | CS0618×16 | 16 ✅ |

- 새로운 경고 코드 종류 **0건** (1순위 기준 통과)
- CS0162 가 ON 2줄 → OFF 0줄로 사라져 **SIMUL-OFF 가 실제로 적용됨**을 교차 확인
- 73-01 Task 3 이 이미 커밋된 시점이라 CS0618 이 16줄(=baseline 표의 "73-01 Task 3 완료 후" 값)

### 기능 검증(코드 경로 추적)

| # | 입력 | 결과 |
| - | ---- | ---- |
| 1 | `$PREP:2,4,1@` | Site=2, Type="4", ZIndex=1, IsRequestValid=true |
| 2 | `$PREP:2,1@` (2필드) | Error 로그, IsRequestValid=false → FAIL ACK |
| 3 | `$PREP:2,4,1,99@` (4필드) | Error 로그, IsRequestValid=false → FAIL ACK |
| 4 | `$PREP:2,,1@` (Type 빈값) | Error 로그, IsRequestValid=false → FAIL ACK |
| 5 | 위 4케이스 전부 | `Parse` 가 null 반환 0건 (`return null` 은 `packet == null` 1건만 잔존) |

## Deviations from Plan

계획대로 실행됨. 아래 2건은 계획이 명시하지 않은 부수 판단이다.

**1. [Rule 2 - 문서 정확성] `PrepAckPacket` 클래스 헤더 주석 갱신**
- **발생 위치:** Task 2
- **내용:** 클래스 위 주석이 구 포맷(`$PREP_ACK:site,z_index,OK@`)을 그대로 적고 있어,
  내가 만든 변경으로 즉시 거짓 문서가 된다. 신규 포맷(`site,Type,z_index,OK|FAIL`)으로 고쳐 썼다.
- **acceptance 영향:** 없음(어떤 grep 카운트도 바뀌지 않음. `public string Type` 카운트는 주석과 무관)
- **커밋:** `6e2f506`

**2. [보고] MSBuild.exe 가 PATH 에 없음**
- 73-BUILD-VERIFY.md 는 `MSBuild.exe ...` 로 적혀 있으나 이 셸의 PATH 에는 없다.
  VS2022 Community 절대경로로 호출했고, **그 외 인자는 규격과 동일**하다.
  후속 plan 도 같은 절대경로가 필요하다.

## 알려진 제약 / 후속 plan 이 처리해야 할 것

**1. `ProcessPrep` 이 아직 `ackPacket.Type` 을 채우지 않는다 (73-04 Task 1 소관)**
`Custom/SystemHandler.cs:884 ProcessPrep` 은 이 plan 의 `files_modified` 밖이라 건드리지 않았다.
따라서 **현재 상태로는 정상 요청이라도 `$PREP_ACK:2,,1,OK` 처럼 Type 이 빈 필드로 나간다.**
직렬화 계층은 준비 완료이고, 다음 두 줄을 `ProcessPrep` 에 넣는 것이 73-04 의 일이다:
- `ackPacket.Type = packet.Type;`
- `if (!packet.IsRequestValid) { → 조기 FAIL ACK + 로그 }`

**2. 구 펌웨어 `$PREP:site,z_index,Op@` 오파싱 (D-73-05 로 수용된 위험, 고치지 말 것)**
구 포맷도 3필드라 개수로 구분되지 않는다. 들어오면 `Type=z_index`, `z_index=Op` 로 조용히
오파싱된다. 제어와 **동시 교체** 전제로 수용한 사항이며 파서 주석에 명시했다.

**3. `TryParseResetFields` 주석의 상호 참조가 stale**
RESET 파서 주석이 "TryParsePrepFields(413-416번째 줄) **하위호환 주석**이 기록한 그 위험"을
언급하는데, 그 하위호환 주석은 이번에 폐기됐다. 계획 범위 밖이라 손대지 않았다.
동작 영향 0, 문서 정확성만의 문제다.

## Threat Flags

없음 — 신규 네트워크 표면/인증 경로/스키마 변경 없음. 기존 `$PREP` 경로의 필드 해석만 변경.
계획의 위협 등록부 T-73-04 / T-73-05 / T-73-06 는 전부 `mitigate` 로 반영:

| Threat ID | 반영 |
| --------- | ---- |
| T-73-04 | Type 을 string 으로만 보관, 라우팅 해석 없음(73-04 화이트리스트 소관) |
| T-73-05 | 파서 `return false` 0건 + 호출부 `return null` 제거 → FAIL ACK 보장 |
| T-73-06 | 명명 상수 인덱스 + `== 3` 정확 비교 + 리터럴 `dataList[1]` 제거 |

## Self-Check

- `WPF_Example/TcpServer/VisionRequestPacket.cs` — FOUND
- `WPF_Example/TcpServer/VisionResponsePacket.cs` — FOUND
- 커밋 `b5a66be` — FOUND
- 커밋 `6e2f506` — FOUND
- `WPF_Example/DatumMeasurement.csproj` — 끝까지 unstaged 유지 확인 (` M` 상태)

## Self-Check: PASSED
