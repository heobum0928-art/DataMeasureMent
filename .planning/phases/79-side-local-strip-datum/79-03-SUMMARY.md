---
phase: 79-side-local-strip-datum
plan: 03
subsystem: vision-measurement-ui
tags: [halcon, roi-wiring, mainview, overlay-color, csharp7.2]

requires:
  - phase: 79-01
    provides: "EdgeToLineDistanceMeasurement.LOCAL_REF_ROI_SUBKEY(\"LocalRef\")/LOCAL_REF_OVERLAY_ROI_ID(\"FAI-RefLine\"), LocalRef_Row/Col/Phi/Length1/Length2, IsLocalRefEnabled"
provides:
  - "MainView.xaml.cs ROI 배선 7함수(BuildPointRoiDefinitions/TryResolvePointRoiTarget/ApplyPointRoiMoveDelta/TryGetPointRoiCenter/ApplyPointRoiResize/ApplyPointRoiClear/TransformMeasurementGeometry) 에 국부 기준 ROI(subKey LocalRef) 분기 — 캔버스 표시·해석·이동·중심·크기·삭제·재앵커"
  - "HalconDisplayService.LOCAL_REF_LINE_COLOR(\"orange\")/LOCAL_REF_LINE_WIDTH(2) + FAI-RefLine 색 분기 — 화면에서 국부 기준선을 주황 2px 로 구분 표시"
  - "저장소 밖 probe: RoiRegressProbe.cs/.exe(신규, 79-03 편집 전 exe 기준 ROI 배선 616케이스 비트 비교), LocalRefProbe roidefs(11사례)·overlaycolor(4사례) 모드"
affects: [79-04-version-audit, 79-05-realab-uat]

tech-stack:
  added: []
  patterns:
    - "기존 DualImage/ArcLineIntersect 의 '측정 1개 안에 서브키로 구분되는 다중 ROI' 패턴을 그대로 재사용 — EdgeToLineDistance 의 Point/LocalRef 도 같은 7함수 안에서 subKey 로 분기"
    - "재앵커(TransformMeasurementGeometry)는 return 하지 않는 in-place 보조 블록 패턴 — 국부 기준 ROI 를 먼저 옮기고(가드: LocalRef_Length1/2>0), 이어서 기존 Point 처리 줄이 그대로 실행된다(단일 return 지점 유지)"
    - "리플렉션 회귀 probe: private 정적 메서드는 인수 없이, private 인스턴스 메서드는 FormatterServices.GetUninitializedObject 로 생성자·InitializeComponent 호출 없이 인스턴스화해 호출 — OffRegressProbe(79-01)와 같은 AssemblyResolve 골격을 그대로 재사용"

key-files:
  created: []
  modified:
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/Halcon/Display/HalconDisplayService.cs

key-decisions:
  - "Task 1 action (1): Point ROI 가 미티칭(Point_Length1/2<=0)이면 기존 :514 가드가 먼저 return 하므로 국부 기준 ROI 가 티칭되어 있어도 캔버스에 보이지 않는다 — 새 동작이 아니라 기존 가드를 그대로 둔 결과이며, probe roidefs_point_untaught 로 확인했다. 향후 '기준 ROI 만 먼저 티칭' UX 가 필요해지면 이 가드를 재검토해야 한다(이번 phase 범위 밖)"
  - "TransformMeasurementGeometry 삽입은 return 하지 않고 아래 기존 Point 처리 줄로 흘러가게 했다(단일 코드 경로) — 국부 기준 ROI 와 Point ROI 는 같은 강체 변환 T 로 동시에 옮겨지고, 미티칭(Length 0) 국부 기준 ROI 는 가드로 스킵되어 옛 레시피의 Row/Col/Phi=0 값이 그대로 유지된다"
  - "HalconDisplayService 색 분기는 FAI-DistLine(청록) 분기 바로 다음, 그 외(파랑) 분기 바로 앞에 삽입 — 분기 순서를 바꾸지 않아 다른 RoiId 의 색 판정에 영향이 없다(after_distline=1 로 확인)"
  - "OverlayCaptureRenderer.cs(저장 캡처)는 변경하지 않았다 — A-79-A7 그대로, 저장 사진의 국부 기준선은 여전히 '그 외' 파랑이다(capture_changed=0)"

requirements-completed: [LSR-01, LSR-04, LSR-05]

coverage:
  - id: D1
    description: "티칭된 기준 ROI(LocalRef_Length1/2>0)를 가진 EdgeToLineDistance 측정은 캔버스 ROI 목록에 두 번째 ROI(Id 끝 _LocalRef)가 생기고, Point ROI 는 기존과 비트 동일하다"
    requirement: "LSR-01"
    verification:
      - kind: integration
        ref: "LocalRefProbe.exe roidefs — case=roidefs_untaught_single, roidefs_taught_two, roidefs_point_untaught"
        status: pass
    human_judgment: false
  - id: D2
    description: "기준 ROI 를 끌기·중심 읽기·크기 바꾸기·삭제해도 기존 핸들러가 subKey LocalRef 로 해석해 LocalRef_* 만 바꾸고 Point_* 는 그대로다(삭제는 IsLocalRefEnabled 불변)"
    requirement: "LSR-01"
    verification:
      - kind: integration
        ref: "LocalRefProbe.exe roidefs — case=move_localref/move_point/center_localref/center_point/resize_localref/clear_localref"
        status: pass
    human_judgment: false
  - id: D3
    description: "마스터 재앵커는 티칭된 기준 ROI 의 중심을 Point ROI 와 같은 변환 T 로 옮기고 LocalRef_Phi 에 같은 회전각을 더하며, 미티칭 기준 ROI 는 Row/Col/Phi=0 그대로 남는다"
    requirement: "LSR-01"
    verification:
      - kind: integration
        ref: "LocalRefProbe.exe roidefs — case=transform_taught, transform_untaught"
        status: pass
    human_judgment: false
  - id: D4
    description: "기준 ROI 를 티칭하지 않은 측정(옛 레시피 전부 포함)은 ROI 배선 결과(목록·이동·중심·크기·삭제·재앵커)가 79-03 편집 전 exe 와 main-snapshot.ini EdgeToLineDistance 96섹션 + 합성 4종에서 바이트 단위로 같다"
    requirement: "LSR-05"
    verification:
      - kind: integration
        ref: "RoiRegressProbe.exe(79-03 편집 전 exe 기준 컴파일) — roi-base.txt(total=616) vs roi-new-03a.txt(Task1)/roi-new-03b.txt(Task2), cmp -s"
        status: pass
      - kind: integration
        ref: "OffRegressProbe.exe(79-01 편집 전 exe 기준) — regress-base.txt vs regress-new-03a.txt/03b.txt, cmp -s (측정값 자체는 79-03 이 손대지 않음을 재확인)"
        status: pass
    human_judgment: false
  - id: D5
    description: "MainView.xaml.cs 변경은 ROI 배선 7함수 안 삽입뿐이고, 7함수 밖 줄과 CommitRectRoi 는 편집 전과 같으며 삭제 줄 0"
    requirement: "LSR-05"
    verification:
      - kind: integration
        ref: "grep/awk 구조 비교 — outside_same=1, commit_same=1, deleted=0, newmethod=0"
        status: pass
    human_judgment: false
  - id: D6
    description: "화면 오버레이 FAI-RefLine(국부를 쓴 측정에만 생김)은 주황 2px 로 그려지고, FAI-DistLine(청록)·그 외(파랑)는 그대로다"
    requirement: "LSR-04"
    verification:
      - kind: integration
        ref: "LocalRefProbe.exe overlaycolor — case=overlay_refline_is_orange/overlay_refline_not_blue/overlay_distline_cyan/overlay_other_blue (HALCON 버퍼 창 픽셀 RGB 실측)"
        status: pass
    human_judgment: false

duration: 약 20분
completed: 2026-09-18
status: complete
---

# Phase 79 Plan 03: 국부(핀 옆 띠) 기준선 — ROI 배선·오버레이 색 Summary

**79-01 이 만든 국부 기준 ROI(LocalRef_*)를 MainView 캔버스에서 보고 끌어 맞출 수 있게 기존 ROI 배선 7함수에 subKey 분기를 끼워 넣고, 화면 오버레이 FAI-RefLine 을 주황 2px 로 구분 표시했다 — 기준 ROI 를 쓰지 않는 측정(옛 레시피 전부)은 616케이스 RoiRegressProbe 로 바이트 단위 회귀 0을 확인했다.**

## Performance

- **Duration:** 약 20분 (base-79-03 기록·pre-edit 빌드·RoiRegressProbe 최초 컴파일 → Task 1 편집·빌드·probe·커밋 → Task 2 편집·빌드·probe·커밋)
- **Completed:** 2026-09-18T05:32:21Z
- **Tasks:** 2 (Task 1 ROI 배선 7함수, Task 2 오버레이 색)
- **Files modified:** 2 (MainView.xaml.cs, HalconDisplayService.cs)

## Accomplishments

- `BuildPointRoiDefinitions`: 티칭된 기준 ROI(`LocalRef_Length1/2>0`)를 가진 EdgeToLineDistance 측정에 두 번째 `RoiDefinition`(Id `<FAI>_<측정>_LocalRef`, Name `<측정>_LocalRef`)을 추가. Point ROI 가 미티칭이면 기존 가드가 먼저 return 하므로 기준 ROI 도 표시되지 않음(동작으로 확인, 아래 "Point 미티칭 동작" 참고)
- `TryResolvePointRoiTarget`: RoiId 끝이 `_LocalRef` 인 경우 subKey `EdgeToLineDistanceMeasurement.LOCAL_REF_ROI_SUBKEY` 로 해석하는 분기 추가
- `ApplyPointRoiMoveDelta`/`TryGetPointRoiCenter`/`ApplyPointRoiResize`/`ApplyPointRoiClear`: subKey가 LocalRef 일 때만 `LocalRef_Row/Col/Length1/Length2` 를 읽고 쓰는 분기를 각 함수 상단에 추가, subKey 가 null(Point)이면 기존 줄이 그대로 처리
- `TransformMeasurementGeometry`: 마스터 재앵커 시 티칭된 국부 기준 ROI(가드: Length1/2>0)의 중심을 강체 변환 T 로 옮기고 `LocalRef_Phi` 에 같은 회전각을 더함. return 하지 않고 아래 기존 Point 처리 줄로 이어지는 단일 코드 경로 유지
- `HalconDisplayService`: `LOCAL_REF_LINE_COLOR`("orange")/`LOCAL_REF_LINE_WIDTH`(2) 상수 + `FAI-DistLine`(청록) 분기 다음에 `EdgeToLineDistanceMeasurement.LOCAL_REF_OVERLAY_ROI_ID` 비교 else-if 를 추가, 그 외(파랑) 분기는 순서·색 불변
- 신규 저장소 밖 probe **RoiRegressProbe.cs**(79-03 편집 전 exe 로 1회만 컴파일)로 EdgeToLineDistance 96섹션(main-snapshot.ini) × 6연산 + 합성 4측정 × 6~18연산 = 616케이스가 편집 전/후 exe 에서 바이트 단위로 같음을 확인
- **LocalRefProbe** 에 `roidefs`(11사례, MainView 7함수 리플렉션 호출)·`overlaycolor`(4사례, HALCON 버퍼 창에 실제 Render 호출 후 픽셀 RGB 판독) 모드를 추가

## Task Commits

Each task was committed atomically:

1. **Task 1: 기준 ROI 캔버스 배선 — 표시·해석·이동·크기·삭제·재앵커(7함수 삽입) + 미티칭 측정 비트 동일 증명** - `90fa884b` (feat)
2. **Task 2: 국부 기준선 오버레이 FAI-RefLine 주황 표시 (HalconDisplayService 색 분기) + 픽셀 확인** - `2541b15d` (feat)

**편집 전 기준(base-79-03):** `ce93f3c6b2aa595e53388349b19b21e2340de057` (docs(79-02): 사용 기준 기록·표시 plan summary)

_TDD 프레임워크 없음 — Task 1 frontmatter 는 `tdd="true"` 로 표기되어 있으나 79-01/79-02 와 동일하게 계획서의 probe verify 절차(정적 회귀 비교 + 케이스별 PASS/FAIL)로 대체했다. UI 배선 삽입은 RED/GREEN 분리보다 "편집 전 exe 대비 회귀 0" 증명이 본질이라 이 방식이 적합하다._

## Files Created/Modified

- `WPF_Example/UI/ContentItem/MainView.xaml.cs` - ROI 배선 7함수에 국부 기준 ROI(subKey `LocalRef`) 분기 삽입(72줄 추가, 삭제 0). 지역변수 `etldLocalRef`, `bLocalRefTaught`, `bIsLocalRefRoi`, `bMoveLocalRef`, `bCenterLocalRef`, `bResizeLocalRef`, `bClearLocalRef`
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` - `LOCAL_REF_LINE_COLOR`/`LOCAL_REF_LINE_WIDTH` 상수 + `Render` 색 분기에 `else if` 1개 추가(10줄 추가, 삭제 0)

## Decisions Made

- Point ROI 미티칭(`Point_Length1<=0 || Point_Length2<=0`)이면 국부 기준 ROI 가 티칭돼 있어도 캔버스에 보이지 않는다 — `BuildPointRoiDefinitions` 의 기존 `:514` 가드(`if (pLen1 <= 0 || pLen2 <= 0) return result;`)가 국부 기준 ROI 추가 코드보다 먼저 실행되기 때문이며, 새로 만든 동작이 아니라 기존 구조를 그대로 둔 결과다. `roidefs_point_untaught` 로 확인(count=0). 운영자가 "핀 위치는 아직 안 잡았지만 기준 ROI 먼저 잡기"를 원하면 이 가드를 별도로 재검토해야 하나, 이번 phase 범위 밖이며 실제 워크플로우(핀 ROI 를 먼저 잡고 기준을 나중에 추가)와도 어긋나지 않는다
- `TransformMeasurementGeometry` 삽입은 국부 기준 ROI 를 먼저 처리한 뒤 return 하지 않고 아래 기존 Point 처리 줄로 흘러가게 했다 — 두 ROI 가 같은 변환 T 로 "동시에" 옮겨진다는 것을 코드 구조로 보장(하나만 옮기고 return 하는 실수를 원천 차단)
- HalconDisplayService 의 새 색 분기는 FAI-DistLine 분기 바로 다음, 최종 else(파랑) 분기 바로 앞에 삽입 — 분기 순서를 그대로 두어 다른 모든 RoiId 의 색 판정 결과에 영향이 없음(`after_distline=1`)
- `OverlayCaptureRenderer.cs`(저장 캡처 렌더러)는 79-01 A-79-A7 결정대로 변경하지 않았다 — 저장되는 캡처 사진의 국부 기준선은 여전히 실측 RGB 8색 고정표의 "그 외" 파랑으로 그려진다(`capture_changed=0`). 화면(HalconDisplayService)과 저장 사진의 색이 다르다는 점은 79-05 U-4 에서 사용자에게 확인받는다

## Deviations from Plan

None - 계획대로 실행. Task 1/Task 2 모두 probe·regress·grep 검증 1회에 통과.

## Issues Encountered

- 없음(기능·검증 측면). 다만 Task 1 커밋 메시지 제목에 오타가 있다 — "재앵커"를 "재앵근"으로 잘못 적었다(`90fa884b`). 코드·probe·SUMMARY 본문에는 영향 없고, 커밋 메시지 재작성(rebase)은 정책상(기존 커밋 재작성 금지) 하지 않았다. 다음 phase 에서 커밋 로그를 참조할 때 참고할 것.
- (환경 메모, 79-01/79-02 인계사항과 동일) Git Bash 에서 probe 실행 시 파일 인자는 `C:/...` 형식(Windows 표기)으로 넘겨야 한다.

## Probe 출력 원문

### roidefs (11사례, `roidefs_fail=0`, Task 1·Task 2 모두 동일)

```
case=roidefs_untaught_single count=1 result=PASS
case=roidefs_taught_two count=2 result=PASS
case=roidefs_point_untaught count=0 result=PASS
case=move_localref localRow=6515 localCol=12687 result=PASS
case=move_point pointRow=102 pointCol=201 result=PASS
case=center_localref row=6510 col=12690 result=PASS
case=center_point row=100 col=200 result=PASS
case=resize_localref localRow=6510 localLen1=110 result=PASS
case=clear_localref localRow=0 enabled=1 result=PASS
case=transform_taught localRow=6443.0257822894228 pointRow=158.24419796234019 result=PASS
case=transform_untaught localRow=0 localPhi=0 result=PASS
roidefs_fail=0
```

### overlaycolor (4사례, `overlaycolor_fail=0`, HALCON 버퍼 창 300x300 실측 RGB)

```
case=overlay_refline_is_orange refRgb=255,165,0 orangeRgb=255,165,0 result=PASS
case=overlay_refline_not_blue refRgb=255,165,0 otherRgb=0,0,255 result=PASS
case=overlay_distline_cyan distRgb=0,255,255 cyanRgb=0,255,255 result=PASS
case=overlay_other_blue otherRgb=0,0,255 blueRgb=0,0,255 result=PASS
overlaycolor_fail=0
```

### synthetic (79-01/02 회귀, `synthetic_fail=0` 유지 확인)

Task 1 이후 재실행해 11사례 모두 `PASS` 유지 확인(내용은 79-01-SUMMARY.md 참고, 재출력 생략).

### RoiRegressProbe (신규, 79-03 편집 전 exe 기준 컴파일, 1회만 컴파일)

- 대상: main-snapshot.ini `TypeName=EdgeToLineDistance` 96섹션 × 6연산(build/move/center/resize/clear/transform) + 합성 4측정(DualImage 10연산, ArcLineIntersect 18연산, EdgeToLineAngle 6연산, CompoundShortAxis 6연산)
- `roi-base.txt` total=616, `roi_base_repeat_diff=0`(편집 전 exe 2회 실행 동일 — 비교 기준 결정적)
- Task 1 후: `roi-new-03a.txt` vs `roi-base.txt` → `roi_regress_diff=0`, `roi_total=616`
- Task 2 후: `roi-new-03b.txt` vs `roi-base.txt` → `roi_regress_diff=0`

### OffRegressProbe (79-01 편집 전 exe 기준, 측정값 회귀 재확인)

- `regress-base.txt`(79-01 산출, total=198)
- Task 1 후: `regress-new-03a.txt` vs `regress-base.txt` → `regress_diff=0`
- Task 2 후: `regress-new-03b.txt` vs `regress-base.txt` → `regress_diff=0`

### 빌드·grep 검증 요약

- `msbuild_exit=0` (Task 1, Task 2 각 1회, 1회에 통과)
- Task 1: `build_id=1 resolve=1 move=2 center=2 resize=4 clear=4 reanchor=1`, `outside_same=1 commit_same=1`, MainView.xaml.cs `deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 logic3=0 qmark=0 newmethod=0`
- Task 2: `color_const=1 width_const=1 branch=1 uses=2 after_distline=1 capture_changed=0`, HalconDisplayService.cs `deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0`

### probe 컴파일에 더한 참조

- `RoiRegressProbe.cs`: `-r:$H/base-bin-03/DatumMeasurement.exe`(79-03 편집 전 exe, 고정) `-r:Newtonsoft.Json.dll -r:halcondotnet.dll -r:$FW/netstandard.dll -r:$FW/WPF/PresentationFramework.dll -r:$FW/WPF/PresentationCore.dll -r:$FW/WPF/WindowsBase.dll -r:$FW/System.Xaml.dll` — OffRegressProbe 와 동일 참조 목록, DatumMeasurement.exe 참조만 79-03 base-bin 으로 교체
- `LocalRefProbe.cs`: 기존 참조 목록에서 변경 없음(`ReringProject.Halcon.Display`, `ReringProject.Halcon.Models`, `System.Collections`, `System.Runtime.Serialization` using 추가는 소스 레벨일 뿐 참조 DLL 추가 아님)

## New Symbols (79-01 Artifacts 표 79-03 행 대조)

| 심볼 | 파일 | 확인 |
|---|---|---|
| RoiId `<FAI>_<측정>_LocalRef`, subKey `LocalRef` 분기 — ROI 배선 7함수 삽입 | MainView.xaml.cs | O |
| 지역변수 `etldLocalRef`, `bLocalRefTaught`, `bIsLocalRefRoi`, `bMoveLocalRef`, `bCenterLocalRef`, `bResizeLocalRef`, `bClearLocalRef` | MainView.xaml.cs | O |
| `HalconDisplayService.LOCAL_REF_LINE_COLOR`("orange")/`LOCAL_REF_LINE_WIDTH`(2) + `EdgeToLineDistanceMeasurement.LOCAL_REF_OVERLAY_ROI_ID` 참조 색 분기 | HalconDisplayService.cs | O |
| `RoiRegressProbe.cs/.exe`(신규, 저장소 밖) | probe | O |
| `LocalRefProbe` `roidefs`·`overlaycolor` 모드(저장소 밖) | probe | O |

79-04(버전 감사)·79-05(realab UAT)는 이 plan 범위 밖.

## User Setup Required

None - 외부 서비스 설정 불필요.

## Next Phase Readiness

- 79-04(버전 감사)는 이 plan 이 수정한 2개 파일(MainView.xaml.cs, HalconDisplayService.cs)을 회귀 점검 대상에 포함해야 한다
- 79-05(realab UAT)의 U-1(첫 기준 ROI 티칭 UX)은 이 plan 이 만든 캔버스 표시·이동·크기·삭제·재앵커 코드 경로가 이미 증명됨 — 실제 화면 조작(드래그로 상자 맞추기)만 남았다. A-79-A5(첫 기준 ROI 는 속성창 숫자 입력, CommitRectRoi 미변경)도 그대로 유효
- 79-05 U-4(옵션 꺼짐 '기준' 칸 빈칸 확인 + 화면 주황/저장 사진 파랑 차이 확인, A-79-A7)는 이 plan 의 `overlay_refline_is_orange`/`capture_changed=0` PASS 로 코드 경로가 이미 증명됨 — 실제 화면·저장 사진 확인만 남음
- `RoiRegressProbe.exe`(base-bin-03 고정 참조)는 이후 plan 에서 재컴파일하지 않는다 — 79-04 회귀 점검에서도 그대로 재사용 가능

## Self-Check: PASSED

수정 파일 2개(`WPF_Example/UI/ContentItem/MainView.xaml.cs`, `WPF_Example/Halcon/Display/HalconDisplayService.cs`) 존재 확인, 커밋 해시 2개(`90fa884b`, `2541b15d`) `git log --oneline --all` 에서 확인됨.

---
*Phase: 79-side-local-strip-datum*
*Completed: 2026-09-18*
