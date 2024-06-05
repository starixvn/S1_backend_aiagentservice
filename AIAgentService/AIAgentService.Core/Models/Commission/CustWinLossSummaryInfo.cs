namespace AIAgentService.Core.Models.Commission;

public class CustWinLossSummaryInfo
{
    public int GameType { get; set; }
    public int SiteId { get; set; }
    public int CurrencyId { get; set; }
    public int CustId { get; set; }
    public decimal WinLossAmount { get; set; }
}