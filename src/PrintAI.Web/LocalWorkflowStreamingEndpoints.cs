using System.Text.Json;
using System.Threading.Channels;
using PrintAI.Planning;

namespace PrintAI.Web;

public static partial class LocalWorkflowEndpoints
{
    private static readonly JsonSerializerOptions StreamJsonOptions =
        new(JsonSerializerDefaults.Web);

    private static void MapLocalWorkflowStreaming(
        WebApplication app,
        LocalWorkflowSession session,
        LocalWorkflowPlanner planner)
    {
        app.MapPost("/api/local/plan/stream", async context =>
        {
            if (!IsAuthorized(context, session))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var request = await context.Request.ReadFromJsonAsync<LocalPlanRequest>(
                cancellationToken: context.RequestAborted);
            if (request is null)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(
                    new { error = "Nhập yêu cầu in trước." }, context.RequestAborted);
                return;
            }
            if (!planner.Readiness.IsReady)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsJsonAsync(
                    new { error = "Không tìm thấy AGY CLI. Cài/đăng nhập AGY hoặc đặt PRINTAI_AGY_PATH." },
                    context.RequestAborted);
                return;
            }

            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.Append("X-Accel-Buffering", "no");
            var channel = Channel.CreateUnbounded<StreamEvent>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
            var progress = new InlineProgress<PlannerProgressUpdate>(update =>
                channel.Writer.TryWrite(new("progress", update)));
            var planning = RunPlanAsync(session, planner, request, progress, channel.Writer,
                context.RequestAborted);

            try
            {
                await foreach (var item in channel.Reader.ReadAllAsync(context.RequestAborted))
                {
                    await context.Response.WriteAsync(
                        $"event: {item.Name}\ndata: {JsonSerializer.Serialize(item.Data, StreamJsonOptions)}\n\n",
                        context.RequestAborted);
                    await context.Response.Body.FlushAsync(context.RequestAborted);
                }
                await planning;
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // The client disconnected; the request token cancels the AGY process as well.
            }
        });
    }

    private static async Task RunPlanAsync(
        LocalWorkflowSession session,
        LocalWorkflowPlanner planner,
        LocalPlanRequest request,
        IProgress<PlannerProgressUpdate> progress,
        ChannelWriter<StreamEvent> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await session.PlanJobAsync(request, planner, cancellationToken, progress);
            writer.TryWrite(new("complete", result));
        }
        catch (LocalWorkflowException exception)
        {
            writer.TryWrite(new("error", new { error = exception.Message }));
        }
        catch (PlanningFormatException)
        {
            writer.TryWrite(new("error", new { error = "AGY trả về kế hoạch không đúng định dạng. Hãy gửi yêu cầu lại; ứng dụng chưa tạo preview hay gửi in." }));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            writer.TryWrite(new("error", new { error = "Kích thước AGY đề xuất không vừa vùng in được trên giấy A4. Hãy yêu cầu thu vừa trang A4 và giữ đúng tỷ lệ." }));
        }
        catch (PlannerTransportException exception)
        {
            writer.TryWrite(new("error", new { error = $"Không nhận được phản hồi từ AGY: {exception.Message}" }));
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private sealed record StreamEvent(string Name, object Data);

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
