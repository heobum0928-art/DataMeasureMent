---
phase: quick-260714-qbz
plan: 01
subsystem: inspection-teaching (Datum 조명)
tags: [halcon, wpf, propertygrid, light-controller, datum]

# Dependency graph
requires:
  - phase: quick-260713-nse
    provides: ShotConfig Ring 6채널 + Bar 4채널 개별 조명 프로퍼티 + LightHandler.SetChannelOnOff/SetChannelLevel + InspectionSequence.ApplyChannelLight
provides:
  - DatumConfig Ring 6채널 + Bar 4채널 + Back/Ring7/Coax 개별 밝기(0~255)/On-Off 조명 프로퍼티 20개
  - InspectionSequence.ApplyDatumLights(DatumConfig) — ApplyShotLightsInternal 과 동일 채널 매핑을 Datum 소스로 재사용
  - Action_FAIMeasurement.EStep.DatumPhase 훅 — datum grab 직전 ApplyDatumLights, 루프 종료 후 ApplyShotLights 로 Shot 조명 복원
  - MainView.GrabAndDisplay(ICameraParam, DatumConfig, bool) 오버로드 — 티칭 Grab 시 Datum 조명 적용(기존엔 LightGroupName 미설정으로 조명 자체가 무동작이었음)
  - PropertyGrid "Light" 탭 분리 (Category="Light|..." 접두사, ShotConfig 와 동일 관례) — Datum 탭 클러터 해소
  - CameraSlaveParam.LightGroupName/LightLevel Browsable(false) — legacy 단일그룹 잔재 숨김
  - InspectionListView 툴바 "Light" 아이콘 버튼 Visibility=Collapsed — 채널별 Light 탭으로 대체되어 숨김(코드 보존)
affects: [DatumConfig PropertyGrid, InspectionListView 툴바, LIGHT-CHANNEL-DESIGN.md]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "PropertyTools.Wpf PropertyGrid 의 Category=\"탭명|그룹명\" 규칙 — 탭명이 같으면 같은 최상위 탭으로 묶이고, 다르면 별도 탭으로 분리된다(ParamEditor TabHeaderTemplate). ShotConfig 가 이미 \"Shot|Light|General|Device\" 로 갈리는 것과 동일 메커니즘을 DatumConfig 에도 적용해 \"Datum|Light\" 두 탭으로 분리."
    - "ApplyShotLightsInternal/ApplyDatumLightsInternal 은 완전 동일 채널 매핑을 서로 다른 소스 객체(ShotConfig/DatumConfig)에 적용 — 공용 ApplyChannelLight 헬퍼 재사용, 코드 복제는 두 Internal 메서드 본문 수준에서만 허용(설정 소스 타입이 달라 제네릭/인터페이스 추출은 이번 스코프 밖)."

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
    - WPF_Example/UI/ControlItem/InspectionListView.xaml
    - WPF_Example/Sequence/Param/CameraSlaveParam.cs

key-decisions:
  - "티칭 Grab 오버로드는 기존 GrabAndDisplay(ICameraParam, bool) 를 리팩터링하지 않고 완전히 별도 오버로드로 추가 — 본문 일부 중복을 감수하는 대신 기존(이미 검증된) 비-Datum 호출 경로 회귀 위험을 0으로 유지(계획에 명시된 권장안)."
  - "Datum 조명 UI는 처음에 전용 팝업 창(DatumLightWindow)으로 구현했으나, 사용자가 '기존 FAI/측정 부분처럼 탭이 붙어있길' 요청 — PropertyTools 의 Category 탭 분리 규칙을 활용해 ShotConfig 와 동일한 네이티브 탭 방식으로 전환하고 팝업은 전량 되돌림(파일 삭제 + csproj 등록 해제)."
  - "CameraSlaveParam.LightGroupName/LightLevel(구 단일그룹 API) 은 ShotConfig/TopInspectionParam/BottomInspectionParam 어디서도 세팅된 적이 없음을 grep 으로 확인 후 Browsable(false) 처리 — CameraMasterParam(시퀀스 루트 노드) 에서는 여전히 DefaultLight 로 세팅/사용되므로 그쪽 클래스는 건드리지 않음(CameraSlaveParam 파생 클래스만 영향)."
  - "InspectionListView 툴바 Light 아이콘은 Visibility=Collapsed 로 숨김(삭제 아님) — 이미 IsEnabled=False 로 시작하고 클릭 핸들러 자체는 여전히 유효(CameraMasterParam 선택 시 등 잔여 경로 보존), 추후 재노출이 필요하면 XAML 한 줄만 되돌리면 됨."

requirements-completed: [DATUM-LIGHT-01]

# Metrics
duration: ~90min (팝업→탭 전환 왕복 포함)
completed: 2026-07-14
---

# Quick Task 260714-qbz: Datum 전용 per-channel 조명 Summary

**DatumConfig 에 Ring 6채널 + Bar 4채널 + Back/Ring7/Coax 개별 조명 필드를 추가하고, 티칭 Grab + 런타임 DatumPhase 양쪽에 실제로 적용되도록 배선. PropertyGrid 는 ShotConfig 와 동일한 "Light" 네이티브 탭으로 분리.**

## Performance
- **Duration:** ~90 min (Task 1~5 실행 + 사용자 피드백에 따른 UI 방식 전환 2회 포함)
- **Completed:** 2026-07-14
- **Tasks:** 5 (PLAN 상 4 코드 작업 + 1 검증) + 후속 UI 개선 3건(사용자 피드백 기반, PLAN 범위 밖)
- **Files modified:** 7 (+ 팝업 시도 3개 파일 생성 후 삭제, 최종 diff 에는 남지 않음)

## Accomplishments

### PLAN Task 1~4 (원 계획대로 실행)
- `DatumConfig`에 `RingLight_Enabled_1~6`/`Brightness_1~6`(Slidable 0~255, backing-field+RaisePropertyChanged), `SideLight_Enabled_1~4`/`Brightness_1~4`, `BackLight_*`, `Ring7Light_*`, `CoaxLight_*` — 총 20개 프로퍼티 추가
- `InspectionSequence.ApplyDatumLights(DatumConfig)` + `ApplyDatumLightsInternal` 신설 — `ApplyShotLightsInternal`과 완전히 동일한 채널 매핑을 Datum 소스로 재사용(`ApplyChannelLight` 공유)
- `Action_FAIMeasurement.cs`의 `EStep.DatumPhase` 루프에서 `datum` null 체크 직후 `parentSeq.ApplyDatumLights(datum)` 호출(1-image/DualImage 두 grab 경로 모두 공통 커버), 루프 종료 후 `ShotParam.ZIndex` 로 `parentSeq.ApplyShotLights(...)` 재호출해 Shot 조명 복원(EStep.Grab 의 측정 grab 이 Datum 조명이 아니라 Shot 조명 아래서 이뤄지도록 보장)
- `MainView.GrabAndDisplay(ICameraParam, DatumConfig, bool)` 오버로드 신설 — Datum 이면 `ApplyDatumLights`, 아니면 기존 `pLight.ApplyLight(param)` 그대로. `InspectionListView.button_grab_Click` 이 `datumForGrab` 을 이 오버로드로 전달하도록 갱신

### 후속 UI 개선 (세션 중 사용자 피드백 3건 반영, PLAN 범위 확장)
1. **팝업 → 네이티브 탭 전환**: 최초엔 `DatumLightWindow` 전용 팝업 창으로 구현했으나 "Datum 탭 옆에 Light 탭이 붙어있길 원한다"는 피드백을 받고, PropertyTools.Wpf 의 `Category="탭명|그룹명"` 규칙(ShotConfig 가 이미 "Shot|Light|General|Device" 로 탭이 갈리는 것과 동일 메커니즘)을 활용해 DatumConfig 조명 카테고리를 `"Datum|Light Ring"` → `"Light|Ring"` 등으로 변경 — Datum 선택 시 PropertyGrid 상단에 "Datum | Light" 두 탭이 자동 분리. 팝업 관련 파일(`DatumLightWindow.xaml(.cs)`, `DatumLightChannelViewModel.cs`) 및 버튼 배선은 전량 원복
2. **`CameraSlaveParam.LightGroupName`/`LightLevel`** — ShotConfig 계열에서 한 번도 세팅된 적 없는 legacy 단일그룹 API 잔재를 `[Browsable(false)]`로 숨김(INI 직렬화/CopyTo/실제 동작 무영향, PropertyGrid 표시만 제거)
3. **InspectionListView 툴바 "Light" 아이콘 버튼** — 이제 Shot/Datum 둘 다 채널별 Light 탭이 PropertyGrid 에 있으므로 중복/혼란 소지가 있는 legacy 단일그룹 토글 버튼을 `Visibility="Collapsed"`로 숨김(코드/핸들러 보존)

## Task Commits

커밋 없음 — 이번 세션에서 git commit 을 명시적으로 요청받지 않아 워킹트리에만 반영됨. 커밋이 필요하면 별도 요청 바람.

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — 조명 프로퍼티 20개(Category="Light|...") + `IsHiddenForAlgorithm` 은 무변경(팝업 시도 때 추가했던 hide 로직은 되돌림)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — `ApplyDatumLights`/`ApplyDatumLightsInternal` 신설, 기존 Shot 조명 메서드 3종 무변경
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — DatumPhase 훅 2곳(적용/복원), EStep.Grab/Measure/End 무변경
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — `GrabAndDisplay` 신규 오버로드(기존 오버로드 무변경)
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — `button_grab_Click` 이 `datumForGrab` 전달, `button_light_Click`/선택 핸들러는 원상태(Datum 분기 없음)
- `WPF_Example/UI/ControlItem/InspectionListView.xaml` — `button_light` Visibility=Collapsed
- `WPF_Example/Sequence/Param/CameraSlaveParam.cs` — `LightGroupName`/`LightLevel` Browsable(false)

## Deviations from Plan

**UI 표현 방식 변경 (PLAN 미명시 영역).** PLAN 은 "PropertyGrid 에 조명 필드가 표시된다"까지만 must_have 로 명시했고 구체적 표현 방식(탭/그룹/팝업)은 규정하지 않았음. 최초 구현(Datum 탭 내부 그룹)이 사용자 기준 "너무 복잡함" 판정을 받아 팝업 → 네이티브 탭(최종) 순으로 2차 반복. 데이터 필드/직렬화/티칭·런타임 배선(PLAN Task 1~4 핵심)은 이 반복 동안 전혀 변경되지 않음 — 오직 PropertyGrid 표시 방식(Category 문자열)만 조정됨.

**추가 정리(Task 5 회귀 감사 중 발견, PLAN 범위 밖).** `CameraSlaveParam.LightGroupName`/`LightLevel` 은닉 및 툴바 Light 버튼 숨김은 PLAN 에 없던 사용자 요청으로, 별도 회귀 위험 없는 순수 UI 정리라 같은 세션에서 함께 처리.

## Regression Guard (git diff 확인)

| 항목 | 결과 |
|---|---|
| `ApplyShotLightsInternal`/`ApplyShotLights`/`TurnOffShotLights`/`ApplyChannelLight` 본문 | 무변경(추가만) |
| `LightHandler.cs` | 무변경(diff 없음) |
| `Action_FAIMeasurement.cs` EStep.Grab/Measure/End | 무변경 — DatumPhase 케이스 내부에만 2줄 삽입 |
| 기존 `GrabAndDisplay(ICameraParam, bool)` 1-인자 오버로드 | 무변경 — 신규 오버로드는 별도 메서드 |
| ShotConfig.cs | 무변경(이번 작업 대상 아님, 이전 세션에서 별도 수정) |

## Build Verification

Debug|x64 MSBuild — 매 Task 후 및 최종 Rebuild 전부 **0 errors**. 신규 경고 0건(기존 CS0618 baseline 5건만 잔존, Phase 33 마이그레이션 관련 무관 경고).

## User Setup Required

None.

## HUMAN-UAT 대기 항목

1. **Datum PropertyGrid "Light" 탭 육안 확인** — Datum 노드 선택 시 "Datum | Light" 탭이 Shot 의 "Shot | Light | General | Device" 와 같은 방식으로 표시되는지, 슬라이더 드래그 시 숫자칸도 즉시 갱신되는지 (사용자 1차 확인: "잘나와" — PASS 보고됨, 정식 UAT 체크는 미완료)
2. **실 하드웨어에서 Datum 티칭 Grab 시 지정 채널 실제 점등 확인** — `GrabAndDisplay(resolved, datumForGrab)` 경로가 실제로 해당 채널만 켜는지, 다른 채널에 간섭 없는지
3. **런타임 `$TEST` 사이클에서 DatumPhase 조명 → Shot 조명 복원 확인** — Datum 조명이 켜진 채로 `EStep.Grab`(측정 이미지)이 찍히지 않는지, `ShotParam.ZIndex` 기반 복원이 여러 Shot/여러 Datum 조합에서도 정확한지
4. **Device 탭에서 Light 그룹 사라짐 확인 + 툴바 Light 아이콘 숨김 확인** — 두 UI 정리가 다른 기능에 영향 없는지

## Next Phase Readiness

- 코드 레벨 구현/배선/회귀 가드는 전부 완료, build PASS.
- HUMAN-UAT 4건 잔존 — 특히 2·3번(실 하드웨어 조명 검증)은 시뮬레이션으로 대체 불가.
- Blocker 없음.

---
*Phase: quick-260714-qbz*
*Completed: 2026-07-14*
