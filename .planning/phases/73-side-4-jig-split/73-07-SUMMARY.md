# 73-07 SUMMARY — 최종 검증

**상태:** COMPLETE (Task 1~3 + SIMUL 실기 검증)

## Task 1 — 정적 대조 21항목

전부 통과. 미일치 1건(`CollectBusySiblingChannels` want 2 / got 3)은 D-73-09 가 지시한
"조건부 안전" 설명 주석이라 지우지 않음 — 코드 참조는 정확히 2건.

코딩 규칙 전수(추가 라인 684줄): `?:` / `??` / `?.` / switch expression **0건**.
`#pragma warning` / `NoWarn` 추가 0건, `[Obsolete]` 삭제 0건.

빌드: SIMUL-ON **18줄**(CS0618×16 + CS0162×2) / SIMUL-OFF **16줄**(CS0618×16), error 0.

## Task 2 — SIMUL 실기 검증 (사람 확인)

| # | 항목 | 결과 |
|---|---|---|
| S1 | RUN 회귀 | PASS — `SIDE_1·3-1_D1` 측정 2 / 이탈 2 (baseline 일치) |
| S2~S5 | Type 2/3/4/5 개별 | PASS — 지그별 개별 P/F 성립 |
| S6 | 4연속 × 5회 | PASS — 매회 **측정 25 / 이탈 7** |
| S8 | 예외 입력 4종 | PASS — 전부 FAIL 응답, **무응답 0건** |
| S9-1 | 범위 밖 z | PASS — `$PREP:2,2,7@` OK → `$TEST` **B**(P 아님), Error 로그 `범위 밖 z_index 수신 — z=7, 최대 z=2` |
| S9-2 | Top/Bottom 회귀 | PASS — `seq=TOP`/`seq=BOTTOM` 분리, TOP 조명 `ALIGN_COAX` 1건 / BACK·RING 0건, BOTTOM 조명 0건(정상) |
| S7 | 조명 FAIL | **미수행** — `73-HUMAN-UAT.md` 로 이월 |

**지그별 개별 P/F 실측 (2026-08-27 10:33, 레시피 재구성 후)**
```
SIDE_1(Type2) z=0~2 → F     SIDE_2(Type3) z=0~3 → P
SIDE_3(Type4) z=0~4 → F     SIDE_4(Type5) z=0~3 → F
Shot 8 / 측정 25 / 이탈 7   ← baseline(2026-08-26 09:17) 완전 일치
```
Shot별 개수까지 baseline 과 동일. 예전엔 4개가 뭉뚱그려져 통째로 F 였으나
이제 **SIDE_2 만 P** 로 나와 어느 지그가 불량인지 구분된다.

## Task 3 — 문서·버전

- `73-HUMAN-UAT.md` 신설 — 실기 필요 H1~H6 + 알려진 제약 K1(조명 잔광)·K2(`$SITE_STATUS`)
- 버전 **1.7.25.0**, `BUILD_DATE=2026-08-26`
- ROADMAP Phase 73 완료 반영

## 코드 리뷰 (오케스트레이터가 별도 실행)

`73-REVIEW.md` — **blocker 0 / warning 5**. 지정 6항목 전부 통과.
그중 2건을 이번에 수정:
- **WR-02** `$PREP@`(내용 없음) 무응답 경로 제거 — `$ALIVE`/`$RESET` 과 같은 예외 처리 추가.
  무응답이면 PLC 가 ACK 무한 대기(라인 정지)한다. 커밋 `20f0bf3`
- **WR-01** `ResourceMap` 주석이 코드와 반대로 적혀 있던 것 정정(동작 무변경). 커밋 `20f0bf3`

WR-03(TCP `$TEST` 상호배타 게이트 부재) / W8(COAX 잔광) / W10(`$SITE_STATUS`)는
`73-HUMAN-UAT.md` 로 이월.

## 이 phase 범위 밖이나 함께 처리한 것

| | 내용 | 커밋 |
|---|---|---|
| SHOT `SimulImagePath` 화면 표시 | `882010e`(3주 전)가 숨긴 것을 사용자 확인 후 원복 | `ead0aea` |
| 레시피 TOP/BOTTOM 복원 | 구 버전(6월 계열) 유입으로 z 전부 0·설정 리셋된 것을 08-14 백업에서 복원 | (레시피 파일) |
| Phase 74/75 신설 | 브러시 마스킹 / Align 보정 이미지 저장 | `f21f4dc`, `504fe19` |

**레시피 복원 상세** — 자세한 경위는 `73-RECIPE-RESTORE.md` 참조.
