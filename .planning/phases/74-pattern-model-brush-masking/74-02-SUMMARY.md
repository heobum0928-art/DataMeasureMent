---
phase: 74-pattern-model-brush-masking
plan: 02
status: complete
date: 2026-08-27
---

# 74-02 SUMMARY — 뷰어 브러시 입력 + HALCON 창 내부 반투명 표시

## 확정 공개 API (Plan 03/04/05 가 이대로 호출한다 — 이름·시그니처 변경 금지)

```csharp
// MainResultViewerControl (namespace ReringProject.UI)
public void StartBrushMasking();
public void StopBrushMasking();                  // 모드만 끈다. 마스크는 지우지 않는다
public bool IsBrushMaskingActive { get; }
public double BrushRadiusPx { get; set; }        // setter 에서 5.0~200.0 클램프, 기본 20.0
public bool IsBrushEraseMode { get; set; }
public HObject CloneBrushMaskRegion();           // 비어 있으면 null. 반환본 Dispose 는 호출자 책임
public void SetBrushMaskRegion(HObject region);  // 내부에 복사본 보관 → 호출자는 인자를 계속 소유
public void ClearBrushMask();
public bool HasBrushMask { get; }
public event EventHandler BrushStrokeCompleted;  // 마우스 놓기 시 1회

// HalconDisplayService (namespace ReringProject.Halcon.Display)
public void RenderBrushMask(HWindow window, HObject maskRegion, string fillColor, string outlineColor);
public void RenderBrushCursor(HWindow window, double row, double col, double radius, string color);
```

## 설계 요점

- **자국은 선을 굵히는 방식**(`GenRegionLine` → `DilationCircle`)이라 마우스 이벤트가 듬성듬성 와도
  붓 자국이 끊기지 않는다. 누른 직후 첫 점만 `GenCircle`.
- **HObject 교체 순서** — 새 것을 만든 뒤 옛 것을 Dispose 한다. 뒤집으면 렌더 스레드가 죽은 핸들을 본다.
- **지우개로 다 지우면 `null` 로 정규화**(`AreaCenter` 면적 0 확인) — `HasBrushMask` 가 거짓말하지 않는다.
- **표시는 HALCON 창 안**(`DispObj`). 채우기는 `'#ff3b3b60'` — `aa`=알파. HALCON 24.11 공식 문서에서
  `'#rrggbbaa'` 가 반투명 region 표시의 유일한 정식 수단임을 확인했다(표준 색상명에는 알파가 없다).
- **채우기 실패해도 외곽선은 그린다.** try 를 나눠서, `'#rrggbbaa'` 를 못 받는 환경에서도 위치는 보인다.
- **전역 `SetDraw` 는 항상 `"margin"` 으로 원복**한다(`HalconDisplayService.Render` 전역 규약).
- 브러시 분기가 세 마우스 핸들러에서 **가장 먼저** 검사되어 팬/줌/ROI편집/폴리곤/측정과 겹치지 않는다.
- 마스크가 있으면 **브러시 모드가 아니어도 계속 보여준다** — 무엇이 빠진 채 모델이 만들어졌는지 알아야 한다.

## 검증 결과

**빌드 SIMUL-ON:** 에러 **0** / 경고 **18줄** / 코드 종류 2종 — baseline 유지.

| acceptance | 기대 | 실측 |
|---|---|---|
| `RenderBrushMask` / `RenderBrushCursor` 시그니처 | 1 / 1 | **1 / 1** ✅ |
| `RenderBrushMask` 안 `margin` 원복 | ≥2 | **2** ✅ |
| `RenderBrushMask` 안 `fill` | 1 | **1** ✅ |
| `SetDraw` 총 (편집 전 실측 **3**) | >3 | **9** ✅ |
| `HalconDisplayService` 삼항 (편집 전 **0**) | 0 | **0** ✅ |
| **공개 API 10종** | 10 | **10** ✅ |
| `DisposeBrushMaskRegion()` 정의 | 1 | **1** ✅ |
| `Dispose()` 안 호출 | 1 | **1** ✅ |
| `IsAnyDrawingModeActive` 에 `_isBrushMasking` | 1 | **1** ✅ |
| `RenderBrushMask`/`Cursor` 호출 | 1 / 1 | **1 / 1** ✅ |
| 렌더 순서 (마스크 < `RenderEditHandles`) | 참 | **1225 < 1232** ✅ |
| `ApplyBrushStamp` 정의 / 호출 | 1 / 2 | **1 / 2** ✅ |
| `DilationCircle` / `Union2` / `Difference` | 1/1/1 | **1/1/1** ✅ |
| `brushStrokeCompletedHandler(this, EventArgs.Empty);` | 1 | **1** ✅ |
| **가로채기 위치** (`HMouseUp` 본문 내 상대 줄) | ≤5 | **3** ✅ |
| 기존 배선 무증가 (Down/Move/Up) | 1/1/1 | **1/1/1** ✅ |
| `"#ff3b3b60"` | 1 | **1** ✅ |
| `MainResultViewerControl` 삼항 (편집 전 **1**) | 1 | **1** ✅ 무변경 |

## Deviations

**[Rule 1 - 계획 허용 분기] `bFilled` 변수 제거**

계획 Task 1 은 `bFilled` 플래그와 빈 `if (bFilled == false) { }` 블록을 두되
"CS0162 경고가 나면 그 블록을 통째로 지우라(변수도 함께)" 고 명시했다.
빈 블록은 아무 일도 하지 않으므로 **처음부터 넣지 않았다** — 계획이 허용한 결과와 동일하며
경고 코드 종류가 늘지 않았음을 빌드로 확인했다.

**M-1 수정 확인:** quick-260827-hdf 에서 고친 "가로채기 위치" 기준
(`awk` 로 `ViewerHost_HMouseUp` 본문 범위 내 상대 줄)이 **실제로 정상 동작**했다 — 상대 3줄.
원래 기준(파일 전체 첫 줄 번호)이었다면 `StartBrushMasking`(파일 앞쪽)이 잡혀 실패했을 것이다.

## Self-Check: PASSED

1. 빌드 에러 0, 경고 코드 종류 CS0618/CS0162 2종뿐 ✅
2. 공개 API 10개 + `RenderBrushMask`/`RenderBrushCursor` 존재 ✅
3. 브러시 분기가 세 마우스 핸들러에서 가장 먼저 검사된다 ✅
4. `Dispose()` 에서 마스크 HObject 해제 ✅
5. `SetDraw` 가 항상 `"margin"` 으로 원복 ✅
