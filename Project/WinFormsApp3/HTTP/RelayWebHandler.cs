using System.Net;
using System.Text;
using System.Text.Json;

namespace WinFormsApp3.HTTP;

/// <summary>
/// Serves the mobile web dashboard (/) and JSON status API (/status) for the ESP32.
/// </summary>
public sealed class RelayWebHandler
{
    private readonly string _webRoot;

    public RelayWebHandler()
    {
        _webRoot = Path.Combine(AppContext.BaseDirectory, "HTTP");
    }

    public async Task HandleAsync(
        HttpListenerContext context,
        Func<string> getRelayState,
        Action<string> setRelayState,
        Action onClientActivity)
    {
        string path = context.Request.Url?.AbsolutePath ?? "/";

        if (string.Equals(context.Request.QueryString["action"], "toggle", StringComparison.OrdinalIgnoreCase))
        {
            string current = getRelayState();
            setRelayState(string.Equals(current, "ON", StringComparison.OrdinalIgnoreCase) ? "OFF" : "ON");
        }

        if (path is "/" or "/index.html")
        {
            await ServeDashboardAsync(context, getRelayState());
            return;
        }

        if (path.Equals("/styles.css", StringComparison.OrdinalIgnoreCase))
        {
            await ServeStaticFileAsync(context, "styles.css", "text/css");
            return;
        }

        if (path.Equals("/status", StringComparison.OrdinalIgnoreCase))
        {
            onClientActivity();
            await ServeStatusJsonAsync(context, getRelayState());
            return;
        }

        await ServeNotFoundAsync(context);
    }

    private async Task ServeDashboardAsync(HttpListenerContext context, string relayState)
    {
        string html = await LoadDashboardHtmlAsync(relayState);
        byte[] buffer = Encoding.UTF8.GetBytes(html);

        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer);
    }

    private async Task ServeStatusJsonAsync(HttpListenerContext context, string relayState)
    {
        var status = new
        {
            state = relayState,
            lastUpdate = DateTime.Now.ToString("HH:mm:ss"),
        };

        string json = JsonSerializer.Serialize(status);
        byte[] buffer = Encoding.UTF8.GetBytes(json);

        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer);
    }

    private async Task ServeStaticFileAsync(HttpListenerContext context, string fileName, string contentType)
    {
        string filePath = Path.Combine(_webRoot, fileName);
        if (!File.Exists(filePath))
        {
            await ServeNotFoundAsync(context);
            return;
        }

        byte[] buffer = await File.ReadAllBytesAsync(filePath);
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = contentType;
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer);
    }

    private async Task ServeNotFoundAsync(HttpListenerContext context)
    {
        const string body = "Not Found";
        byte[] buffer = Encoding.UTF8.GetBytes(body);
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        context.Response.ContentType = "text/plain";
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer);
    }

    private async Task<string> LoadDashboardHtmlAsync(string relayState)
    {
        string templatePath = Path.Combine(_webRoot, "dashboard.html");
        string template = File.Exists(templatePath)
            ? await File.ReadAllTextAsync(templatePath)
            : GetEmbeddedDashboardTemplate();

        bool isOn = string.Equals(relayState, "ON", StringComparison.OrdinalIgnoreCase);
        string stateClass = isOn ? "ON" : "OFF";
        string toggleLabel = isOn ? "OFF" : "ON";

        return template
            .Replace("{{RELAY_STATE}}", relayState, StringComparison.Ordinal)
            .Replace("{{STATE_CLASS}}", stateClass, StringComparison.Ordinal)
            .Replace("{{TOGGLE_LABEL}}", toggleLabel, StringComparison.Ordinal);
    }

    private static string GetEmbeddedDashboardTemplate() =>
        """
        <!DOCTYPE html>
        <html><head><meta name="viewport" content="width=device-width, initial-scale=1.0" />
        <title>Relay</title></head>
        <body style="font-family:sans-serif;text-align:center;margin-top:48px;background:#222;color:#fff;">
        <h1>Relay Control</h1>
        <p>Status: <strong class="{{STATE_CLASS}}">{{RELAY_STATE}}</strong></p>
        <a href="?action=toggle" class="btn {{STATE_CLASS}}">Turn {{TOGGLE_LABEL}}</a>
        <script>setTimeout(() => location.reload(), 3000);</script>
        </body></html>
        """;
}
