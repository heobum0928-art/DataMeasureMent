# Roadmap: DataMeasurement

## Milestones

- ✅ **v1.0 Halcon Migration MVP** — Phases 1-17 (shipped 2026-05-04, 22 deferred items)
- 📋 **v1.1** — Planning (코드 품질 + 검사 워크플로우 실측 + CXP 그래버 + 결과 분석)

## Phases

<details>
<summary>✅ v1.0 Halcon Migration MVP (Phases 1-17) — SHIPPED 2026-05-04</summary>

Full archive: [milestones/v1.0-ROADMAP.md](milestones/v1.0-ROADMAP.md)
Requirements: [milestones/v1.0-REQUIREMENTS.md](milestones/v1.0-REQUIREMENTS.md)
Audit: [milestones/v1.0-MILESTONE-AUDIT.md](milestones/v1.0-MILESTONE-AUDIT.md)
Phase artifacts: [milestones/v1.0-phases/](milestones/v1.0-phases/)

- [x] Phase 1: UI 재설계 (FAI-centric TreeView + 단일 캔버스) — 2/2 — 2026-04-07
- [x] Phase 2: 티칭 & 캘리브레이션 (ROI 시각화 + 픽셀-mm) — 2/2 — 2026-04-08
- [x] Phase 3: 에지 측정 알고리즘 (Halcon MeasurePos) — 2/2 — 2026-04-09
- [x] Phase 4: Datum 기준좌표계 (TwoLineIntersect + hom_mat2d) — 3/3 — 2026-04-10
- [x] Phase 5: 검사 시퀀스 & TCP — 2/2 — 2026-04-09
- [x] Phase 6: Rapid City 확장 (Fixture + 6 Measurement + 조명) — 4/4 — 2026-04-22
- [x] Phase 7: Measurement 오버레이 회귀 수정 — 2/2 — 2026-04-23
- [x] Phase 8: 요구사항 & 트레이서빌리티 동기화 — 1/1 — 2026-04-23
- [x] Phase 9: VERIFICATION 문서 보강 (G3/G4/G5/G7) — 5/5 — 2026-04-23
- [x] Phase 10: Datum 정확성 결함 수정 (WR-01/03/05) — 2/2 — 2026-04-23
- [x] Phase 11: Datum 티칭 UI + ROI 보강 (Circle 지원) — 4/4 — 2026-04-25
- [x] Phase 12: Datum 신규 알고리즘 2종 (CircleTwoHorizontal + VerticalTwoHorizontal) — 3/3 — 2026-04-24
- [x] Phase 13: Datum 알고리즘 확장성 (per-ROI 파라미터 + 시각화) — 5/5 — 2026-04-26
- [x] Phase 14: Datum carry-over (Circle polar sampling + Vertical 그룹) — 5/5 — 2026-04-28
- [x] Phase 15: HALCON MeasurePos 정합성 (measurePhi + EdgeSelection) — 4/4 — 2026-04-29 (partial)
- [x] Phase 16: Circle strip 재설계 + AlgorithmType binding fix — 3/3 — 2026-04-30 (partial)
- [x] Phase 17: Datum 티칭/검증 UX 재설계 + DetectedOrigin + hover — 4/4 — 2026-05-04 (partial 12 PASS / 2 FAIL / 1 SKIP / 1 INVALID)

</details>

### 📋 v1.1 (Planning)

12 작업 묶음 + Phase 17 carry-over 7항목. PROJECT.md `### Active` 참조. 시작: `/gsd-new-milestone` 후 questioning → research → requirements → roadmap.

**Themes:**
- A. 코드 품질 (헝가리안 전체 + if/else + 주석 정리 + PropertyGrid 일반화)
- B. 검사 워크플로우 실측 (Datum→FAI end-to-end + OK/NG/실패 분기)
- C. 이미지 버퍼 인프라 (메모리 상주, 속도 우선)
- D. 신규 하드웨어 (CXP 그래버 RAP 4G 4C12)
- E. 결과 시각화 (이미지 리뷰어)
- F. 데이터 출력 (엑셀 1회/50회 + 알고리즘별 통계표)

## Progress

| Milestone | Phases | Plans Complete | Status | Shipped |
|-----------|--------|----------------|--------|---------|
| v1.0 Halcon Migration MVP | 17 | 55/55 | Complete (22 deferred) | 2026-05-04 |
| v1.1 | TBD | 0 | Planning | — |
