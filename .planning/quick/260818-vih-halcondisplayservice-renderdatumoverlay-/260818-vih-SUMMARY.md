---
phase: quick-260818-vih
plan: 01
subsystem: halcon-display
tags: [refactor, extract-method, datum-overlay, behavior-preserving]
requires: []
provides: ["RenderDatumOverlay 구역 메서드 7개"]
affects: ["WPF_Example/Halcon/Display/HalconDisplayService.cs"]
tech-stack:
  added: []
  patterns: ["Extract Method (behavior-preserving transformation)"]
key-files:
  created: []
  modified:
    - WPF_Example/Halcon/Display/HalconDisplayService.cs
decisions:
  - "구역 메서드 파라미터 이름을 호출자 지역변수와 글자 그대로 동일하게 지어 옮긴 199줄의 토큰 변경을 0건으로 만들었다 — 바이트 동치 diff 가 성립하는 근거"
  - "값형 파라미터(lineWidth/cthHideRois)는 구역 안에서 재대입이 없어 ref 불필요, 값 전달"
  - "바깥 try/catch 는 RenderDatumOverlay 에 잔류시켜 8개 호출 전체를 감싼다 — 예외 시 이후 구역 중단 동작이 원본과 동일"
metrics:
  duration: "약 25분"
  completed: 2026-08-18
---

# Quick 260818-vih: RenderDatumOverlay 구역 메서드 추출 Summary

`HalconDisplayService.RenderDatumOverlay`(BASE `bef801f` L856–1099, 244줄)의 렌더 구역 7개를
private 구역 메서드 7개로 **순수 Extract Method** 했다. 옮긴 199줄은 선행 공백을 제외하면 원본과
**바이트 단위로 동일**하며, 그리기 순서 / 조건 / 색상 / 좌표 / 라벨 / 예외 삼킴 범위가 전부 보존된다.

- 기준 커밋(BASE): `bef801f`
- 커밋 2개: `104c167`(R1–R4), `3b7081e`(R5–R7)
- 변경 파일: `WPF_Example/Halcon/Display/HalconDisplayService.cs` **1개뿐** (`git diff --name-only bef801f HEAD` → 1줄)
- 파일 줄수: 1177 → 1223

---

## ① 바이트 동치 증명 (핵심 증거)

정규화 방식: 각 줄의 **선행 공백만 제거**(`sed 's/^[[:space:]]*//'`). 그 외 어떤 정규화도 하지 않았다.
따라서 diff 가 비어 있다는 것은 **들여쓰기를 뺀 모든 문자가 원본과 같다**는 뜻이다 (토큰 변경 0건).

비교 명령(구역마다 앵커 유일성 `==1` 을 먼저 확인한 뒤 실행):

```bash
diff <(git show bef801f:$F | sed -n "<BASE시작>,<BASE끝>p" | sed 's/^[[:space:]]*//') \
     <(sed -n "<현재앵커줄>,<+줄수-1>p" $F        | sed 's/^[[:space:]]*//')
```

| 구역 | 신규 메서드 | BASE 범위(@`bef801f`) | 줄수 | diff 결과 |
|------|-------------|------------------------|------|-----------|
| R1 Line1/Vertical 슬롯 | `RenderDatumSlotRoi` | L885–915 | 31 | *(빈 출력)* |
| R2 Line2 사각형 | `RenderDatumLine2Roi` | L917–928 | 12 | *(빈 출력)* |
| R3 Circle 검색 영역 | `RenderDatumCircleRoi` | L930–949 | 20 | *(빈 출력)* |
| R4 Horizontal A/B | `RenderDatumHorizontalRois` | L951–978 | 28 | *(빈 출력)* |
| R5 RefOrigin 십자 | `RenderDatumRefOriginCross` | L980–999 | 20 | *(빈 출력)* |
| R6 검출 결과 오버레이 | `RenderDatumDetectedOverlay` | L1001–1060 | 60 | *(빈 출력)* |
| R7 DETECT FAIL 라벨 | `RenderDatumDetectFailLabel` | L1066–1093 | 28 | *(빈 출력)* |
| **합계** | | | **199** | **전부 diff 0** |

실제 실행 출력 (Task 2 verify 블록 [2] — 7구역 전수, diff 가 한 줄도 찍히지 않았다):

```
  OK Draw reference origin cross if configured (20 lines)
  OK 검출 라인 2개 + 교점 오버레이 (60 lines)
  OK Datum 검출 실패 시 (28 lines)
  OK RenderDatumOverlay 슬롯 분기 (31 lines)
  OK Line2 Rectangle2 는 TwoLineIntersect 에서만 렌더 (12 lines)
  OK Circle ROI 검색 영역 (20 lines)
  OK Horizontal A/B ROI Rectangle2 (28 lines)
T2 BYTE-EQUIV PASS (R1-R7 199줄, diff empty)
```

`OK ...` 줄은 `diff` 가 **성공(차이 없음)** 으로 끝났을 때만 출력되도록 짜여 있다.
차이가 한 줄이라도 있었으면 그 자리에 `<` / `>` 줄이 찍히고 체인이 즉시 끊긴다.

---

## ② 라인 멀티셋 대조 — 삭제된 줄 0

순수 추출이면 **줄이 새로 생기기만 하고(메서드 선언·중괄호·주석·호출) 사라지는 줄은 없어야 한다.**
BASE 전체 파일과 현재 전체 파일의 각 줄을 앞뒤 공백 제거 후 정렬해 비교했다.

```bash
comm -23 <(git show bef801f:$F | sed 's/^[[:space:]]*//; s/[[:space:]]*$//' | sort) \
         <(sed 's/^[[:space:]]*//; s/[[:space:]]*$//' $F | sort)
```

- 실행 결과: **`deleted=0`** (빈 출력)
- 반대 방향(`comm -13`, 신규로 추가된 서로 다른 줄): **46줄**
  = 신규 메서드 선언 7 + 새 주석 10 + 호출 7 + 여는/닫는 중괄호와 빈 줄 등
- 총 줄수 1177 → 1223 (**+46**)

즉 **BASE 에 있던 실행 줄 중 사라진 것은 하나도 없다.** 색상 지정 한 줄, 좌표 계산 한 줄도 그대로 남아 있다.

---

## ③ 색상 리터럴 전수 대조 (이 파일 최대 위험 지점)

**왜 이걸 세는가:** HALCON `SetColor` 에 비표준 색상명을 넘기면 예외가 나고, 이 파일의 관습인
`catch { }` 가 그 예외를 조용히 삼킨다. 결과적으로 **빌드도 통과하고 로그도 남지 않은 채 그 오버레이만 화면에서 사라진다.**
이 프로젝트에서 실제로 `"purple"` 과 `"light green"` 으로 겪은 함정이며(그래서 지금 `"#90EE90"` 로 바뀌어 있다),
색상 문자열이 **한 글자만 어긋나도** 정적으로는 아무 증상이 없다. 따라서 이 카운트가 유일한 방어선이다.

| 색상 리터럴 | BASE(`bef801f`) | 현재 | 판정 |
|---|---|---|---|
| `"red"` | 7 | 7 | 동일 |
| `"cyan"` | 6 | 6 | 동일 |
| `"yellow"` | 6 | 6 | 동일 |
| `"green"` | 4 | 4 | 동일 |
| `"magenta"` | 3 | 3 | 동일 |
| `"lime green"` | 2 | 2 | 동일 |
| `"orange"` | 2 | 2 | 동일 |
| `"blue"` | 2 | 2 | 동일 |
| `"slate blue"` | 2 | 2 | 동일 |
| `"gray"` | 1 | 1 | 동일 |
| `"white"` | 1 | 1 | 동일 |
| `"#90EE90"` | 1 | 1 | 동일 |

측정 명령: `grep -cE 'SetColor\([^)]*"<색상>"\)' <파일>` — BASE 는 `git show bef801f:$F` 로 같은 방식 측정.

추가로, 새로 쓴 주석에는 **큰따옴표로 감싼 색상 이름을 단 하나도 넣지 않았다**(카운트 오염 방지).
기존 주석 안의 색상명(`"light green"` 설명 주석 등)은 코드와 함께 그대로 이동했다.

### HALCON 호출 카운트 8종

| 호출 | BASE | 현재 |
|---|---|---|
| `HOperatorSet.SetColor` | 25 | 25 |
| `HOperatorSet.SetLineWidth` | 20 | 20 |
| `HOperatorSet.DispRectangle2` | 7 | 7 |
| `HOperatorSet.DispCircle` | 6 | 6 |
| `HOperatorSet.DispLine` | 21 | 21 |
| `HOperatorSet.DispCross` | 3 | 3 |
| `HOperatorSet.SetTposition` | 5 | 5 |
| `HOperatorSet.WriteString` | 5 | 5 |

### 라벨 문자열 8종 (각 1건 유지)

`"L1")` / `"L2")` / `"Vert")` / `"Circle")` / `"H-A")` / `"H-B")` / `"Datum Origin")` / `"DETECT FAIL: "`
→ 전부 정확히 1건. (그리는 글자가 바뀌면 화면 문구가 달라지므로 함께 고정)

### 헬퍼 호출 카운트 (구역 안 호출이 통째로 따라갔는지)

`DrawRoiLabel` 5 / `DrawRoiLabelAt` 5 / `RenderCircleStripOverlay` 1 / `RenderDatumFindResult` 1 /
`DrawExtendedLine` 2 / `RenderRawEdgePoints` 6 / `EnsureFontInitialized` 6 — 전부 BASE 와 동일.

---

## ④ z-order(그리는 순서) 보존 증명

HALCON 창은 **나중에 그린 것이 위에 온다.** 그래서 호출 순서가 곧 화면 겹침 순서다.
순서가 하나만 바뀌어도 "위에 보여야 할 십자가 원에 가려지는" 식의 시각 회귀가 난다.

현재 `RenderDatumOverlay` 본문의 호출 8줄과 그 줄번호:

| 순서 | 호출 | 줄번호 |
|---|---|---|
| 1 | `RenderDatumSlotRoi(window, datum);` | 885 |
| 2 | `RenderDatumLine2Roi(window, datum);` | 887 |
| 3 | `RenderDatumCircleRoi(window, datum, color, lineWidth, cthHideRois);` | 889 |
| 4 | `RenderDatumHorizontalRois(window, datum, color, lineWidth, cthHideRois);` | 891 |
| 5 | `RenderDatumRefOriginCross(window, datum);` | 893 |
| 6 | `RenderDatumDetectedOverlay(window, datum);` | 895 |
| 7 | `RenderDatumFindResult(window, datum);` *(기존 호출, 잔류)* | 899 |
| 8 | `RenderDatumDetectFailLabel(window, datum);` | 901 |

`885 887 889 891 893 895 899 901` — **엄격 오름차순, 중복 없음(8개 유일)**.
BASE 의 구역 순서(R1 → R2 → R3 → R4 → R5 → R6 → FindResult → R7)와 **1:1 일치**한다.
또한 8개 호출 전부가 첫 구역 메서드 선언(L911)보다 앞에 있어, 전부 `RenderDatumOverlay` 본문 안에 있음이 확인된다.

**구역 내부 순서도 보존:** R6 안쪽 `RenderRawEdgePoints` 6줄은 BASE 와 diff 0 이다
(호출 순서 + 색상 인자 `"cyan" / "magenta" / "gray"(size 4) / "green" / "lime green" / "orange"` 전부 동일).
이 순서는 "raw 점 먼저 → 검출 원 → 중심 십자(top)" 라는 기존 z-order 주석이 이유를 명시해 둔 부분으로, 손대지 않았다.

---

## ⑤ try/catch 무접촉 증명

- 파일 전역 `catch` 카운트: BASE **28** → 현재 **28** (신설 0, 삭제 0)
- 바깥 `try { … } catch { // Suppress display errors }` 는 `RenderDatumOverlay` 에 **그대로 잔류**하여 8개 호출 전체를 감싼다.
- 따라서 구역 메서드 안에서 예외가 나면 호출자로 전파되어 **같은 catch 에 잡히고, 그 뒤 구역은 실행되지 않는다.**
  이는 원본에서 예외 발생 시 이후 구역이 중단되던 동작과 **완전히 동일**하다.
- **구역 메서드 안에 try/catch 를 새로 만들지 않았다.** 만들었다면 원래 중단됐어야 할 뒤쪽 렌더가 계속 그려져 화면이 달라진다.
- R7(`DETECT FAIL`) 내부의 기존 `try { EnsureFontInitialized … WriteString } catch { }` 는
  **경계를 쪼개지 않고 통째로** `RenderDatumDetectFailLabel` 안으로 이동했다(바이트 동치 diff 로 확인됨).

---

## ⑥ 파라미터 취급표 + 잔류 항목

파라미터 이름을 호출자 지역변수와 **글자 그대로 동일**하게 지었다. 그 덕분에 옮긴 199줄에서
식별자를 단 하나도 고칠 필요가 없었고, 이것이 바이트 동치가 성립한 직접적 이유다.

| 파라미터 | 형 | 구역 안 취급 | 전달 방식 | 근거 |
|---|---|---|---|---|
| `window` | `HWindow` (참조형) | 인자로만 사용 | 값 전달 | 재대입 없음. HALCON 창의 색상·선굵기 **상태는 window 객체에 남으므로**, 호출 직전 `SetColor`/`SetLineWidth` 로 설정한 상태가 구역 메서드 안까지 그대로 이어진다 (R1/R2 가 이 상태에 의존해 별도 SetColor 없이 그린다) |
| `datum` | `DatumConfig` (참조형) | 읽기만 | 값 전달 | 재대입 없음 |
| `color` | `string` (참조형·불변) | 읽기만 | 값 전달 | 재대입 없음 |
| `lineWidth` | `int` (값형) | 읽기만 | 값 전달 | 구역 안 대입 0건 → **`ref` 불필요** |
| `cthHideRois` | `bool` (값형) | 읽기만 | 값 전달 | 구역 안 대입 0건 → **`ref` 불필요** |

- `out` 파라미터: 이 메서드 범위에 **0건**.
- `_isFontInitialized` / `_normalFontName` 은 클래스 필드이므로 인스턴스 메서드인 신규 메서드에서 그대로 접근된다 — 파라미터화하지 않았다.

**`RenderDatumOverlay` 에 잔류한 것(추출 대상 아님):**
1. `if (datum == null) return;` null 가드
2. `isSelected` 기반 색상·선굵기 결정 if-else (`"cyan"`/3 vs `"blue"`/2)
3. 최초 `SetColor` / `SetLineWidth` 호출
4. `cthHideRois` 계산 + 그 이유를 적은 설명 주석 3줄
5. 바깥 `try { … } catch { // Suppress display errors }`
6. `RenderDatumFindResult(window, datum);` 호출 + 설명 주석 2줄 (z-stack last 위치를 유지해야 하므로 그 자리 그대로)

또한 같은 파일의 다른 메서드(`RenderCircleStripOverlay` / `RenderDatumFindResult` / `DrawDirectionArrow` /
`DrawRoiLabel` / `DrawRoiLabelAt` / `RenderRawEdgePoints` / `DrawExtendedLine`)는 **무접촉**이며,
시그니처 3종을 별도로 재확인했다. 특히 `RenderCircleStripOverlay` 의 `if (datum == null) return;` 가드도
그대로 살아 있다(파일 전역 가드 카운트 2 = 두 메서드 각 1건).

---

## ⑦ 추출하지 않은 부분과 근거

**없음.** 계획된 7개 구역을 전부 추출했고, 각 구역은 경계를 쪼개지 않고 통째로 옮겼다.
다만 아래 두 가지는 **의도적으로 더 쪼개지 않았다** — 쪼개면 동작이 달라질 수 있기 때문이다.

- R6 안쪽 `if (AlgorithmTypeEnum == CircleTwoHorizontal && CircleDetected_Radius > 0)` 블록:
  더 쪼개면 `SetColor`/`SetLineWidth` 상태 흐름과 z-order 판단 지점이 늘어난다. 이번 범위 밖으로 두었다.
- R7 내부 try/catch: 바깥 try 와 합치거나 분리하면 **삼켜지는 예외 범위가 달라져** 렌더가 부분적으로 사라질 수 있다. 통째로 이동만 했다.

---

## 빌드 검증

```
msbuild DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Rebuild
  (OutputPath = 스크래치 디렉터리 — 실행 중인 D:\Data\ 배포본 무접촉, 프로세스 종료 안 함)
```

- 착수 전 baseline 경고: **12줄 (CS0618×10 + CS0162×2)** → `disp-baseline-warn.txt` 로 저장
- Task 1 후: `exit=0`, 경고 diff → `WARN IDENTICAL`
- Task 2 후: `exit=0`, `warnlines=12`, 경고 diff → `WARN IDENTICAL TO BASELINE (12)`
- 신규 `CS0219` / `CS0168` / `CS0177` / `CS0165`: **0건** (미사용 변수·미할당 변수 경고가 안 났다는 것은 옮긴 구역이 필요한 값을 전부 파라미터로 받고 있다는 뜻)

기타 컨벤션:
- 코드 삼항 `?:` **0건 유지**
- `=> `(expression-bodied / 람다) **0건 유지** (C# 7.2)
- 신규 메서드 선언은 Allman 스타일, 옮긴 본문은 재포맷 없이 원형 유지

---

## 저장소 위생 (csproj 오염 방지)

`WPF_Example/DatumMeasurement.csproj` 에는 **커밋하면 안 되는 로컬 설정**이 떠 있다
(Debug `OutputPath=D:\Data\`, Release `DefineConstants` 의 `SIMUL_MODE`).
저장소에 들어가면 현장 배포본이 시뮬레이션 모드로 나가므로, 매 커밋마다 다음을 확인했다.

- `git add` 는 **대상 파일 경로 1개만** 지정 (`git add -A` / `git commit -a` 미사용)
- 커밋 직전 `git diff --cached --name-only` → 정확히 **1줄** (`HalconDisplayService.cs`)
- 커밋 후 `git status --porcelain -- WPF_Example/DatumMeasurement.csproj` → 여전히 `" M"` (**unstaged 그대로**)
- `git diff --name-only bef801f HEAD` → **1줄**
- 워킹트리 dirty 집합이 착수 시점 baseline 과 동일

```
$ git diff --stat bef801f HEAD
 WPF_Example/Halcon/Display/HalconDisplayService.cs | 416 ++++++++++++---------
 1 file changed, 231 insertions(+), 185 deletions(-)
```

> 참고: git 의 `231 insertions / 185 deletions` 는 **줄 단위 이동을 이동으로 인식하지 못해** 생기는 수치다.
> 실제로 사라진 줄이 0 이라는 것은 ②의 멀티셋 대조(`comm -23` 빈 출력)가 증명한다.

---

## 커밋

| 커밋 | 내용 |
|---|---|
| `104c167` | `refactor(260818-vih): RenderDatumOverlay ROI 4구역을 구역 메서드로 추출 (순수 이동, 렌더 무변경)` |
| `3b7081e` | `refactor(260818-vih): RefOrigin/검출결과/DETECT FAIL 3구역을 구역 메서드로 추출 (순수 이동, 렌더 무변경)` |

---

## ⑧ ⚠ 실기 UAT 요청 (내일 아침 확인 부탁드립니다)

위 정적 증거는 **코드가 원본과 동일함**을 증명하지만, **화면에 찍히는 픽셀 자체를 보증하지는 못합니다.**
이 코드의 실패 모드는 예외도 로그도 없이 오버레이가 조용히 사라지는 것이라, 최종 확인은 눈으로 봐야 합니다.
티칭 화면에서 아래 7가지만 확인해 주시면 됩니다.

1. **`TwoLineIntersect` Datum 선택** → L1 / L2 사각형과 `L1` / `L2` 라벨이 보이는지.
   선택 상태면 청록(cyan, 굵기 3), 비선택이면 파랑(blue, 굵기 2).
2. **`VerticalTwoHorizontal`(및 DualImage)** → `Vert` + `H-A` + `H-B` 가 보이고, **L1/L2 는 보이지 않아야** 합니다.
3. **`CircleTwoHorizontal`** → Circle ROI 원 + Strip 사각형들 + `H-A`/`H-B` 가 보이는지.
   그리고 Edit 모드를 끄고 티칭이 완료된 상태에서는 **이것들이 숨겨지는지**.
4. **티칭 성공 후** → 노란 Line1 외삽선 / 청록 Line2 외삽선 / 빨간 교점 십자 / 연두 검출 원 + 노란 중심 십자.
5. **`IsConfigured` Datum** → 자홍색(magenta) 십자 + `Datum Origin` 글자.
6. **검출 실패 Datum** → 화면 우상단에 빨간 `DETECT FAIL: {이름}` 라벨.
7. **여러 오버레이가 겹칠 때 가려짐 순서가 이전과 같은지** — 특히 연두 검출 원 **위에** 노란 중심 십자가 보이는지.

하나라도 이전과 다르게 보이면 즉시 알려주세요. 두 커밋(`104c167`, `3b7081e`)만 되돌리면
`bef801f` 상태로 완전히 복구됩니다(다른 파일은 전혀 건드리지 않았습니다).

---

## Self-Check: PASSED

- `WPF_Example/Halcon/Display/HalconDisplayService.cs` — FOUND
- 커밋 `104c167` — FOUND (`git log`)
- 커밋 `3b7081e` — FOUND (`git log`, 현재 HEAD)
- 신규 메서드 7개 선언 — FOUND (L911 / L947 / L965 / L990 / L1023 / L1049 / L1115)
