# Phase 35: Side/Bottom 실측 UAT + Phase 33 마이그레이션 보강 - Context

**Gathered:** 2026-05-26
**Status:** Ready for planning

<domain>
## Phase Boundary

**Delivers:**
- Phase 33 partial sign-off carry-over 4건 (CO-33-02 / CO-33-03 / CO-33-04 / CO-33-06) 통합 해소
- `ShotConfig` 의 per-sequence ownership 모델 보강 (`OwnerSequenceName` 필드)
- 이미지 갱신 회귀 (`HalconViewerControl.LoadImage` 캐시 로직) 광범위 수정
- Side/Bottom Datum 검출 실패 디버그
- Phase 33 의 4 미수행 SIMUL UAT (Test 2~5) 재수행 + sign-off

**Out of scope:**
- Datum VerticalTwoHorizontal 듀얼 티칭 이미지 변형 (CO-33-05) → Phase 34
- Bottom Multi-Die 자동 FAI 매핑 (CO-33-01) → v1.2 이연
- 신규 측정 알고리즘 추가
- Phase 24 검사 워크플로우 end-to-end (Phase 35 sign-off 후 후속)

**Sign-off 조건:**
1. Bottom Shot 추가 후 INI 저장 → 재로드 → Bottom 시퀀스 트리에 Shot 정상 복원 (CO-33-06 핵심)
2. Side/Bottom 시퀀스 Datum 티칭 + FAI 측정 SIMUL PASS (Phase 33 Test 2/3)
3. Top 회귀 0 — Phase 23.1 sign-off 시점 동작과 100% 동일 (Phase 33 Test 4)
4. INI 라운드트립 — FIXTURE_SIDE/BOTTOM + 시퀀스별 SHOT 매핑 보존 (Phase 33 Test 5)
5. 이미지 갱신 회귀 해소 (CO-33-02)
6. msbuild Debug/x64 PASS, 신규 warning 0

</domain>

<decisions>
## Implementation Decisions

### A. OwnerSequenceId 아키텍처 타입

- **D-A1 (locked):** `ShotConfig` 에 `OwnerSequenceName` (string) 필드 추가
  - 값: `"TOP"` / `"SIDE"` / `"BOTTOM"` (= `SequenceHandler.SEQ_TOP/SIDE/BOTTOM` 상수)
  - ParamBase reflection 자동 직렬화 (DatumConfig.AlgorithmType 패턴 재사용 — Phase 12-01 D-04 선례)
  - 기본값: `""` (EnsurePerRoiDefaults 폴백 — D-B1 참조)
  - INI 가독성 + 디버깅 용이성

### B. INI 하위호환 폴백 전략

- **D-B1 (Claude's Discretion):** `OwnerSequenceName == ""` 일 때 자동으로 `SEQ_TOP` 폴백
  - 사유: Phase 33 이전에는 Top 만 InspectionSequence 였으므로 기존 Shots 는 모두 Top 소속
  - 회귀 0, 사용자 추가 작업 0
  - 폴백 시점: `LoadPhase6Format` 의 Shot 로드 시 (ShotConfig 의 EnsurePerRoiDefaults 또는 InspectionRecipeManager 분기에서 처리 — planner 결정)

### C. 이미지 갱신 회귀 (CO-33-02) 수정 범위

- **D-C1 (locked):** 광범위 수정 — 캐시 로직 리팩터링
  - `HalconViewerControl.LoadImage(string)` + `LoadImage(HImage)` 두 오버로드 일관성 확보
  - `CurrentImagePath` 가 HImage 오버로드 호출 후에도 캐시 hit 가능하도록 보존 또는 무효화 명시
  - `ShotConfig._image` 캐시 무효화 조건 명시 (현재: 이미지 새 로드 시 항상 setShot — 의도와 어긋날 수 있음)
  - 노드 선택 이벤트 (Shot/FAI/Measurement) 재로드 보장
  - Phase 32-06 fix (4ea5bcc/9c482dd) 의 cover 못한 시나리오 (Datum 노드 Load 후 ROI 티칭 시 stale canvas) 해소

### D. SIMUL UAT 범위 및 순서

- **D-D1 (locked, 2026-05-26 업데이트):** **이미지 hotfix 최우선** → 아키텍처 → UAT 순서로 변경
  1. **Wave 1: 이미지 갱신 hotfix (CO-33-02) 최우선** — Top 도 동일 차단 중이므로 단일 root cause 가설 검증
     - HalconViewerControl 캐시 로직 광범위 수정 (D-C1)
     - 사용자 다중-Load 시나리오로 Top SIMUL 재검증 → Datum 검출 PASS 가 1차 sign-off 조건
  2. Wave 2: OwnerSequenceName 아키텍처 (CO-33-06) — Wave 1 의 fix 가 CO-33-06 도 동시 해소하는지 먼저 확인. 별도 fix 필요 시만 진행
  3. Wave 3: Phase 33 Test 2/3/4/5 통합 SIMUL UAT (Side/Bottom Datum + Top 회귀 + INI 라운드트립)

**기존 D-D1 결정 (2026-05-26 디스커스 시점) 변경 사유:**
사용자 추가 보고로 CO-33-03 (Datum 검출 실패) 가 Top 에서도 동일 재현 — Side/Bottom 특화 아님이 확정됨. 따라서 OwnerSequenceId 아키텍처가 단일 root cause 가 아닐 가능성 매우 높음. 이미지 캐시 hotfix 가 Top/Side/Bottom 동시 해소 가능성 — 최우선 검증.
- **D-D2 (locked):** Phase 33 4 미수행 테스트 모두 Phase 35 에서 재수행
  - Phase 35 완료 시 Phase 33 도 retro 완전 sign-off
  - 33-UAT.md frontmatter `status: partial` → `signed_off` 로 retro 업데이트

### E. D-06 가드 적용 (Phase 33 carry-over)

- **D-E1 (locked):** Phase 33 의 D-06 가드 (`InspectionSequence.cs` / `Action_FAIMeasurement.cs` / `VisionResponsePacket.cs` 변경 금지) 를 Phase 35 에도 가급적 유지
  - 단, CO-33-06 OwnerSequenceId 아키텍처 보강에 필요한 최소 변경은 예외 허용
  - 변경 시 Top 회귀 0 가드 (Test 4) 가 차단 조건

### Claude's Discretion

- Plan 분할 단위 (~4 plans 예상)
- 헬퍼 메서드 위치 (ShotConfig 내부 vs SequenceHandler vs InspectionRecipeManager)
- 트리 재구축 분기 알고리즘 (RecipeManager 가 시퀀스별 Shots 컬렉션 노출 vs UI 가 필터링)
- 이미지 캐시 무효화 트리거 (path 비교 / hash / explicit dispose 추적)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 33 carry-over (직접 prerequisite)
- `.planning/phases/33-side-bottom-inspectionsequence-migration/33-UAT.md` — Phase 33 partial sign-off + CO-33-01~06 명세
- `.planning/phases/33-side-bottom-inspectionsequence-migration/33-01-SUMMARY.md` — SequenceHandler 마이그레이션 완료 상태
- `.planning/phases/33-side-bottom-inspectionsequence-migration/33-02-SUMMARY.md` — FIXTURE_SIDE/BOTTOM 직렬화 완료 상태
- `.planning/phases/33-side-bottom-inspectionsequence-migration/33-CONTEXT.md` — Phase 33 의 디스커스 결정 및 D-01~D-06 명세

### 이미지 갱신 관련 (CO-33-02 root cause 후보)
- `WPF_Example/UI/ContentItem/HalconViewerControl.xaml.cs` L111-136 — `LoadImage(string)` / `LoadImage(HImage)` 두 오버로드 + 캐시 로직
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` L108-151 — `DisplayShotImage` / `DisplayMeasurementImage` / `DisplayFAIImage` + `LoadAndDisplay` (L456-517)
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` L370+ — `InspectionList_SelectionChanged` (Datum/Shot/Measurement 노드 분기)
- Phase 32-06 commits `4ea5bcc` / `9c482dd` — 1차 fix 시도 (이번 cover 못한 시나리오 분석 필요)

### OwnerSequenceId 관련 (CO-33-06)
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` — 필드 추가 대상
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` L36-42 (`AddShot`) + L105-160 (`SavePhase6Format`) + L177-249 (`LoadPhase6Format`) — Shot 생성/직렬화/로드
- `WPF_Example/Custom/Sequence/SequenceHandler.cs` L66-86 (`RebuildInspectionActions`) — 시퀀스별 Shot 필터링 도입 지점
- `WPF_Example/Custom/Sequence/SequenceHandler.cs` SEQ_TOP/SEQ_SIDE/SEQ_BOTTOM 상수 (L11-13)
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` L807-843 (`AddShotToSequence`) — UI 핸들러
- `WPF_Example/Sequence/Param/ParamBase.cs` L325-339 Save String case + L385-395 Load String case — 자동 직렬화 검증

### D-06 가드 (Top 회귀 0)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — 변경 금지 (Phase 33 D-06 계승)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` L257-288 (`GrabOrLoadDatumImage` 3-tier fallback) — 변경 금지
- `WPF_Example/TcpServer/VisionResponsePacket.cs` — 변경 금지

### 선례 패턴
- Phase 12-01 `EDatumAlgorithm` enum / `AlgorithmType` string 패턴 (DatumConfig.cs)
- Phase 17 `ICustomTypeDescriptor` 동적 PropertyGrid (DatumConfig.cs)
- Phase 20 D-12 hbk marker stacking (코드 변경 시 신규 라인에 `//260527 hbk Phase 35` 추가, 기존 마커 보존)
- Phase 21 BUF-02 ClearShots lifetime — Shot 버퍼 dispose 시점

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SequenceHandler.SEQ_TOP` / `SEQ_SIDE` / `SEQ_BOTTOM` 상수 — D-A1 의 `OwnerSequenceName` 값으로 직접 사용
- `ParamBase` reflection 자동 직렬화 (String case) — 추가 코드 0 으로 INI 라운드트립 보장
- `ShotConfig.EnsurePerRoiDefaults` 패턴 — D-B1 폴백 구현 위치 후보 (DatumConfig.EnsurePerRoiDefaults 유사)
- `InspectionRecipeManager.ResolveFixtureSequence(ESequence)` overload (Phase 33 신규) — 시퀀스 해석 헬퍼 재사용 가능
- `HalconViewerControl.CurrentImagePath` / `CurrentImage` 속성 — D-C1 캐시 로직 일관성 확보 지점

### Established Patterns
- 시퀀스별 INI 섹션 분리 패턴 (Phase 33 FIXTURE_SIDE/BOTTOM) — Shot 도 동일 패턴 도입 가능 (SHOT_TOP_n / SHOT_SIDE_n / SHOT_BOTTOM_n) 또는 단일 [SHOT_n] 유지 + OwnerSequenceName 필드 (D-A1 채택 = 후자)
- DatumConfig.TeachingImagePath 와 ShotConfig.SimulImagePath 의 dual-image 구조 (Phase 22 IMG-02 + Phase 23 ALG-01) — Phase 35 변경 0 (유지)
- Force-rebind PropertyGrid 패턴 (Phase 16 D-09, Phase 19 fix, CO-22-01) — InspectionListView 노드 전환 시 stale binding 차단

### Integration Points
- `AddShotToSequence` UI 핸들러: `seqNode.SequenceID` (이미 보유) → `ShotConfig.OwnerSequenceName` 전달
- `RebuildInspectionActions(seqId)`: 글로벌 Shots → 시퀀스 소유 Shot 필터링
- 트리 재구축 (`InspectionListView` ItemsSource 갱신): OwnerSequenceName 으로 시퀀스 노드 아래 그룹화
- `LoadPhase6Format` Shot 로드 루프: 폴백 적용 지점

</code_context>

<specifics>
## Specific Ideas

### 사용자 보고 증상 (CO-33-03 디버그 단서 — 2026-05-26 업데이트)

**핵심 발견 (2026-05-26 추가 보고):** **Top 시퀀스도 동일 증상 재현** — Side/Bottom 특화가 아님.

사용자 재현 시나리오 (Top 에서도 동일 실패):
1. Datum 노드 → Load Image (티칭 이미지)
2. Datum 티칭 (ROI 설정, LastTeachSucceeded 확인)
3. Shot 노드 → Load Image (측정 이미지)
4. Measurement ROI 추가 + 설정
5. **Datum 노드 재방문 → Load Image (다시)**
6. **Shot 노드 재방문 → Load Image (다시)**
7. 검사 실행 → **Datum 위치 못 찾음 (Top/Side/Bottom 동일)**

**근본 원인 후보 (CO-33-02 단일 root cause 가설):**
- 다중 이미지 Load 시 HalconViewerControl 의 `CurrentImagePath` / `CurrentImage` 상태와 ShotConfig._image 캐시 / DatumConfig.TeachingImagePath 간 동기화 갭
- `LoadImage(string)` (캐시 hit 가능) ↔ `LoadImage(HImage)` (CurrentImagePath = null) 교차 호출 시 stale 상태 누적
- ROI 좌표는 보존되지만 실제 검출 시점에 사용되는 이미지가 의도와 다름

**가설 검증 순서:**
1. **CO-33-02 (이미지 갱신) hotfix 가 단일 fix** 일 가능성 매우 높음 → 최우선 작업
2. CO-33-02 hotfix 후 사용자 동일 시나리오 재현 → Top/Side/Bottom 동시 해소 여부 확인
3. CO-33-06 (Bottom Shot 재로드) 도 동일 캐시 문제일 가능성 → CO-33-02 fix 후 별도 검증 필요
4. 미해소 시 추가 디버그 (parentSeq / DatumFindingService 알고리즘 분기 / GrabOrLoadDatumImage 의 File.Exists 검증)

**Phase 33 회귀 여부:**
- Top 시퀀스가 Phase 23.1 (2026-05-19) sign-off 시점에는 동작했으나, 동일 다중-Load 시나리오로 검증되었는지 불명
- 즉, **pre-existing baseline 버그가 사용자가 Phase 33 UAT 중 발견** 했을 가능성 (회귀가 아님)
- 또는 Phase 33 이후 신규 노출 시나리오 — Phase 35 에서 git bisect 또는 코드 검토로 확인 필요

### 사용자 Z 축 매핑 명확화 (대화 2026-05-26)
- "Bottom Datum z축 1 / Shot z축 2" — 이는 이미 Phase 22 IMG-02 + Phase 23 ALG-01 의 dual-image 구조 (TeachingImagePath + SimulImagePath) 와 일치
- DatumPhase: TeachingImagePath (Z=1) 로 Datum 검출
- MeasurePhase: SimulImagePath (Z=2) 로 측정
- Phase 35 에서는 이 구조 변경 0 (유지)

</specifics>

<deferred>
## Deferred Ideas

### CO-33-05 → Phase 34 (이미 분리)
- VerticalTwoHorizontal 듀얼 티칭 이미지 변형 (가로축 2 ROI 이미지 + 세로축 1 ROI 이미지)
- 신규 algorithm 1종 추가 — 기존 1-image 타입 회귀 0

### CO-33-01 → v1.2 이연
- Bottom Multi-Die 자동 FAI 매핑 (Die_i_X / Y / Angle 3 FAI 분리, D-03 Option B)

### Phase 24 후속
- Side/Bottom 도 포함한 검사 워크플로우 end-to-end (WF-01, WF-02) — Phase 35 sign-off 완료 후 진입

</deferred>

---

*Phase: 35-side-bottom-uat-and-phase33-completion*
*Context gathered: 2026-05-26*
