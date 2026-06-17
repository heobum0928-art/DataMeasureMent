# Phase 52: 이미지 수평 보정 (Datum 에지 기반 회전 정렬) - Context

**Gathered:** 2026-06-17
**Status:** Ready for planning

<domain>
## Phase Boundary

Datum 수평 에지의 각도차(수평선=0° 대비)를 산출해 입력 이미지를 회전 정렬(레벨링)한 뒤
측정한다. 사용자 제공 HDevelop 참조 파이프라인(수평 2-ROI Union → `fit_line_contour_xld` →
`get_contour_xld` → `gen_contour_polygon_xld`(LongLine) → `fit_line_contour_xld` →
`angle_lx` → 이미지 회전)을 기존 `DatumFindingService` 수평 2-ROI concat 피팅 경로와
동일하게 구현한다.

**In scope:** Datum 수평 에지 각도 산출 + 이미지 회전 보정 적용 + 적용 시점/범위/토글.
**Out of scope:** 픽셀 캘리브레이션(Phase 53), 신규 측정 알고리즘, 수직 에지 기반 보정.
</domain>

<decisions>
## Implementation Decisions

### 레벨링 기준 Datum/에지
- **D-01:** 회전 각도는 **레시피에서 시퀀스당 명시 지정한 "레벨링 기준" Datum 1개**의
  수평 에지에서 산출한다. 그 Datum 의 수평 2-ROI concat 피팅 라인(기존
  `DatumFindingService` 수평 피팅 재사용) + `angle_lx`(수평선 대비) 로 각도를 구한다.
  시퀀스에 수평 Datum 이 여러 개(예: Side=4)여도 지정된 1개만 기준 — 결정적·정확
  (잘못된 Datum 으로 레벨링되는 위험 제거). 지정 플래그는 시퀀스 레벨링 토글과 같은 자리에 둔다.

### 적용 시점 (WHEN)
- **D-02:** **grab 직후 전처리 회전.** 레벨링 각도 산출 → 입력 이미지를 **실제 회전** →
  회전된 이미지로 Datum 검출 + 전 FAI 측정 전체를 진행한다. 다운스트림(Datum/측정)이
  모두 수평 정렬된 단일 이미지로 동작해 일관성 확보. (좌표만 보정하는 방식은 채택 안 함)

### 적용 범위 (SCOPE)
- **D-03:** **시퀀스당 1회 산출 → 그 시퀀스 전 SHOT 공유.** 카메라/지그 고정 기울기는
  시퀀스 공통이라는 전제. SHOT마다 개별 산출은 채택 안 함.

### on/off 토글 + 영속
- **D-04:** **시퀀스 단위 토글 + 레시피(INI) 저장, 기본 off.** 기본 off 로 기존 레시피
  회귀 0(INI 미존재 시 폴백 off). 시퀀스마다 독립적으로 켜고 끈다.

### Claude's Discretion (구현 재량 — researcher/planner 결정)
- 회전 구현(HALCON `rotate_image` vs `affine_trans_image` + `hom_mat2d` / `vector_angle_to_rigid`)
- 회전 중심점 + 경계 처리(크롭 vs 'false'/'constant' 배경 확장 — 측정 ROI 잘림 방지)
- 레벨링 각도 산출용 사전 검출 패스 구현(기준 Datum 의 수평 ROI 만 raw 이미지에서 1차 검출
  → 회전 → 본 검사 재검출). 회전 후 taught ROI 정합 검증.
- `angle_lx` 부호/회전 방향 규약(시계/반시계) 확정.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 참조 알고리즘 (사용자 제공)
- 사용자 제공 HDevelop 스크립트 — UnionContours 2개 `fit_line_contour_xld` →
  `get_contour_xld` → `gen_contour_polygon_xld`(LongLine) → `fit_line_contour_xld` →
  `angle_lx` → 이미지 회전. (파일 경로 없음 — 사용자 보유. plan 단계에서 필요 시 요청)

### 코드 (재사용 기준)
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — 수평 2-ROI concat 피팅
  (`TryFindVerticalTwoHorizontal` 등, `FitLineContourXld` + `hom_mat2d`). 레벨링 라인 산출 재사용 기준.
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` — affine/rotate 계열 유틸 보유.
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — 측정 진입(`image` HImage). 전처리 회전 삽입 지점 후보.
- `.planning/ROADMAP.md` §"Phase 52" — Goal/Scope/Success Criteria(UAT).
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `DatumFindingService` 수평 2-ROI concat 라인 피팅 — 레벨링 기준 라인/각도 산출에 그대로 재사용(신규 ROI 0).
- `VisionAlgorithmService` 의 affine/rotate 유틸 — 이미지 회전 적용.

### Established Patterns
- 측정/Datum 은 `Action_FAIMeasurement` 가 grab/로드된 `HImage`(`image`)를 받아 처리 — 전처리 회전을 이 진입 직전에 삽입.
- 옵션 플래그는 public 프로퍼티 + INI 직렬화(ParamBase reflection / DatumConfig·시퀀스 파라미터) 패턴. 기본값 false 로 회귀 0 (InvertSign/UseSupplementaryAngle 선례).

### Integration Points
- grab 경로(`DeviceHandler.GrabHalconImage` / `ShotConfig` 이미지) → 측정 진입 사이에 레벨링 전처리.
- 시퀀스 레벨링 토글 + 기준 Datum 지정 영속 위치(시퀀스 파라미터 or DatumConfig 플래그) — planner 확정.
</code_context>

<specifics>
## Specific Ideas

- 사용자 제공 HDevelop 참조가 구현의 정답 경로 — `angle_lx` 기반 각도 산출 + 이미지 회전.
- 기준 Datum 수평 라인 = 기존 DatumFindingService 수평 피팅과 동일 파이프라인(중복 구현 금지).
</specifics>

<deferred>
## Deferred Ideas

- 픽셀 캘리브레이션(체커보드) — Phase 53(별도, POC 신규 #2).
- SHOT별 개별 레벨링 / 전 Datum 평균 레벨링 — 현 SCOPE(시퀀스 1회·기준 1개)에서 제외. 필요 시 후속.
- 수직 에지/2축 기반 보정 — 범위 외.
</deferred>

---

*Phase: 52-datum-2026-06-16-poc-1*
*Context gathered: 2026-06-17*
