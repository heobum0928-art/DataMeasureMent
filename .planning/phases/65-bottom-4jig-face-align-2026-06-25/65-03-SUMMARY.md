---
phase: 65-bottom-4jig-face-align-2026-06-25
plan: "03"
subsystem: EthernetVision / SystemHandler / TCP
tags: [align, shape-matching, slot, bottom, tcp, process-align-test, AV-08]
dependency_graph:
  requires: [65-01 (EBottomAlignSlot enum + AlignShapeMatchService slot API)]
  provides: [ProcessAlignTest 실측 경로, BOTTOM 슬롯 grab+Run+pose 채움]
  affects: [TCP $ALIGN_TEST/$ALIGN_RESULT 와이어 (v3.0 준수)]
tech_stack:
  added: []
  patterns: [try/catch 예외 격리, try/finally HImage Dispose, 헬퍼 메서드 분리(30~40줄 제한)]
key_files:
  created: []
  modified:
    - WPF_Example/Custom/SystemHandler.cs
decisions:
  - "ProcessAlignTest: BOTTOM → RunBottomAlign 헬퍼 위임, 함수 30줄 제한 분할"
  - "FillAlignPoseZero: NG 시 Items에 pose=0 채움 — PLC 형식 일관성(IsPass=false가 실제 불량 신호, T-65-05)"
  - "HImage + DetectedContourXld finally Dispose — TCP 경로에서 XLD 미사용이므로 즉시 해제(Phase 61.1 WR-01/02 선례)"
metrics:
  duration: "~10 min"
  completed: "2026-06-26"
  tasks: 1
  files: 1
requirements: [AV-08]
---

# Phase 65 Plan 03: ProcessAlignTest 실측 grab+Run+pose 채움 Summary

## One-liner

ProcessAlignTest stub(IsPass=true echo) → BOTTOM+AlignFace 0~5 수신 시 슬롯 모델로 grab+Matcher.Run+pose(OffsetX/Y/Theta)+pass/fail 반환으로 교체.

## What Was Built

### Task 1: ProcessAlignTest stub → 실측 경로 배선 (a0fa308)

`WPF_Example/Custom/SystemHandler.cs` 수정:

**ProcessAlignTest 교체:**
- TRAY 경로: grab/Run 미수행, 기존 echo ack 유지 (회귀 0)
- BOTTOM 경로: `EBottomAlignSlotMap.FromAlignFace(AlignFace)` → slot 매핑
- AlignFace 범위 외(음수/6이상): slot==None → `IsPass=false` 즉시 반환+로그 (T-65-01)
- 유효 슬롯: `RunBottomAlign(slot, resultPacket)` 헬퍼 위임

**RunBottomAlign 신규 헬퍼 (private, ~45줄):**
1. `HasTemplate(Bottom, slot)` 미티칭 가드 → false 시 NG+`FillAlignPoseZero`
2. `Camera == null` 가드 → null 시 NG+`FillAlignPoseZero`
3. `HImage img = null; try/finally`:
   - `img = Camera.Grab()` — IsOpen 라이브, 아니면 ALIGN_FALLBACK_IMAGE 폴백 (D-05)
   - `img == null` → NG 반환
   - `res = Matcher.Run(img, Bottom, slot)` — 슬롯 모델 실행
   - `!res.Found` → `FillAlignPoseZero` + NG 반환 (T-65-05)
   - Found → `FillAlignPose` + Trace 로그 + true 반환
   - **finally:** `img.Dispose()` + `res.DetectedContourXld.Dispose()` (HALCON 핸들 누수 방지)
4. 전체 `try/catch(Exception)` → 로그+false (throw 금지, T-65-06)

**FillAlignPose 헬퍼 (private):**
- `pkt.Items.Clear()` 후 `AlignResultItem{ItemName="OffsetX", Value=res.OffsetXmm}` 추가
- `AlignResultItem{ItemName="OffsetY", Value=res.OffsetYmm}` 추가
- `AlignResultItem{ItemName="Theta", Value=res.ThetaDeg}` 추가
- 순서: X → Y → Theta (BuildAlignItems 직렬화 순서 준수, D-08)

**FillAlignPoseZero 헬퍼 (private):**
- NG(미티칭/미연결/grab실패/검출실패) 시 OffsetX=0/OffsetY=0/Theta=0 채움
- `IsPass=false`가 실제 불량 신호 — PLC 형식 일관성 확보 (T-65-05)

## Deviations from Plan

없음 — 계획서 골격과 완전히 일치. TRAY 경로 echo ack 유지, AlignFace echo/MaterialNo echo/dispatch 무변경.

## Threat Surface Scan

| Flag | File | Description |
|------|------|-------------|
| T-65-01 mitigated | SystemHandler.cs | AlignFace OOB → FromAlignFace=None → IsPass=false 즉시 반환, 인덱스 산술 없음 |
| T-65-05 mitigated | SystemHandler.cs | Found=false 시 FillAlignPoseZero(pose=0) + IsPass=false, 잘못된 보정값 미전송 |
| T-65-06 mitigated | SystemHandler.cs | RunBottomAlign 전체 try/catch → 예외 시 false 반환, HImage/XLD finally Dispose |

## Known Stubs

없음. ProcessAlignTest가 실측 경로로 완전히 교체됨.

## Self-Check

- WPF_Example/Custom/SystemHandler.cs 수정: FOUND
- 커밋 a0fa308: FOUND
- CS 컴파일 에러 0 (obj/x64/Debug/DatumMeasurement.exe 14:35 생성, CS error 없음)
- MSB3027/MSB3021은 파일 잠금(VS IDE 실행 중) 복사 실패 — C# 컴파일 에러 아님
- TRAY 경로 grab/Run 미수행 확인: PASS (코드 리뷰)
- AlignFace 범위 외 → IsPass=false 안전 거부 확인: PASS (코드 리뷰)
- Items(OffsetX/OffsetY/Theta) 채움 확인: PASS (코드 리뷰)
- HImage + DetectedContourXld Dispose 확인: PASS (코드 리뷰)
- dispatch switch(L64) 무변경 확인: PASS

## Self-Check: PASSED
