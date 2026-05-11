//260413 hbk Phase 6: 타입 문자열 기반 Measurement 인스턴스 생성 (D-17)
namespace ReringProject.Sequence
{
    /// <summary>
    /// INI 레시피 Type= 값과 UI ComboBox 선택값을 MeasurementBase 파생 인스턴스로 매핑한다.
    /// 미등록 타입명은 null을 반환(T-06-01 완화) — 호출측에서 null 체크 후 로그+skip 처리.
    /// </summary>
    public static class MeasurementFactory //260413 hbk
    {
        public static MeasurementBase Create(string typeName, object owner) //260413 hbk
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            switch (typeName)
            {
                case "EdgePairDistance":
                    return new EdgePairDistanceMeasurement(owner);
                case "PointToLineDistance":
                    return new PointToLineDistanceMeasurement(owner);
                case "PointToPointDistance":
                    return new PointToPointDistanceMeasurement(owner);
                case "LineToLineAngle":
                    return new LineToLineAngleMeasurement(owner);
                case "CircleDiameter":
                    return new CircleDiameterMeasurement(owner);
                case "LineToLineDistance":
                    return new LineToLineDistanceMeasurement(owner);
                case "EdgeToLineDistance": //260512 hbk Phase 23 ALG-01
                    return new EdgeToLineDistanceMeasurement(owner); //260512 hbk Phase 23 ALG-01
                default:
                    return null;
            }
        }

        public static string[] GetTypeNames() //260413 hbk UI ComboBox용
        {
            return new string[]
            {
                "EdgePairDistance",
                "PointToLineDistance",
                "PointToPointDistance",
                "LineToLineAngle",
                "CircleDiameter",
                "LineToLineDistance",
                "EdgeToLineDistance" //260512 hbk Phase 23 ALG-01
            };
        }
    }
}
