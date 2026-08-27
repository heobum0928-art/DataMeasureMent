using System;

namespace ReringProject {

    /// <summary>
    /// Align 정합 검증 기록 1행. ①(ALIGN) 과 ②(SEAT) 가 같은 POCO 를 공유하고 Kind 로 구분한다.
    /// 순수 POCO — 로직/HALCON/IO 없음.
    ///
    /// 행 1건 = 이벤트 1건. ①과 ②를 한 줄로 합치지 않는다.
    /// ① 은 Align 시점, ② 는 그 뒤 검사 시점에 발생하므로 합치려면 ① 을 메모리에 붙들고 ② 를 기다려야 한다.
    /// Align 이 NG 라 PLC 가 검사를 안 돌리면 ② 가 영영 안 오고 ① 기록이 통째로 유실된다.
    /// 대기열은 곧 미결 상태 누적 = 메모리다. 조인은 조회 시점에 자재번호로 한다.
    /// </summary>
    public class AlignVerifyRecord {

        public const string KIND_ALIGN = "ALIGN";
        public const string KIND_SEAT = "SEAT";

        public const string TARGET_TRAY = "TRAY";
        public const string TARGET_BOTTOM = "BOTTOM";

        public const string JUDGE_OK = "OK";
        public const string JUDGE_NG = "NG";
        public const string JUDGE_DETECT_OK = "DETECT_OK";

        /// <summary>자재번호 미수신.</summary>
        public const int NO_MATERIAL = -1;

        /// <summary>기록 시각(컬럼 0).</summary>
        public DateTime RecordTime { get; set; }

        /// <summary>KIND_ALIGN 또는 KIND_SEAT (컬럼 1).</summary>
        public string Kind { get; set; } = "";

        /// <summary>자재번호. 미수신 시 NO_MATERIAL (컬럼 2).</summary>
        public int MaterialNo { get; set; } = NO_MATERIAL;

        /// <summary>① 전용 — TARGET_TRAY / TARGET_BOTTOM (컬럼 3).</summary>
        public string Target { get; set; } = "";

        /// <summary>① 전용 — EBottomAlignSlotMap.ToFileToken(slot). Tray 는 빈칸 (컬럼 4).</summary>
        public string SlotToken { get; set; } = "";

        /// <summary>② 전용 — TOP / BOTTOM / SIDE_1~SIDE_4 (컬럼 5). 지그별 집계 단위.</summary>
        public string SequenceName { get; set; } = "";

        /// <summary>② 전용 — DatumConfig.DatumName (컬럼 6).</summary>
        public string DatumName { get; set; } = "";

        /// <summary>JUDGE_OK / JUDGE_NG / JUDGE_DETECT_OK (컬럼 7).</summary>
        public string Judgement { get; set; } = "";

        /// <summary>① 잔여 X(mm) (컬럼 8). HasResidual 이 false 면 빈칸으로 기록된다.</summary>
        public double ResidualOffsetXmm { get; set; }

        /// <summary>① 잔여 Y(mm) (컬럼 9).</summary>
        public double ResidualOffsetYmm { get; set; }

        /// <summary>① 잔여 각도(deg) (컬럼 10).</summary>
        public double ResidualThetaDeg { get; set; }

        /// <summary>① 재매칭 점수 (컬럼 11).</summary>
        public double Score { get; set; }

        /// <summary>② 검출 원점 Row(px) (컬럼 12). HasSeatOrigin 이 false 면 빈칸.</summary>
        public double DetectedRow { get; set; }

        /// <summary>② 검출 원점 Col(px) (컬럼 13).</summary>
        public double DetectedCol { get; set; }

        /// <summary>② 티칭 기준 원점 Row(px) (컬럼 14).</summary>
        public double RefRow { get; set; }

        /// <summary>② 티칭 기준 원점 Col(px) (컬럼 15).</summary>
        public double RefCol { get; set; }

        /// <summary>
        /// ② ShotConfig.PixelResolution — 단위 <b>mm/px</b> (컬럼 16).
        /// ① 의 SystemSetting.EthernetPixelResolution 은 μm/px 라 단위가 다르다. 헷갈리면 편차가 1000배 틀린다.
        /// CorrectionFactor 는 곱하지 않는다 — 측정값이 아니라 위치 증거다.
        /// </summary>
        public double PixelResolutionMmPerPx { get; set; }

        /// <summary>② Datum 검출 시각 (컬럼 17). default(DateTime) 이면 빈칸.</summary>
        public DateTime DetectTime { get; set; }

        /// <summary>실패 사유 (컬럼 18).</summary>
        public string FailReason { get; set; } = "";

        /// <summary>NG 시 저장된 보정/원본 이미지 파일명 (컬럼 19).</summary>
        public string ImageFileName { get; set; } = "";

        /// <summary>
        /// ① 행인가 — 잔여 4값이 유효한가. double 0.0 을 "빈칸" 으로 착각하지 않기 위한 플래그.
        /// </summary>
        public bool HasResidual { get; set; }

        /// <summary>
        /// ② 행인가 — 검출/기준 좌표 4값이 유효한가.
        /// </summary>
        public bool HasSeatOrigin { get; set; }
    }
}
