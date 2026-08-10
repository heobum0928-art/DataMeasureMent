---
phase: quick-260807-omy
plan: 01
subsystem: api
tags: [tcp-protocol, vision-server, plc-handler, wire-format]

# Dependency graph
requires:
  - phase: quick-260807-lh7
    provides: "$RESET TCP 명령 (같은 VisionServer/SystemHandler 인프라)"
provides:
  - "$TEST Type 필드 숫자 코드(0~5) 라우팅 — TryResolveSlotByType 정수 비교 전환"
  - "$ALIGN_CALIB CmdStr 숫자 코드(0~3) 처리 — AlignCalibPacket.CMD_CODE_* 단일 진실 원천 상수"
  - "$RESULT 응답 3필드 축약 (site;Type;P|F|B) — count/FAI 항목목록 와이어 제거"
affects: [tcp-server, plc-integration, protocol-v-next]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "TryParse 비숫자 가드를 코드 비교보다 먼저 배치 (out 파라미터 0-값 함정 방지)"
    - "다중 소비처를 갖는 프로토콜 상수는 단일 클래스(AlignCalibPacket)에 public const 로 선언해 값 복제 금지"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/TcpServer/ResourceMap.cs
    - WPF_Example/TcpServer/VisionRequestPacket.cs
    - WPF_Example/Custom/SystemHandler.cs
    - WPF_Example/TcpServer/VisionResponsePacket.cs

key-decisions:
  - "TryResolveSlotByType/ProcessAlignCalib 모두 Int32.TryParse 실패 시 코드 비교 이전에 조기 return false — out 파라미터가 실패 시 0이 되어 TOP/START로 오인식되는 함정을 원천 차단"
  - "AlignCalibPacket.CMD_CODE_START/STEP/END/ABORT 를 public const 로 신설해 SystemHandler.cs/VisionResponsePacket.cs 두 소비처가 동일 상수를 참조하도록 강제 (값 복제로 인한 STEP 응답 불일치 방지)"
  - "$ALIGN_TEST 는 재확인 결과 변경 불필요로 확정 — Mode 필드(dataList[2])가애초에 어떤 변수에도 대입되지 않아 텍스트/숫자 여부가 코드에 영향 없음"
  - "MSG_RESULT_ITEM_SEP 상수는 사용처가 사라졌지만 public const 라 컴파일 경고가 없어 선언은 유지, 꼬리주석만 갱신"

requirements-completed: [PROTO-VNEXT-01, PROTO-VNEXT-02, PROTO-VNEXT-03]

# Metrics
duration: 6min
completed: 2026-08-07
---

# Quick Task 260807-omy: TCP v-next 프로토콜 전환 Summary

**$TEST Type/$ALIGN_CALIB CmdStr 를 텍스트 토큰에서 숫자 코드로, $RESULT 응답을 3필드로 축약하는 제어팀 합의 프로토콜 컷오버 — 비숫자 입력 오라우팅/오인식 가드 포함**

## Performance

- **Duration:** 6 min
- **Started:** 2026-08-07T08:58:31Z
- **Completed:** 2026-08-07T09:04:44Z
- **Tasks:** 3 / 3
- **Files modified:** 4

## Accomplishments
- `$TEST` Type 필드 라우팅을 텍스트 토큰("TOP"/"BOTTOM"/"SIDE_1~4")에서 숫자 코드("0"~"5")로 전환, 라우팅 결과는 Phase 63 원본과 100% 동일하게 보존
- `$ALIGN_CALIB` CmdStr 필드를 텍스트("START"/"STEP"/"END"/"ABORT")에서 숫자 코드("0"~"3")로 전환, 3개 소비처(요청 파서/처리 분기/응답 직렬화) 일관 수정
- `$RESULT` 응답 와이어 포맷에서 count와 개별 FAI 항목목록 제거, 내부 판정 데이터(FAICount/FAIResults)는 그대로 보존

## Task Commits

Each task was committed atomically:

1. **Task 1: $TEST Type 필드 — 텍스트 토큰 → 숫자 코드 (ResourceMap 라우팅)** - `56139d0` (feat)
2. **Task 2: $ALIGN_CALIB CmdStr — 텍스트 → 숫자 코드 (상수 + 처리 + 응답 3파일 일관 수정)** - `127ede9` (feat)
3. **Task 3: $RESULT 응답 단순화 — count + 개별 FAI 항목목록 제거** - `f7ed10c` (feat)

**Plan metadata:** (별도 commit, 오케스트레이터가 처리)

## Files Created/Modified
- `WPF_Example/Custom/TcpServer/ResourceMap.cs` - `TryResolveSlotByType` 를 TYPE_CODE_TOP/BOTTOM/SIDE_MIN/SIDE_MAX 정수 상수 기반으로 교체, 비숫자 가드 선행 배치
- `WPF_Example/TcpServer/VisionRequestPacket.cs` - `AlignCalibPacket.CMD_CODE_START/STEP/END/ABORT` 상수 신설, `TryParseAlignCalibFields`/`CmdStr` 꼬리주석 갱신
- `WPF_Example/Custom/SystemHandler.cs` - `ProcessAlignCalib` 의 4개 `string.Equals` 비교를 `nCmd == AlignCalibPacket.CMD_CODE_*` 정수 비교로 교체, 비숫자 CmdStr 조기 반환 가드 추가
- `WPF_Example/TcpServer/VisionResponsePacket.cs` - `BuildAlignCalibMessage` STEP 판정을 상수 경유 정수 비교로 전환, `BuildResultMessageV1` 마지막 4줄 제거, 죽은 메서드(`MapFaiJudgement`/`BuildFaiItemsV1`) 삭제

## 변경 전/후 와이어 형식 예시

### 1) `$TEST` Type 필드

| 항목 | 변경 전 | 변경 후 |
|------|---------|---------|
| TOP | `$TEST:1,TOP,...@` | `$TEST:1,0,...@` |
| BOTTOM | `$TEST:2,BOTTOM,...@` | `$TEST:2,1,...@` |
| SIDE_1~4 | `$TEST:1,SIDE_1,...@` ~ `$TEST:1,SIDE_4,...@` | `$TEST:1,2,...@` ~ `$TEST:1,5,...@` |
| 비인식(폴백) | 텍스트 토큰 불일치 → Site 정수 폴백 | 비숫자 또는 6 이상/음수 → Site 정수 폴백 (동일 안전망) |

### 2) `$ALIGN_CALIB` CmdStr (요청/응답)

| 동작 | 변경 전 요청 | 변경 후 요청 | 변경 전 응답 | 변경 후 응답 |
|------|--------------|--------------|--------------|--------------|
| START | `$ALIGN_CALIB:BOTTOM,START@` | `$ALIGN_CALIB:BOTTOM,0@` | `$ALIGN_CALIB:BOTTOM,START,OK@` | `$ALIGN_CALIB:BOTTOM,0,OK@` |
| STEP | `$ALIGN_CALIB:BOTTOM,STEP@` | `$ALIGN_CALIB:BOTTOM,1@` | `$ALIGN_CALIB:BOTTOM,STEP,N,OK@` | `$ALIGN_CALIB:BOTTOM,1,N,OK@` |
| END | `$ALIGN_CALIB:BOTTOM,END@` | `$ALIGN_CALIB:BOTTOM,2@` | `$ALIGN_CALIB:BOTTOM,END,OK@` | `$ALIGN_CALIB:BOTTOM,2,OK@` |
| ABORT | `$ALIGN_CALIB:BOTTOM,ABORT@` | `$ALIGN_CALIB:BOTTOM,3@` | `$ALIGN_CALIB:BOTTOM,ABORT,OK@` | `$ALIGN_CALIB:BOTTOM,3,OK@` |
| 비숫자 입력 | 텍스트 불일치 → 알 수 없는 CmdStr 로그 + FAIL | 숫자 아님 → 즉시 FAIL 응답 (START 오인식 없음, `PickerCal.Reset()` 미실행) | — | `$ALIGN_CALIB:BOTTOM,XYZ,NG@` |

### 3) `$RESULT` 응답

| 항목 | 변경 전 | 변경 후 |
|------|---------|---------|
| 일반 FAI 사이클 | `RESULT:1;0;P;3;Edge1=1.234=OK,Edge2=2.345=OK,Edge3=0.987=NG@` | `RESULT:1;0;P@` |
| Datum 샷(FAICount=0) | `RESULT:1;0;B;0;@` (trailing `;` 존재) | `RESULT:1;0;B@` (trailing `;` 소멸) |

내부 `TestResultPacket.FAICount`/`FAIResults`/`MapCycleJudgement` 는 전부 무변경 — UI 검사결과 리스트와 엑셀 export 는 계속 개별 측정값을 소비한다.

## $ALIGN_TEST 변경 불필요 판정 근거

`WPF_Example/TcpServer/VisionRequestPacket.cs` 의 `TryParseAlignTestFields`(389번째 줄, 400번째 줄 `// dataList[2]=모드(skip)`) 를 재확인한 결과, 중간 Mode 필드(`dataList[2]`)는 파싱 시 주석만 있고 **어떤 변수에도 대입되지 않는다.** `AlignTestPacket` 클래스에도 대응 프로퍼티가 없다. 따라서 이 필드의 값이 텍스트든 숫자든 코드 동작에 영향을 주지 않으므로, `$ALIGN_TEST`/`TryParseAlignTestFields`/`ProcessAlignTest`/`BuildAlignResultMessage` 는 이번 컷오버에서 **손대지 않았다** — `AlignTarget`("TRAY"/"BOTTOM") 도 이번 규격 변경 대상이 아니라 3곳(`VisionRequestPacket.cs:402` / `Custom/SystemHandler.cs:329` / `VisionResponsePacket.cs:364`)에서 계속 문자열 "BOTTOM" 비교로 남아있다.

## 삭제된 죽은 코드

| 메서드 | 위치(변경 전) | 삭제 근거 |
|--------|----------------|-----------|
| `MapFaiJudgement(FAIResultData faiData)` | `VisionResponsePacket.cs` 319-327번째 줄 | `BuildFaiItemsV1` 에서만 호출됨 — 저장소 전체 grep 확인, 다른 호출부 0건 |
| `BuildFaiItemsV1(TestResultPacket testPacket)` | `VisionResponsePacket.cs` 330-349번째 줄 | `BuildResultMessageV1` 에서만 호출됨 — Task 3(a)로 그 호출부가 제거되어 저장소 전체 호출부 0건 확정 |

삭제 후 저장소 전체 grep(`BuildFaiItemsV1` / `MapFaiJudgement`, `--include=*.cs`)에서 주석 포함 0건 확인 완료 (G1 게이트).

## Decisions Made
- 비숫자 가드를 `Int32.TryParse` 직후, 코드 비교보다 반드시 먼저 배치 — `TryParse` 실패 시 out 파라미터가 0이 되어 쓰레기 입력이 TOP(`TryResolveSlotByType`)/START(`ProcessAlignCalib`)로 조용히 오인식되는 함정을 정적 게이트(행번호 순서 확인)로 강제 방지
- `AlignCalibPacket.CMD_CODE_*` 를 `public const int` 로 단일 클래스에 선언 — `Custom/SystemHandler.cs` 와 `VisionResponsePacket.cs` 두 파일이 값을 복제하지 않고 동일 상수를 참조하도록 강제, STEP 응답 불일치 위험 제거
- `MSG_RESULT_ITEM_SEP` 상수는 사용처가 사라졌지만 `public const char` 라 컴파일 경고가 없어 선언은 유지 (꼬리주석만 "미사용" 취지로 갱신)

## Deviations from Plan

None - plan executed exactly as written. 모든 3개 태스크가 plan interfaces/scope_boundaries 대로 정확히 구현되었고, 각 태스크의 정적 검증 게이트가 전부 PASS 했다.

## Issues Encountered

Bash 도구 환경(Git Bash/MSYS)에서 plan의 `<verify>` 블록에 적힌 `//p:` 이중 슬래시 MSBuild 스위치가 일부 스위치(특히 `$TEMP` 변수를 포함한 `OutputPath`/`BaseIntermediateOutputPath`)에서 일관되게 변환되지 않아 `MSB1001`/`MSB1008` 오류가 발생했다. `MSYS_NO_PATHCONV=1` 환경변수로 MSYS 자동 경로변환을 비활성화하고 단일 슬래시 `/p:` 스위치를 사용해 우회했다 — 코드 변경이나 게이트 판정 기준 자체는 변경되지 않았고, 모든 게이트의 최종 판정(`error CS` 0건, `Build succeeded` 상당)은 plan이 요구한 기준 그대로 충족되었다.

## User Setup Required

None - no external service configuration required.

## Known Stubs

None.

## Threat Flags

None - 이번 변경은 계획된 4개 파일 범위 내에서만 발생했고, `<threat_model>` 에 이미 등록된 표면(라우팅 파싱, ALIGN_CALIB 상태머신, RESULT 응답) 외의 신규 네트워크 엔드포인트/인증 경로/파일접근/스키마 변경은 없다.

## 실기 UAT 미수행분 (사용자/오케스트레이터 승인 대기)

이 plan 은 정적 검증(각 태스크 게이트 + MSBuild Debug/x64 실빌드)까지만 수행했다. 아래 실기 UAT 는 **범위 밖**이며 사용자 승인 대기 상태다:

- `$TEST:1,0,...@` → TOP 시퀀스 기동 / `$TEST:2,1,...@` → BOTTOM 기동 / `$TEST:1,2,...@` → SIDE 기동
- `$TEST:1,9,...@`(범위 밖 숫자) → Site 폴백 라우팅, 무응답 없음 확인
- `$ALIGN_CALIB:BOTTOM,0@` → OK, `,1@` → StepNo 포함 응답, `,2@`/`,3@` → OK, `,XYZ@`(비숫자) → FAIL 응답 + Error 로그 확인
- 사이클 완료 시 `$RESULT:1;0;P@` 형태(3필드)로만 송신되는지 실제 TCP 캡처로 확인
- UI 검사결과 리스트와 엑셀 export 에 개별 측정값이 여전히 표시되는지 확인 (내부 데이터 보존 증명)

## Next Phase Readiness
- 4개 파일 모두 커밋 완료(`56139d0`/`127ede9`/`f7ed10c`), Debug/x64 실빌드 PASS
- 다음 단계는 위 실기 UAT — 제어팀과 함께 실제 PLC/핸들러로 와이어 캡처 검증 권장
- 워킹트리에는 사용자의 별도 실HW 실험 변경(`WPF_Example/DatumMeasurement.csproj` SIMUL_MODE 제거)만 미커밋 상태로 남아있으며, 이번 plan 과 무관하므로 손대지 않았다

---
*Phase: quick-260807-omy*
*Completed: 2026-08-07*

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/TcpServer/ResourceMap.cs
- FOUND: WPF_Example/TcpServer/VisionRequestPacket.cs
- FOUND: WPF_Example/Custom/SystemHandler.cs
- FOUND: WPF_Example/TcpServer/VisionResponsePacket.cs
- FOUND: .planning/quick/260807-omy-tcp-v-next-test-type-align-calib-cmdstr-/260807-omy-SUMMARY.md
- FOUND commit: 56139d0
- FOUND commit: 127ede9
- FOUND commit: f7ed10c
