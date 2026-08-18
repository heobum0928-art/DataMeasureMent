---
phase: quick-260818-fqx
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - Document/Manual/DataMeasurement_Operator_Manual_v1.0.md
  - Document/Manual/DataMeasurement_Operator_Manual_v1.0_Screenshot_Checklist.md
  - Document/Manual/DataMeasurement_Operator_Manual_v1.0.docx
  - Document/Manual/_build_operator_manual_docx.py
autonomous: true
requirements: [DOC-OP-02]
user_setup: []

must_haves:
  truths:
    - "관리자/엔지니어가 9장만 보고 재티칭 전에 레시피 폴더 전체를 안전하게 백업할 수 있다"
    - "9장만 보고 Datum 티칭을 처음부터 끝까지(권한 확인 → 항목 선택 → ROI 지정 → 티칭 → 확인 → 저장) 수행할 수 있다"
    - "9장만 보고 패턴(ModelFinder) 모델을 새로 만들고 결과를 확인할 수 있다"
    - "9장만 보고 Align 티칭(Tray / Bottom)을 ROI 지정부터 티칭 저장·검사 확인까지 수행할 수 있다"
    - "9장에 적힌 버튼/메뉴/메시지 문구가 현재 코드의 실제 화면 문구와 일치한다"
    - "1~8장과 부록 A/B의 원고 내용이 한 글자도 바뀌지 않았다(기존 그림 36개 번호 그대로 유지)"
    - "캡처 체크리스트가 늘어난 전체 그림 집합과 순서·개수까지 1:1로 대응한다"
    - "재생성된 .docx 가 Word 에서 열리고 9장이 Heading 1 로, 9장 하위 절이 Heading 2/3 으로 잡힌다"
    - "WPF_Example/ 아래 소스 코드가 단 한 줄도 바뀌지 않았다"
  artifacts:
    - path: "Document/Manual/DataMeasurement_Operator_Manual_v1.0.md"
      provides: "9장(티칭)이 8장 뒤 · 부록 A 앞에 삽입된 매뉴얼 원고"
      min_lines: 640
    - path: "Document/Manual/DataMeasurement_Operator_Manual_v1.0_Screenshot_Checklist.md"
      provides: "9장 캡처 목록이 추가되고 총 장수가 갱신된 체크리스트"
      min_lines: 120
    - path: "Document/Manual/DataMeasurement_Operator_Manual_v1.0.docx"
      provides: "9장이 포함된 최종 Word 문서(재생성본)"
  key_links:
    - from: "Document/Manual/DataMeasurement_Operator_Manual_v1.0.md"
      to: "Document/Manual/DataMeasurement_Operator_Manual_v1.0.docx"
      via: "_build_operator_manual_docx.py 재실행"
      pattern: "python .*_build_operator_manual_docx\\.py"
    - from: "Document/Manual/DataMeasurement_Operator_Manual_v1.0.md"
      to: "Document/Manual/DataMeasurement_Operator_Manual_v1.0_Screenshot_Checklist.md"
      via: "[그림 N-M] 자리표시자 1:1 대응(순서 포함)"
      pattern: "\\[그림 \\d+-\\d+\\]"
    - from: "Document/Manual/DataMeasurement_Operator_Manual_v1.0.md (9장)"
      to: "WPF_Example UI 코드(읽기 전용)"
      via: "본문에 쓰는 버튼/메시지 문구를 XAML·resx 에서 실제 확인"
      pattern: "Teach Datum|패턴 모델 생성|ROI 1 그리기|티칭 저장"
---

<objective>
이미 완성·검증된 운영자 매뉴얼(`Document/Manual/DataMeasurement_Operator_Manual_v1.0.md` / `.docx`)에 **9장 "티칭"** 을 추가한다. 새 문서를 만들지 않고 기존 원고를 **8장 뒤 · 부록 A 앞** 위치에 확장한다.

Purpose: 선행 태스크(260818-el1)는 티칭을 "관리자 전용, 조작법 다루지 않음"으로 범위 밖에 뒀는데, 실제로 이 매뉴얼을 쓰는 사람(관리자/엔지니어 겸임 포함)이 지금 Datum + 패턴 + Align 티칭을 전부 새로 재작업 중이다. 티칭 절차가 글로 어디에도 없으면 재작업 결과를 재현할 수 없다.
Output: 9장이 추가된 원고 + 갱신된 캡처 체크리스트 + 재생성된 .docx. 전부 `Document/Manual/` 아래에서 **제자리 갱신**.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<hard_constraints>
## 절대 금지 (위반 시 즉시 중단)

1. **`WPF_Example/` 아래 어떤 파일도 수정/생성/삭제하지 않는다.** 소스 코드는 **읽기 전용**이다. Edit/Write 툴을 `WPF_Example/` 경로에 절대 사용하지 않는다.
2. **앱을 빌드하거나 실행하지 않는다.** 이 PC 는 실제 카메라/조명 컨트롤러(시리얼 포트)에 연결돼 있을 수 있다. `msbuild`, `dotnet build`, `DatumMeasurement.exe` 실행 금지. **화면 캡처도 하지 않는다.**
3. **git commit 하지 않는다.** 커밋은 오케스트레이터가 나중에 처리한다.
4. **1~8장과 부록 A/B 를 수정하지 않는다.** 한 글자도, 줄바꿈 하나도 바꾸지 않는다. 9장은 **8장의 마지막 줄(`> 캡처 대상: 프로그램 종료(닫기 버튼) 시 뜨는 ...`) 뒤, `부록 A. 용어 설명` 앞**에 **삽입만** 한다. 기존 그림 번호(1-1 ~ 8-3, 총 36개)를 재배치하거나 다시 매기지 않는다.
   - 이 규칙은 Task 1/2 의 자동 검증에서 `git show HEAD:` 원본과의 **앞부분(prefix) / 뒷부분(suffix) 완전 일치**로 강제 확인한다.
5. **화면 문구를 지어내지 않는다.** 버튼명·체크박스명·메시지·창 제목은 반드시 코드(XAML / `.xaml.cs` / `Resources.ko-KR.resx`)에서 실제로 확인한 문자열만 쓴다. 확인 못 한 동작은 문장을 만들지 말고 그 항목을 빼거나 `[확인 필요: ...]` 로 남긴다.
6. **내부 코드 용어를 본문에 쓰지 않는다.** 9장은 관리자/엔지니어용이지만 여전히 **화면 조작 매뉴얼**이다. `HALCON` / `Halcon` / `HImage` / `SequenceBase` / `TopInspectionAction` / 클래스명 / 알고리즘 내부 설명(measure_pos, shape model 파라미터 등)을 쓰지 않는다. 화면에 실제로 보이는 항목명은 그대로 써도 된다(예: 파라미터 편집기에 나오는 항목명).
7. **민감정보를 문서에 싣지 않는다.** 실제 계정 ID/비밀번호, 사내 IP, 실제 고객사 품목명을 적지 않는다. 레시피 경로는 `D:\Data\Recipe\<레시피명>\` 처럼 **일반화된 형태**로만 쓴다.

## 쓰기 허용된 경로

- `Document/Manual/` 아래 기존 3개 파일(원고 / 체크리스트 / .docx) **갱신**
- `Document/Manual/_build_operator_manual_docx.py` — **원칙적으로 수정하지 않는다.** 9장이 기존 규칙과 실제로 충돌할 때만 최소 수정하고, 그 사유를 SUMMARY 에 남긴다(아래 `<build_script_facts>` 참고 — 플래너가 미리 확인한 결과 수정 불필요).
- 이 quick 태스크의 `.planning/quick/260818-fqx-.../` 아래 SUMMARY
- 임시 검증 스크립트가 필요하면 세션 스크래치패드 디렉터리(`C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\...\scratchpad`)에 쓴다. 프로젝트 트리에 임시 파일을 남기지 않는다.
</hard_constraints>

<context>
@.planning/quick/260818-fqx-document-manual-datameasurement-operator/260818-fqx-CONTEXT.md
@CLAUDE.md

<manuscript_facts>
<!-- 플래너가 원고 502줄 + 빌드 스크립트 442줄을 직접 읽고 확인한 사실. 재조사하지 말 것. -->

**현재 원고 구조 (502줄)**
- 1~6줄: `<!--COVER:TITLE-->` 등 표지 항목 / 7줄: `<!--PAGEBREAK-->`
- 8~20줄: `<!--H1-->개 요` + 본문 / 22~23줄: `<!--H1-->목차` + `<!--TOC-->`
- 25~477줄: 1장 ~ 8장 (8장 마지막 줄 = `> 캡처 대상: 프로그램 종료(닫기 버튼) 시 뜨는 "정말로 프로그램을 종료하시겠습니까?" 확인창.`)
- 479줄: `부록 A. 용어 설명` / 491줄: `부록 B. 일상 점검 체크리스트` / 502줄 끝
- **삽입 지점: 477줄과 479줄 사이(478줄 빈 줄 뒤).**

**원고 문법 (빌드 스크립트가 인식하는 형식 — 반드시 그대로 따를 것)**

| 원고 표기 | 렌더링 |
|---|---|
| `9. 티칭 ...` (맨 앞에 `#` 없음) | Heading 1 |
| `## 9-1. 제목` | Heading 2 |
| `## 9-1-1. 제목` | Heading 3 |
| `[그림 9-1] 설명` (독립 한 줄) | 회색 자리표시자 표 + "※ 이 자리에 화면 캡처 이미지를 삽입하세요" |
| `> 캡처 대상: ...` (그림 줄 **바로 다음 줄**) | 표 아래 작은 회색 이탤릭 문단 |
| `- 텍스트` | List Bullet |
| `① 텍스트` (원 안 숫자로 시작) | List Number |
| `\| a \| b \|` | Word 표(Table Grid) |
| `**굵게**` | 굵은 run |

**빌드 스크립트 제약 (원고 작성 시 반드시 지킬 것)**
- **조작 절차는 반드시 원 안 숫자(①②③) 로 쓴다.** `1. 클릭합니다` 처럼 쓰면 장 제목 정규식(`^\d+\.\s`)에 걸려 **Heading 1 로 잘못 렌더링된다.**
- 원 안 숫자는 **⑳(20)까지만** 지원한다(`CIRCLED_NUMBERS` 상수). 한 절차가 20단계를 넘으면 스크립트 수정이 아니라 **절을 `## 9-2-1.` / `## 9-2-2.` 로 나눈다.**
- `>` 로 시작하는 줄은 `> 캡처 대상:` 형식만 특별 처리된다. 일반 인용문(`> ...`)을 쓰면 `>` 가 본문에 그대로 찍힌다. **쓰지 말 것.**
- 들여쓴 하위 불릿(`  - `)은 Word 에서 **평평하게 펴진다**(1~8장에 이미 있는 기존 동작). 들여쓰기 자체에 의미를 담지 말 것.
- 코드블록(```)은 스크립트가 처리하지 않는다. 쓰지 말 것.

**기존 그림 번호 (총 36개 — 절대 변경 금지)**
1장 1개(1-1) / 2장 6개 / 3장 4개 / 4장 5개 / 5장 6개 / 6장 7개 / 7장 4개 / 8장 3개.
→ **9장 그림은 `[그림 9-1]` 부터 시작하며 기존 번호와 충돌하지 않는다.**

**해결해야 할 모순 (중요)**
현재 원고 개요(14줄)와 1-1절(31줄)에 "측정 영역 그리기, 패턴 모델 생성, 기준좌표 티칭, 캘리브레이션 ... 은 이 매뉴얼에서 다루지 않습니다" 라고 적혀 있다. 9장을 추가하면 이 문장이 사실과 어긋난다.
→ **1~8장을 고쳐서 해결하지 않는다(금지).** 대신 **9장 첫 절 도입부에서 명시적으로 정리한다.** 예: "1~8장은 현장 운영자를 위한 안내이며, 1-1절에서 '다루지 않는다'고 안내한 티칭 작업을 이 장에서 관리자·엔지니어용으로 따로 설명합니다."

**부록 A(용어 설명)도 수정 금지** → 9장에서 새로 쓰는 용어(패턴/ModelFinder, Align, ROI 등)는 **9장 안에서 처음 나올 때 한 줄로 풀어 쓴다.**
</manuscript_facts>

<already_verified>
<!-- 플래너가 코드에서 직접 확인한 사실. 재조사하지 말고 그대로 쓰되, "동작"(클릭하면 무슨 일이 일어나는가)만 .xaml.cs 에서 추가 확인할 것. -->

**레시피 복사 = 폴더 전체 복제, Admin 등급 필수** (`WPF_Example/UI/Recipe/OpenRecipeWindow.xaml.cs:63` `Btn_Copy_Click`)
- 권한 가드: `Login.IsLogin == false || LoginAccount.Grade < EAccountGrade.Admin` → `Localize["Permission denied"]` / `Localize["Requires admin privileges."]` 안내창 후 중단.
  → **Engineer 등급으로는 복사가 안 된다. Admin 전용이다.** (3-3절에서 이미 "접근 거부됨" / "admin 권한이 필요합니다."로 한국어 표기됨 — 같은 문구를 쓸 것)
- 새 이름 입력 → 같은 이름이면 `Localize["Recipe name to be copied must be different."]` 오류 → 이미 있으면 덮어쓰기 확인창 → `RecipeFiles.Handle.Copy()` (`WPF_Example/Utility/RecipeFileHelper.cs:143`) 실행.
- 실패 시 `Localize["Fail to copy recipe"]` 안내창.
- 같은 창의 `Btn_Delete_Click`(98줄)도 동일한 Admin 가드 + "취소가 불가능합니다" 확인창.

**중앙 뷰어 티칭 도구모음** (`WPF_Example/UI/ContentItem/MainView.xaml`)
`btn_rectRoi`("Rect ROI", ToggleButton) / `btn_polygonRoi`("Polygon ROI", ToggleButton) / `btn_circleRoi`("Circle ROI", ToggleButton) / `btn_teachDatum`("Teach Datum", ToggleButton) / `btn_drawPatternRoi`("패턴 1") / `btn_drawPatternRoi2`("패턴 2") / `btn_createPatternModel`("패턴 모델 생성") / `btn_swapHorizontal`("👁 가로", ToggleButton) / `btn_swapVertical`("👁 세로", ToggleButton) / `btn_testFindDatum`("Test Find", `BtnTestFindDatum_Click`) / `btn_reanchor`("Re-anchor", `BtnReanchor_Click`) + `btn_reanchorApply`("Apply") / `btn_reanchorCancel`("Cancel") / `btn_calibrate`("Calibrate") / `btn_checkerboardCalibrate`("체커보드 캘리브")
오버레이 체크박스: `chk_overlayMeasure`("측정 overlay") / `chk_overlayDatum`("Datum 라인") / `chk_overlayPattern`("패턴 ROI")

**Align 티칭 화면** (`WPF_Example/Custom/UI/TrayVisionView.xaml`, `BottomVisionView.xaml` — 두 화면이 거의 같은 구성)
공통 버튼: `Grab` / `Live` / `Stop` / `폴더 열기` / `◀ 이전` / `다음 ▶` / `ROI 1 그리기` / `ROI 2 그리기` / `티칭 저장` / `검사`
공통 체크박스: `ROI 표시` / `에지 표시` / `동축 ON/OFF`
**Bottom 전용 추가 패널**(피커센터 캘리브레이션): `초기화` / `검색 ROI(원) 지정` / `Cal 모델 티칭` / `스텝 추가 (Grab+검출)` / `피커센터 계산`

**Datum 알고리즘 종류** (`WPF_Example/Halcon/Algorithms/DatumFindingService.cs`)
`EDatumAlgorithm` 열거: `CircleTwoHorizontal` / `VerticalTwoHorizontal` / `VerticalTwoHorizontalDualImage`(가로축 이미지 + 세로축 이미지 2장을 쓰는 변형, `TeachingImagePath` / `TeachingImagePath_Vertical` 두 경로 사용).
Teach 경로(`TryTeachXxx`)와 Find 경로(`TryFindXxx`)가 알고리즘별로 짝을 이룬다.

**계정 등급** — `Admin` / `Engineer` 두 가지뿐이며 "Operator" 등급은 존재하지 않는다(3장에 이미 반영됨). 9장은 이 사실과 모순되지 않게 쓸 것.

**git 상태** — `Document/Manual/` 4개 파일 모두 git 에 커밋된 clean 상태다. 따라서 `git show HEAD:<경로>` 로 원본을 꺼내 회귀 검증할 수 있다(각 Task 의 verify 가 이 방식을 쓴다).
</already_verified>

<evidence_map>
<!-- 절별로 읽어야 할 파일. 큰 .xaml.cs 는 전체 Read 금지 — Grep 으로 핸들러/문구만 뽑을 것. -->

| 9장 절 | 근거 파일 (읽기 전용) |
|----|----------------------|
| 공통 — 권한/로그인 | `WPF_Example/Login/LoginManager.cs`, `WPF_Example/UI/Recipe/OpenRecipeWindow.xaml.cs`(위 already_verified 로 충분), `WPF_Example/Properties/Resources.ko-KR.resx`(Grep: `Permission denied`, `Requires admin`) |
| 공통 — 레시피 폴더 백업 | `WPF_Example/Utility/RecipeFileHelper.cs`(Grep: `Copy`, `GetPatternModelFilePath`, `GetPatternImageFilePath`, `Path.Combine`, 확장자 `.shm`/`.ncm`/`.mmf`/`.json`), `WPF_Example/Setting/SystemSetting.cs`(Grep: `RecipePath`, `Dir`) |
| Datum 티칭 | `WPF_Example/UI/ContentItem/MainView.xaml.cs`(Grep: `teachDatum`, `TeachDatum`, `BtnTestFindDatum_Click`, `BtnReanchor`, `SwapHorizontal`, `SwapVertical`), `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs`(Grep: `public `, `DatumName`, `AlgorithmType`), `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`(Grep: `TryTeach`, `error =`) |
| 패턴(ModelFinder) 티칭 | `WPF_Example/UI/ContentItem/MainView.xaml.cs`(Grep: `drawPatternRoi`, `createPatternModel`, `PatternRoi`), `WPF_Example/Halcon/Algorithms/PatternMatchService.cs`(Grep: `public `, `Teach`, `Create`, `error`), `WPF_Example/Utility/RecipeFileHelper.cs`(모델 파일 저장 위치) |
| Align 티칭 (Tray) | `WPF_Example/Custom/UI/TrayVisionView.xaml.cs`(Grep: `_Click`, `MessageBox`, `Localize`) |
| Align 티칭 (Bottom) | `WPF_Example/Custom/UI/BottomVisionView.xaml.cs`(Grep: `_Click`, `MessageBox`, `Localize`), `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs`(Grep: `public `, `Teach`, `error`) |
| 티칭 후 확인 / 주의 | `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs`(Grep: `DatumName` setter 리네임 훅 — 이름 변경 시 모델 파일 자동 리네임 여부 확인) |
</evidence_map>
</context>

<tasks>

<task type="auto">
  <name>Task 1: 9장 도입 + 백업 안내 + Datum 티칭 + 패턴 티칭 작성</name>
  <files>Document/Manual/DataMeasurement_Operator_Manual_v1.0.md</files>
  <action>
원고 `Document/Manual/DataMeasurement_Operator_Manual_v1.0.md` 의 **477줄과 479줄 사이(`부록 A. 용어 설명` 바로 앞)** 에 9장을 삽입하기 시작한다. 이 Task 에서는 **9장 제목 + 공통 절 + Datum 티칭 절 + 패턴 티칭 절**까지 쓴다(Align 은 Task 2).

**반드시 Edit 툴로 삽입한다.** Write 로 파일 전체를 다시 쓰면 1~8장이 미세하게 바뀔 위험이 있다. `부록 A. 용어 설명` 을 앵커로 삼아 그 앞에 9장 블록을 끼워 넣는다.

**진행 방식(컨텍스트 폭발 방지):** `<evidence_map>` 의 절 단위로 진행한다 — 해당 절의 근거 파일만 Grep 으로 훑고 → 그 절을 원고에 이어붙이고 → 다음 절로 넘어간다. 모든 파일을 먼저 다 읽고 나중에 쓰지 않는다. `MainView.xaml.cs` 같은 대형 파일은 **절대 전체 Read 하지 말고** Grep 으로 핸들러 본문 위치를 좁힌 뒤 필요한 구간만 offset/limit Read 한다. `<already_verified>` 의 사실은 다시 조사하지 않는다.

**작성할 구조 (절 번호와 세부 단계 수는 코드를 본 뒤 판단 — 아래는 최소 골격):**

```
9. 티칭 (관리자 · 엔지니어 전용)

## 9-1. 티칭을 시작하기 전에
   - 이 장의 대상(관리자/엔지니어)과 1~8장과의 관계 정리 (위 <manuscript_facts> "해결해야 할 모순" 항목 반드시 반영)
   - 티칭이 무엇인지 한 문단 (제품이 놓인 위치가 조금씩 달라도 같은 곳을 측정하도록 기준을 가르치는 작업)
   - 필요한 권한: 로그인 필요, 레시피 복사·삭제는 Admin 등급 전용(Engineer 로는 안 됨)
   - **재티칭 전 레시피 폴더 전체 백업** (아래 별도 지침)
   - 티칭 중 하지 말아야 할 것 (검사 시퀀스 실행 중 티칭 금지 등 — 코드로 확인한 것만)

## 9-2. Datum(기준좌표) 티칭
## 9-3. 패턴(ModelFinder) 티칭
```

**9-1 백업 안내에 반드시 담을 내용 (CONTEXT.md 확정 사항 — 코드로 재확인 후 서술):**
- 레시피는 `main.ini` 파일 하나가 아니라 **`D:\Data\Recipe\<레시피명>\` 폴더 전체**다. 하위 폴더(TOP / SIDE / BOTTOM / ETHERNET_ALIGN / SEQ_* 등)에 모델 파일(`.shm` / `.ncm` / `.mmf`)과 설정(`.json`), 티칭 이미지가 흩어져 있다.
- 재티칭하면 이 파일들이 **같은 경로에 그대로 덮어써진다.** 되돌리려면 백업본이 있어야 한다.
- 백업 방법 두 가지를 모두 안내한다:
  1. 프로그램 안: [RECIPE] → 레시피 목록 창에서 레시피 선택 → **[COPY]** 버튼 → 새 이름 입력 → 폴더 전체가 복제됨. **Admin 등급 로그인 필수**(아니면 "접근 거부됨" / "admin 권한이 필요합니다." 안내창).
  2. 탐색기: `D:\Data\Recipe\<레시피명>\` 폴더를 통째로 복사해 날짜를 붙여 보관(예: `FAI_1_260818_백업`).
- **실제 버튼 문구(`COPY` / `Delete` 등)와 실제 경로 기본값은 코드로 재확인한 뒤 쓴다.** CONTEXT.md 는 요약본이므로 근거로 삼지 말 것. 파일 개수(55개 등 특정 숫자)는 레시피마다 다르므로 **본문에 숫자를 박지 않는다.**

**9-2 / 9-3 작성 지침:**
- **실제 절차 깊이로 쓴다.** "관리자가 합니다" 같은 회피 문장 금지. 화면에서 무엇을 누르고, 무엇이 열리고, 무엇을 확인하고, 어떻게 저장하는지를 ①②③ 단계로 쓴다.
- 각 절은 최소한 이 흐름을 담는다: **준비(로그인/레시피/항목 선택) → 이미지 확보(Grab 또는 Load) → ROI/영역 지정 → 티칭 실행 → 결과 확인 → 저장 → 실패했을 때 확인할 것.**
- Datum 절에는 알고리즘 종류가 여러 개라는 사실과, 종류에 따라 지정해야 하는 ROI 구성이 달라진다는 점을 **코드로 확인한 범위 안에서** 설명한다(예: 원 + 가로 2개 / 세로 + 가로 2개 / 이미지 2장을 쓰는 변형). 알고리즘 내부 수식·파라미터는 설명하지 않는다.
- Datum 절에는 **[Test Find]** 로 티칭 결과를 검증하는 단계를 반드시 포함한다(티칭만 하고 확인 안 하면 나중에 전 항목이 조용히 실패한다).
- 패턴 절에는 **[패턴 1] / [패턴 2] / [패턴 모델 생성]** 흐름과, 모델 파일이 레시피 폴더 안에 저장된다는 점을 포함한다.
- 코드에서 확인 못 한 동작은 지어내지 말고 그 문장을 빼거나 `[확인 필요: ...]` 로 남긴다.

**그림 자리표시자 규칙 (1~8장과 동일):**
- `[그림 9-M] 설명` 을 독립된 한 줄로, 바로 다음 줄에 `> 캡처 대상: ...` 로 **무슨 화면을 어떤 상태에서 찍어야 하는지** 구체적으로.
- M 은 1부터 빈 번호 없이 연속. 사용자가 지금 실제로 재티칭 중이므로 **캡처 대상 설명은 "지금 그 작업을 하는 김에 찍을 수 있는 화면"** 으로 구체적으로 쓴다.
- 이 Task 범위(9-1 ~ 9-3)에서 **최소 10개**. 9-2 와 9-3 은 각각 **최소 4개**.
  </action>
  <verify>
    <automated>cd "C:/Info/Project/DataMeasurement" && python -c "
import io,re,subprocess
from collections import defaultdict
P='Document/Manual/DataMeasurement_Operator_Manual_v1.0.md'
new=io.open(P,encoding='utf-8').read().replace('\r\n','\n')
orig=subprocess.check_output(['git','show','HEAD:'+P]).decode('utf-8').replace('\r\n','\n')
MARK='부록 A. 용어 설명'
i=orig.index(MARK); head=orig[:i]; tail=orig[i:]
assert new.startswith(head), '회귀: 1~8장 영역이 바뀌었습니다(원본과 앞부분 불일치)'
assert new.endswith(tail), '회귀: 부록 영역이 바뀌었습니다(원본과 뒷부분 불일치)'
ins=new[len(head):len(new)-len(tail)]
assert len(ins.strip())>0, '9장이 삽입되지 않았습니다'
assert re.search(r'^9\. ',ins,re.M), '9장 제목줄(맨 앞 # 없는 \"9. ...\")이 없습니다'
names=re.split(r'^## (9-\d+)\.',ins,flags=re.M)[1::2]
assert len(names)>=3, '9장 절이 3개 미만: %s'%names
bodies=re.split(r'^## 9-\d+\.',ins,flags=re.M)[1:]
per=[len(re.findall(r'\[그림 9-\d+\]',b)) for b in bodies]
assert len([c for c in per if c>=4])>=2, '실제 절차 깊이(그림 4개 이상) 절이 2개 미만: %s'%dict(zip(names,per))
figs=re.findall(r'\[그림 (\d+)-(\d+)\]',new)
d=defaultdict(list)
for a,b in figs: d[int(a)].append(int(b))
for k in sorted(d): assert d[k]==list(range(1,len(d[k])+1)), '%d장 그림 번호 불연속/중복: %s'%(k,d[k])
assert len(d[9])>=10, '9장 그림 자리표시자 부족: %d'%len(d[9])
assert len(figs)==36+len(d[9]), '기존 36개 그림이 보존되지 않음(전체 %d)'%len(figs)
caps=len(re.findall(r'^> 캡처 대상:',new,re.M))
assert caps==len(figs), '캡처 대상 설명 수(%d) != 그림 수(%d)'%(caps,len(figs))
for s in ['Teach Datum','패턴 모델 생성','Test Find']:
    assert s in ins, '코드로 확인된 실제 버튼 문구 누락: '+s
for s in ['Recipe','.shm','복사']:
    assert s in ins, '백업 안내 필수 요소 누락: '+s
banned=['Wafer','Die ','Map Matching','Scrib','HImage','SequenceBase','TopInspectionAction','HALCON','Halcon']
hit=[w for w in banned if w in ins]
assert not hit, '금지 용어 포함: %s'%hit
BEYOND='㉑㉒㉓㉔㉕㉖㉗㉘㉙㉚'
for ln in ins.split('\n'):
    s=ln.strip()
    assert not (s and s[0] in BEYOND), '원 안 숫자 20 초과 사용(빌드 스크립트 미지원) — 절을 나누세요: '+s[:30]
print('PASS Task1: 9장 절 %s, 절별 그림 %s, 9장 그림 %d개, 전체 그림 %d개'%(names,per,len(d[9]),len(figs)))
"</automated>
  </verify>
  <done>원고의 1~8장·부록이 `git show HEAD:` 원본과 앞/뒤 완전 일치하고, 그 사이에 9장(제목 + 9-1 공통/백업 + 9-2 Datum + 9-3 패턴, 3개 이상 절)이 삽입되었다. 9장 그림 자리표시자 10개 이상이 연속 번호로 존재하며 각각 `> 캡처 대상:` 이 붙어 있다. 기존 그림 36개가 그대로 보존됐다. `Teach Datum` / `패턴 모델 생성` / `Test Find` 등 코드로 확인한 실제 버튼 문구를 사용했고, 금지 용어 0건이다.</done>
</task>

<task type="auto">
  <name>Task 2: 9장 Align 티칭 절 작성 (Tray + Bottom)</name>
  <files>Document/Manual/DataMeasurement_Operator_Manual_v1.0.md</files>
  <action>
Task 1 이 작성한 9장 뒤에 **Align 티칭 절**을 이어 쓴다(같은 파일, 여전히 `부록 A. 용어 설명` 앞). Edit 툴로 삽입한다.

**근거 조사:** `WPF_Example/Custom/UI/TrayVisionView.xaml.cs` 와 `BottomVisionView.xaml.cs` 를 Grep(`_Click`, `MessageBox`, `Localize`)으로 훑어 각 버튼이 실제로 무슨 일을 하는지, 어떤 안내/오류 메시지가 뜨는지 확인한다. 필요한 구간만 offset/limit 으로 Read 한다. `AlignShapeMatchService.cs` 는 티칭 실패 메시지 확인 용도로만 Grep 한다.

**작성할 내용:**
- **Align 화면에 어떻게 들어가는지** 부터 쓴다. 본문 영역 위쪽 탭 중 어느 탭인지, 어떤 조건(설정/모드)에서 그 탭이 보이는지를 코드로 확인해 정확히 쓴다. (2-2절이 "다른 탭은 별도의 정렬 비전 설비가 연결된 경우에만 쓰는 화면"이라고 이미 안내해 뒀으므로, 9장은 그 화면 안의 조작을 이어받아 설명한다.)
- **Tray 와 Bottom 두 화면**을 다룬다. 두 화면 구성이 거의 같으므로(`Grab`/`Live`/`Stop`/`폴더 열기`/`◀ 이전`/`다음 ▶`/`ROI 1 그리기`/`ROI 2 그리기`/`티칭 저장`/`검사`), **공통 절차를 한 번 쓰고 Bottom 만의 차이를 뒤에 덧붙이는 구성**을 권장한다. 다만 최종 구성(하나로 묶을지 두 절로 나눌지)은 코드를 본 뒤 판단한다(CONTEXT.md 상 실행자 재량).
- 절차 흐름: **화면 열기 → 대상 슬롯/면 선택(Bottom 은 슬롯이 여러 개) → 이미지 확보([Grab] 또는 [폴더 열기]+[◀ 이전]/[다음 ▶]) → [ROI 1 그리기] / [ROI 2 그리기] → [티칭 저장] → [검사] 로 결과 확인 → 실패 시 확인할 것.**
- `[ROI 표시]` / `[에지 표시]` 체크박스로 결과를 눈으로 확인하는 방법을 포함한다.
- `[동축 ON/OFF]` 체크박스는 조명 관련이므로 **코드로 확인한 동작만** 한 줄로 적는다.
- **Bottom 화면의 피커센터 캘리브레이션 패널**(`초기화` / `검색 ROI(원) 지정` / `Cal 모델 티칭` / `스텝 추가 (Grab+검출)` / `피커센터 계산`)은 CONTEXT.md 가 확정한 3종 티칭(Datum/패턴/Align)에 포함되지 않는 별개 작업이다. **절차를 지어내지 말 것.** 같은 화면에 있어 혼동하기 쉬우므로 **"이 패널은 별도의 피커센터 캘리브레이션용이며 Align 티칭과 다른 작업"** 이라는 경고 한 단락 + 버튼 이름 나열까지만 하고, 절차를 쓰려면 반드시 코드로 흐름을 확인한 뒤에만 쓴다. 확인 못 하면 `[확인 필요: ...]` 로 남긴다.

**선택(재량) — 티칭 후 확인 / 자주 겪는 문제 절:**
여유가 되면 마지막에 짧은 절을 하나 더 둔다. 단 **코드로 확인한 사실만** 쓴다. 후보(전부 코드 확인 필수):
- Datum 이름을 바꿨을 때 모델 파일이 함께 처리되는지(`DatumConfig.DatumName` setter 리네임 훅 확인)
- 티칭 직후 반드시 레시피를 저장해야 하는지(저장 안 하면 어떻게 되는지)
- 티칭했는데 검사에서 계속 실패할 때 먼저 볼 것
확인 못 한 항목은 통째로 뺀다. 추측으로 쓰지 않는다.

**형식 규칙은 Task 1 과 동일**: 절 번호는 `## 9-N.`, 조작 절차는 원 안 숫자 ①②③(⑳ 초과 금지), 버튼은 `[버튼명]`, 그림은 `[그림 9-M]` + 다음 줄 `> 캡처 대상:`, 번호는 Task 1 마지막 번호에서 이어서 연속.
Align 절 그림 **최소 4개**. 9장 전체 그림 **최소 14개**.
  </action>
  <verify>
    <automated>cd "C:/Info/Project/DataMeasurement" && python -c "
import io,re,subprocess
from collections import defaultdict
P='Document/Manual/DataMeasurement_Operator_Manual_v1.0.md'
new=io.open(P,encoding='utf-8').read().replace('\r\n','\n')
orig=subprocess.check_output(['git','show','HEAD:'+P]).decode('utf-8').replace('\r\n','\n')
MARK='부록 A. 용어 설명'
i=orig.index(MARK); head=orig[:i]; tail=orig[i:]
assert new.startswith(head), '회귀: 1~8장 영역이 바뀌었습니다(원본과 앞부분 불일치)'
assert new.endswith(tail), '회귀: 부록 영역이 바뀌었습니다(원본과 뒷부분 불일치)'
ins=new[len(head):len(new)-len(tail)]
names=re.split(r'^## (9-\d+)\.',ins,flags=re.M)[1::2]
assert len(names)>=4, '9장 절이 4개 미만(공통/Datum/패턴/Align): %s'%names
assert names==['9-%d'%(n+1) for n in range(len(names))], '9장 절 번호 불연속: %s'%names
bodies=re.split(r'^## 9-\d+\.',ins,flags=re.M)[1:]
per=[len(re.findall(r'\[그림 9-\d+\]',b)) for b in bodies]
deep=[c for c in per if c>=4]
assert len(deep)>=3, '3종 티칭이 각각 실제 절차 깊이(그림 4개 이상)를 갖추지 못함: %s'%dict(zip(names,per))
figs=re.findall(r'\[그림 (\d+)-(\d+)\]',new)
d=defaultdict(list)
for a,b in figs: d[int(a)].append(int(b))
for k in sorted(d): assert d[k]==list(range(1,len(d[k])+1)), '%d장 그림 번호 불연속/중복: %s'%(k,d[k])
assert len(d[9])>=14, '9장 그림 자리표시자 부족: %d (최소 14)'%len(d[9])
assert len(figs)==36+len(d[9]), '기존 36개 그림이 보존되지 않음(전체 %d)'%len(figs)
caps=len(re.findall(r'^> 캡처 대상:',new,re.M))
assert caps==len(figs), '캡처 대상 설명 수(%d) != 그림 수(%d)'%(caps,len(figs))
for s in ['Teach Datum','패턴 모델 생성','Test Find','ROI 1 그리기','티칭 저장']:
    assert s in ins, '코드로 확인된 실제 버튼 문구 누락: '+s
banned=['Wafer','Die ','Map Matching','Scrib','HImage','SequenceBase','TopInspectionAction','HALCON','Halcon']
hit=[w for w in banned if w in ins]
assert not hit, '금지 용어 포함: %s'%hit
for bad in ['TODO','TBD','FIXME','Lorem']:
    assert bad not in ins, '미완성 표시 잔존: '+bad
BEYOND='㉑㉒㉓㉔㉕㉖㉗㉘㉙㉚'
for ln in ins.split('\n'):
    s=ln.strip()
    assert not (s and s[0] in BEYOND), '원 안 숫자 20 초과 사용 — 절을 나누세요: '+s[:30]
assert len(new.splitlines())>=640, '원고가 너무 짧음: %d줄'%len(new.splitlines())
print('PASS Task2: 9장 절 %s, 절별 그림 %s, 9장 그림 %d개, 전체 그림 %d개, 원고 %d줄'%(names,per,len(d[9]),len(figs),len(new.splitlines())))
"</automated>
  </verify>
  <done>9장이 4개 이상의 절(공통/Datum/패턴/Align)로 완성됐고, 그중 3개 이상이 그림 4개 이상의 실제 절차 깊이를 갖췄다. 9장 그림 14개 이상이 연속 번호로 존재하고 각각 캡처 지시가 붙어 있다. Tray/Bottom Align 절차가 실제 버튼 문구(`ROI 1 그리기` / `티칭 저장` 등) 기반으로 작성됐다. 1~8장·부록은 여전히 원본과 완전 일치하고, 금지 용어·미완성 표시 0건이다.</done>
</task>

<task type="auto">
  <name>Task 3: 캡처 체크리스트 갱신 + .docx 재생성 + 전체 회귀 검증</name>
  <files>Document/Manual/DataMeasurement_Operator_Manual_v1.0_Screenshot_Checklist.md, Document/Manual/DataMeasurement_Operator_Manual_v1.0.docx</files>
  <action>
**(1) 체크리스트 갱신** — `Document/Manual/DataMeasurement_Operator_Manual_v1.0_Screenshot_Checklist.md`

기존 1~8장 표는 **한 글자도 건드리지 않는다.** 다음 3가지만 바꾼다(Edit 툴 사용):

- **9장 섹션 추가** — `### 8장 — 문제가 생겼을 때` 표 뒤, `## 마무리 안내` 앞에 삽입:
  ```
  ### 9장 — 티칭 (관리자 · 엔지니어 전용)

  | 체크 | 그림 번호 | 매뉴얼 위치(절) | 캡처할 화면 | 전제 조건 / 조작 상태 | 강조할 부분 |
  |------|-----------|-----------------|-------------|----------------------|-------------|
  | ☐ | 그림 9-1 | 9-1 | ... | ... | ... |
  ```
  원고의 `[그림 9-M]` 과 바로 뒤 `> 캡처 대상:` 을 **기계적으로 추출**해 채운다. 원고에 없는 내용을 새로 지어내지 않는다. `강조할 부분` 은 해당 절 본문이 설명하는 버튼/영역 이름으로 채운다.
- **`## 마무리 안내` 의 총 장수 줄 갱신** — `**총 캡처 장수: 36장** (...)` 을 새 총합과 장별 내역으로 고친다(9장 개수 포함).
- **`## 사전 준비` 의 로그인 안내 두 줄 보강** — "로그인이 필요한 화면" 목록에 9장을 추가한다(9장 전체가 로그인 필요, 레시피 복사는 Admin 등급 필요). "로그인이 필요 없는 화면" 줄은 그대로 둔다.
- **사전 준비에 한 항목 추가 권장** — 9장 캡처는 실제 재티칭 작업 중에 찍는 것이 가장 쉬우므로, "티칭 작업을 하는 김에 순서대로 찍어두면 된다"는 안내와 **캡처 전 레시피 폴더 백업 권고**를 넣는다.

**(2) .docx 재생성**

```
python Document/Manual/_build_operator_manual_docx.py
```

**스크립트는 원칙적으로 수정하지 않는다.** 플래너가 미리 확인한 결과 9장(`9. `→Heading 1, `## 9-N.`→Heading 2, `## 9-N-K.`→Heading 3, `[그림 9-M]`→회색 자리표시자)은 기존 정규식으로 그대로 처리된다. 실행 결과 실제로 렌더링이 깨지는 경우에만 최소 수정하고, **무엇이 왜 깨졌는지를 SUMMARY 에 반드시 기록**한다.

**(3) 전체 회귀 검증** — 아래 verify 가 다음을 모두 확인한다:
- 원고 1~8장/부록이 `git show HEAD:` 원본과 앞/뒤 완전 일치
- 체크리스트 1~8장 표 영역이 원본과 완전 일치, 그림 집합·순서가 원고와 1:1
- .docx 의 Heading 1/2/3 개수가 원고에서 계산한 기대값과 정확히 일치
- .docx 그림 자리표시자 집합 == 원고 그림 집합, TOC 필드·맑은 고딕 존재
- `WPF_Example/` 신규 변경 0건
  </action>
  <verify>
    <automated>cd "C:/Info/Project/DataMeasurement" && python Document/Manual/_build_operator_manual_docx.py && python -c "
import io,re,zipfile,subprocess
from docx import Document
MD='Document/Manual/DataMeasurement_Operator_Manual_v1.0.md'
CK='Document/Manual/DataMeasurement_Operator_Manual_v1.0_Screenshot_Checklist.md'
DX='Document/Manual/DataMeasurement_Operator_Manual_v1.0.docx'
def rd(p): return io.open(p,encoding='utf-8').read().replace('\r\n','\n')
def git(p): return subprocess.check_output(['git','show','HEAD:'+p]).decode('utf-8').replace('\r\n','\n')
md=rd(MD); mdo=git(MD)
i=mdo.index('부록 A. 용어 설명')
assert md.startswith(mdo[:i]) and md.endswith(mdo[i:]), '회귀: 원고 1~8장/부록이 원본과 다릅니다'
ck=rd(CK); cko=git(CK)
i1=cko.index('### 1장'); j1=cko.index('## 마무리 안내'); i2=ck.index('### 1장')
assert ck[i2:i2+(j1-i1)]==cko[i1:j1], '회귀: 체크리스트 1~8장 표가 변경되었습니다'
a=re.findall(r'\[그림 (\d+-\d+)\]',md)
b=[]
for x in re.findall(r'그림 (\d+-\d+)',ck):
    if x not in b: b.append(x)
assert a==b, '체크리스트 그림 순서/집합 불일치: 원고=%d개 체크=%d개 차이=%s'%(len(a),len(b),set(a)^set(b))
assert ck.count('☐')>=len(a), '체크박스 부족: %d < %d'%(ck.count('☐'),len(a))
assert '### 9장' in ck, '체크리스트에 9장 섹션이 없습니다'
assert ('총 캡처 장수: %d장'%len(a)) in ck, '마무리 안내의 총 캡처 장수가 %d장으로 갱신되지 않았습니다'%len(a)
doc=Document(DX)
h1=[x for x in doc.paragraphs if x.style.name=='Heading 1']
h2=[x for x in doc.paragraphs if x.style.name=='Heading 2']
h3=[x for x in doc.paragraphs if x.style.name=='Heading 3']
e1=len(re.findall(r'^<!--H1-->',md,re.M))+len(re.findall(r'^\d+\. ',md,re.M))+len(re.findall(r'^부록 [A-Z]\.',md,re.M))
e2=len(re.findall(r'^## \d+-\d+\. ',md,re.M))
e3=len(re.findall(r'^## \d+-\d+-\d+\. ',md,re.M))
assert len(h1)==e1, 'Heading1 수 불일치: docx=%d 원고기대=%d'%(len(h1),e1)
assert len(h2)==e2, 'Heading2 수 불일치: docx=%d 원고기대=%d'%(len(h2),e2)
assert len(h3)==e3, 'Heading3 수 불일치: docx=%d 원고기대=%d'%(len(h3),e3)
assert e1>=13, '9장이 Heading1 로 잡히지 않았습니다(e1=%d)'%e1
txt='\n'.join(x.text for x in doc.paragraphs)
for t in doc.tables:
    for r in t.rows:
        for c in r.cells: txt+='\n'+c.text
dfigs=set(re.findall(r'\[그림 (\d+-\d+)\]',txt))
assert dfigs==set(a), 'docx 그림 자리표시자 불일치: docx=%d md=%d 차이=%s'%(len(dfigs),len(a),dfigs^set(a))
z=zipfile.ZipFile(DX)
xml=z.read('word/document.xml').decode('utf-8')
assert 'TOC' in xml and 'fldChar' in xml, 'TOC 필드 없음'
assert '맑은 고딕' in z.read('word/styles.xml').decode('utf-8'), '한글 폰트 미지정'
for bad in ['TODO','TBD','Lorem','FIXME']:
    assert bad not in txt, '미완성 표시 잔존: '+bad
print('PASS Task3: 그림 %d개(9장 %d개), H1=%d H2=%d H3=%d, 체크리스트 1:1 대응'%(len(a),len([x for x in a if x.startswith('9-')]),len(h1),len(h2),len(h3)))
" && test -z "$(git status --porcelain WPF_Example/ | grep -v 'DatumMeasurement.csproj')" && echo "PASS 소스 무변경(WPF_Example/ 신규 변경 0건)" && git status --porcelain Document/Manual/</automated>
  </verify>
  <done>체크리스트에 9장 섹션이 추가되고 총 캡처 장수가 갱신됐으며, 1~8장 표는 원본과 완전 일치한다. 그림 번호가 원고와 순서·집합 모두 1:1이다. `.docx` 가 재생성되어 Heading 1/2/3 개수가 원고에서 계산한 기대값과 정확히 일치하고(9장이 Heading 1로 잡힘), 그림 자리표시자 집합이 원고와 일치하며 TOC 필드·맑은 고딕이 살아 있다. `git status --porcelain WPF_Example/` 에 사전 존재하던 `DatumMeasurement.csproj` 외 변경이 없다.</done>
</task>

</tasks>

<source_coverage_audit>
<!-- CONTEXT.md 결정사항 -> 커버하는 Task 매핑. 누락 0건 확인용. -->

| 출처 | 항목 | 커버 |
|------|------|------|
| CONTEXT `문서 구성` | 새 문서 만들지 않고 같은 매뉴얼 9장으로, 8장 뒤 · 부록 앞 | Task 1 (삽입 지점 + hard_constraint 4) |
| CONTEXT `범위: 티칭 종류` | Datum 티칭 실제 절차 수준 | Task 1 (9-2) |
| CONTEXT `범위: 티칭 종류` | 패턴(ModelFinder) 티칭 실제 절차 수준 | Task 1 (9-3) |
| CONTEXT `범위: 티칭 종류` | Align 티칭(Tray + Bottom) 실제 절차 수준 | Task 2 (9-4) |
| CONTEXT `범위: 티칭 종류` | 하나만 깊고 나머지는 개요 = 금지 | Task 2 verify (`deep>=3` 자동 강제) |
| CONTEXT `범위: 티칭 종류` | 공통 절차(로그인, 왜 관리자 권한)는 공통 절로 한 번만 | Task 1 (9-1) |
| CONTEXT `스크린샷 처리` | 캡처하지 않음, 자리표시자 + `> 캡처 대상:` 유지, 동일 시각 형식 | Task 1/2 (형식 규칙), Task 3 (docx 회색 상자 렌더 검증) |
| CONTEXT `스크린샷 처리` | 재티칭 하는 김에 찍어두면 됨 안내 | Task 3 (체크리스트 사전 준비) |
| CONTEXT `백업 안내` | 레시피 = 폴더 전체(`D:\Data\Recipe\<명>\`), 하위 확장자, 덮어쓰기 | Task 1 (9-1 백업) |
| CONTEXT `백업 안내` | [복사]/COPY 버튼이 폴더 전체 복제, 권한 필요 — **코드로 재확인 후** | Task 1 (already_verified 에 코드 확인 결과 선반영 + 재확인 지시) |
| CONTEXT `Claude's Discretion` | 절 번호/단계 수/Tray-Bottom 분리 여부 | Task 1/2 action 에서 실행자 판단으로 명시 |
| 오케스트레이터 제약 | 1~8장·부록 무변경, 그림 번호 미충돌 | Task 1/2/3 verify (git HEAD prefix/suffix 완전 일치) |
| 오케스트레이터 제약 | 체크리스트 1:1 유지 | Task 3 verify |
| 오케스트레이터 제약 | docx 재빌드 + 선행 태스크와 동일한 구조 검증 | Task 3 verify |
| 오케스트레이터 제약 | `WPF_Example/` 무변경 | Task 3 verify |

**누락 0건.** 이연 항목 없음.
</source_coverage_audit>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| 사내 문서 배포 | 완성된 .docx 가 현장/외부로 배포되며, 문서 안 내용이 곧 노출 범위가 된다 |
| 실행자 → 운영 중 코드베이스 | 문서 작업 에이전트가 실제 장비 제어 소스와 같은 워킹 트리에서 작업한다 |
| 문서 → 실제 재티칭 작업 | 사용자가 이 문서를 보고 **실제 장비에서 재티칭을 수행**한다. 틀린 절차는 레시피 데이터 손실로 직결된다 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-fqx-01 | Information Disclosure | 9장 본문 / 체크리스트 | mitigate | 실제 계정 ID·비밀번호·사내 IP·고객사 품목명 기재 금지. 레시피 경로는 `D:\Data\Recipe\<레시피명>\` 일반형으로만. 캡처 지시문에 민감정보 가림 안내 유지 (`<hard_constraints>` 7) |
| T-fqx-02 | Tampering | `WPF_Example/` 소스 트리 | mitigate | `<hard_constraints>` 1번 쓰기 금지 + Task 3 verify 에서 `git status --porcelain WPF_Example/` 자동 검증(사전 존재 `DatumMeasurement.csproj` 외 변경 0건) |
| T-fqx-03 | Tampering | 기존 1~8장 / 부록 / 체크리스트 1~8장 표 | mitigate | 삽입 전용 편집(Edit 툴, `부록 A.` 앵커) + **Task 1/2/3 verify 가 `git show HEAD:` 원본과 prefix/suffix 완전 일치를 강제**. 재배치·재번호 발생 시 즉시 실패 |
| T-fqx-04 | Denial of Service | 실제 카메라/조명 컨트롤러(시리얼) | mitigate | 앱 빌드·실행·화면 캡처 전면 금지(`<hard_constraints>` 2). 스크린샷은 자리표시자로 대체하고 사용자가 재티칭 중 직접 캡처 |
| T-fqx-05 | Tampering (2차 피해) | 사용자의 실제 레시피 데이터 | mitigate | **9-1 백업 절을 3종 티칭 절차보다 앞에 배치**해 재티칭 전 폴더 전체 백업을 먼저 읽게 한다. 백업 방법 2가지(앱 [COPY] / 탐색기 폴더 복사) 모두 제공. Task 1 verify 가 백업 필수 요소(`Recipe`/`.shm`/`복사`) 존재를 자동 확인 |
| T-fqx-06 | Spoofing (허위 절차) | 9장 본문 | mitigate | 화면 문구 창작 금지(`<hard_constraints>` 5). 코드로 확인된 실제 버튼 문구 5종(`Teach Datum`/`패턴 모델 생성`/`Test Find`/`ROI 1 그리기`/`티칭 저장`)이 본문에 존재하는지 verify 로 강제. 미확인 동작은 `[확인 필요: ...]` 로 남김. Bottom 피커센터 패널은 절차 창작 금지 명시 |
| T-fqx-07 | Repudiation | 문서 내용의 근거 | accept | 별도 서명/추적 체계는 두지 않음. 저위험(사내 조작 매뉴얼)이며 `_build_operator_manual_docx.py` 로 재생성 가능해 원고↔산출물 대응은 추적 가능 |
</threat_model>

<verification>
1. Task 1~3 의 각 `<automated>` 검증이 전부 PASS.
2. `Document/Manual/` 의 3개 파일이 갱신되고, `_build_operator_manual_docx.py` 는 (수정했다면) SUMMARY 에 사유가 기록돼 있다.
3. `git status --porcelain Document/Manual/` 에 원고 / 체크리스트 / .docx 3건(+ 정당화된 스크립트 1건)만 `M` 으로 나타난다.
4. `git status --porcelain WPF_Example/` 에 사전 존재하던 `DatumMeasurement.csproj` 외 변경이 없다.
5. 커밋하지 않은 상태로 종료(오케스트레이터가 처리).
</verification>

<success_criteria>
- [ ] 9장이 8장 뒤 · 부록 A 앞에 삽입됐고, 1~8장과 부록은 `git show HEAD:` 원본과 앞/뒤 완전 일치한다(재배치·재번호 0건)
- [ ] 기존 그림 36개의 번호가 그대로이고, 9장 그림은 9-1 부터 연속으로 14개 이상이다
- [ ] Datum / 패턴 / Align 세 가지가 **모두** 실제 절차(①②③ 단계 + 그림 4개 이상) 수준으로 작성됐다 — 하나만 깊고 나머지 개요 형태가 아니다
- [ ] 9-1 에 재티칭 전 레시피 폴더 전체 백업 안내가 있고, 앱 [COPY] 방식과 탐색기 폴더 복사 방식이 모두 설명돼 있으며, COPY 가 Admin 등급 전용이라는 사실이 코드 근거로 반영돼 있다
- [ ] 9장 첫 절이 "1-1절에서 다루지 않는다고 한 티칭을 이 장에서 다룬다"는 관계를 명시해, 개요/1-1절과의 모순을 1~8장 수정 없이 해소했다
- [ ] 9장에 등장하는 모든 버튼/체크박스/메시지 문구가 코드에서 확인한 실제 문자열이다(미확인 항목은 `[확인 필요: ...]`)
- [ ] Bottom 화면의 피커센터 캘리브레이션 패널에 대해 절차를 창작하지 않았다
- [ ] 금지 용어(HALCON/HImage/클래스명/구형 DDA 개념) 0건, 미완성 표시(TODO/TBD 등) 0건
- [ ] 체크리스트가 늘어난 전체 그림 집합과 순서·개수까지 1:1 대응하고, 총 캡처 장수가 갱신됐으며, 1~8장 표는 무변경이다
- [ ] `.docx` 가 재생성되어 Heading 1/2/3 개수가 원고 기대값과 정확히 일치하고 TOC 필드·맑은 고딕이 살아 있다
- [ ] `WPF_Example/` 무변경, 앱 빌드/실행 0회, 화면 캡처 0회, 커밋 0회
</success_criteria>

<output>
완료 후 `.planning/quick/260818-fqx-document-manual-datameasurement-operator/260818-fqx-SUMMARY.md` 를 작성한다.

SUMMARY 에 반드시 포함할 것:
- 갱신한 파일 경로 3개(+ 스크립트를 수정했다면 그 사유)
- 9장 절 구성(절 번호 → 제목)과 각 절의 그림 개수
- **9장에서 새로 늘어난 캡처 장수**와 **문서 전체 총 캡처 장수**(사용자가 몇 장을 더 찍어야 하는지)
- 사용자 후속 작업 안내:
  1. 지금 재티칭 작업을 하는 김에 `..._Screenshot_Checklist.md` 의 9장 목록 순서대로 화면 캡처
  2. `.docx` 를 Word 로 열어 같은 번호의 회색 상자를 이미지로 교체
  3. 목차 우클릭 → "필드 업데이트" 로 페이지 번호 갱신
  4. **재티칭 전 레시피 폴더 백업**(9-1절) 을 반드시 먼저 수행
- `[확인 필요: ...]` 로 남긴 항목이 있으면 전부 목록으로 정리(무엇을 코드에서 확인하지 못했는지 명시)
- 개요/1-1절과의 모순을 어떤 문장으로 해소했는지 한 줄
</output>
</content>
</invoke>
