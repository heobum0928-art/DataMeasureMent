# Phase 4: Datum System - Context

**Gathered:** 2026-04-09
**Status:** Ready for planning

<domain>
## Phase Boundary

2D AOI 검사에서 제품이 지그(fixture)에 놓일 때마다 생기는 위치·회전 편차를 보정하기 위한 Datum(기준좌표계) 시스템을 도입한다. Datum은 FAIConfig(묶음 = 이미지 단위) 레벨의 속성으로 소속되며, 묶음 내 모든 하위 FAI가 하나의 Datum을 공유한다.

티칭 시점에는 "기준 제품"의 Datum을 저장하고, 런타임 시점에는 매 제품마다 Datum을 새로 계산하여 **기준 Datum과의 변환 행렬(hom_mat2d)** 을 구한다. 이 행렬을 하위 FAI의 ROI(픽셀 좌표)에 적용하여 제품 편차를 자동 보정한다.

Phase 2(ROI 티칭)와 Phase 3(에지 측정)를 관통하는 횡단 레이어이며, **3D/Laser 검사는 범위 외**이다. 제품 종류가 바뀌어도 "Line 2개 → 교점 + 기준각" 이라는 큰 틀은 유지한다는 전제로 설계한다.

Phase 1~3의 기존 데이터 포맷(FAI ROI = 픽셀 절대좌표)은 유지하며, Datum 보정은 **런타임에만** 적용한다. 마이그레이션이 필요 없는 비파괴적 확장이다.

</domain>

<decisions>
## Implementation Decisions

### Datum의 소속 위치
- **D-01:** Datum은 FAIConfig(묶음, 이미지 단위) 레벨 속성이다. 하위 FAI들의 **공통 부모 속성**이며 형제가 아니다. 이미지 1장당 Datum 1개.
- **D-02:** Datum 계산(`FindDatum()`)은 묶음 FAI당 1회만 실행한다. 촬영 직후, 하위 FAI 측정 루프 **전**에 실행된다.

### Datum 모델 구조
- **D-03:** Datum 기본 방식은 **"Line 2개 → 교점 + 기준 X축"** 이다. PDF의 Pre-Datum → Datum A/B 2단계는 내부적으로 이 하나의 구조로 통합한다 (복잡도 감소).
- **D-04:** DatumConfig는 다음을 소유한다:
  - `Line1_ROI` — 기준 X축을 구할 에지 검색 영역
  - `Line2_ROI` — 기준 Y축을 구할 에지 검색 영역
  - `RefOriginRow`, `RefOriginCol` — 티칭 시 저장된 기준 교점 (픽셀)
  - `RefAngleRad` — 티칭 시 저장된 기준 X축 각도
  - `CurrentTransform` — 런타임에 계산되는 hom_mat2d (휘발성)
- **D-05:** 제품 교체에 대비해 DatumConfig는 **전략 패턴 없이 단일 클래스**로 시작한다. 다른 Datum 패턴(원형, 3점 등)이 필요해지면 그때 추상화한다. (YAGNI)

### 좌표계 기준 (비파괴적 접근)
- **D-06:** 하위 FAI의 ROI는 **기존과 동일하게 픽셀 절대좌표로 저장**한다. Phase 1~3의 저장 포맷을 변경하지 않는다. 단, 이 픽셀 좌표는 "티칭 시점에 기준 제품 위에서 찍은 위치"로 간주한다.
- **D-07:** 런타임 실행 순서:
  1. 촬영
  2. `FindDatum()` 실행 → 현재 제품의 교점/각도 추출
  3. 기준 Datum ↔ 현재 Datum 의 차이로 `CurrentTransform` 계산
  4. 각 FAI의 저장된 픽셀 ROI를 `CurrentTransform`으로 변환 → 보정된 픽셀 ROI
  5. 보정된 ROI로 MeasurePos 호출
- **D-08:** 티칭 흐름에서 Datum이 먼저 확정되지 않아도 FAI ROI를 지정할 수 있다. 단 이 경우 보정 기준이 없으므로 경고를 표시한다.

### UI 통합
- **D-09:** `ENodeType`에 `Datum` 타입을 추가한다. InspectionListView 트리에서 묶음 FAI 노드 하위에 Datum 노드 1개가 자동 생성된다 (FAI 노드들과 형제 관계).
- **D-10:** Datum 노드 선택 시 PropertyGrid에 DatumConfig 속성이 표시된다. 기존 PropertyGrid 바인딩 패턴을 그대로 사용한다.
- **D-11:** DatumConfig는 `ParamBase` 를 상속한다. ROI 필드는 Phase 2의 Rectangle1 ROI 편집 방식을 재사용한다.
- **D-12:** Datum용 Line ROI 편집은 기존 "Rect ROI" 버튼과 동일한 MainView 캔버스 드래그 방식을 사용한다. 별도 전용 모드를 만들지 않는다. Datum 노드가 선택된 상태에서 Rect ROI 버튼을 누르면 Line1/Line2를 순차로 지정하는 흐름으로 처리한다.

### 티칭 흐름
- **D-13:** Datum 티칭은 "기준 제품"을 올리고 1회 수행한다:
  1. 촬영
  2. Line1 ROI 지정 → find_line 실행 → 기준 X축 직선 획득
  3. Line2 ROI 지정 → find_line 실행 → 기준 Y축 직선 획득
  4. 교점 계산 → `RefOriginRow/Col` 저장
  5. X축 직선의 각도 → `RefAngleRad` 저장
- **D-14:** 하위 FAI의 ROI는 이 "기준 제품" 이미지 위에서 픽셀로 지정한다. 이후 런타임에서는 제품이 다른 위치에 놓여도 Datum 변환이 자동으로 ROI를 따라가게 한다.

### Halcon 구현
- **D-15:** `FindDatum()` 은 신규 서비스 클래스 `DatumFindingService` 로 구현한다. 입력: HImage + DatumConfig. 출력: `HomMat2D CurrentTransform` + 성공/실패.
- **D-16:** 내부적으로 Halcon `find_line` → `intersection_ll` → `hom_mat2d_identity` → `hom_mat2d_translate` → `hom_mat2d_rotate` 조합을 사용한다. ROI 변환은 `affine_trans_pixel` 또는 `hom_mat2d_affine_trans_point` 로 적용한다.
- **D-17:** Datum 실패 시(직선 찾기 실패, 교점 계산 실패 등) 해당 묶음의 모든 하위 FAI는 측정하지 않고 NG 처리한다. 재시도나 폴백 전략은 Phase 4 범위가 아니다.

### Claude's Discretion
- DatumConfig의 ParamBase 직렬화 포맷 (ROI 2개를 어떤 string 형태로 저장할지)
- `DatumFindingService` 내부의 에러 처리 세부
- 트리에서 Datum 노드를 자동 생성하는 시점 (묶음 FAI 생성 시 자동 1개 vs 명시적 추가)
- 픽셀 ROI 변환 유틸리티의 정확한 위치 (DatumConfig 메서드 vs 별도 헬퍼 vs FAIEdgeMeasurementService 내부)
- Datum 노드의 트리 아이콘/라벨
- Datum 미설정 상태에서 측정 실행 시 동작 (무보정 pass-through vs 경고 후 실행 vs 실패)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 1 결과물 (트리 + FAI 구조)
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` — 묶음 FAI, 이미지, List<Measurement> 소유. DatumConfig 필드 추가 대상.
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — 트리 CRUD. Datum 노드 추가 위치.
- `WPF_Example/UI/ControlItem/NodeViewModel.cs`, `Node.cs` — ENodeType 확장 위치.
- `WPF_Example/UI/ControlItem/InspectionListViewModel.cs` — 트리 생성 로직.

### Phase 2 결과물 (ROI + 캘리브레이션)
- `WPF_Example/Halcon/Models/RoiDefinition.cs` — ROI 모델. Datum의 Line ROI도 이걸 재사용.
- `WPF_Example/Sequence/Param/CameraSlaveParam.cs` — PixelResolution 필드. 참고용 (Phase 4에서는 mm 변환 미사용).
- `WPF_Example/UI/ContentItem/HalconViewerControl.xaml.cs` — StartRectangleDrawing, CommitActiveRectangle. Line ROI 지정에 재사용.
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — ExitCanvasMode, 캔버스 툴바 버튼 흐름.
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` — ROI 렌더링. Datum Line ROI 표시 확장.

### Phase 3 결과물 (에지 측정, 수정 대상)
- `WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs` — `TryMeasure(image, fai, out result)`. **transform 파라미터를 받아 ROI를 변환한 뒤 측정하도록 확장 필요.**
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — 묶음 FAI 측정 실행. **FindDatum() 호출을 하위 FAI 루프 전에 삽입 필요.**
- `WPF_Example/Halcon/Models/FAIEdgeMeasurementResult.cs` — 결과 모델. Datum 정보 로깅 필요 시 확장.

### 프레임워크 기반
- `WPF_Example/Sequence/Param/ParamBase.cs` — INI 직렬화. DatumConfig가 상속.
- `WPF_Example/Sequence/Sequence/SequenceContext.cs` — ActionContext, 측정 실행 컨텍스트.

### 참고 문서 (개념 검증)
- PDF: `010_RC_Varo_AOI_MSoP___Z_Stopper_20230428.pdf` — Station #2/#3/#5/#6 의 Datum SetUp 및 Datum A/B 페이지. 본 Phase에서 참조하는 Datum 패턴의 근거.

</canonical_refs>

<code_context>
## Existing Code Insights

### Datum 없는 현재 흐름 (Phase 3 완료 시점)
1. `Action_FAIMeasurement` 가 묶음 FAI의 이미지를 가져옴
2. 하위 FAI 루프 → 각 FAI의 ROI(픽셀 절대좌표)로 `FAIEdgeMeasurementService.TryMeasure` 호출
3. 제품 편차 시 ROI가 엉뚱한 위치를 봄 → 재티칭 필요

### Phase 4 추가 후 흐름 (비파괴적 확장)
1. `Action_FAIMeasurement` 가 이미지 획득
2. **신규:** `DatumFindingService.FindDatum(image, bundleFAI.DatumConfig, out transform, out error)` 호출
3. 실패 시: 모든 하위 FAI NG 처리, 종료
4. 성공 시: 하위 FAI 루프 → **`TryMeasure(image, fai, transform)`** 호출 (transform 파라미터 추가)
5. `FAIEdgeMeasurementService` 내부에서 fai.ROI(픽셀) → transform 적용 → 보정된 픽셀 ROI → MeasurePos

### 기존 FAI ROI 저장 포맷 유지
- Phase 1~3에서 저장된 기존 레시피는 **그대로 로드·실행 가능**해야 한다.
- DatumConfig가 미설정/빈 상태면 transform = Identity (무보정) 로 fallback → 기존 Phase 3 동작과 동일.
- 이를 통해 Phase 4 롤아웃이 기존 작업을 깨지 않는다.

### ParamBase 직렬화 제약
- string, double, int, enum 만 직접 지원
- Line ROI 2개는 `"r1,c1,phi1,l1,l2;r2,c2,phi2,l1,l2"` 형식의 string 필드로 직렬화 (Phase 2의 Polygon 직렬화 패턴 차용)

### C# 7.2 제약 (Phase 1~3에서 이미 확인됨)
- nullable reference types, switch expressions, record 사용 불가
- Tuple 리턴은 `ValueTuple` 로 가능

### 빌드 커맨드
- `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64`

</code_context>

<specifics>
## Specific Ideas

### DatumConfig 필드 초안
```
class DatumConfig : ParamBase {
    // Line1 ROI (X축 기준선) — string 직렬화 필드로 풀어서 저장
    double Line1_Row, Line1_Col, Line1_Phi, Line1_Length1, Line1_Length2

    // Line2 ROI (Y축 기준선)
    double Line2_Row, Line2_Col, Line2_Phi, Line2_Length1, Line2_Length2

    // 티칭 시 저장되는 기준값 (픽셀)
    double RefOriginRow, RefOriginCol
    double RefAngleRad

    // Line 검출 파라미터
    int    EdgeThreshold
    string EdgePolarity                    // "positive" / "negative" / "all"

    bool   IsConfigured                    // 기준 교점·각도가 저장되었는지

    // 런타임 휘발성 (직렬화 제외)
    [NonSerialized] HomMat2D CurrentTransform
    [NonSerialized] bool     LastFindSucceeded
}
```

### DatumFindingService 인터페이스 초안
```
class DatumFindingService {
    bool TryFindDatum(HImage image, DatumConfig config,
                      out HomMat2D transform, out string error)
    // 내부:
    // 1. find_line(Line1_ROI) → L1
    // 2. find_line(Line2_ROI) → L2
    // 3. intersection_ll(L1, L2) → (curRow, curCol)
    // 4. angle(L1) → curAngle
    // 5. dRow = curRow - RefOriginRow, dCol = curCol - RefOriginCol
    // 6. dAngle = curAngle - RefAngleRad
    // 7. transform = translate(dRow, dCol) ∘ rotate(dAngle around (curRow, curCol))
}
```

### ROI 보정 유틸 초안
```
// FAIEdgeMeasurementService.TryMeasure 내부 또는 헬퍼
RoiDefinition ApplyTransform(RoiDefinition roi, HomMat2D transform) {
    // roi.Row, roi.Col → affine_trans_point → new Row, Col
    // roi.Phi → + rotation part of transform
    // Length1/Length2는 변경 없음
}
```

### 미구성 상태 처리
- `DatumConfig.IsConfigured == false` 인 경우 `CurrentTransform = Identity` 로 간주 → 무보정 pass-through
- 이를 통해 기존 레시피는 Phase 4 롤아웃 후에도 그대로 동작

</specifics>

<deferred>
## Deferred Ideas

- 3D/Laser용 Datum (평면 기반, plane fitting) — 3D 범위 외
- 원형/3점/다각형 기준의 Datum 패턴 — 제품 교체 시 필요하면 추상화
- Datum 실패 시 재촬영/재시도 전략
- Datum 티칭 wizard UI (현재는 순차 클릭으로 처리)
- Datum 결과 시각화 오버레이 (기준 교점 + X축 화살표) — 편의 기능
- 복수 Datum 전환 (제품별 Datum 프리셋) — 필요 시 별도 Phase
- Pre-Datum과 Datum A/B를 2단계로 분리 저장하는 방식 (PDF 원문 구조) — 정확도 부족 시 재검토
- FAI ROI를 mm 상대좌표로 전환하는 마이그레이션 — PDF 값 직접 입력이 필요해지면 별도 Phase로

</deferred>

---

*Phase: 04-datum*
*Context gathered: 2026-04-09*
