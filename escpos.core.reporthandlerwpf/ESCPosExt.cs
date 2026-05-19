using ESCPOS;
using gip.core.reporthandler;
using escpos.core.reporthandler;

namespace escpos.core.reporthandlerwpf
{
    public static class ESCPosExt
    {

        public static byte[] PrintQRCodeExt(string content, QRCodeModel qrCodemodel = QRCodeModel.Model1, QRCodeCorrection qrodeCorrection = QRCodeCorrection.Percent7, QRCodeSizeExt qrCodeSize = QRCodeSizeExt.Six)
        {
            return ESCPosExtX.PrintQRCodeExt(content, qrCodemodel, qrodeCorrection, qrCodeSize);
        }
    }
}
