# Phase 20: 코드 스타일 정리 — Context

**Gathered:** 2026-05-08
**Status:** Ready for planning

<domain>
## Phase Boundary

v1.1 phase 18·19·28 에서 hbk 마커가 추가된 14 파일을 대상으로 (a) `?:` / `??` / `?.` 연산자를 명시적 if/else 로 변환, (b) "what" 주석을 제거하고 "why" 주석만 보존, (c) 이전 hbk 마커 스택 정리하여 코드 노이즈를 줄인다.

**범위 내:**
- v1.1 hbk 마커 14 파일 (DatumConfig, DynamicPropertyHelper, EdgeOptionLists, FAIConfig, CircleDiameterMeasurement, DatumFindingService, VisionAlgorithmService, HalconDisplayService, MainResultViewerControl.xaml.cs, MainView.xaml.cs, InspectionListView.xaml.cs, ComboInputBox.cs, ComboInputBoxWindow.xaml, ComboInputBoxWindow.xaml.cs)
- 각 파일 *전체* (hbk 마커 없는 구 코드 라인 포함) 의 연산자/주석 정리
- AC #3 회귀 검증 (SIMUL_MODE 1회 Datum + FAI 결과 byte-identical)

**범위 외:**
- 14 파일 외 코드베이스 ~144 파일 (Phase 26 헝가리안 리팩토링이 흡수)
- 헝가리안 표기법 적용 (Phase 26)
- 새 기능 추가 / 알고리즘 변경
- LINQ chain 끝의 `?.` 와 expression-bodied member (`=> _field`) 변환

</domain>

<decisions>
## Implementation Decisions

### 연산자 변환 정책

- **D-01:** ROADMAP 엄격 해석 — `?:` 삼항, `??` null 병합, `?.` null 조건 **모두** 명시적 if/else 또는 임시변수 + null 체크로 분해. CONVENTIONS.md §2 의 "?? 단독 / 1-depth ?: 허용" 규칙은 Phase 20 한정으로 대체 (Phase 20 의 v1.1 정책 우선).
- **D-02:** `event?.Invoke(this, e)` → 멀티스레드 경주 안전한 임시변수 패턴:
  ```csharp
  var handler = MyEvent;
  if (handler != null) handler(this, e);
  ```
- **D-03:** `obj?.Field ?? defaultValue` 와 `a?.b?.c` chain → 일차 if/else 분해 + 결과 임시변수. 예:
  ```csharp
  // 변환 전: var v = obj?.Field ?? defaultValue;
  var v = defaultValue;
  if (obj != null) v = obj.Field;
  ```
  깊은 chain (`a?.b?.c`) 은 계층마다 if 로 분해.
- **D-04:** **예외 — LINQ chain 끝의 `?.` 는 유지.** 예: `list.FirstOrDefault(x => x.Name == n)?.Value` 는 그대로 둔다. 사유: 임시변수 도입 비용 대비 가독성 손실이 큼, 의미적으로 단일 expression.
- **D-05:** **예외 — Expression-bodied member 유지.** `public string Name => _name;` 같은 C# 6 expression-bodied 는 Phase 20 변환 대상이 아님. ROADMAP/QUAL-02 가 지목한 3 종 연산자에 포함되지 않음. Phase 26 (헝가리안+더 큰 스타일 정리) 에서 별도 컨벤션으로 다룰 후보.

### 변환 파일 스코프

- **D-06:** 변환 대상 = v1.1 hbk 마커가 추가된 14 파일만. 코드베이스 나머지 ~144 파일은 Phase 26 가 흡수.
- **D-07:** 14 파일 내 hbk 마커 없는 구 코드 라인도 동일 정책 적용 — 파일 단위 일관성 확보. 같은 파일 안에서 한 곳은 if/else 다른 곳은 `?:` 인 혼란 방지.

### 주석 정리 (QUAL-04)

- **D-08:** "what" 임계 = **코드로 다 드러나는 내용 제거.** 메서드명/변수명으로 명확한 설명은 제거. 알고리즘 의도, 하드웨어 프로토콜 매핑, "왜 이 수치 (예: trimCount=2)" 는 보존.
- **D-09:** 한국어/영어 혼재 주석 — *내용* 기준으로만 판정. 한국어 nuance 주석은 기본 보존 (산업 도메인 의도 손실 방지).
- **D-10:** XML doc (`/// <summary>`) **유지** — CONVENTIONS.md "공공 유틸리티 메서드 XML doc 필수" 와 IDE intellisense 보존.
- **D-11:** `#region` **유지** — 대규모 논리 그룹화 (SequenceBase 등) 보조용으로 가치 있음.

### hbk 마커 스택 처리

- **D-12:** Phase 20 가 라인을 다시 쓰는 경우, 같은 라인의 이전 hbk 마커는 **최신 `//260508 hbk Phase 20` 으로 교체** (스택하지 않음). 변경 이력은 git log/blame 에 위임. 사유: 사용자 명시 목표 = 코드 노이즈 감축.
- **D-13:** 변환되지 않는 라인의 이전 hbk 주석은 **그대로 보존** — 안전 우선, scope creep 방지. (즉, Phase 20 스코프 = 변환 라인 + 주석 정리 라인만 마커 변경.)

### 회귀 검증 (AC #3)

- **D-14:** 입증 방법 = msbuild Debug/x64 PASS + SIMUL_MODE 1회 Datum 티칭 (TLI/CTH/VTH 3 알고리즘) + FAI 측정 1회 (Edge 6종 + CircleDiameter) → 검출 origin (Row/Col) / measured mm 값 변환 전·후 비교.
- **D-15:** 회귀 테스트 레시피 = Phase 28 SIMUL UAT 레시피 재사용 (이미 검증된 고정점, 14 파일 변환 경로 대부분 커버). D:\1.bmp 사용.
- **D-16:** 비교 임계 = byte-identical (1e-9 mm 마진). `?:` → if/else 와 `??` → 명시 null 체크는 의미론적으로 동등 변환이므로 런타임 결과 동일해야 함. 차이 발견 시 변환 버그.
- **D-17:** msbuild 신규 warning = 0 (기존 warning 은 보존, 변환 작업이 새 warning 도입 금지).

### Claude's Discretion

- 14 파일 내에서 변환을 **plan wave 분할** 방식 (5 파일 / 5 파일 / 4 파일 vs 의존 그룹별) 은 플래너 결정.
- "what" 판정 경계 케이스는 플래너/실행자가 case-by-case 판단 — D-08 ~ D-09 가 가이드.
- `?.` 분해 시 임시변수 명명 (`_v`, `tmpResult` 등) 은 플래너 결정.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 요구사항 정의
- `.planning/ROADMAP.md` §Phase 20 — 성공 기준 4개
- `.planning/REQUIREMENTS.md` §QUAL-02, §QUAL-04 — Phase 20 요구사항 정의
- `.planning/CONVENTIONS.md` — 참고용. Phase 20 의 연산자 정책은 ROADMAP 우선 (D-01 우선 규칙). 주석/region/early-return 규칙은 CONVENTIONS.md 와 일치 (D-08~D-11).

### v1.1 마일스톤
- `.planning/PROJECT.md` §v1.1 Active — 코드 품질 cross-cutting 항목
- `.planning/v1.1-MILESTONE_add.md` — CODE-RULES.md 도입 계획 (Phase 26 에서 신규 작성 예정, Phase 20 시점 미존재)

### 회귀 검증 레퍼런스 (AC #3)
- `.planning/phases/28-fai-circlediameter-datum-circle/28-UAT.md` — 재사용 SIMUL 레시피 출처 (Phase 28 sign-off 레시피)
- `.planning/phases/28-fai-circlediameter-datum-circle/28-CONTEXT.md` — Phase 28 결정사항 (CircleDiameter polarity, Datum CTH 통합)

### 변환 대상 14 파일 (소스)
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs`
- `WPF_Example/Custom/Sequence/Inspection/DynamicPropertyHelper.cs`
- `WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs`
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs`
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs`
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs`
- `WPF_Example/Halcon/Display/HalconDisplayService.cs`
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs`
- `WPF_Example/UI/ContentItem/MainView.xaml.cs`
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs`
- `WPF_Example/UI/Dialog/ComboInputBox.cs`
- `WPF_Example/UI/Dialog/ComboInputBoxWindow.xaml`
- `WPF_Example/UI/Dialog/ComboInputBoxWindow.xaml.cs`

### 선행 phase context (참고)
- `.planning/phases/18-carry-over-cleanup/18-CONTEXT.md` — hbk 마커 컨벤션 원형 (`//260505 hbk Phase 18`)
- `.planning/phases/19-propertygrid-dynamic-exposure/19-CONTEXT.md` — DynamicPropertyHelper 구조

</canonical_refs>

<code_context>
## Existing Code Insights

### 변환 대상 14 파일 — 연산자 분포 (rough grep, 문자열 포함 가능)

| 파일 | `?.` | `??` | `?:` rough | 비고 |
|------|------|------|-----------|------|
| FAIConfig.cs | 0 | 10 | 10 | INI 자동 직렬화 + 기본값 fallback 패턴 다수 |
| DatumFindingService.cs | 0 | 10 | 21 | Halcon HTuple null 가드 다수 |
| MainResultViewerControl.xaml.cs | 12 | 1 | 25 | UI binding null safety |
| MainView.xaml.cs | 13 | 3 | 22 | UI 이벤트/dispatcher 다수 |
| InspectionListView.xaml.cs | 9 | 1 | 11 | TreeView selection null 가드 |
| 기타 9 파일 | 0 | 0 | 0~5 | 단순 모델/dialog (변환 부담 낮음) |

전체 변환 포인트 ≈ 148 (rough). 5 파일이 핵심 부담.

### Reusable Assets (선행 phase 패턴 재사용)

- **임시변수 + null 체크 패턴** — Phase 18 D-13 `CircleStripSuccesses` 와 Phase 19 `EnsureEdgeMeasureTypeDefault` 가 이미 사용. Phase 20 의 D-02/D-03 는 이 패턴 확장.
- **early return 패턴** — CONVENTIONS.md §4 + 코드베이스 다수 (`if (!bIsValid) return;`). Phase 20 변환 시 메서드 도입부에서 `?:` → early return 으로 풀 수 있는 케이스 활용.
- **Phase 28 SIMUL UAT 레시피** — 14 파일 중 7 개 (FAIConfig, CircleDiameterMeasurement, DatumFindingService, MainView 등) 가 Phase 28 검증 경로. 동일 레시피로 회귀 커버.

### Established Patterns

- **hbk 주석 단일화** — D-12 가 신규 컨벤션. 이전 phase 들은 다중 hbk 스택 안 했으므로 호환.
- **//260508 hbk Phase 20 마커** — 변환된 라인에만 부착. 변경 안 된 라인은 기존 상태 보존.
- **byte-identical 회귀 임계** — Phase 28 REQ-28-03 (0.001 mm 수렴) 보다 엄격. 의미론적 동등 변환이므로 byte-identical 가능.

### Integration Points

- 14 파일 모두 ParamBase / ICustomTypeDescriptor / SequenceBase 프레임워크에 연결 — 변환이 framework 인터페이스 깨면 안 됨.
- DatumFindingService / VisionAlgorithmService — Halcon HOperatorSet wrapping 부분의 try/catch 패턴 보존 필수 (CONVENTIONS.md §4 try-catch 정책).

</code_context>

<specifics>
## Specific Ideas

- **사용자 명시 목표:** "주석이 너무 많아서 줄이고 싶다." → D-12 (hbk 스택 X) + D-08 (코드로 드러나는 what 제거) 가 직접 응답.
- **`event?.Invoke` 패턴 (D-02):** "var handler = ev; if (handler != null) handler(this, e);" — 사용자 추천 옵션 채택.
- **회귀 임계 (D-16):** byte-identical = 1e-9 mm. Phase 28 REQ-28-03 의 0.001 mm 보다 엄격. `?:` → if/else 는 단순 치환이라 잉여 차이 없어야 함.
- **Plan 분할 가이드:** 5 파일 핵심 부담 (FAIConfig, DatumFindingService, MainResultViewerControl, MainView, InspectionListView) + 9 파일 단순 = wave 1: 5 핵심, wave 2: 9 단순. 또는 의존 그룹별. 플래너 결정.

</specifics>

<deferred>
## Deferred Ideas

### Phase 26 헝가리안 전체 리팩토링 시 흡수
- **나머지 코드베이스 ~144 파일의 `?:` / `??` / `?.` 변환** — Phase 26 가 헝가리안 표기법 적용 시 같은 파일 만지므로 함께 처리하는 것이 효율적. Phase 26 discuss 시 결정.
- **Expression-bodied member (`=> _field`) 변환 정책** — D-05 에서 Phase 20 제외. Phase 26 또는 별도 phase 에서 컨벤션 확정.
- **CONVENTIONS.md → CODE-RULES.md 이관** — v1.1-MILESTONE_add.md 의 CODE-RULES.md 도입 계획. Phase 26 에서 신규 작성.

### 후속 phase 자체 정리 부담
- v1.1 phase 21·22·23·24·25·27 도 같은 14 파일을 만질 가능성 — 새 변경 라인은 D-01~D-05 정책 적용 권고 (각 phase 의 plan 에서 명시). Phase 20 sign-off 후 .planning/CONVENTIONS.md 또는 별도 메모리에 권고 추가 검토.

### 50 회 반복 GR&R 회귀 (Phase 25 OUT-03 통합)
- Phase 25 OUT-03 (50 회 반복 측정 엑셀) 도입 후, Phase 20 변환의 50 회 반복 동등성 검증 가능. 현재 Phase 20 은 1 회 비교 (D-14) 만 — Phase 25 도입 시 보강 가능.

</deferred>

---

*Phase: 20-code-style-cleanup*
*Context gathered: 2026-05-08*
