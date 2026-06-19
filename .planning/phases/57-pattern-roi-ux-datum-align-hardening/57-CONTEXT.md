# Phase 57: 패턴 ROI UX & Datum 정렬 보강 - Context

**Gathered:** 2026-06-19
**Status:** Ready for planning

<domain>
## Phase Boundary

패턴매칭 정렬(ALIGN, Phase 54/55/56)의 **후속 보강** — 새 기능이 아니라 기존 ALIGN 의 티칭 UX·시각화·견고성 다듬기.

이 phase 가 다루는 6항목:
1. 패턴 ROI1/ROI2 버튼 나란히 배치 + 2개 필수 안전장치
2. 패턴 ROI 표시/숨김 토글
3. Datum 시각화 색상 통일 (slate blue)
4. Side datum 4-ROI(DualImage) 정렬 보정
5. 매칭 실패 시 측정 진행(lenient) 정책 검증/보강
6. 미사용 leveling 잔재 완전 제거

**Out of scope:** 새 datum 알고리즘, 새 측정 항목, 패턴 엔진 추가, 위치보정 알고리즘 자체 변경(Phase 54/55 에서 확정).
</domain>

<decisions>
## Implementation Decisions

### #4 Side 4-ROI (DualImage) 정렬 — 단일 공유 transform
- **D-01:** Side `VerticalTwoHorizontalDualImage` datum 의 ALIGN 보정은 **단일 공유 transform 방식(A)** 채택. 가로축 이미지(`refImage`/`imageHorizontal`)에서만 패턴매칭으로 `(x, y, θ)` rigid transform 산출 → 가로축 검출 ROI **와** 세로축 검출 ROI(`imageVertical`) **모두에 동일 transform 적용**.
- **D-02:** 근거 = 텔레센트릭 렌즈 + Z축(포커스)만 이동하는 셋업. 텔레센트릭은 작동거리 변화에도 배율·측면위치 불변 → 가로/세로 이미지가 **같은 픽셀 좌표계**. 같은 좌표계 안에서 rigid transform 은 글로벌하게 유효하므로 세로 ROI 에 직접 적용해도 수학적으로 정확. **세로 이미지 별도 패턴 불필요(4개 칠 필요 없음)** — seed 의 "세로 분리매칭"은 불필요한 과설계로 폐기.
- **D-03:** 현재 DualImage datum 은 ALIGN 이 **아예 미적용**(검출-only 폴백) 상태 — `Action_FAIMeasurement.cs:147` 의 deferred 게이트를 **해제**하여 DualImage 경로(`EStep.DatumPhase` 의 DualImage 분기)에서도 `IsPatternAlignEnabled` 시 `TryComposeAlign` 이 호출되도록 연결. 보정 후 검출은 두 이미지(imgH/imgV)에 대해 동일 transform 으로 수행.
- **D-04:** 패턴 티칭/모델은 **가로 이미지 1세트**만 사용(`TeachingImagePath`). 세로 이미지(`TeachingImagePath_Vertical`)에는 패턴 모델 없음.
- **D-05:** 단서 조건(정상 셋업이면 무시): Z 이동이 텔레센트릭 범위 내일 것, ALIGN 패턴 피처가 가로 이미지에서 선명할 것.

### #1 패턴 ROI 2개 필수 — 경고 + override
- **D-06:** 모델 생성 시 패턴을 1개만 그리면 **경고 메시지**("패턴 2개 권장 — 단일은 회전 정밀도 저하" 취지) 표시하되, 사용자가 OK 선택 시 **단일 패턴으로 진행 허용**(override). 하드 블록 아님 — 좋은 패턴 피처가 1개뿐인 부품의 탈출구 유지.
- **D-07:** 패턴1/패턴2 ROI 그리기 버튼은 UI 상 **나란히 배치**(현재 분리 구조 정돈).
- **D-08:** 단일 패턴 폴백 경로(`PatternRoi2_Length == 0` → 단일 패턴 x,y+단일각) 유지. 패턴2는 Phase 55 의 2-점 baseline 각도용이며 강제가 아닌 권장.

### #3 Datum 시각화 색상 — slate blue 통일, 기준선 유지
- **D-09:** 현재 3색 중복(slate blue origin 십자 + magenta 긴 기준선 + legacy yellow) 을 **slate blue 단일색으로 통일**. magenta 기준선·legacy yellow 를 **제거가 아니라 slate blue 로 recolor**.
- **D-10:** **긴 관통 기준선(이미지 전체 걸치는 datum 축선)은 유지** — Phase 56 에서 사용자 요청으로 추가한 것으로, 14208px 같은 큰 이미지에서 축 방향(틸트) 시인성에 필요. slate blue 30px 화살표만으로는 큰 이미지에서 거의 안 보임.
- **D-11:** 대상 렌더 지점 = `HalconDisplayService.cs` 의 magenta 기준선(`:346`), legacy yellow(Line1 검출선/Circle 중심십자 등 `:52/:70/:166/:200` 류). slate blue origin 십자(`:311`)는 그대로.

### #6 leveling 완전 제거
- **D-12:** leveling 잔재 **완전 제거** — 코드 + 측정 상태 + INI 직렬화 모두. ALIGN 이 위치/tilt 보정을 대체하므로 중복.
- **D-13:** 제거 대상: `DatumConfig.IsLevelingReference`(:43), `InspectionSequence.LevelingEnabled`(:49)/`LevelingAngleRad`/`LevelingAngleComputed`, `TryComputeLevelingAngle`(:603), `DatumFindingService.TryGetLevelingAngle`(:609, 2 오버로드), `Action_FAIMeasurement` `EStep.Level`(:80-118, 상태머신 전이 재배선 필요), `InspectionRecipeManager` INI save/load(:97/:139).
- **D-14:** **off 레시피 회귀 0 검증 필수** — 기존 레시피에서 leveling 미사용(off) 시 동작 변화 없음 확인. 옛 INI 의 `IsLevelingReference` 등 stale 키는 매칭 프로퍼티 부재 시 `ParamBase.Load` 가 무시하므로 로드 크래시 없음(검증으로 확인). `EStep.Level` 제거 시 step 전이 순서 안전 재배선이 최우선 위험.

### #2 패턴 ROI 표시/숨김 토글 (사전확정)
- **D-15:** 결과화면에 패턴 ROI 가시성 토글 추가 — 기존 datum/측정 토글(`SetDatumOverlayVisible`) **미러 패턴**으로 구현. 현재 패턴 ROI 는 teach 피드백만 있고 결과화면 렌더/토글 없음.

### #5 매칭 실패 lenient (사전확정)
- **D-16:** 매칭 실패해도 abort 없이 측정 계속(ALIGN_FAIL → NG 처리). Phase 54 의 `MarkAlignFailed`/lenient 정책을 **검증·보강**(이미 구현됨 — abort 제거/skip 경로 확인 및 누락 보강).

### Claude's Discretion
- #3 기준선 길이/두께 미세 조정(현 전체관통 유지가 기본, 과도하면 후속 조정).
- #6 `EStep.Level` 제거 후 상태머신 전이 재배선의 구체적 구현 방식.
- #1 경고 메시지 정확한 문구.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### ALIGN 핵심 경로 (Phase 54/55)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — `TryComposeAlign`(:453, 단일 refImage 매칭→rigid transform), `TryRunDatumPhase` 2-image 오버로드(:322, DualImage 검출), `MarkAlignFailed`(:382), `ResolveDatumModelPath`(:~396)/`ResolveDatumModelPath2`(:425)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — `EStep.DatumPhase`(:122~), DualImage 분기 + **deferred align 게이트**(:147, #4 해제 대상), 단일이미지 align 배선(:177-187), **`EStep.Level`**(:80-118, #6 제거 대상)
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — `TryFindDatum` 1/2-image 오버로드(:36/:73), `TryFindVerticalTwoHorizontalDualImage`(:592), `TryGetLevelingAngle`(:609, #6 제거 대상)

### 시각화 (#3)
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` — `RenderDatumFindResult` slate blue origin 십자(:311), magenta 기준선(:346, #3 recolor), legacy yellow(:52/:70/:166/:200, #3 recolor), datum/측정 토글 `SetDatumOverlayVisible`(#2 미러 원본)

### 패턴 티칭 UI (#1)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — 패턴 모델 생성(`TryCreateModel`/`TryFindRefPose`, :~2771-2890), 패턴2 ROI 그리기(:2723), 패턴2 write-back(:2742)
- `WPF_Example/UI/ContentItem/MainView.xaml` (또는 해당 xaml) — `btn_drawPatternRoi`/`btn_drawPatternRoi2` 버튼 레이아웃(#1 나란히 배치)

### 레시피/설정 (#6)
- `WPF_Example/Custom/.../DatumConfig.cs` — `IsLevelingReference`(:43), `PatternRoi*`/`PatternRoi2*` 필드
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` — leveling INI save/load(:97/:139)

### 배경 분석 (참고)
- `.planning/ALIGN-01-pattern-align-analysis.md` — ALIGN-01 에이전트 분석(있는 경우)
- ROADMAP `.planning/ROADMAP.md` Phase 54/55/56/57 항목

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `TryComposeAlign`(InspectionSequence): 단일 refImage 패턴매칭 → rigid transform. #4 는 이를 DualImage 경로에서도 호출하도록 배선만 추가(transform 산출 로직 재사용, 변경 최소).
- `SetDatumOverlayVisible`(HalconDisplayService/MainResultViewer): #2 패턴 토글의 미러 원본.
- `TryFindVerticalTwoHorizontalDualImage`(DatumFindingService): DualImage 검출 본문 — #4 에서 보정 transform 입력만 추가.

### Established Patterns
- ALIGN 은 검출 ROI 에 transform 적용 후 검출(nominal 불변) — #4 도 동일 패턴 유지(transform 1회 적용 보장, 이중적용 가드).
- lenient(skip+log, abort 없음) 패턴이 DatumPhase 전반에 정착(`MarkDatumFailed`/`MarkAlignFailed`) — #5 는 이를 검증.
- HALCON SetColor 비표준 색상명 → 예외 → catch swallow → silent 미표시 함정(과거 "purple" 사례). #3 recolor 시 "slate blue" 등 **유효 색상명만** 사용.

### Integration Points
- #4: `Action_FAIMeasurement.cs:147` deferred 게이트 — DualImage 분기에 align 단독 경로 삽입(단일이미지 분기 :177-187 미러).
- #6: `EStep` enum + 측정 상태머신 switch — `Level` step 제거 시 전이 재배선(핵심 위험).
- #3: `HalconDisplayService` 렌더 색상 상수 — recolor only(좌표/길이 무변경, 기준선 유지).

</code_context>

<specifics>
## Specific Ideas

- #4 의 결정적 근거는 **텔레센트릭 렌즈 + Z축 포커싱만 이동** 셋업(사용자 확인). 이 가정이 깨지면(다른 스테이지 위치/배율) 단일 transform 이 틀어지므로, 향후 비텔레센트릭/멀티포지션 셋업 도입 시 재검토 필요.
- #3 은 "색 중복 정리"가 사용자 의도 — 정보(긴 기준선)는 보존하고 색만 통일. 선 자체 삭제 아님.
- #1 은 강제보다 "권장 + 안전장치" 성격 — 부품 다양성(패턴 피처 1개) 고려.

</specifics>

<deferred>
## Deferred Ideas

- 세로 이미지 별도 패턴 매칭(B 방식): 현 텔레센트릭 셋업에선 불필요. 비텔레센트릭/멀티포지션 셋업 도입 시에만 재고.
- leveling 기능 자체의 재도입: ALIGN 이 대체하므로 계획 없음(완전 제거).

[기타: 토론은 phase 스코프 내 유지 — 신규 capability 제안 없음]

</deferred>

---

*Phase: 57-pattern-roi-ux-datum-align-hardening*
*Context gathered: 2026-06-19*
