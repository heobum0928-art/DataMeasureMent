---
phase: 23
slug: top-1-a-simul-end-to-end-2026-05-11
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-11
---

# Phase 23 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | none (.NET Framework 4.8 WPF — no xUnit/NUnit/MSTest project in repo) |
| **Config file** | none — verification via msbuild + manual Simul UAT |
| **Quick run command** | `msbuild WPF_Example\DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /v:minimal` |
| **Full suite command** | `msbuild WPF_Example\DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /v:minimal` + Simul UAT (사용자 5/5 PASS) |
| **Estimated runtime** | ~30s build + ~5min UAT |

---

## Sampling Rate

- **After every task commit:** Run quick msbuild Debug/x64 (warning baseline = 6 occurrences)
- **After every plan wave:** Run quick msbuild + 정적 grep 검증 (MeasurementFactory 7 case / GetTypeNames / //260511 hbk Phase 23 marker)
- **Before `/gsd-verify-work`:** msbuild PASS (신규 warning 0) + 사용자 Simul UAT 완주
- **Max feedback latency:** ~30 seconds (msbuild)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 23-XX-XX | TBD by planner | TBD | ALG-01 | — | N/A (단일 사용자 데스크탑 앱) | static + manual UAT | `msbuild` + grep marker | ✅ baseline | ⬜ pending |

*Planner 가 task 별로 채움. 자동 단위 테스트 없음 — msbuild + 정적 grep + Simul UAT 조합으로 Nyquist 충족.*

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] xUnit/NUnit 도입 없음 — 기존 패턴 유지 (Phase 1~22 동일)
- [ ] 사용자가 Simul 이미지 + Datum INI 사전 배치 확인 (D:\TestImg\Datameasurement\ + DatumConfig CTH 설정)

*테스트 프레임워크 신규 도입 금지 (CLAUDE.md tech-stack lock). 기존 인프라 = msbuild + 정적 grep + 사용자 UAT.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Simul end-to-end 완주 (이미지 로드 → Datum 자동 찾기 → A1~A5 측정값 UI 표시) | ALG-01 SC#1 | HALCON 비전/GUI 통합, 단위 테스트 불가 | 1) Top #1 Simul 이미지 + Datum CTH INI 준비, 2) 프로그램 기동 → InspectionListView 펼침, 3) A1~A5 측정값 + mm 단위 표시 확인 |
| OK/NG strip 녹/적 시각화 | ALG-01 SC#2 | UI 색상, GUI dependent | 공차 범위 내/외 두 케이스 모두 시각 확인 |
| TeachingImagePath ≠ InspectionImagePath 분리 동작 | ALG-01 SC#3 | 파일시스템 경로 분리, 통합 시나리오 | 두 INI 경로 다르게 설정 + 동작 → 두 경로 같게 설정 + 동작 (회귀 0) |
| A6 추가 INI 만으로 확장 (코드 변경 0) | ALG-01 SC#4 | INI + 재시작 흐름, 사용자 행동 | INI 직접 편집 (A6 섹션 추가) → 재시작 → A6 표시 / UI 'Add FAI' 버튼 채널도 동일 검증 |
| msbuild Debug/x64 PASS, 신규 warning 0 | ALG-01 SC#5 | 빌드 검증 | `msbuild ... /v:minimal` 출력 warning count = Phase 21 baseline 6 |

---

## Validation Sign-Off

- [ ] 모든 task 가 manual UAT 또는 msbuild/grep 자동 검증과 매핑됨
- [ ] Sampling continuity: 모든 wave 종료 시 msbuild + grep 자동 검증 완료
- [ ] Wave 0 = 없음 (테스트 인프라 추가 없음 - 기존 패턴 유지)
- [ ] watch-mode flag 없음
- [ ] Feedback latency < 30s (msbuild)
- [ ] `nyquist_compliant: true` set in frontmatter (planner 가 task 매핑 완료 후 갱신)

**Approval:** pending
