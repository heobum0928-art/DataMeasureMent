namespace ReringProject.Sequence
{
    // ParamBase switch-case가 enum을 미지원하므로 DatumConfig.AlgorithmType은 string으로 저장/로드됨.
    // (ParamBase.cs Save/Load — Int32/Double/String/Boolean/Rect/Line/Circle/PropertyItem[]/ModelFinderViewModel만 처리)
    public enum EDatumAlgorithm
    {
        TwoLineIntersect,           // 기존 Line1∩Line2 방식 — default(EDatumAlgorithm)
        CircleTwoHorizontal,        // Circle 센터 수직 가상선 ∩ 수평 2-ROI concat
        VerticalTwoHorizontal,      // 수직 ROI ∩ 수평 2-ROI concat
        VerticalTwoHorizontalDualImage, // 가로축 이미지(H_A+H_B) + 세로축 이미지(V) 분리 (Side fixture). enum 순서 보존(append) — 기존 string 직렬화 INI 회귀 0.
    }
}
