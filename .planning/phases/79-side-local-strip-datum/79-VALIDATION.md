---
phase: 79
slug: side-local-strip-datum
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-09-18
---

# Phase 79 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | 없음 (단위 테스트 프로젝트 없음 — 빌드 + 하드룰 grep + 로그/오프라인 재검사 프로브) |
| **Config file** | none |
| **Quick run command** | `"/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -m -nologo -v:minimal -clp:ErrorsOnly` |
| **Full suite command** | Quick run + CLAUDE.md 하드룰 grep 5종(신규/수정 파일, 전부 0) + 옵션 OFF 오프라인 재검사 값 비교 |
| **Estimated runtime** | ~90 seconds (빌드) |

Release 빌드 금지 (D:/Data 배포 exe 보호). Debug|x64 만.

---

## Sampling Rate

- **After every task commit:** Quick run (Debug|x64 빌드, 에러 0)
- **After every plan wave:** Full suite (빌드 + 하드룰 grep + 옵션 OFF 회귀 확인)
- **Before `/gsd-verify-work`:** Full suite green
- **Max feedback latency:** 120 seconds

---

## Per-Task Verification Map

계획 확정 후 planner 가 Task ID 를 채운다. 요구사항별 검증 수단:

| Requirement | Behavior | Test Type | Automated Command | File Exists | Status |
|-------------|----------|-----------|-------------------|-------------|--------|
| LSR-01 | 옵션·기준 ROI·전용 에지 설정 필드 존재, 옛 레시피 로드 시 기본값 유지 | 정적 grep + 빌드 | 필드명 grep, `MeasurementBase.Load` override grep | ✅ 기존 파일 확장 | ⬜ pending |
| LSR-02 | 국부 기준선을 Datum 검출 tick 에서 1번만 계산 (Z 후보 수와 무관) | 로그 | `[LocalRef]` 로그 횟수 = Datum 검출 성공 횟수 | ❌ W0 | ⬜ pending |
| LSR-03 | 기준 ROI 실패 시 전역 기준 전환 + 로그, 사이클 계속 | 오프라인 재검사 + 로그 | 기준 ROI 를 빈 영역에 두고 재검사 → 전환 로그·결과 존재 | ❌ W0 | ⬜ pending |
| LSR-04 | 사용 기준(국부/전역/전환) 화면·cycle.json·CSV 표시 | grep + 육안 | cycle.json 신규 필드 grep, CSV `COLUMN_COUNT` 불변 grep | ❌ W0 | ⬜ pending |
| LSR-05 | 옵션 OFF = 현재와 동일 | grep + 값 비교 | 옵션 OFF 레시피 오프라인 재검사 값 전/후 동일 | ❌ W0 | ⬜ pending |
| LSR-06 | C13·C14 6점 자재 A·B 편차 감소 | 수동(오프라인 재검사 + 수치 비교) | — | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] P2 z1 사진 핀 위치 재프로브 (조사 Open Question)
- [ ] 실제 HALCON `TryFitLine` 기반 스모크 — 09-17 z1 사진에서 기준 ROI 피팅 성공·에지 설정 확정
- [ ] Debug|x64 빌드 경고 baseline 재측정

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| 기준 ROI 티칭·오버레이 표시 | LSR-01, LSR-04 | WPF 화면 조작 | 측정 선택 → 기준 ROI 그리기·이동 → 국부 기준선 색 구분 확인 |
| 자재 A·B 편차 감소 | LSR-06 | 실데이터·사용자 판단 | C13·C14 6점에 켜고 09-17 A·B 사진 재검사 → A−B 가 전역 기준보다 뚜렷이 작은지 |
| 기준값·공차 재확인 | LSR-06 | 0점이 바뀜 | 켠 측정마다 새 값 확인 후 기준값·공차 결정 |
| TOP·BOTTOM 동작·전환 | LSR-03, LSR-05 | 레시피별 확인 | TOP·BOTTOM 측정 1~2개에 켜서 동작·전환 확인 |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
