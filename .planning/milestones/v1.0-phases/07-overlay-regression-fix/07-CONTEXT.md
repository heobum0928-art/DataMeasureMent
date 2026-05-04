# Phase 7: Measurement 오버레이 회귀 수정 - Context

**Gathered:** 2026-04-22
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 6에서 도입된 MeasurementBase 구조가 Phase 3의 에지 시각화(FAI-Edge-OK/NG 녹/적 + FAI-DistLine 청록)를 끊어버린 회귀를 복구한다. 구체적으로 다음 두 가지를 해결한다:

1. **TryExecute 시그니처에 overlay 반환 경로 신설** — 현재 `TryExecute(image, transform, pixelRes, out resultValue, out error)`는 overlay를 내보낼 수 없다. `out List<EdgeInspectionOverlay> overlays` 파라미터를 추가한다.
2. **Action_FAIMeasurement.Run의 Measure 루프가 overlay를 누적** — 현재 `Action_FAIMeasurement.cs:190`이 루프 직후 InspectionOverlays를 빈 리스트로 덮어쓴다. 루프 안에서 각 Measurement의 overlay를 누적하도록 바꾼다.

EdgePairDistanceMeasurement 한정으로 Phase 3 시각화를 완전 복구한다. 나머지 5종 Measurement(PointToLineDistance, PointToPointDistance, LineToLineAngle, LineToLineDistance, CircleDiameter)는 빈 overlay 리스트를 반환하며, 각각의 시각화 구현은 **별도 phase로 deferred**한다.

EdgeInspectionOverlay 모델 자체는 확장하지 않는다(현재 Line + Points 구조 유지). 측정값은 DataGrid에 이미 제대로 흐르고 있으므로 **값 로직은 건드리지 않는다**.

</domain>

<decisions>
## Implementation Decisions

### TryExecute 시그니처 확장
- **D-01:** MeasurementBase.TryExecute에 `out List<EdgeInspectionOverlay> overlays` 파라미터를 추가한다. 기존 out 파라미터(`resultValue`, `error`) 유지. C# 7.2 호환.
  ```csharp
  public abstract bool TryExecute(
      HImage image,
      HTuple datumTransform,
      double pixelResolution,
      out double resultValue,
      out string error,
      out List<EdgeInspectionOverlay> overlays);
  ```
- **D-02:** 측정 실패 시(return false) overlay는 `null`이 아닌 `new List<>()` 또는 부분 overlay를 반환해도 된다. 호출부(Measure 루프)는 실패 전까지 쌓인 overlay를 그대로 누적한다 — 디버깅 편의 + Phase 3 기존 동작 유지(실패해도 ROI 파란 박스 유지).
- **D-03:** 5개 파생 클래스(PointToLine / PointToPoint / LineToLineAngle / LineToLineDistance / CircleDiameter)는 TryExecute 내부에서 `overlays = new List<EdgeInspectionOverlay>()` 빈 리스트 반환. 측정값은 기존대로 DataGrid에 표시되지만 캔버스 시각화는 없음. 5종 시각화는 deferred.

### Measure 루프 누적
- **D-04:** Action_FAIMeasurement.cs의 Measure 스텝에서 루프 진입 직전에 `var overlayAcc = new List<EdgeInspectionOverlay>();` 초기화. 매 Measurement마다 `TryExecute` 호출 후, 반환된 `measOverlays`를 `overlayAcc.AddRange(measOverlays)`로 누적. 루프 종료 후 `pMyContext.InspectionOverlays = overlayAcc` 할당. **현재 코드의 `pMyContext.InspectionOverlays = new List<EdgeInspectionOverlay>()` 라인은 삭제한다.**
- **D-05:** 누적 범위는 현재 Shot(= 이 Action 실행 1회)의 모든 FAI × 모든 Measurement. FAI/Measurement 노드 선택 기반 필터링은 Phase 7 범위 외(기존 Shot 단위 표시 방식 유지).

### OK/NG 판정 반영
- **D-06:** TryExecute는 overlay ID를 판정 무관 기본 형태(`FAI-Edge1`, `FAI-Edge2`, `FAI-DistLine`)로 생성. Action_FAIMeasurement의 Measure 루프가 `EvaluateJudgement` 호출 후 `meas.LastJudgement`에 따라 `FAI-Edge*` 로 시작하는 overlay의 RoiId에 `-OK`/`-NG` suffix를 붙인다. 기존 quick 260417 작업 이전 Phase 3 패턴을 그대로 계승.
- **D-07:** `FAI-DistLine`은 suffix를 붙이지 않는다(기존 청록 고정). HalconDisplayService의 색상 분기(`FAI-Edge*-OK/-NG` startsWith → 녹/적, `FAI-DistLine` → 청록)를 그대로 사용한다 — 서비스 코드 변경 없음.
- **D-08:** 측정 실패(TryExecute false) 시 `meas.LastJudgement = false`로 설정되므로 기본 ID 그대로면 `FAI-Edge*-NG` suffix가 붙어 적색 표시. 실패 지점이 시각적으로 드러난다.

### EdgePair overlay 구성
- **D-09:** EdgePairDistanceMeasurement는 내부에서 이미 overlay를 생성하는 `FAIEdgeMeasurementService.TryMeasure` 결과(`result.Overlays`)를 그대로 전달한다. 서비스 자체는 수정하지 않는다(Phase 3 자산 재사용).
- **D-10:** `result.Overlays`는 `FAI-Edge1`, `FAI-Edge2`, `FAI-DistLine` 3개(Both 모드) 또는 `FAI-Edge1` 1개(Single 모드). 그대로 전달 후 루프에서 suffix 처리.

### 모델/서비스 변경 범위
- **D-11:** EdgeInspectionOverlay 모델은 확장하지 않는다. Circle/Arc 등 새 지오메트리 필드 추가는 나머지 5종 overlay를 구현하는 미래 phase의 몫. Phase 7 스코프 최소화 원칙.
- **D-12:** HalconDisplayService는 수정하지 않는다. 기존 색상 분기(FAI-Edge*, FAI-DistLine)가 Phase 3 규약을 그대로 커버.
- **D-13:** FAIEdgeMeasurementService는 수정하지 않는다.

### 검증 방식
- **D-14:** SIMUL_MODE + D:\1.bmp로 Top 시퀀스 실행 → 캔버스에서 EdgePair FAI의 녹/적 에지선 + 청록 DistLine 재표시 확인. Phase 3 UAT와 동일한 육안 확인 방식(Nyquist 단위 테스트 자동화는 Phase 9 VERIFICATION 과제로 이관).
- **D-15:** 빌드 검증: `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` — Phase 6이 남긴 6개 파생 클래스 시그니처가 모두 컴파일되어야 함(abstract 변경으로 전부 override 필요).

### Claude's Discretion
- EdgePairDistanceMeasurement에서 overlay가 생성 시점에 복사(Clone)가 필요한지 — FAIEdgeMeasurementService가 이미 새 리스트를 반환하므로 그대로 전달해도 무방할 가능성 큼. 구현 시 판단.
- suffix 부여 로직을 Measure 루프 내 인라인으로 둘지, `MeasurementBase` 또는 헬퍼에 정적 메서드로 둘지.
- 5종 파생 클래스의 `overlays = new List<EdgeInspectionOverlay>()`를 한 줄 assign으로 둘지 `overlays = null`로 두고 루프가 null-safe하게 받을지.

### Folded Todos
- 없음 (해당 phase 맞춤형 todo 미수집)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 블로커 지점 (수정 대상)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — Measure 루프 (130~191 주변). L190 InspectionOverlays 초기화 라인 제거 + 누적 로직 추가.
- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` — abstract TryExecute 시그니처 확장 (L45~50).

### 파생 클래스 (TryExecute override 갱신 대상)
- `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgePairDistanceMeasurement.cs` — `result.Overlays` 전달 로직 추가.
- `WPF_Example/Custom/Sequence/Inspection/Measurements/PointToLineDistanceMeasurement.cs` — 빈 리스트 반환.
- `WPF_Example/Custom/Sequence/Inspection/Measurements/PointToPointDistanceMeasurement.cs` — 빈 리스트 반환.
- `WPF_Example/Custom/Sequence/Inspection/Measurements/LineToLineAngleMeasurement.cs` — 빈 리스트 반환.
- `WPF_Example/Custom/Sequence/Inspection/Measurements/LineToLineDistanceMeasurement.cs` — 빈 리스트 반환.
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs` — 빈 리스트 반환.

### 재사용 (수정 없음)
- `WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs` — `TryMeasure(..., out result)` + `result.Overlays` 기존 출력 그대로 사용. L489 `BuildOverlaysBoth`, L545 `BuildOverlaysSingle` 규약 유지.
- `WPF_Example/Halcon/Models/FAIEdgeMeasurementResult.cs` — Overlays 프로퍼티 존재(L42).
- `WPF_Example/Halcon/Models/EdgeInspectionOverlay.cs` — RoiId + LineRow1/Col1/Row2/Col2 + Points 구조. 확장 없음.
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` — L127~137의 `FAI-Edge*`/`FAI-DistLine` 색상 분기 그대로.

### Upstream context
- `.planning/phases/06-rapid-city/06-CONTEXT.md` — D-14 (MeasurementBase + TryExecute 시그니처), D-15 (6종 파생 클래스), D-21 (Measurement 단위 결과 행).
- `.planning/v1.0-MILESTONE-AUDIT.md` — Gap I1 원인/영향 기술.
- `.planning/phases/03-edge-measurement/03-02-PLAN.md` (해당 시 참고) — Phase 3 overlay 규약 원본.

### 참고 문서 (간접)
- `260303_Rapicity_A8_1_Z-Stopper_MSOP_RevB` — 배경 제품 문서. Phase 7에서 직접 참조하지 않음.

</canonical_refs>

<code_context>
## Existing Code Insights

### 블로커 코드 (현재 상태)
Action_FAIMeasurement.cs Measure 스텝 말미:
```csharp
pMyContext.AllPass = allPass;
pMyContext.MeasuredCount = measuredCount;
pMyContext.InspectionOverlays = new List<EdgeInspectionOverlay>();   // ← L190: 블로커
Step = (int)EStep.End;
```

### 수정 후 목표 형태 (개념)
```csharp
// 루프 진입 전
var overlayAcc = new List<EdgeInspectionOverlay>();

foreach (var fai in ShotParam.FAIList) {
    foreach (var meas in fai.Measurements) {
        ...
        List<EdgeInspectionOverlay> measOverlays;
        ok = meas.TryExecute(image, transform, fai.PixelResolutionX,
                             out resultValue, out measError, out measOverlays);
        if (ok) meas.EvaluateJudgement(resultValue);
        else    { meas.ClearResult(); meas.LastJudgement = false; }

        // 판정 결과를 overlay ID에 반영
        if (measOverlays != null) {
            foreach (var ov in measOverlays) {
                if (ov.RoiId != null && ov.RoiId.StartsWith("FAI-Edge")) {
                    ov.RoiId += meas.LastJudgement ? "-OK" : "-NG";
                }
            }
            overlayAcc.AddRange(measOverlays);
        }
        ...
    }
}
pMyContext.InspectionOverlays = overlayAcc;
```

### MeasurementBase.TryExecute 시그니처 (현재 → 목표)
현재:
```csharp
public abstract bool TryExecute(
    HImage image, HTuple datumTransform,
    double pixelResolution,
    out double resultValue, out string error);
```
목표:
```csharp
public abstract bool TryExecute(
    HImage image, HTuple datumTransform,
    double pixelResolution,
    out double resultValue, out string error,
    out List<EdgeInspectionOverlay> overlays);
```

### Phase 3 overlay 규약 (유지)
- `FAI-Edge1`, `FAI-Edge2` → 판정 후 `-OK`/`-NG` suffix → HalconDisplayService가 녹/적.
- `FAI-DistLine` → suffix 없음, 청록 고정.
- FAIEdgeMeasurementService.BuildOverlaysBoth/Single이 이 ID를 이미 생성 중.

### EdgeInspectionOverlay 모델 (확장 없음)
```csharp
class EdgeInspectionOverlay {
  string RoiId;
  List<EdgeInspectionPoint> Points;
  double LineRow1, LineColumn1, LineRow2, LineColumn2;
}
```

### C# 7.2 / .NET 4.8 제약 유지
- out 파라미터 6개 가능 (C# 1.0부터 지원).
- out 변수 인라인 선언(C# 7.0+) 사용 가능.
- abstract 시그니처 변경 시 기존 override 전부 컴파일 실패 → 6개 파생 클래스 일괄 수정 필요.

### 빌드/테스트
- `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64`
- SIMUL_MODE + D:\1.bmp로 Top 시퀀스 검증.

</code_context>

<specifics>
## Specific Ideas

### suffix 부여 헬퍼 (선택 사항)
```csharp
private static void ApplyJudgementSuffix(
    List<EdgeInspectionOverlay> overlays, bool isOk)
{
    if (overlays == null) return;
    string suffix = isOk ? "-OK" : "-NG";
    foreach (var ov in overlays) {
        if (string.IsNullOrEmpty(ov.RoiId)) continue;
        if (ov.RoiId.StartsWith("FAI-Edge", StringComparison.OrdinalIgnoreCase)) {
            ov.RoiId = ov.RoiId + suffix;
        }
        // FAI-DistLine 등은 그대로
    }
}
```

### 예상 Plan 분할
- **Plan 01**: MeasurementBase.TryExecute 시그니처 확장 + 6개 파생 클래스 override 갱신(EdgePair는 result.Overlays 전달, 5종은 빈 리스트). 빌드 통과가 완료 조건.
- **Plan 02**: Action_FAIMeasurement Measure 루프 재작성(overlay 누적 + suffix 부여 + 초기화 라인 제거) + SIMUL_MODE 육안 검증.

2개 plan 구성 권장. 단일 plan으로도 가능하지만 시그니처 변경 커밋과 루프 재작성 커밋을 분리하면 롤백 안전성 ↑.

</specifics>

<deferred>
## Deferred Ideas

- **PointToLineDistanceMeasurement overlay**: 점 위치 마커 + 기준선(Datum line) + 수직선 링크. 별도 phase.
- **PointToPointDistanceMeasurement overlay**: 두 점 마커 + 연결선.
- **LineToLineAngleMeasurement overlay**: 두 fit 라인 + 교점 호(arc)로 각도 표시. EdgeInspectionOverlay 모델에 Arc 필드 필요.
- **LineToLineDistanceMeasurement overlay**: 두 평행선 + 수직 연결선.
- **CircleDiameterMeasurement overlay**: 검출 원 + 중심 마커 + 직경선. 모델에 Circle 필드(CenterRow/Col, Radius) 필요.
- **EdgeInspectionOverlay 모델 확장**: Shape enum(Line/Circle/Arc) + Circle/Arc 전용 필드 추가. 위 5종 overlay 구현의 전제.
- **FAI/Measurement 단위 선택 필터링**: 트리에서 선택된 Measurement만 캔버스 표시. 현재는 Shot 단위 전체 표시.
- **Datum 결과 시각화 overlay**: 기준 교점 + 축 화살표 (Phase 6 deferred와 동일).
- **Nyquist 단위 테스트 자동화**: Phase 7에서는 육안 검증만. 자동화는 Phase 9 VERIFICATION 과제.

</deferred>

---

*Phase: 07-overlay-regression-fix*
*Context gathered: 2026-04-22*
