---
phase: quick-260810-egx
plan: 01
subsystem: inspection-display
tags: [tact, viewer, halcon-memory, opt-in-setting]
requires: []
provides:
  - "SystemSetting.DisableViewerDuringAutoInspect (opt-in, 기본 false)"
  - "자동검사 사이클 표시 전용 127MP 복사 2회/Shot 제거 경로"
affects:
  - WPF_Example/Setting/SystemSetting.cs
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
  - WPF_Example/MainWindow.xaml.cs
tech-stack:
  added: []
  patterns: ["시퀀스 스레드에서 판정 캡처 후 Dispatcher 람다에서 로컬만 사용"]
key-files:
  created: []
  modified:
    - WPF_Example/Setting/SystemSetting.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
    - WPF_Example/MainWindow.xaml.cs
decisions:
  - "극성을 DisableViewer...=false 로 잡아 기존 설치본(INI 키 누락→false 강제)이 자동으로 기존 동작 유지"
  - "skip 조건에 !SaveFailImage 가드 포함 — ResultHalconImage 가 저장 소스가 되는 경우를 코드로 배제"
  - "저장 경로(QueueFaiCapture/CaptureImageSaveService/NeedsRender/SharedHImage)는 한 줄도 수정하지 않음"
metrics:
  tasks: 4
  files: 3
  completed: 2026-08-10
---

# Quick 260810-egx: 자동검사 중 실시간 화면 표시 OFF 모드 Summary

자동검사(TCP `$PREP`/`$TEST`) 사이클에서만 화면 실시간 표시를 끄는 opt-in 설정을 추가해, 표시 목적의 127MP 이미지 복사 2회/Shot 과 UI 뷰어 로드를 제거했다. 저장되는 capture/original 이미지는 OK/NG 전부 기존과 완전히 동일하다.

## What Changed

### Task 1 — `WPF_Example/Setting/SystemSetting.cs`
`OfflineInspectMode` 바로 아래에 `[Category("System|Enviroment")] public bool DisableViewerDuringAutoInspect { get; set; } = false;` 추가. 설정창(PropertyTools)에 노출된다. 주석에 (a) 존재 이유, (b) 저장 이미지는 이 설정과 무관하다는 오해 방지 문구, (c) INI 키 누락 시 false 로드라 기존 설치본은 자동으로 기존 동작 유지 를 명시.

### Task 2 — `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs`
`private bool IsViewerUpdateSkipped(InspectionSequence parentSeq)` 헬퍼 추가. 반환 true 조건은 3항 AND:
1. `parentSeq != null && parentSeq.IsProtocolDrivenCycle()` (자동 사이클)
2. `SystemSetting.Handle.DisableViewerDuringAutoInspect == true`
3. `SystemSetting.Handle.SaveFailImage == false`

3번이 데이터 경로 보호 가드다 — `SaveFailImage` ON 이면 `SequenceBase.SaveResultImage` 가 `Context.ResultHalconImage` 를 실제 저장 소스로 쓰므로 표시사본을 유지한다. `Setting` null 은 false(기존 동작) 폴백.

적용 2곳:
- **`EStep.Grab`**: `ShotParam.SetImage(image)`(측정 소스)와 `image.Dispose()`(누수 방지)는 무조건 수행. 그 사이의 표시사본만 게이트 — skip 이면 기존 `ResultHalconImage` 를 Dispose 하고 `null` 로 두며 `image.CopyImage()` 를 실행하지 않는다.
- **크로스-Z 표시 교체 블록**: `if (!bShotDisplayImageReplaced && !IsViewerUpdateSkipped(parentSeq2))` 로 게이트. `AggregateFaiResult`/`crossZSharedSrc.Release()`/`crossZRoleImage.Dispose()` 는 조건과 무관하게 그대로.

### Task 3 — `WPF_Example/MainWindow.xaml.cs`
`private bool ShouldSkipViewerUpdate(SequenceBase seq)` 헬퍼 추가(설정 ON AND `InspectionSequence` AND `IsProtocolDrivenCycle()`; 캐스팅 실패/Setting null 은 false).

- `OnSequenceFinish` / `OnSequenceError`: `context.Source` 로 판정, skip 이면 `mainView.DisplaySequenceContext(context)` 만 건너뜀.
- `OnActionChanged`: `context.Source.Param.Parent` 로 소유 시퀀스 획득, skip 이면 `mainView.DisplayActionContext(context)` 만 건너뜀.
- `OnSequenceStop`: Display 호출이 없어 무수정.

**경합 대응**: 판정 bool 을 핸들러 진입 직후(시퀀스 스레드)에서 계산해 로컬로 캡처하고, `Dispatcher.BeginInvoke` 람다 안에서는 그 로컬만 읽는다. UI 스레드 도달 시점엔 `RequestPacket` 이 클리어됐을 수 있어 람다 내 재계산은 판정을 뒤집는다.

## Verification

**빌드**: MSBuild Debug|x64, scratch OutputPath 로 빌드 → `error CS` **0건**. (경고는 기존 `CS0618` Obsolete 레거시 시퀀스 관련 5건으로 이번 변경과 무관.) 실행 중인 사용자 프로세스는 건드리지 않았다.

**정적 회귀 4건 (git diff 로 직접 확인):**

| 항목 | 결과 |
|------|------|
| `QueueFaiCapture` / `CaptureImageSaveService.cs` / `NeedsRender` / `SharedHImage` 코드 변경 | **0줄** (diff 내 유일한 매치는 설명 주석 1줄) |
| `ShotParam.SetImage(image)` / `image.Dispose()` | 무변경 — 조건과 무관하게 항상 실행 (주석만 추가) |
| `crossZSharedSrc.Release()` / `crossZRoleImage.Dispose()` | 무변경 (각 finally 그대로) |
| `SetManualToolsEnabled(true)` Finish/Error/Stop 3경로 | 무조건 호출 유지 — Display 호출만 게이트 |
| skip 조건 3항 (프로토콜 AND 설정 ON AND `!SaveFailImage`) | 존재 확인 |

**커밋 위생**: 이 PC 전용 미커밋 로컬 변경 2건(`DatumMeasurement.csproj` SIMUL_MODE 제거, `SystemHandler.cs` memory_allocator 주석)은 커밋 전 diff 를 스냅샷해두고 커밋 후 재비교 → **바이트 단위 동일, 워킹트리에 그대로 남아 있음**. 스테이징은 소스 3파일만 명시적으로 지정. 커밋에 파일 삭제 없음.

## tact 효과 — 아직 측정되지 않음

**측정값이 없으므로 "빨라졌다"는 주장을 하지 않는다.** 이번 변경이 확실히 제거하는 것은 "표시 목적의 127MP 메모리 복사 2회/Shot + 뷰어 로드"뿐이다. 오케스트레이터 로그 분석의 유력 가설이었던 "저장 워커의 오버레이 렌더(127MP, 실측 약 1초/장)가 CPU 를 점유해 측정 스레드를 굶긴다"는 **이번 변경으로 전혀 다루지 않았다** — 저장 렌더는 그대로 남아 있다. 따라서 1.2~1.3초 측정 간 공백이 이 변경만으로 해소된다는 보장은 없다.

### 실기 A/B 측정 절차 (사용자 수행)

1. 설정 **OFF** 상태로 자동검사 1사이클 → Trace 로그에서 측정(FitLine) 간 **최대 간격**과 **사이클 총 시간** 기록.
2. 설정을 **ON** 으로 바꾸고 동일 조건 1사이클 → 같은 두 지표 기록.

### 해석 규칙

- **간격이 눈에 띄게 줄면**: 표시용 복사가 실제 병목의 일부였다는 뜻. 설정을 ON 으로 운영.
- **간격이 여전히 1.2~1.3초로 남으면**: 이번 변경은 병목이 아니었다는 뜻이며, 원인은 저장 워커의 `OverlayCaptureRenderer.RenderToHImage`(오버레이 렌더) CPU 경합 쪽이다. 그 경우 다음 후보는:
  - (a) 저장 워커 스레드 우선순위 하향 / 스로틀 조정
  - (b) 렌더 해상도 축소 (원본 대신 축소본에 오버레이)
  - (c) 저장 렌더 자체를 사이클 종료 후로 지연

  **셋 중 어느 것도 이번 변경으로 다뤄지지 않았다.**

## 실기 UAT 체크리스트 (사용자 수행, 미수행)

1. 설정 OFF + 자동검사 → 지금과 동일하게 화면 갱신 (회귀 0)
2. 설정 ON + 자동검사 → 검사 중 화면 갱신 없음. 단 TCP 응답 P/F, 측정값, 엑셀/cycle.json, capture/original 폴더의 **파일 수·이름이 OFF 때와 동일**
3. 설정 ON + **수동 RUN 버튼 / 티칭 / 일괄검사** → 여전히 실시간 표시됨
4. 설정 ON + 자동검사 종료 후 트리/노드 클릭 → 결과 이미지·오버레이 정상 표시
5. `SaveFailImage` ON + 설정 ON + 자동검사 → 결과이미지 저장이 기존대로 동작 (T-EGX-01 가드 검증)

## Deviations from Plan

None — 플랜대로 실행됨.

## Known Stubs

없음.

## Self-Check: PASSED

- `WPF_Example/Setting/SystemSetting.cs` — FOUND, `DisableViewerDuringAutoInspect` 포함
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — FOUND, `IsViewerUpdateSkipped` + 게이트 2곳 포함
- `WPF_Example/MainWindow.xaml.cs` — FOUND, `ShouldSkipViewerUpdate` + 게이트 3곳 포함
- 커밋 `19aac72` — FOUND (`git log`)
- 로컬 미커밋 2건 보존 — 확인됨
