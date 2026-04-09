---
phase: 3
slug: edge-measurement
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-09
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | No test framework (WPF desktop app, .NET Framework 4.8) |
| **Config file** | none |
| **Quick run command** | `msbuild /p:Configuration=Debug /p:Platform=x64 /t:Build` |
| **Full suite command** | `msbuild /p:Configuration=Debug /p:Platform=x64 /t:Rebuild` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run build command to verify compilation
- **After every plan wave:** Full rebuild + manual SIMUL_MODE verification
- **Before `/gsd-verify-work`:** Full rebuild must succeed
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 3-01-01 | 01 | 1 | ALG-01 | — | N/A | build | `msbuild /p:Configuration=Debug /p:Platform=x64` | ✅ | ⬜ pending |
| 3-01-02 | 01 | 1 | ALG-02 | — | N/A | build | `msbuild /p:Configuration=Debug /p:Platform=x64` | ✅ | ⬜ pending |
| 3-02-01 | 02 | 2 | ALG-04 | — | N/A | build+manual | `msbuild /p:Configuration=Debug /p:Platform=x64` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

*Existing infrastructure covers all phase requirements. No test framework to install — validation relies on build success and manual SIMUL_MODE testing.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| MeasurePos 에지 거리(mm) 계산 | ALG-01 | Halcon 알고리즘은 실제 이미지 필요 | SIMUL_MODE에서 테스트 이미지 로드 → FAI 측정 실행 → 거리 값 확인 |
| OK/NG 공차 판정 | ALG-02 | 판정 로직은 FAIConfig Tolerance 값에 의존 | Tolerance 범위 내/외 값으로 각각 테스트 → 판정 결과 확인 |
| 캔버스 오버레이 표시 | ALG-04 | UI 렌더링은 시각적 확인 필요 | 측정 후 캔버스에서 에지 위치 마커, 거리 값 텍스트, OK/NG 색상 확인 |

---

## Validation Sign-Off

- [ ] All tasks have build verify
- [ ] Manual verification steps documented for all requirements
- [ ] Build succeeds after each task commit
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
