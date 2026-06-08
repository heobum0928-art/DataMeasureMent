---
phase: 38-v1-1-carryover-cleanup-2026-05-28
type: analysis
topic: 프로그램 시작 지연 분석 (#11, D-14)
measured: 2026-05-28 (SIMUL_MODE Debug/x64, 사용자 실측 1회)
source: SystemHandler.Initialize() [STARTUP] Stopwatch 계측 (commit 3c99f66)
---

# Phase 38 — 프로그램 시작 지연 분석 (#11)

> SystemHandler.Initialize() 8단계에 Stopwatch 누적+delta 계측을 부착(commit 3c99f66)하고,
> SIMUL_MODE(Debug/x64)로 1회 실행해 단계별 경과시간을 실측했다.
> **결론: 시작 지연 주범 2개 식별(LoginManager 808ms, SequenceHandler 550ms = 전체의 약 90%).
> 둘 다 구조적/고위험 영역이라 이번 phase 에서는 저위험 개선 미적용 — 전부 carry-over (D-14 회귀 위험 0 우선).**

## 실측 결과 (2026-05-28, SIMUL_MODE 1회)

| 단계 | 누적(ms) | delta(ms) | 비고 |
|---|---|---|---|
| 1) Lights.Initialize | 57 | 57 | 조명 컨트롤러 open (SIMUL — Light Error 후 진행) |
| 2) SequenceHandler | 622 | **550** | ⬅ 지연 주범 #2 — 레시피 로딩 소유 |
| 3) VisionServer+RawImageSaver | 638 | 16 | TCP 서버 + 저장 워커 |
| 4) SystemThread.Start | 663 | 25 | 시스템 루프 스레드 |
| 5) LoginManager | 1471 | **808** | ⬅ 지연 주범 #1 — 계정 DB 로드 추정 |
| 6) ExecOnCreate+WireBuffer | 1472 | 1 | 시퀀스 생성 콜백 |
| 7) CollectRecipe | 1484 | 7 | 레시피 디렉터리 스캔 |
| 8) Localize | 1493 | 0 | 다국어 초기화 |
| **Total Initialize** | **1509** | — | |

- Step 5 LoginManager (808ms, ~54%) + Step 2 SequenceHandler (550ms, ~36%) = 1358ms / 1509ms ≈ **90%**.
- 나머지 6개 단계 합계는 약 151ms 로 미미.
- 사전에 지연 후보로 지목됐던 Step 6 ExecOnCreate(카메라/리소스 init)는 SIMUL_MODE 에서 delta 1ms 로 사실상 무시 가능 — 실 하드웨어에서는 재측정 필요.

## 지연 원인 식별 (≥1개 — 성공기준 #4)

### 원인 #1 — LoginManager.Handle 초기화 (delta 808ms)
- `SystemHandler.Initialize()` Step 5 (`Login = LoginManager.Handle`).
- 싱글턴 첫 접근 시 계정 DB(JSON, Newtonsoft) 로드/역직렬화가 수행되는 것으로 추정.
- 로그인은 사용자가 실제 로그인하는 시점에야 필요 → **첫 사용 시점 lazy-load 후보**.

### 원인 #2 — SequenceHandler.Handle 초기화 (delta 550ms)
- Step 2 (`Sequences = SequenceHandler.Handle`). "레시피 로딩을 소유"하는 핸들러.
- 싱글턴 첫 접근 시 레시피 파싱/시퀀스 구성이 동기 수행되는 것으로 추정.

## 적용한 저위험 개선 (D-14)

**없음 — 이번 phase 에서는 개선을 적용하지 않았다.**

근거(D-14): 두 주범 모두 (a) lazy-load/비동기화 또는 (b) 초기화 순서 재배치가 필요한 **구조적·고위험** 변경이다.
v1.1 종결 직전 시점에 시작 경로(인증/레시피 로딩 동기 의존성)를 건드리면 회귀 위험이 크고,
실측이 SIMUL 1회뿐이라 충분한 근거가 아직 없다. D-14 "확신이 없으면 개선 미적용 → carry-over" 원칙에 따라 전부 이관한다.

## Carry-over (구조적/고위험 — 신규 phase 후보)

| ID | 내용 | 위험/사유 |
|---|---|---|
| CO-38-02 | LoginManager 초기화 808ms — 계정 DB 로드를 첫 로그인 시점으로 lazy-load 검토 | 인증 흐름/MainRun 의존성 확인 필요. 측정 전 lazy 트리거 보장 필요 |
| CO-38-03 | SequenceHandler 초기화 550ms — 레시피 로딩 동기 의존성 분석 후 지연/비동기화 검토 | 시퀀스 구성과 UI/검사 시작 타이밍 의존성. 회귀 위험 |
| CO-38-04 | 실 하드웨어 환경에서 [STARTUP] 재측정 — SIMUL 에서 무시된 Step 6(카메라 init) 등 재평가 | SIMUL 1회 측정의 대표성 한계 |

> 계측 코드([STARTUP] 로그) 자체는 영구 유지 — 이후 개선의 정량 근거 및 회귀 감시용.
