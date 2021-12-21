using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace WA_TEST_001
{
    public partial class Form1 : Form
    {
        private List<PointF> points = new List<PointF>();
        private double[] box = new double[4] { double.MaxValue, double.MaxValue, double.MinValue, double.MinValue };

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_MouseClick(object sender, MouseEventArgs e)
        {
            points.Add(new PointF(e.X, e.Y));            
            ReDraw();
        }

        private void button1_Click(object sender, EventArgs e)
        {
           points.Clear();
            ReDraw();
        }

        private void ReDraw()
        {
            Graphics g = Graphics.FromHwnd(this.Handle);
            g.FillRectangle(new SolidBrush(Color.White), this.ClientRectangle);
            box = new double[4] { double.MaxValue, double.MaxValue, double.MinValue, double.MinValue };
            foreach (PointF p in points)
            {
                if (p.X < box[0]) box[0] = p.X;
                if (p.Y < box[1]) box[1] = p.Y;
                if (p.X > box[2]) box[2] = p.X;
                if (p.Y > box[3]) box[3] = p.Y;
                g.DrawEllipse(new Pen(new SolidBrush(Color.Black)), new Rectangle((int)p.X - 2, (int)p.Y - 2, 4, 4));
            };
            PointF[] line = dkxce.Utils.CloudBorder.GetCloudBorder(box, points.ToArray(), (double)numericUpDown1.Value, (double)numericUpDown2.Value);
            PointF[] enll = dkxce.Utils.CloudBorder.EnlargePolygon(line, (float)numericUpDown3.Value);
            if (line != null)
            {
                int mdl = line.Length / 2;
                Pen pen1 = new Pen(new SolidBrush(Color.Red), 2);
                Pen pen2 = new Pen(new SolidBrush(Color.Blue), 2);
                for (int i = 1; i < line.Length; i++)
                {
                    Point p1 = new Point((int)line[i - 1].X, (int)line[i - 1].Y);
                    Point p2 = new Point((int)line[i].X, (int)line[i].Y);
                    g.DrawLine(i < mdl ? pen1 : pen2, p1, p2);
                };
                g.DrawLine(pen2, new Point((int)line[line.Length - 1].X, (int)line[line.Length - 1].Y), new Point((int)line[0].X, (int)line[0].Y));
            };
            if ((enll != null) && (enll.Length != 0))
            {
                for (int i = 1; i < enll.Length; i++)
                {
                    Point p1 = new Point((int)enll[i - 1].X, (int)enll[i - 1].Y);
                    Point p2 = new Point((int)enll[i].X, (int)enll[i].Y);
                    g.DrawLine(new Pen(new SolidBrush(Color.Green), 2), p1, p2);
                };
                g.DrawLine(new Pen(new SolidBrush(Color.Green), 2), new Point((int)enll[enll.Length - 1].X, (int)enll[enll.Length - 1].Y), new Point((int)enll[0].X, (int)enll[0].Y));
            };
            g.Dispose();
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            ReDraw();
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            ReDraw();
        }

        private void numericUpDown3_ValueChanged(object sender, EventArgs e)
        {
            ReDraw();
        }
    }

    
}