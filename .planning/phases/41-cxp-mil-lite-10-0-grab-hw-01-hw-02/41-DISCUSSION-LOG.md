# Phase 41: CXP 카메라 MIL Lite 10.0 grab 드라이버 통합 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-02
**Phase:** 41-cxp-mil-lite-10-0-grab-hw-01-hw-02
**Areas discussed:** 카메라 매핑/대수, 그래버·카메라 사양 + DCF, 트리거 & grab 모드, MIL→HImage 변환 + 리소스 수명

---

## 카메라 매핑 / 대수

**Q:** CXP 카메라는 Top/Side/Bottom 중 무엇·몇 대? 나머지는 HIK 유지?
**User's choice:** 전부 CXP. 물리 2대 (① top/bottom 겸용, ② side). PC 2대, 각 PC 별도 연결 → 소프트웨어 인스턴스당 CXP 1대만 활성. PC1=Top+Bottom 시퀀스, PC2=Side 시퀀스 (사용자 확인 "맞아").
**Notes:** RegisterRequiredDevices를 HIK 3대 고정 → PC별 CXP 1대 + 역할 설정으로 재구성.

## 그래버 · 카메라 사양 + DCF

**Q:** 그래버 모델 / CXP 버전·레인 / 카메라 모델·해상도·픽셀포맷 / DCF?
**User's choice:**
- 그래버 = RAP4G4C12, PCIe x8, 4채널
- CXP 버전 = 모름 (plan 시 확인)
- 카메라 = ViewWorks 128MP Mono
- DCF = 확인 필요, 코드 설정을 default로
**Notes:** 128MP Mono = 대용량 프레임 → 메모리/성능 plan 고려.

## 트리거 & grab 모드

**Q:** 소프트웨어 단발 / 연속 스트리밍 / 하드웨어 트리거?
**User's choice:** 우선 소프트웨어 트리거 (단발 grab).
**Notes:** 하드웨어 트리거/스트리밍은 deferred.

## MIL → HImage 변환 + 리소스 수명 + SIMUL

**Q9 변환:** zero-copy vs 복사?
**User's choice:** MIL 버퍼에서 GetImagePointer로 HObject로 버퍼 복사.
**Q10 리소스 수명:** 앱 시작 1회 할당/종료 해제?
**User's choice:** OK.
**Q11 SIMUL 폴백:** 기존 파일 grab 유지?
**User's choice:** OK.

## Claude's Discretion

- MIL→HImage 정확한 API 시퀀스, zero-copy 최종 선택(128MP 성능 기반)
- HIK 코드 잔존/제거 범위
- PC별 역할 설정 메커니즘 구체

## Deferred Ideas

- 하드웨어 트리거 / 연속 스트리밍
- DCF 기반 digitizer 설정
- zero-copy 무복사 grab 최적화
- HIK 드라이버 완전 제거
