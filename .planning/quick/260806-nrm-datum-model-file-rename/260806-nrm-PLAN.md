---
phase: quick-260806-nrm
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
autonomous: false
requirements: [TEACH-AUDIT-CO-01]

must_haves:
  truths:
    - "PropertyGrid 에서 티칭 완료된 Datum 의 이름을 A→B 로 바꾸면, 디스크의 패턴 모델 파일이 A 기반 경로에서 B 기반 경로로 실제로 이동한다(옛 파일 사라지고 새 파일 생김)"
    - "이름 변경 직후 재티칭 없이 Test Find / 검사를 돌려도 패턴 매칭이 계속 성공한다(ALIGN_FAIL 로 조용히 죽지 않는다)"
    - "패턴1(.shm/.ncm)과 패턴2(_2 페어) 둘 다 이동한다. 패턴2 를 설정하지 않은 Datum 은 패턴2 파일이 없어도 오류가 아니라 조용한 skip 이다"
    - "아직 티칭하지 않은 Datum 의 이름을 바꿔도 아무 오류/로그 없이 정상 동작한다(옛 경로에 파일이 없는 것이 정상 케이스)"
    - "이동 대상 경로에 이미 다른 파일이 있으면 덮어쓰지 않고 Error 로그만 남긴다(데이터가 조용히 파괴되지 않는다)"
    - "파일 이동이 실패해도(잠김/권한) DatumName 변경 자체는 성립한다 — 사용자는 이름을 바꿀 수 있고 Error 로그로 재티칭 필요를 안다"
    - "레시피 INI 로드 중에는 리네임이 절대 발동하지 않는다(로드는 리플렉션으로 세터를 때리므로, 발동하면 멀쩡한 다른 Datum 의 모델 파일을 훔쳐간다)"
    - "신규 Datum 추가(AddDatum) 중에는 리네임이 절대 발동하지 않는다(신규 객체 초기값 'Datum_1' → 지정이름 변경이 기존 Datum_1 의 .shm 을 훔쳐가는 경로)"
    - "Debug/x64 빌드가 신규 에러 0 으로 통과한다"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs"
      provides: "DatumName 세터의 모델파일 리네임 훅 + 리네임 억제 플래그 + 세터 우회 초기화 메서드"
      contains: "quick-260806-nrm"
    - path: "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs"
      provides: "AddDatum 이 세터 대신 세터-우회 초기화 메서드를 사용하도록 1줄 교체"
      contains: "InitializeDatumName"
  key_links:
    - from: "DatumConfig.DatumName 세터(변경 확정 직전)"
      to: "InspectionSequence.ResolveDatumModelPath(this, OwnerName) / ResolveDatumModelPath2(this, OwnerName)"
      via: "옛 이름 기준 경로를 _datumName 대입 '전에' 계산 → 대입 후 새 이름 기준 경로 계산 → File.Move"
      pattern: "ResolveDatumModelPath2?\\(this, OwnerName\\)"
    - from: "DatumConfig.Load(IniFile, string) 의 base.Load 구간"
      to: "리네임 억제 플래그 _suppressModelRename"
      via: "try/finally 로 true→false, INI 로드 중 세터 발동을 무해화"
      pattern: "_suppressModelRename"
    - from: "InspectionSequence.AddDatum"
      to: "DatumConfig.InitializeDatumName(name)"
      via: "`datum.DatumName = datumName;` 를 세터 우회 초기화 호출로 교체"
      pattern: "datum\\.InitializeDatumName\\(datumName\\)"
    - from: "이동 실패 / 이름 충돌"
      to: "Error 로그 '[DatumRename]'"
      via: "Logging.PrintErrLog — 예외를 삼키되 조용히 죽지 않게 흔적을 남긴다"
      pattern: "\\[DatumRename\\]"
---

<objective>
`DatumName` 을 바꾸면 패턴매칭 모델 파일(`.shm`/`.ncm`, `_2` 페어 포함)이 새 이름 기준 경로로 **자동으로 따라 이동**하게 만든다.
2026-07-10 티칭감사 carry-over #1("모델파일 고아")의 해소이며, 이번 세션에 실제로 터진 SIDE 전항목 Fail 사고의 재발 방지 코드 수정이다.

**왜 이게 필요한가 (이번 세션 코드로 확정):**
- 패턴 모델 파일 경로는 **어디에도 저장되지 않고** 매 호출마다 `DatumConfig.DatumName` 에서 새로 계산된다
  (`InspectionSequence.ResolveDatumModelPath` → `RecipeFiles.GetPatternModelFilePath` → `"Datum" + DatumName + 확장자`, 구분자 없음).
- 그래서 사용자가 PropertyGrid 에서 이름을 바꾸는 **그 순간부터** 모든 읽기/쓰기가 새 파일명을 보는데, 디스크의 실제 바이트는
  티칭 시점의 **옛 이름 그대로** 남는다. `ReadShapeModel`(없는 경로) → `PatternMatchService.TryFindPose` 의 catch → `false`
  → `MarkAlignFailed` → **모달 하나 없이 조용히** 해당 Datum 에 걸린 FAI 전부 Fail.
- 실제 사고: Side PC 에서 Datum 4개(`Side_Datum_1`→`Side_Datum_3-1` 등) 개명 후 SIDE 일괄검사 전항목 Fail.

Purpose: 이름 변경이 데이터(모델 파일)와 어긋나지 않게 하여, 개명 후 재티칭 없이도 검사가 계속 성립하게 한다.
Output: 2개 파일 수정(`DatumConfig.cs` 리네임 훅 + 억제 가드, `InspectionSequence.cs` 1줄 교체). Debug/x64 빌드 PASS.
스크래치 격리 하네스로 파일 이동 판정표 4케이스 자동 검증. 실기 PropertyGrid 개명은 checkpoint 로 사람 확인.

**범위 밖(절대 건드리지 않음):**
- 이미 수동 복구 완료된 16개 물리 파일(`D:\Data\Recipe\FAI_1\SIDE\*`, `D:\디팜스자료\Side_Info\Data\Recipe\FAI_1\SIDE\*`) — 손대지 않는다.
- 2026-07-10 감사의 다른 carry-over(NormalizeTeachingKey, ParamBase 0-클로버, clamp 불일치 등).
- `PatternEngine` 전환(Shape↔NCC) 로 인한 고아 — **다른 트리거이므로 이번 범위 아님.** 이번 수정 후에도
  "Shape 로 티칭 → NCC 로 전환 → 개명" 조합에서는 옛 `.shm` 이 남는다(리졸버가 현재 엔진 확장자만 계산하므로).
  이건 알려진 잔여 갭으로 SUMMARY 에만 기록하고 코드로 다루지 않는다.
- `Datum 삭제` 시 고아(File.Delete) — 이번 범위 아님.
- `PatternMatchService` 의 static 모델 캐시(`_modelCache`) — **분석 결과 수정 불필요**(아래 interfaces 참고). 손대지 않는다.
- `RecipeFileHelper.GetPatternModelFilePath` 경로 공식 복제/재구현 — 금지. 반드시 기존 리졸버를 통해서만 계산한다.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md

<style_rules>
프로젝트 규칙 (예외 없음):
- **삼항 연산자 `?:` 절대 금지 → if-else 만 사용.** (`??` null 병합도 쓰지 말 것 — 명시적 if 로.)
- C# 7.2 / .NET Framework 4.8. C# 8+ 문법(switch expression, nullable reference, `is not`) 사용 금지.
- 새 주석은 짧게, **비자명한 "왜"만 한국어로**. 출처는 `// quick-260806-nrm: ...` 형태로 표기하고,
  2026-07-10 티칭감사 carry-over #1 을 1회 언급한다. 날짜 프리픽스(`//YYMMDD hbk`) 규칙은 폐기됐으니 쓰지 말 것.
- **기존 주석/기존 코드 재작성 금지.** 이번 변경은 순수 추가 + 1줄 교체다.
- 브레이스 스타일: 두 파일 모두 편집 지점 주변이 **K&R**(여는 브레이스 같은 줄)이다 —
  `DatumConfig.DatumName` 세터, `EnsurePerRoiDefaults`, `Load` override, `CopyTo`, `InspectionSequence.AddDatum` 전부 K&R.
  신규 헬퍼도 **K&R** 로 맞춘다. (같은 파일 아래쪽 `PatternEngineList` 등이 Allman 인 건 무시 — 편집 지점 주변 스타일 우선.)
- 헝가리언 강제 도입 안 함 — 이 파일의 기존 로컬 변수 관례(`oldName`, `modelPath` 같은 평범한 camelCase)를 따른다.
- `using` 추가 금지. 아래 interfaces 에 적힌 대로 **완전한정명**을 쓴다
  (이 파일은 이미 `System.ComponentModel.Browsable`, `Newtonsoft.Json.JsonIgnore`, `System.Math` 처럼 완전한정을 습관적으로 쓴다).
</style_rules>

<interfaces>
<!-- 이번 세션에 실제 코드를 읽어 확인한 계약. 그대로 사용하고 추가 탐색하지 말 것. -->

**1) 모델 경로 리졸버 — `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`, `public static`, 네임스페이스 `ReringProject.Sequence`**
```csharp
public static string ResolveDatumModelPath (DatumConfig datum);                        // 1822행 — 호출부 0건(사실상 死코드)
public static string ResolveDatumModelPath (DatumConfig datum, string ownerSeqName);   // 1850행 — 실제 전 호출부가 쓰는 오버로드
public static string ResolveDatumModelPath2(DatumConfig datum);                        // 1893행 — 호출부 0건(사실상 死코드)
public static string ResolveDatumModelPath2(DatumConfig datum, string ownerSeqName);   // 1917행 — 실제 전 호출부가 쓰는 오버로드
```
- 넷 다 내부에서 `datum.DatumName` 과 `datum.PatternEngine` 을 **live 로 읽는다** → 옛 경로는 반드시 `_datumName` 대입 **전에** 계산해야 한다.
- 확장자 선택(`.shm`/`.ncm`)은 리졸버 내부의 `GetPatternModelFilePath(..., datum.PatternEngine)` 이 이미 처리한다 → **확장자 로직 재구현 금지.**
- `_2` 접미사도 `ResolveDatumModelPath2` 내부에서 붙인다 → **접미사 재구현 금지.**
- 부작용: 경로 계산 시 상위 디렉터리가 없으면 `Directory.CreateDirectory` 를 한다(멱등, 무해).
- **의존성 주의:** 내부에서 `SystemHandler.Handle.Setting.CurrentRecipeName`, `SystemHandler.Handle.Sequences.RecipeManager.Shots`,
  `SystemHandler.Handle.Recipes` 를 탄다 → 앱 초기화 이전/레시피 미로드 시점에 부르면 NullReference 가능. 반드시 try/catch 로 감싼다.

**⚠ 어느 오버로드를 쓸 것인가 — 발주 요구사항 #7 의 정정**
발주서는 "1-arg 오버로드가 내부적으로 `datum.OwnerName` 을 쓰니 1-arg 를 쓰라"고 했으나, 실제 코드는 그렇지 않다:
- 1-arg 는 `OwnerName` 을 **쓰지 않는다.** `SourceShotName` 으로 Shot 을 역추적하고, 미매칭 시 **전역 `Shots[0]`** 으로 폴백한다.
- 2-arg 는 미매칭 시 `ownerSeqName` 소유 Shot 으로 스코프를 좁힌다(260723 quick-fix 가 고친 결함이 바로 1-arg 의 전역 폴백이다).
- grep 결과 **실제 호출부 11곳 전부가 2-arg** 이며, 티칭 시점 저장 경로(`MainView.xaml.cs:3661/3682`)도 `(datum, datum.OwnerName)` 이다.
  1-arg 는 호출부가 0건이다.
→ **반드시 2-arg + `OwnerName` 을 쓴다.** 1-arg 를 쓰면 티칭이 실제로 쓴 폴더와 다른 폴더를 계산해
  "옛 파일 없음 → 조용히 skip" 으로 **버그를 그대로 재현**한다. 이 정정 사실을 SUMMARY 에 기록할 것.

**2) `OwnerName` — `WPF_Example/Sequence/Param/ParamBase.cs:41`, `DatumConfig` 가 상속**
```csharp
public string OwnerName { get; }   // Owner 가 SequenceBase 면 그 Name("TOP"/"SIDE"/"BOTTOM"), 아니면 null
```
`DatumConfig` 생성 경로는 `InspectionSequence.AddDatum()` 의 `new DatumConfig(this)` 단 하나라 소유 시퀀스명으로 신뢰 가능
(`MainView.xaml.cs:3972` 주석이 같은 근거를 명시).

**3) 로깅 — `Logging` 은 `ReringProject.Utility`(DatumConfig.cs 4행에 이미 `using`), `ELogType` 은 `ReringProject.Setting`(using 없음 → 완전한정 필요)**
```csharp
Logging.PrintLog   (int logId, string format, params object[] args);
Logging.PrintErrLog(int logId, string format, params object[] args);
// 사용례: Logging.PrintErrLog((int)ReringProject.Setting.ELogType.Error, "...");
```

**4) 세터를 때리는 경로 — 전수 조사 완료(이 3개가 전부)**
| 경로 | 위치 | 위험 | 처리 |
|------|------|------|------|
| PropertyGrid 사용자 편집 | PropertyTools 바인딩 | 없음(이게 우리가 원하는 유일한 트리거) | 리네임 발동 |
| INI 로드 리플렉션 | `ParamBase.Load` 385~387행 `case "String": prop.SetValue(this, sValue)` | **치명적** — 기본값 `"Datum_1"` → 저장된 이름으로 세터가 불려 `DatumDatum_1.shm` 을 훔쳐간다 | `DatumConfig.Load` override 에서 억제 |
| 신규 Datum 추가 | `InspectionSequence.cs:1746` `datum.DatumName = datumName;` | **치명적** — 새 객체 초기값 `"Datum_1"` → `"Datum_3"` 변경이 1번 Datum 의 모델을 훔쳐간다 | 세터 우회 메서드로 교체 |

복사/붙여넣기(`DatumConfig.CopyTo`)는 **이미 안전하다** — `_copyExclude`(1093~1110행)에 `"DatumName"` 이 들어 있어
`CopyPublicPropertiesTo` 가 건너뛴다. **여기는 손대지 말 것**(Task 2 에서 grep 으로 재확인만).

**5) `DatumConfig` 의 기존 앵커(그대로 유지, 재작성 금지)**
```csharp
private string _datumName = "Datum_1";                       // 17행 — 필드 초기화라 세터를 타지 않는다(안전)
public string DatumName { get; set; }                        // 19~26행 — 현재는 RaisePropertyChanged 만
public override bool Load(IniFile loadFile, string groupName)// 1073행 — 이미 존재하는 override. base.Load 호출부를 감싸면 된다
```

**6) `PatternMatchService` static 캐시 — 수정 불필요(분석 완료, 손대지 말 것)**
`_modelCache` 는 **`modelPath` 문자열을 키로** 한다(`PatternMatchService.cs:51`). 개명 후에는 새 경로 = 새 키 →
디스크에서 새로 읽으므로 stale 이 발생하지 않는다. 옛 경로 엔트리는 다시 조회되지 않고 남지만, 개명 횟수만큼의
핸들이라 무시 가능하다. 재티칭 시에는 `TryCreateModel` 이 `InvalidateCache(modelPath)` 를 부르므로 자가 치유된다.
</interfaces>

<architecture_decision>
**결정: `DatumConfig.DatumName` 세터에서 `InspectionSequence` 의 `public static` 리졸버를 직접 호출한다.**

근거(코드로 확인):
- 두 클래스 모두 네임스페이스 `ReringProject.Sequence`, 동일 어셈블리 → 추가 `using` 없이 직접 호출 가능.
- C# 은 어셈블리 내 타입 간 상호 참조를 허용하므로 "순환 참조" 컴파일 에러가 존재하지 않는다.
- `InspectionSequence` 의 필드는 전부 **인스턴스** 필드(`private readonly DeviceHandler pDevs` 등)이고 static 필드 초기화가 없다
  → static 생성자 초기화 순서 사이클 위험 없음.

따라서 발주서 요구사항 #8 의 폴백(헬퍼를 `InspectionSequence.cs` 로 옮기는 안)은 **불필요**하다.
단, 컴파일이 실제로 이 판단을 증명하므로 Task 1 의 빌드 성공이 곧 이 결정의 검증이다.
빌드가 실패하면 그때 폴백(정적 헬퍼를 `InspectionSequence.cs` 에 두고 `DatumConfig` 는 그것만 호출)으로 전환하고 SUMMARY 에 사유를 남긴다.
</architecture_decision>

<tasks>

<task type="auto">
  <name>Task 1: DatumName 세터 모델파일 리네임 훅 + 오발동 억제 가드 구현</name>
  <files>WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs, WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs</files>
  <action>
**(A) `DatumConfig.cs` — 기존 `DatumName` 프로퍼티(19~26행) 교체 + 헬퍼 추가**

기존 세터는 `if (_datumName == value) return; _datumName = value; RaisePropertyChanged(...)` 3줄이다.
이 구조를 유지한 채 "옛 경로 계산 → 대입 → 새 경로 계산 → 이동" 순서를 끼워 넣는다.
**순서가 계약이다** — 리졸버가 `_datumName` 을 live 로 읽으므로 옛 경로는 반드시 대입 전에 계산해야 한다.

`_datumName` 필드(17행) 바로 아래에 억제 플래그를 추가한다:
```csharp
// quick-260806-nrm: 모델 파일 리네임을 '사용자의 PropertyGrid 개명' 에서만 발동시키기 위한 억제 플래그.
//  INI 로드/신규추가 경로도 리플렉션·대입으로 이 세터를 때리는데, 거기서 리네임이 돌면
//  초기값 "Datum_1" 기준 경로를 옮겨버려 멀쩡한 1번 Datum 의 모델을 훔쳐간다.
//  단일 인스턴스 안에서 UI 스레드로만 켜고 끄므로 별도 동기화는 두지 않는다.
private bool _suppressModelRename;
```

세터를 다음 형태로 바꾼다(K&R, 삼항 금지):
```csharp
// quick-260806-nrm: 이름이 바뀌면 패턴 모델 파일(.shm/.ncm, _2 페어 포함)도 새 이름 경로로 따라 옮긴다.
//  모델 경로는 저장되지 않고 DatumName 에서 매번 재계산되므로(ResolveDatumModelPath), 이름만 바꾸면
//  디스크 파일은 옛 이름에 남아 ReadShapeModel 이 조용히 실패 → MarkAlignFailed(모달 없음) → 전항목 Fail 이 된다.
//  2026-07-10 티칭감사 carry-over #1(모델파일 고아) 해소.
[Category("Datum|Identity")]
public string DatumName {
    get { return _datumName; }
    set {
        if (_datumName == value) return;
        string oldName = _datumName;
        // 리졸버가 _datumName 을 live 로 읽으므로 옛 경로는 반드시 대입 '전에' 확보해야 한다.
        string oldPath1 = null;
        string oldPath2 = null;
        bool shouldRename = false;
        if (!_suppressModelRename && !string.IsNullOrEmpty(oldName) && !string.IsNullOrEmpty(value)) {
            shouldRename = true;
            oldPath1 = TryResolveModelPathQuiet(false);
            oldPath2 = TryResolveModelPathQuiet(true);
        }
        _datumName = value;
        if (shouldRename) {
            string newPath1 = TryResolveModelPathQuiet(false);
            string newPath2 = TryResolveModelPathQuiet(true);
            MoveModelFileIfPresent(oldPath1, newPath1, oldName, value);
            MoveModelFileIfPresent(oldPath2, newPath2, oldName, value);
        }
        RaisePropertyChanged(nameof(DatumName));
    }
}
```

세터 바로 아래에 헬퍼 3개를 추가한다:
```csharp
// quick-260806-nrm: 신규 Datum 생성 시 초기 이름 주입 전용(AddDatum). 세터를 우회해 리네임을 발동시키지 않는다.
//  새 객체의 초기값 "Datum_1" 에서 지정 이름으로 바뀌는 것뿐인데, 그걸 개명으로 오인하면
//  실제 1번 Datum 이 티칭해 둔 모델 파일을 빼앗아 간다.
public void InitializeDatumName(string name) {
    _datumName = name;
    RaisePropertyChanged(nameof(DatumName));
}

// quick-260806-nrm: 경로 계산은 SystemHandler 싱글턴(레시피명/Shots)에 의존한다. 앱 초기화 이전이나
//  레시피 미로드 시점에 불리면 NullReference 가 날 수 있으므로 삼키고 null 을 돌려준다 — 리네임만 포기하고
//  이름 변경 자체는 그대로 성립시킨다.
//  오버로드는 반드시 (datum, OwnerName) 2-arg 를 쓴다. 1-arg 는 SourceShotName 미매칭 시 전역 Shots[0] 로
//  폴백해 티칭이 실제로 쓴 폴더와 다른 경로를 만든다(260723 quick-fix 가 고친 결함).
private string TryResolveModelPathQuiet(bool isSecondPattern) {
    try {
        if (isSecondPattern) return InspectionSequence.ResolveDatumModelPath2(this, OwnerName);
        return InspectionSequence.ResolveDatumModelPath(this, OwnerName);
    }
    catch {
        return null;
    }
}

// quick-260806-nrm: 옛 경로 파일이 있으면 새 경로로 옮긴다. 판정표:
//   경로 계산 실패(null/빈값) → skip
//   옛 == 새             → skip (엔진/폴더가 같고 이름만 대소문자 차이 등)
//   옛 파일 없음          → 조용히 skip. 티칭 전 Datum 의 정상 케이스라 오류가 아니다.
//   새 경로에 파일 존재    → 덮어쓰지 않고 Error 로그. 다른 Datum 의 모델을 조용히 파괴하면 안 된다.
//   그 외 예외(잠김/권한)  → Error 로그만. 이름 변경을 되돌리지 않는다 — 사용자는 이름을 바꿀 수 있어야 하고,
//                          로그로 "재티칭 필요" 를 알면 된다.
private static void MoveModelFileIfPresent(string oldPath, string newPath, string oldName, string newName) {
    if (string.IsNullOrEmpty(oldPath)) return;
    if (string.IsNullOrEmpty(newPath)) return;
    if (string.Equals(oldPath, newPath, System.StringComparison.OrdinalIgnoreCase)) return;
    try {
        if (!System.IO.File.Exists(oldPath)) return;
        if (System.IO.File.Exists(newPath)) {
            Logging.PrintErrLog((int)ReringProject.Setting.ELogType.Error,
                "[DatumRename] 대상 경로에 파일이 이미 있어 모델 파일을 옮기지 않았다(덮어쓰기 금지). '"
                + oldName + "' -> '" + newName + "' : " + newPath);
            return;
        }
        System.IO.File.Move(oldPath, newPath);
        Logging.PrintLog((int)ReringProject.Setting.ELogType.Trace,
            "[DatumRename] 패턴 모델 파일 이동 완료. '" + oldName + "' -> '" + newName + "' : " + newPath);
    }
    catch (System.Exception ex) {
        Logging.PrintErrLog((int)ReringProject.Setting.ELogType.Error,
            "[DatumRename] 모델 파일 이동 실패 — 해당 Datum 재티칭 필요. '"
            + oldName + "' -> '" + newName + "' : " + ex.Message);
    }
}
```

**(B) `DatumConfig.cs` — 기존 `Load` override(1073행)에서 base.Load 구간 억제**

기존 본문의 `bool result = base.Load(loadFile, groupName);` 한 줄만 아래로 교체한다.
**그 아래 ZIndexA/ZIndexB 블록과 기존 주석은 한 글자도 건드리지 않는다.**
```csharp
        // quick-260806-nrm: base.Load 는 리플렉션 SetValue 로 DatumName 세터를 때린다(초기값 "Datum_1" → 저장된 이름).
        //  이 구간에서 리네임이 돌면 다른 Datum 의 모델 파일을 옮겨버리므로 반드시 끈다.
        bool result;
        _suppressModelRename = true;
        try {
            result = base.Load(loadFile, groupName);
        }
        finally {
            _suppressModelRename = false;
        }
```

**(C) `InspectionSequence.cs` — `AddDatum`(1746행) 1줄 교체**

`datum.DatumName = datumName;` → `datum.InitializeDatumName(datumName);`
위에 짧은 이유 주석 1줄을 붙인다:
```csharp
            // quick-260806-nrm: 세터를 쓰면 초기값 "Datum_1" → 지정이름 변경이 개명으로 오인돼 1번 Datum 의 모델 파일을 옮겨간다.
            datum.InitializeDatumName(datumName);
```
`AddDatum` 의 나머지(이름 생성 로직, `DatumConfigs.Add`, return)는 무변경.

**금지사항 재확인:** `RecipeFileHelper.GetPatternModelFilePath` 복제 금지 · 1-arg 리졸버 사용 금지 ·
`PatternMatchService` 수정 금지 · `_copyExclude` 수정 금지 · `ParamBase.Load` 수정 금지 · 삼항/`??` 금지 · `using` 추가 금지.
  </action>
  <verify>
  <automated>cd "C:/Info/Project/DataMeasurement" && "/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" //t:Rebuild //p:Configuration=Debug //p:Platform=x64 //v:minimal //nologo 2>&1 | grep -iE "error|Build succeeded"</automated>
  </verify>
  <done>
Debug/x64 빌드가 `Build succeeded`, error 0 으로 통과한다(이것이 곧 architecture_decision 의 "직접 static 호출 가능" 증명이다).
빌드 산출물이 잠겨 있으면 **프로세스를 절대 죽이지 말고** 스크래치 `//p:OutputPath=` 로 컴파일-only 검증한 뒤 잠김 사실을 SUMMARY 에 명시한다.
변경 파일은 정확히 2개(`DatumConfig.cs`, `InspectionSequence.cs`)이며 삭제 라인은 교체된 세터/`bool result` 줄/`AddDatum` 1줄 외에 없다.
  </done>
</task>

<task type="auto">
  <name>Task 2: 오발동 경로 정적 회귀검증 + 파일이동 판정표 격리 하네스 검증</name>
  <files>C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/6daecb8f-c376-47ac-89d1-018d55afefc3/scratchpad/nrm-verify/MoveHarness.cs</files>
  <action>
**(A) 정적 회귀검증 — "리네임이 엉뚱한 데서 발동하지 않는가"**

이 수정의 최대 위험은 기능 미작동이 아니라 **오발동으로 인한 데이터 파괴**다. grep 으로 다음을 확정한다:
1. `DatumName` 에 대한 직접 대입(`datum.DatumName =` 류)이 코드베이스 전체에서 **0건**이어야 한다
   (Task 1 (C) 로 유일한 대입처를 `InitializeDatumName` 으로 바꿨으므로).
2. `_copyExclude` 에 `"DatumName"` 이 **여전히 존재**한다(복사 경로는 원래 안전했고 그 상태가 유지돼야 한다).
3. `DatumConfig` 를 JSON 역직렬화하는 곳이 없다(있으면 Newtonsoft 가 세터를 때려 4번째 오발동 경로가 된다).
4. 리네임 호출이 **2-arg 오버로드**만 쓴다(`ResolveDatumModelPath(this, OwnerName)` / `ResolveDatumModelPath2(this, OwnerName)`).
5. 새 코드에 삼항 `?:` 와 `??` 가 없다.

**(B) 격리 하네스 — 파일 이동 판정표 4케이스 자동 검증**

실코드 경로는 `SystemHandler` 싱글턴(WPF+HALCON+디바이스) 없이는 인스턴스화가 불가능하므로 E2E 자동화가 안 된다.
대신 `MoveModelFileIfPresent` **본문을 그대로 복사**한 콘솔 하네스를 스크래치에 만들어 실제 임시 파일로 4케이스를 돌린다.
(`Logging` 두 줄만 `Console.WriteLine` 스텁으로 치환하고, **판정 로직/분기 순서는 한 글자도 바꾸지 않는다.**)

검증 케이스:
| # | 상황 | 기대 |
|---|------|------|
| 1 | 옛 파일 존재, 새 경로 비어있음 | 새 파일 존재 + 옛 파일 사라짐 → `MOVED_OK` |
| 2 | 옛 파일 없음(티칭 전 Datum) | 아무 일 없음, 로그 없음 → `SKIP_MISSING_OK` |
| 3 | 옛 파일 존재 + 새 경로에 이미 다른 파일 | 둘 다 원본 그대로 보존 + Error 로그 1건 → `SKIP_COLLISION_OK` |
| 4 | 옛 파일이 다른 프로세스에 잠김(FileStream 점유) | 예외 삼킴 + Error 로그 1건, 옛 파일 보존 → `LOCKED_LOGGED_OK` |

컴파일: `C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe`.
하네스는 스크래치에만 만들고 **리포지토리에는 어떤 파일도 추가하지 않는다.**
  </action>
  <verify>
  <automated>cd "C:/Info/Project/DataMeasurement" && D="WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs"; I="WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs"; A=$(grep -rn "\.DatumName[[:space:]]*=[^=]" --include=*.cs WPF_Example/ | wc -l); if [ "$A" -eq 0 ]; then echo "NO_DIRECT_ASSIGN_OK"; else echo "DIRECT_ASSIGN_REMAINS:"; grep -rn "\.DatumName[[:space:]]*=[^=]" --include=*.cs WPF_Example/; fi; if [ "$(grep -c '"DatumName",' "$D")" -ge 1 ]; then echo "COPY_EXCLUDE_INTACT_OK"; else echo "COPY_EXCLUDE_BROKEN"; fi; if [ "$(grep -c "InitializeDatumName(datumName)" "$I")" -eq 1 ]; then echo "ADDDATUM_REWIRED_OK"; else echo "ADDDATUM_NOT_REWIRED"; fi; if [ "$(grep -c "ResolveDatumModelPath2(this, OwnerName)" "$D")" -eq 1 ] && [ "$(grep -c "ResolveDatumModelPath(this, OwnerName)" "$D")" -eq 1 ]; then echo "TWO_ARG_OVERLOAD_OK"; else echo "WRONG_OVERLOAD"; fi; if [ "$(grep -c "_suppressModelRename" "$D")" -ge 4 ]; then echo "SUPPRESS_GUARD_PRESENT_OK"; else echo "SUPPRESS_GUARD_MISSING"; fi; J=$(grep -rn "DeserializeObject<[^>]*Datum" --include=*.cs WPF_Example/ | wc -l); if [ "$J" -eq 0 ]; then echo "NO_JSON_DESERIALIZE_OK"; else echo "JSON_PATH_FOUND"; fi; T=$(git diff -U0 -- "$D" "$I" | grep "^+" | grep -cE "\?[^?]*:|\?\?"); echo "TERNARY_OR_COALESCE_IN_NEW_CODE=$T"; echo "CHANGED_FILES=[$(git diff --name-only | tr '\n' ' ')]"</automated>
  <automated>S="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/6daecb8f-c376-47ac-89d1-018d55afefc3/scratchpad/nrm-verify"; "C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe" //nologo //out:"$S/MoveHarness.exe" "$S/MoveHarness.cs" && "$S/MoveHarness.exe"</automated>
  </verify>
  <done>
정적 검증이 전부 `*_OK` 를 출력한다: `NO_DIRECT_ASSIGN_OK`, `COPY_EXCLUDE_INTACT_OK`, `ADDDATUM_REWIRED_OK`,
`TWO_ARG_OVERLOAD_OK`, `SUPPRESS_GUARD_PRESENT_OK`, `NO_JSON_DESERIALIZE_OK`,
`TERNARY_OR_COALESCE_IN_NEW_CODE=0`, `CHANGED_FILES=[` 에 위 2개 파일만 포함.
하네스가 `MOVED_OK`, `SKIP_MISSING_OK`, `SKIP_COLLISION_OK`, `LOCKED_LOGGED_OK` 4줄을 모두 출력한다.
실패 항목이 있으면 Task 1 로 돌아가 고친 뒤 재검증한다(하네스 로직을 실코드에 맞추는 게 아니라, 실코드를 고친다).
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 3: 실기 PropertyGrid 개명 검증 (사람 확인)</name>
  <action>사용자가 아래 how-to-verify 절차를 실제 앱에서 수행한다. 자동화 불가 구간(SystemHandler 싱글턴 + PropertyGrid UI + 실제 레시피 폴더)이므로 Claude 가 대신 수행하지 않고 결과 보고를 기다린다.</action>
  <what-built>
`DatumName` 을 PropertyGrid 에서 바꾸면 패턴 모델 파일(`.shm`/`.ncm`, `_2` 페어 포함)이 새 이름 경로로 자동 이동한다.
INI 로드/신규 Datum 추가 경로에서는 발동하지 않도록 억제 가드를 넣었다.
빌드 PASS + 정적 회귀검증 + 파일이동 판정표 격리 하네스 4케이스 자동 검증까지는 완료된 상태다.
남은 건 실제 앱에서 싱글턴(레시피명/Shots/OwnerName) 경로가 맞물리는지 확인하는 것으로, 이건 자동화가 불가능하다.
  </what-built>
  <how-to-verify>
앱을 새로 빌드해 실행한 뒤 아래 4가지를 확인해 주세요.

**Test 1 — 개명하면 파일이 따라온다 (핵심)**
1. 패턴 위치보정(`IsPatternAlignEnabled` = true)을 켜고 **티칭이 완료된** Datum 을 하나 고릅니다(예: SIDE 의 Datum).
2. 탐색기로 그 레시피 폴더(예: `D:\Data\Recipe\FAI_1\SIDE\`)를 열어 `Datum<현재이름>.shm`(또는 `.ncm`) 파일이 있는 걸 눈으로 확인합니다.
3. PropertyGrid 에서 `DatumName` 을 다른 이름으로 바꿉니다(예: `Side_Datum_1` → `Side_Datum_TEST`).
4. 탐색기를 새로고침 → **옛 이름 파일이 사라지고 새 이름 파일이 생겼는지** 확인합니다.
5. 패턴2 를 쓰는 Datum 이면 `_2` 파일도 같이 옮겨졌는지 확인합니다.
6. Trace 로그에 `[DatumRename] 패턴 모델 파일 이동 완료` 가 남았는지 확인합니다.

**Test 2 — 개명 후 재티칭 없이 검사가 계속 된다 (이번 사고의 핵심 증상)**
1. Test 1 상태 그대로(재티칭하지 말고) 해당 Datum 의 **Test Find** 를 누릅니다 → 매칭 성공해야 합니다.
2. 이어서 그 Datum 에 걸린 FAI 를 검사합니다 → 개명 전과 동일하게 판정돼야 합니다(전항목 Fail 이 나오면 안 됩니다).
3. 이름을 원래대로 되돌려도 똑같이 동작하는지 확인합니다(파일도 원래 이름으로 돌아옵니다).

**Test 3 — 오발동이 없다 (데이터 파괴 방지, 가장 중요)**
1. 앱을 완전히 종료했다가 다시 켜고 **레시피를 로드**합니다.
2. 레시피 폴더의 `.shm`/`.ncm` 파일 이름과 개수가 **로드 전과 완전히 동일**한지 확인합니다(하나라도 옮겨졌으면 실패).
3. Datum 을 **새로 추가**(＋ 버튼)합니다 → 기존 Datum 들의 모델 파일이 그대로인지 다시 확인합니다.
4. Datum 을 **복사/붙여넣기** 해도 기존 파일이 그대로인지 확인합니다.

**Test 4 — 티칭 안 한 Datum 개명은 조용해야 한다**
1. 새로 추가한(=아직 티칭 안 한) Datum 의 이름을 바꿉니다.
2. 오류 팝업이 뜨지 않고, Error 로그에 `[DatumRename]` 이 남지 않아야 합니다(옛 파일이 없는 건 정상 케이스).

**참고 — 이번 범위 밖 (실패로 치지 마세요):**
`PatternEngine` 을 Shape↔NCC 로 바꾼 뒤 개명하면 옛 확장자 파일은 남습니다. 이건 다른 트리거라 이번 수정 대상이 아닙니다.
  </how-to-verify>
  <resume-signal>Test 1~4 결과를 알려주세요. 전부 통과면 "승인", 문제가 있으면 어떤 Test 에서 무엇이 달랐는지 알려주세요.</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| PropertyGrid 사용자 입력 → 파일시스템 | 사용자가 입력한 임의 문자열(`DatumName`)이 파일 경로 구성요소가 되어 `File.Move` 를 유발한다 |
| INI 레시피 파일 → 리플렉션 세터 | 디스크의 레시피 값이 세터를 통해 파일 이동 로직을 트리거할 수 있다 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-nrm-01 | Tampering | `DatumName` → 경로 문자열 | accept | 이름에 `..`/`\` 를 넣으면 경로 탈출이 가능하나, 단일 사용자 로컬 산업용 앱이고 동일 사용자가 이미 파일시스템 전권을 가진다. 기존 `GetPatternModelFilePath` 도 동일 특성이며 이번 변경이 노출면을 넓히지 않는다(같은 함수가 만든 경로만 사용) |
| T-nrm-02 | Denial of Service | 모델 파일 소실 | mitigate | 충돌 시 덮어쓰기 금지 + Error 로그(Task 1 판정표), 이동 실패 시 옛 파일 보존. Task 2 하네스 케이스 3/4 가 자동 검증 |
| T-nrm-03 | Tampering | INI 로드/AddDatum 오발동 | mitigate | `_suppressModelRename` 가드 + `InitializeDatumName` 세터 우회. Task 2 정적 검증 + Test 3 사람 확인 |
| T-nrm-04 | Repudiation | 조용한 실패 | mitigate | 모든 실패 분기가 `Logging.PrintErrLog` 로 `[DatumRename]` 흔적을 남긴다(원래 버그의 본질이 '모달 없는 조용한 실패' 였다) |
| T-nrm-05 | Denial of Service | 리졸버 예외로 개명 자체가 막힘 | mitigate | `TryResolveModelPathQuiet` 가 예외를 삼키고 null 반환 → 리네임만 포기하고 `DatumName` 변경은 항상 성립 |
</threat_model>

<verification>
1. Debug/x64 빌드 PASS(신규 error 0) — architecture_decision 의 직접 static 호출 가능성 증명 포함
2. 변경 파일 정확히 2개, 삭제 라인은 의도한 교체분뿐
3. 정적 회귀검증 7종 전부 `*_OK`(오발동 경로 0건, 복사 제외 유지, 2-arg 오버로드, 삼항/`??` 0건)
4. 격리 하네스 4케이스 전부 PASS(이동 / 없음-skip / 충돌-보존 / 잠김-로그)
5. 사람 실기 검증 Test 1~4(개명 후 파일 이동 · 재티칭 없이 검사 성립 · 로드/추가/복사 시 오발동 0 · 미티칭 개명 무소음)
</verification>

<success_criteria>
- 티칭된 Datum 을 개명하면 `.shm`/`.ncm` 과 `_2` 페어가 새 이름 경로로 실제 이동하고, 재티칭 없이 매칭이 계속 성공한다
- 레시피 로드 · Datum 추가 · Datum 복사에서는 파일이 단 하나도 움직이지 않는다
- 충돌/잠김/권한 실패 시 데이터가 파괴되지 않고 `[DatumRename]` Error 로그가 남으며, 이름 변경 자체는 성립한다
- 기존 검사/티칭 동작 회귀 0 (경로 계산 공식·리졸버·PatternMatchService·ParamBase 무변경)
</success_criteria>

<output>
완료 후 `.planning/quick/260806-nrm-datum-model-file-rename/260806-nrm-SUMMARY.md` 를 작성한다.
SUMMARY 에 반드시 포함할 것:
1. **오버로드 정정 사실** — 발주 요구사항 #7 은 1-arg 오버로드를 지시했으나 실제 코드에서 1-arg 는 `OwnerName` 을 쓰지 않고
   호출부도 0건이라, 티칭 저장 경로와 동일한 2-arg(`datum, OwnerName`)로 구현했다는 점과 그 근거
2. **아키텍처 결정 결과**(요구사항 #8) — `DatumConfig` → `InspectionSequence` 직접 static 호출로 컴파일 통과, 폴백 불필요
3. **발견된 추가 오발동 경로 2건**(INI 로드 리플렉션, `AddDatum`)과 각각의 가드 — 발주서에 없던 위험이므로 명시
4. **알려진 잔여 갭** — `PatternEngine` 전환 후 개명 시 옛 확장자 파일 잔존(범위 밖), Datum 삭제 시 고아(범위 밖)
5. 격리 하네스 4케이스 결과와 사람 UAT Test 1~4 결과
</output>
