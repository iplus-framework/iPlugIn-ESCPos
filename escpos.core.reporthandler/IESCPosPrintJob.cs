using System.Text;

namespace escpos.core.reporthandler
{
    public interface IESCPosPrintJob
    {
        byte[] Main { get; set; }

        Encoding Encoding { get; set; }
    }
}