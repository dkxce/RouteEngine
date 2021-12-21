namespace RouteGraphBatcher
{
    partial class RGBCForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RGBCForm));
            this.label1 = new System.Windows.Forms.Label();
            this.f1 = new System.Windows.Forms.TextBox();
            this.sf = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.cb = new System.Windows.Forms.ComboBox();
            this.rb = new System.Windows.Forms.Button();
            this.gb = new System.Windows.Forms.GroupBox();
            this.wt = new System.Windows.Forms.Label();
            this.nt = new System.Windows.Forms.Label();
            this.gt = new System.Windows.Forms.Label();
            this.button6 = new System.Windows.Forms.Button();
            this.wd = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.button4 = new System.Windows.Forms.Button();
            this.rd = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.button3 = new System.Windows.Forms.Button();
            this.gd = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.btnsum = new System.Windows.Forms.Button();
            this.cb2 = new System.Windows.Forms.CheckBox();
            this.cb3 = new System.Windows.Forms.CheckBox();
            this.cb1 = new System.Windows.Forms.CheckBox();
            this.cb4 = new System.Windows.Forms.CheckBox();
            this.cb5 = new System.Windows.Forms.CheckBox();
            this.cstreg = new System.Windows.Forms.CheckBox();
            this.customreg = new System.Windows.Forms.NumericUpDown();
            this.arn = new System.Windows.Forms.CheckBox();
            this.btnEx = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.shapesBBox2RegionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mergeShapesMergerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.extractShapesPolygonsExtractorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.gb.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.customreg)).BeginInit();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(23, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(39, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Файл:";
            // 
            // f1
            // 
            this.f1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.f1.Location = new System.Drawing.Point(78, 11);
            this.f1.Name = "f1";
            this.f1.Size = new System.Drawing.Size(642, 20);
            this.f1.TabIndex = 1;
            this.f1.TextChanged += new System.EventHandler(this.f1_TextChanged);
            // 
            // sf
            // 
            this.sf.Location = new System.Drawing.Point(734, 8);
            this.sf.Name = "sf";
            this.sf.Size = new System.Drawing.Size(75, 23);
            this.sf.TabIndex = 2;
            this.sf.Text = "Обзор...";
            this.sf.UseVisualStyleBackColor = true;
            this.sf.Click += new System.EventHandler(this.sf_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(23, 40);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(46, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Регион:";
            // 
            // cb
            // 
            this.cb.DropDownHeight = 250;
            this.cb.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cb.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cb.FormattingEnabled = true;
            this.cb.IntegralHeight = false;
            this.cb.Location = new System.Drawing.Point(78, 37);
            this.cb.Name = "cb";
            this.cb.Size = new System.Drawing.Size(459, 21);
            this.cb.TabIndex = 4;
            this.cb.SelectedIndexChanged += new System.EventHandler(this.cb_SelectedIndexChanged);
            // 
            // rb
            // 
            this.rb.Location = new System.Drawing.Point(734, 40);
            this.rb.Name = "rb";
            this.rb.Size = new System.Drawing.Size(75, 23);
            this.rb.TabIndex = 5;
            this.rb.Text = "Запуск!";
            this.rb.UseVisualStyleBackColor = true;
            this.rb.Click += new System.EventHandler(this.button2_Click);
            // 
            // gb
            // 
            this.gb.Controls.Add(this.wt);
            this.gb.Controls.Add(this.nt);
            this.gb.Controls.Add(this.gt);
            this.gb.Controls.Add(this.button6);
            this.gb.Controls.Add(this.wd);
            this.gb.Controls.Add(this.label6);
            this.gb.Controls.Add(this.button4);
            this.gb.Controls.Add(this.rd);
            this.gb.Controls.Add(this.label4);
            this.gb.Controls.Add(this.button3);
            this.gb.Controls.Add(this.gd);
            this.gb.Controls.Add(this.label3);
            this.gb.Location = new System.Drawing.Point(12, 90);
            this.gb.Name = "gb";
            this.gb.Size = new System.Drawing.Size(814, 156);
            this.gb.TabIndex = 6;
            this.gb.TabStop = false;
            this.gb.Text = "Папки";
            // 
            // wt
            // 
            this.wt.Location = new System.Drawing.Point(66, 131);
            this.wt.Name = "wt";
            this.wt.Size = new System.Drawing.Size(642, 21);
            this.wt.TabIndex = 16;
            this.wt.Text = "Graphs:";
            // 
            // nt
            // 
            this.nt.Location = new System.Drawing.Point(66, 91);
            this.nt.Name = "nt";
            this.nt.Size = new System.Drawing.Size(642, 14);
            this.nt.TabIndex = 15;
            this.nt.Text = "Graphs:";
            // 
            // gt
            // 
            this.gt.Location = new System.Drawing.Point(66, 50);
            this.gt.Name = "gt";
            this.gt.Size = new System.Drawing.Size(642, 14);
            this.gt.TabIndex = 13;
            this.gt.Text = "Graphs:";
            // 
            // button6
            // 
            this.button6.Location = new System.Drawing.Point(722, 105);
            this.button6.Name = "button6";
            this.button6.Size = new System.Drawing.Size(75, 23);
            this.button6.TabIndex = 12;
            this.button6.Text = "Обзор...";
            this.button6.UseVisualStyleBackColor = true;
            this.button6.Click += new System.EventHandler(this.button6_Click);
            // 
            // wd
            // 
            this.wd.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.wd.Location = new System.Drawing.Point(66, 108);
            this.wd.Name = "wd";
            this.wd.Size = new System.Drawing.Size(642, 20);
            this.wd.TabIndex = 11;
            this.wd.TextChanged += new System.EventHandler(this.gd_TextChanged);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(11, 110);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(53, 13);
            this.label6.TabIndex = 10;
            this.label6.Text = "RGWays:";
            // 
            // button4
            // 
            this.button4.Location = new System.Drawing.Point(722, 64);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(75, 23);
            this.button4.TabIndex = 6;
            this.button4.Text = "Обзор...";
            this.button4.UseVisualStyleBackColor = true;
            this.button4.Click += new System.EventHandler(this.button4_Click);
            // 
            // rd
            // 
            this.rd.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.rd.Location = new System.Drawing.Point(66, 67);
            this.rd.Name = "rd";
            this.rd.Size = new System.Drawing.Size(642, 20);
            this.rd.TabIndex = 5;
            this.rd.TextChanged += new System.EventHandler(this.gd_TextChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(11, 69);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(49, 13);
            this.label4.TabIndex = 4;
            this.label4.Text = "Regions:";
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(722, 24);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(75, 23);
            this.button3.TabIndex = 3;
            this.button3.Text = "Обзор...";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // gd
            // 
            this.gd.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.gd.Location = new System.Drawing.Point(66, 27);
            this.gd.Name = "gd";
            this.gd.Size = new System.Drawing.Size(642, 20);
            this.gd.TabIndex = 2;
            this.gd.TextChanged += new System.EventHandler(this.gd_TextChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(11, 29);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(44, 13);
            this.label3.TabIndex = 0;
            this.label3.Text = "Graphs:";
            // 
            // textBox1
            // 
            this.textBox1.BackColor = System.Drawing.SystemColors.Window;
            this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox1.Location = new System.Drawing.Point(12, 287);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.textBox1.Size = new System.Drawing.Size(814, 317);
            this.textBox1.TabIndex = 7;
            this.textBox1.Text = "Status...";
            // 
            // btnsum
            // 
            this.btnsum.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnsum.Location = new System.Drawing.Point(12, 255);
            this.btnsum.Name = "btnsum";
            this.btnsum.Size = new System.Drawing.Size(710, 23);
            this.btnsum.TabIndex = 9;
            this.btnsum.Text = "Склеить маршруты для всех регионов";
            this.btnsum.UseVisualStyleBackColor = true;
            this.btnsum.Click += new System.EventHandler(this.btnsum_Click);
            // 
            // cb2
            // 
            this.cb2.AutoSize = true;
            this.cb2.Checked = true;
            this.cb2.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cb2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cb2.Location = new System.Drawing.Point(543, 45);
            this.cb2.Name = "cb2";
            this.cb2.Size = new System.Drawing.Size(179, 17);
            this.cb2.TabIndex = 10;
            this.cb2.Text = "Расчет транзитных маршрутов";
            this.cb2.UseVisualStyleBackColor = true;
            this.cb2.CheckedChanged += new System.EventHandler(this.cb2_CheckedChanged);
            // 
            // cb3
            // 
            this.cb3.AutoSize = true;
            this.cb3.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cb3.Location = new System.Drawing.Point(543, 59);
            this.cb3.Name = "cb3";
            this.cb3.Size = new System.Drawing.Size(174, 17);
            this.cb3.TabIndex = 12;
            this.cb3.Text = "Создание карты транзита MP";
            this.cb3.UseVisualStyleBackColor = true;
            // 
            // cb1
            // 
            this.cb1.AutoSize = true;
            this.cb1.Checked = true;
            this.cb1.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cb1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cb1.Location = new System.Drawing.Point(543, 31);
            this.cb1.Name = "cb1";
            this.cb1.Size = new System.Drawing.Size(118, 17);
            this.cb1.TabIndex = 13;
            this.cb1.Text = "Построение графа";
            this.cb1.UseVisualStyleBackColor = true;
            this.cb1.CheckedChanged += new System.EventHandler(this.cb1_CheckedChanged);
            // 
            // cb4
            // 
            this.cb4.AutoSize = true;
            this.cb4.Checked = true;
            this.cb4.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cb4.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cb4.Location = new System.Drawing.Point(543, 73);
            this.cb4.Name = "cb4";
            this.cb4.Size = new System.Drawing.Size(176, 17);
            this.cb4.TabIndex = 14;
            this.cb4.Text = "Считать статистику атрибутов";
            this.cb4.UseVisualStyleBackColor = true;
            // 
            // cb5
            // 
            this.cb5.AutoSize = true;
            this.cb5.Checked = true;
            this.cb5.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cb5.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cb5.Location = new System.Drawing.Point(385, 73);
            this.cb5.Name = "cb5";
            this.cb5.Size = new System.Drawing.Size(94, 17);
            this.cb5.TabIndex = 15;
            this.cb5.Text = "Backup графа";
            this.cb5.UseVisualStyleBackColor = true;
            // 
            // cstreg
            // 
            this.cstreg.AutoSize = true;
            this.cstreg.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cstreg.Location = new System.Drawing.Point(78, 65);
            this.cstreg.Name = "cstreg";
            this.cstreg.Size = new System.Drawing.Size(102, 17);
            this.cstreg.TabIndex = 16;
            this.cstreg.Text = "Произвольный:";
            this.cstreg.UseVisualStyleBackColor = true;
            this.cstreg.CheckedChanged += new System.EventHandler(this.cstreg_CheckedChanged);
            // 
            // customreg
            // 
            this.customreg.Enabled = false;
            this.customreg.Location = new System.Drawing.Point(183, 63);
            this.customreg.Maximum = new decimal(new int[] {
            999,
            0,
            0,
            0});
            this.customreg.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.customreg.Name = "customreg";
            this.customreg.Size = new System.Drawing.Size(60, 20);
            this.customreg.TabIndex = 18;
            this.customreg.UpDownAlign = System.Windows.Forms.LeftRightAlignment.Left;
            this.customreg.Value = new decimal(new int[] {
            700,
            0,
            0,
            0});
            // 
            // arn
            // 
            this.arn.AutoSize = true;
            this.arn.Checked = true;
            this.arn.CheckState = System.Windows.Forms.CheckState.Checked;
            this.arn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.arn.Location = new System.Drawing.Point(385, 59);
            this.arn.Name = "arn";
            this.arn.Size = new System.Drawing.Size(152, 17);
            this.arn.TabIndex = 19;
            this.arn.Text = "Спрашивать имя региона";
            this.arn.UseVisualStyleBackColor = true;
            // 
            // btnEx
            // 
            this.btnEx.Location = new System.Drawing.Point(249, 64);
            this.btnEx.Name = "btnEx";
            this.btnEx.Size = new System.Drawing.Size(130, 23);
            this.btnEx.TabIndex = 20;
            this.btnEx.Text = "Извлечь границы";
            this.btnEx.UseVisualStyleBackColor = true;
            this.btnEx.Visible = false;
            this.btnEx.Click += new System.EventHandler(this.btnEx_Click);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(734, 255);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 21;
            this.button1.Text = "Tools ...";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.shapesBBox2RegionsToolStripMenuItem,
            this.mergeShapesMergerToolStripMenuItem,
            this.extractShapesPolygonsExtractorToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(384, 70);
            // 
            // shapesBBox2RegionsToolStripMenuItem
            // 
            this.shapesBBox2RegionsToolStripMenuItem.Name = "shapesBBox2RegionsToolStripMenuItem";
            this.shapesBBox2RegionsToolStripMenuItem.Size = new System.Drawing.Size(383, 22);
            this.shapesBBox2RegionsToolStripMenuItem.Text = "Создать файл Регионов (ShapesBBox2Regions) ...";
            this.shapesBBox2RegionsToolStripMenuItem.Click += new System.EventHandler(this.shapesBBox2RegionsToolStripMenuItem_Click);
            // 
            // mergeShapesMergerToolStripMenuItem
            // 
            this.mergeShapesMergerToolStripMenuItem.Name = "mergeShapesMergerToolStripMenuItem";
            this.mergeShapesMergerToolStripMenuItem.Size = new System.Drawing.Size(383, 22);
            this.mergeShapesMergerToolStripMenuItem.Text = "Склеить шейпы (ShapesMerger) ...";
            this.mergeShapesMergerToolStripMenuItem.Click += new System.EventHandler(this.mergeShapesMergerToolStripMenuItem_Click);
            // 
            // extractShapesPolygonsExtractorToolStripMenuItem
            // 
            this.extractShapesPolygonsExtractorToolStripMenuItem.Name = "extractShapesPolygonsExtractorToolStripMenuItem";
            this.extractShapesPolygonsExtractorToolStripMenuItem.Size = new System.Drawing.Size(383, 22);
            this.extractShapesPolygonsExtractorToolStripMenuItem.Text = "Извлечь полигоны из шейпов (ShapesPolygonsExtractor) ...";
            this.extractShapesPolygonsExtractorToolStripMenuItem.Click += new System.EventHandler(this.extractShapesPolygonsExtractorToolStripMenuItem_Click);
            // 
            // RGBCForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(838, 616);
            this.Controls.Add(this.btnEx);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.arn);
            this.Controls.Add(this.customreg);
            this.Controls.Add(this.cstreg);
            this.Controls.Add(this.gb);
            this.Controls.Add(this.cb5);
            this.Controls.Add(this.cb4);
            this.Controls.Add(this.cb1);
            this.Controls.Add(this.cb3);
            this.Controls.Add(this.cb2);
            this.Controls.Add(this.btnsum);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.rb);
            this.Controls.Add(this.cb);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.sf);
            this.Controls.Add(this.f1);
            this.Controls.Add(this.label1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "RGBCForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "RouteGraph Batch Creator";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.gb.ResumeLayout(false);
            this.gb.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.customreg)).EndInit();
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox f1;
        private System.Windows.Forms.Button sf;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox cb;
        private System.Windows.Forms.Button rb;
        private System.Windows.Forms.GroupBox gb;
        private System.Windows.Forms.Button button6;
        private System.Windows.Forms.TextBox wd;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button button4;
        private System.Windows.Forms.TextBox rd;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.TextBox gd;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label wt;
        private System.Windows.Forms.Label nt;
        private System.Windows.Forms.Label gt;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Button btnsum;
        private System.Windows.Forms.CheckBox cb2;
        private System.Windows.Forms.CheckBox cb3;
        private System.Windows.Forms.CheckBox cb1;
        private System.Windows.Forms.CheckBox cb4;
        private System.Windows.Forms.CheckBox cb5;
        private System.Windows.Forms.CheckBox cstreg;
        private System.Windows.Forms.NumericUpDown customreg;
        private System.Windows.Forms.CheckBox arn;
        private System.Windows.Forms.Button btnEx;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem shapesBBox2RegionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem mergeShapesMergerToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem extractShapesPolygonsExtractorToolStripMenuItem;
    }
}

