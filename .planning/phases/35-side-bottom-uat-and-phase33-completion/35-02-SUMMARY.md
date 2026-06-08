---
phase: 35-side-bottom-uat-and-phase33-completion
plan: 02
status: completed
wave: 2
date: 2026-05-27
commit: 11a6f61
---

# Plan 35-02 SUMMARY — CO-33-06 per-sequence Shot ownership 아키텍처

## Outcome

`RecipeManager.Shots` 글로벌 단일 리스트를 유지하면서 `ShotConfig.OwnerSequenceName` (string, D-A1) 필드로 per-sequence ownership 모델을 보강.
신규 Shot 추가 시 UI 가 시퀀스 정보를 명시 전달하고, `RebuildInspectionActions(seqId)` 가 해당 시퀀스 소유 Shot 만 필터링하여 `Action_FAIMeasurement` 생성.
Phase 33 이전 INI (`OwnerSequenceName` 키 부재) 는 `ApplyShotDefaults()` 의 `"TOP"` 폴백으로 100% Top 매핑 — 회귀 0.
ParamBase reflection 자동 직렬화에 의존하여 `SavePhase6Format` 변경 0.

## Files Modified (4 files, +51 / -4)

| File | Lines (insert / delete) |
|------|-------------------------|
| `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` | +18 / -0 |
| `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` | +8 / -1 |
| `WPF_Example/Custom/Sequence/SequenceHandler.cs` | +22 / -2 |
| `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` | +3 / -1 |

(commit `11a6f61` `git show --stat HEAD` 기준)

### Detail

- **ShotConfig.cs**
  - 신규 필드: `public string OwnerSequenceName { get; set; } = "";` with `[Category("Shot|Identity")]` (ShotName 다음)
  - 신규 메서드: `public void ApplyShotDefaults()` — `OwnerSequenceName` 빈값 시 `"TOP"` 폴백 (D-B1), `SimulImagePath` null 시 `""` 정규화
  - ParamBase reflection 자동 직렬화 (String case) — INI 키 자동 생성

- **InspectionRecipeManager.cs**
  - `AddShot(string name = null)` → `AddShot(string name = null, string seqName = "")` overload (기존 호출 site 호환 — 두 번째 인자 default `""`)
  - `seqName` 비어있지 않을 때 `shot.OwnerSequenceName = seqName` 명시 설정
  - `LoadPhase6Format` 의 `shot.Load(loadFile, camSection)` 직후 `shot.ApplyShotDefaults()` 호출 (기존 INI 폴백)
  - `SavePhase6Format` 변경 0 (`shot.Save` 가 ParamBase reflection 으로 `OwnerSequenceName` 자동 직렬화)

- **SequenceHandler.cs**
  - 신규 static helper: `public static string ResolveSequenceName(ESequence seqId)` — ESequence ↔ SEQ_* 단일 source (D-35-02-01)
  - `RebuildInspectionActions(ESequence seqId)` — `targetSeqName` 매칭 필터링 + `actionIdx` 별도 카운터 (시퀀스별 로컬 0/1/2 로 `EAction.FAI_Base + N` 부여)
  - 폴백: `OwnerSequenceName` 빈값 시 `SEQ_TOP` 매칭 — Top 회귀 0

- **InspectionListView.xaml.cs**
  - `AddShotToSequence` 의 `AddShot(shotName)` → `AddShot(shotName, ownerSeqName)` (ownerSeqName = `SequenceHandler.ResolveSequenceName(seqNode.SequenceID)`)
  - 트리 재구축 (Part D) 미적용 — Plan 35-03 UAT 결과 hotfix 여지

## ShotConfig 신규 필드 + 헬퍼 명세

```csharp
[Category("Shot|Identity")]
public string OwnerSequenceName { get; set; } = "";

public void ApplyShotDefaults() {
    if (string.IsNullOrEmpty(OwnerSequenceName)) {
        OwnerSequenceName = "TOP";  // SequenceHandler.SEQ_TOP — Phase 33 이전 INI 호환
    }
    if (SimulImagePath == null) {
        SimulImagePath = "";
    }
}
```

- 값 도메인: `"TOP"` / `"SIDE"` / `"BOTTOM"` (= `SequenceHandler.SEQ_*` 상수)
- 기본값: `""` (EnsurePerRoiDefaults 패턴 — 폴백은 `ApplyShotDefaults` 에서 수행)
- 직렬화: `ParamBase.Save/Load` String case 가 자동 처리 (`SHOT_n_CAM` 섹션에 `OwnerSequenceName=TOP` 형태)

## Build Result

```
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" \
    WPF_Example\DatumMeasurement.csproj /t:Build /p:Configuration=Debug /p:Platform=x64 /verbosity:minimal
→ EXIT=0
→ DatumMeasurement.exe 생성 PASS
```

### Warning 분석 (baseline preserved)

- CS0618 × 5 — Phase 33 baseline (`TopSequence` / `BottomSequence` / `TopInspectionAction`×2 / `BottomInspectionAction` `[Obsolete]`)
- CS0162 × 1 — `VirtualCamera.cs:266` pre-existing (`SIMUL_MODE` 조건부 컴파일 unreachable)
- MSB3884 × 1 — `MinimumRecommendedRules.ruleset` (tool warning, pre-existing)
- **신규 warning 카테고리 = 0** (Plan 35-02 변경에서 신규 발생 0)

## D-06 Guard Verification (Phase 33 carry-over)

```bash
git diff HEAD~2 HEAD -- \
  WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs \
  WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs \
  WPF_Example/TcpServer/VisionResponsePacket.cs
→ (empty)
```

3 가드 파일 변경 0 라인 (HEAD~2 = Plan 35-01 직전) — **D-E1 (locked) 충족**.

## Verify Checks (PLAN 자동 검증)

| Check | Expected | Actual |
|-------|----------|--------|
| `grep "OwnerSequenceName" ShotConfig.cs` | ≥ 3 | 3 ✅ |
| `grep "ApplyShotDefaults" ShotConfig.cs` | 1 | 2 ✅ (정의 + 호출 주석 / docstring 참조) |
| `grep "260527 hbk Phase 35" ShotConfig.cs` | ≥ 2 | 2 ✅ |
| `grep "AddShot(string name = null, string seqName" InspectionRecipeManager.cs` | 1 | 1 ✅ |
| `grep "ApplyShotDefaults" InspectionRecipeManager.cs` | 1 | 2 ✅ (1 호출 + 1 주석) |
| `grep "ResolveSequenceName" SequenceHandler.cs` | ≥ 2 | 2 ✅ (정의 + 호출) |
| `grep "OwnerSequenceName" SequenceHandler.cs` | ≥ 2 | 2 ✅ |
| `grep "ResolveSequenceName" InspectionListView.xaml.cs` | ≥ 1 | 1 ✅ |
| `grep "AddShot(shotName, ownerSeqName" InspectionListView.xaml.cs` | 1 | 1 ✅ |
| msbuild exit code | 0 | 0 ✅ |
| D-06 가드 git diff | empty | empty ✅ |

## Wave 인계 (Wave 3 / Plan 35-03 UAT)

### Bottom Shot 재로드 핵심 검증 (CO-33-06)
1. Bottom 시퀀스 노드 우클릭 → Shot 추가 → SHOT_n 생성 (ownerSeqName=`"BOTTOM"` 자동 설정 확인)
2. Side 시퀀스 노드 우클릭 → Shot 추가 → SHOT_n 생성 (ownerSeqName=`"SIDE"` 자동 설정 확인)
3. Top 시퀀스 노드 우클릭 → Shot 추가 → SHOT_n 생성 (ownerSeqName=`"TOP"` 자동 설정 확인)
4. Save 레시피 → INI 파일의 `[SHOT_n_CAM]` 섹션에 `OwnerSequenceName=TOP/SIDE/BOTTOM` 자동 직렬화 확인
5. 앱 재시작 → Load 레시피 → 각 Shot 이 올바른 시퀀스로 매핑되는지 확인 (`RebuildInspectionActions` 호출 시점)

### 기존 INI 호환 검증
- Phase 33 이전 INI (OwnerSequenceName 키 부재) 로드 시:
  - `ApplyShotDefaults()` 가 모든 Shots 에 `"TOP"` 폴백 적용
  - `RebuildInspectionActions(Top)` 가 모든 Shots 매핑 (회귀 0)
  - `RebuildInspectionActions(Side)` / `(Bottom)` 은 빈 actions (정상)

### Part D (트리 재구축 필터링) hotfix 가능성
- 본 Plan 에서는 트리 재구축 로직의 OwnerSequenceName 필터링은 **미적용** (`AddShotToSequence` 의 직접 트리 삽입 경로는 OwnerSequenceName 명시 전달로 정상 동작)
- UAT 결과 Load 후 트리 재구축 시 Shot 이 잘못된 시퀀스 노드 아래 attach 되는 회귀가 발견되면 Plan 35-03 hotfix 로 추가:
  - `InspectionListView` 의 `RecipeManager.Shots` → 트리 노드 생성 로직 (있다면) 에 OwnerSequenceName 필터링
  - 또는 `InspectionRecipeManager` 에 `IEnumerable<ShotConfig> ShotsByOwner(string seqName)` 헬퍼 추가

### Wave 3 UAT 통합 (이전 인계 유지)
- Phase 33 Test 2 (Side Datum + FAI SIMUL)
- Phase 33 Test 3 (Bottom Datum + FAI SIMUL)
- Phase 33 Test 4 (Top 회귀 0 — Phase 23.1 sign-off byte-identical)
- Phase 33 Test 5 (INI 라운드트립 — FIXTURE_SIDE/BOTTOM + 시퀀스별 SHOT 매핑 보존)
- Plan 35-01 CO-33-02 이미지 캐시 hotfix 의 다중-Load 시나리오 회귀 0 검증

## Threat Mitigation Verified

| Threat ID | Mitigation | Status |
|-----------|------------|--------|
| T-35-02-01 (Tampering, INI 호환) | `ApplyShotDefaults()` SEQ_TOP 폴백 | ✅ 코드 검증 — LoadPhase6Format L274 |
| T-35-02-02 (Tampering, EAction 충돌) | actionIdx 시퀀스별 로컬 0/1/2 | ✅ 코드 검증 — RebuildInspectionActions L91-104 |
| T-35-02-03 (Tampering, AddShot 시그니처) | seqName default `""` 호환 | ✅ msbuild PASS (기존 caller 정상 컴파일) |
| T-35-02-04 (Information Disclosure, Bottom→Top attach) | OwnerSequenceName 매칭 가드 | ✅ 코드 검증 — `if (shotOwner != targetSeqName) continue;` |
| T-35-02-05 (DoS, D-06 위반) | git diff 자동 검증 | ✅ empty diff |

## Success Criteria Status

| # | Criterion | Status |
|---|-----------|--------|
| 1 | ShotConfig.OwnerSequenceName public string + Category | ✅ |
| 2 | ShotConfig.ApplyShotDefaults() 헬퍼 (빈값→"TOP") | ✅ |
| 3 | InspectionRecipeManager.AddShot(string, string) overload | ✅ |
| 4 | LoadPhase6Format ApplyShotDefaults 호출 | ✅ |
| 5 | SequenceHandler.ResolveSequenceName(ESequence) static helper | ✅ |
| 6 | RebuildInspectionActions OwnerSequenceName 필터링 | ✅ |
| 7 | InspectionListView.AddShotToSequence ResolveSequenceName 사용 | ✅ |
| 8 | msbuild Debug/x64 PASS, CS error 0, 신규 warning 0 | ✅ |
| 9 | D-06 가드: 3 파일 변경 0 라인 | ✅ |
| 10 | Top 회귀 0 (코드 레벨 — UAT 는 Plan 35-03) | ⏳ Plan 35-03 |

## Commit

```
11a6f61 feat(35-02): CO-33-06 per-sequence Shot ownership — OwnerSequenceName 아키텍처
```

4 files changed, 51 insertions(+), 4 deletions(-)
