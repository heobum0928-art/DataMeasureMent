---
phase: quick-260729-jdi
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
autonomous: false
requirements: [JDI-A, JDI-B]
tags: [cross-z, dual-image, simul-mode, fai-measurement, bottom, teaching-image]

must_haves:
  truths:
    - "SIMUL_MODE 가 정의되지 않은 빌드 구성(현재 Debug|x64)에서도 크로스-Z role A tick 은 TeachingImagePath_Horizontal 파일을, role B tick 은 TeachingImagePath_Vertical 파일을 읽는다"
    - "BOTTOM SHOT_E5 의 E5_P1/E5_P2 를 ZIndexA=23/ZIndexB=24 로 두고 z=23 → z=24 수동 트리거하면 실제 측정값(30.5mm 근처)과 OK 판정이 나온다"
    - "같은 측정을 ZIndexA=-1/ZIndexB=-1 로 되돌리면 종전과 동일한 30.543 / 30.537 OK 가 그대로 나온다 (비-크로스-Z 경로 무변경)"
    - "role 교시 경로가 비었거나 파일이 없으면 종전과 동일하게 ShotParam.GetImage() 라이브 폴백 + 동일 의미의 Trace 로그가 남는다"
    - "role 매핑(A→Horizontal, B→Vertical)은 한 글자도 바뀌지 않는다"
    - "ResolveFaiImageASource / TryGrabOrLoadFaiDualImages 의 본문은 한 줄도 바뀌지 않는다"
    - "WPF_Example/DatumMeasurement.csproj 는 수정되지 않는다"
    - "LoadCrossZRoleImage 반환값의 Dispose 소유권은 종전과 동일하게 호출부(ProcessCrossZCaptureTick 의 using)에 있고 이중 Dispose/누수가 없다"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
      provides: "LoadCrossZRoleImage 의 role별 교시 이미지 선택을 #if SIMUL_MODE 게이트 밖으로 이동 (모든 빌드 구성 동일 동작)"
      contains: "TeachingImagePath_Horizontal"
  key_links:
    - from: "Action_FAIMeasurement.ProcessCrossZCaptureTick"
      to: "Action_FAIMeasurement.LoadCrossZRoleImage"
      via: "using (HImage capturedImage = LoadCrossZRoleImage(bIsRoleA, dualMeas)) — 반환 이미지는 항상 새 인스턴스(new HImage(path) 또는 GetImage() 클론)"
      pattern: "using \\(HImage capturedImage = LoadCrossZRoleImage"
    - from: "Action_FAIMeasurement.LoadCrossZRoleImage"
      to: "DualImageEdgeDistanceMeasurement.TeachingImagePath_Horizontal / _Vertical"
      via: "role A/B 별 교시 경로 조회 — 컴파일 게이트 없이 항상 평가"
      pattern: "dualMeas\\.TeachingImagePath_(Horizontal|Vertical)"
---

<objective>
크로스-Z(ZIndexA/ZIndexB 설정) DualImage 측정이 `SIMUL_MODE` 가 정의되지 않은 빌드 구성에서 role별 교시 이미지를 무시하고 Shot 의 단일 오프라인 이미지를 두 role 모두에 쓰던 결함을 닫는다.

Purpose: BOTTOM SHOT_E5 의 E5_P1/E5_P2 가 크로스-Z ON 일 때 에지 0개(`strips ok 0/20 (noEdge 20)`)로 측정값이 나오지 않던 실기 재현 버그의 근본 원인 제거. 크로스-Z 경로를 비-크로스-Z 경로(`TryGrabOrLoadFaiDualImages` / `ResolveFaiImageASource`)와 동일한 "경로가 설정되어 있으면 항상 그 파일 사용" 정책으로 통일한다.
Output: `Action_FAIMeasurement.LoadCrossZRoleImage` 1개 메서드 + 관련 주석 수정. 신규 파일/클래스 없음.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@CLAUDE.md
@.planning/STATE.md

@WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
@WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs

<root_cause>
실기 재현으로 확정된 원인 (재조사 불필요, 라인번호는 fresh Read 로 재확인):

- 비-크로스-Z 경로 `TryGrabOrLoadFaiDualImages`(~766행)는 컴파일 게이트 없이 imageA = `ResolveFaiImageASource`(~744행, `TeachingImagePath_Horizontal` 이 라이브 grab보다 최우선), imageB = `TeachingImagePath_Vertical` 을 쓴다 → 정상 측정(30.543 / 30.537 OK).
- 크로스-Z 경로 `LoadCrossZRoleImage`(~1164행)는 role별 교시 경로 조회 블록 전체가 `#if SIMUL_MODE` 안에 있고 `#else` 는 무조건 `return ShotParam.GetImage();`.
- 현재 `Debug|x64` 의 DefineConstants 는 `TRACE;DEBUG` — SIMUL_MODE 없음. 따라서 크로스-Z 시 role A/B 둘 다 Shot 의 오프라인 이미지(`shot_SHOT_E5__vertical.bmp`)를 받아, 가로 이미지 기준으로 티칭된 PointROI 가 세로 이미지를 보게 되어 에지 0개.
- `[ALIGN]` 로그가 두 경우 완전히 동일 → 정렬/transform 은 무관. 차이는 오직 "실제로 읽는 이미지".
</root_cause>

<design_decisions>
플래너가 코드 확인 후 확정한 사항. 실행자는 이 결정을 그대로 따른다.

**D-1. 실HW 회귀 검토 — `OfflineInspectMode` 와 AND 로 묶지 않는다.**
- `SystemSetting.OfflineInspectMode`(`WPF_Example/Setting/SystemSetting.cs:164`)는 실재하는 런타임 플래그다. 그러나 비-크로스-Z 경로 `ResolveFaiImageASource` 는 이 플래그를 전혀 참조하지 않고 `TeachingImagePath_Horizontal` 을 라이브 grab 보다 무조건 최우선으로 둔다(같은 파일 735~743행 주석이 그 우선순위를 명시적 설계로 문서화, D-08 회귀수정 이력).
- 크로스-Z 만 `OfflineInspectMode` AND 조건을 추가하면 두 경로가 다시 갈라진다 — 그것이 바로 이번 작업이 없애려는 비대칭이다.
- 실HW 노출 위험은 이미 별도로 관리된다: `InspectionListView.xaml.cs:404` 가 RUN 트리거 시 `OfflineInspectMode` 켜짐을 확인 다이얼로그로 경고한다(VersionDefine.cs ⑧). 또한 role 교시 경로는 운영자가 명시적으로 설정해야만 채워지므로, 설정하지 않은 기존 레시피는 종전과 100% 동일하게 라이브 폴백된다(회귀 0).
- 결론: 런타임 게이트 없이 "경로가 설정되어 있으면 항상 사용".

**D-2. `ResolveFaiImageASource` 재사용 — 하지 않는다.**
- 그 헬퍼는 3단 우선순위(명시 경로 > 라이브 grab > `ShotParam.SimulImagePath` 폴백)를 갖고, HImage 를 반환하지 않고 `out pathA / out liveImageA / out bPathALoadNeeded` 3개를 돌려주는 "호출부가 로드한다" 계약이다. 크로스-Z 에 끌어오면 (a) 기존 폴백이 `GetImage()` 였던 것이 `SimulImagePath` 로드로 바뀌어 동작이 변하고, (b) role B 는 대응 헬퍼가 없어 어차피 비대칭이 남으며, (c) 헬퍼 시그니처를 건드리면 비-크로스-Z 호출부 회귀 리스크가 생긴다.
- 대신 role A/B 를 대칭적으로 "경로가 비어있지 않고 `File.Exists` 이면 `new HImage(path)`, 아니면 `ShotParam.GetImage()`" 로 둔다. 이는 role B(Vertical)의 비-크로스-Z 의미론과 동일하고, role A 도 교시 경로가 설정된 경우에는 `ResolveFaiImageASource` 와 결과가 완전히 같다(둘 다 명시 경로 최우선). 이번 버그 시나리오가 정확히 그 경우다.
- 실질적으로 이 결정은 기존 SIMUL_MODE 블록 본문을 그대로 두고 게이트만 벗기는 것이며, 그래서 회귀면이 가장 좁다.

**D-3. 소유권/Dispose 계약 — 변경 없음(확인 완료).**
- `ShotConfig.GetImage()`(`ShotConfig.cs:378`)는 `_image.CopyImage()` 클론을 반환하며 XML 주석에 "호출자가 Dispose 책임"이 명시되어 있다. `new HImage(path)` 도 새 인스턴스다.
- 따라서 `LoadCrossZRoleImage` 는 두 분기 모두 새 인스턴스를 반환하고, 호출부 `ProcessCrossZCaptureTick` 의 `using (HImage capturedImage = ...)`(~1225행)가 유일한 소유자로 Dispose 한다. `StoreCrossZImage(roleKey, capturedImage)` 는 저장소가 별도 사본을 갖는 기존 계약 그대로 — 이번 수정으로 바뀌는 것이 없다. 이중 Dispose/누수 경로 없음.
- `ResolveFaiImageASource` 를 재사용하지 않기로 한 것(D-2)이 `out liveImageA` 소유권을 크로스-Z 로 끌고 들어오는 문제 자체를 없앤다.

**D-4. `dualMeas` null 안전.**
- 게이트를 벗기면 `dualMeas` 가 비-SIMUL 빌드에서도 역참조된다. 유일한 호출부(~343행)는 `dualMeasForGate != null` 이 참인 `bHasAnyZIndex` 블록 안에서만 호출하므로 null 이 될 수 없다. 방어 코드 추가 불필요 — 추가하지 말 것(불필요한 diff 확대 금지).
</design_decisions>
</context>

<tasks>

<task type="auto">
  <name>Task 1: LoadCrossZRoleImage 의 #if SIMUL_MODE 게이트 제거</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
`Action_FAIMeasurement.cs` 의 `LoadCrossZRoleImage`(~1164행, 반드시 fresh Read 로 위치 확인)에서 `#if SIMUL_MODE` / `#else` / `#endif` 전처리 구조만 제거하고, 기존 SIMUL 블록 본문(role 경로 선택 → File.Exists 검사 → new HImage(path) → catch 폴백)을 메서드 본문으로 그대로 승격한다. `#else` 의 무조건 `return ShotParam.GetImage();` 한 줄은 삭제된다. D-2 에 따라 `ResolveFaiImageASource` 를 재사용하지 않는다. D-1 에 따라 `OfflineInspectMode` 등 어떤 런타임 조건과도 AND 로 묶지 않는다.

로직 변경 금지 항목(그대로 유지):
- role 매핑: `bIsRoleA` 이면 `dualMeas.TeachingImagePath_Horizontal`, 아니면 `dualMeas.TeachingImagePath_Vertical`
- 유효성 검사: `!string.IsNullOrEmpty(path) && File.Exists(path)`
- 폴백: 무효 경로 시 `ShotParam.GetImage()`, 로드 예외(try/catch) 시에도 `ShotParam.GetImage()`
- 로그 두 줄의 종류(Trace / Error)와 담긴 정보(role 라벨, 경로, 예외 메시지, "라이브 이미지로 폴백")

로그 문구 조정(동작 변경 아님, 문구만): 게이트가 사라졌으므로 두 로그 메시지에서 `SIMUL` 표현만 제거한다.
- Trace: `"[FAI CrossZ] SIMUL role "` 로 시작하던 것 → `"[FAI CrossZ] role "` 로 시작
- Error: `"[FAI CrossZ] SIMUL role 교시 이미지 로드 실패("` → `"[FAI CrossZ] role 교시 이미지 로드 실패("`

메서드 위 주석 블록(~1154-1163행) 갱신: 기존 이력(Phase 68 GAP-4 배경)은 보존하되 사실과 어긋나게 된 두 부분을 정정한다 — 제목의 "SIMUL_MODE 크로스-Z role별 이미지 대체" 라는 한정, 그리고 "비-SIMUL(실장비): 절대 변경 없음 — 항상 ShotParam.GetImage()" 문장. 프로젝트 주석 관례(`//260729 hbk ...`)로 이력 한 줄을 추가해 (1) 게이트를 벗긴 이유(SIMUL_MODE 미정의 Debug|x64 에서 role A/B 가 같은 이미지를 받아 에지 0개), (2) 비-크로스-Z `ResolveFaiImageASource` 와의 정책 통일, (3) 경로 미설정 레시피는 라이브 폴백 유지로 회귀 0 임을 기록한다.

`ProcessCrossZCaptureTick` 위 주석(~1196행)의 "SIMUL_MODE 에서 role별 교시 경로가 설정돼 있으면" 문장도 "role별 교시 경로가 설정돼 있으면" 으로 한정어만 제거한다(동작 서술은 그대로).

절대 금지: `ResolveFaiImageASource` / `TryGrabOrLoadFaiDualImages` 본문 수정, role 매핑 변경, `DatumMeasurement.csproj` 수정, `dualMeas` null 방어 코드 추가(D-4), 새 파일/클래스 생성, C# 8+ 문법(switch 식, 신규 식 본문 화살표, 보간 문자열). 이 메서드 주변의 Allman 브레이스 스타일을 유지한다.
  </action>
  <verify>
    <automated>cd "C:/code/DataMeasurement" && bash .planning/quick/260729-jdi-loadcrosszroleimage-role-simul-mode-z/260729-jdi-VERIFY.sh</automated>
  </verify>
  <done>
- `LoadCrossZRoleImage` 본문에 전처리 지시자가 없고, role별 교시 경로 선택이 항상 실행된다.
- VERIFY.sh 의 모든 카운트가 want 값과 일치한다 (simul_gate_count=3, simul_log_left=0, live_fallbacks=2, role_a_map=3, role_b_map=2, no_csharp8_added=0 등 — want 값은 수정 전 실측 baseline 기준으로 확정되어 있다).
- diff hunk 가 `LoadCrossZRoleImage` 및 그 주변 주석에만 있고, `ResolveFaiImageASource` / `TryGrabOrLoadFaiDualImages` 는 diff 에 없다.
- `git diff --name-only` 의 소스 파일이 `Action_FAIMeasurement.cs` 하나이고 `DatumMeasurement.csproj` 는 없다.
  </done>
</task>

<task type="auto">
  <name>Task 2: Debug|x64 빌드 + 실행 중 exe 잠금 해소로 바이너리 최신화</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
`Debug|x64`(SIMUL_MODE 없는 구성 — 이번 수정의 의미가 검증되는 구성)로 빌드한다. MSBuild 경로: `C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe`, 타깃 `WPF_Example/DatumMeasurement.csproj`, 옵션 `//t:Build //p:Configuration=Debug //p:Platform=x64 //v:m //nologo`.

`error CS` 가 하나라도 있으면 Task 1 로 되돌아가 고친다. 신규 `warning CS` 도 허용하지 않는다(기존 CS0618/CS0162 는 예외).

사용자가 빌드 직후 실기 확인을 하므로 stale 바이너리는 검증을 무의미하게 만든다:
1. `MSB3021/MSB3026/MSB3027`(실행 중 `DatumMeasurement.exe` 파일 잠금)로 `obj → bin` 복사가 실패했는지 확인한다. 이는 컴파일 실패가 아니지만 `bin/x64/Debug/DatumMeasurement.exe` 가 갱신되지 않는다.
2. `tasklist` 로 실행 중인 `DatumMeasurement.exe` 를 확인하고, 있으면 종료를 시도한다. Visual Studio 디버그 세션이 붙어 있어 종료되지 않으면 강제 종료하지 말고 사용자에게 "DatumMeasurement.exe 를 닫아주세요" 라고 알린 뒤 재빌드한다.
3. 복사까지 성공시킨 뒤 `bin/x64/Debug/DatumMeasurement.exe` 의 수정 시각을 확인하고 그 타임스탬프를 사용자에게 알린다.

코드 변경 없음 — 빌드/산출물 최신화 전용 태스크.
  </action>
  <verify>
    <automated>cd "C:/code/DataMeasurement" && "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj //t:Build //p:Configuration=Debug //p:Platform=x64 //v:m //nologo 2>&1 | grep -E "error CS|warning CS|error MSB302" | grep -v -E "CS0618|CS0162" | head -20; echo "ERR_LIST_ABOVE_MUST_BE_EMPTY"; ls -l --time-style=full-iso bin/x64/Debug/DatumMeasurement.exe; date</automated>
  </verify>
  <done>
- 빌드 출력에 `error CS` 0, 신규 `warning CS` 0, `error MSB3021/3026/3027` 0.
- `bin/x64/Debug/DatumMeasurement.exe` 의 수정 시각이 방금 빌드 시각과 같은 분(minute)이다.
- 사용자에게 "새 exe 준비 완료 + 타임스탬프" 를 알렸다.
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 3: 실기 확인 — 크로스-Z 측정 복구 + 기존 동작 무회귀</name>
  <what-built>
크로스-Z 측정이 z별로 서로 다른 교시 사진을 읽도록 고쳤습니다.

그동안 크로스-Z(ZIndexA/ZIndexB 를 쓰는 측정)에서는 첫 번째 z 든 두 번째 z 든 **샷에 저장된 사진 한 장만** 읽고 있었습니다. 그래서 가로 사진 기준으로 가르쳐 놓은 점(Point) 자리를 세로 사진에서 찾게 되어 에지를 하나도 못 찾고(`strips ok 0/20`) 측정값이 안 나왔습니다.

이제 크로스-Z 도 크로스-Z 를 안 쓰는 일반 측정과 **똑같은 규칙**으로 동작합니다: 첫 번째 z 는 가로 교시 사진, 두 번째 z 는 세로 교시 사진을 읽습니다. 교시 사진 경로를 안 넣어둔 기존 항목은 예전과 똑같이 라이브(샷) 사진을 씁니다.
  </what-built>
  <how-to-verify>
아래를 순서대로 해주세요. 하나라도 다르면 그 지점을 알려주시면 됩니다.

**1. 새 프로그램으로 다시 시작**
   - 실행 중인 프로그램을 완전히 종료한 뒤, 방금 빌드된 것으로 다시 켜주세요.

**2. 설정이 그대로인지만 확인 (바꾸지 마세요)**
   - BOTTOM `SHOT_E5` 의 ZIndex = 23
   - `E5_P1`, `E5_P2` 의 Point z index = 23, Line z index = 24
   - 두 항목의 가로/세로 교시 이미지 경로가 예전 그대로 들어 있는지

**3. 첫 번째 z 트리거 (z = 23)**
   - 수동 Z트리거로 z=23 을 보냅니다.
   - 기대: 트리거가 정상 처리되고, `E5_P1`/`E5_P2` 판정이 `CROSS-Z INCOMPLETE`(아직 짝이 안 맞음) 로 보입니다. 여기서 값이 안 나오는 것은 정상입니다.

**4. 두 번째 z 트리거 (z = 24)** ← 이번 수정의 핵심
   - 이어서 수동 Z트리거로 z=24 를 보냅니다.
   - 기대:
     - `E5_P1`, `E5_P2` 에 **실제 측정값이 나오고 OK 판정**
     - 값이 **30.5mm 근처** (크로스-Z 를 껐을 때 나왔던 30.543 / 30.537 과 비슷하면 성공)
     - 로그의 `[FitLine]` 이 `strips ok 20/20` 및 `50/50` (예전처럼 `0/20`, `0/50` 이면 실패)

**5. 되돌리기 확인 — 크로스-Z 를 껐을 때 (예전 동작 그대로여야 함)**
   - `E5_P1`/`E5_P2` 의 Point z index / Line z index 를 **-1 / -1** 로 바꿉니다.
   - 수동 트리거를 한 번 돌립니다.
   - 기대: 예전과 똑같이 **30.543 / 30.537 OK** 가 그대로 나옵니다.
   - 확인 후 **다시 23 / 24 로 되돌려 주세요.**

**6. 다른 항목 확인 — 크로스-Z 안 쓰는 BOTTOM 샷**
   - 크로스-Z 를 쓰지 않는 다른 BOTTOM 샷을 수동 트리거합니다.
   - 기대: 측정값과 판정이 예전과 완전히 동일합니다.
  </how-to-verify>
  <verify>
    <human-check>4번에서 E5_P1/E5_P2 가 30.5mm 근처 실측값 + OK 이고, 5번/6번에서 예전 값이 그대로 재현된다.</human-check>
    <automated>MISSING — 이 프로젝트는 테스트 프레임워크가 없고, 이 항목은 실제 레시피/이미지/트리거 상태에서만 관측 가능해 자동화 불가. 구조·빌드 검증은 Task 1/2 게이트가 전담한다.</automated>
  </verify>
  <resume-signal>"approved" 라고 적어주시거나, 어느 번호에서 무엇이 달랐는지 알려주세요</resume-signal>
  <done>
- 4번 통과: 크로스-Z ON 상태에서 E5_P1/E5_P2 실측값(30.5mm 근처) + OK, `[FitLine] strips ok 20/20` 및 `50/50`.
- 5번 통과: ZIndex -1/-1 로 되돌렸을 때 30.543 / 30.537 OK 재현 (비-크로스-Z 경로 무회귀). 확인 후 23/24 복구.
- 6번 통과: 크로스-Z 미사용 BOTTOM 샷 결과 무변화.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| 레시피(INI) → 측정 실행 | 운영자가 지정한 `TeachingImagePath_Horizontal/_Vertical` 문자열이 파일 시스템 경로로 사용된다 |
| 파일 시스템 → Halcon | `new HImage(path)` 가 디스크 파일을 디코딩한다 |
| 빌드 구성 → 런타임 동작 | `#if SIMUL_MODE` 전처리 심볼 유무가 측정 경로를 바꿔 왔다(이번 수정의 대상) |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-JDI-01 | Tampering | 빌드 구성별 상이한 측정 경로 | mitigate | 게이트 제거로 모든 구성에서 동일 코드 경로. Task 1 게이트가 `#if SIMUL_MODE` 잔존 수(2, LoadCrossZRoleImage 것만 제거)를 검증 |
| T-JDI-02 | Information Disclosure / Integrity | 실HW 에서 라이브 대신 저장 파일로 측정될 가능성 | accept | D-1 근거: 비-크로스-Z 경로가 이미 동일 정책이며 경로는 운영자가 명시 설정해야만 채워짐. `OfflineInspectMode` RUN 확인 다이얼로그가 별도 경고를 이미 제공 |
| T-JDI-03 | Denial of Service | 잘못된/손상된 교시 이미지 경로 | mitigate | 기존 `File.Exists` 가드 + try/catch → `ShotParam.GetImage()` 라이브 폴백 유지(변경 없음) |
| T-JDI-04 | Tampering | HImage 이중 Dispose / 누수 | mitigate | D-3: 두 분기 모두 새 인스턴스 반환, 소유자는 호출부 `using` 하나뿐. 계약 변경 없음 |
| T-JDI-SC | Tampering | npm/pip/cargo installs | n/a | 이 작업은 패키지 설치가 없다 (.NET Framework 4.8, 기존 참조만 사용) |
</threat_model>

<verification>
- Task 1: `260729-jdi-VERIFY.sh` 카운트 전부 want 일치, diff 범위가 `LoadCrossZRoleImage` + 주변 주석으로 한정, `csproj` 무변경.
- Task 2: MSBuild `Debug|x64` — `error CS` 0, 신규 `warning CS` 0, `MSB3021/3026/3027` 0, `bin/x64/Debug/DatumMeasurement.exe` 타임스탬프 갱신.
- Task 3: 실기 human-verify 6단계 전부 통과.
</verification>

<success_criteria>
- 크로스-Z ON(ZIndexA=23/ZIndexB=24) 상태에서 E5_P1/E5_P2 가 30.5mm 근처 실측값 + OK 판정을 낸다.
- 크로스-Z OFF(-1/-1) 및 크로스-Z 미사용 BOTTOM 샷의 결과가 종전과 동일하다.
- 수정 파일은 `Action_FAIMeasurement.cs` 하나이며 `DatumMeasurement.csproj` 는 손대지 않았다.
- role 매핑(A→Horizontal, B→Vertical)과 `ResolveFaiImageASource`/`TryGrabOrLoadFaiDualImages` 본문이 무변경이다.
- C# 7.2 문법만 사용, 신규 파일/클래스 없음.
</success_criteria>

<output>
Create `.planning/quick/260729-jdi-loadcrosszroleimage-role-simul-mode-z/260729-jdi-SUMMARY.md` when done
</output>
