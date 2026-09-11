# Phase 76: SIDE Datum 세로선 끄기 옵션 - Research

**Researched:** 2026-09-11
**Domain:** 기존 C# / HALCON 비전 검사 코드베이스 내부 아키텍처 분석 (외부 라이브러리/문서 조사 없음 — 순수 코드+운영 레시피 고고학)
**Confidence:** HIGH (모든 핵심 주장은 코드 직접 열람 또는 운영 레시피 `D:\Data\Recipe\FAI_1\main.ini` grep 으로 검증됨)

## Summary

이 phase 는 새 라이브러리가 필요 없는 **순수 리팩토링/조건분기 추가** 작업이다. 조사의 핵심은 "세로선을 끄면 정확히 무엇이 바뀌는가"를 코드 실행 경로 그대로 추적하는 것이었고, 결과는 CONTEXT.md 의 가정보다 **범위가 훨씬 좁다는 것**이 밝혀졌다.

가장 중요한 발견(요약 — 상세는 아래 §3): SIDE 4개 Datum 은 전부 `IsPatternAlignEnabled=True` (운영 레시피 확인)이고, 이 경로(`InspectionSequence.TryComposeAlign`)에서는 **실제 측정에 쓰이는 ROI 이동 transform 이 이미 패턴매칭만으로 산출된 `alignRigid` 다** (`InspectionSequence.cs:3044,3049` — `_datumTransforms[datumKey] = alignRigid`). 세로선(및 가로선) 검출로 만드는 `datumTransform`(교점 기반)은 **측정 위치 결정에 전혀 쓰이지 않고 즉시 alignRigid 로 덮어써진다.** 세로선 검출이 실제로 하는 일은 딱 두 가지뿐이다:
1. **성공/실패 게이트** — 실패하면 `TryComposeAlign` 전체가 `false` 를 반환해 `MarkAlignFailed`(lenient NG)로 이어짐. **이것이 사용자가 겪는 버그의 직접 원인.**
2. **`DetectedOriginRow/Col`, `DetectedRefAngle`, `DetectedRefAngle2` transient 필드 채움** — 이 필드들은 (a) `IDatumOriginConsumer` 경유로 측정 클래스에 주입되고 (b) 화면/캡처 오버레이가 그린다. SIDE 측정 25개는 전부 Y축/각도 측정이라 `DatumAngle2Rad`(세로 기준각)를 쓰는 곳이 없다(§4 코드+레시피로 재검증, `MeasureAxis=X` 카운트 **SIDE 범위 0건**).

따라서 이 phase 의 실제 작업은 "측정 transform 을 다시 설계"하는 것이 아니라, **`DatumFindingService.TryFindVerticalTwoHorizontalDualImage` 안에 가로선만 쓰는 분기를 추가**하고, 그 분기가 채우는 `DetectedOrigin*`/`DetectedRefAngle2` 를 (a) 기존 "미설정" sentinel 관례(`0.0`)에 맞게 채우고 (b) 화면 표시 쪽 한 곳(`HalconDisplayService.RenderDatumFindResult`)에 명시적 게이트를 추가하는 작업으로 좁혀진다. `TryComposeAlign`, `Action_FAIMeasurement`, 크로스-Z/사이클 판정 계층, TCP 응답 계층은 **전부 무변경**으로 충분하다 — 이들은 이미 "성공/실패" 불리언에만 반응하는 범용 게이트라서, detection 성공률이 올라가면 자동으로 혜택을 본다.

**Primary recommendation:** `DatumFindingService.cs` 안에 `TryFindVerticalTwoHorizontalDualImage` 진입부에서 새 `bool` 플래그를 검사해 신규 private 메서드(가칭 `TryFindVerticalTwoHorizontalDualImage_HorizontalOnly`)로 분기시키고, 그 메서드가 (1) Vertical `TryFindLine` 호출을 아예 생략, (2) Horizontal A+B 검출 + line-fit 은 기존과 100% 동일, (3) X 원점은 `AlignPreTransform`(이미 필드로 존재, `TryComposeAlign` 이 alignRigid 를 여기에 주입)을 taught `RefOriginRow/Col` 에 적용해 산출(§5 수식), (4) Y/각도는 그 X 에서 가로 결합선을 평가, (5) `DetectedRefAngle2 = 0.0`(기존 "미설정" sentinel 그대로 — §4 근거)을 유지하도록 구현한다. 호출부(`InspectionSequence`, `Action_FAIMeasurement`, `MainView`)는 전혀 손대지 않는다.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| 세로선 검출 skip 로직 | Algorithm (`Halcon/Algorithms/DatumFindingService.cs`) | — | 검출 알고리즘 그 자체의 분기이므로 단일 서비스 클래스 안에서 완결되어야 함 |
| 옵션 플래그 저장/노출 | Data Model (`Custom/Sequence/Inspection/DatumConfig.cs`) | UI PropertyGrid (ICustomTypeDescriptor) | INI 직렬화 + Algorithm-conditional visibility 는 이미 DatumConfig 가 소유한 책임(`IsHiddenForAlgorithm`) |
| Align 성공/실패 게이트, `_datumTransforms` 관리 | Sequence (`Custom/Sequence/Inspection/InspectionSequence.cs`) | — | **무변경** — 이미 범용 success/fail 게이트라 하위 알고리즘 변경에 자동 대응 |
| Datum 실행 오케스트레이션(auto cycle) | Sequence Action (`Custom/Sequence/Inspection/Action_FAIMeasurement.cs`) | — | **무변경** — `IsPatternAlignEnabled` 라우팅은 그대로 유지 |
| 수동 Test Find UI | Presentation code-behind (`UI/ContentItem/MainView.xaml.cs`) | — | **무변경** — 이미 `TryComposeAlign` 을 호출하는 동일 경로 |
| 런타임 Find 결과 표시(십자/기준선) | Display Service (`Halcon/Display/HalconDisplayService.cs`) | — | `RenderDatumFindResult` 1곳에 명시적 플래그 게이트 추가 필요(§9) |
| 캡처 렌더(저장 이미지 오버레이) | Display Service (`Halcon/Display/OverlayCaptureRenderer.cs`) | Sequence Action (`Action_FAIMeasurement.BuildDatumCaptureSnapshot`) | **무변경으로 충분** — 이미 `DetectedRefAngle2 != 0.0` sentinel 로 게이트됨(§9) |

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-76-01. 옵션 단위 = Datum 별.** `DatumConfig` 속성(레시피 저장). `AlgorithmType == VerticalTwoHorizontalDualImage` 인 Datum 의 속성창에만 노출(사실상 SIDE 1~4 전용). 면별 독립 on/off. INI 키 부재 시 `false`(현재 동작과 동일) — 계획 단계에서 `Load` 경로가 bool 키 부재를 실제로 false 로 처리하는지 확인할 것.
- **D-76-02. 옵션 ON 이면 세로는 항상 무시.** 세로 이미지가 잘 찍혀도 세로선 검출 자체를 돌리지 않는다. "성공하면 쓰고 실패하면 폴백" 방식 금지(반복성 우선, 사이클마다 X 보정 출처가 바뀌는 것 방지).
- **D-76-03. 세로 이미지는 계속 찍는다 — z index/PLC 프로토콜 불변.** `ZIndexB` 촬영·저장 그대로. 응답도 기존 datum index 와 동일.
- **D-76-04. Datum Find 는 OK — 자동 사이클과 수동 Test Find 모두.** 옵션 ON 이면 가로선+패턴매칭 성공 시 datum OK. 자동/수동 동일 규칙(수동이 다르게 동작하면 현장 혼란).
- **D-76-05. 원점 X = 패턴매칭, 원점 Y·각도 = 가로선.** X(열) = 패턴매칭 `alignRigid` 로 이동시킨 위치("매칭은 무조건 할꺼야" — SIDE 는 매칭 항상 사용). Y(행) = 그 X 에서의 가로 결합선의 행 좌표. 각도 = 가로 결합선 각도(지금과 동일). 계획 단계에서 정확한 산출식 결정(후보: `RefOriginCol` 을 `alignRigid` 로 옮긴 열 vs `RefOriginCol + dCol_match`) — 본 연구 §5 에서 결정 완료.
- **D-76-06. View 는 가로선만.** 옵션 ON 이면 세로선·세로 에지점·세로 기준 방향(원점 십자의 세로 팔 포함)을 그리지 않는다. 근거 코드: `Halcon/Display/HalconDisplayService.cs:460` 이 `DetectedRefAngle2` 로 세로 방향을 그림. `config.Line1Detected_*` = 세로선, `config.Line2Detected_*` = 가로 결합선, `Vertical_DetectedEdgeRows/Cols` = 세로 에지점.

### Claude's Discretion (계획 단계에서 정할 것 — 사용자 결정 불필요, 기술 판단)

1. `DetectedRefAngle2`/`DatumAngle2Rad` 값 — 세로선 없을 때 무엇을 넣을지(후보: 가로각+π/2 vs 0). → 본 연구 §4 에서 결정 완료(0.0 권장, 근거 상충 분석 포함).
2. 옵션 ON 상태의 티칭 경로 — `RefOriginRow/Col/Angle` 산출 방법(세로가 흐린 상태에서도 티칭 가능해야 함). → 본 연구 §5-teach 에서 "변경 불필요" 결론.
3. 기존 티칭 데이터 재사용 — 이미 교점으로 티칭된 `RefOriginCol` 을 그대로 쓸 수 있는지. → 본 연구 §5 에서 "그대로 재사용 가능" 결론(재티칭 불필요 목표 달성).
4. `ValidateHorizontalVerticalAngles`(직각성 검증) — 옵션 ON 이면 건너뛴다(세로선이 없으므로). → 본 연구 §5 에서 "절반(수평 방향성 체크)만 재사용, 직각성 체크는 skip" 권장.
5. 오프라인 이미지 자동 채움 — 정책 "측정은 NG 여도 저장, datum 은 OK 일 때만 저장"(`SystemSetting.AutoFillOfflineImages`). 옵션 ON 으로 datum 이 OK 가 되면 흐린 세로 이미지도 저장 대상. → 본 연구 §7 에서 "현행 유지 권장" 결론.
6. 패턴매칭 실패 시 — 기존 동작(`MarkAlignFailed`, lenient D-10) 유지 여부. → 본 연구 §6 에서 "이미 현재 동작이 그러하며 추가 작업 불필요" 결론.

### Deferred Ideas (OUT OF SCOPE)

없음 — CONTEXT.md 에 별도 "Deferred Ideas" 섹션 없음(전량 Locked Decisions + Claude's Discretion 으로 구성).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SDV-01 | Datum 별 "세로선 끄기" 옵션(레시피 저장, UI 노출) | §8 (bool INI 하위호환 검증) + §DatumConfig 터치포인트(신규 `[Category("Datum|Algorithm")]` bool, `IsHiddenForAlgorithm` 확장) |
| SDV-02 | 옵션 ON 시 가로선+패턴매칭 전용 datum 산출(자동+수동 Find 모두 OK) | §1(단일 choke point 확인) + §3(transform 아키텍처) + §5(정확한 산출식) |
| SDV-03 | 옵션 ON 시 View 는 가로선만 표시 | §9(RenderDatumFindResult 게이트 + OverlayCaptureRenderer sentinel 자동 대응) |
| SDV-04 | 옵션 OFF 는 기존 동작과 완전 동일 — TOP/BOTTOM/기존 SIDE 회귀 0 | §10(회귀 표면 — 신규 분기 진입점 1곳, 기존 코드 라인 무변경) |
</phase_requirements>

## Touch-Point Table

| File:Line | 세로선으로 하는 일(현재) | H-only 모드가 해야 할 일 | 놓치면 위험도 |
|---|---|---|---|
| `Halcon/Algorithms/DatumFindingService.cs:625-818` `TryFindVerticalTwoHorizontalDualImage` | `TryFindLine`(Vertical ROI) 호출 → 실패 시 즉시 `return false` | 진입부에서 옵션 플래그 분기 → 신규 private H-only 메서드로 위임(Vertical `TryFindLine` 호출 자체를 생략) | **HIGH** — 이 한 줄의 실패가 사용자가 겪는 버그의 root cause |
| `Halcon/Algorithms/DatumFindingService.cs:1400-1561` `TryTeachVerticalTwoHorizontalDualImage` | Teach 시 양쪽 라인 검출 → `RefOriginRow/Col/RefAngleRad` 저장 | **무변경.** §5-teach 근거로 Teach 는 옵션과 무관 | LOW — 잘못 건드리면 티칭 회귀만 유발, 원래 스코프 아님 |
| `Custom/Sequence/Inspection/InspectionSequence.cs:2926-3052` `TryComposeAlign`(5-arg) | ④단계에서 `detectSvc.TryFindDatum(refImage, refImageVertical, ...)` 호출 → 실패 시 `return false`("datum 검출 실패(패턴 보정 후)") | **무변경.** 내부에서 호출하는 `DatumFindingService.TryFindDatum`(2-image)이 알아서 H-only 분기를 타면 이 함수는 그대로 성공 반환 | **HIGH if 수정 시도** — 이 함수를 건드리면 `_datumTransforms[datumKey]=alignRigid`(측정 위치 결정 로직, L3044/3049) 회귀 위험. 손대지 않는 것이 정답 |
| `Custom/Sequence/Inspection/Action_FAIMeasurement.cs:388-407` `RunDatumDualImageDetection` | `IsPatternAlignEnabled` 시 `TryComposeAlign` 호출, 실패 시 `MarkAlignFailed` | **무변경** | MED if 수정 — 자동 사이클의 유일한 진입점, 손대면 회귀 폭 큼 |
| `UI/ContentItem/MainView.xaml.cs:4403-4528` `BtnTestFindDatum_Click` | align-enabled 시 동일 `TryComposeAlign(datum, imgH, imgV, modelPath, out error)` 호출(자동 사이클과 100% 동일 함수) | **무변경** | MED — 이미 자동 경로와 공유되므로 별도 구현 시 D-76-04("수동=자동 동일 규칙") 위반 |
| `Custom/Sequence/Inspection/Action_FAIMeasurement.cs:1841-1875` `InjectDatumOrigin` | `dc.DetectedOriginRow/Col/DetectedRefAngle/DetectedRefAngle2` → `IDatumOriginConsumer` 필드로 주입(모든 measurement, 매 사이클) | **무변경.** H-only 검출이 이 필드들을 올바르게(§5 수식) 채우기만 하면 됨 | MED — 필드 값이 틀리면 모든 SIDE 측정의 Y-거리 계산이 틀어짐(교점이 실제 가로선 위에 있어야 함) |
| `Measurements/EdgeToLineDistanceMeasurement.cs:141-199` | SIDE 18건(`MeasureAxis=Y`) — `DatumOriginRow/Col + DatumAngleRad` 로 수선의 발 계산. `useAngle2 = measureX && (DatumAngle2Rad != 0.0)`(X 일 때만 Angle2 사용, guard 있음) | **무변경.** X 축 미사용이라 Angle2 guard 는 항상 false 유지, 값 자체는 영향 없음(단, §5 기하학적 이유로 origin 이 실제 가로선 위에 있어야 정답) | LOW(SIDE 현재), 하지만 §4 참고 |
| `Measurements/EdgeToLineAngleMeasurement.cs` | SIDE 7건 — `DatumAngleRad` 만 사용, `DatumAngle2Rad` 미사용 | **무변경** | NONE |
| `Measurements/{CircleCenterDistance,CompoundCenterBDistance,CompoundCenterCDistance,ArcEdgeDistance,ArcLineIntersectDistance}.cs` | `MeasureAxis=="X"` 시 `measureLineAngle = DatumAngle2Rad` — **`!=0.0` guard 없음.** SIDE 현재 0건 사용(레시피 검증) | **무변경(코드) + 문서화(Common Pitfall).** `DetectedRefAngle2=0.0` 유지로 자연히 안전(측정이 "설정 안 됨"을 의미하는 0 을 쓰게 되지만, 이 타입들은 SIDE 에 존재하지 않으므로 현재 영향 0) | MED(미래) — SIDE 에 이 타입 X축 측정이 향후 추가되면 조용히 틀린 각도(0=수평)를 X 기준으로 사용하게 됨. Common Pitfall 로 문서화 필요 |
| `Halcon/Display/HalconDisplayService.cs:394-485` `RenderDatumFindResult` | L450-470 이 `DetectedRefAngle2` 의 `Sin/Cos` 로 "세로 기준선"(slate blue, 이미지 전체 길이)을 그림. **`!=0.0` guard 없음** — `Sin(0)=0,Cos(0)=1` 이라 `vDirLen=1`(>1e-6) 이 되어 **각도 0 이어도 선이 그려짐**(가로 방향처럼 보이는 선이지만 D-76-06 의도상 "세로 기준 방향"은 아예 안 그려야 함) | **명시적 플래그 게이트 추가 필수** — `DetectedRefAngle2` 값에 의존하지 말고 `datum.<신규플래그>` 를 직접 검사해 L450-470 블록 전체를 skip | **HIGH** — 이 한 곳이 D-76-06 미충족의 유일한 실질 위험 지점(sentinel 값만으로는 안 막힘) |
| `Halcon/Display/HalconDisplayService.cs:1279-1330` `RenderDatumDetectedOverlay`(teach-gated) | `LastTeachSucceeded` 시에만 Line1Detected(yellow)/Vertical raw points(orange) 그림 | **무변경.** `MainResultViewerControl.xaml.cs:1200-1210` 이 align-enabled datum(`correctedDatumActive=true`)일 때 이 함수를 아예 호출하지 않고 `RenderDatumFindResult` 만 호출 — SIDE 는 항상 align-enabled 이므로 production 결과화면에서 이 경로 도달 안 함 | LOW — MainView 편집/티칭 화면에서만 보임(스코프 밖) |
| `Halcon/Display/OverlayCaptureRenderer.cs:311-335` `DrawDatumRegions` + `Action_FAIMeasurement.cs:1373-1404` `BuildDatumCaptureSnapshot` | `cap.HasAxis2 = true`(→ cyan 세로축 캡처 렌더)는 `dc.DetectedRefAngle2 != 0.0` 일 때만 set(L1396) | **무변경 — `DetectedRefAngle2=0.0` 유지가 이 게이트를 자동으로 만족시킴** | **HIGH if `DetectedRefAngle2` 를 0 이 아닌 값(예: 가로각+π/2)으로 설정하면** — 저장된 캡처 이미지에 세로 기준선이 그려져 D-76-06 위반(사용자가 나중에 리뷰어에서 봄) |
| `Custom/Sequence/Inspection/DatumConfig.cs:1277-1313` `IsHiddenForAlgorithm` | 알고리즘별 PropertyGrid 필드 hide 스위치(기존 필드들의 확립된 패턴) | 신규 bool 을 `TwoLineIntersect`/`CircleTwoHorizontal`/`VerticalTwoHorizontal` 3개 case 에 `if (name=="<Flag>") return true;` 추가(= `VerticalTwoHorizontalDualImage` case 에는 추가 안 함 → 그쪽만 노출) | MED — 빠뜨리면 TLI/CTH/VTH 에도 의미 없는 옵션이 보여 D-76-01 위반 |
| `Sequence/Param/ParamBase.cs:396-399` + `Utility/Ini.cs:153-159,953-960` | Boolean Load: `IniSection` 인덱서가 키 부재 시 `IniValue.Default`(내부 `Value=null`) 반환 → `ToBool(false)` → **false** | **무변경 — 이미 안전.** DatumConfig.Load() 오버라이드 불필요(Int32 인 `ZIndexA/B`/`DatumZIndex` 와 달리 Boolean 케이스는 버그 없음) | NONE(검증됨, §8) |
| `Custom/Sequence/Inspection/Action_FAIMeasurement.cs:1129` `TryGrabOrLoadDualDatumImages` | ZIndexB 세로 이미지 촬영/로드(그대로 유지) | **무변경** — D-76-03 요구사항이 이미 만족됨(그랩 코드에 손 안 댐) | NONE |
| `Custom/Sequence/Inspection/Action_FAIMeasurement.cs:361-372` `AutoFillDatumOfflineImage` | detect 성공 시 세로 이미지도 오프라인 채움 대상으로 저장 | **무변경 권장**(§7) | LOW |

## §1. 모든 Datum 검출 호출 지점 (SDV-02 관련)

`grep -rn "TryFindDatum\|TryTeachDatum"` 전수 조사 결과, `EDatumAlgorithm.VerticalTwoHorizontalDualImage` + `IsPatternAlignEnabled=True`(SIDE 4개 전부 해당, §3 레시피 검증) 조합에서는 **모든 런타임 경로가 단 하나의 함수로 수렴**한다: `InspectionSequence.TryComposeAlign(datum, imgH, imgV, modelPath, out error)` (`InspectionSequence.cs:2926`).

| 호출 경로 | 파일:라인 | 세로 이미지 전달 방식 | 실패 시 |
|---|---|---|---|
| 자동 검사 사이클 | `Action_FAIMeasurement.cs:391` (`RunDatumDualImageDetection`) | `imgH,imgV` = `TryGrabOrLoadDualDatumImages` 결과 | `MarkAlignFailed`(lenient NG, D-10), `RuntimeDetectFailed=true` |
| 수동 Test Find(Datum Find 버튼) | `MainView.xaml.cs:4454` (`BtnTestFindDatum_Click`) | `TeachingImagePath`/`TeachingImagePath_Vertical` 파일 직접 로드 | 모달 "Find 실패" + `ClearDatumFindResultOverlay` |
| Test Find(모델 인스턴스 없을 때 폴백) | `MainView.xaml.cs:4458` | 동일 | `svc.TryFindDatum` 직접 호출(2-image, align 안 거침) — `seq==null`(비정상 상황)일 때만 도달 |
| 재앵커(Re-anchor) | `MainView.xaml.cs:1146-1149` | — | **DualImage datum 은 명시적으로 미지원**(`"DualImage datum 은 자동 재앵커 미지원입니다"`) — 터치포인트 아님 |
| Teach(티칭) | `MainView.xaml.cs:3889`(마법사) / `MainView.xaml.cs:4193`(패턴모델 생성 시 ref pose 기록) | `TeachingImagePath`/`_Vertical` | `TryTeachDatum`(2-image) — align 과 무관한 별도 함수, **H-only 옵션과 무관**(§5-teach) |
| 휘발 좌표 복원(RestoreDatumOverlayFromTeach / ShowResultDatumOverlays) | `MainView.xaml.cs:2178-2221` `TryRestoreDatumGeometry` | 티칭 이미지 재사용 | `TryTeachDatum` 조용히 재실행(모달 없음) — **production 과 무관**, `d.LastFindSucceeded==false` 일 때만 실행(레시피 로드 직후 등) |
| 결과 리뷰어(MainResultViewerControl) | 별도 검출 호출 없음 — `_datumFindResultOverlay`/`_resultDatumOverlays` 는 검사 시점에 채워진 `DatumConfig` 스냅샷을 그대로 렌더 | — | — |
| 오프라인/저장 이미지 재검사(quick-260911-fia "저장 사진 재검사") | 별도 grep 결과 없음 — `TryGrabOrLoadDualDatumImages`/`AcquireShotImage` 가 SIMUL/오프라인 모드에서 이미지 소스만 바꾸고 검출 경로는 동일(`RunDatumDualImageDetection`) 재사용 | 동일 | 동일 |

**결론:** 계획 단계에서 "모든 진입점을 H-only 로 라우팅"할 필요가 없다. `DatumFindingService.TryFindVerticalTwoHorizontalDualImage`(그리고 그 호출자인 `TryFindDatum` 2-image 오버로드) 딱 한 곳만 고치면, `TryComposeAlign` 을 거치는 **자동 사이클 + 수동 Test Find 전부**가 동시에 커버된다(D-76-04 요구사항과 정확히 일치).

## §2. 검출 후 값을 읽는 모든 지점 (세로선 관련 필드)

`Vertical_DetectedEdgeRows/Cols`, `Line1Detected_*`(VTH-DualImage 에서는 세로선 raw 표시용), `DetectedRefAngle2`, `DetectedOriginCol` 의 전체 소비자:

- **`IDatumOriginConsumer` 경유(모든 측정 타입 공통)**: `Action_FAIMeasurement.cs:1841-1875` `InjectDatumOrigin` 이 `DetectedOriginRow/Col/DetectedRefAngle/DetectedRefAngle2/DetectedCircleRow/Col` 을 매 사이클 주입. 소비자는 `Measurements/*.cs` 전체 10+ 타입이지만, **`DatumAngle2Rad` 를 실제로 읽는 타입은 5개**(`EdgeToLineDistance`(guard 있음), `ArcEdgeDistance`/`CircleCenterDistance`/`CompoundCenterBDistance`/`CompoundCenterCDistance`/`ArcLineIntersectDistance`(guard 없음)) 이고, **SIDE 레시피에는 이 5개 타입이 0건**(§4 검증).
- **표시(`RenderDatumFindResult`)**: `DetectedRefAngle2` 로 세로 기준선을 그림(§9, guard 없음 — 신규 플래그로 직접 게이트해야 함).
- **캡처 렌더(`OverlayCaptureRenderer.DrawDatumRegions`)**: `DetectedRefAngle2 != 0.0` sentinel 로 게이트됨(§9).
- **티칭 편집 오버레이(`RenderDatumDetectedOverlay`)**: `Line1Detected_*`/`Vertical_DetectedEdgeRows/Cols` 를 그리지만 `LastTeachSucceeded` 게이트 + align-enabled datum 은 production 뷰어에서 호출 안 됨(§9).
- **Export/CSV/xlsx**: `grep -rln` 결과 `CycleResultDto`/`ExcelExportService`/리뷰어 export 코드 어디에도 이 필드들이 등장하지 않음 — **export 계층은 터치포인트 아님**(측정 "결과값"만 export 하지, datum 의 raw 검출 좌표는 export 하지 않음).
- **트리 배지(`InspectionListView.xaml.cs`)**: `DetectedEdgeCount = 0` 리셋만 존재(검출 재시작 시) — 세로선 특정 로직 없음, 무변경.

## §3. H-only 모드에서 실제로 바뀌는 것 — transform 아키텍처 (가장 중요한 발견)

`InspectionSequence.TryComposeAlign`(`InspectionSequence.cs:2926-3052`) 을 라인 단위로 추적한 결과:

```
① svc.TryFindPose(refImage, ...) → curRow,curCol,curAngleDeg          [패턴 매칭 1]
②-2 (선택) 패턴2 baseline θ 재계산                                     [패턴 매칭 2]
③ alignRigid = HomMat2dRotate(θ, RefMatchRow, RefMatchCol) 후 Translate(dRow,dCol)   [L2994-3000]
④ detectSvc.AlignPreTransform = alignRigid;
   detectSvc.TryFindDatum(refImage, refImageVertical, datum, out datumTransform, ...)  [L3011-3023]
   → 실패 시 return false (ALIGN_FAIL)                                  ← 세로선 실패가 사용자 버그의 원인
   → 성공해도 datumTransform 은 datum.CurrentTransform 에 "임시로만" 대입(L3029)
⑤ datum.CurrentTransform = alignRigid;             [L3049 — datumTransform 을 덮어씀]
   _datumTransforms[datumKey] = alignRigid;         [L3044 — 측정이 실제로 쓰는 값]
```

즉 **측정 ROI 위치를 결정하는 `_datumTransforms[datumKey]` 는 `alignRigid`(패턴매칭 결과)뿐이며, ④단계의 라인 검출(`datumTransform`)은 최종 대입 직전에 버려진다.** 이는 코드 주석에도 명시되어 있다(`InspectionSequence.cs:3036-3039`: "측정 transform 을 검출-datum 대신 패턴 pose(alignRigid)로 전환... 검출-datum transform 은 tilt 서 패턴(정답) 대비 ~130px 어긋남").

**함의:** H-only 모드로 세로선 검출을 꺼도 **측정 ROI 의 실제 이동/판정 위치는 전혀 바뀌지 않는다** — 이미 patttern-only 로 결정되고 있었기 때문이다. 우리가 실제로 고쳐야 하는 것은 ④단계가 세로선 실패로 `return false` 하지 않게 만드는 것, 그리고 ④단계가 정상 반환할 때 채우는 `DetectedOrigin*`/`DetectedRefAngle*` 값(측정 위치가 아니라 §2 에서 나열한 부수 소비자들이 읽는 값)을 합리적으로 채우는 것뿐이다.

이 발견은 D-76-05 의 "원점 X = 패턴매칭"이라는 사용자 결정과 완전히 일치한다 — 사실 **측정 위치는 이미 100% 패턴매칭 기준이었고**, 이번 phase 가 바꾸는 것은 "그 사실을 세로선 검출 실패가 더 이상 가리지 못하게" 하는 것뿐이다.

## §4. `DetectedRefAngle2`/`DatumAngle2Rad` 값 — 0.0 권장 (충돌 분석 포함)

CONTEXT.md 는 후보로 "가로각+π/2"(직교 가정) vs "0(미설정 표식)"을 제시했다. 두 후보를 코드베이스 전체 소비자 기준으로 대조한 결과:

| 소비자 | `!=0.0` guard 있음? | `0.0` 선택 시 | `curAngle+π/2` 선택 시 |
|---|---|---|---|
| `EdgeToLineDistanceMeasurement.cs:151` | 있음(`useAngle2 = measureX && (DatumAngle2Rad != 0.0)`) | 안전(X 미사용이므로 무관) | 안전(둘 다 무관) |
| `CircleCenterDistanceMeasurement.cs:149-152`, `CompoundCenterBDistanceMeasurement.cs:126-129`, `CompoundCenterCDistanceMeasurement.cs:126-129`, `ArcEdgeDistanceMeasurement.cs:125-128`, `ArcLineIntersectDistanceMeasurement.cs:251-254` | **없음** | SIDE 에 0건이라 현재 무해. 미래에 X축 측정 추가 시 "설정 안 됨"이 명백(0=horizontal 을 X기준으로 쓰면 결과가 확실히 이상해서 QA 단계에서 발견됨) | 미래에 X축 측정 추가 시 **검증되지 않은 근사값이 "진짜 측정된 값"처럼 조용히 사용됨** — 발견하기 더 어려움 |
| `Action_FAIMeasurement.cs:1396` `BuildDatumCaptureSnapshot`(→ `OverlayCaptureRenderer.DrawDatumRegions`) | **있음**(`if (dc.DetectedRefAngle2 != 0.0) cap.HasAxis2 = true;`) | **세로 기준선이 캡처 이미지에 안 그려짐 → D-76-06 자동 충족** | **세로 기준선이 캡처 이미지에 그려짐 → D-76-06 위반**(저장된 검사 이미지에 검증 안 된 선이 나타남) |
| `HalconDisplayService.cs:453-470` `RenderDatumFindResult` | **없음**(`Sin(0)=0,Cos(0)=1` → `vDirLen=1` → 값 무관하게 그려짐) | 선이 그려지긴 하지만(각도 0=거의 가로 방향) 신규 플래그로 별도 게이트 필요(§9) — **어느 값을 넣어도 이 지점은 명시적 게이트가 필요** | 동일 |

**결론: `DetectedRefAngle2 = 0.0`(기존 "미설정" sentinel)을 그대로 유지할 것을 권장.** 결정적 근거는 `Action_FAIMeasurement.cs:1396` 의 캡처 렌더 게이트다 — 이 게이트는 **이미 0.0 을 "세로축 없음"으로 해석해서 자동으로 D-76-06 을 만족**시킨다. 반대로 `curAngle+π/2` 를 넣으면 코드 수정 없이도 저장된 검사 이미지(리뷰어에서 사용자가 보는 이미지)에 세로선이 다시 나타나 D-76-06 을 위반하게 된다. `RenderDatumFindResult` 하나는 값과 무관하게 신규 플래그로 직접 게이트해야 하므로(§9), 값 선택이 그 지점의 해결책이 되지는 못한다.

**Common Pitfall 로 문서화 필요:** `CircleCenterDistance`/`CompoundCenterB/C`/`ArcEdgeDistance`/`ArcLineIntersectDistance` 타입에 `MeasureAxis=X` 를 SIDE 의 H-only Datum 에 설정하면 `DatumAngle2Rad=0.0`(수평각)을 X 기준선으로 써서 조용히 틀린 결과를 낸다. 현재 SIDE 레시피에는 이 조합이 존재하지 않지만(§ recipe 검증), 향후 이 조합을 막는 UI 경고나 최소한 주석 경고를 권장.

## §5. H-only 원점 산출식 (D-76-05 계획 단계 질문 해결)

**후보 (i) `RefOriginCol` 을 `alignRigid` 로 옮긴 열 vs 후보 (ii) `RefOriginCol + dCol_match`.**

`alignRigid = HomMat2dRotate(θ, RefMatchRow, RefMatchCol) 후 Translate(dRow,dCol)`(`InspectionSequence.cs:2994-3000`, θ=패턴 회전각, dRow/dCol=패턴 이동량). 후보 (ii)는 순수 평행이동만 반영하고 회전(θ)에 의한 lever-arm 효과(`RefMatch` 중심과 `RefOrigin` 사이 거리 × sin(θ))를 무시한다. 부품이 회전 없이 순수 평행이동만 한다면 두 후보는 같지만, 회전이 있으면 (ii)는 `RefOrigin` 이 `RefMatch` 에서 먼 Datum 일수록 오차가 커진다(TOP/BOTTOM 이 아니라 SIDE 는 실제로 `RefMatch`~`RefOrigin` 거리가 상당함 — 운영 레시피에서 `RefMatchRow≈6410`, `RefOriginRow`(Teach 시 산출값, INI 미직접확인이나 유사 규모 예상) 등 대각 위치).

**후보 (i) 가 정답이며, 이미 코드베이스에 확립된 패턴과 동일하다.** `DatumFindingService.cs:259-272`(`TryFindCircleTwoHorizontal`)가 정확히 같은 일을 한다:

```csharp
// Source: DatumFindingService.cs:259-272 (기존 CircleTwoHorizontal Find 경로)
double circRoiRow = config.CircleROI_Row;
double circRoiCol = config.CircleROI_Col;
if (AlignPreTransform != null && AlignPreTransform.Length > 0)
{
    HTuple acR, acC;
    HOperatorSet.AffineTransPoint2d(AlignPreTransform, circRoiRow, circRoiCol, out acR, out acC);
    circRoiRow = acR.D;
    circRoiCol = acC.D;
}
```

이는 "ROI 중심점을 `AlignPreTransform`(=`alignRigid`)으로 옮긴다"는 정확히 동일한 연산이며, `AlignPreTransform` 은 `TryComposeAlign` 이 이미 `DatumFindingService` 인스턴스에 주입해 두는 필드(`InspectionSequence.cs:3011`)이므로 **새 코드는 기존 필드를 그대로 재사용**할 수 있다.

**권장 구현(H-only 전용 private 메서드 내부):**

```csharp
// 신규: RefOriginRow/Col(티칭 원점)을 AlignPreTransform 으로 이동 — 기존 CTH ROI 이동 패턴과 동일 규약(§5)
double xOriginRow = config.RefOriginRow;
double xOriginCol = config.RefOriginCol;
if (AlignPreTransform != null && AlignPreTransform.Length > 0)
{
    HTuple tR, tC;
    HOperatorSet.AffineTransPoint2d(AlignPreTransform, xOriginRow, xOriginCol, out tR, out tC);
    xOriginCol = tC.D; // X(열) = 매칭이 이동시킨 열 (D-76-05)
}
// Horizontal A+B 검출 + FitLineContourXld 는 기존 로직 그대로 (hrB,hcB,hrE,hcE 산출)
double curAngle = Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D);
// Y(행) = 그 X 에서 가로 결합선 위의 행 좌표 (D-76-05) — 수평선이므로 hcE!=hcB 가정(가드 필요)
double curRow;
if (Math.Abs(hcE.D - hcB.D) > 1e-6)
    curRow = hrB.D + (xOriginCol - hcB.D) * (hrE.D - hrB.D) / (hcE.D - hcB.D);
else
    curRow = hrB.D; // 극단적 수직 결합선(비정상) 폴백 — 발생 시 HORIZONTAL_TOLERANCE_DEG 체크가 먼저 걸러낼 것
```

`DetectedOriginRow = curRow`, `DetectedOriginCol = xOriginCol`, `DetectedRefAngle = curAngle`, `DetectedRefAngle2 = 0.0`(§4).

**horizontal-only 방향성 체크(권장, 필수는 아님):** `ValidateHorizontalVerticalAngles`(`DatumFindingService.cs:1567-1599`)는 수평 방향성 체크(`HORIZONTAL_TOLERANCE_DEG=15°`, 세로선과 무관)와 직각성 체크(세로선 필요)를 한 함수에서 같이 한다. H-only 메서드는 이 함수를 그대로 호출할 수 없지만(vertPhiRad 인자가 없음), **수평 방향성 체크만 인라인으로 복제**해서 유지할 것을 권장 — `hcE.D - hcB.D` 가 거의 0 이 되는(수직에 가까운 "가로선" 오검출) 극단 케이스를 사전에 막아준다. `PERPENDICULAR_TOLERANCE_DEG` 체크(직각성)는 세로선이 없으므로 완전히 skip(D-76 plan-Q4 그대로 채택).

**`ExpectedAngleDeg`/`AngleTolerance` 게이트(`DatumFindingService.cs:772-785`)는 이미 가로각(`curAngle`)만 사용** — 세로선과 무관하므로 H-only 메서드에도 동일하게 이식 권장(회귀 없음, 오히려 SIDE 에서 이 게이트가 계속 동작해야 UI 배지 일관성 유지).

### §5-teach. 티칭 경로 — 변경 불필요 (Claude's Discretion #2, #3 해결)

`TryTeachVerticalTwoHorizontalDualImage`(`DatumFindingService.cs:1400-1561`)가 저장하는 값은 `RefOriginRow/Col/RefAngleRad`(정적 티칭 기준, 세로+가로 양쪽 라인이 필요) 뿐이다. **런타임 H-only Find 는 이 값들을 "델타 기준점"으로만 읽는다** — §5 의 `AlignPreTransform` 산출식이 `config.RefOriginRow/Col` 을 입력으로 쓰므로, **기존에 이미 성공적으로 티칭된 레시피의 `RefOriginRow/Col/RefAngleRad` 값을 그대로 재사용**하면 재티칭이 필요 없다(목표 4 달성). 세로 이미지가 흐려지는 것은 운영 중(포커스 드리프트) 발생하는 문제이지 최초 티칭 시점의 문제가 아니라고 가정하는 것이 합리적이다(티칭은 드물게, 대개 설비 셋업 직후 수행). 따라서:

- Teach 함수(`TryTeachVerticalTwoHorizontalDualImage`) 자체는 **무변경**.
- `TryRestoreDatumGeometry`(`MainView.xaml.cs:2178-2221`, 휘발 좌표 복원용)도 **무변경** — 이는 항상 티칭 이미지 파일로 `TryTeachDatum` 을 재실행하는 UI 전용 헬퍼이며 production 경로와 무관.
- **[ASSUMED] 리스크:** 만약 세로 이미지가 *영구적으로*(설비 자체가 세로축 조명/포커스를 완전히 상실) 나빠져 재티칭 자체가 필요해지는 상황이라면, 현재 Teach 함수는 여전히 양쪽 라인을 요구하므로 재티칭이 불가능해진다. 이 케이스는 CONTEXT.md 의 "재티칭 불필요가 목표"라는 목표와 상충하지 않지만(목표는 "재티칭이 필요 없게 하자"이지 "재티칭이 절대 불가능해도 된다"가 아님), 만약 사용자가 이 시나리오도 염두에 둔다면 Teach 쪽에도 H-only 분기가 필요하다 — **본 phase 의 6개 잠금 결정 어디에도 이 요구가 명시되어 있지 않으므로 범위 밖으로 간주**하고 Open Question 으로 남긴다.

## §6. 패턴매칭 실패 시 처리 (Claude's Discretion #6 해결)

`TryComposeAlign` 의 ①단계(`svc.TryFindPose`, `InspectionSequence.cs:2938-2947`)는 **모든 align-enabled datum 에서 세로선 검출보다 먼저 실행**되며, 실패 시 즉시 `return false`(→ 호출부가 `MarkAlignFailed`, lenient D-10)한다. 이는 H-only 옵션 유무와 **완전히 무관하게 이미 오늘 일어나는 동작**이다 — H-only 모드를 추가해도 이 ①단계 실패 처리 로직에는 손댈 필요가 없다. CONTEXT.md 의 "계획 단계에서 정할 것 #6"은 사실상 **이미 답이 정해져 있다**: 패턴매칭 실패 = 오늘도 내일도 `MarkAlignFailed`(D-10 lenient, NG 강제 + 사이클 계속).

## §7. 오프라인 이미지 자동 채움 (Claude's Discretion #5 해결)

`Action_FAIMeasurement.cs:360-372`(`ProcessDatumDualImage`): `bAutoFillDatum = bDetectOk && bCrossZLiveCaptured && IsOfflineAutoFillEnabled() && IsLiveCaptureMode()` → true 이면 `AutoFillDatumOfflineImage(datum, imgV, ..., true)` 로 **세로(흐린) 이미지도 저장**한다. H-only 모드에서 `bDetectOk` 는 가로선+패턴 성공만으로 true 가 되므로, 이 흐린 세로 이미지가 "OK" 오프라인 베이스라인으로 저장될 것이다.

**권장: 현행 유지(코드 변경 없음).** 근거: (1) D-76-03 의 명시적 취지가 "세로 이미지는 나중에 포커스 문제 분석에 쓸 수 있게 계속 저장한다"이므로, 흐린 이미지가 저장되는 것 자체가 이 취지와 부합한다. (2) 오프라인 자동 채움은 "재검사용 캐시"이지 "품질 기준"이 아니다 — H-only Datum 은 애초에 세로 이미지 품질에 의존하지 않으므로 흐려도 재검사 결과에 영향이 없다. (3) 이 정책을 바꾸려면 `IsOfflineAutoFillEnabled`/`AutoFillDatumOfflineImage` 에 H-only 인식 분기를 추가해야 하는데, 이는 D-76 의 locked decision 어디에도 요구되지 않은 범위 확장이다.

## §8. 옵션 저장/노출 (SDV-01)

**INI 하위호환(bool 키 부재 → false) — [VERIFIED, 코드 3단 추적]:**
1. `Utility/Ini.cs:953-960` `IniSection.this[string name]` — 키 없으면 `IniValue.Default`(`Ini.cs:380-381`, `new IniValue()` → 내부 `Value` 필드 기본값 `null`) 반환.
2. `Utility/Ini.cs:153-159` `IniValue.ToBool(bool valueIfInvalid=false)` → `TryConvertBool` 이 `Value==null` 이면 `false` 반환 → `valueIfInvalid` 인 `false` 리턴.
3. `Sequence/Param/ParamBase.cs:396-399` `case "Boolean": bool bValue = loadFile[group][name].ToBool(); prop.SetValue(this, bValue);` — 인자 없이 호출하므로 `valueIfInvalid` 기본값 `false` 사용.

**중요: 이는 STATE.md `quick-260909-kl0`(`SystemSetting.Load()`)이 고친 Int32 버그와는 무관한 별개의 코드 경로다.** 그 커밋은 `SystemSetting.Load()`(다른 클래스, `case "Int32"` 케이스, `ToInt()` 기본값 0 이 코드 기본값을 덮어쓰는 문제)만 고쳤다. `DatumConfig`(→ `ParamBase.Load`)의 Boolean 케이스는 애초에 이 버그가 없다 — 이미 `IsPatternAlignEnabled`(`DatumConfig.cs:134`, 동일 bool 패턴)가 "INI 키 미존재 시 자동 false(D-11)"로 문서화되어 있고 실사용 중이다(`DatumConfig.cs:1217` 주석: "IsPatternAlignEnabled 는 bool — INI 키 미존재 시 자동 false(D-11). 별도 폴백 불필요."). **`DatumConfig.Load()` 오버라이드에 신규 필드를 위한 코드를 추가할 필요가 없다** — `ZIndexA`/`ZIndexB`/`DatumZIndex`(Int32) 와 달리.

**PropertyGrid algorithm-conditional 노출 — 기존 정확한 선례 존재:** `DatumConfig.cs:1277-1313` `IsHiddenForAlgorithm(string name, EDatumAlgorithm alg)`. `TeachingImagePath_Vertical`/`ZIndexA`/`ZIndexB`/`ExpectedAngleDeg`/`AngleTolerance` 가 이미 이 패턴으로 "`VerticalTwoHorizontalDualImage` 케이스에서만 hide 조건이 없어 노출되고, 나머지 3개 케이스(`TwoLineIntersect`/`CircleTwoHorizontal`/`VerticalTwoHorizontal`)에서 명시적으로 hide"된다(예: `DatumConfig.cs:1284,1292,1301`). 신규 필드도 동일 패턴으로 3개 case 에 `if (name == "<NewFlagName>") return true;` 를 추가하면 D-76-01 요구사항(VerticalTwoHorizontalDualImage 전용 노출)을 그대로 만족한다.

**권장 필드 배치:** `[Category("Datum|Algorithm")]`(`ExpectedAngleDeg`/`AngleTolerance`/`TwoLineAngleToleranceDeg` 와 동일 카테고리 — 알고리즘 동작을 바꾸는 스위치라는 의미가 일치) + `[System.ComponentModel.Description(...)]`. 이름은 계획 단계에서 확정(가칭 `DisableVerticalDetection` 또는 `HorizontalOnlyMode`).

## §9. View 표시 (SDV-03)

**production 결과화면에서 실제로 그려지는 함수는 `RenderDatumFindResult` 하나뿐**(align-enabled datum, `MainResultViewerControl.xaml.cs:1200-1210` — `correctedDatumActive=true`(항상 SIDE 해당) 분기가 `RenderDatumFindResult` 만 호출하고 `RenderDatumOverlay`(teach 오버레이 포함 버전)는 호출하지 않음).

`RenderDatumFindResult`(`HalconDisplayService.cs:394-485`) 내부 구성:
- L406-421 원점 크로스헤어("Find (row,col)" 라벨) — **유지**(방향성 없는 점 마커, D-76-06 이 금지하는 "선"이 아님).
- L423-434 `DetectedRefAngle` 화살표(가로 방향) — **유지**(가로 기준, H-only 에서도 유효).
- L442-449 가로 기준선(전체 이미지 길이, slate blue) — **유지**(가로 기준, H-only 에서도 유효·정확).
- **L450-470 "수직 기준선" 블록 — H-only 모드에서 반드시 skip 해야 함.** `DetectedCircleRow/Col` 이 0 이면(VTH-DualImage 는 항상 0) `vDirRow=Sin(DetectedRefAngle2), vDirCol=Cos(DetectedRefAngle2)` 로 방향을 잡고 `vDirLen>1e-6` 이면 그린다. **`DetectedRefAngle2=0.0` 이어도 `Sin(0)=0,Cos(0)=1` → `vDirLen=1` → 조건을 만족해 선이 그려진다.** 즉 §4 의 sentinel 값 선택과 무관하게 이 블록은 **항상 뭔가를 그린다** — 명시적으로 `datum.<신규플래그>` 를 검사해 이 블록 전체(L450-470)를 skip 하는 조건을 추가해야 D-76-06 을 만족한다.

**캡처 렌더(`OverlayCaptureRenderer.cs`, RawImageSaveService 가 저장하는 오버레이 이미지)는 §4 의 `DetectedRefAngle2=0.0` 선택만으로 자동 충족** — 별도 코드 수정 불필요(`Action_FAIMeasurement.cs:1396` 의 `!=0.0` 게이트가 이미 존재).

**편집/티칭 화면(`MainView.xaml.cs` 라이브 편집, `RenderDatumOverlay`→`RenderDatumDetectedOverlay`)은 D-76-06 스코프 밖**(§ touch-point table 근거) — 이 화면은 "티칭 데이터"를 보여주는 것이지 "이번 사이클의 런타임 검출"을 보여주는 것이 아니며, `LastTeachSucceeded` 게이트는 옵션과 무관하게 양쪽 라인이 티칭됐음을 전제하기 때문에 원래도 세로선이 보이는 게 맞다(티칭 자체는 무변경, §5-teach).

## §10. 회귀 표면(SDV-04) — 옵션 OFF 는 완전 동일

핵심 설계 원칙: **신규 분기는 `TryFindVerticalTwoHorizontalDualImage` 진입부 단 1곳에서만 추가되며, 기존 코드 라인은 단 한 줄도 수정되지 않는다.** 구체적으로:

```csharp
private bool TryFindVerticalTwoHorizontalDualImage(HImage imageHorizontal, HImage imageVertical, DatumConfig config, out HTuple transform, out string error)
{
    // 신규 1줄 삽입 지점 — 기존 본문(625-818) 은 100% 그대로 유지
    if (config.<NewFlag>)
    {
        return TryFindVerticalTwoHorizontalDualImage_HorizontalOnly(imageHorizontal, config, out transform, out error);
    }
    // ↓ 기존 코드 (라인 626-818) 무변경
    ...
}
```

이 설계라면:
- `config.<NewFlag>==false`(기본값, 기존 레시피 전부) → 기존 코드 경로가 **바이트 단위로 동일하게** 실행됨. TOP/BOTTOM(다른 `AlgorithmTypeEnum` 이라 이 함수 자체에 진입 안 함)과 기존 SIDE(플래그 false)는 회귀 0 이 코드 구조적으로 보장된다.
- 신규 함수(`TryFindVerticalTwoHorizontalDualImage_HorizontalOnly`)는 **완전히 새 코드**이므로 기존 로직과 상호작용하지 않는다.
- `TryComposeAlign`, `Action_FAIMeasurement`, `MainView.xaml.cs` Test Find, 크로스-Z/TCP 판정 계층 — **전부 무변경**(§1, §touch-point table 근거) → 이 계층들의 회귀 위험은 구조적으로 0.
- 유일하게 "기존 함수 내부에 조건을 추가"하는 지점은 `HalconDisplayService.RenderDatumFindResult`(§9) 와 `DatumConfig.IsHiddenForAlgorithm`(§8) 두 곳뿐이며, 둘 다 **새 `if` 분기 추가**(기존 조건 수정 아님)로 구현 가능 — `config.<NewFlag>==false` 일 때 새 `if` 조건이 항상 false 이므로 기존 동작 불변.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| 패턴매칭 보정 transform 을 임의 ROI/기준점에 적용 | 새로운 좌표 변환 유틸리티 | `HOperatorSet.AffineTransPoint2d(AlignPreTransform, row, col, out r, out c)` | `TryFindCircleTwoHorizontal`(`DatumFindingService.cs:259-272`)이 이미 동일 패턴을 사용 중 — 새 구현 대신 그대로 재사용 |
| 알고리즘별 PropertyGrid 필드 hide | 새 UI 필터링 메커니즘 | `DatumConfig.IsHiddenForAlgorithm` 스위치에 case 추가 | 이미 5개 알고리즘이 이 단일 스위치로 관리됨 — 별도 메커니즘 도입 시 두 개의 진실 공급원 생김 |
| bool 옵션의 INI 하위호환 처리 | `Load()` 오버라이드 신규 로직 | 아무것도 안 함 — bool 은 이미 안전(§8) | Int32 버그(quick-260909-kl0)와 혼동하지 말 것. bool 케이스는 검증 결과 문제 없음 |

## Common Pitfalls

### Pitfall 1: `RenderDatumFindResult` 의 sentinel-미체크 세로선 블록
**What goes wrong:** `DetectedRefAngle2` 를 "안전한" 값(0.0 이든 π/2 든)으로 세팅해도 `HalconDisplayService.cs:450-470` 블록은 `vDirLen>1e-6` 조건을 항상 통과해 선을 그린다(각도 0 이어도 `Sin(0)=0,Cos(0)=1` 은 유효한 방향벡터이기 때문).
**Why it happens:** 이 블록은 "값이 0 = 미설정"이라는 sentinel 관례를 지키지 않고 작성되었다(다른 소비자들과 다른 설계).
**How to avoid:** `DetectedRefAngle2` 값과 무관하게, 새 bool 플래그를 `RenderDatumFindResult` 안에서 직접 검사해 이 블록을 skip.
**Warning signs:** UAT 중 "옵션 ON 인데 화면에 옅은 세로선이 여전히 보인다"는 보고.

### Pitfall 2: `TryComposeAlign`/`_datumTransforms` 를 "세로선 없앴으니 다시 설계"하려는 시도
**What goes wrong:** 측정 ROI 위치는 이미 100% `alignRigid`(패턴매칭)로 결정되고 있다(§3). 이 사실을 모르고 `TryComposeAlign` 이나 `_datumTransforms` 대입 로직을 건드리면 TOP/BOTTOM 은 영향 없지만(다른 알고리즘) SIDE 의 기존 동작(옵션 OFF 포함)이 깨질 수 있다.
**Why it happens:** CONTEXT.md 의 "원점 X = 패턴매칭"이라는 표현이 "지금은 아니었는데 이제 그렇게 만든다"처럼 읽힐 수 있지만, 실제로는 "이미 그런데, 그 사실이 세로선 실패에 가려져 있었다"가 맞다.
**How to avoid:** 이 phase 의 코드 변경을 `DatumFindingService.cs` 내부(신규 private 메서드) + `DatumConfig.cs`(필드) + `HalconDisplayService.cs`(표시 게이트) 3개 파일로 한정.

### Pitfall 3: `MeasureAxis=X` 타입에 `DatumAngle2Rad` 무검증 소비
**What goes wrong:** `CircleCenterDistance`/`CompoundCenterBDistance`/`CompoundCenterCDistance`/`ArcEdgeDistance`/`ArcLineIntersectDistance` 는 `DatumAngle2Rad` 를 `!=0.0` 가드 없이 그대로 X축 기준선 각도로 쓴다(§4). SIDE 에는 현재 이 조합이 없지만, 향후 누군가 H-only SIDE Datum 에 이 타입 + `MeasureAxis=X` 측정을 추가하면 `0.0`(수평각)을 X 기준으로 써서 조용히 틀린 값을 낸다.
**Why it happens:** 이 5개 타입은 애초에 CTH(원 기반, `DetectedRefAngle2` 가 항상 실측 직교값)를 전제로 설계되어 `!=0.0` 가드가 불필요했다.
**How to avoid:** 계획 단계에서 이 조합에 대한 방어(경고 로그, PropertyGrid 검증, 또는 최소한 문서화)를 고려. 이번 phase 의 필수 범위는 아니지만 플래그로 기록.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | 사용자가 "재티칭 불필요" 목표에서 말하는 시나리오는 "세로축이 *운영 중* 흐려지는 것"이며 "티칭 시점부터 세로축이 영구적으로 사용 불가능한 것"은 포함하지 않는다 | §5-teach | 만약 후자도 의도했다면 Teach 함수에도 H-only 분기가 필요 — 계획 단계에서 사용자에게 재확인 권장 |
| A2 | 신규 옵션 필드명은 계획 단계에서 확정(`DisableVerticalDetection` 등은 예시 제안일 뿐) | §8, §10 | 이름 자체는 기능에 영향 없음, 순수 명명 이슈 |

**필드명 외에는 이번 연구에서 `[ASSUMED]` 로 남긴 사실 주장이 없음** — 모든 핵심 아키텍처 주장(transform 흐름, sentinel 게이트, INI bool 하위호환, 레시피 실측치)은 코드 직접 열람 또는 운영 레시피 grep 으로 `[VERIFIED]` 되었다.

## Open Questions

1. **신규 bool 필드/UI 문구 확정**
   - What we know: 배치 위치(`Category("Datum|Algorithm")`), hide 패턴(`IsHiddenForAlgorithm` 3-case 추가), INI 하위호환(안전) 모두 확정.
   - What's unclear: 정확한 프로퍼티명/한글 Description 문구.
   - Recommendation: 계획 단계에서 확정, 코드 영향 없음.

2. **`MeasureAxis=X` + H-only Datum 조합에 대한 방어 수준**
   - What we know: 현재 SIDE 레시피에 0건, `EdgeToLineDistance` 외 5개 타입은 무가드.
   - What's unclear: 이 phase 범위에 방어 코드(로그 경고/UI 검증)를 포함할지, 문서화만으로 충분한지.
   - Recommendation: Pitfall 3 로 문서화. 코드 방어는 discuss-phase 에서 사용자 의견 확인 후 planner 재량.

## Environment Availability

해당 없음 — 이 phase 는 기존 코드베이스 내부 리팩토링/조건분기 추가이며 신규 외부 도구/서비스/패키지 의존성이 없다. HALCON 24.11, .NET Framework 4.8 등 기존 스택은 CLAUDE.md 에 이미 고정되어 있고 변경되지 않는다.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | 없음 — 프로젝트에 자동화 테스트 프레임워크 미도입(CLAUDE.md 명시: "No test framework detected") |
| Config file | 없음 |
| Quick run command | `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` (빌드 PASS 확인) + 하드룰 grep 5종(CLAUDE.md 명시) |
| Full suite command | SIMUL 모드 수동 UAT(STATE.md 기존 관례 — "실기 UAT 대기" 패턴) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SDV-01 | 옵션 bool 저장/로드, PropertyGrid 노출 | manual | 없음(PropertyGrid 육안 확인 + INI 파일 grep) | N/A — 테스트 인프라 없음 |
| SDV-02 | 옵션 ON 시 세로 흐림 상황에서도 datum OK, 측정 진행 | manual(SIMUL + 실기) | SIMUL 이미지 셋으로 재현 가능(세로 ROI 를 의도적으로 저대비 이미지로 교체) | 기존 `Cal_Image/` 류 SIMUL 이미지 재사용 |
| SDV-03 | View 세로선 미표시 | manual(육안) | 없음 | N/A |
| SDV-04 | 옵션 OFF 회귀 0 | manual(SIMUL 회귀 스크립트 없음, 기존 3-축 UAT 패턴 준용) | 기존 SIDE SIMUL 1사이클 실행 후 결과값 비교(사용자 실기검증 계획에 이미 명시됨) | — |

### Sampling Rate
- **Per task commit:** `msbuild` Debug/x64 + 하드룰 grep 5종(CLAUDE.md `## Code Style` 섹션 grep 커맨드 그대로) 0건.
- **Per wave merge:** SIMUL 전체 사이클(SIDE_1~4) 1회 수동 실행.
- **Phase gate:** CONTEXT.md 의 "실기 검증(SIDE PC)" 섹션 5개 시나리오(옵션 OFF 기준값 → 옵션 ON 비교 → 세로 흐림 유도 → 좌우 밀림 재현 → View/수동 Find 확인) 전부 통과.

### Wave 0 Gaps
없음 — 이 프로젝트는 자동화 테스트 인프라가 존재하지 않으며(CLAUDE.md 명시), 검증은 빌드+하드룰 grep(코드 레벨) + SIMUL/실기 수동 UAT(기능 레벨) 조합이 확립된 관례다(STATE.md 전 항목이 이 패턴). 신규 테스트 프레임워크 도입은 이 phase 범위 밖.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | 이 phase 는 검사 알고리즘/UI 내부 로직만 변경, 인증 계층 무관 |
| V3 Session Management | No | 해당 없음 |
| V4 Access Control | No | 해당 없음(기존 ADMIN 게이트 등 무변경) |
| V5 Input Validation | 경미하게 해당 | 신규 bool 필드는 PropertyGrid 체크박스로만 입력되므로 파싱 취약점 없음. INI 로드 경로는 기존 `ParamBase.Load` 그대로 사용(§8 검증 완료) |
| V6 Cryptography | No | 해당 없음 |

### Known Threat Patterns for {stack}

해당 없음 — 이 phase 는 네트워크 입력이나 외부 신뢰 경계를 다루지 않는 순수 내부 검사 로직 변경이다(TCP 프로토콜/`VisionServer` 자체는 무변경, §1/§10 확인). 산업 제어 PC 로컬 실행 환경이며 이번 변경으로 공격 표면이 늘지 않는다.

## Sources

### Primary (HIGH confidence — 코드 직접 열람)
- `C:/code/DataMeasurement/WPF_Example/Halcon/Algorithms/DatumFindingService.cs` (전체, 2246줄) — Find/Teach 알고리즘 본문
- `C:/code/DataMeasurement/WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` (2900-3130 구간) — `TryComposeAlign`/`TryRunSingleDatum`/`MarkAlignFailed`/`DetectDatumFailure`
- `C:/code/DataMeasurement/WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (340-472, 1129, 1360-1405, 1830-1875 구간) — Datum 검출 오케스트레이션 + `InjectDatumOrigin`
- `C:/code/DataMeasurement/WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (1-430, 1230-1400 구간) — 필드 정의 + `IsHiddenForAlgorithm` + `Load` 오버라이드
- `C:/code/DataMeasurement/WPF_Example/Custom/Sequence/Inspection/Measurements/*.cs` (grep 전수) — `DatumAngle2Rad`/`DatumOriginRow/Col` 소비 패턴
- `C:/code/DataMeasurement/WPF_Example/Halcon/Display/HalconDisplayService.cs` (370-520, 1260-1330 구간) — `RenderDatumFindResult`/`RenderDatumDetectedOverlay`
- `C:/code/DataMeasurement/WPF_Example/Halcon/Display/OverlayCaptureRenderer.cs` (280-350 구간) — `DrawDatumRegions`
- `C:/code/DataMeasurement/WPF_Example/UI/ContentItem/MainView.xaml.cs` (1120-1175, 2155-2250, 4390-4540 구간) — Test Find/재앵커/복원 UI 코드
- `C:/code/DataMeasurement/WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` (1160-1290 구간) — 결과 리뷰어 렌더 분기
- `C:/code/DataMeasurement/WPF_Example/Sequence/Param/ParamBase.cs` (320-410 구간), `C:/code/DataMeasurement/WPF_Example/Utility/Ini.cs` (140-215, 375-385, 945-970 구간) — INI bool/int Load 동작
- `D:/Data/Recipe/FAI_1/main.ini` (읽기 전용, bash grep/sed 로 직접 검증) — SIDE Datum `AlgorithmType`/`IsPatternAlignEnabled`/`RefMatch*` 4개 전수, SIDE 측정 25개 `TypeName`/`MeasureAxis` 전수

### Secondary (MEDIUM confidence)
없음 — 이번 연구는 전량 코드/레시피 1차 열람으로 완결되어 외부 문서 인용이 필요 없었음.

### Tertiary (LOW confidence)
없음.

## Metadata

**Confidence breakdown:**
- 검출 호출 지점 전수조사(§1): HIGH — grep 전수 + 각 호출부 코드 열람으로 교차검증
- transform 아키텍처(§3): HIGH — `TryComposeAlign` 라인 단위 추적 + 주석 인용으로 재확인
- `DetectedRefAngle2` sentinel 분석(§4): HIGH — 5개 소비 지점 코드 직접 대조
- 원점 산출식(§5): HIGH(기존 CTH 선례와 동형 구조 확인) — 단, 신규 코드이므로 구현 후 SIMUL 재검증 필요
- INI bool 하위호환(§8): HIGH — 3단 코드 추적(IniSection→IniValue→ParamBase)
- 표시 게이트(§9): HIGH — `RenderDatumFindResult`/`OverlayCaptureRenderer`/`MainResultViewerControl` 분기 조건 직접 확인
- 레시피 실측치(SIDE AlgorithmType/IsPatternAlignEnabled/MeasureAxis 분포): HIGH — 운영 레시피 파일 직접 grep(읽기 전용)

**Research date:** 2026-09-11
**Valid until:** 코드베이스가 이 phase 의 기반이 되므로, 이 연구는 phase 76 실행 완료 시점까지 유효(코드 구조 자체가 안정적인 내부 아키텍처라 만료 기한을 짧게 잡을 이유 없음 — 단, 계획/실행 중 `DatumFindingService.cs`/`InspectionSequence.cs` 가 다른 quick-fix 로 먼저 변경되면 라인 번호 재확인 필요).
