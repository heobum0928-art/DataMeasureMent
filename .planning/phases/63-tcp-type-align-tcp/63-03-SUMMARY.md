---
phase: 63-tcp-type-align-tcp
plan: 03
subsystem: TcpServer
tags: [protocol-v3, type-routing, resource-map, proto-type]
requires:
  - "63-01 (TestPacket.Type 필드)"
provides:
  - "TryResolveSlotByType: Type 토큰 → ESite 슬롯 매핑 헬퍼"
  - "SetIdentifier V1 분기 Type 우선 라우팅 (Site 폴백 보존)"
affects:
  - "WPF_Example/Custom/TcpServer/ResourceMap.cs"
tech-stack:
  added: []
  patterns:
    - "bool bTypeResolved = TryResolveSlotByType(...); if (!bTypeResolved) { 폴백 } 패턴"
    - "out ESite 매개변수 + bool 반환 TryXxx 패턴"
key-files:
  created: []
  modified:
    - "WPF_Example/Custom/TcpServer/ResourceMap.cs"
decisions:
  - "TOP→ESite.Top, BOTTOM→ESite.Side(PC1 Side슬롯=BOTTOM 자원), SIDE_*→ESite.Top(PC2 양 슬롯 동일 SIDE)"
  - "Type 미인식/빈값 → false → ResolveSiteSlot(Site) 기존 폴백 보존"
  - "v2.6 else 분기 + ResolveSiteSlot/Map* 본문 무변경 — T-63-10 회귀 0"
metrics:
  duration: 7
  completed: "2026-06-24"
---

# Phase 63 Plan 03: Type 토큰 → ESite 슬롯 라우팅 Summary

TestPacket.Type 토큰(TOP/BOTTOM/SIDE_1~4)을 ESite 슬롯으로 변환하는 헬퍼를 추가하고, SetIdentifier V1 분기가 Type 우선으로 슬롯을 결정하도록 교체했다. Type 부재/미인식 시 기존 ResolveSiteSlot(Site) 폴백을 보존한다.

## What Was Built

### Task 1: Type 토큰 상수 + TryResolveSlotByType 헬퍼 (커밋 35a573e)
- `TYPE_TOKEN_TOP = "TOP"`, `TYPE_TOKEN_BOTTOM = "BOTTOM"`, `TYPE_TOKEN_SIDE_PREFIX = "SIDE_"` 상수 추가 (매직 스트링 제거).
- `TryResolveSlotByType(string szType, out ESite eSlot)` 헬퍼: 빈값/미인식→false/ESite.Top, TOP→true/Top, BOTTOM→true/Side, SIDE_*→true/Top.
- `ResolveSiteSlot` / `MapPc1Resources` / `MapPc2Resources` 본문 무변경.

### Task 2: SetIdentifier V1 분기 Type 우선 라우팅 (커밋 2d541a1)
- V1 분기에서 `ESite eSlot = ResolveSiteSlot(...)` 단일 호출을 `TryResolveSlotByType → 폴백 ResolveSiteSlot` 2단계로 교체.
- `bIsCalibration` 가드(return false; 보존) 무변경.
- else(v2.6) 분기: `Find(EResource.Sequence, (ESite)testPacket.Site)` 보존 — Type 참조 없음 (T-63-10 회귀 0).

## Behavior Verification (정적)

- Type="BOTTOM" + UseV1=true → TryResolveSlotByType=true/Side → Identifier=SEQ_BOTTOM(PC1 Side슬롯=BOTTOM).
- Type="" + UseV1=true → TryResolveSlotByType=false → ResolveSiteSlot(Site) 폴백.
- Type="SIDE_3" + UseV1=true → TryResolveSlotByType=true/Top → Identifier=SEQ_SIDE(PC2 Top슬롯=SIDE).
- UseV1=false → else 분기, Type 참조 없음 — v2.6 동작 불변.

## Deviations from Plan

Task 1 코드(TYPE_TOKEN_* 상수 + TryResolveSlotByType)가 63-02 실행 중 이미 ResourceMap.cs에 존재함을 확인. 63-03 Plan 01 커밋(35a573e)이 선행되어 있어 Task 1을 중복 작성 없이 Task 2만 실행. 기능·명세 완전 일치 — 계획 대비 편차 없음.

## Threat Mitigations Applied

- T-63-08: TryResolveSlotByType 미인식 → false → ResolveSiteSlot(Site) 안전 폴백. 등록된 ESite.Top/Side 슬롯만 산출 → KeyNotFoundException 회피.
- T-63-09: 인식 토큰만 슬롯 매핑, 그 외 기존 Site 폴백 — 미정의 동작 없음.
- T-63-10: else(v2.6) 분기 + ResolveSiteSlot/MapPc1/MapPc2 본문 무변경 — v2.6 회귀 0.

## Known Stubs

없음. 컴파일 검증은 Plan 05(빌드)에서 통합 수행.

## Self-Check: PASSED
- WPF_Example/Custom/TcpServer/ResourceMap.cs: FOUND
- Commit 35a573e (Task 1): FOUND
- Commit 2d541a1 (Task 2): FOUND
