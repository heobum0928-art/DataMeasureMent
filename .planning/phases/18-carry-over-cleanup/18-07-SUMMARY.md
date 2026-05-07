---
phase: 18
plan: 07
status: complete
gap_closure: true
requirements: [CO-04]
completed: 2026-05-07
commits:
  - a7116f3 feat(18-07) — 재티칭 모달 분기 추가
  - 72afb5c fix(18-07) — 모달 문구 수정 (두 경로 명시)
---

## Result

기존 티칭된 Datum (`IsConfigured=true` + 모든 ROI 존재) 에서 `btn_teachDatum` 클릭 시 silent re-teach 가 일어나던 동작에 명시적 사용자 확인 모달을 추가했다. 사용자는 이제 (1) 기존 ROI 로 재티칭, 또는 (2) ROI 삭제 후 재그리기 두 경로를 의식적으로 선택할 수 있다.

## What changed

**File:** `WPF_Example/UI/ContentItem/MainView.xaml.cs`

`TeachDatumButton_Click` ON 분기, `if (datum.IsConfigured)` 블록 안 `ValidateRoiPresence` 가드 통과 직후에 다음 분기를 추가:

- `CustomMessageBox.ShowConfirmation("재티칭 확인", "...", MessageBoxButton.YesNo)` 호출
- 메시지: "이 Datum 은 이미 티칭되어 있습니다.\n기존 ROI 로 재티칭하시겠습니까?\n\n(ROI 를 다시 그리려면 먼저 삭제해 주세요.)"
- **No** → 5개 필드 reset (`IsChecked=false`, `_canvasMode=None`, `_editingDatum=null`, `IsEditMode=false`, `IsTeachDatumMode=false`) → return
- **Yes** → 모달 닫히고 그대로 통과 → `GetFirstMissingStep` = `Done` → `StartDatumTeachStep(Done)` → `InvokeTryTeachDatum()` (기존 silent re-teach 경로 = 명시 승인)

## key-files
created: []
modified:
  - WPF_Example/UI/ContentItem/MainView.xaml.cs

## Verification

| Scenario | 동작 | 결과 |
|----------|------|------|
| 1. 새 Datum (IsConfigured=false) + click | 모달 없이 wizard 즉시 시작 | PASS |
| 2. 기존 Datum + ROI 일부 누락 + click | "티칭 실패" 모달, 재티칭 모달 미표시 | PASS |
| 3a. 기존 Datum + 모든 ROI + No | 모달 닫힘, 버튼 OFF, TeachDatum 모드 미진입 | PASS |
| 3b. 기존 Datum + 모든 ROI + Yes | 모달 닫힘, 즉시 teach 실행 | PASS |

사용자 PASS — 2026-05-07 12:5x runtime UAT.

**msbuild Debug/x64:** 컴파일 PASS (CS 에러 0). 빌드 산출물 복사는 사용자가 실행 중인 `DatumMeasurement.exe` (PID 21496) 잠금 해제 후 정상 갱신.

## Self-Check

- [x] msbuild Debug/x64 컴파일 PASS, 신규 warning 0
- [x] CustomMessageBox.ShowConfirmation 시그니처 (string title, string message, MessageBoxButton) 사용
- [x] No 분기 reset 5개 필드 적용
- [x] No 분기 ExitCanvasMode() 미호출 (L1344 에서 prior 정리됨)
- [x] 모든 변경 라인 `//260507 hbk Phase 18 18-07` 주석
- [x] 시나리오 1/2/3a/3b 사용자 PASS
- [x] CO-04 (UAT Test 2 reclassified) 충족

## Notes

원 plan 의 문구 ("이 Datum 은 이미 티칭되어 있습니다.\n다시 티칭하시겠습니까?") 가 Yes/No 의미를 모호하게 만든다는 사용자 피드백 반영하여 두 경로 (기존 ROI 재티칭 vs ROI 삭제 후 재그리기) 를 모달에서 명시. UX 개선이 본 plan 의 부수적 outcome.
