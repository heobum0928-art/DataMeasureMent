---
phase: quick-260813-fdt
plan: 01
subsystem: vision-inspection-config
tags: [datum, propertygrid, halcon-side-camera, mirror-correction, paramBase]

# Dependency graph
requires: []
provides:
  - "DatumConfig.MirrorX / MirrorY public bool settings (PropertyGrid, Datum|Mirror category)"
  - "값 실제 변경 시에만 발화하는 경고 다이얼로그 (자동닫힘 off, 3가지 필수 고지사항 포함)"
  - "_suppressMirrorWarning 가드 패턴 (레시피 Load / Datum CopyTo 리플렉션 경로 무경고)"
affects: [side-datum-mirror-consume-followup]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "_suppressModelRename 과 동일한 '리플렉션 세터 억제 플래그' 패턴을 새 설정에 재사용 (Load try/finally + CopyTo target 플래그)"

key-files:
  created: []
  modified:
    - "WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs"

key-decisions:
  - "설정 표면만 추가하고 실제 이미지 반전(MIL grab 배선)은 후속 별도 quick 작업으로 분리 (범위 명시적 축소)"
  - "경고는 '진짜 사용자 편집'에서만 발화 — 레시피 로드/Datum 복사·붙여넣기는 리플렉션 경로이므로 억제"

patterns-established:
  - "설정 변경 경고 다이얼로그: 값이 실제로 바뀔 때만(idempotent guard) + 리플렉션 대량쓰기 경로 억제(suppress flag try/finally) 이중 가드"

requirements-completed: [QUICK-260813-FDT-01, QUICK-260813-FDT-02]

# Metrics
duration: ~15min (실행) + 사용자 실기 UAT 대기 별도
completed: 2026-08-13
---

# Quick Task 260813-fdt: Side Datum Mirror 설정 표면 Summary

**`DatumConfig`에 MirrorX/MirrorY 설정 2개 추가 — 값이 실제로 바뀔 때만 뜨는 경고 다이얼로그(자동닫힘 off) + 레시피 로드·Datum 복사 경로 무경고 가드. 실제 이미지 반전 로직은 범위 밖(후속 작업).**

## Performance

- **Duration:** ~15분 (코드 작성~빌드 검증), 이후 사용자 실기 UAT 별도 진행
- **Tasks:** 3/3 완료 (Task 1: auto, Task 2: auto/검증전용, Task 3: checkpoint:human-verify — 승인 완료)
- **Files modified:** 1 (`DatumConfig.cs`)

## Accomplishments

- `DatumConfig`에 `[Category("Datum|Mirror")]` `MirrorX` / `MirrorY` public bool 프로퍼티 2개 추가 (기본값 false, `ParamBase` 리플렉션 경로로 INI 자동 영속)
- 값이 **실제로** 바뀔 때만 발화하는 `CustomMessageBox` 경고(자동닫힘 off) — 촬영방향 변경 / 타 측정 영향 / 재시작 필요 3가지 고지
- `_suppressMirrorWarning` 가드로 `ParamBase.Load`(레시피 로드) / `CopyPublicPropertiesTo`(Datum 붙여넣기) 리플렉션 경로에서는 경고 미발생
- 실기 UAT 사용자 직접 수행 완료 — PropertyGrid 노출, 경고 문구/자동닫힘 off, 조용한 로드 경로, INI 영속 전부 확인

## 실제 삽입 위치 (줄 번호, 최종 파일 기준)

- 억제 플래그 `_suppressMirrorWarning` + `MirrorX`/`MirrorY` 프로퍼티 + `WarnMirrorChanged` 헬퍼: `ZIndexB` (기존 223번째 줄) 바로 다음, `// IOfflineImageParam — Datum 노드 Load 버튼이...` 주석 블록 앞 — plan 지시 위치 그대로.
- `Load` 오버라이드: 기존 `_suppressModelRename = true/false` try/finally에 `_suppressMirrorWarning = true/false` 한 줄씩 추가.
- `CopyTo` 오버라이드: `CopyPublicPropertiesTo(target, _copyExclude)` 호출을 `target._suppressMirrorWarning = true` → try/finally로 감싸는 형태로 변경.

## Task Commits

1. **Task 1: DatumConfig 에 MirrorX/MirrorY 프로퍼티 + 변경 경고 + 리플렉션 억제 가드 추가** - `b49d14f` (feat)
2. **Task 2: 정적 검증 + Debug x64 빌드** - 코드 변경 없음(검증 전용), 커밋 없음
3. **Task 3: 실기 확인 (checkpoint:human-verify)** - 코드 변경 없음, 사용자 승인만

**Plan metadata:** 이 SUMMARY.md 커밋은 오케스트레이터가 별도 처리 (본 실행자는 docs 커밋 생략 지시받음)

## 정적 검증 6종 실측 (Task 1 done 기준)

```
MirrorX=1 MirrorY=1 CAT=2 SUP=6 MSG=1 TGT=2
```
plan 명시 기준(`MirrorX=1 MirrorY=1 CAT=2 SUP=6 MSG=1 TGT=2`)과 **완전 일치**.

## S1~S3 (Task 2) 실측

- **S1 (변경 파일 범위)** — 커밋 전 `git status --porcelain -- WPF_Example`: 정확히 2줄
  ```
   M WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs   (사전 존재, 무관 — 미접촉)
   M WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs                 (이번 작업)
  ```
  DatumConfig.cs 커밋 후 재측정 시에는 이 파일이 이미 committed 상태라 FILES=1(사전 존재 파일만 남음)로 보이는 것이 정상 — task_commit_protocol에 따라 Task 1 완료 직후 즉시 커밋했기 때문. 커밋 전 측정값(2줄)이 plan의 실제 검증 대상이며 이 값으로 PASS 판정.
- **S2 (순수 추가 여부)** — `git diff --numstat`: 67 insertions, **1 deletion** (기준 5줄 이하 충족 — `Load`/`CopyTo` 본문에 try/finally 래핑만 가함)
- **S3 (코딩 규칙)** — 추가된 줄 중 삼항 연산자(`?`) **0건**, 신규 `using` **0건**

## 빌드 실측 (Task 2, S4)

```
BUILD_RC=0 ERRORS=0 WARN_CS=12
```
기준선(`BUILD_RC=0 / ERRORS=0 / WARN_CS=12`)과 **완전 일치**. 경고 내역 세부 확인: `CS0618 ×10` + `CS0162 ×2` — 전부 plan에 명시된 기존 기준선 소스(`Sequence_Top.cs`/`Sequence_Bottom.cs`/`SequenceHandler.cs`/`VirtualCamera.cs`)이며 이번 범위 밖. 스크래치 OutDir 폴백 미사용(정식 `bin/x64/Debug` 산출물 잠김 없이 정상 빌드됨).

## 실기 확인 7단계 결과 (Task 3, checkpoint:human-verify — 사용자 직접 수행)

| # | 항목 | 결과 |
|---|------|------|
| 1 | 앱 재빌드/재시작 → 검사 탭 → SIDE Datum 노드 → PropertyGrid에 Mirror 그룹(MirrorX/MirrorY) 노출 | PASS |
| 2 | MirrorX 체크 → 경고 다이얼로그 발생, 제목 "촬영 방향(반전) 설정 변경", 3요점(촬영방향 변경/타측정 영향/재시작 필요) 전부 포함, 자동닫힘 없음(OK 버튼으로 직접 닫음) — 스크린샷으로 확인 | PASS |
| 3 | 체크박스를 누를 때마다 경고가 매번 재발화 | 의도된 동작으로 확인(각 클릭이 진짜 값 토글이지 no-op 재저장이 아니므로 정상) |
| 4 | MirrorX=true로 저장 → 다른 레시피로 전환 → 원 레시피 재로드 → 로드 중 경고 미발생, MirrorX 값 true로 유지(INI 영속 확인) | PASS |
| 5 | MirrorX 다시 false로 되돌리고 저장(운영 레시피 원상복구) | 완료 |
| 6 | Datum 복사/붙여넣기, 앱 완전 재시작 후 영속성 | 별도 테스트 안 함 — 사용자와 합의하에 생략(4번에서 검증된 동일 가드/`ParamBase.Load` 메커니즘을 그대로 타는 코드 경로이므로 충분하다고 판단) |

**Task 3 승인 상태: APPROVED** (오케스트레이터를 통해 사용자 승인 전달받음, 2026-08-13)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` - MirrorX/MirrorY 설정 프로퍼티 2개, 변경 경고 헬퍼, Load/CopyTo 리플렉션 억제 가드 추가

## Decisions Made

- 실제 이미지 반전(카메라 grab 방향 배선)은 이번 범위에서 명시적으로 제외 — 설정 표면만 우선 만들고 후속 quick 작업으로 분리 (plan 원래 목적)
- Datum 복사/붙여넣기 + 앱 재시작 영속성의 개별 실기 테스트는 사용자와 합의하에 생략 — 4번 항목(레시피 로드 무경고 + INI 영속)에서 이미 검증된 것과 **동일한 코드 경로**(`_suppressMirrorWarning` 가드, `ParamBase` 리플렉션 Boolean case)이기 때문

## Deviations from Plan

None - plan을 그대로 실행함. 코드 삽입 위치, 문구, 가드 패턴 전부 plan 명시 그대로 작성했고 정적 검증 6종 + S1~S4 전부 plan 기준값과 정확히 일치.

## Issues Encountered

None.

## User Setup Required

None - 외부 서비스 설정 불필요.

## 후속 작업 인계 메모

**이번 작업 범위 밖 — 다음 quick 작업에서 처리할 것:**

- `MirrorX`/`MirrorY` 값은 현재 **저장만 되고 소비되지 않는다.** 실제 이미지 좌우/상하 반전 로직을 `MilCamera.RegisterRoleInfo`/`_roleInfoMap` 경로(또는 이에 상응하는 SIDE 카메라 grab 초기화 경로)에서 **앱 시작 시 1회** 적용하도록 배선해야 한다.
- 배선 시 주의: `MirrorX`/`MirrorY`는 `DatumConfig`(Datum 단위) 소속이지만 실제 반전은 **카메라 자체의 촬영 방향**이므로 같은 카메라를 쓰는 다른 Datum/FAI에도 함께 영향을 준다 — 이번 경고 문구에 이미 그 취지를 명시해뒀다. 배선 시 "어느 Datum의 값을 대표값으로 쓸 것인가"(동일 카메라 내 Datum 간 값 충돌 처리)에 대한 설계 결정이 필요하다.
- 값 변경 후 "재시작해야 적용된다"고 경고에 명시했으므로, 실제 배선도 **런타임 즉시 반영이 아니라 앱 시작 시 1회 읽기**로 구현해야 경고 문구와 실제 동작이 일치한다.

## Next Phase Readiness

- 설정 표면(MirrorX/MirrorY) 완료, INI 영속 확인, 경고 UX 사용자 승인 완료 — 다음 quick 작업(실제 이미지 반전 배선)을 위한 선행조건 충족.
- 블로커 없음.

## Self-Check: PASSED

- FOUND: `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs`
- FOUND: `.planning/quick/260813-fdt-side-datum-x-y/260813-fdt-SUMMARY.md`
- FOUND commit: `b49d14f`

---
*Phase: quick-260813-fdt*
*Completed: 2026-08-13*
