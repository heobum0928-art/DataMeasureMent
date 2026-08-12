---
phase: quick-260812-m8i
plan: 01
subsystem: ui-diagnostics
tags: [halcon, korean-localization, error-messages, custommessagebox, wpf, teaching-ux]

# Dependency graph
requires: []
provides:
  - "신규 공용 헬퍼 `ReringProject.Halcon.Algorithms.TeachDiagnostics` — ETeachGrade 열거형 + ClassifyScore(등급 산정, 미배선 인프라) + ToKoreanMessage(원문→한국어, EXACT 10 + NESTED 9 + VALUE 3 사전, 미매칭 시 원문 병기) + ToStatusLine(●▲✕ 기호) + GradeBrush(초록/주황/빨강)"
  - "Datum 티칭 실패 모달 7곳 isAutoClosing=false — 더 이상 7초 뒤 저절로 닫히지 않음"
  - "MainView.xaml.cs raw HALCON/서비스 원문 4곳 → ToKoreanMessage 배선(모델 생성 실패/기준 위치 기록 실패/패턴2 폴백 경고 2곳)"
  - "TrayVisionView/BottomVisionView lbl_teachStatus 대입 15곳(6+9) 전부 문구(ToStatusLine)+색(GradeBrush) 짝지음 — 실패 후 성공 시 빨강 잔존(stale) 방지"
affects:
  - WPF_Example/Halcon/Algorithms/TeachDiagnostics.cs
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
  - WPF_Example/Custom/UI/TrayVisionView.xaml.cs
  - WPF_Example/Custom/UI/BottomVisionView.xaml.cs
  - "다음 Quick(260812 #2 — 점수 등급 실제 배선), Quick #3 — Calib TCP 자동경로 노출"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "표시 전용 헬퍼는 판정 로직과 완전히 분리된 static 클래스로 신설 — 호출부는 CustomMessageBox 인자/lbl_teachStatus.Text·Foreground 대입만 교체, if/else/return/서비스 호출 줄은 diff에 절대 등장 안 시킴(git diff -U0 기반 하드제약 grep으로 기계적 검증)"
    - "원문 사전은 Contains 키워드 추측이 아니라 서비스 파일의 실제 error = \"...\" 리터럴과 1:1 Ordinal 완전일치 — 사전에 없는 원문은 숨기지 않고 \"(원본: ...)\" 로 병기해 미번역 오류를 은폐하지 않음"
    - "using 별칭(using TeachDiag = ...; using ETeachGrade = ...;)으로 이름 충돌(CalibrationResult 등 Halcon.Models) 회피 — 전체 namespace using을 추가하지 않음"

key-files:
  created:
    - WPF_Example/Halcon/Algorithms/TeachDiagnostics.cs
  modified:
    - WPF_Example/DatumMeasurement.csproj
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/Custom/UI/TrayVisionView.xaml.cs
    - WPF_Example/Custom/UI/BottomVisionView.xaml.cs

key-decisions:
  - "브리핑은 '성공 경로 색 복귀'만 지목했으나, lbl_teachStatus.Text 대입 15곳 전부(성공/실패/대기/미선택 포함)에 Foreground를 짝지었다 — 일부만 칠하면 stale 색이 실제로는 안 사라지기 때문. Tray 6/6, Bottom 9/9로 Text==Foreground 대입수 일치를 grep으로 확인."
  - "Bottom lbl_teachStatus 대입은 실측 9곳뿐(494/499가 아니라 448/466/490/496/501/953/956/963/966) — 브리핑이 적은 768/852/895는 존재하지 않음(그 부근은 별개 라벨 lbl_status와 동축/캘 관련 코드). PLAN.md interfaces 섹션의 실측 목록을 그대로 따름."
  - "ClassifyScore는 이번 Quick에서 호출부를 만들지 않음 — Quick #2가 그대로 소비할 인프라로만 신설(계획대로)."
  - "csproj는 클래식 스타일이라 TeachDiagnostics.cs를 <Compile Include>로 수동 등록하지 않으면 조용히 컴파일 제외됨 — RoiLineIntersectionAlgorithm.cs 다음, HalconDisplayService.cs 전에 1줄 삽입."

requirements-completed: [TEACH-UX-01]

# Metrics
duration: ~18min
completed: 2026-08-12
---

# Quick Task 260812-m8i: 티칭 실패/품질 진단 표시 헬퍼 + 한국어화 (1/3) Summary

**신규 `TeachDiagnostics` 표시 헬퍼(한국어 오류사전 22항목 + 등급/색 인프라) 신설, Datum 실패 모달 7곳 자동닫힘 제거 + 한국어화, Align 상태 라벨 15곳 색 stale 방지 — 판정 로직·HALCON 호출 diff 0줄**

## Performance

- **Duration:** ~18 min
- **Completed:** 2026-08-12
- **Tasks:** 2 of 2
- **Files modified:** 5 (신규 1 + 수정 4)

## Accomplishments

### Task 1 — TeachDiagnostics.cs 신설 + csproj 등록

- `ETeachGrade`(Good/Weak/Bad) + `ClassifyScore`(Quick #2 배선용 미사용 인프라) + `ToKoreanMessage`(원문→한국어) + `ToStatusLine`(●/▲/✕ 기호줄) + `ToGradeBrush`(초록 #16A34A/주황 #D97706/빨강 #DC2626) 5종 공개 API.
- 한국어 사전 3계층, 전부 `PatternMatchService.cs`/`AlignShapeMatchService.cs`의 실제 `error = "..."` 리터럴과 대조 완료(추측 0):
  - EXACT_MESSAGES(완전일치) 10항목 — `templateImage is null`, `no match found (empty result)`, `angle_lx 산출 실패...` 등
  - NESTED_PREFIXES(접두부+재귀해석) 9항목 — `TryCreateModel[1]: `, `TryFindRefPose[2]: `, `TryTeach exception: ` 등
  - VALUE_PREFIXES(접두부+수치 병기) 3항목 — `NCC ref find: no match above minScore=` 등
- 사전에 없는 원문은 숨기지 않고 `원인을 정확히 알 수 없는 오류입니다... (원본: {rawError})` 형태로 그대로 병기.
- `WPF_Example/DatumMeasurement.csproj` L581 `RoiLineIntersectionAlgorithm.cs` 바로 다음 줄에 `<Compile Include="Halcon\Algorithms\TeachDiagnostics.cs" />` 삽입(클래식 csproj — 자동 포함 없음).
- Allman 브레이스, 삼항연산자 0건, C# 7.2, UTF-8 BOM 없음(`2f 2f` 로 시작 확인).

### Task 2 — 한국어화 + 모달 자동닫힘 제거 + 상태 라벨 색 명시

**MainView.xaml.cs `InvokeCreatePatternModel`(L3848~3974) 안 9곳:**
- C-1~C-4, C-7: `CustomMessageBox.Show(...)` 끝에 `, MessageBoxImage.Error, true, false` 추가(메시지 본문은 이미 한국어라 무변경) — 5곳.
- C-5, C-6: 패턴2 폴백 경고의 `+ (refErr2 ?? "")` / `+ (err2 ?? "")` → `TeachDiagnostics.ToKoreanMessage(refErr2)` / `(err2)`.
- C-8: 제목 `"ref pose 기록 실패"` → `"기준 위치 기록 실패"`, 본문 `refError` → `ToKoreanMessage(refError)` + 자동닫힘 제거.
- C-9: 본문 `error` → `ToKoreanMessage(error)` + 자동닫힘 제거.
- `Show(...)` 다음 줄의 `return;`은 전부 원래 위치·형태 그대로 보존.

**TrayVisionView.xaml.cs — using 별칭 2줄 + `lbl_teachStatus` 6곳:**
- `using TeachDiag = ...; using ETeachGrade = ...;` (using ReringProject.UI; 다음).
- 유효성 검증 실패(Weak), 티칭 성공(Good), 티칭 실패(Bad, raw error→ToKoreanMessage), 예외(Bad), RefreshStatus의 OK(Good)/없음(Weak) — 6곳 전부 `Text = ToStatusLine(등급, 문구)` + `Foreground = GradeBrush(등급)` 2줄 페어로 전환.

**BottomVisionView.xaml.cs — using 별칭 2줄 + `lbl_teachStatus` 9곳:**
- 슬롯 미선택(Weak), 유효성 검증 실패(Weak), 슬롯별 성공(Good, `_slotRois[...] = ...` 로직 무변경), 실패(Bad), 예외(Bad), RefreshTeachStatus의 단일경로 OK(Good)/없음(Weak)/슬롯 OK(Good)/슬롯 없음(Weak) — 9곳 동일 패턴. `//260626 hbk ...` 줄 끝 주석 전부 보존.

## Task Commits

Each task was committed atomically:

1. **Task 1: TeachDiagnostics.cs 신설 + csproj 등록** - `ca5a380` (feat)
2. **Task 2: 실패 문구 한국어화 + 모달 자동닫힘 제거 + 상태 라벨 색 명시** - `5466f06` (feat)

**Plan metadata:** 본 SUMMARY.md 및 STATE.md/ROADMAP.md는 오케스트레이터가 별도 커밋(실행자는 커밋하지 않음).

_Note: 이 quick task는 TDD 대상이 아님(표시 문자열/색/모달 옵션 추가, 판정 분기 없음) — RED/GREEN 게이트 해당 없음._

## Files Created/Modified

- `WPF_Example/Halcon/Algorithms/TeachDiagnostics.cs` (신규) — 한국어 오류 사전 + 등급/색 표시 헬퍼. 판정/HALCON 호출 없음.
- `WPF_Example/DatumMeasurement.csproj` — `<Compile Include>` 1줄 추가.
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — `InvokeCreatePatternModel` 안 9곳(자동닫힘 제거 7 + 한국어화 4, 겹침 있음).
- `WPF_Example/Custom/UI/TrayVisionView.xaml.cs` — using 별칭 2줄 + `lbl_teachStatus` 6곳.
- `WPF_Example/Custom/UI/BottomVisionView.xaml.cs` — using 별칭 2줄 + `lbl_teachStatus` 9곳.

## Decisions Made

- 범위를 "성공 경로 색 복귀"에서 "Text 대입 15곳 전부"로 확장 — 일부만 칠하면 stale이 실제로 사라지지 않는다는 판단(PLAN.md가 사전에 명시한 결정, 그대로 실행).
- Bottom 실패 지점 줄번호는 브리핑(768/852/895)이 아니라 PLAN.md 실측 목록(448/466/490/496/501/953/956/963/966)을 따름 — 실제 코드에 768/852/895 부근은 `lbl_status`(별개 라벨)와 동축/캘 코드였음, PLAN.md가 이미 이 정정을 포함하고 있었음.
- `ClassifyScore`는 호출부 없이 인프라로만 신설(Quick #2가 배선 예정) — 계획대로.

## Deviations from Plan

None - plan executed exactly as written. 사전 항목·csproj 삽입 위치·9+6+9곳 편집 전부 PLAN.md `<interfaces>` 섹션 표를 앵커로 그대로 따름.

## 하드 제약 검증 결과 (실제 명령 출력)

**하드제약 1 — 판정 심볼이 diff 추가/삭제 줄에 0건:**
```
$ git diff -U0 -- MainView.xaml.cs TrayVisionView.xaml.cs BottomVisionView.xaml.cs \
  | grep -E '^[+-][^+-]' \
  | grep -E 'HOperatorSet\.|TryTeach\(|TryCreateModel\(|TryFindPose\(|TryFindRefPose\(|HasTemplate\(|Matcher\.Run|RefMatch|\breturn\b|\bif *\(|\belse\b|ShowConfirmation|SaveRecipe|EnsurePerRoiDefaults|_slotRois|CommitActiveRectangle|RectToTeachParams|ValidateRois\(\)|ApplyCoaxLight' \
  | wc -l
0
```

**하드제약 2 — 판정 엔진 3파일 무변경:**
```
$ git status --porcelain -- PatternMatchService.cs AlignShapeMatchService.cs PickerCenterCalibrationService.cs | wc -l
0
```

**하드제약 3 — 진입점 앵커 생존(참고용, 실질 보증은 1/4):**
```
grep -c 'bool ok = svc.TryCreateModel(' MainView.xaml.cs                                       → 1
grep -c 'bool bOk = EthernetVisionHandler.Handle.Matcher.TryTeach(' TrayVisionView.xaml.cs      → 1
grep -c 'bool bOk = EthernetVisionHandler.Handle.Matcher.TryTeach(' BottomVisionView.xaml.cs    → 1
```

**하드제약 4 — 추가줄이 화이트리스트(표시 레이어) 밖으로 새지 않음, 0건:**
```
$ git diff -U0 -- MainView.xaml.cs TrayVisionView.xaml.cs BottomVisionView.xaml.cs \
  | grep -E '^\+[^+]' \
  | grep -vE 'lbl_teachStatus\.(Text|Foreground)|CustomMessageBox\.Show\(|alignMsg = |^\+using |//quick-260812' \
  | wc -l
0
```

## 검증 세부 (한국어화/자동닫힘/색)

- raw 원형 6종(`CustomMessageBox.Show("모델 생성 실패", error)` / `("ref pose 기록 실패", refError)` / `"티칭 실패: " + error;`×2 / `"티칭 예외: " + ex.Message;`×2) 전부 **0건**.
- `ToKoreanMessage` 배선: MainView 4곳(`error`/`refError`/`refErr2`/`err2`), Tray 2곳, Bottom 2곳 — 계획과 정확히 일치.
- `MessageBoxImage.Error, true, false` — **7건**(C-1~C-4, C-7, C-8, C-9).
- 색 stale 방지: Tray `Text` 6 == `Foreground` 6, Bottom `Text` 9 == `Foreground` 9. `GradeBrush(ETeachGrade.Good)` Tray 2건 / Bottom 3건 존재.
- 추가줄 삼항연산자(` ? `) **0건**.
- 변경 파일 정확히 5개(신규 1 + 수정 4) — `git status --porcelain`에 `.planning/quick/...`만 추가로 등장. `PickerCenterCalibrationService.cs`는 diff·status 어디에도 없음.

## 빌드 검증

- Task 1: `MSBuild /p:Configuration=Debug /p:Platform=x64 /v:minimal` — **성공(exit 0)**, `bin\x64\Debug\DatumMeasurement.exe` 갱신. 앱이 실행 중이 아니어서 잠기지 않았고 정식 경로가 바로 통과(스크래치 OutDir 폴백 불필요).
- Task 2: `MSBuild /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /v:minimal` — **성공(exit 0)**, 전체 재빌드 통과.
- 신규 `error CS` **0건**. 신규 `warning CS` **0건** — 빌드 로그에 나타난 경고 7건(`CS0618` obsolete 클래스 4건, `CS0162` VirtualCamera 도달불가 코드 2건 등)은 전부 이번 변경과 무관한 pre-existing 경고(`Sequence_Top.cs`/`Sequence_Bottom.cs`/`SequenceHandler.cs`/`VirtualCamera.cs` — 이번 5개 변경 파일에 없음).

## Issues Encountered

None.

## User Setup Required

None - 외부 서비스 설정 불필요.

## Next Phase Readiness

- `TeachDiagnostics` 공개 API 시그니처(`ETeachGrade`/`ClassifyScore`/`ToKoreanMessage`/`ToStatusLine`/`GradeBrush`)가 확정돼 Quick #2(점수 등급 실제 배선)가 코드 수정 없이 바로 소비 가능.
- Quick #3(Calib TCP 자동경로 노출)은 이번 범위 밖 — 이번 작업이 만든 헬퍼와 무관하게 독립 진행 가능.
- `PickerCenterCalibrationService.cs`와 사용자 `stash@{0}` 실험 완전 보존(`git status --porcelain` 0줄, `git stash list`에 여전히 1건).
- Datum/Align 티칭 실패 실기 UAT 권장: 실제 실패 케이스(이미지 없음/ROI 미설정/모델 못 찾음 등)를 유도해 한국어 문구가 자연스럽게 읽히는지, 모달이 저절로 안 닫히는지, 라벨 색이 실패→성공 전환 시 정확히 초록으로 복귀하는지 육안 확인.

---
*Phase: quick-260812-m8i*
*Completed: 2026-08-12*

## Self-Check: PASSED

- FOUND: WPF_Example/Halcon/Algorithms/TeachDiagnostics.cs
- FOUND: WPF_Example/DatumMeasurement.csproj
- FOUND: WPF_Example/UI/ContentItem/MainView.xaml.cs
- FOUND: WPF_Example/Custom/UI/TrayVisionView.xaml.cs
- FOUND: WPF_Example/Custom/UI/BottomVisionView.xaml.cs
- FOUND commit: ca5a380 (Task 1)
- FOUND commit: 5466f06 (Task 2)
