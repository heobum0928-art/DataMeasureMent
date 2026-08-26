# Phase 73 — CONTEXT (discuss 완료 2026-08-26)

SIDE 단일 시퀀스(z=0~15 한 바퀴에 지그 4개)를 SIDE_1~4 독립 시퀀스로 분리하고,
$TEST 의 Type 2~5 를 각 시퀀스로 라우팅한다. 지그마다 z 가 0 부터 독립 시작하고
지그마다 개별 P/F 가 나온다.

---

## D-73-01. z 배정 — 사용자 확정값 (2026-08-26 갱신)

**결정:** Type 별로 검사 종류를 구분하고, **각 시퀀스의 z_index 는 0 부터 시작**한다.

| 시퀀스 | Type | Datum | Datum z | 측정 Shot z | z 범위 |
|---|---|---|---|---|---|
| SIDE_1 | 2 | Side_Datum_3-1 | 0,1 | 2 | **0~2** |
| SIDE_2 | 3 | Side_Datum_3-2 | 0,1 | 2,3 | **0~3** |
| SIDE_3 | 4 | Side_Datum_4-2 | 0,1 | 2,3,4 | **0~4** |
| SIDE_4 | 5 | Side_Datum_4-1 | 0,1 | 2,3 | **0~3** |

**빈칸 없이 연속이어야 한다(제어 확정 2026-08-26).** 여유 z 예약은 하지 않는다 —
검사 항목이 늘면 그때 뒤로 이어 붙인다. 따라서 **마지막 z 에는 항상 측정 Shot 이 있고**,
`ComputeLastZIndex`(자기 Shot 최대 z)가 그대로 최종 P/F 시점이 된다 → 시퀀스별 `MaxZIndex`
선언 **불필요**(초기 검토안 폐기).

Top/Bottom 은 이번 phase 에서 건드리지 않는다(현 레시피상 전부 z=0).

**현 레시피 → 신규 매핑 (Datum 은 z=0,1 고정, Shot 은 z=2 부터):**

| 시퀀스 | 현재 z (A,B / shots) | 신규 z (A,B / shots) | 미사용 z |
|---|---|---|---|
| SIDE_1 | 0,1 / 2 | 0,1 / 2 | 3,4 |
| SIDE_2 | 3,4 / 5,6 | 0,1 / 2,3 | 4~7 |
| SIDE_3 | 7,8 / 9,10,11 | 0,1 / 2,3,4 | 5~9 |
| SIDE_4 | 12,13 / 14,15 | 0,1 / 2,3 | 4~9 |

**미사용 z 는 의도된 여유분이다.** 전체 40개 항목 구성이 목표인데 현 레시피는 25개뿐이라,
앞으로 측정을 추가할 자리를 z 범위에 미리 확보해 둔다. Shot 이 없는 z 는
`StartEmptyScope` 가 이미 정상 처리하므로(BOTTOM z=3/22 선례) PLC 가 보내도 문제없이 넘어간다.

## D-73-02. Datum/Shot 소속 이동 — 레시피 파일 스크립트 편집

**결정:** `main.ini` 를 스크립트로 직접 재작성한다. UI 기능 추가도, 이름 규칙 기반 자동 재배정도 하지 않는다.

**근거:** `DatumConfig.OwnerName` 은 `InspectionSequence.AddDatum()` 생성 경로로만 정해져
트리에서 시퀀스를 옮기는 수단이 없다(MainView.xaml.cs:4160 주석). UI 를 새로 만들면 이번 phase 에
MVVM 작업이 통째로 붙는다. 이름 규칙 자동 배정은 레시피 이름에 영구 의존하게 되어 더 위험하다.

**스크립트가 바꾸는 것:**
- Shot 8개의 `OwnerSequenceName`: `SIDE` → `SIDE_1`..`SIDE_4`
- Datum 4개의 소속 키
- `ZIndex` / `ZIndexA` / `ZIndexB` 를 위 표대로 재계산

**안전장치(필수):**
- 백업 완료: `D:\Backup\FAI_1_backup_before_phase73_260826\` (.shm 31개 포함)
- 스크립트는 **원본을 덮지 말고 새 파일로 출력** → diff 확인 후 교체
- 편집 전후 Datum 개수/Shot 개수/측정 개수 카운트 비교(0 클로버 방지)
- `InspectionRecipeManager.cs:88` 의 "비활성 시퀀스 Datum 보존" 경로가 살아있는지 확인
  (과거 커밋 3faa91b 데이터 손실 사고 지점)

---

## D-73-03. 티칭 모델(.shm) 경로 — 기존 SIDE 폴더 유지

**결정:** `TeachingStorageService.NormalizeTeachingKey` 의 `Contains("SIDE") → "SIDE"` 를 **그대로 둔다.**
SIDE_1~4 가 전부 같은 SIDE 폴더를 쓴다.

**근거:** Datum .shm 파일명이 Datum 이름 기준(`DatumSide_Datum_3-1.shm` 등)이라 4개가 서로 겹치지 않는다.
폴더를 나누면 기존 .shm 을 전부 이동해야 하고, 경로가 어긋나면 "모델 못 찾음" 으로 조용히 전 항목이
실패한다. 재티칭 비용 0 이 이 선택의 핵심 이득이다.

**단, plan 에서 확인할 것:** `ResolveDatumModelPath(datum)` 이 `SourceShotName → ShotConfig →
OwnerSequenceName` 으로 경로를 만든다. OwnerSequenceName 이 `SIDE` → `SIDE_1` 로 바뀌면
**여기서 나오는 경로가 달라질 수 있다.** NormalizeTeachingKey 를 안 건드려도 이 경로가
`SIDE_1` 폴더를 가리키면 기존 .shm 을 못 찾는다. 분리 직후 첫 검사 전에 실제 경로를 로그로 찍어 확인한다.

---

## D-73-04. 테스트 클라이언트 — 이번 phase 에 포함

**결정:** `C:\Info\Project\CommunicationTest` 수정을 이번 phase 범위에 넣는다.

**근거:** 현재 "전체" 모드가 Type 2/3/4/5 각각에 z=0~15 를 돌려 **같은 검사를 4번 반복**한다(44초).
분리 후에는 Type 별 z 범위가 달라지므로(2→0~2, 3→0~3, 4→0~4, 5→0~3) 클라이언트를 안 고치면
분리 결과를 검증할 수단 자체가 없다.

**변경:** Type ↔ z 범위 매핑 테이블을 두고, "전체" 는 Type 2~5 를 각자의 z 범위로 순차 실행.
예상 사이클 44초 → 약 11초.

---

## D-73-05. $PREP 에 Type 추가 — 제어팀 협의 완료 (2026-08-26)

**결정:** `$PREP:site,z_index@` → **`$PREP:site,Type,z_index@`**. 제어팀 합의 완료.
Type 은 TOP/BOTTOM(0,1) 포함 **전 대상**에 적용한다. `$TEST` 는 변경 없음.

**필드 위치:** Type 은 반드시 **2번째**(site 다음, z_index 앞). 3번째 자리는 안 된다 —
현 파서가 구 펌웨어 호환으로 `$PREP` 3번째 필드를 읽지 않고 버린다(VisionRequestPacket.cs:427).

**⚠ 파서 재작성 필수:** `TryParsePrepFields` 는 `dataList[1]` 을 무조건 z_index 로 파싱한다.
Type 이 숫자이므로 **파싱은 성공하고 z_index 로 오인**된다 — 예외도 FAIL 도 안 난다.
`$TEST` 의 `TryParseTestFieldsV1` 처럼 **명명 상수 인덱스 + 버전 분기** 파서로 다시 써야 한다.

**Type ↔ 시퀀스 매핑:** 0=TOP, 1=BOTTOM, 2=SIDE_1, 3=SIDE_2, 4=SIDE_3, 5=SIDE_4

**z_index 의미 확정 (2026-08-26):** 와이어로 오는 z 는 **Type 안에서 0 부터 시작하는 지역 번호**다.
제어 HMI 의 `2D Side Vision Index 위치` 40칸 테이블은 **PLC 측 물리 위치(mm) 저장소**이며,
스펙 표의 `z_index: 검사위치(0~40)` 는 그 저장소 크기를 적은 것이지 와이어 값 범위가 아니다.
(Type, z) → 테이블 행 매핑은 PLC 내부 책임. **우리가 받는 z 는 최대 9.**

지그별 소요: Side#1 5 + Side#2 8 + Side#3 10 + Side#4 10 = **33칸** (40칸 중 여유 7).

**$PREP_ACK 도 Type 을 갖는다:** `$PREP_ACK:site,Type,z_index,OK|FAIL@`
→ VisionResponsePacket.cs:434~453 송신부 수정 필요(M12).

**FAIL 정의:** "해당 z_index 가 없을 때"(범위 밖 요청). 기준점 전용 z(Datum ZIndexA/B)와
아직 항목을 안 넣은 빈 z 는 **OK 로 응답**한다 — 현 코드가 이미 그렇게 동작하므로 무수정.
(과거 SIDE z=1/4/8/13 이 전부 PREP_ACK FAIL 로 나가던 회귀를 고친 결과물 — 되돌리지 말 것.)

---

## D-73-08. 제어 협의 최종 확정 (2026-08-26, 김민욱선임)

**합의 사항**

1. `$PREP:site,Type,z_index@` + `$PREP_ACK:site,Type,z_index,OK|FAIL@` — TOP/BOTTOM 포함 전 대상
2. **지그별 개별 P/F 확정.** 지그 안에서 중간 z 는 B, 마지막 z 에서 P 또는 F
3. `$PREP_ACK` 의 **FAIL = 조명 세팅 실패 전용.** 검사 항목 유무는 응답에 반영하지 않는다
4. z 는 지그마다 0 부터, **빈칸 없이 연속**

**제어 측 공정 순서 (회신에서 확인 — 설계에 중요)**

```
2D Side#1 검사 완료 → 피커로 제품 들기 → 바텀얼라인 비전 검사 → Side#2 로 이동 → ...
```

**지그 4개는 순차 진행이며 사이에 제품 이송과 Bottom Align 검사가 들어간다.**
즉 SIDE_1~4 가 동시에 도는 창이 없다. → R1/R2/R3 의 실제 발생 가능성이 크게 낮아진다.

⚠ **단 이 순서는 제어 공정 설계에 의존할 뿐 코드가 강제하지 않는다.** 수정은 여전히 필요하되
차단 요소는 아니므로 우선순위를 낮춘다. (R1 은 (a)안 유지)

**FAIL 정의 변경의 파급 (범위 축소)**

Shot 유무를 ACK 에 반영하지 않으므로 `ApplyPrepToSequences` 의 예외 분기
(`z==0 무조건 OK`, `크로스-Z Datum 전용 tick OK`)가 **전부 불필요**해진다 → M1 소멸.
대신 `ApplyShotLightsInternal`/`ApplyDatumLightsInternal` 이 `SetOnOff`/`SetLevel` 반환값을
집계해 bool 을 돌려주도록 바꾼다(현재는 반환값을 전부 버림).

⚠ **감지 한계를 제어에 과장 전달하지 말 것.** `SetOnOff(string,bool)` 이 false 를 돌려주는 건
(1) light.ini 그룹명 부재 (2) 채널 매핑 소실 두 경우뿐이다(LightHandler.cs:216).
실제 시리얼 전송은 `void` 라 **케이블 단선/컨트롤러 전원 OFF/LED 고장은 못 잡는다.**

**신규 과제 (FAIL 정의 변경으로 생긴 갭)**

**M13. 범위 밖 z 방어.** ACK 가 더 이상 걸러주지 않으므로, 자기 z 범위 밖 요청이 오면
최종 판정을 내지 말고 로그만 남기고 넘어가야 한다. 현재는 `bIsLastIndex = z >= ComputeLastZIndex`
라 **범위 밖 z 가 측정 0건으로 최종 P/F 를 낼 수 있다**(미측정 PASS 위험).

---

## D-73-06. 검증으로 확인된 사실 (에이전트 2종 교차검증, 2026-08-26)

### 확정된 사실

| 사실 | 근거 |
|---|---|
| `ApplyPrepToSequences(z)` 가 **전 시퀀스**에 조명 적용, 마지막이 이김 | SystemHandler.cs:978~1010, 등록순서 Top→Side→Bottom |
| 조명 적용은 **13채널 절대값 덮어쓰기**(누적 아님) | InspectionSequence.cs:900~945 |
| TOP shot 3개·BOTTOM shot 16개가 **전부 z=0** | main.ini 집계 |
| → PC1 에서 `$PREP z=0` 시 **BOTTOM 이 TOP 조명을 덮어씀** | 위 3개 조합 |
| grab 직전 재적용이 최종 촬영 조명을 바로잡음 | Action_FAIMeasurement.cs:213 |
| 단 재적용은 `DatumConfigs.Count > 0` 일 때만 | Action_FAIMeasurement.cs:203 |
| 현 레시피는 TOP/BOTTOM/SIDE 전부 Datum 보유(1/1/4) | main.ini FIXTURE 섹션 |
| `FindShotByZIndex` 는 **첫 매칭 하나만** 반환 | InspectionSequence.cs:618 |
| → BOTTOM z=0 shot 16개 중 1개 조명만 예열됨 | 위 조합 |

**결론:** TOP/BOTTOM 이 지금까지 멀쩡했던 이유는 (1) grab 직전 재적용, (2) `_lastPrepZIndex` 가
가질 수 있는 값이 0 하나뿐이라 오염될 수가 없었기 때문. **z 값이 대상을 암시한다는 전제** 위에
현 구조가 서 있고, Phase 73 이 그 전제를 깬다.

### 조명 예열 손실의 실제 영향 — 미측정

`WaitForPendingWrites()` 로 시리얼 전송 완료는 대기하므로(Action_FAIMeasurement.cs:216),
남는 것은 **LED 물리 안정화 시간**뿐이다. VersionDefine 1.5.x 기록은 `WaitForPendingWrites`
도입 **이전** 사례라 현재 상황의 근거로 쓸 수 없다.

**예열 유무 산포 비교 실측이 없다.** 근거를 부풀리지 말 것.
→ Phase 73 중 `$PREP`~`$TEST` 간격 장/단 각 20회 반복 측정으로 정량화할 것(선택 과제).

---

## D-73-07. 수정 필요 항목 — 위험도순 (에이전트 전수조사)

### 위험 (설계 결정 선행 필요)

**R1. LIGHT_BAR 채널 공유 소등 충돌 — Type 으로 해결 안 됨**
SIDE_1~4 는 `LIGHT_BAR_1~4` **같은 상수**를 쓴다. `TurnOffOwnShotLights` 의 "자기 채널만"
스코핑이 무의미해져, SIDE_1 종료 소등이 SIDE_2 조명을 끈다.
`InspectionSequence.cs:783~790` 주석이 기록한 "현재 레시피에서는 발생하지 않음" 조건을
**이번 분리가 정확히 만든다.** 상호배타 게이트(`FindBlockingSequenceName`)는 **UI RUN 전용**이고
TCP `$TEST` 경로는 거치지 않는다. `OnStop`/`OnError` 소등은 무조건 발화.
→ **채택: (a) 형제 시퀀스 non-Idle 채널 제외.** 지그별 독립 사이클이라는 이번 phase 취지를
유지하려면 소등도 지그 단위여야 한다. (b) "4-SIDE 전체 종료 후 소등" 은 단순하지만 4개를 다시
하나의 묶음으로 되돌리는 셈이라 기각. plan 단계에서 재확인할 것.

**R2. `_lastPrepZIndex` 전역 단일 변수** (SystemHandler.cs:20,216,236,273,282,285)
z 가 시퀀스마다 0 부터 시작하면 값이 겹친다. **조건부 위험** — `$PREP`/`$TEST` 가 대상 간
섞일 때만 오염된다. 순차 진행이면 성립하지 않으나, 제어가 순서를 지킨다는 보장이 코드에 없다.
→ `Dictionary<string seqName,int>` 로 승격, `ProcessTest` 에서 `packet.Identifier` 로 조회.

**R3. z==0 사이클 리셋 전역 판정** (SystemHandler.cs:236, InspectionSequence.cs:325,1608,1626)
R2 와 동일 원인. 함께 수정.

**R4. `SaveToIni`/`LoadFromIni` Param 위치 인덱스** (Sequence/SequenceHandler.cs:204~210, 242~248)
시퀀스를 위치 인덱스로 `Param0,Param1…` 저장. 3→6 개가 되면 **번호가 밀려 구 레시피 매핑이
어긋난다.** `SaveToIni` 의 Param 저장은 IsDynamicFAIMode 와 무관하게 **항상** 실행된다.
`reference_parambase_missing_key_zeroes_default` 함정과 겹치면 조용한 0-클로버.

**R5. `IsSequenceActive`** (Custom/Sequence/SequenceHandler.cs:42~48)
`role != TopBottom` 이면 `seqId == ESequence.Side` 만 true. SIDE_1~4 추가 시
**시퀀스가 생성조차 안 된다.**

**R6. `ResolveSequenceName` 의 `default: return SEQ_TOP`** (:26~32)
case 미추가 시 SIDE_3 이 **TOP 으로 조용히 해석**되고 로그도 안 남는다.

### 수정 필요

| # | 항목 | 위치 |
|---|---|---|
| M1 | `ApplyPrepToSequences` OR 집계 → 단일 시퀀스 오버로드. 폴백 조건(`z==0 \|\| IsDatumOnlyExecutionIndex`)을 그 시퀀스 기준 재정의. **미수정 시 SIDE z=1/4/8/13 PREP_ACK FAIL 회귀 재현** | SystemHandler.cs:977~1025 |
| M2 | `TriggerInspectionCycleManually` — PrepPacket 에 Type 미설정 → 기본값 0(TOP) 라우팅. M1 과 **동시 수정 필수** | SystemHandler.cs:945~973 |
| M3 | `ESite` 3슬롯 한계 + `MapPc2Resources` — Type 2~5 가 현재 전부 Top 슬롯 폴백. SIDE_1~4 라우팅 신설 | ResourceMap.cs:11~15,106~117,148~185 |
| M4 | `FIXTURE_SIDE` → `FIXTURE_SIDE_1..4` 분할 + 마이그레이션. **일부만 등록된 상태로 저장 시 나머지 Datum `DatumCount=0` 소실**(3faa91b 사고 패턴) | InspectionRecipeManager.cs:85~145,193,272 |
| M5 | `RebuildInspectionActions` 3회 하드코딩 → 6회. 누락 시 트리 미표시 | Custom/Sequence/SequenceHandler.cs:310~312 |
| M6 | `OwnerSequenceName` `"SIDE"` → SIDE_1~4 레시피 마이그레이션 | D-73-02 스크립트 |
| M7 | `TeachingStorageService.cs:229~231` `Contains("SIDE")` — `"SIDE_1"` 도 `"SIDE"` 로 뭉개짐 | D-73-03 참조 |
| M8 | `VisionResponsePacket.cs:226` `Site == (int)ESequence.Bottom` — 프로토콜 site 정수와 enum 직접 비교(잘못된 커플링) | 재검토 |
| M9 | `MainView.xaml.cs:4149` `roles` 배열에 SIDE_1~4 추가. 누락 시 Datum UI 미표시 | |
| M10 | `$PREP` 파서 재작성(D-73-05) + `ResourceMap.SetIdentifier` 에 `Prep` case 신설(현재 없음) | |
| M11 | `$RESET` 에 Type 상태 리셋 추가(새 상태 도입 시) | SystemHandler.cs:914~932 |

### 안전 (무수정 확인)

`ComputeLastZIndex`/`m_nLastZIndex`/`m_nCurrentZIndex`(이미 `OwnerSequenceName == Name` 필터 +
인스턴스 필드) · `Sequences[string]` 인덱서(이름 기반) · `TurnOffPrepLights`(호출자 0) ·
`InspectionListView.xaml.cs`(Owner 문자열 기반)

### 문서 결함 (부수 발견)

- `SequenceHandler.cs:39` 주석 "SIMUL 은 전체 활성" — 코드에 SIMUL 분기 없음(stale)
- `DeviceHandler.cs:220~227` — `sharedMil` 공유는 **실 HW 빌드 한정**. SIMUL 은 역할별 VirtualCamera

---

## 검증 기준 (2026-08-26 09:17 SIDE 자동검사 baseline)

분리 후에도 **측정값과 공차이탈 개수가 동일**해야 한다. 지그별로 쪼개져 나올 뿐이다.

| Shot | 측정 | 이탈 |
|---|---|---|
| 3-1_D1 | 2 | 2 |
| 3_2_D1 (×2) | 2 | 0 |
| 4-2_H5 | 1 | 0 |
| 4-2_C13-14_P1 | 6 | 3 |
| 4-2_F9 | 3 | 0 |
| 4-1_F9 | 3 | 0 |
| 4-1_C13-14 | 6 | 2 |
| **합계** | **25** | **7** |

추가 확인: 지그별 P/F 가 각 시퀀스의 마지막 z 에서 **개별적으로** 나올 것.

---

## 위험 요소 (plan 에 반드시 반영)

1. **Datum 소실** — 시퀀스 구성 변경은 과거 데이터 손실 사고(3faa91b)와 같은 지뢰밭.
   SIDE Datum 4개는 현재 유일본. 백업 확인 후 착수.
2. **크로스-Z 회귀** — 2026-08-26 커밋 8d6982c(SIMUL role B 세로 이미지) 직후라 기준선이 막 잡혔다.
   ZIndexA/B 재매김이 이 수정을 깨뜨리지 않는지 반드시 확인.
3. **.shm 경로 이동** — D-73-03 후단 참조.
4. **Phase 69 상호배타(CanRunSequence)** — SIDE 4개가 같은 카메라 객체를 공유한다.
   참조 동일성 기반 판정이라 자동으로 걸릴 것으로 보이나 **검증 필요**.

---

## 변경 범위 — 초기 grep (16곳/5파일)

> ⚠ 이 목록은 **초기 조사분이며 불완전하다.** 실제 범위는 D-73-07 (위험 6 + 수정필요 11)을 따를 것.

- `Custom/Define/ID.cs` — `ESequence { Top=1, Side=2, Bottom=3 }` → Side 4종
- `Custom/Sequence/SequenceHandler.cs` — SEQ_SIDE 상수 4개, IsSequenceActive,
  RegisterSequences / RegisterActions / InitializeSequences 1→4, CanRunSequence
- `Custom/TcpServer/ResourceMap.cs` — TryResolveSlotByType 의 Type 2~5 Top 슬롯 폴백 → SIDE_1~4 라우팅
- `Custom/Sequence/Inspection/InspectionRecipeManager.cs` — ESequence.Side 참조
- `Custom/Sequence/Inspection/ShotConfig.cs` — ESequence.Side 참조
- `UI/ContentItem/MainView.xaml.cs` — ESequence.Side 참조

**재사용:** 시퀀스별 z 독립 시작과 "마지막 z = 최종 P/F" 는 `InspectionSequence` 에 이미 있다
(`DATUM_Z_INDEX`, `ComputeLastZIndex` 가 `shot.OwnerSequenceName == Name` 으로 자기 것만 집계).
시퀀스를 4개로 늘리면 **자동 적용**된다 — 새로 짤 로직이 아니다.

---

## 코딩 규칙 (프로젝트 상시 — 하위 에이전트 프롬프트에 매번 명시)

- 삼항 `?:` 금지 → if-else. `??` `?.` 축약 금지 → 명시적 분기
- 분기는 if/else 또는 전통 switch 만 (C# 8.0 switch expression 금지)
- 초보자가 봐도 이해되는 코드 — 긴 조건은 이름 있는 bool 로 선추출
- UI 는 MVVM. MainView.xaml.cs(4,300줄)에 새 로직 추가 금지
- 헝가리언 접두사(b/n/sz), C# 7.2 만, 파일별 중괄호 스타일 유지
- 날짜 주석(//YYMMDD hbk) 규칙 폐기 — 비자명한 "왜" 만 최소 주석
- 빌드 경고 baseline — **Phase 73 착수 전 SIMUL-ON 12줄(CS0618×10 + CS0162×2) / SIMUL-OFF 10줄**.
  단 73-01 이 `RegisterActions()` 의 Side 호출을 1→4줄로 늘려 CS0618 이 6줄 증가하므로,
  **73-01 완료 후에는 SIMUL-ON 18줄 / SIMUL-OFF 16줄이 정상**이다. 상세·명령은 `73-BUILD-VERIFY.md`.
  "경고 0" 을 통과 기준으로 쓰지 말 것. 숫자를 맞추려고 `[Obsolete]` 제거/`#pragma warning disable`/`NoWarn` 금지
- 실행 중 프로세스 종료 금지 — 잠김 시 스크래치 OutDir 로 컴파일 검증
- SIMUL_MODE ON/OFF 양쪽 빌드 검증
- `DatumMeasurement.csproj` 는 로컬 전용 → 커밋 금지
- 코드 리뷰는 `gsd-code-reviewer` 필수, 세밀하게

---

## D-73-09. 조사 절차 교훈 — "조건부 안전" 주석 대조 (2026-08-26)

**이번 phase 에서 초기 조사가 놓친 것과 그 이유를 기록한다. 다음 phase 에서 반복하지 말 것.**

초기 SEED 는 `SEQ_SIDE`/`ESequence.Side` **참조 16곳을 grep 으로 세고 범위 파악을 끝냈다고 판단**했다.
그러나 참조를 세는 것과 **변경이 깨는 전제를 찾는 것**은 다른 작업이다. 실제로 놓친 두 건은
**둘 다 코드 주석에 이미 경고가 적혀 있었다.**

| 놓친 것 | 코드에 있던 경고 | 왜 놓쳤나 |
|---|---|---|
| R1 `LIGHT_BAR` 공유 소등 충돌 | `InspectionSequence.cs:789` — "같은 물리 채널을 두 시퀀스가 동시에 쓰도록 레시피가 구성되면 이 스코핑으로도 못 막는다(**현재 레시피 구조에서는 발생하지 않음**)" | `SEQ_SIDE` 참조만 grep. 조명 경로는 대상 밖으로 봄 |
| R4 `SaveToIni` Param 위치 인덱스 | `SequenceHandler.cs:204~210, 242~248` — 시퀀스를 **위치 인덱스**로 `Param0,Param1…` 저장 | "시퀀스 개수를 3→6 으로 늘린다"가 phase 정의인데 개수에 의존하는 저장 구조를 안 찾음 |

**절차 (다음 phase 부터 적용)**

1. 변경 대상 심볼 grep 은 **시작점일 뿐 범위 확정이 아니다.**
2. **"이 변경이 깨는 전제가 무엇인가"** 를 먼저 묻는다.
3. 코드에서 **조건부 안전 주석**을 검색해 이번 변경이 그 조건을 성립시키는지 대조한다:
   `현재.*발생하지 않` / `현재.*구조에서는` / `잔여 위험` / `이 경우엔 안전` / `아직은` / `지금은 문제없`
4. 변경이 **개수·순서·위치 인덱스**에 손대면 그 값에 의존해 **영속 저장**되는 곳을 전수 조사한다
   (INI 섹션 번호, 배열 인덱스, enum 정수값).

**교차검증 에이전트는 사용자 요청이 아니라 기본 절차로 돌린다.** 이번엔 사용자가
"에이전트 확인해봐" 라고 지시한 뒤에야 전수조사가 이루어졌고, 그 전 오케스트레이터 판단은
"TOP/BOTTOM 은 조명 채널이 달라 안전" 이었으나 **실제로는 덮어쓰기라 오판**이었다.
