# Phase 67: 양산 이력 통계 분석 (STAT-01) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-07
**Phase:** 67-양산 이력 통계 분석 (STAT-01)
**Areas discussed:** CSV 저장 위치·파일 단위, 집계·판정 정책, 통계 화면 진입·구조, 조회 필터·차트 상호작용

---

## (사전 스코핑) 데이터 범위 & 출력 형태

| Option | Description | Selected |
|--------|-------------|----------|
| 세션 반복측정만 (기존 확장) | BatchRunService N회 반복 결과를 UI화. 영구 저장 불필요 | |
| 양산 이력 누적 (신규 저장) | 매 사이클 측정값을 파일에 지속 기록 → 기간별 통계 | ✓ |
| 둘 다 | 반복측정 + 양산 이력 | |

**User's choice:** 양산 이력 누적 (큰 작업)
**Notes:** 출력 형태 = UI 화면(테이블+차트). 사용자에게 두 옵션 차이를 실제 상황으로 설명 후 선택.

---

## CSV 저장 위치·파일 단위

| Option | Description | Selected |
|--------|-------------|----------|
| 신규 StatisticsSavePath 설정 | [DirectoryPath] 프로퍼티 추가, 기본 .../Statistics. Result 와 분리 | ✓ |
| ResultSavePath 하위 재사용 | 기존 Result 폴더 하위 Statistics 서브폴더 | |

| Option | Description | Selected |
|--------|-------------|----------|
| 일자별 1파일 | yyyyMMdd.csv 하나에 전 레시피 혼합, 컬럼으로 구분 | ✓ |
| 일자×레시피 분리 | yyyyMMdd_레시피명.csv | |

| Option | Description | Selected |
|--------|-------------|----------|
| 고정 컬럼 + 키로 식별 | 컬럼 고정, Shot/FAI/측정명 키 그룹핑. 항목 증감 무관 | ✓ |
| 헤더에 버전 표기 | CSV 첫 줄 스키마 버전 명시 | |

**User's choice:** 신규 StatisticsSavePath / 일자별 1파일 / 고정 컬럼+키 식별
**Notes:** 검사 결과(Result)와 분리해 관리·백업 용이. 레시피는 CSV 컬럼으로 구분.

---

## 집계·판정 정책

| Option | Description | Selected |
|--------|-------------|----------|
| 검출실패 별도 칼럼 분리 | 불량률 = NG/(OK+NG), 검출실패는 DetectFail 별도 집계 | ✓ |
| 검출실패도 불량으로 간주 | 불량률 = (NG+검출실패)/전체 | |

| Option | Description | Selected |
|--------|-------------|----------|
| 측정값 있는 것만 | OK/NG 불문 측정된 값만 Cpk 모집단. 기존 로직 동일 | ✓ |
| OK 값만 | PASS 측정값만으로 Cpk | |

**User's choice:** 검출실패 별도 칼럼 분리 / 측정값 있는 것만
**Notes:** 기존 RepeatMeasurementStats 정책과 정확히 일치 → 재사용 깔끔. 장비 미안착 이슈가 품질 불량률과 분리됨.

---

## 통계 화면 진입·구조

| Option | Description | Selected |
|--------|-------------|----------|
| ReviewerWindow 미러링 | 메뉴 항목 + 비모달 Show + 멤버 재사용. 단독 StatisticsWindow | ✓ |
| ReviewerWindow 내 탭 추가 | 기존 리뷰어 창에 '통계' 탭 | |

| Option | Description | Selected |
|--------|-------------|----------|
| EPageType.Statistics 신규 | Reviewer 옆 신규 enum, 라벨 "통계분석" | ✓ |
| 배치/이름 나중에 | plan 단계 Claude 재량 | |

**User's choice:** ReviewerWindow 미러링 / EPageType.Statistics 신규
**Notes:** Phase 40 OUT-01 D-08 비모달 패턴 재사용. 라이브 검사 방해 안 함.

---

## 조회 필터·차트 상호작용

| Option | Description | Selected |
|--------|-------------|----------|
| 오늘 하루 | from=to=today, 파일 1개만 읽어 빠름 | ✓ |
| 최근 7일 | 주간 추이 | |
| 빈 값(직접 지정) | 자동 조회 없음 | |

| Option | Description | Selected |
|--------|-------------|----------|
| 테이블 행 클릭→해당 항목 | DataGrid 행 선택으로 히스토그램+추이 갱신 | ✓ |
| 드롭다운 콤보박스 선택 | Shot→FAI→측정명 콤보 | |

| Option | Description | Selected |
|--------|-------------|----------|
| 측정 순서(샘플 인덱스) | 1,2,3... 균등 간격, SPC 추세 판독 용이 | ✓ |
| 실제 시각(timestamp) | 검사 시각 그대로 | |

**User's choice:** 오늘 하루 / 테이블 행 클릭→항목 / 측정 순서(샘플 인덱스)
**Notes:** 추이 차트에 공차 상·하한선 + 평균선 오버레이. 히스토그램 bin 개수는 plan 재량.

---

## Claude's Discretion

- CSV writer 클래스 이름/위치
- 히스토그램 bin 개수 산정 방식
- 통계 창 레이아웃(상하 vs 좌우 분할)
- 대용량 기간 조회 성능 가드 필요 여부
- RepeatMeasurementStats 재사용 연결 방식(어댑터 vs CycleResultDto 재구성)

## Deferred Ideas

- SPC 관리도(X-bar/R chart) — 다음 단계
- 통계 결과 Excel export — 별도 phase
- Cpk 임계 경보/알람 — 향후
- 세션 반복측정(BatchRunService) 결과 UI 화면화 — ①옵션, 추후 원하면 별도 phase
