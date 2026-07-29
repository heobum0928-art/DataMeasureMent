---
phase: quick-260729-kpy
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs
autonomous: false
requirements: [KPY-01]

must_haves:
  truths:
    - "측정 속성창(PropertyGrid)의 Image > DualImage 카테고리에서 '가로축 티칭 이미지'/'세로축 티칭 이미지' 두 입력 필드가 더 이상 보이지 않는다"
    - "같은 카테고리의 'Point z_index (ZIndexA)'/'Line z_index (ZIndexB)' 두 필드는 그대로 보인다 (건드리지 않음)"
    - "두 이미지 경로 값은 INI 레시피에 변경 전과 동일하게 저장/로드된다 — 검사Grab(가로/세로 토글)이 채운 경로가 유지된다"
    - "크로스-Z 듀얼이미지 측정(SHOT_E5 / FAI_E5 / E5_P1·E5_P2)이 변경 전과 동일한 결과를 낸다 (30.5mm 근처, OK)"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs"
      provides: "TeachingImagePath_Vertical / TeachingImagePath_Horizontal 에 PropertyGrid 전용 숨김 attribute"
      contains: "PropertyTools.DataAnnotations.Browsable(false)"
  key_links:
    - from: "DualImageEdgeDistanceMeasurement.TeachingImagePath_Horizontal / _Vertical"
      to: "ParamBase.Save / ParamBase.Load"
      via: "reflection GetProperties(Instance|Public) — public getter/setter 유지, Browsable 로 필터링되지 않음"
      pattern: "public string TeachingImagePath_(Horizontal|Vertical) \\{ get; set; \\}"
    - from: "MainView.xaml.cs 검사Grab / 이미지 Load 경로"
      to: "meas.TeachingImagePath_Horizontal / _Vertical"
      via: "C# 코드에서 프로퍼티 직접 대입·읽기 (PropertyGrid 경유 아님)"
      pattern: "TeachingImagePath_(Horizontal|Vertical)"
---

<objective>
`DualImageEdgeDistanceMeasurement` 의 `TeachingImagePath_Vertical`("세로축 티칭 이미지") / `TeachingImagePath_Horizontal`("가로축 티칭 이미지") 두 프로퍼티를 **측정 속성창(PropertyGrid)에서만 숨긴다.**

Purpose: 메뉴바의 가로축/세로축 토글 + 검사Grab 이 이미 이 경로들을 자동으로 채워주므로, 속성창에 같은 값을 편집하는 입력 필드가 또 뜨면 현장 작업자에게 혼란만 준다. 화면에서만 치우고 내부 동작은 100% 그대로 둔다.

Output: 대상 파일 1개에 `[PropertyTools.DataAnnotations.Browsable(false)]` attribute 2줄 추가. 그 외 변경 0.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs
@WPF_Example/Sequence/Param/ParamBase.cs

**확정된 스코프 경계 (사용자 명시 승인 — 벗어나지 말 것):**
- `ZIndexA` / `ZIndexB` (Point z_index / Line z_index, 값 -1/-1) 크로스-Z 설정은 **건드리지 않는다.** 속성창에 계속 보여야 한다.
- RUN 버튼 지원 여부는 이번 작업 밖. 관련 코드 손대지 말 것.
- 파일 1개만 수정. 새 파일/클래스/메서드 생성 금지. C# 7.2 문법만.
- HEAD 커밋 `5cec861` (LoadCrossZRoleImage SIMUL_MODE 게이트 제거) 을 포함한 오늘자 커밋을 되돌리지 말 것.

**같은 파일에 이미 확립된 두 가지 attribute 패턴 (반드시 구분할 것):**

패턴 A — PropertyGrid 에서만 숨김, **INI 저장은 유지** (← 이번 작업이 써야 할 패턴):
```
[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
```
같은 패턴이 `ParamBase.Owner` / `ParamBase.OwnerName` / `MeasurementBase.LastMeasuredValue` 등에도 쓰이고 있으며, 이들은 정상적으로 저장/로드된다.

패턴 B — PropertyGrid 숨김 + **직렬화까지 차단** (← 이번 작업에서 절대 쓰면 안 되는 패턴):
```
[System.ComponentModel.Browsable(false)]
[PropertyTools.DataAnnotations.Browsable(false)]
[Newtonsoft.Json.JsonIgnore]
public HImage RuntimeImageA { get; set; }
```

**왜 패턴 B 를 쓰면 안 되는가 (회귀 위험):**
검사Grab(가로/세로 토글) 이 `MainView.xaml.cs` 에서 이 두 프로퍼티에 직접 경로를 대입하고, 런타임 측정 로직(`ResolveFaiImageASource` / `TryGrabOrLoadFaiDualImages` / `LoadCrossZRoleImage`)이 이 값을 읽어 실제 검사를 수행한다. 직렬화가 끊기면 프로그램 재시작 시 경로가 비어 오늘 막 고친 크로스-Z 버그가 그대로 재발한다.

**직렬화가 안전한 근거 (코드 확인 완료 — 재확인만 하면 됨):**
- `ParamBase.Save` (ParamBase.cs:318-361): `GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)` 로 전체 public 프로퍼티를 순회하며 `case "String"` 경로로 저장. Browsable attribute 를 보지 않는다.
- `ParamBase.Load` (ParamBase.cs:363-373): `GetType().GetProperties()` 순회, 유일한 제외 조건은 `!prop.CanWrite`. 두 프로퍼티는 `{ get; set; }` 이므로 통과.

**현재 파일 attribute 기준선 (주석 라인 제외 집계):**
| 문자열 | 변경 전 | 변경 후 기대값 |
|--------|---------|----------------|
| `PropertyTools.DataAnnotations.Browsable(false)` | 11 | **13** |
| `System.ComponentModel.Browsable(false)` | 8 | **8 (불변)** |
| `Newtonsoft.Json.JsonIgnore` | 8 | **8 (불변)** |
</context>

<tasks>

<task type="auto">
  <name>Task 1: 두 티칭 이미지 경로 프로퍼티에 PropertyGrid 전용 숨김 attribute 추가</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs</files>
  <action>
파일을 fresh Read 로 열어 정확한 현재 라인 번호를 확인한다 (기준 시점 기준: `TeachingImagePath_Vertical` 선언 ~30행, `TeachingImagePath_Horizontal` 선언 ~38행).

**변경 1 — `TeachingImagePath_Vertical`:**
기존 attribute 블록은 `[Category("Image|DualImage")]` / `[System.ComponentModel.Description(...)]` / `[DisplayName("세로축 티칭 이미지")]` / `[InputFilePath(...)]` / `[AutoUpdateText]` 5줄이다. 이 블록의 **마지막 줄(`[AutoUpdateText]`) 바로 아래, `public string TeachingImagePath_Vertical` 선언 바로 위**에 다음 한 줄을 삽입한다:

`[PropertyTools.DataAnnotations.Browsable(false)]`

들여쓰기는 같은 블록의 다른 attribute 와 동일하게 스페이스 8칸.

**변경 2 — `TeachingImagePath_Horizontal`:**
동일하게, 해당 프로퍼티의 attribute 블록 마지막 줄(`[AutoUpdateText]`) 바로 아래 / `public string TeachingImagePath_Horizontal` 선언 바로 위에 같은 한 줄을 삽입한다.

**같이 넣을 것 — 의도 주석.** 각 삽입 라인 위에 왜 `PropertyTools` 전용인지 한 줄 주석을 단다 (미래에 누가 "3중 attribute 로 통일하자"며 직렬화를 끊는 사고를 막기 위함). 예:
`//260729 hbk quick-kpy: PropertyGrid 표시만 숨김. System.ComponentModel.Browsable/JsonIgnore 는 절대 추가 금지 — 검사Grab 이 채우고 런타임 측정이 읽는 값이라 INI 저장이 반드시 유지되어야 함.`
주석은 두 프로퍼티 중 첫 번째(`TeachingImagePath_Vertical`) 위에 한 번만 달고, 두 번째에는 `//260729 hbk quick-kpy: 위와 동일 (PropertyGrid 표시만 숨김)` 정도로 짧게 단다.

**절대 하지 말 것:**
- `[System.ComponentModel.Browsable(false)]` 추가 금지
- `[Newtonsoft.Json.JsonIgnore]` 추가 금지
- 기존 `[Category]` / `[Description]` / `[DisplayName]` / `[InputFilePath]` / `[AutoUpdateText]` 삭제·수정 금지 (DisplayName 은 MainView 진단 메시지 및 미래 복원 대비 그대로 둔다)
- `{ get; set; } = "";` 접근자·기본값 변경 금지 (public getter/setter 가 사라지면 ParamBase 순회 대상에서 빠져 값이 날아간다)
- `ZIndexA` / `ZIndexB` 및 그 외 어떤 프로퍼티도 손대지 말 것
- 다른 파일 수정 금지

**빌드.** `Debug|x64` 로 빌드한다. MSBuild 경로: `C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe`, 타깃 `WPF_Example/DatumMeasurement.csproj`, 옵션 `//t:Build //p:Configuration=Debug //p:Platform=x64 //v:m //nologo`.

빌드 실패 시(특히 `Browsable` 이름 모호성 CS0104 류) attribute 를 완전수식명 `PropertyTools.DataAnnotations.Browsable(false)` 로 썼는지 재확인한다 — 이 파일은 `using PropertyTools.DataAnnotations;` 와 `System.ComponentModel` 완전수식 사용이 섞여 있어 짧은 이름 `[Browsable(false)]` 은 쓰지 않는다.
  </action>
  <verify>
    <automated>cd "C:/code/DataMeasurement" && F=WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs && "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj //t:Build //p:Configuration=Debug //p:Platform=x64 //v:m //nologo 2>&1 | grep -E "error CS|error MSB" | head -20; echo "ERR_LIST_ABOVE_MUST_BE_EMPTY"; echo "PT_Browsable=$(grep -v '^\s*//' $F | grep -c 'PropertyTools.DataAnnotations.Browsable(false)') EXPECT=13"; ls -l --time-style=full-iso bin/x64/Debug/DatumMeasurement.exe</automated>
  </verify>
  <done>
`error CS` / `error MSB` 0건. `PT_Browsable=13` (변경 전 11 → +2). `bin/x64/Debug/DatumMeasurement.exe` 타임스탬프 갱신됨.
  </done>
</task>

<task type="auto">
  <name>Task 2: 값 보존 가드 — 금지 attribute 미추가 + 직렬화 경로 무변경 확인</name>
  <files>(읽기 전용 — 파일 수정 없음)</files>
  <action>
이번 작업의 **가장 중요한 회귀 방어선**이다. "화면에서만 숨기고 값은 그대로"가 실제로 지켜졌는지 정적으로 증명한다.

**2-1. 금지 attribute 미추가 확인 (카운트 고정 게이트).**
아래 verify 명령이 세 카운트를 출력한다. 주석 라인(`//` 시작)은 제외하고 세므로 설명 주석이 카운트를 오염시키지 않는다. 기대값:
- `PropertyTools.DataAnnotations.Browsable(false)` = **13** (11 + 신규 2)
- `System.ComponentModel.Browsable(false)` = **8** (불변 — 늘었다면 직렬화를 끊은 것)
- `Newtonsoft.Json.JsonIgnore` = **8** (불변 — 늘었다면 직렬화를 끊은 것)

뒤 두 값이 8 이 아니면 **즉시 되돌리고** 패턴 A 로 다시 작업한다.

**2-2. diff 육안 확인.**
`git diff -- <대상파일>` 을 실행해 추가된 라인이 (a) `[PropertyTools.DataAnnotations.Browsable(false)]` 2줄 + (b) 의도 주석뿐인지 확인한다. 삭제(`-`) 라인이 하나라도 있으면 안 된다 — 이번 작업은 순수 추가여야 한다.

**2-3. 프로퍼티 시그니처 보존 확인.**
두 프로퍼티가 여전히 `public string ... { get; set; } = "";` 형태인지 확인한다. `ParamBase.Save` 는 `BindingFlags.Instance | BindingFlags.Public` 로, `ParamBase.Load` 는 `prop.CanWrite` 로만 필터링하므로, public getter + setter 가 살아 있으면 INI 저장/로드는 그대로 동작한다. Browsable attribute 는 두 메서드 어디에서도 검사하지 않는다 (ParamBase.cs:318-373 재확인).

**2-4. 소비자 경로 무변경 확인.**
`git status --porcelain` 으로 수정 파일이 대상 파일 **1개뿐**인지 확인한다. 특히 `MainView.xaml.cs`(검사Grab 이 경로를 대입하는 곳), `Action_FAIMeasurement.cs`(런타임 이미지 로드), `RecipeFileHelper.cs`, `CycleResultSerializer.cs` 는 변경 0 이어야 한다.
  </action>
  <verify>
    <automated>cd "C:/code/DataMeasurement" && F=WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs && echo "== ATTR COUNTS (expect 13 / 8 / 8) ==" && echo "PT_Browsable=$(grep -v '^\s*//' $F | grep -c 'PropertyTools.DataAnnotations.Browsable(false)')" && echo "SCM_Browsable=$(grep -v '^\s*//' $F | grep -c 'System.ComponentModel.Browsable(false)')" && echo "JsonIgnore=$(grep -v '^\s*//' $F | grep -c 'Newtonsoft.Json.JsonIgnore')" && echo "== PROPERTY SIGNATURES (expect 2 lines, both get;set;) ==" && grep -n 'public string TeachingImagePath_\(Vertical\|Horizontal\)' $F && echo "== DIFF: deletions must be 0 ==" && echo "deleted_lines=$(git diff -- $F | grep -c '^-[^-]')" && echo "== CHANGED FILES (expect exactly 1) ==" && git status --porcelain -- 'WPF_Example/*' | grep -v '^??'</automated>
  </verify>
  <done>
`PT_Browsable=13`, `SCM_Browsable=8`, `JsonIgnore=8`. 두 프로퍼티 모두 `public string ... { get; set; } = "";` 유지. `deleted_lines=0`. `git status` 결과 수정된 소스 파일이 `DualImageEdgeDistanceMeasurement.cs` 1개뿐.
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 3: 실기 확인 — 속성창에서 두 칸 사라짐 + 측정값 그대로 유지</name>
  <what-built>
`DualImageEdgeDistanceMeasurement` 측정의 "가로축 티칭 이미지" / "세로축 티칭 이미지" 두 입력칸을 속성창에서 안 보이게 처리했습니다. **값 자체는 그대로 남아 있습니다** — 검사Grab 이 채워주고 검사가 읽어 쓰는 값이라 지우면 안 되기 때문입니다. 즉 "칸만 치웠고, 안에 든 값과 동작은 하나도 안 건드렸다" 가 이번 작업 전부입니다.
  </what-built>
  <how-to-verify>
**1단계 — 프로그램 새로 켜기**
기존에 떠 있던 프로그램을 완전히 끄고, 새로 빌드된 프로그램을 다시 실행합니다. (안 끄고 확인하면 예전 화면이 그대로 보일 수 있습니다.)

**2단계 — 칸이 사라졌는지 보기**
- 왼쪽 트리에서 `SHOT_E5` → `FAI_E5` → `E5_P1` (또는 `E5_P2`) 를 클릭합니다.
- 오른쪽 속성창의 **`Image` 탭 → `DualImage` 묶음**을 봅니다.
- 확인할 것:
  - "가로축 티칭 이미지" 칸이 **안 보여야** 합니다.
  - "세로축 티칭 이미지" 칸도 **안 보여야** 합니다.
  - 반면 "Point z_index (ZIndexA)" 와 "Line z_index (ZIndexB)" 두 칸은 **그대로 보여야** 합니다. (이건 일부러 안 건드렸습니다.)

**참고 (헷갈리기 쉬운 부분):** 트리에서 **Datum 노드**를 클릭하면 거기에는 여전히 이미지 경로 칸이 보입니다. 그건 다른 항목이라 이번에 안 건드린 게 맞습니다. 이번에 치운 건 **측정 항목(E5_P1 / E5_P2)** 의 속성창 두 칸뿐입니다.

**3단계 — 측정이 그대로 되는지 보기 (이게 제일 중요합니다)**
- 수동 트리거로 **z=23 → z=24** 를 다시 한 번 태웁니다.
- 확인할 것: 측정값이 예전과 똑같이 **30.5mm 근처로 나오고 OK 판정**이 떠야 합니다.
- 이게 잘 나오면 "칸은 안 보이는데 안쪽 값은 그대로 살아 있다" 가 증명된 겁니다. 만약 값이 안 나오거나 이미지가 없다는 식의 에러가 뜨면 **바로 알려주세요** — 그건 값이 날아간 것이므로 되돌려야 합니다.

**4단계 — 저장 후 재시작해도 유지되는지 (여유 되면)**
- 레시피를 저장하고 프로그램을 껐다 다시 켠 뒤, 다시 z=23 → z=24 를 태웁니다.
- 여전히 30.5mm 근처 OK 가 나오면 저장/불러오기까지 정상입니다.
  </how-to-verify>
  <resume-signal>
"승인" 이라고 적어주시거나, 안 되는 부분을 있는 그대로 알려주세요 (예: "칸은 없어졌는데 측정이 에러 남", "칸이 아직 보임").
  </resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| (해당 없음) | 이번 변경은 UI 표시 attribute 2줄 추가로, 신뢰 경계를 넘는 입력/출력이 없다. 네트워크·파일·프로세스 경계 변화 0. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-KPY-01 | Tampering | `DualImageEdgeDistanceMeasurement` INI 직렬화 | mitigate | 잘못된 attribute(`System.ComponentModel.Browsable` / `JsonIgnore`) 추가로 레시피 경로 값이 소실되면 크로스-Z 측정이 무음 실패한다. Task 2 의 카운트 고정 게이트(SCM=8 / JsonIgnore=8 불변)와 human-verify 3단계 실측 30.5mm OK 로 차단. |
| T-KPY-02 | Denial of Service | 크로스-Z 측정 실행 경로 | accept | 값 소실 시 측정 실패 가능성이 있으나 Task 2 게이트 + 재시작 실측(4단계)로 커버. 외부 공격 표면 아님(로컬 단일 사용자 산업 장비). |
| T-KPY-SC | Tampering | 패키지 설치 | (해당 없음) | 신규 npm/pip/cargo 패키지 설치 없음. 기존 참조만 사용. |
</threat_model>

<verification>
1. **빌드**: MSBuild `Debug|x64` — `error CS` 0, `error MSB` 0, `DatumMeasurement.exe` 타임스탬프 갱신.
2. **금지 attribute 게이트**: `System.ComponentModel.Browsable(false)` = 8 (불변), `Newtonsoft.Json.JsonIgnore` = 8 (불변), `PropertyTools.DataAnnotations.Browsable(false)` = 13 (+2).
3. **순수 추가 diff**: `git diff` 삭제 라인 0, 수정 파일 1개.
4. **직렬화 경로 코드 리딩**: `ParamBase.Save`(BindingFlags.Instance|Public) / `ParamBase.Load`(CanWrite 만 필터) 어디에도 Browsable 검사 없음 재확인.
5. **human-verify**: 속성창에서 두 칸 소멸 + ZIndexA/ZIndexB 잔존 + z=23→24 측정 30.5mm 근처 OK.
</verification>

<success_criteria>
- [ ] `TeachingImagePath_Vertical` / `TeachingImagePath_Horizontal` 두 프로퍼티에 `[PropertyTools.DataAnnotations.Browsable(false)]` 각 1줄 추가됨
- [ ] `[System.ComponentModel.Browsable(false)]` / `[Newtonsoft.Json.JsonIgnore]` 는 **추가되지 않음** (카운트 8/8 불변)
- [ ] 두 프로퍼티 시그니처 `public string ... { get; set; } = "";` 그대로 유지
- [ ] `ZIndexA` / `ZIndexB` 변경 0 — 속성창에 계속 노출
- [ ] 수정 파일 `DualImageEdgeDistanceMeasurement.cs` 1개뿐, 삭제 라인 0
- [ ] MSBuild `Debug|x64` 에러 0
- [ ] human-verify: 속성창 두 칸 소멸 + z=23→24 측정 30.5mm 근처 OK (값 보존 회귀 테스트 통과)
- [ ] HEAD 커밋 `5cec861` 포함 오늘자 커밋 되돌림 0
</success_criteria>

<output>
Create `.planning/quick/260729-kpy-dualimageedgedistancemeasurement-propert/260729-kpy-SUMMARY.md` when done.
</output>
