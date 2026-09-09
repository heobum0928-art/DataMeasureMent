---
phase: quick-260909-kl0
plan: 01
subsystem: settings-io
tags: [system-settings, ini-recipe, load-defaults, reflection-serialization]

# Dependency graph
requires:
  - phase: none
    provides: n/a (standalone quick task on existing SystemSetting.Load/Save subsystem)
provides:
  - "Removal of dead SystemSetting.TestTimeOut property (0 consumers, was shown in settings window with no effect)"
  - "SystemSetting.Load() Int32 case now falls back to the property's current (code-default) value instead of 0 when the INI key is absent"
affects: [settings-window, ini-recipe-io, light-timing, align-coax-auto-off, log-retention]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Load() Int32 fallback: capture prop.GetValue(this) as valueIfInvalid before calling IniValue.ToInt(), so a missing key preserves the property's current value (constructor default on first load, in-memory value on settings-window re-Load) rather than the ToInt() default of 0"

key-files:
  created: []
  modified:
    - WPF_Example/Setting/SystemSetting.cs

key-decisions:
  - "Removed TestTimeOut outright rather than [Browsable(false)] — 0 consumers, no JSON serialization path (commented out), Load() iterates properties not INI keys so orphan keys are inert, Save() rewrites the whole file each time so the orphan key self-heals on next save. Different from the MeasCorrectionFactor precedent, which is deliberately hidden-but-alive."
  - "Only the Load() side of case \"Int32\" was changed; Save() case \"Int32\", case \"Boolean\", case \"Double\", case \"String\", and Custom/SystemSetting.cs AfterLoad() Restore* guards were left untouched, per plan scope."
  - "Fallback value is prop.GetValue(this) at call time, not a re-derived constructor default — this matters because SettingWindow.xaml.cs:26 calls Load() again every time the settings window opens; documented as a pre-existing latent bug that this fix incidentally also closes (see below)."

requirements-completed: [KL0-01, KL0-02]

# Metrics
duration: unknown (continuation summary — original execution window not captured by this session)
completed: 2026-09-09
---

# Quick Task 260909-kl0: 죽은 TestTimeOut 제거 + Load() Int32 폴백 결함 수정 Summary

**설정창에 노출돼 있었지만 아무도 읽지 않던 `TestTimeOut` 속성을 제거하고, `SystemSetting.Load()` 가 INI 에 int 키가 없을 때 코드 기본값 대신 0 을 덮어쓰던 결함을 고쳤다.**

## Background — 왜 이 작업이 필요했나

실기에서 사용자가 조명 타이밍 문제를 잡으려고 설정창의 `Test time out` 을 2000→3000 으로 올렸으나 아무 변화가 없었다 — 그 설정은 애초에 아무 코드도 읽지 않는 죽은 값이었다. 진짜 원인은 `LightSettleMs` 가 0 이었던 것이고, 그 0 은 사용자가 넣은 값이 아니라 **INI 키 부재 시 코드 기본값(100) 대신 0 이 대입되는 `Load()` 결함**의 결과였다. 같은 계열 피해가 `AlignCoaxAutoOffMs`(자동 소등 꺼짐), `LogDeleteDay`(로그 자동삭제 꺼짐)에도 이미 나 있었고, 지금까지는 `AfterLoad()` 에서 항목마다 개별 땜질(`RestorePcRoleDefault` 등)로 대응해 왔다. 이번 수정은 그 근본 지점을 한 번에 고친다.

## Task 1: 죽은 설정 TestTimeOut 제거

`WPF_Example/Setting/SystemSetting.cs` 에서 `[Category("System|Enviroment")]` + `public int TestTimeOut { get; set; } = 2000;` 두 줄을 삭제했다 (commit `2d42afb4`).

**제거 vs `[Browsable(false)]` 결정 근거:**
- **소비 코드 0건** — `grep -rn`(`*.cs`/`*.xaml`/`*.ini`/`*.json`) 결과 선언 1줄 외 매칭 없음. `bin/x64/{Debug,Release}/Setting.ini` 의 잔존 키 2건은 git 미추적 빌드 산출물이라 대상 아님.
- **다른 직렬화 경로 없음** — `SaveToJson()`/`LoadFromJson()` 은 파일 하단에서 통째로 주석 처리돼 있다. INI 가 유일한 직렬화 경로다.
- **INI 고아 키는 무해하다** — `Load()` 는 INI 키를 순회하지 않고 `GetType().GetProperties()` 로 **프로퍼티**를 돌며 `loadFile[group][name]` 을 조회한다. 속성이 사라지면 그 키는 읽히지 않고 예외도 나지 않는다.
- **고아 키는 다음 저장에서 자연 소멸한다** — `Save()` 는 매번 `new IniFile()` 을 만들어 현재 프로퍼티만 기록하고 파일 전체를 덮어쓴다(병합이 아니라 전체 치환).
- **`[Category]` 그룹 상태머신 누출 없음** — 바로 다음 프로퍼티 `LightSettleMs` 가 자기 `[Category("System|Enviroment")]` 를 갖고 있어 앞 프로퍼티의 그룹이 새어 들어갈 여지가 없다.
- **`MeasCorrectionFactor` 선례와 다른 이유** — 그 선례는 "값과 로직이 살아 있고 운용 정책상 숨김"(재노출 가능성이 설계에 포함)이라 `[Browsable(false)]` 가 맞았다. `TestTimeOut` 은 소비자가 0이라 되살릴 대상 자체가 없다.

보존 확인: `LIGHT_SETTLE_MS_DEFAULT` 상수와 `LightSettleMs` 의 `[Category("System|Enviroment")]` 는 그대로 남아 있다(verify 게이트로 확인).

## Task 2: Load() Int32 케이스 폴백 수정

`Load()` 의 `case "Int32":` 를 다음과 같이 바꿨다(`Save()` 쪽 `case "Int32":` 는 무변경, commit `25c3375f`):

```csharp
case "Int32":
    // 키가 없으면 IniValue.Default(Value == null) 라 ToInt() 기본값 0 이 코드 기본값을 덮어쓴다 — 현재 값을 폴백으로 넘겨 방지한다.
    int nCodeDefault = (int)prop.GetValue(this);
    int iValue = loadFile[group][name].ToInt(nCodeDefault);
    prop.SetValue(this, iValue);
    break;
```

**결함 메커니즘:**
`IniFile this[string section]` 은 섹션이 없으면 빈 섹션을 새로 반환하고(예외 없음), `IniSection this[string name]` 은 키가 없으면 `IniValue.Default`(`Value == null`)를 반환한다. `IniValue.ToInt(int valueIfInvalid = 0)` 은 변환 실패 시 `valueIfInvalid` 를 반환하는데, 인자 없이 호출하면 그 값이 0 이다. 그 0 이 `prop.SetValue` 로 코드 기본값을 덮어쓰고, 이어지는 `Save()` 가 0 을 디스크에 그대로 기록해 결함을 고착시켰다.

**`Load()` 재호출 경로의 의미 — 함께 해소된 잠복 결함:**
`Load()` 는 생성자 외에 **`WPF_Example/UI/Setting/SettingWindow.xaml.cs:26`(`pSetting.Load()`)에서 설정창을 열 때마다 다시 호출된다.** 이번 수정을 적용하면 그 재호출 시 보존되는 값은 "코드 기본값"이 아니라 **호출 시점의 메모리 값**이다. 이 동작이 타당한 이유:
- 수정 전 코드에서는 같은 상황에서 그 값이 **0 으로 초기화된다** — 즉 "INI 에 키가 없는 int 설정은 설정창을 열기만 해도 0 으로 리셋된다"는 잠복 결함이 이미 있었다. 이번 수정은 그 재현 경로도 함께 막는다(메모리 값 보존은 그보다 항상 안전하다).
- INI 에 키가 있는 정상 상태에서는 INI 값이 그대로 이긴다 — 동작 불변.
- 한 번 `Save()` 하고 나면 모든 int 키가 파일에 존재하므로 이 재호출 경로는 과도기(오래된/부분적 INI)에만 의미가 있다.

**`AfterLoad()` 복원 가드 — 무변경 확인:**
`Custom/SystemSetting.cs` 의 `RestorePcRoleDefault`, `RestoreAlignVerifyDefaults` 등은 diff 0 으로 그대로 유지했다. 키 부재 시엔 이제 0 이 아니라 코드 기본값이 들어오므로 가드는 조건 불성립 → no-op(결과 동일). 키가 있고 값이 0 인 경우엔 기존과 100% 동일하게 가드가 복원한다 — 가드를 지웠다면 이미 0 이 기록된 기존 INI 에서의 복원 동작이 사라져 회귀였을 것이다.

## 이번 수정의 한계

**이미 INI 에 `0` 으로 기록돼 있는 값은 소급 복구되지 않는다.** 이번 수정은 "INI 에 키가 **없을** 때"만 효과가 있다 — 키가 있고 그 값이 0 이면 (그 0 이 과거 결함으로 생긴 것이든 사용자가 의도적으로 넣은 것이든 구분할 수 없으므로) 그대로 로드된다. 실기 `D:\Data\Setting.ini` 의 `AlignCoaxAutoOffMs=0`, `LogDeleteDay=0` 이 정확히 이 케이스였다 — 자동 교정은 사용자가 설정창에서 직접 정한 값을 덮어쓸 위험이 있어 범위에서 제외했다. (오케스트레이터가 사용자 지시로 실기 INI 를 `AlignCoaxAutoOffMs=0→3000`, `LogDeleteDay=0→30` 으로 직접 수정했다 — 아래 "실기 UAT 결과" 참고. 이는 사용자 판단에 의한 INI 직접 교정이며 이번 커밋의 코드 변경과는 무관하다.)

## 범위 밖 발견 (수정 안 함)

같은 `switch` 안에 동일 계열 결함이 두 곳 더 있으나 이번 범위 밖이라 손대지 않았다:
- **`case "Double":`** — `IniValue.ToDouble(double valueIfInvalid = 0)` 도 인자 없이 호출돼 키 부재 시 0.0 이 코드 기본값을 덮어쓴다. `RestoreEthernetVisionDefault`/`RestoreCalibSearchDefault`/`RestoreAlignVerifyDefaults` 가 항목별로 이미 땜질 중이다.
- **`case "String":`** — `IniValue.ToString()` 이 `Value` 를 그대로 돌려주므로 키 부재 시 **null** 이 대입된다. `RestoreDataPathDefaults` 가 땜질 중이며, `isDirectory == true` 인데 null 이면 `Directory.CreateDirectory(null)` 이 예외를 던지고 `try/catch` 가 삼킨다.
- **`case "Boolean":`** — 이것은 결함이 아니라 **의도된 하위호환 설계**(코드 내 주석에 명시)다. 누락 INI 키 → false 를 유지해야 하므로 절대 손대지 않았다 — 이번 수정에서도 diff 0.

## 문서 정정 후보 (별건, 이번에 수정 안 함)

`VersionDefine.cs` 의 v1.7.30.0 changelog 가 `AlignCoaxAutoOffMs` 의 "설정 파일에 항목이 없어 0 으로 시작한다"는 사실을 **정상 동작처럼** 기록했으나, 실제로는 이번에 고친 `Load()` int 로드 결함의 **증상**이었다. 버전 changelog 정정/버전 bump 는 이 작업 범위 밖이며 별도 문서 작업으로 남긴다.

## Task Commits

1. **Task 1: 죽은 설정 TestTimeOut 제거** — `2d42afb4` (fix)
2. **Task 2: Load() Int32 케이스 코드 기본값 폴백** — `25c3375f` (fix)

## Files Modified

- `WPF_Example/Setting/SystemSetting.cs` — TestTimeOut 프로퍼티 제거(3줄) + `Load()` `case "Int32":` 폴백 추가(3줄)

## Verification (직전 실행자 확인 완료)

- Debug|x64 MSBuild `error CS` 0건.
- 하드룰 grep 5종(`\?[^?]*:`, `??`, `?.`, `switch.*=>`, `hbk`) 추가 라인 전부 0건.
- `git diff --stat -- WPF_Example/Custom/SystemSetting.cs WPF_Example/DatumMeasurement.csproj` 출력 비어 있음 — 두 파일 무변경.
- `ToInt(` 인자 없는 호출 잔존 0건, `case "Boolean":` 무변경.

## 실기 UAT 결과 (오케스트레이터 수행, Task 3)

**확인됨 (사용자 육안 확인):**
- 설정창 System 탭에서 `Test time out` 항목이 **사라진 것을 사용자가 확인**했다.

**확인하지 못함 — 검증 수단 부재로 미검증 상태로 남음:**
"INI 에서 `LightSettleMs` 키를 지우고 기동 → 100 으로 로드"되는지를 확인하지 못했다. 이유 두 가지:
1. 설정창이 ADMIN 로그인 게이트(`MainView.xaml.cs:153`, `Grade >= EAccountGrade.Admin`)라 오케스트레이터가 직접 값을 눈으로 볼 수 없었다.
2. 우회로 "키 삭제 → 기동 → 종료 시 저장되는 값 확인"을 시도했으나, **앱이 종료 시 `Setting.ini` 를 자동 저장하지 않는다**(파일 수정 시각이 변하지 않음을 확인) — 관측 수단 자체가 없었다.

따라서 이 항목의 현재 근거는 **코드 diff + Debug|x64 빌드 통과 + plan-checker 의 소스 대조 검증**까지이며, 런타임 확인은 되지 않았다. 다음에 새 int 설정이 추가되거나 관리자 계정으로 재검증할 기회가 있을 때 자연히 드러날 것이다.

**참고 — 실기 설정 상태 (오케스트레이터가 사용자 지시로 변경, 이번 코드 변경과 무관):**
`D:\Data\Setting.ini` 를 `AlignCoaxAutoOffMs=0→3000`, `LogDeleteDay=0→30` 으로 수동 변경했다. `LightSettleMs=100` 은 유지. 이 두 값은 **이번 결함의 피해자**였다(코드 기본값이 3000/30 인데 과거 결함으로 0 이 기록돼 있었다). 소급 교정은 코드가 아니라 사용자 판단으로 INI 를 직접 고친 것이다.

## Deviations from Plan

None - 두 태스크 모두 계획대로 실행됐다. Task 3(실기 UAT) 은 checkpoint 이며 위 절차대로 부분 확인 후 진행됐다.

## Issues Encountered

없음. 유일한 마찰은 Task 3 검증 수단 부재(ADMIN 게이트 + 종료 시 미저장)였으며, 이는 코드 결함이 아니라 검증 환경의 한계로 SUMMARY 에 미검증으로 명시했다.

## User Setup Required

없음 — 외부 서비스 설정, 신규 NuGet 패키지 없음.

## Next Phase Readiness

- 코드 변경 완료·커밋·빌드 클린. 후속 조치 없음(이 작업 자체는 완결).
- 파생 항목 두 가지가 향후 참고용으로 남아 있다: (1) `case "Double"`/`case "String"` 의 동일 계열 결함(범위 밖, 위 기록), (2) `VersionDefine.cs` v1.7.30.0 changelog 문구 정정(별건, 위 기록).
- `LightSettleMs=100 실기 로드` 항목은 다음 관리자 세션에서 재검증 기회가 있으면 확인 권장.

---
*Phase: quick-260909-kl0*
*Completed: 2026-09-09*

## Self-Check: PASSED
