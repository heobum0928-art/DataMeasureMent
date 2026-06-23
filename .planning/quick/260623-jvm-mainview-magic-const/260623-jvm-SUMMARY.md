---
status: complete
---

# Quick 260623-jvm: MainView 매직넘버 const화 (QUAL-01 §5)

## 범위
Explore 스코프 분석 선행. MainView.xaml.cs (3097줄)는 프로젝트 함정 최다 파일(SetColor silent fail, airspace, Datum 좌표 휘발성, e.Source 비대칭) → 매우 보수적으로 "값 변경 없는 명확 매직넘버"만.

## 변경 (커밋 c97edcf)
| const | 값 | 위치 | 의미 |
|---|---|---|---|
| `MaxPolygonPoints` | 20 | PolygonMouseDown (2159) | 폴리곤 최대 정점 |
| `MinPolygonPoints` | 3 | RightClick/CompletePolygon (2174,2180) | 폴리곤 최소 정점 |
| `MinCalibrationPixelDistance` | 1.0 | FinishCalibration (2250) | 두 점 최소 픽셀 거리 |
| `MessageDisplaySeconds` | 3.0 | 캘리브 메시지 타이머 (2276) | 메시지 표시 지속(초) |

폴리곤 표시 문자열("20 / 20 pts MAX", "{0} / 20 pts")도 const 사용으로 통일하되 출력 텍스트 동일.

## 의도적 회피
- **부동소수점 위험**: `180.0 / Math.PI`(1337) — const화 시 `a*180/π` → `a*(180/π)` 연산순서 변경으로 마지막 ULP 달라질 수 있음 → 100% 동치 위해 제외
- **의미 어색**: 캘리브 `Count==1`/`==2`(단계 진행 체크) — const 부적절
- **HIGH 회피**: SetPolygonDraft "red"/"blue"(SetColor 함정), "#FF..." WPF 색상, 공개 API/이벤트 핸들러/Dispatcher, 117줄+ 함수(CommitRectRoi 등) 분리
- **보류**: `> 0` ROI 존재 비교(상태성, const 부적절)

## 검증
- msbuild Debug/x64: 0 errors (DatumMeasurement.exe 생성)
- 변경 = const 블록 + 6개 라인. 공개 API/HALCON/이벤트 diff 미등장.
