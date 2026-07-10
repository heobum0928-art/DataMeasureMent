---
phase: quick-260710-f4k
plan: 01
subsystem: infra, tcpserver, halcon-algorithms, ui
tags: [dead-code, cleanup, csproj, tcpserver, halcon, wpf, camera-driver]

requires: []
provides:
  - "csproj 미등록 죽은 소스 6개 파일 삭제 (레거시 Alligator/AlligatorAlgMil MIL 래퍼 + 미사용 Calibration 액션 2종)"
  - "참조 0 재검증 완료된 죽은 메서드/필드 제거 (TCP 패킷 파서/버퍼, Halcon 알고리즘, UI 서비스, 카메라 드라이버)"
  - "주석처리 코드 블록 + 미사용 using 정리"
affects: [tcp-server, halcon-algorithms, camera-drivers, ui-viewmodels]

tech-stack:
  added: []
  patterns: []

key-files:
  created: []
  modified:
    - WPF_Example/TcpServer/VisionResponsePacket.cs
    - WPF_Example/TcpServer/TcpServer.cs
    - WPF_Example/TcpServer/VisionRequestPacket.cs
    - WPF_Example/TcpServer/VisionServer.cs
    - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
    - WPF_Example/Halcon/Display/HalconDisplayService.cs
    - WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
    - WPF_Example/UI/ControlItem/NodeViewModel.cs
    - WPF_Example/UI/ViewModel/ModelFinderViewModel.cs
    - WPF_Example/UI/ViewModel/CalibrationViewModel.cs
    - WPF_Example/Device/Camera/Basler/BaslerCamera.cs
    - WPF_Example/Device/Camera/Hik/HikCamera.cs
    - WPF_Example/Utility/Ini.cs
  deleted:
    - WPF_Example/Custom/Sequence/Bottom/Action_BottomCalibration.cs
    - WPF_Example/Custom/Sequence/Top/Action_TopCalibration.cs
    - WPF_Example/ExternalLib/VisionLib/Alligator/Alligator.cs
    - WPF_Example/ExternalLib/VisionLib/Alligator/AlligatorDef.cs
    - WPF_Example/ExternalLib/VisionLib/AlligatorAlgMil/AlligatorAlgMil.cs
    - WPF_Example/ExternalLib/VisionLib/AlligatorAlgMil/AlligatorAlgMilDef.cs

key-decisions:
  - "HikCamera.cs: FrameDurationTicks 필드 삭제로 orphan 된 frametime 지역변수/RENDERFPS 필드도 동반 삭제 (plan 텍스트는 Basler 만 명시했으나 동일 패턴이라 일관 적용, Rule 1 성격의 직접 결과 정리)"
  - "HikCamera.cs: CopyMemory DllImport 삭제로 미사용된 System.Runtime.InteropServices using 도 함께 제거 (Task 2 삭제의 직접 부작용, Task 3 목록 파일과 무관하게 처리)"
  - "CalibrationViewModel.cs 의 using ReringProject.Utility; 는 IniFile 미사용이나 계획에 명시되지 않은 using 이라 미제거 (범위 밖 보존)"

patterns-established: []

requirements-completed: [DEADCODE-SWEEP-01]

duration: ~35min
completed: 2026-07-10
---

# Quick 260710-f4k: 죽은 코드 스윕 Summary

**csproj 미등록 소스 6개(1,750줄) 삭제 + 참조 0 재검증된 메서드/필드 제거(428줄) + 주석블록/미사용 using 정리(132줄) — 총 2,310줄 삭제, 3개 atomic commit, 매 커밋 전 Debug/x64 빌드 PASS.**

## Performance

- **Duration:** ~35 min
- **Tasks:** 3/3 완료
- **Files modified:** 17 (수정) + 6 (삭제) = 23
- **Commits:** 3 (0761375, 9c21298, 5d6c0cc)

## Accomplishments

### Task 1 — csproj 미등록 죽은 파일 6개 삭제 (commit 0761375)

삭제 전 재검증 게이트 3개 실행 (모두 통과):

```
$ grep -rn "Alligator" --include=*.cs WPF_Example/ | grep -v ExternalLib
WPF_Example/Utility/RecipeFileHelper.cs:187:            FileVersionInfo dllInfo = FileVersionInfo.GetVersionInfo(AppDomain.CurrentDomain.BaseDirectory + "\\AlligatorAlgMil.dll");
→ 1건, RecipeFileHelper.cs 는 bin DLL FileVersionInfo 조회이며 소스 무관. 예상대로 1건만 나옴.

$ grep -rn "TopCalibrationAction\|BottomCalibrationAction\|TopCalibrationParam\|BottomCalibrationParam" --include=*.cs --include=*.xaml WPF_Example/ | grep -v "Action_TopCalibration.cs\|Action_BottomCalibration.cs"
→ 0건

$ grep -n "Calibration.cs\|Alligator" WPF_Example/DatumMeasurement.csproj
→ 0건 (csproj 미등록 확인)
```

삭제 파일 (1,750줄):
- `Action_BottomCalibration.cs` (345줄), `Action_TopCalibration.cs` (279줄)
- `ExternalLib/VisionLib/Alligator/Alligator.cs` (263줄), `AlligatorDef.cs` (40줄)
- `ExternalLib/VisionLib/AlligatorAlgMil/AlligatorAlgMil.cs` (776줄), `AlligatorAlgMilDef.cs` (47줄)

빌드: PASS (`DatumMeasurement -> ...\DatumMeasurement.exe`, 신규 CS 오류 0)
삭제 파일 개수: `git diff --diff-filter=D --name-only HEAD~1..HEAD` → 정확히 6개 확인.

### Task 2 — 참조 0 죽은 메서드/필드 제거 (commit 9c21298, 428줄)

각 항목 삭제 전 grep 참조 0 확인:

- **VisionResponsePacket.cs**: `Convert(string msg)` 오버로드(180줄, 미호출 — 실제 호출측은 `VisionRequestPacket.Convert(msg)` 뿐임을 project-wide grep 으로 확인) + 전용 헬퍼 `SetOnFromString`/`SetSiteStatusFromString` 삭제.
  - 생존 확인: `grep -n "Convert(VisionResponsePacket" ...` → `public static string Convert(VisionResponsePacket packet)` 존재, `ToString()` 이 이를 호출하는 구조 무변경.
- **TcpServer.cs**: `mSendBuffer`(선언만, 실사용 0 — 239줄은 주석에서만 언급), `SIZE_SEND_BUFFER`/`SIZE_RECV_BUFFER` const(mSendBuffer 삭제 후 완전 미참조), `mReceiveTimer`(Stopwatch, 선언만) 삭제.
- **VisionAlgorithmService.cs**: `RunPhiSmokeTest`, `TryFitArc`(XML doc 포함), `TryIntersectCircleLine`, `TryIntersectContours` 삭제(project-wide grep 참조 0 확인) + `TryFindCircle` 내부 항상 null 인 `circleBorder` 변수/Dispose 호출 제거.
  - 생존 확인: `grep -n "bool TryFindCircle"` → 존재. 호출측 `grep -rln "TryFindCircle("` → `CircleCenterDistanceMeasurement.cs`, `CircleDiameterMeasurement.cs` 확인(계획서 기재 콜러와 일치).
- **DatumFindingService.cs**: `TryTeachTwoLineIntersect` 내 미사용/미Dispose 지역변수 `horect` 2줄만 삭제. 나머지 로직 무변경.
- **AlignShapeMatchService.cs**: `HasTemplate` 로 위임하는 얇은 래퍼 `TryLoadTemplate` 삭제 (XAML 참조 0 확인).
- **MainView.xaml.cs**: `InvokeTryTeachDatumForEdit`(26줄, 미호출), `ChangeTabPage`(no-op 3줄) 삭제.
  - 생존 확인: `NotifyDatumParamMaybeChanged` → `InspectionListView.xaml.cs:125` 라이브 호출자(`mv.NotifyDatumParamMaybeChanged(datum);`) 확인, 삭제하지 않음.
- **BaslerCamera.cs**: `FrameDurationTicks` 필드 + 이 필드로만 흘러가는 `RENDERFPS` + ctor 계산 삭제.
- **HikCamera.cs**: `FrameDurationTicks`/`RENDERFPS`/ctor 계산(Basler 와 동일 패턴이라 일관 적용), `ID` 필드(대입만 있고 읽기 0, 클래스 범위 확인 — 상속 `VirtualCamera` 에 `ID` 멤버 없음 확인), `CopyMemory` DllImport(호출 0), 빈 `Dispose` if 블록(`if(CameraHandle != null) { }`) 삭제. `ID` 삭제로 파라미터 `id` 는 `Open(CCameraInfo info, int id)` 내 미사용이 되었으나 private 메서드 시그니처 변경은 구조 변경(Rule 4 영역)이라 보존.

빌드: PASS (`DatumMeasurement -> ...\DatumMeasurement.exe`, 신규 CS 오류 0, 기존 warning 만 잔존)
삭제 파일 개수: `git diff --diff-filter=D --name-only HEAD~1..HEAD` → 0개 (예상대로 파일 삭제 없음, 코드 라인만 삭제).

### Task 3 — 주석처리 코드 블록 + 미사용 using 정리 (commit 5d6c0cc, 132줄)

주석 블록(연속 3줄 이상, 실제 코드 주석처리분만):
- `VisionRequestPacket.cs`: zone 파싱 주석 4곳(RecipeChange/RecipeGet/SiteStatus/Light 케이스) 삭제.
- `BaslerCamera.cs`: Balance White Auto 주석 블록 삭제.
- `HikCamera.cs`: GigE ip fallback 주석 블록(8줄) + 구 `MV_CC_*_NET` API 주석 3줄(연속) 삭제. (단, 328줄의 단일 주석 `//CameraHandle.MV_CC_SetPixelFormat_NET(...)` 은 3줄 미만이라 스킵)
- `Ini.cs`: `#if JS` 블록(17줄) 삭제 — `JS` 심볼이 프로젝트 어디에도 `#define`/`DefineConstants` 로 정의되지 않아 도달 불가 코드임을 grep 으로 확인.
- `NodeViewModel.cs`: `IDragSource`/`IDropTarget` 구현 통째 주석(40줄) + 클래스 선언부 `//, IDragSource, IDropTarget` + `SortNodeChildren` 호출 주석(3줄) 삭제. 파일명은 계획서에 `Custom/UI/ViewModel/NodeViewModel.cs` 로 기재됐으나 실제 경로는 `UI/ControlItem/NodeViewModel.cs` (프로젝트 내 동일명 파일 1개뿐, 오타로 판단하고 진행).

빈 no-op 블록:
- `InspectionListView.xaml.cs`: `AddDatumToSequence` 내 조건 평가 후 본문이 주석 한 줄뿐인 빈 `if` 블록 삭제. `InspectionList_SelectionChanged` 구조는 무변경(계획서 경고 준수).

미사용 using (grep 으로 실사용 0 확인 후 제거, 사용 흔적 있으면 보존):
- `HalconDisplayService.cs`: `System.Linq` 제거. 742/743 의 `.Min` 은 `Math.Min` 이며 LINQ 확장 메서드가 아님을 확인(`\.Select(\|\.Where(\|...` 패턴 grep 0건).
- `MainResultViewerControl.xaml.cs`: `System.Runtime.InteropServices` 제거 (Marshal/DllImport/StructLayout 등 grep 0건). `System.Linq` 는 실사용 있어 보존.
- `InspectionListView.xaml.cs`: `System.Threading.Tasks` 제거 (Task/async/await grep 0건).
- `ModelFinderViewModel.cs`, `CalibrationViewModel.cs`: `System.Collections.Generic`/`System.Linq`/`System.Text`/`System.Threading.Tasks` 각 4개 제거 (두 파일 모두 List/Dictionary/Linq확장/StringBuilder/Task 사용 0건 확인, 파일 전체 짧아 직접 육안 재확인도 병행).

부수 정리(Task 2 의 직접 결과, 별도 using 목록 지시엔 없었으나 계획 밖 새 미사용 심볼을 방치하지 않기 위해 처리):
- `HikCamera.cs`: `CopyMemory` DllImport 삭제로 orphan 된 `System.Runtime.InteropServices` using 제거.
- `HikCamera.cs`: `FrameDurationTicks` 삭제로 orphan 된 `frametime` 지역변수 + `RENDERFPS` 필드 동반 삭제(Basler 와 동일 패턴, 일관 적용).

빌드: PASS (`DatumMeasurement -> ...\DatumMeasurement.exe`, 신규 CS 오류 0)
삭제 파일 개수: `git diff --diff-filter=D --name-only HEAD~1..HEAD` → 0개.

## 범위 밖 발견 사항 (수정하지 않고 보고만)

1. **Device/DeviceHandler.cs:83,89** — `if (IDList.Select(id => id.CamType == ECameraType.Basler) != null)`. LINQ `Select` 는 null 을 반환하지 않으므로 이 조건은 항상 참. `.Any(...)` 의도로 보임. 미수정.
   ```
   83:            if (IDList.Select(id => id.CamType == ECameraType.Basler) != null) {
   89:            if(IDList.Select(id => id.CamType == ECameraType.HIK) != null) {
   ```
2. **Device/Camera/Basler/BaslerCamera.cs:648** — `Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} StopStream. ({2})", Name, e.Message);` 포맷 문자열에 `{2}` 참조가 있으나 인자는 2개(`Name`, `e.Message`)뿐이라 catch 블록 안에서 `FormatException` 발생 가능. 미수정.
3. **Utility/Logging.cs:366** — `Debug.Assert(true, e.Message);` 조건이 상수 `true` 이므로 절대 미발화. `false` 의도로 추정. 미수정.

## 재검증/생존 확인 grep (증거)

```
$ grep -n "Convert(VisionResponsePacket" WPF_Example/TcpServer/VisionResponsePacket.cs
100:        public static string Convert(VisionResponsePacket packet) {

$ grep -n "bool TryFindCircle" WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
256:        public bool TryFindCircle(

$ grep -n "NotifyDatumParamMaybeChanged" WPF_Example/UI/ContentItem/MainView.xaml.cs WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
WPF_Example/UI/ContentItem/MainView.xaml.cs:1213:        public void NotifyDatumParamMaybeChanged(DatumConfig datum) {
WPF_Example/UI/ControlItem/InspectionListView.xaml.cs:125:                mv.NotifyDatumParamMaybeChanged(datum);

$ grep -rn "RunPhiSmokeTest|TryFitArc|TryIntersectCircleLine|TryIntersectContours|SetOnFromString|SetSiteStatusFromString|TryLoadTemplate|InvokeTryTeachDatumForEdit|ChangeTabPage\(" --include=*.cs --include=*.xaml WPF_Example/
(결과 없음 — 모두 참조 0)
```

## 삭제 총 줄 수 (git diff --stat 근거)

```
$ git diff --shortstat HEAD~3 HEAD
23 files changed, 1 insertion(+), 2310 deletions(-)
```

- Task 1: 6 files changed, 1750 deletions(-)
- Task 2: 8 files changed, 428 deletions(-)
- Task 3: 11 files changed, 1 insertion(+), 132 deletions(-)

## Deviations from Plan

### Auto-fixed / 계획 밖 소폭 조정 (Rule 1 성격 — 직접 부작용 정리)

**1. HikCamera.cs 의 orphan 코드 정리**
- **Found during:** Task 2 (HikCamera.cs FrameDurationTicks/CopyMemory 삭제)
- **Issue:** `FrameDurationTicks` 필드 삭제 시 이를 계산하던 `frametime` 지역변수와 `RENDERFPS` 필드가 orphan(미사용)이 됨. `CopyMemory` DllImport 삭제 시 `System.Runtime.InteropServices` using 이 orphan 이 됨.
- **Fix:** frametime/RENDERFPS/using 을 함께 삭제 (Basler 와 동일 패턴이며, 남겨두면 즉시 재차 죽은 코드가 생성되는 상황이라 순수 삭제 범위로 판단).
- **Files modified:** WPF_Example/Device/Camera/Hik/HikCamera.cs
- **Commit:** 9c21298 (필드/using 정리는 동일 커밋 내 포함)

**2. NodeViewModel.cs 경로 정정**
- **Found during:** Task 3 착수
- **Issue:** 계획서 파일 목록에 `WPF_Example/Custom/UI/ViewModel/NodeViewModel.cs` 로 기재되어 있으나 실제 파일은 `WPF_Example/UI/ControlItem/NodeViewModel.cs` 1개뿐 (프로젝트 전체에 동일 파일명 중복 없음).
- **Fix:** 실제 경로의 파일에 계획서 지시대로 작업 진행.
- **Files modified:** WPF_Example/UI/ControlItem/NodeViewModel.cs

### 스킵 항목

없음 — 계획서에 기재된 모든 삭제 대상이 재검증 게이트를 통과하여 예정대로 삭제됨.

### 보존한 미사용 using (확신 부족)

- `CalibrationViewModel.cs` 의 `using ReringProject.Utility;` — `IniFile` 미사용으로 실질적으로도 orphan 이나, Task 3 지시 목록(Collections.Generic/Linq/Text/Threading.Tasks)에 없어 범위 밖으로 판단해 보존.

## Known Stubs

없음 — 이번 작업은 순수 삭제만 수행, 신규 스텁/미배선 데이터 없음.

## Threat Flags

없음 — 신규 네트워크 엔드포인트/인증 경로/파일 접근/스키마 변경 없음. 순수 죽은 코드 제거.

## Self-Check

- [x] Task 1: 6개 파일 삭제 확인 (`git diff --diff-filter=D --name-only HEAD~1` at commit 0761375)
- [x] Task 2/3: 파일 삭제 0건 확인
- [x] 3개 commit 모두 존재 (`git log --oneline`: 0761375, 9c21298, 5d6c0cc)
- [x] 최종 Debug/x64 빌드 PASS, 신규 CS 오류 0
- [x] 생존 대상 심볼(Convert(packet)/TryFindCircle/NotifyDatumParamMaybeChanged) 모두 확인됨
- [x] 버그 3건 미수정 확인(라인 존재 그대로)

## Self-Check: PASSED
