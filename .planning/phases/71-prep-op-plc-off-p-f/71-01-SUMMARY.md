---
phase: 71-prep-op-plc-off-p-f
plan: 01
subsystem: api
tags: [tcp-protocol, vision-server, prep-command, wire-format]

# Dependency graph
requires: []
provides:
  - "$PREP wire format collapsed to site,z_index (2 fields), 구 3필드 요청도 하위호환 파싱"
  - "$PREP_ACK wire format collapsed to site,z_index,OK|FAIL (3 fields, Op echo 제거)"
  - "ProcessPrep 단일 경로 (ON/OFF 분기 제거) — 항상 ApplyPrepToSequences(z_index)"
  - "PrepPacket.Op / PrepAckPacket.Op 프로퍼티 완전 제거, 전 리포 참조 0개"
affects: [71-02, 71-03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Lenient wire-field parsing (D-71-01): dataList.Length >= N 유지, 초과 필드는 읽지 않고 무시 — 엄격 거부는 무응답(라인 정지) 리스크가 더 큼"
    - "삭제 breadcrumb: `// <이름> 제거 //YYMMDD hbk <이유>` 관례 유지 (commit fbe05c8 선례)"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/SystemHandler.cs
    - WPF_Example/TcpServer/VisionRequestPacket.cs
    - WPF_Example/TcpServer/VisionResponsePacket.cs

key-decisions:
  - "D-71-01 (locked, planner 확정): 관대한 파싱 — dataList.Length >= 2 유지, 3번째 필드(구 Op)가 와도 코드에서 아예 읽지 않는다. 엄격 거부는 파싱 실패→null 반환→무응답→PLC 무한 대기(라인 정지)를 유발하므로 채택하지 않음."
  - "TurnOffPrepLights() 는 이 plan 이후 호출자 0개가 되지만 삭제하지 않음 — CONTEXT.md locked decision. 소등 훅은 71-02 에서 InspectionSequence 쪽에 별도로 붙는다."

patterns-established:
  - "$PREP/$PREP_ACK 필드 삭제 시 breadcrumb 주석(Phase 71) 남기는 관례를 3개 파일 전체에 일관 적용"

requirements-completed: [PROTO-PREP-01]

# Metrics
duration: 6min
completed: 2026-08-06
---

# Phase 71 Plan 01: $PREP Op 필드 제거 (wire 포맷 + 처리 로직) Summary

**`$PREP`/`$PREP_ACK` TCP 프로토콜에서 Op(ON/OFF) 필드를 wire 포맷과 코드 양쪽에서 완전히 제거하고, `ProcessPrep` 을 ON/OFF 분기 없는 단일 경로로 평탄화했다 (D-71-01 하위호환 규칙 적용).**

## Performance

- **Duration:** 6 min
- **Started:** 2026-08-06T12:49:31Z
- **Completed:** 2026-08-06T12:55:14Z
- **Tasks:** 2 (계획된 2개 모두 완료)
- **Files modified:** 3

## Accomplishments
- `$PREP` 수신 파서(`TryParsePrepFields`)가 2필드(`site,z_index`)만 읽도록 단순화, 구 3필드 요청도 성공 파싱(D-71-01)
- `$PREP_ACK` 직렬화(`BuildPrepAckMessage`)에서 Op echo 제거 → `site,z_index,OK|FAIL` 3필드로 축소
- `ProcessPrep` 의 ON/OFF `if/else` 분기 제거 → 항상 `ApplyPrepToSequences(packet.ZIndex)` 단일 경로 (commit `fbe05c8` 의 pre-Op 형태로 회귀)
- `PrepPacket.Op` / `PrepAckPacket.Op` 프로퍼티 삭제 + 전 리포지토리 참조 0개 확인 (`DebugManualZTrigger` 의 숨은 사용처 포함)
- `TurnOffPrepLights()` 를 호출자 0개인 채로 의도적으로 유지 (CONTEXT.md locked decision — 71-02 가 별도 소등 훅을 InspectionSequence 에 붙일 예정)

## Task Commits

1. **Task 1: ProcessPrep 단일 경로화 + Op 사용처 전량 제거 (SystemHandler)** - `f0d9f48` (feat)
2. **Task 2: wire 포맷에서 Op 제거 — 파서 2필드화 + ACK Op echo 제거 + 두 Op 프로퍼티 삭제 (D-71-01)** - `342cfda` (feat)

_두 커밋 모두 msbuild Debug/x64 빌드 PASS 이후 커밋됨._

## Files Created/Modified
- `WPF_Example/Custom/SystemHandler.cs` — `ProcessPrep` 단일 경로화, `DebugManualZTrigger` 의 `prepPacket.Op = 1;` 제거, `TurnOffPrepLights()` 유지 사유 주석 추가
- `WPF_Example/TcpServer/VisionRequestPacket.cs` — `TryParsePrepFields` Op 파싱 블록 제거, `PrepPacket.Op` 프로퍼티 삭제
- `WPF_Example/TcpServer/VisionResponsePacket.cs` — `BuildPrepAckMessage` Op echo 줄 제거, `PrepAckPacket.Op` 프로퍼티 삭제

## Wire Format 변경 전/후 (핸들러 팀 전달용)

| 메시지 | 변경 전 (v3.0, Op 포함) | 변경 후 (Phase 71, Op 제거) |
|---|---|---|
| `$PREP` 요청 | `$PREP:site,z_index,Op@` (Op=1 ON / 0 OFF, 선택 필드) | `$PREP:site,z_index@` (2필드 고정) |
| `$PREP_ACK` 응답 | `$PREP_ACK:site,z_index,Op,OK@` / `...,Op,FAIL@` (4필드) | `$PREP_ACK:site,z_index,OK@` / `...,FAIL@` (3필드) |

**예시:**
- 이전: `$PREP:1,3,1@` (ON) → `$PREP_ACK:1,3,1,OK@` / `$PREP:1,3,0@` (OFF) → `$PREP_ACK:1,3,0,OK@`
- 이후: `$PREP:1,3@` → `$PREP_ACK:1,3,OK@` (항상 이 형태 하나뿐, ON/OFF 구분 없음)

**하위호환(D-71-01):** 구 펌웨어가 여전히 `$PREP:1,3,1@` 처럼 3필드로 보내도 파서가 `dataList.Length >= 2` 조건만 검사하므로 파싱은 성공한다. 3번째 필드는 코드가 아예 읽지 않고 무시되며, 결과적으로 "해당 z_index 조명 점등"으로만 처리된다(과거의 명시적 `Op=0` OFF 요청도 이제 점등으로 해석됨 — 측정 정확도에는 무영향, 근거는 아래 참조). 응답은 항상 신형 3필드(`OK`/`FAIL`, Op echo 없음)로 나가므로, 구 펌웨어가 4필드 ACK 파서를 그대로 쓰고 있다면 ACK 해석이 깨질 수 있다 — 이 부분은 threat model T-71-04 에서 "제어팀 문서(`디팜스테크_Vision_Protocol_v1.3.xlsx`)가 이미 3필드로 갱신되어 있음을 확인"으로 mitigate 처리했고, 실제 핸들러 동기화 여부는 71-03 UAT 에서 실기로 확인 예정이다.

## D-71-01 (하위호환 결정) 요약

**결정:** 관대한 파싱 — `dataList.Length >= 2` 를 그대로 유지하고, 3번째 필드(구 Op)가 오더라도 코드에서 아예 `dataList[2]` 를 참조하지 않는다. 필드 수 초과를 파싱 실패로 처리하지 않는다.

**근거:** `VisionRequestPacket.cs` 의 파싱 실패(`return false`)는 호출부에서 `return null` 로 이어지고, null 패킷은 TCP 응답 자체가 나가지 않는다. 만약 3필드 요청을 엄격 거부했다면 구 펌웨어 PLC 는 `$PREP_ACK` 를 영원히 기다리게 되어 설비가 정지한다(자초한 DoS). 반대로 관대한 파싱을 택하면 구 클라이언트가 명시적으로 `Op=0`(OFF)을 보내는 경우에도 이제 "해당 z_index 점등"으로 동작하지만, 모든 `$TEST` 앞에는 항상 `$PREP` 가 선행하고 `ApplyShotLightsInternal` 이 촬영 직전 조명 상태를 매번 선언적으로 재적용하므로 측정 정확도에는 영향이 없다 — 유일한 잔여 영향은 "사이클 종료 시점까지 LED 가 계속 켜져 있다"는 것뿐이며, 그 소등은 71-02 가 사이클 P/F 확정 시점에 자동으로 수행한다.

## 제거된 `.Op` 사용처 최종 목록 (파일:라인, 편집 전 기준)

| # | 파일:라인 (편집 전) | 내용 |
|---|---|---|
| 1 | `WPF_Example/Custom/SystemHandler.cs:799` | `ackPacket.Op = packet.Op;` (ProcessPrep, Op echo 대입) |
| 2 | `WPF_Example/Custom/SystemHandler.cs:802` | `bool bIsOn = packet.Op != 0;` (ProcessPrep, ON/OFF 분기 조건) |
| 3 | `WPF_Example/Custom/SystemHandler.cs:841` | `prepPacket.Op = 1;` (DebugManualZTrigger, CONTEXT.md 에 없던 숨은 사용처) |
| 4 | `WPF_Example/TcpServer/VisionRequestPacket.cs:435` | `if (bOpOk) { prepPacket.Op = nOp; }` (TryParsePrepFields, Op 파싱 블록) |
| 5 | `WPF_Example/TcpServer/VisionRequestPacket.cs:581` | `public int Op { get; set; } = 1;` (PrepPacket 프로퍼티 선언) |
| 6 | `WPF_Example/TcpServer/VisionResponsePacket.cs:447` | `szMsg += packet.Op.ToString();` (BuildPrepAckMessage, Op echo 직렬화) |
| 7 | `WPF_Example/TcpServer/VisionResponsePacket.cs:731` | `public int Op { get; set; } = 1;` (PrepAckPacket 프로퍼티 선언) |

전체 리포지토리(`bin/`, 서드파티 산출물 제외) 대상 `rg -n "\.Op\b|public int Op" WPF_Example/` 재검색 결과 잔여 참조 0개 확인(유일한 유사 매치는 무관한 `RecipeGetPacket.Option` 프로퍼티).

## `TurnOffPrepLights()` 를 호출자 0개인 채로 의도적으로 남긴 이유

`ProcessPrep` 의 OFF 분기가 제거되면서 `SystemHandler.TurnOffPrepLights()` 는 이 plan 완료 시점에 유일한 호출자를 잃는다. 그럼에도 삭제하지 않은 이유는 CONTEXT.md 의 locked decision — "`TurnOffPrepLights()` 와 `InspectionSequence.TurnOffShotLights()` 두 메서드 자체는 그대로 재사용(삭제하지 말 것)" — 때문이다. 소등의 **호출 시점**만 "PLC 가 Op=0 을 명시 요청할 때"에서 "사이클이 P/F 로 확정되는 순간"으로 옮겨질 뿐이며, 새 호출 지점은 71-02 에서 `InspectionSequence.cs` (`BuildScopedResponse` 등)에 직접 `TurnOffShotLights()` 를 호출하는 형태로 추가될 예정이다(이 plan 은 `InspectionSequence.cs` 를 한 글자도 건드리지 않음, 아래 anti-goal 검증 참조). `TurnOffPrepLights()` 는 향후 "PC 단위 전 시퀀스 강제 소등"(예: 비상정지, 레시피 전환) 같은 용도로 재사용될 가능성을 남겨둔 채 유지된다. C# 은 미사용 `private` 메서드에 컴파일 경고를 내지 않으므로 이 상태로도 빌드 경고는 늘지 않았다(실측 확인 — 아래 빌드 검증 참조).

## 빌드 검증 경로

**정상 빌드(정상 경로) — 스크래치 OutDir 불필요, 잠김 없음.**
```
MSYS_NO_PATHCONV=1 "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Build -v:minimal
```
Task 1 커밋 전, Task 2 커밋 전 총 2회 실행 — 둘 다 `DatumMeasurement -> C:\Info\Project\DataMeasurement\WPF_Example\bin\x64\Debug\DatumMeasurement.exe` 로 정상 종료(0 errors). 경고는 기존에 이미 존재하던 4종(`CS0618` x4, `CS0162` x1, 이 plan 과 무관한 legacy 마이그레이션/도달불가 코드 경고)만 나타났고 새로운 경고는 없었다.

*(Bash 도구에서 `/p:` 형태의 슬래시 플래그가 Git Bash 경로 변환 규칙에 걸려 `MSB1008`/`MSB1001` 오류를 냈다 — `MSYS_NO_PATHCONV=1` 환경변수 + `-p:` dash 스타일 스위치로 우회했다. 코드/빌드 산출물과 무관한 셸 환경 이슈이며 deviation 규칙 적용 대상 아님.)*

## Decisions Made
- D-71-01 하위호환 규칙(관대한 파싱)을 plan의 locked decision 그대로 적용, 재논의하지 않음.
- `TurnOffPrepLights()` 를 dead-caller 상태로 유지(CONTEXT.md locked decision 재확인).

## Deviations from Plan

None — plan 이 지정한 <action> 블록의 목표 코드 형태를 문자 그대로 적용했고, 4개 태스크 acceptance_criteria 및 plan-level verification 8개 항목 전부 grep/build 로 통과 확인했다.

**참고(오탐 아님, 코드 이슈 아님):** plan 의 verification 항목 3 (`rg -n "TryParsePrepFields" -A 25 ... | rg "dataList\[2\]"` → 출력 없음 기대)은 실제로는 breadcrumb 주석 한 줄(`// Op 파싱 블록 제거 //260806 hbk Phase 71: dataList[2]=구 Op(수신해도 읽지 않음)`)에 매치되어 1건 출력된다. 이 주석은 plan 의 `<action>` 절이 명시한 "최종 형태" 코드 블록에 문자 그대로 포함된 필수 breadcrumb이며(계획서가 스스로 요구한 텍스트), 실제 코드에서 `dataList[2]` 를 배열 인덱싱으로 읽는 곳은 없다(직접 확인: 주석 줄만 매치, 그 외 non-comment 줄에서 `dataList[2]` 를 찾으면 전부 `TryParsePrepFields` 밖의 무관한 파서(Light/Test)에서 나온다). 즉 "3번째 필드를 코드가 읽지 않는다"는 의도된 불변식은 만족되며, grep 패턴이 주석 텍스트까지 함께 매치하는 것은 검증 스크립트 문구의 부수 효과일 뿐 실제 결함이 아니다.

## Issues Encountered
- Bash 도구에서 `msbuild ... /p:...` 형태 명령이 Git Bash 의 경로 변환 규칙과 충돌해 `MSB1008: 프로젝트를 하나만 지정할 수 있습니다` 오류 발생 → `MSYS_NO_PATHCONV=1` + `-p:` 스위치로 해결. 코드/빌드 결과에는 영향 없음.

## User Setup Required

None - 외부 서비스 설정 불필요.

## Next Phase Readiness
- `$PREP`/`$PREP_ACK` wire 포맷과 `ProcessPrep` 단일 경로가 확정되어 71-02(조명 자동 소등 훅 — `InspectionSequence.cs` `BuildScopedResponse`/`BuildDatumShotResponse`에 `TurnOffShotLights()` 호출 추가)를 시작할 준비가 됐다.
- `TurnOffShotLights()`(InspectionSequence.cs)와 `TurnOffPrepLights()`(SystemHandler.cs)는 이 plan에서 무수정으로 보존되어 71-02가 그대로 재사용 가능.
- 71-03(UAT — 실기 PLC 연동 확인, 특히 T-71-04 핸들러 4필드→3필드 ACK 파서 동기화 여부)은 71-02 완료 후 진행 필요.
- 블로커 없음.

---
*Phase: 71-prep-op-plc-off-p-f*
*Completed: 2026-08-06*

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/SystemHandler.cs
- FOUND: WPF_Example/TcpServer/VisionRequestPacket.cs
- FOUND: WPF_Example/TcpServer/VisionResponsePacket.cs
- FOUND: .planning/phases/71-prep-op-plc-off-p-f/71-01-SUMMARY.md
- FOUND commit: f0d9f48
- FOUND commit: 342cfda
