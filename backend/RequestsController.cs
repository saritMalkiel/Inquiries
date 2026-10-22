using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Requests;

[ApiController]
[Route("api/requests")]
public class RequestsController : ControllerBase
{
    private readonly AppDb _db;
    public RequestsController(AppDb db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<PagedDto>> Get(
        int page = 1, int pageSize = 20,
        string? search = null, string? status = null, string? priority = null,
        CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest("page>=1, pageSize 1-100");

        var q = _db.Requests.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(x => x.Title.Contains(search) || x.OrganizationName.Contains(search));

        if (Enum.TryParse<Status>(status, true, out var s)) q = q.Where(x => x.Status == s);
        if (Enum.TryParse<Priority>(priority, true, out var p)) q = q.Where(x => x.Priority == p);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ItemDto(x.Id, x.Title, x.OrganizationName, x.Status.ToString(),
                                     x.Priority.ToString(), x.AssignedTo, x.CreatedAt,
                                     x.UpdatedAt, Convert.ToBase64String(x.RowVersion)))
            .ToListAsync(ct);

        return Ok(new PagedDto(page, pageSize, total, items));
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, StatusBody body, CancellationToken ct)
    {
        var entity = await _db.Requests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return NotFound();

        if (!Enum.TryParse<Status>(body.Status, true, out var next))
            return BadRequest("סטטוס לא חוקי");

        if (!string.IsNullOrEmpty(body.RowVersion))
            _db.Entry(entity).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(body.RowVersion);

        var prev = entity.Status;
        entity.Status = next;
        entity.UpdatedAt = DateTime.UtcNow;

        _db.StatusChanges.Add(new StatusChange
        {
            RequestId = entity.Id,
            PreviousStatus = prev,
            NewStatus = next,
            ChangedAt = entity.UpdatedAt
        });

        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict("עודכן ע\u05f4י משתמש אחר"); }
        return NoContent();
    }
}
