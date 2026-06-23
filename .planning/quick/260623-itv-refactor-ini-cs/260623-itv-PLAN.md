---
phase: quick-260623-itv
plan: 01
type: execute
wave: 1
depends_on: []
files_modified: [WPF_Example/Utility/Ini.cs]
autonomous: false
requirements: [QUAL-01]
must_haves:
  truths:
    - "공개 API(struct/class/메서드/프로퍼티/파라미터) 시그니처가 리팩토링 전과 100% 동일하다"
    - "INI Load/Save 직렬화 동작이 비트 단위로 동일하다"
    - "TryParseCircle/Line/Rect 의 좌표 필드 개수 상수가 의미있는 const로 선언된다"
    - "msbuild Debug/x64 빌드가 0 errors 로 통과한다"
  artifacts:
    - path: "WPF_Example/Utility/Ini.cs"
      provides: "컨벤션 적용된 INI 라이브러리 (기능 동치)"
      contains: "struct IniValue"
  key_links:
    - from: "IniValue.TryConvertCircle/Line/Rect"
      to: "TryParseCircle/Line/Rect"
      via: "내부 호출 (시그니처 불변)"
      pattern: "TryParse(Circle|Line|Rect)"
---

<objective>
WPF_Example/Utility/Ini.cs (985줄, 오픈소스 INI 라이브러리 포트)에 .planning/CONVENTIONS.md 컨벤션을 **안전 범위 내에서** 적용한다.

Purpose: QUAL-01 점진적 리팩토링. 공개 API는 외부에서 광범위하게 소비(레시피 INI 로드 전체)되므로 절대 변경 금지. 기능 동치성이 컨벤션 준수보다 우선.
Output: 컨벤션이 부분 적용된 Ini.cs (빌드 PASS, 동작 100% 보존)
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@.planning/CONVENTIONS.md
@WPF_Example/Utility/Ini.cs

<constraints>
## 절대 변경 금지 (기능 동치성 최우선)
- public/struct/class 시그니처, 메서드명, 프로퍼티명, 파라미터명·순서·타입
- IDictionary<,>/ICollection<,>/IEnumerable<,>/IDisposable 명시적 인터페이스 구현 시그니처
- implicit/explicit operator 오버로드 전부
- IniValue.Default, DefaultComparer 등 public static 멤버
- Save/Load/LoadValue 의 직렬화·파싱 동작 (출력·파싱 비트 단위 동일)
- 예외 메시지 문자열 (Ordered 관련 InvalidOperationException/IndexOutOfRangeException 등) — 한 글자도 바꾸지 말 것
- #if JS 전처리 블록 — 그대로 둘 것

## 코드 스타일
- 현재 파일 스타일 = K&R (여는 중괄호 같은 줄). 유지할 것.
- C# 7.2 한정: switch expression / nullable ref types / record 금지
- 주석 정책: 대규모 리팩토링이므로 전 줄 //YYMMDD hbk 주석은 부적절. 파일 상단에 변경 요약 주석 1줄(//260623 hbk: 컨벤션 적용 — 지역변수/매직넘버 const화, 공개 API 불변)만 추가.

## 안전 원칙
의심스러우면 변경하지 말 것. "안전한 리팩토링 > 완벽한 컨벤션 준수".
중복 구조(TryParseCircle/Line/Rect 반복 파싱)는 리스크 크면 보류 — 별도 헬퍼 추출은 금지(시그니처/동작 변경 위험). 각 메서드 내부 정리만 수행.
</constraints>
</context>

<tasks>

<task type="auto">
  <name>Task 1: IniValue 좌표 파서 컨벤션 적용 (매직넘버 const + 지역변수 정리)</name>
  <files>WPF_Example/Utility/Ini.cs</files>
  <action>
IniValue struct 내부 TryParseCircle / TryParseLine / TryParseRect 세 메서드에만 집중. 공개 시그니처(메서드명/파라미터/반환타입) 불변.

1. 매직넘버 const화 — IniValue struct 상단(첫 private 메서드 위)에 const 선언 추가:
   `private const int CIRCLE_FIELD_COUNT = 3;`
   `private const int LINE_FIELD_COUNT = 4;`
   `private const int RECT_FIELD_COUNT = 4;`
   그리고 좌표 인덱스 0/1/2/3에 대한 const:
   `private const int FIELD_X = 0;`
   `private const int FIELD_Y = 1;`
   `private const int FIELD_W = 2;   // Circle: radius`
   `private const int FIELD_H = 3;`
   - TryParseCircle 의 `strArray.Length < 3` → `< CIRCLE_FIELD_COUNT`
   - TryParseLine 의 `strArray.Length < 4` → `< LINE_FIELD_COUNT`
   - TryParseRect 의 `strArray.Length < 4` → `< RECT_FIELD_COUNT`
   - strArray[0/1/2/3] 인덱스 → strArray[FIELD_X], strArray[FIELD_Y], strArray[FIELD_W], strArray[FIELD_H] (Circle은 0/1/2만 사용)

2. 지역변수 점진적 헝가리언 + 중간변수 제거 (가독성 우선, 과하지 않게):
   - `string[] strArray` → `string[] szParts`
   - 현재 패턴: `if (!TryParseDouble(strArray[0], out value)) return false; double x = value;` 처럼 공유 `double value` 에 받고 즉시 별도 변수로 복사하는 2단계 구조 → 좌표별 `double` 지역변수에 직접 out 받는 1단계로 정리.
     예) `double dX; if (!TryParseDouble(szParts[FIELD_X], out dX)) return false;`
     이후 circle.CenterX = dX; 식으로 직접 대입. 공유 `double value = 0` 변수 제거.
   - TryParseRect 의 잘못된 들여쓰기(line 74 부근 `double y = value;` 뒤 공백 라인) 정돈.

3. early-return: 이미 early-return 패턴이 잘 적용됨. `if (strArray == null) return false;` 는 string.Split 결과가 null이 될 수 없으나 기존 동작 보존을 위해 **삭제하지 말고 그대로 유지**(방어 코드, 동작 변경 위험 회피).

4. 좌표 매핑 why 주석(비자명한 부분에만 최소):
   - TryParseLine: `// szParts: X1,Y1,X2,Y2 순서` (W/H 변수명이 실제로는 X2/Y2 끝점이라 혼동 소지 → 1줄)
   - 자명한 곳엔 주석 금지.

세 메서드 외(IniValue의 ToXxx/TryConvertXxx/operator, IniFile, IniSection) 는 이번 task에서 건드리지 말 것 — 시그니처 위험 + 이미 early-return 깔끔.
  </action>
  <verify>
  <automated>grep -n "CIRCLE_FIELD_COUNT\|LINE_FIELD_COUNT\|RECT_FIELD_COUNT" WPF_Example/Utility/Ini.cs</automated>
  </verify>
  <done>
  - 3개 FIELD_COUNT const 선언 존재, 매직넘버 3/4 리터럴이 TryParse 3종에서 사라짐
  - 공개 메서드 시그니처(TryParseCircle/Line/Rect, ToCircle/Line/Rect, TryConvertXxx) 불변
  - K&R 스타일 유지, C# 7.2 호환
  </done>
</task>

<task type="auto">
  <name>Task 2: 파일 헤더 변경 요약 주석 + 빌드 검증</name>
  <files>WPF_Example/Utility/Ini.cs</files>
  <action>
1. 파일 상단 using 블록 위 또는 namespace 선언 직전에 변경 요약 주석 1줄 추가:
   `//260623 hbk: CONVENTIONS 적용 — IniValue 좌표 파서 지역변수/매직넘버 const화. 공개 API·직렬화 동작 불변.`

2. 빌드 검증 (반드시 0 errors). 프로젝트 표준 빌드 커맨드 실행:
   `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64`
   - 이 PC의 MSBuild 후보 경로(우선순위): VS2022 Community
     `/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe`
     (없으면 VS2017: `/c/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/MSBuild.exe`)
   - 빌드 실패 시 직전 변경을 검토하여 수정 (시그니처/문법 오류). const 선언 위치·접근자 확인.
   - operator/인터페이스 구현/예외 문자열은 손대지 않았는지 재확인.
  </action>
  <verify>
  <automated>cd /c/Info/Project/DataMeasurement && "/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj //p:Configuration=Debug //p:Platform=x64 //nologo //verbosity:minimal 2>&1 | grep -iE "error|Build succeeded|0 Error"</automated>
  </verify>
  <done>
  - 빌드 결과 "0 Error(s)" / "Build succeeded"
  - 헤더 변경 요약 주석 1줄 존재
  </done>
</task>

</tasks>

<verification>
- 빌드: msbuild Debug/x64 → 0 errors (Task 2 verify)
- 공개 표면 불변 확인 (수동/grep): struct IniValue / class IniFile / class IniSection 의 public 멤버 시그니처가 git diff 상 변경 없음
  `git diff WPF_Example/Utility/Ini.cs` 검토 시 변경이 TryParseCircle/Line/Rect 내부 + const 선언 + 헤더 주석에 국한
- operator 오버로드, 인터페이스 명시 구현, 예외 메시지 문자열 라인은 diff에 등장하지 않아야 함
</verification>

<success_criteria>
- TryParseCircle/Line/Rect 의 매직넘버(3/4, 인덱스 0~3)가 의미있는 const로 대체됨
- 좌표 파서 지역변수 점진적 헝가리언 적용(szParts/dX 등), 불필요한 공유 value 중간변수 제거
- 공개 API·직렬화·파싱·예외 동작 100% 동일 (기능 동치)
- msbuild Debug/x64 0 errors
- K&R 스타일·C# 7.2 유지
</success_criteria>

<output>
After completion, create `.planning/quick/260623-itv-refactor-ini-cs/260623-itv-SUMMARY.md` summarizing: 변경된 메서드(3종), 추가 const, 빌드 결과, 공개 API 불변 확인.
</output>
