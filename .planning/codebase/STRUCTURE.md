<!-- last_mapped_commit: f30f7c42; refreshed: 2026-09-15 -->
# 코드베이스 구조

**분석 일시:** 2026-09-15

## 디렉터리 레이아웃

```
WPF_Example/
├── App.xaml                      # 애플리케이션 정의, 리소스 딕셔너리
├── App.xaml.cs                   # 시작 진입점, 언어 초기화, 싱글톤 mutex
├── MainWindow.xaml               # 주 메뉴/스테이터스바/MDI 컨테이너
├── MainWindow.xaml.cs            # 우인도우 이벤트, 시스템 초기화 호출
├── SystemHandler.cs              # 싱글톤: 모든 서브시스템 소유/조율
├── VersionDefine.cs              # 버전 정보, 배포 스탬프
├── DatumMeasurement.csproj       # MSBuild 프로젝트 파일
├── packages.config               # NuGet 패키지 목록 (classic-style)
│
├── Custom/                       # 프로젝트별 오버라이드 & 구현 (부분 클래스)
│   ├── SystemHandler.cs          # SystemHandler 부분 클래스: MainRun() 폴링 루프
│   ├── Define/
│   │   ├── ID.cs                 # ESequence, EAction enum (모든 시퀀스/액션 ID)
│   │   └── ErrorCode.cs          # 에러 코드 정의
│   ├── Device/
│   │   ├── DeviceHandler.cs      # DeviceHandler 부분: RegisterRequiredDevices()
│   │   └── LightHandler.cs       # 조명 그룹 제어 (RelayCard, JPF 등)
│   ├── Sequence/
│   │   ├── SequenceHandler.cs    # SequenceHandler 부분: 시퀀스/액션 등록
│   │   ├── Top/
│   │   │   ├── Sequence_Top.cs   # TopSequence 구현
│   │   │   └── Action_TopInspection.cs  # Top 이미지 검사 액션
│   │   ├── Bottom/
│   │   │   ├── Sequence_Bottom.cs
│   │   │   └── Action_BottomInspection.cs
│   │   └── Inspection/           # 2계층 동적 FAI 측정
│   │       ├── Action_FAIMeasurement.cs  # 모든 Shot/FAI/Measurement 실행
│   │       ├── InspectionSequence.cs     # 검사 시퀀스 (Top/Bottom 통합)
│   │       ├── ShotConfig.cs      # Shot 레시피 (카메라 위치, 시뮬 이미지)
│   │       ├── FAIConfig.cs        # FAI 항목 (ROI, 에지 파라미터, Measurement 컬렉션)
│   │       ├── DatumConfig.cs      # 기준점 구성 (원/선/교점)
│   │       ├── MeasurementBase.cs  # 측정 추상 클래스 (공차/기준값)
│   │       ├── MeasurementFactory.cs # 타입명 → Measurement 생성자
│   │       ├── Measurements/      # 구체적 측정 타입
│   │       │   ├── EdgeToLineDistanceMeasurement.cs
│   │       │   ├── CircleDiameterMeasurement.cs
│   │       │   ├── DualImageEdgeDistanceMeasurement.cs  # 2장 측정 (가로/세로)
│   │       │   ├── PointToPointDistanceMeasurement.cs
│   │       │   ├── LineToLineAngleMeasurement.cs
│   │       │   ├── CompoundCenterBDistanceMeasurement.cs # 복합 측정
│   │       │   └── ... (총 13+ 타입)
│   │       ├── InspectionRecipeManager.cs  # Shot/FAI 동적 로드/빌드
│   │       ├── InspectionMasterParam.cs    # 검사 마스터 파라미터
│   │       ├── CycleResultSerializer.cs    # 검사 결과 → JSON/CSV 저장
│   │       ├── RepeatRunService.cs         # 반복 측정 N회 통계
│   │       ├── BatchRunService.cs          # 배치 실행
│   │       ├── MeasurementHistoryCsvLoader/Writer.cs
│   │       ├── DynamicPropertyHelper.cs    # PropertyGrid 동적 필터
│   │       ├── EDatumAlgorithm.cs          # Datum 알고리즘 열거형
│   │       ├── EImageSource.cs             # 이미지 소스 (원본/캡처)
│   │       ├── EAngleValidationStatus.cs   # 각도 검증 상태
│   │       └── EdgeOptionLists.cs          # 에지 방향/극성 옵션
│   ├── TcpServer/
│   │   └── ResourceMap.cs         # 정수 사이트/타입 ↔ 문자 매핑
│   ├── EthernetVision/            # Align 비전 (Tray/Picker)
│   │   ├── EthernetVisionHandler.cs        # Ethernet Align 서버
│   │   ├── AlignShapeMatchService.cs       # Shape matching 위치 정렬
│   │   ├── PickerCenterCalibrationService.cs  # 피커 중심 교정
│   │   ├── AlignVerifyRetention.cs         # Align 검증 기록 관리
│   │   ├── AlignVerifyCsvLoader/Writer.cs
│   │   ├── AlignRefPose.cs                 # 기준 자세
│   │   ├── AlignResult.cs                  # 정렬 결과 (delta_x, y, theta)
│   │   └── EBottomAlignSlot.cs             # Bottom Align 슬롯 정의
│   ├── Export/                    # 결과 내보내기
│   │   ├── ExcelExportService.cs           # 검사 결과 Excel 내보내기
│   │   ├── CpkReportExportService.cs       # CPK 통계 리포트
│   │   ├── RepeatExcelExportService.cs
│   │   └── ChartImageCapture.cs            # 차트 PNG 캡처
│   ├── UserData/
│   │   └── GlobalUserData.cs      # 사용자 작업 공간 (모드, 필터 등)
│   └── UI/
│       └── ... (뷰모델 및 커스텀 UI 로직)
│
├── Sequence/                     # 시퀀스 프레임워크 (재사용 가능)
│   ├── Sequence/
│   │   ├── SequenceBase.cs        # 상태머신 기본 클래스 (Idle→Running→Finish/Error)
│   │   ├── SequenceContext.cs     # 라이브 상태, 결과 이미지, 오버레이
│   │   ├── SequenceBuilder.cs     # 빌더 패턴 헬퍼
│   │   └── SequenceHandler.cs     # 시퀀스 레지스트리 & 실행 제어
│   ├── Action/
│   │   ├── ActionBase.cs          # 템플릿 메서드: Run() 오버라이드, Step 루프
│   │   └── ActionContext.cs       # 액션 실행 결과, 오류 코드
│   └── Param/
│       ├── ParamBase.cs           # 리플렉션 기반 INI 직렬화
│       ├── CameraMasterParam.cs   # 시퀀스 마스터 파라미터
│       ├── CameraSlaveParam.cs    # 액션별 카메라 파라미터
│       └── CameraParam.cs         # ICameraParam 인터페이스
│
├── Device/                       # 디바이스 프레임워크
│   ├── DeviceHandler.cs          # 카메라 레지스트리, 생명주기
│   ├── Camera/
│   │   ├── VirtualCamera.cs       # 추상 카메라 (모든 SDK의 공통 인터페이스)
│   │   ├── Basler/
│   │   │   ├── BaslerCamera.cs    # Basler Pylon SDK 래핑
│   │   │   └── BaslerCameraParam.cs
│   │   ├── Hik/
│   │   │   ├── HikCamera.cs       # Hikvision MVS SDK 래핑
│   │   │   └── HikCameraParam.cs
│   │   └── Mil/
│   │       ├── MilCamera.cs       # MIL SDK 래핑 (레거시)
│   │       └── MilCameraParam.cs
│   └── LightController/           # 조명 제어 인터페이스
│       ├── ILightController.cs
│       ├── SerialLightController.cs
│       └── RelayCard/
│
├── Halcon/                       # HALCON 알고리즘 계층
│   ├── Algorithms/
│   │   ├── MeasurementAlgorithm.cs        # 에지 탐지, ROI 관리
│   │   ├── FAIEdgeMeasurementService.cs   # 측정 타입별 거리/각도
│   │   ├── DatumFindingService.cs         # 기준점 탐지
│   │   ├── VisionAlgorithmService.cs      # 공통 이미지 처리
│   │   ├── PatternMatchService.cs         # 패턴 매칭
│   │   ├── CheckerboardCalibrationService.cs  # 카메라 교정
│   │   ├── AlignShapeMatchService.cs      # Align shape matching
│   │   ├── RoiLineIntersectionAlgorithm.cs # ROI 교선 계산
│   │   └── TeachDiagnostics.cs            # 교육 진단/검증
│   ├── Models/
│   │   ├── RoiDefinition.cs       # ROI 모양 (회전 직사각형, 다각형)
│   │   ├── EdgeInspectionOverlay.cs # 오버레이 그리기 데이터
│   │   ├── TeachingJob.cs         # 교육 작업
│   │   └── ... (모델 클래스들)
│   ├── Services/
│   │   ├── HalconImageBridge.cs   # HImage ↔ Mat 변환
│   │   ├── TeachingStorageService.cs  # 교육 이미지 저장/로드
│   │   └── ... (유틸리티 서비스)
│   └── Display/
│       └── ... (렌더링 헬퍼)
│
├── TcpServer/                    # TCP 프로토콜 프레임워크
│   ├── VisionServer.cs            # TCP 서버 (port 2505)
│   ├── TcpServer.cs               # 저수준 소켓 래퍼
│   ├── VisionRequestPacket.cs     # 요청 직렬화 (TEST, PREP, ALIGN 등)
│   ├── VisionResponsePacket.cs    # 응답 직렬화
│   ├── ResourceMap.cs             # 프로토콜 리소스 매핑
│   ├── AlarmEventArgs.cs          # 오류 이벤트
│   └── Message*.cs                # 각 패킷 타입 (TestPacket, AlignPacket 등)
│
├── UI/                           # WPF 사용자 인터페이스
│   ├── MainView.xaml(.cs)         # 메인 콘텐츠 (카메라 이미지 + 결과)
│   ├── ContentItem/               # 주요 뷰
│   │   ├── CameraView.xaml
│   │   ├── RecipeEditView.xaml    # 레시피 트리 편집기
│   │   ├── ImageReviewView.xaml   # 검사 결과 검토
│   │   └── TeachingView.xaml      # 교육 UI
│   ├── ControlItem/               # 재사용 컨트롤
│   │   └── ... (버튼, 라벨, 커스텀 컨트롤)
│   ├── Dialog/
│   │   ├── CustomMessageBox.xaml
│   │   ├── SettingDialog.xaml
│   │   └── ... (모달 창)
│   ├── ViewModel/                 # 뷰모델 (MVVM)
│   │   ├── MainViewViewModel.cs
│   │   ├── ImageReviewViewModel.cs
│   │   ├── RecipeEditViewModel.cs
│   │   └── ... (각 뷰 VM)
│   ├── Setting/
│   │   └── SettingWindow.xaml(.cs) # 시스템 설정 PropertyGrid
│   ├── Recipe/
│   │   └── RecipeWindow.xaml(.cs)  # 레시피 관리
│   ├── Statistics/
│   │   └── StatisticsWindow.xaml   # CPK/통계 차트 (ChartDirector)
│   ├── Log/
│   │   └── LogViewWindow.xaml      # 로그 탭 뷰어
│   ├── Device/
│   │   └── DeviceSelector.xaml     # 카메라/조명 선택 대화
│   ├── Login/
│   │   └── LoginWindow.xaml        # 사용자 로그인
│   ├── Theme/
│   │   └── ... (리소스 딕셔너리, 테마 색상)
│   ├── ProcessMonitor/
│   │   └── ProcessMonitorView.xaml # 프로세스 상태 모니터
│   └── TcpServer/
│       └── TcpServerStatusView.xaml # TCP 클라이언트 상태
│
├── Setting/                      # 시스템 설정 & 구성
│   ├── SystemSetting.cs           # 싱글톤: INI/JSON 설정 읽기
│   └── Custom/SystemSetting.cs    # SystemSetting 부분
│
├── Utility/                      # 유틸리티 서비스
│   ├── Logging.cs                 # 다중 로그 파일 관리
│   ├── RawImageSaveService.cs     # 원본 이미지 비동기 저장
│   ├── CaptureImageSaveService.cs # 캡처 이미지 저장
│   ├── RecipeFiles.cs             # 레시피 파일 레지스트리
│   ├── LocalizationResource.cs    # 언어 문자열 (한영)
│   ├── VirtualCamera.cs (또는 Device/ 이동)  # 카메라 구현체
│   ├── HalconImageBridge.cs       # HImage ↔ Bitmap/Mat 변환
│   └── ... (IniHelper, JsonHelper 등)
│
├── Login/
│   └── LoginManager.cs            # 사용자 권한 관리
│
├── Resource/
│   └── ... (XAML 리소스, 아이콘, 이미지)
│
├── Properties/
│   ├── AssemblyInfo.cs            # 어셈블리 메타데이터
│   └── Resources.resx             # .NET 리소스 파일
│
├── libs/                          # 로컬 DLL (직접 참조)
│   ├── PropertyTools.Wpf.dll
│   ├── WPF.MDI.dll                # MDI 창 레이아웃
│   ├── ImageGlass.ImageBox.dll    # 이미지 뷰어
│   └── ... (기타 로컬 DLL)
│
├── bin/                           # 빌드 출력 (무시)
└── obj/                           # 빌드 임시 (무시)
```

## 디렉터리 용도

### 핵심 레이어

| 디렉터리 | 용도 | 재사용성 | 특징 |
|---------|------|--------|------|
| `Sequence/` | 시퀀스/액션 프레임워크 | High | 모든 프로젝트 공통 |
| `Device/` | 디바이스 추상화 | High | 카메라/조명 SDK 래핑 |
| `Halcon/` | 이미지 처리 알고리즘 | Medium | HALCON 의존성, 프로젝트별 커스텀 |
| `TcpServer/` | TCP 프로토콜 | Medium | 프로토콜 확장 용이 |
| `Custom/` | 프로젝트별 구현 | Low | 부분 클래스, 프로젝트별 고유 |

### 사용자 인터페이스

| 디렉터리 | 용도 |
|---------|------|
| `UI/` | XAML 뷰 + ViewModel (MVVM 패턴) |
| `UI/ContentItem/` | 주요 컨텐츠 뷰 (카메라, 레시피, 결과 검토) |
| `UI/ControlItem/` | 재사용 컨트롤 (커스텀 버튼, 라벨) |
| `UI/Dialog/` | 모달 대화 (설정, 메시지박스) |
| `UI/ViewModel/` | 각 뷰의 데이터 바인딩 로직 |
| `UI/Theme/` | 리소스 딕셔너리 (색상, 스타일) |

### 프로젝트별 추가 기능

| 디렉터리 | 용도 |
|---------|------|
| `Custom/EthernetVision/` | Tray/Picker Align 비전 (하드웨어 고유) |
| `Custom/Export/` | Excel/CSV 결과 내보내기 |
| `Custom/ErrorCode/` | 프로젝트별 에러 코드 정의 |

## 주요 파일 위치

### 엔트리 포인트

| 파일 | 목적 |
|------|------|
| `App.xaml.cs` | 애플리케이션 시작 (언어/mutex) |
| `MainWindow.xaml.cs` | 메인 윈도우 Loaded → SystemHandler.Initialize() |
| `SystemHandler.cs` | 싱글톤 생성자 + 초기화 |
| `Custom/SystemHandler.cs` | MainRun() 폴링 루프 (TCP 라우팅) |

### 시퀀스 & 액션

| 파일 | 목적 |
|------|------|
| `Sequence/Sequence/SequenceBase.cs` | 상태머신 프레임워크 |
| `Sequence/Action/ActionBase.cs` | Step 루프 템플릿 메서드 |
| `Custom/Sequence/SequenceHandler.cs` | 시퀀스/액션 등록 (RegisterSequences) |
| `Custom/Sequence/Top/Action_TopInspection.cs` | Top 이미지 검사 구현 |
| `Custom/Sequence/Bottom/Action_BottomInspection.cs` | Bottom 이미지 검사 구현 |

### FAI 측정 (2계층 모델)

| 파일 | 목적 |
|------|------|
| `Custom/Sequence/Inspection/ShotConfig.cs` | Shot 레시피 (Z축, 시뮬 이미지, FAI 리스트) |
| `Custom/Sequence/Inspection/FAIConfig.cs` | 검사 항목 (ROI, 에지 파라미터, Measurement 컬렉션) |
| `Custom/Sequence/Inspection/MeasurementBase.cs` | 측정 추상 클래스 (공차/기준값) |
| `Custom/Sequence/Inspection/MeasurementFactory.cs` | 타입명 → 구체적 Measurement 생성 |
| `Custom/Sequence/Inspection/Action_FAIMeasurement.cs` | 모든 Shot/FAI/Measurement 실행 |
| `Custom/Sequence/Inspection/Measurements/*.cs` | 13+ 구체적 측정 타입 |

### 레시피 & 설정

| 파일 | 목적 |
|------|------|
| `Setting/SystemSetting.cs` | INI/JSON 읽기 (경로, 포트, 플래그) |
| `Utility/RecipeFiles.cs` | 레시피 파일 등록/로드 |
| `Custom/Sequence/Inspection/InspectionRecipeManager.cs` | Shot/FAI 동적 로드 |

### 카메라 & 디바이스

| 파일 | 목적 |
|------|------|
| `Device/DeviceHandler.cs` | 카메라 레지스트리 |
| `Device/Camera/VirtualCamera.cs` | 추상 카메라 인터페이스 |
| `Device/Camera/Basler/BaslerCamera.cs` | Basler Pylon SDK |
| `Device/Camera/Hik/HikCamera.cs` | Hikvision MVS SDK |
| `Custom/Device/LightHandler.cs` | 조명 그룹 제어 |

### TCP 프로토콜

| 파일 | 목적 |
|------|------|
| `TcpServer/VisionServer.cs` | TCP 서버 (port 2505) |
| `TcpServer/VisionRequestPacket.cs` | 요청 직렬화 (TEST, PREP 등) |
| `TcpServer/VisionResponsePacket.cs` | 응답 직렬화 |
| `Custom/TcpServer/ResourceMap.cs` | 사이트/타입 ↔ 시퀀스/액션 매핑 |

### Halcon 알고리즘

| 파일 | 목적 |
|------|------|
| `Halcon/Algorithms/MeasurementAlgorithm.cs` | 에지 탐지, ROI 관리 |
| `Halcon/Algorithms/FAIEdgeMeasurementService.cs` | 거리/각도 측정 |
| `Halcon/Algorithms/DatumFindingService.cs` | 기준점 탐지 (원/선/교점) |
| `Halcon/Models/RoiDefinition.cs` | ROI 형태 정의 |

### Align 비전 (선택사항)

| 파일 | 목적 |
|------|------|
| `Custom/EthernetVision/EthernetVisionHandler.cs` | Ethernet Align 서버 |
| `Custom/EthernetVision/AlignShapeMatchService.cs` | Shape matching 위치 정렬 |
| `Custom/EthernetVision/PickerCenterCalibrationService.cs` | 피커 중심 교정 |

### 유틸리티

| 파일 | 목적 |
|------|------|
| `Utility/Logging.cs` | 다중 로그 파일 (Trace, Camera, Error 등) |
| `Utility/RawImageSaveService.cs` | 원본 이미지 비동기 저장 |
| `Utility/CaptureImageSaveService.cs` | 캡처 이미지 저장 |

## 명명 규칙

### 파일명

| 패턴 | 예제 | 설명 |
|------|------|------|
| `Action_<Role>.cs` | `Action_TopInspection.cs` | 액션 클래스 |
| `Sequence_<Name>.cs` | `Sequence_Top.cs` | 시퀀스 클래스 |
| `<Domain>Service.cs` | `DatumFindingService.cs` | 서비스 클래스 |
| `<Feature>ViewModel.cs` | `ImageReviewViewModel.cs` | ViewModel 클래스 |
| `E<Type>.cs` | `EDatumAlgorithm.cs` | Enum 타입 |
| `I<Interface>.cs` | `ICameraParam.cs` | 인터페이스 |

### 클래스명

| 패턴 | 예제 |
|------|------|
| 액션 | `TopInspectionAction` 또는 `Action_TopInspection` (부분 클래스) |
| 시퀀스 | `Sequence_Top` (부분 클래스) |
| Context | `TopInspectionContext` (extends ActionContext) |
| Parameter | `TopInspectionParam` (extends CameraSlaveParam) |
| Enum | `ESequence`, `EAction`, `ELogType` |
| Interface | `IHalconTeachingProvider`, `ICameraParam` |
| Service | `DatumFindingService`, `RawImageSaveService` |
| ViewModel | `ImageReviewViewModel` |

### 내 필드명

| 규칙 | 예제 |
|------|------|
| 헝가리언 bool | `bCreated`, `bPathMissing`, `bHasImage` |
| 헝가리언 int | `nStep`, `nZIndex`, `nActionCount` |
| 헝가리언 string | `szSeqName`, `szPath` |
| 헝가리언 double | `dMeasuredValue` |
| HTuple | `hvEdges` |
| Private 필드 | `_image`, `_isStopping` (또는 legacy `pImage`) |

## 신규 코드 추가 위치

### 새 시퀀스 추가

1. `Custom/Define/ID.cs`: `ESequence` enum에 새 멤버 추가
2. `Custom/Sequence/NewSeq/Sequence_NewSeq.cs`: SequenceBase 상속
3. `Custom/Sequence/NewSeq/Action_NewSeqStep.cs`: ActionBase 상속
4. `Custom/Sequence/SequenceHandler.cs`: RegisterSequences() 등록
5. `Custom/Device/DeviceHandler.cs`: 카메라 등록 (필요 시)

### 새 측정 타입 추가

1. `Custom/Sequence/Inspection/Measurements/NewMeasurement.cs`: MeasurementBase 상속
2. `Custom/Sequence/Inspection/MeasurementFactory.cs`: Create() 메서드에 타입 추가
3. `Custom/Sequence/Inspection/EdgeOptionLists.cs`: 옵션 목록 추가 (필요 시)

### 새 Halcon 알고리즘 추가

1. `Halcon/Algorithms/NewAlgorithm.cs`: 알고리즘 클래스 (public static 메서드)
2. `Action_*.cs` Run() 메서드에서 호출

### UI 뷰 추가

1. `UI/ContentItem/NewView.xaml`: XAML 정의
2. `UI/ContentItem/NewView.xaml.cs`: 코드비하인드 (배선만, 로직은 ViewModel)
3. `UI/ViewModel/NewViewModel.cs`: 데이터 바인딩 로직
4. `MainView.xaml`: TabControl/ContentControl에 추가

### TCP 패킷 타입 추가

1. `TcpServer/NewPacket.cs`: 요청/응답 클래스 정의
2. `Custom/TcpServer/ResourceMap.cs`: 매핑 로직 추가
3. `Custom/SystemHandler.cs` MainRun(): ProcessNewPacket() 메서드 추가

## 특수 디렉터리

### bin/ x64/ 및 obj/

**무시 대상:** 모든 바이너리 & 빌드 임시 파일  
**컴파일 확인:** DatumMeasurement.csproj의 `<Compile Include="..."` 항목만 포함

### Custom/ 부분 클래스 패턴

**구조:** 각 프레임워크 클래스가 부분 클래스로 Custom 폴더에 확장됨  
**예:**
- `SystemHandler.cs` (base) + `Custom/SystemHandler.cs` (partial) → MainRun() 폴링 추가  
- `DeviceHandler.cs` (base) + `Custom/Device/DeviceHandler.cs` (partial) → 카메라 등록 추가  
- `SequenceHandler.cs` (base) + `Custom/Sequence/SequenceHandler.cs` (partial) → 시퀀스/액션 등록  

**이점:** 프레임워크는 깨끗하게, 프로젝트별 관심사는 분리  

## 파일 상태 규칙

### 컴파일 대상

- `.csproj` `<Compile Include="..."` 항목만 포함
- 날짜 버전 백업 (예: `Action_BottomInspection_0428.cs`) 제외 — 컴파일되지 않음

### 저장 위치

- 레시피 INI: 설정된 경로 (기본: 실행 폴더 또는 `Setting.RecipePath`)
- 로그 파일: `Setting.GetLogSavePath(ELogType.*)` 폴더
- 검사 이미지: 날짜별 폴더 구조 (`YYYY/MM/DD/` 등)

---

*구조 분석: 2026-09-15*
