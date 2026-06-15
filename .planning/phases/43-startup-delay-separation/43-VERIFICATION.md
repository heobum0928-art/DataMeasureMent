---
phase: 43-startup-delay-separation
verified: 2026-06-15T00:00:00Z
status: passed
score: 5/5 must-haves verified
overrides_applied: 0
---

# Phase 43: 시작지연 분리 Verification Report

**Phase Goal:** 앱 기동 시 동기적으로 수행되는 무거운 초기화(LoginManager 계정 DB 로드)를 지연/분리하여 "측정 가능 시점(첫 $TEST 수용)"까지의 시간을 ≥30% 단축한다. Phase 38 [STARTUP] Stopwatch 계측을 기준선으로 입증.
**Verified:** 2026-06-15
**Status:** PASSED
**Re-verification:** No — 초기 검증

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | LoginManager.Load()가 SystemHandler.Initialize() 동기 임계 경로에서 실행되지 않는다 (백그라운드 thread) | ✓ VERIFIED | LoginManager.cs L101-109 생성자에서 Load() 직접 호출 없음. PreloadWorker에서만 Load() 호출(L179). SystemHandler.cs L152 `LoginManager.Handle.Preload()` 로 백그라운드 thread 기동. |
| 2 | [STARTUP] READY 마커가 Step 7 CollectRecipe 완료 직후 1회 출력되어 '첫 $TEST 수용 가능 시점'을 단일 기준 지표로 기록한다 | ✓ VERIFIED | SystemHandler.cs L171-173에 마커 존재. Step 7(L168 CollectRecipe) 직후, Step 8(L175 Localize) 직전 위치 확인. 로그 문자열 `[STARTUP] READY: {0} ms`. |
| 3 | 로그인 다이얼로그를 열면 AccountList가 항상 완전히 로드된 상태다 (half-loaded race 없음) | ✓ VERIFIED | LoginWindow.xaml.cs L21 `Login.EnsureLoaded()` 가 L24 `comboBox_id.ItemsSource = Login.GetIDList()` 보다 앞에 위치. EnsureLoaded 3-way 방어 경로: (a) _isPreloaded=true → 즉시반환 (b) IsAlive → Join (c) 미기동 → 동기 Load() 폴백. |
| 4 | 백그라운드 프리로드 미완 상태에서 첫 $TEST가 수신되어도 안전하게 처리된다 (Login.IsLogin 기본 false → AutoLogout 분기 미진입) | ✓ VERIFIED | LoginManager.cs L95 `public bool IsLogin { get; private set; }` 기본값 false 유지. Load()는 IsLogin을 변경하지 않음(L199-243 무수정). $TEST 처리는 IsLogin=false 안전 상태에서 수행됨. Runtime UAT (사용자 제공): 첫 $TEST 정상 처리, 명시 오류 없음. |
| 5 | 기존 LoginManager 인증 동작/계정 모델은 변경되지 않는다 (Load() 본문 무수정, IsLogin/LoginAccount/AccountList 의미 동일) | ✓ VERIFIED | LoginManager.cs L199-243 Load() 본문 무수정 — AES decrypt(RijndaelManaged L230), JsonConvert.DeserializeObject(L234), admin 기본 계정 추가 경로 모두 보존. Login()/LogOut()/Save() 등 인증 메서드 무수정. |

**Score:** 5/5 truths verified

---

## Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `WPF_Example/Login/LoginManager.cs` | 백그라운드 프리로드 인프라 (Preload/EnsureLoaded/PreloadWorker) + volatile _isPreloaded | ✓ VERIFIED | L88-89 필드 존재, L101-109 생성자 동기 Load() 제거됨, L171-197 신규 메서드 3종, L199-243 Load() 무수정 |
| `WPF_Example/SystemHandler.cs` | Step 5 백그라운드 기동 교체 + [STARTUP] READY 마커 | ✓ VERIFIED | L149-154 Step 5 교체 완료, L152 Preload() 호출, L171-173 READY 마커, L124 SequenceHandler 무수정, Phase 38 계측 9줄 보존 |
| `WPF_Example/UI/Login/LoginWindow.xaml.cs` | 로그인 진입 시 EnsureLoaded readiness wait | ✓ VERIFIED | L21 `Login.EnsureLoaded()` 존재, GetIDList(L24) 이전에 위치, //260615 hbk Phase 43 마커 존재 |

---

## Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| SystemHandler.cs Initialize() Step 5 | LoginManager.Preload() | 백그라운드 Thread 기동 | ✓ WIRED | L151 `Login = LoginManager.Handle;`, L152 `LoginManager.Handle.Preload();` — 두 줄 모두 확인됨 |
| LoginWindow.xaml.cs ctor | LoginManager.EnsureLoaded() | GetIDList() 호출 이전 동기 대기 | ✓ WIRED | L21 `Login.EnsureLoaded();` → L24 `comboBox_id.ItemsSource = Login.GetIDList();` 순서 확인됨 |
| LoginManager.PreloadWorker() | LoginManager.Load() | 백그라운드 thread에서 Load() 호출 | ✓ WIRED | L179 `if (!Load())` — 생성자 아닌 PreloadWorker 내부에서만 Load() 호출 |

---

## Data-Flow Trace (Level 4)

본 phase는 성능 리팩토링(타이밍 이동)이며 신규 동적 데이터 렌더링 컴포넌트를 추가하지 않음. LoginManager.AccountList는 Load()에서 채워지며 GetIDList()가 소비하는 구조는 변경 없음 — Level 4 trace 생략 적용.

---

## Behavioral Spot-Checks

런타임 UAT는 사용자 직접 수행 및 승인됨 (2026-06-15, `D:\Data\Trace\2026-06-15_Trace.log` 기준).

| Behavior | 측정값 | Status |
|----------|--------|--------|
| [STARTUP] READY Before (Step5 delta 804ms 포함, 1회) | ≈ 1285ms | 기준선 |
| [STARTUP] READY After — 1회차 | 567ms | ✓ PASS |
| [STARTUP] READY After — 2회차 | 540ms | ✓ PASS |
| [STARTUP] READY After — 3회차 | 628ms | ✓ PASS |
| [STARTUP] READY After 3회 평균 | 578ms | ✓ PASS |
| 단축률 (목표 ≥30%) | ≈ 55% | ✓ PASS |
| [LOGIN] Preload complete 위치 | READY 후 ≈ 1s (임계 경로 외부) | ✓ PASS |
| 회귀 (a) 첫 로그인 정상 | 계정 콤보박스 정상 채워짐, admin 로그인 성공 | ✓ PASS |
| 회귀 (b) 첫 $TEST 정상 | 검사 정상 처리, race/NRE 없음 | ✓ PASS |

---

## Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| CO-38-02 | 43-01-PLAN.md | 시작지연 LoginManager 분리 (앱 기동 부담 완화) | ✓ SATISFIED | LoginManager.Load() 백그라운드 thread 이동 완료. READY 평균 578ms, Before 대비 55% 단축. REQUIREMENTS.md 체크됨. |
| CO-38-03 | 43-01-PLAN.md | 시작지연 SequenceHandler 분리 (Initialize 가속) | ✓ SATISFIED | D-06/D-07 결정: SequenceHandler는 측정 임계 경로이므로 이동하지 않음. "시퀀스 준비에 불필요하게 끼어 있는 동기 의존성만 분리" 해석 = LoginManager 1건 분리로 CO-38-03 달성. REQUIREMENTS.md 체크됨. SequenceHandler L124 무수정 보존 확인. |

**REQUIREMENTS.md Traceability 확인:** Phase 43 행에 `CO-38-02, CO-38-03` 할당 — 플랜 frontmatter `requirements` 필드와 일치. 누락 ID 없음.

---

## Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| LoginManager.cs | L192-196 | `else if (!_isPreloaded)` 동기 폴백 경로 | ℹ️ Info | 의도된 방어 경로 — Preload()가 한 번도 기동되지 않은 비정상 경로에서 AccountList를 보장하기 위한 안전망. 정상 흐름에서는 실행되지 않음. |
| LoginWindow.xaml.cs | L90 | `comboBox_id.ItemsSource = Login.GetIDList()` (EnsureLoaded 없음) | ℹ️ Info | 의도적 배제. Btn_edit_Click은 ctor에서 Join이 완료된 이후에만 실행되므로 중복 호출 불필요. plan-checker WARNING c2c6713에 문서화 완료. |

스터브 패턴, TODO/FIXME, 빈 return 없음. 신규 라인 전체에 `//260615 hbk Phase 43` 마커 존재 확인.

---

## Human Verification Required

없음. 런타임 UAT는 사용자가 2026-06-15 직접 수행하여 승인함 (Task 4 approved). 이 섹션은 비어 있으며, status: passed 조건을 충족함.

---

## Gaps Summary

없음. 5/5 must-haves 모두 검증됨.

---

## Carry-over 기록 (Phase 목표와 무관, 정보성)

**CO-43-01** — 앱 기동 후 18~20s 흰 화면. 근본 원인: Initialize() 외부 (cold JIT + 네이티브 DLL 로딩 + MainWindow InitializeComponent XAML inflation). Phase 43 목표(≥30% READY 단축) 달성에 영향 없음. 별도 신규 phase 이연 (사용자 결정).

---

_Verified: 2026-06-15_
_Verifier: Claude (gsd-verifier)_
