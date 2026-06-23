---
phase: 53
slug: 2026-06-16-poc-2
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-23
---

# Phase 53 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | none — 프로젝트에 자동화 테스트 프레임워크 없음 (CLAUDE.md: "No test framework detected") |
| **Config file** | none |
| **Quick run command** | `MSBuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` (빌드 = 1차 검증) |
| **Full suite command** | 빌드 PASS + SIMUL_MODE 수동 UAT (체커보드 이미지 로드) |
| **Estimated runtime** | 빌드 ~60–120초 |

---

## Sampling Rate

- **After every task commit:** Debug/x64 빌드 PASS 확인
- **After every plan wave:** 빌드 PASS + 해당 wave 산출물 SIMUL 스모크
- **Before `/gsd-verify-work`:** 전체 빌드 green + SIMUL UAT 시나리오 실행
- **Max feedback latency:** 빌드 ~120초

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 53-01-* | 01 | 1 | CAL-01 | — | N/A | build | `MSBuild … /p:Platform=x64` | ❌ W0 | ⬜ pending |
| 53-02-* | 02 | 2 | CAL-01 | — | N/A | manual (SIMUL) | 이미지 로드→코너검출→mm/px 산출 | ❌ W0 | ⬜ pending |
| 53-03-* | 03 | 2/3 | CAL-01 | — | N/A | manual (SIMUL) | [적용]→전체 shot PixelResolution 반영→저장 | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- 자동화 프레임워크 미도입 결정 — Wave 0 설치 없음. 검증은 빌드 + SIMUL 수동 UAT.
- *Existing infrastructure (빌드 + SIMUL_MODE)가 모든 phase 검증을 커버.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| 체커보드 코너 검출 → mm/px 산출 | CAL-01 | HALCON 이미지 처리 결과는 시각 확인 필요, 실측 이미지 미확보 | SIMUL: 인터넷 체커보드 이미지 로드 → 코너 검출 오버레이 + mm/px 리포트 표시 확인 |
| 외곽 왜곡 검증 리포트 (D-05) | CAL-01 | 중앙↔외곽 편차% + 임계 경고는 수치+라벨 시각 확인 | 산출 후 편차% 표시 + 1% 초과 시 경고 라벨 확인 |
| [적용] → 전체 shot PixelResolution 반영 (D-03/D-06) | CAL-01 | 레시피 상태 변경, 사용자 확인 흐름 | [적용] 클릭 → 활성 시퀀스 전체 shot PixelResolution 갱신 + 저장 → 재로드 시 유지 확인 |
| 라이브 촬상 (실 HW) | CAL-01 | 실 HW 의존, SIMUL 미동작 | SIMUL_MODE: 라이브 버튼 비활성 폴백 확인 / 실 HW: 라이브 정지→촬상 |

---

## Notes

- 정확도·왜곡 정량 UAT 는 실측 체커보드 이미지 확보 후 (CONTEXT specifics). 이번 phase 는 검출 동작 + wiring 까지 1차 확인.
