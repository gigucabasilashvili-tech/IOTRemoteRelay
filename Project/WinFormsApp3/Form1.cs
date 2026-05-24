using System;
using System.IO;
using System.IO.Ports;
using System.Management;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

namespace WinFormsApp3
{
    public partial class Form1 : Form
    {
        private System.Windows.Forms.Timer watchdogTimer;
        private string relayState = "ON";
        private HttpListener? listener;
        private bool isServerRunning = false;

        public class RelayStatus
        {
            public string state { get; set; } = "OFF";
            public string lastUpdate { get; set; } = "";
        }

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
        private string GetEspComPort()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption like '%COM%'"))
                {
                    foreach (var device in searcher.Get())
                    {
                        string caption = device["Caption"]?.ToString() ?? "";
                        if (caption.Contains("CP210x") || caption.Contains("CH340") || caption.Contains("USB Serial Device"))
                        {
                            int start = caption.IndexOf("(COM");
                            if (start != -1)
                            {
                                int end = caption.IndexOf(")", start);
                                if (end != -1)
                                {
                                    return caption.Substring(start + 1, end - start - 1);
                                }
                            }
                        }
                    }
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
            string comPort = GetEspComPort();

            if (!string.IsNullOrEmpty(comPort))
            {
                pnlPort.Visible = true;
                pnlPort.BringToFront();
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
                    listener.Prefixes.Add("http://*:8080/");
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

                this.Invoke(new Action(() =>
                {
                    lbCnctStRGB.Text = "Connected";
                    lbCnctStRGB.ForeColor = Color.Green;
                    watchdogTimer.Stop();
                    watchdogTimer.Start();
                }));

                var statusObj = new RelayStatus
                {
                    state = relayState,
                    lastUpdate = DateTime.Now.ToString("HH:mm:ss")
                };

                string jsonResponse = JsonSerializer.Serialize(statusObj);
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);

                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = buffer.Length;

                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
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
            if (!string.IsNullOrEmpty(comPort))
            {
                try
                {
                    using (SerialPort serial = new SerialPort(comPort, 115200))
                    {
                        serial.Open();
                        string configPayload = $"CFG:{ip}:{port}\n";
                        serial.Write(configPayload);

                        MessageBox.Show($"Configuration saved and pushed to ESP32", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Saved locally but failed sending to ESP32: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void btnPortSave_Click(object sender, EventArgs e)
        {
            ConfigSave();
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