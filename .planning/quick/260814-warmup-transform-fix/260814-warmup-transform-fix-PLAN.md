---
phase: quick-260814-warmup-transform-fix
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/SystemHandler.cs
autonomous: true
requirements: [MEASURE-WARMUP-01]

must_haves:
  truths:
    - "워밍업이 각 측정 실행 시 datumTransform 인자로 null 대신 유효한(non-null, non-empty) HTuple 을 넘겨서, Datum 참조 측정 타입(EdgeToLineDistanceMeasurement 등)이 TryExecute 진입부 가드에서 즉시 실패하지 않는다"
    - "Datum 좌표가 한 번도 이 측정 객체에 주입된 적 없는(DatumOriginRow/Col 둘 다 0.0) 측정은 identity 로 강제 실행되지 않고 skip 되어 fail 카운트를 오염시키지 않는다"
    - "워밍업 완료 Trace 로그에 skip 카운트가 success/fail 과 함께 남아 재시작 검증 시 원인 구분이 가능하다"
  artifacts:
    - path: "WPF_Example/Custom/SystemHandler.cs"
      provides: "RunMeasureWarmup identity transform 생성 + IsWarmupSkipTarget 판단 + TryWarmupOneMeasurement datumTransform 파라미터화"
      contains: "IsWarmupSkipTarget"
  key_links:
    - from: "RunMeasureWarmup"
      to: "TryWarmupOneMeasurement"
      via: "identityTransform 을 인자로 전달 (더 이상 null 하드코딩 없음)"
      pattern: "TryWarmupOneMeasurement\\(meas, img, identityTransform\\)"
---

<objective>
quick-260814-dxy(커밋 2fbbe94/79974f6)가 도입한 측정 파이프라인 워밍업이 `meas.TryExecute(img, null, 1.0, ...)`
로 `datumTransform` 에 **null** 을 넘기는 버그를 수정한다. 이 프로젝트의 대표 Datum 측정 타입
`EdgeToLineDistanceMeasurement.TryExecute` 는 진입부에서 `if (datumTransform == null || datumTransform.Length
== 0) { error = "Datum not found"; return false; }` 로 **즉시 reject** 한다(`EdgeToLineDistanceMeasurement.cs:111`)
— HALCON `measure_pos` 호출 자체가 전혀 발생하지 않는다. 실측 로그(`success=0 fail=885 elapsed=166ms`, 885건이
166ms 만에 끝남 — 실측정이라면 그보다 훨씬 오래 걸림)와 실제 레시피(`D:\Data\Recipe\FAI_1\main.ini` 의
`SHOT_0_FAI_*_MEAS_*` 섹션 59개 전부 `TypeName=EdgeToLineDistance`)가 이를 뒷받침한다. 즉 워밍업이 "완화 시도"는커녕
**단 한 번도 HALCON 측정 코드를 태우지 못하고 있었다.**

quick-260814-dxy 코드의 주석("datumTransform=null 은 identity 와 동일 — EdgeToLineDistanceMeasurement 등에서
이미 null 체크로 identity 처리하는 기존 관례")은 **틀린 가정이었다.** null→identity 폴백은
`VisionAlgorithmService.TryFitLine` **내부**(datumTransform이 null/empty면 원본 좌표 그대로 사용)에만 해당하고,
그 앞단에서 `EdgeToLineDistanceMeasurement` 자신이 별도로 두는 가드를 놓쳤다.

**수정 방향(identity HTuple 전달, DatumConfig 재검출 없음):** `HOperatorSet.HomMat2dIdentity()` 로 만든 유효한
(non-null, non-empty) HTuple 을 넘긴다 — 이는 프로덕션 `Action_FAIMeasurement.ResolveDatumTransform` 이
"Fixture 미존재/미지정 DatumRef" 상황에서 쓰는 것과 **완전히 동일한 폴백값**이다. identity 로 충분한 근거:
`Point_Row`/`Point_Col`(ROI 정의 좌표)은 교시(teaching) 시점에 사용자가 이미지 위를 직접 클릭해 저장한
**절대 이미지 픽셀 좌표**다(`MainView.xaml.cs` ROI 편집 코드 — `cRow`/`cCol` 클릭좌표를 그대로 대입).
`datumTransform` 은 그 좌표에 얹는 "교시 pose → 이번 사이클 검출 pose" 미세 보정 델타일 뿐이다
(`DatumFindingService.TryFindTwoLineIntersect`: `dRow=curRow-RefOriginRow` 로 translate 후 `curRow/curCol`
중심으로 rotate — 부품이 안 움직였으면 거의 identity). 워밍업은 라이브 검출을 하지 않으므로 이 델타를 알 방법이
없고, 워밍업이 재생하는 이미지가 `shot.SimulImagePath`(=실제 검사에도 그대로 쓰이는 정적 이미지)이므로
무보정(identity)으로도 ROI 는 교시된 실제 위치를 그대로 가리킨다 — "완전 정확"까지는 보장 못 해도 measure_pos
가 진짜 스캔 작업을 하게 만들기엔 충분하다.

`IDatumOriginConsumer` 를 구현하는 9개 측정 타입(`EdgeToLineDistanceMeasurement`, `DualImageEdgeDistanceMeasurement`
등)이 사용하는 `DatumOriginRow`/`DatumOriginCol`/`DatumAngleRad`/`DatumAngle2Rad`(datum 기준선까지 투영거리 계산에
직접 쓰임, identity transform 과는 별개의 경로)는 이미 `ParamBase.Load` 리플렉션이 레시피 로드 시점에 자동으로
채운다(이 필드들도 public double 이라 INI 직렬화 대상 — `main.ini` 의 `DatumOriginRow=8840.10...` 실측이 그 증거).
**추가 주입 코드가 필요 없다.** 단, 한 번도 검출 성공한 적이 없어(신규/미실행 레시피) 이 값이 진짜 0,0 인 측정은
identity 로 강제 실행해봐야 즉시 실패만 반복하므로(원래 버그의 재현일 뿐) skip 한다 — bug report 의 명시적 지시.

Purpose: 워밍업이 실제로 HALCON `measure_pos`/`measure_pairs` 캐시/코드페이지를 데우도록 만들어, quick-260814-dxy
가 원래 의도했던 "Release 콜드스타트 저하 완화" 효과를 실제로 발휘하게 한다.
Output: `WPF_Example/Custom/SystemHandler.cs` 의 워밍업 관련 3개 메서드(`RunMeasureWarmup`,
`TryWarmupOneMeasurement`, 신규 `IsWarmupSkipTarget`) 수정.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md
@.planning/quick/260814-dxy-measure-pos-release-2/260814-dxy-PLAN.md

**코딩 규칙 (이 프로젝트 상시 규칙):**
- 삼항연산자 `?:` 금지 → 반드시 `if / else`
- C# 7.2, .NET Framework 4.8 (8.0+ 문법 금지)
- 헝가리언 표기 — 로컬 `bool` 은 `b` 접두, `int` 는 `n` 접두 (신규 코드 한정)
- 이 지점(SystemHandler.cs 의 `SendTestError` 이후 블록)은 Allman 브레이스 + 헝가리언 스타일 — 그대로 유지
- 신규 주석은 `260814 hbk quick-260814-warmup-transform-fix:` 접두, 비자명한 "왜"만 최소한으로

---

## 절대 건들면 안 되는 파일 (열지도 말 것)

| 파일 | 상태 | 지침 |
|------|------|------|
| `WPF_Example/DatumMeasurement.csproj` | 사용자의 별도 진행 중인 로컬 실험 | **절대 열지도, 건들지도 말 것.** 이번 작업은 새 `.cs` 파일을 만들지 않는다(기존 1개 파일만 수정) |
| `WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs` | 사용자의 별도 진행 중인 로컬 실험 | **절대 열지도, 건들지도 말 것.** 이번 작업과 무관 |

baseline diff 해시 (작업 후에도 동일해야 함, planner 실측 확인):
```
DatumMeasurement.csproj              : 3daa3bef520786d331716fb77bc93e2eb632b966
PickerCenterCalibrationService.cs    : 86d1071909389cdb13b4ff8f3032489aff26e2fe
```

`git add .` / `git add -A` / `git commit -a` 는 금지 — 반드시 수정한 1개 파일만 명시적으로 `git add`.

새 `.cs` 파일도 만들지 않는다 — csproj 는 classic-style(`<Compile Include>`)이라 편집이 필요해지는데, 이 파일은
절대 건들면 안 되는 파일이다.

<interfaces>
<!-- 실행자가 코드베이스를 탐색할 필요가 없도록 편집 대상 지점의 현재 코드를 그대로 옮겨둔다. -->

**`WPF_Example/Custom/SystemHandler.cs` — 현재 `RunMeasureWarmup` 전체(교체 대상, 현재 라인 ~398-435):**
```csharp
        //260814 hbk 대표 Shot 하나를 골라 그 FAI/Measurement 를 N회 반복 실행(TryExecuteMeasurement 와
        //  동일한 meas.TryExecute 호출 경로). EvaluateJudgement/ClearResult 는 호출하지 않는다 — 결과를
        //  완전히 버려서 실제 판정 로직/화면 표시에 어떤 영향도 주지 않는다.
        private void RunMeasureWarmup()
        {
            Stopwatch sw = Stopwatch.StartNew();
            HImage img = null;
            try
            {
                bool bIsSynthetic;
                ShotConfig shot = FindMeasureWarmupShot(out img, out bIsSynthetic);
                if (shot == null || img == null)
                {
                    Logging.PrintLog((int)ELogType.Trace, "[MeasureWarmup] 측정 항목 있는 Shot 없음 — 워밍업 스킵");
                    return;
                }

                int nSuccessCount = 0;
                int nFailCount = 0;
                for (int i = 0; i < MEASURE_WARMUP_ITERATIONS; i++)
                {
                    foreach (FAIConfig fai in shot.FAIList)
                    {
                        foreach (MeasurementBase meas in fai.Measurements)
                        {
                            bool bOk = TryWarmupOneMeasurement(meas, img);
                            if (bOk) nSuccessCount++;
                            else nFailCount++;
                        }
                    }
                }

                Logging.PrintLog((int)ELogType.Trace,
                    "[MeasureWarmup] 완료 shot={0} iterations={1} synthetic={2} success={3} fail={4} elapsed={5}ms",
                    shot.ShotName, MEASURE_WARMUP_ITERATIONS, bIsSynthetic, nSuccessCount, nFailCount, sw.ElapsedMilliseconds);
            }
            finally
            {
                if (img != null) img.Dispose();
            }
        }

        //260814 hbk 단일 측정 1회 실행 — 실제 TryExecuteMeasurement(Action_FAIMeasurement.cs) 의 DualImage
        //  주입 패턴을 그대로 미러링한다. datumTransform=null 은 MeasurementBase.TryExecute 계약상 identity
        //  와 동일(EdgeToLineDistanceMeasurement 등에서 이미 null 체크로 identity 처리하는 기존 관례).
        private bool TryWarmupOneMeasurement(MeasurementBase meas, HImage img)
        {
            DualImageEdgeDistanceMeasurement dualMeas = meas as DualImageEdgeDistanceMeasurement;
            bool bIsDual = dualMeas != null;
            if (bIsDual)
            {
                dualMeas.RuntimeImageA = img;
                dualMeas.RuntimeImageB = img;
            }
            try
            {
                double resultValue;
                string error;
                List<EdgeInspectionOverlay> overlays;
                return meas.TryExecute(img, null, 1.0, out resultValue, out error, out overlays);
            }
            catch
            {
                return false;
            }
            finally
            {
                if (bIsDual)
                {
                    dualMeas.RuntimeImageA = null;
                    dualMeas.RuntimeImageB = null;
                }
            }
        }
```
(`FindMeasureWarmupShot`/`ShotHasAnyMeasurement`, 이 블록 바로 뒤에 이어지는 메서드들은 — 이번 수정과 무관,
손대지 않는다.)

**핵심 타입 시그니처 (전부 이미 존재, 신규 아님):**
```csharp
// MeasurementBase.cs — DatumRef, 빈 문자열=무보정
public string DatumRef { get; set; } = "";

// IDatumOriginConsumer.cs — ReringProject.Sequence 네임스페이스 (이미 using 됨, SystemHandler.cs L12)
public interface IDatumOriginConsumer
{
    double DatumOriginRow { get; set; }
    double DatumOriginCol { get; set; }
    double DatumAngleRad  { get; set; }
    double DatumAngle2Rad { get; set; }
    double DatumDetectedCircleRow { get; set; }
    double DatumDetectedCircleCol { get; set; }
}
// EdgeToLineDistanceMeasurement, DualImageEdgeDistanceMeasurement 등 9개 타입이 구현한다.
// ParamBase.Load 리플렉션이 레시피 로드 시 이 필드들을 INI 값(직전 성공 사이클의 스냅샷)으로 이미 채운다 —
// 워밍업이 별도로 주입할 필요 없음.

// EdgeToLineDistanceMeasurement.cs:111 (root cause 지점, 참고용 — 이 파일은 수정 안 함)
if (datumTransform == null || datumTransform.Length == 0)
{
    error = "Datum not found";
    return false;
}

// VisionAlgorithmService.cs TryFitLine — null/empty 는 여기서만 "원본 좌표 그대로" 로 처리됨(위 가드 앞단만 통과하면 도달)
if (datumTransform != null && datumTransform.Length > 0) { /* 변환 적용 */ }
```

빌드 환경(2026-08-14 재확인, 이전 계획과 동일):
- MSBuild: `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`
- Git Bash 에서는 `-p:` 대시 프리픽스를 쓴다(`//p:` 는 깨짐)
- 빌드에 1~2분 걸릴 수 있으니 Bash 툴 타임아웃을 300000 으로 준다
- 실행 중인 프로세스가 산출물을 잠그고 있으면(MSB3021/3026/3027/3030) **프로세스를 절대 죽이지 말 것**(프로젝트
  하드 규칙) — 스크래치 `-p:OutDir=<scratchpad>/build-verify/` 로 컴파일만 재검증하고 SUMMARY 에 기록
- Debug/x64 빌드 warning 기존 baseline = 정확히 12줄(`CS0618`×10 + `CS0162`×2). "0경고" 를 기준으로 삼지 말 것
  — 목표는 **신규 warning 0 / 신규 error 0**.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: 워밍업 datumTransform null→identity 수정 + skip 가드 추가</name>
  <files>WPF_Example/Custom/SystemHandler.cs</files>
  <action>
`WPF_Example/Custom/SystemHandler.cs` 에서 `RunMeasureWarmup`/`TryWarmupOneMeasurement` 두 메서드를 아래와
같이 교체하고, 그 사이에 신규 `IsWarmupSkipTarget` 메서드를 추가한다(위 interfaces 블록의 "현재 코드"를 아래
"수정 후 코드"로 완전히 대체 — 나머지 `FindMeasureWarmupShot`/`ShotHasAnyMeasurement` 등은 손대지 않는다):

```csharp
        //260814 hbk 대표 Shot 하나를 골라 그 FAI/Measurement 를 N회 반복 실행(TryExecuteMeasurement 와
        //  동일한 meas.TryExecute 호출 경로). EvaluateJudgement/ClearResult 는 호출하지 않는다 — 결과를
        //  완전히 버려서 실제 판정 로직/화면 표시에 어떤 영향도 주지 않는다.
        private void RunMeasureWarmup()
        {
            Stopwatch sw = Stopwatch.StartNew();
            HImage img = null;
            try
            {
                bool bIsSynthetic;
                ShotConfig shot = FindMeasureWarmupShot(out img, out bIsSynthetic);
                if (shot == null || img == null)
                {
                    Logging.PrintLog((int)ELogType.Trace, "[MeasureWarmup] 측정 항목 있는 Shot 없음 — 워밍업 스킵");
                    return;
                }

                //260814 hbk quick-260814-warmup-transform-fix(root cause fix): null 대신 identity HTuple 을
                //  넘긴다. EdgeToLineDistanceMeasurement.TryExecute 는 datumTransform==null 이면 진입부에서
                //  즉시 "Datum not found" 로 false 반환한다(HALCON measure_pos 자체가 호출 안 됨) — 이전
                //  quick-260814-dxy 코드의 "null=identity 로 처리되는 기존 관례" 가정은 VisionAlgorithmService.
                //  TryFitLine 내부에만 해당하고, 그 앞단의 이 가드를 놓쳤던 것이 근본원인이다. identity 를 쓰는
                //  이유: Point_Row/Col(ROI 정의)은 교시 시점 절대 이미지 좌표이고, datumTransform 은 그 위에
                //  얹는 "교시→현재 사이클" 미세 보정 델타일 뿐이다(DatumFindingService.TryFindTwoLineIntersect
                //  참고). 워밍업은 라이브 검출이 없어 그 델타를 알 수 없으므로, 프로덕션 ResolveDatumTransform
                //  이 "Fixture 미존재/미지정" 상황에 쓰는 것과 동일한 identity(무보정)로 대체한다 — 워밍업이
                //  재생하는 이미지가 SimulImagePath(=실제 검사에도 쓰이는 정적 이미지)라 무보정으로도 ROI 는
                //  교시된 실제 위치를 가리킨다.
                HTuple identityTransform;
                try
                {
                    HOperatorSet.HomMat2dIdentity(out identityTransform);
                }
                catch
                {
                    Logging.PrintLog((int)ELogType.Error, "[MeasureWarmup] identity transform 생성 실패 — 워밍업 스킵");
                    return;
                }

                int nSuccessCount = 0;
                int nFailCount = 0;
                int nSkipCount = 0; //260814 hbk quick-260814-warmup-transform-fix: Datum 참조는 있는데 한 번도
                                     //  검출 성공한 적 없는 측정 — identity 강제 실행 시 즉시실패만 반복하므로 skip.
                for (int i = 0; i < MEASURE_WARMUP_ITERATIONS; i++)
                {
                    foreach (FAIConfig fai in shot.FAIList)
                    {
                        foreach (MeasurementBase meas in fai.Measurements)
                        {
                            if (IsWarmupSkipTarget(meas))
                            {
                                nSkipCount++;
                                continue;
                            }
                            bool bOk = TryWarmupOneMeasurement(meas, img, identityTransform);
                            if (bOk) nSuccessCount++;
                            else nFailCount++;
                        }
                    }
                }

                Logging.PrintLog((int)ELogType.Trace,
                    "[MeasureWarmup] 완료 shot={0} iterations={1} synthetic={2} success={3} fail={4} skip={5} elapsed={6}ms",
                    shot.ShotName, MEASURE_WARMUP_ITERATIONS, bIsSynthetic, nSuccessCount, nFailCount, nSkipCount, sw.ElapsedMilliseconds);
            }
            finally
            {
                if (img != null) img.Dispose();
            }
        }

        //260814 hbk quick-260814-warmup-transform-fix: Datum 참조(DatumRef)가 있는데 그 Datum 좌표가 이
        //  측정 객체에 한 번도 주입된 적 없는(IDatumOriginConsumer.DatumOriginRow/Col 둘 다 0.0) 경우만
        //  스킵 대상으로 판단한다. DatumRef 가 빈 문자열이면(무보정 의도) 스킵 아님 — identity 로 정상 실행.
        //  IDatumOriginConsumer 를 구현하지 않는 타입(PointToLineDistance 등)은 이 판단 근거 자체가 없으므로
        //  스킵하지 않고 identity 로 그대로 시도한다.
        private bool IsWarmupSkipTarget(MeasurementBase meas)
        {
            if (meas == null) return true;
            if (string.IsNullOrEmpty(meas.DatumRef)) return false;
            IDatumOriginConsumer consumer = meas as IDatumOriginConsumer;
            if (consumer == null) return false;
            bool bHasInjectedOrigin = (consumer.DatumOriginRow != 0.0 || consumer.DatumOriginCol != 0.0);
            return !bHasInjectedOrigin;
        }

        //260814 hbk 단일 측정 1회 실행 — 실제 TryExecuteMeasurement(Action_FAIMeasurement.cs) 의 DualImage
        //  주입 패턴을 그대로 미러링한다.
        //260814 hbk quick-260814-warmup-transform-fix: datumTransform 은 호출부(RunMeasureWarmup)가 미리
        //  만든 identity HTuple 을 받는다 — null 을 넘기면 EdgeToLineDistanceMeasurement 등이 진입부에서
        //  즉시 reject 하므로 반드시 유효한 non-null/non-empty HTuple 이어야 한다.
        private bool TryWarmupOneMeasurement(MeasurementBase meas, HImage img, HTuple datumTransform)
        {
            DualImageEdgeDistanceMeasurement dualMeas = meas as DualImageEdgeDistanceMeasurement;
            bool bIsDual = dualMeas != null;
            if (bIsDual)
            {
                dualMeas.RuntimeImageA = img;
                dualMeas.RuntimeImageB = img;
            }
            try
            {
                double resultValue;
                string error;
                List<EdgeInspectionOverlay> overlays;
                return meas.TryExecute(img, datumTransform, 1.0, out resultValue, out error, out overlays);
            }
            catch
            {
                return false;
            }
            finally
            {
                if (bIsDual)
                {
                    dualMeas.RuntimeImageA = null;
                    dualMeas.RuntimeImageB = null;
                }
            }
        }
```

**절대 하지 말 것:**
- `WPF_Example/DatumMeasurement.csproj`, `WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs`
  — 열지도 말 것.
- 삼항연산자 금지, `EvaluateJudgement`/`ClearResult` 호출 금지(판정 로직 오염 방지 — 기존 방침 유지).
- 새 `.cs` 파일 생성 금지.
- `DatumConfig`/`InspectionSequence`/`Action_FAIMeasurement`/`DatumFindingService` — 이 4개 파일은 **열지도,
  수정하지도 않는다**(이번 수정은 `SystemHandler.cs` 1개 파일로 완결된다. 라이브 Datum 재검출을 워밍업에
  재현하지 않는다 — bug report 의 명시적 스코프 제한).
  </action>
  <verify>
    <automated>F=WPF_Example/Custom/SystemHandler.cs && echo "=== [1] IsWarmupSkipTarget 정의 : 1 기대 ===" && grep -c "private bool IsWarmupSkipTarget" "$F" && echo "=== [2] TryWarmupOneMeasurement 신규 시그니처(HTuple datumTransform 파라미터) : 1 기대 ===" && grep -c "private bool TryWarmupOneMeasurement(MeasurementBase meas, HImage img, HTuple datumTransform)" "$F" && echo "=== [3] TryExecute 호출에 identityTransform 전달 : 1 기대 ===" && grep -c "meas.TryExecute(img, datumTransform, 1.0," "$F" && echo "=== [4] 옛 null 하드코딩 완전 제거 : 0 기대 ===" && grep -c "TryExecute(img, null, 1.0," "$F" && echo "=== [5] HomMat2dIdentity 호출 추가 : 1 기대 ===" && grep -c "HOperatorSet.HomMat2dIdentity(out identityTransform)" "$F" && echo "=== [6] 호출부 3-인자 전달 : 1 기대 ===" && grep -c "TryWarmupOneMeasurement(meas, img, identityTransform)" "$F" && echo "=== [7] EvaluateJudgement/ClearResult 미호출 확인(0 기대, 워밍업 블록 한정) ===" && awk '/private void RunMeasureWarmup/,/private ShotConfig FindMeasureWarmupShot/' "$F" | grep -c "EvaluateJudgement\|ClearResult" && echo "=== [8] 금지 파일 무변경(해시 baseline 과 동일해야 함) ===" && git diff -- WPF_Example/DatumMeasurement.csproj | git hash-object --stdin && git diff -- WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs | git hash-object --stdin && echo "=== [9] Debug/x64 빌드 ===" && "/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo 2>&1 | grep -iE "error CS|error MSB|warning CS|Build succeeded"</automated>
  </verify>
  <done>
- [1]~[3], [5], [6] 전부 정확히 `1`.
- [4] `0` — 옛 `null` 하드코딩 호출이 파일 안에 더 이상 존재하지 않는다.
- [7] `0` — 워밍업 블록(`RunMeasureWarmup` ~ `FindMeasureWarmupShot` 시작 전까지, `IsWarmupSkipTarget`/
  `TryWarmupOneMeasurement` 포함) 안에서 `EvaluateJudgement`/`ClearResult` 호출 없음(판정/화면 오염 없음
  유지 확인).
- [8] 두 해시가 각각 `3daa3bef520786d331716fb77bc93e2eb632b966` / `86d1071909389cdb13b4ff8f3032489aff26e2fe`
  와 동일 (baseline 과 완전 일치, 이번 작업으로 변경 없음).
- [9] `Build succeeded`, 신규 `error CS`/`error MSB` 0건, warning 은 기존 baseline 12줄(`CS0618`×10 +
  `CS0162`×2)과 정확히 동일(신규 warning 0). 산출물 잠김이면 스크래치 OutDir 컴파일 성공으로 대체하고
  SUMMARY 에 기록.
  </done>
</task>

</tasks>

<verification>
- Task 1 의 `<verify>` grep 체크 [1]~[8] 전부 통과 + Debug/x64 빌드(또는 스크래치 OutDir 컴파일) 성공.
- **런타임 검증은 이 세션 범위 밖이다.** 앱을 재시작해 `D:\Data\Trace` 최신 로그의 `[MeasureWarmup] 완료 ...`
  라인에서 `success` 값이 0보다 큰지, `skip` 값이 몇인지 확인하는 것은 **사용자가 직접 수행**해야 한다(bug
  report 의 명시적 지시 — 이 세션에서 실기 검증까지는 못함). 실행자는 SUMMARY 에 "재시작 테스트는 사용자 확인
  필요"라고 반드시 남길 것. `elapsed` 값도 참고 지표로 SUMMARY 에 남길 것 — 성공 시 이전(166ms)보다 훨씬 길어지는
  것이 정상이다(진짜 measure_pos 스캔이 도는 것이므로).
</verification>

<success_criteria>
- `WPF_Example/Custom/SystemHandler.cs` 의 워밍업 호출 경로가 더 이상 `null` 을 `datumTransform` 으로 넘기지
  않는다 — 항상 유효한 identity HTuple 또는 skip 중 하나.
- Debug/x64 빌드 성공, 신규 error/warning 0건.
- 금지 파일(`DatumMeasurement.csproj`, `PickerCenterCalibrationService.cs`) 무변경 유지.
- SUMMARY 에 "재시작 후 `[MeasureWarmup]` 로그의 success>0 확인은 사용자 몫" 이라는 문구가 명시적으로 남는다.
</success_criteria>

<output>
After completion, create `.planning/quick/260814-warmup-transform-fix/260814-warmup-transform-fix-SUMMARY.md`
</output>
