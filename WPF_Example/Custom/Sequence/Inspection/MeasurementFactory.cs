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
                case "CircleCenterDistance": //260519 hbk Phase 31 D-01 E8
                    return new CircleCenterDistanceMeasurement(owner); //260519 hbk Phase 31 D-01 E8
                case "EdgeToLineAngle": //260519 hbk Phase 31 D-05
                    return new EdgeToLineAngleMeasurement(owner); //260519 hbk Phase 31 D-05
                case "ArcEdgeDistance": //260519 hbk Phase 31 D-08
                    return new ArcEdgeDistanceMeasurement(owner); //260519 hbk Phase 31 D-08
                case "ArcLineIntersectDistance": //260519 hbk Phase 31 D-01
                    return new ArcLineIntersectDistanceMeasurement(owner); //260519 hbk Phase 31 D-01
                case "CompoundAngle": //260519 hbk Phase 31 D-11
                    return new CompoundAngleMeasurement(owner); //260519 hbk Phase 31 D-11
                case "CompoundCenterCDistance": //260519 hbk Phase 31 D-11
                    return new CompoundCenterCDistanceMeasurement(owner); //260519 hbk Phase 31 D-11
                case "CompoundCenterBDistance": //260519 hbk Phase 31 D-11
                    return new CompoundCenterBDistanceMeasurement(owner); //260519 hbk Phase 31 D-11
                case "CompoundShortAxisDistance": //260523 hbk Phase 32 — E3 단축 환원
                    return new CompoundShortAxisDistanceMeasurement(owner); //260523 hbk Phase 32 — E3 단축 환원
                case "DualImageEdgeDistance": //260530 hbk Phase 39.2 D-G1 — Bottom E5
                    return new DualImageEdgeDistanceMeasurement(owner); //260530 hbk Phase 39.2 D-G1
                default:
                    return null;
            }
        }

        //260528 hbk Phase 38 #1 — D-01/D-02/D-03: 미사용 5종(EdgePairDistance/PointToLineDistance/PointToPointDistance/LineToLineAngle/LineToLineDistance) UI 숨김
        // Create() switch 는 INI 하위호환을 위해 5종 case 그대로 유지
        public static string[] GetTypeNames() //260413 hbk UI ComboBox용
        {
            return new string[]
            {
                "CircleDiameter",
                "EdgeToLineDistance", //260512 hbk Phase 23 ALG-01
                "CircleCenterDistance", //260519 hbk Phase 31 D-01 E8
                "EdgeToLineAngle", //260519 hbk Phase 31 D-05
                "ArcEdgeDistance", //260519 hbk Phase 31 D-08
                "ArcLineIntersectDistance", //260519 hbk Phase 31 D-01 I9/I10
                "CompoundAngle", //260519 hbk Phase 31 D-11 E2
                "CompoundCenterCDistance", //260519 hbk Phase 31 D-11 E9
                "CompoundCenterBDistance", //260519 hbk Phase 31 D-11 E10
                "CompoundShortAxisDistance", //260523 hbk Phase 32 — E3 단축 환원
                "DualImageEdgeDistance" //260530 hbk Phase 39.2 D-G1 — Bottom E5
            };
        }
    }
}
