# Phase 38: v1.1 Carry-over Cleanup 일괄 - Context

**Gathered:** 2026-05-28
**Status:** Ready for planning

<domain>
## Phase Boundary

v1.1 누적 carry-over 및 코드/UI 정리 항목 7건(#1 #3 #5 #6 #10 #11 #12)을 한 phase 로 묶어, **운영 영향 0** 으로 정리하고 v1.1 을 깔끔하게 종결한다. 코드 수정은 execute 단계에서만 수행한다.

새 측정 기능 추가, 헝가리안 전체 리팩토링(v1.2 이연), CXP HW 통합(v1.2)은 이 phase 범위 밖이다.

</domain>

<decisions>
## Implementation Decisions

### #1 측정 타입 정리
- **D-01:** 제거 방식 = **UI ComboBox 숨김**. `MeasurementFactory.GetTypeNames()` 에서 미사용 타입을 제외해 신규 선택을 막되, `Create()` switch 와 Measurement 클래스는 유지 → 기존 INI 레시피에 해당 타입이 있어도 로딩 정상(회귀 위험 최소). Factory 완전 삭제 아님.
- **D-02:** 숨김(미사용) 5종 = `EdgePairDistance`, `PointToLineDistance`, `PointToPointDistance`, `LineToLineAngle`, `LineToLineDistance`.
- **D-03:** 유지(노출) = `EdgeToLineDistance`, `CircleCenterDistance`, `EdgeToLineAngle`, `ArcEdgeDistance`, `ArcLineIntersectDistance`, `CompoundAngle`, `CompoundCenterCDistance`, `CompoundCenterBDistance`, `CompoundShortAxisDistance`, `CircleDiameter`. (EdgeToLineAngle/CircleDiameter 는 사용자가 유지로 명시 — 숨김 대상 아님.)

### #12 ReuseFromShotName / SourceShotName
- **D-04:** `DatumConfig.ReuseFromShotName` 은 로직 사용처 0(스카우트 확인: 속성 정의만) → **필드 + 직렬화 완전 제거**.
- **D-05:** `SourceShotName` 은 `InspectionListView.xaml.cs:702-703` 에서 실사용 중 → **유지**.
- **D-06:** INI 하위호환 — 기존 레시피에 `ReuseFromShotName` 키가 있어도 무시(파싱 오류 없이 건너뜀). execute 시 ParamBase/INI 로딩 경로에서 unknown 키 무시 동작 재확인 필요.

### #3 CircleTwoHorizontal RectL1Ratio / RectL2Ratio
- **D-07:** **별도 유지(변경 없음)**. L1=반경 방향, L2=접선 방향으로 의미가 다르고 독립 튜닝 여지가 있음. 기본값만 동일(0.02). 통합 시 유연성 손실 + INI 호환 깨짐 → 손대지 않는다.

### #5 픽셀분해능 카메라별 단일화
- **D-08:** 단일 소스 = **카메라별 단일값(Top/Bottom/Side)**. mm/pixel 을 카메라 단위 1값으로 관리하고 FAI/ROI 검사 경로는 그 값을 참조(또는 분배). ROADMAP #5 의도와 일치.
- **D-09:** X/Y 분리 해상도(`PixelResolutionX`/`PixelResolutionY`)는 **단일값으로 통합(X=Y)** — 정방형 픽셀 가정.
- **D-10:** 마이그레이션 = **로딩 시 카메라 단일값으로 덮어쓰기**. 기존 INI 의 FAI/ROI 별 PixelResolution 값(및 X≠Y)은 로딩 시 카메라값으로 통일하며, X≠Y 인 경우 X 값을 기준으로 한다. 측정값 차이가 발생하면 의도적 보정으로 문서화(성공기준 #3).
- **D-11:** 현재 산재 위치 = `CameraSlaveParam.PixelResolution`(단일), `ShotConfig.PixelResolution`(단일), `FAIConfig.PixelResolutionX/Y`, `RoiDefinition.PixelResolutionX/Y`, `EdgePairDistanceMeasurement.PixelResolutionX/Y`. 분배 코드 = `MainView.xaml.cs:2033-2038`. execute 시 카메라↔Shot↔FAI 관계 및 X/Y 실사용 여부를 코드로 재확인 후 단일화 레벨/소스 위치 확정.

### #6 각도 파라미터 UI (이월 — 이미 확정)
- **D-12:** todo `2026-05-28-datum-angle-param-ui-cleanup.md` 결정 그대로 적용:
  - `DatumConfig.cs:126` `AngleTolerance` 기본값 `1.0` → `0.0` (sentinel=off, 배지 미표시). `DatumFindingService.cs:739` 의 `if (config.AngleTolerance > 0.0)` 게이트가 0 이면 None.
  - `TwoLineAngleToleranceDeg` (`DatumConfig.cs:112`) **PropertyGrid 숨김** (ICustomTypeDescriptor IsHiddenForAlgorithm/GetProperties 필터, L703/710/718 패턴 참조). 검사 직각 게이트 default 10° 로직(`DatumFindingService.cs:957-975`)은 무변경.
  - INI 하위호환: 기존 키 있으면 그 값 우선.

### #10 주석 정리
- **D-13:** **저위험 항목만** — dead 주석, 날짜백업(`_0428` 등) 잔재 주석, 의미 없는 `//YYMMDD` 나열 정리. 로직 설명 주석은 유지. v1.2 헝가리안 전체 리팩토링 전 단계로 범위 제한(회귀 위험 0).

### #11 프로그램 시작 지연
- **D-14:** **원인 식별 + 저위험 개선까지**. 초기화 구간 프로파일링으로 지연 원인 1개 이상 식별·문서화(성공기준 #4), 명백한 저위험 개선(예: 지연 로딩, 중복 초기화 제거)은 이번에 적용. 구조적/고위험 변경은 carry-over 로 명시.

### Claude's Discretion
- #5 의 단일 소스 정확한 저장 위치(카메라 config 객체 vs SystemSetting), 분배 vs 참조 방식, 코드 변경 범위는 execute 시 코드 확인 후 planner/executor 재량.
- #11 프로파일링 도구/방법 선택.
- #10 정리 대상 주석의 구체 판정.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 정의
- `.planning/ROADMAP.md` §"Phase 38: v1.1 Carry-over Cleanup 일괄" (L396-420) — Goal, Scope 7항목, Decisions pending, Success Criteria, Plans 예상.

### #6 각도 UI (이월 결정 원문)
- `.planning/todos/pending/2026-05-28-datum-angle-param-ui-cleanup.md` — AngleTolerance 기본 OFF + TwoLineAngleToleranceDeg 숨김 결정 + 코드 위치(DatumConfig.cs:112/126, DatumFindingService.cs:739/957-975).

### 코드 진입점 (스카우트 확인)
- `WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs` — #1 측정 타입 등록(15종) Create()/GetTypeNames().
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs:36` (ReuseFromShotName), `:40` (SourceShotName) — #12.
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs:698-703` — SourceShotName 실사용처(#12 유지 근거).
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CircleCenterDistanceMeasurement.cs:48-49` — Circle_RectL1Ratio/L2Ratio(#3).
- `WPF_Example/Sequence/Param/CameraSlaveParam.cs:26`, `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs:85-86`, `WPF_Example/Halcon/Models/RoiDefinition.cs:86-89`, `WPF_Example/UI/ContentItem/MainView.xaml.cs:2033-2038` — #5 PixelResolution 산재/분배.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `MeasurementFactory` switch/GetTypeNames 분리 구조 → #1 은 GetTypeNames 만 손대면 UI 숨김 + 로딩 호환 동시 달성.
- `DatumConfig` ICustomTypeDescriptor PropertyGrid 숨김 패턴(L703/710/718, ExpectedAngleDeg/AngleTolerance hide) → #6 TwoLineAngleToleranceDeg 숨김에 동일 패턴 재사용.
- `DatumFindingService.cs:739` `if (config.AngleTolerance > 0.0)` sentinel 게이트 → #6 기본값 0.0 으로 배지 OFF 가 코드 변경 없이 동작.

### Established Patterns
- INI 직렬화는 ParamBase 리플렉션 기반 — 필드 제거 시(#12 ReuseFromShotName) unknown 키 무시 동작 확인 필요.
- PixelResolution 은 단일(CameraSlaveParam/ShotConfig)과 X/Y 분리(FAIConfig/RoiDefinition/EdgePairMeasurement) 두 형태가 공존 → #5 단일화의 핵심 정리 대상.

### Integration Points
- #5: `MainView.xaml.cs:2033-2038` 이 단일 mmPerPixel 을 shot+fai 로 분배하는 현재 지점 — 카메라별 단일 소스 도입 시 이 분배 경로가 1차 변경점.

</code_context>

<specifics>
## Specific Ideas

- #1/#12 제거는 "삭제"가 아니라 "안전한 숨김/부분 제거"를 일관 선호 — 운영 회귀 0 이 최우선(이 phase 의 정체성).
- #5 정방형 픽셀(X=Y) 가정을 명시적으로 채택 — 단순화 우선, 비정방 카메라면 회귀 위험으로 문서화.

</specifics>

<deferred>
## Deferred Ideas

- #3 RectL1/L2 단일 비율 통합 — 의미가 다른 두 축이라 통합하지 않기로 결정. v1.2 에서 재검토 가능.
- #10 전체 코드베이스 주석 재작성 — v1.2 헝가리안 전체 리팩토링(Phase 26 이연)에 통합.
- #11 시작 지연의 구조적/고위험 개선 — 원인이 구조적이면 별도 phase/carry-over 로 명시.

</deferred>

---

*Phase: 38-v1-1-carryover-cleanup-2026-05-28*
*Context gathered: 2026-05-28*
