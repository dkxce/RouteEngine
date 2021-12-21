// http://gis-lab.info/projects/osm_shp/region

using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;

using dkxce.Route.Classes;
using dkxce.Route.Shp2Rt;
using dkxce.Route.GSolver;
using dkxce.Route.PointNLL;
using dkxce.Route.Matrix;
using dkxce.Route.WayList;

namespace CNSL
{
    class Program
    {        
        // // // // // // // // // // // // // // // // // // // // // // // // // // 
        // // // // // // // // // // // // // // // // // // // // // // // // // // 
        // // // // // // // // // // // // // // // // // // // // // // // // // //         
        // // TEST // //
        // // // // // // // // // // // // // // // // // // // // // // // // // // 
        // // // // // // // // // // // // // // // // // // // // // // // // // // 
        // // // // // // // // // // // // // // // // // // // // // // // // // //         

        static void TestGraphGeoFile()
        {
            List<uint> way = new List<uint>();
            way.Add(1);
            way.Add(7);
            PointNLLSearcher schr = PointNLLSearcher.LoadToMemory(XMLSaved<int>.GetCurrentDir()+@"\Matrix\graph.geo");
            PointNLL[] waygeo = schr.GetCoordinates(way.ToArray());
            schr.Close(); // close INDEXES FOR NODE LAT LON
            return;
        }
        static void CreateGraphGeoFile()
        {
            // Create indexes for NODE LAT LON
            Random rka = new Random();
            PointNLLIndexer ixr = new PointNLLIndexer();
            for (uint i = 1; i < 100; i++)
                ixr.Add(new PointNLL(i, (float)(55.0 + (float)rka.Next(100) / 100.0), (float)(39.0 + (float)rka.Next(100) / 100.0)));
            ixr.OptimizeSaveAndClose(XMLSaved<int>.GetCurrentDir() + @"\Matrix\graph.geo");     
        }

        static void CSV2TXT()
        {
            System.IO.FileStream fs = new FileStream(XMLSaved<int>.GetCurrentDir() + @"\Matrix2\TEST.csv", FileMode.Open);
            System.IO.StreamReader sr = new StreamReader(fs);
            sr.ReadLine();

            System.IO.FileStream fw = new FileStream(XMLSaved<int>.GetCurrentDir() + @"\Matrix2\TEST.txt", FileMode.Create);
            System.IO.StreamWriter sw = new StreamWriter(fw);
            sw.WriteLine("#\tnode(cost;length;time)");

            uint prev_node = 0;
            List<uint> next = new List<uint>();
            List<double> length = new List<double>();
            while (!sr.EndOfStream)
            {

                string[] line = sr.ReadLine().Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                double len = double.Parse(line[0]);
                uint f = uint.Parse(line[1]);
                uint e = uint.Parse(line[2]);
                if ((prev_node != f) && (prev_node > 0))
                {
                    if ((prev_node + 1) != f)
                    {
                        for (uint x = prev_node + 1; x < f; x++)
                            sw.WriteLine(x.ToString() + "\t");
                    };
                    sw.Write(prev_node.ToString() + "\t");
                    for (int i = 0; i < next.Count; i++)
                        sw.Write(next[i].ToString() + "(" + length[i].ToString() + ";" + length[i].ToString() + ";" + (length[i]/60).ToString() + ") ");
                    sw.WriteLine();
                    next.Clear();
                    length.Clear();
                };
                prev_node = f;
                next.Add(e);
                length.Add(len);
                continue;
            };
            sr.Close();
            fs.Close();
            if (prev_node > 0)
            {
                sw.Write(prev_node.ToString() + "\t");
                for (int i = 0; i < next.Count; i++)
                    sw.Write(next[i].ToString() + "(" + length[i].ToString() + ";" + length[i].ToString() + ";60) ");
                sw.WriteLine();
                next.Clear();
                length.Clear();
            };
            sw.Close();
            fs.Close();
        }

        #region TULA SHP EXAMPLE
        static void Tula_ConvertShp()
        {
            string shpf = XMLSaved<int>.GetCurrentDir() + @"\Tula\Roads.shp";
            ShpToGraphConverter con = new ShpToGraphConverter(shpf);
            con.WriteLinesNamesFile = true;
            con.ConvertTo(XMLSaved<int>.GetCurrentDir() + @"\Tula\Tula.rt");
            Console.WriteLine("Press Enter key to continue...");
            Console.ReadLine();

        }
        static void Tula_Test()
        {
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " Загрузка графа в память");
            //RMGraph gr = RMGraph.LoadToMemory(XMLSaved<int>.GetCurrentDir() + @"\Tula\Tula.rt", RMGraph.SegmentsInMemoryPreLoad.onDiskCalculations);
            RMGraph gr = RMGraph.LoadToMemory(XMLSaved<int>.GetCurrentDir() + @"\Tula\Tula.rt", 17);
            //RMGraph gr = RMGraph.WorkWithDisk(XMLSaved<int>.GetCurrentDir() + @"\Tula\Tula.rt");

            // in city route
            //FindStartStopResult nodeStart = gr.FindNodeStart((float)54.2523, (float)37.6052, (float)2000);
            //FindStartStopResult nodeEnd = gr.FindNodeEnd((float)54.1654, (float)37.572, (float)2000);

            // region route
            FindStartStopResult nodeStart = gr.FindNodeStart((float)54.5109, (float)37.1094, (float)2000);
            FindStartStopResult nodeEnd = gr.FindNodeEnd((float)53.139, (float)38.120, (float)2000);

            DateTime s = DateTime.Now;
            Console.WriteLine(s.ToString("HH:mm:ss") + " Begin Calc");

            //gr.LoadExternalTimeInfoFile(...); .. Load Traffic
            gr.BeginSolve(true, null);
            gr.MinimizeRouteBy = MinimizeBy.Time;
            //gr.CalculataA(1,4); 
            //gr.Calculate(1, new uint[] { 4 });
            gr.SolveAstar(nodeStart.nodeN, nodeEnd.nodeN); // A*            
            //gr.SolveDeikstra(nodeStart.nodeN, new uint[] { nodeEnd.nodeN }); // NORMAL DEIKSTRA
            //gr.SolveDeikstra(new uint[] { nodeStart.nodeN }, nodeEnd.nodeN); // REVERSE DEIKSTRA
            //gr.SolveDeikstra(nodeStart.nodeN, new uint[] { nodeEnd.nodeN, nodeEnd.nodeN + 10, nodeEnd.nodeN+20 }); // NORMAL DEIKSTRA MULTI

            DateTime e = DateTime.Now;
            TimeSpan ts = e.Subtract(s);
            Console.WriteLine(e.ToString("HH:mm:ss") + " End Calc");
            Console.WriteLine("Elapsed: " + (ts.Minutes).ToString() + "m " + ts.Seconds.ToString() + "s " + ts.Milliseconds.ToString() + "ms");

            float c = gr.GetRouteCost(nodeStart.nodeN, nodeEnd.nodeN);
            float d = gr.GetRouteDistance(nodeStart.nodeN, nodeEnd.nodeN);
            float t = gr.GetRouteTime(nodeStart.nodeN, nodeEnd.nodeN);
            uint[] n = gr.GetRouteNodes(nodeStart.nodeN, nodeEnd.nodeN);
            float[] dd; float[] tt;
            uint[] r = gr.GetRouteLines(nodeStart.nodeN, n, nodeEnd.nodeN, out dd, out tt);
            PointFL[] v = gr.GetRouteVectorWNL(nodeStart.nodeN, n, nodeEnd.nodeN);

            PointF[] v1 = gr.GetRouteVector(nodeStart.nodeN, n, nodeEnd.nodeN);
            RouteResult rr = gr.GetRouteFull(nodeStart, nodeEnd, true, true);
            int[] linkids = gr.GetLinesLinkIDs(rr.lines);
            gr.EndSolve();

            // all-in-one check
            //RouteResult r2 = gr.GetRoute(new System.Drawing.PointF((float)39.52212452888, (float)52.580548586409186), new System.Drawing.PointF((float)39.5174072814941, (float)52.57637509148828));

            gr.Close();

            ToJS(nodeStart.normal, rr.vector, nodeEnd.normal, "navy");
            Console.WriteLine("Press Enter key to continue...");
            Console.ReadLine();
        }
        #endregion

        #region LIPETSK OSM/SHP EXAMPLE
        static void Lipetsk_ConvertOSM()
        {
            string shpf = XMLSaved<int>.GetCurrentDir() + @"\Matrix4\7.shp";
            ShpToGraphConverter con = new ShpToGraphConverter(shpf);
            con.WriteLinesNamesFile = true;
            con.ConvertTo(XMLSaved<int>.GetCurrentDir() + @"\Matrix4\lipetsk_osm.rt");
            Console.WriteLine("Press Enter key to continue...");
            Console.ReadLine(); 
        }
        static void Lipetsk_ConvertShp()
        {
            string shpf = XMLSaved<int>.GetCurrentDir() + @"\Matrix3\Roads_Lipetsk.shp";
            ShpToGraphConverter con = new ShpToGraphConverter(shpf);
            con.RegionID = 11;
            con.WriteLinesNamesFile = true;
            con.ConvertTo(XMLSaved<int>.GetCurrentDir() + @"\Matrix3\lipetsk.rt");
            Console.WriteLine("Press Enter key to continue...");
            Console.ReadLine(); 

        }
        static void Lipetsk_TestOSM()
        {
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " Загрузка графа в память");
            //RMGraph gr = RMGraph.LoadToMemory(XMLSaved<int>.GetCurrentDir() + @"\Matrix4\lipetsk_osm.rt", RMGraph.SegmentsInMemoryPreLoad.onDiskCalculations);
            RMGraph gr = RMGraph.LoadToMemory(XMLSaved<int>.GetCurrentDir() + @"\Matrix4\lipetsk_osm.rt", 10);
            //RMGraph gr = RMGraph.WorkWithDisk(XMLSaved<int>.GetCurrentDir() + @"\Matrix4\lipetsk_osm.rt");

            //uint[] ltogo = gr.GetRouteLines(14709, new uint[] { 14710 }, 14709);
            //int[] llids = gr.GetLinesLinkIDs(ltogo);
            //return;

            // region route
            //FindStartStopResult nodeStart = gr.FindNodeStart((float)53.26366, (float)39.25883, (float)2000);
            //FindStartStopResult nodeEnd = gr.FindNodeEnd((float)52.497246, (float)39.894234, (float)2000);
            // 52.49538, (float)39.9101

            // as NMS
            //FindStartStopResult nodeStart = gr.FindNodeStart((float)52.59708, (float)39.5685, (float)2000);
            //FindStartStopResult nodeEnd = gr.FindNodeEnd((float)52.63228, (float)39.5788, (float)2000);

            // in city route
            FindStartStopResult nodeStart = gr.FindNodeStart((float)52.64549811935865, (float)39.660279750823975, (float)2000);
            FindStartStopResult nodeEnd = gr.FindNodeEnd((float)52.57972714326297, (float)39.51683521270752, (float)2000);

            // test restrictions
            //FindStartStopResult nodeStart = gr.FindNodeStart((float)53.24475, (float)39.13105, (float)2000);
            //FindStartStopResult nodeEnd = gr.FindNodeEnd((float)53.24517, (float)39.1297, (float)2000);            

            // region route 2
            //FindStartStopResult nodeStart = gr.FindNodeStart((float)52.096, (float)37.95, (float)2000);
            //FindStartStopResult nodeEnd = gr.FindNodeEnd((float)53.36, (float)40.10, (float)2000);            

            DateTime s = DateTime.Now;
            Console.WriteLine(s.ToString("HH:mm:ss") + " Begin Calc");

            //gr.LoadExternalTimeInfoFile(...); .. Load Traffic
            gr.BeginSolve(true, null);
            gr.MinimizeRouteBy = MinimizeBy.Time;
            //gr.CalculataA(1,4); 
            //gr.Calculate(1, new uint[] { 4 });
            gr.SolveAstar(nodeStart.nodeN, nodeEnd.nodeN); // A*            
            //gr.SolveDeikstra(nodeStart.nodeN, new uint[] { nodeEnd.nodeN }); // NORMAL DEIKSTRA
            //gr.SolveDeikstra(new uint[] { nodeStart.nodeN }, nodeEnd.nodeN); // REVERSE DEIKSTRA
            //gr.SolveDeikstra(nodeStart.nodeN, new uint[] { nodeEnd.nodeN, nodeEnd.nodeN + 10, nodeEnd.nodeN+20 }); // NORMAL DEIKSTRA MULTI

            DateTime e = DateTime.Now;
            TimeSpan ts = e.Subtract(s);
            Console.WriteLine(e.ToString("HH:mm:ss") + " End Calc");
            Console.WriteLine("Elapsed: " + (ts.Minutes).ToString() + "m " + ts.Seconds.ToString() + "s " + ts.Milliseconds.ToString() + "ms");

            float c = gr.GetRouteCost(nodeStart.nodeN, nodeEnd.nodeN);
            float d = gr.GetRouteDistance(nodeStart.nodeN, nodeEnd.nodeN);
            float t = gr.GetRouteTime(nodeStart.nodeN, nodeEnd.nodeN);
            uint[] n = gr.GetRouteNodes(nodeStart.nodeN, nodeEnd.nodeN);
            float[] dd; float[] tt;
            uint[] r = gr.GetRouteLines(nodeStart.nodeN, n, nodeEnd.nodeN, out dd, out tt);
            PointFL[] v = gr.GetRouteVectorWNL(nodeStart.nodeN, n, nodeEnd.nodeN);            

            PointF[] v1 = gr.GetRouteVector(nodeStart.nodeN, n, nodeEnd.nodeN);
            RouteResult rr = gr.GetRouteFull(nodeStart, nodeEnd, true, true);
            int[] linkids = gr.GetLinesLinkIDs(rr.lines);
            gr.EndSolve();

            // all-in-one check
            //RouteResult r2 = gr.GetRoute(new System.Drawing.PointF((float)39.52212452888, (float)52.580548586409186), new System.Drawing.PointF((float)39.5174072814941, (float)52.57637509148828));

            gr.Close();

            ToJS(nodeStart.normal, rr.vector, nodeEnd.normal,"maroon");
            Console.WriteLine("Press Enter key to continue...");
            Console.ReadLine(); 
        }
        static void Lipetsk_CalcRG()
        {
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " Загрузка графа в память");
            RMGraph gr = RMGraph.LoadToMemory(XMLSaved<int>.GetCurrentDir() + @"\Matrix3\lipetsk.rt", 10);
            gr.MinimizeRouteBy = MinimizeBy.Time;
            gr.CalculateRGNodesRoutes(11); // Расчет путей между точками стыковки межрайонных маршрутов
            gr.Close();
            Console.WriteLine("Press Enter key to continue...");
            Console.ReadLine(); 
        }
        static void Lipetsk_Test()
        {
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " Загрузка графа в память");
            //RMGraph gr = RMGraph.LoadToMemory(XMLSaved<int>.GetCurrentDir() + @"\Matrix3\lipetsk.rt", RMGraph.SegmentsInMemoryPreLoad.onDiskCalculations);
            RMGraph gr = RMGraph.LoadToMemory(XMLSaved<int>.GetCurrentDir() + @"\Matrix3\lipetsk.rt", 10);
            //RMGraph gr = RMGraph.WorkWithDisk(XMLSaved<int>.GetCurrentDir() + @"\Matrix3\lipetsk.rt");

            //uint[] ltogo = gr.GetRouteLines(14709, new uint[] { 14710 }, 14709);
            //int[] llids = gr.GetLinesLinkIDs(ltogo);
            //return;
            
            // region route
            //FindStartStopResult nodeStart = gr.FindNodeStart((float)53.26366, (float)39.25883, (float)2000);
            //FindStartStopResult nodeEnd = gr.FindNodeEnd((float)52.497246, (float)39.894234, (float)2000);
            // 52.49538, (float)39.9101

            // as NMS
            //FindStartStopResult nodeStart = gr.FindNodeStart((float)52.59708, (float)39.5685, (float)2000);
            //FindStartStopResult nodeEnd = gr.FindNodeEnd((float)52.63228, (float)39.5788, (float)2000);

            // in city route
            // U-TURN START
            //FindStartStopResult nodeStart = gr.FindNodeStart((float)52.64549811935865, (float)39.660279750823975, (float)2000);
            //FindStartStopResult nodeEnd = gr.FindNodeEnd((float)52.57972714326297, (float)39.51683521270752, (float)2000);            
            // U-TURN END
            //FindStartStopResult nodeStart = gr.FindNodeStart((float)52.62165451049805, (float)39.56116485595703, (float)2000);
            //FindStartStopResult nodeEnd = gr.FindNodeEnd((float)52.60966873168945, (float)39.567176818847656, (float)2000);
            // U-TURN START & END
            FindStartStopResult nodeStart = gr.FindNodeStart((float)52.64549811935865, (float)39.660279750823975, (float)2000);
            FindStartStopResult nodeEnd = gr.FindNodeEnd((float)52.60966873168945, (float)39.567176818847656, (float)2000);

            // test restrictions
            //FindStartStopResult nodeStart = gr.FindNodeStart((float)53.24475, (float)39.13105, (float)2000);
            //FindStartStopResult nodeEnd = gr.FindNodeEnd((float)53.24517, (float)39.1297, (float)2000);            

            // region route 2
            //FindStartStopResult nodeStart = gr.FindNodeStart((float)52.096, (float)37.95, (float)2000);
            //FindStartStopResult nodeEnd = gr.FindNodeEnd((float)53.36, (float)40.10, (float)2000);            
            
            DateTime s = DateTime.Now;
            Console.WriteLine(s.ToString("HH:mm:ss") + " Begin Calc");

            //gr.LoadExternalTimeInfoFile(...); .. Load Traffic
            gr.BeginSolve(true, null);
            gr.MinimizeRouteBy = MinimizeBy.Time;
            //gr.CalculataA(1,4); 
            //gr.Calculate(1, new uint[] { 4 });
            gr.SolveAstar(nodeStart.nodeN, nodeEnd.nodeN); // A*            
            //gr.SolveDeikstra(nodeStart.nodeN, new uint[] { nodeEnd.nodeN }); // NORMAL DEIKSTRA
            //gr.SolveDeikstra(new uint[] { nodeStart.nodeN }, nodeEnd.nodeN); // REVERSE DEIKSTRA
            //gr.SolveDeikstra(nodeStart.nodeN, new uint[] { nodeEnd.nodeN, nodeEnd.nodeN + 10, nodeEnd.nodeN+20 }); // NORMAL DEIKSTRA MULTI

            DateTime e = DateTime.Now;
            TimeSpan ts = e.Subtract(s);
            Console.WriteLine(e.ToString("HH:mm:ss") + " End Calc");
            Console.WriteLine("Elapsed: " + (ts.Minutes).ToString() + "m " + ts.Seconds.ToString() + "s " + ts.Milliseconds.ToString() + "ms");
            float c = gr.GetRouteCost(nodeStart.nodeN, nodeEnd.nodeN);
            float d = gr.GetRouteDistance(nodeStart.nodeN, nodeEnd.nodeN);
            float t = gr.GetRouteTime(nodeStart.nodeN, nodeEnd.nodeN);
            uint[] n = gr.GetRouteNodes(nodeStart.nodeN, nodeEnd.nodeN);
            float[] dd; float[] tt;
            uint[] r = gr.GetRouteLines(nodeStart.nodeN, n, nodeEnd.nodeN, out dd, out tt);
            PointFL[] v = gr.GetRouteVectorWNL(nodeStart.nodeN, n, nodeEnd.nodeN);

            /* ROUTE DESCRIPTION */
            RouteDescription rd = new RouteDescription(gr);
            RDPoint[] rdps = rd.GetDescription(v);
            rd.SaveTextDescription(rdps, XMLSaved<int>.GetCurrentDir() + @"\Matrix3\WAY-LIST.txt");
            //string test_1 = rdps[2].ToString();
            /* END */
            
            PointF[] v1 = gr.GetRouteVector(nodeStart.nodeN, n, nodeEnd.nodeN);
            RouteResult rr = gr.GetRouteFull(nodeStart, nodeEnd, true, true);
            rdps = rd.GetDescription(rr, nodeStart, nodeEnd);
            rd.SaveTextDescription(rdps, XMLSaved<int>.GetCurrentDir() + @"\Matrix3\WAY-LIST2.txt");
            int[] linkids = gr.GetLinesLinkIDs(rr.lines);            
            gr.EndSolve();

            // all-in-one check
            //RouteResult r2 = gr.GetRoute(new System.Drawing.PointF((float)39.52212452888, (float)52.580548586409186), new System.Drawing.PointF((float)39.5174072814941, (float)52.57637509148828));

            gr.Close();

            ToJS(nodeStart.normal, rr.vector, nodeEnd.normal, "navy");
            Console.WriteLine("Press Enter key to continue...");
            Console.ReadLine();
        }
        static void Lipetsk_Test_010()
        {
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " Загрузка графа в память");
            RMGraph gr = RMGraph.LoadToMemory(XMLSaved<int>.GetCurrentDir() + @"..\GRAPHS\010.rt", 10);
            
            //uint[] ltogo = gr.GetRouteLines(14709, new uint[] { 14710 }, 14709);
            //int[] llids = gr.GetLinesLinkIDs(ltogo);
            //return;

            // region route
            //FindStartStopResult nodeStart = gr.FindNodeStart((float)53.26366, (float)39.25883, (float)2000);
            //FindStartStopResult nodeEnd = gr.FindNodeEnd((float)52.497246, (float)39.894234, (float)2000);
            // 52.49538, (float)39.9101

            // as NMS
            //FindStartStopResult nodeStart = gr.FindNodeStart((float)52.59708, (float)39.5685, (float)2000);
            //FindStartStopResult nodeEnd = gr.FindNodeEnd((float)52.63228, (float)39.5788, (float)2000);

            // in city route
            // U-TURN START
            //FindStartStopResult nodeStart = gr.FindNodeStart((float)52.64549811935865, (float)39.660279750823975, (float)2000);
            //FindStartStopResult nodeEnd = gr.FindNodeEnd((float)52.57972714326297, (float)39.51683521270752, (float)2000);            
            // U-TURN END
            //FindStartStopResult nodeStart = gr.FindNodeStart((float)52.62165451049805, (float)39.56116485595703, (float)2000);
            //FindStartStopResult nodeEnd = gr.FindNodeEnd((float)52.60966873168945, (float)39.567176818847656, (float)2000);
            // U-TURN START & END
            FindStartStopResult nodeStart = gr.FindNodeStart((float)52.64549811935865, (float)39.660279750823975, (float)2000);
            FindStartStopResult nodeEnd = gr.FindNodeEnd((float)52.60966873168945, (float)39.567176818847656, (float)2000);

            // test restrictions
            //FindStartStopResult nodeStart = gr.FindNodeStart((float)53.24475, (float)39.13105, (float)2000);
            //FindStartStopResult nodeEnd = gr.FindNodeEnd((float)53.24517, (float)39.1297, (float)2000);            

            // region route 2
            //FindStartStopResult nodeStart = gr.FindNodeStart((float)52.096, (float)37.95, (float)2000);
            //FindStartStopResult nodeEnd = gr.FindNodeEnd((float)53.36, (float)40.10, (float)2000);            

            DateTime s = DateTime.Now;
            Console.WriteLine(s.ToString("HH:mm:ss") + " Begin Calc");

            //gr.LoadExternalTimeInfoFile(...); .. Load Traffic
            gr.BeginSolve(true, null);
            gr.MinimizeRouteBy = MinimizeBy.Time;
            //gr.CalculataA(1,4); 
            //gr.Calculate(1, new uint[] { 4 });
            gr.SolveAstar(nodeStart.nodeN, nodeEnd.nodeN); // A*            
            //gr.SolveDeikstra(nodeStart.nodeN, new uint[] { nodeEnd.nodeN }); // NORMAL DEIKSTRA
            //gr.SolveDeikstra(new uint[] { nodeStart.nodeN }, nodeEnd.nodeN); // REVERSE DEIKSTRA
            //gr.SolveDeikstra(nodeStart.nodeN, new uint[] { nodeEnd.nodeN, nodeEnd.nodeN + 10, nodeEnd.nodeN+20 }); // NORMAL DEIKSTRA MULTI

            DateTime e = DateTime.Now;
            TimeSpan ts = e.Subtract(s);
            Console.WriteLine(e.ToString("HH:mm:ss") + " End Calc");
            Console.WriteLine("Elapsed: " + (ts.Minutes).ToString() + "m " + ts.Seconds.ToString() + "s " + ts.Milliseconds.ToString() + "ms");
            float c = gr.GetRouteCost(nodeStart.nodeN, nodeEnd.nodeN);
            float d = gr.GetRouteDistance(nodeStart.nodeN, nodeEnd.nodeN);
            float t = gr.GetRouteTime(nodeStart.nodeN, nodeEnd.nodeN);
            uint[] n = gr.GetRouteNodes(nodeStart.nodeN, nodeEnd.nodeN);
            float[] dd; float[] tt;
            uint[] r = gr.GetRouteLines(nodeStart.nodeN, n, nodeEnd.nodeN, out dd, out tt);
            PointFL[] v = gr.GetRouteVectorWNL(nodeStart.nodeN, n, nodeEnd.nodeN);

            /* ROUTE DESCRIPTION */
            RouteDescription rd = new RouteDescription(gr);
            RDPoint[] rdps = rd.GetDescription(v);
            rd.SaveTextDescription(rdps, XMLSaved<int>.GetCurrentDir() + @"\RMXSolver-DESC-WAY-LIST.txt");
            //string test_1 = rdps[2].ToString();
            /* END */

            PointF[] v1 = gr.GetRouteVector(nodeStart.nodeN, n, nodeEnd.nodeN);
            RouteResult rr = gr.GetRouteFull(nodeStart, nodeEnd, true, true);
            rdps = rd.GetDescription(rr, nodeStart, nodeEnd);
            rd.SaveTextDescription(rdps, XMLSaved<int>.GetCurrentDir() + @"\RMXSolver-DESC-WAY-LIST-2.txt");
            int[] linkids = gr.GetLinesLinkIDs(rr.lines);
            gr.EndSolve();

            // all-in-one check
            //RouteResult r2 = gr.GetRoute(new System.Drawing.PointF((float)39.52212452888, (float)52.580548586409186), new System.Drawing.PointF((float)39.5174072814941, (float)52.57637509148828));

            gr.Close();

            string js = ToJS(nodeStart.normal, rr.vector, nodeEnd.normal, "navy");

            FileStream fs = new FileStream(XMLSaved<int>.GetCurrentDir() + @"\RMXSolver-DESC-JS.txt", FileMode.Create, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs);
            sw.Write(js);
            sw.Close();
            fs.Close();

            Console.WriteLine("Press Enter key to continue...");
            Console.ReadLine();
        }
        #endregion        

        public static string ToJS(PointF[] start, PointFL[] route, PointF[] end, string routeColor)
        {
            string s = "";
            s += "var polystart = new GPolyline([\r\n";
            for (int i = 0; i < start.Length; i++)
                s += "new GLatLng(" + start[i].Y.ToString().Replace(",", ".") + ", " + start[i].X.ToString().Replace(",", ".") + ")"+(i<(start.Length-1)?",":"")+"\r\n";
			s += "], \"#FF0000\", 5);\r\n";
            s += "map.addOverlay(polystart);\r\n\r\n";

            s += "var polyroute = new GPolyline([\r\n";
            for (int i = 0; i < route.Length; i++)
                s += "new GLatLng(" + route[i].Y.ToString().Replace(",", ".") + ", " + route[i].X.ToString().Replace(",", ".") + ")" + (i < (route.Length - 1) ? "," : "") + "\r\n";
            s += "], \"" + routeColor + "\", 5);\r\n";
            s += "map.addOverlay(polyroute);\r\n\r\n";

            s += "var polyend = new GPolyline([\r\n";
            for (int i = 0; i < end.Length; i++)
                s += "new GLatLng(" + end[i].Y.ToString().Replace(",", ".") + ", " + end[i].X.ToString().Replace(",", ".") + ")" + (i < (end.Length - 1) ? "," : "") + "\r\n";
            s += "], \"#00FF00\", 5);\r\n";
            s += "map.addOverlay(polyend);\r\n\r\n";

            System.Windows.Forms.Clipboard.SetText(s);
            return s;
        }

        [STAThread]
        static void Main(string[] args)
        {
            //dkxce.Route.ServiceSolver.RussiaSolver.RussiaSolver_Test_PreService();
            //dkxce.Route.ServiceSolver.RussiaSolver.Russia_Test_SvcCmd();

            //dkxce.Route.ServiceSolver.OneRegionSolver.OneRegion_Test_PreService();
            //dkxce.Route.ServiceSolver.OneRegionSolver.OneRegion_Test_SvcCmd();            


            //  Tula - Kaluga - Smolensk
            //TulaKalugaSmolensk.CreateGraphs();
            //TulaKalugaSmolensk.CreateMatrix();
            //TulaKalugaSmolensk.CheckRoute_TuKaSm_A();
            //TulaKalugaSmolensk.CheckRoute_TuKaSm_B();
                        
            // Tula dir
            //Tula_ConvertShp();
            //Tula_Test();

            //Matrix4 dir
            //Lipetsk_ConvertOSM();
            //Lipetsk_TestOSM();

            // Matrix3 dir
            //Lipetsk_ConvertShp();
            //Lipetsk_CalcRG();
            //Lipetsk_Test();            
            
            // Matrix 2 dir
            //CSV2TXT(); // TEST.csv --> TEST.txt       
            
            // Matrix dir
            //CreateMatrix();
            //TestMatrix();

            //CreateGraphGeoFile();
            //TestGraphGeoFile();        
            
            //
            Lipetsk_Test_010();            
        }

        #region RGNodes Matrix EXAMPLE
        static void CreateMatrix()
        {
            string MX_file = XMLSaved<int>.GetCurrentDir() + @"\Matrix\matrix.bin";
            RMMatrix rm = RMMatrix.CreateInMemory(13);//.CreateOnDisk(13, mxPath);
            
            // Read Text Graph Info
            System.IO.FileStream fs = new FileStream(XMLSaved<int>.GetCurrentDir() + @"\Matrix\TextGraph.txt", FileMode.Open);
            System.IO.StreamReader sr = new StreamReader(fs);

            sr.ReadLine(); // 1st line - info
            while (!sr.EndOfStream)
            {
                string ln = sr.ReadLine();
                uint iFrom = uint.Parse(ln.Substring(0, ln.IndexOf("\t")));
                string[] itms = ln.Substring(ln.IndexOf("\t") + 1).Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < itms.Length; i++)
                {
                    string nnew = itms[i].Substring(0, itms[i].Length - 1);
                    uint iTo = uint.Parse(nnew.Substring(0, nnew.IndexOf("(")));
                    string[] cost_dist_speed = nnew.Substring(nnew.IndexOf("(") + 1).Split(new string[] { ";" }, StringSplitOptions.None);
                    uint lTo = uint.Parse(cost_dist_speed[0]);

                    // Params
                    Single lreal = Single.Parse(cost_dist_speed[1], rm.DotDelimiter); // km
                    Single time = Single.Parse(cost_dist_speed[2], rm.DotDelimiter); // minutes

                    rm.AddWay(iFrom, iTo, lTo, lreal, time, 0); // ADD INFO TO MATRIX
                };
            };
            fs.Close(); // CLOSE TEXT FILE

            rm.SaveToTextFile(XMLSaved<int>.GetCurrentDir() + @"\Matrix\CalcMatrixFU0.txt"); // save matrix to txt
            rm.Solve(); // calculate matrix
            rm.SaveToFile(MX_file); // save matrix to disk
            rm.SaveToTextFile(XMLSaved<int>.GetCurrentDir() + @"\Matrix\CalcMatrixFU.txt"); // save matrix to txt
            rm.Close(); // close matrix
        }

        static void TestMatrix()
        {
            string MX_file = XMLSaved<int>.GetCurrentDir() + @"\Matrix\matrix.bin";
            RMMatrix rm = RMMatrix.LoadToMemory(MX_file);
            
            uint uFrom = 5; // start node
            uint uTo = 10; // end node

            double c1 = rm.GetRouteCost(uFrom, uTo); // read params
            double l = rm.GetRouteDist(uFrom, uTo); // ..
            double t = rm.GetRouteTime(uFrom, uTo);
            uint[] arr = rm.GetRouteWay(uFrom, uTo); // ..
            rm.Close(); // close matrix        
        }
        #endregion
    }
}
