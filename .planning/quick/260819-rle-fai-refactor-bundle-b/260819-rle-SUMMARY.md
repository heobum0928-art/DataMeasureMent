---
phase: quick-260819-rle
plan: 01
status: complete
subsystem: inspection
tags: [csharp, refactor, extract-method, logging, file-naming]

requires:
  - phase: quick-260819-q9t
    provides: "동일 파일 오늘자 Bundle A(Dispose/MeasurementName 헬퍼+null방어 통일+pCamera 제거) 결과물 위에서 진행"
provides:
  - "LogDatumPhaseSummary(int,int,int,Stopwatch) 헬퍼 신설 — RunDatumPhase 완료 요약 로그 1줄을 LogAndTallyAlgorithm 과 대칭되는 소규모 로깅 전용 헬퍼로 추출"
  - "ResolveFaiCaptureFileNames(FAIConfig,...,out string,out string) 헬퍼 신설 — QueueFaiCapture 의 파일명/경로 결정(fai.Last*ImageFileName 기록 포함)을 Enqueue 부수효과와 분리"
affects: [action-faimeasurement, capture-image-save]

tech-stack:
  added: []
  patterns:
    - "로깅 전용 헬퍼 추출 — 카운터+Stopwatch 를 받아 string.Format+LogSeqStep 한 줄만 찍는 LogAndTallyAlgorithm 관례를 그대로 재사용"
    - "out 파라미터 2개로 순수계산(파일명 결정+필드 기록)과 부수효과(Enqueue)를 분리 — originName==null 을 '공유 origin 재사용' 신호로 사용해 원본의 중첩 if 가드를 평탄화된 단일 조건으로 재작성(동치 유지)"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

key-decisions:
  - "AddRef() 호출 2곳(origin 1 + capture 1)은 HImage 참조카운트가 실제 Enqueue 와 짝을 이뤄야 하므로 헬퍼로 옮기지 않고 QueueFaiCapture 본문에 그대로 유지"
  - "DateTime.Now 는 QueueFaiCapture 에서 1회만 계산해 헬퍼에 파라미터로 전달 — origin/capture 타임스탬프 공유 불변식 보존, 헬퍼 내부 재계산 금지"
  - "plan 의 Task2 정적 검증 스크립트가 전제한 파일 전역 카운트(AddRef=2, origin BuildFileName=1) 는 이 파일에 이미 존재하던 무관한 QueueSharedShotOrigin 메서드(자체 AddRef 1건 + origin BuildFileName 1건 보유) 를 계산에서 빠뜨린 것으로 확인 — git show HEAD(Task1 커밋 시점)로 재확인한 결과 Task2 편집 이전부터 이미 전역 카운트가 3/2 였음(내 편집으로 생긴 값 아님). 실제 must_have 요건(QueueFaiCapture+ResolveFaiCaptureFileNames 두 메서드 범위 내 AddRef=2/origin BuildFileName=1/DateTime.Now=1)은 awk 로 범위를 좁혀 별도 확인 — 정확히 일치"

requirements-completed: [RLE-01, RLE-02]

duration: 약 12분
completed: 2026-08-19
---

# Quick 260819-rle: FAI 리팩토링 Bundle B (LogDatumPhaseSummary/ResolveFaiCaptureFileNames 헬퍼 추출) Summary

**`Action_FAIMeasurement.cs` 순수 Extract Method 2건 — RunDatumPhase 요약로그 1줄→`LogDatumPhaseSummary` 헬퍼(LogAndTallyAlgorithm 과 대칭), QueueFaiCapture(73줄)의 파일명/경로 결정부→`ResolveFaiCaptureFileNames` 헬퍼로 분리(Enqueue 부수효과만 원본에 남김). 2커밋 전부 `wc -l` 사전실측값(1749→1759)과 정확히 일치, 최종 clean Rebuild error0/warning12(baseline).**

## Performance

- **Duration:** 약 12분 (커밋 2건, 검증+빌드 포함)
- **Tasks:** 2/2 완료
- **Files modified:** 1

## Accomplishments

- `LogDatumPhaseSummary(int nDatumOk, int nDatumFail, int nDatumCached, Stopwatch swDatumPhase)` 헬퍼 신설 — `RunDatumPhase` 마지막 완료 요약 로그(`"완료 — 검출성공 {0} / 실패 {1} / 캐시재사용 {2} ({3:F2}초)"`) 1줄을 헬퍼 호출로 치환. 시작 로그/`foreach` 루프/조명 복귀 블록/`Step = ...` 분기는 전부 원위치 무변경
- `ResolveFaiCaptureFileNames(FAIConfig fai, List<EdgeInspectionOverlay> faiOverlays, string sequenceName, string szSharedOriginPath, DateTime ts, out string captureName, out string originName)` 헬퍼 신설 — seg/judge/nIndexNumber 계산 + `fai.LastCaptureImageFileName`/`fai.LastOriginImageFileName` 대입 전체를 이동. `originName` 은 공유 origin 재사용 시 `null`, 개별 저장 필요 시 실제 파일명
- `QueueFaiCapture` 는 이제 헬퍼 호출 1줄 + Enqueue 부수효과만 담당 — 널가드 순서 보존(`if (originName != null && saver != null && sharedSrc != null)` 이 원본의 `!bUseSharedOrigin && saver!=null && sharedSrc!=null` 과 동치), `AddRef()` 2곳(origin 1 + capture 1) 전부 원본 위치(QueueFaiCapture 본문) 그대로 유지, `DateTime.Now` 는 1회만 계산해 헬퍼에 전달(재계산 없음)
- 두 Task 모두 신규 코드 삼항 `?:` 0건(if-else 만 사용), 헝가리언 변수명(judge/seg/nIndexNumber/parentSeq/bUseSharedOrigin) 무변경, public/internal 시그니처 변경 0건 — 외부 호출부(RunDatumPhase L140, QueueFaiCapture L1713 상당) 그대로 컴파일

## Task Commits

Each task was committed atomically:

1. **Task 1: LogDatumPhaseSummary 헬퍼 신설 + RunDatumPhase 요약로그 추출** - `d456f78` (refactor)
2. **Task 2: ResolveFaiCaptureFileNames 헬퍼 신설 + QueueFaiCapture 파일명결정/Enqueue 분리** - `9d9a20c` (refactor)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - 헬퍼 2개 신설(LogDatumPhaseSummary/ResolveFaiCaptureFileNames), RunDatumPhase 요약로그 추출 + QueueFaiCapture 파일명결정/Enqueue 분리. 1744줄 → 1759줄(순증가 +15)

## Verification Results

| Task | 커밋 | wc -l (결정론적) | 기대값 | 일치 | 빌드 (error/warning) |
|---|---|---|---|---|---|
| 1 | `d456f78` | 1749 | 1749 | PASS | 0 / 12 (baseline) |
| 2 | `9d9a20c` | 1759 | 1759 | PASS | 0 / 12 (baseline, 최종 `-t:Rebuild` clean 빌드) |

git diff --numstat (정보용, 하드게이트 아님): Task1 `+7/-2`, Task2 `+32/-22`.

### Task별 카운트 검증 (자기참조 오염 방지 포함)

- **Task 1:** `LogDatumPhaseSummary(` 리터럴 = **2**(선언1+호출1, PASS) / `if (bDatumOnly) {` = **1**(Step 분기 무변경) / 시작 로그(`기준점 검출`) = **1**(무변경) / 완료 로그 텍스트(`완료 —`) = **1**(헬퍼 내부로 이동, 텍스트 보존) / 외부 호출부 `case EStep.DatumPhase: RunDatumPhase(); break;` = **1**(무변경)
- **Task 2:** `ResolveFaiCaptureFileNames(` 리터럴 = **2**(선언1+호출1, PASS) / `fai.LastCaptureImageFileName =` = **1** / `fai.LastOriginImageFileName =` = **2**(공유/개별 분기 둘 다 보존) / `if (originName != null && saver != null && sharedSrc != null) {` = **1**(원본과 동치인 신형태 가드) / `if (saver == null || sharedSrc == null) return;` = **1**(capture 가드 무변경) / 외부 호출부 `QueueFaiCapture(fai, sharedSrc, faiOverlays, datumSnapshot, ownerSeqName, szSharedOriginPath)` = **1**(무변경)
- **Task 2 범위 한정 확인**(전역 카운트가 아닌 `ResolveFaiCaptureFileNames`+`QueueFaiCapture` 두 메서드 안으로 `awk` 범위를 좁혀 재확인): `sharedSrc.AddRef()` = **2**(origin 1 + capture 1, PASS — 원본과 동일 횟수/위치), `CaptureImageSaveService.BuildFileName("origin"` = **1**(PASS), `DateTime.Now` = **1**(PASS, 헬퍼에서 재계산 안 함). 전역(파일 전체) 카운트는 각각 3/2 로 다르게 나오는데, 이는 이 파일에 이미 존재하던 무관한 `QueueSharedShotOrigin` 메서드(자체 `AddRef` 1건 + `origin` `BuildFileName` 1건 보유, `git show` 로 Task2 편집 전부터 존재 확인)가 섞여 든 것 — plan 의 Task2 정적 검증 스크립트가 이를 감안하지 못한 것으로, 이번 편집으로 새로 생긴 문제 아님(아래 Deviations 참고)

### 누적 확인 (2커밋)

`git diff --name-only 47e7160 HEAD` = `Action_FAIMeasurement.cs` 1개 파일뿐. 최종 `wc -l` = **1759**(1744+7+8, 사전 손계산 1749/1759 와 정확히 일치). `git status --porcelain` = ` M WPF_Example/DatumMeasurement.csproj` 만(csproj 2커밋 내내 unstaged 유지).

### 인코딩 보존

매 Task 편집 직후 확인 — UTF-8 BOM(`efbbbf`) 유지 2회 전부 PASS, `grep -c $'\r'` = 0 2회 전부 PASS(CRLF 오염 없음). 한글 주석/문자열 손상 0건.

### 커밋 위생

매 Task 커밋 전 `git diff --cached --name-only` = 대상 파일 1줄만 출력 확인 후 커밋. 매 커밋 후 `git status --short` 로 `WPF_Example/DatumMeasurement.csproj` 가 계속 ` M`(unstaged) 상태인지 확인 — 2회 전부 PASS, 한 번도 스테이징되지 않음.

## Decisions Made

- `AddRef()` 2곳 모두 헬퍼로 옮기지 않고 `QueueFaiCapture` 본문에 유지 — HImage 참조카운트 관리가 실제 `Enqueue` 와 짝을 이뤄야 하므로(threat register T-rle-02, mitigate 로 grep 검증)
- `DateTime.Now` 는 `QueueFaiCapture` 에서 1회만 계산해 헬퍼에 파라미터로 전달 — origin/capture 타임스탬프 공유 불변식 보존
- plan Task2 의 정적 검증 스크립트가 전제한 전역 카운트(AddRef=2, origin BuildFileName=1)가 실측 3/2 로 나온 것을 발견 — `git show HEAD`(Task1 커밋본, 내 Task2 편집 이전)로 확인한 결과 이미 그 시점부터 3/2 였으므로 이번 편집이 만든 값이 아님을 실증. must_have 의 실제 요건(QueueFaiCapture/ResolveFaiCaptureFileNames 범위 내 AddRef=2)은 `awk` 로 두 메서드 범위만 골라 재확인해 정확히 일치함을 확인 — 하드 게이트로 사용한 것은 이 범위한정 카운트

## Deviations from Plan

### Auto-fixed Issues

없음 — 코드 변경은 plan 의 old_string/new_string 을 그대로 사용, 전부 1회 매치로 성공. 유일한 차이는 검증 스크립트 해석뿐이다.

**1. [검증 스크립트 해석 조정 — 코드 변경 아님] Task2 전역 AddRef/origin BuildFileName 카운트 정정**
- **Found during:** Task 2 사전확인(step 0)
- **Issue:** plan 의 step 0 사전확인 스크립트가 `grep -oF 'sharedSrc.AddRef()' | wc -l` 로 **2** 를 기대했으나 실제는 **3**(이미 존재하던 `QueueSharedShotOrigin` 메서드의 무관한 AddRef 1건 포함). 검증 단계의 `grep -oF 'sharedSrc.AddRef()' | wc -l = 2`, `grep -cF 'CaptureImageSaveService.BuildFileName("origin"' = 1` 두 항목도 동일 이유로 전역 카운트가 각각 3/2 로 나옴
- **Fix:** 코드는 변경하지 않음(plan 대로 편집). 검증만 `awk` 로 `ResolveFaiCaptureFileNames`+`QueueFaiCapture` 두 메서드 범위로 좁혀 재확인 — 범위 한정 카운트는 AddRef=2/origin BuildFileName=1/DateTime.Now=1 로 must_have 요건과 정확히 일치
- **Files modified:** 없음(검증 방법만 조정)
- **Verification:** `git show 47e7160(=HEAD 이전 Task1 커밋):파일 | grep -c` 로 Task2 편집 이전부터 전역 카운트가 이미 3/2 였음을 재확인 — 회귀 아님
- **Committed in:** 해당 없음(코드 변경 없는 검증 방법 조정)

---

**Total deviations:** 1건(코드 변경 없는 검증 스크립트 해석 조정)
**Impact on plan:** 코드/동작에 영향 없음. must_have 의 실제 의미(QueueFaiCapture 본문 범위 내 AddRef 불변)는 범위 한정 검증으로 정확히 충족.

## Issues Encountered

**전역 grep 카운트가 plan 사전확인값과 다름(위 Deviations 참고):** Task2 착수 전 사전확인 스크립트 3개 항목(`private void QueueFaiCapture(...)` 존재/외부호출부/`AddRef()` 카운트) 중 앞 2개는 정확히 일치(1/1), `AddRef()` 카운트만 기대 2 대 실측 3 로 어긋났음. `grep -n` 으로 즉시 재탐색해 원인(무관한 `QueueSharedShotOrigin` 메서드, L1227-1250, 이번 plan 범위 밖)을 특정하고 `git show` 로 회귀가 아님을 재확인한 뒤 진행 — old_string 자체(Task2 대상 원문 77줄)는 plan 이 제시한 그대로 정확히 매치했으므로 편집에는 영향 없었음.

## User Setup Required

None - 외부 서비스 설정 불필요. 정적 검증(카운트+wc -l+빌드)만으로 회귀 0 결론 — 순수 텍스트 추출/재배치라 판정 로직 접근 없음. plan 이 권고한 실기 UAT(Shot 1개 검사 후 [SEQ] DatumPhase 완료 로그 + 원본/캡처 PNG 파일명 비교)는 선택사항이며 이번 세션에서는 미실행.

## Next Phase Readiness

- `Action_FAIMeasurement.cs` Bundle B(RunDatumPhase 요약로그/QueueFaiCapture 파일명결정) 정리 완료, 후속 코드 작업 없음
- Blockers 없음

## Known Stubs

없음 - 순수 리팩토링(추출/치환)이며 신규 데이터 소스/바인딩/UI 변경 없음.

## Threat Flags

없음 - 신규 네트워크 엔드포인트·인증 경로·파일 접근·스키마 변경 없음. `ResolveFaiCaptureFileNames` 는 기존과 동일한 로컬 파일 시스템 경로 생성 로직을 그대로 이동한 것뿐이며 새로운 노출면이 없다(threat_model T-rle-01, accept). AddRef 호출횟수/위치 불변(T-rle-02, mitigate)은 위 카운트 검증으로 충족.

## Self-Check: PASSED

파일 존재 확인:
```
FOUND: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
FOUND: .planning/quick/260819-rle-fai-refactor-bundle-b/260819-rle-SUMMARY.md
```

커밋 존재 확인:
```
FOUND: d456f78 (Task 1)
FOUND: 9d9a20c (Task 2)
```

---
*Phase: quick-260819-rle*
*Completed: 2026-08-19*
