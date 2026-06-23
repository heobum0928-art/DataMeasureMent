namespace ReringProject.Setting
{
    //260623 hbk Phase 58 — AV-01: 이더넷 정렬 비전 동작 모드 (None=비활성, Tray/Bottom=활성)
    public enum EEthernetVisionMode
    {
        None   = 0,  // 기능 비활성 (연결 시도 안 함)
        Tray   = 1,  // Tray Align 모드
        Bottom = 2   // Bottom Align 모드
    }
}
