---
phase: 48-protocol-v1-test-result-site-material
plan: 03
subsystem: protocol
tags: [protocol-v1, result-serialization, hungarian, proto-02]
dependency_graph:
  requires:
    - SystemSetting.UseProtocolV1 (48-01 foundation)
  provides:
    - VisionResponsePacket.BuildResultMessageV1 (v1.0 RESULT 직렬화)
    - VisionResponsePacket.MapCycleJudgement (P/F/B cycle 판정 문자 매핑)
    - VisionResponsePacket.MapFaiJudgement (FAI 항목 OK|NG 판정 문자)
    - VisionResponsePacket.BuildFaiItemsV1 (id=val=judge 항목 직렬화)
    - MSG_RESULT_HEADER_SEP(;) / MSG_RESULT_ITEM_SEP(,) / MSG_RESULT_INNER_SEP(=) 상수
    - TEST_RESULT_BUFFER("B") 상수
    - TestResultPacket.IsBuffer (Phase 49 판정 엔진이 채울 자리)
  affects:
    - VisionResponsePacket.Convert(packet) EVisionResponseType.Test case
tech_stack:
  added: []
  patterns:
    - D-00 헝가리언 접두사 + if/else only + 명명 상수 + 30줄 한도 (제어 코딩 지침)
    - UseProtocolV1 분기 패턴 (v1.0/v2.6 공존 — D-06)
    - BuildXxx + MapXxx 단일책임 분리 패턴 (30줄 한도 충족)
key_files:
  created: []
  modified:
    - WPF_Example/TcpServer/VisionResponsePacket.cs
decisions:
  - "v1.0 분기는 EVisionResponseType.Test case 최상단에서 break — 기존 IsDynamicFAI/Bottom/기본 블록 byte-identical 보존(회귀 0)"
  - "IsBuffer=true가 Result=OK/NG보다 최우선으로 B를 반환 — 진행 중 상태가 오판정 방지"
  - "FAICount=0(Datum 샷)시 BuildFaiItemsV1 빈 문자열 반환 → 마지막 ; 뒤 항목 없음 = 규격 RESULT:site;B;0; 일치"
  - "using ReringProject.Setting 추가 — SystemSetting.Handle.UseProtocolV1 접근 필요"
  - "v1.0 RESULT에 InspectionType 필드 없음 — v2.6(,site,InspectionType,...) 과 차이"
metrics:
  duration: 168
  completed_date: "2026-06-22"
---

# Phase 48 Plan 03: PROTO-02 RESULT 직렬화 Summary

**One-liner:** v1.0 `$RESULT:site;P|F|B;count;id=val=OK|NG,...@` 3단 구분자 직렬화 — BuildResultMessageV1 + MapCycleJudgement(IsBuffer 우선) + BuildFaiItemsV1 + 4 명명 상수 + IsBuffer 자리 신설

## Build Result

msbuild Debug/x64 PASS — 0 errors, 0 new warnings. 기존 베이스라인 경고(CS0618 x5, CS0162 x1, MSB3884 x1) 유지.

## What Was Built

### Task 1: RESULT v1.0 직렬화 — 전체 구현 (commit ffe85d6)

**WPF_Example/TcpServer/VisionResponsePacket.cs** — 90줄 추가, 2줄 변경:

#### (a) 3단 구분자 + Buffer 상수 (lines ~60-63)

```csharp
public const char MSG_RESULT_HEADER_SEP = ';';   // 헤더 구분자 (site/판정/count 사이)
public const char MSG_RESULT_ITEM_SEP   = ',';   // 항목 간 구분자
public const char MSG_RESULT_INNER_SEP  = '=';   // 항목 내부 구분자 (id=val=judge)
public const string TEST_RESULT_BUFFER  = "B";   // cycle 진행 중(Buffer) 판정
```

#### (b) TestResultPacket.IsBuffer 속성

```csharp
public bool IsBuffer { get; set; } = false;
// Phase 49 판정 엔진이 채울 자리. 현재 기본 false → 항상 P/F.
```

#### (c) MapCycleJudgement — 15줄

IsBuffer 최우선 → B, Result==OK → P, 그 외(NG/NotExist/ANG/TECHING) → F.

#### (d) MapFaiJudgement — 9줄

FAIResultData.Result==OK → "OK", 그 외 → "NG".

#### (e) BuildFaiItemsV1 — 19줄

항목 루프: `id=val=judge` + 항목 간 `,`. FAICount=0이면 빈 문자열 반환.

#### (f) BuildResultMessageV1 — 14줄

`RESULT:{site};{P|F|B};{count};{items}` 조립. MSG_RESULT_HEADER_SEP 3회 사용.

#### (g) EVisionResponseType.Test case 분기

```csharp
bool bUseV1 = SystemSetting.Handle.UseProtocolV1;
if (bUseV1)
{
    msg += BuildResultMessageV1(testPacket);
    break;
}
// ↓ 이하 기존 v2.6 블록 그대로 (회귀 0)
```

## byte 검증 결과 (코드 경로 추적, 4 케이스)

| 케이스 | 입력 | 예상 출력 | 일치 |
|--------|------|-----------|------|
| 케이스1 (정상, 전체 OK) | site=1, IsBuffer=false, Result=OK, FAI[{A1,OK,12.345},{C1,OK,5.000}] | `RESULT:1;P;2;A1=12.345=OK,C1=5.000=OK` | PASS |
| 케이스2 (NG 포함) | site=1, IsBuffer=false, Result=NG, FAI[{A1,OK,12.345},{C1,NG,9.999}] | `RESULT:1;F;2;A1=12.345=OK,C1=9.999=NG` | PASS |
| 케이스3 (Buffer, FAI 있음) | site=2, IsBuffer=true, FAI[{A1,OK,1.000}] | `RESULT:2;B;1;A1=1.000=OK` | PASS |
| 케이스4 (Datum 샷, count=0) | site=1, IsBuffer=true, FAI 빈 리스트 | `RESULT:1;B;0;` | PASS |

Vision-Protocol-v1.0.md 케이스 1~3 포맷과 byte 일치.

## D-00 준수 확인

| 규칙 | 상태 |
|------|------|
| 헝가리언 접두사 (b/n/sz) | PASS — bUseV1/bIsBuffer/bIsPass/bIsOk/bNeedsSeparator/szMsg/szItems/nCount/faiData |
| if/else if/else only (삼항 ?:/null병합 ?? 0건) | PASS — 신규 4개 메서드 내 0건 |
| 중첩 2단계 이하 | PASS — 최대 1단계 |
| 조건식 bool 변수화 | PASS — bIsBuffer/bIsPass/bIsOk/bNeedsSeparator 모두 변수화 |
| 매직넘버/리터럴 금지 | PASS — ';'/'='/',' 모두 명명 상수 경유 (grep 확인) |
| 함수 30줄 한도 | PASS — 최장 BuildFaiItemsV1 = 19줄 |
| 동사+목적어 함수명 | PASS — BuildResultMessageV1/MapCycleJudgement/MapFaiJudgement/BuildFaiItemsV1 |

## 설계 핵심 노트

1. **IsBuffer가 Phase 49 판정 엔진이 채울 자리임 명시**: 현재 기본값 `false` — 모든 사이클이 P/F만 출력. Phase 49에서 CycleState 엔진이 IsBuffer=true를 설정하면 B가 출력됨.

2. **FAIName 구분자 주입 전제**: FAIName은 내부 레시피(FAIConfig.FAIName)에서 유래하는 영숫자 코드(A1, C1 등). 외부 입력 아님. 현재 `;`/`,`/`=`/`@` 미포함 전제(레시피 작성 규약). 향후 임의 FAIName 허용 시 BuildFaiItemsV1 진입부에 sanitize 추가 필요 (T-48-07 TODO).

3. **v1.0 RESULT에 InspectionType 필드 없음**: v2.6 포맷(`RESULT:site,InspectionType,P,count,...`)은 InspectionType을 2번째 필드로 포함. v1.0(`RESULT:site;P|F|B;count;...`)은 InspectionType 없음 — 규격 차이.

4. **using ReringProject.Setting 추가**: 기존 파일에 없었으므로 SystemSetting.Handle 접근을 위해 추가.

## Deviations from Plan

없음. 계획 그대로 실행됨.

## Known Stubs

- **TestResultPacket.IsBuffer = false (기본값)**: Phase 49 판정 엔진이 채우기 전까지 모든 사이클이 P/F만 출력. B 출력은 Phase 49 범위.

## Threat Flags

T-48-07 mitigation 부분 충족:
- FAIName은 내부 레시피 유래 영숫자 코드 (외부 입력 아님) — 현재 주입 불가.
- 향후 임의 FAIName 허용 시 BuildFaiItemsV1에 sanitize 추가 필요 (TODO 기록).

## Self-Check: PASSED

- WPF_Example/TcpServer/VisionResponsePacket.cs 수정 존재 확인
- BuildResultMessageV1/MapCycleJudgement/MapFaiJudgement/BuildFaiItemsV1 grep 확인
- MSG_RESULT_HEADER_SEP/MSG_RESULT_ITEM_SEP/MSG_RESULT_INNER_SEP/TEST_RESULT_BUFFER/IsBuffer grep 확인
- commit ffe85d6 존재 확인
- msbuild Debug/x64 0 errors, 0 new warnings PASS
