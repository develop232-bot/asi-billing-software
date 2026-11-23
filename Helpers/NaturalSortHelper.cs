using System;
using System.Text.RegularExpressions;

namespace AsiErp.Helpers
{
    public static class NaturalSortHelper
    {
        private static readonly Regex ChunkRegex = new Regex(@"(\d+|\D+)", RegexOptions.Compiled);
        private static readonly Regex NumberRegex = new Regex(@"(\d+(\.\d+)?)", RegexOptions.Compiled);

        /// <summary>
        /// Generates a normalized sort key for alphanumeric strings like SKUs.
        /// Examples:
        /// "ASI-RDY-002" -> "asi-rdy-000000000002"
        /// "ASI-RDY-10"  -> "asi-rdy-000000000010"
        /// "RM-2"        -> "rm-000000000002"
        /// "RM-10"       -> "rm-000000000010"
        /// This ensures standard string sorting sorts natural alphanumeric codes in exact numeric order!
        /// </summary>
        public static string GetSkuSortKey(string? sku)
        {
            if (string.IsNullOrWhiteSpace(sku)) return "zzzzzzzzzzzz";
            var chunks = ChunkRegex.Matches(sku.Trim().ToLowerInvariant());
            var sb = new System.Text.StringBuilder();
            foreach (Match m in chunks)
            {
                if (long.TryParse(m.Value, out long n))
                {
                    sb.Append(n.ToString("D12"));
                }
                else
                {
                    sb.Append(m.Value);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Compares two strings using natural alphanumeric sorting.
        /// </summary>
        public static int CompareNatural(string? a, string? b)
        {
            return string.Compare(GetSkuSortKey(a), GetSkuSortKey(b), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Computes a numeric rank for garment and shoe sizes.
        /// Numeric sizes: 5 -> 5.0, 7.5 -> 7.5, 12 -> 12.0
        /// Letter sizes:
        /// Free / Free Size -> 1000
        /// XS / Extra Small -> 1001
        /// S / Small        -> 1002
        /// M / Medium       -> 1003
        /// L / Large        -> 1004
        /// XL / Extra Large -> 1005
        /// XXL / 2XL        -> 1006
        /// 3XL / XXXL       -> 1007
        /// 4XL              -> 1008
        /// Default/None/—   -> 999999
        /// </summary>
        public static double GetSizeRank(string? size)
        {
            if (string.IsNullOrWhiteSpace(size) || size == "—" || size == "-")
                return 999999;

            string s = size.Trim().ToLowerInvariant();

            // Check letter / word sizes
            if (s.Contains("free")) return 1000;
            if (s == "xs" || s == "extra small") return 1001;
            if (s == "s" || s == "small") return 1002;
            if (s == "m" || s == "medium") return 1003;
            if (s == "l" || s == "large") return 1004;
            if (s == "xl" || s == "extra large") return 1005;
            if (s == "xxl" || s == "2xl" || s == "double xl") return 1006;
            if (s == "xxxl" || s == "3xl" || s == "triple xl") return 1007;
            if (s == "4xl") return 1008;

            // Direct numeric match or match within text (e.g. "Size 8", "UK 7.5", "7")
            var match = NumberRegex.Match(s);
            if (match.Success && double.TryParse(match.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double num))
            {
                return num;
            }

            return 5000; // other unclassified text
        }
    }
}
