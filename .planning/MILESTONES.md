# Milestones

## v1.1 Quality + Workflow + Infrastructure (In progress)

**Status:** in progress (시작 2026-05-04). Phase 18 signed_off, Phase 19 signed_off (2026-05-08).

**Phase Map (11 phases, continue numbering from v1.0 last=17):**

- Phase 18: Carry-over 정리 (CO-01, CO-03, CO-04, CO-05, CO-06)
- Phase 19: PropertyGrid 동적 노출 일반화 (QUAL-03, CO-02)
- Phase 20: 코드 스타일 정리 (QUAL-02, QUAL-04)
- Phase 21: 메모리 이미지 버퍼 (BUF-01, BUF-02)
- Phase 22: CXP SDK 확정 (HW-01)
- Phase 23: CXP 드라이버 통합 (HW-02)
- Phase 24: 검사 워크플로우 end-to-end (WF-01, WF-02)
- Phase 25: 결과 분석 & Export (OUT-01, OUT-02, OUT-03, OUT-04)
- Phase 26: 헝가리안 전체 리팩토링 (QUAL-01)
- **Phase 27 — Side Inspection 확장 (신설 2026-05-08):**
  - Plan 27-01: LineToLineAngle 알고리즘 구현 (D1, H5 대응)
  - Plan 27-02: Side Fixture INI 설정 추가 (단변1/2, 장변3/4)
  - Plan 27-03: PC2 Side 전용 구성 검증 (TCP Vision Server 독립 동작)
- **Phase 28 — FAI CircleDiameter + Datum Circle 알고리즘 통합 (신설 2026-05-08):**
  - 사용자 요청: FAI 의 CircleDiameter 측정에 Datum CircleTwoHorizontal 의 폴라 샘플링 + 파라미터 적용
  - 스코프: Plans TBD (spec phase 에서 명확화 — 가져올 파라미터 범위 + 알고리즘 호출 경로 + ROI 입력 방식)
  - 의존: Phase 19 (PropertyTools.Wpf 콤보 패턴 + ICustomTypeDescriptor 동적 hide 검증 완료)

**Phase 27 배경:**
- D1 (Fixture #3-1, #3-2): Back light, Datum A vs 벽면 직선 각도 측정
- H5 (Fixture #4-2): Back light, Datum A vs 직선 MN 각도 측정
- Side Datum (단변1/2, 장변3/4): 기존 TwoLineIntersect 재사용 — 신규 알고리즘 불필요
- PC 구성: PC1(Top/Bottom) / PC2(Side) 분리, 동일 SW 독립 배포

**Phase 28 배경:**
- 현재 CircleDiameterMeasurement (Phase 6 D-15): VisionAlgorithmService.TryFindCircle (단순 FitCircleContourXld) — Sigma/EdgeThreshold/EdgePolarity 3 파라미터
- Datum CircleTwoHorizontal (Phase 16~18): 폴라 샘플링 + Circle_RadialDirection (Inward/Outward) + Circle_EdgeDirection/EdgeSelection + RectL1Ratio/L2Ratio strip cap (Phase 18 CO-01 검증)
- 사용자 의도: FAI 측정에서도 Datum 동일한 검출 정밀도/파라미터 사용

**See:** [ROADMAP.md](ROADMAP.md), [REQUIREMENTS.md](REQUIREMENTS.md)

---

## v1.0 Halcon Migration MVP (Shipped: 2026-05-04)

**Phases completed:** 17 phases, 55 plans, 61 tasks
**Timeline:** 2026-03-17 → 2026-05-04 (49 days)
**Codebase:** 64,057 LOC C# (158 files) + 3,329 LOC XAML
**Commits:** 330 (feat 99 / fix 45 / docs 155 / chore·refactor 31)
**Known deferred items at close:** 22 (see STATE.md `## Deferred Items` — quick task artifacts 11 + UAT partial 7 + verification 4; carry to v1.1)

**Key accomplishments:**

1. **MIL+OpenCV → Halcon 24.11 마이그레이션 완료** — NewDDA 원본 vision 파이프라인을 HOperatorSet/HImage 기반으로 전면 재구성
2. **Shot-FAI 2계층 동적 검사 모델** — 100+ 검사 항목 런타임 추가/삭제 + INI 하위호환 (IsDynamicFAIMode 분기)
3. **Halcon 에지 측정 6종 + per-ROI 파라미터** — EdgeDirection/Selection/SampleCount/TrimCount/Polarity 명시 매핑, MeasurePos strip-loop 기반 정밀 측정 (mm)
4. **Datum 좌표계 알고리즘 3종** — TwoLineIntersect / CircleTwoHorizontal / VerticalTwoHorizontal + 런타임 datumTransform 적용
5. **Datum 티칭/검증 UX** — ROI 그리기/이동/삭제 + Edit 모드 + AlgorithmType 동적 PropertyGrid (ICustomTypeDescriptor) + Test Find DetectedOrigin 시각화
6. **TCP Vision Server + 시퀀스 엔진 + Rapid City 확장** — 외부 핸들러 통신 + Top/Side/Bottom 카메라 시퀀스 (Halcon 기반) + Fixture/Multi-Datum + 6 Measurement + 조명 필드 + 새 INI 포맷

**Archives:**
- Roadmap: [milestones/v1.0-ROADMAP.md](milestones/v1.0-ROADMAP.md)
- Requirements: [milestones/v1.0-REQUIREMENTS.md](milestones/v1.0-REQUIREMENTS.md)
- Audit: [milestones/v1.0-MILESTONE-AUDIT.md](milestones/v1.0-MILESTONE-AUDIT.md)
- Phase artifacts (17 dirs): [milestones/v1.0-phases/](milestones/v1.0-phases/)

---
