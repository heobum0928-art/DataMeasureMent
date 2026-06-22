---
phase: 48-protocol-v1-test-result-site-material
reviewed: 2026-06-22T14:00:00Z
depth: standard
files_reviewed: 13
files_reviewed_list:
  - WPF_Example/Setting/SystemSetting.cs
  - WPF_Example/Custom/SystemSetting.cs
  - WPF_Example/TcpServer/VisionRequestPacket.cs
  - WPF_Example/Custom/TcpServer/ResourceMap.cs
  - WPF_Example/TcpServer/VisionServer.cs
  - WPF_Example/TcpServer/TcpServer.cs
  - WPF_Example/TcpServer/VisionResponsePacket.cs
  - WPF_Example/UI/ViewModel/CycleResultDto.cs
  - WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/Utility/CaptureImageSaveService.cs
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
  - WPF_Example/Custom/Export/ExcelExportService.cs
findings:
  critical: 1
  warning: 5
  info: 6
  total: 12
status: issues_found
---

# Phase 48: Code Review Report

**Reviewed:** 2026-06-22T14:00:00Z
**Depth:** standard
**Files Reviewed:** 13
**Status:** issues_found

## Summary

Phase 48 구현 범위(TCP 제어 프로토콜 v1.0, RESULT 직렬화, 2-PC Site 매핑, 자재번호 전파)를 검토했다.
전체적으로 v2.6 하위 호환 분기(`UseProtocolV1=false`)가 잘 보호되어 있고, PcRole INI 누락 방어(AfterLoad/RestorePcRoleDefault)와 path-traversal 방어(SanitizeFilePart/SanitizeFileName)는 올바르게 구현되었다.

주요 발견:
- **Critical 1건**: `EncodingType`가 `static` 필드인데 `protected static ApplyEncoding`을 통해 파생 클래스(VisionServer) 생성자에서 수정되는 구조 — 프로세스 내에 `TcpServer` 인스턴스가 2개 이상 생성되면 전역 인코딩이 예기치 않게 변경될 수 있다.
- **Warning 5건**: `ResourceMap.Find()`가 KeyNotFoundException을 swallow 없이 throw하므로 잘못된 Site값이 앱 크래시를 유발할 수 있는 경로가 있음; v1.0 파서에서 `TEST_FIELD_ZINDEX`(인덱스 3)가 상수로 정의되었으나 필드 2(인덱스 2)는 완전히 무시됨; 코딩 지침 위반 3건.
- **Info 6건**: 코딩 지침(헝가리언/if-else) 준수 미흡 부분, 구조적 개선 제안.

---

## Critical Issues

### CR-01: `EncodingType`가 `static` 필드 — 다중 인스턴스 시 전역 오염

**File:** `WPF_Example/TcpServer/TcpServer.cs:76`
**Issue:** `MessageEncodingType EncodingType`가 `private static` 필드이고, `protected static ApplyEncoding()`을 통해 VisionServer 생성자가 이를 수정한다. `TcpServer`(또는 그 파생 클래스) 인스턴스가 프로세스 내에 1개 이상 생성되는 경우 — 예를 들어 테스트 목적 재생성, 핫-재구동 등 — 나중에 생성된 인스턴스가 인코딩을 바꾸면 기존 연결 인스턴스의 `ConvertMessage()` 동작도 조용히 바뀐다. 현재는 인스턴스가 1개뿐이어서 실제로 발생하지 않지만, 구조적 시한폭탄이다. 또한 `ApplyEncoding`이 `protected static`이라 어떤 파생 클래스에서든 호출 가능하여 캡슐화가 유명무실하다.

**Fix:**
```csharp
// TcpServer.cs — 인스턴스 필드로 변경 (static 제거)
private MessageEncodingType EncodingType { get; set; } = MessageEncodingType.Default;

// ApplyEncoding: static → instance 메서드로 변경
protected void ApplyEncoding(MessageEncodingType eEncoding)
{
    EncodingType = eEncoding;
}
```
`ConvertMessage()`와 동일 클래스 내의 `Header`/`Trailer` static 필드도 동일 문제를 가지고 있으나, Header/Trailer는 Phase 48 이전부터 존재하므로 범위 외로 기록만 한다.

---

## Warnings

### WR-01: `ResourceMap.Find()` — KeyNotFoundException이 catch 없이 전파될 수 있는 경로

**File:** `WPF_Example/TcpServer/ResourceMap.cs:100-106` (framework), `WPF_Example/Custom/TcpServer/ResourceMap.cs:130-181`
**Issue:** `ResourceMap.Find(EResource, ESite)`는 `this[res][site]`를 직접 반환한다. `SiteMap.this[ESite]`가 내부적으로 `SiteList[site]`를 호출하는데, 존재하지 않는 키이면 `KeyNotFoundException`이 throw된다. v1.0 경로에서 `SetIdentifier`의 `Light` case(line 134)는 `ResolveSiteSlot` 없이 `(ESite)lightPacket.Site`를 직접 캐스팅하므로, 프로토콜이 Site=3(Bottom)을 전송하면 v1.0 Light 매핑에 ESite.Bottom 키가 없어 `KeyNotFoundException`이 발생한다.

`VisionServer.GetRecvPacket()`의 catch(ArgumentOutOfRangeException, IndexOutOfRangeException)는 이를 잡지 못한다(KeyNotFoundException은 다른 타입이다). 결과적으로 예외가 `MainRun()` 폴링 루프까지 전파될 수 있으며, 루프가 try/catch로 감싸지지 않으면 스레드 크래시로 이어진다.

**Fix:**
```csharp
// Custom/TcpServer/ResourceMap.cs — Light case 에 ResolveSiteSlot 적용 (v1.0 분기)
case VisionRequestType.Light:
    LightPacket lightPacket = packet.AsLight();
    bool bLightUseV1 = SystemSetting.Handle.UseProtocolV1;
    ESite eLightSlot;
    if (bLightUseV1)
    {
        eLightSlot = ResolveSiteSlot(lightPacket.Site);
    }
    else
    {
        eLightSlot = (ESite)lightPacket.Site;
    }
    lightPacket.Identifier = Find(EResource.Light, eLightSlot);
    // ...
```

또는 `VisionServer.GetRecvPacket()`에 `catch (KeyNotFoundException)` 추가.

---

### WR-02: v1.0 TEST 파서 — 필드 인덱스 2(예약 필드)가 완전히 무시되어 z_index 인덱스 불일치 위험

**File:** `WPF_Example/TcpServer/VisionRequestPacket.cs:35`
**Issue:** 프로토콜 규격 `$TEST:site,MaterialNumber,null,z_index@`에서 인덱스 2는 예약 필드("null" 리터럴)이고 인덱스 3이 z_index이다. 상수 `TEST_FIELD_ZINDEX = 3`은 올바르다. 그러나 `ParseZIndexField`는 `dataList[3]`을 `Int32.TryParse`로 파싱하는데, 만약 외부 장비가 규격을 변경하여 인덱스 2에 정수를 넣거나 필드 순서를 바꾸면 `TestID`(z_index)가 잘못된 값이 된다. 더 중요하게는 `TEST_MIN_FIELD_ZINDEX = 4`이므로 패킷이 `$TEST:site,material,null@` (필드 3개)이면 z_index 파싱을 생략하고 `SENTINEL_Z_INDEX_STR = "-1"`을 반환한다 — 이는 의도된 동작이지만, `TestID = "-1"`이 하류에서 파일명에 `-1` 문자열로 삽입될 수 있다.

`ParseZIndexField`가 반환한 `"-1"` 문자열이 `TestPacket.TestID`에 저장되고, 이는 v2.6 경로의 `testPacket.Identifier2` 등 기존 코드에서 사용될 수 있다. v1.0에서는 `Identifier2`를 항상 `SequenceHandler.ACT_INSPECT`로 overwrite하므로 실제 문제가 되지 않지만, `TestID` 필드 자체가 `-1` 문자열로 설정되는 점은 의도를 오해할 여지가 있다.

**Fix:**
`ParseZIndexField`의 반환 타입을 `int`로 변경하거나, `TestID`가 v1.0에서 실제로 사용되지 않는다면 해당 필드를 `TestPacket`에서 v1.0 전용 필드로 명시적으로 분리하여 혼선을 방지하라. 최소한 상수에 주석을 추가한다:

```csharp
// SENTINEL_Z_INDEX_STR: TestID 에 "-1" 이 들어감 — v1.0 에서는 TestID 가 Identifier2 할당에
// 쓰이지 않으므로 하류 영향 없음. v2.6 경로에선 TestID = "실제 ID 문자열" 이 요구됨.
public const string SENTINEL_Z_INDEX_STR = "-1";
```

---

### WR-03: `InspectionSequence.AddResponse()` — `RequestPacket.IndexNumber` 접근 전 null 체크 중복 및 불필요한 이중 null 검사

**File:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:142-147`
**Issue:** `AddResponse()`는 line 87에서 이미 `if (RequestPacket == null) return;`을 수행한다. 그런데 line 143에서 `bool bHasRequest = RequestPacket != null;`을 다시 검사한다. `RequestPacket`은 단일 시퀀스 스레드 내에서만 접근되므로 line 87 이후 null이 될 수 없다. 이 이중 검사는 독자에게 혼선을 준다. 코딩 지침(D-00) 상 조건을 변수화하는 것 자체는 올바르지만, 이미 보장된 상태를 재검사하는 것은 코드 품질 문제이다.

**Fix:**
```csharp
// AddResponse() 내부 (line 87 이후는 RequestPacket != null 보장)
// line 141-147 블록을 단순화:
int nIndexNumber = RequestPacket.IndexNumber; //260622 hbk Phase 48 PROTO-01
```

---

### WR-04: `CaptureImageSaveService.BuildFileName` 7-arg 오버로드 — `nIndexNumber == 0`이 자재번호로 처리되지 않음

**File:** `WPF_Example/Utility/CaptureImageSaveService.cs:200`
**Issue:** `bool bHasMaterial = nIndexNumber > FILENAME_NO_MATERIAL;`에서 `FILENAME_NO_MATERIAL = -1`이므로 `nIndexNumber == 0`이면 `bHasMaterial = true`가 되어 파일명에 `_M0`이 삽입된다. 자재번호가 0인 경우가 실제 유효한 자재번호인지 sentinel인지 프로토콜 규격에 의존한다. `VisionRequestPacket.SENTINEL_NO_MATERIAL = -1`이므로 0은 유효한 자재번호로 취급되는 것이 맞다. 그러나 현재 `ParseMaterialField`에서는 비정수/null/빈값을 `-1`로 정규화하지만, 정수 `0`은 `-1`이 아니므로 유효 자재번호로 통과한다.

이 자체가 버그는 아니지만, 프로토콜 규격에 "자재번호 0은 미수신"이라는 약속이 있다면 sentinel 조건을 `<= 0`으로 변경해야 한다. 현재 상수와 코드 간에 이 약속이 명시되어 있지 않다.

**Fix:**
sentinel 정책을 주석으로 명확히 한다:
```csharp
// FILENAME_NO_MATERIAL: -1 = 미수신(sentinel). 0 포함 양수는 유효 자재번호.
// 프로토콜 규격상 자재번호 0은 유효값으로 처리 (디팜스테크 협의 기준).
private const int FILENAME_NO_MATERIAL = -1;
```
만약 0이 유효하지 않은 값이라면 sentinel을 `<= 0`으로 변경하고, `VisionRequestPacket.SENTINEL_NO_MATERIAL`도 동일하게 조정한다.

---

### WR-05: `VisionResponsePacket.BuildResultMessageV1` 마지막 `;` 뒤 항목 없는 경우 — 파서 계약 모호

**File:** `WPF_Example/TcpServer/VisionResponsePacket.cs:429-442`
**Issue:** 주석에 "FAICount=0(Datum 샷)이면 BuildFaiItemsV1 가 빈 문자열 반환 → 'RESULT:{site};B;0;' (마지막 ';' 뒤 항목 없음)"이라고 명시되어 있다. 이 형식(`RESULT:site;B;0;`)의 trailing semicolon이 수신 측(핸들러/호스트) 파서에서 허용되는지 확인이 필요하다. 많은 파서가 trailing separator를 허용하지 않거나 빈 토큰을 만들어 파싱 오류를 낸다.

Phase 48 계획 범위 내에서 수신 측 구현이 확정되지 않았더라도, 생성 시점에 trailing separator를 생략하는 방어적 구현이 더 안전하다.

**Fix:**
```csharp
// BuildResultMessageV1 마지막 줄 조정
string szFaiItems = BuildFaiItemsV1(testPacket);
szMsg += testPacket.FAICount.ToString();
if (!string.IsNullOrEmpty(szFaiItems))
{
    szMsg += MSG_RESULT_HEADER_SEP;   // ';' — 항목이 있을 때만 추가
    szMsg += szFaiItems;
}
// FAICount=0이면 trailing ';' 없음: RESULT:site;B;0
```

---

## Info

### IN-01: 코딩 지침(D-00) 위반 — `ResolveDatumModelPath2` 내 `??` 연산자 사용

**File:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:443`
**Issue:** `string propertyName = (datum.DatumName ?? "") + "_2";` — `??` null 병합 연산자는 D-00 코딩 지침에서 명시적으로 금지되어 있다. 이 파일은 Phase 48 범위(제어 시퀀스)와는 다른 측정 시퀀스이지만, 신규 추가된 `ResolveDatumModelPath2` 메서드는 Phase 이전에도 동일 패턴이 `ResolveDatumModelPath`(line 419: `if (propertyName == null) propertyName = "";`)로 구현되어 있으며, 미러라고 주석을 달았음에도 불구하고 새로 작성된 `ResolveDatumModelPath2`에서 `??`를 사용하여 불일치가 생겼다. 적용 범위가 "제어 프로토콜 신규 코드"로 한정되나, 동일 소스 파일에 혼재하는 점은 품질 위험이다.

**Fix:**
```csharp
// ResolveDatumModelPath2 (line 443)
string propertyName = datum.DatumName;
if (propertyName == null) propertyName = "";
propertyName = propertyName + "_2";
```

---

### IN-02: 코딩 지침(D-00) 위반 — `VisionResponsePacket` 내 삼항 연산자 ternary 사용

**File:** `WPF_Example/TcpServer/VisionResponsePacket.cs:379-381`
**Issue:** v2.6 경로(비Phase 48 코드)의 FAI 결과 직렬화 루프에서 사용된 기존 ternary/삼항 연산자 패턴은 Phase 48 검토 범위이므로 명시한다. 단, 이 코드는 Phase 48 이전에 존재하던 코드이며 v2.6 경로(`IsDynamicFAI = true`이면서 `UseProtocolV1 = false`)에서만 실행된다. Phase 48 신규 코드(`BuildResultMessageV1`, `MapCycleJudgement`, `MapFaiJudgement`, `BuildFaiItemsV1`)는 D-00을 잘 준수하고 있다. 기존 코드와의 혼재는 의도된 것으로 보인다.

**Fix:** 기존 코드 수정은 별도 QUAL-01 phase에서 수행. Phase 48 신규 코드는 적절히 준수 중.

---

### IN-03: `EncodingType` static 필드와 `Header`/`Trailer` static 필드의 일관성

**File:** `WPF_Example/TcpServer/TcpServer.cs:343-344`
**Issue:** `Header`와 `Trailer`도 `public static byte`이므로 CR-01에서 언급된 static 오염 문제와 동일한 구조를 가진다. Phase 48 이전부터 있던 코드이므로 신규 결함은 아니나, CR-01을 수정할 때 일관성 있게 같이 인스턴스 필드로 전환할 것을 권고한다.

---

### IN-04: `SiteStatusPacket`의 `SetIdentifier` 분기 — v1.0 시 `(ESite)packet.Site` 직접 캐스팅

**File:** `WPF_Example/Custom/TcpServer/ResourceMap.cs:148`
**Issue:** `SiteStatus` case에서 `packet.Identifier = Find(EResource.Sequence, (ESite)packet.Site);`가 UseProtocolV1 분기 없이 직접 캐스팅한다. v1.0에서 Site=1→ESite.Top 슬롯, Site=2→ESite.Side 슬롯 매핑이 필요하다면 이 경로도 `ResolveSiteSlot`을 통해야 한다. 현재 v1.0 프로토콜에서 SiteStatus 커맨드가 사용되지 않으면 문제 없지만, 사용된다면 WR-01과 동일하게 KeyNotFoundException이 발생한다.

**Fix:** WR-01과 동일한 방식으로 v1.0 분기를 SiteStatus case에도 추가한다.

---

### IN-05: `ExcelExportService` — 자재번호 행 추가로 테이블 헤더 행 오프셋 변경 (하드코딩 `hr = 6`)

**File:** `WPF_Example/Custom/Export/ExcelExportService.cs:56`
**Issue:** `int hr = 6;` 에 주석으로 "자재번호 행(행 4) 추가에 따른 오프셋 조정 (5→6)"이라고 설명되어 있다. 행 번호가 매직 넘버로 하드코딩되어 있어, 향후 메타 행이 추가되면 이 오프셋을 다시 수동으로 조정해야 한다. D-00 규칙(매직 넘버 금지)에 해당한다.

**Fix:**
```csharp
private const int META_ROW_COUNT = 4;  // 모델명/검사일시/종합판정/자재번호
private const int TABLE_HEADER_ROW = META_ROW_COUNT + 2; // 빈 줄 1개 여유

// Export() 내
int hr = TABLE_HEADER_ROW; // 6 하드코딩 대신 상수 사용
```

---

### IN-06: `TestPacket.IndexNumber` 기본값 초기화 — 필드 선언과 `SENTINEL_NO_MATERIAL` 상수 중복

**File:** `WPF_Example/TcpServer/VisionRequestPacket.cs:441`
**Issue:** `public int IndexNumber { get; set; } = SENTINEL_NO_MATERIAL;` — `SENTINEL_NO_MATERIAL`은 `VisionRequestPacket` 클래스에 `public const int`로 선언되어 있고, `TestPacket`은 `VisionRequestPacket`을 상속하므로 `SENTINEL_NO_MATERIAL`을 직접 참조할 수 있다. 현재 코드는 올바르다. 단, `CaptureImageSaveService`에 `FILENAME_NO_MATERIAL = -1`이라는 동일 의미의 상수가 중복 선언되어 있다. 두 상수는 같은 도메인 개념(-1 sentinel)이므로 `VisionRequestPacket.SENTINEL_NO_MATERIAL`을 공유하거나 동일 파일에 집중시키는 것이 단일 소스 원칙에 부합한다.

**Fix:** `CaptureImageSaveService.FILENAME_NO_MATERIAL`을 제거하고 `VisionRequestPacket.SENTINEL_NO_MATERIAL`을 참조하거나, 공용 상수 파일(예: `Define/ProtocolConstants.cs`)로 이동한다.

---

_Reviewed: 2026-06-22T14:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
