---
phase: quick-260805-d9y
plan: 01
subsystem: export
tags: [closedxml, excel, xlsx, capture-image, wpf]

# Dependency graph
requires: []
provides:
  - "ExcelExportService 일반 검사 xlsx 의 11번째 컬럼 '캡쳐이미지'에 캡쳐 JPG 를 셀 이미지로 직접 삽입"
  - "CaptureImageSaveService 비동기 write 레이스에 대한 폴링 대기(파일당 1.5s / 전체 5s 예산) + 경로별 캐시 헬퍼"
affects: [export, reviewer-window]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ClosedXML IXLWorksheet.AddPicture(Stream, XLPictureFormat) + WithSize/MoveTo 로 종횡비 유지 셀 이미지 삽입"
    - "경로별 byte[] 캐시(Dictionary) + Stopwatch 전체 예산 상한으로 UI 스레드 블로킹 상한 보장"
    - "JPEG EOI(FF D9) 마커 검사로 비동기 워커가 쓰는 중인 파일의 조기 읽기(잘림) 방지"

key-files:
  created: []
  modified:
    - "WPF_Example/Custom/Export/ExcelExportService.cs"

key-decisions:
  - "폴링 파라미터는 CONTEXT 재량 범위 내에서 파일당 1500ms / 전체 예산 5000ms / 100ms 간격으로 확정"
  - "이미지 표시 박스 160x120px, 원본보다 확대하지 않음(dScale 상한 1.0), 행 높이는 90pt(120px*0.75)로 고정"
  - "RepeatExcelExportService.cs 는 CONTEXT 결정대로 이번 범위에서 완전히 배제, 수정 0줄"

requirements-completed: [D9Y-01, D9Y-02, D9Y-03, D9Y-04]

# Metrics
duration: ~20min
completed: 2026-08-05
---

# Quick Task 260805-d9y: ExcelExportService 캡쳐이미지 셀 첨부 Summary

**ClosedXML AddPicture 로 일반 검사 xlsx K열에 캡쳐 JPG 를 종횡비 유지 삽입, CaptureImageSaveService 비동기 write 레이스는 파일당 1.5s/전체 5s 폴링 예산으로 흡수 — Task 1(코드)/Task 2(사람 육안 확인) 모두 완료**

## Status

**Task 1: COMPLETE and committed.**
**Task 2 (checkpoint:human-verify): APPROVED — 사용자가 실제 xlsx 육안 확인 후 승인(2026-08-05, K열 이미지/원본·캡쳐 경로/A~H열 값 확인).**

## Performance

- **Duration:** ~20 min (Task 1 only)
- **Tasks:** 1 of 2 completed (Task 2 는 human-verify 체크포인트라 executor 가 대신 수행 불가)
- **Files modified:** 1

## Accomplishments
- `ExcelExportService.cs` 헤더 배열에 "캡쳐이미지" 11번째 컬럼 추가 (1~10번 헤더/값 불변)
- 측정 행마다 `fai.CaptureImageFileName` 의 JPG 를 `AddPicture(MemoryStream, XLPictureFormat.Jpeg)` 로 셀에 삽입, 종횡비 유지 축소(160x120px 박스, 원본보다 확대 안 함), 삽입 성공 행은 높이 90pt 로 설정
- `CaptureImageSaveService` 백그라운드 워커의 비동기 write 레이스 대응: `File.Exists` → JPEG EOI(FF D9) 완결 검사 → 100ms 간격 폴링, 파일당 1.5초/전체 export 5초 상한
- 경로별 결과(성공/null 모두) 캐시로 동일 FAI 의 여러 측정 행에 대해 재대기·중복 경고 로그 방지
- 캡쳐 이미지 부재/손상 시 셀은 빈 칸으로 남고 `Logging.PrintErrLog` 경고만 남기며 `Export` 는 여전히 `true` 반환

## Task Commits

1. **Task 1: ExcelExportService 에 캡쳐이미지 첨부 컬럼 + 비동기 레이스 대기 구현** - `656dc45` (feat)

Task 2 는 checkpoint:human-verify — 코드 변경이 없으므로 커밋 없음. 오케스트레이터의 최종 docs 커밋도 Task 2 승인 이후로 미룸.

## Files Created/Modified
- `WPF_Example/Custom/Export/ExcelExportService.cs` - 11번째 컬럼 "캡쳐이미지" 추가 + `LoadCaptureImageBytes`/`WaitForCaptureImage`/`TryReadCompleteJpeg`/`TryInsertCaptureImage` private static 헬퍼 4개 신규(1개는 로드+캐시 조합, 나머지 3개는 대기/완결검사/삽입)

## Decisions Made
- 폴링 간격 100ms, 파일당 타임아웃 1500ms, 전체 예산 5000ms — CONTEXT 가 위임한 "1~2초 재량" 범위 내에서 선택 (D9Y-04 의 "수 분간 멈추지 않음" 요구를 만족시키는 상한)
- 이미지 표시 박스 160x120px + 원본보다 확대하지 않는 dScale ≤ 1.0 클램프 — CONTEXT 가 위임한 "셀 크기/스케일 구현 재량" 범위 내에서 선택
- 컬럼 폭은 `AdjustToContents()` 호출 **이후**에 11번 컬럼만 덮어써서 설정 — AdjustToContents 가 텍스트 기준으로만 폭을 잡고 그림 폭을 고려하지 않는다는 interfaces 섹션의 사실을 그대로 따름

## Deviations from Plan

None - plan 의 `<action>` 지시(using/const/헤더/캐시/헬퍼 3개 시그니처와 로직)를 그대로 따라 구현했다. coding_rules(삼항 금지/헝가리언/매직넘버 const화/함수 분리/try-catch 삼킴 패턴) 전부 준수.

## Issues Encountered

MSBuild 경로가 PATH 에 없어 `msbuild` 명령이 바로 실행되지 않았다 — Visual Studio 2022 설치 경로의 `MSBuild.exe` 전체 경로로 직접 호출해 해결(코드 변경과 무관, 빌드 실행 환경 이슈).

## Self-Check: PASSED

- FOUND: `WPF_Example/Custom/Export/ExcelExportService.cs` (수정됨, 존재 확인)
- FOUND: commit `656dc45` (`git log --oneline` 확인됨)
- 빌드: Debug|x64 MSBuild 결과 error 0 (기존 CS0618/CS0162 warning 만 존재, 이번 변경과 무관)
- 구조 grep 6종 전부 count ≥ 1: `AddPicture(ms, XLPictureFormat.Jpeg)`, `CAPTURE_IMAGE_COLUMN = 11`, `"캡쳐이미지"`, `File.ReadAllBytes`, `Thread.Sleep(CAPTURE_WAIT_POLL_MS)`, `CAPTURE_WAIT_BUDGET_MS`
- scope guard 3종 전부 0: `RepeatExcelExportService.cs` 미수정 / `packages.config` 미수정 / 신규 추가 라인에 삼항 연산자 없음
- 9번 컬럼(`ws.Cell(row, 9).Value`) 대입 라인 삭제/수정 없음 (diff 상 삭제 라인 0)
- 커밋 후 의도치 않은 파일 삭제 없음 (`git diff --diff-filter=D HEAD~1 HEAD` 결과 없음)

## User Setup Required

None - 새 NuGet 패키지/설정 없음. 기존 ClosedXML 0.105.0 만 사용.

## Next Phase Readiness

**Task 2 (checkpoint:human-verify) 승인 완료.** 아래는 승인 시 사용된 체크리스트(참고용 기록).

### 개발자 확인 체크리스트 (how-to-verify, plan 원문 그대로 — 승인됨)

1. `Debug|x64` 로 빌드 후 앱 실행 (SIMUL_MODE 로 충분합니다).
2. 검사를 1 cycle 돌려서 캡쳐 이미지가 생성되게 합니다.
3. 리뷰어 창을 열고 방금 cycle 을 선택 → **[엑셀 export]** 버튼 → 아무 위치에나 저장.
4. 저장된 `.xlsx` 를 엑셀로 열고 다음을 확인:
   - [ ] I열 "원본이미지 경로", J열 "캡쳐이미지 경로" 텍스트가 예전과 똑같이 나오는지
   - [ ] K열 헤더가 "캡쳐이미지" 이고, 그 아래 셀에 오버레이가 그려진 결과 이미지가 실제로 보이는지
   - [ ] 이미지가 찌그러지지 않고(종횡비 유지) 셀 안에 적당한 크기로 들어가는지
   - [ ] A~H열(Shot/FAI/측정명/Nominal/Tol+/Tol-/측정값/판정) 값이 예전과 동일한지
5. **빈 칸 동작 확인:** 검사가 끝나자마자 곧바로 export 해서 뒤쪽 몇 행 K열이 비어 있어도 "저장 완료"로 성공하는지 확인 (로그에 `capture image not ready, cell left blank` 경고).
6. **멈춤 확인:** 캡쳐 이미지 폴더가 없는/지운 오래된 cycle 을 export 해서 5초 이내 저장 완료되는지 확인.
7. **반복검사 export 회귀 확인:** 리뷰어의 반복검사(Gage R&R) export 버튼이 기존과 동일하게 동작하는지 확인 (이번 작업은 손대지 않은 영역).

참고: 셀 이미지는 원본 JPG 바이트 그대로 임베드된다(표시만 축소). 측정 항목이 많은 cycle 은 xlsx 용량이 커질 수 있다 — 부담스러우면 후속 "썸네일 리샘플링" 작업을 별도 요청.

**사용자 승인: "A 통과" (2026-08-05, 스크린샷 확인).**

---
*Phase: quick-260805-d9y*
*Completed: 2026-08-05*
