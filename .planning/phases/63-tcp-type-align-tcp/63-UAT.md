---
status: complete
phase: 63-tcp-type-align-tcp
source: [63-01-SUMMARY.md, 63-02-SUMMARY.md, 63-03-SUMMARY.md, 63-04-SUMMARY.md, 63-05-SUMMARY.md]
started: 2026-06-24T00:00:00
updated: 2026-06-24T00:00:00
---

## Current Test

[testing complete]

## Tests

### 1. V1 $TEST Type 필드 파싱 + RESULT echo
expected: |
  SIMUL_MODE로 앱 실행 후 mock client 에서 V1 포맷 $TEST를 전송했을 때
  Type 토큰이 응답에 그대로 돌아오는지 확인한다.

  전송: $TEST:1,TOP,12345,null,2@  (site=1, Type=TOP, 자재번호=12345, z_index=2)
  예상 응답: $RESULT:1;TOP;P|F|B;count;...@  (Type 토큰이 세미콜론 2번째 자리에 echo)
result: pass
reported: "$RESULT:1;TOP;F;2;FAI_I9=4.896=NG,FAI_I10=0.537=NG@"

### 2. V1 Type 부재(구 포맷) 폴백
expected: |
  Type 필드 없는 구 포맷($TEST:1,12345,null,2@)이나
  Type=null 포맷($TEST:1,null,12345,null,2@)을 전송했을 때
  앱이 크래시·에러 없이 처리하고, RESULT 응답의 Type 자리가 빈값(;;)으로 돌아온다.

  전송: $TEST:1,null,12345,null,2@
  예상 응답: $RESULT:1;;P|F|B;count;...@  (Type 자리 빈값 — 인덱스 어긋남 없음)
result: pass
reported: "$RESULT:1;;F;2;FAI_I9=4.896=NG,FAI_I10=0.537=NG@"

### 3. $ALIGN_TEST 수신 → ALIGN_RESULT 응답
expected: |
  Align 테스트 커맨드를 전송했을 때 ack 응답이 돌아온다.

  전송: $ALIGN_TEST:TRAY@
  예상 응답: $ALIGN_RESULT:TRAY;P;@  (target echo + Pass ack, 항목 없음 — Phase 62 연계 전 골격)

  전송: $ALIGN_TEST:BOTTOM@
  예상 응답: $ALIGN_RESULT:BOTTOM;P;@
result: pass
reported: "$ALIGN_RESULT:TRAY;P;@ / $ALIGN_RESULT:BOTTOM;P;@"

### 4. $ALIGN_CALIB 수신 → ALIGN_CALIB 응답
expected: |
  Align 캘리브 커맨드를 전송했을 때 ack 응답이 돌아온다.

  전송: $ALIGN_CALIB:TRAY@
  예상 응답: $ALIGN_CALIB:TRAY;P@

  전송: $ALIGN_CALIB:BOTTOM@
  예상 응답: $ALIGN_CALIB:BOTTOM;P@
result: pass
reported: "$ALIGN_CALIB:TRAY;P@ / $ALIGN_CALIB:BOTTOM;P@"

### 5. v2.6 회귀 — UseProtocolV1=false 경로 무변경
expected: |
  Setting.ini 에서 UseProtocolV1=false 로 설정(또는 기본값) 후 앱을 재시작하여
  기존 v2.6 포맷이 여전히 정상 작동하는지 확인한다.

  전송: $TEST:1,12345,null,2@  (v2.6 포맷 — Type 필드 없음)
  예상: 기존과 동일하게 처리됨 (RESULT 응답 포맷에 Type 자리 없음 — v2.6 직렬화 경로)
result: pass
reported: "$RESULT:1;12345;B;0;@"

## Summary

total: 5
passed: 5
issues: 0
pending: 0
skipped: 0

## Gaps

[none yet]
