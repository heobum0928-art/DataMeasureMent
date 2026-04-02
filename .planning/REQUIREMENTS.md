# Requirements: DataMeasurement

**Defined:** 2026-04-02
**Core Value:** Shot-FAI 2계층 동적 구조로 100개+ 검사 항목을 유연하게 관리하고, Halcon 에지 측정으로 정밀한 거리 측정(mm) + 공차 판정 수행

## v1 Requirements

Requirements for initial release. Each maps to roadmap phases.

### UI 재설계

- [ ] **UI-01**: TreeView에서 Shot/FAI 2계층 구조를 탐색할 수 있다
- [ ] **UI-02**: 단일 캔버스에서 선택된 Shot의 이미지를 표시한다 (기존 5탭 제거)
- [ ] **UI-03**: FAI 측정 결과(거리 mm, OK/NG 판정)를 테이블로 표시한다
- [ ] **UI-04**: Shot을 추가/삭제/수정할 수 있다
- [ ] **UI-05**: FAI를 추가/삭제/수정할 수 있다

### 티칭

- [ ] **TCH-01**: 캔버스에 FAI ROI 오버레이를 시각적으로 표시한다 (Edge 방향, 범위)
- [ ] **TCH-02**: TeachingStorageService를 통해 ROI 데이터를 저장/로드한다

### 에지 측정 알고리즘

- [ ] **ALG-01**: FAI ROI 내에서 Halcon MeasurePos로 에지 페어 거리(mm)를 계산한다
- [ ] **ALG-02**: FAIConfig의 Tolerance 기준으로 OK/NG 판정을 수행한다
- [ ] **ALG-03**: 픽셀→mm 변환을 위한 캘리브레이션 기능을 제공한다
- [ ] **ALG-04**: 측정 결과(에지 위치, 거리, 판정)를 캔버스에 오버레이로 표시한다

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
| UI-01 | — | Pending |
| UI-02 | — | Pending |
| UI-03 | — | Pending |
| UI-04 | — | Pending |
| UI-05 | — | Pending |
| TCH-01 | — | Pending |
| TCH-02 | — | Pending |
| ALG-01 | — | Pending |
| ALG-02 | — | Pending |
| ALG-03 | — | Pending |
| ALG-04 | — | Pending |
| SEQ-01 | — | Pending |
| SEQ-02 | — | Pending |
| SEQ-03 | — | Pending |
| SEQ-04 | — | Pending |

**Coverage:**
- v1 requirements: 15 total
- Mapped to phases: 0
- Unmapped: 15

---
*Requirements defined: 2026-04-02*
*Last updated: 2026-04-02 after initial definition*
