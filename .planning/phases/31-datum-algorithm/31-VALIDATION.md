---
phase: 31
slug: datum-algorithm
status: planned
nyquist_compliant: true
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
| 31-01-01 | 01 | 1 | D-03 | T-31-01 | 인터페이스 계약 정의 | grep | `findstr interface IDatumOriginConsumer` | ✅ | ⬜ pending |
| 31-01-02 | 01 | 1 | D-04, D-10 | T-31-02/03 | 0-나눗셈/판별식 가드 | grep | `findstr ComputeProjectionDistance TryFitArc` | ✅ | ⬜ pending |
| 31-01-03 | 01 | 1 | D-03 | T-31-04 | 하드코딩 분기 제거 | build | `MSBuild ... /t:Rebuild` | ✅ | ⬜ pending |
| 31-02-01 | 02 | 2 | D-01 | T-31-05 | TryFindCircle 실패 안전 종결 | grep | `findstr class CircleCenterDistanceMeasurement` | ✅ | ⬜ pending |
| 31-02-02 | 02 | 2 | D-05, D-08 | T-31-05/06 | TryFitLine "All" 명시 | grep | `findstr class EdgeToLineAngleMeasurement` | ✅ | ⬜ pending |
| 31-02-03 | 02 | 2 | D-01 | T-31-05 | 타입 dispatch 등록 | build | `MSBuild ... /t:Rebuild` | ✅ | ⬜ pending |
| 31-03-01 | 03 | 3 | D-01, D-10 | T-31-09/10 | 호 비수렴/교점 0개 가드 | build | `MSBuild ... /t:Build` | ✅ | ⬜ pending |
| 31-03-02 | 03 | 3 | D-07, D-09, D-11 | T-31-08 | 다단계 체인 실패 안전 종결 | build | `MSBuild ... /t:Build` | ✅ | ⬜ pending |
| 31-03-03 | 03 | 3 | D-11 | T-31-08 | 7종 타입 dispatch 등록 | build | `MSBuild ... /t:Rebuild` | ✅ | ⬜ pending |
| 31-04-01 | 04 | 4 | CO-23.1-02 | — | ROI 버튼 일반화 | build | `MSBuild ... /t:Build` | ✅ | ⬜ pending |
| 31-04-02 | 04 | 4 | CO-23.1-01 | — | 듀얼 뷰어 UI 형태 결정 | checkpoint | N/A (decision) | ✅ | ⬜ pending |
| 31-04-03 | 04 | 4 | CO-23.1-01 | T-31-12 | TeachingImagePath File.Exists 가드 | build | `MSBuild ... /t:Rebuild` | ✅ | ⬜ pending |
| 31-05-01 | 05 | 5 | D-01..D-08 | — | 최종 통합 빌드 | build | `MSBuild ... /t:Rebuild` | ✅ | ⬜ pending |
| 31-05-02 | 05 | 5 | CO-23.1-01/02 | T-31-15 | SIMUL UAT 사인오프 | UAT | N/A (manual) | ❌ W0 (31-UAT.md → 31-01) | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*
*31-UAT.md 는 Plan 01 Task 3 에서 scaffold 생성된다 (Wave 0 gap 해소).*

---

## Wave 0 Requirements

- [ ] `.planning/phases/31-datum-algorithm/31-UAT.md` — 신규 측정 타입 7종 + carry-over 2건 UAT 시나리오 scaffold (Plan 01 Task 3 에서 생성)
- [ ] SIMUL 이미지 준비 (Bottom Fixture #2 / Side Fixture #3 / Top Fixture #2) — 사용자 제공
- [ ] `IDatumOriginConsumer.cs` 신규 파일 + csproj Compile ItemGroup 등록 (Plan 01 Task 1)
- [ ] 신규 .cs 파일 각각 csproj Compile ItemGroup 등록 확인 (Plan 02/03 각 task)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| 신규 측정 타입 7종 측정값 정확성 (mm/deg) | E8/D1/H5/I9/I10/E2/E9/E10/ArcEdge | Halcon 이미지 측정 — 실측 결과는 SIMUL 이미지로만 검증, 자동화 불가 | SIMUL_MODE 에서 각 타입 ROI 티칭 후 측정 실행, 31-UAT.md 시나리오 따라 공차 판정 확인 |
| CO-23.1-01 듀얼 이미지 표시 | CO-23.1-01 | 시각적 UI 동작 — TeachingImagePath ≠ InspectionImagePath 분리 표시 | MainView 에서 datum/측정 이미지 개별 표시 육안 확인 |
| CO-23.1-02 신규 타입 Rect ROI 버튼 활성화 | CO-23.1-02 | UI 상태 — 측정 타입 선택 시 버튼 enable/disable | 각 신규 타입 선택 시 ROI 버튼 활성 상태 확인 |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify (build) or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without build verify
- [x] Wave 0 covers all MISSING references (31-UAT.md → Plan 01 Task 3, IDatumOriginConsumer.cs → Plan 01 Task 1)
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** planned (2026-05-19) — 5 plans, 5 waves
