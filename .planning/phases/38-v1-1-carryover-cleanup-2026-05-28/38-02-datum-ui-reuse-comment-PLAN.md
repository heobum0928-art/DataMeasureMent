---
phase: 38-v1-1-carryover-cleanup-2026-05-28
plan: 02
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
autonomous: true
requirements: []

must_haves:
  truths:
    - "DualImage 각도 검증 배지가 사용자 설정 없이는 기본 OFF 다 (AngleTolerance 기본 0.0 sentinel)"
    - "TwoLineAngleToleranceDeg 가 PropertyGrid 에서 더 이상 노출되지 않는다"
    - "검사 직각 게이트(default 10°) 로직은 무변경으로 동작한다 (DatumFindingService.cs:957-975)"
    - "DatumConfig.ReuseFromShotName 필드와 직렬화가 완전히 제거된다 (사용처 0)"
    - "SourceShotName 은 유지된다 (InspectionListView.xaml.cs:698-703 실사용)"
    - "기존 INI 에 ReuseFromShotName / AngleTolerance / TwoLineAngleToleranceDeg 키가 있어도 로딩이 정상 동작한다"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs"
      provides: "AngleTolerance 기본 0.0, TwoLineAngleToleranceDeg PropertyGrid 숨김, ReuseFromShotName 제거"
      contains: "AngleTolerance"
  key_links:
    - from: "DatumConfig.AngleTolerance (기본 0.0)"
      to: "DatumFindingService.cs:739 if (config.AngleTolerance > 0.0)"
      via: "sentinel 게이트 — 0.0 이면 배지 OFF(None)"
      pattern: "AngleTolerance"
    - from: "DatumConfig.IsHiddenForAlgorithm"
      to: "PropertyGrid GetProperties 필터"
      via: "TwoLineAngleToleranceDeg 무조건 hide"
      pattern: "TwoLineAngleToleranceDeg"
---

<objective>
v1.1 정리 항목 #6(각도 파라미터 UI), #12(ReuseFromShotName 제거), #10(DatumConfig 한정 저위험 주석 정리)를 구현한다. 세 항목 모두 DatumConfig.cs 단일 파일에 집중되며, 검사 게이트 로직 회귀 0 + INI 하위호환을 유지한다.

Purpose: DualImage 각도 검증 배지를 기본 OFF 로(혼란 제거), TwoLineAngleToleranceDeg 를 PropertyGrid 에서 숨겨 각도 파라미터 혼동 제거, 사용처 0 인 ReuseFromShotName 필드 제거, 그리고 같은 파일의 dead/노이즈 주석만 정리.
Output: DatumConfig.cs 의 AngleTolerance 기본값 1.0→0.0, TwoLineAngleToleranceDeg hide, ReuseFromShotName 완전 제거, 저위험 주석 정리.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/STATE.md
@.planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-CONTEXT.md
@.planning/todos/pending/2026-05-28-datum-angle-param-ui-cleanup.md

<interfaces>
<!-- DatumConfig.cs 진입점 (스카우트 + 코드 확인) -->

DatumConfig.cs:
  - :34-36  ReuseFromShotName 정의 (사용처 0 — grep 확인: 정의 1줄만 매치)
      //260413 hbk Phase 6: ReuseFromShot 모드일 때 재사용할 Shot 이름 (D-07)
      [Category("Datum|ImageSource")]
      public string ReuseFromShotName { get; set; } = "";
  - :38-40  SourceShotName 정의 (유지 — InspectionListView.xaml.cs:698-703 실사용)
  - :112    public double TwoLineAngleToleranceDeg { get; set; } = 10.0;  (직각 게이트 임계각)
  - :126    public double AngleTolerance { get; set; } = 1.0;  (DualImage 각도 배지 tolerance — sentinel)
  - :699-730 IsHiddenForAlgorithm(string name, EDatumAlgorithm alg) — PropertyGrid hide 필터.
      switch(alg) 각 case 가 name 매칭으로 hide 판정. ExpectedAngleDeg/AngleTolerance 는 이미 TLI/CTH/VTH 에서 hide (L703/710/718).
      switch 진입 전(L700 직전) 무조건 hide 한 줄 추가가 "모든 알고리즘에서 숨김" 패턴.

DatumFindingService.cs (이 파일은 무수정 — 동작 확인용):
  - :739   if (config.AngleTolerance > 0.0)  → 0.0 이면 AngleValidationStatus=None (배지 OFF). 기본값 0.0 변경만으로 OFF 달성, 코드 무변경.
  - :957-975 직각 게이트 default 10° 로직 — TwoLineAngleToleranceDeg 소비. 무변경(필드/게이트 보존).

ParamBase INI 직렬화 = GetType().GetProperties() reflection (ParamBase.cs L325 Save / L385 Load).
  필드 제거 시(ReuseFromShotName) → Save 에서 키 미출력. Load 는 INI 에 존재하는 키를 프로퍼티에 매핑 — 매칭 프로퍼티 없으면 무시(unknown 키 파싱 오류 없음). execute 시 ParamBase Load 경로에서 unknown 키 무시 동작 재확인 필요(D-06).
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: AngleTolerance 기본 OFF + TwoLineAngleToleranceDeg PropertyGrid 숨김 (#6, D-12)</name>
  <read_first>
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs:106-126 (TwoLineAngleToleranceDeg L112, AngleTolerance L126 정의 + XML 주석)
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs:699-730 (IsHiddenForAlgorithm — ExpectedAngleDeg/AngleTolerance hide 패턴 L703/710/718)
    - WPF_Example/Custom/Sequence/Inspection/DatumFindingService.cs:735-742 (AngleTolerance > 0.0 sentinel 게이트 — 무수정 확인)
    - WPF_Example/Custom/Sequence/Inspection/DatumFindingService.cs:955-975 (직각 게이트 default 10° — 무수정 확인)
  </read_first>
  <action>
    (a) AngleTolerance 기본값 변경: DatumConfig.cs:126
        현재: `public double AngleTolerance { get; set; } = 1.0; //260528 hbk Phase 36 D-36-05/07/13`
        변경: `= 0.0` 으로 (sentinel=off). 위 주석 XML 블록(L121-125)의 "default 1.0°(D-36-07)" 문구도 "default 0.0 (OFF, Phase 38 #6 D-12)" 로 갱신. 변경 라인에 //260528 hbk Phase 38 #6 마커 부착(기존 Phase 36 마커 stacking 보존).
        이것만으로 DatumFindingService.cs:739 `if (config.AngleTolerance > 0.0)` 게이트가 0 이면 AngleValidationStatus=None → 배지 미표시. DatumFindingService 는 무수정.
        INI 하위호환: 기존 레시피에 AngleTolerance 키 있으면 ParamBase Load 가 그 값으로 덮어씀(D-12 "기존 키 우선") → 회귀 0.

    (b) TwoLineAngleToleranceDeg PropertyGrid 숨김: IsHiddenForAlgorithm(L699) 의 switch(alg) { 진입 직후, 어느 case 보다 먼저 무조건 hide 한 줄을 추가한다:
        switch(alg) 의 여는 중괄호 다음 줄(L700 다음)에:
          if (name == "TwoLineAngleToleranceDeg") return true; //260528 hbk Phase 38 #6 D-12 — 모든 알고리즘에서 PropertyGrid 숨김 (직각 게이트 로직은 무변경)
        (또는 메서드 본문 첫 줄 `switch (alg) {` 바로 위에 배치해도 동일 효과 — 모든 case 보다 선행). ExpectedAngleDeg/AngleTolerance hide 패턴(L703 등)과 동일한 name 비교 방식.
        주의: 필드 정의(L112) 와 직렬화는 제거하지 않는다 → 검사 직각 게이트 안전망 + INI 호환 보존. PropertyGrid 노출만 차단.

    DatumFindingService.cs:957-975 직각 게이트 default 10° 로직은 절대 변경하지 않는다(D-12 명시).
  </action>
  <verify>
    <automated>Grep "AngleTolerance" WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs:126 영역 — `= 0.0` 매치 확인; Grep "TwoLineAngleToleranceDeg" IsHiddenForAlgorithm 영역에 `return true` 매치 확인</automated>
  </verify>
  <acceptance_criteria>
    - DatumConfig.cs 의 AngleTolerance 프로퍼티 기본값 = 0.0 (`public double AngleTolerance { get; set; } = 0.0;`)
    - IsHiddenForAlgorithm 에 `if (name == "TwoLineAngleToleranceDeg") return true;` 가 switch 모든 case 보다 선행 위치에 존재
    - TwoLineAngleToleranceDeg 프로퍼티 정의(L112)는 그대로 존재 (필드/직렬화 미삭제)
    - DatumFindingService.cs:739 `if (config.AngleTolerance > 0.0)` 무변경
    - DatumFindingService.cs:957-975 직각 게이트 10° 로직 무변경
  </acceptance_criteria>
  <done>각도 배지 기본 OFF + TwoLineAngleToleranceDeg PropertyGrid 숨김 달성, 검사 게이트 로직 회귀 0</done>
</task>

<task type="auto">
  <name>Task 2: ReuseFromShotName 필드 제거 + 저위험 주석 정리 (#12 D-04/D-05/D-06, #10 D-13)</name>
  <read_first>
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs:30-44 (ReuseFromShotName L34-36, SourceShotName L38-40 — 유지 대상)
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs:695-705 (SourceShotName 실사용처 — 유지 근거 확인, 이 파일은 무수정)
    - WPF_Example/Sequence/Param/ParamBase.cs:320-340, 380-400 (Save/Load reflection — unknown 키 무시 동작 확인)
  </read_first>
  <action>
    (a) #12 ReuseFromShotName 완전 제거 (D-04): DatumConfig.cs:34-36 의 3줄(주석 L34 + [Category] L35 + 프로퍼티 L36)을 통째로 삭제한다:
          //260413 hbk Phase 6: ReuseFromShot 모드일 때 재사용할 Shot 이름 (D-07)
          [Category("Datum|ImageSource")]
          public string ReuseFromShotName { get; set; } = "";
        SourceShotName(L38-40)은 절대 삭제하지 말 것(D-05 — InspectionListView.xaml.cs:698-703 실사용).
        ImageSourceMode(L31-32)도 이 Task 범위 밖 — 건드리지 않는다.
        ParamBase reflection 직렬화이므로 별도 Save/Load 코드 제거 불필요 — 프로퍼티 삭제만으로 직렬화 자동 제외(D-04).
        INI 하위호환(D-06): 기존 INI 에 ReuseFromShotName 키가 있어도 ParamBase Load 가 매칭 프로퍼티를 못 찾으면 무시한다. read_first 로 ParamBase.cs Load 경로가 unknown 키에 대해 예외 없이 skip 함을 재확인하고, 그 동작을 SUMMARY 에 인용할 것(D-06 요구).

    (b) #10 저위험 주석 정리 (D-13) — DatumConfig.cs 파일 한정으로만 수행한다(다른 파일 금지 — 같은 wave 38-01 과 충돌 방지). 정리 대상은 명백히 dead/노이즈인 것만:
        - 삭제로 인해 댕글링된 주석(ReuseFromShotName 삭제 잔재가 있으면 함께 제거)
        - 의미 없이 중복 나열된 //YYMMDD 마커 노이즈 (단, 단일 마커는 보존 — 변경 추적용)
        - _0428 같은 날짜백업 잔재 언급 주석(있을 경우)
        반드시 보존: 로직/알고리즘 설명 주석, "왜(why)" 주석, Phase 결정 ID 인용 주석, INI 직렬화 동작 설명 주석.
        회귀 위험 0 이 절대 우선 — 판단이 모호하면 보존한다(D-13). 코드 라인은 단 하나도 변경하지 않는다(주석만).
        신규 정리는 보수적으로: 확실한 dead 주석이 없으면 (a) 만 수행하고 (b) 는 "정리 대상 없음" 으로 SUMMARY 에 기록해도 무방하다.
  </action>
  <verify>
    <automated>Grep "ReuseFromShotName" WPF_Example/ — 0 매치; Grep "SourceShotName" WPF_Example/ — InspectionListView.xaml.cs 사용처 + DatumConfig.cs 정의 매치 유지; msbuild Debug/x64 exit 0</automated>
  </verify>
  <acceptance_criteria>
    - `Grep "ReuseFromShotName" WPF_Example/` 결과 0 매치 (필드 + 모든 참조 제거)
    - `Grep "SourceShotName" WPF_Example/` 결과에 InspectionListView.xaml.cs 사용처 + DatumConfig.cs 정의가 여전히 존재 (유지 확인)
    - DatumConfig.cs 의 코드 라인(주석 제외)은 ReuseFromShotName 3줄 삭제 외 변경 없음
    - msbuild Debug/x64 exit 0, 신규 warning 0
    - 하위호환: ReuseFromShotName 키를 가진 기존 INI 가 ParamBase Load 에서 예외 없이 로드됨 (unknown 키 무시 동작 SUMMARY 에 인용)
  </acceptance_criteria>
  <done>ReuseFromShotName 완전 제거 + SourceShotName 유지 + DatumConfig 저위험 주석 정리, INI 하위호환 유지</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| INI 레시피 파일 → ParamBase.Load | 디스크 INI 가 메모리 DatumConfig 로 로드 (오프라인 데스크톱, 외부 네트워크 입력 없음) |

## STRIDE Threat Register

오프라인 Windows 산업용 데스크톱 앱 — 신규 외부/네트워크 인터페이스 없음. 전통적 auth/injection 위협 표면 없음(명시).

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-38-03 | Tampering | #12 ReuseFromShotName 제거 — 구 INI 의 unknown 키 처리 | mitigate | ParamBase Load 가 매칭 프로퍼티 없는 키를 예외 없이 skip 함을 read_first 로 검증 후 SUMMARY 인용(D-06). 파싱 오류 없이 하위호환 유지. |
| T-38-04 | Repudiation | #6 AngleTolerance 기본 OFF — 안전망 약화 우려 | accept | 각도 검증은 배지(표시)일 뿐 검출 거부 아님. 직각 게이트(DatumFindingService.cs:957-975 default 10°)는 무변경으로 잘못된 datum 거부 안전망 보존. 배지 OFF 는 의도된 혼란 제거. |
</threat_model>

<verification>
- msbuild Debug/x64 PASS, 신규 warning 0 (성공기준 #1)
- AngleTolerance 기본 0.0 + TwoLineAngleToleranceDeg hide (grep)
- 직각 게이트 로직 회귀 0 (DatumFindingService 무수정)
- ReuseFromShotName 0 매치 + SourceShotName 유지 (grep)
- INI 하위호환 — 제거/숨김 키 모두 로딩 무오류
</verification>

<success_criteria>
- 각도 파라미터 UI 정리(배지 기본 OFF + TwoLineAngleToleranceDeg 숨김) — 검사 게이트 로직 회귀 0 (Success Criteria #2)
- 미사용 기능(ReuseFromShotName) 정리 후 msbuild PASS, 신규 warning 0 (Success Criteria #1)
- INI 하위호환 유지 (Success Criteria #5)
</success_criteria>

<output>
완료 후 `.planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-02-SUMMARY.md` 생성. ParamBase unknown 키 무시 동작(D-06)을 코드 인용으로 문서화할 것.
</output>
