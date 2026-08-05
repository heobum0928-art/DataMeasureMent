namespace ReringProject.UI
{
    // Manual Measure(캘리퍼)에서 두 번째 점에 걸 축 제약.
    // 좌표계 주의: 뷰어 Point 는 X=column, Y=row 이다.
    public enum ECaliperMode
    {
        Free       = 0,  // 자유 방향 (기존 동작)
        Horizontal = 1,  // 수평 고정 — 끝점 Y(row)를 시작점 Y로 강제
        Vertical   = 2   // 수직 고정 — 끝점 X(column)를 시작점 X로 강제
    }
}
