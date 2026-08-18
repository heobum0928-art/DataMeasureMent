---
phase: quick-260818-fqx
plan: 01
subsystem: docs
tags: [operator-manual, teaching, datum, pattern-matching, align, docx, screenshot-checklist]

# Dependency graph
requires:
  - phase: quick-260818-el1
    provides: "현장 운영자용 Word 매뉴얼 원본(1~8장 + 부록 A/B), 그림 자리표시자/캡처 체크리스트 형식, 빌드 스크립트"
provides:
  - "9장(티칭) — Datum / 패턴(ModelFinder) / Align(Tray+Bottom) 3종 티칭 실제 절차 원고"
  - "9장 캡처 체크리스트 22항목 + 총 캡처 장수 갱신(36→58)"
  - "재생성된 .docx(9장 포함, Heading 1/2/3 구조 검증 완료)"
affects: [operator-manual-future-revisions]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "원고 삽입은 항상 `부록 A. 용어 설명` 앵커 앞 Edit(prefix/suffix 완전 일치 자동검증) — 새 장 추가 시 재사용 가능한 패턴"

key-files:
  created: []
  modified:
    - Document/Manual/DataMeasurement_Operator_Manual_v1.0.md
    - Document/Manual/DataMeasurement_Operator_Manual_v1.0_Screenshot_Checklist.md
    - Document/Manual/DataMeasurement_Operator_Manual_v1.0.docx

key-decisions:
  - "Align(Tray/Bottom) 화면은 코드 확인 결과 로그인 체크가 없어 '로그인 필요 없음'으로 정확히 기재(계획의 단순화 가정과 다름)"
  - "Datum AlgorithmType은 코드상 4종(TwoLineIntersect 포함)임을 확인해 9-2절 표에 4종 모두 반영"
  - "Bottom 피커센터 캘리브레이션(36-스텝) 절차는 창작하지 않고 경고 문단 + 버튼 나열 + [확인 필요]로 남김"

requirements-completed: [DOC-OP-02]

# Metrics
duration: ~65min (정확한 시작 시각 미기록 — 실행 로그 기준 근사치)
completed: 2026-08-18
---

# Quick Task 260818-fqx: 운영자 매뉴얼 "티칭" 챕터 추가 Summary

**기존 운영자 매뉴얼(1~8장, 이미 출하됨)에 9장(티칭)을 8장 뒤·부록 A 앞에 삽입 — Datum 4종 알고리즘/패턴(ModelFinder) 2-패턴 모델/Align(Tray+Bottom, 6슬롯) 3가지를 실제 버튼·안내창 문구 기반 ①②③ 절차로 작성하고, 캡처 체크리스트 22항목 추가 + .docx 재생성까지 완료.**

## Performance

- **Duration:** 약 65분 (세션 시작 시각을 별도로 기록하지 않아 근사치 — CONTEXT/PLAN 완성 이후 이 실행 에이전트가 소비한 시간 기준)
- **Completed:** 2026-08-18T05:19:04Z
- **Tasks:** 3/3 (Task 1: 9-1~9-3 작성, Task 2: 9-4~9-6 작성, Task 3: 체크리스트+docx+전체 회귀검증)
- **Files modified:** 3 (원고 .md, 체크리스트 .md, .docx) + 참고용 deferred-items.md 1개 신규

## Accomplishments

- 원고 502줄 → 699줄로 확장. 9장이 8장 마지막 줄과 `부록 A. 용어 설명` 사이에 삽입됐고, 1~8장·부록은 `git show HEAD:` 원본과 앞/뒤 완전 일치(자동검증 통과) — 재배치·재번호 0건.
- 기존 그림 36개 번호 그대로 보존, 9장 그림 [그림 9-1] ~ [그림 9-22] 22개가 연속 번호로 추가됨.
- Datum(9-2, 알고리즘 4종 표+① ~ ⑨ 절차+Re-anchor 보너스 설명) / 패턴(9-3, ① ~ ⑨ 절차) / Align 공통·Tray(9-4, ① ~ ⑧ 절차) 3개 절이 모두 그림 4개 이상의 실제 절차 깊이로 작성됨(계획의 "하나만 깊고 나머지 개요 금지" 요건 충족).
- Align Bottom 차이점(9-5, 6슬롯 드롭다운 + 피커센터 패널 경고)과 사후 확인/트러블슈팅(9-6)까지 총 6개 절 완성.
- 캡처 체크리스트에 `### 9장` 섹션(22행 표) 추가, 총 캡처 장수 36 → 58장 갱신, "사전 준비"에 9장 캡처 안내(백업 선행 포함) 및 로그인 필요/불필요 화면 목록을 사실대로 갱신. 1~8장 표 영역은 원본과 완전 일치(자동검증 통과).
- `.docx` 재생성 완료 — Heading 1=13 / Heading 2=44 / Heading 3=0, 원고에서 계산한 기대값과 정확히 일치. 그림 자리표시자 58개(9장 22개 포함) 원고와 1:1. TOC 필드·맑은 고딕 폰트 확인됨.
- `_build_operator_manual_docx.py`는 **수정하지 않음** — 계획이 사전 확인한 대로 9장 문법(`9. `→H1, `## 9-N.`→H2, `[그림 9-M]`→회색 자리표시자)이 기존 정규식으로 그대로 처리됨.
- `WPF_Example/` 신규 변경 0건(세션 시작 시점에 이미 존재하던 `DatumMeasurement.csproj` 변경만 남아 있음, 이번 태스크가 만든 변경 아님). 앱 빌드/실행 0회, 화면 캡처 0회, git commit 0회(오케스트레이터가 처리).

## Task Commits

**커밋 없음.** 이 실행은 오케스트레이터 지시에 따라 태스크 단위 커밋을 생략했다(`git commit` 자체를 호출하지 않음). `Document/Manual/`의 3개 파일 변경은 워킹 트리에 uncommitted 상태로 남아 있으며, 오케스트레이터가 이후 별도로 스테이징·커밋한다.

## Files Created/Modified

- `Document/Manual/DataMeasurement_Operator_Manual_v1.0.md` — 9장(티칭) 6개 절 삽입(502→699줄). 1~8장·부록 무변경.
- `Document/Manual/DataMeasurement_Operator_Manual_v1.0_Screenshot_Checklist.md` — `### 9장` 캡처 표 22행 추가, 사전 준비/로그인 안내/총 캡처 장수 갱신(106→134줄). 1~8장 표 무변경.
- `Document/Manual/DataMeasurement_Operator_Manual_v1.0.docx` — 위 원고 기준으로 전체 재생성(문단 439개, 그림 자리표시자 58개).
- `.planning/quick/260818-fqx-document-manual-datameasurement-operator/deferred-items.md` — (신규, 참고용) 이번 태스크 범위 밖에서 발견한 5-2절 서술 불일치 1건 기록.

## 9장 절 구성과 그림 개수

| 절 번호 | 제목 | 그림 개수 | 비고 |
|---|---|---|---|
| 9-1 | 티칭을 시작하기 전에 | 3 ([그림 9-1]~[그림 9-3]) | 1-1절 모순 해소 + 권한/실행중 주의 + 레시피 폴더 백업 2가지 방법 |
| 9-2 | Datum(기준좌표) 티칭 | 5 ([그림 9-4]~[그림 9-8]) | AlgorithmType 4종 표 + ①~⑨ 절차 + Re-anchor 참고 |
| 9-3 | 패턴(ModelFinder) 티칭 | 4 ([그림 9-9]~[그림 9-12]) | 패턴 1/2 + 모델 생성 + Recipe Save + ①~⑨ 절차 |
| 9-4 | Align 티칭 — 화면 열기와 공통 절차 (Tray 기준) | 5 ([그림 9-13]~[그림 9-17]) | 탭 진입 + ①~⑧ 절차(Grab/ROI/티칭저장/검사/동축조명) |
| 9-5 | Align 티칭 — Bottom 화면의 차이점 | 3 ([그림 9-18]~[그림 9-20]) | 6슬롯 드롭다운 + 피커센터 패널 경고([확인 필요] 포함) |
| 9-6 | 티칭 후 확인과 자주 겪는 문제 | 2 ([그림 9-21]~[그림 9-22]) | Recipe Save 재확인 + 이름변경 자동 리네임 + 실패 체크리스트 |

**9장 전체 그림 22개.**

## 캡처 장수

- **9장에서 새로 늘어난 캡처 장수: 22장**
- **문서 전체 총 캡처 장수: 58장** (기존 36장 + 신규 22장)
- 사용자는 캡처 체크리스트의 `### 9장` 표(22행)를 순서대로 채우면 됨.

## 사용자 후속 작업 안내

1. 지금 진행 중인 실제 재티칭 작업을 하는 김에, `DataMeasurement_Operator_Manual_v1.0_Screenshot_Checklist.md`의 `### 9장` 목록(그림 9-1 ~ 9-22) 순서대로 화면을 캡처하십시오. Bottom 화면(그림 9-18~9-20)은 슬롯을 하나씩 바꿔가며 촬영해야 6개 슬롯 관련 내용을 채울 수 있습니다.
2. `DataMeasurement_Operator_Manual_v1.0.docx`를 Word로 열어, 같은 번호가 적힌 회색 상자를 찾아 캡처한 이미지로 교체하십시오(회색 상자 클릭 → 이미지로 교체).
3. 목차를 우클릭 → "필드 업데이트"로 페이지 번호를 갱신하십시오(9장이 추가되며 이후 부록 A/B의 실제 페이지 번호가 바뀝니다).
4. **재티칭 전 레시피 폴더 백업을 반드시 먼저 수행하십시오**(9-1절 "재티칭 전 레시피 폴더 전체 백업" 참고 — 앱 안 [RECIPE] → [COPY], 또는 탐색기로 `D:\Data\Recipe\<레시피명>\` 폴더를 통째로 복사).

## `[확인 필요]`로 남긴 항목

1. **Bottom "피커센터 캘 (36-스텝)" 패널의 정확한 조작 순서** (9-5절) — 이 패널은 CONTEXT.md가 확정한 3종 티칭(Datum/패턴/Align)에 포함되지 않는 별개 작업이라 코드로 흐름을 재확인하지 않고 절차를 창작하지 않았습니다. 버튼 이름 나열([초기화]/[검색 ROI(원) 지정]/[Cal 모델 티칭]/[스텝 추가 (Grab+검출)]/[피커센터 계산])과 "별개 작업" 경고만 남겼습니다. 담당 관리자·엔지니어가 별도로 절차를 채워야 합니다.

## 개요 / 1-1절 모순 해소

9-1절 첫 문장에서 "1-1절은 ... 이 매뉴얼에서 다루지 않는다고 안내하며, 이는 1~8장이 현장 운영자를 대상으로 하기 때문이고, 9장이 그 작업 중 Datum·패턴·Align 티칭을 관리자·엔지니어 대상으로 별도로 설명한다"고 명시해, 1~8장을 한 글자도 고치지 않고 모순을 해소했습니다.

## Decisions Made

- **Align 화면 로그인 요구사항 정정** — 계획의 액션 지시문은 "9장 전체가 로그인 필요"를 전제했으나, `TrayVisionView.xaml.cs` / `BottomVisionView.xaml.cs`를 직접 확인(evidence_map이 지시한 Grep 대상)한 결과 두 파일 모두 `IsLogin` 체크가 전혀 없음을 확인했습니다. 9-1절 본문과 체크리스트 "로그인이 필요한 화면" 목록에는 실제로 로그인이 필요한 9-2/9-3(Datum/패턴 — 파라미터 편집기가 로그인해야 활성화됨)만 추가하고, 9-4/9-5(Align)는 "로그인 여부와 관계없이 조작 가능"이라고 정확히 반영했습니다. `hard_constraints` 5번("화면 문구를 지어내지 않는다")과 정확성 우선 원칙에 따른 판단입니다.
- **Datum 알고리즘 4종 전체 반영** — `<already_verified>`에는 CircleTwoHorizontal / VerticalTwoHorizontal / VerticalTwoHorizontalDualImage 3종만 언급돼 있었으나, `EDatumAlgorithm.cs`를 직접 읽어 `TwoLineIntersect`(default, 두 직선 교차 방식)까지 총 4종이 실제 `AlgorithmType` 드롭다운에 존재함을 확인했습니다. 9-2절 표에 4종 전부를 정확히 반영했습니다.
- **Bottom 피커센터 패널은 절차를 만들지 않고 경고로 처리** — 계획이 명시적으로 요구한 안전장치("절차를 지어내지 말 것")를 그대로 따랐습니다.

## Deviations from Plan

### 계획 대비 사실관계 보정 (코드 확인 기반, 창작 아님)

**1. Align 화면 로그인 요구사항**
- **발견 시점:** Task 2 (evidence_map이 지시한 TrayVisionView.xaml.cs/BottomVisionView.xaml.cs Grep 조사 중)
- **내용:** 계획 Task 3 액션 문구는 "9장 전체가 로그인 필요"를 가정했으나 코드에는 로그인 체크가 없음
- **처리:** 9-1절 본문 + 체크리스트 로그인 안내 두 곳 모두 "9-2/9-3만 로그인 필요, 9-4/9-5는 불필요"로 정확히 기재. 계획의 "로그인이 필요 없는 화면 줄은 그대로 둔다" 지시는 그대로 준수(그 줄 자체는 손대지 않음).
- **파일:** `Document/Manual/DataMeasurement_Operator_Manual_v1.0.md`(9-1절), `Document/Manual/DataMeasurement_Operator_Manual_v1.0_Screenshot_Checklist.md`(사전 준비)

**2. Datum 알고리즘 4종(3종→4종) 반영**
- **발견 시점:** Task 1 (evidence_map이 지시한 DatumConfig.cs Grep 조사 중)
- **내용:** `<already_verified>`가 사전 조사한 3종 외에 `TwoLineIntersect`(default) 1종이 코드에 더 존재
- **처리:** 9-2절 표에 4종 전부 기재
- **파일:** `Document/Manual/DataMeasurement_Operator_Manual_v1.0.md`(9-2절)

이 두 건은 버그 수정이 아니라 "코드로 재확인 후 서술" 지시(`<hard_constraints>` 5번, evidence_map)를 충실히 따른 결과 계획의 단순화된 가정보다 더 정확한 사실을 반영한 것입니다. 구조·범위 변경은 없습니다.

### 범위 밖 발견 (수정하지 않음)

**3. 5-2절 "[Light] 버튼" 서술이 현재 코드와 어긋남**
- 5장은 이미 출하된 1~8장 영역이라 `hard_constraints` 4번에 따라 수정 대상이 아닙니다.
- `.planning/quick/260818-fqx-document-manual-datameasurement-operator/deferred-items.md`에 기록만 하고 손대지 않았습니다.

---

**Total deviations:** 2건 사실관계 보정(창작 아님, 구조 변경 없음) + 1건 범위 밖 발견 기록(미수정)
**Impact on plan:** 계획의 골격·요구사항을 전부 충족하면서 코드 확인 결과를 더 정확하게 반영. 스코프 확장 없음.

## Issues Encountered

None — 3개 Task의 자동 검증 스크립트가 모두 첫 시도에 PASS했습니다(재시도 없음).

## User Setup Required

None — 외부 서비스 설정 불필요. 위 "사용자 후속 작업 안내" 4단계(화면 캡처 → docx 이미지 교체 → 목차 필드 업데이트 → 백업)만 필요합니다.

## Next Phase Readiness

- 운영자 매뉴얼이 1~9장 + 부록 A/B 구성으로 완성됐습니다. 스크린샷 58장 삽입 전까지는 초안 상태(회색 자리표시자)입니다.
- 다음 단계는 사용자가 실제 재티칭 작업 중 순서대로 캡처를 진행하는 것이며, 코드/문서 작업은 이 태스크로 종결됩니다.
- 5-2절 [Light] 버튼 서술 정정은 별도 quick 태스크로 후속 처리를 권장합니다(deferred-items.md 참고).

---
*Phase: quick-260818-fqx*
*Completed: 2026-08-18*

## Self-Check: PASSED

- FOUND: `Document/Manual/DataMeasurement_Operator_Manual_v1.0.md`
- FOUND: `Document/Manual/DataMeasurement_Operator_Manual_v1.0_Screenshot_Checklist.md`
- FOUND: `Document/Manual/DataMeasurement_Operator_Manual_v1.0.docx`
- FOUND: `.planning/quick/260818-fqx-document-manual-datameasurement-operator/deferred-items.md`
- FOUND: `.planning/quick/260818-fqx-document-manual-datameasurement-operator/260818-fqx-SUMMARY.md`
- Commits: N/A by design — orchestrator instruction for this run explicitly forbade `git commit`. `git log --oneline -3` confirms HEAD is unchanged (`4798f1e`, pre-existing) and no new commit was created by this executor.
- `git status --porcelain Document/Manual/` shows exactly the 3 expected `M` entries (`.md`, `.docx`, checklist `.md`) — matches plan `<verification>` item 3.
- `git status --porcelain WPF_Example/` shows only the pre-existing `DatumMeasurement.csproj` modification (present before this session started, per initial `gitStatus` context) — matches plan `<verification>` item 4. No new source changes.
- Task 1/2/3 automated `<verify>` scripts all printed `PASS` on first attempt (re-confirmed above in tool output).
