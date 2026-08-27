# Phase 74 — SEED

**패턴 모델 생성 시 브러시 마스킹 (옵션)**
신설 2026-08-27 · Bottom Align 캘리브레이션 노이즈 대응

## 발단

사용자 제안(2026-08-27): Bottom Align 캘리브레이션 착수 예정인데,
*"영상이 깔끔하게 나오면 괜찮은데 노이즈 자체가 많이 나오게 되면 마스킹 처리가 필요할 수도 있다.
모델 딸 때 원하는 부분만 따게 붓 브러쉬 같은 걸로 제거하고 모델을 등록하는 것"*

## 목표

Shape/NCC 모델 등록 전에 **제외할 영역을 브러시로 칠해** 모델에서 빼낸다.
**옵션 기능** — 토글 off 면 기존 경로와 완전히 동일(회귀 0).

## 기반 (조사 완료 — 신규 구축 최소)

| 기능 | 위치 |
|---|---|
| `ReduceDomain` | `PatternMatchService.cs:168` · `PickerCenterCalibrationService.cs:134,246` · `DatumFindingService.cs:1749,1759,2017,2027` · `CheckerboardCalibrationService.cs:83` |
| 모델 생성 | `PatternMatchService.cs:175`(NCC) `:190`(Shape) · `PickerCenterCalibrationService.cs:137`(Shape) |
| 마우스 처리 선례 | `MainView.xaml.cs:3053` `HalconViewer_PolygonMouseDown` (+ Measure/Calibration 2종), 이벤트 `ImageLeftClicked` |

폴리곤 ROI 를 마우스로 찍는 코드가 이미 동작한다 — 브러시는 그 확장.

## 동작

```
모델 ROI 지정
  → 브러시로 제외 영역 칠하기 (원 영역 누적)
  → ROI 에서 Difference (지우개는 Union)
  → ReduceDomain
  → CreateShapeModel / CreateNccModel
```

## ⚠ UI 제약 (이 프로젝트 기확인)

`HWindowControlWPF` 는 HWND 라 **그 위에 얹은 WPF 요소가 airspace 로 가려진다.**

| 요소 | 위치 |
|---|---|
| 브러시 자국·미리보기 | **HALCON 창 안** (`DispRegion`) |
| 브러시 크기 / 모드 / 초기화 | **창 밖 사이드 패널** |

## 범위

1. 브러시 영역 누적 (마우스 이동 + 원 Union/Difference)
2. HALCON 창 내부 반투명 오버레이 실시간 표시
3. 모델 생성 경로에 마스크 반영
4. **마스크 레시피 저장/복원** — 없으면 재생성 때마다 다시 칠해야 함
5. 옵션 토글 (기본 off)
6. UI 패널 (브러시 크기 / 모드 / 초기화 / 미리보기)

## 선행 확인

**Bottom Align 캘리브레이션을 먼저 돌려 노이즈 수준을 본다.**
영상이 깨끗하면 이 phase 자체가 불필요할 수 있다.

## 미결 (discuss 에서 확정)

- 마스크 저장 형식 — HALCON `write_region` 파일 vs INI 좌표 직렬화
- 적용 범위 — Align 모델만인지, 검사용 Datum 패턴(`PatternMatchService`)까지인지
- 브러시 외 다른 도구(사각형 제외, 자동 임계값 제외) 필요 여부
- 기존 모델에 마스크를 나중에 추가할 때 재생성 정책

## 코딩 규칙

삼항 `?:` / `??` / `?.` 금지 · 전통 `switch` 만(C# 8.0 switch expression 금지) · C# 7.2 ·
헝가리언(b/n/sz) · 파일별 중괄호 스타일 유지 · 날짜 주석 신규 금지 ·
**UI 는 MVVM** (`MainView.xaml.cs` 에 새 로직 추가 금지 — 새 ViewModel) ·
빌드 경고 baseline 준수("경고 0" 아님) · 실행 중 프로세스 종료 금지 ·
`DatumMeasurement.csproj` 커밋 금지
