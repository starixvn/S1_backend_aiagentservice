namespace AIAgentService.Core.Models.Commission;

public class CommissionLogInfo
{
    public int AgentId { get; set; }
    public int CustId { get; set; }
    public int GameType { get; set; }
    public decimal WinLossAmount { get; set; }
    public decimal ClaimBonusAmount { get; set; }
    
    /// <summary>
    /// 分潤%數
    /// </summary>
    public decimal Percentage { get; set; }
    
    /// <summary>
    /// 下層分潤提成
    /// </summary>
    public decimal Sharing { get; set; }
    
    /// <summary>
    /// 前月累計負營利
    /// </summary>
    public decimal LastMonthNegativeRevenue { get; set; }
    
    public decimal Commission { get; set; }
}