# Phase 54: Datum 패턴매칭 위치보정 (ALIGN-01) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-18
**Phase:** 54-datum-align-01-x-y-tilt
**Areas discussed:** 매칭 엔진 Shape/NCC, 모델 파일 영속, 패턴 티칭 UI, 검색영역/ref pose 정의

---

## 매칭 엔진 Shape/NCC

| Option | Description | Selected |
|--------|-------------|----------|
| per-Datum 선택형 (Shape기본+NCC) | Datum 마다 Shape/NCC 선택(AlgorithmType 드롭다운 미러). 포커스 불량=NCC, tilt 큰부위=Shape | ✓ |
| Shape 고정 | 회전/조명 강, defocus 약점은 MinContrast 낮춤+티칭 포커스 정합 완화 | |
| NCC 고정 | defocus 강, 회전은 작은 angle range (coarse x,y 전용이라 OK) | |

**User's choice:** per-Datum 선택형 (Shape 기본 + NCC 옵션)
**Notes:** 사용자가 자재 포커싱 불량 가능성 제기 → Shape 의 defocus 취약 우려. 매칭은 coarse x,y 전용(정밀 θ=line-fit)이라 NCC 회전 약점 완화. defocus 는 엔진만으로 해결 불가(line-fit/측정 에지도 훼손) — 포커스 품질이 전제, 엔진 선택+티칭 포커스 정합은 완화책.

---

## 모델 파일 영속

| Option | Description | Selected |
|--------|-------------|----------|
| 이름 기반 결정적 재계산 | 절대경로 미저장, (recipe/seq/datum/ext) 재계산. Copy/Rename/Delete 자동 정합, stale 0 | ✓ |
| 상대경로 저장 | 레시피 기준 상대경로를 PatternModelPath 저장 | |
| 절대경로 저장 | 전체 경로 저장, Copy 시 stale 위험 | |

**User's choice:** 이름 기반 결정적 재계산
**Notes:** RecipeFiles.GetModelFilePath 패턴 + Copy(재귀)/Delete(폴더째)가 모델 자동 동반 → ROADMAP "높음 리스크(백업 누락)" 해소. 확장자 = HALCON .shm/.ncm 신규(EXTENSION_MODEL=.mmf 은 MIL 레거시라 미재사용).

---

## 패턴 티칭 UI

| Option | Description | Selected |
|--------|-------------|----------|
| 최소한 완결형 | Datum 노드 패턴 ROI 그리기 + [모델 생성/저장] + ref pose 자동기록. 기존 Datum 티칭 UX 재사용 | ✓ |
| 풀 기능 | + 검색영역 별도 그리기 + score 미리보기 + 재티칭 미리보기 | |
| 후속 phase 분리 | 1차 백엔드만, 티칭 임시 — UI 없으면 SIMUL 검증 불가(Phase 52 전철) | |

**User's choice:** 최소한 완결형
**Notes:** UI 없으면 모델 생성·SIMUL 검증 불가 → Phase 52 PARTIAL 전철 회피. 풀 기능은 후속.

---

## 검색영역 / 속도 전략

| Option | Description | Selected |
|--------|-------------|----------|
| 전체 이미지 | 전체 검색 — 단순하나 152MP 등 고해상도 tact 위험 | |
| 변위 margin + 다운샘플 | 검색영역=template±예상변위 margin(PatternSearchMarginPx) + coarse 매칭 1/2~1/4 다운샘플(x,y 획득→스케일 복원). 정밀 θ·측정 원본 | ✓ |
| 변위 margin 검색영역만 | 검색영역만 제한(다운샘플 없이) | |

**User's choice:** 변위 margin + 다운샘플
**Notes:** 사용자가 "전체 이미지면 Find 시간 길지 않나" 제기 (Phase 41 VIEWORKS 152MP). 매칭이 coarse x,y 전용이라 다운샘플 안전 — 4~16배 가속, x,y 몇 px 오차는 line-fit ROI 안착에 무관. 검색영역은 자재 변위 물리한계로 margin 제한.

---

## ref pose 기록 방식

| Option | Description | Selected |
|--------|-------------|----------|
| 티칭 시 find 결과 pose | 티칭 이미지 find pose(Row/Col/AngleDeg) 저장. 런타임 pose−ref=변위, 동일 연산 부호 일관 | ✓ |
| template ROI 중심을 ref | ROI 중심 좌표를 ref — 단순하나 find 연산과 미세 차이(부호 불일치 위험) | |

**User's choice:** 티칭 시 find 결과 pose
**Notes:** 런타임과 동일 연산이라 부호/좌표계 일관성 보장.

---

## Claude's Discretion

- vector_angle_to_rigid vs 수동 합성 (분석문서 §3 권고 = vector_angle_to_rigid)
- 다운샘플 비율/AngleExtent/NumLevels 기본값 (SIMUL 튜닝)
- PatternMatchService 내부 구조 (try/catch 규약 준수)

## Deferred Ideas

- Side DualImage 패턴매칭 → 후속 phase
- 비강체 부위별 휨 보정 → 범위 밖
- 티칭 풀 기능 → 후속
- 실데이터 변형 페어 UAT → 이미지 확보 후
