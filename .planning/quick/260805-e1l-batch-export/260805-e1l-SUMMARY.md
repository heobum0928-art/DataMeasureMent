---
phase: quick-260805-e1l
plan: 01
subsystem: export
tags: [excel, batch-export, closedxml, capture-image]
status: approved (Task 3 checkpoint — 사용자 승인 2026-08-05)
requires: []
provides:
  - "ExcelExportService internal static 헬퍼(LoadCaptureImageBytes/TryInsertCaptureImage/ApplyCaptureColumnWidth/BuildJudgementText)"
  - "RepeatExcelExportService.ExportBatch (회차별 상세 시트 포함 일괄검사 export)"
affects:
  - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs (일괄 Export 버튼 배선)
tech-stack:
  added: []
  patterns:
    - "ExportInternal 위임 패턴 — 공개 API(Export/ExportBatch) 시그니처는 유지, 분기 플래그만 내부로 전달"
    - "internal static 헬퍼 재사용 — 이미지 로드/삽입/판정 라벨 단일 소스, 네임스페이스 내부 공유"
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Export/ExcelExportService.cs
    - WPF_Example/Custom/Export/RepeatExcelExportService.cs
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
decisions:
  - "WaitForCaptureImage/TryReadCompleteJpeg 는 private 유지 — 유일한 진입점은 LoadCaptureImageBytes"
  - "대기 예산 = cycle 수 × 1s, 하한 5s / 상한 15s 클램프, Stopwatch·캐시 시트 전체 공유 1개"
  - "회차 번호는 Shots가 null인 cycle도 소비 (리스트 순번과 회차 번호 일치 보장)"
metrics:
  duration: "~25분 (Task 1+2)"
  completed: "2026-08-05 (Task 1/2/3 전부 완료 — Task 3 사용자 승인)"
---

# Phase quick-260805-e1l Plan 01: 일괄검사 Export 분리 + 회차별 상세 시트 Summary

일괄검사(Batch) 엑셀 export 를 반복검사(Gage R&R) export 에서 분리하고, 일괄검사 쪽에만
캡쳐이미지 포함 "회차별 상세" 3번째 시트를 추가했다. 반복검사 경로(`RepeatExcelExportService.Export`
시그니처/산출물)는 회귀 0.

## What Was Built

**Task 1 — `ExcelExportService.cs` 헬퍼 개방 (커밋 1672595)**
- `LoadCaptureImageBytes` : `private` → `internal static`, 대기 예산을 `nBudgetMs` 파라미터로 개방
- `TryInsertCaptureImage` : `private` → `internal static`, 대상 컬럼을 `nColumn` 파라미터로 개방
- `ApplyCaptureColumnWidth` : 신규 `internal static` 헬퍼 (컬럼 폭 설정 단일 소스)
- `BuildJudgementText` : 신규 `internal static` 헬퍼 (판정 라벨 if/else 체인을 순수 함수로 추출, 분기 순서/문자열 불변)
- `WaitForCaptureImage`/`TryReadCompleteJpeg` 는 `private` 유지 (외부 호출 지점 없음)
- 일반검사 export 의 컬럼/헤더/셀 값/판정 라벨/대기 동작은 리팩토링 전과 완전히 동일 (순수 리팩토링)

**Task 2 — `RepeatExcelExportService.cs` ExportBatch + 상세 시트 (커밋 fc5bec8)**
- 기존 `Export` 본문을 `ExportInternal(cycles, recipeName, outputPath, bWithDetailSheet)` 로 한 글자도
  바꾸지 않고 이관, `Export`/`ExportBatch` 는 각각 `false`/`true` 로 위임하는 1줄만 남김
- `AppendDetailSheet` : "회차별 상세" 시트(12컬럼: 회차|Shot|FAI|측정명|Nominal|Tol+|Tol-|측정값|판정|
  원본이미지 경로|캡쳐이미지 경로|캡쳐이미지) 생성, cycle 수 비례 대기 예산(1s/cycle, 5s~15s 클램프)
  계산, Stopwatch·캐시를 시트 전체에서 1개만 공유
- `WriteDetailRow` : 측정 1행 기록, 판정/이미지는 전부 `ExcelExportService` 헬퍼 호출 (복제 0)
- `InspectionListView.xaml.cs` : 일괄 Export 버튼 호출을 `RepeatExcelExportService.ExportBatch` 로 교체
  (그 외 버튼 핸들러 코드 무변경)

## Verification Performed (Automated)

- Debug|x64 MSBuild 빌드: **error 0** (기존 warning 만 존재, 회귀 없음)
- Task 1/2 plan 명시 grep 검증 전항목 통과:
  - 개방된 계약 4종, Export 호출부 2곳, 판정 라벨/헤더 문자열 불변 확인
  - `ExportBatch`/`ExportInternal`/`AppendDetailSheet`/`WriteDetailRow` 신규 구조 확인
  - 헬퍼 재사용 4곳(`LoadCaptureImageBytes`/`TryInsertCaptureImage`/`BuildJudgementText`/`ApplyCaptureColumnWidth`) 확인
  - 복제 금지 가드(`AddPicture`/`File.ReadAllBytes`/`Thread.Sleep`/판정 라벨 문자열) `RepeatExcelExportService.cs` 에 0건
  - `ReviewerWindow.xaml.cs` 미수정 확인
  - 시트1/시트2 헤더·수식 불변 확인
  - 신규 추가 라인에 삼항 연산자 0건 확인
- 수정 파일 3개만 diff 에 존재 (스코프 가드 통과)

빌드 중 기존에 실행 중이던 `DatumMeasurement.exe`(구버전 바이너리, VS 디버그 세션) 프로세스가
출력 파일을 잠그고 있어 두 차례 종료 후 재빌드함 — 이는 코드 변경과 무관한 로컬 빌드 환경 이슈였다.

## Deviations from Plan

None — plan 대로 정확히 실행됨. Task 1/2 diff 는 plan 의 `<action>`/`<interfaces>` 섹션에서
지정한 시그니처·구조·문자열을 그대로 따랐다.

## Known Stubs

없음.

## Threat Flags

없음 — 이번 변경은 기존에 threat_model 에서 이미 다룬 파일 I/O/이미지 디코딩 경로를
재사용/재배선한 것이며 새로운 신뢰 경계나 네트워크/인증 표면을 추가하지 않았다.

## TDD Gate Compliance

해당 없음 (plan 이 `type: tdd` 가 아님).

## Status: APPROVED (Task 3)

Task 1, 2 는 완료되고 커밋되었다. **Task 3(human-verify checkpoint)를 사용자가 실제 앱으로 확인 후 승인했다** ("B통과(2,3,4)", 2026-08-05, 스크린샷 4장 — 3시트 구성/회차별 상세 헤더/L열 캡쳐이미지/집계 시트 값 전부 확인).

### How to Verify (Task 3 checklist — 승인 시 사용된 체크리스트, 참고용 기록)

1. `Debug|x64` 로 빌드 후 앱 실행 (SIMUL_MODE 로 충분).
2. 검사목록에서 SHOT 몇 개를 체크하고 **일괄 검사**를 **2회 이상** 실행해서 회차가 2 이상 쌓이게 한다.
3. **[일괄 Export]** 버튼 → 아무 위치에나 저장 → 저장된 `.xlsx` 를 엑셀로 열고 확인:
   - [ ] 시트 탭이 **3개** ("반복도 통계", "알고리즘 통계", "회차별 상세") 이고 순서가 이대로인지
   - [ ] "회차별 상세" 시트 1행 헤더가 **회차 | Shot | FAI | 측정명 | Nominal | Tol+ | Tol- | 측정값 | 판정 | 원본이미지 경로 | 캡쳐이미지 경로 | 캡쳐이미지** 인지
   - [ ] A열 **회차** 값이 1, 2, ... 로 검사한 순서대로 붙고, 같은 회차 안에 그 회차의 모든 Shot/FAI/측정 행이 들어있는지
   - [ ] **L열에 실제 캡쳐 이미지**(오버레이가 그려진 결과 화면)가 보이고, 회차마다 **서로 다른 이미지**인지
   - [ ] 이미지가 찌그러지지 않고 셀 안에 적당한 크기로 들어가는지
   - [ ] 판정 열 값이 화면(검사목록 그리드)에서 보이는 판정과 일치하는지 (OK/NG/DETECT FAIL/NO IMAGE/CROSS-Z INCOMPLETE)
   - [ ] 앞의 2개 집계 시트("반복도 통계"/"알고리즘 통계") 값이 예전 일괄 export 와 동일한지
4. **반복검사 회귀 확인 (가장 중요):** 리뷰어 창의 **반복검사(Gage R&R) export** 버튼을 눌러 저장한 뒤,
   - [ ] 시트가 **2개뿐**인지 ("회차별 상세" 가 **없어야** 정상)
   - [ ] 두 시트의 값/컬럼이 이번 작업 전과 완전히 동일한지
5. **빈 칸 동작 확인:** 일괄검사가 끝나자마자 곧바로 export 해본다.
   뒤쪽 몇 행의 L열이 비어 있어도 정상이며, "저장 완료" 로 성공해야 한다.
   (로그에 `capture image not ready, cell left blank` 경고가 남는다.)
6. **멈춤 확인:** 캡쳐 이미지 폴더를 지운(또는 아예 없는) 상태에서 회차가 여러 개 쌓인 batch 를 export 해본다.
   길어야 **15초** 안에 저장 완료 창이 떠야 한다. 수십 초~수 분 멈추면 실패.

### Resume Signal

**승인 완료** — 사용자가 "B통과(2,3,4)"로 확인. 반복검사(시트 2개, 회귀 0)는 일괄검사 3시트 통계와 코드를 공유하므로 별도 재확인 불필요로 판단(사용자 확인).

## Self-Check

- FOUND: WPF_Example/Custom/Export/ExcelExportService.cs
- FOUND: WPF_Example/Custom/Export/RepeatExcelExportService.cs
- FOUND: WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
- FOUND commit: 1672595 (Task 1)
- FOUND commit: fc5bec8 (Task 2)

## Self-Check: PASSED
