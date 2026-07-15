---
phase: quick-260715-moi
plan: 01
subsystem: inspection-runtime (수동/오프라인 검사)
tags: [halcon, wpf, offline-inspect, manual-jig, image-source]

# Dependency graph
requires:
  - Action_FAIMeasurement EStep.Grab / GrabOrLoadDatumImage (SIMUL 로드 경로)
  - ShotConfig.SimulImagePath / DatumConfig.TeachingImagePath (IOfflineImageParam.SetLatestImagePath)
  - MainView GrabAndDisplay(datum) 조명 규약 / LoadAndDisplay 경로기록 규약
provides:
  - SystemSetting.OfflineInspectMode 런타임 플래그(영속 INI)
  - Action_FAIMeasurement 오프라인 로드 분기 + 공용 헬퍼(LoadShotInspectionImage / LoadDatumImageFromPath)
  - MainView.GrabSaveAndDisplay(라이브 grab → png 저장 → 노드 경로 기록 → 표시)
  - InspectionListView "검사Grab" 버튼(button_grabInsp) + BuildOfflineImagePath
affects: [Z 모터 없는 수동 지그 검사 워크플로]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "오프라인 이미지 소스: 실HW 빌드(Debug|x64=SIMUL_MODE off)에서 OfflineInspectMode ON 이면 EStep.Grab=LoadShotInspectionImage(ShotParam.SimulImagePath), GrabOrLoadDatumImage=LoadDatumImageFromPath(TeachingImagePath→SimulImagePath, grab폴백 없음). SIMUL_MODE 매크로 로드 경로와 헬퍼 공유 → 두 빌드 동작 일치."
    - "옵션 B 검사이미지 확보: InspectionListView 트리 Datum/Shot 노드 → '검사Grab' → GrabSaveAndDisplay 로 노드 카메라 라이브 grab → <ImageSavePath>\\OfflineInspect\\<recipe>\\<node>.png 저장(WriteImage png, 확장자 포함 경로) → SetLatestImagePath 반영 → grab 완료 await 후 RefreshParamEditor."

key-files:
  created: []
  modified:
    - WPF_Example/Setting/SystemSetting.cs                              # OfflineInspectMode bool
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs   # 오프라인 로드 분기 + 헬퍼 2종
    - WPF_Example/UI/ContentItem/MainView.xaml.cs                       # GrabSaveAndDisplay(async Task)
    - WPF_Example/UI/ControlItem/InspectionListView.xaml               # button_grabInsp
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs            # 핸들러 + BuildOfflineImagePath + SanitizeFileName + 게이팅
    - WPF_Example/VersionDefine.cs                                      # 1.5.0.0 → 1.6.0.0 changelog

key-decisions:
  - "플래그=SystemSetting 영속(수동 지그는 상시 오프라인일 확률 큼, Settings 노출로 조작자 인지). 누락 INI 키 → false(ToBool 기본) 로 기존 레시피 라이브 grab 유지 — 하위호환 Load 오버라이드 불필요."
  - "트리거 신규 없음 — 기존 RUN(btn_start=Sequences.Start) 이 수동 검사 트리거. 오프라인 플래그는 '이미지 소스'만 바꿈(직교). RUN(Sequence)=전 shot, RUN(Shot)=해당 shot."
  - "옵션 B(노드별 전용 버튼 1클릭) 사용자 확정. GrabSaveAndDisplay 는 GrabAndDisplay(datum) 조명·표시 규약 + LoadAndDisplay 경로기록 규약(DualImage 세로 토글 특례 포함)을 합성 — 별도 신규 규약 도입 없음."
  - "오프라인 datum 은 grab 폴백 금지(allowGrabFallback=false) — 저장 이미지 부재 시 null 반환해 datum Find 실패로 명확히 드러냄(잘못된 Z 라이브 grab 은폐 방지). SIMUL 은 기존대로 grab 폴백 유지(allowGrabFallback=true)."
  - "저장 경로 결정적(<recipe>\\<node>.png) → 재-grab 시 덮어써 경로 안정. 파일명 SanitizeFileName 으로 무효문자 치환."

requirements-completed: [OFFLINE-01, OFFLINE-02, OFFLINE-03]

# Metrics
completed: 2026-07-15
---

# Quick Task 260715-moi: 수동/오프라인 검사 모드 (옵션 B) Summary

**Z 모터 없는 수동 지그: 사람이 datum/shot Z 를 맞춰 '검사Grab' 으로 이미지를 확보하면, OfflineInspectMode 검사가 라이브 grab 대신 그 저장 이미지들을 로드해 측정한다. 각 이미지가 이미 올바른 Z(초점)라, 라이브 grab 이 shot 마다 datum transform 을 재계산하며 무너지던 공유-datum 정합 문제를 우회한다.**

## Accomplishments
- **OFFLINE-01 플래그**: `SystemSetting.OfflineInspectMode`(영속, [Category("System|Enviroment")]). 누락 키 → false.
- **OFFLINE-02 로드 분기**: `Action_FAIMeasurement`
  - `LoadShotInspectionImage()` — SHOT 검사 이미지 단일소스(SimulImagePath), 실패 시 null(공유 카메라 캐시 fallback 차단, 캐스케이드 방지).
  - `LoadDatumImageFromPath(datum, path, allowGrabFallback)` — TeachingImagePath→SimulImagePath, 오프라인은 grab 폴백 없음.
  - EStep.Grab / GrabOrLoadDatumImage 의 비-SIMUL 경로에 `if (OfflineInspectMode) 로드 else grab` 분기. SIMUL 경로도 동일 헬퍼로 통일.
- **OFFLINE-03 검사Grab 버튼**: `button_grabInsp`(Grab/Load 인접 툴바, Datum/Shot 노드에서 활성 — button_grab 과 동일 게이팅).
  - `MainView.GrabSaveAndDisplay(displayParam, datum, pathSink, savePath)` — 조명 적용(datum=ApplyDatumLights / shot=ApplyShotLightsDirect) → WaitForPendingWrites → grab → png 저장 → 표시 성공 시 경로 기록 → 캐시 동기화.
  - `BuildOfflineImagePath` — `<ImageSavePath>\OfflineInspect\<recipe>\<node>.png`, 폴더 자동생성, SanitizeFileName.
  - 핸들러가 `async void` 로 `await GrabSaveAndDisplay` 후 `RefreshParamEditor` → 경로 write-back 이 PropertyGrid 에 반영.

## Build Verification
- **Debug|x64 (실HW, SIMUL_MODE off)** — 내 오프라인 `#else` 분기가 컴파일되는 경로. Build **0 errors, 0 warnings**.
- **Debug|AnyCPU (SIMUL_MODE on)** — SIMUL `#if` 분기(공용 헬퍼 호출)도 컴파일 확인. Build **0 errors**, 신규 warning 0(기존 TopSequence/VirtualCamera obsolete/unreachable 경고만).
- VersionDefine 1.6.0.0 changelog 문자열(연결/이스케이프) 포함 재빌드 0 errors.

## Rule Audit
삼항 `?:` 0 / C# 8+ 0 / 신규 `//YYMMDD hbk` 서명 0. 기존 파일 스타일(if-else, Allman/K&R 혼재는 파일별 유지) 준수.

## Regression Guard
- SIMUL 동작 보존: LoadShotInspectionImage=기존 SHOT 로드 규약(null-on-fail), LoadDatumImageFromPath(...,true)=기존 datum(teaching→simul→grab) 규약 동일.
- 비-오프라인(플래그 OFF) 실HW: EStep.Grab/GrabOrLoadDatumImage 모두 기존과 동일하게 라이브 grab.
- DualImage datum 은 원래 TryGrabOrLoadDualDatumImages 로 항상 파일 로드 → 변경 불필요(무변경).

## HUMAN-UAT 대기 (실 HW 필요)
1. **셋업**: datum Z 맞춤 → Datum 노드 '검사Grab' (저장 확인) → 각 shot Z 맞춤 → Shot 노드 '검사Grab'.
2. **오프라인 검사**: Settings 에서 OfflineInspectMode ON → RUN(Sequence) → 각 측정 PASS/정합 육안 확인. 저장 이미지로 측정되는지(라이브 grab 아님).
3. **경로 부재 처리**: '검사Grab' 안 한 shot/datum 을 오프라인 검사 → 조용한 오검 없이 해당 SHOT skip / datum Find 실패 로그 확인.
4. **복귀**: OfflineInspectMode OFF → 라이브 grab 정상 복귀.
5. **재-grab 덮어쓰기**: 같은 노드 '검사Grab' 재실행 시 같은 경로 덮어쓰고 PropertyGrid 경로 유지.

## 후속(범위 밖)
- 오토 모드(Z 모터 + TCP Z 이동) — IAxisController placeholder, 하드웨어 부재로 보류.
- 여러 노드 batch '검사Grab' 마법사/진행 표시.
- 오프라인 상태 화면 대형 인디케이터(현재 Settings 값 + Grab 시 라벨 + 로드 실패 로그).

---
*Phase: quick-260715-moi*
*Completed: 2026-07-15*
