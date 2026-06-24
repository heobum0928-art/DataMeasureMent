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
- [x] **PROTO-02
**: RESULT 포맷 3단 구분자 — `$RESULT:site;P|F|B;count;id=val=OK,...@` (;/,/=) 직렬화/역직렬화
- [x] **PROTO-03
**: P/F/B 3-state 판정 엔진 — NG 발견 시 즉시 종료 X, 마지막 Index까지 진행 후 종합 (Pass/Fail/Bypass)
- [x] **PROTO-04
**: Datum 샷(z_index=0) 빈 응답 + Datum 실패 시 즉시 F
- [x] **PROTO-05
**: 멀티샷 사이클 state — `CycleState`, `ECycleResult` enum 신설 + InspectionSequence 사이클 단위 NG mark + 자동 리셋
- [ ] **PROTO-06**: 프로토콜 v2.7 통신 회귀 시험 (제어팀(김민우 선임) 동기화 후)

---

## v1.3 요구사항 (Align 비전 — 이더넷 카메라)

> 신규 마일스톤 (started 2026-06-23). 기존 Grabber 검사(v1.0~v1.2)와 **완전 독립**으로 같은 DataMeasurement 실행파일에 공존. v1.2 는 열어둔 채 병행.

### Config & Camera (Phase 58 / A)
- [x] **AV-01
**: 사용자가 EthernetVisionMode(None/Tray/Bottom) + 카메라 IP/노출/픽셀분해능(8.652 μm/px)을 INI [ETHERNET_VISION] 로 설정·저장하고, 미존재 키는 기본값으로 보장받는다
- [x] **AV-02
**: 이더넷 카메라(Hikvision MV-CH250-90GM, MvCamCtrl.Net)를 독립 클래스로 연결/grab/live/stop 하며, 미연결(SIMUL)이면 D:\align_test.bmp 로드로 대체하고 실패해도 Grabber 검사는 정상 동작한다

### Vision Algorithm (Phase 59 / B)
- [x] **AV-03
**: 사용자가 ROI 를 지정해 Shape Model 을 티칭하고 .shm 로 저장/로드하며, find_shape_model 로 매칭 위치(Row/Col/Angle/Score)를 산출한다
- [x] **AV-04
**: Tray 모드는 X/Y Offset, Bottom 모드는 X/Y Offset + Theta 를 산출한다 (각 모드 별도 템플릿)

### Calibration — Bottom 전용 (Phase 60 / C)
- [x] **AV-05
**: 피커가 지그를 픽업한 상태로 10°씩 36스텝 회전한 자재 중심 궤적으로 피커 편심원 중심(피커 실제 센터)을 최소자승으로 계산한다
- [ ] **AV-06**: 비전이 읽는 각도와 피커 실제 회전각 간 선형 오프셋 보정계수를 산출·적용한다

### UI — TabControl (Phase 61 / D)
- [ ] **AV-07**: MainWindow 에 TabControl 을 추가해 [검사]/[Tray 비전]/[Bottom 비전] 탭으로 통합하고, EthernetVisionMode 에 따라 Tray/Bottom 탭 Visibility 를 제어한다 (기존 MainView 는 [검사] 탭으로 이동)
- [x] **AV-08
**: Tray/BottomVisionView 에 툴바(Grab/Live/Stop)+티칭 패널+검사 결과 패널(+Bottom 캘 패널)을 제공하고 HalconViewer 를 공용한다

### TCP (Phase 62 / E)
- [x] **AV-09
**: Align 결과를 기존 VisionServer/TcpServer 프레임워크로 전송한다 ($RESULT site=TRAY/BOTTOM, Tray=OffsetX/Y, Bottom=OffsetX/Y/Theta)

### 프로토콜 Type 필드 (Phase 63)
- [x] **PROTO-Type
**: 디팜스테크 Vision Protocol v3.0 Type 필드 — 수신 `$TEST:site,Type,자재번호,null,z_index@`(Type=[1], site=PC독립번호) 파싱 + 송신 `$RESULT:site;Type;P|F|B;...@`(Type echo) + `$ALIGN_TEST/$ALIGN_CALIB` 수신/`$ALIGN_RESULT/$ALIGN_CALIB` 응답 빌더 통합. V1(UseProtocolV1=true) 한정, v2.6 회귀 0, 검사 시퀀스/Action/SequenceBase 무변경.

**제약 (전 phase 공통):** 기존 Grabber 검사 코드(Sequence/Action/SystemHandler) 절대 수정 금지(추가만) · 이더넷 카메라 실패해도 Grabber 정상 · EthernetVisionMode=None 이면 탭 숨김+기능 비활성 · 헝가리언 표기법 · C# 7.2 · Halcon try-catch · 함수 30줄 이하 · 매직넘버 const · SIMUL_MODE=D:\align_test.bmp. 참조: D:\Backup\파이널비전\WPF_Example_260604 (TabControl 구조 — 신규 설계 말고 기존 탭 패턴 확장).

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
| CAL-01 | Phase 53 | completed 2026-06-23 (체커보드 픽셀 캘리브, 코드/빌드 충족, 육안 UAT pending) |
| AV-01, AV-02 | Phase 58 | not started (v1.3 Config & Camera) |
| AV-03, AV-04 | Phase 59 | not started (v1.3 Vision Algorithm) |
| AV-05, AV-06 | Phase 60 | not started (v1.3 Calibration Bottom) |
| AV-07, AV-08 | Phase 61 | not started (v1.3 UI TabControl) |
| AV-09 | Phase 62 | not started (v1.3 TCP) |
| PROTO-Type | Phase 63 | planned 2026-06-24 (5 plans, 3 waves — TCP Type 필드 + Align TCP 통합) |

---

*Last updated: 2026-06-17 — LEVEL-01(Phase 52) partial 등재: 백엔드 완료, 레벨링 UI carry-over CO-52-01.*
