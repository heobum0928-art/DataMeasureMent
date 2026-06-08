---
phase: 38-v1-1-carryover-cleanup-2026-05-28
verified: 2026-05-28T15:00:00Z
status: passed
score: 9/10 must-haves verified (10th = CO-38-01, 사용자 결정으로 carry-over 수용)
overrides_applied: 0
human_resolution:
  - test: "CO-38-01 — 픽셀분해능 런타임/UI 단일소스 (항목별 숨김 + Shot→FAI cascade)"
    resolution: "수용 (A) — 사용자가 2026-05-28 execute-phase UAT 중 carry-over 를 명시적으로 선택. D-10 결정 범위(로딩 시 정규화)는 코드로 VERIFIED, 추가 런타임 UI 단일소스는 CO-38-01 신규 phase 로 이관. phase 38 범위 내 gap 아님."
---

# Phase 38: v1.1 Carry-over Cleanup 검증 보고서

**Phase Goal:** v1.1 누적 carry-over 및 코드/UI 정리 항목 7건(#1 #3 #5 #6 #10 #11 #12)을 한 phase 로 묶어, 운영 영향 0 으로 정리하고 v1.1 을 깔끔하게 종결한다.
**Verified:** 2026-05-28T15:00:00Z
**Status:** passed (CO-38-01 carry-over 사용자 수용)
**Re-verification:** No — 초기 검증

---

## Step 0: 이전 검증 없음

이전 VERIFICATION.md 없음 — 초기 검증 모드.

---

## Goal Achievement

### Observable Truths

| # | Truth (출처) | Status | Evidence |
|---|-------------|--------|----------|
| 1 | GetTypeNames() 에서 미사용 5종이 UI 에서 제거됐다 | VERIFIED | `MeasurementFactory.cs` L55-68: 반환 배열 10종, `EdgePairDistance` 등 5종 부재 (grep 직접 확인) |
| 2 | Create() switch 에 5종 case 가 그대로 존재해 INI 로딩 호환이 유지된다 | VERIFIED | `MeasurementFactory.cs` L16-27: `case "EdgePairDistance":` 포함 5종 case 모두 잔존 |
| 3 | FAI PixelResolution 이 로딩 시 카메라 단일값으로 정규화된다 | VERIFIED | `InspectionRecipeManager.cs` L309-317: CAM 섹션 존재 가드 + `camRes = shot.PixelResolution` + `fai2.PixelResolutionX/Y = camRes` 블록 확인 (WR-01 fix 포함) |
| 4 | AngleTolerance 기본값 0.0 (sentinel OFF) — 신규 Datum 배지 미표시 | VERIFIED | `DatumConfig.cs` L122: `= 0.0 //260528 hbk Phase 36 D-36-05/07/13 //260528 hbk Phase 38 #6` |
| 5 | TwoLineAngleToleranceDeg 가 PropertyGrid 에서 숨겨진다 | VERIFIED | `DatumConfig.cs` L696: `if (name == "TwoLineAngleToleranceDeg") return true; //260528 hbk Phase 38 #6 D-12` — switch 진입 전, 모든 case 선행 |
| 6 | ReuseFromShotName 이 완전 제거되어 코드 사용처가 0이다 | VERIFIED | `grep "ReuseFromShotName" WPF_Example/` 결과 0 매치 (전체 디렉터리 검색) |
| 7 | SourceShotName 은 유지된다 | VERIFIED | `DatumConfig.cs` L36: 정의 잔존; `InspectionListView.xaml.cs` L702-703: 실사용처 잔존 |
| 8 | SystemHandler.Initialize() 에 8단계 [STARTUP] Stopwatch 계측이 부착됐다 | VERIFIED | `SystemHandler.cs` L105-168: Stopwatch.StartNew() + `[STARTUP] Step 1~8` + Total = 9줄, `//260528 hbk Phase 38 #11` 마커 |
| 9 | 시작 지연 원인이 1개 이상 식별·문서화됐다 | VERIFIED | `38-STARTUP-ANALYSIS.md`: LoginManager(808ms) + SequenceHandler(550ms) 2개 식별, carry-over CO-38-02/03/04 분류 |
| 10 | CO-38-01 픽셀분해능 단일소스 UI (런타임 cascade + 항목별 숨김) | HUMAN NEEDED | UAT 5 carry-over — D-10 로딩 정규화는 달성됐으나 runtime/UI 단일소스는 사용자 결정으로 CO-38-01 이관. 수용 여부 인간 판단 필요. |

**Score:** 9/10 truths verified (1 human_needed)

---

### Deferred Items

| # | Item | 결정 근거 |
|---|------|----------|
| #3 CircleTwoHorizontal RectL1/L2 비율 통합 | 38-CONTEXT.md line 104: "의미가 다른 두 축이라 통합하지 않기로 결정. v1.2 에서 재검토 가능." — discuss 단계 명시적 descope. |

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs` | GetTypeNames 10종, Create switch 15종 | VERIFIED | GetTypeNames 반환 10종 확인, Create switch EdgePairDistance 등 5종 case 잔존 확인 |
| `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` | LoadPhase6Format FAI 정규화 블록 | VERIFIED | L309-317 camRes 정규화 블록, CAM 섹션 가드(WR-01 fix) 포함 |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` | AngleTolerance=0.0, TwoLineAngleToleranceDeg hide, ReuseFromShotName 제거 | VERIFIED | 3항목 모두 코드에서 직접 확인 |
| `WPF_Example/SystemHandler.cs` | [STARTUP] 단계별 Stopwatch 계측 | VERIFIED | 9줄 계측 로그 (Step 1~8 + Total), delta/cumulative 모두 부착 |
| `.planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-STARTUP-ANALYSIS.md` | 시작 지연 원인 + 개선/carry-over 분류 | VERIFIED | LoginManager(808ms), SequenceHandler(550ms) 2개 원인, CO-38-02~04 분류 존재 |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `InspectionRecipeManager.LoadPhase6Format` | `fai.PixelResolutionX/Y = shot.PixelResolution` | FAI 정규화 루프 | WIRED | L309-317: `if (loadFile.ContainsSection(camSection))` 가드 후 `camRes = shot.PixelResolution` 분배 |
| `DatumConfig.AngleTolerance (= 0.0)` | `DatumFindingService.cs:739 if (config.AngleTolerance > 0.0)` | sentinel 게이트 | WIRED | AngleTolerance 기본 0.0 → 게이트 False → `AngleValidationStatus = None` (배지 OFF). DatumFindingService 무수정 확인. |
| `DatumConfig.IsHiddenForAlgorithm` | PropertyGrid TwoLineAngleToleranceDeg 숨김 | switch 진입 전 `if (name == ...)` | WIRED | L696: 모든 alg case 보다 선행 위치에 `return true` 확인 |
| `SystemHandler.Initialize()` | `Logging.PrintLog [STARTUP] 단계별 경과시간` | Stopwatch 계측 | WIRED | `Stopwatch.StartNew()` → 각 단계 완료 후 `sw.ElapsedMilliseconds` 로그 9줄 |

---

### Data-Flow Trace (Level 4)

38-01 #5 정규화 데이터 흐름:

| 데이터 변수 | 소스 | 흐름 | Status |
|------------|------|------|--------|
| `fai.PixelResolutionX/Y` | `shot.PixelResolution` (ShotConfig, CameraSlaveParam 상속) | LoadPhase6Format → FAI 루프 완료 후 정규화 블록 → fai.PixelResolutionX/Y | FLOWING (로딩 시) |
| (런타임 UI cascade) | Shot 편집 시 FAI cascade | ApplyCalibrationResult 에서만 발생, 편집 cascade 없음 | STATIC (CO-38-01 carry-over) |

---

### Behavioral Spot-Checks

자동화된 테스트 프레임워크 없음(MSBuild-only 프로젝트). 코드 grep + UAT 문서로 대체.

| Behavior | 검증 방법 | Result | Status |
|----------|----------|--------|--------|
| GetTypeNames 10종 반환 (5종 부재) | `grep -n "EdgePairDistance" MeasurementFactory.cs` (GetTypeNames 블록 범위) | GetTypeNames 블록에 0 매치, Create switch에 1 case | PASS |
| PixelResolution 정규화 — CAM 섹션 가드 | `grep "ContainsSection" InspectionRecipeManager.cs` 주변 | L311: `if (loadFile.ContainsSection(camSection))` 가드 확인 | PASS |
| AngleTolerance 기본 0.0 | `grep "AngleTolerance.*=.*0" DatumConfig.cs` | L122: `= 0.0` 확인 | PASS |
| TwoLineAngleToleranceDeg PropertyGrid 숨김 | `grep "TwoLineAngleToleranceDeg.*return true" DatumConfig.cs` | L696: IsHiddenForAlgorithm 진입 직후 return true 확인 | PASS |
| ReuseFromShotName 완전 제거 | `grep -r "ReuseFromShotName" WPF_Example/` | 0 매치 | PASS |
| SourceShotName 유지 | `grep -r "SourceShotName" WPF_Example/` | DatumConfig.cs L36 + InspectionListView L702-703 확인 | PASS |
| Stopwatch [STARTUP] ≥4 줄 | `grep "STARTUP" SystemHandler.cs` | 9줄 (Step 1~8 + Total) — 요구 ≥4 충족 | PASS |
| msbuild Debug/x64 | 38-UAT.md auto_checks | `msbuild_debug_x64: pass`, `msbuild_new_warnings: 0` | PASS |

---

### Requirements Coverage

공식 requirement ID 없음 (phase_req_ids = null). 7개 scope 항목으로 대체 검증.

| Scope 항목 | Plan | Status | Evidence |
|-----------|------|--------|----------|
| #1 측정타입 정리 | 38-01 Task 1 | SATISFIED | GetTypeNames 10종, Create switch 15종, 커밋 78db678 |
| #3 CircleTwoHorizontal RectL1/L2 | — | DEFERRED (명시 descope) | 38-CONTEXT.md D-07: "의미가 다른 두 축 — 변경 없음" |
| #5 픽셀분해능 카메라별 단일화 | 38-01 Task 2 | PARTIAL (CO-38-01) | D-10 로딩 정규화 달성; 런타임 UI cascade = carry-over (사용자 결정) |
| #6 각도 UI 정리 | 38-02 Task 1 | SATISFIED | AngleTolerance=0.0, TwoLineAngleToleranceDeg hide, 커밋 be7f5ab |
| #10 주석 정리 (DatumConfig 한정) | 38-02 Task 2 | SATISFIED | "dead 주석 없음 — 정리 대상 없음" (SUMMARY 기록, ReuseFromShotName 3줄 삭제 잔재 없음) |
| #11 시작 지연 분석 | 38-03 Task 1 | SATISFIED | Stopwatch 9줄 계측, 38-STARTUP-ANALYSIS.md 원인 2개, 커밋 3c99f66 |
| #12 ReuseFromShotName 제거 | 38-02 Task 2 | SATISFIED | grep 0 매치, SourceShotName 유지, 커밋 67c3ccc |

---

### Anti-Patterns Found

| 파일 | 항목 | Severity | Impact |
|------|------|----------|--------|
| `FAIConfig.cs` L84-87 | `PixelResolutionX/Y` PropertyGrid 에 `[Category("Calibration")]` 로 여전히 노출됨 | Info | CO-38-01 carry-over 범위 — 운영상 측정 오류 유발 안 함 (로딩 시 정규화로 runtime 값은 카메라값으로 덮어쓰기 됨). 단, UI 편집 시 혼동 가능. |
| `DatumConfig.cs` L30 | `ImageSourceMode` 주석에 더 이상 동작하지 않는 "ReuseFromShot" 모드 언급 (IN-03 잔여) | Info | 기능 영향 없음. 향후 주석 정리 시 제거 권장. |

---

### 코드 리뷰 수용 확인 (38-REVIEW.md)

| Warning | 조치 | 확인 |
|---------|------|------|
| WR-01: CAM 섹션 부재 시 PixelResolution 1.0 덮어쓰기 | InspectionRecipeManager.cs L311 `if (loadFile.ContainsSection(camSection))` 가드 추가 | VERIFIED — 코드에서 직접 확인 |
| WR-02: FAIConfig.EdgeMeasureType 기본값 "EdgePairDistance"(제거 타입) | FAIConfig.cs L55 기본값 `"EdgeToLineDistance"` 로 변경 | VERIFIED — FAIConfig.cs L55: `= "EdgeToLineDistance" //260507 hbk Phase 19 QUAL-03 //260528 hbk Phase 38 WR-02` |

---

### Human Verification Required

#### 1. CO-38-01 수용 여부 판정

**Test:** phase 38 UAT 5 에서 CO-38-01(픽셀분해능 단일소스 UI)이 carry-over 로 이관됐다. 이 항목의 현재 상태를 사용자가 수용 가능한 부분 달성으로 인정할지, 아니면 별도 phase 에서 반드시 해결해야 할 gap 으로 볼지 확인이 필요하다.

**Expected:** 다음 중 하나:
- (A) "CO-38-01 은 신규 phase discuss→plan 으로 이관 완료 — phase 38 범위 내에서는 D-10 달성으로 충분히 수용" → phase 38 status = passed
- (B) "CO-38-01 은 phase 38 미완 gap — FAIConfig PropertyGrid PixelResolution 숨김 + Shot→FAI cascade 가 이번 phase 에 포함됐어야 함" → gaps_found

**현재 판단:** 사용자가 2026-05-28 직접 "CO-38-01 신규 phase(discuss→plan) 이관" 결정을 내렸으므로 (A) 수용이 타당하나, 공식 검증 기록상 인간 확인이 필요.

**Why human:** UAT sign-off 는 사용자가 직접 한 결정이나, 검증자(GSD verifier) 입장에서 이 carry-over 가 phase 38 성공기준 #3("픽셀분해능 카메라별 단일화 — 기존 측정값 회귀 0 또는 의도적 보정 문서화")의 완전 충족으로 볼 수 있는지 인간 판단이 필요하다.

---

### Gaps Summary

자동 검증된 gaps_found 없음. 9/10 must-have 검증 통과.

미결 항목 1건은 CO-38-01 carry-over 수용 여부 — 사용자가 이미 결정을 내렸으나 공식 검증 기록에 수용 확인이 필요하다. UAT 5 CARRY-OVER 상태는 38-UAT.md 에 "사용자 결정으로 이관"으로 명시되어 있으므로, 사용자가 (A) 확인을 주면 status = passed 로 갱신 가능하다.

**코드 품질 총평:**
- #1/#12/#6/#11: 코드 변경이 플랜 must_haves 를 정확히 충족. 마커 컨벤션(`//260528 hbk Phase 38 #N`) 일관 부착.
- WR-01/WR-02: 코드 리뷰 지적 사항 모두 수정 완료 (CAM 섹션 가드 + 기본 EdgeMeasureType 정렬).
- 빌드: msbuild Debug/x64 PASS, 신규 warning 0 (기존 CS0618/CS0162/MSB3884 만 유지).
- INI 하위호환: ParamBase reflection 경로에서 unknown 키 무시 동작 코드 인용으로 검증됨.

---

_Verified: 2026-05-28T15:00:00Z_
_Verifier: Claude (gsd-verifier)_
