---
phase: 74-pattern-model-brush-masking
plan: 04
status: complete
date: 2026-08-27
---

# 74-04 SUMMARY — Align 화면(Bottom / Tray)에 브러시 패널 배선

이 phase 의 원래 동기(Bottom Align 캘리브레이션 노이즈)가 여기서 실현된다.

## 편집 전 실측 기준값 (기록 필수)

| 항목 | 값 |
|---|---|
| `AlignShapeMatchService.cs` 삼항 | **0** |
| `AlignShapeMatchService.cs` `Directory.CreateDirectory` 매치 | **3** (주석 1 + 실호출 2) |
| `BottomVisionView.xaml.cs` / `TrayVisionView.xaml.cs` 삼항 | **0 / 0** |
| `Matcher.TryTeach(` (Bottom / Tray) | **1 / 1** |
| `<RowDefinition` (Bottom / Tray XAML) | **9 / 8** |

## 만든 것

| 파일 | 내용 |
|---|---|
| `Custom/EthernetVision/AlignShapeMatchService.cs` | `GetModelPathsForMask` 추가 — 순수 삽입(삭제 0줄) |
| `Custom/UI/BottomVisionView.xaml` | `xmlns:ui` + 티칭 GroupBox 안 패널 1개 |
| `Custom/UI/BottomVisionView.xaml.cs` | 훅 배선 3곳 + `RegenerateTeachSilent` |
| `Custom/UI/TrayVisionView.xaml` | 〃 (슬롯 없음) |
| `Custom/UI/TrayVisionView.xaml.cs` | 〃 (슬롯 없는 `TryTeach` 오버로드) |

csproj 무변경(신규 파일 없음).

## 설계 요점

- **`GetModelPathsForMask` 는 `BuildShmPath` 를 쓴다** — 순수 문자열 도출이라 폴더를 만들지 않는다.
  `GetShmPath` 는 `Directory.CreateDirectory` 부작용이 있어 **조회 용도로 쓰면 안 된다**
  (acceptance 로 `GetShmPath` 0건 강제).
- **재생성은 기존 `TryTeach` 재호출**이다. `TryTeach` 가 모델 재생성 + ref pose 재기록을 전부
  담당하므로 중복 구현이 없다. Bottom 은 슬롯 오버로드, Tray 는 슬롯 없는 오버로드.
- **`RowDefinition` / `Grid.Row` 를 한 글자도 안 건드렸다** — 기존 티칭 GroupBox 안에 넣었기 때문이다
  (과거 Row 번호 재배치가 사고의 원인이었다).
- 마스크 재로드 시점: Bottom 3곳(Attach / 슬롯 전환 / 티칭 성공), Tray 2곳(Attach / 티칭 성공).

## 검증 결과

**빌드 SIMUL-ON:** 에러 **0** / 경고 **18줄** / 코드 종류 2종 — baseline 유지.

| acceptance | 기대 | 실측 |
|---|---|---|
| `public IList<string> GetModelPathsForMask` | 1 | **1** ✅ |
| └ 안에서 `BuildShmPath` | 2 | **2** ✅ |
| └ 안에서 `GetShmPath` (부작용 헬퍼 미사용) | 0 | **0** ✅ |
| `Directory.CreateDirectory` **실호출** 무변경 | 2 | **2** ✅ (아래) |
| `public bool TryTeach` 오버로드 | 4 | **4** ✅ |
| `AlignShapeMatchService.cs` 삭제 줄 | 0 | **0** ✅ |
| `AlignShapeMatchService.cs` 삼항 (편집 전 0) | 0 | **0** ✅ |
| **Bottom** `xmlns:ui` / 패널 / `x:Name` | 1/1/1 | **1/1/1** ✅ |
| **Bottom** `<RowDefinition` (편집 전 **9**) | 9 | **9** ✅ 무변경 |
| **Bottom** `RegenerateTeachSilent()` | 1 | **1** ✅ |
| **Bottom** 훅 3종 (`ModelPathsProvider`/`ModelRegenerator`/`Attach`) | 1/1/1 | **1/1/1** ✅ |
| **Bottom** `ReloadMaskFromDisk();` | 3 | **3** ✅ |
| **Bottom** `RegenerateTeachSilent` 안 `CustomMessageBox` | 0 | **0** ✅ |
| **Bottom** `Matcher.TryTeach(` (편집 전 1) | 2 | **2** ✅ |
| **Bottom** 삼항 (편집 전 0) | 0 | **0** ✅ |
| **Tray** `xmlns:ui` / `x:Name` | 1/1 | **1/1** ✅ |
| **Tray** `<RowDefinition` (편집 전 **8**) | 8 | **8** ✅ 무변경 |
| **Tray** `RegenerateTeachSilent()` | 1 | **1** ✅ |
| **Tray** `ReloadMaskFromDisk();` | 2 | **2** ✅ |
| **Tray** `RegenerateTeachSilent` 안 `EBottomAlignSlot` (슬롯 없는 오버로드) | 0 | **0** ✅ |
| **Tray** `Matcher.TryTeach(` (편집 전 1) | 2 | **2** ✅ |
| `Halcon/` `UI/` `Setting/` 무변경 | 빈 출력 | **빈 출력** ✅ |

## Deviations

**[Rule 3 - 검증 기준 함정] `Directory.CreateDirectory` grep 이 주석을 셌다 (코드 수정 없음)**

`AlignShapeMatchService.cs` 매치가 편집 전 **3** → 편집 후 **4** 로 늘었으나,
`git diff` 로 확인한 결과 **이번 편집이 추가한 매치는 주석 한 줄뿐**이다:

```
+        //  GetShmPath 는 Directory.CreateDirectory 부작용이 있어 '조회' 용도로는 쓰면 안 된다.
```

**실호출은 편집 전과 동일하게 2건**(`124줄`, `281줄`)이고 **삭제 줄은 0**이다.
이 주석은 "왜 `BuildShmPath` 를 쓰는가" 를 설명하는 핵심 문서라 지우지 않았다.

Phase 75(75-02/03/04) + 74-01 에 이어 **다섯 번째** 주석-포함 grep 함정.
`grep -v '//'` 도 완전하지 않다 — `281줄` 처럼 **끝에 주석이 달린 실호출**까지 걸러내기 때문이다.
정확한 판정은 `git diff` 로 **이번 편집이 추가/삭제한 줄**을 직접 보는 것이다.

## Self-Check: PASSED

1. 빌드 에러 0, 경고 코드 종류 2종뿐 ✅
2. `GetModelPathsForMask` 가 `GetShmPath`(폴더 생성)를 쓰지 않는다 ✅
3. 두 XAML 의 `RowDefinition` 수 무변경 (9 / 8) ✅
4. `RegenerateTeachSilent` 안에 `CustomMessageBox` 0건 ✅
5. `Matcher.TryTeach(` 호출이 각 파일에서 2회(기존 1 + 신규 1) ✅
