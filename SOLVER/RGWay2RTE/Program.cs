using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace RGWay2RTE
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("dkxce RGWay 2 RTE Converter");

            // NO ARGS
            if (args.Length == 0)
            {
                Console.WriteLine(@"FORMAT: RGWay2RTE.exe file.rgway.xml file.rte");
                Console.WriteLine(@"FORMAT: RGWay2RTE.exe rte PATH\*.rgway.xml");
                Console.WriteLine(@"FORMAT: RGWay2RTE.exe rte1 PATH\*.rgway.xml");
                Console.WriteLine(@"FORMAT: RGWay2RTE.exe kml PATH\*.rgway.xml");
                Console.WriteLine(@"FORMAT: RGWay2RTE.exe mp PATH\*.rgway.xml");
                Console.WriteLine(@"FORMAT: RGWay2RTE.exe geojson PATH\*.rgway.xml");
                System.Threading.Thread.Sleep(5000);
                return;
            };

            // rte
            if ((args.Length == 2) && (args[0].ToLower() == "rte"))
            {
                string file = args[1].Replace("\"","");
                string fp = System.IO.Path.GetDirectoryName(file);
                string fn = System.IO.Path.GetFileName(file);
                string[] files = System.IO.Directory.GetFiles(fp,fn);
                System.IO.Directory.CreateDirectory(fp + @"\RTE");
                string exe = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase.ToString();

                Console.WriteLine("Processing "+files.Length.ToString()+" files");
                for (int i = 0; i < files.Length; i++)
                {                    
                    Process(files[i],fp + @"\RTE\" + System.IO.Path.GetFileNameWithoutExtension(files[i]) + ".rte");
                    Console.WriteLine(i.ToString() + "/" + files.Length.ToString() + ": " + System.IO.Path.GetFileName(files[i]) + " to " + System.IO.Path.GetFileNameWithoutExtension(files[i]) + ".rte");
                };                
                return;
            };

            // rte1
            if ((args.Length == 2) && (args[0].ToLower() == "rte1"))
            {
                string file = args[1].Replace("\"", "");
                string fp = System.IO.Path.GetDirectoryName(file);
                string fn = System.IO.Path.GetFileName(file);
                string[] files = System.IO.Directory.GetFiles(fp, fn);
                System.IO.Directory.CreateDirectory(fp + @"\RTE1");
                string exe = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase.ToString();

                if (files.Length > 0)
                {
                    Console.WriteLine("Processing " + files.Length.ToString() + " files");
                    System.IO.FileStream fs = new FileStream(fp + @"\RTE1\RGWays " + DateTime.Now.ToString("yyyyMMdd HHmmss") + ".rte", FileMode.Create);
                    string tmpFN = System.IO.Path.GetFileName(fs.Name);
                    StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.GetEncoding(1251));
                    sw.WriteLine("OziExplorer Route File Version 1.0");
                    sw.WriteLine("WGS 84");
                    sw.WriteLine("dkxce RGWay 2 RTE Converter");
                    sw.WriteLine("...");
                    sw.WriteLine("");
                    for (int f = 0; f < files.Length; f++)
                    {
                        XmlDocument xd = new XmlDocument();
                        xd.Load(files[f]);
                        XmlNodeList nl = xd.SelectNodes("RouteResultStored/route/vector/p");
                        if ((nl != null) && (nl.Count > 0))
                        {
                            sw.WriteLine("R,0," + System.IO.Path.GetFileName(files[f]));
                            for (int i = 0; i < nl.Count; i++)
                                sw.WriteLine("W,0," + (i + 1).ToString() + ",0,0," + nl[i].Attributes["y"].Value + "," + nl[i].Attributes["x"].Value + ",,,,,,,,,");
                            sw.WriteLine("");
                            sw.Flush();
                        };
                        xd = null;
                        Console.WriteLine(f.ToString() + "/" + files.Length.ToString() + ": " + System.IO.Path.GetFileName(files[f]) + " to " + tmpFN);
                    };
                    sw.Flush();
                    sw.Close();
                    fs.Close();
                };
                return;
            };

            // kml
            if ((args.Length == 2) && (args[0].ToLower() == "kml"))
            {
                string file = args[1].Replace("\"", "");
                string fp = System.IO.Path.GetDirectoryName(file);
                string fn = System.IO.Path.GetFileName(file);
                string[] files = System.IO.Directory.GetFiles(fp, fn);
                System.IO.Directory.CreateDirectory(fp + @"\KML");
                string exe = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase.ToString();

                if (files.Length > 0)
                {
                    int dividor = 100000;
                    Console.WriteLine("Processing " + files.Length.ToString() + " files");

                    string DT = DateTime.Now.ToString("yyyyMMdd HHmmss");
                    int kmlf = files.Length / dividor;
                    if (files.Length % dividor > 0) kmlf++;

                    System.IO.FileStream fs = null; // = new FileStream(fp + @"\KML\RGWays " + DateTime.Now.ToString("yyyyMMdd HHmmss") + " 1-"+kmlf.ToString()+".kml", FileMode.Create);
                    string tmpFN = null;            // = System.IO.Path.GetFileName(fs.Name);
                    StreamWriter sw = null;         // = new StreamWriter(fs, System.Text.Encoding.GetEncoding(1251));
                    //sw.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?><kml xmlns=\"http://earth.google.com/kml/2.2\"><Document xmlns=\"\"><Folder><name>RGWays to KML " + DateTime.Now.ToString("yyyyMMdd HHmmss") + "</name>");
                    for (int f = 0; f < files.Length; f++) // LIMIT dividor
                    {
                        if (f % dividor == 0)
                        {
                            if (f > 0)
                            {
                                sw.WriteLine("</Folder></Document></kml>");
                                sw.Flush();
                                sw.Close();
                                fs.Close();
                            };
                            int kmlc = f / dividor + 1;
                            fs = new FileStream(fp + @"\KML\RGWays " + DT + " FILE "+kmlc.ToString()+" OF " + kmlf.ToString() + ".kml", FileMode.Create);
                            tmpFN = System.IO.Path.GetFileName(fs.Name);
                            sw = new StreamWriter(fs, System.Text.Encoding.GetEncoding(1251));
                            sw.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?><kml xmlns=\"http://earth.google.com/kml/2.2\"><Document xmlns=\"\"><Folder><name>RGWays to KML " + DT + " FILE " + kmlc.ToString() + " OF " + kmlf.ToString() + "</name>");
                        };

                        XmlDocument xd = new XmlDocument();
                        xd.Load(files[f]);
                        XmlNodeList nl = xd.SelectNodes("RouteResultStored/route/vector/p");
                        if ((nl != null) && (nl.Count > 0))
                        {
                            sw.WriteLine("<Placemark><name>" + System.IO.Path.GetFileName(files[f]) + " ("+(f+1).ToString()+"/"+files.Length.ToString()+")</name><LineString><coordinates>");
                            for (int i = 0; i < nl.Count; i++)
                                sw.Write(nl[i].Attributes["x"].Value + "," + nl[i].Attributes["y"].Value + ",0 ");
                            sw.WriteLine("");
                            sw.Write("</coordinates></LineString></Placemark>");
                            sw.Flush();
                        };
                        xd = null;
                        Console.WriteLine((f+1).ToString() + "/" + files.Length.ToString() + ": " + System.IO.Path.GetFileName(files[f]) + " to " + tmpFN);
                    };
                    sw.WriteLine("</Folder></Document></kml>");
                    sw.Flush();
                    sw.Close();
                    fs.Close();
                };
                return;
            };

            // geojson
            if ((args.Length == 2) && (args[0].ToLower() == "geojson"))
            {
                string file = args[1].Replace("\"", "");
                string fp = System.IO.Path.GetDirectoryName(file);
                string fn = System.IO.Path.GetFileName(file);
                string[] files = System.IO.Directory.GetFiles(fp, fn);
                System.IO.Directory.CreateDirectory(fp + @"\GeoJSON");
                string exe = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase.ToString();

                Hashtable ht = new Hashtable();

                if (files.Length > 0)
                {
                    Console.WriteLine("Processing " + files.Length.ToString() + " files");
                    System.IO.FileStream fs = new FileStream(fp + @"\GeoJSON\RGWays " + DateTime.Now.ToString("yyyyMMdd HHmmss") + ".geojson", FileMode.Create);
                    string tmpFN = System.IO.Path.GetFileName(fs.Name);
                    StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.GetEncoding(1251));
                    sw.WriteLine("{ \"type\": \"FeatureCollection\", \"features\": [");
                    int fCount = 0;
                    for (int i = 0; i < files.Length; i++)
                    {
                        XmlDocument xd = new XmlDocument();
                        xd.Load(files[i]);
                        XmlNodeList nl = xd.SelectNodes("RouteResultStored/route/vector/p");
                        if ((nl != null) && (nl.Count > 0))
                        {                            
                            string lbl = System.IO.Path.GetFileName(files[i]);
                            lbl = lbl.Substring(0, lbl.IndexOf("."));

                            if ((fCount++) > 0) sw.WriteLine(",");
                            sw.Write("{ \"type\": \"Feature\", \"geometry\": {\"type\": \"LineString\", \"coordinates\": [");
                            for (int i2 = 0; i2 < nl.Count; i2++)
                                sw.Write((i2 > 0 ? "," : "") + "[" + nl[i2].Attributes["y"].Value + "," + nl[i2].Attributes["x"].Value + "]");
                            sw.WriteLine("]},");
                            sw.Write("\"properties\": {\"name\": \"" + lbl + "\"}}");
                            
                            try
                            {
                                string[] ft = lbl.Split(new char[] { 'T', 'B', 't', 'b' }, StringSplitOptions.None);
                                ht[ft[0]] = nl[0].Attributes["y"].Value + "," + nl[0].Attributes["x"].Value;
                                ht[ft[1]] = nl[nl.Count - 1].Attributes["y"].Value + "," + nl[nl.Count - 1].Attributes["x"].Value;
                            }
                            catch { };
                        };
                        xd = null;
                        Console.WriteLine(i.ToString() + "/" + files.Length.ToString() + ": " + System.IO.Path.GetFileName(files[i]) + " to " + tmpFN);
                    };

                    if(ht.Count > 0)
                        foreach(string key in ht.Keys)
                        {
                            if ((fCount++) > 0) sw.WriteLine(",");
                            sw.Write("{ \"type\": \"Feature\", \"geometry\": {\"type\": \"Point\", \"coordinates\": [" + ht[key].ToString() + "]},");
                            sw.Write("\"properties\": {\"id\": " + key + "}}");                            
                        };
                    sw.WriteLine("]}");
                    sw.Flush();
                    sw.Close();
                    fs.Close();
                };
                

                return;
            };

            // mp
            if ((args.Length == 2) && (args[0].ToLower() == "mp"))
            {
                string file = args[1].Replace("\"", "");
                string fp = System.IO.Path.GetDirectoryName(file);
                string fn = System.IO.Path.GetFileName(file);
                string[] files = System.IO.Directory.GetFiles(fp, fn);
                System.IO.Directory.CreateDirectory(fp + @"\MP");
                string exe = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase.ToString();

                Hashtable ht = new Hashtable();

                if (files.Length > 0)
                {
                    Console.WriteLine("Processing " + files.Length.ToString() + " files");
                    System.IO.FileStream fs = new FileStream(fp + @"\MP\RGWays " + DateTime.Now.ToString("yyyyMMdd HHmmss") + ".mp", FileMode.Create);
                    string tmpFN = System.IO.Path.GetFileName(fs.Name);
                    StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.GetEncoding(1251));
                    sw.WriteLine("; Generated by dkxce RGWay 2 MP Converter");
                    sw.WriteLine("");
                    sw.WriteLine("[IMG ID]");
                    sw.WriteLine("[END-IMG ID]");
                    sw.WriteLine("");
                    for (int i = 0; i < files.Length; i++)
                    {
                        XmlDocument xd = new XmlDocument();
                        xd.Load(files[i]);
                        XmlNodeList nl = xd.SelectNodes("RouteResultStored/route/vector/p");
                        if ((nl != null) && (nl.Count > 0))
                        {
                            string lbl = System.IO.Path.GetFileName(files[i]);
                            lbl = lbl.Substring(0, lbl.IndexOf("."));
                            sw.WriteLine("; " + lbl);
                            sw.WriteLine("[POLYLINE]");
                            sw.WriteLine("Label=" + lbl);
                            sw.WriteLine("Type=0x3");
                            sw.Write("Data0=");
                            for (int i2 = 0; i2 < nl.Count; i2++)
                                sw.Write((i2 > 0 ? "," : "") + "(" + nl[i2].Attributes["y"].Value + "," + nl[i2].Attributes["x"].Value + ")");
                            sw.WriteLine("");
                            sw.WriteLine("[END]");

                            try
                            {
                                string[] ft = lbl.Split(new char[] { 'T', 'B', 't', 'b' }, StringSplitOptions.None);
                                ht[ft[0]] = "Data0=(" + nl[0].Attributes["y"].Value + "," + nl[0].Attributes["x"].Value + ")";
                                ht[ft[1]] = "Data0=(" + nl[nl.Count - 1].Attributes["y"].Value + "," + nl[nl.Count - 1].Attributes["x"].Value + ")";
                            }
                            catch { };
                        };
                        xd = null;
                        Console.WriteLine(i.ToString() + "/" + files.Length.ToString() + ": " + System.IO.Path.GetFileName(files[i]) + " to " + tmpFN);
                    };

                    if (ht.Count > 0)
                        foreach (string key in ht.Keys)
                        {
                            sw.WriteLine("; " + key);
                            sw.WriteLine("[POI]");
                            sw.WriteLine("Label=" + key);
                            sw.WriteLine("Type=0x2000");
                            sw.WriteLine(ht[key].ToString());
                            sw.WriteLine("[END]");
                            sw.WriteLine();
                        };
                    sw.Flush();
                    sw.Close();
                    fs.Close();
                };
                return;
            };

            if (args.Length != 2) return;
            Console.WriteLine("Converting "+args[0]+" to "+args[1]);
            Process(args[0], args[1]);            
        }

        private static void Process(string f1, string f2)
        {
            XmlDocument xd = new XmlDocument();
            xd.Load(f1);
            XmlNodeList nl = xd.SelectNodes("RouteResultStored/route/vector/p");
            if ((nl != null) && (nl.Count > 0))
            {
                FileStream fs = new FileStream(f2, FileMode.Create);
                StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.GetEncoding(1251));
                sw.WriteLine("OziExplorer Route File Version 1.0");
                sw.WriteLine("WGS 84");
                sw.WriteLine("dkxce RGWay 2 RTE Converter");
                sw.WriteLine("...");
                sw.WriteLine("R,0," + System.IO.Path.GetFileName(f1));
                for (int i = 0; i < nl.Count; i++)
                    sw.WriteLine("W,0," + (i + 1).ToString() + ",0,0," + nl[i].Attributes["y"].Value + "," + nl[i].Attributes["x"].Value + ",,,,,,,,,");
                sw.Flush();
                sw.Close();
                fs.Close();
            };
        }
    }
}
