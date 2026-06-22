---
phase: 48-protocol-v1-test-result-site-material
verified: 2026-06-22T15:30:00Z
status: human_needed
score: 5/5 must-haves verified (all truths pass automated checks)
overrides_applied: 0
human_verification:
  - test: "실제 디팜스테크 핸들러(또는 mock_vision_client.py)로 $TEST:1,42,null,1@ 패킷 송신 후 수신된 TestPacket.IndexNumber=42 확인"
    expected: "IndexNumber=42, 파일명에 _M42 포함, xlsx 자재번호 행에 42 기록"
    why_human: "TCP 실제 통신 — 현 환경에서 디팜스테크 핸들러 없음, 실 소켓 세션 불가"
  - test: "UseProtocolV1=true, PcRole=1(PC1)로 설정 후 Site=1 $TEST 수신 → TOP 시퀀스 동작 / Site=2 → BOTTOM 시퀀스 동작 확인"
    expected: "PC1 Site1=TOP 자원, Site2=BOTTOM 자원으로 라우팅"
    why_human: "시퀀스 실행 + 실 하드웨어 / SIMUL 카메라 동작 필요 — 코드 경로는 검증됨, 런타임 동작은 수동 확인 필요"
  - test: "UseProtocolV1=true, PcRole=2(PC2)로 설정 후 Site=1, Site=2 각각 $TEST 수신 → 양쪽 모두 SIDE 시퀀스 동작 확인"
    expected: "PC2 Site1/Site2 모두 SIDE 자원으로 라우팅"
    why_human: "런타임 라우팅 동작 — PcRole=2 분기 코드는 검증됨, 동작은 수동 확인 필요"
  - test: "$RESULT 출력을 TCP 수신 측(mock 클라이언트)에서 캡쳐하여 byte 형식 확인: RESULT:1;P;2;A1=12.345=OK,C1=5.000=OK"
    expected: "Vision-Protocol-v1.0.md 케이스 1~3과 byte 일치"
    why_human: "실 TCP 송신 후 수신 측 byte 캡쳐 필요 — 직렬화 코드 경로 추적은 완료, 실제 전송 포맷은 수동 확인 필요"
  - test: "UseProtocolV1=false (기본값)으로 v2.6 검사 사이클 1회 실행 — 기존 동작 회귀 없음 확인"
    expected: "기존 RESULT 포맷(,site,InspectionType,P,count,...), 기존 포트(2505), 기존 인코딩(Default)"
    why_human: "v2.6 경로 회귀 확인 — 실 검사 사이클 실행 필요"
---

# Phase 48: 제어 프로토콜 v1.0 Verification Report

**Phase Goal:** 디팜스테크 인터페이스 TCP/IP 프로토콜 v1.0(엑셀 규격) 커맨드 계층을 구현한다. `$TEST:site,…,z_index@`(자재 IndexNumber 필드 포함, 유연 파서)를 파싱하고 `$RESULT:site;P|F|B;count;id=val=OK,…@`를 직렬화하며, 2-PC 구조에 맞춰 Site 번호 체계를 재정합한다. 자재번호는 결과 저장(Export/파일명)까지 전파한다.
**Verified:** 2026-06-22T15:30:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths (ROADMAP Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | `$TEST:site,…,z_index@`(자재번호 포함) 유연 파싱 — 필드 누락/추가에도 폴백 동작 | ✓ VERIFIED | VisionRequestPacket.cs: `TryParseTestFieldsV1`, `ParseMaterialField`, `ParseZIndexField` — 명명 상수 인덱스(`TEST_FIELD_MATERIAL=1`, `TEST_FIELD_ZINDEX=3`), `SENTINEL_NO_MATERIAL=-1` 폴백, `bHasMaterial`/`bHasZIndex` 길이 체크 실존. 7개 behavior 케이스 코드 경로 추적 완료. |
| 2 | 자재 IndexNumber 가 결과 저장(파일명/xlsx)에 기록됨 | ✓ VERIFIED | (A) xlsx 경로: InspectionSequence.AddResponse → BuildDto(nIndexNumber) → CycleResultDto.IndexNumber → ExcelExportService ws.Cell(4,"자재번호") 확인. (B) 파일명 경로: QueueFaiCapture → parentSeq.RequestPacket.IndexNumber → BuildFileName 7-인자 → `_M{번호}`. 양 경로 grep 확인. |
| 3 | `$RESULT` P/F/B 직렬화가 엑셀 예시와 byte 일치 (케이스 1~3) | ✓ VERIFIED | VisionResponsePacket.cs: `BuildResultMessageV1` + `MapCycleJudgement`(IsBuffer 우선→B, OK→P, else→F) + `BuildFaiItemsV1`(id=val=judge). 3단 구분자 상수(`;`/`,`/`=`) 명명화. 코드 추적: 케이스1 → `RESULT:1;P;2;A1=12.345=OK,C1=5.000=OK`, 케이스4(Datum) → `RESULT:1;B;0;`. 실 TCP 출력 확인은 human_verification 이관. |
| 4 | Site 번호 2-PC 체계로 매핑 (TOP/BOTTOM/SIDE_1/SIDE_2) | ✓ VERIFIED | Custom/TcpServer/ResourceMap.cs: `EPcRole` enum(PC1_TopBottom=1/PC2_Side=2), `InitializeV1`→bIsPc1 분기→`MapPc1Resources`(Site1=TOP,Site2=BOTTOM)/`MapPc2Resources`(Site1/2=SIDE), `ResolveSiteSlot` 슬롯 변환. framework ResourceMap.cs git diff=0(무수정). SetIdentifier v1.0 분기: `ResolveSiteSlot(testPacket.Site)` 경유. |
| 5 | 기존 v2.6 경로 회귀 0 (또는 마이그레이션 가드) | ✓ VERIFIED | 모든 신규 코드가 `bool bUseV1 = SystemSetting.Handle.UseProtocolV1; if(bUseV1){...}else{...}` 패턴으로 v2.6 분기 보존. `TryParseTestFieldsV26` byte-identical 추출. `InitializeV26` 12개 Add 호출 그대로. 기존 IsDynamicFAI 직렬화 블록 잔존 확인(grep `IsDynamicFAI` in VisionResponsePacket.cs). git diff 신규 '+' 라인에 `??`/`?:` 0건 확인. 런타임 v2.6 회귀 = human_verification. |

**Score:** 5/5 truths verified (코드 수준)

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `WPF_Example/Setting/SystemSetting.cs` | `UseProtocolV1`(false), `PcRole`(1), `ServerPortV1`(7701) 속성 + `AfterLoad()` 선언 | ✓ VERIFIED | lines 94-106, 270-271: 3속성 기본값 확인, AfterLoad() 호출 확인 |
| `WPF_Example/Custom/SystemSetting.cs` | PcRole==0 → PC_ROLE_DEFAULT(1) 복원 가드 | ✓ VERIFIED | lines 29-50: `PC_ROLE_DEFAULT=1`, `partial void AfterLoad()`, `RestorePcRoleDefault()` — D-00 준수 |
| `WPF_Example/TcpServer/VisionRequestPacket.cs` | `TestPacket.IndexNumber` + `TryParseTestFieldsV1` + 명명 상수 + v2.6/v1.0 분기 | ✓ VERIFIED | IndexNumber=SENTINEL_NO_MATERIAL(line 441), 상수 8종(lines 29-37), 파서 3종(lines 321-363), 분기(lines 280-290) |
| `WPF_Example/Custom/TcpServer/ResourceMap.cs` | `EPcRole` enum + `InitializeV1/V26` + `MapPc1/Pc2Resources` + `ResolveSiteSlot` + SetIdentifier 분기 | ✓ VERIFIED | 모든 심볼 grep 확인. v2.6 보존 = InitializeV26 12 Add 호출 확인 |
| `WPF_Example/TcpServer/TcpServer.cs` | `ApplyEncoding` protected static + Port 분기(ServerPortV1) | ✓ VERIFIED | ApplyEncoding(line 80), bUseV1→ServerPortV1 분기(lines 350-354) |
| `WPF_Example/TcpServer/VisionServer.cs` | `ApplyEncoding(Utf8)` 호출 (bUseV1 시) | ✓ VERIFIED | lines 24-28 확인 |
| `WPF_Example/TcpServer/VisionResponsePacket.cs` | `BuildResultMessageV1` + `MapCycleJudgement` + `MapFaiJudgement` + `BuildFaiItemsV1` + 4 상수 + `TestResultPacket.IsBuffer` + bUseV1 분기 | ✓ VERIFIED | 상수 4종(lines 60-63), IsBuffer(line 644), 4개 메서드 grep 확인, bUseV1 분기(lines 349-353) |
| `WPF_Example/UI/ViewModel/CycleResultDto.cs` | `IndexNumber`(int=-1) 필드 | ✓ VERIFIED | line 19: `public int IndexNumber { get; set; } = -1;` |
| `WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs` | `BuildDto` `int nIndexNumber=-1` optional 파라미터 + dto 대입 | ✓ VERIFIED | lines 41, 50 확인 |
| `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` | AddResponse에서 RequestPacket.IndexNumber → BuildDto(nIndexNumber) 전달 | ✓ VERIFIED | lines 141-154: bHasRequest + nIndexNumber 추출 + 6번째 인자 전달 |
| `WPF_Example/Utility/CaptureImageSaveService.cs` | 7-인자 `BuildFileName`(nIndexNumber) + `FILENAME_NO_MATERIAL` + `_M{번호}` 삽입 | ✓ VERIFIED | lines 191-207: FILENAME_NO_MATERIAL=-1, 7-인자 오버로드, bHasMaterial→`_M` 삽입 |
| `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` | `QueueFaiCapture` 내 RequestPacket.IndexNumber 추출 + 7-인자 BuildFileName 호출 | ✓ VERIFIED | lines 601-620: bHasRequest + nIndexNumber 추출, 7-인자 호출 2건 |
| `WPF_Example/Custom/Export/ExcelExportService.cs` | xlsx 자재번호 행(행 4) + `int hr = 6` 오프셋 | ✓ VERIFIED | lines 44-56: ws.Cell(4,1)="자재번호", cycle.IndexNumber 분기, hr=6 |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `VisionRequestPacket.Convert` CMD_RECV_TEST | `SystemSetting.Handle.UseProtocolV1` | `bUseV1` 분기 → V1/V26 파서 선택 | ✓ WIRED | VisionRequestPacket.cs line 280 |
| `TryParseTestFieldsV1` | `TestPacket.IndexNumber` | `ParseMaterialField` → sentinel(-1) 또는 정수값 | ✓ WIRED | VisionRequestPacket.cs line 331 |
| `ResourceMap.Initialize` | `SystemSetting.Handle.UseProtocolV1 / PcRole` | `bUseV1` → `InitializeV1(PcRole)` vs `InitializeV26` | ✓ WIRED | ResourceMap.cs lines 38-50 |
| `ResourceMap.SetIdentifier(Test)` | `ResolveSiteSlot → Find(Sequence/Action)` | v1.0 분기 Site정수→ESite슬롯 변환 후 Find | ✓ WIRED | ResourceMap.cs lines 163-168 |
| `VisionServer 생성자` | `TcpServer.PortNum / EncodingType` | `ApplyEncoding(Utf8)` + TcpServer 생성자 Port 분기 | ✓ WIRED | VisionServer.cs lines 24-28, TcpServer.cs lines 350-354 |
| `VisionResponsePacket.Convert Test` | `SystemSetting.Handle.UseProtocolV1` | `bUseV1` → `BuildResultMessageV1` vs v2.6 블록 | ✓ WIRED | VisionResponsePacket.cs lines 349-353 |
| `BuildResultMessageV1` | `TestResultPacket.FAIResults / FAICount` | `BuildFaiItemsV1` → id=val=judge 항목 직렬화 | ✓ WIRED | VisionResponsePacket.cs lines 441, 476-494 |
| `InspectionSequence.AddResponse` | `CycleResultSerializer.BuildDto(nIndexNumber)` | `RequestPacket.IndexNumber` 추출 후 6번째 인자 | ✓ WIRED | InspectionSequence.cs lines 141-154 |
| `Action_FAIMeasurement.QueueFaiCapture` | `CaptureImageSaveService.BuildFileName(…nIndexNumber)` | `parentSeq.RequestPacket.IndexNumber` → 7-인자 호출 | ✓ WIRED | Action_FAIMeasurement.cs lines 601-620 |
| `CycleResultDto.IndexNumber` | `ExcelExportService ws.Cell(4, "자재번호")` | `cycle.IndexNumber >= 0` 분기 → 셀 값 | ✓ WIRED | ExcelExportService.cs lines 44-52 |

---

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|--------------|--------|-------------------|--------|
| `ExcelExportService.cs` | `cycle.IndexNumber` | `CycleResultDto.IndexNumber` ← `BuildDto(nIndexNumber)` ← `RequestPacket.IndexNumber` (TCP 패킷) | TestPacket에서 실 파싱값 전파 | ✓ FLOWING |
| `CaptureImageSaveService.BuildFileName` (7-arg) | `nIndexNumber` | `parentSeq.RequestPacket.IndexNumber` ← TCP 파싱 | TCP 패킷에서 실 값 전파 | ✓ FLOWING |
| `BuildResultMessageV1` | `testPacket.FAIResults`, `testPacket.IsBuffer` | `FAIResults` = 검사 실행 결과 리스트 (실 데이터), `IsBuffer` 기본 false (Phase 49 스텁) | FAIResults는 실 데이터. IsBuffer=false는 Phase 49 범위 스텁 (설계 의도 명시) | ✓ FLOWING (IsBuffer Phase 49 deferred) |

---

### Behavioral Spot-Checks

Step 7b: SKIPPED — 실 TCP 소켓 통신 없이 파서/직렬화 런타임 동작 확인 불가 (디팜스테크 핸들러 또는 mock 클라이언트 필요). 코드 경로 정적 추적으로 대체 완료 (Truth #1~#3 evidence 참조).

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|------------|-------------|-------------|--------|----------|
| PROTO-01 | 48-01, 48-02, 48-04 | TEST 커맨드 z_index 파라미터 파싱 + ResourceMap z_index↔Shot 매핑 + 자재번호 전파 | ✓ SATISFIED | TryParseTestFieldsV1(자재번호/z_index 파싱), TestPacket.IndexNumber(전파 출발점), ResourceMap 2-PC 분기, CycleResultDto/ExcelExportService/BuildFileName 자재번호 전파 |
| PROTO-02 | 48-03 | RESULT 포맷 3단 구분자 `$RESULT:site;P|F|B;count;id=val=OK,...@` 직렬화/역직렬화 | ✓ SATISFIED | BuildResultMessageV1 + 3단 구분자 상수 + IsBuffer/MapCycleJudgement + BuildFaiItemsV1. 케이스 1~3 byte 추적 완료 |

REQUIREMENTS.md 추적표에서 PROTO-01/PROTO-02 범위는 Phase 48로 매핑됨 확인. PROTO-03~PROTO-06은 Phase 49/50 범위 — 본 phase 범위 밖.

---

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `VisionResponsePacket.cs` | ~379-381 | 기존(pre-Phase 48) v2.6 FAI 루프 내 삼항 연산자 — D-00 위반 | ℹ️ Info | Phase 48 이전 코드. Phase 48 신규 코드(BuildResultMessageV1 등)는 D-00 준수 확인. QUAL-01 phase 범위. |
| `InspectionSequence.cs` | ~443 | `??` null 병합 연산자 사용 (`ResolveDatumModelPath2` 내) — D-00 위반 | ℹ️ Info | Phase 55 ALIGN-02 코드(주석 `//260619 hbk Phase 55`). Phase 48 범위 외. QUAL-01 이연. |
| `InspectionSequence.cs` | ~143 | AddResponse 내 RequestPacket null 이중 체크 (line 87에서 이미 체크 후 143에서 재검사) | ℹ️ Info | 기능 결함 아님(안전한 중복). CR-WR-03(48-REVIEW.md) 기록됨. |
| `TcpServer.cs` | 76 | `EncodingType` private static — 다중 인스턴스 시 전역 오염 잠재 (CR-01) | ⚠️ Warning | 현행 싱글턴 아키텍처(1 인스턴스)에서 실제 미발생. CO-48-01로 carry-over 결정됨(48-REVIEW.md). |
| `ExcelExportService.cs` | 56 | `int hr = 6` 하드코딩 (D-00 매직넘버 금지) | ℹ️ Info | IN-05(48-REVIEW.md). 기능 동작에 문제 없음. QUAL-01 범위. |

D-00 준수 검사 (신규 Phase 48 제어 코드):

- `??` / `?:` / `?.` in new protocol code: git diff 신규 '+' 라인 grep → **0건**
- 헝가리언 변수명: `bUseV1`, `bIsPc1`, `bIsSite2`, `bHasSite`, `bSiteValid`, `bHasMaterial`, `bIsNullPlaceholder`, `bMaterialValid`, `bHasZIndex`, `bZValid`, `bParseV1Ok`, `bParseV26Ok`, `bIsBuffer`, `bIsPass`, `bIsOk`, `bNeedsSeparator`, `bHasRequest`, `bHasMaterial`, `nSiteNum`, `nMaterial`, `nZIndex`, `nPcRole`, `nIndexNumber`, `szMsg`, `szItems`, `szRaw`, `szMat` — 확인됨
- 명명 상수: `SENTINEL_NO_MATERIAL`, `TEST_FIELD_SITE/MATERIAL/ZINDEX`, `TEST_MIN_FIELD_*`, `EPcRole.PC1_TopBottom/PC2_Side`, `MSG_RESULT_HEADER_SEP/ITEM_SEP/INNER_SEP`, `TEST_RESULT_BUFFER`, `FILENAME_NO_MATERIAL`, `PC_ROLE_DEFAULT` — 확인됨

---

### Human Verification Required

#### 1. TEST 패킷 유연 파싱 E2E (TCP)

**Test:** UseProtocolV1=true 설정 후 mock 클라이언트로 다음 패킷들을 순차 송신:
- `$TEST:1,42,null,1@` → IndexNumber=42 확인
- `$TEST:1,null,null,0@` → IndexNumber=-1 확인
- `$TEST:1@` (필드 누락) → IndexNumber=-1, TestID=-1 폴백 확인

**Expected:** 각 케이스별 IndexNumber 파싱 결과가 SUMMARY 행동 케이스표와 일치
**Why human:** 실 TCP 소켓 송수신 필요 — 코드 정적 경로 추적은 완료

#### 2. 자재번호 파일명/xlsx 전파 확인

**Test:** UseProtocolV1=true + IndexNumber=42인 TEST 패킷 수신 후 검사 사이클 완료 → (A) 생성된 캡쳐 파일명에 `_M42` 포함 여부, (B) xlsx export 행 4에 자재번호=42 기록 여부
**Expected:** 파일명 `origin_TOP_..._M42_....jpg`, xlsx 행4열B=42
**Why human:** 검사 사이클 실행 + 파일시스템/xlsx 실제 출력 확인 필요

#### 3. 2-PC Site 라우팅 — PC1 모드

**Test:** UseProtocolV1=true, PcRole=1(PC1) 설정 후 SIMUL 모드로 Site=1 $TEST → TOP 시퀀스 실행, Site=2 $TEST → BOTTOM 시퀀스 실행 확인
**Expected:** PC1: Site1=TOP, Site2=BOTTOM 시퀀스 각각 Grab/Inspect 실행
**Why human:** 시퀀스 실행 라우팅은 런타임 동작 — ResourceMap 코드 경로는 정적 검증 완료

#### 4. 2-PC Site 라우팅 — PC2 모드

**Test:** UseProtocolV1=true, PcRole=2(PC2) 설정 후 Site=1, Site=2 $TEST → 양쪽 모두 SIDE 시퀀스 실행 확인
**Expected:** PC2: Site1=SIDE, Site2=SIDE (동일 물리 자원 공유)
**Why human:** 런타임 라우팅 확인 필요

#### 5. RESULT byte 포맷 확인

**Test:** UseProtocolV1=true 상태에서 검사 완료 후 송신되는 $RESULT 메시지를 TCP 수신 측에서 캡쳐하여 byte 단위 포맷 확인:
- 케이스1(전체 OK): `$RESULT:1;P;N;id1=val1=OK,...@`
- 케이스4(Datum 샷): `$RESULT:1;B;0;@`

**Expected:** Vision-Protocol-v1.0.md 케이스 1~3과 byte 일치. 특히 첫 구분자가 `;`(`,` 아님).
**Why human:** 실 TCP 출력 byte 캡쳐 필요 — 직렬화 코드 정적 추적은 완료

#### 6. v2.6 경로 회귀 확인

**Test:** UseProtocolV1=false (기본값) 상태에서 기존 v2.6 검사 사이클 1회 실행 — RESULT 포맷, 포트(2505), 인코딩 모두 기존과 동일한지 확인
**Expected:** 기존 RESULT 포맷(`RESULT:site,InspectionType,P,...`), 포트 2505, 기존 동작 완전 보존
**Why human:** 런타임 v2.6 경로 실행 확인 필요

---

### Known Stubs (Phase 49 범위, 비-블로커)

| Stub | 위치 | 현재 동작 | Phase 49 역할 |
|------|------|-----------|--------------|
| `TestResultPacket.IsBuffer = false` (기본값) | VisionResponsePacket.cs:644 | 항상 P/F만 출력, B 출력 불가 | Phase 49 판정 엔진이 IsBuffer=true 설정 → B 출력 활성화 |

이 스텁은 Phase 48 범위 내 설계 의도(직렬화 "자리"만 마련, 판정 엔진은 Phase 49). 설계 문서에 명시됨.

---

### Review Findings 처리 현황

Phase 48 코드 리뷰(48-REVIEW.md, 2026-06-22) 처리 상태:

| ID | 심각도 | 처리 |
|----|--------|------|
| CR-01: EncodingType static 오염 | Critical | CO-48-01 carry-over (싱글턴 아키텍처에서 미발현, 구조적 latent) |
| WR-01: $LIGHT/$SITE_STATUS Find KeyNotFoundException 경로 | Warning | Phase 49 범위 (해당 커맨드 구현 미착수) |
| WR-02: z_index 인덱스 2 예약 필드 무시 | Warning | 설계 의도 (인덱스 3=z_index, 인덱스 2=예약=null — CONTEXT D-01 기준 올바름) |
| WR-03: AddResponse null 이중 체크 | Warning | 기능 결함 아님. 코드 품질 개선 carry-over |
| WR-04: nIndexNumber==0 자재번호 처리 | Warning | 설계 의도 (0은 유효 자재번호, -1만 sentinel) |
| WR-05: trailing `;` Datum 샷 | Warning | BY DESIGN — Vision-Protocol-v1.0.md 규격의 `$RESULT:site;B;0;@` 형식 준수 |

---

### Gaps Summary

코드 수준에서 모든 5개 ROADMAP success criteria가 충족됨. 11개 아티팩트 + 10개 키 링크 모두 검증됨. D-00 준수(신규 제어 코드 `??`/`?:`/`?.` 0건) 확인됨.

**현재 status가 `human_needed`인 유일한 이유:** 실 TCP 핸들러 없이 런타임 동작(패킷 송수신, 시퀀스 라우팅, 파일/xlsx 실제 출력)을 확인할 수 없음. 코드 정적 경로 추적은 전 항목 완료. 자동화 빌드 게이트: PASS (0 errors, 0 new warnings — 4 SUMMARY 모두 확인).

---

_Verified: 2026-06-22T15:30:00Z_
_Verifier: Claude (gsd-verifier)_
