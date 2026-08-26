---
phase: 73-side-4-jig-split
plan: 05
subsystem: lighting-and-cycle-judgement
tags: [lighting, channel-scoping, prep-ack, cycle-judgement, side-jig-split, phase73]
requires:
  - "73-01 — SEQ_SIDE_1~4 상수 + SIDE_1~4 InspectionSequence 등록"
  - "73-03 — main.ini 마이그레이션(Owner=SIDE_1~4, FIXTURE_SIDE_1~4) + NormalizeModelFolderName"
  - "73-04 — ApplyPrepToSequence(단일 시퀀스 조명 적용) / $PREP_ACK FAIL 정의"
provides:
  - "CollectOwnedChannelScope() — 점등/소등이 공유하는 자기 소유 조명 채널 집합 단일 소스"
  - "CollectBusySiblingChannels() — 비-Idle 형제 시퀀스 채널을 소등 대상에서 제외(R1)"
  - "ApplyChannelLight/ApplyGroupLight(ownedScope, ...) bool — 스코프 밖 채널 미접촉 + 성공 여부 반환"
  - "ApplyShotLights(int) 반환 계약 = 조명 세팅 성공 여부($PREP_ACK OK/FAIL), Shot 부재는 OK"
  - "bIsBeyondRange — 자기 z 범위 밖 요청이 최종 P/F 를 내지 못하게 차단(M13)"
  - "WarnIfEnabledOutOfScope() — 스코핑이 만든 무음 실패 경로 가시화"
affects:
  - "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs"
  - "WPF_Example/UI/ControlItem/InspectionListView.xaml.cs (주석만)"
  - "73-07 (검증) — [W8] 스코프 밖 잔광 잔여 위험을 73-HUMAN-UAT.md K1 로 이월"
tech-stack:
  added: []
  patterns:
    - "symmetric scoping — 점등과 소등이 같은 수집 함수(CollectOwnShotChannels + CollectOwnDatumChannels)를 공유"
    - "AND-aggregate without early return — 13채널 전부 호출 후 && 집계(절대값 덮어쓰기 성질 보존)"
    - "named-bool extraction — bIsOwned / bAllOk / bHasMeasurementShots / bIsBeyondRange (삼항 0건)"
    - "fail-loud on newly-silent path — 스코핑이 새로 만든 무음 억제 경로를 Error 로그로 드러냄"
key-files:
  created: []
  modified:
    - "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs"
    - "WPF_Example/UI/ControlItem/InspectionListView.xaml.cs"
decisions:
  - "자기 채널 집합 '안'에서는 Enabled=false -> OFF 를 그대로 유지 — 바뀐 건 '남의 채널을 안 건드린다' 뿐이다. 이걸 놓치면 이전 사이클 조명이 남아 잔광 회귀가 된다"
  - "점등 스코프를 새로 계산하지 않고 소등 쪽 CollectOwnShotChannels/CollectOwnDatumChannels 를 재사용 — 기준이 갈리면 '켠 채널을 안 끄거나 안 켠 채널을 끄는' 비대칭이 다시 생긴다"
  - "Shot 이 없는 z 는 조명 세팅 성공(OK) — D-73-08. SIDE z=1/4/8/13 이 전부 PREP_ACK FAIL 로 나가던 회귀를 되돌리지 않기 위한 계약"
  - "m_nLastZIndex == 0 은 범위 밖으로 취급하지 않는다 — 그 영역은 기존 WR-01 가드(bEmptyLastScope) 소관이라 이중 처리하지 않는다"
  - "TurnOffShotLights()(전 채널 강제 소등)는 무변경 보존 — [W8] 잔광 발생 시 운영자의 유일한 복구 경로"
  - "조명 실패 감지 한계(케이블 단선/컨트롤러 전원 OFF/LED 고장 미검출)를 코드 주석에 명시 — 제어에 과장 전달 금지(D-73-08)"
metrics:
  duration: "약 55분"
  completed: "2026-08-26"
  tasks: 3
  commits: 4
  files: 2
---

# Phase 73 Plan 05: 조명 채널 스코핑 대칭화 + 범위 밖 z 방어 Summary

조명 점등을 "13채널 절대값 덮어쓰기"에서 "자기 소유 채널만"으로 바꿔 소등 쪽 스코핑과 대칭을 맞추고,
그 스코프를 만드는 기준을 점등/소등이 **같은 함수**에서 가져오게 했다. 함께 `$PREP_ACK` 의 새 FAIL 정의를
실제 감지 가능하게 하고(조명 세팅 성공 bool 전파), FAIL 정의 변경으로 생긴 **범위 밖 z 의 미측정 PASS
위험**을 닫았다(M13).

## 무엇을 왜 했는가

`ApplyShotLightsInternal` 은 호출될 때마다 13채널 전부에 절대값을 썼다. 시퀀스가 3개일 때는 카메라 공유로
순차 실행이라 실질 피해가 없었지만, SIDE_1~4 는 **같은 물리 채널을 공유**하므로 한 지그의 `$PREP` 이
다른 지그의 조명을 그대로 덮어쓴다.

바꾼 것은 딱 하나다 — **남의 채널을 건드리지 않는다.** 자기 채널 집합 안에서는 예전 그대로
`Enabled=false → OFF` 를 건다. 이걸 함께 없앴다면 이전 사이클 조명이 남아 측정값이 조용히 틀어지는
잔광 회귀가 됐을 것이다.

스코프를 새로 계산하지 않고 소등 쪽이 이미 쓰던 `CollectOwnShotChannels` / `CollectOwnDatumChannels`
를 그대로 재사용했다. 두 경로가 각자 채널 목록을 가지면 언젠가 갈라져서 "켠 채널을 안 끄거나 안 켠 채널을
끄는" 비대칭이 다시 생긴다.

## Tasks 완료

| Task | 내용 | 커밋 |
| ---- | ---- | ---- |
| 1 | 사이클 종료 소등에서 비-Idle 형제 시퀀스 채널 제외 (R1 소등 측) | `da53d8c` |
| 2 | 점등도 자기 소유 채널만 — 대칭 스코핑 + 조명 성공 bool 전파 | `06ccdff` |
| 3 | 범위 밖 z_index 가 최종 P/F 를 내지 못하게 차단 (M13) | `9515632` |
| — | [Rule 2] 스코프 밖 점등 요청 무음 무시 → Error 로그 + 불변식 주석 갱신 | `733426d` |

## 변경 상세

### Task 1 — `CollectBusySiblingChannels()` (R1)

`SystemHandler.Handle.Sequences` 를 순회해 **자기 자신이 아니고**(`ReferenceEquals`) **Idle 이 아닌**
`InspectionSequence` 의 Shot/Datum 채널을 모은다. `TurnOffOwnShotLights` 가 소등 직전에 이 집합을 빼고,
뺀 채널마다 `[CycleLightOff] ... 소등 보류` 로그를 남긴다.

`CollectOwnShotChannels`/`CollectOwnDatumChannels` 는 같은 클래스의 다른 인스턴스라 `private` 그대로
호출된다 — **접근 제한자를 넓히지 않았다.**

`TurnOffShotLights()`(전 채널 강제 소등, 비상정지/레시피 전환 경로)는 **한 줄도 건드리지 않았다.**

### Task 2 — 대칭 스코핑 + bool 전파

- **`CollectOwnedChannelScope()`** 신설 — 본문은 `CollectOwnShotChannels` + `CollectOwnDatumChannels`
  두 호출뿐이다(별도 채널 목록을 새로 열거하지 않는다). 호출처 4곳:
  정의 / `ApplyShotLightsInternal` / `ApplyDatumLightsInternal` / `TurnOffOwnShotLights`.
- **`ApplyChannelLight(ownedScope, ...)`** / **`ApplyGroupLight(ownedScope, ...)`** — 둘 다 `bool` 반환.
  스코프 밖이면 `true` 반환 후 미접촉. 스코프 안이면 기존 동작 그대로(ON→SetLevel, OFF는 OnOff(false)만).
  `SetChannelLevel`/`SetLevel` 은 실제로 `bool` 을 돌려주므로(`LightHandler.cs:239,292`) `bOnOk && bLevelOk`
  로 집계했다.
- **`ApplyShotLightsInternal` / `ApplyDatumLightsInternal`** — `bool` 반환. 13채널을 **하나도 건너뛰지 않고**
  전부 호출한 뒤 `&&` 로 집계(중간 return 없음). 채널 목록/순서/Enabled/Brightness 소스는 무변경.
  실패 시 `[LightSet] ... 조명 세팅 실패 — light.ini 그룹/채널명 확인 필요` Error 로그.
- **`ApplyShotLights(int)`** — Shot 없는 z 는 `true`(OK) + Trace 로그로 계약 변경. 73-04 가 남긴
  [W9] 갭 종결.
- **`ApplyShotLightsDirect` / `ApplyDatumLights`** — `bool` 반환으로 값 전달. 호출부는 전부 반환값을
  쓰지 않으므로 **한 곳도 수정하지 않았다**(MainView 3곳 / Action_FAIMeasurement 2곳 확인).

### Task 3 — 범위 밖 z 방어 (M13)

`AddResponseV1Cycle` 의 판정 3줄만 교체했다.

```
bool bHasMeasurementShots = m_nLastZIndex > 0;
bool bIsBeyondRange = bHasMeasurementShots && m_nCurrentZIndex > m_nLastZIndex;
```

범위 밖이면 Error 로그 후 `bIsLastIndex = false` 로 두어 기존 코드가 알아서 B 를 만든다.
`ApplyCycleJudgement` / `BuildScopedResponse` / WR-01(`bEmptyLastScope`) 는 **한 줄도 바꾸지 않았다.**

`m_nLastZIndex == 0`(측정 Shot 이 없는 레시피)을 범위 밖으로 보지 않는 이유는, 그 영역이 이미 WR-01
가드의 담당이기 때문이다. 둘 다 걸면 같은 위험을 두 군데서 다르게 처리하게 된다.

## 현 레시피 실측 — 스코프 집합 (검증 시나리오 근거)

`D:\Data\Recipe\FAI_1\main.ini` 를 직접 파싱해 확인했다(계획 [W7] 과 완전 일치).

| 시퀀스 | 소유 채널 스코프 | 근거 |
| ------ | ---------------- | ---- |
| SIDE_1 | `{BACK, ALIGN_COAX}` | SHOT_4(z=2) Back+Coax, Side_Datum_3-1 Back |
| SIDE_2 | `{BACK}` | SHOT_6(z=2)/SHOT_26(z=3) Back, Side_Datum_3-2 Back |
| SIDE_3 | `{BACK}` | SHOT_3/23/24(z=2,3,4) Back, Side_Datum_4-2 Back |
| SIDE_4 | `{BACK}` | SHOT_5/25(z=2,3) Back, Side_Datum_4-1 Back |
| TOP | `{ALIGN_COAX}` | Top_Datum 만 Coax. TOP shot 3개는 조명 전부 False |
| BOTTOM | `{}` (공집합) | BOTTOM shot 16개 + Bottom_Datum 전부 조명 False |

BAR 채널(`SideLight_Enabled_1~4`)은 **현 레시피에서 아무도 켜지 않는다** — 검증 시나리오에 BAR 를 쓰면
무조건 통과하는 vacuous 테스트가 된다. `BACK` / `ALIGN_COAX` 로 검증해야 한다.

**로그 표기 주의:** `LightHandler.cs:234` 는 groupName **값**을 찍는다. `LIGHT_BACK` 의 값은 `"BACK"`
이므로 실제 로그는 `BACK - Set On : True` 형태다 — **`LIGHT_` 접두사가 없다.**

## Verification — 실제 실행 결과

### acceptance 전 항목

```
===== Task 1 =====
CollectBusySiblingChannels        want 2  got 3   ← 아래 "미일치 1건" 참조(코드만 세면 2)
소등 보류                          want 1  got 1
EContextState.Idle                want>=1  got 1
ReferenceEquals(sibling, this)    want 1  got 1
CollectOwn*/AddChannelIfEnabled 접근제한자 private void 유지  want 3  got 3
===== Task 2 =====
CollectOwnedChannelScope          want 4  got 4
private bool ApplyChannelLight(HashSet<string> ownedScope   want 1  got 1
private bool ApplyGroupLight(HashSet<string> ownedScope     want 1  got 1
private bool ApplyShotLightsInternal    want 1  got 1
private bool ApplyDatumLightsInternal   want 1  got 1
구 void 시그니처 3종               want 0  got 0
조명 세팅 대상 없음(OK)            want 1  got 1
케이블 단선                        want 1  got 1
Shot 본문  ApplyChannelLight( 10 / ApplyGroupLight( 3 / LightHandler.Handle. 0   전부 일치
Datum 본문 ApplyChannelLight( 10 / ApplyGroupLight( 3 / LightHandler.Handle. 0   전부 일치
===== Task 3 =====
bIsBeyondRange                    want 3  got 3
bHasMeasurementShots              want 2  got 2
범위 밖 z_index 수신               want 1  got 1
bEmptyLastScope (WR-01 보존)      want 2  got 2
===== 무변경 확인 =====
TurnOffShotLights / CollectOwn* / AddChannelIfEnabled / ApplyCycleJudgement /
BuildScopedResponse 본문 무변경 (git diff HEAD~4 hunk 0건)
```

**스코프 안 OFF 유지 확인** (잔광 회귀 방지 — 이 plan 최대 회귀 지점):

```
ApplyChannelLight 본문 : return LightHandler.Handle.SetChannelOnOff(channelName, false);
ApplyGroupLight  본문 : return LightHandler.Handle.SetOnOff(groupName, false);
```

**같은 수집 기준 확인** — `CollectOwnedChannelScope` 본문 전문:

```csharp
private HashSet<string> CollectOwnedChannelScope()
{
    var channels = new HashSet<string>();
    CollectOwnShotChannels(channels);
    CollectOwnDatumChannels(channels);
    return channels;
}
```

### 미일치 1건 — `CollectBusySiblingChannels` 2 → 3 (숫자를 맞추려는 조작 없이 보고)

3건의 내역:

```
790:        private HashSet<string> CollectBusySiblingChannels()          ← 정의(코드)
825:        //  ... 그래서 이제 CollectBusySiblingChannels()               ← 주석
832:            HashSet<string> busyChannels = CollectBusySiblingChannels(); ← 호출(코드)
```

**코드 참조는 계획대로 정확히 2건**(`grep -v '//'` 로 세면 2)이고, 3번째는 아래 [Rule 2 - 문서 정확성]
항목에서 갱신한 **주석 안의 언급**이다. 계획의 의도(정의 1 + 호출 1)는 충족했다.
`73-CONTEXT.md` D-73-09 가 "조건부 안전 주석을 이번 변경이 성립시키는지 대조하라"고 명시한 절차를 따른
결과이므로, **숫자를 맞추려고 주석을 지우지 않았다.**

### 빌드 (73-BUILD-VERIFY.md §2~4 규격 그대로)

MSBuild 는 PATH 에 없어 절대경로 호출:
`C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`
(나머지 인자는 규격 동일 — 대시 형식 / `-t:Rebuild` / 스크래치 `OutDir`·`IntermediateOutputPath`)

| 시점 | 구성 | exit | error | warning 줄 | 코드 분포 | 기준 |
| ---- | ---- | ---- | ----- | ---------- | --------- | ---- |
| Task 1 후 | SIMUL-ON (`Debug\|x64`) | 0 | 0 | **18** | CS0618×16 + CS0162×2 | 18 ✅ |
| Task 2 후 | SIMUL-ON | 0 | 0 | **18** | CS0618×16 + CS0162×2 | 18 ✅ |
| Task 3 후 | SIMUL-ON | 0 | 0 | **18** | CS0618×16 + CS0162×2 | 18 ✅ |
| Task 3 후 | SIMUL-OFF (`-p:DefineConstants=TRACE%3BDEBUG`) | 0 | 0 | **16** | CS0618×16 | 16 ✅ |
| Rule 2 후 | SIMUL-ON / SIMUL-OFF | 0 / 0 | 0 / 0 | **18 / 16** | 동일 | 18/16 ✅ |

- **새 경고 코드 종류 0건** (통과 기준 1순위)
- CS0162 가 ON 2줄 → OFF 0줄로 사라져 **SIMUL-OFF 가 실제로 적용됨**을 교차 확인
- `[Obsolete]` 제거 / `#pragma warning disable` / `NoWarn` 사용 **0건**

### [W4] 코딩 규칙 — plan 전체 diff 추가 라인 전수 검사

```
?? / ?.              : 0줄
삼항 후보(주석 제외) : 0줄
switch expression => : 0줄
```

### 73-03 산출물 보존

`NormalizeModelFolderName` 5건 / `[DatumModelPath]` Trace 로그 1건 그대로 살아있다(삭제·이동 0).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - 무음 실패 경로] 스코프 밖인데 "켜달라"는 요청을 조용히 무시하던 문제**

- **발생 위치:** Task 2 완료 후 호출부 전수 검토 중
- **문제:** `ownedScope` 는 소유 Shot/Datum 의 `Enabled=true` 플래그에서 그대로 만들어진다. 따라서
  `bEnabled == true` 인데 스코프 밖이라는 조합은 **소유권 판정이 어긋났다는 뜻**이다
  (다른 시퀀스 인스턴스로 `ApplyShotLightsDirect` 호출, `RecipeManager` 미로딩 등).
  계획 코드대로면 이때 `true` 를 반환하고 조용히 넘어가 **조명 없이 grab 이 진행돼 이미지가 조용히
  어두워진다.** 스코핑 도입이 **새로 만든** 무음 실패 경로이고, 위협 등록부의 T-73-17(조명 세팅 실패가
  조용히 삼켜짐)과 같은 성격이다.
- **수정:** `WarnIfEnabledOutOfScope(bEnabled, szLightName)` 추가 — `bEnabled == true` 인 스코프 밖
  요청에만 Error 로그. `Enabled=false` 인 스코프 밖 채널은 애초에 "남의 채널 미접촉"이 목적이므로
  로그하지 않는다(BOTTOM 처럼 스코프가 공집합인 시퀀스에서 로그 폭주가 나지 않는다).
- **동작 무변경:** 반환값·제어흐름은 계획 코드 그대로다. 로그만 추가했다.
- **acceptance 영향:** 없음(전 항목 재확인 통과).
- **커밋:** `733426d`

**2. [Rule 2 - 문서 정확성] 이번 변경으로 거짓이 된 불변식 주석 2곳 갱신**

- **(a) `InspectionSequence.cs` `TurnOffOwnShotLights` 헤더:**
  "잔여 위험: 같은 물리 채널을 두 시퀀스가 동시에 쓰도록 레시피가 구성되면 이 스코핑으로도 못 막는다
  (**현재 레시피 구조에서는 발생하지 않음**)" — 73-CONTEXT D-73-09 가 지목한 바로 그 조건부 안전 주석이고,
  이번 SIDE_1~4 분리가 그 조건을 정확히 만든다. Task 1 이 닫은 구멍이라는 사실로 다시 썼다.
  → 이 문장이 `CollectBusySiblingChannels` 카운트 2→3 의 원인이다.
- **(b) `InspectionListView.xaml.cs:1467`:**
  "ApplyShotLightsInternal은 shot 자신의 필드 + static LightHandler.Handle만 사용하므로 **어떤
  InspectionSequence 인스턴스로 호출하든 결과는 같다**" — Task 2 가 이 불변식을 깼다. 이제는
  그 shot 의 `OwnerSequenceName` 과 같은 인스턴스로 호출해야 조명이 켜진다.
  거짓 불변식을 남겨두면 다음 phase 가 그대로 믿고 판단한다(D-73-09 가 기록한 실패 패턴).
- **범위:** 계획의 `files_modified` 에는 `InspectionSequence.cs` 만 있었으나 (b)는 다른 파일이다.
  **주석 한 블록뿐이고 코드·동작·빌드 산출물에 영향이 없다**(diff 6줄, 전부 `//`).
  CRLF/BOM 은 원본 그대로 유지했다(CRLF 1649 / BOM True 확인).
- **커밋:** `733426d`

**3. [보고] MSBuild.exe 가 PATH 에 없음 — 절대경로 호출**

73-01/02/04 와 동일. 인자는 `73-BUILD-VERIFY.md` 규격 그대로다.

## 알려진 잔여 위험 (73-07 이 `73-HUMAN-UAT.md` 로 옮길 것)

**K1. [W8] 스코프 밖 조명 잔광 — 이 plan 이 의도적으로 남긴 위험**

`CollectOwnedChannelScope` 는 `AddChannelIfEnabled`(Enabled=true 인 채널만 수집)를 재사용하므로,
**자기 시퀀스가 한 번도 켜지 않는 채널은 더 이상 강제 OFF 되지 않는다.**

현 레시피의 구체적 시나리오:

- `ALIGN_COAX` 를 켜는 SIDE 지그는 **SIDE_1 뿐**이다(SHOT_4).
- SIDE_1 이 비정상 종료(OnError/OnStop)해 COAX 가 켜진 채 남으면,
  SIDE_2~4 는 COAX 를 스코프에 갖고 있지 않아 **끄지 못한다** → 촬영 조명 오염.
- PC1 쪽은 BOTTOM 의 스코프가 **공집합**이라 BOTTOM 의 `$PREP` 이 아무 채널도 끄지 않는다.
  (현 레시피에서는 TOP 이 자기 `ALIGN_COAX` 를 스스로 끄므로 실제 오염 경로는 확인되지 않았다.)

**제거하지 않고 남긴 이유:** 제거하려면 "시퀀스가 안 쓰는 채널도 끈다"로 되돌려야 하는데, 그게 바로
이 plan 이 없앤 간섭 원인이다. 대신:

1. 비상 경로인 **`TurnOffShotLights()`(전 채널 강제 소등)를 무변경 보존**했다 —
   운영자는 레시피 전환 / `$PREP` Op=0 / 비상정지로 복구할 수 있다.
2. 위 Rule 2 로그(`WarnIfEnabledOutOfScope`)가 반대 방향(켜야 하는데 안 켜짐)을 잡아 준다.

**K2. 조명 실패 감지 한계 — 제어에 과장 전달 금지 (D-73-08, accept)**

`$PREP_ACK` 의 FAIL 이 잡아내는 것은 두 가지뿐이다:
(1) `light.ini` 에 그룹/채널명이 없음 (2) 채널 매핑 소실(`RebindChannels` 결과 group.Count=0).
실제 시리얼 전송은 `void` 라 **케이블 단선 / 컨트롤러 전원 OFF / LED 고장은 못 잡는다.**
"FAIL 이 안 왔으니 조명은 정상"이라는 뜻이 **아니다.** 이 한계는 `ApplyChannelLight` 주석에 명시했다.

**K3. `$SITE_STATUS` PC2 SIDE_1 한정 보고** — 73-04 SUMMARY 의 [W10] 그대로 유효(이 plan 범위 밖).

## 검증 시나리오 (73-07 가 실기로 확인할 것)

BAR 채널은 아무도 켜지 않으므로 쓰지 않는다. `BACK` / `ALIGN_COAX` 로 검증한다.
로그에 **`LIGHT_` 접두사가 없다**(`BACK - Set On : True` 형태).

1. SIDE_2 의 `$PREP`(z=2) 직후 → `BACK - Set On` **존재**, `ALIGN_COAX - Set On` **0건**
2. SIDE_1 의 `$PREP`(z=2) 직후 → `BACK - Set On` 과 `ALIGN_COAX - Set On` **둘 다 존재**
3. SIDE_1 사이클 종료 소등 시 SIDE_2 가 비-Idle 이면 `BACK` 에 대해
   `[CycleLightOff] ... 소등 보류` 로그 + 실제 OFF 미호출
4. `D:/Data/Light/light.ini` `[Controller0] ChannelNames` 의 `BACK` **채널명**을 틀리면
   → `RebindChannels` 가 group.Count=0 → `ApplyShotLights` false → `$PREP_ACK ... FAIL`
5. Shot 없는 z(예: SIDE_1 z=1) → `$PREP_ACK ... OK` (73-04 [W9] 종결 확인)
6. SIDE_1 최대 z=2 인데 z=7 요청 → `[SEQ] SIDE_1 범위 밖 z_index 수신` Error 로그 + **B 응답**, P/F 없음
7. 25측정 / 7이탈 baseline 유지 (73-CONTEXT 검증 기준)

## Known Stubs

없음. 이 plan 이 만든 경로는 전부 실제 동작에 연결돼 있다.

## Threat Model 반영

| Threat ID | 반영 |
| --------- | ---- |
| T-73-15 (EoP, 범위 밖 z) | `bIsBeyondRange` 시 `bIsLastIndex=false` 로 최종 판정 금지 + Error 로그 |
| T-73-16 (DoS, 형제 채널 소등) | `CollectBusySiblingChannels` 로 비-Idle 형제 채널 제외 + 보류 로그 |
| T-73-19 (Tampering, 남의 채널 덮어쓰기) | `CollectOwnedChannelScope` 밖 채널 미접촉(점등/소등 동일 기준). 두 Internal 본문의 `LightHandler.Handle.` 직접 호출 0건으로 확인 |
| T-73-17 (Repudiation, 조명 실패 무음) | 13채널 AND 집계 → `$PREP_ACK` FAIL + Error 로그. **추가로** 스코프 밖 점등 요청도 로그화(Rule 2) |
| T-73-18 (조명 감지 한계 오해) | accept — `ApplyChannelLight` 주석 + 위 K2 에 명시 |

## Threat Flags

없음 — 신규 네트워크 표면 / 인증 경로 / 파일 접근 패턴 / 스키마 변경 없음.
조명 적용 대상 범위와 사이클 판정 조건만 좁혔다.

## csproj

`git status --porcelain WPF_Example/DatumMeasurement.csproj` → ` M`(앞칸 공백, 끝까지 unstaged).
4개 커밋 어디에도 포함되지 않았고, 4개 커밋 모두 **파일 삭제 0건**이다.

## Self-Check: PASSED

- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — FOUND
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — FOUND
- `.planning/phases/73-side-4-jig-split/73-05-SUMMARY.md` — FOUND
- 커밋 `da53d8c` / `06ccdff` / `9515632` / `733426d` — FOUND
