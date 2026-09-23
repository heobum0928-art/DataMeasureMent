---
status: complete
slug: reviewer-accum-scope-search
date: 2026-09-23
---

# 리뷰어 누적 엑셀 저장 대상(NG/OK/전체) + 찾기

**요청(사용자 2026-09-23):** "리뷰어에서 … NG 만 모아서 데이터로 저장하거나 OK만 저장하거나 둘다 저장하거나 하는 기능 … 엑셀로 출력할때 물론 검색도 그 기능이 되야하고"

**결정:** 드롭다운 1개 · 한 파일에 시트 분리 · 찾기 = 기간(날짜·시:분) + 자재번호 조회 (측정명 찾기는 사용자 요청으로 뺌) · 판정 필터(양품만/불량만)

## 무엇을 했나

| 영역 | 내용 |
|---|---|
| 저장 대상 | 왼쪽 `누적 엑셀 저장` 버튼 위 드롭다운: NG 만 / OK 만 / 전체 |
| 파일 | `NG_분석_누적.xlsx` 한 파일. NG → `NG 누적` 시트, OK → `OK 누적` 시트 |
| 중복 | 이미 들어간 항목 건너뛰기는 시트마다 따로 |
| OK 행 | 원인 분석 칸은 비움, 값·판정·z 등은 그대로. 측정값 없는 행은 저장 안 함 |
| 조회 | 리뷰어 왼쪽 위 `기간 [날짜 시:분] ~ [날짜 시:분]`, `자재번호`, `[조회]` — 여러 날 가능. 조회로 열면 누적 엑셀도 조회된 검사만 저장 |
| 판정 필터 | `양품만 보기` 체크 추가, `불량만 보기`와 서로 배타 |

## 파일

- `WPF_Example/Custom/Export/ExcelExportService.cs` — `EAccumExportScope` enum, `SHEET_NAME_OK`, `AppendDateFolder(..., scope)` 오버로드(기존 2인자 = NG 만, 회귀 0), `BuildNgRows` scope 필터, `AppendRowsToSheet` 로 시트별 쓰기, 메시지 문구 일반화("NG" → "측정")
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml` — `cmb_accumScope`, `txt_rowSearch`, `chk_passOnly`
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs` — `ResolveAccumScope`, `ApplySearchText`, `ApplyPassOnly`, 체크 배타 처리
- `WPF_Example/VersionDefine.cs` — 1.7.54.0

## 검증

- Debug|x64 빌드 오류 0
- 가독성 grep(삼항·`??`·`?.`·switch 식·날짜 주석): 추가 줄 전부 0
- 실제 동작 확인은 다음 Release 배포 때

## 알아 둘 점

- 버튼 이름이 `NG 누적 엑셀 저장` → `누적 엑셀 저장`, 메시지 제목이 `누적 엑셀`로 바뀜
- 기존 `NG 누적` 시트는 그대로 이어서 쌓임
