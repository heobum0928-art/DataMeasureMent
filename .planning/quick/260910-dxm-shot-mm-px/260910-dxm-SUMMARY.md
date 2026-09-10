---
phase: quick-260910-dxm
plan: 01
subsystem: ui
tags: [halcon, checkerboard-calibration, wpf, mvvm-partial]

requires: []
provides:
  - "체커보드 캘리브 [적용] 1회로 레시피 전체 Shot 의 PixelResolution 을 일괄 반영 (시퀀스 전환 불필요)"
  - "mm/px 표시 4경로(체커보드 리포트/확인창/결과창, 2점 Calibrate) F8(유효숫자 6자리) 통일"
  - "GrabCalibrationImage 라이브 촬상 실패 버그 수정 — 소유 Shot 미발견 시 첫 Shot 카메라 폴백 + Trace 로그"
affects: [checkerboard-calibration, calibration-window, side-pc-single-camera]

tech-stack:
  added: []
  patterns:
    - "형식 문자열 표시 지정자는 계산 로직과 분리된 '표시 계약'으로 취급 — 상수화하지 않고 파일별 리터럴 유지"
    - "1순위 정상 경로 루프는 한 글자도 수정하지 않고, 실패 시 뒤에 별도 폴백 블록만 추가(구조적 회귀 방지)"

key-files:
  created: []
  modified:
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs

key-decisions:
  - "자릿수 F8 채택 — F4(2자리)/F5(3자리)는 유효숫자 부족, F8(6자리)이 요구 충족 + 후행 0 과다 방지의 균형점, F10 은 노이즈"
  - "표시 자릿수 상수(const) 미신설 — 두 클래스가 달라 공유하려면 신규 .cs 필요(금지), 파일별 const 는 표시 계약을 둘로 쪼개 더 나쁨"
  - "2점 Calibrate 의 F4 도 F8 로 함께 수정 — 같은 물리량(mm/px)이고 더 심하게 잘림. mm 거리 표시(F3)는 별개 물리량이라 무변경"
  - "확인창에 적용 대상 Shot 개수 미표시 — 레시피 조회를 확인 모달 앞으로 당겨야 하는 신규 로직이라 범위 밖으로 결정"
  - "GrabCalibrationImage 의 소유자 필터는 보존 — ApplyCheckerboardCalibration 과 목적이 달라(카메라 선택 힌트) 정반대로 취급"
  - "폴백을 별도 루프로 분리 — 1순위 루프 본문을 한 글자도 안 건드려야 TOP/BOTTOM PC 우선순위 역전이 구조적으로 불가능"
  - "임시 이미지 파일명은 그대로(추정 시퀀스명 사용) — 폴백 시 어긋날 수 있으나 Trace 로그가 실사용 Shot/카메라를 정확히 남김"

requirements-completed: [QUICK-260910-DXM]

coverage:
  - id: D1
    description: "체커보드 캘리브 [적용] 1회로 레시피 전체 Shot(SIDE_1~4 + TOP/BOTTOM)의 PixelResolution 이 동일 값으로 갱신됨"
    requirement: "QUICK-260910-DXM"
    verification:
      - kind: manual_procedural
        ref: "260910-dxm-PLAN.md Task 3 UAT 1,6,7,8"
        status: unknown
    human_judgment: true
    rationale: "실기 레시피(D:\\Data\\Recipe\\FAI_1)와 실물 카메라 대상 UAT — 자동화 불가, 앱 실행 자체가 이번 세션에서 금지됨"
  - id: D2
    description: "확인창에 전체 SHOT 대상 + TOP/BOTTOM 포함 + 다른 PC 복사 위험 경고 4항목이 모두 보임"
    requirement: "QUICK-260910-DXM"
    verification:
      - kind: unit
        ref: "Task 1 자동 게이트 #7 (전체 SHOT/TOP/BOTTOM/다른 PC 토큰 grep)"
        status: pass
      - kind: manual_procedural
        ref: "260910-dxm-PLAN.md Task 3 UAT 5"
        status: unknown
    human_judgment: true
    rationale: "토큰 존재는 자동 확인됐으나 실제 화면 렌더링(줄바꿈/가독성)은 육안 확인 필요"
  - id: D3
    description: "mm/px 표시 4경로(산출 리포트/확인창/결과창/2점 Calibrate)가 소수 8자리(유효숫자 6자리)로 보임, 저장 정밀도는 double 원본 그대로"
    requirement: "QUICK-260910-DXM"
    verification:
      - kind: unit
        ref: "Task 1 자동 게이트 #4 (:F8} 개수 MainView 4건 + CalibrationWindow 3건, 옛 :F4}/:F5} 소멸 확인)"
        status: pass
      - kind: manual_procedural
        ref: "260910-dxm-PLAN.md Task 3 UAT 4,5,7,9"
        status: unknown
    human_judgment: true
    rationale: "저장값이 표시만 바뀌고 원본 정밀도를 유지하는지는 INI 파일 실측 확인이 필요 — 자동화 게이트는 계산 로직 무변경만 보장"
  - id: D4
    description: "검사를 한 번도 돌리지 않아 결과표가 빈 상태에서도 [라이브 촬상]이 이미지를 반환 — 폴백 발동 시 Trace 로그에 Shot/카메라 기록"
    requirement: "QUICK-260910-DXM"
    verification:
      - kind: unit
        ref: "Task 2 자동 게이트 #2,#3,#4,#7 (폴백 루프 존재, Trace 로그 ShotName/DeviceName 포함, 1순위→폴백 순서, return null 계수 불변)"
        status: pass
      - kind: manual_procedural
        ref: "260910-dxm-PLAN.md Task 3 UAT 2,3"
        status: unknown
    human_judgment: true
    rationale: "실물 카메라 grab 성공 여부와 Trace 로그 실측은 실기 UAT 전용 — 이번 세션은 앱 실행 금지 규약으로 자동화 불가"

duration: 55min
completed: 2026-09-10
status: complete
---

# Quick 260910-dxm: 체커보드 캘리브 전체 Shot 일괄 적용 + mm/px 자릿수 확대 + 라이브 촬상 버그 수정 Summary

**체커보드 캘리브 적용 범위를 활성 시퀀스에서 레시피 전체 Shot 으로 확장하고, mm/px 표시를 F8(유효숫자 6자리)로 통일했으며, 검사 미실행 시 라이브 촬상이 항상 실패하던 버그를 소유 Shot 폴백으로 고쳤다.**

## Performance

- **Duration:** 약 55분
- **Started:** 2026-09-10 (코드 태스크 착수)
- **Completed:** 2026-09-10 (Task 1/2 커밋 완료, Task 3 체크포인트는 오케스트레이터 확인 대기)
- **Tasks:** 2/3 (Task 1, Task 2 완료·커밋. Task 3 은 실기 UAT 체크포인트로 이번 세션에서 앱 실행하지 않음)
- **Files modified:** 2 (MainView.xaml.cs, CalibrationWindow.xaml.cs)

## Accomplishments
- `ApplyCheckerboardCalibration` 의 활성 시퀀스 소유자 필터 제거 — 레시피 전체 Shot 의 `PixelResolution` 을 1회 적용으로 덮음 (SIDE_1~4 시퀀스 전환 4회 반복 불필요)
- 확인창에 "레시피 전체 SHOT (TOP/BOTTOM 포함)" 대상 범위 + "다른 PC 로 복사하면 그쪽 측정이 틀어짐" 경고 명시 (되돌리기 어려운 덮어쓰기이므로)
- mm/px 표시 4경로(체커보드 산출 리포트, 체커보드 확인창, 체커보드 결과창, 2점 Calibrate 확인창/안내)를 전부 F8(유효숫자 6자리)로 통일 — 기존 F4/F5(2~3자리)로 `0.00240297321001669` 이 `0.0024`/`0.00240` 로 잘려 보이던 문제 해결
- `GrabCalibrationImage` — 활성 시퀀스 추정(측정 결과표 선택 행 기반)이 검사 미실행 시 빗나가 SIDE 전용 PC 에서 카메라 도달 전에 null 반환하던 버그를, 1순위 필터 실패 시 레시피 첫 Shot 으로 폴백하는 방식으로 수정. 폴백 발동 시 Trace 로그에 사용한 Shot 이름 + DeviceName 기록

## Task Commits

Each task was committed atomically:

1. **Task 1: 전체 Shot 일괄 적용 + mm/px 자릿수 확대** - `ef373356` (feat)
2. **Task 2: [라이브 촬상] 실패 버그 수정 — 소유 Shot 미발견 시 첫 Shot 카메라 폴백** - `e92099f9` (fix)
3. **Task 3: 실기 UAT — 오케스트레이터 확인 대기 (앱 미실행, 코드 변경 없음)**

**Plan metadata:** 이 커밋(SUMMARY/STATE 기록)으로 뒤이어 기록됨

## Files Created/Modified
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` - `ApplyCheckerboardCalibration` 필터 제거+확인창/결과창 문구, 2점 Calibrate mm/px 표시 2곳 F8, `GrabCalibrationImage` 폴백 블록 추가, 낡은 주석 2곳 정정
- `WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs` - 산출 리포트 mm/px 표시 3개 필드 F8

## Decisions Made

**1. 자릿수를 F8 로 정한 근거 — 실측값 비교표**

| 지정자 | 0.00240297321001669 | 0.00265 (BOTTOM) | 유효숫자 | 판정 |
|--------|---------------------|------------------|----------|------|
| F4 (2점 Calibrate 기존) | 0.0024 | 0.0027 | 2자리 | 부족 |
| F5 (체커보드 기존) | 0.00240 | 0.00265 | 3자리 | 부족 |
| **F8 (채택)** | **0.00240297** | **0.00265000** | **6자리** | 요구 충족 + 후행 0 4개로 허용 범위 |
| F10 | 0.0024029732 | 0.0026500000 | 8자리 | 후행 0 6개 — 노이즈, 가독성 저하 |

F8 을 전 표시 지점(산출 리포트/확인창/결과창/2점 Calibrate 2곳) 동일 적용 — 유효숫자 최소 6자리 요구를 0.002x 대역에서 정확히 충족하는 최소 자릿수이면서 후행 0 이 과하게 붙지 않는 균형점.

**2. 상수(const)를 만들지 않은 근거**

(a) `MainView`와 `CalibrationWindow`는 서로 다른 클래스라 공유 const 를 두려면 신규 `.cs` 파일이 필요한데 이번 작업은 신규 파일 생성 금지 규약. (b) 파일별로 각각 const 를 두면 같은 "mm/px 표시 계약"의 단일 소스가 둘로 쪼개져 오히려 나빠짐. (c) 두 파일 모두 복합 형식 문자열 안의 지정자를 전부 리터럴로 쓰는 기존 관례(F1/F2/F3 다수)를 갖고 있어 한 곳만 상수화하면 일관성이 깨짐. 매직넘버 금지 규칙은 계산에 쓰이는 수치 상수를 겨냥한 것이고, 복합 형식 문자열의 표시 지정자는 그 대상이 아니라고 판단.

**3. 2점 Calibrate 의 F4 까지 함께 고친 근거**

체커보드 캘리브와 동일한 물리량(mm/px)을 보여주는 지점이고, 기존 F4 는 0.0024 대역에서 유효숫자 2자리로 체커보드 쪽(F5, 3자리)보다 더 심하게 잘렸다. 같은 창의 mm 거리 표시(총거리/가로/세로, F3 지정자 6곳)는 배율이 아니라 mm 환산 거리값(0.x~수십 mm 범위)이라 별개 물리량이며, F3(μm 단위까지)로 이미 충분해 건드리지 않음. 픽셀 거리 표시(F1)도 무관해 무변경.

**4. 전체 Shot 적용의 알려진 부작용**

TOP / BOTTOM 배율이 SIDE 값으로 덮인다. 이 PC(SIDE 전용)에서는 무해하지만, 이 레시피를 다른 PC(TOP/BOTTOM 전용)로 복사해 쓰면 그쪽 측정이 틀어진다. 되돌리기는 레시피 파일 백업뿐(앱 내 되돌리기 기능은 범위 밖, UAT 1번에서 백업 절차 요구).

**5. `GrabCalibrationImage` 의 필터를 남긴 이유**

`ResolveActiveSequenceForCalibration` 은 이번 작업에서 두 소비처가 정반대로 갈렸다: `ApplyCheckerboardCalibration` 은 "적용 대상 Shot 선별" 역할을 완전히 잃고 의존을 제거했지만(전체 Shot 일괄 적용이 되면 "활성 시퀀스" 개념 자체가 무의미), `GrabCalibrationImage` 는 "라이브 촬상할 카메라 선택" 역할이 여전히 필요해 필터를 그대로 유지 + 실패 시 폴백만 추가했다. 즉 이 메서드는 "적용 범위 결정자" 역할을 잃고 "카메라 선택 힌트" 역할만 남았다.

**6. 빌드 우회 여부**

이번 세션에서는 앱이 처음부터 실행 중이 아니었음(`tasklist` 로 확인, `app not running`). 두 Task 모두 표준 `-p:Configuration=Debug -p:Platform=x64` 빌드로 진행했고, `-p:OutputPath` 우회는 사용하지 않았다. 두 빌드 모두 CS 오류 0, 경고는 baseline(CS0618 8건 + CS0169 1건)만 존재.

**7. 확인창 개수 미표시 결정**

적용 대상 Shot 수를 확인창에 미리 보여주려면 레시피 조회(`recipeManager`)를 확인 모달 앞으로 끌어올려야 하는데, 이는 기존 흐름("확인창 → 레시피 매니저 조회 → 적용")을 바꾸는 새 로직이다. `MainView.xaml.cs` 에 새 로직 추가를 최소화하라는 CLAUDE.md 규칙에 따라 순서를 바꾸지 않았다 — 경고는 개수 없이 "전체 SHOT (TOP/BOTTOM 포함)" 범위 명시로 대체.

**8. [라이브 촬상] 실패의 원인과 고친 방식**

원인 사슬: `ResolveActiveSequenceForCalibration` 이 "활성 시퀀스"를 측정 결과표(`dataGrid_faiResults`) 선택 행에서 역추적한다 → 검사를 한 번도 안 돌리거나 아무 행도 선택하지 않으면 사슬이 끊겨 기본값(`SEQ_TOP`)으로 폴백 → `GrabCalibrationImage` 의 소유자 필터가 그 시퀀스(TOP) 소유 Shot 을 찾는데, SIDE 전용 PC 에는 TOP 소유 Shot 이 0건 → `camShot` 이 끝까지 비어 카메라 grab 호출 전에 즉시 `null` 반환. 수정 방식: 추정 로직(`ResolveActiveSequenceForCalibration`)은 전혀 건드리지 않고, `GrabCalibrationImage` 의 필터 루프 직후 "못 찾았을 때"의 뒤처리만 추가 — 1순위 탐색이 실패했을 때만 레시피의 null 이 아닌 첫 Shot 으로 폴백(비교 없이 첫 것을 집는 것이 폴백의 정의). 이 장비는 물리 카메라가 1대라 어느 Shot 을 집어도 결국 같은 카메라.

**9. 폴백을 별도 루프로 둔 근거**

기존 필터 루프 안에 "첫 Shot 기억" 변수를 끼워 넣는 쪽이 줄 수는 적지만, 그러면 정상 경로의 루프 본문을 수정하게 된다. 루프를 한 글자도 안 건드리고 뒤에 블록만 덧붙이면 TOP/BOTTOM PC 경로의 회귀 위험이 구조적으로 0 이고, 우선순위 역전도 물리적으로 불가능하다(1순위 필터가 먼저 코드에 등장하고, 폴백은 필터가 실패했을 때만 실행되는 `if (camShot == null)` 블록 안에 있음). 자동 게이트 #4(order check)로 소스 라인 순서까지 확인.

**10. 임시 이미지 파일명을 바꾸지 않은 근거**

`SaveTempImage("Calibration_" + activeSeq, ...)` 는 그대로 둔다. 폴백 시 실제 사용 카메라와 파일명의 시퀀스명이 어긋날 수 있지만, (a) 무엇을 실제로 썼는지는 Trace 로그(Shot 이름 + DeviceName)가 정확히 남기고, (b) 파일명까지 맞추려면 실제 사용 Shot 을 추적하는 변수가 하나 더 필요해 "버그 수정 최소 추가" 범위를 넘는다.

**11. 게이트 계수 재계산 결과**

Task 2 의 폴백 코드를 추가한 뒤에도 `activeSeq) continue` 는 파일 전체 1건, `ResolveActiveSequenceForCalibration` 참조는 2건(정의 1 + `GrabCalibrationImage` 호출 1) 그대로 불변임을 확인. 이유: 폴백 블록은 소유 시퀀스 비교(`owner != activeSeq`)를 전혀 수행하지 않고(비교 없이 첫 Shot 을 집는 것이 폴백의 정의), `ResolveActiveSequenceForCalibration` 메서드도 다시 호출하지 않기 때문이다.

## Deviations from Plan

None - 플랜이 지정한 라인번호·삭제 범위·형식 문자열 구조를 그대로 따라 실행했다. 편집 전 재확인한 실측 라인번호(3230 대, 3411, 3430, 3455~3459, 3473, 3494~3542)가 플랜의 사전 조사 결과와 정확히 일치했다.

**Task 2 게이트 #8(diff 범위 2개 파일 기대) 관련 주석:** 플랜의 자동 게이트 #8 은 두 Task 가 모두 미커밋 상태라고 가정해 `git diff --name-only -- WPF_Example | wc -l` 이 2 를 기대했으나, 본 실행자는 Task 마다 원자적 커밋을 하는 GSD 프로토콜을 따르므로 Task 2 실행 시점에는 Task 1 의 `CalibrationWindow.xaml.cs` 변경이 이미 커밋되어 `git diff` 에 잡히지 않아 실측값이 1 로 나왔다. 게이트의 실제 의도("금지 파일 미변경 + 변경 파일이 의도한 두 파일로 국한")는 Task 1 실행 시점의 게이트 #6(2 확인 완료) + Task 2 게이트 #8 의 금지 파일 미변경(0) 조합으로 동일하게 충족되므로 버그 수정이나 범위 이탈이 아니다.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness

**Task 3(실기 UAT, gate="blocking")이 오케스트레이터 확인 대기 상태다.** 코드 변경은 전부 완료·커밋·빌드 검증(CS 오류 0)됐으나, 실물 카메라와 `D:\Data\Recipe\FAI_1` 레시피를 사용하는 10단계 UAT(라이브 촬상 복구, 1회 일괄 적용, 자릿수, 확인창 경고, 영속성, 측정 회귀, 2점 Calibrate 회귀, 정렬 비전 무영향)는 이번 세션에서 앱을 실행하지 않았으므로 수행되지 않았다. 절차는 `260910-dxm-PLAN.md` Task 3 `<how-to-verify>` 참고. UAT 승인("approved") 또는 발견 문제 보고 전까지 이 quick 작업은 미완료 상태로 남는다.

---
*Phase: quick-260910-dxm*
*Completed: 2026-09-10 (코드 태스크만; 실기 UAT 대기)*

---

## 사후 정정 — 확인창 문구 (오케스트레이터, 커밋 b9b6199d)

실행자가 커밋한 뒤 **오케스트레이터가 확인창 문구를 바꿨다.** 실행자는 이를 "동시 수정"으로
감지하고 임의로 되돌리지 않은 채 보고했다 — 올바른 판단이었다. 아래가 그 경위와 결론이다.

**바뀐 이유:** 플랜이 요구한 원 문구는 "TOP/BOTTOM 도 덮인다 = 위험" 을 경고했다. 이는
**SIDE PC 만 보고 세운 반쪽 판단**이었다. 사용자가 이후 확인해 준 사실 — TOP/BOTTOM PC 는
두 시퀀스가 **한 카메라**를 공유한다 — 에 따르면, 그 PC 에서는 두 시퀀스가 함께 덮이는 것이
오히려 정상 동작이다. 원 문구를 그대로 두면 그 PC 에서 정상 동작을 위험으로 오독하게 된다.

**최종 문구 (HEAD 기준):**
```
이 레시피의 모든 SHOT 의 PixelResolution 을
1 px = {0:F8} mm 로 덮어씁니다.

[주의] 카메라가 여러 대인 장비라면 카메라마다 따로 잡아야 합니다.
이 적용은 시퀀스를 가리지 않고 레시피 안의 모든 SHOT 을 같은 배율로 덮습니다.
되돌리기 어려운 덮어쓰기입니다. 적용하시겠습니까?
```

**게이트 #7 / T-DXM-01 정정:** 위 SUMMARY 본문과 must_haves 의 "TOP/BOTTOM 포함 + 다른 PC
복사 위험 4항목" 기준은 **더 이상 유효하지 않다.** 실행자의 게이트 #7 PASS 기록도 커밋
`ef373356` 시점 기준이며 현재 HEAD 에는 해당하지 않는다. 완화의 **의도**(되돌리기 어려운
전체 덮어쓰기를 조용히 하지 않는다)는 유지된다 — 확인 모달과 "모든 SHOT" 범위 명시,
"카메라가 여러 대면 따로" 주의, "되돌리기 어려움" 경고가 그대로 남아 있다.

**UAT 5번(확인창 경고) 판정 기준을 위 최종 문구로 볼 것.** 옛 토큰(TOP/BOTTOM/다른 PC)을
찾으면 안 된다.

**빌드:** 문구 변경 후 Debug|x64 재빌드 CS 오류 0. `string.Format` 자리표시자 2개 ↔ 인자
2개(`mmPerPixel`, `warnLine`) 일치 육안 확인 — 여기가 어긋나면 컴파일은 되고 런타임에
FormatException 이 난다.
