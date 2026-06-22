---
phase: 48-protocol-v1-test-result-site-material
plan: 02
subsystem: protocol
tags: [protocol-v1, resource-map, site-mapping, 2pc, tcp-encoding, hungarian]
dependency_graph:
  requires:
    - 48-01 (SystemSetting.UseProtocolV1 / PcRole / ServerPortV1 foundation)
  provides:
    - ResourceMap EPcRole enum (PC1_TopBottom=1 / PC2_Side=2)
    - ResourceMap.InitializeV1 / InitializeV26 분기
    - ResourceMap.MapPc1Resources / MapPc2Resources
    - ResourceMap.ResolveSiteSlot (T-48-04 DoS mitigation)
    - TcpServer.ApplyEncoding protected static 진입점
    - VisionServer v1.0 UTF-8 인코딩 + Port 7701 활성화
  affects:
    - VisionServer TCP 포트 (UseProtocolV1=true 시 7701)
    - 메시지 인코딩 (UseProtocolV1=true 시 UTF-8)
    - ResourceMap Site→Sequence/Camera/Light/Action 매핑 경로
tech_stack:
  added: []
  patterns:
    - ESite 슬롯 재사용 패턴 — framework Dictionary<ESite> 키 호환, 자원은 PcRole 결정
    - bUseV1 분기 패턴 — bool 변수 → if/else (D-00 준수)
    - ApplyEncoding protected static 진입점 — private EncodingType 캡슐화 유지
key_files:
  created: []
  modified:
    - WPF_Example/Custom/TcpServer/ResourceMap.cs
    - WPF_Example/TcpServer/TcpServer.cs
    - WPF_Example/TcpServer/VisionServer.cs
decisions:
  - "ESite 슬롯 재사용 설계: Site1=ESite.Top/Site2=ESite.Side 슬롯으로 매핑. framework ResourceMap 이 Dictionary<ESite, ...> 강결합이라 신규 ESiteV1 enum 을 Add/Find 인자로 넘기면 컴파일 실패 → ESite 슬롯(Top=Site1 키, Side=Site2 키)을 재사용하고 실제 자원(CAMERA_TOP vs CAMERA_BOTTOM vs CAMERA_SIDE)은 PcRole로 결정."
  - "PC2 Site1/Site2 동일 SIDE 자원 공유: 물리 SIDE 카메라는 1종뿐. SIDE_1/SIDE_2 는 검사 위치 구분(PLC 관리)이지 별도 카메라가 아님. MapPc2Resources 에서 Site1/Site2 모두 CAMERA_SIDE/LIGHT_SIDE/SEQ_SIDE 로 매핑."
  - "v1.0 은 항상 ETestType.Inspection action 매핑: v1.0 $TEST 포맷에 TestType 명시 필드 없음(D-01 Plan 01). SetIdentifier v1.0 분기에서 Find(EResource.Action, eSlot, ETestType.Inspection) 명시 전달 — InitializeV1 이 ETestType.Inspection 으로 Add 했으므로 일치."
  - "Port 결정을 TcpServer 생성자에서 분기한 이유: base 생성자 안에서 mListener = new TcpListener(PortNum) + mListener.Start() 가 즉시 호출됨. VisionServer 생성자 본문(base() 이후)에서 포트를 바꾸면 이미 listen 이 시작된 뒤 → Port 결정은 TcpServer 생성자 내 ServerPort 읽는 지점에서 분기해야 함."
metrics:
  duration: 20
  completed_date: "2026-06-22"
---

# Phase 48 Plan 02: 2-PC Site 재정합 + v1.0 전송 계층 Summary

**One-liner:** ResourceMap ESite 슬롯 재사용 2-PC 매핑(PcRole 런타임 결정) + TcpServer ApplyEncoding protected 진입점 + VisionServer UTF-8 Port 7701 분기, v2.6 경로 완전 보존

## Build Result

msbuild Debug/x64 PASS — 0 errors, 0 new warnings. 기존 베이스라인 경고(CS0618 x5, CS0162 x1, MSB3884 x1) 유지.

## What Was Built

### Task 1: ResourceMap 2-PC 재정합 (commit 5933471)

**WPF_Example/Custom/TcpServer/ResourceMap.cs** — 신규/수정:

| 추가 심볼 | 역할 |
|-----------|------|
| `EPcRole` enum (PC1_TopBottom=1, PC2_Side=2) | D-00 매직넘버 회피 — PcRole 정수 비교 상수화 |
| `Initialize()` | bUseV1 분기 → InitializeV1() vs InitializeV26() |
| `InitializeV26()` | 현행 v2.6 12개 Add 호출 byte-identical 추출 (회귀 0) |
| `InitializeV1()` | nPcRole → bIsPc1 분기 → MapPc1Resources / MapPc2Resources |
| `MapPc1Resources()` | PC1: Site1(Top 슬롯)=TOP자원, Site2(Side 슬롯)=BOTTOM자원 |
| `MapPc2Resources()` | PC2: Site1/Site2 모두 SIDE자원 (물리 1종 공유) |
| `ResolveSiteSlot(int nSite)` | Site2→ESite.Side, 범위 밖→ESite.Top 폴백 (T-48-04 DoS mitigation) |
| `SetIdentifier` Test case | bUseV1 분기: v1.0=ResolveSiteSlot+Inspection, v2.6=기존 캐스팅 보존 |

**설계 핵심 — ESite 슬롯 재사용:**
- framework `ResourceMap.cs` 는 `Dictionary<ESite, SiteMap>` / `Dictionary<ESite, TestMap>` 강결합
- `Add(EResource, ESite, string)` / `Find(EResource, ESite)` 시그니처가 `ESite` 강타입
- 신규 `ESiteV1.Site1/Site2` enum을 Add/Find에 넘기면 컴파일 실패 → 기존 ESite 키 재사용
- Site1 = ESite.Top 슬롯 / Site2 = ESite.Side 슬롯 (의미 재해석, 자원은 PcRole로 결정)

**코드 경로 검증:**
| UseProtocolV1 | PcRole | Site 정수 | 매핑 결과 |
|---|---|---|---|
| false | any | 1 | ESite.Top → TOP 자원 (v2.6) |
| false | any | 2 | ESite.Side → SIDE 자원 (v2.6) |
| false | any | 3 | ESite.Bottom → BOTTOM 자원 (v2.6) |
| true | 1(PC1) | 1 | ResolveSiteSlot→ESite.Top → TOP 자원 |
| true | 1(PC1) | 2 | ResolveSiteSlot→ESite.Side → BOTTOM 자원 |
| true | 2(PC2) | 1 | ResolveSiteSlot→ESite.Top → SIDE 자원 |
| true | 2(PC2) | 2 | ResolveSiteSlot→ESite.Side → SIDE 자원 |
| true | any | 0/3/-1 | ResolveSiteSlot→ESite.Top 폴백 → 안전 처리 |

### Task 2: VisionServer Port 7701 + UTF-8 인코딩 (commit 9eb0c7d)

**WPF_Example/TcpServer/TcpServer.cs** — 신규:

| 추가 | 역할 |
|------|------|
| `ApplyEncoding(MessageEncodingType eEncoding)` protected static | private EncodingType setter 파생 클래스 진입점. 캡슐화 유지. |
| TcpServer 생성자 bUseV1 분기 | ServerPortV1(7701) vs ServerPort(2505) — mListener.Start() 이전에 결정 |

**왜 TcpServer 생성자에서 포트를 결정해야 하는가:**
`TcpServer()` 생성자 안에서 `mListener = new TcpListener(PortNum)` + `mListener.Start()` 가 즉시 실행됨.
`VisionServer() : base()` 호출 시 base() 가 먼저 완료되므로, VisionServer 생성자 본문(base 이후)에서 포트를 변경하면 이미 잘못된 포트로 listen 이 시작된 뒤임.
따라서 포트 결정 분기는 TcpServer 생성자의 `ServerPort` 읽는 지점(이미 `using ReringProject.Setting` 있음)에서 수행해야 함.

**WPF_Example/TcpServer/VisionServer.cs** — 수정:

| 변경 | 역할 |
|------|------|
| `using ReringProject.Setting` 추가 | SystemSetting.Handle 접근 |
| VisionServer 생성자 bUseV1 분기 | true 시 ApplyEncoding(Utf8) 호출; false 시 Default 유지 (v2.6 회귀 0) |

**v2.6 회귀 보존 확인:**
- UseProtocolV1=false → PortNum=ServerPort(2505), EncodingType=Default — 기존 동작 그대로

## D-00 준수 확인

| 규칙 | 상태 |
|------|------|
| 헝가리언 접두사 (b/n) | PASS — bUseV1, bIsPc1, bIsSite2, nPcRole, nSite, eEncoding |
| if/else if/else only (삼항 ?:/null병합 ?? 0건) | PASS — git diff 신규 '+' 라인 grep 확인 |
| 중첩 2단계 이하 | PASS — 최대 1단계 (bool변수 → if) |
| 조건식 bool 변수화 | PASS — bUseV1/bIsPc1/bIsSite2/bIsCalibration 모두 명시적 변수 |
| 매직넘버 금지 | PASS — EPcRole enum, ESite enum 상수 사용 |
| 함수 30줄 한도 | PASS — 최장 MapPc1Resources = 8줄, ResolveSiteSlot = 8줄 |
| 동사+목적어 함수명 | PASS — InitializeV1/V26, MapPc1/Pc2Resources, ResolveSiteSlot, ApplyEncoding |

## framework ResourceMap.cs 무수정 확인

`git diff WPF_Example/TcpServer/ResourceMap.cs` — 0 변경. 전체 구현은 Custom/TcpServer/ResourceMap.cs 에 국한.

## Deviations from Plan

없음 — 계획대로 정확히 실행됨.

## Known Stubs

없음. 이 plan은 순수 프로토콜 매핑/전송 설정 코드로 UI 렌더 없음.
PC2 Site1/Site2 SIDE 자원 공유는 의도적 설계 (물리 SIDE 카메라 1종, Index Table 기준).

## Threat Flags

없음 — 신규 네트워크 엔드포인트 추가 없음 (기존 TCP 포트 변경만).
T-48-04/05/06 위협 레지스터 처리 완료:
- T-48-04 (DoS): ResolveSiteSlot 폴백 → 범위 밖 Site 안전 처리
- T-48-05 (Tampering): v2.6 분기 기존 동작 수준 유지 (accept)
- T-48-06 (Info Disclosure): 포트 7701 노출 = 규격 준수 (accept, 로컬 신뢰망)

## Self-Check: PASSED

- WPF_Example/Custom/TcpServer/ResourceMap.cs — EPcRole/InitializeV1/InitializeV26/MapPc1Resources/MapPc2Resources/ResolveSiteSlot 존재 확인
- WPF_Example/TcpServer/TcpServer.cs — ApplyEncoding/ServerPortV1 분기 존재 확인
- WPF_Example/TcpServer/VisionServer.cs — Utf8 ApplyEncoding 호출 확인
- framework WPF_Example/TcpServer/ResourceMap.cs diff 0 확인
- commit 5933471 존재 확인
- commit 9eb0c7d 존재 확인
- msbuild Debug/x64 0 errors, 0 new warnings PASS
