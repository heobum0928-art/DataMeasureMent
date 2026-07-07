---
phase: quick-260707-fdx
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs
autonomous: true
requirements: [STAT-01]
must_haves:
  truths:
    - "히스토그램 x축 라벨이 5개 내외로 띄엄띄엄 표시되어 겹치지 않는다"
    - "추이 차트에서 공차 0(USL==LSL)일 때 USL/LSL 라벨이 겹치지 않고 단일 마크로 합쳐 표시된다"
    - "추이 차트에서 USL/LSL y값이 서로 근접하면 라벨이 겹치지 않는다"
    - "히스토그램의 USL/LSL 수직 마크도 근접(공차 0) 시 단일 마크로 합쳐진다"
    - "기존 정상 렌더(넓은 공차·충분한 샘플) 동작은 회귀 없이 그대로 유지된다"
    - "Debug/x64 msbuild 빌드가 PASS 한다"
  artifacts:
    - path: "WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs"
      provides: "라벨 겹침 제거 로직이 적용된 RenderHistogram/RenderTrend + 신규 헬퍼"
      contains: "setLabelStep"
  key_links:
    - from: "RenderHistogram"
      to: "XYChart.xAxis().setLabelStep"
      via: "라벨 스텝 계산 후 적용"
      pattern: "setLabelStep"
    - from: "RenderTrend"
      to: "AddSpecMarksY(근접 병합 헬퍼)"
      via: "USL/LSL 근접·공차0 판정 후 마크 추가"
      pattern: "AddSpecMarksY"
---

<objective>
StatisticsWindow(양산 이력 통계 분석)의 ChartDirector 차트 두 곳에서 발견된 라벨 겹침 표시 버그를 수정한다.

Purpose: UAT에서 발견된 표시 결함. (1) 히스토그램 x축에 BIN_COUNT=20개 라벨이 좁은 폭(~470px)에 전부 그려져 겹침. (2) 추이 차트의 평균/USL/LSL 수평 마크 라벨이 값 근접(특히 공차 0 → USL=LSL=Nominal) 시 완전히 겹침. 히스토그램의 USL/LSL 수직 마크도 동일 근접 겹침 가능.

Output: 라벨 스텝 적용 + 근접/공차0 마크 병합 로직이 적용된 StatisticsWindow.xaml.cs (단일 파일 수정).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@./CLAUDE.md
@WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs
@WPF_Example/UI/Statistics/StatisticsWindow.xaml

<project_rules>
반드시 준수 (하위 에이전트 프롬프트에도 매번 명시):
- 변경/추가한 모든 라인에 `//260707 hbk` 주석 (코드 수정 주석 컨벤션).
- 삼항 연산자 `?:` 금지 → 반드시 if-else 로 작성.
- 헝가리언 표기법 유지 (지역/필드 변수 접두: n=int, d=double, sz=string, b=bool 등 이 파일 기존 패턴 따름).
- C# 7.2 한정: switch expression / record / nullable reference types 사용 금지. 단순 for 루프 유지(Linq 지양 — 이 파일 MinOf/MaxOf 패턴 준수).
- 이 파일은 Allman brace 스타일(중괄호 새 줄). 기존 스타일 그대로 유지.
- 회귀 0: 넓은 공차·충분한 샘플의 정상 렌더 경로는 동작 변경 없어야 함.
</project_rules>

<interfaces>
<!-- ChartDirector API — 이미 참조된 ChartDirector 네임스페이스(using ChartDirector) 안에 존재. 별도 추가 참조 불필요. -->
<!-- 실행자는 아래 시그니처를 그대로 사용 — 코드베이스 재탐색 불필요. -->

XYChart (ChartDirector):
  Axis xAxis();                         // 히스토그램 라벨은 xAxis 에 setLabels 로 설정됨
  Axis yAxis();                         // 추이 차트 수평 마크는 yAxis 에 addMark 로 설정됨
  Layer addBarLayer(double[] data, int color);
  Layer addLineLayer(double[] data, int color);
  void setPlotArea(int x, int y, int w, int h);

Axis (ChartDirector):
  void setLabels(string[] labels);
  void setLabelStep(int majorStep);     // n번째 라벨만 표시 (예: 4 → 0,4,8,12,16 → 5개). 겹침 해소 핵심 API.
  Mark addMark(double value, int color, string text);   // 반환 Mark 로 라벨 스타일 조정 가능
  void setLabelStyle(string font, double fontSize, int fontColor, double fontAngle);  // 라벨 기울임(보조)

Mark (ChartDirector, addMark 반환):
  void setAlignment(int alignment);     // 라벨 정렬(겹침 시 위/아래 분산 보조, 예: Chart.TopLeft / Chart.BottomRight)

기존 상수/필드 (StatisticsWindow.xaml.cs 58~68행):
  BIN_COUNT = 20, CHART_W = 560, CHART_H = 300
  COLOR_USL/COLOR_LSL = 0xcc0000(빨강), COLOR_MEAN = 0x008800(초록)
  플롯 영역 폭 = CHART_W - 90 = 470px  → 20개 라벨 겹침의 원인
</interfaces>
</context>

<tasks>

<task type="auto" tdd="false">
  <name>Task 1: 히스토그램 x축 라벨 스텝 적용 + USL/LSL 마크 근접 병합</name>
  <files>WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs</files>
  <behavior>
    - 히스토그램: BIN_COUNT=20 라벨을 5개 내외로 축약 표시(겹침 제거).
    - 히스토그램 USL/LSL 수직 마크: bin 좌표(0..BIN_COUNT)에서 두 마크가 근접(공차 0 → 동일 위치)하면 "USL/LSL" 단일 마크 하나만.
    - 정상 케이스(넓은 공차): USL/LSL 각각 별도 마크 유지(회귀 0).
  </behavior>
  <action>
RenderHistogram(235행)을 수정한다.

1) 최대 라벨 개수 상수를 다른 상수들 근처(58~66행 구역)에 추가:
   `private const int MAX_X_LABELS = 5;   //260707 hbk 히스토그램 x축 최대 표시 라벨 수(겹침 방지)`

2) `c.xAxis().setLabels(labels);` 호출 직후, 라벨 스텝을 계산·적용한다. 스텝 = ceil(BIN_COUNT / MAX_X_LABELS). BIN_COUNT=20, MAX_X_LABELS=5 → 4 → 라벨 인덱스 0,4,8,12,16 = 5개.
   삼항 금지 → Math.Max 로 최소 1 보장:
   ```
   int nLabelStep = (int)Math.Ceiling((double)BIN_COUNT / MAX_X_LABELS);   //260707 hbk 라벨 겹침 방지 스텝
   if (nLabelStep < 1)
   {
       nLabelStep = 1;
   }
   c.xAxis().setLabelStep(nLabelStep);   //260707 hbk 5개 내외만 표시
   ```
   (선택 보강) 좁은 폭에서 F3 라벨이 여전히 붙으면 살짝 기울인다:
   `c.xAxis().setLabelStyle("", 8, ChartDirector.Chart.TextColor, -30);   //260707 hbk 라벨 기울임 보조` — 단, 이는 보조이며 setLabelStep 이 주 수정임.

3) USL/LSL 수직 마크 근접 병합. 기존 `if (dRange > 0)` 블록(254~262행) 내부의 두 addMark 호출을 근접 판정으로 감싼다. bin 좌표계(0..BIN_COUNT)에서 임계값 dEps = 0.5 (반 bin) 사용:
   ```
   double dEps = 0.5;   //260707 hbk 반 bin 이내면 USL/LSL 동일 위치로 간주(공차 0 포함)
   if (Math.Abs(dUslBin - dLslBin) <= dEps)
   {
       double dMidBin = (dUslBin + dLslBin) / 2.0;   //260707 hbk 겹침 → 단일 마크로 병합
       c.xAxis().addMark(dMidBin, COLOR_USL, "USL/LSL");   //260707 hbk 병합 라벨
   }
   else
   {
       c.xAxis().addMark(dUslBin, COLOR_USL, "USL");
       c.xAxis().addMark(dLslBin, COLOR_LSL, "LSL");
   }
   ```
   기존 두 줄(260~261행)을 위 if-else 로 대체. `//260707 hbk` 주석은 변경 라인에 부여.

주의: BuildHistogramBins / MinOf / MaxOf / RenderTrend 는 이 태스크에서 건드리지 않는다. Allman 중괄호·헝가리언 접두·if-else 유지.
  </action>
  <verify>
    <automated>MISSING — 이 프로젝트는 테스트 프레임워크 없음. Task 3(빌드) 로 컴파일 검증. 라벨 스텝/병합 로직은 코드 grep 으로 확인: rg "setLabelStep" WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs</automated>
  </verify>
  <done>RenderHistogram 에 setLabelStep(계산값) 적용 + USL/LSL 근접 시 "USL/LSL" 단일 마크 병합 if-else 존재. 정상 케이스는 USL/LSL 개별 마크 유지. 모든 변경 라인 //260707 hbk 주석.</done>
</task>

<task type="auto" tdd="false">
  <name>Task 2: 추이 차트 평균/USL/LSL 수평 마크 근접 병합(공차 0 겹침 제거)</name>
  <files>WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs</files>
  <behavior>
    - 공차 0(USL==LSL==Nominal): USL/LSL 라벨 완전 겹침 → "USL/LSL" 단일 마크 하나로 표시.
    - USL/LSL y값이 서로 근접(공차 매우 작음): 단일 병합 마크.
    - 평균 마크는 항상 표시. USL/LSL 이 평균과도 근접하면 라벨 정렬을 분산(위/아래)해 문자 겹침 완화.
    - 정상(넓은 공차): 평균/USL/LSL 3개 개별 마크 유지(회귀 0).
  </behavior>
  <action>
RenderTrend(268행)를 수정하고, 값-공간 근접 병합 헬퍼 `AddSpecMarksY` 를 신규 추가한다.

1) RenderTrend 의 3줄 addMark(281~283행):
   ```
   c.yAxis().addMark(dMean, COLOR_MEAN, "평균");
   c.yAxis().addMark(dUsl, COLOR_USL, "USL");
   c.yAxis().addMark(dLsl, COLOR_LSL, "LSL");
   ```
   을 헬퍼 호출 한 줄로 대체:
   `AddSpecMarksY(c, values, dMean, dUsl, dLsl);   //260707 hbk 근접/공차0 마크 병합 렌더`

2) 클래스 하단(MaxOf 아래)에 신규 private 메서드 추가. 값-공간 임계값은 데이터 스팬 기반으로 산출한다(고정 픽셀 아님 → 단위 무관 회귀 안전):
   ```
   /// <summary>추이 차트 평균/USL/LSL 수평 마크를 근접 겹침 방지하여 렌더(공차 0 → USL/LSL 병합). //260707 hbk</summary>
   private void AddSpecMarksY(XYChart c, List<double> values, double dMean, double dUsl, double dLsl)   //260707 hbk 마크 겹침 제거 헬퍼
   {
       double dMin = MinOf(values);   //260707 hbk
       double dMax = MaxOf(values);   //260707 hbk
       double dSpan = dMax - dMin;    //260707 hbk 데이터 스팬 기준 근접 임계

       // USL/LSL 을 스팬에 포함(마크가 데이터 밖일 수 있음)
       if (dUsl > dMax)   //260707 hbk
       {
           dSpan = dUsl - dMin;
       }
       if (dLsl < dMin)   //260707 hbk
       {
           dSpan = dMax - dLsl;
           if (dUsl > dMax)
           {
               dSpan = dUsl - dLsl;
           }
       }

       double dEps = dSpan * 0.02;   //260707 hbk 스팬 2% 이내면 근접으로 간주
       if (dEps <= 0)   //260707 hbk 스팬 0(전 값 동일) → 절대 최소 임계
       {
           dEps = 1e-9;
       }

       c.yAxis().addMark(dMean, COLOR_MEAN, "평균");   //260707 hbk 평균은 항상

       if (Math.Abs(dUsl - dLsl) <= dEps)   //260707 hbk 공차 0 또는 USL≈LSL → 병합
       {
           double dMid = (dUsl + dLsl) / 2.0;   //260707 hbk
           c.yAxis().addMark(dMid, COLOR_USL, "USL/LSL");   //260707 hbk 단일 병합 마크
       }
       else   //260707 hbk 정상: 개별 마크(회귀 0)
       {
           c.yAxis().addMark(dUsl, COLOR_USL, "USL");   //260707 hbk
           c.yAxis().addMark(dLsl, COLOR_LSL, "LSL");   //260707 hbk
       }
   }
   ```
   - 삼항 금지·헝가리언·Allman·C# 7.2 준수. Mark 반환값으로 setAlignment 를 쓰고 싶으면 써도 되나, 필수 아님(병합만으로 완전 겹침 제거됨). API 시그니처 불확실 시 setAlignment 는 생략하고 addMark 만 사용.
   - `XYChart` 타입은 이미 using ChartDirector 로 참조됨.

주의: RenderHistogram / BuildHistogramBins 는 이 태스크에서 미변경. dMean/dUsl/dLsl 값 계산(Grid_Stats_SelectionChanged 228~231행)은 변경 금지.
  </action>
  <verify>
    <automated>MISSING — 테스트 프레임워크 없음. Task 3(빌드) 로 컴파일 검증 + grep: rg "AddSpecMarksY" WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs</automated>
  </verify>
  <done>RenderTrend 이 AddSpecMarksY 호출로 대체되고, 신규 헬퍼가 공차0/USL≈LSL 시 "USL/LSL" 병합 마크, 정상 시 개별 마크를 렌더. 평균 마크 항상 유지. 모든 변경/추가 라인 //260707 hbk.</done>
</task>

<task type="auto" tdd="false">
  <name>Task 3: Debug/x64 빌드 검증</name>
  <files>WPF_Example/DatumMeasurement.csproj</files>
  <action>
전체 컴파일이 깨지지 않았는지 확인한다(회귀 0 게이트). msbuild Debug/x64 로 빌드.

MSBuild 경로는 VS 2017/2019 기준. 다음 중 존재하는 것 사용:
- `"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"`
- 또는 `"C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe"`
- 또는 PATH 의 `msbuild`

명령(예):
`msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Build /v:minimal /nologo`

SIMUL_MODE 심볼은 Debug 구성에서 자동 활성. 빌드 결과 "0 Error(s)" 확인. 경고는 무시 가능(기존 경고 수준 초과 신규 에러만 차단).
  </action>
  <verify>
    <automated>msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Build /v:minimal /nologo</automated>
  </verify>
  <done>Debug/x64 빌드 "Build succeeded" / 0 Error(s). StatisticsWindow.xaml.cs 컴파일 통과.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| (해당 없음) | 이 수정은 이미 로드된 로컬 통계 데이터(MeasurementHistoryCsvLoader 결과)를 차트로 렌더링만 한다. 신규 외부 입력·네트워크·파일쓰기 경계 없음. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-fdx-01 | D (DoS) | AddSpecMarksY dSpan 계산 | accept | 이미 dEps<=0 → 1e-9 하한 가드로 0 나눗셈/무한 방지. 신규 위험 없음. |
| T-fdx-02 | T (Tampering) | 차트 라벨 병합 로직 | accept | 표시 전용, 판정/데이터 무변경. 렌더 회귀는 빌드+육안 UAT 로 커버. |
</threat_model>

<verification>
- setLabelStep 계산값(=4)이 RenderHistogram 에 적용되어 히스토그램 라벨 5개 내외.
- 공차 0 데이터 선택 시 추이 차트에 "평균" + "USL/LSL" 2개 마크만(3중 겹침 소멸).
- 넓은 공차 데이터 선택 시 평균/USL/LSL 3개 개별 마크 유지(회귀 0).
- Debug/x64 빌드 PASS.
- 변경 라인 전부 //260707 hbk, 삼항 없음, Allman/헝가리언 유지.
</verification>

<success_criteria>
- 히스토그램 x축 라벨 겹침 해소(setLabelStep 적용, ~5개).
- 추이 차트·히스토그램의 USL/LSL 마크가 공차 0/근접 시 단일 "USL/LSL" 마크로 병합.
- 정상 렌더 경로 회귀 0.
- msbuild Debug/x64 성공.
</success_criteria>

<output>
After completion, create `.planning/quick/260707-fdx-stat-chart-label-overlap/260707-fdx-SUMMARY.md`
</output>
