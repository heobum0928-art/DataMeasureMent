---
id: 260417-ou8
title: EdgePairDistanceMeasurement ROI 필드 제거 — FAIConfig 단일 소스화
date: 2026-04-17
status: completed
---

# Summary: FAIConfig ROI 단일 소스화

## 배경
Phase 6-04 UAT에서 "사용자가 그린 노란 ROI ≠ 측정이 실제 사용하는 빨간 ROI" 증상.
직전 커밋 `44523ad`(ROI 좌표 불일치 — FAIConfig↔Measurement ROI 동기화)는 표면 패치로,
DataGrid 선택 조건이 안 맞으면 동기화가 스킵되어 버그 재발.

## 변경
1. **EdgePairDistanceMeasurement.cs**
   - `ROI_Row/Col/Phi/Length1/Length2` 5개 프로퍼티 제거
   - `TryExecute`가 `Owner as FAIConfig` 캐스팅 후 `ownerFai.ROI_*` 직접 참조
   - Owner가 FAIConfig 아니면 error 반환하고 false

2. **MainView.xaml.cs `CommitRectRoi`**
   - Measurement.ROI_* 동기화 블록 삭제 (구조적으로 불필요)
   - 이유 주석 한 줄 남김

## 결과
- ROI 저장 위치: **FAIConfig.ROI_* 단 한 곳**
- 동기화 필요성 자체 소멸 → 재발 불가
- 레거시 INI 파일의 `EdgePair.ROI_*` 키는 `ParamBase.Load` 리플렉션이 무시하므로 하위호환 유지

## 검증
- `edgeMeas.ROI_` / `EdgePairDistanceMeasurement.*ROI_` 전역 검색 → 0건
- 런타임 UAT는 빌드 후 사용자 확인 필요

## 관련 커밋
- 선행: `44523ad fix(06-04): ROI 좌표 불일치 — FAIConfig↔Measurement ROI 동기화` (이번 커밋으로 해당 로직의 보완 완료)
