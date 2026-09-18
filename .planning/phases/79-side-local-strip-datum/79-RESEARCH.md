# Phase 79: SIDE 핀 옆 띠 기준 옵션 (EdgeToLineDistance 국부 기준선) - Research

**Researched:** 2026-09-18
**Domain:** HALCON 에지 라인 피팅(`VisionAlgorithmService.TryFitLine`) 재사용 + `EdgeToLineDistanceMeasurement` 측정별 옵션 + INI/JSON 하위호환 + WPF ROI 티칭 배선
**Confidence:** MEDIUM — 코드 구조·레시피·과거 실측 데이터는 실제 소스/파일 대조로 HIGH, 그러나 핵심 전제("측정 사진에 핀 옆 띠가 잡히는가")는 JPG 육안 검사로만 확인했고 실제 HALCON 서브픽셀 피팅 결과가 아니므로 O-79-01 은 MEDIUM. 아래 BLOCKER 절 필독.

## BLOCKER 후보 — O-79-01 핵심 발견 (계획 전 확인 필요)

**D-79-05("기준 ROI는 측정과 같은 사진에서 찾는다")를 그대로 적용하면, 실제 운영에서 Z 자동선택이 고르는 사진은 그 핀 옆 띠가 흐릿한 사진일 때가 잦다.** 09-17 SIDE_1 실제 저장 사진(JPG, 자재 A·B 각 3사이클, 총 6사이클 정밀 대조)으로 확인한 사실:

- 같은 한 장의 사진(z7, `origin_SIDE_1_SIDE_SHOT_1_C13-14_M1_174002689.jpg`) 안에서 **col≈1690(P1) 위치는 띠 에지가 선명하고, col≈7400(P2)·col≈12870(P3) 위치는 같은 띠가 흐릿하다** — 즉 이 부품은 띠가 좌우로 휠 뿐 아니라 **깊이(초점) 방향으로도 위치가 달라** 한 장의 사진이 전체 폭에서 동시에 선명할 수 없다 [VERIFIED: 육안 크롭 비교, 재현 가능 — 크롭 스크립트/좌표 아래 기록].
- C13_P1·C14_P1 이 실제로 채택하는 Z(z=3 또는 z=5, 6사이클 전부 이 범위)에서는 col≈1690 의 띠가 **흐릿한 뭉치**로만 보인다(z=5,9 흐림 / z=7,8 에서만 선명 — 그러나 z=7,8 은 P1이 아니라 P2·P3 근처가 선명해지는 시점과도 안 맞음).
- C13_P2·C14_P2 가 채택하는 z=7, C13_P3·C14_P3 가 채택하는 z=9 에서도 **각각 자기 컬럼 위치의 띠는 흐릿**했다(위 표 참고).
- 자재 B 3사이클(z 매핑 정밀 확인됨: z3→174315651.jpg, z5→174318192.jpg, z7→174320194.jpg, z9→174322859.jpg) 전부 같은 패턴 재현.

**해석:** 09-17 CONTEXT.md 근거 데이터(왼쪽 +2.8µm/오른쪽 +4.0µm 로 자재차 해소)는 사용자가 **오프라인에서 선명한 사진 1장을 수동으로 골라** 분석한 결과일 가능성이 높다(연구 세션에서 자동 Z선택 결과와 대조 불가 — Open Question 1 참고). 실제 운영에서 Z 자동선택은 **핀 에지 강도만으로** 사진을 고르므로(D-79-08 불변), 그 사진에서 배경 띠까지 선명하다는 보장이 없다.

**이것이 계획을 막지는 않는다 — D-79-06(실패 시 자동 전환+로그, 검사 계속)이 정확히 이 상황을 위해 설계된 안전장치다.** 다만 사용자에게 명확히 알려야 할 사실: **"핀 옆 띠 기준" 옵션은 켜도 실제 가동 중 국부 기준 대신 전역 기준으로 자동 전환되는 빈도가 낮지 않을 수 있다.** 권장:
1. (기본, 변경 없음) D-79-05 그대로 진행 — 국부 실패 시 전역 폴백, D-79-07 표시로 실사용 비율을 눈으로 확인. LSR-06 검증에서 "전환됨" 빈도를 반드시 세어 사용자에게 보고한다.
2. (대안, CONTEXT 재확인 필요) 기준 ROI 만 별도로 **그 Shot 의 기준 Z(`ShotParam.ZIndex`, 범위와 무관하게 항상 촬영되는 사진)**를 쓰게 하는 방안 — D-79-05 문구("같은 사진")를 벗어나므로 사용자 승인 필요. 이 리서치는 **1번을 기본값으로 권장**하고, LSR-06 실측에서 폴백 비율이 지나치게 높으면(예: 6점 중 다수가 매번 전환) 2번을 사용자와 재논의할 것을 제안한다.

이 절의 확인 방법(재현 가능한 크롭 좌표)은 "Runtime State Inventory" 다음 "Common Pitfalls > Pitfall 1"에 정리했다.

## Summary

Phase 79 는 새 알고리즘을 만들지 않는다 — `EdgeToLineDistanceMeasurement.TryExecute` 안에서 이미 호출 중인 `VisionAlgorithmService.TryFitLine`(strip-loop 20분할 + tukey 라인 피팅, Phase 57.1/77 이 다듬어 놓은 바로 그 함수)을 **기준 ROI 1개에 대해 한 번 더 호출**하는 것이 핵심이다. 현재 코드는 이미 `DatumOriginRow/Col/DatumAngleRad`(전역 2점 직선)로 `HOperatorSet.ProjectionPl` 정사영을 수행하는 구조라, "기준선"을 전역 datum 축 대신 **기준 ROI 가 갓 피팅한 2점(local axis)**으로 바꿔 끼우기만 하면 나머지 계산(부호 정규화, per-edge-point 평균, overlay 생성)은 전부 그대로 재사용된다. `LineToLineDistanceMeasurement`(같은 폴더, `Line1|ROI`+`Line2|ROI` 2개 카테고리)가 "측정 안에 ROI 2개" 패턴의 기존 전례이며, ROI 티칭 배선(`MainView.xaml.cs` 의 `BuildPointRoiDefinitions`/`ApplyPointRoiMoveDelta`/`TryGetPointRoiCenter`/`ApplyPointRoiResize` 4개 함수, `DualImageEdgeDistanceMeasurement` 의 subKey `"Point"`/`"Line"` 접미사)도 그대로 복제 가능한 전례가 이미 존재한다.

레시피 하위호환은 이미 이 프로젝트가 여러 번 겪은 문제(`MeasCorrectionFactor`/`MeasCorrectionEnabled` 의 "키 누락 → 0/false 클로버" 버그와 그 수정 패턴, `MeasurementBase.Load` override)를 그대로 복제하면 된다 — `IsLocalRefEnabled`(bool, 기본 false)는 클로버되어도 안전(false=false)하지만, 새 Edge 설정값(`LocalRefEdgeThreshold=10`, `LocalRefSigma=1.0` 등 0 이 아닌 기본값)은 옛 레시피에 키가 없으면 0 으로 클로버되므로 `MeasurementBase.Load` 와 같은 override 가 필요하다.

가장 중요한 리스크는 위 BLOCKER 절의 O-79-01 발견이다 — "같은 사진" 제약이 자동 Z선택과 만나면 국부 기준 활성 빈도가 기대보다 낮을 수 있다.

**Primary recommendation:** `EdgeToLineDistanceMeasurement.TryExecute` 안에 현재 인라인으로 박혀 있는 "axis 2점 계산"(현재 `DatumOriginRow/Col`+`DatumAngleRad` 로부터 axisR1/C1/R2/C2 를 만드는 코드, 파일 내 `if (measureX) ... else ...` 블록) 을 `TryResolveMeasurementAxis(...)` 같은 private 헬퍼로 뽑아내고, `IsLocalRefEnabled` 가 켜져 있으면 그 앞에서 `svc.TryFitLine(image, LocalRef_Row, ...)` 를 먼저 시도해 성공하면 그 2점을 axis 로 쓰고, 실패(또는 옵션 꺼짐)하면 지금 로직 그대로(전역 datum 축) 흘러가게 만든다. 이 구조면 "옵션 꺼짐 = 완전히 같은 코드 경로"가 코드 레벨에서 보장된다.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| 기준 ROI 라인 피팅(LSR-01/02) | Backend(`ReringProject.Halcon.Algorithms.VisionAlgorithmService`, HALCON 순수 알고리즘) | Sequence(`ReringProject.Sequence.EdgeToLineDistanceMeasurement`) | `TryFitLine` 은 이미 이미지·ROI·HALCON 만 의존하는 순수 알고리즘 계층 — 신규 계산 없이 재호출만 추가 |
| 자동 전환·로그(LSR-03) | Sequence(측정 클래스 내부, `TryExecute` 리턴 전) | Utility(`Logging.PrintLog`) | 판정/기록에 영향 주지 않는 관측 로그이며, 측정 자신이 실패를 알고 있는 유일한 지점이라 여기서 처리해야 재계산이 없다 |
| 사용 기준 표시·기록(LSR-04) | Backend(DTO — `MeasurementResultDto`/`EdgeInspectionOverlay`) | Frontend(리뷰어/결과 그리드/CSV 열) | Phase 78 이 이미 확립한 "CycleResultDto 단일 소스, 소비자 여럿" 원칙을 그대로 따른다 |
| 회귀 0(LSR-05) | Backend(측정·INI·JSON 전 계층) | — | 옵션 OFF 경로가 코드 분기 자체를 안 타야(신규 계산 진입 자체 없음) 회귀가 구조적으로 불가능해진다 |
| ROI 티칭·표시(LSR-06 지원) | Frontend(`MainView.xaml.cs` ROI 배선 4함수, `HalconDisplayService` 오버레이) | Backend(`RoiDefinition`) | 기존 다중 ROI 측정(`DualImageEdgeDistanceMeasurement`, `ArcLineIntersectDistanceMeasurement`)과 동일한 계층 — 신규 계층 불필요 |

## User Constraints (from CONTEXT.md)

<user_constraints>

### Locked Decisions

- **D-79-01:** 목적은 자재 간 측정 편차 감소. 원인은 금속 띠 휨이고, 핀 옆 띠 기준 옵션(방법 2)으로 해결한다.
- **D-79-02:** 도면의 C13·C14 높이 기준은 핀 옆 띠 면이다. 국부 기준 값이 도면 치수와 같은 뜻이다.
- **D-79-03:** EdgeToLineDistance 에 측정별 체크박스 + 기준 ROI 1개를 추가한다. 기준 ROI 의 띠 에지 라인을 0점으로 쓴다. 측정마다 따로 켤 수 있다.
- **D-79-04:** 기본 꺼짐. 옛 레시피와 옵션 꺼진 측정은 측정값·판정·기록·표시가 현재와 완전히 같다.
- **D-79-05:** 기준 ROI 는 측정과 같은 사진(그 측정이 쓰는 z 사진)에서 찾는다. 다른 z 사진을 쓰지 않는다. **(위 BLOCKER 절 참고 — 실측으로 이 전제의 리스크를 확인했다.)**
- **D-79-06:** 기준 ROI 를 못 찾으면 기존 전역 기준선으로 자동 전환하고 로그를 남긴다. 검사는 계속한다.
- **D-79-07:** 결과 화면과 기록에 사용 기준(국부 / 전역 / 국부 실패→전역)을 표시한다.
- **D-79-08:** Z 범위 자동 선택 구조, 레시피 구조, PLC z 번호·프로토콜, Datum 알고리즘은 바꾸지 않는다.
- **D-79-09:** 범위는 옵션 개발 + SIDE C13·C14 6점 자재 A·B 검증까지. F9·다른 측정 타입 확대는 범위 밖.

### Claude's Discretion (열린 항목, 제안값 — 계획 단계에서 확정, 이 리서치의 조사 결과 아래 각 항목에 반영)

| ID | 항목 | CONTEXT 제안 | 이 리서치의 결론 |
|---|---|---|---|
| O-79-01 | 측정 사진에서 띠가 잡히는가 | 조사로 확인 | **부분적으로만 그렇다 — 위 BLOCKER 절.** 컬럼별로 초점이 갈린다. 기준 ROI 전용 에지 설정 필요(아래 Architecture Patterns) |
| O-79-02 | 기준선 기울기 | (a) 자체 기울기 (b) 위치만+전역 각도 | **(b) 를 기본으로 권장** — 짧은 ROI 기울기 노이즈 회피, DatumAngleRad 자체가 이미 작음(-0.5°). 수치 비교는 LSR-06 실측 필요(ASSUMED, 아래 Assumptions Log A1) |
| O-79-03 | Z 범위 자동 선택과의 관계 | 조사 후 결정 | **코드 변경 불필요** — `TryExecute` 안에 내장하면 `RunZFocusCandidates` 가 후보마다 자동으로 그 후보의 기준 ROI 를 재시도한다(재계산 없음, D-77-02 원칙 유지). Z 선택 점수(`LastFitScore`)는 핀 에지 강도만 유지(D-79-08) — 기준 ROI 성공 여부가 Z 선택에 영향주지 않게 별도 `VisionAlgorithmService` 인스턴스(EdgeScore=null)로 호출 |
| O-79-04 | 기록 위치 | cycle.json 필드+그리드/리뷰어, CSV 는 77-03 선례 참고 | **CSV 헤더 맨 끝에 1열 추가**(77-03 의 `선택Z` 열과 완전히 같은 패턴 — `COLUMN_COUNT`=14 유지, 새 열은 옵션 인덱스로만 읽음) — 통계/CPK 리더는 별도 CSV(`D:\Data\Statistics\*.csv`)를 쓰므로 영향 없음(아래 확인) |
| O-79-05 | 기준값·공차 | 코드가 자동으로 안 바꿈, 안내만 | 그대로 채택 — 계획에서 UI 설명 문구만 추가 |
| O-79-06 | 기준 ROI 티칭·표시 | 기존 배선 재사용, 오버레이 색 구분 | `MainView.xaml.cs` 4함수에 분기 추가(DualImage 의 `"Point"/"Line"` subKey 패턴과 동일), overlay 신규 RoiId `"FAI-RefLine"`(주황) 제안 — 아래 Code Examples |
| O-79-07 | 이름 | `IsLocalRefEnabled`, `LocalRef_Row/Col/Phi/Length1/Length2`, 카테고리 "Local Ref" | 그대로 채택 + Edge 설정도 `LocalRef` 접두(`LocalRefEdgeThreshold` 등)로 핀 자신의 `EdgeThreshold` 와 분리 — O-79-01 이 서로 다른 에지 설정이 필요할 수 있음을 보였다 |

### Deferred Ideas (OUT OF SCOPE)

- F9 및 다른 위치 측정 확대 — 근처에 쓸 만한 직선이 있는지 따로 확인 후 별도 phase
- EdgeToLineAngle 등 다른 측정 타입에 같은 옵션 — 효과 확인 후
- z1 사진 흔들림(PLC 대기시간) — PLC 테스트로 별도 확인
- 평균값의 도면값 치우침(C13 +0.23 등) — 보정/티칭 문제로 별도

</user_constraints>

## Project Constraints (from CLAUDE.md)

- C# 7.2 고정. 삼항 `?:` / 이항 `??`·`??=` / null 조건 `?.`·`?[]` / C# 8 `switch` 식 전부 금지 — `if/else`, 명시적 null 체크, 전통 `switch`(+`break`)만 사용. (`EdgeToLineDistanceMeasurement.cs` 는 이미 이 규칙을 지키며 작성돼 있다 — 그 스타일 그대로 확장할 것)
- 헝가리언 접두사(`b`/`n`/`sz`/`d`/`hv`), 매직넘버 금지(named const), 3개 이상 `&&`/`||` 조건은 이름 있는 bool 로 선추출.
- 새 UI 로직은 ViewModel/서비스 계층에만. `MainView.xaml.cs`(4,400+줄)에는 **이번에 손대는 지점(ROI 배선 4함수)에만** 최소 추가 — 새 로직 총량을 늘리지 않는다.
- 날짜 주석(`//YYMMDD hbk`) 신규 금지 — `// Phase 79 LSR-xx: ...` 형식만 사용.
- `HImage`/`HObject`/`HTuple` 은 반드시 Dispose. `TryFitLine` 재호출 시 새 HObject(`contour`)가 생기므로 기존 try/catch/finally 안에서 Dispose 되는지 재확인 필요(아래 Common Pitfalls).
- **신규 .cs 파일 금지** — 새 클래스(`LocalRef` 관련 헬퍼, DTO)는 전부 기존 파일에 추가.
- `D:\Data*` 읽기 전용. Release 빌드 금지. Debug|x64 빌드만.
- STATE/ROADMAP 은 gsd 도구 대신 수동 편집.
- 옵션 꺼짐 경로: 측정값·판정·기록·표시 불변. cycle.json 은 필드 추가만.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| LSR-01 | 옵션·기준 ROI 추가 | "Standard Stack"(신규 라이브러리 없음) + "Architecture Patterns > Pattern 1/2" + "Code Examples" — 필드 명명·카테고리·기본값 확정 |
| LSR-02 | 국부 기준 측정 | "Architecture Patterns > Pattern 1" — `TryFitLine` 재호출로 axis 2점 확보, 기존 `ProjectionPl` 로직 그대로 재사용. O-79-02 기울기 선택 근거 포함 |
| LSR-03 | 자동 전환(폴백) | "Architecture Patterns > Pattern 2" + "Common Pitfalls > Pitfall 2" — 실패 시 전역 axis 로 무중단 전환하는 정확한 삽입 지점 |
| LSR-04 | 사용 기준 표시·기록 | "Don't Hand-Roll"(77-03 `선택Z` 패턴 재사용) + "Code Examples > CSV 확장" |
| LSR-05 | 회귀 0 | "Runtime State Inventory" + "Common Pitfalls > Pitfall 3/4" + "Validation Architecture" — 옵션 OFF 경로가 신규 코드에 진입조차 안 함을 보장하는 가드 위치 |
| LSR-06 | C13·C14 자재 A·B 검증 | "BLOCKER 절" + "Open Questions" + "Validation Architecture" — 오프라인 재검사 절차와 A/B 비교 방법 |

</phase_requirements>

## Standard Stack

이 phase 는 새 NuGet 패키지를 설치하지 않는다. 기존 `halcondotnet`(HALCON 24.11) 의 `HOperatorSet.ProjectionPl`, `FitLineContourXld`, `MeasurePos`(모두 `VisionAlgorithmService.TryFitLine` 내부에 이미 사용 중)를 그대로 재사용한다.

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| halcondotnet | 24.11(Progress Steady) [VERIFIED: CLAUDE.md 명시 경로] | 에지 검출·라인 피팅·정사영 | 이미 설치·사용 중, 신규 알고리즘 불필요 |
| Newtonsoft.Json | 13.0.3 [VERIFIED: WPF_Example/packages.config] | cycle.json 직렬화 | `CycleResultSerializer` 기존 사용, 신규 필드 추가만 |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| 기준 ROI 를 위해 새 Halcon 알고리즘(예: `fit_line` 직접 호출) 작성 | 기존 `VisionAlgorithmService.TryFitLine` 재호출 | 새 알고리즘은 strip-loop/trim/tukey 피팅을 처음부터 재구현해야 하고, Phase 57.1/77 이 이미 다듬어 놓은 견고성(strip 실패 카운트, EdgeStrengthScore 등)을 잃는다. 재사용이 명백히 우월 |
| 기준선 계산을 새 서비스 클래스로 분리 | `EdgeToLineDistanceMeasurement.TryExecute` 안의 private 헬퍼로 추출 | 신규 .cs 파일 금지 제약과 "이 측정 타입 전용 로직"이라는 성격상 별도 서비스화는 과설계 |

**Installation:** 없음.

## Package Legitimacy Audit

이 phase 는 새 외부 패키지를 설치하지 않는다. Package Legitimacy Gate 대상 없음.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| (신규 설치 없음) | — | — | — | — | — | N/A |

**Packages removed due to [SLOP] verdict:** 없음
**Packages flagged as suspicious [SUS]:** 없음

## Architecture Patterns

### System Architecture Diagram

```
[측정 실행 tick] (Action_FAIMeasurement.TryExecuteMeasurement)
        │
        ▼
[IDatumOriginConsumer 주입] (InjectDatumOrigin: DatumOriginRow/Col, DatumAngleRad ← DatumConfig)
        │
        ▼
┌───────────────────────────────────────────────────────────────────┐
│ EdgeToLineDistanceMeasurement.TryExecute(image, datumTransform, ...)│
│                                                                       │
│  1) 핀 에지 피팅 (기존, 무변경)                                       │
│     svc.TryFitLine(image, Point_Row/Col/Phi/Len1/Len2, ...)          │
│       → pr1,pc1,pr2,pc2, collectedEdgePoints, LastFitScore           │
│                                                                       │
│  2) [신규] 기준선 axis 결정 — TryResolveMeasurementAxis(...)          │
│     IF IsLocalRefEnabled:                                            │
│       refSvc = new VisionAlgorithmService()  // EdgeScore=null,      │
│                                               //  LastFitScore 오염 방지│
│       refOk = refSvc.TryFitLine(image, LocalRef_Row/Col/Phi/Len1/2,   │
│                  datumTransform, LocalRefEdgeSampleCount, ...,        │
│                  LocalRefEdgeDirection, LocalRefEdgePolarity,         │
│                  out refR1, out refC1, out refR2, out refC2, ...)     │
│       IF refOk: axis = (refR1,refC1)-(refR2,refC2), RefSource="Local" │
│       ELSE: 로그(fallback) + axis = 전역(기존 로직), RefSource=       │
│              "GlobalFallback"                                        │
│     ELSE (옵션 꺼짐): axis = 전역(기존 로직 그대로), RefSource=null    │
│                                                                       │
│  3) 거리 계산 (기존, 무변경) — HOperatorSet.ProjectionPl(axis) 로     │
│     collectedEdgePoints 각각을 axis 에 정사영, 부호 있는 평균 거리     │
│                                                                       │
│  4) overlay 생성 (기존 FAI-Edge1/FAI-DistLine 무변경                  │
│     + [신규] refOk 이면 "FAI-RefLine" 오버레이 추가)                   │
└───────────────────────────────────────────────────────────────────┘
        │
        ▼
[결과 기록] RecordMeasurementResult → meas.LastRefSource (신규 필드)
        │
        ▼
[cycle.json] CycleResultSerializer.BuildDto → MeasurementResultDto.RefSource
        │              ├──► 결과 그리드/리뷰어 (사용 기준 표시)
        │              └──► CSV 맨 끝 열(선택Z 옆) 추가
        ▼
[Z 범위 후보 반복] RunZFocusCandidates 가 후보 z 마다 TryExecute 를 그대로
  다시 호출 → 후보마다 자기 사진으로 기준 ROI 도 자동 재시도(코드 변경 없음)
```

### Recommended Project Structure

신규 파일 없음(제약). 기존 파일에 추가되는 논리적 구획:

```
WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs
  ├── IsLocalRefEnabled (신규 bool, 기본 false, Category "Local Ref")
  ├── LocalRef_Row/Col/Phi/Length1/Length2 (신규 double, Category "Local Ref|ROI")
  ├── LocalRefEdgeThreshold/Sigma/EdgeSampleCount/EdgeTrimCount/EdgePolarity/EdgeDirection
  │     (신규, Category "Local Ref|Edge" — 핀 자신의 EdgeThreshold 등과 분리된 별도 설정)
  ├── TryResolveMeasurementAxis(...) (신규 private 메서드 — 기존 인라인 axis 계산 추출 + 국부 우선 시도)
  └── TryExecute 본문: 2)단계에 TryResolveMeasurementAxis 호출 삽입, overlay 에 FAI-RefLine 추가

WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs
  ├── LastRefSource (신규 public 필드 — LastFitScore 와 동일 패턴, 프로퍼티 아님 → INI/JSON 자동 제외)
  ├── ClearResult() 에 LastRefSource = null 추가(Phase 77 LastFitScore 리셋 지점과 같은 곳)
  └── (IsLocalRefEnabled 는 EdgeToLineDistanceMeasurement 소유— MeasurementBase 공통화 불필요, 다른 측정 타입엔 없음)

WPF_Example/UI/ViewModel/CycleResultDto.cs
  ├── MeasurementResultDto.RefSource (신규 string, 기본 null — 77-03 SelectedZIndex 필드 추가 패턴과 동일)
  └── CycleResultSerializer.BuildDto 가 meas.LastRefSource 를 복사(1줄)

WPF_Example/Halcon/Models/EdgeInspectionOverlay.cs
  └── (신규 필드 불필요 — RoiId="FAI-RefLine" 만으로 충분, SelectedZLabel 패턴을 그대로 재사용해도 됨)

WPF_Example/Halcon/Display/HalconDisplayService.cs
  └── overlay.RoiId 분기에 "FAI-RefLine" 케이스 추가(주황, FAI-DistLine cyan 분기 옆)

WPF_Example/UI/ContentItem/MainView.xaml.cs
  ├── BuildPointRoiDefinitions: EdgeToLineDistanceMeasurement 분기에 LocalRef ROI 추가 반환(subKey "_LocalRef")
  ├── ApplyPointRoiMoveDelta / TryGetPointRoiCenter / ApplyPointRoiResize: 동일 subKey 분기 추가
  └── (DualImageEdgeDistanceMeasurement 의 "Point"/"Line" 2-분기 패턴을 그대로 복제)

WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs / MeasurementHistoryCsvLoader.cs
  ├── CSV_HEADER 맨 끝에 "사용기준" 열 추가(선택Z 다음 컬럼)
  └── COLUMN_COUNT(14) 는 유지 — 신규 열은 옵션 인덱스로만 읽기(77-03 COL_SELECTED_Z 패턴 복제)
```

### Pattern 1: 기준 ROI 도 `TryFitLine` 재사용 — 신규 알고리즘 없음

**What:** `VisionAlgorithmService.TryFitLine` 은 이미지·ROI 5개 값(`row,col,phi,length1,length2`)·datumTransform·에지 설정을 받아 strip-loop(기본 20분할) + tukey 라인 피팅으로 2점(row1,col1)-(row2,col2)을 반환하는 범용 함수다. 핀 ROI 에 쓰는 것과 완전히 같은 함수를 기준 ROI 좌표로 한 번 더 호출하면 된다.
**When to use:** `IsLocalRefEnabled==true` 일 때 핀 피팅 직후.
**Example:**
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs:131-140
//  (기존 핀 피팅 호출부 — 기준 ROI 도 이 시그니처를 그대로 재사용한다)
var svc = new VisionAlgorithmService();
// ... 기존 핀 피팅(변경 없음) ...

// 신규 — 기준 ROI (다른 VisionAlgorithmService 인스턴스: EdgeScore=null 로
//  Z선택 점수(LastFitScore)에 영향을 주지 않는다, D-79-08)
double refR1 = 0, refC1 = 0, refR2 = 0, refC2 = 0;
bool bRefOk = false;
string szRefError = null;
if (IsLocalRefEnabled)
{
    var refSvc = new VisionAlgorithmService(); // EdgeScore 미설정 = 점수 수집 안 함
    bRefOk = refSvc.TryFitLine(image,
        LocalRef_Row, LocalRef_Col, LocalRef_Phi, LocalRef_Length1, LocalRef_Length2,
        datumTransform,
        LocalRefEdgeSampleCount, LocalRefEdgeTrimCount, LocalRefSigma, LocalRefEdgeThreshold,
        LocalRefEdgeDirection, LocalRefEdgePolarity,
        out refR1, out refC1, out refR2, out refC2, out szRefError);
}
```

### Pattern 2: axis 선택 — 국부 성공 시 대체, 실패/OFF 시 기존 전역 로직 그대로

**What:** 현재 `TryExecute` 안에서 `axisR1,axisC1,axisR2,axisC2`(정사영 대상 직선의 2점)를 `DatumOriginRow/Col`+`DatumAngleRad`(또는 `DatumAngle2Rad`)로부터 계산하는 인라인 블록(파일의 `if (measureX) ... else ...` 부분, `//260625 hbk 부호 정규화...` 주석 아래)이 있다. 이 계산을 그대로 두고, **그 앞에** "국부 성공 시 이 값을 덮어쓴다"는 조건 하나만 추가한다.
**When to use:** `datumOriginInjected` 분기 진입 직후, axis 계산 전.
**주의 — O-79-02 (기울기 선택):** 국부 라인을 그대로 axis 로 쓰면 자체 기울기(옵션 a)가 반영된다. **이 리서치는 옵션 (b, 위치만 반영 + 전역 DatumAngleRad 유지)를 기본으로 권장**하지만 수치 검증(LSR-06)이 필요하므로, 계획 단계에서 **둘 다 구현 가능하게 상수 하나로 토글**(예: `private const bool USE_LOCAL_REF_ANGLE = false;`)해 두면 LSR-06 실측 후 값만 바꿔 재검증할 수 있다.
```csharp
// 옵션 (b) 권장 구현 — 위치만 로컬, 각도는 전역 유지
if (bRefOk)
{
    double localMidRow = (refR1 + refR2) / 2.0;
    double localMidCol = (refC1 + refC2) / 2.0;
    // 전역 DatumOriginRow/Col 자리에 localMidRow/Col 을 대입하고, 이후 axis 계산(sinT/cosT,
    //  axisR1..axisC2)은 기존 DatumAngleRad 기반 코드를 그대로 재사용한다.
    RunAxisCalcWithOrigin(localMidRow, localMidCol, DatumAngleRad, ...);
    LastRefSource = "Local";
}
else
{
    // 기존 코드 그대로 (DatumOriginRow/Col + DatumAngleRad)
    LastRefSource = IsLocalRefEnabled ? "GlobalFallback" : null;
    if (IsLocalRefEnabled && !string.IsNullOrEmpty(szRefError))
    {
        Logging.PrintLog((int)ELogType.Error,
            "[LocalRef] 기준 ROI 실패 → 전역 기준 전환 — " + MeasurementName + ": " + szRefError);
    }
}
```
**옵션 (a, 자체 기울기) 구현 시:** `refR1,refC1,refR2,refC2` 를 axis 2점으로 그대로 대입하면 된다(가장 단순). 계획 단계에서 (a)/(b) 중 하나를 확정할 것.

### Pattern 3: ROI 티칭 배선 — DualImage 의 2-ROI subKey 패턴 복제

**What:** `MainView.xaml.cs` 는 이미 "측정 하나가 ROI 2개를 가지는" 경우를 `DualImageEdgeDistanceMeasurement`(subKey `"Point"`/`"Line"`)로 처리한 전례가 있다. `EdgeToLineDistanceMeasurement` 에 `"LocalRef"` subKey 를 추가하면 4개 함수(`BuildPointRoiDefinitions`/`ApplyPointRoiMoveDelta`/`TryGetPointRoiCenter`/`ApplyPointRoiResize`) 모두 같은 패턴으로 확장된다.
**Example:**
```csharp
// Source: WPF_Example/UI/ContentItem/MainView.xaml.cs:476-497 (DualImage 분기, 복제 대상 패턴)
var dual = m as DualImageEdgeDistanceMeasurement;
if (dual != null) {
    if (dual.PointROI_Length1 > 0 && dual.PointROI_Length2 > 0) {
        result.Add(new RoiDefinition { Id = faiName + "_" + measName + "_Point", ... });
    }
    if (dual.LineROI_Length1 > 0 && dual.LineROI_Length2 > 0) {
        result.Add(new RoiDefinition { Id = faiName + "_" + measName + "_Line", ... });
    }
    return result;
}
// 신규 — EdgeToLineDistanceMeasurement 는 기존에 조기 return 없이 아래 단일-ROI 공통 블록(라인 500-524)을
//  타므로, LocalRef ROI 는 그 블록 "이전"에 별도로 추가해야 한다(기존 단일 Point ROI 흐름은 그대로 유지).
var etldForRef = m as EdgeToLineDistanceMeasurement;
if (etldForRef != null && etldForRef.LocalRef_Length1 > 0 && etldForRef.LocalRef_Length2 > 0) {
    result.Add(new RoiDefinition {
        Id = faiName + "_" + measName + "_LocalRef",
        Name = measName + "_LocalRef",
        Row1 = etldForRef.LocalRef_Row - etldForRef.LocalRef_Length1,
        Column1 = etldForRef.LocalRef_Col - etldForRef.LocalRef_Length2,
        Row2 = etldForRef.LocalRef_Row + etldForRef.LocalRef_Length1,
        Column2 = etldForRef.LocalRef_Col + etldForRef.LocalRef_Length2,
        IsTaught = true
    });
    // 조기 return 하지 않는다 — 아래 기존 단일 Point ROI 블록(500번대)이 이어서 pin ROI 도 추가해야 한다.
}
```
**주의:** `ArcLineIntersectDistanceMeasurement`/`DualImageEdgeDistanceMeasurement` 는 여러 ROI 를 반환하고 `return result`로 조기 종료하지만, `EdgeToLineDistanceMeasurement` 는 기존에 단일 Point ROI 만 반환하던 타입이라 **조기 return 하면 안 된다** — LocalRef ROI 를 추가한 뒤 기존 500번째 줄 이하 공통 블록(`pRow/pCol/pLen1/pLen2` 판별 후 단일 결과 추가)까지 이어져야 pin ROI 도 계속 나온다.

### Pattern 4: 하위호환 — `MeasurementBase.Load` override 전례 그대로 복제

**What:** `ParamBase.Load`(리플렉션 INI 로더)는 프로퍼티가 있으면 **키 존재 여부와 무관하게** `loadFile[group][name].ToDouble()`/`.ToBool()` 을 호출한다. 키가 없으면 `IniValue.Default` → `ToDouble()`=0, `ToBool()`=false 로 반환된다(WPF_Example/Sequence/Param/ParamBase.cs:381-399, [VERIFIED: 코드 직접 확인]). `MeasurementBase.Load` 는 정확히 이 문제 때문에 `MeasCorrectionFactor`(기본 1.0)에 override 를 걸어 놓았다.
**적용 대상:**
- `IsLocalRefEnabled`(bool, 기본 false): 클로버되어도 false→false, **override 불필요**.
- `LocalRef_Row/Col/Phi/Length1/Length2`(double, 기본 0): 클로버되어도 0→0(옵션 꺼짐 상태와 동일 의미), **override 불필요** — 단, 옵션을 켰다가 나중에 다시 로드했을 때 0 이면 `Length1<=0` 가드에 걸려 ROI 가 "미티칭"으로 보이므로 사용자가 다시 그려야 한다(정상 동작, 문서화만 필요).
- `LocalRefEdgeThreshold`(기본 10), `LocalRefSigma`(1.0), `LocalRefEdgeSampleCount`(20), `LocalRefEdgeTrimCount`(10): **override 필요** — 키 없으면 0 으로 클로버되어, 나중에 옵션을 켜는 순간 `Sigma=0`(HALCON에서 무의미한 값) 으로 측정 시도됨.
- `LocalRefEdgePolarity`/`LocalRefEdgeDirection`(string, 기본 "DarkToLight"/"BtoT"): string 은 `ToString()` 이 null 이 아니라 빈 문자열을 반환하는지 확인 필요(기존 `EdgePolarity`/`EdgeDirection` 도 이 문제에 노출돼 있는지는 Open Question 2 참고) — VisionAlgorithmService.TryFitLine 은 `direction`/`polarity` 문자열이 알 수 없는 값이면 각각 `measurePhi=0.0`(LtoR 취급)/`pol="positive"`(DarkToLight 취급) 으로 안전 폴백하므로(코드 79-98줄), **없어도 크래시하지 않지만 의도와 다른 기본값**이 될 수 있다.
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs:228-247 (복제할 override 패턴)
public override bool Load(IniFile loadFile, string groupName) {
    bool result = base.Load(loadFile, groupName);
    IniSection sec;
    bool bHasSection = loadFile.TryGetSection(groupName, out sec) && sec != null;
    if (!bHasSection || !sec.ContainsKey("LocalRefEdgeThreshold")) { LocalRefEdgeThreshold = 10; }
    if (!bHasSection || !sec.ContainsKey("LocalRefSigma")) { LocalRefSigma = 1.0; }
    if (!bHasSection || !sec.ContainsKey("LocalRefEdgeSampleCount")) { LocalRefEdgeSampleCount = 20; }
    if (!bHasSection || !sec.ContainsKey("LocalRefEdgeTrimCount")) { LocalRefEdgeTrimCount = 10; }
    return result;
}
// 이 override 는 EdgeToLineDistanceMeasurement 에 추가한다(MeasurementBase 아님 — LocalRef* 필드는
//  EdgeToLineDistanceMeasurement 전용이므로 base.Load() 를 먼저 부른 뒤 이 4줄만 추가).
```

### Anti-Patterns to Avoid

- **기준 ROI 성공 여부로 Z 후보 우선순위를 바꾸는 것(O-79-03):** D-79-08 위반. `RunZFocusCandidates`/`PickZFocusResult`(핀 에지 강도만 비교)는 무변경 — 기준 ROI 는 `TryExecute` 내부에서 "부가 정보"로만 취급한다.
- **기준 ROI 의 `VisionAlgorithmService` 인스턴스에 `EdgeScore` 를 설정하는 것:** 그러면 `meas.LastFitScore` 가 기준 ROI 강도로 오염되어 Z선택 점수가 달라진다 — 반드시 별도 인스턴스, `EdgeScore=null`(기본값) 유지.
- **`LastRefSource` 를 프로퍼티로 선언하는 것:** `LastSkipReason`(구식 패턴, 프로퍼티라서 INI 에 매 저장마다 값이 남는다)처럼 만들면 안 된다 — `LastFitScore`/`LastSelectedZIndex`(Phase 77 이 확립한 신식 패턴, **필드**)를 따라야 `ParamBase.Save/Load`(프로퍼티만 순회) 대상에서 자동 제외된다.
- **BuildPointRoiDefinitions 에서 LocalRef 분기 뒤 `return result` 하는 것:** 기존 pin ROI(500번대 공통 블록)가 안 나오게 되어 화면에서 핀 ROI 박스가 사라진다(회귀).
- **옵션 OFF 인데도 `TryResolveMeasurementAxis` 안에서 `TryFitLine` 을 호출하는 것:** `IsLocalRefEnabled` 가드를 함수 맨 앞에 두지 않으면, 꺼진 측정도 매 사이클 불필요한 HALCON 호출(성능) + `LocalRef_Length1<=0`(미티칭) 인 ROI 로 `TryFitLine` 을 호출해 매번 "insufficient edge points" 에러 로그가 남는다(로그 스팸, LSR-05 위반 소지).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| 라인 피팅(strip-loop+tukey) | 새 HALCON 알고리즘 함수 | `VisionAlgorithmService.TryFitLine` 재호출 | Phase 57.1/77 이 이미 강건화(strip 실패 카운트, trim, EdgeStrengthScore)해 놓은 함수. 새로 짜면 같은 버그를 재도입할 위험 |
| 정사영·부호 거리 계산 | 새 벡터 수학 | 기존 `axisR1..axisC2`+`ProjectionPl`+부호 정규화 블록 재사용(axis 2점만 교체) | 이미 measureX/measureY, useAngle2 3분기를 정확히 처리하는 코드가 있다 |
| "선택 Z"/사용 기준 표시 텍스트 포맷 | 새 포맷터 | `MeasurementBase.FormatSelectedZ` 패턴을 그대로 본떠 `LastRefSource` 문자열 상수 3개(`"Local"`/`"GlobalFallback"`/null)로 충분 — 별도 포맷 함수 불필요 | 값 자체가 이미 사람이 읽을 수 있는 짧은 문자열 |
| CSV 열 추가 시 파일 스캔/헤더 파싱 로직 | 새 CSV 파서 | `MeasurementHistoryCsvWriter/Loader` 의 `COLUMN_COUNT` 고정 + 옵션 열 인덱스 가드 패턴(77-03 이 확립) | 이미 해결된 하위호환 문제(옛 14열 파일 vs 신 15/16열 파일) |
| ROI 다중 티칭 배선 | 새 ROI 관리 시스템 | `MainView.xaml.cs` 의 4함수 + subKey 문자열 분기(`DualImageEdgeDistanceMeasurement` 전례) | 이미 3개 측정 타입(ArcLineIntersect 4-ROI, DualImage 2-ROI)이 이 패턴으로 동작 중 |

**Key insight:** 이 phase 의 모든 "Don't Hand-Roll" 항목이 전부 **이미 이 코드베이스 안에 존재하는 패턴의 재사용**이다 — Phase 79 는 알고리즘 개발이 아니라 "기존 조각을 새 조합으로 다시 붙이는" 배선 작업에 가깝다. 새로 만들어야 하는 것은 오직 "기준 ROI 를 먼저 시도하고 실패하면 기존 경로로 폴백한다"는 **분기 로직 자체**뿐이다.

## Runtime State Inventory

> Rename/refactor 아님(신규 옵션 추가) — 그러나 "회귀 0·옛 데이터 호환"(LSR-05)이 요구사항이므로 관련 항목만 점검.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| 저장된 데이터(레시피 INI, `D:\Data\Recipe\FAI_1\main.ini`) | C13/C14 6개 측정 섹션(`SHOT_5_FAI_0_MEAS_0`~`_5`)에 `LocalRef*` 키가 전혀 없음(당연, 아직 미구현) — `ParamBase.Load` 리플렉션이 없는 키를 0/false/null 로 채운다 [VERIFIED: main.ini 직접 확인] | Pattern 4 의 override 를 `LocalRefEdgeThreshold` 등 비-0 기본값 필드에 반드시 적용 |
| 저장된 데이터(cycle.json, `D:\Data\Result\20260917\*\cycle.json`) | 328개 사이클 전부 `MeasurementResultDto` 에 `RefSource` 없음(신규 필드) — Newtonsoft 는 없는 필드를 C# 기본값(null)으로 채운다(`CycleResultSerializer`, 기존 78-NGA-07 이 검증한 패턴과 동일) | 코드 추가만, 마이그레이션 불필요 |
| 저장된 데이터(CSV, `MeasurementHistoryCsvWriter` 출력) | 기존 파일은 14~15열(선택Z 유무). 신규 "사용기준" 열은 그 다음(16번째) — `COLUMN_COUNT`=14 그대로면 옛 파일도 안전 | `fields.Count > COL_REF_SOURCE`(16) 가드로만 읽기 |
| 라이브 서비스 설정 | 없음(오프라인 옵션, PLC/외부 서비스와 무관) | 해당 없음 |
| OS 등록 상태 | 없음 | 해당 없음 |
| 시크릿/환경변수 | 없음 | 해당 없음 |
| 빌드 산출물 | 신규 .cs 파일 없음 → csproj 변경 없음, 재설치 불필요. Debug\|x64 재빌드만 | 없음 |

## Common Pitfalls

### Pitfall 1: "같은 사진"이 곧 "그 사진 전체가 선명함"을 뜻하지 않는다 (O-79-01 재현 절차)
**What goes wrong:** 국부 기준 ROI 를 핀 근처에 배치해도, Z 자동선택이 고른 사진에서 그 위치의 배경 띠가 흐릿하면 `TryFitLine` 이 "insufficient edge points" 또는 낮은 신뢰도 피팅으로 실패해 매번 전역 폴백만 일어난다.
**Why it happens:** 부품 표면이 평면이 아니라 깊이 방향으로도 기울어져 있어(3D 워프), 한 Z 위치가 화면 전체 폭에서 동시에 초점을 맞추지 못한다. Phase 77 의 Z-range 기능 자체가 이 현상(위치별 최적 초점이 다름)을 전제로 만들어졌다.
**How to avoid:** 계획 단계에서 (1) LocalRef 전용 Edge 설정(`LocalRefSigma` 를 핀보다 높게, 예: 3~5 → 블러에 강한 저역통과)을 시도, (2) `LocalRef_Length1/Length2`(ROI 크기)를 핀 ROI 보다 크게 잡아 strip 표본 수를 늘려 노이즈를 평균으로 상쇄, (3) LSR-06 실측에서 폴백 발생률을 반드시 측정해 사용자에게 보고.
**Warning signs:** LSR-06 오프라인 재검사에서 6점 대부분이 매 사이클 `GlobalFallback` 으로 표시됨.
**재현 좌표(검증용):** 이미지 좌표계는 `row,col`(image 픽셀), 원본은 13376×9528px. 아래는 이번 조사에서 크롭·확인한 실제 파일/좌표:
| 자재 | z(확인됨) | 파일 | col 중심 | 결과 |
|---|---|---|---|---|
| B | 3 | `D:\Data\Result\Image\260917\1743\original\origin_SIDE_1_SIDE_SHOT_1_C13-14_M2_174315651.jpg` | 1690 | 흐림 |
| B | 5 | `...174318192.jpg` | 1690 | 흐림 |
| B | 7 | `...174320194.jpg` | 7395 | 흐림 |
| B | 9 | `...174322859.jpg` | 12870 | 매우 흐림(핀 자체도 흐림) |
| A | 7 | `D:\Data\Result\Image\260917\1740\original\origin_SIDE_1_SIDE_SHOT_1_C13-14_M1_174002689.jpg` | 1690 | **선명** |
| A | 7 | 위와 동일 파일 | 7400 | 흐림(같은 사진, 다른 컬럼) |
| A | 8 | `...174003692.jpg` | 1690 | 선명 |
크롭 방법: `row=6608`(DatumOriginRow 근방) 중심, `halfW=600,halfH=400` 로 System.Drawing 크롭(스크래치패드 `crop.ps1`, 재현 가능).

### Pitfall 2: 기준 ROI HObject(`contour`) Dispose 누락
**What goes wrong:** `VisionAlgorithmService.TryFitLine` 내부는 `HObject contour` 를 생성하고 자기 안에서 Dispose 하는 구조다(기존 핀 호출에서도 동일 위험이 이미 존재) — 새 `refSvc.TryFitLine` 호출도 **같은 함수**를 그대로 쓰므로 이 부분은 이미 해결된 문제이지만, 만약 계획 단계에서 이 로직을 복사(재구현)하면 Dispose 를 빠뜨리기 쉽다.
**Why it happens:** try/catch 블록 안에서 여러 return 경로가 있는 함수라 finally 처리를 놓치기 쉽다.
**How to avoid:** 절대 복사하지 말고 `TryFitLine` 함수를 그대로 재호출한다(Pattern 1).
**Warning signs:** 장시간 가동 후 메모리 증가(HALCON HObject 누수는 이 프로젝트가 2026-08-06 에 대형 인시던트로 겪은 이력이 있다 — STATE.md 참고).

### Pitfall 3: 옵션 OFF 인데 신규 코드 경로에 진입
**What goes wrong:** `IsLocalRefEnabled` 가드를 axis 계산 함수 내부 깊숙이 두면, 기존 `datumOriginInjected` 판정이나 `measureX`/`useAngle2` 분기 순서가 미묘하게 바뀌어 옵션 OFF 인 기존 18개 EdgeToLineDistance 측정(Phase 76 조사 기준)의 계산 결과가 부동소수점 수준에서 달라질 위험이 있다.
**Why it happens:** 조건 분기를 기존 코드 "안에" 끼워 넣으면 실행 순서가 바뀐다.
**How to avoid:** `IsLocalRefEnabled` 체크를 가장 바깥(axis 계산 함수 진입 즉시)에 두고, false 면 **기존 코드를 단 한 줄도 거치지 않고 원래 흐름으로 바로 점프**하게 만든다 — "새 코드가 없었던 것처럼" 동작해야 한다.
**Warning signs:** 옵션 꺼진 측정의 `LastMeasuredValue` 가 마지막 자릿수에서 달라짐(회귀 감사 시 `git diff` 전/후 동일 레시피로 오프라인 재검사 값 bit-비교).

### Pitfall 4: CSV `COLUMN_COUNT` 를 15/16 으로 올리는 것
**What goes wrong:** `MeasurementHistoryCsvLoader.COLUMN_COUNT`(현재 14) 를 16으로 올리면, 14~15열짜리 기존 파일이 전부 "손상 행"으로 걸러진다(주석에 명시된 경고, `MeasurementHistoryCsvWriter.cs:138` 부근).
**Why it happens:** 새 열을 추가하면서 필수 열 개수도 같이 늘리고 싶은 직관적 실수.
**How to avoid:** `COLUMN_COUNT` 는 14 로 고정, 새 "사용기준" 열은 `COL_REF_SOURCE`(예: 16, `COL_SELECTED_Z`=15 다음) 로만 조건부 읽기(`fields.Count > COL_REF_SOURCE`).
**Warning signs:** 옛 CSV 파일을 리뷰어/통계 화면에서 열었을 때 행이 통째로 사라짐.

## Code Examples

### CSV 헤더/파싱 확장 (77-03 선례 그대로 복제)
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs:24-25 (기존)
private const string CSV_HEADER = "검사일시,RecipeName,IndexNumber,ShotName,FAIName,MeasurementName,TypeName,NominalValue,TolerancePlus,ToleranceMinus,MeasuredValue,Judgement,HasResult,OverallCycleResult,검사구분,선택Z";
// 신규 — 맨 끝에 1열 추가
private const string CSV_HEADER = "...,선택Z,사용기준";
// fields.Add(MapRefSource(meas));  // 선택Z 다음에 추가

// Source: WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs:122,142 (기존)
private const int COLUMN_COUNT = 14;      // 그대로 유지 — 손상 행 가드
private const int COL_SELECTED_Z = 15;
// 신규
private const int COL_REF_SOURCE = 16;
// 읽기: bool bHasCol = fields.Count > COL_REF_SOURCE; string szRefSource = bHasCol ? fields[COL_REF_SOURCE] : null;
```

### 오버레이 색상 분기 추가
```csharp
// Source: WPF_Example/Halcon/Display/HalconDisplayService.cs:272-276 (기존, FAI-DistLine 옆에 추가)
else if (string.Equals(overlay.RoiId, "FAI-DistLine", StringComparison.OrdinalIgnoreCase))
{
    window.SetColor("cyan");
    window.SetLineWidth(1);
}
else if (string.Equals(overlay.RoiId, "FAI-RefLine", StringComparison.OrdinalIgnoreCase)) // 신규
{
    window.SetColor("orange"); // FAI-Edge(녹/적)·FAI-DistLine(cyan) 과 겹치지 않는 색
    window.SetLineWidth(1);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| EdgeToLineDistance 는 항상 전역 2점 datum 기준선까지 거리 | 측정별 옵션으로 같은 사진의 국부 띠 기준선까지 거리(실패 시 전역 폴백) | Phase 79 (D-79-01~03) | 자재 간 편차(도면 기준=핀 옆 띠 면일 때) 감소 기대, 단 O-79-01 이 보인 초점 불일치로 활성 빈도는 실측 전까지 불확실 |

**Deprecated/outdated:** 없음(전역 datum 기준 방식은 이 옵션이 꺼진 모든 측정·구 레시피에서 그대로 유지).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | O-79-02 기울기 선택 — (b) 위치만 로컬+전역 각도 유지가 (a) 자체 기울기보다 안정적일 것 | Architecture Patterns > Pattern 2 | LSR-06 실측에서 (b) 가 오히려 자재차를 덜 줄이면 (a) 로 전환 필요 — 상수 토글로 대비 권장 |
| A2 | LocalRef 전용 Edge 설정(Sigma 상향 등)이 O-79-01 의 블러 문제를 완화할 것 | Common Pitfalls > Pitfall 1 | 실제 HALCON 서브픽셀 피팅(육안 아님)으로 검증 안 됨 — Wave 0/1 에서 실측 필요, 최악의 경우 여전히 폴백 빈발 |
| A3 | `EdgePolarity`/`EdgeDirection` 문자열이 INI 키 누락 시 빈 문자열로 로드되어도 `TryFitLine` 이 안전 폴백(LtoR/DarkToLight 취급)한다 | Architecture Patterns > Pattern 4 | 실제 `IniValue.ToString()` 기본 반환값(빈 문자열 vs null)을 코드로 재확인하지 않음 — Open Question 2 |
| A4 | 09-17 CONTEXT.md 근거 수치(왼쪽 +2.8µm 등)는 자동 Z선택이 아니라 수동으로 고른 선명 사진 기준일 가능성이 높다 | BLOCKER 절 | 만약 실제로 자동선택 사진 기준이었다면 이 리서치의 리스크 평가가 과장된 것 — Open Question 1 로 확인 필요 |

**빈 테이블 아님 — 위 4건은 계획·검증 단계에서 반드시 재확인.**

## Open Questions

1. **09-17 근거 데이터가 어떤 Z 선택 방식으로 얻어졌는가?**
   - What we know: CONTEXT.md 는 "핀 바로 옆 띠 기준으로 재면 자재 차이가 사라짐(왼쪽 +2.8µm/오른쪽 +4.0µm)"이라고만 기록, Z 선택 방식(자동 vs 수동 특정 사진)은 명시 안 됨.
   - What's unclear: 이 리서치가 발견한 "자동선택 Z 에서는 배경 띠가 흐릿하다"는 사실과 09-17 분석이 상충하는지, 아니면 09-17 분석이 다른(더 선명한) 사진을 썼는지.
   - Recommendation: 계획/실행 전에 사용자에게 09-17 분석에 쓴 정확한 사진 파일명·Z 값을 확인 요청. 확인되면 그 Z 에서 각 컬럼의 초점 상태를 재크롭 대조.

2. **`IniValue.ToString()`/`ToDouble()`/`ToBool()` 의 정확한 기본값 반환 로직(파라미터 없는 오버로드)?**
   - What we know: `ParamBase.Load` 가 `loadFile[group][name].ToBool()`(인자 없음)을 호출하며, `MeasurementBase.cs:64` 주석은 이것이 "인자없는 기본값(0/false)"이라고 명시.
   - What's unclear: `IniValue` 클래스 정의 파일을 직접 열람하지 않았다(시간 제약) — `ToString()` 무인자 오버로드가 `null` 을 반환하는지 `""` 를 반환하는지 미확인.
   - Recommendation: 계획 단계에서 `WPF_Example/Utility/`(또는 `IniFile` 정의 위치) 의 `IniValue` 클래스를 열람해 `ToString()` 반환값을 확정하고, `LocalRefEdgePolarity`/`LocalRefEdgeDirection` 에 override 가 필요한지 결정.

3. **`EdgeOptionLists` 신규 값 추가 필요 여부** — 기준 ROI 가 핀과 다른 `EdgeDirection`(예: 핀은 BtoT/TtoB, 기준 ROI 도 띠의 수평 에지를 잡으므로 동일 방향 계열)을 쓸 가능성이 높지만, 레시피 실측 6건 전부 핀 자신도 BtoT/TtoB 라서 기준 ROI 도 같은 방향이면 충분해 보인다 — LSR-06 실측 단계에서 재확인.

## Environment Availability

이 phase 는 순수 코드/레시피 변경이며 신규 외부 도구·서비스 의존이 없다(HALCON 은 이미 설치·검증됨, CLAUDE.md 명시 경로). 섹션 스킵 조건 충족 — 생략.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | 없음(xUnit/NUnit/MSTest 프로젝트 미존재, CLAUDE.md 명시) |
| Config file | 없음 — 검증은 빌드+grep+오프라인 재검사로 대체 |
| Quick run command | `"/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -m -nologo -v:minimal -clp:ErrorsOnly` [VERIFIED: Phase 77/78 PLAN.md 다수 실사용] |
| Full suite command | 위와 동일 + 아래 하드룰 grep + 오프라인 재검사(D-77-06 OfflineSelect 모드) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| LSR-01 | 신규 필드·기본값 존재 | 정적 grep | `git diff --name-status BASE HEAD -- EdgeToLineDistanceMeasurement.cs` + 필드명 grep | ✅ (기존 파일 확장) |
| LSR-02 | 국부 기준 측정값 계산 | 오프라인 재검사(수동) | Phase 77 D-77-06 OfflineSelect 모드로 09-17 저장 사진 재측정, `LastRefSource=="Local"` 인 케이스의 값이 09-17 CONTEXT 수치(왼쪽 +2.8µm 근방)와 근접한지 육안+수치 대조 | ❌ 신규 수행 필요(Wave 0/1 산출물 없음, 실제 앱으로 재검사) |
| LSR-03 | 자동 전환 무중단 | 오프라인 재검사 + 로그 확인 | 기준 ROI 를 의도적으로 미티칭(Length=0)한 측정으로 재검사 → `[LocalRef] 기준 ROI 실패 → 전역 기준 전환` 로그 발생 + 사이클 정상 종료 확인 | ❌ 신규 |
| LSR-04 | 표시·기록 | 시각 확인 + grep | 리뷰어에서 "국부/전역/전환" 표시 확인, cycle.json 에 `RefSource` 필드 존재 grep | ❌ 신규 |
| LSR-05 | 회귀 0 | 정적 grep + 오프라인 재검사 bit-비교 | 옵션 OFF 측정 18건(Phase 76 조사 기준)의 옵션 적용 전/후 `LastMeasuredValue` 동일 값(재검사 결과 diff 0) | ❌ 신규(비교 대본 필요) |
| LSR-06 | 자재 A·B 검증 | 오프라인 재검사 + 수동 통계 | 09-17 저장 사진(자재 A 3사이클·B 3사이클, 이 리서치가 특정한 파일)으로 옵션 ON 재검사 → A-B 갭이 전역 기준 대비 축소되는지 확인 | ❌ 신규 |

### 하드룰 grep (신규/수정 파일 대상, 전부 0 이어야 함 — 78-06-PLAN.md 실사용 패턴)
```bash
ADD=$(git diff -w -U0 $BASE HEAD -- "$FILE" | grep -E '^\+' | grep -vE '^\+\+\+')
echo "ternary=$(printf '%s\n' "$ADD" | grep -cE '\?[^?]*:')"
echo "coalesce=$(printf '%s\n' "$ADD" | grep -cF '??')"
echo "nullcond=$(printf '%s\n' "$ADD" | grep -cF '?.')"
echo "switchexpr=$(printf '%s\n' "$ADD" | grep -cE 'switch.*=>')"
echo "datesig=$(printf '%s\n' "$ADD" | grep -cF 'hbk')"
echo "logic3=$(printf '%s\n' "$ADD" | grep -cE '(&&|\|\|).*(&&|\|\|).*(&&|\|\|)')"
```

### Sampling Rate
- **Per task commit:** Debug|x64 quick run command (에러 0 확인, 경고 baseline 은 실행 직전 실측 — Phase 73 이력상 숫자가 phase 마다 조금씩 달라질 수 있어 계획 단계에서 재측정 권장)
- **Per wave merge:** 하드룰 grep 전체 + 옵션 OFF 회귀 오프라인 재검사(bit-비교)
- **Phase gate:** LSR-06 A/B 비교 수치 확보 후 `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `IniValue.ToString()`/무인자 오버로드 정확한 반환값 확인(Open Question 2) — LocalRef 문자열 필드 override 필요 여부 결정에 선행
- [ ] 09-17 근거 데이터의 정확한 Z/사진 출처 확인(Open Question 1) — 사용자 문의
- [ ] 오프라인 재검사(D-77-06 OfflineSelect) 로 실제 HALCON `TryFitLine` 을 09-17 저장 사진에 돌려보는 최소 스모크 — 이 리서치의 육안 판단(BLOCKER 절)을 서브픽셀 결과로 교차검증
- [ ] Debug|x64 최신 빌드 경고 baseline 재측정(Phase 73 이후 라인 수가 달라졌을 수 있음)

*(테스트 프레임워크 부재는 기존 정책 — 신규 도입은 이 phase 범위 밖)*

## Security Domain

이 phase 는 인증/세션/네트워크 프로토콜을 건드리지 않는다(D-79-08 PLC z 번호·TCP 프로토콜 불변). 신규 입력은 PropertyGrid 로 사용자가 직접 입력하는 ROI 좌표·에지 설정값(로컬 신뢰 입력, 원격 공격 표면 아님)뿐이다.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | 변경 없음(`LoginManager` 무관) |
| V3 Session Management | no | 해당 없음 |
| V4 Access Control | no | 해당 없음 |
| V5 Input Validation | yes(경미) | ROI `Length1/Length2<=0` 가드(기존 패턴 재사용), `EdgeThreshold`/`Sigma` 등 숫자 입력은 PropertyGrid 가 타입 강제(double/int) — 별도 검증 로직 불필요 |
| V6 Cryptography | no | 해당 없음 |

### Known Threat Patterns for {stack}

이 phase 의 신규 입력 표면은 오직 레시피 파일(INI/JSON, 로컬 신뢰 파일)과 PropertyGrid UI 뿐이라 STRIDE 관점의 신규 위협 패턴은 식별되지 않는다.

## Sources

### Primary (HIGH confidence — 코드/실측 직접 확인)
- `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs` (전체) — 현재 axis 계산·overlay 구조
- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` (전체) — `LastFitScore`/`LastSelectedZIndex` 필드 패턴, `Load` override 패턴, `ClearResult`
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs:1-260` — `TryFitLine` strip-loop 구조
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:1880-2670` — `InjectDatumOrigin`, `ExecuteZRangeSelection`, `RunZFocusCandidates`, `PickZFocusResult`
- `WPF_Example/Custom/Sequence/Inspection/Measurements/LineToLineDistanceMeasurement.cs` (전체) — 2-ROI 측정 전례
- `WPF_Example/UI/ContentItem/MainView.xaml.cs:395-975` — ROI 티칭 배선 4함수
- `WPF_Example/Halcon/Display/HalconDisplayService.cs:150-310` — overlay 색상 분기
- `WPF_Example/UI/ViewModel/CycleResultDto.cs:90-230` — DTO 구조
- `WPF_Example/Sequence/Param/ParamBase.cs:315-420` — INI 리플렉션 Save/Load
- `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs`, `MeasurementHistoryCsvLoader.cs` — CSV 하위호환 패턴
- `D:\Data\Recipe\FAI_1\main.ini` (라인 5266~5600대) — C13/C14 6개 측정 실제 ROI·에지 설정값
- `D:\Data\Result\20260917\*_cycle\cycle.json` (6개 최종 tick, A/B 각 3사이클) — 실측 A-B 편차 재계산(C13_P1 +3.7µm, C14_P1 -0.3µm, C13_P2 -10.8µm, C14_P2 -5.2µm, C13_P3 +47.7µm, C14_P3 +47.2µm — 이 세션에서 직접 계산, 09-17 CONTEXT 수치와 크기 정합)
- `D:\Data\Result\Image\260917\{1740,1743,1746}\original\*.jpg` — 육안 크롭 대조(9개 크롭, 좌표 Pitfall 1 표에 기록)
- `.planning/phases/78-reviewer-ng-cause-analysis/78-06-PLAN.md` — 하드룰 grep 명령 실사용 예
- `.planning/phases/77-side-z-focus-select/77-HUMAN-UAT.md`, `77-VALIDATION.md` — 검증된 MSBuild 명령

### Secondary (MEDIUM confidence)
- `.planning/STATE.md` (2026-08-27 핸드오프) — MSBuild 경로·경고 baseline·재발방지 3건 메모(경고 baseline 은 Phase 73 시점 수치라 재측정 권장)

### Tertiary (LOW confidence — 이 세션 육안 판단, HALCON 서브픽셀 결과 아님)
- BLOCKER 절의 "선명/흐림" 판단 전부 — JPG 압축·다운스케일 표시 기반 육안 평가. 실제 `measure_pos`/`FitLineContourXld` 서브픽셀 결과와 다를 수 있음(Wave 0 Gaps 참고)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — 신규 패키지 없음, 기존 함수 재사용만 확인
- Architecture: HIGH — 실제 코드 읽고 정확한 삽입 지점·기존 패턴 대조 완료
- 핵심 전제(O-79-01 사진 내 띠 시인성): MEDIUM — 실제 저장 사진 다수 대조했으나 육안 판단(JPG)이며 HALCON 서브픽셀 검증 아님. 이 리서치의 최대 리스크

**Research date:** 2026-09-18
**Valid until:** 이 phase 계획·실행이 시작되면 즉시(레시피/사진은 정적 데이터, 코드 구조는 안정적 — 단 09-17 이후 레시피 재티칭이 있었다면 ROI 좌표 재확인 필요)
