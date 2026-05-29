//260530 hbk Phase 39.2 D-G3 — Tree 노드 자연정렬 비교자 (Shot2 < Shot10)
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ReringProject.Utility
{
    /// <summary>
    /// 자연정렬(Natural Sort) IComparer&lt;string&gt; 구현.
    /// 사전식 비교는 "Shot10" &lt; "Shot2" 가 되는 문제를 회피하기 위해
    /// regex 로 숫자 chunk 와 텍스트 chunk 를 분리한 후 chunk 단위로 비교한다.
    ///  - 양쪽 chunk 모두 숫자 → int 로 변환해 수치 비교
    ///  - 그 외 → System.StringComparison.OrdinalIgnoreCase 로 사전식 비교
    /// chunk 수 동일까지 끝나면 chunk 수 큰 쪽이 뒤.
    /// </summary>
    public class NaturalStringComparer : IComparer<string>
    {
        // 숫자 연속 또는 비숫자 연속을 chunk 로 분리
        private static readonly Regex _chunkRegex = new Regex(@"(\d+|\D+)", RegexOptions.Compiled);

        public int Compare(string x, string y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            var xChunks = _chunkRegex.Matches(x);
            var yChunks = _chunkRegex.Matches(y);
            int n = System.Math.Min(xChunks.Count, yChunks.Count);
            for (int i = 0; i < n; i++)
            {
                string xc = xChunks[i].Value;
                string yc = yChunks[i].Value;
                int cmp;
                if (xc.Length > 0 && yc.Length > 0 && char.IsDigit(xc[0]) && char.IsDigit(yc[0]))
                {
                    int xi, yi;
                    if (int.TryParse(xc, out xi) && int.TryParse(yc, out yi))
                    {
                        cmp = xi.CompareTo(yi);
                    }
                    else
                    {
                        // overflow 등 int.TryParse 실패 시 사전식 fallback
                        cmp = string.Compare(xc, yc, System.StringComparison.OrdinalIgnoreCase);
                    }
                }
                else
                {
                    cmp = string.Compare(xc, yc, System.StringComparison.OrdinalIgnoreCase);
                }
                if (cmp != 0) return cmp;
            }
            return xChunks.Count.CompareTo(yChunks.Count);
        }
    }
}
