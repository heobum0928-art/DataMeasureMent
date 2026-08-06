# Quick Task 260806-dsn: 일괄검사 메모리 폭증(30GB+, 사이클 종료 후에도 미감소) 근본원인 수정 - Context

**Gathered:** 2026-08-06 (원래 OverlayCaptureRenderer 버퍼윈도우 재사용 건으로 시작했으나, 재조사 결과 진짜 근본원인이 확정되어 CONTEXT 전면 개정)

**Status:** Ready for planning (원인 확정 — 실제 크래시 재현 + 앱 자체 로그 + Windows 이벤트로그 + 격리 재검증 + HALCON 공식 문서 5중 교차검증 완료, 재조사 불필요. 사용자 결정 완료.)

<domain>
## Task Boundary

**이전 가설 폐기(중요, plan/실행 시 재시도 금지)**: `OverlayCaptureRenderer.RenderToHImage`의 매 호출 `open_window`/`close_window`가 원인이라는 가설은 **사용자가 해당 호출을 완전히 주석 처리하고 재빌드해서 재현한 결과, 동일하게 메모리가 폭증하여 반증됨.** 이 파일/함수는 이번 수정 범위에서 완전히 제외한다(단, 별도로 알려진 `#6001 open_window` 에러 로그 자체는 "이미 메모리가 부족한 상태에서 마침 이 연산이 실패로 표면화된 결과"였을 뿐 원인이 아니었음).

**확정된 근본원인 (오케스트레이터가 실기 화면자동화로 직접 재현 + 실시간 메모리 관찰 + 멀티에이전트 조사로 확정, 2계층 구조)**:

### 계층 1 — 앱 자체가 사이클이 끝나도 이미지를 계속 들고 있음 (~8-16GB, 사용자 관찰 "메모리가 줄지 않는다"의 직접 원인)
실측(격리 폴링, 30개 항목 Bottom 배치 1사이클 관찰): 1GB → 2.7GB → 8.3GB(약 30초 정체) → 9.9 → 11.3 → 11.9 → 12.4GB로 **계단식 증가, 사이클이 끝나고 앱이 Idle 상태여도 절대 감소하지 않음.**

확정된 3가지 보존 지점(전부 file:line 확인 완료, HIGH confidence):
1. **`ShotConfig._image`**(`ShotConfig.cs:377-383`, `SetImage`) — 체크된 Shot마다 `image.CopyImage()`를 저장. 해제 시점은 **그 Shot이 다음번에 다시 실행되어 `EStep.Init`(`ClearAllResults`, `Action_FAIMeasurement.cs:69`)에 도달할 때뿐**이며 사이클 종료 시점에는 전혀 해제되지 않는다. 30개 Shot × 실측 이미지 크기(아래 참고) ≈ 수 GB.
2. **`ActionContext.ResultHalconImage`**(`Action_FAIMeasurement.cs:264`, cross-Z 변형 `:458`) — `image.CopyImage()`를 화면표시용으로 저장. 해제는 **같은 액션이 다음에 다시 실행될 때**뿐(`SequenceBase.cs:348`의 `StartCore→Context.Clear()`는 **다음 사이클 시작 시점**에 호출되지, 현재 사이클 종료 시점이 아님). `SequenceContext.CopyFrom`(`SequenceContext.cs:169-172`)이 한 벌 더 복제해 보관.
3. **크로스-Z 이미지 저장소**(`InspectionSequence.cs`, `StoreCrossZImage`/`TakeCrossZImageCopy`) — `TakeCrossZImageCopy`가 **원본을 저장소에서 제거하지 않고 사본만 반환**한다(`InspectionSequence.cs:812` 부근). 저장소는 오직 **다음 실행 시작**(`BeginCrossZImageCycle`)에서만 비워진다.

**실제 이미지 크기 확정(사용자 제공 실측)**: `D:\Data\Image\OfflineInspect\FAI_1\*.bmp` 실측 결과 **파일 1개 = 127MB**(전체 32개 = 3.6GB) — 애초에 mzf 작업 때 가정했던 "12MP≈12MB"보다 **약 10배 큰 실제 이미지 크기**였음. 이 크기 차이가 8-16GB 규모를 설명하는 핵심 근거.

### 계층 2 — HALCON 자체가 해제된 메모리를 OS에 즉시 반환하지 않음 (공식 문서로 확정, 계층 1과 무관하게 별도로 존재하는 문제)
이 PC에 설치된 HALCON 24.11 공식 문서(`C:\Program Files\MVTec\HALCON-24.11-Progress-Steady\doc\html\manuals\memory_management\`)를 직접 확인:
- Windows에서 22.11부터 기본 할당자가 **mimalloc**으로 바뀌었고, 이는 "Win32 기본 힙 할당자보다 메모리를 더 공격적으로 캐싱하는 경향"이 있음(공식 문서 문구).
- HALCON은 3중 캐시(image cache/global memory cache/temporary memory cache)를 갖는데, 그중 **`temporary_mem_cache`**(기본값 `exclusive`, 스레드별 슈퍼블록을 "현재 이미지 크기 기준 휴리스틱"으로 잡아둠)가 "다른 캐시보다 메모리 소비에 훨씬 큰 영향을 준다"고 공식 문서에 명시. 이 앱처럼 스레드가 많고(시퀀스 스레드+HALCON 자동병렬화 워커) 이미지가 127MB급으로 큰 경우 이 캐시가 수 GB를 스레드별로 유지할 수 있음.
- 공식 FAQ: "HALCON 객체를 지웠는데 왜 메모리가 안 줄어드나요? → 십중팔구 메모리 캐싱 때문입니다."
- **공식 해법(문서 Chapter 4 "Handling Suspected Memory Leaks in HALCON", 앱 시작 시 1회)**:
  ```
  set_system('global_mem_cache','idle');
  set_system('temporary_mem_cache','idle');
  set_system('image_cache_capacity',0);
  ```

</domain>

<decisions>
## Implementation Decisions (LOCKED)

### Part A — HALCON 메모리 캐시 설정 적용 (LOCKED, 위험도 최저, 공식 권장)
앱 시작 시점에 아래 3줄을 **가장 먼저**(다른 시퀀스/디바이스 초기화 이전) 실행한다:
```csharp
HOperatorSet.SetSystem("global_mem_cache", "idle");
HOperatorSet.SetSystem("temporary_mem_cache", "idle");
HOperatorSet.SetSystem("image_cache_capacity", 0);
```
위치: `WPF_Example\SystemHandler.cs`의 `Initialize()` 진입부(가장 이른 시점) — 플래너가 실제 코드를 보고 정확한 삽입 지점을 정한다. 이 3줄은 **런타임 성능에 미미한 영향**(공식 문서 기준 캐시를 끄는 것이지 기능을 제거하는 게 아님)만 있고 정확성에는 전혀 영향 없다 — 별도 UAT 불필요, 빌드+실행만 확인.

### Part B — Shot/Action 이미지 보존을 "사이클 종료 시 정리"로 전환 (LOCKED, 사용자 확정: "사이클 끝나면 그냥 비우기")
사용자가 3가지 옵션(즉시 비우기 / LRU N개 유지 / 이번엔 손대지 않기) 중 **"사이클 종료 시 현재 화면에 표시 중인 노드 1개만 남기고 나머지는 즉시 Dispose"**를 선택함.

**핵심 발견(구현 난이도를 크게 낮춤)**: `MainView.xaml.cs`의 `DisplayContextToViewer`(line 1645-1680)에 **디스크 재로드 폴백이 이미 구현되어 있다** — `context.ResultHalconImage`가 null이면 자동으로 `context.ResultImagePath`(저장된 파일 경로)에서 `halconViewer.LoadImage(path)`로 다시 로드한다(line 1665-1676). **즉 새 재로드 로직을 만들 필요가 없다** — `ResultHalconImage`를 사이클 종료 시 null로 정리하기만 하면 기존 폴백이 자동으로 작동한다. 단, `context.ResultImagePath`가 정리 대상 모든 Shot에 대해 실제로 유효한 파일 경로를 갖고 있는지(빈 문자열/오래된 경로가 아닌지)는 **plan-checker가 반드시 확인**해야 한다 — 이게 비어있으면 정리 후 해당 Shot 클릭 시 아무것도 안 보이는 회귀가 생긴다.

구현 대상(플래너가 정확한 삽입 지점/헬퍼 설계는 재량, 아래는 요구사항):
1. **일괄검사 1사이클이 완전히 끝나는 시점**(`BatchRunService.HandleFinish`가 최종 `OnBatchComplete`를 발화하는 시점, 또는 그와 동등한 시퀀스 레벨의 "사이클 종료" 훅)에서, **현재 UI에 표시 중인 노드에 해당하는 Shot/Action을 제외한 나머지 전부**에 대해:
   - `ShotConfig._image`를 Dispose하고 null 처리(단, `SetImage`/`GetImage`의 기존 lock 규약을 그대로 따를 것 — `ShotConfig.cs`에 이미 있는 `_imageLock` 사용).
   - 해당 Shot의 각 Action의 `ActionContext.ResultHalconImage`를 Dispose하고 null 처리.
2. **크로스-Z 이미지 저장소**도 같은 시점에 전부 Clear(Dispose 포함) — `BeginCrossZImageCycle`과 동일한 정리 로직을 재사용하거나 그에 준하는 새 메서드를 추가해도 됨(플래너 재량).
3. **"현재 표시 중인 노드"를 정확히 식별하는 방법**은 플래너가 기존 UI 상태(예: `InspectionListView.SelectedParam`, 또는 `MainView`가 마지막으로 렌더링한 노드를 추적하는 기존 필드)를 활용해 정한다 — 이 판별이 틀리면 "지금 보고 있는 화면이 갑자기 사라지는" 사용자 체감 회귀가 생기므로 plan-checker가 반드시 검증.
4. **단일 RUN(`Btn_start_Click`)이나 배치가 아닌 일반 검사 흐름에는 영향 없어야 한다** — 이번 정리는 오직 "사이클이 완전히 끝난 시점의 사후 정리"이며, 검사 진행 중(사이클 도중) 어떤 이미지도 조기에 Dispose되어서는 안 된다(다음 shot 처리 중 참조 오류 방지).

### 범위 밖 (건드리지 않음)
- `OverlayCaptureRenderer.cs`(`RenderToHImage`) — 위에서 반증됨, 무수정.
- `PatternMatchService.cs` — 어제(260805-ojq) 이미 수정+검증 완료, 오늘 사용자의 실험적 편집(각도 0,0/SubPixel false/TupleRad)은 전부 되돌려 git HEAD 상태로 복원됨(커밋 없음, clean). 무수정.
- `CaptureImageSaveService.cs`의 `MAX_QUEUE_DEPTH=50` 값 — 실제 이미지가 127MB라 순간 최대 6.5GB까지 물릴 수 있음이 확인됐으나 이건 **사이클 종료 후 드레인되는 transient 피크**이지 "안 줄어드는" 증상의 원인이 아니므로 이번 범위 밖(필요시 별도 quick task로 상한값 재조정 검토 가능, Claude's Discretion 언급만 하고 이번엔 무수정).
- `DualImage` 측정의 티칭 이미지 로드/해제 — 조사 결과 모든 경로(성공/실패/예외)에서 이미 올바르게 Dispose됨이 확인됨(누적 없음). 무수정.

</decisions>

<specifics>
## Specific Ideas

- Part A 대상 파일: `WPF_Example\SystemHandler.cs` (Initialize 메서드).
- Part B 대상 파일(플래너가 정확한 범위 확정): `WPF_Example\Custom\Sequence\Inspection\ShotConfig.cs`(정리 헬퍼 추가), `WPF_Example\Custom\Sequence\Inspection\Action_FAIMeasurement.cs` 또는 `InspectionSequence.cs`(사이클 종료 훅 + 크로스-Z 저장소 정리), `WPF_Example\Custom\Sequence\Inspection\BatchRunService.cs`(정리 트리거 지점 — `OnBatchComplete` 발화 전후) 또는 동등한 훅.
- 참고: `WPF_Example\UI\ContentItem\MainView.xaml.cs:1645-1680` (`DisplayContextToViewer`) — 이미 구현된 디스크 재로드 폴백, 무수정(이 폴백을 "이용"만 함).
- 검증: 사용자가 실제로 재현했던 시나리오(Bottom, 30개 항목 체크, 일괄검사) 그대로 재검증 필요 — `checkpoint:human-verify` 필수. (a) 배치 1사이클 후 메모리가 대폭 낮아지는지(수 GB대가 아니라 수백 MB대로), (b) 표시 중이던 마지막 노드의 이미지는 여전히 정상 표시되는지, (c) 다른 Shot/FAI 노드를 클릭했을 때 디스크에서 재로드되어 정상적으로 이미지+오버레이가 보이는지(끊기거나 빈 화면이 아닌지), (d) 재생대상 Shot을 다시 검사(재실행)했을 때 정상 동작(회귀 없는지).
- 앱 재시작 없이 계속 실행 중인 프로세스(오늘 세션에서 화면자동화로 실행했던 인스턴스)는 이번 plan 실행 전 정리됨 — 실행자는 최신 커밋 기준 재빌드 후 검증.

</specifics>

<canonical_refs>
## Canonical References

- `C:\Program Files\MVTec\HALCON-24.11-Progress-Steady\doc\html\manuals\memory_management\memory_management_0003.html` ~ `0009.html` — mimalloc 할당자, 3중 캐시, 공식 권장 `set_system` 3줄.
- 오케스트레이터 실기 재현 데이터: PowerShell `Get-Process` 폴링 실측 메모리 곡선(1→2.7→8.3→9.9→11.3→11.9→12.4GB, 계단식·미감소), Windows Application 이벤트로그(halcon.DLL 0xc0000005 크래시 이력 2026-08-05~06 최소 10회), 앱 자체 Error 로그(`D:\Data\Error\2026-08-06_Error.log` `#6001 open_window` — 원인 아닌 결과로 재분류됨).
- 사용자 실측: `D:\Data\Image\OfflineInspect\FAI_1\*.bmp` 파일 크기 127MB(1장), 32장/3.6GB.

</canonical_refs>
