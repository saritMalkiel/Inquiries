using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Request = Requests.ServiceRequest;
namespace Requests;

[ApiController]
[Route("api/dev")]
public class DevController : ControllerBase
{
    private readonly AppDb _db;
    public DevController(AppDb db) => _db = db;

    [HttpPost("seed")]
    public async Task<IActionResult> Seed(int count = 100000, bool reset = false, CancellationToken ct = default)
    {
        if (reset)
        {
            await _db.StatusChanges.ExecuteDeleteAsync(ct);
            await _db.Requests.ExecuteDeleteAsync(ct);
        }

        var orgs = new[] { "עיריית בני ברק", "עיריית ירושלים", "מוסדות החינוך", "קופת חולים" };
        var rnd = new Random(42);

        for (int i = 0; i < count; i += 5000)
        {
            var size = Math.Min(5000, count - i);
            for (int j = 0; j < size; j++)
            {
                var created = DateTime.UtcNow.AddDays(-rnd.Next(0, 365));
                _db.Requests.Add(new Request
                {
                    Title = $"פנייה {i + j + 1}",
                    OrganizationName = orgs[rnd.Next(orgs.Length)],
                    Status = (Status)rnd.Next(4),
                    Priority = (Priority)rnd.Next(3),
                    AssignedTo = "מטפל " + (char)('א' + rnd.Next(6)),
                    CreatedAt = created,
                    UpdatedAt = created
                });
            }
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
        }

        return Ok(new { created = count });
    }
}
