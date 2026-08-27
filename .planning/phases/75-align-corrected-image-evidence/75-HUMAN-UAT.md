# Phase 75 — 실기 확인 (HUMAN UAT)

**상태:** 사용자 회신 대기
**버전:** 1.7.26.0 (2026-08-27)
**빌드:** SIMUL-ON 에러 0 / 경고 18줄 · SIMUL-OFF 에러 0 / 경고 16줄 — 둘 다 baseline 유지

각 항목에 `PASS` / `FAIL(증상)` / `PENDING` 중 하나를 기록한다.

---

- [ ] **U-1 설정이 보이는가** — PENDING

설정 창(PropertyGrid) → `Path|AlignVerify` 그룹에 5개 항목이 보이는가.
- `AlignVerifySavePath` 기본값이 `D:\Data\AlignVerify` 인가
- `AlignVerifyKeepDays` = 180, `AlignVerifyImageKeepDays` = 30 인가
- 임계 2종(`AlignVerifyResidualLimitMm` / `AlignVerifySeatLimitMm`)이 **0** 인가

→ 구 `Setting.ini` 로 시작해도 경로가 빈칸/0 이 되지 않아야 한다.

---

- [ ] **U-2 ① 이 기록되는가 (정상 건 포함)** — PENDING

Align 을 몇 번 돌린 뒤 `D:\Data\AlignVerify\<오늘날짜>.csv` 를 연다.
- 헤더가 20컬럼인가
- `구분 == ALIGN` 행이 **정상 건에도** 쌓였는가 (D-75-01)
- `잔여OffsetXmm` / `잔여OffsetYmm` / `잔여ThetaDeg` 에 숫자가 들어 있는가
- 값이 대략 0 근처인가 (크게 튀면 ① 자체 또는 캘리브를 의심한다)

---

- [ ] **U-3 ② 가 기록되는가 (지그 구분 포함)** — PENDING

검사를 몇 사이클 돌린 뒤 같은 CSV 에서
- `구분 == SEAT` 행이 있는가
- `시퀀스` 열에 `TOP`/`BOTTOM` 또는 `SIDE_1`~`SIDE_4` 가 **지그별로 구분되어** 나오는가
- `검출Row/Col` 과 `기준Row/Col` 이 둘 다 채워져 있는가
- `해상도mmPerPx` 가 0 이 아닌가 (0 이면 SHOT 매칭 실패 — `75-04-SUMMARY.md` 참조)

---

- [ ] **U-4 NG 일 때만 이미지가 생기는가 (D-75-04)** — PENDING

- 정상 Align 을 여러 번 돌린 뒤 `D:\Data\Result\AlignVerify\<yyMMdd>\` 가 **비어 있는지** 확인
- 일부러 NG(패턴을 가리거나 미티칭 슬롯 호출)를 만든 뒤 같은 폴더에
  `aligncorr_...jpg` 또는 `alignraw_...jpg` 가 **1장** 생기는지 확인
- ⚠ 이 확인 동안 **작업관리자에서 메모리 추이를 같이 본다.** 계단식으로 계속 오르면 즉시 보고

---

- [ ] **U-5 조회 화면이 읽히는가** — PENDING

결과 리뷰어 → **[Align 정합 조회]** → 자재번호 입력 → [조회]
- ① / ② 숫자가 보이는가
- 임계가 0 이므로 **정상/벗어남 판정 문구가 안 나오는가** (나오면 결함이다)
- 하단에 "SIDE 는 깊이 방향 미검증" 한계 문구 2줄이 보이는가
- 최근 N개 추세와 시퀀스별 표가 채워지는가

---

- [ ] **U-6 회귀가 없는가 (택트 포함)** — PENDING

- 평소 검사 1사이클을 그대로 돌려 PLC 응답(P/F)이 이전과 같은지
- 통계 화면과 CPK 엑셀 export 가 이전과 동일하게 열리는지
  (기존 측정이력 CSV 를 건드리지 않았다는 확인)
- 🔴 **택트 실측** — 알고리즘 로그에서 `[ALIGN_VERIFY] ... reused=True elapsed=NNms` 줄을 찾아
  `elapsed` 값을 여러 건 확인한다. ① 은 **PLC 응답이 나가기 전에 동기로** 도는 코드라
  이 숫자가 곧 사이클에 얹힌 지연이다.
  - `reused=True` 로 나오는가 (False 면 1차 검출을 낭비로 다시 돈 것)
  - `elapsed` 가 수십 ms 수준인가. **100ms 를 넘으면 기록하고 보고한다** —
    PLC 가 응답 후 22ms 만에 다음 요청을 보내는 이슈가 있어 사이클 조기종료로 이어질 수 있다
  - Align 을 연속으로 여러 번 돌렸을 때 PLC 쪽 거부/재시도가 늘지 않았는지 같이 본다

---

## 회신 형식

`U-1: PASS` / `U-4: FAIL — (증상)` 형태로 알려 주시면 된다.
전부 통과면 `전부 PASS` 한 마디로 충분하다.
