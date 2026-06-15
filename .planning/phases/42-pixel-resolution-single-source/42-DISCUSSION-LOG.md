# Phase 42: 픽셀분해능 런타임 단일소스 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-15
**Phase:** 42-pixel-resolution-single-source (CO-38-01)
**Areas discussed:** 단일소스 전략, Shot 편집 진입점/라이브 반영, PropertyGrid 정리 범위, EdgePair 정합 + X/Y 통합

---

## 단일소스 전략 (핵심)

| Option | Description | Selected |
|--------|-------------|----------|
| (A) Cascade만 | fai 복사본 유지 + Shot 편집 시 전체 FAI 복사. 논리적 단일소스 | |
| (B) 소비지점 Rewire | Action_FAIMeasurement가 shot.PixelResolution 직접 전달, fai 복사본 소비 제거. 물리적 단일소스 | ✓ |
| (C) 둘 다 | Rewire + 편집 cascade 유지 | |

**User's choice:** (B) 소비지점 Rewire
**Notes:** 대부분 측정이 이미 pixelResolution 파라미터를 소비 → 호출부 1곳(Action_FAIMeasurement.cs:269,284)만 변경으로 측정 코드 무변경, 회귀 위험 최소. "측정 경로 단일 소스" 성공기준 직접 충족.

---

## Shot 편집 진입점 / 라이브 반영

### 편집 진입점
| Option | Description | Selected |
|--------|-------------|----------|
| PropertyGrid + 캘리브 둘 다 | Shot 노드 PropertyGrid PixelResolution + 2점 캘리브레이션 액션, 둘 다 shot.PixelResolution 수렴 | ✓ |
| 캘리브 액션만 | PropertyGrid 읽기전용, 캘리브레이션으로만 설정 | |

### 라이브 반영
| Option | Description | Selected |
|--------|-------------|----------|
| 다음 검사부터 반영 | (B) 구조상 검사 시 shot 값 직접 읽음 → 자동. 추가 구현 불필요 | ✓ |
| 편집 즉시 재계산 | 편집 commit 즉시 현재 표시 결과/오버레이 재계산 | |

**User's choice:** PropertyGrid + 캘리브 둘 다 / 다음 검사부터 반영
**Notes:** "재시작 없이 반영" 성공기준이 (B) 구조로 자동 충족.

---

## PropertyGrid 정리 범위

### 표시 처리
| Option | Description | Selected |
|--------|-------------|----------|
| 완전 숨김 (Browsable false) | 소비 안 되는 잔존값이므로 노출 불필요 | ✓ |
| 읽기전용 회색 | 값은 보이되 편집 불가, 디버깅 가시성 유지 | |

### 정리 범위
| Option | Description | Selected |
|--------|-------------|----------|
| FAIConfig + EdgePair 모두 | FAIConfig X/Y + EdgePair X/Y 모두, ShotConfig만 노출 | ✓ |
| FAIConfig만 | FAIConfig X/Y만, EdgePair는 4번 결정에 따라 | |

**User's choice:** 완전 숨김 / FAIConfig + EdgePair 모두
**Notes:** ShotConfig.PixelResolution은 유일 편집 소스로 노출 유지.

---

## EdgePair 정합 + X/Y 통합

### EdgePair 정합
| Option | Description | Selected |
|--------|-------------|----------|
| 파라미터 사용으로 재배선 | self.PixelResolutionX 대신 넘어온 pixelResolution 파라미터 사용 | ✓ |
| 자체 필드 유지 (현행) | EdgePair만 예외로 남김 | |

### X=Y 통합
| Option | Description | Selected |
|--------|-------------|----------|
| 물리 필드 유지 + 소비 안 함 | X/Y INI 키 호환 유지, (B)상 미소비라 X≠Y 무해, 마이그레이션 불필요 | ✓ |
| X/Y 물리 제거 → 단일 프로퍼티 | INI 키 변경, 마이그레이션 필요, 회귀위험↑ | |

**User's choice:** 파라미터 사용으로 재배선 / 물리 필드 유지 + 소비 안 함
**Notes:** D-09(X=Y) 이미 확정. 물리 필드 보존으로 INI 하위호환 유지하며 마이그레이션 회피.

---

## Claude's Discretion

- 숨김 구현 방식(어트리뷰트 vs ICustomTypeDescriptor 필터)
- shot.PixelResolution 접근 방식(Owner 체인 walk vs Action 직접 참조)
- 로딩 정규화 measurement-level 확장 필요 여부

## Deferred Ideas

- X/Y 비정방형(X≠Y) 실지원 — 별도 phase
- INI 포맷 키 단일화(물리 X/Y 제거) — 차기 포맷 버전(v7)
