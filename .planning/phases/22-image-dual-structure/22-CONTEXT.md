# Phase 22 Context — 이미지 이중화 구조

## 배경

현재 Simul 모드에서는 이미지 1장을 로드하면 Datum 티칭과 FAI 검사 모두
동일한 경로의 이미지를 사용한다.

문제: 티칭 시 사용한 기준 이미지를 나중에 참조할 수 없어
재티칭 시 기준이 불명확하고, 검사 이미지와 역할 혼용이 발생한다.

## 이미지 역할 분리

| 구분 | 용도 | 시점 |
|---|---|---|
| TeachingImagePath | ROI 위치 정의, Test Find 검증 | 한 번 (셋업) |
| InspectionImagePath | Datum 찾기 + FAI 측정 | 매 사이클 |

Simul 모드에서는 두 경로가 동일 파일을 가리켜도 무방하나,
코드 레벨에서 역할은 항상 분리 유지한다.

## 영향 범위 (예상)

- DatumConfig.cs: TeachingImagePath 필드 추가
- INI 직렬화 로직: TeachingImagePath read/write
- Simul 검사 실행 경로: InspectionImagePath 별도 참조
- UI: 티칭 이미지 경로 표시/설정 가능하면 추가

## 의존

- Phase 21 완료 후 시작
- Phase 22 완료 후 Phase 23 (A시리즈 Simul) 에서 이 구조 활용
