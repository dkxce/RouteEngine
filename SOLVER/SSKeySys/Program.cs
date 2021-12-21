using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Management;


namespace SSKeySys
{
    class Program
    {
        static void Main(string[] args)
        {
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
            
            /* SSKEYSYS.exe */
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
            System.IO.FileStream fs = new FileStream(AppDomain.CurrentDomain.BaseDirectory + @"SSKeySys.log", FileMode.Create, FileAccess.Write);
            System.IO.StreamWriter sw = new StreamWriter(fs);
            sw.Write(preQ);
            sw.Write(cwrl);
            sw.Close();
            fs.Close();
            System.Threading.Thread.Sleep(60000);
            
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
