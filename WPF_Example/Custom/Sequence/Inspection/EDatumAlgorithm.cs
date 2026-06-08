//260423 hbk Phase 12 D-09 — Datum 알고리즘 식별자 (PropertyGrid enum 자동 드롭다운 대상)
//260423 hbk Phase 13 이동 예정 위치: WPF_Example/Halcon/Algorithms/Datum/EDatumAlgorithm.cs
//260423 hbk ParamBase switch-case가 enum을 미지원하므로 DatumConfig.AlgorithmType은 string으로 저장/로드됨.
//260423 hbk (ParamBase.cs:330-363 Save/Load 참조 — Int32/Double/String/Boolean/Rect/Line/Circle/PropertyItem[]/ModelFinderViewModel만 처리)

namespace ReringProject.Sequence
{
    public enum EDatumAlgorithm //260423 hbk Phase 12 D-09
    {
        TwoLineIntersect,           //260423 hbk 기존 Phase 4 방식 (Line1∩Line2) — default(EDatumAlgorithm)
        CircleTwoHorizontal,        //260423 hbk Circle 센터 수직 가상선 ∩ 수평 2-ROI concat
        VerticalTwoHorizontal,      //260423 hbk 수직 ROI ∩ 수평 2-ROI concat
        VerticalTwoHorizontalDualImage, //260527 hbk Phase 34 D-34-03 — 가로축 이미지(H_A+H_B) + 세로축 이미지(V) 분리 (Side fixture). enum 순서 보존(append) — 기존 string 직렬화 INI 회귀 0.
    }
}
