<!-- last_mapped_commit: f30f7c42 -->
# 코딩 컨벤션

**분석일시:** 2026-09-15

## 명명 규칙

### 파일

**Action 클래스:**
- 패턴: `Action_<SequenceName><Role>.cs` (e.g., `Action_TopSideInspection.cs`, `Action_BottomInspection.cs`, `Action_FAIMeasurement.cs`)
- 날짜 버전 백업(`Action_BottomInspection_0428.cs` 같은 파일)은 프로젝트에 포함되지 않음; 무시

**Sequence 클래스:**
- 패턴: `Sequence_<Name>.cs` (e.g., `Sequence_Top.cs`, `Sequence_Bottom.cs`)

**모델/데이터 클래스:**
- PascalCase 명사 (e.g., `RoiDefinition.cs`, `TeachingJob.cs`, `ShotConfig.cs`)

**서비스:**
- 패턴: `<Domain>Service.cs` (e.g., `RawImageSaveService.cs`, `TeachingStorageService.cs`, `HalconTeachingHelper.cs`)

**ViewModel:**
- 패턴: `<Feature>ViewModel.cs` (e.g., `InspectionListViewModel.cs`, `ModelFinderViewModel.cs`)

### 타입

**Enum:**
- 접두사: `E` + PascalCase (e.g., `ESequence`, `EAction`, `EContextState`, `ELogType`, `ECameraType`)
- 멤버: PascalCase (e.g., `EAction.Top_Inspection`, `ESequence.Top`)
- Step 열거형 (액션 내부): private enum `{ Init, Grab, Measure, End }` 패턴

**Interface:**
- 접두사: `I` + PascalCase (e.g., `IHalconTeachingProvider`, `ICameraParam`, `IDrawableItem`)

**Context 클래스:**
- 패턴: `<ActionName>Context` extends `ActionContext` (e.g., `TopInspectionContext`)

**Param 클래스:**
- 패턴: `<ActionName>Param` extends `CameraSlaveParam` 또는 `ParamBase` (e.g., `TopInspectionParam`)

### 속성 및 메서드

**Public 속성:**
- PascalCase (e.g., `IsInitialized`, `CurrentActionIndex`, `ShotName`, `Name`, `Context`)

**Private 필드:**
- **신규 코드:** camelCase + `_` 접두사 (e.g., `_image`, `_isStopping`, `_queue`, `_jobName`)
- **레거시 코드:** 헝가리언 접두사 `p` (포인터식) (e.g., `pMyContext`, `pCamera`, `pSystemHandle`)
- **Protected 필드:** PascalCase 접두사 없음 (e.g., `Actions`, `CurAction`, `Interlock`)

**Boolean 플래그:**
- 접두사: `Is`, `Has` (e.g., `IsOpen`, `HasImage`, `IsInitialized`, `IsTaught`, `bCreated`)
- 레거시: `b` 접두사 (e.g., `bCreated`, `bValid`)

**상수:**
- UPPER_SNAKE_CASE (e.g., `MSG_STX`, `MSG_ETX`, `DEFAULT_LOG_EXT`, `CENTER_PEN_THICKNESS`, `MIN_ROI_HALF_LENGTH`)

**헝가리언 접두사** (레거시 + 코드 유지성):
- `b` → bool (e.g., `bPathMissing`, `bCreated`, `bValid`)
- `n` → int (e.g., `nActionCount`)
- `sz` → string (e.g., `szShm1`, `szJson`, `szAlgoType`, `szActionName`)
- `d` → double (e.g., `dValue`)
- `hv` → HTuple (e.g., `hvHandle`)

**메서드 (Public/Protected):**
- PascalCase (e.g., `OnCreate`, `FinishAction`, `TryInspectSingleEdge`, `Release`, `Initialize`)
- `Try` 접두사: `out` 매개변수로 bool 반환 패턴 (e.g., `TryInspectSingleEdge`, `TryRun`, `TryFitLine`)
- Lifecycle 콜백: `On<Event>` 명명 (e.g., `OnCreate`, `OnBegin`, `OnEnd`, `OnLoad`, `OnPaused`, `OnResume`)
- 이벤트 핸들러: `<Subject>Button_Click`, `<Event>Handler` 패턴

**메서드 반환:**
- `bool` — 성공/실패 판정
- `Context` 객체 — ActionBase.Run() 재정의
- `string` 요약 — 배치 처리 결과 (e.g., `MeasurementAlgorithm.Run()`)
- `null` — 파일 로드 헬퍼 (호출자가 null 체크 필수)

## 코드 스타일

### 괄호 스타일

**지정된 스타일을 사용할 것:**
- **구 모듈** (Logging, SequenceBase, VirtualCamera, Device/): K&R 스타일 — 여는 괄호가 선언과 같은 줄
  ```csharp
  public void Method() {
      // body
  }
  ```

- **신규 모듈** (Halcon/, Custom/): Allman 스타일 — 여는 괄호가 자신의 줄에
  ```csharp
  public void Method()
  {
      // body
  }
  ```

**규칙:** 수정하는 파일/모듈의 기존 스타일을 따를 것; 한 파일 안에서 섞지 말 것

### 조건문 가독성 (MANDATORY — CLAUDE.md 준수)

**삼항 `?:` 금지** → 반드시 if/else
```csharp
// 금지
return CurAction == null ? EContextState.Idle.ToString() : CurAction.Name;

// 필수
if (CurAction == null) return EContextState.Idle.ToString();
else return CurAction.Name;
```

**Null 병합 `??` 금지** → 명시적 null 분기
```csharp
// 현재 위반: 신규 Custom 코드에서 사용됨
return SequenceName ?? DeviceName ?? _jobName;  // 금지됨

// 필수
if (!string.IsNullOrEmpty(SequenceName)) return SequenceName;
else if (!string.IsNullOrEmpty(DeviceName)) return DeviceName;
else return _jobName;
```

**Null 조건 `?.` 금지** → 명시적 null 체크
```csharp
// 금지 (Custom 코드에서 일부 위반)
return _hikCamera?.LastHalconImage;

// 필수
if (_hikCamera != null) return _hikCamera.LastHalconImage;
else return null;
```

**Switch 식(`=>`) 금지** → 전통 switch 문만
```csharp
// 금지
var result = state switch { Idle => "대기", Running => "실행", _ => "?" };

// 필수
switch (state) {
  case EContextState.Idle: return "대기";
  case EContextState.Running: return "실행";
  default: return "?";
}
```

**긴 조건은 이름 있는 bool로 선추출**
```csharp
// 이렇게
bool bPathMissing = string.IsNullOrEmpty(szShm1) || string.IsNullOrEmpty(szJson);
if (bPathMissing) { ... }

// 이렇게 말고
if (string.IsNullOrEmpty(szShm1) || string.IsNullOrEmpty(szJson) || refPose == null) { ... }
```

**중괄호는 한 줄짜리 분기라도 생략하지 말 것**
```csharp
// 필수
if (condition) {
    return false;
}

// 금지
if (condition) return false;
```

**중첩을 3단계 이상 피할 것** — 조기 return (guard clause)로 중첩을 낮춤

**현재 준수 상황:**
- ✅ Sequence/Device 기층: 명시적 if/else 대다수 사용
- ✅ Halcon 알고리즘: 모두 if/else 패턴 준수
- ⚠️ Custom 코드: null-coalescing `??` 사용 (109+ 인스턴스, 정책 위반)
- ⚠️ Custom 코드: null-conditional `?.` 사용 (10+ 인스턴스, 정책 위반)
- ✅ 전체: switch 식(`=>`) 미사용 (정책 준수)

### Import 순서

```csharp
using System;                              // 시스템 라이브러리
using System.Collections.Generic;         // 컬렉션
using HalconDotNet;                        // 외부 SDK (Halcon)
using OpenCvSharp;                         // 외부 SDK (OpenCV)
using ReringProject.Define;                // 프로젝트 기층 (Define)
using ReringProject.Device;                // 프로젝트 서브시스템
using ReringProject.Halcon.Algorithms;     // 프로젝트 알고리즘
using ReringProject.Sequence;              // 프로젝트 Sequence
```

**Path 별칭:** 사용 (e.g., `using TeachDiag = ReringProject.Halcon.Algorithms.TeachDiagnostics;`)

**Namespace:**
- Root: `ReringProject`
- Sub-namespaces: `ReringProject.Sequence`, `ReringProject.Device`, `ReringProject.Halcon.Algorithms`, `ReringProject.Halcon.Models`, `ReringProject.Halcon.Services`, `ReringProject.Network`, `ReringProject.UI`, `ReringProject.Utility`, `ReringProject.Setting`, `ReringProject.Define`
- Custom (프로젝트별 override): `Custom/` 폴더 파일도 동일 namespace 사용; 분리된 namespace 없음

### 주석

**필수 주석 (새 코드에서):**
- 비자명한 "왜"만 최소한으로
- 하드웨어 프로토콜을 소프트웨어 개념으로 매핑 (TCP 패킷 필드, 존/사이트 매핑)
- 비자명한 알고리즘 파라미터 (왜 sigma=1.0인지, 왜 trimCount가 적용되는지)
- Thread-safety 의도 (e.g., `// Thread-safe image buffer`)
- 구현 예정 스텁 (e.g., `// Phase 8: Halcon edge measurement will be implemented here`)

**공개 메서드에 필수:** 유틸리티/서비스 메서드는 JSDoc/자명하지 않은 동작

**금지되는 주석:**
- **날짜 주석 신규 금지** — 2026-06-11 정책 전환: `//YYMMDD hbk` 패턴은 NEW CODE에서 신규 도입 금지
  - ⚠️ **현재 상황:** Custom 코드에 1005+ 인스턴스 (레거시 코드에서 유지는 허용, 신규 작성 시 제거)
  - 레거시 기층에 69-39 인스턴스 (구 패턴으로 유지)

**#region 사용:** 대규모 파일에서 논리 그룹 (SequenceBase.cs의 delegates/enums), 메서드 내부가 아닌 top-level만

## 에러 처리

### Halcon 메서드 호출

**필수 패턴:** `try { } catch { return false; }`로 모든 `HOperatorSet.*` 호출 감싸기
```csharp
try {
    HOperatorSet.MeasurePos(image, handle, ... out rows, out cols, out amp, out dist);
}
catch {
    return false;  // bare catch OK — 예외 상세 억제
}
```

### HImage/HObject/HTuple Dispose

**필수:** try/finally 또는 using 블록으로 항상 해제
```csharp
// 단기 사용 — using
using (var image = new HImage(imagePath)) {
    return Run(image, rois);
}

// 장기 사용 — try/finally
try {
    if (image != null) {
        // ... 작업
    }
}
finally {
    if (image != null) {
        try { image.Dispose(); } catch { }  // 누수 방지
    }
}
```

**Pattern from `MeasurementAlgorithm.cs`:**
```csharp
try {
    HOperatorSet.MeasurePos(...);  // out 변수들
}
finally {
    HOperatorSet.CloseMeasure(handle);  // 모든 경로에서 반드시
}
```

### ActionBase 에러

**반환:** Exception throw 금지 → `FinishAction(EContextResult.Error)` 호출 후 `Context` 반환
```csharp
if (failed) {
    pMyContext.InspectResult = EVisionResultType.NG;
    FinishAction(EContextResult.Error);
    return Context;
}
```

### 로깅

**Framework:** `Logging` 유틸리티 + `ELogType` enum
- 각 로그 타입은 별도 파일로 매핑 (Trace, Camera, Error, Image 등)
- 호출 시 `(int)ELogType` cast 필수

**에러 캡처 지점:**
- 예외를 잡은 바로 그 자리에서 로그 (조용히 버리지 말 것)
- 비경계 catch: 정상 동작이 깨지면 안 될 때만 (e.g., 임시 파일 삭제, 로그 회전)

## UI 아키텍처

### MVVM 적용

**신규 UI 로직:**
- ViewModel에 배치 (`<Feature>ViewModel.cs` — PropertyTools.Observable 상속)
- Code-behind은 배선만: 이벤트 핸들러 → ViewModel 메서드 1줄 호출
- 상태/표시 문자열은 ViewModel에서 생성 → View에서 바인딩

**기존 Code-Behind:**
- `MainView.xaml.cs` (4,400줄+) 같은 비대한 파일에는 신규 로직 추가 금지
- 리팩토링 phase 아니면 이번에 손대는 지점에만 MVVM 적용

**ViewModel 패턴:**
```csharp
public class ModelFinderViewModel : PropertyTools.Observable {
    public string ModelFile {
        get { return modelFile; }
        set { this.SetValue(ref modelFile, value); }
    }
    private string modelFile;
}
```

### 속성 그리드

**PropertyTools.Wpf 사용:** `[Category]`, `[DirectoryPath]`, `[AutoUpdateText]` 속성으로 Settings 윈도우 드라이브
- PropertyTools.Wpf v3.1.0 (패키지) vs 실제 `libs\PropertyTools.Wpf.dll` v1.0.0.0 버전 불일치는 메모리 기록됨

## 목표

**코드 읽기:** C# 초보자가 읽어도 흐름이 보이는 수준 (한 줄에 조건 3개 이상 금지, 심플한 구조)

**일관성:** 같은 파일 내 스타일은 일정하게 유지; 레거시와 신규 스타일 혼용 금지

---

*컨벤션 분석: 2026-09-15*
