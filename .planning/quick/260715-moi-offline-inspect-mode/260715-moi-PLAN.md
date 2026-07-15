---
phase: quick-260715-moi
plan: 01
subsystem: inspection-runtime (수동/오프라인 검사)
tags: [halcon, wpf, offline-inspect, manual-jig, image-source]

# Dependency graph
requires:
  - Action_FAIMeasurement EStep.Grab / GrabOrLoadDatumImage (SIMUL 로드 경로 재사용)
  - ShotConfig.SimulImagePath / DatumConfig.TeachingImagePath (노드별 저장 이미지 경로)
provides:
  - OfflineInspectMode 런타임 플래그 — 실 카메라에서도 라이브 grab 대신 노드별 저장 이미지로 검사
  - 노드별 "검사이미지 Grab" 버튼(옵션 B) — 1클릭 라이브 grab + 해당 노드 경로에 저장
affects: [수동 지그 검사 워크플로 — Z모터 없이 사람이 Z 돌리며 datum/shot별 이미지 확보 후 RUN]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "오프라인 이미지 소스: 비-SIMUL 빌드에서도 OfflineInspectMode ON 이면 EStep.Grab=ShotParam.SimulImagePath 로드, GrabOrLoadDatumImage=DatumConfig.TeachingImagePath 로드. SIMUL_MODE 매크로 로드 경로와 동일 코드 공유."
    - "옵션 B (노드별 검사이미지 확보): InspectionListView 트리에서 Datum/Shot 노드 선택 → '검사이미지 Grab' → 그 노드 카메라 파라미터로 라이브 grab → 결정적 파일경로 저장 → 노드 경로 필드에 반영. 재-grab 시 덮어씀."

key-files:
  created: []
  modified:
    - WPF_Example/Setting/SystemSetting.cs           # OfflineInspectMode 플래그
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs  # 오프라인 로드 분기
    - WPF_Example/UI/ControlItem/InspectionListView.xaml               # "검사이미지 Grab" 버튼
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs            # 버튼 핸들러 + grab/save

key-decisions:
  - "플래그 위치 = SystemSetting(영속). 수동 지그 현장은 상시 오프라인일 확률이 높아 영속이 자연스럽고, Settings 창에 노출돼 조작자가 상태를 본다. 상시 켜져 오검 위험 → 켜졌을 때 로그 + (v1) 최소한 로그로 경고."
  - "트리거는 신규 없음 — 기존 RUN(btn_start=Sequences.Start) 이 수동 검사 트리거. 오프라인 플래그는 '이미지 소스'만 바꾼다(트리거 직교). RUN(Sequence 노드)=전 shot 실행, RUN(Shot 노드)=해당 shot."
  - "저장 포맷/경로: <ImageSavePath>\\OfflineInspect\\<recipe>\\ 아래 결정적 파일명(datum=DatumName, shot=ShotName). HImage.WriteImage. 재-grab 덮어씀 → 경로 안정."
  - "옵션 B 선택(사용자 확정): 노드별 전용 버튼 1클릭. 별도 batch/마법사 없음(후속)."
  - "각 shot 은 자기 Z 이미지, datum 은 자기(초점맞는) 이미지 → 라이브 grab 이 Z별로 datum transform 을 매 shot 재계산하며 무너지던 문제를 오프라인이 우회(각 이미지가 이미 올바른 Z)."

requirements: [OFFLINE-01, OFFLINE-02, OFFLINE-03]

# Metrics
started: 2026-07-15
---

# Quick Task 260715-moi: 수동/오프라인 검사 모드 (옵션 B)

## 배경 / 문제
- 실 카메라이나 **Z축이 사람 손 메뉴얼 지그**(모터 없음, IAxisController 는 placeholder).
- 라이브 grab 검사: 한 번의 $TEST/RUN 이 **모든 shot 을 한 물리 Z 에서** 실행 → shot 별로 datum 을 자기 이미지에서 재-find 하며 transform 이 shot 마다 초기화 → 서로 다른 Z 의 shot 들이 공유 datum 과 정합 불가.
- **해결**: 오프라인 모드 = 사람이 Z 를 datum 위치로 맞춰 **datum 이미지 grab/저장**, 각 shot 의 Z 로 맞춰 **shot 이미지 grab/저장** → 검사 시 라이브 grab 대신 그 저장 이미지들을 로드. 각 이미지가 이미 올바른 Z(초점) 라 정합 성립.

## 목표 (범위)
1. **OFFLINE-01** `SystemSetting.OfflineInspectMode` 플래그(영속). 켜지면 비-SIMUL 빌드도 검사 시 노드별 저장 이미지 로드.
2. **OFFLINE-02** `Action_FAIMeasurement` 의 비-SIMUL grab 경로 2곳(EStep.Grab, GrabOrLoadDatumImage)에 오프라인 로드 분기.
3. **OFFLINE-03** InspectionListView 에 노드별 **"검사이미지 Grab"** 버튼 — Datum/Shot 노드에서 1클릭 라이브 grab + 결정적 경로 저장 + 노드 경로 필드 반영.

## 범위 밖 (후속)
- 오토 모드(Z 모터 + TCP 로 Z 이동) — 하드웨어 부재로 보류.
- 여러 노드 batch grab 마법사 / 진행 표시.
- 오프라인 상태 화면 대형 인디케이터(v1 은 Settings 값 + 로그).

## 설계

### 1) 플래그 (OFFLINE-01)
`SystemSetting.cs` 에 추가:
```csharp
private bool _offlineInspectMode = false;
[Category("System|Inspection")]
[DisplayName("Offline Inspect Mode")]
[Description("ON: 실 카메라에서도 라이브 grab 대신 노드별 저장 이미지로 검사 (수동 지그).")]
public bool OfflineInspectMode { get { return _offlineInspectMode; } set { _offlineInspectMode = value; } }
```
- ParamBase/INI 자동 직렬화 대상인지 확인(SystemSetting 은 자체 Save/Load 방식). bool 저장/로드 경로 따를 것. 누락 키 → false 기본.

### 2) 오프라인 로드 분기 (OFFLINE-02)
`Action_FAIMeasurement.cs`:
- **EStep.Grab (~192-226)**: 현재
  ```
  #if SIMUL_MODE   load ShotParam.SimulImagePath
  #else            image = Devices.GrabHalconImage(ShotParam)
  #endif
  ```
  → 비-SIMUL 분기를 `if (SystemSetting.Handle.OfflineInspectMode) { load ShotParam.SimulImagePath } else { grab }` 로. SIMUL 로드 코드와 공유 헬퍼(`LoadImageFromPath(path)`)로 추출.
- **GrabOrLoadDatumImage (~322-340)**: 동일 — 비-SIMUL 에서 offline ON 이면 `DatumConfig.TeachingImagePath`(폴백 SimulImagePath) 로드.
- 경로 없음/파일 없음 → 명확한 에러(Logging + FinishAction(Error) or 기존 실패 규약). 라이브 grab 처럼 조용히 실패하지 말 것.

### 3) "검사이미지 Grab" 버튼 (OFFLINE-03)
`InspectionListView.xaml`: btn_start(RUN) 인접에 `btn_grabInspImage` "검사이미지 Grab".
`InspectionListView.xaml.cs` 핸들러:
1. 선택 노드 → `DatumConfig` 또는 `ShotConfig`(ICameraParam) 판정. 그 외 → 안내 후 return.
2. 카메라 파라미터 확보(datum=ResolveDatumCameraParam 상당 / shot=ShotConfig 자체).
3. `image = SystemHandler.Handle.Devices.GrabHalconImage(param)`.
4. 저장 폴더 `<ImageSavePath>\OfflineInspect\<recipe>\` 생성.
5. 파일명 datum=`<DatumName>.png` / shot=`<ShotName>.png`. `image.WriteImage("png", 0, path)`.
6. 노드 경로 반영: datum → `TeachingImagePath=path`, shot → `SimulImagePath=path`. 레시피 저장(dirty/Save).
7. MainView 에 표시 갱신(선택 노드 재렌더). 성공 토스트/메시지.
- 조명: grab 전 그 노드 조명 적용(datum=ApplyDatumLights, shot=ApplyShotLights) — 기존 GrabAndDisplay 경로 재사용 가능하면 그걸로.

## 검증
- 앱 종료 후 Debug|x64 빌드 0 errors, 신규 warning 0.
- 규칙: 삼항 `?:` 0, C# 8+ 0, 신규 주석 서명 0.
- HUMAN-UAT(실 HW): datum Z 맞춰 Grab → 각 shot Z 맞춰 Grab → OfflineInspectMode ON → RUN(Sequence) → 각 측정 PASS/정합 육안 확인. OFF 로 되돌리면 라이브 grab 복귀.

## 리스크 / 안전장치
- 오프라인 ON 상태로 방치 시 stale 이미지 오검 → 켜질 때 Logging 경고, Settings 노출.
- 경로 미설정 shot/datum 을 오프라인 검사 → 조용한 실패 금지, 명확 에러.
- 코어 검사 흐름(EStep) 수정 → 분기만 추가, SIMUL/라이브 기존 경로 무변경 보장.
