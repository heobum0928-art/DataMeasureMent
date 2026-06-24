---
phase: 63-tcp-type-align-tcp
plan: 04
subsystem: Sequence/SystemHandler
tags: [protocol-v3, type-echo, align-dispatch, proto-type, av-09]
requires:
  - "63-01 (TestPacket.Type 필드 + AlignTestPacket/AlignCalibPacket 수신)"
  - "63-02 (TestResultPacket.Type + AlignResultPacket/AlignCalibResultPacket 송신)"
provides:
  - "InspectionSequence 응답 3곳 Type echo (AddResponse/BuildDatumShotResponse/BuildScopedResponse)"
  - "SystemHandler MainRun ALIGN_TEST/ALIGN_CALIB dispatch 분기 + ProcessAlignTest/ProcessAlignCalib"
affects:
  - "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs"
  - "WPF_Example/Custom/SystemHandler.cs"
  - "WPF_Example/TcpServer/VisionResponsePacket.cs (AlignCalibResultPacket 개명)"
tech-stack:
  added: []
  patterns:
    - "객체 초기화자 1줄 추가 — Type echo (기존 Target/Site/InspectionType/IsDynamicFAI 블록에 동화)"
    - "bHasPacket bool 변수화 null 가드 + null 반환 패턴 (T-63-11 mitigation)"
    - "Phase 63 ProcessAlignTest/ProcessAlignCalib ack 골격 (Phase 62 연계 예약)"
key-files:
  created: []
  modified:
    - "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs"
    - "WPF_Example/Custom/SystemHandler.cs"
    - "WPF_Example/TcpServer/VisionResponsePacket.cs"
decisions:
  - "Type echo = RequestPacket.Type 객체 초기화자 1줄 추가 — 3곳 동일 패턴, 집계/판정 로직 무변경"
  - "ProcessAlignTest/ProcessAlignCalib = Phase 62 미확정 → ack 골격(IsPass=true 고정), 실 측정 연계는 Phase 62 확정 시"
  - "AlignCalibPacket(응답측) → AlignCalibResultPacket 개명 — 수신측 동명(VisionRequestPacket 파생) 충돌 방지 (Rule 3 deviation)"
metrics:
  duration: 8
  completed: "2026-06-24"
---

# Phase 63 Plan 04: Type echo 전파 + ALIGN dispatch Summary

InspectionSequence 응답 생성 3곳에 RequestPacket.Type echo를 추가하고, SystemHandler MainRun dispatch switch에 $ALIGN_TEST/$ALIGN_CALIB 분기를 통합했다.

## What Was Built

### Task 1: InspectionSequence 응답 3곳 Type echo (커밋 fc5490a)
- `AddResponse()` v2.6 폴백 블록(line 117): `Type = RequestPacket.Type` 추가.
- `BuildDatumShotResponse()`(line 336): `Type = RequestPacket.Type` 추가.
- `BuildScopedResponse()`(line 420): `Type = RequestPacket.Type` 추가.
- 3곳 모두 기존 객체 초기화자(`Target/Site/InspectionType/IsDynamicFAI`) 블록 내 추가 — 집계/판정 로직 무변경.
- v2.6 경로에서도 Type 이 채워지지만 v2.6 직렬화(`Convert`)는 Type 출력 없음 → 회귀 0.

### Task 2: SystemHandler ALIGN dispatch 분기 (커밋 b76af74)
- MainRun switch 에 `case VisionRequestType.AlignTest` / `case VisionRequestType.AlignCalib` 추가 (Unknown case 앞).
- `ProcessAlignTest(AlignTestPacket packet)` — packet null → null 반환; 정상 → `AlignResultPacket(Target/AlignTarget/IsPass=true)` ack.
- `ProcessAlignCalib(AlignCalibPacket packet)` — packet null → null 반환; 정상 → `AlignCalibResultPacket(Target/AlignTarget/IsPass=true)` ack.
- 기존 5 case(Light/RecipeChange/RecipeGet/SiteStatus/Test) 무변경, ProcessTest/ProcessLightSet/SendTestError 무변경.

## Behavior Verification (정적)

- V1 사이클 `RequestPacket.Type="SIDE_3"` → 3개 응답 경로 모두 `TestResultPacket.Type="SIDE_3"` → `BuildResultMessageV1` `;SIDE_3;` echo.
- `$ALIGN_TEST:TRAY@` → `AsAlignTest()` → `ProcessAlignTest` → `AlignResultPacket(AlignTarget="TRAY", IsPass=true)` → `Server.SendPacket` → `ALIGN_RESULT:TRAY;P;`.
- `$ALIGN_CALIB:BOTTOM@` → `AsAlignCalib()` → `ProcessAlignCalib` → `AlignCalibResultPacket(AlignTarget="BOTTOM", IsPass=true)` → `ALIGN_CALIB:BOTTOM;P`.
- packet null → null 반환 → responsePacket == null → 미송신 (MainRun `else if` 조건).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] AlignCalibPacket 동명 충돌 — 응답측 AlignCalibResultPacket 으로 개명**
- **Found during:** Task 2 구현 시 — 수신측(`VisionRequestPacket.cs`)과 응답측(`VisionResponsePacket.cs`) 모두 `AlignCalibPacket`을 `ReringProject.Network`에 선언. SystemHandler.cs 에서 같은 네임스페이스의 두 타입 동시 참조 시 CS0104 ambiguous reference 빌드 오류 예상.
- **Fix:** `VisionResponsePacket.cs` 내 응답측 클래스/생성자/메서드 시그니처를 `AlignCalibResultPacket`으로 일괄 개명 (3곳). 63-02 SUMMARY 에 언급된 예상 충돌 경로를 선제 해소.
- **Files modified:** `WPF_Example/TcpServer/VisionResponsePacket.cs`
- **Commit:** `b76af74` (Task 2 와 동일 커밋에 포함)

## Threat Mitigations Applied

- T-63-11: `ProcessAlignTest`/`ProcessAlignCalib` — `bool bHasPacket = packet != null` 변수화 후 null → `return null` → MainRun 미송신. `AsAlignTest()`/`AsAlignCalib()` 타입 불일치 시 null 반환 → 동일 가드 적용.
- T-63-12: Type echo = 라우팅 토큰 전달 뿐, 권한/판정에 영향 없음 → accept.
- T-63-13: case 추가 only, 기존 case/Process* 무변경. Type echo = 초기화자 1줄 추가 — 집계 로직 불변.

## Known Stubs

- `ProcessAlignTest`/`ProcessAlignCalib` IsPass 고정 `true` — Phase 62 Align 결과 모델 미확정으로 ack 골격만 구현. 실 OffsetX/Y/Theta 측정 값 채움은 Phase 62 연계 시 확장.

## Self-Check: PASSED
- WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs: FOUND
- WPF_Example/Custom/SystemHandler.cs: FOUND
- WPF_Example/TcpServer/VisionResponsePacket.cs: FOUND
- Commit fc5490a (Task 1): FOUND
- Commit b76af74 (Task 2): FOUND
