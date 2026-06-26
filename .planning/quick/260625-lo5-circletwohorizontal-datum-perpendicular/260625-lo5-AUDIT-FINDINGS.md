# 측정/Datum/투영 코드 부호·기하 감사 — 확정 발견 5건 (배치 백로그)

**출처:** quick-260625-lo5 후속 에이전트 팀 감사 (2026-06-25 실행).
워크플로우는 분석(40여 에이전트, 발견 12건)을 완료했으나 종합 단계에서 행 → journal에서 결과 추출.
**검증 통과(real) 5건 / 반증 7건.** 사용자 확인: "datum 선은 항상 한쪽 고정, 크게 안 벗어남"
→ **5건 모두 현 운영선 미발현(잠재 위험). 활성 버그 0.** 배치로 정리.

## 근본 패턴
수평축(θ≈0, cosθ≈1)용 로직(cosθ≥0 정규화 / 수직=수평+90° 가정 / π/2 고정 / 부호없는 거리)을
직교 수직축(θ≈π/2, cosθ≈0)에 무비판 복제 + datum origin에서 먼 측정점의 레버암 증폭.
오늘 수정한 버그 1·2(커밋 a442b2b, 90071e5)가 이 패턴의 두 사례.

---

## 🔴 A-01 (HIGH 잠재) — ComputeProjectionDistance unsigned (5개 타입)
**파일:** `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs:668-705` (line 676 주석 "절대값, 부호 처리 없음")
**위임 타입 5종:** ArcEdgeDistance(:130) / CircleCenterDistance(:151) / ArcLineIntersect(:283) / CompoundCenterB(:125) / CompoundCenterC(:125)
**내용:** `Math.Sqrt(dr²+dc²)`로 크기만 반환, 방향(부호) 소실. (버그2처럼 "부호가 뒤집힘"이 아니라 "부호가 아예 없음")
**증상:** ① InvertSign=True → 항상 음수 → 영구 NG. ② 측정점이 datum선 반대편으로 넘어가면 절대값이라 합격 처리(방향성 불량 은폐).
**현 운영 미발현 이유:** 측정점이 항상 한쪽·소변형 → 크기 항상 정확. 크기는 버그1 datum각 수정으로 틸트보정도 됨.
**가드:** 이 5개 타입에 **InvertSign 켜지 말 것**(켜면 영구 NG).
**수정안:** ComputeProjectionDistance foot 오버로드(:668-705)에서 Math.Sqrt → signed 투영성분(EdgeToLineDistance:205 식, 축별 정규화)으로 교체. 한 번에 5개 타입 해결. 단 출력 변하므로 타입별 +방향(MeasureAxis 기본값) 확인 + 재티칭 필요. overlay foot 좌표 무영향.

## 🟡 D-01 (LOW) — ArcLineIntersect MeasureAxis=X 교점 혼합
**파일:** `WPF_Example/Custom/Sequence/Inspection/Measurements/ArcLineIntersectDistanceMeasurement.cs:248-273, 283-287`
**내용:** col은 선택 교점(Far/Close), row는 두 교점 **평균**을 섞음 → 틸트 시 perpendicular축 좌표가 거리에 누설. 편향 ≈ |(int1Row−int2Row)/2|·sinθ_tilt.
**크기:** 실측 I9_P1 기준 ~0.002mm (공차 0.05mm의 ~3-4%). 부호 안전. 미미.
**수정안:** 선택된 단일 교점에서 row·col **양 좌표** 모두 취하기(평균 혼합 제거). overlay 끝점도 동일점으로.

## 🟡 E-07 (LOW) — ClearDatumTransforms transient 미초기화
**파일:** `WPF_Example/.../InspectionSequence.cs:777-788`
**내용:** DetectedCircleRow/Col·RefAngle2·Origin* transient 미리셋. 세션 중 datum 타입을 CircleTwoHorizontal→타입 변경 후 재검사 시 stale 원중심 생존 → CompoundAngle(E2)의 0-가드(CompoundAngleMeasurement.cs:113) 무력화 → 오각도 산출(clean fail 대신).
**트리거:** 세션 중 datum 타입 변경 + 미reload. 좁음.
**수정안:** ClearDatumTransforms 루프에서 각 datum의 transient 검출필드 0으로 리셋(if/else, //YYMMDD hbk).

## 🟡 C-06 (LOW) — DualImage 동일프레임 가드 없음
**파일:** `WPF_Example/.../Measurements/DualImageEdgeDistanceMeasurement.cs:155-201`
**내용:** imageA(PointROI)/imageB(LineROI) projection_pl 시 두 이미지 **동일 프레임(해상도) 검증 없음**. 다른 해상도 페어 설정 시 silent 오측정. (DatumFindingService:91-98는 SameFrame 가드 있음)
**트리거:** 의도적으로 다른 해상도 이미지 페어 구성 시만. 정상 동일카메라/해상도면 무영향.
**수정안:** TryGrabOrLoadFaiDualImages 직후 GetImageSize W×H 비교 가드 + false + 로그.

## 🟡 B-02 (LOW) — sinθ2 정규화 θ2≈0/π 경계
**파일:** `WPF_Example/.../Measurements/EdgeToLineDistanceMeasurement.cs:167-169` (오늘 추가한 sinθ≥0 정규화)
**내용:** DatumAngle2Rad가 0/π 근방이면 sin(θ2)=±ε라 부호 불안정 — 단 이는 TwoLineIntersect datum(수직각이 임의방향)에서만 가능. CircleTwoHorizontal/VTH는 θ2=curAngle+90°≈π/2라 sinθ2≈1로 안정.
**현 레시피:** FAI_1은 TwoLineIntersect datum 미사용 → 미발현. 이론적.
**수정안(필요시):** measureX 부호를 origin-relative 불변량으로 결정하거나, TLI datum에서 θ2 직교성 teach 단계 검증.

---

## 반증된 7건 (참고, 조치 불요)
- DualImage InvertSign "항상 NG" = unsigned 타입에 무의미한 옵션 켠 오설정(측정결함 아님).
- 그 외 6건: 타처 가드되거나 도달 불가 조건으로 반증(journal 참조).

## 권고
현 운영(한쪽 고정·소변형)에서 **즉시 고칠 활성 버그 0**. 위 5건은 한 batch phase로 묶어
타입별 +방향 의도 확인 후 일괄 정리 권장(특히 A-01은 재티칭 동반). 그 전까지 가드 = **5개 unsigned 타입에 InvertSign off 유지**.
