# Roadmap: DataMeasurement

## Milestones

- **v1.0 Halcon Migration MVP** — ✅ Shipped 2026-05-04. Archive: [milestones/v1.0-ROADMAP.md](milestones/v1.0-ROADMAP.md) (17 phases / 55 plans)
- **v1.1 Quality + Workflow + Algorithm** — ✅ Shipped 2026-05-28. Archive: [milestones/v1.1-ROADMAP.md](milestones/v1.1-ROADMAP.md) · [REQUIREMENTS](milestones/v1.1-REQUIREMENTS.md) · [AUDIT](v1.1-MILESTONE-AUDIT.md)
  - 17 phases (18~38, inserts 23.1/34.1). Quality(QUAL-02/03/04)+Buffer+Image-dual+Algorithm/Datum 대거 확장 완료.
  - v1.2 이연: WF-01/02, OUT-01~04, HW-01/02, QUAL-01. 부분: ALG-01(CO-23-01).
- **v1.2 POC Workflow + Output + Carry-over + Protocol v2.7** — ◷ Active (started 2026-05-29, POC 납기 2026-06-30)
  - 11 phases (39~49), 5순위 우선순위 구조. continue numbering 모드.

---

## v1.2 Phases (Phase 39부터)

> POC 6월 말 기준 5단계 우선순위. 1~2순위 완료 전 5순위 착수 금지.

### 우선순위 1 — POC 시연 필수

- [ ] **Phase 39: 검사 워크플로우 E2E** — Datum→FAI→결과 OK/NG/검출 실패 분기 (WF-01, WF-02)
  - Success: SIMUL+실카메라 모두 Datum 검출→FAI 측정→TCP 결과 응답 끊김 없음 / OK·NG·검출실패 3분기 후속 동작 명세 적용 / 사이클 내 NG 누적 처리 안정
- [ ] **Phase 40: 결과 분석 & Export I — 리뷰어 + 1회 검사 엑셀** (OUT-01, OUT-02)
  - Success: 날짜/원본 폴더 로드 시 결과 이미지 재현 / 1회 검사 결과 xlsx 생성 (메타+측정값+판정+이미지 링크)
- [ ] **Phase 41: 결과 분석 & Export II — 50회 반복도 + 알고리즘 통계** (OUT-03, OUT-04)
  - Success: 50회 반복 시퀀스 자동 실행 + mean/stddev/range/Cpk xlsx / 알고리즘별(TLI/CTH/VTH/Edge 6종+) 통계 표 생성

### 우선순위 2 — v1.1 Carry-over 정리

- [ ] **Phase 42: 픽셀분해능 런타임 단일소스** (CO-38-01)
  - Success: Shot 단일값 편집 시 재시작 없이 전체 FAI 반영 / PropertyGrid 항목별 노출 정리 / 측정 경로 단일 소스
- [ ] **Phase 43: 시작지연 분리 (LoginManager + SequenceHandler)** (CO-38-02, CO-38-03)
  - Success: 앱 기동 LoginManager lazy-load 후 측정 가능 시점 ≥ 30% 단축 / SequenceHandler 동기 의존성 제거 후 Initialize 가속 입증
- [ ] **Phase 44: 실HW [STARTUP] 재측정** (CO-38-04, HW 도착 시 / 미도착 시 Simul 베이스라인)
- [ ] **Phase 45: A1~A5 측정값 UI 표시** (CO-23-01, Phase 23 ALG-01 잔여)

### 우선순위 3 — HW 도착 시점

- [ ] **Phase 46: CXP 그래버 통합 (RAP 4G 4C12)** (HW-01, HW-02)
  - Success: SDK 확정/설치 / VirtualCamera 인터페이스 호환 / HIK 와 동일 GrabHalconImage 경로 / 4ch grab 실측 PASS
  - HW 미도착 시: Simul 검증으로 마감 (v1.3 이연 가능)

### 우선순위 4 — 시간 여유 시

- [ ] **Phase 47: 헝가리안 표기법 전체 리팩토링** (QUAL-01)
  - Success: 전체 식별자 헝가리안 컨벤션 적용 / msbuild Debug/x64 PASS / 회귀 0 / 기존 코드 스타일과 충돌 없음

### 우선순위 5 — POC 시연 이후 (제어팀 동기화 필요)

- [ ] **Phase 48: 제어 프로토콜 v2.7 — TEST z_index + RESULT 직렬화** (PROTO-01, PROTO-02)
  - Success: `$TEST:site,null,z_index@` 파싱 / `$RESULT:site;P|F|B;count;id=val=OK,...@` 직렬화·역직렬화 / 회귀 0
- [ ] **Phase 49: 제어 프로토콜 v2.7 — 3-state 엔진 + Datum 빈 응답 + CycleState** (PROTO-03, PROTO-04, PROTO-05)
  - Success: P/F/B 종합 판정 / Datum 샷(z_index=0) 빈 응답 / Datum 실패 즉시 F / CycleState·ECycleResult enum + 자동 리셋
- [ ] **Phase 50: 제어 프로토콜 v2.7 — 통신 회귀 시험** (PROTO-06)
  - Success: 제어팀(김민우 선임) 동기화 / 실 핸들러 통신 회귀 PASS / v2.6 → v2.7 마이그레이션 절차 문서화

---

## Progress Table (v1.2)

| 순위 | Phase | 제목 | REQ-IDs | Status | Plans | Date |
|------|-------|------|---------|--------|-------|------|
| 1 | 39 | 검사 워크플로우 E2E | WF-01, WF-02 | Not started | TBD | — |
| 1 | 40 | Export I (리뷰어+1회) | OUT-01, OUT-02 | Not started | TBD | — |
| 1 | 41 | Export II (반복도+통계) | OUT-03, OUT-04 | Not started | TBD | — |
| 2 | 42 | 픽셀분해능 단일소스 | CO-38-01 | Not started | TBD | — |
| 2 | 43 | 시작지연 분리 | CO-38-02/03 | Not started | TBD | — |
| 2 | 44 | 실HW STARTUP 재측정 | CO-38-04 | Not started (HW) | TBD | — |
| 2 | 45 | A1~A5 측정값 UI | CO-23-01 | Not started | TBD | — |
| 3 | 46 | CXP 그래버 통합 | HW-01/02 | Not started (HW) | TBD | — |
| 4 | 47 | 헝가리안 리팩토링 | QUAL-01 | Not started | TBD | — |
| 5 | 48 | Protocol v2.7 TEST/RESULT | PROTO-01/02 | Not started (POC 후) | TBD | — |
| 5 | 49 | Protocol v2.7 3-state/Cycle | PROTO-03/04/05 | Not started (POC 후) | TBD | — |
| 5 | 50 | Protocol v2.7 회귀 시험 | PROTO-06 | Not started (POC 후) | TBD | — |

> v1.0/v1.1 phase 진행표 전문: [milestones/v1.0-ROADMAP.md](milestones/v1.0-ROADMAP.md), [milestones/v1.1-ROADMAP.md](milestones/v1.1-ROADMAP.md)

---

## Backlog

### Phase 999.1: Datum 2-image 지원 (side 검사) — ✅ ABSORBED to v1.1 Phase 27/34/36/37 (2026-05-28)
- Side datum 2-image (4 DualImage / 8-image) 지원 v1.1 종결.

---

*Last updated: 2026-05-29 — v1.2 roadmap created (11 phases, 39~50, 5순위 우선순위).*
