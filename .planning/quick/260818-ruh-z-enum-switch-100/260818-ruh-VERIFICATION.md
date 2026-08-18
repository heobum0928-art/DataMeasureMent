---
phase: quick-260818-ruh
verified: 2026-08-18T00:00:00Z
status: human_needed
score: 11/11 code-level must-haves verified
overrides_applied: 0
findings:
  - id: F-ruh-01
    severity: info
    title: "SUMMARY 의 불변 카운트 표 1행이 사실과 다름 (ProcessCrossZCaptureTick 3 → 실제 5)"
    behavioral_impact: none
    detail: "코드 레벨 출현은 2→2 로 무변경(호출 1 + 선언 1). 증가분 2건은 신규 WHY 주석. 동작 회귀 아님. 다만 PLAN 의 자동 검증식 `grep -c 'ProcessCrossZCaptureTick' == 3` 는 실제로는 FAIL 이며, SUMMARY 가 'INVARIANT COUNTS PASS' 로 보고한 것은 부정확하다."
human_verification:
  - test: "프로토콜 사이클 크로스-Z 측정 — Z1 tick 에서 CROSS_Z_INCOMPLETE 표시, Z2 tick 에서 정상 측정값"
    expected: "Z1 은 미완성 표시, Z2 에서 실제 측정값 산출 (갈래 6b → 7)"
    why_human: "실제 PLC 2-tick 시퀀스와 HW Z축 트리거가 필요 — 정적 분석/컴파일로는 재현 불가"
  - test: "FAI 캡처 PNG 가 리팩토링 전과 동일 이미지인지 대조"
    expected: "저장된 role 이미지가 리팩토링 전과 동일"
    why_human: "런타임 HImage 내용 비교 — 이번 작업 최대 위험 지점(갈래 #5 부수효과). 코드상 동치는 증명됐으나 실물 확인 권장"
  - test: "RUN 버튼(비프로토콜)으로 크로스-Z 항목 실행"
    expected: "PASS 로 조용히 집계되지 않고 NG 로 표시 (갈래 2 / 6a)"
    why_human: "UI 표시 및 집계 결과 육안 확인 필요"
---

# Quick 260818-ruh 검증 리포트 — 크로스-Z 게이트 enum + switch 리팩토링

**목표:** 크로스-Z 게이트를 명시적 상태 enum + switch 로 리팩토링 — **동작 100% 보존**
**검증 방식:** SUMMARY 주장 불신. `git diff b6a8cb8 a77e471` 을 직접 문장 단위로 대조 + 모든 grep 재실행 + 빌드 재실행.
**결론:** **동작 100% 보존 확인.** 7갈래 전부 1:1 동치. 코드 레벨 회귀 0건.

---

## 0. 검증 기준점

| 항목 | 실측값 |
|------|--------|
| 커밋 | `a77e471` (부모 `b6a8cb8`) |
| 커밋된 파일 | `Action_FAIMeasurement.cs` **1개뿐** (`git show --name-only` 확인) |
| diff hunk | 정확히 **3덩어리** — `@@ -39,6 +39,18 @@`(enum) / `@@ -547,73 +559,69 @@`(게이트) / `@@ -667,6 +675,50 @@`(신규 메서드 3개) |
| 빌드 | Debug\|x64 **성공**, 경고 **12줄** (CS0618×10 + CS0162×2) = baseline 정확히 일치 |

**핵심 전제 확인:** `crossZRoleImage` / `faiAllPass` / `measuredCount` 는 `ProcessOneMeasurement` 의 **`ref` 파라미터**다(L531–533). 따라서 헬퍼로 `ref` 재전달해도 **동일 저장소를 가리키는 별칭(aliasing)** 이 그대로 유지된다 — 값 복사가 아니다. 이것이 헬퍼 추출이 동치인 근거의 핵심이다.

---

## 1. 7갈래 1:1 대조표 (전 코드 → 후 코드)

원본은 `git show b6a8cb8:…Action_FAIMeasurement.cs` 의 L547–620.

| # | 조건 | 전(원본) 코드 | 후(현재) 코드 | 판정 |
|---|------|---------------|---------------|------|
| **1** | `IsZIndexMisconfigured` true | `if (bMisconfigured) { MarkMeasurementZIndexMisconfigured(meas); faiAllPass=false; measuredCount++; return; }` | L574 `bMisconfigured` 계산 → L576 `eGate = Misconfigured` → **L597–601** `case Misconfigured:` 에 동일 4문장 (`Mark…` / `faiAllPass=false` / `measuredCount++` / `return`) | ✓ **동치** — 문장 4개, 순서 동일 |
| **2** | `!bRelevant` && 비프로토콜 | `if (!bRelevant) { if (bNonProtocolCycle) { Mark…Incomplete(meas,false,false,parentSeq2); faiAllPass=false; measuredCount++; } return; }` | **L602–609** `case NotMyTick:` — `if (bNonProtocolCycle) { MarkMeasurementCrossZIncomplete(meas, false, false, parentSeq2); faiAllPass = false; measuredCount++; }` 인자 문자열까지 동일 | ✓ **동치** |
| **3** | `!bRelevant` && 프로토콜 | 위 if 진입 안 함 → `return` 만. 상태변화 0 | **L609** `return; // 프로토콜: …상태변화 없음` — `measuredCount++`(L607)이 `if (bNonProtocolCycle)` 블록 **L604–608 안에만** 존재 | ✓ **동치 — `measuredCount` 증가 안 함** (아래 §2 기계증명) |
| **4** | `!bCaptureOk` | `ClearResult(); LastSkipReason=NO_IMAGE; LastJudgement=false; faiAllPass=false; measuredCount++; return;` | **L610–616** `case CaptureFailed:` 에 **동일 5문장 + return**, 순서 동일 | ✓ **동치** |
| **5** | 부수효과 `bCaptureOk && crossZRoleImage==null && !IsNullOrEmpty(szCapturedRoleKey) && parentSeq2!=null` | `!bCaptureOk` 게이트 **뒤**, `!bCompleted` 게이트 **앞**에 인라인 | `TakeCrossZRoleImageIfFirst` (본체 L695–701, 조건식 **원문 verbatim**) 호출부 = **L618(HalfPending 첫 줄) / L622(BothReady 첫 줄) 2곳뿐** | ✓ **동치** — 아래 §3 상세 |
| **6a** | `!bCompleted` && 비프로토콜 | `Mark…Incomplete(meas,true,false,parentSeq2); faiAllPass=false;` … `measuredCount++; return;` | `MarkCrossZHalfPending` if-참 **L705–708**, 합류점 `measuredCount++` **L719**, 호출 L619 직후 L620 `return` | ✓ **동치** |
| **6b** | `!bCompleted` && 프로토콜 | `Mark…Incomplete(meas,true,true,parentSeq2); faiAllPass=false;` … `measuredCount++; return;` | `MarkCrossZHalfPending` else **L709–718**(T-HWB-01 주석 5줄 동반 이동), 합류점 `measuredCount++` **L719** | ✓ **동치** |
| **7** | `bCompleted` | 모든 if 를 빠져나가 `}` 로 블록 종료 → L618 `ResolveDatumTransform` | **L621–623** `case BothReady:` → `break; // 완성 index` → L624 switch 닫힘 → L625 `if(bHasAnyZIndex)` 닫힘 → **L626 `HTuple transform = ResolveDatumTransform(...)`** | ✓ **동치 — `return` 아님, 확인함** |

### 갈래 7 (치명적 회귀 후보) 실측 코드
```
621                    case ECrossZGate.BothReady:
622                        TakeCrossZRoleImageIfFirst(parentSeq2, bCaptureOk, szCapturedRoleKey, ref crossZRoleImage);
623                        break; // 완성 index — 아래 공용 실행 경로로 계속 진행(...)
624                }
625            }
626            HTuple transform = ResolveDatumTransform(parentSeq2, meas.DatumRef);
```
`break` 확인. switch 이후 `if` 블록 안에 **잔여 문장이 0개**이므로 `break` 는 곧바로 L626 으로 흐른다. **크로스-Z 측정 미실행 회귀 없음.**

---

## 2. 갈래 #3 전용 가드 (통계 오염 여부) — 직접 재실행

`case NotMyTick:` ~ `case CaptureFailed:` 구간 실측:

| 패턴 | 개수 |
|------|------|
| `if (bNonProtocolCycle)` | 1 |
| `measuredCount++` | 1 (그 if 블록 **안**) |
| `MarkMeasurementCrossZIncomplete(meas, false, false, parentSeq2)` | 1 |

→ `measuredCount++` 가 if 밖으로 나오지 않았음이 기계 증명됨. **프로토콜 사이클에서 통계 오염 없음.**

---

## 3. 부수효과 #5 — 실행 상태 집합 불변

**원본**에서 이 문장에 도달하는 상태:
- Misconfigured → 이전 `return` 으로 탈출 ✗
- NotMyTick → 이전 `return` 으로 탈출 ✗
- CaptureFailed → 이전 `return` 으로 탈출 ✗
- HalfPending → **도달 ✓**
- BothReady → **도달 ✓**

**리팩토링 후:** 호출부는 L618 / L622 **2곳뿐**이며, switch **앞**(L562–594)에는 0건. → 실행 상태 집합 `{HalfPending, BothReady}` **완전 동일**.

조건식 4항 verbatim 비교 (원본 인라인 vs L696):
```
bCaptureOk && crossZRoleImage == null && !string.IsNullOrEmpty(szCapturedRoleKey) && parentSeq2 != null
```
→ **문자 단위 동일.** 첫 항 `bCaptureOk` 도 제거하지 않고 보존(대조 근거).
`TakeCrossZImageCopy(szCapturedRoleKey)` 파일 카운트 **1 → 1**.

**캡처 이미지 뒤바뀜(T-ruh-03) 위험 = 코드상 제거됨.**

---

## 4. 단락(short-circuit) 순서 — 호출 횟수 보존

구조 (L574–592):
```
bool bMisconfigured = IsZIndexMisconfigured(dualMeasForGate, parentSeq2);   // L574
if (bMisconfigured) { eGate = Misconfigured; }                              // L575-577
else {
    ProcessCrossZCaptureTick(...);                                          // L581
    bNonProtocolCycle = parentSeq2 == null || !parentSeq2.IsProtocolDrivenCycle();  // L590
    eGate = ResolveCrossZGate(bRelevant, bCaptureOk, bCompleted);           // L591
}
```
→ `IsZIndexMisconfigured` 가 true 면 `ProcessCrossZCaptureTick`(L581) 과 `IsProtocolDrivenCycle()`(L590) 에 **도달 자체 불가**. 원본과 동일. **불필요한 캡처/저장 발생 없음.**

`ResolveCrossZGate`(L682–687) 는 인자 3개 bool 만 읽고 아무것도 쓰지 않는 **완전 순수 함수**. 판정 순서 `bRelevant → bCaptureOk → bCompleted` 가 원본 중첩 if 순서와 1:1.

---

## 5. switch 완전성 / out 변수 확정 대입

- `case ECrossZGate.*:` 앵커 카운트 = **5** = enum 멤버 5개 → **누락 case 0**. `default:` 없음은 의도적(주석 L593–594 명문화). 5멤버 전수 처리이므로 조용한 fall-through 경로 없음.
- `eGate` 는 if/else 양쪽에서 확정 대입 → 컴파일 통과.
- out 변수 5개(`bRelevant`/`bCaptureOk`/`bCompleted`/`szCapturedRoleKey`/`bNonProtocolCycle`)를 선언 시 초기화 → **신규 CS0219 / CS0168 경고 0건** (빌드 실측 확인).

---

## 6. 프로젝트 규칙 준수 — 직접 재실행

| 규칙 | 실측 | 판정 |
|------|------|------|
| 삼항 `?:` 0건 | 정제 grep 결과 **1줄**, L1281 = `//260702 hbk 기존 삼항(?:) → if-else 로 전개` **주석** | ✓ 코드 삼항 0 |
| C# 7.2 — switch expression 금지 | `=> ` 카운트 **1** (기존 baseline L52 `ShotParam => …`, 증가 0). pattern `when` **0** | ✓ 고전 switch/case/break |
| 헝가리언 | 신규 전부 `b`/`sz`/`e` 접두 (`bMisconfigured`, `bNonProtocolCycle`, `szCapturedRoleKey`, `eGate`, `ECrossZGate`) | ✓ |
| 보존 주석 삭제 0건 | `260729-e9q` **2→2**, `260729-hwb` **8→8**, `Phase 68 D-02a/D-05` **1→1** | ✓ 삭제 0 |
| 커밋 파일 1개 | `git show --name-only a77e471` → `Action_FAIMeasurement.cs` 만 | ✓ |
| **csproj 미커밋 유지** | `git status --porcelain` → ` M WPF_Example/DatumMeasurement.csproj`. 커밋 포함 여부 grep → **0**. 로컬 `OutputPath=D:\Data\` + Release `SIMUL_MODE` 그대로 unstaged | ✓ **BLOCKER 아님** |

---

## 7. 파일 전역 불변 카운트 — 직접 재측정 (before=`b6a8cb8` vs after)

| 패턴 | before | after | 판정 |
|------|--------|-------|------|
| `measuredCount++` | 8 | 8 | ✓ |
| `faiAllPass = false` | 8 | 8 | ✓ |
| `meas.ClearResult()` | 7 | 7 | ✓ |
| `MarkMeasurementCrossZIncomplete(` | 4 | 4 | ✓ |
| `SkipReason.NO_IMAGE` | 2 | 2 | ✓ |
| `TakeCrossZImageCopy(szCapturedRoleKey)` | 1 | 1 | ✓ |
| `parentSeq2.IsProtocolDrivenCycle()` | 1 | 1 | ✓ |
| `IsZIndexMisconfigured(` | 2 | 2 | ✓ |
| `MarkMeasurementZIndexMisconfigured` | 3 | 3 | ✓ |
| `ProcessCrossZCaptureTick` | 3 | **5** | ⚠ **F-ruh-01** |
| `case EStep.*:` (무회귀) | 6 | 6 | ✓ |

### F-ruh-01 상세 (동작 영향 없음)

SUMMARY 는 이 행을 "3 | **3**" 으로 적고 `INVARIANT COUNTS PASS` 라고 보고했으나 **실제 raw 카운트는 5**다. 내역:

| 줄 | 성격 |
|----|------|
| L563 | **주석**(신규) `⚠ 판정에 필요한 호출 중 ProcessCrossZCaptureTick 은 순수하지 않다…` |
| L581 | **코드** — 호출 |
| L679 | **주석**(신규) `…IsZIndexMisconfigured / ProcessCrossZCaptureTick /…` |
| L947 | **주석**(기존) |
| L1572 | **코드** — 메서드 선언 |

→ **코드 출현 2 → 2 로 무변경** (호출 1 + 선언 1). 증가분 2건은 전부 신규 WHY 주석. **동작 회귀 아님.**
다만 PLAN 의 자동 검증식 `[ "$(grep -c 'ProcessCrossZCaptureTick' $F)" = "3" ]` 은 실제로 **FAIL** 하며, 이는 PLAN 이 앵커 없는 카운트를 쓴 설계 결함(PLAN §G-5 가 스스로 경고한 바로 그 유형)이다. SUMMARY 의 "PASS" 보고는 부정확하다. 코드 수정은 불필요.

---

## 8. 범위 밖 무변경

diff hunk 가 3덩어리뿐이므로 기계적으로 보장된다:
- **Datum 게이트 2개** — `IsDatumFailed`(L539–545) / `IsDatumRefUnresolvable`(L549–554) → 원본 L547 이전 구간, **diff 무접촉**
- **`ResolveDatumTransform` 이후 실행/집계 경로** — 원본 L620 이후, **diff 무접촉**. L626 `ResolveDatumTransform` / L627 `InjectDatumOrigin` 은 hunk 내 **context 줄**(변경 없음)로 나타남
- `bHasAnyZIndex` / `dualMeasForGate` 선언 2줄 — hunk 시작 이전 context, **무변경**

---

## 9. 종합 판정

| 항목 | 결과 |
|------|------|
| 7갈래 1:1 동치 | ✓ 7/7 |
| 갈래 #3 통계 오염 | ✓ 없음 |
| 갈래 #5 부수효과 시점 | ✓ 원본과 동일 (`{HalfPending, BothReady}`) |
| 갈래 #7 `break` fall-through | ✓ 확인 (return 아님) |
| 단락 순서 / 호출 횟수 | ✓ 보존 |
| switch 누락 case | ✓ 없음 (5/5) |
| out 변수 확정 대입 | ✓ (신규 미사용변수 경고 0) |
| 빌드 | ✓ 성공, 경고 12줄 baseline 동일 |
| 프로젝트 규칙 | ✓ 전부 준수 |
| csproj 유출 | ✓ 없음 (unstaged 유지) |
| SUMMARY 정확성 | ⚠ 1행 부정확 (F-ruh-01, 동작 영향 없음) |

**코드 레벨 회귀 0건. "동작 100% 보존" 목표 달성.**
남은 것은 실기 UAT 3건뿐이며, 이는 PLC 2-tick 시퀀스 / 실제 캡처 PNG 대조 / UI 표시 확인이라 사람만 할 수 있다 → `status: human_needed`.

---

_Verified: 2026-08-18_
_Verifier: Claude (gsd-verifier) — SUMMARY 무신뢰, git diff 직접 대조 + 전 grep 재실행 + 빌드 재실행_
