using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using AsiErp.Models;

namespace AsiErp.Services
{
    public static class BarcodeService
    {
        private static readonly string[] L_PATTERNS = {
            "0001101", "0011001", "0010011", "0111101", "0100011",
            "0110001", "0101111", "0111011", "0110111", "0001011"
        };

        private static readonly string[] G_PATTERNS = {
            "0100111", "0110011", "0011011", "0100001", "0011101",
            "0111001", "0000101", "0010001", "0001001", "0010111"
        };

        private static readonly string[] R_PATTERNS = {
            "1110010", "1100110", "1101100", "1000010", "1011100",
            "1001110", "1010000", "1000100", "1001000", "1110100"
        };

        private static readonly string[] FIRST_DIGIT_PARITY = {
            "LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG",
            "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL"
        };

        /// <summary>
        /// Generates a standard 13-digit EAN-13 barcode with valid checksum digit.
        /// Format: Country prefix 890 (India) + Category code (3 digits) + Sequence (6 digits) + Check digit (1 digit).
        /// </summary>
        public static string GenerateBarcode(string category = "Shoe", string sourceType = "M", int sequence = 1)
        {
            string catCode = category?.ToLowerInvariant() switch
            {
                "shoe" => sourceType == "M" ? "640" : "641",
                "uniform" => sourceType == "M" ? "620" : "621",
                "accessory" => "420",
                _ => "600"
            };

            string baseCode = $"890{catCode}";
            int seqMod = Math.Clamp(sequence, 1, 999999);
            string seqStr = seqMod.ToString("D6");
            string twelveDigits = $"{baseCode}{seqStr}";
            int checkDigit = CalculateEan13CheckDigit(twelveDigits);

            return $"{twelveDigits}{checkDigit}";
        }

        /// <summary>
        /// Calculates the 13th check digit for a 12-digit EAN barcode string using the standard GS1 formula.
        /// </summary>
        public static int CalculateEan13CheckDigit(string twelveDigits)
        {
            if (string.IsNullOrEmpty(twelveDigits) || twelveDigits.Length < 12)
            {
                return 0;
            }

            int sum = 0;
            for (int i = 0; i < 12; i++)
            {
                if (int.TryParse(twelveDigits[i].ToString(), out int val))
                {
                    sum += (i % 2 == 1) ? val * 3 : val;
                }
            }

            int mod = sum % 10;
            return mod == 0 ? 0 : 10 - mod;
        }

        public static bool IsValidBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return false;
            string clean = barcode.Trim();
            return clean.Length >= 6 && clean.All(char.IsLetterOrDigit);
        }

        /// <summary>
        /// Encodes a 13-digit code into 95 binary modules (true = black bar, false = white space)
        /// with standard GS1 EAN-13 start, center, and stop guards.
        /// </summary>
        public static bool[] GetEan13Modules(string barcode)
        {
            string clean = new string((barcode ?? "").Where(char.IsDigit).ToArray());
            if (clean.Length < 12) clean = clean.PadLeft(12, '0');
            if (clean.Length == 12) clean += CalculateEan13CheckDigit(clean);
            if (clean.Length > 13) clean = clean[..13];

            int firstDigit = clean[0] - '0';
            string parity = FIRST_DIGIT_PARITY[Math.Clamp(firstDigit, 0, 9)];

            var bits = new List<bool>(95);

            // Start guard: 101
            bits.Add(true); bits.Add(false); bits.Add(true);

            // Left 6 digits
            for (int i = 0; i < 6; i++)
            {
                int d = clean[i + 1] - '0';
                string pat = parity[i] == 'L' ? L_PATTERNS[d] : G_PATTERNS[d];
                foreach (char c in pat) bits.Add(c == '1');
            }

            // Center guard: 01010
            bits.Add(false); bits.Add(true); bits.Add(false); bits.Add(true); bits.Add(false);

            // Right 6 digits (including check digit)
            for (int i = 0; i < 6; i++)
            {
                int d = clean[i + 7] - '0';
                string pat = R_PATTERNS[d];
                foreach (char c in pat) bits.Add(c == '1');
            }

            // Stop guard: 101
            bits.Add(true); bits.Add(false); bits.Add(true);

            return bits.ToArray();
        }

        /// <summary>
        /// Generates an authentic, crisp vector DrawingImage of the barcode with human-readable digits.
        /// </summary>
        public static DrawingImage GenerateBarcodeImage(string barcode, double width = 240, double height = 75)
        {
            var drawingGroup = new DrawingGroup();
            using (var dc = drawingGroup.Open())
            {
                // Background
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));

                bool[] modules = GetEan13Modules(barcode);
                string cleanDigits = new string((barcode ?? "").Where(char.IsDigit).ToArray());
                if (cleanDigits.Length < 13) cleanDigits = cleanDigits.PadLeft(13, '0');

                bool isCompact = height < 50;
                double marginX = width < 180 ? 8 : 14;
                double barAreaWidth = width - (marginX * 2);
                double moduleWidth = barAreaWidth / modules.Length;
                double textSpace = isCompact ? 10 : 22;
                double barHeight = Math.Max(10, height - textSpace);
                double guardExtra = isCompact ? 2 : 4;
                double fontSize = isCompact ? 7.5 : 11;

                var blackBrush = Brushes.Black;

                for (int i = 0; i < modules.Length; i++)
                {
                    if (modules[i])
                    {
                        // Check if this module is part of guard bars
                        bool isGuard = (i < 3) || (i >= 45 && i < 50) || (i >= 92);
                        double curHeight = isGuard ? barHeight + guardExtra : barHeight;
                        double x = marginX + (i * moduleWidth);
                        dc.DrawRectangle(blackBrush, null, new Rect(x, isCompact ? 1 : 4, moduleWidth + 0.1, curHeight));
                    }
                }

                // Draw human-readable numbers under bars
                var typeface = new Typeface(new FontFamily("Consolas, Segoe UI, Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

                // Format: D  DDDDDD  DDDDDD
                string firstDigit = cleanDigits.Substring(0, 1);
                string leftGroup = cleanDigits.Substring(1, 6);
                string rightGroup = cleanDigits.Substring(7, 6);

                double textY = isCompact ? (barHeight + 1) : (barHeight + 6);
                var ftFirst = new FormattedText(firstDigit, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, blackBrush, 1.25);
                dc.DrawText(ftFirst, new Point(marginX - ftFirst.Width - (isCompact ? 2 : 3), isCompact ? (barHeight - 3) : (barHeight - 4)));

                var ftLeft = new FormattedText(leftGroup, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, blackBrush, 1.25);
                double leftCenter = marginX + (24 * moduleWidth);
                dc.DrawText(ftLeft, new Point(leftCenter - (ftLeft.Width / 2), textY));

                var ftRight = new FormattedText(rightGroup, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, blackBrush, 1.25);
                double rightCenter = marginX + (71 * moduleWidth);
                dc.DrawText(ftRight, new Point(rightCenter - (ftRight.Width / 2), textY));
            }

            return new DrawingImage(drawingGroup);
        }

        public static ProductItem? FindProductByBarcodeOrSku(IEnumerable<ProductItem> products, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;

            string clean = query.Trim();

            // 1. Exact Barcode match
            var match = products.FirstOrDefault(p => !string.IsNullOrEmpty(p.Barcode) && string.Equals(p.Barcode.Trim(), clean, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            // 2. Exact SKU match
            match = products.FirstOrDefault(p => !string.IsNullOrEmpty(p.Sku) && string.Equals(p.Sku.Trim(), clean, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            // 3. Exact ID match
            match = products.FirstOrDefault(p => string.Equals(p.Id.Trim(), clean, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            // 4. Case-insensitive substring fallback
            return products.FirstOrDefault(p => (!string.IsNullOrEmpty(p.Barcode) && p.Barcode.Contains(clean, StringComparison.OrdinalIgnoreCase)) ||
                                                 (!string.IsNullOrEmpty(p.Sku) && p.Sku.Contains(clean, StringComparison.OrdinalIgnoreCase)));
        }

        public static ProductItem? FindProductByBarcodeOrSku(string query, IEnumerable<ProductItem> products)
            => FindProductByBarcodeOrSku(products, query);
    }
}