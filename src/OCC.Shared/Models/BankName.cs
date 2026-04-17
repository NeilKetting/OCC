using System.ComponentModel;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Enumeration of accepted South African banking institutions.
    /// Used for referencing payment destinations for Suppliers and Employees.
    /// </summary>
    public enum BankName
    {
        [Description("Select Bank")]
        None = 0,
        [Description("FNB / RMB")]
        FNB_RMB = 1,
        [Description("ABSA BANK LIMITED")]
        ABSA = 2,
        [Description("Capitec Bank")]
        Capitec = 3,
        [Description("Capitec Business")]
        CapitecBusiness = 4,
        [Description("Nedbank Limited")]
        Nedbank = 5,
        [Description("Standard Bank")]
        StandardBank = 6,
        [Description("African Bank")]
        AfricanBank = 7,
        [Description("Albaraka Bank")]
        AlbarakaBank = 8,
        [Description("Bidvest Bank")]
        BidvestBank = 9,
        [Description("Access Bank (South Africa) Ltd")]
        AccessBank = 10,
        [Description("African Bank Business")]
        AfricanBankBusiness = 11,
        [Description("African Bank Incorp Ubank")]
        AfricanBankUbank = 12,
        [Description("Bank of China")]
        BankOfChina = 13,
        [Description("Bank Zero")]
        BankZero = 14,
        [Description("Bidvest Bank Alliance")]
        BidvestBankAlliance = 15,
        [Description("CitiBank")]
        CitiBank = 16,
        [Description("Discovery Bank")]
        DiscoveryBank = 17,
        [Description("FinBond Mutual Bank")]
        FinBond = 18,
        [Description("HBZ Bank")]
        HBZBank = 19,
        [Description("HSBC Bank")]
        HSBC = 20,
        [Description("Investec Bank")]
        Investec = 21,
        [Description("JP Morgan Chase")]
        JPMorgan = 22,
        [Description("Nedbank Incorp FBS")]
        NedbankFBS = 23,
        [Description("Nedbank LTD BOE")]
        NedbankBOE = 24,
        [Description("Nedbank PEP Bank")]
        NedbankPEP = 25,
        [Description("OM Bank Limited")]
        OMBank = 26,
        [Description("Olympus Mobile")]
        OlympusMobile = 27,
        [Description("Peoples Bank Ltd Inc NBS")]
        PeoplesBank = 28,
        [Description("S.A. Reserve Bank")]
        SAReserveBank = 29,
        [Description("South African Postbank SOC Ltd")]
        Postbank = 30,
        [Description("Standard Chartered Bank")]
        StandardChartered = 31,
        [Description("State Bank of India")]
        StateBankOfIndia = 32,
        [Description("TymeBank")]
        TymeBank = 33,
        [Description("Unibank")]
        Unibank = 34,
        [Description("Sasfin Bank")]
        Sasfin = 36,
        [Description("Other")]
        Other = 999
    }
}
