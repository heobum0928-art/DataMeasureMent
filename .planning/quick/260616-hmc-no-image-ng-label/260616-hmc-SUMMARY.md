---
quick_id: 260616-hmc
slug: no-image-ng-label
date: 2026-06-16
status: complete
commits:
  - 11c359e
  - c512839
---

# Quick Task 260616-hmc: NO_IMAGE NG 라벨 일관 표기

## 배경

선행 디버그 수정 `simul-shot-cascade`(commit de7773f)에서 무효 `SimulImagePath` SHOT을 `image=null` 로 두고 `Action_FAIMeasurement.cs` EStep.Measure else 분기에서 `allPass=false` + 모든 measurement `LastSkipReason="NO_IMAGE"` + `LastJudgement=false` 로 NG 처리하는 로직이 들어갔다. NG 판정 자체는 동작했으나, 판정 라벨 렌더 4곳이 `"DATUM_FAIL"` 만 인식하고 `"NO_IMAGE"` 는 누락되어 결과 그리드/Excel 에서 명확한 NG 가 아니라 `"—"`(미측정 dash) 로 표시되는 갭이 남아 있었다.

사용자 결정: **명확한 NG + "이미지 없음" 라벨**. JudgeText 표시 토큰은 기존 영문(OK/NG/DETECT FAIL) 일관성 위해 `"NO IMAGE"` 사용.

## 변경 내용

DATUM_FAIL 분기 옆에 NO_IMAGE 분기를 추가 (DATUM_FAIL 직후, HasResult 분기 앞 — 우선순위 유지):

| # | 파일 | 변경 |
|---|------|------|
| 1 | `WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs` | JudgeText 3분기에 `else if (LastSkipReason == "NO_IMAGE") JudgeText="NO IMAGE";` |
| 2 | `WPF_Example/Custom/Export/ExcelExportService.cs` | Excel 8번 컬럼 동일 분기 |
| 3 | `WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs` | '불량만 보기' 필터(185) + 첫 불량 자동 포커스(191) 조건에 `|| r.JudgeText == "NO IMAGE"` |
| 4 | `WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs` | 반복도 통계: 조건을 `DATUM_FAIL || NO_IMAGE` 로 확장 → 값 목록 제외 + DetectFailCount 집계 |

확인만(변경 불필요): `CycleResultSerializer.cs:126` 가 `meas.LastSkipReason` 을 verbatim 복사 → `"NO_IMAGE"` DTO 전달 이미 동작.

## 핵심 주의점

- 매칭 키는 밑줄 `"NO_IMAGE"` (LastSkipReason), 표시 토큰은 공백 `"NO IMAGE"` (JudgeText). ReviewerWindow 만 JudgeText 비교, 나머지는 LastSkipReason 비교.
- C# 7.2 if/else-if 체인 유지, 각 파일 기존 brace 스타일, 모든 변경 라인 `//260616 hbk` 주석.

## 검증

- **MSBuild Debug|x64 (SIMUL_MODE) 빌드 성공** — 0 errors, `DatumMeasurement.exe` 생성. 경고는 전부 사전 존재(CS0618/MSB3884/CS0162), 본 변경 무관.
- 4곳 분기 문자열/조건 일관성 정적 확인.

## Commits

- `11c359e` feat(quick-260616-hmc-01): UI/Export NO_IMAGE → "NO IMAGE" 라벨 분기 3곳 추가
- `c512839` feat(quick-260616-hmc-01): 반복도 통계 NO_IMAGE = DetectFail 취급
