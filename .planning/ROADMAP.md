# Roadmap: DataMeasurement

## Milestones

- **v1.0 Halcon Migration MVP** ✅ Phases 1-17 (shipped 2026-05-04, 22 deferred items)
- **v1.1 Quality + Workflow + Algorithm** 🔄 Phases 18-28 (started 2026-05-04)
- **v1.2 Hardware Integration + Code Cleanup** ⏳ CXP SDK + Driver + 헝가리안 리팩토링 (deferred — 장비 도착 후, Phase 26 이연 2026-05-26)

## Phases

<details>
<summary>v1.0 Halcon Migration MVP (Phases 1-17) ✅ SHIPPED 2026-05-04</summary>

Full archive: [milestones/v1.0-ROADMAP.md](milestones/v1.0-ROADMAP.md)
Phase artifacts: [milestones/v1.0-phases/](milestones/v1.0-phases/)

- [x] Phase 1~17: v1.0 전체 완료 (상세는 milestones/v1.0-ROADMAP.md 참조)

</details>

### v1.1 Quality + Workflow + Algorithm

- [x] **Phase 18: Carry-over 정리** — completed 2026-05-07
- [x] **Phase 19: PropertyGrid 동적 노출 일반화** — completed 2026-05-07
- [x] **Phase 20: 코드 스타일 정리** — signed off 2026-05-09
- [x] **Phase 21: 메모리 이미지 버퍼** — ✅ signed off 2026-05-11 (BUF-01/BUF-02 4/4 AC, hotfix a3d9545)
- [x] **Phase 22: 이미지 이중화 구조** — ✅ signed off 2026-05-11 (IMG-01/IMG-02, 4/4 UAT PASS) ← 신설 2026-05-11
- [x] **Phase 23: Top #1 A시리즈 Simul end-to-end** — ✅ 최종 sign-off 2026-05-19 (23.1-UAT.md 가 23-UAT.md supersede, D-14)
- [x] **Phase 23.1: EdgeToLineDistance ROI 티칭 배선 + 다점 치수 지원** (INSERTED) — ✅ SIGNED OFF 2026-05-19 (8/8 PASS). CO-23-01 resolved / CO-23.1-01·02 → 신규 알고리즘 Phase 이연
- [⚠] **Phase 33: Side/Bottom InspectionSequence 마이그레이션** — PARTIAL signed off 2026-05-26, retro 부분 sign-off 2026-05-27 (Test 3/4/5 PASS via Phase 35, Test 2 Side → Phase 34)
- [⚠] **Phase 34: Datum VerticalTwoHorizontal 듀얼 티칭 이미지 변형** — PARTIAL signed off 2026-05-27 (Test 1+5 PASS · Test 3-a/3-b PASS · Test 3-d FAIL swap UX 갭 · Test 2/3-c/3-e/3-f/4 → Phase 34.1)
- [⚠] **Phase 34.1: Datum DualImage swap UX** — PARTIAL signed_off 2026-05-27 (5/7 Test PASS · Test 4 INI NOT-TESTED · Test 7 PARTIAL 직각도 한계 → CO-34.1-01 carry-over). UAT 도중 6 hotfix 발견 + 종결 (CO-34.1-02~07). 2026-05-28 실측 페어 UAT 도중 추가 hotfix CO-34.1-08 (BtnTestFindDatum_Click DualImage 분기 누락) + CO-34.1-09 신규 carry-over (좌표계/각도 검증 갭) → Phase 36 신설.
- [⚠] **Phase 35: Side/Bottom 실측 UAT + Phase 33 마이그레이션 보강** — PARTIAL signed off 2026-05-27 (5/6 PASS, Test 4 Side → Phase 34, CO-33-02/06 해소, CO-35-01/02 hotfix)
- [ ] **Phase 36: Datum DualImage 설계 보강** — 좌표계 통합 (anchor/offset) + 각도 검증 파라미터 (ExpectedAngleDeg + AngleTolerance) + Test Find 시각화 강화 (CO-34.1-09 carry-over 종결 + Test 4 INI 실측)
- [ ] **Phase 24: 검사 워크플로우 end-to-end** — Datum→FAI→결과 처리 완주 + OK/NG/실패 분기 (WF-01, WF-02) — Top/Bottom prerequisite 충족 (Side 는 Phase 34 후)
- [ ] **Phase 25: 결과 분석 & Export** — 이미지 리뷰어 + xlsx export + 알고리즘별 통계 (OUT-01..04)
- [ ] **Phase 27: Side Inspection 확장** — LineToLineAngle + Side Fixture INI + PC2 분리 + Datum 2-image 지원 (D1, H5, Phase 999.1 흡수 2026-05-26)
- [ ] ~~**Phase 26: 헝가리안 전체 리팩토링**~~ — **v1.2 로 이연 2026-05-26** (QUAL-01, 코드 정리 — POC 납기 후)
- [x] **Phase 28: FAI CircleDiameter + Datum Circle 알고리즘 통합** — signed off 2026-05-08
- [x] **Phase 31: Datum 기준 측정 알고리즘 확장** — ✅ signed off 2026-05-26 (Test 1/2/6/7/8/9 PASS, Test 3/4/5 → Phase 32 transferred, CO-31-01 신규 carry-over) ← 신설 2026-05-19

---

### v1.2 Hardware Integration (이연 — 장비 도착 후)

- [ ] **Phase 29: CXP SDK 확정** (구 Phase 22) — HW-01
- [ ] **Phase 30: CXP 드라이버 통합** (구 Phase 23) — HW-02

> 이연 사유: POC 납기(6월 말) 기준 HW 도착 전까지 Simul 모드 알고리즘/UI 검증 우선.
> CXP 장비 도착(6월 중순 예상) 후 v1.2 개시.

---

## Phase Details

### Phase 21: 메모리 이미지 버퍼
**Goal**: 각 Shot 검사에서 캡처한 HImage 를 메모리에 보관하여 디스크 I/O 없이 재조회할 수 있고, 시퀀스 리셋 또는 레시피 변경 시 버퍼 내 모든 HImage 가 명시적으로 제거된다
**Depends on**: Phase 20
**Requirements**: BUF-01, BUF-02
**Plans**: 3 plans
Plans:
- [x] 21-01-PLAN.md
- [x] 21-02-PLAN.md
- [x] 21-03-PLAN.md — VERIFICATION + UAT sign-off (signed_off 2026-05-11, hotfix a3d9545)

---

### Phase 22: 이미지 이중화 구조 (신설 2026-05-11)
**Goal**: Datum 티칭 이미지(TeachingImagePath)와 검사 이미지(InspectionImagePath)를 코드 레벨에서 역할 분리.
티칭 시 사용한 기준 이미지를 INI에 보존하고, 검사 실행 시에는 별도 경로의 이미지를 사용할 수 있도록 한다.
Simul 모드에서는 두 경로가 동일 파일을 가리켜도 무방하나, 참조 경로는 항상 분리 유지된다.
**Depends on**: Phase 21
**Requirements**: IMG-01, IMG-02
**Background**:
  - 현재 Simul 모드: 이미지 1장 로드 시 Datum/FAI 모두 동일 경로 사용
  - 문제: 티칭 시 사용한 기준 이미지를 나중에 참조할 수 없음 (재티칭 시 기준 불명)
  - 해결: TeachingImagePath(INI 저장) / InspectionImagePath(검사 실행 시) 분리
**Success Criteria** (what must be TRUE):
  1. DatumConfig 에 TeachingImagePath 필드 추가 + INI 직렬화/역직렬화 동작
  2. 검사 실행(Simul) 시 이미지 경로는 InspectionImagePath 로 분리
  3. 두 경로가 같은 파일이어도 Datum 찾기 → FAI 측정 정상 동작
  4. TeachingImagePath 가 INI에 없을 경우 빈 문자열 폴백 (기존 동작 회귀 없음)
  5. msbuild Debug/x64 PASS, 신규 warning 0
**Plans**: 2 plans
Plans:
- [ ] 22-01-PLAN.md — DatumConfig TeachingImagePath 필드 + EnsurePerRoiDefaults null 가드 (IMG-01)
- [ ] 22-02-PLAN.md — InspectionImagePath 역할 명시 주석 + msbuild + UAT sign-off (IMG-02, autonomous: false)

---

### Phase 23: Top #1 A시리즈 Simul end-to-end (신설 2026-05-11)
**Goal**: PPT(Datum_정보_260511_2D) 기반으로 Top Fixture #1 의 Datum B/C 설정 →
FAI A1~A5 Y방향 거리 측정까지 Simul 이미지 1장으로 오류 없이 완주한다.
이 Phase 이후 어떤 FAI도 동일 구조로 확장 가능하다.
**Depends on**: Phase 22
**Requirements**: ALG-01
**Background (PPT 구조)**:
  - Datum B: Top View 하단 수평면 접선 → Y축 기준선
  - Datum C: B1 홀 센터 통과 수직선 → X축 기준선
  - FAI A1~A5: Datum B → 측정 포인트 Y방향 거리 (Back light, Fixture #1)
  - 측정 알고리즘: EdgeToLineDistance
**Success Criteria** (what must be TRUE):
  1. Simul 이미지 로드 → Datum B/C 자동 찾기 → A1~A5 측정값(mm) UI 표시가 오류 없이 완주
  2. A1~A5 각 측정값이 공차 범위 내 OK/NG 판정 → 결과 strip 색상(녹/적) 표시
  3. 티칭 이미지 경로와 검사 이미지 경로 분리 상태에서도 동일 동작 (Phase 22 구조 활용)
  4. 동일 구조로 A6~A23 추가가 INI 설정만으로 가능 (확장성 검증)
  5. msbuild Debug/x64 PASS, 신규 warning 0
**Plans**: 3 plans
Plans:
- [ ] 23-01-PLAN.md — EdgeToLineDistanceMeasurement 신규 + TryFitLine selection 확장 + csproj 등록 (Wave 1)
- [ ] 23-02-PLAN.md — MeasurementFactory dispatch + GrabOrLoadDatumImage TeachingImagePath 분기 (Wave 2)
- [ ] 23-03-PLAN.md — Simul end-to-end UAT + sign-off (Wave 3, autonomous: false)

---

### Phase 23.1: EdgeToLineDistance ROI 티칭 배선 + 다점 치수 지원 (INSERTED)

**Goal:** EdgeToLineDistance measurement 의 Point ROI 를 측정별로 드로잉 티칭할 수 있게 배선하고,
1개 FAI 가 N개 measurement(다점 P1/P2/…)를 갖는 유동 구조를 UI·INI 로 운용 가능하게 한다.
EdgeSelection 결함을 구조적으로 차단하고, A1~A5 의 Datum→Y거리(mm) 측정을 실데이터로 검증하여 Phase 23 을 최종 마감한다.
**Requirements**: ALG-01 (carry-over — Phase 23 미완 완결)
**Depends on:** Phase 23
**Plans:** 3 plans

Plans:
- [x] 23.1-01-PLAN.md — EdgeSelection 구조적 차단 (TryExecute "All" 고정 + ICustomTypeDescriptor hide, D-08/D-09) — Wave 1
- [x] 23.1-02-PLAN.md — ROI 측정별 티칭 배선 + 다점 운용 UI (CommitRectRoi Measurement 분기 + GetCurrentFAIRois 다점 렌더, D-01~D-05) — Wave 2
- [x] 23.1-03-PLAN.md — msbuild 검증 + 실데이터 SIMUL UAT (Phase 23 SC#1~SC#4 통합 sign-off, autonomous: false, D-12~D-15) — Wave 3

**Status:** ✅ SIGNED OFF (2026-05-19) — 23.1-UAT.md 8/8 PASS (대화형 UAT). Phase 23 도 동시 최종 sign-off (D-14). carry-over: CO-23-01 resolved / CO-23.1-01·02 → 신규 알고리즘 Phase 이연. 후속 quick: 260517-l5e, 260518-vxp, 260519-c08.

---

### Phase 33: Side/Bottom InspectionSequence 마이그레이션 (신설 2026-05-26, Phase 24 prerequisite)
**Goal**: Side / Bottom 카메라 시퀀스를 레거시 `TopSequence` / `BottomSequence` 에서 신규 `InspectionSequence` 로 마이그레이션 — Side/Bottom 에서도 Multi-Datum + Dynamic FAI 구조 사용 가능하게 한다.
**Depends on**: Phase 23.1
**Background (코드 조사 2026-05-26)**:
  - `SequenceHandler.RegisterSequences()` (L30-34):
    - Top → `InspectionSequence` (Datum + dynamic FAI 지원, Phase 5/6 마이그레이션)
    - Side → `TopSequence` (레거시) — DatumConfigs 필드 부재 → **Datum 형성 불가**
    - Bottom → `BottomSequence` (레거시) — DatumConfigs 필드 부재 → **Datum 형성 불가**
  - v1.0 마이그레이션 시 Top 만 InspectionSequence 로 마이그레이션, Side/Bottom 누락
  - Side/Bottom 검사가 작동 안 하면 Phase 24 의 end-to-end 검증 자체가 불가능 → Phase 24 prerequisite 로 설정
**Scope**:
  1. SequenceHandler 의 Side/Bottom 시퀀스 클래스 교체 (TopSequence/BottomSequence → InspectionSequence)
  2. 관련 Action 매핑 정리 (TopInspectionAction 의 Side 케이스 + BottomInspectionAction 의 Bottom 케이스 → Action_FAIMeasurement 사용 또는 InspectionSequence 기반 흐름으로 통합)
  3. INI 하위호환: Side/Bottom 의 기존 recipe 가 새 구조에서도 정상 로드되도록 EnsurePerRoiDefaults 확장 (또는 마이그레이션 코드)
  4. 레거시 TopSequence/BottomSequence/TopInspectionAction/BottomInspectionAction 클래스 deprecate 또는 정리
  5. SIMUL UAT — Side / Bottom 각각에서 Datum 티칭 + FAI 측정 작동 검증
**Success Criteria**:
  1. Side / Bottom 시퀀스가 InspectionSequence 인스턴스로 등록됨 (grep 검증)
  2. Side / Bottom 노드에서 Datum 추가 + 티칭 + FAI 측정 정상 작동
  3. INI Save/Load 라운드트립 유지 (Side/Bottom recipe 호환)
  4. msbuild Debug/x64 PASS, 신규 warning 0
**Plans**: 3 plans
Plans:
- [ ] 33-01-PLAN.md — SequenceHandler Side/Bottom InspectionSequence 교체 + 레거시 4 클래스 [Obsolete] (Wave 1)
- [ ] 33-02-PLAN.md — InspectionRecipeManager 시퀀스별 FIXTURE 직렬화 확장 (FIXTURE_SIDE/BOTTOM, Top byte-identical) (Wave 2, depends on 33-01)
- [ ] 33-03-PLAN.md — SIMUL UAT (msbuild + Side / Bottom Datum + Top 회귀 + INI 라운드트립) + sign-off (Wave 3, autonomous: false)

---

### Phase 34: Datum VerticalTwoHorizontal 듀얼 티칭 이미지 변형 (신설 2026-05-26)
**Goal**: VerticalTwoHorizontal 알고리즘의 2-image 변형 1종 추가 — 가로축 ROI 2개 (Horizontal_A + Horizontal_B) 는 이미지 1장, 세로축 ROI 1개 (Vertical) 는 이미지 2장에서 검출하여 Datum 좌표 결합.
**Depends on**: Phase 33 (구조 마이그레이션 완료 — 코드 부분)
**Background (Phase 33 carry-over CO-33-05)**:
  - 기존 3 datum 타입 (TwoLineIntersect / CircleTwoHorizontal / VerticalTwoHorizontal) 모두 단일 이미지 전제
  - 실제 검사 현장에서 가로축 ROI 와 세로축 ROI 가 동일 이미지에서 모두 잡히지 않는 케이스 존재
  - 신규 algorithm 1종 추가 (예: `VerticalTwoHorizontalDualImage`) — 기존 1-image 타입은 회귀 0
**Success Criteria**:
  1. 신규 EDatumAlgorithm 값 1개 추가, AlgorithmType 콤보박스에 노출
  2. DatumConfig 에 `TeachingImagePath_Vertical` 필드 추가 (ICustomTypeDescriptor 로 신규 타입일 때만 노출)
  3. DatumFindingService 신규 분기 — 가로 ROI 2개는 image1, 세로 ROI 1개는 image2 에서 검출
  4. 티칭 UI step 머신에 이미지 전환 step 추가 (Horizontal_A → Horizontal_B → image switch → Vertical)
  5. Action_FAIMeasurement GrabOrLoadDatumImage 가 ROI 별 이미지 선택 (algorithm = 신규일 때 vertical ROI 는 image2)
  6. INI 호환: TeachingImagePath_Vertical 미존재 시 "" 정규화 (기존 INI 회귀 0)
  7. 기존 1-image 타입 회귀 0 (msbuild PASS + Top recipe 로드 확인)
**Plans**: 4 plans
Plans:
- [x] 34-01-PLAN.md — EDatumAlgorithm enum + DatumConfig 필드/AlgorithmTypeList/INI 정규화/ICustomTypeDescriptor 분기 (Wave 1)
- [x] 34-02-PLAN.md — DatumFindingService 2-image TryFindDatum/TryTeachDatum 오버로드 + 2 신규 private 메서드 (Wave 2)
- [x] 34-03-PLAN.md — MainView 4 메서드 분기 + Action_FAIMeasurement EStep.DatumPhase 분기 + TryGrabOrLoadDualDatumImages (Wave 3)
- [x] 34-04-PLAN.md — SIMUL UAT 5 Test (msbuild + 1-image 회귀 0 + DualImage SIMUL + INI 라운드트립 + D-34-13/14 가드) + sign-off (Wave 4, autonomous: false) — **partial signed off 2026-05-27**: Test 1+5 PASS · Test 3-a/3-b PASS · Test 3-d FAIL swap UX 갭 → Phase 34.1 · Test 2/3-c/3-e/3-f/4 → 34.1 UAT 일괄

---

### Phase 34.1: Datum DualImage swap UX (INSERTED 2026-05-27, gap-closure)
**Goal**: Phase 34 partial sign-off 의 CO-34-01~04 흡수 — DualImage (VerticalTwoHorizontalDualImage) algorithm 에서 사용자가 현재 표시 이미지를 시각적으로 구분하고 (캔버스 우상단 배지) + 수동으로 가로/세로 swap 가능한 (캔버스 툴바 토글 버튼 2개) UI 추가. 자동 swap (Phase 34 D-34-06) 은 유지하되 사용자가 언제든 되돌릴 수 있는 보완 채널.
**Depends on**: Phase 34 (partial signed off)
**Type**: gap-closure (parent_phase=34)
**Background**:
  - Phase 34 UAT (2026-05-27): 자동 swap 만으로는 사용자가 현재 어느 축 이미지인지 시각적 구분 불가 + 임의 swap 불가 → ROI 그리기 신뢰성 확보 불가능 → DualImage 워크플로우 미완성
  - 사용자 피드백 인용: "이미지를 사용자가 원하는대로 스왑이 필요할 꺼 같아 이렇게 보면 헷갈려"
**Success Criteria (CONTEXT D-34.1-01~17)**:
  1. DualImage algorithm 선택 시 캔버스 툴바에 [👁 가로] [👁 세로] 토글 버튼 2개 + 우상단 배지 (가로축 = Blue700 #1976D2 / 세로축 = Orange800 #F57C00, 14px, 마진 12px) 모두 Visible (D-34.1-09/14)
  2. 1-image algorithm (TLI/CTH/VTH) 에서는 토글/배지 모두 Collapsed (D-34.1-09)
  3. 수동 토글 시 (a) 배지 텍스트 + (b) 배지 색상 + (c) 캔버스 ROI 가시성 (가로 = HA+HB / 세로 = Vertical) **3자 동시 전환** (D-34.1-15)
  4. 자동 swap (StartDatumTeachStep(Vertical), L1994~) 도 동일 헬퍼 UpdateImageSourceBadge(EImageSource) 경유 — 자동/수동 일관성 (D-34.1-15)
  5. Datum 노드 이동 시 swap 상태 = 가로축 기본 리셋 (D-34.1-08, 세션 한정 + INI 미저장)
  6. Wizard step 라벨 ↔ 배지 의미 분리 — swap 시 step 라벨 변경 안 함 (D-34.1-11)
  7. SIMUL 의사 페어 (Cal_Image/DualImageTest/) 로 Datum 결합 PASS → Phase 34.1 종결 (실측 Side fixture 페어 = CO-34.1-01 carry-over, D-34.1-16)
  8. Phase 34 D-34-13/14 가드 (DatumConfig / VisionResponsePacket / InspectionSequence / Action_FAIMeasurement) 변경 0 유지 (D-34.1-07)
**Plans**: 2 plans
Plans:
- [x] 34.1-01-PLAN.md — MainView 토글 버튼 + 배지 XAML + 3자 동시 갱신 헬퍼 + EImageSource enum + Cal_Image/DualImageTest/ 폴더 + DatumConfig.cs working-tree 정리 (Wave 1, autonomous: true) — ✅ completed 2026-05-27 (commits 789e364 / 118e8b7 / 937f959, msbuild PASS, 가드 4파일 변경 0)
- [ ] 34.1-02-PLAN.md — SIMUL UAT 7 Test (msbuild + 1-image 회귀 0 + DualImage SIMUL 3-a~3-f + INI 라운드트립 + 가드 4파일 + 3자 동시 전환 + 의사 페어 PASS) + sign-off (Wave 2, autonomous: false, CO-34-01~04 흡수)

**Carry-over (예정)**: CO-34.1-01 (Side fixture 실측 이미지 페어 확보 후 DualImage Datum 결합 실측 검증) → 장비 도착 후 v1.1 다음 회주 또는 Phase 27 에서 종결


---

### Phase 35: Side/Bottom 실측 UAT + Phase 33 마이그레이션 보강 (신설 2026-05-26)
**Goal**: Phase 33 partial sign-off carry-over 통합 — Side/Bottom 시퀀스에서 Datum 티칭 + FAI 측정이 SIMUL 실제 검증을 통과하고, Bottom Shot 재로드 가능하도록 RecipeManager 의 per-sequence Shot ownership 모델을 보강한다. Phase 33 의 SequenceHandler/INI 마이그레이션을 운영 가능한 상태로 완결.
**Depends on**: Phase 33 (partial sign-off, 코드 PASS)
**Background (Phase 33 partial sign-off carry-over)**:
  - **CO-33-02 (이미지 갱신 회귀)**: Shot 이미지 로드 후 Measurement 노드 이동 시 다른 이미지 표시. Edit 모드 최근 이미지 로드 시 ROI 이동 안 됨. Phase 32-06 fix (4ea5bcc/9c482dd) 가 cover 못한 시나리오. HalconViewerControl.LoadImage 캐시 조건 + LoadImage(HImage) 의 CurrentImagePath=null 로직 의심.
  - **CO-33-03 (Side/Bottom Datum 검출 실패)**: 사용자 보고. Datum 노드 선택 → 이미지 로드(Z=1) → ROI 티칭 → 검사 → Datum 위치 못 찾음. 작업 순서는 올바름.
  - **CO-33-04 (Side/Bottom 실측 SIMUL UAT)**: Phase 33 Test 2/3 (Side/Bottom SIMUL) + Test 4 (Top 회귀 실측) + Test 5 (INI 라운드트립 실측) — 모두 미수행
  - **CO-33-06 (Bottom Shot 재로드 실패) — 아키텍처 보강**: RecipeManager.Shots 가 글로벌 단일 리스트. ShotConfig 에 OwnerSequenceId 필드 없어서 INI 저장 후 재로드 시 Bottom Shot 이 어느 시퀀스 트리 아래 붙일지 정보 부재. Phase 33 의 마이그레이션이 구조적으로 미완성 상태였음을 노출.
**Scope (예상)**:
  1. **CO-33-06 아키텍처 보강** (가장 무거운 작업):
     - ShotConfig 에 `OwnerSequenceId` (또는 `OwnerSequenceName`) 필드 추가
     - InspectionRecipeManager.AddShot(name, seqId) overload
     - INI [SHOT_n] 섹션에 OwnerSequenceId 저장/로드
     - RebuildInspectionActions(seqId) 가 글로벌 Shots 에서 해당 시퀀스 소유 Shot 만 필터링
     - AddShotToSequence UI 핸들러가 seqNode.SequenceID 를 ShotConfig 에 전달
     - 트리 재구축 시 OwnerSequenceId 기준 분기
     - 기존 INI 호환: OwnerSequenceId 미존재 시 Top 폴백 (Phase 33 이전 동작 보존)
  2. **CO-33-02 hotfix** (이미지 갱신 회귀): HalconViewerControl 이미지 갱신 보장 + CurrentImagePath 정상화
  3. **CO-33-03 디버그** (Datum 검출 실패): CO-33-02 hotfix 후 재현. parentSeq resolution / DatumFindingService 분기 확인
  4. **CO-33-04 SIMUL UAT 5종 통합 실행**: Side / Bottom / Top 회귀 / INI 라운드트립
**Success Criteria**:
  1. Bottom Shot 추가 후 INI 저장 → 재로드 → Bottom 시퀀스 트리에 Shot 정상 복원 (CO-33-06 핵심)
  2. Side/Bottom 시퀀스에서 Datum 티칭 + FAI 측정 SIMUL 검증 PASS (Phase 33 Test 2/3)
  3. Top 회귀 0 — Phase 23.1 sign-off 시점 동작과 100% 동일 (Phase 33 Test 4)
  4. INI 라운드트립 — FIXTURE_SIDE/BOTTOM + 시퀀스별 SHOT 매핑 보존 (Phase 33 Test 5)
  5. 이미지 갱신 회귀 해소 — Shot/FAI/Measurement 노드 간 전환 시 stale cache 없음 (CO-33-02)
  6. msbuild Debug/x64 PASS, 신규 warning 0
**Plans**: TBD (~4 plans 예상 — 35-01 OwnerSequenceId 아키텍처 / 35-02 이미지 갱신 hotfix / 35-03 Datum 디버그 / 35-04 통합 SIMUL UAT)

---

### Phase 36: Datum DualImage 설계 보강 (신설 2026-05-28, Phase 34.1 CO-34.1-09 carry-over 종결)
**Goal**: DualImage (VerticalTwoHorizontalDualImage) 알고리즘에서 두 이미지의 픽셀 좌표계를 통합 처리하고, 검출된 angle 의 정확성을 사용자가 검증할 수 있는 파라미터 + Test Find 시각화를 보강하여, 실측 Side fixture 페어로 Datum 결합 워크플로우를 완결한다.
**Depends on**: Phase 34.1 (partial signed_off, swap UX + CO-34.1-08 hotfix 종결)
**Background (Phase 34.1 CO-34.1-09)**:
  - **좌표계 통합 부재**: `DatumFindingService.IntersectionLl` (L674-677 Find / L1424-1428 Teach) 가 `imageVertical/imageHorizontal` 픽셀 좌표를 변환 없이 같은 평면에 두고 교차 계산. 두 이미지가 다른 fixture 위치/시점에서 찍힌 페어인 경우 의미 없는 origin/angle 산출 → 보라색 십자 화면 밖 또는 엉뚱한 위치.
  - **각도 검증 UX 부재**: 검출된 `DetectedRefAngle` 이 PropertyGrid 의 `DetectedAngleDeg` ReadOnly 로만 노출. 사용자가 기댓값 입력해서 ±ε 비교할 수단 없음 → "정확히 되었는지 안되었는지 판단 불가" (heobum0928-art 2026-05-28 피드백).
  - **2026-05-28 실측 페어 UAT 결과**: Side fixture A1_A2 (가로) + B1 (세로) 페어로 Test Find 시 Teach 는 PASS, Find 는 검출은 됨 (`accumulated 20 edge points` 로그 확인) 하지만 시각화 결과가 사용자에게 의미 있는 위치에 표시되지 않음.
**Success Criteria**:
  1. 실측 Side fixture A1_A2 + B1 페어로 DualImage Teach + Test Find PASS — DetectedOrigin 이 시각적으로 의미 있는 위치 (anchor 명시 이미지 좌표계 기준).
  2. `DatumConfig` 에 `ExpectedAngleDeg` + `AngleTolerance` 신규 입력 가능 필드 — Test Find 시 검출 angle 과 비교해 PropertyGrid 에 PASS/FAIL 색상 배지 표시.
  3. Test Find 시각화 보강 — DetectedOrigin/Angle 오버레이가 현재 표시 이미지 좌표계 안에 그려지거나, 좌표가 화면 밖일 때 화면 중앙 fallback 십자 + 좌표 텍스트.
  4. INI 라운드트립 PASS (CO-34.1-02 잔여 Test 4 동시 종결) — 신규 필드 (ExpectedAngleDeg / AngleTolerance / anchor 메타) 직렬화.
  5. 기존 1-image algorithm 3종 + DualImage 회귀 0 (D-34-13/14 + D-34.1-07 가드 4파일 변경 0 유지).
  6. msbuild Debug/x64 PASS, 신규 warning 0.
**Plans**: TBD (~3-4 plans 예상 — 36-01 좌표계 anchor/offset 모델 + 알고리즘 보강 / 36-02 ExpectedAngleDeg + AngleTolerance UI + 시각화 / 36-03 INI 라운드트립 + 실측 페어 UAT)
**Carry-over absorbed**: CO-34.1-01 (Side fixture 실측 검증) / CO-34.1-02 (INI Test 4) / CO-34.1-09 (좌표계 + 각도 검증)

---

### Phase 24: 검사 워크플로우 end-to-end
**Goal**: Datum 티칭 후 FAI 측정 후 결과 처리 전 과정이 SIMUL_MODE 와 카메라 쪽에서 오류 없이 완주하고,
OK/NG/검사실패 각 결과에 따라 TCP 응답 + 이미지 저장 + UI 표시가 올바르게 분기된다
**Depends on**: Phase 33 (Side/Bottom 마이그레이션 prerequisite, 신설 2026-05-26)
**Requirements**: WF-01, WF-02
**Success Criteria** (what must be TRUE):
  1. SIMUL_MODE 에서 시퀀스 1회 실행 → Datum 보정 → Shot N개 Grab → FAI M개 측정 → 종합 판정 오류 없이 완주
  2. OK 판정 시 TCP OK 응답 전송 확인
  3. NG 판정 시 TCP NG 응답 + 실패 이미지 저장 + UI NG 표시 확인
  4. 검사실패(ROI 미검출) 시 TCP Error 응답 + 오류 이미지 저장 + UI 오류 표시 확인
  5. INI 하위호환 (IsDynamicFAIMode + EnsurePerRoiDefaults) end-to-end 실행 후 유지
**Plans**: TBD

---

### Phase 25: 결과 분석 & Export
**Goal**: 검사 결과 이미지를 날짜/헤더 기준으로 불러와 표현할 수 있고,
1회/50회 반복 측정값을 xlsx 로 export 하며, 알고리즘별 통계 분석화면을 조회할 수 있다
**Depends on**: Phase 24
**Requirements**: OUT-01, OUT-02, OUT-03, OUT-04
**Plans**: TBD
**UI hint**: yes

---

### ~~Phase 26: 헝가리안 전체 리팩토링~~ (v1.2 로 이연 2026-05-26)
**Goal**: 코드베이스 전체의 모든 식별자에 헝가리안 표기법을 일관되게 적용
**Depends on**: Phase 25
**Requirements**: QUAL-01
**Plans**: TBD
**Status**: **v1.2 로 이연 (2026-05-26)** — 사용자 결정. POC 납기 후 시간 여유 있을 때 진행. 운영에 미치는 영향 0 (코드 가독성/유지보수 개선 only). v1.0/v1.1 모두 헝가리안 미적용 상태로 정상 동작 중.

---

### Phase 27: Side Inspection 확장 (신설 2026-05-08, 999.1 흡수 2026-05-26)
**Goal**: PC2(Side) 전용 구성 + LineToLineAngle 알고리즘 + Side Fixture INI 추가로 Datum A vs 직선 각도 측정(D1/H5) 지원 + Datum 을 2개 이미지로 구성하는 side-only 케이스 지원
**Depends on**: Phase 25 (Phase 26 v1.2 이연으로 직전 prerequisite 가 25)
**Background (Phase 999.1 흡수 2026-05-26)**:
  - 현재 datum-finding 은 단일 이미지 전제 — `TryFindDatum`/`TryTeachDatum` 모두 `HImage` 1개만 받고, `DatumConfig` 도 단일 이미지 소스 모델
  - Side 검사 일부 케이스는 datum 을 영상 2개로 구성 필요 (Line1=image0, Line2=image1 식으로 피처 분배)
  - PC2 / Side Fixture INI 와 같은 트랜잭션으로 처리해야 INI 호환성 깨짐 없음 → Phase 27 흡수 결정
  - **사용자 케이스 명확화 (2026-05-26):** 동일 카메라에 광원과 Z-Position 만 변경하여 촬영한 이미지 2장 → 좌표계 변환 0 (동일 sensor frame 보장)

**핵심 통찰 (2026-05-26 코드 조사):**
  - `IntersectionLl` 알고리즘 자체는 image-agnostic (line endpoint 좌표만 사용) → 알고리즘 변경 0
  - `ShotConfig` 가 이미 `ZPosition` (L13) + 4 광원 enable/brightness (Ring/Back/Coax/Side, L46-61) 보유 → Z + 조명 per-Shot 모델 완비
  - `ShotConfig._image` 버퍼 (Phase 21 lifetime contract) → 다중 Shot 이미지 동시 보관 가능
  - **변경 포인트는 "라인 검출 시 어떤 Shot 의 이미지 사용하는가" 뿐** — Shot 모델/광원/Z/버퍼 인프라 모두 재활용

**Scope**:
  - PC2(Side) 전용 구성 분리
  - Side Fixture INI 신설 + 직렬화 (D1, H5)
  - LineToLineAngle 측정 타입 (Datum A vs 직선 각도)
  - Datum 2-image 구조 변경 (5 항목 — 동일 카메라 가정으로 좌표 변환 불요):
    1. `DatumConfig` 에 `Line1_SourceShotName` / `Line2_SourceShotName` 필드 추가 (빈 문자열 → 단일 이미지 모드 폴백, 회귀 0)
    2. `DatumFindingService.TryFindDatum` 다중 이미지 오버로드 신설 (`IDictionary<string, HImage> shotImages`)
    3. `TryFindTwoLineIntersect` 내부 — `ResolveLineImage(SourceShotName, shotImages, defaultImage)` 헬퍼로 Line1/Line2 각자 이미지 분기 (IntersectionLl 호출은 그대로)
    4. `Action_FAIMeasurement.GrabOrLoadDatumImage` → `GrabOrLoadDatumImages` 다중 반환 (DatumConfig 검사 후 필요한 Shot 만 로드)
    5. (선택) DatumConfig PropertyGrid 의 `Line1/2_SourceShotName` 필드를 등록된 Shot 이름 ComboBox 로 노출

**Plans**: TBD (4 plans 예상 — 27-01 LineToLineAngle / 27-02 Side Fixture INI / 27-03 Datum 2-image 구조 + 알고리즘 / 27-04 PC2 검증 + SIMUL UAT)

---

### Phase 28: FAI CircleDiameter + Datum Circle 알고리즘 통합 ✅
**Goal**: FAI CircleDiameterMeasurement 에 Datum 폴라 샘플링 + Circle_RadialDirection 파라미터 적용
**Depends on**: Phase 19
**Plans**: 4/4 ✅ signed off 2026-05-08

---

### Phase 31: Datum 기준 측정 알고리즘 확장 (신설 2026-05-19)
**Goal**: SOP(Datum_정보_260511_2D) 의 신규 측정 항목을 측정 타입으로 구현하여,
Datum 절대 좌표계 기준의 거리·각도·교점 측정을 EdgeToLineDistance 와 동일한 Shot-FAI 구조로 운용 가능하게 한다.
**Depends on**: Phase 23.1
**Background**:
  - Phase 23.1 에서 EdgeToLineDistance 가 Datum 기준 측정의 첫 타입으로 완성됨 (projection_pl 정사영 수직거리, MeasureAxis X/Y)
  - SOP 에는 원중심/호/CompoundAngle 등 추가 측정 항목 다수 — 현재 미지원
**신규 측정 타입 (SOP 코드)**:
  - **E8**: 원중심 → Datum 거리
  - **D1**: Datum 기준 각도
  - **I9 / I10**: 호(arc) ∩ 라인 교점 → Datum 거리
  - **E2 / E9 / E10**: CompoundAngle (복합 각도)
  - **ArcEdgeDistance** (G 시리즈): 호 에지 거리
**흡수 carry-over (Phase 23.1)**:
  - **CO-23.1-01**: TeachingImagePath ≠ InspectionImagePath 분리 시 UI 뷰어가 한 이미지만 표시 — datum/측정 이미지 개별 보기 필요
  - **CO-23.1-02**: EdgeToLineDistance·CircleDiameter 외 측정 타입은 Rect ROI 버튼 미활성 — ROI 티칭 배선 일반화
**SOP**: `C:\Info\Doc\2.디팜스테크\02_설계\SOP\Datum_정보_260511_2D.pptx`
**Plans**: 5 plans (5 waves)
Plans:
- [ ] 31-01-PLAN.md - Foundation: IDatumOriginConsumer 인터페이스 + ComputeProjectionDistance/TryFitArc/TryIntersectCircleLine 헬퍼 + EdgeToLineDistance/Action_FAIMeasurement 일반화 + 31-UAT scaffold (Wave 1)
- [ ] 31-02-PLAN.md - 단순 신규 타입: E8(CircleCenterDistance)/D1.H5(EdgeToLineAngle)/ArcEdgeDistance + MeasurementFactory (Wave 2)
- [ ] 31-03-PLAN.md - 복합 신규 타입: I9.I10(ArcLineIntersectDistance)/E2(CompoundAngle)/E9.E10(CompoundCenterC.BDistance) + MeasurementFactory (Wave 3)
- [ ] 31-04-PLAN.md - carry-over: CO-23.1-02 ROI 버튼 일반화 + CO-23.1-01 듀얼 이미지 뷰어 (Wave 4)
- [ ] 31-05-PLAN.md - 최종 빌드 검증 + SIMUL UAT 사인오프 (Wave 5, autonomous: false)

---

### Phase 32: 측정 알고리즘 SOP 재정합 (신설 2026-05-21)
**Goal**: Phase 31 신규 측정 타입(I9/I10/E2/E9/E10)의 알고리즘을 SOP 실무 방식으로
전면 재작성하고 E3(단축 거리) 신규 타입을 추가하여 실제 SOP 측정 절차와 일치시킨다.
**Depends on**: Phase 31
**Background**:
  - Phase 31 UAT 중 I9/I10/E2/E9/E10 측정 알고리즘이 SOP 실무 방식과 불일치 확인
  - ArcLineIntersect: 3점 호 피팅 폐기 → Rect 2개 직선 피팅 + HALCON intersection_lines 교점
  - E2/E3/E9/E10: CL1~3 원 + La/Lb 라인 체인 폐기 → 공통 컨투어 알고리즘 (reduce_domain → edges_sub_pix canny → union_adjacent_contours_xld → smallest_rectangle2_xld → shape_trans_xld → 최대면적 사각형 LargestRect)
**측정 타입 변경**:
  - **I9 / I10 (ArcLineIntersect)**: Rect 2개 → 직선 2개 교점 → Datum 거리 (명칭 유지)
  - **E2 (CompoundAngle)**: LargestRect 중심 ↔ DatumC 검출 원중심 대각선 ↔ DatumB 각도
  - **E3 (신규)**: LargestRect 단축 거리 (공차 0.600±0.030)
  - **E9 / E10 (CompoundCenterC/B)**: LargestRect 중심 → Datum C/B 거리
**신규 인프라**: VisionAlgorithmService 컨투어 알고리즘 + intersection_lines 메서드, DatumC 검출 원중심 주입 채널, canny/union 파라미터 PropertyGrid 노출, MeasurementFactory E3 등록
**이관**: Phase 31 UAT Test 3·4·5 → 본 phase
**SOP**: `C:\Info\Doc\2.디팜스테크\02_설계\SOP\Datum_정보_260511_2D.pptx` (E2 p.49, E3 p.50)
**Plans**: 8 plans
Plans:
- [x] 32-01-PLAN.md — VisionAlgorithmService 공통 컨투어 알고리즘 + intersection_lines/contours 래퍼 신설
- [x] 32-02-PLAN.md — IDatumOriginConsumer 검출 원중심 주입 채널 확장 (8 구현 클래스 영향)
- [x] 32-03-PLAN.md — ArcLineIntersect(I9/I10) 2직선 교점 재작성
- [x] 32-04-PLAN.md — E2/E9/E10 공통 컨투어 알고리즘 기반 재작성
- [x] 32-05-PLAN.md — E3 CompoundShortAxisDistance 신규 타입 + Factory 등록
- [x] 32-06-PLAN.md — MainView ROI 티칭 배선 + SIMUL UAT
- [x] 32-07-PLAN.md — overlay 전체 정합 (foot 오버로드 교체 + ADDITIVE 원칙 적용)
- [x] 32-08-PLAN.md — ArcLineIntersect 4-ROI 두 교점 평균 재설계 (I9/I10 UAT 확정)

---

## v1.2 Hardware Integration (이연)

### Phase 29: CXP SDK 확정 (구 Phase 22)
**Depends on**: 장비 도착 (6월 중순 예상)
**Requirements**: HW-01
**Plans**: TBD

### Phase 30: CXP 드라이버 통합 (구 Phase 23)
**Depends on**: Phase 29
**Requirements**: HW-02
**Plans**: TBD

### Phase 26 (이연): 헝가리안 전체 리팩토링 (v1.1 → v1.2 이연 2026-05-26)
**Goal**: 코드베이스 전체의 모든 식별자에 헝가리안 표기법을 일관되게 적용
**Depends on**: Phase 25 (v1.1)
**Requirements**: QUAL-01
**Plans**: TBD
**Reason for defer**: POC 납기 (6월 말) 우선. 코드 정리 작업으로 운영 영향 0. v1.0/v1.1 모두 헝가리안 미적용으로 정상 동작. v1.2 의 HW 통합과 묶어서 정리.

---

## Progress Table

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 18. Carry-over 정리 | 7/7 | ✅ Complete | 2026-05-07 |
| 19. PropertyGrid 동적 노출 일반화 | 2/2 | ✅ Complete | 2026-05-07 |
| 20. 코드 스타일 정리 | 8/8 | ✅ Complete | 2026-05-09 |
| 21. 메모리 이미지 버퍼 | 3/3 | ✅ Complete (signed off, hotfix a3d9545) | 2026-05-11 |
| 22. 이미지 이중화 구조 | 2/2 | ✅ Complete (signed off) | 2026-05-11 |
| 23. Top #1 A시리즈 Simul end-to-end | 3/3 | ✅ Complete | 2026-05-19 |
| 23.1. EdgeToLineDistance ROI 티칭 배선 + 다점 치수 (INSERTED) | 3/3 | ✅ Complete | 2026-05-19 |
| 33. Side/Bottom InspectionSequence 마이그레이션 | 3/3 | ⚠ PARTIAL signed off, retro 부분 sign-off 2026-05-27 (Test 3/4/5 PASS via Phase 35) | 2026-05-26 |
| 34. Datum VerticalTwoHorizontal 듀얼 티칭 이미지 | 3/4 | ⚠ PARTIAL signed off (Test 1+5 PASS, Test 3-d swap UX 갭 → Phase 34.1, Test 2/3-c/3-e/3-f/4 → 34.1 UAT 일괄) | 2026-05-27 |
| 34.1. Datum DualImage swap UX (INSERTED) | 2/2 | ⚠ PARTIAL signed off (5/7 Test PASS · Test 4 NOT-TESTED · Test 7 PARTIAL → CO-34.1-01) · UAT 도중 6 hotfix (CO-34.1-02~07) | 2026-05-27 |
| 35. Side/Bottom 실측 UAT + Phase 33 보강 | 3/3 | ⚠ PARTIAL signed off (5/6 UAT PASS, CO-35-01/02 hotfix, Test 4 Side carry-over → Phase 34.1 연장) | 2026-05-27 |
| 24. 검사 워크플로우 end-to-end | 0/TBD | ⏳ Planned (Top/Bottom prerequisite 충족, Side 는 Phase 34.1 후) | - |
| 25. 결과 분석 & Export | 0/TBD | ⏳ Planned | - |
| 27. Side Inspection 확장 | 0/TBD | ⏳ Planned | - |
| ~~26. 헝가리안 전체 리팩토링~~ | — | ⏭ Deferred → v1.2 | (2026-05-26 이연) |
| 28. FAI CircleDiameter + Datum Circle | 4/4 | ✅ Complete | 2026-05-08 |
| 31. Datum 기준 측정 알고리즘 확장 | 5/5 | ✅ Complete (signed off, CO-31-01 carry-over) | 2026-05-26 |
| 32. 측정 알고리즘 SOP 재정합 | 8/8 | ✅ Complete (UAT PASS) | 2026-05-23 |
| **v1.2** | | | |
| 29. CXP SDK 확정 (구 Phase 22) | 0/TBD | ⏳ Deferred | - |
| 30. CXP 드라이버 통합 (구 Phase 23) | 0/TBD | ⏳ Deferred | - |

*v1.1 roadmap updated: 2026-05-27 — Phase 34.1 PARTIAL signed_off (5/7 Test PASS, Plan 01+02 종결). UAT 도중 6개 hotfix 발견 + 종결 (CO-34.1-02 Visibility wiring + _editingDatum → _selectedDatumForSwap / CO-34.1-03 frozen brush / CO-34.1-04 Halcon airspace 우회 (토글 자체 axis 색상) / CO-34.1-05 ROI 항상 표시 / CO-34.1-06 RenderDatumOverlay DualImage 갱신 / CO-34.1-07 Load Image 모드별 분기). 가드 4파일 변경 0 유지. CO-34.1-01 carry-over (Side fixture 실측 페어 + 캘리브레이션) = v1.1 다음 회주 또는 Phase 27 종결. 학습: Phase 34 D-34-05 enum 추가 시 분기 갱신 누락 사이트 2곳 (whitelist + RenderDatumOverlay) — 후속 phase plan-checker 체크리스트에 "enum 사용 site grep 검증" 추가 권장. v1.1 잔여 = 24 → 25 → 27.*

*v1.1 roadmap updated: 2026-05-27 — Phase 34.1 planning 완료 (2 plans, 2 waves: 01=swap UI / 02=SIMUL UAT sign-off). D-34.1-01~17 분배: Plan 01=12 IDs / Plan 02=6 IDs (D-34.1-15 양쪽 중복 검증). 변경 가드 4파일 (DatumConfig / VisionResponsePacket / InspectionSequence / Action_FAIMeasurement) 0 변경 유지. Cal_Image 가 비어 있어 SIMUL 의사 페어는 README 가이드 + 사용자 수동 배치 (Plan 02 사전 단계).*

*v1.1 roadmap updated: 2026-05-27 — Phase 34 PARTIAL signed off + Phase 34.1 (Datum DualImage swap UX) 신설. UAT 도중 사용자 피드백 — 자동 swap 만으로는 현재 표시 이미지를 시각적으로 알 수 없고 임의 swap 불가 → ROI 그리기 신뢰성 확보 불가. 결정: 수동 swap (PropertyGrid [👁] 아이콘) + 캔버스 우상단 배지 (가로축 파랑 / 세로축 주황). CO-34-01~04 + Phase 35 Test 4 Side carry-over → 34.1 흡수. v1.1 잔여 = 34.1 → 24 → 25 → 27.*

*v1.1 roadmap updated: 2026-05-28 — Phase 34.1 실측 페어 UAT 도중 CO-34.1-08 hotfix (BtnTestFindDatum_Click DualImage 분기 누락, 61d407a) + CO-34.1-09 신규 carry-over (DualImage 좌표계 통합 부재 + 각도 검증 UX 부재). Phase 36 (Datum DualImage 설계 보강) 신설 — Phase 35 다음, Phase 24 직전 삽입. v1.1 잔여 = 36 → 24 → 25 → 27. (gsd-sdk phase.add CLI 가 Phase 1 으로 잘못 할당 → 수동 삽입, project_gsd_insert_phase_cli_bug 재확인.)*

*v1.1 roadmap updated: 2026-05-26 — Phase 33 신설 (Side/Bottom InspectionSequence 마이그레이션). 코드 조사 결과 SequenceHandler L30-34 에서 Top 만 InspectionSequence 사용, Side/Bottom 은 레거시 TopSequence/BottomSequence → DatumConfigs 부재 → Side/Bottom 에서 Datum 형성 구조적으로 불가. Phase 24 (검사 워크플로우 end-to-end) 의 prerequisite 로 등록 — Side/Bottom 검사가 안 되면 end-to-end 검증 불가. v1.1 잔여 = 33 → 24 → 25 → 27.*
*v1.1 roadmap updated: 2026-05-26 — Phase 22 retro 동기화. 22-UAT.md 가 2026-05-11 에 이미 signed_off (4/4 PASS) 였으나 ROADMAP 표 미갱신 상태였음 — 표 및 체크박스 동기화 완료. quick 260526-kay (EdgeSelection 차단 해제 3군 일괄) UAT PASS (사용자 3/3 2026-05-26).*
*v1.1 roadmap updated: 2026-05-26 — Phase 26 (헝가리안 리팩토링) v1.2 로 이연. 사용자 결정 — POC 납기 우선, 코드 정리 작업이라 운영 영향 0. Phase 27 (Side Inspection) 의 Depends on 을 Phase 26 → Phase 25 로 변경. v1.1 잔여 = Phase 22 + 24 + 25 + 27. v1.2 = CXP HW + 헝가리안 리팩토링.*
*v1.1 roadmap updated: 2026-05-26 — Phase 27 Datum 2-image scope 구체화 (사용자 케이스: 동일 카메라, 광원+Z 만 변경한 이미지 2장 → 좌표계 변환 0). 코드 조사 결과 ShotConfig 가 이미 ZPosition + 4 광원 + 이미지 버퍼 보유 (인프라 재활용) — 변경 포인트는 "라인 검출 시 어떤 Shot 이미지 사용" 1점에 집중. 5 변경 사항 (DatumConfig 필드 + TryFindDatum 오버로드 + TryFindTwoLineIntersect 분기 + GrabOrLoadDatumImages 다중 반환 + UI ComboBox) 명문화. Plan 작성은 /gsd-plan-phase 27 실행 시점.*
*v1.1 roadmap updated: 2026-05-26 — Phase 999.1 (Datum 2-image side 지원) → Phase 27 ABSORBED. Phase 27 스코프 4 항목 (LineToLineAngle + Side Fixture INI + PC2 분리 + Datum 2-image), plans 3→4 plans 로 확장. INI 호환성 + side 컨텍스트 동일 + backlog 원문 권장 근거.*
*v1.1 roadmap updated: 2026-05-26 — Phase 31 SIGNED OFF. Test 1/2/6 (E8/D1·H5/ArcEdge) Phase 31 PASS + Test 3/4/5 (I9·I10/E2/E9·E10) Phase 32 transferred (SIGNED_OFF 2026-05-23) + Test 7 (ROI 버튼) retro PASS + Test 8 (듀얼 이미지 레이블) 사용자 3-step PASS + Test 9 msbuild PASS. CO-31-01 신규 carry-over (PropertyGrid 양방향 즉시 갱신 미작동 — Name 4종 plain auto-property INotifyPropertyChanged 부재).*
*v1.1 roadmap updated: 2026-05-26 — Phase 21 SIGNED OFF retro-marked. 21-UAT.md 가 2026-05-11 에 이미 signed_off (4 테스트 — Test 1 verified / Test 2 PASS hit=7 / Test 3 not_tested→Phase 23 / Test 4 PASS, hotfix a3d9545) 였으나 21-03-SUMMARY.md / ROADMAP 표 미갱신 상태였음 — Plan 03 마무리 작성 완료.*
*v1.1 roadmap updated: 2026-05-23 — Phase 32 SIGNED OFF (32-UAT.md 전 항목 PASS, 사용자 approved). gsd-verifier goal-backward 검증 6/6 truths + 14 artifacts + 10 key_links + 8 threat mitigations 모두 확인. UAT 직전 quick 260523-j72 가 E3 알고리즘을 사용자 reference HALCON 일치화(b3dd847/c95982d/af07972). Phase 32 complete.*
*v1.1 roadmap updated: 2026-05-21 — Phase 32 추가 (측정 알고리즘 SOP 재정합 — I9/I10/E2/E9/E10 재작성 + E3 신규). gsd-sdk phase.add CLI 가 다시 phase_number 오산정(1) → 수동 보정 (다음 정수 = 32).*
*v1.1 roadmap updated: 2026-05-19 — Phase 31 추가 (Datum 기준 측정 알고리즘 확장 — E8/D1/I9·I10/CompoundAngle/ArcEdgeDistance + CO-23.1-01·02 흡수). gsd-sdk phase.add CLI 가 phase_number 오산정(1) → 수동 보정 (다음 정수 = 31).*
*v1.1 roadmap updated: 2026-05-19 — Phase 23.1 SIGNED OFF (23.1-UAT.md 8/8 PASS, 대화형 UAT). Phase 23 도 동시 최종 sign-off (D-14). CO-23-01 resolved, CO-23.1-01·02 → 신규 알고리즘 Phase.*
*v1.1 roadmap updated: 2026-05-17 — Phase 23.1 plan breakdown 완료 (3 plans, 3 waves — 23.1-01 EdgeSelection 차단 / 23.1-02 ROI 티칭 배선 / 23.1-03 UAT sign-off).*
*v1.1 roadmap updated: 2026-05-17 — Phase 23.1 삽입 (INSERTED, after Phase 23) — EdgeToLineDistance ROI 티칭 배선 + 다점 치수 지원 (SOP 갭 대응, urgent).*
*v1.1 roadmap updated: 2026-05-11 — Phase 22/23 재편 (이미지 이중화 + A시리즈 Simul), HW phases → v1.2 이연. Phase 22 plans 2/2 작성 (22-01, 22-02 — 2026-05-11).*

## Backlog

### Phase 999.1: Datum 2-image 지원 (side 검사) — ✅ ABSORBED to Phase 27 (2026-05-26)

**Status:** Phase 27 (Side Inspection 확장) 스코프로 흡수됨 — 2026-05-26 결정.
**Reason:** Side 검사 컨텍스트 동일 + PC2/Side Fixture INI 와 같은 트랜잭션으로 처리해야 INI 호환성 보장.
**See:** Phase 27 본문 §"Background (Phase 999.1 흡수 2026-05-26)" 와 §"Scope" 항목 4 "Datum 2-image 구조 변경".

**원 backlog 본문 (보존, 2026-05-17 등록):**
> side 검사에서 datum 을 영상 2개로 구성해야 하는 경우 지원. 현재 datum-finding 은 단일 이미지 전제 — `TryFindDatum`/`TryTeachDatum` 모두 `HImage` 1개만 받고, `DatumConfig` 도 단일 이미지 소스 모델(`ImageSourceMode`/`SourceShotName`/`TeachingImagePath` 각 1개). 2-image datum 은 구조 변경 필요: ①`DatumConfig` 다중 이미지 참조 필드 ②`TryFindDatum`/`TryTeachDatum` 시그니처 확장(`IEnumerable<HImage>` 또는 오버로드) ③3개 datum 알고리즘(TwoLineIntersect/CircleTwoHorizontal/VerticalTwoHorizontal)이 이미지 간 피처 융합(예: Line1=image0, Line2=image1) ④`Action_FAIMeasurement.GrabOrLoadDatumImage` 다중 이미지 취득 ⑤단일 이미지 하위호환(기본 오버로드/팩토리). 출처: 사용자 설계 질문 2026-05-17 (quick 260517-l5e 후속).
