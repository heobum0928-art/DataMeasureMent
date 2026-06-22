# Requirements: DataMeasurement v1.2

**Defined:** 2026-05-29
**Milestone:** v1.2 POC Workflow + Output + Carry-over + Protocol v2.7
**POC 납기:** 2026-06-30
**Status:** Defining — Phase 39부터 continue numbering

> v1.0/v1.1 요구사항: [milestones/v1.0-REQUIREMENTS.md](milestones/v1.0-REQUIREMENTS.md), [milestones/v1.1-REQUIREMENTS.md](milestones/v1.1-REQUIREMENTS.md)

---

## 우선순위 구조 (POC 6월 말 기준)

| 순위 | 카테고리 | 시점 | REQ-ID |
|---|---|---|---|
| **1** | WF (검사 워크플로우 E2E) + OUT (결과 분석/Export) | POC 시연 필수 | WF-01/02, OUT-01~04 |
| **2** | CO (v1.1 carry-over 정리) | POC 전 정리 | CO-38-01~04, CO-23-01 |
| **3** | HW (CXP 그래버) | 장비 도착 시 합류 | HW-01/02 |
| **4** | QUAL (헝가리안 전체 리팩토링) | 시간 여유 시 | QUAL-01 |
| **5** | PROTO (제어 프로토콜 v2.7) | POC 시연 이후 | PROTO-01~06 |

**진행 정책:**
- 1~2순위 완료 전 5순위 착수 금지 (POC 시연 중 통신 사양 변경 부담 회피)
- HW 미도착 시 3순위는 Simul 검증으로 마감
- 4순위는 1~3순위 완료 후 잔여 시간으로 결정

---

## v1.2 요구사항

### 우선순위 1 — 검사 워크플로우 E2E (POC 필수)

- [x] **WF-01
**: Datum→FAI 측정→결과 처리 end-to-end (SIMUL+실카메라 모두)
  - Top/Side/Bottom 멀티샷 시퀀스 정상 진행
  - Datum 검출 실패 vs 측정 실패 분기 명확화
  - TCP 결과 응답 포맷 검증
- [ ] **WF-02**: OK/NG/검출 실패 결과별 후속 동작 분기
  - 검출 실패 시 후속 측정 skip 정책
  - 사이클 내 NG 누적 처리
  - 결과 코드 명세 (현행 프로토콜 v2.6 기준, v2.7 은 PROTO-01 에서 별도)

### 우선순위 1 — 결과 분석 & Export (POC 필수)

- [x] **OUT-01
**: 결과 이미지 리뷰어 — 날짜/원본 폴더 로드 → 결과 재현 (위치 TBD)
- [x] **OUT-02
**: 시퀀스 1회 검사 결과 → 엑셀 export
- [ ] **OUT-03**: 50회 반복 측정값 → 엑셀 export (반복도 단순 통계, mean/stddev/range/Cpk — 정식 Gage R&R ANOVA 아님)
- [ ] **OUT-04**: 검출 알고리즘별 통계 분석표 (TLI/CTH/VTH/Edge 6종 + 신규 알고리즘)

### 우선순위 2 — v1.1 Carry-over 정리

- [ ] **CO-38-01**: 픽셀분해능 런타임 단일소스 (현행 다중 경로 통합)
- [x] **CO-38-02
**: 시작지연 LoginManager 분리 (앱 기동 부담 완화)
- [x] **CO-38-03
**: 시작지연 SequenceHandler 분리 (Initialize 가속)
- [ ] **CO-38-04**: 실HW [STARTUP] 재측정 (HW 도착 후, 또는 Simul 베이스라인)
- [x] **CO-43-01
**: 기동 체감속도 — 18~20초 흰 화면 마스킹 + 콜드스타트 계측 (Phase 43.1 SIGNED_OFF — 스플래시 ✓ + (e)=21513ms 계측 ✓ + 레시피 로딩 14787ms 지배 구간 확인 → Phase 43.2에서 비동기화)
- [x] **CO-23-01**: A1~A5 측정값 UI 표시 (Phase 23 ALG-01 미완 잔여) — ✅ RESOLVED 2026-06-16 (Phase 40/40.1/40.2 측정값 표시 UI 구현으로 충족, 별도 Phase 45 불필요)

### 우선순위 3 — CXP 프레임 그래버 (장비 도착 시점)

- [x] **HW-01
**: RAP 4G 4C12 CXP SDK 확정/설치 (Euresys Coaxlink 또는 Matrox — 장비 도착 후 결정)
- [x] **HW-02
**: CXP 드라이버 통합 — VirtualCamera 인터페이스 호환, HIK 와 동일 GrabHalconImage 경로

> HW 미도착 시: 본 카테고리는 Simul 검증으로 마감하고 v1.3 으로 이연

### 우선순위 4 — 헝가리안 표기법 전체 리팩토링 (시간 여유 시)

- [ ] **QUAL-01**: 헝가리안 표기법 전체 리팩토링 (v1.1 deferred, 가독성 우선 — 사용자 명시 결정)

### 우선순위 5 — 제어 프로토콜 v2.7 (POC 시연 이후)

- [x] **PROTO-01
**: TEST 커맨드 z_index 파라미터 — `$TEST:site,null,z_index@` 파싱 + ResourceMap z_index↔Shot 매핑
- [ ] **PROTO-02**: RESULT 포맷 3단 구분자 — `$RESULT:site;P|F|B;count;id=val=OK,...@` (;/,/=) 직렬화/역직렬화
- [ ] **PROTO-03**: P/F/B 3-state 판정 엔진 — NG 발견 시 즉시 종료 X, 마지막 Index까지 진행 후 종합 (Pass/Fail/Bypass)
- [ ] **PROTO-04**: Datum 샷(z_index=0) 빈 응답 + Datum 실패 시 즉시 F
- [ ] **PROTO-05**: 멀티샷 사이클 state — `CycleState`, `ECycleResult` enum 신설 + InspectionSequence 사이클 단위 NG mark + 자동 리셋
- [ ] **PROTO-06**: 프로토콜 v2.7 통신 회귀 시험 (제어팀(김민우 선임) 동기화 후)

---

## Future Requirements (deferred)

- (없음 — v1.2 는 v1.1 carry-over 흡수 milestone)

---

## Out of Scope

- 3D/Laser 측정 — 2D 에지 측정만 사용
- Wafer 검사 시퀀스 — 원본(NewDDA)에만 해당
- OAuth/사용자 인증 고도화 — 기존 LoginManager 유지
- 정식 Gage R&R ANOVA (operator × part × trial) — OUT-03 은 단순 반복도 통계만
- 디스크 기반 이미지 캐시 — 메모리 상주만 (속도 우선)
- 제어 프로토콜 v2.7 POC 동시 진행 — 시연 중 통신 사양 변경 부담 회피 (PROTO-01~06 모두 시연 후로 lock)

---

## Traceability

| REQ-ID | Phase | Status |
|---|---|---|
| WF-01, WF-02 | Phase 39 | not started |
| OUT-01, OUT-02 | Phase 40 | not started |
| OUT-03, OUT-04 | Phase 41 | not started |
| CO-38-01 | Phase 42 | not started |
| CO-38-02, CO-38-03 | Phase 43 | signed_off (READY 55% 단축) |
| CO-43-01 | Phase 43.1 | signed_off (스플래시 마스킹 + 계측 완료) |
| CO-43-01 후속 | Phase 43.2 | not started (레시피 로딩 비동기화) |
| CO-38-04 | Phase 44 | not started (HW 도착 시) |
| CO-23-01 | Phase 45 | RESOLVED 2026-06-16 (Phase 40 시리즈로 충족) |
| HW-01, HW-02 | Phase 46 | not started (HW 미도착) |
| QUAL-01 | Phase 47 | not started (여유 시) |
| PROTO-01, PROTO-02 | Phase 48 | not started (POC 후) |
| PROTO-03, PROTO-04, PROTO-05 | Phase 49 | not started (POC 후) |
| PROTO-06 | Phase 50 | not started (POC 후) |
| BATCH-01 | Phase 51 | in progress (Wave 1 완료, Wave 2 UI 잔여) |
| LEVEL-01 | Phase 52 | partial (2026-06-17, 백엔드 완료·빌드 PASS·리뷰 클린 / UAT Test 2 FAIL — 활성화·기준지정 UI 부재 CO-52-01 → Phase 52.1) |

---

*Last updated: 2026-06-17 — LEVEL-01(Phase 52) partial 등재: 백엔드 완료, 레벨링 UI carry-over CO-52-01.*
