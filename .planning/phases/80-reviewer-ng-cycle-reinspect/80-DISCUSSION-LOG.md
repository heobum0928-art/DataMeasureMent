# Phase 80: 리뷰어 NG 사이클 사진 한 번에 불러와 파라미터 수정·재검사 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-09-18
**Phase:** 80-reviewer-ng-cycle-reinspect
**Areas discussed:** 부르는 방법·화면 흐름, 불러올 사진 범위, 운영 레시피 보호, JPG 차이 · 기준 ROI 시험 찾기

---

## 부르는 방법·화면 흐름

| Option | Description | Selected |
|--------|-------------|----------|
| 버튼 | 행 선택 후 "이 사진으로 파라미터 수정" | ✓ |
| 더블클릭 | 빠르지만 모르고 누를 수 있음 | |
| 버튼 + 더블클릭 | 둘 다 | |

- 리뷰어 창: **열어 둔 채 메인 화면을 앞으로** ✓ / 닫기
- 선택 상태: **그 Shot + 그 측정(FAI) 선택** ✓ / Shot 까지만
- 자동 RUN: **Test Find 까지만** ✓ / RUN 까지 자동
- 버튼 위치 (추가 질문, 사용자 "사용자가 쉽게 접근할 수 있도록"): **NG 원인 설명 바로 아래, 크게** ✓ / 위쪽 버튼 줄 / 두 곳 + 더블클릭
- 확인창: **한 번에 바로 불러오기** ✓ / 확인 한 번

**Notes:** 사용자 추가 요청 "UI 단순하고 쉽게 가야 함" → D-80-00. "코드도 원칙 알지? 이항 삼항 안 쓰고 MVVM" → D-80-16/17.

---

## 불러올 사진 범위

| Option | Description | Selected |
|--------|-------------|----------|
| 같은 자재의 모든 Shot + 기준점 | 반복검사 코드 재사용, 로드맵 원래 목표 | ✓ |
| 고른 Shot + 그 Shot 의 기준점만 | 단순, 다른 Shot 결과 섞일 수 있음 | |

- Z 범위 Shot: **z 후보 사진 전부, 없으면 선택 z 한 장 + 안내** ✓ / 선택 z 한 장만
- 기준점 사진 없음: **알림 후 Shot 사진만** ✓ / 알림 후 아무것도 안 불러옴 / 버튼 비활성

---

## 운영 레시피 보호

| Option | Description | Selected |
|--------|-------------|----------|
| 파라미터는 저장, 사진 경로는 원래 값으로 | 고친 값 바로 반영 | ✓ |
| 불러온 동안 저장 막기 | 반복검사 방식 | |
| 저장 때 확인창만 | 사용자가 결정 | |

- 되돌리는 때 (복수): **[해제] 버튼 ✓, PLC 자동 검사 수신 시 ✓, 종료·레시피 변경 시 ✓**
- 상태 표시: **메인 화면 한 줄 표시** ✓ / 로그만

---

## JPG 차이 · 기준 ROI 시험 찾기

- JPG: **상태 줄에 짧게 안내** ✓ / 불러올 때 확인창 / 안내 없음
- 기준 ROI 시험 찾기: **이번에 포함** ✓ / 다음 Phase 로

---

## Claude's Discretion

- 문구·크기·색, 리뷰어→메인 연결 방식, 스냅샷/복원 코드 공유 여부, Local Ref 재계산 트리거 방식

## Deferred Ideas

- 재검사 결과와 리뷰어 원래 값 비교 표시
