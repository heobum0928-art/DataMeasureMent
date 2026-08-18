---
phase: quick-260814-warmup-transform-fix
plan: 01
subsystem: measurement-warmup
tags: [halcon, measure_pos, warmup, bugfix, datum]
requires: []
provides:
  - "RunMeasureWarmup identity datumTransform"
  - "IsWarmupSkipTarget"
affects:
  - "WPF_Example/Custom/SystemHandler.cs"
tech-stack:
  added: []
  patterns:
    - "HOperatorSet.HomMat2dIdentity() 로 무보정 HTuple 생성 후 TryExecute 에 전달 (null 대신)"
key-files:
  created: []
  modified:
    - "WPF_Example/Custom/SystemHandler.cs"
decisions:
  - "identity transform 채택 — Point_Row/Col 은 교시 시점 절대 이미지 좌표이고 datumTransform 은 그 위에 얹는 미세 보정 델타일 뿐이라, 워밍업이 재생하는 SimulImagePath(실검사와 동일한 정적 이미지) 위에서는 무보정으로도 ROI 가 교시된 실제 위치를 가리킴"
  - "DatumOriginRow/Col 둘 다 0.0(검출 이력 없음)인 측정은 identity 강제실행 대신 skip — 원래 버그의 재현(즉시실패 반복)을 막기 위함"
metrics:
  duration: "~15분"
  completed: "2026-08-14"
---

# Quick 260814-warmup-transform-fix: 워밍업 datumTransform null→identity 수정 Summary

quick-260814-dxy 가 도입한 측정 파이프라인 워밍업이 `meas.TryExecute(img, null, 1.0, ...)` 로 `datumTransform`
에 **null** 을 넘겨, `EdgeToLineDistanceMeasurement` 등 Datum 참조 측정 타입이 진입부 가드(`datumTransform == null
|| datumTransform.Length == 0`)에서 즉시 reject 되어 HALCON `measure_pos` 를 단 한 번도 태우지 못하던 근본 버그를
`HOperatorSet.HomMat2dIdentity()` 로 만든 유효한 identity HTuple 전달로 수정했다.

## 변경 사항

**`WPF_Example/Custom/SystemHandler.cs`** (`RunMeasureWarmup`, `TryWarmupOneMeasurement` 수정 + `IsWarmupSkipTarget`
신규 추가):

1. `RunMeasureWarmup`: 루프 진입 전 `HOperatorSet.HomMat2dIdentity(out identityTransform)` 로 identity HTuple 을
   1회 생성. 생성 실패 시(catch) 워밍업 전체를 스킵하고 Error 로그 남김.
2. `IsWarmupSkipTarget(MeasurementBase meas)` 신규: `DatumRef` 가 비어있지 않고 `IDatumOriginConsumer` 구현체인데
   `DatumOriginRow`/`DatumOriginCol` 이 둘 다 0.0(=검출 이력 없음)이면 skip. 그 외(무보정 의도, 또는
   `IDatumOriginConsumer` 미구현)는 identity 로 시도.
3. `TryWarmupOneMeasurement`: 3번째 파라미터 `HTuple datumTransform` 추가, `meas.TryExecute` 호출에 `null` 대신
   그대로 전달.
4. 루프 body: `IsWarmupSkipTarget` 체크 → skip 이면 `nSkipCount++` 후 `continue`, 아니면
   `TryWarmupOneMeasurement(meas, img, identityTransform)` 호출.
5. 완료 Trace 로그에 `skip={5}` 필드 추가 (`success`/`fail`/`skip` 3종 카운트 모두 노출).

`EvaluateJudgement`/`ClearResult` 호출 없음 유지(판정 로직/화면 표시 오염 방지) — 워밍업 블록 전체 grep 확인.

## Deviations from Plan

None — plan 그대로 실행됨.

## Verification

- Plan `<verify>` grep 체크 [1]~[8] 전부 정확히 기대값과 일치 (IsWarmupSkipTarget 정의 1개, 신규 3-인자
  TryWarmupOneMeasurement 시그니처 1개, `TryExecute(img, datumTransform, 1.0,` 1개, 옛 `TryExecute(img, null, 1.0,`
  0개, `HomMat2dIdentity` 호출 1개, 호출부 3-인자 전달 1개, 워밍업 블록 내 EvaluateJudgement/ClearResult 0개,
  금지 파일 2개 무변경).
- 금지 파일 해시 (변경 없음, baseline 과 완전 일치):
  - `DatumMeasurement.csproj`: `3daa3bef520786d331716fb77bc93e2eb632b966`
  - `PickerCenterCalibrationService.cs`: `86d1071909389cdb13b4ff8f3032489aff26e2fe`
- **정상 빌드(`D:\Data\DatumMeasurement.exe` 대상)는 MSB3021/MSB3027 로 실패** — 실행 중인 프로세스
  (`Microsoft Visual Studio Insiders` PID 31036, `DatumMeasurement` PID 4984)가 산출물을 잠그고 있음.
  프로젝트 하드 규칙("빌드산출물 잠김 → 프로세스 절대 죽이지 말 것")에 따라 프로세스는 건드리지 않고,
  대신 스크래치 `-p:OutDir=<scratchpad>/build-verify/` 로 재검증: **exit 0, error 0건, warning 0건, 컴파일 성공**
  확인. `D:\Data` 산출물 복사만 막혀있을 뿐 컴파일 자체는 100% 통과.
  - 참고: 잠금 없는 최초 시도(정규 OutDir)에서 관찰된 warning 은 CS0618×10 + CS0162×2 = 12줄로
    기존 baseline 과 정확히 일치(신규 warning 0건 확인됨).
- `git diff --diff-filter=D HEAD~1 HEAD` — 삭제된 파일 없음.

## Known Stubs

None.

## Threat Flags

None — 워밍업은 결과를 버리는 내부 진단 경로이며, 신규 네트워크/인증/파일 접근 표면 없음.

---

**중요 — 사용자 확인 필요 (이 세션 범위 밖):**
런타임 검증은 앱을 재시작해야 확인 가능하다. `D:\Data\Trace` 최신 로그에서 `[MeasureWarmup] 완료 ...` 라인을 찾아:
- `success` 값이 0보다 큰지 (이전엔 항상 0이었음)
- `skip` 값이 몇인지 (신규/미실행 레시피 항목 수와 대략 일치해야 함)
- `elapsed` 값이 이전(약 166ms)보다 훨씬 길어졌는지 — 길어지는 것이 정상(진짜 `measure_pos` 스캔이 도는 것이므로)

이 3가지는 사용자가 직접 재시작 후 로그로 확인해야 한다.

## Self-Check: PASSED

- FOUND: `WPF_Example/Custom/SystemHandler.cs` (수정 확인됨)
- FOUND: commit `1860bd5` (`git log --oneline` 확인됨)
