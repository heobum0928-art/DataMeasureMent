---
phase: 63-tcp-type-align-tcp
plan: 02
subsystem: TcpServer
tags: [protocol-v3, tcp-send, align, proto-type, av-09]
requires:
  - "63-01: 수신측 Type 필드 + Align 수신 커맨드 (대칭 송신측)"
provides:
  - "TestResultPacket.Type 필드 + BuildResultMessageV1 Type echo (V1 RESULT 헤더 Type 토큰)"
  - "AlignResultPacket / AlignCalibPacket 송신 패킷 + 빌더 (AV-09 송신측)"
affects:
  - "VisionResponsePacket.Convert(VisionResponsePacket) — Align 2 case"
tech-stack:
  added: []
  patterns:
    - "bIsPass bool 변수화 + if/else 직렬화 (C# 7.2, 삼항/null병합 금지)"
    - "i>0 분리자 패턴 (BuildFaiItemsV1 차용) — BuildAlignItems"
    - "V1/Align 경로 격리 — Type echo/Align 빌더는 v2.6 Test 블록 무영향 (회귀 0)"
key-files:
  created: []
  modified:
    - "WPF_Example/TcpServer/VisionResponsePacket.cs"
decisions:
  - "TestResultPacket.Type 기본값 \"\" → v2.6 경로/미설정에서 안전. Type echo 는 BuildResultMessageV1(V1) 한정"
  - "Type 빈값이면 ;; 자리 보존 → count/판정 인덱스 어긋남 방지 (T-63-05 mitigation)"
  - "Align 항목 = 가변 List<AlignResultItem> 로 Tray(2: OffsetX/Y)/Bottom(3: +Theta) 모두 수용. Phase 62 모델 미확정 → Items 채움은 후속"
  - "AlignCalib 응답 = target;P|F 만 (항목 없음 — 캘리브 ack)"
metrics:
  duration: 6
  completed: "2026-06-24"
---

# Phase 63 Plan 02: TCP 송신 Type echo + Align 응답 빌더 통합 Summary

디팜스테크 Vision Protocol v3.0 송신측 — UseProtocolV1=true 한정으로 $RESULT 헤더에 Type 을 echo($RESULT:site;Type;P|F|B;count;...)하고, Align 전용 응답($ALIGN_RESULT/$ALIGN_CALIB)을 기존 RESULT 프레임워크와 동형 빌더로 생성했다. 63-01(수신측)과 대칭.

## What Was Built

### Task 1: TestResultPacket.Type 필드 + BuildResultMessageV1 Type echo
- `TestResultPacket.Type` 필드 (`string`, 기본값 `""`) — 미설정/v2.6 경로 안전.
- `BuildResultMessageV1`: site 토큰 직후 `;Type;` 삽입 → `RESULT:site;Type;P|F|B;count;id=val=judge,...`.
- Type 빈값이면 `;;` 자리 보존 (자재/판정 인덱스 어긋남 방지).
- `MapCycleJudgement` / `BuildFaiItemsV1` 본문 무변경.
- Convert 의 v2.6 Test 블록(`testPacket.Site == (int)ESequence.Bottom` 하드코딩 분기 포함) 무변경 — 회귀 0.
- 커밋: `ccb088e`

### Task 2: $ALIGN_RESULT / $ALIGN_CALIB 응답 타입 + 빌더 골격
- `EVisionResponseType.AlignResult` / `AlignCalib` enum 멤버 (Unknown=999 앞).
- `CMD_SEND_ALIGN_RESULT = "ALIGN_RESULT"`, `CMD_SEND_ALIGN_CALIB = "ALIGN_CALIB"` 송신 상수.
- `AlignResultItem`(ItemName/Value/IsPass), `AlignResultPacket`(AlignTarget/IsPass/Items), `AlignCalibPacket`(AlignTarget/IsPass) 클래스.
- `AsAlignResult` / `AsAlignCalib` 다운캐스트 헬퍼.
- `Convert(VisionResponsePacket)` switch 에 2 case 추가 — 기존 6 case(GrabStatus/Light/RecipeChange/RecipeGet/SiteStatus/Test) 무손상.
- `BuildAlignResultMessage`(target;P|F;items), `BuildAlignItems`(i>0 분리자, Name=val=OK|NG), `BuildAlignCalibMessage`(target;P|F) 빌더.
- 커밋: `8905191`

## Behavior Verification (정적)

- Type="SIDE_3", Site=2, IsBuffer=true, count=0 → `RESULT:2;SIDE_3;B;0;`.
- Type="" → `RESULT:2;;B;0;` (자리 보존).
- AlignResultPacket(AlignTarget="TRAY", IsPass=true, Items=[OffsetX,OffsetY]) → `ALIGN_RESULT:TRAY;P;OffsetX=..=OK,OffsetY=..=OK`.
- AlignCalibPacket(AlignTarget="TRAY", IsPass=true) → `ALIGN_CALIB:TRAY;P`.

## Deviations from Plan

None - plan executed exactly as written.

추가 정합: BuildResultMessageV1 의 doc 주석에 Phase 63 Type echo 1줄 보강 (포맷 명세 갱신, 동작 무변경).

## Threat Mitigations Applied

- T-63-05: BuildResultMessageV1 가 Type 빈값이어도 `szMsg += testPacket.Type` 뒤 `MSG_RESULT_HEADER_SEP` 무조건 삽입 → `;;` 자리 보존, count/판정 인덱스 어긋남 차단.
- T-63-07: Type echo / Align 빌더가 V1·Align 경로 한정 — Convert v2.6 Test 블록 무변경으로 v2.6 RESULT byte-identical.

## Notes

- 컴파일 검증은 Plan 05(빌드)에서 통합 수행 (이 plan 은 코드 변경만).
- Align Items 채움(실 OffsetX/Y/Theta 값)은 Phase 62 Align 결과 모델 확정 시 후속 — 현재는 빌더/타입 골격.

## Self-Check: PASSED
- WPF_Example/TcpServer/VisionResponsePacket.cs: FOUND
- Commit ccb088e: FOUND
- Commit 8905191: FOUND
