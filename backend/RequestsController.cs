using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Requests;

[ApiController]
[Route("api/requests")]
public class RequestsController : ControllerBase
{
    private readonly AppDb _db;
    public RequestsController(AppDb db) => _db = db;

    // GET /api/requests?page=1&pageSize=25&search=..&status=New,Waiting&priority=High...
    [HttpGet]
    public async Task<ActionResult<PagedResultDto>> Get(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? status = null,          // CSV: "New,InProgress"
        string? priority = null,
        string? organizationName = null,
        string? assignedTo = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? sortBy = null,          // createdAt | updatedAt | title | priority | status
        string? sortDir = null,         // asc | desc
        CancellationToken ct = default)
    {
        if (page < 1) return BadRequest("page חייב להיות >= 1");
        if (pageSize is < 1 or > 100) return BadRequest("pageSize בין 1 ל-100");

        var q = _db.Requests.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(x => x.Title.Contains(term) || x.OrganizationName.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(organizationName))
        {
            var org = organizationName.Trim();
            q = q.Where(x => x.OrganizationName.Contains(org));
        }

        if (!string.IsNullOrWhiteSpace(assignedTo))
        {
            var who = assignedTo.Trim();
            q = q.Where(x => x.AssignedTo != null && x.AssignedTo.Contains(who));
        }

        // CSV → מערך סטטוסים
        var statuses = ParseEnums<Status>(status);
        if (statuses.Count > 0) q = q.Where(x => statuses.Contains(x.Status));

        var priorities = ParseEnums<Priority>(priority);
        if (priorities.Count > 0) q = q.Where(x => priorities.Contains(x.Priority));

        if (fromDate is not null) q = q.Where(x => x.CreatedAt >= fromDate.Value);
        if (toDate is not null)
        {
            var end = toDate.Value.Date.AddDays(1);   // כולל יום הסיום
            q = q.Where(x => x.CreatedAt < end);
        }

        q = ApplySort(q, sortBy, sortDir);

        var total = await q.CountAsync(ct);
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToDto)
            .ToListAsync(ct);

        return Ok(new PagedResultDto(page, pageSize, total, totalPages, items));
    }

    // GET /api/requests/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ServiceRequestDto>> GetById(int id, CancellationToken ct)
    {
        var dto = await _db.Requests.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(ct);

        return dto is null ? NotFound() : Ok(dto);
    }

    // GET /api/requests/{id}/history
    [HttpGet("{id:int}/history")]
    public async Task<ActionResult<List<StatusHistoryDto>>> GetHistory(int id, CancellationToken ct)
    {
        var exists = await _db.Requests.AnyAsync(x => x.Id == id, ct);
        if (!exists) return NotFound();

        var history = await _db.StatusChanges.AsNoTracking()
            .Where(x => x.RequestId == id)
            .OrderByDescending(x => x.ChangedAt)
            .Select(x => new StatusHistoryDto(
                x.PreviousStatus.ToString(),
                x.NewStatus.ToString(),
                x.ChangedAt,
                x.ChangedBy))
            .ToListAsync(ct);

        return Ok(history);
    }

    // GET /api/requests/aggregations
    [HttpGet("aggregations")]
    public async Task<ActionResult<AggregationsDto>> GetAggregations(CancellationToken ct)
    {
        var total = await _db.Requests.CountAsync(ct);

        var byStatus = await _db.Requests
            .GroupBy(x => x.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byPriority = await _db.Requests
            .GroupBy(x => x.Priority)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return Ok(new AggregationsDto(
            total,
            FillAllStatuses(byStatus.ToDictionary(x => x.Key.ToString(), x => x.Count)),
            FillAllPriorities(byPriority.ToDictionary(x => x.Key.ToString(), x => x.Count)),
            DateTime.UtcNow));
    }

    // PATCH /api/requests/{id}/status
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<UpdateStatusResult>> UpdateStatus(
        int id, UpdateStatusPayload body, CancellationToken ct)
    {
        if (!Enum.TryParse<Status>(body.Status, true, out var next))
            return BadRequest("סטטוס לא חוקי");

        var entity = await _db.Requests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return NotFound();

        if (entity.Status == next)
            return BadRequest("הסטטוס זהה לסטטוס הנוכחי");

        // אופטימיות נעילה — מחזיר 409 אם מישהו עדכן בינתיים
        if (!string.IsNullOrEmpty(body.RowVersion))
        {
            try { _db.Entry(entity).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(body.RowVersion); }
            catch (FormatException) { return BadRequest("RowVersion לא תקין"); }
        }

        var prev = entity.Status;
        entity.Status = next;
        entity.UpdatedAt = DateTime.UtcNow;

        _db.StatusChanges.Add(new StatusChange
        {
            RequestId = entity.Id,
            PreviousStatus = prev,
            NewStatus = next,
            ChangedAt = entity.UpdatedAt,
            ChangedBy = User?.Identity?.Name ?? "system"
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "הרשומה עודכנה בינתיים ע\"י משתמש אחר" });
        }

        return Ok(new UpdateStatusResult(
            entity.Id,
            entity.Status.ToString(),
            Convert.ToBase64String(entity.RowVersion),
            entity.UpdatedAt));
    }

    // PATCH /api/requests/bulk-status
    [HttpPatch("bulk-status")]
    public async Task<ActionResult<BulkStatusResult>> BulkUpdateStatus(
        BulkStatusPayload body, CancellationToken ct)
    {
        if (!Enum.TryParse<Status>(body.Status, true, out var next))
            return BadRequest("סטטוס לא חוקי");

        if (body.Ids is null || body.Ids.Count == 0)
            return BadRequest("לא נשלחו רשומות");

        if (body.Ids.Count > 500)
            return BadRequest("עד 500 רשומות בעדכון מרוכז");

        var result = new BulkStatusResult();
        var entities = await _db.Requests
            .Where(x => body.Ids.Contains(x.Id))
            .ToListAsync(ct);

        var found = entities.Select(x => x.Id).ToHashSet();
        foreach (var missing in body.Ids.Where(i => !found.Contains(i)))
            result.Failed.Add(new BulkFailure(missing, "הרשומה לא נמצאה"));

        var now = DateTime.UtcNow;
        foreach (var entity in entities)
        {
            if (entity.Status == next)
            {
                result.Failed.Add(new BulkFailure(entity.Id, "הסטטוס זהה לנוכחי"));
                continue;
            }

            var prev = entity.Status;
            entity.Status = next;
            entity.UpdatedAt = now;

            _db.StatusChanges.Add(new StatusChange
            {
                RequestId = entity.Id,
                PreviousStatus = prev,
                NewStatus = next,
                ChangedAt = now,
                ChangedBy = User?.Identity?.Name ?? "system"
            });

            result.Updated++;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "אחת הרשומות עודכנה בינתיים — נסה שוב" });
        }

        return Ok(result);
    }

    // === עזר ===

    private static readonly System.Linq.Expressions.Expression<Func<ServiceRequest, ServiceRequestDto>> ToDto =
        x => new ServiceRequestDto(
            x.Id, x.Title, x.Description, x.OrganizationName,
            x.Status.ToString(), x.Priority.ToString(), x.AssignedTo,
            x.CreatedAt, x.UpdatedAt, Convert.ToBase64String(x.RowVersion));

    private static List<TEnum> ParseEnums<TEnum>(string? csv) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(csv)) return new List<TEnum>();

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Select(s => Enum.TryParse<TEnum>(s, true, out var v) ? (TEnum?)v : null)
                  .Where(v => v is not null)
                  .Select(v => v!.Value)
                  .ToList();
    }

    private static IQueryable<ServiceRequest> ApplySort(
        IQueryable<ServiceRequest> q, string? sortBy, string? sortDir)
    {
        var desc = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

        return (sortBy?.ToLowerInvariant()) switch
        {
            "title"      => desc ? q.OrderByDescending(x => x.Title) : q.OrderBy(x => x.Title),
            "status"     => desc ? q.OrderByDescending(x => x.Status) : q.OrderBy(x => x.Status),
            "priority"   => desc ? q.OrderByDescending(x => x.Priority) : q.OrderBy(x => x.Priority),
            "updatedat"  => desc ? q.OrderByDescending(x => x.UpdatedAt) : q.OrderBy(x => x.UpdatedAt),
            _            => desc ? q.OrderByDescending(x => x.CreatedAt) : q.OrderBy(x => x.CreatedAt),
        };
    }

    private static Dictionary<string, int> FillAllStatuses(Dictionary<string, int> counts)
    {
        var result = Enum.GetNames<Status>().ToDictionary(n => n, _ => 0);
        foreach (var (k, v) in counts) result[k] = v;
        return result;
    }

    private static Dictionary<string, int> FillAllPriorities(Dictionary<string, int> counts)
    {
        var result = Enum.GetNames<Priority>().ToDictionary(n => n, _ => 0);
        foreach (var (k, v) in counts) result[k] = v;
        return result;
    }
}