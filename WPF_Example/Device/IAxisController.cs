namespace ReringProject.Device {

    /// <summary>
    /// Z축 모터 제어 인터페이스. 실제 HW 연동은 별도 Phase에서 구현.
    /// </summary>
    public interface IAxisController {
        bool MoveToPosition(double positionMm);
        bool IsMoveDone { get; }
        double CurrentPosition { get; }
    }
}
