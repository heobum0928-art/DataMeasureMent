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

### Active

**v1.1 Cross-cutting (코드 품질):**
- [ ] PropertyGrid 동적 노출 일반화 — DatumConfig ICustomTypeDescriptor 패턴을 다른 모델로 확장
- [ ] 연산자 정리 — 삼항/null 병합/null 조건 → 명시적 if/else (전체)
- [ ] **헝가리안 표기법 — 전체 리팩토링** (가독성 우선, 신규 코드만 아님)
- [ ] 주석 정리 — "왜"만 남기고 번잡 제거

**v1.1 검사 워크플로우 실측:**
- [ ] Datum→FAI 측정→결과 처리 end-to-end 검증
- [ ] OK/NG/검출 실패 결과별 후속 동작 분기

**v1.1 인프라:**
- [ ] 검사별 이미지 버퍼 (메모리 상주, 속도 우선, 보관 정책 없음)
- [ ] CXP 그래버 보드 드라이버 — RAP 4G 4C12 (Euresys Coaxlink 또는 Matrox)

**v1.1 결과/분석:**
- [ ] 결과 이미지 리뷰어 — 날짜/원본 폴더 로드 → 결과 재현 (위치 TBD)
- [ ] 시퀀스 1회 검사 결과 → 엑셀 export
- [ ] 50회 반복 측정값 → 엑셀 export (반복도 단순 통계, 정식 Gage R&R ANOVA 아님)
- [ ] 검출 알고리즘별 통계 분석표 (TLI/CTH/VTH/Edge 6종)

**Phase 17 carry-over (v1.0 partial):**
- [ ] Test 2 Circle_RadialDirection ItemsSource (Inward/Outward만)
- [ ] Test 8 DatumConfig PropertyGrid 동적 노출 (위 cross-cutting #1 과 통합)
- [ ] Test 10 btn_teachDatum 호환성 가드 spec
- [ ] Length=0 escape hatch 우클릭 메뉴
- [ ] 검출 strip 색상 (성공 녹/실패 빨강)
- [ ] FormatTeachError ROI label 보존

### Out of Scope

- 3D/Laser 측정 — 2D 에지 측정만 사용
- Wafer 검사 시퀀스 — 원본(NewDDA)에만 해당
- OAuth/사용자 인증 고도화 — 기존 LoginManager 유지
- 정식 Gage R&R ANOVA (operator × part × trial) — v1.1 은 단순 반복도 통계만
- 디스크 기반 이미지 캐시 — v1.1 은 메모리 상주만 (속도 우선)

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

## Current Milestone: v1.1 Quality + Workflow + Infrastructure

**Goal:** v1.0 마이그레이션 후속 — 코드 품질 일괄 정비 (헝가리안 전체 리팩토링 + 명시적 if/else + 주석 정리 + PropertyGrid 동적 노출 일반화) + 검사 워크플로우 실측 (Datum→FAI→결과 처리 end-to-end) + 메모리 이미지 버퍼 + CXP 그래버 (RAP 4G 4C12) + 결과 분석/엑셀 export. Phase 17 partial sign-off carry-over 흡수.

**Target features:**
- 코드 품질 cross-cutting: 헝가리안 전체 / PropertyGrid 동적 노출 일반화 / 연산자 정리 / 주석 정리
- 검사 워크플로우 실측: Datum→FAI→결과 end-to-end + OK/NG/검출 실패 분기
- 인프라: 검사별 메모리 이미지 버퍼 (디스크 캐시 X) + CXP 그래버 (Euresys/Matrox SDK 확정)
- 결과/분석: 결과 이미지 리뷰어 (위치 TBD) + 1회 검사 엑셀 + 50회 반복도 엑셀 + 알고리즘별 통계
- Phase 17 carry-over 6건 흡수 (Circle_RadialDirection / 호환성 가드 spec / Length=0 escape hatch / 검출 strip 색상 / FormatTeachError ROI label / DatumConfig PropertyGrid 동적 노출)

**Phase numbering:** 18부터 (v1.0 last=17, continue numbering 모드).

**Status:** v1.0 Halcon Migration MVP shipped 2026-05-04. v1.1 starting 2026-05-04.

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
*Last updated: 2026-05-04 — v1.1 milestone started (continue phase numbering from 18; v1.0 close = 17 phases / 55 plans / 22 deferred items carried over)*
