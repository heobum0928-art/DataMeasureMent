---
phase: quick-260910-dxm
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
  - WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs
autonomous: false
requirements:
  - QUICK-260910-DXM
user_setup: []

must_haves:
  truths:
    - "체커보드 캘리브 [적용] 1회로 레시피에 있는 모든 Shot 의 PixelResolution 이 같은 값이 된다 — SIDE_1~4 사이를 시퀀스 전환하며 4번 적용할 필요가 없다."
    - "확인창 문구에 (a) 활성 시퀀스가 아니라 레시피 전체 SHOT 대상임, (b) 이 PC 가 쓰지 않는 TOP / BOTTOM 샷의 배율값까지 덮인다는 경고, (c) 이 레시피를 다른 PC 로 복사해 쓰면 그쪽 측정이 틀어진다는 경고가 모두 보인다."
    - "체커보드 산출 리포트의 `1 px = ... mm` 가 0.0024 대 값에서 유효숫자 6자리 이상으로 보인다 (기존엔 3자리로 잘렸다)."
    - "확인창·결과창·2점 Calibrate 의 mm/px 표시가 모두 같은 자릿수 규칙을 쓴다 (서로 다른 자릿수로 갈라져 보이지 않는다)."
    - "검사를 한 번도 돌리지 않아 측정 결과표가 빈 상태에서도 [라이브 촬상] 이 이미지를 반환한다 — 활성 시퀀스 추정이 빗나가 소유 Shot 이 0건일 때 첫 Shot 의 카메라로 폴백한다."
    - "라이브 촬상의 카메라 선택 우선순위는 불변이다 — 활성 시퀀스 소유 Shot 을 먼저 찾고, 그 탐색이 실패했을 때만 폴백한다 (TOP / BOTTOM PC 회귀 금지)."
    - "폴백이 발동하면 Trace 로그에 실제로 사용한 Shot 이름과 카메라(DeviceName) 가 남는다 — 조용한 카메라 대체 금지."
    - "레시피에 Shot 이 하나도 없으면 종전대로 null 을 반환해 '라이브 촬상 실패.' 안내가 뜬다 (기존 실패 경로 보존)."
    - "계산·저장 정밀도는 무변경이다. PixelResolution 은 double 원본 그대로 저장되고, 표시 자릿수만 바뀐다."
    - "이더넷 정렬 카메라의 체커보드 캘리브(EthernetPixelResolution)는 전혀 영향받지 않는다."
    - "신규 `.cs` 파일 0개, `DatumMeasurement.csproj` 무변경 — classic MSBuild 등록 회피."
  artifacts:
    - "WPF_Example/UI/ContentItem/MainView.xaml.cs — ApplyCheckerboardCalibration: 소유 시퀀스 필터 제거 + 확인창/결과창 문구 교체"
    - "WPF_Example/UI/ContentItem/MainView.xaml.cs — 2점 Calibrate 의 mm/px 표시 2곳 자릿수 확대"
    - "WPF_Example/UI/ContentItem/MainView.xaml.cs — GrabCalibrationImage: 소유자 필터 탐색 실패 시 첫 Shot 폴백 블록 + Trace 로그 (라이브 촬상 실패 버그 수정)"
    - "WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs — 산출 리포트의 mm/px 3개 필드 자릿수 확대"
  key_links:
    - "ApplyCheckerboardCalibration 루프 → shot.PixelResolution (Phase 42 단일소스, 측정 소비처) + fai.PixelResolutionX/Y (INI 호환 보존) — 이 3개 쓰기 조합을 깨면 측정이 틀어진다"
    - "MainWindow.SaveRecipe → existingFile 보존 경로 (비활성 시퀀스 소실 방지, 3faa91b). 전체 Shot 적용 후에도 이 저장 경로를 그대로 쓴다"
    - "GrabCalibrationImage 의 ResolveActiveSequenceForCalibration + 소유자 필터 → 라이브 grab 카메라 선택. 이번 필터 제거 대상이 아니다(보존 필수). 그 뒤에 폴백 블록만 덧붙는다 — 필터가 먼저, 폴백이 나중이라는 이 순서가 TOP / BOTTOM PC 무회귀의 근거다"
    - "dataGrid_faiResults.SelectedItem → ResolveActiveSequenceForCalibration → GrabCalibrationImage 의 camShot 탐색 → pDev.GrabHalconImage. 결과표가 비면 이 사슬이 끊겨 grab 자체가 호출되지 않는다 (이번 버그의 실제 경로)"
    - "CalibrationResult.MmPerPixel / MmPerPixelX / MmPerPixelY → 표시 전용 소비 경로. CheckerboardCalibrationService 의 산출 로직은 무변경"
---

<objective>
체커보드 픽셀 캘리브레이션을 활성 시퀀스의 Shot 에만 적용하던 필터를 제거해 레시피 전체 Shot 에
1회로 일괄 적용하고, mm/px 표시 자릿수를 유효숫자가 보이는 수준으로 확대한다.

Purpose: 이 장비는 SIDE PC 로 물리 카메라가 1대다. 시퀀스만 SIDE_1~4 로 쪼개져 있어 픽셀 배율은
전부 같은데, 현재는 시퀀스를 바꿔가며 캘리브를 4번 적용해야 한다. 또한 실측값
`0.00240297321001669` 이 기존 자릿수에서는 `0.00240` 으로 잘려 유효숫자가 3자리뿐이라
사용자가 값이 제대로 들어갔는지 화면에서 확인할 수 없다.

또한 같은 창의 [라이브 촬상] 이 항상 실패하던 버그를 고친다. 활성 시퀀스를 측정 결과표 선택 행으로
추정하는데, 검사를 한 번도 돌리지 않으면 결과표가 비어 TOP 으로 폴백하고, SIDE 전용 PC 에는 TOP 소유
Shot 이 없어 카메라에 도달하기 전에 null 을 반환했다. 소유 Shot 탐색이 실패하면 첫 Shot 의 카메라로
폴백하도록 고친다(이 장비는 물리 카메라 1대).

Output: 기존 메서드 3곳 + 1개 리포트 문자열 수정. 신규 파일·신규 메서드 없음.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@C:/code/DataMeasurement/CLAUDE.md
@C:/code/DataMeasurement/.planning/quick/260910-dxm-shot-mm-px/260910-dxm-PLAN.md

주요 소스 (편집 전 라인번호 재확인 필수 — 파일이 4,583줄이라 라인이 쉽게 밀린다):
@C:/code/DataMeasurement/WPF_Example/UI/ContentItem/MainView.xaml.cs
@C:/code/DataMeasurement/WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs
</context>

<investigation_findings>
아래는 플래너가 코드로 직접 확인한 사실이다. 실행자는 이 판단을 그대로 따르되, 라인번호만 재확인한다.

## 1. 편집 지점 7곳 (플래너 실측 라인번호, 2026-09-10 기준)

| # | 파일 | 약 라인 | 현재 | 성격 |
|---|------|---------|------|------|
| 1 | MainView.xaml.cs | 3430 | `string activeSeq = ResolveActiveSequenceForCalibration();` | **삭제** (필터 제거로 미사용 → 경고 유발) |
| 2 | MainView.xaml.cs | 3436~3438 | 확인창 `string msg = string.Format(...)` | 문구 전면 교체 + 자릿수 |
| 3 | MainView.xaml.cs | 3455~3459 | `owner` 지역변수 산출 2줄 + 비교 continue 1줄 | **필터 3줄 삭제** |
| 4 | MainView.xaml.cs | 3473 | 결과창 `"{0}개 SHOT 에 적용 + 저장 완료 ..."` | 문구 보강 + 자릿수 |
| 5 | MainView.xaml.cs | 3250 / 3260 | 2점 Calibrate 의 mm/px 표시 2곳 | 자릿수만 |
| 6 | CalibrationWindow.xaml.cs | 196 | 리포트 1번째 필드 `1 px = ... mm (X ... / Y ...)` 3개 인자 | 자릿수만 |
| 7 | MainView.xaml.cs | 3518 | `GrabCalibrationImage` 의 소유자 필터 루프 직후 — `camShot` 미발견 시 즉시 null 반환 | **폴백 블록 추가** (Task 2, 버그 수정) |

6번까지가 Task 1(적용 범위 + 자릿수), 7번이 Task 2(라이브 촬상 버그)다. 같은 파일을 두 Task 가
순차로 편집하므로 Task 1 의 게이트는 Task 1 시점의 diff 만 본다.

## 2. `ResolveActiveSequenceForCalibration` 은 삭제하지 않는다 — 소비처가 2개다

파일 전체 3회 등장: 정의(약 :3411), `ApplyCheckerboardCalibration` 호출(약 :3430, **이번에 제거**),
`GrabCalibrationImage` 호출(약 :3500, **보존**).

`GrabCalibrationImage`(약 :3497~3539)는 "활성 시퀀스의 첫 Shot 을 찾아 그 카메라 param 으로
`pDev.GrabHalconImage` 한다" 는 전혀 다른 목적이라 같은 모양의 소유자 필터(약 :3510~3514)를
자체적으로 갖고 있다. **그 필터는 건드리지 않는다.** 없애면 라이브 촬상이 엉뚱한 카메라를 잡는다.
임시 이미지 파일명(`"Calibration_" + activeSeq`)도 그 메서드 안이라 그대로다.

결과적으로 파일 전체의 소유자 비교 `continue` 는 2회 → 1회가 되어야 한다. 0회가 되면 라이브 촬상을
망가뜨린 것이다 (게이트 #3 이 이것을 잡는다).

### 두 소비처의 운명이 갈리는 지점 (중요)

같은 메서드를 쓰지만 이번 작업에서 **정반대로 취급**된다. 헷갈리면 둘 다 망가진다.

| 소비처 | 목적 | 이번 처리 |
|--------|------|-----------|
| `ApplyCheckerboardCalibration` | 적용 대상 Shot 선별 | **의존 제거.** 전체 Shot 일괄 적용이 되면 "활성 시퀀스" 개념 자체가 사라진다 |
| `GrabCalibrationImage` | grab 할 카메라 param 선택 | **의존 유지 + 폴백 추가.** 카메라를 하나 골라야 하므로 개념이 여전히 필요하다 |

즉 이 메서드는 "적용 범위 결정자" 역할을 잃고 "카메라 선택 힌트" 역할만 남는다. 힌트가 빗나갔을 때
실패하지 않게 만드는 것이 Task 2 다.

### 게이트 계수 재계산 (Task 2 추가분 반영)

Task 2 의 폴백 코드가 게이트 #3 의 두 계수를 흔드는지 확인했다. **둘 다 불변이다.**

- `activeSeq) continue` → **1** 유지. 폴백 블록은 소유자 비교를 하지 않는다(비교 없이 첫 Shot 을 집는
  것이 폴백의 정의다). 새 `continue` 는 null Shot 건너뛰기용이라 이 패턴과 문자열이 다르다.
- `ResolveActiveSequenceForCalibration` 참조 → **2** 유지 (정의 1 + 라이브 grab 호출 1). 폴백은 이
  메서드를 다시 호출하지 않는다.

단, 새 주석이나 로그 문구에 위 두 문자열을 그대로 적으면 계수가 틀어진다. Task 2 action 의 금지
항목에 넣었다.

## 3. 자릿수 결정 = F8 (근거)

실기 레시피(`D:\Data\Recipe\FAI_1`) 실측값으로 비교:

| 지정자 | 0.00240297321001669 | 0.00265 (BOTTOM) | 유효숫자 | 판정 |
|--------|---------------------|------------------|----------|------|
| F4 (현 2점 Calibrate) | 0.0024 | 0.0027 | 2자리 | 사용자 지적 그대로 — 쓸 수 없다 |
| F5 (현 체커보드) | 0.00240 | 0.00265 | 3자리 | 유효숫자 부족 |
| **F8 (채택)** | **0.00240297** | **0.00265000** | **6자리** | 요구 충족 + 후행 0 4개로 허용 범위 |
| F10 | 0.0024029732 | 0.0026500000 | 8자리 | 후행 0 6개 — 노이즈, 읽는 속도 저하 |

F8 을 전 표시 지점에 동일 적용한다. 요구사항의 "유효숫자 최소 6자리"를 0.002x 대역에서 정확히
충족하는 최소 자릿수이면서, 딱 떨어지는 값에서 무의미한 0 이 과하게 붙지 않는 균형점이다.

**자릿수 상수(const)는 만들지 않는다.** 근거 2가지:
(a) 두 파일은 서로 다른 클래스(`MainView` / `CalibrationWindow`)라 공유 const 를 두려면 신규 `.cs`
   파일이 필요한데 이번 작업은 신규 파일 생성 금지다. 파일별로 각각 const 를 두면 같은 표시 계약의
   단일 소스가 둘로 쪼개져 오히려 나빠진다.
(b) 이 두 파일은 복합 형식 문자열 안의 지정자를 전부 리터럴로 쓰는 기존 관례(F1/F2/F3 다수)를 갖고
   있어, 한 곳만 상수화하면 일관성이 깨진다.
매직넘버 금지 규칙은 계산에 쓰이는 수치 상수를 겨냥한 것이고, 복합 형식 문자열의 표시 지정자는
그 대상이 아니라고 판단했다. 이 판단 근거를 SUMMARY 에 기록할 것.

## 4. 거리(mm) 표시는 자릿수를 바꾸지 않는다

같은 메서드 안의 `{0:F3}mm` 계열 6곳은 **mm/px 가 아니라 mm 거리값**(0.x~수십 mm 범위)이다.
F3 = μm 단위까지라 이미 충분하고, 이번 불만(배율값이 잘려 보인다)과 무관하다. 건드리면
무관한 diff 확산 + 측정 결과 표시 회귀 위험만 생긴다. 게이트 #5 가 이 6곳의 불변을 확인한다.

## 5. `applied` 카운트 의미 변화

필터 제거 후 `applied` 는 "레시피의 null 이 아닌 전체 Shot 수" 가 된다. 결과창 문구에서 그 수치는
여전히 유효하므로 계산식은 그대로 두고 문구에만 "전체" 를 명시한다.

## 6. 확인 게이트 순서는 그대로 둔다

현재 흐름은 "확인창 → 레시피 매니저 조회 → 적용" 이다. 확인창에 적용 대상 Shot 개수를 미리
보여주려면 매니저 조회를 확인창 앞으로 끌어올려야 하는데, 그것은 새 로직이다.
`MainView.xaml.cs` 에 새 로직 추가 금지 원칙에 따라 **순서를 바꾸지 않는다.** 경고는 개수 없이
"전체 SHOT (TOP / BOTTOM 포함)" 이라는 범위 명시로 충분하다.

## 7. 정렬 비전 쪽은 완전 별개 (확인 완료)

`Custom/UI/BottomVisionView.xaml.cs` / `Custom/UI/TrayVisionView.xaml.cs` 의
`ApplyEthernetCheckerboardCalibration` 은 이더넷 정렬 카메라의 `EthernetPixelResolution` 을 다루는
다른 기능이다. 같은 "체커보드" 단어를 쓰지만 소비처가 다르다. **이번 diff 에 포함되면 실패다**
(게이트 #6).

## 8. [라이브 촬상] 실패의 확정된 원인 (Task 2 근거)

**증상:** 캘리브 창에서 [라이브 촬상] → `"라이브 촬상 실패."` 만 뜨고 이미지가 오지 않는다.
**결정적 단서:** Camera 로그에 grab 시도 자체가 안 찍힌다 → 카메라에 도달하기 전에 `null` 이 반환된다.

**원인 사슬 (코드로 확정):**

1. `ResolveActiveSequenceForCalibration`(약 :3411) 이 "활성 시퀀스" 를 **측정 결과표
   (`dataGrid_faiResults`) 에서 선택된 행** 으로 판단한다. 선택 행 → FAI → 소유 Shot → 그 Shot 의
   소유 시퀀스명 순으로 거슬러 올라간다.
2. 검사를 한 번도 돌리지 않았거나 아무 행도 선택하지 않으면 그 사슬이 첫 칸에서 끊겨 **기본값 폴백**
   (TOP)이 반환된다. 이 폴백 자체는 2점 캘리브 시절의 합리적 기본값이었다.
3. `GrabCalibrationImage`(약 :3498) 의 소유자 필터(약 :3514)가 그 시퀀스를 소유한 Shot 을 찾는다.
4. **이 PC 는 SIDE 전용(물리 카메라 1대)이라 해당 소유 Shot 이 0건** → `camShot` 이 끝까지 비고
   약 :3517 에서 즉시 `null` 반환 → 창이 실패 메시지를 띄운다. grab 호출에 도달하지 못한다.

**처음 몇 번 성공했던 이유:** 그때는 검사를 돌린 뒤라 결과표에 SIDE Shot 행이 있었다. 즉 이 버그는
"검사 전에 캘리브하려 할 때" 만 재현되는 상태 의존 버그다 — 하필 캘리브를 가장 하고 싶은 시점이다.

**수정 방향 = 최소 추가:** 추정 로직(1~2)은 건드리지 않는다. TOP / BOTTOM PC 에서는 결과표 선택 →
소유 Shot 매칭이 정상 동작하므로 기존 경로가 여전히 최선이다. **4번 지점에서 실패 대신 첫 Shot 으로
폴백** 한다. 사용자 확정 사실: 이 장비는 카메라 1대라 어느 Shot 을 잡아도 같은 카메라다.

**폴백을 별도 루프로 두는 이유:** 기존 필터 루프 안에 "첫 Shot 기억" 변수를 끼워 넣는 쪽이 줄 수는
적지만, 그러면 **정상 경로의 루프 본문을 수정** 하게 된다. 루프를 한 글자도 안 건드리고 뒤에 블록만
덧붙이면 TOP / BOTTOM PC 경로의 회귀 위험이 구조적으로 0 이고, 우선순위 역전도 물리적으로 불가능하다.
읽는 사람에게도 "먼저 이걸 시도, 실패하면 저걸" 이 한눈에 보인다.

**임시 이미지 파일명은 그대로 둔다.** 파일명이 추정 시퀀스명을 쓰므로 폴백 시 실제 카메라와 이름이
어긋날 수 있으나, (a) 무엇을 썼는지는 Trace 로그가 정확히 남기고, (b) 파일명까지 바꾸려면 실제 사용
Shot 을 추적하는 변수가 하나 더 필요해 "버그 수정 최소 추가" 를 넘는다. 이 판단을 SUMMARY 에 남긴다.
</investigation_findings>

<tasks>

<task type="tracer">
  <name>Task 1: 전체 Shot 일괄 적용 + mm/px 자릿수 확대</name>
  <files>WPF_Example/UI/ContentItem/MainView.xaml.cs, WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs</files>
  <precondition>`tasklist | grep -i DatumMeasurement` — 플래닝 시점에 앱이 실행 중이었다(PID 6420). 실행 중이면 기본 빌드가 `bin/x64/Debug` 파일 잠금으로 실패한다. **강제 종료하지 말 것.** 사용자에게 보고하고, 문법 검증만 필요하면 verify 의 우회 빌드(별도 OutputPath)를 쓴다.</precondition>
  <read_first>
    - WPF_Example/UI/ContentItem/MainView.xaml.cs 약 :3236~3266 (2점 Calibrate 의 입력→적용→안내 흐름, 표시 지점 2곳)
    - WPF_Example/UI/ContentItem/MainView.xaml.cs 약 :3405~3420 (ResolveActiveSequenceForCalibration 정의 + 설명 주석)
    - WPF_Example/UI/ContentItem/MainView.xaml.cs 약 :3424~3476 (ApplyCheckerboardCalibration 전체 — 이번 주 편집 대상)
    - WPF_Example/UI/ContentItem/MainView.xaml.cs 약 :3494~3539 (GrabCalibrationImage — 같은 모양의 필터를 갖고 있으나 **보존 대상**)
    - WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs 약 :190~215 (리포트 string.Format + 경고 통합 표시)
  </read_first>
  <action>
두 파일의 기존 메서드 안에서만 수정한다. 신규 메서드·신규 필드·신규 파일 전부 만들지 않는다.
각 파일의 기존 중괄호 스타일을 따른다 — MainView.xaml.cs 는 K&R, CalibrationWindow.xaml.cs 는 Allman.

### (A) MainView.xaml.cs — ApplyCheckerboardCalibration: 소유 시퀀스 필터 제거

1. 메서드 진입부의 활성 시퀀스 조회 지역변수 선언(약 :3430)을 **삭제한다.** 확인창이 더는 그 값을
   쓰지 않으므로 남겨두면 미사용 변수가 된다.
2. Shot 순회 루프 안(약 :3455~3459)의 소유자 판정 블록을 **전부 삭제한다** — 소유 시퀀스명을
   구하는 지역변수 선언과 그 분기 2줄, 그리고 불일치 시 `continue` 하는 1줄까지 세 덩어리 전부다.
   삭제 후 루프 본문은 `shot` null 가드 → `shot.PixelResolution` 대입 → `FAIList` 순회 →
   `applied` 증가 순서만 남는다.
3. 쓰기 3종(`shot.PixelResolution`, `fai.PixelResolutionX`, `fai.PixelResolutionY`)과 그 옆 설명
   주석은 **그대로 둔다.** 단일소스 + INI 호환 보존 조합이라 하나라도 빠지면 측정이 틀어진다.
4. 저장 호출(`MainWindow.SaveRecipe`) 경로도 그대로다. existingFile 보존 가드가 그 안에 있다.

### (B) MainView.xaml.cs — 확인창 문구 전면 교체 (약 :3436~3438)

`string.Format` 인자가 3개(활성시퀀스/배율/경고)에서 **2개(배율/경고)** 로 줄어든다. 자리표시자
번호를 반드시 0,1 로 다시 매길 것 — 번호를 안 고치면 런타임 `FormatException` 이다.

형식 문자열은 아래 5줄을 `+` 로 이어 붙인 것이어야 한다(`\n` 은 C# 문자열 이스케이프, 실제 줄바꿈
문자를 소스에 넣지 말 것). 배율 자리는 첫 번째 인자에 F8 지정자, 경고 줄은 두 번째 인자다.

- 1줄: `레시피의 전체 SHOT (이 PC 가 쓰지 않는 TOP / BOTTOM 샷까지 포함) 의 PixelResolution 을`
- 2줄: `1 px = (배율, F8) mm 로 덮어씁니다.(경고)` 뒤에 빈 줄 하나
- 3줄: `[주의] TOP / BOTTOM 은 원래 SIDE 와 다른 배율값을 갖습니다. 이 적용으로 그 값들도 SIDE 값으로 덮입니다.`
- 4줄: `이 PC 는 SIDE 만 검사하므로 무해하지만, 이 레시피를 다른 PC 로 복사해 쓰면 그쪽 측정이 틀어집니다.`
- 5줄: `되돌리기 어려운 덮어쓰기입니다. 적용하시겠습니까?`

기존 왜곡 경고 줄(`warnLine`) 산출 코드와 확인 모달 호출(`CustomMessageBox.ShowConfirmation`,
`MessageBoxButton.OKCancel`), 취소 시 조기 return 은 그대로 둔다. 왜곡 편차 퍼센트의 F2 지정자도
그대로다(퍼센트값이라 자릿수 불만과 무관).

### (C) MainView.xaml.cs — 결과창 문구 (약 :3473)

`"{0}개 SHOT 에 적용 + 저장 완료 ..."` 의 개수 앞에 `전체` 를 붙여 범위를 명시하고, 배율 자리의
지정자를 F8 로 바꾼다. 인자 순서·개수는 그대로다.

### (D) MainView.xaml.cs — 2점 Calibrate 의 mm/px 표시 2곳 (약 :3250, :3260)

같은 물리량(mm/px)을 보여주는 지점이라 함께 맞춘다. 기존 F4 는 0.0024 대 값에서 유효숫자 2자리로
체커보드 쪽보다 더 심하게 잘린다. 두 곳 모두 배율 인자의 지정자를 F8 로 바꾼다.

**같은 구역의 mm 거리 표시(총거리/가로/세로, F3 지정자 6곳)는 건드리지 않는다.** 그것은 배율이
아니라 mm 환산 거리값이다. 픽셀 거리의 F1 지정자도 그대로다.
"메모리에만 반영됐고 레시피 파일에는 저장되지 않았습니다" 경고 본문도 그대로 둔다 — 2점 Calibrate
는 여전히 SaveRecipe 를 호출하지 않는 기존 동작이다.

### (E) CalibrationWindow.xaml.cs — 산출 리포트 (약 :196)

리포트 첫 줄 `1 px = ... mm (X ... / Y ...)` 의 배율 3개 인자(전체/X축/Y축) 지정자를 전부 F8 로
바꾼다. 같은 형식 문자열의 나머지 필드(px 간격 F2, 편차 퍼센트 F2, 규칙성 F1 등)는 그대로다.
`CultureInfo.InvariantCulture` 인자와 ROI 표기 등 기존 인자 구성도 그대로다.

이 파일에는 기존 조건 연산자가 포함된 줄(ROI 좌표 인자 등)이 있지만 **이번에 수정하는 줄은 형식
문자열 한 줄뿐이므로 그 줄들은 diff 에 들어가지 않는다.** 리팩토링하지 말 것 — 범위 밖이다.

### (F) 낡은 주석 정정

필터 제거로 사실과 달라지는 설명 주석 2곳을 고친다.

1. `ApplyCheckerboardCalibration` 머리 주석(약 :3425~3426): "활성 시퀀스 전체 shot 에 일괄 반영" →
   레시피 전체 shot 기준으로 고치고, 물리 카메라가 1대라 시퀀스 구분이 의미 없다는 이유를 한 줄 덧붙인다.
2. `ResolveActiveSequenceForCalibration` 머리 주석(약 :3408~3410): 체커보드 캘리브가 활성 시퀀스
   전체 shot 에 반영된다는 설명이 이제 틀렸다. 이 메서드의 남은 용도가 **라이브 촬상 카메라 선택**
   뿐임을 적는다.

두 주석 모두 기존 앞머리의 `260623` 날짜 토큰과 작성자 이니셜은 버리고 `Phase 53:` 형태의 출처만
남긴다 (날짜·이니셜 접두 주석 신규 생성 금지 규칙). 기존에 파일에 남아 있는 다른 legacy 주석은
건드리지 않는다.

### 하드룰
삼항 연산자 / null 병합 연산자 / null 조건 연산자 / C# 8 switch 식 전부 금지 — 명시적 if/else 로
쓴다. 한 줄짜리 분기도 중괄호를 생략하지 않는다. 불리언은 `b`, 정수는 `n`, 문자열은 `sz`,
실수는 `d` 접두사. 매직넘버 금지. C# 7.2 문법만.
**새 주석에 옛 형식 지정자 문자열이나 삭제한 비교식을 그대로 인용하지 말 것** — 검증 게이트가
소스 전체를 grep 하므로 주석에 남기면 게이트가 자기 자신을 무효화한다.
이번 작업은 기존 메서드 안의 줄 삭제 + 문자열 교체로 끝나야 한다. 새 헬퍼 메서드가 필요하다고
느껴지면 범위를 잘못 잡은 것이다.

### 절대 건드리지 말 것
`WPF_Example/DatumMeasurement.csproj` (신규 `.cs` 파일 생성 금지),
`WPF_Example/Halcon/Algorithms/CheckerboardCalibrationService.cs` (계산·저장 정밀도 무변경),
`WPF_Example/Custom/UI/BottomVisionView.xaml.cs` / `TrayVisionView.xaml.cs`
(이더넷 정렬 카메라용 별개 기능), `GrabCalibrationImage` 안의 카메라 선택 필터,
`D:\Data\Recipe\FAI_1` 실기 레시피 파일.
  </action>
  <verify>
    <automated>
```bash
cd /c/code/DataMeasurement
MV=WPF_Example/UI/ContentItem/MainView.xaml.cs
CW=WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs

# 0) 앱 실행 여부 — 실행 중이면 강제 종료 금지, 보고 후 1b 로 간다
tasklist | grep -i DatumMeasurement && echo "APP RUNNING" || echo "app not running"

# 1) 빌드 게이트 (앱 미실행 시). CS0618/CS0169 경고는 baseline, 게이트 아님
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" \
  WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal \
  2>&1 | tee /tmp/dxm-build.log | tail -20
grep -c ": error " /tmp/dxm-build.log            # 0 이어야 함

# 1b) 앱 실행 중 우회 (문법 검증 전용 — bin 잠금 회피). 이 경로를 썼다면 SUMMARY 에 명시할 것
#     MSB3021 류 복사 오류는 게이트 아님. CS 오류만 본다
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" \
  WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal \
  -p:OutputPath=C:/Temp/dxm-build/ 2>&1 | tee /tmp/dxm-build.log | tail -20
grep -c ": error CS" /tmp/dxm-build.log          # 0 이어야 함

# 2) 필터 제거 — ApplyCheckerboardCalibration 본문에 활성시퀀스 지역변수 0회 (baseline 3)
sed -n '/private void ApplyCheckerboardCalibration/,/^        private void OpenCheckerboardCalibrationButton_Click/p' $MV \
  | grep -c 'activeSeq'                          # 0 이어야 함

# 3) 라이브 촬상 필터 보존 — 파일 전체 비교 continue 2회 → 1회, 메서드 참조 3회 → 2회
grep -c 'activeSeq) continue' $MV                # 1 (0 이면 라이브 촬상을 망가뜨린 것)
grep -coF 'ResolveActiveSequenceForCalibration' $MV   # 2 (정의 1 + GrabCalibrationImage 호출 1)

# 4) 자릿수 — 옛 지정자 소멸 / 새 지정자 정확한 개수
grep -cE ':F[45]\}' $MV                          # 0 (baseline 4)
grep -oE ':F8\}' $MV | wc -l                     # 4 (확인창1 + 결과창1 + 2점Calibrate2)
grep -cE ':F5\}' $CW                             # 0 (baseline 1줄에 3건 — -c 는 줄 수를 센다)
grep -oE ':F8\}' $CW | wc -l                     # 3 (전체/X/Y)

# 5) mm 거리 표시 불변 (무관한 diff 확산 방지)
grep -oE ':F3\}' $MV | wc -l                     # 6 (baseline 그대로)

# 6) 금지 파일 미변경 + diff 범위가 정확히 두 파일
git diff --name-only | grep -cE 'DatumMeasurement.csproj|CheckerboardCalibrationService|BottomVisionView|TrayVisionView'   # 0
git diff --name-only -- WPF_Example | wc -l      # 2

# 7) 경고 문구 필수 토큰 (조용한 덮어쓰기 방지)
sed -n '/private void ApplyCheckerboardCalibration/,/^        private void OpenCheckerboardCalibrationButton_Click/p' $MV \
  > /tmp/dxm-apply.txt
grep -c '전체 SHOT' /tmp/dxm-apply.txt           # >=1
grep -c 'TOP' /tmp/dxm-apply.txt                 # >=1
grep -c 'BOTTOM' /tmp/dxm-apply.txt              # >=1
grep -c '다른 PC' /tmp/dxm-apply.txt             # >=1

# 8) 쓰기 3종 보존 (단일소스 + INI 호환)
grep -c 'shot.PixelResolution = mmPerPixel' $MV      # 2 (체커보드 1 + 2점 Calibrate 1)
grep -c 'fai.PixelResolutionX = mmPerPixel' $MV      # 2
grep -c 'fai.PixelResolutionY = mmPerPixel' $MV      # 2

# 9) 가독류 게이트 — diff 추가 라인만 대상 (두 파일 모두 legacy 위반이 이미 다수 존재)
D=$(git diff -U0 -- $MV $CW | grep '^+' | grep -v '^+++')
printf '%s\n' "$D" | grep -vE '^\+\s*//' | grep -cE '\?[^?]*:'   # 삼항: 0
printf '%s\n' "$D" | grep -vE '^\+\s*//' | grep -cF '??'          # null 병합: 0
printf '%s\n' "$D" | grep -vE '^\+\s*//' | grep -cF '?.'          # null 조건: 0
printf '%s\n' "$D" | grep -vE '^\+\s*//' | grep -cE 'switch.*=>'  # switch 식: 0
printf '%s\n' "$D" | grep -cF 'hbk'                               # 날짜·이니셜 주석: 0
```
    </automated>
  </verify>
  <done>
빌드 CS 오류 0. 게이트 2~9 전부 기대값 일치:
`ApplyCheckerboardCalibration` 본문에 활성 시퀀스 의존이 0건이고, `GrabCalibrationImage` 의 카메라
선택 필터는 1건 살아 있다. mm/px 표시 지점 7곳(MainView 4 + CalibrationWindow 3) 전부 F8 이고
옛 지정자는 0건, mm 거리 표시 6곳은 불변이다. 확인창에 전체 SHOT / TOP / BOTTOM / 다른 PC 경고
토큰이 모두 존재한다. diff 는 정확히 두 파일이며 금지 파일은 변경되지 않았다.
  </done>
</task>

<task type="auto">
  <name>Task 2: [라이브 촬상] 실패 버그 수정 — 소유 Shot 미발견 시 첫 Shot 카메라 폴백</name>
  <files>WPF_Example/UI/ContentItem/MainView.xaml.cs</files>
  <precondition>Task 1 이 같은 파일을 이미 편집했다 — 삭제한 줄들 때문에 라인번호가 위로 밀려 있다. 편집 전 `grep -n 'private string GrabCalibrationImage' WPF_Example/UI/ContentItem/MainView.xaml.cs` 로 현재 위치를 다시 잡을 것. 앱 실행 여부는 Task 1 의 precondition 과 동일 규약(강제 종료 금지).</precondition>
  <read_first>
    - WPF_Example/UI/ContentItem/MainView.xaml.cs — `GrabCalibrationImage` 전체 (소유자 필터 루프 → camShot 판정 → 계측 stopwatch → pDev.GrabHalconImage → SaveTempImage + tact 로그 → finally Dispose → catch)
    - WPF_Example/UI/ContentItem/MainView.xaml.cs — `ResolveActiveSequenceForCalibration` 정의 (이번 수정 대상 **아님**. 반환 규약만 확인)
    - 이 PLAN 의 investigation_findings #8 — 원인 사슬 4단계 + 폴백을 별도 루프로 두는 이유 + 파일명을 안 바꾸는 이유
  </read_first>
  <action>
`GrabCalibrationImage` 안에서만 수정한다. 다른 메서드·다른 파일은 건드리지 않는다. 신규 헬퍼 메서드·
신규 필드·신규 파일 전부 만들지 않는다. MainView.xaml.cs 는 K&R 중괄호 스타일이다.

### (A) 기존 소유자 필터 루프는 한 글자도 수정하지 않는다

루프 헤더·본문·null 가드·소유 시퀀스 판정·`break` 전부 그대로 둔다. 이 루프가 **1순위** 이고
TOP / BOTTOM PC 에서는 여기서 매칭이 성공하므로 그쪽 동작은 변하지 않는다. 이 루프를 손대거나
폴백을 앞으로 옮기면 우선순위가 뒤집혀 실패다.

### (B) 루프 직후의 즉시 실패 한 줄을 폴백 블록으로 교체

현재 필터 루프 다음에는 "카메라용 Shot 을 못 찾았으면 곧바로 null 을 반환" 하는 한 줄이 있다. 이
한 줄을 아래 구조로 바꾼다.

1. "못 찾았다" 조건으로 분기한다.
2. 그 안에서 레시피 Shot 목록을 처음부터 다시 훑어 **null 이 아닌 첫 Shot** 을 집고 곧바로 `break`
   한다. 소유 시퀀스 비교는 **하지 않는다** — 비교 없이 첫 것을 집는 것이 폴백의 정의다. 루프
   인덱스는 `nIdx` 로 선언한다(헝가리언). 바깥 루프 인덱스와 스코프가 달라 충돌하지 않는다.
3. 폴백으로 Shot 을 실제로 집었을 때**만** `Logging.PrintLog((int)ELogType.Trace, ...)` 로 한 줄
   남긴다. 로그에는 집은 Shot 의 이름(`ShotName`) 과 카메라(`DeviceName`) 를 **반드시 둘 다** 넣는다.
   어느 카메라를 썼는지 모르면 나중에 원인 추적이 불가능하다. 문구에 물음표 문자를 쓰지 말 것
   (가독류 게이트가 물음표 뒤 콜론을 삼항으로 오인한다).
4. 그 뒤에 **여전히 못 찾았으면 null 을 반환** 하는 최종 가드를 둔다. 레시피 Shot 이 0건인 경우라
   기존 실패 동작(창의 실패 안내)이 그대로 유지돼야 한다. 이 가드를 빼면 뒤에서 역참조 예외가 난다.

한 줄짜리 분기라도 중괄호를 생략하지 않는다.

### (C) 왜 그런지 설명하는 주석 2~3줄

폴백 블록 위에 주석을 단다. 반드시 담을 내용 3가지:
- 활성 시퀀스 추정이 측정 결과표 선택에 의존하므로 검사 전(결과표가 빈 상태)에는 추정이 빗나간다
- 이 장비는 물리 카메라가 1대라 어느 Shot 을 잡아도 같은 카메라다
- 1순위 탐색이 먼저고 이 블록은 그 탐색이 실패했을 때만 돈다 (TOP / BOTTOM PC 무영향)

날짜·작성자 이니셜 접두사는 붙이지 않는다.

### (D) 건드리지 않는 것 (명시)

- **임시 이미지 파일명 인자** — 추정 시퀀스명을 그대로 쓴다. 폴백 시 실제 카메라와 이름이 어긋날 수
  있지만 무엇을 썼는지는 Trace 로그가 정확히 남긴다. 근거는 investigation_findings #8 마지막 단락.
- **`ResolveActiveSequenceForCalibration` 의 반환 로직** — 기본값 폴백 포함 전부 불변. 여기를 고치면
  TOP / BOTTOM PC 의 정상 경로가 변한다. 이번 수정은 이 메서드를 고치는 게 아니라 **그 결과로 Shot 을
  못 찾았을 때의 뒤처리를 추가** 하는 것이다.
- 기존 grab/저장 tact 계측 stopwatch 2개와 그 Trace 로그, `grabbed.Dispose()` 의 `finally`, 메서드
  전체를 감싼 `catch { return null; }` — throw 금지 규약이라 그대로다.
- Task 1 이 수정한 지점 전부 — 되돌리지 말 것.

### 하드룰
삼항 연산자 / null 병합 연산자 / null 조건 연산자 / C# 8 switch 식 전부 금지 — 명시적 if/else 로
쓴다. 정수는 `n`, 문자열은 `sz`, 불리언은 `b` 접두사. 매직넘버 금지. C# 7.2 문법만.
**새 주석·로그 문구에 코드 조각을 그대로 인용하지 말 것** — 소유자 비교식, 활성 시퀀스 결정 메서드
이름, 반환문, 로그 타입 표기 전부 해당된다. 게이트가 소스를 grep 해 **정확한 계수** 를 확인하므로
주석에 한 번만 적어도 게이트가 자기 자신을 무효화한다. 주석은 "왜" 를 평문으로만 쓴다.
헬퍼 메서드가 필요하다고 느껴지면 범위를 잘못 잡은 것이다. 이 수정은 한 메서드 안에서 끝난다.
  </action>
  <verify>
    <automated>
```bash
cd /c/code/DataMeasurement
MV=WPF_Example/UI/ContentItem/MainView.xaml.cs
sed -n '/private string GrabCalibrationImage/,/private void TeachDatumButton_Click/p' $MV > /tmp/dxm-grab.txt

# 0) 앱 실행 여부 — Task 1 과 동일 규약. 실행 중이면 강제 종료 금지, 1b 우회로 간다
tasklist | grep -i DatumMeasurement && echo "APP RUNNING" || echo "app not running"

# 1) 빌드 게이트 (앱 미실행 시)
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" \
  WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal \
  2>&1 | tee /tmp/dxm-build2.log | tail -20
grep -c ": error " /tmp/dxm-build2.log            # 0 이어야 함

# 1b) 앱 실행 중 우회 (문법 검증 전용 — bin 잠금 회피). MSB3021 류 복사 오류는 게이트 아님
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" \
  WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal \
  -p:OutputPath=C:/Temp/dxm-build/ 2>&1 | tee /tmp/dxm-build2.log | tail -20
grep -c ": error CS" /tmp/dxm-build2.log          # 0 이어야 함

# 2) 폴백 탐색 루프 존재 — Shot 목록 재순회가 1건 늘었다
grep -c 'recipeManager.ShotCount' /tmp/dxm-grab.txt   # 2 (baseline 1)

# 3) 폴백 Trace 로그 존재 + 어느 Shot / 어느 카메라인지 기록 (조용한 카메라 대체 금지)
grep -c 'ELogType.Trace' /tmp/dxm-grab.txt            # 2 (baseline 1 = 기존 tact 로그)
grep -c 'ShotName' /tmp/dxm-grab.txt                  # >=1 (baseline 0)
grep -c 'DeviceName' /tmp/dxm-grab.txt                # >=1 (baseline 0)

# 4) 우선순위 불변 — 1순위 소유자 필터가 폴백 루프보다 먼저 온다 (역전 금지)
nFilter=$(grep -n 'activeSeq) continue' /tmp/dxm-grab.txt | head -1 | cut -d: -f1)
nFallback=$(grep -n 'recipeManager.ShotCount' /tmp/dxm-grab.txt | tail -1 | cut -d: -f1)
echo "filter=$nFilter fallback=$nFallback"
if [ "$nFilter" -lt "$nFallback" ]; then echo ORDER-OK; else echo ORDER-FAIL; fi   # ORDER-OK

# 5) Task 1 계수 게이트 불변 재확인 (재계산 결과 둘 다 변하지 않아야 한다)
grep -c 'activeSeq) continue' $MV                     # 1 (0 이면 1순위 필터를 지운 것)
grep -coF 'ResolveActiveSequenceForCalibration' $MV   # 2 (정의 1 + 라이브 grab 호출 1)

# 6) 활성 시퀀스 결정 로직 불변 — 반환 분기 2개 유지 (우선순위 로직 변경 금지)
sed -n '/private string ResolveActiveSequenceForCalibration() {/,/^        }$/p' $MV \
  | grep -vE '^\s*//' | grep -c 'return'              # 2

# 7) 실패 경로 보존 — Shot 0건이면 여전히 null (실패 경로 추가/삭제 0)
grep -c 'return null' /tmp/dxm-grab.txt               # 4 (baseline 4)

# 8) diff 범위 — 여전히 두 파일, 금지 파일 미변경
git diff --name-only | grep -cE 'DatumMeasurement.csproj|CheckerboardCalibrationService|BottomVisionView|TrayVisionView'   # 0
git diff --name-only -- WPF_Example | wc -l           # 2

# 9) 가독류 게이트 — diff 추가 라인 전체(Task 1 + Task 2 누적) 대상
CW=WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs
D=$(git diff -U0 -- $MV $CW | grep '^+' | grep -v '^+++')
printf '%s\n' "$D" | grep -vE '^\+\s*//' | grep -cE '\?[^?]*:'   # 삼항: 0
printf '%s\n' "$D" | grep -vE '^\+\s*//' | grep -cF '??'          # null 병합: 0
printf '%s\n' "$D" | grep -vE '^\+\s*//' | grep -cF '?.'          # null 조건: 0
printf '%s\n' "$D" | grep -vE '^\+\s*//' | grep -cE 'switch.*=>'  # switch 식: 0
printf '%s\n' "$D" | grep -cF 'hbk'                               # 날짜·이니셜 주석: 0
```
    </automated>
  </verify>
  <done>
빌드 CS 오류 0. 게이트 2~9 전부 기대값 일치: `GrabCalibrationImage` 안에 Shot 재순회 폴백 루프가
1건 생기고(총 2건), 폴백 Trace 로그가 Shot 이름과 카메라 이름을 함께 남긴다. 1순위 소유자 필터는
폴백보다 앞에 1건 살아 있고(ORDER-OK), `ResolveActiveSequenceForCalibration` 의 반환 분기 2개와
메서드 참조 계수 2는 불변이다. 실패 경로(`return null`) 개수도 4로 불변이라 Shot 0건일 때의 기존
실패 안내가 유지된다. diff 는 여전히 정확히 두 파일이다.
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 3: 실기 UAT — 라이브 촬상 복구 + 1회 일괄 적용 + 자릿수 + 2점 Calibrate 회귀</name>
  <what-built>
체커보드 캘리브 [적용] 이 활성 시퀀스가 아니라 레시피 전체 Shot 의 `PixelResolution` 을 한 번에
덮도록 필터를 제거했다. 되돌리기 어려운 덮어쓰기이므로 확인창에 적용 범위(TOP / BOTTOM 포함)와
다른 PC 로 레시피를 복사할 때의 위험을 명시했다. mm/px 표시는 산출 리포트·확인창·결과창·2점
Calibrate 네 경로 모두 F8(유효숫자 6자리)로 통일했다. 계산·저장 정밀도는 무변경이다.

또한 [라이브 촬상] 이 항상 실패하던 버그를 고쳤다. 원인은 "활성 시퀀스" 를 측정 결과표의 선택 행으로
추정하는 구조였다 — 검사를 한 번도 돌리지 않으면 결과표가 비어 TOP 으로 추정되고, 이 PC 에는 TOP 소유
Shot 이 없어 카메라에 도달하기 전에 실패했다(Camera 로그에 grab 시도조차 안 찍혔던 이유). 이제 소유
Shot 탐색이 실패하면 첫 Shot 의 카메라로 폴백하고, 폴백이 발동하면 어느 Shot / 어느 카메라를 썼는지
Trace 로그에 남긴다. 기존 1순위 탐색은 그대로 먼저 시도하므로 TOP / BOTTOM PC 동작은 변하지 않는다.
  </what-built>
  <how-to-verify>
실기에서 아래 순서를 그대로 밟아 주세요.

**1. 적용 전 현재값 기록 (되돌릴 수 있게)**
   - 적용 전에 `D:\Data\Recipe\FAI_1` 폴더를 다른 이름으로 복사해 백업해 주세요.
   - TOP / BOTTOM / SIDE_1 / SIDE_4 의 `PixelResolution` 현재값을 적어 둡니다
     (플래너 실측: TOP 0.00240297321001669 / BOTTOM 0.00265 / SIDE_1 0.0023696 / SIDE_4 0.0023923444976).

**2. [라이브 촬상] 복구 — 검사를 한 번도 돌리지 않은 상태에서 (이번 버그 수정의 핵심)**
   - **앱을 새로 기동** 하고, 검사를 **한 번도 돌리지 않은 채** (측정 결과표가 빈 상태 그대로)
     체커보드 캘리브 창을 엽니다. 결과표에서 아무 행도 클릭하지 마세요 — 이 상태가 버그 재현 조건입니다.
   - [라이브 촬상] 을 누릅니다. **이미지가 정상으로 들어오면 성공입니다.**
     `"라이브 촬상 실패."` 가 뜨면 실패입니다(수정 전 증상 그대로).
   - Trace 로그를 열어 **폴백이 발동한 기록** 이 남았는지 확인해 주세요. 어느 Shot 이름과 어느 카메라를
     썼는지 적혀 있어야 합니다. 이미지는 들어왔는데 로그가 없으면 조용한 카메라 대체이므로 실패입니다.
   - Camera 로그에 grab 시도가 찍히는지도 같이 봐 주세요 (수정 전에는 여기까지 도달하지 못했습니다).

**3. 1순위 경로 무회귀 — 검사를 돌린 뒤의 라이브 촬상**
   - SIDE 검사를 1사이클 돌려 측정 결과표를 채운 뒤, 결과표에서 **행 하나를 선택** 합니다.
   - 캘리브 창에서 [라이브 촬상] 을 다시 누릅니다. 이미지가 정상으로 들어오는지 확인합니다.
   - 이번에는 Trace 로그에 **폴백 기록이 남지 않아야** 합니다. 선택 행으로 소유 Shot 을 정상적으로
     찾았다는 뜻이고, TOP / BOTTOM PC 에서 쓰이는 기존 경로가 살아 있다는 증거입니다.
     여기서도 폴백 로그가 뜬다면 우선순위가 뒤집힌 것이므로 보고해 주세요.
   - 이 사이클의 측정 mm 값 몇 개를 적어 두세요 — 8번(측정 회귀)에서 적용 전/후 비교 기준이 됩니다.

**4. 산출 리포트 자릿수**
   - 체커보드 캘리브 창에서 촬상 → 산출을 실행합니다.
   - 리포트 첫 줄 `1 px = ... mm (X ... / Y ...)` 이 `0.00240297` 처럼 **소수 8자리**로 보이는지
     확인합니다. `0.00240` 으로 잘려 보이면 실패입니다.

**5. 확인창 경고 (가장 중요)**
   - [적용] 을 누릅니다. 확인창에 아래 4가지가 모두 보이는지 확인해 주세요:
     (a) 활성 시퀀스가 아니라 **레시피 전체 SHOT** 이 대상이라는 문구
     (b) 이 PC 가 쓰지 않는 **TOP / BOTTOM 샷도 포함**된다는 표기
     (c) TOP / BOTTOM 의 배율값이 SIDE 값으로 덮인다는 경고
     (d) 이 레시피를 **다른 PC 로 복사해 쓰면 그쪽 측정이 틀어진다**는 경고
   - 배율값도 소수 8자리로 보이는지 같이 확인합니다.
   - 여기서 일단 **취소** 를 눌러, 취소 시 아무것도 바뀌지 않는지(값 유지) 확인해 주세요.

**6. 1회 일괄 적용**
   - 다시 [적용] → 확인을 누릅니다.
   - 결과창의 적용 개수가 레시피 전체 Shot 수와 맞는지, 배율이 8자리로 보이는지 확인합니다.
   - **시퀀스를 전환하지 않은 채로** SIDE_1 / SIDE_2 / SIDE_3 / SIDE_4 의 모든 Shot
     `PixelResolution` 이 같은 값이 됐는지 확인합니다. (이게 이번 작업의 핵심입니다 — 4번 적용이
     아니라 1번으로 끝나야 합니다.)
   - TOP / BOTTOM Shot 도 같은 값으로 덮였는지 확인합니다(의도된 동작입니다).

**7. 영속성**
   - 앱을 완전히 종료하고 재기동 → 레시피를 다시 로드해 전체 Shot 값이 유지되는지 확인합니다.
   - 저장된 값이 `0.0024` 처럼 잘려 저장되지 않았는지(INI 파일의 자릿수가 종전과 같은 원본 정밀도인지)
     확인해 주세요 — 표시만 바뀌어야 하고 저장값은 `double` 원본이어야 합니다.

**8. 측정 회귀**
   - SIDE 검사를 1사이클 돌려 측정 mm 값이 **3번에서 적어 둔 적용 전 값** 과 같은 수준인지 확인합니다
     (같은 배율을 적용했다면 SIDE 측정값은 변하지 않거나 아주 미세하게만 달라야 합니다).

**9. 2점 Calibrate 회귀 (별개 기능)**
   - 2점 Calibrate 를 실행해 두 점 클릭 → 실제 거리 입력 → 적용까지 해 봅니다.
   - 적용 안내 문구의 `1px = ... mm` 이 8자리로 보이는지 확인합니다.
   - "레시피 파일에는 저장되지 않았습니다" 경고가 종전대로 뜨는지 확인합니다.
   - 삼각형 오버레이의 거리(mm) 표시는 종전 자릿수 그대로인지 확인합니다(여기는 안 바꿨습니다).

**10. 정렬 비전 무영향**
   - Bottom / Tray 정렬 비전의 체커보드 캘리브(이더넷 카메라)가 종전대로 동작하는지 한 번 확인합니다.

문제가 있으면 1번에서 백업한 레시피 폴더로 되돌릴 수 있습니다.
  </how-to-verify>
  <resume-signal>"approved" 또는 발견한 문제를 설명해 주세요</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| 캘리브 창 [적용] 버튼(사용자 1클릭) → 레시피 전체 Shot 인메모리 모델 + INI 파일 | 단일 클릭이 레시피 전 범위의 측정 기준값을 되돌리기 어렵게 덮어쓴다 |
| 이 PC 의 레시피 파일 → 다른 PC (파일 복사) | 한 PC 에서 무해한 덮어쓰기가 복사 경로를 타고 다른 PC 의 측정 정확도를 깨뜨린다 |
| 캘리브 창 [라이브 촬상] → 어느 물리 카메라를 잡을지 결정 | 카메라 선택이 UI 선택 상태(측정 결과표)에 암묵적으로 의존한다. 추정이 빗나간 채 조용히 다른 카메라를 잡으면 그 이미지로 산출한 배율이 전 Shot 에 적용된다 |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-DXM-01 | Tampering | 필터 제거로 TOP / BOTTOM Shot 배율이 SIDE 값으로 덮임 | high | mitigate | 확인창에 적용 범위(전체 SHOT) + TOP / BOTTOM 포함 + 다른 PC 복사 위험을 명시. 게이트 #7 이 4개 토큰 존재를 자동 확인. 조용한 덮어쓰기 금지 |
| T-DXM-02 | Tampering | 라이브 촬상 카메라 선택 필터까지 함께 지워질 위험 | high | mitigate | `GrabCalibrationImage` 의 필터 보존을 게이트 #3 으로 계수 검증(비교 continue 1회 / 메서드 참조 2회) |
| T-DXM-03 | Tampering | 쓰기 3종(단일소스 + INI 호환) 중 일부 누락 | high | mitigate | 루프 본문 쓰기 3종 보존을 게이트 #8 로 계수 검증 |
| T-DXM-04 | Information Disclosure | 표시 자릿수 변경이 저장·계산 정밀도 변경으로 오인/오작동 | medium | mitigate | `CheckerboardCalibrationService` 무변경을 게이트 #6 으로 강제. UAT 5번에서 INI 저장값 원본 정밀도 확인 |
| T-DXM-05 | Tampering | 같은 "체커보드" 이름의 이더넷 정렬 캘리브 오수정 | medium | mitigate | `BottomVisionView` / `TrayVisionView` 미변경을 게이트 #6 으로 강제 + UAT 8번 육안 확인 |
| T-DXM-06 | Denial of Service | 형식 문자열 인자 개수 변경 시 `FormatException` 으로 확인창 크래시 | medium | mitigate | 확인창 자리표시자를 0,1 로 재번호 지정(action (B) 명시) + UAT 3번이 실제 확인창 표시를 직접 확인 |
| T-DXM-07 | Repudiation | 적용 전 값 소실(되돌릴 근거 없음) | low | accept | UAT 1번에서 레시피 폴더 백업 + 현재값 기록을 선행 절차로 요구. 앱 내 되돌리기 기능 신설은 범위 밖 |
| T-DXM-08 | Tampering | 빌드 시 앱 실행 중이면 산출물 잠금/부분 교체 | low | mitigate | precondition 으로 실행 여부 확인 + 강제 종료 금지. 필요 시 별도 OutputPath 로 문법 검증만 수행하고 SUMMARY 에 명시 |
| T-DXM-09 | Tampering | 폴백이 기존 우선순위를 뒤집어 TOP / BOTTOM PC 가 엉뚱한 카메라로 grab → 그 이미지로 산출한 배율이 전 Shot 에 적용된다 | high | mitigate | 1순위 필터 루프를 한 글자도 수정하지 않고 **뒤에만** 폴백 블록을 덧붙인다(구조적 역전 불가). Task 2 게이트 #4 가 소스 라인 순서로 필터 → 폴백 순서를 검증, #5/#6 이 결정 로직 불변(계수 1 / 2 / 반환 분기 2)을 검증. UAT 3번이 실기에서 "결과표 행 선택 시 폴백 로그 없음" 을 직접 확인 |
| T-DXM-10 | Repudiation | 폴백이 조용히 다른 카메라를 써서 사후 원인 추적 불가 | medium | mitigate | 폴백 발동 시 Trace 로그에 사용한 Shot 이름 + DeviceName 기록. Task 2 게이트 #3 이 로그 존재와 두 필드 포함을 검증. UAT 2번이 실기 로그를 직접 확인 |
| T-DXM-11 | Denial of Service | 폴백 추가 중 최종 null 가드를 빼면 Shot 0건 레시피에서 역참조 예외가 난다 | medium | mitigate | action (B)4 에 최종 가드를 필수로 명시 + 게이트 #7 이 실패 경로 계수 불변(4)을 검증. 메서드 전체를 감싼 catch 는 그대로 유지(throw 금지 규약) |
</threat_model>

<verification>
- 빌드 CS 오류 0 (`CS0618` / `CS0169` 경고는 baseline, 게이트 아님)
- Task 1 의 자동 게이트 9종 전부 통과 — 특히 #3(라이브 촬상 필터 보존)과 #7(경고 토큰 4종)
- Task 2 의 자동 게이트 9종 전부 통과 — 특히 #4(필터 → 폴백 순서, 우선순위 역전 금지)와
  #3(폴백 Trace 로그에 Shot 이름 + DeviceName 포함), #5/#6(Task 1 계수 게이트와 결정 로직 불변)
- 실기 UAT 10단계 전부 통과 — 특히 2번(결과표 빈 상태 라이브 촬상 복구 + 폴백 로그),
  3번(결과표 선택 시 폴백 로그 없음 = 1순위 경로 생존), 5번(확인창 경고),
  6번(시퀀스 전환 없이 1회로 SIDE_1~4 전부 동일값)
</verification>

<success_criteria>
- 체커보드 캘리브 [적용] 1회로 SIDE_1~4 의 모든 Shot `PixelResolution` 이 같은 값이 된다 (시퀀스 전환 불필요)
- 확인창에 전체 SHOT 대상임 + TOP / BOTTOM 포함 덮어쓰기 + 다른 PC 복사 위험이 모두 보인다
- 산출 리포트 / 확인창 / 결과창 / 2점 Calibrate 의 mm/px 가 전부 소수 8자리(유효숫자 6자리)로 보인다
- mm 거리 표시(F3 6곳)와 픽셀 거리 표시는 자릿수 회귀 0
- 2점 Calibrate 기능이 회귀 없이 동작한다 (미저장 경고 포함)
- 저장된 `PixelResolution` 은 `double` 원본 정밀도를 유지한다 (표시만 변경)
- **검사를 한 번도 돌리지 않은 상태(결과표가 빈 상태)에서 [라이브 촬상] 이 이미지를 정상으로 가져온다**
- 폴백이 발동하면 Trace 로그에 사용한 Shot 이름과 카메라(DeviceName) 가 남는다 (조용한 대체 0)
- 결과표에서 행을 선택한 상태에서는 폴백이 발동하지 않는다 (1순위 경로 생존 = TOP / BOTTOM PC 무회귀)
- 레시피 Shot 이 0건이면 종전대로 실패 안내가 뜬다 (예외 경로로 빠지지 않는다)
- 이더넷 정렬 카메라 캘리브 무영향, 신규 `.cs` 0개, 신규 헬퍼 메서드 0개, `csproj` 무변경
</success_criteria>

<output>
`.planning/quick/260910-dxm-shot-mm-px/260910-dxm-SUMMARY.md` 작성.
SUMMARY 에 반드시 포함할 것:
1. **자릿수를 F8 로 정한 근거** — 실측값 비교표(F4/F5/F8/F10 × 0.00240297321001669 / 0.00265)와
   "유효숫자 6자리 최소 충족 + 후행 0 과다 회피" 판단
2. **상수(const)를 만들지 않은 근거** — 두 파일이 다른 클래스이고 신규 `.cs` 금지라 공유 const 불가,
   파일별 const 는 표시 계약의 단일 소스를 쪼개므로 더 나쁘다는 판단
3. **2점 Calibrate 의 F4 까지 함께 고친 근거** — 같은 물리량(mm/px)이고 0.0024 대역에서 유효숫자
   2자리로 더 심하게 잘렸음. 반대로 mm 거리 표시(F3)는 왜 손대지 않았는지도 함께
4. **전체 Shot 적용의 알려진 부작용** — TOP / BOTTOM 배율이 SIDE 값으로 덮인다. 이 PC(SIDE 전용)
   에서는 무해하지만 레시피를 다른 PC 로 복사해 쓰면 그쪽 측정이 틀어진다. 되돌리기는 레시피 백업뿐
5. **`GrabCalibrationImage` 의 필터를 남긴 이유** — 라이브 촬상 카메라 선택용이라 목적이 다르다.
   같은 메서드의 두 소비처가 이번에 정반대로 갈린 이유(적용 범위 결정자 역할 상실 / 카메라 선택 힌트
   역할 유지)도 함께
6. 빌드를 별도 OutputPath 로 우회했는지 여부 (앱 실행 중이었다면 그 사실과 함께)
7. 확인창 개수 미표시 결정 — 적용 대상 Shot 수를 확인창에 미리 보여주려면 레시피 조회를 확인 모달
   앞으로 옮겨야 하는데 그것은 새 로직이라 범위 밖으로 뒀다
8. **[라이브 촬상] 실패의 원인과 고친 방식** — "활성 시퀀스" 를 측정 결과표 선택 행으로 추정하는
   구조 + 못 찾으면 기본값 폴백 → SIDE 전용 PC 에 해당 소유 Shot 이 없어 grab 전에 null 반환.
   추정 로직은 그대로 두고 **탐색 실패 시 첫 Shot 폴백** 만 추가했다는 점(우선순위 불변)
9. **폴백을 별도 루프로 둔 근거** — 기존 필터 루프 안에 변수를 끼워 넣는 쪽이 줄은 적지만 정상 경로
   본문을 수정하게 된다. 루프를 안 건드리고 뒤에 붙이면 TOP / BOTTOM PC 회귀 위험이 구조적으로 0
10. **임시 이미지 파일명을 바꾸지 않은 근거** — 폴백 시 파일명의 시퀀스명과 실제 카메라가 어긋날 수
    있으나 Trace 로그가 진실을 남기고, 파일명까지 맞추려면 추적 변수가 하나 더 필요해 "버그 수정
    최소 추가" 범위를 넘는다
11. **게이트 계수 재계산 결과** — 폴백 추가에도 `activeSeq) continue` 1건 / 메서드 참조 2건이 불변인
    이유(폴백은 소유자 비교도 메서드 재호출도 하지 않는다)
</output>
