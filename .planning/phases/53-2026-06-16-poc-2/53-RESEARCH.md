# Phase 53: 픽셀 캘리브레이션 (체커보드) - Research

**Researched:** 2026-06-23
**Domain:** HALCON 24.11 체커보드 코너 검출 + mm/px 산출 + WPF 별도 캘리브 창 + ShotConfig.PixelResolution 일괄 반영
**Confidence:** HIGH (코드 경로/적용 로직 VERIFIED in codebase, HALCON 연산자 CITED MVTec docs)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** 격자 한 칸 실제 크기(mm)는 **사용자 수동 입력**, 직전 입력값을 기본값으로 기억.
- **D-07 (LOCK):** **풀 caltab 캘리브 안 씀.** 일반 흑백 체커보드 코너 검출 → 인접 코너 간격(px) → `knownMm / pixelDist = mm/px` 직접 산출. `set_calibration_data`/caltab 점판 불필요.
- **D-02:** mm/px는 **단일 평균값**(가로·세로 간격 평균) → `PixelResolution` 반영. 텔레센트릭 등방 가정. X·Y 분리값은 **리포트로 참고 표시만**.
- **D-05:** 외곽 왜곡 검증 = **수치 + 임계 경고**. 중앙↔외곽 간격 편차% 표시 + 임계(예: 1%) 초과 시 경고. "추후 undistort 승격" 판정 게이트 — **핵심 기능**.
- **D-08 (LOCK):** 왜곡 보정(undistort) 안 함.
- **D-03:** 산출값을 **활성 시퀀스 전체 shot**에 일괄 반영(기존 "선택 FAI shot 1개"보다 확장).
- **D-06:** **사용자 확인 후 반영.** 산출값+왜곡 리포트 먼저 표시 → **[적용] 버튼**으로 반영. 자동 반영 아님.
- **D-04:** **이미지 로드 + 라이브 촬상 둘 다.** `SIMUL_MODE`에선 이미지 로드만, 실 HW는 라이브 정지→촬상. 별도 Window UX.
- Phase 42 단일소스 계약 유지: 측정 소비 = `ShotConfig.PixelResolution`. FAI `PixelResolutionX/Y`는 INI 호환 보존용.

### Claude's Discretion
- 코너 검출 HALCON 연산자 선택 및 격자 정렬/이상치 절사 방식 (→ 본 research 가 결정)
- 캘리브 창 레이아웃/위젯 세부 배치
- 검출 실패·부분검출 시 가드/에러 메시지

### Deferred Ideas (OUT OF SCOPE)
- **렌즈 왜곡 보정(undistort)** — D-05 임계 초과 실측 시 별도 Phase 승격. `gen_cam_par_area_scan_telecentric_division` 후보. 이번 phase 아님.
- X·Y 분리 적용(이방성 보정) — 현재 단일 평균(D-02). 필요시 후속.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CAL-01 | 체커보드(라이브 정지/촬상 또는 이미지 로드) → 픽셀 해상도(mm/px) 산출 → 측정 PixelResolution 적용. 별도 창 제공. | 코너검출=`saddle_points_sub_pix`(§Standard Stack), 격자정렬/median 통계(§Pattern 2), 중앙↔외곽 편차%(§Pattern 3), 라이브/SIMUL 입력(§Pattern 4), 일괄 반영 wiring(§Pattern 5) |
</phase_requirements>

## Summary

이 phase 는 별도 WPF 창에서 흑백 체커보드 이미지(로드 or 라이브 촬상)를 받아, HALCON 으로 격자 내부 코너를 검출하고, 인접 코너 간격(px)과 사용자 입력 칸 크기(mm)로 mm/px 를 직접 산출한 뒤, 사용자 [적용] 확인 후 활성 시퀀스 전체 shot 의 `ShotConfig.PixelResolution` 에 일괄 반영한다. 풀 카메라 캘리브(caltab)는 명시적으로 배제(D-07/D-08)되고, 텔레센트릭 등방 가정 하에 단일 평균 mm/px 만 적용한다(D-02).

핵심 알고리즘 선택: 체커보드 내부 코너는 **saddle point(안장점)** — 한 방향으로 밝기 최소, 직교 방향으로 최대 — 이므로 HALCON `saddle_points_sub_pix` 가 정확히 이 패턴을 서브픽셀로 검출한다. `points_foerstner`(코너당 2점 반환)나 `edges_sub_pix`+교점(직접 격자 구조화 필요)보다 코드가 단순하고 체커보드 전용이다. 검출된 (Row, Column) 배열을 격자로 정렬 → 행/열별 인접 간격 → median/trim 통계로 mm/px 산출.

D-05 외곽 왜곡 게이트가 단순 부가기능이 아닌 핵심 판정 기준임에 주의: 측정이 FOV 외곽 위주라 텔레센트릭 잔여왜곡 최대 지점과 겹친다. 중앙부 평균 간격 대비 외곽부 평균 간격 편차%를 계산해 임계(1%) 초과 시 경고 라벨을 띄운다.

**Primary recommendation:** `saddle_points_sub_pix(Image, 'facet', Sigma=1.0, Threshold=5.0)` 로 코너 검출 → 격자 정렬 → 행/열 인접간격 median → `knownMm / medianSpacing = mm/px`. 기존 `ApplyCalibrationResult` 로직을 "활성 시퀀스 전체 shot 필터"로 확장하고 [적용] 후 `MainWindow.SaveRecipe()` 호출. 신규 `CalibrationWindow`(WPF Window) + `CheckerboardCalibrationService`(HALCON 래퍼).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| 체커보드 코너 검출 + mm/px 산출 | Halcon Algorithm (`Halcon/Algorithms`) | — | HOperatorSet 래핑 = 기존 VisionAlgorithmService 패턴 자리 |
| 외곽 왜곡 편차% 계산 | Halcon Algorithm (순수 수학 static) | — | HALCON 비의존 순수 통계 → static 메서드 (VisionAlgorithmService 선례) |
| 캘리브 창 UI/입력/리포트 표시 | UI (`UI/Dialog` Window) | — | TeachingWindow/ReviewerWindow 별도 Window 선례 |
| 라이브 촬상 / 이미지 로드 | Device (`DeviceHandler.GrabHalconImage`) | UI | 카메라 추상화는 DeviceHandler, SIMUL 분기는 VirtualCamera |
| PixelResolution 일괄 반영 + 저장 | Sequence/Recipe (`SequenceHandler.RecipeManager.Shots` + `MainWindow.SaveRecipe`) | UI | Shot 단일소스 = RecipeManager.Shots, 저장 = SaveRecipe |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| HALCON (halcondotnet) | 24.11 Progress Steady | `saddle_points_sub_pix` 코너 검출 | 체커보드 내부 코너 = saddle point, 서브픽셀 전용 연산자 [CITED: mvtec.com saddle_points_sub_pix] |
| WPF | .NET Framework 4.8 | 별도 캘리브 Window | TeachingWindow/ReviewerWindow 선례 [VERIFIED: codebase] |

### 코너 검출 연산자 선택 (Discretion 결정)

**선택: `saddle_points_sub_pix`** — HDevelop 시그니처:
```
saddle_points_sub_pix(Image : : Filter, Sigma, Threshold : Row, Column)
```
- **Filter** = `'facet'`(기본, 빠름) 또는 `'gauss'`(정확). 권장 `'facet'`. [CITED: mvtec.com]
- **Sigma** = 가우시안 커널. 권장 1.0 (범위 0.7~3.0). facet+Sigma=0.0 으로 스무딩 생략 가능. [CITED]
- **Threshold** = Hessian eigenvalue 최소 절대값. 기본 5.0 (범위 2.0~8.0). 검출 누락 시 낮추고, 노이즈 과검출 시 높임. [CITED]
- **출력** Row, Column = 서브픽셀 코너 좌표 배열(HTuple). [CITED]

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| saddle_points_sub_pix | `points_foerstner` | 코너당 junction+area 2점 반환 → 격자 1점으로 정리하는 후처리 필요. 체커보드엔 과함. [CITED: mvtec.com points_foerstner] |
| saddle_points_sub_pix | `edges_sub_pix` + 직선 피팅 + 교점 | 격자선 추출/그룹화/교점 계산을 직접 구현 → 코드량·실패점 다수. D-07 직접산출 정신엔 맞으나 불필요하게 복잡. |
| saddle_points_sub_pix | `find_caltab`/`find_marks_and_pose` | caltab 점판(HALCON 전용 마커) 전제 → **D-07 으로 명시 배제.** 일반 흑백 체커보드엔 부적합. |

**HOperatorSet 호출 형태 (C#, 프로젝트 try/catch 패턴):**
```csharp
// 260623 hbk Phase 53: 체커보드 saddle point 코너 검출
HTuple rows, cols;
try
{
    HOperatorSet.SaddlePointsSubPix(image, "facet", 1.0, 5.0, out rows, out cols);
}
catch
{
    error = "코너 검출 실패";
    return false;
}
// rows.Length == cols.Length == 검출 코너 수
```

**Installation:** 추가 패키지 없음 — halcondotnet 24.11 기설치 (`C:\Program Files\MVTec\HALCON-24.11-Progress-Steady\bin\dotnet35\halcondotnet.dll`). [VERIFIED: CLAUDE.md]

## Architecture Patterns

### System Architecture Diagram

```
[CalibrationWindow (WPF Dialog)]
   |
   |-- 입력모드 분기 (D-04)
   |     ├─ [이미지 로드] ──> HImage.ReadImage(path)
   |     └─ [라이브 촬상] ──> DeviceHandler.GrabHalconImage(camParam)  ← SIMUL_MODE 면 비활성/폴백
   |                              (VirtualCamera.GrabHalconImage)
   |
   v
[CheckerboardCalibrationService]  (Halcon/Algorithms)
   |  1. SaddlePointsSubPix(image,'facet',1.0,5.0) → rows,cols
   |  2. 격자 정렬: 코너를 행/열 grid 로 클러스터링 (Pattern 2)
   |  3. 인접 간격(px) 행방향/열방향 수집 → median/trim
   |  4. mmPerPixel = knownMm / medianSpacing  (단일 평균, D-02)
   |  5. 중앙↔외곽 편차% (D-05, Pattern 3)
   |
   v
[CalibrationResult]  { MmPerPixel, MmPerPixelX(리포트), MmPerPixelY(리포트),
                        MeanSpacing, StdDev, CenterOuterDeviationPct, IsDistortionWarn, CornerCount }
   |
   v
[리포트 표시 + 왜곡 경고 라벨]  ──사용자──>  [적용] 버튼 (D-06)
   |
   v
[ApplyToActiveSequence]  (Pattern 5)
   |  recipeManager.Shots.Where(OwnerSequenceName==active) 
   |     → shot.PixelResolution = mmPerPixel
   |     → fai.PixelResolutionX/Y = mmPerPixel  (INI 호환)
   |
   v
[MainWindow.SaveRecipe()]  → 레시피 영속화
```

### Recommended Project Structure
```
WPF_Example/
├── Halcon/Algorithms/
│   └── CheckerboardCalibrationService.cs   # 신규: 코너검출 + mm/px + 왜곡% (HOperatorSet 래퍼 + static 통계)
├── UI/Dialog/
│   ├── CalibrationWindow.xaml              # 신규: 별도 창 (TeachingWindow 패턴)
│   └── CalibrationWindow.xaml.cs           # 신규: 입력모드/검출/리포트/적용 핸들러
```

### Pattern 1: HALCON 래퍼 (try/catch return false)
**What:** 모든 HOperatorSet 호출을 `try { } catch { return false; }` 로 감싸고 `out` 결과 + `bool` 반환. `Try` 접두 메서드.
**When to use:** 모든 신규 Halcon 알고리즘 메서드.
**Example:**
```csharp
// Source: WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs (TryFitLine 패턴)
public bool TryDetectCheckerboardCorners(HImage image, out HTuple rows, out HTuple cols, out string error)
{
    rows = cols = null; error = null;
    if (image == null) { error = "image is null"; return false; }
    try
    {
        HOperatorSet.SaddlePointsSubPix(image, "facet", 1.0, 5.0, out rows, out cols);
    }
    catch { error = "saddle point 검출 실패"; return false; }
    if (rows == null || rows.Length < MinCornerCount) { error = "코너 부족"; return false; }
    return true;
}
```

### Pattern 2: 격자 정렬 + 이상치 절사 (Discretion 결정)
**What:** 검출 코너를 행/열 그리드로 묶고, 같은 행 내 인접 코너의 column 차(가로 간격), 같은 열 내 인접 코너의 row 차(세로 간격)를 수집해 robust 통계.
**알고리즘 (telecentric → 격자가 거의 축정렬 가정):**
1. 모든 코너 (row, col) 수집.
2. **행 그룹화:** row 값을 정렬 → 인접 row 차가 (대략적 피치 추정값 × 0.5) 미만이면 같은 행. (또는 row 를 격자 피치로 나눠 round 한 행 인덱스로 버킷팅.)
3. 각 행 내부에서 col 정렬 → **인접 col 차 = 가로 픽셀 간격** 수집.
4. 동일하게 col 그룹화 → 각 열 내부 row 정렬 → **인접 row 차 = 세로 픽셀 간격** 수집.
5. **이상치 절사:** 간격 리스트를 정렬 후 **median** 사용 (또는 상하위 N% 절사 후 mean — 기존 프로젝트 `SortAndTrimPercent` 패턴 재사용 가능, 메모리 `project_phase57_1_seed` 참조). 누락 코너로 인한 2칸 점프(=2×피치)는 median 이 자연 배제.
6. `medianGapX`, `medianGapY` 산출.

**산출 (D-02):**
```csharp
double mmPerPixelX = knownMm / medianGapX;   // 리포트용
double mmPerPixelY = knownMm / medianGapY;   // 리포트용
double mmPerPixel  = knownMm / ((medianGapX + medianGapY) / 2.0);  // 단일 평균 적용값
```
> **주의:** 2칸 점프 오염을 막으려면 median 또는 "최소 간격 근처 클러스터의 median"을 쓸 것. 단순 mean 은 누락 코너에 취약.

### Pattern 3: 중앙↔외곽 편차% (D-05 왜곡 게이트)
**What:** 텔레센트릭 잔여왜곡은 FOV 외곽에서 격자 간격이 미세하게 달라짐. 중앙부 간격 평균 대비 외곽부 간격 평균의 편차%.
**Concrete formula:**
1. 모든 인접 간격에 대해, 그 간격의 **중점이 이미지 중심에서 떨어진 거리** r 을 계산.
2. r ≤ (이미지 반대각선 × 0.33) → **중앙부 그룹**, r ≥ (× 0.66) → **외곽부 그룹** (경계대는 제외해 대비 선명화).
3. `gapCenterMean = mean(중앙부 간격)`, `gapOuterMean = mean(외곽부 간격)`.
4. **편차% = `abs(gapOuterMean - gapCenterMean) / gapCenterMean × 100`.**
5. `IsDistortionWarn = (편차% > DistortionWarnThresholdPct)` — 임계 기본 1.0% (D-05 "예: 1%"). 임계는 const + 향후 조정 가능하게.
**리포트 표시:** "중앙 간격 {c:F2}px / 외곽 간격 {o:F2}px / 편차 {d:F2}% [경고: undistort 검토]"

### Pattern 4: 라이브 / SIMUL 입력 (D-04)
**What:** 이미지 로드는 항상 동작, 라이브 촬상은 실 HW 만.
**라이브 경로:** `DeviceHandler.Handle.GrabHalconImage(camParam)` → `VirtualCamera.GrabHalconImage()` → HImage. [VERIFIED: DeviceHandler.cs:326]
**SIMUL 분기:** `#if SIMUL_MODE` 일 때 [라이브 촬상] 버튼 비활성화 또는 누르면 "SIMUL 모드: 이미지 로드를 사용하세요" 안내. (VirtualCamera 가 SIMUL 에서 background 이미지를 반환하므로 폴백 자체는 동작하지만, D-04 가 SIMUL=이미지 로드만 명시.)
```csharp
#if SIMUL_MODE
    btn_liveCapture.IsEnabled = false;
    btn_liveCapture.ToolTip = "SIMUL 모드에서는 이미지 로드만 가능합니다.";
#endif
```
> **주의:** "라이브 정지→촬상" = 현재 라이브 grab loop 를 멈추고 단일 프레임을 freeze 해 검출. 기존 라이브 표시가 MainView 의 grab 주기로 돌아가므로, 캘리브 창은 **단일 GrabHalconImage 1콜로 정지 프레임**을 얻어 자체 HalconViewer 에 표시하면 충분(별도 freeze 상태머신 불필요).

### Pattern 5: 활성 시퀀스 전체 shot 일괄 반영 (D-03) + 저장
**What:** 기존 `ApplyCalibrationResult`(선택 FAI 1개)를 활성 시퀀스 전체 shot 으로 확장.
**Shots 단일소스:** `SystemHandler.Handle.Sequences.RecipeManager.Shots` (List<ShotConfig>). [VERIFIED: SequenceHandler.cs:116, MainView.xaml.cs:2013]
**활성 시퀀스 필터:** `shot.OwnerSequenceName` (빈값 → TOP 폴백, 기존 정책). [VERIFIED: InspectionListView.xaml.cs:432]
```csharp
// 260623 hbk Phase 53: 활성 시퀀스 전체 shot 일괄 PixelResolution 반영 (D-03)
var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;
string activeSeq = ResolveActiveSequenceName();  // TOP/SIDE/BOTTOM
foreach (ShotConfig shot in recipeManager.Shots)
{
    string owner = string.IsNullOrEmpty(shot.OwnerSequenceName) ? "TOP" : shot.OwnerSequenceName;
    if (owner != activeSeq) continue;
    shot.PixelResolution = mmPerPixel;                      // Phase 42 단일소스
    foreach (FAIConfig fai in shot.FAIList)
    {
        fai.PixelResolutionX = mmPerPixel;                 // INI 호환 보존
        fai.PixelResolutionY = mmPerPixel;
    }
}
```
**저장:** [적용] 후 `MainWindow.SaveRecipe()` 호출로 영속화. [VERIFIED: MainWindow.xaml.cs:263 → SequenceHandler.SaveRecipe → SaveNewFormat]
> **주의 (메모리 `project_recipe_datum_loss_camerarole`):** CameraRole 전환 후 저장 시 비활성 시퀀스 데이터 소실 버그 이력. `SaveNewFormat(saveFile, existingFile)` 의 existingFile 보존 경로가 이미 적용됨(3faa91b). 캘리브 적용 후 SaveRecipe 가 이 경로를 타는지 확인할 것.

### Anti-Patterns to Avoid
- **인접 간격 mean 직접 사용:** 누락/오검출 코너의 2×피치 점프가 평균을 오염. → **median/trim 필수.**
- **caltab/set_calibration_data 도입:** D-07 명시 배제. 검토조차 금지.
- **자동 PixelResolution 덮어쓰기:** D-06 위반. 반드시 [적용] 버튼 게이트.
- **undistort/이미지 와핑 구현:** D-08 배제. 이번 phase 는 검증(편차%)까지만.
- **선택 FAI 1개에만 반영:** 기존 `ApplyCalibrationResult` 그대로 쓰면 D-03 위반.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| 체커보드 코너 서브픽셀 검출 | Harris/직접 saddle 수식 | `HOperatorSet.SaddlePointsSubPix` | 체커보드 코너=saddle, HALCON 전용 서브픽셀 연산자 |
| 이미지↔HImage 로드 | 픽셀 버퍼 수동 | `HImage.ReadImage` / `DeviceHandler.GrabHalconImage` | 기존 경로, Gray8 정규화 포함 |
| 레시피 영속화 | 직접 INI 쓰기 | `MainWindow.SaveRecipe()` | existingFile 보존(비활성 시퀀스 소실 가드) 포함 |
| 절사 통계 | 새 정렬/절사 | 기존 `VisionAlgorithmService.SortAndTrimPercent` (메모리 phase57.1) | 프로젝트 공유 절사 로직 재사용 |

**Key insight:** 코너 검출은 단일 HALCON 콜로 끝나고, 나머지는 순수 C# 그리드 정렬/통계. 복잡도는 알고리즘이 아니라 "누락 코너에 강건한 격자 간격 추정"에 있다 → median.

## Runtime State Inventory

> Greenfield 기능 추가(신규 창 + 신규 서비스). 기존 런타임 상태 변경은 PixelResolution 쓰기 1건.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | `ShotConfig.PixelResolution` (레시피 INI) — 캘리브 적용 시 덮어씀. FAI `PixelResolutionX/Y` INI 키 동반 갱신. | 적용 후 SaveRecipe — 코드 edit (덮어쓰기는 D-06 사용자 확인 게이트) |
| Live service config | None — 외부 서비스 무관. | None |
| OS-registered state | None — verified (UI 기능). | None |
| Secrets/env vars | None. | None |
| Build artifacts | None — 신규 .cs/.xaml 추가만, csproj 에 Compile/Page 항목 추가 필요. | csproj 에 신규 파일 등록 |

## Common Pitfalls

### Pitfall 1: 누락 코너로 인한 간격 오염
**What goes wrong:** 조명 불균일/저대비로 일부 코너 미검출 → 인접 간격에 2×피치 점프 → mean 사용 시 mm/px 오류.
**Why it happens:** saddle Threshold 너무 높거나 격자 일부 흐림.
**How to avoid:** median/trim 사용 + 최소 코너 수 가드 + Threshold 노출(낮춰 재검출). 검출 코너 수를 리포트에 표시.
**Warning signs:** StdDev/평균 비율이 큼, 외곽 편차%가 비현실적으로 큼.

### Pitfall 2: 격자가 축정렬 안 됨 (회전)
**What goes wrong:** 체커보드가 기울어 촬영되면 단순 row/col 버킷팅 실패.
**Why it happens:** 사용자가 보드를 비스듬히 놓음.
**How to avoid:** 텔레센트릭+POC 셋업은 보드 거의 정렬 가정(D-02 등방). 경미한 회전은 버킷 허용오차로 흡수. 큰 회전은 "보드를 수평으로 놓으세요" 가드 메시지 + 검출 실패 처리. (회전 보정은 scope 밖.)
**Warning signs:** 행/열 그룹화 결과 코너 수 불균일.

### Pitfall 3: SIMUL 에서 라이브 촬상 오해
**What goes wrong:** SIMUL 빌드에서 라이브 버튼 누름 → background 이미지 반환되나 D-04 의도와 불일치.
**How to avoid:** `#if SIMUL_MODE` 로 라이브 버튼 비활성 + 안내.
**Warning signs:** SIMUL UAT 에서 라이브 경로로 검출 시도.

### Pitfall 4: 적용 후 저장 누락 / 비활성 시퀀스 소실
**What goes wrong:** PixelResolution 메모리에만 반영되고 SaveRecipe 안 함 → 재시작 시 손실. 또는 SaveRecipe 가 비활성 시퀀스 Datum 소실(이력 버그).
**How to avoid:** [적용] 핸들러에서 반드시 `SaveRecipe()` 호출 + existingFile 보존 경로 확인(메모리 `project_recipe_datum_loss_camerarole`, 수정 3faa91b 적용됨).
**Warning signs:** 재시작 후 측정값 변화 없음 / 다른 시퀀스 Datum 0.

## Code Examples

### 코너 검출 → 간격 median → mm/px (서비스 골격)
```csharp
// Source: 신규 CheckerboardCalibrationService — VisionAlgorithmService try/catch 패턴 차용
public bool TryCalibrate(HImage image, double knownMmPerCell, out CalibrationResult result, out string error)
{
    result = null; error = null;
    HTuple rows, cols;
    try { HOperatorSet.SaddlePointsSubPix(image, "facet", 1.0, 5.0, out rows, out cols); }
    catch { error = "코너 검출 실패"; return false; }
    if (rows == null || rows.Length < MinCornerCount) { error = "코너 부족 (조명/대비 확인)"; return false; }

    // 격자 정렬 → 행/열 인접 간격 수집 (Pattern 2)
    List<double> gapsX = CollectRowAdjacentColGaps(rows, cols);
    List<double> gapsY = CollectColAdjacentRowGaps(rows, cols);
    double medGapX = Median(gapsX);
    double medGapY = Median(gapsY);
    double medGap  = (medGapX + medGapY) / 2.0;
    if (medGap <= 0) { error = "유효 간격 없음"; return false; }

    // 중앙↔외곽 편차% (Pattern 3)
    double devPct = ComputeCenterOuterDeviationPct(rows, cols, image);

    result = new CalibrationResult {
        MmPerPixel  = knownMmPerCell / medGap,
        MmPerPixelX = knownMmPerCell / medGapX,   // 리포트
        MmPerPixelY = knownMmPerCell / medGapY,   // 리포트
        MeanSpacingPx = medGap,
        CenterOuterDeviationPct = devPct,
        IsDistortionWarn = devPct > DistortionWarnThresholdPct,
        CornerCount = rows.Length
    };
    return true;
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| 2점 수동 캘리브 (`FinishCalibration`) | 체커보드 다코너 통계 캘리브 | Phase 53 | 다수 코너 median 으로 단일 2점보다 정밀, 외곽 왜곡 검증 가능 |
| 선택 FAI 1개 반영 | 활성 시퀀스 전체 shot (D-03) | Phase 53 | 동일 카메라/렌즈 전제 일관성 |

**Deprecated/outdated:** caltab 기반 풀 캘리브 — 텔레센트릭+직접산출 정신상 배제(D-07), 본 phase 미사용.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `saddle_points_sub_pix` 가 HALCON 24.11 에 존재 (docs 는 21.11 확인) | Standard Stack | LOW — v12부터 안정 연산자, 제거 사례 없음. 빌드 시 즉시 확인됨 |
| A2 | Sigma=1.0 / Threshold=5.0 이 POC 체커보드에 적정 | Standard Stack | MEDIUM — 실측 이미지 부족. UAT 에서 튜닝 필요 → 파라미터 노출 권장 |
| A3 | 왜곡 임계 1.0% 가 적정 게이트 | Pattern 3 | MEDIUM — D-05 "예: 1%". 실측 후 조정. const 로 노출 |
| A4 | 활성 시퀀스 식별이 OwnerSequenceName 필터로 충분 | Pattern 5 | LOW — 기존 RebuildInspectionActions 동일 패턴 |
| A5 | 캘리브 적용 후 SaveRecipe 가 existingFile 보존 경로를 탐 | Pattern 5 | MEDIUM — 비활성 시퀀스 소실 이력. plan 단계서 SaveRecipe 호출 경로 확인 필요 |

## Open Questions

1. **활성 시퀀스 식별 방법**
   - What we know: Shots 는 OwnerSequenceName 으로 시퀀스 귀속. 트리 선택 시퀀스 존재.
   - What's unclear: 캘리브 창에서 "활성 시퀀스"를 어떻게 받을지 (MainView 선택 노드? 명시 콤보박스?).
   - Recommendation: 캘리브 창 진입 시 현재 선택 시퀀스를 인자로 받거나, 창 내 시퀀스 선택 콤보 제공. planner 가 UX 확정.

2. **saddle 파라미터 / 왜곡 임계 노출 범위**
   - What we know: 실측 이미지 없음, UAT 튜닝 예상.
   - Recommendation: Sigma/Threshold/임계%를 const 로 두되, 검출 실패 시 조정 가능한 입력 필드 1~2개 노출 고려.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| halcondotnet | saddle_points_sub_pix | ✓ | 24.11 | — |
| 실 체커보드 이미지 | 정확도/왜곡 UAT | ✗ | — | 인터넷 다운로드 체커보드로 검출 동작 1차 확인 (CONTEXT specifics) |
| 실 카메라 HW | 라이브 촬상 경로 | ✗ (SIMUL) | — | SIMUL=이미지 로드만 (D-04) |

**Missing dependencies with fallback:**
- 실측 체커보드/카메라 → SIMUL 이미지 로드로 검출 파이프라인까지 검증, 정확도 UAT 는 실측 확보 후 (CONTEXT: "구현 먼저").

## Validation Architecture

> 프로젝트에 자동화 테스트 프레임워크 없음 (CLAUDE.md: "No test framework detected"). nyquist_validation config 확인 불가 시 수동 UAT 기준.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | 없음 (xUnit/NUnit/MSTest 미도입) |
| Config file | none |
| Quick run command | 빌드(MSBuild Debug/x64) + SIMUL 실행 수동 검증 |
| Full suite command | N/A |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Command | Exists? |
|--------|----------|-----------|---------|---------|
| CAL-01 | 체커보드 이미지 로드 → 코너 검출 → mm/px 산출 | manual SIMUL UAT | 앱 실행 → 캘리브 창 → 이미지 로드 → 산출값 확인 | ❌ Wave 0 (수동) |
| CAL-01 | 왜곡 편차% 표시 + 임계 경고 | manual | 외곽왜곡 큰 이미지로 경고 라벨 확인 | ❌ 수동 |
| CAL-01 | [적용] → 활성 시퀀스 전체 shot PixelResolution 반영 + 저장 | manual | 적용 후 shot PixelResolution + 재시작 영속 확인 | ❌ 수동 |

### Wave 0 Gaps
- 없음 (자동 테스트 인프라 미사용). SIMUL 수동 UAT 로 검증 — 정확도는 실측 이미지 확보 후.

## Security Domain

> 산업용 오프라인 데스크탑 UI 기능 (네트워크/인증/암호 무관). 입력 검증 외 ASVS 해당 카테고리 없음.

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V5 Input Validation | yes | knownMm 숫자 파싱 검증(`double.TryParse` + >0), 코너 수 가드, null HImage 가드 |
| 기타 (V2/V3/V4/V6) | no | 해당 없음 (로컬 UI, 외부 노출 없음) |

## Project Constraints (from CLAUDE.md)

- **Tech stack 고정:** .NET Framework 4.8 + WPF + HALCON 24.11. C# 7.2 (switch expression/record/nullable ref 금지).
- **주석:** 코드 수정 시 `//YYMMDD hbk` 필수 (메모리 feedback).
- **Brace style:** 신규 Halcon 코드 = Allman (편집 파일 스타일 따름).
- **HALCON 에러처리:** 모든 `HOperatorSet.*` 를 `try { } catch { return false; }` 로.
- **`Try` 접두 + out 결과 패턴.**
- **GSD workflow 경유** (직접 편집 금지).
- **응답 한국어**, 기능 완료 후 검증 단계에서만 쉽게 설명 (메모리 feedback).

## Sources

### Primary (HIGH confidence)
- `saddle_points_sub_pix` [CITED: mvtec.com/doc/halcon saddle_points_sub_pix] — 시그니처/파라미터/출력
- `points_foerstner` [CITED: mvtec.com/doc/halcon points_foerstner] — 대안 비교 (코너당 2점)
- Codebase [VERIFIED]: MainView.xaml.cs(2218~2318 캘리브), SequenceHandler.cs(116/173/244), DeviceHandler.cs(326), VirtualCamera.cs(184~244 SIMUL), InspectionListView.xaml.cs(432 OwnerSequenceName), MainWindow.xaml.cs(263 SaveRecipe), VisionAlgorithmService.cs(try/catch 패턴)

### Secondary (MEDIUM confidence)
- WebSearch: 체커보드 코너=saddle point, saddle_points_sub_pix 가 표준 (MVTec 문서 교차확인)

### Tertiary (LOW confidence)
- saddle Sigma/Threshold/왜곡 임계 구체값 — 실측 UAT 튜닝 필요 (Assumptions A2/A3)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — 연산자 docs CITED, 기존 코드 패턴 VERIFIED
- Architecture: HIGH — 적용/저장/입력 경로 모두 codebase VERIFIED
- Pitfalls: MEDIUM-HIGH — 누락코너/저장소실은 코드/메모리 근거, 파라미터 튜닝은 실측 의존

**Research date:** 2026-06-23
**Valid until:** ~2026-07-23 (HALCON 안정 API, 코드베이스 변동 시 재확인)
