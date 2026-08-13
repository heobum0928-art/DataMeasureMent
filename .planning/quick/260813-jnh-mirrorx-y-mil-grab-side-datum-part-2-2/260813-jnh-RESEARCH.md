# Quick 260813-jnh: MirrorX/Y → MIL grab 방향 배선 (Side Datum 미러 Part 2/2) — Research

**Researched:** 2026-08-13
**Domain:** 이 저장소 내부 코드 추적 (카메라 grab 호출 체인 / 앱 시작 순서 / 레시피 로드 타이밍)
**Confidence:** HIGH (모든 핵심 결론이 실제 소스 파일 + 실제 레시피 INI 실측으로 검증됨. 외부 문서 의존 0)

---

## Summary

Grab 경로를 끝까지 추적한 결과 **핵심 발견 3가지**가 나왔다.

1. **티칭 경로와 생산(검사) 경로는 서로 다른 UI/트리거를 갖지만, 결국 완전히 동일한 단 하나의 병목 함수로 수렴한다** — `DeviceHandler.GrabHalconImage(ICameraParam param)` (`WPF_Example/Device/DeviceHandler.cs:329-335`). 이 한 함수가 카메라 조회와 `requestIdentifier` 전달을 동시에 담당한다. 즉 "티칭용 훅"과 "생산용 훅"을 따로 만들 필요가 없다. `[VERIFIED: 코드 추적]`

2. **현재 `requestIdentifier`는 zIndex를 전혀 모른다.** `DeviceHandler.cs:334`가 `cam.GrabHalconImage(param.DeviceName)`로 넘기며, SIDE 경로에서 이 값은 항상 문자열 `"CAM_SIDE"` 하나뿐이다(실 레시피 `[SHOT_n_CAM] DeviceName=CAM_SIDE` 실측 확인). zIndex별 식별자는 **신설이 필요**하다. `[VERIFIED: DeviceHandler.cs:334 + D:\Data\Recipe\FAI_1\main.ini]`

3. **레시피는 카메라 초기화보다 훨씬 나중에 로드된다.** `Devices.Initialize()`는 `SystemHandler` **생성자**(`SystemHandler.cs:100-101`)에서 실행되고, 레시피 로드는 `MainWindow.Window_ContentRendered_LoadRecipe`(`MainWindow.xaml.cs:434-441`)에서 창이 렌더된 뒤에야 실행된다. 따라서 **"DeviceHandler 생성자에서 Datum을 스캔해 역할 등록"은 구조적으로 불가능**하다. `[VERIFIED: 코드 추적]`

**Primary recommendation:**
`DeviceHandler`에 **`GrabHalconImage(ICameraParam param, string requestIdentifier)` 2-인자 오버로드를 신설**하고, SIDE 카메라 등록 시 **미러 조합 4종(정상/X/Y/XY) 역할을 정적으로 미리 등록**한 뒤, grab 호출부에서 Datum(또는 Shot)의 미러 플래그로 식별자만 골라 넘긴다. 레시피 스캔·시작 순서 문제·`Devices` 딕셔너리 오염을 전부 회피하는 유일한 저위험 설계다(§5 참고).

---

## 1. 실제 grab 호출 체인 (file:line 기반)

### 1-A. 공통 병목 (양쪽 경로가 여기로 수렴)

```
WPF_Example/Device/DeviceHandler.cs:329-335
  public HImage GrabHalconImage(ICameraParam param) {
      VirtualCamera cam = this[param.DeviceName];      // ← 카메라 인스턴스 조회 (Devices 딕셔너리)
      if (cam == null) return null;
      if (cam.Properties == null) return null;
      if (!cam.Properties.ApplyFromParam(param)) return null;
      return cam.GrabHalconImage(param.DeviceName);    // ← requestIdentifier = DeviceName ("CAM_SIDE")
  }
        ↓
WPF_Example/Device/Camera/Mil/MilCamera.cs:253  GrabHalconImage(string requestIdentifier)
  :254-256   #if SIMUL_MODE → LastHalconImage 즉시 반환 (실기 grab 없음)
  :267-270   Streaming 중이면 null 반환 (라이브뷰 창 열려있으면 검사 grab 차단)
  :272       DeviceInfo roleInfo = ResolveRoleInfo(requestIdentifier);   ← 역할 해석 지점
  :276       GrabFromBuffer(roleInfo)
        ↓
MilCamera.cs:305-323  GrabFromBuffer(DeviceInfo roleInfo)
  :308-321   roleInfo.ReverseX/ReverseY → M_REVERSE / M_NORMAL
  :322-323   MIL.MdigControl(MilDigitizer, M_GRAB_DIRECTION_X / _Y, ...)   ← 실제 하드웨어 반전
  :326       MIL.MdigGrab(...)
```

> **중요:** `MdigControl(M_GRAB_DIRECTION_*)`는 **이미 매 grab 직전마다 재적용되고 있다**(`MilCamera.cs:322-323`, quick-260805-jtj에서 도입). `Open()`의 :194-195 설정은 초기값일 뿐이며 매 grab에서 덮어써진다. → **"per-grab 방향 전환"은 이 코드베이스의 기존 확립된 패턴이며 추가 비용이 0이다.** `[VERIFIED: MilCamera.cs:194-195, 305-323]`

### 1-B. 티칭 경로 (사용자가 트리에서 Datum/Shot 노드 선택 → Grab 버튼)

```
InspectionListView.xaml.cs:1162  button_grab_Click            (일반 Grab)
  :1164-1168  SelectedParam is DatumConfig → ResolveDatumCameraParam(datum) → MainView.GrabAndDisplay(resolved, datum)
  :1179-1180  그 외 ICameraParam(=ShotConfig) → MainView.GrabAndDisplay(camParam)

InspectionListView.xaml.cs:1185  button_grabInsp_Click        (검사이미지 Grab, 오프라인용)
  :1191-1196  Datum → GrabSaveAndDisplay(resolved, datum, datum, savePath)
  :1200-1203  Shot  → GrabSaveAndDisplay(shot, null, shot, savePath)

InspectionListView.xaml.cs:1313-1325  ResolveDatumCameraParam(datum)
  → datum.SourceShotName 으로 ShotConfig 조회, 미설정이면 "이 datum이 속한 시퀀스의 첫 Shot" 폴백
  → 즉 티칭도 결국 ShotConfig(ICameraParam) 를 grab 파라미터로 사용한다

MainView.xaml.cs:1216 / :1283 / :1375   pDev.GrabHalconImage(param)   ← 1-A 병목으로 수렴
```

**결론:** 티칭 경로에는 **zIndex 개념이 아예 없다.** 대신 **`DatumConfig` 객체 자체가 호출부에 직접 손에 들려 있다**(`GrabAndDisplay(param, datum)` / `GrabSaveAndDisplay(param, datum, ...)`의 2번째 인자). 따라서 티칭에서는 `datum.MirrorX/MirrorY`를 **직접** 읽으면 되고 zIndex 매핑이 필요 없다. `[VERIFIED: MainView.xaml.cs:1263, 1332]`

### 1-C. 생산(검사 사이클) 경로

두 종류의 grab이 있고 **둘의 성격이 다르다**.

**(a) Shot 검사이미지 grab** — `Action_FAIMeasurement.cs:263-306` `EStep.Grab`
```
:249-253   nCurZ = parentSeq.GetExecutionZIndex();   ← 현재 tick 의 z_index 를 이미 계산해 둠
:268-269   #if SIMUL_MODE → LoadShotInspectionImage()   (파일 로드, 실기 grab 없음)
:271-273   OfflineInspectMode → LoadShotInspectionImage()
:276       image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam);   ← 실기 grab
```

**(b) Datum 검출용 grab** — DualImage(크로스-Z) 포함
```
Action_FAIMeasurement.cs:643-657  TryGrabOrLoadDualDatumImages(datum, parentSeq, ...)
  :651-654  프로토콜 사이클 + ZIndexA/B 둘 다 설정 → TryGrabOrLoadCrossZDatumImages
  :656      아니면 TryLoadStaticDualDatumImages (TeachingImagePath / _Vertical 파일 로드, grab 없음)

:694-722  TryGrabOrLoadCrossZDatumImages
  :702-705  nCurZ = GetExecutionZIndex(); bIsRoleA = (nCurZ == datum.ZIndexA); bIsRoleB = (nCurZ == datum.ZIndexB)
  :718      CaptureAndStoreCrossZDatumImage(datum, parentSeq, bIsRoleA)
        ↓
:726-738  CaptureAndStoreCrossZDatumImage
  :727      HImage capturedImage = GrabOrLoadDatumImage(datum);   ← datum 객체 보유!
        ↓
:557-583  GrabOrLoadDatumImage(DatumConfig datum)
  :563-564  #if SIMUL_MODE → LoadDatumImageFromPath (파일)
  :566-568  OfflineInspectMode → LoadDatumImageFromPath (파일)
  :570      image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam);   ← 실기 grab
  (+ :597   LoadDatumImageFromPath 내부 폴백 grab, SIMUL 전용)
```

**결론:** 생산 경로의 Datum grab도 **`datum` 객체를 손에 쥔 채로** `GrabHalconImage(ShotParam)`를 호출한다(`Action_FAIMeasurement.cs:570`). 즉 **티칭·생산 양쪽 모두 Datum grab 시점에 `datum.MirrorX/MirrorY`를 직접 읽을 수 있다.** zIndex 역추적이 필요한 곳은 **Shot 검사이미지 grab(a) 단 한 군데뿐**이다. `[VERIFIED: Action_FAIMeasurement.cs:557-583, 726-738]`

### 1-D. 수동 z 트리거는 별도 경로가 아니다

화면의 `zindex: [0] 수동 트리거 실행` 패널은 `MainView.xaml.cs:136-161 ManualZTriggerButton_Click` → `Custom/SystemHandler.cs:938-967 DebugManualZTrigger` → `ProcessPrep()` + `ProcessTest()`로, **프로덕션 TCP 경로를 그대로 재사용**한다(주석에 명시: "ProcessPrep/ProcessTest 는 프로덕션 TCP 경로 — 시그니처/로직 변경 금지"). → 수동 트리거는 (c) 생산 경로와 100% 동일. 별도 훅 불필요. `[VERIFIED: Custom/SystemHandler.cs:938-967]`

---

## 2. z_index 공간의 통일성 (질문 4에 대한 답: **YES, 단일 공간이다**)

`DatumConfig.ZIndexA/ZIndexB`와 `ShotConfig.ZIndex`는 **같은 z_index 공간**의 서로 다른 값이다. 실 레시피(`D:\Data\Recipe\FAI_1\main.ini`) 실측:

| 물리 포즈 | Datum (ZIndexA/B) | 대응 Shot (ZIndex) | Shot 의 DatumRef |
|---|---|---|---|
| 3-1 | Side_Datum_3-1 → **0 / 1** (L217-218) | SIDE_SHOT_1_D1 → **2** | `Side_Datum_3-1` |
| 3-2 | Side_Datum_3-2 → **3 / 4** (L397-398) | SIDE_SHOT_2_1_D1 → **5** | `Side_Datum_3-2` |
| 4-2 | Side_Datum_4-2 → **7 / 8** (L577-578) | SIDE_SHOT_3_H5 → **9** | `Side_Datum_3` ⚠ (존재하지 않는 이름) |
| 4-1 | Side_Datum_4-1 → **12 / 13** (L757-758) | SIDE_SHOT_4-1_F9 → **14** | `Side_Datum_4-1` |

`[VERIFIED: D:\Data\Recipe\FAI_1\main.ini 실측 dump]`

**여기서 중대한 발견:** 한 물리 포즈는 **여러 z_index에 걸쳐 있다**(예: 4-1 포즈 = z 12, 13, **14**). Datum은 z 12/13에서 찍고, 같은 포즈의 FAI 측정용 Shot 이미지는 z **14**에서 찍는다. **미러가 필요한 포즈라면 z=14의 Shot 이미지도 똑같이 미러되어야 한다** — 그렇지 않으면 미러된 Datum 좌표계로 미러 안 된 이미지를 측정하게 되어 전 항목이 어긋난다.

- 미러 플래그는 `DatumConfig`에만 있고 `ShotConfig`에는 없다.
- Shot → Datum 연결고리는 **존재한다**: `MeasurementBase.DatumRef`(`MeasurementBase.cs:18`). 위 표에서 실측 확인됨.
- ⚠ 단, `SIDE_SHOT_3_H5`의 `DatumRef=Side_Datum_3`는 **개명 전 이름이 남은 dangling 참조**다(현재 Datum 이름은 `Side_Datum_4-2`). 이미 `InspectionSequence.IsDatumRefUnresolvable`(:2029-2038)로 감지되는 기존 데이터 결함. **DatumRef 기반 매핑은 이 Shot에서 실패한다** — plan 단계에서 반드시 고려할 것.
- `+1 규칙`(datumZB + 1 = shotZ)은 4개 포즈 모두에서 성립하지만 **레시피 규약이 아니라 우연**일 수 있다. 새 규칙 발명은 지침 위반이므로 권장하지 않는다.

**기존 재사용 가능 헬퍼:** `InspectionSequence.BuildCrossZDatumIndexSet()`(:1200-1222)이 이미 `DatumConfigs`를 순회해 `ZIndexA/ZIndexB`(!= -1)를 `HashSet<int>`로 모은다. z-맵이 필요해지면 이 패턴을 그대로 확장하는 것이 컨벤션에 맞다. `[VERIFIED: InspectionSequence.cs:1200-1222]`

---

## 3. 앱 시작 순서 / 레시피 로드 타이밍 (질문 6에 대한 답: **DeviceHandler 생성자는 불가**)

```
App.xaml.cs Application_Startup
  → MainWindow 생성
     → SystemHandler.Handle (싱글턴 생성자)
        SystemHandler.cs:100-101   Devices = DeviceHandler.Handle;  Devices.Initialize();   ★ 카메라 등록/Open
                                     ↳ Custom/Device/DeviceHandler.cs:96-112 RegisterRequiredDevices()
                                     ↳ Device/DeviceHandler.cs:221-249 MIL case (sharedMil.RegisterRoleInfo(id) @:235)
     → SystemHandler.Initialize()
        SystemHandler.cs:174       Sequences = SequenceHandler.Handle;    (시퀀스 골격만, 레시피 아님)
        SystemHandler.cs:208       Sequences.ExecOnCreate();
     → MainWindow.xaml.cs:434-441  Window_ContentRendered_LoadRecipe
        → SystemHandler.LoadRecipe(Setting.CurrentRecipeName)  (SystemHandler.cs:246-252)
           → SequenceHandler.LoadRecipe (:152) → LoadFromIni (:183-216)
              → Custom/SequenceHandler.cs:298-314 TryLoadNewFormat   ★ 여기서 DatumConfigs/Shots 가 채워짐
                 :304   RecipeManager.Load(loadFile)
                 :310-312 RebuildInspectionActions(Top/Side/Bottom)
```

- **`DeviceHandler.Initialize()` 시점에는 `DatumConfig`가 단 하나도 존재하지 않는다.** `[VERIFIED]`
- 레시피 데이터가 확보되는 **유일한 완료 지점**은 `Custom/Sequence/SequenceHandler.cs:314` (`TryLoadNewFormat` 의 `return true` 직전).
- 이 경로는 **앱 시작 + 런타임 레시피 교체 둘 다** 지난다: `MainWindow.xaml.cs:373`(UI 레시피 변경), `Custom/SystemHandler.cs:148/153`(TCP $RECIPE). → 레시피 스캔형 설계를 택하면 **런타임 교체 시 이전 레시피의 역할이 `_roleInfoMap`에 그대로 남는다**(§4 Pitfall 2).

---

## 4. Pitfalls (이 코드베이스 고유)

### Pitfall 1 — `Devices` 딕셔너리에 가짜 카메라 키를 추가하는 접근은 위험 (Approach B 기각 근거)
TOP/BOTTOM 선례(`DeviceHandler.cs:236 Devices.Add(id.Identifier, sharedMil)`)를 흉내 내 `Devices["CAM_SIDE#MX"]`를 추가하고 `param.DeviceName`을 바꾸는 방식은 다음을 전부 깨뜨린다:
- `ShotConfig.DeviceName`은 **INI에 영속 저장**되는 값이다(`[SHOT_n_CAM] DeviceName=CAM_SIDE` 실측). 런타임에 바꾸면 저장 시 오염된다.
- `CameraParam.DeviceNameList`(`Sequence/Param/CameraParam.cs:113-119, 163-166`)가 `Devices` 목록으로 UI 드롭다운을 만든다 → 가짜 장치가 사용자에게 노출된다.
- SIMUL_MODE에서는 `AddVirtualCamera(id)`(`DeviceHandler.cs:226`)로 **키가 1개만** 생성된다 → `this["CAM_SIDE#MX"]`가 null → "Device Not Opened"로 grab 전면 실패.
- `SequenceHandler.FindBlockingSequenceName`(`Custom/Sequence/SequenceHandler.cs:76+`)이 카메라 **객체 참조 동일성**으로 상호배타를 판정한다 — 같은 객체를 여러 키로 넣으면 판정 의미가 흐려진다.
`[VERIFIED: 각 파일 직접 확인]`

### Pitfall 2 — `_roleInfoMap`에는 삭제/초기화 API가 없다
`MilCamera.cs:52-60`은 `_roleInfoMap[key] = value` 쓰기만 있고 `Remove`/`Clear`가 없다. 레시피 스캔형(“미러 켜진 z만 등록”) 설계를 택하면 **레시피 A에서 z=12를 미러 등록 → 레시피 B로 교체 → z=12 항목이 그대로 남아 미러가 유령처럼 계속 적용**된다. 스캔형을 택할 경우 반드시 (a) `Clear` 추가, 또는 (b) **모든 z를 항상 덮어쓰기 등록**(미러 off인 z도 명시 등록)해야 한다. `[VERIFIED: MilCamera.cs:52-60 전체 확인]`

### Pitfall 3 — 비프로토콜 실행에서 `GetExecutionZIndex()`는 항상 0이다 (거짓말)
`InspectionSequence.cs:1179-1194`: RUN 버튼 / 일괄검사 / RepeatRun은 `RequestPacket == null`이라 `GetExecutionZIndex()`가 **안전 폴백으로 0**을 반환한다(코드 주석 :1184-1189에 명시). 그런데 실 레시피에서 **z=0은 `Side_Datum_3-1.ZIndexA`로 실제 사용 중인 값**이다. → z 기반 미러 조회를 무조건 하면, 만약 z=0이 미러로 지정될 경우 **RUN/일괄검사에서 전 SIDE Shot이 잘못 미러된다.** z 기반 조회는 반드시 `IsProtocolDrivenCycle()`(:1190-1194) 게이트 뒤에 두거나, 아예 z 대신 `ShotParam.ZIndex`(정적 선언값, 항상 정확) / `datum` 객체를 직접 쓸 것. `[VERIFIED: InspectionSequence.cs:1179-1194 + 레시피 z=0 실측]`

### Pitfall 4 — SIMUL_MODE에서는 MIL 미러가 **원리적으로 검증 불가**
- `DatumMeasurement.csproj:43,64` → Debug 빌드는 `SIMUL_MODE` 정의. 이 개발 PC의 유일한 빌드 경로.
- `Device/DeviceHandler.cs:223-226`: SIMUL에서는 **`MilCamera` 객체 자체가 생성되지 않고** `AddVirtualCamera(id)`로 대체된다. `MilCamera.cs:254-256`의 SIMUL 분기까지 갈 일도 없다.
- `VirtualCamera.GrabHalconImage(string requestIdentifier)`(`VirtualCamera.cs:460-462`)는 **인자를 무시하고** 파라미터 없는 오버로드로 위임한다.
→ **좋은 소식:** 어떤 식별자를 넘겨도 SIMUL에서는 조용히 무시되므로 **회귀 위험 0**. **나쁜 소식:** 미러 동작 자체는 SIMUL에서 절대 재현되지 않는다.
**권장:** plan에 (i) SIMUL 정적 검증(식별자가 의도대로 만들어지는지 로그로 확인) + (ii) **실기 HW 확인을 human-verify 체크포인트로 명시 분리**. SIMUL 티칭 이미지를 미리 뒤집어 두는 방식은 `TeachingImagePath`/`SimulImagePath` 파일을 손대는 것이라 데이터 오염 위험이 커 **비권장**. `[VERIFIED: csproj + DeviceHandler.cs:223-226 + VirtualCamera.cs:460-462]`

### Pitfall 5 — 라이브 스트리밍 중 grab 차단 / LiveLoop은 기본 Info 사용
- `MilCamera.cs:267-270`: `CaptureMode == Streaming`이면 검사 grab이 **null 반환**(stale 프레임 금지). 미러 배선과 무관하지만, 실기 검증 시 DeviceSelector 라이브뷰 창이 열려 있으면 grab 자체가 안 되므로 "미러가 안 먹는다"로 오진하기 쉽다.
- `MilCamera.cs:500 LiveLoop → GrabFromBuffer(Info)`: 라이브 미리보기는 항상 **기본 Info**를 쓴다. 즉 라이브뷰 화면은 미러가 적용되지 않는다(의도된 기존 동작, 변경 불필요).
`[VERIFIED: MilCamera.cs:267-270, 496-527]`

### Pitfall 6 — HImage 소유권 계약
`MilCamera.cs:281-289`: grab 결과는 `LastGrabHalconImage`에 보관 후 `LastHalconImage`(=`CopyImage()` 독립본)를 반환한다. 호출부가 Dispose 책임을 진다(`Action_FAIMeasurement.cs:302`, `MainView.xaml.cs:1252`). **미러 배선은 이 계약을 건드리지 않는다** — 식별자 문자열만 추가로 전달할 뿐 이미지 수명주기는 무변경이어야 한다. `[VERIFIED]`

---

## 5. 권장 통합 지점 (구현은 planner 몫 — 형태만 제시)

### 설계 A (권장): 정적 4역할 + 2-인자 오버로드

**왜 이게 최선인가:** 레시피 스캔이 전혀 필요 없으므로 §3의 시작 순서 문제와 §4 Pitfall 2(stale 역할)가 **동시에 소멸**한다. 미러 조합은 (X,Y) 불리언 2개 = **최대 4가지**뿐이므로 z 개수와 무관하게 역할이 4개로 고정된다.

**(1) 역할 등록 — `WPF_Example/Custom/Device/DeviceHandler.cs`**
`RegisterRequiredDevices()`(:96-112)의 SIDE 분기(:108-111)에서, 기존 `RegisterCxpCamera(CAMERA_SIDE, ...)` 이후 미러 조합 3종을 추가 등록.
식별자 규약 제안: `CAMERA_SIDE + "#MX"`, `"#MY"`, `"#MXY"` (기존 `CAM_SIDE`는 정상 = 무변경).
`SetRequiredDevice`(`Device/DeviceHandler.cs:292-295`)는 `IDList`에 추가하므로 그대로 쓰면 `Devices` 딕셔너리에도 키가 생겨 Pitfall 1에 걸린다 → **`Devices`가 아니라 `MilCamera.RegisterRoleInfo(new DeviceInfo(...))`만 직접 호출**해야 한다. 호출 위치는 `Device/DeviceHandler.cs:231-246`의 MIL 분기 안(sharedMil 획득 직후)이 자연스럽다.

**(2) 전달 통로 — `WPF_Example/Device/DeviceHandler.cs:329-335`**
```
// 신설 (기존 1-인자는 이 2-인자에 param.DeviceName 을 넘겨 위임 → 호출부 회귀 0)
public HImage GrabHalconImage(ICameraParam param, string requestIdentifier)
```
카메라 조회는 계속 `this[param.DeviceName]`, 역할 해석만 `requestIdentifier`로 분리. SIMUL의 `VirtualCamera`는 인자를 무시하므로 안전(§4 Pitfall 4).

**(3) 식별자 결정 헬퍼 (신설, 단일 소스)**
`bool mirrorX, bool mirrorY, string baseDeviceName` → 식별자 문자열. 삼항 금지 규칙에 따라 if-else 4분기. 배치 위치 후보: `Custom/Device/DeviceHandler.cs`(카메라 네이밍 상수가 이미 여기 모여 있음).

**(4) 호출부 배선 — 총 4곳 (모두 SIDE·비-SIMUL 경로만 영향)**

| # | 파일:라인 | 미러 소스 | 비고 |
|---|---|---|---|
| a | `Action_FAIMeasurement.cs:570` (`GrabOrLoadDatumImage`) | `datum.MirrorX/MirrorY` **직접** | 생산 Datum grab (크로스-Z 캡처가 여기로 옴) |
| b | `MainView.xaml.cs:1283` (`GrabAndDisplay(param, datum)`) | `datum.MirrorX/MirrorY` **직접** | 티칭 Datum grab |
| c | `MainView.xaml.cs:1375` (`GrabSaveAndDisplay(..., datum, ...)`) | `datum` (null이면 무미러) | 티칭 검사이미지 Grab |
| d | `Action_FAIMeasurement.cs:276` (`EStep.Grab`) | **Shot→Datum 역추적 필요** | §2의 z=14 문제. 아래 참조 |

(d)의 Shot 미러 해석 방법 3안 (planner/사용자 결정 필요):
- **d-1 (권장):** Shot의 FAI 측정들이 참조하는 `DatumRef`(`MeasurementBase.cs:18`) → 해당 `DatumConfig`의 미러 플래그. 근거가 레시피 데이터에 실재하며 새 규약을 발명하지 않음. ⚠ dangling `DatumRef`(SIDE_SHOT_3_H5) 는 무미러 폴백 + 경고 로그.
- **d-2:** `InspectionSequence`에 z→미러 맵 구축(`BuildCrossZDatumIndexSet` 패턴 확장). Datum의 ZIndexA/B만 커버되어 **Shot z(=14)를 놓친다** → 단독으로는 불충분.
- **d-3:** `ShotConfig`에 MirrorX/Y 프로퍼티 신설. 가장 명시적이지만 Part 1 범위(DatumConfig 전용)를 넘고 UI/INI 추가 작업 발생.

**(5) 하지 말 것**
- `MilCamera.cs`의 `GrabFromBuffer`/`ResolveRoleInfo` 로직 변경 (이미 필요한 기능을 전부 제공함 — 신규 코드 0)
- `DatumConfig.cs`의 `MirrorX`/`MirrorY`/`_suppressMirrorWarning` 수정 (Part 1 완료분)
- `Devices` 딕셔너리에 키 추가, `param.DeviceName` 런타임 변조 (§4 Pitfall 1)
- HALCON `mirror_image`/`RotateImage` 소프트웨어 미러 (사용자 결정으로 기각됨, ~27ms)

### 설계 B (대안): 레시피 스캔 + z별 역할 등록
훅 위치는 **반드시** `Custom/Sequence/SequenceHandler.cs:298-314 TryLoadNewFormat`의 `return true` 직전(§3). `_roleInfoMap` stale 문제(Pitfall 2) 때문에 "미러 켜진 z만"이 아니라 **레시피에 선언된 전 z를 매번 덮어쓰기 등록**해야 한다. 설계 A 대비 이점이 없고 복잡도만 높다 → **비권장**.

---

## 6. 설계 결정과의 정합성 — planner가 사용자에게 확인해야 할 1건

사전 결정문에는 **"앱 시작 시 1회 적용, per-grab 동적 적용 아님"**이 명시되어 있고, Part 1의 경고 다이얼로그도 "프로그램을 다시 시작해야 적용된다"고 안내한다(`DatumConfig.cs:238, 252`).

**그러나 코드 실측 결과** `MdigControl(M_GRAB_DIRECTION_X/Y)`는 **이미 2026-08-05(quick-260805-jtj)부터 매 grab 직전 재적용되고 있다**(`MilCamera.cs:322-323`). 즉:
- "역할 **등록**은 시작 시 1회, 역할 **선택**은 grab 시점" 구조는 사용자 결정과 **모순되지 않는다**(동적 재설정이 아니라, 이미 존재하는 per-grab 재적용에 올바른 값을 골라 넣는 것).
- 다만 설계 A는 **레시피 로드에 의존하지 않으므로 재시작 없이도 즉시 반영된다.** → 이미 배포된 "재시작 필요" 안내 문구가 **실제보다 보수적**이 된다(기능상 무해, 문구만 부정확).

**Planner 판단 필요:** (i) 문구 그대로 두고 "재시작해도 물론 적용됨"으로 넘어갈지, (ii) Part 1 파일(수정 금지 대상)을 예외적으로 열어 문구를 고칠지. 연구자 의견은 **(i)** — 수정 금지 제약을 지키고, 실기 UAT에서 재시작 없이도 되는지 관찰만 기록.

---

## 7. Assumptions Log

| # | 가정 | 위치 | 틀렸을 때의 영향 |
|---|---|---|---|
| A1 | 물리 포즈 4-1은 z 12/13(Datum) + z 14(Shot) 전부 미러가 필요하다 | §2 | Shot 이미지만 미러 안 되어 Datum 좌표계와 어긋남 → 전 FAI 오측정. **실기 확인 필수** |
| A2 | `M_GRAB_DIRECTION_X/Y` 반전은 회전(`RotateAngle`)과 독립적으로 조합 가능 | §5 | SIDE는 `ROTATE_SIDE = _0`(`Custom/Device/DeviceHandler.cs:43`)라 현재 조합 이슈 없음. 회전 도입 시 재검토 |
| A3 | SIDE 카메라를 쓰는 다른 소비자(캘리브 `MainView.xaml.cs:3292`, 라이브뷰)는 미러 무관하게 기본 역할이면 된다 | §1 | 캘리브 이미지가 미러된 포즈에서 촬영되면 mm/px 산출에는 영향 없으나(스케일만 사용) 육안 혼동 가능 |

---

## 8. 실기 검증 체크포인트 (SIMUL로 대체 불가 — human-verify)

1. 실 HW SIDE PC에서 `Side_Datum_4-1`의 `MirrorY=True` 저장 → 앱 재시작 → 해당 Datum '검사이미지 Grab' → 저장 bmp가 상하 반전되었는지 육안 확인
2. 같은 상태에서 `SIDE_SHOT_4-1_F9`(z=14) Shot Grab 이미지도 동일 방향으로 반전되었는지 확인 (A1 검증)
3. `MirrorX/Y = False`인 나머지 3개 Datum + 그 Shot들이 **완전히 무변경**인지 확인 (회귀 0)
4. TCP `$PREP`/`$TEST` 또는 수동 z 트리거로 z=12→13→14 전체 사이클 실행 → Datum 검출 성공 + FAI 측정값이 미러 전과 정합적인지 확인

---

## Sources

### Primary (HIGH — 이 저장소 소스 직접 확인)
- `WPF_Example/Device/DeviceHandler.cs` (:14-40 DeviceInfo, :221-249 MIL 등록, :292-295 SetRequiredDevice, :329-335 grab 병목)
- `WPF_Example/Device/Camera/Mil/MilCamera.cs` (:20-71 역할 맵, :194-195/:305-323 MdigControl, :249-297 grab, :496-527 LiveLoop)
- `WPF_Example/Device/Camera/VirtualCamera.cs` (:455-462 base grab 오버로드)
- `WPF_Example/Custom/Device/DeviceHandler.cs` (:32-44 상수, :96-126 등록)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (:249-306, :542-605, :643-800)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` (:52, :617-745, :1179-1228)
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (:108-118, :214-261)
- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` (:18 DatumRef)
- `WPF_Example/Custom/Sequence/SequenceHandler.cs` (:38-48 IsSequenceActive, :239-314 Rebuild/TryLoadNewFormat)
- `WPF_Example/Sequence/SequenceHandler.cs` (:152-216 LoadRecipe/LoadFromIni)
- `WPF_Example/SystemHandler.cs` (:80-232 생성자+Initialize, :246-252 LoadRecipe)
- `WPF_Example/Custom/SystemHandler.cs` (:938-991 DebugManualZTrigger/ApplyPrepToSequences)
- `WPF_Example/MainWindow.xaml.cs` (:373, :434-441 레시피 로드 시점)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` (:120-161 수동 Z, :1196-1460 grab 3종, :3263-3307 캘리브 grab)
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` (:1162-1209 Grab 버튼, :1313-1325 ResolveDatumCameraParam)
- `WPF_Example/DatumMeasurement.csproj` (:43, :64 SIMUL_MODE)
- `D:\Data\Recipe\FAI_1\main.ini` — 실 운영 레시피 실측 dump (Datum z / Shot z / DatumRef / DeviceName)

### 참고 (과거 작업 기록)
- `.planning/quick/260805-jtj-.../260805-jtj-SUMMARY.md` — `_roleInfoMap` 도입 배경 (TOP/BOTTOM 공유)
- `.planning/quick/260813-fdt-side-datum-x-y/260813-fdt-SUMMARY.md` — Part 1 인계 메모

## Metadata

**Confidence breakdown:**
- 호출 체인 / 병목 위치: **HIGH** — 전 경로를 파일:라인으로 직접 추적, 추론 없음
- 시작 순서 / 레시피 타이밍: **HIGH** — 생성자↔Initialize↔ContentRendered 순서 코드로 확정
- z_index 통일성 및 z=14 갭: **HIGH** — 실 운영 레시피 INI 실측
- Shot 미러 해석 방법(d-1/d-2/d-3): **MEDIUM** — DatumRef 연결은 실측 확인했으나 dangling 사례 1건 존재, 사용자 확인 필요
- SIMUL 검증 불가 판정: **HIGH** — csproj + `#if SIMUL_MODE` 분기 직접 확인

**Research date:** 2026-08-13
**Valid until:** 이 저장소의 grab 경로가 바뀌기 전까지 (내부 코드 추적이라 외부 만료 없음)
