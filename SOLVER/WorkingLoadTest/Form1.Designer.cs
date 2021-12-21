namespace WorkingLoadTest
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.inServer = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.inMethod = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.inKey = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.inConsts = new System.Windows.Forms.TextBox();
            this.button1 = new System.Windows.Forms.Button();
            this.inAcc = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.inVars = new System.Windows.Forms.ComboBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.label6 = new System.Windows.Forms.Label();
            this.txtLR = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // inServer
            // 
            this.inServer.DropDownHeight = 200;
            this.inServer.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.inServer.FormattingEnabled = true;
            this.inServer.IntegralHeight = false;
            this.inServer.Items.AddRange(new object[] {
            "http://127.0.0.1:8080/NMS/",
            "tcp://127.0.0.1/",
            "tcpxml://127.0.0.1/",
            "dcall://127.0.0.1/",
            "remoting://127.0.0.1/"});
            this.inServer.Location = new System.Drawing.Point(87, 6);
            this.inServer.Name = "inServer";
            this.inServer.Size = new System.Drawing.Size(408, 21);
            this.inServer.TabIndex = 0;
            this.inServer.Text = "http://127.0.0.1:8080/NMS/";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(8, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(47, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Сервер:";
            // 
            // inMethod
            // 
            this.inMethod.DropDownHeight = 140;
            this.inMethod.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.inMethod.FormattingEnabled = true;
            this.inMethod.IntegralHeight = false;
            this.inMethod.Items.AddRange(new object[] {
            "sroute.ashx?k={k}&f={f}&x={xx}&y={yy}&i={i}&p={p}",
            "snearroad.ashx?k={k}&f={f}&n={n}&x={xx}&y={yy}"});
            this.inMethod.Location = new System.Drawing.Point(87, 33);
            this.inMethod.Name = "inMethod";
            this.inMethod.Size = new System.Drawing.Size(408, 21);
            this.inMethod.TabIndex = 2;
            this.inMethod.Text = "sroute.ashx?k={k}&f={f}&x={xx}&y={yy}&i={i}&p={p}";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(8, 36);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(42, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Метод:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(8, 63);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(36, 13);
            this.label3.TabIndex = 4;
            this.label3.Text = "Ключ:";
            // 
            // inKey
            // 
            this.inKey.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.inKey.FormattingEnabled = true;
            this.inKey.Items.AddRange(new object[] {
            "TEST"});
            this.inKey.Location = new System.Drawing.Point(87, 61);
            this.inKey.Name = "inKey";
            this.inKey.Size = new System.Drawing.Size(408, 21);
            this.inKey.TabIndex = 5;
            this.inKey.Text = "TEST";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(8, 90);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(73, 13);
            this.label4.TabIndex = 6;
            this.label4.Text = "Постоянные:";
            // 
            // inConsts
            // 
            this.inConsts.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.inConsts.Location = new System.Drawing.Point(87, 87);
            this.inConsts.Multiline = true;
            this.inConsts.Name = "inConsts";
            this.inConsts.Size = new System.Drawing.Size(408, 105);
            this.inConsts.TabIndex = 7;
            this.inConsts.Text = "vars.Add(\"f\",\"json\");\r\nvars.Add(\"i\",\"0\");\r\nvars.Add(\"p\",\"1\");\r\nvars.Add(\"n\",\"0\");" +
                "\r\nvars.Add(\"wts\",\"hs\");";
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(420, 222);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 8;
            this.button1.Text = "RUN";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // inAcc
            // 
            this.inAcc.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.inAcc.Location = new System.Drawing.Point(11, 264);
            this.inAcc.Multiline = true;
            this.inAcc.Name = "inAcc";
            this.inAcc.ReadOnly = true;
            this.inAcc.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.inAcc.Size = new System.Drawing.Size(486, 232);
            this.inAcc.TabIndex = 9;
            this.inAcc.WordWrap = false;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(9, 198);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(76, 13);
            this.label5.TabIndex = 10;
            this.label5.Text = "Переменные:";
            // 
            // inVars
            // 
            this.inVars.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.inVars.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.inVars.FormattingEnabled = true;
            this.inVars.Items.AddRange(new object[] {
            "Координаты"});
            this.inVars.Location = new System.Drawing.Point(87, 195);
            this.inVars.Name = "inVars";
            this.inVars.Size = new System.Drawing.Size(408, 21);
            this.inVars.TabIndex = 11;
            // 
            // timer1
            // 
            this.timer1.Interval = 1000;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(12, 248);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(44, 13);
            this.label6.TabIndex = 12;
            this.label6.Text = "Статус:";
            // 
            // txtLR
            // 
            this.txtLR.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtLR.Location = new System.Drawing.Point(503, 33);
            this.txtLR.Multiline = true;
            this.txtLR.Name = "txtLR";
            this.txtLR.ReadOnly = true;
            this.txtLR.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtLR.Size = new System.Drawing.Size(448, 463);
            this.txtLR.TabIndex = 13;
            this.txtLR.WordWrap = false;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(501, 9);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(81, 13);
            this.label7.TabIndex = 14;
            this.label7.Text = "Last Response:";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(963, 508);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.txtLR);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.inVars);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.inAcc);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.inConsts);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.inKey);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.inMethod);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.inServer);
            this.Name = "Form1";
            this.Text = "Тестирование маршрутного движка";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox inServer;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox inMethod;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox inKey;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox inConsts;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.TextBox inAcc;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox inVars;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox txtLR;
        private System.Windows.Forms.Label label7;
    }
}

