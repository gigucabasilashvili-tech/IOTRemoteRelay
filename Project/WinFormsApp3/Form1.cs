using System;
using System.IO;
using System.IO.Ports;
using System.Management;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using WinFormsApp3.HTTP;

namespace WinFormsApp3
{
    public partial class Form1 : Form
    {
        private System.Windows.Forms.Timer watchdogTimer;
        private string relayState = "ON";
        private HttpListener? webListener;
        private HttpListener? espListener;
        private bool isServerRunning = false;
        private readonly RelayWebHandler webHandler = new();

        public Form1()
        {
            InitializeComponent();

            pnlLogin.Visible = true;
            pnlPort.Visible = false;

            pnlLogin.Dock = DockStyle.Fill;
            pnlMain.Dock = DockStyle.Fill;

            tbPort.Text = RelayServerDefaults.EspListenPort;

            watchdogTimer = new System.Windows.Forms.Timer();
            watchdogTimer.Interval = 90000;
            watchdogTimer.Tick += WatchdogTimer_Tick;

            UpdateRelayUI();
            this.FormClosing += Form1_FormClosing;
        }

        private void Form1_Load(object sender, EventArgs e) { }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            StopServers();
        }

        private static readonly string[] EspUsbVendorIds =
        {
            "VID_10C4",
            "VID_1A86",
            "VID_303A",
            "VID_0403",
        };

        private static readonly string[] EspCaptionKeywords =
            // es aris arduinos saxelebis sia ritic xvdeba rom ESP32 USB-tia dakavshirebuli
        {
            "CP210", "CH340", "CH343", "CH910", "Silicon Labs", "UART", "ESP32",
            "USB JTAG", "Enhanced SERIAL", "Silicon Labs CP210x USB to UART Bridge", "Silicon Labs"
        };

        private static string? ExtractComPortFromCaption(string caption)
        {
            int start = caption.IndexOf("(COM", StringComparison.OrdinalIgnoreCase);
            if (start == -1) return null;

            int end = caption.IndexOf(')', start);
            if (end == -1) return null;

            return caption.Substring(start + 1, end - start - 1);
        }

        private static bool IsLikelyEsp32SerialDevice(string caption, string pnpDeviceId)
        {
            foreach (string vendorId in EspUsbVendorIds)
            {
                if (pnpDeviceId.Contains(vendorId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            foreach (string keyword in EspCaptionKeywords)
            {
                if (caption.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private string GetEspComPort()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Caption, PNPDeviceID FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'");

                foreach (var device in searcher.Get())
                {
                    string caption = device["Caption"]?.ToString() ?? "";
                    string pnpDeviceId = device["PNPDeviceID"]?.ToString() ?? "";

                    if (!IsLikelyEsp32SerialDevice(caption, pnpDeviceId))
                        continue;

                    string? comPort = ExtractComPortFromCaption(caption);
                    if (!string.IsNullOrEmpty(comPort))
                        return comPort;
                }
            }
            catch (ManagementException mEx)
            {
                Console.WriteLine("WMI Query Failed in port extraction: " + mEx.Message);
            }

            return string.Empty;
        }

        private void CheckForEsp32Usb()
        {
            bool espDetected = !string.IsNullOrEmpty(GetEspComPort());
            pnlPort.Visible = espDetected;

            if (espDetected)
                pnlPort.BringToFront();
        }

        private static bool WaitForSerialToken(SerialPort serial, string token, TimeSpan timeout, out string captured)
        {
            captured = string.Empty;
            var deadline = DateTime.UtcNow.Add(timeout);

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    captured += serial.ReadExisting();
                }
                catch (TimeoutException)
                {
                }

                if (captured.Contains(token, StringComparison.Ordinal))
                    return true;

                Thread.Sleep(50);
            }

            return false;
        }

        private bool TryPushConfigToEsp32(string comPort, string ip, string port, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                using var serial = new SerialPort(comPort, 115200)
                {
                    Encoding = Encoding.UTF8,
                    NewLine = "\n",
                    DtrEnable = true,
                    RtsEnable = true,
                    ReadTimeout = 500,
                    WriteTimeout = 3000,
                };

                serial.Open();

                serial.DtrEnable = false;
                serial.RtsEnable = true;
                Thread.Sleep(100);

                serial.DtrEnable = true;
                serial.RtsEnable = false;
                Thread.Sleep(100);

                if (!WaitForSerialToken(serial, "READY", TimeSpan.FromSeconds(12), out string bootLog))
                {
                    errorMessage = "ESP32 did not respond after USB connect. Close Serial Monitor and re-flash the firmware if needed.";
                    return false;
                }

                serial.DiscardInBuffer();
                serial.WriteLine($"CFG:{ip}:{port}");

                if (WaitForSerialToken(serial, "Configuration saved to Flash", TimeSpan.FromSeconds(4), out _))
                    return true;

                errorMessage = string.IsNullOrWhiteSpace(bootLog)
                    ? "No confirmation from ESP32"
                    : "No confirmation from ESP32. Last device output: " + bootLog.Trim();
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private async void BtnLogin_Click(object sender, EventArgs e)
        {
            if (tbUsr.Text == "admin" && tbPas.Text == "1234")
            {
                pnlLogin.Visible = false;
                pnlMain.Visible = true;
                CheckForEsp32Usb();
                await StartServersAsync();
            }
            else
            {
                MessageBox.Show("Incorrect Username or Password", "Login Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateRelayUI()
        {
            lbRlStRGB.Text = relayState;
            lbRlStRGB.ForeColor = (relayState == "ON") ? Color.Green : Color.Red;
        }

        private void btnPower_Click(object sender, EventArgs e)
        {
            relayState = (relayState == "ON") ? "OFF" : "ON";
            UpdateRelayUI();
        }

        private async void btnServerStart_Click(object sender, EventArgs e)
        {
            if (isServerRunning)
            {
                StopServers();
                ResetUI();
                return;
            }

            lbCnctStRGB.Text = "Starting...";
            lbCnctStRGB.ForeColor = Color.Orange;
            btnServerStart.Enabled = false;

            await StartServersAsync();
        }

        private string GetEspListenPort()
        {
            string port = tbPort.Text.Trim();
            return string.IsNullOrEmpty(port) ? RelayServerDefaults.EspListenPort : port;
        }

        public async Task StartServersAsync()
        {
            if (isServerRunning)
                return;

            try
            {
                StopListenersOnly();

                webListener = new HttpListener();
                webListener.Prefixes.Add($"http://*:{RelayServerDefaults.WebDashboardPort}/");
                webListener.Start();

                espListener = new HttpListener();
                espListener.Prefixes.Add($"http://*:{GetEspListenPort()}/");
                espListener.Start();

                isServerRunning = true;
                btnServerStart.Enabled = true;
                btnServerStart.Text = "Stop Server";
                lbCnctStRGB.Text = "Waiting for ESP32...";
                lbCnctStRGB.ForeColor = Color.Blue;

                _ = AcceptLoopAsync(webListener, espPoll: false);
                _ = AcceptLoopAsync(espListener, espPoll: true);

                await Task.CompletedTask;
            }
            catch (HttpListenerException hEx)
            {
                MessageBox.Show(
                    "Error starting HTTP servers. Run as Administrator or register URL ACLs:\n" +
                    $"netsh http add urlacl url=http://*:{RelayServerDefaults.WebDashboardPort}/ user=Everyone\n" +
                    $"netsh http add urlacl url=http://*:{GetEspListenPort()}/ user=Everyone\n\n" +
                    hEx.Message,
                    "Server Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                ResetUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show("SERVER ERROR: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ResetUI();
            }
        }

        private async Task AcceptLoopAsync(HttpListener activeListener, bool espPoll)
        {
            while (isServerRunning)
            {
                try
                {
                    HttpListenerContext context = await activeListener.GetContextAsync();
                    _ = Task.Run(async () => await ProcessRequest(context, espPoll));
                }
                catch (HttpListenerException) when (!isServerRunning)
                {
                    break;
                }
                catch (ObjectDisposedException) when (!isServerRunning)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Accept loop error: " + ex.Message);
                }
            }
        }

        private async Task ProcessRequest(HttpListenerContext context, bool espPoll)
        {
            HttpListenerResponse response = context.Response;

            try
            {
                if (context.Request.HasEntityBody)
                {
                    using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    {
                        await reader.ReadToEndAsync();
                    }
                }

                await webHandler.HandleAsync(
                    context,
                    () => relayState,
                    newState =>
                    {
                        void Apply()
                        {
                            relayState = newState;
                            UpdateRelayUI();
                        }

                        if (InvokeRequired)
                            Invoke(Apply);
                        else
                            Apply();
                    },
                    espPoll ? MarkEsp32Connected : () => { },
                    requireAuth: !espPoll);

                response.OutputStream.Flush();
            }
            catch (Exception ex) when (ex is ObjectDisposedException || ex is HttpListenerException)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine("Data Processing Error: " + ex.Message);
            }
            finally
            {
                try
                {
                    if (espPoll)
                    {
                        await Task.Delay(2000);
                    }
                    response.Close();
                }
                catch { }
            }
        }

        private void MarkEsp32Connected()
        {
            void Update()
            {
                lbCnctStRGB.Text = "ESP32 Connected";
                lbCnctStRGB.ForeColor = Color.Green;
                watchdogTimer.Stop();
                watchdogTimer.Start();
            }

            if (InvokeRequired)
                Invoke(Update);
            else
                Update();
        }

        private void WatchdogTimer_Tick(object? sender, EventArgs e)
        {
            lbCnctStRGB.Text = "ESP32 Timeout";
            lbCnctStRGB.ForeColor = Color.Red;
            watchdogTimer.Stop();
        }

        private void StopListenersOnly()
        {
            try { webListener?.Stop(); } catch { }
            try { webListener?.Close(); } catch { }
            webListener = null;

            try { espListener?.Stop(); } catch { }
            try { espListener?.Close(); } catch { }
            espListener = null;
        }

        private void StopServers()
        {
            isServerRunning = false;
            StopListenersOnly();
        }

        private void ResetUI()
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(ResetUI));
                return;
            }

            btnServerStart.Enabled = true;
            btnServerStart.Text = "Start Server";
            lbCnctStRGB.Text = "Disconnected";
            lbCnctStRGB.ForeColor = Color.Red;
            isServerRunning = false;
            StopListenersOnly();
        }

        private async void ConfigSave()
        {
            string ip = tbIP.Text.Trim();
            string port = GetEspListenPort();

            if (string.IsNullOrEmpty(ip))
            {
                MessageBox.Show("Please enter a valid public IP address.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            tbPort.Text = port;

            try
            {
                bool wasRunning = isServerRunning;
                StopServers();

                if (wasRunning)
                    await StartServersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            string comPort = GetEspComPort();
            if (string.IsNullOrEmpty(comPort))
            {
                MessageBox.Show(
                    "PC server settings updated, but no ESP32 USB port was detected. Plug in the board and try again.",
                    "ESP32 Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (TryPushConfigToEsp32(comPort, ip, port, out string espError))
            {
                MessageBox.Show(
                    $"Configuration saved on PC and written to ESP32 on {comPort}.\n" +
                    $"Device will connect to {ip}:{port} via SIM800L.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    $"PC server settings updated, but ESP32 write failed: {espError}",
                    "ESP32 Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void btnPortSave_Click(object sender, EventArgs e)
        {
            ConfigSave();
            pnlPort.Visible = false;
        }

        private void btnConfigCl_Click(object sender, EventArgs e)
        {
            pnlPort.Visible = false;
        }

        private void textBox1_TextChanged(object sender, EventArgs e) { }
        private void lbCred_Click(object sender, EventArgs e) { }
        private void LGNpassword_Click(object sender, EventArgs e) { }
        private void pnlLogin_Paint(object sender, PaintEventArgs e) { }
        private void pnlMain_Paint(object sender, PaintEventArgs e) { }
        private void lbMac_Click(object sender, EventArgs e) { }
        private void label1_Click(object sender, EventArgs e) { }
    }
}