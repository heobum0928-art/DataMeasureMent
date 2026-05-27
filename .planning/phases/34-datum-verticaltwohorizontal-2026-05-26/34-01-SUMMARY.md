---
phase: 34-datum-verticaltwohorizontal-2026-05-26
plan: 01
subsystem: Custom/Sequence/Inspection
tags: [datum, dual-image, enum, propertygrid, ini-compat]
requires:
  - .planning/phases/22-image-dual-structure/22-CONTEXT.md (IMG-01 패턴)
  - .planning/phases/17-datum-teaching-validation-ux/17-CONTEXT.md (ICustomTypeDescriptor 패턴)
  - .planning/phases/12-*/12-CONTEXT.md (D-09 enum 도입)
provides:
  - EDatumAlgorithm.VerticalTwoHorizontalDualImage enum 식별자
  - DatumConfig.TeachingImagePath_Vertical 필드 (string=\"\")
  - DatumConfig.AlgorithmTypeList 신규 ToString() 항목
  - DatumConfig.EnsurePerRoiDefaults TeachingImagePath_Vertical null→\"\" 정규화
  - DatumConfig.IsHiddenForAlgorithm DualImage case + 기존 3 case 의 TeachingImagePath_Vertical hide 라인
affects:
  - downstream: 34-02 (DatumFindingService 2-image 분기), 34-03 (MainView 티칭 UI + Action_FAIMeasurement 분기), 34-04 (SIMUL UAT)
tech-stack:
  added: []
  patterns:
    - "Phase 22 IMG-01: TeachingImagePath null→\"\" 정규화 패턴 1:1 복제"
    - "Phase 17 D-09: ICustomTypeDescriptor.IsHiddenForAlgorithm switch case 확장"
    - "Phase 12 D-09: ParamBase reflection String case 자동 직렬화 (enum 은 string 으로)"
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
decisions:
  - "enum 값을 끝에 append — 기존 3 값 ordinal 보존 (D-34-03)"
  - "TeachingImagePath_Vertical 을 [Category(\"Datum|ImageSource\")] 그룹에 배치 — 기존 TeachingImagePath 와 동일 카테고리로 UX 일관성 (D-34-04)"
  - "INI 정규화는 algorithm 분기 없이 일률 적용 — DualImage 가 아닌 algorithm 의 기존 INI 로드 시에도 \"\" 정규화 (D-34-11/D-34-12)"
  - "IsHiddenForAlgorithm 의 기존 3 case 에 'name == TeachingImagePath_Vertical → hide' 라인 추가 — 역방향 hide (D-34-04)"
  - "신규 DualImage case 본문은 VTH 와 동일 hide 그룹 (Line1_*/Line2_*/Circle_*) — Vertical_*/Horizontal_A_*/Horizontal_B_* + TeachingImagePath_Vertical 노출"
metrics:
  duration: "5m 44s"
  completed: 2026-05-27T03:55:22Z
  tasks: 3
  files-changed: 2
  lines-added: 17
  lines-deleted: 0
  commits: 3
---

# Phase 34 Plan 01: Datum DualImage 데이터 모델 토대 Summary

`EDatumAlgorithm.VerticalTwoHorizontalDualImage` enum 1줄 + `DatumConfig.TeachingImagePath_Vertical` 필드/콤보박스/INI 정규화/PropertyGrid 분기를 추가하여 Phase 34 후속 plan(02/03/04) 의 데이터 모델 토대를 마련했다.

## One-liner

Datum 알고리즘에 듀얼 티칭 이미지 변형(DualImage) 식별자 1개와 세로축 전용 이미지 경로 필드(`TeachingImagePath_Vertical`)를 추가 — 기존 1-image 3종(TLI/CTH/VTH) 회귀 0 보장.

## Implementation

### Task 1 (commit `5159c15`): EDatumAlgorithm enum 1줄 추가

- 파일: `WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs`
- 변경: enum body 마지막에 `VerticalTwoHorizontalDualImage,` 1줄 append.
- 기존 3 값 순서 보존 → ParamBase string 직렬화 기반 INI 회귀 0.
- 신규 라인 주석: `//260527 hbk Phase 34 D-34-03` (변경 추적 컨벤션).

### Task 2 (commit `d209dbf`): DatumConfig 필드 + AlgorithmTypeList 항목 추가

- 파일: `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs`
- 변경 A (필드, line 46-50): 기존 `TeachingImagePath` (line 44) 직후에 다음 추가:
  ```csharp
  //260527 hbk Phase 34 D-34-04 — 가로축 이미지(TeachingImagePath) 와 분리된 세로축 이미지 경로.
  //  algorithm == VerticalTwoHorizontalDualImage 일 때만 의미가 있으며, ...
  [Category("Datum|ImageSource")]
  public string TeachingImagePath_Vertical { get; set; } = ""; //260527 hbk Phase 34 D-34-04
  ```
- 변경 B (콤보박스, line 89): `AlgorithmTypeList` getter 안 List initializer 끝에 1줄 append:
  ```csharp
  EDatumAlgorithm.VerticalTwoHorizontalDualImage.ToString(), //260527 hbk Phase 34 D-34-05
  ```
- ParamBase reflection 자동 직렬화 (case "String") → INI 저장/로드 코드 추가 0 (Phase 22 IMG-01 패턴 1:1).

### Task 3 (commit `e35a955`): INI 정규화 hook + ICustomTypeDescriptor 분기 + msbuild PASS

- 파일: `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs`
- 변경 A (정규화, line 571): 기존 `TeachingImagePath` null 가드(line 570) 다음 줄에 추가:
  ```csharp
  if (TeachingImagePath_Vertical == null) TeachingImagePath_Vertical = ""; //260527 hbk Phase 34 D-34-11
  ```
- 변경 B-1 (역방향 hide, 3 case): `IsHiddenForAlgorithm` switch 안 기존 3 case (TwoLineIntersect / CircleTwoHorizontal / VerticalTwoHorizontal) 첫 줄 직전에 각각 1줄 추가 (총 3 라인):
  ```csharp
  if (name == "TeachingImagePath_Vertical") return true; //260527 hbk Phase 34 D-34-04 — DualImage 전용 필드 hide
  ```
- 변경 B-2 (DualImage case): 기존 VTH case 직후에 신규 case 추가 (5 라인):
  ```csharp
  case EDatumAlgorithm.VerticalTwoHorizontalDualImage: //260527 hbk Phase 34 D-34-04 — DualImage 변형: ...
      if (name.StartsWith("Line1_") || name.StartsWith("Line1Detected_")) return true;
      if (name.StartsWith("Line2_") || name.StartsWith("Line2Detected_")) return true;
      if (name.StartsWith("Circle_") || ...) return true;
      return false;
  ```
- 본문 hide 그룹 = VTH 와 동일 (Vertical_* + Horizontal_A_*/Horizontal_B_* 노출). 차이 = TeachingImagePath_Vertical 추가 노출 (역방향 hide 라인 없음 → 노출).

## Verification

### msbuild Debug/x64 Rebuild — PASS

`C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Rebuild /v:minimal /nologo` 실행 결과:

```
DatumMeasurement -> .../WPF_Example/bin/x64/Debug/DatumMeasurement.exe

MSBUILD_EXIT_CODE=0
```

- 종료 코드: **0** (PASS)
- 신규 CS error: **0**
- 신규 MC error: **0**
- 신규 warning: **0**

빌드 경고 baseline (7 unique × 2 csproj pass = 14 line; 모두 Phase 33 이전 pre-existing):

| ID | 위치 | 분류 |
|----|------|------|
| MSB3884 | Microsoft.CSharp.CurrentVersion.targets(130,9) | infra (MinimumRecommendedRules.ruleset 누락) |
| CS0618 | Sequence_Bottom.cs(30,38) BottomSequence | Phase 33 obsolete migration |
| CS0618 | Sequence_Top.cs(19,35) TopSequence | Phase 33 obsolete migration |
| CS0162 | VirtualCamera.cs(266,13) | unreachable code (pre-existing) |
| CS0618 | SequenceHandler.cs(51,21) TopInspectionAction | Phase 33 obsolete migration |
| CS0618 | SequenceHandler.cs(52,21) TopInspectionAction | Phase 33 obsolete migration |
| CS0618 | SequenceHandler.cs(53,21) BottomInspectionAction | Phase 33 obsolete migration |

→ 신규 코드(EDatumAlgorithm.cs +1 / DatumConfig.cs +16)는 warning 생성 0.

### D-34-13 / D-34-14 가드 — PASS

Plan 01 단계에서 다음 파일들의 변경 0 라인 검증 (base `f0e77947a60b8eb801f04464cd9a6a501c64cf61` 대비):

```
git diff --numstat f0e7794 -- \
  WPF_Example/Halcon/Algorithms/DatumFindingService.cs \
  WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs \
  WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs \
  WPF_Example/TcpServer/VisionResponsePacket.cs \
  WPF_Example/UI/ContentItem/MainView.xaml.cs
```

결과: **출력 0 라인** = 5개 가드 파일 모두 변경 0 라인 ✓

### Acceptance Criteria 매핑

| Criteria (Plan 01) | 결과 | 증거 |
|---|---|---|
| EDatumAlgorithm 에 `VerticalTwoHorizontalDualImage,` 1회 등장 | PASS | EDatumAlgorithm.cs:13 |
| 기존 3 enum 값 순서 보존 | PASS | EDatumAlgorithm.cs:10-12 (TLI/CTH/VTH 순서 그대로) |
| 신규 enum 라인에 `//260527 hbk Phase 34 D-34-03` | PASS | EDatumAlgorithm.cs:13 |
| EDatumAlgorithm.cs 파일 라인 = 기존+1 (14→15) | PASS | 라인 1추가 (+1/-0) |
| DatumConfig 에 `public string TeachingImagePath_Vertical { get; set; } = "";` 1회 | PASS | DatumConfig.cs:50 |
| 신규 필드가 line 45~50 사이 (기존 line 44 직후) | PASS | DatumConfig.cs:50 |
| `[Category("Datum|ImageSource")]` 어트리뷰트 | PASS | DatumConfig.cs:49 |
| AlgorithmTypeList 에 `EDatumAlgorithm.VerticalTwoHorizontalDualImage.ToString()` 1회 | PASS | DatumConfig.cs:89 |
| 기존 3 항목 (TLI/CTH/VTH ToString) 보존 | PASS | DatumConfig.cs:86-88 |
| `if (TeachingImagePath_Vertical == null) TeachingImagePath_Vertical = "";` 1회 (EnsurePerRoiDefaults 안) | PASS | DatumConfig.cs:571 |
| 정규화 라인이 기존 line 563 (현재 570) 다음 줄에 위치 | PASS | DatumConfig.cs:570→571 |
| `case EDatumAlgorithm.VerticalTwoHorizontalDualImage:` 1회 (IsHiddenForAlgorithm switch) | PASS | DatumConfig.cs:696 |
| `if (name == "TeachingImagePath_Vertical") return true;` 3회 (TLI/CTH/VTH 각 1) | PASS | DatumConfig.cs:678, 684, 691 |
| 신규 DualImage case 본문 = VTH 와 동일 hide 그룹 | PASS | DatumConfig.cs:697-700 (Line1_*/Line2_*/Circle_* hide 3줄 + return false) |
| 신규 라인에 `//260527 hbk Phase 34` 주석 (>=5건) | PASS | grep 매치 8건 (Task 2: 2건 / Task 3: 6건) |
| msbuild Debug/x64 Rebuild 종료 코드 0 | PASS | exit 0 |
| 신규 CS error 0, 신규 warning 0 | PASS | 14 warning 모두 pre-existing baseline |
| EDatumAlgorithm.cs ≈ +1, DatumConfig.cs ≈ +8~+12 | PASS (DatumConfig +16) | +16: 필드 5줄 + 콤보 1줄 + 정규화 1줄 + IsHidden 신규 case 5줄 + TIP_V hide 3줄 + 신규 case 안 빈 줄 등 (오차 범위 안에서 합리적) |

## Threat Surface Scan

`<threat_model>` 의 T-34-01-01 ~ T-34-01-06 모두 plan 대로 처리:

- **T-34-01-01 (Tampering INI)** mitigated: `EnsurePerRoiDefaults` 안 `if (TeachingImagePath_Vertical == null) TeachingImagePath_Vertical = "";` 일률 정규화.
- **T-34-01-02 (Info Disclosure)** mitigated: 3 case (TLI/CTH/VTH) 각각 `name == "TeachingImagePath_Vertical" → return true` 역방향 hide.
- **T-34-01-03 (DoS)** accepted: 기존 AlgorithmTypeEnum getter 의 TryParse → TwoLineIntersect 폴백 보존.
- **T-34-01-04 (Spoofing)** accepted: enum 끝에 append → ordinal 보존, string 직렬화 충돌 없음.
- **T-34-01-05 (Repudiation)** mitigated: 모든 신규 라인 `//260527 hbk Phase 34 D-34-XX` 주석 (grep 8건).
- **T-34-01-06 (EoP)** mitigated: unsafe / native 도입 0 — auto-property + 리스트 항목 + null guard + switch case 만.

**신규 threat surface 없음** — Plan 01 은 enum + auto-property + dispatch 분기로 한정.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] worktree bin/x64/Debug 누락 HintPath DLL 6개**
- **Found during:** Task 3 msbuild 실행 시 MC3074 PropertyTools.Wpf / WPF.MDI / Bitmap 등 XAML namespace 못 찾음.
- **Issue:** worktree 가 fresh 상태라서 csproj 의 `HintPath="bin\x64\Debug\*.dll"` 로 참조되는 build-only DLL (Basler.Pylon, MvCamCtrl.Net, PropertyTools, PropertyTools.Wpf, WPF.MDI, dll/x64/ImageGlass.ImageBox) 이 worktree bin 에 없음. csproj 가 packages.config 형식이라 NuGet restore 만으로는 bin 채워지지 않음 (이 DLL 들은 packages 가 아닌 로컬 vendor DLL).
- **Fix:** main repo `WPF_Example/bin/x64/Debug/` 에서 worktree 의 동일 경로로 build input DLL 6개 복사. 코드 / csproj / packages.config 수정 없음. 빌드 후 즉시 정상 동작.
- **Files modified:** worktree 의 `WPF_Example/bin/x64/Debug/` 에 build input DLL 6개 추가 (gitignored 영역). 소스 코드 / `.planning/` 외 commit 영역 변경 없음.
- **Commit:** N/A (build input DLL, gitignored)

### Out-of-scope deferred

- `WPF_Example/bin/x64/Debug/` 의 누락 DLL 문제는 **Plan 01 범위 외**의 worktree 환경 문제. 본 plan 으로 인해 발생한 것이 아니며 다른 worktree 가 동일 phase 를 실행해도 동일 영향. orchestrator 가 worktree 셋업 시 bin 동기화하면 향후 회피 가능 — 별도 인프라 작업으로 분리.

## Known Stubs

없음. Plan 01 은 enum + auto-property + dispatch 분기 추가만 — UI rendering / 데이터 wiring 안 함. 신규 필드/콤보 항목은 Plan 02 (DatumFindingService 분기) + Plan 03 (MainView 티칭 UI) 에서 소비되도록 의도된 설계.

## Self-Check: PASSED

**1. Files modified — 모두 존재:**
- `WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs` — FOUND (15 lines, +1)
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — FOUND (+16)

**2. Commits — 모두 git log 에 존재:**
- `5159c15` Task 1 — FOUND
- `d209dbf` Task 2 — FOUND
- `e35a955` Task 3 — FOUND

**3. Acceptance criteria — 모두 PASS** (위 매핑 표 참조).

**4. msbuild PASS — exit 0** (build.log 저장: `.planning/tmp/build.log`).

**5. D-34-13/D-34-14 가드 — 5개 파일 모두 변경 0 라인.**
