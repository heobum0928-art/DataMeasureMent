# Phase 19: PropertyGrid 동적 노출 일반화 — Context

**Gathered:** 2026-05-07 (discuss checkpoint 복원)
**Status:** Ready for planning
**Source:** 19-DISCUSS-CHECKPOINT.json

<domain>
## Phase Boundary

DatumConfig 전용으로 구현된 ICustomTypeDescriptor 기반 동적 PropertyGrid 필터링 패턴을 FAIConfig에도 확장한다. 두 모델 모두 공통 정적 헬퍼(`DynamicPropertyHelper.FilterProperties`)를 통해 런타임에 필요한 속성만 PropertyGrid에 노출한다.

**범위 내:**
- `DynamicPropertyHelper.FilterProperties` 신규 파일 1개 생성
- `DatumConfig.GetProperties(Attribute[])` 내부를 헬퍼 호출로 리팩토링
- `FAIConfig`에 `EdgeMeasureType` 필드 추가 (INI 저장) + ICustomTypeDescriptor 구현
- CO-02: DatumConfig Phase 17 Test 8 (TLI/CTH/VTH 동적 노출) 회귀 검증

**범위 외:**
- FAIConfig 이외의 다른 모델 클래스 확장
- MeasurementBase 파생 클래스별 PropertyGrid 필터
- INI 스키마 변경 (EdgeMeasureType는 신규 필드, 미존재 시 fallback)
</domain>

<decisions>
## Implementation Decisions

### FAIConfig EdgeMeasureType 필드
- FAIConfig에 `EdgeMeasureType` 신규 string 필드 추가
- **INI 저장 필요** — DatumConfig.AlgorithmType 과 동일 패턴 (ParamBase 자동 직렬화)
- EdgeMeasureType INI 미존재 시 기본값: `""` → `"EdgePairDistance"` fallback (EnsureEdgeMeasureTypeDefault or property getter)
- `[ItemsSourceProperty(nameof(EdgeMeasureTypeList))]` 드롭다운 연결
- 유효값: MeasurementFactory.GetTypeNames() 목록 ("EdgePairDistance", "PointToLineDistance", "PointToPointDistance", "LineToLineAngle", "CircleDiameter", "LineToLineDistance")

### FAIConfig 동적 숨김 규칙
CircleDiameter 선택 시 숨길 필드:
- `EdgeDirection` (및 `EdgeDirectionList`)
- `EdgePolarity` (및 `EdgePolarityList`)
- `EdgeSelection`
- `EdgeSampleCount`
- `EdgeTrimCount`
- `Sigma`

모든 타입에서 항상 노출:
- ROI 그룹: `ROI_Row`, `ROI_Col`, `ROI_Phi`, `ROI_Length1`, `ROI_Length2`, `PolygonPoints`
- Calibration: `PixelResolutionX`, `PixelResolutionY`
- `EdgeThreshold` (CircleDiameter 제외 모든 타입 공통)

### 공통 추상화 전략
- 신규 파일: `WPF_Example/Custom/Sequence/Inspection/DynamicPropertyHelper.cs`
- 시그니처: `public static PropertyDescriptorCollection FilterProperties(object obj, Attribute[] attrs, Func<string, bool> hideFunc, HashSet<string> sourceNames)`
- 내부 로직:
  1. `TypeDescriptor.GetProperties(obj, attrs, true)` → hideFunc로 필터
  2. `TypeDescriptor.GetProperties(obj, true)` (allNoFilter) → sourceNames 화이트리스트 추가
  3. `PropertyDescriptorCollection(keep.ToArray())` 반환
- 클래스 계층 변경 없음 (DatumConfig/FAIConfig 상속 구조 유지)

### DatumConfig 리팩토링 (회귀 0)
- DatumConfig.GetProperties(Attribute[]) 내부 로직을 DynamicPropertyHelper.FilterProperties 호출로 교체
- IsHiddenForAlgorithm(string, EDatumAlgorithm) static 메서드는 DatumConfig에 유지 (헬퍼 콜백으로 전달)
- DatumConfig 행동 변경 없음 — 리팩토링 전후 동일 PropertyDescriptor 컬렉션 반환

### CO-02 수용
- Phase 17 Test 8 재검증: TLI(Line1_*/Line2_* 노출, Circle_*/Vertical_*/Horizontal_A_*/Horizontal_B_* 숨김), CTH(Circle_*(EdgeDirection 제외) + Horizontal_A_*/B_* 노출), VTH(Vertical_* + Horizontal_A_*/B_* 노출) 각각 확인
- DatumConfig 동적 노출 회귀 0 확인 (Phase 17-02 작동 그대로)
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 핵심 구현 파일
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — ICustomTypeDescriptor 패턴 원본 (GetProperties, IsHiddenForAlgorithm, allNoFilter+sourceNames CO-01 패턴)
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` — 확장 대상 (현재 ICustomTypeDescriptor 없음)
- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` — FAI 측정 추상 기반
- `WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs` — EdgeMeasureType 유효값 목록 소스

### Phase 17 패턴 레퍼런스
- `.planning/milestones/v1.0-phases/17-datum-ux-circle-strip-1-test-find-detectedorigin-hover/17-UAT.md` — Test 8 CO-02 재검증 기준

### Phase 18 패턴 레퍼런스
- `.planning/phases/18-carry-over-cleanup/18-01-PLAN.md` — allNoFilter+sourceNames CO-01 패턴 (DatumConfig.GetProperties 수정 이력)
</canonical_refs>

<specifics>
## Specific Ideas

- `DynamicPropertyHelper` 위치: `WPF_Example/Custom/Sequence/Inspection/` (DatumConfig/FAIConfig 와 동일 네임스페이스 `ReringProject.Sequence`)
- FAIConfig.EdgeMeasureType 기본값: `"EdgePairDistance"` (가장 기본적인 타입, 숨김 필드 없음)
- CircleDiameter 타입은 ROI 형태가 원이므로 EdgeDirection/Polarity/Selection 파라미터가 의미 없음 → 숨김
- csproj 등록 필수: `WPF_Example/DatumMeasurement.csproj` Compile ItemGroup에 DynamicPropertyHelper.cs 추가
</specifics>

<deferred>
## Deferred Ideas

- FAIConfig 이외 MeasurementBase 파생 클래스별 PropertyGrid 동적 필터 — v1.2 고려
- DynamicPropertyHelper 의 Category-level 숨김 지원 — 현재 PropertyDescriptor name 기반으로 충분
</deferred>

---

*Phase: 19-propertygrid-dynamic-exposure*
*Context gathered: 2026-05-07 via discuss checkpoint*
