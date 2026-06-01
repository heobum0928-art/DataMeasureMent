# Phase 40: 결과 분석 & Export I — Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-01
**Phase:** 40-export-i-1-2026-06-01
**Areas discussed:** 결과 영속화 전략, xlsx 라이브러리, 엑셀 레이아웃+이미지 링크, 리뷰어 UI 위치+폴더/트리거

---

## 결과 영속화 전략 (OUT-01 핵심)

| Option | Description | Selected |
|--------|-------------|----------|
| 구조화 JSON + 재렌더 | cycle 단위 JSON(측정값+판정+nominal/tol+overlay 기하+이미지경로), 리뷰어 역직렬화 후 재렌더. xlsx 공통 토대 | ✓ |
| 결과 PNG + 경량 JSON 사이드카 | overlay 그려진 PNG + 측정값/메타 JSON. 정적 표시, 인터랙티브 X | |
| raw 이미지 + 레시피 재실행 | 저장 raw 를 재측정. 무겁고 레시피 변경 시 불일치 | |

**User's choice:** 구조화 JSON + 재렌더
**Notes:** Newtonsoft.Json 이미 사용 가능. cycle 결과 객체가 전무하여 xlsx 도 어차피 구조화 데이터 필요 → 영속화 계층이 OUT-01·OUT-02 공통 토대라는 점이 결정 근거.

### 메타데이터 범위

| Option | Description | Selected |
|--------|-------------|----------|
| 타임스탬프+모델명+종합판정 | 검사 일시 + 레시피/모델명 + OK/NG/검출실패 | ✓ |
| + 작업자/TestId/시퀀스 | 위 + LoginManager 작업자 + TCP TestId + 시퀀스명 | |
| 최소 (타임스탬프+판정만) | 시각+판정만 | |

**User's choice:** 타임스탬프+모델명+종합판정

### 저장 위치/단위

| Option | Description | Selected |
|--------|-------------|----------|
| ResultSavePath/날짜/cycle 단위 | ./Result/{YYYYMMDD}/{HHmmss}_.../ cycle 폴더+JSON | ✓ |
| 이미지 Raw 날짜 폴더 옆 JSON | {ImageSavePath}/Raw/{YYYYMMDD}/ 에 동거 | |
| Claude 재량 | plan 에서 결정 | |

**User's choice:** ResultSavePath/날짜/cycle 단위

---

## xlsx 라이브러리

| Option | Description | Selected |
|--------|-------------|----------|
| ClosedXML (MIT) | MIT, fluent API, 이미지/하이퍼링크, .NET 4.6+. packages.config 전이 의존성 주의 | ✓ |
| NPOI (Apache 2.0) | 성숙, xls+xlsx, SharpZipLib 의존, API 장황 | |
| OpenXML SDK | MS 공식, 의존성 적음, 매우 verbose | |
| EPPlus 4.5.3.3 (LGPL) | 5+ 상용 위험, 4.5.3.3 마지막 무료, 구버전 고정 | |

**User's choice:** ClosedXML (MIT)
**Notes:** 상용 산업 제품 → 라이선스 안전 우선. packages.config 전이 의존성 + .NET 4.8 바인딩은 research/plan 검증.

---

## 엑셀 레이아웃 + 이미지 링크

### 행 구조

| Option | Description | Selected |
|--------|-------------|----------|
| 1행 = 1측정 | Shot/FAI/측정명/nominal/tol±/측정값/판정. 평면적·필터 용이 | ✓ |
| 1행 = 1FAI 요약 | FAI당 한 줄 요약 | |
| 계층 (Shot>FAI>측정) | 그룹 헤더 + 측정 행 | |

**User's choice:** 1행 = 1측정

### 메타 배치

| Option | Description | Selected |
|--------|-------------|----------|
| 시트 상단 헤더 블록 | 상단 모델명·일시·종합판정 + 아래 테이블 | ✓ |
| 별도 메타 시트 | Summary + Data 시트 분리 | |

**User's choice:** 시트 상단 헤더 블록

### 이미지 링크

| Option | Description | Selected |
|--------|-------------|----------|
| 하이퍼링크 | 셀에 이미지 경로 하이퍼링크, 클릭 시 외부 뷰어 | ✓ |
| 셀 임베드 썸네일 | 이미지 셀 직접 삽입, 파일 비대 | |
| 둘 다 | 임베드+하이퍼링크 | |

**User's choice:** 하이퍼링크

---

## 리뷰어 UI 위치 + 폴더/트리거

### UI 위치

| Option | Description | Selected |
|--------|-------------|----------|
| 별도 창 (Window) | 독립 리뷰어 Window, MainView display-only 유지 | ✓ |
| MainView 탭/패널 | 메인 화면 내 탭 통합 | |

**User's choice:** 별도 창 (Window)

### 폴더 로드 UX

| Option | Description | Selected |
|--------|-------------|----------|
| 날짜 폴더 → cycle 목록 → 선택 | Ookii 폴더 다이얼로그 → cycle 목록(시각·판정) → 재현 | ✓ |
| 개별 cycle 폴더 직접 선택 | cycle 폴더 직접 열기 | |

**User's choice:** 날짜 폴더 → cycle 목록 → 선택

### xlsx export 트리거

| Option | Description | Selected |
|--------|-------------|----------|
| 리뷰어에서 수동 버튼 | 연 cycle 을 [엑셀 export] 버튼으로 생성 | ✓ |
| 검사 후 자동 생성 | 매 cycle 종료 시 자동 | |
| 둘 다 | 자동 + 수동 | |

**User's choice:** 리뷰어에서 수동 버튼

---

## Claude's Discretion

- overlay JSON 스키마 상세, 저장 wiring 시점, HalconDisplayService 재사용 방식, 에러/빈/검출실패 cycle 표현, 결과 폴더 보존 정책 → research/plan 위임

## Deferred Ideas

- 50회 반복도 통계(Phase 41 OUT-03), 알고리즘별 통계표(Phase 41 OUT-04)
- 검사 후 자동 xlsx, 셀 임베드 썸네일, 메타 작업자/TestId 확장, 결과 폴더 정리 정책
