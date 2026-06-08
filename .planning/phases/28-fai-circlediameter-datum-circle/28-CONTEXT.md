# Phase 28: FAI CircleDiameter + Datum Circle 알고리즘 통합 — Context

**Gathered:** 2026-05-08
**Status:** Ready for planning

<domain>
## Phase Boundary

`CircleDiameterMeasurement`(FAI 측정) 의 검출 알고리즘에 Datum CircleTwoHorizontal 의 폴라 샘플링 경로(`VisionAlgorithmService.TryFindCircleByPolarSampling`)를 **선택적으로** 추가한다. 사용자가 `Circle_RadialDirection` 콤보로 `Inward` 또는 `Outward` 를 선택하면 폴라 경로, 빈 문자열(default) 이면 기존 fit 경로(`TryFindCircle`) 가 호출된다.

**범위 내:**
- `CircleDiameterMeasurement.cs` 단일 파일에 `Circle_RadialDirection` 필드 + `[ItemsSourceProperty]` 콤보 + `TryExecute` 분기 추가
- `EdgeOptionLists.cs` 에 polarity 매핑 helper static 메서드 + polar default 상수 추가
- `DatumFindingService.cs` 의 polarity inline 매핑 2곳을 신규 helper 호출로 교체 (3중 inline 제거)

**범위 외:** SPEC §Out of Scope 그대로 유지 (Circle_EdgeDirection / EdgeSelection / RectL1Ratio / RectL2Ratio / ICustomTypeDescriptor 도입 / 다른 Measurement 클래스 변경)

</domain>

<spec_lock>
## Requirements (locked via SPEC.md)

**6 requirements 잠김.** 자세한 요구사항·범위·수용 기준은 `28-SPEC.md` 참조.

다운스트림 에이전트(researcher, planner, executor)는 plan/implement 전 반드시 `28-SPEC.md` 를 읽어야 한다. CONTEXT.md 는 요구사항을 중복 기술하지 않는다.

**In scope (from SPEC.md):**
- CircleDiameterMeasurement.cs 단일 필드 추가 (Circle_RadialDirection)
- TryExecute 분기 로직 (빈 → fit / 명시 → polar)
- PropertyGrid 콤보 노출 (정적, 동적 hide 없음)
- INI 하위호환 (default 빈 string)
- Datum 폴라 helper 의 FAI 호출 경로
- EdgeOptionLists.RadialDirections 단일 소스 재사용

**Out of scope (from SPEC.md):**
- Circle_EdgeDirection / Circle_EdgeSelection 추가
- RectL1Ratio / RectL2Ratio (strip cap) 노출
- CircleROI / Rect strip ROI 입력 방식 변경
- ICustomTypeDescriptor 동적 hide 도입
- Test Find DetectedOrigin 시각화
- 다른 Measurement 클래스 (PointToLineDistance 등) 알고리즘 변경
- Phase 19 측정 추가 모달 UX (이미 quick 260508-mcb 완료)

</spec_lock>

<decisions>
## Implementation Decisions

### 호출 경로 / Helper

- **D-01: 직접 호출.** `VisionAlgorithmService.TryFindCircleByPolarSampling` 은 이미 public (line 214). `CircleDiameterMeasurement.TryExecute` 에서 `new VisionAlgorithmService().TryFindCircleByPolarSampling(...)` 직접 호출. 신규 wrapper 클래스 / 신규 service 메서드 추가 없음 — 최소 스코프.

- **D-02: Polarity 매핑은 EdgeOptionLists static 메서드로 단일화.** `EdgeOptionLists` 에 `public static string MapRadialDirectionToHalconPolarity(string radial)` 추가. 매핑 규칙 = `"Outward" → "negative"`, 그 외(`"Inward"` 포함) → `"positive"` (Datum CTH 의 기존 line 200, 730 inline 식과 동일).
- **D-03: DatumFindingService 두 곳을 helper 호출로 교체.** `DatumFindingService.cs` line 200 (`TryFindCircleTwoHorizontal`), line 730 (`TryTeachCircleTwoHorizontal`) 의 inline `string.Equals(...) ? "negative" : "positive"` 코드 → `EdgeOptionLists.MapRadialDirectionToHalconPolarity(config.Circle_RadialDirection)` 호출로 교체. 3중 inline 중복 제거. 이는 SPEC out-of-scope "다른 Measurement 클래스 변경 금지" 에 저촉되지 않음 (DatumFindingService 는 service 레이어).

### Polar 알고리즘 기본 파라미터 (FAI 측)

- **D-04: EdgeOptionLists 에 FAI polar default 상수 추가.** SPEC out-of-scope 에 따라 PolarStepDeg / RectL1Ratio / RectL2Ratio / EdgeSelection 4개는 PropertyGrid 에 노출하지 않으나, polar 경로 호출 시 값이 필요하다. EdgeOptionLists 에 다음 const 추가:
  - `public const double FaiCirclePolarStepDeg = 10.0;`
  - `public const double FaiCircleRectL1Ratio  = 0.02;`
  - `public const double FaiCircleRectL2Ratio  = 0.02;`
  - `public const string FaiCircleEdgeSelection = "First";`
  값은 `DatumConfig` Circle_PolarStepDeg / RectL1Ratio / RectL2Ratio default (10.0 / 0.02 / 0.02) 및 `EdgeOptionLists.Selections` 의 첫 항목과 일치 → REQ-28-03 동등성 자동 보장.

- **D-05: REQ-28-03 동등성 검증은 default 일치 + 사용자 UAT 단일.** AC-4 (`|FAI_diameter - Datum_diameter| ≤ 0.001 mm`) 는 D-04 의 default 일치를 통해 결정적으로 만족된다. 자동 회귀 테스트 / 런타임 assert 도입하지 않음 — v1.0 시점 test framework 없음, 신규 도입은 스코프 초과. AC-4 검증은 SIMUL_MODE (D:\\1.bmp) 에서 사용자가 동일 ROI / Sigma / EdgeThreshold / RadialDirection 설정 후 결과 비교.

### PropertyGrid 노출

- **D-06: `[Category("Edge")]` 그룹.** Circle_RadialDirection 은 EdgeThreshold / Sigma / EdgePolarity 와 같은 Edge 카테고리에 배치. polarity / RadialDirection 은 의미적으로 에지 검출 방향 파라미터로 함께 보는 게 자연스러움. AC-1 충족.

- **D-07: 콤보 옵션 = `EdgeOptionLists.RadialDirections` 2옵션 그대로 단일 소스.** SPEC REQ-28-01 "동일한 단일 소스 사용" 해석을 그대로 적용. 콤보에는 `Inward` / `Outward` 2개만 표시. 빈 문자열 = "선택 안 함" 상태는 INI default / 코드 default 로만 존재 → 사용자가 콤보에서 직접 빈 값으로 되돌릴 수 없음. 한 번 Inward/Outward 를 선택하면 fit 경로로 복귀하려면 INI 파일에서 `Circle_RadialDirection=` 라인을 비우거나 키 자체를 삭제해야 한다.
  - 트레이드오프: UX 약간 어색하나, FAI 전용 List (3옵션) 또는 라벨 매핑 도입 시 SPEC "단일 소스" 해석에서 멀어짐 + 추가 코드. 현재 UX 비용이 단순성 이득보다 작다고 판단.
  - 사용자 가이드 필요 시: "RadialDirection 을 선택하지 않으려면 INI 편집" 메모를 README/UAT 노트에 추가 (planner 결정).

### EdgePolarity 우선순위

- **D-08: RadialDirection 명시 시 EdgePolarity 무시.** Polar 경로의 polarity 는 `MapRadialDirectionToHalconPolarity(Circle_RadialDirection)` 단일 원천. 기존 EdgePolarity 필드는 동일 객체에 남아있으나 polar 호출에서는 사용하지 않는다. RadialDirection 빈 값일 때만 fit 경로 + EdgePolarity 적용 (기존 동작 그대로) → REQ-28-04 INI 하위호환 보장.
  - Datum CTH 의 기존 패턴 (DatumFindingService line 200/730 에서 RadialDirection → polarity override) 과 의미적으로 동일.
  - EdgePolarity 필드를 hide 하지 않음 (REQ-28-05 ICustomTypeDescriptor 미도입). 사용자는 PropertyGrid 에서 두 필드 모두 보지만, polar 경로 진입 시 EdgePolarity 는 무시된다는 사실은 위 D-07 의 "사용자 가이드" 와 함께 문서화.

### Claude's Discretion

- D-03 의 helper 호출 교체 시 `using ReringProject.Sequence;` 또는 fully-qualified name (`ReringProject.Sequence.EdgeOptionLists.Map...`) 어느 쪽을 쓸지 — DatumFindingService.cs 의 기존 using 절과 일관성에 따라 planner / executor 가 결정.
- `Circle_RadialDirection` 의 PropertyGrid 표시 순서 (EdgeThreshold/Sigma/EdgePolarity 사이 어디 배치) — 선언 순서 그대로면 됨, 별도 attribute 강제 안 함.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 28 SPEC (locked requirements)
- `.planning/phases/28-fai-circlediameter-datum-circle/28-SPEC.md` — **MUST read.** REQ-28-01~06, AC-1~6, In/Out scope.

### 핵심 구현 파일
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs` (62 lines) — 수정 대상. 현재 6필드 (Circle_Row/Col/Radius + EdgeThreshold/Sigma/EdgePolarity), `TryExecute` 가 `VisionAlgorithmService.TryFindCircle` 단일 호출.
- `WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs` — 매핑 helper + polar default 상수 추가 대상. 현재 RadialDirections (line 27) 단일 소스 보유.
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` §`TryFindCircle` (line 120-200), §`TryFindCircleByPolarSampling` (line 214-370) — 두 메서드 모두 public, 직접 호출 가능. 변경 없음.
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` line 200 (`TryFindCircleTwoHorizontal`), line 730 (`TryTeachCircleTwoHorizontal`) — D-03 inline polarity 매핑 교체 대상.

### Phase 17 D-02 패턴 (단일 소스 재사용)
- `WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs` §line 25-27 — `RadialDirections = {"Inward","Outward"}` 단일 소스 정의 (Phase 17 D-02). FAI 도 그대로 참조.

### Phase 18 CO-01 패턴 (RadialDirection 콤보 바인딩 검증)
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` line 242-252 — `Circle_RadialDirection` + `[ItemsSourceProperty(nameof(Circle_RadialDirectionList))]` + `Circle_RadialDirectionList` 래퍼 패턴. FAI 도 동일 패턴 모방.

### Phase 19 의존 (선행 phase, 동적 hide 미적용)
- `.planning/phases/19-propertygrid-dynamic-exposure/19-CONTEXT.md` — Phase 19 가 FAIConfig 에 EdgeMeasureType 동적 hide 도입했으나 Phase 28 의 CircleDiameterMeasurement 는 ICustomTypeDescriptor 미구현 (REQ-28-05) → 정적 노출만.

### REQ / ROADMAP
- `.planning/REQUIREMENTS.md` — Phase 28 entry (필요 시 참조).
- `.planning/ROADMAP.md` — Phase 28 등록 (a1ca199 commit).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `EdgeOptionLists.RadialDirections` — Phase 17 D-02 에서 정의된 단일 소스. CircleDiameter 콤보 ItemsSource 로 그대로 참조 (FAI 전용 변형 List 만들지 않음).
- `VisionAlgorithmService.TryFindCircleByPolarSampling` — public, sanity clamp 보유 (`stepDeg≤0→10`, `ratio≤0→0.05`, `selection=""→"First"`). FAI 에서 직접 호출 가능.
- `DatumConfig.Circle_RadialDirection` 콤보 바인딩 패턴 (line 242-252) — `[ItemsSourceProperty(nameof(Circle_RadialDirectionList))]` + `[Browsable(false)] public List<string> Circle_RadialDirectionList { get; }` — CircleDiameterMeasurement 에 동일 패턴 적용.
- `MeasurementBase.cs` ParamBase 자동 INI 직렬화 — string 필드는 자동 처리 → REQ-28-04 INI 하위호환 자동 충족 (default `""` 유지).

### Established Patterns
- `//260508 hbk Phase 28` 주석 컨벤션 — 모든 신규/수정 라인에 필수 (CLAUDE.md, SPEC.md Constraints).
- K&R 브레이스 스타일 (CircleDiameterMeasurement.cs 기존 스타일) 유지 (CLAUDE.md, SPEC.md Constraints).
- `[Category("...")]` + `[ItemsSourceProperty(nameof(...))]` + `[Browsable(false)] public List<string> ...List` 콤보 노출 3종 세트.
- Polarity 매핑 inline 중복 (DatumFindingService line 200, 730) — D-03 으로 helper 호출 교체 (현재 phase 의 부수 정리).

### Integration Points
- `CircleDiameterMeasurement.TryExecute` 분기:
  ```csharp
  if (string.IsNullOrEmpty(Circle_RadialDirection))
  {
      // 기존 fit 경로 (변경 없음)
      svc.TryFindCircle(image, Circle_Row, Circle_Col, Circle_Radius,
                        datumTransform, Sigma, EdgeThreshold, EdgePolarity,
                        out foundRow, out foundCol, out foundRadius, out error);
  }
  else
  {
      // polar 경로 (신규)
      string polarity = EdgeOptionLists.MapRadialDirectionToHalconPolarity(Circle_RadialDirection);
      bool[] unusedStrips;
      HTuple unusedRows, unusedCols;
      svc.TryFindCircleByPolarSampling(
          image, Circle_Row, Circle_Col, Circle_Radius,
          EdgeOptionLists.FaiCirclePolarStepDeg,
          EdgeOptionLists.FaiCircleRectL1Ratio,
          EdgeOptionLists.FaiCircleRectL2Ratio,
          Sigma, EdgeThreshold, polarity,
          EdgeOptionLists.FaiCircleEdgeSelection,
          datumTransform,
          out foundRow, out foundCol, out foundRadius,
          out unusedRows, out unusedCols, out unusedStrips,
          out error);
  }
  ```
  (정확한 시그니처는 planner 가 VisionAlgorithmService.cs line 214-223 확인 후 매칭.)

- DatumFindingService 두 곳 동일 교체:
  ```csharp
  // Before
  string circlePolarity = string.Equals(config.Circle_RadialDirection, "Outward", ...) ? "negative" : "positive";
  // After
  string circlePolarity = EdgeOptionLists.MapRadialDirectionToHalconPolarity(config.Circle_RadialDirection);
  ```

</code_context>

<specifics>
## Specific Ideas

- EdgeOptionLists 신규 멤버 위치: 기존 List 정의 (line 13-27) 다음에 polar default 상수 4개 + 매핑 메서드 1개 추가. 주석 `//260508 hbk Phase 28` 필수.
- CircleDiameterMeasurement 신규 콤보 표시 라벨: 기본 PropertyTools 표시 (`Circle_RadialDirection` → "Circle Radial Direction") 그대로. 한국어 라벨 attribute 미도입.
- INI 키명: `Circle_RadialDirection` (ParamBase 자동 직렬화). 기존 INI 파일 로드 시 키 부재 → C# default `""` → fit 경로 → REQ-28-04 동일 결과 보장.
- AC-2 / AC-3 grep 명령 예시 (planner 가 plan 의 acceptance 에 그대로 사용):
  - 빈 문자열 분기 grep: `grep -A 5 "string.IsNullOrEmpty(Circle_RadialDirection)" CircleDiameterMeasurement.cs`
  - polar 호출 grep: `grep "TryFindCircleByPolarSampling" CircleDiameterMeasurement.cs`
- AC-6 신규 라인 주석: `grep -c "260508 hbk Phase 28" CircleDiameterMeasurement.cs EdgeOptionLists.cs DatumFindingService.cs` ≥ 변경 라인 수.

</specifics>

<deferred>
## Deferred Ideas

- **사용자가 콤보에서 직접 빈 값(="선택 안 함")으로 복귀하는 UX** — D-07 의 단순성 우선 결정으로 INI 수동 편집 필요. 추후 v1.2 backlog 검토 (FAI 전용 RadialDirections 3옵션 List 또는 "(선택 안 함)" 라벨 매핑).
- **다른 FAI Measurement 클래스에 Datum 알고리즘 통합** (PointToLineDistance / LineToLineAngle 등) — Phase 28 out of scope. 패턴이 검증되면 v1.2 에서 일반화.
- **CircleDiameter 의 PolarStepDeg / RectL1Ratio / RectL2Ratio 사용자 노출** — D-04 의 const 가 모든 사용 사례를 커버하지 못할 경우 v1.2 재검토 (현재는 Datum default 일치가 REQ-28-03 동등성을 결정).
- **자동화된 FAI ↔ Datum 동등성 회귀 테스트** — D-05 에서 자동 테스트 도입 보류. v1.2 에서 SIMUL_MODE 기반 unit-test 인프라 도입 시 추가 검토.

</deferred>

---

*Phase: 28-fai-circlediameter-datum-circle*
*Context gathered: 2026-05-08*
