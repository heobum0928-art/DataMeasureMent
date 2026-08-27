---
phase: 74-pattern-model-brush-masking
plan: 01
status: complete
date: 2026-08-27
---

# 74-01 SUMMARY — 브러시 마스크 코어

## 만든 것

| 파일 | 내용 |
|---|---|
| `Setting/SystemSetting.cs` | `UsePatternBrushMask` 토글 (기본 **false**) — 순수 삽입 |
| `Halcon/Services/PatternMaskService.cs` (신규) | 마스크 경로 도출 + 저장/로드/삭제 + 옵션 게이트 |
| `Halcon/Algorithms/PatternMatchService.cs` | `TryCreateModel` 안 ROI − 마스크 `Difference` — 순수 삽입 |
| `DatumMeasurement.csproj` | Compile 1개 — **커밋 안 함** |

## 공개 계약 (74-03 이 호출한다)

```csharp
// namespace ReringProject.Halcon.Services — static
public const string EXTENSION_PATTERN_MASK = ".mask.hobj";
public static string ResolveMaskPath(string szModelPath);                 // 실패 시 null
public static bool   IsMaskEnabled();
public static bool   HasMask(string szModelPath);                          // 옵션 토글 무시(상태 표시용)
public static bool   TryLoadMask(string szModelPath, out HObject maskRegion);   // true 면 호출자가 Dispose
public static bool   TrySaveMask(string szModelPath, HObject maskRegion, out string szError);
public static bool   DeleteMask(string szModelPath);
```

## 설계 요점

- **마스크 경로는 `modelPath` 문자열에서만 파생한다.** 폴더 규약을 새로 만들지 않으므로
  Datum(`GetPatternModelFilePath`)이든 Align(`BuildShmPath`)이든 무조건 모델과 같은 폴더에 떨어진다.
  Phase 73 에서 폴더 규약이 갈려 `.shm` 을 조용히 못 찾을 뻔한 사고가 재발하지 않는다.
- `X.shm` 과 `X.ncm` 은 **같은 마스크 `X.mask.hobj` 를 공유**한다 (마스크는 ROI 에 속하지 엔진에 속하지 않는다).
- `.hobj` = HALCON HOBJ 포맷. `.reg` 는 HALCON 12 이전 legacy — 공식 문서 확인 후 `.hobj` 채택.
- **`ResolveMaskPath`/`TryLoadMask` 는 디렉터리를 절대 만들지 않는다.** `CreateDirectory` 는 `TrySaveMask` 안에만 있다.
- `TryCreateModel` 은 `CreateShapeModel`/`CreateNccModel`/`Write*Model` 호출을 **한 줄도 안 건드렸다.**
  `rect` 를 `Difference` 결과로 **같은 변수에 재대입**해 기존 `finally` 가 그대로 Dispose 한다.

## 회귀 0 보장 구조

`TryLoadMask` 의 **첫 문장이 옵션 게이트**다 (`UsePatternBrushMask` 확인이 `File.Exists` 보다 앞).
옵션이 꺼져 있으면 마스크 파일이 디스크에 있어도 **존재 여부조차 보지 않고** false 를 돌려주므로,
`TryCreateModel` 의 마스크 분기에 진입하지 않고 `ReduceDomain` 은 편집 전과 동일한 `rect` 를 받는다.

## 검증 결과

**빌드 SIMUL-ON:** 에러 **0** / 경고 **18줄** / 코드 종류 `CS0162`·`CS0618` 2종 — baseline 유지.

| acceptance | 기대 | 실측 |
|---|---|---|
| `UsePatternBrushMask` 선언 / 총 등장 | 1 / 1 | **1 / 1** ✅ |
| `OriginImageFormat` (편집 전 실측 **3**) | 3 | **3** ✅ 무변경 |
| `EXTENSION_PATTERN_MASK = ".mask.hobj";` | 1 | **1** ✅ |
| `Directory.CreateDirectory` **실호출** | 1 | **1** ✅ (아래) |
| └ `TrySaveMask` 안에 있는가 | 1 | **1** ✅ |
| 게이트 순서 (옵션 < `File.Exists`) | 참 | **7줄 < 18줄** ✅ |
| csproj `PatternMaskService.cs` | 1 | **1** ✅ |
| `HOperatorSet.ReduceDomain` (편집 전 **2**) | 2 | **2** ✅ |
| `GenRectangle2` / `CreateShapeModel` / `CreateNccModel` | 1/1/1 | **1/1/1** ✅ |
| `PatternMaskService.TryLoadMask(modelPath, out maskRegion)` | 1 | **1** ✅ |
| `maskRegion.Dispose()` | 1 | **1** ✅ |
| 삽입 순서 `GenRectangle2 < TryLoadMask < ReduceDomain` | 참 | **173 < 177 < 200** ✅ |
| **호출부 무변경** (`AlignShapeMatchService.cs`, `MainView.xaml.cs`) | 빈 출력 | **빈 출력** ✅ |
| `PatternMatchService` 삼항 (편집 전 실측 **3**) | 3 | **3** ✅ 무변경 |

## Deviations

**[Rule 3 - 검증 기준 함정] `grep -c 'Directory.CreateDirectory'` 가 주석을 셌다 (코드 수정 없음)**

`PatternMaskService.cs` 에서 **2** 로 나왔으나, `18줄` 은 클래스 XML doc 주석
(`/// Directory.CreateDirectory 는 TrySaveMask 안에서만 허용한다.`)이다.
**실호출은 `149줄` 1건**이며 `TrySaveMask` 안에 있다. 주석 제외 검사로 확인했고 코드는 고치지 않았다.
그 주석은 이 클래스의 핵심 제약을 선언하는 문서다.

Phase 75 (75-02/03/04) 에 이어 **네 번째** 주석-포함 grep 함정. 인수인계 경고가 계속 적중한다.

## Self-Check: PASSED

1. 빌드 에러 0, 경고 코드 종류 2종뿐 ✅
2. `PatternMaskService.cs` 안 `Directory.CreateDirectory` 실호출이 `TrySaveMask` 한 곳뿐 ✅
3. `TryLoadMask` 첫 게이트가 `UsePatternBrushMask` (파일 존재 확인보다 앞) ✅
4. `TryCreateModel` 호출부 2개 파일 git diff 무변경 ✅
5. csproj unstaged ✅
