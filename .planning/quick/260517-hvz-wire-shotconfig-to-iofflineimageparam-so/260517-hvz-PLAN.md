---
phase: quick-260517-hvz
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
autonomous: true
requirements: [QUICK-260517-HVZ]

must_haves:
  truths:
    - "SHOT 노드를 선택하고 Load 버튼으로 이미지를 고르면 SimulImagePath 가 채워진다"
    - "ShotConfig 인스턴스를 IOfflineImageParam 으로 캐스팅할 수 있다"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs"
      provides: "IOfflineImageParam 구현 (GetLatestImagePath / SetLatestImagePath)"
      contains: "IOfflineImageParam"
  key_links:
    - from: "WPF_Example/UI/ContentItem/MainView.xaml.cs"
      to: "ShotConfig.SetLatestImagePath"
      via: "LoadAndDisplay 의 is IOfflineImageParam 분기 (L309)"
      pattern: "SetLatestImagePath"
---

<objective>
ShotConfig 를 IOfflineImageParam 인터페이스에 연결하여, SHOT 노드 선택 후 Load 버튼이 선택한 이미지 경로를 SimulImagePath 에 자동 저장하도록 배선한다.

Purpose: 현재 MainView.LoadAndDisplay (MainView.xaml.cs:309-311) 는 `param is IOfflineImageParam` 분기로 IOfflineImageParam 구현 param 에 한해서만 Load 이미지 경로를 저장한다. ShotConfig 는 이 인터페이스를 미구현하여 SHOT 노드 선택 후 Load 해도 SimulImagePath 가 비어 있고, 그 결과 Action_FAIMeasurement EStep.Grab 이 이미지 로드에 실패 → EStep.Measure 의 ShotParam.GetImage() 가 null → 측정 루프 스킵 → 측정값 컬럼이 '—' 로 표시된다.

Output: ShotConfig.cs 단일 파일 수정 — 클래스 선언에 IOfflineImageParam 추가 + 멤버 2개 구현.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@./CLAUDE.md

<interfaces>
<!-- 실행자가 필요한 계약. 코드베이스에서 추출함 — 추가 탐색 불필요. -->

IOfflineImageParam 정의 (WPF_Example/Sequence/Param/CameraParam.cs:31-35, namespace ReringProject.Sequence):
```csharp
public interface IOfflineImageParam {
    string GetLatestImagePath();
    void SetLatestImagePath(string imagePath);
}
```

ShotConfig 현재 선언 (WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs:9, namespace ReringProject.Sequence):
```csharp
public class ShotConfig : CameraSlaveParam {
```

SimulImagePath — 이미 존재하는 public 프로퍼티 (ShotConfig.cs:15-16, 신규 필드 불필요):
```csharp
[Category("Shot|Simulation")]
public string SimulImagePath { get; set; } = "";
```

소비 지점 (WPF_Example/UI/ContentItem/MainView.xaml.cs:309-311):
```csharp
if (param is IOfflineImageParam offlineImageParam) {
    offlineImageParam.SetLatestImagePath(dialog.FileName);
}
```

참고 구현 예시 — TopInspectionParam (WPF_Example/Custom/Sequence/Top/Action_TopInspection.cs:38, 121-128):
TopInspectionParam 은 `_latestImagePath` 필드 + TeachingJob 동기화가 있어 구현이 더 복잡하다.
ShotConfig 는 SimulImagePath 프로퍼티에 직접 위임하면 되므로 더 단순하다 — TopInspectionParam 의 구조를 그대로 복사하지 말 것.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: ShotConfig 를 IOfflineImageParam 에 연결</name>
  <files>WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs</files>
  <action>
ShotConfig 가 IOfflineImageParam 을 구현하도록 다음 2가지를 수정한다. ShotConfig 와 IOfflineImageParam 은 동일 namespace(ReringProject.Sequence)이므로 using 추가는 불필요하다.

1. 클래스 선언 변경 (L9):
   `public class ShotConfig : CameraSlaveParam {`
   → `public class ShotConfig : CameraSlaveParam, IOfflineImageParam { //260517 hbk`
   클래스 선언 라인에 `//260517 hbk` 주석 마커를 붙인다.

2. IOfflineImageParam 멤버 2개를 구현한다. SimulImagePath 프로퍼티에 직접 위임하는 단순 구현 — TopInspectionParam 처럼 별도 `_latestImagePath` 필드나 TeachingJob 동기화를 추가하지 말 것. ShotConfig 의 기존 brace 스타일(Allman — 여는 중괄호를 다음 줄에)을 따른다. 적절한 위치는 SimulImagePath 프로퍼티 인근 또는 생성자 위. 두 메서드 위/끝에 `//260517 hbk` 주석 마커를 둔다:

```csharp
//260517 hbk IOfflineImageParam — MainView Load 버튼이 SHOT 노드 선택 시 경로 저장
public string GetLatestImagePath()
{
    return SimulImagePath;
}

public void SetLatestImagePath(string imagePath)
{
    SimulImagePath = imagePath;
}
```

기존 hbk 주석 마커(예: L24 `//260413 hbk Phase 6`, L41, L47 `//260510 hbk Phase 21`)는 모두 보존한다. .NET Framework 4.8 / C# 7.2 제약 — C# 8.0+ 문법(switch expression, nullable reference type, expression-bodied member 강제 등) 금지. 위 구현은 일반 메서드 본문이므로 C# 7.2 호환.
  </action>
  <verify>
    <automated>msbuild WPF_Example\DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Rebuild /v:minimal /nologo</automated>
  </verify>
  <done>
- msbuild Debug/x64 Rebuild: 0 errors, 신규 warning 0 (Phase 21 baseline = 6 warnings 유지, 추가 0).
- ShotConfig 클래스 선언이 `: CameraSlaveParam, IOfflineImageParam` 형태이며 GetLatestImagePath/SetLatestImagePath 가 public 으로 구현됨.
- 코드 검증: ShotConfig 가 IOfflineImageParam 으로 캐스팅 가능 → MainView.LoadAndDisplay L309 `param is IOfflineImageParam` 분기를 SHOT 노드 param 이 통과한다.
- 신규 필드 추가 없음 (SimulImagePath 재사용). 기존 hbk 마커 전부 보존, 수정/추가 라인에 `//260517 hbk` 마커 존재.
  </done>
</task>

</tasks>

<verification>
- 빌드: msbuild Debug/x64 Rebuild — 0 errors, 신규 warning 0.
- 인터페이스 계약: ShotConfig 가 IOfflineImageParam 의 멤버 2개(GetLatestImagePath, SetLatestImagePath)를 모두 public 으로 구현.
- 배선: MainView.xaml.cs:309 의 `is IOfflineImageParam` 분기가 SHOT 노드 선택 시 ShotConfig 에 대해 true 가 되어 SetLatestImagePath 가 호출됨.
</verification>

<success_criteria>
- ShotConfig.cs 단일 파일 수정으로 IOfflineImageParam 구현 완료.
- SHOT 노드 선택 후 Load 버튼 클릭 시 선택한 이미지 경로가 SimulImagePath 에 저장되는 경로가 코드상 성립.
- Debug/x64 빌드 통과, 신규 warning 0.
- C# 7.2 / .NET Framework 4.8 제약 준수, 기존 hbk 마커 보존 + 신규 `//260517 hbk` 마커 부착.
</success_criteria>

<output>
After completion, create `.planning/quick/260517-hvz-wire-shotconfig-to-iofflineimageparam-so/260517-hvz-SUMMARY.md`
</output>
</content>
</invoke>
