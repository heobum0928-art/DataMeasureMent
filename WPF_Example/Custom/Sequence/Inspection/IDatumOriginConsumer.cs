//260519 hbk Phase 31 D-03 — Datum 절대좌표 주입 인터페이스
namespace ReringProject.Sequence
{
    /// <summary>
    /// Datum 절대좌표를 측정 객체에 주입하는 표준 인터페이스.
    /// Action_FAIMeasurement.EStep.Measure 에서 DatumConfig 를 찾아 주입.
    /// ParamBase INI 직렬화는 이 인터페이스 무관 (public 프로퍼티 reflection 경로).
    /// </summary>
    public interface IDatumOriginConsumer //260519 hbk Phase 31 D-03
    {
        double DatumOriginRow { get; set; }
        double DatumOriginCol { get; set; }
        double DatumAngleRad  { get; set; }
    }
}
