# Quick Task 260805-ojq: PatternMatchService NCC/Shape 모델 read+clear 반복 캐싱 - Context

**Gathered:** 2026-08-05
**Status:** Ready for planning (원인 확정 — 사용자 실기 재현 + HALCON 공식문서 교차검증 완료, 재조사 불필요)

<domain>
## Task Boundary

사용자가 일괄검사 중 프로세스 메모리가 53.2GB까지 폭증(멈춰도 시간이 지나도 감소 없음)해 강제종료됨을 실측 보고. 여러 가설(캡쳐이미지 큐, HALCON region/HImage 누수, MeasurePos 핸들, 엑셀 export, `_batchAccumulated` DTO 누적)을 순차 조사·반증했고, 최종적으로 사용자가 **"Bottom Datum 검사에서 Test Find를 계속 누르니까 [메모리가] 올라가네"** 라는 결정적이고 재현 가능한 단서를 제공, 직접 코드 확인 후 사용자가 근본 원인을 정확히 지목함.

**확정된 메커니즘**: `WPF_Example\Halcon\Algorithms\PatternMatchService.cs`의 `TryFindPose`(런타임 매칭, ~line 304-462)가 호출될 때마다:
1. `HOperatorSet.ReadNccModel(modelPath, out modelId)` 또는 `ReadShapeModel(modelPath, out modelId)` — 파일에서 모델을 **매번 통째로 새로 읽어 메모리에 재구축**
2. `FindNccModel`/`FindShapeModel` — 1회 매칭
3. `finally` 블록에서 `ClearNccModel`/`ClearShapeModel(modelId)` — **즉시 폐기**

이 메서드는 `InspectionSequence.TryComposeAlign`(`WPF_Example\Custom\Sequence\Inspection\InspectionSequence.cs:1897`)의 ①매칭 단계에서 호출되며, `TryComposeAlign` 자체는:
- **실제 배치검사**: `Action_FAIMeasurement.cs`의 매 사이클, 패턴정렬(`IsPatternAlignEnabled`) 활성화된 Datum마다 호출 (Pattern2 baseline 설정 시 `TryFindPose`가 사이클당 최대 2회 호출, `TryComposeAlign` line 1934-1959)
- **Test Find UI**: `MainView.xaml.cs`의 `BtnTestFindDatum_Click`(line 4080)에서 클릭할 때마다 호출

즉 "read 통째로 새로 만들고 → 1회 쓰고 → 즉시 버리기"를 **매 사이클/매 클릭마다 반복**하는 구조 — 정상적인 HALCON 앱 설계라면 모델은 티칭(재교시) 시점에만 새로 읽고, 이후에는 캐싱된 핸들을 계속 재사용해야 한다.

**NCC vs Shape 비대칭 확인(사용자 실측 + HALCON 공식문서 교차검증)**: 사용자가 Top(문제 없음)과 Bottom(53GB 폭증)의 차이가 패턴매칭 엔진(NCC vs Shape)에 있음을 실측으로 확인 — **Bottom이 NCC 엔진**. HALCON 공식 문서(`create_ncc_model` reference, MVTec)에 따르면 NCC 모델은 **모든 회전각 스텝 × 모든 피라미드 레벨마다 래스터화(rasterized)된 템플릿 이미지 전체를 메모리에 저장**하는 방식인 반면, Shape 모델은 벡터/그래디언트 기반의 훨씬 가벼운 표현이다(사용자 확인: "shape이 더 가벼워 벡터라서"). 각도범위(`PatternAngleExtentDeg`)가 있는 NCC 모델은 이 read+clear 반복 자체가 무겁고, 반복 시 프로세스 메모리가 OS 에 정상적으로 반환되지 않고 누적되는 것으로 판단됨(HALCON 내부 메모리 풀이 대형 블록의 빈번한 할당/해제를 감당 못하는 것으로 추정 — 정확한 내부 메커니즘은 MVTec 비공개 구현이라 확인 불가하나, "매번 통째로 새로 만들고 버리는" 반복 자체가 원인이라는 사실 관계는 확정).

**함께 확인/반증된 것들(재조사 불필요, 이번 범위 아님)**:
- `CaptureImageSaveService` 큐 백프레셔(quick-260805-mzf, 커밋 44339bc) — 재빌드 후에도 문제 재현되어 이것만으로는 불충분함을 확인, 별개 문제로 이미 처리 완료.
- 일괄검사 동시실행 크래시(quick-260805-mze, 커밋 100bafe) — 별개 문제, 이미 처리 완료.
- `OverlayCaptureRenderer.cs`/`CaptureImageSaveService.SaveRequest`/`SharedHImage` AddRef-Release 대칭성/`ShotConfig.SetImage-GetImage`/`ExcelExportService` 캡쳐이미지 삽입/`_batchAccumulated`(DTO만 보유, 이미지 없음)/`EdgeInspectionOverlay`(순수 geometry) — 전부 직접 코드 확인 결과 정상, 원인 아님.
- MeasurePos 핸들(`GenMeasureRectangle2`→`CloseMeasure`) 6곳 중 5곳 정상, 1곳(`MeasurementAlgorithm.cs` 레거시 Obsolete 경로)만 예외 시에만 새는 낮은 영향도 버그 — 별도 task로 분리됨(task_79a05c8b), 이번 범위 아님.
- `VisionAlgorithmService.cs`의 `horotteRect` 리전 누수 — quick-260805-mzh(커밋 8e1e702)로 이미 수정 완료, 규모가 작아(작은 HRegion) 53GB 폭증의 주원인이 아니었음.

</domain>

<decisions>
## Implementation Decisions (LOCKED)

### 해결 방향: read+clear 반복을 caching으로 교체 (사용자 명시 지시: "readncc나 readshapemodel은 한번만 읽어 모델 만들고", "매번 로드하고 clear 하지말고")

`PatternMatchService.TryFindPose`를 수정하여:
1. `modelPath`를 키로 하는 **static 캐시**(`Dictionary<string, ...>` 형태 — 정확한 자료구조/래퍼 타입은 Claude's Discretion, 단 modelId(HTuple)와 engine 여부(NCC/Shape, Clear 호출 시 필요)를 함께 보관해야 함)를 도입한다.
2. 캐시에 `modelPath`가 있으면 **재사용**(파일 재읽기 없음), 없으면 그때 `ReadNccModel`/`ReadShapeModel`로 1회 로드 후 캐시에 저장한다(lazy load, 최초 1회).
3. **`finally`에서 더 이상 `ClearNccModel`/`ClearShapeModel`을 호출하지 않는다** — 모델 소유권이 캐시로 이전되므로, 매 호출 후 폐기하는 기존 로직을 제거한다.
4. 스레드 안전성: Top/Side/Bottom 시퀀스가 각자 스레드에서 동시에 다른(또는 같은) `modelPath`로 캐시에 접근할 수 있으므로 `lock` 또는 `ConcurrentDictionary`로 동시성을 보호한다. 같은 modelId로 `FindNccModel`/`FindShapeModel`을 여러 스레드가 동시에 호출하는 것은 HALCON에서 일반적으로 안전한 사용 패턴(모델은 read-only로 조회됨)으로 간주하고 별도 직렬화(lock)는 두지 않는다 — 단, 이 가정을 코드 주석으로 명시할 것.

### 캐시 무효화(재티칭 대응) — LOCKED, 반드시 구현
`PatternMatchService.TryCreateModel`(line 56-154, 티칭 시 새 모델을 생성해 `modelPath`에 write하는 메서드)이 성공적으로 새 모델을 파일에 쓴 직후, **같은 `modelPath`로 캐시된 기존 항목이 있으면 반드시 무효화**(캐시된 modelId를 `ClearNccModel`/`ClearShapeModel`로 정리 후 캐시에서 제거)해야 한다. 이걸 빠뜨리면 재티칭 후에도 이전(stale) 모델이 계속 쓰이는 회귀가 발생한다 — **이 항목은 plan-checker가 반드시 검증할 것**.

### 범위: TryFindPose만. TryFindRefPose는 이번 범위 아님
`TryFindRefPose`(line 170-, 티칭 완료 직후 ref pose 계산용, **호출 빈도가 티칭 1회뿐**)는 이번 캐싱 대상에서 제외한다 — 배치검사/Test Find처럼 반복 호출되지 않으므로 기존 read+clear 그대로 두어도 문제가 되지 않는다(불필요한 변경 범위 확대 금지, CLAUDE.md 원칙). 단, 플래너가 판단하기에 캐시 헬퍼를 공유해도 안전하고 코드가 더 단순해진다면 재사용은 허용(Claude's Discretion) — 단 기존 동작(매 호출 후 clear) 자체를 바꾸지는 말 것.

### 캐시 무효화 트리거 3종 확인(사용자 확정) — 타임스탬프 기반 검증으로 통일 (LOCKED, 추가 결정)
사용자가 캐시가 stale해지는 상황을 "재티칭하거나 프로그램 껐다가 키거나 레시피 파일 변경하거나 그정도"로 명시 확인함. 세 가지 대응:
1. **앱 재시작**: static 캐시가 프로세스 수명이므로 자동 해결(코드 불필요).
2. **재티칭**: 위의 `TryCreateModel` 훅으로 처리.
3. **레시피 파일 변경(백업 복원 등 앱을 거치지 않고 .shm/.ncm 파일 자체가 바뀌는 경우 포함)**: `TryCreateModel` 훅만으로는 "이 앱이 직접 쓴 경우"만 잡히고 외부에서 파일이 교체된 경우를 못 잡는다. **더 견고한 방법으로, 캐시 항목에 로드 시점의 `File.GetLastWriteTimeUtc(modelPath)`를 함께 저장**하고, `TryFindPose`가 캐시를 조회할 때마다 현재 파일의 마지막 수정시각을 다시 확인해 **캐시 저장 시점보다 파일이 더 최신이면 캐시 miss로 취급하고 재로드**한다. 이 방식이 3가지 트리거를 전부 통일된 메커니즘으로 커버하므로, `TryCreateModel` 훅(2번)은 유지하되(즉시 무효화로 응답성 확보) 타임스탬프 체크를 안전망으로 추가한다. `File.GetLastWriteTimeUtc` 호출은 파일 전체를 다시 읽는 것보다 훨씬 저렴하므로 매 호출 오버헤드는 무시 가능한 수준.

### 앱 종료 시 캐시 정리 — Claude's Discretion
프로세스 종료 시 캐시된 모델들을 전부 `Clear`하는 정리 훅(예: `SystemHandler` 종료 경로)을 추가할지는 플래너 재량. 필수는 아님(프로세스 종료 시 OS가 어차피 회수) — 있으면 좋으나 이번 버그(런타임 중 무한 증가) 해결에 필수는 아니다.

</decisions>

<specifics>
## Specific Ideas

- 대상 파일: `WPF_Example\Halcon\Algorithms\PatternMatchService.cs`
  - `TryFindPose` (약 line 304-462) — 캐싱 적용 대상, `ReadNccModel`/`ReadShapeModel`(line 378,392) + `finally`의 `ClearNccModel`/`ClearShapeModel`(line 446-459) 수정
  - `TryCreateModel` (line 56-154) — 성공 시(line 127 `return true;` 직전) 캐시 무효화 호출 추가
  - `TryFindRefPose` (line 170-) — 무변경 (범위 밖)
- 호출부(무수정 대상, 시그니처 불변이라 영향 없음 확인됨): `InspectionSequence.TryComposeAlign`(line 1888,1897), `MainView.xaml.cs BtnTestFindDatum_Click`(line 4080)
- 검증: 이 버그는 처음부터 사용자의 실기 재현(Test Find 반복 클릭 → 메모리 상승)으로 발견되었으므로, 최종 검증도 **같은 방식의 사용자 실측**(재빌드 후 Bottom Datum Test Find 반복 클릭 시 메모리 안정 확인 + 배치검사 재실행 시 메모리 안정 확인)이 필요하다 — plan에 checkpoint:human-verify 태스크로 반드시 포함할 것.
- 다른 진행 중인 quick task(mzf — 이미 완료·Task 2 사람 실측만 대기 중)와 파일 겹침 없음.

</specifics>

<canonical_refs>
## Canonical References

- [create_ncc_model — HALCON Operator Reference](https://www.mvtec.com/doc/halcon/13/en/create_ncc_model.html) — NCC 모델이 회전각 스텝×피라미드 레벨마다 래스터 이미지를 저장함을 확인
- [clear_ncc_model — HALCON Operator Reference](https://www.mvtec.com/doc/halcon/13/en/clear_ncc_model.html) — clear가 모델 메모리를 해제하고 핸들을 무효화함(정상 사용법 확인)
- 사용자 실기 재현: "bottom datum 검사에서 test find 를 계속 누르니까 올라가네" / "Top은 안그런데 Bottom만 엄청 올라가거든 차이는 NCC 랑 shape인데" / "아니 Bottom이 NCC야" / "shape이 더 가벼워 벡터라서"

</canonical_refs>
