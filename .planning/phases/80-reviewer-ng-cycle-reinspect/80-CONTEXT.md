# Phase 80: 리뷰어 NG 사이클 사진 한 번에 불러와 파라미터 수정·재검사 - Context

**Gathered:** 2026-09-18
**Status:** Ready for planning

**사용자 원문 (2026-09-18):** "결국은 리뷰어에서 NG 이미지로 확인된 거는 내가 파라미터 수정을 할 때 보다 쉽게 할 수 있게 해당 이미지를 열 수 있게 하는 게 관건" · "사용자가 쉽게 접근할 수 있도록 해야 함" · "UI 단순하고 쉽게 가야 함"

<domain>
## Phase Boundary

리뷰어에서 NG 행을 고르고 버튼 한 번을 누르면, 그 자재 검사에 쓰인 사진(모든 Shot 사진 + 기준점 사진 + Z 후보 사진)이 메인 화면에 들어온다. 이어서 기준점 Test Find 가 자동 실행되어, 사용자는 파라미터를 고치고 RUN 만 누르면 재검사 결과를 본다. 운영 레시피에는 리뷰어 사진 경로가 섞이지 않는다.

```
리뷰어: NG 행 선택 → 원인 1줄·근거·확인할 일 → [이 사진으로 파라미터 수정] (한 번 클릭)
   ↓ (리뷰어 창은 열린 채, 메인 화면이 앞으로)
메인 화면: 같은 자재의 모든 Shot 사진 + 기준점 사진(+Z 후보 사진) 적용
   ├─ NG 난 Shot + 측정(FAI) 선택, 파라미터 보이는 상태
   ├─ 기준점 Test Find 자동 실행 (확인창 없음)
   └─ 상태 줄: "리뷰어 사진 사용 중 — 09-18 17:38 자재 12 [JPG 안내] [해제]"
   ↓ 파라미터 수정 → RUN(직접) → 재검사 결과
   ↓ 레시피 저장 → 파라미터만 저장, 사진 경로는 원래 값
```

**함께 처리 (로드맵 확정):**
1. 리뷰어 사이클 전체 보기에서 여러 Shot 의 검출 선이 사진 한 장에 겹쳐 그려지는 버그 (`ReviewerWindow.xaml.cs:183`) — 표시된 사진에 속한 FAI 선만 그린다.
2. 수동 RUN 에서 기준 ROI(Local Ref)를 고친 뒤 Test Find 없이 RUN 만 눌러도 새 기준 ROI 로 국부 기준선을 다시 계산 (지금은 "설정이 바뀜" → 전역 전환). PLC 자동 검사는 영향 없음.
3. 기준 ROI 시험 찾기 (D-80-15).

**범위 밖:** 재검사 결과와 리뷰어 원래 값의 자동 비교 표시, 리뷰어에 ViewModel 전면 도입, 검사 알고리즘·판정·PLC 프로토콜 변경.
</domain>

<decisions>
## Implementation Decisions

### UI 원칙 (전 항목에 적용)
- **D-80-00:** **UI 는 단순하고 쉽게.** 새로 생기는 UI 는 ① 리뷰어 버튼 1개 ② 메인 화면 상태 줄 1줄(해제 버튼 포함) ③ 기준 ROI 시험 찾기 버튼 1개 뿐이다. 새 창·새 설정 항목·추가 확인창을 만들지 않는다. 문구는 쉬운 한국어 한 줄(사용자는 비전 초보, D-78-05 와 같은 원칙).

### 부르는 방법·화면 흐름
- **D-80-01:** 리뷰어에 버튼 **"이 사진으로 파라미터 수정"** 1개. 위치는 **NG 원인 설명(원인·근거·확인할 일) 바로 아래, 크게**. 더블클릭·오른쪽 클릭 메뉴는 만들지 않는다.
- **D-80-02:** 행이 선택되지 않았거나 불러올 수 없는 행이면 버튼은 비활성이고 **이유를 버튼 옆에 한 줄로** 표시한다.
- **D-80-03:** 누르면 **확인창 없이 바로** 불러온다(운영 레시피는 D-80-09 로 보호되므로 안전).
- **D-80-04:** 불러온 뒤 **리뷰어 창은 열어 둔 채** 메인 화면을 앞으로 가져온다. 다른 NG 행을 이어서 고를 수 있다.
- **D-80-05:** 메인 화면에서 **NG 난 Shot + 그 측정(FAI)을 선택**해 파라미터가 바로 보이게 한다.
- **D-80-06:** **Test Find 까지만 자동**, RUN 은 사용자가 직접 누른다. 자동 Test Find 는 현재의 "사진 출처 선택" 대화상자(`AskTestImageSource`)를 거치지 않는다.

### 불러올 사진 범위
- **D-80-07:** **같은 자재의 모든 Shot 사진 + 기준점 사진(가로·세로)** 을 불러온다. "같은 자재" 묶음은 반복검사의 저장 사이클 재검사(`SavedCycleRerunPlanner.GroupIntoParts` — 시간순, 기준점 z 틱에서 새 자재 시작)와 **같은 규칙**을 쓴다(검사마다 다른 방식을 만들지 않음).
- **D-80-08:** **Z 범위 Shot 은 z 후보 사진 전부** 를 불러와 Z 자동 선택까지 리뷰어와 같게 재현한다. 후보 사진이 없으면(저장 체크 기본 꺼짐) **리뷰어에서 선택된 z 한 장 + 상태 줄 안내**.
- **D-80-09:** 짝이 맞는 기준점 사진이 없으면(수동 검사 기록 — 기준점 사진은 PLC 자동 검사 때만 저장됨 — 또는 파일 없음) **알림 다이얼로그 후 Shot 사진만 불러온다.** 기준점 사진은 현재 것을 그대로 두고, Test Find 자동 실행은 하지 않는다.

### 운영 레시피 보호
- **D-80-10:** 불러온 상태에서 레시피를 저장하면 **파라미터는 저장, 사진 경로(SimulImagePath·TeachingImagePath·TeachingImagePath_Vertical·RerunZRangeImagePaths 등)는 불러오기 전 원래 값으로** 저장된다. 저장을 막지 않는다(고친 파라미터를 바로 저장해야 하므로).
- **D-80-11:** 원래 사진 경로로 되돌리는 때: ① 상태 줄의 **[해제] 버튼** ② **PLC 자동 검사가 오면 자동 해제** ③ **프로그램 종료·레시피 변경 시 자동 해제**. 해제해도 고친 파라미터는 그대로 유지된다. 다른 NG 행을 또 불러오면 앞의 불러오기를 대체한다(원래 값은 처음 것 유지).
- **D-80-12:** 불러온 동안 메인 화면에 **눈에 띄는 한 줄 상태 표시**: "리뷰어 사진 사용 중 — 날짜 시간 자재번호" + [해제] 버튼. 로그에도 남긴다.
- **D-80-13:** 재검사에 필요한 오프라인 모드 전환(`OfflineInspectMode`)은 불러오기 동안 켜고 해제 시 원래 값으로 되돌린다(반복검사와 같은 방식).

### JPG 차이 · 기준 ROI 시험 찾기
- **D-80-14:** 불러온 사진이 JPG 면 **상태 줄에 짧게 안내**: "JPG 사진 — 실제 검사값과 조금 다를 수 있음". 팝업 없음.
- **D-80-15:** **기준 ROI 시험 찾기를 이번 phase 에 포함.** 기준 ROI(Local Ref) 티칭 시 기준점 가로 사진(z1)을 배경으로 기준 ROI 상자를 보여 주고, 버튼 1개로 그 자리에서 띠 에지 라인을 찾아 선으로 보여 준다(Phase 79 UAT 의 "측정 사진에서는 에지가 안 보임" 불편 해결). D-79-05(기준 ROI 는 z1 기준점 가로 사진에서 찾는다)와 같은 사진·같은 찾기 로직을 쓴다.

### 코드 원칙 (사용자 재확인 2026-09-18 — 모든 plan·executor·서브에이전트 프롬프트에 그대로 명시)
- **D-80-16:** **CLAUDE.md 🔒 가독성 규칙 필수.** 삼항 `?:`·null 병합 `??`/`??=`·null 조건 `?.`/`?[]`·switch 식 금지, 한 줄 분기도 중괄호, 긴 조건은 이름 있는 bool 로 추출, 헝가리언 접두사, 매직넘버는 const, C# 7.2 문법만, HImage/HObject/HTuple 은 finally 에서 Dispose, 날짜 주석 금지. 신규·수정 파일 모두 CLAUDE.md 의 검증 grep 5종이 0 이어야 한다.
- **D-80-17:** **MVVM.** 불러오기·짝 맞추기·스냅샷/복원·상태 줄 문구는 서비스 + ViewModel 에 둔다. `ReviewerWindow.xaml.cs`·`MainView.xaml.cs` 에는 배선만(이벤트 → VM/서비스 호출 1줄). 버튼 활성·비활성 이유·상태 줄 문자열은 VM 에서 만들어 바인딩한다.

### 계획 단계 추가 결정 (사용자 2026-09-18, plan-phase 중)
- **D-80-18:** **새 .cs 파일 허용.** Phase 78·79 의 "신규 .cs 파일 금지"는 이번 phase 에 적용하지 않는다. 불러오기 서비스·ViewModel 을 새 파일로 분리하고 `WPF_Example/DatumMeasurement.csproj` 에 `<Compile Include>` 로 등록한다(기존 파일 비대화 방지, D-80-17 MVVM).
- **D-80-19:** **기준점 사진이 일부만 있으면 "없음"과 같게 처리한다.** NG 측정이 쓰는 기준점에 필요한 사진(단일 / 가로 H / 세로 V) 중 하나라도 없으면 D-80-09 경로(알림 후 Shot 사진만, 기준점 사진은 현재 것 유지, 자동 Test Find 안 함). 짝이 반만 맞는 상태로 재검사하지 않는다.

### Claude's Discretion
- 버튼·상태 줄의 정확한 문구·크기·색 (D-80-00 원칙 안에서)
- 리뷰어 → 메인 화면 연결 방식(Owner 경유 등). 단 새 로직은 code-behind 가 아닌 서비스/VM 쪽에 두고, 비대한 `MainView.xaml.cs` 에는 배선만(CLAUDE.md 규칙 4)
- 스냅샷/복원 구현을 `RepeatRunService` 와 공유할지 새 서비스로 뺄지
- Local Ref 재계산 트리거 방식(수동 RUN 시 설정 키 불일치면 현재 기준점 사진에서 재계산 등)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 프로젝트 규칙
- `CLAUDE.md` — 🔒 가독성 규칙(삼항·`??`·`?.`·switch 식 금지, 중괄호 필수, 헝가리언, HImage Dispose, 날짜 주석 금지), MVVM 규칙
- `.planning/ROADMAP.md` §Phase 80 — 목표·함께 처리 항목·확정 방향

### 선행 phase 결정
- `.planning/phases/78-reviewer-ng-cause-analysis/78-CONTEXT.md` — 리뷰어 실제 촬영 사진(D-78-07), NG 원인 표시(D-78-04/05), cycle.json 기록 항목(D-78-08)
- `.planning/phases/77-side-z-focus-select/77-CONTEXT.md` — Z 범위 자동 선택, 저장 사진 재검사 시 z 별 사진 사용(D-77-06)
- `.planning/phases/79-side-local-strip-datum/79-CONTEXT.md` — 국부 기준(Local Ref) 옵션, 기준 ROI 는 z1 기준점 가로 사진(D-79-05), 실패 시 전역 전환(D-79-06)

### 핵심 코드 (조사 결과)
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml(.cs)` — 행 구성 `DisplayCycle`, 사이클 전체 보기 선 겹침 버그 :176-183, NG 행 자동 선택 `ApplyRowFilter` :200
- `WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs` — `ReviewMeasurementRow`(OwnerShot/OwnerFai/Source), `ReviewerImagePathResolver` :146
- `WPF_Example/UI/ViewModel/CycleResultDto.cs` — `DatumImages` :57, `ZRangeImages` :63, `OriginImageFileName`(절대경로), `IsProtocolDriven`, `ZIndex`, `IndexNumber`
- `WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs` — `SavedCycleRerunPlanner.GroupIntoParts` :1326, `FillPartDatumPhotos` :1375, `ApplySavedCyclePart` :708, 스냅샷/복원 `BuildOverrideSnapshot` :455 / `RestoreSavedCycleOverrides` :1060, `IsSavedCycleRerunActive` :402, 종료 시 복원 :417
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — 틱 기준점 사진 기록 :527 / 틱 시작 초기화 :451, `HoldManualDatum` :1413, `TryComposeAlign` :3409, `_localRefLines` :88, `ComputeLocalRefLinesForDatum` :3645, `ClearDatumTransforms` :2963
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — `BtnTestFindDatum_Click` :4475, `AskTestImageSource` :4636, `LoadAndDisplay` :1605
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — `TryResolveLocalRef` :1975, 오프라인 Z 후보 `LoadOfflineZRangeCandidates` :2306, 기준점 사진 로드 `GrabOrLoadDatumImage` :1080
- `WPF_Example/Halcon/.../EdgeToLineDistanceMeasurement.cs` — `BuildLocalRefSettingsKey` :497, `LOCAL_REF_REASON_STALE` :34
- `WPF_Example/MainWindow.xaml.cs` — `SaveRecipe` :292(저장 차단 조건), `PopupView(EPageType.Reviewer)` :401
- `WPF_Example/Utility/CaptureImageSaveService.cs` — 저장 경로·파일명 규칙 :384-468

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`SavedCycleRerunPlanner`** (RepeatRunService): 같은 자재 묶기 + 기준점 사진을 이름·역할(단일/H/V)로 짝지어 `TeachingImagePath(_Vertical)` 에, Shot 사진을 `SimulImagePath` 에 채우는 로직이 이미 있음 → D-80-07 의 핵심 재사용 대상.
- **RepeatRunService 스냅샷/복원 + OfflineInspectMode 켜기/되돌리기 + 종료 시 복원** → D-80-10/11/13 의 선례.
- **`ShotConfig.RerunZRangeImagePaths`** → D-80-08 Z 후보 사진 주입 경로.
- **`ReviewerImagePathResolver`** → 선 겹침 버그 수정 시 "표시 사진 ↔ FAI" 짝 판단에 사용.

### Established Patterns
- 기준점 사진은 **기준점 z 틱의 cycle.json 에만** 기록됨(틱 시작마다 초기화). 고른 행의 cycle.json 에는 없을 수 있으므로 **같은 폴더(날짜)의 앞쪽 사이클을 거슬러** 찾아야 함.
- 기준점 사진은 **PLC 자동 검사 + 라이브 카메라일 때만** 저장 → 수동 검사 기록은 D-80-09 경로로 감.
- 사이클 ID 없음 — `InspectionTime` 순서 + 기준점 z 로 자재를 구분.
- 리뷰어는 ViewModel 없이 code-behind 로 되어 있음 → 새 로직은 서비스/VM 으로 분리하고 code-behind 는 배선만.

### Integration Points / 주의점
- Test Find 비패턴 기준점 경로(`DatumFindingService.TryFindDatum` 직접 호출)는 시퀀스 기준점 캐시·국부 기준선을 채우지 않음 → 자동 Test Find 후 RUN 이 옛 캐시를 쓰지 않도록 확인 필요 (함께 처리 2번과 연결).
- `SaveRecipe` 경로에 "사진 경로는 원래 값으로" 저장 처리 추가 필요 (D-80-10). 현재는 사진 경로가 그대로 레시피에 들어감.
- PLC 자동 검사 수신·레시피 변경·종료 지점에 자동 해제 연결 (D-80-11).

</code_context>

<specifics>
## Specific Ideas

- 버튼 문구 예: "이 사진으로 파라미터 수정"
- 상태 줄 예: "리뷰어 사진 사용 중 — 09-18 17:38 자재 12  (JPG 사진 — 실제 검사값과 조금 다를 수 있음)  [해제]"
- 사용자가 전에 1738·1743 폴더에서 기준점 사진 짝을 손으로 찾았던 일을 프로그램이 대신하는 것이 핵심 가치.
- 불러온 사진·선은 리뷰어에서 본 것과 똑같아야 한다(로드맵 확정 방향).
- **함께 처리 2 실사례 (2026-09-18 17:21~17:29, SIDE_1 C13_P3):** 17:21:34 Test Find 후 기준 ROI 를 row 6510 · col 12800 으로 수정 → 17:24~17:27 수동 RUN 9회 전부 cycle.json `RefSource="LocalFallback"`(측정값 2.1859 고정) → 17:29:12 Test Find 재실행 후 `RefSource="Local"`(2.1545). 사용자가 "국부 왜 안 돼지?"로 문의 — 이 phase 의 Local Ref 재계산이 사용자 체감 문제임을 확인.

</specifics>

<deferred>
## Deferred Ideas

- 재검사 결과를 리뷰어 원래 값과 나란히 비교 표시 — 필요하면 다음 phase.

### Reviewed Todos (not folded)
- `2026-05-28-datum-angle-param-ui-cleanup.md` (Datum 각도 파라미터 UI 정리) — 키워드 "datum" 만 겹침(점수 0.2), 이 phase 범위와 무관.

</deferred>

---

*Phase: 80-reviewer-ng-cycle-reinspect*
*Context gathered: 2026-09-18*
