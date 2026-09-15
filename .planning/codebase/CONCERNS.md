---
last_mapped_commit: f30f7c42
analysis_date: 2026-09-15
focus: concerns
---

# 코드베이스 우려사항 (Technical Concerns)

**분석 일시:** 2026-09-15

---

## 기술부채 (Tech Debt)

### 비대한 코드비하인드 (XAML Code-Behind Bloat)

**문제:** WPF UI 로직이 코드비하인드에 집중되어 있어 유지보수와 테스트가 어려움.

**파일:**
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — **4,601줄** (가장 심각)
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` — 2,504줄
- `WPF_Example/Custom/UI/BottomVisionView.xaml.cs` — 2,356줄
- `WPF_Example/Custom/UI/TrayVisionView.xaml.cs` — 2,231줄
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — 1,726줄
- `WPF_Example/UI/ContentItem/HalconViewerControl.xaml.cs` — 1,440줄

**영향:** 
- 단일 파일에서 여러 책임 처리 (ROI 편집, Datum 교수, 이중 이미지 교환, 속성 갱신, 오버레이 렌더링)
- 테스트 불가능
- 코드 변경 시 회귀 위험 높음

**해결 방법:**
MVVM 패턴으로 점진적 리팩토링. 새로운 로직은 ViewModel에, 복잡한 이벤트 핸들러는 분리된 behavior 클래스로 이동. `MainView.xaml.cs` → ViewModel + Attached Behaviors로 분할.

---

## 알려진 버그 (Known Bugs)

### 로그 메시지 손실 (Logging Message Loss on Native Crash)

**문제:** Logging 시스템이 비동기 `BeginInvoke()`를 사용하여, 네이티브 충돌 직전 대기 중인 로그 메시지가 파일/UI에 기록되지 않음.

**파일:** `WPF_Example/Utility/Logging.cs` 줄 385
```csharp
listView.Dispatcher.BeginInvoke( System.Windows.Threading.DispatcherPriority.Background, new Action(()=>{
    // UI 업데이트 (비동기, 지연됨)
}));
```

**증상:**
- HALCON 또는 카메라 SDK 네이티브 충돌 발생 시, 그 직전의 로그 메시지가 손실됨
- 디버깅 어려움
- 사용자는 어떤 작업에서 충돌했는지 알 수 없음

**현재 완화책:** 메모리 노트: "Logging.PrintLog는 async이고 native crash 직전 메시지를 잃음; 동기 File.AppendAllText 사용"

**권장:**
로그 큐 플러시 시 동기 파일 I/O 사용, 또는 네이티브 크래시 핸들러 등록 시 `Environment.FailFast()`로 프로세스 즉시 종료 전 로그 동기화.

---

## 보안 고려사항 (Security Considerations)

### INI 파일 기본값 위험 (INI Reflection Default Fallback Risk)

**문제:** ParamBase 반사(reflection) 기반 INI 로드에서 누락된 정수 키가 자동으로 0으로 기본값 설정됨.

**파일:** `WPF_Example/Utility/Ini.cs` 줄 179
```csharp
public int ToInt(int valueIfInvalid = 0) {
    // 키가 존재하지 않거나 파싱 실패 시 0 반환
    ...
    value = 0;
    return false;
}
```

**파일:** `WPF_Example/Sequence/Param/ParamBase.cs` 줄 378
```csharp
case "Int32":
    int iValue = loadFile[group][name].ToInt();  // 기본값 = 0
    prop.SetValue(this, iValue);
```

**위험:**
- 레시피 버전 업데이트 후 새로운 정수 파라미터가 추가되면, 구형 INI에서 읽을 때 0으로 설정됨
- 0이 유효한 값이 아닌 경우 검사 오류 발생 (예: EdgeThreshold, SampleCount 등이 0이면 알고리즘 실패)
- 조용한 실패 — 로그나 경고 없이 잘못된 파라미터로 실행

**권장:**
- 파라미터마다 의미 있는 기본값 설정 (0 대신 초기값 명시)
- 누락된 키는 로그에 기록 → `Logging.PrintLog((int)ELogType.Error, "INI 키 누락: {0}", keyName)`
- 중요 파라미터는 `required` 속성 추가

---

## 성능 병목 (Performance Bottlenecks)

### 파라미터 로드 중 과도한 예외 생성 (Exception Storm on Recipe Load)

**문제:** ParamBase.Load()에서 setter 없는 읽기전용 프로퍼티에 대해 `SetValue()` 호출 시 ArgumentException이 발생하고, 이를 예외 처리하면서 동기 파일 I/O 기반 로깅이 수행됨.

**파일:** `WPF_Example/Sequence/Param/ParamBase.cs` 줄 369-373 (260615 hbk 주석)
```csharp
// 260615 hbk Phase 43.2: setter 없는 읽기전용(계산) 프로퍼티 건너뛰기 — 레시피 로드 11s 병목 제거.
//  기존: 읽기전용 프로퍼티에도 prop.SetValue 호출 → ArgumentException → catch → PrintErrLog 동기 파일 I/O.
//  레시피 1회 로드당 약 4948회 예외+파일쓰기 발생.
if (!prop.CanWrite && type != "PropertyItem[]" && type != "ModelFinderViewModel") continue;
```

**영향:**
- 레시피 로드 시간 11초 → 현재는 고정됨 (CanWrite 체크 추가)
- 이전 버전: 파일 I/O가 동기식이라 시스템 전체 응답성 저하

**현재 상태:** 이미 개선됨 (CanWrite 가드 추가). 하지만 로거가 여전히 BeginInvoke를 사용하므로 다른 병목이 있을 수 있음.

---

## 취약한 영역 (Fragile Areas)

### 비대한 코드비하인드의 상호작용 복잡성

**파일:** `WPF_Example/UI/ContentItem/MainView.xaml.cs`

**취약한 이유:**
- 이벤트 핸들러(`PointerInfoChanged`, `RoiMoveCompleted`, `RoiDeleteRequested`, `RoiGeometryChanged` 등)가 연쇄 상태 변경 트리거
- 전역 상태 (_editingDatum, _editingFai, _editingMeasurement, _selectedDualImageMeasurement, _currentImageSource) 7개 이상 추적
- ROI 편집 모드(RectRoi, PolygonRoi, CircleRoi, TeachDatum, Calibration, PatternRoi, PatternRoi2, DistanceMeasure) 8가지 상호 작용
- 두 장짜리 이미지(DualImage) 토글 로직이 깊게 중첩

**안전한 수정 방법:**
1. 단일 이벤트 수정 시 해당 핸들러만 변경, 상태 변경은 최소화
2. 새 기능은 별도 ViewModel + Behavior로 구현
3. 변경 후 전체 ROI 편집/교육/검사 흐름 수동 테스트 필수

**테스트 커버리지 격차:** UI 테스트 없음 (WPF의 한계)

---

### 여러 센터에서 HALCON 핸들 관리

**파일:** `WPF_Example/Halcon/Algorithms/MeasurementAlgorithm.cs`

**현재 패턴:**
```csharp
try {
    HOperatorSet.GenMeasureRectangle2(... out handle);
    HOperatorSet.MeasurePos(...);
}
finally {
    HOperatorSet.CloseMeasure(handle);  // 항상 해제
}
```

**취약한 점:**
- 모든 알고리즘 호출자가 `try/finally` 패턴을 강제하지 않음 (컨벤션일 뿐)
- HALCON 3D 연산(MIL Phase 41) 추가 후 상호작용 미파악
- 대용량 이미지(16544×9200) 처리 시 메모리 누수 위험

**안전한 수정:**
HALCON 핸들을 래핑한 `using` 지원 클래스 제작 → 모든 후보자는 자동 Dispose

---

## 스케일링 한계 (Scaling Limits)

### 메모리 사용 (이미지 크기 미최적화)

**문제:** 특정 카메라(예: SIDE 뷰)가 매우 큰 해상도를 가질 수 있음 (16544×9200 추정, ≈152MB 단일 이미지).

**파일:** `WPF_Example/Device/Camera/VirtualCamera.cs` (구체적 해상도는 Custom/Device/DeviceHandler에서 설정)

**영향:**
- 이미지 2-3장 동시 메모리 로드 시 400-600MB 점유
- 특히 두 장짜리 검사(DualImage) 수행 시 각 Shot마다 2장 로드
- 낮은 사양 PC에서 메모리 부족 가능성

**현재 완화:** 
- `RawImageSaveService` 비동기 큐를 사용해 메인 검사 스레드 블로킹 최소화
- HALCON 메모리는 검사 후 즉시 해제 (`Dispose()`)

**권장:**
- 대용량 이미지는 타일(tile) 처리 또는 다운샘플링 고려
- 메모리 풀 구현 (재사용 가능한 HImage 캐시)

---

## 위험한 의존성 (Dependencies at Risk)

### PropertyTools.Wpf DLL 버전 혼동

**문제:** 프로젝트는 `libs/PropertyTools.Wpf.dll` v1.0.0.0을 참조하지만, NuGet `packages/PropertyTools.Wpf.3.1.0`도 존재.

**파일:** `WPF_Example/DatumMeasurement.csproj` 줄 150-152
```xml
<Reference Include="PropertyTools.Wpf, Version=1.0.0.0, Culture=neutral, processorArchitecture=MSIL">
  <SpecificVersion>False</SpecificVersion>
  <HintPath>libs\PropertyTools.Wpf.dll</HintPath>
</Reference>
```

**위험:** 
- 로컬 버전과 NuGet 버전이 다름
- 빌드 환경이 바뀌거나 `libs/` DLL이 삭제되면 NuGet v3.1.0 로드 → 버전 미스매치 가능

**권장:**
- NuGet 의존성 통일 (v3.1.0으로 정규화)
- 로컬 `libs/` DLL 제거 또는 버전 명시

---

### Matrox MIL 런타임 (Phase 41, CXP Grab)

**파일:** `WPF_Example/DatumMeasurement.csproj` 줄 210-214
```xml
<!-- 260602 hbk Phase 41 — Matrox MIL Lite 10.0 .NET binding (CXP grab) -->
<Reference Include="Matrox.MatroxImagingLibrary">
  <HintPath>C:\Program Files\Matrox Imaging\MIL\MIL.NET\Matrox.MatroxImagingLibrary.dll</HintPath>
  <Private>True</Private>
</Reference>
```

**위험:**
- 하드코드된 절대 경로 (`C:\Program Files\...`)
- MIL 미설치 환경에서 빌드 실패
- 현재 시스템은 주로 HALCON + HIK/Basler 카메라 사용 → MIL은 선택적 기능

**영향:** CXP 카메라 (드물게 사용) 구동 불가

**권장:**
- 조건부 빌드 (MIL_ENABLED 심볼)
- 또는 런타임 로드 + Reflection으로 Soft 의존성 처리

---

## 누락된 중요 기능 (Missing Critical Features)

### 테스트 프레임워크 부재

**문제:** 프로젝트에 xUnit/NUnit/MSTest 같은 단위 테스트 프레임워크가 없음.

**파일:** `WPF_Example/` 디렉토리에 `*.csproj.Tests` 또는 `Test/` 폴더 없음

**예외:** Python 목(mock) 스크립트만 존재
- `Test/mock_vision_client.py` — TCP 클라이언트 시뮬레이션
- `Test/mock_vision_server.py` — TCP 서버 시뮬레이션

**영향:**
- 알고리즘 변경 후 회귀 테스트 불가
- HALCON 알고리즘(MeasurementAlgorithm, FAIEdgeMeasurementService 등)의 정확성을 자동 검증할 수 없음
- 리팩토링 위험 높음

**권장:**
- xUnit 추가 → `Halcon.Algorithms.*` 테스트
- 픽스처 이미지 준비 (공개 테스트 데이터)
- CI 통합 (빌드마다 테스트 실행)

---

## 테스트 커버리지 격차 (Test Coverage Gaps)

### HALCON 알고리즘 미테스트

**테스트 안 되는 것:**
- `MeasurementAlgorithm.TryInspectSingleEdge()` — 에지 검출 정확도
- `FAIEdgeMeasurementService` — 각도/거리 측정 정확도
- `DatumFindingService` — Datum 패턴 매칭
- 모든 Measurement 파생 클래스 (`EdgePairDistanceMeasurement`, `CircleDiameterMeasurement`, `CompoundAngleMeasurement` 등)

**파일:**
- `WPF_Example/Halcon/Algorithms/`
- `WPF_Example/Custom/Sequence/Inspection/Measurements/`

**위험:** 
- 알고리즘 버그 현장 배포 후 발견 (공차 판정 오류)
- 회귀 추적 어려움

**우선도:** **높음** — 검사 정확도 직결

---

### 네트워크 패킷 처리 미테스트

**테스트 안 되는 것:**
- `VisionRequestPacket` 파싱 (TCP 요청 직렬화/역직렬화)
- `VisionResponsePacket` 구성 (응답 프로토콜)
- `ResourceMap` 매핑 (사이트/테스트 타입 → 내부 이름)
- 패킷 경계 케이스 (부분 수신, 손상된 데이터 등)

**파일:**
- `WPF_Example/TcpServer/VisionRequestPacket.cs`
- `WPF_Example/TcpServer/VisionResponsePacket.cs`
- `WPF_Example/Custom/TcpServer/ResourceMap.cs`

**위험:**
- 핸들러 호스트와의 통신 오류 검사 불완전
- 새로운 패킷 타입 추가 시 회귀

**우선도:** **중간** — 네트워크 안정성 영향

---

### UI 레이아웃/바인딩 미테스트

**테스트 안 되는 것:**
- XAML 바인딩 경로 (Typo 가능성)
- 속성창 PropertyGrid 렌더링 (`PropertyTools` 커스텀 속성)
- 데이터 템플릿 (테리토리 맵, 이중 이미지 스왑 UI)

**파일:**
- `WPF_Example/UI/` XAML 파일 (40개 이상)

**우선도:** **낮음** — UI는 런타임 수동 테스트로 충분

---

## 관리 우선도 (Priority Matrix)

| 항목 | 심각도 | 영향범위 | 권장조치 |
|------|--------|---------|---------|
| 비대한 MainView (4601줄) | 높음 | 유지보수 어려움 | MVVM 리팩토링 (점진적) |
| 로그 메시지 손실 (네이티브 충돌) | 높음 | 디버깅 어려움 | 동기 로깅 또는 크래시 핸들러 추가 |
| INI 기본값 (0) 위험 | 중간 | 조용한 파라미터 오류 | 기본값 명시 + 로깅 |
| 파라미터 로드 예외 스톰 | 낮음 | 이미 해결됨 (CanWrite 체크) | 진행 중 |
| PropertyTools.Wpf 버전 혼동 | 낮음 | 빌드 환경 민감 | NuGet 통일 |
| HALCON 알고리즘 미테스트 | 매우높음 | 검사 정확도 직결 | xUnit 추가 + 픽스처 준비 |
| 네트워크 패킷 미테스트 | 중간 | 통신 안정성 | 단위 테스트 추가 |

---

**분석 완료:** 2026-09-15  
**코드베이스 버전:** f30f7c42 (docs: STATE — 재티칭 후 동축 조명값 유지 PASS)
