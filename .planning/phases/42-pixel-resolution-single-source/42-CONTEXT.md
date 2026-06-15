# Phase 42: 픽셀분해능 런타임 단일소스 (CO-38-01) - Context

**Gathered:** 2026-06-15
**Status:** Ready for planning

<domain>
## Phase Boundary

산재된 mm/pixel 픽셀분해능 값을 **카메라(Shot) 단일 소스**로 통합한다. Phase 38 Test 5(#5 픽셀분해능 단일소스 UI)의 carry-over(CO-38-01)로, 38-01 이 구현한 "로딩 시 정규화"(D-10) 범위를 넘어 **런타임 단일소스 + 항목별 UI 정리 + 측정 경로 단일 소스**까지 달성한다.

**달성 목표(성공기준, ROADMAP):**
1. Shot 단일값 편집 시 재시작 없이 전체 FAI 반영
2. PropertyGrid 항목별 노출 정리
3. 측정 경로 단일 소스

**In scope:** 측정 소비 경로 단일소스화(Rewire) · 항목별 PropertyGrid 노출 정리 · EdgePair 측정 정합.
**Out of scope:** X/Y 분리 해상도 비정방형 지원(D-09 X=Y 확정) · INI 포맷 키 변경/마이그레이션 · 정식 Gage R&R · 측정 알고리즘 로직 변경.

</domain>

<carried_forward>
## Phase 38에서 이월(확정) — 재논의 없음

- **D-08 (Phase 38):** 단일 소스 = 카메라별 단일값(Top/Bottom/Side).
- **D-09 (Phase 38):** X/Y 분리 해상도는 단일값으로 통합 — 정방형 픽셀 가정(X=Y).
- **D-10 (Phase 38):** 로딩 시 카메라 단일값으로 덮어쓰기 정규화 — 이미 `InspectionRecipeManager.cs:328-334` 구현됨(CAM 섹션 있을 때만).
- 프로젝트 제약: 기존 INI 레시피 하위 호환 유지.

</carried_forward>

<decisions>
## Implementation Decisions

### 단일소스 전략 (핵심)
- **D-01:** **(B) 소비지점 Rewire 채택.** `Action_FAIMeasurement` (`Action_FAIMeasurement.cs:269,284`) 가 측정에 넘기는 pixelResolution 파라미터를 `fai.PixelResolutionX` 대신 **`shot.PixelResolution`(= `fai.Owner as ShotConfig`)** 으로 변경한다. 대부분 측정이 이미 이 파라미터를 소비하므로(ArcEdge/ArcLineIntersect/CircleCenter/CircleDiameter/Compound*/DualImage/EdgeToLine/LineToLine ✓) 측정 코드 무변경으로 물리적 단일소스가 달성된다. `fai.PixelResolutionX/Y` 는 INI 호환용 잔존 저장으로만 남기고 소비하지 않는다.

### Shot 편집 진입점 / 라이브 반영
- **D-02:** 카메라 단일값 편집 진입점 = **Shot 노드 PropertyGrid `PixelResolution` 필드(이미 노출, `CameraSlaveParam.cs:25` `[Category("General|AOI")]`) + 기존 2점 캘리브레이션 액션(`MainView.xaml.cs:2187` ApplyCalibrationResult)** 둘 다. 양쪽 모두 `shot.PixelResolution` 으로 수렴한다.
- **D-03:** 라이브 반영 = **다음 검사부터 자동 반영.** (B) 구조상 검사 시점에 `shot.PixelResolution` 을 직접 읽으므로 별도 cascade/트리거 구현 불필요 — "재시작 없이 반영" 성공기준이 구조적으로 충족된다. 편집 즉시 현재 표시된 결과/오버레이 재계산은 하지 않는다.

### PropertyGrid 정리
- **D-04:** 항목별 PixelResolution 은 **완전 숨김(`Browsable(false)`).** 소비되지 않는 잔존값이므로 운영자 노출 불필요.
- **D-05:** 정리 대상 = **`FAIConfig.PixelResolutionX/Y` (`FAIConfig.cs:78-80`) + `EdgePairDistanceMeasurement.PixelResolutionX/Y` (`EdgePairDistanceMeasurement.cs:38-40`)**. `ShotConfig.PixelResolution` 은 유일한 편집 소스로 노출 유지.

### EdgePair 정합 + X/Y 통합
- **D-06:** **`EdgePairDistanceMeasurement` 재배선** — `TryExecute` 의 temp FAIConfig 구성 시 `self.PixelResolutionX/Y`(`EdgePairDistanceMeasurement.cs:86-87`) 대신 **넘어온 `pixelResolution` 파라미터** 사용. 전 측정이 동일하게 shot 단일소스 소비.
- **D-07:** X=Y 정방형 가정(D-09) 처리 = **물리 X/Y 필드 유지(INI 키 호환) + 소비 안 함.** (B) 구조상 X/Y 값이 측정에 소비되지 않으므로 X≠Y 여도 무해. 로딩 정규화가 이미 X=Y 처리. **INI 키 변경/마이그레이션 코드 불필요.**

### Claude's Discretion
- 숨김 구현 방식: `[Browsable(false)]` 어트리뷰트 직접 부여 vs ICustomTypeDescriptor 필터(Phase 38 D-12 IsHiddenForAlgorithm/GetProperties 패턴) — executor 코드 확인 후 결정.
- `shot.PixelResolution` 접근 방식(Owner 체인 walk: meas→fai→shot vs Action 보유 ShotConfig 직접 참조) — 실코드 확인 후 planner/executor 재량.
- 로딩 정규화(`InspectionRecipeManager.cs:328-334`)의 measurement-level 값 정규화 확장 필요 여부 — (B) 하에서 measurement 자체 값 미소비라 영향 적으나 검토.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 38 carry-over 출처 (필독)
- `.planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-CONTEXT.md` §#5 — D-08~D-11 (단일소스 정의/정규화 결정)
- `.planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-UAT.md` — Test 5 carry-over 판정 근거 + CO-38-01 Gap 스펙(범위 ①②③)

### 코드 — 단일소스 분배/소비 경로
- `WPF_Example/Sequence/Param/CameraSlaveParam.cs:22-25` — `ShotConfig.PixelResolution` 정규 소스 (`[Category("General|AOI")]`)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:269,284` — pixelResolution 파라미터 전달부 (D-01 1차 변경점)
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs:76-80` — `PixelResolutionX/Y` 잔존 필드 (`[Category("Calibration")]`, D-04/D-05 숨김 대상)
- `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgePairDistanceMeasurement.cs:38-40,72-89` — 자체 PixelResolution 필드 + temp FAIConfig 구성 (D-06 재배선 대상)
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs:325-334` — 로딩 시 fai = camRes 정규화 (D-10 구현, 유지)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs:2187-2208` — ApplyCalibrationResult cascade (D-02 편집 진입점)
- `WPF_Example/Halcon/Models/RoiDefinition.cs:86-89` — RoiDefinition.PixelResolutionX/Y (소비 지점 정합 검토 대상, CO-38-01 범위 ③)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **pixelResolution 파라미터 계약**: 거의 모든 `IMeasurement.TryExecute(image, transform, pixelResolution, ...)` 가 이미 파라미터를 소비 → D-01 Rewire 가 측정 코드 무변경으로 가능.
- **로딩 정규화(InspectionRecipeManager.cs:328-334)**: D-10 이미 구현 — Phase 42 는 런타임/UI 분만 추가.
- **PropertyGrid 숨김 패턴**: Phase 38 D-12 의 ICustomTypeDescriptor IsHiddenForAlgorithm/GetProperties 필터(DatumConfig) 가 선례.

### Established Patterns
- ShotConfig extends CameraSlaveParam → Shot 자체가 카메라 객체(단일 소스 보유). FAIConfig.Owner == ShotConfig.
- 측정 객체(Measurement)는 FAIConfig.Owner 체인을 통해 Shot 접근 가능 (EdgePair 의 `Owner as FAIConfig` 선례).

### Integration Points
- D-01 변경점: `Action_FAIMeasurement.cs:269,284` 단일 호출부 (회귀 위험 최소).
- D-06 변경점: `EdgePairDistanceMeasurement.cs:86-87` temp 구성부.
- D-04/D-05: FAIConfig + EdgePair PropertyGrid 어트리뷰트/필터.

</code_context>

<specifics>
## Specific Ideas

- "측정 경로 단일 소스" 성공기준을 (B) Rewire 로 직접 충족 — fai 복사본은 INI 라운드트립용으로만 잔존, 소비 0.
- 회귀 검증 핵심: Rewire 전후 동일 레시피의 측정 mm 값 회귀 0 (정상 레시피는 로딩 정규화로 fai==shot 이라 값 불변이 기대됨). 값 차이 발생 시 의도적 보정으로 문서화(Phase 38 성공기준 #3 연속선).

</specifics>

<deferred>
## Deferred Ideas

- X/Y 비정방형(X≠Y) 실지원 — D-09 가 X=Y 로 확정. 비정방형 카메라 도입 시 별도 phase.
- INI 포맷 키를 단일 `PixelResolution` 으로 정리(물리 X/Y 제거) — D-07 에서 호환 위해 보류. 차기 포맷 버전(v7) 작업 시 함께.

</deferred>

---

*Phase: 42-pixel-resolution-single-source*
*Context gathered: 2026-06-15*
