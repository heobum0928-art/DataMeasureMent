---
phase: 75-align-corrected-image-evidence
plan: 03
status: complete
date: 2026-08-27
---

# 75-03 SUMMARY — ① 을 Align 경로에 배선 + NG 이미지 저장

## 삽입 위치 (전부 순수 삽입 — 삭제 0줄)

| 파일 | 위치 | 내용 |
|---|---|---|
| `Utility/CaptureImageSaveService.cs` | `CaptureImageSaveRequest` 프로퍼티 끝 | `DirectoryOverride` 1개 |
| 〃 | `SaveRequest()` 의 `baseDirectory` 계산 **다음 줄** | `bHasDirOverride` 분기 2줄 |
| `Custom/SystemHandler.cs` | 클래스 상단 | 상수 3개 |
| 〃 | `RunBottomAlign` 조기 return A/B | `RecordAlignFailureOnly(...)` 2곳 |
| 〃 | `RunBottomAlign` `AlignResult res = null;` 다음 | `bool bAlignPass = false;` |
| 〃 | `RunBottomAlign` `return true;` 직전 | `bAlignPass = true;` |
| 〃 | `RunBottomAlign` `finally` **맨 앞** | `RecordAlignVerify(...)` |
| 〃 | `RunTrayAlign` 동일 5곳 | 〃 (`EBottomAlignSlot.None`) |
| 〃 | `FillAlignPoseZero` 아래 | 신규 private 메서드 3개 |

**`git diff` 삭제 줄: 두 파일 모두 `0`** = 기존 판정·응답 경로 무변경.

## 이미지 저장 파일명 형식 (75-05 UI 가 이 파일을 찾아 연다)

```
{prefix}_ALIGN_{TARGET}_{slotToken}_NG_{HHmmssfff}.jpg
```

실제 예시:
```
aligncorr_ALIGN_BOTTOM_3D_Top_NG_143052871.jpg      ← 보정 이미지가 있을 때
alignraw_ALIGN_TRAY_NG_143052871.jpg                ← 보정 불가(검출 실패) 시 원본
```

- `MaterialNo >= 0` 이면 `_M{번호}` 가 `ALIGN_{TARGET}` 뒤에 삽입된다 (`BuildFileName` 오버로드 규약)
- 확장자는 `.jpg` 고정 (`ResolveExtension` 은 prefix가 `origin` 일 때만 BMP 를 허용)
- 저장 폴더: `{ResultSavePath}\AlignVerify\{yyMMdd}\` (`AlignVerifyRetention.BuildAlignImageDirectory`)

## 🔴 큐 혼잡 시 이미지 생략 정책 (75-06 문서화 대상)

```csharp
private const int ALIGN_IMAGE_MAX_QUEUE_DEPTH = 25;   // 기존 큐 상한 50 의 절반
```

`saver.QueueDepth >= 25` 면 **증거 이미지를 포기하고 즉시 반환**한다. 수치 기록(CSV)은 그대로 남는다.

이유: `CaptureImageSaveService.Enqueue` 는 내부에 백프레셔(`WaitForQueueSpace`)가 있어 큐가
꽉 차면 **호출 스레드를 최대 30초까지 세운다.** 여기서는 그 호출 스레드가 **TCP 응답 스레드**라
PLC 사이클이 통째로 지연된다. Align 증거 이미지는 "있으면 좋은" 보조 자료이므로 포기하는 쪽이 맞다.

생략 시 로그: `[ALIGN_VERIFY] 저장 큐 혼잡(depth={n}) — 증거 이미지 생략, 수치 기록은 유지`

## 메모리 사고 이력 대응 (협상 대상 아님)

| 제약 | 이행 |
|---|---|
| 새 저장 메커니즘 만들지 않기 | 기존 `CaptureImageSaveService` 그대로 사용 ✅ |
| 큐 상한 + 백프레셔 무변경 | `MAX_QUEUE_DEPTH=50` / `WORKER_COUNT=6` / `WaitForQueueSpace();` 각 1건 유지 ✅ |
| `SharedHImage` refcount | `AddRef()` 1 ↔ `Release()` 1 (`finally` 보장) ✅ |
| NG 만 저장 | 정상 건은 `EnqueueAlignEvidenceImage` **호출조차 안 함** → `CopyImage` 0회 ✅ |
| `corrected` Dispose | `RecordAlignVerify` 의 `finally` 단일 지점 ✅ |

## 검증 결과

| 빌드 | 에러 | 경고 | 코드 종류 |
|---|---|---|---|
| SIMUL-ON (`Debug\|x64`) | **0** | **18줄** | `CS0162`/`CS0618` 2종 ✅ |
| SIMUL-OFF (`-p:DefineConstants=TRACE%3BDEBUG`) | **0** | **16줄** | `CS0618` 1종 ✅ |

| acceptance | 기대 | 실측 |
|---|---|---|
| `SystemHandler.cs` 삭제 줄 | **0** | **0** ✅ |
| `CaptureImageSaveService.cs` 삭제 줄 | 0 | **0** ✅ |
| `RecordAlignVerify(` | 3 | **3** ✅ |
| `RecordAlignFailureOnly(` | 5 | **5** ✅ |
| `RunCorrectedRecheck` | 1 | **1** ✅ |
| 재사용판 호출(`res.HasDetection, res.DetectedRow1`) | 1 | **1** ✅ |
| `AlignVerifyCsvWriter.Append` | 2 | **2** ✅ |
| `shared.AddRef();` / `shared.Release();` | 1 / 1 | **1 / 1** ✅ |
| `ALIGN_IMAGE_MAX_QUEUE_DEPTH` | 2 | **2** ✅ |
| `corrected.Dispose();` | 1 | **1** ✅ |
| 추가 줄 `throw` **문** | 0 | **0** ✅ (아래) |
| 추가 줄 `?:`/`??`/`?.` | 0 | **0** ✅ |
| 추가 줄 판정 심볼 접촉 | 0 | **0** ✅ |
| `CopyImage()` **호출** | 1 | **1** ✅ (아래) |
| Capture 큐 상수 3종 무변경 | 각 1 | **각 1** ✅ |

## Deviations

**[Rule 3 - 검증 기준 함정] grep 카운트 2건이 주석을 셌다 (코드 수정 없음)**

1. `CopyImage()` = **2** — 실제 호출 1건(`919줄`) + **주석 1건**(`916줄` "src.CopyImage() 를 쓰는 이유: …").
2. 추가 줄 `throw ` = **1** — 전부 **주석**(`// 실패해도 throw 하지 않는다(TCP 스레드 크래시 방지)`).
   주석 제외 시 `0`.

두 경우 모두 **코드를 고치지 않았다.** 두 주석은 "왜 복사하는가 / 왜 throw 하지 않는가" 를 설명하는
가치 있는 문서이고, 숫자를 맞추려 지우는 것은 인수인계가 명시적으로 경고한 사고 패턴이다.
Phase 73 인수인계의 **"grep 카운트는 주석 포함 여부 검토"** 가 75-02 에 이어 다시 적중했다.

## 커밋

`Utility/CaptureImageSaveService.cs` + `Custom/SystemHandler.cs` 2개 파일. csproj 무변경(신규 파일 없음).

## Self-Check: PASSED

플랜 `<verification>` 6항목 전부 통과:
1. `Custom/SystemHandler.cs` 삭제 0줄 ✅
2. `CaptureImageSaveService.cs` 삭제 0줄 + 큐/워커/백프레셔 상수 무변경 ✅
3. `shared.AddRef()` 1 ↔ `shared.Release()` 1 ✅
4. `CopyImage()` 호출 1건(NG 경로 한정) ✅
5. 추가 줄에 `throw` 문 0건, `?:`/`??`/`?.` 0건 ✅
6. SIMUL-ON / SIMUL-OFF 빌드 에러 0, 새 경고 코드 0건 ✅
