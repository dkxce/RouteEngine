using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Net;
using System.Text;
using System.Xml;

namespace nmsRoutesWebTest
{
    class TestProgram
    {
        // 0:HTTP(through nmsServices much slower) or 
        // 1:TCP(faster) 
        // 2:Remoting
        // 3:TCPXML
        static byte http_0_tcp_1_remoting_2 = 1;         

        static int threadCount = 5;

        // IF TCP
        static string ip = "127.0.0.1"; //"192.168.0.18" "127.0.0.1"
        static int port = 7755;

        // IF HTTP
        // "http://maps.navicom.ru:82/nms/sroute.ashx?k={k}&f=xml&x={x}&y={y}&i=0";
        // "http://maps.navicom.ru/nms/sroute.ashx?k={k}&f=xml&x={x}&y={y}&i=0";
        // "http://192.168.0.18/nms/sroute.ashx?k={k}&f=xml&x={x}&y={y}&i=0";
        // "http://localhost/nms/sroute.ashx?k={k}&f=xml&x={x}&y={y}&i=0";
        static string url = "http://127.0.0.1:8080/nms/sroute.ashx?k={k}&f=xml&x={x}&y={y}&i=0";
        static string key = "TEST";
                
        //static double[] lat = new double[] { 56.352, 55.6053 }; // MOSCOW
        //static double[] lon = new double[] { 37.530, 37.620 };
        static double[] lat = new double[] { 52.927, 52.488 }; // LIPETSK
        static double[] lon = new double[] { 38.93, 39.90 };

        static bool run = true;
        static bool loop = true;
        static int thrRun = 0;

        static Random rnd = new Random();
        static System.Threading.Mutex mtx = new System.Threading.Mutex();

        // nmsRoutesWebTest.exe <licenseKey> <http/tcp/remoting/tcpxml> <server:127.0.0.1/http..> <threads:5> <lip/msk>
        static void Main(string[] args)
        {
            //// GetNearRoad

            ////HTTP
            // n - names
            //string pu = "http://localhost:8080/nms/snearroad.ashx?k=TEST&n=0&x=39.566334145326486,39.609832763671875,37.39,49.49,37.15,37.38&y=52.61555643344044,52.61555643344044,55.45,57.82,55.47,55.45";
            //HttpWebRequest wr = (HttpWebRequest)HttpWebRequest.Create(pu);
            //wr.Timeout = 120 * 1000;
            //HttpWebResponse res = (HttpWebResponse)wr.GetResponse();
            //Stream rs = res.GetResponseStream();
            //StreamReader sr = new StreamReader(rs);
            //string xml = sr.ReadToEnd();
            //sr.Close();
            //rs.Close();
            //res.Close();

            //// REMOTING
            ////dkxce.Route.ISolver.IRoute ic = dkxce.Route.ISolver.IRouteClient.Connect(ip, port);
            ////dkxce.Route.ISolver.RNearRoad[] ir = ic.GetNearRoad(new double[] { 52.61555643344044, 52.61555643344044, 55.45, 57.82, 55.47, 55.45 }, new double[] { 39.566334145326486, 39.609832763671875, 37.39, 49.49, 37.15, 37.38 }, false);

            ////TCP
            //dkxce.Route.ISolver.RouteClient rc = new dkxce.Route.ISolver.RouteClient("127.0.0.1", 7755, "NONE");
            //dkxce.Route.ISolver.RNearRoad[] nr = rc.GetNearRoad(new double[] { 52.61555643344044, 52.61555643344044, 55.45, 57.82, 55.47, 55.45 }, new double[] { 39.566334145326486, 39.609832763671875, 37.39, 49.49, 37.15, 37.38 }, false);
            //string txt = rc.GetNearRoadXML(new double[] { 52.61555643344044, 52.61555643344044, 55.45, 57.82, 55.47, 55.45 }, new double[] { 39.566334145326486, 39.609832763671875, 37.39, 49.49, 37.15, 37.38 }, false);

            //nmsRoutesDirectCall.nmsRouteClient drc = new nmsRoutesDirectCall.nmsRouteClient(ip, port);
            //string drctxt = drc.GetNearRoadXML(key, new double[] { 52.61555643344044, 52.61555643344044, 55.45, 57.82, 55.47, 55.45 }, new double[] { 39.566334145326486, 39.609832763671875, 37.39, 49.49, 37.15, 37.38 }, false);
            
            //return;


            //byte[] ba = GetReqStruct("1a2b3c4d", new string[] { "Лазер", "Боре", "Хер", "Обрезал" }, new double[] { 55.45, 55.46, 55.47, 55.48 }, new double[] { 37.39, 37.310, 37.311, 37.312 }, DateTime.Parse("11.11.2011"), 0x01, new int[] { 10, 11, 24 });
            //dkxce.Route.ISolver.RAsk aks = ParseReqStruct(ba);
            //return;

            if (args != null)
            {
                if (args.Length > 0)
                    key = args[0];
                if (args.Length > 1)
                {
                    if (args[1] == "http") http_0_tcp_1_remoting_2 = 0;
                    if (args[1] == "tcp") http_0_tcp_1_remoting_2 = 1;
                    if (args[1] == "remoting") http_0_tcp_1_remoting_2 = 2;
                    if (args[1] == "tcpxml") http_0_tcp_1_remoting_2 = 3;
                };
                if (args.Length > 2)
                {
                    ip = args[2];
                    url = args[2];
                };
                if (args.Length > 3)
                    threadCount = Convert.ToInt32(args[3]);
                if (args.Length > 4)
                {
                    if (args[4] == "msk")
                    {
                        lat = new double[] { 56.352, 55.6053 }; // MOSCOW
                        lon = new double[] { 37.530, 37.620 };
                    };
                    if (args[4] == "lip")
                    {
                        lat = new double[] { 52.927, 52.488 };  // LIPETSK
                        lon = new double[] { 38.93, 39.90 };
                    };
                };
            };

            Console.WriteLine("nmsRoutesWebTest");
            Console.WriteLine("Usage: nmsRoutesWebTest.exe [licenseKey] [http/tcp/tcpxml/remoting] [server:127.0.0.1/http...] [threads:5] [lip/msk]");
            System.Threading.Thread.Sleep(2000);
            Console.WriteLine("Key: " + key);
            Console.Write("Start with " + threadCount.ToString() + " ");
            if (http_0_tcp_1_remoting_2 == 0) Console.Write("HTTP");
            if (http_0_tcp_1_remoting_2 == 1) Console.Write("TCP");
            if (http_0_tcp_1_remoting_2 == 2) Console.Write("Remoting");
            if (http_0_tcp_1_remoting_2 == 3) Console.Write("TCPXML");
            Console.WriteLine(" threads");
            if (http_0_tcp_1_remoting_2 == 0)
                Console.WriteLine("Location: " + url);
            else
                Console.WriteLine("Location: tcp://" + ip + ":" + port.ToString() + "/" + (http_0_tcp_1_remoting_2 == 1 ? "TCP" : "Remoting"));
            Console.WriteLine();
            Console.WriteLine("Press ENTER to pause");
            

            // Remove the 2 connection limit in WebClient
            System.Net.ServicePointManager.DefaultConnectionLimit = threadCount + 2;

            for (int i = 0; i < threadCount; i++)
            {
                System.Threading.Thread thr;
                if (http_0_tcp_1_remoting_2 == 0)
                    thr = new System.Threading.Thread(LoopHTTP);
                else
                    thr = new System.Threading.Thread(LoopTCP);
                thr.Start(i);
            };
            
            while (loop)
            {
                Console.ReadLine();
                run = false;
                Console.Write("Do you really want to exit? Yes/No: ");
                string ex = Console.ReadLine();
                if (ex.ToLower() == "yes")
                    loop = false;
                else
                    Console.WriteLine("continue...");
                run = true;
            };
            Console.WriteLine("wait, stopping...");
            while(thrRun > 0)
                System.Threading.Thread.Sleep(200);
            Console.WriteLine("Press Enter to Exit");
            Console.ReadLine();
        }

        // TCP Thread
        public static void LoopTCP(object threadID)
        {
            System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Highest;
            thrRun++;
            Console.WriteLine("TCP Thread " + threadID.ToString() + " started");
            while (loop)
            {                
                DateTime beg = DateTime.Now;
                try
                {
                    dkxce.Route.ISolver.RStop[] rs = new dkxce.Route.ISolver.RStop[2];
                    rs[0] = new dkxce.Route.ISolver.RStop("START", GetYY(), GetXX());
                    rs[1] = new dkxce.Route.ISolver.RStop("FINISH", GetYY(), GetXX());
                    dkxce.Route.ISolver.RResult rr = new dkxce.Route.ISolver.RResult(rs);
                     dkxce.Route.ISolver.RNearRoad[] nr = new dkxce.Route.ISolver.RNearRoad[0];

                    if (http_0_tcp_1_remoting_2 == 1) // tcp
                    {
                        dkxce.Route.ISolver.RouteClient rc = new dkxce.Route.ISolver.RouteClient(ip, port, key);
                        rr = rc.GetRoute(rs, beg, 0x01, null);
                        // string xml = rc.GetRouteXML(rs, beg, 0x01, null);
                        // xml += "";
                    }
                    if (http_0_tcp_1_remoting_2 == 2) // remoting
                    {
                        dkxce.Route.ISolver.IRoute rc = dkxce.Route.ISolver.IRouteClient.Connect(ip, port);
                        rr = rc.GetRoute(rs, beg, 0x01, null);
                    };
                    if (http_0_tcp_1_remoting_2 == 3) // tcpxml 
                    {
                        nmsRoutesDirectCall.nmsRouteClient rc = new nmsRoutesDirectCall.nmsRouteClient(ip, port);
                        string xml = rc.GetRouteXML(key, new string[] { "START", "FINISH" }, new double[] { GetYY(), GetYY() }, new double[] { GetXX(), GetXX() }, beg, 0x01);
                        rr = new dkxce.Route.ISolver.RResult(rs);
                        XmlDocument xd = new XmlDocument();
                        xd.LoadXml(xml);
                        XmlNode xn = xd.SelectSingleNode("Route/driveLength");
                        try { rr.driveLength = Convert.ToDouble(xn.InnerText); } catch { };
                        try { rr.driveLength = Convert.ToDouble(xn.InnerText.Replace(".",",")); } catch { };
                        rr.LastError = xd.SelectSingleNode("Route/LastError").InnerText;
                    };


                    TimeSpan ts = DateTime.Now.Subtract(beg);
                    
                    while (!run)
                        System.Threading.Thread.Sleep(1);
                    if (http_0_tcp_1_remoting_2 == 1) Console.Write("TCP");
                    if (http_0_tcp_1_remoting_2 == 2) Console.Write("Rem");
                    if (http_0_tcp_1_remoting_2 == 3) Console.Write("TCPXML");
                    if (http_0_tcp_1_remoting_2 == 11) Console.Write("TCP R");
                    if (http_0_tcp_1_remoting_2 == 12) Console.Write("Rem R");
                    if (http_0_tcp_1_remoting_2 == 13) Console.Write("TCPXML R");
                    if(http_0_tcp_1_remoting_2 < 10)
                        Console.WriteLine(" Thread " + threadID.ToString() + " (" + ts.TotalSeconds.ToString() + "s) Length: " + rr.driveLength.ToString() + "m" + " " + (rr.driveLength == 0 ? rr.LastError : ""));
                    else
                        Console.WriteLine(" Thread " + threadID.ToString() + " (" + ts.TotalSeconds.ToString() + "s) " + nr[0].lat.ToString()+" " + nr[0].lon.ToString());

                    if (rr.LastError != String.Empty)
                    {
                        mtx.WaitOne();
                        FileStream fs = new FileStream(GetCurrentDir() + @"\calcerr.txt", FileMode.OpenOrCreate);
                        fs.Position = fs.Length;
                        byte[] txt = System.Text.Encoding.GetEncoding(1251).GetBytes(DateTime.Now.ToString("yyyyMMdd HHmmss fff")+" "+rr.LastError + "\r\n");
                        fs.Write(txt, 0, txt.Length);
                        fs.Close();
                        mtx.ReleaseMutex();
                    };
                }
                catch (Exception ex)
                {
                    TimeSpan ts = DateTime.Now.Subtract(beg);
                    Console.Write(DateTime.Now.ToString("yyyyMMdd HHmmss fff")+" ");
                    while (!run)
                        System.Threading.Thread.Sleep(1);
                    if (http_0_tcp_1_remoting_2 == 1) Console.Write("TCP");
                    if (http_0_tcp_1_remoting_2 == 2) Console.Write("Rem");
                    if (http_0_tcp_1_remoting_2 == 3) Console.Write("TCPXML");
                    Console.WriteLine(" Thread " + threadID.ToString() + " (" + ts.TotalSeconds.ToString() + "s) ERROR: " + ex.ToString());
                    mtx.WaitOne();
                    FileStream fs = new FileStream(GetCurrentDir() + @"\errlog.txt", FileMode.OpenOrCreate);
                    fs.Position = fs.Length;
                    byte[] txt = System.Text.Encoding.GetEncoding(1251).GetBytes(ex.ToString() + "\r\n");
                    fs.Write(txt, 0, txt.Length);
                    fs.Close();
                    mtx.ReleaseMutex();
                };

                System.Threading.Thread.Sleep(1);
            };
            Console.WriteLine("TCP Thread " + threadID.ToString() + " stopped");
            thrRun--;
        }

        // HTTP Thread
        public static void LoopHTTP(object threadID)
        {
            System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Highest;
            thrRun++;
            Console.WriteLine("HTTP Thread "+threadID.ToString()+" started");
            while (loop)
            {
                string query = url.Replace("{k}", key).Replace("{x}", GetX()).Replace("{y}", GetY());
                DateTime beg = DateTime.Now;
                try
                {                    
                    HttpWebRequest wr = (HttpWebRequest)HttpWebRequest.Create(query);
                    wr.Timeout = 120 * 1000;
                    HttpWebResponse res = (HttpWebResponse)wr.GetResponse();
                    Stream rs = res.GetResponseStream();
                    StreamReader sr = new StreamReader(rs);
                    string xml = sr.ReadToEnd();
                    sr.Close();
                    rs.Close();
                    res.Close();
                    TimeSpan ts = DateTime.Now.Subtract(beg);
                    XmlDocument xd = new XmlDocument();
                    try
                    {
                        xd.LoadXml(xml);
                        string innerText = "";
                        if (xml.IndexOf("Route") > 0)
                            innerText = xd.SelectSingleNode("Route/driveLength").InnerText;
                        else if (xml.IndexOf("ArrayOfNearRoad") > 0)
                            innerText = xd.SelectSingleNode("ArrayOfNearRoad/NearRoad/distance").InnerText;
                        else if (xml.IndexOf("SearchRecs") > 0)
                            innerText = xd.SelectSingleNode("SearchRecs/total").InnerText;
                        while (!run)
                            System.Threading.Thread.Sleep(1);
                        if (innerText != "")
                            Console.WriteLine("HTTP Thread " + threadID.ToString() + " " + res.StatusCode.ToString() + " (" + ts.TotalSeconds.ToString() + "s) Length: " + innerText + "m");
                        else
                            Console.WriteLine("HTTP Thread " + threadID.ToString() + " " + res.StatusCode.ToString() + " (" + ts.TotalSeconds.ToString() + "s): " + (xml.IndexOf("\r\n") > 0 ? xml.Substring(0, xml.IndexOf("\r\n")) : xml.Length.ToString()));
                    }
                    catch
                    {
                        while (!run)
                            System.Threading.Thread.Sleep(1);
                        Console.WriteLine("HTTP Thread " + threadID.ToString() + " " + res.StatusCode.ToString() + " (" + ts.TotalSeconds.ToString() + "s) Parse XML Error : " + (xml.IndexOf("\r\n") > 0 ? xml.Substring(0, xml.IndexOf("\r\n")) : xml.Length.ToString()));
                    };
                }
                catch (Exception ex)
                {
                    TimeSpan ts = DateTime.Now.Subtract(beg);
                    Console.Write(DateTime.Now.ToString("yyyyMMdd HHmmss fff") + " ");
                    while (!run)
                        System.Threading.Thread.Sleep(1);
                    Console.WriteLine("HTTP Thread " + threadID.ToString() + " (" + ts.TotalSeconds.ToString() + "s) ERROR: " + ex.ToString());
                    mtx.WaitOne();
                    FileStream fs = new FileStream(GetCurrentDir()+@"\errlog.txt",FileMode.OpenOrCreate);
                    fs.Position = fs.Length;
                    byte[] txt = System.Text.Encoding.GetEncoding(1251).GetBytes(query.Replace("maps.navicom.ru:82","localhost") + "\r\n");
                    fs.Write(txt, 0, txt.Length);
                    fs.Close();
                    mtx.ReleaseMutex();
                };

                System.Threading.Thread.Sleep(1);
            };
            Console.WriteLine("HTTP Thread " + threadID.ToString() + " stopped");
            thrRun--;
        }       

        static string GetX()
        {
            return GetXX().ToString("0.00000").Replace(",", ".") + "," + GetXX().ToString("0.00000").Replace(",", ".");
        }
        static string GetY()
        {
            return GetYY().ToString("0.00000").Replace(",", ".") + "," + GetYY().ToString("0.00000").Replace(",", ".");
        }

        static double GetXX()
        {
            double d = (lon[0] + (50000 - (double)rnd.Next(10000, 99999)) / 1000000.0);
            return d;
        }
        static double GetYY()
        {
            double d = (lat[0] + (50000 - (double)rnd.Next(10000, 99999)) / 1000000.0);
            return d;
        }

        public static string GetCurrentDir()
        {
            string fname = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase.ToString();
            fname = fname.Replace("file:///", "");
            fname = fname.Replace("/", @"\");
            fname = fname.Substring(0, fname.LastIndexOf(@"\") + 1);
            return fname;
        }

        private static byte[] GetReqStruct(string licenseKey, string[] stopNames, double[] latt, double[] lonn, DateTime startTime, long flags, int[] regions)
        {
            if ((stopNames == null) || (latt == null) || (lonn == null)) return null;
            if ((stopNames.Length != latt.Length) || (stopNames.Length != lonn.Length)) return null;
            if ((stopNames.Length < 2) || ((stopNames.Length > 100))) return null;

            List<byte> ba = new List<byte>();
            ba.Add((byte)licenseKey.Length);
            ba.AddRange(System.Text.Encoding.GetEncoding(1251).GetBytes(licenseKey));
            ba.Add((byte)stopNames.Length);
            for (int i = 0; i < stopNames.Length; i++)
            {
                ba.Add((byte)stopNames[i].Length);
                ba.AddRange(System.Text.Encoding.GetEncoding(1251).GetBytes(stopNames[i]));
                ba.AddRange(BitConverter.GetBytes(latt[i]));
                ba.AddRange(BitConverter.GetBytes(lonn[i]));
            };
            ba.AddRange(BitConverter.GetBytes(startTime.ToOADate()));
            ba.AddRange(BitConverter.GetBytes(flags));
            if (regions.Length == 0)
                ba.Add(0);
            else
            {
                ba.Add((byte)regions.Length);
                for(int i=0;i<regions.Length;i++)
                    ba.Add((byte)regions[i]);
            };
            return ba.ToArray();
        }

        private static dkxce.Route.ISolver.RAsk ParseReqStruct(byte[] data)
        {
            dkxce.Route.ISolver.RAsk ra = new dkxce.Route.ISolver.RAsk();
            ra.licenseKey = System.Text.Encoding.GetEncoding(1251).GetString(data, 1, data[0]);
            int z = 1 + data[0];
            ra.stops = new dkxce.Route.ISolver.RStop[data[z++]];
            for (int i = 0; i < ra.stops.Length; i++)
            {
                byte nl = data[z++];
                string n = System.Text.Encoding.GetEncoding(1251).GetString(data, z, nl);
                z += nl;
                double lat = BitConverter.ToDouble(data, z);
                z += 8;
                double lon = BitConverter.ToDouble(data, z);
                z += 8;
                ra.stops[i] = new dkxce.Route.ISolver.RStop(n, lat, lon);
            };
            ra.startTime = DateTime.FromOADate(BitConverter.ToDouble(data,z));
            z += 8;
            ra.flags = BitConverter.ToUInt32(data, z);
            z += 8;
            ra.RegionsAvailableToUser = new int[data[z++]];
            for (int i = 0; i < ra.RegionsAvailableToUser.Length; i++)
                ra.RegionsAvailableToUser[i] = data[z++];
            return ra;
        }
    }
}
