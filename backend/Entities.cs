namespace Requests;

public enum Status { New, InProgress, Waiting, Completed }
public enum Priority { Low, Medium, High }

public class Request
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string OrganizationName { get; set; } = "";
    public Status Status { get; set; } = Status.New;
    public Priority Priority { get; set; } = Priority.Medium;
    public string? AssignedTo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class StatusChange
{
    public long Id { get; set; }
    public int RequestId { get; set; }
    public Status PreviousStatus { get; set; }
    public Status NewStatus { get; set; }
    public DateTime ChangedAt { get; set; }
    public string ChangedBy { get; set; } = "system";
}
