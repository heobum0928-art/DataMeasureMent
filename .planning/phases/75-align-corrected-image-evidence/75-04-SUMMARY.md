---
phase: 75-align-corrected-image-evidence
plan: 04
status: complete
date: 2026-08-27
---

# 75-04 SUMMARY — ② 안착 위치 기록 (기록 전용, 판정 무변경)

## 삽입 위치 (전부 순수 삽입 — 삭제 0줄)

| 파일 | 줄(편집 후) | 내용 |
|---|---|---|
| `DatumConfig.cs` | `:946~953` | `_lastFindTimeUtc` 필드 + `LastFindTimeUtc` **get 전용** 프로퍼티 |
| `DatumConfig.cs` | `LastFindSucceeded` setter 첫 줄 | `if (value) { _lastFindTimeUtc = System.DateTime.UtcNow; }` |
| `DatumConfig.cs` | `_copyExclude` | `"LastFindTimeUtc",` |
| `InspectionSequence.cs` | `DatumConfigs` 선언 아래 | `_dtCycleStartUtc` 필드 |
| `InspectionSequence.cs` | `HandleRunStartResetResults` `try {` 다음 | `_dtCycleStartUtc = DateTime.UtcNow;` |
| `InspectionSequence.cs` | `AddResponse` `SaveAsync` 다음 | `RecordSeatingEvidence(nIndexNumber);` |
| `InspectionSequence.cs` | `HandleManualCyclePersist` `SaveAsync` 다음 | `RecordSeatingEvidence(NO_MATERIAL);` |
| `InspectionSequence.cs` | `PersistAndEnqueueV1` `SaveAsync` 다음 | `bLastIndexOfCycle` 게이트 + 호출 |
| `InspectionSequence.cs` | `HandleManualCyclePersist` 아래 | `RecordSeatingEvidence` / `ResolveDatumPixelResolutionMm` |

**`git diff` 삭제 줄: 두 파일 모두 `0`** = 기존 P/F 판정·TCP 응답·cycle.json 무변경.

## 🔴 stale 게이트 — "왜 어떤 Datum 은 기록에 안 나오나" 의 답 (75-06 문서화 대상)

`DetectedOriginRow/Col` 과 `LastFindSucceeded` 는 **사이클 단위로 초기화되지 않는다.**
어떤 Datum 이 지난 사이클에 성공하고 이번 사이클에는 아예 안 돌았다면, 그 Datum 은 여전히
`LastFindSucceeded == true` + 지난 사이클 좌표를 들고 있다. 그대로 기록하면 **돌지도 않은 Datum 의
옛 좌표가 이번 자재번호로 찍힌다** — "안착이 튀었다" 는 잘못된 증거가 만들어진다.

기록되려면 **세 조건을 모두** 통과해야 한다:

1. `d.LastFindSucceeded == true`
2. `d.LastFindTimeUtc >= _dtCycleStartUtc` ← **stale 게이트**
3. `d.DetectedOriginRow != 0.0 || d.DetectedOriginCol != 0.0`

`_dtCycleStartUtc` 는 `HandleRunStartResetResults`(OnStart 단일 지점)에서 찍는다.

### ⚠ 스탬프는 idempotent 가드보다 **위**에 있다 (실측 확인: 8줄 < 9줄)

`LastFindSucceeded` setter 는 `if (_lastFindSucceeded == value) return;` 가드를 갖고 있다.
스탬프를 가드 **아래**에 두면 연속 사이클에서 `true → true` 가 될 때 가드가 즉시 return 해
스탬프가 갱신되지 않고, 그 Datum 이 **영원히 "묵은 값" 으로 걸러진다.**

## 🔴 `PixelResolutionMmPerPx` 가 0 으로 기록되는 조건 (75-05 UI 가 처리해야 한다)

`ResolveDatumPixelResolutionMm(d)` 는 `Actions` 를 순회하며
`Action_FAIMeasurement.ShotParam.ShotName == d.SourceShotName` 인 SHOT 을 찾는다.

**0 이 되는 경우:**
- `d.SourceShotName` 이 비었거나 어떤 SHOT 과도 매칭되지 않음
- 해당 SHOT 의 `PixelResolution` 이 실제로 0 (미캘리브레이션)
- 순회 중 예외 (전체 try/catch → `0.0` 반환)

**75-05 는 `해상도mmPerPx <= 0` 이면 mm 로 환산하지 말고 `"환산 불가(px 만): {px}px"` 로 표시해야 한다.**
0 을 곱해 `0mm` 로 보여주면 "편차 없음" 으로 오독된다.

## 사이클당 기록 건수

| 경로 | 트리거 | 자재번호 | 기록 시점 |
|---|---|---|---|
| `AddResponse` | v2.6 TCP | `nIndexNumber` | 사이클 1회 |
| `HandleManualCyclePersist` | 화면 수동 RUN | `-1` | 사이클 1회 |
| `PersistAndEnqueueV1` | v1.0 프로토콜 | `nIndexNumber` | **마지막 z_index 에서만** 1회 |

세 경로는 서로 배타적이다(`HandleManualCyclePersist` 는 `RequestPacket != null` 이면 즉시 return).
`PersistAndEnqueueV1` 은 z_index 마다 호출되므로 `bLastIndexOfCycle = !packet.IsBuffer` 로 한 번만 기록한다
(SIDE 는 z 가 최대 16회 돈다).

행 수 = **이번 사이클에 검출된 Datum 개수**.

## 검증 결과

| 빌드 | 에러 | 경고 | 코드 종류 |
|---|---|---|---|
| SIMUL-ON | **0** | **18줄** | `CS0162`/`CS0618` ✅ |
| SIMUL-OFF | **0** | **16줄** | `CS0618` ✅ |

| acceptance | 기대 | 실측 |
|---|---|---|
| `DatumConfig.cs` 삭제 줄 | **0** | **0** ✅ |
| `InspectionSequence.cs` 삭제 줄 | **0** | **0** ✅ |
| `LastFindTimeUtc` 프로퍼티 | 1 | **1** ✅ |
| 스탬프 대입 | 1 | **1** ✅ |
| `"LastFindTimeUtc",` (copyExclude) | 1 | **1** ✅ |
| 스탬프가 가드보다 위 | 참 | **8줄 < 9줄** ✅ |
| `RecordSeatingEvidence(` | 4 | **4** ✅ |
| `_dtCycleStartUtc` | 3 | **3** ✅ |
| `bFreshDetection` | 2 | **2** ✅ |
| `bLastIndexOfCycle` | 2 | **2** ✅ |
| `AlignVerifyCsvWriter.Append` | 1 | **1** ✅ |
| `ResolveDatumPixelResolutionMm` | 2 | **2** ✅ |
| 추가 줄 `CorrectionFactor` **사용** | 0 | **0** ✅ (아래) |
| 추가 줄 `throw` 문 | 0 | **0** ✅ |
| 추가 줄 판정 심볼 | 0 | **0** ✅ |
| 추가 줄 `?:`/`??`/`?.` | 0 | **0** ✅ |

## Deviations

**[Rule 2 - 컴파일 오류] `DatumConfig.cs` 에 `using System;` 이 없다**

- 발견: Task 1 빌드에서 `CS0246: 'DateTime' 형식을 찾을 수 없습니다` 2건.
- 원인: 계획이 `private DateTime _lastFindTimeUtc;` 를 그대로 지시했으나, 이 파일의 using 은
  `System.Collections.Generic` / `HalconDotNet` / `PropertyTools.DataAnnotations` / `ReringProject.Utility`
  뿐이다. 계획이 확인하지 않은 사실.
- 조치: `using System;` 을 추가하는 대신 **`System.DateTime` 완전정규화**로 썼다.
  이 파일이 이미 `System.ComponentModel.Browsable` / `Newtonsoft.Json.JsonIgnore` 를 완전정규화로 쓰고 있어
  기존 관례와 일치하고, using 블록을 건드리지 않아 **순수 삽입 원칙도 유지**된다.

**[Rule 3 - 검증 기준 함정] 추가 줄 `CorrectionFactor` grep 이 주석을 셌다 (코드 수정 없음)**

- `git diff | grep '^+' | grep -c 'CorrectionFactor'` = **2**. 둘 다 **주석**이다 —
  `// CorrectionFactor 는 곱하지 않는다: …` / `// (이 프로젝트에는 CorrectionFactor 이중 적용 사고 이력이 …)`.
  주석 제외 시 **0**. 실제 사용 0건.
- 이 주석은 **왜 적용하지 않는지**를 남기는 것이 목적이고, 이 프로젝트에는 CorrectionFactor
  이중 적용 사고 이력이 실제로 있어 후속 작업자가 반드시 봐야 한다. 지우지 않았다.
- 75-02 / 75-03 에 이어 **세 번째** 주석-포함 grep 함정. Phase 73 인수인계의 경고가 계속 적중한다.

## 커밋

`DatumConfig.cs` + `InspectionSequence.cs` 2개 파일. csproj 무변경(신규 파일 없음).

## Self-Check: PASSED

플랜 `<verification>` 6항목 전부 통과:
1. 두 파일 삭제 0줄 (판정 로직 무변경 증거) ✅
2. 검출 시각 스탬프가 idempotent 가드보다 **위** ✅
3. `bFreshDetection` stale 게이트 존재 ✅
4. v1.0 경로는 `bLastIndexOfCycle` 로 사이클당 1회만 기록 ✅
5. 추가 줄에 `CorrectionFactor` 사용 / `throw` 문 / 판정 심볼 0건 ✅
6. SIMUL-ON / SIMUL-OFF 빌드 에러 0, 새 경고 코드 0건 ✅
