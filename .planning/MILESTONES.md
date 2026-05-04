# Milestones

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
