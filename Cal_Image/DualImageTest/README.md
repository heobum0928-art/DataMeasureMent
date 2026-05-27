# Cal_Image/DualImageTest/ — Phase 34.1 SIMUL 의사 페어

Phase 34.1 (Datum DualImage swap UX) 의 SIMUL UAT 용 의사 듀얼 이미지 페어 배치 폴더.

## 배경

Phase 34 (Datum VerticalTwoHorizontal 듀얼 티칭 이미지 변형) 의 신규 algorithm
`VerticalTwoHorizontalDualImage` 는 가로축 ROI 2개 (HorizontalA + HorizontalB) 를 이미지 #1 에서,
세로축 ROI 1개 (Vertical) 를 이미지 #2 에서 검출한다.

Side fixture 실측 페어는 장비 도착 후 별도 확보 예정 (CO-34.1-01 carry-over).
Phase 34.1 sign-off 는 SIMUL 의사 페어 (Top 가로 1장 + Side 세로 1장) 로 종결한다 (D-34.1-16).

## 정정 컨텍스트 (D-34.1-17 정정 2026-05-27)

당초 CONTEXT D-34.1-17 잠금 = "Plan 1 의 마지막 task 에서 executor 가 결정적 조작으로 기존
Cal_Image 의 Top/Side 이미지를 본 폴더로 복사 + 의미 있는 파일명 부여 → 사용자 수동 준비 부담 0."
그러나 planning 단계 실측 결과 `Cal_Image/Top/` 및 `Cal_Image/Side/` 폴더가 워크트리에 존재하지
않고 git-tracked 후보 2장도 working-tree deleted 상태로 확인됨. 이에 따라 **D-34.1-17 정정
(CONTEXT.md L59)** 으로 "사용자 수동 준비 부담 0" 잠금 해제 + Plan 1 = 폴더+README 생성만,
실제 이미지 페어 배치는 Plan 02 UAT 사전 준비 단계 사용자 책임으로 재정의됨. 본 README 가
그 가이드 역할.

## 사용자 수동 배치 단계 (Phase 34.1 SIMUL UAT 직전)

1. **가로축 이미지** (1장): Top 카메라 시뮬 이미지 중 가로 에지가 명확한 1장을 복사.
   - 권장 파일명: `top_horizontal.bmp` 또는 `top_horizontal.jpg`
   - 출처 후보: 기존 Top 시뮬 이미지 또는 Wafer/LEFT_1227, Right_1227 의 가로면이 보이는 이미지

2. **세로축 이미지** (1장): Side 카메라 시뮬 이미지 중 세로 에지가 명확한 1장을 복사.
   - 권장 파일명: `side_vertical.bmp` 또는 `side_vertical.jpg`
   - 출처 후보: Cal_Image/Side/ (없으면 임의의 세로 에지 보이는 시뮬 이미지 1장)

3. **검증**: 두 이미지가 본 폴더에 존재하면 UAT Test 7 (D-34.1-16) 진입 준비 완료.

## UAT 진입 경로 (Plan 2)

1. 앱 실행 → InspectionListView 에서 Datum 노드 1개 선택.
2. PropertyGrid 의 AlgorithmType = `VerticalTwoHorizontalDualImage` 로 변경.
3. PropertyGrid 의 TeachingImagePath = `Cal_Image/DualImageTest/top_horizontal.{ext}` (절대 경로).
4. PropertyGrid 의 TeachingImagePath_Vertical = `Cal_Image/DualImageTest/side_vertical.{ext}`.
5. 캔버스 툴바의 `[👁 가로]` 토글 → 가로축 이미지 + HA/HB ROI 표시.
6. `[👁 세로]` 토글 → 세로축 이미지 + Vertical ROI 표시.
7. Test Find → Datum 결합 PASS.

## 변경 가드

본 폴더는 Phase 34.1 sign-off 후에도 SIMUL UAT 재현용으로 보존.
Phase 27 (Side Inspection 확장) 또는 CO-34.1-01 종결 시점에 실측 페어로 교체될 수 있음.
