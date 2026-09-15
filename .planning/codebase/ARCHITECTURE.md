<!-- last_mapped_commit: f30f7c42; refreshed: 2026-09-15 -->
# 건축 패턴 분석

**분석 일시:** 2026-09-15

## 시스템 개요

```
┌─────────────────────────────────────────────────────────────────┐
│                    WPF 사용자 인터페이스                          │
│  (MainWindow, MainView, Statistics, Settings, Teaching UI)      │
│             `WPF_Example/UI/**/*.xaml(.cs)`                      │
└─────────────────────────────┬───────────────────────────────────┘
                              │
┌─────────────────────────────▼───────────────────────────────────┐
│                    시스템 핸들러 (싱글톤)                          │
│  `WPF_Example/SystemHandler.cs` + `Custom/SystemHandler.cs`     │
│  · 통합 구성 및 모든 서브시스템 조율                             │
│  · TCP 패킷 수신·응답 라우팅 (MainRun 폴링 루프)                 │
│  · 시퀀스/디바이스/조명/네트워크/레시피 생명주기 관리             │
└──┬──────────────────────────┬──────────────────┬────────────────┘
   │                          │                  │
   ▼                          ▼                  ▼
┌──────────────────────┐ ┌────────────────┐ ┌──────────────────┐
│ 시퀀스 핸들러         │ │ 디바이스 핸들러 │ │ 조명 컨트롤러      │
│ `Sequence/`          │ │ `Device/`      │ │ `Custom/Device/`  │
│ 상태머신 프레임워크   │ │ 가상 카메라     │ │ 릴레이 제어        │
│ 및 구체적 시퀀스/액션 │ │ SDK 추상화      │ │                  │
│                      │ │                │ │                  │
│ · Top                │ │ · Basler       │ │ · JPF             │
│ · Bottom             │ │ · Hikvision    │ │ · Pamtek          │
│ · Side (1~4 지그)     │ │ · MIL          │ │ · Serial I/O      │
│                      │ │                │ │                  │
└──────────┬───────────┘ └───────┬────────┘ └──────────────────┘
           │                     │
           ▼                     ▼
    ┌──────────────────┐  ┌────────────────────┐
    │ HALCON 알고리즘   │  │ 잡 구성/레시피 데이터  │
    │ `Halcon/`        │  │ `Custom/Sequence/  │
    │                  │  │  Inspection/`      │
    │ · 에지 측정       │  │                    │
    │ · 기준점 찾기     │  │ · ShotConfig       │
    │ · 교정/매칭       │  │ · FAIConfig        │
    │ · Align 검증      │  │ · MeasurementBase  │
    │                  │  │                    │
    └──────────────────┘  └────────────────────┘
           │
           ▼
    ┌──────────────────┐
    │ Ethernet Align   │
    │ 비전 (선택사항)   │
    │ `Custom/Ethernet │
    │  Vision/`        │
    │                  │
    │ · Tray 위치 정렬 │
    │ · 피커 중심 교정 │
    │ · AlignVerify    │
    └──────────────────┘
           │
           ▼
    ┌──────────────────────┐
    │ TCP Vision Server    │
    │ `TcpServer/`         │
    │                      │
    │ Protocol: $CMD,...@  │
    │ (PLC/핸들러 통신)    │
    └──────────────────────┘
```

## 구성 요소 책임

| 구성요소 | 책임 | 파일 |
|---------|------|------|
| **시스템 핸들러** | 전역 통합, TCP 폴링, 시퀀스 라우팅 | `SystemHandler.cs`, `Custom/SystemHandler.cs` |
| **시퀀스 프레임워크** | 상태머신, 액션 실행 루프 | `Sequence/Sequence/SequenceBase.cs`, `Sequence/Action/ActionBase.cs` |
| **구체적 시퀀스** | Top/Bottom/Side 검사 흐름 | `Custom/Sequence/Top/`, `Custom/Sequence/Bottom/` |
| **FAI 측정** | 2계층 동적 구조 (Shot → FAI → 측정) | `Custom/Sequence/Inspection/Action_FAIMeasurement.cs`, `FAIConfig.cs`, `ShotConfig.cs` |
| **기준점 찾기** | Datum 알고리즘 (원/선/교점) | `Halcon/Algorithms/DatumFindingService.cs` |
| **에지 측정** | Halcon 측정 (거리/각도) | `Halcon/Algorithms/FAIEdgeMeasurementService.cs` |
| **디바이스 추상화** | VirtualCamera, 카메라 SDK 래핑 | `Device/Camera/VirtualCamera.cs`, `Device/DeviceHandler.cs` |
| **조명 제어** | 릴레이 온/오프, 레벨 설정 | `Custom/Device/LightHandler.cs` |
| **TCP 서버** | 프로토콜 파싱, PLC 통신 | `TcpServer/VisionServer.cs` |
| **Ethernet Align** | Tray/Picker 위치 정렬 | `Custom/EthernetVision/EthernetVisionHandler.cs` |

## 패턴 개요

**전체 패턴:** SystemHandler 싱글톤이 모든 서브시스템 조율  
**시퀀스 구조:** 상태머신 (Idle → Running → Finish/Error)  
**액션 패턴:** 템플릿 메서드 — `Run()` 단계 루프 (`EStep` enum 기반)  
**디바이스:** 가상 카메라 추상화 — 실제 SDK 구현은 숨김  
**데이터 모델:** 2계층 Shot → FAI 레시피 구조 + 측정 팩토리  
**TCP 흐름:** MainRun() 폴링 → $TEST/$PREP/$LIGHT/$ALIGN 분배 → 시퀀스 실행 → 응답 큐 수신  

## 계층 구조

### 1. 사용자 인터페이스 계층

**용도:** WPF XAML 뷰 + 코드비하인드, ViewModel 바인딩  
**위치:** `WPF_Example/UI/`, `WPF_Example/Custom/UI/`, `WPF_Example/MainWindow.xaml(.cs)`  
**포함 내용:** 카메라 이미지 표시, 검사 결과 오버레이, 설정 창, 레시피 트리, 통계 차트  
**의존성:** SystemHandler.Sequences, SystemHandler.Devices, 로그 서비스  
**사용처:** 사용자 상호작용, 상태 표시, 수동 검사 트리거  

### 2. 시스템 핸들러 (통합 조율)

**용도:** 모든 서브시스템 생명주기, TCP 패킷 폴링, 시퀀스 디스패치  
**위치:** `WPF_Example/SystemHandler.cs` (초기화), `WPF_Example/Custom/SystemHandler.cs` (MainRun 폴링)  
**포함 내용:**  
- 싱글톤 접근점 `SystemHandler.Handle`  
- 초기화 순서: Setting → Logging → Devices → Lights → Sequences → Server → RawImageSaver  
- MainRun() 폴링 루프: TCP 응답 전송 → TCP 수신 패킷 처리 → 시퀀스 라우팅  
- ProcessTest/ProcessPrep/ProcessAlignTest: 패킷을 시퀀스 커맨드로 변환  

**의존성:** 모든 핸들러  
**사용처:** App.xaml.cs, MainWindow.xaml.cs, UI 이벤트 핸들러  

### 3. 시퀀스 프레임워크

**용도:** 상태머신 기반 시퀀스/액션 실행 엔진  
**위치:** `WPF_Example/Sequence/Sequence/SequenceBase.cs`, `WPF_Example/Sequence/Action/ActionBase.cs`  
**포함 내용:**  
- `SequenceBase`: 상태관리, 액션 루프, 결과 큐  
- `ActionBase`: 템플릿 메서드 패턴 (Run() 오버라이드)  
- `SequenceContext`: 라이브 상태, 결과 이미지, 오버레이 (UI 소비)  
- `ActionContext`: 액션 단계 결과, 오류 코드  
- 각 시퀀스는 자신의 스레드(`ThreadPriority.Highest`)에서 ~5ms 간격 액션 실행  

**의존성:** Halcon, DeviceHandler, Define (ESequence/EAction enum)  
**사용처:** SequenceHandler 등록, SystemHandler 라우팅  

### 4. 구체적 시퀀스 & 액션 (Custom 레이어)

**용도:** 프로젝트별 검사 시퀀스/액션 구현  
**위치:** `WPF_Example/Custom/Sequence/`  
**포함 내용:**  
- `Sequence_Top` / `Sequence_Bottom`: Top/Bottom 이미지 잡 및 처리  
- `Action_TopInspection` / `Action_BottomInspection`: 각 이미지에 대한 검사/오버레이  
- `Action_FAIMeasurement`: 2계층 동적 레시피 (Shot → FAI → 측정) 실행  
- 각 액션은 `EStep` enum 기반 상태머신으로 구현  

**의존성:** SequenceBase, ActionBase, Halcon 알고리즘, ShotConfig/FAIConfig  
**사용처:** SequenceHandler.RegisterSequences/RegisterActions  

### 5. FAI 측정 계층 (2계층 동적 구조)

**용도:** 검사 항목별 측정 관리  
**위치:** `WPF_Example/Custom/Sequence/Inspection/`  
**포함 내용:**  
- `ShotConfig`: 카메라 위치 (Z축 인덱스), 시뮬레이션 이미지 경로  
- `FAIConfig`: 수동 검사 항목 (이름, ROI, 에지 파라미터)  
  - Measurements: N개 측정 (EdgeToLineDistance, CircleDiameter, DualImageEdge 등)  
- `MeasurementBase`: 추상 측정 단위 (공차/기준값)  
- `MeasurementFactory`: 타입명 → 구체적 Measurement 생성  
- `Action_FAIMeasurement`: 모든 Shot의 모든 FAI의 모든 Measurement 순회, Halcon 실행  

**의존성:** Halcon 알고리즘, FAIEdgeMeasurementService  
**사용처:** TCP 검사 결과 응답, 검사 결과 저장/로드  

### 6. 기준점 & Datum 계층

**용도:** 기준 좌표 추출 (원 중심, 선, 교점)  
**위치:** `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`, `Custom/Sequence/Inspection/DatumConfig.cs`  
**포함 내용:**  
- `DatumConfig`: 기준점 종류 (Circle, Line, Horizontal, Vertical, Intersection), ROI, 알고리즘 선택  
- `DatumFindingService`: Halcon 기하학 연산 (circle_center, line fit, intersection)  
- Datum-skip: 기준점 미검출 시 FAI 측정 생략 (응답 'N')  

**의존성:** Halcon, HalconDotNet  
**사용처:** Action_FAIMeasurement 측정 전 기준점 추출  

### 7. Halcon 알고리즘 계층

**용도:** 이미지 처리, 에지 탐지, 기하학 연산  
**위치:** `WPF_Example/Halcon/Algorithms/`  
**포함 내용:**  
- `MeasurementAlgorithm`: Halcon 에지 추출, ROI 관리  
- `FAIEdgeMeasurementService`: 측정 타입별 거리/각도 계산  
- `DatumFindingService`: 기준점 탐지  
- `AlignShapeMatchService`: Tray/Picker 위치 정렬  
- 모든 `HOperatorSet.*` 호출은 `try { } catch { return false; }` 패턴  

**의존성:** HalconDotNet, System.Drawing  
**사용처:** 액션 Run() 메서드, Align 검증  

### 8. 디바이스 추상화 계층

**용도:** 카메라 & 조명 SDK 래핑  
**위치:** `WPF_Example/Device/` (프레임워크), `WPF_Example/Custom/Device/` (구현)  
**포함 내용:**  
- `VirtualCamera`: 모든 카메라의 공통 인터페이스  
  - `GrabHalconImage(ICameraParam)`: 설정된 파라미터로 Halcon HImage 반환  
  - 구현: `BaslerCamera`, `HikCamera`, `MilCamera` (내부 SDK 호출)  
- `DeviceHandler`: VirtualCamera 레지스트리 + 생명주기  
  - `RegisterRequiredDevices()`: 카메라 역할별 활성화 (Top/Bottom/Side)  
- `LightHandler`: 조명 그룹 제어 (on/off, 레벨)  

**의존성:** 카메라 SDK (Basler Pylon, Hikvision MVS, MIL), OpenCvSharp  
**사용처:** ActionBase.GrabImage(), 알고리즘 입력  

### 9. TCP 네트워크 계층

**용도:** PLC/외부 핸들러와의 통신  
**위치:** `WPF_Example/TcpServer/` (프레임워크), `WPF_Example/Custom/TcpServer/` (리소스 맵핑)  
**포함 내용:**  
- `VisionServer`: TCP 서버 (`port 2505`)  
- Protocol: `$CMD,params@` (STX=$, ETX=@, 구분자=,/:)  
- 패킷 타입: TEST, PREP, ALIGN, LIGHT, RECIPE, ALIVE, RESET  
- `VisionRequestPacket` / `VisionResponsePacket`: 직렬화  
- `ResourceMap`: 수치 사이트/타입 ↔ 문자 시퀀스/액션 매핑  

**의존성:** System.Net.Sockets  
**사용처:** SystemHandler.MainRun(), ProcessXxx 메서드  

### 10. Ethernet Align Vision (선택사항)

**용도:** Tray/Picker 위치 정렬, 교정  
**위치:** `WPF_Example/Custom/EthernetVision/`  
**포함 내용:**  
- `EthernetVisionHandler`: TCP 비전 요청 서버, Bottom Align 시퀀스  
- `PickerCenterCalibrationService`: 피커 중심 교정  
- `AlignShapeMatchService`: Shape matching 기반 위치 정렬  
- `AlignVerifyRetention`: Align 검증 기록 (CSV 저장/로드)  

**의존성:** Halcon, TCP 통신  
**사용처:** ProcessAlignTest/ProcessAlignCalib, Custom/SystemHandler  

### 11. 유틸리티 & 서비스 계층

**용도:** 로깅, 파일 I/O, 설정 관리  
**위치:** `WPF_Example/Utility/`, `WPF_Example/Setting/`  
**포함 내용:**  
- `Logging`: 다중 로그 파일 (Trace, Camera, Error, Algorithm 등)  
- `SystemSetting`: INI/JSON 설정 (레시피 경로, 포트, 플래그)  
- `RawImageSaveService`: 검사 원본 이미지 비동기 저장  
- `CaptureImageSaveService`: 측정 캡처 이미지 저장  
- `RecipeFiles`: 레시피 파일 등록/로드  

**의존성:** System.IO, Newtonsoft.Json  
**사용처:** 모든 계층  

## 데이터 흐름

### 기본 검사 요청 흐름 ($TEST)

```
PLC → TCP $TEST 패킷 → VisionServer.GetRecvPacket()
  ↓
SystemHandler.MainRun() → ProcessTest(packet)
  ↓
패킷.Site (정수) → ResourceMap.SetIdentifier() → 시퀀스 이름/액션 매핑
  ↓
SequenceHandler[seq_name] → SequenceBase.Execute(TestPacket)
  ↓
액션 상태머신 루프 (EStep enum):
  1. Init: 카메라/조명 초기화, 디바이스 그랩 준비
  2. Grab: GrabHalconImage() → HImage 획득
  3. Process: Halcon 에지/기준점 탐지, 측정 실행
  4. Result: 공차 판정, 결과 집계, 이미지 저장
  5. Finish: FinishAction() → 다음 액션 또는 시퀀스 종료
  ↓
SequenceContext.State = Finish
  ↓
SequenceBase.PopResponse() → TestResultPacket
  ↓
SystemHandler.MainRun() → Server.SendPacket() → PLC로 응답 전송
```

### $PREP ($PREP + z_index 저장)

```
PLC → $PREP,z_index@ 
  ↓
SystemHandler.MainRun() → ProcessPrep()
  ↓
_lastPrepZIndexBySeq[seq_name] = z_index (lock 보호)
  ↓
응답: $PREP_ACK@ (즉시, 디바이스 준비 완료 확인)
```

### $ALIGN (Tray/Picker 위치 정렬)

```
PLC → $ALIGN 패킷
  ↓
ProcessAlignTest() / ProcessAlignCalib()
  ↓
EthernetVisionHandler → AlignShapeMatchService
  ↓
Halcon shape matching → 위치 오프셋 계산
  ↓
응답: AlignResult (delta_x, delta_y, theta)
```

### $LIGHT (조명 제어)

```
PLC → $LIGHT,group,level@
  ↓
ProcessLightSet() → LightHandler.SetLevel() / SetOnOff()
  ↓
응답: null (fire-and-forget, $PREP_ACK로 확인)
```

### 상태 관리

**SequenceContext 상태:** `Idle` → `Running` → `Finish` 또는 `Error`  
**ActionContext 상태:** 액션 Step 진행 (Init → Grab → Process → Result → Finish)  
**에러 전파:** Action.FinishAction(EContextResult.Error) → Sequence.OnError 이벤트 → UI 표시  

## 핵심 추상화

### 템플릿 메서드: SequenceBase/ActionBase

**패턴:** 프레임워크 (상태/통지 관리) vs. 구현 (Run() 오버라이드)  
**예:** `Action_TopInspection.Run()` 내부 `switch(Step)` 루프  
```csharp
switch (Step) {
    case EStep.Init:
        // 초기화 (조명 on, 파라미터 로드)
        Step = EStep.Grab;
        break;
    case EStep.Grab:
        // 이미지 잡, 재시도 로직
        Step = EStep.Process;
        break;
    case EStep.Process:
        // 알고리즘 실행, 결과 수집
        Step = EStep.Finish;
        break;
    case EStep.Finish:
        FinishAction(EContextResult.OK);
        return Context;
}
```

### ParamBase: 자동 직렬화 (INI ↔ 객체)

**패턴:** 리플렉션 기반 ParamBase.Save()/Load()  
**사용:** ShotConfig, FAIConfig, CameraSlaveParam  
```csharp
public class ShotConfig : CameraSlaveParam {
    [Category("Shot")]
    public double ZPosition { get; set; }
    public string SimulImagePath { get; set; }
    public List<FAIConfig> FAIList { get; set; }
}
// INI 자동 직렬화: [ShotName] ZPosition=10.5 SimulImagePath=...
```

### MeasurementFactory: 동적 측정 타입

**패턴:** 팩토리 메서드, 타입명 → 구체적 클래스  
**지원 타입:** EdgeToLineDistance, CircleDiameter, DualImageEdge, LineToLineAngle 등  
```csharp
MeasurementBase m = MeasurementFactory.Create("EdgeToLineDistance", fai);
```

### VirtualCamera: 디바이스 추상화

**패턴:** 모든 카메라 공통 인터페이스  
```csharp
HImage img = camera.GrabHalconImage(param);
// 구현: BaslerCamera, HikCamera, MilCamera 중 하나
```

### ResourceMap: 프로토콜 어댑터

**패턴:** 정수 사이트/타입 ↔ 문자 시퀀스/액션 매핑  
```csharp
ResourceMap.SetIdentifier(ref packet);
// packet.Identifier = 0 → "TOP"
// packet.Identifier2 = 1 → "Inspect"
```

## 엔트리 포인트

### 애플리케이션 시작

**위치:** `WPF_Example/App.xaml.cs` Application_Startup  
**트리거:** 프로세스 시작, mutex 가드 (단일 인스턴스)  
**책임:** 언어 초기화, MainWindow 생성/표시  

### 메인 윈도우 로드

**위치:** `WPF_Example/MainWindow.xaml.cs` Loaded 이벤트  
**트리거:** MainWindow 렌더 완료  
**책임:** `SystemHandler.Handle.Initialize()` 호출 → 모든 서브시스템 시작  
- Logging.Start()  
- Devices.Initialize()  
- Lights 초기화  
- Sequences 등록 & 시작  
- VisionServer 시작  
- RawImageSaveService 시작  

### 시스템 메인 루프

**위치:** `WPF_Example/SystemHandler.cs` SystemProcess()  
**트리거:** 별도 백그라운드 스레드, 1ms 주기 폴링  
**책임:** TCP 수신/응답 라우팅  
- Server.GetRecvPacket() → 패킷 파싱  
- ProcessTest/ProcessPrep/ProcessLight/ProcessAlign 분배  
- SequenceBase.PopResponse() → TCP 응답 전송  
- 응답 큐 드레인  

### 시퀀스 실행 루프

**위치:** `WPF_Example/Sequence/Sequence/SequenceBase.cs` MainExecute()  
**트리거:** 각 시퀀스 독립 스레드 (`ThreadPriority.Highest`)  
**책임:** 액션 상태머신 실행  
- Command 체크 (Start/Pause/Resume/Stop)  
- ActionBase.Run() 호출 (Step 진행)  
- SequenceContext 상태 업데이트  
- UI 콜백 (OnSequenceStateChanged)  

### 액션 Step 실행

**위치:** 구체적 액션 (e.g., `Custom/Sequence/Top/Action_TopInspection.cs`) Run() 오버라이드  
**트리거:** SequenceBase.ExecuteAction() 호출  
**책임:** Step 루프 진행  
- Grab: GrabHalconImage()  
- Process: 알고리즘 호출  
- Result: 판정 및 저장  
- Finish: FinishAction() 호출 시 다음 액션으로 진행  

## 건축적 제약

- **스레딩:** 각 시퀀스는 자신의 ThreadPriority.Highest 스레드에서 독립 실행; 공유 자원은 lock 보호  
- **전역 상태:** SystemHandler 싱글톤 (Devices, Lights, Sequences, Server); 내부 Dictionary는 lock 보호  
- **원형 수입:** 알려진 원형 의존성 없음 (프레임워크 → Custom 방향 순환 없음)  
- **동기화:** SequenceContext 결과는 액션 완료 시만 쓰기 (SequenceBase.FinishAction); UI는 읽기 전용  
- **HImage 수명:** 모든 HImage는 Dispose 필수; try/finally 또는 using 패턴  
- **대기 없음:** Halcon 연산은 비차단; 순수 실시간 주기(~5ms 액션 루프, ~1ms 폴링)에서만 대기  

## 안티패턴

### 1. 분산된 전역 상태

**무엇이 일어나는가:** 여러 시퀀스가 static/전역 변수로 상태를 공유  
**왜 문제인가:** 상호 간섭, 테스트 불가, 동시성 버그  
**올바른 방법:** SystemHandler → SequenceHandler → 각 Sequence 단계별 소유권 명확화  
예: `SystemHandler.Handle.Sequences[i].Context` 로 읽기  

### 2. 동기식 대기 in 알고리즘

**무엇이 일어나는가:** `Thread.Sleep()` 또는 `WaitHandle.WaitOne()` 호출  
**왜 문제인가:** 시퀀스 스레드 블로킹 → 검사 지연  
**올바른 방법:** Halcon 비동기 호출, 상태머신 Step 분리  

### 3. UI Code-Behind에 검사 로직

**무엇이 일어나는가:** MainWindow.xaml.cs에 알고리즘/상태 관리 코드  
**왜 문제인가:** 테스트 불가, 동시성 버그 (UI 스레드 ≠ 시퀀스 스레드)  
**올바른 방법:** ViewModel에 로직 옮김; code-behind는 이벤트 핸들러만  

### 4. 문자열 기반 동적 로드 (리소스맵 없음)

**무엇이 일어나는가:** `SequenceHandler[packet.Identifier.ToString()]` 직접 호출  
**왜 문제인가:** 매직 문자열, 사이트 매핑 미등록 시 null 반환  
**올바른 방법:** ResourceMap.SetIdentifier() → 이름 검증 후 조회  

## 오류 처리

**전략:** 실패를 반환값 (bool, EContextResult) 로 전파, 예외 아님  

**패턴:**  
```csharp
// Halcon 호출은 항상 try { } catch { return false; }
try {
    HOperatorSet.EdgesImage(...);
    return true;
} catch {
    return false;
}

// ActionBase: 실패 시 FinishAction(EContextResult.Error)
if (!TryMeasure()) {
    FinishAction(EContextResult.Error);
    return Context;
}

// SequenceBase: 에러 상태 감지 → OnError 콜백 → 로그/UI
if (Context.Result == EContextResult.Error) {
    OnError?.Invoke(Context);
    State = EContextState.Error;
}
```

## 횡단 관심사

**로깅:** ELogType enum 별 파일 (Trace, Camera, Error 등); Logging.PrintLog() 호출  
**검증:** 공개 엔트리에서 인자 검증 후 내부 로직 호출  
**인증:** LoginManager (사용자 권한)  
**상태 집계:** SequenceHandler.IsIdle 전체 상태, 또는 TryGetBlockingSequence() 세밀한 차단 판정  

---

*건축 분석: 2026-09-15*
