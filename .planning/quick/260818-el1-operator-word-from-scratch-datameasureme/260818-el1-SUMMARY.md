---
phase: quick-260818-el1
plan: 01
subsystem: docs
tags: [operator-manual, docx, python-docx, korean, screenshot-checklist]

requires: []
provides:
  - "현재 코드베이스(2026-08-18 시점) 기준으로 처음부터 작성한 현장 운영자용 Word 매뉴얼"
  - "매뉴얼 원고(.md, 단일 소스) + 재생성 가능한 .docx 빌드 스크립트"
  - "그림 자리표시자 36개와 1:1 대응하는 스크린샷 캡처 체크리스트"
affects: []

tech-stack:
  added: []
  patterns:
    - "python-docx 기반 .md -> .docx 변환 스크립트 (스크립트 위치 기준 상대경로, 재실행 가능)"
    - "본문 번호 목록은 원 안 숫자(①②③...) 사용 — 장/절 제목 '숫자(-숫자)*.' 정규식과의 충돌 회피"

key-files:
  created:
    - Document/Manual/DataMeasurement_Operator_Manual_v1.0.md
    - Document/Manual/DataMeasurement_Operator_Manual_v1.0_Screenshot_Checklist.md
    - Document/Manual/_build_operator_manual_docx.py
    - Document/Manual/DataMeasurement_Operator_Manual_v1.0.docx
  modified: []

key-decisions:
  - "본문 조작 절차의 번호 목록은 '1. 2. 3.' 대신 원 안 숫자(①②③...)를 사용 — 장/절 제목이 'N. 제목' / 'N-M. 제목' 형식이라 겹치는 것을 방지"
  - "원본 DDA 매뉴얼(_docx_text.txt/_pptx_text.txt/Equipment_Overview.md)은 장/절 번호 스타일과 톤 참고만 하고 내용은 전부 현재 코드 근거로 새로 작성"
  - "MainWindow.xaml의 [Tray 비전]/[Bottom 비전] 탭은 존재를 짧게 언급만 하고 조작법은 다루지 않음 (CONTEXT.md 범위=검사 워크플로우, 별도 정렬 비전 서브시스템은 범위 밖)"

requirements-completed: [DOC-OP-01]

duration: 26min
completed: 2026-08-18
---

# Quick Task 260818-el1: DataMeasurement 운영자 Word 매뉴얼(처음부터 작성) Summary

**현재 코드 기준(로그인 미필요=Admin/Engineer 2등급뿐, 실제 화면 문구·버튼명 전량 확인)으로 8장+부록 2개, 그림 자리표시자 36개짜리 신규 운영자 매뉴얼(.md→.docx)과 1:1 대응 스크린샷 캡처 체크리스트, 재생성 가능한 python-docx 빌드 스크립트를 새로 작성했다. 원본 DDA(Wafer/Die 기반) 매뉴얼 내용은 옮겨 적지 않았다.**

## Performance

- **Duration:** 약 26분
- **Started:** 2026-08-18T01:45:16Z
- **Completed:** 2026-08-18T02:11:06Z
- **Tasks:** 3 (원고 작성 / 캡처 체크리스트 / .docx 빌드)
- **Files created:** 4

## Accomplishments

- `WPF_Example/` 소스 코드(MenuBar, LoginManager, InspectionListView, MainView, OpenRecipeWindow, ReviewerWindow, StatisticsWindow, LightHandlerWindow, DeviceSelector, TcpServerWindow, SystemSetting, Resources.ko-KR.resx 등)를 읽기 전용으로 직접 확인해서, 실제 버튼 문구·메뉴 항목·안내 메시지 원문(예: "로그인 실패!", "시스템이 Running 중입니다.", "접근 거부됨")을 그대로 인용한 매뉴얼 본문 작성.
- 8장(시작하기 전에 / 화면 구성 / 로그인 / 레시피 / 검사 실행 / 결과 확인 / 조명·카메라 / 문제 해결) + 부록 2개(용어 설명 / 일상 점검 체크리스트) 고정 목차 100% 반영.
- "Operator 계정"이 실제로는 존재하지 않는다는 사실(`LoginManager.EAccountGrade` = Admin/Engineer뿐, 미로그인 시 표시되는 "OPERATOR"는 계정이 아니라 기본 표시 문구일 뿐)을 코드로 확인해 정확히 반영 — 존재하지 않는 개념을 지어내지 않음.
- 로그인 필요/불필요 화면을 코드(`MenuBar.IsEditable`, `InspectionListView.IsEditable` setter)로 직접 추적해 절 단위로 정확히 구분 (예: 상단 메뉴 [RECIPE]는 로그인 불필요, 좌측 패널 "..." 버튼과 [CAMERA]/[LIGHT]/[CONNECT]/[SETTING]은 로그인 필요).
- 그림 자리표시자 36개(요구 최소 25개 초과) — 장별 연속 번호, 전부 `> 캡처 대상:` 설명 포함.
- 캡처 체크리스트가 원고와 그림 번호 순서·집합 정확히 1:1 대응(자동 검증 통과).
- python-docx 빌드 스크립트가 한글 폰트(맑은 고딕 + eastAsia), 표지, Word 자동 목차 필드(TOC \o "1-3"), Heading 1~3, 회색 그림 자리표시자 표, 마크다운 표 변환을 전부 구현하고 실행 성공(Heading 1=12개, Heading 2=38개).
- 관리자/엔지니어 전용 기능(ROI 그리기, 패턴 모델 생성, Datum 티칭, 캘리브레이션, 계정 관리, 조명 포트 설정, TCP 원시 명령 전송 등)은 조작법을 쓰지 않고 "관리자 전용"으로만 안내 — 코드 구조·클래스명·내부 알고리즘 용어(HALCON, HImage, SequenceBase, TopInspectionAction 등)와 구형 설비 개념(Wafer, Die, Map Matching, Scrib, BIN)은 0건.

## Files Created

- `Document/Manual/DataMeasurement_Operator_Manual_v1.0.md` — 매뉴얼 본문 원고(단일 소스), 502줄, 그림 자리표시자 36개
- `Document/Manual/DataMeasurement_Operator_Manual_v1.0_Screenshot_Checklist.md` — 캡처 체크리스트, 106줄, 그림 36개 1:1 대응, 체크박스 36개
- `Document/Manual/_build_operator_manual_docx.py` — 원고(.md) → .docx 변환 스크립트(재실행 가능, 442줄)
- `Document/Manual/DataMeasurement_Operator_Manual_v1.0.docx` — 최종 Word 문서(50KB, Heading 1×12 / Heading 2×38 / 표 37개[그림 36 + 부록 표 1])

## Decisions Made

- **번호 목록에 "1. 2. 3." 대신 원 안 숫자(①②③...) 사용**: 장/절 제목이 "N. 제목"/"N-M. 제목" 형식이라, 본문에 일반 숫자형 번호 목록("1. 클릭합니다")을 쓰면 `.docx` 빌드 스크립트의 장 제목 인식 정규식과 줄 시작 패턴이 겹쳐 절차 문장이 엉뚱하게 Heading 1로 렌더링될 위험이 있었다. 계획서에 예시로 제시된 "① ~을 클릭합니다 → ② ..." 형식을 본문 전체의 번호 목록 표준으로 채택해 이 충돌을 원천 차단했다. 스크립트 상단 주석에 설계 근거를 남겨 향후 원고 수정 시 실수로 "1. 2. 3."을 쓰지 않도록 안내했다.
- **Tray/Bottom 비전 탭 언급 범위 최소화**: `MainWindow.xaml`을 읽다가 [검사] 탭 외에 [Tray 비전]/[Bottom 비전] 탭이 실제로 존재하는 것을 확인했다(별도 정렬 비전 서브시스템, v1.3). CONTEXT.md의 범위가 검사 워크플로우(로그인/레시피/검사실행/결과확인)로 한정되어 있고 고정 목차에도 해당 챕터가 없어, 2-2절에서 탭이 존재한다는 사실만 정확히 언급하고 조작법은 다루지 않았다(범위 확대 방지, 동시에 "탭이 하나뿐이다"라고 잘못 기술하지 않도록 사실 왜곡도 방지).
- **레시피 저장 시 "Cross-Z 설정 혼재" 오류는 세부 설명 없이 존재만 안내**: `SaveRecipe()`의 `FindMixedCrossZShots` 관련 오류 메시지는 z_index 개념을 알아야 이해되는 엔지니어 수준 내용이라, "레시피 구성을 바꾼 관리자에게 확인 요청"으로만 안내하고 원문 그대로 옮기지 않았다(코드 구조 설명 금지 규칙 준수).

## Deviations from Plan

None - 계획대로 실행했다. Rule 1~4에 해당하는 버그 수정·긴급 보완·아키텍처 변경은 없었다(문서 전용 작업이라 코드에 손대지 않음).

## Verification

- Task 1 자동 검증: PASS (그림 36개, 502줄, 장별 최소 개수 전부 충족, 금지 용어 0건, 고정 목차 8장+부록2 전부 포함)
- Task 2 자동 검증: PASS (체크리스트 그림 36개 원고와 순서·집합 1:1 대응, 106줄, 체크박스 36개)
- Task 3 자동 검증: PASS (Heading 1=12, Heading 2=38, docx 그림 자리표시자=원고와 정확히 일치, TOC 필드 존재, 맑은 고딕 eastAsia 폰트 확인, TODO/TBD/Lorem/FIXME 0건)
- `git status --porcelain WPF_Example/` — 세션 시작 전부터 있던 `DatumMeasurement.csproj` 외 변경 0건 확인 (소스 코드 무변경)
- 앱 빌드/실행/화면 캡처 0회 (하드웨어 미접촉)
- git commit 0회 (오케스트레이터가 커밋 처리 예정)

## `[확인 필요: ...]` 로 남긴 항목

없음 — 본문에 등장하는 모든 화면 문구·동작은 코드에서 직접 확인 후 작성했다. 확인이 애매했던 항목(Save/Copy/Delete의 로그인·등급 요구조건, RUN 시 오프라인 모드 확인창, 일괄검사 오류 문구 등)은 전부 해당 `.xaml.cs` 소스를 직접 읽거나 `Resources.ko-KR.resx`에서 실제 한글 문자열을 대조해 확정했다.

## User Setup Required

이 작업 자체는 추가 설정이 필요 없다(문서 산출물만 생성). 다만 문서를 실사용하려면 아래 후속 작업이 필요하다.

**총 캡처 필요 장수: 36장** (사용자가 직접 장비 앞에서 촬영해야 하는 화면 수).

1. `Document/Manual/DataMeasurement_Operator_Manual_v1.0_Screenshot_Checklist.md` 를 보고 그림 36개를 순서대로 캡처한다. (사전 준비 절에 로그인 필요/불필요 화면 구분과 검사 결과 준비 방법이 정리되어 있다.)
2. `DataMeasurement_Operator_Manual_v1.0.docx` 를 Word 로 열어, 같은 번호가 적힌 회색 상자를 찾아 캡처한 이미지로 교체한다.
3. 목차 위에서 마우스 우클릭 → "필드 업데이트"(또는 목차 클릭 후 F9)를 실행해 실제 페이지 번호를 채운다.
4. 표지의 `[회사명]` / `[연락처]` 자리표시자를 실제 값으로 채운다.
5. (선택) 원고(`.md`)를 수정한 경우 `python Document/Manual/_build_operator_manual_docx.py` 를 다시 실행하면 `.docx` 가 최신 내용으로 재생성된다. 이때 회색 자리표시자는 초기화되므로, 이미 붙여넣은 캡처 이미지는 재실행 전에 별도 백업해 두어야 한다.

## Next Phase Readiness

- 문서 산출물은 캡처 이미지 삽입 전까지는 "초안" 상태이며, 위 후속 작업 1~4가 끝나야 실사용 가능한 최종 매뉴얼이 된다.
- 이후 화면 문구가 바뀌면(신규 phase 진행 등) 원고(`.md`)만 수정하고 빌드 스크립트를 재실행하면 되므로, 유지보수 부담은 낮다.
- 오케스트레이터가 커밋 및 STATE.md/ROADMAP.md 갱신을 처리한다.

---
*Phase: quick-260818-el1*
*Completed: 2026-08-18*

## Self-Check: PASSED

- FOUND: Document/Manual/DataMeasurement_Operator_Manual_v1.0.md
- FOUND: Document/Manual/DataMeasurement_Operator_Manual_v1.0_Screenshot_Checklist.md
- FOUND: Document/Manual/_build_operator_manual_docx.py
- FOUND: Document/Manual/DataMeasurement_Operator_Manual_v1.0.docx
- Task 1 automated verify: PASS (그림 36개, 502줄)
- Task 2 automated verify: PASS (그림 36개 1:1 대응, 106줄, 체크박스 36개)
- Task 3 automated verify: PASS (H1=12, H2=38, 그림=36, TOC 필드 확인, 맑은 고딕 폰트 확인, 미완성 표시 0건)
- `git status --porcelain WPF_Example/` PASS (사전 존재 DatumMeasurement.csproj 외 변경 0건)
- No commits made (as instructed — orchestrator handles staging/commit)
