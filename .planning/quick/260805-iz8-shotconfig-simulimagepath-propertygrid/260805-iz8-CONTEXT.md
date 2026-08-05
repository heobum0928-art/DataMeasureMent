# Quick Task 260805-iz8: ShotConfig.SimulImagePath PropertyGrid 전용 숨김 - Context

**Gathered:** 2026-08-05
**Status:** Ready for planning (사용자가 의미를 명확히 확정함 — D-2 질문에 "①" 응답)

<domain>
## Task Boundary

`ShotConfig.SimulImagePath`(ShotConfig.cs:20-21, `[Category("Shot|Simulation")]`)가 PropertyGrid에 노출되는 게 "의미 없다"는 사용자 요청. 사용자가 명확히 확정한 의미: **로직으로서는 계속 필요하다(SIMUL/오프라인 검사, 레시피 INI 저장/로드, 반복검사에서 지금도 핵심적으로 쓰임) — 다만 수동 편집 UI(PropertyGrid)로 노출될 필요가 없다는 뜻.**

**주의: `SimulImagePath` 필드/INI 직렬화 자체는 절대 삭제하지 않는다.** 삭제하면 SIMUL/오프라인 검사 전반(Action_FAIMeasurement.cs 다수 지점), 레시피 INI 저장/로드(InspectionRecipeManager.cs), 반복검사(RepeatRunService.cs)가 깨진다.

</domain>

<decisions>
## Implementation Decisions (LOCKED)

- 기존 컨벤션(커밋 d7896d1, DatumConfig의 TeachingImagePath_Vertical/Horizontal 사례 — "검사Grab이 채우고 런타임이 읽는 값이라 수동편집 UI로는 불필요"라는 이유로 필드/INI 직렬화는 유지한 채 `[PropertyTools.DataAnnotations.Browsable(false)]`만 추가해 그리드 표시만 숨김)를 **그대로 따른다.**
- `ShotConfig.cs:20-21`에 `[PropertyTools.DataAnnotations.Browsable(false)]` 한 줄만 추가한다.
- `System.ComponentModel.Browsable`이나 `Newtonsoft.Json.JsonIgnore`는 **추가하지 않는다** — 이건 직렬화까지 차단하는 다른 패턴(패턴 B)이고, 이번엔 패턴 A(PropertyGrid 전용 숨김)만 쓴다.
- 값/직렬화가 100% 보존되는지 확인(260805-kpy 의 실기 검증 방식 참고 — 프로그램 재시작 후 필드가 그리드에서 사라졌는지 + SIMUL/오프라인 검사가 여전히 정상 동작하는지).
- 툴바 버튼(`button_simFolder`, InspectionListView.xaml:227-232/1024-1070)은 **건드리지 않는다** — 이건 폴더 선택해서 전체 Shot에 일괄 할당하는 별도 기능이고 이번 작업 범위 밖.

</decisions>

<specifics>
## Specific Ideas

- 참고 커밋: d7896d1 (DatumConfig.TeachingImagePath_Vertical/Horizontal 동일 패턴).
- `ParamBase.Save()`(BindingFlags.Instance|Public 순회)/`Load()`(CanWrite 만 필터)는 `Browsable`을 보지 않는다 — 이미 260805-kpy 작업에서 코드로 확인된 사실, 재확인 불필요.

</specifics>

<canonical_refs>
## Canonical References

- 260805-kpy quick task (동일 패턴, 동일 검증 방식) — `.planning/quick/260729-kpy-*`.

</canonical_refs>
