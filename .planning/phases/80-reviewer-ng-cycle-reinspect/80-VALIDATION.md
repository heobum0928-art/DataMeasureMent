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

> plan 확정 후 task ID 로 갱신. 현재는 결정(D-80-xx) 단위 매핑.

| Decision | Behavior | Test Type | Automated Command / Check | Status |
|----------|----------|-----------|---------------------------|--------|
| D-80-01/02 | 리뷰어 버튼 1개(NG 원인 패널 아래), 비활성 시 이유 한 줄 | 정적 + 수동 | ReviewerWindow.xaml 에 버튼 1개 + VM 바인딩(IsEnabled/이유 문자열) 존재 grep | ⬜ pending |
| D-80-07/08/19 | 같은 자재 Shot·기준점·Z 후보 사진 수집, 기준점 일부 결손 = 없음 | 정적 + 수동 | 신규 서비스가 `SavedCycleRerunPlanner` 경유(재구현 없음) grep, 실데이터 폴더 대조 | ⬜ pending |
| D-80-09 | 기준점 사진 없음 → 알림 + Shot 만 + 자동 Test Find 안 함 | 수동 UAT | 수동 검사 기록으로 재현 | ⬜ pending |
| D-80-10 | 저장 시 파라미터만, 사진 경로는 원래 값 | 정적 + 수동 | 저장 후 레시피 INI 의 SimulImagePath/TeachingImagePath 가 원래 값인지 확인 | ⬜ pending |
| D-80-11/13 | 해제 4경로([해제]/PLC Test/레시피 변경/종료) + OfflineInspectMode 복원 | 정적 + 로그 | 각 훅 지점 호출 grep, 로그 1줄씩 | ⬜ pending |
| D-80-12/14 | 상태 줄 1줄(+JPG 안내, [해제]) VM 바인딩 | 정적 + 수동 | 문자열이 VM 에서 생성되는지 grep(View 계산 없음) | ⬜ pending |
| D-80-06 | 자동 Test Find 가 AskTestImageSource 대화상자를 거치지 않음 | 정적 + 수동 | 자동 경로에서 AskTestImageSource 미호출 | ⬜ pending |
| 함께 처리 2 | 수동 RUN 에서 Local Ref ROI 수정 → 재계산, PLC 자동 경로 불변 | 정적 + 로그 | 재계산 분기가 비-PLC 조건으로 가드됨 grep, 로그 확인 | ⬜ pending |
| 함께 처리 1 | 리뷰어 전체 보기 선 겹침 수정 | 수동 | 다중 Shot 사이클에서 표시 사진의 Shot 선만 | ⬜ pending |
| D-80-15 | 기준 ROI 시험 찾기(z1 배경 + 선 표시) | 수동 UAT | 런타임과 같은 ComputeLocalRefLine 사용 grep | ⬜ pending |
| D-80-16/17 | 가독성 규칙·MVVM | 자동 | grep 5종 0, code-behind 추가분은 배선 1줄 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `NodeViewModel.cs` 전체 재확인 — `Param`/`IsSelected`/`IsExpanded` 시그니처 (트리 프로그램 선택용)
- [ ] 수동 검사(IsProtocolDriven=false) 사이클 폴더 1개 + PLC 자동 사이클(기준점 사진 포함) 폴더 1개 확보 — UAT 재현 데이터

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
