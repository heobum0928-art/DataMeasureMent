---
quick_id: 260423-lws
slug: datum-grab-loadimage-datumconfig
status: complete
date: 2026-04-23
---

# Quick Task 260423-lws: Datum 노드 Grab/LoadImage 지원 — SUMMARY

## 완료 내용

Datum 노드 선택 시 Grab/LoadImage 버튼이 비활성화되어 있던 버그 해소.
`DatumConfig`는 `ICameraParam` 미구현이라 기존 가드(`if (!(SelectedParam is ICameraParam)) return;`)에서 차단됐음.
`SourceShotName` 기반으로 `ShotConfig`를 조회해 `ICameraParam`으로 위임하는 경로 추가.

## 파일 변경

- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs`
  - `using System.Linq;` 추가 (FirstOrDefault용)
  - `InspectionList_SelectionChanged`: 진입부에 `button_loadImage.IsEnabled = false` 초기화 추가
  - `InspectionList_SelectionChanged`: `ENodeType.Datum` 블록 내에 `button_grab.IsEnabled = true`, `button_loadImage.IsEnabled = true` 추가
  - `button_grab_Click`: `SelectedParam is DatumConfig` 분기 추가 → `ResolveDatumCameraParam` 호출 → `GrabAndDisplay(resolved)` 위임
  - `button_loadImage_Click`: 동일 패턴 분기
  - `ResolveDatumCameraParam(DatumConfig)` helper 추가 — `SourceShotName` 매칭, 없으면 `Shots[0]` fallback, 빈 리스트면 null

## 검증

- `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` 빌드 성공
- 기존 경고 외 신규 경고 없음
- 런타임 UAT 대기

## UAT 체크리스트

- [ ] Datum 노드 선택 → button_grab, button_loadImage 모두 활성화
- [ ] Datum 노드 + Grab 클릭 → MainView 이미지 표시 (ShotConfig 위임됨)
- [ ] Datum 노드 + LoadImage 클릭 → 파일 다이얼로그 열림
- [ ] Shots 빈 레시피 → 조용히 return, 예외 없음
- [ ] ShotConfig(Action) 노드 기존 동작 유지
- [ ] 다른 노드(FAI/Measurement) 선택 시 두 버튼 비활성 정상 유지

## 비고

- `SourceShotName`이 빈 문자열이거나 매칭 실패 시 `Shots[0]` fallback (플랜의 원래 의도)
- `DatumConfig`에 `ICameraParam` 직접 구현은 하지 않음 — 데이터 모델 오염 방지, 조회 책임을 UI에 국한
