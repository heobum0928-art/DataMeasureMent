# Quick Task 260818-ef5: Action_FAIMeasurement Run() 메서드 가독성 리팩토링 — 무회귀 최우선 - Context

**Gathered:** 2026-08-18
**Status:** Ready for planning

<domain>
## Task Boundary

`WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs`의 `public override ActionContext Run()` 메서드(약 98~676줄, 580줄)가 switch(EStep) 안에 거대한 case 블록(특히 `case EStep.DatumPhase:`가 100줄 이상)을 갖고 있어 가독성이 떨어진다는 사용자 피드백. 사용자가 직접 그 블록 전체를 붙여넣으며 문제를 지목했다.

리팩토링 목표:
1. 거대 case 블록을 의미 있는 이름의 private 메서드로 추출 (`RunInit`, `RunMoveZ`, `RunDatumPhase`, `RunGrab`, `RunMeasure`, `RunEnd` 및 DatumPhase 내부의 dual-image/1-image 분기용 하위 헬퍼)
2. 삼항연산자(`?:`) 파일 전체에서 제거 → if-else
3. 중첩 조건을 명시적 if-else/switch로 풀어쓰기
4. 오늘(2026-08-18) 타이머 해상도 조사용으로 넣은 임시 진단 로그 필드 정리

</domain>

<decisions>
## Implementation Decisions

### 최우선 원칙 — 기존 동작 100% 보존
사용자 표현(반복 강조): "제일 중요한건 기존 프로그램 영향을 절대 주면 안돼". 생산 라인 검사 판정 코드이므로:
- 분기 조건, 실행 순서, side-effect 순서(조명 적용/대기 시점, Dispose 순서, MarkDatumFailed/MarkAlignFailed 호출 시점, Step 전이 조건) 전부 리팩토링 전후 완전 동일해야 함
- 순수 구조 재배치(메서드 추출, 삼항→if-else 표현 변환)만 허용. 로직/판정/타이밍 변경 절대 금지
- 사용자가 명시적으로 "여러 에이전트도 써도 돼"라고 승인 — plan-checker/verifier가 "각 case 블록의 분기 조건과 실행 순서가 리팩토링 전후 1:1 대응하는지"를 명시적 체크리스트로 확인할 것

### 임시 진단 로그 정리 범위
사용자 결정(AskUserQuestion): **완전 제거**. 대상 필드 — `[FaiTiming]` 자체 및 `light=`/`lightWait=`/`grab=`/`detect=`/`thread=`/`dbg=`/`sleep5=` 전부. 근거: 타이머 해상도 근본원인은 오늘 이미 확정 수정됨(커밋 `327cb73`/`369811c`). `Action_FAIMeasurement.cs` 안의 해당 필드만 대상 — `SystemHandler.cs`/`SequenceBase.cs`의 `[MemCacheWarmup]`/`[TimerRes]`는 이번 범위 밖(건드리지 않음).

**유지할 것**: 오늘 만든 `[SEQ]`/`[Datum]`/`[Measure]` 시퀀스 서사 로그(`LogSeqStep`/`LogSeqAlgo` 호출) — 이건 정리 대상이 아니고 사용자용 로그로 그대로 유지. tact(단계별 소요시간)는 이미 `[SEQ]` 로그 안에 포함되어 있으므로 정보 손실 없음.

### 메서드 추출 단위
사용자 원문 그대로 채택: `RunInit`/`RunMoveZ`/`RunDatumPhase`/`RunGrab`/`RunMeasure`/`RunEnd` 6개 + DatumPhase 내부 dual-image/1-image 두 분기를 각각 더 작은 헬퍼로(`TryDetectOneDatumDualImage`/`TryDetectOneDatumSingleImage` 류 이름 예시, 실제 이름은 플래너가 기존 명명 관례에 맞춰 확정).

### Claude's Discretion
- 추출된 메서드들의 정확한 시그니처(참조 전달 방식 등)와 파일 내 배치 순서
- DatumPhase 내부를 얼마나 더 잘게 쪼갤지의 구체적 경계(사용자는 "쪼갤 수 있으면 쪼갠다"로 방향만 제시)

</decisions>

<specifics>
## Specific Ideas

사용자가 직접 예시로 `case EStep.DatumPhase: { ... }` 전체 블록(현재 파일의 약 154~330줄 부근, dual-image 오설정 게이트 → TryGrabOrLoadDualDatumImages → align/단일검출 분기 → 1-image 분기의 GrabOrLoadDatumImage → align/단일검출 분기까지)을 붙여넣어 "이게 번잡하다"의 구체적 기준을 제시함. 이 블록이 리팩토링의 핵심 타겟.

</specifics>

<canonical_refs>
## Canonical References

- 프로젝트 표준 컨벤션(이 세션 내내 지켜온 것, CLAUDE.md에도 명시): 삼항연산자 금지, 헝가리언 표기법(bXxx/nXxx/szXxx/dXxx), C# 7.2(C# 8+ 문법 금지), Allman 스타일(이 파일은 이미 Allman)
- 기존 상세 WHY 주석(quick-260807, Phase 54/57/68 등)은 삭제 금지 — 추출된 메서드로 그대로 이동
- 오늘 커밋 `327cb73`(타이머 해상도 1ms 적용), `369811c`(최소화/가림 시에도 유지) — 타이머 버그가 이미 해결됐다는 근거

</canonical_refs>
