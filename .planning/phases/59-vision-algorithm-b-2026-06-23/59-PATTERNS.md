# Phase 59: Vision Algorithm (B) - Pattern Map

**Mapped:** 2026-06-24
**Files analyzed:** 5 (3 new files + 2 modifications)
**Analogs found:** 5 / 5

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `Custom/EthernetVision/AlignResult.cs` | model/DTO | transform (data carrier) | `UI/ViewModel/CycleResultDto.cs` | role-match |
| `Custom/EthernetVision/AlignShapeMatchService.cs` | service | request-response (HALCON) | `Halcon/Algorithms/PatternMatchService.cs` | exact |
| `Custom/EthernetVision/EthernetVisionHandler.cs` (modify) | singleton handler | request-response | `Custom/EthernetVision/EthernetVisionHandler.cs` itself + `EthernetAlignCamera` composition pattern | exact |
| `.shm` path helper (inline in service) | utility | file-I/O | `Utility/RecipeFileHelper.cs` `GetPatternModelFilePath()` | role-match |
| Ref-pose sidecar JSON (inline in service) | serialization | file-I/O | `Custom/Sequence/Inspection/CycleResultSerializer.cs` + `Newtonsoft.Json` pattern | role-match |

---

## Pattern Assignments

### `Custom/EthernetVision/AlignResult.cs` (model/DTO)

**Analog:** `WPF_Example/UI/ViewModel/CycleResultDto.cs`

**Pattern:** Plain POCO class in the `ReringProject` root namespace (same as EthernetVisionHandler). No base class, no INI serialization, no PropertyGrid annotations — pure data carrier for Phase 62 TCP consumer.

**Class skeleton to copy** (CycleResultDto.cs lines 1-29 adapted):
```csharp
//260624 hbk Phase 59
namespace ReringProject {

    /// <summary>
    /// Shape matching align 결과 모델. AlignShapeMatchService.Run() 반환값.
    /// Phase 62 TCP 전송 소비(OffsetXmm/OffsetYmm/ThetaDeg).
    /// </summary>
    public class AlignResult {
        /// <summary>매칭 성공 여부. false = Found=false 이면 나머지 필드 미사용.</summary>
        public bool Found { get; set; }

        /// <summary>매칭 점수 (0~1).</summary>
        public double Score { get; set; }

        /// <summary>X Offset(mm) = dCol × (PixelResolution/1000). Row↔Y/Col↔X 규약 — UAT 에서 부호 확정.</summary>
        public double OffsetXmm { get; set; }

        /// <summary>Y Offset(mm) = dRow × (PixelResolution/1000).</summary>
        public double OffsetYmm { get; set; }

        /// <summary>Theta(deg) = curAngleDeg − refAngleDeg. Tray 모드에서는 0 / HasTheta=false.</summary>
        public double ThetaDeg { get; set; }

        /// <summary>true = Bottom 모드 (ThetaDeg 유효). false = Tray 모드 (ThetaDeg=0 무시).</summary>
        public bool HasTheta { get; set; }
    }
}
```

**Naming convention:** PascalCase public auto-properties, K&R brace style (same file as EthernetVisionHandler — match that file's style). No `[Category]`/`[Browsable]` attributes.

---

### `Custom/EthernetVision/AlignShapeMatchService.cs` (service, request-response)

**Analog:** `WPF_Example/Halcon/Algorithms/PatternMatchService.cs` (the engine this service calls)

#### A. Imports pattern (PatternMatchService.cs lines 1-10):
```csharp
//260624 hbk Phase 59
using System;
using System.IO;
using HalconDotNet;
using Newtonsoft.Json;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject {
```

Note: `ReringProject` root namespace (not `Halcon.Algorithms`) — same as EthernetVisionHandler. K&R brace style throughout.

#### B. Constants block (PatternMatchService.cs lines 21-36 + D-03):
```csharp
    public class AlignShapeMatchService {

        // Shape 엔진 고정 (D-01: NCC 미사용, AlignShapeMatchService 는 Shape 전용)
        private const string ENGINE = "Shape";

        // D-03: 모델 피라미드 레벨, 최소 스코어, 그리디니스 (PatternMatchService 기본값 재사용)
        private const int    NUM_LEVELS      = 4;
        private const double MIN_SCORE       = 0.5;
        private const double GREEDINESS      = 0.9;

        // D-03: 모드별 angle extent 기본값 (deg). 런타임 튜닝 가능 const.
        private const double TRAY_ANGLE_EXTENT_DEG   = 10.0;   // Tray = 위치 위주, 작은 범위
        private const double BOTTOM_ANGLE_EXTENT_DEG = 45.0;   // Bottom = Theta 산출, 넓은 범위

        // D-05: px→mm 변환. EthernetPixelResolution 단위 = μm/px → /1000 = mm/px
        private const double UM_PER_MM = 1000.0;

        // D-04: 이더넷 전용 하위 폴더명 (레시피 폴더 격리)
        private const string ETHERNET_ALIGN_FOLDER = "ETHERNET_ALIGN";

        // 사이드카 JSON 파일명 규약 (.shm 옆에 동일 이름.json)
        private const string REF_POSE_EXT = ".json";

        private readonly PatternMatchService _matcher;

        public AlignShapeMatchService() {
            _matcher = new PatternMatchService();   // composition — D-01
        }
```

#### C. Core TryTeach pattern (analogs: PatternMatchService.TryCreateModel L56-154 + TryFindRefPose L170-282):
```csharp
        // D-07: TryTeach = TryCreateModel + TryFindRefPose + sidecar JSON 저장.
        // ROI 파라미터는 Phase 61 UI 가 전달 — 이 서비스는 ROI 드로잉 모름.
        public bool TryTeach(
            HImage img,
            double roiRow, double roiCol, double roiPhi,
            double roiLen1, double roiLen2,
            EEthernetVisionMode mode,
            out string error)
        {
            error = null;

            if (img == null)       { error = "img is null";               return false; }
            if (mode == EEthernetVisionMode.None) { error = "mode=None"; return false; }

            string shmPath  = GetShmPath(mode);
            string jsonPath = Path.ChangeExtension(shmPath, REF_POSE_EXT);

            double angleExtentDeg = (mode == EEthernetVisionMode.Bottom)
                ? BOTTOM_ANGLE_EXTENT_DEG
                : TRAY_ANGLE_EXTENT_DEG;

            // Step 1: create_shape_model + write_shape_model (PatternMatchService 위임)
            string createErr;
            if (!_matcher.TryCreateModel(img, roiRow, roiCol, roiPhi, roiLen1, roiLen2,
                    ENGINE, angleExtentDeg, shmPath, out createErr))
            {
                error = "TryCreateModel: " + createErr;
                return false;
            }

            // Step 2: ref pose — 동일 이미지 find → refRow/Col/Angle 저장 (D-04)
            double refRow, refCol, refAngleDeg, refScore; string findErr;
            if (!_matcher.TryFindRefPose(img, ENGINE, shmPath, MIN_SCORE,
                    out refRow, out refCol, out refAngleDeg, out refScore, out findErr))
            {
                error = "TryFindRefPose: " + findErr;
                return false;
            }

            // Step 3: 사이드카 JSON 저장 (Newtonsoft.Json — D-04)
            return TrySaveRefPose(jsonPath, refRow, refCol, refAngleDeg, angleExtentDeg, out error);
        }
```

#### D. Core Run pattern (analog: InspectionSequence.TryComposeAlign lines 806-820 for dRow/dCol + D-05 offset formula):
```csharp
        // D-07: Run = read .shm+ref json → find → offset(px→mm).
        // 실패 시 Found=false 반환, 예외 throw 없음 (D-06).
        public AlignResult Run(HImage img, EEthernetVisionMode mode) {
            if (img == null || mode == EEthernetVisionMode.None) {
                return new AlignResult { Found = false };
            }

            string shmPath  = GetShmPath(mode);
            string jsonPath = Path.ChangeExtension(shmPath, REF_POSE_EXT);

            try {
                // 레퍼런스 포즈 로드 (사이드카 JSON)
                AlignRefPose refPose = LoadRefPose(jsonPath);
                if (refPose == null) {
                    Logging.PrintLog((int)ELogType.Error, "[ALIGN_SVC] ref pose json 없음: {0}", jsonPath);
                    return new AlignResult { Found = false };
                }

                // TryFindPose: 전체 이미지 검색 (margin=이미지 전폭, downsample=1)
                // — Datum Align 과 달리 이더넷은 단독 이미지라 전체 검색이 안전.
                double curRow, curCol, curAngleDeg, curScore; string findErr;
                bool bFound = _matcher.TryFindPose(
                    img, ENGINE, shmPath,
                    /*roiRow*/ 0, /*roiCol*/ 0,       // 검색영역 제한 없음 → margin 크게
                    /*roiLen1*/ 99999, /*roiLen2*/ 99999,
                    /*marginPx*/ 0, MIN_SCORE, /*downsample*/ 1.0,
                    out curRow, out curCol, out curAngleDeg, out curScore, out findErr);

                if (!bFound) {
                    Logging.PrintLog((int)ELogType.Error, "[ALIGN_SVC] find failed ({0}): {1}", mode, findErr ?? "");
                    return new AlignResult { Found = false };
                }

                // D-05: offset 산출 — cur − ref (InspectionSequence dRow/dCol 패턴 동일)
                double dRow = curRow - refPose.RefRow;
                double dCol = curCol - refPose.RefCol;
                double resMm = SystemSetting.Handle.EthernetPixelResolution / UM_PER_MM;

                bool bBottom = (mode == EEthernetVisionMode.Bottom);
                return new AlignResult {
                    Found      = true,
                    Score      = curScore,
                    OffsetXmm  = dCol * resMm,           // Col→X 축 규약 (UAT 에서 부호 확정)
                    OffsetYmm  = dRow * resMm,           // Row→Y 축 규약
                    ThetaDeg   = bBottom ? (curAngleDeg - refPose.RefAngleDeg) : 0.0,
                    HasTheta   = bBottom,
                };
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Error, "[ALIGN_SVC] Run exception: {0}", ex.Message);
                return new AlignResult { Found = false };
            }
        }
```

#### E. .shm path helper (analog: RecipeFileHelper.GetPatternModelFilePath lines 102-114):
```csharp
        // D-04: {RecipeSavePath}\{CurrentRecipeName}\ETHERNET_ALIGN\{Tray|Bottom}.shm
        // RecipeFileHelper.GetPatternModelFilePath 패턴 직접 적용 — Directory.CreateDirectory 포함.
        private string GetShmPath(EEthernetVisionMode mode) {
            string recipePath   = SystemSetting.Handle.RecipeSavePath;
            string recipeName   = SystemSetting.Handle.CurrentRecipeName;
            if (string.IsNullOrEmpty(recipeName)) recipeName = "DEFAULT";

            string folder = System.IO.Path.Combine(recipePath, recipeName, ETHERNET_ALIGN_FOLDER);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string modeFileName = (mode == EEthernetVisionMode.Bottom) ? "Bottom" : "Tray";
            return System.IO.Path.Combine(folder, modeFileName + PatternMatchService.EXTENSION_SHAPE_MODEL);
        }
```

#### F. Sidecar JSON save/load (analog: CycleResultSerializer lines 183-219 + Newtonsoft.Json):
```csharp
        // D-04: 레퍼런스 포즈 사이드카 json 저장 (Newtonsoft.Json.JsonConvert.SerializeObject 패턴)
        // CycleResultSerializer.Save 패턴: File.WriteAllText + Formatting.Indented.
        private bool TrySaveRefPose(string jsonPath, double refRow, double refCol,
            double refAngleDeg, double angleExtentDeg, out string error) {
            error = null;
            try {
                var refPose = new AlignRefPose {
                    RefRow        = refRow,
                    RefCol        = refCol,
                    RefAngleDeg   = refAngleDeg,
                    AngleExtentDeg = angleExtentDeg,
                    Engine        = ENGINE,
                };
                string json = JsonConvert.SerializeObject(refPose, Formatting.Indented);
                File.WriteAllText(jsonPath, json, System.Text.Encoding.UTF8);
                return true;
            }
            catch (Exception ex) {
                error = "TrySaveRefPose: " + ex.Message;
                return false;
            }
        }

        // D-04: 사이드카 json 로드 (CycleResultSerializer.Load 패턴: TypeNameHandling.None + try/catch null 반환)
        private AlignRefPose LoadRefPose(string jsonPath) {
            try {
                if (!File.Exists(jsonPath)) return null;
                string json = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };
                return JsonConvert.DeserializeObject<AlignRefPose>(json, settings);
            }
            catch {
                return null;
            }
        }
```

#### G. AlignRefPose sidecar model (inline nested class or separate file):
```csharp
    // D-04 사이드카 JSON 스키마. AlignShapeMatchService.cs 동일 파일 내 또는 별도 .cs.
    // CycleResultDto 패턴: 순수 POCO, public auto-properties, 기본값 있음.
    public class AlignRefPose {
        public double RefRow         { get; set; }
        public double RefCol         { get; set; }
        public double RefAngleDeg    { get; set; }
        public double AngleExtentDeg { get; set; }
        public string Engine         { get; set; } = "Shape";
    }
```

#### H. TryLoadTemplate / HasTemplate helpers (analog: EthernetAlignCamera.IsOpen 패턴):
```csharp
        // D-07: 템플릿 존재 여부 확인 (Phase 61 UI 가 버튼 활성화 조건으로 사용)
        public bool HasTemplate(EEthernetVisionMode mode) {
            if (mode == EEthernetVisionMode.None) return false;
            string shmPath  = GetShmPath(mode);
            string jsonPath = Path.ChangeExtension(shmPath, REF_POSE_EXT);
            return File.Exists(shmPath) && File.Exists(jsonPath);
        }

        // D-07: 템플릿 파일 존재 여부를 파일 시스템으로 확인 (로드는 Run 내부 지연 수행)
        public bool TryLoadTemplate(EEthernetVisionMode mode) {
            return HasTemplate(mode);   // 파일 존재 = 로드 가능 의미론
        }
```

---

### `EthernetVisionHandler.cs` — `Matcher` 프로퍼티 추가 (modification)

**Analog:** `EthernetVisionHandler.cs` itself — `Camera` 프로퍼티 + lazy 생성 패턴 (lines 18-43)

**Pattern to replicate** (lines 18-43 — Camera 프로퍼티 선언 + Initialize 내 생성):
```csharp
    // 기존 (Phase 58, line 18):
    public EthernetAlignCamera Camera { get; private set; }

    // Phase 59 추가 — 동일 패턴, Matcher 는 Initialize() 마지막에 항상 생성:
    public AlignShapeMatchService Matcher { get; private set; }
```

**Initialize() 내 추가 위치** (line 42 `IsInitialized = bConnected` 직후):
```csharp
        public void Initialize() {
            try {
                bool bModeOff = SystemSetting.Handle.EthernetVisionMode == EEthernetVisionMode.None;
                if (bModeOff) {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] mode = None, skip connect");
                    IsInitialized = false;
                    // Mode=None 에서도 Matcher 생성 — 서비스 자체는 stateless (D-02)
                    Matcher = new AlignShapeMatchService();
                    return;
                }

                Camera = new EthernetAlignCamera();
                string camIp = SystemSetting.Handle.EthernetCameraIp;
                bool bConnected = Camera.Connect(camIp);
                IsInitialized = bConnected;

                // 260624 hbk Phase 59 — D-02: Matcher lazy 생성 (카메라 성공/실패 무관, 상태 없음)
                Matcher = new AlignShapeMatchService();

                if (bConnected) {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] connected: {0}", camIp);
                }
                else {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] connect failed (fallback active): {0}", camIp);
                }
            }
            catch (Exception ex) {
                IsInitialized = false;
                Matcher = new AlignShapeMatchService();  // 예외에서도 null guard
                Logging.PrintLog((int)ELogType.Error, "[ETHERNET] Initialize error: {0}", ex.Message);
            }
        }
```

**Style note:** EthernetVisionHandler.cs 는 K&R 스타일(여는 중괄호 같은 줄). 이 파일에 추가하는 모든 코드는 K&R 유지.

---

## Shared Patterns

### HALCON try-catch + finally dispose
**Source:** `PatternMatchService.cs` (전체 파일 — 모든 public 메서드 동일 구조)
**Apply to:** `AlignShapeMatchService` 의 모든 HALCON 호출 경로
```csharp
// PatternMatchService.cs lines 78-153 (TryCreateModel) 패턴:
HObject rect = null;
HObject reducedImage = null;
HTuple modelId = null;
try {
    HOperatorSet.GenRectangle2(out rect, ...);
    // ... HALCON 연산 ...
    return true;
}
catch (Exception ex) {
    error = ex.Message;
    return false;
}
finally {
    if (rect        != null) { try { rect.Dispose();         } catch { } }
    if (reducedImage != null) { try { reducedImage.Dispose(); } catch { } }
    if (modelId     != null) { try { HOperatorSet.ClearShapeModel(modelId); } catch { } }
}
```

### Logging pattern
**Source:** `EthernetVisionHandler.cs` lines 31-53 + `EthernetAlignCamera.cs` lines 89-91
**Apply to:** `AlignShapeMatchService` 모든 실패/성공 분기
```csharp
Logging.PrintLog((int)ELogType.Error,   "[ALIGN_SVC] 실패 메시지: {0}", ex.Message);
Logging.PrintLog((int)ELogType.Trace,   "[ALIGN_SVC] 진단 수치: cur=({0:F1},{1:F1}) ...", curRow, curCol);
```

### px→mm 변환 (D-05)
**Source:** `Custom/SystemSetting.cs` line 83 (`EthernetPixelResolution = 8.652` μm/px)
**Apply to:** `AlignShapeMatchService.Run()` 내 offset 산출
```csharp
// D-05: EthernetPixelResolution(μm/px) / 1000.0 = mm/px
double resMm = SystemSetting.Handle.EthernetPixelResolution / 1000.0;
double offsetXmm = dCol * resMm;   // Col → X
double offsetYmm = dRow * resMm;   // Row → Y
```

### Newtonsoft.Json serialize/deserialize
**Source:** `CycleResultSerializer.cs` lines 186-187, 218-219
**Apply to:** `AlignShapeMatchService` 사이드카 ref-pose JSON
```csharp
// 저장:
string json = JsonConvert.SerializeObject(obj, Formatting.Indented);
File.WriteAllText(path, json, System.Text.Encoding.UTF8);

// 로드 (RCE 방지 — TypeNameHandling.None 명시):
var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };
var obj = JsonConvert.DeserializeObject<T>(json, settings);
```

### .shm 경로 규약
**Source:** `RecipeFileHelper.GetPatternModelFilePath()` lines 102-114
**Apply to:** `AlignShapeMatchService.GetShmPath()`
```csharp
// 기존: Path.Combine(RecipeSavePath, recipeName, seqName, actName) + name + ".shm"
// 이더넷: Path.Combine(RecipeSavePath, recipeName, "ETHERNET_ALIGN") + modeName + ".shm"
// Directory.CreateDirectory 포함 — 없으면 생성.
```

### C# 7.2 enum switch 분기
**Source:** `EthernetVisionHandler.cs` lines 30-34 (if/else 모드 분기) + CLAUDE.md 제약
**Apply to:** `AlignShapeMatchService` 모드별 분기 전반
```csharp
// switch expression(C# 8+) 금지. if/else 또는 switch statement 사용.
if (mode == EEthernetVisionMode.Bottom) { ... }
else { ... }  // Tray
```

---

## No Analog Found

없음 — 모든 신규 파일이 기존 코드에서 직접 analog 를 가짐.

---

## Metadata

**Analog search scope:** `WPF_Example/Halcon/Algorithms/`, `WPF_Example/Custom/EthernetVision/`, `WPF_Example/Utility/`, `WPF_Example/Custom/Sequence/Inspection/`, `WPF_Example/UI/ViewModel/`, `WPF_Example/Setting/`
**Files scanned:** 11
**Pattern extraction date:** 2026-06-24
