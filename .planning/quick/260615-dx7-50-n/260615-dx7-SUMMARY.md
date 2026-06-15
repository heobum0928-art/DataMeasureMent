---
quick_id: 260615-dx7
type: quick
status: complete
date: 2026-06-15
commit: 18a656e
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs
  - WPF_Example/UI/Reviewer/ReviewerWindow.xaml
  - WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs
---

# Quick 260615-dx7 — 반복 검사 입력: 고정 50회 → 이미지 폴더 N장 순회

## 배경
Phase 41.1(반복도/알고리즘 통계 xlsx) UAT 직전 발견된 요구 변경. 기존 "고정 이미지 50회 반복"은
같은 이미지를 50번 돌려 알고리즘 반복도만 측정. 실사용은 "자재를 N번 촬영(이미지 N장) → 그만큼 N회 검사"가
맞다고 확정. 1 사이클 = 이미지 1장(단일 Shot), 횟수 = 폴더 이미지 개수(가변, 50 고정 폐기).

## 변경
- **RepeatRunService.cs**
  - `StartFromImages(InspectionSequence seq, List<string> imagePaths)` 신규 — `TargetCount = imagePaths.Count`
  - `ApplyCurrentImage()` — 매 사이클 StartAll 직전 `recipeManager.Shots` 전체의 `SimulImagePath` 를
    `imagePaths[CompletedCount]` 로 교체 (인덱스 = 완료횟수 자동 동기화)
  - `_imagePaths == null` 분기로 기존 `Start()` 고정모드 무손상 (하위호환)
  - `Stop()` 에서 `_imagePaths = null` 정리
- **ReviewerWindow.xaml** — 버튼 텍스트 "50회 반복 실행" → "이미지 폴더 반복 검사"
- **ReviewerWindow.xaml.cs** — `Button_RepeatRun_Click` 재작성: Ookii 폴더 다이얼로그 →
  `.bmp/.jpg/.jpeg/.png/.tif/.tiff` OrdinalIgnoreCase 정렬 수집 → 0장 가드 → `StartFromImages` 호출.
  진행 레이블/버튼 텍스트 폴더 모드 기준으로 갱신. "50회" 문자열 잔재 제거.

## 검증
- msbuild Debug/x64 Build: **0 errors** (exit 0)
- grep: StartFromImages/ApplyCurrentImage/VistaFolderBrowserDialog 존재, "50회" 잔재 0
- 통계(RepeatMeasurementStats) / xlsx export(RepeatExcelExportService) 무변경 — 입력 소스만 교체

## 잔여 (UAT 필요)
SIMUL 앱 실행 후 육안 확인:
- [결과 리뷰어] → "이미지 폴더 반복 검사" → 이미지 폴더 선택
- 진행 레이블 "진행 중: N/<이미지수>" 갱신, 매 사이클 다른 이미지 로드 확인
- 완료 후 "반복도 엑셀 export" → 2시트 xlsx 정상 (이미지별 행 누적)
- 회귀: 폴더 모드가 통계/엑셀 기존 출력 형식 유지
