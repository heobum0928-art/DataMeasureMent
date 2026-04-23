# BUGS — DataMeasurement 결함 추적

> 운영/개발 중 발견된 결함 기록. WR (Warning from Review) 명명을 확장하여 사용:
> - `WR-XX-NN` : Phase XX 코드 리뷰에서 발견 (기존 패턴)
> - `WR-RT-NN` : 런타임/사용 중 발견 (Runtime, 신규)
>
> **운영 규칙**
> 1. 발견 즉시 Open 표에 한 줄 추가 (수정은 나중)
> 2. 처리 시작 시 "처리 계획" 컬럼 갱신
> 3. 완료 시 Fixed 표로 이동 + 커밋 해시 기록
> 4. Phase 종료 시 BUGS.md 한 번 훑어 우선순위 재검토

---

## Open

| ID | 발견일 | 출처 Phase | Severity | 증상 | 영향 | 처리 계획 |
|----|--------|-----------|----------|------|------|----------|
| WR-RT-01 | 2026-04-23 | Phase 1 | 🟡 Warning | Circle ROI가 존재하지 않음 — Rect/Polygon만 지원 | RC-03 (CircleDiameterMeasurement) 사용 불가 / Datum의 원 검출 (B1 등)도 영향 가능 | **Phase 11 묶음 예정** — WR-RT-03·04와 함께 "Datum 티칭 UI + ROI 보강" Phase 신설 (캔버스/ROI 코드 공통) |
| WR-RT-03 | 2026-04-23 | Phase 4 (+ Phase 6 Plan 04 미완) | 🔴 Blocker | **[범위 확대 2026-04-23]** 원 증상은 "티칭 화면에 이미지 로드 없음"이었으나 조사 결과 **Datum 티칭 UI 자체가 미구현**. `DatumFindingService.TryTeachDatum` (line 122)은 존재하나 UI 호출자 0건. Datum 노드 선택 시 `SetDatumOverlay`만 호출되고 이미지 로드는 없음 (`InspectionListView.xaml.cs:273-283`). `Action_FAIMeasurement.cs:220` 주석이 "ReuseFromShot 모드는 향후 Plan 04 UI 작업과 함께 구현"으로 명시 — Phase 6 Plan 04에서 빠진 작업. 현재는 사용자가 PropertyGrid에 Line1/Line2 좌표·RefOrigin·RefAngle을 수동 숫자 입력해야 함 (비현실적) | Datum 티칭 워크플로 전면 차단 / TCH-01 (ROI 시각화) 부분 미달 / Datum 기능 실사용 불가 | **Phase 11 묶음 예정** — "Datum 티칭 UI 완성": 이미지 로드 + TeachingWindow 연동(또는 전용 다이얼로그) + 2 Line ROI 드로잉 + `TryTeachDatum` 호출 + DatumConfig 저장. WR-RT-01·04와 캔버스/ROI 코드 공유 |
| WR-RT-04 | 2026-04-23 | Phase 1·2·3 통합 | 🟡 Warning | 프로그램 흐름 시퀀스가 의도대로 안내되지 않음: ① Datum 티칭→저장 ② ROI 그리기 ③ 알고리즘 선택+파라미터+테스트 ④ 저장 — UI가 이 순서를 강제/안내하는지 미확인 | 사용자가 임의 순서로 작업 시 혼란 / 미저장 상태로 진행 가능 | **Phase 11 묶음 예정** — Datum 티칭 UI 설계(WR-RT-03) discuss-phase에서 워크플로 가이드/단계 강제를 함께 결정 (독립 조사 대신 fold-in) |

---

## Fixed

| ID | 발견일 | 수정일 | 출처 Phase | 어떻게 해결 | 커밋 |
|----|--------|--------|-----------|-------------|------|
| WR-RT-02 | 2026-04-23 | 2026-04-23 | Phase 1 + Quick 260409-e3v | PropertyTools `[ItemsSourceProperty]` + 공용 `EdgeOptionLists` 도입으로 PropertyGrid를 자유 텍스트 → 드롭다운. `string` 타입 유지로 INI 하위호환. 8 파일 수정 | 5ff753a (Quick 260423-hzt) |

---

## 분류 가이드

### Severity

- 🔴 **Blocker** — 라인 멈춤 / 측정 불가 / 핵심 기능 차단
- 🟡 **Warning** — 동작은 하나 결과 의심 / 사용자 혼란
- 🟢 **Info** — 미관 / dead code / 추후 개선

### 처리 그릇

| 크기 | 그릇 | 기준 |
|------|------|------|
| 1~2시간 | Quick Task | 단일 파일 / 단순 수정 / 독립적 |
| 반나절~1일 | 다음 Phase에 끼워넣기 | 진행 중인 Phase 범위와 겹침 |
| 2일+ | 결함 모음 Phase 신설 | 3개 이상 묶기 가능 / 구조적 변경 |

### Phase 추정 방법

1. 파일 위치 → 출처 Phase 매핑 (`MainView.xaml.cs` = Phase 1·2 등)
2. REQUIREMENTS.md Traceability 표 → 영향받는 요구사항 → Phase
3. git blame → 코드 추가 시점 → Phase

---

## 통계

- Open: 3건 (Blocker 1, Warning 2)
- Fixed: 1건 (WR-RT-02)
- 출처 Phase: Phase 1 (1건), Phase 4 (1건, 범위 Phase 6 Plan 04까지 확대), 통합 (1건)

### 처리 로드맵 (2026-04-23 조사 기반)

1. ~~**Quick Task** — WR-RT-02 (ComboBox/TypeConverter, 1~2시간)~~ — Quick 260423-hzt로 처리 완료
2. **Phase 11 (신설 예정)** — "Datum 티칭 UI + ROI 보강":
   - WR-RT-03: Datum 티칭 UI 전체 구현 (현재 미구현 상태 확정)
   - WR-RT-01: Circle ROI 추가 (동일 캔버스·ROI 코드 공유)
   - WR-RT-04: 티칭 워크플로 순서 가이드 (discuss-phase에서 fold-in)

묶는 이유: 셋 다 캔버스 드로잉·`RoiDefinition` 확장·`IHalconTeachingProvider` 패턴·직렬화를 공유 → 분리 시 동일 파일 3회 회귀 위험.

---

*Created: 2026-04-23 — Initial population from Phase 9 후속 사용자 발견*
*Last updated: 2026-04-23 — WR-RT-02 Fixed (Quick 260423-hzt, ComboBox 처리 완료)*
