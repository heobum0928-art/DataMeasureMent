# Phase 69: 메인 화면 POC 패널 정리 + RUN 버튼 간헐적 미동작 조사 - Context

**Gathered:** 2026-08-05
**Status:** Ready for planning

<domain>
## Phase Boundary

메인 화면 하단 "POC 자동화 임시 테스트" 패널(z_index 기반 수동 트리거)의 처리 방향을 확정하고, RUN 버튼(`btn_start`/`btn_batchRun`)이 간헐적으로 반응하지 않는 버그의 근본 원인(`SequenceHandler.IsIdle`이 등록된 모든 시퀀스를 하나로 묶어 판정하는 구조)을 하드웨어 안전성을 해치지 않는 범위에서 수정한다.

</domain>

<decisions>
## Implementation Decisions

### D-01: IsIdle 버그 수정 — 시퀀스별 독립 판정 + 물리 카메라 공유 시에만 상호배타 유지 (LOCKED)

**확정된 근본 원인**: `SequenceHandler.StateAll`(`WPF_Example\Sequence\SequenceHandler.cs:87-102`)이 등록된 모든 시퀀스를 순회하며 하나라도 `Running`이면 즉시 전체를 `Running`으로 반환한다. `IsIdle`(:108-111)은 이 `StateAll`을 그대로 쓴다. `InspectionListView.Btn_start_Click`(:387-390)이 이 전역 `IsIdle`로 RUN을 막아, 다른 시퀀스가 도는 중이면 지금 누르려는 시퀀스가 놀고 있어도 "Sequence is already running" 에러로 차단된다.

**중요한 안전성 발견**: `DeviceHandler.cs:221-244`(실HW, non-SIMUL) 확인 결과, `CameraRole=TopBottom`인 PC에서는 CAM_BOTTOM이 별도 카메라 객체를 만들지 않고 이미 열린 CAM_TOP의 `MilCamera` 인스턴스를 그대로 재사용한다(`Devices["CAM_TOP"] == Devices["CAM_BOTTOM"]`, 완전히 같은 참조). 게다가 grab 경로(`MilCamera.GrabHalconImage`/`MdigGrab`)에는 동시 접근을 막는 lock이 전혀 없다. 따라서 IsIdle을 순수하게 시퀀스별 독립 판정으로만 바꾸면, 실HW TopBottom 역할에서 Top/Bottom이 진짜로 동시에 같은 물리 카메라에 grab을 시도할 수 있어 이미지 오염/크래시 위험이 생긴다. (SIMUL_MODE는 `AddVirtualCamera`가 매번 독립 인스턴스를 생성하므로 이 위험이 없다. Cross-Z DualImage 상태(`InspectionSequence`의 `m_dicCrossZImages` 등)는 인스턴스 필드라 이번 수정과 무관.)

**확정된 수정 방향** (사용자 승인, 2026-08-05):
- **실HW에서 물리 카메라를 실제로 공유하는 시퀀스끼리만** 지금처럼 상호배타(하나 돌면 다른 하나 차단) 유지.
- **그 외 경우(SIMUL_MODE 전체, 또는 실HW라도 서로 다른 물리 카메라를 쓰는 시퀀스 조합)는 독립적으로 동시 실행 허용.**
- 판정 방식: 두 시퀀스가 참조하는 카메라 디바이스 객체가 실제로 같은 참조인지(`ReferenceEquals` 또는 동등한 방식)로 "진짜 공유하는지" 확인 — 역할(TopBottom/Side) 문자열 매칭이 아니라 실제 디바이스 공유 여부를 근거로 판정할 것(디바이스 등록 구조가 바뀌어도 안전성이 깨지지 않도록).
- 수정 지점: `InspectionListView.Btn_start_Click`/`Btn_batchRun_Click` 레벨에서 "지금 실행하려는 시퀀스와 카메라를 공유하는 다른 시퀀스만" 확인하는 방식을 우선 검토(전역 `SequenceHandler.IsIdle`/`StateAll` 자체를 바꾸는 것보다 회귀 범위가 좁음 — 다른 호출부(TCP 등) 영향 최소화). 단, 실제 구현 시 `IsIdle`/`StateAll`을 시퀀스 단위로 오버로드 추가하는 것이 더 적절하다고 판단되면 리서치/플래닝 단계에서 근거와 함께 조정 가능.

### D-02: POC 패널 — 지금처럼 유지 (LOCKED)
- 메인 화면 하단의 "POC 자동화 임시 테스트" 패널(콤보+Z-index 입력+트리거 버튼)은 **삭제하거나 라벨/동작을 바꾸지 않는다.**
- 근거: z_index 지정 수동 테스트 수단으로 여전히 필요(`btn_start`는 z_index 개념이 없어 완전히 동일한 대체가 안 됨).
- `IAxisController`/Z축 실이동 관련 추가 구현은 **불필요** — Z축 실이동은 이미 처음부터 외부 PLC/수동 다이얼 담당이고 현재 동작이 사용자가 원하는 상태.

### D-03: 실패 사유 메시지 — 이번 phase에 포함 (LOCKED)
- RUN 실행이 막힐 때(D-01의 카메라 공유 상호배타 케이스 포함), "어느 시퀀스가 바빠서 막혔는지"를 명시하는 메시지로 개선한다.
- 현재 `CustomMessageBox.Show("Error", "Sequence is already running.", ...)`(`InspectionListView.xaml.cs:388`)는 어느 시퀀스가 원인인지 알려주지 않는다 — `SequenceHandler.StateAll`의 `_StateSeqName`(어느 시퀀스가 non-Idle인지 이미 추적하는 필드, `SequenceHandler.cs:90,96,100`)을 재사용해 메시지에 포함시키는 방향으로 우선 검토.
- 이 작업은 D-01과 같은 파일(InspectionListView.xaml.cs) 영역을 건드리므로 같은 plan/wave에서 함께 처리하는 것이 효율적.

### Claude's Discretion
- IsIdle/StateAll 수정을 정확히 어느 레이어에 넣을지(SequenceHandler 오버로드 추가 vs 호출부 국소 수정)는 리서치 결과에 따라 플래너가 결정.
- "카메라 공유 여부" 판정 방식의 정확한 구현(어느 클래스/메서드에 헬퍼를 둘지)은 플래너 재량.
- 실패 메시지의 정확한 문구/포맷은 재량.

</decisions>

<canonical_refs>
## Canonical References

No external specs — 이 phase는 사용자가 상세 코드 추적을 완료해 제공한 내용과, 이번 discuss-phase 세션 중 에이전트가 추가 검증한 사실(카메라 공유/grab lock 부재/Cross-Z 무관성)을 기반으로 확정됨.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SequenceHandler._StateSeqName`(`SequenceHandler.cs:90,96,100`): `StateAll` 계산 중 어느 시퀀스가 non-Idle인지 이미 추적하는 필드 — D-03의 실패 메시지 개선에 그대로 재사용 가능.
- `SequenceHandler.IsSequenceActive(ESequence)`(`SequenceHandler.cs:43-48`): 이 PC 역할(CameraRole)에서 어떤 시퀀스가 등록되는지 판단하는 기존 헬퍼 — 카메라 공유 판정 로직 설계 시 참고할 패턴(단, 이 헬퍼 자체는 "등록 여부"이지 "카메라 공유 여부"가 아니므로 그대로 재사용은 안 됨, 새 판정 로직 필요).

### Established Patterns
- `DeviceHandler.RegisterRequiredDevices()`(`DeviceHandler.cs:96-104`, `Custom\Device\DeviceHandler.cs`)와 `SequenceHandler.RegisterSequences()`(`SequenceHandler.cs:50-58`)가 "SequenceHandler.IsSequenceActive 와 정책이 1:1 동기화되어야 함(미등록 카메라 시퀀스 미생성)" 주석(DeviceHandler.cs:100)으로 이미 연결되어 있음 — 유사한 동기화 원칙을 카메라 공유 판정에도 적용 검토.
- 실HW MIL 카메라 공유 실증 코드: `WPF_Example\Device\DeviceHandler.cs:221-244`(`sharedMil` 패턴) — CAM_BOTTOM이 이미 열린 CAM_TOP의 `MilCamera` 객체를 `Devices` 딕셔너리에 추가하는 실제 코드.

### Integration Points
- `InspectionListView.xaml.cs:369-393`(`Btn_start_Click`) 및 `Btn_batchRun_Click` — RUN 진입점, D-01/D-03 모두 여기서 시작.
- `WPF_Example\Sequence\SequenceHandler.cs:87-111`(`StateAll`/`IsIdle`) — 전역 판정 로직의 현재 위치.

</code_context>

<deferred>
## Deferred Ideas

- **REVERSE_X_BOTTOM 미적용 버그**: CAM_BOTTOM이 CAM_TOP의 `MilCamera` 객체(및 `Info`/`Properties`)를 그대로 재사용하면서, `DeviceHandler.cs:38`의 `REVERSE_X_BOTTOM=true` 설정이 실제로 반영되지 않는 별도 결함을 발견함. 이번 phase 범위와 무관한 별개 버그라 세션 중 별도 작업(task_aabad99c)으로 분리 등록됨 — 이 phase에서 손대지 않는다.
- **그룹 D-3(RUN 실행 중 NG 사유 다이얼로그)**: 그룹 C의 "실패 사유 메시지"(D-03, RUN이 아예 막혔을 때의 사유)와는 다른 주제 — "실행은 됐지만 측정이 NG난 이유"를 설명하는 다이얼로그는 `SequenceContext`/`ActionContext` 데이터 구조 리서치가 선행되어야 해서 별도 phase(`/gsd-plan-phase` 리서치 포함)로 남겨둠.

</deferred>

---

*Phase: 69-poc-run*
*Context gathered: 2026-08-05*
