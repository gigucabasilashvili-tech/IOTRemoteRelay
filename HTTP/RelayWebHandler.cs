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
        Action onEsp32Poll,
        bool requireAuth = false)
    {
        string path = context.Request.Url?.AbsolutePath ?? "/";
        string method = context.Request.HttpMethod;

        // ESP32 device API — no login.
        if (path.Equals("/status", StringComparison.OrdinalIgnoreCase) && method == "GET")
        {
            onEsp32Poll();
            await ServeStatusJsonAsync(context, getRelayState());
            return;
        }

        if (requireAuth && !IsAuthorized(context.Request))
        {
            await ServeUnauthorizedAsync(context);
            return;
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

        // Browser dashboard: read/update relay without affecting ESP32 connection status.
        if (path.Equals("/api/status", StringComparison.OrdinalIgnoreCase) && method == "GET")
        {
            await ServeStatusJsonAsync(context, getRelayState());
            return;
        }

        if (path.Equals("/api/toggle", StringComparison.OrdinalIgnoreCase) &&
            (method == "POST" || method == "GET"))
        {
            ToggleRelayState(getRelayState, setRelayState);
            await ServeToggleResponseAsync(context, method, getRelayState());
            return;
        }

        await ServeNotFoundAsync(context);
    }

    private static bool IsAuthorized(HttpListenerRequest request)
    {
        string? authHeader = request.Headers["Authorization"];
        if (string.IsNullOrEmpty(authHeader) ||
            !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            string encoded = authHeader["Basic ".Length..];
            string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            int colonIndex = decoded.IndexOf(':');
            if (colonIndex < 0)
                return false;

            string username = decoded[..colonIndex];
            string password = decoded[(colonIndex + 1)..];

            return username == RelayServerDefaults.WebUsername &&
                   password == RelayServerDefaults.WebPassword;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static async Task ServeUnauthorizedAsync(HttpListenerContext context)
    {
        const string body = "Login required";
        byte[] buffer = Encoding.UTF8.GetBytes(body);

        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        context.Response.Headers.Add("WWW-Authenticate", "Basic realm=\"Relay Control\"");
        context.Response.ContentType = "text/plain";
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer);
    }

    private static void ToggleRelayState(Func<string> getRelayState, Action<string> setRelayState)
    {
        string current = getRelayState();
        setRelayState(string.Equals(current, "ON", StringComparison.OrdinalIgnoreCase) ? "OFF" : "ON");
    }

    private static async Task ServeToggleResponseAsync(HttpListenerContext context, string method, string relayState)
    {
        if (method == "GET")
        {
            context.Response.StatusCode = (int)HttpStatusCode.Redirect;
            context.Response.RedirectLocation = "/";
            context.Response.Close();
            return;
        }

        var status = new { state = relayState, lastUpdate = DateTime.Now.ToString("HH:mm:ss") };
        string json = JsonSerializer.Serialize(status);
        byte[] buffer = Encoding.UTF8.GetBytes(json);

        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer);
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
        <button type="button" id="toggleBtn" class="btn {{STATE_CLASS}}">Turn {{TOGGLE_LABEL}}</button>
        <script>
        const statusEl = document.querySelector('.status-value');
        const toggleBtn = document.getElementById('toggleBtn');
        function applyState(state) {
            const on = state.toUpperCase() === 'ON';
            statusEl.textContent = on ? 'ON' : 'OFF';
            statusEl.className = 'status-value ' + (on ? 'ON' : 'OFF');
            toggleBtn.className = 'btn ' + (on ? 'ON' : 'OFF');
            toggleBtn.textContent = 'Turn ' + (on ? 'OFF' : 'ON');
        }
        async function refreshStatus() {
            const r = await fetch('/api/status');
            const data = await r.json();
            applyState(data.state);
        }
        toggleBtn.addEventListener('click', async () => {
            const r = await fetch('/api/toggle', { method: 'POST' });
            const data = await r.json();
            applyState(data.state);
        });
        setInterval(refreshStatus, 3000);
        </script>
        </body></html>
        """;
}
