---
phase: 65-bottom-4jig-face-align-2026-06-25
plan: "01"
subsystem: EthernetVision / AlignShapeMatchService
tags: [align, shape-matching, slot, bottom, ethernet-vision, AV-08]
dependency_graph:
  requires: []
  provides: [EBottomAlignSlot enum, EBottomAlignSlotMap 매퍼, AlignShapeMatchService slot API]
  affects: [BottomVisionView (Plan 02), ProcessAlignTest 라우팅 (Plan 03)]
tech_stack:
  added: []
  patterns: [오버로드 하위호환, EndsWith 마지막 접미사 제거, enum 매퍼 정적 클래스]
key_files:
  created:
    - WPF_Example/Custom/EthernetVision/EBottomAlignSlot.cs
  modified:
    - WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs
    - WPF_Example/DatumMeasurement.csproj
decisions:
  - "TryTeach slot 파라미터는 기본값 방식 대신 오버로드 2개 분리 — out 파라미터와 기본값 동시 사용 시 C# 7.2 호출자 모호성 회피"
  - "BuildJsonPath: Replace 전체 치환 방지 → EndsWith 체크 후 Substring 마지막 _1 제거 (2D_SIDE_1 토큰 안전)"
  - "HasTemplate/TryLoadTemplate/Run: slot 기본값 = EBottomAlignSlot.None (기존 호출자 오버로드 불필요)"
metrics:
  duration: "~15 min"
  completed: "2026-06-26"
  tasks: 2
  files: 3
requirements: [AV-08]
---

# Phase 65 Plan 01: EBottomAlignSlot enum + AlignShapeMatchService 6슬롯 확장 Summary

## One-liner

EBottomAlignSlot 7값 enum(None=-1, 슬롯 0~5) + AlignShapeMatchService 경로/API slot 파라미터 확장으로 Bottom 면별 6세트 모델 독립 경로 지원.

## What Was Built

### Task 1: EBottomAlignSlot enum + 토큰/AlignFace 매퍼 신규 파일 (f53b837)

`WPF_Example/Custom/EthernetVision/EBottomAlignSlot.cs` 신규 생성:

- `EBottomAlignSlot` enum 7값: `None=-1`(폴백 센티넬) + `Slot3DTop=0` ~ `Slot2DSide2=5`(AlignFace 0~5 1:1 정렬, D-03)
- `EBottomAlignSlotMap` 정적 클래스 3개 메서드:
  - `ToFileToken(slot)`: 파일명 토큰 반환 (예: `"3D_Top"`, `"2D_SIDE_1"`). None → `""` (폴백 신호, D-02)
  - `FromAlignFace(int)`: TCP AlignFace 0~5 → 슬롯. 범위 외(음수/6이상) → `None` (T-65-01 OOB 방지)
  - `ToDisplayLabel(slot)`: UI 라벨. None → `"(단일)"`
- `DatumMeasurement.csproj` Compile Include 등록 (CS0246 방지)

### Task 2: AlignShapeMatchService slot 파라미터 확장 (bced792)

`WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs` 수정:

**경로 헬퍼 (private):**
- `BuildShmPath(mode, modelIndex, slot=None)`: mode==Bottom && slot!=None 이면 `"Bottom_" + token` + `_N.shm`; 그 외(None/Tray) 기존 경로 그대로 (D-09, D-10)
- `GetShmPath(mode, modelIndex, slot=None)`: BuildShmPath 위임 + Directory.CreateDirectory
- `BuildJsonPath(mode, slot=None)`: EndsWith 방식 마지막 `_1` 제거 → `Bottom_2D_SIDE_1_1.shm` → `Bottom_2D_SIDE_1.json` 안전 산출

**공개 API:**
- `HasTemplate(mode, slot=None)`: slot 반영 경로 존재 확인
- `TryLoadTemplate(mode, slot=None)`: HasTemplate 위임
- `TryTeach(...)`: 기존 호출자 하위호환 오버로드(slot 없음, None 위임) + 슬롯 오버로드(slot 명시) 2개 분리
- `Run(img, mode, slot=None)`: slot 반영 경로로 TryFindPose 실행

**비-slot 메서드 무변경:** TrySaveRefPose / LoadRefPose / ComputeAngleLx / BuildDetectedRoiBox / TryBuildDetectedContourXld / TryBuildMovedContour / ApplyPickerCenterCorrection — 시그니처·본문 무변경.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - 하위호환 오버로드] TryTeach slot 파라미터 기본값 대신 2-오버로드 분리**
- **발견:** TryTeach에 `out string error` 앞에 `EBottomAlignSlot slot` 추가 시, `out` 있는 메서드에서 기본값 파라미터와 혼합하면 기존 호출자 컴파일 오류 (TrayVisionView, BottomVisionView 의 기존 호출 9인자 → 10인자 불일치)
- **수정:** slot 없는 기존 오버로드(기존 호출자 호환) + slot 있는 신규 오버로드 2개 분리. 기존 오버로드가 `EBottomAlignSlot.None`으로 신규 오버로드 위임
- **파일:** AlignShapeMatchService.cs
- **커밋:** bced792

**2. [Rule 1 - 버그 사전 방지] BuildJsonPath _1 오치환 수정**
- **발견:** 계획서 명시 경고: 기존 `Replace(MODEL_SUFFIX_1, "")` 가 `Bottom_2D_SIDE_1_1` 파일명에서 두 `_1` 모두 치환 → `Bottom_2D_SIDE_` 로 잘못된 json 경로 산출
- **수정:** `EndsWith(MODEL_SUFFIX_1)` 체크 후 `Substring(0, baseName.Length - MODEL_SUFFIX_1.Length)` 로 마지막 `_1`만 제거 → `Bottom_2D_SIDE_1.json` 정확 산출
- **파일:** AlignShapeMatchService.cs
- **커밋:** bced792

## Path Output Verification

| 입력 | 기대 경로 | 검증 |
|------|----------|------|
| mode=Bottom, slot=None, idx=1 | `Bottom_1.shm` | BuildShmPath 분기: slot==None → modeFileName="Bottom" 그대로 |
| mode=Bottom, slot=Slot3DTop, idx=1 | `Bottom_3D_Top_1.shm` | token="3D_Top" → "Bottom_3D_Top_1.shm" |
| mode=Bottom, slot=Slot2DSide1, idx=1 | `Bottom_2D_SIDE_1_1.shm` | token="2D_SIDE_1" → "Bottom_2D_SIDE_1_1.shm" |
| BuildJsonPath(Bottom, Slot2DSide1) | `Bottom_2D_SIDE_1.json` | EndsWith("_1") 제거 → "Bottom_2D_SIDE_1.json" |
| BuildJsonPath(Bottom, None) | `Bottom.json` | "Bottom_1".EndsWith("_1") → "Bottom.json" |
| mode=Tray | `Tray_1/2.shm` + `Tray.json` | modeFileName="Tray" (D-10 무변경) |

## Threat Surface Scan

T-65-01 (Tampering/DoS): `FromAlignFace` 범위 외 정수(음수/6이상) → `None` 반환. 인덱스 산술 없이 if-else 매핑만 사용 → OOB 불가. 구현 완료.

T-65-02 (Information Disclosure): 슬롯 토큰은 내부 상수 집합에서만 생성(ToFileToken 반환값), 외부 문자열 미사용 → path traversal 불가. accept 처리됨.

## Known Stubs

없음. 이 계획은 데이터/서비스 기반 레이어만 구성하며, UI(Plan 02)와 TCP 라우팅(Plan 03)이 이 slot API를 소비한다.

## Self-Check

- EBottomAlignSlot.cs 존재: FOUND
- AlignShapeMatchService.cs 수정: FOUND
- DatumMeasurement.csproj Compile Include: FOUND
- 커밋 f53b837: FOUND
- 커밋 bced792: FOUND
- msbuild Debug/x64 PASS: PASS (DatumMeasurement.exe 생성, CS 에러 0)

## Self-Check: PASSED
