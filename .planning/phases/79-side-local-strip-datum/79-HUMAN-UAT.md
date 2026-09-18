# Phase 79 — 핀 옆 띠 기준(Local Ref) UAT

**상태:** 사용자 확인 대기
**버전:** 1.7.50.0 (2026-09-18, `WPF_Example/VersionDefine.cs`)
**빌드:** **Debug|x64 전용.** Release|x64 는 `OutputPath` 가 `D:\Data\` 라 빌드만 해도 현장 배포 exe 를 덮어쓴다 — 이 UAT 절차 전체가 Release 빌드를 요구하지 않는다. 배포는 앱 종료 확인 후 사용자 승인으로 별도 진행한다(P-15).

**옵션을 켜면 0점이 바뀌어 값이 달라집니다.** 켠 측정마다 U-6 에서 기준값(NominalValue)·공차를 새 값 기준으로 확인·결정해야 합니다 — 코드는 자동으로 바꾸지 않습니다(O-79-05).

각 항목에 `PASS` / `FAIL(증상)` / `PENDING` / `대기` 중 하나를 기록한다.

---

## 실행 환경

- **이 PC(사무실, SIMUL) 에서 할 수 있는 것:** U-1 ~ U-6, U-8 — 09-17 A·B 실사진(오프라인)과 SIMUL TCP 모의 클라이언트로 확인한다.
- **장비 PC 에서만 할 수 있는 것:** U-7(TOP·BOTTOM, 사무실 PC 엔 TOP·BOTTOM 사진이 없음 — A-79-A3), 그리고 가능하면 U-6 의 실물 A·B 재검사.
- 배포(Release|x64 빌드, `D:\Data` 덮어쓰기) · **운영 레시피(`D:\Data\Recipe\FAI_1\main.ini`) 적용은 이 체크포인트 범위 밖**이다 — UAT 합격 뒤 사용자 결정으로 별도 진행한다.

---

## 공통 준비 (U-1~U-6, U-8 시작 전 1회)

1. **빌드:** `WPF_Example/DatumMeasurement.csproj` 를 **Debug|x64** 로 빌드한다(SIMUL_MODE 활성).
   ```
   "/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -m -nologo -v:minimal -clp:ErrorsOnly
   ```
   Release|x64 는 빌드하지 않는다.
2. **시험 레시피 복사(사용자 작업):** `D:\Data\Recipe\FAI_1` 폴더를 통째로 복사해(예: `D:\Data\Recipe\FAI_1_UAT79`) 시험용으로 쓴다. **운영 레시피(`D:\Data\Recipe\FAI_1\main.ini`) 는 이 절차 중 직접 열어 수정하지 않는다.** 앱의 레시피 불러오기(Load)가 이 시험 복사본만 가리키게 한다. 운영 레시피 적용은 UAT 합격 뒤 사용자 결정이다(P-15).
3. **로그 위치:** 앱 로그 패널의 `Algorithm` 탭(파일: `D:\Data\Algorithm\...`)과 `Error` 탭을 열어 둔다. `[LocalRef]` 로그는 전부 `Algorithm` 탭(찾음/계산 요약)과 `Error` 탭(기준 ROI 실패·전환)에 찍힌다.
4. **대상:** 시험 레시피의 SIDE_1 시퀀스, Shot `SIDE_SHOT_1_C13-14`, 측정 6개(C13_P1, C14_P1, C13_P2, C14_P2, C13_P3, C14_P3) — 전부 `EdgeToLineDistance`, `DatumRef=Side_Datum_1`.
5. **TCP 송신 도구(U-2, U-8 용):** `Test/mock_vision_client.py` 또는 `Test/HandlerCommunicationTest.py`. 메시지 형식(v1.0, 포트 7701, PC2=SIDE_1, Type=2):
   - 조명 준비: `$PREP:1,2,<z번호>@`
   - 검사 트리거: `$TEST:1,2,BJWC73.20@`
   - `SIDE_SHOT_1_C13-14` 는 `ZIndex=3`~`ZIndexEnd=9`(범위 7장) 이므로, z3부터 z9까지 순서대로 `$PREP`→`$TEST` 를 7번 반복해야 마지막 tick(z9)에서 최종 판정이 나온다(Phase 77 범위 Shot 프로토콜과 동일).
6. **09-17 실사진(오프라인 준비, 사용자 작업 — `D:\Data` 아래 복사이므로 실행자는 하지 않는다):** U-2·U-6 에서 실제 사진으로 사이클을 돌리려면 —
   - **기준점(z1) 사진:** Side_Datum_1 의 `TeachingImagePath`(시험 레시피 복사본)를 09-17 A1 사이클의 z1 사진(`D:/Data/Result/Image/260917/1738/original/datum_SIDE_1_Side_Datum_1_H_173855718.jpg`)으로 교체한다.
   - **핀(z3~z9) 사진:** `OfflineInspectMode` 를 켜고 `<ImageSavePath>\OfflineInspect\<시험 레시피 이름>\shot_SIDE_SHOT_1_C13-14_z3.bmp` ~ `_z9.bmp` 7개 파일에 A1 사이클의 origin 사진 7장(realab 출력 `realab_cycle label=A1 ...` 줄의 z3~z9 파일명, 전부 `D:/Data/Result/Image/260917/1739/original/` 아래)을 bmp 로 변환해 채운다. B 사이클(B1)을 쓰려면 `_M2_` 접두 파일로 같은 방식.
   - 자세한 오프라인 z 준비 방식은 `.planning/phases/77-side-z-focus-select/77-HUMAN-UAT.md` U-2 준비를 참고한다(같은 메커니즘).

---

## 추천 티칭 표 (realab 결과, `$H/realab.txt` `realab_pick`)

realab 은 09-17 자재 A(4사이클)·B(3사이클)의 z1 사진에서 핀 옆 띠 후보 창 2개씩(총 6창) × 에지 조합 8개를 조사해, **찾음 7/7 이고 같은 자재 안 흔들림(A−B 값 미사용)이 가장 작은 창**을 추천했다(O-79-10, T-79-20). 에지 설정은 전부 `LocalRefSigma=1.0`, `LocalRefEdgeSampleCount=20`, `LocalRefEdgeTrimCount=10`, `LocalRefEdgeSelection=Strongest`, `LocalRef_Phi=0` 고정이다.

| 측정 | 추천 창 | LocalRef_Row | LocalRef_Col | LocalRef_Length1 | LocalRef_Length2 | LocalRefEdgeDirection | LocalRefEdgePolarity | LocalRefEdgeThreshold |
|---|---|---|---|---|---|---|---|---|
| C13_P1 | stripL2 | 6590.0 | 1850.0 | 110.0 | 90.0 | BtoT | LightToDark | 10 |
| C14_P1 | stripL | 6590.0 | 1490.0 | 110.0 | 90.0 | BtoT | LightToDark | 10 |
| C13_P2 | stripM (★아래 주의) | 6550.0 | 6990.0 | 170.0 | 90.0 | TtoB | DarkToLight | 10 |
| C14_P2 | stripM (★아래 주의) | 6550.0 | 6990.0 | 170.0 | 90.0 | TtoB | DarkToLight | 10 |
| C13_P3 | stripR | 6510.0 | 12690.0 | 110.0 | 90.0 | BtoT | LightToDark | 10 |
| C14_P3 | stripR2 | 6510.0 | 13040.0 | 110.0 | 90.0 | BtoT | LightToDark | 10 |

**P2(C13_P2·C14_P2) 참고(A-79-A2):** RESEARCH Wave 0 우려와 달리 이번 realab 는 stripM·stripM2 모두 찾음 7/7 조합을 찾았다(`재조사 필요` 아님). 다만 아래 A−B 표에서 보듯 **P2 는 국부 기준을 켜도 A−B 가 오히려 나빠진다** — "찾음" 과 "효과" 는 별개다. U-5 에서 P2 는 **켜지 않는 것을 권고**한다.

**티칭 순서(A-79-A5):** 위 표 값을 측정의 `Local Ref|ROI` 카테고리(`LocalRef_Row/Col/Phi/Length1/Length2`)와 `Local Ref|Edge` 카테고리(`LocalRefEdgeThreshold/LocalRefSigma/LocalRefEdgeSampleCount/LocalRefEdgeTrimCount/LocalRefEdgePolarity/LocalRefEdgeDirection/LocalRefEdgeSelection`)에 숫자로 입력 → `Local Ref|Option` 의 체크박스(`IsLocalRefEnabled`, "국부 기준 사용 (핀 옆 띠)")를 켠다 → 측정 노드를 다시 누르면 캔버스에 `<FAI>_<측정>_LocalRef` 상자가 나타난다 → 필요하면 캔버스에서 끌어 미세 조정한다.

---

## 전역·국부 A−B 비교 표 (realab_meas, µm)

realab 은 6측정 × 자기 핀의 창 2개(측정마다 12행)로 국부값과 운영 전역값(cycle.json `LastMeasuredValue`)을 나란히 냈다. **추천 창은 A−B 로 고르지 않았다(같은 7사이클로 고르고 평가하면 좋게만 보인다) — 최종 선택은 U-5.**

| 측정 | 창 | 찾음 | 전역 A−B(µm) | 국부 A−B(µm) | 전역 흔들림A/B(µm) | 국부 흔들림A/B(µm) |
|---|---|---|---|---|---|---|
| C13_P1 | stripL | 7/7 | 3.3 | **3.3** | 2.7 / 1.3 | 2.8 / 0.5 |
| C13_P1 | **stripL2(추천)** | 7/7 | 3.3 | **39.4 ⚠** | 2.7 / 1.3 | 2.8 / 1.4 |
| C14_P1 | **stripL(추천)** | 7/7 | -0.5 | **-0.8** | 2.3 / 1.8 | 2.5 / 1.5 |
| C14_P1 | stripL2 | 7/7 | -0.5 | 34.0 ⚠ | 2.3 / 1.8 | 3.1 / 0.3 |
| C13_P2 | **stripM(추천)** | 7/7 | -10.7 | **13.0 ⚠ 악화** | 2.1 / 2.3 | 6.2 / 0.9 |
| C13_P2 | stripM2 | 7/7 | -10.7 | 13.7 ⚠ 악화 | 2.1 / 2.3 | 3.8 / 6.4 |
| C14_P2 | **stripM(추천)** | 7/7 | -5.1 | **18.7 ⚠ 악화** | 2.0 / 2.1 | 6.0 / 0.9 |
| C14_P2 | stripM2 | 7/7 | -5.1 | 19.4 ⚠ 악화 | 2.0 / 2.1 | 3.6 / 6.3 |
| C13_P3 | **stripR(추천)** | 7/7 | 47.9 | **9.5** | 3.0 / 3.6 | 4.8 / 2.5 |
| C13_P3 | stripR2 | 7/7 | 47.9 | -6.8 | 3.0 / 3.6 | 4.3 / 6.2 |
| C14_P3 | stripR | 7/7 | 47.3 | 19.0 | 3.5 / 4.5 | 9.3 / 1.7 |
| C14_P3 | **stripR2(추천)** | 7/7 | 47.3 | **2.7** | 3.5 / 4.5 | 7.6 / 3.5 |

**정직하게 보고합니다(project_constraints):**
- **P1·P3 는 개선 또는 유지.** C13_P3(47.9→9.5µm)·C14_P3(추천 stripR2, 47.3→2.7µm) 는 09-17 분석("+2.8/+4.0µm 수준") 과 같은 방향으로 뚜렷이 좋아졌다. C13_P1/C14_P1 은 원래 전역도 좋았고(±3.3, -0.5µm) 국부(stripL 계열)도 비슷하게 유지된다.
- **⚠ C13_P1 의 추천 창(stripL2) 은 형제 창(stripL) 보다 훨씬 나쁘다(39.4 vs 3.3µm).** 추천 규칙(흔들림 최소 + 핀에 가까운 창)이 두 창의 흔들림이 비슷해(2.8µm 동률) 열 거리로 골랐는데, 이 경우 실제 A−B 는 stripL 이 압도적으로 낫다. **U-5 에서 C13_P1 은 stripL2 대신 stripL 사용을 검토할 것을 강력히 권고한다.**
- **⚠⚠ P2(C13_P2·C14_P2) 는 두 창 모두 국부가 전역보다 A−B 가 더 나쁘다(악화).** 찾음 7/7 은 성립하지만 효과가 없다(A-79-A2) — **U-5 에서 P2 는 켜지 않는 것을 권고**한다.
- C14_P3 는 stripR(19.0)보다 stripR2(2.7, 추천)가 뚜렷이 낫다 — 추천 그대로 사용을 권고한다.
- realab 은 단위 변환(기준점 보정 없이)으로 ROI 를 놓았으므로 실제 앱 값과 조금 다를 수 있다.

---

- [ ] U-1. Local Ref 탭 UI·티칭 방식 (A-79-E1) — PENDING

(LSR-01, D-79-03/04, flagged assumption **A-79-E1**)

목적: 속성창 `Local Ref` 탭 구성과 첫 기준 ROI 를 숫자로 넣는 티칭 방식이 운영자에게 쓸 만한지 사용자가 판단한다.

준비: 시험 레시피에서 `SIDE_SHOT_1_C13-14` → `C13_P3` 측정을 선택한다.

단계:
1. 속성창에 `Local Ref` 카테고리 3그룹이 보이는가: `Local Ref|Option`(체크박스 "국부 기준 사용 (핀 옆 띠)" = `IsLocalRefEnabled`, 기본 꺼짐), `Local Ref|ROI`(`LocalRef_Row`/`LocalRef_Col`/`LocalRef_Phi`/`LocalRef_Length1`/`LocalRef_Length2`), `Local Ref|Edge`(`LocalRefEdgeThreshold`/`LocalRefSigma`/`LocalRefEdgeSampleCount`/`LocalRefEdgeTrimCount`/`LocalRefEdgePolarity`/`LocalRefEdgeDirection`/`LocalRefEdgeSelection`).
2. 옛 레시피(운영 `D:\Data\Recipe\FAI_1\main.ini`, 열람만·저장 금지)를 열어 이 측정을 보면 `IsLocalRefEnabled`=꺼짐, 에지 설정 기본값(Threshold 10 / Sigma 1.0 / SampleCount 20 / Trim 10 / Polarity DarkToLight / Direction TtoB / Selection Strongest)으로 보이는가.
3. 위 "추천 티칭 표" 의 C13_P3 값(창 stripR: `LocalRef_Row`=6510.0, `LocalRef_Col`=12690.0, `LocalRef_Phi`=0, `LocalRef_Length1`=110.0, `LocalRef_Length2`=90.0, `LocalRefEdgeDirection`=BtoT, `LocalRefEdgePolarity`=LightToDark, `LocalRefEdgeThreshold`=10)을 속성창에 숫자로 입력한다.
4. `IsLocalRefEnabled` 를 켠다.
5. 측정 노드를 캔버스에서 다시 누른다(재선택) — `C13_P3_LocalRef` ROI 상자가 핀 옆 띠 위치에 나타나는가.
6. 상자를 끌어 옮기기·모서리로 크기 바꾸기가 되는가. Edit 모드에서 위치를 손보정할 수 있는가.
7. 레시피 저장 후 시험 복사본 `main.ini` 를 메모장으로 열어 `C13_P3` 섹션에만 `IsLocalRefEnabled`/`LocalRef_Row`/`LocalRef_Col`/`LocalRef_Phi`/`LocalRef_Length1`/`LocalRef_Length2`/`LocalRefEdgeThreshold`/`LocalRefSigma`/`LocalRefEdgeSampleCount`/`LocalRefEdgeTrimCount`/`LocalRefEdgePolarity`/`LocalRefEdgeDirection`/`LocalRefEdgeSelection` 13개 키가 새로 생겼는지, 다른 측정 섹션(예 `C14_P3`)은 무변경인지 확인한다.

합격 기준: 1~7 모두 확인.

**A-79-E1 판단 칸(자동 해소 금지):** 탭 구성·숫자 입력 방식이 쓸 만한가? 고칠 점이 있다면 적는다.

결과:

---

- [ ] U-2. 실제 사이클 — C13_P3 만 켬 (tracer 화면 증거, A-79-E2) — PENDING

(LSR-02, D-79-05, flagged assumption **A-79-E2**)

목적: C13_P3 만 켜고 C14_P3 는 끈 채 실제 SIDE 사이클(SIMUL)에서 로그·화면·기록이 기대대로 나오는지, C13_P3 값이 realab A1 국부값(2.2994mm 부근, stripL 대신 stripR 참고면 C13_P3 stripR local≈2.1647A/2.1553B 평균)과 비슷한지 확인한다.

준비: 공통 준비 6번대로 A1 사이클 사진(z1 + z3~z9)을 시험 레시피 경로에 넣는다. C13_P3 만 `IsLocalRefEnabled=true`(U-1 값 그대로), C14_P3 는 꺼짐 그대로 둔다.

단계:
1. 공통 준비 5번 순서로 `$PREP:1,2,3@`→`$TEST`, `$PREP:1,2,4@`→`$TEST`, … `$PREP:1,2,9@`→`$TEST` 를 z3부터 z9까지 순서대로 보낸다.
2. 기준점 검출이 성공하는 시점(사이클 시작, z1 사진 사용)에 `Algorithm` 로그에 `[LocalRef] SIDE_1 · Side_Datum_1: 국부 기준선 1개 계산 (찾음 1 / 못 찾음 0, <N> ms)` 요약 줄 1개와, 그 안에 `[LocalRef] 기준선 찾음 — SIDE_1 · Side_Datum_1 · C13_P3: 중점 row=<F2> col=<F2> 에지세기=<F1>` 줄이 기준점 검출마다(사이클당 1번) 찍히는가.
3. z9 최종 tick 의 결과 그리드에서 `C13_P3` 의 "기준" 칸이 `국부`, `C14_P3` 의 "기준" 칸이 빈칸인가.
4. `C13_P3` 오버레이의 주황 선(`FAI-RefLine`)이 띠 에지 위에 놓이는가.
5. 이 사이클의 `cycle.json`(결과 저장 폴더)에서 `C13_P3` 측정의 `"RefSource": "Local"`, `C14_P3` 측정에 `RefSource` 없음(또는 `null`)을 확인한다.
6. 그날 일자별 CSV 끝 열(`사용기준`)에서 `C13_P3` 행이 `국부`, `C14_P3` 행이 빈칸인가.
7. `C13_P3` 측정값이 realab 의 A1 국부값과 비슷한지 확인하고 차이를 기록한다(완전히 같지 않을 수 있다 — realab 은 단위 변환만 쓰고 앱은 기준점 보정을 함께 쓴다).
8. `C14_P3` 값이 옵션 추가 전(79-01 이전, 또는 `IsLocalRefEnabled` 끈 상태)과 같은지 확인한다(회귀 0).

합격 기준: 2~8 모두 확인.

**A-79-E2 판단 칸:** z1 사진의 띠 위치를 측정 사진 핀과 같은 좌표로 쓰는 전제가 실제 사이클에서도 성립하는가(전역도 이미 이렇게 쓰고 있음 — z1 사진 흔들림은 전역과 동일하게 남는 별개 이슈, CONTEXT 참고)?

결과:

---

- [ ] U-3. 전역 기준선 자동 전환 (A-79-E3) — PENDING

(LSR-03, D-79-06, flagged assumption **A-79-E3**, A-79-A6)

목적: 기준 ROI 를 못 쓰는 상황 3가지에서 값이 안전하게 전역으로 전환되고 사이클이 멈추지 않는지, 원인 문구가 충분한지 확인한다. `C13_P3` 로 이어서 진행한다.

단계:
1. **(a) ROI 삭제:** `C13_P3` 의 `LocalRef_*` 를 캔버스에서 삭제(Delete)하거나 `LocalRef_Length1`/`LocalRef_Length2` 를 0 으로 되돌린 뒤 사이클을 재실행한다 — `Error` 탭에 `[LocalRef] 전역 기준선으로 전환 — SIDE_SHOT_1_C13-14 · C13_P3: 기준 ROI 가 티칭되지 않음 (LocalRef_Length1/Length2 가 0)` 이 찍히는가. 결과 그리드 "기준" 칸이 `국부실패→전역` 인가. 값이 옵션 끈 값과 같은가.
2. **(b) 평평한 곳으로 이동:** `LocalRef_*` 를 다시 U-1 값으로 되돌린 뒤, `LocalRef_Row`/`LocalRef_Col` 을 띠 에지가 없는 평평한 위치로 옮기고 재실행한다 — `Error` 탭에 `[LocalRef] 기준 ROI 실패 — SIDE_1 · Side_Datum_1 · C13_P3: <에지 못 찾음 원인 문구>`(계산 시점) 와 이어서 `[LocalRef] 전역 기준선으로 전환 — SIDE_SHOT_1_C13-14 · C13_P3: <같은 원인 문구>`(주입 시점)가 찍히는가. "기준" 칸이 `국부실패→전역` 인가.
3. **(c) Test Find 재사용 뒤 ROI 변경(A-79-A6):** `LocalRef_*` 를 U-1 값으로 복구하고 Test Find 로 기준점을 1번 잡은 뒤(이때는 `국부` 로 정상 동작해야 함), 기준 ROI 를 조금 옮기고(다시 찾기 없이) 수동 RUN 한다 — `[LocalRef] 전역 기준선으로 전환 — SIDE_SHOT_1_C13-14 · C13_P3: 기준점을 찾은 뒤 기준 ROI 또는 에지 설정이 바뀜 — 기준점을 다시 찾으면 반영됨` 이 찍히는가. "기준" 칸이 `국부실패→전역` 인가.
4. 세 경우 모두 검사가 멈추거나 강제 NG 로 바뀌지 않는가(값·판정이 옵션 끈 경우와 동일).

합격 기준: 1~4 모두 확인.

**A-79-E3 판단 칸(자동 해소 금지):** 원인 문구가 나중에 원인을 찾기에 충분한가? 고칠 점이 있다면 적는다.

결과:

---

- [ ] U-4. 표시·기록 회귀 + 저장 사진 색 (A-79-A4, A-79-A7) — PENDING

(LSR-04/05, D-79-04/07, flagged assumptions **A-79-A4**, **A-79-A7**)

목적: 옵션 꺼진 측정의 "기준" 칸 빈칸 표시와 옛 파일 하위호환, 저장 캡처 사진의 국부 기준선 색(화면 주황 vs 저장 파랑)이 괜찮은지 확인한다.

단계:
1. `C13_P3`/`C14_P3` 모두 `IsLocalRefEnabled` 를 끄고 사이클을 돌린다 — 두 측정 다 "기준" 칸이 빈칸인가(값·판정은 옵션 추가 전과 동일).
2. 리뷰어 창에서 옛 날짜(예: `20260916`) 사이클을 열어 "기준" 열이 빈칸이고 예외 없이 로드되는가.
3. U-2/U-3 에서 만든 새 사이클을 리뷰어로 다시 열어 `국부`/`국부실패→전역` 표시가 그대로 보이는가.
4. U-2 사이클의 저장 캡처 사진(결과 이미지 폴더)을 열어 국부 기준선이 화면에서 본 주황이 아니라 기존 "그 외" 색(파랑)으로 그려져 있는가(`OverlayCaptureRenderer` 는 이번 phase 에서 바꾸지 않았다 — A-79-A7).

합격 기준: 1~4 모두 확인.

**A-79-A4 판단 칸:** 옵션 꺼진 측정의 "기준" 칸을 빈칸(= 기존 전역)으로 둔 표시가 괜찮은가?
**A-79-A7 판단 칸:** 화면(주황)과 저장 사진(파랑)의 색이 다른 것이 괜찮은가, 아니면 후속 quick 으로 저장 사진도 주황으로 맞춰야 하는가?

결과:

---

- [ ] U-5. 켤 측정·창 결정 (O-79-10, A-79-A2, D-79-02) — PENDING

목적: 위 "추천 티칭 표"·"A−B 비교 표" 를 보고 C13·C14 6점 중 실제로 켤 측정과 창을 정한다. 도면의 C13·C14 높이 기준은 핀 옆 띠 면(D-79-02)이므로, 국부 기준으로 잰 값이 도면 치수와 같은 뜻이 된다.

단계:
1. C13_P1: **stripL2(추천)** 또는 **stripL(대안, A−B 3.3µm 로 더 우수)** 중 선택.
2. C14_P1: stripL(추천, 그대로 사용 권고).
3. C13_P2/C14_P2: **켜지 않음(권고, A-79-A2 — 찾음은 7/7 이나 A−B 가 오히려 악화)** 또는 사용자가 위험을 감수하고 켬(창 지정).
4. C13_P3: stripR(추천) 또는 stripR2(대안, 부호 반대 -6.8µm) 중 선택.
5. C14_P3: stripR2(추천, 그대로 사용 권고).
6. 결정한 측정·창을 아래 표에 적는다.

| 측정 | 켬/끔 | 최종 창 |
|---|---|---|
| C13_P1 | | |
| C14_P1 | | |
| C13_P2 | | |
| C14_P2 | | |
| C13_P3 | | |
| C14_P3 | | |

결과:

---

- [ ] U-6. 효과 확인 + 기준값·공차 결정 (A-79-E4, PR-5) — PENDING

(LSR-06, D-79-01, flagged assumption **A-79-E4**)

목적: U-5 에서 켠 측정으로 자재 A·B 사이클을 돌려 전역(옵션 끔) 대비 A−B 가 실제로 줄었는지 확인하고, 켠 측정마다 새 값 기준 기준값·공차를 결정한다.

준비: U-5 에서 켜기로 한 측정마다 최종 창 값을 티칭한다. 장비 PC 라면 실물 자재 A·B 로, 사무실 PC 라면 09-17 A1/B1(또는 여러 사이클) 사진 재검사로 대체한다.

단계:
1. 옵션 끈 상태(전역)로 자재 A·B 사이클을 돌려 값을 기록한다.
2. 옵션 켠 상태(국부)로 같은 자재 A·B 사이클을 돌려 값을 기록한다.
3. 아래 표를 채운다.

| 측정 | 전역 A−B(µm) | 국부 A−B(µm) | 판정(개선/유지/악화) |
|---|---|---|---|
| | | | |

4. 켠 측정마다 새 값(국부 기준) 기준으로 `NominalValue`/`TolerancePlus`/`ToleranceMinus` 를 결정해 적는다(코드가 자동으로 바꾸지 않는다 — O-79-05, prohibition).

| 측정 | 새 NominalValue | 새 TolerancePlus | 새 ToleranceMinus |
|---|---|---|---|
| | | | |

합격 기준: 1~4 모두 기록.

**A-79-E4 판단 칸(자동 해소 금지):** 켠 측정의 A−B 편차가 전역보다 뚜렷이 줄었는가(목표: 09-17 분석 수준 수 µm)? realab 표(위)와 실제 사이클 결과를 함께 보고 판단한다.

결과:

---

- [ ] U-7. TOP·BOTTOM 동작·전환 (장비 PC, backstop, A-79-A3) — 대기

(D-79-09, flagged assumption **A-79-A3**)

목적: 옵션이 SIDE 전용이 아니라 **레시피 티칭만으로 TOP·BOTTOM 의 어떤 EdgeToLineDistance 측정에도 켤 수 있음**(코드 수정 없이)을 실기로 확인한다. 사무실 PC 에는 TOP·BOTTOM 사진이 없어 이 항목은 장비 PC 전용이다.

준비: 예시 후보 — TOP: Shot `SHOT_A1-23-C1-C12` 의 측정 `A1_P1`(`DatumRef=Top_Datum`). BOTTOM: Shot `SHOT_B1-4` 의 측정 `B1_P1`(`DatumRef=Bottom_Datum`). 실제 장비 PC 레시피에서 이름이 다르면 아무 EdgeToLineDistance 측정이나 1~2개 골라도 된다.

단계:
1. 위 측정(또는 대체 측정) 1~2개에 `Local Ref|ROI`·`Local Ref|Edge` 값을 그 측정의 기준점 검출 사진(TOP·BOTTOM 은 `CircleTwoHorizontal` 이 쓰는 1장 사진 — D-79-09 공통 규칙: "그 측정의 기준점이 가로선을 찾은 사진") 위에서 핀 옆 적당한 직선 구조에 맞춰 티칭한다.
2. `IsLocalRefEnabled` 를 켜고 사이클을 돌린다 — "기준" 칸이 `국부` 로 표시되는가, `[LocalRef] 기준선 찾음` 로그가 찍히는가.
3. 기준 ROI 를 삭제하고 재실행 — `국부실패→전역` 전환과 값 불변을 확인한다(U-3 과 같은 패턴).
4. 코드 수정 없이(레시피 티칭만으로) 동작했는지 확인한다.

합격 기준(backstop): 2~4 확인. 어려우면 `대기` 로 남긴다.

결과: 대기

---

- [ ] U-8. 동시 작업 안전성 (edge LSR-04 concurrency) — PENDING

목적: SIMUL TCP 반복 검사 도중 Test Find·기준 ROI 끌기를 해도 앱이 멈추거나 죽지 않는지 확인한다.

단계:
1. `C13_P3`(`IsLocalRefEnabled=true`)를 포함한 SIMUL TCP 반복 검사(공통 준비 5번의 z3~z9 PREP/TEST 사이클)를 계속 돌린다.
2. 사이클이 도는 도중 화면에서 Test Find 를 누르거나 `C13_P3_LocalRef` ROI 를 끌어 옮긴다.
3. 앱이 멈추거나 죽지 않는가.
4. 그 사이클(설정이 바뀐 뒤의 사이클)의 전환 로그 원인이 `기준점을 찾은 뒤 기준 ROI 또는 에지 설정이 바뀜 — 기준점을 다시 찾으면 반영됨` 또는 `이번 사이클에 기준점 가로 사진에서 국부 기준선을 구하지 않음 (Test Find 로 잡아 둔 기준점 재사용 등)` 중 하나로 남는가.

합격 기준: 3·4 확인.

결과:

---

## 알려진 한계

1. **A-79-A1:** 기울기는 전역 각도 유지(P-5 (b)). 국부 기울기(a)가 더 나은지는 미검증.
2. **A-79-A2:** P2(C13_P2·C14_P2)는 창을 찾긴 하지만(찾음 7/7) A−B 효과가 없다(오히려 악화) — U-5 에서 켜지 않는 것을 권고.
3. **A-79-A3:** 사무실 PC 에는 TOP·BOTTOM 사진이 없다(U-7 은 장비 PC backstop).
4. **A-79-A4:** 옵션 꺼진 측정의 "기준" 칸은 빈칸(= 기존 전역) — U-4 에서 사용자 확인.
5. **A-79-A5:** 첫 기준 ROI 는 속성창 숫자 입력으로 만든다(`CommitRectRoi` 미변경). 불편하면 후속 quick.
6. **A-79-A6:** Test Find 로 잡아 둔 기준점을 재사용하는 수동 RUN 은 그 사진의 국부 기준선을 쓴다. 이후 ROI 를 고치면 다시 찾기 전까지 전환(원인 '설정이 바뀜').
7. **A-79-A7:** 화면(주황)과 저장 캡처 사진(파랑)의 국부 기준선 색이 다르다 — `OverlayCaptureRenderer` 는 이번 phase 에서 바꾸지 않았다.
8. **z1 사진 흔들림:** 같은 사이클 z3 사진 대비 최대 ~10µm 어긋남(2026-09-15 분석, PLC 대기시간 테스트 미수행)이 있으나, 전역 기준선도 z1 에서 오므로 이 어긋남은 지금과 똑같이 남는다 — 국부 기준이 새로 더하는 오차는 아니다(CONTEXT 별건).
9. **realab 수치 vs 앱 실측:** realab 은 단위 변환(기준점 보정 없이)으로 ROI 를 놓았으므로 앱에서 실제로 나오는 값과 조금 다를 수 있다(U-2 에서 차이 확인).
10. **C13_P1 추천 창 주의:** 추천 규칙(찾음 7/7 + 흔들림 최소)이 고른 stripL2 는 실제로는 형제 창 stripL 보다 A−B 가 훨씬 나쁘다(39.4 vs 3.3µm) — U-5 에서 stripL 사용을 검토할 것.

---

## 회신 형식

`U-1: PASS` / `U-3: FAIL — (증상)` 형태로 각 항목을 알려 주시면 된다. U-7 은 장비 PC 전이면 `U-7: 대기` 로 남겨도 된다. U-5·U-6 의 결정 표(켤 측정·창·기준값·공차)도 함께 알려 주세요.
U-1~U-6·U-8 전부 통과면 `U-1~U-6, U-8 전부 PASS, U-7 대기` 한 마디로 충분하다.
