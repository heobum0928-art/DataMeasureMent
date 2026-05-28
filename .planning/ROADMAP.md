# Roadmap: DataMeasurement

## Milestones

- **v1.0 Halcon Migration MVP** — ✅ Shipped 2026-05-04. Archive: [milestones/v1.0-ROADMAP.md](milestones/v1.0-ROADMAP.md) (17 phases / 55 plans)
- **v1.1 Quality + Workflow + Algorithm** — ✅ Shipped 2026-05-28. Archive: [milestones/v1.1-ROADMAP.md](milestones/v1.1-ROADMAP.md) · [REQUIREMENTS](milestones/v1.1-REQUIREMENTS.md) · [AUDIT](v1.1-MILESTONE-AUDIT.md)
  - 17 phases (18~38, inserts 23.1/34.1). Quality(QUAL-02/03/04)+Buffer+Image-dual+Algorithm/Datum 대거 확장 완료.
  - v1.2 이연: WF-01/02, OUT-01~04, HW-01/02, QUAL-01. 부분: ALG-01(CO-23-01).
- **v1.2 Hardware Integration + Workflow/Output** — ◷ Active (다음 마일스톤 — `/gsd-new-milestone` 로 정식 개시 권장)

---

## v1.2 Phases (이연·신규 후보 — 정식 요구사항은 /gsd-new-milestone 후 확정)

> v1.1 에서 이연된 기능 + Phase 38 carry-over. 새 마일스톤 시작 시 요구사항/순서 재확정.

- [ ] **Phase 24: 검사 워크플로우 end-to-end** — Datum→FAI→결과 OK/NG/실패 분기 (WF-01, WF-02)
- [ ] **Phase 25: 결과 분석 & Export** — 이미지 리뷰어 + xlsx export + 반복도/알고리즘 통계 (OUT-01~04)
- [ ] **Phase 27: Side Inspection 확장** — LineToLineAngle + Side Fixture INI + PC2 분리 (D1, H5)
- [ ] **Phase 26: 헝가리안 전체 리팩토링** — 전체 식별자 헝가리안 (QUAL-01, POC 납기 후)
- [ ] **Phase 29: CXP SDK 확정** — RAP 4G 4C12 SDK 확정/설치 (HW-01, 장비 도착 후)
- [ ] **Phase 30: CXP 드라이버 통합** — VirtualCamera 추상화 유지 CXP 드라이버 (HW-02, 장비 도착 후)

### v1.1 Carry-over (v1.2 로 이월)

- [ ] **CO-38-01**: 픽셀분해능 런타임/UI 단일소스 — Shot 단일값 편집 시 재시작 없이 전체 FAI 일괄 적용 + 항목별 PixelResolutionX/Y PropertyGrid 숨김 + 측정경로 재배선
- [ ] **CO-38-02**: 시작지연 LoginManager 808ms — 계정 DB lazy-load 검토
- [ ] **CO-38-03**: 시작지연 SequenceHandler 550ms — 레시피 로딩 동기 의존성 분석
- [ ] **CO-38-04**: 실 하드웨어 [STARTUP] 재측정 (SIMUL 1회 측정 한계)
- [ ] **CO-23-01**: Top A1~A5 측정값 UI 표시 결함 (ALG-01 부분 잔여)

---

## Progress Table (v1.2 — 시작 전)

| Phase | Status | Plans | Date |
|-------|--------|-------|------|
| 24 검사 워크플로우 E2E | Not started | TBD | — |
| 25 결과 분석 & Export | Not started | TBD | — |
| 27 Side Inspection 확장 | Not started | TBD | — |
| 26 헝가리안 리팩토링 | Not started | TBD | — |
| 29 CXP SDK | Deferred (장비) | TBD | — |
| 30 CXP 드라이버 | Deferred (장비) | TBD | — |

> v1.1 phase 상세/진행표 전문은 [milestones/v1.1-ROADMAP.md](milestones/v1.1-ROADMAP.md) 참조.

---

## Backlog

### Phase 999.1: Datum 2-image 지원 (side 검사) — ✅ ABSORBED to Phase 27 (2026-05-26)
- Side datum 2-image 지원은 Phase 27 Side Inspection 확장으로 흡수됨. parking lot 유지(미실행).
