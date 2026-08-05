---
phase: quick-260805-ojq
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Halcon/Algorithms/PatternMatchService.cs
autonomous: false
requirements: [QUICK-260805-ojq]

must_haves:
  truths:
    - "Bottom NCC-engine datum에서 [Test Find]를 반복 클릭해도 프로세스 메모리가 계속 우상향하지 않고 안정적으로 유지된다"
    - "패턴정렬(IsPatternAlignEnabled) datum이 포함된 일괄검사를 연속 수행해도 메모리가 무한 증가하지 않는다"
    - "같은 modelPath로 TryFindPose를 두 번째 호출할 때부터는 디스크에서 모델을 다시 읽지 않는다(캐시 hit — ReadNccModel/ReadShapeModel 미호출)"
    - "Datum을 재티칭(TryCreateModel 성공)하면 같은 modelPath의 이전 캐시가 즉시 무효화되어, 다음 TryFindPose가 새 모델을 사용한다(stale 모델 재사용 회귀 없음)"
    - "Top/Side/Bottom 세 시퀀스 스레드가 동시에 서로 다른(또는 같은) modelPath로 캐시에 접근해도 예외/크래시 없이 동작한다"
  artifacts:
    - path: "WPF_Example/Halcon/Algorithms/PatternMatchService.cs"
      provides: "static 모델 캐시(GetOrLoadModel 조회/lazy-load + InvalidateCache 무효화) + TryFindPose lazy-load 전환 + TryCreateModel 재티칭 무효화 훅"
      contains: "private static HTuple GetOrLoadModel"
  key_links:
    - from: "PatternMatchService.TryFindPose"
      to: "PatternMatchService.GetOrLoadModel"
      via: "modelId = GetOrLoadModel(modelPath, isNcc) — ReadNccModel/ReadShapeModel을 캐시 미스일 때만 호출"
      pattern: "GetOrLoadModel\\(modelPath,"
    - from: "PatternMatchService.TryCreateModel"
      to: "PatternMatchService.InvalidateCache"
      via: "새 모델 write 성공 직후, return true 직전 InvalidateCache(modelPath) 호출"
      pattern: "InvalidateCache\\(modelPath\\);"
---

<objective>
`PatternMatchService.TryFindPose`가 호출될 때마다 NCC/Shape 모델을 디스크에서 통째로 새로 읽고(`ReadNccModel`/`ReadShapeModel`) 1회 매칭 후 즉시 폐기(`ClearNccModel`/`ClearShapeModel`)하던 구조를, `modelPath`를 키로 하는 **static 캐시**로 교체한다. 캐시 hit 이면 재읽기 없이 재사용, miss 면 1회만 로드 후 캐시에 적재(lazy load). 캐시된 모델은 **재티칭(`TryCreateModel` 성공) 시에만** 무효화(Clear)된다.

Purpose: 사용자가 실기 재현(Bottom NCC datum에서 [Test Find] 반복 클릭)으로 직접 확인하고 HALCON 공식문서로 교차검증한 확정 원인 — "매번 통째로 새로 만들고 버리는" 반복이 일괄검사/Test Find 사이클마다 실행되어 프로세스 메모리가 53GB+까지 폭증, 강제종료로 이어졌다. NCC 모델은 회전각 스텝×피라미드 레벨마다 래스터 이미지 전체를 저장하는 무거운 구조라 이 반복의 비용이 특히 크다(Bottom=NCC가 폭증, Top=Shape는 상대적으로 무증상). 이번 수정은 오늘 이미 처리된 별개 원인 2건(quick-260805-mze 배치 동시실행 크래시 100bafe, quick-260805-mzf 캡쳐이미지 큐 백프레셔 44339bc)과 겹치지 않는 세 번째, 별개의 확정 근본원인이다.

Output: `WPF_Example/Halcon/Algorithms/PatternMatchService.cs` 1개 파일 수정 — static 캐시 인프라(필드 2개 + 내부 클래스 1개 + private static 메서드 2개) 신규 추가, `TryFindPose`의 read+clear를 lazy-load로 전환, `TryCreateModel`의 재티칭 성공 경로에 캐시 무효화 훅 추가.

**범위 밖(무변경, CONTEXT.md LOCKED):** `TryFindRefPose`(티칭 1회뿐, 저빈도 — 기존 read+clear 그대로 유지). 앱 종료 시 캐시 전체 정리 훅은 Claude's Discretion 항목으로 이번 plan에서는 추가하지 않는다(런타임 중 무한 증가라는 이번 버그 해결에 필수가 아니고, 프로세스 종료 시 OS가 회수하므로 스코프를 최소로 유지).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/quick/260805-ojq-pattern-model-cache/260805-ojq-CONTEXT.md
@CLAUDE.md
@WPF_Example/Halcon/Algorithms/PatternMatchService.cs

<interfaces>
<!-- 실행자가 코드베이스를 탐색하지 않아도 되도록 필요한 계약을 여기 전부 제공한다. -->

**PatternMatchService 공개 시그니처 (무변경 — 캐싱은 내부 구현 세부사항, 호출부는 무수정)**:
```csharp
public bool TryCreateModel(HImage templateImage, double roiRow, double roiCol, double roiPhi,
    double roiLen1, double roiLen2, string engine, double angleExtentDeg, string modelPath, out string error);

public bool TryFindRefPose(HImage templateImage, string engine, string modelPath, double minScore,
    out double refRow, out double refCol, out double refAngleDeg, out double refScore, out string error);

public bool TryFindPose(HImage runtimeImage, string engine, string modelPath,
    double roiRow, double roiCol, double roiLen1, double roiLen2, double marginPx, double minScore,
    double downsampleFactor,
    out double curRow, out double curCol, out double curAngleDeg, out double curScore, out string error);
```

**전체 호출부 인벤토리 (플래너가 grep으로 확인 완료 — 아래가 전부이며, 전부 위 시그니처의 `bool` 반환값 + `out` 파라미터만 소비한다. `modelId`/`HTuple`을 직접 다루거나 Clear를 호출하는 곳은 단 한 곳도 없다 → 시그니처 불변이므로 아래 3개 파일 전부 무수정으로 캐싱 이득을 자동으로 받는다):**

1. `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — `TryComposeAlign` (line 1906 `new PatternMatchService()`, line 1909/1940 `svc.TryFindPose(...)`). 실제 배치검사 핫패스(패턴정렬 datum마다 사이클당 최대 2회 호출) + Test Find(아래 3번)가 경유하는 곳.
2. `WPF_Example/UI/ContentItem/MainView.xaml.cs`:
   - `RefreshPatternRefPoseAfterTeach` (line 3652 `new PatternMatchService()`, line 3655/3671 `svc.TryFindPose(...)`) — 재티칭(라인핏) 직후 RefMatch 재앵커용, 저빈도.
   - `InvokeCreatePatternModel` (line 3858 `new PatternMatchService()`, line 3860 `svc.TryCreateModel(...)` 패턴1, line 3869 `svc.TryFindPose(...)` 패턴1, line 3882 `svc.TryCreateModel(...)` 패턴2, line 3888 `svc.TryFindPose(...)` 패턴2) — **패턴 모델 재티칭 경로. TryCreateModel 직후 같은 modelPath로 TryFindPose가 바로 이어지므로, 캐시 무효화(TryCreateModel)→즉시 재로드(TryFindPose)가 정확히 새 모델을 읽어야 한다 — 이번 plan의 무효화 로직이 정확해야 하는 이유.**
   - `BtnTestFindDatum_Click` (line 4080) → `GetInspectionSequenceForDatum(datum)` 으로 얻은 시퀀스의 `seq.TryComposeAlign(...)` 을 호출(line 4131, 4158) → 결국 1번의 `TryComposeAlign`을 경유. 이것이 사용자가 반복 클릭해 메모리 폭증을 재현한 "Test Find" 버튼.
3. `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs` — **CONTEXT.md에 명시되지 않았으나 플래너가 grep으로 추가 발견한 별도 호출부(v1.3 Align 비전, Tray/Bottom 이더넷 카메라 서브시스템, Phase 54~65)**. 생성자(line 60)에서 `_matcher = new PatternMatchService()` 1회, `TryTeach`(line 367/377 `TryCreateModel`, line 388/399 `TryFindRefPose`), `Run`(line 469/486 `TryFindPose`, 엔진 고정 `"Shape"`). `Run`은 이 서브시스템의 반복 위치보정 경로 — 같은 근본원인(read+clear 반복)에 노출되어 있었으나 Shape 엔진 고정이라 NCC만큼 무겁지 않았을 뿐. 이번 수정으로 **무수정으로 동일하게 캐싱 이득을 받는다** (static 캐시라 `PatternMatchService` 인스턴스가 여러 개 — 메인 Datum 시스템은 호출마다 `new`, EthernetVision은 생성자에서 1회 — 라도 `modelPath` 키로 프로세스 전체가 공유하므로 무관하다).

**오늘 이미 처리된 별개 quick task (파일 겹침 없음, 재조사 불필요)**: quick-260805-mze(`InspectionListView.xaml.cs`, 커밋 100bafe), quick-260805-mzf(`CaptureImageSaveService.cs`, 커밋 44339bc). 둘 다 `PatternMatchService.cs`를 건드리지 않는다.
</interfaces>

<current_file_state>
`WPF_Example/Halcon/Algorithms/PatternMatchService.cs` (2026-08-05 시점, 총 466줄) 관련 실제 라인:
- 7-8: `using System;` / `using HalconDotNet;` (Dictionary 사용을 위해 `System.Collections.Generic` 추가 필요)
- 37-40: `DEFAULT_NCC_NUM_LEVELS` 상수 선언 직후, `TryCreateModel`의 XML doc 주석 직전 — 캐시 인프라 삽입 위치
- 56-154: `TryCreateModel` — 92-127 구간(engine 분기 + write + `return true;`)에 무효화 훅 삽입
- 170-282: `TryFindRefPose` — **무변경**
- 304-462: `TryFindPose` — 376-405(모델 로드 분기), 441-461(finally의 Clear) 수정
</current_file_state>
</context>

<tasks>

<task type="auto">
  <name>Task 1: PatternMatchService에 static 모델 캐시 도입 — TryFindPose lazy-load 전환 + TryCreateModel 재티칭 무효화</name>

  <files>WPF_Example/Halcon/Algorithms/PatternMatchService.cs</files>

  <action>
`PatternMatchService.cs` **한 파일에만** 4곳을 순서대로 수정한다. 각 위치는 **정확히 이 텍스트**(2026-08-05 라이브 파일 기준)를 찾아 치환한다 — 문자열 매칭으로 대상을 찾고, 라인 번호는 참고용으로만 사용할 것(위 `<current_file_state>` 참고).

**절대 건드리지 말 것 (CONTEXT LOCKED / 범위 밖):**
- `TryFindRefPose`(line 170-282) 본문 — 단 한 글자도 수정하지 않는다. `AlignShapeMatchService.cs`가 이 함수를 그대로 쓰고 있어 함수를 고치면 그쪽이 깨진다(이전 quick-260728-l2r에서 동일 사유로 무수정 확정됨).
- `PatternMatchService`의 4개 공개 메서드 시그니처(`TryCreateModel`/`TryFindRefPose`/`TryFindPose` 파라미터/반환 타입) — 절대 변경 금지. 3개 호출부 파일이 시그니처 불변을 전제로 무수정 상태를 유지한다.
- `TryCreateModel`/`TryFindRefPose` 자체의 로컬 `modelId`(생성/조회용 임시 핸들) Clear 로직 — 이건 캐시와 무관한 각 메서드 자신의 임시 핸들이므로 그대로 둔다.


**(1) using 추가** — 파일 최상단, `using System;` 바로 아래에 `using System.Collections.Generic;` 삽입:

찾을 텍스트(BEFORE):
```csharp
using System;
using HalconDotNet;
```

치환할 텍스트(AFTER):
```csharp
using System;
using System.Collections.Generic;
using HalconDotNet;
```


**(2) 캐시 인프라 신규 삽입** — `DEFAULT_NCC_NUM_LEVELS` 상수 선언 직후, `TryCreateModel`의 XML 문서 주석(`/// <summary>`) 바로 앞에 삽입.

찾을 텍스트(BEFORE):
```csharp
        // NCC 기본 NumLevels
        private const int DEFAULT_NCC_NUM_LEVELS = 4;

        /// <summary>
        /// template ROI(Rect2)로 reduce_domain 한 영역에서 모델 생성 후 engine 별 파일 저장.
```

치환할 텍스트(AFTER):
```csharp
        // NCC 기본 NumLevels
        private const int DEFAULT_NCC_NUM_LEVELS = 4;

        // 캐시 동시성 보호용 락. Top/Side/Bottom 시퀀스가 각자 스레드에서 서로 다른(또는 같은) modelPath 로
        // 동시에 캐시에 접근할 수 있으므로, 딕셔너리 조회/삽입/제거는 전부 이 락 아래에서 수행한다.
        // FindNccModel/FindShapeModel(모델을 조회만 하는 호출) 자체는 이 락 밖에서 실행된다 — 같은 modelId 를
        // 여러 스레드가 동시에 Find 하는 것은 HALCON 문서상 안전한 사용 패턴(모델은 조회 중 read-only)이므로
        // 별도 직렬화는 하지 않는다.
        private static readonly object _cacheLock = new object();

        // modelPath → 로드된 모델 핸들 + Clear 시 어떤 오퍼레이터(NCC/Shape)를 써야 하는지 캐시.
        // static 인 이유: 호출부(TryComposeAlign, BtnTestFindDatum_Click 등)가 매 호출마다
        // new PatternMatchService() 를 새로 만들기 때문에, 인스턴스 필드로는 캐시가 전혀 재사용되지 않는다.
        private static readonly Dictionary<string, CachedModelEntry> _modelCache = new Dictionary<string, CachedModelEntry>();

        // 캐시 1건 = 로드된 modelId + 무효화(재티칭) 시 호출할 Clear 오퍼레이터 식별용 엔진 플래그.
        private sealed class CachedModelEntry
        {
            public HTuple ModelId;
            public bool IsNcc;
        }

        // modelPath 에 해당하는 모델을 캐시에서 재사용(hit)하거나, 없으면 1회만 Read 해서 캐시에 적재한다(lazy load, miss).
        // 반환된 HTuple 의 폐기(Clear) 책임은 더 이상 호출자에게 없다 — 캐시가 소유권을 가지며,
        // TryCreateModel 의 재티칭 무효화(InvalidateCache) 시점에만 Clear 된다.
        private static HTuple GetOrLoadModel(string modelPath, bool isNcc)
        {
            lock (_cacheLock)
            {
                CachedModelEntry entry;
                if (_modelCache.TryGetValue(modelPath, out entry))
                {
                    return entry.ModelId;
                }

                HTuple newModelId;
                if (isNcc)
                {
                    HOperatorSet.ReadNccModel(modelPath, out newModelId);
                }
                else
                {
                    HOperatorSet.ReadShapeModel(modelPath, out newModelId);
                }

                entry = new CachedModelEntry();
                entry.ModelId = newModelId;
                entry.IsNcc = isNcc;
                _modelCache[modelPath] = entry;
                return newModelId;
            }
        }

        // modelPath 로 캐시된 모델이 있으면 Clear 후 캐시에서 제거한다. 재티칭(TryCreateModel 성공) 직후
        // 반드시 호출해야 한다 — 그렇지 않으면 다음 TryFindPose 호출이 재티칭 이전의 stale 모델을 계속
        // 재사용하는 회귀가 발생한다.
        private static void InvalidateCache(string modelPath)
        {
            lock (_cacheLock)
            {
                CachedModelEntry entry;
                if (_modelCache.TryGetValue(modelPath, out entry))
                {
                    try
                    {
                        if (entry.IsNcc)
                        {
                            HOperatorSet.ClearNccModel(entry.ModelId);
                        }
                        else
                        {
                            HOperatorSet.ClearShapeModel(entry.ModelId);
                        }
                    }
                    catch { }
                    _modelCache.Remove(modelPath);
                }
            }
        }

        /// <summary>
        /// template ROI(Rect2)로 reduce_domain 한 영역에서 모델 생성 후 engine 별 파일 저장.
```


**(3) `TryCreateModel` — 재티칭 성공 직후 캐시 무효화** — engine 분기(NCC/Shape) 둘 다 이 지점에서 합류하는 `return true;` 직전에 삽입.

찾을 텍스트(BEFORE):
```csharp
                    // Shape 모델 파일 저장
                    HOperatorSet.WriteShapeModel(modelId, modelPath);
                }

                return true;
            }
```

치환할 텍스트(AFTER):
```csharp
                    // Shape 모델 파일 저장
                    HOperatorSet.WriteShapeModel(modelId, modelPath);
                }

                // 재티칭 성공 — 같은 modelPath 로 캐시된 이전(stale) 모델이 있으면 즉시 무효화한다.
                // 이걸 빠뜨리면 다음 TryFindPose 호출이 재티칭 이전 모델을 계속 재사용하는 회귀가 발생한다.
                InvalidateCache(modelPath);

                return true;
            }
```

(참고: 이 `modelId`는 방금 이 메서드가 생성/write 한 **임시** 핸들이며, 바로 아래 `finally`에서 기존 그대로 Clear 된다 — 캐시와는 무관한 별개 핸들이므로 `finally`는 무수정.)


**(4) `TryFindPose` — NCC 분기: Read를 캐시 조회/lazy-load로 교체**

찾을 텍스트(BEFORE):
```csharp
                if (isNcc)
                {
                    HOperatorSet.ReadNccModel(modelPath, out modelId);

                    HOperatorSet.FindNccModel(
```

치환할 텍스트(AFTER):
```csharp
                if (isNcc)
                {
                    // 캐시 hit 이면 디스크 재읽기 없이 재사용, miss 면 1회 로드 후 캐시 적재(lazy load).
                    // 이 호출 이후 finally 에서 더 이상 Clear 하지 않는다 — 소유권이 캐시로 이전됨.
                    modelId = GetOrLoadModel(modelPath, true);

                    HOperatorSet.FindNccModel(
```


**(5) `TryFindPose` — Shape 분기: Read를 캐시 조회/lazy-load로 교체**

찾을 텍스트(BEFORE):
```csharp
                else
                {
                    HOperatorSet.ReadShapeModel(modelPath, out modelId);

                    //260618 hbk find_shape_model 출력 4개 — acuity 제거(CS1501 fix)
                    HOperatorSet.FindShapeModel(
```

치환할 텍스트(AFTER):
```csharp
                else
                {
                    // 캐시 hit 이면 디스크 재읽기 없이 재사용, miss 면 1회 로드 후 캐시 적재(lazy load).
                    modelId = GetOrLoadModel(modelPath, false);

                    //260618 hbk find_shape_model 출력 4개 — acuity 제거(CS1501 fix)
                    HOperatorSet.FindShapeModel(
```

(`isNcc`/`modelPath`/`engine` 로컬 변수는 기존 그대로 — 신규 변수 도입 없음, 회귀 0.)


**(6) `TryFindPose` — finally 블록에서 modelId Clear 제거** (소유권이 캐시로 이전되었으므로 매 호출 폐기 금지)

찾을 텍스트(BEFORE):
```csharp
            finally
            {
                if (searchRect != null) { try { searchRect.Dispose(); } catch { } }
                if (reducedImage != null) { try { reducedImage.Dispose(); } catch { } }
                if (scaledImage != null) { try { scaledImage.Dispose(); } catch { } }
                if (modelId != null)
                {
                    try
                    {
                        if (string.Equals(engine, "NCC", StringComparison.OrdinalIgnoreCase))
                        {
                            HOperatorSet.ClearNccModel(modelId);
                        }
                        else
                        {
                            HOperatorSet.ClearShapeModel(modelId);
                        }
                    }
                    catch { }
                }
            }
        }
```

치환할 텍스트(AFTER):
```csharp
            finally
            {
                if (searchRect != null) { try { searchRect.Dispose(); } catch { } }
                if (reducedImage != null) { try { reducedImage.Dispose(); } catch { } }
                if (scaledImage != null) { try { scaledImage.Dispose(); } catch { } }
                // modelId 는 더 이상 여기서 Clear 하지 않는다 — 캐시(GetOrLoadModel)가 소유권을 가지며,
                // 재티칭(TryCreateModel -> InvalidateCache) 시점에만 Clear 된다. 매 호출마다 read+clear를
                // 반복하던 것이 이번 캐싱 작업(quick-260805-ojq)의 근본 수정 대상이었다.
            }
        }
```

(`HObject`류(`searchRect`/`reducedImage`/`scaledImage`) Dispose는 기존 그대로 유지 — 이것들은 캐시와 무관한 로컬 임시 이미지다.)


**코딩 규약 (필수 준수, 위반 시 재작업)**
- 삼항 연산자 `?:` 금지 → 위 AFTER 텍스트는 전부 `if`/`else`로만 작성되어 있음 — 그대로 옮길 것, 스스로 삼항으로 축약하지 말 것.
- C# 7.2 한정 — switch expression / nullable reference types / target-typed `new`(`new()`) / record 금지. 위 코드는 전부 `new Dictionary<string, CachedModelEntry>()`처럼 명시적 타입 인자를 쓴다.
- 이 파일은 **Allman 스타일**(여는 중괄호를 다음 줄에)이다 — 파일 전체가 이미 이 스타일이므로 위 AFTER 블록도 전부 Allman으로 작성되어 있다. `CaptureImageSaveService.cs`(K&R)와 혼동하지 말 것.
- 이 파일의 기존 지역변수/필드는 헝가리언 접두사를 쓰지 않는다(`isNcc`, `modelId`, `curRow` 등 전부 무접두사). 신규로 추가하는 `_cacheLock`/`_modelCache`/`GetOrLoadModel`/`InvalidateCache`/`CachedModelEntry`도 **이 파일의 기존 스타일을 그대로 따라 접두사 없이** 작성한다(파일 내 일관성 우선 — CLAUDE.md "파일/모듈의 기존 스타일을 따르라").
- 주석은 "왜"만 최소로. `//YYMMDD hbk` 날짜 접두 주석 규칙은 폐기됐으므로 신규 주석에 붙이지 않는다(위 AFTER 텍스트에 이미 반영됨 — 그대로 사용).
- `TryFindRefPose` 본문은 글자 하나도 건드리지 않는다.
  </action>

  <verify>
    <automated>
cd "C:/Info/Project/DataMeasurement" && "/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" //p:Configuration=Debug //p:Platform=x64 //v:minimal //nologo 2>&1 | tail -30
    </automated>
    <automated>
# 위 빌드가 MSB3021/MSB3027/MSB3030(파일 잠금 — 실행 중인 VS 디버그 세션과 bin/obj 경합, 오늘 세션에서 이미 1회 발생한 알려진 이슈) 로 실패하면:
# 절대 devenv.exe/DatumMeasurement.exe 프로세스를 강제종료(taskkill 등)하지 말 것 — 실행 중인 디버그 세션을 죽이면 사용자 작업 손실 위험.
# 대신 스크래치 OutDir로 컴파일 전용 재검증(레포의 실제 bin/obj는 건드리지 않음):
cd "C:/Info/Project/DataMeasurement" && "/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" //p:Configuration=Debug //p:Platform=x64 //p:OutputPath="$TEMP/gsd-ojq-scratch/bin/" //p:BaseIntermediateOutputPath="$TEMP/gsd-ojq-scratch/obj/" //v:minimal //nologo 2>&1 | tail -30
# 그래도 실패(잠금 외 사유)하면 실행을 중단하고 정확한 에러를 그대로 보고할 것 — 임의로 코드를 더 바꾸지 말 것.
    </automated>
    <automated>
cd "C:/Info/Project/DataMeasurement" && git diff --stat -- WPF_Example/Halcon/Algorithms/PatternMatchService.cs && echo "--- 위 변경 파일은 정확히 1개(PatternMatchService.cs)여야 한다 ---"
    </automated>
    <automated>
cd "C:/Info/Project/DataMeasurement" && F=WPF_Example/Halcon/Algorithms/PatternMatchService.cs && echo "using System.Collections.Generic(EXPECT 1): $(grep -c '^using System.Collections.Generic;' $F)" && echo "GetOrLoadModel def(EXPECT 1): $(grep -c 'private static HTuple GetOrLoadModel' $F)" && echo "GetOrLoadModel calls(EXPECT 2): $(grep -c 'GetOrLoadModel(modelPath,' $F)" && echo "InvalidateCache def(EXPECT 1): $(grep -c 'private static void InvalidateCache' $F)" && echo "InvalidateCache calls(EXPECT 1): $(grep -c 'InvalidateCache(modelPath);' $F)" && echo "_modelCache refs(EXPECT 5): $(grep -c '_modelCache' $F)" && echo "_cacheLock refs(EXPECT 3): $(grep -c '_cacheLock' $F)" && echo "CachedModelEntry refs(EXPECT >=4): $(grep -c 'CachedModelEntry' $F)"
    </automated>
    <automated>
cd "C:/Info/Project/DataMeasurement" && F=WPF_Example/Halcon/Algorithms/PatternMatchService.cs && echo "--- ClearNccModel/ClearShapeModel 총 호출 수(EXPECT 3 each — 위치만 이동, 총량은 동일) ---" && echo "ClearNccModel: $(grep -c 'ClearNccModel(' $F)" && echo "ClearShapeModel: $(grep -c 'ClearShapeModel(' $F)" && echo "--- TryFindPose 본문 내부 Clear 잔존(EXPECT 0 — 이번 수정의 핵심 검증) ---" && awk '/public bool TryFindPose\(/,0' "$F" | grep -c "ClearNccModel\|ClearShapeModel" && echo "--- TryCreateModel 구간 Clear(EXPECT 2, 무변경 — 임시 생성 핸들 자체 정리) ---" && awk '/public bool TryCreateModel\(/,/public bool TryFindRefPose\(/' "$F" | grep -c "ClearNccModel\|ClearShapeModel" && echo "--- TryFindRefPose 구간 Clear(EXPECT 2, 무변경 — 범위 밖) ---" && awk '/public bool TryFindRefPose\(/,/public bool TryFindPose\(/' "$F" | grep -c "ClearNccModel\|ClearShapeModel"
    </automated>
    <automated>
cd "C:/Info/Project/DataMeasurement" && echo "--- TryFindRefPose 본문 완전 무변경 확인(사전 저장된 원문과 diff, EXPECT 빈 출력) ---" && git diff -- WPF_Example/Halcon/Algorithms/PatternMatchService.cs | awk '/@@.*TryFindRefPose/,/@@/' | grep -v "^@@" | head -20
    </automated>
    <automated>
cd "C:/Info/Project/DataMeasurement" && echo "--- PatternMatchService 외 다른 파일에서 TryFindPose/TryCreateModel 호출부 전수 확인(EXPECT 정확히 3개 파일: InspectionSequence.cs, MainView.xaml.cs, AlignShapeMatchService.cs만) ---" && grep -rln "\.TryFindPose(\|\.TryCreateModel(" --include="*.cs" WPF_Example | grep -v "WPF_Example/Halcon/Algorithms/PatternMatchService.cs"
    </automated>
  </verify>

  <done>
- Debug/x64 빌드 성공(정상 경로 또는 스크래치 OutDir 경로 중 하나로 컴파일 확인), 신규 `error CS`/`warning CS` 0건.
- 변경 파일이 `PatternMatchService.cs` 1개뿐이다.
- `GetOrLoadModel` 정의 1개, 호출 2개(`TryFindPose`의 NCC/Shape 분기 각 1회)가 존재한다.
- `InvalidateCache` 정의 1개, 호출 1개(`TryCreateModel`의 `return true;` 직전)가 존재한다.
- `TryFindPose` 본문(`finally` 포함)에 `ClearNccModel`/`ClearShapeModel` 호출이 **0개** 남았다 — 소유권이 캐시로 완전히 이전됨.
- `TryCreateModel`과 `TryFindRefPose` 구간의 기존 Clear 호출(각 2개, 자기 자신의 임시 핸들 정리용)은 **그대로 남아** 무변경임이 확인된다.
- `TryFindRefPose` 본문에는 diff가 전혀 없다(범위 밖, 글자 하나도 안 바뀜).
- `PatternMatchService.cs` 외에 `TryFindPose`/`TryCreateModel`을 호출하는 파일이 `InspectionSequence.cs`/`MainView.xaml.cs`/`AlignShapeMatchService.cs` 3개뿐임을 재확인했고(오늘 다른 quick task가 새 호출부를 추가하지 않았음), 이 3개 파일에 대한 diff가 0이다(무수정 확인).
- 새 코드에 삼항 연산자 없음, Allman 브레이스 스타일 유지, C# 7.2 문법만 사용.
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 2: 실기 재현 — Test Find 반복 클릭 + 일괄검사 연속 실행 메모리 안정성 확인</name>
  <files>(검증 전용 — 코드 변경 없음)</files>
  <what-built>
`PatternMatchService`에 static 모델 캐시를 도입했다. 이제 같은 `modelPath`(Datum 하나당 고정 파일)에 대해 첫 호출에서만 디스크에서 모델을 읽고, 이후 호출(같은 Datum에 대한 반복 Test Find, 또는 배치검사의 매 사이클)은 캐시된 모델을 재사용한다. 재티칭(패턴 모델 재생성)을 하면 그 즉시 캐시가 무효화되어 다음 Find부터 새 모델을 쓴다. 이 버그는 원래 사용자가 실기에서 발견했으므로(Bottom NCC datum Test Find 반복 클릭 → Task Manager 메모리 상승 관찰), 최종 검증도 동일한 방식으로 사용자가 직접 확인해야 한다.
  </what-built>
  <action>
사용자가 아래 절차를 그대로 수행하여 (a) Test Find 반복 클릭 시 메모리가 안정적인지, (b) 일괄검사를 연속 실행해도 메모리가 안정적인지 확인한다.
  </action>
  <how-to-verify>
**사전 준비**
1. 앱을 완전히 재빌드(Debug/x64, Rebuild 권장)한 뒤 실행한다. (Task 1에서 스크래치 OutDir로만 컴파일 검증했을 수 있으므로, 사용자의 정식 빌드에서 다시 한 번 빌드 성공을 확인하는 의미도 있다.)
2. 작업 관리자(Ctrl+Shift+Esc) → 세부 정보 탭에서 `DatumMeasurement.exe`의 메모리(비공개 작업 집합)를 상시 볼 수 있게 띄워 둔다.

**(a) Test Find 반복 클릭 — 원래 재현 시나리오 그대로**
3. 레시피에서 **Bottom 카메라의, NCC 엔진(`PatternEngine = NCC`)으로 패턴정렬(`IsPatternAlignEnabled`)이 켜진 Datum**을 선택한다(사용자가 원래 문제를 재현했던 것과 동일한 Datum — 모르면 각 Datum의 PropertyGrid에서 `PatternEngine` 값을 확인).
4. 시작 시점 메모리를 메모한다.
5. **[Test Find] 버튼을 최소 30회 이상 연속으로 클릭**한다(원래 문제 재현 때와 비슷한 횟수).
6. 메모리 관찰:
   - 기대: 초반 1~2회 클릭에서 약간 상승(최초 로드) 후, 이후 클릭들에서는 거의 변화 없이 평평(flat)해야 한다. 클릭할 때마다 계속 우상향하면 실패.
   - 실패 신호: 클릭 횟수에 비례해 메모리가 계속 증가(수백 MB~GB 단위).

**(b) 일괄검사 연속 실행**
7. Test Find로 쓴 메모리가 안정된 상태에서, 패턴정렬 Datum이 포함된 레시피로 **일괄검사를 최소 20회 이상** 연속 실행한다(가능하면 원래 크래시 재현 때와 비슷한 회차).
8. 실행 중/완료 후 메모리 관찰:
   - 기대: 초기값 대비 수백 MB 범위 안에서 오르내리고, 계속 우상향만 하지 않는다(53GB까지 갔던 이전과 명확히 다른 패턴이어야 한다).
   - 실패 신호: GB 단위로 멈추지 않고 계속 증가한다.

**(c) 재티칭 회귀 확인 (stale 모델 재사용 방지 — 이번 수정의 핵심 요구사항)**
9. (a)에서 쓴 Datum을 다시 티칭(패턴 ROI를 살짝 옮기거나 [패턴 모델 생성]을 다시 클릭)한다.
10. 재티칭 직후 [Test Find]를 다시 클릭해, 검출 결과가 **재티칭한 새 위치 기준으로 정상 동작**하는지 확인한다(이전 모델이 계속 쓰이는 것처럼 결과가 예전 위치에 고정되어 있으면 실패).

11. 위 (a)/(b)/(c) 중 하나라도 이상하면 관찰된 수치(메모리 값, 클릭/회차 수)와 증상을 그대로 알려주세요.
  </how-to-verify>
  <verify>사용자 실측 승인. (a) Test Find 반복 클릭 시 메모리 평평, (b) 일괄검사 20회+ 연속 실행 시 메모리 유한 안정, (c) 재티칭 후 새 모델이 즉시 반영됨 — 3가지 모두 확인.</verify>
  <done>사용자가 (a)(b)(c) 세 가지를 모두 확인하고 "승인"함. 메모리가 GB 단위로 단조 증가하지 않음이 실측으로 재확인됨.</done>
  <resume-signal>"승인" 또는 관찰된 문제(메모리 수치 / 어느 단계(a/b/c)에서 실패했는지)를 알려주세요</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| Top/Side/Bottom 시퀀스 스레드 → static 모델 캐시(`_modelCache`) | 서로 다른 카메라 시퀀스 스레드가 동시에 같은 프로세스 전역 Dictionary 에 접근하는 지점 |
| UI 스레드(Test Find/패턴 모델 생성) → static 모델 캐시 | 사용자 클릭이 시퀀스 스레드와 동시에 같은 캐시를 건드릴 수 있는 지점(예: 배치검사 중 다른 Datum을 Test Find) |
| `TryCreateModel`(재티칭 무효화) → 진행 중인 `TryFindPose`(Find) | 재티칭이 다른 스레드가 현재 사용 중인 handle을 Clear할 수 있는 지점 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-ojq-01 | D (자원 고갈) | `TryFindPose`가 매 호출마다 모델을 통째로 read+clear (NCC 모델은 회전각×피라미드 레벨별 래스터 이미지 보유 — 특히 무거움) | mitigate | `modelPath` 키 static 캐시로 lazy-load + 재사용. Clear는 무효화 시점에만 발생(Task 1). 사용자 실기 재현(Task 2)으로 최종 확인. |
| T-ojq-02 | T (stale 데이터 사용) | 재티칭 후에도 캐시된 구모델이 계속 쓰여 사용자가 최신 티칭 결과를 못 보는 회귀 | mitigate | `TryCreateModel` 성공 직후(`return true;` 직전) `InvalidateCache(modelPath)`로 즉시 Clear+캐시 제거 — CONTEXT.md LOCKED, Task 1 verify에서 정적 확인 + Task 2(c)에서 실측 재확인. |
| T-ojq-03 | D (경쟁 상태로 인한 크래시/컬렉션 손상) | 여러 스레드가 동시에 `_modelCache` 딕셔너리를 조회/삽입/제거 | mitigate | 모든 딕셔너리 접근(`GetOrLoadModel`, `InvalidateCache`)을 단일 `lock (_cacheLock)` 아래로 통일. |
| T-ojq-04 | D/T (Find 도중 무효화 경합) | `InvalidateCache`가 실행되는 순간, 다른 스레드가 같은 `modelId`로 `FindNccModel`/`FindShapeModel`을 이미 실행 중일 수 있음(락 밖에서 Find가 실행되므로) | accept | 재티칭은 사용자의 단발 UI 액션이며 통상 검사가 정지된 상태에서 수행된다. 설령 경합이 발생해도 HALCON이 invalid-handle 예외를 던지고, `TryFindPose`의 기존 `catch (Exception ex)` 경로가 이를 흡수해 `false` 반환(해당 1회 Find 실패)으로 그치며 프로세스 크래시로 이어지지 않는다. 참조 카운트/`ReaderWriterLockSlim` 등 근본 해결은 CONTEXT.md LOCKED 설계(정직렬화 없음) 밖의 스코프 확대이므로 채택하지 않는다. |
| T-ojq-05 | D (Find-vs-Find 동시 호출) | 같은 `modelId`로 여러 스레드가 동시에 `FindNccModel`/`FindShapeModel` 호출 | accept | CONTEXT.md LOCKED 결정: HALCON 문서상 모델은 조회 중 read-only이므로 안전한 사용 패턴으로 간주, 별도 직렬화 없음 — `GetOrLoadModel`/캐시 필드 선언부 주석으로 이 가정을 명시함(Task 1). |
| T-ojq-06 | I / E / S | 정보 노출 / 권한 상승 / 스푸핑 | accept | 프로세스 내부 알고리즘 서비스의 캐싱 전략 변경일 뿐, 신뢰 경계 밖 노출면이나 권한 구조에 변화 없음. |
</threat_model>

<verification>
1. Task 1의 `<automated>` 전부 통과 — 빌드 성공, 캐시 인프라 구조 확인(정의/호출 개수), `TryFindPose` 본문에 Clear 잔존 0, `TryCreateModel`/`TryFindRefPose` 무변경 확인, 다른 캐시 호출부(`InspectionSequence.cs`/`MainView.xaml.cs`/`AlignShapeMatchService.cs`) 무수정 확인.
2. Task 2(사람 UAT) — (a) Test Find 30회+ 반복 클릭 시 메모리 평평, (b) 일괄검사 20회+ 연속 실행 시 메모리 유한 안정, (c) 재티칭 직후 새 모델이 즉시 반영(stale 재사용 없음) — 3가지 모두 사용자 승인.
</verification>

<success_criteria>
- 같은 `modelPath`에 대해 `TryFindPose`가 두 번째 호출부터는 `ReadNccModel`/`ReadShapeModel`을 호출하지 않는다(캐시 재사용).
- `TryCreateModel` 성공 즉시 같은 `modelPath`의 캐시가 무효화되어, 재티칭 후 stale 모델이 재사용되는 회귀가 없다.
- `TryFindRefPose`와 그 호출부(`AlignShapeMatchService.cs`)는 이번 변경으로 전혀 영향받지 않는다(무수정, 회귀 0).
- Bottom NCC datum Test Find 반복 클릭 및 일괄검사 연속 실행에서 프로세스 메모리가 더 이상 GB 단위로 단조 증가하지 않음이 실기로 확인된다.
- Debug/x64 빌드 PASS, 변경 파일은 `PatternMatchService.cs` 1개뿐, 다른 캐시 호출부(3개 파일) 무수정.
</success_criteria>

<output>
완료 후 `.planning/quick/260805-ojq-pattern-model-cache/260805-ojq-SUMMARY.md` 생성.

SUMMARY에 반드시 포함할 참고 사항:
> **참고:** `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs`(v1.3 Align 비전, Tray/Bottom 이더넷 카메라)도 `PatternMatchService.TryFindPose`/`TryCreateModel`을 그대로 호출하는 별도 서브시스템이며, 이번 캐싱 수정으로 무수정 상태로 동일한 이득(read+clear 반복 제거)을 자동으로 받는다. CONTEXT.md에는 명시되지 않았던 호출부이나 grep으로 확인 완료, 시그니처 불변이라 영향 없음.
>
> **앱 종료 시 캐시 정리 훅은 의도적으로 추가하지 않음** (CONTEXT.md "Claude's Discretion" 항목) — 이번 버그는 런타임 중 무한 증가였고, 프로세스 종료 시 OS가 회수하므로 필수가 아니라 스코프를 최소로 유지함. 필요해지면 별도 quick task로 추가 가능.
</output>
