# Quick Task 260805-mze: 일괄검사 동시실행 크래시 방지 — Btn_batchRun_Click 전역 IsIdle 복원 - Context

**Gathered:** 2026-08-05
**Status:** Ready for planning (원인 확정됨 — 재조사 불필요)

<domain>
## Task Boundary

Phase 69(commit `ca88862`)가 `InspectionListView.xaml.cs`의 RUN 진입점 4곳을 전역 `Sequences.IsIdle` 대신 시퀀스 단위 `TryGetBlockingSequence`로 교체했다. 이 중 **`Btn_batchRun_Click`(일괄검사 버튼)만** 실사용자 테스트에서 프로세스 크래시를 유발함이 확인됐다 — **`Btn_start_Click`(단일 RUN)은 동일 방식으로 문제없이 동작 확인됨(Test 1 PASS).**

**확정된 원인**: `InspectionListView.xaml.cs:32-35`의 `_batchService`(`BatchRunService`)/`_batchShots`/`_batchAccumulated`가 **시퀀스별로 분리되지 않은 단일 공용 필드**다. BOTTOM 일괄검사가 실행 중인 상태에서 (물리 카메라를 공유하지 않는) TOP 일괄검사를 시작하면, `TryGetBlockingSequence`는 "차단 안 함"으로 정확히 판정하지만, 그 뒤 `_batchService = new BatchRunService();`(:601)이 BOTTOM이 아직 쓰고 있는 참조를 TOP 것으로 덮어써 버려 크래시로 이어진다. 사용자가 실기(BOTTOM 일괄검사 도중 TOP 일괄검사 시작)로 재현 확인함.

</domain>

<decisions>
## Implementation Decisions (LOCKED — 이미 git diff로 원본 확인 완료)

### 되돌릴 지점: `Btn_batchRun_Click`의 메인 차단 체크 단 하나
`InspectionListView.xaml.cs`에서 Phase 69(`ca88862`)가 아래처럼 바꾼 부분을 **원래대로 되돌린다**:

```csharp
// 현재 (Phase 69, 문제 있음)
string sBlockingSeqName;
if (SystemHandler.Handle.Sequences.TryGetBlockingSequence(seqID, out sBlockingSeqName)) {
    CustomMessageBox.Show("일괄 검사",
        string.Format(
            "실행할 수 없습니다 — '{0}' 시퀀스가 아직 Idle 이 아닙니다.\n(자기 자신이거나, 같은 물리 카메라를 공유하는 시퀀스입니다.)",
            sBlockingSeqName),
        MessageBoxImage.Error);
    return;
}
```

**되돌릴 원래 코드** (git show ca88862 의 diff에서 확인한 정확한 원문):
```csharp
if (!SystemHandler.Handle.Sequences.IsIdle) {
    CustomMessageBox.Show("일괄 검사", "시퀀스가 이미 실행 중입니다.", MessageBoxImage.Error);
    return;
}
```

### 되돌리지 않는 것 (LOCKED)
- **`Btn_start_Click`(단일 RUN)의 `TryGetBlockingSequence` 체크는 그대로 유지한다.** 이건 `_batchService` 등 공용 필드를 전혀 안 쓰고, 사용자 실기 테스트(Test 1)로 TOP+BOTTOM 동시 단일 RUN이 크래시 없이 정상 동작함을 이미 확인했다.
- **일괄검사 진입부의 lazy-rebuild 게이트**(`SystemHandler.Handle.Sequences.GetSequenceState(seqID) == EContextState.Idle`, 위 메인 체크보다 뒤에 위치)는 **건드리지 않는다.** 메인 체크를 전역으로 되돌리면 다른 시퀀스가 바쁠 때 이미 그 앞에서 return 되므로, 이 rebuild 게이트는 크로스-시퀀스 상황에서 도달 자체가 안 된다 — 그대로 둬도 안전하고, 되돌릴 이유가 없다.
- **`_batchService`/`_batchShots`/`_batchAccumulated`를 시퀀스별로 분리하는 근본 수정은 이번 범위 밖이다.** 이건 훨씬 큰 작업(공용 필드를 Dictionary<ESequence, ...> 등으로 재설계)이라 별도 task로 남긴다 — 지금은 "크로스-시퀀스 일괄검사 동시 실행 자체를 막아서" 안전하게 만드는 것이 목표.

</decisions>

<specifics>
## Specific Ideas

- 정확한 위치: `InspectionListView.xaml.cs`, `Btn_batchRun_Click` 메서드 내부, `checkedShots`/D-02(같은 시퀀스만 선택 가능) 검증 직후, `inspSeq` 해석 이전.
- 참고: `git show ca88862 -- WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` 로 정확한 before/after diff 확인 가능.

</specifics>

<canonical_refs>
## Canonical References

No external specs — 사용자 실기 재현 + git diff로 원인/원복 대상 모두 확정됨.

</canonical_refs>
