---
phase: 78
slug: reviewer-ng-cause-analysis
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-09-17
---

# Phase 78 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | 없음 (xUnit/NUnit/MSTest 부재) — Framework `csc.exe` 로 컴파일한 리플렉션 probe + 실 cycle.json 데이터 비교 (Quick 260915-k5g · Phase 77-03 패턴) |
| **Config file** | none — probe 는 저장소 밖 스크래치 경로 |
| **Quick run command** | `"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -m -nologo -v:minimal -clp:ErrorsOnly` |
| **Full suite command** | Debug\|x64 빌드 + probe 재컴파일·실행 (`csc -platform:x64 -lib:WPF_Example/bin/x64/Debug -r:DatumMeasurement.exe NgCauseProbe.cs`) 대상 `D:/Data/Result/20260601,20260811,20260915,20260916,20260917` (읽기 전용) |
| **Estimated runtime** | ~90 seconds (빌드) + ~30 seconds (probe) |

---

## Sampling Rate

- **After every task commit:** Debug\|x64 빌드 + CLAUDE.md 하드룰 grep (삼항·`??`·`?.`·switch식·`hbk`) on added lines
- **After every plan wave:** probe 재실행 — 옛 JSON(20260601/20260811) 로드 회귀 + 신규 데이터(20260915~17) 원인 판정
- **Before `/gsd-verify-work`:** 성공 기준 3 (R5 기준점 흔들림 · R6 치우침 · R8 초점 범위 끝값) 실데이터 판정 확인
- **Max feedback latency:** 120 seconds

---

## Per-Task Verification Map

(planner 가 PLAN 작성 후 Task ID 로 채운다 — 아래는 요구사항 단위 매핑)

| Requirement | Behavior | Test Type | Automated Command | File Exists | Status |
|-------------|----------|-----------|-------------------|-------------|--------|
| NGA-01 | R1~R9 규칙이 실 cycle.json 에서 기대 원인으로 분류 | 리플렉션 probe + 실데이터 assertion | `NgCauseProbe.exe <binDir> <dateDirs...>` | ❌ W0 | ⬜ pending |
| NGA-02 | NG 행 선택 시 원인·근거·확인할 일 3줄 표시, code-behind 배선만 | 빌드 + grep 게이트(branch_kw/codelines) | `grep -cE '\bif\b|\bswitch\b'` on ReviewerWindow.xaml.cs added lines | ❌ W0 | ⬜ pending |
| NGA-03 | NG 누적 엑셀 재실행 시 중복 없음 | probe (ClosedXML 로 xlsx 열어 키 유일성) | `NgCauseProbe.exe --xlsx <scratch path>` | ❌ W0 | ⬜ pending |
| NGA-04 | 버튼 2개 삭제, 반복검사 묶음·Align 정합 조회 유지 | 빌드 + grep | `grep -c "btn_chartSmoke\|btn_exportExcel" ReviewerWindow.xaml` == 0 | ✅ | ⬜ pending |
| NGA-05 | 옛 cycle.json 로드 무크래시, 없는 데이터 규칙은 건너뜀 | probe | 20260601/20260811 로드 비교 | ❌ W0 | ⬜ pending |
| NGA-06 | 리뷰어가 OriginImageFileName 우선, 없으면 ResultImagePath 폴백 | probe (경로 해석 함수 리플렉션 호출) | 20260915~17 샘플 File.Exists | ❌ W0 | ⬜ pending |
| NGA-07 | 기준점 각도·원점·매칭 점수, Z 후보 점수 cycle.json 기록 + 옛 파일 호환 | 빌드 + 수동 1사이클 cycle.json 확인 | 사람 확인 (실행 하네스 부재) | ❌ manual | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `NgCauseProbe.cs` (저장소 밖 스크래치) — R1~R9 판정 assertion, 옛/신규 JSON 로드, 사진 경로 해석
- [ ] xlsx append/dedupe 확인 섹션 (스크래치 경로에만 xlsx 생성, D:/Data 쓰기 금지)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| 신규 진단 필드가 실제 사이클 cycle.json 에 기록 | NGA-07 | 장비/PLC 사이클 실행 하네스 없음 | 배포 후 SIDE 자동 사이클 1회 → 해당 cycle.json 에 기준점 각도·점수, Z 후보 점수 존재 확인 |
| 리뷰어 원인 패널 가독성 (비전 초보 기준) | NGA-02 | 사람 판단 | 리뷰어 → 20260917 → NG 행 클릭 → 3줄 문구 이해 가능 여부 |
| NG 누적 엑셀 열어 보기 | NGA-03 | 사람 판단 | 버튼 2회 실행 후 파일 열어 중복 없음·원인 열 확인 |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
