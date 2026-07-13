---
phase: quick-260713-nse
plan: 01
subsystem: device-io (조명 제어)
tags: [halcon, wpf, propertygrid, ini-migration, light-controller]

# Dependency graph
requires:
  - phase: quick-260625 (Phase 64 LIGHT-01)
    provides: LightHandler 13채널 물리 등록(RING_CH1~6/BACK/BAR_1~4/RING7/ALIGN_COAX) + ShotConfig 4종 조명 그룹 필드
provides:
  - ShotConfig Ring 6채널 + Bar 4채널 개별 밝기(0~255)/On-Off 스칼라 프로퍼티 20개
  - ShotConfig.Load override — 구 레시피(채널 키 없음) 로드 시 구 통합 필드 값을 채널 전체로 브로드캐스트하는 안전장치
  - ShotConfig.CopyTo override — Shot Copy/Paste 시 조명 28필드 + PixelResolution/CorrectionFactor 복사(기존 버그 수정)
  - LightHandler.TryFindChannel/SetChannelOnOff/SetChannelLevel — 채널명 기반 개별 제어 API
affects: [LIGHT-CHANNEL-DESIGN.md D-L01, 검사 PropertyGrid Light 탭, InspectionSequence.ApplyShotLightsInternal]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ParamBase 하위호환 마이그레이션: Load override 에서 base.Load 이후 IniSection.ContainsKey 로 신규키 부재 판별 → 구 필드 값 브로드캐스트 (CameraSlaveParam.Load CorrectionFactor 선례 재사용, 2번째 적용 사례)"
    - "그룹→채널 개별 전환 시 물리 인덱스 하드코딩 금지, 채널명(string) 기반 조회 헬퍼(TryFindChannel)로 안전 보장"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Device/LightController/LightHandler.cs

key-decisions:
  - "CopyTo 는 신규 20채널 + 구 8필드 + PixelResolution/CorrectionFactor 만 복사 (must_haves truths 범위) — ZPosition/DelayMs/ZIndex/OwnerSequenceName/SimulImagePath 는 계획의 '조명 외 필드는 신중히 판단' 지침에 따라 이번 스코프에서 제외(불필요한 동작 변화 방지)"
  - "Task 4 마이그레이션 검증은 콘솔 하네스 대신 정밀 코드 추적으로 수행 — ShotConfig 생성자가 SystemHandler.Handle 싱글턴(TCP 서버 바인드/디바이스 초기화/백그라운드 스레드)을 필연적으로 트리거하여 격리된 임시 하네스로는 안전하게 검증 불가. 계획의 명시된 대안 경로 사용."

requirements-completed: [LIGHT-CH-01]

# Metrics
duration: ~25min
completed: 2026-07-13
---

# Quick Task 260713-nse: Ring 6채널 + Bar 4채널 개별 조명 제어 Summary

**Ring 6채널 + Bar 4채널을 Shot 단위로 개별 밝기(0~255)/On-Off 제어하도록 ShotConfig 확장, 구 레시피 자동 브로드캐스트 마이그레이션 안전장치 포함**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-07-13T17:10:00+09:00 (추정)
- **Completed:** 2026-07-13T17:20:00+09:00
- **Tasks:** 4 (3 코드 작업 + 1 검증 전용)
- **Files modified:** 3

## Accomplishments
- `ShotConfig` 에 `RingLight_Enabled_1~6`/`RingLight_Brightness_1~6`(Slidable 0~255) + `SideLight_Enabled_1~4`/`SideLight_Brightness_1~4`(Slidable 0~255) 20개 스칼라 프로퍼티 추가 — PropertyGrid Light 탭에 XAML 수정 없이 Ring 6줄 + Bar 4줄 자동 표시
- `ShotConfig.Load` override 신설 — 신규 채널 키 부재(구 레시피) 시에만 구 `RingLight_Enabled/Brightness`, `SideLight_Enabled/Brightness` 값을 각각 6채널/4채널 전체로 브로드캐스트. `CameraSlaveParam.Load`(CorrectionFactor 마이그레이션) 선례를 그대로 복제
- `ShotConfig.CopyTo` override 신설 — Copy/Paste 시 조명 28필드(신규 20 + 구 8) + `PixelResolution`/`CorrectionFactor` 복사(기존엔 override 부재로 전혀 복사되지 않던 버그)
- `LightHandler.TryFindChannel`/`SetChannelOnOff`/`SetChannelLevel` 신설 — 채널명 기반 개별 제어. 인덱스 하드코딩 금지(Bar 를 ch0 부터 쓰면 백라이트 오작동 위험 차단)
- `InspectionSequence.ApplyShotLightsInternal` 의 Ring/Bar 분기를 그룹 API → 채널별 개별 API 로 전환(`ApplyChannelLight` 로컬 헬퍼). BACK/ALIGN_COAX/RING7 분기, `TurnOffShotLights`, `RegisterLightController` 는 완전 무변경

## Task Commits

Each task was committed atomically:

1. **Task 1: ShotConfig 채널별 스칼라 프로퍼티 20개 선언 + 구 필드 UI 숨김** - `0d38a93` (feat)
2. **Task 2: ShotConfig.Load override(마이그레이션) + CopyTo override** - `6ef5806` (fix)
3. **Task 3: LightHandler 채널명 헬퍼 + ApplyShotLightsInternal 채널별 적용 전환** - `89b207f` (feat)
4. **Task 4: 마이그레이션 실증 검증 + 회귀 가드 diff 감사** - 코드 변경 없음(검증 전용), 결과는 아래 "마이그레이션 검증" 섹션

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` - Ring 6채널/Bar 4채널 개별 프로퍼티 20개, Load override(마이그레이션), CopyTo override
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - `ApplyShotLightsInternal` Ring/Bar 채널별 전환, `ApplyChannelLight` 헬퍼 추가
- `WPF_Example/Device/LightController/LightHandler.cs` - `TryFindChannel`/`SetChannelOnOff`/`SetChannelLevel` 추가(기존 public 시그니처 무변경)

## Decisions Made
- **CopyTo 복사 범위 최소화:** must_haves truths("조명 필드 + PixelResolution/CorrectionFactor 복사")에 명시된 범위만 구현. `ZPosition`/`DelayMs`/`ZIndex`/`OwnerSequenceName`/`SimulImagePath` 는 계획이 "조명 외 필드는 기존 동작 변화 최소화를 위해 신중히 판단" 이라 명시했으므로 이번 스코프에서 제외 — 예상 밖의 Shot Copy/Paste 동작 변화(예: ZIndex 복사로 인한 z_index 사이클 오매칭)를 방지.
- **Task 4 검증 방법 = 정밀 코드 추적 (하네스 대신):** 아래 상세.

## Deviations from Plan

None - plan executed exactly as written (Task 1~3). Task 4 는 계획이 명시한 "하네스 실행 불가 시 대안" 조건에 해당하여 코드 추적 경로로 수행(계획에 명시된 정당한 대안 경로이므로 편차 아님).

## Issues Encountered

**Task 4 하네스 실행 불가 판단(사전 차단):** `ShotConfig` 생성자 체인은 `CameraSlaveParam(object owner)` → `SystemHandler.Handle.Devices` / `SystemHandler.Handle.Lights` 접근을 필수로 거친다. `SystemHandler.Handle` 은 정적 싱글턴으로, 최초 접근 시 private 생성자가 즉시 실행되어:
- `DeviceHandler.Handle.Initialize()` (카메라/디바이스 초기화)
- `VisionServer` TCP 서버 바인드(기본 포트 2505)
- `LightHandler`/`RawImageSaveService`/`SequenceHandler` 등 다수의 백그라운드 스레드 기동

를 전부 트리거한다. 임시 콘솔 하네스(scratchpad, `csc.exe` 컴파일 후 `bin/x64/Debug` 에서 실행)로 `new ShotConfig(null)` 을 호출하는 순간 이 전체 앱 부트스트랩이 함께 실행되며, 실행 중인 개발 인스턴스와 TCP 포트 충돌, 백그라운드 스레드로 인한 프로세스 미종료(하네스가 절대 자연 종료되지 않음), light.ini/카메라 SDK 의존 등 검증 목적과 무관한 부작용이 발생할 위험이 크다. 생성자를 우회하는 `FormatterServices.GetUninitializedObject` 방식도 검토했으나, `ParamBase.Load` 가 `PropertyItem[] PropertyArray`(생성자에서만 초기화됨, `CameraSlaveParam(object owner)` 참조)에 대해 `propItems.Length` 를 즉시 역참조하므로 생성자 미실행 시 `NullReferenceException` 이 확정적으로 발생한다. 두 경로 모두 부적합 → 계획이 사전 승인한 대안(정밀 코드 추적)으로 전환.

## 마이그레이션 검증 (정밀 코드 추적 — 하네스 대체)

호출 경로: `ShotConfig.Load(loadFile, groupName)` → `CameraSlaveParam.Load` → `ParamBase.Load` (reflection 직렬화) → `CameraSlaveParam.Load` 복귀(CorrectionFactor 복원) → `ShotConfig.Load` 복귀(신규 브로드캐스트 로직).

핵심 근거 코드:
- `ParamBase.Load` (`Sequence/Param/ParamBase.cs:377-399`): `case "Int32": int iValue = loadFile[group][name].ToInt(); prop.SetValue(this, iValue);` / `case "Boolean": bool bValue = loadFile[group][name].ToBool(); ...` — **모든** public 프로퍼티(신규 20개 포함)를 무조건 순회하며 INI 값으로 덮어쓴다.
- `IniSection.this[string]` (`Utility/Ini.cs:953-960`): 키 부재 시 `IniValue.Default` 반환(딕셔너리에 값 삽입하지 않음, 부작용 없음).
- `IniValue.ToInt()`/`ToBool()` (`Utility/Ini.cs:153-159, 179-185`): `Value == null` → `TryConvertBool/Int` 실패 → `valueIfInvalid` 기본값(각각 `false`/`0`) 반환.
- `IniSection.ContainsKey` (`Utility/Ini.cs:852-854`): 내부 `Dictionary<string, IniValue>.ContainsKey` 직접 위임 — INI 파일에 해당 키가 실제로 쓰여 있었는지를 정확히 판별(값이 0/false 인 경우와 키 자체가 없는 경우를 구분 가능).

**시나리오 1 — 구 레시피 (`[SHOT_0_CAM]` 에 `RingLight_Enabled=True`, `RingLight_Brightness=120`, `SideLight_Enabled=True`, `SideLight_Brightness=80` 만 존재, 채널별 키 없음):**

| 단계 | 상태 |
|---|---|
| `ParamBase.Load` 1차 순회 | `RingLight_Enabled=true`, `RingLight_Brightness=120`, `SideLight_Enabled=true`, `SideLight_Brightness=80` (INI 값대로 로드) |
| `ParamBase.Load` (신규 채널 프로퍼티) | `RingLight_Enabled_1~6=false`, `RingLight_Brightness_1~6=0`, `SideLight_Enabled_1~4=false`, `SideLight_Brightness_1~4=0` (키 부재 → 기본값 클로버) |
| `ShotConfig.Load` 브로드캐스트 (`sec.ContainsKey("RingLight_Brightness_1")==false`) | `RingLight_Enabled_1~6 = true`, `RingLight_Brightness_1~6 = 120` |
| `ShotConfig.Load` 브로드캐스트 (`sec.ContainsKey("SideLight_Brightness_1")==false`) | `SideLight_Enabled_1~4 = true`, `SideLight_Brightness_1~4 = 80` |

**결과: `RingLight_Brightness_1~6 == 120`, `RingLight_Enabled_1~6 == true`, `SideLight_Brightness_1~4 == 80`, `SideLight_Enabled_1~4 == true`** — 조명 전소등 회귀 없음. 계획의 목표 관측값과 정확히 일치.

**시나리오 2 — 신규 레시피 (채널별 키 존재, 예: `RingLight_Brightness_1=10 ~ _6=60`, `RingLight_Brightness=999`(구 키 잔존) 등 혼재):**

| 단계 | 상태 |
|---|---|
| `ParamBase.Load` | 채널별 키가 INI 에 실제 존재 → `RingLight_Brightness_1~6` 각각 10,20,30,40,50,60 로 개별 로드. 구 `RingLight_Brightness=999` 도 별도로 로드되지만 채널 필드와 무관. |
| `ShotConfig.Load` 브로드캐스트 게이트 (`sec.ContainsKey("RingLight_Brightness_1")==true`) | 브로드캐스트 **건너뜀** — 채널값 10~60 그대로 보존, 999 로 덮이지 않음 |
| Bar 4채널도 동일 로직(`SideLight_Brightness_1` 키 존재 시 게이트) | 채널별 값 보존 |

**결과: 채널별 값이 그대로 보존되고 구 통합 값이 브로드캐스트로 덮어쓰지 않음** — 계획의 두 번째 요구사항(신규 레시피 회귀 없음) 충족.

이 추적은 `CameraSlaveParam.Load` 의 `CorrectionFactor` 마이그레이션(`Sequence/Param/CameraSlaveParam.cs:172-182`)이 이미 프로덕션에서 검증된 동일 패턴이라는 점에서 추가 신뢰도를 갖는다(2026-06-19 이후 배포, 회귀 미보고).

## 회귀 가드 diff 감사 (`git diff 6f8b3ce..89b207f`)

| 항목 | 결과 |
|---|---|
| `Custom/Device/LightHandler.cs` (`RegisterLightController`) | **무변경** — diff 출력 0줄 |
| `InspectionSequence.TurnOffShotLights()` | **무변경** — diff 내 해당 함수명 매치 0건 |
| `ApplyShotLightsInternal` BACK/ALIGN_COAX/RING7 분기 | **무변경** — diff 확인 결과 텍스트 100% 동일(위치만 유지, 삭제/추가 라인 없음) |
| `Device/LightController/LightHandler.cs` 기존 public 메서드 시그니처 | **무변경** — 197번째 줄(`SetLevel(string groupName, int level)` 끝) 이후 순수 추가만 존재, 기존 라인 삭제 0건 |
| `ShotConfig` 구 필드 4개 삭제 여부 | **삭제 안 됨** — `RingLight_Enabled`(65행)/`RingLight_Brightness`(67행)/`SideLight_Enabled`(103행)/`SideLight_Brightness`(105행) 전부 잔존, `[Browsable(false)]` 만 추가 |

## 규칙 감사

| 항목 | 결과 |
|---|---|
| 삼항 연산자 `?:` | 0건 (diff 추가 라인 grep 스캔) |
| C# 8+ 문법(`??=`/`using var`/`record`/switch expression) | 0건 |
| 신규 `//YYMMDD hbk` 형식 주석 | 0건(diff 상 나타나는 `//260625`/`//260626` 는 재배치된 **기존** 주석이며 신규 작성 없음. 신규 주석은 전부 날짜/이니셜 없는 평문 한글로 작성) |

## User Setup Required

None - no external service configuration required.

## HUMAN-UAT 대기 항목 (실HW 육안/물리 확인 필요, 코드 검증만으로 완결 불가)

1. **현장 구 레시피 첫 로드 시 육안 확인** — 실제 운용 중인 구 레시피(채널 키 없음)를 로드했을 때 검사 PropertyGrid 의 `RingLight_Brightness_1~6`/`SideLight_Brightness_1~4` 가 구 값으로 채워지는지 확인. 위 코드 추적으로 논리는 검증됐으나 실제 파일 I/O·PropertyGrid 렌더링 경로의 육안 확인은 미수행.
2. **임시 배선(링6 + 백라이트1) 컨트롤러에서 채널별 개별 점등/소등 물리 확인** — `TryFindChannel`/`SetChannelOnOff`/`SetChannelLevel` 이 실제 JPF 컨트롤러 시리얼 프로토콜을 거쳐 의도한 물리 채널만 점등/소등하는지, 인접 채널(특히 Bar↔Back 경계) 간섭이 없는지 확인.
3. **저장 후 재로드 시 채널별 값 유지 확인** — PropertyGrid 에서 채널별 값을 편집→저장→재로드했을 때 값이 정확히 보존되는지(신규 키가 채널별로 정확히 기록/판독되는지) 확인.

## Next Phase Readiness

- `LIGHT-CHANNEL-DESIGN.md` 의 D-L01 "Ring 6분할 독립 제어 여부 — 미결" 이 이번 작업으로 **해소**됨(Ring 6채널 + Bar 4채널 개별 밝기/On-Off 완전 지원).
- 코드 레벨 구현/마이그레이션/회귀 가드는 전부 완료 및 build PASS. 실HW UAT 3건만 잔존(위 목록).
- Blocker 없음 — 다음 작업(있다면) 은 위 HUMAN-UAT 결과 확인 후 정상 진행 가능.

---
*Phase: quick-260713-nse*
*Completed: 2026-07-13*

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
- FOUND: WPF_Example/Device/LightController/LightHandler.cs
- FOUND: .planning/quick/260713-nse-on-off-ring-6-bar-4-shotconfig-ui-applys/260713-nse-SUMMARY.md
- FOUND commit: 0d38a93 (Task 1)
- FOUND commit: 6ef5806 (Task 2)
- FOUND commit: 89b207f (Task 3)
