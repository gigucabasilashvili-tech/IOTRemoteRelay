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
        private HttpListener? listener;
        private bool isServerRunning = false;
        private readonly RelayWebHandler webHandler = new();

        public Form1()
        {
            InitializeComponent();

            pnlMain.Visible = false;
            pnlLogin.Visible = true;
            pnlPort.Visible = false;

            pnlLogin.Dock = DockStyle.Fill;
            pnlMain.Dock = DockStyle.Fill;

            watchdogTimer = new System.Windows.Forms.Timer();
            watchdogTimer.Interval = 3000; //3 second timeout
            watchdogTimer.Tick += WatchdogTimer_Tick;

            UpdateRelayUI();
            this.FormClosing += Form1_FormClosing;
        }

        private void Form1_Load(object sender, EventArgs e) { }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            isServerRunning = false;
            try
            {
                listener?.Stop();
                listener?.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error closing listener: " + ex.Message);
            }
        }
        private static readonly string[] EspUsbVendorIds =
        {
            "VID_10C4", // Silicon Labs CP210x
            "VID_1A86", // WCH CH340/CH343
            "VID_303A", // Espressif native USB (ESP32-S2/S3/C3)
            "VID_0403", // FTDI
        };

        private static readonly string[] EspCaptionKeywords =
        {
            "CP210", "CH340", "CH343", "CH910", "Silicon Labs", "UART", "ESP32",
            "USB JTAG", "Enhanced SERIAL",
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
                    DtrEnable = false,
                    RtsEnable = false,
                    ReadTimeout = 500,
                    WriteTimeout = 3000,
                };

                serial.Open();
                // Opening USB-UART often resets the ESP32; wait until firmware prints READY.
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

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            if (tbUsr.Text == "admin" && tbPas.Text == "1234")
            {
                pnlLogin.Visible = false;
                pnlMain.Visible = true;
                CheckForEsp32Usb();
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
            lbCnctStRGB.Text = "Starting...";
            lbCnctStRGB.ForeColor = Color.Orange;
            btnServerStart.Enabled = false;

            await ServerStart();
        }

        public async Task ServerStart()
        {
            try
            {
                if (listener == null)
                {
                    listener = new HttpListener();
                    listener.Prefixes.Add($"http://*:{RelayServerDefaults.Port}/");
                }

                listener.Start();
                isServerRunning = true;

                lbCnctStRGB.Text = "Waiting for Client...";
                lbCnctStRGB.ForeColor = Color.Blue;

                while (isServerRunning)
                {
                    HttpListenerContext context = await listener.GetContextAsync();
                    _ = Task.Run(async () => await ProcessRequest(context));
                }
            }
            catch (HttpListenerException hEx)
            {
                MessageBox.Show("Error: " + hEx.Message, "Server Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ResetUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show("SERVER ERROR: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ResetUI();
            }
        }

        private async Task ProcessRequest(HttpListenerContext context)
        {
            try
            {
                using (var reader = new StreamReader(context.Request.InputStream))
                {
                    await reader.ReadToEndAsync();
                }

                MarkClientConnected();

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
                    () => { });
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
                try { context.Response.Close(); } catch { }
            }
        }

        private void MarkClientConnected()
        {
            void Update()
            {
                lbCnctStRGB.Text = "Connected";
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
            lbCnctStRGB.Text = "Client Timeout";
            lbCnctStRGB.ForeColor = Color.Red;
            watchdogTimer.Stop();
        }

        private void ResetUI()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new MethodInvoker(ResetUI));
                return;
            }

            btnServerStart.Enabled = true;
            lbCnctStRGB.Text = "Disconnected";
            lbCnctStRGB.ForeColor = Color.Red;
            isServerRunning = false;

            try { listener?.Stop(); } catch { }
        }

        private void ConfigSave()
        {
            string ip = tbIP.Text.Trim();
            string port = tbPort.Text.Trim();

            if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(port))
            {
                MessageBox.Show("please enter valip ip address and port number", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                bool wasRunning = isServerRunning;
                if (isServerRunning)
                {
                    isServerRunning = false;
                    listener?.Stop();
                }

                listener = new HttpListener();
                listener.Prefixes.Add($"http://*:{port}/");

                if (wasRunning)
                {
                    _ = ServerStart();
                }
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
                    $"Configuration saved on PC and written to ESP32 on {comPort}.",
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