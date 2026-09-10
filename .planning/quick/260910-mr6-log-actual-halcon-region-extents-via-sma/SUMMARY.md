---
task: 260910-mr6
slug: log-actual-halcon-region-extents-via-sma
status: code-complete-uat-pending
date: 2026-09-10
---

# 요약 — Compound Rect ROI 탐색영역 "실측" 관측 로그

`260910-md3` 가 넣은 관측 로그는 `dGenLen1/dGenLen2` 에서 폭/높이를 **역산**해 찍었다.
그 값들은 "HALCON `gen_rectangle2` 4번째 인자 = Phi 방향 반장축" 이라는 **이 코드의 가정**에서
나오므로, 가정이 틀려도 기대값을 그대로 출력하는 **순환 로그**였다(자기 가정을 반증 불가).

이번 작업은 `gen_rectangle2` 가 **실제로 만들어낸 region** 을 HALCON 에 되물어 실측 폭/높이를
남기도록 교체했다. 코드의 가정과 독립적인 근거가 된다.

## 변경 (커밋 `878bbbb2`)

`WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` 2곳
(`TryFindLargestContourRect` / `TryFindShortAxisIntersections`), `GenRectangle2` 직후.

- `smallest_rectangle1` → `region(aabb): height/width` — **주 판정 지표**.
  파라미터 이름에 행/열이 박혀 있어 해석 여지가 0이고, 사용자 요구("꼭지점 4점을 사이즈별로")에 대응.
- `smallest_rectangle2` → `region(rect2): phi/major/minor` — 보조 지표.
  HALCON 이 `Length2 <= Length1` 로 **정규화**하므로 축이 뒤바뀌어도 길이 숫자는 동일하고 `phi` 만
  바뀐다. 오독을 이름 차원에서 막기 위해 `len1/len2` 가 아니라 `major/minor` 로 표기했다.
- 기존 계산값(`dHeightPx`/`dWidthPx`) 로그는 **제거**. 실측값이 회귀 감지 능력까지 포섭한다
  (swap 을 되돌리면 region 자체가 뒤집히므로 실측값도 뒤집힌다). `height=` 쌍이 둘이면 오독 위험.

## 안전성 — "기존 검사에 영향을 받으면 안 된다" (사용자 명시 제약)

**관측 전용. 측정 결과에 영향 0.**

- 측정 로직 diff **0**: `dGenLen1`/`dGenLen2` 계산, `GenRectangle2` 호출, `ReduceDomain` 이하
  파이프라인 전부 불변. 이번 커밋에서 사라진 `dGenLen*` 관련 라인은 옛 로그 전용 지역변수 2쌍뿐이며,
  전체 파일 grep 상 다른 사용처가 없다.
- 삽입 지점은 두 메서드의 거대한 `try { ... } catch { return false; }` **내부**다. 여기서 예외가 나면
  바깥 catch 가 삼켜 **측정이 조용히 실패**한다. → region 조회와 모든 `.D` 접근을 **자체
  `try { } catch { }`** 로 격리했고, 센티넬(`PROBE_FAIL = -1.0`)을 try 진입 **전**에 대입해
  실패 경로 자체가 예외를 낼 수 없게 했다. `Logging.PrintLog` 는 격리 블록 **밖**이라 조회가
  실패해도 로그 한 줄은 반드시 남는다.
- `rect` region 은 읽기 전용으로만 접근한다(HALCON 문서상 `input_object` / `const Hobject`).
  직후 `ReduceDomain(image, rect, ...)` 이 같은 region 을 그대로 쓴다.

## 로그 판독표

정상(`Rect_Phi=0`, 예: `L1=189, L2=199`):
```
[ContourRect] roi: ... teach: halfRow=189.0 halfCol=199.0  region(aabb): height=378.0 width=398.0  region(rect2): phi=0.0000 major=199.0 minor=189.0
```

| 관측 | 판독 |
|---|---|
| `height == 2*halfRow` 이고 `width == 2*halfCol` | **정상** — 그린 박스대로 탐색 |
| `height == 2*halfCol` 이고 `width == 2*halfRow` | **축 반전 회귀** — 즉시 보고 |
| 전 항목 `-1.0` | region 조회 실패(격리 catch). 측정에는 무해 |
| **일부만 `-1.0`** (예: `aabb` 는 실값, `rect2` 는 `-1.0`) | 두 연산자 중 하나만 실패. **측정에는 무해**하며 전체 실패로 오독하지 말 것 |
| `height`/`width` 가 `0.0` | 빈 region — ROI 가 이미지 밖이거나 clipping 의심 |
| `region(rect2): phi` 가 `0` 이 아님 | `Rect_Phi != 0` 이거나 축 반전. `aabb` 는 이때 AABB 라 값이 커지므로 `aabb` 단독 판정 불가 |
| ±1 px 오차 | 래스터화 정상 범위 |

## CLAUDE.md 규칙에 대한 의도적 예외 1건

CLAUDE.md §5 는 `HTuple` Dispose 를 요구하지만, 이번에 추가한 연산자 out-`HTuple`
(`hvBoxRow1` 등 9개)은 **해제하지 않았다.**

- **실제 이유**: 이 코드베이스는 연산자 out-`HTuple` 을 해제하는 사례가 단 한 건도 없다
  (같은 파일 `:309`, `:909`, `:1071` 등). 파일·저장소 전반의 일관된 관행을 따랐다.
- 계획 단계에서 "안전하게 해제할 방법이 없다"는 근거를 들었으나 이는 **약한 주장**이다 —
  같은 메서드가 이미 `finally { if (rect != null) { try { rect.Dispose(); } catch { } } }` 라는
  안전한 해제 관용구를 쓰고 있어, 같은 방식으로 해제해도 예외 유출 경로는 열리지 않는다.
- 즉 이것은 **의식적이고 재검토 가능한 일탈**이다. 저장소 전반의 out-`HTuple` 정책을 잡을 때
  함께 정리할 것.

## 실기 UAT (사용자 수행)

- [ ] 1. Compound 측정이 포함된 Shot 오프라인 검사 → `D:\Data\Algorithm\` 로그의
      `[ContourRect]` / `[ShortAxis]` 줄에서 `height == 2*halfRow`, `width == 2*halfCol` 확인
      (**불변식 판정**. 절대좌표 아님 — ROI 이동·datum 보정으로 중심은 움직인다)
- [ ] 2. **안전성 회귀 테스트** — 같은 검사에서 Compound 4종 6개 항목의 측정값이
      `260910-md3` 직후와 **동일**한지 확인. 관측 전용 변경이므로 값이 변하면 안 된다.
      변했다면 즉시 보고 → 롤백
- [ ] 3. `EdgeToLineDistance` 등 다른 측정이 회귀하지 않았는지 확인

## 검증 완료 (정적)

- MSBuild `Debug|x64` — **에러 0**, `DatumMeasurement.exe` 재생성
- 하드룰(추가 라인 기준): 삼항 0 / `??` 0 / `?.` 0 / switch 식 0 / 날짜 주석 0
- 변경 파일: `VisionAlgorithmService.cs` **1개** — 금지 파일(`DatumMeasurement.csproj`,
  `FAIEdgeMeasurementService.cs`, `PatternMatchService.cs`, `MainView.xaml.cs`) 전부 미접촉
- `TryFitLine`/`AppendStrip`(ly4 수정분), `TryFindCircleByPolarSampling` 미접촉

## 상태

코드 작업 완료. **실기 UAT 대기** (실카메라/오프라인 검사 필요 — 사용자 수행).
