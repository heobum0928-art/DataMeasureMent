# Phase 71: $PREP Op 필드 제거 + 조명 소등 자동화 - Context

**Gathered:** 2026-08-06
**Status:** Ready for planning
**Source:** 사용자가 파일/라인 레벨 상세 스펙을 직접 제공(discuss급 상세도) — 아래는 그 스펙을 CONTEXT.md 형식으로 옮긴 것, 재해석 없음.

<domain>
## Phase Boundary

`$PREP:site,z_index,Op@`(Op=1 ON/0 OFF) 프로토콜에서 Op 필드를 완전히 제거한다(항상 ON 의미로 통일). 대신 사이클이 P 또는 F로 확정되는 순간 비전이 자동으로 전체 조명을 소등한다. PLC는 더 이상 명시적으로 OFF를 요청할 필요가 없어진다.

**왜:** `ApplyShotLightsInternal`(`InspectionSequence.cs:650`)이 매 호출마다 모든 채널(Ring1-6/Back/Coax/Ring7/Bar)을 "이 shot 설정에 맞게 켜거나 끄거나" 완전히 선언적으로 재적용하기 때문에, 한 사이클 안에서 z_index가 바뀔 때마다 안 쓰는 채널은 이미 자동으로 꺼진다. 즉 Op=1(ON)은 사실상 항상 필요하고, Op=0(OFF)은 "사이클 완전히 끝난 뒤 전부 소등"이라는 별도 목적(측정 정확도와 무관, LED 수명/안전 목적)으로만 쓰인다.

</domain>

<decisions>
## Implementation Decisions

### TCP 프로토콜 — 요청 파서
- `WPF_Example/TcpServer/VisionRequestPacket.cs`의 `TryParsePrepFields`(:415-439) — 지금 `dataList.Length>=3`이면 3번째 필드를 Op로 파싱하는 선택적 로직(:430-436)을 제거. 파서는 항상 site+z_index 2필드만 받도록 단순화(`$PREP:site,z_index@`).
- `PrepPacket.Op` 프로퍼티 — 더 이상 안 쓰이면 제거(사용처 전부 정리 후).

### TCP 프로토콜 — 처리 로직
- `WPF_Example/Custom/SystemHandler.cs`의 `ProcessPrep`(:788-821) — 지금 `bIsOn = packet.Op != 0`으로 ON/OFF 분기하는 if-else(:802-819)를 제거하고, 항상 `ApplyPrepToSequences(packet.ZIndex)` 경로만 타도록 단순화.

### TCP 프로토콜 — 응답 직렬화
- `WPF_Example/TcpServer/VisionResponsePacket.cs`의 `BuildPrepAckMessage`(:438-459) — Op echo 2줄(:446-447, `packet.Op.ToString()` + 그 앞 구분자) 제거. 결과 포맷: `$PREP_ACK:site,z_index,OK@` / `$PREP_ACK:site,z_index,FAIL@`
- `PrepAckPacket.Op` 프로퍼티도 더 이상 안 쓰이면 제거.

### 조명 자동 소등 — 재사용할 것
`TurnOffPrepLights()`(SystemHandler.cs:888-905)와 `InspectionSequence.TurnOffShotLights()`(:636-643) 메서드 자체는 그대로 재사용(삭제하지 말 것) — **호출 시점만 "PLC가 Op=0을 보낼 때"에서 "사이클이 P/F로 확정되는 순간"으로 옮긴다.**

### 조명 자동 소등 — 훅 위치 (중요: 종료 경로가 2곳)
사이클이 P/F로 끝나는 경로가 2곳이라 둘 다 훅이 필요함:
1. `ApplyCycleJudgement`(InspectionSequence.cs:1591-1612) — 정상 흐름, `bIsLastIndex==true`일 때 `packet.Result`를 P(OK) 또는 F(NG)로 확정(:1600-1611)
2. `TryApplyCrossZDatumImmediateFail`(:1393-1424) — Datum(Index 0) 검출 즉시 실패 시 조기 종료, `packet.IsBuffer=false; packet.Result=NG;`로 바로 확정(:1418-1420)

**권장 구현 방식**: 두 함수 각각에 훅을 따로 넣지 말고, 이 둘을 호출하는 `BuildScopedResponse`(:1338~) 안에서 **두 호출이 다 끝난 뒤 `packet.IsBuffer`가 false인지 한 번만 확인**해서 `TurnOffShotLights()`를 호출하는 단일 지점으로 만들 것 — 두 함수 개별 수정보다 누락 위험이 적음. `BuildDatumShotResponse`(:1252)나 다른 조기 응답 경로가 있다면 그것도 같은 조건(`IsBuffer==false`)으로 커버되는지 확인.

### Claude's Discretion
- 기존 `$PREP:site,z_index,Op@`(3필드, 구 클라이언트) 요청이 왔을 때: 파서가 3번째 필드를 완전히 무시하고 앞 2필드만 보게 할지, 필드 개수 불일치로 파싱 실패시킬지 — planner가 하위호환 범위를 판단해 결정(완료 기준에 명시된 확인 항목).

### 확정: 프로토콜 문서는 이미 목표 상태로 갱신되어 있음 (2026-08-06, 사용자 제공)
**정확한 경로**: `Z:\DOC_EXPORT\2026-08-06[17.51.59]\디팜스테크_Vision_Protocol_v1.3.xlsx` (패턴매퍼가 추측한 `C:\Info\Doc\2.디팜스테크\...` 경로는 틀림 — 무시할 것).

사용자가 이 문서의 PREP/PREP_ACK 행 스크린샷을 직접 제공, 내용이 이 CONTEXT.md의 목표 상태와 완전히 일치함을 확인:
- `PREP ★`: 필드 `$PREP:site,z_index@` / 설명: "site: PC 번호(PC1=1,PC2=2), z_index: Z축 인덱스(0,1,2...) 0=Datum 샷" / 응답 `$PREP_ACK:site,z_index,OK@` 또는 `$PREP_ACK:site,z_index,FAIL@` / 비고: "비전이 z_index 맞는 조명 자동 선정 / ACK = 조명 안정/촬영 준비 완료 신호 / HW 트리거 전환 시에도 PREP 유지(트리거만 교체)" / 예시: `[조명 ON] $PREP:1,0@ → $PREP_ACK:1,0,OK@`, `[조명 OFF] $PREP:1,0@ → $PREP_ACK:1,0,OK@` (ON/OFF 예시가 동일 메시지 형태 — Op 필드가 없으므로 당연함).
- `PREP_ACK`: 필드 `$PREP_ACK:site,z_index,OK@` / `$PREP_ACK:site,z_index,FAIL@` / 설명: "z_index: Echo, OK: 조명 점등/소등 완료, FAIL: 해당 z_index Shot 없음 등" / 트리거: "$PREP 수신 후" / 비고: "ACK = 촬영/소등 완료 확인(HW 트리거 준비)"

**결론(locked)**: 이 xlsx 파일 자체는 이미 목표 상태 — planner는 이 문서를 "편집"하는 태스크를 만들 필요 없다. 대신 이 문서를 코드 변경의 **근거/검증 기준**으로 참조만 하면 된다(코드가 이 문서와 일치하는지 확인하는 용도). 문서 편집이 실제로 필요한지(예: 리포지토리 내 다른 사본이 구버전으로 남아있는지)는 발견되면 planner가 별도로 플래그.

</decisions>

<specifics>
## Specific Ideas

### 코딩 컨벤션
삼항연산자 금지(if-else), 헝가리언 표기법, 기존 try/catch 패턴 유지.

### 비목표 (스코프 밖)
- `$PREP`/`$TEST` 자체를 합치는 것은 이번 범위 아님(별도로 논의했고 HW 트리거 호환성 때문에 기각됨) — PREP은 여전히 TEST와 분리된 별도 메시지로 유지.
- 조명 안정화 대기(ACK 타이밍) 로직은 무수정.

### 완료 기준
- Debug/x64 빌드 PASS
- `$PREP:1,3@`(Op 없이) → `$PREP_ACK:1,3,OK@` 정상 동작
- 한 사이클 내 z_index 여러 번 전환 시 조명이 매번 올바르게 전환(기존 동작 회귀 0)
- 정상 종료(P) 후 전체 조명 소등 확인
- NG 누적 종료(F) 후 전체 조명 소등 확인
- Datum 즉시실패(F) 경로에서도 전체 조명 소등 확인 — 이 경로가 누락되기 쉬우니 반드시 별도 테스트
- 기존 `$PREP:site,z_index,Op@`(3필드, 구 클라이언트) 요청 처리 방식 결정(하위호환 범위 확인)
- 프로토콜 문서(디팜스테크_Vision_Protocol_vX.X.xlsx)의 PREP 관련 행(포맷/파라미터/예시)도 Op 제거 반영해서 갱신

</specifics>

<canonical_refs>
## Canonical References

### 프로토콜 사양 (검증 기준)
- `Z:\DOC_EXPORT\2026-08-06[17.51.59]\디팜스테크_Vision_Protocol_v1.3.xlsx` — PREP/PREP_ACK 행이 이미 목표 상태(Op 없음)로 갱신되어 있음. 코드 변경의 정답지로 사용할 것.

</canonical_refs>

---

*Phase: 71-prep-op-plc-off-p-f*
*Context gathered: 2026-08-06 (user-provided detailed spec, transcribed verbatim into this template)*
