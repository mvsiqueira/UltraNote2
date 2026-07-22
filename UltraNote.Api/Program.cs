using Microsoft.AspNetCore.DataProtection;
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

// Data Protection signs/encrypts the AttachmentCookie (see GoogleAuth.CookieScheme). Without
// a persisted key ring, ASP.NET Core keeps it in the container's ephemeral filesystem — every
// container recreate (redeploy) generates a new key and silently invalidates every cookie
// already issued, so already-logged-in users start getting 401s until their next token
// refresh. Persisting to the same volume as the SQLite db keeps the key stable across deploys.
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .SetApplicationName("UltraNote");
}

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
    FixLegacyAttachmentUrls(db);
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

// Attachment/image links baked into a note's ContentHtml carry two kinds of now-fixed
// mistakes from before today, both baked in permanently at insert time (there's no page
// context later to fix them against, unlike freshly-generated links):
//   1. An ABSOLUTE URL tied to whichever domain was current when inserted — see
//      UltraNoteApiClient's attachmentUrlBase doc. Notes touched before the
//      note-api.<domain> Cloudflare routes were retired (DEPLOY-QNAP.md's /ultranote
//      migration) still carry that now-dead domain and 404.
//   2. "Inserir link no texto" used to point at the download endpoint (?download=true,
//      forces a save-file prompt) instead of the view one — links inserted before that
//      changed still force a download instead of opening the file inline.
// One-time, idempotent (nothing left to match once fixed) — rewrites (1) to the same
// site-root-relative path new inserts use today (works under any of the 3 access domains)
// and strips (2) so old links behave like new ones.
static void FixLegacyAttachmentUrls(AppDbContext db)
{
    string[] legacyPrefixes =
    [
        "https://note-api.ultrasoft.app.br/api/attachments/",
        "https://note-api.ultrasoftinc.com.br/api/attachments/",
    ];
    const string replacement = "/ultranote/api-note/api/attachments/";
    const string downloadSuffix = "?download=true";

    var candidates = db.Notes
        .Where(n => EF.Functions.Like(n.ContentHtml, "%note-api.ultrasoft.app.br%")
                 || EF.Functions.Like(n.ContentHtml, "%note-api.ultrasoftinc.com.br%")
                 || EF.Functions.Like(n.ContentHtml, "%?download=true%"))
        .ToList();

    var fixedCount = 0;
    foreach (var note in candidates)
    {
        var updated = note.ContentHtml;
        foreach (var prefix in legacyPrefixes)
            updated = updated.Replace(prefix, replacement);
        updated = updated.Replace(downloadSuffix, "");
        if (updated != note.ContentHtml)
        {
            note.ContentHtml = updated;
            fixedCount++;
        }
    }
    if (fixedCount > 0)
    {
        db.SaveChanges();
        Console.WriteLine($"[UltraNote] Fixed legacy attachment URLs in {fixedCount} note(s).");
    }
}
