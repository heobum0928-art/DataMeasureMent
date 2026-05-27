----
phase: 34-datum-verticaltwohorizontal-2026-05-26
plan: 04
status: partial
gates: PARTIAL
updated: 2026-05-27
total: 5
passed: 2
issues: 0
pending: 3
skipped: 0
blocked: 0
----

# Phase 34 UAT — Datum VerticalTwoHorizontal Dual Image

**Sign-off:** 2026-05-27 (partial)
**Carry-over target:** Phase 34.1 (Datum DualImage swap UX)

## Summary Table

| # | Test | Result | User Confirmed | Evidence |
|---|------|--------|----------------|----------|
| 1 | msbuild Debug/x64 PASS + 신규 warning 0 | PASS | 2026-05-27 | `.planning/tmp/msbuild-34-04.log` (exit=0, errors=0, warnings=14 모두 baseline) |
| 2 | 기존 1-image 3 algorithm (TLI/CTH/VTH) 회귀 0 | PENDING | — | swap UX hotfix 후 일괄 재실행 예정 |
| 3 | 신규 DualImage SIMUL UAT | PARTIAL | 2026-05-27 | 3-a (콤보 노출) / 3-b (TeachingImagePath_Vertical 필드 노출) PASS · 3-d (자동 swap UX 갭) FAIL · 3-c/e/f 재검증 필요 |
| 4 | INI 라운드트립 (TeachingImagePath_Vertical 보존 + 미존재 키 회귀 0) | PENDING | — | swap UX hotfix 후 재실행 |
| 5 | D-34-13/14 가드 (InspectionSequence/VisionResponsePacket 변경) | PASS | 2026-05-27 | `git diff --numstat f0e7794 HEAD`: VisionResponsePacket=0/0 · Action_FAIMeasurement hunks=2 (≤3) |

## Test Details

### Test 1 — msbuild PASS (자동)
- 명령: `MSBuild.exe WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Rebuild`
- 결과: exit=0, errors=0, warnings=14 (전부 baseline — MSB3884×2 / CS0618×10 deprecation / CS0162×2 unreachable code, Phase 33 이전)
- **사용자 합의**: PASS

### Test 5 — D-34-13/14 가드 (자동)
- `git diff --numstat f0e7794 HEAD` 기준 (Phase 34 base = f0e7794):

| 파일 | 변경 | Plan 자체 acceptance | 핵심 가드 |
|------|------|---------------------|----------|
| InspectionSequence.cs | +16/-0 | ≤15 (Plan 03 본문 inconsistency — +1 deviation) | D-34-14 ≤5 seam: 정정 허용 범위 안 |
| VisionResponsePacket.cs | 0/0 | 0/0 | ✅ D-34-14 핵심 PASS |
| EDatumAlgorithm.cs | +1/-0 | +1 | ✅ |
| DatumConfig.cs | +16/-0 | +8~12 (Plan 01 inconsistency — +4 deviation) | — |
| DatumFindingService.cs | +399/-0 | +350~400 | ✅ |
| Action_FAIMeasurement.cs | +78/-13, hunks=2 | hunks ≤3, +50~65 (라인 +13 deviation) | ✅ D-34-13 hunks PASS |
| MainView.xaml.cs | +98/-5 | +70~100 | ✅ |

- **사용자 합의**: PASS — 핵심 가드 (VisionResponsePacket 0/0 + Action_FAIMeasurement hunks≤3) 충족. 라인 수 deviation 3건은 Plan 본문이 제공한 코드 자체의 self-inconsistency 로 Plan 01/03 SUMMARY 에 명시됨.

### Test 3 — 신규 DualImage SIMUL UAT (PARTIAL)

| Sub-test | 설명 | 결과 | 비고 |
|----------|------|------|------|
| 3-a | AlgorithmType 콤보에 `VerticalTwoHorizontalDualImage` 항목 존재 | PASS | 사용자 시각 확인 (UAT 도중) |
| 3-b | TeachingImagePath_Vertical 필드 PropertyGrid 노출 | PASS | 사용자 시각 확인 |
| 3-c | 티칭 wizard HA → HB → V 순서 동작 | NOT-TESTED | wizard 가 자동 진행되지만 swap UX 갭으로 사용자가 어느 이미지를 보는지 혼선 → 신뢰성 있는 검증 불가, Phase 34.1 후 재시도 |
| 3-d | Horizontal_B 완료 직후 halconViewer 가 세로축 이미지로 자동 swap | **FAIL — UX 갭 발견** | 자동 swap 자체는 동작하나 사용자가 어느 이미지를 보고 있는지 명확하지 않음. **사용자 피드백**: "이미지를 사용자가 원하는대로 스왑이 필요할 꺼 같아 이렇게 보면 헷갈려". → Phase 34.1 carry-over |
| 3-e step 1 | Datum 검출 PASS, `DualImage Datum failed` 로그 0건 | NOT-TESTED | 3-d 갭으로 ROI 위치 신뢰성 확보 불가. Phase 34.1 후 재시도 |
| 3-e step 2 | DualImage 측정값 ≠ identity 측정값 (T-34-03-08 해소 증명) | NOT-TESTED | 동일 사유 |
| 3-f | 빈 경로 가드 (TeachingImagePath_Vertical 빈 상태 + 한국어 에러) | NOT-TESTED | 동일 사유 |

- **사용자 합의**: PARTIAL — 3-a / 3-b 만 PASS, 나머지는 Phase 34.1 hotfix 후 재검증.

### Test 2 / Test 4 — PENDING

swap UX hotfix 가 들어가면 MainView 가 변경되므로, Test 2 (1-image 회귀 0) 와 Test 4 (INI 라운드트립) 도 그 시점에 일괄 재검증하는 것이 효율적. Phase 34.1 UAT 와 합쳐서 진행.

## Phase 35 Test 4 Side carry-over 흡수

**부분 흡수** — Phase 35 35-UAT.md Test 4 (Side 실측 SIMUL UAT) 의 일부는 Test 3-a/3-b 로 흡수되었으나, ROI 그리기 / 검출 (3-c/d/e) 까지는 Phase 34.1 의존. Phase 35 Test 4 carry-over 상태도 Phase 34.1 완료 시점까지 연장.

## Carry-over

| ID | 내용 | 처리 phase |
|----|------|-----------|
| CO-34-01 | Datum DualImage swap UX — 사용자가 현재 표시 이미지를 시각적으로 알 수 없고 임의 swap 불가 | **Phase 34.1** |
| CO-34-02 | Test 3-c / 3-e / 3-f 미검증 (swap UX 의존) | Phase 34.1 UAT |
| CO-34-03 | Test 2 (1-image 회귀 0) / Test 4 (INI 라운드트립) 미검증 — Phase 34.1 MainView 변경 후 일괄 재실행 | Phase 34.1 UAT |
| CO-34-04 | Phase 35 Test 4 Side 실측 carry-over — Phase 34.1 까지 연장 | Phase 34.1 완료 후 종결 |

## Plan inconsistency 기록 (deviation 3건)

| 파일 | Plan acceptance | 실제 | 비고 |
|------|----------------|------|------|
| InspectionSequence.cs | ≤15 라인 | +16 | Plan 03 본문이 제공한 코드 자체가 16 라인 — Plan 03 SUMMARY deviation note 에 기재됨. D-34-14 ≤5 seam 정정 범위 안 |
| DatumConfig.cs | +8~12 | +16 | Plan 01 acceptance 텍스트 자체가 PropertyGrid 분기 라인을 누락 — Plan 01 SUMMARY 기재됨 |
| Action_FAIMeasurement.cs | +50~65 | +78 | Plan 03 acceptance 텍스트 자체가 신규 `TryGrabOrLoadDualDatumImages` 본문 라인을 누락 — Plan 03 SUMMARY 기재됨. **D-34-13 핵심 (hunks ≤3) 은 PASS** (실제 hunks=2) |

라인 수 deviation 은 모두 Plan 본문이 제공한 코드와 acceptance 텍스트의 self-inconsistency 이며, 동작/가드 영향 없음.

## Memory 정정 trigger

- `project_phase35_progress.md` 의 Side fixture ROI ↔ image 매핑 명시는 Phase 34 D-34-01 정합과 일치 확인 (가로축 ROI 2개 = TeachingImagePath / 세로축 ROI 1개 = TeachingImagePath_Vertical). 정정 불요.
- 새 메모리 `project_phase34_progress.md` 갱신 — partial sign-off + CO-34-01 → Phase 34.1.

## ⚠ UAT 진행 중 회귀 발견 (2026-05-27)

UAT Test 3 진행 도중 `git status` 검사에서 `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` 가 **HEAD (c5ac271, Plan 34-03 DualImage 분기 포함) 대비 -79/+13 회귀 상태** 로 발견되었음. EStep.DatumPhase 안의 DualImage if-else 분기와 `TryGrabOrLoadDualDatumImages` 호출이 working tree 에서 사라졌고, **msbuild Rebuild (Test 1) 가 이 회귀 상태의 .cs 파일을 컴파일했을 가능성** 존재.

**원인 후보:**
- VS 가 열린 상태에서 외부 도구 (예: pre-existing edit-and-continue 임시 파일) 가 main 의 .cs 를 덮어씀
- 사용자 의도 없는 액션

**복원 처리:** `git checkout HEAD -- Action_FAIMeasurement.cs` 로 c5ac271 상태 복원. 복원 후 DualImage 패턴 3개 (`VerticalTwoHorizontalDualImage` 분기 + `TryGrabOrLoadDualDatumImages` 함수 + 호출 site) 정상 검출.

**Test 3-d FAIL (swap UX 갭) 의 신뢰성 영향:**

| 시나리오 | 발생 가능성 | UAT 결과 영향 |
|---------|-----------|--------------|
| A: msbuild 빌드 시점에는 정상, 그 후 회귀 발생 | 가능 | 사용자 UAT 는 정상 빌드된 exe 로 진행 → Test 3-a/3-b/3-d 결과 신뢰 가능 → swap UX 갭은 **진짜 UX 문제** |
| B: msbuild 가 이미 회귀 상태의 .cs 를 컴파일 | 가능 | 사용자 UAT 는 회귀 빌드 → Test 3-d 가 본 것은 1-image 분기의 오작동 → swap UX 갭의 일부는 회귀 부작용일 수 있음 |

어느 시나리오인지 확신할 수 없으므로 **Phase 34.1 의 첫 task = working tree 복원 후 msbuild Rebuild 재실행 + 동일 시나리오 재현 확인** 으로 안전망 둠. Phase 34.1 UAT 가 swap UX 갭이 정말 UX 문제임을 재확인하면 본 partial sign-off 유효.

**예방 조치 (Phase 34.1 시드 컨텍스트 D-34.1-07 신설):** UAT 시작 전후로 `git status --porcelain` 자동 검사 → working tree dirty 여부 확인하는 절차를 워크플로우에 명시. msbuild Rebuild 직후에도 동일 검사.

## Commits (Phase 34 base = f0e7794)

- 5159c15 / d209dbf / e35a955 / 4521b1e — Plan 34-01
- ec64417 / 7253803 / f7e0e54 — Plan 34-02
- 3fba0d2 / a72b377 / c5ac271 / e4ac134 — Plan 34-03
- 309a29d / af498b2 / d992887 — worktree merge commits
- ea35f39 — SUMMARY restore (orchestrator resurrection-check false positive)
- 0a90c19 / 5c527fd / 9ec0d77 — ROADMAP per-plan marks
