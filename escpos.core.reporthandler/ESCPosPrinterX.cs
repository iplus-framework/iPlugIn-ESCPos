using ESCPOS;
using ESCPOS.Utils;
using gip.core.autocomponent;
using gip.core.datamodel;
using gip.core.reporthandler;
using System;
using System.Text;
using System.Threading;

namespace escpos.core.reporthandler
{
    [ACClassInfo(Const.PackName_VarioSystem, "en{'ESCPos Printer X'}de{'ESCPos Printer X'}", Global.ACKinds.TPABGModule, Global.ACStorableTypes.Required, false, false)]
    public class ESCPosPrinterX : ACPrintServerBase
    {
        private ACPropertyConfigValue<bool> _UseScryberLayoutRenderer;

        [ACPropertyConfig("en{'Use Scryber layout renderer'}de{'Scryber-Layout-Renderer verwenden'}")]
        public bool UseScryberLayoutRenderer
        {
            get => _UseScryberLayoutRenderer.ValueT;
            set => _UseScryberLayoutRenderer.ValueT = value;
        }

        public ESCPosPrinterX(ACClass acType, IACObject content, IACObject parentACObject, ACValueList parameter, string acIdentifier = "")
            : base(acType, content, parentACObject, parameter, acIdentifier)
        {
            _UseScryberLayoutRenderer = new ACPropertyConfigValue<bool>(this, nameof(UseScryberLayoutRenderer), true);
        }

        public override bool ACInit(Global.ACStartTypes startChildMode = Global.ACStartTypes.Automatic)
        {
            if (!base.ACInit(startChildMode))
                return false;

            _ = UseScryberLayoutRenderer;
            return true;
        }

        public static byte[] SelectCodeTable(byte codeTable)
        {
            return new byte[] { 27, 116, codeTable };
        }

        public static byte[] SelectInternationalCharacterSet(CharSet charSet)
        {
            return new byte[] { 27, 82, (byte)charSet };
        }

        public virtual byte[] GetESCPosCodePage(int codePage)
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

        protected override PrintJob TryCreateScryberCustomPrintJob(ACClassDesign aCClassDesign, ReportData reportData)
        {
            if (!UseScryberLayoutRenderer || aCClassDesign == null || reportData == null)
                return null;

            string template = GetScryberTemplate(aCClassDesign);
            if (string.IsNullOrWhiteSpace(template))
                return null;

            try
            {
                Encoding encoding = ResolveEncoding();
                byte[] codePageCommand = GetESCPosCodePage(encoding.CodePage);
                ESCPosScryberLayoutRendererX renderer = new ESCPosScryberLayoutRendererX(encoding, codePageCommand);

                byte[] bytes = ScryberReportEngine.RenderWithLayoutRenderer(template, reportData, renderer);
                if (bytes == null || bytes.Length == 0)
                    return null;

                return new PrintJob
                {
                    Name = aCClassDesign.ACIdentifier,
                    Main = bytes,
                    Encoding = encoding,
                    ColumnMultiplier = 1,
                    ColumnDivisor = 1,
                };
            }
            catch (Exception e)
            {
                Messages.LogException(GetACUrl(), nameof(TryCreateScryberCustomPrintJob), e);
                return null;
            }
        }

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

        public override bool SendDataToPrinter(PrintJob printJob)
        {
            if (printJob?.Main == null)
                return false;

            byte[] bytes = printJob.Main;
            for (int tries = 0; tries < PrintTries; tries++)
            {
                try
                {
                    bytes = bytes.Add(Commands.FullPaperCut);
                    bytes.Print($"{IPAddress}:{Port}");
                    if (IsAlarmActive(IsConnected) != null)
                        AcknowledgeAlarms();
                    IsConnected.ValueT = true;
                    return true;
                }
                catch (Exception e)
                {
                    string message = string.Format("Print failed on {0}. See log for further details.", IPAddress);
                    if (IsAlarmActive(IsConnected, message) == null)
                        Messages.LogException(GetACUrl(), nameof(SendDataToPrinter), e);
                    OnNewAlarmOccurred(IsConnected, message);
                    IsConnected.ValueT = false;
                    Thread.Sleep(5000);
                }
            }
            return false;
        }

        protected override PrintJob OnDoPrint(ACClassDesign aCClassDesign, int codePage, ReportData reportData)
        {
            return null;
        }
    }
}
