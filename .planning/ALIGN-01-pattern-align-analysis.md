# Datum 패턴매칭 위치보정 (ALIGN-01) — 에이전트 팀 설계 분석 (2026-06-17)

> 상태: **분석 완료 / phase 미생성**. 내일 사용자 결정(아래 §7 4문항) 후 GSD 신규 phase 생성 → discuss/plan.
> 출처: Workflow `datum-ncc-alignment-analysis` (에이전트 5개, recon 2 + analyze 2 + synth 1).

## 0. 문제 (사용자 제기)
레벨링(Phase 52)은 **기울기(회전)만** 보정한다. 그러나 실제 자재가 항상 같은 위치로 들어오지 않고 **X,Y 위치가 틀어져** 들어올 수 있다. 위치가 충분히 이탈하면 **작은 측정 ROI 들이 대상 에지를 벗어나 엉뚱한 위치를 측정**한다. → Datum 에 패턴매칭을 두어 자재 위치를 찾아 ROI 를 보정하자.

핵심 결정: 보정 범위를 **(A) x,y+tilt** vs **(B) x,y 오프셋만**.

## 1. 핵심 권고 (한 줄)
**(A) x,y+tilt 통합 보정 채택 + angle OFF 토글(=B)을 단일 파라미터(`PatternAngleExtentDeg=0`)로 보존.** 사용자가 x,y 보정을 원하는 "위치 큰 이탈" 상황은 거의 항상 tilt 를 동반하고, **바로 그때 레벨링의 tilt 보장 전제가 깨지기** 때문.

## 2. 사용자 직관 검증 ("레벨링이 tilt 잡으니 x,y 만")
참이 되려면 ① 자재별 회전변동≈0 ② 레벨링각=자재 실제 tilt ③ 위치 틀어져도 레벨링 수평 ROI 가 에지를 문다 ④ 회전중심/평행이동 분리 — **4개 동시 성립** 필요.
구조적으로 깨지는 지점:
- **레벨링 입력이 위치 이탈에 취약**: `TryComputeLevelingAngle`(InspectionSequence.cs L411)은 기준 Datum 수평 2-ROI 에지 의존. 자재가 X,Y 로 크게 틀어지면 그 ROI 가 먼저 에지 이탈 → 각도 산출 실패(무회전 폴백) 또는 오각도. **"x,y 틀어진 상황" ↔ "레벨링 신뢰 상황" 이 상호 배타적.**
- **레벨링 = 시퀀스당 1회 고정 각도**(`_levelingAngleRad`). 자재별 ±1~3° 변동 → 잔여 회전 → 긴 에지에서 lateral offset = L·sinθ 만큼 작은 ROI 미끄러짐.

(B) 실패 케이스: 레벨링 OFF / 기준 Datum 검출 실패 / 부품별 회전변동 / 큰 회전각(매칭 score 급락) / 패턴 회전중심 ≠ 레벨링 회전중심.

## 3. 권고 설계
- **엔진 = Shape 모델**(`create_shape_model`/`find_shape_model`), NCC 아님 (NCC 회전 취약). NCC 는 "회전≈0 + 에지약함 + 밝기패턴만" 한정 후순위.
- **보정량 = `vector_angle_to_rigid(refRow,Col,Angle, curRow,Col,Angle)`** (수동 translate+rotate 합성의 회전중심/순서 버그 회피).
- **적용 = ROI 좌표 변환**(`affine_trans_pixel`), 이미지 warp 아님 → 서브픽셀 무손실 + ROI 잘림 없음 + 저비용. 기존 `_datumTransforms` 채널에 합성만 → **Measure 본문 무수정**.
- **적용 시점 = EStep `Level` 단계 확장**(새 단계 신설 X). 매칭 먼저(원본 이미지) → align transform 산출. 주기 = 시퀀스당 1회(레벨링 미러).
- **레벨링과 순서/이중보정 가드**: **매칭은 반드시 보정 전 원본 grab 이미지에서.** 통합안 1(레벨링 대체) 권장 — rigid 행렬이 angle 까지 포함하므로 레벨링 이미지 회전 제거 가능. 레벨링 유지 시 warp 2회 금지(`hom_mat2d_compose` 단일 행렬). **레벨링×패턴 angle 동시 적용 금지 가드 필수.**
- **off 회귀 0**: `IsPatternAlignEnabled` 기본 false + INI 키 미존재 폴백 false(EnsurePerRoiDefaults). enabled=false → align=identity → `_datumTransforms` 무변경.
- **실패 폴백**: score<MinScore/모델 로드 실패 → identity 무보정 + 로그(lenient), abort 금지. (단 "엉뚱 측정 방지" 위해 라인별 Error 옵션 검토 — §7 Q2.)

## 4. 재사용 vs 신규
**재사용(검증된 패턴):** 레벨링 캐시 lifecycle(`_levelingAngleRad/_levelingComputed`+Set/Reset, `ClearDatumTransforms` 리셋) / transform 전파 채널(`_datumTransforms`→`TryGetDatumTransform`→`meas.TryExecute(image,transform)`→`AffineTransPoint2d`) / hom_mat2d 빌드(DatumFindingService L186~) / per-Datum bool·string 영속(`IsLevelingReference`,`TeachingImagePath`).
**신규:** `PatternMatchService`(현 코드베이스 NCC/Shape 호출 전무) / HALCON 모델 `.shm` 파일 영속(ParamBase 는 double/int/string/bool 만 직렬화 → 모델은 별도 파일, DatumConfig 엔 경로만) / 패턴 티칭 UI.
**미활용 골격(검토만):** `ModelFinderViewModel`(EAlgorithmType.PatternMatch) UI 골격만·HALCON 미구현 / Alligator 레거시 미사용. → 신규 HALCON 직결이 더 단순할 듯.

## 5. 제안 GSD Phase (ALIGN-01)
**제목:** Datum 패턴매칭 위치보정 (ALIGN-01) — 자재 X,Y(+tilt) 변위 정렬
**Goal:** 자재가 X,Y(+tilt)로 틀어져도 측정 ROI 가 대상 에지를 벗어나지 않게, Datum 에 Shape 모델 패턴매칭을 두어 위치를 찾고 측정/Datum ROI 보정. angle 보정 = `PatternAngleExtentDeg` 단일 토글 A(>0)/B(=0). OFF 시 회귀 0.
**discuss lock 가정:** 엔진=Shape / 적용=ROI 좌표변환 / 주입=`_datumTransforms` 합성 / 주기=시퀀스당 1회 / 산출=`vector_angle_to_rigid` / 매칭=원본 이미지 / 레벨링×패턴 angle 동시금지 가드.

| Wave | Plan | 내용 |
|---|---|---|
| W1 Algorithm | P1 `PatternMatchService` | create/write/read/find_shape_model + vector_angle_to_rigid → out HTuple transform. try/catch 규약 |
| W1 | P2 DatumConfig 필드+영속 | IsPatternAlignEnabled, PatternModelPath(.shm), PatternRoi_*, RefMatchRow/Col/AngleDeg, PatternMinScore, PatternAngleExtentDeg. INI 호환 + EnsurePerRoiDefaults |
| W2 Integration | P3 align 캐시+합성 | _alignOffset*/_alignAngleRad/_alignComputed + Set/Reset(ClearDatumTransforms 리셋) + HomMat2dCompose |
| W2 | P4 EStep Level 통합 | Level 단계 매칭 삽입(원본,enabled 가드) + lenient 폴백 + 레벨링 순서 정합 |
| W3 UI | P5 패턴 티칭 UI | Datum 노드 패턴 ROI 그리기 + 모델 생성/저장 + ref pose 기록 (공수 축소 시 후속 phase 분리, W1~W2 4-plan 1차 출하) |

**UAT(SIMUL 우선):** 1) 회귀0(off) 2) x,y 보정 추종 vs off 이탈 대조 3) tilt 옵션(AngleExtent>0) 4) 실패 폴백 abort 없음 5) 영속(.shm+ref pose 재시작) 6) 레벨링 공존 warp 1회/이중보정 없음.
**공수/리스크:** 중간(3~5 plan/2~3 wave, 레벨링 미러). (높음) .shm 모델 파일 영속 — 레시피 백업/복사 시 동반 누락 주의(레시피 폴더 내 저장). (중) 부호/좌표계 캘리브레이션(find_shape_model angle 반시계+rad vs Atan2 규약), 티칭 1장 affine_trans_pixel 추종 SIMUL 검증. (중) SIMUL 변형 이미지 페어 확보(Phase 41.1 이미지 부족 전례) — 합성 변형 1차 후 실데이터.

## 6. Side DualImage
별도 확정사항([[project_phase52_progress]]): Side 두 이미지는 같은 거치대 Z 포커싱만 다름 → 기울기 동일. 레벨링은 같은 각도 적용 정당. 패턴매칭 Side 범위는 §7 Q3 에서 결정.

## 7. 사용자 결정 대기 (내일) — 4문항
1. **보정 범위:** A(x,y+tilt)+angle 토글(권장) / B(x,y만) / discuss 에서 결정.
2. **매칭 주기:** 시퀀스당 1회(권장,레벨링 미러) / SHOT당 / discuss.
3. **실패 정책:** lenient 무보정 진행 / Error 정지 / 항목별 선택.
4. **Side 범위:** 1차는 단일 이미지 Datum부터(DualImage carry-over) / Side DualImage 포함 / discuss.
+ (discuss lock 권장) tilt 보정값을 측정에 실제 반영(레벨링 대체) vs 검색만 angle·보정은 레벨링에 위임 — 이중보정 가드 동작이 여기서 갈림.

## 관련 파일
- WPF_Example/Halcon/Algorithms/DatumFindingService.cs — hom_mat2d 보정 패턴, TryGetLevelingAngle
- WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs — RotateImageByAngle, AffineTransPoint2d(L45~60)
- WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs — 레벨링 캐시, _datumTransforms/TryGetDatumTransform, ClearDatumTransforms 리셋, TryComputeLevelingAngle(L411)
- WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs — EStep, Level/DatumPhase/Grab/Measure, transform 주입
- WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs — ParamBase 직렬화 한계(L11), IsLevelingReference(L43), TeachingImagePath(L38)
- WPF_Example/UI/ViewModel/ModelFinderViewModel.cs — 미활성 ModelFinder/PatternMatch 골격
