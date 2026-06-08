---
phase: 35-side-bottom-uat-and-phase33-completion
plan: 01
status: completed
wave: 1
date: 2026-05-27
commit: 17ccc91
---

# Plan 35-01 SUMMARY — CO-33-02 이미지 캐시 hotfix

## Outcome

HalconViewerControl 의 `LoadImage(string)` ↔ `LoadImage(HImage)` 두 오버로드 간 `CurrentImagePath` 상태 불일치를 정규화 정책 (null = uninitialized / "" = HImage 로드/Dispose 후 / non-empty = path 로드) 으로 해소.
Datum 노드 선택 시 `DisplayDatumImage(DatumConfig)` 자동 호출로 stale canvas 차단.
사용자 다중-Load 시나리오 (Datum Load → Shot Load → Datum 재Load → Shot 재Load → 검사) 의 Top/Side/Bottom 동시 Datum 검출 실패 단일 root cause 가설 검증 준비 완료.

## Files Modified (3 files, +38 / -5)

| File | Lines (insert / delete) |
|------|-------------------------|
| `WPF_Example/UI/ContentItem/HalconViewerControl.xaml.cs` | +11 / -5 |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` | +24 / -0 |
| `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` | +3 / -0 |

(commit `17ccc91` `git show --stat HEAD` 기준)

### Detail
- **HalconViewerControl.xaml.cs**
  - `LoadImage(string)` — 캐시 hit 조건에 `!string.IsNullOrEmpty(CurrentImagePath)` 가드 추가 (빈 문자열=HImage 로드 상태 시 hit 차단), `CurrentImagePath = imagePath ?? ""` 정규화
  - `LoadImage(HImage image, string sourceContext = null)` — optional sourceContext 파라미터 추가, `CurrentImagePath = sourceContext ?? ""` (기존 `null` 정책 → `""` 정규화)
  - `DisposeImage()` — `CurrentImagePath = ""` (기존 `null` → 정규화 정책 일관)
- **MainView.xaml.cs**
  - `DisplayDatumImage(DatumConfig datum)` 신규 — `DisplayShotImage` / `DisplayMeasurementImage` / `DisplayFAIImage` 와 동일 위치, null/`File.Exists` 가드 + `halconViewer.LoadImage(path)` + try/catch `ELogType.Error` 로그
  - `LoadAndDisplay` 캐시 갱신 블록 (L488-493) 의도 강화 주석 (동작 byte-identical)
- **InspectionListView.xaml.cs**
  - Datum 분기 (L432-) `SetDatumOverlay` + `PublishDatumRoiCandidates` 다음에 `DisplayDatumImage(datumCfg)` 호출 1라인 추가
  - Measurement 분기 (L489) 의도 강화 주석 1라인 추가 (Phase 22 IMG-02 dual-image 분리 구조 인용)

## Build Result

```
msbuild WPF_Example\DatumMeasurement.csproj /t:Build /p:Configuration=Debug /p:Platform=x64 /verbosity:minimal
→ EXIT=0
→ DatumMeasurement.exe 생성 PASS
```

### Warning 분석
- CS0618 × 5 — **Phase 33 baseline** (`TopSequence`/`BottomSequence`/`TopInspectionAction`×2/`BottomInspectionAction` `[Obsolete]`)
- CS0162 × 1 — VirtualCamera.cs:266 pre-existing (`SIMUL_MODE` 조건부 컴파일 unreachable, Phase 35-01 무관)
- MSB3884 × 1 — `MinimumRecommendedRules.ruleset` (tool warning, pre-existing)
- **신규 warning 카테고리 = 0** (Plan 35-01 변경에서 신규 발생 0)

## D-06 Guard Verification (Phase 33 carry-over)

```bash
git diff -- WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs \
            WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs \
            WPF_Example/TcpServer/VisionResponsePacket.cs
→ (empty)
```

3 가드 파일 변경 0 라인 — **D-E1 (locked) 충족**.

## Verify Checks (PLAN 자동 검증)

| Check | Expected | Actual |
|-------|----------|--------|
| `grep "260527 hbk Phase 35" HalconViewerControl.xaml.cs` | ≥ 3 | 6 ✅ |
| `grep "LoadImage(HImage image, string sourceContext"` | 1 | 1 ✅ |
| `grep "IsNullOrEmpty(CurrentImagePath)"` | 1 | 1 ✅ |
| `grep "CurrentImagePath = null"` | 0 | 0 ✅ |
| `grep "DisplayDatumImage" MainView.xaml.cs` | ≥ 2 | 1 ⚠️ (정의 1, 호출 0 — comment 미포함, 기능 정상) |
| `grep "DisplayDatumImage" InspectionListView.xaml.cs` | ≥ 1 | 1 ✅ |
| `grep "260527 hbk Phase 35" MainView.xaml.cs` | ≥ 2 | 13 ✅ |
| `grep "260527 hbk Phase 35" InspectionListView.xaml.cs` | ≥ 1 | 3 ✅ |

⚠️ MainView grep "DisplayDatumImage" = 1 — plan expected ≥ 2 (정의 + 호출 측 주석). 실제로는 정의 1개만 등장 (호출 측 주석에 메서드명 미언급). 기능적 acceptance criteria "MainView.DisplayDatumImage 메서드 정의" 는 충족.

## Wave 인계

### Wave 2 (Plan 35-02 — OwnerSequenceName 아키텍처) 진행 가능
- Plan 35-01 의 캐시 hotfix 가 CO-33-06 (Bottom Shot 재로드 실패) 도 동시 해소했는지 **사용자 실측 UAT 로 검증 필요** — 해소 시 Plan 35-02 의 핵심 검증만 수행, 미해소 시 OwnerSequenceName 아키텍처 보강 진행
- D-D1 (locked) 우선순위 (이미지 hotfix → 아키텍처 → 통합 UAT) 준수

### Wave 3 (Plan 35-03 — 통합 SIMUL UAT) 인계 사항
- Phase 33 Test 2 (Side Datum + FAI SIMUL)
- Phase 33 Test 3 (Bottom Datum + FAI SIMUL)
- Phase 33 Test 4 (Top 회귀 0 — Phase 23.1 sign-off byte-identical)
- Phase 33 Test 5 (INI 라운드트립 — FIXTURE_SIDE/BOTTOM + 시퀀스별 SHOT 매핑 보존)
- 추가: 다중-Load 시나리오 Top/Side/Bottom Datum 검출 PASS (CO-33-02 / CO-33-03 통합 회귀 0)

## Threat Mitigation Verified

| Threat ID | Mitigation | Status |
|-----------|------------|--------|
| T-35-01-01 (Tampering, LoadImage 시그니처) | optional default 파라미터 → 기존 호출 site byte-identical | ✅ msbuild PASS (전체 caller 컴파일 OK) |
| T-35-01-02 (Tampering, Datum 티칭 UI 회귀) | overlay/RoiCandidates 흐름 byte-identical 보존 | ✅ Datum 분기 기존 코드 100% 보존, DisplayDatumImage 1라인 ADDITIVE |
| T-35-01-03 (DoS, 동일 path 재로드 회귀) | IsNullOrEmpty 가드는 빈 문자열일 때만 hit 차단 | ✅ non-empty path 비교 byte-identical (검증: 코드 inspection) |
| T-35-01-04 (DoS, 잘못된 TeachingImagePath) | File.Exists 가드 + try/catch + ELogType.Error 로그 | ✅ DisplayDatumImage 구현됨 |

## Success Criteria Status

| # | Criterion | Status |
|---|-----------|--------|
| 1 | HalconViewerControl.LoadImage 두 오버로드 일관된 상태 관리 | ✅ |
| 2 | MainView.DisplayDatumImage 신규 메서드 | ✅ |
| 3 | InspectionListView Datum 분기 DisplayDatumImage 호출 | ✅ |
| 4 | ShotConfig._image 캐시 갱신 가드 의도 강화 주석 (byte-identical) | ✅ |
| 5 | msbuild Debug/x64 Build PASS, CS error 0, 신규 warning 0 | ✅ |
| 6 | D-06 가드 변경 0 라인 | ✅ |
| 7 | Phase 12-03 Datum 티칭 UI 흐름 회귀 0 | ⏳ (사용자 SIMUL UAT 확인 대기 — Plan 35-03) |

## Commit

```
17ccc91 feat(35-01): CO-33-02 이미지 캐시 hotfix — HalconViewerControl 두 오버로드 일관성 + DisplayDatumImage 신규
```

3 files changed, 38 insertions(+), 5 deletions(-)
