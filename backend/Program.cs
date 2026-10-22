using Microsoft.EntityFrameworkCore;
using Requests;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDb>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Db")));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Automatic migration on startup – convenient for development; in production, run as a separate step.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();
