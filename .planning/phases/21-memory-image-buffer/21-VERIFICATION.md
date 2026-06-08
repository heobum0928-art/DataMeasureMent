---
phase: 21-memory-image-buffer
verification_date: 2026-05-10
status: verified  # AC#1 / AC#3 / AC#4 자동 audit PASS. AC#2 는 21-UAT.md Test 2 에 위임.
verified_by: gsd-checker (automated grep + msbuild Debug/x64 rebuild)
ac_status:
  AC1: verified           # disk-free 표시 경로 — DisplayShotImage/DisplayFAIImage 범위 forbidden API 0 hits
  AC2: deferred_to_uat    # dispose 입증 — 21-UAT.md Test 2 사용자 시나리오 (recipe load × 5 ClearShots 로그 카운트)
  AC3: verified           # XML doc 6 블록 + 7 마커 grep PASS
  AC4: verified           # msbuild Debug/x64 rebuild PASS, 신규 warning 0 (3 pre-existing baseline preserved × 2 build pass)
---

# Phase 21 Verification Report

**Date:** 2026-05-10
**Method:** automated grep + msbuild rebuild (D-08 Option A + D-09 + D-10 build half)

---

## AC#1 — 디스크 비접근 표시 경로 (D-08 Option A: grep audit)

**Method:** D-08 Option A (코드 grep audit). Option B (SIMUL UAT 시각 확인) 는 21-UAT.md Test 1 에서 사용자 검증 (D-08 Option C 합산).

**Search scope:** `WPF_Example/UI/ContentItem/MainView.xaml.cs` 메서드 `DisplayFAIImage` (L101-L110) / `DisplayShotImage` (L113-L132).

**Forbidden API patterns (CONTEXT.md D-08 + PATTERNS.md §"AC#1 grep audit scope"):**

| # | Forbidden pattern | grep 명령 (rg 등가) | 결과 (file-wide) | AC#1 scope (L101-L132) |
|---|-------------------|----------------------|------------------|-------------------------|
| 1 | `HImage.ReadImage` | `rg -n "HImage\.ReadImage" MainView.xaml.cs` | **0 hits** | 0 hits ✅ |
| 2 | `new HImage(` (path 생성자) | `rg -n "new HImage\(" MainView.xaml.cs` | **0 hits** | 0 hits ✅ |
| 3 | `BitmapImage` (UriSource / new BitmapImage(new Uri)) | `rg -n "BitmapImage" MainView.xaml.cs` | **0 hits** | 0 hits ✅ |
| 4 | `FileStream` | `rg -n "FileStream" MainView.xaml.cs` | **0 hits** | 0 hits ✅ |
| 5 | `File.Open` / `File.OpenRead` / `File.ReadAllBytes` | `rg -n "File\.Open\|File\.ReadAllBytes\|File\.OpenRead" MainView.xaml.cs` | **0 hits** | 0 hits ✅ |
| 6 | `File.` (System.IO.File 일반) | `rg -n "File\." MainView.xaml.cs` | 1 hit @ **L464** (`File.Exists` in legacy disk-fallback path of a different method) | **0 hits in scope ✅** |

**Out-of-scope hit detail (L464 — AC#1 scope 외):**

```csharp
// MainView.xaml.cs L464 — different method (UpdateFromContext or similar legacy disk-fallback)
if (!string.IsNullOrWhiteSpace(context.ResultImagePath) && File.Exists(context.ResultImagePath)) {
    ...
    halconViewer.LoadImage(context.ResultImagePath);   // ← legacy disk-fallback path (Phase 25 OUT-01 territory)
```

이 경로는 Phase 21 의 BUF-02 lifetime 계약 대상이 아닌 별도 sequence-context-result 표시 경로 (Phase 25 OUT-01 결과 이미지 리뷰어와 정합). PATTERNS.md §"AC#1 grep audit scope" 의 명시 규정대로 **AC#1 scope 는 `DisplayShotImage` / `DisplayFAIImage` 메서드 본문 한정** — 0 hits 가 확정.

**확인된 disk-free 경로 (MainView.xaml.cs L113-L132 인용):**

```csharp
/// <summary>Displays the image stored in the given ShotConfig on the canvas.</summary>
private void DisplayShotImage(ShotConfig shot) {
    if (shot != null && shot.HasImage) {
        HImage img = null;
        try {
            img = shot.GetImage();                    // ← 메모리 clone (Plan 01 XML doc: caller-disposes)
            if (img != null) {
                halconViewer.LoadImage(img);          // ← HImage 인스턴스 직접 로드 (디스크 I/O 0)
                label_message.Visibility = Visibility.Collapsed;
            } else {
                label_message.Content = "이미지 로드 실패";
                label_message.Visibility = Visibility.Visible;
            }
        } finally {
            if (img != null) img.Dispose();           // ← Phase 20 D-02 expanded ?., 호출자 dispose 책임 이행
        }
    } else {
        label_message.Content = "NO Image";
        label_message.Visibility = Visibility.Visible;
    }
}
```

**`DisplayFAIImage` (L101-L110)** 는 단순히 `fai.Owner as ShotConfig` 캐스팅 후 `DisplayShotImage(shot)` 위임 — 자체 디스크 호출 0.

**결론:** AC#1 충족 — 결과 이미지 표시 경로의 6/6 forbidden API 패턴 0 hits, scope 한정 audit 통과. ✅ **verified.**

---

## AC#2 — Dispose 입증 (UAT 위임)

**Method:** D-11 ① logging 카운트. 21-UAT.md Test 2 (recipe load × 5 사용자 시나리오) 에서 사용자가 `[InspectionRecipeManager] ClearShots disposed {N} shot buffers` 로그 라인을 카운트하여 검증.

**보조 코드 입증 (Plan 02 Task 3 instrumentation):**

```bash
rg -c "\[InspectionRecipeManager\] ClearShots disposed" WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs
# 결과: 1 hit
```

| Check | 파일 | 결과 |
|-------|------|------|
| Logging instrumentation 존재 | InspectionRecipeManager.cs L64 | 1 hit ✅ |

**InspectionRecipeManager.ClearShots() L62-69 인용 (Plan 02 Task 3 instrumentation 적용):**

```csharp
public void ClearShots() {
    //260510 hbk Phase 21: BUF-02 dispose 입증 instrumentation — UAT 가 recipe load × N 회 후 이 로그 라인 카운트로 dispose 검증
    Logging.PrintLog((int)ELogType.Trace, "[InspectionRecipeManager] ClearShots disposed {0} shot buffers", Shots.Count);
    foreach (var shot in Shots) {
        shot.ClearImage();
    }
    Shots.Clear();
}
```

`ClearShots()` 호출 시점 (D-02 3 채널) 에 매번 1 라인 출력 — UAT Test 2 가 사용자 recipe load × 5 시나리오에서 실측 카운트로 dispose 발생 입증.

**결론:** AC#2 코드측 instrumentation 준비 완료. **deferred_to_uat (Test 2)**.

---

## AC#3 — 수명 보장 시점 문서화 (D-09: XML doc + 마커 grep)

**Method:** Plan 01 의 XML doc 6 블록 + 마커 자동 카운트.

### XML doc 블록 6개 (실측 위치)

| # | 파일 | 멤버 | 위치 | 핵심 키워드 (Plan 01 SUMMARY 인용) |
|---|------|------|------|--------------------------------------|
| 1 | ShotConfig.cs | `HasImage` | L48-51 | "_imageLock 으로 동기화", "임의 thread 에서 안전" |
| 2 | ShotConfig.cs | `SetImage(HImage)` | L65-70 | "clone-on-input", "기존 _image 자동 Dispose", "호출자는 입력 image 의 소유권 보유" |
| 3 | ShotConfig.cs | `GetImage()` | L79-87 | "**호출자가 반환된 HImage 의 Dispose 책임을 진다**", `using` + try/finally 양쪽 패턴 인용 |
| 4 | ShotConfig.cs | `ClearImage()` | L95-105 | "(1) 레시피 변경 — Custom/SystemHandler.cs OnRecipeChanged subscriber", "(2) 시퀀스 리셋 — Action_FAIMeasurement.cs EStep.Init", "(3) 앱 종료 — SystemHandler.Release()", "멱등 (idempotent)" |
| 5 | ShotConfig.cs | `ClearAllResults()` | L131-137 | "sequence reset 트리거", "Action_FAIMeasurement.cs EStep.Init 단계 (Run 사이클 진입 시 매번)", "별도 OnReset 이벤트/메서드 미도입 (Phase 21 D-04)" |
| 6 | InspectionRecipeManager.cs | `ClearShots()` | L52-61 | "(1) 레시피 변경 — Custom/SystemHandler.cs 의 OnRecipeChanged subscriber", "(2) 앱 종료 — SystemHandler.Release() 에서 Sequences.Dispose() 직전 호출", "ClearImage 가 null-safe → 멱등" |

### 마커 카운트 (실측)

| 명령 (rg 등가) | Expected | Actual | 결과 |
|------|----------|--------|------|
| `rg -c "/// <summary>" ShotConfig.cs` | ≥ 5 | **5** | ✅ |
| `rg -c "260510 hbk Phase 21" ShotConfig.cs` | ≥ 5 | **5** | ✅ |
| `rg -c "/// <summary>" InspectionRecipeManager.cs` | ≥ 1 | **1** | ✅ |
| `rg -c "260510 hbk Phase 21" InspectionRecipeManager.cs` | ≥ 1 | **2** (Plan 01 doc + Plan 02 logging instrumentation) | ✅ |
| `rg -c "260510 hbk Phase 21" Action_FAIMeasurement.cs` | ≥ 1 | **1** (EStep.Init L60 marker) | ✅ |
| `rg -c "260510 hbk Phase 21" Custom/SystemHandler.cs` | ≥ 5 | **6** (Plan 02 — WireBufferLifecycle / UnwireBufferLifecycle / OnRecipeChanged_FlushBuffers 메서드 헤더 + body 마커) | ✅ |
| `rg -c "260510 hbk Phase 21" SystemHandler.cs (framework)` | ≥ 3 | **3** (Plan 02 — Initialize wire / Release unwire / Release ClearShots) | ✅ |
| `rg -c "caller MUST dispose\|호출자" ShotConfig.cs` (GetImage doc) | ≥ 1 | **2** | ✅ |

**총 8/8 grep check PASS.**

**결론:** AC#3 충족 — 6 XML doc 블록 + 17 마커 (5+2+1+6+3) 모두 코드 인접 grep 으로 발견 가능. ✅ **verified.**

---

## AC#4 — msbuild Debug/x64 PASS (D-10)

**Command:**

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' `
    'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' `
    -t:Clean -p:Configuration=Debug -p:Platform=x64 -v:q
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' `
    'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' `
    -t:Rebuild -p:Configuration=Debug -p:Platform=x64 -v:m -clp:Summary
```

**Result (clean rebuild — Plan 01 + Plan 02 누적 후 fresh):**

| Metric | Value |
|--------|-------|
| **Errors (오류)** | **0** |
| **Warnings (경고)** | 6 (= 3 unique × 2 builds [main + WPF temp]) |
| **Output binary** | `WPF_Example/bin/x64/Debug/DatumMeasurement.exe` 생성 |
| **Elapsed (경과 시간)** | 00:00:02.63 |

**Warning baseline preservation matrix (Phase 20 sign-off 시점 baseline 과 비교):**

| Warning ID | Location | Source | Phase 21 신규? |
|------------|----------|--------|----------------|
| `MSB3884` | `Microsoft.CSharp.CurrentVersion.targets:130` | `MinimumRecommendedRules.ruleset` 누락 (환경) | NO (사전 존재) |
| `CS0162` | `WPF_Example/Device/Camera/VirtualCamera.cs:266` | `SIMUL_MODE` conditional 분기 (Phase 21 미수정 파일) | NO (사전 존재) |
| `CS0219` | `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs:64` | `scanHorizontal` 변수 미사용 (Phase 21 미수정 파일) | NO (사전 존재) |

**신규 warning = 0.**

WPF MSBuild 의 two-pass XAML 컴파일 (`*_wpftmp.csproj` + main `DatumMeasurement.csproj`) 가 각 warning 을 2번 출력하는 것은 표준 artifact (실제 unique warning 3개 × 2 = 6).

**SIMUL_MODE 회귀:** 21-UAT.md Test 3 (사용자 SIMUL 1회 검사 — Datum 티칭 + FAI 측정 + 결과 이미지 리뷰) 에 위임.

**결론:** AC#4 build half 충족 — clean rebuild PASS, 신규 warning 0, baseline preserved. ✅ **verified (build 부분).** 회귀 검증은 UAT 에 위임.

---

## 종합 매트릭스

| AC | 자동 입증 (이 문서) | UAT 입증 (21-UAT.md) | 종합 |
|----|---------------------|----------------------|------|
| **AC#1** disk-free 표시 | ✅ grep 0 hits in scope (5/5 forbidden API patterns + 1 out-of-scope 분리) | Test 1 보강 (시각 / Process Monitor) | **verified** (자동) + UAT 보강 |
| **AC#2** dispose 입증 | (instrumentation grep 1 hit) | Test 2 사용자 카운트 ≥ 5 | **deferred_to_uat** |
| **AC#3** 수명 시점 문서화 | ✅ 6 doc 블록 + 17 마커 grep PASS | — | **verified** |
| **AC#4** msbuild + 회귀 | ✅ clean rebuild PASS, 신규 warning 0 | Test 3 SIMUL 1회 검사 / Test 4 자동 인용 | **verified (build)** + UAT 회귀 |

Phase 21 sign-off 는 21-UAT.md 의 사용자 결과 (Test 1-4) 를 받은 후 최종 결정 — Plan 03 Task 2 가 처리.

---

## Plan 03 Task 1 결론

- AC#1 / AC#3 / AC#4 (build half) **자동 verified.**
- AC#2 + AC#4 (회귀 half) **21-UAT.md 위임.**
- 자동 코드 audit 누락 0 — Phase 21 의 lifetime 계약 6 doc / 17 marker / 3 dispose channel 모두 grep-discoverable.

다음: 21-UAT.md scaffold (Plan 03 Task 2) → 사용자 SIMUL UAT 4 테스트 수행 → Phase 21 sign-off 결정.

---

*Phase: 21-memory-image-buffer*
*Verification automated by Plan 03 Task 1*
*Date: 2026-05-10*
