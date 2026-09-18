# Phase 79: SIDE 핀 옆 띠 기준 옵션 (EdgeToLineDistance 국부 기준선) - Research

**Researched:** 2026-09-18 (개정 2026-09-18 — D-79-05/D-79-09 변경 반영)
**Domain:** HALCON 에지 라인 피팅(`VisionAlgorithmService.TryFitLine`) 재사용 + Datum 검출 시점 사전계산 + `EdgeToLineDistanceMeasurement` 측정별 옵션 + INI/JSON 하위호환 + WPF ROI 티칭 배선
**Confidence:** MEDIUM-HIGH — 코드 구조·레시피·과거 실측 데이터(z1 이미지 기준)는 실제 파일 대조로 HIGH. 실행 설계(사전계산 지점)는 코드 구조상 명확히 HIGH. O-79-10(ROI 위치 선택)은 실측 근거가 있으나 표본 수(A 4사이클·B 3사이클, EdgeProbe 단순 알고리즘)가 적어 MEDIUM.

## 개정 이력

- **2026-09-18 최초:** "기준 ROI는 측정과 같은 사진(z3~z9)에서 찾는다"(D-79-05 최초안)로 조사 → 측정 사진에서 핀 옆 띠가 흐려 BLOCKER 로 표시.
- **2026-09-18 개정(본 버전):** 사용자가 D-79-05 를 "기준점 가로 사진(z1)에서 찾는다"로 변경 + D-79-09 를 "SIDE·TOP·BOTTOM 전체에 레시피 티칭만으로 적용 가능"으로 확대. 아래 내용은 전부 이 개정판 기준이다. **BLOCKER 해소됨** — 근거는 "Datum 이미지 가용성" 절 참고.

## Summary

Phase 79 는 새 알고리즘을 만들지 않는다. 개정된 설계는 두 단계로 나뉜다:

1. **DatumPhase(기준점 검출) 시점 — 국부 기준선 사전계산.** SIDE/TOP/BOTTOM 모든 Datum 알고리즘(`CircleTwoHorizontal`, `VerticalTwoHorizontalDualImage` — 이 레시피에 실사용되는 유일한 2종, 아래 확인)은 검출을 위해 반드시 HImage 1장(1-image 알고리즘) 또는 2장(DualImage, 가로축=`imgH`)을 grab 한다. 이 이미지는 **`Action_FAIMeasurement.ProcessDatumSingleImage`/`ProcessDatumDualImage` 안에서 검출 직후 `finally` 블록으로 즉시 Dispose 된다**(`WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:471-473`, `:407-410`) — 측정 tick(z3~z9)까지 살아있지 않는다. 따라서 **"이미지를 들고 있다가 측정 시점에 쓰는" 방식(O-79-08 옵션 ii)은 불가능하거나 대형 메모리 위험**을 재도입하며, **"검출 시점에 즉시 국부 기준선을 피팅해 숫자로 저장"(옵션 i)이 유일하게 안전한 설계**다. 이 시점에 `ShotParam.FAIList`(이미 `IsDatumOwnedByCurrentShot` 이 쓰는 것과 동일한 접근, `Action_FAIMeasurement.cs:354-359`)를 순회해 `DatumRef==datum.DatumName && IsLocalRefEnabled` 인 `EdgeToLineDistanceMeasurement` 를 찾아, 그 측정의 `LocalRef_*` ROI 로 `TryFitLine` 을 한 번 호출하고 결과(2점 또는 실패)를 그 측정 객체의 새 런타임 필드에 저장한다.
2. **Measure 단계(z3~z9, Phase 77 Z 자동선택) — 소비만.** `EdgeToLineDistanceMeasurement.TryExecute` 는 기존처럼 핀 에지를 측정 사진에서 피팅하되, 기준선(axis) 결정 시 "1) 사전계산된 국부 기준선이 있으면 그것을 쓰고, 2) 없으면(옵션 꺼짐 또는 사전계산 실패) 기존 전역 datum 축을 그대로 쓴다." **재계산·재피팅은 전혀 없다** — Z 후보마다 다시 도는 게 아니라 사이클(정확히는 이 Datum 이 검출된 그 tick)당 1번만 계산된 값을 그대로 재사용한다(D-79-05/O-79-03).

이 설계는 SIDE 뿐 아니라 **레시피에 이미 존재하는 모든 EdgeToLineDistance 측정에 코드 변경 없이 적용된다** — 왜냐하면 사전계산 훅이 "이 Datum 을 검출하는 모든 지점"(1-image/DualImage 공용 헬퍼)에 한 곳만 추가되고, 그 안에서 "이 Datum 을 참조하며 옵션을 켠 측정"을 데이터(DatumRef, IsLocalRefEnabled)로만 찾기 때문이다. 실제 레시피 스캔 결과 TOP 59개·BOTTOM 19개·SIDE_1/2 각 9개, 총 96개의 EdgeToLineDistance 측정이 이미 존재하며 전부 이 메커니즘의 적용 대상이 된다(D-79-09).

가장 중요한 발견 두 가지:
- **BLOCKER 해소:** z1 기준점 가로 사진은 09-17 이전 세션의 `EdgeProbe` 실측(A 4사이클·B 3사이클, 17:38~17:46)에서 띠 에지 세기 8~30(강함), 사이클 간 흔들림 1px 이하로 확인됐다 — 측정 사진(z3~z9)과 달리 초점 문제가 없다.
- **새 위험 — ROI 위치(왼쪽/오른쪽 중 어느 창) 선택이 결과를 좌우한다(O-79-10).** 같은 "P1 왼쪽"이라도 핀에 더 가까운 창(stripL, col 1400–1580)은 A−B 갭을 −48µm 대에서 −2.7µm 로 줄이지만, 조금 더 먼 창(stripL2, col 1760–1940)은 오히려 +60µm 로 **더 나빠진다** — 실측으로 처음 확인됨(아래 "O-79-02/O-79-10 실측" 절). 두 창이 다른 에지를 잡고 있다는 뜻이며, 계획 단계는 이 실측 없이 "아무 데나 놓아도 된다"고 가정하면 안 된다.

**Primary recommendation:**
1. Datum 검출 헬퍼(`RunDatumSingleImageDetection`/`RunDatumDualImageDetection`, 검출 성공 직후)에 신규 private 메서드 `ComputeLocalRefLines(datum, parentSeq, img)` 를 추가해 국부 기준선을 사전계산 — 이 한 곳만 SIDE/TOP/BOTTOM 공용으로 동작.
2. `EdgeToLineDistanceMeasurement.TryExecute` 의 axis 계산부는 "사전계산 값이 있으면 그것, 없으면 전역"으로 1줄짜리 분기만 추가 — 옵션 꺼짐 경로는 완전히 무변경.
3. 기준 ROI 위치는 **핀에 가장 가까운 창**을 우선 사용하고, 두 후보 창(왼쪽/오른쪽 각 1개씩 넓게 잡거나, 넓은 ROI 1개로 strip-loop 이 알아서 평균내게)을 실측(LSR-06)으로 검증한 뒤 확정한다. UAT 절차에 "에지 세기·좌우 후보 비교" 단계를 반드시 넣는다.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| 국부 기준선 사전계산(LSR-01/02, 신규) | Sequence(`Action_FAIMeasurement.ComputeLocalRefLines`, DatumPhase 실행 중) | Backend(`VisionAlgorithmService.TryFitLine`) | Datum 이미지가 살아있는 유일한 시점이 DatumPhase 내부이므로, 이 계층에서만 국부 기준선을 만들 수 있다 |
| 기준선 소비(axis 결정, LSR-02) | Sequence(`EdgeToLineDistanceMeasurement.TryExecute`) | — | 순수 값 대입(신규 계산 없음) — 옵션 꺼짐 시 이 계층은 기존 코드와 완전히 동일 |
| 자동 전환·로그(LSR-03) | Sequence(측정 클래스 내부) | Utility(`Logging.PrintLog`) | 사전계산 실패도, 사전계산 자체가 없었던 경우도 여기서 동일하게 "전역 폴백"으로 흡수 |
| 사용 기준 표시·기록(LSR-04) | Backend(DTO) | Frontend(리뷰어/결과 그리드/CSV) | Phase 78 확립 원칙 재사용 |
| 회귀 0(LSR-05) | Backend(측정·Datum·INI·JSON 전 계층) | — | 옵션 OFF 인 측정은 `ComputeLocalRefLines` 의 필터(`IsLocalRefEnabled`)에서 걸러져 계산 자체가 안 됨 |
| TOP·BOTTOM 적용(D-79-09) | Sequence(공용 Datum 검출 헬퍼) | — | 사전계산 훅이 시퀀스 종류에 무관한 공용 코드 경로 1곳에만 존재 — 신규 분기 불필요 |

## User Constraints (from CONTEXT.md)

<user_constraints>

### Locked Decisions

- **D-79-01:** 목적은 자재 간 측정 편차 감소. 원인은 금속 띠 휨이고, 핀 옆 띠 기준 옵션(방법 2)으로 해결한다.
- **D-79-02:** 도면의 C13·C14 높이 기준은 핀 옆 띠 면이다. 국부 기준 값이 도면 치수와 같은 뜻이다.
- **D-79-03:** EdgeToLineDistance 에 측정별 체크박스 + 기준 ROI 1개를 추가한다. 기준 ROI 의 띠 에지 라인을 0점으로 쓴다. 측정마다 따로 켤 수 있다.
- **D-79-04:** 기본 꺼짐. 옛 레시피와 옵션 꺼진 측정은 현재와 완전히 같다.
- **D-79-05 (2026-09-18 개정):** **기준 ROI 는 기준점 가로 사진(z1)에서 찾는다.** 핀은 지금처럼 측정 사진(Z 자동 선택)에서 잰다. Z 자동 선택은 그대로다. 국부 기준선은 사이클(정확히는 이 Datum 검출 tick)당 1번만 계산한다(후보마다 재계산 안 함).
- **D-79-06:** 기준 ROI 를 못 찾으면 기존 전역 기준선으로 자동 전환하고 로그를 남긴다. 검사는 멈추지 않는다.
- **D-79-07:** 결과 화면과 기록에 사용 기준(국부 / 전역 / 국부 실패→전역)을 표시한다.
- **D-79-08:** Z 범위 자동 선택 구조, 레시피 구조, PLC z 번호·프로토콜, Datum 알고리즘은 바꾸지 않는다.
- **D-79-09 (2026-09-18 개정, 확대):** 범위는 ① 옵션 개발 ② SIDE C13·C14 6점 자재 A·B 검증. **옵션은 SIDE·TOP·BOTTOM 의 모든 EdgeToLineDistance 측정에서 레시피 티칭만으로 켤 수 있어야 한다**(다른 검사에 켤 때 코드 수정 불필요). 공통 규칙 = 그 측정의 Datum 이 가로선을 찾은 사진(그 Datum 검출용 이미지)에서 기준 ROI 를 찾는다. 효과 검증 대상은 C13·C14 6점. TOP·BOTTOM 은 동작·폴백 확인까지. F9·다른 측정 타입 확대는 범위 밖.

### Claude's Discretion (열린 항목 — 이 리서치의 결론 반영)

| ID | 항목 | 이 리서치의 결론 |
|---|---|---|
| O-79-01 | ~~측정 사진에서 띠가 잡히는가~~ | **해소.** D-79-05 개정으로 z1 이미지 사용 — 흐림 문제 회피 |
| O-79-02 | 기준선 기울기 (a)자체 기울기 vs (b)위치만+전역각도 | **(b) 권장, 신뢹도 상향.** 실측(아래)에서 인접한 두 후보 창(왼쪽 stripL/stripL2)이 94px 이나 어긋나는 사례를 발견 — 좁은 간격의 창 2개로 기울기(secant)를 추정하면 이런 오류를 그대로 기울기로 흡수한다. 각도는 전역 `DatumAngleRad`(이미 작음, -0.5°) 유지, 위치만 국부 보정이 안전 |
| O-79-03 | Z 범위 자동 선택과의 관계 | **코드 변경 없음, 확정.** 국부 기준선은 Datum 검출 tick 에서 1번 계산 후 값으로 캐시 — `RunZFocusCandidates` 는 그 값을 그대로 읽기만 한다(재피팅 없음) |
| O-79-08 | Datum 이미지 가용 방법 | **옵션 (i) 확정 — 검출 시점 사전계산.** 이미지가 검출 직후 Dispose 되므로 (ii)는 위험(메모리, 08-06 인시던트 재현 우려)하고 사실상 불가능(스코프 밖). 아래 "Datum 이미지 가용성" 절 |
| O-79-09 | ROI 좌표계·티칭 이미지 | **같은 카메라 → 같은 픽셀 좌표계, `datumTransform` 도 동일값 재사용 가능.** 티칭은 측정 이미지 위에서 그대로 그려도 되고(좌표만 맞으면 됨), 사용자가 z1 이미지에서 위치를 미리 확인하고 싶으면 Datum 노드를 선택해 `TeachingImagePath` 를 보면 된다(기존 기능, 신규 UI 불필요) |
| O-79-10 (신규) | 기준 ROI 좌우/위치 선택 | **실측으로 확인 — 창 선택이 결과를 크게 좌우한다.** 핀에 더 가까운 창을 권장. 아래 표 참고 |
| O-79-04 | 기록 위치 | 변경 없음 — cycle.json 필드 + CSV 끝 열(77-03 선례) |
| O-79-05 | 기준값·공차 | 변경 없음 |
| O-79-06 | 기준 ROI 티칭·표시 | 변경 없음 — 기존 배선 재사용 |
| O-79-07 | 이름 | 변경 없음 |

### Deferred Ideas (OUT OF SCOPE)

- F9 및 다른 위치 측정 확대 — 근처에 쓸 만한 직선이 그 Datum 이미지에서 선명한지 확인 후 별도 phase
- EdgeToLineAngle 등 다른 측정 타입에 같은 옵션 — 효과 확인 후
- z1 사진 흔들림(PLC 대기시간) — PLC 테스트로 별도 확인. **전역 기준선도 z1 에서 오므로 이 어긋남은 옵션 켜도 새로 추가되지 않는다**(오늘과 동일하게 남음, 사용자 확인 완료)
- 평균값의 도면값 치우침 — 보정/티칭 문제로 별도

</user_constraints>

## Project Constraints (from CLAUDE.md)

- C# 7.2 고정. 삼항 `?:` / 이항 `??`·`??=` / null 조건 `?.`·`?[]` / C# 8 `switch` 식 전부 금지 — `if/else`, 명시적 null 체크, 전통 `switch`(+`break`)만 사용.
- 헝가리언 접두사, 매직넘버 금지(named const), 3개 이상 `&&`/`||` 조건은 이름 있는 bool 로 선추출.
- 새 UI 로직은 ViewModel/서비스 계층에만. `MainView.xaml.cs` 는 이번에 손대는 지점(ROI 배선 4함수)에만 최소 추가.
- 날짜 주석 신규 금지 — `// Phase 79 LSR-xx: ...` 형식만.
- `HImage`/`HObject`/`HTuple` 반드시 Dispose. **이번 설계는 DatumPhase 의 `img`/`imgH` 를 그대로 재사용(추가 grab 없음)하므로 새 Dispose 대상은 없다** — 단, `TryFitLine` 내부의 `contour`(HObject)는 그 함수 자신이 Dispose 하므로 호출부는 신경 쓸 필요 없음.
- 신규 .cs 파일 금지 — 전부 기존 파일에 추가.
- `D:\Data*` 읽기 전용. Release 빌드 금지. Debug|x64 빌드만.
- 옵션 꺼짐 경로: 측정값·판정·기록·표시 불변. cycle.json 은 필드 추가만.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| LSR-01 | 옵션·기준 ROI 추가 | "Standard Stack" + "Architecture Patterns > Pattern 1/3" |
| LSR-02 | 국부 기준 측정 | "Architecture Patterns > Pattern 1(사전계산)/Pattern 2(소비)" — Datum 이미지 가용성 절 근거 |
| LSR-03 | 자동 전환(폴백) | "Architecture Patterns > Pattern 2" + "Common Pitfalls > Pitfall 2" |
| LSR-04 | 사용 기준 표시·기록 | "Don't Hand-Roll" + "Code Examples > CSV 확장"(변경 없음) |
| LSR-05 | 회귀 0 | "Runtime State Inventory" + "Common Pitfalls > Pitfall 3/4" |
| LSR-06 | C13·C14 자재 A·B 검증 | "O-79-02/O-79-10 실측 결과" + "Validation Architecture" |

</phase_requirements>

## Standard Stack

새 NuGet 패키지 없음. 기존 `halcondotnet`(HALCON 24.11)의 `VisionAlgorithmService.TryFitLine` 을 그대로 재사용.

**Installation:** 없음. **Package Legitimacy Audit:** N/A(신규 패키지 없음).

## Datum 이미지 가용성 (O-79-08 — 조사 결과)

### 사용 중인 Datum 알고리즘 (레시피 실측, `D:\Data\Recipe\FAI_1\main.ini`)

| Datum | AlgorithmType | 이미지 종류 | 소속 시퀀스 |
|---|---|---|---|
| Side_Datum_1~4 | `VerticalTwoHorizontalDualImage` | 2장(imgH=가로축, imgV=세로축) — **가로선은 imgH 에서 찾음** | SIDE_1~4 |
| Top_Datum | `CircleTwoHorizontal` | 1장(img) — Step1 원 + Step2/3 수평 A/B ROI 를 **같은 이미지**에서 찾음 | TOP |
| Bottom_Datum | `CircleTwoHorizontal` | 1장(img) | BOTTOM |

[VERIFIED: `D:\Data\Recipe\FAI_1\main.ini` `AlgorithmType=` grep 결과 이 2종만 실사용. `EDatumAlgorithm` enum 에는 `TwoLineIntersect`/`VerticalTwoHorizontal` 도 있으나(`WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs:5-11`) 이 레시피엔 없음] — 즉 **"가로선 이미지가 아예 없는 Datum"은 이 레시피에 존재하지 않는다.** `CircleTwoHorizontal`(TOP/BOTTOM)도 명시적으로 "Step 2/3 Horizontal A/B" 를 수행하므로(`DatumFindingService.cs:256-272` 주석) 가로선 이미지가 항상 있다. 일반적으로도 이 4개 알고리즘 전부 "검출에 쓴 이미지"가 곧 "가로선을 찾은 이미지" 이거나(1-image), 최소한 imgH 가 그 이미지다(DualImage) — **"가로선 이미지가 없는 Datum" 이라는 O-79-09 task 5 가 우려한 케이스는 이 코드 모델에서 구조적으로 발생하지 않는다.** (혹시 미래에 그런 알고리즘이 추가되면 "사전계산 훅이 이미지를 못 받음 → 그냥 호출 안 됨 → LastLocalRefFound=false → 전역 폴백" 으로 자연히 안전하다.)

### 이미지 수명 (핵심 근거)

`Action_FAIMeasurement.cs`:
- **1-image (CircleTwoHorizontal, `ProcessDatumSingleImage`, :444-474):** `HImage img = GrabOrLoadDatumImage(datum);`(:445) → `RunDatumSingleImageDetection(datum, parentSeq, img, ...)`(:460, 이 안에서 `TryComposeAlign`/`TryRunSingleDatum` 호출) → `ArchiveDatumImageForCycle`(:463, 사본 저장) → **`finally { img.Dispose(); }`(:471-473)**.
- **DualImage (VerticalTwoHorizontalDualImage, `ProcessDatumDualImage`, :364-411):** `HImage imgH = null, imgV = null;`(:377) → `TryGrabOrLoadDualDatumImages(...)`(:381) → `RunDatumDualImageDetection(datum, parentSeq, imgH, imgV)`(:394, 이 안에서 `TryComposeAlign`/`TryRunSingleDatum` 호출) → **`finally { SafeDisposeImage(imgH); SafeDisposeImage(imgV); }`(:407-410)**.

두 경로 모두 **검출 성공 여부와 무관하게, 이 메서드가 리턴하기 전에 이미지가 사라진다.** 이 시점은 `EStep.DatumPhase`(측정 tick 인 `EStep.Measure` 보다 먼저 실행, `Action_FAIMeasurement.cs:1891` 주석 확인)이며, 이 시퀀스의 전용 스레드(`SequenceBase` 가 시퀀스마다 만드는 스레드, CLAUDE.md 아키텍처 문서 확인) 위에서 순차 실행된다 — 같은 스레드 안에서 grab→검출→(신규)국부기준선계산→dispose 가 순서대로 일어나므로 **락(lock) 추가가 필요 없다.**

### 결론 — 옵션 (i) 채택 근거

- **옵션 (ii, 이미지를 들고 있다가 측정 시점에 피팅)를 채택하면:** 13376×9528 HImage(약 127MB 급, STATE.md 2026-08-06 인시던트 실측 규모와 동일 오더)를 최소 이 Datum 을 참조하는 마지막 측정 tick까지(SIDE 는 z9까지, TOP 은 그 Shot 의 마지막 FAI 까지) **복제해서 보관**해야 한다 — `ShotConfig._image`/크로스-Z 저장소가 이미 겪은 "사이클 종료 후에도 영구 보존" 급 메모리 인시던트를 재현할 위험이 크다. 또한 수동 단일측정(Test 버튼)·오프라인 재검사(D-77-06 OfflineSelect)·SIMUL_MODE 저장사진 재현 등 "Datum 이미지와 측정 이미지가 같은 실행 흐름에 없는" 경로들과 별도로 다시 이미지를 확보해야 해 구현이 여러 갈래로 갈라진다.
- **옵션 (i, 검출 시점에 즉시 피팅해 숫자만 저장)을 채택하면:** 메모리 증가 없음(같은 이미지, 추가 복제 없음), 락 불필요(같은 스레드 순차 실행), Test 버튼/오프라인 재검사/SIMUL_MODE 전부 "Datum 이 검출되는 그 경로"만 타면 자동으로 동작(추가 분기 불필요) — **왜냐하면 이 사전계산 훅이 Datum 검출 성공/실패와 항상 함께 실행되는 하나의 지점에만 있기 때문.**

**IDatumOriginConsumer 주입 지점(참고, 기존 유지):** `Action_FAIMeasurement.InjectDatumOrigin`, `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:1890-1923` — `EStep.Measure` 진입 시 `DatumConfig.DetectedOrigin*` 를 읽어 측정에 주입한다. 이 리서치의 신규 국부 기준선 값은 이 지점을 거치지 않는다(측정 자신의 필드에 이미 있으므로) — 이 지점은 **전역 축 정보용으로만 계속 쓰인다.**

### 사전계산 삽입 지점 (신규 코드 위치, file:line)

- `RunDatumSingleImageDetection`(`Action_FAIMeasurement.cs:478-505`) — `TryComposeAlign`/`TryRunSingleDatum` 성공 분기 각각 직후.
- `RunDatumDualImageDetection`(`Action_FAIMeasurement.cs:418-441`) — 마찬가지로 성공 분기 직후, `imgH` 전달(가로축 이미지).
- 두 곳 모두 새 헬퍼 `ComputeLocalRefLines(DatumConfig datum, InspectionSequence parentSeq, HImage imgHorizontal)` 를 호출 — `ShotParam`(Action_FAIMeasurement 인스턴스 필드, `IsDatumOwnedByCurrentShot` 이미 사용 중)을 통해 `ShotParam.FAIList` 를 순회.

## Architecture Patterns

### System Architecture Diagram

```
[Shot 사이클 시작] (Action_FAIMeasurement 상태기계, EStep.DatumPhase)
        │
        ▼
┌────────────────────────────────────────────────────────────────────┐
│ ProcessDatumSingleImage / ProcessDatumDualImage                      │
│   img(H) = grab/load (예: SIDE z1, TOP/BOTTOM 해당 tick)             │
│   TryRunSingleDatum / TryComposeAlign(img, datum) → 성공 시 datum    │
│     transform 을 parentSeq._datumTransforms 에 캐시                  │
│   ┌──────────────────────────────────────────────────────────┐      │
│   │ [신규] ComputeLocalRefLines(datum, parentSeq, imgH)        │      │
│   │  parentSeq.TryGetDatumTransform(datum.DatumName, out t)    │      │
│   │  foreach fai in ShotParam.FAIList:                          │      │
│   │    foreach meas in fai.Measurements:                        │      │
│   │      if meas is EdgeToLineDistanceMeasurement etld           │      │
│   │         && etld.DatumRef == datum.DatumName                  │      │
│   │         && etld.IsLocalRefEnabled && etld.LocalRef_Length1>0 │      │
│   │           && etld.LocalRef_Length2>0:                        │      │
│   │        ok = TryFitLine(imgH, etld.LocalRef_*, t, ...)         │      │
│   │        etld.LastLocalRefFound = ok                            │      │
│   │        etld.LastLocalRefRow1/Col1/Row2/Col2 = (fit 결과)      │      │
│   └──────────────────────────────────────────────────────────┘      │
│   finally { img(H)(V).Dispose(); }  ← 기존 그대로, 신규 보관 없음      │
└────────────────────────────────────────────────────────────────────┘
        │
        ▼ (같은 사이클, 여러 tick 뒤)
[EStep.Measure, z3~z9 Phase 77 자동선택] Action_FAIMeasurement.TryExecuteMeasurement
        │
        ▼
┌────────────────────────────────────────────────────────────────────┐
│ EdgeToLineDistanceMeasurement.TryExecute(image=측정사진, ...)        │
│  1) 핀 에지 피팅 (기존, 무변경) — image(zK) 에서 TryFitLine            │
│  2) axis 결정 (신규 1줄 분기):                                        │
│     if (IsLocalRefEnabled && LastLocalRefFound):                     │
│         axis = (LastLocalRefRow1,Col1)-(Row2,Col2), RefSource="Local"│
│     else:                                                             │
│         axis = 기존 전역 DatumOriginRow/Col+DatumAngleRad 그대로       │
│         RefSource = IsLocalRefEnabled ? "GlobalFallback" : null       │
│  3) 거리 계산 (기존, 무변경) — ProjectionPl(axis)                      │
└────────────────────────────────────────────────────────────────────┘
        │
        ▼
[결과 기록] LastRefSource → cycle.json MeasurementResultDto.RefSource
         → 결과 그리드/리뷰어, CSV 끝 열
```

### Recommended Project Structure

```
WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
  ├── ComputeLocalRefLines(DatumConfig, InspectionSequence, HImage) 신규 private 메서드
  │     — RunDatumSingleImageDetection/RunDatumDualImageDetection 성공 분기 직후 호출
  └── (IsDatumOwnedByCurrentShot 의 fai.Measurements 순회 패턴 재사용)

WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs
  ├── IsLocalRefEnabled (bool, 기본 false, Category "Local Ref")
  ├── LocalRef_Row/Col/Phi/Length1/Length2 (double, Category "Local Ref|ROI")
  ├── LocalRefEdgeThreshold/Sigma/EdgeSampleCount/EdgeTrimCount/EdgePolarity/EdgeDirection
  │     (Category "Local Ref|Edge" — z1 이미지 기준 별도 튜닝값)
  ├── LastLocalRefFound(bool)/LastLocalRefRow1/Col1/Row2/Col2(double) — 신규 런타임 필드
  │     (MeasurementBase.LastFitScore 와 동일 패턴 — public **필드**, INI/JSON 자동 제외)
  ├── TryResolveMeasurementAxis(...) — 기존 인라인 axis 계산 추출 + 1) 사전계산 값 우선 대입
  └── overlay 에 FAI-RefLine 추가(LastLocalRefFound 일 때만)

WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs
  ├── LastRefSource (신규 public 필드, LastFitScore 패턴)
  └── ClearResult() 에 LastLocalRefFound=false 등 리셋 추가

WPF_Example/UI/ViewModel/CycleResultDto.cs, HalconDisplayService.cs, MainView.xaml.cs,
MeasurementHistoryCsvWriter/Loader.cs
  └── (이전 리서치 버전과 동일 — 변경 없음, 아래 "Don't Hand-Roll"/Code Examples 참고)
```

### Pattern 1: DatumPhase 에서 국부 기준선 사전계산 (신규 핵심 패턴)

**What:** Datum 검출이 성공한 직후, 아직 이미지가 살아있는 그 순간에 이 Datum 을 참조하는 모든 옵션-ON 측정의 기준 ROI 를 피팅해 결과를 측정 객체 자신에 저장한다.
**When to use:** `RunDatumSingleImageDetection`/`RunDatumDualImageDetection` 의 성공(`true` 리턴) 직후.
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:478-505 (RunDatumSingleImageDetection, 삽입 위치 예시)
private void RunDatumSingleImageDetection(DatumConfig datum, InspectionSequence parentSeq, HImage img, ref int nDatumOk, ref int nDatumFail) {
    if (datum.IsPatternAlignEnabled) {
        string modelPath = InspectionSequence.ResolveDatumModelPath(datum, parentSeq.Name);
        string alignErr;
        if (!parentSeq.TryComposeAlign(datum, img, modelPath, out alignErr)) {
            // ... 기존 실패 처리 ...
            nDatumFail++;
        } else {
            nDatumOk++;
            ComputeLocalRefLines(datum, parentSeq, img); // 신규 — 성공 직후만
        }
    } else {
        string derr;
        if (!parentSeq.TryRunSingleDatum(datum, img, null, out derr)) {
            // ... 기존 실패 처리 ...
            nDatumFail++;
        } else {
            nDatumOk++;
            ComputeLocalRefLines(datum, parentSeq, img); // 신규
        }
    }
}

// 신규 헬퍼 — IsDatumOwnedByCurrentShot(:347-361)의 순회 패턴을 그대로 재사용
private void ComputeLocalRefLines(DatumConfig datum, InspectionSequence parentSeq, HImage imgHorizontal) {
    if (ShotParam == null || ShotParam.FAIList == null) { return; }
    HTuple transform;
    bool bHasTransform = parentSeq.TryGetDatumTransform(datum.DatumName, out transform);
    if (!bHasTransform) { return; } // 방금 검출 성공했으므로 정상적으로는 항상 true
    foreach (var fai in ShotParam.FAIList) {
        if (fai == null) { continue; }
        foreach (var meas in fai.Measurements) {
            var etld = meas as EdgeToLineDistanceMeasurement;
            if (etld == null) { continue; }
            if (etld.DatumRef != datum.DatumName) { continue; }
            if (!etld.IsLocalRefEnabled) { continue; }
            bool bTaught = etld.LocalRef_Length1 > 0 && etld.LocalRef_Length2 > 0;
            if (!bTaught) { continue; }
            var svc = new VisionAlgorithmService(); // EdgeScore 미설정 — Z선택 점수와 무관(D-79-08)
            double r1, c1, r2, c2; string err;
            bool ok = svc.TryFitLine(imgHorizontal,
                etld.LocalRef_Row, etld.LocalRef_Col, etld.LocalRef_Phi,
                etld.LocalRef_Length1, etld.LocalRef_Length2, transform,
                etld.LocalRefEdgeSampleCount, etld.LocalRefEdgeTrimCount,
                etld.LocalRefSigma, etld.LocalRefEdgeThreshold,
                etld.LocalRefEdgeDirection, etld.LocalRefEdgePolarity,
                out r1, out c1, out r2, out c2, out err);
            etld.LastLocalRefFound = ok;
            if (ok) { etld.LastLocalRefRow1 = r1; etld.LastLocalRefCol1 = c1; etld.LastLocalRefRow2 = r2; etld.LastLocalRefCol2 = c2; }
            else { Logging.PrintLog((int)ELogType.Error, "[LocalRef] 기준 ROI 실패(Datum 이미지) — " + (etld.MeasurementName ?? "") + ": " + (err ?? "")); }
        }
    }
}
```
**왜 `ShotParam.FAIList` 만으로 SIDE/TOP/BOTTOM 전부 충분한가:** BOTTOM 은 `Bottom_Datum` 하나를 19개 측정이 **서로 다른 여러 Shot**(SHOT_B1-4, SHOT_E1-4, SHOT_E6_P1 등)에서 참조한다(레시피 실측). `Bottom_Datum.SourceShotName` 이 비어 있어 `IsDatumOwnedByCurrentShot` 의 폴백 규칙("SourceShotName 미해결 → 항상 소유")에 따라 **이 Datum 을 참조하는 모든 Shot 이 각자 자기 tick 에서 독립적으로 재검출**한다(기존 동작, 변경 없음). 따라서 `ComputeLocalRefLines` 도 각 Shot 이 자기 tick 에서 실행될 때 **그 Shot 소유 측정만** 처리하면 되고, 다른 Shot 의 측정은 그 Shot 자신의 tick 에서 알아서 처리된다 — 전역 레시피 순회가 필요 없다.

### Pattern 2: axis 소비 — 사전계산 값 우선, 없으면 전역 그대로

**What:** `TryExecute` 안의 기존 axis 계산(인라인 `if (measureX) ... else ...` 블록)은 그대로 두고, 그 앞에서 사전계산 값이 있으면 `DatumOriginRow/Col`+`DatumAngleRad` 자리에 국부 값을 대입한다(O-79-02 (b) 채택 — 위치만 교체, 각도는 전역 유지).
```csharp
double axisOriginRow = DatumOriginRow, axisOriginCol = DatumOriginCol, axisAngle = DatumAngleRad;
if (IsLocalRefEnabled && LastLocalRefFound) {
    axisOriginRow = (LastLocalRefRow1 + LastLocalRefRow2) / 2.0;
    axisOriginCol = (LastLocalRefCol1 + LastLocalRefCol2) / 2.0;
    // axisAngle 은 DatumAngleRad 유지 (O-79-02 (b) — 아래 실측 근거)
    LastRefSource = "Local";
} else {
    LastRefSource = IsLocalRefEnabled ? "GlobalFallback" : null;
    if (IsLocalRefEnabled) {
        Logging.PrintLog((int)ELogType.Error, "[LocalRef] 사전계산 값 없음 → 전역 기준 전환 — " + (MeasurementName ?? ""));
    }
}
// 이후 기존 axisR1/C1/R2/C2 계산 블록은 DatumOriginRow/Col 대신 axisOriginRow/Col, DatumAngleRad 대신 axisAngle 사용
```
**옵션 (a, 자체 기울기) 필요 시:** `axisAngle` 을 `Math.Atan2(LastLocalRefRow2-LastLocalRefRow1, LastLocalRefCol2-LastLocalRefCol1)` 로 계산해 대입 — 단, 아래 실측 근거상 **권장하지 않음.**

### Pattern 3: ROI 티칭 배선 — 이전 리서치와 동일(변경 없음)

`MainView.xaml.cs` 의 `BuildPointRoiDefinitions`/`ApplyPointRoiMoveDelta`/`TryGetPointRoiCenter`/`ApplyPointRoiResize` 4함수에 `EdgeToLineDistanceMeasurement` 의 `LocalRef_*` 를 `DualImageEdgeDistanceMeasurement` 의 `"Point"/"Line"` subKey 패턴과 동일하게 추가한다(이전 리서치 버전의 Pattern 3, 코드 그대로 유효 — z1/측정 이미지 어느 쪽에 그려도 좌표는 같은 카메라 프레임이므로 무관).

**O-79-09 — 어느 이미지 위에서 그리나:** 같은 카메라이므로 **어느 이미지를 캔버스에 띄운 상태든 Row/Col 좌표를 그리는 행위 자체는 동일하다.** 사용자가 띠 위치를 z1 이미지에서 미리 확인하고 싶다면, **신규 UI 없이** 트리에서 해당 Datum 노드를 먼저 선택하면 `DatumConfig.TeachingImagePath`(가로축 z1 이미지, 이미 `MainView.DisplayShotImage`류 기존 로직이 캔버스에 표시함, `MainView.xaml.cs:246-254`)가 뜬다 — 좌표를 확인한 뒤 측정 노드로 돌아와 `LocalRef_*` ROI 를 그 좌표에 배치하면 된다. **이것이 최소-UI 옵션이다** — 이미지 전환 버튼, 오버레이 동기화 등 새 로직 불필요.

**`datumTransform` 이 어느 이미지 기준인가(O-79-09):** `datumTransform`(= `_datumTransforms[datumKey]`)은 **그 Datum 을 검출한 바로 그 이미지**(z1/가로축)를 분석해서 나온 "티칭시 기준위치 → 이번 사이클 실제위치" 보정값이다. 같은 카메라·같은 사이클이면 이 보정은 z1 이미지에도, 측정 이미지(zK)에도 **동일하게 유효**하다(핀 ROI 가 이미 매 tick 이 값을 그대로 재사용 중 — 새로운 가정이 아니다). 따라서 `ComputeLocalRefLines` 가 `parentSeq.TryGetDatumTransform` 으로 얻은 값을 그대로 `LocalRef_*` ROI 에 적용하는 것이 정확하다.

### Pattern 4: 하위호환 — INI 리플렉션 override + Open Question 2 최종 해소

`WPF_Example/Utility/Ini.cs` 를 직접 열람해 확정:
- `IniValue.ToBool()`(:153, 무인자 오버로드 `valueIfInvalid=false`) → 키 없으면 **false**.
- `IniValue.ToDouble()`(:276, 무인자 `valueIfInvalid=0`) → 키 없으면 **0**.
- `IniValue.ToString()`(:317, **override, 인자 없음**) → `return Value;` — 키 없으면(`Value==null`) **null**(빈 문자열이 아님!). `ParamBase.Load` 의 `case "String": string sValue = loadFile[group][name].ToString(); prop.SetValue(this, sValue);`(`ParamBase.cs:386-387`)가 바로 이 무인자 오버로드를 호출하므로, **문자열 프로퍼티는 키가 없으면 선언 기본값이 아니라 `null` 로 클로버된다.**

**영향:** `LocalRefEdgePolarity`(기본 "DarkToLight")/`LocalRefEdgeDirection`(기본 "BtoT") 는 옛 레시피(키 없음)를 로드하면 `null` 이 된다. `VisionAlgorithmService.TryFitLine` 은 `string.Equals(direction, "TtoB", ...)` 류 비교라 `null` 이 들어와도 **예외는 없다**(전부 `false`로 평가되어 최종 `else` 폴백 — direction→LtoR 취급, polarity→"positive"(DarkToLight) 취급, `VisionAlgorithmService.cs:79-98`). 즉 **크래시는 없지만 선언 기본값과 다른 폴백**이 조용히 적용된다 — `LocalRefEdgeDirection` 선언 기본 "BtoT" 인데 null 이면 LtoR 취급되어 스캔 축이 달라진다(수평 에지 검출 실패 가능성). **따라서 override 필요.**

```csharp
// EdgeToLineDistanceMeasurement 에 추가 (MeasurementBase.Load 의 MeasCorrectionFactor override 와 동일 패턴)
public override bool Load(IniFile loadFile, string groupName) {
    bool result = base.Load(loadFile, groupName);
    IniSection sec;
    bool bHasSection = loadFile.TryGetSection(groupName, out sec) && sec != null;
    if (!bHasSection || !sec.ContainsKey("LocalRefEdgeThreshold")) { LocalRefEdgeThreshold = 10; }
    if (!bHasSection || !sec.ContainsKey("LocalRefSigma")) { LocalRefSigma = 1.0; }
    if (!bHasSection || !sec.ContainsKey("LocalRefEdgeSampleCount")) { LocalRefEdgeSampleCount = 20; }
    if (!bHasSection || !sec.ContainsKey("LocalRefEdgeTrimCount")) { LocalRefEdgeTrimCount = 10; }
    if (!bHasSection || !sec.ContainsKey("LocalRefEdgePolarity")) { LocalRefEdgePolarity = "DarkToLight"; } // Open Question 2 해소 — override 필요로 정정
    if (!bHasSection || !sec.ContainsKey("LocalRefEdgeDirection")) { LocalRefEdgeDirection = "BtoT"; }        // 위와 동일 이유
    return result;
}
```
`IsLocalRefEnabled`(bool, 기본 false)와 `LocalRef_Row/Col/Phi/Length1/Length2`(double, 기본 0)는 override 불필요(클로버되어도 false/0 = 옵션 꺼짐과 동일 의미, 이전 리서치 버전과 동일 결론).

## Don't Hand-Roll

이전 리서치 버전과 동일(변경 없음) — `TryFitLine` 재사용, `ProjectionPl`+부호 정규화 재사용, `FormatSelectedZ` 류 포맷터 대신 짧은 문자열 상수, CSV `COLUMN_COUNT` 고정 패턴, ROI 다중 티칭 배선(`DualImageEdgeDistanceMeasurement` 전례). 추가된 항목:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| "이 Datum 을 이 Shot 이 소유하는가" 판정 | 새 소유권 판정 로직 | `IsDatumOwnedByCurrentShot`(`Action_FAIMeasurement.cs:347-361`)이 이미 SourceShotName 미해결 시 폴백 규칙까지 구현해 둠 — `ComputeLocalRefLines` 는 이 판정을 다시 안 하고 `ShotParam.FAIList` 만 순회(호출 자체가 이미 "소유 확정 후" 지점) | 중복 판정은 버그 유입 지점만 늘린다 |

## Runtime State Inventory

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| 저장된 데이터(INI) | `LocalRef*` 키 전무 — `ParamBase.Load` 리플렉션이 없는 키를 0/false/**null**(string!)로 채움 [VERIFIED: Ini.cs 직접 확인] | Pattern 4 override(문자열 2개 포함, Open Q2 반영해 확대) |
| 저장된 데이터(cycle.json) | `RefSource` 필드 없음(신규) — Newtonsoft null 폴백 | 코드 추가만 |
| 저장된 데이터(CSV) | 신규 "사용기준" 열 — `COLUMN_COUNT`=14 유지, 옵션 인덱스로만 읽기 | 이전 리서치 버전과 동일 |
| 라이브 서비스 설정 | 없음 | 해당 없음 |
| **이미지 메모리(신규 검토)** | `ComputeLocalRefLines` 는 기존 `img`/`imgH` 참조를 재사용할 뿐 **새 HImage 를 생성/복제하지 않는다**(TryFitLine 은 원본 이미지를 읽기만 함) | 추가 조치 불필요 — 08-06 급 메모리 인시던트 재현 경로 아님 |
| OS 등록 상태/시크릿 | 없음 | 해당 없음 |
| 빌드 산출물 | 신규 .cs 파일 없음 | 없음 |

## Common Pitfalls

### Pitfall 1 (개정): 기준 ROI "어느 창"인지가 성패를 가른다 — O-79-10 실측
**What goes wrong:** 핀 옆(왼쪽 또는 오른쪽)에 기준 ROI 를 둔다는 방향만 맞고 정확한 위치(핀에서 얼마나 떨어진 창)가 틀리면, 편차를 줄이기는커녕 오히려 늘릴 수 있다.
**증거(EdgeProbe 실측, z1 이미지, A 4사이클·B 3사이클, 17:38~17:46, pixelResolution=0.0023696mm/px):**

| 포인트 | 후보 창(col 범위) | pin−strip 거리 A평균(px) | B평균(px) | A−B(µm) | 평가 |
|---|---|---|---|---|---|
| P1(왼쪽) | stripL 1400–1580 (핀에 가까움) | 927.94 | 929.09 | **-2.7** | 우수 (09-17 "+2.8µm" 스케일과 일치) |
| P1(왼쪽) | stripL2 1760–1940 (조금 더 멂) | 1021.49 | 996.17 | **+60.0** | **훨씬 나쁨** — 다른 에지를 잡고 있을 가능성(두 창의 posRow 가 94px 차이) |
| P3(오른쪽) | stripR 12600–12780 (핀에 가까움) | 912.63 | 911.76 | **+2.1** | 양호 (09-17 "+4.0µm" 스케일과 일치) |
| P3(오른쪽) | stripR2 12950–13130 (조금 더 멂) | 912.95 | 917.09 | **-9.8** | stripR 보다 나쁨(치명적이진 않음) |
| P2 | stripM/stripM2 6900–7080/7480–7660 | 에지 세기 양호(9.2~9.6), **핀 z1 위치는 이 세션 프로브가 못 찾음(진폭 0.05~0.12, 노이즈 수준)** | | | **미확정 — Wave 0 재확인 필요**(강 자체는 깨끗함) |

**Why it happens:** 09-17 CONTEXT.md 의 "94px 차이" 경고(P1 왼쪽 두 후보가 다른 행을 가리킴)를 무시하고 "핀 옆 아무 창"으로 가르치면 이 발생 가능성이 실측으로 확인된 그대로 재현된다.
**How to avoid:** (1) 계획에서 LocalRef ROI 기본 위치를 **핀에 가장 가까운 창**으로 명시, (2) UAT 절차서에 "왼쪽/오른쪽 후보 창 각각의 TryFitLine 결과(위치·강도)를 비교해 더 강하고 일관된 쪽을 채택" 단계 추가, (3) 계획 단계에서 P2 는 z1 이미지에서 핀 위치를 다시 정확히 특정하는 소작업이 필요함을 Wave 0 에 명시.
**Warning signs:** LSR-06 검증에서 옵션 ON 이 옵션 OFF 보다 A-B 갭을 더 키움.

### Pitfall 2: 사전계산 시점과 소비 시점의 필드 리셋 순서
**What goes wrong:** `LastLocalRefFound`/`LastLocalRefRow1` 등을 `ClearResult()`(사이클/tick 시작)에서만 리셋하고 `ComputeLocalRefLines` 가 사이클 중 **한 번도 안 불리면**(예: 이 Datum 이 이번엔 검출 실패) 이전 사이클 값이 남아 있을 위험 — 단, `ClearResult()` 가 이미 매 tick 초입에 호출되는 기존 패턴(Phase 77 `LastFitScore` 리셋과 동일 지점)이라면 문제 없다.
**Why it happens:** 사전계산이 "다른 phase(DatumPhase)"에서 일어나므로, "언제 리셋되고 언제 다시 채워지는지"의 시간 순서가 기존 필드(같은 phase 내에서 채워지는 `LastFitScore`)보다 한 단계 더 김.
**How to avoid:** `ClearResult()` 리셋 시점이 DatumPhase 보다 먼저(즉 매 사이클/tick 의 맨 처음)임을 반드시 확인 후, 그 지점에 `LastLocalRefFound=false; LastLocalRefRow1=0; ...` 추가.
**Warning signs:** Datum 검출이 간헐적으로 실패하는 사이클에서 직전 사이클의 국부 기준값이 그대로 재사용됨(그런데 이러면 오히려 "이상하게 검사가 성공"하는 조용한 오류라 발견이 어려움 — 계획 단계에서 반드시 테스트 케이스로 명시).

### Pitfall 3: `IsLocalRefEnabled` 가드를 `ComputeLocalRefLines` 안 깊숙이 두는 것
**What goes wrong:** 가드가 없으면 옵션 꺼진 측정도 매 Datum 검출마다 불필요한 `TryFitLine` 호출(미티칭 ROI 로 매번 "insufficient edge points" 에러 로그 스팸) 발생.
**How to avoid:** `foreach` 루프 맨 앞에서 `if (!etld.IsLocalRefEnabled) { continue; }` 가드(위 Pattern 1 코드에 이미 반영).

### Pitfall 4: CSV `COLUMN_COUNT` 상향 — 이전 리서치 버전과 동일, 유지

`MeasurementHistoryCsvLoader.COLUMN_COUNT`(14)를 올리면 옛 파일이 손상 행으로 걸러진다. 신규 열은 `COL_REF_SOURCE`(16, `COL_SELECTED_Z`=15 다음)로만 조건부 읽기.

## Code Examples

이전 리서치 버전의 "CSV 헤더/파싱 확장", "오버레이 색상 분기 추가"(`FAI-RefLine`, 주황) 코드는 변경 없이 유효 — 그대로 재사용.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| EdgeToLineDistance 는 항상 전역 2점 datum 기준선까지 거리 | 옵션 ON 시 그 Datum 의 가로선 검출 이미지(z1 류)에서 사전계산한 국부 기준선까지 거리, 실패 시 전역 폴백 | Phase 79 (D-79-01~09, 2026-09-18 개정) | 자재 간 편차 감소 기대. z1 이미지 채택으로 O-79-01 BLOCKER 해소. SIDE·TOP·BOTTOM 전체 적용 가능(레시피 티칭만) |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | O-79-02 (b, 위치만 로컬+전역 각도)가 (a) 보다 안정적 | Pattern 2 | 실측(A-B 갭 축소)은 위치 교체만으로 확인됨 — 각도까지 국부화했을 때 더 나아지는지는 미검증. 상수 토글로 대비 권장(이전 버전과 동일 권고 유지) |
| A2 (해소) | ~~LocalRef 전용 Edge 설정이 블러 문제를 완화할 것~~ | — | z1 채택으로 블러 문제 자체가 사라져 이 가정은 더 이상 필요 없음 |
| A3 (해소) | ~~문자열 필드 override 불필요~~ | Pattern 4 | **정정됨** — `IniValue.ToString()` 이 null 을 반환함을 확인, override 필요로 결론 변경 |
| A4 (해소) | ~~09-17 근거 수치가 자동선택 Z 기준인지 불명확~~ | — | 사용자 확인: z1 기준점 가로 사진에서 잰 값이었음(CONTEXT.md 2026-09-18 개정 명시) |
| A5 (신규) | P2 의 z1 이미지 내 핀 위치는 이 세션 프로브가 정확히 못 찾았다(진폭 노이즈 수준) — 실제로는 위치만 다시 잡으면 P1/P3 와 유사하게 잘 잡힐 것 | Pitfall 1 표 | Wave 0 재확인 없이 계획을 확정하면 P2 국부 기준의 실제 효과를 모른 채 진행하게 됨 |
| A6 (신규) | BOTTOM 의 19개 측정이 흩어진 여러 Shot 각각에서 `Bottom_Datum` 이 독립 재검출되는 기존 동작이 그대로 유지된다는 전제 하에 `ComputeLocalRefLines` 설계가 안전하다 | Pattern 1 | `SourceShotName` 상속/폴백 로직이 이 리서치의 이해와 다르게 동작하면(예: 캐시 재사용 tick 이 있어 재검출을 건너뛰는 경우) 그 tick 에서는 국부 기준선이 계산되지 않고 전역 폴백만 계속 탈 수 있음 — 기능상 안전(D-79-06)하지만 "왜 항상 전환됨" 으로 보일 수 있어 계획 단계에서 재확인 권장 |

## Open Questions

1. **(해소, 참고용 유지) 09-17 근거 데이터 출처** — z1 기준점 가로 사진에서 측정한 값이었음이 사용자 확인으로 해소됨.
2. **(해소) `IniValue.ToString()` 기본값** — null 반환 확인 완료, Pattern 4 override 에 반영.
3. **P2 z1 이미지 핀 위치 재프로브 필요.** 현재 `EdgeProbe` 데이터의 `pinP2` ROI(col 7250–7330)는 유효 에지를 못 찾았다(진폭 0.05~0.12). Wave 0 에서 실제 레시피 좌표(C13_P2 Point_Col≈7395, C14_P2≈7408) 주변을 다시 스캔해 P2 의 국부 기준 효과를 확인해야 한다.
4. **각도(옵션 a) 실측 비교 미실시.** `EdgeProbe` 는 행-프로파일 평균으로 위치만 구하는 단순 도구라 실제 `TryFitLine`(strip-loop+tukey) 기반 각도 비교는 하지 못했다. (b) 채택을 뒤집을 만한 이득이 있는지는 실제 HALCON 피팅 프로브(스크래치패드 `79-probe`)로 추가 확인 가능하나 이번 세션 범위에서는 보류.
5. **TOP/BOTTOM 실측 효과는 미검증(D-79-09 범위상 "동작·폴백 확인까지"로 충분).** TOP 의 `SHOT_A1-23-C1-C12` 59개, BOTTOM 9개 Shot 의 19개 측정 중 어느 것이 실제로 옵션 켰을 때 이득을 보는지는 이번 phase 범위 밖(F9·확대는 별도 phase, D-79-09).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | 없음(기존 정책) |
| Quick run command | `"/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -m -nologo -v:minimal -clp:ErrorsOnly` [VERIFIED: Phase 77/78 다수 실사용] |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| LSR-01 | 신규 필드 존재 | 정적 grep | 필드명 grep | ✅ 기존 파일 확장 |
| LSR-02 | 국부 기준 계산이 Datum 이미지(z1)에서 1번만 일어남 | 로그 확인 | `[LocalRef]` 로그 발생 횟수(Datum 검출 성공 tick 수와 일치해야 함, Z 후보 수와 무관) | ❌ 신규 |
| LSR-03 | 자동 전환 | 오프라인 재검사 + 로그 | 기준 ROI 미티칭 상태로 재검사 → `[LocalRef] 사전계산 값 없음 → 전역 기준 전환` 확인 | ❌ 신규 |
| LSR-04 | 표시·기록 | 시각+grep | 리뷰어 "국부/전역/전환" 표시, cycle.json `RefSource` grep | ❌ 신규 |
| LSR-05 | 회귀 0 | 정적 grep + bit-비교 | 옵션 OFF 측정(96개 EdgeToLineDistance 중 옵션 미설정 전부) 재검사 값 diff 0 | ❌ 신규 |
| LSR-06 | 자재 A·B 검증 | 오프라인 재검사 + 수치 비교 | Wave 0 P2 재프로브 후 C13·C14 6점 전체 A-B 갭 축소 확인 | ❌ 신규 |

### Sampling Rate — 이전 리서치 버전과 동일

### Wave 0 Gaps
- [ ] P2 z1 이미지 핀 위치 재프로브(Open Question 3)
- [ ] `ComputeLocalRefLines` 를 실제 Debug 빌드로 넣고 SIDE C13/C14 6점 + TOP/BOTTOM 각 1~2개 측정으로 스모크(옵션 ON 시 `LastRefSource=="Local"` 확인, OFF 시 완전 무변경 확인)
- [ ] Debug|x64 최신 빌드 경고 baseline 재측정

## Sources

### Primary (HIGH confidence — 코드/실측 직접 확인)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:330-505` — `ProcessDatumDualImage`/`ProcessDatumSingleImage`/`RunDatumDualImageDetection`/`RunDatumSingleImageDetection`/`IsDatumOwnedByCurrentShot`(이미지 수명·소유권 판정의 결정적 근거)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:3404-3626` — `TryComposeAlign`/`TryRunSingleDatum`/`TryGetDatumTransform`
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs:90-272` — `CircleTwoHorizontal`/`VerticalTwoHorizontalDualImage` 알고리즘이 가로선을 검출하는 지점
- `WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs` — 알고리즘 enum 전체
- `WPF_Example/Utility/Ini.cs:153-326` — `IniValue.ToBool/ToDouble/ToString` 기본값 확정(Open Question 2 해소)
- `D:\Data\Recipe\FAI_1\main.ini` — Datum 섹션(`FIXTURE_*_DATUM_0`, AlgorithmType 전수), TOP/BOTTOM/SIDE EdgeToLineDistance 측정 전수 스캔(TOP 59·BOTTOM 19·SIDE_1/2 각 9)
- 이전 세션 스크래치패드(읽기 전용) `C:\Users\admin\AppData\Local\Temp\claude\...\a4dcaa7e-.../scratchpad\{jobs78,jobs78b,jobs78c,jobs78d,out78,out78b,out78c,out78d}.tsv`, `edgeprobe\EdgeProbe.cs` — z1 이미지 stripL/stripL2/stripR/stripR2/stripM/stripM2/pinP1/pinP3/pinP2 실측(A 4사이클·B 3사이클, 17:38~17:46, 17:31 재티칭 이후)

### Secondary (MEDIUM confidence)
- 이 세션에서 위 tsv 원자료로부터 계산한 A-B 갭(px→µm 환산, pixelResolution=0.0023696mm/px) — 계산 자체는 재현 가능하나 `EdgeProbe` 알고리즘(행-프로파일 평균+2차미분 피크, HALCON `measure_pos`/`FitLineContourXld` 아님)의 정밀도 한계 있음

### Tertiary (LOW confidence)
- 없음(이전 버전의 육안-크롭 판단은 z1 설계 채택으로 더 이상 이 리서치의 핵심 근거가 아님 — 참고용으로만 이전 버전 기록 유지 안 함, 개정 이력 절 참고)

## Metadata

**Confidence breakdown:**
- Datum 이미지 가용성/사전계산 설계: HIGH — 코드 직접 대조, 수명·스레드 확정
- O-79-02/O-79-10 수치: MEDIUM — 실측 있음, 표본 적고 도구가 단순(HALCON 서브픽셀 아님)
- TOP/BOTTOM 적용성: HIGH(구조) / 미검증(효과) — 레시피 스캔으로 구조 확정, 효과는 D-79-09 범위상 이번 phase 대상 아님

**Research date:** 2026-09-18 (개정)
**Valid until:** 계획·실행 착수 시까지. 09-17 이후 SIDE Datum 재티칭이 있었다면 z1 이미지 내 좌표(stripL/stripR 등) 재확인 필요.
