# Phase 74 — CONTEXT (discuss 완료 2026-08-27)

패턴 모델 생성 시 **원하지 않는 영역을 브러시로 칠해 제외**한다. **옵션** — 끄면 기존과 동일(회귀 0).

---

## D-74-01. 브러시 입력 = 드래그로 칠하기

**결정:** 진짜 붓처럼 **누른 채 끌면 지나간 자리가 칠해진다.**

**필요한 것:** 뷰어에 **마우스 이동/놓기 이벤트 신설**.
현재 `MainResultViewerControl` 은 `ImageLeftClicked` / `ImageRightClicked` 두 개만 노출한다
(`MainResultViewerControl.xaml.cs:176~177`).

**위험 낮음 근거:** 같은 컨트롤에서 클릭 기반 상호작용이 이미 3종 동작 중이다 —
`HalconViewer_PolygonMouseDown`(`MainView.xaml.cs:3053`), `_MeasureMouseDown`, `_CalibrationMouseDown`.
이벤트 배선·좌표 변환·구독 해제 패턴을 그대로 따르면 된다.

**구현:** 마우스 이동마다 그 좌표에 **원(circle) 영역**을 만들어 누적(`Union`).
지우개 모드는 누적분에서 뺀다(`Difference`).

---

## D-74-02. 마스크 저장 = HALCON region 파일

**결정:** 모델 파일(`.shm`/`.ncm`) **옆에 마스크 파일**을 나란히 둔다.

**근거:** 어떤 모양이든 그대로 보존되고 용량이 작다. 모델과 짝이라 관리가 자연스럽다.
INI 좌표 직렬화는 자국이 많아지면 파일이 커지고 복잡한 모양이 정확히 복원 안 될 수 있다.

**⚠ 경로 규약 주의:** 모델 경로는 `RecipeFileHelper.GetPatternModelFilePath(recipe, seqName, ...)` 로
만들어지고, **없는 폴더를 `Directory.CreateDirectory` 로 새로 만들어버린다**(`RecipeFileHelper.cs:103`).
Phase 73 에서 이것 때문에 `.shm` 을 조용히 못 찾는 사고가 날 뻔했다(M7 `NormalizeModelFolderName` 으로 방어).
**마스크 파일도 같은 헬퍼를 써서 모델과 반드시 같은 폴더에 떨어지게 할 것.**

**고아 파일 정리:** 모델이 지워지거나 이름이 바뀌면 마스크도 따라가야 한다.
과거 고아 `.shm` 이 남은 이력이 있다(`project_teaching_audit_260710`).

---

## D-74-03. 적용 범위 = Align + Datum 둘 다

**결정:** `PatternMatchService.TryCreateModel`(`PatternMatchService.cs:134`) **한 곳만 고친다.**

이 함수가 **단일 진입점**이라 자동으로 양쪽에 적용된다:
- `AlignShapeMatchService.cs:376/386` — Align 패턴 1/2 (Tray/Bottom Align)
- `MainView.xaml.cs:4059/4081` — 패턴 모델 생성 버튼 (Datum 티칭)

**Align 만 적용하려면 오히려 분기를 추가해야 해서 코드가 는다.**
옵션이 꺼져 있으면 어느 쪽도 영향이 없으므로 범위를 좁힐 실익이 없다.

**적용 지점:** `TryCreateModel` 내부의 `GenRectangle2` → `ReduceDomain` 사이
(`PatternMatchService.cs:167~168`). ROI 사각형에서 마스크를 `Difference` 로 뺀 뒤 `ReduceDomain` 에 넘긴다.
`CreateShapeModel`/`CreateNccModel` 호출부는 **무변경**.

---

## D-74-04. 마스크 변경 시 모델 자동 재생성

**결정:** 마스크를 고치면 **모델을 즉시 다시 만든다.**

**근거:** 마스크와 모델이 항상 일치한다. 수동이면 "고쳤는데 왜 그대로지" 혼란이 생기고,
마스크만 바뀐 채 옛 모델이 남아 조용히 잘못 매칭될 수 있다.

**주의:** 재생성은 파일 쓰기다. 브러시 자국 하나마다 재생성하면 느리다 →
**칠하기가 끝난 시점**(마우스 놓기 또는 [적용])에 한 번만 재생성할 것.

---

## UI 배치 — airspace 제약

`HWindowControlWPF` 는 HWND 라 **그 위에 얹은 WPF 요소가 가려진다**(이 프로젝트 기확인,
`feedback_halcon_hwnd_airspace`).

| 요소 | 위치 |
|---|---|
| 브러시 자국 · 미리보기 | **HALCON 창 안** (`DispRegion`, 반투명) |
| 브러시 크기 / 칠하기·지우개 / 초기화 / 옵션 토글 | **창 밖 사이드 패널** |

**UI 는 MVVM.** `MainView.xaml.cs`(4,300줄)에 새 로직을 넣지 말 것 — 새 ViewModel 로 분리한다.
단 이번 phase 는 리팩토링이 목적이 아니므로 **이번에 손대는 지점에만** 적용하고
나머지 기존 code-behind 는 그대로 둔다.

---

## 기반 (조사 완료 — 신규 구축 최소)

| 항목 | 위치 |
|---|---|
| 모델 생성 단일 진입점 | `PatternMatchService.cs:134` `TryCreateModel` |
| ROI → ReduceDomain | `PatternMatchService.cs:167~168` |
| 모델 저장 | `:184` `WriteNccModel` / `:205` `WriteShapeModel` |
| `ReduceDomain` 선례 8곳 | `PickerCenterCalibrationService.cs:134,246` · `DatumFindingService.cs:1749,1759,2017,2027` · `CheckerboardCalibrationService.cs:83` |
| 클릭 처리 선례 3종 | `MainView.xaml.cs:3053` 외 |
| 모델 경로 헬퍼 | `RecipeFileHelper.GetPatternModelFilePath` |

---

## 선행 확인 (착수 전)

**Bottom Align 캘리브레이션을 먼저 돌려 노이즈 수준을 본다.**
영상이 깨끗하면 이 기능을 켤 일이 없을 수도 있다. 다만 **옵션이라 만들어 두는 것 자체는 무해**하다.

---

## 미결 (plan 에서 확정)

- 브러시 크기 범위와 기본값 (px 단위, 화면 배율과의 관계)
- 마스크 파일 확장자·명명 규약
- 옵션 토글의 저장 위치 (레시피 vs 시스템 설정)
- 마스크가 있는 모델을 UI 에서 어떻게 표시할지(아이콘/텍스트)

---

## 코딩 규칙

삼항 `?:` / `??` / `?.` 금지 · 전통 `switch` 만(C# 8.0 switch expression 금지) · C# 7.2 ·
긴 조건은 이름 있는 `bool` 로 선추출 · 헝가리언(b/n/sz) · 파일별 중괄호 스타일 유지 ·
날짜 주석 신규 금지 · **UI 는 MVVM** · 빌드 경고 baseline 준수("경고 0" 아님) ·
실행 중 프로세스 종료 금지 · `DatumMeasurement.csproj` 커밋 금지 · **HImage/HRegion Dispose**
