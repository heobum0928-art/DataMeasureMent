---
phase: 31-datum-algorithm
plan: "05"
subsystem: verification + uat-signoff
tags: [uat, signoff, phase-31, datum-algorithm, D-01, D-05, D-07, D-08, CO-23.1-01, CO-23.1-02]

# Dependency graph
requires:
  - phase: 31-01
    provides: "Foundation (IDatumOriginConsumer + ComputeProjectionDistance/TryFitArc/TryIntersectCircleLine helpers + EdgeToLineDistance/Action_FAIMeasurement 일반화)"
  - phase: 31-02
    provides: "단순 신규 타입: CircleCenterDistance / EdgeToLineAngle / ArcEdgeDistance + MeasurementFactory"
  - phase: 31-03
    provides: "복합 신규 타입: ArcLineIntersectDistance / CompoundAngle / CompoundCenterCDistance / CompoundCenterBDistance + MeasurementFactory"
  - phase: 31-04
    provides: "UI ROI 버튼 화이트리스트 (7 신규 타입) + UpdateImageSourceLabel 듀얼 이미지 레이블"
  - phase: 32-sop-i9-i10-e2-e9-e10-e3
    provides: "I9/I10/E2/E9/E10 SOP 재정합 + E3 신규 (Phase 31 Test 3/4/5 transferred → SIGNED_OFF 2026-05-23)"
provides:
  - ".planning/phases/31-datum-algorithm/31-UAT.md — 9 테스트 최종 결과 + signed_off frontmatter (2026-05-26)"
  - "Phase 31 sign-off 결정 (7 신규 측정 타입 + 2 carry-over UI 항목 + 빌드 검증 모두 PASS / transferred)"
  - "CO-31-01 신규 carry-over — PropertyGrid 즉시 갱신 미작동 (Test 8 부수 발견)"
affects:
  - "Phase 22 (이미지 이중화 INI 직렬화): Test 8 검증 흐름이 TeachingImagePath/SimulImagePath 분리 UX 의 baseline"
  - "v1.1 milestone close: 측정 알고리즘 phase 모두 완료 (28/31/32) — 다음은 Phase 22/24/25/26/27 결정"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "측정 phase UAT 재이관 패턴: Phase 31 Test 3/4/5 가 SOP 불일치로 발견되어 Phase 32 신설 + 재작성 + 사용자 사인오프"
    - "Retro PASS 마킹 정당화: 코드 화이트리스트 grep + 후속 phase 의 사용자 행동 검증으로 보강"
    - "Carry-over 분리 원칙: Test 8 의 레이블 표시 자체 (CO-23.1-01) 와 PropertyGrid binding 이슈 (신규 CO-31-01) 를 명확히 분리"

key-files:
  created:
    - ".planning/phases/31-datum-algorithm/31-05-SUMMARY.md (이 파일)"
  modified:
    - ".planning/phases/31-datum-algorithm/31-UAT.md (frontmatter signed_off + Test 7 retro PASS + Test 8 PASS 3-step + CO-31-01 carry-over)"

key-decisions:
  - "Test 1/2/6 (E8/D1·H5/ArcEdge) — 즉시 PASS (2026-05-19~21 사용자 시각 검증, hotfix 다수 포함)"
  - "Test 3/4/5 (I9·I10/E2/E9·E10) — SOP 불일치 확인 후 Phase 32 신설 + 재작성 → Phase 32 SIGNED_OFF 2026-05-23 로 효력 발생"
  - "Test 7 (CO-23.1-02 ROI 버튼) — retro PASS 2026-05-26: MainView.xaml.cs FindSelectedRect/CircleMeasurement 화이트리스트 8 타입 등록 grep 확인 + Phase 31 Test 1/2/6 / Phase 32 UAT 흐름에서 행동 검증 / Phase 32 hotfix 15fa8d8 (E3 누락 수정) 으로 cap 검증 완료"
  - "Test 8 (CO-23.1-01 듀얼 이미지 레이블) — 사용자 3단계 시각 UAT 2026-05-26: A 티칭 이미지 표시 PASS / B 검사 이미지 교체 PASS / C 빈 경로 시 Collapsed PASS"
  - "CO-31-01 신규 carry-over: PropertyGrid 즉시 갱신 미작동 (Test 8 B 단계 부수 발견) — 레이블 자체와 별개의 UI binding/refresh 문제이므로 Phase 31 sign-off 차단 아님"
  - "Test 9 (msbuild) — 자동 검증 PASS (exit 0, 신규 warning 0, Phase 21 baseline 2 유지)"

patterns-established:
  - "복수 phase 분기 UAT 흐름: 원 phase 의 일부 test 가 후속 phase 로 이관되어 후속 phase 의 sign-off 가 원 phase 의 효력으로 인정되는 패턴 (Phase 31 ↔ Phase 32)"
  - "Phase 31 UAT 다단계 (코드 작성 → Test 1/2/6 사용자 PASS 다수 hotfix → Test 3/4/5 SOP 재정합 위해 Phase 32 신설 → Test 7 retro / Test 8 사용자 → final sign-off) — v1.1 milestone 의 대형 phase 의 표준 흐름"

requirements-completed: [D-01, D-05, D-07, D-08, CO-23.1-01, CO-23.1-02]

# Metrics
duration: 8 days (2026-05-19 ~ 2026-05-26)
completed: 2026-05-26
---

# Phase 31 Plan 05: Final Verification + UAT Sign-off Summary

**Phase 31 SIGNED_OFF 2026-05-26. 7 신규 측정 타입(E8/D1·H5/I9·I10/E2/E9·E10/ArcEdge) + carry-over 2건 (CO-23.1-01 듀얼 이미지 레이블 / CO-23.1-02 ROI 버튼 활성화) + 빌드 검증 모두 PASS 또는 transferred-to-Phase-32 (SIGNED_OFF). PropertyGrid 즉시 갱신 미작동은 별도 CO-31-01 carry-over 로 분리.**

## Performance

- **UAT 시작:** 2026-05-19 (Test 1 E8)
- **UAT 종료:** 2026-05-26 (Test 7 retro PASS + Test 8 사용자 PASS)
- **Tests:** 9 (7 측정 타입 + 2 UI carry-over + 1 빌드)
- **PASS:** 6 (Test 1/2/6/7/8/9)
- **Transferred (Phase 32 SIGNED_OFF):** 3 (Test 3/4/5)
- **FAIL:** 0
- **Carry-overs:** 1 신규 (CO-31-01 PropertyGrid 즉시 갱신)

## UAT Result Matrix

| Test | 측정 타입 / 항목 | Result | 검증 일자 | 비고 |
|------|------------------|--------|-----------|------|
| 1 | E8 CircleCenterDistance | ✅ PASS | 2026-05-19 | hotfix 3건 (0071d50 / 3d8b4fc / b526167) — 결과 overlay 빈 리스트 / datum 기준선 짧음 / X축 수직선 어긋남 |
| 2 | D1·H5 EdgeToLineAngle | ✅ PASS | 2026-05-20 | hotfix 1건 (f0842e4) — 시각 피드백 4건 통합 |
| 3 | I9·I10 ArcLineIntersect | 🔄 Phase 32 | 2026-05-23 | SOP 불일치 → Phase 32 SIGNED_OFF (2직선 교점 재작성 + 4-ROI 두 교점 평균 측정점 보정) |
| 4 | E2 CompoundAngle | 🔄 Phase 32 | 2026-05-23 | SOP 불일치 → Phase 32 SIGNED_OFF (공통 컨투어 알고리즘 + LargestRect XLD) |
| 5 | E9·E10 CompoundCenterC/B | 🔄 Phase 32 | 2026-05-23 | SOP 불일치 → Phase 32 SIGNED_OFF (E9/E10 CompoundCenterC.B 재작성) |
| 6 | ArcEdgeDistance (G 시리즈) | ✅ PASS | 2026-05-21 | hotfix 1건 (b7c34cf) — Manual Tools 잠금 / ComputeProjectionDistance 단순화 / 행렬 역변환 제거 |
| 7 | CO-23.1-02 ROI 버튼 활성화 | ✅ PASS_retro | 2026-05-26 | MainView 화이트리스트 8 타입 grep + Phase 31 Test 1/2/6 + Phase 32 UAT 흐름 행동 검증 |
| 8 | CO-23.1-01 듀얼 이미지 레이블 | ✅ PASS | 2026-05-26 | A 티칭 이미지 표시 / B 검사 이미지 교체 / C 빈 경로 Collapsed — 사용자 3단계 시각 검증 |
| 9 | MSBuild Debug/x64 | ✅ PASS | (자동) | exit 0, 신규 warning 0, Phase 21 baseline 2 유지 |

**Summary:** Total=9, PASS=6, Transferred(Phase32 signed_off)=3, FAIL=0.

## Phase 31 ↔ Phase 32 분기 흐름

Phase 31 의 Test 3/4/5 가 UAT 중 SOP 실무 방식과 불일치 확인됨:
- **ArcLineIntersect:** 원래 3점 호 피팅 알고리즘 → SOP 는 Rect 2개 직선 피팅 + HALCON `intersection_lines` 교점
- **E2/E9/E10:** 원래 CL1~3 원 + La/Lb 라인 체인 → SOP 는 공통 컨투어 알고리즘 (reduce_domain → edges_sub_pix canny → union_adjacent_contours_xld → smallest_rectangle2_xld → shape_trans_xld → 최대면적 LargestRect)
- **E3 (신규):** Phase 31 미포함 → Phase 32 에서 신설 (CompoundShortAxisDistance, 공차 0.600±0.030)

→ **Phase 32 신설 2026-05-21** (측정 알고리즘 SOP 재정합) → **8 plans 모두 완료 + SIGNED_OFF 2026-05-23**.

Phase 32 의 UAT (`32-UAT.md`) 가 Phase 31 의 Test 3/4/5 효력을 대체 충족 — `transferred_to_phase_32` 상태로 마킹.

## Test 7 Retro PASS 근거

| 근거 | 출처 |
|------|------|
| MainView.xaml.cs L1567-1598 `FindSelectedRectMeasurement` 화이트리스트 8 타입 등록 | EdgeToLineDistance / EdgeToLineAngle / ArcEdgeDistance / ArcLineIntersect / CompoundAngle / CompoundCenterC / CompoundCenterB / CompoundShortAxis |
| MainView.xaml.cs L1541-1564 `FindSelectedCircleMeasurement` CircleCenterDistance 포함 | CircleCenterDistance / CircleDiameter |
| Phase 31 Test 1 PASS — CircleCenterDistance Circle ROI 드로잉 행동 검증 | 31-UAT.md Test 1 |
| Phase 31 Test 2 PASS — EdgeToLineAngle Rect ROI 드로잉 행동 검증 | 31-UAT.md Test 2 |
| Phase 31 Test 6 PASS — ArcEdgeDistance Rect ROI 드로잉 행동 검증 | 31-UAT.md Test 6 |
| Phase 32 UAT — ArcLineIntersect/E2/E3/E9/E10 5종 ROI 드로잉 PASS | 32-UAT.md §1 ROI 티칭 UX |
| Phase 32 hotfix `15fa8d8` (E3 화이트리스트 누락 수정) — cap 검증 완료 | 32-UAT.md hotfix |

## Test 8 사용자 시각 UAT 결과

| 단계 | 시나리오 | 기대 | 결과 |
|------|----------|------|------|
| A | Datum 노드 → Load → jpg 선택 | 하단 라벨 "티칭 이미지: <경로>" 표시 | ✅ PASS |
| B | Shot 노드 → Load → 다른 jpg 선택 | 라벨 "검사 이미지: <경로>" 로 교체 | ✅ PASS (라벨 갱신 정상, PropertyGrid 만 별개 issue) |
| C | 이미지 미로드 노드 선택 | 라벨 Collapsed (사라짐) | ✅ PASS |

**부수 발견:** 단계 B 중 PropertyGrid 즉시 갱신이 안 되는 별도 binding/refresh 문제 관찰 → **CO-31-01 신규 carry-over** 로 분리 (레이블 자체 PASS 와 무관).

## Files Modified

- `.planning/phases/31-datum-algorithm/31-UAT.md` — frontmatter `status: signed_off` + `signed_off_date: 2026-05-26` + `signed_off_by` + `test_results` 9 항목 + `carry_overs` CO-31-01; Test 7 retro PASS + Test 8 사용자 PASS + 부수 발견 기록.
- `.planning/phases/31-datum-algorithm/31-05-SUMMARY.md` (이 파일 신설).

## Carry-overs

1. **CO-31-01 (신규, 이 phase 발생) — PropertyGrid 양방향 즉시 갱신 미작동**

   2026-05-26 사용자 보고로 두 방향 모두 확인됨:

   - **(a) Tree → PropertyGrid:** Test 8 단계 B 부수 발견. Shot 노드로 전환 시 `txt_imageSourceLabel` 라벨은 정상 갱신되나 우측 PropertyGrid 가 즉시 반영 안 됨.
   - **(b) PropertyGrid → Tree:** PropertyGrid 에서 `DatumName` / `ShotName` / `FAIName` / `MeasurementName` 변경 시 트리 노드 헤더가 즉시 안 바뀜.

   **Root cause:** Name 류 4개 프로퍼티 모두 plain `{ get; set; }` auto-property — `INotifyPropertyChanged.PropertyChanged` 발화 부재. `InspectionListView.xaml` L51 `EditableTextBlock Text="{Binding Name}"` 가 OneWay-effect 로 작동 (변경 푸시 없음).
   - `DatumConfig.cs:19` `public string DatumName { get; set; } = "Datum_1";`
   - `ShotConfig.cs:34` `public string ShotName { get; set; }`
   - `FAIConfig.cs:103` `public string FAIName { get; set; }`
   - `MeasurementBase.cs:21` `public string MeasurementName { get; set; } = "";`

   **Disposition 후보:**
   - v1.1 quick fix (Name 4종을 PropertyChanged 발화 setter 로 교체 + 각 클래스에 `INotifyPropertyChanged` 구현)
   - 또는 Phase 22 (이미지 이중화 — DatumConfig.TeachingImagePath INI 직렬화 시 PropertyChanged 패턴 도입 동시 처리)
   - 또는 Phase 26 (헝가리안 리팩토링 — 전체 식별자 일면 갱신 시 동시 처리)

## Phase 31 Goal Achievement

| 목표 | 달성 | 증거 |
|------|------|------|
| 7 신규 측정 타입 추가 + Datum 절대 좌표계 기준 거리/각도/교점 측정 | ✅ | E8/D1·H5/ArcEdge Phase 31 PASS + I9·I10/E2/E9·E10 Phase 32 PASS |
| EdgeToLineDistance 와 동일한 Shot-FAI 구조로 운용 가능 | ✅ | Test 7 retro PASS — 화이트리스트 + ROI 드로잉 행동 검증 |
| CO-23.1-01 (TeachingImagePath ≠ InspectionImagePath UI 구분) 흡수 | ✅ | Test 8 사용자 PASS 3 단계 |
| CO-23.1-02 (ROI 버튼 일반화) 흡수 | ✅ | Test 7 retro PASS |
| msbuild Debug/x64 PASS, 신규 warning 0 | ✅ | Test 9 자동 |

## Phase 31 SIGNED_OFF — Plan 05 결론

- Phase 31 의 신규 측정 타입 7종 + carry-over 2건 + 빌드 검증 = 9 항목 모두 PASS 또는 Phase 32 SIGNED_OFF transfer 로 효력 발생
- 사용자 sign-off (heobum0928@gmail.com) 2026-05-26
- v1.1 의 측정 알고리즘 phase 모두 완료 — Phase 28 (FAI CircleDiameter + Datum Circle) / Phase 31 (Datum 기준 측정 7종) / Phase 32 (SOP 재정합 + E3)
- 다음 행동지: **v1.1 의 비-알고리즘 phase 결정** — Phase 22 (이미지 이중화) / 24 (검사 워크플로우) / 25 (결과 분석) / 26 (헝가리안) / 27 (Side Inspection) 중 우선순위 정하거나 milestone close 검토

---
*Phase: 31-datum-algorithm*
*Plan: 05*
*Completed: 2026-05-26*
