---
phase: 34-datum-verticaltwohorizontal-2026-05-26
plan: 02
subsystem: Halcon/Algorithms
tags: [datum, dual-image, halcon, vision-algorithm, two-image-overload]
requires:
  - .planning/phases/34-datum-verticaltwohorizontal-2026-05-26/34-01-SUMMARY.md (EDatumAlgorithm.VerticalTwoHorizontalDualImage enum 식별자 사용)
  - .planning/phases/34-datum-verticaltwohorizontal-2026-05-26/34-CONTEXT.md (D-34-01, D-34-02, D-34-13, D-34-14)
  - .planning/phases/34-datum-verticaltwohorizontal-2026-05-26/34-PATTERNS.md (section 6, 7 — TryFind/TryTeachDatum dispatch)
provides:
  - DatumFindingService.TryFindDatum(HImage imageHorizontal, HImage imageVertical, DatumConfig, out HTuple, out string) public 2-image 오버로드
  - DatumFindingService.TryTeachDatum(HImage imageHorizontal, HImage imageVertical, DatumConfig, out string) public 2-image 오버로드
  - DatumFindingService.TryFindVerticalTwoHorizontalDualImage private 메서드 (기존 TryFindVerticalTwoHorizontal 100% 복제 + ROI별 이미지 분기)
  - DatumFindingService.TryTeachVerticalTwoHorizontalDualImage private 메서드 (기존 TryTeachVerticalTwoHorizontal 100% 복제 + ROI별 이미지 분기)
affects:
  - downstream: 34-03 (MainView 티칭 UI 가 새 Teach 오버로드 호출 + Action_FAIMeasurement 가 새 Find 오버로드 호출), 34-04 (SIMUL UAT)
tech-stack:
  added: []
  patterns:
    - "Phase 34 D-34-01: ROI별 이미지 소스 분기 — Vertical=imageVertical, Horizontal_A/B=imageHorizontal"
    - "Phase 34 D-34-02: 잘못된 algorithm enum 가드 — 신규 오버로드는 VerticalTwoHorizontalDualImage 전용"
    - "Phase 34 D-34-13: 기존 단일-이미지 시그니처 0 라인 변경 — 1-image 3 algorithm 회귀 0"
    - "기존 TryFindLine/TryExtractEdgePoints/ValidateHorizontalVerticalAngles/MIN_HORIZONTAL_EDGES 헬퍼 그대로 재사용 — 신규 헬퍼 추가 0"
key-files:
  created: []
  modified:
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
decisions:
  - "옵션 A 채택 — 신규 public 오버로드 추가 (서비스가 파일 I/O 보유 X, 호출처가 두 이미지 모두 로드 후 주입). D-34-13 의 'Action_FAIMeasurement.GrabOrLoadDatumImage 단일 지점' 과 정합."
  - "기존 TryFindVerticalTwoHorizontal / TryTeachVerticalTwoHorizontal 본문 100% 복제 (helper extraction X) — 회귀 위험 0 + dual-image variant 1개만이라 공통화 가치 낮음 (CONTEXT deferred ideas 'v1.2 후보' 일치)"
  - "두 이미지 GetImageSize 각각 호출 — 같은 카메라 해상도 가정이지만 명시적으로 분리하여 향후 해상도 검증 phase 의 hook 자리 마련 (T-34-02-01 accept)"
  - "Dispose 패턴 — 기존 finally 블록 (`if (contour != null) { try { contour.Dispose(); } catch { } }`) 100% 복제 (T-34-02-02 mitigate)"
  - "Allman 브레이스 + //260527 hbk Phase 34 D-34-XX 주석 컨벤션 100% 준수 (PATTERNS.md Shared Patterns + memory feedback_comment_convention)"
metrics:
  duration: "~25m"
  completed: 2026-05-27T13:12:10Z
  tasks: 2
  files-changed: 1
  lines-added: 399
  lines-deleted: 0
  commits: 2
---

# Phase 34 Plan 02: DatumFindingService 2-image 분기 Summary

`VerticalTwoHorizontalDualImage` algorithm 용 두 신규 public 오버로드 (`TryFindDatum` / `TryTeachDatum` 2-image) + 두 신규 private 메서드 (`TryFindVerticalTwoHorizontalDualImage` / `TryTeachVerticalTwoHorizontalDualImage`) 를 `DatumFindingService.cs` 단일 파일에 추가하여, 가로축 이미지(Horizontal_A/B ROI 검출) 와 세로축 이미지(Vertical ROI 검출) 를 분리 입력받아 결합 Datum 좌표를 산출하는 비전 알고리즘 코어를 완성했다.

## One-liner

DatumFindingService 에 VerticalTwoHorizontalDualImage 전용 2-image Find/Teach 오버로드 4개 신설 — 기존 단일-이미지 3 algorithm 본문 0 라인 변경, msbuild Debug/x64 PASS (신규 warning 0).

## Implementation

### Task 1 (commit `ec64417`): TryFindDatum 2-image 오버로드 + TryFindVerticalTwoHorizontalDualImage

- 파일: `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`
- **변경 A (신규 public 오버로드, 기존 TryFindDatum 직후):**
  ```csharp
  public bool TryFindDatum(HImage imageHorizontal, HImage imageVertical, DatumConfig config, out HTuple transform, out string error)
  {
      ...
      if (config.AlgorithmTypeEnum != EDatumAlgorithm.VerticalTwoHorizontalDualImage) {
          error = "Algorithm is not VerticalTwoHorizontalDualImage; use single-image TryFindDatum overload";
          return false;
      }
      return TryFindVerticalTwoHorizontalDualImage(imageHorizontal, imageVertical, config, out transform, out error);
  }
  ```
- **변경 B (신규 private 메서드, TryFindVerticalTwoHorizontal 직후):** 기존 `TryFindVerticalTwoHorizontal` (line 371-546) 본문 100% 복제 + 3가지 변경:
  1. 시그니처 → `private bool TryFindVerticalTwoHorizontalDualImage(HImage imageHorizontal, HImage imageVertical, DatumConfig, out HTuple, out string)`
  2. Vertical 라인 검출 → `imageVertical` 사용. `imageVertical.GetImageSize` → `imageVerticalWidth/Height` 별도 변수.
  3. Horizontal A/B 에지점 검출 → `imageHorizontal` 사용. `imageHorizontal.GetImageSize` → `imageHorizontalWidth/Height` 별도 변수.
- 이후 로직 (totalEdges 가드, TupleConcat, GenContourPolygonXld, FitLineContourXld, IntersectionLl, ValidateHorizontalVerticalAngles, hom_mat2d 빌드, DetectedOrigin/DetectedRefAngle/DetectedRefAngle2 transient, Line1Detected/Line2Detected/Vertical/Horizontal_A/Horizontal_B_DetectedEdge* transient, LastFindSucceeded=true, catch/finally) 100% 동일 복제.
- 모든 신규 라인에 `//260527 hbk Phase 34 D-34-01` 또는 `D-34-02` 주석.

### Task 2 (commit `7253803`): TryTeachDatum 2-image 오버로드 + TryTeachVerticalTwoHorizontalDualImage + msbuild PASS

- 파일: `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`
- **변경 A (신규 public 오버로드, 기존 TryTeachDatum 직후):** Task 1 의 Find 오버로드와 대칭 — algorithm enum 가드 + DualImage 분기 호출.
- **변경 B (신규 private 메서드, TryTeachVerticalTwoHorizontal 직후):** 기존 `TryTeachVerticalTwoHorizontal` (line 913-1083) 본문 100% 복제 + Task 1 과 동일 3가지 변경 (시그니처, Vertical→imageVertical, Horizontal A/B→imageHorizontal).
- 이후 로직 (totalEdges 가드, TupleConcat, GenContourPolygonXld, FitLineContourXld, IntersectionLl, 기준값 저장 RefOriginRow/RefOriginCol/RefAngleRad + IsConfigured=true, Line1Detected/Line2Detected transient, LastTeachSucceeded=true, ValidateHorizontalVerticalAngles 게이트, catch/finally) 100% 동일 복제.
- catch 블록 패턴도 기존과 동일 — `if (config != null) { config.LastTeachSucceeded = false; }` + error = ex.Message.

### 신규 시그니처 4개 (모두 DatumFindingService.cs 안)

| Visibility | Signature |
|---|---|
| public  | `bool TryFindDatum(HImage imageHorizontal, HImage imageVertical, DatumConfig config, out HTuple transform, out string error)` |
| public  | `bool TryTeachDatum(HImage imageHorizontal, HImage imageVertical, DatumConfig config, out string error)` |
| private | `bool TryFindVerticalTwoHorizontalDualImage(HImage imageHorizontal, HImage imageVertical, DatumConfig config, out HTuple transform, out string error)` |
| private | `bool TryTeachVerticalTwoHorizontalDualImage(HImage imageHorizontal, HImage imageVertical, DatumConfig config, out string error)` |

## Verification

### msbuild Debug/x64 Rebuild — PASS

`C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Rebuild /v:minimal /nologo /fl` 실행 결과:

```
DatumMeasurement -> .../WPF_Example/bin/x64/Debug/DatumMeasurement.exe

MSBUILD_EXIT_CODE=0
```

- 종료 코드: **0** (PASS)
- 신규 CS error: **0**
- 신규 MSB error: **0**
- 신규 warning: **0** (Phase 21 baseline 14 동일, csproj 2회 처리로 출력 28 line)

빌드 경고 baseline (Plan 34-01 SUMMARY 와 동일 — 모두 Phase 33 이전 pre-existing):

| ID | 위치 | 분류 |
|----|------|------|
| MSB3884 | Microsoft.CSharp.CurrentVersion.targets(130,9) | infra (MinimumRecommendedRules.ruleset 누락) |
| CS0618 | Sequence_Bottom.cs(30,38) BottomSequence | Phase 33 obsolete migration |
| CS0618 | Sequence_Top.cs(19,35) TopSequence | Phase 33 obsolete migration |
| CS0162 | VirtualCamera.cs(266,13) | unreachable code (pre-existing) |
| CS0618 | SequenceHandler.cs(51,21) TopInspectionAction | Phase 33 obsolete migration |
| CS0618 | SequenceHandler.cs(52,21) TopInspectionAction | Phase 33 obsolete migration |
| CS0618 | SequenceHandler.cs(53,21) BottomInspectionAction | Phase 33 obsolete migration |

→ 신규 코드 (DatumFindingService.cs +399 라인) 는 warning 생성 0.

### D-34-13 / D-34-14 가드 — PASS

Plan 02 단계에서 다음 파일들의 변경 0 라인 검증 (`git diff --numstat HEAD` — 무출력 = 변경 0):

```
WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs
WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
WPF_Example/TcpServer/VisionResponsePacket.cs
WPF_Example/UI/ContentItem/MainView.xaml.cs
```

결과: **출력 0 라인** = 6개 가드 파일 모두 변경 0 라인.

기존 단일-이미지 4 함수 (TryFindDatum / TryTeachDatum / TryFindVerticalTwoHorizontal / TryTeachVerticalTwoHorizontal) 본문 0 라인 변경 — `git diff` 가 신규 메서드/오버로드만 표시 (insertion-only, 0 deletions).

### Acceptance Criteria 매핑

| Criteria (Plan 02) | 결과 | 증거 |
|---|---|---|
| TryFindDatum(HImage imageHorizontal, HImage imageVertical, ...) public 오버로드 추가 | PASS | grep `TryFindDatum\(HImage imageHorizontal` = 1 |
| TryTeachDatum(HImage imageHorizontal, HImage imageVertical, ...) public 오버로드 추가 | PASS | grep `TryTeachDatum\(HImage imageHorizontal` = 1 |
| TryFindVerticalTwoHorizontalDualImage private 메서드 추가 (식별자 ≥ 2: declaration + dispatch call) | PASS | grep `TryFindVerticalTwoHorizontalDualImage` = 2 |
| TryTeachVerticalTwoHorizontalDualImage private 메서드 추가 (식별자 ≥ 2: declaration + dispatch call) | PASS | grep `TryTeachVerticalTwoHorizontalDualImage` = 2 |
| 기존 단일-이미지 TryFindDatum / TryTeachDatum 시그니처 보존 | PASS | grep `TryFindDatum\(HImage image, DatumConfig config` = 1, grep `TryTeachDatum\(HImage image, DatumConfig config` = 1 |
| 신규 메서드 본문 안 imageVertical.GetImageSize 호출 (Find + Teach 각 1개 = 2개) | PASS | grep = 2 |
| 신규 메서드 본문 안 imageHorizontal.GetImageSize 호출 (Find + Teach 각 1개 = 2개) | PASS | grep = 2 |
| 신규 메서드 본문 안 TryFindLine(imageVertical,...) 호출 (Find + Teach 각 1개 = 2개) | PASS | grep `imageVertical, imageVerticalWidth` = 2 |
| 신규 메서드 본문 안 TryExtractEdgePoints(imageHorizontal,...) 호출 (Find + Teach 각 2개 = 4개) | PASS | grep `imageHorizontal, imageHorizontalWidth` = 4 |
| 신규 라인에 //260527 hbk Phase 34 D-34 주석 ≥ 5 | PASS | grep = 196 |
| msbuild Debug/x64 Rebuild 종료 코드 0 | PASS | exit 0 |
| 신규 CS error 0, 신규 warning 0 | PASS | 14 warning 모두 pre-existing baseline (Plan 01 동일) |
| git diff --numstat: DatumFindingService.cs 변경 라인 ≈ +350~+400 | PASS | +399 / -0 |
| git diff --numstat: EDatumAlgorithm / DatumConfig / Action_FAIMeasurement / InspectionSequence / VisionResponsePacket / MainView.xaml.cs 모두 0,0 | PASS | 무출력 |

## Threat Surface Scan

`<threat_model>` 의 T-34-02-01 ~ T-34-02-06 모두 plan 대로 처리:

- **T-34-02-01 (Tampering — 해상도 불일치)** accept: 두 이미지 같은 카메라 해상도 가정 (CONTEXT D-34 Claude's Discretion #4). 신규 메서드는 두 GetImageSize 호출을 명시적으로 분리하여 향후 해상도 검증 phase 의 hook 자리 마련.
- **T-34-02-02 (DoS — contour dispose 누락)** mitigated: 기존 finally 블록 패턴 100% 복제 (`if (contour != null) { try { contour.Dispose(); } catch { } }`).
- **T-34-02-03 (DoS — stale transform on exception)** mitigated: catch 블록에서 `HOperatorSet.HomMat2dIdentity(out transform);` + `return false;` (Find 측), Teach 측은 `config.LastTeachSucceeded = false` 표시. 기존 패턴 동일.
- **T-34-02-04 (Spoofing — 잘못된 algorithm enum)** mitigated: 신규 오버로드 진입부에 `if (config.AlgorithmTypeEnum != EDatumAlgorithm.VerticalTwoHorizontalDualImage)` 명시 가드 + 명확한 error 메시지 ("...; use single-image TryFindDatum overload" / "...; use single-image TryTeachDatum overload").
- **T-34-02-05 (Information Disclosure)** accept: 기존 error 패턴 ("Vertical: " + lineError, "Horizontal_A: " + edgeErrorA 등) 과 동일 — 진단 목적 의도된 노출. PII/secret 없음.
- **T-34-02-06 (Repudiation)** mitigated: 모든 신규 라인 `//260527 hbk Phase 34 D-34-XX` 주석 (grep 매치 196건).

**신규 threat surface 없음** — Plan 02 는 기존 헬퍼 (`TryFindLine`, `TryExtractEdgePoints`, `ValidateHorizontalVerticalAngles`, `MIN_HORIZONTAL_EDGES`) + 기존 transient 필드를 그대로 재사용. file I/O / network / 외부 의존성 신규 도입 0.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] worktree bin/x64/Debug 누락 HintPath DLL 5개**
- **Found during:** Task 2 msbuild 첫 시도 시 PropertyTools.Wpf / WPF.MDI / Basler.Pylon / MvCamCtrl.Net 등 HintPath 참조 못 찾음.
- **Issue:** worktree 가 fresh 상태라서 csproj 의 `HintPath="bin\x64\Debug\*.dll"` 로 참조되는 build-only DLL 들이 worktree bin 에 없음. Plan 01 SUMMARY 의 동일 deviation 과 동일 원인.
- **Fix:** main repo `WPF_Example/bin/x64/Debug/` 에서 worktree 의 동일 경로로 Basler.Pylon.dll / MvCamCtrl.Net.dll / PropertyTools.dll / PropertyTools.Wpf.dll / WPF.MDI.dll + dll/ 하위 모두 복사. 코드/csproj/packages.config 수정 없음. 빌드 후 즉시 정상 동작.
- **Files modified:** worktree 의 `WPF_Example/bin/x64/Debug/` 에 build input DLL 5개 + dll/ 하위 추가 (gitignored 영역). 소스 코드 / `.planning/` 외 commit 영역 변경 없음.
- **Commit:** N/A (build input DLL, gitignored)

### Out-of-scope deferred

- `WPF_Example/bin/x64/Debug/` 의 누락 DLL 문제는 **Plan 02 범위 외**의 worktree 환경 문제 (Plan 01 SUMMARY 와 동일). 본 plan 으로 인해 발생한 것이 아니며 orchestrator 가 worktree 셋업 시 bin 동기화하면 향후 회피 가능 — 별도 인프라 작업으로 분리.

## Known Stubs

없음. Plan 02 는 알고리즘 코어 신설만 — UI rendering / 데이터 wiring 안 함. 신규 4개 메서드는 Plan 03 (MainView 티칭 UI + Action_FAIMeasurement 검사 분기) 에서 소비되도록 의도된 설계. Plan 02 단계의 신규 메서드 호출 site = 0개 (의도된 dead-code-until-wired 상태).

## Self-Check: PASSED

**1. Files modified — 모두 존재:**
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — FOUND (+399, -0)

**2. Commits — 모두 git log 에 존재:**
- `ec64417` Task 1 — FOUND (`feat(34-02): add TryFindDatum 2-image overload + TryFindVerticalTwoHorizontalDualImage (D-34-01/02)`)
- `7253803` Task 2 — FOUND (`feat(34-02): add TryTeachDatum 2-image overload + TryTeachVerticalTwoHorizontalDualImage (D-34-01/02)`)

**3. Acceptance criteria — 모두 PASS** (위 매핑 표 참조).

**4. msbuild PASS — exit 0** (build.log 저장: `.planning/tmp/build.log`, build-stdout.log: `.planning/tmp/build-stdout.log`).

**5. D-34-13/D-34-14 가드 — 6개 파일 모두 변경 0 라인.**
