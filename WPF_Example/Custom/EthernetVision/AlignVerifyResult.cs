namespace ReringProject {

    /// <summary>
    /// ① "보정 후 다시 재봄" 결과 모델. AlignShapeMatchService.RunCorrectedRecheck() 반환값.
    /// 순수 POCO — 로직/HALCON/IO 없음.
    ///
    /// 이 결과는 "검출 + 강체변환의 자기일관성" 만 검증한다.
    /// Bottom 의 ApplyPickerCenterCorrection(피커센터 기준 재표현, 부호 규약 미확정)은 여기서 검증되지 않는다.
    /// 그 구간은 ②(검사 시 Datum 원점 기록)가 잡는다. Tray/Bottom 모두 동일한 정의를 쓴다.
    /// </summary>
    public class AlignVerifyResult {

        /// <summary>재매칭까지 전부 성공해 잔여값이 유효한가. false 면 아래 수치 미사용.</summary>
        public bool Verified { get; set; }

        /// <summary>보정 후 남은 X 오프셋(mm). Col 축 — Run() 의 Col↔X 규약과 동일.</summary>
        public double ResidualOffsetXmm { get; set; }

        /// <summary>보정 후 남은 Y 오프셋(mm). Row 축 — Run() 의 Row↔Y 규약과 동일.</summary>
        public double ResidualOffsetYmm { get; set; }

        /// <summary>보정 후 남은 각도(deg). 재baseline − 기준 baseline.</summary>
        public double ResidualThetaDeg { get; set; }

        /// <summary>sqrt(X²+Y²). 화면 표시용 크기값 — RunCorrectedRecheck 가 채운다.</summary>
        public double ResidualDistanceMm { get; set; }

        /// <summary>재매칭 두 패턴 점수 중 작은 값(보수적 지표).</summary>
        public double Score { get; set; }

        /// <summary>실패 사유. 성공 시 빈 문자열.</summary>
        public string FailReason { get; set; } = "";
    }
}
