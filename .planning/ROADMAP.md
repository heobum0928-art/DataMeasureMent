# Roadmap: DataMeasurement

## Milestones

- **v1.0 Halcon Migration MVP** — ✅ Shipped 2026-05-04. Archive: [milestones/v1.0-ROADMAP.md](milestones/v1.0-ROADMAP.md) (17 phases / 55 plans)
- **v1.1 Quality + Workflow + Algorithm** — ✅ Shipped 2026-05-28. Archive: [milestones/v1.1-ROADMAP.md](milestones/v1.1-ROADMAP.md) · [REQUIREMENTS](milestones/v1.1-REQUIREMENTS.md) · [AUDIT](v1.1-MILESTONE-AUDIT.md)
  - 17 phases (18~38, inserts 23.1/34.1). Quality(QUAL-02/03/04)+Buffer+Image-dual+Algorithm/Datum 대거 확장 완료.
  - v1.2 이연: WF-01/02, OUT-01~04, HW-01/02, QUAL-01. 부분: ALG-01(CO-23-01).
- **v1.2 POC Workflow + Output + Carry-over + Protocol v2.7** — ◷ Active (started 2026-05-29, POC 납기 2026-06-30)
  - 13 phases (39~50 + 39.1/39.2 insert), 5순위 우선순위 구조. continue numbering 모드.

---

## v1.2 Phases (Phase 39부터)

> POC 6월 말 기준 5단계 우선순위. 1~2순위 완료 전 5순위 착수 금지.

### Phase 39: 검사 워크플로우 E2E (신설 2026-05-29)
**Goal**: Datum 검출 → FAI 측정 → 결과 처리(OK/NG/검출 실패)까지 1 사이클이 SIMUL 모드와 실카메라(HIK) 모두에서 끊김 없이 통과하고, OK·NG·검출 실패 3분기 후속 동작 명세를 v2.6 프로토콜 기준으로 적용한다.
**Depends on**: Phase 37 (Side multi-datum), Phase 38 (v1.1 carry-over cleanup) — 둘 다 signed_off
**Requirements**: WF-01, WF-02
**Background**: v1.1 까지 Datum/FAI 알고리즘은 안정화 완료(Phase 23~37). 그러나 sequence-level E2E 경로는 부분 검증만 됐고 "Datum 검출 실패 vs 측정 실패" 분기, NG 누적 정책, 검출 실패 시 후속 측정 skip 처리가 sequence 마다 산발적으로 다르다. POC 시연 전에 1 사이클 전체가 한 정책으로 통일되어야 한다.
**Scope**:
  - **Datum 단계 분기**: Datum 검출 실패 → 즉시 NG + 후속 FAI skip + TCP 결과 응답
  - **FAI 단계 분기**: FAI 측정 실패(에지 미검출/공차 초과) → NG mark, 다음 FAI 계속 진행
  - **사이클 종합 판정**: OK = 전 항목 PASS, NG = 1 건이라도 FAIL, 검출 실패 = Datum 단계 실패 (별도 분기)
  - **TCP 결과 응답 포맷**: 현행 v2.6 프로토콜 유지 (v2.7 은 Phase 48 별도)
  - **SIMUL + 실카메라 양쪽 검증**: SIMUL 이미지 셋(Cal_Image/) + HIK 실카메라(가능 시) 회귀
  - **NG 누적 처리**: 사이클 내 NG 발생 후 후속 측정 정상 진행, 사이클 종료 시 종합 판정
**Success Criteria (UAT)**:
  - SIMUL Top/Side/Bottom 멀티샷 → Datum 검출 → 전 FAI 측정 → TCP 응답까지 끊김 없음
  - Datum 검출 실패 케이스: 후속 FAI skip + TCP NG 응답 (검출 실패 코드)
  - FAI 측정 실패 케이스: 해당 FAI NG mark, 다음 FAI 계속, 사이클 종합 NG
  - 전 항목 PASS 케이스: TCP OK 응답
  - 사이클 내 NG 2건 이상 케이스: 모두 누적, 사이클 종합 NG, 결과 분석에 전부 노출

- [x] **Phase 39: 검사 워크플로우 E2E** — Datum→FAI→결과 OK/NG/검출 실패 분기 (WF-01, WF-02) — SIGNED_OFF 2026-05-29 (5/5 UAT PASS + 3 hotfix)
  - Success: SIMUL+실카메라 모두 Datum 검출→FAI 측정→TCP 결과 응답 끊김 없음 / OK·NG·검출실패 3분기 후속 동작 명세 적용 / 사이클 내 NG 누적 처리 안정
  - **Plans:** 4 plans (3 waves)
    - [x] 39-01-PLAN.md — Per-FAI datum gate + LastSkipReason 데이터 모델 (D-01, D-02, D-07 가드) [Wave 1]
    - [x] 39-02-PLAN.md — 3-state cycle hierarchy + TCP wire 매핑 (D-03, D-05, D-06, D-08, D-10) [Wave 2]
    - [x] 39-03-PLAN.md — UI overlay DETECT FAIL 라벨 + Datum 노드 INPC 배지 (D-04) [Wave 2]
    - [x] 39-04-PLAN.md — SIMUL UAT 5 시나리오 + sign-off (D-09) [Wave 3]

### Phase 39.1: 검사 워크플로우 긴급 fixes (신설 2026-05-29)
**Goal**: Phase 39 sign-off 직후 사용자가 직접 보고한 4 긴급 항목 (algorithm 2 + UI 2) 을 단일 phase 안에서 처리한다. POC 2026-06-30 시연 측정 정확도 직접 영향 = #1 CircleDiameter polar 파라미터 노출 + #2 EdgeToLineDistance projection_pl 축 정확화. UI 편의성 = #3 검사 후 FAI 노드 클릭 결과 재현 + #4 Datum CTH Edit 모드 분리.
**Depends on**: Phase 39 (signed_off)
**Requirements**: WF-01
**Scope** (CONTEXT.md 16 결정 G1~G4 lock):
  - **#1 CircleDiameter**: 4 polar 필드 (Circle_PolarStepDeg / RectL1Ratio / RectL2Ratio / PolarEdgeSelection) 인스턴스화 + ICustomTypeDescriptor 동적 hide/show + ctor idempotent migration. Datum CTH 변경 0 anti-goal (D-G2-05).
  - **#2 EdgeToLineDistance**: measureX 분기에서 DatumAngle2Rad (실 datum 수직 기준선) 사용. measureY + Datum 미주입 폴백 변경 0. TLI Datum 폴백 보장 (D-G3-04).
  - **#3 FAI 노드 조회**: 검사 후 노드 클릭 시 측정 결과 + 이미지 + overlay 재현. Sequence 동작 변경 0 (Phase 37 lenient + Phase 39 gate 회귀 0). 전 FAI 타입 공통 (D-G4-02).
  - **#4 Datum CTH Edit**: btn_teachDatum 토글 기반 분리 — fitting 원 ↔ Edit ROI 핸들. DETECT FAIL 라벨 (Phase 39 CO-39-02/03) + fitting 원 (Phase 11/13/16) + RenderDatumFindResult (Phase 36 CO-36-03) 변경 0.
**Success Criteria (UAT)**:
  - Item #1: polar 4 필드 PropertyGrid 노출 + 변경 시 측정값 결정적 변화 + Datum CTH 회귀 0
  - Item #2: 부정확 직각 케이스 정확화 (Δ1 < Δ0) + measureY/TLI/미주입 폴백 회귀 0
  - Item #3: FAI/Measurement 노드 클릭 시 6 타입 공통 overlay 재현 + Sequence 회귀 0
  - Item #4: CTH 평소 모드 fitting 원만 + Edit 모드 ROI 핸들 동시 + VTH/TLI/Phase 36 swap 회귀 0

- [x] **Phase 39.1: 검사 워크플로우 긴급 fixes** — algorithm 2 + UI 2 (WF-01) — SIGNED_OFF 2026-05-29 (4/4 UAT PASS + CO-39.1-01 hotfix 2 rev)
  - Success: 4 항목 SIMUL UAT PASS + Phase 28/31/36/37/39/11/13/16/23/23.1 회귀 0
  - **Plans:** 4 plans (3 waves) — 원래 의도 2 wave (algorithm + UI) 였으나 MainView.xaml.cs file overlap 으로 UI 도 2 wave 로 분리
    - [x] 39.1-01-PLAN.md — Item #1 CircleDiameter 4 polar 필드 노출 + ICustomTypeDescriptor (D-G2-01~05) [Wave 1, algorithm]
    - [x] 39.1-02-PLAN.md — Item #2 EdgeToLineDistance measureX DatumAngle2Rad (D-G3-01~04) [Wave 1, algorithm]
    - [x] 39.1-03-PLAN.md — Item #3 FAI 노드 조회 overlay 재현 (D-G4-01~02) [Wave 2, UI]
    - [x] 39.1-04-PLAN.md — Item #4 Datum CTH Edit 모드 분리 (D-G4-03~05) [Wave 3, UI — Plan 03 file overlap 해소]

### Phase 39.2: 긴급 추가건2 (신설 2026-05-30)
**Goal**: Phase 39.1 SIGNED_OFF 직후 사용자 발의 4 신규 항목 — Bottom E5 듀얼 이미지 FAI 측정 + Top I10 close-point variant + Tree 정렬 + Tree 아이콘 차별화 — 단일 phase 처리. POC 2026-06-30 시연 대비 사용자 UX + 측정 커버리지 보강.
**Depends on**: Phase 39.1 (signed_off)
**Requirements**: WF-01 (FAI 측정 확장 + UI UX)
**Scope** (CONTEXT.md 6 결정 D-G1~D-G4 lock):
  - **#1 Bottom E5 DualImage FAI**: 신규 `DualImageEdgeDistanceMeasurement` MeasurementBase 서브타입 — Phase 37 VTH-DualImage 패턴 차용 (TeachingImagePath / TeachingImagePath_Vertical 슬롯 재사용 + 2 ROI + 양 이미지 에지 검출 → projection_pl 거리). Datum DualImage / Phase 37 lenient 회귀 0.
  - **#2 Top I10 close-point variant**: `ArcLineIntersectDistanceMeasurement` 에 `IntersectionPointSelection` 문자열 파라미터 ({Far, Close}, default=Far) 추가 — INI 회귀 0 + ItemsSourceProperty 콤보 노출. 신규 타입 도입 0 (단일 소스).
  - **#3 Tree 노드 정렬**: Shot/FAI/Datum/Measurement 트리 노드 Name 자연정렬 (Shot10 vs Shot2 정렬) 자동. 전 레벨 + 정렬 시점 = Add* / Rename / RebuildTree / Constructor.
  - **#4 Tree 아이콘 차별화**: 5 NodeType + 12 Measurement TypeName 별 Geometry path 아이콘 — Material Icons SVG path inline + IValueConverter + DynamicResource lookup. ContextMenu PNG 보존.
**Success Criteria (UAT)**:
  - Item #1: Bottom E5 2 이미지 / 2 ROI 거리 측정 성공 + Action_FAIMeasurement / 기존 10 FAI 타입 회귀 0
  - Item #2: P1 / P2 close/far 선택 시 정확 거리값 + INI 하위호환 (기본 Far → Phase 32 sign-off 결과 byte-identical)
  - Item #3: Shot 생성 순서 무관 자연정렬 표시 + Rename 시 즉시 재정렬 + ParamBase INI 순서 변경 0
  - Item #4: FAI vs Measurement 아이콘 시각 차별 + Measurement TypeName 별 아이콘 다름 + 노드 텍스트 변경 0

- [~] **Phase 39.2: 긴급 추가건2** — DualImage FAI + I10 close-point + Tree 정렬 + Tree 아이콘 (WF-01) — **PARTIAL_SIGNED_OFF 2026-05-30** (3 PASS / 1 carry-over / 1 skipped, 2 hotfix)
  - Success: 4 항목 SIMUL UAT 중 D-G2 / D-G3(hotfix) / D-G4(hotfix) PASS, D-G1 → Phase 39.3 이연
  - **Plans:** 5 plans (3 waves)
    - [x] 39.2-01-PLAN.md — D-G1 DualImageEdgeDistanceMeasurement 신규 (코드 OK, UAT FAIL — RectROI 비활성 + 가로/세로 이미지 2슬롯 UX 미적용 → CO-39.2-01-01 → Phase 39.3 신설)
    - [x] 39.2-02-PLAN.md — D-G2 ArcLineIntersect IntersectionPointSelection Far/Close (UAT PASS)
    - [x] 39.2-03-PLAN.md — D-G3 Tree 정렬 (자동정렬 → 사용자 Move ▲▼ 재설계, hotfix 74f608b, UAT PASS)
    - [x] 39.2-04-PLAN.md — D-G4 Tree Geometry 아이콘 18 종 (App.xaml 이동 hotfix df6f711, UAT PASS)
    - [x] 39.2-05-PLAN.md — SIMUL UAT 결과 기록 + partial sign-off (3 PASS / 1 carry-over / 1 skipped)

### Phase 39.3: DualImage FAI UX 재설계 (신설 2026-05-30 — CO-39.2-01-01 carry-over)
**Goal**: Phase 39.2 D-G1 (Bottom E5 DualImage FAI) UAT FAIL 후속 — `DualImageEdgeDistanceMeasurement` 의 측정 알고리즘은 유지, UI/UX 를 Side Datum DualImage 패턴 (가로/세로 버튼 + 이미지 2 슬롯 + RectROI 2개 활성) 으로 재설계.
**Depends on**: Phase 39.2 (partial signed_off — 39.2-01 코드 baseline 유지)
**Requirements**: WF-01 (FAI 측정 확장 UX)
**Scope (CONTEXT.md 6 lock decisions D-G1~D-G5):**
  - DualImage 측정 입력 UI 가 기존 Side Datum DualImage (Phase 34.1 / 37) UX 와 동일하게 동작 (가로/세로 이미지 슬롯 + 2 RectROI 셋팅 + 슬롯 swap)
  - RectROI drawable attribute 가 PropertyGrid 에서 활성화 (현재 비활성 원인 분석 → 39.2-01 Plan 의 attribute / interface 누락 보완)
  - 측정 알고리즘 변경 0 (projection_pl 거리, PointROI/LineROI 처리, IDatumOriginConsumer 보존)
  - INI 회귀 0 (39.2-01 sign-off baseline 유지)
  - Datum DualImage (Phase 36/37) / FAI 기존 10 타입 / Phase 39.1 회귀 0
**Success Criteria (UAT)**:
  - Bottom E5 DualImage FAI Measurement Type 선택 → 가로/세로 이미지 슬롯 노출 + 양 슬롯 별도 ROI 셋팅 가능
  - 양 ROI 모두 RectROI drawable + edit 모드 정상 동작
  - 측정 결과 (mm) 가 Phase 39.2-01 코드 baseline 과 동일 알고리즘으로 산출
  - Datum DualImage / 기존 FAI / 39.1 회귀 0

- [~] **Phase 39.3: DualImage FAI UX 재설계** — Side Datum DualImage 패턴 차용 + RectROI 활성화 (WF-01, CO-39.2-01-01) — **PARTIAL_SIGNED_OFF 2026-05-30** (Test 1-3 PASS + 회귀 C PASS / Test 4 + 회귀 A/B/D/E NOT_TESTED → Phase 39.4 흡수)
  - Success: Bottom E5 DualImage FAI UAT 동작 확인 (CO-39.2-01-01 종결) + 측정 알고리즘 변경 0 + Anti-Goal 10/10 ✅
  - **Carry-over:** CO-39.3-01 (DualImage Shot 이미지 점유 → 작업자 인지 혼동) → Phase 39.4
  - **Plans:** 4 plans (3 waves)
    - [x] 39.3-01-PLAN.md — RectROI 활성화 baseline: isRectRoiType + FindSelectedRectMeasurement + CommitRectRoi + BuildPointRoiDefinitions 4 분기 (D-G1 + D-G5) [Wave 1]
    - [x] 39.3-02-PLAN.md — Swap UX wiring: _selectedDualImageMeasurement mutex + PublishMeasurementDualImageSelection + BtnSwap*_Click Measurement 우선순위 + UpdateImageSourceBadge publish (D-G2, Datum DualImage 패턴 차용) [Wave 2]
    - [x] 39.3-03-PLAN.md — TeachingImagePath_Vertical [InputFilePath] + [AutoUpdateText] Browse 버튼 (D-G3) [Wave 1, Plan 01 과 다른 파일]
    - [x] 39.3-04-PLAN.md — SIMUL UAT (partial sign-off 2026-05-30, Test 4 + 회귀 → Phase 39.4 흡수)

### Phase 39.4: Bottom DualImage 수동 Swap UX 재설계 (신설 2026-05-30 — CO-39.3-01 carry-over)
**Goal**: Phase 39.3 Test 2 (Swap UX) PASS 후 사용자 발견 결함 — Shot 이미지가 공통 자원인데 DualImage Measurement 의 "가로축 티칭 이미지" 로 단독 점유되어 작업자 인지 혼동 발생. DualImage 가로/세로 양측 모두 Measurement 단위 명시 경로 (`TeachingImagePath_Horizontal` 신규 + 기존 `TeachingImagePath_Vertical` 유지) 로 재설계 + Datum DualImage (Phase 22 IMG-01 / Phase 37) 패턴 일관화.
**Depends on**: Phase 39.3 (PARTIAL_SIGNED_OFF)
**Requirements**: WF-01
**Scope (estimated, discuss-phase 에서 lock):**
  - DualImageEdgeDistanceMeasurement.TeachingImagePath_Horizontal 신규 필드 + [InputFilePath] + [AutoUpdateText] (Plan 39.3-03 mirror)
  - Action_FAIMeasurement.TryGrabOrLoadFaiDualImages 분기 교체 (RuntimeImageA 소스 = meas.TeachingImagePath_Horizontal, fallback = ShotConfig 이미지)
  - MainView.BtnSwapHorizontal_Click Measurement 분기 교체 (가로축 = meas.TeachingImagePath_Horizontal 로드, fallback = ShotConfig)
  - PropertyGrid 가로/세로 셀 라벨 명시 + Browse 버튼 둘 다 노출
  - **39.3 D-G4 anti-goal ("Action_FAIMeasurement 본문 변경 0") 은 39.4 의 새 contract 로 해제**
**Success Criteria (UAT)**:
  - DualImage Measurement PropertyGrid 에 가로/세로 Browse 버튼 둘 다 노출 + 라벨 명시
  - Horizontal swap 시 meas.TeachingImagePath_Horizontal 이미지 로드 (fallback = ShotConfig)
  - Vertical swap 시 meas.TeachingImagePath_Vertical 이미지 로드 (39.3 baseline)
  - 측정값 byte-identical (39.2-01 baseline)
  - 회귀 0: Datum DualImage / 기존 7 Measurement / Phase 39.1~39.3 D-G1/G2/G5

- [~] **Phase 39.4: Bottom DualImage 수동 Swap UX 재설계** — DualImage 양측 명시 경로 + Datum 패턴 일관화 (WF-01, CO-39.3-01 → 종결) — **PARTIAL_SIGNED_OFF 2026-05-31** (Test 1~4 + Verify A 5/5 PASS + 1 hotfix CO-39.4-01 = `6843c0d`, Verify B/D/E + INI 호환 = CO-39.4-02 carry-over)
  - Success: DualImage 가로/세로 양측 명시 경로 + 측정값 byte-identical + Datum 회귀 0 — ✅ 핵심 가치 달성
  - **Carry-over:** CO-39.4-02 (Verify B/D/E + INI 호환 회귀 smoke, 회귀 위험 LOW — Anti-Goal 자동 검증 10/10 PASS)
  - **Plans 4 / Waves 3** (Wave 1: Plan 01 / Wave 2: Plan 02 + 03 sequential / Wave 3: Plan 04 UAT)
    - [x] 39.4-01-PLAN.md — DualImageEdgeDistanceMeasurement TeachingImagePath_Horizontal + 5종 attribute (5cf4fb3)
    - [x] 39.4-02-PLAN.md — Action_FAIMeasurement.TryGrabOrLoadFaiDualImages pathA D-G1 fallback if/else (484819e)
    - [x] 39.4-03-PLAN.md — MainView.BtnSwapHorizontal_Click + UpdateImageSourceBadge D-G1+G4 (85d8c92)
    - [x] 39.4-04-PLAN.md — SIMUL UAT + sign-off (d32a45b → f2149c8 → 6843c0d hotfix → eb6d4cb → b3399de)

### Phase 40: 결과 분석 & Export I — 리뷰어 + 1회 검사 엑셀 (신설 2026-06-01)
**Goal**: 검사 완료된 결과를 사후에 검토·추출할 수 있는 출력 계층을 구축한다. (1) 날짜/원본 폴더 단위로 과거 검사 결과를 로드하여 결과 이미지(에지·overlay·판정)를 재현하는 **리뷰어**(OUT-01), (2) 시퀀스 1회 검사 결과를 메타데이터+측정값+판정+이미지 링크가 포함된 **xlsx 파일로 export**(OUT-02).
**Depends on**: Phase 39 (검사 워크플로우 E2E — OK/NG/검출실패 3분기 + TCP 결과) signed_off, Phase 39.4 (DualImage swap) partial
**Requirements**: OUT-01, OUT-02
**Plans**: 4 plans (3 waves) — planned 2026-06-01
Plans:
- [x] 40-01-PLAN.md — cycle 결과 JSON 영속화 토대 (CycleResultDto + CycleResultSerializer + AddResponse wiring)
- [x] 40-02-PLAN.md — ClosedXML 0.105.0 + 전이 의존성 등록 + 런타임 smoke test [BLOCKING]
- [x] 40-03-PLAN.md — 결과 리뷰어 Window (날짜폴더 → cycle 목록 → 이미지/overlay 재렌더 + 측정표, OUT-01)
- [~] 40-04-PLAN.md — ExcelExportService + 리뷰어 [엑셀 export] 버튼 (OUT-02) — **코드 완료, 빌드 PASS, UAT 대기**(2026-06-09)
**Background**: 측정 알고리즘은 Phase 23~39 에서 안정화 완료. 그러나 검사 결과는 현재 라이브 화면 + `RawImageSaveService` 의 원본 이미지 저장만 존재하고, (a) 저장된 결과를 사후에 다시 불러와 검토하는 경로, (b) 측정값/판정을 정형 데이터(xlsx)로 추출하는 경로가 없다. POC 2026-06-30 시연에서 "검사 → 결과 리뷰 → 엑셀 추출" 흐름이 필요.
**Scope**:
  - **OUT-01 결과 리뷰어**: 날짜/원본 폴더 선택 → 저장된 결과 이미지 + overlay + 판정 재현 (UI 위치 TBD — 별도 창 vs MainView 탭)
  - **OUT-02 1회 엑셀 export**: 시퀀스 1회 검사 결과 → xlsx (메타데이터 + 측정값 mm + 판정 OK/NG + 이미지 링크)
  - **Excel 라이브러리 선정**: 현 의존성에 엑셀 라이브러리 없음 (.NET 4.8 호환 + 라이선스 고려)
  - **결과 저장 포맷/폴더 구조**: 리뷰어가 읽을 결과 폴더 레이아웃 (`RawImageSaveService` / `SaveResultImage` 연계)
  - **이미지 링크 방식**: xlsx 내 하이퍼링크 vs 셀 임베드
**Out of scope** (Phase 41.1 — OUT-03/04):
  - 50회 반복도 통계 (mean/stddev/range/Cpk)
  - 검출 알고리즘별 통계 분석표
**Success Criteria (UAT)**:
  - 날짜/원본 폴더 로드 시 결과 이미지 + overlay + 판정이 라이브 검사와 동일하게 재현
  - 1회 검사 결과 xlsx 생성 (메타 + 측정값 + 판정 + 이미지 링크 모두 포함)
  - 생성된 xlsx 가 외부 도구(Excel) 에서 정상 열림

- [ ] **Phase 40: 결과 분석 & Export I — 리뷰어 + 1회 검사 엑셀** (OUT-01, OUT-02)
  - Success: 날짜/원본 폴더 로드 시 결과 이미지 재현 / 1회 검사 결과 xlsx 생성 (메타+측정값+판정+이미지 링크)
- [ ] **Phase 41.1: 결과 분석 & Export II — 50회 반복도 + 알고리즘 통계** (OUT-03, OUT-04) — ⏸ DEFERRED 2026-06-16 (3 plans 계획 완료/실행 0건, 검증용 반복 이미지 부족 → 이미지 확보 후 재개)
  - Success: 50회 반복 시퀀스 자동 실행 + mean/stddev/range/Cpk xlsx / 알고리즘별(TLI/CTH/VTH/Edge 6종+) 통계 표 생성

### Phase 41.1: 결과 분석 & Export II — 50회 반복도 + 알고리즘 통계 (신설 2026-06-12)
**Goal**: 50회 반복 측정 사이클을 자동 실행하고 반복도 통계(mean/stddev/range/Cpk)와 알고리즘별 통계 분석표를 xlsx로 export한다.
**Depends on**: Phase 40 (Export I — 리뷰어 + 1회 엑셀) signed_off
**Requirements**: OUT-03, OUT-04
**Background**: Phase 40 에서 1회 검사 결과의 xlsx export 기반(CycleResultSerializer + ExcelExportService) 이 완성됨. Phase 41.1 은 이 기반 위에서 (a) 50회 자동 반복 실행 + 결과 누적, (b) 반복도 단순 통계(mean/stddev/range/Cpk) xlsx, (c) 알고리즘별(TLI/CTH/VTH/Edge 6종+) 통계 분석표를 추가한다. 정식 Gage R&R ANOVA 는 OUT-OF-SCOPE (REQUIREMENTS.md 명시).
**Scope**:
  - **OUT-03 반복도 excel**: 50회 시퀀스 자동 실행 + 누적 결과 → mean/stddev/range/Cpk xlsx
  - **OUT-04 알고리즘 통계표**: TLI/CTH/VTH/Edge 6종+ 알고리즘별 검출 성공률/평균/표준편차 집계표
  - Phase 40 ExcelExportService / CycleResultSerializer 재사용 (코드 복제 0)
**Success Criteria (UAT)**:
  - 50회 자동 반복 실행 후 반복도 xlsx 생성 (mean/stddev/range/Cpk 컬럼 포함)
  - 알고리즘별 통계표 xlsx 생성 (TLI/CTH/VTH + Edge 타입별 행)
  - 생성된 xlsx 가 외부 도구(Excel) 에서 정상 열림

### 우선순위 2 — v1.1 Carry-over 정리

- [x] **Phase 42: 픽셀분해능 런타임 단일소스** (CO-38-01) — signed_off 2026-06-15 (UAT 2/2 PASS, code review clean)
  - Success: Shot 단일값 편집 시 재시작 없이 전체 FAI 반영 / PropertyGrid 항목별 노출 정리 / 측정 경로 단일 소스
  - **Plans:** 1 plan
  - Plans:
    - [x] 42-01-PLAN.md — 측정 소비 Rewire(D-01/D-06) + PropertyGrid 항목별 숨김(D-04/D-05) + 회귀 검증
- [x] **Phase 43: 시작지연 분리 (LoginManager + SequenceHandler)** (CO-38-02, CO-38-03) — SIGNED_OFF 2026-06-15 (1 plan, UAT PASS — READY 55% 단축)
  - Success: 앱 기동 LoginManager 백그라운드 프리로드(Step 5 808ms 제거) → [STARTUP] READY avg 578ms (Before ≈1285ms, 55% 단축, 목표 ≥30% PASS). CO-43-01(흰 화면) carry-over.
- [x] **Phase 43.1: 기동 체감속도 개선 — 흰 화면 마스킹 + 콜드스타트 계측** (CO-43-01) — SIGNED_OFF 2026-06-15 (1 plan, UAT PASS — 스플래시 즉시 표시, 레시피 로딩 14787ms 지배 구간 확인)
  - Success: 스플래시 ≤1s 표시(흰 화면 마스킹 ✓) + (d)=6726ms/(e)=21513ms 구간 분해 수치 확보 + 회귀 0. 지배 구간 = 레시피 로딩(~14787ms) → Phase 43.2에서 비동기화.
- [x] **Phase 43.2: 기동 체감속도 단축 — 레시피 로딩 비동기화** (CO-43-01 후속) — SIGNED_OFF 2026-06-15 (3 plans, UAT PASS — 창 표시 21513ms→3129ms 85% 단축 + 레시피 로드 11s 병목(ParamBase.Load 예외 storm 4948회/로드) 제거)
- [ ] **Phase 44: 실HW [STARTUP] 재측정** (CO-38-04, HW 도착 시 / 미도착 시 Simul 베이스라인)
- [x] **Phase 45: A1~A5 측정값 UI 표시** (CO-23-01, Phase 23 ALG-01 잔여) — ✅ RESOLVED 2026-06-16 (Phase 40/40.1/40.2 리뷰어·결과화면·Export 가 측정값 표시를 이미 구현 — MainView.xaml / ReviewerWindow.xaml / MeasurementResultRow.cs. 별도 phase 불필요)

### 우선순위 1 — POC 신규 기능 (신설 2026-06-16)

- [ ] **Phase 51: 시퀀스 일괄 검사 & 일괄 Export** (BATCH-01)
  - Success: Top/Bottom 시퀀스 단위로 전체 SHOT을 한 번에 실행 → 전 SHOT/FAI 측정 결과 누적 → 단일 xlsx 일괄 추출 (SHOT 개별 트리거 불필요) / SHOT 개별 검사 회귀 0
- [ ] **Phase 52: 이미지 수평 보정 (Datum 에지 기반 회전 정렬)** (LEVEL-01) — ⚠ PARTIAL 2026-06-17 (백엔드 완료, UI carry-over CO-52-01)
  - Success: Datum 수평 에지 검출 각도와 수평선의 각도차로 입력 이미지를 회전 정렬(레벨링) 후 측정 / 회귀 0
- [ ] **Phase 53: 픽셀 캘리브레이션 (체커보드)** (CAL-01)
  - Success: 별도 캘리브 창에서 체커보드(라이브 정지/촬상 또는 이미지 로드) → 픽셀 해상도(mm/px) 산출 → 측정 PixelResolution 적용

### Phase 51: 시퀀스 일괄 검사 & 일괄 Export (신설 2026-06-16 — POC 신규 #3)
**Goal**: 현재 SHOT 노드를 개별 트리거하는 검사 방식을, 시퀀스(Top/Bottom) 단위로 전체 SHOT을 한 번에 실행하여 전 측정 결과를 누적하고 엑셀로 일괄 추출할 수 있게 한다. SIMUL 모드 우선, 실 모드는 검사 사이클마다 결과를 채우는(append) 방식.
**Depends on**: Phase 40 (Export I — CycleResultSerializer / ExcelExportService) , Phase 39 (검사 워크플로우 E2E) signed_off
**Requirements**: BATCH-01
**Background**: 현재 검사는 SHOT 별로 트리거되어, 엑셀 추출 시 시퀀스 전체 데이터를 한 번에 얻기 어렵다. POC 2026-06-30 산출물은 Top/Bottom 시퀀스 전체 측정값이 한 번에 나와야 한다.
**Scope** (discuss 에서 확정):
  - 시퀀스 단위 일괄 실행 진입점(UI 버튼/명령) + 전 SHOT 순차 실행 + 전 결과 누적
  - Phase 40 Export(CycleResultSerializer / ExcelExportService) 재사용한 일괄 xlsx
  - SIMUL: 각 SHOT SimulImagePath 순회 실행 / 실 모드: 검사 사이클마다 결과 append
**Out of scope**: 50회 반복도(Phase 41.1), 신규 측정 알고리즘
**Success Criteria (UAT)**:
  - Top/Bottom 일괄 실행 → 전 SHOT/FAI 측정 결과 누적 → 단일 xlsx 로 전 결과 추출
  - SHOT 개별 트리거 검사 회귀 0

### Phase 52: 이미지 수평 보정 (Datum 에지 기반 회전 정렬) (신설 2026-06-16 — POC 신규 #1)
**Goal**: Datum 수평 에지를 검출해 수평선과의 각도차로 입력 이미지를 회전 정렬(레벨링)한 뒤 측정한다.
**Depends on**: DatumFindingService (수평 2-ROI concat 라인 피팅 — 기존)
**Requirements**: LEVEL-01
**Background**: 사용자 제공 HDevelop 참조 — UnionContours 2개 `fit_line_contour_xld` → `get_contour_xld` → `gen_contour_polygon_xld`(LongLine) → `fit_line_contour_xld` → `angle_lx` 로 각도 산출 → 이미지 회전. DatumFindingService 의 수평 2-ROI concat 피팅(TryFindVerticalTwoHorizontal 등)과 동일 파이프라인.
**Scope** (discuss 에서 확정):
  - Datum 수평 에지 각도 산출 + 이미지 회전 보정 적용
  - 적용 시점(검사 전 전처리 / Datum 검출 후) 및 적용 범위(시퀀스/SHOT) 확정
**Success Criteria (UAT)**:
  - 기울어진 입력 이미지가 수평 정렬된 후 측정 / 기존 측정 회귀 0
**Plans:** 4 plans (3 waves) -- planned 2026-06-17
Plans:
- [x] 52-01-PLAN.md -- InspectionSequence LevelingEnabled + leveling angle cache + DatumConfig.IsLevelingReference + FIXTURE INI save/load (D-01/D-04) [Wave 1]
- [x] 52-02-PLAN.md -- DatumFindingService.TryGetLevelingAngle (Math.Atan2 angle) + VisionAlgorithmService.RotateImageByAngle (affine_trans_image) [Wave 1]
- [x] 52-03-PLAN.md -- InspectionSequence.TryComputeLevelingAngle (seq-once cache) + Action_FAIMeasurement EStep.Level grab rotation (D-02/D-03) [Wave 2]
- [~] 52-04-PLAN.md -- integration build PASS + SIMUL UAT (0 PASS / 1 FAIL / 3 BLOCKED / 1 NOT_TESTED) -- UI 부재로 미충족, CO-52-01 [Wave 3]

**Status:** ⚠ PARTIAL (NOT signed_off) 2026-06-17 -- 백엔드 52-01~03 완료·빌드 PASS·코드리뷰 클린(0 critical, WR-01/WR-02 fix 적용). 사용자 SIMUL UAT Test 2(핵심) FAIL: LevelingEnabled/IsLevelingReference 활성화·기준지정 UI 와 결과 회전 시각화 부재로 기능 실행·검증 불가 → carry-over CO-52-01 → Phase 52.1(레벨링 UI) 신설 예정. LEVEL-01 사용자 검증 미충족.

### Phase 53: 픽셀 캘리브레이션 (체커보드) (신설 2026-06-16 — POC 신규 #2)
**Goal**: 체커보드(격자 white/black) 기반 픽셀 캘리브레이션 기능 — 라이브 정지/촬상 또는 이미지 로드로 격자 이미지를 입력받아 픽셀 해상도(mm/px)를 산출하고 측정에 적용한다. 별도 창으로 제공.
**Depends on**: Phase 42 (ShotConfig.PixelResolution 단일소스) signed_off
**Requirements**: CAL-01
**Background**: 현재 픽셀 해상도는 수동 입력. 체커보드 캘리브로 산출/적용이 필요. 라이브 중에는 라이브 정지 후 촬상, 또는 이미지 로드로도 가능해야 하며, 산출값이 픽셀 해상도에 반영되어야 한다.
**Scope** (discuss 에서 확정):
  - 별도 캘리브 창(라이브 정지→촬상 or 이미지 로드) + 격자 검출 + mm/px 산출 + PixelResolution 반영
  - 캘리브 알고리즘(HALCON caltab/find_caltab vs 격자 코너 검출) 확정
**Success Criteria (UAT)**:
  - 체커보드 입력 → 픽셀 해상도 산출 → 측정 PixelResolution 적용 / 이미지 로드 모드 동작

### Phase 43: 시작지연 분리 (LoginManager + SequenceHandler) (CO-38-02, CO-38-03)
**Goal**: 앱 기동 시 동기적으로 수행되는 무거운 초기화(계정 DB 로드, 레시피 동기 로딩)를 지연/분리하여 "측정 가능 시점"까지의 시간을 단축한다. Phase 38 에서 추가한 `[STARTUP]` Stopwatch 계측을 기준선으로, 측정 가능 시점이 ≥30% 단축됨을 입증한다.
**Depends on**: Phase 38 (`[STARTUP]` 계측 9줄 SystemHandler.Initialize) signed_off
**Requirements**: CO-38-02, CO-38-03
**Background**: Phase 38 SIMUL 1회 실측 — `SystemHandler.Initialize` Total **1509ms**, 이 중 Step5 LoginManager delta **808ms**(계정 DB 로드 추정) + Step2 SequenceHandler delta **550ms**(레시피 동기 로딩) ≈ 전체의 90%. 이 두 구간이 기동 지연의 지배적 원인. Phase 38 에서 "저위험 개선 미적용 → 전부 carry-over" 로 남긴 항목.
**Scope**:
  - **CO-38-02 LoginManager lazy-load**: 계정 DB 로드를 앱 기동 동기 경로에서 분리 — 최초 로그인 시도 시점 또는 백그라운드 로드로 지연. 기존 LoginManager 인증 동작/계정 모델 변경 없음.
  - **CO-38-03 SequenceHandler 동기 의존성 제거**: 레시피 동기 로딩이 Initialize 를 블로킹하지 않도록 분리 — 측정 시작 전 준비 완료 보장은 유지하되 기동 경로에서 제거.
  - **계측 기반 입증**: `[STARTUP]` 로그(Step1~8 + Total)로 Before/After 비교, 측정 가능 시점 ≥30% 단축 수치 제시.
**Out of scope**:
  - CO-38-04 실HW [STARTUP] 재측정 (Phase 44 — HW 도착 의존)
  - OAuth/사용자 인증 고도화 (기존 LoginManager 유지, REQUIREMENTS.md 명시)
**Success Criteria (UAT)**:
  - 앱 기동 LoginManager lazy-load 후 측정 가능 시점 ≥30% 단축 (`[STARTUP]` 로그 Before/After 비교)
  - SequenceHandler 동기 의존성 제거 후 Initialize 가속 입증 (Step2 delta 감소 수치)
  - 회귀 0: 첫 로그인/첫 검사 흐름 정상 (lazy-load 로 인한 미준비 상태 버그 없음)

**Plans:** 1 plan
Plans:
- [x] 43-01-PLAN.md — LoginManager 백그라운드 프리로드(Step5 동기 808ms 제거) + [STARTUP] READY 마커 + LoginWindow EnsureLoaded readiness wait + 30% 평균/회귀 UAT — COMPLETE (55% READY 단축, CO-38-02/CO-38-03 종결)

### Phase 43.1: 기동 체감속도 개선 — 흰 화면 마스킹 + 콜드스타트 계측 (CO-43-01) — 신설 2026-06-15
**Goal**: 앱 기동 시 발생하는 **18~20초 흰 화면**(체감 기동 지연)을 제거/마스킹하여 POC 시연 체감 속도를 개선한다. 먼저 흰 화면 구간을 계측해 지배 원인을 수치화하고, 즉시 표시되는 시각적 피드백(스플래시/로딩)으로 흰 화면을 없앤다.
**Depends on**: Phase 43 (SIGNED_OFF — [STARTUP] READY 계측 + LoginManager bg 프리로드) / [STARTUP] Total Initialize 계측 기준선
**Requirements**: CO-43-01
**Background**: Phase 43 UAT 에서 발견. `[STARTUP] Total Initialize` = ~579ms 인데 실제 더블클릭→창 표시까지 18~20초 흰 화면. 근본 원인은 Initialize() **밖** — `MainWindow` 생성자가 `Show()` **이전에** Initialize()(MainWindow.xaml.cs:78) + InitializeComponent()(:81, 전체 MDI XAML inflation) 를 모두 끝냄. 흰 화면 = process 콜드 JIT(Debug) + Halcon/OpenCV/카메라 네이티브 DLL 로딩 + XAML inflation 의 합. 스플래시 부재(SplashScreen/StartupUri 없음).
**Scope**:
  - **계측(measure)**: 흰 화면 구간을 분해하는 Stopwatch 마커 추가 — (a) process→App.Startup, (b) MainWindow ctor 진입, (c) Initialize, (d) InitializeComponent, (e) ctor→Show→첫 paint(ContentRendered/Loaded). 어느 구간이 지배적인지 사용자 실행 로그로 수치화.
  - **마스킹(mask)**: 흰 화면을 시각적 피드백으로 대체 — WPF `SplashScreen`(즉시 PNG, 관리 UI 이전 표시) 또는 경량 로딩 창. 콜드스타트 비용 위치와 무관하게 흰 화면 체감 제거. **저위험 우선.**
  - **(조건부) 비핵심 초기화 비동기화**: 계측 결과가 가리키는 비핵심 무거운 구간을 첫 paint 이후로 지연. 측정 준비 보장(첫 $TEST 수용)은 깨지 않음.
**Out of scope**:
  - Release/NGEN/ReadyToRun 빌드 전환 자체(별도 검토) — 단, 흰 화면이 Debug 콜드 JIT 지배이면 Release 측정값을 참고로 기록.
  - 측정 알고리즘/검사 흐름 변경 없음. SystemHandler.Initialize 동작 의미 불변(순서 재배치만 허용).
**Success Criteria (UAT)**:
  - 앱 기동 시 흰 화면 대신 즉시(≤1s) 시각 피드백(스플래시/로딩) 표시 — 사용자 체감 "멈춤" 제거.
  - 흰 화면 구간 분해 수치 확보(어디서 18~20초 소모되는지 로그로 입증).
  - 회귀 0: 기동 후 첫 로그인/첫 $TEST/MainView 정상, READY 마커 의미 유지.

**Plans:** 1 plan
Plans:
- [x] 43.1-01-PLAN.md — App/MainWindow 흰 화면 구간 [STARTUP-WHITE] (a)~(e) 분해 계측 + WPF 네이티브 SplashScreen 즉시 표시(관리 UI 이전, ContentRendered fade close) + splash.png 자산 + 회귀 UAT (CO-43-01) — COMPLETE

### Phase 43.2: 기동 체감속도 단축 — 레시피 로딩 비동기화 (CO-43-01 후속) — 신설 2026-06-15
**Goal**: Phase 43.1 계측에서 확인된 **레시피 로딩 ~14787ms** (지배 구간)를 `Show()` 이후 비동기로 이동하여 실제 기동 체감 시간을 단축한다.
**Depends on**: Phase 43.1 (SIGNED_OFF — [STARTUP-WHITE] 계측, 지배 구간 = 레시피 로딩 확인)
**Requirements**: CO-43-01 후속
**Plans:** 3 plans (3 waves)
Plans:
- [ ] 43.2-01-PLAN.md — SystemHandler _isRecipeReady 필드 + IsRecipeReady 프로퍼티 + LoadRecipe (f)/(g) 마커 [Wave 1]
- [ ] 43.2-02-PLAN.md — MainWindow Window_Loaded 동기 제거 + ContentRendered BeginInvoke(Background) 이동 + OnLoadRecipe Dispatcher 래핑 [Wave 2]
- [ ] 43.2-03-PLAN.md — ProcessTest IsRecipeReady guard + UAT Before/After 수치 확인 + 회귀 0 [Wave 3]

### 우선순위 3 — HW 도착 시점

- [ ] **Phase 46: CXP 그래버 통합 (RAP 4G 4C12)** (HW-01, HW-02)
  - Success: SDK 확정/설치 / VirtualCamera 인터페이스 호환 / HIK 와 동일 GrabHalconImage 경로 / 4ch grab 실측 PASS
  - HW 미도착 시: Simul 검증으로 마감 (v1.3 이연 가능)

### 우선순위 4 — 시간 여유 시

- [ ] **Phase 47: 헝가리안 표기법 전체 리팩토링** (QUAL-01)
  - Success: 전체 식별자 헝가리안 컨벤션 적용 / msbuild Debug/x64 PASS / 회귀 0 / 기존 코드 스타일과 충돌 없음

### 우선순위 5 — POC 시연 이후 (제어팀 동기화 필요)

- [ ] **Phase 48: 제어 프로토콜 v2.7 — TEST z_index + RESULT 직렬화** (PROTO-01, PROTO-02)
  - Success: `$TEST:site,null,z_index@` 파싱 / `$RESULT:site;P|F|B;count;id=val=OK,...@` 직렬화·역직렬화 / 회귀 0
- [ ] **Phase 49: 제어 프로토콜 v2.7 — 3-state 엔진 + Datum 빈 응답 + CycleState** (PROTO-03, PROTO-04, PROTO-05)
  - Success: P/F/B 종합 판정 / Datum 샷(z_index=0) 빈 응답 / Datum 실패 즉시 F / CycleState·ECycleResult enum + 자동 리셋
- [ ] **Phase 50: 제어 프로토콜 v2.7 — 통신 회귀 시험** (PROTO-06)
  - Success: 제어팀(김민우 선임) 동기화 / 실 핸들러 통신 회귀 PASS / v2.6 → v2.7 마이그레이션 절차 문서화

---

## Progress Table (v1.2)

| 순위 | Phase | 제목 | REQ-IDs | Status | Plans | Date |
|------|-------|------|---------|--------|-------|------|
| 1 | 39 | 검사 워크플로우 E2E | WF-01, WF-02 | SIGNED_OFF | 4 | 2026-05-29 |
| 1 | 39.1 | 검사 워크플로우 긴급 fixes | WF-01 | SIGNED_OFF | 4 | 2026-05-29 |
| 1 | 39.2 | 긴급 추가건2 (DualImage+I10+Tree) | WF-01 | PARTIAL_SIGNED_OFF | 5 | 2026-05-30 |
| 1 | 39.3 | DualImage FAI UX 재설계 (CO-39.2-01-01) | WF-01 | Planned | 4 | 2026-05-30 |
| 1 | 40 | Export I (리뷰어+1회) | OUT-01, OUT-02 | Not started | TBD | — |
| 1 | 41.1 | Export II (반복도+통계) | OUT-03, OUT-04 | Not started | TBD | — |
| 2 | 42 | 픽셀분해능 단일소스 | CO-38-01 | SIGNED_OFF | 1 | 2026-06-15 |
| 2 | 43 | 시작지연 분리 | CO-38-02/03 | SIGNED_OFF | 1 | 2026-06-15 |
| 2 | 44 | 실HW STARTUP 재측정 | CO-38-04 | Not started (HW) | TBD | — |
| 2 | 45 | A1~A5 측정값 UI | CO-23-01 | RESOLVED (Phase 40 시리즈) | — | 2026-06-16 |
| 3 | 46 | CXP 그래버 통합 | HW-01/02 | Not started (HW) | TBD | — |
| 4 | 47 | 헝가리안 리팩토링 | QUAL-01 | Not started | TBD | — |
| 5 | 48 | Protocol v2.7 TEST/RESULT | PROTO-01/02 | Not started (POC 후) | TBD | — |
| 5 | 49 | Protocol v2.7 3-state/Cycle | PROTO-03/04/05 | Not started (POC 후) | TBD | — |
| 5 | 50 | Protocol v2.7 회귀 시험 | PROTO-06 | Not started (POC 후) | TBD | — |
| 1 | 51 | 시퀀스 일괄 검사 & Export | BATCH-01 | Complete (signed off 2026-06-16) | 2026-06-16 | UAT 전항목 PASS |
| 1 | 52 | 이미지 수평 보정 (Datum 에지) | LEVEL-01 | In progress (3/4) | TBD | — |
| 1 | 53 | 픽셀 캘리브레이션 (체커보드) | CAL-01 | Not started | TBD | — |

> v1.0/v1.1 phase 진행표 전문: [milestones/v1.0-ROADMAP.md](milestones/v1.0-ROADMAP.md), [milestones/v1.1-ROADMAP.md](milestones/v1.1-ROADMAP.md)

---

## Backlog

### Phase 40.2: FAI별 측정 캡쳐 이미지 저장 + 엑셀 파일명 2컬럼 (INSERTED)

**Goal:** 검사 시점에 FAI별 원본 이미지와 측정 오버레이가 입혀진 캡쳐 이미지를 각각 PNG로 디스크에 저장하고, 엑셀 export의 하이퍼링크 컬럼을 원본/캡쳐 파일명 텍스트 2컬럼으로 교체한다.
**Requirements**: CONTEXT 잠긴 결정 (phase_req_ids 없음)
**Depends on:** Phase 40
**Plans:** 4 plans

Plans:
- [ ] 40.2-01-PLAN.md — DTO 파일명 필드 + FAIConfig transient 필드 + CaptureImageSaveService 비동기 워커 + SystemHandler 등록
- [ ] 40.2-02-PLAN.md — OverlayCaptureRenderer 헤드리스 버퍼 캡쳐 + Action_FAIMeasurement FAI별 origin/capture enqueue + 파일명 write-back
- [ ] 40.2-03-PLAN.md — CycleResultSerializer 파일명 복사 + ExcelExportService 하이퍼링크→파일명 텍스트 2컬럼 교체
- [ ] 40.2-04-PLAN.md — SIMUL_MODE UAT (폴더/파일명/오버레이/엑셀 육안 검증 + sign-off)

### Phase 40.1: 리뷰어/뷰어 UAT 후속 UI 3건 (overlay On/Off 토글 + 트리 Shot 접기 + Polygon ROI 숨김) (INSERTED)

**Goal:** 리뷰어/뷰어 UAT 후속 UI 3건 — 이미지 뷰어 overlay On/Off 토글(측정 overlay + Datum 라인 독립) + Shot 트리 기본 접기 + Polygon ROI UI 숨김. 라이브 MainView/InspectionListView 한정.
**Requirements**: 없음 (긴급 UI 후속)
**Depends on:** Phase 40
**Plans:** 2/2 plans complete

Plans:
- [x] 40.1-01-PLAN.md — 이미지 뷰어 overlay On/Off 토글 2개(#2) + Polygon ROI UI 숨김(#4)
- [x] 40.1-02-PLAN.md — Shot 트리 기본 접기 / 펼치기(#3)

### Phase 999.1: Datum 2-image 지원 (side 검사) — ✅ ABSORBED to v1.1 Phase 27/34/36/37 (2026-05-28)
- Side datum 2-image (4 DualImage / 8-image) 지원 v1.1 종결.

### Phase 41: CXP 카메라 MIL Lite 10.0 grab 드라이버 통합 (HW-01/HW-02)

**Goal:** CoaXPress 카메라를 Matrox MIL Lite 10.0 으로 software-trigger 단발 grab 하여 신규 MilCamera : VirtualCamera 드라이버로 기존 DeviceHandler.GrabHalconImage(param) 계약(HImage 반환)에 통합한다. 시퀀스/액션 코드 무변경, SIMUL_MODE 폴백 유지, HIK/Basler 와 동일 계약. 실 HW(RAP4G4C12 보드) 미도착 — SIMUL_MODE 전 경로 구현·검증, 실 HW 연결부(MsysAlloc 시스템 디스크립터·ViewWorks 해상도)는 보드 도착 후 확정하도록 isolate.
**Requirements**: HW-01, HW-02
**Depends on:** Phase 40
**Plans:** 4 plans (4 waves) — planned 2026-06-02

Plans:
- [x] 41-01-PLAN.md — MIL .NET DLL csproj 참조 + ECameraType.MIL enum (foundation, HW-01) [Wave 1]
- [x] 41-02-PLAN.md — MilCamera : VirtualCamera 신규 드라이버 (MIL 1회 할당 / MdigGrab→GenImage1 / SIMUL 폴백 / 역순 해제, HW-02) [Wave 2]
- [x] 41-03-PLAN.md — DeviceHandler case MIL + RegisterRequiredDevices PC별 역할(CameraRole) 재구성 + SystemSetting INI (HW-02, D-03) [Wave 3]
- [x] 41-04-PLAN.md — UAT 6 시나리오 + sign-off (SIMUL Test 1~5 + 실 HW Test 6) [Wave 4]

**✅ SIGNED_OFF 2026-06-09** — UAT 6/6 PASS. 실 HW(Matrox RapixoCXP + VIEWORKS VP-152MX2-M16I0) grab+라이브 동작확인(Test 6). HW-02 런타임 VERIFIED, 실 HW grab carry-over 종결. 커밋: 2dddf13(드라이버) + a397039(CO-41-01) + CO-41-02. Carry-over: CO-41-03(역할별 다중 카메라 부분 등록 미검증).

### Phase 54: Datum 패턴매칭 위치보정 (ALIGN-01) — 자재 X,Y+tilt 변위 정렬 (신설 2026-06-18 — POC 신규, Phase 52 레벨링 흡수·대체)

**Goal:** 자재가 X,Y(+tilt)로 틀어져 들어와도 작은 측정 ROI 가 대상 에지를 벗어나지 않도록, Datum 에 패턴매칭을 두어 자재 위치를 찾고 측정/Datum ROI **좌표**를 보정한다. 측정은 **원본 픽셀**에서 수행(이미지 warp 금지). Phase 52(레벨링)를 흡수 — 이미지 회전(RotateImageByAngle) 폐기, line-fit 각도산출(TryGetLevelingAngle)만 θ 소스로 재사용.
**Requirements**: ALIGN-01 (신규)
**Depends on:** Phase 39 (검사 워크플로우 E2E — `_datumTransforms` / `IsDatumFailed` / `MarkDatumFailed` gate), Phase 52 (레벨링 인프라 — 흡수)
**Background/근거:** `.planning/ALIGN-01-pattern-align-analysis.md` (§8 하이브리드, §9 Datum단위 매칭). 레벨링은 회전만 보정 → 자재 X,Y 변위 시 작은 측정 ROI 가 에지 이탈하여 오측.

**확정 결정 (discuss lock 대상):**
- **보정 = 하이브리드**: 패턴매칭 = **x,y 위치 전용** / **line-fit = 정밀 θ** (매칭 angle 은 거칠어 ~0.3~0.5° → 측정 부적합). tilt(θ)를 측정에 실반영(레벨링 대체).
- **적용 = ROI 좌표변환**(`affine_trans_pixel`), 이미지 warp 아님 → 서브픽셀 무손실 + 저비용. 측정은 원본 이미지 픽셀.
- **매칭 주기 = Datum 당 1회** (Top/Bottom=Datum1개=사실상 시퀀스당1회, Side=Datum4개=4회). **per-Datum 국소 강체** 가정(글로벌 아님).
- **적용 채널 = 기존 `_datumTransforms[DatumName]`** 에 align rigid transform 을 `hom_mat2d_compose` 로 합성 저장 → 측정은 `meas.DatumRef` 로 자동 라우팅(Measure 본문·라우팅 무수정).
- **흐름(Datum당):** ① 매칭(원본 grab 이미지)→x,y ② x,y 로 그 Datum line-fit ROI 이동→이동 엣지 line-fit→정밀 θ ③ (x,y+θ) rigid→`_datumTransforms[DatumName]` 합성 ④ `DatumRef` 측정 자동 적용.
- **매칭 입력 = 보정 전 원본 grab 이미지** (이중보정 가드). 레벨링 이미지회전 폐기로 warp 0회.
- **실패 정책 = lenient(시퀀스 진행, abort 없음) + 매칭 실패 Datum 의 측정은 NG 강제.** 기존 `MarkDatumFailed(DatumName)` 재사용 — `LastSkipReason="ALIGN_FAIL"`, `LastJudgement=false`. 가짜 숫자 안 넣고 값 클리어+NG+사유 → 양품 오판 0.
- **1차 범위 = Top/Bottom + Side 단일 이미지 Datum.** Side DualImage(2-image)는 후속 phase.
- **off 회귀 0 = `DatumConfig.IsPatternAlignEnabled` 기본 false** + INI 키 미존재 폴백 false(EnsurePerRoiDefaults). enabled=false → align=identity → `_datumTransforms` 무변경.
- **신규 영속(per-Datum, DatumConfig):** `IsPatternAlignEnabled`(bool)/`PatternModelPath`(모델 파일 경로)/`PatternRoi_*`(검색영역)/`RefMatchRow/Col/AngleDeg`(기준 pose)/`PatternMinScore`/`PatternAngleExtentDeg`. 모델은 별도 파일(ParamBase 는 double/int/string/bool 만 직렬화) — **레시피 폴더 내 저장**(백업 동반).
- **신규 서비스 `PatternMatchService`** (create/read/write/find_*_model + `vector_angle_to_rigid`). 패턴 티칭 UI(Datum 노드 패턴 ROI 그리기 + 모델 생성/저장 + ref pose 기록).

**Open (discuss 에서 결정):**
- **매칭 엔진 = Shape vs NCC vs 선택형** ⚠ — Shape(edge-gradient)는 **회전/조명/클러터에 강하나 defocus(블러)에 취약**(에지 약화→score 급락). NCC(intensity pattern)는 **defocus 에 강하나 회전 취약**(angle range 필요·느림). 자재 포커싱 불량 우려(사용자 제기 2026-06-18) → defocus-robust 가 중요하면 NCC, tilt 가 크면 Shape. **권장 = per-Datum 엔진 선택자**(`AlgorithmType` 드롭다운 패턴 미러, Shape 기본 + NCC 옵션). 단 매칭은 coarse x,y 만 담당(정밀 θ는 line-fit)이라 NCC 의 회전 약점은 완화됨. 단, defocus 는 line-fit θ·측정 에지도 같이 훼손 → 엔진만으로 해결 불가(포커스 자체가 전제).

**예상 구조:** 3~5 plan / 2~3 wave (레벨링 인프라 미러).
**Success Criteria (UAT, SIMUL 우선 — 합성 변형 이미지 페어):**
- off 회귀 0 (IsPatternAlignEnabled=false → 기존 측정 byte-identical)
- 자재 X,Y 이동 케이스: 보정 추종(측정 정상) vs off(에지 이탈) 대조
- tilt 케이스: x,y + line-fit θ 합성 보정 후 측정 정상
- 매칭 실패 폴백: 시퀀스 abort 0 + 해당 Datum 측정 NG(ALIGN_FAIL)
- 영속: 모델 파일 + ref pose 재시작 후 동작
- Side 4 Datum 각각 독립 보정(per-Datum)

**리스크:** (높음) 모델 파일 영속 — 레시피 백업/복사 시 동반 누락. (중) 부호/좌표계 캘리브(매칭 angle 규약 ↔ Atan2). (중) SIMUL 변형 이미지 페어 확보(Phase 41.1 이미지 부족 전례). (중) **defocus 취약성** — 엔진 선택 + 기준 이미지 포커스 정합으로 완화, 근본은 포커스 품질.

**Plans:** 5 plans (3 waves)

Plans:
- [ ] 54-01-PLAN.md — DatumConfig ALIGN 필드 + PatternEngine 드롭다운 + EnsurePerRoiDefaults 폴백 (D-01/D-09/D-11) [Wave 1]
- [ ] 54-02-PLAN.md — DeviceHandler .shm/.ncm 상수 + RecipeFileHelper engine-aware 모델 경로 재계산 (D-07/D-07a/D-07b) [Wave 1]
- [ ] 54-03-PLAN.md — PatternMatchService 신규 (shape+ncc create/write/read/find + reduce_domain + 다운샘플 coarse + rigid 산출) (D-01/D-06/D-06a) [Wave 1]
- [ ] 54-04-PLAN.md — InspectionSequence align 합성 + Action_FAIMeasurement DatumPhase 매칭 통합 + RotateImageByAngle 폐기 + ALIGN_FAIL lenient (D-02~D-05/D-10/D-11) [Wave 2]
- [ ] 54-05-PLAN.md — 패턴 티칭 UI(ROI 그리기 + 모델 생성/저장 + ref pose 기록) + SIMUL UAT (D-08/D-09) [Wave 3]

### Phase 57: 패턴 ROI UX & Datum 정렬 보강 (신설 2026-06-19 — Phase 54~56 ALIGN 후속 UAT 피드백)

**Goal:** 패턴매칭 정렬(ALIGN)의 티칭 UX·시각화·견고성을 보강한다. 패턴 ROI 입력을 명확/안전하게 하고, datum 시각화 색상 중복을 정리하며, 매칭 실패 시 검사가 멈추지 않게 하고, 미사용 leveling 잔재를 제거한다.
**Requirements**: ALIGN 후속 (Phase 54 ALIGN-01 / 55 ALIGN-02 / 56 시각화 carry-over)
**Depends on:** Phase 56 (보정 ROI/Datum 시각화), Phase 54/55 (ALIGN-01/02 패턴매칭)
**Background:** Phase 56 sign-off 후 실측 UAT 피드백 6항목 (2026-06-19). 조사 = 본 세션 Explore 3건 (leveling 소비경로 / Side datum ROI 구조 / 패턴버튼·datum 색상).

**스코프 6항목 (사용자 결정 반영):**
1. **Pattern ROI1/ROI2 버튼 나란히 배치 + 2개 필수 안전장치** — 현재 `패턴 1`(필수)/`패턴 2`(선택) 구조. 1개만 그리고 모델 생성 시 "패턴 2개 필요" 경고. (MainView.xaml btn_drawPatternRoi/2:181-244, InvokeCreatePatternModel:2826)
2. **Pattern ROI 표시/숨김 토글 옵션** — 현재 패턴 ROI 가시성 토글 부재(teach 피드백뿐). datum/측정 토글(SetDatumOverlayVisible) 패턴 미러로 추가.
3. **Datum 색상 통일 = slate blue만** (사용자 결정) — 확대 시 slate blue(검출 origin)+magenta(Phase 56 기준선)+legacy yellow(Line1 검출선:908/Circle 중심십자:952) 중복. **magenta 기준선 + legacy yellow 제거**, slate blue origin 십자만 유지. (HalconDisplayService.cs)
4. **Side datum 4-ROI (세로축 별도 매칭)** — ⚠ **세부 설계 discuss서 논의**(사용자 결정). 현재 교점은 수평∩수직(4점)이나 PatternRoi2 가 가로축 이미지에서만 매칭(InspectionSequence.cs:490-514). 세로 이미지 별도 입력+분리 매칭 필요 여부 discuss.
5. **매칭 에러 시 측정 진행(lenient)** — 매칭 실패해도 abort 없이 측정 계속(NG 처리). Phase 54 ALIGN_FAIL 정책 검증/보강.
6. **leveling reference 제거** (사용자 결정 = 안 씀) — Phase 52 IsLevelingReference(DatumConfig:43)/LevelingEnabled(InspectionSequence:49) + TryComputeLevelingAngle(603)/TryGetLevelingAngle(DatumFindingService:609) + EStep.Level(Action_FAIMeasurement:80-118) + INI 직렬화(InspectionRecipeManager:97/139) 제거. ALIGN 이 위치/tilt 보정하므로 중복. 기존 레시피 off 폴백 회귀 0 확인.

**Plans:** 5 plans, 2 waves (전 plan 실행 완료 2026-06-19, UAT 대기)

Plans:
- [x] 57-01-PLAN.md — #6 leveling 완전 제거 (코드+상태+INI, MoveZ→DatumPhase 재배선, off 회귀 0) [Wave 1] ✅ 2026-06-19 (40ffe36, d10c884)
- [x] 57-02-PLAN.md — #4 Side DualImage 단일 공유 transform align + #5 lenient 검증 (TryFindLine transform 이식, 게이트 해제) [Wave 2] ✅ 2026-06-19 (c079b4f, 4eeb71b)
- [x] 57-03-PLAN.md — #3 datum 시각화 slate blue 통일 (magenta→slate blue recolor, 기준선 유지) [Wave 1] ✅ 2026-06-19 (e4464c3)
- [x] 57-04-PLAN.md — #1 패턴 ROI 버튼 나란히 배치 + 단일 패턴 경고+override [Wave 1] ✅ 2026-06-19 (a179c22, 25f1b71)
- [x] 57-05-PLAN.md — #2 패턴 ROI 표시/숨김 토글 (SetDatumOverlayVisible 미러, cyan 렌더) [Wave 2] ✅ 2026-06-19 (cf97c5b, 9a290a7)

Wave 1 (병렬): 57-01, 57-03, 57-04 (파일 비충돌)
Wave 2 (병렬): 57-02 (deps 57-01), 57-05 (deps 57-03/57-04)

---

### Phase 57.1: 패턴 ROI 검증 & 안전장치 (INSERTED 2026-06-22 — Phase 57 UAT 피드백)

**Goal:** Phase 57 패턴 ROI/정렬 UAT 중 발견된 4개 항목을 검증·보강한다. 패턴매칭 보정이 Top/Bottom에도 실제 적용되는지 육안 확인 가능하게 하고, ROI 회전 시 length1/length2 장축·baseline 회전각이 올바른지 진단하며, 패턴 ROI 시각화 렌더 조건을 안정화하고, 패턴 ROI 버튼에 비-Datum 노드 비활성화 + 알림 안전장치를 추가한다.
**Requirements**: Phase 57 후속 (ALIGN-01/02 UAT 피드백)
**Depends on:** Phase 57 (패턴 ROI UX & Datum 정렬 보강)
**Background:** Phase 57 sign-off 전 UAT 피드백 4항목 (2026-06-22). 조사 = 본 세션 Explore 3건 + 직접 코드 확인 (PatternMatchService / FAIEdgeMeasurementService / MainView·MainResultViewerControl 렌더 게이트).

**스코프 4항목 (사용자 결정 = Phase 57.1 묶음):**
1. **Top/Bottom 패턴매칭 보정 적용 + 육안 확인** — Top/Side/Bottom 모두 동일 InspectionSequence→TryComposeAlign 경로(IsPatternAlignEnabled per-Datum, 기본 off). 보정 ROI cyan 박스가 실제로 보여 보정 여부를 육안 확인 가능해야 함(#3 시각화와 연계).
2. **gen_rectangle2 length1/length2 장축·회전각 진단 (swap 아님)** — FAIEdgeMeasurementService.cs:51 analytic 회전(phi += rotAngle, length 유지)은 강체회전상 정상. 진단 = (a) baseline 회전각 thetaRad가 90° 어긋난 값 반환하는지, (b) HALCON 규약(length1=phi방향, length2=수직) 매핑이 티칭 드로잉 직관과 일치하는지 확증·문서화/시각화. ※ length swap 코드 수정은 회귀 위험 — 금지.
3. **패턴 ROI 시각화 렌더 조건 안정화** — cyan 패턴 ROI는 `_resultDatumOverlays` 채워질 때만 렌더(MainResultViewerControl). Measurement/Shot/FAI 노드 렌더 경로에서만 채워지고 Datum 노드 단독 선택/티칭 모드에선 비어 안 보임 → Datum 노드 선택 시에도 채우기.
4. **패턴 ROI 버튼 비-Datum 비활성화 + 알림 안전장치** — 패턴1/패턴2/패턴모델생성 버튼이 Datum 노드 외 이동 시 명시적 비활성화 안 됨. (a) 비-Datum 노드 선택 시 IsEnabled=false, (b) 클릭 핸들러에 "Datum 티칭 존을 먼저 선택하세요" 알림 메시지박스 가드.

**Success Criteria (UAT):**
  - Top/Bottom Datum에 IsPatternAlignEnabled on 후 보정 ROI(cyan)가 결과 화면에 표시되어 보정 위치 육안 확인 가능
  - length1/length2 장축 매핑 + baseline 회전각이 90° 어긋남 없이 ROI가 부품 정렬과 일치 (진단 보고/시각화로 확증)
  - Datum/Measurement/Shot/FAI 어느 노드를 선택해도 패턴 ROI 표시 토글이 일관되게 동작
  - 비-Datum 노드 선택 시 패턴 ROI 버튼 비활성화, 부주의 클릭 시 알림 메시지박스 표시

**Plans:** 3 plans (2 waves)

Plans:
- [x] 57.1-01-PLAN.md — D-03/D-01: 패턴 ROI(cyan) 시각화 렌더 안정화 (Datum 노드 선택 시 _resultDatumOverlays 채움) ✅ 2026-06-22 (49a48ec)
- [x] 57.1-02-PLAN.md — D-02: 회전각 확증 Trace 로그 + length 장축 매핑 주석 (측정값 무변경, swap 금지) ✅ 2026-06-22 (00ec5ad)
- [x] 57.1-03-PLAN.md — D-04: 패턴 버튼 비-Datum 비활성화 확증 + 클릭 가드 메시지 통일 ✅ 2026-06-22 (72896d5)
- [x] 57.1-04-PLAN.md — Test Find 버튼을 런타임 패턴 보정 경로(TryComposeAlign)에 연결 (티칭 단계 Top/Bottom/Side 보정 육안 확인) ✅ 2026-06-22 (cd5ed77)
- [x] 57.1-05-PLAN.md — 패턴 1/패턴 2 ROI 그리기 버튼 진입 전 OK/Cancel 진행 확인 (무심코 클릭 방지) ✅ 2026-06-22 (b600a16)
- [x] 57.1-06-PLAN.md — Test Find 보정 ROI 박스 위치이동 표시 (보정 datum 성공 시 ShowResultDatumOverlays → orange ROI 박스 부품 따라 이동, UAT Test 4 fix) ✅ 2026-06-22 (63cc0d8)
- [x] 57.1-07-PLAN.md — 측정 ROI 표시 90° 정상화(View length1/length2 스왑 수정, 측정값 무변경) + cyan 패턴 ROI CurrentTransform 위치보정 표시 ✅ 2026-06-22 (088f6ab)
- [x] 57.1-08-PLAN.md — TryFitLine 에지 trim 을 위치축 정렬 + 양끝 % 절사로 교체 (EdgeTrimCount=양끝 각 %) ✅ 2026-06-22
- [x] 57.1-09-PLAN.md — 에지 trim 정렬+% 절사를 VisionAlgorithmService.SortAndTrimPercent 공유 헬퍼로 단일소스화 (Datum 검출 + 전 측정 trim 통일) ✅ 2026-06-22 (f14c8b9)
- [x] 57.1-10-PLAN.md — EdgeTrimCount UI 를 %(비율) 표시로 통일 (측정 13 + Datum 6 trim 필드에 [DisplayName("...Edge Trim (%)")] 추가, INI 키 보존) ✅ 2026-06-22 (a1a167d)

- [x] **Phase 57.1: 패턴 ROI 검증 & 안전장치** — Top/Bottom 보정 육안확인 + length 장축 진단 + 시각화 안정화 + 버튼 안전장치 (Phase 57 UAT 후속) — SIGNED_OFF 2026-06-22 (11 plans, UAT 9/9 PASS)
  - 04 Test Find 보정연결(Top/Bottom/Side) · 05 패턴 그리기버튼 OK/Cancel 확인창 · 06 Test Find 보정 ROI 박스 이동표시 · 07 측정 ROI 90° View수정(length1=hwidth=Column)+패턴 ROI 위치보정 · 08 TryFitLine trim 정렬+%절사 · 09 trim 통일(Datum 검출+전 측정, 공유 SortAndTrimPercent) · 10 UI [DisplayName] "Edge Trim (%)"(측정13+Datum6, INI 호환) · 11 재티칭 RefMatch 동기화(Teach=Find)
  - 핵심: HALCON gen_rectangle2 Length1=hwidth(Column)/Length2=hheight(Row) · EdgeTrimCount 의미 개수→%(양끝각) · 측정은 SmallestRectangle2 사용→90°는 View만

---

*Last updated: 2026-06-15 — Phase 43(시작지연 분리, CO-38-02/CO-38-03) signed_off (1 plan, UAT PASS — [STARTUP] READY 55% 단축, avg 578ms vs Before ≈1285ms). LoginManager 백그라운드 프리로드 + EnsureLoaded race 차단. CO-43-01(흰 화면) carry-over.*

