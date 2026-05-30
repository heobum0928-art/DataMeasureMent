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
**Scope (seed — discuss-phase 에서 lock):**
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

- [ ] **Phase 39.3: DualImage FAI UX 재설계** — Side Datum DualImage 패턴 차용 + RectROI 활성화 (WF-01, CO-39.2-01-01)
  - Success: Bottom E5 DualImage FAI UAT PASS + 측정 알고리즘 변경 0 + Datum/기존 FAI 회귀 0
  - **Plans:** TBD (discuss-phase 에서 lock)

- [ ] **Phase 40: 결과 분석 & Export I — 리뷰어 + 1회 검사 엑셀** (OUT-01, OUT-02)
  - Success: 날짜/원본 폴더 로드 시 결과 이미지 재현 / 1회 검사 결과 xlsx 생성 (메타+측정값+판정+이미지 링크)
- [ ] **Phase 41: 결과 분석 & Export II — 50회 반복도 + 알고리즘 통계** (OUT-03, OUT-04)
  - Success: 50회 반복 시퀀스 자동 실행 + mean/stddev/range/Cpk xlsx / 알고리즘별(TLI/CTH/VTH/Edge 6종+) 통계 표 생성

### 우선순위 2 — v1.1 Carry-over 정리

- [ ] **Phase 42: 픽셀분해능 런타임 단일소스** (CO-38-01)
  - Success: Shot 단일값 편집 시 재시작 없이 전체 FAI 반영 / PropertyGrid 항목별 노출 정리 / 측정 경로 단일 소스
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
| 1 | 39.3 | DualImage FAI UX 재설계 (CO-39.2-01-01) | WF-01 | Seed | TBD | 2026-05-30 |
| 1 | 40 | Export I (리뷰어+1회) | OUT-01, OUT-02 | Not started | TBD | — |
| 1 | 41 | Export II (반복도+통계) | OUT-03, OUT-04 | Not started | TBD | — |
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

### Phase 999.1: Datum 2-image 지원 (side 검사) — ✅ ABSORBED to v1.1 Phase 27/34/36/37 (2026-05-28)
- Side datum 2-image (4 DualImage / 8-image) 지원 v1.1 종결.

---

*Last updated: 2026-05-30 — Phase 39.2 PARTIAL_SIGNED_OFF (3 PASS / 1 carry-over / 1 skipped, 2 hotfix). Phase 39.3 seed 신설 (CO-39.2-01-01 → DualImage FAI UX 재설계).*
