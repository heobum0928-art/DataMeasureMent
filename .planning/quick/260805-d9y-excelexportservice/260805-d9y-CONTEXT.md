# Quick Task 260805-d9y: 일반 검사 엑셀(ExcelExportService)에 캡쳐 이미지 셀 삽입 - Context

**Gathered:** 2026-08-05
**Status:** Ready for planning

<domain>
## Task Boundary

`ExcelExportService.cs`(일반 검사 결과 xlsx export)에 "캡쳐이미지 경로" 텍스트 컬럼 뒤에 실제 캡쳐 이미지(오버레이 렌더링된 결과 이미지)를 셀에 첨부하는 컬럼을 추가한다. `RepeatExcelExportService.cs`(반복검사/Gage R&R 통계 export)는 이번 범위에서 명시적으로 제외한다.

</domain>

<decisions>
## Implementation Decisions

### 컬럼 구성 및 범위
- 기존 "원본이미지 경로"(텍스트), "캡쳐이미지 경로"(텍스트) 컬럼은 그대로 유지한다. 순서: 원본이미지 경로 → 캡쳐이미지 경로 → (신규) 캡쳐이미지 첨부.
- 그 뒤에 새 컬럼을 추가해 캡쳐이미지 경로가 가리키는 JPG 파일을 실제로 셀에 이미지로 삽입한다. 이미 참조 중인 ClosedXML 0.105.0으로 충분하며(AddPicture 계열 API 보유), 새 라이브러리 도입은 불필요/금지.
- `RepeatExcelExportService.cs`는 이번 작업에서 손대지 않는다 — 그 쪽은 집계 전용 리포트(측정항목당 N회 통계 1행)라 회차별 이미지를 넣을 자연스러운 자리가 없다는 논의 끝에 범위에서 제외하기로 확정됨.

### 비동기 파일쓰기 레이스 처리
- `CaptureImageSaveService`가 캡쳐 JPG를 백그라운드 워커 스레드에서 비동기로 저장한다(경로 문자열 자체는 `Action_FAIMeasurement.cs`의 `QueueFaiCapture`에서 동기로 먼저 확정됨). 따라서 export 시점에 파일이 아직 디스크에 없을 수 있다(검증된 사실 — flush/wait 지점이 현재 코드에 전혀 없음).
- 처리 방식: 이미지 삽입 전 `File.Exists`를 짧게 폴링하며 대기(예: 최대 1~2초, 100ms 간격). 타임아웃 내 파일이 나타나지 않으면 해당 셀은 빈 칸으로 두고 경고 로그(`Logging.PrintErrLog` 등 기존 패턴)를 남긴다. Export 자체를 실패시키면 안 된다 — 최선을 다해 채우고 없으면 빈 칸.

### Claude's Discretion
- 정확한 폴링 간격/타임아웃 값(1~2초 범위 내), 이미지 셀 크기(스케일)/행 높이 조정 방식은 구현 재량.
- 기존 `ws.Columns().AdjustToContents()` 호출과 이미지 삽입이 서로 방해되지 않도록 처리 순서는 구현 재량.

</decisions>

<specifics>
## Specific Ideas

- 헤더/데이터 채우는 위치: `WPF_Example\Custom\Export\ExcelExportService.cs` — 헤더 배열(6행 부근), Shot→FAI→Measurement 3중 루프로 데이터를 채우는 부분(원본/캡쳐 경로는 각각 약 114~115행 부근에서 텍스트로 기록됨).
- 캡쳐이미지 파일이 실제로 쓰여지는 경로: `WPF_Example\Utility\CaptureImageSaveService.cs`(전용 워커 스레드, `ConcurrentQueue`), 큐잉 트리거는 `WPF_Example\Custom\Sequence\Inspection\Action_FAIMeasurement.cs`의 `QueueFaiCapture`(847~909행 부근)에서 파일명/경로를 동기 확정 후 Enqueue.
- 원본이미지와 캡쳐이미지는 서로 다른 두 종류뿐이다(원본 = 오버레이 없는 raw grab, 캡쳐 = 오버레이 입힌 결과 이미지) — 별도의 "측정 이미지"라는 제3의 이미지는 없다. 셀에 삽입할 대상은 캡쳐이미지(오버레이 포함 결과 시각화본)다.

</specifics>

<canonical_refs>
## Canonical References

No external specs — requirements fully captured in decisions above.

</canonical_refs>
