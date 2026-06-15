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
- [ ] **Phase 41.1: 결과 분석 & Export II — 50회 반복도 + 알고리즘 통계** (OUT-03, OUT-04)
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
- [ ] **Phase 43: 시작지연 분리 (LoginManager + SequenceHandler)** (CO-38-02, CO-38-03)
  - Success: 앱 기동 LoginManager lazy-load 후 측정 가능 시점 ≥ 30% 단축 / SequenceHandler 동기 의존성 제거 후 Initialize 가속 입증
- [ ] **Phase 44: 실HW [STARTUP] 재측정** (CO-38-04, HW 도착 시 / 미도착 시 Simul 베이스라인)
- [ ] **Phase 45: A1~A5 측정값 UI 표시** (CO-23-01, Phase 23 ALG-01 잔여)

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
| 2 | 42 | 픽셀분해능 단일소스 | CO-38-01 | Not started | TBD | — |
| 2 | 43 | 시작지연 분리 | CO-38-02/03 | Not started | TBD | — |
| 2 | 44 | 실HW STARTUP 재측정 | CO-38-04 | Not started (HW) | TBD | — |
| 2 | 45 | A1~A5 측정값 UI | CO-23-01 | Not started | TBD | — |
| 3 | 46 | CXP 그래버 통합 | HW-01/02 | Not started (HW) | TBD | — |
| 4 | 47 | 헝가리안 리팩토링 | QUAL-01 | Not started | TBD | — |
| 5 | 48 | Protocol v2.7 TEST/RESULT | PROTO-01/02 | Not started (POC 후) | TBD | — |
| 5 | 49 | Protocol v2.7 3-state/Cycle | PROTO-03/04/05 | Not started (POC 후) | TBD | — |
| 5 | 50 | Protocol v2.7 회귀 시험 | PROTO-06 | Not started (POC 후) | TBD | — |

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

---

*Last updated: 2026-06-15 — Phase 42(픽셀분해능 런타임 단일소스, CO-38-01) signed_off (1 plan, UAT 2/2 PASS, code review clean). 측정 소비를 ShotConfig.PixelResolution 단일소스로 Rewire + 항목별 PixelResolutionX/Y PropertyGrid 숨김(INI 호환 보존). 회귀 0.*

