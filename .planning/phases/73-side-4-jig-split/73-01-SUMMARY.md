---
phase: 73-side-4-jig-split
plan: 01
subsystem: sequence-registration
tags: [sequence, enum, recipe-compat, side-jig-split]
requires:
  - "없음 (wave 1, depends_on: [])"
provides:
  - "ESequence.Side1~Side4 = 4~7 / EAction.Side1~4_Inspection = 7~10"
  - "SEQ_SIDE_1~4 상수 + ResolveSequenceName 4 case (조용한 TOP 폴백 제거)"
  - "IsSequenceActive — Side 역할 PC 에서 SIDE_1~4 전부 활성"
  - "SIDE_1~4 InspectionSequence 4개 등록/초기화/RebuildInspectionActions 6분기"
  - "레거시 Param0..N 저장/로드 가드 (ParamSequenceCount)"
affects:
  - "WPF_Example/Custom/Define/ID.cs"
  - "WPF_Example/Custom/Sequence/SequenceHandler.cs"
  - "WPF_Example/Sequence/SequenceHandler.cs"
tech-stack:
  added: []
  patterns:
    - "named-bool-extraction — bSkipLegacyParam / bSeqCountMismatch / bIsSideJig 로 조건 선추출(삼항 0건)"
    - "count-stamped legacy format — 위치 인덱스 저장 포맷에 개수(ParamSequenceCount)를 함께 남겨 로드 시 오매핑 차단"
    - "logged fallback — switch default 폴백을 유지하되 Error 로그를 남겨 조용한 오라우팅 제거"
key-files:
  created: []
  modified:
    - "WPF_Example/Sequence/SequenceHandler.cs"
    - "WPF_Example/Custom/Define/ID.cs"
    - "WPF_Example/Custom/Sequence/SequenceHandler.cs"
decisions:
  - "ESequence.Side = 2 를 삭제하지 않고 레거시 식별자로 존치 — VisionResponsePacket.cs:226 이 (int)ESequence.Bottom 을 와이어 site 정수와 직접 비교하므로 enum 번호 재배치가 프로토콜 회귀를 만든다"
  - "Top=1 / Side=2 / Bottom=3 값 무변경, SIDE_1~4 는 뒤에 4~7 로 이어 붙임"
  - "동적 FAI 모드에서는 레거시 Param0..N 을 아예 저장하지 않는다 — SHOTS 포맷이 단일 소스이고 로드 경로가 이 키를 읽지 않는다"
  - "레거시 로드는 ParamSequenceCount 불일치 시 통째로 스킵 + Error 로그. 부분 로드보다 0-클로버 위험이 낮다"
  - "SEQ_SIDE 상수와 EAction.Side_Inspection 은 존치 — 구 레시피 OwnerSequenceName 값이자 후속 마이그레이션(M4/M6)의 기준값"
  - "빌드는 MSBuild 절대경로로 호출(PATH 에 MSBuild.exe 없음). 그 외 73-BUILD-VERIFY.md 규격 그대로"
metrics:
  duration: "약 25분"
  completed: "2026-08-26"
  tasks: 3
  files: 3
---

# Phase 73 Plan 01: SIDE 4지그 분리 골격 Summary

SIDE 단일 시퀀스를 SIDE_1~4 네 개의 독립 `InspectionSequence` 로 분리하는 타입 기반 골격을 완성했다. enum·상수·활성 판정·등록 3지점·Action 재구축을 한 커밋 범위 안에서 함께 바꿔 "시퀀스 미생성" / "TOP 조용한 폴백" 중간 상태를 만들지 않았고, 시퀀스 개수를 3→6 으로 늘리기 **전에** 레거시 `Param0..N` 위치 인덱스 오매핑 경로를 먼저 닫았다.

## What Was Built

### Task 1 — 레거시 Param0..N 위치 인덱스 시프트 차단 (R4, `aafaa50`)

`WPF_Example/Sequence/SequenceHandler.cs`

- `SaveToIni`: `bool bSkipLegacyParam = IsDynamicFAIMode;` 로 분기. 동적 FAI 모드면 `Param0..N` 을 저장하지 않고 `Info/ParamSequenceCount = 0` 만 남긴다. 레거시 모드면 기존 루프를 그대로 돌린 뒤 `ParamSequenceCount = Sequences.Count` 를 기록한다.
- `LoadFromIni`: 레거시 루프 직전에 `nSavedSeqCount` / `bHasSavedSeqCount` / `bSeqCountMismatch` 를 계산하고, 불일치면 Error 로그를 남긴 뒤 루프 전체를 `if (!bSeqCountMismatch)` 로 건너뛴다.
- `Logging` / `ELogType` 은 이 파일에 이미 `using ReringProject.Utility;` / `using ReringProject.Setting;` 이 있어 using 추가 불필요했다.

### Task 2 — enum/상수/이름 해석/활성 판정 (R5·R6, `5bd007e`)

`WPF_Example/Custom/Define/ID.cs`

- `ESequence`: `Side1 = 4 … Side4 = 7` 추가. `Top = 1` / `Side = 2` / `Bottom = 3` 은 값·위치 모두 무변경이며, `Side = 2` 존치 이유를 주석으로 코드에 남겼다.
- `EAction`: `Side1_Inspection = 7 … Side4_Inspection = 10` 추가(`FAI_Base = 100` 과 충돌 없음).

`WPF_Example/Custom/Sequence/SequenceHandler.cs`

- `SEQ_SIDE_1~4` 상수 4개 추가(`SEQ_SIDE` 존치).
- `ResolveSequenceName`: SIDE_1~4 case 4개 추가. `default:` 는 폴백을 유지하되 `Logging.PrintLog((int)ELogType.Error, ...)` 로 흔적을 남긴다.
- `IsSequenceActive`: `bIsSideJig` 이름 있는 bool 로 SIDE_1~4 판정. 레거시 `ESequence.Side` 는 의도적으로 제외.
- stale 주석 "SIMUL 은 전체 활성 …" 문장 삭제(코드에 SIMUL 분기 없음). 같은 블록의 "TopBottom=Top/Bottom, Side=Side 만 활성" 도 현행에 맞게 갱신.

### Task 3 — 등록 3지점 + Action 재구축 6분기화 (M5, `6f8efda`)

`WPF_Example/Custom/Sequence/SequenceHandler.cs`

- `RegisterSequences()` : `InspectionSequence(ESequence.SideN, SEQ_SIDE_N, Side_Alg_Index, DeviceHandler.CAMERA_SIDE, LightHandler.LIGHT_BAR)` 4블록.
- `RegisterActions()` : `TopSideInspectionAction(EAction.SideN_Inspection, …)` 4블록.
- `InitializeSequences()` : `seqSide1~4` 4블록.
- `TryLoadNewFormat()` : `RebuildInspectionActions(ESequence.Side)` 1줄 → SIDE_1~4 4줄. Top/Bottom 줄은 무변경.

## Verification — 실제 실행 결과

### acceptance grep (전 항목 통과)

```
# Task 1
ParamSequenceCount=3   bSeqCountMismatch=3   bSkipLegacyParam=2
219: param.Load(loadFile, "Param" + m.ToString());
264: param.Save(saveFile, "Param" + m.ToString());        (여전히 2건)

# Task 2
Side1 = 4 → 1 / Side2 = 5 → 1 / Side3 = 6 → 1 / Side4 = 7 → 1
Top = 1 → 1 / Bottom = 3 → 1        (기존 값 무변경)
Side1_Inspection = 7 → 1 / Side4_Inspection = 10 → 1
SEQ_SIDE_1~4 각 1 / "SIMUL 은 전체 활성" → 0
ResolveSequenceName default 블록에 Logging.PrintLog 1건

# Task 3
IsSequenceActive(ESequence.Side1..Side4) → 각 3
RebuildInspectionActions(ESequence.Side  → 4
RebuildInspectionActions(ESequence.Side) → 0
new InspectionSequence(ESequence.Side,   → 0
```

### 빌드 (73-BUILD-VERIFY.md 규격, `-t:Rebuild` + 스크래치 OutDir/IntermediateOutputPath)

| 시점 | 구성 | exit | error | warning 줄 | 코드 분포 |
|---|---|---|---|---|---|
| Task 1 후 | SIMUL-ON (Debug\|x64) | 0 | 0 | 12 | CS0618×10 + CS0162×2 |
| Task 2 후 | SIMUL-ON | 0 | 0 | 12 | CS0618×10 + CS0162×2 |
| Task 3 후 | SIMUL-ON | 0 | 0 | **18** | CS0618×16 + CS0162×2 |
| Task 3 후 | SIMUL-OFF (`-p:DefineConstants=TRACE%3BDEBUG`) | 0 | 0 | **16** | CS0618×16 |

계획이 예고한 18/16 과 정확히 일치한다. CS0162 가 OFF 에서 2→0 으로 사라져 SIMUL-OFF 가 실제로 적용됐음이 교차 확인된다. 새 경고 코드 종류 0건이며 `[Obsolete]` 제거 / `#pragma warning disable` / `NoWarn` 은 사용하지 않았다.

### 코딩 규칙 [W4]

세 커밋의 추가 라인 전수 검사 — `??` / `?.` 0건, 주석 제외 삼항 후보 0건.

### csproj

`git status --porcelain WPF_Example/DatumMeasurement.csproj` → ` M` (앞칸 공백, 끝까지 unstaged). 세 커밋 어디에도 포함되지 않았다(`git log --name-only` 검색 0건). 세 커밋 모두 파일 삭제 0건.

## Deviations from Plan

### 환경 적응 (계획 내용 무변경)

**1. [Rule 3 - Blocking] MSBuild 절대경로 사용**
- **Found during:** Task 1 빌드 검증
- **Issue:** `MSBuild.exe` 가 Git Bash PATH 에 없어 `command not found` (exit 127)
- **Fix:** `/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe` 를 절대경로로 호출. 옵션·구성·경로는 73-BUILD-VERIFY.md 그대로
- **Files modified:** 없음(빌드 호출 방식만)

그 외 계획 대비 코드 변경 편차 없음. 3개 Task 모두 계획에 적힌 코드 그대로 적용했다.

## Known Stubs

없음. `TopSideInspectionAction` 4개는 계획이 명시한 placeholder 로, 동적 FAI 모드 진입 시 `RebuildInspectionActions` 가 `Action_FAIMeasurement` 로 교체한다(기존 Top/Bottom 과 동일 구조).

## 남은 `ESequence.Side` 참조 (후속 plan 소관 — 이 plan 범위 밖)

```
Custom/Sequence/Inspection/InspectionRecipeManager.cs:193  SaveFixtureForSequence(..., ESequence.Side, "FIXTURE_SIDE", ...)
Custom/Sequence/Inspection/InspectionRecipeManager.cs:272  LoadFixtureForSequence(..., ESequence.Side, "FIXTURE_SIDE")
UI/ContentItem/MainView.xaml.cs:4149                       ESequence[] roles = { Top, Side, Bottom }
```

각각 D-73-07 의 **M4**(FIXTURE_SIDE → FIXTURE_SIDE_1~4 분할 + 마이그레이션)와 **M9**(Datum UI roles 배열)에 배정돼 있다. 이 상태로는 SIDE Datum 이 여전히 `FIXTURE_SIDE` 한 섹션에 저장/로드되고 MainView Datum 트리에 SIDE_1~4 가 뜨지 않는다 — **M4 완료 전에는 레시피 저장을 하지 말 것**(3faa91b 데이터 손실 패턴).

## Threat Flags

없음. 이 plan 은 신규 네트워크 표면/인증 경로/파일 접근 패턴을 도입하지 않는다. T-73-01(레거시 Param 오매핑)·T-73-02(조용한 폴백)는 계획대로 mitigate 됐고, T-73-03 은 등록 3지점이 모두 동일 술어 `IsSequenceActive` 를 쓰도록 유지됐다.

## Follow-up (다음 plan 확인 필요)

1. **PC2 실기동 로그 확인** — SIDE_1~4 네 시퀀스 생성 흔적. 이 plan 은 컴파일까지만 검증했다(앱 기동 UAT 미수행).
2. **R1 LIGHT_BAR 공유 소등 충돌** — SIDE_1~4 가 같은 `LIGHT_BAR` 그룹을 쓰게 되어 D-73-07 R1 조건이 이번 커밋으로 실제 성립했다. 후속 plan 의 (a)안(형제 시퀀스 non-Idle 채널 제외) 적용 전까지는 지그 간 소등 간섭 가능.
3. **`TeachingStorageService.ResolveDatumModelPath`** — OwnerSequenceName 이 `SIDE_1` 로 바뀐 뒤 실제 산출 경로를 로그로 확인(D-73-03 후단).

## Self-Check: PASSED

수정 파일 3개 + SUMMARY 존재 확인, 커밋 3개(aafaa50 / 5bd007e / 6f8efda) 존재 확인.
