# Phase 23: Top #1 A시리즈 Simul end-to-end — Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in 23-CONTEXT.md — this log preserves alternatives considered.

**Date:** 2026-05-11
**Phase:** 23-top-1-a-simul-end-to-end-2026-05-11
**Areas discussed:** Datum B/C 구성 전략, A1~A5 측정 알고리즘 (ALG-01), 확장성 검증 (A6~A23), Simul 이미지 + UI 결과 표시

---

## Area Selection

**Question:** Phase 23 에서 논의할 회색 영역을 선택해 주세요 (다중 선택 가능)

| Option | Description | Selected |
|--------|-------------|----------|
| Datum B/C 구성 전략 | EDatumAlgorithm 3종 중 PPT 매핑, Datum 1개 vs 2개 | ✓ |
| A1~A5 측정 알고리즘 (ALG-01 잠금) | EdgeToLineDistance 신규 vs PointToLineDistance 재사용 | ✓ |
| 확장성 검증 방식 (A6~A23) | 실제 추가 검증 vs 정적 패턴 검증 | ✓ |
| Simul 이미지 소스 + UI 결과 표시 | PPT 기반 이미지 출처 + 5개 strip 갱신 UAT | ✓ |

**User's choice:** 4 영역 전체 선택

---

## Area 1 — Datum B/C 구성 전략

### Q1.1 — Datum 표현 방식

| Option | Description | Selected |
|--------|-------------|----------|
| Datum 1개 — CTH 단독 (추천) | B1 홀 Circle + 2 horizontal lines, origin=center, Y축 자동 도출 | |
| Datum 2개 — B(TLI) + C(CTH) | Multi-Datum 패턴 (Phase 6 Rapid City) | |
| Datum 2개 — B(VTH 또는 단일 horizontal) + C(별도 수직 line) | 알고리즘 신규 추가 가능성 | |
| PPT 확인 후 결정 | researcher 위임 | ✓ |

**User's choice:** PPT 확인 후 결정 → researcher 가 PPT 매핑 후 lock-in
**Notes:** 이후 인접 결정(Y부호/Fixture범위/TeachingImagePath 소비)은 알고리즘 무관하게 잠금.

### Q1.2 — A1~A5 가 Datum B 위쪽일 때 측정값 부호

| Option | Description | Selected |
|--------|-------------|----------|
| +Y (위쪽 양수, 추천) | 공학 표준, PPT 공차 표기와 일치 | ✓ |
| +Y (아래쪽 양수, HALCON row 방향) | image row 부호 그대로 | |
| 절대값 (부호 무시) | 부호 표기 안 함 | |
| PPT 확인 후 결정 | researcher 위임 | |

**User's choice:** +Y (위쪽 양수, 공학 표준)

### Q1.3 — Fixture 범위

| Option | Description | Selected |
|--------|-------------|----------|
| Top Fixture #1 단독 (추천, ROADMAP 일치) | #2~ 는 SC#4 확장 대상 | ✓ |
| Top #1 + #2 동시 | 시퀀스 순회 포함 | |
| 전체 Top Fixture | 셋업만 (검증은 #1) | |

**User's choice:** Top Fixture #1 단독

### Q1.4 — TeachingImagePath 소비 (Phase 22 carry-over)

| Option | Description | Selected |
|--------|-------------|----------|
| Phase 23 범위 — 자동 로드 구현 (추천) | Datum 첫기 시 TeachingImagePath 우선, 비어있으면 InspectionImagePath 폴백 | ✓ |
| Phase 23 범위 외 | TeachingImagePath INI 보존만 (Phase 22 수준) | |
| Simul 모드 한정 동일 경로 전제 | 자동 로드는 Phase 25 로 이연 | |

**User's choice:** Phase 23 범위 — 자동 로드 구현
**Notes:** 사용자가 효성도 high 로 변경하면서 명시. Phase 22 SC#3 "두 경로 같은 파일 가능" 합의는 회귀 0 유지.

### Q1.5 — 좌표계 표기 정정 (사용자 요청)

| Topic | Description | Locked |
|-------|-------------|--------|
| ROADMAP 표현 | "Datum B → Y축 기준선 / Datum C → X축 기준선" | (그대로 보존, 의미는 "Y측정 기준선 / X측정 기준선") |
| 그림 기준 (사용자 해석) | Datum B = X축 horizontal, Datum C = Y축 vertical | ✓ (이후 모든 문서 통일) |

**User's clarification:** "Y축 기준선" 은 실제로 X축 수평선을 의미, "X축 기준선" 은 Y축 수직선을 의미 (그림 기준).

---

## Area 2 — A1~A5 측정 알고리즘 (ALG-01 잠금)

### Q2.1 — 알고리즘 구현 전략

| Option | Description | Selected |
|--------|-------------|----------|
| 신규 EdgeToLineDistance 클래스 추가 (추천) | MeasurementFactory 7번째. Point ROI 1개만 fit, Datum 변환 후 Y좌표 = 거리. Line ROI 불필요. | ✓ |
| 기존 PointToLineDistance 재사용 + 가상 Line ROI 규약 | INI 규약으로 Line_Phi=0 고정 | |
| PointToLineDistance 에 분기 추가 | UseDatumLineAsReference (bool) 옵션 | |
| 동일 이름으로 PointToLineDistance 리네이밍 | INI 호환성 우려 | |

**User's choice:** 신규 EdgeToLineDistance 클래스 추가

### Q2.2 — Edge 파라미터 노출 범위

| Option | Description | Selected |
|--------|-------------|----------|
| PointToLineDistance 와 동일 6종 (추천) | EdgeThreshold, Sigma, EdgeSampleCount, EdgeTrimCount, EdgePolarity, EdgeDirection | ✓ |
| 6종 + EdgeSelection (first/last) 명시 | HALCON 필수 (메모리) | |
| 최소 세트 (Threshold, Sigma, Polarity 만) | EdgeSampleCount/TrimCount/Direction 내부 고정 | |

**User's choice:** PointToLineDistance 와 동일 6종
**Notes:** Q2.4 에서 EdgeSelection 명시 매핑이 별도 잠겼으므로 실제 노출 = 7 파라미터 (6 + EdgeSelection).

### Q2.3 — Point ROI 형태

| Option | Description | Selected |
|--------|-------------|----------|
| Rectangle 만 (추천) | Y방향 거리는 수평 에지 1개로 충분 | ✓ |
| Rectangle + Polygon | Polygon ROI 지원 | |
| Rectangle + Polygon + Circle | Circle hole center 도 지원 | |

**User's choice:** Rectangle 만

### Q2.4 — 측정값 mm 정밀도

| Option | Description | Selected |
|--------|-------------|----------|
| 소수점 3자릿 (0.001mm, 추천) | 1μm 해상도. PPT 공차 표기 관례 | ✓ |
| 소수점 4자릿 (0.0001mm) | 0.1μm 해상도 | |
| 소수점 2자릿 (0.01mm) | 10μm 해상도 | |
| INI 상수 (DisplayDecimals) | 설정 가능 | |

**User's choice:** 소수점 3자릿 (0.001mm)

### Q2.5 — HALCON measure_pos 명시 매핑

| Option | Description | Selected |
|--------|-------------|----------|
| 새 클래스에도 measurePhi 명시 + EdgeSelection 명시 (필수) | 메모리 feedback_halcon_measurepos_must_haves 준수 | ✓ |
| VisionAlgorithmService 래퍼 재사용 (이미 준수) | 이중 구현 방지 | |

**User's choice:** 새 클래스에도 명시 매핑 (필수)
**Notes:** 구현 방식 = VisionAlgorithmService.TryFitLine 재사용으로 자동 준수.

### Q2.6 — Datum 첫기 실패 시 동작

| Option | Description | Selected |
|--------|-------------|----------|
| FAI 측정 fail 명시 (추천) | A1~A5 5개 모두 빨강 strip | ✓ |
| Identity transform 폴백 | raw image 좌표 사용 (디버그용) | |
| Skip + 경고만 | 시퀀스 계속 (결함 은폐 위험) | |

**User's choice:** FAI 측정 fail 명시

---

## Area 3 — 확장성 검증 (A6~A23)

### Q3.1 — SC#4 검증 방식

| Option | Description | Selected |
|--------|-------------|----------|
| 실제 A6 1개 추가 + Simul 동작 (추천) | INI 수동 편집 → 재시작 → 6개 UI 표시 확인 | ✓ |
| 실제 A6 추가 + 코드 패턴 정적 검증 | 이중 검증 | |
| A23 까지 일괄 등록 + Simul 동작 | 최대 부하 검증 | |
| 코드 패턴 정적 검증만 | 실제 실행 없음 | |

**User's choice:** 실제 A6 1개 추가 + Simul 동작

### Q3.2 — FAI 신규 추가 채널

| Option | Description | Selected |
|--------|-------------|----------|
| INI 직접 편집 + UI 추가 버튼 둘 다 (추천) | InspectionListView 'Add FAI' (이미 존재) + INI 양쪽 검증 | ✓ |
| INI 직접 편집만 | ROADMAP SC#4 문구 그대로 | |
| UI 추가 버튼만 | INI 직접 편집 무시 | |

**User's choice:** INI 직접 편집 + UI 추가 버튼 둘 다

### Q3.3 — 확장 한계 (메모리/성능)

| Option | Description | Selected |
|--------|-------------|----------|
| A23 까지 보장, 검증은 A6 1개 (추천) | Phase 5 100개+ 설계 재확인 | ✓ |
| A23 + Bottom Fixture A1~A23 동시 보장 | 구조적 확장성 보장 | |
| 한계 명시 없음 | Phase 5 결정 재확인만 | |

**User's choice:** A23 까지 보장, 검증은 A6 1개

### Q3.4 — INI 섹션/키 명명

| Option | Description | Selected |
|--------|-------------|----------|
| 기존 IsDynamicFAIMode + InspectionRecipeManager 패턴 그대로 (추천) | Phase 5 잠금. 신규 명명 규칙 없음 | ✓ |
| [FAI_Aseries_A1] 형식 슈프리픽스 추가 | A시리즈 명시 구분, 하위호환 늘 | |

**User's choice:** 기존 패턴 그대로

---

## Area 4 — Simul 이미지 + UI 결과 표시

### Q4.1 — Simul 이미지 파일 출처

| Option | Description | Selected |
|--------|-------------|----------|
| 사용자가 제공 (PPT 기반 실제 도면 이미지, 추천) | D:\TestImg\Datameasurement\ 하위 비치. Phase 23 시작 전 준비 | ✓ |
| 기존 D:\1.bmp 재사용 | SIMUL_MODE 기본 | |
| Phase 22 UAT 의 teaching_b.bmp 활용 | 검증된 경로 재사용 | |

**User's choice:** 사용자가 제공

### Q4.2 — A1~A5 결과 표시 UI

| Option | Description | Selected |
|--------|-------------|----------|
| InspectionListView TreeView 펼침 + strip 5개 동시 표시 (추천) | CO-05 녹/적. 신규 UI 없음 | ✓ |
| TreeView + 별도 dashboard 패널 추가 | 5개 이상 모니터링용 신규 UI | |
| TreeView 하나 + MeasuredValue 테이블 (이미 존재) | InspectionListView 값 테이블 그대로 | |

**User's choice:** TreeView 펼침 + strip 5개 동시 표시

### Q4.3 — 공차(Nominal/Tolerance) 입력 경로

| Option | Description | Selected |
|--------|-------------|----------|
| MeasurementBase PropertyGrid (기존, 추천) | Phase 6 NominalValue/UpperTolerance/LowerTolerance 자동 상속 | ✓ |
| INI 직접 편집 전용 | PropertyGrid 노출 안 함 | |
| 신규 공차 입력 패널 (UI) | 테이블형 신규 UI | |

**User's choice:** MeasurementBase PropertyGrid (기존)

### Q4.4 — msbuild 검증 기준 (SC#5)

| Option | Description | Selected |
|--------|-------------|----------|
| Debug/x64 PASS + Phase 21 baseline 동일 warning (추천) | 6 occurrences = MSB3884×2 + CS0162×2 + CS0219×2. 신규 0 | ✓ |
| Debug/x64 + Release/x64 둘 다 PASS | Release 까지 검증 | |
| Debug/x64 PASS 만 | 우선순위 조정 | |

**User's choice:** Debug/x64 PASS + Phase 21 baseline 동일

---

## Wrap-up

**Question:** 4개 영역의 결정을 잠그고 CONTEXT.md 를 작성할지, 더 논의할 회색 영역을 다룰지?

| Option | Description | Selected |
|--------|-------------|----------|
| CONTEXT.md 작성 진행 (추천) | 19 결정 lock-in + commit | ✓ |
| 추가 회색 영역 탐색 | 미발견 의사결정 포인트 추가 | |
| 이미 논의한 영역 재방문 | 잠긴 결정 재검토 | |

**User's choice:** CONTEXT.md 작성 진행

---

## Claude's Discretion (researcher/planner 위임)

- 신규 `EdgeToLineDistance.cs` 파일 위치 = `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistance.cs`
- TryExecute 내부 구조 (Point ROI fit → midpoint → datumTransform 적용 순서)
- TeachingImagePath 자동 로드 fallback 로그 메시지 형식
- A6 추가 UAT 시 INI 섹션 인덱스 (InspectionRecipeManager 기존 규칙)
- EdgeToLineDistance 의 ICustomTypeDescriptor hide 규칙 추가 여부 (현재 미적용)

## Deferred Ideas

- Top Fixture #2~ / Bottom Fixture 의 A시리즈 검증 (Phase 24 또는 별도 phase)
- Polygon/Circle ROI 형태 지원
- 50회 반복도 통계 (Phase 25 OUT-03)
- TCP 응답 분기 (Phase 24 WF-02)
- 결과 dashboard 패널 신규 (Phase 25)
- EdgeToLineDistance 의 X축 거리 측정 (별도 phase)
- 공차 입력 테이블형 UI 패널 (Phase 25 통합)
- ICustomTypeDescriptor hide 규칙 (필요 시 backlog)
