---
phase: quick-260818-vih
verified: 2026-08-18T00:00:00Z
status: human_needed
score: 15/15 must-haves verified
overrides_applied: 0
base_commit: bef801f
commits:
  - 104c167
  - 3b7081e
human_verification:
  - test: "TwoLineIntersect Datum 선택 → L1 / L2 사각형 + 라벨 표시, 선택 시 cyan(굵기3) / 비선택 blue(굵기2)"
    expected: "L1·L2 사각형과 라벨이 이전과 동일하게 보이고 선택 여부에 따라 색·굵기가 바뀐다"
    why_human: "HALCON HWND 픽셀 출력은 정적 분석으로 확인 불가"
  - test: "VerticalTwoHorizontal(및 DualImage) → Vert + H-A + H-B 표시, L1/L2 는 표시되지 않음"
    expected: "Vert/H-A/H-B 만 그려지고 L1/L2 는 없음"
    why_human: "렌더 결과 픽셀 확인 필요"
  - test: "CircleTwoHorizontal → Circle ROI + Strip 사각형 + H-A/H-B, Edit 모드 OFF + 티칭 완료 시 이들이 숨겨지는지"
    expected: "cthHideRois 가드가 이전과 동일하게 작동해 평소 모드에서 ROI 핸들이 숨겨진다"
    why_human: "런타임 상태(LastTeachSucceeded/isEditMode) 조합별 화면 확인 필요"
  - test: "티칭 성공 후 → 노란 Line1 외삽 / 청록 Line2 외삽 / 빨간 교점 십자 / 초록(#90EE90) 검출 원 + 노란 중심 십자"
    expected: "6종 오버레이가 모두 보이고 색이 이전과 같다"
    why_human: "SetColor 예외는 catch 로 삼켜져 로그가 남지 않는다 — 눈으로만 확인 가능"
  - test: "IsConfigured Datum → magenta 십자 + 'Datum Origin' 텍스트"
    expected: "자홍색 십자와 텍스트 표시"
    why_human: "폰트 초기화 + 텍스트 렌더는 화면 확인 필요"
  - test: "검출 실패 Datum → 우상단 빨간 'DETECT FAIL: {이름}' 라벨"
    expected: "라벨이 우상단에 표시되고 여러 datum 동시 실패 시 25px 간격으로 어긋나 겹치지 않는다"
    why_human: "GetPart 기반 좌표 + hash stagger 는 실제 표시 영역에서만 확인 가능"
  - test: "여러 오버레이가 겹칠 때 가려짐 순서(z-order)가 이전과 같은지 — 특히 검출 원 위에 노란 중심 십자가 보이는지"
    expected: "중심 십자가 검출 원 위에 보인다 (원에 가려지지 않는다)"
    why_human: "겹침 순서는 픽셀로만 확인 가능"
---

# Quick 260818-vih: `RenderDatumOverlay` 구역 추출 검증 보고서

**목표:** `HalconDisplayService.RenderDatumOverlay` 244줄을 7개 구역 메서드로 추출 — **렌더 동작 100% 보존**
**Base:** `bef801f` (파일 1177줄) → **HEAD:** `3b7081e` (파일 1223줄, +46줄)
**검증 방식:** SUMMARY 주장을 배제하고 **모든 명령을 검증자가 직접 재실행**
**Status:** `human_needed` — 정적 증거는 15/15 전부 PASS, 화면 픽셀만 사람 확인 필요

---

## 결론 요약

> **회귀 위험 근거 없음.** 이 변경은 diff 상 **순수 Extract Method 7건**이며,
> 옮겨간 **199줄이 원본과 바이트 단위로 동일**하고,
> **삭제된 소스 줄이 0줄**, **모든 문자열 리터럴이 개수까지 완전 동일**,
> **`try`/`catch` 개수가 18/28 로 불변**(구역 메서드 안 신규 try 0건),
> **호출 8줄이 원본 구역 순서와 1:1 오름차순**(z-order 보존),
> **범위 밖 영역 L1–881 및 파일 꼬리 76줄이 바이트 동일**,
> **빌드 PASS + 경고가 baseline 12줄과 정확히 일치**.

---

## ① 구역별 바이트 동치 대조 (핵심 증거)

정규화 = 각 줄의 **선행 공백만 제거**(dedent 만 허용). 토큰 변경이 한 글자라도 있으면 diff 가 비지 않는다.

| # | 구역 | 신규 메서드 | BASE 범위 (@bef801f) | 줄수 | 현재 위치 | diff |
|---|------|-------------|----------------------|------|-----------|------|
| R1 | Line1 / Vertical 슬롯 | `RenderDatumSlotRoi` | L885–915 | 31 | L913–943 | **EMPTY** ✓ |
| R2 | Line2 Rectangle2 | `RenderDatumLine2Roi` | L917–928 | 12 | L949–960 | **EMPTY** ✓ |
| R3 | Circle ROI + Strip | `RenderDatumCircleRoi` | L930–949 | 20 | L967–986 | **EMPTY** ✓ |
| R4 | Horizontal A/B ROI | `RenderDatumHorizontalRois` | L951–978 | 28 | L992–1019 | **EMPTY** ✓ |
| R5 | RefOrigin 십자 | `RenderDatumRefOriginCross` | L980–999 | 20 | L1025–1044 | **EMPTY** ✓ |
| R6 | 검출 결과 오버레이 | `RenderDatumDetectedOverlay` | L1001–1060 | 60 | L1051–1110 | **EMPTY** ✓ |
| R7 | DETECT FAIL 라벨 | `RenderDatumDetectFailLabel` | L1066–1093 | 28 | L1117–1144 | **EMPTY** ✓ |
| | | | | **199** | | |

실행 출력(검증자 직접 재실행):
```
  OK  [RenderDatumOverlay 슬롯 분기]  31줄  BASE L885-915 -> NOW L913..943  diff EMPTY
  OK  [Line2 Rectangle2 는 TwoLineIntersect 에서만 렌더]  12줄  BASE L917-928 -> NOW L949..960  diff EMPTY
  OK  [Circle ROI 검색 영역]  20줄  BASE L930-949 -> NOW L967..986  diff EMPTY
  OK  [Horizontal A/B ROI Rectangle2]  28줄  BASE L951-978 -> NOW L992..1019  diff EMPTY
  OK  [Draw reference origin cross if configured]  20줄  BASE L980-999 -> NOW L1025..1044  diff EMPTY
  OK  [검출 라인 2개 + 교점 오버레이]  60줄  BASE L1001-1060 -> NOW L1051..1110  diff EMPTY
  OK  [Datum 검출 실패 시]  28줄  BASE L1066-1093 -> NOW L1117..1144  diff EMPTY
```
**앵커 유일성:** 7개 앵커 모두 파일 내 `==1` (`chk` 함수가 `!=1` 이면 즉시 FAIL 하도록 되어 있고, 전부 통과했다).

---

## ② 라인 멀티셋 대조 — 삭제된 줄 **0**

```
comm -23 <(정렬 base) <(정렬 after) | wc -l  →  0
```
**BASE 에 있던 줄 중 현재 파일에 없는 줄이 단 하나도 없다.** 순수 추출이므로 추가만 있어야 한다는 조건 충족.

**추가된 46줄의 정체 (전수):**

| 종류 | 개수 | 내용 |
|------|------|------|
| 신규 메서드 선언 | 7 | `private void RenderDatumSlotRoi/Line2Roi/CircleRoi/HorizontalRois/RefOriginCross/DetectedOverlay/DetectFailLabel` |
| 신규 `//260818 hbk` 주석 | 11 | Extract Method 설명 (금칙어 없음 — 색상명/`HOperatorSet.`/`catch`/라벨 문자열 미포함) |
| 호출 줄 | 7 | 구역 메서드 호출 (`RenderDatumFindResult` 는 기존 줄이라 제외) |
| `{` / `}` | 14 | 메서드 본체 중괄호 |
| 빈 줄 | 7 | 메서드 구분 |
| **합계** | **46** | 1177 + 46 = **1223** ✓ (실측 `wc -l` 일치) |

**실행 가능한 코드(문·식)는 단 한 줄도 추가되지 않았다.**

---

## ③ 색상 리터럴 — 이 파일 최대 함정

HALCON `SetColor` 에 비표준 색상명이 들어가면 **예외 → `catch { }` 삼킴 → 로그 없음 → 그 오버레이만 조용히 사라진다.**
빌드도 통과하고 에러도 안 뜨기 때문에 **리터럴 대조가 유일한 방어선**이다.
(이 프로젝트 실제 사고: `"purple"`, `"light green"` → 현재 `"#90EE90"` 으로 교체되어 있음)

**검증 A — 요청받은 12종 카운트 (`SetColor\([^)]*"X"\)`):**

| 색상 | BASE | NOW | |
|------|------|-----|---|
| red | 7 | 7 | OK |
| cyan | 6 | 6 | OK |
| yellow | 6 | 6 | OK |
| green | 4 | 4 | OK |
| magenta | 3 | 3 | OK |
| lime green | 2 | 2 | OK |
| orange | 2 | 2 | OK |
| blue | 2 | 2 | OK |
| slate blue | 2 | 2 | OK |
| gray | 1 | 1 | OK |
| white | 1 | 1 | OK |
| #90EE90 | 1 | 1 | OK |

> 주의: 이 정규식의 `green` 4건에는 `lime green` 2건이 **부분 문자열로 포함**된다(정규식 특성). 그래서 아래 B 를 추가로 돌렸다.

**검증 B — 파일 전체 큰따옴표 문자열 리터럴 **전수** 멀티셋 대조 (요청보다 강한 검사):**
```
diff <(base 의 모든 "..." | sort | uniq -c) <(now 의 모든 "..." | sort | uniq -c)
  >>> IDENTICAL (all string literals, counts included)
```
**색상명·라벨("L1"/"L2"/"Vert"/"Circle"/"H-A"/"H-B"/"Datum Origin"/"DETECT FAIL: ")·기타 모든 문자열이 개수까지 완전히 동일.**
색상 리터럴이 한 글자라도 바뀌었을 가능성은 이 검사로 **배제**된다.

**검증 C — `SetColor(window, "리터럴")` 실호출 분포 (변수 전달 제외):**
BASE / NOW 양쪽 동일 — yellow 4, slate blue 2, red 2, magenta 2, cyan 2, green 1, gray 1, blue 1, `#90EE90` 1.

---

## ④ try/catch 경계 — 렌더 중단 동작

| 항목 | BASE | NOW | 판정 |
|------|------|-----|------|
| 파일 전역 `try` 개수 | 18 | 18 | **불변** ✓ |
| 파일 전역 `catch` 개수 | 28 | 28 | **불변** ✓ |
| 구역 메서드 영역(L913–1145) 내 `try` | — | **1** | R7 내부 기존 try 만 ✓ |

→ **구역 메서드 안에 새 try/catch 가 생기지 않았다.** (`try` 총합이 불변인 것이 결정적 증거 — 신설이 있었다면 19가 된다.)

**바깥 try 가 8개 호출 전체를 감싸는지 (직접 확인):**
```
L873              try
L874              {
L875–876              SetColor(window, color); SetLineWidth(window, lineWidth);
L878–883              cthHideRois 계산 + 설명 주석 3줄
L885 L887 L889 L891 L893 L895 L899 L901   ← 호출 8줄 (전부 try 안)
L902              }
L903              catch
L905                  // Suppress display errors
```
→ 구역 메서드에서 예외가 나면 **호출자의 같은 catch 로 전파되어 이후 구역이 중단된다** = 원본과 동일 동작.

**R7 내부 try/catch 통째 이동 확인:** `RenderDatumDetectFailLabel`(L1115~) 본문 안에
`try { EnsureFontInitialized … WriteString } catch { // Suppress display errors (기존 RenderDatumOverlay catch 컨벤션) }` 가
BASE L1071–1092 과 **바이트 동일**로 존재(위 ① R7 diff EMPTY 로 증명). 경계 분할 0건.

---

## ⑤ z-order (그리는 순서 = 화면 겹침 순서)

나중에 그린 것이 위에 온다. 호출 순서가 곧 화면이다.

| 순번 | 호출 | 현재 줄번호 | 원본 구역 시작 줄(@bef801f) |
|------|------|-------------|------------------------------|
| 1 | `RenderDatumSlotRoi(window, datum);` | **885** | 885 (R1) |
| 2 | `RenderDatumLine2Roi(window, datum);` | **887** | 917 (R2) |
| 3 | `RenderDatumCircleRoi(window, datum, color, lineWidth, cthHideRois);` | **889** | 930 (R3) |
| 4 | `RenderDatumHorizontalRois(window, datum, color, lineWidth, cthHideRois);` | **891** | 951 (R4) |
| 5 | `RenderDatumRefOriginCross(window, datum);` | **893** | 980 (R5) |
| 6 | `RenderDatumDetectedOverlay(window, datum);` | **895** | 1001 (R6) |
| 7 | `RenderDatumFindResult(window, datum);` (기존, 잔류) | **899** | 1064 |
| 8 | `RenderDatumDetectFailLabel(window, datum);` | **901** | 1066 (R7) |

→ 885 < 887 < 889 < 891 < 893 < 895 < 899 < 901, **엄격 오름차순**이고
원본 순서 885 < 917 < 930 < 951 < 980 < 1001 < 1064 < 1066 과 **1:1 대응**. **z-order 보존.**

`RenderDatumFindResult` 는 새 메서드 안으로 끌려 들어가지 않고 원래 자리(z-stack last 직전)에 주석 2줄과 함께 그대로 남았다 — diff 로 확인.

**R6 내부 z-order(`RenderRawEdgePoints` 6줄):**
```
diff <(base 의 6줄) <(now 의 6줄)  →  차이 없음
```
순서·색상 인자(`cyan`/`magenta`/`gray`,4.0/`green`/`lime green`/`orange`)까지 diff 0.

---

## ⑥ 파라미터 전달 정확성

**선언부 (실측):**
```
L911  private void RenderDatumSlotRoi(HWindow window, DatumConfig datum)
L947  private void RenderDatumLine2Roi(HWindow window, DatumConfig datum)
L965  private void RenderDatumCircleRoi(HWindow window, DatumConfig datum, string color, int lineWidth, bool cthHideRois)
L990  private void RenderDatumHorizontalRois(HWindow window, DatumConfig datum, string color, int lineWidth, bool cthHideRois)
L1023 private void RenderDatumRefOriginCross(HWindow window, DatumConfig datum)
L1049 private void RenderDatumDetectedOverlay(HWindow window, DatumConfig datum)
L1115 private void RenderDatumDetectFailLabel(HWindow window, DatumConfig datum)
```
**호출부 (실측, ⑤ 표와 동일):** 이름·순서·개수가 선언부와 **완전히 일치**.
파라미터 이름을 호출자 지역변수와 **글자 그대로 동일**하게 지었기 때문에, 잘못된 인자 순서로 넘어갈 여지가 구조적으로 없다
(예: `color` 와 `lineWidth` 를 바꿔 넘겼다면 `string`/`int` 형 불일치로 **컴파일 에러**가 난다 — 빌드 PASS 가 이를 보증).

**재대입 검사 (`ref` 불필요 근거, 검증자 직접 실행):**
L913–1145 전 범위에서 `color` / `lineWidth` / `cthHideRois` / `window` / `datum` 에 대한
대입(`=`, `+=`, `-=`, `++`, `--`) 패턴 검색 → **코드 히트 0건** (유일한 히트는 `cthHideRois 가드:` 주석 1줄, 대입 아님).
→ 전부 읽기 전용이므로 **값 전달로 충분, `ref` 불필요**가 독립 확인됨.
신규 메서드 7개 선언에 `ref`/`out` 파라미터 **0건**.

**HALCON 창 상태 이월:** `window` 는 참조형이므로 호출 전 `SetColor(window, color)`/`SetLineWidth(window, lineWidth)` 로 설정된
창 상태가 R1/R2 안까지 그대로 이어진다(R1/R2 는 자체 SetColor 없이 이 상태에 의존). 값 전달이어도 참조 복사이므로 동작 동일.

---

## ⑦ 잔류 항목 (`RenderDatumOverlay` 에 남아야 하는 것)

| 항목 | 확인 |
|------|------|
| `if (datum == null) return;` | **L858** 잔류 ✓ (선언 L856 직후) |
| `color` / `lineWidth` if-else (cyan·3 / blue·2) | L860–871 잔류 ✓ 삼항 아님 |
| 바깥 `try` L873 / `catch` L903 / `// Suppress display errors` | 잔류 ✓ |
| `SetColor` / `SetLineWidth` 초기 설정 | L875–876 잔류 ✓ |
| `cthHideRois` 계산 + 설명 주석 3줄 | L878–883 잔류 ✓ |
| `RenderDatumFindResult` 호출 + 주석 2줄 | L897–899 잔류 ✓ |

---

## ⑧ `if (datum == null) return;` 2건 보존

```
748:            if (datum == null) return;     ← RenderCircleStripOverlay
858:            if (datum == null) return;     ← RenderDatumOverlay
```
**전역 2건 유지.** 카운트를 맞추려고 L748 을 지우는 2차 사고는 발생하지 않았다.
`RenderCircleStripOverlay`(선언 L746)는 **L1–881 바이트 동일 구간**에 있으므로 **완전 무변경**이 증명된다(⑨ 참조).

---

## ⑨ 범위 밖 무변경 (가장 강한 형태로 증명)

파일을 세 구간으로 나눠 직접 대조:

| 구간 | 대조 | 결과 |
|------|------|------|
| **머리** BASE L1–881 ↔ NOW L1–881 | `diff` 원문(정규화 없음) | **완전 동일** ✓ |
| **편집 구간** BASE L882–1101 ↔ NOW L882–1147 | ① 구역별 바이트 동치 + ② 삭제 0 | 순수 추출 ✓ |
| **꼬리** BASE L1102–1177 ↔ NOW L1148–1223 (76줄) | `diff` 원문 | **완전 동일** ✓ |

범위 밖 메서드 선언 위치 (전부 바이트 동일 구간 안):

| 메서드 | 줄 | 구간 |
|--------|-----|------|
| `RenderDatumFindResult` | 329 | 머리(동일) |
| `EnsureFontInitialized` | 450 | 머리(동일) |
| `DrawDirectionArrow` | 653 | 머리(동일) |
| `DrawExtendedLine` | 694 | 머리(동일) |
| `RenderRawEdgePoints` | 721 | 머리(동일) |
| `RenderCircleStripOverlay` | 746 | 머리(동일) |
| `DrawRoiLabel` | 1150 | 꼬리(동일) |
| `DrawRoiLabelAt` | 1164 | 꼬리(동일) |

**HALCON 호출 카운트 (BASE/NOW):**
SetColor 25/25 · SetLineWidth 20/20 · DispRectangle2 7/7 · DispCircle 6/6 · DispLine 21/21 ·
DispCross 3/3 · SetTposition 5/5 · WriteString 5/5 · GetPart 1/1 — **전부 일치**

**헬퍼 호출 카운트 (BASE/NOW):**
`DrawRoiLabel(window` 5/5 · `DrawRoiLabelAt(` 7/7 · `RenderCircleStripOverlay(window, datum);` 1/1 ·
`RenderDatumFindResult(window, datum);` 1/1 · `DrawExtendedLine(window,` 2/2 ·
`RenderRawEdgePoints(window, datum.` 6/6 · `EnsureFontInitialized(window);` 6/6 — **전부 일치**

---

## ⑩ 프로젝트 규칙 준수

| 규칙 | BASE | NOW | 판정 |
|------|------|-----|------|
| 코드 삼항 `?:` | 0 | **0** | ✓ 유지 |
| `=> ` (expression-bodied / lambda) | 0 | **0** | ✓ 유지 (C# 7.2) |
| 브레이스 스타일 | Allman | Allman | ✓ 신규 메서드 7개 모두 여는 중괄호 다음 줄 |
| 기존 주석 삭제 | — | **0건** | ✓ ② 멀티셋 삭제 0 이 곧 주석 삭제 0을 증명 |
| `RenderDatumOverlay 슬롯 분기:` / `cthHideRois 가드:` / `CircleTwoHorizontal: … (의도적)` / `z-order:` 주석 | 존재 | 존재 | ✓ 로직 따라 이동만 |
| 신규 주석 접두 `//260818 hbk` | — | 7개 블록 전부 | ✓ |
| 신규 주석 금칙어(색상명·`HOperatorSet.`·`catch`·라벨) | — | **0건** | ✓ (③ 문자열 멀티셋 IDENTICAL 이 증명) |

---

## ⑪ 빌드 (검증자 직접 재실행)

```
MSBuild Debug|x64 -t:Rebuild  →  OutputPath = <scratch>\vih-verify\
  DatumMeasurement -> ...\scratchpad\vih-verify\DatumMeasurement.exe
```
**빌드 성공.** 경고 출력 = **CS0618 × 10 + CS0162 × 2 = 12줄** (wpftmp 프로젝트 6줄 + 본 프로젝트 6줄, 동일 내용 2회)
= **baseline 과 정확히 일치.**

**신규 경고 0건:** `CS0219`(할당했지만 미사용) / `CS0168`(선언 후 미사용) / `CS0177`(out 미할당) / `CS0165`(미할당 변수 사용) — **한 건도 없음.**
→ 추출 과정에서 지역변수가 끊기거나(`CS0165`) 잘려 남는(`CS0219`) 일이 발생하지 않았음이 컴파일러로 확인됨.

빌드 산출물 잠김 없음, 프로세스 종료 없음.

---

## ⑫ 커밋 위생

| 확인 | 결과 |
|------|------|
| `104c167` 변경 파일 | `WPF_Example/Halcon/Display/HalconDisplayService.cs` **1개뿐** ✓ |
| `3b7081e` 변경 파일 | `WPF_Example/Halcon/Display/HalconDisplayService.cs` **1개뿐** ✓ |
| `git diff --stat bef801f 3b7081e` | 1 file changed, 231 insertions(+), 185 deletions(-) — **1파일** ✓ |
| `WPF_Example/DatumMeasurement.csproj` | `git status` → **` M`(unstaged)** ✓ / `git log bef801f..HEAD -- csproj` → **0 커밋** ✓ |

**⚠ csproj 로컬 설정(Debug `OutputPath=D:\Data\`, Release `SIMUL_MODE`)은 커밋되지 않았다.** BLOCKER 아님.

---

## Must-Have 판정표

| # | Must-Have | 판정 | 근거 |
|---|-----------|------|------|
| 1 | 7구역 199줄 바이트 동치 | ✓ VERIFIED | ① 7/7 diff EMPTY |
| 2 | 삭제된 줄 0 | ✓ VERIFIED | ② `comm -23` = 0줄 |
| 3 | 호출 순서 원본과 1:1 | ✓ VERIFIED | ⑤ 885<887<889<891<893<895<899<901 |
| 4 | 색상 리터럴 12종 불변 | ✓ VERIFIED | ③ A/B/C 3중 확인, 전체 문자열 멀티셋 IDENTICAL |
| 5 | HALCON 호출 8종 카운트 불변 | ✓ VERIFIED | ⑨ 9종 전부 일치 |
| 6 | 라벨 문자열 8종 각 1건 | ✓ VERIFIED | ③ B 문자열 멀티셋 IDENTICAL |
| 7 | 바깥 try/catch 잔류, 구역 내 try 신설 0 | ✓ VERIFIED | ④ `try` 18/18, `catch` 28/28 |
| 8 | R7 내부 try/catch 통째 이동 | ✓ VERIFIED | ① R7 diff EMPTY |
| 9 | `color`/`lineWidth`/`cthHideRois` 재대입 0, ref 불필요 | ✓ VERIFIED | ⑥ 대입 히트 0건, ref/out 0건 |
| 10 | 가드·색상결정·cthHideRois·try 잔류 | ✓ VERIFIED | ⑦ 전항목 확인 |
| 11 | 기존 주석 삭제 0건 | ✓ VERIFIED | ② 멀티셋 삭제 0 |
| 12 | 빌드 성공 + 경고 baseline 12줄 | ✓ VERIFIED | ⑪ 직접 빌드, 신규 경고 0 |
| 13 | 삼항 0 / `=> ` 0 유지 | ✓ VERIFIED | ⑩ 0/0 |
| 14 | 범위 밖 무접촉 | ✓ VERIFIED | ⑨ 머리 881줄 + 꼬리 76줄 원문 diff 0 |
| 15 | `HalconDisplayService.cs` 외 커밋 0, csproj unstaged 유지 | ✓ VERIFIED | ⑫ |

**Score: 15/15**

---

## Gaps

**없음.** 정적으로 검출 가능한 회귀 위험이 하나도 발견되지 않았다.

---

## 사람이 눈으로 봐야 할 항목 (내일 아침 티칭 화면)

정적 증거는 **소스가 동일하다**는 것까지만 보증한다. HALCON 이 실제로 창에 무엇을 그리는지는
**픽셀로만 확인 가능**하다(SetColor 실패는 예외도 로그도 남기지 않는다). 아래 7가지를 한 번씩만 봐 주면 된다.

1. **TwoLineIntersect Datum 선택** → `L1` / `L2` 사각형 + 라벨이 보이는가. 선택 시 **하늘색(굵기 3)**, 비선택 시 **파란색(굵기 2)** 인가.
2. **VerticalTwoHorizontal (및 DualImage)** → `Vert` + `H-A` + `H-B` 만 보이고 `L1`/`L2` 는 **안 보이는가**.
3. **CircleTwoHorizontal** → `Circle` 원 + Strip 사각형들 + `H-A`/`H-B` 가 보이는가.
   그리고 **Edit 모드 OFF + 티칭 완료 상태에서 이들이 숨겨지는가**(`cthHideRois` 가드).
4. **티칭 성공 후** → 노란 Line1 외삽선 / 청록 Line2 외삽선 / **빨간 교점 십자** / 연두색 검출 원 / 노란 중심 십자.
5. **IsConfigured Datum** → **자홍색 십자 + "Datum Origin"** 텍스트.
6. **검출 실패 Datum** → 화면 **우상단 빨간 "DETECT FAIL: {이름}"** 라벨. 여러 개 동시 실패 시 세로로 어긋나 겹치지 않는가.
7. **겹침 순서** → 특히 **연두색 검출 원 위에 노란 중심 십자가 보이는가**(원에 가려지면 z-order 회귀).

**하나라도 이전과 다르면 즉시 롤백:**
```bash
cd /c/Info/Project/DataMeasurement
git revert --no-edit 3b7081e 104c167
# 또는 파일 단위 되돌리기 (csproj 로컬 변경 보존)
git checkout bef801f -- WPF_Example/Halcon/Display/HalconDisplayService.cs
```
`bef801f` 이 착수 전 상태이며, 이 두 커밋은 **이 파일 하나만** 건드렸으므로 되돌려도 다른 작업에 영향이 없다.

---

_Verified: 2026-08-18_
_Verifier: Claude (gsd-verifier) — 모든 명령을 SUMMARY 와 독립적으로 직접 재실행_
