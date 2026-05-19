---
phase: 31
slug: datum-algorithm
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-19
---

# Phase 31 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | none — 자동화 테스트 프레임워크 미도입 (CLAUDE.md: "No test framework detected") |
| **Config file** | none |
| **Quick run command** | `MSBuild WPF_Example\DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /v:minimal /t:Rebuild` |
| **Full suite command** | SIMUL_MODE 수동 UAT (31-UAT.md) |
| **Estimated runtime** | ~60 seconds (build) |

---

## Sampling Rate

- **After every task commit:** Run `MSBuild WPF_Example\DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /v:minimal /t:Rebuild`
- **After every plan wave:** Build PASS + SIMUL_MODE 에서 1개 신규 타입 측정 정상 동작 확인
- **Before `/gsd-verify-work`:** 31-UAT.md 전 시나리오 PASS
- **Max feedback latency:** 60 seconds (build)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 31-XX-XX | XX | X | (planner fills) | — | N/A | build / UAT | `MSBuild ... /t:Rebuild` | ✅ / ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*
*Planner: populate one row per task once PLAN.md files exist.*

---

## Wave 0 Requirements

- [ ] `.planning/phases/31-datum-algorithm/31-UAT.md` — 신규 측정 타입 6종 + carry-over 2건 UAT 시나리오 scaffold
- [ ] SIMUL 이미지 준비 (Bottom Fixture #2 / Side Fixture #3 / Top Fixture #2) — 사용자 제공
- [ ] `IDatumOriginConsumer.cs` 신규 파일 + csproj Compile ItemGroup 등록
- [ ] 신규 .cs 파일 각각 csproj Compile ItemGroup 등록 확인

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| 신규 측정 타입 6종 측정값 정확성 (mm/deg) | E8/D1/H5/I9/I10/E2/E9/E10/ArcEdge | Halcon 이미지 측정 — 실측 결과는 SIMUL 이미지로만 검증, 자동화 불가 | SIMUL_MODE 에서 각 타입 ROI 티칭 후 측정 실행, 31-UAT.md 시나리오 따라 공차 판정 확인 |
| CO-23.1-01 듀얼 이미지 표시 | CO-23.1-01 | 시각적 UI 동작 — TeachingImagePath ≠ InspectionImagePath 분리 표시 | MainView 에서 datum/측정 이미지 개별 표시 육안 확인 |
| CO-23.1-02 신규 타입 Rect ROI 버튼 활성화 | CO-23.1-02 | UI 상태 — 측정 타입 선택 시 버튼 enable/disable | 각 신규 타입 선택 시 ROI 버튼 활성 상태 확인 |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify (build) or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without build verify
- [ ] Wave 0 covers all MISSING references (31-UAT.md, IDatumOriginConsumer.cs)
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
