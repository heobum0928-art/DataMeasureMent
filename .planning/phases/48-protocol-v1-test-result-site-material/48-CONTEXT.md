# Phase 48: 제어 프로토콜 (디팜스테크 v1.0) — TEST z_index + 자재번호 + RESULT P/F/B + Site 재정합 - Context

**Gathered:** 2026-06-22
**Status:** Ready for planning

<domain>
## Phase Boundary

디팜스테크 인터페이스 TCP/IP 프로토콜 v1.0(엑셀 규격) **커맨드 계층** 구현:
- `$TEST:site,자재번호,null,z_index@` 유연 파싱
- `$RESULT:site;P|F|B;count;id=val=OK,…@` 직렬화
- Site 번호 2-PC 체계 재정합
- 자재번호를 결과 저장(Export/파일명)까지 전파

**In scope:** TEST/RESULT 파싱·직렬화, 자재번호 필드+전파, Site 2-PC 매핑, v2.6 호환 플래그.
**Out of scope (→ 다른 phase):** 조명 Shot 단위 전환 실행(요구#1 실행부), 교차-Z 측정(요구#3-2), 분단위 저장(요구#4), P/F/B 3-state 판정 엔진(Phase 49), 통신 회귀시험(Phase 50).
</domain>

<decisions>
## Implementation Decisions

### 코딩 규약 (🔒 LOCKED — 최우선)
- **D-00:** 제어/프로토콜 신규 코드는 `.planning/refs/control-sequence-coding-guideline.md` 준수 — 헝가리언 표기(b/n/f/d/sz/p/m_/g_), `if/else if/else` 만(삼항 `?:`·null병합 `??` 금지), 중첩 2단계 한도, 조건식 변수화, 매직넘버 금지(상수/enum), 함수 단일책임·30줄 한도, 동사+목적어 함수명. **이 지침이 CLAUDE.md "파일 스타일 따르기"보다 우선.** QUAL-01(Phase 47)과 정합.

### A. 자재번호 필드 위치 + 유연 파서
- **D-01:** TEST 포맷 = `$TEST:site,자재번호,null,z_index@` (자재번호 = site 다음 2번째 필드, null 예약·z_index 유지). ※ 최종 와이어 위치는 제어팀(김민우 선임) 합의 사항 — 파서가 유연하면 합의 결과 흡수.
- **D-02:** 파서는 **유연 구조화** — 고정 매직 인덱스(dataList[i]) 의존 탈피. 필드 이름/순서 매핑 + **누락 시 기본값**(자재번호 미수신 → -1 sentinel)으로 폴백. 향후 필드 추가/순서 변경 시 파서 한 곳만 수정. 헝가리언+if/else 규약 적용.

### B. Site 재정합 (2-PC)
- **D-03:** PC 역할은 **설정/레시피(SystemSetting)로 지정** (PC1=TOP/BOTTOM, PC2=SIDE_1/SIDE_2). 빌드 상수/컴파일 분기 X → 한 바이너리로 유연, 재빌드 불필요.
- **D-04:** 현행 ResourceMap(Site1=Top/2=Side/3=Bottom)을 엑셀 2-PC 체계로 재정합. Port 7701, `@` 종료, UTF-8 적용.

### C. 자재번호 전파 범위
- **D-05:** Phase 48에서 **Export/파일명까지 전파** (한 번에). 경로: TestPacket.자재번호 → SequenceContext/ActionContext → CycleResultDto → CaptureImageSaveService(파일명) + ExcelExportService(xlsx 열). "데이터 저장 원활" 충족.

### D. v2.6 ↔ v1.0
- **D-06:** v1.0 신규 구현 + 기존 **v2.6 경로는 config 플래그 뒤에 보존**(안전). 회귀 리스크 최소. (교체 아님)

### Claude's Discretion
- 유연 파서의 구체 자료구조(매핑 테이블 vs 명명 split), sentinel 값(-1/0), RESULT count 의미 매핑, v2.6/v1.0 플래그 키 이름 — 구현 시 결정.
</decisions>

<canonical_refs>
## Canonical References
**Downstream agents MUST read these before planning or implementing.**

### 프로토콜 규격 (필수)
- `.planning/refs/Vision-Protocol-v1.0.md` — 엑셀 v1.0 전문(2-PC, Site 정의, $TEST/$RESULT/$LIGHT, P/F/B, Index Table). canonical spec.
- `.planning/refs/control-sequence-coding-guideline.md` — 🔒 제어 코드 코딩 지침(헝가리언/if-else/삼항·??금지 등).

### 코드 (수정 대상)
- `WPF_Example/TcpServer/VisionRequestPacket.cs` — TEST 파싱(~259-288). 유연 파서 적용 지점.
- `WPF_Example/TcpServer/VisionResponsePacket.cs` — RESULT 직렬화.
- `WPF_Example/TcpServer/VisionServer.cs` — 송수신/세퍼레이터.
- `WPF_Example/Custom/TcpServer/ResourceMap.cs` — Site 매핑(2-PC 재정합).
- `TestPacket`(VisionRequestPacket.cs 내) — 자재번호 필드 추가.
- `WPF_Example/UI/ViewModel/CycleResultDto.cs` / `WPF_Example/Utility/CaptureImageSaveService.cs` / ExcelExportService — 자재번호 전파/저장.
- `WPF_Example/Setting/SystemSetting.cs` — PC 역할 설정.

### 프로젝트 규약
- `CLAUDE.md` — .NET 4.8/C# 7.2, HALCON, GSD 워크플로 (단 제어 코드 스타일은 D-00 우선).
</canonical_refs>

<code_context>
## Existing Code Insights
- **TEST 파싱**: VisionRequestPacket.Convert()(~259-288) dataList split, [0]=Site/[1]=TestType·null/[2]=TestID·z_index, 길이 `<3` 체크 — 매직 인덱스. → 유연 구조화로 교체.
- **TestPacket**: TestType/TestID/Identifier2 속성. 자재번호 속성 추가.
- **ResourceMap**: Site(1=Top/2=Side/3=Bottom)×TestType 매핑 — 2-PC로 재정합.
- **결과 저장**: CycleResultDto → CaptureImageSaveService(파일명/폴더, 이미 yyMMdd/HHmm 분폴더) → ExcelExportService(xlsx). 자재번호 열/파일명 추가.
- **현행 v2.6 유지 중** — 플래그로 v1.0 공존.
</code_context>

<specifics>
## Specific Ideas
- RESULT P/F/B는 엑셀 케이스 1~3(정상/중간NG/Datum실패) 예시와 byte 일치 목표. id=val=OK 형식, 측정 id(A1,C1,…)=FAI명.
- z_index 0=Datum 샷(빈 응답 `;B;0;`). Index Table(Index→LightType+Z+FAI그룹)이 z_index↔Shot 매핑 근거.
- 자재번호는 "이 자재가 몇번인지" = 데이터 저장 식별용. 향후 포맷 변경 가능성 → 유연 파서 필수.
</specifics>

<deferred>
## Deferred Ideas (다른 phase)
- **요구#1 조명 멀티샷 실행** — z_index↔Shot 매핑은 48, Shot 단위 조명전환 실행(LightHandler Shot 기반)은 후속/49. 
- **요구#3-2 교차-Z 측정** — "Z1 정보 보유→Z2 측정" 시퀀스 엔진 확장(LARGE) → 신규 phase.
- **요구#4 분단위 저장** — Capture는 이미 HHmm 분폴더. RawImageSaveService/Export 분단위 정합(SMALL) → OUT/신규.
- **Phase 49** P/F/B 3-state 판정 엔진 + Datum 빈응답 + CycleState. **Phase 50** 통신 회귀시험(제어팀 동기화).
- **$LIGHT, $SITE_STATUS, $RECIPE, $GET_RECIPE, $RESET, $ALIVE** 전체 커맨드 — 48은 TEST/RESULT 중심, 나머지 커맨드 완성은 49/추가.
</deferred>

---
*Phase: 48-protocol-v1-test-result-site-material*
*Context gathered: 2026-06-22*
