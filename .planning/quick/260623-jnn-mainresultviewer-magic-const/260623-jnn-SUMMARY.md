---
status: complete
---

# Quick 260623-jnn: MainResultViewer 매직넘버 const화 (QUAL-01 §5)

## 범위
Explore 스코프 분석 선행 → 안전(LOW) 범위만. 사용자 지시 "기능 100% 유지, 위험 높은 부분 회피" 반영.

MainResultViewerControl.xaml.cs (1934줄)는 이미 매직넘버 대부분 const화 상태라 안전 수확 적음. 명확·무위험 3개만 처리.

## 변경 (커밋 2bf6e49)
| const | 값 | 위치 | 의미 |
|---|---|---|---|
| `MinViewPartSize` | 20.0 | SetImagePart (1437-38) | 최소 뷰 파트 크기 |
| `PanMarginScale` | 0.75 | SetImagePart (1454-55) | 팬 마진 비율 |
| `PolygonMinVertices` | 3 | RenderNow 가드 (802) | 폴리곤 최소 정점 |

리터럴→const 1:1 치환만. 값·분기·계산 구조 불변.

## 의도적 회피 (위험)
- **HIGH**: RenderNow(139줄)/HMouseDown·Move·Up(117~142줄) 함수 분리 — 이벤트·마우스 상태 분기 엮임
- **HIGH**: RenderEditHandles `SetColor("yellow")` — SetColor 함정 영역
- 공개 API/이벤트 시그니처, Dispatcher 순서, HALCON 직접 호출
- **보류**: 다의적 리터럴 0.5(데드존/비율), 1.0(9곳), (0,0) 초기화 판정, line-width 2/색상 문자열

## 검증
- msbuild Debug/x64: 0 errors (DatumMeasurement.exe 생성, 경고만)
- 공개 API/HALCON/이벤트 diff 미등장 (3개 라인 + const 블록만)
