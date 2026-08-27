---
status: pending
phase: 73-side-4-jig-split
source: [73-07-PLAN.md, 73-CONTEXT.md]
started: 2026-08-26
updated: 2026-08-26
---

# Phase 73 — 실기 필요 항목 추적 (실장비 전용 UAT)

이 파일은 **SIMUL_MODE + TCP 로는 원리적으로 확인할 수 없는 항목 = 실기 필요 항목만** 담는다.
Phase 73 의 완료 판정은 SIMUL 검증(S1~S9, `73-07-PLAN.md` Task 2)으로 내리며,
**이 파일의 항목들은 phase 완료를 막지 않는다**(Phase 65 `65-HUMAN-UAT.md` 선례).

실기 확인 시점에 각 항목의 `result:` 와 `상태:` 를 갱신한다.

## Current Test

[작업자 실장비 대기 — 실 Z축 / 실 조명 컨트롤러 / 실 PLC / 실 자재 필요]

---

## Tests

### H1. 조명 예열 효과 정량화

why-not-simul: |
  SIMUL 은 `VirtualLightController` 를 쓴다. 시리얼 전송도, LED 물리 안정화 시간도 없다.
  즉 "$PREP 으로 미리 켜 두는 것이 측정 산포를 줄이는가" 라는 질문 자체가 SIMUL 에서는
  측정 대상이 존재하지 않는다.
steps: |
  1. 실장비에서 SIDE_1 한 지그를 고정 자재로 20회 반복 검사한다.
     - 조건 A(예열 짧음): `$PREP` 직후 즉시 `$TEST` 송신
     - 조건 B(예열 김): `$PREP` 후 충분히(예: 1초 이상) 두고 `$TEST` 송신
  2. 두 조건 각각 20회의 측정값을 엑셀 export 로 뽑는다.
  3. 항목별 표준편차/최대-최소 폭을 비교해 수치로 기록한다.
expected: |
  조건 A/B 의 산포 수치가 표로 기록되고, 차이가 유의한지 아닌지 **숫자로** 결론난다.
근거-주의: |
  ⚠ **현재 예열 효과는 "영향 미측정" 상태다.** D-73-06 이 명시했다 —
  "예열 유무 산포 비교 실측이 없다. 근거를 부풀리지 말 것."
  `WaitForPendingWrites()`(Action_FAIMeasurement.cs:216)로 시리얼 전송 완료는 이미 대기하므로
  남는 변수는 LED 물리 안정화 시간뿐이며, VersionDefine 1.5.x 의 과거 기록은
  `WaitForPendingWrites` 도입 **이전** 사례라 현재 상황의 근거로 쓸 수 없다.
  이 실측 전까지는 "예열이 산포를 줄인다" 고 단정해 적지 말 것.
상태: PENDING(실기 대기)
result: [pending]

### H2. 실제 Z축 이동 타이밍

why-not-simul: |
  SIMUL 은 축을 움직이지 않고 저장된 이미지를 읽는다. z 전환 시 실제 축이
  목표 위치에 도달했는지, 정지 후 흔들림이 남아 있는지는 실기에서만 나타난다.
steps: |
  1. 지그 4개(SIDE_1~4)를 각각 자동검사로 완주시킨다.
  2. 각 z 전환마다 캡쳐된 이미지를 리뷰어에서 확인한다(모션 블러/초점 이탈 여부).
  3. Datum 검출 실패나 측정 산포가 특정 z 에서만 커지는지 확인한다.
expected: 각 지그의 z 전환에서 이미지 흔들림/미도달 0건. 특정 z 편중 실패 0건.
상태: PENDING(실기 대기)
result: [pending]

### H3. PLC 실제 송신 순서

why-not-simul: |
  제어 공정 순서(Side#1 검사 → 피커 이송 → 바텀얼라인 → Side#2 …)는 **코드가 강제하지 않는다.**
  D-73-08 회신이 "SIDE_1~4 동시 실행 창이 없다" 는 근거인데, 그 전제가 실제로 지켜지는지는
  실 PLC 로만 확인된다. SIMUL 에서는 우리가 보내는 순서만 관측된다.
steps: |
  1. 실 PLC 연동 상태에서 1 사이클 전체를 로그로 남긴다.
  2. `[PREP] site=... Type=... seq=... z=...` 와 `$TEST`/`$RESULT` 타임스탬프를 순서대로 정렬한다.
  3. SIDE_1 의 최종 P/F 가 나오기 **전에** SIDE_2 의 `$PREP`/`$TEST` 가 오는 구간이 있는지 본다.
expected: |
  실제 순서가 D-73-08 회신과 일치하고, SIDE 시퀀스가 동시에 도는 창이 0 이다.
  동시 실행 창이 관측되면 R1(조명 채널 공유) 잔여 위험 K1 이 실제 위험으로 승격된다.
상태: PENDING(실기 대기)
result: [pending]

### H4. 크로스-Z Datum 실촬영

why-not-simul: |
  SIMUL 은 저장 이미지 경로를 읽는다. 크로스-Z Datum(ZIndexA/B 두 장 조합)의 role B 이미지
  선택 결함은 2026-08-26 커밋 `8d6982c` 로 막 고쳐진 참이라 기준선이 새로 잡힌 상태다.
  실제 2장 촬영으로 Datum 이 잡히는지는 실 Z축 + 실 카메라에서만 확인된다.
steps: |
  1. SIDE_1~4 각각을 자동검사로 돌린다.
  2. 각 지그의 Datum(3-1 / 3-2 / 4-2 / 4-1)이 z=0, z=1 두 장 실촬영으로 Find 성공하는지 확인한다.
  3. 로그의 `[DatumModelPath] owner=SIDE_N folder=SIDE path=...` 가 기존 `.shm` 을 가리키는지 확인한다.
expected: 4개 Datum 전부 실촬영 2장으로 검출 성공. 모델 경로가 기존 SIDE 폴더를 가리킨다.
상태: PENDING(실기 대기)
result: [pending]

### H5. 조명 실패 실물 검출 한계 — 제어팀 통보

why-not-simul: |
  `LightHandler.SetOnOff(string,bool)` 이 false 를 돌려주는 경우는 두 가지뿐이다(LightHandler.cs:216 계열):
  (1) light.ini 그룹명 부재 (2) 채널 매핑 소실. 실제 시리얼 전송은 `void` 라
  **케이블 단선 / 컨트롤러 전원 OFF / LED 고장은 감지하지 못한다.**
  이건 SIMUL/실기 문제가 아니라 설계 한계이며, 제어가 `$PREP_ACK` 의 FAIL 을
  "조명이 물리적으로 켜졌다는 보증" 으로 오해하면 안 된다.
steps: |
  1. 제어팀(김민욱선임)에게 위 한계를 문서로 전달한다.
  2. 조명 물리 고장 감지가 필요하다면 별도 수단(전류 감시/피드백 신호)이 필요함을 합의한다.
expected: 제어팀이 한계를 인지했다는 확인 회신을 받는다.
상태: PENDING(제어팀 통보 대기)
result: [pending]

### H6. 지그별 배출 연동

why-not-simul: |
  지그별 개별 P/F 를 제어가 실제로 지그 단위 배출에 쓰는지는 실 설비 동작이다.
  비전 쪽은 `$RESULT:site;Type;P|F|B@` 를 지그마다 내보내는 것까지가 책임이다.
steps: |
  1. 지그 4개 중 1개만 NG 인 자재를 준비한다.
  2. 자동검사 1사이클을 돌린다.
  3. 해당 지그만 배출되고 나머지 3개는 정상 진행되는지 확인한다.
expected: NG 판정이 난 지그 1개만 배출된다. 4개 통째 배출(구 동작) 0건.
상태: PENDING(실기 대기)
result: [pending]

---

## 알려진 제약 (실기 항목은 아니지만 이월 대상)

### K1. [W8] 스코프 밖 조명 잔광

출처: `73-05-SUMMARY.md`

`CollectOwnedChannelScope()` 는 `AddChannelIfEnabled`(Enabled=true 인 채널만 수집)를 재사용한다.
따라서 **자기 시퀀스가 한 번도 켜지 않는 채널은 강제 OFF 대상에서도 빠진다.**
현 레시피에서는 SIDE_1(SHOT_4)만 `ALIGN_COAX` 를 켜므로, SIDE_1 이 비정상 종료(사이클 중단 등)해
COAX 가 켜진 채 남으면 SIDE_2~4 촬영이 오염될 수 있다.

- 이 설계는 의도적이다 — 반대로 하면(전 채널 강제 OFF) 지그별 독립 사이클이라는 이번 phase 취지가 깨진다.
- 복구 수단: `TurnOffShotLights()`(전 채널 강제 소등, 레시피 전환/비상정지 경로)를 무변경 보존했다.
- 실제 위험도는 H3(제어 송신 순서 실측) 결과에 종속된다.

상태: PENDING(실기 관찰 대기 — 현 레시피에서 관측되면 보고)

### K2. [W10] `$SITE_STATUS` 대상 특정 불가

출처: `73-04-SUMMARY.md`

`ResourceMap.cs:184` 가 ESite 슬롯 1개만 조회하므로, PC2 에서는 **SIDE_1 상태만 보고**된다.
SIDE_2~4 가 검사 중이어도 Idle 로 보일 수 있다.
근본 원인은 `$SITE_STATUS` 명령에 Type 필드가 없다는 것이며, 비전 단독으로는 고칠 수 없다.

- 필요한 조치: 제어와 `$SITE_STATUS` 에 Type 필드 추가를 재협의.
- 그전까지는 제어가 이 응답을 "PC2 전체 상태" 로 해석하지 않도록 주의해야 한다.

상태: PENDING(제어 재협의 대기)

---

## 갱신 규칙

- 실기 확인 후 `result: [pending]` 을 `result: PASS` / `result: FAIL — 사유` 로 바꾼다.
- FAIL 이면 신규 phase 또는 quick 으로 이월하고 그 링크를 여기에 남긴다.
- H1 은 실측 수치를 반드시 함께 적는다. 수치 없는 "효과 있음" 기재 금지(D-73-06).

---

## 별도 확인 사항 — BOTTOM `SHOT_E5` 티칭 (2026-08-27)

Phase 73 과 무관하나 TOP/BOTTOM 복원 검증 중 드러나 기록한다.

**증상:** 크로스-Z 측정 `E5_P1`/`E5_P2` 가 회차마다 결과가 갈린다.
```
10:27 사이클 → F   [FitLine] ok 0/20 (noEdge 20)
                   'E5_P2' failed: insufficient edge points (0) across 20 strips
10:30 사이클 → P   DualImageEdgeDistance×2 정상, 에러 로그 없음
```

**원인:** 사용자 확인 — **E5 티칭 문제**. 에지 검출이 경계에 걸쳐 있어 간헐적으로 실패한다.

**Phase 73 과 무관한 근거:**
- 레시피 설정이 08-14 백업과 **한 글자도 다르지 않음**
  (`ZIndexA=23 / ZIndexB=24 / TeachingImagePath_Horizontal / _Vertical` 전부 동일)
- 크로스-Z 조합 로직 자체는 정상 작동(`DualImageEdgeDistance×2` 실행 확인)
- 이번 세션 초반에도 같은 항목이 오설정으로 지적된 바 있음(그때 "나중에" 로 보류)

**조치:** E5 재티칭 필요. 별도 작업으로 처리.

**상태: PENDING**
