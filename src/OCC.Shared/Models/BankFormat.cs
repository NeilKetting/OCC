using System.ComponentModel;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Supported bank file export layouts for bulk wages payment.
    /// </summary>
    public enum BankFormat
    {
        [Description("Standard CSV Format")]
        StandardCsv,

        [Description("Nedbank NetBank CSV Format")]
        NedbankNetBankCsv
    }
}
