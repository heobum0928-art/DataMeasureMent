---
phase: 06-rapid-city
plan: 02
status: complete
date: 2026-04-13
requirements: [RC-01, RC-02, RC-04]
---

# Plan 06-02 Summary

## Goal
Datum을 ShotConfig에서 InspectionSequence(Fixture) 레벨로 승격. Multi-Datum 지원(List&lt;DatumConfig&gt;), DatumConfig에 DatumName/ImageSourceMode 추가, ShotConfig에 조명 8필드 추가, ShotConfig.Datum 단일 소유 제거. (D-01, D-04~D-13, D-25)

## Delivered

### 수정 파일 (7)

**Task 1: Fixture 구조 전환**
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs`
  - `DatumName` (default "Datum_1"), `ImageSourceMode` (default "Dedicated"), `ReuseFromShotName` 추가 (D-06, D-07, D-08)
  - 기존 Line1/Line2 ROI, RefOrigin, CurrentTransform 필드 그대로 유지 (D-05)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`
  - `DisplayName` (사용자 편집 가능, GetDisplayName()로 Name 폴백) (D-01)
  - `List<DatumConfig> DatumConfigs` + `_datumTransforms` dictionary (D-04)
  - `AddDatum(string name)`, `RemoveDatum(int index)`
  - `TryRunDatumPhase(HImage image, out string error)` — DatumConfigs 순회, DatumFindingService 호출, 실패 즉시 중단 + 명확한 에러 메시지 (D-09, T-06-05)
  - `TryGetDatumTransform(string datumRef, out HTuple transform)` — 빈 datumRef는 HomMat2dIdentity 반환 (D-10)
  - using `System.Collections.Generic`, `HalconDotNet`, `ReringProject.Halcon.Algorithms` 추가
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs`
  - `DatumConfig Datum` 프로퍼티 제거 + 생성자 초기화 제거 + ClearAllResults의 Datum 초기화 블록 제거 (D-25)
  - Ring/Back/Coax/Side 각각 `*_Enabled` (bool) + `*_Brightness` (int) — 총 8필드 추가 (D-11)

**Task 2: 컴파일 에러 해소**
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs`
  - `ShotParam.Datum` 참조 전면 제거
  - `HOperatorSet.HomMat2dIdentity(out datumTransform)` fallback으로 교체 — 기존 TryMeasure 호출 흐름 유지
  - Plan 03에서 Fixture TryRunDatumPhase 결과 주입 예정 (TODO 주석)
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs`
  - `AddShot()`의 `shot.Datum = new DatumConfig(shot)` 제거
  - `Save()/Load()`의 `SHOT_{s}_DATUM` 섹션 처리 제거 (Plan 03에서 Fixture 포맷 재설계)
- `WPF_Example/UI/ControlItem/InspectionListViewModel.cs`
  - `CreateSequenceNode`의 Shot 자식 Datum 노드 생성 블록 제거
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs`
  - `AddShot` UI 경로의 Shot 자식 Datum 노드 생성 블록 제거

## Verification
- `msbuild /p:Configuration=Debug /p:Platform=x64` → 성공
- `DatumMeasurement.exe` 생성 확인
- `grep shot.Datum|ShotParam.Datum|ShotConfig.Datum` → 코드 참조 없음 (주석 1건만 존재)
- InspectionSequence API 6종 (`DisplayName`, `DatumConfigs`, `AddDatum`, `RemoveDatum`, `TryRunDatumPhase`, `TryGetDatumTransform`) 확인
- DatumConfig 신규 3프로퍼티 확인
- ShotConfig 조명 8필드 확인

## Design Notes
- **HTuple identity fallback**: Action_FAIMeasurement는 Datum 참조를 완전히 끊고 `HomMat2dIdentity`로 교체하여 기존 FAIEdgeMeasurementService.TryMeasure 시그니처를 유지. Plan 03에서 부모 InspectionSequence의 `_datumTransforms` dictionary를 주입받도록 재설계 예정. 현재는 기능상 Datum 보정 없이 동작 (일시적 회귀 허용 — Plan 03 의존).
- **조명 필드 8개**: bool+int 쌍이므로 ParamBase 자동 INI 직렬화로 충분. 추가 Save/Load 코드 불필요. 실제 하드웨어 제어는 D-12 범위 외.
- **TryRunDatumPhase 단일 책임**: DatumConfigs가 비면 즉시 true (D-10 pass-through). 하나라도 실패하면 명확한 에러 메시지로 즉시 false — T-06-05 DoS 완화.
- **TryGetDatumTransform 빈 datumRef**: null/빈 문자열은 identity 반환. "무보정 측정"을 명시적으로 지원. `_datumTransforms`에 없는 datumRef는 false 반환 (호출자가 에러 처리).
- **GetDisplayName**: `DisplayName` 필드가 빈 문자열이면 `Name`(SequenceBase.Name)을 반환. UI가 여기만 호출하면 뒤에 이름 편집 기능을 붙이기 쉬움.
- **DatumConfig.ImageSourceMode string 타입**: enum 대신 string — ParamBase가 enum 직렬화를 지원하지 않아 string으로 결정. T-06-04는 Plan 03 Load 시점에서 "Dedicated"/"ReuseFromShot" 외 값 fallback으로 처리 예정.
- **Owner wiring**: `AddDatum` 내부에서 `new DatumConfig(this)`로 InspectionSequence가 owner가 되도록 설정. ParamBase.Load가 reflection으로 owner를 참조할 수 있음.

## Deferred (Plan 03/04)
- InspectionRecipeManager의 Fixture 단위 Datum INI 포맷 설계 + 실제 Save/Load
- Action_FAIMeasurement의 Datum transform 주입 흐름 재설계 (부모 Sequence의 TryRunDatumPhase 호출 → dictionary 전달)
- InspectionListView(ViewModel)의 Sequence 자식 Datum 노드 렌더링
- Datum `ImageSourceMode` 런타임 처리 (Dedicated vs ReuseFromShot 이미지 캡처 흐름)
- Datum `ImageSourceMode` Load 검증 (T-06-04)

## Environment Note
- 새 worktree에는 `WPF_Example/bin/x64/Debug`에 HintPath 참조 DLL들(PropertyTools.dll, WPF.MDI.dll, halcondotnet.dll 등)이 없어 XAML 마크업 컴파일이 MC3074 에러를 냄. 메인 프로젝트 bin 디렉토리에서 DLL을 복사한 후 빌드 성공. 프로젝트가 csproj의 HintPath를 `bin\x64\Debug\`로 걸어두는 특이 구조 때문 — 후속 worktree 사용 시 동일 절차 필요.

## Deviations from Plan
**None** — 플랜이 지시한 대로 DatumConfig 확장 + InspectionSequence Fixture 구조 + ShotConfig Datum 제거/조명 추가 + 4개 caller 수정 모두 완료. Task 2의 "다른 파일에서 `shot.Datum` 참조" grep 단계에서 플랜에 명시되지 않은 `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs`를 추가로 발견하여 동일 패턴(주석 처리 후 TODO)으로 수정 — Rule 3 (blocking issue, 컴파일 에러).

## Commits
- `35f5eda feat(06-02): Datum Fixture 레벨 승격 + ShotConfig 조명 필드 + DatumConfig 확장`
- `1f5011c fix(06-02): ShotConfig.Datum 제거에 따른 컴파일 에러 해소`

## Self-Check: PASSED
- DatumConfig.cs: DatumName/ImageSourceMode/ReuseFromShotName 프로퍼티 존재 FOUND
- InspectionSequence.cs: DisplayName/DatumConfigs/TryRunDatumPhase/TryGetDatumTransform/AddDatum/RemoveDatum/GetDisplayName 존재 FOUND
- ShotConfig.cs: Datum 프로퍼티 제거 + Ring/Back/Coax/Side Enabled+Brightness 8필드 존재 FOUND
- Action_FAIMeasurement.cs/InspectionRecipeManager.cs/InspectionListViewModel.cs/InspectionListView.xaml.cs: `shot.Datum`/`ShotParam.Datum` 코드 참조 없음 FOUND
- Commits 35f5eda, 1f5011c FOUND in git log
- Build: msbuild Debug x64 → 성공 (DatumMeasurement.exe 생성) FOUND
