namespace Requests;

public record ItemDto(int Id, string Title, string OrganizationName, string Status,
                      string Priority, string? AssignedTo, DateTime CreatedAt,
                      DateTime UpdatedAt, string RowVersion);

public record PagedDto(int Page, int PageSize, int Total, List<ItemDto> Items);

public record StatusBody(string Status, string? RowVersion);
