---
phase: 52-datum-2026-06-16-poc-1
plan: 04
type: human-uat
status: partial
requirements: [LEVEL-01]
created: 2026-06-17
updated: 2026-06-17
summary:
  total: 5
  passed: 0
  failed: 0
  pending: 5
---

# Phase 52: 이미지 수평 보정 (Datum 에지 기반 회전 정렬) — SIMUL UAT

> **목적:** SIMUL 모드에서 이미지 수평 보정(레벨링) 전 경로를 사용자 육안 검증하고 Phase 52 를 sign-off 한다.
> 회전 정렬 동작(부호/방향), 회전된 단일 이미지로 Datum 검출+측정 동시 동작(D-02 원안), off 회귀 0, 측정값 변화를 사용자가 시각 확인한다.
>
> **ROADMAP Success Criteria:** 기울어진 입력 이미지가 수평 정렬된 후 측정 / 기존 측정 회귀 0.

## 빌드 검증 결과 (Task 1 자동)

| 항목 | 결과 |
|------|------|
| 빌드 명령 | `MSBuild DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Rebuild` |
| MSBuild 경로 | `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe` (vswhere 탐색) |
| 빌드 PASS/FAIL | **PASS** (`bin\x64\Debug\DatumMeasurement.exe` 생성) |
| 에러 수 | **0** |
| 경고 수 (전체) | **6** (모두 pre-existing baseline) |
| 신규 에러 수 (Phase 52 파일) | **0** |
| 신규 경고 수 (Phase 52 파일) | **0** |

**경고 baseline 분석 (전 6건, Phase 22 baseline 동일 — 신규 0):**

| # | 위치 | 코드 | 내용 | 출처 |
|---|------|------|------|------|
| 1 | `Custom/Sequence/Bottom/Sequence_Bottom.cs(30,38)` | CS0618 | `BottomSequence` deprecated | Phase 33 마이그레이션 (pre-existing) |
| 2 | `Custom/Sequence/Top/Sequence_Top.cs(19,35)` | CS0618 | `TopSequence` deprecated | Phase 33 마이그레이션 (pre-existing) |
| 3 | `Custom/Sequence/SequenceHandler.cs(68,30)` | CS0618 | `TopInspectionAction` deprecated | Phase 33 마이그레이션 (pre-existing) |
| 4 | `Custom/Sequence/SequenceHandler.cs(70,30)` | CS0618 | `TopInspectionAction` deprecated | Phase 33 마이그레이션 (pre-existing) |
| 5 | `Custom/Sequence/SequenceHandler.cs(72,30)` | CS0618 | `BottomInspectionAction` deprecated | Phase 33 마이그레이션 (pre-existing) |
| 6 | `Device/Camera/VirtualCamera.cs(237,13)` | CS0162 | 접근할 수 없는 코드 (unreachable) | pre-existing |

> Phase 52 가 수정한 파일(InspectionSequence.cs / DatumConfig.cs / InspectionRecipeManager.cs / DatumFindingService.cs / VisionAlgorithmService.cs / Action_FAIMeasurement.cs)에서 발생한 경고는 **0건**. baseline 대비 신규 에러/경고 0. (MSB3884 ruleset 경고는 빌드 인프라 항목으로 제외.)

---

## Wave 1~3 구현 요약 (검증 대상)

Phase 52 이미지 수평 보정 (Datum 에지 기반 회전 정렬) 전 경로:

- **52-01:** 시퀀스 단위 `LevelingEnabled` 토글 + FIXTURE 섹션 INI save/load (기본 off, 키 미존재 폴백 off) + `DatumConfig.IsLevelingReference` 기준 Datum 플래그 + 레벨링 각도 캐시 멤버 (`_levelingAngleRad`/`_levelingComputed` + Set/Reset/getter).
- **52-02:** `DatumFindingService.TryGetLevelingAngle` (수평 2-ROI concat 피팅 라인 각도 radian 산출, MIN_HORIZONTAL_EDGES 가드) + `VisionAlgorithmService.RotateImageByAngle` (`hom_mat2d_rotate`(이미지 중심) + `AffineTransImage`, adapt_image_size=false 고정 크기, 근사0/예외 시 원본 복사 폴백).
- **52-03:** `EStep.Level` (MoveZ→Level→DatumPhase→Grab→Measure) 에서 기준 Datum 으로 각도 시퀀스당 1회 산출(`TryComputeLevelingAngle`, D-03 캐시) + DatumPhase(1-image+DualImage)·Grab 양쪽에 동일 `-LevelingAngleRad`·동일 회전중심 적용 → 회전된 단일 이미지로 Datum 검출 + 전 FAI 측정 동작 (D-02 원안). `LevelingEnabled && LevelingComputed` 게이트 → off/미산출 pass-through (회귀 0). taught ROI 좌표 미변환(방식 a, 소각도 가정).

---

## SIMUL UAT 테스트 (사용자 육안 검증 대기)

> SIMUL_MODE 빌드(`bin\x64\Debug\DatumMeasurement.exe`)로 앱 실행 후 각 Test 수행. 각 Test 결과(PASS/FAIL + 비고)를 보고.

### Test 1 — LevelingEnabled=off 기본 회귀 (기존 레시피 측정값 동일)

**절차:** 기존 레시피 로드 → 임의 SHOT 검사 → 측정값 기록. LevelingEnabled 미설정(기본 off) 상태에서 측정값이 Phase 51 baseline 과 동일한지 확인. (기존 레시피 INI 에 레벨링 키 없음 → off 폴백)

**기대 결과:** 기존 레시피 측정값 변화 0 (회귀 0). Datum 검출/측정 정상.

- **결과:** PENDING
- **비고:**

---

### Test 2 — 레벨링 동작 + 회전 이미지로 Datum 검출 (D-02 원안 핵심)

**절차:** 한 시퀀스(예: Top)의 Datum 1개를 PropertyGrid 에서 `IsLevelingReference = true` 지정 + 시퀀스 `LevelingEnabled = on` (FIXTURE 토글). 기울어진 입력 이미지로 검사 실행 → 결과 화면에서 확인:
  - (a) 이미지가 수평 정렬되어 표시되는지
  - (b) Datum 수평 라인 오버레이가 수평에 가깝게 정렬되는지
  - (c) **회전된 이미지에서 Datum 검출이 성공하는지 (taught ROI 가 회전 이미지 에지를 덮음)**

**기대 결과:** 기울어진 이미지가 수평 정렬된 후, 동일 수평 정렬 이미지로 Datum 검출과 측정이 모두 동작 (D-02 원안).

- **결과:** PENDING
- **비고:**

---

### Test 3 — 회전 방향 부호 확인

**절차:** Test 2 에서 회전 방향이 기울기를 **상쇄**하는 방향인지 확인. 만약 반대로 더 기울어지면 → `Action_FAIMeasurement` Grab + DatumPhase 의 `-LevelingAngleRad` 부호를 양쪽 동시에 `+` 로 반전하는 hotfix 필요 (보고).

**기대 결과:** 회전 방향 = 기울기 상쇄(올바른 방향). Datum/측정 동일 부호.

- **결과:** PENDING
- **비고:**

---

### Test 4 — 기준 Datum 미지정/검출 실패 시 무회전 폴백

**절차:** `IsLevelingReference` 를 전부 끄거나 기준 Datum 검출이 실패하도록 ROI 를 잘못 둔 뒤 `LevelingEnabled=on` 으로 검사 → 앱이 abort/crash 없이 무회전으로 Datum 검출+측정 진행하는지 + 로그에 "[Leveling] ... 무회전 진행" 류 출력되는지 확인.

**기대 결과:** abort/crash 없음. 무회전(lenient pass-through)으로 Datum 검출+측정 정상 진행. 로그 출력 확인.

- **결과:** PENDING
- **비고:**

---

### Test 5 — INI save/load 영속

**절차:** `LevelingEnabled=on` + `IsLevelingReference` 설정 후 레시피 저장 → 앱 재시작 → 재로드 시 설정 유지 확인. CameraRole 전환(TopBottom ↔ Side) 후 저장 시 비활성 시퀀스 Datum/레벨링 키 소실 없는지 확인 (MEMORY `recipe_datum_loss_camerarole` 회귀 가드).

**기대 결과:** 저장/재로드 시 LevelingEnabled/IsLevelingReference 유지. 기존 레시피(키 없음)는 off 폴백. CameraRole 전환 후 비활성 시퀀스 Datum/레벨링 키 소실 0.

- **결과:** PENDING
- **비고:**

---

## Sign-off

- **status:** partial (Task 1 빌드 검증 완료, 사용자 SIMUL 육안 검증 대기)
- **sign-off 조건:** 5 Test 전 항목 PASS 또는 사용자 합의 시 `status: signed_off`. FAIL 시 hotfix 후 재검증. carry-over 발생 시 CO-52-XX 등재.
