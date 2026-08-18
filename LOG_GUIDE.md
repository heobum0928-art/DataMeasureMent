# 로그 → 코드 찾아가기 가이드

화면(Trace 탭)에 뜨는 `[SEQ]`/`[ALGO]` 로그를 보고, 문제가 생긴 부분의 코드를 직접 찾아가기 위한 가이드입니다.

## 찾는 방법 (공통)

1. 로그 줄에서 **대괄호 안 이름**을 확인합니다. 예: `[DatumPhase]`, `[Grab]`, `[MoveZ]`, `[Measure]`
2. Visual Studio 또는 VS Code에서 **전체 검색**(Ctrl+Shift+F)을 열고, 아래 표의 "검색어" 칸을 그대로 검색합니다.
3. 검색 결과 중 `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` 파일이 대부분입니다 — 그 파일 하나로 좁혀서 찾아도 됩니다.

## 태그 → 검색어 / 파일

| 로그에 보이는 형태 | 검색어 | 파일 |
|---|---|---|
| `[SEQ] ── {시퀀스} 루틴 시작 ──` | `LogRoutineBegin` | `WPF_Example/Sequence/Sequence/SequenceBase.cs` (`StartCore` 메서드) |
| `[SEQ] ── {시퀀스} 루틴 종료 ──` | `LogRoutineEnd` | 같은 파일 (`Finish()` / `Error()` 메서드) |
| `[SEQ] {시퀀스} · {Shot} 시작` | `LogActionBegin` | 같은 파일 (`ExecuteAction` 메서드) |
| `[SEQ] {시퀀스} · {Shot} 완료` | `LogActionEnd` | 같은 파일 (`ExecuteAction` 메서드) |
| `[SEQ] ... · [MoveZ] ...` | `case EStep.MoveZ` | `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` |
| `[SEQ] ... · [DatumPhase] ...` | `case EStep.DatumPhase` | 같은 파일 (기준점 검출) |
| `[SEQ] ... · [Grab] ...` | `case EStep.Grab` | 같은 파일 (촬영) |
| `[SEQ] ... · [Measure] ...` | `case EStep.Measure` | 같은 파일 (측정) |
| `[ALGO] {Shot} · ... type=...` | `[ALGO]` | 같은 파일, Measure 단계 안(측정 1건 실행 직후) |

**규칙**: `[단계명]`은 코드의 `EStep`이라는 목록에 있는 이름과 항상 똑같습니다. 대괄호 안 글자를 그대로 `case EStep.그이름`으로 검색하면 거의 항상 한 번에 찾아집니다.

## Algorithm 탭 (알고리즘 세부 계측)

Trace 탭이 너무 복잡해지지 않도록, 측정/검출 알고리즘 내부의 세부 수치(strip 처리, 에지 피팅 잔차, 렌더링 시간 등)는 별도 **Algorithm 탭**에 모아둡니다. 아래는 그 태그들입니다.

| 태그 | 파일 |
|---|---|
| `[Datum.*]` (strip-loop, tact, trim) | `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` |
| `[FitLine]` | `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` |
| `[CaptureRender]` | `WPF_Example/Halcon/Display/OverlayCaptureRenderer.cs` |
| `[CaptureSave]` | `WPF_Example/Utility/CaptureImageSaveService.cs` |
| `[ALIGN_SVC]` | `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs` |
| `[FaiTiming]` | `Action_FAIMeasurement.cs` (임시 성능 조사용, 추후 정리 예정) |

## 예시

로그에 이 줄이 있다면:
```
[SEQ]   TOP · SHOT_A1-23-C1-C12 · [DatumPhase] 완료 — 검출성공 1 / 실패 0 / 캐시재사용 0 (0.22초)
```

→ `Action_FAIMeasurement.cs`에서 `case EStep.DatumPhase`를 검색하면 그 코드로 바로 이동합니다.
