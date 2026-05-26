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
- [ ] **Phase 24: 검사 워크플로우 end-to-end** — Datum→FAI→결과 처리 완주 + OK/NG/실패 분기 (WF-01, WF-02)
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

### Phase 24: 검사 워크플로우 end-to-end
**Goal**: Datum 티칭 후 FAI 측정 후 결과 처리 전 과정이 SIMUL_MODE 와 카메라 쪽에서 오류 없이 완주하고,
OK/NG/검사실패 각 결과에 따라 TCP 응답 + 이미지 저장 + UI 표시가 올바르게 분기된다
**Depends on**: Phase 23
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
| 24. 검사 워크플로우 end-to-end | 0/TBD | ⏳ Planned | - |
| 25. 결과 분석 & Export | 0/TBD | ⏳ Planned | - |
| 27. Side Inspection 확장 | 0/TBD | ⏳ Planned | - |
| ~~26. 헝가리안 전체 리팩토링~~ | — | ⏭ Deferred → v1.2 | (2026-05-26 이연) |
| 28. FAI CircleDiameter + Datum Circle | 4/4 | ✅ Complete | 2026-05-08 |
| 31. Datum 기준 측정 알고리즘 확장 | 5/5 | ✅ Complete (signed off, CO-31-01 carry-over) | 2026-05-26 |
| 32. 측정 알고리즘 SOP 재정합 | 8/8 | ✅ Complete (UAT PASS) | 2026-05-23 |
| **v1.2** | | | |
| 29. CXP SDK 확정 (구 Phase 22) | 0/TBD | ⏳ Deferred | - |
| 30. CXP 드라이버 통합 (구 Phase 23) | 0/TBD | ⏳ Deferred | - |

---

*v1.1 roadmap updated: 2026-05-26 — Phase 22 retro 동기화. 22-UAT.md 가 2026-05-11 에 이미 signed_off (4/4 PASS) 였으나 ROADMAP 표 미갱신 상태였음 — 표 및 체크박스 동기화 완료. quick 260526-kay (EdgeSelection 차단 해제 3군 일괄) UAT PASS (사용자 3/3 2026-05-26). v1.1 잔여 = 24 → 25 → 27.*
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
