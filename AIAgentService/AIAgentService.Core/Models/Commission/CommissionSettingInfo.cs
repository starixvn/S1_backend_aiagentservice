namespace AIAgentService.Core.Models.Commission;

public class CommissionSettingInfo
{
    public int AgentId { get; set; }
    public int GameType { get; set; }
    public decimal UpperPercentage { get; set; }
    public decimal Percentage { get; set; }
}