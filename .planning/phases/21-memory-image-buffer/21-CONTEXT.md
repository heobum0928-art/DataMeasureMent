# Phase 21: 메모리 이미지 버퍼 — Context

**Gathered:** 2026-05-10
**Status:** Ready for planning

<domain>
## Phase Boundary

매 Inspection Shot 검사 후 캡처된 HImage 를 메모리에 보관하여 결과 이미지 리뷰어/디버그 뷰가 디스크 I/O 없이 마지막 Shot 이미지를 표시할 수 있게 하고, 레시피 변경·시퀀스 리셋·앱 종료 시점에 버퍼가 명시적으로 해제되도록 수명 계약을 코드+문서로 못 박는다.

**범위 내:**
- `ShotConfig._image` 버퍼의 lifetime 명세 명문화 (XML doc + 명시 해제 hook)
- `SequenceHandler.OnRecipeChanged` 이벤트에 buffer-flush subscriber 추가 (현재 누락)
- `Action_FAIMeasurement.EStep.Init` 의 `ClearAllResults` 경로 보존·문서화 (sequence reset 트리거)
- `SystemHandler.Release()` shutdown 시점에 `RecipeManager.ClearShots()` 명시 호출 (현재 검증 필요)
- AC#1 디스크-비접근 표시 경로 (`MainView.DisplayShotImage` → `ShotConfig.GetImage`) 의 명시 검증 (Process Monitor 또는 grep 기반 fileio API 부재 입증)
- AC#2 dispose 입증 (HImage 누수 0 검증 — 측정 도구는 planner 결정)
- msbuild Debug/x64 PASS + SIMUL_MODE 1회 검사 회귀

**범위 외:**
- `TopInspectionParam._latestHalconImage` 통일 — Inspection 흐름과 lifecycle 분리 (티칭/캘리브레이션 용도)
- `Action_FAIMeasurement.GrabOrLoadDatumImage` 의 datumImage 보관 — 현재 finally Dispose 유지
- 별도 `ImageBuffer` 클래스 또는 `IImageBuffer` 인터페이스 추출 — Phase 26 헝가리안 리팩토링과 충돌 방지
- 신규 디버그 뷰 / "Buffer Inspector" 창 — Phase 25 OUT-01 결과 이미지 리뷰어와 중복 우려
- 다중-Shot 보관 정책 변경 (LRU, last-N 등) — REQUIREMENTS.md "보관 정책 없음" 잠금
- 디스크 fallback / 캐시 — REQUIREMENTS.md "디스크 fallback 없음" 잠금

</domain>

<decisions>
## Implementation Decisions

### 버퍼 범위 (Scope)

- **D-01:** 메모리 상주 대상 = **Inspection Shot 만**. `ShotConfig._image` 가 유일한 Phase 21 버퍼. `TopInspectionParam._latestHalconImage` 와 Datum 단계 캡처는 Phase 21 범위 외 (별도 lifecycle, 충돌 시 carry-over 로 분리). Phase 21 스코프 최소화 + Phase 26 리팩토링 충돌 회피.

### Lifetime 트리거 (명시 해제 시점)

- **D-02:** AC#2 충족 = 3 채널 명시 해제:
  1. **레시피 변경** — `SystemHandler.Sequences.OnRecipeChanged` 이벤트에 buffer-flush subscriber 신규 등록 (현재 누락). subscriber 가 `RecipeManager.ClearShots()` 명시 호출. Load() 경로 의존에서 명시 훅으로 승격.
  2. **Sequence Reset** — 기존 `Action_FAIMeasurement.EStep.Init` → `ShotParam.ClearAllResults()` 경로 유지 + 마킹 주석 (현재 의도가 reset 임을 명시).
  3. **App shutdown** — `SystemHandler.Release()` 에서 `Sequences.RecipeManager.ClearShots()` 명시 호출 검증. 누락 시 추가.
- **D-03:** subscriber 등록 위치는 `Custom/SystemHandler.cs` 의 Initialize() (RecipeManager 가 SequenceHandler 를 통해 접근 가능한 시점) — planner 가 정확한 wire-up 위치 결정.
- **D-04:** `SequenceBase` 에 신규 `OnReset` 메서드/이벤트 도입 안 함 — Phase 21 스코프 외, EStep.Init 경로로 충분.

### API 표면 / 추상화

- **D-05:** 기존 `ShotConfig.SetImage` / `GetImage` / `ClearImage` / `HasImage` / `ClearAllResults` 시그니처 유지. 신규 클래스/인터페이스 0. Phase 26 헝가리안 리팩토링이 추상화 결정 자유도 보존.
- **D-06:** XML doc 강화 대상 (수명 계약 명세):
  - `ShotConfig.SetImage(HImage)` — clone-on-input + 기존 _image 자동 dispose, 호출자가 원본 dispose 책임
  - `ShotConfig.GetImage()` — clone-on-output, **caller dispose 책임** (현재 호출부 `using` 패턴 유지)
  - `ShotConfig.ClearImage()` — 수명 종료 명시, 호출 시점 (recipe change / sequence init / shutdown) 문서화
  - `ShotConfig.HasImage` — `_imageLock` 동기화 보장
  - `ShotConfig.ClearAllResults()` — sequence reset 시점 호출, image + FAI result 동시 초기화 의도 명시
  - `InspectionRecipeManager.ClearShots()` — 모든 shot.ClearImage 순회, 레시피 변경 시 호출되어야 함 (recipe lifetime 계약)

### AC#1 디스크-비접근 표시 경로

- **D-07:** AC#1 충족 경로 = 기존 `InspectionListView` FAI 트리 클릭 → `MainView.DisplayFAIImage(fai)` → `DisplayShotImage(shot)` → `shot.GetImage()` (메모리 직접 읽기). UI 신규 0.
- **D-08:** AC#1 입증 방법 = SIMUL UAT 시 결과 이미지 표시 경로의 fileio API 부재 명시 검증. 입증 도구는 planner 결정 (옵션: ① Process Monitor 캡처 + screenshot, ② grep 기반 `File.*` / `HImage.ReadImage` 호출 부재 코드 audit, ③ 둘 다).

### 회귀 검증 (AC#3, AC#4)

- **D-09:** AC#3 "수명 보장 시점 문서화" = D-06 XML doc 강화로 충족. 추가 별도 문서 (예: BUFFER-LIFETIME.md) 도입 안 함 — 코드 인접 주석 우선.
- **D-10:** AC#4 회귀 검증 = msbuild Debug/x64 PASS + SIMUL_MODE 1회 검사 (Datum 티칭 + FAI 측정 + 결과 이미지 리뷰 클릭) 정상 동작. 측정 비교 임계 = byte-identical (변경이 행위 보존 의도).
- **D-11:** AC#2 dispose 입증 도구 = planner 결정. 권장 우선순위: ① HImage 인스턴스 카운터 + 단위 시퀀스 (recipe load × 5회 후 카운트 0 입증), ② GC.GetTotalMemory before/after 비교 (잡음 큼 — 보조). 메모리 프로파일러 (dotMemory 등) 는 Phase 21 외부 도구로 분류.

### Claude's Discretion

- subscriber wire-up 정확한 위치 (`Custom/SystemHandler.cs` Initialize vs MainWindow Loaded vs SequenceHandler 자체) — planner 결정.
- XML doc 문장의 실제 표현 (영문/한글, summary/remarks 분리 등) — executor 결정. 도메인 용어 일관성 유지.
- AC#2 dispose 입증 단위 시퀀스의 정확한 횟수 (5회 vs 10회) 와 검증 위치 (UAT.md 수동 vs SIMUL 자동) — planner 결정.
- subscriber 등록/해제 lifecycle 보호 (App shutdown 시 unsubscribe) — planner 결정.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 요구사항 정의
- `.planning/ROADMAP.md` §Phase 21 — 성공 기준 4개 (AC#1~AC#4)
- `.planning/REQUIREMENTS.md` §BUF-01, §BUF-02 — Phase 21 요구사항 정의 ("보관 정책 없음, 디스크 fallback 없음" 잠금)
- `.planning/PROJECT.md` §v1.1 Active — "검사별 이미지 버퍼 (메모리 상주, 속도 우선, 보관 정책 없음)"

### 인접 phase context
- `.planning/phases/20-code-style-cleanup/20-CONTEXT.md` — D-01 연산자 변환 정책 (신규 코드는 if/else 명시), D-08 주석 정책 (why 만 보존)
- `.planning/phases/19-propertygrid-dynamic-exposure/19-CONTEXT.md` — DynamicPropertyHelper 위임 패턴 (참고용, Phase 21 직접 사용 없음)

### Phase 21 핵심 소스 파일
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` — _image / _imageLock / SetImage / GetImage / ClearImage / HasImage / ClearAllResults (XML doc 강화 대상)
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` — ClearShots() (XML doc 강화 + 호출 명시), Load() L166 ClearShots 호출 경로
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — EStep.Init L60 ShotParam.ClearAllResults() 호출 (sequence reset 트리거 마킹), EStep.Grab L106-129 HasImage / SetImage / GetImage 사용 패턴
- `WPF_Example/Sequence/SequenceHandler.cs` — OnRecipeChanged 이벤트 (L46 declaration, L162 invoke) — subscriber 추가 대상
- `WPF_Example/Custom/SystemHandler.cs` — Initialize() / Release() (subscriber wire-up + shutdown 위치 후보)
- `WPF_Example/SystemHandler.cs` — LoadRecipe() 경로 (D-02 channel #1 추적)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — DisplayFAIImage L101 / DisplayShotImage L113 (AC#1 검증 대상 경로)
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — FAI 트리 click → DisplayFAIImage 호출 (L462)

### 후속 phase 경계 (carry-over 방지)
- Phase 25 OUT-01 — 결과 이미지 리뷰어 (날짜/원본 폴더 디스크 로드, 과거 기록 재현). Phase 21 메모리 경로와 별개.
- Phase 26 헝가리안 전체 리팩토링 — `_image` / `_imageLock` 등 Phase 21 코드 만지므로 신규 추상화 도입 시 충돌 위험. Phase 21 은 추상화 0 유지 (D-05).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **ShotConfig 의 thread-safe HImage 패턴 (이미 존재):** clone-on-IO + lock 으로 multi-thread 안전. Phase 21 추가 가치 = 수명 계약 가시화.
- **InspectionRecipeManager.ClearShots():** 이미 모든 shot.ClearImage 순회 (L51-56). Phase 21 은 호출 경로 명시 + 추가 트리거 wire.
- **Action_FAIMeasurement.EStep.Init:** ShotParam.ClearAllResults() 호출 (L60). Sequence reset 의 사실상 트리거. 마킹 주석으로 의도 명문화.
- **MainView.DisplayShotImage (L113-132):** shot.GetImage() 직접 사용 + try/finally Dispose (Phase 20 D-02 패턴 적용됨). AC#1 충족 경로 — 신규 UI 불요.

### Established Patterns

- **using-block clone disposal:** `using (var image = ShotParam.GetImage()) { ... }` (Action_FAIMeasurement L141). caller dispose 책임 패턴 — XML doc 에 명시.
- **SetImage 의 internal CopyImage:** `_image?.Dispose(); _image = image?.CopyImage();` (ShotConfig L60-64). Phase 20 D-02 작업으로 `_image?.Dispose()` 가 if-block 으로 풀려 있을 가능성 있음 (planner 가 현재 상태 확인).
- **OnRecipeChanged 이벤트 발화:** `SequenceHandler.cs:162` `OnRecipeChanged?.Invoke(this, new RecipeChangedEventArgs(name));` — Phase 20 D-02 적용으로 임시변수 패턴일 수 있음 (planner 확인).
- **MainWindow 가 OnRecipeChanged subscriber:** `MainWindow.xaml.cs:82` `Sequences.OnRecipeChanged += OnLoadRecipe;` — 같은 위치에 buffer-flush subscriber 추가 가능 후보.

### Integration Points

- **SystemHandler.LoadRecipe → SequenceHandler.OnRecipeChanged 발화 → (신규) buffer-flush subscriber → RecipeManager.ClearShots:** D-02 channel #1 의 신규 wire 경로.
- **SystemHandler.Release() → Sequences.RecipeManager.ClearShots():** App shutdown 시 누락 가능 — planner 가 호출 여부 확인 + 누락 시 추가.
- **InspectionRecipeManager.Load() L166 ClearShots:** 레시피 재로드의 기존 경로. D-02 channel #1 신규 subscriber 와 중복 호출 가능 — 중복 호출은 idempotent (ClearImage 가 null-safe) 이므로 안전, planner 가 명시 확인.

</code_context>

<specifics>
## Specific Ideas

- **사용자 명시 결정:**
  - D-01 Inspection Shot 만 (Top/Datum 통일 거부 — 스코프 최소화)
  - D-02 OnRecipeChanged subscriber 명시 (Load 경로 의존 → 명시 훅 승격)
  - D-05 기존 메서드 유지 (신규 클래스/인터페이스 0)
  - D-07 기존 DisplayShotImage 재사용 (UI 신규 0)
- **회귀 임계:** byte-identical (Phase 20 D-16 동일 — XML doc 추가는 행위 보존, subscriber 추가는 새 dispose 호출이지만 idempotent).
- **REQUIREMENTS.md 잠금 사항:**
  - BUF-01 "보관 정책 없음, 디스크 fallback 없음" — LRU/last-N/disk-cache 도입 금지
  - BUF-02 "lifetime 을 명시적으로 관리" — D-02 3 채널이 직접 응답
- **hbk 마커 컨벤션:** 신규/변경 라인에 `//260510 hbk Phase 21` 마커 부착. Phase 20 D-12 정책 (스택 X, 변환 라인만 마커 교체) 준수.

</specifics>

<deferred>
## Deferred Ideas

### Phase 26 헝가리안 리팩토링 시 흡수
- **ImageBuffer 클래스 / IImageBuffer 인터페이스 추출** — Phase 21 이 D-05 로 거부. Phase 26 에서 헝가리안 + 추상화 동시 검토.
- **`_image` / `_imageLock` 헝가리안 표기 정합** — Phase 26 에서 일괄.

### Phase 25 OUT-01 결과 이미지 리뷰어 와 분리 유지
- **디스크 기반 과거 결과 재현** — Phase 21 의 메모리 경로와 별개 lifecycle. Phase 25 시 Phase 21 의 메모리 경로를 fallback 으로 활용 가능 (논의 필요).
- **"Buffer Inspector" 신규 디버그 창** — D-07 로 Phase 21 거부. 필요 시 Phase 25 또는 별도 phase 에서 검토.

### Phase 21 범위 외 (carry-over 후보)
- **TopInspectionParam._latestHalconImage 와 ShotConfig._image 패턴 통일** — Phase 26 또는 별도 carry-over phase. 현재는 lifecycle 분리 (티칭 vs 검사) 가 의도적.
- **Action_FAIMeasurement.GrabOrLoadDatumImage 의 datumImage 보관** — 현재 finally Dispose. Datum 재조회 요구가 생기면 별도 phase 에서 ShotConfig 패턴 적용.
- **SequenceBase.OnReset 명시 hook 도입** — D-04 로 Phase 21 거부. v2.0+ 또는 Phase 24 워크플로우 end-to-end 시 검토.

</deferred>

---

*Phase: 21-memory-image-buffer*
*Context gathered: 2026-05-10*
