# Phase 76 — SIDE Datum 세로선 끄기 옵션 실기 확인 (HUMAN UAT)

**상태:** 사용자 회신 대기
**버전:** 1.7.41.0 (2026-09-11)
**빌드:** SIMUL-ON(Debug|x64) 에러 0 / 경고 `CS0169 CS0618`만 · SIMUL-OFF(DefineConstants=TRACE;DEBUG) 에러 0 / 경고 `CS0169 CS0618`만 — 둘 다 76-01 계획 시점 기준값 유지, 신규 경고 종류 0

각 항목에 `PASS` / `FAIL(증상)` / `PENDING` 중 하나를 기록한다.

---

## 실행 환경

- **이 PC(SIMUL) 에서 먼저 할 수 있는 것:** U-1 전체, U-3 의 Test Find 부분(로그 확인 제외), U-6 의 Test Find·Datum 선택 화면, U-8.
- **SIDE PC 에서 반드시 해야 하는 것:** U-2 ~ U-7 (실제 부품·실제 카메라 필요).
- **새 빌드 버전:** 1.7.41.0 (`WPF_Example/VersionDefine.cs`).
- **레시피 안전:** SIDE PC 에서 옵션을 켜 볼 때는 먼저 레시피 복사본에서 `IsVerticalLineDisabled` 를 켜고 확인할 것을 권한다. 운영 레시피(`D:\Data\Recipe`) 저장은 확인이 끝난 뒤 사용자가 직접 한다 — 이 UAT 절차 자체는 운영 레시피를 건드리지 않는다.
- 이 빌드 전 레시피(옵션 키가 없는 옛 레시피)를 열어도 모든 SIDE Datum 의 옵션은 꺼진 상태로 보여야 한다(하위 호환, D-76-01).

---

- [ ] U-1. 옵션 노출·저장·옛 레시피 — PENDING

(SDV-01, D-76-01)
- `Side_Datum_1` ~ `Side_Datum_4` 속성창의 `Datum|Algorithm` 그룹에 `IsVerticalLineDisabled` 가 보이는가.
- `Top_Datum` / `Bottom_Datum` 속성창에는 이 항목이 보이지 않는가(알고리즘이 달라 자동으로 숨음).
- SIDE Datum 하나만 켜고 저장한 뒤 레시피를 다시 불러오면 그 Datum 만 켜져 있고 나머지 SIDE Datum 은 꺼진 채로 남아 있는가.
- 이 빌드 전(76-03 이전)에 저장된 레시피를 열면 모든 SIDE Datum 의 옵션이 꺼져 있는가(키 부재 시 false).

결과:

---

- [ ] U-2. 옵션 OFF 기준값 — PENDING

(SDV-04) SIDE PC 필수.
- 옵션을 전부 OFF 로 둔 채 SIDE 1사이클을 돌리고, 25개 측정값을 전부 적는다.
- 각 SIDE Datum 의 Test Find 성공 창에 뜨는 '검출 원점' (row, col) 을 Datum 별로 적는다.

결과:

---

- [ ] U-3. 옵션 ON 동등성과 로그 — PENDING

(SDV-02, D-76-04, D-76-05) SIDE PC 필수(Test Find 부분은 SIMUL 도 가능하나 로그·비교는 SIDE PC 필요).
- U-2 와 같은 부품을 그대로 두고 옵션을 ON 으로 바꿔 1사이클 돌린다. 25개 측정값이 U-2 값과 반복성 범위 안에 있는가.
- 각 Datum 의 Test Find '검출 원점' 의 col 값이 U-2 때의 교점 col 값과 몇 px 이내인가(크게 다르면 그대로 적어 둔다 — 패턴 기준점과 티칭 원점이 서로 다른 이미지에서 잡혔을 가능성이 있다).
- `D:\Data\Algorithm` 의 오늘 로그에서 Datum 이름과 함께 `[Datum.Vertical] skipped` 와 `[Datum.HorizontalOnly] ok` 줄이 보이는가.

결과:

---

- [ ] U-4. 세로 흐림 내성 — PENDING

(SDV-02, D-76-02) SIDE PC 필수.
- 세로 이미지(ZIndexB)를 일부러 흐리게 만든다(초점을 일부러 이탈시킨다).
- 옵션 ON 인 Datum 은 Datum OK 로 사이클을 끝까지 완주하는가.
- 옵션 OFF 인 Datum 은 이전과 같이 `ALIGN_FAIL`(Vertical 오류)로 실패하는가(회귀 없음 확인).

결과:

---

- [ ] U-5. 좌우 이동 추종 — PENDING

(SDV-02, D-76-05) SIDE PC 필수.
- 부품을 좌우로 밀어서 다시 안착시킨 뒤 사이클을 돌린다.
- 옵션 ON 에서 측정 ROI(결과 화면의 초록 박스)가 부품을 따라 옮겨 가는가, 측정값이 안정적인가.
- **A-76-E1 관찰:** 패턴매칭이 좌우로만 틀리게 매칭되는 경우, 옵션 ON 에서는 세로선이라는 독립 교차검증이 더 이상 없어 이를 걸러내지 못한다. 이런 상황이 관찰되면 기록해 둔다.

결과:

---

- [ ] U-6. 표시 — PENDING

(SDV-03, D-76-06) 결과 화면·Test Find 는 SIMUL 가능, 저장 캡처 확인은 SIDE PC 권장.
- 옵션 ON Datum 의 결과 화면, Test Find 결과, Datum 노드 선택 화면, 저장 캡처 JPEG 모두에서 세로 기준선, 노랑 세로 검출선, 주황 세로 에지점, 원점 십자의 세로 팔이 안 보이는가.
- 같은 화면들에서 가로 기준선과 가로 관련 요소는 그대로 보이는가.
- 옵션 OFF Datum 과 TOP/BOTTOM Datum 은 이전과 똑같이 보이는가(회귀 없음).
- **A-76-E2 해석 확인:** 티칭 원점 표시(마젠타/빨강 십자)와 세로 ROI 검색 사각형은 이번 phase 에서 의도적으로 남겨 두었다(설정·티칭 기준 표시로 판단). 이것까지 숨겨야 한다고 판단되면 `FAIL` 로 적고 이유를 남긴다.

결과:

---

- [ ] U-7. 회귀 — PENDING

(SDV-04, D-76-03) SIDE PC 필수.
- TOP·BOTTOM 1사이클의 PLC 합부 판정과 측정값이 이전과 동일한가.
- SIDE 옵션 ON 상태에서도 z index 순서와 PLC 응답의 Datum index 가 그대로인가.
- 저장 폴더에 SIDE 세로(ZIndexB) 이미지가 옵션 ON 상태에서도 계속 저장되는가.

결과:

---

- [ ] U-8. 매칭 없으면 OK 아님 — PENDING

(SDV-02, PR-2) SIMUL 가능.
- 옵션 ON 인 Datum 에서 패턴이 맞지 않게 한 상태로 Test Find 를 돌리면 'Find 실패'로 나오는가(자동 사이클이면 `ALIGN_FAIL`).
- 가능하면 `IsPatternAlignEnabled` 를 잠시 끄고 옵션 ON 상태로 Test Find 를 돌려 'requires pattern align' 문구로 실패하는가.

결과:

---

## 알려진 한계

1. 티칭은 여전히 세로선이 필요하다(D-76-07). 처음부터 세로 이미지를 쓸 수 없는 설비는 이번 phase 범위 밖이다.
2. 세로 기준각이 없다(`DetectedRefAngle2 = 0.0`). 옵션 ON Datum 을 참조하는 X축(`MeasureAxis=X`) 측정은 쓰지 않는다 — `EdgeToLineDistance` 는 가상 수직선으로 폴백하지만 `CircleCenterDistance`, `CompoundCenterBDistance`, `CompoundCenterCDistance`, `ArcEdgeDistance`, `ArcLineIntersectDistance` 는 0.0(가로 방향)을 X 기준으로 써서 틀린 값을 낸다. 현재 SIDE 레시피의 X축 측정은 0건이다.
3. 패턴 false match 교차검증 상실 — A-76-E1. 패턴매칭이 좌우로만 틀리게 매칭되면 옵션 OFF 시절과 달리 세로선이 이를 걸러 주지 않는다.
4. 표시 해석 — A-76-E2. 옵션 ON 상태로 티칭하면 세로 검출선이 화면에 안 보이므로, 티칭 결과의 세로선을 눈으로 확인하려면 옵션을 잠시 끄고 확인한다.
5. 오프라인 이미지 자동 채움 기능은 옵션 ON 상태에서 Datum 이 OK 이면 흐린 세로 이미지도 그대로 저장한다(의도된 동작 — D-76-03, 초점 분석용 보존).
6. Phase 75 의 SEAT 기록(AlignVerify CSV)의 검출Col 값은, 옵션 ON Datum 에서는 세로선 교점이 아니라 패턴매칭으로 옮긴 티칭 원점 열이다.

---

## 회신 형식

`U-1: PASS` / `U-4: FAIL — (증상)` 형태로 알려 주시면 된다.
나중에 확인할 항목은 `U-n: 보류` 로 알려 주시면 PENDING 그대로 남긴다.
전부 통과면 `전부 PASS` 한 마디로 충분하다.
