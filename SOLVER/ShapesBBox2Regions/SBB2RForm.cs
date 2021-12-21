using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace ShapesBBox2Regions
{
    public partial class SBB2RForm : Form
    {
        public SBB2RForm()
        {
            InitializeComponent();
            this.listBox1.DrawMode = DrawMode.OwnerDrawFixed;
            this.AllowDrop = true;
            this.DragEnter += new DragEventHandler(SBB2RForm_DragEnter);
            this.DragDrop += new DragEventHandler(SBB2RForm_DragDrop);
        }

        private void SBB2RForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void SBB2RForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            AddFiles(files);           
        }

        private void AddFiles(string[] files)
        {
            if (files == null) return;
            if (files.Length == 0) return;

            KMZRebuilder.WaitingBoxForm wbf = new KMZRebuilder.WaitingBoxForm("Import files", "Prepare", this);
            wbf.Show();
            for (int i = 0; i < files.Length; i++)
            {
                string file = Path.GetFileName(files[i]);
                wbf.Text = String.Format(System.Globalization.CultureInfo.InvariantCulture, "Adding {0} file - {1}/{2} - {3:0.00}%", file, i + 1, files.Length, ((double)(i + 1)) / ((double)files.Length) * 100.0);
                try
                {
                    AddFile(files[i]);
                }
                catch (Exception ex)
                {
                    wbf.Hide();
                    MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
            };
            wbf.Hide();
            wbf = null;
            UpdateData();
        }

        private void AddFile(string fileName)
        {
            if(String.IsNullOrEmpty(fileName)) return;
            if(!File.Exists(fileName)) return;
            string ext = Path.GetExtension(fileName).ToLower();
            if((ext != ".shp") && (ext != ".xml") && (ext != ".rt")) return;

            BoxRecord br = null;
            if (ext == ".shp") br = GetShape(fileName);
            if (ext == ".xml") br = GetXML(fileName);
            if (ext == ".rt") br = GetRT(fileName);

            if (br == null) return;
            listBox1.Items.Add(br);            
        }

        private BoxRecord GetShape(string fileName)
        {
            ShapeBoxReader sbr = new ShapeBoxReader(fileName);
            if (!sbr.ShapeOk) return null;
            return new BoxRecord(sbr.box, 999, Path.GetFileName(fileName), Path.GetFileName(fileName));
        }

        private BoxRecord GetXML(string fileName)
        {
            try
            {
                AdditionalInformation addit = XMLSaved<AdditionalInformation>.Load(fileName);
                if (double.IsNaN(addit.minX)) return null;
                if (double.IsNaN(addit.minY)) return null;
                if (double.IsNaN(addit.maxX)) return null;
                if (double.IsNaN(addit.maxY)) return null;
                if ((addit.minX + addit.minY + addit.maxX + addit.maxY) == 0) return null;
                return new BoxRecord(new double[] { addit.minX, addit.minY, addit.maxX, addit.maxY }, addit.regionID, addit.RegionName, Path.GetFileName(fileName));
            }
            catch { };
            return null;
        }

        private BoxRecord GetRT(string fileName)
        {
            string s_path = null;
            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs);
            while (!sr.EndOfStream)
            {
                string ln = sr.ReadLine();
                if(String.IsNullOrEmpty(ln)) continue;
                if(ln.StartsWith("Source path:"))
                {
                    s_path = ln.Substring(13).Trim();
                    break;
                };
            };
            sr.Close();
            fs.Close();

            if ((!String.IsNullOrEmpty(s_path)) && (File.Exists(s_path)))
            {
                BoxRecord br =  GetShape(s_path);
                if (br != null) br.Name += " from " + Path.GetFileName(fileName);
                if (br != null) br.File += Path.GetFileName(fileName);
                return br;
            };

            return null;
        }

        private int info_last = 0;
        private void UpdateData()
        {
            lttl.Text = String.Format("Total Boxes: {0}", listBox1.Items.Count);
            if (info_last != listBox1.Items.Count)
            {
                if (listBox1.Items.Count > info_last)
                    llast.Text = String.Format("Status: Added {0} boxes", listBox1.Items.Count - info_last);
                else
                    llast.Text = String.Format("Status: Removed {0} boxes", info_last - listBox1.Items.Count);
            }
            else
                llast.Text = "Status: Idle";
            info_last = listBox1.Items.Count;
            listBox1.Refresh();
            saveBtn.Enabled = listBox1.Items.Count > 0;
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            clearListToolStripMenuItem.Enabled = listBox1.Items.Count > 0;
            deleteCurrentFileToolStripMenuItem.Enabled = listBox1.SelectedIndex >= 0;
            changeRegionIDToolStripMenuItem.Enabled = listBox1.SelectedIndex >= 0;
            changeRegionNameToolStripMenuItem.Enabled = listBox1.SelectedIndex >= 0;
            sortByIDToolStripMenuItem.Enabled = listBox1.Items.Count > 0;
            sortByNameToolStripMenuItem.Enabled = listBox1.Items.Count > 0;
        }

        private void addFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Add files";
            ofd.DefaultExt = ".shp";
            ofd.Multiselect = true;
            ofd.Filter = "All supported files (*.shp;*.xml;*.rt)|*.shp;*.xml;*.rt|Shape files (*.shp)|*.shp|XML files (*.xml)|*.xml|Route info files (*.rt)|*.rt";
            string[] files = null;
            if(ofd.ShowDialog() == DialogResult.OK)
                files = ofd.FileNames;
            ofd.Dispose();
            if(files == null) return;
            AddFiles(files);
        }

        private void addFilesFromDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = "";
            if (InputBox.QueryDirectoryBox("Add files", "Select Directory:", ref path) != DialogResult.OK) return;
            List<string> files = new List<string>();
            try { files.AddRange(Directory.GetFiles(path, "*.rt", SearchOption.TopDirectoryOnly)); } catch { };
            try { files.AddRange(Directory.GetFiles(path, "*.shp", SearchOption.TopDirectoryOnly)); } catch { };
            try { files.AddRange(Directory.GetFiles(path, "*.xml", SearchOption.TopDirectoryOnly)); } catch { };
            if(files.Count > 0) AddFiles(files.ToArray());
        }

        private void addRtFromDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = "";
            if (InputBox.QueryDirectoryBox("Add files", "Select Directory:", ref path) != DialogResult.OK) return;
            List<string> files = new List<string>();
            try { files.AddRange(Directory.GetFiles(path, "*.rt", SearchOption.TopDirectoryOnly)); } catch { };
            if (files.Count > 0) AddFiles(files.ToArray());
        }

        private void addShapeFilesFromDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = "";
            if (InputBox.QueryDirectoryBox("Add files", "Select Directory:", ref path) != DialogResult.OK) return;
            List<string> files = new List<string>();
            try { files.AddRange(Directory.GetFiles(path, "*.shp", SearchOption.TopDirectoryOnly)); } catch { };
            if (files.Count > 0) AddFiles(files.ToArray());
        }

        private void addXMLFromDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = "";
            if (InputBox.QueryDirectoryBox("Add files", "Select Directory:", ref path) != DialogResult.OK) return;
            List<string> files = new List<string>();
            try { files.AddRange(Directory.GetFiles(path, "*.xml", SearchOption.TopDirectoryOnly)); } catch { };
            if (files.Count > 0) AddFiles(files.ToArray());
        }

        private void Remove(int index)
        {
            int si = listBox1.SelectedIndex;
            listBox1.Items.RemoveAt(listBox1.SelectedIndex);
            if (si < listBox1.Items.Count) listBox1.SelectedIndex = si;
            else if (--si >= 0)
                listBox1.SelectedIndex = si;
            UpdateData();
        }

        private void deleteCurrentFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex < 0) return;
            Remove(listBox1.SelectedIndex);            
        }

        private DateTime lastNumEnter = DateTime.MinValue;
        private int lastNumSelected = -1;
        private void listBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.KeyCode == Keys.Delete) && (listBox1.SelectedIndex >= 0))
            {
                lastNumSelected = -1;
                Remove(listBox1.SelectedIndex);                
            };

            if ((e.Modifiers == Keys.Alt) && (listBox1.Items.Count > 1) && (listBox1.SelectedIndex >= 0))
            {
                lastNumSelected = -1;
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
                UpdateData();
            };

            if ((e.KeyCode == Keys.Insert) || ((e.Modifiers == Keys.Control) && (e.KeyCode == Keys.O)))
            {
                lastNumSelected = -1;
                addFilesToolStripMenuItem_Click(sender, null);
                return;
            };

            if ((e.KeyCode == Keys.F2) && (listBox1.SelectedIndex >= 0))
            {
                lastNumSelected = -1;
                Rename(listBox1.SelectedIndex);
                return;
            };

            if ((e.Modifiers == Keys.None) && (e.KeyCode == Keys.Enter) && (listBox1.SelectedIndex >= 0))
            {
                lastNumSelected = -1;
                int ns = listBox1.SelectedIndex + 1;
                if (ns < listBox1.Items.Count)
                    listBox1.SelectedIndex = ns;
                else
                    listBox1.SelectedIndex = 0;
                return;
            };    
            
            if ((e.KeyCode == Keys.Space) && (listBox1.SelectedIndex >= 0))
            {
                lastNumSelected = -1;
                ReID(listBox1.SelectedIndex);
                return;
            };

            if ((e.Modifiers == Keys.None) && (listBox1.SelectedIndex >= 0))
            {
                int di = DigitByKey(e.KeyCode);
                if(di >= 0)
                {
                    e.SuppressKeyPress = true;
                    bool ni = (lastNumSelected != listBox1.SelectedIndex) || (DateTime.Now.Subtract(lastNumEnter).TotalSeconds > 2);
                    ReID(listBox1.SelectedIndex, di.ToString(), ni);
                    lastNumEnter = DateTime.Now;
                    lastNumSelected = listBox1.SelectedIndex;
                    return;
                }
                else
                    lastNumSelected = -1; ;
            };
        }

        private int DigitByKey(Keys key)
        {
            switch(key)
            {
                case Keys.D0: case Keys.NumPad0: return 0;
                case Keys.D1: case Keys.NumPad1: return 1;
                case Keys.D2: case Keys.NumPad2: return 2;
                case Keys.D3: case Keys.NumPad3: return 3;
                case Keys.D4: case Keys.NumPad4: return 4;
                case Keys.D5: case Keys.NumPad5: return 5;
                case Keys.D6: case Keys.NumPad6: return 6;
                case Keys.D7: case Keys.NumPad7: return 7;
                case Keys.D8: case Keys.NumPad8: return 8;
                case Keys.D9: case Keys.NumPad9: return 9;
                default: return -1;
            };
        }

        private void clearListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
            UpdateData();
        }

        private void listBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            
        }

        private void ReID(int index)
        {
            BoxRecord br = (BoxRecord)listBox1.Items[index];
            int id = br.ID;
            if (InputBox.Show("Change ID", "Enter new ID:", ref id, 1, 999, null) != DialogResult.OK) return;
            br.ID = id;
            listBox1.Items[index] = br;
        }

        private void ReID(int index, string d, bool newValue)
        {
            BoxRecord br = (BoxRecord)listBox1.Items[index];
            string id = newValue ? d : br.ID.ToString() + d;
            if (id.Length > nidBox.Value) id = id.Substring(id.Length - (int)nidBox.Value);
            br.ID = int.Parse(id);
            listBox1.Items[index] = br;
        }

        private void Rename(int index)
        {
            BoxRecord br = (BoxRecord)listBox1.Items[index];
            string name = br.Name;
            if (InputBox.Show("Change Name", "Enter new name:", ref name, "") != DialogResult.OK) return;
            br.Name = name;
            listBox1.Items[index] = br;
        }

        private void changeRegionIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex < 0) return;
            ReID(listBox1.SelectedIndex);
        }

        private void changeRegionNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex < 0) return;
            Rename(listBox1.SelectedIndex);
        }

        private void saveBtn_Click(object sender, EventArgs e)
        {
            if (listBox1.Items.Count == 0) return;

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Save Regions";
            sfd.DefaultExt = ".shp";
            sfd.Filter = "Shape files (*.shp)|*.shp";
            string fName = null;
            if (sfd.ShowDialog() == DialogResult.OK) fName = sfd.FileName;
            sfd.Dispose();
            if (fName == null) return;
            SaveRegions(fName);
        }

        private void SaveRegions(string fileName)
        {            
            SimpleSHPWriter ssw = SimpleSHPWriter.CreateAreasFile(fileName);
            DBFSharp.DBFFile dbf = new DBFSharp.DBFFile(fileName.Replace(Path.GetExtension(fileName), ".dbf"), FileMode.Create);

            DBFSharp.FieldInfos finfos = new DBFSharp.FieldInfos();
            finfos.Add("REGION_ID", 010, DBFSharp.FieldType.Numeric);
            finfos.Add("NAME", 128, DBFSharp.FieldType.Character);
            finfos.Add("X", 019, 11, DBFSharp.FieldType.Numeric);
            finfos.Add("Y", 019, 11, DBFSharp.FieldType.Numeric);
            dbf.WriteHeader(finfos);

            KMZRebuilder.WaitingBoxForm wbf = new KMZRebuilder.WaitingBoxForm("Create Regions Shape", "Prepare", this);
            wbf.Show();

            for (int i = 0; i < listBox1.Items.Count; i++)
            {                
                BoxRecord br = (BoxRecord)listBox1.Items[i];

                wbf.Text = String.Format(System.Globalization.CultureInfo.InvariantCulture, "Adding box {0} - {1}/{2} - {3:0.00}%", br.ID, i + 1, listBox1.Items.Count, ((double)(i + 1)) / ((double)listBox1.Items.Count) * 100.0);

                ssw.WriteSingleArea(br.Enlarge((int)kmBox.Value));

                Dictionary<string, object> rec = new Dictionary<string, object>();
                rec.Add("REGION_ID", br.ID);
                rec.Add("NAME", br.Name);
                rec.Add("X", br.centerX);
                rec.Add("Y", br.centerY);
                dbf.WriteRecord(rec);
            };
            dbf.Close();
            ssw.Close();

            wbf.Hide();
            wbf = null;
        }

        private void Sort(byte sortBy)
        {
            if (listBox1.Items.Count == 0) return;
            BoxRecord si = null;
            if (listBox1.SelectedIndex >= 0) si = (BoxRecord)listBox1.SelectedItem;
            List<BoxRecord> brs = new List<BoxRecord>();
            for (int i = 0; i < listBox1.Items.Count; i++) brs.Add((BoxRecord)listBox1.Items[i]);
            listBox1.Items.Clear();
            brs.Sort(new BoxSorter((byte)sortBy));
            foreach (BoxRecord br in brs) listBox1.Items.Add(br);
            if (si != null) listBox1.SelectedItem = si;
        }

        private void sortByIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Sort(0);
        }

        private void sortByNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Sort(1);
        }

        private void listBox1_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();

            if (e.Index >= 0)
            {
                int dopW = 50;

                BoxRecord br = (BoxRecord)listBox1.Items[e.Index];
                string sID = br.ID.ToString();
                Font fD = new Font(e.Font, FontStyle.Bold);

                SizeF sD = e.Graphics.MeasureString(sID, e.Font);
                SizeF sN = e.Graphics.MeasureString(br.Name, e.Font);
                SizeF sB = e.Graphics.MeasureString(br.SBox, e.Font);
                SizeF sC = e.Graphics.MeasureString(br.SCXY, e.Font);

                e.Graphics.DrawString(sID, fD, new SolidBrush(e.ForeColor), e.Bounds.X + dopW - sD.Width, e.Bounds.Y);
                e.Graphics.DrawString(br.Name, e.Font, new SolidBrush(e.ForeColor), e.Bounds.X + 2 + dopW, e.Bounds.Y);
                if ((e.State & DrawItemState.Selected) == 0)
                    e.Graphics.FillRectangle(new SolidBrush(Color.LightYellow), new Rectangle(new Point((int)(e.Bounds.X + 2 + dopW + sN.Width + 5), (int)e.Bounds.Y), new Size((int)sB.Width, (int)e.Bounds.Height)));
                e.Graphics.DrawString(br.SBox, e.Font, new SolidBrush(Color.Gray), e.Bounds.X + 2 + dopW + sN.Width + 5, e.Bounds.Y);
                e.Graphics.DrawString(br.SCXY, e.Font, new SolidBrush(Color.DarkGreen), e.Bounds.X + 2 + dopW + sN.Width + 5 + sB.Width + 5, e.Bounds.Y);
                e.Graphics.DrawString(br.File, e.Font, new SolidBrush(Color.Maroon), e.Bounds.X + 2 + dopW + sN.Width + 5 + sB.Width + 5 + sC.Width + 5, e.Bounds.Y);
            };

            e.DrawFocusRectangle();
        }
    }
}