# Requirements: DataMeasurement v1.1

**Defined:** 2026-05-04
**Milestone:** v1.1 Quality + Workflow + Infrastructure
**Core Value:** Shot-FAI 2계층 동적 구조로 100개+ 검사 항목을 유연하게 관리하고, Halcon 에지 측정으로 정밀한 거리 측정(mm) + 공차 판정 + Datum 자동 보정 수행

> v1.0 (Halcon Migration MVP) 요구사항은 [milestones/v1.0-REQUIREMENTS.md](milestones/v1.0-REQUIREMENTS.md) 참조.

---

## v1.1 Requirements

각 요구사항은 ROADMAP.md 의 phase 에 1:1 매핑된다.

### 코드 품질 (Cross-cutting)

- [ ] **QUAL-01**: 모든 식별자(필드/속성/지역변수/메서드 인자)에 헝가리안 표기법을 적용한다 (전체 리팩토링, 신규/기존 무관)
- [ ] **QUAL-02**: 삼항 연산자, null 병합(`??`), null 조건(`?.`) 을 명시적 if/else 로 변환한다
- [ ] **QUAL-03**: PropertyGrid 의 알고리즘별 동적 속성 노출 패턴(DatumConfig ICustomTypeDescriptor)을 다른 모델 클래스로 일반화한다
- [ ] **QUAL-04**: 코드 주석을 정리하여 "왜(why)" 만 남기고 "무엇(what)" 주석은 제거한다

### 검사 워크플로우 실측 (Workflow)

- [ ] **WF-01**: Datum 티칭 → FAI 측정 → 결과 처리 end-to-end 검증을 SIMUL_MODE 와 실 카메라 양쪽에서 수행한다
- [ ] **WF-02**: 검사 결과(OK / NG / 검출 실패) 별로 후속 동작(TCP 응답, 이미지 저장, UI 표시)을 분기한다

### 메모리 이미지 버퍼 (Buffer Infrastructure)

- [ ] **BUF-01**: 검사별 이미지를 메모리에 상주시켜 디스크 I/O 없이 재사용한다 (보관 정책 없음, 디스크 fallback 없음)
- [ ] **BUF-02**: 이미지 버퍼의 lifetime 을 명시적으로 관리한다 (sequence reset / recipe change 시점에 초기화)

### CXP 프레임 그래버 (Hardware)

- [ ] **HW-01**: RAP 4G 4C12 CXP 프레임 그래버 보드 SDK(Euresys Coaxlink 또는 Matrox)를 확정하고 설치한다
- [ ] **HW-02**: 기존 VirtualCamera 추상화를 유지하면서 CXP 카메라 드라이버를 추가한다 (Basler/HIK 와 동일 인터페이스)

### 결과 분석/Export (Output)

- [ ] **OUT-01**: 결과 이미지 리뷰어를 제공한다 — 날짜/원본 폴더 로드 → 결과 재현 (UI 위치는 phase 단계에서 결정)
- [ ] **OUT-02**: 시퀀스 1회 검사 결과를 엑셀 파일로 export 한다
- [ ] **OUT-03**: 50회 반복 측정값을 엑셀로 export 한다 (반복도 통계: mean/stddev/range/Cpk — 정식 Gage R&R ANOVA 아님)
- [ ] **OUT-04**: 알고리즘별(TLI/CTH/VTH/Edge 6종) 통계 분석표를 산출한다

### Phase 17 Carry-over (v1.0 partial sign-off 잔여)

- [x] **CO-01
**: DatumConfig.Circle_RadialDirection 의 PropertyGrid ItemsSource 를 Inward/Outward 두 값으로 제한한다 (Phase 17 Test 2)
- [ ] **CO-02**: DatumConfig PropertyGrid 동적 노출(QUAL-03)에 Phase 17 Test 8 잔여를 흡수한다
- [x] **CO-03**: btn_teachDatum 알고리즘 호환성 가드의 사양을 명문화하고 spec 으로 검증한다 (Phase 17 Test 10)
- [x] **CO-04
**: ROI Length=0 escape hatch 를 우클릭 메뉴로 노출한다
- [x] **CO-05**: 검출 strip 색상을 성공=녹색 / 실패=빨강으로 분기한다
- [x] **CO-06
**: FormatTeachError 메시지에서 ROI label 을 보존한다

---

## Future Requirements (deferred to v2.0+)

- 정식 Gage R&R ANOVA (operator × part × trial 분산 분해) — v1.1 은 단순 반복도 통계만
- 디스크 기반 이미지 캐시 — v1.1 은 메모리 상주만
- Side 카메라 검사 — v1.0/v1.1 모두 Top/Bottom 만
- 검사 결과 히스토리/트렌드 차트 (UI-07)
- Shot/FAI 트리 드래그 앤 드롭 (UI-06)

---

## Out of Scope

| Feature | Reason |
|---------|--------|
| 3D/Laser 측정 | 2D 에지 측정만 사용 |
| Wafer 검사 시퀀스 | 원본(NewDDA)에만 해당 |
| OAuth/사용자 인증 고도화 | 기존 LoginManager 유지 |
| 정식 Gage R&R ANOVA (분산 분해) | v1.1 은 단순 반복도 통계 (mean/stddev/range/Cpk) |
| 디스크 기반 이미지 캐시 | 검사 속도 우선 — 메모리 상주만 |
| C# 8+ 기능 (nullable refs, switch expressions, records) | C# 7.2 csproj 강제 |
| 자동 헝가리안 변환 도구 | 수동 리팩토링 (가독성 검토 필요) |

---

## Traceability

| Phase | Requirements | Status |
|-------|-------------|--------|
| Phase 18 — Carry-over 정리 | CO-01, CO-03, CO-04, CO-05, CO-06 | Pending |
| Phase 19 — PropertyGrid 동적 노출 일반화 | QUAL-03, CO-02 | Pending |
| Phase 20 — 코드 스타일 정리 | QUAL-02, QUAL-04 | Pending |
| Phase 21 — 메모리 이미지 버퍼 | BUF-01, BUF-02 | Pending |
| Phase 22 — CXP SDK 확정 | HW-01 | Pending |
| Phase 23 — CXP 드라이버 통합 | HW-02 | Pending |
| Phase 24 — 검사 워크플로우 end-to-end | WF-01, WF-02 | Pending |
| Phase 25 — 결과 분석 & Export | OUT-01, OUT-02, OUT-03, OUT-04 | Pending |
| Phase 26 — 헝가리안 전체 리팩토링 | QUAL-01 | Pending |

**Coverage: 18/18 requirements mapped (100%)**

---

*Defined: 2026-05-04 — Traceability filled by roadmapper 2026-05-04*
