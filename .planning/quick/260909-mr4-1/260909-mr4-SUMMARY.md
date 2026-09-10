---
phase: quick-260909-mr4
plan: 01
subsystem: vision-inspection
tags: [halcon, offline-inspect, capture-save-service, datum, shot, recipe]

requires: []
provides:
  - "SystemSetting.AutoFillOfflineImages — 기본 꺼짐 설정 게이트"
  - "RecipeFiles.BuildOfflineImagePath — 오프라인 검사이미지 경로 단일 소스(수동/자동 공유)"
  - "CaptureImageSaveRequest.FormatOverride — 요청 단위 저장 포맷 오버라이드(bmp 강제)"
  - "Action_FAIMeasurement 자동채움 훅 — Shot(판정 무관) + Datum(검출 성공 시만, 1-image/크로스-Z 방향 보존)"
affects: [inspection-list-view, main-view-grab-save, offline-inspect-mode]

tech-stack:
  added: []
  patterns:
    - "SharedHImage AddRef/Release 짝 재사용 — Shot 은 sharedSrc 에 AddRef 만 추가, Datum 은 CopyImage 사본 + finally Release"
    - "요청 단위 포맷 오버라이드로 기존 설정(OriginImageFormat) 규칙을 건드리지 않고 bmp 강제"
    - "out bool 로 이미 계산된 조건(bCrossZEnabled)을 그대로 내보내 호출부 재계산 금지(D-09 준수)"

key-files:
  created: []
  modified:
    - "WPF_Example/Setting/SystemSetting.cs"
    - "WPF_Example/Utility/RecipeFileHelper.cs"
    - "WPF_Example/Utility/CaptureImageSaveService.cs"
    - "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
    - "WPF_Example/UI/ControlItem/InspectionListView.xaml.cs"

key-decisions:
  - "덮어쓰기 정책: 매 사이클 같은 노드는 같은 파일명을 덮어쓴다(의도 = '마지막으로 검사한 이미지' 1장 보유). 히스토리는 기존 origin/capture 트리가 담당, 이번 변경 영향 없음. 파일 수 = 노드 수 고정."
  - "write-back 정책: 값이 실제로 다를 때만 SimulImagePath/TeachingImagePath(_Vertical) 에 대입 + Trace 로그 1줄. 레시피 저장(RecipeFiles.Save)은 절대 자동 호출하지 않음 — 사용자 의도 없이 레시피 파일을 쓰지 않는다."
  - "가로/세로 방향은 화면 토글(EImageSource)을 흉내내지 않고, 검출이 실제로 소비한 imgH/imgV 변수를 그대로 _horizontal/_vertical 에 대응시킨다(TryTakeCrossZImageClones/TryLoadStaticDualDatumImages 기확립 규약)."
  - "정적 2장 Datum 경로(TryLoadStaticDualDatumImages)와 SIMUL_MODE/OfflineInspectMode 는 자동채움 대상에서 제외 — 저장 파일을 읽어 자기 자신에게 되쓰는 무의미한 왕복을 막기 위함(bCrossZLiveCaptured/IsLiveCaptureMode 게이트)."

requirements-completed: [QUICK-260909-MR4-01, QUICK-260909-MR4-02, QUICK-260909-MR4-03, QUICK-260909-MR4-04, QUICK-260909-MR4-05]

coverage:
  - id: D1
    description: "설정 AutoFillOfflineImages 신설(기본 꺼짐) — 신규 코드 전부의 유일한 게이트"
    requirement: "QUICK-260909-MR4-01"
    verification:
      - kind: manual_procedural
        ref: "설정창 System|Enviroment 탭에서 AutoFillOfflineImages 항목 확인, 기본값 false"
        status: unknown
    human_judgment: true
    rationale: "설정창 UI 표시/기본값은 실행 중인 앱에서 육안 확인이 필요 — 오케스트레이터 UAT 항목 1"
  - id: D2
    description: "Shot 이미지 자동채움 — 매 Shot 촬영마다 저장(판정 무관, sharedSrc AddRef 재사용)"
    requirement: "QUICK-260909-MR4-02"
    verification:
      - kind: manual_procedural
        ref: "실기 자동검사 1사이클, OfflineInspect\\<레시피>\\shot_<이름>.bmp 전부 채워짐(NG 포함) 확인 — UAT 항목 2"
        status: unknown
    human_judgment: true
    rationale: "실 카메라/실 사이클 없이는 검증 불가 — 하드웨어 UAT, 오케스트레이터가 사용자와 함께 수행"
  - id: D3
    description: "Datum 이미지 자동채움 — 검출 성공 시에만(1-image/크로스-Z 방향 보존)"
    requirement: "QUICK-260909-MR4-03"
    verification:
      - kind: manual_procedural
        ref: "검출 성공 시 datum_<이름>[_horizontal/_vertical].bmp 채워짐, 실패 사이클엔 mtime 불변, 방향이 수동 [검사Grab] 과 일치 — UAT 항목 3/4/5"
        status: unknown
    human_judgment: true
    rationale: "실기 검출 성공/실패 재현과 이미지 방향 육안 대조가 필요 — 하드웨어 UAT"
  - id: D4
    description: "tact 무증가 — 비동기 큐 재사용, 검사 스레드 동기 디스크 I/O 0"
    requirement: "QUICK-260909-MR4-04"
    verification:
      - kind: other
        ref: "git diff -U0 Action_FAIMeasurement.cs | grep 'WriteImage|CreateDirectory' → 0 (코드 게이트, 이미 실행함)"
        status: pass
      - kind: manual_procedural
        ref: "실기 [SEQ] 로그 tact 비교(ON/OFF), 저장 지연 로그 미발생 — UAT 항목 6"
        status: unknown
    human_judgment: true
    rationale: "정적 코드 게이트는 통과했으나 실측 tact 비교는 실기에서만 가능"
  - id: D5
    description: "덮어쓰기 정책 명시 — 매 사이클 같은 파일명 갱신"
    requirement: "QUICK-260909-MR4-05"
    verification:
      - kind: other
        ref: "RecipeFiles.BuildOfflineImagePath 가 노드명 결정론적 파일명만 반환(타임스탬프 미포함) — 코드 확인"
        status: pass
    human_judgment: false

duration: 64min
completed: 2026-09-10
status: complete
---

# Quick 260909-mr4: 오프라인 검사이미지 자동채움 Summary

**자동 검사 1사이클이 촬영한 Shot/Datum 이미지를 `OfflineInspect` 경로에도 함께 채우는 설정 게이트(기본 꺼짐) + 공용 경로함수 + bmp 포맷 오버라이드 + 방향보존 Datum 훅 구현 완료(코드 태스크 3건 커밋), 실기 UAT는 오케스트레이터 확인 대기.**

## Performance

- **Duration:** 64min (커밋 기준, 09:17~10:21 KST)
- **Started:** 2026-09-10T01:17:00Z
- **Completed:** 2026-09-10T01:21:00Z
- **Tasks:** 3/3 코드 태스크 완료
- **Files modified:** 5

## Accomplishments
- `SystemSetting.AutoFillOfflineImages` bool 설정 신설(System|Enviroment, 기본 false) — 신규 코드 전부의 유일한 게이트, INI 키 누락 시 기존 설치본 자동으로 꺼진 상태 유지
- `RecipeFiles.BuildOfflineImagePath` 를 비-UI 공용 함수로 신설 — 폴더를 만들지 않는 순수 문자열 계산(검사 스레드에서도 안전하게 호출 가능), 경로 규약 상수(`OFFLINE_FOLDER`/`OFFLINE_EXT`/`OFFLINE_PREFIX_SHOT`/`OFFLINE_PREFIX_DATUM`/`OFFLINE_SUFFIX_HORIZONTAL`/`OFFLINE_SUFFIX_VERTICAL`)를 함께 공개
- `CaptureImageSaveRequest.FormatOverride` 추가 — 비어 있으면 기존 `OriginImageFormat`(JPG/BMP) 규칙 그대로, 오프라인 자동채움만 항상 bmp 강제
- `Action_FAIMeasurement` 자동채움 인프라(게이트/라이브캡처판정/큐잉 헬퍼 4개) + Shot 훅(기존 `sharedSrc` 에 AddRef 재사용, 추가 복사 0) + Datum 훅(1-image 는 카운터 전후 비교로 검출성공 판정, 크로스-Z 는 `imgH`/`imgV` 를 검출이 실제 소비한 변수 그대로 가로/세로에 대응)
- 수동 [검사Grab](`InspectionListView`)이 같은 `RecipeFiles.BuildOfflineImagePath` 를 쓰도록 위임 — 경로 규약 이중화 제거, 로컬 `SanitizeFileName` 삭제

## Task Commits

Each task was committed atomically:

1. **Task 1: 설정 + 경로 단일소스 + 포맷 오버라이드 + Shot 자동채움 (tracer)** - `a9708674` (feat)
2. **Task 2: Datum 자동채움 — 검출 성공 시에만, 가로/세로 방향 보존** - `a6989a1e` (feat)
3. **Task 3: 수동 [검사Grab] 경로 계산을 공용 함수로 위임** - `6552f7d6` (refactor)

**Plan metadata:** (이 커밋에서 함께 기록됨)

## Files Created/Modified
- `WPF_Example/Setting/SystemSetting.cs` - `AutoFillOfflineImages` bool 설정(기본 false) 신설
- `WPF_Example/Utility/RecipeFileHelper.cs` - `RecipeFiles.BuildOfflineImagePath` 공용 함수 + 경로 규약 상수 신설
- `WPF_Example/Utility/CaptureImageSaveService.cs` - `CaptureImageSaveRequest.FormatOverride` 추가, `SaveRequest` 포맷 결정부에 반영
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - 자동채움 게이트/헬퍼 4개 + Shot/Datum(1-image·크로스-Z) 훅, `TryGrabOrLoadDualDatumImages`/`RunDatumDualImageDetection` 시그니처 확장
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` - `BuildOfflineImagePath` 위임, `SanitizeFileName` 삭제, 리터럴 접두사 → 공용 상수

## Decisions Made
- **덮어쓰기 정책(요구사항 5):** 같은 노드는 매 사이클 같은 파일명을 덮어쓴다. 이것이 의도다 — 히스토리가 아니라 "이 노드의 현재 오프라인 검사이미지" 한 장이며, 목표가 "마지막으로 검사 진행한 이미지로 채우기"이기 때문이다. 결과 이미지 히스토리는 기존 `origin/capture` 트리(`ResultSavePath\Image\yyMMdd\HHmm\...`)가 그대로 담당하며 이번 변경의 영향을 받지 않는다. 파일 수는 노드 수만큼으로 고정(무한 증가 없음).
- **write-back 정책과 알려진 한계(요구사항 6):** 파일명이 노드 이름에서 결정론적으로 나오므로 현재 값이 이미 같은 경로면 아무것도 하지 않는다. 다르거나 비어 있을 때만 대입하고 Trace 로그 1줄을 남긴다. **`RecipeFiles.Save`/레시피 저장은 호출하지 않는다** — 사용자 의도 없이 레시피를 쓰지 않는다.
  - **알려진 한계 — 비동기 저장 완료 전 write-back:** write-back(`ShotParam.SimulImagePath`/`datum.TeachingImagePath(_Vertical)` 갱신)은 `CaptureImageSaveService` 워커가 실제 파일을 디스크에 쓰기 **전**에 동기적으로 일어난다(큐잉만 확정, 디스크 쓰기는 비동기). 워커가 아직 쓰지 않은 시점에 앱이 죽거나 강제 종료되면, 노드는 아직 존재하지 않는 파일을 가리키게 된다. 이 실패 모드는 조용히 묻히지 않는다 — 다음 오프라인 검사 사이클에서 `File.Exists` 실패 → Error 로그 + 해당 Datum/Shot NG 판정으로 **시끄럽게** 드러난다(조용한 오검이 아님). 큐 자체는 기존 `MAX_QUEUE_DEPTH=50` 백프레셔로 상한이 있고, 정상 운영 중(워커가 살아있는 한) 수 초 내 반영된다.
- **노드명 충돌(수동 경로에서 상속한 기존 한계):** 파일명에 시퀀스명이 없어 서로 다른 시퀀스가 완전히 동일한 ShotName/DatumName 을 쓰면 파일이 충돌한다. 이는 현재 수동 [검사Grab] 경로도 동일한 한계이며, 파일명 규약을 바꾸면 오프라인 검사가 기존 파일을 못 찾게 되므로 이번 작업에서 바꾸지 않았다.
- **범위 밖(비목표) — 측정 단위 듀얼 이미지 경로:** `DualImageEdgeDistanceMeasurement.TeachingImagePath_Horizontal/_Vertical` (FAI 측정 단위의 듀얼 이미지 경로, Shot/Datum 과 별개 산출물)은 이번 자동채움 대상이 아니다. 요구사항은 Shot 이미지와 Datum 이미지 2종뿐이었고, 측정 단위 듀얼 경로는 측정마다 다른 z-role 짝을 요구하는 별개 개념이라 의도적으로 제외했다. Shot 노드의 `SimulImagePath` 만 자동채움이 갱신한다.
- **방향 대응 근거:** `imgH`(role A/`ZIndexA`)는 검출이 가로축으로 소비한 바로 그 이미지이자 `TeachingImagePath` 대응, `imgV`(role B/`ZIndexB`)는 세로축 소비 이미지이자 `TeachingImagePath_Vertical` 대응이다. 이 규약은 `TryTakeCrossZImageClones`(`Horizontal=keyA, Vertical=keyB`)와 `TryLoadStaticDualDatumImages`(`pathH=TeachingImagePath, pathV=TeachingImagePath_Vertical`)에 이미 확립돼 있던 것을 그대로 재사용했다 — 화면 토글(`_currentImageSource`/`EImageSource`)을 새로 흉내내지 않았다(코드 게이트로 참조 0건 확인).

## Deviations from Plan

**1. [Rule 1 - 가독성 게이트 대응] 기존 날짜 주석(hbk) 이 diff 추가 라인에 재등장해 게이트 실패 → 문구 정리**
- **Found during:** Task 2 자동 검증(`grep -cF 'hbk'` 게이트)
- **Issue:** `RunDatumDualImageDetection` 반환값을 `void`→`bool` 로 바꾸며 그 호출 줄(`bool bDetectOk = RunDatumDualImageDetection(...)`)에 원래 있던 레거시 주석 `//260819 hbk quick-260819-s05: ...` 이 그대로 딸려와 diff 추가 라인이 됐다. 이 줄은 신규 작성 주석이 아니라 기존 주석이 우연히 같은 줄에 있었을 뿐이지만, `git diff` 기반 게이트는 라인 단위로만 판별하므로 실패로 잡혔다.
- **Fix:** 그 줄의 주석에서 날짜/`hbk` 태그(`260819 hbk quick-260819-s05:`)만 제거하고 설명 문구는 그대로 유지했다(CLAUDE.md 2026-06-11 정책 전환은 "신규 날짜 주석 금지"이며, 이미 건드리는 줄에서 낡은 태그를 트리밍하는 것은 정책 위반이 아니다).
- **Files modified:** `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs`
- **Verification:** 재빌드 통과, 가독성 게이트 5종 전부 0으로 재확인.
- **Committed in:** `a6989a1e` (Task 2 커밋에 포함)

---

**Total deviations:** 1 auto-fixed (Rule 1 - 가독성 게이트 대응, 코드 동작 무변경)
**Impact on plan:** 스코프 영향 없음. 순수 주석 정리, 로직/동작 변경 0.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## 실기 UAT (오케스트레이터 확인 대기 — 앱을 실행하지 않았음)

execution_notes 지시에 따라 코드 태스크 3건은 전부 완료·커밋했으나, 앱 실행은 하지 않았다.
아래 8개 항목은 오케스트레이터가 사용자와 함께 실기에서 확인해야 한다(플랜 `<verification>` 원문 그대로):

1. **회귀 0 (설정 OFF, 기본):** 자동 검사 한 사이클 → 동작이 종전과 동일, `OfflineInspect\<레시피명>\` 에 새 파일 없음. — **대기**
2. **Shot 채움 (설정 ON):** 모든 Shot 이 `shot_<ShotName>.bmp` 로 채워짐(NG 포함). — **대기**
3. **Datum 성공 시 채움:** 검출 성공 → `datum_<이름>_horizontal.bmp`/`_vertical.bmp`(또는 1-image `datum_<이름>.bmp`). — **대기**
4. **Datum 실패 시 미덮어쓰기:** 검출 실패 사이클 후 파일 mtime 불변. — **대기**
5. **방향 정합:** 자동 `_horizontal`/`_vertical` 이 수동 [검사Grab] 과 같은 방향. — **대기**
6. **tact:** `[SEQ]` 단계 요약 소요초 비교, 저장 지연 로그 미발생. — **대기**
7. **오프라인 재생:** 자동채움 후 `OfflineInspectMode` ON 검사가 그 이미지로 수행됨. — **대기**
8. **레시피 저장:** write-back 은 Trace 로그로만 확인, 사용자가 직접 저장하기 전까지 INI 무변경. — **대기**

## Next Phase Readiness
- 코드 게이트(빌드 에러 0, 가독성 5종 0, origin 경로 무변경, 게이트 단일 입구, 동기 I/O 0, MainView 무변경, 방향 대응 1:1, SharedHImage 수명 짝)는 모두 통과·확인 완료.
- 실기 UAT 8항목은 오케스트레이터가 이어서 수행해야 다음 단계(Side 티칭 재작업)로 넘어갈 수 있다.
- `D:\Data\Recipe\FAI_1` 실기 레시피는 이번 작업에서 건드리지 않았다.

---
*Phase: quick-260909-mr4*
*Completed: 2026-09-10*

## Self-Check: PASSED
- 5개 소스 파일 + SUMMARY.md 전부 디스크에 존재 확인.
- 커밋 3건(`a9708674`, `a6989a1e`, `6552f7d6`) 전부 `git log --oneline --all` 에서 확인.
