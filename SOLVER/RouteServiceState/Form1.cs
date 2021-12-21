using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Management;
using System.Windows.Forms;
using System.ServiceProcess;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using Microsoft.Win32.SafeHandles;
using System.Runtime.ConstrainedExecution;

namespace RouteServiceState
{
    public partial class RMSVForm : Form
    {
        string sName = "dkxce.Route.ServiceSolver";
        string sFile = Path.GetFullPath(XMLSaved<int>.GetCurrentDir() + @"\..\Service\dkxce.Route.ServiceSolver.exe");

        bool isadmin = false;
        bool stopped = true;
        TimeSpan[] ts = new TimeSpan[11];
        DateTime[] dt = new DateTime[11];

        public RMSVForm()
        {
            InitializeComponent();
            isadmin = PrivilegeManager.IsUserAnAdmin();
            Text += String.Format(" {0}", isadmin ? "[Admin]" : "[NoAdmin]");
        }

        private void RMSVForm_Load(object sender, EventArgs e)
        {
            dataGridView1.Rows.Add(new object[] { "Route.ServiceSolver", "Wait...", "", "", "", "", "", "", "", "", "Start", "Stop" });
            timer1_Tick(sender, e);
            timer1.Enabled = true;
            LoadConfigs();
        }

        private void LoadConfigs()
        {
            string path = Path.GetFullPath(XMLSaved<int>.GetCurrentDir() + @"\..\Service\");
            string[] files = Directory.GetFiles(path, "*.xml", SearchOption.TopDirectoryOnly);
            Array.Sort<string>(files);
            foreach(string file in files)
            {
                string desc = "";
                try { SvcConfig cfg = XMLSaved<SvcConfig>.Load(file); desc = cfg.http_description; } catch { };
                if (!String.IsNullOrEmpty(desc))
                {
                    if((desc == "Default") || (desc == "Default Configuration"))
                        cfgs.Items.Insert(0, new ItemInfo(file, desc, true));
                    else
                        cfgs.Items.Add(new ItemInfo(file, desc));
                }
                else
                {
                    if (Path.GetFileName(file) == "dkxce.Route.ServiceSolver.xml")
                        cfgs.Items.Insert(0, new ItemInfo(file, "Default"));
                    else
                        cfgs.Items.Add(new ItemInfo(file));
                };
            };
            try
            {
                path = Path.GetFullPath(XMLSaved<int>.GetCurrentDir() + @"\..\");
                files = Directory.GetFiles(path, "*.xml", SearchOption.TopDirectoryOnly);
                foreach (string file in files)
                {
                    //
                    string desc = "";
                    try { SvcConfig cfg = XMLSaved<SvcConfig>.Load(file); desc = cfg.http_description; }
                    catch { };
                    if (!String.IsNullOrEmpty(desc))
                        cfgs.Items.Add(new ItemInfo(file, desc, @"..\"));
                    else
                        cfgs.Items.Add(new ItemInfo(file, null, @"..\"));

                };
            }
            catch { };
            if(cfgs.Items.Count > 0) cfgs.SelectedIndex = 0;
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            timer1.Interval = (int)(numericUpDown1.Value * 1000);
        }

        private static Process GetProcess(string svcName)
        {
            ManagementObject service = new ManagementObject(@"Win32_service.Name='" + svcName + "'");
            object o = service.GetPropertyValue("ProcessId");
            int processId = (int)((UInt32)o);
            return Process.GetProcessById(processId);
        }

        private void callUpdateStatus(ServiceController sc, int row)
        {
            callUpdateStatus(sc, row, "");
        }

        private void callUpdateStatus(ServiceController sc, int row, string file)
        {
            Process p = null;

            try
            {
                dataGridView1.Rows[row - 1].Cells[1].Value = sc.Status.ToString();
                dataGridView1.Rows[row - 1].Cells[1].Style.ForeColor = Color.Green;
                p = GetProcess(sc.ServiceName);
            }
            catch (InvalidOperationException ex)
            {
                dataGridView1.Rows[row - 1].Cells[1].Value = "ERROR";
                dataGridView1.Rows[row - 1].Cells[2].Value = ex.Message;
            }
            catch (Exception ex)
            {
                dataGridView1.Rows[row - 1].Cells[1].Value = "ERROR";
                dataGridView1.Rows[row - 1].Cells[2].Value = ex.Message;
            };
            if ((p != null) && (p.Id == 0))
            {
                Process[] pcs = Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(file));
                if ((pcs != null) && (pcs.Length > 0))
                {
                    dataGridView1.Rows[row - 1].Cells[1].Value = "Console";
                    dataGridView1.Rows[row - 1].Cells[1].Style.ForeColor = Color.Blue;
                    p = pcs[0];
                };
            };
            if (p == null)
            {
                Process[] pcs = Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(file));
                if ((pcs != null) && (pcs.Length > 0))
                {
                    dataGridView1.Rows[row - 1].Cells[1].Value = "Console";
                    dataGridView1.Rows[row - 1].Cells[1].Style.ForeColor = Color.Blue;
                    p = pcs[0];
                }
                else if (System.IO.File.Exists(file))
                    dataGridView1.Rows[row - 1].Cells[1].Value = "No Service";
                else
                    dataGridView1.Rows[row - 1].Cells[1].Value = "Not Found";
            };

            if ((p != null) && (p.Id > 0))
            {
                string usage = "?";
                if ((ts[row - 1] != null) && (dt[row - 1] != null))
                {
                    TimeSpan dts = p.TotalProcessorTime.Subtract(ts[row - 1]);
                    TimeSpan ddt = DateTime.UtcNow.Subtract(dt[row - 1]);
                    usage = ((int)(Convert.ToDouble(dts.TotalMilliseconds) / Convert.ToDouble(ddt.TotalMilliseconds) * 100)).ToString() + " %";
                };
                ts[row - 1] = p.TotalProcessorTime;
                dt[row - 1] = DateTime.UtcNow;

                dataGridView1.Rows[row - 1].Cells[2].Value = p.Id.ToString();
                dataGridView1.Rows[row - 1].Cells[3].Value = usage;
                dataGridView1.Rows[row - 1].Cells[4].Value = p.HandleCount.ToString();
                dataGridView1.Rows[row - 1].Cells[5].Value = p.Threads.Count.ToString();
                dataGridView1.Rows[row - 1].Cells[6].Value = (p.WorkingSet64 / 1024 / 1024).ToString() + " MB";
                dataGridView1.Rows[row - 1].Cells[7].Value = (p.VirtualMemorySize64 / 1024 / 1024).ToString() + " MB";
                dataGridView1.Rows[row - 1].Cells[8].Value = (p.PrivateMemorySize64 / 1024 / 1024).ToString() + " MB";
                dataGridView1.Rows[row - 1].Cells[9].Value = p.StartTime.ToString();
                dataGridView1.Rows[row - 1].Cells[10].Style.ForeColor = Color.Gray;
                dataGridView1.Rows[row - 1].Cells[11].Style.ForeColor = Color.Black;
            }
            else
            {
                dataGridView1.Rows[row - 1].Cells[1].Style.ForeColor = Color.Maroon;
                dataGridView1.Rows[row - 1].Cells[2].Value = "";
                dataGridView1.Rows[row - 1].Cells[3].Value = "";
                dataGridView1.Rows[row - 1].Cells[4].Value = "";
                dataGridView1.Rows[row - 1].Cells[5].Value = "";
                dataGridView1.Rows[row - 1].Cells[6].Value = "";
                dataGridView1.Rows[row - 1].Cells[7].Value = "";
                dataGridView1.Rows[row - 1].Cells[8].Value = "";
                dataGridView1.Rows[row - 1].Cells[9].Value = "";
                dataGridView1.Rows[row - 1].Cells[10].Style.ForeColor = Color.Black;
                dataGridView1.Rows[row - 1].Cells[11].Style.ForeColor = Color.Gray;
            };
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                callUpdateStatus(new ServiceController(sName), 1, sFile);
            }
            catch  { };
            
            string val = dataGridView1.Rows[0].Cells[1].Value.ToString();
            stopped = val == "Stopped" || val == "No Service";


            panelRun.Enabled = stopped;
            cfgs.Enabled = stopped;
            startC.Enabled = stopped;
            startS.Enabled = stopped;
            btnI.Enabled = stopped && (val == "No Service");
            btnU.Enabled = stopped && (val == "Stopped");            

            GetRouteData();
        }

        private void GetRouteData()
        {
            //FileMappingServer fms = new FileMappingServer();
            //fms.Connect();
            //fms.WriteData("Hello, World!");                        
            //fms.Close();

            if (dataGridView2.RowCount == 0)
                dataGridView2.Rows.Add();

            RouteThreaderInfo rti = new RouteThreaderInfo();
            FileMappingClient fmc = new FileMappingClient();
            try
            {
                fmc.Connect("Global\\dRSSThreads");
                string data = fmc.ReadData();
                if ((data != null) && (data != "") && (data != "null"))
                    rti = XMLSaved<RouteThreaderInfo>.LoadText(data);
            }
            catch (Exception ex) 
            { 
                lut.Text = "No Data " + DateTime.Now.ToString("HH:mm:ss dd.MM.yyyy") + " Error: " + ex.Message;                
            };
            fmc.Close();

            if (String.IsNullOrEmpty(rti.Protocol))
            {
                panel1.Enabled = false;
                dataGridView2.Enabled = false;
                dataGridView3.Enabled = false;
                dataGridView2.Rows.Clear();
                dataGridView3.Rows.Clear();
                return;
            }
            else
            {
                panel1.Enabled = true;
                dataGridView2.Enabled = true;
                dataGridView3.Enabled = true;
            };

            cfnm.Text = rti.confile;

            lProtocol.Text = rti.Protocol;
            lDPool.Text = rti.DynamicPool.ToString();
            lArea.Text = rti.Area;
            lMode.Text = rti.Mode;

            lGRC.Text = rti.GlobalRegionsCache;
            lTRC.Text = rti.ThreadRegionsCache;
            lMST.Text = rti.MaxSolveTime.ToString();
            lMWT.Text = rti.MaxWaitTime.ToString();

            lTCP.Text = rti.config.defPort.ToString();
            lHTTP.Text = rti.config.defHTTP.ToString();
            lHA.Text = rti.config.authorization.ToString();
            lHD.Text = rti.config.http_description;

            ttST.Text = rti.startedAt.ToString("HH:mm:ss dd.MM.yyyy");
            ttNow.Text = DateTime.UtcNow.ToString("HH:mm:ss dd.MM.yyyy");
            ttRun.Text = RunningToString(DateTime.UtcNow, rti.startedAt);

            dataGridView2.Rows[0].Cells[0].Value = rti.ObjectsUsed.ToString();
            dataGridView2.Rows[0].Cells[1].Value = rti.ObjectsIdle.ToString();
            dataGridView2.Rows[0].Cells[2].Value = rti.ThreadsAlive.ToString();
            dataGridView2.Rows[0].Cells[3].Value = rti.ThreadsCounted.ToString();
            dataGridView2.Rows[0].Cells[4].Value = rti.ThreadsMaxAlive.ToString();

            string[] objs = rti.ObjectsData.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
            if (objs.Length != dataGridView3.Rows.Count)
            {
                dataGridView3.Rows.Clear();
                foreach (string obj in objs)
                    dataGridView3.Rows.Add(obj.Split(new string[] { ":" }, StringSplitOptions.None));
            }
            else
            {
                for (int i = 0; i < objs.Length; i++)
                    dataGridView3.Rows[i].SetValues(objs[i].Split(new string[] { ":" }, StringSplitOptions.None));
            };

            lut.Text = DateTime.Now.ToString("HH:mm:ss dd.MM.yyyy");
        }

        private string RunningToString(DateTime till, DateTime since)
        {
            TimeSpan ts = till.Subtract(since);
            return String.Format("{4:00}:{5:00}:{6:00} {3:00}.{1:00}:{0:0000} UTC - {2} weeks", (int)(ts.Days / 365.2425), (int)(ts.Days / 30.436875), (int)ts.TotalDays / 7, ts.Days, ts.Hours, ts.Minutes, ts.Seconds);
        }        

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if ((e.ColumnIndex == 10) || (e.ColumnIndex == 11) && (e.RowIndex == 0))
            {
                string killer = XMLSaved<int>.GetCurrentDir() + @"\nircmd.exe";
                try
                {
                    ServiceController sc = new ServiceController(sName);
                    // START //
                    if (e.ColumnIndex == 10)
                    {
                        Exception err = null;
                        try { sc.Start(); } catch (Exception ex) { err = ex; };
                        if (err != null)
                        {
                            try { ProcessStart(sFile); err = null; } catch { }; 
                        };
                        if (err != null) throw err;
                    };
                    // STOP //
                    if (e.ColumnIndex == 11)
                    {
                        Exception err = null;
                        try { sc.Stop(); } catch (Exception ex) { err = ex; };
                        if (err != null)
                        {
                            Process[] pcs = Process.GetProcessesByName(sName);
                            try { if ((pcs != null) && (pcs.Length > 0)) { pcs[0].Close(); err = null; }; } catch { };
                            try { if ((pcs != null) && (pcs.Length > 0)) { pcs[0].Kill(); err = null; }; } catch { };
                            try { ProcessStart(killer, "killprocess " + sName + ".exe"); err = null; } catch { };                            
                        };
                        if (err != null) throw err;
                    };
                }
                catch (Exception ex) 
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
            };  
        }

        private void ProcessStart(string proc)
        {
            ProcessStartInfo psi = new ProcessStartInfo(proc);
            if (System.Environment.OSVersion.Version.Major >= 6)
            {
                psi.UseShellExecute = true;
                psi.Verb = "runas";
            };
            Process.Start(psi);
        }

        private void ProcessStart(string proc, string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo(proc, args);
            if (System.Environment.OSVersion.Version.Major >= 6)
            {
                psi.UseShellExecute = true;
                psi.Verb = "runas";
            };
            Process.Start(psi);
        }

        private void startC_Click(object sender, EventArgs e)
        {
            if (cfgs.SelectedIndex < 0) return;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(sFile, ((ItemInfo)cfgs.SelectedItem).fName);
                if (System.Environment.OSVersion.Version.Major >= 6)
                {
                    psi.UseShellExecute = true;
                    psi.Verb = "runas";
                }
                psi.WorkingDirectory = Path.GetDirectoryName(sFile);
                Process.Start(psi);
            }
            catch { };
        }

        private void btnI_Click(object sender, EventArgs e)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(sFile, "/i");
                psi.WorkingDirectory = Path.GetDirectoryName(sFile);
                Process.Start(psi);
            }
            catch { };
        }

        private void btnU_Click(object sender, EventArgs e)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(sFile, "/u");
                psi.WorkingDirectory = Path.GetDirectoryName(sFile);
                Process.Start(psi);
            }
            catch { };
        }

        private void startS_Click(object sender, EventArgs e)
        {
            if (cfgs.SelectedIndex < 0) return;
            try
            {
                ServiceController sc = new ServiceController(sName);
                sc.Start(new string[] { ((ItemInfo)cfgs.SelectedItem).fName });
            }
            catch { };
        }

        private void cfgs_SelectedIndexChanged(object sender, EventArgs e)
        {
           // do nothing
        }

        private void cfgs_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (!cfgs.Enabled) return;
            if (e.Index == -1) return;

            ComboBox combo = ((ComboBox)sender);
            ItemInfo ii = (ItemInfo)cfgs.Items[e.Index];

            e.DrawBackground();

            string res = "";
            if (String.IsNullOrEmpty(ii.fAddit)) res = Path.GetFileName(ii.fName);
            else res = String.Format("{0}  ({1})", Path.GetFileName(ii.fName), ii.fAddit);
            if (!String.IsNullOrEmpty(ii.fSub)) res += String.Format(" at {0}", ii.fSub);
            
            string fn = Path.GetFileNameWithoutExtension(ii.fName);
            string fe = Path.GetExtension(ii.fName);
            string fc = " (";
            string fx = ") ";
            int w = 0;

            e.Graphics.DrawString(fn, e.Font, new SolidBrush(ii.fDef ? Color.DarkViolet : e.ForeColor), new PointF(e.Bounds.Left + w, e.Bounds.Top));
            w += (int)e.Graphics.MeasureString(fn, e.Font, 0, StringFormat.GenericTypographic).Width;
            e.Graphics.DrawString(fe, e.Font, new SolidBrush(Color.Gray), new PointF(e.Bounds.Left + w, e.Bounds.Top));
            w += (int)e.Graphics.MeasureString(fe, e.Font, 0, StringFormat.GenericTypographic).Width;
            if (!String.IsNullOrEmpty(ii.fAddit))
            {
                e.Graphics.DrawString(fc, e.Font, new SolidBrush(Color.Silver), new PointF(e.Bounds.Left + w, e.Bounds.Top));
                w += (int)e.Graphics.MeasureString(fc, e.Font, 0, StringFormat.GenericTypographic).Width;
                e.Graphics.DrawString(ii.fAddit, e.Font, new SolidBrush(ii.fDef ? Color.DarkViolet : ((e.State & DrawItemState.Focus) == DrawItemState.Focus ? e.ForeColor : Color.Navy)), new PointF(e.Bounds.Left + w, e.Bounds.Top));
                w += (int)e.Graphics.MeasureString(ii.fAddit.Trim(), e.Font, 0, StringFormat.GenericTypographic).Width;
                e.Graphics.DrawString(fx, e.Font, new SolidBrush(Color.Silver), new PointF(e.Bounds.Left + w, e.Bounds.Top));
                w += (int)e.Graphics.MeasureString(fx, e.Font, 0, StringFormat.GenericTypographic).Width;
            };
            
            e.DrawFocusRectangle();
        }

        private void RMSVForm_Shown(object sender, EventArgs e)
        {
            if (!isadmin)
                MessageBox.Show("This application requeries admin rights\r\nЭто приложение требует админских прав\r\n\r\nПожалуйста, запустите приложение с правами администратора", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

            this.l23.Text = "Logical Processors: " + Environment.ProcessorCount.ToString();            
            System.Threading.Thread thr = new System.Threading.Thread(GetSysID);
            thr.Start();
        }

        private void GetSysID()
        {
            string SysID = "";
            try
            {
                SysID = SYSID.GetSystemID(true, true, false, true, false);
            }
            catch { };
            this.Invoke(new SetSysIDDelegate(SetSysID), SysID);
        }

        private delegate void SetSysIDDelegate(string sysid);
        private void SetSysID(string sysid)
        {
            this.sysidbox.Text = sysid;            
        }

        private void eBtn_Click(object sender, EventArgs e)
        {
            if (cfgs.SelectedIndex < 0) return;
            string[] notepads = new string[] { "notepad++.exe", "akelpad.exe", "notepad.exe" };
            int tri = 0;
            while (tri < 3)
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(notepads[tri++], ((ItemInfo)cfgs.SelectedItem).fName);
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                    break;
                }
                catch { };
            };
        }      
    }

    public class PrivilegeManager
    {
        [DllImport("shell32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsUserAnAdmin();

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool OpenProcessToken(
            IntPtr ProcessHandle,
            UInt32 DesiredAccess, out IntPtr TokenHandle);

        private static uint STANDARD_RIGHTS_REQUIRED = 0x000F0000;
        private static uint STANDARD_RIGHTS_READ = 0x00020000;
        private static uint TOKEN_ASSIGN_PRIMARY = 0x0001;
        private static uint TOKEN_DUPLICATE = 0x0002;
        private static uint TOKEN_IMPERSONATE = 0x0004;
        private static uint TOKEN_QUERY = 0x0008;
        private static uint TOKEN_QUERY_SOURCE = 0x0010;
        private static uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private static uint TOKEN_ADJUST_GROUPS = 0x0040;
        private static uint TOKEN_ADJUST_DEFAULT = 0x0080;
        private static uint TOKEN_ADJUST_SESSIONID = 0x0100;
        private static uint TOKEN_READ = (STANDARD_RIGHTS_READ | TOKEN_QUERY);
        private static uint TOKEN_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED | TOKEN_ASSIGN_PRIMARY |
            TOKEN_DUPLICATE | TOKEN_IMPERSONATE | TOKEN_QUERY | TOKEN_QUERY_SOURCE |
            TOKEN_ADJUST_PRIVILEGES | TOKEN_ADJUST_GROUPS | TOKEN_ADJUST_DEFAULT |
            TOKEN_ADJUST_SESSIONID);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool LookupPrivilegeValue(
            string lpSystemName,
            string lpName,
            out LUID lpLuid);

        #region Privelege constants

        public const string SE_ASSIGNPRIMARYTOKEN_NAME = "SeAssignPrimaryTokenPrivilege";
        public const string SE_AUDIT_NAME = "SeAuditPrivilege";
        public const string SE_BACKUP_NAME = "SeBackupPrivilege";
        public const string SE_CHANGE_NOTIFY_NAME = "SeChangeNotifyPrivilege";
        public const string SE_CREATE_GLOBAL_NAME = "SeCreateGlobalPrivilege";
        public const string SE_CREATE_PAGEFILE_NAME = "SeCreatePagefilePrivilege";
        public const string SE_CREATE_PERMANENT_NAME = "SeCreatePermanentPrivilege";
        public const string SE_CREATE_SYMBOLIC_LINK_NAME = "SeCreateSymbolicLinkPrivilege";
        public const string SE_CREATE_TOKEN_NAME = "SeCreateTokenPrivilege";
        public const string SE_DEBUG_NAME = "SeDebugPrivilege";
        public const string SE_ENABLE_DELEGATION_NAME = "SeEnableDelegationPrivilege";
        public const string SE_IMPERSONATE_NAME = "SeImpersonatePrivilege";
        public const string SE_INC_BASE_PRIORITY_NAME = "SeIncreaseBasePriorityPrivilege";
        public const string SE_INCREASE_QUOTA_NAME = "SeIncreaseQuotaPrivilege";
        public const string SE_INC_WORKING_SET_NAME = "SeIncreaseWorkingSetPrivilege";
        public const string SE_LOAD_DRIVER_NAME = "SeLoadDriverPrivilege";
        public const string SE_LOCK_MEMORY_NAME = "SeLockMemoryPrivilege";
        public const string SE_MACHINE_ACCOUNT_NAME = "SeMachineAccountPrivilege";
        public const string SE_MANAGE_VOLUME_NAME = "SeManageVolumePrivilege";
        public const string SE_PROF_SINGLE_PROCESS_NAME = "SeProfileSingleProcessPrivilege";
        public const string SE_RELABEL_NAME = "SeRelabelPrivilege";
        public const string SE_REMOTE_SHUTDOWN_NAME = "SeRemoteShutdownPrivilege";
        public const string SE_RESTORE_NAME = "SeRestorePrivilege";
        public const string SE_SECURITY_NAME = "SeSecurityPrivilege";
        public const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
        public const string SE_SYNC_AGENT_NAME = "SeSyncAgentPrivilege";
        public const string SE_SYSTEM_ENVIRONMENT_NAME = "SeSystemEnvironmentPrivilege";
        public const string SE_SYSTEM_PROFILE_NAME = "SeSystemProfilePrivilege";
        public const string SE_SYSTEMTIME_NAME = "SeSystemtimePrivilege";
        public const string SE_TAKE_OWNERSHIP_NAME = "SeTakeOwnershipPrivilege";
        public const string SE_TCB_NAME = "SeTcbPrivilege";
        public const string SE_TIME_ZONE_NAME = "SeTimeZonePrivilege";
        public const string SE_TRUSTED_CREDMAN_ACCESS_NAME = "SeTrustedCredManAccessPrivilege";
        public const string SE_UNDOCK_NAME = "SeUndockPrivilege";
        public const string SE_UNSOLICITED_INPUT_NAME = "SeUnsolicitedInputPrivilege";
        #endregion

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public UInt32 LowPart;
            public Int32 HighPart;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hHandle);

        public const UInt32 SE_PRIVILEGE_ENABLED_BY_DEFAULT = 0x00000001;
        public const UInt32 SE_PRIVILEGE_ENABLED = 0x00000002;
        public const UInt32 SE_PRIVILEGE_REMOVED = 0x00000004;
        public const UInt32 SE_PRIVILEGE_USED_FOR_ACCESS = 0x80000000;

        [StructLayout(LayoutKind.Sequential)]
        public struct TOKEN_PRIVILEGES
        {
            public UInt32 PrivilegeCount;
            public LUID Luid;
            public UInt32 Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public UInt32 Attributes;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AdjustTokenPrivileges(
            IntPtr TokenHandle,
           [MarshalAs(UnmanagedType.Bool)]bool DisableAllPrivileges,
           ref TOKEN_PRIVILEGES NewState,
           UInt32 Zero,
           IntPtr Null1,
           IntPtr Null2);

        /// <summary>
        /// Меняет привилегию
        /// </summary>
        /// <param name="PID">ID процесса</param>
        /// <param name="privelege">Привилегия</param>
        public static void SetPrivilege(
            IntPtr PID,
            string privilege)
        {
            IntPtr hToken;
            LUID luidSEDebugNameValue;
            TOKEN_PRIVILEGES tkpPrivileges;

            if (!OpenProcessToken(PID, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out hToken))
            {
                throw new Exception("Произошла ошибка при выполнении OpenProcessToken(). Код ошибки "
                    + Marshal.GetLastWin32Error());
            }

            if (!LookupPrivilegeValue(null, privilege, out luidSEDebugNameValue))
            {
                CloseHandle(hToken);
                throw new Exception("Произошла ошибка при выполнении LookupPrivilegeValue(). Код ошибки "
                    + Marshal.GetLastWin32Error());
            }

            tkpPrivileges.PrivilegeCount = 1;
            tkpPrivileges.Luid = luidSEDebugNameValue;
            tkpPrivileges.Attributes = SE_PRIVILEGE_ENABLED;

            if (!AdjustTokenPrivileges(hToken, false, ref tkpPrivileges, 0, IntPtr.Zero, IntPtr.Zero))
            {
                throw new Exception("Произошла ошибка при выполнении LookupPrivilegeValue(). Код ошибки :"
                    + Marshal.GetLastWin32Error());
            }
            CloseHandle(hToken);
        }
    }

    internal class FileMappingClient
    {
        internal const uint ViewOffset = 0;
        internal const uint ViewSize = 15 * 1024;
        private bool connected = false;
        public bool Connected
        {
            get { return connected; }
        }

        private SafeFileMappingHandle hMapFile = null;
        private IntPtr pView = IntPtr.Zero;

        /// <summary>
        ///     Global\\SampleMap
        ///     http://msdn.microsoft.com/en-us/library/aa366537.aspx
        /// </summary>
        /// <param name="FullMapName"></param>
        public void Connect(string FullMapName)
        {
            try { PrivilegeManager.SetPrivilege(Process.GetCurrentProcess().Handle, PrivilegeManager.SE_CREATE_GLOBAL_NAME); } catch { };
            
            try
            {
                hMapFile = NativeMethod.OpenFileMapping(
                    FileMapAccess.FILE_MAP_READ_ACCESS,    // Read access
                    false,                          // Do not inherit the name
                    FullMapName                     // File mapping name
                    );

                if (hMapFile.IsInvalid)
                {
                    connected = false;
                    hMapFile.Close();
                    return;
                };

                pView = NativeMethod.MapViewOfFile(
                    hMapFile,                       // Handle of the map object
                    FileMapAccess.FILE_MAP_READ_ACCESS,    // Read access
                    0,                              // High-order DWORD of file offset 
                    ViewOffset,                     // Low-order DWORD of file offset
                    ViewSize                        // Byte# to map to view
                    );

                if (pView == IntPtr.Zero) throw new Win32Exception();

                connected = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("The process throws the error: {0}", ex.Message);
            };
        }

        public string ReadData()
        {
            if (connected)
                return Marshal.PtrToStringUni(pView);
            else
                return null;
        }

        public void Close()
        {
            if (hMapFile != null)
            {
                if (pView != IntPtr.Zero)
                {
                    NativeMethod.UnmapViewOfFile(pView);
                    pView = IntPtr.Zero;
                }
                hMapFile.Close();
                hMapFile = null;
            };
            connected = false;
        }


        #region Native API Signatures and Types

        /// <summary>
        /// Access rights for file mapping objects
        /// http://msdn.microsoft.com/en-us/library/aa366559.aspx
        /// </summary>
        [Flags]
        public enum FileMapAccess
        {
            FILE_MAP_COPY = 0x0001,
            FILE_MAP_WRITE = 0x0002,
            FILE_MAP_READ = 0x0004,
            SECTION_MAP_EXECUTE = 0x0008,
            SECTION_EXTEND_SIZE = 0x0010,
            FILE_MAP_ALL_ACCESS = 0x000F001F,
            FILE_MAP_READ_ACCESS = 0x000F000C
        }


        /// <summary>
        /// Represents a wrapper class for a file mapping handle. 
        /// </summary>
        [SuppressUnmanagedCodeSecurity,
        HostProtection(SecurityAction.LinkDemand, MayLeakOnAbort = true)]
        internal sealed class SafeFileMappingHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
            private SafeFileMappingHandle()
                : base(true)
            {
            }

            [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
            public SafeFileMappingHandle(IntPtr handle, bool ownsHandle)
                : base(ownsHandle)
            {
                base.SetHandle(handle);
            }

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success),
            DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool CloseHandle(IntPtr handle);

            protected override bool ReleaseHandle()
            {
                return CloseHandle(base.handle);
            }
        }


        /// <summary>
        /// The class exposes Windows APIs used in this code sample.
        /// </summary>
        [SuppressUnmanagedCodeSecurity]
        internal class NativeMethod
        {
            /// <summary>
            /// Opens a named file mapping object.
            /// </summary>
            /// <param name="dwDesiredAccess">
            /// The access to the file mapping object. This access is checked against 
            /// any security descriptor on the target file mapping object.
            /// </param>
            /// <param name="bInheritHandle">
            /// If this parameter is TRUE, a process created by the CreateProcess 
            /// function can inherit the handle; otherwise, the handle cannot be 
            /// inherited.
            /// </param>
            /// <param name="lpName">
            /// The name of the file mapping object to be opened.
            /// </param>
            /// <returns>
            /// If the function succeeds, the return value is an open handle to the 
            /// specified file mapping object.
            /// </returns>
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern SafeFileMappingHandle OpenFileMapping(
                FileMapAccess dwDesiredAccess, bool bInheritHandle, string lpName);


            /// <summary>
            /// Maps a view of a file mapping into the address space of a calling
            /// process.
            /// </summary>
            /// <param name="hFileMappingObject">
            /// A handle to a file mapping object. The CreateFileMapping and 
            /// OpenFileMapping functions return this handle.
            /// </param>
            /// <param name="dwDesiredAccess">
            /// The type of access to a file mapping object, which determines the 
            /// protection of the pages.
            /// </param>
            /// <param name="dwFileOffsetHigh">
            /// A high-order DWORD of the file offset where the view begins.
            /// </param>
            /// <param name="dwFileOffsetLow">
            /// A low-order DWORD of the file offset where the view is to begin.
            /// </param>
            /// <param name="dwNumberOfBytesToMap">
            /// The number of bytes of a file mapping to map to the view. All bytes 
            /// must be within the maximum size specified by CreateFileMapping.
            /// </param>
            /// <returns>
            /// If the function succeeds, the return value is the starting address 
            /// of the mapped view.
            /// </returns>
            [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr MapViewOfFile(
                SafeFileMappingHandle hFileMappingObject,
                FileMapAccess dwDesiredAccess,
                uint dwFileOffsetHigh,
                uint dwFileOffsetLow,
                uint dwNumberOfBytesToMap);


            /// <summary>
            /// Unmaps a mapped view of a file from the calling process's address 
            /// space.
            /// </summary>
            /// <param name="lpBaseAddress">
            /// A pointer to the base address of the mapped view of a file that 
            /// is to be unmapped.
            /// </param>
            /// <returns></returns>
            [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);
        }

        #endregion
    }

    internal class FileMappingServer
    {
        private SafeFileMappingHandle hMapFile = null;
        private IntPtr pView = IntPtr.Zero;

        private const string FullMapName = "Global\\dRSSThreads";
        private const uint MapSize = 65536;
        private const uint ViewOffset = 0;
        private const uint ViewSize = 15 * 1024;

        private bool connected = false;
        public bool Connected { get { return connected; } }

        public void Connect()
        {
            try { PrivilegeManager.SetPrivilege(Process.GetCurrentProcess().Handle, PrivilegeManager.SE_CREATE_GLOBAL_NAME); } catch { };

            try
            {
                SECURITY_ATTRIBUTES sa = SECURITY_ATTRIBUTES.Empty;
                hMapFile = NativeMethod.CreateFileMapping(INVALID_HANDLE_VALUE, ref sa, FileProtection.PAGE_READWRITE, 0, MapSize, FullMapName);

                if (hMapFile.IsInvalid) throw new Win32Exception();

                //IntPtr sidPtr = IntPtr.Zero;
                //SECURITY_INFORMATION sFlags = SECURITY_INFORMATION.Owner;
                //System.Security.Principal.NTAccount user = new System.Security.Principal.NTAccount("P1R4T3\\Harris");
                //System.Security.Principal.SecurityIdentifier sid = (System.Security.Principal.SecurityIdentifier)user.Translate(typeof(System.Security.Principal.SecurityIdentifier));
                //ConvertStringSidToSid(sid.ToString(), ref sidPtr);
                SetNamedSecurityInfoW(FullMapName, SE_OBJECT_TYPE.SE_KERNEL_OBJECT, SECURITY_INFORMATION.Dacl, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                pView = NativeMethod.MapViewOfFile(hMapFile, FileMapAccess.FILE_MAP_ALL_ACCESS, 0, ViewOffset, ViewSize);
                if (pView == IntPtr.Zero) throw new Win32Exception();
                connected = true;
                byte[] bMessage = Encoding.Unicode.GetBytes("BEGINS" + '\0');
                Marshal.Copy(bMessage, 0, pView, bMessage.Length);
            }
            catch (Exception ex)
            {
                XMLSaved<int>.Add2SysLog(ex.Message);
            };
        }

        public void WriteData(RouteThreaderInfo message)
        {
            if (connected)
            {
                byte[] bMessage = Encoding.Unicode.GetBytes(XMLSaved<RouteThreaderInfo>.Save(message) + '\0');
                Marshal.Copy(bMessage, 0, pView, bMessage.Length);
            };
        }

        public void Close()
        {
            if (hMapFile != null)
            {
                if (pView != IntPtr.Zero)
                {
                    NativeMethod.UnmapViewOfFile(pView);
                    pView = IntPtr.Zero;
                };
                hMapFile.Close();
                hMapFile = null;
            };
            connected = false;
        }

        #region Native API Signatures and Types

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SetNamedSecurityInfoW(String pObjectName, SE_OBJECT_TYPE ObjectType, SECURITY_INFORMATION SecurityInfo, IntPtr psidOwner, IntPtr psidGroup, IntPtr pDacl, IntPtr pSacl);

        [DllImport("Advapi32.dll", SetLastError = true)]
        private static extern bool ConvertStringSidToSid(String StringSid, ref IntPtr Sid);

        private enum SE_OBJECT_TYPE
        {
            SE_UNKNOWN_OBJECT_TYPE = 0,
            SE_FILE_OBJECT,
            SE_SERVICE,
            SE_PRINTER,
            SE_REGISTRY_KEY,
            SE_LMSHARE,
            SE_KERNEL_OBJECT,
            SE_WINDOW_OBJECT,
            SE_DS_OBJECT,
            SE_DS_OBJECT_ALL,
            SE_PROVIDER_DEFINED_OBJECT,
            SE_WMIGUID_OBJECT,
            SE_REGISTRY_WOW64_32KEY
        }

        [Flags]
        private enum SECURITY_INFORMATION : uint
        {
            Owner = 0x00000001,
            Group = 0x00000002,
            Dacl = 0x00000004,
            Sacl = 0x00000008,
            ProtectedDacl = 0x80000000,
            ProtectedSacl = 0x40000000,
            UnprotectedDacl = 0x20000000,
            UnprotectedSacl = 0x10000000
        }


        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;

            public static SECURITY_ATTRIBUTES Empty
            {
                get
                {
                    SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES();
                    sa.nLength = sizeof(int) * 2 + IntPtr.Size;
                    sa.lpSecurityDescriptor = IntPtr.Zero;
                    sa.bInheritHandle = 0;
                    return sa;
                }
            }
        }

        /// <summary>
        /// Memory Protection Constants
        /// http://msdn.microsoft.com/en-us/library/aa366786.aspx
        /// </summary>
        [Flags]
        public enum FileProtection : uint
        {
            NONE = 0x00,
            PAGE_NOACCESS = 0x01,
            PAGE_READONLY = 0x02,
            PAGE_READWRITE = 0x04,
            PAGE_WRITECOPY = 0x08,
            PAGE_EXECUTE = 0x10,
            PAGE_EXECUTE_READ = 0x20,
            PAGE_EXECUTE_READWRITE = 0x40,
            PAGE_EXECUTE_WRITECOPY = 0x80,
            PAGE_GUARD = 0x100,
            PAGE_NOCACHE = 0x200,
            PAGE_WRITECOMBINE = 0x400,
            SEC_FILE = 0x800000,
            SEC_IMAGE = 0x1000000,
            SEC_RESERVE = 0x4000000,
            SEC_COMMIT = 0x8000000,
            SEC_NOCACHE = 0x10000000
        }


        /// <summary>
        /// Access rights for file mapping objects
        /// http://msdn.microsoft.com/en-us/library/aa366559.aspx
        /// </summary>
        [Flags]
        public enum FileMapAccess
        {
            FILE_MAP_COPY = 0x0001,
            FILE_MAP_WRITE = 0x0002,
            FILE_MAP_READ = 0x0004,
            SECTION_MAP_EXECUTE = 0x0008,
            SECTION_EXTEND_SIZE = 0x0010,
            FILE_MAP_ALL_ACCESS = 0x000F001F,
            FILE_MAP_READ_ACCESS = 0x000F000C
        }


        /// <summary>
        /// Represents a wrapper class for a file mapping handle. 
        /// </summary>
        [SuppressUnmanagedCodeSecurity,
        HostProtection(SecurityAction.LinkDemand, MayLeakOnAbort = true)]
        internal sealed class SafeFileMappingHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
            private SafeFileMappingHandle()
                : base(true)
            {
            }

            [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
            public SafeFileMappingHandle(IntPtr handle, bool ownsHandle)
                : base(ownsHandle)
            {
                base.SetHandle(handle);
            }

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success),
            DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool CloseHandle(IntPtr handle);

            protected override bool ReleaseHandle()
            {
                return CloseHandle(base.handle);
            }
        }


        internal static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);


        /// <summary>
        /// The class exposes Windows APIs used in this code sample.
        /// </summary>
        [SuppressUnmanagedCodeSecurity]
        internal class NativeMethod
        {
            /// <summary>
            /// Creates or opens a named or unnamed file mapping object for a 
            /// specified file.
            /// </summary>
            /// <param name="hFile">
            /// A handle to the file from which to create a file mapping object.
            /// </param>
            /// <param name="lpAttributes">
            /// A pointer to a SECURITY_ATTRIBUTES structure that determines 
            /// whether a returned handle can be inherited by child processes.
            /// </param>
            /// <param name="flProtect">
            /// Specifies the page protection of the file mapping object. All 
            /// mapped views of the object must be compatible with this 
            /// protection.
            /// </param>
            /// <param name="dwMaximumSizeHigh">
            /// The high-order DWORD of the maximum size of the file mapping 
            /// object.
            /// </param>
            /// <param name="dwMaximumSizeLow">
            /// The low-order DWORD of the maximum size of the file mapping 
            /// object.
            /// </param>
            /// <param name="lpName">
            /// The name of the file mapping object.
            /// </param>
            /// <returns>
            /// If the function succeeds, the return value is a handle to the 
            /// newly created file mapping object.
            /// </returns>
            [DllImport("Kernel32.dll", SetLastError = true)]
            public static extern SafeFileMappingHandle CreateFileMapping(
                IntPtr hFile,
                ref SECURITY_ATTRIBUTES lpAttributes,
                FileProtection flProtect,
                uint dwMaximumSizeHigh,
                uint dwMaximumSizeLow,
                string lpName);


            /// <summary>
            /// Maps a view of a file mapping into the address space of a calling
            /// process.
            /// </summary>
            /// <param name="hFileMappingObject">
            /// A handle to a file mapping object. The CreateFileMapping and 
            /// OpenFileMapping functions return this handle.
            /// </param>
            /// <param name="dwDesiredAccess">
            /// The type of access to a file mapping object, which determines the 
            /// protection of the pages.
            /// </param>
            /// <param name="dwFileOffsetHigh">
            /// A high-order DWORD of the file offset where the view begins.
            /// </param>
            /// <param name="dwFileOffsetLow">
            /// A low-order DWORD of the file offset where the view is to begin.
            /// </param>
            /// <param name="dwNumberOfBytesToMap">
            /// The number of bytes of a file mapping to map to the view. All bytes 
            /// must be within the maximum size specified by CreateFileMapping.
            /// </param>
            /// <returns>
            /// If the function succeeds, the return value is the starting address 
            /// of the mapped view.
            /// </returns>
            [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr MapViewOfFile(
                SafeFileMappingHandle hFileMappingObject,
                FileMapAccess dwDesiredAccess,
                uint dwFileOffsetHigh,
                uint dwFileOffsetLow,
                uint dwNumberOfBytesToMap);


            /// <summary>
            /// Unmaps a mapped view of a file from the calling process's address 
            /// space.
            /// </summary>
            /// <param name="lpBaseAddress">
            /// A pointer to the base address of the mapped view of a file that 
            /// is to be unmapped.
            /// </param>
            /// <returns></returns>
            [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);
        }

        #endregion
    }

    internal class AnotherProcessMemory
    {
        private const int PROCESS_WM_READ = 0x0010;
        private const int PROCESS_VM_WRITE = 0x0020;
        private const int PROCESS_VM_OPERATION = 0x0008;

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);


        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesWritten);


        private int _ProcessID = 0;
        private IntPtr _ProcessHandle = IntPtr.Zero;

        public static int GetCurrentProcessId() { return Process.GetCurrentProcess().Id; }

        public AnotherProcessMemory(int ProcessID)
        {
            this._ProcessID = ProcessID;
        }

        public AnotherProcessMemory(string ProcessName)
        {
            this._ProcessID = Process.GetProcessesByName(ProcessName)[0].Id;
        }

        public void Open(System.IO.FileAccess mode)
        {
            if (mode == System.IO.FileAccess.Read)
                _ProcessHandle = OpenProcess(PROCESS_WM_READ, false, _ProcessID);
            else if (mode == System.IO.FileAccess.Write)
                _ProcessHandle = OpenProcess(PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, _ProcessID);
            else
                _ProcessHandle = OpenProcess(PROCESS_WM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, _ProcessID);
        }

        public void Close()
        {
            if (IsOpen)
            {
                CloseHandle(_ProcessHandle);
                _ProcessHandle = IntPtr.Zero;
            };
        }

        public bool IsOpen
        {
            get
            {
                return _ProcessHandle != IntPtr.Zero;
            }
        }

        public bool Read(int BaseAddress, byte[] buffer, int bufferSize, ref int noOfBytesRead)
        {
            if (!IsOpen)
                throw new Exception("Process is not opened!");
            return ReadProcessMemory((int)_ProcessHandle, BaseAddress, buffer, bufferSize, ref noOfBytesRead);
        }

        public bool Write(int BaseAddress, byte[] buffer, int bufferSize, ref int noOfBytesWritten)
        {
            if (!IsOpen)
                throw new Exception("Process is not opened!");
            return WriteProcessMemory((int)_ProcessHandle, BaseAddress, buffer, bufferSize, ref noOfBytesWritten);
        }

        public bool Read(IntPtr BaseAddress, byte[] buffer, int bufferSize, ref int noOfBytesRead)
        {
            if (!IsOpen)
                throw new Exception("Process is not opened!");
            return ReadProcessMemory((int)_ProcessHandle, (int)BaseAddress, buffer, bufferSize, ref noOfBytesRead);
        }

        public bool Write(IntPtr BaseAddress, byte[] buffer, int bufferSize, ref int noOfBytesWritten)
        {
            if (!IsOpen)
                throw new Exception("Process is not opened!");
            return WriteProcessMemory((int)_ProcessHandle, (int)BaseAddress, buffer, bufferSize, ref noOfBytesWritten);
        }
    }

    public class ItemInfo
    {
        public string fName = null;
        public string fAddit = null;
        public string fSub = null;
        public bool fDef = false;

        public ItemInfo(string fName)
        {
            this.fName = fName;
        }

        public ItemInfo(string fName, string fAddit)
        {
            this.fName = fName;
            this.fAddit = fAddit;
        }

        public ItemInfo(string fName, string fAddit, bool fDef)
        {
            this.fName = fName;
            this.fAddit = fAddit;
            this.fDef = fDef;
        }

        public ItemInfo(string fName, string fAddit, string fSub)
        {
            this.fName = fName;
            this.fAddit = fAddit;
            this.fSub = fSub;
        }

        public ItemInfo(string fName, string fAddit, string fSub, bool fDef)
        {
            this.fName = fName;
            this.fAddit = fAddit;
            this.fSub = fSub;
            this.fDef = fDef;
        }

        public override string ToString()
        {
            string res = "";
            if (String.IsNullOrEmpty(fAddit)) res = Path.GetFileName(fName);
            else res = String.Format("{0}  ({1})", Path.GetFileName(fName), fAddit);
            if (!String.IsNullOrEmpty(fSub)) res += String.Format(" at {0}", fSub);
            return res;
        }
    }

    [Serializable]
    public class RouteThreaderInfo
    {
        public DateTime startedAt;

        public bool DynamicPool;

        public int MaxSolveTime;
        public int MaxWaitTime;

        public int ObjectsUsed;
        public int ObjectsIdle;
        public int ThreadsAlive;
        public int ThreadsCounted;
        public int ThreadsMaxAlive;

        public string Protocol;
        public string Area;
        public string Mode;
        public string GlobalRegionsCache;
        public string ThreadRegionsCache;

        public string ObjectsData;

        public SvcConfig config;
        public string confile;
    }

    [Serializable]
    public class SvcConfig
    {
        public int defPort = 7755;
        public int defHTTP = 80;

        [XmlElement("http.authorization")]
        public bool authorization = false;
        [XmlElement("http.showhost")]
        public bool http_showhost = false;
        [XmlElement("http.showip")]
        public bool http_showip = false;
        [XmlElement("http.description")]
        public string http_description = null;
        [XmlElement("http.html")]
        public string http_html = null;        
    }
}