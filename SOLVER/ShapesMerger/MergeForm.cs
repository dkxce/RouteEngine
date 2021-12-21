using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace ShapesMerger
{
    public partial class MergeForm : Form
    {
        public int ShapeTypes = 0;

        public MergeForm()
        {
            InitializeComponent();
            this.AllowDrop = true;
            this.DragEnter += new DragEventHandler(Form1_DragEnter);
            this.DragDrop += new DragEventHandler(Form1_DragDrop);
        }

        private void Form1_DragEnter(object sender, DragEventArgs e) 
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void Form1_DragDrop(object sender, DragEventArgs e) 
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files)
            {
                if (Path.GetExtension(file).ToLower() != ".shp") continue;
                FileInfo fi = new FileInfo(file);
                ShapeInfo shi = new ShapeInfo(fi);
                listBox1.Items.Add(shi);
            };
            CheckEnabled();
        }

        private void CheckEnabled()
        {            
            ShapeTypes = 0;
            List<int> shtps = new List<int>();
            foreach (object obj in listBox1.Items)
            {
                ShapeInfo shi = (ShapeInfo)obj;
                if (!shtps.Contains(shi.shpType)) shtps.Add(shi.shpType);
                ShapeTypes = shi.shpType;
                
            };
            if (shtps.Count > 1) ShapeTypes = -1;
            string st = "Unknown";
            if (ShapeTypes == -1) st = "Multi";
            if (ShapeTypes == 1) st = "Point";
            if (ShapeTypes == 3) st = "PolyLine";
            if (ShapeTypes == 5) st = "Polygon";
            label2.Text = String.Format("Shape Types: {0}", st);

            button1.Enabled = (listBox1.Items.Count > 1) && ((ShapeTypes == 1) || (ShapeTypes == 3) || (ShapeTypes == 5));
        }
        
        private void listBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            
        }

        private void listBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.KeyCode == Keys.Delete) && (listBox1.SelectedIndex >= 0))
                listBox1.Items.RemoveAt(listBox1.SelectedIndex);

            if ((e.Modifiers == Keys.Alt) && (listBox1.Items.Count > 1) && (listBox1.SelectedIndex >= 0))
            {
                int si = listBox1.SelectedIndex;
                if ((e.KeyCode == Keys.Up) && (si > 0))
                {
                    object obj = listBox1.Items[si];
                    listBox1.Items.RemoveAt(si);
                    listBox1.Items.Insert(--si, obj);
                    listBox1.SelectedIndex = si;
                };
                if ((e.KeyCode == Keys.Down) && (si < (listBox1.Items.Count - 1)))
                {
                    object obj = listBox1.Items[si];
                    listBox1.Items.RemoveAt(si);
                    listBox1.Items.Insert(++si, obj);
                    listBox1.SelectedIndex = si;
                };
            };

            if((e.KeyCode == Keys.Insert) || ((e.Modifiers == Keys.Control) && (e.KeyCode == Keys.O)))
            {
                OpenFD();
                return;
            };

            CheckEnabled();
        }

        private void OpenFD()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.DefaultExt = ".shp";
            ofd.Filter = "Shape Files (*.shp)|*.shp";
            ofd.Title = "Add files ...";
            ofd.Multiselect = true;
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                foreach (string file in ofd.FileNames)
                {
                    if (Path.GetExtension(file).ToLower() != ".shp") continue;
                    FileInfo fi = new FileInfo(file);
                    ShapeInfo shi = new ShapeInfo(fi);
                    listBox1.Items.Add(shi);
                };
                CheckEnabled();
            };
            ofd.Dispose();                
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (listBox1.Items.Count < 2) return;

            string resFile = null;
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.DefaultExt = ".shp";
            sfd.Filter = "Shape Files (*.shp)|*.shp";
            sfd.Title = "Save as ...";
            if (sfd.ShowDialog() == DialogResult.OK)
                resFile = sfd.FileName;
            sfd.Dispose();
            if (String.IsNullOrEmpty(resFile)) return;

            long ttlLength = 0;
            List<string> files = new List<string>();
            foreach (object obj in listBox1.Items)
            {
                ShapeInfo shi = (ShapeInfo)obj;
                ttlLength += shi.fileInfo.Length;
                files.Add(shi.fileInfo.FullName);
            };

            MergeShapes(resFile, files.ToArray(), ttlLength, wdbf.Checked, mbox.Checked);
        }

        private KMZRebuilder.WaitingBoxForm wbf = null;
        private void MergeShapes(string resFile, string[] files, long ttlLength, bool wDBF, bool mDBF)
        {
            if (files == null) return;
            if (files.Length < 2) return;

            wbf = new KMZRebuilder.WaitingBoxForm("Merging...", "Prepare", this);
            wbf.Show();

            DBFSharp.DBFFile dbff = null;
            if (wDBF)
            {
                string fd = resFile.Replace(Path.GetExtension(resFile), ".dbf");
                dbff = new DBFSharp.DBFFile(fd, FileMode.Create);
                DBFSharp.FieldInfos finfos = new DBFSharp.FieldInfos();
                finfos.Add("MERGE_ID", 020, DBFSharp.FieldType.Numeric);
                finfos.Add("FILE_ID", 020, DBFSharp.FieldType.Numeric);
                finfos.Add("FILE_NAME", 100, DBFSharp.FieldType.Character);
                if (mDBF)
                {
                    foreach (string file in files)
                    {
                        string md = file.Replace(Path.GetExtension(file), ".dbf");
                        MergeDBFHeaders(finfos, md);
                    };
                };
                dbff.WriteHeader(finfos);
            };
            FileStream f_to = new FileStream(resFile, FileMode.Create, FileAccess.Write);
            int fCounted = 0; int rCounted = 0; long progress = 0;
            foreach (string file in files)
            {
                FileStream f_from = new FileStream(file, FileMode.Open, FileAccess.Read);
                if ((++fCounted) == 1) CopyShapeHeader(f_from, f_to);
                CopyShapeBody(f_from, f_to, ref rCounted, ref progress, ttlLength, dbff, mDBF);
                f_from.Close();
            };
            f_to.Close();
            if (dbff != null) dbff.Close();
            wbf.Hide();
            wbf = null;
            MessageBox.Show(
                "Merging done!\r\nTotal objects: "+rCounted.ToString(), 
                "Merging", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void MergeDBFHeaders(DBFSharp.FieldInfos finfos, string fileName)
        {
            if (!File.Exists(fileName)) return;
            DBFSharp.DBFFile tmpf = new DBFSharp.DBFFile(fileName, FileMode.Open);
            DBFSharp.FieldInfos fff = tmpf.FieldInfos;
            tmpf.Close();
            if (fff == null) return;
            if (fff.Count == 0) return;
            for (int i = 0; i < fff.Count; i++)
            {
                bool ex = false;
                for (int y = 0; y < finfos.Count; y++)
                {
                    if (finfos[y].GName == fff[i].GName)
                        ex = true;
                    if (finfos[y].BName(System.Text.Encoding.GetEncoding(1251)) == fff[i].BName(System.Text.Encoding.GetEncoding(1251)))
                        ex = true;
                };
                if (!ex)
                {
                    DBFSharp.FieldType ft = fff[i].FType;
                    //if(ft == DBFSharp.FieldType.Float) ft = DBFSharp.FieldType.Numeric; 
                    //if(ft == DBFSharp.FieldType.Integer) ft = DBFSharp.FieldType.Numeric;
                    byte fl = fff[i].FLength;
                    if (fl < 10) fl = (byte)10;
                    finfos.Add(new DBFSharp.FieldInfo(fff[i].FName, fl, fff[i].FDecimalPoint , ft));
                };
            };
        }

        private void CopyShapeHeader(FileStream fromFile, FileStream toFile)
        {
            byte[] arr = new byte[100];
            fromFile.Position = 0;
            toFile.Position = 0;
            fromFile.Read(arr, 0, arr.Length);
            toFile.Write(arr, 0, arr.Length);
            toFile.Flush();
        }

        private void CopyShapeBody(FileStream fromFile, FileStream toFile, ref int recordsCounted, ref long progress, long ttlLength, DBFSharp.DBFFile dbff, bool mDBF)
        {
            string dName = fromFile.Name.Replace(Path.GetExtension(fromFile.Name),".dbf");
            string fName = Path.GetFileName(fromFile.Name);
            string tName = Path.GetFileName(toFile.Name);

            DBFSharp.DBFFile tmp_dbf = null;
            if (mDBF && File.Exists(dName))
                tmp_dbf = new DBFSharp.DBFFile(dName, FileMode.Open);

            progress += 100;
            fromFile.Position = 100;            
            byte[] recHeader = new byte[8];
            uint infile_counter = 0;
            while (fromFile.Position < fromFile.Length)
            {
                // READ
                fromFile.Read(recHeader, 0, recHeader.Length);
                int recordNumber = ShapeInfo.readIntBig(recHeader, 0);
                int contentLength = ShapeInfo.readIntBig(recHeader, 4);
                byte[] recData = new byte[contentLength * 2];
                fromFile.Read(recData, 0, recData.Length);
                // WRITE
                ShapeInfo.writeIntBig(ref recHeader, 0, ++recordsCounted);
                toFile.Write(recHeader, 0, recHeader.Length);
                toFile.Write(recData, 0, recData.Length);
                // DBF
                if (dbff != null)
                {
                    Dictionary<string, object> rec = new Dictionary<string, object>();
                    rec.Add("MERGE_ID", recordsCounted);                    
                    rec.Add("FILE_ID", recordNumber);
                    rec.Add("FILE_NAME", fName);
                    if (tmp_dbf != null)
                    {
                        Dictionary<string,object> recs = tmp_dbf.ReadRecord(++infile_counter);
                        foreach (KeyValuePair<string, object> kvp in recs)
                        {
                            if (rec.ContainsKey(kvp.Key))
                                rec[kvp.Key] = kvp.Value;
                            else
                                rec.Add(kvp.Key, kvp.Value);
                        };
                    };
                    dbff.WriteRecord(rec);
                };
                // PROGRESS
                progress += 8 + contentLength * 2;
                if (wbf != null)
                {                    
                    double pc = 100.0 * (((double)progress) / ((double)ttlLength));
                    wbf.Text = String.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "Merging file {0} to {4}, total: {1}/{2} - {3:0.00}%", fName, progress, ttlLength, pc, tName);
                };
            };
            toFile.Flush();
            if (tmp_dbf != null) tmp_dbf.Close();
        }

        private void addFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFD();
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            cdf.Enabled = listBox1.SelectedIndex >= 0;
            clist.Enabled = listBox1.Items.Count > 0;
        }

        private void clist_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
            CheckEnabled();
        }

        private void cdf_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex < 0) return;
            listBox1.Items.RemoveAt(listBox1.SelectedIndex);
            CheckEnabled();
        }

        private void wdbf_CheckedChanged(object sender, EventArgs e)
        {
            mbox.Enabled = wdbf.Checked;
        }
    }

    public class ShapeInfo
    {
        public FileInfo fileInfo;
        public int shpType = 0;

        public ShapeInfo(FileInfo fi)
        {
            this.fileInfo = fi;
            LoadShapeType();
        }

        private void LoadShapeType()
        {
            FileStream shapeFileStream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read);
            long shapeFileLength = shapeFileStream.Length;
            if (shapeFileLength < 100)
            {
                shapeFileStream.Close();
                return;
            };

            byte[] shapeFileData = new byte[100];
            shapeFileStream.Read(shapeFileData, 0, shapeFileData.Length);
            shapeFileStream.Close();

            this.shpType = readIntLittle(shapeFileData, 32);            
        }

        public static int readIntLittle(byte[] data, int pos)
        {
            byte[] bytes = new byte[4];
            bytes[0] = data[pos];
            bytes[1] = data[pos + 1];
            bytes[2] = data[pos + 2];
            bytes[3] = data[pos + 3];
            return BitConverter.ToInt32(bytes, 0);
        }

        public static int readIntBig(byte[] data, int pos)
        {
            byte[] bytes = new byte[4];
            bytes[0] = data[pos];
            bytes[1] = data[pos + 1];
            bytes[2] = data[pos + 2];
            bytes[3] = data[pos + 3];
            Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        public static void writeIntBig(ref byte[] data, int pos, int val)
        {
            byte[] arr = BitConverter.GetBytes(val);
            Array.Reverse(arr);
            Array.Copy(arr, 0, data, pos, 4);
        }

        public string ShapeType
        {
            get
            {
                if (shpType == 1) return "Point";
                if (shpType == 3) return "PolyLine";
                if (shpType == 5) return "Polygon";
                return "Unknown";
            }
        }

        public override string ToString()
        {
            return String.Format("{0} as [{2}] at {1}", fileInfo.Name, fileInfo.Directory, ShapeType);
        }
    }
}