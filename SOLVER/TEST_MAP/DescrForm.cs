using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace TEST_MAP
{
    public partial class DescrForm : Form
    {
        public DescrForm(string txt)
        {
            InitializeComponent();
            this.textBox1.Text = txt;
        }

        private void DescrForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            
        }

        private void DescrForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            this.Dispose();
        }

        private void DescrForm_Shown(object sender, EventArgs e)
        {
            textBox1.SelectionLength = 0;
        }
    }
}