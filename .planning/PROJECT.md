# DataMeasurement

## What This Is

WPF 기반 산업용 비전 검사 시스템. Halcon 24.11 이미지 처리로 카메라(Top/Side/Bottom) 이미지에서 에지 측정 + Datum 기준좌표 보정 + 공차 판정을 수행한다. TCP 서버로 외부 핸들러/호스트와 통신하고, 시퀀스 엔진(Top/Bottom/Inspection)이 Shot 순회 Grab + FAI 측정을 자동화한다.

원본(NewDDA, MIL+OpenCV 5대)을 Halcon+HIK 3대 구성으로 마이그레이션한 프로젝트. v1.0 에서 마이그레이션 완료 + Datum 3 알고리즘(TLI/CTH/VTH) + per-ROI 에지 파라미터 + 티칭/검증 UX 완성.

## Core Value

Shot-FAI 2계층 동적 구조로 100개+ 검사 항목을 유연하게 관리하고, Halcon 에지 측정으로 정밀한 거리 측정(mm) + 공차 판정 + Datum 자동 보정을 수행하는 것.

## Requirements

### Validated

- ✓ SystemHandler 싱글턴 오케스트레이터 — existing
- ✓ 시퀀스 엔진 (SequenceBase/ActionBase) 프레임워크 — existing
- ✓ Top/Bottom 카메라 시퀀스 + Inspection 액션 — existing
- ✓ Halcon 비전 레이어 (MeasurementAlgorithm, RoiLineIntersection, TeachingStorage) — existing
- ✓ TCP 서버 통신 (VisionServer, 패킷 처리, ResourceMap) — existing
- ✓ 카메라 디바이스 레이어 (VirtualCamera, Basler, HIK) — existing
- ✓ INI 기반 레시피/설정 관리 — existing
- ✓ WPF UI (MainView, InspectionListView, TeachingWindow) — existing
- ✓ SIMUL_MODE 시뮬레이션 지원 — existing
- ✓ Shot-FAI 2계층 데이터 모델 — v1.0 (Phase 5)
- ✓ FAI-centric TreeView 2계층 UI + 단일 캔버스 + 결과 테이블 + CRUD — v1.0 (Phase 1)
- ✓ Main 화면 통합 티칭 (Grab + ROI Rect/Polygon/Circle 드래그 + 캘리브레이션) — v1.0 (Phase 2, 11)
- ✓ Halcon 에지 측정 6종 + per-ROI 파라미터 (EdgeDirection/Selection/SampleCount/TrimCount/Polarity) — v1.0 (Phase 3, 6, 13, 15)
- ✓ 검사 시퀀스 (Shot 순회 Grab + FAI 측정) + TCP 결과 응답 — v1.0 (Phase 5, 6)
- ✓ Datum 좌표계 (TwoLineIntersect/CircleTwoHorizontal/VerticalTwoHorizontal 3 알고리즘) — v1.0 (Phase 4, 12, 14)
- ✓ Datum 티칭/검증 UX (ROI 그리기/이동/삭제 + AlgorithmType 동적 PropertyGrid + Test Find DetectedOrigin 시각화) — v1.0 (Phase 11, 13, 17)
- ✓ Rapid City 확장 (Fixture/Multi-Datum + 6 Measurement + 조명 필드 + 새 INI 포맷) — v1.0 (Phase 6)
- ✓ 제어 프로토콜 v1.0 (디팜스테크) 커맨드 계층 — TEST 유연파서(자재 IndexNumber)/RESULT P-F-B 직렬화 구조/Site 2-PC(PcRole 런타임)/자재번호 Export·파일명 전파, v2.6 플래그 공존 — v1.2 (Phase 48, 코드 5/5 검증·빌드 PASS; 실통신 UAT 5건은 Phase 49/50 통합 검증 대기)

### Active

**v1.2 우선순위 1 — POC 시연 필수 (~2026-06-30):**
- [ ] 검사 워크플로우 E2E — Datum→FAI 측정→결과 분기 (OK/NG/검출 실패)
- [ ] 결과 분석/Export — 1회 검사 엑셀 + 50회 반복도 + 알고리즘별 통계 + 결과 이미지 리뷰어

**v1.2 우선순위 2 — v1.1 carry-over 정리:**
- [ ] CO-38-01 픽셀분해능 런타임 단일소스
- [ ] CO-38-02/03 시작지연 LoginManager/SequenceHandler 분리
- [ ] CO-38-04 실HW [STARTUP] 재측정
- [ ] CO-23-01 A1~A5 측정값 UI 표시

**v1.2 우선순위 3 — 장비 도착 시점 합류:**
- [ ] CXP 그래버 보드 드라이버 — RAP 4G 4C12 (Euresys/Matrox SDK 확정)
- HW 미도착 시 Simul 검증으로 마감, 도착 시 우선순위 재조정

**v1.2 우선순위 4 — 시간 여유 시:**
- [ ] **헝가리안 표기법 — 전체 리팩토링** (v1.1 deferred)

**v1.2 우선순위 5 — POC 시연 이후:**
- [~] 제어 프로토콜 v1.0 (디팜스테크) — TCP/IP 통신 사양 (구 'v2.7' 명칭, 엑셀 v1.0 규격). **Phase 48 완료: TEST 유연파서 / RESULT 직렬화 구조 / Site 2-PC / 자재번호 전파 (코드 검증)**. 잔여(P/F/B 판정엔진·CycleState·Datum 빈응답·나머지 커맨드)=Phase 49, 통신 회귀시험=Phase 50.
  - TEST 커맨드: `$TEST:site,자재번호,null,z_index@` (유연 파서 — Phase 48 ✓)
  - RESULT 포맷: `$RESULT:site;P|F|B;count;id=val=OK,...@` (;/,/= 3단 구분자 — 직렬화 구조 Phase 48 ✓)
  - P/F/B 3-state 판정 — NG 발견 시 즉시 종료 X, 마지막 Index 까지 진행 후 종합 판정
  - Datum 샷(z_index=0): 빈 응답. Datum 실패 시 즉시 F
  - 멀티샷 사이클 state (CycleState, ECycleResult enum 신설)
  - InspectionSequence 사이클 단위 NG mark + 자동 리셋

### Out of Scope

- 3D/Laser 측정 — 2D 에지 측정만 사용
- Wafer 검사 시퀀스 — 원본(NewDDA)에만 해당
- OAuth/사용자 인증 고도화 — 기존 LoginManager 유지
- 정식 Gage R&R ANOVA (operator × part × trial) — v1.1/v1.2 모두 단순 반복도 통계만
- 디스크 기반 이미지 캐시 — 메모리 상주만 (속도 우선)
- 제어 프로토콜 v2.7 동시 진행 — POC 시연(6월 말) 중 통신 사양 변경 부담 회피, 시연 후 제어팀(김민우 선임)과 동기화 후 진행

## Context

- 원본 프로젝트: D:\Project\NewDDA (MIL + OpenCV, HIK 5대)
- 현재 프로젝트: C:\Info\Project\DataMeasurement (Halcon 24.11, HIK 3대)
- 카메라: Top/Side/Bottom (2448x2048), Z축 이동으로 Shot 위치 결정
- 구조: 1 카메라 → N Shot(Z축 위치) → 각 Shot 내 M개 FAI(측정 영역)
- v1.0 결과: 64,057 LOC C# (158 files) + 3,329 LOC XAML + 17 phases / 55 plans / 330 commits / 49일 (2026-03-17 → 2026-05-04)
- v1.0 deferred: 22건 (quick task artifact 11 + UAT partial 7 + verification 4) — STATE.md `## Deferred Items` 참조
- SIMUL_MODE: D:\1.bmp 사용, 하드웨어 없이 개발/테스트
- framework+custom 분리: WPF_Example/Sequence/ (프레임워크) + WPF_Example/Custom/ (프로젝트 전용)
- v1.1 신규 하드웨어: RAP 4G 4C12 CXP 프레임 그래버 보드

## Constraints

- **Tech stack**: .NET Framework 4.8 + WPF + Halcon 24.11 — 변경 불가
- **Architecture**: SystemHandler 싱글턴 + SequenceBase/ActionBase 패턴 유지
- **Compatibility**: 기존 INI 레시피 포맷과 하위 호환 (IsDynamicFAIMode + EnsurePerRoiDefaults 패턴)
- **Hardware**: HIK 카메라 SDK (MvCamCtrl.Net), 실제 테스트는 SIMUL_MODE 로 대체
- **C# version**: C# 7.2 (csproj 강제) — C# 8+ 기능 (nullable refs, switch expressions, records) 사용 금지
- **v1.1 신규 컨벤션**: 헝가리안 표기법 + 명시적 if/else (전체 리팩토링)

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| MIL → Halcon 변환 | Halcon이 에지 측정에 더 적합 | ✓ Good (v1.0 완료) |
| Shot-FAI 2계층 동적 구조 | 100개+ 검사 항목 대응, 기존 5탭 구조 한계 | ✓ Good (Phase 5) |
| CameraSlaveParam 상속으로 ShotConfig 구현 | 기존 프레임워크와 호환 | ✓ Good (Phase 5) |
| 기존/신규 INI 포맷 공존 (IsDynamicFAIMode) | 하위 호환 유지 | ✓ Good (Phase 5) |
| Datum AlgorithmType — switch 디스패치 (Strategy 패턴 X) | 단순 + 코드 locality | ✓ Good (Phase 12, 13 deferred refactor) |
| EnsurePerRoiDefaults idempotent 마이그레이션 | per-ROI 30 신규 필드 INI 하위호환 | ✓ Good (Phase 13-04) |
| HALCON MeasurePos measurePhi 명시 매핑 (rp 자동도출 의존 폐기) | BtoT/TtoB 부호 결함 직접 해결 | ✓ Good (Phase 15-02) |
| Circle 알고리즘 — 360° polar sampling (Phase 14-04) | strip 회전식 폐기 + raw 점 시각화 가능 | ✓ Good (Phase 14, 17) |
| ICustomTypeDescriptor — DatumConfig 동적 PropertyGrid | 알고리즘별 무관 속성 hide | ✓ Good (Phase 17-02, v1.1 일반화 예정) |
| Auto-reteach off (사용자 명시 트리거) | 리소스 과부하 제거 | ✓ Good (Phase 16-02) |
| 헝가리안 표기법 전체 리팩토링 | 가독성 우선 (사용자 명시) | — Pending (v1.1) |
| 메모리 상주 이미지 버퍼 (디스크 캐시 X) | 검사 속도 요구 | — Pending (v1.1) |
| Gage R&R = 단순 반복도 통계 (ANOVA X) | 정식 분산 분해 불필요 | — Pending (v1.1) |
| Side Inspection PC 분리 (PC1: Top/Bottom, PC2: Side) — 2026-05-08 | 물리적 독립 배포, 동일 SW 인스턴스, 각자 TCP로 제어팀에 결과 전송 | ✓ Good (v1.1 Phase 27/33/35) |
| 제어 프로토콜 v1.0 = v2.6 플래그 공존 (교체 X), 신규 ESiteV1 폐기→기존 ESite 슬롯 재해석 + PcRole 런타임 분기 — 2026-06-22 | 회귀 0 (UseProtocolV1=false 시 v2.6 byte-identical), framework ResourceMap 무수정, 한 바이너리로 PC1/PC2 유연 | ✓ Good (Phase 48, 코드 검증·UAT 대기) |
| LineToLineAngle 알고리즘 신규 추가 (Phase 27) — 2026-05-08 | D1/H5 각도 측정 대응, Back light 2D | ✓ Good (v1.1 Phase 27) |
| Side Fixture Datum = TwoLineIntersect 재사용 — 2026-05-08 | 교점 계산만 필요, Plane C/D는 OMM 전용 절차로 2D 비전 무관 | ✓ Good (v1.1 Phase 27) |
| v1.2 우선순위 5단계 (POC 6월 말 기준) — 2026-05-29 | WF/OUT 시연 필수 → carry-over → HW 도착 시 합류 → 헝가리안 → Protocol v2.7 (POC 후) | — Pending (v1.2) |
| 제어 프로토콜 v2.7 = v1.2 5순위 (POC 시연 이후) — 2026-05-29 | 시연 중 통신 사양 변경 부담 회피 + 제어팀(김민우 선임) 동기화 필요 | — Pending (v1.2) |
| HW 미도착 시 Simul 검증으로 마감 — 2026-05-29 | RAP 4G 4C12 배송 지연 가능성, POC 시연 일정 고정 | — Pending (v1.2) |

## Current Milestone: v1.3 Align 비전 (이더넷 카메라) — started 2026-06-23

**Goal:** 기존 Grabber 검사(Top/Bottom/Side)와 **완전 독립**으로 같은 DataMeasurement 실행파일에 공존하는, 이더넷 카메라(Hikvision MV-CH250-90GM GigE) 기반 Tray/Bottom Align 비전 서브시스템을 추가한다. Shape Matching 으로 X/Y(+Bottom Theta) Offset 산출 → TCP 전송, MainWindow TabControl 통합.

**Target features (Phase 58~62, A~E):**
- **A (58) Config & Camera**: EthernetVisionConfig(INI [ETHERNET_VISION], None/Tray/Bottom) + 독립 이더넷 카메라 (AV-01/02)
- **B (59) Vision Algorithm**: AlignShapeMatchService(.shm 티칭/매칭) + Tray/Bottom X/Y/Theta (AV-03/04)
- **C (60) Calibration (Bottom)**: 피커 센터 캘(36스텝 편심원) + 각도 캘 (AV-05/06)
- **D (61) UI TabControl**: [검사]/[Tray]/[Bottom] 탭 통합, 모드별 Visibility (AV-07/08)
- **E (62) TCP**: $RESULT site=TRAY/BOTTOM 전송 (AV-09)

**핵심 제약:** 기존 Grabber 코드 절대 수정 금지(추가만) · 독립 동작(이더넷 카메라 실패해도 Grabber 정상) · 헝가리언 · C# 7.2 · Halcon try-catch · SIMUL_MODE=D:\align_test.bmp. 참조: D:\Backup\파이널비전\WPF_Example_260604 (TabControl 구조).

**Phase numbering:** 58부터 (v1.2 마지막 사용 번호 57.1 다음, continue numbering). **코드 작성 전 phase 별 설계 제안 → 동의 후 구현.**

---

## 병행 마일스톤: v1.2 POC Workflow + Output + Carry-over + Protocol v2.7 (열어둔 채 carry-over 진행)

**Goal:** POC 시연(2026-06-30) 대응 — 검사 워크플로우 E2E + 결과 분석/Export + v1.1 carry-over 정리 + HW 도착 시 CXP 합류 + 헝가리안 리팩토링(시간 여유 시) + 제어 프로토콜 v2.7 (POC 후).

**Target features (5순위 우선순위):**
- **1순위 (POC 필수)**: WF-01/02 검사 워크플로우 E2E + OUT-01~04 결과 분석/Export
- **2순위 (carry-over)**: CO-38-01 픽셀분해능 / CO-38-02-03 시작지연 / CO-38-04 실HW STARTUP / CO-23-01 A1~A5 UI
- **3순위 (HW 도착)**: HW-01/02 CXP 그래버 (RAP 4G 4C12, 미도착 시 Simul 검증)
- **4순위 (여유 시)**: QUAL-01 헝가리안 표기법 전체 리팩토링
- **5순위 (POC 후)**: PROTO-01 제어 프로토콜 v2.7 — TEST z_index / RESULT P/F/B 3-state / CycleState / Datum 빈 응답

**Phase numbering:** 39부터 (v1.1 last=38, continue numbering 모드).

**Status:** v1.1 Quality+Workflow+Algorithm shipped 2026-05-28 (git tag v1.1). v1.2 starting 2026-05-29.

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-06-23 — **v1.3 Align 비전 (이더넷 카메라) 마일스톤 시작** (phases 58~62 A~E, REQUIREMENTS AV-01~09). 기존 Grabber 검사와 완전 독립 서브시스템. v1.2 는 열어둔 채 병행. 이전: Phase 53 캘리브 완료 2026-06-23.*
