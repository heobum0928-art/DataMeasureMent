# Phase 58: Config & Camera (A) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-23
**Phase:** 58-config-camera-a-2026-06-23
**Areas discussed:** 카메라 클래스 구조, Config 저장 위치, 조율 구조, 연결 시점

---

## 진행 모드 결정 (선행)

| Option | Description | Selected |
|--------|-------------|----------|
| chain (검증 직전 정지) | discuss는 interactive(설계 동의) → plan→execute 자동 → UAT 직전 정지 | ✓ |
| 코드 직전 정지 | discuss→plan 자동 → execute 직전 정지 (동의 후 구현 엄수) | |
| 단계별 수동 | discuss/plan/execute 각각 직접 실행 | |

**User's choice:** chain — 58~62 매 phase 이 패턴 반복. 설계 동의는 discuss 단계에서 보존.
**Notes:** v1.3 "코드 작성 전 phase별 설계 동의" 규칙은 interactive discuss 가 충족.

---

## 카메라 클래스 구조

| Option | Description | Selected |
|--------|-------------|----------|
| 신규 래퍼 + HikCamera 내부 재사용 | EthernetAlignCamera 가 HikCamera 인스턴스 보유. 독립 클래스 요건 + MvCamCtrl 재사용(중복 0) + HikCamera 무수정 | ✓ |
| HikCamera 직접 사용 (래퍼 없음) | 핸들러/UI 가 HikCamera 직접 보유. 단순하나 '독립 클래스' 모호 + VirtualCamera 결합 노출 | |
| 완전 신규 클래스 (MvCamCtrl 직접) | HikCamera 미사용, MvCamCtrl.Net 직접 wrap. 최대 독립이나 연결/grab 코드 중복 | |

**User's choice:** 신규 래퍼 + HikCamera 내부 재사용 (D-01)
**Notes:** HikCamera 는 DeviceHandler 등록 없이 생성자+Open(IP) 독립 인스턴스화 가능(스카우팅 확인).

---

## Config 저장 위치

| Option | Description | Selected |
|--------|-------------|----------|
| 기존 Setting.ini 에 [ETHERNET_VISION] 섹션 추가 | Custom/SystemSetting.cs 프로퍼티 + AfterLoad() 기본값 복원(8.652), Phase 48 PcRole 패턴. 기존 인프라 재사용, '추가만' | ✓ |
| 별도 EthernetVisionConfig + 독립 INI | 자체 load/save 새 클래스 + 별도 .ini. 최대 독립이나 INI 인프라 재작성 + PropertyGrid 없음 | |

**User's choice:** 기존 Setting.ini 섹션 추가 (D-02)
**Notes:** 미존재 키 → 0 로드 방어는 Phase 48 RestorePcRoleDefault 패턴 그대로 적용.

---

## 조율 구조

| Option | Description | Selected |
|--------|-------------|----------|
| 전용 EthernetVisionHandler 도입 | config + 카메라(+이후 align/TCP) 소유 싱글턴, SystemHandler try-catch 실패-격리 init 한 줄. 59~62 골격 | ✓ |
| 최소 구성 (핸들러 없음) | config + 카메라 클래스만, 소유/조율은 61 UI 직접. 가벼우나 59~62 재정비 가능성 | |

**User's choice:** 전용 EthernetVisionHandler 도입 (D-03)
**Notes:** 기반 phase 에서 깔끔한 아키텍처 골격 확보.

---

## 연결 시점 / 생명주기

| Option | Description | Selected |
|--------|-------------|----------|
| 모드 게이트 + 지연 연결 | None=연결 안 함, Tray/Bottom 일 때만 연결. 실패 Grabber 무영향(try-catch), SIMUL 폴백 D:\align_test.bmp | ✓ |
| 앱 시작 시 연결 | Grabber init 과 함께 기동 시 연결. None 모드에서도 시도 → 불필요 | |

**User's choice:** 모드 게이트 + 지연 연결 (D-04)
**Notes:** None→기능 비활성 제약과 정합. 불필요 연결 시도 회피.

---

## Claude's Discretion

- EEthernetVisionMode enum 명/위치, [ETHERNET_VISION] INI 키 정확한 이름, 노출/IP 기본값, Live stream 세부, EthernetVisionHandler 파일 위치.

## Deferred Ideas

- Phase 59 Shape Matching / Phase 60 Calibration / Phase 61 TabControl UI / Phase 62 TCP — 전부 후속 phase.
