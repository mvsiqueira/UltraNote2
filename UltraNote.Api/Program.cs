using Microsoft.EntityFrameworkCore;
using UltraNote.Api.Auth;
using UltraNote.Api.Data;
using UltraNote.Api.Endpoints;
using UltraNote.Api.Storage;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Db")
    ?? "Data Source=ultranote.db";

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connectionString));
builder.Services.AddSingleton<IAttachmentStorage, FileSystemAttachmentStorage>();
builder.Services.AddGoogleAuth(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS: allow the desktop/web clients to call the API. When specific origins are
// configured (production), credentials (the AttachmentCookie) are allowed too — the
// browser rejects AllowCredentials combined with AllowAnyOrigin, so dev (no configured
// origins) falls back to the permissive, credentials-less policy.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    p.AllowAnyHeader().AllowAnyMethod();
    if (corsOrigins.Length > 0) p.WithOrigins(corsOrigins).AllowCredentials();
    else p.AllowAnyOrigin();
}));

var app = builder.Build();

// Apply migrations and enable WAL for better concurrent read/write behaviour.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

var authOptions = app.Services.GetRequiredService<AuthOptions>();

var api = app.MapGroup("/api");
api.MapFolderEndpoints();
api.MapNoteEndpoints();
api.MapAttachmentEndpoints();
api.MapAuthEndpoints();

// Only enforce the allowlist when auth is enabled; otherwise the API stays open for local dev.
if (authOptions.Enabled)
    api.RequireAuthorization(GoogleAuth.EmailAllowlistPolicy);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
