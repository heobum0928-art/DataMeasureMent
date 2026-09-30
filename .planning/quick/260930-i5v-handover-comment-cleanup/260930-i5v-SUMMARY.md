---
quick_id: 260930-i5v
slug: handover-comment-cleanup
status: complete
date: 2026-09-30
commits: [31c03426, 5527de6e, 63497f4e, 6fdb0585, d2958701, c80a8acd, 014e73f8]
tag: before-comment-cleanup
---

# 인계 대비 주석 정리 (기능 영향 0) — 요약

## 한 일

1. **태그** `before-comment-cleanup`(c72c9c5b) — 정리 전 주석을 언제든 꺼내볼 수 있는 기준점. 내부 저장소의 `.planning/` 과 커밋 기록은 삭제하지 않음.
2. **C# 주석 정리** (122개 파일, 주석 약 2,380개) — C# 렉서로 실제 주석 토큰만 수정.
   - 제거: Phase/Plan/Wave/Task 번호, 결정 코드(D-/CO-/T-/WR-/AV-/SZF-/REGR- 등), quick id, `.planning`·`*.md` 참조, UAT, "Claude's Discretion"
   - 서명: 주석 머리를 `//YYMMDD hbk` 로 통일(사용자 요청). 원래 서명이 있으면 그 날짜 유지, 없으면 해당 줄의 git blame 작성일 사용. 이어지는 줄·`///` 문서 주석에는 붙이지 않음
   - 태그가 문장의 주어였던 33곳은 손으로 문장을 다듬음
3. **XAML·csproj 주석 정리** — XAML 은 줄 수를 유지(WPF Debug BAML 의 줄 정보 보존). csproj 는 로컬 SIMUL 설정을 커밋하지 않도록 인덱스에만 정리본을 올림.
4. **실행 문자열 정리** — 로그 태그 `[Phase70]`/`[Phase40]` → `[SEQ]`, `[Phase 61]` → `[ALIGN]`, 구 레시피 안내 팝업, 레시피 경고 로그(내부 변환 스크립트 경로 제거), Obsolete 문구 4, 버전 이력 3, region 이름 1. 이 문자열을 읽어서 분기하는 코드 없음(출력 전용).
5. **문서** — `LOG_GUIDE.md`, `Branch_Debug_Map` html 의 작업 번호를 날짜로 교체.
6. **인계용 저장소 뽑기 스크립트** `scripts/export_handover.sh`
   - `git archive` 로 지정 커밋을 풀고 `.planning/`, `.claude/`, `CLAUDE.md`, 이 스크립트, `scripts/migrate_phase73_recipe.py`(끝난 일회성 도구) 제외
   - `.gitignore` 의 개발 도구 블록 제거, 커밋 1개짜리 새 저장소 생성(작성자 = 로컬 git 설정), `core.longpaths` 켬
   - 원본 저장소는 읽기만 함(HEAD·작업폴더 무변경 확인)

## 검증

| 항목 | 결과 |
|---|---|
| 소스 | 변경된 .cs 전부 주석 외 코드 토큰이 태그와 동일(3차 문자열 수정 전 기준) |
| 빌드 | Debug|x64 Rebuild error 0, 경고 20줄(CS0618×16, CS0162×2, CS0169×2) — 정리 전과 동일 |
| IL | ildasm 덤프에서 문자열 상수·데이터 RVA·`PrintErrLog [CallerLineNumber]` 줄번호만 가리면 397,504줄 차이 0 |
| 바뀐 문자열 | ldstr 12개(로그 10, 팝업 1, 경고 1) + Obsolete 4 + 버전 이력 3 — 모두 의도한 것 |
| 리소스 | `g.resources`(BAML)·Win32 res·아이콘 바이트 동일 |
| 인계본 | `.planning`/`.claude`/`CLAUDE.md`/`scripts` 없음, 커밋 1개, Co-Authored 0, 텍스트 파일 흔적은 `ERecipeFormatVersion.Phase5/Phase6`·`LoadPhase6Format`(코드 식별자, 유지)뿐 |

## 남긴 것(의도)

- `ERecipeFormatVersion { Phase5, Phase6 }`, `LoadPhase6Format()` — 레시피 형식 버전 이름. 식별자 변경은 코드 변경이라 유지.
- 내부 저장소의 커밋 메시지·`.planning/` — 추적용으로 보존. 인계본은 스냅숏 1개라 포함되지 않음.
- 긴 설명형 주석(8줄 이상 83덩어리)의 문체 자체는 손대지 않음 — 흔적 토큰만 제거.

## CLAUDE.md 와의 충돌

CLAUDE.md 는 "날짜 주석(`//YYMMDD hbk`) 신규 금지(2026-06-11)" 규칙과 `grep -cF 'hbk'` = 0 검증을 요구한다.
이번 작업은 사용자 지시("주석도 내가 한 것처럼 날짜랑 넣어, //260930 hbk")에 따라 서명을 유지·부여했다. CLAUDE.md 규칙 갱신 여부는 사용자 결정 대기.

## 후속 (db1bb007)

1차 정리 뒤 남은 것을 사용자 확인 후 추가로 정리했다.

- **빠졌던 작업 번호**: `LSR-`(약 40), `NGA-`(30), `PR-`, `N5C-`, `REQ-`, `E1L-`, `ALI-`, `D-F3W-*`, `Part A/B:`, `M13:`,
  서명 뒤 `#N`(약 40), 서명 뒤 원 번호 결정 표시 `①:`·`②:`(약 40), 소문자 `plan NN`.
  원인: 코드 접두어를 목록으로 관리해서 목록에 없는 접두어가 빠졌다. 이번에는 주석 전체에서
  `대문자-숫자` 형태를 모두 뽑아 접두어를 확인한 뒤 작업 번호만 골라 넣었다.
- **태그를 뺀 자리의 조각** 약 160곳: `( 무중단)`, `불가 .`, `(: …)`, `(… —)`, `<summary>:` 등은 규칙으로 정리.
  `(폴백 = 동작)`, `— /통합 확정`, `Bottom 은 이후 항상`, `plan 03 이 …` 처럼 뜻이 빠진 23곳은 직접 문장을 고쳤다.
- **유지**: 단계 목록·화면 버튼을 가리키는 원 번호(`(①②③)`, `① 원본 매칭 → ②…`), 다른 파일 줄 번호 참조(`(:442-453)`),
  NG 원인 코드 `R5`/`R8`, FAI 이름 `A1-23`, `UTF-8`.
- 검증: 빌드 error 0 / 경고 20줄 동일, 가린 IL 차이 0, 직전 커밋 대비 문자열 변화 0, 리소스 바이트 동일.
