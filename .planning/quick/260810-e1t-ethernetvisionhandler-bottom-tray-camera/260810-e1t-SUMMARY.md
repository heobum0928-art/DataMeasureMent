---
phase: quick-260810-e1t
plan: 01
subsystem: ethernet-vision
tags: [shutdown, camera, resource-cleanup, hik-gige]
requires: []
provides:
  - "EthernetVisionHandler.Release() — 이더넷 정렬 카메라 종료 진입점"
  - "SystemHandler.Release() → EthernetVisionHandler.Handle.Release() 배선"
affects:
  - "앱 종료 경로 (SystemHandler.Release)"
tech-stack:
  added: []
  patterns:
    - "Initialize() 와 동일한 방어적 컨벤션: 내부 전체 try-catch + 호출부 belt-and-suspenders 외부 try-catch"
key-files:
  created: []
  modified:
    - WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs
    - WPF_Example/SystemHandler.cs
decisions:
  - "Camera null 가드는 ?. 대신 명시적 if — EthernetVisionHandler.cs 파일 내부 일관성 우선"
  - "배선 위치는 Devices.Dispose() 직후 — 이더넷 카메라는 Grabber(Devices)와 별도 핸들러라 Devices 정리와 나란히 두는 것이 의도가 드러남"
metrics:
  duration: ~10분
  completed: 2026-08-10
---

# Quick 260810-e1t: EthernetVisionHandler 종료 시 카메라 Close() 배선 Summary

프로그램 종료 시 이더넷 정렬 카메라(Bottom/Tray, Hik GigE) 연결이 끊기지 않던 문제를 `EthernetVisionHandler.Release()` 신설 + `SystemHandler.Release()` 배선으로 해결.

## 무엇이 문제였나

`EthernetAlignCamera.Close()` 메서드 자체는 이미 정상 동작하는 상태로 존재했지만, **코드 전체에서 이 메서드를 호출하는 곳이 단 한 군데도 없었다.** `EthernetVisionHandler`에는 `Initialize()`만 있고 종료 계열 메서드가 아예 없었고, 앱 종료 시 실행되는 유일한 정리 경로인 `SystemHandler.Release()`도 Devices/Sequences/Server/Lights는 전부 정리하면서 `EthernetVisionHandler`는 건드리지 않았다. 즉 "연결을 끊는 코드는 있는데 그걸 부르는 사람이 없는" 상태 → 앱이 꺼져도 카메라 연결이 남아 있었다.

## 무엇을 했나

### Task 1 — `EthernetVisionHandler.Release()` 신설
`WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs`, `Initialize()`와 `ShowConnectFailAlarm()` 사이에 삽입.

- `Camera != null` 명시적 if 가드 — `EthernetVisionMode == None`이면 `Initialize()`가 일찍 return 해서 `Camera`가 끝까지 null로 남으므로 필수.
- 전체를 try-catch로 감싸 **절대 throw 하지 않음.** 앱 종료 경로에서 호출되므로 여기서 예외가 새면 뒤따르는 정리 단계(Sequences.Dispose, Server.Dispose, Lights.Release 등)가 전부 중단된다.
- 성공 시 Camera 로그, 실패 시 Error 로그.

### Task 2 — `SystemHandler.Release()` 배선
`Devices.Dispose();` 직후, 기존 BUF-02 주석 앞에 삽입. `SystemHandler.Initialize()`가 이미 `EthernetVisionHandler.Handle.Initialize()`를 belt-and-suspenders 외부 try-catch로 감싸는 전례(234~243행)를 그대로 따랐다.

호출 순서: `Devices.Dispose()` (272행) → `EthernetVisionHandler.Handle.Release()` (280행) → `UnwireBufferLifecycle()` (287행).

## 검증 결과

**Task 1 자동검증 4/4 통과** — `public void Release()` 1건, `Camera != null` 가드 1건, `Camera.Close()` 호출 1건, `EthernetAlignCamera.cs` 무변경(계획대로 열지도 고치지도 않음).

**Task 2 자동검증 통과**
- [1] `EthernetVisionHandler.Handle.Release()` 호출 1건.
- [2] 호출 순서 정상 (272 → 280 → 287행).
- [3] **Debug/x64 Rebuild PASS** — exit code 0, `error CS` 0건, `error MSB` 0건, `bin\x64\Debug\DatumMeasurement.exe` 정상 산출. 앱이 꺼져 있어 파일 잠금 없이 정식 경로로 빌드됨(스크래치 폴백 불필요). 남은 warning은 전부 기존 CS0618(Phase 33 마이그레이션 관련 Obsolete)로 이번 변경과 무관.
- [4] `DatumMeasurement.csproj` diff hash = `f0dd3a511bd51a3cc6df91c555d4336df60e0c0d` — 계획 기대값과 **정확히 일치.**
- [6] 커밋에 두 파일만 포함(csproj 없음), 삭제된 파일 0건, untracked 0건.

### 검증 항목 [5]에 대한 설명 (기대 해시와 다르나 정상)

계획은 커밋 후 `SystemHandler.cs` 잔여 diff 해시가 `dbf56c603ff7f02cfd00b5e626a3a2cecb83f630`로 유지되기를 기대했으나 실제로는 `8c532ef2...`가 나왔다. **이는 실패가 아니라 계획의 기대값이 구조적으로 성립할 수 없는 값이었기 때문이다.** git diff 출력의 `index <old>..<new>` 헤더 라인은 파일의 커밋된 blob 해시를 담는데, 이 파일에 어떤 hunk든 커밋하는 순간 blob이 바뀌므로 index 라인은 **반드시** 달라진다. 즉 원래 기대 해시는 커밋 이전에 계산된 값이라 커밋 후 보존될 수 없다.

계획이 실제로 증명하려던 불변식은 "무관한 변경이 커밋에 섞이지 않고 그대로 남았는가"이고, 이는 index 라인을 제외하고 비교해 **완전 일치로 직접 확인**했다:

```
NOW    : 41b1f36ebac38a073dd504487e7bcf0dce90e283
BEFORE : 41b1f36ebac38a073dd504487e7bcf0dce90e283
```

잔여 diff 전문도 육안 확인 결과 `memory_allocator` 주석 라인 1개짜리 hunk 하나뿐이며, 커밋 diff에는 `memory_allocator` 문자열이 0건이다.

## 무관한 로컬 변경 2건 — 그대로 보존됨

| 파일 | 내용 | 상태 |
|------|------|------|
| `WPF_Example/DatumMeasurement.csproj` | `DefineConstants`에서 `SIMUL_MODE` 제거 (실HW PC 마커) | 미커밋 유지, diff 해시 불변 확인 |
| `WPF_Example/SystemHandler.cs` (약 128행) | `memory_allocator` 라인 주석 처리 (사용자 실험) | 미커밋 유지, 내용 불변 확인 |

`SystemHandler.cs`는 이번 작업의 편집 대상이기도 해서, 계획의 패치 기반 선택적 스테이징 절차를 그대로 따랐다: 편집 **전에** 기존 diff를 패치로 저장(해시 `dbf56c60...`로 사전 대조 완료) → 편집 → 파일 전체 `git add` → `git apply --cached --reverse`로 인덱스에서만 무관한 hunk 되돌리기(`--cached`라 워킹 디렉터리의 주석 처리는 그대로) → 커밋 전 인덱스 내용 확인(`Release()` 있음 / `memory_allocator` 없음). `DatumMeasurement.csproj`는 열지도 `git add` 하지도 않았다.

## Deviations from Plan

없음 — 계획대로 실행됨. 검증 항목 [5]의 해시 불일치는 위에 설명한 대로 계획 기대값 자체의 구조적 한계이며, 계획이 의도한 불변식은 더 강한 방식(index 라인 제외 전문 비교)으로 충족 확인했다. 코드 변경은 계획의 [목표] 블록과 한 글자도 다르지 않다.

## Known Stubs

없음.

## Threat Flags

없음 — 앱 종료 경로에 기존 카메라 정리 메서드 호출 1개를 추가한 로컬 리소스 정리 배선일 뿐. 네트워크 입력 파싱/파일 I/O/외부 명령 실행/신규 패키지 설치 없음. 계획의 T-e1t-01(종료 경로 예외로 인한 정리 중단)은 이중 try-catch로 mitigate 완료, T-e1t-02(`Close()` 블로킹 가능성)는 기존 코드 특성이라 계획대로 accept.

## 사용자 확인 필요 (실기)

빌드 검증까지가 이 작업의 범위다. 실제 카메라를 연결한 상태에서 **프로그램 종료 → 재실행 시 카메라가 정상 재연결되는지**는 사용자가 실기에서 확인해야 한다. 종료 시 Camera 로그에 `[ETHERNET] camera closed on release` 가 남으면 배선이 실제로 동작한 것이다.

## Commits

- `8338ee2` — fix(quick-260810-e1t): EthernetVisionHandler 종료 시 카메라 Close() 호출 배선 (2 files, +29)

## Self-Check: PASSED

- `WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs` — FOUND, `public void Release()` 포함
- `WPF_Example/SystemHandler.cs` — FOUND, `EthernetVisionHandler.Handle.Release()` 포함
- 커밋 `8338ee2` — FOUND (git log 확인)
- Debug/x64 빌드 산출물 `bin\x64\Debug\DatumMeasurement.exe` — 생성 확인
