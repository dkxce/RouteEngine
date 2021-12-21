using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.IO;

namespace RouteGraphBatcher
{
    public partial class RGBCForm : Form
    {
        public dkxce.Route.Regions.PointInRegionUtils piru;
            
        public BatchParams bp = new BatchParams();
        public RGBCForm()
        {
            InitializeComponent();            
        }

        private void PreFillDirs()
        {
            bp.Graphs = gd.Text;
            bp.Regions = rd.Text;
            bp.RGWays = wd.Text;            
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if(cb1.Checked)
                if (!File.Exists(f1.Text))
                {
                    MessageBox.Show("Файл `" + f1.Text + "` не найден!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                };  

            if(cb2.Checked || cb3.Checked)
                if (cb.SelectedIndex < 0)
                {
                    MessageBox.Show("Выберите регион", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                };

            gb.Enabled = false;
            cb.Enabled = false;
            rb.Enabled = false;
            f1.Enabled = false;
            sf.Enabled = false;
            btnsum.Enabled = false;
            cstreg.Enabled = false;
            customreg.Enabled = false;

            PreFillDirs();
            string bpg = bp.Graphs;
            string bpr = bp.Regions;
            string bpw = bp.RGWays;

            if (!Path.IsPathRooted(bpg))
                bpg = XMLSaved<int>.GetCurrentDir() + bpg;
            if (!Path.IsPathRooted(bpr))
                bpr = XMLSaved<int>.GetCurrentDir() + bpr;
            if (!Path.IsPathRooted(bpw))
                bpw = XMLSaved<int>.GetCurrentDir() + bpw;

            int reg = (cstreg.Checked) ? (int)customreg.Value : ((NI)cb.SelectedItem).i;
            string RegName = "", nRegName = "";
            if (!cstreg.Checked)
            {
                string rn = ((NI)cb.SelectedItem).n;
                if (rn.IndexOf("[") > 0) rn = rn.Substring(0, rn.IndexOf("[")).Trim();
                nRegName = RegName = rn;
            };
            if (arn.Checked)
            {
                if (InputBox.QueryStringBox("Название региона", "Введите название региона:", ref nRegName) == DialogResult.OK)
                    RegName = nRegName;
            };
            
            // OLD VERSION // where no B & region in name, example: `01T02.rgway.xml`
            // foreach (string file in Directory.GetFiles(bpg, String.Format("{0:000}", reg) + ".rgnodes.xml"))
            //    File.Delete(file);
            //{
            //dkxce.Route.Classes.TRGNode[] nds = XMLSaved<dkxce.Route.Classes.T RGNode[]>.Load(file);
            //File.Delete(file);
            //for(int x=0;x<nds.Length;x++)
            //    for (int y = 0; y < nds.Length; y++)
            //    {
            //        string tmp_file = bpw + @"\" + nds[x].id.ToString() + "T" + nds[y].id.ToString() + ".rgway.xml";
            //        if (File.Exists(tmp_file)) 
            //            File.Delete(tmp_file);
            //    };                
            //};

            // BEGIN //
            textBox1.Clear();

            if (cb5.Checked)
            {
                textBox1.Text += @"copy " + bpg + @"\" + String.Format("{0:000}", reg) + "*.*" + " " + bpg + "\\BACKUP\r\n\r\n";
                string dName = Directory.GetParent(bpg + @"\BACKUP").FullName;
                string nName = dName + @"\BACKUP\";
                foreach (string newPath in Directory.GetFiles(dName, String.Format("{0:000}", reg) + "*.*", SearchOption.TopDirectoryOnly))
                    File.Copy(newPath, newPath.Replace(dName, nName), true);
            };

            if (cb2.Checked)
            {
                textBox1.Text += "del " + bpw + @"\*B" + reg.ToString() + ".rgway.xml" + "\r\n\r\n";
                string[] files = Directory.GetFiles(bpw + @"\", "*B" + reg.ToString() + ".rgway.xml");
                foreach (string ftd in files)
                    File.Delete(ftd);
            };

            if (cb1.Checked)
            {
                textBox1.Text += @"del " + bpg + @"\*B" + String.Format("{0:000}", reg) + "*.*" + "\r\n\r\n";
                string[] files = Directory.GetFiles(bpg, String.Format("{0:000}", reg) + "*.*");
                foreach (string file in files)
                {
                    if (file.Substring(file.Length - 8).ToLower() == ".jam.cfg") continue;
                    File.Delete(file);
                };
            };

            System.Diagnostics.ProcessStartInfo psi;
            System.Diagnostics.Process p;
            if (cb1.Checked)
            {
                psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = XMLSaved<int>.GetCurrentDir() + @"\RouteGraphCreator.exe";
                psi.Arguments = reg.ToString() + " " + "\"" + f1.Text + "\" " + "\"" + bpg + @"\" + String.Format("{0:000}", reg) + ".rt\"";
                if (cb4.Checked)
                    psi.Arguments += " attr";
                if(!String.IsNullOrEmpty(RegName))
                    psi.Arguments += " \"/regName=" + RegName + "\"";

                textBox1.Text += "RouteGraphCreator.exe " + psi.Arguments + "\r\n\r\n";

                p = System.Diagnostics.Process.Start(psi);
                Application.DoEvents();
                p.WaitForExit();
                Application.DoEvents();
            };

            if (cb2.Checked)
            {
                // CalcRG
                psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = XMLSaved<int>.GetCurrentDir() + @"\RouteGraphCalcRG.exe";
                psi.Arguments = reg.ToString() + " " + "\"" + bpg + @"\" + String.Format("{0:000}", reg) + ".rt\" \"" + bpw + "\" ";

                textBox1.Text += "RouteGraphCalcRG.exe " + psi.Arguments + "\r\n\r\n";

                p = System.Diagnostics.Process.Start(psi);
                Application.DoEvents();
                p.WaitForExit();
                Application.DoEvents();                
            };

            if (cb3.Checked)
            {
                // MP
                psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = XMLSaved<int>.GetCurrentDir() + @"\RGWay2RTE.exe";
                psi.Arguments = "mp \"" + bpw + "\\*B" + reg.ToString() + ".rgway.xml\"";

                textBox1.Text += "RGWay2RTE.exe " + psi.Arguments + "\r\n\r\n";

                p = System.Diagnostics.Process.Start(psi);
                Application.DoEvents();
                p.WaitForExit();
                Application.DoEvents();
            };

            gb.Enabled = true;
            cb.Enabled = true;
            rb.Enabled = true;
            f1.Enabled = true;
            sf.Enabled = true;
            btnsum.Enabled = true;
            cstreg.Enabled = true;
            customreg.Enabled = true;
            if (cstreg.Checked) cb.Enabled = false;
            else customreg.Enabled = false;

            textBox1.Text += "...\r\nDone!";
        }

        private void sf_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Выберите shape-файл";
            ofd.DefaultExt = ".shp";
            ofd.Filter = "Shape files (*.shp)|*.shp";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                f1.Text = ofd.FileName;
            };
            ofd.Dispose();
        }

        private void gd_TextChanged(object sender, EventArgs e)
        {
            gt.Text = gd.Text;
            if (!Path.IsPathRooted(gt.Text))
                gt.Text = XMLSaved<int>.GetCurrentDir() + gt.Text;
            
            nt.Text = rd.Text;
            if (!Path.IsPathRooted(nt.Text))
                nt.Text = XMLSaved<int>.GetCurrentDir() + nt.Text;

            wt.Text = wd.Text;
            if (!Path.IsPathRooted(wt.Text))
                wt.Text = XMLSaved<int>.GetCurrentDir() + wt.Text;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            PreFillDirs();
            XMLSaved<BatchParams>.Save(XMLSaved<int>.GetCurrentDir() + @"\RouteGraphBatcher.xml", bp);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string bpf = XMLSaved<int>.GetCurrentDir() + @"\RouteGraphBatcher.xml";
            if (File.Exists(bpf)) bp = XMLSaved<BatchParams>.Load(bpf);            

            gd.Text = bp.Graphs;
            rd.Text = bp.Regions;
            wd.Text = bp.RGWays;

            piru = new dkxce.Route.Regions.PointInRegionUtils();
            string tmp = rd.Text;
            if (!Path.IsPathRooted(tmp)) tmp = XMLSaved<int>.GetCurrentDir() + tmp;
            piru.LoadRegionsFromFile(tmp + @"\Regions.shp");            

            for (int i = 0; i < piru.RegionsNames.Length; i++)
                cb.Items.Add(new NI(piru.RegionsNames[i]+" "+String.Format("[{0:000}]",piru.RegionsIDs[i]),piru.RegionsIDs[i]));
            cb.Sorted = true;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.OverwritePrompt = false;
            sfd.Title = "Выберите папку";
            sfd.FileName = "sample.rt";
            sfd.DefaultExt = ".rt";
            sfd.Filter = "Route Graphs|*.rt";
            if (sfd.ShowDialog() == DialogResult.OK)
                gd.Text = Path.GetDirectoryName(sfd.FileName);
            sfd.Dispose();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.OverwritePrompt = false;
            sfd.Title = "Выберите папку";
            sfd.FileName = "sample.shp";
            sfd.DefaultExt = ".shp";
            sfd.Filter = "Shape files|*.shp";
            if (sfd.ShowDialog() == DialogResult.OK)
                rd.Text = Path.GetDirectoryName(sfd.FileName);
            sfd.Dispose();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.OverwritePrompt = false;
            sfd.Title = "Выберите папку";
            sfd.FileName = "sample.xml";
            sfd.DefaultExt = ".xml";
            sfd.Filter = "XML files|*.xml";
            if (sfd.ShowDialog() == DialogResult.OK)
                wd.Text = Path.GetDirectoryName(sfd.FileName);
            sfd.Dispose();
        }

        private void btnsum_Click(object sender, EventArgs e)
        {
            gb.Enabled = false;
            cb.Enabled = false;
            rb.Enabled = false;
            f1.Enabled = false;
            sf.Enabled = false;
            btnsum.Enabled = false;

            PreFillDirs();
            string bpg = bp.Graphs;
            string bpr = bp.Regions;
            
            if (!Path.IsPathRooted(bpg))
                bpg = XMLSaved<int>.GetCurrentDir() + bpg;
            if (!Path.IsPathRooted(bpr))
                bpr = XMLSaved<int>.GetCurrentDir() + bpr;
            
            //
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
            psi.FileName = XMLSaved<int>.GetCurrentDir() + @"\RouteGraphCalcMatrix.exe";
            psi.Arguments = "\"" + bpg + "\" " + "\"" + bpg + @"\"+"000.bin\" "+bpr;

            textBox1.Clear();
            textBox1.Text += psi.FileName + " " + psi.Arguments + "\r\n";

            System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi);
            Application.DoEvents();
            p.WaitForExit();
            Application.DoEvents();
            //

            gb.Enabled = true;
            cb.Enabled = true;
            rb.Enabled = true;
            f1.Enabled = true;
            sf.Enabled = true;
            btnsum.Enabled = true;
        }

        private void cb2_CheckedChanged(object sender, EventArgs e)
        {
        }

        private void cb1_CheckedChanged(object sender, EventArgs e)
        {
            cb4.Enabled = cb1.Checked;
        }

        private void f1_TextChanged(object sender, EventArgs e)
        {
            if (File.Exists(f1.Text))
            {
                PreReadShape(f1.Text);
                string cfgf = PreCheckFCFG(f1.Text);
                PreCheckFields(cfgf, f1.Text.Replace(System.IO.Path.GetExtension(f1.Text),".dbf"));
            };
        }

        private void PreCheckFields(string cfgFile, string dbfFile)
        {
            dkxce.Route.Classes.ShapeFields stream_ShapeFile_FieldNames = null;
            try
            {
                stream_ShapeFile_FieldNames = dkxce.Route.Classes.ShapeFields.Load(cfgFile);
            }
            catch
            {
                return;
            };

            List<string> flds = new List<string>();
            Hashtable fht = new Hashtable();
            string fieldsOk = "";
            string fieldsBad = "";
            string fieldsErr = "";
            string fieldsEdd = "";
            string cautions = "";
            bool raiseNoFields = false;
            // READ FIELDS
            try
            {
                string[] FIELDS = ReadDBFFields(dbfFile, out fht);
                flds.AddRange(FIELDS);
            }
            catch (Exception)
            {
                return;
            };
            if (flds.Count == 0) return;

            // CHECK FIELDS
            {
                if (stream_ShapeFile_FieldNames.SOURCE == "GARMIN")
                {
                    Console.WriteLine(stream_ShapeFile_FieldNames.SOURCE);

                    CheckFields("LinkId", ref stream_ShapeFile_FieldNames.fldLinkId, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 0, 'N', fht, ref cautions);
                    CheckFields("GarminType", ref stream_ShapeFile_FieldNames.fldGarminType, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 0, 'C', fht, ref cautions);
                }
                else if (stream_ShapeFile_FieldNames.SOURCE == "OSM")
                {
                    Console.WriteLine(stream_ShapeFile_FieldNames.SOURCE);

                    CheckFields("OSM_ID", ref stream_ShapeFile_FieldNames.fldOSM_ID, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 0, 'N', fht, ref cautions);
                    CheckFields("OSM_SURFACE", ref stream_ShapeFile_FieldNames.fldOSM_SURFACE, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 0, 'C', fht, ref cautions);
                }
                else if ((stream_ShapeFile_FieldNames.SOURCE == "OSM2") || (stream_ShapeFile_FieldNames.SOURCE == "OSM2SHP"))
                {
                    CheckFields("OSM_ID", ref stream_ShapeFile_FieldNames.fldOSM_ID, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 0, 'N', fht, ref cautions);
                    CheckFields("OSM_TYPE", ref stream_ShapeFile_FieldNames.fldOSM_TYPE, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 1, 'C', fht, ref cautions);
                    CheckFields("OSM_SURFACE", ref stream_ShapeFile_FieldNames.fldOSM_SURFACE, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 1, 'C', fht, ref cautions);
                    CheckFields("GarminType", ref stream_ShapeFile_FieldNames.fldGarminType, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 1, 'C', fht, ref cautions);

                    CheckFields("SERVICE", ref stream_ShapeFile_FieldNames.fldOSM_ADDIT_SERVICE, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'C', fht, ref cautions);
                    CheckFields("JUNCTION", ref stream_ShapeFile_FieldNames.fldOSM_ADDIT_JUNCTION, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'C', fht, ref cautions);
                    CheckFields("LANES", ref stream_ShapeFile_FieldNames.fldOSM_ADDIT_LANES, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'C', fht, ref cautions);
                    CheckFields("MAXACTUAL", ref stream_ShapeFile_FieldNames.fldOSM_ADDIT_MAXACTUAL, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'C', fht, ref cautions);
                };
                // other fields
                {
                    CheckFields("SpeedLimit", ref stream_ShapeFile_FieldNames.fldSpeedLimit, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 0, 'N', fht, ref cautions);
                    CheckFields("RouteLevel", ref stream_ShapeFile_FieldNames.fldRouteLevel, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'N', fht, ref cautions);
                    CheckFields("RouteSpeed", ref stream_ShapeFile_FieldNames.fldRouteSpeed, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'N', fht, ref cautions);
                    CheckFields("OneWay", ref stream_ShapeFile_FieldNames.fldOneWay, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 0, '0', fht, ref cautions);
                    CheckFields("Length", ref stream_ShapeFile_FieldNames.fldLength, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 3, 'F', fht, ref cautions);
                    CheckFields("Name", ref stream_ShapeFile_FieldNames.fldName, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 3, 'C', fht, ref cautions);
                    CheckFields("TurnRestrictions", ref stream_ShapeFile_FieldNames.fldTurnRstr, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 3, 'C', fht, ref cautions);
                    CheckFields("TMC", ref stream_ShapeFile_FieldNames.fldTMC, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'C', fht, ref cautions);
                    CheckFields("RGNODE", ref stream_ShapeFile_FieldNames.fldRGNODE, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'C', fht, ref cautions);
                    CheckFields("ACC_MASK", ref stream_ShapeFile_FieldNames.fldACCMask, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'C', fht, ref cautions);
                    CheckFields("ATTR", ref stream_ShapeFile_FieldNames.fldAttr, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'C', fht, ref cautions);
                    CheckFields("MaxWeight", ref stream_ShapeFile_FieldNames.fldMaxWeight, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'F', fht, ref cautions);
                    CheckFields("MaxAxle", ref stream_ShapeFile_FieldNames.fldMaxAxle, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'F', fht, ref cautions);
                    CheckFields("MaxHeight", ref stream_ShapeFile_FieldNames.fldMaxHeight, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'F', fht, ref cautions);
                    CheckFields("MaxWidth", ref stream_ShapeFile_FieldNames.fldMaxWidth, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'F', fht, ref cautions);
                    CheckFields("MaxLength", ref stream_ShapeFile_FieldNames.fldMaxLength, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'F', fht, ref cautions);
                    CheckFields("MinDistance", ref stream_ShapeFile_FieldNames.fldMinDistance, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'F', fht, ref cautions);
                };
            };
            // RESULT
            string outText = "Analyse: \r\n " + dbfFile + "\r\n\r\n";
            if (fieldsOk.Length > 0)
            {
                outText += "Found Fields: \r\n ";
                outText += fieldsOk.Trim().Trim(',') + "\r\n\r\n";
            };            
            //if (fieldsBad.Length > 0)
            //{
            //    outText += "Bad Fields: \r\n ";
            //    outText += (fieldsBad.Trim().Trim(',')) + "\r\n\r\n";
            //};            
            if (fieldsErr.Length > 0)
            {
                outText += "Failed Fields: \r\n ";
                outText += (fieldsBad.Length > 0 ? fieldsErr : fieldsErr.Trim().Trim(',')) + "\r\n\r\n";
            };
            if (cautions.Length > 0)
            {
                outText += "Cautions: \r\n  ";
                outText += cautions.Trim().Trim(',') + "\r\n\r\n";
            };
            if (raiseNoFields)
            {
                outText += "Error: \r\n  ";
                outText += ("Required Fields (" + fieldsEdd.Trim().Trim(',') + ") not Found") + "\r\n\r\n";                
            };
            textBox1.Text = (outText);
        }

        private string PreCheckFCFG(string fileName)
        {
            string fieldCongifDefault = XMLSaved<int>.GetCurrentDir() + @"\default.fldcfg.xml";
            string fieldConfigFileName = Path.GetDirectoryName(fileName) + @"\" + Path.GetFileNameWithoutExtension(fileName) + ".fldcfg.xml";
            string fieldConfigFileDef = Path.GetDirectoryName(fileName) + @"\default.fldcfg.xml";
            if (File.Exists(fieldConfigFileName))
                return fieldConfigFileName;
            else if (File.Exists(fieldConfigFileDef))
                return fieldConfigFileDef;
            else
            {
                File.Copy(fieldCongifDefault, fieldConfigFileDef);
                return fieldConfigFileDef;
            };
        }

        private void PreReadShape(string filename)
        {
            FileStream shapeFileStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            byte[] shapeFileData = new byte[100];
            if (shapeFileStream.Length >= 100)
                shapeFileStream.Read(shapeFileData, 0, shapeFileData.Length);
            shapeFileStream.Close();
            
            int shapetype = readIntLittle(shapeFileData, 32);
            if (shapetype != 3) return;

            double MinX = readDoubleLittle(shapeFileData, 36 + 8 * 0);
            double MinY = readDoubleLittle(shapeFileData, 36 + 8 * 1);
            double MaxX = readDoubleLittle(shapeFileData, 36 + 8 * 2);
            double MaxY = readDoubleLittle(shapeFileData, 36 + 8 * 3);
            double cx = (MinX + MaxX) / 2.0;
            double cy = (MinY + MaxY) / 2.0;
            SelectRegion(piru.PointInRegion(cy, cx));
            
        }

        private void SelectRegion(int regID)
        {
            if (regID < 1) return;
            foreach (object obj in cb.Items)
            {
                NI ni = (NI)obj;
                if (ni.i == regID)
                {
                    cb.SelectedItem = obj;
                    break;
                };
            };
        }

        private int readIntLittle(byte[] data, int pos)
        {
            byte[] bytes = new byte[4];
            bytes[0] = data[pos];
            bytes[1] = data[pos + 1];
            bytes[2] = data[pos + 2];
            bytes[3] = data[pos + 3];
            return BitConverter.ToInt32(bytes, 0);
        }

        private double readDoubleLittle(byte[] data, int pos)
        {
            byte[] bytes = new byte[8];
            bytes[0] = data[pos];
            bytes[1] = data[pos + 1];
            bytes[2] = data[pos + 2];
            bytes[3] = data[pos + 3];
            bytes[4] = data[pos + 4];
            bytes[5] = data[pos + 5];
            bytes[6] = data[pos + 6];
            bytes[7] = data[pos + 7];
            return BitConverter.ToDouble(bytes, 0);
        }

        private string[] ReadDBFFields(string filename, out Hashtable fieldsType)
        {
            string dbffile = filename.Substring(0, filename.Length - 4) + ".dbf";
            FileStream dbfFileStream = new FileStream(dbffile, FileMode.Open, FileAccess.Read);

            // Read File Version
            dbfFileStream.Position = 0;
            int ver = dbfFileStream.ReadByte();

            // Read Records Count
            dbfFileStream.Position = 04;
            byte[] bb = new byte[4];
            dbfFileStream.Read(bb, 0, 4);
            int total_lines = BitConverter.ToInt32(bb, 0);

            // Read DataRecord 1st Position
            dbfFileStream.Position = 8;
            bb = new byte[2];
            dbfFileStream.Read(bb, 0, 2);
            short dataRecord_1st_Pos = BitConverter.ToInt16(bb, 0);
            int FieldsCount = (((bb[0] + (bb[1] * 0x100)) - 1) / 32) - 1;

            // Read DataRecord Length
            dbfFileStream.Position = 10;
            bb = new byte[2];
            dbfFileStream.Read(bb, 0, 2);
            short dataRecord_Length = BitConverter.ToInt16(bb, 0);

            // Read Заголовки Полей
            dbfFileStream.Position = 32;
            string[] Fields_Name = new string[FieldsCount]; // Массив названий полей
            Hashtable fieldsLength = new Hashtable(); // Массив размеров полей
            fieldsType = new Hashtable();   // Массив типов полей
            byte[] Fields_Dig = new byte[FieldsCount];   // Массив размеров дробной части
            int[] Fields_Offset = new int[FieldsCount];    // Массив отступов
            bb = new byte[32 * FieldsCount]; // Описание полей: 32 байтa * кол-во, начиная с 33-го            
            dbfFileStream.Read(bb, 0, bb.Length);
            int FieldsLength = 0;
            for (int x = 0; x < FieldsCount; x++)
            {
                Fields_Name[x] = System.Text.Encoding.Default.GetString(bb, x * 32, 10).TrimEnd(new char[] { (char)0x00 }).ToUpper();
                fieldsType.Add(Fields_Name[x], "" + (char)bb[x * 32 + 11]);
                fieldsLength.Add(Fields_Name[x], (int)bb[x * 32 + 16]);
                Fields_Dig[x] = bb[x * 32 + 17];
                Fields_Offset[x] = 1 + FieldsLength;
                FieldsLength = FieldsLength + (int)fieldsLength[Fields_Name[x]];

                // loadedScript.fieldsType[Fields_Name[x]] == "L" -- System.Boolean
                // loadedScript.fieldsType[Fields_Name[x]] == "D" -- System.DateTime
                // loadedScript.fieldsType[Fields_Name[x]] == "N" -- System.Int32 (FieldDigs[x] == 0) / System.Decimal (FieldDigs[x] != 0)
                // loadedScript.fieldsType[Fields_Name[x]] == "F" -- System.Double
                // loadedScript.fieldsType[Fields_Name[x]] == "C" -- System.String
                // loadedScript.fieldsType[Fields_Name[x]] == def -- System.String
            };
            dbfFileStream.Close();

            return Fields_Name;
        }

        // todo = 0 - поля всегда должны быть (LinkID, GarminType, OSM_ID, OSM_SURFACE, SpeedLimit, OneWay); ошибка если полей нет
        // todo = 1 - желательные поля (OSM_TYPE, OSM_SURFACE, GarminType); нет ошибки если нет полей или не указаны
        // todo = 2 - проверяются, если указаны (ROUTE_LEVEL, ROUTE_SPEED, TMC, RGNODE, ...); ошибка если указаны, но нет полей
        // todo = 3 - желательные поля (Length, Name, TurnRestrictions); ошибка если указаны, но нет полей        
        private void CheckFields(string name, ref string f, List<string> flds, ref string fieldsOk, ref string fieldsBad, ref string fieldsErr, ref string fieldsEdd, ref bool raiseNoFields, byte todo, char fType, Hashtable fTypes, ref string cautions)
        {
            if ((f != null) && (f != String.Empty) && (flds.Contains(f)) && (fType != '0') && ((string)fTypes[f] != fType.ToString()))
                cautions += name + " is not `" + fType.ToString() + "`, ";

            string[] ff = new string[0];
            if ((f != null) && (f != String.Empty)) ff = f.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            bool fCont = false;
            foreach (string fff in ff)
                if (flds.Contains(fff))
                {
                    f = fff;
                    fCont = true;
                    break;
                };

            if (todo == 0)
            {
                if (fCont)
                    fieldsOk += name + ", ";
                else
                {
                    fieldsErr += name + ", ";
                    fieldsEdd += name + ", ";
                    raiseNoFields = true;
                };
            };
            if (todo == 1)
            {
                if (fCont)
                    fieldsOk += name + ", ";
                else
                    fieldsErr += name + ", ";
            };
            if (todo == 2)
            {
                if ((f != null) && (f != String.Empty))
                {
                    if (fCont)
                        fieldsOk += name + ", ";
                    else
                    {
                        fieldsErr += name + ", ";
                        fieldsEdd += name + ", ";
                        raiseNoFields = true;
                    };
                }
                else
                    fieldsBad += name + ", ";
            };
            if (todo == 3)
            {
                if ((f != null) && (f != String.Empty))
                {
                    if (fCont)
                        fieldsOk += name + ", ";
                    else
                    {
                        fieldsErr += name + ", ";
                        fieldsEdd += name + ", ";
                        raiseNoFields = true;
                    };
                }
                else
                    fieldsErr += name + ", ";
            };
        }

        private void cstreg_CheckedChanged(object sender, EventArgs e)
        {
            cb.Enabled = !cstreg.Checked;
            customreg.Enabled = cstreg.Checked;
        }

        private void cb_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnEx.Visible = (piru != null) && (cb.SelectedIndex >= 0) && (piru.RegionsCount > 0);
        }

        private void btnEx_Click(object sender, EventArgs e)
        {
            if (cb.SelectedIndex < 0) return;
            if (piru == null) return;            

            if (piru.RegionsCount < 1) return;
            NI ni = ((NI)cb.SelectedItem);
            int rid = -1;
            string rnm = "";
            for (int i = 0; i < piru.RegionsCount; i++)
                if (piru.RegionsIDs[i] == ni.i)
                {
                    rid = i;
                    rnm = ni.n;
                };
            if (rid < 0) return;
            int sval = 0;

            if (InputBox.QueryListBox("Извлечение границ", "Выберите что хотите извлечь:", new string[] { rnm, "*** ВСЕ РЕГИОНЫ ***" }, ref sval) != DialogResult.OK) return;

            int km = 0, rfk = 0;
            if (InputBox.QueryNumberBox("Увеличить зону", "На сколько километров увеличить зону", ref rfk, 0, 500) == DialogResult.OK)
                km = rfk;

            if (sval == 0)
            {
                dkxce.Route.Regions.PointInRegionUtils.Polygon poly = piru.GetRegionPoly(rid);
                if (poly == null) return;                

                SaveFileDialog sfd = new SaveFileDialog();
                sfd.DefaultExt = ".shp";
                sfd.Filter = "Shape Files (*.shp)|*.shp";
                sfd.Title = "Export Bounds";
                if (sfd.ShowDialog() == DialogResult.OK)
                    ExtractPoly(poly, sfd.FileName, km);
                sfd.Dispose();
            }
            else
            {
                string path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory);
                if(InputBox.QueryDirectoryBox("Выберите папку","Папка для извелчения границ:", ref path) != DialogResult.OK) return;
                KMZRebuilder.WaitingBoxForm wfb = new KMZRebuilder.WaitingBoxForm("Извлечение границ", "Загрузка...", this);
                wfb.Show();
                for (int i = 0; i < piru.RegionsCount; i++)
                {
                    dkxce.Route.Regions.PointInRegionUtils.Polygon poly = piru.GetRegionPoly(i);
                    string fName = path + @"\" + String.Format("{0} - {1}.shp", piru.RegionsIDs[i], piru.RegionsNames[i]);
                    try
                    {
                        ExtractPoly(poly, fName, km);
                    }
                    catch (Exception ex)
                    {
                        wfb.Hide();
                        if (MessageBox.Show(ex.Message + "\r\n\r\nПродолжить?", "Error", MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
                            wfb.Show();
                        else
                            break;
                    };
                    wfb.Text = String.Format("Обработано {0} регионов из {1} - {2:0.00}%",i+1, piru.RegionsCount, ((double)(i+1))/((double)piru.RegionsCount)*100.0 );
                };
                wfb.Hide();
                wfb = null;
                MessageBox.Show("Готово", "Извлечение границ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
        }

        private void ExtractPoly(dkxce.Route.Regions.PointInRegionUtils.Polygon poly, string fileName, int enlargeKm)
        {            
            string fext = Path.GetExtension(fileName);

            string box = fileName.Replace(fext, "_[box].shp");
            string ext = fileName.Replace(fext, "_[ext].shp");

            string kml = fileName.Replace(fext, ".kml");
            string kmb = fileName.Replace(fext, "_[box].kml");
            string kme = fileName.Replace(fext, "_[ext].kml");
            
            string bbb = fileName.Replace(fext, "_[box].txt");
            string bbe = fileName.Replace(fext, "_[ext].txt");

            Save2Shape(fileName, poly.points);
            Save2KML(kml, poly.points);

            Save2TextUrl(bbb, Poly2TextUrl(poly.box));            
            Save2Shape(box, poly.box);            
            Save2KML(kmb, poly.box);

            if (enlargeKm > 0)
            {
                double[] enl = Enlarge(poly.box, enlargeKm);
                Save2TextUrl(bbe, Poly2TextUrl(enl));
                Save2Shape(ext, enl);
                Save2KML(kme, enl);
            };
        }

        private double[] Enlarge(double[] box, double dist_in_km)
        {
            double[] res = new double[4];
            double lon_min = box[0];
            double lat_min = box[1];
            double lon_max = box[2];
            double lat_max = box[3];
            double d_buttom = 1.0 / (dkxce.Route.Classes.Utils.GetLengthMetersA(lat_min - 1, (lon_min + lon_max) / 2, lat_min, (lon_min + lon_max) / 2, false) / 1000.0) * dist_in_km;
            double d_top = 1.0 / (dkxce.Route.Classes.Utils.GetLengthMetersA(lat_max, (lon_min + lon_max) / 2, lat_max + 1, (lon_min + lon_max) / 2, false) / 1000.0) * dist_in_km;
            double d_left = 1.0 / (dkxce.Route.Classes.Utils.GetLengthMetersA((lat_min + lat_max) / 2, lon_min - 1, (lat_min + lat_max) / 2, lon_min, false) / 1000.0) * dist_in_km;
            double d_right = 1.0 / (dkxce.Route.Classes.Utils.GetLengthMetersA((lat_min + lat_max) / 2, lon_max, (lat_min + lat_max) / 2, lon_max + 1, false) / 1000.0) * dist_in_km;
            res = new double[4] { box[0] - d_buttom, box[1] - d_left, box[2] + d_top, box[3] + d_right };
            return res;
        }

        public static string Poly2TextUrl(double[] box)
        {
            PointF[] pass = new PointF[4] { new PointF((float)box[0], (float)box[1]), new PointF((float)box[2], (float)box[1]), new PointF((float)box[2], (float)box[3]), new PointF((float)box[0], (float)box[3]) };
            return Poly2TextUrl(pass);
        }

        public static string Poly2TextUrl(PointF[] poly)
        {
            // [BBIKE_EXTRACT_LINK] //
            double xmin = double.MaxValue;
            double ymin = double.MaxValue;
            double xmax = double.MinValue;
            double ymax = double.MinValue;

            string pll = "";
            for (int i = 0; i < poly.Length; i++)
            {
                xmin = Math.Min(xmin, poly[i].X);
                ymin = Math.Min(ymin, poly[i].Y);
                xmax = Math.Max(xmax, poly[i].X);
                ymax = Math.Max(ymax, poly[i].Y);
                if (pll.Length > 0)
                    pll += "|";
                pll += String.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0},{1}", poly[i].X, poly[i].Y);
            };

            string url = "https://extract.bbbike.org/";
            url += String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "?sw_lng={0}&sw_lat={1}&ne_lng={2}&ne_lat={3}",
                xmin, ymin, xmax, ymax);
            url += "&format=mapsforge-osm.zip";
            url += "&coords=" + pll;
            url += "&city=Noname";
            url += "&lang=en";

            return url;
        }

        public static void Save2TextUrl(string filename, string url)
        {
            if (filename == null) return;

            FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs, Encoding.ASCII);
            sw.WriteLine("[BBIKE_EXTRACT_LINK]");
            sw.WriteLine(url);
            sw.Close();
            fs.Close();
        }

        public static void Save2Shape(string filename, double[] box)
        {
            PointF[] pass = new PointF[4] { new PointF((float)box[0], (float)box[1]), new PointF((float)box[2], (float)box[1]), new PointF((float)box[2], (float)box[3]), new PointF((float)box[0], (float)box[3]) };
            Save2Shape(filename, pass);
        }

        public static void Save2Shape(string filename, dkxce.Route.Classes.PointF[] poly)
        {
            PointF[] pass = new PointF[poly.Length];
            for (int i = 0; i < poly.Length; i++)
                pass[i] = new PointF(poly[i].X, poly[i].Y);
            Save2Shape(filename, pass);
        }

        public static void Save2Shape(string filename, PointF[] poly)
        {
            double xmin = double.MaxValue;
            double ymin = double.MaxValue;
            double xmax = double.MinValue;
            double ymax = double.MinValue;

            for (int i = 0; i < poly.Length; i++)
            {
                xmin = Math.Min(xmin, poly[i].X);
                ymin = Math.Min(ymin, poly[i].Y);
                xmax = Math.Max(xmax, poly[i].X);
                ymax = Math.Max(ymax, poly[i].Y);
            };

            List<byte> arr = new List<byte>();
            arr.AddRange(Convert(BitConverter.GetBytes((int)9994), false)); // File Code
            arr.AddRange(new byte[20]);                                    // Not used
            arr.AddRange(Convert(BitConverter.GetBytes((int)((100 + 8 + 48 + 16 * poly.Length) / 2)), false)); // File_Length / 2
            arr.AddRange(Convert(BitConverter.GetBytes((int)1000), true)); // Version 1000
            arr.AddRange(Convert(BitConverter.GetBytes((int)5), true)); // Polygon Type
            arr.AddRange(Convert(BitConverter.GetBytes((double)xmin), true));
            arr.AddRange(Convert(BitConverter.GetBytes((double)ymin), true));
            arr.AddRange(Convert(BitConverter.GetBytes((double)xmax), true));
            arr.AddRange(Convert(BitConverter.GetBytes((double)ymax), true));
            arr.AddRange(new byte[32]); // end of header

            arr.AddRange(Convert(BitConverter.GetBytes((int)1), false)); // rec number
            arr.AddRange(Convert(BitConverter.GetBytes((int)((48 + 16 * poly.Length) / 2)), false));// rec_length / 2
            arr.AddRange(Convert(BitConverter.GetBytes((int)5), true)); // rec type polygon
            arr.AddRange(Convert(BitConverter.GetBytes((double)xmin), true));
            arr.AddRange(Convert(BitConverter.GetBytes((double)ymin), true));
            arr.AddRange(Convert(BitConverter.GetBytes((double)xmax), true));
            arr.AddRange(Convert(BitConverter.GetBytes((double)ymax), true));
            arr.AddRange(Convert(BitConverter.GetBytes((int)1), true)); // 1 part
            arr.AddRange(Convert(BitConverter.GetBytes((int)poly.Length), true)); // 4 points
            arr.AddRange(Convert(BitConverter.GetBytes((int)0), true)); // start at 0 point

            for (int i = 0; i < poly.Length; i++)
            {
                arr.AddRange(Convert(BitConverter.GetBytes((double)poly[i].X), true)); // point 0 x
                arr.AddRange(Convert(BitConverter.GetBytes((double)poly[i].Y), true)); // point 0 y
            };

            FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
            fs.Write(arr.ToArray(), 0, arr.Count);
            fs.Close();
        }

        public static void Save2KML(string filename, double[] box)
        {
            PointF[] pass = new PointF[4] { new PointF((float)box[0], (float)box[1]), new PointF((float)box[2], (float)box[1]), new PointF((float)box[2], (float)box[3]), new PointF((float)box[0], (float)box[3]) };
            Save2KML(filename, pass);
        }

        public static void Save2KML(string filename, dkxce.Route.Classes.PointF[] poly)
        {
            PointF[] pass = new PointF[poly.Length];
            for (int i = 0; i < poly.Length; i++)
                pass[i] = new PointF(poly[i].X, poly[i].Y);
            Save2KML(filename, pass);
        }

        public static void Save2KML(string filename, PointF[] poly)
        {
            FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8);
            sw.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sw.WriteLine("<kml xmlns=\"http://www.opengis.net/kml/2.2\"><Document>");
            sw.WriteLine("<Placemark><name>My Polygon</name>");
            sw.Write("<Polygon><extrude>1</extrude><outerBoundaryIs><LinearRing><coordinates>");
            foreach (PointF p in poly)
                sw.Write(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1},0 ", p.X, p.Y));
            sw.WriteLine("</coordinates></LinearRing></outerBoundaryIs></Polygon></Placemark>");
            sw.WriteLine("</Document>");
            sw.WriteLine("</kml>");
            sw.Close();
            fs.Close();
        }

        public static byte[] Convert(byte[] ba, bool bigEndian)
        {
            if (BitConverter.IsLittleEndian != bigEndian) Array.Reverse(ba);
            return ba;
        }

        private void shapesBBox2RegionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(@"..\TOOLS\ShapesBBox2Regions.exe");
            }
            catch { };
        }

        private void mergeShapesMergerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(@"..\TOOLS\ShapesMerger.exe");
            }
            catch { };
        }

        private void extractShapesPolygonsExtractorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(@"..\TOOLS\ShapesPolygonsExtractor.exe");
            }
            catch { };
        }

        private void button1_Click(object sender, EventArgs e)
        {
            contextMenuStrip1.Show(button1, new Point(0, button1.Height));
        }
    }

    public struct NI
    {
        public string n;
        public int i;
        public NI(string n, int i)
        {
            this.n = n;
            this.i = i;
        }
        public override string ToString()
        {
            return n;
        }
    }

    [Serializable]
    public class BatchParams
    {
        public string Graphs = "GRAPHS";
        public string Regions = "Regions";
        //public string RGNodes = "RGNodes";
        public string RGWays = "RGWays";
    }
}