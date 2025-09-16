using System;
using System.Collections.Generic;
using System.Linq;

namespace escpos.core.reporthandlerwpf
{
    public static class Gs1Code128Raster
    {
        private static readonly int[][] CODE128 = new int[][]
        {
        new[]{2,1,2,2,2,2}, new[]{2,2,2,1,2,2}, new[]{2,2,2,2,2,1}, new[]{1,2,1,2,2,3}, new[]{1,2,1,3,2,2},
        new[]{1,3,1,2,2,2}, new[]{1,2,2,2,1,3}, new[]{1,2,2,3,1,2}, new[]{1,3,2,2,1,2}, new[]{2,2,1,2,1,3},
        new[]{2,2,1,3,1,2}, new[]{2,3,1,2,1,2}, new[]{1,1,2,2,3,2}, new[]{1,2,2,1,3,2}, new[]{1,2,2,2,3,1},
        new[]{1,1,3,2,2,2}, new[]{1,2,3,1,2,2}, new[]{1,2,3,2,2,1}, new[]{2,2,3,2,1,1}, new[]{2,2,1,1,3,2},
        new[]{2,2,1,2,3,1}, new[]{2,1,3,2,1,2}, new[]{2,2,3,1,1,2}, new[]{3,1,2,1,3,1}, new[]{3,1,1,2,2,2},
        new[]{3,2,1,1,2,2}, new[]{3,2,1,2,2,1}, new[]{3,1,2,2,1,2}, new[]{3,2,2,1,1,2}, new[]{3,2,2,2,1,1},
        new[]{2,1,2,1,2,3}, new[]{2,1,2,3,2,1}, new[]{2,3,2,1,2,1}, new[]{1,1,1,3,2,3}, new[]{1,3,1,1,2,3},
        new[]{1,3,1,3,2,1}, new[]{1,1,2,3,1,3}, new[]{1,3,2,1,1,3}, new[]{1,3,2,3,1,1}, new[]{2,1,1,3,1,3},
        new[]{2,3,1,1,1,3}, new[]{2,3,1,3,1,1}, new[]{1,1,2,1,3,3}, new[]{1,1,2,3,3,1}, new[]{1,3,2,1,3,1},
        new[]{1,1,3,1,2,3}, new[]{1,1,3,3,2,1}, new[]{1,3,3,1,2,1}, new[]{3,1,3,1,2,1}, new[]{2,1,1,3,3,1},
        new[]{2,3,1,1,3,1}, new[]{2,1,3,1,1,3}, new[]{2,1,3,3,1,1}, new[]{2,1,3,1,3,1}, new[]{3,1,1,1,2,3},
        new[]{3,1,1,3,2,1}, new[]{3,3,1,1,2,1}, new[]{3,1,2,1,1,3}, new[]{3,1,2,3,1,1}, new[]{3,3,2,1,1,1},
        new[]{3,1,4,1,1,1}, new[]{2,2,1,4,1,1}, new[]{4,3,1,1,1,1}, new[]{1,1,1,2,2,4}, new[]{1,1,1,4,2,2},
        new[]{1,2,1,1,2,4}, new[]{1,2,1,4,2,1}, new[]{1,4,1,1,2,2}, new[]{1,4,1,2,2,1}, new[]{1,1,2,2,1,4},
        new[]{1,1,2,4,1,2}, new[]{1,2,2,1,1,4}, new[]{1,2,2,4,1,1}, new[]{1,4,2,1,1,2}, new[]{1,4,2,2,1,1},
        new[]{2,4,1,2,1,1}, new[]{2,2,1,1,1,4}, new[]{4,1,3,1,1,1}, new[]{2,4,1,1,1,2}, new[]{1,3,4,1,1,1},
        new[]{1,1,1,2,4,2}, new[]{1,2,1,1,4,2}, new[]{1,2,1,2,4,1}, new[]{1,1,4,2,1,2}, new[]{1,2,4,1,1,2},
        new[]{1,2,4,2,1,1}, new[]{4,1,1,2,1,2}, new[]{4,2,1,1,1,2}, new[]{4,2,1,2,1,1}, new[]{2,1,2,1,4,1},
        new[]{2,1,4,1,2,1}, new[]{4,1,2,1,2,1}, new[]{1,1,1,1,4,3}, new[]{1,1,1,3,4,1}, new[]{1,3,1,1,4,1},
        new[]{1,1,4,1,1,3}, new[]{1,1,4,3,1,1}, new[]{4,1,1,1,1,3}, new[]{4,1,1,3,1,1}, new[]{1,1,3,1,4,1},
        new[]{1,1,4,1,3,1}, new[]{3,1,1,1,4,1}, new[]{4,1,1,1,3,1}, new[]{2,1,1,4,1,2}, new[]{2,1,1,2,1,4},
        new[]{2,1,1,2,3,2}, new[]{2,3,3,1,1,1,2} 
        };

        private const int START_B = 104;
        private const int FNC1 = 102;
        private const int STOP = 106;

        public static byte[] FromInputToRaster(List<(string ai, string val, bool variable)> fields, int moduleWidth = 2, int heightPx = 140)
        {
            var codewords = BuildCodewords(fields);
            return RenderRaster(codewords, moduleWidth, heightPx);
        }

        public static byte[] FromInputToRasterFit(List<(string ai, string val, bool variable)> fields, int desiredWidthDots, int heightPx, int minModule = 1, int maxModule = 6)
        {
            var codewords = BuildCodewords(fields);
            int totalModules = 0;
            for (int i = 0; i < codewords.Count; i++)
            {
                var pat = CODE128[codewords[i]];
                for (int k = 0; k < pat.Length; k++) totalModules += pat[k];
            }

            int moduleWidth = desiredWidthDots / Math.Max(1, totalModules);
            moduleWidth = Clamp(moduleWidth, minModule, maxModule);

            if (moduleWidth * totalModules > desiredWidthDots)
                moduleWidth = Math.Max(minModule, moduleWidth - 1);

            return RenderRaster(codewords, moduleWidth, heightPx);
        }

        

        private static List<int> BuildCodewords(List<(string ai, string val, bool variable)> items)
        {
            var cw = new List<int>();
            cw.Add(START_B);
            cw.Add(FNC1);

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                bool last = i == items.Count - 1;

                foreach (char c in it.ai) cw.Add(CharB(c));
                foreach (char c in it.val) cw.Add(CharB(c));

                if (it.variable && !last) cw.Add(FNC1);
            }

            int sum = cw[0];
            for (int i = 1; i < cw.Count; i++) sum += i * cw[i];
            int checksum = sum % 103;
            cw.Add(checksum);
            cw.Add(STOP);
            return cw;
        }

        private static int CharB(char c)
        {
            int a = (int)c;
            if (a < 32 || a > 126) throw new ArgumentException("Code Set B: dozvoljen ASCII 32..126.");
            return a - 32;
        }

        private static byte[] RenderRaster(List<int> codewords, int moduleWidth, int heightPx)
        {
            var modules = new List<bool>();
            for (int i = 0; i < codewords.Count; i++)
            {
                var pat = CODE128[codewords[i]];
                bool bar = true;
                foreach (int w in pat)
                {
                    for (int k = 0; k < w; k++) modules.Add(bar);
                    bar = !bar;
                }
            }

            int wPx = modules.Count * Math.Max(1, moduleWidth);
            int hPx = Math.Max(16, heightPx);
            int wBytes = (wPx + 7) / 8;
            var bmp = new byte[wBytes * hPx];

            for (int y = 0; y < hPx; y++)
            {
                int row = y * wBytes;
                int bit = 7, idx = row;
                byte cur = 0;
                for (int x = 0; x < wPx; x++)
                {
                    bool black = modules[x / moduleWidth];
                    if (black) cur |= (byte)(1 << bit);
                    bit--;
                    if (bit < 0) { bmp[idx++] = cur; cur = 0; bit = 7; }
                }
                if (bit != 7) bmp[idx] = cur;
            }

            var outBuf = new List<byte>(8 + bmp.Length + 2);
            outBuf.Add(0x1D); outBuf.Add(0x76); outBuf.Add(0x30); outBuf.Add(0x00);
            outBuf.Add((byte)(wBytes & 0xFF));
            outBuf.Add((byte)((wBytes >> 8) & 0xFF));
            outBuf.Add((byte)(hPx & 0xFF));
            outBuf.Add((byte)((hPx >> 8) & 0xFF));
            outBuf.AddRange(bmp);
            outBuf.Add(0x0A); outBuf.Add(0x0A);
            return outBuf.ToArray();
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
