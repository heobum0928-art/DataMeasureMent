---
phase: quick-260813-fdt
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
autonomous: false
requirements: [QUICK-260813-FDT-01, QUICK-260813-FDT-02]

must_haves:
  truths:
    - "검사 탭에서 Datum 노드를 선택하면 PropertyGrid 에 Mirror 그룹의 MirrorX / MirrorY 체크박스가 보인다 (4개 AlgorithmType 전부)"
    - "MirrorX 또는 MirrorY 를 실제로 다른 값으로 바꾸면 경고 메시지박스가 1회 뜬다"
    - "경고 문구에 (1) 카메라 촬영 방향이 바뀐다 (2) 다른 측정까지 틀어질 수 있다 (3) 재시작해야 적용된다 — 3가지가 쉬운 한국어로 들어있다"
    - "같은 값으로 다시 저장하면 경고가 뜨지 않는다"
    - "레시피(INI) 로드 시 MirrorX=True 가 저장돼 있어도 경고가 뜨지 않는다"
    - "Datum 노드 복사/붙여넣기 시 경고가 뜨지 않고 값은 정상 복사된다"
    - "값이 INI 에 저장되고 재시작 후 다시 로드된다 (ParamBase reflection 경로)"
    - "Datum 검출/판정 로직과 다른 파일은 전혀 바뀌지 않는다"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs"
      provides: "MirrorX / MirrorY 설정 프로퍼티 + 변경 경고 + 리플렉션 경로 억제 가드"
      contains: "public bool MirrorX"
  key_links:
    - from: "DatumConfig.MirrorX setter"
      to: "CustomMessageBox.Show"
      via: "WarnMirrorChanged 헬퍼 (호출부는 정확히 1곳 — 주석에는 이 메서드 이름을 쓰지 않는다)"
      pattern: "CustomMessageBox\\.Show"
    - from: "DatumConfig.Load override"
      to: "_suppressMirrorWarning"
      via: "base.Load 리플렉션 SetValue 구간 억제"
      pattern: "_suppressMirrorWarning = true"
    - from: "DatumConfig.CopyTo"
      to: "target._suppressMirrorWarning"
      via: "CopyPublicPropertiesTo 붙여넣기 구간 억제"
      pattern: "target\\._suppressMirrorWarning"
---

<objective>
Side Datum 이 지그 회전 때문에 상하/좌우가 뒤집혀 촬영되는 문제에 대비해, `DatumConfig` 에 **MirrorX / MirrorY 설정값만** 추가하고, 사용자가 이 값을 바꿀 때 위험을 알리는 경고 메시지박스를 띄운다.

Purpose: 지금은 "설정 표면(surface)" 만 만든다. 실제 이미지 뒤집기(MIL grab 방향 배선)는 **후속 별도 quick 작업**이며 이번 범위가 아니다.
Output: `DatumConfig.cs` 단일 파일 수정 — public bool 2개 + 경고 1개 + 리플렉션 억제 가드.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md
@WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs

<verified_facts>
<!-- 계획 단계에서 실제 파일을 읽어 확인한 사실. 실행자는 탐색 없이 그대로 사용할 것. -->

**대상 파일 구조 (DatumConfig.cs, 총 1218줄)**
- `public class DatumConfig : ParamBase, System.ComponentModel.ICustomTypeDescriptor, IOfflineImageParam`
- `[Category("Datum|...")]` 프로퍼티는 PropertyGrid 에 자동 노출됨. **XAML/UI 코드 추가 불필요.**
- `using` 목록: `System.Collections.Generic`, `HalconDotNet`, `PropertyTools.DataAnnotations`, `ReringProject.Utility` 뿐이다.
  → `[Category(...)]` / `[DisplayName(...)]` 는 **PropertyTools** 어트리뷰트(bare 사용).
  → `System.ComponentModel.Description` / `System.IO.File` / `ReringProject.Setting.ELogType` 처럼 **완전수식(fully-qualified)이 이 파일의 관례**다. **새 `using` 을 추가하지 말 것.**

**PropertyGrid 노출 필터 (숨김 사고 방지 — 확인 완료)**
- `IsHiddenForAlgorithm(name, alg)` (1113~1148줄)이 4개 알고리즘 분기별로 이름 접두사로 숨긴다.
- 4개 분기 전부 확인함: `MirrorX` / `MirrorY` 는 어떤 숨김 조건(`Line1_`/`Line2_`/`Circle_`/`Vertical_`/`Horizontal_A_`/`Horizontal_B_`/`TeachingImagePath_Vertical`/`ZIndexA`/`ZIndexB`/`ExpectedAngleDeg`/`AngleTolerance`/`TwoLineAngleToleranceDeg`)에도 걸리지 않고 `return false` 로 빠진다 → **4개 알고리즘 전부에서 정상 노출된다. `IsHiddenForAlgorithm` 을 수정할 필요 없음.**
- `BuildFilteredProperties` 의 `sourceNames` 화이트리스트는 `ItemsSourceProperty` 드롭다운 전용이다. bool 체크박스는 등록 불필요.

**INI 영속화 (확인 완료 — Load 오버라이드 폴백 불필요)**
- `ParamBase.Save`/`Load` 는 `GetType().GetProperties()` 리플렉션 + 타입 switch. `case "Boolean"` 이 존재(396~399줄)하므로 **public bool 은 자동 저장/로드된다.**
- INI 키가 없으면 `ToBool()` 이 `false` 를 넣는데, 이는 C# 초기값 `false` 와 **동일**하다 → `ZIndexA/ZIndexB` 같은 sentinel 복원 로직이 **필요 없다.**

**⚠ 핵심 함정 — 리플렉션이 세터를 때린다 (이번 작업의 진짜 난점)**
경고를 세터에 그냥 넣으면 다음 두 경로에서 **사용자가 아무것도 안 했는데 경고창이 뜬다**:
1. `ParamBase.Load` (363~430줄) — `prop.SetValue(this, bValue)` 로 세터 호출. **레시피 로드 때마다** 경고 발생.
2. `ParamBase.CopyPublicPropertiesTo` (443~477줄) — `prop.SetValue(target, ...)`. **Datum 붙여넣기 때마다** 경고 발생.

이 파일에는 이미 **동일한 문제를 푼 검증된 패턴**이 있다: `_suppressModelRename` (19~23줄 선언, `DatumName` 세터에서 검사, `Load` 오버라이드 1157~1177줄에서 try/finally 로 억제). **이 패턴을 그대로 복제한다.**

`CopyTo` (1209~1215줄)는 `CopyPublicPropertiesTo(target, _copyExclude)` 로 **target 인스턴스**의 세터를 때린다 → 억제 플래그는 `target._suppressMirrorWarning` 으로 켜야 한다(같은 클래스라 private 접근 가능).

**JSON 경로는 무시해도 된다 (확인 완료)**: `SequenceHandler.LoadRecipe` 의 기본값은 `ERecipeFileType.Ini` 이고, 유일한 실호출부인 `SystemHandler.cs:252` 가 `ERecipeFileType.Ini` 를 하드코딩한다. `LoadFromJson` 는 런타임 도달 불가.

**CustomMessageBox 실제 시그니처 (WPF_Example/UI/Dialog/CustomMessageBox.cs)**
```csharp
namespace ReringProject.UI {
    public static class CustomMessageBox {
        public static bool Show(string title, string message,
            MessageBoxImage imageType = MessageBoxImage.Information,
            bool isModal = true, bool isAutoClosing = true,
            int autoClosingTime = MessageBoxModel.TIME_AUTOCLOSING);   // TIME_AUTOCLOSING = 7 (초)
    }
}
```
- 내부에서 `App.Current.Dispatcher.BeginInvoke` 로 넘기므로 **호출 스레드 무관하게 안전**하고, 세터를 블로킹하지 않는다(재진입 없음). 이중 마샬링 금지.
- **`isAutoClosing` 기본값이 true(7초 자동닫힘)** 다. 읽어야 하는 경고이므로 반드시 `false` 로 끈다. 선례: `WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs:118` ("isAutoClosing=false : 기본 7초 자동닫힘을 끈다. 알람은 사용자가 직접 닫아야 한다").
- `using System.Windows` 가 없으므로 이미지 인자는 `System.Windows.MessageBoxImage.Warning` 로 완전수식.
- 클래스는 `ReringProject.UI` 네임스페이스 → `ReringProject.UI.CustomMessageBox.Show(...)` 로 완전수식(새 using 금지).

**이름 충돌 없음**: 리포 전체 `.cs` 에 `MirrorX` / `MirrorY` 심볼 0건.
**호출 카운트 기준선**: `DatumConfig.cs` 현재 `CustomMessageBox` 등장 0건 → 이번 작업 후 **정확히 1건(실제 호출부)** 이 되어야 한다. Task 1 의 `MSG=1` 게이트가 이것을 지킨다.

**브레이스 스타일**: 이 파일은 혼재돼 있다. 이번에 만드는 "백킹필드 + 가드 있는 세터" 의 최근접 선례는 `DatumName`(30~53줄)과 `RingLight_Brightness_1`(616~623줄)이며 **둘 다 K&R(여는 중괄호 같은 줄)** 이다. → **K&R 로 통일한다.**

**MSBuild 실경로 (확인 완료)**: `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe` (PATH 에 없음 — 전체 경로 사용)

**작업 시작 시점 git 상태 (사전 존재 변경 — 건드리지 말 것)**:
```
 M WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs
?? .planning/quick/260813-fdt-side-datum-x-y/
```
→ `PickerCenterCalibrationService.cs` 는 사용자의 진행 중 실험이다. **그대로 둔다.**
→ 둘째 줄은 이 plan 자신의 미추적 디렉터리다. git 은 미추적 디렉터리를 **안의 파일 개수와 무관하게 1줄**로 센다.
→ 그래서 파일 개수 검증은 반드시 **`git status --porcelain -- WPF_Example`** 처럼 소스 트리로 범위를 좁힌다(범위를 안 좁히면 3줄이 나와 게이트가 오작동한다).
→ 완료 판정은 "DatumConfig.cs 가 **추가로** modified 로 나타난다" 이다.
</verified_facts>
</context>

<tasks>

<task type="auto">
  <name>Task 1: DatumConfig 에 MirrorX/MirrorY 프로퍼티 + 변경 경고 + 리플렉션 억제 가드 추가</name>
  <files>WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs</files>
  <action>
**이 파일 외 어떤 파일도 열거나 수정하지 말 것.** MIL/DeviceHandler/카메라 파일은 절대 접촉 금지(후속 별도 작업).

**코딩 규칙 (프로젝트 상시 규칙 — 위반 시 리젝)**
- 삼항 연산자 `?:` **금지**. 반드시 if-else.
- C# 7.2 문법만. (C# 8+ `switch` 식 / nullable 참조형 / `record` 금지)
- 브레이스 K&R(여는 중괄호 같은 줄) — `DatumName` 세터와 동일.
- 새 `using` 추가 금지 — 완전수식 사용.
- 주석은 "왜"가 비자명한 곳에만, `quick-260813:` 접두사. 주석 도배 금지.
- **주석 안에 `CustomMessageBox.Show` 라는 문자열을 쓰지 말 것.** 정적 검증이 `grep -c` 로 **줄 수**를 세므로, 주석에 메서드 이름이 들어가면 `MSG` 가 2가 되어 게이트가 깨진다. 주석에서는 "메시지박스" 라고만 쓴다. (아래 (4) 주석은 이미 그렇게 작성돼 있다 — 그대로 복사할 것.)

**(1) 삽입 위치**
`Datum|ImageSource` 그룹의 마지막 프로퍼티인
```csharp
        [Category("Datum|ImageSource")]
        [System.ComponentModel.Description("LineROI 라이브 캡처 z_index. -1=미설정(기존 정적 이미지 경로 사용)")]
        public int ZIndexB { get; set; } = -1;
```
바로 **다음 줄**, `// IOfflineImageParam — Datum 노드 Load 버튼이...` 주석 블록 **앞**에 아래 (2)~(4) 를 순서대로 삽입한다. (카메라/이미지 취득 계열 설정을 한곳에 모으기 위함)

**(2) 억제 플래그 (프로퍼티보다 먼저 선언)**
```csharp
        // quick-260813: 경고를 '사용자의 PropertyGrid 편집' 에서만 띄우기 위한 억제 플래그.
        //  ParamBase.Load(INI 리플렉션 SetValue)와 CopyPublicPropertiesTo(붙여넣기)가 같은 세터를 때리므로,
        //  가드가 없으면 레시피 로드/붙여넣기마다 경고창이 뜬다. 위 _suppressModelRename 과 동일한 패턴이며,
        //  단일 인스턴스 안에서 UI 스레드로만 켜고 끄므로 별도 동기화는 두지 않는다.
        private bool _suppressMirrorWarning;
```

**(3) 프로퍼티 2개** — 백킹필드는 각 프로퍼티 바로 위(이 파일의 `_ringLightBrightness1` 배치 관례).
```csharp
        // quick-260813: 카메라 하드웨어 촬영 방향(좌우/상하 뒤집기) 설정. 여기서는 값만 보관하고 실제 뒤집기는
        //  하지 않는다 — MIL grab 배선은 후속 별도 작업이며, 앱 시작 시 1회 적용될 예정이다.
        //  INI 키 미존재 시 ParamBase.Load 의 Boolean case 가 false 를 넣는데 C# 초기값과 같으므로
        //  ZIndexA/ZIndexB 같은 Load 오버라이드 폴백이 필요 없다.
        private bool _mirrorX;

        [Category("Datum|Mirror")]
        [System.ComponentModel.Description("카메라가 사진을 좌우로 뒤집어 찍게 한다. 기본 꺼짐. 프로그램을 다시 시작해야 적용된다.")]
        public bool MirrorX {
            get { return _mirrorX; }
            set {
                if (_mirrorX == value) return; // 같은 값 재저장 시 경고 반복 방지
                _mirrorX = value;
                RaisePropertyChanged(nameof(MirrorX));
                WarnMirrorChanged("좌우 반전(MirrorX)", value);
            }
        }

        private bool _mirrorY;

        [Category("Datum|Mirror")]
        [System.ComponentModel.Description("카메라가 사진을 상하로 뒤집어 찍게 한다. 기본 꺼짐. 프로그램을 다시 시작해야 적용된다.")]
        public bool MirrorY {
            get { return _mirrorY; }
            set {
                if (_mirrorY == value) return; // 같은 값 재저장 시 경고 반복 방지
                _mirrorY = value;
                RaisePropertyChanged(nameof(MirrorY));
                WarnMirrorChanged("상하 반전(MirrorY)", value);
            }
        }
```

**(4) 경고 헬퍼** — 문구는 초보 작업자가 읽는다는 전제로 쉬운 한국어. 3가지 요점 필수.
주석의 메서드 이름은 의도적으로 뺐다(위 코딩 규칙 마지막 항목 참조). 아래를 **그대로** 쓴다.
```csharp
        // quick-260813: 이 값은 '카메라가 찍어오는 사진 자체' 를 바꾸므로 이 Datum 하나가 아니라 같은 카메라를 쓰는
        //  다른 측정까지 영향을 받는다. 초보 작업자가 무심코 켰다가 원인 모를 전항목 틀어짐을 겪지 않도록 알린다.
        //  메시지박스 호출은 내부에서 Dispatcher.BeginInvoke 로 넘어가므로 세터를 블로킹하지 않는다
        //  (PropertyGrid 쓰기가 끝난 뒤 창이 뜬다 — 재진입 없음). 이중 마샬링 금지.
        //  isAutoClosing=false : 기본 7초 자동닫힘을 끈다. 읽고 직접 닫아야 하는 경고다.
        private void WarnMirrorChanged(string label, bool isOn) {
            if (_suppressMirrorWarning) return;
            string stateText;
            if (isOn) stateText = "켜짐";
            else      stateText = "꺼짐";
            string message =
                "[" + label + "] 설정을 '" + stateText + "' 으로 바꿨습니다.\n\n" +
                "1. 이 설정은 카메라가 사진을 찍어오는 방향 자체를 뒤집습니다. 화면에 보이는 그림만 돌리는 것이 아닙니다.\n\n" +
                "2. 같은 카메라로 찍는 다른 검사 항목의 측정값까지 함께 틀어질 수 있습니다. 잘 모르면 바꾸지 마시고, 바꿨다면 다른 항목들도 꼭 다시 확인하세요.\n\n" +
                "3. 지금 바로 적용되지 않습니다. 프로그램을 완전히 종료했다가 다시 실행해야 반영됩니다.";
            ReringProject.UI.CustomMessageBox.Show("촬영 방향(반전) 설정 변경", message,
                System.Windows.MessageBoxImage.Warning, true, false);
        }
```

**(5) 기존 `Load` 오버라이드에 억제 추가** (1157줄 부근). 기존 `_suppressModelRename` try/finally 를 **그대로 두고 한 줄씩만 덧붙인다**:
```csharp
            bool result;
            _suppressModelRename = true;
            _suppressMirrorWarning = true; // quick-260813: 리플렉션 SetValue 가 Mirror 세터를 때려 경고창이 뜨는 것을 막는다
            try {
                result = base.Load(loadFile, groupName);
            }
            finally {
                _suppressModelRename = false;
                _suppressMirrorWarning = false;
            }
```

**(6) 기존 `CopyTo` 오버라이드에 억제 추가** (1209줄 부근). `CopyPublicPropertiesTo` 는 **target** 세터를 때리므로 target 쪽 플래그를 켠다:
```csharp
        public override bool CopyTo(ParamBase param) {
            DatumConfig target = param as DatumConfig;
            if (target == null) return false;
            base.CopyTo(param);
            // quick-260813: 붙여넣기는 리플렉션으로 target 세터를 때린다. 사용자의 직접 편집이 아니므로 경고를 끈다.
            target._suppressMirrorWarning = true;
            try {
                CopyPublicPropertiesTo(target, _copyExclude);
            }
            finally {
                target._suppressMirrorWarning = false;
            }
            return true;
        }
```

**(7) 손대지 말 것**
- `_copyExclude` 에 MirrorX/MirrorY 를 **추가하지 않는다** (붙여넣기 시 값은 따라가야 정상).
- `IsHiddenForAlgorithm`, `EnsurePerRoiDefaults`, `BuildFilteredProperties`, 그 외 모든 기존 프로퍼티/검출·판정 로직 **무수정**.
  </action>
  <verify>
    <automated>cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs && echo "MirrorX=$(grep -c 'public bool MirrorX' $F) MirrorY=$(grep -c 'public bool MirrorY' $F) CAT=$(grep -c 'Datum|Mirror' $F) SUP=$(grep -c '_suppressMirrorWarning' $F) MSG=$(grep -c 'CustomMessageBox.Show' $F) TGT=$(grep -c 'target._suppressMirrorWarning' $F)"</automated>
  </verify>
  <done>
위 명령 출력이 `MirrorX=1 MirrorY=1 CAT=2 SUP=6 MSG=1 TGT=2` 이다.
(SUP=6 내역: 선언 1 + WarnMirrorChanged 검사 1 + Load true/false 2 + CopyTo true/false 2)
(MSG=1 내역: **실제 호출부 1줄뿐**이라는 뜻이다. `grep -c` 는 매칭된 '줄 수' 를 세므로 주석에 `CustomMessageBox.Show` 라는 문자열을 적으면 MSG=2 가 된다. 그래서 (4) 의 주석은 메서드 이름 없이 "메시지박스" 로만 쓴다. **MSG=2 가 나오면 중복 호출이 아니라 주석에 이름이 들어간 것이므로 주석을 고쳐 1로 맞춘다.**)
  </done>
</task>

<task type="auto">
  <name>Task 2: 정적 검증(회귀 0 / 규칙 준수) + Debug x64 빌드</name>
  <files>WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs</files>
  <action>
코드 수정 없음. 아래 4가지를 순서대로 검증하고 결과를 SUMMARY 에 기록한다.

**S1. 변경 파일 범위** — 소스 트리로 범위를 좁힌 `git status --porcelain -- WPF_Example` 가 정확히 2줄이어야 한다:
```
 M WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs   (사전 존재, 무관)
 M WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs                 (이번 작업)
```
`-- WPF_Example` pathspec 은 **필수**다. 빼면 이 plan 자신의 미추적 디렉터리 `?? .planning/quick/260813-fdt-side-datum-x-y/` 가 1줄 더 잡혀 **3줄**이 나온다(정상 상태이며 오류가 아니다 — 게이트를 오작동시킬 뿐이다).
`WPF_Example` 아래에 위 2개 외 다른 파일이 나오면 **즉시 중단하고 보고**한다.

**S2. 순수 추가 여부** — `git diff --numstat -- WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` 에서 삭제줄이 소수(기존 `Load`/`CopyTo` 본문 재작성분, 5줄 이하)인지 확인. 그 이상이면 기존 로직을 건드린 것이므로 중단·보고.

**S3. 코딩 규칙** — 추가된 줄(`git diff -U0 -- <파일> | grep '^+'`)에 삼항 연산자 `?:` 가 0건인지 확인. (한국어 문구에 물음표를 넣지 않았으므로 `?` 자체가 0건이어야 한다.) 새 `using` 라인이 추가되지 않았는지도 함께 확인.

**S4. 빌드** — 이 리포의 확립된 방식 그대로. **MSBuild 프로세스 종료코드가 유일한 성공 신호**다(`-v:minimal -nologo` 는 "Build succeeded." 문구를 지운다). 그리고 **경고 0 이 아니라 12 가 기준선**이다(`Sequence_Top.cs`/`Sequence_Bottom.cs`/`SequenceHandler.cs` 의 CS0618 ×10 + `VirtualCamera.cs` 의 CS0162 ×2 — 전부 이번 범위 밖, 재컴파일마다 항상 재출현).

```bash
cd /c/Info/Project/DataMeasurement
MSB="C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
LOG="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad/fdt-build.log"
"$MSB" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo > "$LOG" 2>&1
rc=$?
nerr=$(grep -c ': error' "$LOG"); nwarn=$(grep -c 'warning CS' "$LOG")
echo "BUILD_RC=$rc ERRORS=$nerr WARN_CS=$nwarn"
```
합격선: `BUILD_RC=0`, `ERRORS=0`, `WARN_CS=12`.

**출력물 잠김 폴백**: 앱이 실행 중이어서 `bin/x64/Debug` 산출물이 잠기면 **프로세스를 절대 죽이지 말 것**(리포 확립 규칙). 대신 스크래치 OutDir 로 컴파일만 검증한다. 아래 두 가지를 반드시 지킨다:
- **단일 대시** `-p:` 로 쓴다. `//p:` 는 MSYS 에서 UNC 경로로 뭉개져 MSB1001 이 난다.
- 경로 값은 **슬래시(`/`)** 로 쓰고 **끝도 `/`** 로 닫는다. **백슬래시로 끝내면 안 된다** — Bash 큰따옴표 안에서 `\"` 는 문자열 종료가 아니라 **이스케이프된 따옴표 문자**로 해석되어, MSBuild 가 실행되기도 전에 ``unexpected EOF while looking for matching `"'`` 로 죽는다. MSBuild 는 Windows 에서도 `/` 경로를 정상 처리한다(위 `$MSB` / `$LOG` 와 같은 표기).
```bash
"$MSB" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo -p:OutputPath="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad/fdt-bin/" > "$LOG" 2>&1
```
폴백을 썼다면 SUMMARY.md 에 반드시 명시한다.
  </action>
  <verify>
    <automated>cd /c/Info/Project/DataMeasurement && MSB="C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" && LOG="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad/fdt-build.log" && "$MSB" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo > "$LOG" 2>&1; rc=$?; echo "BUILD_RC=$rc ERRORS=$(grep -c ': error' "$LOG") WARN_CS=$(grep -c 'warning CS' "$LOG") FILES=$(git status --porcelain -- WPF_Example | wc -l)"</automated>
  </verify>
  <done>
`BUILD_RC=0 ERRORS=0 WARN_CS=12 FILES=2` 이고, S1~S3 이 모두 통과했다(추가 변경 파일 없음 / 삭제줄 5 이하 / 삼항·신규 using 0건).
(FILES 는 **`git status --porcelain -- WPF_Example`** 의 줄 수다: 사전 존재 `PickerCenterCalibrationService.cs` 1줄 + 이번 작업 `DatumConfig.cs` 1줄 = 2. pathspec 을 빼고 세면 `.planning/` 미추적 디렉터리 때문에 3이 나오므로, 3이 나왔다면 명령의 `-- WPF_Example` 누락을 먼저 의심한다.)
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 3: 실기 확인 (PropertyGrid 노출 · 경고 1회 · 조용한 경로 무경고 · INI 저장)</name>
  <what-built>
`DatumConfig` 에 **MirrorX / MirrorY** 두 개의 설정(체크박스)을 추가했습니다. 검사 탭에서 Datum 을 고르면 오른쪽 속성창에 **Mirror** 라는 그룹으로 나타납니다.

이 값을 실제로 바꾸면 경고창이 뜨고, 다음 3가지를 알려줍니다.
1. 카메라가 사진을 찍어오는 **방향 자체**가 바뀐다는 것
2. 같은 카메라를 쓰는 **다른 검사 항목의 측정값도 틀어질 수 있다**는 것
3. **프로그램을 껐다 켜야** 실제로 적용된다는 것

값을 바꾸지 않고 그대로 두면(같은 값 재저장) 경고는 뜨지 않습니다. 레시피를 불러오거나 Datum 을 복사·붙여넣기 할 때도 경고가 뜨지 않게 막아뒀습니다(이 경로들도 내부적으로는 같은 코드를 건드리기 때문에 막지 않으면 창이 계속 떴을 것입니다).

**주의: 이번 작업은 "설정값을 만드는 것"까지입니다.** 체크를 켜도 실제로 이미지가 뒤집히지는 않습니다. 실제 뒤집기(카메라 grab 방향 배선)는 다음 작업에서 합니다.
  </what-built>
  <how-to-verify>
1. 앱을 다시 빌드/실행하고 **검사** 탭으로 갑니다.
2. SIDE 의 Datum 노드(예: `Side_Datum_4-1`)를 클릭합니다.
   → 오른쪽 속성창 **Datum** 탭에 **Mirror** 그룹과 `MirrorX`, `MirrorY` 체크박스 2개가 보이는지 확인.
3. `MirrorX` 를 체크합니다.
   → 경고창이 **1번** 뜨고, 위 3가지 내용이 쉬운 말로 적혀 있는지 확인. **7초 뒤 자동으로 닫히지 않고** 직접 닫아야 하는지 확인.
4. 창을 닫고, 같은 칸을 클릭했다가 값 변경 없이 빠져나옵니다.
   → 경고가 **다시 뜨지 않아야** 합니다.
5. 레시피를 **저장**한 뒤, 다른 레시피로 바꿨다가 다시 이 레시피를 **불러옵니다**.
   → 불러오는 동안 경고창이 **한 번도 뜨지 않아야** 합니다. 그리고 `MirrorX` 체크가 **유지**돼 있어야 합니다(INI 저장 확인).
6. (선택) Datum 노드를 **복사 → 붙여넣기** 합니다.
   → 경고가 뜨지 않고, 붙여넣어진 Datum 의 `MirrorX` 도 같이 켜져 있어야 합니다.
7. 마지막으로 `MirrorX` 를 다시 **꺼서 원래대로(false) 되돌리고** 레시피를 저장합니다(이번 작업은 설정만 추가한 것이므로 운영값은 원복 상태로 남깁니다).

문제가 있으면 어느 번호에서 무엇이 달랐는지 알려주세요.
  </how-to-verify>
  <resume-signal>"approved" 라고 쓰시거나, 문제가 있으면 번호와 증상을 알려주세요</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| (해당 없음) | 이번 변경은 로컬 프로세스 내부 설정 프로퍼티 2개 추가로, 신뢰 경계를 새로 넘는 입력이 없다. TCP/파일/외부 API 표면 변화 0. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-fdt-01 | Tampering | 레시피 INI (`MirrorX`/`MirrorY` 키) | accept | 기존 레시피 파일과 동일한 신뢰 수준(로컬 디스크, 이미 수십 개 키가 같은 경로로 저장됨). 새 공격면 아님. bool 파싱은 `ToBool()` 로 예외 없이 false 폴백. |
| T-fdt-02 | Denial of Service | `WarnMirrorChanged` → `CustomMessageBox` | mitigate | 리플렉션 대량 쓰기(`ParamBase.Load` 레시피 전체 로드, `CopyPublicPropertiesTo` 붙여넣기)가 세터를 때려 모달 창이 폭주하는 자기-DoS 를 `_suppressMirrorWarning` try/finally 가드로 차단. 동일값 재저장은 세터 초입 idempotent 가드로 차단. |
| T-fdt-03 | Tampering | 검사 정확도 (사용자 오조작) | mitigate | 값 변경 시 "다른 측정까지 틀어질 수 있다 + 재시작 필요" 를 자동닫힘 없는 경고로 강제 고지(본 작업의 목적 그 자체). |
</threat_model>

<verification>
1. `grep` 정적 검증 6종 (Task 1 done 기준 `MirrorX=1 MirrorY=1 CAT=2 SUP=6 MSG=1 TGT=2`) 통과
2. `git status --porcelain -- WPF_Example` 2줄 — DatumConfig.cs 외 신규 변경 0 (pathspec 없이 세면 `.planning/` 미추적 디렉터리가 포함돼 3줄이 나온다 — 정상이며 오류 아님)
3. 삼항 연산자 0건, 신규 `using` 0건, C# 7.2 문법 준수
4. Debug/x64 빌드 `BUILD_RC=0` / `: error` 0 / `warning CS` **12**(기준선 불변)
5. 실기 확인 Task 3 의 7단계 통과
</verification>

<success_criteria>
- `DatumConfig` 에 `[Category("Datum|Mirror")]` public bool `MirrorX`, `MirrorY` 2개 존재, 기본값 false, ParamBase 리플렉션으로 INI 자동 영속
- 값이 **실제로 바뀔 때만** `CustomMessageBox` 경고 1회 표시(자동닫힘 off), 문구에 하드웨어 방향/타 측정 영향/재시작 필요 3점 포함. 호출부는 파일 전체에서 **정확히 1곳**
- 레시피 로드·Datum 붙여넣기 경로에서는 경고 미발생, 값은 정상 보존/복사
- MIL·DeviceHandler·카메라 파일 및 Datum 검출/판정 로직 무변경, 변경 파일 = `DatumConfig.cs` 단 1개
- 빌드 기준선(error 0 / warning CS 12) 불변
</success_criteria>

<output>
완료 후 `.planning/quick/260813-fdt-side-datum-x-y/260813-fdt-SUMMARY.md` 를 작성한다.
포함 항목: 실제 삽입 위치(줄 번호), 정적 검증 6종 실측 출력, 빌드 실측 출력(BUILD_RC/ERRORS/WARN_CS), 스크래치 OutDir 폴백 사용 여부, 실기 확인 7단계 결과, 그리고 **후속 작업 인계 메모**(MirrorX/MirrorY 를 `MilCamera.RegisterRoleInfo`/`_roleInfoMap` 경로에서 앱 시작 시 1회 소비 — 이번 작업 범위 밖).
</output>
