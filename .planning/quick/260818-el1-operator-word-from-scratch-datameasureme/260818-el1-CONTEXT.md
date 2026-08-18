# Quick Task 260818-el1: 현장 운영자(Operator)용 신규 Word 매뉴얼 작성 - Context

**Gathered:** 2026-08-18
**Status:** Ready for planning

<domain>
## Task Boundary

현장 운영자(Operator)용 신규 Word 사용자 매뉴얼을 처음부터(from scratch) 작성한다. 대상 시스템은 현재 DataMeasurement(Halcon 24.11 기반) 비전 검사 시스템이다. 코드 변경은 절대 금지 — 이번 작업의 변경 대상은 `Document/Manual/` 산출물 문서뿐이다.

</domain>

<decisions>
## Implementation Decisions

### 매뉴얼 범위
- 처음부터 새로 작성한다 (원본 DDA 매뉴얼을 그대로 갱신하는 게 아님).
- 원본 자료(`Document/Manual/DDA_Vision_User_Manual_ver1.0.docx`, `_docx_text.txt`, `DDA_Operation_Manual_v1.1.pptx`, `_pptx_text.txt`, `Equipment_Overview.md`)는 장/절 구성과 톤 참고용으로만 쓰고, 내용은 현재 코드베이스 기준으로 새로 확인해서 쓴다. 원본 내용을 베끼지 않는다.

### 대상 독자
- 현장 운영자(Operator) 전용. 로그인, 레시피 불러오기/저장/복사, 수동 테스트, 검사 실행/결과 확인 등 일상 조작 위주로 작성한다.
- 코드 구조, 클래스명, 내부 알고리즘 설명은 포함하지 않는다.

### 출력 형식
- Word(.docx), 기존 v1.0 문서와 유사한 장/절 구성 스타일을 유지한다.
- 산출물 파일: `Document/Manual/DataMeasurement_Operator_Manual_v1.0.docx`

### 스크린샷 처리 방식 (사용자 확인 완료, 2026-08-18)
- 이 PC에는 현재 빌드된 실행파일(`bin/x64/Debug/DatumMeasurement.exe`)이 없고, `D:\Data\Recipe`에 실제 제품명 레시피(FAI_1, CU_THIN, ECI_ANTI_PEELING 등)가 있어 실제 장비와 연결된 PC일 가능성이 있다. 임의로 빌드/실행해 카메라·조명 컨트롤러(시리얼 포트) 같은 실물 하드웨어를 건드리는 위험을 피하기 위해, Claude가 앱을 직접 실행해서 스크린샷을 캡처하지 않는다.
- 대신 매뉴얼 본문에 각 단계마다 어떤 화면/상태를 캡처해야 하는지 명시하는 **자리표시자(placeholder)**를 넣는다. 예: `[그림 3-1: 로그인 화면 — ID/PW 입력란, 로그인 버튼]`. 원본 매뉴얼처럼 "그림 N-M" 캡션 번호 체계를 유지한다.
- 실행 결과물과 별도로, 캡처해야 할 화면 목록(자리표시자 캡션 전체 목록 + 어떤 화면/조작 상태를 보여줘야 하는지 설명)을 정리해서 사용자에게 전달한다. 사용자가 직접 캡처해서 끼워 넣는다.
- (참고, 미확정) 추후 사용자가 원하면 SIMUL_MODE 등 하드웨어 접촉 없는 안전한 방식으로 Claude가 일부 화면을 대신 캡처하는 것도 가능 — 이번 작업 범위에는 포함하지 않는다.

### Claude's Discretion
- 매뉴얼의 장(chapter) 구성 순서, 세부 절 제목, 캡션 번호 매기기 방식은 원본 v1.0 문서 스타일을 참고해 Claude가 정한다.

</decisions>

<specifics>
## Specific Ideas

없음 — 위 결정 사항으로 충분히 범위가 잡힘.

</specifics>

<canonical_refs>
## Canonical References

- `Document/Manual/DDA_Vision_User_Manual_ver1.0.docx` / `_docx_text.txt` — 원본 설비 사용자 매뉴얼 (스타일 참고)
- `Document/Manual/DDA_Operation_Manual_v1.1.pptx` / `_pptx_text.txt` — 원본 설비 운영 슬라이드 (스타일 참고)
- `Document/Manual/Equipment_Overview.md` — 원본 설비 요약
- `WPF_Example/UI/`, `WPF_Example/Custom/UI/` — 현재 시스템 UI 코드 (내용 근거, 읽기 전용)

</canonical_refs>
