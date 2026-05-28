---
phase: 38-v1-1-carryover-cleanup-2026-05-28
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs
  - WPF_Example/Sequence/Param/CameraSlaveParam.cs
  - WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs
  - WPF_Example/Halcon/Models/RoiDefinition.cs
  - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgePairDistanceMeasurement.cs
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs
autonomous: true
requirements: []

must_haves:
  truths:
    - "사용자가 새 Measurement Type ComboBox 에서 미사용 5종(EdgePairDistance/PointToLineDistance/PointToPointDistance/LineToLineAngle/LineToLineDistance)을 더 이상 선택할 수 없다"
    - "기존 INI 레시피에 미사용 타입이 있어도 로딩이 정상 동작한다(파싱 오류 없이 측정 클래스 생성)"
    - "mm/pixel 분해능이 카메라(ShotConfig)별 단일값으로 일원화되어 모든 하위 FAI/ROI 가 그 값을 사용한다"
    - "X/Y 분리 분해능이 단일값(X=Y)으로 통합되며, 기존 INI 의 FAI별 PixelResolution 값은 로딩 시 카메라값으로 통일된다"
    - "msbuild Debug/x64 가 0 신규 warning 으로 빌드된다"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs"
      provides: "GetTypeNames() 에서 5종 제거, Create() switch 전체 유지"
      contains: "case \"EdgePairDistance\""
    - path: "WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs"
      provides: "로딩 시 FAI PixelResolution 카메라값 정규화 마이그레이션"
      contains: "PixelResolution"
  key_links:
    - from: "InspectionRecipeManager.LoadPhase6Format"
      to: "fai.PixelResolutionX/Y = shot.PixelResolution"
      via: "FAI 정규화 루프"
      pattern: "PixelResolution"
    - from: "Action_FAIMeasurement.cs:213"
      to: "meas.TryExecute(... fai.PixelResolutionX ...)"
      via: "측정 mm 변환 소비 지점"
      pattern: "fai\\.PixelResolutionX"
---

<objective>
v1.1 정리 항목 #1(측정 타입 정리)과 #5(픽셀분해능 카메라별 단일화)를 구현한다. 두 항목 모두 INI 하위호환을 유지하면서 운영 회귀 0 을 목표로 한다.

Purpose: 미사용 측정 타입을 UI 에서 숨겨 신규 선택 혼란을 제거하고, 산재된 X/Y 분리 분해능을 카메라별 단일값으로 일원화해 mm 변환 일관성을 확보한다.
Output: MeasurementFactory GetTypeNames 정리(5종 제거) + 카메라 단일값 기준 FAI/ROI 분해능 정규화 마이그레이션.
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

<interfaces>
<!-- 코드 진입점 — executor 는 아래 위치를 read_first 로 직접 확인한다 -->

MeasurementFactory.cs (Create switch 15종 / GetTypeNames 15종):
  - Create(string typeName, object owner): 미등록은 null 반환 (T-06-01 완화 — InspectionRecipeManager.cs:298 에서 null 체크 후 skip)
  - GetTypeNames(): FAIConfig L60 ItemsSource 캐시 단일 소스 (UI ComboBox + INI Type 드롭다운)

PixelResolution 산재 위치:
  - CameraSlaveParam.cs:26  → public double PixelResolution { get; set; } = 1.0;   (단일, ShotConfig 가 상속 = 카메라 객체)
  - FAIConfig.cs:85-86       → PixelResolutionX / PixelResolutionY = 1.0          (X/Y 분리)
  - RoiDefinition.cs:86,89   → PixelResolutionX / PixelResolutionY = 1.0          (X/Y 분리, DataMember)
  - EdgePairDistanceMeasurement.cs:41-42 → PixelResolutionX / PixelResolutionY = 1.0
  - MainView.xaml.cs:2033-2038 → 캘리브레이션 시 shot.PixelResolution = mmPerPixel; FAI X/Y = mmPerPixel 분배 (이미 단일값 분배 중)

실제 mm 변환 소비 지점 (회귀 검증 anchor):
  - Action_FAIMeasurement.cs:213 → meas.TryExecute(image, transform, fai.PixelResolutionX, ...)  (fai.PixelResolutionX 단일 인자만 소비 — Y 는 측정 경로 미소비)
  - FAIEdgeMeasurementService.cs:310-311 → pixelDist * fai.PixelResolutionX 또는 PixelResolutionY (X/Y 방향 분기 소비)

로딩 진입점 (마이그레이션 hook):
  - InspectionRecipeManager.cs:233 LoadPhase6Format — SHOT 루프 내 FAI 로드 (fai.Load L286) 후 L306-307 사이가 정규화 삽입점
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: MeasurementFactory GetTypeNames 미사용 5종 제거 (#1, D-01/D-02/D-03)</name>
  <read_first>
    - WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs (전체 — Create switch L14-49, GetTypeNames L51-71)
    - WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs:55-80 (GetTypeNames 를 ItemsSource 로 캐시하는 단일 소스 — UI ComboBox 노출 경로 확인)
  </read_first>
  <action>
    MeasurementFactory.cs 의 GetTypeNames() (L51-71) 반환 배열에서 아래 5개 문자열만 제거한다:
      "EdgePairDistance", "PointToLineDistance", "PointToPointDistance", "LineToLineAngle", "LineToLineDistance"
    제거 후 남는 노출 10종 = "CircleDiameter", "EdgeToLineDistance", "CircleCenterDistance", "EdgeToLineAngle",
      "ArcEdgeDistance", "ArcLineIntersectDistance", "CompoundAngle", "CompoundCenterCDistance",
      "CompoundCenterBDistance", "CompoundShortAxisDistance".
    (주의: "EdgeToLineAngle" 와 "CircleDiameter" 는 D-03 에 따라 유지 — 절대 제거하지 말 것.)

    Create() switch (L14-49) 는 절대 손대지 말 것 — 5개 case 모두 그대로 유지해야 기존 INI 레시피에 해당 타입이 있어도 측정 클래스가 정상 생성된다(D-01).
    Measurement 클래스 파일들(EdgePairDistanceMeasurement.cs 등)도 이 Task 에서 삭제/수정하지 않는다.

    변경 라인에 //260528 hbk Phase 38 #1 마커를 부착한다 (기존 //260413 hbk 등 마커 보존, Phase 20 D-12 stacking 패턴).
  </action>
  <verify>
    <automated>Grep "EdgePairDistance" WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs — GetTypeNames 블록(L51 이후)에는 0 매치, Create switch(L14-49)에는 1 매치 (case + return 2줄)이 남아 있어야 한다</automated>
  </verify>
  <acceptance_criteria>
    - GetTypeNames() 반환 배열에 "EdgePairDistance" / "PointToLineDistance" / "PointToPointDistance" / "LineToLineAngle" / "LineToLineDistance" 가 모두 부재
    - GetTypeNames() 반환 배열에 "EdgeToLineAngle" 와 "CircleDiameter" 가 존재 (유지 확인)
    - Create() switch 에 `case "EdgePairDistance":` 가 여전히 존재 (INI 하위호환 보존)
    - GetTypeNames() 반환 배열 원소 개수 = 10
  </acceptance_criteria>
  <done>GetTypeNames 10종, Create switch 15종 유지, 미사용 5종 UI 숨김 + INI 로딩 호환 동시 달성</done>
</task>

<task type="auto" tdd="false">
  <name>Task 2: 픽셀분해능 카메라별 단일화 마이그레이션 (#5, D-08/D-09/D-10/D-11)</name>
  <read_first>
    - WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs:233-310 (LoadPhase6Format — SHOT/FAI 로드 루프, 정규화 삽입점 L306-307)
    - WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs:82-86 (PixelResolutionX/Y 정의)
    - WPF_Example/Sequence/Param/CameraSlaveParam.cs:25-26 (PixelResolution 단일값 — ShotConfig 가 상속하는 카메라 객체)
    - WPF_Example/UI/ContentItem/MainView.xaml.cs:2020-2041 (캘리브레이션 분배 — 이미 단일 mmPerPixel → shot + FAI X/Y 분배 중)
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:210-215 (fai.PixelResolutionX 소비 지점 — 회귀 anchor)
    - WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs:305-312 (X/Y 방향 분기 소비)
  </read_first>
  <action>
    단일 소스 = 카메라별 단일값 = `ShotConfig.PixelResolution`(CameraSlaveParam.cs:26 상속, 카메라 단위). 별도 새 저장 객체를 추가하지 않는다 — ShotConfig 가 이미 카메라 객체이므로 그것이 단일 소스다(Claude's Discretion 결정: 새 config 객체 추가 대신 기존 ShotConfig.PixelResolution 을 정규 소스로 채택, 분배 방식 유지).

    마이그레이션(D-10) = 로딩 시 카메라 단일값으로 덮어쓰기. InspectionRecipeManager.LoadPhase6Format 의 FAI 로드 루프가 끝난 직후(L306 `}` 와 L307 `}` 사이, 즉 한 SHOT 의 모든 FAI 로드 완료 시점)에 정규화 블록을 추가한다:

      //260528 hbk Phase 38 #5 — D-10 마이그레이션: FAI별 산재 PixelResolution 을 카메라(Shot) 단일값으로 통일 (X=Y 정방형 픽셀 가정 D-09)
      double camRes = shot.PixelResolution;
      foreach (FAIConfig fai in shot.FAIList)
      {
          fai.PixelResolutionX = camRes;
          fai.PixelResolutionY = camRes;
      }

    이 블록의 정확한 위치: `for (int f ...)` FAI 루프를 닫는 `}` (현재 L307) 바로 다음, SHOT `for` 루프(`for (int s ...)`)가 닫히기 전. read_first 로 정확한 brace 위치를 재확인 후 삽입할 것.

    X≠Y 케이스(D-10): camRes 는 단일값이므로 X 와 Y 모두 동일하게 덮어쓰는 것으로 "X≠Y 시 X 기준" 요구를 충족한다 (camRes = shot.PixelResolution 이 X 역할). FAIConfig/RoiDefinition/EdgePairDistanceMeasurement 의 X/Y 필드 정의 자체는 INI 하위호환을 위해 제거하지 않는다(직렬화 키 유지 → 구 레시피 로딩 무오류). 단지 로딩 시 카메라값으로 정규화한다.

    FAIConfig.ToRoiDefinition 경로(FAIConfig.cs:205-206, 249-250)는 무수정 — 정규화된 fai.PixelResolutionX/Y 를 그대로 복사하므로 RoiDefinition 도 자동으로 카메라값을 받는다.

    CameraSlaveParam.cs / FAIConfig.cs / RoiDefinition.cs / EdgePairDistanceMeasurement.cs 는 이 Task 에서 필드 추가/삭제하지 않는다(회귀 0). 단, files_modified 에 포함된 이유는 read_first 확인용이며 실제 편집은 InspectionRecipeManager.cs 한 파일에 집중한다. 만약 read_first 결과 X/Y 별도 소비(FAIEdgeMeasurementService.cs:310-311 방향 분기)가 정규화로 영향받지 않음을 확인하면(X=Y 이므로 동일값) 추가 편집 불필요.

    측정값 차이 발생 가능성(성공기준 #3): 기존 INI 가 FAI별로 서로 다른 X/Y 값을 가졌다면 정규화 후 측정 mm 가 변할 수 있다. 이는 의도적 보정이며 SUMMARY 에 "정규화 전후 mm 변화 = 의도적 카메라 단일화 보정(D-10)" 으로 문서화한다.
  </action>
  <verify>
    <automated>msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 — exit 0, 신규 warning 0</automated>
  </verify>
  <acceptance_criteria>
    - InspectionRecipeManager.cs 의 LoadPhase6Format 에 `fai.PixelResolutionX = camRes;` 및 `fai.PixelResolutionY = camRes;` 가 SHOT 루프 내 FAI 루프 이후 위치에 존재
    - `double camRes = shot.PixelResolution;` 가 정규화 블록에 존재 (카메라 단일값이 소스)
    - CameraSlaveParam.cs / FAIConfig.cs / RoiDefinition.cs 의 PixelResolution 필드 정의는 변경 없이 그대로 존재 (INI 직렬화 키 유지 → 하위호환)
    - msbuild Debug/x64 exit 0, 신규 warning 0
    - 하위호환: PixelResolutionX/Y 키를 가진 기존 Phase6 INI 가 파싱 오류 없이 로드됨 (필드 제거 0 → 키 무시 문제 없음)
  </acceptance_criteria>
  <done>로딩 시 모든 FAI PixelResolution 이 카메라 단일값(X=Y)으로 통일, RoiDefinition 자동 전파, INI 하위호환 유지</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| INI 레시피 파일 → 로더 | 디스크 INI 가 신뢰 경계를 넘어 메모리 모델로 로드 (오프라인 데스크톱, 외부 네트워크 입력 없음) |

## STRIDE Threat Register

오프라인 Windows 산업용 데스크톱 앱 — 이 phase 는 신규 외부/네트워크 인터페이스를 도입하지 않는다. 전통적 auth/injection 위협 표면 없음(명시).

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-38-01 | Tampering | #5 PixelResolution 마이그레이션 (InspectionRecipeManager.LoadPhase6Format) | mitigate | 무결성 위험 = 로딩 시 mm 값 silent 변경. 변경 measurement 는 zero-regression 이거나 의도적 보정으로 문서화(성공기준 #3). X=Y 정방형 가정을 SUMMARY 에 명시, 비정방 카메라면 회귀 위험으로 문서화. |
| T-38-02 | Denial of Service | #1 GetTypeNames 정리 | accept | 미사용 타입을 UI 에서만 숨김 — Create() switch 유지로 기존 레시피 로딩 보존. 데이터 손실/거부 위험 없음. |
</threat_model>

<verification>
- msbuild Debug/x64 PASS, 신규 warning 0 (성공기준 #1)
- 미사용 5종 GetTypeNames 부재 + Create switch 잔존 (grep)
- PixelResolution 카메라 단일값 정규화 존재 (grep)
- 기존 PixelResolutionX/Y 키 INI 하위호환 유지 (필드 미삭제)
</verification>

<success_criteria>
- 미사용 측정 타입 정리 후 msbuild PASS, 신규 warning 0 (Success Criteria #1)
- 픽셀분해능 카메라별 단일화 — 기존 측정값 회귀 0 또는 의도적 보정 문서화 (Success Criteria #3)
- INI 하위호환 유지 (Success Criteria #5)
</success_criteria>

<output>
완료 후 `.planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-01-SUMMARY.md` 생성. #5 정규화 전후 mm 변화 가능성과 X=Y 정방형 가정을 의도적 보정으로 문서화할 것.
</output>
