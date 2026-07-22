---
phase: 260722-vks
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/Measurements/CircleCenterDistanceMeasurement.cs
  - WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs
  - WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundAngleMeasurement.cs
  - WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterBDistanceMeasurement.cs
  - WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterCDistanceMeasurement.cs
autonomous: true
requirements:
  - FIX-A-CCD-EdgePolarity      # CircleCenterDistance: dead EdgePolarity 숨김
  - FIX-B-CDD-EdgePolarity      # CircleDiameter: polar 경로에서 inert EdgePolarity 숨김
  - FIX-C1-CA-DeadEdge          # CompoundAngle: 미사용 6개 Edge 프로퍼티 숨김
  - FIX-C2-CA-DatumOverlay      # CompoundAngle: 누락된 DatumB 오버레이 추가
  - FIX-D-CCB-DeadEdge          # CompoundCenterBDistance: 미사용 6개 Edge 프로퍼티 숨김
  - FIX-E-CCC-DeadEdge          # CompoundCenterCDistance: 미사용 6개 Edge 프로퍼티 숨김

must_haves:
  truths:
    - "CircleCenterDistanceMeasurement 의 EdgePolarity 는 PropertyGrid 에 더 이상 편집 가능한 ComboBox 로 표시되지 않는다 (필드/직렬화는 보존)."
    - "CircleDiameterMeasurement 의 EdgePolarity 는 Circle_RadialDirection 이 비어있지 않을 때(Inward/Outward, polar 경로) 숨겨지고, 비어있을 때(legacy fit 경로, INI 하위호환)만 표시된다."
    - "CompoundAngle/CompoundCenterBDistance/CompoundCenterCDistance 의 EdgeThreshold/Sigma/EdgeSampleCount/EdgeTrimCount/EdgePolarity/EdgeDirection 6개 프로퍼티는 PropertyGrid 에 더 이상 표시되지 않는다 (필드/직렬화 보존)."
    - "CompoundAngle 이 이미 계산한 DatumB 기준선(daR1,daC1,daR2,daC2)이 FAI-DatumLine 오버레이 + DatumOrigin 점 마커로 렌더링된다 (sibling EdgeToLineAngle 패턴)."
    - "어떤 파일도 resultValue 계산이나 기존 오버레이 동작이 바뀌지 않는다 (C2 는 순수 추가만, 나머지는 visibility-only)."
    - "솔루션이 msbuild Debug/x64 로 신규 에러/경고 0 으로 빌드된다."
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/Measurements/CircleCenterDistanceMeasurement.cs"
      provides: "dead EdgePolarity 에 [Browsable(false)] 적용"
      contains: "Browsable(false)"
    - path: "WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs"
      provides: "IsHiddenForRadialDirection 확장 — non-empty radialDir 시 EdgePolarity hide"
      contains: "EdgePolarity"
    - path: "WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundAngleMeasurement.cs"
      provides: "6개 dead Edge 프로퍼티 숨김 + FAI-DatumLine 오버레이 추가"
      contains: "FAI-DatumLine"
    - path: "WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterBDistanceMeasurement.cs"
      provides: "6개 dead Edge 프로퍼티 숨김"
      contains: "Browsable(false)"
    - path: "WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterCDistanceMeasurement.cs"
      provides: "6개 dead Edge 프로퍼티 숨김"
      contains: "Browsable(false)"
  key_links:
    - from: "MeasurementBase.MeasCorrectionFactor ([Browsable(false)] 확정-dead 패턴)"
      to: "5개 측정 파일의 dead 프로퍼티 숨김"
      via: "[PropertyTools.DataAnnotations.Browsable(false)] 어노테이션 재사용"
      pattern: "PropertyTools.DataAnnotations.Browsable\\(false\\)"
    - from: "EdgeToLineAngleMeasurement.cs FAI-DatumLine 오버레이 패턴"
      to: "CompoundAngleMeasurement.cs 오버레이 블록"
      via: "daR1..daC2 + DatumOrigin 점 마커로 FAI-DatumLine 추가"
      pattern: "FAI-DatumLine"
---

<objective>
확정된 PropertyGrid dead-property 버그 5건 + 누락 오버레이 1건 수정 (멀티에이전트 adversarial audit 로 확정, 재조사 불필요 — 구현만). 프로덕션 레시피(D:\Data\Recipe\FAI_1\main.ini)가 실제 사용하는 5개 측정 타입 대상.

Fix A: `CircleCenterDistanceMeasurement.cs` — `EdgePolarity` 는 실질 도달 가능한 모든 설정에서 결과에 영향이 없는데(legacy fit 분기는 기본값 "Inward" 때문에 UI 경로로 도달 불가, polar 분기는 설계상 무시) live ComboBox 로 노출됨 → `[Browsable(false)]` 로 숨김.

Fix B: `CircleDiameterMeasurement.cs` — `Circle_RadialDirection` 이 non-empty(polar) 일 때 EdgePolarity 는 읽히지 않으나 두 모드 모두에서 계속 표시됨 → 기존 `IsHiddenForRadialDirection` 메커니즘을 확장해 polar 경로에서만 EdgePolarity 를 숨김 (legacy fit 경로는 계속 표시, INI 하위호환).

Fix C: `CompoundAngleMeasurement.cs` — (C1) TryExecute 에서 전혀 읽히지 않는 6개 Edge 프로퍼티(copy-paste 잔재)를 숨김. (C2) 이미 계산·사용 중인 DatumB 기준선(daR1..daC2)이 오버레이로만 누락됨 → sibling `EdgeToLineAngleMeasurement.cs` 패턴으로 FAI-DatumLine 오버레이 추가 (순수 추가, resultValue 무변경).

Fix D/E: `CompoundCenterBDistanceMeasurement.cs` / `CompoundCenterCDistanceMeasurement.cs` — Fix C1 과 동일한 6개 dead Edge 프로퍼티 숨김.

Purpose: 사용자에게 결과에 영향 없는 편집 컨트롤을 노출해 혼란/오설정을 유발하던 문제 제거 + CompoundAngle 각도 판정 기준선의 시각적 확인 가능화. **알고리즘 동작 변경 아님** (C2 오버레이 추가만 예외, 그것도 순수 additive).
Output: 측정 파일 5개 수정, 파일당 atomic 커밋, msbuild Debug/x64 클린 빌드.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@CLAUDE.md

<constraints>
- C# 7.2 / .NET Framework 4.8. C# 8+ 문법 금지 (switch expression, nullable ref types, using declaration 등).
- 각 파일의 기존 스타일(Allman 브레이스, PascalCase 프로퍼티) 그대로 유지. 새 헬퍼/추상화 도입 금지.
- **필드/프로퍼티 자체나 직렬화(INI/JSON)는 절대 제거하지 않는다** — 오직 PropertyGrid 표시만 차단. 하위호환 유지.
- 확정-dead 프로퍼티 숨김의 established 컨벤션은 오직 `[PropertyTools.DataAnnotations.Browsable(false)]` — 새 숨김 방식 발명 금지.
- Fix B 는 알고리즘(TryExecute 의 polarity 계산)을 절대 건드리지 않는다 — visibility-only.
- Fix C2 는 순수 additive — resultValue 계산이나 기존 오버레이(FAI-Edge1/FAI-DiagLine) 한 줄도 바꾸지 않는다.
- 이 작업은 진행 중인 Phase 68(cross-Z dual-image)과 무관 — 위 5개 파일 외 어떤 파일도 건드리지 않는다.
</constraints>

<interfaces>
<!-- 코드베이스에서 추출 — executor 는 이 계약을 직접 사용, 재탐색 불필요. 단, 각 파일은 반드시 편집 전 FULL read 할 것. -->

확정-dead 프로퍼티 숨김의 established 패턴 (WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs:42~44):
  [PropertyTools.DataAnnotations.Browsable(false)]
  [System.ComponentModel.Description("측정별 보정계수(×). ... 1.0=무보정. ...")]
  public double MeasCorrectionFactor { get; set; } = 1.0;
  → 프로퍼티에 직접 [PropertyTools.DataAnnotations.Browsable(false)] 부착. 필드/직렬화 유지.

Fix B 대상 — CircleDiameterMeasurement.cs 의 기존 hide 메커니즘 (읽고 그대로 확장):
  - ICustomTypeDescriptor 구현 (:14). GetProperties → BuildFilteredProperties (:176~187) →
    DynamicPropertyHelper.FilterProperties(this, attrs, name => IsHiddenForRadialDirection(name, Circle_RadialDirection), sourceNames).
  - IsHiddenForRadialDirection(name, radialDir) (:189~198): 현재는 radialDir 이 빈값일 때만
    polar 4 필드(Circle_PolarStepDeg/RectL1Ratio/RectL2Ratio/PolarEdgeSelection + PolarEdgeSelectionList)를 hide.
  - EdgePolarityList 래퍼는 이미 [Browsable(false)] (:48) 이며 sourceNames HashSet 에도 포함 → 별도 hide 불필요.
    실제 렌더되는 것은 EdgePolarity(문자열 ComboBox) 뿐 → 그것만 non-empty 분기에서 hide 하면 됨.

Fix C2 대상 — sibling EdgeToLineAngleMeasurement.cs 의 FAI-DatumLine 오버레이 패턴 (:180~194):
  bool datumInjected = (DatumOriginRow != 0.0 || DatumOriginCol != 0.0);
  if (datumInjected)
  {
      overlays.Add(new EdgeInspectionOverlay
      {
          RoiId = "FAI-DatumLine",
          LineRow1 = daR1, LineColumn1 = daC1,
          LineRow2 = daR2, LineColumn2 = daC2,
          Points = new List<EdgeInspectionPoint>
          {
              new EdgeInspectionPoint { Row = DatumOriginRow, Column = DatumOriginCol }
          }
      });
  }
  → CompoundAngle 에는 daR1,daC1,daR2,daC2 가 이미 (:125~128) 계산되어 AngleLineLine 에 사용됨. 그 값을 그대로 재사용.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task A: Hide dead EdgePolarity in CircleCenterDistanceMeasurement</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Measurements/CircleCenterDistanceMeasurement.cs</files>
  <action>
파일 전체를 먼저 read 한다.

`EdgePolarity` 프로퍼티(현재 :27~28)에 `[PropertyTools.DataAnnotations.Browsable(false)]` 어노테이션을 추가한다 — MeasurementBase.MeasCorrectionFactor(:42~44) 패턴과 동일. 기존 `[ItemsSourceProperty(nameof(EdgePolarityList))]` 어노테이션과 필드/직렬화는 그대로 둔다 (무해, 하위호환).

바로 위에 짧은 한 줄 '왜' 주석 추가: TryFindCircle 의 polarity 파라미터가 메서드 본문에서 미참조이고, legacy fit 분기는 Circle_RadialDirection 기본값 "Inward" 때문에 PropertyGrid 경로로 도달 불가하며, polar 분기는 설계상 EdgePolarity 를 무시하므로 실질적으로 dead 임을 명시 (예: `// EdgePolarity dead: TryFindCircle polarity 인자 미참조 + legacy fit 분기(Circle_RadialDirection="")는 기본값 "Inward" 로 UI 도달 불가, polar 분기는 설계상 무시 → PropertyGrid 숨김(필드/INI 보존).`).

**절대 변경 금지**: EdgePolarity 필드/타입/기본값 "DarkToLight", TryExecute 의 어떤 로직도. TryFindCircle 를 polarity-aware 로 만들지 않는다 (범위 밖).
  </action>
  <verify>
    <automated>"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" DatumMeasurement.sln /p:Configuration=Debug /p:Platform=x64 /t:Build /v:m</automated>
    자기검토: EdgePolarity 에 [Browsable(false)] 가 붙었고, ItemsSourceProperty/필드/직렬화는 보존되었으며, TryExecute 는 무변경(diff)임을 확인.
  </verify>
  <done>EdgePolarity 가 PropertyGrid 에서 숨겨짐. 필드/직렬화/기본값 보존. TryExecute 무변경. 빌드 신규 에러/경고 0.</done>
</task>

<task type="auto">
  <name>Task B: Hide inert EdgePolarity in polar mode of CircleDiameterMeasurement</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs</files>
  <action>
파일 전체를 먼저 read 하고, 특히 `IsHiddenForRadialDirection`(:189~198) 과 그 소비자 `BuildFilteredProperties`/`DynamicPropertyHelper.FilterProperties`(:176~187) 흐름을 완전히 이해한 뒤 편집한다.

**기존** hide 메커니즘을 확장한다 (새 메커니즘 만들지 않음). 현재 `IsHiddenForRadialDirection` 은 `radialDir` 이 빈값일 때만 polar 4 필드를 hide 한다. 여기에 "radialDir 이 non-empty(polar 경로) 일 때 EdgePolarity 를 hide" 규칙을 추가한다 — polar 경로에서 polarity 는 `MapRadialDirectionToHalconPolarity(Circle_RadialDirection)` 로 파생되고 EdgePolarity 는 읽히지 않기 때문.

기존 메서드의 `if (string.IsNullOrEmpty(radialDir)) { ... }` 블록은 그대로 두고, 그 뒤에 established `if (name == "...") return true;` 스타일로 non-empty 분기를 추가한다:
```
else {
    // radialDir non-empty(polar): polarity = MapRadialDirectionToHalconPolarity 파생 → EdgePolarity 미참조 → hide (legacy fit 경로에서만 표시).
    if (name == "EdgePolarity") return true;
}
```
EdgePolarityList 래퍼는 이미 [Browsable(false)](:48) + sourceNames 포함(:180) 이므로 추가 hide 불필요 — 실제 렌더되는 EdgePolarity(문자열 ComboBox) 만 처리하면 충분.

**절대 변경 금지**: TryExecute 의 polarity 계산 로직(:86~121), EdgePolarity 필드/기본값, ICustomTypeDescriptor 위임 메서드들. 이것은 visibility-only 수정이다.
  </action>
  <verify>
    <automated>"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" DatumMeasurement.sln /p:Configuration=Debug /p:Platform=x64 /t:Build /v:m</automated>
    자기검토: IsHiddenForRadialDirection 이 non-empty radialDir 에서 EdgePolarity==name 일 때 true 를 반환하고, empty 분기(polar 4 필드 hide)는 무변경이며, TryExecute 는 무변경임을 확인. legacy fit 경로(radialDir="")에서는 EdgePolarity 가 여전히 표시됨을 논리적으로 확인.
  </verify>
  <done>EdgePolarity 가 Circle_RadialDirection non-empty(polar) 일 때만 숨겨지고 empty(legacy fit) 일 때 표시됨. TryExecute polarity 로직 무변경. 빌드 신규 에러/경고 0.</done>
</task>

<task type="auto">
  <name>Task C: Hide 6 dead Edge props + add missing DatumB overlay in CompoundAngleMeasurement</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundAngleMeasurement.cs</files>
  <action>
파일 전체를 먼저 read 한다. 이 태스크는 독립적인 두 수정(C1 숨김 + C2 오버레이 추가)을 포함한다.

**C1 (visibility-only):** 다음 6개 프로퍼티에 각각 `[PropertyTools.DataAnnotations.Browsable(false)]` 를 추가한다 — TryExecute 는 오직 `TryFindLargestContourRect(... CannyAlpha, CannyLow, CannyHigh, UnionDistance ...)` 만 호출하며 아래 6개는 어디서도 읽히지 않는 copy-paste 잔재(TryFitLine 기반 sibling 에서 복사됨):
  - EdgeThreshold (:30)
  - Sigma (:31)
  - EdgeSampleCount (:32)
  - EdgeTrimCount (:35 — 기존 [DisplayName]/주석 유지, Browsable 추가만)
  - EdgePolarity (:37 — 기존 [ItemsSourceProperty] 유지, Browsable 추가만)
  - EdgeDirection (:39 — 기존 [ItemsSourceProperty] 유지, Browsable 추가만)
그룹 위에 짧은 '왜' 주석 한 줄 추가 (예: `// 아래 6개는 미사용 copy-paste 잔재 — TryFindLargestContourRect(Canny 파이프라인)는 이들을 읽지 않음. PropertyGrid 숨김(필드/INI 보존).`).
래퍼 EdgeDirectionList/EdgePolarityList(:42~45)는 이미 [Browsable(false)] → 무변경. `[Category("Edge")]`(:29)도 그대로 (숨김 후 Edge 카테고리가 비는 것은 의도된 결과).

**C2 (purely additive overlay):** TryExecute 의 오버레이 블록에서, 기존 overlay 2(FAI-DiagLine, :153~163) 추가 **직후**·`return true;`(:165) **직전**에 FAI-DatumLine 오버레이를 추가한다. 이미 (:123~128)에서 계산된 `daR1,daC1,daR2,daC2`(DatumB 기준선 2점)를 재사용한다. sibling EdgeToLineAngleMeasurement.cs(:180~194) 패턴을 정확히 따른다:
  - `bool datumInjected = (DatumOriginRow != 0.0 || DatumOriginCol != 0.0);`
  - `if (datumInjected)` 가드 안에서 `RoiId = "FAI-DatumLine"`, LineRow1/Col1 = daR1/daC1, LineRow2/Col2 = daR2/daC2, Points = DatumOrigin(Row=DatumOriginRow, Column=DatumOriginCol) 단일 점 마커.
  - <interfaces> 의 sibling 스니펫과 동일한 필드 순서/스타일 사용. daR1..daC2 를 GetDatumAxisLine 등으로 재계산하지 않는다 — 이미 계산된 ±200px 라인 값을 그대로 사용.

**절대 변경 금지**: resultValue 계산(:131~139, AngleLineLine + UseSupplementaryAngle), 기존 FAI-Edge1/FAI-DiagLine 오버레이, daR1..daC2 계산식. C2 는 오직 새 오버레이 항목 추가만.
  </action>
  <verify>
    <automated>"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" DatumMeasurement.sln /p:Configuration=Debug /p:Platform=x64 /t:Build /v:m</automated>
    자기검토: (C1) 6개 프로퍼티 각각 [Browsable(false)] 부착 + 필드/직렬화 보존. (C2) 새 FAI-DatumLine 오버레이가 daR1..daC2 + DatumOrigin 마커를 datumInjected 가드로 추가하며, resultValue/기존 오버레이/daR1..daC2 계산은 diff 상 무변경임을 확인.
  </verify>
  <done>6개 dead Edge 프로퍼티가 PropertyGrid 에서 숨겨짐(필드/직렬화 보존). FAI-DatumLine 오버레이가 daR1..daC2 로 추가됨. resultValue/기존 오버레이 무변경. 빌드 신규 에러/경고 0.</done>
</task>

<task type="auto">
  <name>Task D: Hide 6 dead Edge props in CompoundCenterBDistanceMeasurement</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterBDistanceMeasurement.cs</files>
  <action>
파일 전체를 먼저 read 한다. Fix C1 과 동일한 dead-property 패턴 — 아래 6개 프로퍼티에 각각 `[PropertyTools.DataAnnotations.Browsable(false)]` 추가 (TryExecute 는 Rect_*/datumTransform/Canny*/UnionDistance 로 TryFindLargestContourRect 를 호출하고 MeasureAxis/DatumOrigin*/DatumAngle*Rad 로 투영거리를 내며, 아래 6개는 어디서도 읽히지 않음):
  - EdgeThreshold (:30)
  - Sigma (:31)
  - EdgeSampleCount (:32)
  - EdgeTrimCount (:35 — 기존 [DisplayName]/주석 유지)
  - EdgePolarity (:37 — 기존 [ItemsSourceProperty] 유지)
  - EdgeDirection (:39 — 기존 [ItemsSourceProperty] 유지)
그룹 위에 Fix C1 과 동일 취지의 '왜' 주석 한 줄. 래퍼 EdgeDirectionList/EdgePolarityList(:42~45)는 이미 [Browsable(false)] → 무변경.

**중요 — 숨기지 말 것**: `MeasureAxis`(:58)는 TryExecute(:119) 에서 실제 사용됨 → 그대로 표시 유지. `[Category("Edge")]` 도 유지.
**절대 변경 금지**: TryExecute 의 어떤 로직/오버레이도. visibility-only.
  </action>
  <verify>
    <automated>"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" DatumMeasurement.sln /p:Configuration=Debug /p:Platform=x64 /t:Build /v:m</automated>
    자기검토: 6개 프로퍼티 각각 [Browsable(false)] 부착 + 필드/직렬화 보존. MeasureAxis 는 여전히 표시. TryExecute 무변경(diff).
  </verify>
  <done>6개 dead Edge 프로퍼티가 PropertyGrid 에서 숨겨짐(필드/직렬화 보존). MeasureAxis 표시 유지. TryExecute 무변경. 빌드 신규 에러/경고 0.</done>
</task>

<task type="auto">
  <name>Task E: Hide 6 dead Edge props in CompoundCenterCDistanceMeasurement</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterCDistanceMeasurement.cs</files>
  <action>
파일 전체를 먼저 read 한다. 이 파일은 자체 doc 주석대로 CompoundCenterBDistance 와 구조 "완전 동일" — Fix D 와 동일하게 아래 6개 프로퍼티에 각각 `[PropertyTools.DataAnnotations.Browsable(false)]` 추가 (TryFindLargestContourRect 기반, 6개 미참조):
  - EdgeThreshold (:30)
  - Sigma (:31)
  - EdgeSampleCount (:32)
  - EdgeTrimCount (:35 — 기존 [DisplayName]/주석 유지)
  - EdgePolarity (:37 — 기존 [ItemsSourceProperty] 유지)
  - EdgeDirection (:39 — 기존 [ItemsSourceProperty] 유지)
그룹 위에 Fix D 와 동일 취지의 '왜' 주석 한 줄. 래퍼(:42~45)는 이미 [Browsable(false)] → 무변경.

**중요 — 숨기지 말 것**: `MeasureAxis`(:58, 기본값 "X")는 TryExecute(:119) 에서 사용됨 → 표시 유지.
**절대 변경 금지**: TryExecute 의 어떤 로직/오버레이도. visibility-only.
  </action>
  <verify>
    <automated>"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" DatumMeasurement.sln /p:Configuration=Debug /p:Platform=x64 /t:Build /v:m</automated>
    자기검토: 6개 프로퍼티 각각 [Browsable(false)] 부착 + 필드/직렬화 보존. MeasureAxis 표시 유지. TryExecute 무변경(diff).
  </verify>
  <done>6개 dead Edge 프로퍼티가 PropertyGrid 에서 숨겨짐(필드/직렬화 보존). MeasureAxis 표시 유지. TryExecute 무변경. 빌드 신규 에러/경고 0.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries
순수 내부 PropertyGrid visibility + 오버레이 렌더링 수정. 신규 외부 입력/네트워크/패키지 설치/파일 I/O 없음 — 새 신뢰 경계 없음.

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-vks-01 | Tampering | INI/JSON 하위호환(직렬화 회귀) | mitigate | 필드/프로퍼티/직렬화 어노테이션 제거 금지 — 오직 [Browsable(false)] 표시 차단만. 기존 레시피 load/save 무영향 |
| T-vks-02 | Tampering | 측정 결과 정확도(오버레이 오추가) | mitigate | C2 는 datumInjected 가드 + 이미 계산된 daR1..daC2 재사용, resultValue/기존 오버레이 무변경. Task별 자기검토(diff) |
| T-vks-03 | Repudiation | Fix B 가시성 로직 회귀(legacy 경로 오숨김) | accept | 기존 IsHiddenForRadialDirection 확장만, empty(legacy fit) 분기 무변경 → legacy 레시피 EdgePolarity 계속 표시. 컴파일+논리 검토로 확인 |
</threat_model>

<verification>
전체 확인 (visibility-only + C2 additive — "컴파일됨" 이상):
1. **빌드**: 5개 태스크 각각 완료 후 `"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" DatumMeasurement.sln /p:Configuration=Debug /p:Platform=x64 /t:Build /v:m` — 신규 에러/경고 0.
2. **Browsable 적용 검토** (A/C1/D/E): 대상 프로퍼티에 `[PropertyTools.DataAnnotations.Browsable(false)]` 가 정확히 부착됐고, 필드/타입/기본값/직렬화 어노테이션([ItemsSourceProperty]/[DisplayName])이 보존됐음을 확인.
3. **Fix B 가시성 로직 검토**: IsHiddenForRadialDirection 이 (a) empty radialDir → polar 4 필드 hide(무변경), (b) non-empty radialDir → EdgePolarity hide(신규) 두 분기를 정확히 처리. legacy fit 경로에서 EdgePolarity 계속 표시.
4. **Fix C2 additive 검토**: FAI-DatumLine 오버레이가 datumInjected 가드 하에 daR1..daC2 + DatumOrigin 마커로 추가됨. resultValue(AngleLineLine+보각), 기존 FAI-Edge1/FAI-DiagLine, daR1..daC2 계산식 모두 diff 상 무변경.
5. **무변경 검토**: 5개 파일 모두 TryExecute 의 측정 수학은 (C2 오버레이 추가 예외) 한 줄도 바뀌지 않음. MeasureAxis(D/E)는 계속 표시.
6. **범위 검토**: 위 5개 파일 외 어떤 파일도 수정되지 않음 (Phase 68 무관).
</verification>

<success_criteria>
- CircleCenterDistance/CircleDiameter/CompoundAngle/CompoundCenterBDistance/CompoundCenterCDistance 의 확정-dead 프로퍼티가 PropertyGrid 에 더 이상 편집 컨트롤로 노출되지 않음.
- Fix B: EdgePolarity 는 polar 경로에서만 숨겨지고 legacy fit 경로(INI 하위호환)에서는 표시.
- Fix C2: CompoundAngle 의 DatumB 기준선이 FAI-DatumLine 오버레이로 렌더링됨.
- 모든 필드/직렬화/resultValue 계산 보존 (C2 오버레이 추가만 예외, 그것도 additive).
- msbuild Debug/x64 신규 에러/경고 0. 파일당 atomic 커밋.
</success_criteria>

<output>
Create `.planning/quick/260722-vks-fix-5-confirmed-dead-propertygrid-proper/260722-vks-SUMMARY.md` when done
</output>
