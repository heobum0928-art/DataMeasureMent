---
phase: 80
slug: reviewer-ng-cycle-reinspect
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-09-18
---

# Phase 80 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> 단위 테스트 프레임워크 없음(프로젝트 정책). 자동 검증 = 빌드 + CLAUDE.md grep 5종 + 정적 소스 단언. 동작 검증 = 장비 PC 수동 UAT(Release 빌드만 — Debug 는 라이선스 키 없음).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | 없음 (xUnit/NUnit/MSTest 미도입) |
| **Config file** | none |
| **Quick run command** | `"/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -m -nologo -v:minimal -clp:ErrorsOnly` |
| **Full suite command** | Quick run + 신규·수정 파일마다 CLAUDE.md grep 5종(삼항 `\?[^\?]*:` · `??` · `?.` · `switch.*=>` · `hbk`) 전부 0 |
| **Estimated runtime** | ~60–120 seconds |

---

## Sampling Rate

- **After every task commit:** Quick run command (빌드 오류 0) + 그 task 가 건드린 파일 grep 5종
- **After every plan wave:** Full suite command
- **Before `/gsd-verify-work`:** Full suite green + 아래 수동 UAT 목록 준비
- **Max feedback latency:** 120 seconds

---

## Per-Task Verification Map

> plan 확정(2026-09-18) — task ID 매핑. 모든 task 의 `<automated>` = Debug|x64 빌드 + 배선/회귀 grep + 가독성 grep(새 파일 전체, 기존 파일은 더한 줄). 기준 해시는 `/c/Users/admin/AppData/Local/Temp/p80/base-80-*.txt`.

| Task | Decision | Behavior | Test Type | Automated Command / Check | Status |
|------|----------|----------|-----------|---------------------------|--------|
| 80-01-T1 (tracer) | D-80-00/01/02/03/12/13/17/18 | 리뷰어 버튼 → VM → 서비스 → 부품 진입점 → Shot 사진·OfflineInspectMode → 상태 줄 + [해제] | 정적 | chain_* grep, handler 1문장, csproj 2 | ⬜ pending |
| 80-01-T2 | D-80-10/11 | 저장 시 원래 경로, PLC·레시피 변경·종료 자동 해제 | 정적 | save_wrap, plc_order_ok, closing_order_ok | ⬜ pending |
| 80-02-T1 | 함께 처리 2 | 수동 RUN stale 재계산(TeachingImagePath 파일), PLC 가드 | 정적 | first_stmt_guard, same[...] 7개 md5 | ⬜ pending |
| 80-02-T2 | D-80-06/15/18 | 대화상자 없는 공용 Test Find + 시험 찾기 서비스 | 정적 | compose/single/hold/busy, dialog_free=0 | ⬜ pending |
| 80-02-T3 | D-80-15/00 | 기준 ROI 시험 찾기 버튼 + 라벨 결과 | 정적 | same[BtnTestFindDatum_Click]=1, mv_dialog=0 | ⬜ pending |
| 80-03-T1 | D-80-07/19 | 같은 자재 묶기(GroupIntoParts) + 완전성 판정 + 재검사 상호 배제 | 정적 | group/collect/fromdates, same[...] 9개, deleted_vs_phase=0 | ⬜ pending |
| 80-03-T2 | D-80-07/08/09/12/14 | 기준점(완전할 때만)·두 장짜리·Z·버퍼·리뷰어 선·알림·안내 | 정적 | complete_call, flag_in_apply, alert_call | ⬜ pending |
| 80-03-T3 | 함께 처리 1, D-80-09 | 전체 보기 선 겹침 수정 + 알림 배선 | 정적 | dc_collect, dc_selectmany=0, same[...] 6개 | ⬜ pending |
| 80-04-T1 | D-80-04/05 | 리뷰어 최소화·복원, 측정 노드 선택, 캐시 잊기 | 정적 | select_def/deferred/reselect, nav_* | ⬜ pending |
| 80-04-T2 | D-80-06 | 자동 Test Find(완전할 때만, 캐시 비운 뒤, 시퀀스 시작 없음) | 정적 | order_ok, no_seq_start=0 | ⬜ pending |
| 80-04-T3 | D-80-05/00 | 측정 노드 RUN, 리뷰어 사용 중 오프라인 확인창 생략 | 정적 | resolve_before_check, same_resolve_runnable | ⬜ pending |
| 80-05-T1 | D-80-00/16/17/18, 회귀 0 | 누적 감사 + 버전 1.7.51.0 | 정적 | audit-80.txt (UI 3개·대화상자 1·md5 14개) | ⬜ pending |
| 80-05-T2/T3 | 전부 | 장비 PC Release UAT U-1~U-11 | 수동 | 80-HUMAN-UAT.md | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `NodeViewModel.cs` 전체 재확인 — `Param`(Node.ParamData)/`IsSelected`/`IsExpanded`/`Parent`/`Children`/`ExpandParents()` 존재 확인 (계획 단계 2026-09-18)
- [ ] 수동 검사(IsProtocolDriven=false) 사이클 폴더 1개 + PLC 자동 사이클(기준점 사진 포함) 폴더 1개 확보 — UAT 재현 데이터 (장비 PC, 80-05 U-2/U-3)

---

## Manual-Only Verifications

| Behavior | Decision | Why Manual | Test Instructions |
|----------|----------|------------|-------------------|
| 버튼 1회로 사진 전부 불러오기 + Shot/FAI 선택 + 자동 Test Find | D-80-01..08 | WPF UI·실사진 필요 | 리뷰어 → PLC 자동 NG 행 → 버튼 → 메인 화면 확인 |
| 기준점 없음 알림 | D-80-09/19 | 실데이터 필요 | 수동 검사 기록 행으로 버튼 |
| 저장 보호·해제 | D-80-10/11 | 레시피 파일·PLC 필요 | 불러온 뒤 파라미터 수정·저장 → INI 확인, [해제]/PLC Test/레시피 변경/종료 각각 |
| Local Ref 재계산·시험 찾기 | 함께 처리 2, D-80-15 | 실사진 필요 | Local Ref ROI 이동 → RUN → 재계산 로그, 시험 찾기 선 표시 |
| 리뷰어 선 겹침 | 함께 처리 1 | 육안 | 다중 Shot 사이클 전체 보기 |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
