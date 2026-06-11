namespace ReringProject.Sequence
{
    /// <summary>
    /// INI 레시피 Type= 값과 UI ComboBox 선택값을 MeasurementBase 파생 인스턴스로 매핑한다.
    /// 미등록 타입명은 null을 반환 — 호출측에서 null 체크 후 로그+skip 처리.
    /// </summary>
    public static class MeasurementFactory
    {
        public static MeasurementBase Create(string typeName, object owner)
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
                case "EdgeToLineDistance":
                    return new EdgeToLineDistanceMeasurement(owner);
                case "CircleCenterDistance":
                    return new CircleCenterDistanceMeasurement(owner);
                case "EdgeToLineAngle":
                    return new EdgeToLineAngleMeasurement(owner);
                case "ArcEdgeDistance":
                    return new ArcEdgeDistanceMeasurement(owner);
                case "ArcLineIntersectDistance":
                    return new ArcLineIntersectDistanceMeasurement(owner);
                case "CompoundAngle":
                    return new CompoundAngleMeasurement(owner);
                case "CompoundCenterCDistance":
                    return new CompoundCenterCDistanceMeasurement(owner);
                case "CompoundCenterBDistance":
                    return new CompoundCenterBDistanceMeasurement(owner);
                case "CompoundShortAxisDistance":
                    return new CompoundShortAxisDistanceMeasurement(owner);
                case "DualImageEdgeDistance":
                    return new DualImageEdgeDistanceMeasurement(owner);
                default:
                    return null;
            }
        }

        // 미사용 5종(EdgePairDistance/PointToLineDistance/PointToPointDistance/LineToLineAngle/LineToLineDistance) UI 숨김.
        // Create() switch 는 INI 하위호환을 위해 5종 case 그대로 유지.
        public static string[] GetTypeNames() // UI ComboBox용
        {
            return new string[]
            {
                "CircleDiameter",
                "EdgeToLineDistance",
                "CircleCenterDistance",
                "EdgeToLineAngle",
                "ArcEdgeDistance",
                "ArcLineIntersectDistance",
                "CompoundAngle",
                "CompoundCenterCDistance",
                "CompoundCenterBDistance",
                "CompoundShortAxisDistance",
                "DualImageEdgeDistance"
            };
        }
    }
}
