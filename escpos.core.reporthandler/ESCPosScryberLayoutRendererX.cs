using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using gip.core.autocomponent;
using ESCPOS;
using gip.core.datamodel;
using gip.core.reporthandler;
using Scryber;
using Scryber.Components;
using Scryber.Drawing;
using Scryber.PDF;
using Scryber.PDF.Layout;
using ScryberDocument = Scryber.Components.Document;

namespace escpos.core.reporthandler
{
    /// <summary>
    /// First-pass Scryber layout renderer for ESC/POS text output.
    /// It walks the laid out page/line/run tree and emits plain text with basic alignment.
    /// </summary>
    public sealed class ESCPosScryberLayoutRendererX : IDocumentLayoutRenderer
    {
        private readonly Encoding _encoding;
        private readonly byte[] _codePageCommand;

        public ESCPosScryberLayoutRendererX(Encoding encoding, byte[] codePageCommand)
        {
            _encoding = encoding ?? Encoding.ASCII;
            _codePageCommand = codePageCommand ?? Array.Empty<byte>();
        }

        public void Render(ScryberDocument document, PDFLayoutDocument layout, PDFLayoutContext layoutContext, Stream output)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            using (MemoryStream buffer = new MemoryStream())
            {
                WriteBytes(buffer, Commands.InitializePrinter);
                WriteBytes(buffer, _codePageCommand);

                for (int i = 0; i < layout.AllPages.Count; i++)
                {
                    PDFLayoutPage page = layout.AllPages[i];
                    if (page == null)
                        continue;

                    if (i > 0)
                        WriteBytes(buffer, Commands.LF, Commands.LF);

                    WriteBlock(buffer, document, page.HeaderBlock);
                    WriteBlock(buffer, document, page.ContentBlock);
                    WriteBlock(buffer, document, page.FooterBlock);
                }

                buffer.Position = 0;
                buffer.CopyTo(output);
            }
        }

        private void WriteBlock(Stream output, ScryberDocument document, PDFLayoutBlock block)
        {
            if (block == null)
                return;

            if (block.Columns != null)
            {
                foreach (PDFLayoutRegion column in block.Columns)
                    WriteRegion(output, document, column);
            }

            if (block.HasPositionedRegions && block.PositionedRegions != null)
            {
                foreach (PDFLayoutRegion positioned in block.PositionedRegions)
                    WriteRegion(output, document, positioned);
            }
        }

        private void WriteRegion(Stream output, ScryberDocument document, PDFLayoutRegion region)
        {
            if (region == null || region.Contents == null)
                return;

            foreach (PDFLayoutItem item in region.Contents)
            {
                if (item is PDFLayoutLine line)
                    WriteLine(output, document, line);
                else if (item is PDFLayoutBlock block)
                    WriteBlock(output, document, block);
            }
        }

        private void WriteLine(Stream output, ScryberDocument document, PDFLayoutLine line)
        {
            if (line?.Runs == null || line.Runs.Count == 0)
                return;

            StringBuilder text = new StringBuilder();
            Justification lineAlignment = MapAlignment(line.HAlignment);

            foreach (PDFLayoutRun run in line.Runs)
            {
                if (TryWriteBarcodeRun(output, document, run, lineAlignment))
                    continue;

                string runText = ExtractRunText(run);
                if (!String.IsNullOrEmpty(runText))
                    text.Append(runText);
            }

            string plainText = text.ToString().TrimEnd();
            if (String.IsNullOrEmpty(plainText))
                return;

            WriteBytes(output, Commands.SelectPrintMode(PrintMode.Reset));
            WriteBytes(output, Commands.SelectJustification(lineAlignment));
            WriteBytes(output, _encoding.GetBytes(plainText));
            WriteBytes(output, Commands.LF);
        }

        private bool TryWriteBarcodeRun(Stream output, ScryberDocument document, PDFLayoutRun run, Justification fallbackAlignment)
        {
            Component component = run?.Owner as Component;
            if (!TryGetMetadata(component, out string barcodeTypeValue, "barcode-type", "escpos-barcode-type"))
                return false;

            string barcodeValue = ResolveBarcodeValue(document, component, run, out GS1Model gs1Model);
            if (String.IsNullOrWhiteSpace(barcodeValue))
                return true;

            bool showHri = GetBoolMetadata(component, false, "show-hri");
            Justification alignment = fallbackAlignment;
            if (TryGetMetadata(component, out string alignValue, "barcode-align"))
            {
                if (alignValue.Equals("center", StringComparison.OrdinalIgnoreCase))
                    alignment = Justification.Center;
                else if (alignValue.Equals("right", StringComparison.OrdinalIgnoreCase))
                    alignment = Justification.Right;
                else if (alignValue.Equals("left", StringComparison.OrdinalIgnoreCase))
                    alignment = Justification.Left;
            }

            if (barcodeTypeValue.Equals("QRCODE", StringComparison.OrdinalIgnoreCase))
            {
                int sizeValue = GetIntMetadata(component, 6, "barcode-width", "qr-pixels-per-module");
                if (sizeValue < 2 || sizeValue > 10)
                    sizeValue = 6;

                QRCodeSizeExt size = (QRCodeSizeExt)sizeValue;
                string qrContent = gs1Model != null && gs1Model.IsGs1 && !string.IsNullOrEmpty(gs1Model.RawGs1Value)
                    ? "\u001D" + gs1Model.RawGs1Value
                    : barcodeValue;

                WriteBytes(output,
                    Commands.LF,
                    Commands.SelectJustification(alignment),
                    Commands.SelectPrintMode(PrintMode.Reset),
                    ESCPosExtX.PrintQRCodeExt(qrContent, QRCodeModel.Model1, QRCodeCorrection.Percent30, size));

                if (showHri && !string.IsNullOrWhiteSpace(gs1Model?.HriText))
                {
                    WriteBytes(output, Commands.LF, Commands.SelectJustification(alignment), Commands.SelectPrintMode(PrintMode.Reset));
                    WriteBytes(output, _encoding.GetBytes(gs1Model.HriText));
                }

                WriteBytes(output, Commands.LF, Commands.LF, Commands.LF, Commands.LF, Commands.LF);
                return true;
            }

            if (barcodeTypeValue.Equals("CODE128", StringComparison.OrdinalIgnoreCase))
            {
                if (gs1Model != null && gs1Model.IsGs1 && gs1Model.Items != null && gs1Model.Items.Count > 0)
                {
                    int desiredWidthDots = GetIntMetadata(component, 3, "esc-desired-width-dots");
                    int heightPx = GetIntMetadata(component, 250, "esc-height-px", "barcode-height");
                    int minModule = GetIntMetadata(component, 1, "esc-min-module");
                    int maxModule = GetIntMetadata(component, 6, "esc-max-module");
                    bool rotate90 = GetBoolMetadata(component, false, "rotate-90");

                    byte[] barcodeRaster = Gs1Code128RasterX.FromInputToRasterFit(
                        fields: gs1Model.Items,
                        desiredWidthDots: desiredWidthDots,
                        heightPx: heightPx,
                        minModule: minModule,
                        maxModule: maxModule,
                        rotated90: rotate90);

                    WriteBytes(output,
                        Commands.LF,
                        Commands.SelectJustification(alignment),
                        barcodeRaster,
                        Commands.SelectJustification(Justification.Left),
                        Commands.LF);

                    if (showHri && !string.IsNullOrWhiteSpace(gs1Model.HriText))
                    {
                        WriteBytes(output, Commands.LF, Commands.SelectJustification(alignment), Commands.SelectPrintMode(PrintMode.Reset));
                        WriteBytes(output, _encoding.GetBytes(gs1Model.HriText));
                    }

                    WriteBytes(output, Commands.LF, Commands.LF, Commands.LF, Commands.LF, Commands.LF);
                    return true;
                }

                WriteBytes(output, Commands.LF, Commands.Barcode(BarCodeType.CODE128, barcodeValue));
                WriteBytes(output, Commands.LF, Commands.LF, Commands.LF, Commands.LF, Commands.LF);
                return true;
            }

            if (Enum.TryParse(barcodeTypeValue, true, out BarCodeType otherType))
            {
                WriteBytes(output, Commands.LF, Commands.Barcode(otherType, barcodeValue));
                WriteBytes(output, Commands.LF, Commands.LF, Commands.LF, Commands.LF, Commands.LF);
                return true;
            }

            return false;
        }

        private string ResolveBarcodeValue(ScryberDocument document, Component component, PDFLayoutRun run, out GS1Model gs1Model)
        {
            gs1Model = null;

            string value = ExtractRunText(run)?.Trim();
            if (TryGetMetadata(component, out string explicitValue, "barcode-value"))
                value = explicitValue;

            if (TryBuildGs1Model(document, component, out gs1Model) && !string.IsNullOrWhiteSpace(gs1Model.RawGs1Value))
                value = gs1Model.RawGs1Value;

            return value;
        }

        private static bool TryBuildGs1Model(ScryberDocument document, Component component, out GS1Model model)
        {
            model = null;
            if (document == null || component == null)
                return false;

            if (!TryGetMetadata(component, out string vbShowColumns, "vb-show-columns"))
                return false;
            if (!TryGetMetadata(component, out string vbShowColumnsKeys, "vb-show-columns-keys"))
                return false;
            if (!TryGetMetadata(component, out string vbContentPath, "vb-content"))
                return false;

            if (!document.Params.TryGetValue("reportData", out object rawReportData) || !(rawReportData is ReportData reportData))
                return false;

            object source = ScryberReportEngine.ResolveVBContent(reportData, vbContentPath);
            if (source == null)
                return false;

            string[] aiKeys = SplitCsv(vbShowColumnsKeys);
            string[] valueIdentifiers = SplitCsv(vbShowColumns);
            if (aiKeys.Length == 0 || aiKeys.Length != valueIdentifiers.Length)
                return false;

            List<(string ai, string val, bool variable)> input = GS1.GetGS1Data(source, aiKeys, valueIdentifiers);
            if (input == null || input.Count == 0)
                return false;

            model = GS1.GetGS1Model(aiKeys, input);
            return model != null && !string.IsNullOrWhiteSpace(model.RawGs1Value);
        }

        private static string[] SplitCsv(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<string>();

            return value
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToArray();
        }

        private static string ExtractRunText(PDFLayoutRun run)
        {
            if (run is PDFTextRunCharacter full)
            {
                return full.Characters ?? String.Empty;
            }

            if (run is PDFTextRunPartialCharacter partial)
            {
                if (String.IsNullOrEmpty(partial.Characters))
                    return String.Empty;

                int start = Math.Max(0, partial.StartOffset);
                int count = Math.Max(0, partial.CharacterCount);
                if (start >= partial.Characters.Length || count == 0)
                    return String.Empty;

                if ((start + count) > partial.Characters.Length)
                    count = partial.Characters.Length - start;

                return partial.Characters.Substring(start, count);
            }

            return String.Empty;
        }

        private static bool TryGetMetadata(Component component, out string value, params string[] keys)
        {
            value = null;
            if (component == null || keys == null || keys.Length == 0)
                return false;

            foreach (string key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (component.TryGetMetadata(key, out value) && !string.IsNullOrWhiteSpace(value))
                {
                    value = value.Trim();
                    return true;
                }
            }

            return false;
        }

        private static int GetIntMetadata(Component component, int defaultValue, params string[] keys)
        {
            if (TryGetMetadata(component, out string raw, keys) && Int32.TryParse(raw, out int parsed))
                return parsed;

            return defaultValue;
        }

        private static bool GetBoolMetadata(Component component, bool defaultValue, params string[] keys)
        {
            if (TryGetMetadata(component, out string raw, keys))
            {
                if (Boolean.TryParse(raw, out bool parsed))
                    return parsed;

                if (String.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (String.Equals(raw, "0", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(raw, "no", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return defaultValue;
        }

        private static Justification MapAlignment(HorizontalAlignment alignment)
        {
            if (alignment == HorizontalAlignment.Center)
                return Justification.Center;
            if (alignment == HorizontalAlignment.Right)
                return Justification.Right;
            return Justification.Left;
        }

        private static void WriteBytes(Stream stream, params byte[][] chunks)
        {
            if (stream == null || chunks == null)
                return;

            foreach (byte[] chunk in chunks)
            {
                if (chunk == null || chunk.Length == 0)
                    continue;

                stream.Write(chunk, 0, chunk.Length);
            }
        }
    }
}
