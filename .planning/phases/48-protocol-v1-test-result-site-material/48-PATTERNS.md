# Phase 48: 제어 프로토콜 v1.0 — Pattern Map

**Mapped:** 2026-06-22
**Files analyzed:** 9 (수정 대상) + 2 (전파 체인 중간 노드)
**Analogs found:** 9 / 9

---

## LOCKED 코딩 규약 (모든 제어/프로토콜 코드에 우선 적용)

출처: `.planning/refs/control-sequence-coding-guideline.md`
**CLAUDE.md "파일 스타일 따르기"보다 우선. planner/executor 반드시 준수.**

```
- 헝가리언 변수 접두사 필수:
    bool  → b    (bIsReady, bIsValid)
    int   → n    (nSiteNum, nZIndex)
    string→ sz   (szField, szMaterial)
    멤버  → m_   (m_nCount, m_bIsReady)
- 분기: if / else if / else 전용. 삼항 ?: 금지. null병합 ?? 금지.
- 조건식은 bool 변수에 먼저 담는다:
    나쁜: if (dataList.Length > 2 && dataList[1] != "null")
    좋은: bool bHasMaterial = dataList.Length > 1 && dataList[1] != "null";
          if (bHasMaterial) { ... }
- 중첩 2단계 초과 시 함수 분리.
- 매직 넘버 금지 → 상수/enum.
- 함수 30줄 한도 → 초과 시 단일책임 함수 분리.
- 동사+목적어 함수명: ParseTestFields, BuildResultMessage, MapSiteToSequence.
```

---

## File Classification

| 수정/생성 파일 | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `WPF_Example/TcpServer/VisionRequestPacket.cs` — TestPacket 파서+속성 | protocol-parser | request-response | 동일 파일 내 `CMD_RECV_LIGHT` 파싱 블록 (lines 216-258) | exact |
| `WPF_Example/TcpServer/VisionResponsePacket.cs` — RESULT 직렬화 | protocol-serializer | request-response | 동일 파일 내 `IsDynamicFAI` 직렬화 블록 (lines 353-370) | exact |
| `WPF_Example/TcpServer/VisionServer.cs` — Port/Encoding | config | request-response | `WPF_Example/TcpServer/TcpServer.cs` (lines 319-352) | role-match |
| `WPF_Example/Custom/TcpServer/ResourceMap.cs` — Site 2-PC 매핑 | mapper | request-response | 동일 파일 전체 (lines 33-93) | exact |
| `WPF_Example/Setting/SystemSetting.cs` — PC 역할 속성 | config | CRUD | 동일 파일 내 `ServerPort`, `SaveFailImage` 속성 (lines 36-100) | exact |
| `WPF_Example/UI/ViewModel/CycleResultDto.cs` — 자재번호 필드 | model | CRUD | 동일 파일 내 `RecipeName` 필드 (line 16) | exact |
| `WPF_Example/Utility/CaptureImageSaveService.cs` — 파일명 자재번호 | service | file-I/O | 동일 파일 `BuildFileName` (lines 174-184) | exact |
| `WPF_Example/Custom/Export/ExcelExportService.cs` — xlsx 열 자재번호 | service | file-I/O | 동일 파일 메타헤더 블록 (lines 35-50) | exact |
| `WPF_Example/Sequence/Sequence/SequenceBase.cs` — 자재번호 전파 저장 | model | request-response | 동일 파일 `RequestPacket` / `TargetID` 필드 (lines 42, 69) | role-match |

---

## Pattern Assignments

---

### `VisionRequestPacket.cs` — TestPacket 유연 파서 + 자재번호 속성

**Analog:** 동일 파일 내 `CMD_RECV_LIGHT` 파싱 (lines 216-258) + `CMD_RECV_TEST` 현행 (lines 259-288)

#### 현행 고정 인덱스 패턴 (교체 대상, lines 259-288):
```csharp
case CMD_RECV_TEST:
    packet = new TestPacket();
    TestPacket testPacket = packet.AsTest();

    dataList = msgList[1].Split(VisionServer.MSG_CONTENTS_SEPERATOR);
    if (dataList.Length < 3) return null;         // 매직 숫자 < 3

    // site: dataList[0] — 고정 인덱스
    if (Int32.TryParse(dataList[0], out siteNum) == false) { return null; }
    testPacket.Site = siteNum;

    // test kind: dataList[1] — 고정 인덱스
    if (Int32.TryParse(dataList[1], out testKind) == false) { return null; }
    testPacket.TestType = testKind;

    // test ID: dataList[2] — 고정 인덱스
    testID = dataList[2];
    testPacket.TestID = testID;
    break;
```

#### 교체 목표 패턴 (유연 파서 — 헝가리언/if-else 규약 적용):
**근거: D-02 (필드 이름/순서 매핑 + 누락 시 기본값 폴백), D-00 (헝가리언).**

```csharp
// v1.0 TEST 포맷: $TEST:site,MaterialNumber,null,z_index@
//   [0]=site  [1]=MaterialNumber(자재번호)  [2]=null(예약)  [3]=z_index
// 유연 파서: 필드 개수 1개 이상이면 site만 필수, 나머지는 누락 시 sentinel 폴백.
// 헝가리언 + if/else + 조건 변수화 — control-sequence-coding-guideline 준수.

private static bool TryParseTestFields(string[] dataList, TestPacket testPacket)
{
    // site (필수 — index 0)
    bool bHasSite = dataList.Length >= 1;
    if (!bHasSite) { return false; }

    int nSiteNum = 0;
    bool bSiteValid = Int32.TryParse(dataList[0], out nSiteNum);
    if (!bSiteValid) { return false; }
    testPacket.Site = nSiteNum;

    // MaterialNumber (선택 — index 1, 누락/null문자열 → sentinel -1)
    bool bHasMaterial = dataList.Length >= 2;
    if (bHasMaterial)
    {
        string szRaw = dataList[1];
        bool bIsNullPlaceholder = string.IsNullOrEmpty(szRaw) || szRaw == "null";
        if (bIsNullPlaceholder)
        {
            testPacket.IndexNumber = SENTINEL_NO_MATERIAL;
        }
        else
        {
            int nMaterial = 0;
            bool bMaterialValid = Int32.TryParse(szRaw, out nMaterial);
            if (bMaterialValid)
            {
                testPacket.IndexNumber = nMaterial;
            }
            else
            {
                testPacket.IndexNumber = SENTINEL_NO_MATERIAL;
            }
        }
    }
    else
    {
        testPacket.IndexNumber = SENTINEL_NO_MATERIAL;
    }

    // z_index (선택 — index 3 v1.0, 누락 → sentinel -1)
    bool bHasZIndex = dataList.Length >= 4;
    if (bHasZIndex)
    {
        int nZIndex = 0;
        bool bZValid = Int32.TryParse(dataList[3], out nZIndex);
        if (bZValid)
        {
            testPacket.TestID = nZIndex.ToString();
        }
        else
        {
            testPacket.TestID = SENTINEL_Z_INDEX_STR;
        }
    }
    else
    {
        testPacket.TestID = SENTINEL_Z_INDEX_STR;
    }

    return true;
}
```

#### TestPacket 클래스 — 자재번호 속성 추가 패턴 (lines 360-368):
```csharp
// 현행 TestPacket (lines 360-368):
public class TestPacket : VisionRequestPacket {
    public int TestType { get; set; }
    public string TestID { get; set; }       // z_index 문자열 보관
    public string Identifier2 { get; set; }

    public TestPacket() : base(VisionRequestType.Test) { }
}

// 추가할 속성:
//   IndexNumber: 자재번호(int). 미수신 시 -1(SENTINEL_NO_MATERIAL).
//   상수 SENTINEL_NO_MATERIAL = -1; SENTINEL_Z_INDEX_STR = "-1";
```

#### v2.6 플래그 분기 패턴 (D-06) — `CMD_RECV_TEST` switch 블록:
```csharp
// v2.6/v1.0 공존: SystemSetting.UseProtocolV1 플래그로 분기.
// v1.0 파서(유연)를 새 private 메서드로 분리하고 기존 v2.6 블록은 else 보존.
bool bUseV1 = SystemSetting.Handle.UseProtocolV1;
if (bUseV1)
{
    bool bParseOk = TryParseTestFieldsV1(dataList, testPacket);
    if (!bParseOk) { return null; }
}
else
{
    bool bParseOk = TryParseTestFieldsV26(dataList, testPacket);
    if (!bParseOk) { return null; }
}
```

---

### `VisionResponsePacket.cs` — RESULT v1.0 직렬화

**Analog:** 동일 파일 내 `IsDynamicFAI` 직렬화 블록 (lines 353-370)

#### 현행 Dynamic FAI 직렬화 패턴 (lines 353-370) — 복사 기반:
```csharp
// v1.0 RESULT 포맷: $RESULT:site;P|F|B;count;id1=val1=OK|NG,...@
// 구분자: 헤더(;) / 항목(,) / 항목내부(=)
// 현행 msg_contents_seperator = ',' → 헤더와 항목을 ';' 로 분리하는 v1.0은
// 직렬화 메서드를 별도 BuildResultMessageV1() 로 분리해야 한다.

// [현행 패턴 — 복사 기준]:
if (testPacket.IsDynamicFAI) {
    msg += testPacket.GetResultString();           // cycle P/F/N
    msg += VisionServer.MSG_CONTENTS_SEPERATOR;    // ','
    msg += testPacket.FAICount.ToString();
    for (int i = 0; i < testPacket.FAICount; i++) {
        msg += VisionServer.MSG_CONTENTS_SEPERATOR;
        var faiData = testPacket.FAIResults[i];
        string faiCode;
        if (faiData.Result == EVisionResultType.OK) faiCode = TEST_RESULT_PASS;
        else if (faiData.Result == EVisionResultType.NotExist) faiCode = TEST_RESULT_NOTEXIST;
        else faiCode = TEST_RESULT_FAIL;
        msg += faiCode;
        msg += VisionServer.MSG_CONTENTS_SEPERATOR;
        msg += faiData.DistanceMm.ToString("0.000");
    }
}
```

#### v1.0 직렬화 목표 패턴 (헝가리언/if-else 규약, 30줄 한도):
```csharp
// v1.0: $RESULT:site;P|F|B;count;id=val=OK|NG,...@
// 상수 선언:
public const char MSG_RESULT_HEADER_SEP = ';';   // 헤더 구분자
public const char MSG_RESULT_ITEM_SEP   = ',';   // 항목 구분자
public const char MSG_RESULT_INNER_SEP  = '=';   // 항목내부 구분자
public const string TEST_RESULT_BUFFER  = "B";   // 진행 중

private static string BuildResultMessageV1(TestResultPacket testPacket)
{
    // 헝가리언 + if/else. 30줄 초과 시 AppendFaiItems() 분리.
    string szMsg = "";
    szMsg += CMD_SEND_TEST;
    szMsg += VisionServer.MSG_CMD_SEPERATOR;        // ':'
    szMsg += testPacket.Site.ToString();
    szMsg += MSG_RESULT_HEADER_SEP;                 // ';'
    szMsg += MapCycleJudgement(testPacket);          // P/F/B
    szMsg += MSG_RESULT_HEADER_SEP;
    szMsg += testPacket.FAICount.ToString();
    szMsg += MSG_RESULT_HEADER_SEP;
    szMsg += BuildFaiItems(testPacket);              // id=val=OK|NG,...
    return szMsg;
}

private static string MapCycleJudgement(TestResultPacket testPacket)
{
    // P/F/B 3-state 매핑. Phase 48 범위: 직렬화 구조만. 판정 엔진은 Phase 49.
    bool bIsBuffer = testPacket.IsBuffer;
    if (bIsBuffer) { return TEST_RESULT_BUFFER; }

    bool bIsPass = testPacket.Result == EVisionResultType.OK;
    if (bIsPass) { return TEST_RESULT_PASS; }

    return TEST_RESULT_FAIL;
}
```

#### TestResultPacket — 신규 속성 (v1.0 판정 상태):
```csharp
// v1.0 B 상태 지원용 플래그. Phase 49 에서 판정 엔진 완성 시 채워짐.
// Phase 48 파서 구조에서는 자리만 마련.
public bool IsBuffer { get; set; } = false;
```

---

### `VisionServer.cs` — Port 7701 / UTF-8 / `@` 종료

**Analog:** `WPF_Example/TcpServer/TcpServer.cs` (lines 319-352)

#### 현행 TcpServer 생성자 패턴 (lines 340-352):
```csharp
public TcpServer() {
    PortNum = SystemSetting.Handle.ServerPort;   // INI 에서 읽음
    mListener = new TcpListener(IPAddress.Any, PortNum);
    mListener.Start();
    OnAlarm += OnAlarmProcess;
    mConnectionThread = new Thread(ConnectionExecute);
    mConnectionThread.IsBackground = true;
    mConnectionThread.Name = "TcpServer";
    mConnectionThread.Start();
}
```

#### 현행 인코딩 스위치 (lines 151-157):
```csharp
case MessageEncodingType.Utf8:
    return Encoding.UTF8.GetBytes(msg);
```

#### v1.0 변경 지점:
```csharp
// VisionServer 생성자: Header/Trailer 는 이미 '$'/'@' 로 설정됨 (lines 19-21).
// Port: SystemSetting.Handle.ServerPort 를 7701 로 기본값 변경 (SystemSetting.cs 에서 수행).
// Encoding: TcpServer.EncodingType = MessageEncodingType.Utf8 으로 설정.
// VisionServer 생성자에서 EncodingType = MessageEncodingType.Utf8 호출 추가.
public VisionServer() : base() {
    Header = (byte)MSG_STX;
    Trailer = (byte)MSG_ETX;
    SetEncoding(MessageEncodingType.Utf8);   // v1.0: UTF-8 강제
}
```

---

### `WPF_Example/Custom/TcpServer/ResourceMap.cs` — Site 2-PC 재정합

**Analog:** 동일 파일 전체 (lines 1-96)

#### 현행 ESite + Initialize 패턴 (lines 10-55):
```csharp
// 현행 ESite (교체 대상):
public enum ESite : int {
    Top = 1,
    Side = 2,
    Bottom = 3,
}

// 현행 Initialize (교체 대상):
public void Initialize() {
    Add(EResource.Camera, ESite.Top, DeviceHandler.CAMERA_TOP);
    Add(EResource.Camera, ESite.Side, DeviceHandler.CAMERA_SIDE);
    Add(EResource.Camera, ESite.Bottom, DeviceHandler.CAMERA_BOTTOM);

    Add(EResource.Light, ESite.Top, LightHandler.LIGHT_TOP);
    Add(EResource.Light, ESite.Side, LightHandler.LIGHT_SIDE);
    Add(EResource.Light, ESite.Bottom, LightHandler.LIGHT_BOTTOM);

    Add(EResource.Sequence, ESite.Top, SequenceHandler.SEQ_TOP);
    Add(EResource.Sequence, ESite.Side, SequenceHandler.SEQ_SIDE);
    Add(EResource.Sequence, ESite.Bottom, SequenceHandler.SEQ_BOTTOM);

    Add(EResource.Action, ESite.Top, ETestType.Inspection, SequenceHandler.ACT_INSPECT);
    Add(EResource.Action, ESite.Side, ETestType.Inspection, SequenceHandler.ACT_INSPECT);
    Add(EResource.Action, ESite.Bottom, ETestType.Inspection, SequenceHandler.ACT_INSPECT);
}
```

#### v1.0 목표 패턴 (2-PC, SystemSetting.PcRole 기반):
```csharp
// 새 enum — 기존 ESite 대신 또는 병행.
// PC 역할(SystemSetting.PcRole)에 따라 Site 번호의 의미가 달라진다.
// PC1: Site1=TOP / Site2=BOTTOM
// PC2: Site1=SIDE_1 / Site2=SIDE_2

public enum EPcRole : int {
    PC1_TopBottom = 1,
    PC2_Side1Side2 = 2,
}

// v2.6/v1.0 공존 (D-06): SystemSetting.UseProtocolV1 플래그 분기.
public void Initialize()
{
    bool bUseV1 = SystemSetting.Handle.UseProtocolV1;
    if (bUseV1)
    {
        InitializeV1();
    }
    else
    {
        InitializeV26();   // 기존 로직 보존
    }
}

private void InitializeV1()
{
    // PC 역할에 따라 Site→Sequence/Camera 매핑을 동적으로 구성.
    // 헝가리언 + if/else (삼항 금지).
    int nPcRole = (int)SystemSetting.Handle.PcRole;
    bool bIsPC1 = nPcRole == (int)EPcRole.PC1_TopBottom;

    if (bIsPC1)
    {
        // PC1: Site1=TOP, Site2=BOTTOM
        Add(EResource.Camera,   ESiteV1.Site1, DeviceHandler.CAMERA_TOP);
        Add(EResource.Camera,   ESiteV1.Site2, DeviceHandler.CAMERA_BOTTOM);
        Add(EResource.Light,    ESiteV1.Site1, LightHandler.LIGHT_TOP);
        Add(EResource.Light,    ESiteV1.Site2, LightHandler.LIGHT_BOTTOM);
        Add(EResource.Sequence, ESiteV1.Site1, SequenceHandler.SEQ_TOP);
        Add(EResource.Sequence, ESiteV1.Site2, SequenceHandler.SEQ_BOTTOM);
        Add(EResource.Action,   ESiteV1.Site1, ETestType.Inspection, SequenceHandler.ACT_INSPECT);
        Add(EResource.Action,   ESiteV1.Site2, ETestType.Inspection, SequenceHandler.ACT_INSPECT);
    }
    else
    {
        // PC2: Site1=SIDE_1, Site2=SIDE_2
        Add(EResource.Camera,   ESiteV1.Site1, DeviceHandler.CAMERA_SIDE);
        Add(EResource.Camera,   ESiteV1.Site2, DeviceHandler.CAMERA_SIDE);
        Add(EResource.Light,    ESiteV1.Site1, LightHandler.LIGHT_SIDE);
        Add(EResource.Light,    ESiteV1.Site2, LightHandler.LIGHT_SIDE);
        Add(EResource.Sequence, ESiteV1.Site1, SequenceHandler.SEQ_SIDE);
        Add(EResource.Sequence, ESiteV1.Site2, SequenceHandler.SEQ_SIDE);
        Add(EResource.Action,   ESiteV1.Site1, ETestType.Inspection, SequenceHandler.ACT_INSPECT);
        Add(EResource.Action,   ESiteV1.Site2, ETestType.Inspection, SequenceHandler.ACT_INSPECT);
    }
}
```

---

### `WPF_Example/Setting/SystemSetting.cs` — PC 역할 + v1.0 플래그 속성

**Analog:** 동일 파일 내 `ServerPort`(line 36), `AutoLogoutWhenRecvTest`(line 98), `SaveFailImage`(line 100) 속성

#### 기존 속성 추가 패턴 (lines 34-100):
```csharp
// 기존 패턴 (int 속성):
[Category("Connection|Server")]
public int ServerPort { get; set; } = 2505;

// 기존 패턴 (bool 속성):
public bool SaveFailImage { get; set; } = false;
```

#### v1.0 신규 속성:
```csharp
// v1.0 프로토콜 활성화 플래그 (D-06: v2.6/v1.0 공존).
[Category("Connection|Protocol")]
public bool UseProtocolV1 { get; set; } = false;

// PC 역할 (D-03: 빌드 상수 대신 설정으로 지정).
// 1 = PC1(TOP/BOTTOM), 2 = PC2(SIDE_1/SIDE_2).
[Category("Connection|Server")]
public int PcRole { get; set; } = 1;

// v1.0 Port 기본값: 7701 (기존 2505 → 7701). UseProtocolV1 true 시에만 유효.
// ServerPort 기본값은 v2.6 호환 유지(2505). v1.0 포트는 별도 속성으로 분리.
public int ServerPortV1 { get; set; } = 7701;
```

> **주의:** `SystemSetting.Load()`는 reflection 기반 INI 파싱 (lines 186-253).
> 신규 속성을 추가하면 자동으로 저장/로드된다. 기존 INI 에 키 미존재 시 0/false로 로드되는
> 기지 문제(`reference_parambase_missing_key_zeroes_default.md`)가 있으므로,
> `UseProtocolV1` 기본값 `false` 는 0으로 로드되어도 올바르게 동작한다.
> `PcRole` 기본값 `1` 은 구 INI 에서 0으로 로드됨 → Custom SystemSetting partial 클래스에서
> Load 오버라이드 또는 `if (PcRole == 0) PcRole = 1;` 방어 코드 필요.

---

### `WPF_Example/UI/ViewModel/CycleResultDto.cs` — 자재번호 필드 추가

**Analog:** 동일 파일 내 `RecipeName` 필드 (line 16)

#### 현행 CycleResultDto 상단 (lines 12-26):
```csharp
public class CycleResultDto
{
    public DateTime InspectionTime { get; set; }
    public string RecipeName { get; set; }
    public string OverallJudgement { get; set; }
    public string CycleFolderPath { get; set; }
    public List<ShotResultDto> Shots { get; set; } = new List<ShotResultDto>();
}
```

#### 추가할 필드:
```csharp
// 자재번호 (TestPacket.IndexNumber 에서 전파됨). -1 = 미수신(sentinel).
// RecipeName 패턴과 동일: 단순 int 프로퍼티, JSON 직렬화 포함.
public int IndexNumber { get; set; } = -1;
```

---

### `WPF_Example/Utility/CaptureImageSaveService.cs` — 파일명 자재번호 반영

**Analog:** 동일 파일 `BuildFileName` (lines 174-184)

#### 현행 BuildFileName (lines 174-184):
```csharp
public static string BuildFileName(
    string prefix, string sequence, string faiName,
    string measurePointSegment, string judgement, DateTime ts)
{
    string seq  = SanitizeFilePart(sequence, "SEQ");
    string fai  = SanitizeFilePart(faiName, "FAI");
    string seg  = SanitizeFilePart(measurePointSegment, "");
    string judge = SanitizeFilePart(judgement, "");
    string time = ts.ToString("HHmmssfff");
    string name = prefix + "_" + seq + "_" + fai;
    if (!string.IsNullOrEmpty(seg))   { name += "_" + seg; }
    if (!string.IsNullOrEmpty(judge)) { name += "_" + judge; }
    return name + "_" + time + ".jpg";
}
```

#### 자재번호 추가 패턴:
```csharp
// 오버로드 추가 — 기존 시그니처 호환 유지, 자재번호 선택.
// 자재번호 -1 이면 파일명에 포함하지 않음(sentinel 처리).
// SanitizeFilePart 패턴 그대로 재사용.
// 헝가리언 + if/else.
public static string BuildFileName(
    string prefix, string sequence, string faiName,
    string measurePointSegment, string judgement, DateTime ts, int nIndexNumber)
{
    string seq    = SanitizeFilePart(sequence, "SEQ");
    string fai    = SanitizeFilePart(faiName, "FAI");
    string seg    = SanitizeFilePart(measurePointSegment, "");
    string judge  = SanitizeFilePart(judgement, "");
    string time   = ts.ToString("HHmmssfff");

    bool bHasMaterial = nIndexNumber >= 0;
    string szMat = "";
    if (bHasMaterial)
    {
        szMat = nIndexNumber.ToString();
    }

    string name = prefix + "_" + seq + "_" + fai;
    if (!string.IsNullOrEmpty(szMat))  { name += "_M" + szMat; }
    if (!string.IsNullOrEmpty(seg))    { name += "_" + seg; }
    if (!string.IsNullOrEmpty(judge))  { name += "_" + judge; }
    return name + "_" + time + ".jpg";
}
```

---

### `WPF_Example/Custom/Export/ExcelExportService.cs` — xlsx 자재번호 열

**Analog:** 동일 파일 메타헤더 블록 (lines 35-50)

#### 현행 메타헤더 블록 (lines 35-50):
```csharp
ws.Cell(1, 1).Value = "모델명";
ws.Cell(1, 2).Value = cycle.RecipeName != null ? cycle.RecipeName : "";
ws.Cell(2, 1).Value = "검사일시";
ws.Cell(2, 2).Value = cycle.InspectionTime.ToString("yyyy-MM-dd HH:mm:ss");
ws.Cell(3, 1).Value = "종합판정";
ws.Cell(3, 2).Value = cycle.OverallJudgement != null ? cycle.OverallJudgement : "";
```

**주의:** 현행 코드에 삼항 `?:` 사용. 제어/프로토콜 코드 범위(D-00)는 VisionRequestPacket/
VisionResponsePacket/VisionServer/ResourceMap 신규 코드에만 적용.
ExcelExportService 는 기존 스타일(CLAUDE.md) 유지 가능하나,
자재번호 추가 셀은 일관성을 위해 if/else 권장.

#### 자재번호 메타 행 추가 패턴:
```csharp
// 기존 행 3 이후 행 4에 자재번호 추가. 테이블 헤더(hr)도 1행 이동.
ws.Cell(4, 1).Value = "자재번호";
// IndexNumber -1 = 미수신 → "-" 표시.
if (cycle.IndexNumber >= 0)
{
    ws.Cell(4, 2).Value = cycle.IndexNumber;
}
else
{
    ws.Cell(4, 2).Value = "-";
}
int hr = 6;   // 기존 5 → 6으로 이동 (자재번호 행 추가에 따른 오프셋).
```

---

## 자재번호 전파 체인 Pattern (Shared)

**자재번호(IndexNumber)가 흐르는 전체 경로:**

```
TestPacket.IndexNumber          ← VisionRequestPacket.cs (D-02 유연 파서에서 채움)
    ↓  (SequenceBase.Start/StartAll)
SequenceBase.RequestPacket      ← SequenceBase.cs line 69/291/346
    ↓  (Action에서 접근)
pMyParam.Parent.RequestPacket.IndexNumber  ← 기존 TestID 전파 패턴(line 345-349)과 동일
    ↓  (AddResponse → CycleResultSerializer.BuildDto)
CycleResultDto.IndexNumber      ← CycleResultDto.cs (신규 필드)
    ↓  (동시 두 소비자)
CaptureImageSaveService.BuildFileName(..., nIndexNumber)   ← 파일명
ExcelExportService.Export(cycle, ...)                      ← xlsx 자재번호 행
```

#### 기존 TestID 전파 analog (lines 343-361 in Action_TopInspection.cs):
```csharp
// TestID(현행) → IndexNumber(신규)와 동일 전파 패턴.
TestPacket requestPacket = null;
if (pMyParam.Parent != null) {
    requestPacket = pMyParam.Parent.RequestPacket;   // SequenceBase.RequestPacket
}
string testId = null;
if (requestPacket != null) {
    testId = requestPacket.TestID;                   // ← IndexNumber 로 교체
}
// → RawImageSaveRequest.TestId 로 전달 (파일명 포함)
SystemHandler.Handle.RawImageSaver.Enqueue(new RawImageSaveRequest {
    TestId = testId,     // ← IndexNumber 전달 지점
    ...
});
```

#### CycleResultSerializer.BuildDto 진입점 (lines 35-50 in CycleResultSerializer.cs):
```csharp
// 현행 BuildDto 시그니처:
public static CycleResultDto BuildDto(
    InspectionRecipeManager recipeManager,
    EVisionResultType cycleResult,
    DateTime when,
    string recipeName,
    string ownerSequenceName = null)

// v1.0 확장: IndexNumber 추가.
// 호출부(BatchRunService.cs line 127, AddResponse 내부)에서 전달.
public static CycleResultDto BuildDto(
    InspectionRecipeManager recipeManager,
    EVisionResultType cycleResult,
    DateTime when,
    string recipeName,
    string ownerSequenceName = null,
    int nIndexNumber = -1)   // 추가
{
    var dto = new CycleResultDto {
        InspectionTime = when,
        RecipeName = recipeNameStr,
        OverallJudgement = MapJudgement(cycleResult),
        IndexNumber = nIndexNumber   // 추가
    };
    ...
}
```

---

## Shared Patterns

### 프로토콜 파싱 — split + TryParse 패턴
**Source:** `VisionRequestPacket.cs` lines 147-163 (RecipeChange 파서, 가장 단순/명확)
**Apply to:** 모든 신규 파서 함수

```csharp
// 단계: (1) Split, (2) Length 체크, (3) TryParse, (4) 실패 시 return null or false
dataList = msgList[1].Split(VisionServer.MSG_CONTENTS_SEPERATOR);
if (dataList.Length < 2) return null;
if (Int32.TryParse(dataList[0], out siteNum) == false) { return null; }
```

### 구분자 상수
**Source:** `VisionServer.cs` lines 9-12
```csharp
public const char MSG_STX = '$';
public const char MSG_ETX = '@';
public const char MSG_CONTENTS_SEPERATOR = ',';
public const char MSG_CMD_SEPERATOR = ':';
```
v1.0 추가 구분자는 `VisionResponsePacket.cs`에 상수로 선언:
```csharp
public const char MSG_RESULT_HEADER_SEP = ';';
public const char MSG_RESULT_INNER_SEP  = '=';
```

### SystemSetting INI Load 패턴
**Source:** `SystemSetting.cs` lines 186-253 (reflection 기반 키-값 파싱)
**신규 속성 추가 시:** public 프로퍼티 선언만으로 자동 저장/로드. 단 기본값 ≠ 0 이면 Load 오버라이드 필요.

### 에러 핸들링 — 파서 실패
**Source:** `VisionServer.cs` lines 50-56
```csharp
catch (ArgumentOutOfRangeException argumentException) {
    PerformOnAlarm(new AlarmEventArgs(..., argumentException.Message));
}
catch (IndexOutOfRangeException indexException) {
    PerformOnAlarm(new AlarmEventArgs(..., indexException.Message));
}
```

---

## No Analog Found

없음. 모든 파일에 대해 동일 또는 역할-일치 analog가 코드베이스 내에 존재한다.

---

## Metadata

**Analog search scope:** `WPF_Example/TcpServer/`, `WPF_Example/Custom/TcpServer/`, `WPF_Example/Setting/`, `WPF_Example/UI/ViewModel/`, `WPF_Example/Utility/`, `WPF_Example/Custom/Export/`, `WPF_Example/Sequence/`
**Files scanned:** 11 (read) + 추가 grep 검색
**Pattern extraction date:** 2026-06-22
