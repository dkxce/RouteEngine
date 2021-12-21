namespace ShapesBBox2Regions
{
    partial class SBB2RForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SBB2RForm));
            this.panel1 = new System.Windows.Forms.Panel();
            this.kmBox = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.saveBtn = new System.Windows.Forms.Button();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addFilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem4 = new System.Windows.Forms.ToolStripSeparator();
            this.addFilesFromDirectoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addRtFromDirectoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addShapeFilesFromDirectoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addXMLFromDirectoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripSeparator();
            this.sortByIDToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sortByNameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem5 = new System.Windows.Forms.ToolStripSeparator();
            this.changeRegionIDToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.changeRegionNameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem3 = new System.Windows.Forms.ToolStripSeparator();
            this.deleteCurrentFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.clearListToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.panel2 = new System.Windows.Forms.Panel();
            this.label3 = new System.Windows.Forms.Label();
            this.panel3 = new System.Windows.Forms.Panel();
            this.llast = new System.Windows.Forms.Label();
            this.lttl = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.nidBox = new System.Windows.Forms.NumericUpDown();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.kmBox)).BeginInit();
            this.contextMenuStrip1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nidBox)).BeginInit();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.nidBox);
            this.panel1.Controls.Add(this.label4);
            this.panel1.Controls.Add(this.kmBox);
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.saveBtn);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel1.Location = new System.Drawing.Point(0, 449);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(792, 24);
            this.panel1.TabIndex = 4;
            // 
            // kmBox
            // 
            this.kmBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.kmBox.Location = new System.Drawing.Point(141, 0);
            this.kmBox.Maximum = new decimal(new int[] {
            999,
            0,
            0,
            0});
            this.kmBox.Name = "kmBox";
            this.kmBox.Size = new System.Drawing.Size(65, 20);
            this.kmBox.TabIndex = 4;
            this.kmBox.Value = new decimal(new int[] {
            5,
            0,
            0,
            0});
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(55, 3);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(172, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Enlarge BBox to:                        km";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 3);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(46, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Options:";
            // 
            // saveBtn
            // 
            this.saveBtn.Dock = System.Windows.Forms.DockStyle.Right;
            this.saveBtn.Enabled = false;
            this.saveBtn.Location = new System.Drawing.Point(717, 0);
            this.saveBtn.Name = "saveBtn";
            this.saveBtn.Size = new System.Drawing.Size(75, 24);
            this.saveBtn.TabIndex = 0;
            this.saveBtn.Text = "Save ...";
            this.saveBtn.UseVisualStyleBackColor = true;
            this.saveBtn.Click += new System.EventHandler(this.saveBtn_Click);
            // 
            // listBox1
            // 
            this.listBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.listBox1.ContextMenuStrip = this.contextMenuStrip1;
            this.listBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.listBox1.FormattingEnabled = true;
            this.listBox1.ItemHeight = 16;
            this.listBox1.Location = new System.Drawing.Point(0, 33);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(792, 402);
            this.listBox1.TabIndex = 5;
            this.listBox1.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.listBox1_DrawItem);
            this.listBox1.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.listBox1_KeyPress);
            this.listBox1.KeyDown += new System.Windows.Forms.KeyEventHandler(this.listBox1_KeyDown);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addFilesToolStripMenuItem,
            this.toolStripMenuItem4,
            this.addFilesFromDirectoryToolStripMenuItem,
            this.addRtFromDirectoryToolStripMenuItem,
            this.addShapeFilesFromDirectoryToolStripMenuItem,
            this.addXMLFromDirectoryToolStripMenuItem,
            this.toolStripMenuItem2,
            this.sortByIDToolStripMenuItem,
            this.sortByNameToolStripMenuItem,
            this.toolStripMenuItem5,
            this.changeRegionIDToolStripMenuItem,
            this.changeRegionNameToolStripMenuItem,
            this.toolStripMenuItem3,
            this.deleteCurrentFileToolStripMenuItem,
            this.toolStripMenuItem1,
            this.clearListToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(247, 276);
            this.contextMenuStrip1.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuStrip1_Opening);
            // 
            // addFilesToolStripMenuItem
            // 
            this.addFilesToolStripMenuItem.Name = "addFilesToolStripMenuItem";
            this.addFilesToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.addFilesToolStripMenuItem.Text = "Add files (Ins) ...";
            this.addFilesToolStripMenuItem.Click += new System.EventHandler(this.addFilesToolStripMenuItem_Click);
            // 
            // toolStripMenuItem4
            // 
            this.toolStripMenuItem4.Name = "toolStripMenuItem4";
            this.toolStripMenuItem4.Size = new System.Drawing.Size(243, 6);
            // 
            // addFilesFromDirectoryToolStripMenuItem
            // 
            this.addFilesFromDirectoryToolStripMenuItem.Name = "addFilesFromDirectoryToolStripMenuItem";
            this.addFilesFromDirectoryToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.addFilesFromDirectoryToolStripMenuItem.Text = "Add files from Directory ...";
            this.addFilesFromDirectoryToolStripMenuItem.Click += new System.EventHandler(this.addFilesFromDirectoryToolStripMenuItem_Click);
            // 
            // addRtFromDirectoryToolStripMenuItem
            // 
            this.addRtFromDirectoryToolStripMenuItem.Name = "addRtFromDirectoryToolStripMenuItem";
            this.addRtFromDirectoryToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.addRtFromDirectoryToolStripMenuItem.Text = "Add Rt from Directory ...";
            this.addRtFromDirectoryToolStripMenuItem.Click += new System.EventHandler(this.addRtFromDirectoryToolStripMenuItem_Click);
            // 
            // addShapeFilesFromDirectoryToolStripMenuItem
            // 
            this.addShapeFilesFromDirectoryToolStripMenuItem.Name = "addShapeFilesFromDirectoryToolStripMenuItem";
            this.addShapeFilesFromDirectoryToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.addShapeFilesFromDirectoryToolStripMenuItem.Text = "Add Shape files from Directory ...";
            this.addShapeFilesFromDirectoryToolStripMenuItem.Click += new System.EventHandler(this.addShapeFilesFromDirectoryToolStripMenuItem_Click);
            // 
            // addXMLFromDirectoryToolStripMenuItem
            // 
            this.addXMLFromDirectoryToolStripMenuItem.Name = "addXMLFromDirectoryToolStripMenuItem";
            this.addXMLFromDirectoryToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.addXMLFromDirectoryToolStripMenuItem.Text = "Add XML from Directory ...";
            this.addXMLFromDirectoryToolStripMenuItem.Click += new System.EventHandler(this.addXMLFromDirectoryToolStripMenuItem_Click);
            // 
            // toolStripMenuItem2
            // 
            this.toolStripMenuItem2.Name = "toolStripMenuItem2";
            this.toolStripMenuItem2.Size = new System.Drawing.Size(243, 6);
            // 
            // sortByIDToolStripMenuItem
            // 
            this.sortByIDToolStripMenuItem.Name = "sortByIDToolStripMenuItem";
            this.sortByIDToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.sortByIDToolStripMenuItem.Text = "Sort By ID";
            this.sortByIDToolStripMenuItem.Click += new System.EventHandler(this.sortByIDToolStripMenuItem_Click);
            // 
            // sortByNameToolStripMenuItem
            // 
            this.sortByNameToolStripMenuItem.Name = "sortByNameToolStripMenuItem";
            this.sortByNameToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.sortByNameToolStripMenuItem.Text = "Sort By Name";
            this.sortByNameToolStripMenuItem.Click += new System.EventHandler(this.sortByNameToolStripMenuItem_Click);
            // 
            // toolStripMenuItem5
            // 
            this.toolStripMenuItem5.Name = "toolStripMenuItem5";
            this.toolStripMenuItem5.Size = new System.Drawing.Size(243, 6);
            // 
            // changeRegionIDToolStripMenuItem
            // 
            this.changeRegionIDToolStripMenuItem.Name = "changeRegionIDToolStripMenuItem";
            this.changeRegionIDToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.changeRegionIDToolStripMenuItem.Text = "Change Region ID (Space) ...";
            this.changeRegionIDToolStripMenuItem.Click += new System.EventHandler(this.changeRegionIDToolStripMenuItem_Click);
            // 
            // changeRegionNameToolStripMenuItem
            // 
            this.changeRegionNameToolStripMenuItem.Name = "changeRegionNameToolStripMenuItem";
            this.changeRegionNameToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.changeRegionNameToolStripMenuItem.Text = "Change Region Name (F2) ...";
            this.changeRegionNameToolStripMenuItem.Click += new System.EventHandler(this.changeRegionNameToolStripMenuItem_Click);
            // 
            // toolStripMenuItem3
            // 
            this.toolStripMenuItem3.Name = "toolStripMenuItem3";
            this.toolStripMenuItem3.Size = new System.Drawing.Size(243, 6);
            // 
            // deleteCurrentFileToolStripMenuItem
            // 
            this.deleteCurrentFileToolStripMenuItem.Enabled = false;
            this.deleteCurrentFileToolStripMenuItem.Name = "deleteCurrentFileToolStripMenuItem";
            this.deleteCurrentFileToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.deleteCurrentFileToolStripMenuItem.Text = "Delete current file (Del)";
            this.deleteCurrentFileToolStripMenuItem.Click += new System.EventHandler(this.deleteCurrentFileToolStripMenuItem_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(243, 6);
            // 
            // clearListToolStripMenuItem
            // 
            this.clearListToolStripMenuItem.Enabled = false;
            this.clearListToolStripMenuItem.Name = "clearListToolStripMenuItem";
            this.clearListToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.clearListToolStripMenuItem.Text = "Clear list";
            this.clearListToolStripMenuItem.Click += new System.EventHandler(this.clearListToolStripMenuItem_Click);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.label3);
            this.panel2.Controls.Add(this.panel3);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel2.Location = new System.Drawing.Point(0, 0);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(792, 33);
            this.panel2.TabIndex = 7;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(3, 9);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(287, 13);
            this.label3.TabIndex = 1;
            this.label3.Text = "User drag & drop or context menu to add file(s) - .shp, .xml, .rt";
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.llast);
            this.panel3.Controls.Add(this.lttl);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Right;
            this.panel3.Location = new System.Drawing.Point(592, 0);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(200, 33);
            this.panel3.TabIndex = 0;
            // 
            // llast
            // 
            this.llast.AutoSize = true;
            this.llast.Location = new System.Drawing.Point(3, 17);
            this.llast.Name = "llast";
            this.llast.Size = new System.Drawing.Size(60, 13);
            this.llast.TabIndex = 1;
            this.llast.Text = "Status: Idle";
            // 
            // lttl
            // 
            this.lttl.AutoSize = true;
            this.lttl.Location = new System.Drawing.Point(3, 4);
            this.lttl.Name = "lttl";
            this.lttl.Size = new System.Drawing.Size(75, 13);
            this.lttl.TabIndex = 0;
            this.lttl.Text = "Total Boxes: 0";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(235, 3);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(114, 13);
            this.label4.TabIndex = 5;
            this.label4.Text = "|    Number of ID digits:";
            // 
            // nidBox
            // 
            this.nidBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.nidBox.Location = new System.Drawing.Point(355, 1);
            this.nidBox.Maximum = new decimal(new int[] {
            6,
            0,
            0,
            0});
            this.nidBox.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.nidBox.Name = "nidBox";
            this.nidBox.Size = new System.Drawing.Size(32, 20);
            this.nidBox.TabIndex = 6;
            this.nidBox.Value = new decimal(new int[] {
            3,
            0,
            0,
            0});
            // 
            // SBB2RForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(792, 473);
            this.Controls.Add(this.listBox1);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "SBB2RForm";
            this.Text = "Shapes BBox to Regions Converter";
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.kmBox)).EndInit();
            this.contextMenuStrip1.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nidBox)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.NumericUpDown kmBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button saveBtn;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem addFilesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteCurrentFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem clearListToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addFilesFromDirectoryToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addRtFromDirectoryToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addShapeFilesFromDirectoryToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addXMLFromDirectoryToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem2;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Label llast;
        private System.Windows.Forms.Label lttl;
        private System.Windows.Forms.ToolStripMenuItem changeRegionIDToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem changeRegionNameToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem3;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem4;
        private System.Windows.Forms.ToolStripMenuItem sortByIDToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sortByNameToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem5;
        private System.Windows.Forms.NumericUpDown nidBox;
        private System.Windows.Forms.Label label4;
    }
}

