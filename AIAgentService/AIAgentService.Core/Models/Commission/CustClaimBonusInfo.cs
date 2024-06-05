namespace AIAgentService.Core.Models.Commission;

public class CustClaimBonusInfo
{
    public int SiteId { get; set; }
    public int CurrencyId { get; set; }
    public int CustId { get; set; }
    public decimal BonusAmountClaim { get; set; }
}