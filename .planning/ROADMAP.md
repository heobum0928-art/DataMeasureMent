# Roadmap: DataMeasurement

## Milestones

- **v1.0 Halcon Migration MVP** — Phases 1-17 (shipped 2026-05-04, 22 deferred items)
- **v1.1 Quality + Workflow + Infrastructure** — Phases 18-26 (started 2026-05-04)

## Phases

<details>
<summary>v1.0 Halcon Migration MVP (Phases 1-17) — SHIPPED 2026-05-04</summary>

Full archive: [milestones/v1.0-ROADMAP.md](milestones/v1.0-ROADMAP.md)
Requirements: [milestones/v1.0-REQUIREMENTS.md](milestones/v1.0-REQUIREMENTS.md)
Audit: [milestones/v1.0-MILESTONE-AUDIT.md](milestones/v1.0-MILESTONE-AUDIT.md)
Phase artifacts: [milestones/v1.0-phases/](milestones/v1.0-phases/)

- [x] Phase 1: UI 재설계 (FAI-centric TreeView + 단일 캔버스) — 2/2 — 2026-04-07
- [x] Phase 2: 티칭 & 캘리브레이션 (ROI 시각화 + 픽셀-mm) — 2/2 — 2026-04-08
- [x] Phase 3: 에지 측정 알고리즘 (Halcon MeasurePos) — 2/2 — 2026-04-09
- [x] Phase 4: Datum 기준좌표계 (TwoLineIntersect + hom_mat2d) — 3/3 — 2026-04-10
- [x] Phase 5: 검사 시퀀스 & TCP — 2/2 — 2026-04-09
- [x] Phase 6: Rapid City 확장 (Fixture + 6 Measurement + 조명) — 4/4 — 2026-04-22
- [x] Phase 7: Measurement 오버레이 회귀 수정 — 2/2 — 2026-04-23
- [x] Phase 8: 요구사항 & 트레이서빌리티 동기화 — 1/1 — 2026-04-23
- [x] Phase 9: VERIFICATION 문서 보강 (G3/G4/G5/G7) — 5/5 — 2026-04-23
- [x] Phase 10: Datum 정확성 결함 수정 (WR-01/03/05) — 2/2 — 2026-04-23
- [x] Phase 11: Datum 티칭 UI + ROI 보강 (Circle 지원) — 4/4 — 2026-04-25
- [x] Phase 12: Datum 신규 알고리즘 2종 (CircleTwoHorizontal + VerticalTwoHorizontal) — 3/3 — 2026-04-24
- [x] Phase 13: Datum 알고리즘 확장성 (per-ROI 파라미터 + 시각화) — 5/5 — 2026-04-26
- [x] Phase 14: Datum carry-over (Circle polar sampling + Vertical 그룹) — 5/5 — 2026-04-28
- [x] Phase 15: HALCON MeasurePos 정합성 (measurePhi + EdgeSelection) — 4/4 — 2026-04-29 (partial)
- [x] Phase 16: Circle strip 재설계 + AlgorithmType binding fix — 3/3 — 2026-04-30 (partial)
- [x] Phase 17: Datum 티칭/검증 UX 재설계 + DetectedOrigin + hover — 4/4 — 2026-05-04 (partial 12 PASS / 2 FAIL / 1 SKIP / 1 INVALID)

</details>

### v1.1 Quality + Workflow + Infrastructure

- [ ] **Phase 18: Carry-over 정리** - Phase 17 partial sign-off 잔여 5건 (CO-01/03/04/05/06) 흡수
- [ ] **Phase 19: PropertyGrid 동적 노출 일반화** - DatumConfig ICustomTypeDescriptor 패턴을 다른 모델로 확장 (QUAL-03, CO-02)
- [ ] **Phase 20: 코드 스타일 정리** - 삼항/null 연산자 → 명시적 if/else + "why" 주석만 보존 (QUAL-02, QUAL-04)
- [ ] **Phase 21: 메모리 이미지 버퍼** - 검사별 이미지 메모리 상주 + lifetime 명시 관리 (BUF-01, BUF-02)
- [ ] **Phase 22: CXP SDK 확정** - RAP 4G 4C12 SDK 후보 평가 + 설치 + 드라이버 설계 spec (HW-01)
- [ ] **Phase 23: CXP 드라이버 통합** - VirtualCamera 추상화 유지하며 CXP 드라이버 클래스 추가 (HW-02)
- [ ] **Phase 24: 검사 워크플로우 end-to-end** - Datum→FAI→결과 처리 실측 + OK/NG/실패 분기 (WF-01, WF-02)
- [ ] **Phase 25: 결과 분석 & Export** - 이미지 리뷰어 + 1회 검사 엑셀 + 반복도 엑셀 + 알고리즘별 통계 (OUT-01..04)
- [ ] **Phase 26: 헝가리안 전체 리팩토링** - 전체 식별자 헝가리안 표기법 전면 적용 (QUAL-01)

## Phase Details

### Phase 18: Carry-over 정리
**Goal**: Phase 17 partial sign-off 에서 이월된 5건의 소형 결함을 완전히 해소하여 v1.1 개발 기반을 깨끗하게 만든다
**Depends on**: Nothing (v1.1 first phase)
**Requirements**: CO-01, CO-03, CO-04, CO-05, CO-06
**Success Criteria** (what must be TRUE):
  1. DatumConfig.Circle_RadialDirection PropertyGrid 콤보박스에 Inward/Outward 두 항목만 표시된다 (CO-01)
  2. btn_teachDatum 클릭 시 AlgorithmType 과 호환되지 않는 ROI 조합에 대한 경고 모달 사양이 spec 문서로 명문화되고 자동 검증된다 (CO-03)
  3. ROI 우클릭 메뉴에 Length=0 escape hatch 항목이 나타나고 실행된다 (CO-04)
  4. Datum/FAI 검출 결과 strip 이 성공=녹색, 실패=빨강으로 구분 표시된다 (CO-05)
  5. FormatTeachError 오류 메시지에 문제 ROI 의 label 이름이 포함된다 (CO-06)
**Plans**: TBD

### Phase 19: PropertyGrid 동적 노출 일반화
**Goal**: DatumConfig 에만 적용된 ICustomTypeDescriptor 기반 동적 PropertyGrid 패턴을 FAIConfig 등 다른 모델 클래스로 확장하여, 현재 설정 종류에 무관한 속성이 PropertyGrid 에서 자동으로 숨겨진다
**Depends on**: Phase 18
**Requirements**: QUAL-03, CO-02
**Success Criteria** (what must be TRUE):
  1. DatumConfig 동적 노출이 Phase 17-02 동작 그대로 유지된다 (회귀 0) — CO-02 흡수
  2. FAIConfig 를 PropertyGrid 에 표시할 때 현재 EdgeMeasureType 에 무관한 속성이 숨겨진다
  3. 동적 노출 패턴을 적용하기 위한 공통 추상 베이스 또는 헬퍼가 구현되어 새 모델 등록이 일관된 절차로 가능하다
  4. msbuild Debug/x64 PASS, 신규 warning 0
**Plans**: TBD
**UI hint**: yes

### Phase 20: 코드 스타일 정리
**Goal**: 코드베이스 전반의 삼항/null 연산자를 명시적 if/else 로 전환하고, "what" 주석을 제거하여 "why" 주석만 남김으로써 가독성을 높인다
**Depends on**: Phase 18
**Requirements**: QUAL-02, QUAL-04
**Success Criteria** (what must be TRUE):
  1. 변환 대상 파일에서 `?:` 삼항, `??` null 병합, `?.` null 조건 연산자가 명시적 if/else 블록으로 교체된다
  2. 코드를 그대로 서술하는 "what" 주석이 제거되고 설계 의도·비즈니스 규칙을 담은 "why" 주석이 보존된다
  3. SIMUL_MODE 검사 시나리오가 변환 전/후 동일하게 동작한다 (로직 회귀 0)
  4. msbuild Debug/x64 PASS, 신규 warning 0
**Plans**: TBD

### Phase 21: 메모리 이미지 버퍼
**Goal**: 각 Shot 검사 시 캡처된 HImage 를 메모리에 보관하여 디스크 I/O 없이 재조회할 수 있고, 시퀀스 리셋 또는 레시피 변경 시 버퍼가 명시적으로 해제된다
**Depends on**: Phase 20
**Requirements**: BUF-01, BUF-02
**Success Criteria** (what must be TRUE):
  1. 검사 완료 후 결과 이미지 리뷰어(또는 디버그 뷰)에서 마지막 Shot 이미지를 디스크 접근 없이 표시할 수 있다
  2. 레시피 변경 또는 시퀀스 Reset 이벤트 발생 시 버퍼 내 모든 HImage 가 Dispose 된다 (메모리 누수 없음)
  3. 버퍼에 보관되는 HImage 수와 보관 시점이 코드 주석 또는 명시적 상수로 문서화되어 있다
  4. msbuild Debug/x64 PASS, 신규 warning 0; SIMUL_MODE 정상 동작
**Plans**: TBD

### Phase 22: CXP SDK 확정
**Goal**: RAP 4G 4C12 CXP 프레임 그래버 보드에 대해 Euresys Coaxlink 또는 Matrox 중 SDK 를 확정하고, 개발 PC 에 설치 검증 및 드라이버 통합 설계 spec 을 산출한다
**Depends on**: Phase 21
**Requirements**: HW-01
**Success Criteria** (what must be TRUE):
  1. SDK 선택 근거(라이선스, HALCON 연동 방식, API 호환성)가 결정 문서로 기록된다
  2. 선택한 SDK 가 개발 PC 에 설치되고 sample 애플리케이션이 빌드/실행된다
  3. VirtualCamera 추상화 아래에 CXP 드라이버를 삽입하기 위한 인터페이스 설계 초안(클래스 구조 + 메서드 서명)이 phase spec 에 포함된다
  4. SDK 설치 후에도 SIMUL_MODE 경로(D:\1.bmp)가 영향받지 않는다
**Plans**: TBD

### Phase 23: CXP 드라이버 통합
**Goal**: 확정된 CXP SDK 를 기반으로 VirtualCamera 추상화를 유지하면서 CXP 카메라 드라이버 클래스를 추가하고, Basler/HIK 와 동일한 인터페이스로 DeviceHandler 에 등록된다
**Depends on**: Phase 22
**Requirements**: HW-02
**Success Criteria** (what must be TRUE):
  1. `CxpCamera` (또는 동등 명칭) 클래스가 `VirtualCamera` 의 `GrabHalconImage()` 인터페이스를 구현한다
  2. DeviceHandler 에 CXP 카메라 등록 경로가 추가된다
  3. CXP 하드웨어 미연결 상태에서 SIMUL_MODE 로 빌드/실행 시 예외 없이 정상 초기화된다
  4. msbuild Debug/x64 PASS, 신규 warning 0
**Plans**: TBD

### Phase 24: 검사 워크플로우 end-to-end
**Goal**: Datum 티칭 → FAI 측정 → 결과 처리 전 과정이 SIMUL_MODE 와 실 카메라 양쪽에서 오류 없이 완주되고, OK/NG/검출 실패 각 결과에 따라 TCP 응답 + 이미지 저장 + UI 표시가 올바르게 분기된다
**Depends on**: Phase 21, Phase 23
**Requirements**: WF-01, WF-02
**Success Criteria** (what must be TRUE):
  1. SIMUL_MODE 에서 시퀀스 1회 실행 시 Datum 보정 → Shot N개 Grab → FAI M개 측정 → 종합 판정이 오류 없이 완주된다
  2. OK 판정 시 TCP OK 응답 전송이 확인된다
  3. NG 판정 시 TCP NG 응답 + 실패 이미지 저장 + UI 결과 테이블 NG 행 표시가 확인된다
  4. 검출 실패(에지 미검출) 시 TCP Error 응답 + 오류 이미지 저장 + UI 오류 표시가 확인된다
  5. INI 하위호환 (IsDynamicFAIMode + EnsurePerRoiDefaults) 이 end-to-end 실행 후 깨지지 않는다
**Plans**: TBD

### Phase 25: 결과 분석 & Export
**Goal**: 검사 결과 이미지를 날짜/폴더 기준으로 불러와 재현할 수 있고, 1회 검사 결과와 50회 반복 측정값을 엑셀로 export 하며, 알고리즘별 통계 분석표를 조회할 수 있다
**Depends on**: Phase 24
**Requirements**: OUT-01, OUT-02, OUT-03, OUT-04
**Success Criteria** (what must be TRUE):
  1. 결과 이미지 리뷰어에서 날짜/폴더를 선택하면 저장된 결과 이미지와 측정값이 재현된다 (OUT-01)
  2. "엑셀 Export (1회)" 실행 시 마지막 검사의 Shot × FAI 측정값과 OK/NG 판정이 .xlsx 파일로 저장된다 (OUT-02)
  3. "반복도 Export (50회)" 실행 시 각 FAI 의 mean/stddev/range/Cpk 가 .xlsx 파일로 산출된다 (OUT-03)
  4. 알고리즘별 통계 분석표에서 TLI/CTH/VTH/Edge 6종 각각의 측정 분포 요약을 테이블로 확인할 수 있다 (OUT-04)
**Plans**: TBD
**UI hint**: yes

### Phase 26: 헝가리안 전체 리팩토링
**Goal**: 코드베이스 전체(신규/기존 무관)의 모든 식별자(필드/속성/지역변수/메서드 인자)에 헝가리안 표기법을 일관되게 적용하여 가독성을 높인다
**Depends on**: Phase 25
**Requirements**: QUAL-01
**Success Criteria** (what must be TRUE):
  1. 리팩토링 대상 파일 전체에서 헝가리안 접두사 미적용 필드/속성이 grep 기준 정의 문서에 따라 검출되지 않는다
  2. msbuild Debug/x64 PASS, 신규 warning 0
  3. SIMUL_MODE 검사 시나리오가 리팩토링 전/후 동일하게 통과된다 (동작 회귀 0)
  4. INI 하위호환 (IsDynamicFAIMode + EnsurePerRoiDefaults) 이 리팩토링 후에도 유지된다
  5. 모든 변경 라인 위에 `//YYMMDD hbk` 주석이 존재한다 (grep count 검증 가능)
**Plans**: TBD

---

## Progress Table

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 18. Carry-over 정리 | 0/TBD | Not started | - |
| 19. PropertyGrid 동적 노출 일반화 | 0/TBD | Not started | - |
| 20. 코드 스타일 정리 | 0/TBD | Not started | - |
| 21. 메모리 이미지 버퍼 | 0/TBD | Not started | - |
| 22. CXP SDK 확정 | 0/TBD | Not started | - |
| 23. CXP 드라이버 통합 | 0/TBD | Not started | - |
| 24. 검사 워크플로우 end-to-end | 0/TBD | Not started | - |
| 25. 결과 분석 & Export | 0/TBD | Not started | - |
| 26. 헝가리안 전체 리팩토링 | 0/TBD | Not started | - |

---

## v1.0 Progress (archived)

| Milestone | Phases | Plans Complete | Status | Shipped |
|-----------|--------|----------------|--------|---------|
| v1.0 Halcon Migration MVP | 17 | 55/55 | Complete (22 deferred) | 2026-05-04 |
| v1.1 Quality + Workflow + Infrastructure | 9 | 0/TBD | In progress | — |

---

*v1.1 roadmap created: 2026-05-04 — Phase 18-26 (continues from v1.0 Phase 17)*
