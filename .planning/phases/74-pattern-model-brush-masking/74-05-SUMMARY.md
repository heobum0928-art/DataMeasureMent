---
phase: 74-pattern-model-brush-masking
plan: 05
status: complete
date: 2026-08-27
---

# 74-05 SUMMARY — Datum 티칭 화면(MainView)에 브러시 패널 + 🔴 B-1 블로커 수정

## 편집 전 실측 기준값 (기록 필수)

| 항목 | 값 |
|---|---|
| `MainView.xaml.cs` 줄 수 | **4476** |
| `MainView.xaml.cs` `CustomMessageBox` | **46** |
| `MainView.xaml.cs` 삼항 | **3** |
| `MainView.xaml.cs` `svc.TryCreateModel(img,` | **2** |
| `MainView.xaml` `<RowDefinition` | **7** |
| `MainView.xaml` `Grid.Row` 분포 | 0:3 / 1:2 / 2:1 / 3:1 / 4:1 |
| `MainView.xaml` `Grid.Column` 분포 | 0:3 / 1:3 / 2:2 |

## 🔴 B-1 블로커 수정 (quick-260827-hdf 에서 계획에 반영된 항목)

`canvasToolbar` Border 가 `Height="36"` 으로 고정돼 있어, 계획대로 그 안 `Grid.Row="1"` 에
패널을 넣으면 **Row 1 이 0px** 가 되어 패널이 안 보이거나 아래 HALCON 창을 침범한다(airspace 재발).

```xml
<!-- 편집 전 -->  Padding="8,4" Height="36">
<!-- 편집 후 -->  Padding="8,4" MinHeight="36">
```

**한 단어만 바꿨다.** 값 `36` 은 그대로. 루트 Grid 의 Row 0 이 `Height="Auto"` 라 패널이 펼쳐진
만큼만 늘어나고, 접히면(`Visibility="Collapsed"`) 다시 36px 로 돌아온다.

| B-1 검증 | 기대 | 실측 |
|---|---|---|
| `Padding="8,4" Height="36"` | 0 | **0** ✅ |
| `Padding="8,4" MinHeight="36"` | 1 | **1** ✅ |
| `Height=` 를 지운 줄 수 (다른 컨트롤 높이 미접촉) | 1 | **1** ✅ |

## 만든 것

| 파일 | 내용 |
|---|---|
| `Halcon/Services/DatumPatternModelRegenService.cs` (신규) | Datum 모델 경로 조회 + 모달 없는 재생성 |
| `UI/ContentItem/MainView.xaml` | `MinHeight` 수정 + RowDefinitions 2 + [브러시] 토글 + 패널 |
| `UI/ContentItem/MainView.xaml.cs` | 필드 1 + Loaded 배선 + 토글 핸들러 + 재생성 래퍼 + 선택변경 훅 |
| `DatumMeasurement.csproj` | Compile 1개 — **커밋 안 함** |

## 검증 결과

**빌드 SIMUL-ON:** 에러 **0** / 경고 **18줄** / 코드 종류 2종 — baseline 유지.

| acceptance | 기대 | 실측 |
|---|---|---|
| `dmv:PatternBrushPanel` / `x:Name="brushPanel"` / `btn_brushMask` | 1/1/1 | **1/1/1** ✅ |
| `<RowDefinition` (편집 전 **7**) | **+2** = 9 | **9** ✅ |
| `Grid.Row` 분포 — `"1"` 만 +1 | 0:3/1:**3**/2:1/3:1/4:1 | **동일** ✅ |
| `Grid.Column` 분포 무변경 | 0:3/1:3/2:2 | **동일** ✅ |
| **airspace 회피** — HWND 컨테이너 안 패널 | 0 | **0** ✅ |
| `RegenerateDatumPatternSilent()` | 1 | **1** ✅ |
| `BrushMaskToggleButton_Click` | 1 | **1** ✅ |
| `brushPanel.ViewModel` | 6 | **6** ✅ |
| `_brushTargetDatum` | 4 | **4** ✅ |
| `svc.TryCreateModel(img,` (편집 전 **2**) | 2 | **2** ✅ 기존 흐름 무변경 |
| `CustomMessageBox` (편집 전 **46**) | 46 | **46** ✅ 새 모달 0건 |
| `MainView.xaml.cs` 삼항 (편집 전 **3**) | 3 | **3** ✅ |
| 줄 수 증가 | ≤+45 | **+46** ⚠ (아래) |
| 서비스 `GetModelPathsForMask` / `RegenerateSilent` | 1/1 | **1/1** ✅ |
| 서비스 `ResolveDatumModelPath(` / `2(` | 2/2 | **2/2** ✅ |
| 서비스 **경로 직접 조립** (`GetPatternModelFilePath`/`Path.Combine`) | 0 | **0** ✅ |
| 서비스 `TryCreateModel(` / `TryFindPose(` | 2/2 | **2/2** ✅ |
| 서비스 `EnsurePerRoiDefaults();` | 1 | **1** ✅ |
| 서비스 `CustomMessageBox` **코드** / `SaveRecipe` | 0/0 | **0/0** ✅ (아래) |
| 서비스 `?:` / `??` / `?.` | 0 | **0/0/0** ✅ |
| `Custom/` 무변경 (이 plan 은 Align 미접촉) | 빈 출력 | **빈 출력** ✅ |

## Deviations

**[Rule 3 - 기준 초과 1줄] `MainView.xaml.cs` 증가량 +46 (기준 ≤+45)**

- 실제 추가 47줄 중 **10줄이 주석/빈 줄**이다. 실코드는 약 37줄
  (필드 1 + Loaded 배선 5 + 토글 핸들러 17 + 재생성 래퍼 11 + 선택변경 훅 3).
- 이 기준은 "새 로직을 code-behind 에 넣지 않았다" 는 **프록시 지표**이고, 계산/판정 로직은 전부
  `DatumPatternModelRegenService` 에 있다(`svc.TryCreateModel(img,` 카운트 무변경 = 기존 흐름 무접촉,
  새 `CustomMessageBox` 0건이 그 증거).
- **1줄을 맞추려고 주석을 지우지 않았다.** 인수인계가 경고한 "숫자를 맞추려 코드를 지우는" 패턴이다.

**[Rule 3 - 검증 기준 함정] 서비스 `CustomMessageBox` grep 이 주석을 셌다 (코드 수정 없음)**

`DatumPatternModelRegenService.cs` 에서 **1** 로 나왔으나 `50줄` XML doc 주석이다:
`/// MainView.InvokeCreatePatternModel 의 계산 흐름을 그대로 따르되 CustomMessageBox 와 …`
**실사용 0건.** 이 주석은 "이 서비스가 원본과 무엇이 다른가" 를 설명하는 문서다.

Phase 75(3회) + 74-01 + 74-04 에 이어 **여섯 번째** 주석-포함 grep 함정.

## Self-Check: PASSED

1. 빌드 에러 0, 경고 코드 종류 2종뿐 ✅
2. `MainView.xaml.cs` 새 `CustomMessageBox` 0건 (46 유지) ✅
3. `DatumPatternModelRegenService` 가 경로를 직접 조립하지 않는다 (`Path.Combine` 0건) ✅
4. 브러시 패널이 HWND 컨테이너(`Grid Grid.Row="1"`) 밖에 있다 ✅
5. `Grid.Row`/`Grid.Column` 분포가 새 패널 1건 외 무변경 ✅
6. 🔴 `canvasToolbar` `Height="36"` → `MinHeight="36"` (B-1 해소) ✅
