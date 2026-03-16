# DDA 설비 개요 (Vision 시스템)

## 한줄 요약
DDA 설비는 Wafer의 Map Matching을 기반으로 **Die 분리(Detach) 전 상부 검사**와 **Die 하부 검사**를 수행해 불량 Die를 검출하는 비전 검사 장비이다.

## 목적/대상
- Wafer 내 Die의 위치/맵 정합(Map Matching) 수행
- Die 분리 전 상부 검사 및 분리 후/하부 검사 수행
- Wafer 상의 Die 불량 검출

## 주요 기능 (매뉴얼 기반)
- 사용자 로그인 권한에 따라 메뉴/기능 활성화 (Operator / Admin)
- Recipe 관리
- Recipe 불러오기/저장/다른 이름으로 저장
- Recipe 설정(관리자 권한 필요)
- 수동 테스트(Manual Run)
- Wafer 검사 파라미터 확인
- Die 크기(Width/Height), Scrib X/Y, Wafer 회전(0/180)
- 원본 Wafer 이미지 저장 여부
- BIN 번호 옵션 및 위치 표시 여부
- Model ArcLength / Min ArcLength
- Die 비율(Width/Height, Area) 및 판정 기준
- Binary / Morphology 임계값 및 적용/저장 여부
- Map 파일명, Teaching 이미지 사용 여부
- 검사 ROI 선택(Inner/Outer/Both)
- Teaching 모드(모델 변경 시 이미지 기반 설정)
- Wafer 외곽 원 탐색(Circle Finder)
- Die 모델 탐색(Model Finder)
- 모델 검색 파라미터(Score, Scaling, Rotation 등)

## 화면/운영 흐름 (요약)
1. Operator / Admin 로그인
2. Recipe 불러오기 및 파라미터 확인
3. 필요 시 Teaching 모드로 모델/ROI 설정
4. 검사 실행 및 결과 확인

## 화면 구성 요소 (발췌 요약)
- State Check: 카메라 상태/건강 상태 확인
- Main Menu: 프로그램 실행 및 설정
- Camera Viewer: 검사 영상 확인
- Recipe Viewer: 현재 Recipe 확인
- Manual Inspection: 수동 검사
- Save Recipe / Setting Recipe
- Operator 로그인

## 운용/주의 사항 (매뉴얼 발췌 요지)
- 장비 교육을 이수한 작업자만 운용 권장
- 수리/문제 발생 시 제조사 A/S 지원 요청
- 사양 및 매뉴얼 내용은 사전 고지 없이 변경될 수 있음

## 참고 파일
- Document/Manual/DDA_Vision_User_Manual_ver1.0.docx
- Document/Manual/DDA_Operation_Manual_v1.1.pptx
- (추출본) Document/Manual/_docx_text.txt
- (추출본) Document/Manual/_pptx_text.txt
