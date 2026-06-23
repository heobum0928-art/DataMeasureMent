---
status: complete
phase: 49-protocol-v1-judgment-engine
source:
  - 49-01-SUMMARY.md
  - 49-02-SUMMARY.md
  - 49-03-SUMMARY.md
started: 2026-06-23T00:00:00Z
updated: 2026-06-23T00:00:00Z
---

## Current Test

[testing complete]

## Tests

### 1. 제어 스택 스모크 (앱 기동 + TCP 연결)
expected: UseProtocolV1=true 로 앱을 새로 기동. 시작 오류 없이 메인 창 표시 + TCP 비전 서버(포트 7701)가 핸들러/SIMUL 연결 정상 수락.
result: pass
note: "$SITE_STATUS:1@ → $SITE_STATUS:1,Ready@ 왕복 확인 (2026-06-23, SIMUL). 포트 7701 응답 정상, Phase 49 TcpServer instance 화 회귀 0."

### 2. Datum 샷(z_index=0) 정상 → 빈 응답
expected: $TEST z_index=0 으로 Datum 샷 수신 시 Datum 검출이 정상이면 응답이 `RESULT:site;B;0;` (IsBuffer=true, FAICount=0 빈 응답). byte 단위로 B/0 확인.
result: pass
note: "$TEST:1,1,null,0@ → $RESULT:1;B;0;@ (2026-06-23, SIMUL, Top). 정상 Datum 샷 빈 B 확인 (D-06). (1차 F 는 Side datum 미검출 — Test 3 으로 분류.)"

### 3. Datum 샷(z_index=0) 검출 실패 → 즉시 F
expected: UseProtocolV1=true 에서 $TEST z_index=0 Datum 검출 실패 시 빈 B 가 아니라 즉시 `RESULT:site;F;...` (Result=NG → 직렬화 F). 후속 Index skip 은 핸들러 주도.
result: pass
note: "$RESULT:site;F;0;@ (2026-06-23, SIMUL). Side datum 미검출 상태에서 D-04 즉시 F 경로 wire 확인 (IsBuffer=false, count 0). 정상 Datum(Top)은 B 반환(Test 2 대비) — 실패 시에만 F 로 분기 정상."

### 4. 중간 Index NG 발생 → 응답 B, 사이클 계속
expected: 중간 측정 Index 에서 FAI NG 가 발생해도 응답은 `B`(IsBuffer=true). 사이클이 종료되지 않고 다음 Index 로 계속 진행(NG 는 내부 m_bCycleHasNG 에 누적).
result: pass
note: "$TEST:1,1,null,1@ (TOP SHOT_0 ZIndex=1, 중간) → $RESULT:1;B;35;...다수 NG...@ (2026-06-23, SIMUL). NG 다수(A1/A2/A3/A14~/C1~C12 등)에도 IsBuffer=true→B, 사이클 미종료. 35항목=A1~A23+C1~C12. 불변식(NG 있어도 중간 B) wire 확인."

### 5. 마지막 Index → 종합 P/F 1회
expected: 마지막 Index(z_index 최댓값)에서 사이클 누적 NG 가 있으면 `F`, 전부 OK 면 `P` 를 1회 산출(IsBuffer=false). 종료 판정은 마지막 Index 에서만.
result: pass
note: "$TEST:1,1,null,2@ (TOP SHOT_1+SHOT_11 ZIndex=2, 마지막) → $RESULT:1;F;2;FAI_I9=NG,FAI_I10=NG@ (2026-06-23, SIMUL). 마지막 Index 종합 F 1회(IsBuffer=false). z=1 NG 누적 + z=2 자체 NG(I9/I10) 모두 F 방향 일치. (순수 누적 격리 — z=2 전부 OK인데 z=1 NG만으로 F — 는 데이터상 미격리, 단 중간 B→마지막 F 불변식은 확인됨.)"

### 6. 다음 자재 Index 0 재수신 → 리셋
expected: 한 사이클 종료 후 다음 자재의 $TEST z_index=0 을 다시 받으면 이전 사이클의 NG 가 잔류하지 않는다(ResetCycleState). 직전에 F 였어도 새 사이클은 클린 슬레이트로 시작.
result: pass
note: "직전 사이클이 F(z=2) 로 끝난 직후 $TEST:1,1,null,0@ → $RESULT:1;B;0;@ (2026-06-23, SIMUL). 이전 NG 미잔류, 깨끗한 빈 B 로 새 사이클 시작 = ResetCycleState 동작 확인."

### 7. UseProtocolV1=false 회귀 (v2.6 무변경)
expected: UseProtocolV1=false 로 두면 기존 v2.6 전체-Shot 집계/응답 포맷이 그대로 동작(B/P/F 사이클 엔진 미적용). Phase 48 이전과 동일한 응답.
result: pass
note: "UseProtocolV1=false 재시작 후 $TEST:1,1,BJWC73.20@ → $RESULT:1,1,F,0.000,0.000,0.000@ (2026-06-23, SIMUL). v2.6 옛 포맷(쉼표 구분 site,testtype,판정,값들 — 세미콜론/FAI리스트/Buffer 사이클 없음) 그대로 동작 = 회귀 0. (이 PC 는 ServerPort=ServerPortV1=7701 동일 설정이라 포트는 7701 유지, 프로토콜만 전환.)"

### 8. ZIndex 미설정 레시피 + 측정 Index 수신 (WR-01 fix)
expected: 전 Shot ZIndex=0 인 레시피에서 측정 Index(z≥1)를 받으면 — 중간 Index 면 빈 `B` 응답 + 에러 로그에 "[V1Cycle] BuildScopedResponse 빈 결과: ZIndex 매칭 0건" 경고가 남는다. 마지막 Index(매칭 0건)면 `P`(합격)가 아니라 `F`(fail-safe)가 나간다 — WR-01 수정 확증. (운용상 정상은 모든 레시피에 ZIndex 설정.)
result: pass
note: "$TEST:1,1,null,1@ (Top, ZIndex 미설정 레시피=전부 0) → $RESULT:1;F;0;@ (2026-06-23, SIMUL). 매칭 0건 → ComputeLastZIndex=0 → 1>=0 마지막 오인 → WR-01 수정으로 P 아닌 F 강제(count 0). false-PASS 차단 wire 확인. (수정 전이면 P;0; 였을 상황.) 에러 로그 확증: D:/Data/Error/2026-06-23_Error.log — '[V1Cycle] BuildScopedResponse 빈 결과: ZIndex 매칭 0건 (Seq=TOP, z=1, last=0)' (BLOCKER 1 경고 + WR-01 F 동시 동작)."

### 9. 인코딩 회귀 (UTF-8/한글, 49-03 instance화)
expected: UseProtocolV1=true 로 실제 $TEST/$RESULT 송수신 시 한글/UTF-8 메시지가 Phase 48 baseline 과 동일하게 정상 인코딩된다(TcpServer EncodingType static→instance 전환 후 회귀 0).
result: pass
note: "2026-06-23 SIMUL 세션 중 $SITE_STATUS/$TEST/$RESULT UTF-8 메시지 10+ 회 정상 송수신(35항목 장문 RESULT 포함, 깨짐 0). EncodingType static→instance(49-03) 회귀 0 입증. 단 한글 문자 격리 테스트는 미수행(메시지 내용이 ASCII FAI 코드) — UTF-8 인코딩 경로 자체는 확인."

## Summary

total: 9
passed: 9
issues: 0
pending: 0
skipped: 0

## Gaps

[none yet]
