# DataMeasurement

## What This Is

WPF 기반 산업용 비전 검사 시스템. Halcon 이미지 처리를 사용하여 카메라(Top/Side/Bottom)로 촬영한 이미지에서 에지 측정 및 공차 판정을 수행한다. TCP 서버를 통해 외부 장비(핸들러/호스트)와 통신하며, 시퀀스 엔진이 검사 흐름을 관리한다.

원본 프로젝트(NewDDA, MIL+OpenCV)를 Halcon 기반으로 변환한 프로젝트이다.

## Core Value

Shot-FAI 2계층 동적 구조로 100개 이상의 검사 항목을 유연하게 관리하고, Halcon 에지 측정으로 정밀한 거리 측정(mm) + 공차 판정을 수행하는 것.

## Requirements

### Validated

- ✓ SystemHandler 싱글턴 오케스트레이터 구조 — existing
- ✓ 시퀀스 엔진 (SequenceBase/ActionBase) 프레임워크 — existing
- ✓ Top/Bottom 카메라 시퀀스 + Inspection 액션 — existing
- ✓ Halcon 비전 레이어 (MeasurementAlgorithm, RoiLineIntersection, TeachingStorage) — existing
- ✓ TCP 서버 통신 (VisionServer, 패킷 처리, ResourceMap) — existing
- ✓ 카메라 디바이스 레이어 (VirtualCamera, Basler, HIK) — existing
- ✓ INI 기반 레시피/설정 관리 — existing
- ✓ WPF UI (MainView, InspectionListView, TeachingWindow) — existing
- ✓ SIMUL_MODE 시뮬레이션 지원 — existing
- ✓ Shot-FAI 2계층 데이터 모델 (ShotConfig, FAIConfig, InspectionRecipeManager, Action_FAIMeasurement) — Phase 5 완료

### Active

- [ ] UI 재설계 — TreeView 2계층(Shot/FAI) + 단일 캔버스
- [ ] 티칭 통합 — Main 화면에서 Grab + 멀티 ROI 티칭
- [ ] Halcon 에지 측정 알고리즘 — FAI별 에지 간 거리 측정(mm) + 공차 판정
- [ ] 검사 시퀀스 + TCP 통신 — Shot 순회 Grab/측정 + TCP 결과 응답

### Out of Scope

- 3D/Laser 측정 — 2D 에지 측정만 사용
- Wafer 검사 시퀀스 — 원본(NewDDA)에만 해당
- Side 카메라 검사 — 현재 Top/Bottom만 대상
- OAuth/사용자 인증 고도화 — 기존 LoginManager 유지

## Context

- 원본 프로젝트: D:\Project\NewDDA (MIL + OpenCV, HIK 5대)
- 현재 프로젝트: C:\Info\Project\DataMeasurement (Halcon, HIK 3대)
- 카메라: Top/Side/Bottom (2448x2048), Z축 이동으로 Shot 위치 결정
- 구조: 1 카메라 → N Shot(Z축 위치) → 각 Shot 내 M개 FAI(측정 영역)
- Phase 5 완료: ShotConfig, FAIConfig, InspectionRecipeManager, Action_FAIMeasurement 생성
- SIMUL_MODE: D:\1.bmp 사용, 하드웨어 없이 개발/테스트
- framework+custom 분리: WPF_Example/Sequence/ (프레임워크) + WPF_Example/Custom/ (프로젝트 전용)

## Constraints

- **Tech stack**: .NET Framework 4.8 + WPF + Halcon 24.11 — 변경 불가
- **Architecture**: SystemHandler 싱글턴 + SequenceBase/ActionBase 패턴 유지
- **Compatibility**: 기존 INI 레시피 포맷과 하위 호환 (IsDynamicFAIMode 분기)
- **Hardware**: HIK 카메라 SDK (MvCamCtrl.Net), 실제 테스트는 SIMUL_MODE로 대체

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| MIL → Halcon 변환 | Halcon이 에지 측정에 더 적합 | — Pending |
| Shot-FAI 2계층 동적 구조 | 100개+ 검사 항목 대응, 기존 5탭 구조 한계 | ✓ Good (Phase 5) |
| CameraSlaveParam 상속으로 ShotConfig 구현 | 기존 프레임워크와 호환 | ✓ Good (Phase 5) |
| 기존/신규 INI 포맷 공존 | IsDynamicFAIMode로 자동 감지, 하위 호환 | ✓ Good (Phase 5) |

## Current Milestone: v1.0

**Goal:** Phase 5에서 만든 Shot-FAI 데이터 모델 위에 UI, 티칭, 에지 측정 알고리즘, 검사 시퀀스를 완성하여 실제 검사가 가능한 시스템을 구축한다.

**Target features:**
- TreeView 2계층 UI (Shot/FAI) + 단일 캔버스
- Main 화면 통합 티칭 (Grab + 멀티 ROI)
- Halcon 에지 측정 (FAI별 거리 mm + 공차 판정)
- 검사 시퀀스 (Shot 순회) + TCP 결과 응답

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd:transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd:complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-04-02 after initialization*
