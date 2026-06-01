# Phase 40: 결과 분석 & Export I — 리뷰어 + 1회 검사 엑셀 - Context

**Gathered:** 2026-06-01
**Status:** Ready for planning

<domain>
## Phase Boundary

검사 결과의 **사후 출력 계층**을 구축한다:
- **OUT-01 결과 리뷰어**: 날짜/원본 폴더로 과거 검사 결과를 로드하여 결과 이미지 + overlay + 판정을 재현
- **OUT-02 1회 검사 엑셀 Export**: 시퀀스 1회 검사(cycle) 결과를 메타데이터 + 측정값(mm) + 판정(OK/NG) + 이미지 링크가 포함된 xlsx 로 export

**범위 밖 (Phase 41 — OUT-03/04):** 50회 반복도 통계(mean/stddev/range/Cpk), 검출 알고리즘별 통계 분석표. 신규 capability 추가 금지.
</domain>

<decisions>
## Implementation Decisions

### 결과 영속화 전략 (OUT-01 핵심 토대)

- **D-01 (재현 방식 = 구조화 JSON + 재렌더):** 검사 시 **cycle 단위 구조화 JSON**을 저장한다. JSON 은 측정값(mm) + 판정(OK/NG) + nominal/tolerance + overlay 기하(`EdgeInspectionOverlay` 직렬화: Points, LineRow/Col 등) + 원본/결과 이미지 경로를 담는다. 리뷰어는 JSON 을 **역직렬화**한 뒤 `HalconDisplayService` 로 overlay 를 **재렌더**한다. `Newtonsoft.Json` 이미 사용 가능. **이 영속화 계층은 OUT-01(재렌더)·OUT-02(xlsx)의 공통 토대** — xlsx 도 동일 JSON 데이터를 재사용한다. (대안 기각: 결과 PNG만 저장 = 인터랙티브 X; raw+레시피 재실행 = 무겁고 레시피 변경 시 불일치.)

- **D-02 (cycle 메타데이터 = 타임스탬프 + 모델/레시피명 + 종합판정):** 현재 cycle 단위 메타가 전무하므로 신규로 캡처한다. 최소 실용 세트 = 검사 일시 + 현재 레시피/모델명 + 종합 판정(OK / NG / 검출실패). (작업자/TestId/시퀀스명 등은 범위 밖 — 필요 시 후속 확장.)

- **D-03 (저장 위치/단위 = `ResultSavePath/{YYYYMMDD}/` cycle 폴더):** 기존 `SystemSetting.ResultSavePath`(기본 `./Result`, 현재 구조 없음)를 활용. `./Result/{YYYYMMDD}/{HHmmss}_.../` 형태의 **cycle 폴더**에 JSON + (관련) 이미지를 둔다. **1 검사 = 1 cycle = 전 Shot/FAI 포함.** 날짜 폴더 로드 UX(D-09)와 자연 연결.

### xlsx Export (OUT-02)

- **D-04 (라이브러리 = ClosedXML, MIT):** 상용 산업 제품이므로 라이선스 안전한 MIT 채택. fluent API + 이미지/하이퍼링크 지원, .NET 4.6+ 타깃 → 4.8 호환. **(연구 필요)** packages.config(classic) 환경에서 `DocumentFormat.OpenXml` 등 전이 의존성 수동 추가 + .NET 4.8 바인딩 검증은 research/plan 단계에서 확인. (대안: NPOI/Apache, OpenXML SDK verbose, EPPlus 5+ 상용 라이선스 위험.)

- **D-05 (행 구조 = 1행 = 1측정):** 각 측정항목마다 한 줄. 컬럼 = Shot / FAI / 측정명 / nominal / tol+ / tol- / 측정값 / 판정. 가장 평면적·정렬·필터 용이, Phase 41 통계와 연결 자연.

- **D-06 (메타 배치 = 시트 상단 헤더 블록):** 시트 상단 몇 행에 모델명·검사일시·종합 OK/NG 표시, 그 아래 측정 테이블. 한 시트에서 모두 보임.

- **D-07 (이미지 연결 = 하이퍼링크):** 셀에 결과 이미지 파일 경로 하이퍼링크 → 클릭 시 외부 뷰어로 열림. xlsx 경량 유지, 구현 단순, 영속화된 결과 폴더(D-03) 경로 재사용. (대안 기각: 셀 임베드 = 파일 비대 + 삽입 복잡.)

### 리뷰어 UI (OUT-01)

- **D-08 (UI 위치 = 별도 창 Window):** 메뉴/버튼으로 여는 독립 리뷰어 Window. 라이브 검사 화면(MainView, display 전용 원칙) 방해 없이 과거 결과 탐색. 결합도 낮음.

- **D-09 (폴더 로드 UX = 날짜 폴더 → cycle 목록 → 선택):** `Ookii.Dialogs.Wpf` 폴더 다이얼로그(이미 참조됨)로 날짜 폴더 선택 → 그 안의 cycle 목록(시각·종합판정) 표시 → cycle 선택 시 이미지 + overlay + 측정표 재현. D-03 의 `ResultSavePath/{YYYYMMDD}/` 구조와 일치.

- **D-10 (xlsx 트리거 = 리뷰어 수동 [엑셀 export] 버튼):** 리뷰어에서 연 cycle 을 [엑셀 export] 버튼으로 xlsx 생성(영속화 JSON → ClosedXML). 명확·제어 용이. 저장 위치 = 해당 cycle 폴더 또는 사용자 지정. (검사 후 자동 생성은 범위 밖 — 필요 시 후속.)

### Claude's Discretion (research/plan 에 위임)

- overlay JSON 스키마 상세 (`EdgeInspectionOverlay` 어느 필드를 직렬화/복원하는지) — 재렌더 충실도 결정
- 검사 cycle 완료 시 JSON 저장 wiring 시점 (어느 Action/Sequence/SystemHandler 경로에서 cycle 결과를 모아 직렬화하는지)
- 리뷰어 재렌더 시 `HalconDisplayService` 재사용 방식 (라이브 경로와 동일 메서드 공유 여부)
- 에러/빈 결과/검출실패 cycle 의 리뷰어·xlsx 표현
- 결과 폴더 정리/보존 정책 (디스크 누적) — POC 범위에서는 필수 아님
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 요구사항 / 로드맵
- `.planning/ROADMAP.md` (Phase 40 섹션) — Goal/Scope/Out-of-scope/Success Criteria
- `.planning/REQUIREMENTS.md` §OUT-01/OUT-02 — 결과 리뷰어 + 1회 검사 엑셀 acceptance

### 결과 저장 / 경로 (D-03 연계)
- `WPF_Example/Setting/SystemSetting.cs:60-67,100` — `ImageSavePath`, `ResultSavePath`, `SaveFailImage`
- `WPF_Example/Utility/RawImageSaveService.cs:71-85` — 날짜 폴더 저장 패턴(`Raw/{YYYYMMDD}/`), 파일 naming, 비동기 큐
- `WPF_Example/Sequence/Sequence/SequenceBase.cs` (SaveResultImage) — 결과 이미지 저장 진입점 + `SaveFailImage` 가드

### 결과 데이터 모델 (D-01/D-05 연계)
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs:98-114` — `MeasuredValue`, `IsPass`, `LastOverlays`(`[JsonIgnore]`), `WasDatumSkipped`
- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs:43-58` — `LastMeasuredValue`, `LastJudgement`, `LastHasResult`, `LastSkipReason`, `NominalValue`, `TolerancePlus/Minus`, `MeasurementName`
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs:31-47` — `FAIList`, `ShotName`, `OwnerSequenceName`
- `WPF_Example/UI/ViewModel/MeasurementResultRow.cs:40-66` — 결과행 DTO (xlsx 행/리뷰어 그리드 참고)

### Overlay 재렌더 (D-01 재렌더 경로)
- `WPF_Example/Halcon/Models/EdgeInspectionOverlay.cs:22-48` — `RoiId`, `Points`, `LineRow1/Col1/Row2/Col2` (JSON 직렬화 대상)
- `WPF_Example/Halcon/Display/HalconDisplayService.cs:137-219` — overlay 렌더 루프(RoiId prefix 별 색상)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs:243-251` — `fai.LastOverlays` → `SetInspectionOverlays` 재렌더 패턴(node click)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:174-306` — 측정 시 `LastOverlays` 채우는 지점

### 라이브러리 / 의존성 (D-04 연계)
- `WPF_Example/packages.config` — 현재 의존성(스프레드시트 라이브러리 없음, `Ookii.Dialogs.Wpf`/`Newtonsoft.Json` 존재). ClosedXML + 전이 의존성 추가 대상
- `WPF_Example/App.config` — assembly binding redirect (ClosedXML 전이 의존성 추가 시 갱신 가능성)

**외부 ADR/스펙 없음** — 결정은 위 decisions 에 모두 캡처됨.
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Newtonsoft.Json` (참조됨) — cycle 결과 JSON 직렬화/역직렬화 (D-01)
- `Ookii.Dialogs.Wpf` (참조됨) — 리뷰어 날짜 폴더 선택 다이얼로그 (D-09)
- `SystemSetting.ResultSavePath`(`./Result`, 미사용) — cycle 결과 루트로 활용 (D-03)
- `HalconDisplayService.Render`(L137-219) — overlay 재렌더 (라이브 경로 재사용, D-01)
- `MeasurementResultRow` DTO — 리뷰어 그리드 + xlsx 행 매핑 참고 (D-05)
- `RawImageSaveService` 날짜 폴더 패턴 — 결과 폴더 구조 모델 (D-03)

### Established Patterns
- 날짜 기반 폴더 저장(`{path}/{YYYYMMDD}/{HHmmss...}`) — RawImageSaveService 패턴 답습
- `LastOverlays`/`LastMeasuredValue` 등 결과는 **메모리 전용**(`[JsonIgnore]`) — 영속화는 신규로 직렬화해야 함 (greenfield)
- packages.config(classic) NuGet — 전이 의존성 수동 관리 필요(ClosedXML 주의점)

### Integration Points
- **결과 저장 wiring**: cycle 완료 시점(어느 Sequence/Action/SystemHandler 경로)에서 전 Shot/FAI 결과 + overlay 를 모아 JSON 직렬화 — plan 에서 확정
- **리뷰어 진입**: MainWindow 메뉴/버튼 → 신규 Reviewer Window
- **xlsx 생성**: 리뷰어 export 버튼 → 영속화 JSON 로드 → ClosedXML 빌드

### Greenfield (기존 자산 없음)
- Excel/CSV export 코드 전무
- 결과 리뷰어/폴더 로드 UI 전무
- cycle 단위 결과 객체 전무 (신규 모델 필요)
- overlay 디스크 영속화 전무
</code_context>

<specifics>
## Specific Ideas

- 영속화 JSON 은 OUT-01·OUT-02 가 공유하는 **단일 소스** — 리뷰어 재렌더와 xlsx 가 같은 데이터를 읽는다(중복 모델 회피).
- xlsx 헤더 블록(D-06)의 종합판정은 D-02 의 OK/NG/검출실패 3분기(Phase 39 정책)와 일치시킨다.
- 리뷰어 cycle 목록은 시각 + 종합판정으로 빠르게 NG cycle 을 찾을 수 있게 한다(D-09).
</specifics>

<deferred>
## Deferred Ideas

- 50회 반복도 통계(mean/stddev/range/Cpk) — **Phase 41 (OUT-03)**
- 검출 알고리즘별 통계 분석표(TLI/CTH/VTH/Edge 등) — **Phase 41 (OUT-04)**
- 검사 후 자동 xlsx 생성 — Phase 40 은 리뷰어 수동 export 만(D-10). 필요 시 후속.
- 셀 임베드 썸네일 이미지 — D-07 하이퍼링크 채택, 임베드는 후속 옵션.
- cycle 메타에 작업자/TestId/시퀀스명 추가 — D-02 최소 세트, 추적성 강화 시 후속.
- 결과 폴더 보존/정리 정책(디스크 누적) — POC 범위 밖.
</deferred>

---

*Phase: 40-export-i-1-2026-06-01*
*Context gathered: 2026-06-01*
