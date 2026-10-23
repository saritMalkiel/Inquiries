namespace Requests;

public static class DbSeeder
{
    public static void Seed(AppDb db)
    {
        if (db.Requests.Any()) return;

        var now = DateTime.UtcNow;
        db.Requests.AddRange(
            new ServiceRequest { Title = "בקשת רישום לתלמוד תורה", OrganizationName = "תלמוד תורה עץ חיים", Status = Status.New, Priority = Priority.High, AssignedTo = "יוסף כהן", CreatedAt = now.AddDays(-12), UpdatedAt = now.AddDays(-12) },
            new ServiceRequest { Title = "עדכון פרטי התאמה", OrganizationName = "בית יעקב", Status = Status.InProgress, Priority = Priority.Medium, AssignedTo = "רבקה לוי", CreatedAt = now.AddDays(-9), UpdatedAt = now.AddDays(-2) },
            new ServiceRequest { Title = "בקשת מימון שנתי", OrganizationName = "כולל אברכים", Status = Status.Waiting, Priority = Priority.High, AssignedTo = "משה פרידמן", CreatedAt = now.AddDays(-7), UpdatedAt = now.AddDays(-1) },
            new ServiceRequest { Title = "תיקון כתובת למשלוח", OrganizationName = "גמילות חסדים", Status = Status.Completed, Priority = Priority.Low, AssignedTo = "שרה שטרן", CreatedAt = now.AddDays(-20), UpdatedAt = now.AddDays(-5) },
            new ServiceRequest { Title = "בקשת הצטרפות למוסד", OrganizationName = "בית יעקב", Status = Status.New, Priority = Priority.Medium, CreatedAt = now.AddDays(-3), UpdatedAt = now.AddDays(-3) }
        );
        db.SaveChanges();

        var first = db.Requests.OrderBy(x => x.Id).First();
        db.StatusChanges.Add(new StatusChange
        {
            RequestId = first.Id,
            PreviousStatus = Status.New,
            NewStatus = Status.InProgress,
            ChangedAt = now.AddDays(-10),
            ChangedBy = "מנהל מערכת"
        });
        db.SaveChanges();
    }
}