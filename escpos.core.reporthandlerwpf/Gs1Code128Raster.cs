using System;
using System.Collections.Generic;
using System.Linq;
using ESCPOS;

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

        public static byte[] FromInputToRasterFit(List<(string ai, string val, bool variable)> fields, int desiredWidthDots, int heightPx, int minModule = 1, int maxModule = 6, bool rotated90 = false)
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

            if (rotated90)
            {
                // For rotated barcode, swap the parameters:
                // heightPx becomes the width of rotated barcode (vertical extent)
                // desiredWidthDots becomes the height of rotated barcode (horizontal extent)
                return FromInputToRasterRotatedWithSize(fields, heightPx, desiredWidthDots, minModule, maxModule);
            }
            else
            {
                return RenderRaster(codewords, moduleWidth, heightPx);
            }
        }

        public static byte[] FromInputToRasterFitRotated(List<(string ai, string val, bool variable)> fields, int desiredWidthDots, int heightPx, int minModule = 1, int maxModule = 6)
        {
            return FromInputToRasterFit(fields, desiredWidthDots, heightPx, minModule, maxModule, true);
        }

        /// <summary>
        /// Generates a 90-degree rotated GS1 Code128 barcode with explicit width and height control
        /// </summary>
        /// <param name="fields">GS1 data fields</param>
        /// <param name="rotatedWidthDots">Desired width of the rotated barcode in dots</param>
        /// <param name="rotatedHeightDots">Desired height of the rotated barcode in dots</param>
        /// <param name="minModule">Minimum module width</param>
        /// <param name="maxModule">Maximum module width</param>
        /// <returns>ESC/POS raster command for rotated barcode</returns>
        public static byte[] FromInputToRasterRotatedWithSize(List<(string ai, string val, bool variable)> fields, int rotatedWidthDots, int rotatedHeightDots, int minModule = 1, int maxModule = 6)
        {
            var codewords = BuildCodewords(fields);
            int totalModules = 0;
            for (int i = 0; i < codewords.Count; i++)
            {
                var pat = CODE128[codewords[i]];
                for (int k = 0; k < pat.Length; k++) totalModules += pat[k];
            }

            // For rotated barcode, the height controls how wide each module line is
            int moduleWidth = rotatedHeightDots / Math.Max(1, totalModules);
            moduleWidth = Clamp(moduleWidth, minModule, maxModule);

            if (moduleWidth * totalModules > rotatedHeightDots)
                moduleWidth = Math.Max(minModule, moduleWidth - 1);

            return RenderRasterRotatedExplicit(codewords, moduleWidth, rotatedWidthDots, rotatedHeightDots);
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

            // Add quiet zones (white space) before and after barcode
            int quietZoneWidth = Math.Max(10, moduleWidth * 10); // 10 modules quiet zone
            var modulesWithQuietZone = new List<bool>();
            
            // Left quiet zone
            for (int i = 0; i < quietZoneWidth; i++)
            {
                modulesWithQuietZone.Add(false); // white
            }
            
            // Barcode modules
            modulesWithQuietZone.AddRange(modules);
            
            // Right quiet zone
            for (int i = 0; i < quietZoneWidth; i++)
            {
                modulesWithQuietZone.Add(false); // white
            }

            int wPx = modulesWithQuietZone.Count * Math.Max(1, moduleWidth);
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
                    bool black = modulesWithQuietZone[x / moduleWidth];
                    if (black) cur |= (byte)(1 << bit);
                    bit--;
                    if (bit < 0) { bmp[idx++] = cur; cur = 0; bit = 7; }
                }
                if (bit != 7) bmp[idx] = cur;
            }

            return CreateRasterCommand(wBytes, hPx, bmp);
        }

        private static byte[] RenderRasterRotated(List<int> codewords, int moduleWidth, int heightPx, List<(string ai, string val, bool variable)> fields)
        {
            // Generate barcode modules
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

            // Add quiet zones (white space) before and after barcode
            int quietZoneWidth = Math.Max(10, moduleWidth * 10); // 10 modules quiet zone
            var modulesWithQuietZone = new List<bool>();
            
            // Left quiet zone
            for (int i = 0; i < quietZoneWidth; i++)
            {
                modulesWithQuietZone.Add(false); // white
            }
            
            // Barcode modules
            modulesWithQuietZone.AddRange(modules);
            
            // Right quiet zone
            for (int i = 0; i < quietZoneWidth; i++)
            {
                modulesWithQuietZone.Add(false); // white
            }

            int originalBarcodeWidth = modulesWithQuietZone.Count * Math.Max(1, moduleWidth);
            int originalBarcodeHeight = Math.Max(16, heightPx);
            
            // For 90-degree rotation, width and height are swapped
            // Original width becomes rotated height, original height becomes rotated width
            int rotatedWidth = originalBarcodeHeight;   // Original height becomes new width
            int rotatedHeight = originalBarcodeWidth;   // Original width becomes new height

            int wBytes = (rotatedWidth + 7) / 8;
            var bmp = new byte[wBytes * rotatedHeight];

            // Render rotated barcode (90 degrees clockwise)
            // We need to expand each module vertically (in rotated coordinates)
            for (int y = 0; y < rotatedHeight; y++)
            {
                for (int x = 0; x < rotatedWidth; x++)
                {
                    bool isBlack = false;

                    // Map rotated coordinates back to original coordinates
                    if (x < originalBarcodeHeight && y < originalBarcodeWidth)
                    {
                        // For 90-degree clockwise rotation:
                        // rotated_x corresponds to original_y (height position)
                        // rotated_y corresponds to original_x (module position)
                        int originalModuleIndex = y / moduleWidth;
                        int originalHeightPosition = originalBarcodeHeight - 1 - x;
                        
                        // Check if we're within valid module range and height range
                        if (originalModuleIndex < modulesWithQuietZone.Count && 
                            originalHeightPosition >= 0 && 
                            originalHeightPosition < originalBarcodeHeight)
                        {
                            isBlack = modulesWithQuietZone[originalModuleIndex];
                        }
                    }

                    // Set pixel in bitmap
                    if (isBlack)
                    {
                        int byteIndex = y * wBytes + (x / 8);
                        int bitIndex = 7 - (x % 8);
                        if (byteIndex < bmp.Length)
                        {
                            bmp[byteIndex] |= (byte)(1 << bitIndex);
                        }
                    }
                }
            }

            return CreateRasterCommand(wBytes, rotatedHeight, bmp);
        }

        /// <summary>
        /// Renders rotated barcode with explicit width and height control
        /// </summary>
        private static byte[] RenderRasterRotatedExplicit(List<int> codewords, int moduleWidth, int rotatedWidthDots, int rotatedHeightDots)
        {
            // Generate barcode modules
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

            // Add quiet zones (white space) before and after barcode
            int quietZoneWidth = Math.Max(10, moduleWidth * 10); // 10 modules quiet zone
            var modulesWithQuietZone = new List<bool>();
            
            // Left quiet zone
            for (int i = 0; i < quietZoneWidth; i++)
            {
                modulesWithQuietZone.Add(false); // white
            }
            
            // Barcode modules
            modulesWithQuietZone.AddRange(modules);
            
            // Right quiet zone
            for (int i = 0; i < quietZoneWidth; i++)
            {
                modulesWithQuietZone.Add(false); // white
            }

            int wBytes = (rotatedWidthDots + 7) / 8;
            var bmp = new byte[wBytes * rotatedHeightDots];

            // Calculate the actual barcode dimensions with quiet zones
            int actualBarcodeWidth = modulesWithQuietZone.Count * moduleWidth;

            // Render rotated barcode (90 degrees clockwise)
            for (int y = 0; y < rotatedHeightDots; y++)
            {
                for (int x = 0; x < rotatedWidthDots; x++)
                {
                    bool isBlack = false;

                    // Map rotated coordinates to module position
                    if (y < actualBarcodeWidth && x < rotatedWidthDots)
                    {
                        int moduleIndex = y / moduleWidth;
                        
                        // Check if we're within valid module range
                        if (moduleIndex < modulesWithQuietZone.Count)
                        {
                            isBlack = modulesWithQuietZone[moduleIndex];
                        }
                    }

                    // Set pixel in bitmap
                    if (isBlack)
                    {
                        int byteIndex = y * wBytes + (x / 8);
                        int bitIndex = 7 - (x % 8);
                        if (byteIndex < bmp.Length)
                        {
                            bmp[byteIndex] |= (byte)(1 << bitIndex);
                        }
                    }
                }
            }

            return CreateRasterCommand(wBytes, rotatedHeightDots, bmp);
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// Creates ESC/POS GS v 0 raster graphics command
        /// </summary>
        /// <param name="widthBytes">Width in bytes</param>
        /// <param name="heightPixels">Height in pixels</param>
        /// <param name="data">Bitmap data</param>
        /// <returns>Complete raster command with data</returns>
        private static byte[] CreateRasterCommand(int widthBytes, int heightPixels, byte[] data)
        {
            var outBuf = new List<byte>(8 + data.Length + Commands.LF.Length * 2);
            
            // GS v 0 command for raster graphics
            outBuf.Add(0x1D); // GS
            outBuf.Add(0x76); // v
            outBuf.Add(0x30); // 0 (raster format)
            outBuf.Add(0x00); // Reserved byte
            
            // Width in bytes (little endian)
            outBuf.Add((byte)(widthBytes & 0xFF));
            outBuf.Add((byte)((widthBytes >> 8) & 0xFF));
            
            // Height in pixels (little endian)
            outBuf.Add((byte)(heightPixels & 0xFF));
            outBuf.Add((byte)((heightPixels >> 8) & 0xFF));
            
            // Bitmap data
            outBuf.AddRange(data);
            
            return outBuf.ToArray();
        }
    }
}
