---
phase: 48-protocol-v1-test-result-site-material
plan: 04
subsystem: protocol
tags: [protocol-v1, material-number, xlsx-export, filename, hungarian, sentinel]
dependency_graph:
  requires:
    - 48-01 (TestPacket.IndexNumber, SENTINEL_NO_MATERIAL = -1)
  provides:
    - CycleResultDto.IndexNumber (xlsx 전파 DTO 필드)
    - BuildDto nIndexNumber 파라미터 (optional, 기본 -1)
    - InspectionSequence.AddResponse 자재번호 추출 + BuildDto 전달
    - CaptureImageSaveService.BuildFileName 7-인자 오버로드 (_M{번호})
    - FILENAME_NO_MATERIAL = -1 명명 상수
    - QueueFaiCapture 자재번호 파일명 전파
    - ExcelExportService 자재번호 메타 행(행 4), 테이블 헤더 오프셋 6
  affects:
    - CycleResultSerializer.BuildDto 시그니처 (optional param 추가 — 기존 호출부 4곳 호환)
    - ExcelExportService.Export xlsx 레이아웃 (행 5→6 이동)
tech_stack:
  added: []
  patterns:
    - optional 파라미터 끝 추가(기본 -1) — 기존 호출부 4곳 무수정 호환
    - 7-인자 오버로드 신설 — 기존 6-인자 보존으로 다른 호출부 회귀 0
    - Hungarian + if/else + 명명 상수(D-00) — 전파 경계 코드(BuildFileName 오버로드/QueueFaiCapture/AddResponse)
key_files:
  created: []
  modified:
    - WPF_Example/UI/ViewModel/CycleResultDto.cs
    - WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Utility/CaptureImageSaveService.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
    - WPF_Example/Custom/Export/ExcelExportService.cs
decisions:
  - "BuildFileName 자재번호 위치: FAI 뒤, seg 앞 (_M{번호} 토큰 고정) — 자재번호가 FAI와 가깝게 식별되도록 우선 배치"
  - "FILENAME_NO_MATERIAL = -1 명명 상수: private const int — 매직넘버 금지(D-00). 파일 scope(CaptureImageSaveService)"
  - "무수정 BuildDto 호출부 3곳(HandleManualCyclePersist/BatchRunService/RepeatRunService): optional 기본 -1 — 수동/배치/반복 경로는 자재번호 없음, 회귀 0"
  - "xlsx 테이블 헤더 오프셋 5→6: 자재번호 행(행 4) 삽입으로 1행 이동. hr 변수 참조로 테이블 데이터(row = hr + 1)도 자동 이동"
  - "RepeatExcelExportService 미포함: cycle export(ExcelExportService)만 자재번호 행 추가 — 범위 밖 명시"
  - "D-00 적용 범위: BuildFileName 오버로드·QueueFaiCapture 추출 블록·AddResponse 추출 블록 = 제어/저장 경계 코드. CycleResultDto/CycleResultSerializer/ExcelExportService = 기존 스타일 유지(자재번호 신규 분기는 if/else 권장으로 일관성 충족)"
metrics:
  duration: 25
  completed_date: "2026-06-22"
---

# Phase 48 Plan 04: 자재번호 결과 저장 전파 (IndexNumber → 파일명 + xlsx) Summary

**One-liner:** TestPacket.IndexNumber → AddResponse/BuildDto/CycleResultDto → xlsx "자재번호" 행(행 4) + QueueFaiCapture/BuildFileName → 파일명 `_M{번호}` 삽입 (미수신 -1 시 양쪽 모두 생략/"-")

## Build Result

msbuild Debug/x64 PASS — 0 errors, 0 new warnings. 기존 베이스라인 경고(CS0618 x5, CS0162 x1, MSB3884 x2) 유지.

## What Was Built

### Task 1: CycleResultDto.IndexNumber + BuildDto nIndexNumber + AddResponse 전파 (commit 17d0240)

**전파 경로 A (xlsx): TestPacket.IndexNumber → AddResponse → BuildDto → CycleResultDto → ExcelExportService**

**WPF_Example/UI/ViewModel/CycleResultDto.cs**
- `public int IndexNumber { get; set; } = -1;` 추가 — 자재번호 전파 DTO 필드. RecipeName 패턴과 동일.

**WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs**
- `BuildDto` 시그니처 끝에 `int nIndexNumber = -1` optional 파라미터 추가
- `dto` 초기화 블록에 `IndexNumber = nIndexNumber` 대입
- 기존 호출부 4곳 무수정 — optional 기본 -1로 컴파일 호환

**WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs** — `AddResponse()` 메서드
- `RequestPacket` null 체크: `bool bHasRequest = RequestPacket != null; if (bHasRequest) { nIndexNumber = ...; }` (D-00 Hungarian + if/else, `?.` 금지)
- `BuildDto(... Name, nIndexNumber)` 6번째 인자 전달

### Task 2: BuildFileName 자재번호 오버로드 + QueueFaiCapture 전파 (commit d51c6f8)

**전파 경로 B (파일명): QueueFaiCapture → RequestPacket.IndexNumber → BuildFileName(7-인자) → _M{번호}**

**WPF_Example/Utility/CaptureImageSaveService.cs**
- `private const int FILENAME_NO_MATERIAL = -1;` 명명 상수 (D-00 매직넘버 금지)
- `BuildFileName(..., DateTime ts, int nIndexNumber)` 7-인자 오버로드 신설
  - `bool bHasMaterial = nIndexNumber > FILENAME_NO_MATERIAL; if (bHasMaterial) { szMat = ...; }`
  - `_M{자재번호}` 토큰: FAI 뒤, seg 앞 순서
- 기존 6-인자 `BuildFileName` 무수정 보존

**WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs** — `QueueFaiCapture()` 메서드
- 부모 시퀀스 추출: `ShotParam != null ? ... as InspectionSequence : null` → `if/else` 분리 (D-00 `?.` 금지)
- `bool bHasRequest = parentSeq != null && parentSeq.RequestPacket != null;` 조건 변수화
- `if (bHasRequest) { nIndexNumber = parentSeq.RequestPacket.IndexNumber; }` 추출
- `BuildFileName("origin", ..., ts, nIndexNumber)` / `BuildFileName("capture", ..., ts, nIndexNumber)` 7-인자 호출

### Task 3: xlsx export 자재번호 메타 헤더 행 추가 (commit 97739d9)

**WPF_Example/Custom/Export/ExcelExportService.cs** — `Export()` 메서드
- `ws.Cell(4, 1).Value = "자재번호";` 신규 행
- `if (cycle.IndexNumber >= 0) { ws.Cell(4, 2).Value = cycle.IndexNumber; } else { ws.Cell(4, 2).Value = "-"; }` (D-00 if/else 권장 준수)
- `int hr = 6;` — 자재번호 행 삽입에 따른 오프셋 조정 (5→6). 테이블 헤더 배열·데이터 루프 무변경.

## 전파 경로 추적

```
TestPacket.IndexNumber (Plan 01 산출, SENTINEL = -1)
  ↓  SequenceBase.RequestPacket (Start/StartAll 에서 set)
  ↓
[경로 A: xlsx]
  InspectionSequence.AddResponse (bHasRequest → nIndexNumber 추출)
    → CycleResultSerializer.BuildDto(nIndexNumber)
      → CycleResultDto.IndexNumber
        → ExcelExportService.Export: ws.Cell(4) "자재번호" 행

[경로 B: 파일명]
  Action_FAIMeasurement.QueueFaiCapture (bHasRequest → nIndexNumber 추출)
    → CaptureImageSaveService.BuildFileName(..., nIndexNumber)
      → 파일명 origin_TOP_FAI_A1_M42_P1P2_OK_153012345.jpg
```

## 미수신(-1) 동작 검증

| 조건 | 경로 A (xlsx) | 경로 B (파일명) |
|------|-------------|----------------|
| IndexNumber = 42 (수신) | ws.Cell(4,2) = 42 | `_M42` 포함 |
| IndexNumber = -1 (미수신) | ws.Cell(4,2) = "-" | `_M` 없음 (생략) |
| RequestPacket = null (수동/배치/반복) | BuildDto optional -1 → "-" | nIndexNumber = -1 → 생략 |

## D-00 준수 확인

| 규칙 | 상태 |
|------|------|
| 헝가리언 접두사 (b/n/sz) | PASS — bHasRequest/nIndexNumber/bHasMaterial/szMat/parentSeq 모두 적용 |
| if/else only (삼항 ?:/null병합 ?? /null조건 ?.  0건) | PASS — 신규 제어 경계 코드 전체 검증 |
| 조건식 bool 변수화 | PASS — bHasRequest/bHasMaterial 명시 변수화 |
| 매직넘버 금지 | PASS — FILENAME_NO_MATERIAL = -1 명명 상수 |
| 기존 스타일 코드(ExcelExportService) | 자재번호 신규 분기 if/else — 일관성 충족 |

## Deviations from Plan

없음 — 계획대로 실행.

## Known Stubs

없음.

## Threat Flags

T-48-10/T-48-11 미티게이션 확인:
- T-48-10 (Tampering/파일명): `nIndexNumber`는 int (Plan 01에서 비정수→-1 정규화) → `_M{int}` 만 삽입. `/`, `\`, `..` 불가. 기존 SanitizeFilePart도 다른 세그먼트(seq/fai)에 유지.
- T-48-11 (xlsx 수식 주입): `cycle.IndexNumber`는 int → `ws.Cell.Value`에 정수 대입. 수식 문자 불가. 미수신 시 리터럴 `"-"`.

## Self-Check: PASSED

- WPF_Example/UI/ViewModel/CycleResultDto.cs — IndexNumber 필드 존재 확인
- WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs — nIndexNumber 파라미터 + IndexNumber 대입 확인
- WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs — bHasRequest + RequestPacket.IndexNumber 추출 확인
- WPF_Example/Utility/CaptureImageSaveService.cs — FILENAME_NO_MATERIAL 상수 + 7-인자 오버로드 확인
- WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs — QueueFaiCapture nIndexNumber 추출 + 7-인자 호출 확인
- WPF_Example/Custom/Export/ExcelExportService.cs — "자재번호" 셀(행 4) + int hr = 6 확인
- commit 17d0240 존재 확인
- commit d51c6f8 존재 확인
- commit 97739d9 존재 확인
- msbuild Debug/x64 0 errors, 0 new warnings PASS
