---
quick_id: 260827-hdf
slug: phase-74-75-3
date: 2026-08-27
status: complete
---

# Quick 260827-hdf — Phase 74/75 계획 검증 결함 3건 반영 (완료)

Phase 74/75 계획 12개(6,496줄)를 코드베이스·HALCON 24.11 공식문서·실빌드와 대조한 검증에서
나온 결함 3건을 **계획 문서에만** 반영했다. `WPF_Example/` 소스는 한 줄도 바꾸지 않았다.

## 검증 과정에서 확정한 사실

- **빌드 기준선 실측**: `Debug|x64` clean rebuild → **에러 0 / 경고 18줄 (CS0618×16 + CS0162×2)**.
  계획이 적어둔 값과 일치. (자동메모리의 "12줄" 은 낡은 값이라 갱신함)
- HALCON `Difference`/`AreaCenter`/`CountObj`/`ReadRegion`/`WriteRegion` 시그니처 5종 — 어셈블리 리플렉션으로 일치 확인
- `write_region` 의 `.hobj`=HOBJ / `.reg`=legacy — HALCON 24.11 공식 문서 원문 확인
- `set_color` 의 `'#rrggbbaa'` 알파 지원 — 공식 문서 확인
- `new HImage(hobj)` 후 원본 `Dispose` 안전 — **실제 실행해서 확인**(자체 참조를 잡음)
- 계획들이 인용한 파일:줄 참조 40여 건 대부분 정확

## 반영한 결함

### B-1 (블로커) → `74-05-PLAN.md`

`canvasToolbar` Border 가 `MainView.xaml:144` 에서 `Height="36"` 고정이라, 계획대로 그 안에
`Grid.Row="1"` 로 브러시 패널을 넣으면 **Row 1 이 0px** 가 되어 패널이 안 보이거나 아래 HALCON
HWND 를 침범한다(피하려던 airspace 문제 재발). 계획은 `RowDefinitions` 부재까지만 확인하고
고정 높이를 놓쳤다.

반영 내용:
- `<interfaces>` 에 고정 높이 실측 + 영향 설명 추가
- Task 2 `<action>` 에 **단계 (0)** 신설 — `Height="36"` → `MinHeight="36"` (값 36 유지, `Grid.Row` 무변경)
- acceptance 3건 추가 — `Height="36"` 0건 / `MinHeight="36"` 1건 / `Height=` 삭제 줄이 정확히 1줄
- "절대 하지 말 것" 에 "기존 줄 수정은 이 한 곳뿐" 명시
- plan `<verification>` 6번 + must_have 문구 보강

### M-1 (중간) → `74-02-PLAN.md`

Task 3 acceptance 가 `grep -n "_isBrushStroking = false;"` **파일 전체 첫 줄 번호**를 기준으로
삼는데, Task 2 가 이미 `StartBrushMasking`/`StopBrushMasking`(파일 앞쪽)에 같은 문장을 넣으므로
**올바른 구현에서도 항상 실패**한다. 인수인계에 "숫자를 맞추려 코드를 지우는 사고 이력" 이 있어
위험도가 높았다.

반영 내용: `awk '/private void ViewerHost_HMouseUp/,/^        }$/'` 범위 내 **상대 줄 번호** 기준으로
교체 + 왜 파일 전체 grep 이 틀리는지 + "안 맞아도 코드 지우지 말 것" 경고 병기.

### M-2 (중간) → `75-01` / `75-03` / `75-06-PLAN.md`

`RunCorrectedRecheck` 는 `RunBottomAlign` 의 `finally` 에서 **동기** 실행 = PLC 응답 전에 돈다.
계획대로면 `TryFindPose` **4회**(1차 2 + 재매칭 2) + 전체이미지 `AffineTransImage` 1회이고,
검색 인자가 `FULL_SEARCH_LEN=99999`/`downsample=1.0` 전역 탐색이라 CONTEXT D-75-01 의
"매칭 1회 추가 = 수십 ms" 추정보다 비싸다.

**1차 검출 2회는 낭비다** — `Run()` 이 방금 같은 이미지에서 한 검출이 `AlignResult.HasDetection` /
`DetectedRow1`/`DetectedCol1`/`DetectedRow2`/`DetectedCol2`(실측 `AlignResult.cs:31~43`)에 있고,
호출부 `RecordAlignVerify` 가 그 `res` 를 쥐고 있다.

반영 내용:
- `75-01`: **오버로드 2개** 구조로 변경 — (A) 자체 검출판 = (B) 에 `bHasDetection:false` 위임,
  (B) 검출 재사용판이 본체. 5단계를 `bHasDetection` 분기로 재작성(false 면 기존 폴백).
  `Stopwatch` 로 **소요시간(ms)** 계측 + `[ALIGN_VERIFY]` 로그에 `reused=` / `elapsed=` 추가.
  acceptance 6건 추가/조정(`TryFindPose == 4` 는 유지하되 **폴백 분기 안에만 있을 것**을 강제).
- `75-03`: (B) 를 호출하도록 수정 + "(A) 쓰면 안 된다" 명시 + acceptance 1건 추가.
- `75-06`: U-6 를 "회귀가 없는가 (택트 포함)" 으로 확장 — `reused=True` 확인, `elapsed` 실측,
  **100ms 초과 시 보고**(`project_plc_rapid_retest_start_reject`: PLC 가 응답 22ms 뒤 다음 요청).
  U 항목 수는 6 유지(기존 acceptance grep 호환).

## 검증

| 항목 | 결과 |
|---|---|
| `WPF_Example/` 변경 | `DatumMeasurement.csproj` 1건뿐 — **세션 시작 시점부터 있던 것, 미접촉** |
| 74-05 `MinHeight="36"` | 4건 (설명 2 + action 1 + acceptance 1) |
| 74-02 옛 기준(`첫 줄 번호의 차이`) | **0건** (제거됨) |
| 74-02 새 기준(awk 범위) | 1건 |
| 75-01 오버로드 선언 | 2건 (+acceptance 1) |
| 75-01 `Stopwatch` | 3건 |
| 75-03 재사용판 호출 | 2건 (본문 1 + acceptance 1) |
| 75-06 `elapsed=` 확인 항목 | 1건, U-1~U-6 **6개 유지** |

빌드 검증 없음 — 마크다운만 변경했다.

## 남은 것 (반영하지 않음, 경미)

검증에서 같이 나왔으나 실행을 막지 않아 반영하지 않은 것:

- **L-1** `74-04-PLAN.md` 가 BottomVisionView 의 `ValidateRois`/`RectToTeachParams` 를 `:787`/`780~840줄`
  로 적었으나 실제는 **1217/1244**(Tray 쪽 줄번호가 복사됨). `grep` 으로 찾으면 되는 수준
- **L-2** 두 phase 빌드 명령 **24곳**이 이전 세션 스크래치패드 GUID(`7fe7f8e3-…`)를 하드코딩.
  해당 폴더가 아직 존재해 지금은 동작하나, 임시폴더가 정리되면 깨진다
- **L-3** `74-06-PLAN.md` (D)-3 의 "결과는 정확히 3곳" — `grep -rn` 은 줄 단위라 실제 7~8줄이 나온다
  (바로 아래 acceptance 는 "파일 3개 이하" 로 맞게 적혀 있음). 헛발동 가능
- **L-4** `74-03-PLAN.md` 가 `INotifyPropertyChanged` 를 직접 구현 — 같은 폴더에
  `UI/ViewModel/Observable.cs` 추상 베이스가 이미 있다. 동작엔 무해한 관례 이탈

## 착수 전 권고 (변함없음)

인수인계의 권고가 그대로 유효하다 — **Bottom Align 캘리브레이션을 먼저 한 번 돌려 노이즈 수준을
볼 것.** Phase 74 는 옵션이라 만들어 둬도 무해하지만, Phase 75 의 판정 임계값은 실측 산포 없이는
정할 수 없다(계획도 1차 배포는 임계 0 = 판정 없음으로 잡아 뒀다).
