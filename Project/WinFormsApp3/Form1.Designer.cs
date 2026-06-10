namespace WinFormsApp3
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            pnlMain = new Panel();
            lbMac = new Label();
            btnServerStart = new Button();
            lbCnctStRGB = new Label();
            lbRlStRGB = new Label();
            btnPower = new Button();
            lbRlSt = new Label();
            lbCnctSt = new Label();
            pnlLogin = new Panel();
            lbCred = new Label();
            BtnLogin = new Button();
            tbPas = new TextBox();
            LGNpassword = new Label();
            LGNusername = new Label();
            tbUsr = new TextBox();
            pnlPort = new Panel();
            btnConfigCl = new Button();
            lbDPort = new Label();
            lbConfig = new Label();
            tbPort = new TextBox();
            lbPort = new Label();
            tbIP = new TextBox();
            lbIP = new Label();
            btnPortSave = new Button();
            pnlMain.SuspendLayout();
            pnlLogin.SuspendLayout();
            pnlPort.SuspendLayout();
            SuspendLayout();
            // 
            // pnlMain
            // 
            pnlMain.Controls.Add(lbMac);
            pnlMain.Controls.Add(btnServerStart);
            pnlMain.Controls.Add(lbCnctStRGB);
            pnlMain.Controls.Add(lbRlStRGB);
            pnlMain.Controls.Add(btnPower);
            pnlMain.Controls.Add(lbRlSt);
            pnlMain.Controls.Add(lbCnctSt);
            pnlMain.Dock = DockStyle.Fill;
            pnlMain.Location = new Point(0, 0);
            pnlMain.Name = "pnlMain";
            pnlMain.Size = new Size(800, 450);
            pnlMain.TabIndex = 0;
            pnlMain.Paint += pnlMain_Paint;
            // 
            // lbMac
            // 
            lbMac.AutoSize = true;
            lbMac.Font = new Font("Segoe UI", 11F);
            lbMac.Location = new Point(49, 138);
            lbMac.Name = "lbMac";
            lbMac.Size = new Size(99, 20);
            lbMac.TabIndex = 6;
            lbMac.Text = "MAC address:";
            lbMac.Click += lbMac_Click;
            // 
            // btnServerStart
            // 
            btnServerStart.Font = new Font("Segoe UI", 14F);
            btnServerStart.Location = new Point(290, 227);
            btnServerStart.Name = "btnServerStart";
            btnServerStart.Size = new Size(213, 36);
            btnServerStart.TabIndex = 5;
            btnServerStart.Text = "Start Server";
            btnServerStart.UseVisualStyleBackColor = true;
            btnServerStart.Click += btnServerStart_Click;
            // 
            // lbCnctStRGB
            // 
            lbCnctStRGB.AutoSize = true;
            lbCnctStRGB.Font = new Font("Segoe UI", 16F);
            lbCnctStRGB.ForeColor = Color.Red;
            lbCnctStRGB.Location = new Point(49, 94);
            lbCnctStRGB.Name = "lbCnctStRGB";
            lbCnctStRGB.Size = new Size(143, 30);
            lbCnctStRGB.TabIndex = 4;
            lbCnctStRGB.Text = "Disconnected";
            // 
            // lbRlStRGB
            // 
            lbRlStRGB.AutoSize = true;
            lbRlStRGB.Font = new Font("Segoe UI", 16F);
            lbRlStRGB.ForeColor = Color.Lime;
            lbRlStRGB.Location = new Point(534, 94);
            lbRlStRGB.Name = "lbRlStRGB";
            lbRlStRGB.Size = new Size(42, 30);
            lbRlStRGB.TabIndex = 3;
            lbRlStRGB.Text = "On";
            // 
            // btnPower
            // 
            btnPower.Font = new Font("Segoe UI", 16F);
            btnPower.Location = new Point(290, 269);
            btnPower.Name = "btnPower";
            btnPower.Size = new Size(213, 81);
            btnPower.TabIndex = 2;
            btnPower.Text = "Power";
            btnPower.UseVisualStyleBackColor = true;
            btnPower.Click += btnPower_Click;
            // 
            // lbRlSt
            // 
            lbRlSt.AutoSize = true;
            lbRlSt.Font = new Font("Segoe UI", 16F);
            lbRlSt.Location = new Point(534, 64);
            lbRlSt.Name = "lbRlSt";
            lbRlSt.Size = new Size(132, 30);
            lbRlSt.TabIndex = 1;
            lbRlSt.Text = "Relay Status:";
            // 
            // lbCnctSt
            // 
            lbCnctSt.AutoSize = true;
            lbCnctSt.Font = new Font("Segoe UI", 18F);
            lbCnctSt.Location = new Point(49, 62);
            lbCnctSt.Name = "lbCnctSt";
            lbCnctSt.Size = new Size(213, 32);
            lbCnctSt.TabIndex = 0;
            lbCnctSt.Text = "Connection Status:";
            // 
            // pnlLogin
            // 
            pnlLogin.Controls.Add(lbCred);
            pnlLogin.Controls.Add(BtnLogin);
            pnlLogin.Controls.Add(tbPas);
            pnlLogin.Controls.Add(LGNpassword);
            pnlLogin.Controls.Add(LGNusername);
            pnlLogin.Controls.Add(tbUsr);
            pnlLogin.Dock = DockStyle.Fill;
            pnlLogin.Location = new Point(0, 0);
            pnlLogin.Name = "pnlLogin";
            pnlLogin.Size = new Size(800, 450);
            pnlLogin.TabIndex = 6;
            pnlLogin.Paint += pnlLogin_Paint;
            // 
            // lbCred
            // 
            lbCred.AutoSize = true;
            lbCred.Font = new Font("Segoe UI", 30F);
            lbCred.Location = new Point(302, 48);
            lbCred.Name = "lbCred";
            lbCred.Size = new Size(146, 54);
            lbCred.TabIndex = 5;
            lbCred.Text = "Sign In";
            lbCred.Click += lbCred_Click;
            // 
            // BtnLogin
            // 
            BtnLogin.Font = new Font("Segoe UI", 16F);
            BtnLogin.Location = new Point(290, 292);
            BtnLogin.Name = "BtnLogin";
            BtnLogin.Size = new Size(168, 58);
            BtnLogin.TabIndex = 4;
            BtnLogin.Text = "Log in";
            BtnLogin.UseVisualStyleBackColor = true;
            BtnLogin.Click += BtnLogin_Click;
            // 
            // tbPas
            // 
            tbPas.Location = new Point(260, 215);
            tbPas.Name = "tbPas";
            tbPas.Size = new Size(229, 23);
            tbPas.TabIndex = 3;
            tbPas.UseSystemPasswordChar = true;
            // 
            // LGNpassword
            // 
            LGNpassword.AutoSize = true;
            LGNpassword.Font = new Font("Segoe UI", 12F);
            LGNpassword.Location = new Point(154, 217);
            LGNpassword.Name = "LGNpassword";
            LGNpassword.Size = new Size(76, 21);
            LGNpassword.TabIndex = 2;
            LGNpassword.Text = "Password";
            LGNpassword.Click += LGNpassword_Click;
            // 
            // LGNusername
            // 
            LGNusername.AutoSize = true;
            LGNusername.Font = new Font("Segoe UI", 12F);
            LGNusername.Location = new Point(151, 159);
            LGNusername.Name = "LGNusername";
            LGNusername.Size = new Size(81, 21);
            LGNusername.TabIndex = 1;
            LGNusername.Text = "Username";
            // 
            // tbUsr
            // 
            tbUsr.Location = new Point(260, 157);
            tbUsr.Name = "tbUsr";
            tbUsr.Size = new Size(229, 23);
            tbUsr.TabIndex = 0;
            tbUsr.TextChanged += textBox1_TextChanged;
            // 
            // pnlPort
            // 
            pnlPort.Controls.Add(btnConfigCl);
            pnlPort.Controls.Add(lbDPort);
            pnlPort.Controls.Add(lbConfig);
            pnlPort.Controls.Add(tbPort);
            pnlPort.Controls.Add(lbPort);
            pnlPort.Controls.Add(tbIP);
            pnlPort.Controls.Add(lbIP);
            pnlPort.Controls.Add(btnPortSave);
            pnlPort.Dock = DockStyle.Fill;
            pnlPort.Location = new Point(0, 0);
            pnlPort.Name = "pnlPort";
            pnlPort.Size = new Size(800, 450);
            pnlPort.TabIndex = 10;
            pnlPort.Visible = false;
            // 
            // btnConfigCl
            // 
            btnConfigCl.Location = new Point(312, 377);
            btnConfigCl.Name = "btnConfigCl";
            btnConfigCl.Size = new Size(136, 23);
            btnConfigCl.TabIndex = 7;
            btnConfigCl.Text = "Close";
            btnConfigCl.UseVisualStyleBackColor = true;
            btnConfigCl.Click += btnConfigCl_Click;
            // 
            // lbDPort
            // 
            lbDPort.AutoSize = true;
            lbDPort.Location = new Point(205, 209);
            lbDPort.Name = "lbDPort";
            lbDPort.Size = new Size(100, 15);
            lbDPort.TabIndex = 6;
            lbDPort.Text = "Web UI: 8080 | ESP32 port: 65050";
            // 
            // lbConfig
            // 
            lbConfig.AutoSize = true;
            lbConfig.Font = new Font("Segoe UI", 22F);
            lbConfig.Location = new Point(205, 21);
            lbConfig.Name = "lbConfig";
            lbConfig.Size = new Size(367, 41);
            lbConfig.TabIndex = 5;
            lbConfig.Text = "ESP32 Initial Configuration";
            // 
            // tbPort
            // 
            tbPort.Location = new Point(290, 179);
            tbPort.Name = "tbPort";
            tbPort.Size = new Size(282, 23);
            tbPort.TabIndex = 4;
            tbPort.Text = "65050";
            // 
            // lbPort
            // 
            lbPort.AutoSize = true;
            lbPort.Font = new Font("Segoe UI", 16F);
            lbPort.Location = new Point(205, 172);
            lbPort.Name = "lbPort";
            lbPort.Size = new Size(57, 30);
            lbPort.TabIndex = 3;
            lbPort.Text = "Port:";
            // 
            // tbIP
            // 
            tbIP.Location = new Point(290, 132);
            tbIP.Name = "tbIP";
            tbIP.Size = new Size(286, 23);
            tbIP.TabIndex = 2;
            // 
            // lbIP
            // 
            lbIP.AutoSize = true;
            lbIP.Font = new Font("Segoe UI", 16F);
            lbIP.Location = new Point(154, 123);
            lbIP.Name = "lbIP";
            lbIP.Size = new Size(120, 30);
            lbIP.TabIndex = 1;
            lbIP.Text = "IP Address:";
            lbIP.Click += label1_Click;
            // 
            // btnPortSave
            // 
            btnPortSave.Location = new Point(312, 292);
            btnPortSave.Name = "btnPortSave";
            btnPortSave.Size = new Size(136, 67);
            btnPortSave.TabIndex = 0;
            btnPortSave.Text = "Save";
            btnPortSave.UseVisualStyleBackColor = true;
            btnPortSave.Click += btnPortSave_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(pnlLogin);
            Controls.Add(pnlPort);
            Controls.Add(pnlMain);
            Name = "Form1";
            Text = "Form1";
            Load += Form1_Load;
            pnlMain.ResumeLayout(false);
            pnlMain.PerformLayout();
            pnlLogin.ResumeLayout(false);
            pnlLogin.PerformLayout();
            pnlPort.ResumeLayout(false);
            pnlPort.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Panel pnlMain;
        private Label lbCnctSt;
        private Label lbCnctStRGB;
        private Label lbRlStRGB;
        private Button btnPower;
        private Label lbRlSt;
        private Button btnServerStart;
        private Panel pnlLogin;
        private TextBox tbUsr;
        private Label LGNpassword;
        private Label LGNusername;
        private Button BtnLogin;
        private TextBox tbPas;
        private Label lbCred;
        private Label lbMac;
        private Panel pnlPort;
        private Button btnPortSave;
        private Label lbIP;
        private TextBox tbIP;
        private Label lbConfig;
        private TextBox tbPort;
        private Label lbPort;
        private Label lbDPort;
        private Button btnConfigCl;
    }
}
