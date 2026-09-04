---
phase: quick-260904-iwm
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs
  - WPF_Example/Custom/SystemHandler.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionMasterParam.cs
autonomous: false
requirements: [QUICK-260904-IWM]

must_haves:
  truths:
    - "시퀀스마다 '기준점(Datum) 촬영 = 새 사이클 시작' 인 z_index 를 다르게 가질 수 있다 (Top=0, Bottom=11, Side2=11 …)"
    - "아무 설정도 하지 않으면 그 시퀀스가 소유한 Shot 의 ZIndex 최솟값이 자동으로 기준점이 된다"
    - "기존 레시피(Shot 이 0 부터 시작)는 자동값이 0 이라 동작이 지금과 100% 같다 (회귀 0)"
    - "사용자가 직접 넣은 값은 자동값보다 우선하며, Shot 최솟값과 다르면 경고가 보이되 저장은 막히지 않는다"
    - "기준점 값은 레시피 FIXTURE 섹션에 저장되고, 비활성 시퀀스(다른 CameraRole)의 값도 저장 시 보존된다"
    - "Debug|x64 빌드 error CS 0"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs"
      provides: "DatumZIndexOverride 필드 + GetDatumZIndex() 실효값 접근자 + 모든 판정 지점 치환"
    - path: "WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs"
      provides: "FIXTURE 섹션 DatumZIndex 키 저장/로드/보존 3경로"
    - path: "WPF_Example/Custom/SystemHandler.cs"
      provides: "StartV1Scoped 사이클 시작 판정 + GetPrepZIndex 폴백을 시퀀스 실효값으로 치환"
    - path: "WPF_Example/Custom/Sequence/Inspection/InspectionMasterParam.cs"
      provides: "PropertyGrid 기준점 입력 + 실효값/경고 표시(읽기 전용)"
  key_links:
    - from: "SystemHandler.StartV1Scoped"
      to: "InspectionSequence.GetDatumZIndex()"
      via: "seq as InspectionSequence 캐스트 후 비교 대상 값만 치환"
      pattern: "GetDatumZIndex\\(\\)"
    - from: "InspectionSequence.AddResponseV1Cycle / OnStart / ShouldSkipMeasurementAfterDatumPhase"
      to: "GetDatumZIndex()"
      via: "const DATUM_Z_INDEX 제거 후 전 판정 지점이 같은 접근자를 소비"
      pattern: "GetDatumZIndex\\(\\)"
    - from: "InspectionRecipeManager FIXTURE 섹션"
      to: "InspectionSequence.DatumZIndexOverride"
      via: "INI 키 DatumZIndex (키 부재 = -1 = 자동)"
      pattern: "\\[\"DatumZIndex\"\\]"
    - from: "InspectionMasterParam.DatumZIndexOverride"
      to: "InspectionSequence.DatumZIndexOverride"
      via: "DisplayName 과 동일한 프록시 프로퍼티 관용구"
      pattern: "_insp.DatumZIndexOverride"
---

<objective>
시퀀스별 "기준점(Datum) 촬영 = 새 사이클 시작" 인 z_index 를 설정값으로 만든다.

Purpose: 제어(PLC)가 40개 버퍼 번호를 시퀀스마다 구간으로 나눠 배정한다(Top=0~4, Bottom=11~40, Side1=0~3, Side2=11~13 …). 각 구간의 **시작 번호가 그 시퀀스의 기준점 촬영이고 새 사이클 시작**이다. 그런데 프로그램은 지금 "z_index 0" 하나만 기준점으로 취급하도록 상수로 박혀 있어서, Bottom 처럼 11 부터 시작하는 시퀀스는 사이클이 영원히 시작되지 않는다. 값 매칭(어떤 번호가 오면 같은 ZIndex 의 Shot 을 찍는다)은 이미 되고 있고, $PREP z 도 시퀀스별로 기억된다. 남은 걸림돌은 "기준점 = 0 고정" 하나뿐이다.
Output: 시퀀스에 기준점 인덱스 개념(사용자 지정값 + 자동 실효값) 추가 → 레시피 저장 → 판정 지점 전면 치환 → UI 입력/경고.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
</execution_context>

<context>
@CLAUDE.md

@WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
@WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs
@WPF_Example/Custom/SystemHandler.cs
@WPF_Example/Custom/Sequence/Inspection/InspectionMasterParam.cs
</context>

<analysis>

## 플래너 실측 확인 (전부 실제 파일에서 재검증함)

### "0 = 기준점" 이 박혀 있는 곳 — 전수 grep 결과가 이게 전부다

`grep -rn "DATUM_Z_INDEX\|DATUM_TEST_Z_INDEX" WPF_Example/ --include=*.cs` 및
`grep -rn "ZIndex == 0\|nCurZ == 0\|nZIndex == 0"` 로 확인. **아래 목록 밖에 숨은 0 비교는 없다.**

`WPF_Example/Custom/SystemHandler.cs`
- `:263` `private const int DATUM_TEST_Z_INDEX = 0;`
- `:302` `StartV1Scoped` — `nPrepZIndex == DATUM_TEST_Z_INDEX` → 사이클 시작 분기
- `:336` `inspDatumSeq.FindZeroIndexDatumTriggerActionIndices()` 호출
- `:44-60` `GetPrepZIndex(seqName)` — 기록 없음 → `0` 폴백 (2군데 return 0)

`WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`
- `:88` `private const int DATUM_Z_INDEX = 0;`
- `:453` `OnStart` — `GetExecutionZIndex() == DATUM_Z_INDEX` → `ClearDatumTransforms()` (새 사이클 캐시 비움)
- `:1561` `IsDatumOnlyExecutionIndex` 최상단 가드
- `:1663` `FindZeroIndexDatumTriggerActionIndices()` 정의
- `:1706` `ShouldSkipMeasurementAfterDatumPhase` 의 `bIsZeroIndex`
- `:1716` 같은 함수에서 `FindZeroIndex...()` 호출
- `:1806` `AddResponseV1Cycle` — `m_nCurrentZIndex == DATUM_Z_INDEX`
- `:1842` `HandleDatumIndexResponse` — `m_nCurrentZIndex = DATUM_Z_INDEX`
- `:1846` 같은 함수 `TryTurnOffLightsOnCycleEnd(..., "datum-index0", DATUM_Z_INDEX)` (nZIndex 는 **로그 전용**, `:1065-1080` 확인)
- `:1226-1227` `ResetCycleState` — `m_nCurrentZIndex = 0; m_nLastZIndex = 0;` (맨 0, 상수 아님)

### 근거를 남겨야 하는 판단 4건 (요구사항 3·6)

**(1) `ParseCurrentZIndex`(:1721) 의 0 정규화 — 바꾸지 않는다.**
이 0 은 "기준점" 이 아니라 **"요청 패킷이 없다(수동 RUN·일괄검사)"** 를 뜻한다. 근거:
- `:1386-1392` `IsProtocolDrivenCycle()` 주석이 명시 — "ParseCurrentZIndex 는 packet==null 일 때도 0 을 반환해(D-08 안전 폴백) 진짜 프로토콜 z=0 과 '프로토콜 자체가 없음'을 구별하지 못한다" → 그래서 별도 신호(IsProtocolDrivenCycle)를 둔 것이다.
- 만약 여기서 기준점 값(예 11)으로 정규화하면 `Action_FAIMeasurement.cs:1119-1121`, `:1719-1721` 의 크로스-Z 역할 판정(`nCurZ == datum.ZIndexA`)이 **수동 RUN 에서 갑자기 role A 캡처로 매칭**된다. 지금은 0 이라 절대 매칭되지 않고 저장본 재검출 경로로 빠진다. 이 사이트의 실제 생산 워크플로가 수동 지그 RUN 버튼이므로(메모리 `manual-jig-offline-inspect`) 변경 위험이 이득보다 크다.
- 따라서 값은 0 유지하되 **매직넘버만 이름 있는 const(`UNSET_CYCLE_Z_INDEX`)로 승격**하고, "이건 기준점이 아니라 '요청 없음' 이다" 를 주석으로 못박는다.

**(2) `IsDatumOnlyExecutionIndex`(:1561) 가드의 잔여 엣지 — 구조 변경 없이 주석으로 남긴다.**
기존 주석은 "수동 RUN 은 nZIndex 가 0 이라 이 가드가 이미 false 를 강제한다" 고 적혀 있다. 기준점이 11 이 되면 수동 RUN 의 0 은 이 가드를 통과한다. 그래도 안전한 이유: 그 다음 조건이 `IsZIndexUsedByCrossZDatum(0)` 인데, Bottom 처럼 11~40 구간을 쓰는 시퀀스의 크로스-Z Datum 이 ZIndexA/B=0 을 선언하는 일은 구간 배정상 없다 → false 로 빠진다.
`IsProtocolDrivenCycle()` 가드를 여기에 **추가하면 안 된다**: 이 함수는 `SystemHandler.StartV1Scoped:352` 에서도 호출되는데, 그 시점은 `StartSubset/StartAll` **이전**이라 `RequestPacket` 이 아직 이번 사이클 값으로 세팅되지 않았다(이전 사이클 잔값 또는 null) → 프로토콜 경로가 깨진다.

**(3) `bHasMeasurementShots = m_nLastZIndex > 0`(:1818) — 바꾸지 않는다.**
이 비교의 의미는 "기준점보다 큰가" 가 아니라 **"이 시퀀스가 소유한 Shot 이 하나라도 있는가"** 다(`ComputeLastZIndex` 는 소유 Shot 이 0 건이면 0 을 반환한다, `:636-660`). 기준점이 11 이어도 Bottom 은 최댓값 40 → `>0` 성립. 소유 Shot 0 건이면 여전히 0 → 기존 WR-01 가드(마지막 index 매칭 0건이면 F 강제)가 그대로 방어한다. 의미가 유지되므로 손대지 말 것. 다만 **주석에 "이 0 은 기준점이 아니라 '소유 Shot 없음' 센티널" 이라고 명시**한다.

**(4) 무변경 대상이 기준점≠0 에서도 계약을 유지하는 근거.**
- `ComputeLastZIndex`(:636) = 소유 Shot ZIndex **최댓값** + 크로스-Z 완성 index. 기준점(최솟값)과 독립 개념이라 영향 없음. **크로스-Z 확장을 최솟값 쪽에 대칭 적용하지 말 것** — 기준점은 "PLC 구간의 시작 번호" 라는 운영 정의이지 크로스-Z 완성 index 와 무관하다.
- `FindShotByZIndex` / `FindActionIndicesByZIndex`(:838) = 순수 값 매칭. 번호를 그대로 받아 같은 ZIndex Shot 을 실행하므로 기준점이 무엇이든 무관.
- `StartEmptyScope` 경로(SystemHandler `:355-372`) = "매칭 0건" 처리. 기준점 분기에 들어가지 않은 번호만 도달하므로 무관.
- 크로스-Z(ZIndexA/B) = 값 매칭. 무관.
- **"z==기준점 에서만 캐시 Clear" 치환의 정확성**: `:453` 의 분기는 `!IsProtocolDrivenCycle()` 의 else-if 다 → 프로토콜 사이클에서만 도달한다. 프로토콜에서 `GetExecutionZIndex()` 는 `$PREP` 가 넣어준 실제 버퍼 번호를 돌려주므로, 그 값이 그 시퀀스의 기준점과 같을 때가 곧 "구간 시작 = 새 부품" 이다. 나머지 번호는 같은 사이클의 연속 tick 이라 Clear 하면 안 된다 — **치환 전후로 이 불변식이 그대로 성립한다.** 기준점 0 인 시퀀스는 비교 대상 값이 그대로 0 이라 회귀도 0.

**(5) `FindZeroIndexDatumTriggerActionIndices` 이름 — 정정한다.**
"Zero" 가 의미상 어긋난다(z=0 전용이 아니라 "기준점 index 의 대표 트리거"). 호출부가 단 2곳(`SystemHandler.cs:336`, `InspectionSequence.cs:1716`)뿐이라 안전하다 → `FindDatumIndexTriggerActionIndices` 로 rename 하고 두 호출부 + 관련 주석(`SystemHandler.cs:258`, `InspectionSequence.cs:1657-1662`, `:1690-1698`)의 "z=0" 표현도 "기준점 index" 로 정정한다.

### 저장 경로 (요구사항 2) — 실측 확인

`InspectionRecipeManager.cs`
- `:181-190` TOP 인라인 저장 블록 (`saveFile["FIXTURE"]["DisplayName"]`, `["DatumCount"]`)
- `:85-101` `SaveFixtureForSequence` — SIDE_1~4(`:193-196`), BOTTOM(`:209`) 공용
- `:106-122` `PreserveFixtureFromExisting` — **섹션 통째 대입**(`saveFile[prefix] = existingFile[prefix]`)이라 신규 키도 자동 보존됨. 손댈 곳은 "보존할 기존 데이터 없음" 빈 분기(`:108-113`) 하나뿐이다.
- `:126-148` `LoadFixtureForSequence`, `:271-285` TOP 인라인 로드 블록
- INI 미존재 키는 `IniValue.Default` 를 돌려주고(`Ini.cs:953-960`) `.ToInt(v)` 는 변환 실패 시 인자값을 반환한다(`Ini.cs:179-185`) → **`ToInt(NO_DATUM_Z_INDEX_OVERRIDE)` 한 줄로 "키 부재 = 자동" 이 성립한다.** `ContainsKey` 가드는 불필요.

### UI 경로 (요구사항 4) — 기존 관례 그대로

- 트리 Sequence 노드의 PropertyGrid 소스는 `seq.Param` = `InspectionMasterParam` 이다 (`InspectionListViewModel.cs:109`, `InspectionListView.xaml:303-310` 이 `SelectedItem.Param` 에 바인딩).
- `InspectionMasterParam.DisplayName`(`:18-31`) 이 **이미 정확히 같은 패턴**이다 — 실체는 `InspectionSequence.DisplayName`, 저장은 FIXTURE 섹션, PropertyGrid 는 프록시 프로퍼티로 편집. 이걸 그대로 복제하면 code-behind 0 줄, XAML 0 줄 수정으로 끝난다(= 최소 침습 + MVVM 요구 자동 충족).
- 시퀀스 `Param` 은 **어떤 파일로도 직렬화되지 않는다**(`Param.Save`/`Load` 호출부 grep 0건) → FIXTURE 섹션이 단일 저장원이고 이중 저장 위험 없음.
- `ParamBase` 는 `Load`(`:373`)/`CopyTo`(`:449`) 양쪽에서 `!prop.CanWrite → continue` 가드가 있으므로 **getter 전용 계산 프로퍼티를 추가해도 복사/붙여넣기가 깨지지 않는다.**
- 읽기 전용 표시 관례는 `DatumConfig.cs:1018-1035` — `[System.ComponentModel.ReadOnly(true)]` + `[PropertyTools.DataAnnotations.ReadOnly(true)]` **둘 다** 부착.

</analysis>

<tasks>

<task type="auto">
  <name>Task 1: 기준점 인덱스 개념 + 레시피 저장</name>
  <files>WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs, WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs</files>
  <action>
**이 태스크에서는 판정 지점을 아직 건드리지 않는다.** `DATUM_Z_INDEX`(:88) 는 그대로 두고 API 와 저장만 추가한다 — 태스크 경계에서 항상 빌드가 통과해야 한다.

### 1-A. `InspectionSequence.cs` — 상수 3개

`:86-88` 의 기존 상수 블록(`DATUM_Z_INDEX` / `CROSS_Z_UNSET`) 옆에 추가한다.

- `public const int NO_DATUM_Z_INDEX_OVERRIDE = -1;` — "사용자 미지정 = 자동". public 인 이유는 `InspectionRecipeManager` 와 `InspectionMasterParam` 이 같은 센티널을 공유해야 하기 때문(값 복제 금지).
- `private const int MIN_VALID_Z_INDEX = 0;` — 유효한 가장 작은 z. 지정값 유효성 판정과 "소유 Shot 없음" 폴백에 둘 다 쓴다.
- `private const int UNSET_CYCLE_Z_INDEX = 0;` — "이번 tick 의 요청 자체가 없음". **기준점과 다른 개념이라는 것을 주석으로 못박을 것**(analysis 판단 (1)).

### 1-B. `InspectionSequence.cs` — 필드

`:45-46` `DisplayName` 바로 아래, 같은 관용구로:

```
public int DatumZIndexOverride { get; set; } = NO_DATUM_Z_INDEX_OVERRIDE;
```
주석: 사용자가 직접 넣은 기준점 z_index. 음수 = 미지정(자동). 실효값은 반드시 `GetDatumZIndex()` 로만 읽을 것.

### 1-C. `InspectionSequence.cs` — 공개 API 3개

`ComputeLastZIndex`(:636) 바로 위/아래에 붙여 "z_index 산출" 코드를 한곳에 모은다. 브레이스 스타일은 그 주변(Allman)을 따른다.

1. `public bool TryGetOwnedShotZIndexRange(out int nMin, out int nMax)`
   - `SystemHandler.Handle.Sequences.RecipeManager` 를 읽고, `shot.OwnerSequenceName == Name` 인 Shot 만 순회 — **`ComputeLastZIndex` 와 완전히 같은 소유 판정식을 쓸 것**(레거시 빈 OwnerSequenceName 처리 일관성).
   - 소유 Shot 0건 또는 recipeManager null → `nMin=0, nMax=0, return false`.
   - 크로스-Z 완성 index(`MaxCrossZCompletionZIndex`)는 **여기에 절대 섞지 말 것**(analysis (4)).

2. `public int GetDatumZIndex()`
   - `DatumZIndexOverride >= MIN_VALID_Z_INDEX` 면 그 값을 반환(0 도 정당한 지정값이다).
   - 아니면 `TryGetOwnedShotZIndexRange` 의 `nMin`.
   - Shot 이 없으면 `MIN_VALID_Z_INDEX`.
   - 캐시하지 말 것 — 레시피 교체/Shot 편집 후 스테일 값이 사이클 시작을 망가뜨리는 위험이 캐시 이득보다 크다. `ComputeLastZIndex` 가 이미 매 응답마다 같은 순회를 하고 있으므로 비용 근거도 동일하다. 이 근거를 주석으로 남긴다.

3. `public bool TryGetDatumZIndexWarning(out string szWarning)`
   - 미지정(자동)이면 `false`(경고 없음).
   - 소유 Shot 이 없으면 `false`(경고 없음).
   - 지정값 == `nMin` 이면 `false`.
   - 그 외 `true` + 문구: `"기준점 z=7 이 이 시퀀스 Shot 범위(4~7)의 시작 번호(4)가 아닙니다 — PLC 구간 시작 번호와 일치하는지 확인하세요."` (숫자는 실제값으로 포맷)
   - **판정만 한다. 여기서 로그/메시지박스를 띄우지 말 것**(표시 빈도를 호출부가 정한다).

### 1-D. `InspectionRecipeManager.cs` — FIXTURE 키 `DatumZIndex` 5곳

저장 3경로 + 로드 2경로. 키 이름은 정확히 `"DatumZIndex"`.

- `:181-190` TOP 저장: `DatumCount` 줄 다음에 `saveFile["FIXTURE"]["DatumZIndex"] = fixtureSeq.DatumZIndexOverride;`
- `:85-101` `SaveFixtureForSequence`: 같은 위치에 `saveFile[sectionPrefix]["DatumZIndex"] = seq.DatumZIndexOverride;`
- `:106-122` `PreserveFixtureFromExisting`: **빈 분기(`:108-113`)에만** `saveFile[sectionPrefix]["DatumZIndex"] = InspectionSequence.NO_DATUM_Z_INDEX_OVERRIDE;` 추가. 그 아래 섹션 통째 대입 경로는 신규 키를 자동 보존하므로 **손대지 말 것**.
- `:271-285` TOP 로드: `if (fixtureSeq != null)` 를 기존 `if (fixtureSeq != null && loadFile.ContainsSection("FIXTURE"))` **앞에** 별도로 하나 두어 `fixtureSeq.DatumZIndexOverride = InspectionSequence.NO_DATUM_Z_INDEX_OVERRIDE;` 로 먼저 초기화하고, 기존 if 블록 안에서 `fixtureSeq.DatumZIndexOverride = loadFile["FIXTURE"]["DatumZIndex"].ToInt(InspectionSequence.NO_DATUM_Z_INDEX_OVERRIDE);` 로 덮는다.
  이유: 섹션이 없는 레시피로 교체했을 때 이전 레시피의 기준점이 살아남으면 엉뚱한 사이클 시작이 된다.
- `:126-148` `LoadFixtureForSequence`: `seq.DatumConfigs.Clear();` 바로 다음(= `ContainsSection` 조기 return **앞**)에 `seq.DatumZIndexOverride = InspectionSequence.NO_DATUM_Z_INDEX_OVERRIDE;` 를 넣고, 섹션이 있을 때 `.ToInt(...)` 로 덮는다.

`ContainsKey` 가드는 넣지 말 것 — `ToInt(기본값)` 이 이미 "키 부재 = 자동" 을 보장한다(analysis 참조).
  </action>
  <verify>
    <automated><![CDATA[
set -e
cd /c/code/DataMeasurement

# 1) API/키가 실제로 들어갔는지
grep -q 'public const int NO_DATUM_Z_INDEX_OVERRIDE' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
grep -q 'public int DatumZIndexOverride' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
grep -q 'public int GetDatumZIndex()' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
grep -q 'public bool TryGetOwnedShotZIndexRange' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
grep -q 'public bool TryGetDatumZIndexWarning' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
# 저장 3 + 로드 2 = DatumZIndex 키 참조 5회 이상
test "$(grep -c '"DatumZIndex"' WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs)" -ge 5

# 2) 하드룰 — 추가 라인만 검사 (기존 라인의 hbk 주석은 대상 아님)
git diff -U0 -- WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs \
                WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs \
  | grep '^+' | grep -v '^+++' > /tmp/iwm_t1.txt
test "$(grep -cF 'hbk' /tmp/iwm_t1.txt)" = "0"
test "$(grep -cF '??' /tmp/iwm_t1.txt)" = "0"
test "$(grep -cF '?.' /tmp/iwm_t1.txt)" = "0"
test "$(grep -cE 'switch.*=>' /tmp/iwm_t1.txt)" = "0"
test "$(grep -cE '\?[^?]*:' /tmp/iwm_t1.txt)" = "0"

# 3) 빌드 — error CS 0 (MSB3027 bin 복사 실패는 게이트 아님)
"/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" \
  WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo \
  2>&1 | tee /tmp/iwm_build1.log
test "$(grep -cE 'error CS' /tmp/iwm_build1.log)" = "0"
echo T1_OK
]]></automated>
    <manual>삼항 게이트가 히트하면 코드가 아니라 한글 주석의 `?` + `:` 조합일 수 있다. 육안 확인 후 **주석 문구를 고쳐 0 을 만든다** — 게이트를 완화하거나 우회하지 말 것.</manual>
  </verify>
  <done>`GetDatumZIndex()` 가 존재하고, 지정값이 없으면 소유 Shot ZIndex 최솟값을(Shot 이 없으면 0을) 반환한다. FIXTURE 섹션 5경로에 `DatumZIndex` 키가 배선됐고 키 부재 시 -1(자동)로 로드된다. 판정 지점은 아직 변경 전이며 빌드 error CS 0.</done>
</task>

<task type="auto">
  <name>Task 2: 판정 지점 전면 치환 (상수 → 시퀀스 실효값)</name>
  <files>WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs, WPF_Example/Custom/SystemHandler.cs</files>
  <action>
**원칙: 로직 구조는 그대로 두고 비교 대상 값만 바꾼다.** 특히 `StartV1Scoped` 는 과거 TOCTOU 경합을 잡은 예민한 지점이다 — `BeginCrossZImageCycle()` 호출 위치, `StartSubset`/`StartAll` 순서, `State==Idle` 사전체크 부재를 **절대 바꾸지 말 것**(`SystemHandler.cs:304-334` 의 긴 주석이 그 이유다).

### 2-A. `InspectionSequence.cs`

1. `:88` `private const int DATUM_Z_INDEX = 0;` **삭제.** 이후 참조는 전부 `GetDatumZIndex()`.
2. `:453` `else if (GetExecutionZIndex() == DATUM_Z_INDEX)` → `GetDatumZIndex()` 와 비교. 조건은 이름 있는 bool 로 선추출할 것.
   - `:461-467` 주석의 "z==DATUM_Z_INDEX(0)", "z>=1" 표현을 "z == 이 시퀀스의 기준점", "그 외 번호" 로 정정한다(analysis (4) 의 불변식 근거를 한 줄 포함).
3. `:1561` `IsDatumOnlyExecutionIndex` 최상단 가드 → `nZIndex == GetDatumZIndex()`.
   - `:1556-1559` 주석에 analysis 판단 (2) 를 요약해 남긴다: 수동 RUN(요청 없음)의 0 이 기준점≠0 시퀀스에서 이 가드를 통과하지만 `IsZIndexUsedByCrossZDatum(0)` 이 false 라 안전하며, **여기에 `IsProtocolDrivenCycle()` 가드를 추가하면 `StartV1Scoped` 호출 시점에 RequestPacket 이 아직 이번 사이클 값이 아니어서 프로토콜 경로가 깨진다.**
4. `:1663` `FindZeroIndexDatumTriggerActionIndices` → **`FindDatumIndexTriggerActionIndices` 로 rename** (선언 + 호출부 `:1716` + `SystemHandler.cs:336`). `:1657-1662` 주석의 "z=0" 표현도 "기준점 index" 로 정정.
5. `:1706` `bool bIsZeroIndex = nZIndex == DATUM_Z_INDEX;` → `bool bIsDatumIndex = nZIndex == GetDatumZIndex();` (변수명도 정정). `:1690-1698` 주석의 z=0 표현 정정.
6. `:1806` `m_nCurrentZIndex == DATUM_Z_INDEX` → `GetDatumZIndex()`.
7. `:1818` `bHasMeasurementShots = m_nLastZIndex > 0` — **값·비교 모두 변경 금지.** 주석 한 줄만 추가: 이 0 은 기준점이 아니라 "이 시퀀스 소유 Shot 이 0건" 센티널이다(analysis (3)).
8. `:1842` `m_nCurrentZIndex = DATUM_Z_INDEX;` → `GetDatumZIndex()`. 같은 값을 두 번 계산하지 않도록 `HandleDatumIndexResponse` 진입부에서 지역변수 `int nDatumZ = GetDatumZIndex();` 로 한 번만 받아 쓴다.
9. `:1846` `TryTurnOffLightsOnCycleEnd(datumPacket, "datum-index0", DATUM_Z_INDEX)` → 태그를 `"datum-index"` 로, 값은 `nDatumZ`. (이 인자는 로그 전용임을 이미 확인함 — 동작 영향 없음)
10. `:1226-1227` `ResetCycleState` 의 맨 `0` 두 개 → `UNSET_CYCLE_Z_INDEX`. **값은 그대로 0 이다**(다음 tick 에 즉시 덮어써지는 중립값이며, `$RESET` 경로에서도 기준점으로 세팅할 이유가 없다). 이 근거를 주석으로 남긴다.
11. `:1721` `ParseCurrentZIndex` — 반환 `0` 3곳을 `UNSET_CYCLE_Z_INDEX` 로 승격. **동작 변경 금지**(analysis (1)). 주석에 "이 0 은 기준점이 아니라 '요청 패킷 없음' 이며, 진짜 프로토콜/수동 구분은 `IsProtocolDrivenCycle()` 이 담당한다" 를 명시.

### 2-B. `WPF_Example/Custom/SystemHandler.cs`

1. `:263` `DATUM_TEST_Z_INDEX` → `private const int NO_SEQUENCE_DATUM_Z_INDEX = 0;` 으로 **의미 변경 + 개명**. 뜻: "시퀀스를 해석할 수 없을 때의 폴백 기준점". `:255-262` 주석도 "z=0" → "그 시퀀스의 기준점 index" 로 정정.
2. 신규 private 헬퍼:
   ```
   private int ResolveDatumZIndex(string szSeqName)
   ```
   `Sequences[szSeqName]`(문자열 인덱서는 미존재 시 null 반환 — `SequenceHandler.cs:138-150` 확인함) → `as InspectionSequence` → null 이면 `NO_SEQUENCE_DATUM_Z_INDEX`, 아니면 `insp.GetDatumZIndex()`.
3. `:44-60` `GetPrepZIndex`:
   - 이름 없음 분기의 `return 0` → `return NO_SEQUENCE_DATUM_Z_INDEX;`
   - 기록 없음 폴백 → `ResolveDatumZIndex(szSeqName)` 반환. **`ResolveDatumZIndex` 는 `_prepZIndexLock` 밖에서 호출할 것**(락 안에서 시퀀스/레시피를 만지지 말 것 — 기존 락 범위 원칙).
   - 로그 문구를 "z_index=0 으로 폴백" → "그 시퀀스의 기준점 z_index={실효값} 으로 폴백" 으로 정정하고 실제 값을 찍는다.
   - `:43` 의 헤더 주석("0(=Datum 인덱스) 보수적 폴백")도 정정.
4. `:296-302` `StartV1Scoped`:
   - `InspectionSequence inspSeq = seq as InspectionSequence;` 캐스트를 함수 최상단으로 **hoist** 하고, `int nDatumZ;` 를 `inspSeq == null` 이면 `NO_SEQUENCE_DATUM_Z_INDEX`, 아니면 `inspSeq.GetDatumZIndex()` 로 구한다.
   - `bool bIsDatumZIndex = nPrepZIndex == nDatumZ;`
   - 기준점 분기 안의 지역변수 `inspDatumSeq` 는 hoist 한 `inspSeq` 로 대체한다. **`if (!bIsInspSeq) return seq.StartAll(packet);` 방어 폴백 2곳은 그대로 유지**(캐스트 실패 시 `nDatumZ==0` 이라 종전과 정확히 같은 동작이 된다).
   - `datumZeroIndices` 지역변수명 → `datumTriggerIndices`, 호출은 rename 된 `FindDatumIndexTriggerActionIndices()`.
   - `:255-262`, `:288-295`, `:304-334`, `:337-341` 주석의 "z=0" / "z>=1" 표현을 "기준점 index" / "그 외 번호" 로 정정하되 **TOCTOU·FIX-0·GAP-2·GAP-3 설명 본문은 지우지 말 것**(왜 이 구조인지의 유일한 기록이다).
  </action>
  <verify>
    <automated><![CDATA[
set -e
cd /c/code/DataMeasurement

# 1) 옛 상수/옛 이름이 완전히 사라졌는지 (주석 본문 포함). \b 경계라 NO_DATUM_Z_INDEX_OVERRIDE 는 매치되지 않는다.
test "$(grep -cE '\bDATUM_Z_INDEX\b' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs)" = "0"
test "$(grep -rnE '\bDATUM_TEST_Z_INDEX\b|FindZeroIndexDatumTriggerActionIndices' WPF_Example --include=*.cs | wc -l)" = "0"

# 2) 새 접근자가 판정 지점에서 실제로 소비되는지 (InspectionSequence 4곳 이상 + SystemHandler)
test "$(grep -c 'GetDatumZIndex()' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs)" -ge 5
grep -q 'GetDatumZIndex()' WPF_Example/Custom/SystemHandler.cs
grep -q 'FindDatumIndexTriggerActionIndices' WPF_Example/Custom/SystemHandler.cs
grep -q 'private int ResolveDatumZIndex' WPF_Example/Custom/SystemHandler.cs

# 3) 무변경 계약 — 이 3줄은 그대로 남아 있어야 한다
grep -q 'm_nLastZIndex > 0' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
grep -q 'inspDatumSeq\|inspSeq' WPF_Example/Custom/SystemHandler.cs
grep -q 'BeginCrossZImageCycle();' WPF_Example/Custom/SystemHandler.cs

# 4) 하드룰 — 추가 라인만
git diff -U0 -- WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs \
                WPF_Example/Custom/SystemHandler.cs \
  | grep '^+' | grep -v '^+++' > /tmp/iwm_t2.txt
test "$(grep -cF 'hbk' /tmp/iwm_t2.txt)" = "0"
test "$(grep -cF '??' /tmp/iwm_t2.txt)" = "0"
test "$(grep -cF '?.' /tmp/iwm_t2.txt)" = "0"
test "$(grep -cE 'switch.*=>' /tmp/iwm_t2.txt)" = "0"
test "$(grep -cE '\?[^?]*:' /tmp/iwm_t2.txt)" = "0"

# 5) 빌드
"/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" \
  WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo \
  2>&1 | tee /tmp/iwm_build2.log
test "$(grep -cE 'error CS' /tmp/iwm_build2.log)" = "0"
echo T2_OK
]]></automated>
  </verify>
  <done>`DATUM_Z_INDEX` / `DATUM_TEST_Z_INDEX` 상수가 사라지고 모든 기준점 판정이 `GetDatumZIndex()` 를 소비한다. `GetPrepZIndex` 폴백이 시퀀스 실효값을 돌려준다. `m_nLastZIndex > 0`, `ParseCurrentZIndex` 반환값(0), `StartV1Scoped` 의 `BeginCrossZImageCycle`/`StartSubset` 순서는 그대로다. 빌드 error CS 0.</done>
</task>

<task type="auto">
  <name>Task 3: 트리 시퀀스 노드에 기준점 입력 + 경고 표시</name>
  <files>WPF_Example/Custom/Sequence/Inspection/InspectionMasterParam.cs</files>
  <action>
**이 파일 1개만 수정한다.** `InspectionListView.xaml`, `InspectionListView.xaml.cs`, `InspectionListViewModel.cs` 는 **손대지 말 것** — 트리 Sequence 노드는 이미 `seq.Param`(= 이 클래스)을 PropertyGrid 에 바인딩하고 있으므로 프로퍼티만 추가하면 UI 가 완성된다(= code-behind 0 줄, MVVM 요구 충족). 브레이스 스타일은 이 파일의 K&R 을 따른다.

기존 `DisplayName`(:18-31) 프록시 관용구를 그대로 복제한다. `_insp` null 가드 필수(base 생성자 시점에는 아직 null 이다).

1. 편집 프로퍼티
   ```
   [Category("Fixture|Datum")]
   public int DatumZIndexOverride { get; set; }
   ```
   - getter: `_insp` null 이면 `InspectionSequence.NO_DATUM_Z_INDEX_OVERRIDE`, 아니면 `_insp.DatumZIndexOverride`.
   - setter: 같은 값이면 return. 음수는 전부 `NO_DATUM_Z_INDEX_OVERRIDE` 로 정규화해서 대입(사용자가 -5 를 넣어도 "자동" 이 되게).
   - setter 끝에서 `RaisePropertyChanged("DatumZIndexOverride")` **와** `RaisePropertyChanged("DatumZIndexInfo")` 를 둘 다 발화.
   - setter 안에서 `_insp.TryGetDatumZIndexWarning(out szWarning)` 이 true 면 `Logging.PrintLog((int)ELogType.Error, ...)` 로 1회 기록한다. **메시지박스를 띄우지 말 것** — 이 클래스는 Sequence 네임스페이스라 UI 를 소유하지 않으며, 저장을 막지 않는다는 요구와도 맞다.
   - 주석에 "-1 = 자동(소유 Shot ZIndex 최솟값)" 을 남긴다.

2. 표시 전용 프로퍼티 (getter 만)
   ```
   [Category("Fixture|Datum")]
   [System.ComponentModel.ReadOnly(true)]
   [PropertyTools.DataAnnotations.ReadOnly(true)]
   public string DatumZIndexInfo { get { ... } }
   ```
   문자열 4형태(모두 `if/else`, 삼항 금지):
   - `_insp` null → `""`
   - 자동 + Shot 있음 → `"자동 — 현재 {실효값} (Shot 범위 {min}~{max})"`
   - 자동 + Shot 없음 → `"자동 — 이 시퀀스 소유 Shot 없음, 0 사용"`
   - 지정 + 경고 있음 → `"⚠ " + 경고문구` (`TryGetDatumZIndexWarning` 결과 그대로)
   - 지정 + 경고 없음 → `"지정 {값} (Shot 범위 {min}~{max})"`

   setter 를 만들지 말 것. `ParamBase.Load`/`CopyTo` 는 `CanWrite` 가드가 있어 안전함을 확인했다.

**알려진 한계 — 이걸로 시간 쓰지 말 것:** PropertyTools 가 읽기 전용 항목을 즉시 갱신하지 않아 값을 바꾼 직후 안내문이 그대로일 수 있다. 그 경우 다른 노드를 선택했다 돌아오면 갱신된다. **이 한계를 우회하려고 code-behind 나 XAML 을 건드리지 말 것** — 경고는 로그에도 남으므로 요구는 충족된다.
  </action>
  <verify>
    <automated><![CDATA[
set -e
cd /c/code/DataMeasurement

grep -q 'public int DatumZIndexOverride' WPF_Example/Custom/Sequence/Inspection/InspectionMasterParam.cs
grep -q 'DatumZIndexInfo' WPF_Example/Custom/Sequence/Inspection/InspectionMasterParam.cs
grep -q 'PropertyTools.DataAnnotations.ReadOnly(true)' WPF_Example/Custom/Sequence/Inspection/InspectionMasterParam.cs
# 최소 침습 — UI 3파일은 변경 0
test "$(git diff --name-only -- WPF_Example/UI | wc -l)" = "0"

git diff -U0 -- WPF_Example/Custom/Sequence/Inspection/InspectionMasterParam.cs \
  | grep '^+' | grep -v '^+++' > /tmp/iwm_t3.txt
test "$(grep -cF 'hbk' /tmp/iwm_t3.txt)" = "0"
test "$(grep -cF '??' /tmp/iwm_t3.txt)" = "0"
test "$(grep -cF '?.' /tmp/iwm_t3.txt)" = "0"
test "$(grep -cE 'switch.*=>' /tmp/iwm_t3.txt)" = "0"
test "$(grep -cE '\?[^?]*:' /tmp/iwm_t3.txt)" = "0"

# 범위 — 변경 파일 4개 고정, 신규 소스파일 0, csproj 미변경
git diff --name-only -- WPF_Example | sort > /tmp/iwm_files.txt
test "$(wc -l < /tmp/iwm_files.txt)" = "4"
test "$(grep -c 'DatumMeasurement.csproj' /tmp/iwm_files.txt)" = "0"
test "$(git status --short | grep -cE '^\?\? .*\.(cs|xaml)$')" = "0"

"/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" \
  WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo \
  2>&1 | tee /tmp/iwm_build3.log
test "$(grep -cE 'error CS' /tmp/iwm_build3.log)" = "0"
echo T3_OK
]]></automated>
  </verify>
  <done>트리에서 시퀀스 노드를 선택하면 PropertyGrid `Fixture|Datum` 그룹에 기준점 입력칸과 안내문(자동/지정/경고)이 보인다. UI 3파일과 csproj 는 변경되지 않았고 빌드 error CS 0.</done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 4: 실기 확인 (기존 레시피 회귀 + Bottom 11 사이클 시작 + 경고)</name>
  <action>아래 <how-to-verify> 절차를 사용자에게 그대로 제시하고 응답을 기다린다. 코드 수정 없음.</action>
  <what-built>
    시퀀스마다 "기준점(= 그 시퀀스 구간의 시작 번호, 여기서 새 사이클이 시작됨)" 을 따로 정할 수 있게 했다.
    아무것도 설정하지 않으면 **그 시퀀스가 가진 Shot 번호 중 가장 작은 번호**가 자동으로 기준점이 된다.
    그래서 Bottom 이 11~40 을 쓰면 11 이 자동으로 사이클 시작이 되고, 기존 레시피(0 부터 시작)는 예전과 완전히 똑같이 동작한다.
    직접 숫자를 넣으면 그 값이 우선하고, Shot 의 가장 작은 번호와 다르면 안내문에 경고가 뜨지만 저장은 막지 않는다.
  </what-built>
  <how-to-verify>
    `bin/x64/Debug/DatumMeasurement.exe` 실행.

    **1) 기존 레시피 회귀 (가장 중요)**
    - 지금 쓰던 레시피를 그대로 열고 평소처럼 검사를 한 번 돌린다(PLC 또는 수동 지그 RUN 버튼).
    - 결과·응답이 **어제와 똑같은지** 확인한다. 뭔가 달라졌으면 즉시 실패로 보고할 것.

    **2) 자동값 확인**
    - 좌측 트리에서 시퀀스 이름(예 `BOTTOM`)을 클릭한다.
    - 우측 속성창 `Fixture|Datum` 그룹에 기준점 칸과 안내문이 보이는가?
    - 안내문이 `자동 — 현재 N (Shot 범위 N~M)` 형태이고, **N 이 그 시퀀스 Shot 중 가장 작은 번호와 같은가?**

    **3) Bottom 11 로 실기 확인** (Bottom Shot 이 11~40 인 레시피 필요)
    - 제어 쪽에서 `$PREP:1,1,11@` 다음에 `$TEST` 를 보낸다.
    - 이때 **새 사이클로 시작되는지**(기준점 촬영 응답 `B`, 이전 부품 결과가 초기화됨) 확인한다.
    - 이어서 12, 13 … 을 보내고 마지막 번호(40)에서 종합 판정(P 또는 F)이 나오는지 확인한다.

    **4) 경고 확인**
    - 기준점 칸에 일부러 엉뚱한 값(예 Shot 이 11~40 인데 `40`)을 넣는다.
    - 안내문에 `⚠ ... 시작 번호(11)가 아닙니다` 가 뜨는가? (바로 안 바뀌면 다른 노드를 클릭했다 돌아와 볼 것)
    - **저장이 막히지 않는지** 확인한다. 확인 후 값을 지워 자동(-1)으로 되돌린다.

    **5) 저장/불러오기**
    - 기준점을 정한 뒤 레시피 저장 → 프로그램 재시작 → 같은 레시피 열기 → 값이 그대로 남아 있는가?
    - 현재 모드에 없는 시퀀스(예 Side 모드에서 Bottom)의 기준점 값이 저장 후에도 파일에 남아 있는가?
      (`main.ini` 의 `[FIXTURE_BOTTOM]` 에 `DatumZIndex` 줄이 살아 있는지 메모장으로 확인)
  </how-to-verify>
  <resume-signal>"approved" 또는 어긋난 항목(항목 번호 + 시퀀스 이름 + 보낸 번호 + 실제로 나온 응답)을 알려주세요</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| PLC/호스트 → TCP `$PREP`/`$TEST` z_index | 외부에서 들어온 정수 버퍼 번호가 "사이클 시작" 판정에 쓰인다 |
| 운영자 → PropertyGrid 기준점 입력 | 사람이 넣은 값이 사이클 시작 판정을 바꾼다 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-IWM-01 | Denial of Service | `StartV1Scoped` 기준점 분기 | mitigate | 기준점을 잘못 지정하면 사이클이 시작되지 않는다 → 기본값을 "자동(Shot 최솟값)" 으로 두어 오설정 자체가 발생하지 않게 하고, 수동 지정이 Shot 최솟값과 다르면 `TryGetDatumZIndexWarning` 경고 + 로그 |
| T-IWM-02 | Repudiation | `GetPrepZIndex` 폴백 | mitigate | `$PREP` 누락 폴백 로그에 실제 사용한 기준점 값을 함께 찍어 사후 추적 가능하게 한다 |
| T-IWM-03 | Tampering | 레시피 INI `DatumZIndex` 키 | accept | 로컬 파일이며 기존 FIXTURE 키(DisplayName/DatumCount)와 동일한 신뢰 수준. 음수는 setter/실효값 계산에서 "자동" 으로 정규화되어 잘못된 값이 판정에 흘러들지 않는다 |
| T-IWM-SC | Tampering | 패키지 설치 | n/a | 신규 패키지 설치 없음(npm/pip/cargo 미사용, 신규 파일 0개) |
</threat_model>

<verification>
1. Debug|x64 빌드 **error CS 0** (`MSB3027` bin 복사 실패는 게이트 아님, 실행 중인 프로세스를 강제 종료하지 말 것).
2. 하드룰 grep — **추가된 diff 라인 기준** 전항목 0: 삼항 / `??` / `?.` / `switch.*=>` / `hbk`.
3. 옛 상수 소멸: `DATUM_Z_INDEX`, `DATUM_TEST_Z_INDEX`, `FindZeroIndexDatumTriggerActionIndices` 전 저장소 grep 0건.
4. 무변경 계약 유지: `m_nLastZIndex > 0`, `ParseCurrentZIndex` 반환 0, `BeginCrossZImageCycle()` 호출 위치, `StartSubset/StartAll` 순서, `ComputeLastZIndex` 최댓값 로직.
5. 회귀 0 코드 경로 확인: 지정값 없음 + Shot 이 0 부터 시작 → `GetDatumZIndex()==0` → 모든 판정 지점이 종전과 동일한 값을 비교한다.
6. 변경 파일 정확히 4개, 신규 소스파일 0개, `DatumMeasurement.csproj` 미변경/미스테이징 (이 파일에는 이 PC 전용 로컬 설정이 들어 있을 수 있다).
7. 스테이징은 수정 파일만 명시적으로 — `git add .` / `git add -A` **금지**.
</verification>

<success_criteria>
- 기준점 인덱스가 시퀀스별 설정값이 되고, 실효값 접근자(`GetDatumZIndex()`)를 **모든** 판정 지점이 소비한다.
- 미지정 시 자동으로 소유 Shot ZIndex 최솟값이 쓰이고, Shot 이 없으면 0 이다.
- 기존 레시피는 자동값이 0 이라 동작이 종전과 100% 동일하다.
- 기준점 값이 레시피 FIXTURE 섹션에 저장/로드되고, 비활성 시퀀스의 값도 저장 시 보존된다.
- 트리 시퀀스 노드에서 값을 편집할 수 있고 실효값과 경고가 보이며, 경고가 저장을 막지 않는다.
- 빌드 error CS 0, 하드룰 grep 전항목 0.
</success_criteria>

<output>
완료 후 `.planning/quick/260904-iwm-datum-z-0/260904-iwm-SUMMARY.md` 를 작성한다.
</output>
