# Phase 53: 픽셀 캘리브레이션 (체커보드) - Discussion Log

> **Audit trail only.** Do not use as input to planning/research/execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-23
**Phase:** 53-픽셀 캘리브레이션 (체커보드)
**Areas discussed:** 격자 검출 & 피치 입력, mm/px 산출 & 외곽 왜곡 검증, PixelResolution 적용 범위, 입력 모드 & 창

---

## 격자 한 칸 실제 크기(mm) 입력
| Option | Selected |
|--------|----------|
| 수동 입력 (기억된 기본값) | ✓ |
| 설정에 고정값 저장 | |

## mm/px X·Y 분리
| Option | Selected |
|--------|----------|
| 단일 평균 (텔레센트릭 등방) | ✓ |
| X·Y 분리 적용 | |

## PixelResolution 적용 범위
| Option | Selected |
|--------|----------|
| 활성 시퀀스 전체 shot | ✓ |
| 사용자 체크 선택 shot | |
| 선택 shot 1개 (기존) | |

## 입력 모드
| Option | Selected |
|--------|----------|
| 이미지 로드 + 라이브 촬상 둘 다 | ✓ |
| 이미지 로드만 (POC) | |

## 외곽 왜곡 검증 표현
| Option | Selected |
|--------|----------|
| 수치 + 임계 경고 | ✓ |
| 수치만 리포트 | |
| 수치 + 시각화 | |

## PixelResolution 반영 시점
| Option | Selected |
|--------|----------|
| 사용자 확인 후 반영 ([적용] 버튼) | ✓ |
| 산출 즉시 반영 | |

## Carried-forward (대화 전반 LOCK)
- 풀 caltab 캘리브 안 씀 → 코너 검출 + 격자 간격 직접 산출
- 왜곡 보정(undistort) 안 함 — 외곽 편차 유의미 시 별도 phase 승격

## Claude's Discretion
- 코너 검출 HALCON 연산자 선택, 창 레이아웃, 검출 견고성 가드
