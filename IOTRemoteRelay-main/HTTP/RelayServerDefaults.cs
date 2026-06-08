namespace WinFormsApp3.HTTP;

public static class RelayServerDefaults
{
    /// <summary>Browser dashboard and mobile web UI.</summary>
    public const string WebDashboardPort = "8080";

    /// <summary>ESP32 polls /status on this port (public IP + this port saved to device).</summary>
    public const string EspListenPort = "65050";
}
