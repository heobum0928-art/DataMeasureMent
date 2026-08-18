# Quick Task 260818-fqx: 운영자 매뉴얼에 "티칭" 챕터 추가 - Context

**Gathered:** 2026-08-18
**Status:** Ready for planning

<domain>
## Task Boundary

기존 운영자 매뉴얼(quick task 260818-el1 산출물: `Document/Manual/DataMeasurement_Operator_Manual_v1.0.md` / `.docx`)에 "티칭(Teaching)" 챕터를 새로 추가한다. 새 문서를 만들지 않고 같은 원고/문서를 확장(9장으로 추가, 부록 앞에 배치)한다. 코드 변경은 절대 금지.

</domain>

<decisions>
## Implementation Decisions

### 이 챕터가 필요해진 이유
- 260818-el1은 티칭을 의도적으로 "관리자 전용, 조작법 다루지 않음"으로 범위 밖 처리했다.
- 사용자가 실사용 관점에서 "티칭을 알려줘야지 화면 없이 어떻게 하나"라고 피드백 — 이 매뉴얼을 실제로 쓰는 사람(관리자/엔지니어 겸임 포함)에게는 티칭 절차가 어딘가에는 있어야 한다.
- 사용자가 지금 실제로 Datum + 패턴(ModelFinder) + Align 티칭을 전부 처음부터("전부 새로") 재작업 중.

### 문서 구성
- 별도 문서를 새로 만들지 않고, 같은 매뉴얼 안에 "관리자/엔지니어 전용" 챕터로 추가한다(오케스트레이터가 제안, 사용자 이견 없음 — 확정).
- 새 챕터는 기존 8장 뒤, 부록 A/B 앞에 9장으로 배치.

### 범위: 티칭 종류
- **Datum 티칭 + 패턴(ModelFinder) 티칭 + Align 티칭 — 전부** 실제 절차 수준으로 다룬다(사용자 답변: "전부 새로"). 하나만 깊고 나머지는 개요로 넘기는 방식이 아니다.
- 공통 절차(로그인, 왜 관리자 권한이 필요한지)는 공통 절로 한 번만 쓰고, 이후 3개 절로 종류별 세부 절차를 나눈다.

### 스크린샷 처리 (기존 260818-el1과 동일 방침 유지)
- 이번에도 Claude가 앱을 빌드/실행해서 캡처하지 않는다(실카메라·조명 컨트롤러 보호, 260818-el1 CONTEXT.md와 동일 사유).
- 자리표시자 방식 유지: `[그림 N-M] 설명` + `> 캡처 대상:` 줄. 사용자가 실제로 티칭을 재작업 중이므로, 하시는 김에 순서 상관없이 화면을 캡처해두면 그대로 끼워 넣을 수 있다고 안내됨(사용자에게 이미 전달함).
- "이미지 없이 매뉴얼을 어떻게 만드느냐"는 사용자 우려에 대해, 회색 자리표시자 상자 + 캡처 대상 설명 + 별도 체크리스트 방식이라고 이미 설명 완료 — 이번 챕터도 동일한 시각적 형식(굵은 회색 상자, "※ 이 자리에 화면 캡처 이미지를 삽입하세요")을 유지해야 한다.

### 백업 안내 (사용자에게 이미 설명한 내용, 챕터에 반영 필요)
- 재티칭 전 레시피 폴더 전체 백업을 권장 문구로 포함한다.
- 근거: `main.ini` 하나만으로는 부족하다 — 레시피는 `D:\Data\Recipe\<레시피명>\` 폴더 전체이며 `.shm`/`.ncm`/`.mmf`/`.json`/이미지 파일이 하위 폴더(TOP/SIDE/BOTTOM/ETHERNET_ALIGN/SEQ_*)에 흩어져 있다(FAI_1 레시피 실측: `.shm` 31 + `.ncm` 2 + `.mmf` 15 + `.json` 7 = 55개 파일). 재티칭하면 이 파일들이 같은 경로에 그대로 덮어써진다.
- 앱의 [레시피 열기] 창 `[복사]`(COPY) 버튼이 폴더 전체를 복제해준다(`Btn_Copy_Click` → `RecipeFiles.Handle.Copy()`, 관리자 권한 필요) — 이 사실을 실행자가 코드로 재확인 후 절차에 포함할 것.

### Claude's Discretion
- 9장 내부 절 번호 매기기, 각 티칭 종류별 세부 단계 수는 실제 코드(버튼/다이얼로그 흐름)를 조사한 실행자가 판단한다.
- Align 챕터에서 Tray/Bottom 두 서브시스템을 어느 정도까지 각각 다룰지(공통 절차로 묶을지 별도로 나눌지)는 실행자가 코드를 보고 판단한다.

</decisions>

<specifics>
## Specific Ideas

없음 — 위 결정 사항으로 충분히 범위가 잡힘.

</specifics>

<canonical_refs>
## Canonical References

- `.planning/quick/260818-el1-operator-word-from-scratch-datameasureme/260818-el1-PLAN.md` — 선행 quick task의 원고 규칙(고정 목차, 그림 자리표시자 형식, 하드 제약, evidence_map 스타일)을 그대로 계승할 것. 특히 <already_verified> 섹션의 계정 등급(Admin/Engineer뿐) 사실은 이번 챕터에도 그대로 적용된다.
- `Document/Manual/DataMeasurement_Operator_Manual_v1.0.md` — 확장 대상 원고. 1~8장/부록은 절대 수정하지 않는다(이미 검증 통과, 회귀 방지).
- `Document/Manual/_build_operator_manual_docx.py` — 재사용/재실행할 빌드 스크립트. 로직 자체를 바꿀 필요는 없을 것으로 예상되나(원고만 늘리면 됨), 만약 9장이 기존 정규식/규칙과 충돌하면 최소 수정한다.
- `WPF_Example/Utility/RecipeFileHelper.cs` — 레시피 폴더 구조(`GetPatternModelFilePath`/`GetPatternImageFilePath`/`Copy`) 근거 파일.
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs`, `WPF_Example/Halcon/Algorithms/PatternMatchService.cs`, `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs`, `WPF_Example/UI/ContentItem/MainView.xaml(.cs)`, `WPF_Example/Custom/UI/BottomVisionView.xaml.cs`, `WPF_Example/Custom/UI/TrayVisionView.xaml.cs` — 3종 티칭 절차의 근거 코드.

</canonical_refs>
