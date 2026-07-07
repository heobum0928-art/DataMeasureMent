# Phase 67: 양산 이력 통계 분석 (STAT-01) - Context

**Gathered:** 2026-07-07
**Status:** Ready for planning

<domain>
## Phase Boundary

실제 양산 검사(`$TEST` TCP / 수동) 완료 결과를 **일자별 CSV 로 지속 누적**하고, 신규 통계 화면에서 **기간·레시피로 조회**하여 측정 항목별 통계(N/Mean/StdDev/Range/Cpk/OK/NG/검출실패/불량률) 테이블 + 분포 히스토그램(공차선) + 시간별 측정값 추이 차트를 확인한다.

**In scope:**
- 검사 완료 시 측정 항목당 1행 CSV append (수집 계층)
- CSV 기간 조회 + 기존 `RepeatMeasurementStats` 재사용 통계 집계
- 통계 조회 UI (테이블 + 히스토그램 + 추이 차트)

**Out of scope (다른 phase 또는 deferred):**
- SPC 관리도(X-bar/R chart) — 다음 단계
- 통계 결과 Excel export (기존 RepeatExcelExportService 와 별개)
- 실시간 스트리밍/대시보드, 알람(Cpk 임계 경보)
- SQLite/LiteDB 등 신규 DB 도입 (CSV 로 확정)

</domain>

<decisions>
## Implementation Decisions

### CSV 저장 위치·파일 단위
- **D-01:** 신규 `StatisticsSavePath` Setting 추가 — `SystemSetting` 에 `[DirectoryPath][AutoUpdateText]` 프로퍼티, 기본값 `AppDomain.CurrentDomain.BaseDirectory + @"Statistics"`. 기존 `ResultSavePath`(`.../Result`) 와 동일 패턴. 검사 결과와 분리되어 관리·백업 용이. 설정 창(PropertyGrid)에서 경로 변경 가능.
- **D-02:** 일자별 1파일 = `StatisticsSavePath\yyyyMMdd.csv`. 모든 레시피 혼합 저장, 레시피는 CSV 컬럼(RecipeName)으로 구분. 파일 수 최소화 + 기간 조회 단순(날짜 범위 파일만 읽기).
- **D-03:** **고정 컬럼 스키마 + 키(Shot/FAI/측정명) 식별**. 레시피 변경/측정항목 증감이 있어도 행 단위 누적이라 무관. 조회 시 Shot/FAI/측정명 키로 그룹핑. 컬럼(측정 항목당 1행):
  `검사일시(yyyy-MM-dd HH:mm:ss)` / `RecipeName` / `IndexNumber(자재번호)` / `ShotName` / `FAIName` / `MeasurementName` / `TypeName` / `NominalValue` / `TolerancePlus` / `ToleranceMinus` / `MeasuredValue` / `Judgement(OK|NG|DATUM_FAIL|NO_IMAGE|...)` / `HasResult(bool)` / `OverallCycleResult(P|F|N)`
  - CSV 필드 이스케이프 필수 — 측정명·타입명에 콤마 가능성 → 표준 CSV 따옴표 처리(RFC4180 스타일).

### 수집 훅 (Collection)
- **D-04:** 수집 훅 위치 = `CycleResultSerializer.SaveAsync` 내부(이미 비동기 `Task.Factory.StartNew` 스레드). v2.6(`AddResponse` line 174) / v1.0(`PersistAndEnqueueV1` line 712) / 수동(`HandleManualCyclePersist` line 199) **3경로 모두 SaveAsync 로 수렴** → 여기 한 곳만 훅하면 전 경로 자동 커버. cycle.json 쓰기와 동일 try/catch 격리(검사/TCP 응답 무영향).
- **D-05:** 동시 쓰기 보호 — 신규 CSV writer 에 static `lock` 오브젝트. 프로세스 내 복수 InspectionSequence 스레드가 근접 완료 시 append 경합 방지(파일 append 는 단일 lock 구간).

### 집계·판정 정책
- **D-06:** 검출실패(`DATUM_FAIL`/`NO_IMAGE`)는 **별도 DetectFail 칼럼으로 분리**. 불량률 = `NG / (OK + NG)` (측정된 것만 분모). 검출실패는 DetectFailCount 로 별도 집계·표시. → 장비 미안착 이슈가 품질 불량률과 섞이지 않음. 기존 `RepeatMeasurementStats.AddSample` 정책과 동일.
- **D-07:** Cpk 계산 모집단 = **측정값 있는 것만**(OK/NG 불문, 검출실패·NO_IMAGE 제외). N≥1 이면 평균, N≥2 이면 stddev, `Cpk = min((USL-mean)/(3σ), (mean-LSL)/(3σ))`, σ=0 이면 PositiveInfinity. 기존 `RepeatMeasurementStats.ComputeAll` 로직 그대로 재사용(DRY).

### 통계 화면 진입·구조
- **D-08:** **`ReviewerWindow` 미러링** — 신규 `StatisticsWindow`(비모달 `Show()`, MainWindow 멤버 `mStatisticsWindow` 로 재사용, 이미 열려있으면 포커스). 라이브 검사(MainView) 방해 안 함. 기존 Phase 40 OUT-01 D-08 패턴과 동일.
- **D-09:** 메뉴 진입점 = `EPageType.Statistics` 신규 enum 값(`Reviewer` 옆에 추가). 메뉴/버튼 라벨 "통계분석". `OpenPage`(또는 상응 스위치)에서 Reviewer 와 동일한 비모달 Show 분기.

### 조회 필터·차트 상호작용
- **D-10:** 기간 기본값 = **오늘 하루**(from=to=today). 화면 오픈 시 오늘 CSV 1개만 읽어 빠름. 사용자가 기간 넓히면 해당 범위 파일 로드.
- **D-11:** 필터 = 기간(from~to DatePicker) + 레시피 드롭다운(로드된 CSV 에서 distinct RecipeName 채움) + [조회] 버튼.
- **D-12:** 차트 항목 선택 = **테이블 행 클릭 → 해당 Shot/FAI/측정명 항목**의 히스토그램+추이 갱신. 별도 콤보박스 없이 DataGrid SelectionChanged 로 구동.
- **D-13:** 추이 차트 x축 = **측정 순서(샘플 인덱스 1,2,3...)**. 샘플 간격 균등 → SPC 추세 판독 용이. 공차 상·하한선(USL/LSL) + 평균선 오버레이.
- **D-14:** 히스토그램 = 분포 막대 + 공차 상·하한선(USL/LSL) 수직선. bin 개수는 plan 단계 Claude 재량(예: sqrt(N) 또는 고정 20 — 데이터량 따라).

### Claude's Discretion
- CSV writer 클래스 이름/위치(예: `ProductionHistoryLogger` 또는 `MeasurementHistoryCsvWriter`, `WPF_Example/Custom/Sequence/Inspection/` 또는 `Utility/`).
- 히스토그램 bin 개수 산정 방식.
- 통계 창 레이아웃(테이블 상단/차트 하단 vs 좌우 분할) — ReviewerWindow 스타일 참고.
- 대용량 기간 조회 시 성능 가드(예: 최대 일수 경고) 필요 여부 — plan 에서 판단.
- CSV 파싱/집계 시 기존 `RepeatMeasurementStats` 재사용 방식(플랫 행 → 최소 CycleResultDto 재구성 후 AddSample, 또는 얇은 어댑터).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 데이터 흐름 (수집 훅 대상)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — `AddResponse`(v2.6, line 95~182, SaveAsync line 174) / `AddResponseV1Cycle`(v1.0, line 493~) + `PersistAndEnqueueV1`(line 695~718, SaveAsync line 712) / `HandleManualCyclePersist`(수동, line 187~205, SaveAsync line 199). 3경로 모두 SaveAsync 로 수렴 = 단일 훅 포인트.
- `WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs` — `BuildDto`(line 35~156, recipeManager→CycleResultDto 스냅샷) / `SaveAsync`(line 162~200, 비동기 파일 쓰기 = **CSV append 훅 삽입 지점**) / `Load`.
- `WPF_Example/UI/ViewModel/CycleResultDto.cs` — DTO 계층: `CycleResultDto`(InspectionTime/RecipeName/IndexNumber/OverallJudgement/Shots) → `ShotResultDto`(ShotName/OwnerSequenceName/FAIs) → `FaiResultDto`(FAIName/IsPass/WasDatumSkipped/Measurements) → `MeasurementResultDto`(MeasurementName/TypeName/NominalValue/TolerancePlus/Minus/LastMeasuredValue/LastJudgement/LastHasResult/LastSkipReason). CSV 행 생성 소스.

### 통계 로직 재사용
- `WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs` — `MeasurementStat`(N/Mean/StdDev/Range/Cpk/OkCount/NgCount/DetectFailCount) + `RepeatMeasurementStats.AddSample(CycleResultDto)`/`ComputeAll()`. Cpk·불량률 집계 **그대로 재사용**. skip 정책(DATUM_FAIL/NO_IMAGE→DetectFail) 동일.
- `WPF_Example/Custom/Export/RepeatExcelExportService.cs` — 통계 계산 호출 패턴 참고(AddSample 루프 → ComputeAll → 렌더). 알고리즘 카테고리 집계 참고용.

### UI 진입·구조 참조
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml` / `ReviewerWindow.xaml.cs` — 신규 StatisticsWindow 미러링 대상(비모달 결과 조회 창).
- `WPF_Example/MainWindow.xaml.cs` — `EPageType`(line 27~31, Reviewer 추가 지점) + `OpenPage` 스위치(line 355~363, Reviewer 비모달 Show + 멤버 재사용 패턴). `mReviewerWindow` 멤버(line 70).

### 설정
- `WPF_Example/Setting/SystemSetting.cs` — `ResultSavePath`(line 66) 등 `[DirectoryPath][AutoUpdateText]` 패턴 = 신규 `StatisticsSavePath` 추가 참조.
- `WPF_Example/Custom/SystemSetting.cs` — custom partial(추가 필요시).

### 차트 라이브러리
- `ChartDirector.Net` / `ChartDirector.Net.Desktop.Controls` v7.1.0 (packages.config) — 히스토그램·추이 차트. wafer map 뷰에서 사용 이력 있음(참고 코드 탐색 필요).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `RepeatMeasurementStats` (Cpk/Mean/StdDev/Range/불량률 집계) — 통계 계산 그대로 재사용. 플랫 CSV 행 → CycleResultDto 재구성 후 AddSample, 또는 얇은 어댑터로 연결.
- `ReviewerWindow` (비모달 결과 조회 창) — StatisticsWindow 구조/오픈 패턴 템플릿.
- `CycleResultDto` 계층 — CSV 행 필드 소스(측정 항목당 1행 평탄화).
- `SystemSetting` `[DirectoryPath]` 패턴 — StatisticsSavePath 추가.
- `ChartDirector.Net` — 프로젝트 기존 차트 의존성(신규 도입 불필요).

### Established Patterns
- 결과 영속화 = `SaveAsync` 비동기 fire-and-forget + try/catch 격리(검사 스레드 무영향). CSV append 도 동일 스레드/패턴 재사용.
- 비모달 창 = `EPageType` enum + `OpenPage` 스위치 + 멤버 재사용(`IsLoaded` 체크 후 Show).
- Setting = PropertyGrid([Category]/[DirectoryPath]/[AutoUpdateText]) 자동 노출.

### Integration Points
- CSV 수집: `CycleResultSerializer.SaveAsync` 내부(단일 지점, 전 경로 커버).
- 메뉴: `MainWindow.EPageType.Statistics` + OpenPage 분기.
- 설정: `SystemSetting.StatisticsSavePath`.
- 통계 계산: `RepeatMeasurementStats` 재사용.

</code_context>

<specifics>
## Specific Ideas

- 앞선 대화(2026-07-07)에서 사용자가 "양산 이력 누적"을 명시적으로 선택(세션 반복측정이 아닌 실 생산 데이터 지속 기록). 초 단위 검사 주기 → CSV 로 충분, SQLite 과임.
- 차트는 실용 기본 세트(테이블+히스토그램+추이)로 시작, 관리도(X-bar)는 다음 단계로 명시적 이연.
- 헝가리언 표기법 + if/else only + 삼항(`?:`)·null 병합(`??`) 금지 + 함수 30줄/단일책임 + 매직넘버 상수화 — 프로젝트 CODE-RULES 엄수(하위 에이전트 프롬프트에 매번 명시).
- 수정/추가 라인에 `//260707 hbk` 주석.

</specifics>

<deferred>
## Deferred Ideas

- **SPC 관리도(X-bar/R chart)** — 통계 화면 다음 확장. 이번 phase 는 히스토그램+추이까지.
- **통계 결과 Excel export** — 필요시 별도 phase(기존 RepeatExcelExportService 확장 or 신규).
- **Cpk 임계 경보/알람** — 공정능력 저하 실시간 경고. 향후.
- **세션 반복측정(BatchRunService) 결과 UI 화면화** — 애초 "①" 옵션(현재 Excel-only). 사용자가 "②(양산 이력)" 선택 → 이연. 추후 원하면 별도 phase.

</deferred>

---

*Phase: 67-stat-01-2026-07-07*
*Context gathered: 2026-07-07*
