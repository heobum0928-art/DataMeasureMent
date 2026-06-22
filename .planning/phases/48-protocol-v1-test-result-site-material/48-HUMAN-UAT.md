---
status: partial
phase: 48-protocol-v1-test-result-site-material
source: [48-VERIFICATION.md]
started: 2026-06-22T08:29:43Z
updated: 2026-06-22T08:29:43Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. TEST 패킷 E2E 파싱 — 자재번호(IndexNumber) 수신·전파
실제 디팜스테크 핸들러(또는 `Test/mock_vision_client.py`)로 `$TEST:1,42,null,1@` 패킷 송신 후 수신된 `TestPacket.IndexNumber=42` 확인. 미수신 케이스(`$TEST:1,null,null,1@` 또는 필드 부족)는 `-1` 폴백 확인.
expected: IndexNumber=42 (미수신 시 -1), 캡쳐 파일명에 `_M42` 포함(-1이면 생략), xlsx 자재번호 행(행 4)에 42 기록(-1이면 '-')
result: [pending]

### 2. PC1 모드 Site 라우팅
`UseProtocolV1=true`, `PcRole=1`(PC1) 설정 후 Site=1 `$TEST` 수신 → TOP 시퀀스 동작 / Site=2 → BOTTOM 시퀀스 동작 확인.
expected: PC1 Site1=TOP 자원, Site2=BOTTOM 자원으로 라우팅
result: [pending]

### 3. PC2 모드 Site 라우팅
`UseProtocolV1=true`, `PcRole=2`(PC2) 설정 후 Site=1, Site=2 각각 `$TEST` 수신 → 양쪽 모두 SIDE 시퀀스 동작 확인.
expected: PC2 Site1/Site2 모두 SIDE 자원으로 라우팅 (동일 물리 SIDE 공유)
result: [pending]

### 4. RESULT byte 포맷 일치
`$RESULT` 출력을 TCP 수신 측(mock 클라이언트)에서 캡쳐하여 byte 형식 확인: `RESULT:1;P;2;A1=12.345=OK,C1=5.000=OK`. Datum 샷(z_index=0) → `RESULT:1;B;0;`.
expected: `.planning/refs/Vision-Protocol-v1.0.md` 케이스 1~3과 byte 일치 (첫 구분자 `;`, 항목 구분 `,`, 항목내부 `=`)
result: [pending]

### 5. v2.6 경로 회귀 0
`UseProtocolV1=false`(기본값)으로 v2.6 검사 사이클 1회 실행 — 기존 동작 회귀 없음 확인.
expected: 기존 RESULT 포맷, 기존 포트(2505), 기존 인코딩(Default), 기존 Site 매핑(1=Top/2=Side/3=Bottom) 모두 변동 없음
result: [pending]

## Summary

total: 5
passed: 0
issues: 0
pending: 5
skipped: 0
blocked: 0

## Gaps
