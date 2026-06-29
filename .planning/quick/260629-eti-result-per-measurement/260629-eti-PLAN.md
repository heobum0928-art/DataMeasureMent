---
phase: quick-260629-eti
plan: 01
type: execute
wave: 1
depends_on: []
files_modified: [WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs]
autonomous: true
requirements: [ETI-RESULT-PER-MEASUREMENT]

must_haves:
  truths:
    - "$RESULT TCP 응답이 FAI 단위가 아니라 측정(Measurement) 단위로 항목을 전송한다"
    - "측정이 2개 이상인 FAI는 측정마다 항목 1개씩 펴서 전송된다 (P2 불량값 은폐 제거)"
    - "측정 1개 FAI는 id=FAIName 그대로, 다측정 FAI는 id=FAIName_P{1-based} 로 송신된다"
    - "측정 단위 datum/align-skip 은 'N'(NotExist), 측정 NG는 'NG', 그 외 'OK' 로 분류된다"
    - "사이클 종합 P/F/B 결과는 변경 전과 동일하다 (m_bCycleHasNG 누적 동등)"
    - "Debug/x64 빌드가 error 0 으로 통과한다"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs"
      provides: "측정 단위 AddFaiResult 재작성 + ClassifyMeasurement 헬퍼 신규"
      contains: "ClassifyMeasurement"
  key_links:
    - from: "AddFaiResult"
      to: "fai.Measurements 순회 + packet.FAIResults.Add(FAIResultData ...)"
      via: "측정마다 항목 1개"
      pattern: "foreach.*Measurements"
    - from: "ClassifyMeasurement"
      to: "m_bCycleHasNG"
      via: "측정 단위 NG/skip 누적"
      pattern: "m_bCycleHasNG = true"
---

<objective>
$RESULT TCP 응답을 "FAI 단위" → "측정(Measurement) 단위" 전송으로 전환한다 (Vision_Protocol_v1.1 B안).

현재 `AddFaiResult` 는 FAI당 항목 1개만 보내고 값으로 `fai.MeasuredValue`(=Measurements[0]=첫 측정 P1)만 실어, FAI 안 두 번째 측정(P2) 불량값이 은폐된다. 측정마다 항목 1개로 펴서 전 측정값/판정을 전송하도록 `AddFaiResult` 를 재작성하고, 측정 단위 3-state 분류 헬퍼 `ClassifyMeasurement` 를 신규 추가한다.

Purpose: P2(두 번째 측정) 불량이 와이어에서 사라지는 데이터 은폐 결함 제거. 제어팀 핸들러 파서가 측정점 단위로 결과를 수신.
Output: InspectionSequence.cs 1파일 수정 (AddFaiResult 재작성 + ClassifyMeasurement 신규). Debug/x64 빌드 PASS.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md

<interfaces>
<!-- 코드베이스에서 추출한 계약. executor 는 코드 탐색 없이 이 계약을 그대로 사용한다. -->

From WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs (측정 단위 결과 필드):
```csharp
public string MeasurementName { get; set; }          // 측정 이름 (id 네이밍에는 사용하지 않음 — id 는 FAIName 기반)
public double LastMeasuredValue { get; set; }        // 휘발성, 측정값(mm). val 로 전송
public bool   LastJudgement { get; set; }            // true=OK / false=NG
public string LastSkipReason { get; set; }           // null/"" = 정상 또는 측정 NG, "DATUM_FAIL"/"ALIGN_FAIL" = datum/align 검출 실패 skip, "NO_IMAGE" = 이미지 없음
```

From WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs:
```csharp
public List<MeasurementBase> Measurements { get; private set; }   // FAI 하위 측정 목록 (0개 = Datum 샷)
public string FAIName { get; set; }                               // FAI 이름. null 가능 → "FAI" 폴백
public double MeasuredValue { get; set; }                         // (유지) Measurements[0] 집계값 — 다른 소비처 호환, TCP 경로는 더 이상 사용 안 함
public bool   IsPass { get; set; }                               // (유지) 다른 소비처 호환
public bool   WasDatumSkipped { get; set; }                       // (유지) FAI 단위 datum-skip 플래그 — ClassifyFai 가 사용
```

From WPF_Example/TcpServer/VisionResponsePacket.cs (와이어 빌더 — 변경 금지, 계약 확인용):
```csharp
public enum EVisionResultType : int { NG = 0, OK = 1, NotExist = 2, ANG = 3, TECHING = 4 }

public class FAIResultData {
    public string FAIName { get; set; }              // id (와이어 직렬화에서 id 로 출력)
    public EVisionResultType Result { get; set; }    // OK/NG/NotExist → 와이어 OK|NG (NotExist 는 BuildFaiItemsV1 의 MapFaiJudgement 에서 NG 로 매핑됨)
    public double DistanceMm { get; set; }           // val
    // 3-state 직접 전달 ctor (이걸 사용):
    public FAIResultData(string name, EVisionResultType result, double distMm);
}

// TestResultPacket.FAIResults : List<FAIResultData> (항목 누적 대상)
// TestResultPacket.FAICount  => FAIResults.Count (자동)
// BuildFaiItemsV1: 각 FAIResultData 를 id=val=judge 로 반복 직렬화. 항목 수만 늘어남 — 무변경.
```

<!-- 와이어 판정 주의: BuildFaiItemsV1 → MapFaiJudgement 는 Result==OK 면 "OK", 그 외(NG/NotExist 포함) "NG" 로 출력한다.
     기존 v1.0 동작과 동일 — 측정 단위 NotExist 도 와이어상 "NG" 토큰으로 나가지만, 사이클 종합은 m_bCycleHasNG 로 별도 F 처리됨. -->
</interfaces>

<!-- 변경 대상 파일은 phase 66 등으로 라인이 드리프트되므로 절대 라인 신뢰 금지.
     아래 content-anchor(메서드 시그니처/주석)로 위치를 찾아 편집할 것. -->
<edit_anchors>
- 재작성 대상 메서드 anchor: `private void AddFaiResult(TestResultPacket packet, FAIConfig fai)`
- 기존 분류 헬퍼 anchor (패턴 복제용, 변경 금지): `private EVisionResultType ClassifyFai(FAIConfig fai)`
- 누적 플래그 anchor (변경 금지): `private bool m_bCycleHasNG = false;`
- 호출처 anchor (변경 금지 — AddFaiResult 시그니처 유지): `AddFaiResult(packet, fai);` (AggregateIndexFais 내부)
</edit_anchors>
</context>

<tasks>

<task type="auto" tdd="false">
  <name>Task 1: AddFaiResult 측정 단위 전환 + ClassifyMeasurement 헬퍼 신규</name>
  <files>WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs</files>
  <behavior>
    - 측정 2개 FAI(예 FAI_A1, P1=OK/P2=NG) → packet.FAIResults 에 항목 2개 추가: id=FAI_A1_P1(OK), id=FAI_A1_P2(NG).
    - 측정 1개 FAI(예 FAI_A2) → 항목 1개: id=FAI_A2 (suffix 없음).
    - 측정 0개 FAI(Datum 샷) → 항목 0개 (기존과 동일, 변화 없음).
    - 측정의 LastSkipReason=="DATUM_FAIL" 또는 "ALIGN_FAIL" → 해당 항목 EVisionResultType.NotExist + m_bCycleHasNG=true.
    - 측정의 LastJudgement==false (skip 아님) → EVisionResultType.NG + m_bCycleHasNG=true.
    - 그 외 → EVisionResultType.OK (m_bCycleHasNG 변화 없음).
    - 항목 val = meas.LastMeasuredValue.
  </behavior>
  <action>
**규칙 엄수 (executor 필독):**
- 수정/추가 라인마다 `//260629 hbk` + 한국어 사유 1줄 (오늘 날짜 260629 — phase 66과 별개 신규 작업).
- 삼항 `?:` 금지 → if-else 사용. 헝가리언 표기. C# 7.2 / .NET 4.8 (switch expression / record / nullable ref / 범위연산자 `..` / `is` 패턴 금지).
- InspectionSequence.cs 기존 Allman 중괄호 스타일 유지 (이 파일은 메서드 `{` 가 같은 줄이 아니라 다음 줄 — 주변 코드 스타일을 그대로 따를 것).
- HOperatorSet 해당 없음. 회귀 0.

**content-anchor 로 위치 찾기 (절대 라인 신뢰 금지):**
변경 대상은 `private void AddFaiResult(TestResultPacket packet, FAIConfig fai)` 메서드 본문. 기존 본문은 다음과 같다(phase 49):
```
EVisionResultType eCode = ClassifyFai(fai);
string szName = fai.FAIName;
if (string.IsNullOrEmpty(szName)) { szName = "FAI"; }
packet.FAIResults.Add(new FAIResultData(szName, eCode, fai.MeasuredValue));
```

**(1) AddFaiResult 본문 재작성** — null 가드는 유지하고, FAI 단위 1항목 → 측정 단위 N항목으로 교체:

- `fai == null` 가드는 그대로 유지 (조기 return).
- FAI 이름 폴백 변수 준비: `string szFaiName = fai.FAIName;` → `string.IsNullOrEmpty(szFaiName)` 면 `szFaiName = "FAI";` (헝가리언, if-else).
- 측정 개수 변수: `int nMeasCount = fai.Measurements.Count;` (Measurements 는 항상 non-null — FAIConfig 에서 `= new List<MeasurementBase>()` 초기화 보장됨).
- `for (int i = 0; i < nMeasCount; i++)` 루프:
    - `MeasurementBase meas = fai.Measurements[i];`
    - null 측정 방어: `if (meas == null) { continue; }`
    - id 네이밍 (삼항 금지 → if-else):
      ```
      string szItemId;
      if (nMeasCount > 1)
      {
          szItemId = szFaiName + "_P" + (i + 1).ToString();   // 1-based 측정 인덱스
      }
      else
      {
          szItemId = szFaiName;                                // 측정 1개면 suffix 없음
      }
      ```
    - 분류: `EVisionResultType eCode = ClassifyMeasurement(meas);`
    - 값: `double dVal = meas.LastMeasuredValue;`
    - 추가: `packet.FAIResults.Add(new FAIResultData(szItemId, eCode, dVal));`
- 루프 종료 후 추가 처리 없음. nMeasCount==0 이면 루프가 0회 → 항목 0개(Datum 샷, 기존 동작 보존).
- **주의**: 더 이상 `fai.MeasuredValue` / `fai.IsPass` / `fai.WasDatumSkipped` 를 TCP 경로에서 읽지 않는다 (필드 자체는 삭제 금지 — 다른 소비처 호환).

**(2) ClassifyMeasurement 헬퍼 신규 추가** — 기존 `ClassifyFai(FAIConfig fai)` 패턴을 측정 단위로 복제. ClassifyFai 바로 아래에 배치:
```
//260629 hbk 측정 단위 3-state 분류 — datum/align-skip('N')·측정 NG('F')는 m_bCycleHasNG 누적. 그 외 OK('P'). (ClassifyFai 측정 단위 복제)
private EVisionResultType ClassifyMeasurement(MeasurementBase meas)
{
    string szSkip = meas.LastSkipReason;                                          //260629 hbk 측정 단위 skip 사유
    bool bDatumSkipped = (szSkip == "DATUM_FAIL") || (szSkip == "ALIGN_FAIL");    //260629 hbk datum/align 검출 실패 skip = 'N'
    if (bDatumSkipped)
    {
        m_bCycleHasNG = true;                                                     //260629 hbk 검출 실패도 사이클 NG 누적
        return EVisionResultType.NotExist;
    }
    bool bNotPass = !meas.LastJudgement;                                          //260629 hbk LastJudgement true=OK
    if (bNotPass)
    {
        m_bCycleHasNG = true;                                                     //260629 hbk 측정 NG → 사이클 NG 누적
        return EVisionResultType.NG;
    }
    return EVisionResultType.OK;                                                  //260629 hbk 정상 측정
}
```
(들여쓰기/중괄호 스타일은 ClassifyFai 와 동일하게 맞출 것 — Allman, 같은 들여쓰기 단.)

**중요 — ClassifyFai 는 삭제하지 말 것.** ClassifyFai 가 다른 곳에서 호출되지 않더라도 그대로 남겨둔다 (회귀 위험 회피). AddFaiResult 만 ClassifyFai → ClassifyMeasurement 순회로 바뀐다.

**누적 동등성 확인:** 기존 ClassifyFai 는 FAI 단위로 m_bCycleHasNG 를 set 했고, fai.WasDatumSkipped 는 "FAI 하위 측정 중 1건이라도 DATUM_FAIL/ALIGN_FAIL 이면 true"(Action_FAIMeasurement 가 집계), fai.IsPass 는 측정 집계 판정이었다. 측정 단위 순회로 바꿔도 "측정 하나라도 skip/NG 면 m_bCycleHasNG=true" 가 되어 사이클 종합 F 결과는 동일하다.
  </action>
  <verify>
    <automated>"/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "C:/Info/Project/DataMeasurement/WPF_Example/DatumMeasurement.csproj" //t:Build //p:Configuration=Debug //p:Platform=x64 //v:minimal 2>&1 | grep -iE "error|Build succeeded"</automated>
  </verify>
  <done>
    AddFaiResult 가 fai.Measurements 를 순회하며 측정마다 packet.FAIResults.Add(new FAIResultData(id, eCode, LastMeasuredValue)) 1건씩 추가한다. 다측정 FAI id=FAIName_P{1-based}, 단측정 FAI id=FAIName. ClassifyMeasurement 헬퍼가 존재하고 datum/align-skip→NotExist+m_bCycleHasNG, !LastJudgement→NG+m_bCycleHasNG, 그 외 OK 를 반환한다. ClassifyFai 는 보존됨. 수정/추가 라인마다 //260629 hbk 주석.
  </done>
</task>

<task type="auto" tdd="false">
  <name>Task 2: Debug/x64 빌드 검증 + 회귀 확인</name>
  <files>WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs</files>
  <action>
빌드 검증 + 코드 정적 확인 (코드 변경 없음 — Task 1 산출물 검증 전용):

**(1) 빌드:** Debug/x64 로 DatumMeasurement.csproj 빌드. error 0 이어야 한다. MSBuild 경로가 다르면 다음 후보를 순차 시도:
- `C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe`
- `C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/MSBuild.exe`
- `C:/Program Files (x86)/Microsoft Visual Studio/2019/BuildTools/MSBuild/Current/Bin/MSBuild.exe`
- PATH 의 `msbuild`
에러 0 + "Build succeeded" 확인.

**(2) 회귀 정적 확인 (변경 금지 항목이 손대지 않았는지 grep 으로 확인):**
- BuildResultMessageV1 / BuildFaiItemsV1 / MapCycleJudgement / MapFaiJudgement (VisionResponsePacket.cs) 무변경 — 이 plan 에서 VisionResponsePacket.cs 는 files_modified 아님. 수정되지 않았어야 함.
- ApplyCycleJudgement / m_bCycleHasNG / m_bCycleDatumFailed 누적 로직(InspectionSequence.cs) 무변경.
- ClassifyFai 보존 확인 (삭제 금지).
- FAIConfig.MeasuredValue / IsPass 필드 보존.

**(3) 측정 단위 항목 늘어남 확인:** AddFaiResult 가 더 이상 `fai.MeasuredValue` 를 FAIResultData val 로 쓰지 않고 `meas.LastMeasuredValue` 를 쓰는지 확인.
  </action>
  <verify>
    <automated>"/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "C:/Info/Project/DataMeasurement/WPF_Example/DatumMeasurement.csproj" //t:Build //p:Configuration=Debug //p:Platform=x64 //v:minimal 2>&1 | grep -iE "error|Build succeeded"</automated>
  </verify>
  <done>
    Debug/x64 빌드 "Build succeeded" + error 0. VisionResponsePacket.cs 와이어 빌더 / ApplyCycleJudgement / ClassifyFai / FAIConfig.MeasuredValue·IsPass 모두 무변경 보존. AddFaiResult 가 meas.LastMeasuredValue 를 val 로 사용.
  </done>
</task>

</tasks>

<verification>
- Debug/x64 빌드 error 0 (DatumMeasurement.csproj, /p:Configuration=Debug /p:Platform=x64).
- AddFaiResult: fai.Measurements 순회, 측정마다 FAIResultData 1건. 다측정 id=FAIName_P{n}, 단측정 id=FAIName, 0측정 항목 0.
- ClassifyMeasurement: datum/align-skip→NotExist+m_bCycleHasNG, NG→NG+m_bCycleHasNG, 그 외 OK.
- 사이클 P/F/B 동등: 측정 하나라도 skip/NG 면 m_bCycleHasNG=true → 마지막 Index 종합 F (변경 전과 동일).
- 무변경: BuildResultMessageV1/BuildFaiItemsV1/MapCycleJudgement/MapFaiJudgement/ApplyCycleJudgement/ClassifyFai/FAIConfig.MeasuredValue·IsPass.
- 모든 수정·추가 라인에 //260629 hbk 한국어 사유 주석.
</verification>

<success_criteria>
- $RESULT 와이어가 측정 단위 항목(id=val=judge)을 전송하고, P2 등 두 번째 측정 불량값이 더 이상 은폐되지 않는다.
- 다측정 FAI id = FAIName_P{1-based}, 단측정 FAI id = FAIName (제어팀 엑셀 예시 정합).
- 사이클 종합 P/F/B 결과가 변경 전과 동일하다.
- Debug/x64 빌드 PASS (error 0).
</success_criteria>

<output>
After completion, create `.planning/quick/260629-eti-result-per-measurement/260629-eti-SUMMARY.md`
</output>
