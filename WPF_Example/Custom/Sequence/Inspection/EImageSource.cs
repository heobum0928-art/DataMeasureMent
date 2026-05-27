//260527 hbk Phase 34.1 D-34.1-15 — 자동/수동 swap 의 공통 인자 타입. EImageSource 단일 enum.
//  - UpdateImageSourceBadge(EImageSource) 헬퍼 시그니처 단일화 → 자동 swap (L1994-2008) + 수동 토글 (btn_swapHorizontal/Vertical_Click) 양쪽 일관성 보장.
//  - DatumConfig 변경 0 가드 (D-34-14 / D-34.1-07) 유지 위해 별도 파일.

namespace ReringProject.Sequence
{
    public enum EImageSource
    {
        Horizontal = 0,  //260527 hbk Phase 34.1 — 가로축 (TeachingImagePath). 기본값.
        Vertical   = 1   //260527 hbk Phase 34.1 — 세로축 (TeachingImagePath_Vertical, DualImage 전용).
    }
}
