---
phase: quick-260909-ktj
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
autonomous: false
requirements:
  - QUICK-260909-KTJ
user_setup: []

must_haves:
  truths:
    - "사용자가 PropertyGrid 에서 Datum 이름을 바꾸면, 같은 시퀀스가 소유한 Shot 의 측정 중 DatumRef 가 옛 이름과 정확히 일치하는 것만 새 이름으로 갱신된다."
    - "기존 레시피를 로드하기만 했을 때는 어떤 측정의 DatumRef 도 바뀌지 않는다 (INI 로드 오발동 0건)."
    - "빈 DatumRef(무보정 의도) 측정과, 다른 이름을 가리키는 DatumRef 는 절대 건드리지 않는다."
    - "옛 이름 또는 새 이름이 같은 시퀀스 안에서 모호하면(다른 Datum 이 그 이름을 이미 보유) 갱신을 포기하고 Error 로그만 남긴다."
    - "Owner 가 null 이면 DatumRef 갱신은 조용히 생략되고, 패턴 모델 파일 이동은 종전대로 수행된다."
    - "갱신 건수(0건 포함)가 Trace 로그로 남아 사용자가 개명이 따라갔는지 확인할 수 있다."
    - "개명 후 레시피를 저장하고 앱을 재기동해도 갱신된 DatumRef 가 유지된다."
    - "Action_FAIMeasurement 의 DATUM_REF_MISSING 안전망은 그대로 살아 있다."
  artifacts:
    - "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs — UpdateMeasurementDatumRefsAfterRename 메서드"
    - "WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs — DatumName 세터 안 호출 지점"
  key_links:
    - "DatumConfig.DatumName 세터의 shouldRename 가드 → 새 DatumRef 갱신 호출 (가드 안쪽이어야 INI 로드 오발동이 막힌다)"
    - "DatumConfig.Owner (as InspectionSequence) → 소유 시퀀스 도달 경로"
    - "InspectionSequence.IsShotOwnedBySequence(shot, Name) → 시퀀스 경계 판정 단일 소스"
    - "MeasurementBase.DatumRef (public string) → ParamBase 리플렉션 INI 직렬화로 자동 영속"
---

<objective>
Datum 이름을 바꾸면 그 Datum 을 참조하던 측정들의 `DatumRef` 도 함께 따라가게 한다.

Purpose: 지금은 패턴 모델 파일(.shm/.ncm)만 따라가고 `DatumRef` 는 안 따라간다. 저장소 전체에서
`DatumRef` 에 값을 쓰는 코드가 0건이라, 개명 즉시 그 Datum 을 참조하던 측정이 전부 미아가 되어
`SkipReason.DATUM_REF_MISSING` 으로 skip(NG) 된다. 실기 레시피 기준 `Side_Datum_3` 하나만 바꿔도
측정 40건이 통째로 죽는다.

Output: `InspectionSequence` 에 개명 전파 메서드 1개 + `DatumConfig.DatumName` 세터 안 호출 1줄.
신규 파일 없음(classic MSBuild csproj 등록 회피). 두 파일 모두 이미 csproj 에 등록되어 있다.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@C:/code/DataMeasurement/CLAUDE.md
@C:/code/DataMeasurement/.planning/quick/260909-ktj-datum-datumref/260909-ktj-PLAN.md

주요 소스 (편집 전 라인번호 재확인 필수):
@C:/code/DataMeasurement/WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
@C:/code/DataMeasurement/WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
</context>

<investigation_findings>
아래는 플래너가 코드로 직접 확인한 사실이다. 실행자는 이 판단을 그대로 따르되, 라인번호만 재확인한다.

## 1. 억제 플래그 — 기존 `_suppressModelRename` 재사용이 정답 (신규 플래그 불필요)

`DatumConfig.DatumName` 세터를 때리는 경로를 전수 조사한 결과:

| 경로 | 세터 통과 여부 | 현재 보호 |
|------|----------------|-----------|
| PropertyGrid 사용자 편집 | 통과 | 없음 (= 유일한 진짜 개명) |
| `ParamBase.Load` 리플렉션 SetValue | 통과 | `DatumConfig.Load` override 가 `_suppressModelRename = true` 로 감싼다 (약 :1316~1330) |
| `CopyPublicPropertiesTo` (붙여넣기) | **통과 안 함** | `_copyExclude` 에 `"DatumName"` 포함 (약 :1357) |
| `InspectionSequence.AddDatum` (신규 추가) | **통과 안 함** | `InitializeDatumName` 이 세터를 우회 (약 :58) |

즉 모델 파일 이동이 위험한 경로 집합과 DatumRef 갱신이 위험한 경로 집합이 **완전히 동일**하다.
별도 플래그를 만들면 두 곳을 따로 켜고 끄는 실수 여지만 생긴다. 기존 `shouldRename` 지역 불리언
(= `!_suppressModelRename && 옛이름 비어있지 않음 && 새이름 비어있지 않음`) 을 그대로 재사용한다.

부수 확인: 레시피 전체 로드 순서상 `InspectionRecipeManager` 는 `ClearShots()` 이후에 Datum 을
로드하므로 로드 시점 Shots 는 비어 있다. 그래도 이 순서에 기대지 않는다 — `LoadFixtureForSequence`
같은 부분 로드 경로가 언제든 순서를 바꿀 수 있으므로 플래그 가드가 유일한 방어선이다.

## 2. 시퀀스 경계 — 같은 시퀀스 내부로 한정한다

런타임 해석은 전부 시퀀스 스코프다:
- `Action_FAIMeasurement` 은 `ShotParam.Parent as InspectionSequence` 로 부모 시퀀스를 잡고(약 :514),
  `parentSeq2.IsDatumRefUnresolvable` / `ResolveDatumTransform` 둘 다 그 시퀀스의 `DatumConfigs` 만 본다.
- `InspectionSequence.FindDatumByName`(약 :2516) 도 인스턴스 `DatumConfigs` 한정.

따라서 다른 시퀀스의 측정을 건드리면 **오히려 오작동**이다 (다른 시퀀스가 같은 이름의 Datum 을
정당하게 가질 수 있다). Shot 소유 판정은 이미 존재하는 단일 진입점
`InspectionSequence.IsShotOwnedBySequence(shot, szSeqName)`(public static, 약 :599) 을 재사용한다 —
빈 `OwnerSequenceName` 의 TOP 폴백까지 다른 소비자(`ComputeOverallResult`, `TryGetOwnedShotZIndexRange`,
`BatchRunService`, `RepeatRunService`)와 일관되게 움직여야 하기 때문이다.

단, 그 메서드는 `szSeqName` 이 비면 "전체 매칭"(레거시 과대판정 = 읽기에는 안전) 계약이다.
**쓰기(mutation)에는 위험하므로** 시퀀스 `Name` 이 비어 있으면 갱신 자체를 포기한다.

## 3. 이름 충돌 — 검증이 아예 없다. 모호하면 갱신하지 않는다

`AddDatumToSequence`(InspectionListView 약 :1509)는 사용자가 입력한 이름을 무검증으로 받고,
PropertyGrid 개명에도 중복 검사가 없다. 즉 같은 시퀀스에 동명 Datum 이 존재할 수 있다.

이 상태에서 DatumRef 를 갱신하면:
- 새 이름이 다른 Datum 과 겹치면 → 그 측정들이 조용히 남의 Datum 에 붙는다 (오검, 최악).
- 옛 이름을 다른 Datum 도 갖고 있었다면 → 그 Datum 의 측정을 훔쳐온다.

정책: **둘 중 하나라도 해당하면 갱신하지 않고 Error 로그만 남긴다.** 갱신을 포기하면 기존
`DATUM_REF_MISSING` 안전망이 그대로 작동해 시끄럽게 NG 를 내므로 조용한 오검보다 안전하다.
이는 모델 파일 이동의 기존 판정표("새 경로에 파일 존재 → 덮어쓰지 않고 Error 로그", 약 :78~84)와
같은 철학이다. 중복 이름 자체를 막는 검증 신설은 이번 범위 밖이다.

## 4. 영속성 / 소급 교정
- `MeasurementBase.DatumRef` 는 `public string` 이라 `ParamBase` 리플렉션이
  `[SHOT_{s}_FAI_{f}_MEAS_{m}]` 섹션으로 자동 저장한다(`InspectionRecipeManager` 약 :238~245).
  별도 저장 코드 불필요.
- 이미 미아가 된 기존 레시피의 소급 교정 마이그레이션은 **넣지 않는다**(범위 밖). 앞으로의 개명에만 적용된다.

## 5. 알려진 한계 (SUMMARY 에 기록할 것)
- `DatumRef` 는 `RaisePropertyChanged` 를 발화하지 않는 단순 auto-property 라, 갱신 순간 열려 있던
  측정 PropertyGrid 는 즉시 새 값으로 다시 그려지지 않는다. 노드를 다시 선택하면 보인다. 데이터는 이미 옳다.
- 검사 실행 중 개명은 기존에도 보호되지 않는 영역이다(모든 레시피 편집이 동일 노출). 이번 변경이
  새로운 종류의 경합을 만들지 않으므로 락은 추가하지 않는다.
</investigation_findings>

<tasks>

<task type="tracer">
  <name>Task 1: Datum 개명 → 측정 DatumRef 전파 (end-to-end)</name>
  <files>WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs, WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs</files>
  <precondition>`tasklist | grep -i DatumMeasurement` 결과가 비어 있어야 한다 (앱 실행 중이면 빌드가 파일 잠금으로 실패한다). 실행 중이면 **강제 종료하지 말고** 사용자에게 보고하고 대기할 것.</precondition>
  <read_first>
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs 약 :17~53 (DatumName 세터 + 억제 플래그 선언), 약 :254~270 (Owner 를 InspectionSequence 로 캐스팅하는 선례), 약 :1316~1332 (Load override 의 억제 구간)
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs 약 :592~622 (IsShotOwnedBySequence), 약 :672~700 (SystemHandler/Sequences/RecipeManager 3단 null 가드 선례), 약 :2453~2526 (IsDatumRefUnresolvable / ResolveShotGrabMirror / FindDatumByName 클러스터)
  </read_first>
  <action>
InspectionSequence.cs 에 공개 메서드 하나를 신설하고, DatumConfig.cs 세터에서 그것을 호출한다.

### (A) InspectionSequence.cs — 신설 메서드

배치: `FindDatumByName`(약 :2516) 바로 다음, `MarkAlignFailed` 앞. DatumRef 해석 헬퍼들이 모여 있는
자리라 응집도가 맞다. 이 구역의 중괄호 스타일은 Allman 이므로 그대로 따른다.

시그니처:
`public void UpdateMeasurementDatumRefsAfterRename(DatumConfig renamedDatum, string szOldName, string szNewName)`

동작 순서 (전부 가드 절, 중첩 금지, 한 줄 분기라도 중괄호 필수):

1. `renamedDatum` 이 null 이면 return.
2. `szOldName` 또는 `szNewName` 이 `string.IsNullOrEmpty` 면 return.
3. `string.Equals(szOldName, szNewName, StringComparison.Ordinal)` 이면 return.
4. `string.IsNullOrEmpty(Name)` 이면 return. — 이유를 주석으로 남길 것:
   `IsShotOwnedBySequence` 는 시퀀스명이 비면 "전체 매칭"을 반환하는 계약이라 읽기에는 안전하지만
   쓰기에는 전 시퀀스를 갈아엎게 되므로 여기서 차단한다.
5. 모호성 검사. 이름 있는 불리언 `bOldNameStillUsed`, `bNewNameCollides` 를 false 로 초기화하고
   `DatumConfigs` 를 한 번 순회한다(`DatumConfigs` null 가드 포함). 각 원소 `d` 에 대해
   d 가 null 이면 continue, `object.ReferenceEquals(d, renamedDatum)` 이면 continue.
   `string.Equals(d.DatumName, szOldName, StringComparison.Ordinal)` 이면 `bOldNameStillUsed = true`.
   `string.Equals(d.DatumName, szNewName, StringComparison.Ordinal)` 이면 `bNewNameCollides = true`.
   둘 중 하나라도 true 면 `Logging.PrintErrLog((int)ELogType.Error, ...)` 로
   "[DatumRename] 같은 시퀀스에 동명 Datum 이 있어 측정 참조(DatumRef)를 갱신하지 않았다" 취지의
   메시지(옛 이름 / 새 이름 / 시퀀스 Name 포함)를 남기고 return.
   조건식에 논리 연산자를 3개 이상 늘어놓지 말고 위처럼 이름 있는 불리언으로 선추출할 것.
6. 레시피 접근. `SystemHandler.Handle` → `.Sequences` → `.RecipeManager` → `.Shots` 를 각각
   별도 지역변수 + 별도 null 가드로 받는다(약 :672 `TryGetOwnedShotZIndexRange` 의 3단 가드와 동일
   형태). 어느 하나라도 null 이면 로그 없이 return — 앱 초기화 이전이나 레시피 미로드 시점의 정상 케이스다.
7. `int nUpdated = 0;` 후 3중 foreach 순회:
   - `foreach (ShotConfig shot in ...Shots)` — shot 이 null 이면 continue.
   - `InspectionSequence.IsShotOwnedBySequence(shot, Name)` 결과를 `bOwnedByThisSeq` 에 담아
     false 면 continue. **이 소유 판정 로직을 여기서 다시 구현하지 말 것.**
   - `shot.FAIList` 가 null 이면 continue.
   - `foreach (FAIConfig fai in shot.FAIList)` — fai 가 null 이거나 `fai.Measurements` 가 null 이면 continue.
   - `foreach (MeasurementBase meas in fai.Measurements)` — meas 가 null 이면 continue.
     `string.IsNullOrEmpty(meas.DatumRef)` 이면 continue (무보정 의도 보존).
     `string.Equals(meas.DatumRef, szOldName, StringComparison.Ordinal)` 이 false 면 continue
     (다른 이름을 가리키는 참조 보존).
     그 외에는 `meas.DatumRef = szNewName;` 후 `nUpdated++`.
8. 마지막에 항상 `Logging.PrintLog((int)ELogType.Trace, ...)` 로 갱신 결과를 남긴다.
   0건일 때도 반드시 남길 것 — 사용자가 "참조가 원래 없었다" 와 "기능이 안 돌았다" 를 구분해야 한다.
   메시지에 시퀀스 Name, 옛 이름, 새 이름, `nUpdated` 를 모두 포함하고 태그는 기존과 동일하게
   `[DatumRename]` 을 쓴다.

이 파일의 `using System;` 및 `ReringProject.Setting`(ELogType), `ReringProject.Utility`(Logging) 은
이미 선언되어 있으므로 using 추가는 불필요하다. 메서드 앞에 이 변경의 "왜"(개명 시 참조가 미아가 되어
전 측정이 DATUM_REF_MISSING 으로 skip 되던 결함)를 3~5줄 주석으로 남길 것. 날짜/이니셜 접두 주석은 쓰지 않는다.

### (B) DatumConfig.cs — 세터에서 호출

위치: `DatumName` 세터(약 :30~53) 안, 기존 `if (shouldRename) { ... }` 블록(약 :45~50)의
`MoveModelFileIfPresent` 두 줄 **다음**, 같은 블록 안. `RaisePropertyChanged` 호출보다 앞이다.
이 파일 이 구역의 중괄호 스타일은 K&R 이므로 그대로 따른다.

내용: `InspectionSequence` 로 Owner 를 캐스팅해 지역변수로 받고(약 :264 의
`InspectionSequence owner = Owner as InspectionSequence;` 선례와 동일 형태 — 다만 이 블록에는
`oldName` 지역변수가 이미 있으므로 이름 충돌이 없도록 `ownerSeq` 같은 별도 이름을 쓸 것),
null 이면 아무것도 하지 않는다(모델 파일 이동은 이미 위에서 끝났으므로 영향 없음).
null 이 아니면 `ownerSeq.UpdateMeasurementDatumRefsAfterRename(this, oldName, value);` 를 호출한다.

**반드시 `shouldRename` 블록 안쪽이어야 한다.** 블록 밖에 두면 INI 로드 중 리플렉션이 세터를 때릴 때
갱신이 발동해 멀쩡한 레시피를 망가뜨린다 — 모델 파일 사고와 정확히 같은 함정이다.

기존 지역변수 `shouldRename` 은 이름을 바꾸지 말 것(무관한 diff 확산 금지). 대신
`_suppressModelRename` 필드 선언부(약 :19~23) 주석에 "이제 모델 파일 이동과 측정 DatumRef 전파
두 효과를 함께 게이트한다"는 한 줄을 덧붙인다.

### 하드룰
삼항 연산자 / null 병합 / null 조건 연산자 / C# 8 switch 식 전부 금지 — 명시적 if/else 로 쓴다.
한 줄짜리 분기도 중괄호를 생략하지 않는다. 불리언은 `b`, 정수는 `n`, 문자열은 `sz` 접두사.
매직넘버 금지. C# 7.2 문법만. 날짜·이니셜이 붙은 형식의 신규 주석은 만들지 않는다.
UI code-behind 는 이번 작업에서 전혀 건드리지 않는다.

### 절대 건드리지 말 것
`WPF_Example/DatumMeasurement.csproj`, `WPF_Example/Setting/SystemSetting.cs`(형제 태스크 260909-kl0 전용),
`Action_FAIMeasurement.cs` 의 `MarkMeasurementDatumRefMissing` 안전망. 신규 `.cs` 파일 생성 금지.
  </action>
  <verify>
    <automated>
```bash
cd /c/code/DataMeasurement

# 0) 앱 실행 여부 (실행 중이면 빌드 실패 — 강제 종료 금지, 보고할 것)
tasklist | grep -i DatumMeasurement && echo "APP RUNNING - HALT" || echo "app not running"

# 1) 빌드 게이트: 에러 0 (CS0618 경고는 baseline, 게이트 아님)
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" \
  WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal \
  2>&1 | tee /tmp/build.log | tail -20
grep -c ": error " /tmp/build.log   # 0 이어야 함

# 2) 구조 게이트: 호출이 shouldRename 가드 안쪽에 있는가 (INI 로드 오발동 방어의 핵심)
#    닫는 중괄호 패턴은 16칸 들여쓰기다 — 칸 수를 바꾸면 range 가 안 잡힌다
sed -n '/if (shouldRename) {/,/^                }$/p' \
  WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs \
  | grep -c 'UpdateMeasurementDatumRefsAfterRename'      # 1 이어야 함

# 3) Load override 의 억제가 살아 있는가
grep -c '_suppressModelRename = true;' WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs   # 1

# 4) 신설 메서드 1개 + 소유 판정 단일 소스 재사용 (재구현 금지)
#    baseline: IsShotOwnedBySequence 5회 → 신설 메서드에서 1회 더 써서 6
grep -c 'UpdateMeasurementDatumRefsAfterRename' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs  # 1
grep -c 'IsShotOwnedBySequence' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs                  # 6

# 5) 안전망 보존 (baseline 3 — 정의 1 + 호출 1 + 주석 언급 1)
grep -c 'MarkMeasurementDatumRefMissing' WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs  # 3

# 6) 금지 파일 미변경
git diff --name-only | grep -c 'DatumMeasurement.csproj\|Setting/SystemSetting.cs'   # 0

# 7) 가독성 게이트 — diff 추가 라인만 대상 (두 파일 모두 legacy 위반이 이미 존재)
D=$(git diff -U0 -- WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs \
                    WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs \
    | grep '^+' | grep -v '^+++')
printf '%s\n' "$D" | grep -vE '^\+\s*//' | grep -cE '\?[^?]*:'   # 삼항: 0
printf '%s\n' "$D" | grep -vE '^\+\s*//' | grep -cF '??'          # null 병합: 0
printf '%s\n' "$D" | grep -vE '^\+\s*//' | grep -cF '?.'          # null 조건: 0
printf '%s\n' "$D" | grep -vE '^\+\s*//' | grep -cE 'switch.*=>'  # switch 식: 0
printf '%s\n' "$D" | grep -cF 'hbk'                               # 날짜 주석: 0
```
    </automated>
  </verify>
  <done>
Debug/x64 빌드 에러 0. `UpdateMeasurementDatumRefsAfterRename` 가 `InspectionSequence` 에 존재하고
`DatumConfig.DatumName` 세터의 `shouldRename` 블록 안에서 단 한 번 호출된다. 위 7개 게이트 전부 통과.
`DatumMeasurement.csproj` 와 `Setting/SystemSetting.cs` 는 변경되지 않았다.
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 2: 실기 UAT — 개명 전파 확인 + 로드 무변화 회귀 확인</name>
  <what-built>
Datum 이름 변경 시, 같은 시퀀스가 소유한 Shot 의 측정 중 `DatumRef` 가 옛 이름과 정확히 일치하는
것만 새 이름으로 자동 갱신되도록 배선했다. 옛/새 이름이 같은 시퀀스에서 모호하면 갱신을 포기하고
Error 로그만 남긴다. 빈 `DatumRef` 와 다른 이름을 가리키는 `DatumRef` 는 건드리지 않는다.
갱신 건수는 Trace 로그에 `[DatumRename]` 태그로 남는다.
  </what-built>
  <how-to-verify>
실기 레시피(`D:\Data\Recipe\FAI_1`)로 아래 순서를 그대로 밟아 주세요.

**1. 로드 오발동 회귀 (가장 중요)**
   - 앱을 켜고 레시피를 로드만 한다. 아무것도 바꾸지 않는다.
   - Trace 로그에 `[DatumRename]` 줄이 **한 줄도 없어야** 한다.
   - 트리에서 아무 측정이나 몇 개 열어 `DatumRef` 값이 종전 그대로인지 확인한다.

**2. 개명이 따라가는가 (`Side_Datum_1`, 참조 측정 8건)**
   - `Side_Datum_1` 노드를 선택하고 PropertyGrid 에서 `DatumName` 을 예: `Side_Datum_1_TEST` 로 바꾼다.
   - Trace 로그에 `[DatumRename] ... 8` 취지의 갱신 건수 줄이 뜨는지 확인한다.
   - 그 8건 측정 노드를 열어 `DatumRef` 가 새 이름으로 바뀌었는지 확인한다.
     (PropertyGrid 가 열린 채였다면 노드를 다시 선택해야 새 값이 보입니다 — 알려진 표시 한계입니다.)

**3. 검사가 정상인가**
   - 개명 상태로 SIDE 검사를 1사이클 돌린다.
   - `DATUM_REF_MISSING` skip 이 **0건**이어야 한다. Error 로그의 `[ShotMirror] ... Datum 이 레시피에 없음`
     경고도 나오지 않아야 한다.

**4. 기존 동작 회귀 없음 (패턴 모델 파일)**
   - 개명 후 해당 Datum 의 패턴 매칭이 종전대로 동작하는지(모델 파일이 새 이름을 따라갔는지) 확인한다.

**5. 영속성**
   - 레시피를 저장하고 앱을 완전히 종료 후 재기동한다.
   - 다시 로드해 그 8건의 `DatumRef` 가 새 이름으로 유지되는지 확인한다.
   - 이때도 `[DatumRename]` 로그는 뜨지 않아야 한다(로드는 개명이 아니다).

**6. 원복**
   - 이름을 `Side_Datum_1` 로 되돌리고 8건이 다시 따라오는지 확인 후 저장한다.
  </how-to-verify>
  <resume-signal>"approved" 또는 발견한 문제를 설명해 주세요</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| PropertyGrid(사용자 입력) → 레시피 인메모리 모델 | 검증 없는 문자열이 Datum 식별자가 되고, 그것이 측정 참조를 재배선한다 |
| INI 로드(리플렉션 SetValue) → 동일 세터 | 사용자 의도가 아닌 대입이 사용자 편집과 구분 불가하게 같은 세터를 때린다 |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-KTJ-01 | Tampering | `DatumConfig.DatumName` 세터 (INI 로드 경로) | critical | mitigate | 갱신 호출을 `shouldRename`(= `_suppressModelRename` 가드) 블록 안에만 둔다. 구조 게이트 #2 로 자동 검증 |
| T-KTJ-02 | Tampering | 동명 Datum 존재 시 참조 재배선 | high | mitigate | 옛/새 이름 모호성 검사 후 갱신 포기 + Error 로그. 기존 `DATUM_REF_MISSING` 안전망이 시끄럽게 잡도록 남긴다 |
| T-KTJ-03 | Tampering | 시퀀스 경계 넘는 갱신 | high | mitigate | `IsShotOwnedBySequence(shot, Name)` 단일 소스 사용 + 시퀀스 `Name` 이 비면 전면 중단 |
| T-KTJ-04 | Denial of Service | `Owner` null (레시피 미로드/독립 생성) | medium | mitigate | `Owner`/`SystemHandler.Handle`/`Sequences`/`RecipeManager`/`Shots` 전 구간 null 가드, 조용히 생략 |
| T-KTJ-05 | Information Disclosure | 갱신 결과 불가시 | low | mitigate | 0건 포함 항상 Trace 로그(`[DatumRename]`, 시퀀스/옛이름/새이름/건수) |
| T-KTJ-06 | Tampering | 검사 실행 중 개명(UI 스레드 vs 시퀀스 스레드) | low | accept | 모든 레시피 편집이 이미 동일하게 노출된 기존 조건. 새 경합 클래스를 만들지 않으므로 락 미추가 |
</threat_model>

<verification>
- Debug/x64 빌드 에러 0 (경고 baseline `CS0618` 은 게이트 아님)
- Task 1 의 자동 게이트 7종 전부 통과
- 실기 UAT 6단계 전부 통과 (특히 1번 "로드만 했을 때 무변화")
</verification>

<success_criteria>
- `Side_Datum_1` 개명 시 그 8건 측정의 `DatumRef` 가 새 이름으로 따라간다
- 개명 후 검사에서 `DATUM_REF_MISSING` skip 0건
- 패턴 모델 파일 이동은 종전과 동일하게 동작한다 (회귀 0)
- 레시피 저장·재기동 후에도 갱신된 `DatumRef` 유지
- 기존 레시피를 로드만 했을 때 `DatumRef` 무변화 + `[DatumRename]` 로그 0줄
</success_criteria>

<output>
`.planning/quick/260909-ktj-datum-datumref/260909-ktj-SUMMARY.md` 작성.
SUMMARY 에 반드시 포함할 것:
1. 억제 플래그를 신설하지 않고 `_suppressModelRename` 을 재사용한 근거 (세터 진입 경로 4종 전수표)
2. 시퀀스 경계를 같은 시퀀스로 한정한 근거 (런타임 해석이 시퀀스 스코프임)
3. 이름 충돌 시 "갱신하지 않고 로그만" 정책과 그 이유
4. **소급 교정은 범위 밖** — 이미 미아가 된 기존 레시피의 `DatumRef` 는 자동으로 고쳐지지 않는다.
   이번 수정은 앞으로의 개명에만 적용된다.
5. 알려진 표시 한계 — `DatumRef` 는 PropertyChanged 를 발화하지 않아 열려 있던 PropertyGrid 는
   즉시 갱신되지 않는다(노드 재선택 필요)
</output>
