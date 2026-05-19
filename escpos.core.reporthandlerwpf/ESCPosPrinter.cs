using gip.core.datamodel;
using gip.core.reporthandlerwpf.Flowdoc;
using System.Windows.Documents;
using System;
using System.Threading;
using ESCPOS;
using ESCPOS.Utils;
using System.Windows;
using gip.core.reporthandler;
using gip.core.reporthandlerwpf;
using escpos.core.reporthandler;
using System.Threading.Tasks;
using System.Text;
using gip.core.layoutengine;
using gip.core.autocomponent;

namespace escpos.core.reporthandlerwpf
{
    [ACClassInfo(Const.PackName_VarioSystem, "en{'ESCPos Printer'}de{'ESCPos Printer'}", Global.ACKinds.TPABGModule, Global.ACStorableTypes.Required, false, false)]
    public class ESCPosPrinter : ACPrintServerBaseWPF
    {
        private readonly ESCPosPrinterXShared _shared = new ESCPosPrinterXShared();


        #region ctor's
        public ESCPosPrinter(ACClass acType, IACObject content, IACObject parentACObject, ACValueList parameter, string acIdentifier = "")
           : base(acType, content, parentACObject, parameter, acIdentifier)
        {
        }

        public override bool ACInit(Global.ACStartTypes startChildMode = Global.ACStartTypes.Automatic)
        {
            if (!base.ACInit(startChildMode))
                return false;

            return true;
        }

        public override async Task<bool> ACDeInit(bool deleteACClassTask = false)
        {
            return await base.ACDeInit(deleteACClassTask);
        }
        #endregion

        #region Methods (ACPrintServerBase)

        #region Methods -> Character Set
        public static byte[] SelectCodeTable(byte codeTable)
        {
            return new byte[3]
            {
                27,
                116,
                codeTable
            };
        }

        public static byte[] SelectInternationalCharacterSet(CharSet charSet)
        {
            return new byte[3]
            {
                27,
                82,
                (byte)charSet
            };
        }

        public virtual byte[] GetESCPosCodePage(int codePage)
        {
            return _shared.GetESCPosCodePage(codePage);
        }

        //public byte[] GetInternationalCharacterSet(string language)
        //{
        //    byte[] bytes = null;
        //    switch (language)
        //    {
        //        case "de-DE":
        //            bytes = Commands.SelectInternationalCharacterSet(CharSet.Germany);
        //            break;
        //        case "hr-HR":
        //            bytes = ESCPosPrinter.SelectInternationalCharacterSet(CharSet.Germany);
        //            break;
        //    }
        //    return bytes;
        //}

        #endregion

        #region Methods -> Render

        private Encoding ResolveEncoding()
        {
            Encoding encoder = Encoding.ASCII;
            if (CodePage <= 0)
                return encoder;

            try
            {
                return Encoding.GetEncoding(CodePage);
            }
            catch (Exception ex)
            {
                Messages.LogException(GetACUrl(), nameof(ResolveEncoding), ex);
                return encoder;
            }
        }

        /// <summary>
        /// Convert report data to stream
        /// </summary>
        /// <param name="reportData"></param>
        /// <exception cref="NotImplementedException"></exception>
        public override bool SendDataToPrinter(PrintJob printJob)
        {
            if (printJob == null)
            {
                return false;
            }

            byte[] bytes = _shared.GetTransportBytes(printJob, appendFullPaperCut: true);
            if (bytes == null || bytes.Length == 0)
                return false;

            for (int tries = 0; tries < PrintTries; tries++)
            {
                try
                {
                    bytes.Print(string.Format("{0}:{1}", IPAddress, Port));
                    if (IsAlarmActive(IsConnected) != null)
                        AcknowledgeAlarms();
                    IsConnected.ValueT = true;
                    return true;
                }
                catch (Exception e)
                {
                    string message = String.Format("Print failed on {0}. See log for further details.", IPAddress);
                    if (IsAlarmActive(IsConnected, message) == null)
                        Messages.LogException(GetACUrl(), "SendDataToPrinter(20)", e);
                    OnNewAlarmOccurred(IsConnected, message);
                    IsConnected.ValueT = false;
                    Thread.Sleep(5000);
                }
            }
            return false;
        }

        #region Methods -> Render -> FlowDoc

        public override PrintJob GetPrintJob(string reportName, FlowDocument flowDocument)
        {
            Encoding encoder = ResolveEncoding();
            VBFlowDocument vbFlowDocument = flowDocument as VBFlowDocument;

            int? codePage = null;
            if (vbFlowDocument != null && vbFlowDocument.CodePage > 0)
                codePage = vbFlowDocument.CodePage;
            else if (CodePage > 0)
                codePage = CodePage;

            if (codePage.HasValue)
            {
                try
                {
                    encoder = Encoding.GetEncoding(codePage.Value);
                }
                catch (Exception ex)
                {
                    Messages.LogException(GetACUrl(), nameof(GetPrintJob), ex);
                }
            }

            ESCPosPrintJobWPF printJob = new ESCPosPrintJobWPF
            {
                FlowDocument = flowDocument,
                Encoding = encoder,
                ColumnMultiplier = 1,
                ColumnDivisor = 1,
            };

            OnRenderFlowDocument(printJob, printJob.FlowDocument);
            return printJob;
        }

        public override void OnRenderFlowDocument(PrintJob printJob, FlowDocument flowDoc)
        {
            if (printJob is IESCPosPrintJob escPosPrintJob)
            {
                _shared.InitializeJob(escPosPrintJob, printJob?.Encoding?.CodePage ?? Encoding.ASCII.CodePage);
            }
            else
            {
                printJob.Main = ESCPosPrinterXShared.Append(printJob.Main,
                    Commands.InitializePrinter,
                    _shared.GetESCPosCodePage(printJob?.Encoding?.CodePage ?? Encoding.ASCII.CodePage));
            }

            base.OnRenderFlowDocument(printJob, flowDoc);
        }



        #endregion

        #region Methods -> Render -> Block


        public override void OnRenderBlockHeader(PrintJob printJob, Block block, BlockDocumentPosition position)
        {

        }

        public override void OnRenderBlockFooter(PrintJob printJob, Block block, BlockDocumentPosition position)
        {

        }


        public override void OnRenderSectionReportHeaderHeader(PrintJob printJob, SectionReportHeader sectionReportHeader)
        {
            SetPrintFormat(printJob, sectionReportHeader, sectionReportHeader.TextAlignment);
        }

        public override void OnRenderSectionReportHeaderFooter(PrintJob printJob, SectionReportHeader sectionReportHeader)
        {
            PrintJobWPF printJobWPF = printJob as PrintJobWPF;
            if (printJobWPF != null)
                printJobWPF.PrintFormats.RemoveAt(printJobWPF.PrintFormats.Count - 1);
        }


        public override void OnRenderSectionReportFooterHeader(PrintJob printJob, SectionReportFooter sectionReportFooter)
        {
            SetPrintFormat(printJob, sectionReportFooter, sectionReportFooter.TextAlignment);
        }

        public override void OnRenderSectionReportFooterFooter(PrintJob printJob, SectionReportFooter sectionReportFooter)
        {
            PrintJobWPF printJobWPF = printJob as PrintJobWPF;
            if (printJobWPF != null)
                printJobWPF.PrintFormats.RemoveAt(printJobWPF.PrintFormats.Count - 1);
        }


        public override void OnRenderSectionDataGroupHeader(PrintJob printJob, SectionDataGroup sectionDataGroup)
        {
            SetPrintFormat(printJob, sectionDataGroup, sectionDataGroup.TextAlignment);
        }

        public override void OnRenderSectionDataGroupFooter(PrintJob printJob, SectionDataGroup sectionDataGroup)
        {
            PrintJobWPF printJobWPF = printJob as PrintJobWPF;
            if (printJobWPF != null)
                printJobWPF.PrintFormats.RemoveAt(printJobWPF.PrintFormats.Count - 1);
        }


        #endregion

        #region Methods -> Render -> Table


        public override void OnRenderSectionTableHeader(PrintJob printJob, Table table)
        {
            PrintJobWPF printJobWPF = printJob as PrintJobWPF;
            if (printJobWPF != null)
            {
                PrintFormat printFormat = new PrintFormat();
                printFormat.FontSize = table.FontSize;
                printFormat.FontWeight = table.FontWeight;
                printFormat.TextAlignment = table.TextAlignment;
                printJobWPF.PrintFormats.Add(printFormat);
            }
        }

        public override void OnRenderSectionTableFooter(PrintJob printJob, Table table)
        {
            PrintJobWPF printJobWPF = printJob as PrintJobWPF;
            if (printJobWPF != null)
                printJobWPF.PrintFormats.RemoveAt(printJobWPF.PrintFormats.Count - 1);
        }


        public override void OnRenderTableColumn(PrintJob printJob, TableColumn tableColumn)
        {
            //
        }

        public override void OnRenderTableRowGroupHeader(PrintJob printJob, TableRowGroup tableRowGroup)
        {
            SetPrintFormat(printJob, tableRowGroup, null);
        }

        public override void OnRenderTableRowGroupFooter(PrintJob printJob, TableRowGroup tableRowGroup)
        {
            PrintJobWPF printJobWPF = printJob as PrintJobWPF;
            if (printJobWPF != null)
                printJobWPF.PrintFormats.RemoveAt(printJobWPF.PrintFormats.Count - 1);
        }

        public override void OnRenderTableRowHeader(PrintJob printJob, TableRow tableRow)
        {
            SetPrintFormat(printJob, tableRow, null);
        }

        public override void OnRenderTableRowFooter(PrintJob printJob, TableRow tableRow)
        {
            PrintJobWPF printJobWPF = printJob as PrintJobWPF;
            if (printJobWPF != null)
                printJobWPF.PrintFormats.RemoveAt(printJobWPF.PrintFormats.Count - 1);
        }

        #endregion

        #region Methods -> Render -> Inlines

        public override void OnRenderParagraphHeader(PrintJob printJob, Paragraph paragraph)
        {
            SetPrintFormat(printJob, paragraph, paragraph.TextAlignment);
        }

        public override void OnRenderParagraphFooter(PrintJob printJob, Paragraph paragraph)
        {
            PrintJobWPF printJobWPF = printJob as PrintJobWPF;
            if (printJobWPF != null)
                printJobWPF.PrintFormats.RemoveAt(printJobWPF.PrintFormats.Count - 1);
        }

        public override void OnRenderInlineContextValue(PrintJob printJob, InlineContextValue inlineContextValue)
        {
            PrintJobWPF printJobWPF = printJob as PrintJobWPF;
            if (printJobWPF != null)
            {
                SetPrintFormat(printJob, inlineContextValue, null);
                PrintFormat defaultPrintFormat = printJobWPF.GetDefaultPrintFormat();
                PrintFormattedText(printJob, defaultPrintFormat, inlineContextValue.Text);
                printJobWPF.PrintFormats.RemoveAt(printJobWPF.PrintFormats.Count - 1);
            }
        }

        public override void OnRenderInlineDocumentValue(PrintJob printJob, InlineDocumentValue inlineDocumentValue)
        {
            PrintJobWPF printJobWPF = printJob as PrintJobWPF;
            if (printJobWPF != null)
            {
                SetPrintFormat(printJob, inlineDocumentValue, null);
                PrintFormat defaultPrintFormat = printJobWPF.GetDefaultPrintFormat();
                PrintFormattedText(printJob, defaultPrintFormat, inlineDocumentValue.Text);
                printJobWPF.PrintFormats.RemoveAt(printJobWPF.PrintFormats.Count - 1);
            }
        }

        public override void OnRenderInlineACMethodValue(PrintJob printJob, InlineACMethodValue inlineACMethodValue)
        {
            PrintJobWPF printJobWPF = printJob as PrintJobWPF;
            if (printJobWPF != null)
            {
                SetPrintFormat(printJob, inlineACMethodValue, null);
                PrintFormat defaultPrintFormat = printJobWPF.GetDefaultPrintFormat();
                PrintFormattedText(printJob, defaultPrintFormat, inlineACMethodValue.Text);
                printJobWPF.PrintFormats.RemoveAt(printJobWPF.PrintFormats.Count - 1);
            }
        }

        public override void OnRenderInlineTableCellValue(PrintJob printJob, InlineTableCellValue inlineTableCellValue)
        {
            PrintJobWPF printJobWPF = printJob as PrintJobWPF;
            if (printJobWPF != null)
            {
                SetPrintFormat(printJob, inlineTableCellValue, null);
                PrintFormat defaultPrintFormat = printJobWPF.GetDefaultPrintFormat();
                PrintFormattedText(printJob, defaultPrintFormat, inlineTableCellValue.Text);
                printJobWPF.PrintFormats.RemoveAt(printJobWPF.PrintFormats.Count - 1);
            }
        }

        public override void OnRenderInlineBarcode(PrintJob printJob, InlineBarcode inlineBarcode)
        {
            if (printJob == null || inlineBarcode == null || inlineBarcode.Value == null)
                return;

            IESCPosPrintJob escPosPrintJob = printJob as IESCPosPrintJob;
            if (escPosPrintJob == null)
                return;

            string barcodeValue = inlineBarcode.Value.ToString();
            if (inlineBarcode.BarcodeType == BarcodeType.QRCODE)
            {
                QRCodeSizeExt qRCodeSizeExt = QRCodeSizeExt.Six;
                if (inlineBarcode.BarcodeWidth >= 2 && inlineBarcode.BarcodeWidth <= 10)
                    qRCodeSizeExt = (QRCodeSizeExt)inlineBarcode.BarcodeWidth;

                byte[] barcodeBytes = _shared.BuildQrCodeBytes(
                    barcodeValue,
                    inlineBarcode.GS1Model,
                    inlineBarcode.ShowHRI,
                    Justification.Center,
                    qRCodeSizeExt,
                    printJob.Encoding);

                _shared.AppendToJob(escPosPrintJob, barcodeBytes);
            }
            else if (inlineBarcode.BarcodeType == BarcodeType.CODE128)
            {

                byte[] barcodeBytes = _shared.BuildCode128Bytes(
                    barcodeValue,
                    inlineBarcode.GS1Model,
                    inlineBarcode.ShowHRI,
                    Justification.Center,
                    printJob.Encoding,
                    inlineBarcode.ESCDesiredWidthDots,
                    inlineBarcode.ESCHeightPx,
                    inlineBarcode.ESCMinModule,
                    inlineBarcode.ESCMaxModule,
                    inlineBarcode.Rotate90);

                _shared.AppendToJob(escPosPrintJob, barcodeBytes);
            }
            else
            {
                BarCodeType barCodeType = BarCodeType.EAN8;
                if (Enum.TryParse(inlineBarcode.BarcodeType.ToString(), out barCodeType))
                {
                    byte[] barcodeBytes = _shared.BuildGenericBarcodeBytes(barCodeType, barcodeValue);
                    _shared.AppendToJob(escPosPrintJob, barcodeBytes);
                }
            }
        }

        public override void OnRenderInlineBoolValue(PrintJob printJob, InlineBoolValue inlineBoolValue)
        {
            PrintJobWPF printJobWPF = printJob as PrintJobWPF;
            if (printJobWPF != null)
            {
                SetPrintFormat(printJob, inlineBoolValue, null);
                PrintFormat defaultPrintFormat = printJobWPF.GetDefaultPrintFormat();
                PrintFormattedText(printJob, defaultPrintFormat, inlineBoolValue.Value.ToString());
                printJobWPF.PrintFormats.RemoveAt(printJobWPF.PrintFormats.Count - 1);
            }
        }

        public override void OnRenderRun(PrintJob printJob, Run run)
        {
            PrintJobWPF printJobWPF = printJob as PrintJobWPF;
            if (printJobWPF != null)
            {
                SetPrintFormat(printJob, run, null);
                PrintFormat defaultPrintFormat = printJobWPF.GetDefaultPrintFormat();
                PrintFormattedText(printJob, defaultPrintFormat, run.Text);
                printJobWPF.PrintFormats.RemoveAt(printJobWPF.PrintFormats.Count - 1);
            }
        }

        public override void OnRenderLineBreak(PrintJob printJob, LineBreak lineBreak)
        {
            printJob.Main = printJob.Main.Add(Commands.LF);
        }

        #endregion


        #region Methods -> Render-> Common

        private void SetPrintFormat(PrintJob printJob, TextElement textElement, TextAlignment? textAlignment)
        {
            PrintJobWPF printJobWPF = printJob as PrintJobWPF;
            if (printJobWPF != null)
            {
                PrintFormat printFormat = new PrintFormat();
                printFormat.FontSize = textElement.FontSize;
                printFormat.FontWeight = textElement.FontWeight;
                printFormat.TextAlignment = textAlignment;
                printJobWPF.PrintFormats.Add(printFormat);
            }
        }

        protected Tuple<Justification, CharSizeWidth, CharSizeHeight> GetESCFormat(PrintFormat defaultPrintFormat)
        {
            Justification justification = Justification.Left;
            if (defaultPrintFormat.TextAlignment != null && defaultPrintFormat.TextAlignment != TextAlignment.Left)
                if (defaultPrintFormat.TextAlignment == TextAlignment.Right)
                    justification = Justification.Right;
                else if (defaultPrintFormat.TextAlignment == TextAlignment.Center)
                    justification = Justification.Center;
                else if (defaultPrintFormat.TextAlignment == TextAlignment.Justify)
                    justification = Justification.Center;


            CharSizeWidth charSizeWidth = CharSizeWidth.Normal;
            CharSizeHeight charSizeHeight = CharSizeHeight.Normal;

            if (defaultPrintFormat.FontSize != null)
            {
                if (defaultPrintFormat.FontSize >= 18)
                {
                    charSizeWidth = CharSizeWidth.Quadruple;
                    charSizeHeight = CharSizeHeight.Quadruple;
                }
                else if (defaultPrintFormat.FontSize >= 16)
                {
                    charSizeWidth = CharSizeWidth.Triple;
                    charSizeHeight = CharSizeHeight.Triple;
                }
                else if (defaultPrintFormat.FontSize >= 14)
                {
                    charSizeWidth = CharSizeWidth.Double;
                    charSizeHeight = CharSizeHeight.Double;
                }
            }

            return new Tuple<Justification, CharSizeWidth, CharSizeHeight>(justification, charSizeWidth, charSizeHeight);
        }

        protected void PrintFormattedText(PrintJob printJob, PrintFormat defaultPrintFormat, string text)
        {
            Tuple<Justification, CharSizeWidth, CharSizeHeight> format = GetESCFormat(defaultPrintFormat);
            printJob.Main = printJob.Main.Add(Commands.SelectPrintMode(PrintMode.Reset));
            printJob.Main = printJob.Main.Add(Commands.SelectCharSize(format.Item2, format.Item3));
            printJob.Main = printJob.Main.Add(Commands.LF, Commands.SelectJustification(format.Item1), printJob.Encoding.GetBytes(text));
        }

        #endregion

        #region Methods -> Render-> GS1



        private static int DipsToDots(double dips, int dpi = 203)
        {
            return (int)Math.Round(dips / 96.0 * dpi);
        }

        #endregion

        #endregion

        #endregion

    }
}
