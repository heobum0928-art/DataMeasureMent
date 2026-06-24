# Phase 60: Calibration — Bottom (C) - Context

**Gathered:** 2026-06-24
**Status:** Ready for planning
**Mode:** `--auto` (사용자 외부 — Claude 권장 설계 + 근거 문서화. UAT 는 검증 직전 정지.)

<domain>
## Phase Boundary

Bottom Align 전용 **피커 센터 캘리브레이션** (서비스 계층, UI 없음). 피커가 지그를 픽업한 채 **10°씩 36스텝(360°) 회전** → 각 스텝의 자재 중심이 **편심원**을 그림 → **최소자승 원 피팅** → 원 중심 = 피커의 실제 회전 중심. 이 값을 저장하고 Bottom 정렬 보정에 반영한다.

**🔻 범위 축소 (사용자 결정 "Calibration 하나로만" 2026-06-24):** **AV-06(비전각↔피커각 선형 각도 캘) 폐기.** Phase 59 의 2-패턴 `angle_lx` 가 각도를 정확히 산출하므로 별도 각도 캘 불필요. **Phase 60 = AV-05(피커센터) 하나만.** (REQUIREMENTS 의 AV-06 은 phase 완료 시 "dropped — superseded by Phase 59 2-pattern angle_lx" 로 갱신.)

**범위 밖:** ROI 드로잉/캘 버튼/36스텝 트리거 UI = Phase 61. `$RESULT` TCP = Phase 62. 실제 피커 회전은 외부 하드웨어(PLC) — 비전은 스텝마다 grab + 중심 측정만.

**핵심 제약 (전 phase 공통):** 기존 Grabber 코드 무수정(추가만) · 헝가리언 · C# 7.2 · 모든 HALCON try-catch + finally dispose · 함수 30줄 · 매직넘버 const · 실패 격리(Grabber 무영향).
</domain>

<decisions>
## Implementation Decisions

### 서비스 구조 (D-01)
- **D-01:** 신규 `PickerCenterCalibrationService`(`Custom/EthernetVision/`), `EthernetVisionHandler` 가 소유(Matcher 옆 `PickerCal` 프로퍼티). **상태형 누적** API:
  - `void Reset()` — 누적 초기화.
  - `bool TryAddStep(HImage img, out string error)` — 한 스텝 이미지에서 자재 중심 측정 → 누적(36회 호출).
  - `bool TryComputePickerCenter(out double row, out double col, out double radius, out string error)` — 누적 점들로 원 피팅 → 피커센터 산출 + INI 저장.
  - 외부(Phase 61 UI / PLC 트리거)가 스텝마다 호출. 비전은 회전을 제어하지 않음.

### 스텝별 Cal 지그 중심 (D-02 — 사용자 정정 2026-06-24)
- **D-02:** 스텝별 중심 = **전용 Cal 지그의 원형 피처를 `fit_circle_contour_xld` 로 피팅한 중심** (2-패턴 shape-match 아님). 캘은 전용 Cal 지그(깨끗한 원)를 사용 — 생산부품(이형)의 2-패턴과 무관. 구현: 이미지(또는 검색 ROI)에서 에지 추출(`edges_sub_pix` 또는 threshold→경계) → 원 컨투어 선택 → `HOperatorSet.FitCircleContourXld(...)` → 지그 중심(row,col). `VisionAlgorithmService.TryFindCircle`(에지→FitCircleContourXld→중심) 패턴 참조(독립 구현, VisionAlgorithmService **무수정**). 지그가 회전하며 이동하므로 검색 영역은 지그 sweep 을 충분히 커버(또는 전체 이미지).
  - **→ `AlignShapeMatchService.TryFindCenter` 추가 불필요(폐기). Phase 59 의존 제거 = 캘은 완전 독립.**
  - `fit_circle_contour_xld` 가 **두 번** 사용: ① 스텝별 지그 원 → 지그 중심, ② 36 지그 중심(편심 궤적) → 피커 중심(D-03).

### 원 피팅 (D-03)
- **D-03:** **`FitCircleContourXld`("atukey" 강건 최소자승)** 재사용(`VisionAlgorithmService.cs` 검증 패턴). 36 (row,col) → `GenContourPolygonXld` → fit → center(row,col)+radius. try-catch + HObject/HTuple finally dispose. 가드: 최소 점 수(예: ≥ MIN_STEPS const, 권장 36) 미달/반경 비정상 시 false.

### 저장 (D-04)
- **D-04:** 피커센터를 `SystemSetting` `[ETHERNET_VISION]` 에 `PickerCenterRow`/`PickerCenterCol`(double) 신규 프로퍼티로 저장(머신 단위 HW 캘 → 레시피 아닌 시스템 설정). Phase 58 AfterLoad 패턴 — 기본값 0(미캘 상태). reflection Load 자동 처리. (이형 부품이므로 추가로 RefMidRow/Col 등 필요 시 사이드카 json 고려하나 우선 INI 2 스칼라.)

### 보정 적용 (D-05)
- **D-05:** Bottom 정렬 결과를 **피커센터 기준 강체 변환**으로 표현 — 부품이 피커센터를 중심으로 dθ 회전하므로, 피커에 내릴 보정(dx,dy,dθ)은 피커센터를 회전중심으로 합성. `HomMat2dRotate`(dθ, pickerRow, pickerCol) + 잔여 translation, 또는 `vector_angle_to_rigid`. **부호/회전중심 규약은 피커 컨트롤러 기준이라 UAT/통합에서 확정**(phase 59 부호/축 플래그와 동일). 피커센터 미캘(0,0) 시 = 이미지/midpoint 기준 폴백(현 phase 59 동작 유지).

### 실패 격리 / UAT (D-06)
- **D-06:** 전 public 메서드 try-catch → false/never throw(Grabber 무영향). UAT 는 **실 피커 + 36 회전 이미지**가 있어야 가능 → Phase 58/59 처럼 **검증 직전 정지, Phase 61 UI 후 일괄**. SIMUL 단독으론 36-스텝 회전 시퀀스 부재.

### Claude's Discretion
- MIN_STEPS/반경 가드 const 값, TryFindCenter 시그니처 세부, PickerCal 프로퍼티 명, 보정 적용의 정확한 HomMat2d 합성 순서(UAT 확정 전 기본 구현).

</decisions>

<canonical_refs>
## Canonical References

### v1.3 요구사항 / 로드맵
- `.planning/ROADMAP.md` §"Phase 60: Calibration — Bottom (C)" — Goal/SC (단 AV-06 폐기 반영).
- `.planning/REQUIREMENTS.md` line 110~111 (AV-05 유효, **AV-06 폐기**).
- `.planning/phases/59-vision-algorithm-b-2026-06-23/59-CONTEXT.md` `<revision>` — 2-패턴 angle_lx(각도 정확 → 각도 캘 불필요 근거).

### 재사용 코드
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` — `FitCircleContourXld`("atukey", L~310/499/744) + `GenContourPolygonXld`. **D-03 원 피팅 패턴.** 읽기 전용 참조(수정 금지 — Grabber 공유 가능).
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` `TryFindCircle`(L256~336) — 스텝별 Cal 지그 중심 추출(에지→FitCircleContourXld) 패턴. 무수정 참조.
- `WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs` — `PickerCal` 프로퍼티 추가(Matcher 패턴).
- `WPF_Example/Custom/SystemSetting.cs` `[ETHERNET_VISION]` + AfterLoad — PickerCenterRow/Col 추가(Phase 58 패턴).
- `WPF_Example/Custom/EthernetVision/EthernetAlignCamera.cs` `Grab()` — 스텝별 이미지 소스(외부 회전).
- (참조) `D:\Project\NewDDA\...\Action_BottomCalibration.cs` — per-picker 중심 저장 패턴(편심원 아님, 충돌 없음).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `FitCircleContourXld`("atukey") + `GenContourPolygonXld`: 36점 강건 원 피팅, 즉시 재사용.
- `VisionAlgorithmService.TryFindCircle`(에지→FitCircleContourXld→중심): **스텝별 Cal 지그 중심** 추출 패턴(독립 구현). AlignShapeMatchService(2-패턴)는 캘에 사용 안 함.
- SystemSetting [ETHERNET_VISION] INI + AfterLoad: 스칼라 캘 결과 저장(Phase 58).
- EthernetAlignCamera.Grab(): 스텝 이미지.
- MathNet.Numerics v5.0.0 available(필요 시 대체 피팅), 단 HALCON atukey 우선.

### Established Patterns
- 싱글턴 handler 가 서브서비스 소유(Matcher → PickerCal).
- HALCON try-catch + finally dispose.

### Integration Points
- `EthernetVisionHandler.PickerCal` 프로퍼티 + lazy 생성.
- 스텝별 Cal 지그 중심 = PickerCenterCalibrationService 내부 원 검출(FitCircleContourXld). AlignShapeMatchService 편집 불필요.
- `SystemSetting` PickerCenterRow/Col + AfterLoad 기본값.
- 신규 파일: `PickerCenterCalibrationService.cs` + csproj.

</code_context>

<specifics>
## Specific Ideas

- "기존 패턴 확장": 신규 알고리즘 최소 — HALCON 원 피팅 + phase 59 2-패턴 중심 재사용, 신규는 36-스텝 누적 + 피커센터 산출/저장/적용.
- AV-06(각도 캘) 폐기는 phase 60 핵심 결정 — 2-패턴 angle_lx 가 대체.
- 보정 적용(D-05) 부호/회전중심 규약은 피커 컨트롤러 기준 UAT 확정.

</specifics>

<deferred>
## Deferred Ideas
- 36-스텝 트리거 + 캘 버튼 + 결과 표시 UI(TabControl Bottom 탭) → Phase 61.
- `$RESULT` TCP → Phase 62.
- AV-06 각도 캘 → **폐기**(2-패턴 angle_lx 로 충족, 재도입 안 함).
</deferred>

---

*Phase: 60-calibration-bottom-c-2026-06-24*
*Context gathered: 2026-06-24 (--auto, Claude-decided, AV-05 only)*
