# Phase 73 — SIDE 시퀀스를 지그 4개(SIDE_1~4)로 분리

**신설:** 2026-08-26
**상태:** seed (discuss 대기)
**Depends on:** Phase 72

---

## Goal

SIDE 를 단일 시퀀스(z=0~15 한 바퀴에 지그 4개)에서 **지그별 독립 시퀀스 4개**로 분리한다.
Top/Bottom 과 동일하게 각 시퀀스의 z 가 0부터 시작하고, `$TEST` 의 Type 2~5 가 SIDE_1~4 로 라우팅된다.
이로써 **지그마다 개별 P/F 판정**이 나가 제어가 지그별 배출을 판단할 수 있게 된다.

---

## 왜 필요한가

### 제어팀 확정 스펙 (2026-08-13, 김민욱선임)

```
site : PC 번호 (PC1=1, PC2=2)
Type : 검사 대상 (TOP=0 / BOTTOM=1 / SIDE_1~SIDE_4 = 2,3,4,5)
자재번호 : 자재 식별 번호 (정수, 추적용)
※ z_index 없음 — 직전 PREP 가 보유
```

### 현재 구현과의 괴리

`ResourceMap.TryResolveSlotByType` 은 Type 2/4/5 를 전부 Top 슬롯으로, 1/3 을 Bottom 슬롯으로
폴백시킬 뿐이다. **SIDE 검사에서 Type 값은 실질적으로 무시**되고, 실행 대상은 오직 z_index 가 정한다.

현재 레시피 z 배치 (`D:\Data\Recipe\FAI_1\main.ini` 실측):

| 지그(Datum) | 촬영 z (ZIndexA/B) | 측정 Shot (z) |
|---|---|---|
| Side_Datum_3-1 | 0, 1 | SIDE_SHOT_3-1_D1 (2) |
| Side_Datum_3-2 | 3, 4 | SIDE_SHOT_3_2_D1 (5), (6) |
| Side_Datum_4-2 | 7, 8 | 4-2_H5 (9), 4-2_C13-14_P1 (10), 4-2_F9 (11) |
| Side_Datum_4-1 | 12, 13 | 4-1_F9 (14), 4-1_C13-14 (15) |

즉 **z=0~15 한 바퀴 = 지그 4개 전부**. 사용자 확인: Datum 하나 = 지그 하나.

### 이 구조의 실제 문제

1. **어느 지그가 불량인지 제어가 알 수 없다.** 최종 P/F 가 z=15 에서 한 번만 나오므로,
   지그 4개 중 하나만 NG 여도 통째로 F 가 나가 지그별 배출 판단이 불가능하다.
2. **택트 손해.** 1번 지그가 NG 여도 z=15 까지 11초를 다 쓴 뒤에야 결과가 나온다.
3. **스펙과 구현 불일치.** "z 로 다 구분할 거면 Type 이 왜 있나" — 사용자 지적, 타당함으로 확인.

---

## 목표 구조 (사용자 확정)

Top/Bottom 과 동일하게 시퀀스별 z 가 0부터 독립 시작:

```
Top     z=0~3
Bottom  z=0~36
Side 1  z=0~4
Side 2  z=0~7
Side 3  z=0~10
Side 4  z=0~11
```

Type 라우팅: `Type 2→SIDE_1 / 3→SIDE_2 / 4→SIDE_3 / 5→SIDE_4`

**핵심 이점 — 기존 엔진을 그대로 재사용한다.**
`InspectionSequence` 의 두 로직이 시퀀스 단위로 이미 동작하므로 분리만 하면 자동 적용된다:
- `DATUM_Z_INDEX`(z=0) = 새 사이클 시작 → 지그마다 독립 사이클
- `ComputeLastZIndex()` 가 `shot.OwnerSequenceName == Name` 으로 자기 것만 집계
  → 각 시퀀스의 max z 가 그 지그의 최종 P/F 시점

---

## 변경 범위 (2026-08-26 grep 실측)

`SEQ_SIDE` / `ESequence.Side` 참조 **16곳 / 5개 파일**.

| 파일 | 변경 |
|---|---|
| `Custom/Define/ID.cs` | `enum ESequence { Top=1, Side=2, Bottom=3 }` → Side 4종 |
| `Custom/Sequence/SequenceHandler.cs` | `SEQ_SIDE` 1개→4개, `IsSequenceActive`, `RegisterSequences`, `RegisterActions`, `InitializeSequences`, `CanRunSequence`(상호배타) |
| `Custom/TcpServer/ResourceMap.cs` | `TryResolveSlotByType` Type 2~5 → SIDE_1~4 라우팅, `TYPE_CODE_*` 주석 갱신 |
| `Custom/Sequence/Inspection/InspectionRecipeManager.cs` | `ESequence.Side` 참조 |
| `Custom/Sequence/Inspection/ShotConfig.cs` | `ESequence.Side` 참조 |
| `UI/ContentItem/MainView.xaml.cs` | `ESequence.Side` 참조 (트리/카메라 선택) |

4개 시퀀스 모두 `DeviceHandler.CAMERA_SIDE` + `LightHandler.LIGHT_BAR` 공유.
물리 카메라 공유는 Top/Bottom 선례가 있다(`DeviceHandler` sharedMil 경로).

### 레시피 재배정 — 스크립트로 가능

`OwnerSequenceName` 이 INI 에 그대로 저장돼 있음을 확인(35건, SIDE 4건은 4944/5071/5237/5442행).
따라서 파일 편집 스크립트로 처리 가능하다:
- Shot 8개: `OwnerSequenceName=SIDE` → `SIDE_1`..`SIDE_4`
- Datum 4개: 소속 재배정 (`DatumConfig.OwnerName` 결정 경로 확인 필요)
- `ZIndex` / `ZIndexA` / `ZIndexB` 를 시퀀스별 0-베이스로 재계산

---

## 위험 요소 (plan 에 반드시 반영)

1. **Datum 데이터 손실 이력** — `InspectionRecipeManager.cs:88`:
   > 시퀀스 미등록(타 CameraRole) — DatumCount=0 으로 덮어쓰지 말고 기존 레시피의 Datum 을 보존

   과거 CameraRole 전환 후 저장 시 비활성 시퀀스 Datum 이 소실된 사고(커밋 `3faa91b`)가 있었다.
   **시퀀스 구성 변경이 정확히 그 지뢰밭.** SIDE Datum 4개는 현재 유일본이다.
   → **백업 완료:** `D:\Backup\FAI_1_backup_before_phase73_260826\` (.shm 31개 포함, 2026-08-26)

2. **크로스-Z 재매김 회귀** — 2026-08-26 커밋 `8d6982c`(SIMUL role B 세로 이미지 결함) 수정 직후라
   기준선이 막 잡힌 상태다. ZIndexA/B 재매김이 이걸 깨뜨리지 않는지 확인 필수.

3. **`TeachingStorageService.cs:229`** — `text.Contains("SIDE")` → `"SIDE"` 반환.
   SIDE_1~4 가 모두 "SIDE" 를 포함하므로 4개 시퀀스의 `.shm` 이 같은 폴더로 수렴해 **덮어쓰기 위험.**

---

## 검증 기준 (baseline 확보 완료)

**2026-08-26 09:17 SIDE_3 자동검사 실측**이 비교 기준이다.

- z=0~15 완주, 측정 **25개 전부 실행**, 공차이탈 **합계 7개**

| Shot | 측정 | 이탈 |
|---|---|---|
| 3-1_D1 | 2 | 2 |
| 3_2_D1 | 2 | 0 |
| 3_2_D1 | 2 | 0 |
| 4-2_H5 | 1 | 0 |
| 4-2_C13-14_P1 | 6 | 3 |
| 4-2_F9 | 3 | 0 |
| 4-1_F9 | 3 | 0 |
| 4-1_C13-14 | 6 | 2 |

분리 후에도 **같은 측정값·같은 이탈 개수**가 나와야 한다(지그별로 쪼개져 나올 뿐).
추가로 **지그별 P/F 가 각 시퀀스의 마지막 z 에서 개별적으로** 나오는지 확인.

---

## 미결 사항 (discuss 에서 확정)

- Side1~4 각각의 실제 z 상한 (사용자 제시 0~4/0~7/0~10/0~11 vs 현 레시피 최소 0~2/0~3/0~4/0~3)
- Datum 의 시퀀스 이동 수단 (`OwnerName` 은 `AddDatum()` 생성 경로로만 결정 — `MainView.xaml.cs:4160`)
- 제어팀에 "Side 검사 = 지그별 개별 $TEST" 확정 통보 여부 (현재는 우리 쪽 설계 결정 상태)
- 테스트 클라이언트(`C:\Info\Project\CommunicationTest`) 대응 — 현재 "전체" 모드가
  Type 2~5 로 각각 z=0~15 를 돌려 **같은 검사를 4번 반복**(44초). 분리 후엔 Type 별 z 범위가 달라진다.

---

## 코딩 규칙 (이번 phase 강조 — 사용자 명시)

### 필수

- **삼항 연산자 `?:` 전면 금지** → 반드시 `if / else`
- **이항 축약 금지** — `??`, `?.` 를 이용한 축약 대입 대신 명시적 분기
- **분기는 `if / else` 또는 `switch` 만 쓴다** (사용자 명시). 값이 3개 이상으로 갈리면
  `switch` 가 오히려 읽기 쉽다 — Type 2~5 → SIDE_1~4 같은 매핑은 `switch` 를 권장한다.
- **초보자가 봐도 이해되는 코드** — 한 줄에 두 가지 일을 하지 않는다.
  조건이 길면 이름 있는 `bool` 변수로 먼저 뽑고, 그 변수를 `if` 에 쓴다.

  ```csharp
  // 좋음 — 조건에 이름을 붙여 의미가 드러난다
  bool bIsSideSequence = seqId == ESequence.Side1 || seqId == ESequence.Side2;
  if (bIsSideSequence) { ... }
  else { ... }

  // 좋음 — 여러 갈래는 switch 로 평평하게
  switch (nTypeCode) {
      case TYPE_CODE_SIDE1: szSeqName = SEQ_SIDE_1; break;
      case TYPE_CODE_SIDE2: szSeqName = SEQ_SIDE_2; break;
      case TYPE_CODE_SIDE3: szSeqName = SEQ_SIDE_3; break;
      case TYPE_CODE_SIDE4: szSeqName = SEQ_SIDE_4; break;
      default:              szSeqName = SEQ_TOP;    break;
  }

  // 금지
  return b ? A : B;                    // 삼항
  var name = x ?? "SIDE";              // ?? 축약
  int n = obj?.Value ?? 0;             // ?. + ?? 조합
  if (a && (b || c) && !d) { ... }     // 조건이 길면 bool 변수로 먼저 뽑을 것
  ```

  ※ `switch` 는 전통 문법(`case ... : break;`)만 쓴다. C# 8.0 switch expression(`=>`)은 금지.

### UI 작업은 MVVM 패턴 (사용자 명시)

이 phase 의 UI 변경 범위는 작다 — 실측 3곳뿐:

| 위치 | 내용 |
|---|---|
| `UI/ContentItem/MainView.xaml.cs:4149` | `ESequence[] roles = { Top, Side, Bottom }` → Side 4종 반영 |
| `InspectionRecipeManager.cs:193` | `SaveFixtureForSequence(..., ESequence.Side, "FIXTURE_SIDE", ...)` |
| `InspectionRecipeManager.cs:272` | `LoadFixtureForSequence(..., ESequence.Side, "FIXTURE_SIDE")` |

**규칙:**
- 새로 붙는 UI 로직은 **code-behind 가 아니라 ViewModel** 에 둔다.
  기존 ViewModel 이 있으면 거기에 얹고(`UI/ViewModel/`), 없으면 새로 만든다.
- `MainView.xaml.cs` 는 이미 4,300줄이 넘는 이 코드베이스 최대 문제 파일이다.
  **여기에 새 로직을 추가하지 않는다.** 불가피하면 그 이유를 plan 에 남긴다.
- View 는 바인딩만 하고 판단하지 않는다. 상태/분기는 ViewModel 로 옮긴다.
- 다만 **이번 phase 는 리팩토링이 목적이 아니다.** 기존 code-behind 를 MVVM 으로
  전면 전환하는 작업은 범위 밖(그건 QUAL-01 계열). 위 3곳처럼 **이번에 손대는 자리**에만
  적용하고, 손대지 않는 기존 코드는 그대로 둔다.

  ※ 이 프로젝트의 MVVM 은 부분 적용 상태다(CLAUDE.md: "some views carry ViewModel classes,
    others use direct code-behind with INotifyPropertyChanged"). 새 코드가 그 비율을
    악화시키지 않는 것이 이 규칙의 목적이다.
- 헝가리언 접두사 유지 (bool=`b`, int=`n`, string=`sz`) — 해당 파일 기존 스타일 우선
- C# 7.2 문법만 (switch expression / nullable reference types 금지)
- 파일별 중괄호 스타일 유지 — 섞지 말 것
- 날짜 주석(`//YYMMDD hbk`) 규칙은 폐기됨 — 새로 달지 않는다. **비자명한 "왜"만** 최소 주석

### 빌드/검증

- 빌드 경고 baseline: **CS0618×10 + CS0162×2 (12줄)**. "경고 0"을 통과 기준으로 쓰지 말 것
- **SIMUL_MODE ON/OFF 양쪽 빌드 검증** (조건부 컴파일 존재)
- 실행 중 프로세스 종료 금지 — 산출물 잠김 시 스크래치 OutDir 로 컴파일만 검증

---

## 품질 게이트 (사용자 명시)

### 에이전트 검토 — 세밀하게

plan 실행 후 `gsd-code-reviewer` 를 **반드시** 돌린다. 검토 시 특히 아래를 지정해 확인시킬 것:

1. 삼항/축약 연산자 잔존 0건 (신규 코드 전수)
2. `ESequence.Side` → 4종 전환에서 **누락된 참조**가 없는지 (16곳 전수 대조)
3. Datum 소실 위험 경로 — `InspectionRecipeManager` 의 DatumCount 보존 로직이
   시퀀스 4개 구성에서도 유효한지
4. `.shm` 경로 충돌 — `TeachingStorageService` 가 SIDE_1~4 를 구분하는지
5. 크로스-Z ZIndexA/B 재매김이 완성 index(`max(A,B)`) 계약을 깨지 않는지
6. `CanRunSequence` 상호배타 — SIDE 4개가 같은 카메라를 공유하므로 동시 실행이 막히는지

### 테스트 — 충분히

정적 검증만으로 끝내지 않는다. 최소 아래를 **실기(SIMUL) 실행**으로 확인한다:

| # | 테스트 | 통과 기준 |
|---|---|---|
| T1 | 회귀 — RUN 버튼 SIDE | 측정값이 baseline 과 동일 |
| T2 | Type 2 자동검사 | SIDE_1 만 실행, 그 지그의 P/F 개별 산출 |
| T3 | Type 3/4/5 각각 | 각 지그만 실행, 개별 P/F |
| T4 | 4 Type 연속 | 4지그 전부 검사, 측정 합계 25개 / 이탈 7개 (baseline 일치) |
| T5 | 반복 실행 (최소 5회) | `Fail to Start Sequence` 0건, 매번 완주 |
| T6 | 리뷰어 확인 | Overlay·측정결과·측정명 라벨 정상 |
| T7 | Top/Bottom 회귀 | SIDE 분리가 다른 시퀀스에 영향 없음 |

T4 가 핵심이다 — **분리 전후 측정값이 같아야** 구조 변경이 판정에 영향을 주지 않았음이 증명된다.

---

## 다음 단계

`/gsd-discuss-phase 73` — 위 "미결 사항" 4건을 확정한 뒤 plan 으로 진행.
