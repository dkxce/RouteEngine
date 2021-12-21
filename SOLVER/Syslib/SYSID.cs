using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using System.Management;
using System.Security.Cryptography;
using System.Security;

namespace System
{
    public class SYSID
    {
        public static string _BIOSID
        {
            get
            {
                ManagementObjectSearcher mos = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
                ManagementObjectCollection moc = mos.Get();
                string motherBoard = "";
                foreach (ManagementObject mo in moc)
                    motherBoard = (string)mo["SerialNumber"];
                return motherBoard;
            }
        }

        public static string _MotherBoardID
        {
            get
            {
                ManagementObjectSearcher mos = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
                ManagementObjectCollection moc = mos.Get();
                string motherBoard = "";
                foreach (ManagementObject mo in moc)
                    motherBoard = (string)mo["SerialNumber"];
                return motherBoard;
            }
        }

        public static string _ProcessorID
        {
            get
            {
                ManagementObjectSearcher mos = new ManagementObjectSearcher("SELECT * FROM Win32_processor");
                ManagementObjectCollection moc = mos.Get();
                string motherBoard = "";
                foreach (ManagementObject mo in moc)
                    motherBoard = (string)mo["ProcessorID"];
                return motherBoard;
            }
        }

        public static string _DiskCID
        {
            get
            {
                ManagementObject dsk = new ManagementObject("win32_logicaldisk.deviceid=\"C:\"");
                dsk.Get();
                return dsk["VolumeSerialNumber"].ToString();
            }
        }

        public static string _CurrentDiskID
        {
            get
            {
                ManagementObject dsk = new ManagementObject("win32_logicaldisk.deviceid=\"" + AppDomain.CurrentDomain.BaseDirectory.Substring(0, 1) + ":\"");
                dsk.Get();
                return dsk["VolumeSerialNumber"].ToString();
            }
        }
        
        public string GetSysID() { return SYSID.GetSystemID(true, true, false, true, false); }

        public static string GetSystemID(bool BIOS, bool MotherBoard, bool CPU, bool DiskC, bool CurrDisk)
        {
            string code = "";
            try
            {
                if (BIOS) code += "/" + _BIOSID;
            }
            catch { };
            try
            {
                if (MotherBoard) code += "/" + _MotherBoardID;
            }
            catch { };
            try
            {
                if (CPU) code += "/" + _ProcessorID;
            }
            catch { };
            try
            {
                if (DiskC) code += "/" + _DiskCID;
            }
            catch { };
            try
            {
                if (CurrDisk) code += "/" + _CurrentDiskID;
            }
            catch { };

            int codeType = (BIOS ? 1 : 0) + (MotherBoard ? 2 : 0) + (CPU ? 4 : 0) + (DiskC ? 8 : 0) + (CurrDisk ? 0x10 : 0);
            
            //return codeType.ToString("X2") + "-" + 
            
            return GetHash(code);
        }

        private static string GetHash(string s)
        {
            MD5 sec = new MD5CryptoServiceProvider();
            ASCIIEncoding enc = new ASCIIEncoding();
            byte[] bt = enc.GetBytes(s);
            return GetHexString(sec.ComputeHash(bt));
        }

        private static string GetHexString(byte[] bt)
        {
            string s = string.Empty;
            for (int i = 0; i < bt.Length; i++)
            {
                byte b = bt[i];
                int n, n1, n2;
                n = (int)b;
                n1 = n & 15;
                n2 = (n >> 4) & 15;
                if (n2 > 9)
                    s += ((char)(n2 - 10 + (int)'A')).ToString();
                else
                    s += n2.ToString();
                if (n1 > 9)
                    s += ((char)(n1 - 10 + (int)'A')).ToString();
                else
                    s += n1.ToString();
                if ((i + 1) != bt.Length && (i + 1) % 2 == 0) s += "-";
            }
            return s;
        }
    }
}
