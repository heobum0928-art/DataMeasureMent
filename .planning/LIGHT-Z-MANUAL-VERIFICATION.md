# 조명 HW 채널 + 수동 Z 트리거 검증 절차
> 작성: 2026-07-13 | quick-260713-eza

---

## 1. 배경 및 현재 하드웨어 상태

- 새로 설치된 링(Ring)+백라이트(Backlight) 조명 HW 테스트 필요 (바(Bar) 조명 제외).
- 조명 컨트롤러: JPF-1208 2대. 채널 배치는 현재 코드 기준 7+6(13채널) 유지로 확정(사용자 결정, 2026-07-13).

| 컨트롤러 | 채널 구성 | 채널 수 |
|---------|----------|--------|
| Controller A (Index 0) | RING_CH1~6 + ALIGN_COAX | 7채널 |
| Controller B (Index 1) | BACK + BAR_1~4 + RING7 | 6채널 |

- Z축은 현재 수동 지그(눈금 읽고 아날로그 다이얼)로 조작. POC 장비의 자동 Z축 연동(`IAxisController` 구현, 현재 인터페이스만 존재·구현체 0%)이 붙기 전까지 이 상태 유지.
- 이번 세션(quick-260713-ej3)에서 그 갭을 메우는 **임시** 수동 Z축 트리거 UI를 MainView에 추가함. 관련 코드/커밋:
  - `WPF_Example/Custom/SystemHandler.cs`의 `DebugManualZTrigger(string seqName, int zIndex)` — 내부에서 실제 프로덕션 경로 `ProcessPrep`→`ProcessTest`를 그대로 호출(PREP 실패 시 TEST 미진행).
  - `WPF_Example/UI/ContentItem/MainView.xaml`/`.xaml.cs`의 하단 경고색 패널(시퀀스 콤보 + z_index 입력 + "수동 트리거 실행" 버튼).
  - 커밋 `3b0c5ee`, `9157150`.
  - **삭제 조건**: `IAxisController` 실제 구현 완료 시 이 wrapper 메서드 + UI 패널 전체 삭제(코드에도 동일 주석 있음).

---

## 2. 코드리뷰 결과

gsd-code-reviewer 에이전트 리뷰 결과. 심각도별(높음→중간→낮음)로 그룹핑, 두 그룹(조명 컨트롤러 서브시스템 / 수동 Z 트리거 임시 코드)으로 구분한다.

### 그룹 1 — 조명 컨트롤러 서브시스템

#### [높음]

- **`JPFLightController.Close()` 예외처리 누락**
  위치: `WPF_Example/Device/LightController/JPFLightController.cs:51-61`
  Open/WriteOnOff/WriteLevel은 try-catch 보호되나 Close만 없음. `mPort.WriteLine("#Oa0&")`/`mPort.Close()` 예외 시 `LightHandler.CloseAll()` foreach 중단→나머지 컨트롤러 Close 미실행(포트 누수)+예외가 Release 호출자까지 전파(앱 종료 크래시 가능). 컨벤션(비필수 정리는 try-catch 보호) 위반.

- **물리 배선 교차(cross-wiring) 조기 발견 가드 부재**
  위치: `WPF_Example/Custom/Device/LightHandler.cs:41-73`
  컨트롤러 A/B는 논리적으로 하드코딩, 물리 COM 배정은 `light.ini`의 Controller0/Controller1 Port에만 의존. Open 성공은 "포트가 열렸다"만 의미, 그 뒤 실제 장치가 A인지 B인지 미검증. 유일한 단서는 Open 내 전채널 blink(`JPFLightController.cs:36-40`, 전체 ON→레벨150→OFF)뿐이며 육안·컨트롤러 단위(채널 단위 식별 불가). A/B 물리 교차되어도 정상 기동.

#### [중간]

- **light.ini 부재 시 두 컨트롤러 동일 포트 충돌**
  위치: `WPF_Example/Device/LightController/LightHandler.cs:351-366` (`Load()`)
  파일 없으면 즉시 return false, Port는 VirtualLightController 기본값 3 유지. 신규 설치서 ini 없으면 둘 다 COM3 요구→하나 Open 실패(로그만, OnError 미발화).

- **`LightHandler.OnError` 구독자 전무**
  위치: 전체 grep 결과
  `LightHandler.Handle.OnError +=` 없음(`MainWindow.xaml.cs:96`은 Sequence.OnError만). Execute()에서 FAIL_LIMIT(3회) 초과 시 OnError fire(`LightHandler.cs:394-397,410-413,426-429,445-448`)하나 수신처 없어 UI 알림 없이 로그만. `ELightErrorType.OpenFail`도 정의만·미발화 죽은 값.

- **SerialPort Read/WriteTimeout 미설정 (HW 검증 필요)**
  위치: `JPFLightController.cs:22-49`
  ReadTimeout/WriteTimeout 미설정→기본 무한 대기. 실기 미응답 시 WriteLine 블로킹→`LightHandler.Execute()` 전용 스레드 정지 위험(SIMUL_MODE 미노출 — 실HW 테스트 확인 필요).

- **하드웨어 ON/OFF 실피드백 부재 (아키텍처 갭)**
  위치: `JPFLightController.ReadOnOff/ReadLevel`
  `base`(`VirtualLightController.cs:141-148,171-179`) 그대로 호출·실제 시리얼 읽기 미전송(더미 Sleep+true). `GetOnOff()/GetLevel()`은 "마지막 명령값"일 뿐 실측 아님. `$PREP Op=0` 소등 WriteLevel이 접촉불량으로 미도달해도 예외 없으면 SW는 성공 간주 — 조명 켜진 채 남아도 감지 경로 없음.

#### [낮음]

- **`WriteOnOff`에서 `SetOnOff`로 On 상태 먼저 갱신, 실제 레벨은 다음 사이클 전송**
  위치: `JPFLightController.cs:90`, `LightHandler.cs:431-436`
  전송 실패 시 재시도는 계속되나(영구 무시 아님) 재시도 구간 `GetOnOff()` 보고값과 실제 HW 불일치 가능.

- **`LightHandler.SetOnOff/SetLevel`(`CmdTable[,]`)에 락 없음**
  위치: `LightHandler.cs`
  다중 시퀀스 스레드(+수동 Z트리거 UI 스레드) 동시 갱신 시 과도기 프레임 값 뒤섞임 가능.

#### 참고 (문제 없음)

- LightGroup 5종(RING/BACK/BAR/RING7/ALIGN_COAX)↔ShotConfig 필드 매핑은 `InspectionSequence.ApplyShotLightsInternal`에서 정확히 일치 확인.

### 그룹 2 — 수동 Z 트리거 임시 코드

#### [높음, 코드상 확정]

- **`_lastPrepZIndex` 전역 상태 경쟁(race)**
  위치: `WPF_Example/Custom/SystemHandler.cs:18,205,717,742-772`
  `DebugManualZTrigger`는 UI 스레드에서 직접 ProcessPrep→ProcessTest 순차 호출, 실제 호스트 TCP 처리는 `MainRun()`(SystemProcess 백그라운드, 1ms 폴링). 두 스레드가 락 없이 같은 `volatile int _lastPrepZIndex` R/W — volatile은 가시성만, "저장→주입" 2단계 원자성 미보장. 버튼 클릭과 호스트 `$PREP`/`$TEST`가 시간 겹치면 ProcessTest가 읽는 `packet.TestID`가 의도한 z_index 아닌 다른 스레드 덮어쓴 값일 수 있음 — 잘못된 z_index 검사 실행 or 호스트 테스트 오염.

#### [정보, 데드락 아님]

- **`Start()/StartAll()`은 State==Idle 확인 후 커맨드 플래그만 세팅·즉시 반환(논블로킹)**
  UI 스레드 블로킹/데드락 위험 없음. 단 `MessageBox.Show("트리거 성공"...)`(`MainView.xaml.cs:149-151`)은 "큐잉 성공"만 의미하고 검사/판정 완료 아님에도 완료로 오인 소지 (중간).

#### [낮음]

- **중복 클릭은 State!=Idle 가드로 안전하게 "실패" 처리 (정상)**
  `combo_ManualZSeq`는 Loaded 시 1회만 채워지고 동적 FAI 재구성 시 미갱신 — 삭제된 시퀀스 선택 시 크래시 없이 조용히 실패.

- **`DebugManualZTrigger`가 만드는 `testPacket`에 `Sender` 미설정 (확인 필요)**
  이후 응답이 PopResponse/SendPacket 경로로 흘러도 대상 클라이언트 없어 조용히 버려질 것으로 보임(의도 추정, 명시적 처리 아님).

> ⚠️ **경고**
> 수동 Z 트리거는 실제 호스트($PREP/$TEST TCP)가 동시에 붙어있지 않은 상태(오프라인/단독 POC 테스트)에서만 사용할 것 — `_lastPrepZIndex` 전역 상태를 실제 TCP 경로와 공유하므로, 호스트가 동시에 테스트를 보내는 상황에서 쓰면 잘못된 z_index로 검사가 실행되거나 호스트 테스트가 오염될 수 있음(코드리뷰 확정 사항).

---

## 3. 단계별 검증 절차

### A. 조명 HW 배선 확인 (코드 변경 전, 물리 점검)

- [ ] Controller A/B가 실제 어느 COM 포트에 물렸는지 `light.ini`의 Controller0/Controller1 Port 값과 실물 대조
- [ ] 각 컨트롤러 Open 시 전채널 blink(전체 ON→레벨150→OFF) 육안 관찰, 기대 채널 그룹(A=Ring6+AlignCoax, B=Back+Bar4+Ring7)과 실제 배선 일치 확인 — 섹션 2 [높음] "물리 배선 교차 가드 부재" 리스크와 연결, 이 육안 확인이 유일한 안전장치임을 명시
- [ ] light.ini 존재 여부 확인(없으면 COM 포트 충돌 위험 — 섹션 2 [중간] 항목 연결)

### B. 조명 채널별 개별 테스트 (시퀀스별: Top/Side/Bottom)

- [ ] 각 시퀀스 ShotConfig에 매핑된 조명 그룹(Ring/Back/Coax 등)이 개별 Shot 선택 후 실제 점등 확인
- [ ] 신규 설치 Ring/Backlight만 우선 테스트(Bar 제외 — 사용자 명시)
- [ ] 소등(Op=0) 명령 후 실제 꺼짐 육안 확인 — 섹션 2 "하드웨어 ON/OFF 실피드백 부재" 리스크 연결(SW 성공 보고해도 실제 미소등 가능 강조)

### C. 수동 Z 트리거 사용/검증 절차

- [ ] 선행 조건: 실제 호스트 TCP 미연결 단독 오프라인 상태 확인(경고박스 참조)
- [ ] 레시피 로드 확인(IsRecipeReady) — 미완료 시 트리거 실패 명시
- [ ] 지그를 목표 z_index 물리 Z 높이로 수동 이동(다이얼)
- [ ] MainView 하단 임시 패널에서 대상 시퀀스 선택 + z_index 입력 + "수동 트리거 실행" 클릭
- [ ] 로그(`Logging.PrintLog`, "[임시 수동Z트리거]" 태그 검색) 확인 — PREP/TEST 각각 성공 여부
- [ ] 조명이 해당 z_index Shot 설정대로 실제 점등 육안 확인
- [ ] 메시지박스는 "트리거 큐잉 성공"이지 "검사 완료" 아님 주의(섹션 2 리스크) — 실제 검사 결과는 별도 UI 확인

### D. 회귀 확인

- [ ] 기존 실제 TCP $PREP/$TEST 경로(호스트 연동)가 오늘 변경으로 영향 없음 — git diff 기준 ProcessPrep/ProcessTest/StartAll/Sequences.Start 무변경 (이미 확인 완료로 기록)

---

## 4. 향후 정리 항목 (Carry-over)

- 수동 Z 트리거 UI + `DebugManualZTrigger`: `IAxisController` 자동 Z축 실구현 완료 시 삭제 대상.
- 섹션 2의 [높음]/[중간] 코드리뷰 항목들은 이번 문서 작성 범위에서 **수정하지 않음** — 별도 판단/후속 작업으로 남김(명시).
- 채널 배치는 이번엔 7+6 유지로 확정됐으나, 추후 6+6 변경 검토 시 `LightHandler.cs`/`Custom/Device/LightHandler.cs`/`JPFLightController.cs` 채널 상수·그룹매핑 재검토 필요(이전 대화 논의됨).
