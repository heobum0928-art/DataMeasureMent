# Phase 54: Datum 패턴매칭 위치보정 (ALIGN-01) - Context

**Gathered:** 2026-06-18
**Status:** Ready for planning

<domain>
## Phase Boundary

자재가 X,Y(+tilt)로 틀어져 들어와도 작은 측정 ROI 가 대상 에지를 벗어나지 않도록, **Datum 에 패턴매칭**을 두어 자재 위치를 찾고 측정/Datum ROI **좌표**를 보정한다. 측정은 **원본 픽셀**에서 수행(이미지 warp 금지). Phase 52(레벨링, LEVEL-01)를 **흡수·대체** — 레벨링의 이미지 회전(`RotateImageByAngle`)은 폐기, line-fit 각도산출(`TryGetLevelingAngle`)만 정밀 θ 소스로 재사용.

**In scope:** Top/Bottom + Side **단일 이미지** Datum. 패턴매칭(x,y) + line-fit(정밀 θ) 하이브리드 → rigid transform → ROI 좌표변환.
**Out of scope:** Side DualImage(2-image) 패턴매칭 → 후속 phase. 비강체(부위별 휨) 보정. 이미지 warp 측정.

</domain>

<decisions>
## Implementation Decisions

### 매칭 엔진 (D-01)
- **D-01:** per-Datum 엔진 선택형 — `DatumConfig.PatternEngine` (string, "Shape" | "NCC", 기본 "Shape"). 기존 `AlgorithmType` 드롭다운(ICustomTypeDescriptor + ItemsSourceProperty) 패턴 미러.
- **D-01a:** Shape(`create_shape_model`/`find_shape_model`) = 회전/조명/클러터 강, defocus 약. NCC(`create_ncc_model`/`find_ncc_model`) = defocus 강, 회전 약. 포커싱 불량 부위 Datum = NCC, tilt 큰 부위 = Shape.
- **D-01b:** 매칭은 **coarse x,y 전용** (정밀 θ는 line-fit). ∴ NCC 회전 약점은 작은 angle range(AngleExtent)로 완화 — 매칭이 각도 정밀도를 책임지지 않음.

### 보정 산출/적용 (이미 ROADMAP lock — 재확인)
- **D-02:** 하이브리드 — 매칭=x,y / line-fit=정밀 θ. 흐름(Datum당): ① 매칭(원본 grab 이미지)→x,y ② x,y 로 그 Datum line-fit ROI 이동→이동 엣지 line-fit→정밀 θ ③ (x,y+θ) rigid transform ④ `_datumTransforms[DatumName]` 에 `hom_mat2d_compose` 합성 → `DatumRef` 측정 자동 적용.
- **D-03:** 적용 = **ROI 좌표변환**(`affine_trans_pixel`/`AffineTransPoint2d`), 이미지 warp 아님. 측정은 원본 픽셀. Measure 본문·라우팅 무수정 (`meas.DatumRef` → `TryGetDatumTransform` 기존 채널).
- **D-04:** 매칭 주기 = **Datum 당 1회** (Top/Bottom=1, Side=4). per-Datum 국소 강체 가정(글로벌 아님). 적용 시점 = EStep `Level`(또는 DatumPhase) 확장, 시퀀스당 datum 루프.
- **D-05:** 매칭 입력 = **보정 전 원본 grab 이미지** (이중보정 가드). 레벨링 이미지회전 폐기로 warp 0회.

### 검색 영역 / 속도 (D-06)
- **D-06:** 검색 영역 = **template ROI ± 예상 최대 변위 margin** (전체 이미지 아님). margin = 파라미터 `PatternSearchMarginPx`(또는 비율, per-Datum). `reduce_domain` 으로 검색 영역 제한.
- **D-06a:** **coarse 매칭은 다운샘플에서** — 1/2~1/4 해상도(피라미드 상위 레벨 / `zoom_image_factor`, 파라미터화)에서 매칭하여 x,y 획득 → 스케일 복원. 152MP(VIEWORKS, Phase 41) 등 고해상도 tact 대응. 정밀 θ·측정은 원본. 추가 가속: `NumLevels` 충분, `Greediness`↑, `MinScore`(`PatternMinScore`)↑.

### 모델 파일 영속 (D-07)
- **D-07:** **이름 기반 결정적 경로 재계산** — 절대경로 저장 안 함. (recipeName/seqName/datumName/engine확장자)로 매 로드·저장 시 경로 계산(`RecipeFiles.GetModelFilePath` 패턴). 레시피 Copy/Rename/Delete 자동 정합 → stale 0. DatumConfig 에는 경로 미저장, `IsPatternAlignEnabled`+`PatternEngine`+pose/score 파라미터만.
- **D-07a:** 저장 위치 = 레시피 폴더 하위(`RecipeSavePath/recipe/seq/...`) — `RecipeFiles.Copy`(CopyFilesRecursively)/`Delete`(폴더째) 가 모델 파일 자동 동반. ROADMAP "높음 리스크(백업 누락)" 해소.
- **D-07b:** 확장자 = HALCON **`.shm`**(shape, `write_shape_model`/`read_shape_model`) / **`.ncm`**(ncc, `write_ncc_model`/`read_ncc_model`) 신규 상수. `EXTENSION_MODEL=".mmf"` 는 MIL(Alligator 레거시)이라 **미재사용**.

### 패턴 티칭 UI (D-08)
- **D-08:** **최소한 완결형** (1차 포함 필수 — 없으면 모델 생성·SIMUL 검증 불가, Phase 52 전철). Datum 노드에서 패턴 ROI(Rect) 그리기 + [모델 생성/저장] 버튼 + ref pose 자동 기록. 기존 Datum 티칭 UX(ECanvasMode.TeachDatum / Rect ROI write-back) 재사용. 검색영역 별도 그리기·score 미리보기·재티칭 미리보기는 후속.

### ref pose 기록 (D-09)
- **D-09:** **티칭 시 find 결과 pose 기록** — 티칭 이미지에서 한 번 find 돌려 그 pose(`RefMatchRow`/`RefMatchCol`/`RefMatchAngleDeg`) 저장. 런타임 pose − ref pose = 변위. 런타임과 동일 연산이라 부호/좌표계 일관성 보장(template ROI 중심 사용은 부호 불일치 위험 → 기각).

### 실패 정책 (이미 ROADMAP lock — 재확인)
- **D-10:** lenient — 매칭 score<MinScore / 모델 로드 실패 시 시퀀스 진행(abort 없음) + 해당 Datum 측정 NG 강제. 기존 `MarkDatumFailed(DatumName)` 재사용, `LastSkipReason="ALIGN_FAIL"`, `LastJudgement=false`. 가짜 숫자 안 넣고 값 클리어+NG+사유 → 양품 오판 0.

### off 회귀 0 (이미 ROADMAP lock — 재확인)
- **D-11:** `IsPatternAlignEnabled` 기본 false + INI 키 미존재 폴백 false(`EnsurePerRoiDefaults`). enabled=false → align=identity → `_datumTransforms` 무변경 → 기존 측정 byte-identical.

### Claude's Discretion
- `vector_angle_to_rigid` vs 수동 translate+rotate 합성 — rigid 행렬 구성 세부는 planner/executor 재량(분석문서 §3 권고 = vector_angle_to_rigid).
- 다운샘플 비율 기본값(1/2 vs 1/4), AngleExtent 기본값, NumLevels — 연구/플랜에서 SIMUL 튜닝.
- `PatternMatchService` 메서드 시그니처/내부 구조 — try/catch 규약(HOperatorSet 래핑) 준수 하에 재량.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 설계 근거
- `.planning/ALIGN-01-pattern-align-analysis.md` — ALIGN-01 전체 설계 분석. §8 하이브리드(무 warp / Shape=x,y + line-fit=θ), §9 Datum단위 매칭(`_datumTransforms` 채널), §3 엔진/산출(`vector_angle_to_rigid`), §4 재사용 vs 신규.

### 보정 채널 / 측정 통합 (재사용 인프라)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — `_datumTransforms`(L56, DatumName 키), `TryGetDatumTransform`(L400), `TryRunSingleDatum`/`ClearDatumTransforms`(누적/리셋), `MarkDatumFailed`/`IsDatumFailed`(L362~372), 레벨링 캐시 lifecycle(`_levelingAngleRad`/`_levelingComputed`), `TryComputeLevelingAngle`(L411).
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — EStep(Level/DatumPhase/Grab/Measure), 측정 시 transform 적용(L295 TryGetDatumTransform → L347/362 TryExecute), datum 실패 게이트(L280~292, ClearResult+ALIGN_FAIL 패턴 위치), DatumConfig 매핑(L307~334).
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — line-fit(수평 2-ROI concat), `TryGetLevelingAngle`(θ 소스 재사용), hom_mat2d 보정 빌드(L186~).
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` — `AffineTransPoint2d`(L45~60, ROI 좌표변환), `RotateImageByAngle`(레벨링 — 폐기 대상).

### 모델 파일 영속 (재사용 인프라)
- `WPF_Example/Utility/RecipeFileHelper.cs` — `GetModelFilePath`(L86, recipe/seq/act/property → 경로 계산, 디렉터리 생성), `Copy`/`CopyFilesRecursively`(L122~149, 폴더 재귀 복사), `Delete`(L113), `GetRecipeFilePath`/`RecipeSavePath`.
- `WPF_Example/Custom/Device/DeviceHandler.cs` — `EXTENSION_MODEL=".mmf"`(L83, MIL — 미재사용), `FILTER_MODEL`(L82). 신규 `.shm`/`.ncm` 상수 추가 위치.

### 영속/PropertyGrid 모델 (재사용 패턴)
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — ParamBase 직렬화(double/int/string/bool), ICustomTypeDescriptor 동적 hide(`IsHiddenForAlgorithm`/`BuildFilteredProperties`), `AlgorithmType` 드롭다운(ItemsSourceProperty) — PatternEngine 미러 대상. `IsLevelingReference`(L43), `EnsurePerRoiDefaults`(INI 폴백).
- `WPF_Example/UI/ViewModel/ModelFinderViewModel.cs` — `[InputFilePath(EXTENSION_MODEL, FILTER_MODEL)]` 패턴(미활성 골격, HALCON 미구현 — 참고만).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `_datumTransforms[DatumName]` + `TryGetDatumTransform` + `meas.DatumRef` 라우팅 — per-Datum transform 적용이 **추가 라우팅 0**으로 성립. align rigid 를 합성 저장만 하면 측정 자동 추종.
- `MarkDatumFailed`/`IsDatumFailed` + `LastSkipReason`/`LastJudgement` — 매칭 실패 NG 처리(ALIGN_FAIL) 그대로 재사용. 값 클리어+NG+사유 패턴(Action_FAIMeasurement L280~292) 존재.
- `RecipeFiles.GetModelFilePath` + `Copy`(재귀)/`Delete`(폴더째) — 모델 파일 영속·백업·삭제 자동 동반. 이름 기반 경로 재계산이 stale 0 보장.
- `DatumFindingService.TryGetLevelingAngle` line-fit — 정밀 θ 소스로 재사용(레벨링 이미지회전만 폐기).
- DatumConfig ICustomTypeDescriptor + AlgorithmType 드롭다운 — PatternEngine(Shape/NCC) 동적 노출 패턴 미러.
- Datum 티칭 UX(ECanvasMode.TeachDatum, Rect ROI write-back) — 패턴 ROI 그리기 재사용.

### Established Patterns
- HALCON 호출 try/catch 래핑(return false) 규약 — PatternMatchService 준수.
- ParamBase 직렬화 한계(double/int/string/bool) → 모델은 별도 파일, INI 엔 경로 미저장(재계산).
- per-ROI sentinel 0/"" + EnsurePerRoiDefaults idempotent 폴백 → off 회귀 0.

### Integration Points
- 신규 `PatternMatchService`(HALCON shape/ncc — 현 코드베이스 HALCON 매칭 전무, Wafer scan 은 MIL .mmf 라 비참조). create/read/write/find_{shape,ncc}_model + vector_angle_to_rigid → out HTuple rigid transform.
- EStep Level/DatumPhase 확장 — datum 루프에서 매칭 삽입(enabled 가드, 원본 이미지) → align rigid 를 `_datumTransforms[DatumName]` 합성.
- DatumConfig 신규 필드 + EnsurePerRoiDefaults 폴백 + ICustomTypeDescriptor hide.

</code_context>

<specifics>
## Specific Ideas

- 사용자 핵심 우려 2건이 설계를 형성함: (1) 전체 이미지 회전은 측정 정밀도/tact 손해 → ROI 좌표변환(무 warp). (2) 전체 이미지 find 는 152MP 에서 느림 → 검색영역 변위 margin + 다운샘플 coarse 매칭.
- 매칭 엔진은 포커싱 불량 가능성 때문에 Shape 고정이 아닌 per-Datum Shape/NCC 선택형(사용자 제기).

</specifics>

<deferred>
## Deferred Ideas

- Side DualImage(2-image) 패턴매칭 — 후속 phase (DualImage carry-over 격리).
- 비강체(부위별 휨) 보정 — 범위 밖(per-Datum 국소 강체까지만).
- 티칭 풀 기능(검색영역 별도 그리기, score 미리보기, 재티칭 미리보기) — 1차는 최소 완결형, 후속 보강.
- 실데이터(실카메라) 변형 페어 UAT — 1차는 SIMUL 합성 변형 페어(Phase 41.1 이미지 부족 전례). 이미지 확보 후.

</deferred>

---

*Phase: 54-datum-align-01-x-y-tilt*
*Context gathered: 2026-06-18*
