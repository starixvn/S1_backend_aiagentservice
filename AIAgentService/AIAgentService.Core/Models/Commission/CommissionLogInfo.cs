namespace AIAgentService.Core.Models.Commission;

public class CommissionLogInfo
{
    public int AgentId { get; set; }
    public int CustId { get; set; }
    public int GameType { get; set; }
    public decimal WinLossAmount { get; set; }
    public decimal ClaimBonusAmount { get; set; }
    public decimal Percentage { get; set; }
    public decimal Commission { get; set; }
}