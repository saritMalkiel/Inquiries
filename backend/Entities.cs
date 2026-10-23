using System.ComponentModel.DataAnnotations;

namespace Requests;

public enum Status { New, InProgress, Waiting, Completed }
public enum Priority { Low, Medium, High }

public class ServiceRequest
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string OrganizationName { get; set; } = "";
    public Status Status { get; set; } = Status.New;
    public Priority Priority { get; set; } = Priority.Medium;
    public string? AssignedTo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<StatusChange> StatusChanges { get; set; } = new();
}

public class StatusChange
{
    public long Id { get; set; }
    public int RequestId { get; set; }
    public ServiceRequest? Request { get; set; }
    public Status PreviousStatus { get; set; }
    public Status NewStatus { get; set; }
    public DateTime ChangedAt { get; set; }
    public string ChangedBy { get; set; } = "system";
}

// === DTOs — השמות והשדות חייבים להתאים ל-request.models.ts ===

public record ServiceRequestDto(
    int Id,
    string Title,
    string? Description,
    string OrganizationName,
    string Status,
    string Priority,
    string? AssignedTo,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string RowVersion);

public record PagedResultDto(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    List<ServiceRequestDto> Items);

public record StatusHistoryDto(
    string PreviousStatus,
    string NewStatus,
    DateTime ChangedAt,
    string ChangedBy);

public record AggregationsDto(
    int TotalRequests,
    Dictionary<string, int> ByStatus,
    Dictionary<string, int> ByPriority,
    DateTime GeneratedAt);

public record UpdateStatusPayload(string Status, string? RowVersion);

public record UpdateStatusResult(int Id, string Status, string RowVersion, DateTime UpdatedAt);

public record BulkStatusPayload(List<int> Ids, string Status);

public class BulkStatusResult
{
    public int Updated { get; set; }
    public List<BulkFailure> Failed { get; set; } = new();
}

public record BulkFailure(int Id, string Reason);