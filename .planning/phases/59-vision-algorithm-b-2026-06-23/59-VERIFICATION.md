---
phase: 59-vision-algorithm-b-2026-06-23
verified: 2026-06-24T00:45:00Z
status: human_needed
score: 4/4 must-haves verified
overrides_applied: 0
human_verification:
  - test: "Teach (Tray): ROI 지정 후 TryTeach 호출 → ETHERNET_ALIGN\\Tray.shm + Tray.json 생성 확인"
    expected: "RecipeSavePath\\RecipeName\\ETHERNET_ALIGN\\Tray.shm 및 Tray.json 이 디스크에 실제 생성"
    why_human: "파일 I/O + HALCON create_shape_model 실행 — SIMUL 이미지(D:\\align_test.bmp)와 실 ROI 파라미터 필요. Phase 61 UI 완성 후 버튼 경로로 검증."
  - test: "Teach (Bottom): TryTeach Bottom 모드 호출 → Bottom.shm + Bottom.json 생성, Tray.shm 미변경"
    expected: "Bottom.shm/Bottom.json 신규 생성, Tray 파일 무변화 (per-mode 격리)"
    why_human: "파일 격리는 런타임 디스크 검사로만 확인 가능."
  - test: "Run (Tray): 티칭 후 동일/유사 이미지 Run 호출 → AlignResult.Found=true, OffsetXmm/OffsetYmm 0에 가까운 값, HasTheta=false"
    expected: "동일 이미지에서 offset ≈ 0mm, Score ≥ 0.5, HasTheta=false"
    why_human: "HALCON find_shape_model 실행 + ref pose JSON 로드 — 앱 실행 + 실제 이미지 필요."
  - test: "Run (Bottom): 기준 이미지 대비 이동/회전된 이미지 Run → OffsetXmm/OffsetYmm 비영, ThetaDeg 비영, HasTheta=true"
    expected: "shift 된 이미지에서 offset mm 값이 물리적 이동량과 일치, ThetaDeg = 각도 차"
    why_human: "부호/축 매핑(Row→Y, Col→X) 실측 확인은 실 장비 기준 UAT 에서만 확정 가능."
  - test: ".shm 저장→재로드→매칭 라운드트립: TryTeach 후 앱 재시작 → HasTemplate true, Run 정상 동작"
    expected: "앱 재기동 후 HasTemplate(mode) = true, Run 결과 Found=true"
    why_human: "csproj/런타임 파일 영속성 검증 — 앱 재시작 필요."
---

# Phase 59: Vision Algorithm (B) Verification Report

**Phase Goal:** Shape Matching(create/find/read/write_shape_model)으로 ROI 티칭→.shm 저장, Tray=X/Y · Bottom=X/Y/Theta Offset 산출.
**Verified:** 2026-06-24T00:45:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths (ROADMAP Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | ROI 지정 후 티칭 → .shm 저장/로드 | ✓ VERIFIED (code) | `TryTeach`: `_matcher.TryCreateModel(...)` → `.shm` 기록; `TrySaveRefPose(...)` → `.json` 사이드카; `HasTemplate`/`LoadRefPose` 로드 경로 구현. `GetShmPath` 는 `PatternMatchService.EXTENSION_SHAPE_MODEL` 상수 사용. |
| 2 | find_shape_model 로 Row/Col/Angle/Score 산출 (Halcon try-catch) | ✓ VERIFIED (code) | `Run`: `_matcher.TryFindPose(...)` → `curRow`, `curCol`, `curAngleDeg`, `curScore` 산출; HALCON 호출은 `PatternMatchService` 내부 try-catch, `Run`/`TryTeach` 자체도 try-catch 래핑. 예외 시 `AlignResult{Found=false}` 반환, throw 없음. |
| 3 | Tray 모드 X/Y Offset, Bottom 모드 X/Y/Theta 산출 | ✓ VERIFIED (code) | `Run`: 항상 `OffsetXmm = dCol * resMm`, `OffsetYmm = dRow * resMm` 설정. Bottom 한정 `ThetaDeg = curAngleDeg - refPose.RefAngleDeg` + `HasTheta = true`. Tray: `ThetaDeg = 0.0`, `HasTheta = false`. |
| 4 | Tray/Bottom 별도 템플릿 관리 | ✓ VERIFIED (code) | `GetShmPath(EEthernetVisionMode mode)`: `Bottom → "Bottom"`, else → `"Tray"` — `ETHERNET_ALIGN\Tray.shm` vs `ETHERNET_ALIGN\Bottom.shm`. 독립 경로 분기 확인. |

**Score:** 4/4 truths verified (code level)

**Status 결정:** 코드 수준 4/4 전부 검증. 단, SC-1~SC-4 모두 런타임(HALCON 실행 + 실 이미지 + Phase 61 UI)에서의 실제 동작 확인이 필요 → `human_needed`.

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `WPF_Example/Custom/EthernetVision/AlignResult.cs` | Align 결과 DTO (Found/Score/OffsetXmm/OffsetYmm/ThetaDeg/HasTheta) | ✓ VERIFIED | 6개 public auto-property 전부 존재. namespace ReringProject. K&R. `//260624 hbk Phase 59`. C# 7.2. |
| `WPF_Example/Custom/EthernetVision/AlignRefPose.cs` | 레퍼런스 포즈 사이드카 JSON 스키마 (RefRow/RefCol/RefAngleDeg/AngleExtentDeg/Engine) | ✓ VERIFIED | 5개 public auto-property 전부 존재. namespace ReringProject. K&R. `//260624 hbk Phase 59`. |
| `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs` | Shape-matching 오케스트레이션 (TryTeach/Run/HasTemplate/TryLoadTemplate) | ✓ VERIFIED | `public AlignResult Run(`, `public bool TryTeach(`, `HasTemplate`, `TryLoadTemplate` 전부 구현. `_matcher = new PatternMatchService()` 합성. `ETHERNET_ALIGN` 상수. `TypeNameHandling.None`. |
| `WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs` | Matcher 프로퍼티 + Initialize() 모든 경로 non-null 보장 | ✓ VERIFIED | `public AlignShapeMatchService Matcher { get; private set; }` 선언. `try` 최상단 `Matcher = new AlignShapeMatchService()` + `catch` 내 null-guard 2번 생성. |
| `WPF_Example/DatumMeasurement.csproj` | AlignResult.cs/AlignRefPose.cs/AlignShapeMatchService.cs 컴파일 등록 | ✓ VERIFIED | csproj 239~243행: `AlignRefPose.cs`, `AlignResult.cs`, `AlignShapeMatchService.cs` 3개 `<Compile Include>` 확인. |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `AlignShapeMatchService.TryTeach` | `PatternMatchService.TryCreateModel` | `_matcher.TryCreateModel(img, roiRow, roiCol, roiPhi, roiLen1, roiLen2, ENGINE, angleExtentDeg, shmPath, out createErr)` | ✓ WIRED | AlignShapeMatchService.cs L168 — 시그니처 일치 |
| `AlignShapeMatchService.TryTeach` | `PatternMatchService.TryFindRefPose` | `_matcher.TryFindRefPose(img, ENGINE, shmPath, MIN_SCORE, out refRow, ...)` | ✓ WIRED | AlignShapeMatchService.cs L179 |
| `AlignShapeMatchService.Run` | `PatternMatchService.TryFindPose` | `_matcher.TryFindPose(img, ENGINE, shmPath, 0.0, 0.0, FULL_SEARCH_LEN, FULL_SEARCH_LEN, 0.0, MIN_SCORE, 1.0, out curRow, ...)` | ✓ WIRED | AlignShapeMatchService.cs L230 — 전체 이미지 검색(margin=0, downsample=1) |
| `AlignShapeMatchService.Run` | `AlignResult` (Plan 01) | `new AlignResult()` 생성 후 필드 할당 | ✓ WIRED | AlignShapeMatchService.cs L258~270 |
| `AlignShapeMatchService` | `{RecipeSavePath}\{recipe}\ETHERNET_ALIGN\{Tray\|Bottom}.shm` | `GetShmPath(mode)` — `Path.Combine + PatternMatchService.EXTENSION_SHAPE_MODEL` | ✓ WIRED | AlignShapeMatchService.cs L60~72 |
| `EthernetVisionHandler.Initialize()` | `AlignShapeMatchService` | `Matcher = new AlignShapeMatchService()` (try 최상단 + catch null-guard) | ✓ WIRED | EthernetVisionHandler.cs L34, L58~60 |
| `EthernetVisionHandler.Matcher` | Phase 61 UI / Phase 62 TCP | `public AlignShapeMatchService Matcher { get; private set; }` | ✓ WIRED | EthernetVisionHandler.cs L21 — getter 공개 |

---

### Data-Flow Trace (Level 4)

AlignShapeMatchService 는 동적 데이터(HALCON 이미지)를 소비하되, 자체 렌더링 없음 — 순수 알고리즘 서비스. 데이터 흐름 경로:

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|--------------|--------|--------------------|--------|
| `AlignShapeMatchService.TryTeach` | `img` (HImage) | 호출자(Phase 61 Camera.Grab()) | 런타임 검증 필요 | ? RUNTIME — Phase 61 UI 없이 확인 불가 |
| `AlignShapeMatchService.Run` | `curRow/curCol/curAngleDeg/curScore` | `_matcher.TryFindPose` → HALCON find_shape_model | 런타임 검증 필요 | ? RUNTIME |
| `AlignShapeMatchService.Run` → `AlignResult` | `OffsetXmm`, `OffsetYmm`, `ThetaDeg` | `dRow/dCol × resMm`, `curAngleDeg - refPose.RefAngleDeg` | 수식 코드 확인 ✓ | ✓ FORMULA-VERIFIED (실측은 human) |
| `EthernetPixelResolution` | `resMm = EthernetPixelResolution / UM_PER_MM` | `SystemSetting.Handle.EthernetPixelResolution` (INI, Phase 58 AV-01) | AV-01 기반 — Phase 58 코드에서 검증됨 | ✓ WIRED |

---

### Behavioral Spot-Checks

Step 7b: SKIPPED — AlignShapeMatchService 는 HALCON 실행 + 실 이미지 + EthernetVisionHandler.Initialize() 호출 없이는 런타임 동작 확인 불가. Phase 61 UI 미완성. 빌드(msbuild Debug/x64 exit 0) 는 Plan 03 Task 2에서 확인됨.

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|---------|
| AV-03 | 59-01, 59-02, 59-03 | Shape Model 티칭/매칭: ROI 지정 → create_shape_model → .shm 저장/로드, find_shape_model Row/Col/Angle/Score 산출 (Halcon try-catch) | ✓ SATISFIED (code) | `TryTeach` → `TryCreateModel` + `TryFindRefPose` + `TrySaveRefPose`; `Run` → `TryFindPose` → `AlignResult{Found, Score, ...}`. 모든 경로 try-catch 격리. |
| AV-04 | 59-01, 59-02, 59-03 | Tray = X/Y Offset, Bottom = X/Y+Theta, 각 모드 별도 템플릿 | ✓ SATISFIED (code) | `GetShmPath` Tray.shm/Bottom.shm 분기; `Run` Tray→HasTheta=false/ThetaDeg=0, Bottom→HasTheta=true/ThetaDeg=curAngleDeg-refAngleDeg. |

---

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| (없음) | — | — | — | — |

금지 파일 수정 없음 (`git diff dcf5f6c HEAD --name-only` grep → ANTIGOAL_PASS). EthernetVisionHandler.cs 추가 전용 확인 (삭제 라인 0). `PatternMatchService.cs`, `RecipeFileHelper.cs`, Grabber 파일 전부 무수정.

---

### Human Verification Required

다음 항목은 코드 검사로 확인 불가 — Phase 61 UI 완성 후 앱 실행 상태에서 수행.

#### 1. Teach 라운드트립 (Tray)

**Test:** Phase 61 Tray 탭 → ROI 드로잉 → [Teach] 버튼 클릭
**Expected:** `ETHERNET_ALIGN\Tray.shm` + `Tray.json` 파일 생성, 로그 `[ALIGN_SVC] teach OK (Tray): ...` 출력
**Why human:** HALCON `create_shape_model` + `write_shape_model` 실행 + 파일 I/O — SIMUL 이미지(D:\align_test.bmp)와 앱 실행 필요

#### 2. Teach 라운드트립 (Bottom)

**Test:** Phase 61 Bottom 탭 → ROI 드로잉 → [Teach] 버튼 클릭
**Expected:** `ETHERNET_ALIGN\Bottom.shm` + `Bottom.json` 생성, Tray 파일 무변화
**Why human:** Tray vs Bottom 템플릿 격리는 실 디스크 검사로만 확인 가능

#### 3. Run → OffsetX/Y 정상값 (Tray, 동일 이미지)

**Test:** Teach 직후 동일 이미지로 Run 호출
**Expected:** `AlignResult.Found=true`, `OffsetXmm ≈ 0`, `OffsetYmm ≈ 0`, `Score ≥ 0.5`, `HasTheta=false`
**Why human:** HALCON `find_shape_model` 실행 + ref pose JSON 로드 결과 확인 — 앱 실행 필요

#### 4. Run → OffsetX/Y/Theta 정상값 (Bottom, 이동/회전 이미지)

**Test:** Bottom Teach 후 물리적으로 이동/회전된 이미지(또는 SIMUL 대체) 로 Run 호출
**Expected:** `OffsetXmm` / `OffsetYmm` 이동량과 일치, `ThetaDeg` 회전각 차이, `HasTheta=true`
**Why human:** 부호/축 매핑(Col→X, Row→Y) 및 Theta 방향은 실 장비 UAT 에서 확정

#### 5. 앱 재시작 후 .shm 로드

**Test:** Teach 완료 → 앱 종료 → 재시작 → `HasTemplate(Tray)` + `HasTemplate(Bottom)` 확인
**Expected:** 재시작 후 두 모드 모두 `HasTemplate = true`
**Why human:** 파일 영속성 + INI `RecipeSavePath` / `CurrentRecipeName` 설정 일관성 확인

---

### Gaps Summary

코드 수준 갭 없음. 4개 ROADMAP 성공 기준 모두 소스에서 구현 확인.

런타임 확인은 Phase 61 UI 완성 후 Human UAT 로 진행 예정 (Phase 58 동일 패턴 — CONTEXT D-02: "UAT 는 Phase 61 UI 완성 후 일괄").

---

*Verified: 2026-06-24T00:45:00Z*
*Verifier: Claude (gsd-verifier)*
