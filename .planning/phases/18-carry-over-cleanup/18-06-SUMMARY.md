---
phase: 18
plan: 06
status: abandoned
gap_closure: true
requirements: [CO-01]
completed: 2026-05-07
outcome: superseded_by_debug_fix
resolution_commit: ba25e88
---

## Result

**Plan abandoned. CO-01 resolved by separate debug-session fix (ba25e88) outside this plan's scope.**

## Why abandoned

이 plan 은 "Browsable(false) 제거 + IsHiddenForAlgorithm 의 EndsWith(\"List\") 일괄 hide" 접근으로 CO-01 을 해결하려 했으나, 두 단계 검증 후 layer 가 잘못된 fix 임이 판명:

1. **Task 1 H1 trace (commit c0d6135 → revert 6e5f0ce)** — Circle_RadialDirectionList getter 호출 안 됨 확인. 당시 결론은 "PropertyTools 가 ICustomTypeDescriptor 우회".
2. **Task 2 fix attempt (commit 1dd8272 → revert ff9792d)** — *List 들의 Browsable 제거하니 PropertyGrid 에 DataGrid (Length 컬럼) 로 노출되는 부작용 발생, IsHiddenForAlgorithm hide 블록은 호출조차 안 됨, 원본 LtoR 4항목 표시는 그대로. 두 commits 모두 revert.

## Real root cause (debug session)

원래 보고된 "Circle_RadialDirection 이 LtoR/RtoL 4항목 표시" 는 **misidentification 이었음**. PropertyTools.Wpf 3.1.0 디컴파일 (gsd-debugger 세션 `co-01-radialdir-fallback`) 결과:

- Circle_RadialDirection 드롭다운은 정상적으로 Inward/Outward 2항목 표시 ✓
- 사용자가 본 LtoR 은 인접 필드 `Circle_EdgeDirection` 의 ComboBox 였음
- Phase 17 D-03 의 IsHiddenForAlgorithm CTH 분기 hide 룰 (`if (name == "Circle_EdgeDirection") return true`) 이 dynamic enumeration 경로 차이로 안 먹음 → Circle_EdgeDirection 노출됨

## Fix applied (outside this plan)

**Commit ba25e88 — `Circle_EdgeDirection` 에 `[PropertyTools.DataAnnotations.Browsable(false)]` 정적 추가.** Circle 알고리즘은 EdgeDirection 대신 RadialDirection (Inward/Outward) 사용 → 어떤 모드에서도 노출 불필요 → 영구 hide.

User UAT (2026-05-07) PASS: CTH 모드에서 (1) LtoR row 사라짐 (2) Inward/Outward row 정상 표시.

## key-files
created:
  - .planning/debug/resolved/co-01-radialdir-fallback.md
modified:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs (Circle_EdgeDirection 에 Browsable(false) 추가, ba25e88)

## Self-Check

- [x] CO-01 closed (별도 commit ba25e88 로 해결)
- [x] User UAT PASS
- [x] Plan 의 잘못된 시도는 모두 revert (1dd8272/c0d6135 → ff9792d/6e5f0ce)
- [x] Debug session artifacts 보존 (.planning/debug/resolved/co-01-radialdir-fallback.md)

## Lessons

1. **잘못된 가설 검증의 함정:** H1 trace 가 fire 안 한다고 해서 "PropertyTools 가 ICustomTypeDescriptor 전체를 우회" 라고 단정한 건 over-conclusion. 디컴파일로 실제 코드 경로를 확인했더라면 첫 단계에서 misidentification 이라는 것을 깨달을 수 있었음.
2. **UI 버그 보고 시 정확한 ComboBox 식별 필수:** 인접한 비슷한 ComboBox 가 여러 개 있으면 사용자도 혼동 가능. 진단 시 카테고리/라벨 정확한 매칭 우선 검증.
3. **간단한 정적 fix 가 dynamic enumeration 보다 안정적:** `[Browsable(false)]` 한 줄이 IsHiddenForAlgorithm 의 dynamic 룰보다 enumeration 경로에 무관하게 동작.
