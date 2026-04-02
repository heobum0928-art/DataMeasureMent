# Testing Patterns

**Analysis Date:** 2026-04-02

## Test Framework

**Runner:**
- No C# unit test project detected. There is no `.Tests.csproj`, no NUnit/xUnit/MSTest references, and no test runner configuration.
- The solution file `DatumMeasurement.sln` contains a single project: `WPF_Example/DatumMeasurement.csproj`

**Assertion Library:**
- None (no automated C# test assertions present)

**Run Commands:**
```bash
# No automated C# test commands exist.
# Manual/integration-level testing is done via Python scripts:
python Test/HandlerCommunicationTest.py    # TCP protocol integration test
python Test/mock_vision_server.py          # Simulate vision server for client tests
python Test/mock_vision_client.py          # Simulate vision client for server tests
```

## Test File Organization

**Location:**
- `Test/` directory at project root contains only Python scripts — no C# test files exist anywhere
- No co-located `*.Test.cs` or `*.Spec.cs` files alongside production code

**Naming:**
- Python scripts follow `<description>.py` naming with no enforced convention

**Structure:**
```
Test/
├── HandlerCommunicationTest.py   # Multi-port TCP message sequence test
├── mock_vision_client.py         # Minimal TCP client sending one inspection packet
├── mock_vision_server.py         # Minimal TCP server returning a canned response
└── ImageRotate.py                # Utility script (image manipulation, not a test)
```

## Test Structure

**Python TCP integration tests:**
```python
# mock_vision_client.py pattern:
HOST = "127.0.0.1"
PORT = 7701
MESSAGE = "$TEST:1,2,BJWC73.20@"  # Protocol: $CMD:site,type,target@

def main():
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as client:
        client.connect((HOST, PORT))
        client.sendall(MESSAGE.encode("utf-8"))
        response = client.recv(1024).decode("utf-8", errors="replace")
        # Validate manually by reading printed output
```

```python
# mock_vision_server.py — canned response pattern:
response = "$TEST:1,2,1,OK,0.100,0.200,0.300@"
conn.sendall(response.encode("utf-8"))
```

**HandlerCommunicationTest.py pattern:**
- Connects to one or more ports
- Sends a sequence of messages from `msg_list`
- Reads responses with a 5-second timeout
- Prints send/receive to stdout — validation is manual inspection of console output

## Mocking

**Framework:** None in C# code.

**Patterns:**
- `VirtualCamera` class at `WPF_Example/Device/Camera/VirtualCamera.cs` is the hardware mock for cameras — it loads images from disk instead of a physical camera. This is the primary isolation mechanism used during development/simulation.
- Compile-time simulation mode is enabled via the `SIMUL_MODE` preprocessor constant (defined in `Debug` build config in `DatumMeasurement.csproj`):
  ```csharp
  #if SIMUL_MODE
  Properties.Width = width.I;
  Properties.Height = height.I;
  return normalizedImage;
  #endif
  ```
- `Action_FAIMeasurement.cs` contains explicit stubs for Phase 8 algorithm work:
  ```csharp
  // Phase 8: Halcon edge measurement will be implemented here
  // For now, mark all FAIs as measured with stub values
  fai.SetResult(fai.NominalValue); // stub: nominal = pass
  ```

**What to Mock:**
- Camera hardware: use `VirtualCamera` with a `BackgroundImagePath` pointing to a test image directory
- TCP server/client: use `mock_vision_server.py` / `mock_vision_client.py` in `Test/`
- Algorithm inputs: pass an actual image file path or an `HImage` loaded from disk

**What NOT to Mock:**
- `SystemHandler.Handle` — it is a sealed singleton and cannot be replaced. Code that calls `SystemHandler.Handle.*` directly cannot be unit tested without refactoring.
- `HalconDotNet` operator calls — no mock/stub infrastructure exists for Halcon; testing requires a real Halcon runtime license.

## Fixtures and Factories

**Test Data:**
- No programmatic fixtures or factory classes exist
- Test images are loaded from `Image/` and `Cal_Image/` directories at project root
- `VirtualCamera.BackgroundImagePath` is set at runtime to a folder; the camera cycles through all image files found in that folder

**Teaching data:**
- `HalconTeachingHelper.CreateDefaultJob(jobName, fallbackRect)` creates a minimal `TeachingJob` with one ROI from a WPF `Rect` — this serves as a factory for default teaching state in integration scenarios

**Recipe data:**
- `InspectionRecipeManager.AddShot()` and `ShotConfig.AddFAI()` build recipe objects programmatically — usable in manual integration tests

## Coverage

**Requirements:** None enforced. No coverage tooling configured.

**View Coverage:**
```bash
# Not available — no test runner configured.
```

## Test Types

**Unit Tests:**
- Not present. No xUnit/NUnit/MSTest projects exist.

**Integration Tests:**
- Performed manually by running the WPF application with `SIMUL_MODE` build and feeding test images through `VirtualCamera`
- TCP protocol tested via Python scripts in `Test/`

**E2E Tests:**
- Not formally defined. The equivalent is launching the full application, connecting a Python TCP client, and observing inspection results on-screen.

## Common Patterns

**Algorithm verification (manual):**
```csharp
// Pattern used in MeasurementAlgorithm — testable without WPF by calling:
var alg = new MeasurementAlgorithm();
string result = alg.Run("path/to/test.png", roiList);
// Inspect result string manually:
// "Image: path/to/test.png | ROI: 2/2 taught | ROI 1: OK pts=18 | ROI 2: OK pts=22"
```

**Try-pattern result checking:**
```csharp
// RoiLineIntersectionAlgorithm — call pattern for integration verification:
var alg = new RoiLineIntersectionAlgorithm();
RoiLineInspectionResult result;
bool ok = alg.TryRun("path/to/test.png", teachingJob.Rois, out result);
// Check result.HasIntersection, result.IntersectionRow, result.IntersectionColumn
```

**TCP protocol verification:**
```python
# Send one packet and check response format
# Request:  $TEST:1,2,BJWC73.20@
# Response: $TEST:1,2,1,OK,0.100,0.200,0.300@
#            ^CMD ^site,type,testid,status,offsetX,offsetY,angle
```

## Gaps and Recommendations

**No automated C# tests exist.** This is the primary gap.

**Testable without full WPF initialization:**
- `WPF_Example/Halcon/Algorithms/MeasurementAlgorithm.cs` — pure algorithm, no WPF/device dependency
- `WPF_Example/Halcon/Algorithms/RoiLineIntersectionAlgorithm.cs` — pure algorithm
- `WPF_Example/Halcon/Services/TeachingStorageService.cs` — file I/O, easily testable
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` — INI load/save, no device dependency
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` — `SetResult()` logic, pure computation

**Not testable without refactoring:**
- Any class calling `SystemHandler.Handle.*` directly — would require extracting interfaces or dependency injection
- Any class calling `HalconDotNet` operators — requires a Halcon license at test time

---

*Testing analysis: 2026-04-02*
