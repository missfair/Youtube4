namespace YoutubeAutomation.Models;

public enum StepStatus
{
    NotStarted,
    InProgress,
    Completed,
    Failed
}

public class WorkflowStep
{
    public int StepNumber { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public StepStatus Status { get; set; } = StepStatus.NotStarted;
    public int Progress { get; set; } = 0;
    public string ErrorMessage { get; set; } = "";

    public bool IsCompleted => Status == StepStatus.Completed;
    public bool IsInProgress => Status == StepStatus.InProgress;
    public bool IsFailed => Status == StepStatus.Failed;
    public bool CanStart => Status == StepStatus.NotStarted || Status == StepStatus.Failed;
}
