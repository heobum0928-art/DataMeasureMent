# Quick Task 260814-f6i: SIMUL Datum $TEST 통신테스트 이미지 폴백 - Context

**Gathered:** 2026-08-14
**Status:** Ready for planning

<domain>
## Task Boundary

Top 시뮬레이션(SIMUL_MODE) 통신 테스트 중 $TEST(z_index=0, Datum) 요청이 Datum 검출용 이미지 파일 부재로 즉시 F 응답을 내면서 TCP 프로토콜 왕복 테스트 자체가 막히는 문제. 실측 정확도가 아니라 "패킷이 끝까지 도는지" 확인이 목적.

기존 조사로 확인된 사실:
- 단일 이미지 Datum 로드 경로(Action_FAIMeasurement.cs 약 590~643줄, per-datum 1-image 로드)는 이미
  TeachingImagePath → ShotParam.SimulImagePath 폴백이 이미 구현되어 있음(634~635줄).
- DualImage Datum 로드 경로(TryLoadStaticDualDatumImages, 약 700~730줄, VerticalTwoHorizontal 등)는
  이 폴백이 없음 — pathH(TeachingImagePath)/pathV(TeachingImagePath_Vertical)가 비어있거나 파일이
  없으면 곧바로 실패 로그 남기고 리턴(712/716줄), SimulImagePath 폴백 시도 없음.
- $PREP_ACK가 z_index=0(Datum)에 대해 항상 FAIL로 응답하는 것은 설계상 정상 동작(Datum 조명은
  ApplyDatumLights로 별도 적용되며 $PREP/ApplyShotLights 경로는 z>=1 Shot만 대상) — 이번 작업 범위 아님, 건드리지 말 것.

</domain>

<decisions>
## Implementation Decisions

### 적용 범위
- Datum($TEST z_index=0) 경로만 우선 처리한다. Align/Calib(이미지 소스가 EthernetVisionHandler 카메라 Grab 쪽으로 완전히 다른 경로)는 이번 작업 범위에서 제외 — 필요시 별도 quick task로 진행.

### 폴백 이미지
- 더미/빈 이미지를 새로 생성하지 않는다. 이미 레시피에 "등록되어 있는" SimulImagePath(= 해당 Datum을 소유한 Shot의 ShotConfig.SimulImagePath, 즉 InspectionListView UI에서 "검사이미지 Grab"으로 확보해둔 이미지)를 그대로 재사용한다.
- 이는 기존 단일 이미지 Datum 경로(Action_FAIMeasurement.cs 634~635줄)가 이미 쓰고 있는 것과 동일한 폴백 패턴이다. DualImage Datum 경로에도 같은 패턴을 대칭적으로 추가하는 것이 핵심 작업.
- 가로축(pathH/TeachingImagePath)과 세로축(pathV/TeachingImagePath_Vertical) 둘 다 동일한 ShotParam.SimulImagePath 한 장으로 폴백한다(기존 "Simul에서 두 경로 동일 파일 가능" 코멘트와 일치, Action_FAIMeasurement.cs:271 참고).

### Claude's Discretion
- 정확한 대상 datum이 어떤 AlgorithmType(단일/DualImage)을 쓰는지, 현재 Top SIMUL 레시피의 TeachingImagePath/SimulImagePath 값이 실제로 비어있는지는 플래너/실행자가 코드+레시피를 직접 확인해서 판단.
- 이 폴백을 SIMUL_MODE 컴파일 심볼로 가드할지, 기존 단일 이미지 경로처럼 무조건(SIMUL/오프라인 공용)으로 둘지는 기존 단일 이미지 경로의 패턴을 그대로 따른다(문 571줄 주석: "SIMUL / 오프라인 공용") — 별도 신규 게이팅 발명 금지, 기존 컨벤션 재사용.
- 실측 판정(P/F) 로직, $PREP ACK 로직, ShotConfig/DatumConfig 필드 스키마는 변경하지 않는다 — 오직 이미지 로드 실패 시 폴백 순서만 확장한다.

</decisions>

<specifics>
## Specific Ideas

Action_FAIMeasurement.cs의 TryLoadStaticDualDatumImages(가칭, 약 700~730줄)에서 pathH/pathV가 없을 때
바로 실패시키지 말고, ShotParam.SimulImagePath로 폴백 후 그 이미지를 pathH/pathV 양쪽에 재사용하도록
수정. 기존 단일 이미지 경로(627~643줄)의 폴백 코드 구조를 참고해 동일한 스타일로 작성.

</specifics>

<canonical_refs>
## Canonical References

No external specs — requirements fully captured in decisions above.

</canonical_refs>
