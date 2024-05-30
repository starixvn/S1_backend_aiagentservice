namespace AIAgentService.Core.Models;

public class JobOption
{
    public string Group { get; set; }

    public string ClassName { get; set; }

    public string JobName { get; set; }

    public string Description { get; set; }

    public string Cron { get; set; }

    public bool Enable { get; set; }

    public string Parameters { get; set; }
}