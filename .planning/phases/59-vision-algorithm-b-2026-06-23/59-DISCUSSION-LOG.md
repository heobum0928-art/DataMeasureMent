# Phase 59: Vision Algorithm (B) - Discussion Log

> **Audit trail only.** Decisions captured in CONTEXT.md.

**Date:** 2026-06-24
**Phase:** 59-vision-algorithm-b-2026-06-23
**Mode:** `--auto` — 사용자 외부 부재. Claude 가 권장 설계를 선택하고 근거를 CONTEXT.md 에 문서화. 사용자 복귀 시 검토/수정 가능.

---

## Auto-selected decisions (Claude-decided, recommended defaults)

| 결정 | 선택 | 근거 |
|------|------|------|
| 서비스 구조 (D-01) | 신규 `AlignShapeMatchService` + 기존 `PatternMatchService` 재사용(composition, 무수정) | 검증된 범용 알고리즘(Grabber 결합 없는 primitive API), phase 58 HikCamera 합성 패턴과 동일 |
| 소유/통합 (D-02) | `EthernetVisionHandler.Matcher` 프로퍼티 lazy 생성 | phase 58 handler 아키텍처 연장, SystemHandler 추가 수정 0 |
| Shape 파라미터 (D-03) | PatternMatchService 기본값(NumLevels 4/contrast auto/MinContrast 10) + 모드별 AngleExtent | 검증값 재사용, Tray 작은각/Bottom 넓은각 |
| .shm 저장 (D-04) | `ETHERNET_ALIGN\Tray.shm`/`Bottom.shm` + 레퍼런스 포즈 사이드카 json | recipe 규약 재사용+이더넷 격리, offset 계산용 ref pose 필요 |
| Offset (D-05) | cur−ref, px×(8.652/1000)=mm, Tray=X/Y, Bottom=X/Y/Theta, `AlignResult` 모델 | AV-04 직결, 부호/축은 UAT 확정 |
| 실패 격리 (D-06) | 전 메서드 try-catch → `AlignResult{Found=false}`, finally dispose | Grabber 무영향(v1.3 핵심), HALCON 핸들 누수 방지 |
| API (D-07) | TryTeach / Run / TryLoadTemplate / HasTemplate | UI(ROI 드로잉)는 Phase 61, 59 는 서비스 API |

## 핵심 발견 (스카우팅)
- DataMeasurement 에 완성된 Shape Matching 서비스 `PatternMatchService`(phase 54~56) 존재 → 전면 재사용.
- Phase 58 산출물(EthernetVisionHandler.Handle.Camera.Grab, EthernetPixelResolution 8.652) 통합 지점 확인.

## Deferred
- Phase 60 캘리브 / Phase 61 UI / Phase 62 TCP / EthernetExposure 적용(WR-03).
