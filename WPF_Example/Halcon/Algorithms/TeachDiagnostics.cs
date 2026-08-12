//quick-260812: 티칭 실패/품질 진단 표시 헬퍼. 순수 표시(문자열/색) 레이어 —
// 이 파일에는 판정도 HALCON 호출도 없다. 어떤 메서드도 pass/fail 을 만들거나 바꾸지 않는다.
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace ReringProject.Halcon.Algorithms
{
    /// <summary>
    /// 티칭 결과 등급. Good = 기준보다 여유 있음, Weak = 간신히 통과(주의), Bad = 기준 미달/실패.
    /// </summary>
    public enum ETeachGrade
    {
        Good,
        Weak,
        Bad
    }

    /// <summary>
    /// 티칭 실패 원문(서비스 계층 영문 메시지 / HALCON 예외)을 운영자용 한국어 문구로 바꾸고,
    /// 등급별 상태 문구와 색을 제공하는 표시 전용 헬퍼.
    /// 사전은 PatternMatchService / AlignShapeMatchService 가 코드에서 직접 만든 문자열만 담는다
    /// (키워드 Contains 추측 매칭 금지 — 오역이 오진단으로 이어지기 때문).
    /// </summary>
    public static class TeachDiagnostics
    {
        // 등급 색 (초록 #16A34A / 주황 #D97706 / 빨강 #DC2626)
        private static readonly Brush BRUSH_GOOD = MakeFrozenBrush(0x16, 0xA3, 0x4A);
        private static readonly Brush BRUSH_WEAK = MakeFrozenBrush(0xD9, 0x77, 0x06);
        private static readonly Brush BRUSH_BAD  = MakeFrozenBrush(0xDC, 0x26, 0x26);

        // 점수 여유 임계값 — minScore 보다 이만큼 이상 남으면 Good, 남긴 했지만 이보다 적으면 Weak.
        // 보수적 초기값, 현장 데이터로 튜닝 예정.
        private const double GOOD_MARGIN = 0.15;

        // 중첩 접두부 해석 최대 깊이 (비정상 입력에서 무한 재귀 방지)
        private const int MAX_UNWRAP_DEPTH = 3;

        private const string MSG_EMPTY   = "원인이 기록되지 않은 오류입니다. 이 화면을 캡처해서 담당자에게 전달하세요.";
        private const string MSG_UNKNOWN = "원인을 정확히 알 수 없는 오류입니다. 이 화면을 캡처해서 담당자에게 전달하세요.";

        // ── 사전 1: 코드가 만든 "고정 전문" 그대로 매칭 (Ordinal 완전일치) ─────────────
        private static readonly Dictionary<string, string> EXACT_MESSAGES =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // PatternMatchService.cs
            { "templateImage is null",
              "티칭에 쓸 이미지가 없습니다. 먼저 Grab 또는 이미지 불러오기를 하세요." },
            { "runtimeImage is null",
              "검사할 이미지가 없습니다. 먼저 Grab 또는 이미지 불러오기를 하세요." },
            { "modelPath is null or empty",
              "패턴 모델을 저장할 경로를 만들지 못했습니다. 레시피가 선택돼 있는지, Shot 이름이 비어있지 않은지 확인하세요." },
            { "no match found (empty result)",
              "패턴을 한 곳도 찾지 못했습니다. 조명과 초점이 티칭할 때와 같은지, 부품이 검색 범위 안에 들어와 있는지 확인하세요." },

            // AlignShapeMatchService.cs
            { "img is null",
              "이미지가 없습니다. 먼저 Grab 을 하세요." },
            { "mode is None",
              "비전 모드(Tray / Bottom)가 정해지지 않았습니다. 탭을 다시 선택하세요." },
            { "RecipeSavePath 미설정",
              "레시피 저장 폴더가 설정돼 있지 않습니다. 설정 창에서 RecipeSavePath 를 먼저 지정하세요." },
            { "jsonPath 산출 실패",
              "기준값(JSON) 저장 경로를 만들지 못했습니다. 레시피 저장 폴더와 레시피 이름을 확인하세요." },
            { "angle_lx 산출 실패 (두 중심 동일 위치 또는 HALCON 오류)",
              "두 패턴의 기울기를 계산하지 못했습니다. 패턴 1 과 패턴 2 가 서로 충분히 떨어진 위치(가급적 반대 대각)에 오도록 다시 그리세요." },
            { "두 패턴 contour 변환 모두 실패",
              "찾은 패턴의 외곽선을 화면에 그리지 못했습니다. 티칭 결과 자체에는 영향이 없습니다." },
        };

        // ── 사전 2: "고정 접두부 + 안쪽 원인" 형태 → 접두부 해석 후 나머지를 재귀 해석 ──
        private static readonly List<KeyValuePair<string, string>> NESTED_PREFIXES =
            new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("TryCreateModel[1]: ",           "패턴 1 모델을 만들지 못했습니다"),
            new KeyValuePair<string, string>("TryCreateModel[2]: ",           "패턴 2 모델을 만들지 못했습니다"),
            new KeyValuePair<string, string>("TryFindRefPose[1]: ",           "패턴 1 의 기준 위치를 기록하지 못했습니다"),
            new KeyValuePair<string, string>("TryFindRefPose[2]: ",           "패턴 2 의 기준 위치를 기록하지 못했습니다"),
            new KeyValuePair<string, string>("TrySaveRefPose: ",              "기준값 파일을 저장하지 못했습니다"),
            new KeyValuePair<string, string>("TrySaveCoax: ",                 "동축 조명 설정을 저장하지 못했습니다"),
            new KeyValuePair<string, string>("TryTeach exception: ",          "티칭 도중 예기치 못한 오류가 났습니다"),
            new KeyValuePair<string, string>("TryBuildDetectedContourXld: ",  "찾은 패턴의 외곽선을 만들지 못했습니다"),
            new KeyValuePair<string, string>("TryBuildMovedContour: ",        "패턴 외곽선을 검출 위치로 옮기지 못했습니다"),
        };

        // ── 사전 3: "고정 접두부 + 수치" 형태 → 수치는 해석하지 않고 괄호로 병기 ────────
        private static readonly List<KeyValuePair<string, string>> VALUE_PREFIXES =
            new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("NCC ref find: no match above minScore=",
                "방금 만든 패턴을 티칭 이미지에서 다시 찾지 못했습니다. 패턴 ROI 를 무늬가 뚜렷한 곳으로 옮기거나 최소 점수를 낮추세요."),
            new KeyValuePair<string, string>("Shape ref find: no match above minScore=",
                "방금 만든 패턴을 티칭 이미지에서 다시 찾지 못했습니다. 패턴 ROI 를 무늬가 뚜렷한 곳으로 옮기거나 최소 점수를 낮추세요."),
            new KeyValuePair<string, string>("match score ",
                "패턴은 찾았지만 닮은 정도가 기준 점수에 못 미칩니다. 조명과 초점이 티칭할 때와 같은지 확인하거나 최소 점수를 낮추세요."),
        };

        /// <summary>
        /// 매칭 점수가 최소 기준보다 얼마나 여유 있는지로 등급을 매긴다.
        /// 이번 Quick 에서는 호출부가 없다 — Quick #2(점수 등급 실제 배선)가 그대로 소비할 인프라다.
        /// 판정에 관여하지 않는다: 여기서 Bad 가 나와도 검사 pass/fail 은 전혀 바뀌지 않는다.
        /// </summary>
        public static ETeachGrade ClassifyScore(double score, double minScore)
        {
            if (double.IsNaN(score) || double.IsNaN(minScore))
            {
                return ETeachGrade.Bad;
            }
            double margin = score - minScore;
            if (margin < 0.0)
            {
                return ETeachGrade.Bad;
            }
            if (margin < GOOD_MARGIN)
            {
                return ETeachGrade.Weak;
            }
            return ETeachGrade.Good;
        }

        /// <summary>
        /// 티칭 실패 원문을 운영자용 한국어 문구로 바꾼다.
        /// 사전에 없으면 숨기지 않고 "(원본: ...)" 로 원문을 병기한다.
        /// </summary>
        public static string ToKoreanMessage(string rawError)
        {
            return Translate(rawError, 0);
        }

        private static string Translate(string rawError, int depth)
        {
            if (string.IsNullOrEmpty(rawError))
            {
                return MSG_EMPTY;
            }

            string trimmed = rawError.Trim();

            string exact;
            if (EXACT_MESSAGES.TryGetValue(trimmed, out exact))
            {
                return exact;
            }

            if (depth < MAX_UNWRAP_DEPTH)
            {
                foreach (KeyValuePair<string, string> entry in NESTED_PREFIXES)
                {
                    if (trimmed.StartsWith(entry.Key, StringComparison.Ordinal))
                    {
                        string inner = trimmed.Substring(entry.Key.Length);
                        return entry.Value + " — " + Translate(inner, depth + 1);
                    }
                }
            }

            foreach (KeyValuePair<string, string> entry in VALUE_PREFIXES)
            {
                if (trimmed.StartsWith(entry.Key, StringComparison.Ordinal))
                {
                    string numbers = trimmed.Substring(entry.Key.Length);
                    return entry.Value + " (수치: " + numbers + ")";
                }
            }

            return MSG_UNKNOWN + " (원본: " + trimmed + ")";
        }

        /// <summary>
        /// 등급 기호를 붙인 한 줄 상태 문구. ● = 양호, ▲ = 주의, ✕ = 실패.
        /// </summary>
        public static string ToStatusLine(ETeachGrade grade, string message)
        {
            string mark;
            if (grade == ETeachGrade.Good)
            {
                mark = "●";
            }
            else if (grade == ETeachGrade.Weak)
            {
                mark = "▲";
            }
            else
            {
                mark = "✕";
            }

            if (string.IsNullOrEmpty(message))
            {
                return mark;
            }
            return mark + " " + message;
        }

        /// <summary>
        /// 등급별 글자색. 실패 후 성공 시 색이 남지 않도록 호출부는 Text 대입마다 이 값을 함께 지정한다.
        /// </summary>
        public static Brush GradeBrush(ETeachGrade grade)
        {
            if (grade == ETeachGrade.Good)
            {
                return BRUSH_GOOD;
            }
            if (grade == ETeachGrade.Weak)
            {
                return BRUSH_WEAK;
            }
            return BRUSH_BAD;
        }

        // Freeze: UI 스레드 외에서 만들어져도 안전하게 공유되도록 고정한다.
        private static Brush MakeFrozenBrush(byte r, byte g, byte b)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
