//260528 hbk Phase 36 D-36-08 — DualImage Test Find 직후 검출 각도 PASS/FAIL 평가 상태.
//  None = 미평가 (AngleTolerance == 0.0 sentinel, D-36-13).
//  Pass = |Detected - Expected| ≤ AngleTolerance (wrap-around 정규화 후).
//  Fail = 오차 초과.
//  사용처: DatumConfig.AngleValidationStatus transient (INI/JSON 직렬화 제외 — Phase 17 D-13 3-종 데코).
//  소비처: HalconDisplayService DrawExpectedAngleArrow (색상 분기) + PropertyGrid 색상 배지 (Plan 03).
namespace ReringProject.Sequence
{
    public enum EAngleValidationStatus
    {
        None = 0, //260528 hbk Phase 36 D-36-08 — sentinel: 미평가 (default 값 보장)
        Pass = 1, //260528 hbk Phase 36 D-36-08
        Fail = 2, //260528 hbk Phase 36 D-36-08
    }
}
