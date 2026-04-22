# Requirements: DataMeasurement

**Defined:** 2026-04-02
**Core Value:** Shot-FAI 2계층 동적 구조로 100개+ 검사 항목을 유연하게 관리하고, Halcon 에지 측정으로 정밀한 거리 측정(mm) + 공차 판정 수행

## v1 Requirements

Requirements for initial release. Each maps to roadmap phases.

### UI 재설계

- [x] **UI-01**: TreeView에서 Shot/FAI 2계층 구조를 탐색할 수 있다
- [x] **UI-02**: 단일 캔버스에서 선택된 Shot의 이미지를 표시한다 (기존 5탭 제거)
- [x] **UI-03**: FAI 측정 결과(거리 mm, OK/NG 판정)를 테이블로 표시한다
- [x] **UI-04**: FAI를 추가/삭제/수정할 수 있다 (Shot 계층 제거됨, UI-05와 통합)
- [x] **UI-05**: FAI를 추가/삭제/수정할 수 있다

### 티칭

- [ ] **TCH-01**: 캔버스에 FAI ROI 오버레이를 시각적으로 표시한다 (Edge 방향, 범위)
- [ ] **TCH-02**: TeachingStorageService를 통해 ROI 데이터를 저장/로드한다

### 에지 측정 알고리즘

- [x] **ALG-01**: FAI ROI 내에서 Halcon MeasurePos로 에지 페어 거리(mm)를 계산한다
- [x] **ALG-02**: FAIConfig의 Tolerance 기준으로 OK/NG 판정을 수행한다
- [ ] **ALG-03**: 픽셀→mm 변환을 위한 캘리브레이션 기능을 제공한다
- [ ] **ALG-04**: 측정 결과(에지 위치, 거리, 판정)를 캔버스에 오버레이로 표시한다
- [x] **ALG-05**: Datum 기준좌표계로 제품 위치/회전 편차를 자동 보정한다 (hom_mat2d 변환)

### 검사 시퀀스

- [ ] **SEQ-01**: Z축 이동하며 각 Shot 위치에서 카메라 Grab을 순차 실행한다
- [ ] **SEQ-02**: 각 Shot의 모든 FAI에 대해 에지 측정을 수행한다
- [ ] **SEQ-03**: 전체 FAI 결과를 종합하여 최종 OK/NG 판정을 산출한다
- [ ] **SEQ-04**: 측정 결과를 TCP 패킷으로 호스트에 전송한다

## v2 Requirements

### 티칭 고도화

- **TCH-03**: Main 화면에서 별도 다이얼로그 없이 직접 카메라 Grab
- **TCH-04**: 하나의 Shot 이미지에서 여러 FAI ROI를 드래그로 설정

### UI 고도화

- **UI-06**: Shot/FAI 트리에서 드래그 앤 드롭으로 순서 변경
- **UI-07**: 검사 결과 히스토리/트렌드 차트

## Out of Scope

| Feature | Reason |
|---------|--------|
| 3D/Laser 측정 | 2D 에지 측정만 사용 |
| Wafer 검사 시퀀스 | 원본(NewDDA)에만 해당 |
| Side 카메라 검사 | 현재 Top/Bottom만 대상 |
| OAuth/인증 고도화 | 기존 LoginManager 유지 |
| 실시간 카메라 스트리밍 | Grab 단위 촬영만 필요 |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| UI-01 | Phase 1 | Complete |
| UI-02 | Phase 1 | Complete |
| UI-03 | Phase 1 | Complete |
| UI-04 | Phase 1 | Complete |
| UI-05 | Phase 1 | Complete |
| TCH-01 | Phase 2 | Pending |
| TCH-02 | Phase 2 | Pending |
| ALG-03 | Phase 2 | Pending |
| ALG-01 | Phase 3 | Complete |
| ALG-02 | Phase 3 | Complete |
| ALG-04 | Phase 3 → Phase 7 (gap closure) | Pending |
| ALG-05 | Phase 4 | Complete |
| SEQ-01 | Phase 5 | Pending |
| SEQ-02 | Phase 5 | Pending |
| SEQ-03 | Phase 5 | Pending |
| SEQ-04 | Phase 5 | Pending |
| RC-01 | Phase 6 (등록은 Phase 8) | Pending |
| RC-02 | Phase 6 (등록은 Phase 8) | Pending |
| RC-03 | Phase 6 (등록은 Phase 8) | Pending |
| RC-04 | Phase 6 (등록은 Phase 8) | Pending |
| RC-05 | Phase 6 (등록은 Phase 8) | Pending |
| RC-06 | Phase 6 (등록은 Phase 8) | Pending |

**Coverage:**
- v1 requirements: 22 total (UI-01..UI-05, TCH-01..TCH-02, ALG-01..ALG-05, SEQ-01..SEQ-04, RC-01..RC-06)
- Mapped to phases: 22
- Unmapped: 0
- 참고: RC-01..RC-06은 Phase 6에서 구현되었으나 본 문서 정의/본문 체크박스 등록은 Phase 8(요구사항 동기화)에서 수행된다.

---
*Requirements defined: 2026-04-02*
*Last updated: 2026-04-22 — gap closure phase 7~10 배정 (full sync in Phase 8)*
