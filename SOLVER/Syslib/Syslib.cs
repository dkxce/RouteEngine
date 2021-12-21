//
//      DO NOT BUILD `AnyCPU`
// in AnyCPU build DLL will not work!
//   !!! Build only x86 or x64 !!!
//

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using RGiesecke.DllExport;

using System.Management;
using System.Security.Cryptography;
using System.Security;
using System.Runtime;
using System.ServiceProcess;

namespace System
{
    internal static class UnmanagedExports
    {
        // System::Call 'Syslib.dll::GetSysId(v) t.r0'
        [DllExport("GetSysId", CallingConvention = CallingConvention.Cdecl)]
        static IntPtr GetSysId()
        {
          string res = SYSID.GetSystemID(true, true, false, true, false);
          return Marshal.StringToHGlobalUni(res);
        }

        // System::Call 'Syslib.dll::GetInstallPath(v) t.r0'
        [DllExport("GetInstallPath", CallingConvention = CallingConvention.Cdecl)]
        static IntPtr GetInstallPath()
        {
            long fs = long.MinValue;
            string drv = @"C:\ROUTES\";
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                {
                    if (drive.TotalFreeSpace > fs)
                    {
                        fs = drive.TotalFreeSpace;
                        drv = Path.GetFullPath(drive.RootDirectory.ToString() + @"ROUTES\");
                    };
                }
            };
            return Marshal.StringToHGlobalUni(drv);
        }

        // System::Call 'Syslib.dll::ServiceIsInstalled(v) b.r0'
        [DllExport("ServiceIsInstalled", CallingConvention = CallingConvention.Cdecl)]
        static byte ServiceIsInstalled()
        {
            ServiceController service = null;
            try
            {
                ServiceController[] svcs = ServiceController.GetServices();
                foreach(ServiceController s in svcs)
                    if(s.ServiceName == "dkxce.Route.ServiceSolver")
                        service = s;
            }
            catch (Exception) {};
            return service == null ? (byte)0 : (byte)1;
        }       

        // System::Call 'Syslib.dll::SetPorts(t "File", i 7755, i 8080)'
        [DllExport("SetPorts", CallingConvention = CallingConvention.Cdecl)]
        static void SetPorts(IntPtr f, int tcp, int http)
        {
            string file = Marshal.PtrToStringUni(f);
            if (!File.Exists(file)) return;
            FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, System.Text.Encoding.UTF8);
            string data = sr.ReadToEnd();
            sr.Close();
            fs.Close();
            data = Regex.Replace(data, @">(\d*)</defPort>", ">" + tcp.ToString() + "</defPort>");
            data = Regex.Replace(data, @">(\d*)</defHTTP>", ">" + http.ToString() + "</defHTTP>");
            fs = new FileStream(file, FileMode.Create, FileAccess.Write);
            byte[] arr = System.Text.Encoding.UTF8.GetBytes(data);
            fs.Write(arr, 0, arr.Length);
            fs.Close();
        }        

        // System::Call 'Syslib.dll::GetTCPPort(t "File") i .r0'
        [DllExport("GetTCPPort", CallingConvention = CallingConvention.Cdecl)]
        static int GetTCPPort(IntPtr f)
        {
            string file = Marshal.PtrToStringUni(f);
            if (!File.Exists(file)) return 0;

            FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, System.Text.Encoding.UTF8);
            string data = sr.ReadToEnd();
            sr.Close();
            fs.Close();
            Regex rx = new Regex(@">(\d*)</defPort>");
            Match rc = rx.Match(data);
            if (rc.Success)
                return int.Parse(rc.Groups[1].Value);
            return 0;
        }

        // System::Call 'Syslib.dll::GetHTTPPort(t "File") t .r0'
        [DllExport("GetHTTPPort", CallingConvention = CallingConvention.Cdecl)]
        static int GetHTTPPort(IntPtr f)
        {
            string file = Marshal.PtrToStringUni(f);
            if (!File.Exists(file)) return 0;

            FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, System.Text.Encoding.UTF8);
            string data = sr.ReadToEnd();
            sr.Close();
            fs.Close();
            Regex rx = new Regex(@">(\d*)</defHTTP>");
            Match rc = rx.Match(data);
            if (rc.Success)
                return int.Parse(rc.Groups[1].Value);
            return 0;
        }

        // System::Call 'Syslib.dll::GetProtocol(t "File") t .r0'
        [DllExport("GetProtocol", CallingConvention = CallingConvention.Cdecl)]
        static IntPtr GetProtocol(IntPtr f)
        {
            string file = Marshal.PtrToStringUni(f);
            if (!File.Exists(file)) return Marshal.StringToHGlobalUni("tcp");

            FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, System.Text.Encoding.UTF8);
            string data = sr.ReadToEnd();
            sr.Close();
            fs.Close();
            Regex rx = new Regex(@">([\w\n\r\t\s]*)</defProto>");
            Match rc = rx.Match(data);
            if (rc.Success)
                return Marshal.StringToHGlobalUni(rc.Groups[1].Value.Trim());
            return Marshal.StringToHGlobalUni("tcp");
        }

        // System::Call 'Syslib.dll::GetArea(t "File") t .r0'
        [DllExport("GetArea", CallingConvention = CallingConvention.Cdecl)]
        static IntPtr GetArea(IntPtr f)
        {
            string file = Marshal.PtrToStringUni(f);
            if (!File.Exists(file)) return Marshal.StringToHGlobalUni("one");

            FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, System.Text.Encoding.UTF8);
            string data = sr.ReadToEnd();
            sr.Close();
            fs.Close();
            Regex rx = new Regex(@">([\w\n\r\t\s]*)</defArea>");
            Match rc = rx.Match(data);
            if (rc.Success)
                return Marshal.StringToHGlobalUni(rc.Groups[1].Value.Trim());
            return Marshal.StringToHGlobalUni("one");
        }

        // System::Call 'Syslib.dll::GetOneRegion(t "File") t .r0'
        [DllExport("GetOneRegion", CallingConvention = CallingConvention.Cdecl)]
        static IntPtr GetOneRegion(IntPtr f)
        {
            string file = Marshal.PtrToStringUni(f);
            if (!File.Exists(file)) return Marshal.StringToHGlobalUni("unknown");

            FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, System.Text.Encoding.UTF8);
            string data = sr.ReadToEnd();
            sr.Close();
            fs.Close();
            Regex rx = new Regex(@">([^<]*)</OneRegion>");
            Match rc = rx.Match(data);
            if (rc.Success)
                return Marshal.StringToHGlobalUni(rc.Groups[1].Value.Trim());
            return Marshal.StringToHGlobalUni("unknown");
        }

        // System::Call 'Syslib.dll::SetOneRegion(t "File", t "RegFile")'
        [DllExport("SetOneRegion", CallingConvention = CallingConvention.Cdecl)]
        static void SetOneRegion(IntPtr f, IntPtr r)
        {
            string file = Marshal.PtrToStringUni(f);
            if (!File.Exists(file)) return;
            FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, System.Text.Encoding.UTF8);
            string data = sr.ReadToEnd();
            sr.Close();
            fs.Close();
            data = Regex.Replace(data, @">([^<]*)</OneRegion>", ">\r\t\t" + Marshal.PtrToStringUni(r) + "\r\t</OneRegion>");
            fs = new FileStream(file, FileMode.Create, FileAccess.Write);
            byte[] arr = System.Text.Encoding.UTF8.GetBytes(data);
            fs.Write(arr, 0, arr.Length);
            fs.Close();
        }

        // System::Call 'Syslib.dll::SetModeMap(t "File", t "Mode", t "Map")'
        [DllExport("SetModeMap", CallingConvention = CallingConvention.Cdecl)]
        static void SetModeMap(IntPtr f, IntPtr mode, IntPtr map)
        {
            string file = Marshal.PtrToStringUni(f);
            string proto = Marshal.PtrToStringUni(mode);
            string area = Marshal.PtrToStringUni(map);
            if (proto == "dual(tcp+http)") proto = "dual";
            if (!File.Exists(file)) return;
            FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, System.Text.Encoding.UTF8);
            string data = sr.ReadToEnd();
            sr.Close();
            fs.Close();
            data = Regex.Replace(data, @">([\w\n\r\t\s]*)</defProto>", ">" + proto + "</defProto>");
            data = Regex.Replace(data, @">([\w\n\r\t\s]*)</defArea>", ">" + area + "</defArea>");
            fs = new FileStream(file, FileMode.Create, FileAccess.Write);
            byte[] arr = System.Text.Encoding.UTF8.GetBytes(data);
            fs.Write(arr, 0, arr.Length);
            fs.Close();
        }
    }
}