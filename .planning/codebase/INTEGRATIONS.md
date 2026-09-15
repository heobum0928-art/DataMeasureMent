---
last_mapped_commit: f30f7c42
analysis_date: 2026-09-15
---

# 외부 통합

**분석 일시:** 2026-09-15

## 카메라 & 영상 입력

### 카메라 하드웨어 추상화

**공통 인터페이스:**
- 추상 클래스: `VirtualCamera` (`WPF_Example/Device/Camera/VirtualCamera.cs`)
- 모든 카메라는 `DeviceHandler.GrabHalconImage(param)` 호출로 `HImage` 반환
- 실제 하드웨어 유형 (Basler, HIK, MIL) 또는 시뮬레이션 투명성 제공

**지원 카메라 타입:**
```csharp
public enum ECameraType {
    Virtual,   // 시뮬레이션 (SIMUL_MODE)
    Basler,    // 바슬러 GigE/USB
    HIK,       // 하이크비전 (H264V, GB200, 등)
    MIL,       // Matrox MIL Lite 10.0 (CXP 사용, Phase 41+)
}
```

### Basler 카메라
**SDK:** Basler Pylon 1.1.0
- 참조: `libs\Basler.Pylon.dll`
- 구현: `WPF_Example/Device/Camera/Basler/BaslerCamera.cs` (Grab, Open, Close, 속성)
- 속성: `WPF_Example/Device/Camera/Basler/BaslerCameraProperty.cs`
- 기능:
  - 디바이스 열거: `BaslerCamera.EnumerateDevice()`
  - 디바이스명 조회: `BaslerCamera.GetDeviceName(index)`
  - HALCON 이미지 획득: `GrabHalconImage()`

### Hikvision (HIK/MvCam) 카메라
**SDK:** MvCamCtrl.Net 4.1.0.3
- 참조: `libs\MvCamCtrl.Net.dll`
- 구현: `WPF_Example/Device/Camera/Hik/HikCamera.cs` (Grab, Open, Close, 속성)
- 속성: `WPF_Example/Device/Camera/Hik/HikCameraProperty.cs`
- 기능:
  - 디바이스 열거: `HikCamera.EnumerateDevice()`
  - 디바이스명 조회: `HikCamera.GetDeviceName(index)`
  - HALCON 이미지 획득: `GrabHalconImage()` (픽셀 버퍼 unsafe 포인터 사용)

### Matrox MIL 카메라 (CXP)
**SDK:** Matrox MIL Lite 10.0
- 참조: `C:\Program Files\Matrox Imaging\MIL\MIL.NET\Matrox.MatroxImagingLibrary.dll`
- 구현: `WPF_Example/Device/Camera/Mil/MilCamera.cs` (Grab, Open, Close, 속성)
- 속성: `WPF_Example/Device/Camera/Mil/MilCameraProperty.cs`
- 기능:
  - CXP 카메라 그랩 (Phase 41 이후 추가)
  - HALCON 이미지 변환

### 가상 카메라 (시뮬레이션)
**구현:** `WPF_Example/Device/Camera/VirtualCamera.cs`
- SIMUL_MODE 활성화 시 사용 (개발/테스트 PC 전용)
- 로컬 파일에서 테스트 이미지 로드 (`GetSimulatedImage()`)
- 프로토타이핑, 수동 테스트용

### 디바이스 등록 & 초기화
**위치:** `WPF_Example/Device/DeviceHandler.cs` (프레임워크) + `WPF_Example/Custom/Device/DeviceHandler.cs` (프로젝트 특화)
- 싱글턴: `DeviceHandler.Handle`
- 메서드: `Initialize()` → 모든 카메라 열거 및 열기
- 결과: `EInitializeResult` 플래그 (Success, NoCamera, NotEnoughCamera, OpenFail 등)
- 등록: `AddVirtualCamera()`, `AddBaslerCamera()`, `AddHikCamera()`, `AddMilCamera()`

**카메라 정보:**
- 클래스: `DeviceInfo` — 카메라 타입, 해상도, 회전, 좌우뒤집기, 트리거 소스
- 트리거 소스: Software, Hardware_Line0/1/2/3

## 조명 제어

### 조명 하드웨어 추상화
**공통 인터페이스:**
- 추상 클래스: `VirtualLightController` (`WPF_Example/Device/LightController/`)
- 모든 조명은 `LightHandler.SetOnOff()`, `LightHandler.SetLevel()` 호출로 제어
- 실제 하드웨어 (JPF, Pamtek) 또는 가상 투명성 제공

### JPF 조명 컨트롤러
**하드웨어:** JPF-1208 (8 채널)
- 구현: `WPF_Example/Device/LightController/JPFLightController.cs`
- 통신: Serial COM 포트 (`System.IO.Ports.SerialPort`)
- 프로토콜:
  - ON 전체: `#Oa1&` (모든 채널 켜기)
  - OFF 전체: `#Oa0&` (모든 채널 끄기)
  - 채널 레벨 설정: `#A{channel}{level:000}&` (레벨 0-255)
  - 전류 제한: `#I{ampere:0000}&` (최대 전류 설정, Phase 64 LIGHT-01)
- 초기화: 모든 채널 ON → 레벨 150 → 레벨 0 시험

### Pamtek 조명 컨트롤러
**구현:** `WPF_Example/Device/LightController/PamtekLightController.cs`
- 통신: Serial COM 포트 (`System.IO.Ports.SerialPort`)
- 프로토콜: JPF와 유사
  - 채널별 ON/OFF + 레벨: `#A{channel+1}{level:000}&`
  - 전류 제한: `#I{ampere:0000}&`

### 가상 조명 컨트롤러
**구현:** `WPF_Example/Device/LightController/VirtualLightController.cs`
- 시뮬레이션 전용 (상태 메모리에만 보관, 실 송신 안 함)
- 테스트, 개발용

### 조명 관리자
**위치:** `WPF_Example/Device/LightController/LightHandler.cs` (프레임워크) + `WPF_Example/Custom/Device/LightHandler.cs` (프로젝트 특화)
- 싱글턴: `LightHandler.Handle`
- 주기: 조명 상태 체크/쓰기 스레드 (1ms 폴링)
- 메서드:
  - `SetOnOff(index, channel, state)` — 채널 ON/OFF
  - `SetLevel(index, channel, level)` — 채널 레벨 설정 (0-255)
- 오류 처리: 최대 3회 재시도, 초과 시 `LightFailEvent` 발생

**채널 한계:**
- `CHANNEL_LIMIT = 8` — 컨트롤러당 최대 채널 (JPF-1208 대응, Phase 64 LIGHT-01)

### 조명 그룹 (논리 그룹화)
**구현:** `WPF_Example/Device/LightController/LightGroup.cs`
- 물리 채널을 논리 그룹으로 매핑 (예: "TopLight", "SideLight", ...)
- TCP 요청에서 그룹명 수신 → 실제 채널 번호로 변환

## TCP 통신 & 프로토콜

### VisionServer (서버)
**위치:** `WPF_Example/TcpServer/VisionServer.cs`
- 부모 클래스: `TcpServer` (기본 TCP 구현 담당)
- 기본 포트: 2505 (`SystemSetting.Handle.ServerPort`)
- 프로토콜 프레임 형식: `$<COMMAND>:<Fields>@`
  - STX (시작): `$` (0x24)
  - ETX (종료): `@` (0x40)
  - 필드 구분: `,` (쉼표)
  - 명령 구분: `:` (콜론)

### 수신 명령 타입 (VisionRequestType)
**정의:** `WPF_Example/TcpServer/VisionRequestPacket.cs`

| 명령 | 상수 | 패킷 클래스 | 용도 |
|------|------|-----------|------|
| `$RECIPE:site,recipeName@` | RecipeChange | RecipeChangePacket | 레시피 변경 |
| `$GET_RECIPE:site,maxCount,option@` | RecipeGet | RecipeGetPacket | 레시피 목록 조회 |
| `$SITE_STATUS:site@` | SiteStatus | SiteStatusPacket | 사이트 상태 조회 |
| `$LIGHT:site,type,onOffBits@` | Light | LightPacket | 조명 제어 |
| `$TEST:site,type,materialNum[,zIndex]@` | Test | TestPacket | 검사 요청 (v1.0: Type 필드 포함) |
| `$ALIGN_TEST:site,type@` | AlignTest | AlignTestPacket | 정렬 테스트 (Phase 63 AV-09) |
| `$ALIGN_CALIB:site,type@` | AlignCalib | AlignCalibPacket | 정렬 캘리브레이션 (Phase 63 AV-09) |
| `$PREP:site,type,zIndex@` | Prep | PrepPacket | 사전 준비/초점 설정 (Phase 64 LIGHT-01) |
| `$ALIVE@` | Alive | AlivePacket | 하트비트 (v3.0) |
| `$RESET@` | Reset | ResetPacket | 시퀀스 상태 복구 (quick-260807-lh7) |

### 프로토콜 버전 관리
**v2.6 (레거시, 기본값):**
- 포트 2505 (ServerPort)
- Test 필드: `site,materialNum,...` (Type 없음)
- 인코딩: 기본 (ASCII 호환성)

**v1.0 (신규, 선택):**
- 포트: 미정의 (향후 변경 가능)
- Test 필드: `site,type,materialNum,...` (Type 필드 삽입, v2.6 대비 인덱스 +1 시프트)
- 인코딩: UTF-8 강제 (Phase 48 PROTO-01)
- 활성화 플래그: `SystemSetting.Handle.UseProtocolV1` (기본값 false)
- 참고: Vision-Protocol-v1.0.md 문서 참고

### 응답 패킷 (VisionResponsePacket)
**위치:** `WPF_Example/TcpServer/VisionResponsePacket.cs`
- 형식: `$<RESPONSE>:<Fields>@`
- 응답 타입: TestResponse, AlignTestResponse, AlignCalibResponse, PrepResponse, SiteStatusResponse, etc.
- 판정 결과: Pass(P), Fail(F), Block(B — 다음 index 호출)
- 필드: site, 측정값, 판정, 타임스탬프 등

### 리소스 매핑 (프로토콜 ↔ 내부명)
**위치:** `WPF_Example/TcpServer/ResourceMap.cs` (프레임워크) + `WPF_Example/Custom/TcpServer/ResourceMap.cs` (프로젝트 특화)
- TCP 필드 (숫자 사이트/타입 코드) → 내부 문자열 식별자 변환
- 예: site=1, type=0 → SequenceName="Top", ActionName="TopInspection", CameraName="CAMERA_TOP"
- 메서드: `SetIdentifier(ref packet)` — 수신 패킷에 내부명 기록

### TCP 처리 흐름
**위치:** `WPF_Example/Custom/SystemHandler.cs` → `MainRun()` (1ms 폴링)
1. `VisionServer.GetRecvPacket()` — TCP 수신 메시지 파싱
2. `ResourceIdentifier.SetIdentifier()` — 필드 값 → 내부명 매핑
3. `ProcessXxx()` 메서드 호출 — 요청 타입별 처리 (ProcessTest, ProcessLight, etc.)
4. `VisionResponsePacket` 생성 및 `VisionServer.SendPacket()` — 응답 송신

## 데이터 저장소

### 파일 기반 저장
**모든 데이터는 로컬 디스크 저장 (클라우드 없음)**

**경로 관리:**
- 설정 담당: `WPF_Example/Setting/SystemSetting.cs` (싱글턴, 모든 경로 속성)
- 기본 저장소 루트: `D:\Data\` (실 운영 PC)

**디렉토리 구조:**

| 경로 | 용도 | 접근 |
|------|------|------|
| `D:\Data\Recipe\` | 검사 레시피 (INI, JSON) | RecipeSavePath |
| `D:\Data\Calibration\` | 캘리브레이션 데이터 (Halcon ROI, 교정 파라미터) | CalibrationSavePath |
| `D:\Data\Trace\` | 추적 로그 (시퀀스 흐름) | TraceLogSavePath |
| `D:\Data\Image\` | 획득 원본 이미지 | ImageSavePath |
| `D:\Data\Result\` | 검사 결과 이미지 (오버레이 포함) | ResultSavePath |
| `D:\Data\Error\` | 에러 로그 이미지 | ErrorSavePath |
| `D:\Data\Camera\` | 카메라 드라이버 로그 | CameraLogSavePath |
| `D:\Data\Light\` | 조명 제어 로그 + light.ini (설정) | LightControllerPath, LightConfigPath |
| `D:\Data\TcpConnection\` | TCP 통신 로그 | TcpConnectionPath |
| `D:\Data\Flow\` | 흐름 진단 로그 (Phase 26+) | FlowLogSavePath |
| `D:\Data\Algorithm\` | 알고리즘 진단 로그 (Phase 77+, ELogType.Algorithm) | AlgorithmLogSavePath |
| `D:\Data\Statistics\` | 양산 이력 통계 CSV (Phase 40 STAT-01) | StatisticsSavePath |
| `D:\Data\Load\`, `D:\Data\Save\` | 맵 데이터 로드/저장 | MapDataLoadPath, MapDataSavePath |
| `D:\Data\account.db` | 사용자 계정 데이터베이스 (SQL Lite) | AccountDbFilePath |
| `D:\Data\CameraConfig\` | 카메라 설정 파일 (.cfg, Basler/Hik 설정) | CameraConfigPath |
| `D:\Data\DisplayConfig.ini` | 디스플레이 구성 파일 | DisplayConfigFilePath |

### 설정 파일 (INI & JSON)
**위치:** 애플리케이션 기본 디렉토리 (bin\x64\Debug\ 등)

| 파일 | 형식 | 내용 | 접근 클래스 |
|------|------|------|-----------|
| `Setting.ini` | INI | 호환성 유지 (구 포맷) | SystemSetting.Load/Save() |
| `Setting.json` | JSON | 현재 설정 (신규) | SystemSetting.Load/Save() |
| `light.ini` | INI | 조명 컨트롤러 포트/채널 매핑 | LightHandler 초기화 |

### 로그 파일 정책
**자동 삭제:** `LogDeleteDay` 일 이상 된 로그는 자동 삭제 (기본값 30일)
- 구현: `Utility/Logging.cs` → `DeleteLogByDay()`
- 로그 타입별 서로 다른 파일: Trace.log, Camera.log, Error.log, etc.

### 검사 결과 저장
**형식:**
- 이미지: PNG (`Result/` 폴더)
- 메타데이터: JSON (결과 값, 타임스탬프, 판정)
- 통계: CSV (`Statistics/` 폴더, Phase 40 STAT-01+)
- Excel: XLSX (CpkReport, MeasurementHistory, Phase 40 OUT-02)

**구현:**
- `RawImageSaveService` — 실시간 이미지 저장 (별도 큐 스레드)
- `CaptureImageSaveService` — 캡처 이미지 저장
- `ExcelExportService` — XLSX 내보내기 (ClosedXML 사용)

## 인증 & 계정 관리

### 로그인 시스템
**구현:** `WPF_Example/Login/LoginManager.cs`
- 저장소: SQLite DB (`D:\Data\account.db`)
- 사용자명/비밀번호 검증
- 권한 등급: Admin, User, Operator 등 (프로젝트 특화)

### 자동 로그아웃
**설정:** `SystemSetting.AutoLogoutWhenRecvTest` (bool)
- 검사 요청($TEST) 수신 시 자동 로그아웃 가능 (보안 기능)

## 모니터링 & 로깅

### 로그 시스템
**위치:** `WPF_Example/Utility/Logging.cs`
- 싱글턴: `Logging.PrintLog()`, `Logging.PrintErrLog()`
- 로그 타입 (ELogType enum):
  - Trace (0) — 시퀀스 흐름, 액션 진행
  - Camera (1) — 카메라 개폐, 그랩 상태
  - LightController (2) — 조명 ON/OFF, 레벨 변경
  - TcpConnection (3) — TCP 수신/응답
  - Result (4) — 검사 판정 결과
  - Image (5) — 이미지 저장 상태
  - Error (6) — 예외, 에러 메시지
  - Flow (7) — 상세 흐름 진단 (Phase 26+)
  - Algorithm (8) — 알고리즘 진단 (Phase 77+)

**기록:** 각 로그 타입마다 별도 파일 (D:\Data\Trace\, etc.)

### 진단 로그 (FlowLog, AlgorithmLog)
**FlowLog:** `WPF_Example/Utility/FlowLog.cs`
- 매 액션 단계별 상세 로그 (Phase 26+)
- 성능 병목 진단용

**AlgorithmLog:** `ELogType.Algorithm` (Phase 77+)
- 에지 검출, 피팅, 측정 상세 로그
- Trace 탭을 오염시키지 않기 위해 분리

## 외부 라이브러리 & 런타임

### Halcon (이미지 처리 핵심)
**버전:** HALCON 24.11 Progress Steady
- 설치 위치: `C:\Program Files\MVTec\HALCON-24.11-Progress-Steady\`
- .NET 바인딩: `halcondotnet.dll` (버전 자동 감지)
- 주요 기능:
  - HImage — 이미지 객체 (grab 직후 → Dispose 필수)
  - HTuple — 동적 배열/튜플
  - HOperatorSet — 연산자 호출 (예: FindEdges, FitLine, etc.)
- 사용 위치:
  - `WPF_Example/Halcon/Algorithms/` — 에지 측정, 패턴 매칭, 캘리브레이션
  - `Custom/Sequence/Inspection/Measurements/` — FAI 측정 알고리즘

### OpenCV (보조 이미지 처리)
**버전:** OpenCV 4.8 (OpenCvSharp4 바인딩)
- 주요 기능: Mat 변환, 색 공간 변환, 기본 처리
- HImage ↔ Mat 변환: `HalconImageBridge.cs`

### 엑셀 내보내기 라이브러리
**ClosedXML:** XLSX 생성 (Microsoft Office 미필요)
- 용도: CpkReport, MeasurementHistory, BatchRun 결과

## 웹훅 & 콜백

**없음** — 모든 통신은 동기식 TCP 요청/응답 방식
- TCP 서버가 외부 요청(PLC/Handler) 수신 → 처리 → 응답 송신
- 비동기 콜백 없음 (단방향 폴링 모드)

---

*통합 감사: 2026-09-15*
