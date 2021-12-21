using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;

using dkxce.Route.Classes;
using dkxce.Route.Matrix;

using dkxce.Route.Regions;

namespace RouteGraphCalcMatrix
{
    class Program
    {        
        static void Main(string[] args)
        {
            if ((args == null) || (args.Length < 2))
            {
                Console.WriteLine("Модуль создание матрицы межрегиональных маршрутов");
                Console.WriteLine("User syntax: RouteGraphCalcMatrix.exe %graphs_dir% %matrix_filename% %regions_dir%");
                Console.WriteLine("Example: RouteGraphCalcMatrix.exe DATA\\READY\\GRAPHS DATA\\READY\\000.bin ");
                System.Threading.Thread.Sleep(5000);
                return;
            };

            string path = args[0];
            if (!System.IO.Path.IsPathRooted(path)) path = XMLSaved<int>.GetCurrentDir() + path;

            int size = 10;
            string[] files = System.IO.Directory.GetFiles(path, "*.rgnodes.xml");
            Console.WriteLine("Calculate matrix size...");
            foreach (string file in files)
            {
                TRGNode[] nds = XMLSaved<TRGNode[]>.Load(file);
                for (int i = 0; i < nds.Length; i++)
                    if (nds[i].id > size) size = nds[i].id;
            };
            Console.WriteLine("Matrix size is " + size.ToString() + " X " + size.ToString());

            RMMatrix rm = RMMatrix.CreateInMemory(size);
            RGNodesDots rgnd = new RGNodesDots(size);
            if (args.Length > 2) rgnd.RegionsDir = args[2];

            Console.WriteLine("Loading RGNodes in Matrix");
            foreach (string file in files)
            {
                TRGNode[] nds = XMLSaved<TRGNode[]>.Load(file);
                for (int i = 0; i < nds.Length; i++)
                {                    
                    if (nds[i].links != null)
                        for (int ito = 0; ito < nds[i].links.Length; ito++)
                            rm.AddWay((uint)nds[i].id, (uint)nds[i].links[ito], nds[i].costs[ito], nds[i].dists[ito], nds[i].times[ito], (ushort)nds[i].region);

                    string err = "";
                    if (nds[i].links != null)
                        if (nds[i].dists.Length > 0)
                            if (nds[i].dists[0] > 1E+20)
                                err = nds[i].region.ToString()+"["+nds[i].links[0].ToString()+"->-"+nds[i].id.ToString()+"]";

                    rgnd.AddNode(nds[i].id, nds[i].lat, nds[i].lon, nds[i].region.ToString(), err);
                };
            };

            path = args[1];
            if (!System.IO.Path.IsPathRooted(path)) path = XMLSaved<int>.GetCurrentDir() + path;

            rm.SaveToTextFile(path + ".zero.txt");
            Console.WriteLine("Solving...");
            rm.Solve(); // calculate matrix
            rm.SaveToFile(path); // save matrix to disk
            rm.SaveToTextFile(path + ".filled.txt");
            rgnd.SaveNodesToCSV(path + ".csv");
            rgnd.SaveNodesToGPX(path + ".gpx");
            rgnd.SaveNodesToMP(path + ".mp");
            rgnd.SaveNodesToKML(path + ".kml");
            rgnd.SaveNodesToGeoJSON(path + ".geojson");
            System.Threading.Thread.Sleep(1500);
            Console.WriteLine("Done");
            rm.Close(); // close matrix
        }

        private class RGNodesDots
        {
            public string RegionsDir = "";

            private int size = 0;
            private float[] lats;
            private float[] lons;
            private string[] regs;
            private string[] errs;

            private int maxRegsIn = 2;

            public RGNodesDots(int size)
            {
                this.size = size;
                this.lats = new float[size+1];
                this.lons = new float[size+1];
                this.regs = new string[size+1];
                this.errs = new string[size+1];
            }

            public void AddNode(int id, float lat, float lon, string reg, string error)
            {
                try
                {
                    this.lats[id] = lat;
                    this.lons[id] = lon;
                    if (this.regs[id] == null) this.regs[id] = "";
                    if (this.errs[id] == null) this.errs[id] = "";
                    this.regs[id] += (this.regs[id].Length > 0 ? "," : "") + reg;
                    int tmp = this.regs[id].Split(new string[] { "," }, StringSplitOptions.None).Length;
                    if (tmp > maxRegsIn) tmp = maxRegsIn;
                    if(error.Length > 0)
                        this.errs[id] += (this.errs[id].Length > 0 ? "," : "") + error;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.ReadLine();
                };
            }

            /// <summary>
            ///     Создает таблицу координат узлов межрегиональных маршрутов
            /// </summary>
            /// <param name="fn">Полный путь к файлу</param>
            public void SaveNodesToCSV(string fn)
            {
                    FileStream fout = new FileStream(fn, FileMode.Create);
                    StreamWriter sw = new StreamWriter(fout,System.Text.Encoding.GetEncoding(1251));

                    PointInRegionUtils ru = new PointInRegionUtils();
                    if(this.RegionsDir != "")
                        ru.LoadRegionsFromFile(this.RegionsDir + @"\regions.shp");

                    //sw.WriteLine("Точки стыковки межрегиональных маршрутов");
                    sw.Write("RGNODE;REGION_A;REGION_B;" + (maxRegsIn > 2 ? "REGIONS_ALL;" : "") + "LAT;LON;ERROR_IN(FROM_RGNODE->-TO_RGNODE);");
                    if (ru.RegionsCount > 0)
                    {
                        sw.Write("REG_ANAM;");
                        sw.Write("REG_BNAM;");
                    };
                    sw.WriteLine();
                    for (uint x = 1; x <= size; x++)
                    {
                        sw.Write(x.ToString() + ";");
                        string[] r = new string[] { "" };
                        if(this.regs[x] != null) r = this.regs[x].Split(new string[] { "," }, StringSplitOptions.None);
                        sw.Write(r[0] + ";");
                        sw.Write((r.Length > 1 ? r[1] : "") + ";");                        
                        if(maxRegsIn > 2)
                            sw.Write((r.Length > 2 ? this.regs[x] : "") + ";");
                        sw.Write(this.lats[x].ToString() + ";");
                        sw.Write(this.lons[x].ToString() + ";");
                        sw.Write(this.errs[x] + ";");
                        if (ru.RegionsCount > 0)
                        {
                            if(r[0].Length > 0)
                                sw.Write(ru.RegionNameByRegionId(int.Parse(r[0]))+";");
                            else
                                sw.Write(";");
                            if(r.Length > 1)
                                sw.Write(ru.RegionNameByRegionId(int.Parse(r[1])) + ";");
                            else
                                sw.Write(";");
                        };
                        sw.WriteLine();
                    };
                    sw.WriteLine();

                    sw.Flush();
                    fout.Close();
            }

            public void SaveNodesToGPX(string fn)
            {
                FileStream fout = new FileStream(fn, FileMode.Create);
                StreamWriter sw = new StreamWriter(fout, System.Text.Encoding.GetEncoding(1251));

                sw.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\" ?>");
                sw.WriteLine("<gpx xmlns=\"http://www.topografix.com/GPX/1/1/\" version=\"1.1\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd\">");

                for (uint x = 1; x <= size; x++)
                    if((this.lats[x] != 0) && (this.lons[x] != 0))
                        sw.WriteLine("<wpt lat=\""+this.lats[x].ToString().Replace(",",".")+"\" lon=\""+this.lons[x].ToString().Replace(",",".")+"\"><name>"+x.ToString()+"</name></wpt>");
                sw.WriteLine("</gpx>");

                sw.Flush();
                fout.Close();
            }

            public void SaveNodesToKML(string fn)
            {
                FileStream fout = new FileStream(fn, FileMode.Create);
                StreamWriter sw = new StreamWriter(fout, System.Text.Encoding.GetEncoding(1251));

                sw.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?><kml xmlns=\"http://earth.google.com/kml/2.2\"><Document xmlns=\"\"><Folder><name>RGNodes</name>");

                for (uint x = 1; x <= size; x++)
                    if ((this.lats[x] != 0) && (this.lons[x] != 0))
                        sw.WriteLine("<Placemark><name>" + x.ToString() + "</name><Point><coordinates>" + this.lons[x].ToString().Replace(",", ".") + "," + this.lats[x].ToString().Replace(",", ".") + ",0</coordinates></Point></Placemark>");
                sw.WriteLine("</Folder></Document></kml>");

                sw.Flush();
                sw.Close();
            }

            public void SaveNodesToGeoJSON(string fn)
            {
                FileStream fout = new FileStream(fn, FileMode.Create);
                StreamWriter sw = new StreamWriter(fout, System.Text.Encoding.GetEncoding(1251));
                sw.WriteLine("{ \"type\": \"FeatureCollection\", \"features\": [");
                int fC = 0;

                for (uint x = 1; x <= size; x++)
                    if ((this.lats[x] != 0) && (this.lons[x] != 0))
                    {
                        if ((fC++) > 0) sw.Write(",");
                        sw.Write("{ \"type\": \"Feature\", \"geometry\": {\"type\": \"Point\", \"coordinates\": ["+this.lons[x].ToString().Replace(",", ".") + "," + this.lats[x].ToString().Replace(",", ".")+"]},");
                        sw.WriteLine("\"properties\": {\"id\": "+x.ToString()+ "}}");
                    };


                sw.WriteLine("]}");
                sw.Flush();
                sw.Close();
            }

            public void SaveNodesToMP(string fn)
            {
                FileStream fout = new FileStream(fn, FileMode.Create);
                StreamWriter sw = new StreamWriter(fout, System.Text.Encoding.GetEncoding(1251));

                sw.WriteLine("; Generated by dkxce RGWay 2 MP Converter");
                sw.WriteLine("");
                sw.WriteLine("[IMG ID]");
                sw.WriteLine("[END-IMG ID]");
                sw.WriteLine("");

                for (uint x = 1; x <= size; x++)
                    if ((this.lats[x] != 0) && (this.lons[x] != 0))
                    {
                        sw.WriteLine("[POI]");
                        sw.WriteLine("Type=0x1611");
                        sw.WriteLine("Label=" + x.ToString());
                        sw.WriteLine("Data0=(" + this.lats[x].ToString().Replace(",", ".") + "," + this.lons[x].ToString().Replace(",", ".") + ")");
                        sw.WriteLine("[END]");
                    };

                sw.Flush();
                fout.Close();
            }
        }
    }
}
