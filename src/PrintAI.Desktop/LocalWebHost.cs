using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Threading;

namespace PrintAI.Desktop;

public sealed class LocalWebHost : IAsyncDisposable
{
    public const int DefaultPort = 5271;
    private const long MaxRequestBytes = 256L * 1024 * 1024;

    private readonly WebApplication _app;
    private readonly LocalWebSession _session;

    private LocalWebHost(
        WebApplication app,
        LocalWebSession session,
        Uri baseUri)
    {
        _app = app;
        _session = session;
        BaseUri = baseUri;
    }

    public Uri BaseUri { get; }

    public static async Task<LocalWebHost> StartAsync(
        DesktopSession desktopSession,
        Dispatcher dispatcher,
        int port = DefaultPort,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(desktopSession);
        ArgumentNullException.ThrowIfNull(dispatcher);

        var webRoot = Path.Combine(
            AppContext.BaseDirectory,
            "local-web");

        if (!Directory.Exists(webRoot))
        {
            throw new DirectoryNotFoundException(
                $"Local web assets are missing: {webRoot}");
        }

        var options = new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = webRoot,
            Args = []
        };

        var builder = WebApplication.CreateBuilder(options);
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.WebHost.ConfigureKestrel(kestrel =>
            kestrel.Limits.MaxRequestBodySize = MaxRequestBytes);

        builder.Services.Configure<FormOptions>(form =>
            form.MultipartBodyLengthLimit = MaxRequestBytes);

        var localSession = new LocalWebSession(
            desktopSession,
            dispatcher);

        builder.Services.AddSingleton(localSession);

        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            if (!IsAllowedLoopbackRequest(context, port))
            {
                context.Response.StatusCode =
                    StatusCodes.Status403Forbidden;
                return;
            }

            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.Headers["Cache-Control"] =
                    "no-store, no-cache, must-revalidate";
            }

            await next();
        });

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            service = "PrintAI.LocalWeb",
            loopbackOnly = true,
            port
        }));

        app.MapGet("/api/bootstrap", async (
            LocalWebSession session,
            CancellationToken ct) =>
        {
            var state = await session.ReadStateAsync(ct);
            return Results.Ok(new
            {
                sessionToken = session.Token,
                state
            });
        });

        app.MapGet("/api/state", async (
            HttpContext context,
            LocalWebSession session,
            CancellationToken ct) =>
        {
            if (!HasSessionToken(context, session))
                return Results.Unauthorized();

            return Results.Ok(await session.ReadStateAsync(ct));
        });

        app.MapPost("/api/files", async (
            HttpContext context,
            LocalWebSession session,
            CancellationToken ct) =>
        {
            if (!HasSessionToken(context, session))
                return Results.Unauthorized();

            if (!context.Request.HasFormContentType)
            {
                return Results.BadRequest(
                    new { error = "multipart/form-data is required." });
            }

            try
            {
                var form = await context.Request.ReadFormAsync(ct);
                var state = await session.AddUploadedFilesAsync(
                    form.Files,
                    ct);

                return Results.Ok(state);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new
                {
                    error = ex.Message
                });
            }
        });

        app.MapPost("/api/action", async (
            HttpContext context,
            LocalWebSession session,
            LocalWebAction action,
            CancellationToken ct) =>
        {
            if (!HasSessionToken(context, session))
                return Results.Unauthorized();

            try
            {
                return Results.Ok(
                    await session.ExecuteAsync(action, ct));
            }
            catch (Exception ex) when (
                ex is ArgumentException or
                InvalidOperationException or
                NotSupportedException)
            {
                return Results.BadRequest(new
                {
                    error = ex.Message
                });
            }
        });

        app.MapFallbackToFile("index.html");

        await app.StartAsync(cancellationToken);

        return new(
            app,
            localSession,
            new Uri($"http://127.0.0.1:{port}/"));
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _app.StopAsync();
        }
        finally
        {
            _session.Dispose();
            await _app.DisposeAsync();
        }
    }

    private static bool HasSessionToken(
        HttpContext context,
        LocalWebSession session) =>
        context.Request.Headers.TryGetValue(
            "X-PrintAI-Session",
            out var supplied) &&
        string.Equals(
            supplied.ToString(),
            session.Token,
            StringComparison.Ordinal);

    private static bool IsAllowedLoopbackRequest(
        HttpContext context,
        int port)
    {
        var remote = context.Connection.RemoteIpAddress;
        if (remote is null || !IPAddress.IsLoopback(remote))
            return false;

        var host = context.Request.Host;
        if (host.Port is not null && host.Port != port)
            return false;

        return string.Equals(
                   host.Host,
                   "127.0.0.1",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   host.Host,
                   "localhost",
                   StringComparison.OrdinalIgnoreCase);
    }
}
