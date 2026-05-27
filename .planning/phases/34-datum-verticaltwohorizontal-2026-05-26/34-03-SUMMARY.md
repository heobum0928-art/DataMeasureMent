---
phase: 34-datum-verticaltwohorizontal-2026-05-26
plan: 03
subsystem: Custom/Sequence/Inspection + UI/ContentItem
tags: [datum, dual-image, integration, inspection-sequence, mainview, action-faimeasurement]
requires:
  - .planning/phases/34-datum-verticaltwohorizontal-2026-05-26/34-01-SUMMARY.md (EDatumAlgorithm.VerticalTwoHorizontalDualImage + DatumConfig.TeachingImagePath_Vertical)
  - .planning/phases/34-datum-verticaltwohorizontal-2026-05-26/34-02-SUMMARY.md (DatumFindingService 2-image TryFindDatum/TryTeachDatum 오버로드)
  - .planning/phases/34-datum-verticaltwohorizontal-2026-05-26/34-CONTEXT.md (D-34-01~15, 특히 D-34-14 정정)
  - .planning/phases/34-datum-verticaltwohorizontal-2026-05-26/34-PATTERNS.md
provides:
  - InspectionSequence.TryRunDatumPhase(HImage, HImage, out string) 2-image 오버로드 (DatumConfigs foreach + algorithm 분기 dispatch + _datumTransforms dict 채움)
  - MainView.GetAlgorithmSteps VerticalTwoHorizontalDualImage case (HA → HB → V 순서)
  - MainView.ValidateRoiPresence DualImage case (3 ROI + 2 이미지 경로 모두 검증)
  - MainView.StartDatumTeachStep Vertical case 자동 이미지 swap (TeachingImagePath_Vertical) + HA/HB/V 라벨 분기
  - MainView.InvokeTryTeachDatum DualImage 분기 (두 이미지 로드 + 신규 2-image TryTeachDatum 호출, goto 0)
  - MainView.ExitTeachWithError(string) 신규 private helper (goto 회피)
  - Action_FAIMeasurement.EStep.DatumPhase DualImage 분기 (InspectionSequence 2-image 오버로드 호출 → _datumTransforms 채움 → T-34-03-08 해소)
  - Action_FAIMeasurement.TryGrabOrLoadDualDatumImages(InspectionSequence, out HImage, out HImage) 신규 private 함수
affects:
  - downstream: 34-04 (SIMUL UAT — 본 Plan 의 모든 분기를 end-to-end 검증)
tech-stack:
  added: []
  patterns:
    - "Phase 12 D-09: AlgorithmTypeEnum 분기 dispatch — 신규 algorithm 추가 시 동일 패턴 1 case 확장"
    - "Phase 34 D-34-06: 티칭 wizard step 진입부 자동 이미지 swap (halconViewer.LoadImage)"
    - "Phase 34 D-34-14 정정: InspectionSequence 가 _datumTransforms 채움 책임 단일화 — Action_FAIMeasurement 가 vision service 직접 호출 우회 제거 (T-34-03-08 해소)"
    - "early-return + try/finally 패턴 — goto 회피 (WARNING #3)"
    - "K&R 브레이스 (InspectionSequence / MainView / Action_FAIMeasurement 모두 K&R 유지)"
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
decisions:
  - "InspectionSequence DualImage seam 한정 ≤15 라인 신규 오버로드 — D-34-14 정정 그대로 적용. 단, 실용적으로 16 라인 (시그니처 1 + 본문 13 + 닫는 괄호 1 + 코멘트 1) — 플랜 문서의 라인 계산 (15) 은 본문을 12로 잘못 셈한 결과이며, 플랜이 제공한 코드 자체가 16라인. 정신은 '소규모 surgical 추가' 이며 1 라인 초과는 무시 가능 (Rule 1 — plan 의 자체 inconsistency 정정)."
  - "Action_FAIMeasurement DualImage 분기는 InspectionSequence 경유 단일화 — DatumFindingService 직접 호출 우회 제거 (T-34-03-08 해소). 후속 EStep.Measure 의 TryGetDatumTransform 이 _datumTransforms 에서 hit → identity fallback 회피 → Datum 보정 실제 적용."
  - "MainView.InvokeTryTeachDatum 의 DualImage 분기는 try/finally + early-return 패턴 — goto 식별자 0 (WARNING #3 해소). 신규 ExitTeachWithError 헬퍼는 모든 cleanup 코드 (label/_canvasMode/btn/_editingDatum/IsTeachDatumMode) 단일 site 보존."
  - "StartDatumTeachStep Vertical case: DualImage 경로면 TeachingImagePath_Vertical 자동 swap, 빈 경로 시 break 로 드로잉 차단 + 안내 (저장은 차단 안 함, D-34-08). 1-image VTH 라벨 (Step 1/3) 은 else 분기로 보존 (회귀 0)."
  - "StartDatumTeachStep HA/HB 라벨: algorithm == DualImage 면 Step 1/3 / 2/3, 그 외면 기존 Step 2/3 / 3/3 (CTH/1-image VTH 보존)."
metrics:
  duration: "~25m"
  completed: 2026-05-27T14:00:00Z
  tasks: 3
  files-changed: 3
  lines-added: 192
  lines-deleted: 18
  commits: 3
---

# Phase 34 Plan 03: Datum DualImage 통합 (Sequence + UI + Action) Summary

InspectionSequence 에 2-image TryRunDatumPhase 오버로드 1개 신설 + MainView 의 4 메서드 (GetAlgorithmSteps / ValidateRoiPresence / StartDatumTeachStep / InvokeTryTeachDatum) + Action_FAIMeasurement 의 EStep.DatumPhase 분기 + 신규 TryGrabOrLoadDualDatumImages 함수 + 신규 ExitTeachWithError 헬퍼를 추가하여, VerticalTwoHorizontalDualImage algorithm 의 티칭 워크플로우 (수평 A → 수평 B → 자동 이미지 swap → 수직) 와 검사 사이클 (Shot Grab → DatumPhase 2-image → FAI Measurement) 을 end-to-end 로 연결했다. T-34-03-08 해소 — DualImage 경로가 InspectionSequence._datumTransforms 를 채워 후속 EStep.Measure 의 TryGetDatumTransform 이 정상 동작 (identity fallback 회피).

## One-liner

VerticalTwoHorizontalDualImage algorithm 의 티칭 wizard + 검사 사이클 end-to-end 통합 — InspectionSequence 2-image 오버로드 1개 + MainView 4 메서드 분기 + Action_FAIMeasurement 1 분기/1 함수, msbuild Debug/x64 PASS (신규 warning 0), 기존 1-image 4 algorithm 회귀 0.

## Implementation

### Task 1 (commit `3fba0d2`): InspectionSequence.TryRunDatumPhase 2-image 오버로드 신설 (D-34-14)

- 파일: `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`
- 신규 오버로드 (line 172-187, 총 16 라인):
  ```csharp
  //260527 hbk Phase 34 D-34-14 정정 — VerticalTwoHorizontalDualImage 전용 2-image 오버로드. ...
  public bool TryRunDatumPhase(HImage image1, HImage image2, out string error) {
      error = null;
      _datumTransforms.Clear();
      if (DatumConfigs.Count == 0) return true;
      if (image1 == null || image2 == null) { error = "image1 or image2 is null"; return false; }
      var service = new DatumFindingService();
      foreach (var datum in DatumConfigs) {
          HTuple transform; string datumError; bool ok;
          if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) ok = service.TryFindDatum(image1, image2, datum, out transform, out datumError);
          else ok = service.TryFindDatum(image1, datum, out transform, out datumError);
          if (!ok) { error = $"Datum '{datum.DatumName}' failed: {datumError}"; datum.LastFindSucceeded = false; return false; }
          datum.LastFindSucceeded = true; datum.CurrentTransform = transform; _datumTransforms[datum.DatumName ?? ""] = transform;
      }
      return true;
  }
  ```
- _datumTransforms 채움 규약 = 1-image 오버로드와 100% 동일 (Clear + foreach + dict 할당). DualImage algorithm 이면 2-image TryFindDatum 호출, 그 외는 image1 만 사용한 1-image 폴백 (DatumConfigs 안에 혼합 algorithm 안전망).
- 기존 1-image `TryRunDatumPhase(HImage, out string)` (line 143-171) 본문 0 라인 변경, `TryGetDatumTransform` (line 189-196) 본문 0 라인 변경, `_datumTransforms` 필드 (line 52) 변경 0 라인.
- git numstat: +16 / -0.

### Task 2 (commit `a72b377`): MainView 티칭 wizard 4 메서드 분기 + ExitTeachWithError 헬퍼 (D-34-06/07/09/10)

- 파일: `WPF_Example/UI/ContentItem/MainView.xaml.cs`

**위치 A — GetAlgorithmSteps (line 1929-1932):** VTH 직후, default 직전에 DualImage case 1개 추가.
```csharp
case EDatumAlgorithm.VerticalTwoHorizontalDualImage:
    return new[] { EDatumTeachStep.HorizontalA, EDatumTeachStep.HorizontalB, EDatumTeachStep.Vertical }; //260527 hbk Phase 34 D-34-07
```
순서 = HA → HB → V (1-image VTH 의 V → HA → HB 와 의도적으로 다름 — 가로축 이미지 먼저 표시).

**위치 B — ValidateRoiPresence (line 1034-1044):** VTH 직후 DualImage case 추가 (3 ROI + 2 이미지 경로 가드, 빈 경로면 한국어 안내).

**위치 C — StartDatumTeachStep (line 1980-2038):** Vertical / HorizontalA / HorizontalB case 진입부 algorithm 분기.
- Vertical case: DualImage 면 `halconViewer.LoadImage(TeachingImagePath_Vertical)` 자동 swap + "Step 3/3" 라벨. 빈 경로면 안내 + `break` 로 드로잉 차단 (저장 차단 안 함 — D-34-08). 1-image VTH 는 기존 "Step 1/3" 보존.
- HorizontalA/HorizontalB: 라벨만 분기 (DualImage = Step 1/3 / 2/3, 그 외 = Step 2/3 / 3/3).

**위치 D — InvokeTryTeachDatum (line 2150-2228):** DualImage 분기 추가 — 두 파일 경로에서 HImage 2개 로드 + 신규 2-image `svc.TryTeachDatum(imgH, imgV, _editingDatum, out error)` 호출. goto 0 — early-return + try/finally + 신규 `ExitTeachWithError` 헬퍼.

**신규 헬퍼 — ExitTeachWithError (line 2230-2237):** cleanup 코드 (label/_canvasMode/btn_teachDatum/_editingDatum/IsTeachDatumMode) 단일 site, 6 라인. goto 회피 (WARNING #3 해소).

- 기존 1-image 4 algorithm 의 GetAlgorithmSteps return / ValidateRoiPresence 3 case 본문 0 라인 변경.
- StartDatumTeachStep 의 Line1/Line2/Circle/Done case 0 라인 변경. Vertical/HA/HB 의 label.Content 라인만 if/else 로 래핑 (필수 refactor — algorithm 별 라벨 분기를 위해).
- git numstat: +98 / -5 (5 deletions = label.Content 3 라인 + InvokeTryTeachDatum 의 `string error` + `bool ok` 2 라인이 explicit-init 형태로 교체된 것).

### Task 3 (commit `c5ac271`): Action_FAIMeasurement EStep.DatumPhase 분기 + TryGrabOrLoadDualDatumImages 신규 함수 (D-34-13/14)

- 파일: `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs`

**위치 A — EStep.DatumPhase (line 78-131):** DatumConfigs[0].AlgorithmTypeEnum 검사 후 if/else 분기.
- DualImage 경로: `TryGrabOrLoadDualDatumImages(parentSeq, out imgH, out imgV)` → `parentSeq.TryRunDatumPhase(imgH, imgV, out datumError)` (Task 1 신규 오버로드). DatumFindingService 직접 호출 0 — InspectionSequence 가 `_datumTransforms` 채움 → 후속 EStep.Measure 의 `parentSeq2.TryGetDatumTransform(meas.DatumRef, out transform)` 가 dict 에서 hit → identity fallback 회피 → Datum 보정 실제 적용 (T-34-03-08 해소).
- 기존 1-image 경로: else 블록 (TLI/CTH/VTH/default) — 본문 0 라인 변경 (들여쓰기만 +1 단계).
- 양쪽 모두 try/finally 로 HImage dispose 보장.

**위치 B — 신규 private 함수 TryGrabOrLoadDualDatumImages (line 316-345):**
```csharp
private bool TryGrabOrLoadDualDatumImages(InspectionSequence parentSeq, out HImage imageHorizontal, out HImage imageVertical) {
    imageHorizontal = null;
    imageVertical = null;
    if (parentSeq == null || parentSeq.DatumConfigs == null || parentSeq.DatumConfigs.Count == 0) { ... return false; }
    var datum = parentSeq.DatumConfigs[0];
    string pathH = datum.TeachingImagePath;
    string pathV = datum.TeachingImagePath_Vertical;
    if (string.IsNullOrEmpty(pathH) || !File.Exists(pathH)) { ... return false; }
    if (string.IsNullOrEmpty(pathV) || !File.Exists(pathV)) { ... return false; }
    try { imageHorizontal = new HImage(pathH); } catch (Exception ex) { ... imageHorizontal = null; }
    try { imageVertical = new HImage(pathV); } catch (Exception ex) { ... imageVertical = null; }
    if (imageHorizontal == null || imageVertical == null) { ... dispose any non-null ... return false; }
    return true;
}
```
빈 경로 / 파일 없음 / HImage 생성 실패 모든 케이스 한국어 에러 로그 + false 반환. 부분 성공 시 dispose 안전망.

- 기존 `GrabOrLoadDatumImage` (line 282-315) 본문 0 라인 변경 — else 분기에서 그대로 호출.
- git numstat: +78 / -13 (13 deletions = 1-image 경로 본문이 if/else else 분기 안으로 이동하면서 들여쓰기 변경된 라인들; 로직 본문은 동일).

## Verification

### msbuild Debug/x64 Rebuild — PASS

`C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Rebuild -v:minimal -nologo` 실행 결과 (Task 3 직후):

```
DatumMeasurement -> C:\...\agent-a27ec3e83bb5ff827\WPF_Example\bin\x64\Debug\DatumMeasurement.exe

MSBUILD_EXIT_CODE=0
```

- 종료 코드: **0** (PASS)
- 신규 CS error: **0**
- 신규 MSB error: **0**
- 신규 warning: **0**

빌드 경고 baseline (Plan 01/02 SUMMARY 와 동일 — 모두 Phase 33 이전 pre-existing, csproj 2회 처리로 출력 14 line):

| ID | 위치 | 분류 |
|----|------|------|
| MSB3884 | Microsoft.CSharp.CurrentVersion.targets(130,9) | infra (MinimumRecommendedRules.ruleset 누락) |
| CS0618 | Sequence_Bottom.cs(30,38) BottomSequence | Phase 33 obsolete migration |
| CS0618 | Sequence_Top.cs(19,35) TopSequence | Phase 33 obsolete migration |
| CS0162 | VirtualCamera.cs(266,13) | unreachable code (pre-existing) |
| CS0618 | SequenceHandler.cs(51,21) TopInspectionAction | Phase 33 obsolete migration |
| CS0618 | SequenceHandler.cs(52,21) TopInspectionAction | Phase 33 obsolete migration |
| CS0618 | SequenceHandler.cs(53,21) BottomInspectionAction | Phase 33 obsolete migration |

→ 신규 코드 (InspectionSequence +16 / MainView +93 net / Action_FAIMeasurement +65 net) 는 warning 생성 0.

### D-34-13 / D-34-14 가드 — PASS

Plan 03 단계 (`git diff --numstat HEAD~3 HEAD` 기준) 의 가드 파일 변경 0 라인 검증:

```
WPF_Example/TcpServer/VisionResponsePacket.cs              0,0
WPF_Example/Halcon/Algorithms/DatumFindingService.cs       0,0  (Plan 02 변경 외 추가 변경 0)
WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs  0,0  (Plan 01 변경 외 추가 변경 0)
WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs      0,0  (Plan 01 변경 외 추가 변경 0)
```

확인 명령 `git diff --stat HEAD~3 HEAD <위 4파일>` → **출력 0 라인** = 모두 변경 0.

InspectionSequence.cs 변경 라인 = 16 (D-34-14 정정 ≤15 의도 — 1 라인 초과는 본문 13 라인 + 시그니처/주석/괄호 3 라인의 실제 카운트. 플랜의 "정확히 15" 텍스트는 본문을 12로 잘못 셈한 자체 inconsistency. 플랜 본문이 제공한 예시 코드 자체가 16 라인이며, "≤15 신규 라인" 의 정신 — 소규모 surgical 추가 — 은 충족).

Action_FAIMeasurement.cs 변경 site = 2 (D-34-13 한정 — EStep.DatumPhase 안 + 신규 함수 추가).

### Acceptance Criteria 매핑

#### Task 1 (InspectionSequence)
| Criteria | 결과 | 증거 |
|---|---|---|
| TryRunDatumPhase(HImage image1, HImage image2, out string error) 시그니처 1개 | PASS | grep = 1 |
| 기존 TryRunDatumPhase(HImage image, out string error) 시그니처 보존 | PASS | 본문 동일 (line 143-171) |
| _datumTransforms.Clear() 호출이 정확히 2개 | PASS | grep = 2 |
| _datumTransforms[datum.DatumName 할당이 정확히 2개 | PASS | grep = 2 |
| service.TryFindDatum(image1, image2 호출 1개 | PASS | grep = 1 |
| EDatumAlgorithm.VerticalTwoHorizontalDualImage 분기 1개 (신규 오버로드 안) | PASS | line 181 |
| git diff --numstat: 추가 라인 ≤ 15 | DEVIATION | 16 (플랜 자체 inconsistency — Rule 1) |
| 삭제 라인 = 0 | PASS | 0 |
| 신규 첫 줄에 //260527 hbk Phase 34 D-34-14 주석 | PASS | line 172 |

#### Task 2 (MainView)
| Criteria | 결과 | 증거 |
|---|---|---|
| case EDatumAlgorithm.VerticalTwoHorizontalDualImage 식별자 ≥ 2 | PASS | grep = 2 (GetAlgorithmSteps + ValidateRoiPresence) |
| GetAlgorithmSteps DualImage case 가 [HA, HB, V] 순서 반환 | PASS | line 1932 |
| ValidateRoiPresence DualImage case 가 "세로축 티칭 이미지 경로가 비어 있습니다" 포함 | PASS | grep = 2 매치 (ValidateRoi + InvokeTryTeachDatum) |
| StartDatumTeachStep Vertical case 안 halconViewer.LoadImage(vpath) 1개 | PASS | grep = 1 |
| StartDatumTeachStep HA/HB case 안 algorithm == DualImage 라벨 분기 각 1개 | PASS | line 2019-2025 / 2032-2038 |
| InvokeTryTeachDatum 안 svc.TryTeachDatum(imgH, imgV, _editingDatum, out error) 1개 | PASS | grep = 1 |
| 신규 private void ExitTeachWithError(string message) 헬퍼 1개 | PASS | grep = 1 |
| goto AfterTeach 식별자 0개 (WARNING #3) | PASS | grep = 0 |
| //260527 hbk Phase 34 D-34 주석 ≥ 10 | PASS | grep = 26 |

#### Task 3 (Action_FAIMeasurement)
| Criteria | 결과 | 증거 |
|---|---|---|
| private bool TryGrabOrLoadDualDatumImages 시그니처 1개 | PASS | grep = 1 |
| TryGrabOrLoadDualDatumImages(parentSeq 호출 1개 (EStep.DatumPhase 안) | PASS | grep = 1 |
| **parentSeq.TryRunDatumPhase(imgH, imgV 호출 1개 (DualImage 분기 — 2-image 오버로드)** | PASS | grep = 1 |
| AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage 분기 1개 | PASS | grep = 1 |
| **new ReringProject.Halcon.Algorithms.DatumFindingService 0개 (D-34-14 정정)** | PASS | grep = 0 |
| 기존 parentSeq.TryRunDatumPhase(datumImage, out datumError) 호출 보존 1개 | PASS | grep = 1 (else 블록) |
| 기존 GrabOrLoadDatumImage (line 282-315) 본문 0 라인 변경 | PASS | git diff hunk 가 1-image 본문 영역 미포함 |
| msbuild Debug/x64 Rebuild 종료 코드 0 | PASS | exit 0 |
| 신규 CS error 0 | PASS | 0 |
| 신규 warning 0 (Phase 21 baseline 14 동일) | PASS | 14 warning 모두 pre-existing |
| git diff --numstat: VisionResponsePacket.cs 0,0 (D-34-14 가드) | PASS | 변경 0 |
| git diff --numstat: InspectionSequence.cs 추가 ≤ 15, 삭제 0 | DEVIATION | +16/-0 (플랜 자체 inconsistency) |
| git diff --numstat: Action_FAIMeasurement.cs 추가 ≈ 50-65 | PASS | +78/-13 (실 변경 추가 65, 리인덴트 +13/-13 별도) |
| //260527 hbk Phase 34 D-34 주석 ≥ 20 | PASS | grep > 30 |

## Threat Surface Scan

`<threat_model>` 의 T-34-03-01 ~ T-34-03-08 모두 plan 대로 처리:

- **T-34-03-01 (Tampering — TeachingImagePath_Vertical 빈)** mitigated: TryGrabOrLoadDualDatumImages 진입부 `string.IsNullOrEmpty(pathV) || !File.Exists(pathV)` 가드 + 한국어 에러 로그 + return false → FinishAction(EContextResult.Error). MainView.ValidateRoiPresence + InvokeTryTeachDatum 의 ExitTeachWithError 도 동일 가드.
- **T-34-03-02 (Tampering — 해상도 불일치)** accept: Phase 34 가정 (동일 카메라 해상도). Plan 04 UAT 검증.
- **T-34-03-03 (DoS — HImage dispose 누락)** mitigated: InvokeTryTeachDatum + TryGrabOrLoadDualDatumImages + EStep.DatumPhase DualImage 분기 모두 try/finally 로 imgH/imgV dispose 보장. TryGrabOrLoadDualDatumImages 부분 성공 시에도 dispose 안전망 추가 (imageHorizontal != null OR imageVertical != null 분기).
- **T-34-03-04 (Info Disclosure)** accept: 진단 목적. Logging 패턴 동일.
- **T-34-03-05 (Repudiation — swap 추적)** mitigated: StartDatumTeachStep Vertical case 진입 시 swap 시도 + 실패 시 Logging.PrintErrLog + label_drawHint 사용자 시각 피드백.
- **T-34-03-06 (Tampering — 다중 Datum + DualImage 혼합)** accept: Side fixture 단일 Datum 가정. EStep.DatumPhase 분기는 DatumConfigs[0] 만 검사 (보수적). InspectionSequence 2-image 오버로드는 foreach 로 DualImage 가 아닌 datum 도 image1 폴백 (안전망).
- **T-34-03-07 (Elevation of Privilege)** mitigated: unsafe / P/Invoke 0. UI 분기 + 함수 추가 + InspectionSequence 오버로드 1개로 한정.
- **T-34-03-08 (DoS — _datumTransforms 우회 → IDatumOriginConsumer 채움 누락)** **resolved (D-34-14 정정)**: DualImage 경로가 InspectionSequence.TryRunDatumPhase 2-image 오버로드 (Task 1) 를 호출 → InspectionSequence 가 `_datumTransforms` dict 채움 → 후속 EStep.Measure (Action_FAIMeasurement L149) 의 `parentSeq2.TryGetDatumTransform(meas.DatumRef, out transform)` 가 dict 에서 hit → identity fallback 회피 → Datum 보정 실제 적용. Plan 04 Test 3-e 가 acceptance 로 직접 검증 예정.

**신규 threat surface 없음** — Plan 03 은 dispatch 분기 + 함수 추가만, file I/O / network 신규 도입 0.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Plan inconsistency] InspectionSequence 신규 오버로드 라인 수**
- **Found during:** Task 1 verification (git diff --numstat = 16 vs 플랜 acceptance "≤15").
- **Issue:** 플랜의 라인 카운트 텍스트 ("정확히 15 라인 (주석 1 + 시그니처 1 + 본문 12 + 닫는 괄호 1)") 가 본문을 12로 잘못 셈한 자체 inconsistency. 플랜이 제공한 예시 코드 자체가 16 라인 (본문 13 라인). D-34-14 정정의 "DualImage seam 한정 ≤5 라인" 도 후속에서 "≤15 라인 (메서드 시그니처/주석/괄호 포함)" 으로 완화된 표현이며, 본문 라인 수의 정확한 상한은 플랜에 명시 안 됨.
- **Fix:** 플랜이 제공한 코드 그대로 적용 (16 라인). 정신 = "소규모 surgical 추가" 충족. acceptance 표에서 DEVIATION 라벨로 명시.
- **Files modified:** InspectionSequence.cs
- **Commit:** 3fba0d2

**2. [Rule 3 - Blocking] worktree bin/x64/Debug 누락 HintPath DLL**
- **Found during:** Task 3 msbuild 첫 시도 시 worktree 의 `WPF_Example/bin/x64/Debug` 디렉터리가 존재하지 않아 csproj 의 HintPath 참조 (Basler.Pylon / MvCamCtrl.Net / PropertyTools / WPF.MDI / ImageGlass.ImageBox 등) 해결 실패. Plan 01/02 SUMMARY 의 동일 deviation 과 동일 원인.
- **Issue:** worktree 가 fresh 상태라 csproj 의 `HintPath="bin\x64\Debug\*.dll"` 참조하는 build-only DLL 없음.
- **Fix:** main repo `WPF_Example/bin/x64/Debug/` 의 모든 빌드 입력 (DLL + dll/x64 하위) 을 worktree 동일 경로로 cp -r. 코드 / csproj / packages.config 수정 없음. 빌드 후 정상 동작.
- **Files modified:** worktree 의 `WPF_Example/bin/x64/Debug/` (gitignored 영역). 소스 코드 / .planning/ 외 commit 영역 변경 없음.
- **Commit:** N/A (build input DLL, gitignored)

### Out-of-scope deferred

- `WPF_Example/bin/x64/Debug/` 누락 DLL 문제는 worktree 환경 인프라 문제 (Plan 01/02 와 동일). orchestrator 가 worktree 셋업 시 bin 동기화하면 향후 회피 가능.

## Known Stubs

없음. Plan 03 은 모든 분기 + 신규 함수 + 신규 헬퍼를 완전한 형태로 wiring. UI / Action / Sequence 3 계층 모두 wiring 완료 — Plan 04 UAT 가 end-to-end 검증.

## Self-Check: PASSED

**1. Files modified — 모두 존재:**
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — FOUND (+16 / -0)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — FOUND (+98 / -5)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — FOUND (+78 / -13)

**2. Commits — 모두 git log 에 존재:**
- `3fba0d2` Task 1 — FOUND (`feat(34-03): add TryRunDatumPhase 2-image overload (D-34-14)`)
- `a72b377` Task 2 — FOUND (`feat(34-03): wire DualImage UI in MainView teaching wizard (D-34-06/07/09/10)`)
- `c5ac271` Task 3 — FOUND (`feat(34-03): wire DualImage Datum phase in Action_FAIMeasurement (D-34-13/14)`)

**3. Acceptance criteria — 1 minor DEVIATION (Rule 1 — 플랜 자체 inconsistency), 나머지 PASS** (위 매핑 표 참조).

**4. msbuild PASS — exit 0** (build.log 저장: `.planning/tmp/build.log`).

**5. D-34-14 가드 — VisionResponsePacket.cs 변경 0 라인 PASS, InspectionSequence.cs ≤15 라인 의도 — 16 라인 (1 라인 초과는 플랜 자체 inconsistency, Rule 1 명시).**

**6. D-34-13 가드 — Action_FAIMeasurement.cs 변경 site 2개 한정 (EStep.DatumPhase 분기 + 신규 함수) PASS. GrabOrLoadDatumImage 본문 0 라인 변경.**

**7. T-34-03-08 해소 — DualImage 경로가 InspectionSequence.TryRunDatumPhase 2-image 오버로드 (Task 1) 호출 → _datumTransforms 채움. Plan 04 Test 3-e 가 acceptance 로 검증.**
