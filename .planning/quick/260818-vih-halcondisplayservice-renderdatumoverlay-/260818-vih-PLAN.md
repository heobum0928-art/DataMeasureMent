---
phase: quick-260818-vih
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Halcon/Display/HalconDisplayService.cs
autonomous: true
requirements: [VIH-01, VIH-02, VIH-03]

must_haves:
  truths:
    - "`RenderDatumOverlay`(HEAD `bef801f` L856–1099)의 7개 렌더 구역이 각각 private 메서드로 통째 이동하고, 옮겨간 199줄은 선행 공백을 제거하면 원본과 **바이트 단위로 동일**하다 (토큰 변경 0건)"
    - "파일 전체 라인 멀티셋 대조에서 **삭제된 줄이 0줄**이다 — `comm -23 <(base 정규화·정렬) <(after 정규화·정렬)` 가 빈 출력. 순수 추출이므로 추가만 있고 삭제는 없어야 한다"
    - "구역 메서드 **호출 순서가 원본 구역 순서와 1:1** 이다 (Slot → Line2 → Circle → Horizontal → RefOrigin → Detected → FindResult → DetectFail). z-order 가 곧 화면이므로 순서가 바뀌면 오버레이가 가려진다"
    - "`HOperatorSet.SetColor` 색상 리터럴 12종의 파일 전역 카운트가 착수 전 실측과 동일하다: red 7 / cyan 6 / yellow 6 / green 4 / magenta 3 / lime green 2 / orange 2 / blue 2 / slate blue 2 / gray 1 / white 1 / #90EE90 1. **색상 문자열이 한 글자만 바뀌어도 HALCON 이 예외를 던지고 `catch { }` 가 삼켜 조용히 렌더가 사라진다** — 빌드도 통과하고 로그도 안 남으므로 이 카운트가 유일한 방어선"
    - "HALCON 호출 카운트 8종이 파일 전역에서 착수 전과 동일하다: SetColor 25 / SetLineWidth 20 / DispRectangle2 7 / DispCircle 6 / DispLine 21 / DispCross 3 / SetTposition 5 / WriteString 5"
    - "라벨 문자열 8종이 각각 정확히 1건 유지된다: `\"L1\")` `\"L2\")` `\"Vert\")` `\"Circle\")` `\"H-A\")` `\"H-B\")` `\"Datum Origin\")` `\"DETECT FAIL: \"`"
    - "바깥 `try { ... } catch { }` 는 `RenderDatumOverlay` 에 그대로 남아 7개 호출 전체를 감싼다 — 예외 발생 시 이후 구역이 중단되는 동작이 원본과 동일하다. 구역별로 try/catch 를 새로 만들지 않는다"
    - "구역 7(`DETECT FAIL`) 안쪽의 기존 `try { ... } catch { }` 는 **통째로** 함께 이동한다 — 경계를 쪼개지 않는다"
    - "구역 간 공유 지역변수 3개(`color` string=참조형 읽기전용 / `lineWidth` int=값형 읽기전용 / `cthHideRois` bool=값형 읽기전용)는 전부 **재대입이 없어 `ref` 불필요**, 값 전달이다. 파라미터 이름을 호출자 지역변수와 글자 그대로 동일하게 지어 본문 토큰 변경을 0으로 만든다"
    - "null 가드(`if (datum == null) return;`) + 색상 결정(if-else) + `cthHideRois` 계산 + 바깥 try/catch 는 `RenderDatumOverlay` 에 잔류한다 — 추출 대상이 아니다"
    - "`CircleTwoHorizontal: Line1/Vertical 모두 렌더하지 않음 (의도적)` / `RenderDatumOverlay 슬롯 분기:` / `cthHideRois 가드:` / `z-order` 계열 등 '왜'를 남긴 기존 주석이 **삭제 0건**으로 로직을 따라 이동한다"
    - "msbuild Debug|x64 성공 + 경고가 baseline 12줄(CS0618×10 + CS0162×2)과 동일 — 신규 CS0219/CS0168/CS0177/CS0165 0건"
    - "파일 전체 코드 삼항(`?:`) **0건 유지**(착수 전 0건), `=> ` 0건 유지(C# 7.2)"
    - "범위 밖 무접촉: `RenderCircleStripOverlay` / `RenderDatumFindResult` / `DrawDirectionArrow` / `DrawRoiLabel` / `DrawRoiLabelAt` / `RenderRawEdgePoints` / `DrawExtendedLine` 등 같은 파일 다른 메서드 및 다른 모든 파일"
    - "`HalconDisplayService.cs` 외 어떤 파일도 스테이징/커밋되지 않는다 — 특히 `WPF_Example/DatumMeasurement.csproj` 의 로컬 미커밋 변경(Debug OutputPath=D:\\Data\\, Release DefineConstants 의 SIMUL_MODE)이 그대로 unstaged 로 남는다"
  artifacts:
    - path: "WPF_Example/Halcon/Display/HalconDisplayService.cs"
      provides: "private 구역 메서드 7개 신규 + RenderDatumOverlay 본문이 가드/색상결정/순차호출만 남은 형태"
      contains: "private void RenderDatumSlotRoi("
    - path: ".planning/quick/260818-vih-halcondisplayservice-renderdatumoverlay-/260818-vih-SUMMARY.md"
      provides: "구역별 바이트 동치 diff 결과 + 라인 멀티셋 대조 + 색상 리터럴/HALCON 호출 카운트 전후표 + z-order 호출순서 증명 + UAT 요청"
  key_links:
    - from: "RenderDatumOverlay try 블록 (색상/cthHideRois 결정 직후)"
      to: "RenderDatumSlotRoi → RenderDatumLine2Roi → RenderDatumCircleRoi → RenderDatumHorizontalRois → RenderDatumRefOriginCross → RenderDatumDetectedOverlay → RenderDatumFindResult → RenderDatumDetectFailLabel"
      via: "구역 순서 그대로의 순차 호출 8줄 (z-order 보존)"
      pattern: "^[[:space:]]*RenderDatumSlotRoi\\(window, datum\\);"
    - from: "RenderDatumCircleRoi / RenderDatumHorizontalRois"
      to: "color / lineWidth / cthHideRois 파라미터"
      via: "호출자 지역변수와 동일 이름의 값 전달 파라미터 (재대입 없음 → ref 불필요)"
      pattern: "^[[:space:]]*RenderDatumCircleRoi\\(window, datum, color, lineWidth, cthHideRois\\);"
    - from: "RenderDatumDetectFailLabel 본문"
      to: "내부 try { EnsureFontInitialized … WriteString } catch { }"
      via: "기존 내부 try/catch 를 통째로 이동 (경계 분할 금지)"
      pattern: "^[[:space:]]*private void RenderDatumDetectFailLabel\\(HWindow window, DatumConfig datum\\)"
---

<objective>
`WPF_Example/Halcon/Display/HalconDisplayService.cs` 의
`public void RenderDatumOverlay(HWindow window, DatumConfig datum, bool isSelected, bool isEditMode = false)`
(HEAD `bef801f` 기준 **L856–1099, 244줄**) 을 **순수 Extract Method** 로 7개 private 구역 메서드로 쪼갠다.

Purpose: 이 메서드는 티칭 화면에서 사용자가 **눈으로 보는** Datum 오버레이를 그린다.
사용자 원문(오늘 반복 강조) — **"제일중요한건 기존기능 영향 절대없게"**.
사용자는 지금 Datum 재티칭 작업 중이고 **내일 아침 이 화면을 쓴다.** 지금은 자고 있어 실기 확인이 불가능하다.
→ 이 작업은 "코드 개선"이 아니라 **의미 보존 변환(behavior-preserving transformation)** 이며,
   **정적 증거만으로 무회귀를 증명**해야 한다.

Output: 같은 파일 1개. private 메서드 7개 신규 + `RenderDatumOverlay` 본문이 짧은 순차 호출 형태로 축소. 커밋 2개.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md

**착수 전 필수 확인 (30초). 하나라도 다르면 즉시 중단하고 사용자에게 보고 — 아래 모든 줄번호가 무효화된다:**
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Halcon/Display/HalconDisplayService.cs
git rev-parse --short HEAD          # 기대: bef801f
git status --porcelain              # 기대: " M WPF_Example/DatumMeasurement.csproj" 단 1줄 (+ 미추적 .planning/quick/* 디렉터리)
git status --porcelain -- $F        # 기대: 출력 없음 (clean)
wc -l $F                            # 기대: 1177
sed -n '856p;885p;917p;930p;951p;980p;1001p;1066p;1099p' $F
```
기대 출력(순서대로):
```
        public void RenderDatumOverlay(HWindow window, DatumConfig datum, bool isSelected, bool isEditMode = false)
                // RenderDatumOverlay 슬롯 분기: AlgorithmType 별로 그릴 슬롯을 분기.
                // Line2 Rectangle2 는 TwoLineIntersect 에서만 렌더 (Circle/Vertical-TwoHorizontal 은 Line2 미사용)
                // Circle ROI 검색 영역 (CircleTwoHorizontal 일 때만 렌더, Line1/Line2 와 동일 색)
                // Horizontal A/B ROI Rectangle2 (CircleTwoHorizontal + VerticalTwoHorizontal 공용)
                // Draw reference origin cross if configured
                // 검출 라인 2개 + 교점 오버레이 (TryTeachDatum 성공 시에만, 기존 cyan/blue/magenta 팔레트는 건드리지 않음)
                // Datum 검출 실패 시 'DETECT FAIL' 적색 라벨 렌더.
        }
```

**⚠ 워킹트리 오염 주의 (이번 작업 최대 사고 위험):**
`WPF_Example/DatumMeasurement.csproj` 에 **커밋하면 안 되는 로컬 설정**이 떠 있다 —
Debug `OutputPath=D:\Data\`, Release `DefineConstants` 의 `SIMUL_MODE`.
저장소에 들어가면 **현장 배포본이 시뮬레이션 모드로 나간다.**
→ **`git add -A` / `git add .` / `git commit -a` 절대 금지.** 반드시 대상 파일 1개만 경로로 스테이징한다.
</context>

<ground_rules>
## 이 플랜 전체에 적용되는 절대 규칙

### G-1. 허용되는 변환은 정확히 1종 — "잘라내서 새 메서드에 붙이기"
- 구역 블록을 그대로 잘라 새 private 메서드 본문으로 옮기고, 원래 자리에 호출 1줄을 넣는다. 끝.
- **그 외 어떤 편집도 금지:**
  - 그리기 순서(z-order) 변경 / 조건식 정리 / if-else 병합 / 조기 return 도입 금지
  - 색상 문자열 / 좌표 계산식 / 라벨 문자열 / 숫자 상수 수정 금지 (한 글자도)
  - 기존 지역변수·필드 리네임 금지 (**리네임 0건이 바이트 동치의 전제다**)
  - try/catch 경계 이동·분할·신설 금지 (§G-3)
  - 방어 코드 / null 체크 / 로그 / 예외 처리 추가 금지
  - 주석 삭제 금지 (§G-5)
  - **범위 확장 금지** — 같은 파일의 `RenderCircleStripOverlay` / `RenderDatumFindResult` / `DrawDirectionArrow` /
    `DrawRoiLabel` / `DrawRoiLabelAt` / `RenderRawEdgePoints` / `DrawExtendedLine` 및 다른 파일 전부 **무접촉**
- **동작이 조금이라도 바뀔 것 같은 부분은 추출하지 말고 원형 유지하고, 그 판단 근거를 SUMMARY 에 적는다.**

### G-2. 구역 경계 확정표 (BASE = `bef801f`, 파일 1177줄) — 이 표가 유일한 진실
| # | 구역 | BASE 줄범위 | 줄수 | 신규 메서드 | 파라미터 |
|---|------|-------------|------|-------------|----------|
| R1 | Line1 / Vertical 슬롯 (+ CircleTwoHorizontal 미렌더 주석) | 885–915 | 31 | `RenderDatumSlotRoi` | `(HWindow window, DatumConfig datum)` |
| R2 | Line2 Rectangle2 | 917–928 | 12 | `RenderDatumLine2Roi` | `(HWindow window, DatumConfig datum)` |
| R3 | Circle ROI + pre-teach Strip | 930–949 | 20 | `RenderDatumCircleRoi` | `(HWindow window, DatumConfig datum, string color, int lineWidth, bool cthHideRois)` |
| R4 | Horizontal A/B ROI | 951–978 | 28 | `RenderDatumHorizontalRois` | `(HWindow window, DatumConfig datum, string color, int lineWidth, bool cthHideRois)` |
| R5 | RefOrigin 십자 (magenta) | 980–999 | 20 | `RenderDatumRefOriginCross` | `(HWindow window, DatumConfig datum)` |
| R6 | 검출 결과 오버레이 (LastTeachSucceeded 블록 전체) | 1001–1060 | 60 | `RenderDatumDetectedOverlay` | `(HWindow window, DatumConfig datum)` |
| R7 | DETECT FAIL 라벨 (내부 try/catch 포함) | 1066–1093 | 28 | `RenderDatumDetectFailLabel` | `(HWindow window, DatumConfig datum)` |

**이동 총합 199줄.** 각 구역의 **첫 줄은 그 구역 설명 주석**이며, 그 주석도 함께 새 메서드 본문 첫 줄로 옮긴다
(메서드 선언 **위**가 아니라 **본문 안 첫 줄** — 구역별 diff 앵커가 본문 안에 있어야 하기 때문).

**잔류(추출 금지):**
- L856–884: 시그니처 / `if (datum == null) return;` / `color`·`lineWidth` if-else / `try {` / `SetColor`·`SetLineWidth` / `cthHideRois` 계산 + 그 설명 주석 3줄
- L916 / L929 / L950 / L979 / L1000 / L1061 / L1065: 구역 사이 빈 줄 — 그대로 둔다
- L1062–1064: `RenderDatumFindResult 를 LastTeachSucceeded 블록 밖에서 호출.` 주석 2줄 + `RenderDatumFindResult(window, datum);` — **이미 별도 메서드 호출이므로 그 자리 그대로 유지**
- L1094–1099: 바깥 `}` / `catch { // Suppress display errors }` / `}` — **그대로 유지**

### G-3. try/catch 는 통째로만 — 이 파일의 조용한 실패 메커니즘
- 이 파일은 `catch { }` 삼킴이 관습이다(파일 전역 `catch` 28건).
  **바깥 `try` 는 `RenderDatumOverlay` 에 그대로 남아 8개 호출 전체를 감싼다.**
  호출된 메서드에서 예외가 나면 그대로 호출자로 전파되어 같은 `catch` 에 잡히고 **이후 구역이 중단된다 — 원본과 동일 동작**이다.
- **구역 메서드 안에 새 try/catch 를 만들지 말 것.** 만들면 원래 중단되던 지점 이후가 계속 그려져 **화면이 달라진다.**
- R7 내부의 기존 `try { … } catch { … }`(BASE L1071–1092)는 **통째로** R7 과 함께 이동한다.

### G-4. 파라미터 = 호출자 지역변수와 **글자 그대로 같은 이름** (동치 증명의 핵심 장치)
`window` / `datum` / `color` / `lineWidth` / `cthHideRois` — 전부 동일 이름.
그러면 옮겨간 199줄은 **토큰 변경 0건**이 되고, "선행 공백만 제거한 diff 가 비어 있음"이 곧 바이트 동치 증명이 된다.

| 변수 | 형 | 구역 안 취급 | 전달 방식 | 근거 |
|------|----|--------------|-----------|------|
| `window` | `HWindow` (참조형) | 인자로만 사용 | 값 전달 | 재대입 없음. **HALCON 창 색상/선굵기 상태는 window 객체에 남으므로** 호출 전 `SetColor` 상태가 구역 메서드 안까지 그대로 이어진다(R1/R2 가 이에 의존) |
| `datum` | `DatumConfig` (참조형) | 읽기만 | 값 전달 | 재대입 없음 |
| `color` | `string` (참조형·불변) | 읽기만 | 값 전달 | 재대입 없음 |
| `lineWidth` | `int` (값형) | 읽기만 | 값 전달 | **읽기만 하므로 `ref` 불필요** |
| `cthHideRois` | `bool` (값형) | 읽기만 | 값 전달 | **읽기만 하므로 `ref` 불필요** |

> `out`/`ref` 파라미터는 이 메서드에 **하나도 없다**(착수 전 확인 완료). 값형 2개도 구역 안에서 대입되지 않으므로 값 전달로 충분하다.
> `_isFontInitialized` / `_normalFontName` 은 클래스 필드이므로 인스턴스 메서드인 새 메서드에서 그대로 접근된다 — 파라미터로 만들지 말 것.

### G-5. 보존 대상 주석 — 삭제 0건, 로직 따라 이동만
| 앵커 | BASE 위치 | 이동 후 |
|------|-----------|---------|
| `RenderDatumOverlay 슬롯 분기:` 설명 4줄 | 885–888 | `RenderDatumSlotRoi` 본문 첫 4줄 |
| `CircleTwoHorizontal: Line1/Vertical 모두 렌더하지 않음 (의도적).` | 915 | `RenderDatumSlotRoi` 본문 **마지막 줄** |
| `cthHideRois 가드:` 2건 | 931, 952 | 각각 R3 / R4 본문 안 동일 상대 위치 |
| `cthHideRois` 계산부 설명 3줄 | 878–880 | **`RenderDatumOverlay` 에 잔류**(계산이 남으므로) |
| `pre-teach Strip 사각형 stepCount 개 정적 시각화 (z-order: ROI 경계 위)` | 947 | R3 본문 안 |
| `z-order 정렬: Raw edge points 먼저 …` / `z-order: 검출 원 그린 후 center cross (top) …` | 1027, 1038 | R6 본문 안 |
| `"light green" 비표준 색상명 → HALCON SetColor 예외 → catch swallow → 미표시 결함.` | 1042 | R6 본문 안 |
| `색상: "red" 표준명 (비표준명은 SetColor catch swallow 로 silent 미표시 위험).` | 1068 | R7 본문 안 |
| `RenderDatumFindResult 를 LastTeachSucceeded 블록 밖에서 호출.` 2줄 | 1062–1063 | **`RenderDatumOverlay` 에 잔류** |

### G-6. 코딩 컨벤션 (하드)
- **삼항 `?:` 금지** — if-else 만. 착수 전 파일 0건 → **0 유지**
- **C# 7.2 only** — switch expression, pattern matching switch, nullable reference types, record, expression-bodied 신규 멤버 전부 금지. `=> ` 착수 전 0건 → **0 유지**
- **신규 메서드 선언은 Allman** — 이 파일의 메서드/`if` 는 여는 중괄호가 다음 줄이다(실측: L856/857, L889/890). 옮겨오는 본문 내부 스타일은 **원본 그대로 유지**(재포맷 금지, diff 노이즈 = 대조 방해)
- 기존 지역변수 리네임 금지. 신규 파라미터도 호출자 이름과 동일하게 유지(§G-4)가 헝가리언보다 우선한다 — 리네임하면 바이트 동치가 깨진다
- 신규 주석 접두 `//260818 hbk`, 비자명한 "왜"만

### G-7. 신규 주석 금칙어 (자기모순 검증 방지 — 최근 2개 plan 이 연속으로 여기서 blocker 를 받았다)
새로 쓰는 주석/코드에 아래 문자열을 **넣지 말 것**. 검증식이 영구 실패한다.
1. **구역 diff 앵커 7종** (각각 파일 내 `==1` 이어야 하며 검증식이 이를 요구한다):
   `RenderDatumOverlay 슬롯 분기` / `Line2 Rectangle2 는 TwoLineIntersect 에서만 렌더` / `Circle ROI 검색 영역` /
   `Horizontal A/B ROI Rectangle2` / `Draw reference origin cross if configured` /
   `검출 라인 2개 + 교점 오버레이` / `Datum 검출 실패 시`
2. **`HOperatorSet.` 접두 문자열** — 8종 호출 카운트가 깨진다
3. **큰따옴표로 감싼 색상 이름** (`"red"` `"cyan"` `"yellow"` `"green"` `"magenta"` `"blue"` `"gray"` `"white"` `"orange"` `"lime green"` `"slate blue"` `"#90EE90"`) — 색상 리터럴 카운트가 깨진다.
   기존 주석에 이미 들어 있는 것들(L1042/L1068 등)은 **그대로 이동**시키므로 문제없다. **새로 쓰지만 말 것.**
4. **`catch`** 라는 단어 — 파일 전역 `catch` 28 카운트가 깨진다 (기존 주석의 `catch swallow` 는 이동이라 무해)
5. 라벨 문자열 8종(`"L1")` `"L2")` `"Vert")` `"Circle")` `"H-A")` `"H-B")` `"Datum Origin")` `"DETECT FAIL: "`)
6. `?` 뒤에 같은 줄에서 `:` 가 오는 형태 / `=> `

### G-8. 빌드 규칙
- 앱이 `D:\Data\` 에서 실행 중일 수 있다 → **프로세스 종료 절대 금지.** 스크래치 `OutputPath` 로 컴파일만 검증
- **`//p:` 금지, `-p:` 사용** (`/` 섞이면 Git Bash 가 `MSB1001` 로 죽는다)
- **경고 baseline = 12줄 (CS0618×10 + CS0162×2).** "경고 0" 을 통과 기준으로 쓰면 항상 거짓 실패
- 편집 구역(L856–1099)에 `#if` **0개**임을 착수 전 확인 완료 → 비-SIMUL 빌드 불필요

```bash
# ⚠ OutputPath 의 후행 백슬래시는 반드시 `\\` 로 쓸 것.
#   `"$SCR\vih-simul\"` 는 `\"` 가 닫는 따옴표를 이스케이프해 bash 문법 에러(unexpected EOF)가 나고
#   빌드가 아예 실행되지 않는다 — 직전 ukh plan 의 올바른 형태를 그대로 따른다.
MSB="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad"
"$MSB" 'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' \
  -p:Configuration=Debug -p:Platform=x64 -p:OutputPath="$SCR\\vih-simul\\" \
  -t:Rebuild -v:minimal -nologo
```
파일 잠김으로 실패하면 OutputPath 를 새 이름(예: `"$SCR\\vih-simul2\\"`, **후행 `\\` 유지**)으로 바꿔 재시도. 그래도 안 되면 **죽이지 말고 사용자에게 보고.**

### G-9. 셸 변수는 호출 사이에 살아남지 않는다
Bash 호출마다 셸이 새로 뜬다. `$F` / `$SCR` / `$BASE` 를 쓰는 **모든 블록의 첫 줄에서 다시 정의**할 것:
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Halcon/Display/HalconDisplayService.cs
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad"
BASE=bef801f   # 착수 시점 HEAD — 모든 diff 대조의 유일한 기준점
```
정의 없이 실행하면 경로가 빈 문자열이 되어 **조용히 오탐**한다.

### G-10. Grep 규칙
- **모든 grep 에 대상 파일 경로 명시** (없으면 stdin 대기로 멈춤)
- 개수 기준은 `^[[:space:]]*` 앵커 또는 `-F` 코드 토큰으로 좁힌다
- **삼항 검출은 줄 단위**: `grep -nE '\?[^?:]*:' <path> | grep -vE '\?\?|\?\.' | wc -l` → **0**.
  `-o`(매치 단위)로 바꾸면 문자열 리터럴에서 오탐이 난다
- Task 1 편집 후 R5–R7 의 줄번호가 밀린다 → **Task 2 는 BASE 줄번호로 파일을 찾지 말고 반드시 앵커 `grep -n` 으로 위치를 구할 것**(diff 의 base 쪽만 `git show $BASE:$F | sed -n 'A,Bp'` 로 고정 사용)
</ground_rules>

<tasks>

<task type="auto">
  <name>Task 1: ROI 구역 4개(R1–R4, BASE L885–978) → 구역 메서드 4개 추출</name>
  <files>WPF_Example/Halcon/Display/HalconDisplayService.cs</files>
  <action>
**0단계 — 기준점 고정 (Task 1·2 통틀어 1회만 실행):**
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Halcon/Display/HalconDisplayService.cs
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad"
BASE=$(git rev-parse --short HEAD)               # bef801f 이어야 함. 다르면 즉시 중단
[ -f "$SCR/disp-git-baseline.txt" ] || git status --porcelain > "$SCR/disp-git-baseline.txt"
# 라인 멀티셋 baseline (전체 파일, 선행/후행 공백 제거 후 정렬)
git show $BASE:$F | sed 's/^[[:space:]]*//; s/[[:space:]]*$//' | sort > "$SCR/disp-base-lines.txt"
```
그리고 **G-8 빌드를 착수 전 상태에서 1회** 돌려 경고 줄을 `$SCR/disp-baseline-warn.txt` 에 저장한다.
이후 모든 경고 비교는 기억이 아니라 이 파일 기준.

---

**1단계 — R1–R4 를 잘라내고 그 자리에 호출 4줄을 넣는다.**

BASE L885–915 / L917–928 / L930–949 / L951–978 (§G-2 표) 을 **각 구역 통째로** 잘라내고,
각 구역이 있던 자리에 아래 호출 1줄씩을 넣는다(들여쓰기 16칸, **구역 순서 그대로**):
```csharp
                RenderDatumSlotRoi(window, datum);

                RenderDatumLine2Roi(window, datum);

                RenderDatumCircleRoi(window, datum, color, lineWidth, cthHideRois);

                RenderDatumHorizontalRois(window, datum, color, lineWidth, cthHideRois);
```
**구역 사이 빈 줄(L916/929/950)과 L979 이후는 손대지 않는다. L884 위(가드·색상·cthHideRois·`try {`)도 손대지 않는다.**

---

**2단계 — 신규 메서드 4개를 `RenderDatumOverlay` 본체 닫는 `}` (BASE L1099) **바로 아래**,
`// Datum ROI 라벨 그리기 …` 주석(BASE L1101) **앞**에 §G-2 표 순서대로 추가한다.**
본문은 잘라낸 줄을 **각 줄 앞 공백 4칸씩만 줄여서**(16→12칸 기준) 붙인다. **토큰은 단 하나도 바꾸지 않는다.**

```csharp
        //260818 hbk Extract Method: RenderDatumOverlay 의 슬롯 분기 구역을 그대로 옮긴 것.
        //  창(window) 의 색상·선굵기 상태는 호출 전에 이미 설정돼 있고 그 상태가 이어진다 — 여기서 다시 설정하면 안 된다.
        private void RenderDatumSlotRoi(HWindow window, DatumConfig datum)
        {
            <BASE L885–915 (31줄) 를 4칸 dedent 해서 그대로 붙여넣는다>
        }

        //260818 hbk Extract Method: Line2 Rectangle2 구역을 그대로 옮긴 것. 색상 상태는 호출자에서 이어진다.
        private void RenderDatumLine2Roi(HWindow window, DatumConfig datum)
        {
            <BASE L917–928 (12줄)>
        }

        //260818 hbk Extract Method: Circle ROI 구역을 그대로 옮긴 것.
        //  cthHideRois / color / lineWidth 는 호출자 지역변수와 같은 이름의 값 전달 파라미터다(구역 안에서 읽기만 하므로 ref 불필요).
        private void RenderDatumCircleRoi(HWindow window, DatumConfig datum, string color, int lineWidth, bool cthHideRois)
        {
            <BASE L930–949 (20줄)>
        }

        //260818 hbk Extract Method: Horizontal A/B ROI 구역을 그대로 옮긴 것. 파라미터 취급은 위와 동일.
        private void RenderDatumHorizontalRois(HWindow window, DatumConfig datum, string color, int lineWidth, bool cthHideRois)
        {
            <BASE L951–978 (28줄)>
        }
```

**절대 하지 말 것:** 구역 순서 변경, 조건식 정리, `if` 병합, 색상/좌표/라벨 문자열 수정,
구역 메서드 안에 try/catch 신설, `datum` 필드 접근을 지역변수로 캐싱, 재포맷.
§G-7 금칙어를 새 주석에 넣지 말 것.

---

**3단계 — 빌드 + 정적 검증 (커밋 전).** verify 블록 **1·2·3 + G-8 빌드**를 여기서 실행한다.
verify 블록 **4(HYGIENE)는 여기서 실행하지 말 것** — `git show HEAD` 로 커밋 결과를 검사하므로
커밋 전에 돌리면 직전 커밋(`bef801f`)을 보고 오판한다.

**4단계 — 커밋. `git add -A` 금지, 대상 파일만:**
```bash
cd /c/Info/Project/DataMeasurement
git add WPF_Example/Halcon/Display/HalconDisplayService.cs
git diff --cached --name-only          # 정확히 1줄이어야 함
git commit -m "refactor(260818-vih): RenderDatumOverlay ROI 4구역을 구역 메서드로 추출 (순수 이동, 렌더 무변경)"
git status --porcelain -- WPF_Example/DatumMeasurement.csproj   # 여전히 " M" (unstaged) 여야 함
```

**5단계 — 커밋 후 위생 검증.** verify 블록 **4(HYGIENE)** 를 여기서 실행한다(블록 안에서 `SCR` 재정의 필수).
  </action>
  <verify>
    <automated>
# [1] 구조 — 신규 메서드 4개 + 호출부 4곳 + z-order(호출 순서) 보존
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Halcon/Display/HalconDisplayService.cs && \
[ "$(grep -cE '^[[:space:]]*private void RenderDatumSlotRoi\(HWindow window, DatumConfig datum\)$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*private void RenderDatumLine2Roi\(HWindow window, DatumConfig datum\)$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*private void RenderDatumCircleRoi\(HWindow window, DatumConfig datum, string color, int lineWidth, bool cthHideRois\)$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*private void RenderDatumHorizontalRois\(HWindow window, DatumConfig datum, string color, int lineWidth, bool cthHideRois\)$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*RenderDatumSlotRoi\(window, datum\);$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*RenderDatumLine2Roi\(window, datum\);$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*RenderDatumCircleRoi\(window, datum, color, lineWidth, cthHideRois\);$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*RenderDatumHorizontalRois\(window, datum, color, lineWidth, cthHideRois\);$' $F)" = "1" ] && \
echo "== z-order: 호출 4줄이 구역 순서대로 오름차순 ==" && \
A=$(grep -nE '^[[:space:]]*RenderDatumSlotRoi\(window, datum\);$' $F | cut -d: -f1) && \
B=$(grep -nE '^[[:space:]]*RenderDatumLine2Roi\(window, datum\);$' $F | cut -d: -f1) && \
C=$(grep -nE '^[[:space:]]*RenderDatumCircleRoi\(' $F | cut -d: -f1) && \
D=$(grep -nE '^[[:space:]]*RenderDatumHorizontalRois\(' $F | cut -d: -f1) && \
E=$(grep -nE '^[[:space:]]*RenderDatumFindResult\(window, datum\);$' $F | cut -d: -f1) && \
[ "$A" -lt "$B" ] && [ "$B" -lt "$C" ] && [ "$C" -lt "$D" ] && [ "$D" -lt "$E" ] && \
echo "== 잔류 확인: 가드 / 색상 결정 / cthHideRois / 바깥 try-catch ==" && \
# ⚠ 이 가드는 파일에 2건이다 — L748 RenderCircleStripOverlay / L858 RenderDatumOverlay.
#   전역 카운트는 2 를 요구해 **다른 메서드(L748)의 가드가 지워지지 않았음**까지 함께 보증하고,
#   RenderDatumOverlay 자신의 가드는 선언 직후 2줄 안에 있는지로 따로 확인한다.
#   (전역을 1 로 쓰면 영구 실패 + "맞추려고 L748 을 지우는" 2차 사고가 난다)
[ "$(grep -cE '^[[:space:]]*if \(datum == null\) return;$' $F)" = "2" ] && \
[ "$(grep -A2 -E '^[[:space:]]*public void RenderDatumOverlay\(' $F | grep -cE '^[[:space:]]*if \(datum == null\) return;$')" = "1" ] && \
[ "$(grep -cF 'bool cthHideRois = (datum.AlgorithmTypeEnum == EDatumAlgorithm.CircleTwoHorizontal)' $F)" = "1" ] && \
[ "$(grep -cF '// Suppress display errors' $F)" -ge 1 ] && \
echo "T1 STRUCTURE PASS"
    </automated>
    <automated>
# [2] ⭐바이트 동치 증명 — R1~R4 각 구역이 선행공백 제거 후 원본과 완전히 같은가
# ⚠ 앵커 유일성(==1)을 먼저 못박고 diff 로 넘어간다. 앵커가 2건이면 sed 범위가 엉뚱하게 넓어진다(260818-ukh 사고).
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Halcon/Display/HalconDisplayService.cs && \
BASE=bef801f && \
chk() { # $1=앵커 $2=줄수 $3=BASE시작 $4=BASE끝
  [ "$(grep -cF "$1" $F)" = "1" ] || { echo "ANCHOR NOT UNIQUE: $1"; return 1; }
  L=$(grep -nF "$1" $F | cut -d: -f1)
  diff <(git show $BASE:$F | sed -n "$3,$4p" | sed 's/^[[:space:]]*//') \
       <(sed -n "${L},$((L+$2-1))p" $F | sed 's/^[[:space:]]*//') || return 1
  echo "  OK $1 ($2 lines)"
} && \
chk 'RenderDatumOverlay 슬롯 분기' 31 885 915 && \
chk 'Line2 Rectangle2 는 TwoLineIntersect 에서만 렌더' 12 917 928 && \
chk 'Circle ROI 검색 영역' 20 930 949 && \
chk 'Horizontal A/B ROI Rectangle2' 28 951 978 && \
echo "T1 BYTE-EQUIV PASS (R1-R4, diff empty)"
    </automated>
    <automated>
# [3] ⭐라인 멀티셋 대조(삭제된 줄 0) + 색상 리터럴 전수 + HALCON 호출 카운트 8종
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Halcon/Display/HalconDisplayService.cs && \
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad" && \
echo "== 삭제된 줄 0 (순수 추출이므로 추가만 있어야 함) ==" && \
[ "$(comm -23 "$SCR/disp-base-lines.txt" <(sed 's/^[[:space:]]*//; s/[[:space:]]*$//' $F | sort) | wc -l)" = "0" ] && \
echo "== 색상 리터럴 전수 (한 글자만 틀려도 HALCON 이 조용히 렌더를 버린다) ==" && \
[ "$(grep -cE 'SetColor\([^)]*"red"\)' $F)" = "7" ] && \
[ "$(grep -cE 'SetColor\([^)]*"cyan"\)' $F)" = "6" ] && \
[ "$(grep -cE 'SetColor\([^)]*"yellow"\)' $F)" = "6" ] && \
[ "$(grep -cE 'SetColor\([^)]*"green"\)' $F)" = "4" ] && \
[ "$(grep -cE 'SetColor\([^)]*"magenta"\)' $F)" = "3" ] && \
[ "$(grep -cE 'SetColor\([^)]*"lime green"\)' $F)" = "2" ] && \
[ "$(grep -cE 'SetColor\([^)]*"orange"\)' $F)" = "2" ] && \
[ "$(grep -cE 'SetColor\([^)]*"blue"\)' $F)" = "2" ] && \
[ "$(grep -cE 'SetColor\([^)]*"slate blue"\)' $F)" = "2" ] && \
[ "$(grep -cE 'SetColor\([^)]*"gray"\)' $F)" = "1" ] && \
[ "$(grep -cE 'SetColor\([^)]*"white"\)' $F)" = "1" ] && \
[ "$(grep -cE 'SetColor\([^)]*"#90EE90"\)' $F)" = "1" ] && \
echo "== HALCON 호출 카운트 8종 ==" && \
[ "$(grep -c 'HOperatorSet.SetColor' $F)" = "25" ] && \
[ "$(grep -c 'HOperatorSet.SetLineWidth' $F)" = "20" ] && \
[ "$(grep -c 'HOperatorSet.DispRectangle2' $F)" = "7" ] && \
[ "$(grep -c 'HOperatorSet.DispCircle' $F)" = "6" ] && \
[ "$(grep -c 'HOperatorSet.DispLine' $F)" = "21" ] && \
[ "$(grep -c 'HOperatorSet.DispCross' $F)" = "3" ] && \
[ "$(grep -c 'HOperatorSet.SetTposition' $F)" = "5" ] && \
[ "$(grep -c 'HOperatorSet.WriteString' $F)" = "5" ] && \
[ "$(grep -c 'catch' $F)" = "28" ] && \
echo "== 라벨 문자열 8종 각 1건 ==" && \
for s in '"L1")' '"L2")' '"Vert")' '"Circle")' '"H-A")' '"H-B")' '"Datum Origin")' '"DETECT FAIL: "'; do \
  [ "$(grep -cF "$s" $F)" = "1" ] || { echo "LABEL BROKEN: $s"; exit 1; }; done && \
echo "== 헬퍼 호출 카운트 (구역 안 호출이 통째로 따라갔는지) ==" && \
[ "$(grep -cE '^[[:space:]]*DrawRoiLabel\(window' $F)" = "5" ] && \
[ "$(grep -cE '^[[:space:]]*DrawRoiLabelAt\(' $F)" = "5" ] && \
[ "$(grep -cE '^[[:space:]]*RenderCircleStripOverlay\(window, datum\);$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*RenderDatumFindResult\(window, datum\);$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*DrawExtendedLine\(window,' $F)" = "2" ] && \
[ "$(grep -c 'RenderRawEdgePoints(window, datum\.' $F)" = "6" ] && \
[ "$(grep -cE '^[[:space:]]*EnsureFontInitialized\(window\);$' $F)" = "6" ] && \
echo "T1 INVARIANT COUNTS PASS"
    </automated>
    <automated>
# [4] HYGIENE — ⚠ 반드시 **커밋 이후** 실행 (git show HEAD 로 커밋 결과 검사). SCR 재정의 필수.
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Halcon/Display/HalconDisplayService.cs && \
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad" && \
echo "== 코드 삼항 0건 유지 ==" && \
[ "$(grep -nE '\?[^?:]*:' $F | grep -vE '\?\?|\?\.' | wc -l)" = "0" ] && \
echo "== C# 7.2: expression-bodied / lambda 0건 유지 ==" && [ "$(grep -c '=> ' $F)" = "0" ] && \
echo "== 커밋에 대상 파일만 ==" && \
[ "$(git show --stat --name-only --format= HEAD | grep -v '^$' | wc -l)" = "1" ] && \
git show --name-only --format= HEAD | grep -q 'HalconDisplayService.cs' && \
[ "$(git show --name-only --format= HEAD | grep -c 'DatumMeasurement.csproj')" = "0" ] && \
echo "== csproj 로컬 변경이 unstaged 로 그대로 ==" && \
git status --porcelain -- WPF_Example/DatumMeasurement.csproj | grep -q '^ M' && \
echo "== 워킹트리 dirty 집합이 baseline 대비 대상 파일 하나만 변동 ==" && \
diff <(cut -c4- "$SCR/disp-git-baseline.txt" | sort) <(git status --porcelain | cut -c4- | sort) | grep -c '^[<>]' | grep -qE '^[01]$' && \
echo "T1 HYGIENE PASS"
    </automated>
    <automated>G-8 SIMUL 빌드 → 성공 + 경고가 $SCR/disp-baseline-warn.txt 와 동일(12줄: CS0618×10 + CS0162×2). 신규 CS0219/CS0168/CS0177/CS0165 가 1건이라도 생기면 FAIL</automated>
  </verify>
  <done>
`RenderDatumSlotRoi` / `RenderDatumLine2Roi` / `RenderDatumCircleRoi` / `RenderDatumHorizontalRois` 4개 신규 + 호출 4줄.
옮겨간 91줄(31+12+20+28)이 선행공백 제거 후 원본과 **diff 0**(토큰 변경 0건).
호출 4줄의 줄번호가 구역 순서대로 오름차순 → z-order 보존.
삭제된 줄 0(라인 멀티셋), 색상 리터럴 12종 + HALCON 호출 8종 + 라벨 8종 전부 착수 전과 동일.
바깥 try/catch·가드·색상 결정·`cthHideRois` 계산은 `RenderDatumOverlay` 에 잔류.
빌드 성공 + 경고 12줄 baseline 동일. 커밋 1개, 스테이징 파일 정확히 1개(csproj 무접촉).
  </done>
</task>

<task type="auto">
  <name>Task 2: RefOrigin / 검출결과 / DETECT FAIL (R5–R7, BASE L980–1093) → 구역 메서드 3개 추출</name>
  <files>WPF_Example/Halcon/Display/HalconDisplayService.cs</files>
  <action>
**전제:** Task 1 커밋 완료. Task 1 이 편집한 구역(BASE L885–978)이 R5–R7 **앞**에 있으므로
**작업 파일의 줄번호는 이미 밀려 있다.** 위치는 반드시 앵커 `grep -n` 으로 구한다(§G-10).
diff 의 base 쪽만 `git show $BASE:$F | sed -n 'A,Bp'` 로 BASE 줄번호를 고정 사용한다.

---

**1단계 — R5–R7 을 잘라내고 그 자리에 호출 3줄을 넣는다.**

| 구역 | 앵커(파일 내 유일) | BASE 줄범위 | 줄수 |
|------|--------------------|-------------|------|
| R5 | `// Draw reference origin cross if configured` | 980–999 | 20 |
| R6 | `// 검출 라인 2개 + 교점 오버레이` | 1001–1060 | 60 |
| R7 | `// Datum 검출 실패 시 'DETECT FAIL' 적색 라벨 렌더.` | 1066–1093 | 28 |

각 구역 자리에 넣을 호출(들여쓰기 16칸):
```csharp
                RenderDatumRefOriginCross(window, datum);

                RenderDatumDetectedOverlay(window, datum);

                RenderDatumDetectFailLabel(window, datum);
```
**BASE L1062–1064 (`RenderDatumFindResult 를 …` 주석 2줄 + `RenderDatumFindResult(window, datum);`) 는
R6 호출과 R7 호출 사이에 그대로 남는다 — 손대지 않는다. 최종 호출 순서는
Slot → Line2 → Circle → Horizontal → RefOrigin → Detected → FindResult → DetectFail 이다.**
**BASE L1094 이후(바깥 `}` / `catch { // Suppress display errors }` / `}`)도 손대지 않는다.**

---

**2단계 — 신규 메서드 3개를 Task 1 이 추가한 `RenderDatumHorizontalRois` 닫는 `}` **바로 아래**,
`// Datum ROI 라벨 그리기 …` 주석 **앞**에 §G-2 표 순서대로 추가한다.**
본문은 잘라낸 줄을 **4칸 dedent** 해서 그대로 붙인다. **토큰 변경 0건.**

```csharp
        //260818 hbk Extract Method: RefOrigin 십자 구역을 그대로 옮긴 것.
        private void RenderDatumRefOriginCross(HWindow window, DatumConfig datum)
        {
            <BASE L980–999 (20줄)>
        }

        //260818 hbk Extract Method: 검출 결과 오버레이(LastTeachSucceeded 블록) 전체를 그대로 옮긴 것.
        //  블록 안 그리기 순서가 곧 화면 겹침 순서다 — 한 줄이라도 앞뒤로 옮기면 위에 와야 할 것이 가려진다.
        private void RenderDatumDetectedOverlay(HWindow window, DatumConfig datum)
        {
            <BASE L1001–1060 (60줄)>
        }

        //260818 hbk Extract Method: DETECT FAIL 라벨 구역을 그대로 옮긴 것.
        //  안쪽 예외 처리 블록은 통째로 함께 옮겼다 — 경계를 쪼개면 삼켜지는 예외 범위가 달라져 렌더가 부분적으로 사라진다.
        private void RenderDatumDetectFailLabel(HWindow window, DatumConfig datum)
        {
            <BASE L1066–1093 (28줄). 내부 try/catch 포함, 통째로>
        }
```

**절대 하지 말 것:**
- R6 안쪽 `RenderRawEdgePoints` 6줄의 **순서·색상 인자** 변경 (z-order 정렬 주석이 이유를 남겨 놓았다)
- R6 안쪽 `if (datum.AlgorithmTypeEnum == EDatumAlgorithm.CircleTwoHorizontal && …)` 를 또 별도 메서드로 쪼개기 (이번 범위 밖)
- R7 내부 try/catch 를 풀거나 바깥 try 로 합치기
- `#90EE90` 등 색상 리터럴 손대기, `crossSize` / `crossHalf` / `circleCenterCrossHalf` 숫자·이름 변경
- `RenderDatumFindResult(window, datum);` 를 새 메서드 안으로 끌고 들어가기 (호출 위치가 z-stack last 여야 한다)
- §G-7 금칙어를 새 주석에 넣기

---

**3단계 — 빌드 + 정적 검증 (커밋 전).** verify 블록 **1·2·3 + G-8 빌드** 실행.
verify 블록 **4(HYGIENE)는 커밋 이후**에만 실행.

**4단계 — 커밋. 대상 파일만:**
```bash
cd /c/Info/Project/DataMeasurement
git add WPF_Example/Halcon/Display/HalconDisplayService.cs
git diff --cached --name-only          # 정확히 1줄
git commit -m "refactor(260818-vih): RefOrigin/검출결과/DETECT FAIL 3구역을 구역 메서드로 추출 (순수 이동, 렌더 무변경)"
git status --porcelain -- WPF_Example/DatumMeasurement.csproj   # 여전히 " M"
```

**5단계 — 커밋 후 verify 블록 4(HYGIENE) 실행** (블록 안에서 `SCR` 재정의 필수).
  </action>
  <verify>
    <automated>
# [1] 구조 — 신규 메서드 3개 + 호출부 3곳 + ⭐z-order 8단계 전체 순서
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Halcon/Display/HalconDisplayService.cs && \
[ "$(grep -cE '^[[:space:]]*private void RenderDatumRefOriginCross\(HWindow window, DatumConfig datum\)$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*private void RenderDatumDetectedOverlay\(HWindow window, DatumConfig datum\)$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*private void RenderDatumDetectFailLabel\(HWindow window, DatumConfig datum\)$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*RenderDatumRefOriginCross\(window, datum\);$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*RenderDatumDetectedOverlay\(window, datum\);$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*RenderDatumDetectFailLabel\(window, datum\);$' $F)" = "1" ] && \
echo "== z-order: 호출 8줄이 원본 구역 순서대로 엄격히 오름차순 ==" && \
N1=$(grep -nE '^[[:space:]]*RenderDatumSlotRoi\(window, datum\);$' $F | cut -d: -f1) && \
N2=$(grep -nE '^[[:space:]]*RenderDatumLine2Roi\(window, datum\);$' $F | cut -d: -f1) && \
N3=$(grep -nE '^[[:space:]]*RenderDatumCircleRoi\(' $F | cut -d: -f1) && \
N4=$(grep -nE '^[[:space:]]*RenderDatumHorizontalRois\(' $F | cut -d: -f1) && \
N5=$(grep -nE '^[[:space:]]*RenderDatumRefOriginCross\(window, datum\);$' $F | cut -d: -f1) && \
N6=$(grep -nE '^[[:space:]]*RenderDatumDetectedOverlay\(window, datum\);$' $F | cut -d: -f1) && \
N7=$(grep -nE '^[[:space:]]*RenderDatumFindResult\(window, datum\);$' $F | cut -d: -f1) && \
N8=$(grep -nE '^[[:space:]]*RenderDatumDetectFailLabel\(window, datum\);$' $F | cut -d: -f1) && \
printf '%s\n' $N1 $N2 $N3 $N4 $N5 $N6 $N7 $N8 > /tmp/vih_order.txt && \
diff /tmp/vih_order.txt <(sort -n /tmp/vih_order.txt) && \
[ "$(sort -n /tmp/vih_order.txt | uniq | wc -l)" = "8" ] && \
echo "== 8개 호출이 전부 RenderDatumOverlay 본문 안(선언부 이전)에 있다 ==" && \
DECL=$(grep -nE '^[[:space:]]*private void RenderDatumSlotRoi\(' $F | cut -d: -f1) && \
[ "$N8" -lt "$DECL" ] && \
echo "== 바깥 try/catch 잔류 + 구역 메서드 안 try 신설 0 ==" && \
[ "$(grep -c 'catch' $F)" = "28" ] && \
echo "T2 STRUCTURE + Z-ORDER PASS"
    </automated>
    <automated>
# [2] ⭐바이트 동치 증명 — R5~R7 각 구역이 선행공백 제거 후 원본과 완전히 같은가
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Halcon/Display/HalconDisplayService.cs && \
BASE=bef801f && \
chk() {
  [ "$(grep -cF "$1" $F)" = "1" ] || { echo "ANCHOR NOT UNIQUE: $1"; return 1; }
  L=$(grep -nF "$1" $F | cut -d: -f1)
  diff <(git show $BASE:$F | sed -n "$3,$4p" | sed 's/^[[:space:]]*//') \
       <(sed -n "${L},$((L+$2-1))p" $F | sed 's/^[[:space:]]*//') || return 1
  echo "  OK $1 ($2 lines)"
} && \
chk 'Draw reference origin cross if configured' 20 980 999 && \
chk '검출 라인 2개 + 교점 오버레이' 60 1001 1060 && \
chk 'Datum 검출 실패 시' 28 1066 1093 && \
echo "== R1~R4 도 여전히 동치(Task1 결과 무회귀) ==" && \
chk 'RenderDatumOverlay 슬롯 분기' 31 885 915 && \
chk 'Line2 Rectangle2 는 TwoLineIntersect 에서만 렌더' 12 917 928 && \
chk 'Circle ROI 검색 영역' 20 930 949 && \
chk 'Horizontal A/B ROI Rectangle2' 28 951 978 && \
echo "T2 BYTE-EQUIV PASS (R1-R7 전 구역 199줄, diff empty)"
    </automated>
    <automated>
# [3] ⭐라인 멀티셋 대조(삭제된 줄 0) + 색상/HALCON/라벨/헬퍼 카운트 전수 재확인
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Halcon/Display/HalconDisplayService.cs && \
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad" && \
[ "$(comm -23 "$SCR/disp-base-lines.txt" <(sed 's/^[[:space:]]*//; s/[[:space:]]*$//' $F | sort) | wc -l)" = "0" ] && \
[ "$(grep -cE 'SetColor\([^)]*"red"\)' $F)" = "7" ] && \
[ "$(grep -cE 'SetColor\([^)]*"cyan"\)' $F)" = "6" ] && \
[ "$(grep -cE 'SetColor\([^)]*"yellow"\)' $F)" = "6" ] && \
[ "$(grep -cE 'SetColor\([^)]*"green"\)' $F)" = "4" ] && \
[ "$(grep -cE 'SetColor\([^)]*"magenta"\)' $F)" = "3" ] && \
[ "$(grep -cE 'SetColor\([^)]*"lime green"\)' $F)" = "2" ] && \
[ "$(grep -cE 'SetColor\([^)]*"orange"\)' $F)" = "2" ] && \
[ "$(grep -cE 'SetColor\([^)]*"blue"\)' $F)" = "2" ] && \
[ "$(grep -cE 'SetColor\([^)]*"slate blue"\)' $F)" = "2" ] && \
[ "$(grep -cE 'SetColor\([^)]*"gray"\)' $F)" = "1" ] && \
[ "$(grep -cE 'SetColor\([^)]*"white"\)' $F)" = "1" ] && \
[ "$(grep -cE 'SetColor\([^)]*"#90EE90"\)' $F)" = "1" ] && \
[ "$(grep -c 'HOperatorSet.SetColor' $F)" = "25" ] && \
[ "$(grep -c 'HOperatorSet.SetLineWidth' $F)" = "20" ] && \
[ "$(grep -c 'HOperatorSet.DispRectangle2' $F)" = "7" ] && \
[ "$(grep -c 'HOperatorSet.DispCircle' $F)" = "6" ] && \
[ "$(grep -c 'HOperatorSet.DispLine' $F)" = "21" ] && \
[ "$(grep -c 'HOperatorSet.DispCross' $F)" = "3" ] && \
[ "$(grep -c 'HOperatorSet.SetTposition' $F)" = "5" ] && \
[ "$(grep -c 'HOperatorSet.WriteString' $F)" = "5" ] && \
for s in '"L1")' '"L2")' '"Vert")' '"Circle")' '"H-A")' '"H-B")' '"Datum Origin")' '"DETECT FAIL: "'; do \
  [ "$(grep -cF "$s" $F)" = "1" ] || { echo "LABEL BROKEN: $s"; exit 1; }; done && \
echo "== R6 raw edge point 6줄 순서·색상 무변경 ==" && \
[ "$(grep -c 'RenderRawEdgePoints(window, datum\.' $F)" = "6" ] && \
diff <(git show bef801f:$F | grep -n 'RenderRawEdgePoints(window, datum\.' | cut -d: -f2- | sed 's/^[[:space:]]*//') \
     <(grep 'RenderRawEdgePoints(window, datum\.' $F | sed 's/^[[:space:]]*//') && \
[ "$(grep -cE '^[[:space:]]*DrawRoiLabel\(window' $F)" = "5" ] && \
[ "$(grep -cE '^[[:space:]]*DrawRoiLabelAt\(' $F)" = "5" ] && \
[ "$(grep -cE '^[[:space:]]*RenderCircleStripOverlay\(window, datum\);$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*RenderDatumFindResult\(window, datum\);$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*DrawExtendedLine\(window,' $F)" = "2" ] && \
[ "$(grep -cE '^[[:space:]]*EnsureFontInitialized\(window\);$' $F)" = "6" ] && \
echo "== 범위 밖 메서드 시그니처 무변경 ==" && \
[ "$(grep -cE '^[[:space:]]*private static void RenderCircleStripOverlay\(HWindow window, DatumConfig datum\)$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*public void RenderDatumFindResult\(HWindow window, DatumConfig datum\)$' $F)" = "1" ] && \
[ "$(grep -cE '^[[:space:]]*private static void DrawDirectionArrow\(HWindow window, RoiDefinition roi\)$' $F)" = "1" ] && \
echo "T2 INVARIANT COUNTS PASS"
    </automated>
    <automated>
# [4] HYGIENE — ⚠ 반드시 **커밋 이후** 실행. SCR 재정의 필수.
cd /c/Info/Project/DataMeasurement && F=WPF_Example/Halcon/Display/HalconDisplayService.cs && \
SCR="C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\scratchpad" && \
[ "$(grep -nE '\?[^?:]*:' $F | grep -vE '\?\?|\?\.' | wc -l)" = "0" ] && \
[ "$(grep -c '=> ' $F)" = "0" ] && \
[ "$(git show --stat --name-only --format= HEAD | grep -v '^$' | wc -l)" = "1" ] && \
git show --name-only --format= HEAD | grep -q 'HalconDisplayService.cs' && \
[ "$(git show --name-only --format= HEAD | grep -c 'DatumMeasurement.csproj')" = "0" ] && \
git status --porcelain -- WPF_Example/DatumMeasurement.csproj | grep -q '^ M' && \
echo "== 두 커밋 합쳐도 변경 파일은 대상 1개뿐 ==" && \
[ "$(git diff --name-only bef801f HEAD | wc -l)" = "1" ] && \
diff <(cut -c4- "$SCR/disp-git-baseline.txt" | sort) <(git status --porcelain | cut -c4- | sort) | grep -c '^[<>]' | grep -qE '^[01]$' && \
echo "T2 HYGIENE PASS"
    </automated>
    <automated>G-8 SIMUL 빌드 → 성공 + 경고가 $SCR/disp-baseline-warn.txt 와 동일(12줄). 신규 CS0219/CS0168/CS0177/CS0165 가 1건이라도 뜨면 즉시 중단</automated>
  </verify>
  <done>
`RenderDatumRefOriginCross` / `RenderDatumDetectedOverlay` / `RenderDatumDetectFailLabel` 3개 신규 + 호출 3줄.
R1–R7 **전 구역 199줄**이 선행공백 제거 후 원본과 **diff 0**.
호출 8줄(구역 7개 + 기존 `RenderDatumFindResult`)이 원본 구역 순서대로 엄격히 오름차순 → z-order 보존.
삭제된 줄 0, `catch` 28 유지(구역 메서드 안 try 신설 0), R7 내부 try/catch 통째 이동.
색상 리터럴 12종 / HALCON 호출 8종 / 라벨 8종 / 헬퍼 호출 7종 전부 착수 전과 동일.
`RenderRawEdgePoints` 6줄이 순서·색상 인자까지 원본과 diff 0.
빌드 성공 + 경고 12줄 baseline 동일. 커밋 1개, `bef801f..HEAD` 변경 파일 총 1개(csproj 무접촉).
  </done>
</task>

<task type="auto">
  <name>Task 3: 동치 증명 SUMMARY 작성 (정적 증거만으로 무회귀 증명)</name>
  <files>.planning/quick/260818-vih-halcondisplayservice-renderdatumoverlay-/260818-vih-SUMMARY.md</files>
  <action>
"빌드 통과했으니 OK" 는 근거로 인정하지 않는다. 사용자는 내일 아침에야 실기 확인이 가능하고,
이 코드의 실패 모드는 **예외도 로그도 없이 오버레이가 조용히 사라지는 것**이다.
아래 6개 절을 **실제 명령 출력**으로 채운다(추측·요약 금지).

**① 바이트 동치 증명 (핵심)**
| 구역 | 신규 메서드 | BASE 범위(@bef801f) | 줄수 | diff 결과 |
|------|-------------|---------------------|------|-----------|
| R1 Line1/Vertical 슬롯 | `RenderDatumSlotRoi` | L885–915 | 31 | (붙여넣기: 비어 있어야 함) |
| R2 Line2 | `RenderDatumLine2Roi` | L917–928 | 12 | |
| R3 Circle ROI | `RenderDatumCircleRoi` | L930–949 | 20 | |
| R4 Horizontal A/B | `RenderDatumHorizontalRois` | L951–978 | 28 | |
| R5 RefOrigin 십자 | `RenderDatumRefOriginCross` | L980–999 | 20 | |
| R6 검출 결과 | `RenderDatumDetectedOverlay` | L1001–1060 | 60 | |
| R7 DETECT FAIL | `RenderDatumDetectFailLabel` | L1066–1093 | 28 | |
정규화 방식(선행 공백 제거)과 총 이동 199줄을 명시한다.

**② 라인 멀티셋 대조 — 삭제된 실행줄 0**
`comm -23` 출력이 비어 있음을 붙이고, "순수 추출이므로 추가만 있고 삭제는 0" 이라는 논리를 기술한다.
추가된 줄 수(신규 메서드 선언·주석·중괄호·호출 8줄)도 함께 제시한다.

**③ 색상 리터럴 전수표 (이 파일 최대 위험)**
red 7 / cyan 6 / yellow 6 / green 4 / magenta 3 / lime green 2 / orange 2 / blue 2 / slate blue 2 / gray 1 / white 1 / #90EE90 1
— 전후 동일함을 grep 출력으로 제시하고,
**"색상 문자열이 한 글자만 어긋나면 HALCON 이 예외를 던지고 `catch { }` 가 삼켜, 빌드도 통과하고 로그도 없이 그 오버레이만 사라진다"**
는 실패 메커니즘을 명시한다(이 프로젝트에서 실제로 겪은 함정: `"purple"`, `"light green"`).

**④ z-order 보존 증명**
호출 8줄의 **현재 줄번호**를 순서대로 나열하고 엄격 오름차순임을 보인다:
Slot → Line2 → Circle → Horizontal → RefOrigin → Detected → **FindResult(기존, 잔류)** → DetectFail.
"나중에 그린 것이 위에 온다 → 순서가 곧 화면"이라는 이유를 함께 적는다.
R6 안쪽 `RenderRawEdgePoints` 6줄이 순서·색상 인자까지 diff 0 임도 제시한다.

**⑤ try/catch 무접촉 증명**
- 파일 전역 `catch` 28 유지 (신설 0, 삭제 0)
- 바깥 `try { … } catch { // Suppress display errors }` 가 `RenderDatumOverlay` 에 잔류하여 8개 호출 전체를 감싼다
- 따라서 구역 메서드에서 예외가 나면 **호출자의 같은 catch 에 잡히고 이후 구역이 중단**된다 → 원본과 동일 동작이라는 논리
- R7 내부 try/catch 는 통째로 이동(경계 분할 0)

**⑥ 파라미터 취급표 + 잔류 항목**
5개 파라미터(`window`/`datum`/`color`/`lineWidth`/`cthHideRois`)의 형(값형/참조형) / 구역 안 취급 / 전달 방식 / **`ref` 불필요 근거(전부 읽기 전용, 재대입 0)** 를 적는다.
`out` 파라미터 0건임도 명시.
잔류 항목: null 가드 / 색상·선굵기 if-else / `cthHideRois` 계산 + 설명 주석 3줄 / 바깥 try-catch / `RenderDatumFindResult` 호출.

**⑦ 추출하지 않은 부분과 그 근거** (있다면)
G-1 에 따라 "동작이 바뀔 것 같아 원형 유지한" 구역이 있으면 그 판단 근거를 적는다. 없으면 "없음"으로 명시.

**⑧ ⚠ 실기 UAT 요청 (사용자에게 남기는 마지막 절)**
정적 증거로는 **화면 픽셀을 보증하지 못한다.** 내일 아침 티칭 화면에서 아래를 눈으로 확인해달라고 적는다:
1. `TwoLineIntersect` Datum 선택 → **L1 / L2 사각형 + 라벨** 표시, 선택 시 cyan(굵기3) / 비선택 blue(굵기2)
2. `VerticalTwoHorizontal`(및 DualImage) → **Vert + H-A + H-B** 표시, L1/L2 는 표시되지 **않음**
3. `CircleTwoHorizontal` → **Circle ROI + Strip 사각형 + H-A/H-B**, Edit 모드 OFF + 티칭 완료 시 이들이 **숨겨지는지**
4. 티칭 성공 후 → **노란 Line1 외삽 / 청록 Line2 외삽 / 빨간 교점 십자 / 초록 검출 원 + 노란 중심 십자**
5. `IsConfigured` Datum → **magenta 십자 + "Datum Origin" 텍스트**
6. 검출 실패 Datum → 우상단 **빨간 "DETECT FAIL: {이름}"** 라벨
7. 여러 오버레이가 겹칠 때 **가려짐 순서가 이전과 같은지**(특히 검출 원 위에 노란 중심 십자가 보이는지)
  </action>
  <verify>
    <automated>
cd /c/Info/Project/DataMeasurement && \
S=.planning/quick/260818-vih-halcondisplayservice-renderdatumoverlay-/260818-vih-SUMMARY.md && \
[ -f "$S" ] && \
grep -qF 'bef801f' "$S" && \
grep -qF 'RenderDatumSlotRoi' "$S" && \
grep -qF 'RenderDatumDetectFailLabel' "$S" && \
grep -qF '199' "$S" && \
grep -qF 'comm -23' "$S" && \
grep -qF 'z-order' "$S" && \
grep -qiF 'UAT' "$S" && \
echo "T3 SUMMARY PASS"
    </automated>
  </verify>
  <done>
SUMMARY 에 ①구역별 바이트 동치 diff(7구역·199줄, 전부 빈 출력) ②라인 멀티셋 삭제 0
③색상 리터럴 12종 전후표 + 실패 메커니즘 설명 ④z-order 호출 8줄 줄번호 오름차순 증명
⑤try/catch 28 유지 + 바깥 try 잔류 논리 ⑥파라미터 취급표(ref 불필요 근거) + 잔류 항목
⑦미추출 구역 근거 ⑧실기 UAT 7항목 요청이 실제 명령 출력과 함께 기록됨.
  </done>
</task>

</tasks>

<verification>
1. `git diff --stat bef801f HEAD` → `WPF_Example/Halcon/Display/HalconDisplayService.cs` 단 1개 파일
2. R1–R7 전 구역(199줄) 선행공백 정규화 diff → **전부 빈 출력**
3. 라인 멀티셋 `comm -23` → **빈 출력**(삭제된 줄 0)
4. 색상 리터럴 12종 / HALCON 호출 8종 / 라벨 8종 / `catch` 28 → 착수 전과 동일
5. 호출 8줄 줄번호 엄격 오름차순(z-order 보존)
6. msbuild Debug|x64 성공 + 경고 12줄 baseline
7. 코드 삼항 0건, `=> ` 0건
8. `git status --porcelain -- WPF_Example/DatumMeasurement.csproj` → 여전히 ` M`(unstaged)
</verification>

<success_criteria>
- `RenderDatumOverlay` 본문이 **가드 + 색상 결정 + `cthHideRois` 계산 + try 안 순차 호출 8줄 + catch** 형태로 축소
- private 구역 메서드 7개가 원본 구역 순서대로 선언되고, 각 본문이 원본과 **바이트 동치**
- 렌더 동작(순서·조건·색상·좌표·라벨·예외 삼킴 범위) 100% 보존이 정적으로 증명됨
- 빌드 PASS(경고 12줄 baseline), 커밋 2개, `HalconDisplayService.cs` 외 무변경
</success_criteria>

<output>
완료 후 `.planning/quick/260818-vih-halcondisplayservice-renderdatumoverlay-/260818-vih-SUMMARY.md` 생성
</output>
