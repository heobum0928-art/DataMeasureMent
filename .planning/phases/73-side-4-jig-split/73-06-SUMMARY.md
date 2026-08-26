---
phase: 73-side-4-jig-split
plan: 06
subsystem: test-client
tags: [protocol, prep, test-client, phase73, CommunicationTest]
requires:
  - "73-02: $PREP:site,Type,z_index@ 3필드 포맷 확정"
provides:
  - "ResolveMaxZIndexByType — Type↔z 상한 매핑(0/1=0, 2=2, 3=3, 4=4, 5=3)"
  - "자동반복이 Type 별 z 범위로만 순회 (전체 모드 최대 16회 송신)"
  - "$PREP 3필드 송신 — 73-07 SIMUL 검증의 선행 조건"
  - "수동 송신(raw) 경로 — 프로토콜 예외 케이스 3종 송신 수단"
affects:
  - "73-07 (SIMUL 검증): 이 클라이언트로 지그별 개별 P/F 및 예외 케이스를 확인"
tech-stack:
  added: []
  patterns:
    - "전통 switch 기반 매핑 테이블 + -1 = 미지원 표시"
    - "안전 상한(MAX_Z_INDEX) 과 논리 상한(nMaxZ) 분리"
key-files:
  created: []
  modified:
    - C:/Info/Project/CommunicationTest/CommunicationTest/MainWindow.xaml.cs
decisions:
  - "MainWindow.xaml 무변경 — 자유 문자열 송신 UI(TcpSendBox + 전송 버튼)가 이미 존재하여 재사용"
  - "MAX_Z_INDEX(50) 상수는 삭제하지 않고 안전 상한(clamp)으로 존치 — 매핑 테이블 오기 방어"
  - "미지 Type 은 무한루프 대신 로그 남기고 대상 건너뜀(continue)"
metrics:
  duration: "약 25분"
  completed: 2026-08-26
  tasks: 2
  commits: 2
  files: 1
---

# Phase 73 Plan 06: TCP 테스트 클라이언트 Phase 73 대응 Summary

TCP 테스트 클라이언트(`CommunicationTest`)의 자동반복을 Type 별 z 범위(SIDE_1=0\~2 / SIDE_2=0\~3 /
SIDE_3=0\~4 / SIDE_4=0\~3)로만 돌도록 고치고, `$PREP` 송신을 `$PREP:site,Type,z_index@` 3필드로 교체했다.

## 무엇을 왜 했는가

기존 "전체" 모드는 Type 2/3/4/5 각각에 z=0\~50(실측상 z=15 부근에서 최종 P/F 수신까지) 을 돌려
**같은 검사를 4번 반복**했다. Phase 73 이후에는 지그마다 z 가 0 부터 독립 시작하고 마지막 z 에서
개별 P/F 가 나오므로, 클라이언트가 Type 별 상한을 모르면 분리 결과를 검증할 수단 자체가 없다.

또 `$PREP` 이 2필드(`site,z_index`)로 나가고 있었다. 73-02 가 서버 파서를 3필드 전용으로 재작성했으므로
클라이언트가 그대로면 **모든 $PREP 이 필드 개수 위반 → FAIL ACK** 가 된다.

## Tasks 완료

| Task | 내용 | 커밋 (CommunicationTest 저장소) |
| ---- | ---- | ---- |
| 1 | Type↔z 범위 테이블 + 3필드 `$PREP` 송신 + 삼항 2건 제거 | `69f684a` |
| 2 | 수동 송신(raw) 경로를 예외 케이스 검증 수단으로 명시 | `58ec483` |

⚠ **두 커밋은 `C:\Info\Project\CommunicationTest` 저장소에 있다.** DataMeasurement 저장소가 아니다.

## 변경 상세 — `CommunicationTest/MainWindow.xaml.cs`

### (1) `ResolveMaxZIndexByType(int nTestType)` 신설

전통 `switch` 로 Type→z 상한을 돌려준다. `-1` 은 "자동반복 대상이 아님".

| Type | 대상 | 반환 | z 범위 | 송신 횟수 |
| --- | --- | --- | --- | --- |
| 0 | TOP | 0 | 0 | 1 |
| 1 | BOTTOM | 0 | 0 | 1 |
| 2 | SIDE_1 | 2 | 0\~2 | 3 |
| 3 | SIDE_2 | 3 | 0\~3 | 4 |
| 4 | SIDE_3 | 4 | 0\~4 | 5 |
| 5 | SIDE_4 | 3 | 0\~3 | 4 |
| 그 외 | — | -1 | — | 건너뜀 |

"레시피 종속 — z 배치가 바뀌면 여기도 같이 고쳐야 한다" 주석을 테이블 위에 명시했다(T-73-21 mitigation).

### (2) z 루프 상한 분리

- `const int MAX_Z_INDEX = 50` 은 **삭제하지 않고 존치** — `nMaxZ` 가 이를 넘으면 clamp 한다.
  매핑 테이블에 오기가 들어가도 무한 루프로 번지지 않게 하는 2중 방어다.
- 미지 Type 이면 `AppendLog(...) ; continue;` 로 그 대상만 건너뛴다(전체 중단 아님).
- 루프 종료 경고 문구를 `z-index 상한(50) 도달` → `z 범위 끝(z={nMaxZ})까지 갔는데도 계속 B(진행중)
  — 최종 P/F 미수신, 레시피/판정 확인 필요` 로 교체.

### (3) `$PREP` 3필드 송신

```csharp
await _tcp.SendAndWaitAsync($"$PREP:{site},{testType},{zIndex}@", "PREP_ACK", timeoutMs, _autoCts.Token);
```

`$TEST` 는 **변경 없음** (`$TEST:{site},{testType},{material}@`).

### (4) 삼항 제거 — 실측 2건 모두

| 위치 | 변경 전 | 변경 후 |
| --- | --- | --- |
| site 결정 (구 `:218`) | `int site = isSideTarget ? 2 : 1;` | `int site = 1;` + `if (isSideTarget) { site = 2; }` |
| 라벨 (구 `:236`) | `string stageLabel = isSideTarget ? DescribeSideStage(zIndex) : null;` | `string stageLabel = null;` + `if (isSideTarget) { stageLabel = DescribeSideStage(testType, zIndex); }` |

### (5) `DescribeSideStage(int nTestType, int nZIndex)` 재작성

구 SIDE 16칸 z 배치 테이블(`Datum_3-1 촬영 (z=0,1)` … `z<=15`)을 전면 제거하고,
Phase 73 의 "z 는 지그마다 0 부터, 0\~1=Datum, 2\~=측정, 마지막 z=최종 P/F" 구조로 다시 썼다.
`ResolveMaxZIndexByType` 을 재사용해 마지막 z 를 판별한다(호출처 1건).

## Task 2 — 예외 케이스 송신 절차 (73-07 이 그대로 따라 하면 된다)

**코드 변경 없이 기존 기능을 쓴다.** 신규 UI 를 추가하지 않았다.

**UI 위치:** TCP 탭 → `전송 (Enter 또는 전송 버튼)` 그룹박스
(`MainWindow.xaml` 66\~76행: `TcpSendBox` TextBox + `전송` 버튼 + `\r\n` 체크박스)

**사용법**
1. TCP 연결(클라이언트 모드, DataMeasurement 의 `ServerPort` 기본 2505)
2. `\r\n` 체크박스는 **끈 채로 둔다** — 비전 프로토콜 종단자는 `@` 다
3. `TcpSendBox` 에 아래 문자열을 입력하고 `전송`(또는 Enter)
4. 응답은 로그창(`TcpLogBox`)에 `[RX] 원문` 으로 그대로 찍힌다
   (`Communication/TcpCommunicator.cs:142` — `Log($"[RX] {Encoding.UTF8.GetString(...)}")`)

**검증할 3종**

| # | 케이스 | 입력 문자열 | 기대 응답 |
| - | ------ | ---------- | -------- |
| 1a | 필드 개수 위반 (2필드) | `$PREP:2,1@` | `$PREP_ACK: ... FAIL@` (73-02: `IsRequestValid=false` → `IsOk` 기본 `false`) |
| 1b | 필드 개수 위반 (4필드) | `$PREP:2,4,1,99@` | `$PREP_ACK: ... FAIL@` |
| 2 | 미지 Type | `$PREP:2,9,0@` | `$PREP_ACK:2,9,0,FAIL@` |
| 3 | 범위 밖 z (M13) | `$PREP:2,2,7@` 송신 후 `$TEST:2,2,1@` | `$RESULT` 가 `B` 이고 **최종 P/F 가 나오지 않을 것** (미측정 PASS 가 나오면 M13 미구현) |

⚠ 1a/1b 는 요청이 규격 위반이라 서버가 Type/z 를 신뢰할 수 없다. 73-02 결정에 따라
**Type 필드가 빈 값으로 echo 될 수 있다** — `FAIL` 여부만 보고 판정하라.
⚠ 케이스 2 의 `FAIL` 은 73-04(라우팅) 가 미지 Type 화이트리스트를 구현해야 나온다.
이 plan 시점에서는 아직 73-04 완료 여부에 종속적이다.

**핸들러에 파싱/재조립이 없음(검증됨):** `TcpSend_Click` 본문(주석 제외)에서
`PREP|TEST|RESULT|Split|Parse|Contains` 매칭 **0건**. 입력 문자열을 그대로 `_tcp.SendAsync(msg)` 로 보낸다.

## 검증 결과 — 실행한 명령과 출력

### Task 1 acceptance (8/8 PASS)

```
$ cd "C:/Info/Project/CommunicationTest/CommunicationTest"
1) ResolveMaxZIndexByType = 3  (기대 3)
2) $PREP 3필드 = 1  (기대 1)
3) 구 2필드 $PREP 잔존 = 0  (기대 0)
4) $TEST 무변경 = 1  (기대 1)
5) isSideTarget 삼항 = 0  (기대 0)
6) int site = 1; = 1  (기대 1)
7) 구 z테이블 Datum_3-1 촬영 = 0  (기대 0)
8) MAX_Z_INDEX 상수 존치 = 1  (기대 1)
```

`_local_backup_before_pull_260811/` 무변경:
```
$ find _local_backup_before_pull_260811 -type f -newermt "2026-08-26 00:00" | wc -l
0        (총 파일 15개, 오늘 수정 0개)
$ git diff --name-only -- _local_backup_before_pull_260811/ | wc -l
0
```

### Task 2 acceptance

```
$ grep -ci 'raw\|수동 송신\|SendRaw' MainWindow.xaml.cs
1
$ sed -n '/private async void TcpSend_Click/,/^        }/p' MainWindow.xaml.cs | grep -v '^\s*//' \
    | grep -c 'PREP\|TEST\|RESULT\|Split\|Parse\|Contains'
0        (핸들러에 재조립/파싱 없음)
```

### 빌드

```
$ "/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" \
    "C:/Info/Project/CommunicationTest/CommunicationTest.sln" -nologo -t:Rebuild \
    "-p:OutDir=<SCRATCH>\out\" "-p:IntermediateOutputPath=<SCRATCH>\obj\"
    경고 0개
    오류 0개
EXIT=0
```

### 사이클 송신 횟수 (verification #1)

| 모드 | 구 | 신 |
| ---- | -- | -- |
| 전체(SIDE_1\~4) 1바퀴 최대 z 송신 | 4 × 16 = 64 | **3 + 4 + 5 + 4 = 16** |

(각 z 에서 `$PREP` 1회 + `$TEST` 1회. 중간 응답이 `B` 가 아니면 그 자리에서 사이클을 끝내므로
16 은 상한이다.)

## Deviations from Plan

**1. [Rule 3 - 블로킹] 정규 출력 경로 빌드가 파일 잠김으로 실패 → 스크래치 OutDir 로 컴파일 검증**
- **발생 위치:** Task 1 빌드 단계
- **문제:** `bin\Debug\CommunicationTest.exe` 가 실행 중인 프로세스에 잠겨 있어 복사 단계에서
  `error MSB3027` / `MSB3021` 발생 (exit 1). 잠금 주체: `Visual Studio 2026 Remote Debugger (7348),
  CommunicationTest (24000)` — **즉 테스트 클라이언트가 디버거로 실행 중**이다.
- **조치:** 프로젝트 규칙(빌드 잠김 시 프로세스 종료 금지)에 따라 프로세스를 건드리지 않고
  `-p:OutDir` / `-p:IntermediateOutputPath` 를 스크래치로 돌려 **컴파일만** 검증했다 → 경고 0 / 에러 0 / exit 0.
- **잔여:** 이 컴파일 결과가 `bin\Debug` 에 반영되지 않았다. **73-07 이 클라이언트를 쓰기 전에
  실행 중인 CommunicationTest.exe 를 사용자가 닫고 한 번 정상 빌드해야 한다.** 안 그러면
  구 2필드 `$PREP` 바이너리로 검증하게 되어 전 케이스가 FAIL ACK 로 나온다.
- **커밋 영향:** 없음(소스는 정상 커밋됨)

**2. [계획 허용 범위 내 선택] Task 2 를 "코드 변경 없음" 이 아니라 "주석만 추가" 로 마감**
- 계획은 기존 기능이 있으면 "코드 변경 없이 SUMMARY 기록만" 을 허용했다. 그대로 두면
  Task 2 의 `<automated>` 검증 `grep -ci "raw|수동 송신|SendRaw"` 이 **0** 을 반환한다.
  73-07 이 이 경로를 찾을 수 있도록 `TcpSend_Click` 위에 용도/금지사항 주석을 달았다(동작 변경 0).
- **acceptance 영향:** 위반 없음. "핸들러에 재조립/파싱 코드가 없다" 는 그대로 유지(0건 확인).

## 알려진 제약 / 후속 plan 이 처리해야 할 것

**1. `bin\Debug` 바이너리가 구 버전이다 (위 Deviation 1).** 73-07 착수 전 재빌드 필요.

**2. `ResolveMaxZIndexByType` 테이블은 레시피(FAI_1) 종속이다.**
73-03/73-05 의 레시피 z 재매김 결과가 D-73-01 표와 다르면 이 테이블도 같이 고쳐야 한다.
어긋나면 마지막 z 를 안 보내 최종 P/F 를 못 받거나(짧음), 범위 밖 z 를 보내게 된다(김).
73-07 이 실제 응답으로 교차 확인할 것(T-73-21).

**3. Top/Bottom 단일 대상 모드의 z 상한이 0 으로 좁혀졌다.**
구 코드는 `B` 응답이 오는 한 계속 z 를 올렸으나, 이제 Type 0/1 은 z=0 한 번만 보낸다.
D-73-01 의 "Top/Bottom 은 현 레시피상 전부 z=0" 에 근거한 값이다. Top/Bottom 레시피에
z>0 Shot 이 생기면 `case 0/1` 을 같이 올려야 한다.

**4. 자동반복은 `$PREP_ACK` 의 OK/FAIL 을 판정하지 않는다(기존 동작 유지).**
`SendAndWaitAsync` 가 `PREP_ACK` 수신만 확인하고 넘어간다. 조명 세팅 FAIL 을 자동으로 잡지
않으므로, 73-07 은 로그의 `[RX] $PREP_ACK...` 원문을 눈으로 확인해야 한다.

## Threat Flags

없음 — 신규 네트워크 표면/인증 경로/스키마 변경 없음. 기존 개발용 TCP 클라이언트의 송신 문자열
조립 규칙만 변경했다.

| Threat ID | 반영 |
| --------- | ---- |
| T-73-20 | accept 유지 — 자유 문자열 송신은 의도된 기능, 신규 추가 없이 기존 경로 재사용 |
| T-73-21 | mitigate — `ResolveMaxZIndexByType` 위에 "레시피 종속" 주석 명시 + 73-07 교차 확인 항목으로 기록 |

## Self-Check

파일:
- `C:/Info/Project/CommunicationTest/CommunicationTest/MainWindow.xaml.cs` — FOUND
- `C:/Info/Project/DataMeasurement/.planning/phases/73-side-4-jig-split/73-06-SUMMARY.md` — FOUND

커밋(CommunicationTest 저장소):
- `69f684a` feat(73-06): Type별 z 범위 자동반복 + $PREP 3필드 송신 — FOUND
- `58ec483` docs(73-06): 수동 송신 경로를 프로토콜 예외 케이스 검증 수단으로 명시 — FOUND

범위 준수:
- `WPF_Example/` 하위 무변경 — 이 plan 은 DataMeasurement 소스를 전혀 열지 않았다
- `D:\Data\Recipe\**` 무변경 — 앱 실행/저장 없음
- `_local_backup_before_pull_260811/` 무변경 (오늘 수정 파일 0 / 총 15)
- STATE.md / ROADMAP.md 무변경

## Self-Check: PASSED
