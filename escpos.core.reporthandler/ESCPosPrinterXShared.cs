using ESCPOS;
using ESCPOS.Utils;
using gip.core.datamodel;
using gip.core.reporthandler;
using System;
using System.Text;

namespace escpos.core.reporthandler
{
    public sealed class ESCPosPrinterXShared
    {
        public byte[] GetESCPosCodePage(int codePage)
        {
            switch (codePage)
            {
                case 20127:
                    return Commands.SelectCodeTable(CodeTable.USA);
                case 850:
                    return Commands.SelectCodeTable(CodeTable.Multilingual);
                case 852:
                    return Commands.SelectCodeTable(CodeTable.Latin2);
                case 855:
                    return Commands.SelectCodeTable(CodeTable.Cyrillic);
                case 1252:
                    return Commands.SelectCodeTable(CodeTable.Windows1252);
                default:
                    return Commands.SelectCodeTable(CodeTable.USA);
            }
        }

        public void InitializeJob(IESCPosPrintJob printJob, int codePage)
        {
            if (printJob == null)
                return;

            printJob.Main = Append(printJob.Main, Commands.InitializePrinter, GetESCPosCodePage(codePage));
        }

        public void AppendToJob(IESCPosPrintJob printJob, params byte[][] chunks)
        {
            if (printJob == null)
                return;

            printJob.Main = Append(printJob.Main, chunks);
        }

        public byte[] GetTransportBytes(PrintJob printJob, bool appendFullPaperCut = true)
        {
            byte[] bytes = printJob?.Main;
            if (bytes == null || bytes.Length == 0)
                return Array.Empty<byte>();

            return appendFullPaperCut ? bytes.Add(Commands.FullPaperCut) : bytes;
        }

        public byte[] BuildQrCodeBytes(string barcodeValue, GS1Model gs1Model, bool showHri, Justification alignment, QRCodeSizeExt size, Encoding encoding)
        {
            string qrContent = gs1Model != null && gs1Model.IsGs1 && !string.IsNullOrEmpty(gs1Model.RawGs1Value)
                ? "\u001D" + gs1Model.RawGs1Value
                : barcodeValue;

            byte[] bytes = Combine(
                Commands.LF,
                Commands.SelectJustification(alignment),
                Commands.SelectPrintMode(PrintMode.Reset),
                ESCPosExtX.PrintQRCodeExt(qrContent, QRCodeModel.Model1, QRCodeCorrection.Percent30, size));

            if (showHri && !string.IsNullOrWhiteSpace(gs1Model?.HriText))
            {
                bytes = bytes.Add(
                    Commands.LF,
                    Commands.SelectJustification(alignment),
                    Commands.SelectPrintMode(PrintMode.Reset),
                    (encoding ?? Encoding.ASCII).GetBytes(gs1Model.HriText));
            }

            return bytes.Add(Commands.LF, Commands.LF, Commands.LF, Commands.LF, Commands.LF);
        }

        public byte[] BuildCode128Bytes(string barcodeValue, GS1Model gs1Model, bool showHri, Justification alignment, Encoding encoding,
            int desiredWidthDots, int heightPx, int minModule, int maxModule, bool rotated90)
        {
            byte[] bytes;

            if (gs1Model != null && gs1Model.IsGs1 && gs1Model.Items != null && gs1Model.Items.Count > 0)
            {
                byte[] barcodeRaster = Gs1Code128RasterX.FromInputToRasterFit(
                    fields: gs1Model.Items,
                    desiredWidthDots: desiredWidthDots,
                    heightPx: heightPx,
                    minModule: minModule,
                    maxModule: maxModule,
                    rotated90: rotated90);

                bytes = Combine(
                    Commands.LF,
                    Commands.SelectJustification(alignment),
                    barcodeRaster,
                    Commands.SelectJustification(Justification.Left),
                    Commands.LF);

                if (showHri && !string.IsNullOrWhiteSpace(gs1Model.HriText))
                {
                    bytes = bytes.Add(
                        Commands.LF,
                        Commands.SelectJustification(alignment),
                        Commands.SelectPrintMode(PrintMode.Reset),
                        (encoding ?? Encoding.ASCII).GetBytes(gs1Model.HriText));
                }
            }
            else
            {
                bytes = Combine(Commands.LF, Commands.Barcode(BarCodeType.CODE128, barcodeValue));
            }

            return bytes.Add(Commands.LF, Commands.LF, Commands.LF, Commands.LF, Commands.LF);
        }

        public byte[] BuildGenericBarcodeBytes(BarCodeType barcodeType, string barcodeValue)
        {
            return Combine(
                Commands.LF,
                Commands.Barcode(barcodeType, barcodeValue),
                Commands.LF,
                Commands.LF,
                Commands.LF,
                Commands.LF,
                Commands.LF);
        }

        public static byte[] Append(byte[] seed, params byte[][] chunks)
        {
            byte[] current = seed ?? Array.Empty<byte>();
            if (chunks == null || chunks.Length == 0)
                return current;

            return current.Add(chunks);
        }

        public static byte[] Combine(params byte[][] chunks)
        {
            return Append(Array.Empty<byte>(), chunks);
        }
    }
}