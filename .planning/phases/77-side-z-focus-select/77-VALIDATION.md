---
phase: 77
slug: side-z-focus-select
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-09-15
---

# Phase 77 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> 출처: `77-RESEARCH.md` §Validation Architecture (MSBuild 경로는 이 PC 실측값으로 교정).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | 없음 — 단위 테스트 프레임워크 미도입(정책 유지). 검증 = 빌드 + grep 정적 단언 + SIMUL 수동 실행 로그 + SIDE 실기 UAT |
| **Config file** | none |
| **Quick run command** | `"/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -m -nologo -v:minimal -clp:ErrorsOnly` |
| **Full suite command** | Quick run + 가독성 grep 5종(추가 줄 기준) + 아래 요구사항별 grep 단언 전항목 |
| **Estimated runtime** | ~60–120 seconds (빌드) |

⚠ **Release|x64 빌드 금지 (실행 단계):** `DatumMeasurement.csproj` 의 Release|x64 `OutputPath` 는 `D:\Data\` — 빌드만 해도 현장 배포 exe 를 덮어쓴다. 검증 빌드는 **Debug|x64 만**. 배포는 사용자 앱 종료 확인 후 별도.

---

## Sampling Rate

- **After every task commit:** Debug|x64 빌드 PASS + CLAUDE.md 가독성 grep 5종(삼항 / `??` / `?.` / switch 식 / `hbk` 날짜주석)이 **이번 커밋의 추가 줄(`git diff -U0 | grep '^+'`)에서 0건**
- **After every plan wave:** 위 + 아래 Per-Task 표의 grep 단언 전항목 재확인
- **Before `/gsd-verify-work`:** Debug|x64 빌드 PASS + SIMUL 1 Shot(EdgeToLineDistance) end-to-end 수동 실행 로그(`z-select` 라인) 확인
- **Max feedback latency:** 120 seconds

---

## Per-Task Verification Map

> Task ID 는 계획 확정 후 planner 가 채운다. 아래는 요구사항별 최소 단언.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 77-TBD | TBD | 1 | SZF-01 | — | N/A | build + grep | `grep -n "ZIndexStart\|ZIndexEnd" WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` + Debug|x64 빌드 | ✅ (기존 파일) | ⬜ pending |
| 77-TBD | TBD | 1 | SZF-02 | T-77-01 | 레시피 범위 밖 z 는 저장 안 함(무한 증식 방지), 평가 후 Dispose | grep + UAT | `grep -n "ZRange" WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` (저장/평가/정리 호출부 존재) | ✅ | ⬜ pending |
| 77-TBD | TBD | 1 | SZF-03 | — | N/A | build + grep + SIMUL | `grep -n "EdgeStrengthScore" WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs` + SIMUL 로그 strip 별 최대 amp·합·분모 육안 대조 | ✅ | ⬜ pending |
| 77-TBD | TBD | 2 | SZF-04 | — | N/A | grep + CSV diff | `grep -n "SelectedZ" WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs` + 기존 CSV 로드 호환 확인 | ✅ | ⬜ pending |
| 77-TBD | TBD | all | SZF-05 | — | N/A | diff 정적 단언 | 범위 꺼짐(0/0) 경로 함수(`FindShotByZIndex`, `FindActionIndicesByZIndex`, `RunGrab`, `TryExecuteMeasurement`, 기존 `TryFitLine` 호출부)의 diff 가 **가드 추가 외 기존 줄 삭제·변경 0** (`git diff --numstat` 삭제 줄 검토) | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- 단위 테스트 부재 → `EdgeStrengthScore` 계산식(Σ strip 최대 |amp| ÷ EdgeSampleCount, 에지 없는 strip = 0 — D-77-05)은 **Algorithm 로그에 strip 별 값·합·분모를 출력**해 육안 대조로 대체 (`[FitLine] strips ok ...` 기존 로그 관례)
- Framework install: 불필요

*Existing infrastructure covers all phase requirements (빌드 + grep + 로그 + UAT).*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| PLC 가 범위 z 를 순서대로 보낼 때 영상 누적 → 마지막 z 에서 측정별 선택·결과 전송 | SZF-02, SZF-03 | 실제 PLC·카메라·Z 축 필요, **O-1 z 번호 배정 제어팀 협의 후** | SIDE PC: 범위 켠 Shot 1개로 자동 사이클 → Algorithm 로그 z-select 라인(측정별 후보 z·점수·선택 z) + 결과 CSV 선택 Z 열 확인 |
| 반복 사이클에서 선택 Z 흔들림으로 측정값이 튀지 않음 | SZF-03 | 실물 반복 촬영 필요 | 같은 부품 10회 → 측정별 선택 Z 분포·측정값 범위 비교 (동점 규칙 동작) |
| 중간 z 누락·반복 전송 시 사이클 완주 | SZF-02 | PLC 시나리오 필요 | z 하나 빼고 전송 / 같은 z 두 번 전송 → 경고 로그 + 사이클 완주 |
| 범위 꺼진 Shot·TOP/BOTTOM·수동 RUN 기존 동작 동일 | SZF-05 | 실측값 비교 필요 | 기존 레시피로 자동 사이클·수동 RUN → 이전 버전과 측정값 동일 |
| Z 이동 중 XY 흔들림 영향 | SZF-03 | 미검증 전제(O-9) | 같은 Z 반복 vs 다른 Z 선택 시 측정값 차이 비교 |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
