# Phase 72: CPK 데이터 리포트 출력 재설계 - Context

**Gathered:** 2026-08-18
**Status:** Ready for planning

<domain>
## Phase Boundary

고객/외주업체 참고 리포트 파일(`Rapid City A8.1_Z Stopper_ Data Report_R04_260623_AOI 국산화 개발.xlsx`)과 대조하여 확인된 5개 갭을 메운다:
1. 가로형(wide) RAW DATA 매트릭스 시트 신설 (1행=FAI 항목, 열=반복회차)
2. Cpk 엑셀 export 복원 (Phase 51에서 제거된 것을 되살림 — `RepeatMeasurementStats.cs`의 기존 계산 로직 재사용)
3. Cp 신규 계산·export 추가 (지금 코드베이스 어디에도 없음, 신규 구현)
4. USL/LSL 명시적 컬럼화
5. 그래프 export 지원

기존 export 경로(`ExcelExportService`, `RepeatExcelExportService`의 현 Sheet1/2/3)의 기존 동작·기존 컬럼 위치는 깨지 않는다 — 추가 시트/컬럼 방식으로 확장. 판정 로직(P/F/B, `RepeatMeasurementStats`의 계산식)은 변경 대상이 아니다 — export 표현만 재설계.

</domain>

<decisions>
## Implementation Decisions

### D-01. 그래프 — 이미지 삽입 방식 채택
- ClosedXML 0.105.0에는 네이티브 엑셀 차트 API가 없음(`AddChart`/`IXLChart` 전무, `AddPicture`만 가능) — 라이브러리 교체는 다른 export 코드 전체에 영향을 주므로 이번 Phase에서는 하지 않는다.
- `WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs`의 기존 `RenderHistogram()`/`RenderTrend()`(WPF Canvas 렌더, USL/LSL/평균 기준선 포함, BIN_COUNT=20)를 재활용한다.
- 방식: 해당 Canvas를 `RenderTargetBitmap`으로 캡처 → PNG 바이트로 인코딩 → `IXLWorksheet.AddPicture()`로 엑셀 시트에 삽입. 참고파일도 실제로는 네이티브 차트가 아니라 EMF/PNG 정지 이미지 붙여넣기였음(zip 내부 검증 완료: `xl/charts/` 0개, `xl/media/`에 emf+png만 존재) — 방식 자체가 참고파일과 일치.
- 헤드리스(비HW) 환경에서 Canvas 렌더가 필요하므로, Phase 40.2에서 이미 해결한 "헤드리스 HALCON 버퍼윈도우 dump" 패턴(`.planning/phases/40-2-*`)을 참고할 것 — WPF Canvas는 HALCON 윈도우와 다르지만 헤드리스 렌더 자체의 선례가 있음.

### D-02. 엑셀 수식 vs 고정값 — 고정값 채택
- Max/Min/Mean/StdDev/Cp/Cpk/USL/LSL 전부 C#에서 계산한 뒤 `.Value`로 기록한다. 엑셀 라이브 수식(`FormulaA1`)은 쓰지 않는다.
- 근거: 기존 export 코드(`ExcelExportService.cs`, `RepeatExcelExportService.cs`) 전체가 이미 `.Value`만 쓰는 일관된 관행이며, 이번 Phase만 수식 방식으로 바꾸면 유지보수 일관성이 깨진다. `RepeatMeasurementStats.cs`의 계산 로직이 이미 검증되어 있으므로 그 결과를 그대로 쓰는 것이 리스크도 낮다.

### D-03. 100회+ 반복 검증 데이터 — RepeatRunService 폴더 반복 재사용
- Phase 41.1이 "검증용 반복 이미지 부족"으로 DEFERRED됐던 것과 같은 문제를 이번엔 `WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs`의 `StartFromImages(seq, imagePaths)`(quick-260615-dx7, 폴더-이미지 자동 반복 검사 모드)로 해결한다. 실기 없이도 폴더에 이미지를 여러 장 준비해 100회 이상 자동 순회 가능.
- UAT 단계에서 실제 100회 이상 데이터를 이 방식으로 확보할 것 — 신규 인프라 구현 불필요, 기존 기능 재사용.

### D-04 (REVISED 260818). 시트 2장 고정 + 자재번호는 "열", 시트가 아님

**⚠ 최초 D-04는 틀렸음 — 참고파일 실데이터를 다시 세어 정정함. 아래가 확정본이다.**

**정정 근거 (openpyxl 재검증, 260818):**
- 참고파일 6개 시트 중 **4개가 숨김(hidden)** 상태 — 실제로 사용자에게 보이는 건 `RAW DATA(1)`과 `1Cav 세부치수_Cpk` **2장뿐**이다.
  | 시트 | 상태 | 실데이터 |
  |---|---|---|
  | `RAW DATA(1)` | visible | 189 FAI행, 샘플열 `#1`,`#2`,`#3` 3개만 채워짐 |
  | `1Cav 세부치수_Cpk` | visible | 163 항목, `135 OK / 163` |
  | `RAW DATA(2)` | hidden | **완전히 빔** (샘플열 0개) |
  | `2Cav 세부치수_Cpk` | hidden | `0 OK / 0` — 빔 |
  | `검사성적서` | hidden | 4개 셀만 발췌한 수기 계산 |
  | `Data Report 안내사항` | hidden | 가이드 |
- `#1`~`#32` 헤더는 **양식의 빈 칸 용량**일 뿐 — 실제 데이터는 `#1`,`#2`,`#3` 3개.
- 사용자 확정 해석: `#1`/`#2`/`#3` = **서로 다른 자재 3개를 각각 1번씩 측정한 것**. 같은 부품의 3회 반복이 아니다. 즉 Cpk가 "자재 간 산포"로 계산되는 정상적인 공정능력지수 의미와 일치한다.

**따라서 축 매핑 확정:**
- **행** = FAI 측정 항목 (A1_P1, A1_P2, ...)
- **열(`#1`,`#2`,`#3`...)** = 자재 1개당 1열. 자재번호는 **열 축**이지 시트 축이 아니다.
- **시트** = 참고파일에선 Cavity(금형 캐비티) 축이었으나, 실제로는 1Cavity만 사용됨. 우리 시스템에 Cavity 개념이 없으므로(코드 전수검색 `Cavity`/`Cav[0-9]` 매치 0건) **시트는 1세트 고정**.

**확정 산출물 — 시트 2장만 생성 (사용자 선택: "보이는 2장만"):**
1. `RAW DATA(1)` — 1행=FAI 항목, `#1`부터 오른쪽으로 자재 1개씩. 헤더: `Number(FAI명)/도면항목설명/측정방식/설계값/상한공차/하한공차/#1~#N`.
2. `1Cav 세부치수_Cpk` — 1행=FAI 항목. 좌측(B~L): `SPC/FAI#/측정방식/Datum유형/공차유형/기준치수/+공차/-공차/검사방법/USL/LSL`. 우측(N~V): `Maximum/Minimum/Mean/#1 Target StdDev/Std Dev/Cp/UCPK/LCPK/Cpk`. X열 판정. 상단 `OK/Total`·`NG/Total`·`NG FAI# 항목`.
- 숨겨진 4개 시트(`검사성적서`, `2Cav`, `RAW DATA(2)`, `안내사항`)는 **만들지 않는다**. 특히 `검사성적서`의 "4개 셀 발췌" 규칙은 일반화 불가능한 수기 작업이므로 재현 시도 자체를 하지 않는다.

### D-05 (신규 260818). 폴더 반복검사에 자재번호 입력 추가
- 문제: `RepeatRunService`/`BatchRunService`가 결과 DTO의 자재번호(`IndexNumber`)를 항상 -1로 남긴다 — 자재번호는 TCP `$TEST` 경로로 요청이 올 때만 채워진다(코드 확인: `BuildDto()` 호출 시 `nIndexNumber` 인자 미전달). 따라서 오프라인 폴더 반복만으로는 "자재별 열 분리"를 검증할 수 없다.
- 결정(사용자 선택: "추가한다"): 폴더 반복검사 시작 시 **자재번호를 지정할 수 있는 입력 경로를 추가**한다. 자재 1번으로 N장, 자재 2번으로 N장 돌리면 RAW DATA에 열이 2개 생기는지를 사무실에서 검증 가능하게 한다.
- 자재번호가 지정되지 않은(-1) 결과는 단일 열로 취급(폴백).

### Claude's Discretion
- RAW DATA 시트와 Cpk 상세 시트를 하나의 export 흐름(신규 서비스 또는 `RepeatExcelExportService` 확장) 중 어느 쪽으로 구현할지의 정확한 클래스/메서드 배치.
- PNG 이미지 삽입 시 셀 앵커 위치, 이미지 크기·해상도의 구체적 값.
- D-05 자재번호 입력 UI의 정확한 위치·컨트롤 형태(기존 반복검사 시작 경로에 자연스럽게 붙이는 선에서).
- 판정 등급(OK/NG/Cpk경고) 임계값은 참고파일 수식 그대로: `min<LSL 또는 max>USL → NG`, `Cpk<1.33 → Cpk(경고)`, `그 외 → OK`.

</decisions>

<specifics>
## Specific Ideas

- 참고파일 실측 구조(openpyxl 직접 분석, 260818): 6개 시트 존재하나 **4개는 숨김이고 실데이터도 없음** — 실질 산출물은 `RAW DATA(1)` + `1Cav 세부치수_Cpk` 2장. 상세는 D-04 표 참조.
- Cpk 상세 시트 수식(참고파일 그대로 이식할 계산 공식):
  - `N=MAX(...)`, `O=MIN(...)`, `P(Mean)=AVERAGE(...)`, `R(StdDev)=STDEV(...)`
  - `S(Cp) = (Tol+ + ABS(Tol-)) / (6 × StdDev)`
  - `T(UCPK) = (USL - Mean) / (3 × StdDev)`
  - `U(LCPK) = (Mean - LSL) / (3 × StdDev)`
  - `V(Cpk) = MIN(UCPK, LCPK)`
  - 판정(X열): `min<LSL 또는 max>USL → "NG"`, 아니고 `Cpk<1.33 → "Cpk"`(경고), 아니면 `"OK"`
  - 상단 요약: `OK/Total`, `NG/Total`, `NG FAI# 항목 리스트`
- `검사성적서` 시트는 `1Cav_Cpk`와 거의 동일한 좌측 컬럼(SPC~LSL, N~S Max/Min/Mean/StdDev/Cp)이지만 자재 통합 합계로 추정 — 자재번호 축을 합친 전체 요약 시트로 볼 수 있음(추가 재확인 필요, 연구 단계에서 명확화).

</specifics>

<canonical_refs>
## Canonical References

### 참고 리포트 파일 (외부, 읽기전용)
- `C:\Info\Doc\2.디팜스테크\12_Data\Rapid City A8.1_Z Stopper_ Data Report_R04_260623_AOI 국산화 개발.xlsx` — 이번 Phase가 대조하는 목표 포맷. 시트/컬럼/수식 구조의 1차 소스.

### 과거 관련 Phase 산출물
- `.planning/phases/51-export-2026-06-16-poc-3/51-02-SUMMARY.md:21` — CPK/StdDev/Range를 엑셀에서 제거했던 근거 커밋(이번 Phase는 이걸 복원하는 성격, 파괴적 변경 아님)
- `.planning/ROADMAP.md` Phase 41.1 항목 — "⏸ DEFERRED 2026-06-16 (검증용 반복 이미지 부족)", 가로형 원본 매트릭스 개념이 이 Phase에서 처음 계획되었다가 실행 0건으로 보류된 것을 이번 Phase가 재개
- ~~`.planning/phases/40-2-*` — 헤드리스 HALCON 버퍼윈도우 dump 선례~~ **무효**: 연구 결과 `OverlayCaptureRenderer.cs` 주석 확인 — 그 window-dump 방식은 성능 문제로 2026-08-10에 채널 분해 픽셀 페인팅으로 완전 교체됨. WPF Canvas 캡처는 별개 기술이라 참고 가치 없음.
- `.planning/phases/72-.../72-RESEARCH.md` — 이번 Phase 연구 산출물. `## User Constraints` / `## Architecture Patterns`(Pattern 1~5) / `## Common Pitfalls` 우선 참조.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs` — `AddSample(CycleResultDto)`/`ComputeAll()` → `N/Mean/StdDev/Range/Cpk/NominalValue/TolerancePlus/ToleranceMinus/OkCount/NgCount/DetectFailCount`. Cpk 계산 이미 존재·검증됨: `usl=Nominal+TolPlus`, `lsl=Nominal-|TolMinus|`, `cpk=min(cpkUpper,cpkLower)`. **Cp는 여기에도 없음 — 신규 추가 필요.**
- `WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs` (Phase 67/STAT-01) — `RenderHistogram()`/`RenderTrend()`, USL/LSL/평균 기준선 포함 Canvas 렌더. 그래프 export의 기반.
- `WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs` — `StartFromImages(seq, imagePaths)`, 폴더-이미지 자동 반복 검사. 100회+ UAT 데이터 확보 수단.
- `WPF_Example/Custom/Export/RepeatExcelExportService.cs` — `Export()`/`ExportBatch()`, ClosedXML 사용 패턴(시트 추가, 헤더 스타일 등)의 기존 관례.

### Established Patterns
- 모든 기존 export 코드는 `.Value`만 사용, `FormulaA1` 미사용 — 이번 Phase도 이 관례를 따름(D-02).
- `CycleResultDto`가 반복/배치 실행 전반의 결과 운반 단위 — Cpk 통계·RAW DATA 매트릭스 모두 이 DTO 컬렉션에서 파생되어야 함.

### Integration Points
- 자재번호 = `CycleResultDto.IndexNumber`. **연구 확인: `RepeatRunService`/`BatchRunService`는 `BuildDto()` 호출 시 `nIndexNumber` 인자를 넘기지 않아 항상 -1이 된다** — TCP `$TEST` 경로만 실제 값을 채운다. D-05가 이 갭을 메운다.
- RAW DATA의 열 축(`#1`,`#2`,...)이 이 `IndexNumber` 값으로 결정된다(D-04 개정본).

</code_context>

<deferred>
## Deferred Ideas

- ClosedXML을 차트 지원 라이브러리로 교체 — 리스크가 크고 다른 export 경로 전체에 영향을 주므로 이번 Phase 범위 밖. 이미지 삽입 방식(D-01)으로 대체.
- 엑셀 라이브 수식(FormulaA1) 방식 — 이번엔 고정값(D-02)으로, 추후 필요성이 생기면 별도 Phase.
- `검사성적서` 시트 — 재현하지 않기로 확정(D-04). 참고파일에서 숨김 상태이며 4개 셀만 발췌한 수기 계산이라 일반화 불가능. 고객이 명시적으로 요구하면 그때 별도 Phase.
- `2Cav`/`RAW DATA(2)` 등 Cavity 축 다중 시트 — 참고파일에서 빈 템플릿이었고 우리 시스템에 Cavity 개념 자체가 없음. 향후 금형 다캐비티 대응이 실제로 필요해지면 별도 Phase.
- `Data Report 안내사항` 가이드 시트 — 정적 문서라 export 자동화 대상 아님.

</deferred>

---

*Phase: 72-cpk-rapid-city-a8-1-z-stopper-data-report-r04-raw-data-cpk-e*
*Context gathered: 2026-08-18*
