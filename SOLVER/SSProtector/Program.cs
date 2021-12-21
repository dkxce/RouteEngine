#define noCLIENT

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Xml;

using System.Management;

using dkxce.Route.ServiceSolver;

namespace SSProtector
{
    class Program
    {
        static void Main(string[] args)
        {            
            #if CLIENT 
            string preQ = "";
            string[] infos = new string[] { "Win32_OperatingSystem", "Win32_ComputerSystem", "Win32_Processor",  /*"Win32_LogicalMemoryConfiguration",*/ "Win32_PhysicalMemory", "Win32_DiskPartition" };
            foreach (string info in infos)
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from " + info);
                try
                {
                    foreach (ManagementObject share in searcher.Get())
                    {
                        try
                        {
                            string q = String.Format("{0}: {1}", share["Name"].ToString(), share["Name"].ToString());
                            Console.WriteLine(q);
                            preQ += q + "\r\n";
                        }
                        catch
                        {
                            string q =String.Format("{0}: {1}", share.ToString(), share.ToString());
                            Console.WriteLine(q);
                            preQ += q + "\r\n";
                        };

                        if (share.Properties.Count <= 0)
                            continue;

                        foreach (PropertyData PC in share.Properties)
                            if (PC.Value != null && PC.Value.ToString() != "")
                            {
                                // indexof size //
                                string k = PC.Name.ToString();
                                string v = PC.Value.ToString();
                                if ((k.ToLower().IndexOf("size") >= 0) || (k.ToLower().IndexOf("capacity") >= 0) || (k.ToLower().IndexOf("totalphysicalmemory") >= 0))
                                {
                                    double big = 0;
                                    if (double.TryParse(v, out big))
                                    {
                                        v = ToFileSize(big);
                                    };
                                };
                                string q = String.Format("\t{0}: {1}", k, v);
                                Console.WriteLine(q);
                                preQ += q + "\r\n";
                            };
                    }
                }
                catch { };
                preQ += "\r\n";
            };
            
            /* nmsKEYSYS.exe */
            string cwrl = "";
            cwrl += ("APPVIID:\r\n");
            cwrl += ("\t" + (SYSID._IsVirtual ? "V" : "P") + System.Environment.ProcessorCount.ToString() +  "\r\n");
            cwrl += ("SYSTID:\r\n");
            cwrl += ("\t" + SYSID.GetSystemID(true, true, false, true, false) + "\r\n");
            cwrl += ("DISKCID:\r\n");
            try
            {
                cwrl += ("\t" + SYSID._CurrentDiskID + "\r\n");
            }
            catch { };
            Console.Write(cwrl);
            System.IO.FileStream fs = new FileStream(AppDomain.CurrentDomain.BaseDirectory + @"nmsKEYSYS.log", FileMode.Create, FileAccess.Write);
            System.IO.StreamWriter sw = new StreamWriter(fs);
            sw.Write(preQ);
            sw.Write(cwrl);
            sw.Close();
            fs.Close();
            System.Threading.Thread.Sleep(60000);
#else
            if ((args == null) || (args.Length < 2))
            {
                Console.WriteLine("dkxce.Route.ServiceSolver License Tool");
                Console.WriteLine("methods: ");
                Console.WriteLine("  <filename> genmid   -- add key to exe file for local machine");
                Console.WriteLine("  <filename> addmid MACHINE-ID  -- add key to exe file for specified machine");                
                Console.ReadLine();
                return;
            };

            string licFile = args[0];
            if (args[1] == "genmid")
            {
                string ccode = "dkxce.Route.ServiceSolver.exe";
                string HASH1 = SYSID.GetSystemID(true, true, false, true, false);
                string HASH2 = SYSID.Encrypt(HASH1, ccode);
                //SYSID.SaveFileDate(args[0], HASH2);
                SaveKeyFile(args[0], HASH2);
                Console.WriteLine("ADD 1 HASH " + HASH1);
                return;
            };

            if (args[1] == "addmid")
            {
                string ccode = "dkxce.Route.ServiceSolver.exe";
                string HASH1 = args[2];
                string HASH2 = SYSID.Encrypt(HASH1, ccode);
                //SYSID.SaveFileDate(args[0], HASH2);
                SaveKeyFile(args[0], HASH2);
                Console.WriteLine("ADD 1 HASH " + HASH1);
                return;
            };
#endif
        }

        private static byte[] PREFIX = new byte[] { 0x72, 0x65, 0x67, 0x62, 0x6b, 0x62, 0x64, 0x76, 0x66, 0x75, 0x66, 0x70, 0x62, 0x79, 0x74, 0x68, 0x74, 0x70, 0x62, 0x79, 0x6a, 0x64, 0x65, 0x2e, 0x70, 0x62, 0x79, 0x65 };

        private static void SaveKeyFile(string fileName, string hash)
        {
            if (File.Exists(fileName)) File.Delete(fileName);
            File.Copy(AppDomain.CurrentDomain.BaseDirectory + @"dkxce.Route.ServiceSolver.exe", fileName);
            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite);
            int i = 0;
            int B = fs.ReadByte();
            while (i < PREFIX.Length)
            {
                if (B != PREFIX[i++]) i = 0;
                B = fs.ReadByte();
            };
            fs.Position++;
            byte[] ba = System.Text.Encoding.ASCII.GetBytes(hash);
            fs.Write(ba, 0, ba.Length);
            ba = new byte[512 - ba.Length];
            fs.Write(ba, 0, ba.Length);
            fs.Close();
        }

        public static string ToFileSize(double value)
        {
            string[] suffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
            for (int i = 0; i < suffixes.Length; i++)
            {
                if (value <= (Math.Pow(1024, i + 1)))
                {
                    return ThreeNonZeroDigits(value /
                        Math.Pow(1024, i)) +
                        " " + suffixes[i];
                }
            }

            return ThreeNonZeroDigits(value /
                Math.Pow(1024, suffixes.Length - 1)) +
                " " + suffixes[suffixes.Length - 1];
        }

        private static string ThreeNonZeroDigits(double value)
        {
            if (value >= 100)
            {
                // No digits after the decimal.
                return value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (value >= 10)
            {
                // One digit after the decimal.
                return value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                // Two digits after the decimal.
                return value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
