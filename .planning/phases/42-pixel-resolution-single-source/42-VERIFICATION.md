---
phase: 42-pixel-resolution-single-source
verified: 2026-06-15T00:00:00Z
status: human_needed
score: 5/6
overrides_applied: 0
re_verification: null
gaps: []
deferred: []
human_verification:
  - test: "Shot PropertyGrid에서 PixelResolution 편집 후 다음 검사 실행 — FAI 항목별 PropertyGrid에 PixelResolutionX/Y 노출 여부 확인"
    expected: "Shot 노드 선택 시 PropertyGrid에 PixelResolution(단일값) 표시, FAI/EdgePair 노드 선택 시 PixelResolutionX/Y 행이 숨겨짐. Shot 값 편집 → 앱 재시작 없이 다음 SIMUL 검사에서 mm 결과가 갱신됨."
    why_human: "WPF PropertyGrid(PropertyTools.Wpf) 런타임 렌더링 + ICustomTypeDescriptor GetProperties 필터는 정적 grep으로 완전 검증 불가. HWND airspace 우려 없이 실제 PropertyGrid 행 노출 여부는 실행 환경에서 눈으로 확인 필요."
  - test: "SIMUL_MODE 에서 shot.PixelResolution 값을 변경(1.0 → 0.5)한 후 FAI 검사 재실행, 측정 mm 결과가 절반으로 변경되는지 확인"
    expected: "재시작 없이 동일 Shot의 다음 검사 사이클에서 mm 값이 변경된 PixelResolution에 따라 재계산됨."
    why_human: "런타임 동적 재반영은 정적 코드 구조로 논증 가능하나(EStep.Measure가 검사 시점 직접 읽음), 실제 수치 변화는 SIMUL 모드 실행 후 결과 UI에서 사람이 확인해야 함."
---

# Phase 42: 픽셀분해능 런타임 단일소스 검증 보고서

**Phase 목표:** Shot 단일값 편집 시 재시작 없이 전체 FAI 반영 / PropertyGrid 항목별 노출 정리 / 측정 경로 단일 소스
**검증 일시:** 2026-06-15
**상태:** human_needed
**초기 검증:** Yes — 이전 VERIFICATION.md 없음

---

## 목표 달성 여부

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | 측정 경로가 Shot 단일 소스(ShotParam.PixelResolution)에서 픽셀분해능을 읽는다 — 항목별 fai.PixelResolutionX 소비 0 | ✓ VERIFIED | Action_FAIMeasurement.cs:194 `pixRes = ShotParam.PixelResolution`, 270/285 두 호출부 모두 `pixRes` 전달. `fai.PixelResolutionX` 측정 호출부 grep → 0 매치. |
| 2 | EdgePair 측정도 넘어온 pixelResolution 파라미터(=shot 단일소스)를 소비한다 — self.PixelResolutionX/Y 소비 0 | ✓ VERIFIED | EdgePairDistanceMeasurement.cs:72-74 Owner 체인 walk `ownerShot.PixelResolution`으로 resolvedPixelRes 산출, temp FAIConfig:92-93 `PixelResolutionX/Y = resolvedPixelRes`. `PixelResolutionX = PixelResolutionX` self-대입 → 0 매치. FAIEdgeMeasurementService:295 `fai.PixelResolutionX` 소비는 temp 객체(resolvedPixelRes 주입) 경유로 단일소스와 정확히 연결됨. |
| 3 | Shot 단일값을 편집하면 다음 검사부터 별도 cascade 없이 전체 FAI에 반영된다 (구조적) | ✓ VERIFIED (구조적) | EStep.Measure case:194 매 실행 시 `ShotParam.PixelResolution` 직접 읽음. cascade/트리거 미구현이 정상(D-03). 런타임 수치 변화는 human 확인 필요(Truth #5 human_verification). |
| 4 | PropertyGrid에서 항목별(FAI/EdgePair) PixelResolutionX/Y가 노출되지 않는다 | ✓ VERIFIED (정적) | FAIConfig.cs:77,79 `[PropertyTools.DataAnnotations.Browsable(false)]` 각 1회. EdgePairDistanceMeasurement.cs:39,41 동일. `[Category("Calibration")]` / `[Category("EdgePair\|Calibration")]` 제거 확인(grep → 0). 런타임 PropertyGrid 표시는 human 확인 필요. |
| 5 | ShotConfig.PixelResolution은 유일한 편집 소스로 PropertyGrid 노출이 유지된다 (변경 없음) | ✓ VERIFIED | CameraSlaveParam.cs:25 `[Category("General\|AOI")]` 노출 유지, git diff 미포함. MainView.xaml.cs:2200 `shot.PixelResolution = mmPerPixel` 단일소스 기록 확인. |
| 6 | 정규화된 레시피의 측정 mm 값이 Rewire 전후 동일하다 (회귀 0) | ✓ VERIFIED (논증) | InspectionRecipeManager.cs:328-334 로딩 시 `fai2.PixelResolutionX/Y = shot.PixelResolution` 덮어쓰기 정규화 확인. CAM 섹션 존재 레시피는 로딩 후 fai값 == shot값 성립 → Rewire 전후 동일 값 전달. 구형(CAM 미존재) 레시피는 D-01 의도적 보정(문서화됨). |

**점수:** 5/6 truths 정적 완전 검증 (Truth 3/4 런타임 거동은 human 확인 필요)

---

### 필수 Artifacts

| Artifact | 제공 기능 | Status | 세부 사항 |
|----------|-----------|--------|-----------|
| `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` | EStep.Measure가 ShotParam.PixelResolution 1회 캡처 후 두 호출부 전달 | ✓ VERIFIED | pixRes 선언(L194), TryExecute 호출 2곳(L270, L285) 모두 pixRes 사용. |
| `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgePairDistanceMeasurement.cs` | temp FAIConfig 구성 시 resolvedPixelRes 소비 + 항목 필드 Browsable(false) | ✓ VERIFIED | resolvedPixelRes 3 매치(선언+X+Y 대입). Browsable(false) PixelResolutionX/Y 각 1개. |
| `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` | PixelResolutionX/Y에 Browsable(false) 부여, 필드 보존 | ✓ VERIFIED | L77/79 Browsable(false) 존재. 필드 삭제 없음(INI 직렬화 경로 유지). |

---

### Key Link 검증 (Wiring)

| From | To | Via | Status | 세부 사항 |
|------|----|-----|--------|-----------|
| Action_FAIMeasurement EStep.Measure | ShotParam.PixelResolution | `double pixRes = ShotParam.PixelResolution` (L194), TryExecute(image, transform, pixRes, ...) (L270/285) | ✓ WIRED | grep `TryExecute(image, transform, pixRes` → 2 매치. fai.PixelResolutionX 측정 호출부 → 0 매치. |
| EdgePairDistanceMeasurement temp FAIConfig | pixelResolution 파라미터 (= shot 단일소스) | `ownerFai.Owner as ShotConfig` → `resolvedPixelRes` → `PixelResolutionX/Y = resolvedPixelRes` | ✓ WIRED | grep `resolvedPixelRes` → 3 매치. `PixelResolutionX = PixelResolutionX` self-대입 → 0 매치. |
| FAIEdgeMeasurementService:295 | fai.PixelResolutionX (temp FAIConfig) | EdgePairDistanceMeasurement가 temp에 resolvedPixelRes 주입 후 TryMeasure 호출 | ✓ WIRED | 서비스의 `fai.PixelResolutionX` 소비는 temp 객체 경유로 shot 단일소스에 정확히 연결됨. |
| ApplyCalibrationResult | shot.PixelResolution | `shot.PixelResolution = mmPerPixel` (MainView.xaml.cs:2200) | ✓ WIRED | 2점 캘리브레이션 진입점이 shot 단일소스에 기록. |

---

### Data-Flow Trace (Level 4)

| Artifact | 데이터 변수 | 소스 | 실제 데이터 흐름 | Status |
|----------|------------|------|------------------|--------|
| EdgePairDistanceMeasurement.TryExecute | resolvedPixelRes | ownerShot.PixelResolution (shot 단일소스) 또는 pixelResolution 파라미터 fallback | shot.PixelResolution → Action_FAIMeasurement pixRes → TryExecute 파라미터 → ownerShot.PixelResolution (두 경로 동일 값) | ✓ FLOWING |
| Action_FAIMeasurement EStep.Measure | pixRes | ShotParam.PixelResolution | 매 Measure 사이클마다 shot에서 직접 읽음 | ✓ FLOWING |

---

### Behavioral Spot-checks

Step 7b: SKIPPED — WPF 데스크탑 앱, 서버/클라이언트 없음. 런타임 거동은 human verification으로 이관.

---

### Requirements Coverage

| Requirement | 소스 Plan | 설명 | Status | Evidence |
|-------------|-----------|------|--------|----------|
| CO-38-01 | PLAN 42-01 | 픽셀분해능 런타임 단일소스 (현행 다중 경로 통합) | ✓ SATISFIED (구조적) | ROADMAP 성공기준 3종 (측정 경로 단일소스 / PropertyGrid 정리 / Shot 편집 반영) 모두 코드 구조로 충족. REQUIREMENTS.md Phase 42 매핑 일치. |

---

### Anti-Patterns

| 파일 | 라인 | 패턴 | 심각도 | 영향 |
|------|------|------|--------|------|
| Action_FAIMeasurement.cs | 194 | `ShotParam != null ? ShotParam.PixelResolution : 1.0` — false 분기 dead code (`if (ShotParam != null)` 블록 내부) | ℹ️ Info | 동작 정확성 무영향. REVIEW.md IN-01에 기록됨. 다음 파일 수정 시 정리 권장. |
| EdgePairDistanceMeasurement.cs | 40-42 | PixelResolutionY에 인라인 주석 없음 | ℹ️ Info | 가독성. REVIEW.md IN-02에 기록됨. |
| FAIConfig.cs | 221, 265 | `PixelResolutionX = PixelResolutionX` — RoiDefinition 생성 시 FAIConfig 자체 값 복사 | ℹ️ Info | **측정 소비 경로 외부(RoiDefinition = 티칭/오버레이 표시용).** FAIEdgeMeasurementService는 RoiDefinition이 아닌 FAIConfig를 직접 사용. Action_FAIMeasurement EStep.Measure 루프에서 이 경로는 소비되지 않음. 기능 회귀 없음. |

블로커 anti-pattern 없음.

---

### Human Verification 필요 항목

#### 1. PropertyGrid 항목별 PixelResolutionX/Y 숨김 시각 확인

**Test:** SIMUL_MODE 실행 → FAI 노드 선택 → PropertyGrid 스크롤하여 "PixelResolutionX/Y" 행 유무 확인. Shot 노드 선택 → "PixelResolution" 행 유무 확인.
**Expected:** FAI/EdgePair 노드 PropertyGrid에 PixelResolutionX/Y 행 없음. Shot 노드에 PixelResolution 행 존재.
**Why human:** WPF PropertyTools.Wpf PropertyGrid는 ICustomTypeDescriptor(FAIConfig) + [Browsable(false)] 두 레이어를 동시에 적용. 정적 grep으로 어트리뷰트 존재는 확인했으나 실제 렌더링된 PropertyGrid 행 노출 여부는 런타임에서만 확인 가능.

#### 2. Shot 단일값 편집 후 다음 검사 mm 반영 확인

**Test:** SIMUL_MODE 실행 → Shot 노드 PropertyGrid에서 PixelResolution을 1.0 → 0.5로 편집 → 앱 재시작 없이 FAI 검사 실행 → 결과 mm 값 확인.
**Expected:** 재시작 없이 동일 검사에서 mm 결과가 이전 대비 약 절반으로 변경됨. (정규화 레시피 기준)
**Why human:** EStep.Measure가 매 실행 시 ShotParam.PixelResolution을 직접 읽는 구조는 정적으로 검증되었으나, 실제 수치 변화 확인은 SIMUL 모드 실행 후 결과 화면에서 사람이 확인해야 함.

---

### Gaps 요약

코드 구조 수준에서는 모든 must-have가 충족됩니다. 식별된 gap 없음.

- Truth 3(라이브 반영)과 Truth 4(PropertyGrid 숨김)는 정적 구조 완전 검증 완료, 런타임 거동은 human verification으로 이관.
- FAIConfig.cs:221/265의 `PixelResolutionX = PixelResolutionX`는 RoiDefinition(티칭/오버레이 표시용) 생성 경로이며 측정 mm 계산과 무관 — 블로커 아님.
- REVIEW.md Info 2건(dead code guard, 주석 누락)은 비기능적, 다음 수정 시 정리.

---

_Verified: 2026-06-15_
_Verifier: Claude (gsd-verifier)_
