using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.Features;
using PrintAI.Planning;

namespace PrintAI.Web;

public static partial class LocalWorkflowEndpoints
{
    public const long MaxRequestBytes = 256L * 1024 * 1024;

    public static void Configure(WebApplicationBuilder builder) =>
        builder.Services.Configure<FormOptions>(options =>
            options.MultipartBodyLengthLimit = MaxRequestBytes);

    public static void MapLocalWorkflow(
        this WebApplication app,
        LocalWorkflowSession session,
        MacCupsPrinterAdapter printerAdapter,
        LocalWorkflowPlanner planner)
    {
        MapLocalWorkflowStreaming(app, session, planner);
        app.MapGet("/api/bootstrap", (HttpContext context) =>
            IsLoopbackRequest(context)
                ? Results.Ok(new { sessionToken = session.SessionToken })
                : Results.NotFound());

        app.MapGet("/api/local/sources", (HttpContext context) =>
            IsAuthorized(context, session)
                ? Results.Ok(session.ListSources())
                : Results.Unauthorized());

        app.MapGet("/api/local/printers", async (HttpContext context) =>
        {
            if (!IsAuthorized(context, session))
                return Results.Unauthorized();
            return Results.Ok(await printerAdapter.ListAsync(context.RequestAborted));
        });

        app.MapGet("/api/local/planner", (HttpContext context) =>
            IsAuthorized(context, session)
                ? Results.Ok(planner.Readiness)
                : Results.Unauthorized());

        app.MapGet("/api/local/sources/{sourceId:guid}/thumbnail", (
            HttpContext context, Guid sourceId) =>
        {
            if (!IsAuthorized(context, session))
                return Results.Unauthorized();
            try
            {
                return Results.File(session.RenderSourceThumbnail(sourceId), "image/png");
            }
            catch (LocalWorkflowException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentOutOfRangeException)
            {
                return Results.UnprocessableEntity(new { error = "Không tạo được thumbnail cho file này." });
            }
        });

        app.MapDelete("/api/local/sources", async (HttpContext context) =>
        {
            if (!IsAuthorized(context, session))
                return Results.Unauthorized();
            var request = await context.Request.ReadFromJsonAsync<RemoveLocalSourcesRequest>(
                cancellationToken: context.RequestAborted);
            if (request is null)
                return Results.BadRequest(new { error = "Chọn file cần xóa." });
            try
            {
                return Results.Ok(session.RemoveSources(request.SourceIds));
            }
            catch (LocalWorkflowException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

        app.MapPost("/api/local/plan", async (HttpContext context) =>
        {
            if (!IsAuthorized(context, session))
                return Results.Unauthorized();
            var request = await context.Request.ReadFromJsonAsync<LocalPlanRequest>(
                cancellationToken: context.RequestAborted);
            if (request is null)
                return Results.BadRequest(new { error = "Nhập yêu cầu in trước." });
            if (!planner.Readiness.IsReady)
                return Results.Problem("Không tìm thấy AGY CLI. Cài/đăng nhập AGY hoặc đặt PRINTAI_AGY_PATH.", statusCode: 503);
            try
            {
                return Results.Ok(await session.PlanJobAsync(request, planner, context.RequestAborted));
            }
            catch (LocalWorkflowException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (PlanningFormatException exception)
            {
                return Results.BadRequest(new { error = $"Kế hoạch AGY không hợp lệ: {exception.Message}" });
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                return Results.BadRequest(new { error = "Kích thước AGY đề xuất không vừa vùng in được trên giấy A4. Hãy yêu cầu thu vừa trang A4 và giữ đúng tỷ lệ." });
            }
        });

        app.MapPost("/api/local/files", async (
            HttpContext context,
            ILogger<LocalWorkflowSession> logger) =>
        {
            if (!IsAuthorized(context, session))
                return Results.Unauthorized();
            if (!context.Request.HasFormContentType)
                return Results.BadRequest(new { error = "Tệp cần được gửi dưới dạng multipart/form-data." });

            var requestLimit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (requestLimit is { IsReadOnly: false })
                requestLimit.MaxRequestBodySize = MaxRequestBytes;

            try
            {
                var form = await context.Request.ReadFormAsync(context.RequestAborted);
                var files = await session.AddFilesAsync(form.Files, context.RequestAborted);
                return Results.Ok(files);
            }
            catch (LocalWorkflowException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Local web upload was rejected.");
                return Results.BadRequest(new { error = "Không đọc được một tệp. Kiểm tra định dạng hoặc thử tệp khác." });
            }
        });

        app.MapPost("/api/local/jobs", async (HttpContext context) =>
        {
            if (!IsAuthorized(context, session))
                return Results.Unauthorized();

            try
            {
                var request = await context.Request.ReadFromJsonAsync<CreateLocalJobRequest>(
                    cancellationToken: context.RequestAborted);
                return request is null
                    ? Results.BadRequest(new { error = "Thiếu cấu hình job." })
                    : Results.Ok(session.CreateJob(request));
            }
            catch (LocalWorkflowException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = "Không thể xếp tệp với kích thước này trên giấy A4." });
            }
        });

        app.MapGet("/api/local/jobs/{jobId:guid}/preview/{page:int}", (
            HttpContext context,
            Guid jobId,
            int page) =>
        {
            if (!IsAuthorized(context, session))
                return Results.Unauthorized();

            try
            {
                return Results.File(session.RenderPreview(jobId, page), "image/png");
            }
            catch (LocalWorkflowException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
        });

        app.MapGet("/api/local/jobs/{jobId:guid}/pdf", (
            HttpContext context,
            Guid jobId) =>
        {
            if (!IsAuthorized(context, session))
                return Results.Unauthorized();

            try
            {
                return Results.File(
                    session.CreatePdf(jobId),
                    "application/pdf",
                    "PrintAI-A4.pdf");
            }
            catch (LocalWorkflowException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return Results.Problem("Không tạo được PDF. Hãy thử lại với job nhỏ hơn.");
            }
        });

        app.MapPost("/api/local/jobs/{jobId:guid}/print", async (
            HttpContext context, Guid jobId, ILogger<LocalWorkflowSession> logger) =>
        {
            if (!IsAuthorized(context, session))
                return Results.Unauthorized();
            var request = await context.Request.ReadFromJsonAsync<PrintLocalJobRequest>(
                cancellationToken: context.RequestAborted);
            if (request is null || string.IsNullOrWhiteSpace(request.PrinterName))
                return Results.BadRequest(new { error = "Chọn một máy in trước khi gửi." });
            try
            {
                var pdfPath = session.CreatePdf(jobId);
                return Results.Ok(await printerAdapter.SubmitAsync(
                    request.PrinterName, request.Copies, pdfPath, context.RequestAborted));
            }
            catch (LocalWorkflowException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "Could not prepare local print job {JobId}.", jobId);
                return Results.Problem("Không chuẩn bị được PDF in. Hãy thử tạo preview lại.");
            }
        });
    }

    internal static bool IsAuthorized(HttpContext context, LocalWorkflowSession session)
    {
        if (!IsLoopbackRequest(context))
            return false;

        var supplied = Encoding.UTF8.GetBytes(context.Request.Headers["X-PrintAI-Session"].ToString());
        var expected = Encoding.UTF8.GetBytes(session.SessionToken);
        return CryptographicOperations.FixedTimeEquals(supplied, expected);
    }

    private static bool IsLoopbackRequest(HttpContext context)
    {
        if (context.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address))
            return false;

        var host = context.Request.Host.Host;
        if (!host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
            !host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var origin = context.Request.Headers.Origin.ToString();
        return origin.Length == 0 ||
               (Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttp &&
                uri.IsLoopback &&
                uri.Authority.Equals(context.Request.Host.Value, StringComparison.OrdinalIgnoreCase));
    }
}
