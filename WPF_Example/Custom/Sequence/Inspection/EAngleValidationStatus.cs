namespace ReringProject.Sequence
{
    // DualImage Test Find 직후 검출 각도 PASS/FAIL 평가 상태.
    //  None = 미평가 (AngleTolerance == 0.0 sentinel).
    //  Pass = |Detected - Expected| ≤ AngleTolerance (wrap-around 정규화 후).
    //  Fail = 오차 초과.
    // 사용처: DatumConfig.AngleValidationStatus transient (INI/JSON 직렬화 제외).
    // 소비처: HalconDisplayService DrawExpectedAngleArrow (색상 분기) + PropertyGrid 색상 배지.
    public enum EAngleValidationStatus
    {
        None = 0, // sentinel: 미평가 (default 값 보장)
        Pass = 1,
        Fail = 2,
    }
}
