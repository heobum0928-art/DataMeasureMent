---
phase: 63-tcp-type-align-tcp
plan: 01
subsystem: TcpServer
tags: [protocol-v3, tcp-recv, align, proto-type, av-09]
requires: []
provides:
  - "TEST_FIELD_TYPE 상수 + TestPacket.Type 필드 (V1 TEST Type 라우팅)"
  - "AlignTestPacket / AlignCalibPacket 수신 패킷 + 파서 (AV-09 수신측)"
affects:
  - "VisionRequestPacket.Convert(string) — Type 파싱 + ALIGN 2 case"
tech-stack:
  added: []
  patterns:
    - "bHas* bool 변수화 + sentinel 폴백 방어 파싱 (Phase 48 패턴 계승)"
    - "V1/V26 분기 격리 — 인덱스 시프트는 V1 파서 한정 (회귀 0)"
key-files:
  created: []
  modified:
    - "WPF_Example/TcpServer/VisionRequestPacket.cs"
decisions:
  - "TEST_FIELD_TYPE=1 삽입 → 자재번호[1]→[2]/z_index[3]→[4] V1 한정 시프트, V26 파서 dataList[0/1/2] 하드코딩 무변경"
  - "ALIGN_TEST/ALIGN_CALIB 페이로드 = target 토큰 단일 필드로 가정 (Phase 62 Align 결과 모델 미확정 → 라우팅용 Target 만 추출)"
metrics:
  duration: 8
  completed: "2026-06-24"
---

# Phase 63 Plan 01: TCP 수신 Type 필드 + Align 수신 커맨드 통합 Summary

디팜스테크 Vision Protocol v3.0 수신측 — UseProtocolV1=true 한정으로 $TEST 에 Type 필드를 추가(자재번호/z_index 인덱스 +1 시프트)하고, $ALIGN_TEST/$ALIGN_CALIB 커맨드를 기존 switch 파싱 프레임워크에 통합했다.

## What Was Built

### Task 1: V1 파서 Type 필드 + 인덱스 시프트
- 신규 상수 `TEST_FIELD_TYPE = 1`, `TEST_MIN_FIELD_TYPE = 2`.
- V1 한정 시프트: `TEST_FIELD_MATERIAL` 1→2, `TEST_FIELD_ZINDEX` 3→4, `TEST_MIN_FIELD_MATERIAL` 2→3, `TEST_MIN_FIELD_ZINDEX` 4→5.
- `TestPacket.Type` 필드 (`""` 기본값) — INI/미수신 안전.
- `ParseTypeField` 방어 헬퍼: 누락/`null`/빈값 → `""`. `TryParseTestFieldsV1` 가 site 파싱 직후 호출.
- `TryParseTestFieldsV26` 본문(`dataList[0/1/2]` 하드코딩) 무변경 — v2.6 회귀 0.
- 커밋: `eb0eaa5`

### Task 2: $ALIGN_TEST / $ALIGN_CALIB 통합
- `VisionRequestType.AlignTest`/`AlignCalib` enum 멤버 (Unknown=999 앞).
- `CMD_RECV_ALIGN_TEST = "ALIGN_TEST"`, `CMD_RECV_ALIGN_CALIB = "ALIGN_CALIB"` 수신 상수.
- `Convert(string)` switch 에 2 case 추가 — 기존 4 case(TEST/LIGHT/RECIPE/SITE_STATUS) 무손상.
- `TryParseAlignTestFields`/`TryParseAlignCalibFields` 방어 파서: `dataList` null 또는 길이<1 → `false` 반환(Convert 가 null 패킷 반환, 예외 없음).
- `AsAlignTest`/`AsAlignCalib` 헬퍼 + `AlignTestPacket`/`AlignCalibPacket` 클래스(각 `AlignTarget` 프로퍼티).
- 커밋: `0a000ff`

## Behavior Verification (정적)

- `$TEST:1,SIDE_3,12345,null,2@` (V1) 파싱 → Site=1, Type="SIDE_3", IndexNumber=12345, TestID="2".
- `$ALIGN_TEST:TRAY@` → AlignTestPacket(AlignTarget="TRAY"). 필드 0개 → null 반환.
- V26 파서 `dataList[0/1/2]` 하드코딩 인덱스 보존 (회귀 0).

## Deviations from Plan

None - plan executed exactly as written.

## Threat Mitigations Applied

- T-63-01: `ParseTypeField` 가 `dataList.Length >= TEST_MIN_FIELD_TYPE` 가드 + bool 변수화 후 분기 → 인덱스 범위 밖 접근 차단, 미수신 시 "" 폴백.
- T-63-02: ALIGN 파서가 `dataList != null && Length >= 1` 가드 후 false 반환 → null 패킷, 예외 throw 없음.
- T-63-03: V26 파서 무변경으로 v2.6 site/type/testID 위치 보존.

## Notes

- 컴파일 검증은 Plan 05(빌드)에서 통합 수행 (이 plan 은 코드 변경만).

## Self-Check: PASSED
- WPF_Example/TcpServer/VisionRequestPacket.cs: FOUND
- Commit eb0eaa5: FOUND
- Commit 0a000ff: FOUND
