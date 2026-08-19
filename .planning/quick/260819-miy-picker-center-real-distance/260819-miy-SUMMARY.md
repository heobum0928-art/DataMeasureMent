---
phase: quick-260819-miy
plan: 01
status: complete
subsystem: ui
tags: [bottomvisionview, picker-center, calibration, mm-conversion]

requires: []
provides:
  - "BuildPickerCenterText(double r, double c, double rad) 헬퍼: 피커센터 계산 결과(픽셀)에 화면(이미지) 중심 대비 실제 오프셋(mm, 총거리+가로+세로)을 붙여 표시"
affects: [bottom-vision-picker-calibration-ui]

tech-stack:
  added: []
  patterns:
    - "px→mm 환산: SystemSetting.Handle.EthernetPixelResolution(µm/px) / UM_PER_MM(1000.0) — AlignShapeMatchService.cs 의 기존 상수/공식과 동일, 이 파일 안에 로컬 const 로 재정의"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/UI/BottomVisionView.xaml.cs

key-decisions:
  - "비교 기준 = 이미지 자체의 중앙(가로/세로 절반). SystemSetting.Handle.PickerCenterRow/Col 은 이 계산이 채워 넣는 대상값이지 비교 기준이 아니므로 사용하지 않음(plan 조사 결과 그대로 따름)."
  - "_viewer == null || _viewer.CurrentImage == null 이면 예외를 던지지 않고 기존 픽셀-only 문구로 조용히 폴백 — 오프라인/이미지 미로드 상태에서도 Compute 버튼이 크래시하지 않음."
  - "TCP 자동경로(EthernetVisionHandler.OnCalibEndViewer, L108 부근)는 이번 헬퍼를 쓰지 않고 원래의 string.Format 그대로 둠 — 수동 Compute 버튼 경로만 확장(plan 범위 제약)."

requirements-completed: [QUICK-260819-MIY-01]

duration: 약 20분
completed: 2026-08-19
---

# Quick 260819-miy: 피커센터 Compute 결과에 화면중심 대비 실거리(mm) 표시 추가 Summary

**`BottomVisionView.xaml.cs` 의 `CalComputeButton_Click` 이 호출하는 신규 `BuildPickerCenterText` 헬퍼로, 기존 픽셀 좌표/반경 표시 뒤에 이미지 중심 대비 실제 오프셋(mm, 총거리+가로+세로)을 이어 붙여 `lbl_pickerCenter` 에 표시**

## Performance

- **Duration:** 약 20분
- **Completed:** 2026-08-19
- **Tasks:** 2/3 자동 실행 완료, 1개(Task 3, checkpoint:human-verify)는 실기(카메라) 필요 — 이 세션에서 실행 불가(PC 에 카메라 없음), 사람 확인 대기로 표시
- **Files modified:** 1

## Accomplishments

- `CalComputeButton_Click` 의 `if (bOk)` 블록 첫 대입(L903~904, 편집 전)을 `lbl_pickerCenter.Text = BuildPickerCenterText(r, c, rad);` 1줄로 교체 — 그 아래 오버레이 표시/저장 확인 다이얼로그/`SystemSetting.Handle.Save()` 로직은 전혀 손대지 않음
- 신규 `private string BuildPickerCenterText(double r, double c, double rad)` 헬퍼를 `CalComputeButton_Click` 닫는 `}` 바로 다음, `// ─── private 헬퍼 ───...` 구분 주석 앞에 추가(약 33줄)
  - `_viewer.CurrentImage.GetImageSize` 로 이미지 가로/세로를 얻어 중심(`imgCenterCol`/`imgCenterRow`) 계산
  - `dRowPx = r - imgCenterRow`, `dColPx = c - imgCenterCol`, `totalPx = sqrt(dRowPx²+dColPx²)` 로 픽셀 오프셋 계산
  - `SystemSetting.Handle.EthernetPixelResolution / UM_PER_MM(1000.0)` 로 mm/px 환산 후 총/가로/세로 3값을 mm 로 변환
  - 반환 문구 예: `피커센터 (512.34,480.12) r=15.20  |  중심오프셋 0.523mm (가로 0.412mm, 세로 0.321mm)`
  - `_viewer == null || _viewer.CurrentImage == null` 이면 mm 부분 없이 기존 픽셀-only 문구(`피커센터 (r,c) r=반경`)로 조용히 폴백 — 예외 없음
- `TryComputePickerCenter` 시그니처, `PickerCenterCalibrationService.cs`, 저장 확인 다이얼로그(`MessageBox.Show(...YesNo...)`)/`SystemSetting.Handle.Save()` 호출 로직, 모든 `.xaml` 파일 — 전부 무변경(grep/`git status` 로 확인)
- TCP 자동경로(`EthernetVisionHandler.OnCalibEndViewer`, L108 부근)의 `lbl_pickerCenter.Text = string.Format(...)` 원본 그대로 보존(수동 버튼 경로만 확장)

## Task Commits

1. **Task 1: BuildPickerCenterText 헬퍼 추가 + CalComputeButton_Click 호출부 교체** - `015a302` (feat)

Task 2(검증/빌드)는 코드 수정이 없는 검증 전용 태스크라 별도 커밋 없음(plan 명시). Task 3(실기 확인)은 사람 확인 대기 — 아래 "실기 확인 대기" 섹션 참조. metadata 커밋(SUMMARY.md)은 별도 진행하지 않음(quick 태스크 관례상 STATE.md 갱신 커밋에서 함께 처리).

## Files Created/Modified

- `WPF_Example/Custom/UI/BottomVisionView.xaml.cs` - L902~904(교체, 2줄→1줄) + 헬퍼 신규 삽입(약 33줄). `git diff --numstat`: `35 insertions(+), 2 deletions(-)`

## Decisions Made

Plan 이 제시한 코드/문구를 한 글자도 다르지 않게 그대로 사용. 비교 기준(이미지 중심 vs `SystemSetting.Handle.PickerCenterRow/Col`)과 mm 환산 상수(`UM_PER_MM=1000.0`, 로컬 재정의) 판단은 plan 이 이미 조사·확정해 둔 것을 그대로 따름 — 별도 판단 불필요.

## Deviations from Plan

None - plan 그대로 실행됨.

## Issues Encountered

None. 빌드 산출물 잠김 없음(앱 미실행 상태) — 스크래치 OutDir 폴백 불필요, 기본 `bin/x64/Debug` 경로로 정상 빌드됨.

## Verification Results

| # | 항목 | 결과 |
|---|---|---|
| 1 | Task 1 정적 검증 7종: `HELPER=1 CALL=1 OLDCALL=1 UMCONST=1 RES=1 GETSIZE=1 GUARD=5` | PASS (plan 기대값과 정확히 일치) |
| 2 | S1 변경 범위: `git status --porcelain -- WPF_Example` = `DatumMeasurement.csproj` 1줄만(사전 존재, 무관) — `BottomVisionView.xaml.cs` 는 커밋 완료 상태라 클린. `PickerCenterCalibrationService.cs`/`*.xaml` 은 둘 다 빈 출력 | PASS |
| 3 | S2 변경 폭: `git diff --numstat`(커밋 대상) = `35 insertions(+), 2 deletions(-)` — 삭제줄 정확히 2(교체한 2줄) | PASS |
| 4 | S3 코딩 규칙: 추가된 줄에서 삼항 연산자(`?`) 0건, 신규 `using` 0건 | PASS |
| 5 | S4 빌드: `BUILD_RC=0 ERRORS=0 WARN_CS=12`(CS0618×10 + CS0162×2, baseline 정확 일치, 신규 경고 0건). 스크래치 OutDir 폴백 미사용(잠김 없었음) | PASS |
| 6 | 커밋 위생: `BottomVisionView.xaml.cs` 1개 파일만 스테이징·커밋(`git add` 경로 명시, `-A`/`-a` 미사용). 커밋 전/후 모두 `DatumMeasurement.csproj` unstaged(` M`) 유지 확인 | PASS |
| 7 | Task 3(실기 확인, checkpoint:human-verify) | **PENDING** — 이 세션은 카메라 하드웨어가 없는 PC 라 실행 불가. 사람 확인 대기(아래 섹션) |

## 실기 확인 대기 (Task 3, checkpoint:human-verify — 미실행)

이 작업은 실제 카메라/피커 하드웨어가 있는 PC 에서만 확인 가능하며, 이번 세션 환경에는 카메라가 없어 실행할 수 없습니다. 실패로 간주하지 않고 **대기 상태**로 남깁니다.

확인 방법(plan L285~293 그대로):
1. 앱을 다시 빌드/실행 → Bottom 비전 탭으로 이동
2. 피커센터 캘리브레이션 스텝을 몇 번 진행(Step 누적) 후 **Compute** 버튼 클릭
3. 라벨에 `피커센터 (r,c) r=반경` 뒤에 `중심오프셋 N.NNNmm (가로 N.NNNmm, 세로 N.NNNmm)` 형태가 이어 붙는지 확인
4. 값이 상식적인 범위인지 확인 — 총거리(중심오프셋) ≥ max(|가로|,|세로|) 는 피타고라스 정리상 항상 성립해야 함
5. (선택) 이미지 미로드 상태에서 계산 성공 시 mm 부분 없이 기존 픽셀 표시만 나오는지 확인(크래시 없음)
6. 기존 동작(저장 확인 다이얼로그, 저장 완료/취소 문구, 피팅원 오버레이) 이전과 동일 작동 확인

r=c=이미지 중심일 때 공식상 총 오프셋이 정확히 0.000mm 가 되는 것은 코드 리뷰로 확인됨(`dRowPx=dColPx=0` → `totalPx=0` → `totalMm=0`) — 별도 실측 불필요.

## User Setup Required

None - 외부 서비스 설정 불필요. 다음 실사용(하드웨어 세션) 때 위 "실기 확인 대기" 6단계만 확인하면 됨.

## Next Phase Readiness

- 코드 변경/빌드 완료, 후속 코드 작업 없음
- Blockers 없음(실기 확인은 blocker 가 아니라 정상적인 대기 항목)

## Known Stubs

없음 - 표시 전용 헬퍼 추가이며 새 데이터 소스/바인딩 없음.

## Threat Flags

없음 - 신규 네트워크 엔드포인트·인증 경로·파일 접근·스키마 변경 없음. 로컬 UI 라벨 표시 로직만 추가(threat_model 의 T-miy-01/02/03 모두 disposition 대로: mitigate 항목은 null 가드로 구현 완료, accept 항목은 해당 없음).

## Self-Check: PASSED

파일 존재 확인:
```
FOUND: WPF_Example/Custom/UI/BottomVisionView.xaml.cs
```

커밋 존재 확인:
```
FOUND: 015a302
```

---
*Phase: quick-260819-miy*
*Completed: 2026-08-19*
