---
phase: 48-protocol-v1-test-result-site-material
plan: 01
subsystem: protocol
tags: [protocol-v1, test-parser, system-setting, sentinel, hungarian]
dependency_graph:
  requires: []
  provides:
    - SystemSetting.UseProtocolV1 (Wave 2 모든 plan 이 공유하는 v1/v2.6 분기 플래그)
    - SystemSetting.PcRole (Wave 2 Plan 02 Site 2-PC 매핑 기반)
    - SystemSetting.ServerPortV1 (Wave 2 포트 전환 기반)
    - TestPacket.IndexNumber (Wave 2 Plan 04 자재번호 전파 출발점)
    - TryParseTestFieldsV1 / ParseMaterialField / ParseZIndexField (유연 파서)
    - SENTINEL_NO_MATERIAL = -1 (자재번호 미수신 sentinel — 전파 체인 표준값)
  affects:
    - VisionRequestPacket.Convert(string) CMD_RECV_TEST 분기
tech_stack:
  added: []
  patterns:
    - partial void 메서드 후크(AfterLoad) — base Load() 완료 후 Custom 처리 연결
    - 명명 인덱스 상수 (TEST_FIELD_*) — 고정 매직 인덱스 탈피 패턴
    - sentinel 폴백 파서 — 필드 누락 시 기본값 반환
key_files:
  created: []
  modified:
    - WPF_Example/Setting/SystemSetting.cs
    - WPF_Example/Custom/SystemSetting.cs
    - WPF_Example/TcpServer/VisionRequestPacket.cs
decisions:
  - "v1.0 분기에서 TestType 기본값(0=ETestType.Default) 유지 — v1.0은 TestType 명시 필드 없음. ResourceMap.SetIdentifier가 TestType==0(Default)이면 Inspection으로 흘림(정상). Site→Sequence 매핑은 Wave 2 Plan 02 PcRole 기반으로 처리."
  - "sentinel 값 -1 채택 — SENTINEL_NO_MATERIAL = -1. 자재번호 0은 유효한 자재번호일 수 있으므로 -1 선택. Wave 2 Plan 04 전파 체인도 >= 0 조건으로 유효값 판별."
  - "AfterLoad() partial void 메서드 후크 패턴 채택 — base SystemSetting.Load() 끝에서 AfterLoad() 호출, Custom/SystemSetting.cs 에서 구현. 기존 메서드 시그니처 무변경(v2.6 회귀 0)."
  - "testID 미사용 로컬 변수 제거(CS0219 신규 경고) — v1.0/v2.6 파서 메서드 내부로 이전되어 switch scope 에서 불필요. Rule 1 자동 수정."
metrics:
  duration: 30
  completed_date: "2026-06-22"
---

# Phase 48 Plan 01: PROTO-01 Foundation — 유연 TEST 파서 + SystemSetting v1.0 플래그 Summary

**One-liner:** `UseProtocolV1/PcRole/ServerPortV1` SystemSetting 속성 + `TryParseTestFieldsV1` 유연 파서(자재번호 필드, sentinel -1 폴백) + `TestPacket.IndexNumber` + v2.6/v1.0 분기 구현

## Build Result

msbuild Debug/x64 PASS — 0 errors, 0 new warnings. 기존 베이스라인 경고(CS0618 x5, CS0162 x1, MSB3884 x1) 유지.

## What Was Built

### Task 1: SystemSetting v1.0 속성 + Custom Load 가드 (commit a1bcff4)

**WPF_Example/Setting/SystemSetting.cs** — 신규 속성 3개 추가:

| 속성 | 타입 | 기본값 | Category | 역할 |
|------|------|--------|----------|------|
| UseProtocolV1 | bool | false | Connection/Protocol | v1.0 활성화 플래그 (D-06) |
| PcRole | int | 1 | Connection/Server | PC 역할(1=PC1/2=PC2, D-03) |
| ServerPortV1 | int | 7701 | Connection/Server | v1.0 전용 포트 |

`partial void AfterLoad()` 선언 → `Load()` 완료 직후 호출.

**WPF_Example/Custom/SystemSetting.cs** — Load 가드 추가:

- `PC_ROLE_DEFAULT = 1` 명명 상수
- `partial void AfterLoad()` 구현 → `RestorePcRoleDefault()` 호출
- `RestorePcRoleDefault()`: `bool bPcRoleMissing = PcRole == 0; if (bPcRoleMissing) { PcRole = PC_ROLE_DEFAULT; }` — 구 INI 0 로드 방어

### Task 2: TestPacket.IndexNumber + 유연 V1 파서 + v2.6/v1.0 분기 (commit 38d22f4)

**WPF_Example/TcpServer/VisionRequestPacket.cs**:

**명명 상수 (매직넘버 0건):**
- `SENTINEL_NO_MATERIAL = -1` (자재번호 미수신)
- `SENTINEL_Z_INDEX_STR = "-1"` (z_index 미수신)
- `TEST_NULL_PLACEHOLDER = "null"` (예약 문자열)
- `TEST_FIELD_SITE = 0`, `TEST_FIELD_MATERIAL = 1`, `TEST_FIELD_ZINDEX = 3`
- `TEST_MIN_FIELD_SITE = 1`, `TEST_MIN_FIELD_MATERIAL = 2`, `TEST_MIN_FIELD_ZINDEX = 4`

**TestPacket 클래스:**
- `public int IndexNumber { get; set; } = SENTINEL_NO_MATERIAL;` 추가 — 자재번호 전파 출발점

**파서 메서드 3종 (모두 D-00 준수):**
- `TryParseTestFieldsV1(string[] dataList, TestPacket testPacket)` — 메인 유연 파서 (11줄)
- `ParseMaterialField(string[] dataList)` — 자재번호 파싱 헬퍼 (14줄)
- `ParseZIndexField(string[] dataList)` — z_index 파싱 헬퍼 (8줄)

**레거시 파서:**
- `TryParseTestFieldsV26(string[] dataList, TestPacket testPacket)` — 기존 v2.6 로직 byte-identical 추출 (회귀 0)

**CMD_RECV_TEST 분기:**
```csharp
bool bUseV1 = ReringProject.Setting.SystemSetting.Handle.UseProtocolV1;
if (bUseV1) { ... TryParseTestFieldsV1 ... }
else        { ... TryParseTestFieldsV26 ... }
```

## Behavior 검증 (코드 경로 추적)

| 입력 | Site | IndexNumber | TestID | 반환 |
|------|------|-------------|--------|------|
| "1,42,null,1" | 1 | 42 | "1" | true |
| "1,null,null,0" | 1 | -1 | "0" | true |
| "1" | 1 | -1 | "-1" | true |
| "1,42" | 1 | 42 | "-1" | true |
| "" | - | - | - | false (bHasSite=false) |
| "abc,42,null,1" | - | - | - | false (bSiteValid=false) |
| "1,xyz,null,1" | 1 | -1 | "1" | true (bMaterialValid=false → sentinel) |

## D-00 준수 확인

| 규칙 | 상태 |
|------|------|
| 헝가리언 접두사 (b/n/sz) | PASS — 모든 신규 로컬 변수에 적용 |
| if/else if/else only (삼항 ?:/null병합 ?? 0건) | PASS — grep 확인 |
| 중첩 2단계 이하 | PASS — 최대 1단계 (bool변수 → if) |
| 조건식 bool 변수화 | PASS — bHasSite/bSiteValid/bHasMaterial/bIsNullPlaceholder/bMaterialValid 등 |
| 매직넘버 금지 | PASS — 모든 인덱스/sentinel 명명 상수 |
| 함수 30줄 한도 | PASS — 최장 TryParseTestFieldsV1 = 11줄 |
| 동사+목적어 함수명 | PASS — TryParseTestFieldsV1/ParseMaterialField/ParseZIndexField/RestorePcRoleDefault |

## 주요 설계 결정

1. **v1.0 TestType 기본값(0) 유지 결정**: v1.0 $TEST 포맷에 TestType 명시 필드 없음. TryParseTestFieldsV1 에서 TestType을 설정하지 않으면 기본값 0(=ETestType.Default). ResourceMap.SetIdentifier 에서 `(ETestType)testPacket.TestType == ETestType.Calibration` 분기가 Calibration을 걸러내므로, TestType=0(Default)이면 Inspection으로 흐름(정상 동작). Site→Sequence 매핑은 Wave 2 Plan 02가 PcRole 기반으로 처리.

2. **sentinel 값 -1 채택**: 자재번호 0은 유효할 수 있으므로 -1을 미수신 sentinel로 사용. Wave 2 Plan 04의 파일명/xlsx 전파에서 `>= 0` 조건으로 유효값 판별.

3. **AfterLoad() partial 후크 연결 지점**: Load() 루프 끝(catch 블록 이후, Save() 메서드 전)에 `AfterLoad()` 호출. Custom partial 구현이 없으면 컴파일러가 호출 자체를 제거하므로 null 안전.

4. **추가된 명명 상수 목록**: `SENTINEL_NO_MATERIAL(-1)`, `SENTINEL_Z_INDEX_STR("-1")`, `TEST_NULL_PLACEHOLDER("null")`, `TEST_FIELD_SITE(0)`, `TEST_FIELD_MATERIAL(1)`, `TEST_FIELD_ZINDEX(3)`, `TEST_MIN_FIELD_SITE(1)`, `TEST_MIN_FIELD_MATERIAL(2)`, `TEST_MIN_FIELD_ZINDEX(4)`, `PC_ROLE_DEFAULT(1)`.

5. **Custom Load 가드 연결 지점**: `WPF_Example/Custom/SystemSetting.cs` partial `AfterLoad()` 구현 → `RestorePcRoleDefault()` 호출. PcRole == 0이면 PC_ROLE_DEFAULT(=1)로 복원.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] CS0219 신규 경고 — testID 미사용 로컬 변수 제거**

- **Found during:** Task 2 (빌드 검증)
- **Issue:** `string testID = ""` 로컬 변수가 v1.0/v2.6 파서 분기 도입으로 switch scope에서 더 이상 사용되지 않아 CS0219 경고 발생
- **Fix:** 해당 로컬 변수 선언 제거 (v2.6 파서 메서드 `TryParseTestFieldsV26` 내부 지역 변수로 이전)
- **Files modified:** `WPF_Example/TcpServer/VisionRequestPacket.cs`
- **Commit:** 38d22f4

## Known Stubs

없음. 이 plan은 순수 파서/설정 코드로 UI 렌더 없음.

## Threat Flags

이 plan의 구현이 T-48-01/T-48-02 위협 레지스터 mitigation을 충족합니다:
- T-48-01 (DoS): `dataList.Length >= TEST_MIN_FIELD_*` 체크 + `Int32.TryParse` 실패 시 sentinel 반환 — IndexOutOfRange/FormatException 미발생.
- T-48-02 (Tampering): 자재번호 비정수/변형 입력 → `SENTINEL_NO_MATERIAL(-1)` 정규화 — 임의 문자열이 전파 체인으로 흐르지 않음.

## Self-Check: PASSED

- WPF_Example/Setting/SystemSetting.cs 존재 및 UseProtocolV1/PcRole/ServerPortV1 속성 확인
- WPF_Example/Custom/SystemSetting.cs 존재 및 PcRole==0 가드/PC_ROLE_DEFAULT 확인
- WPF_Example/TcpServer/VisionRequestPacket.cs 존재 및 IndexNumber/TryParseTestFieldsV1/SENTINEL_NO_MATERIAL 확인
- commit a1bcff4 존재 확인
- commit 38d22f4 존재 확인
- msbuild Debug/x64 0 errors, 0 new warnings PASS
