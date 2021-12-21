using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;

using dkxce.Route.Classes;
using dkxce.Route.Shp2Rt;
using dkxce.Route.GSolver;
using dkxce.Route.Regions;
using dkxce.Route.Matrix;

namespace CNSL
{
    public class TulaKalugaSmolensk
    {
        public static void CreateGraphs()
        {
            string shpf = XMLSaved<int>.GetCurrentDir() + @"\Matrix6\Tula.shp";
            ShpToGraphConverter con = new ShpToGraphConverter(shpf);
            con.WriteLinesNamesFile = true;
            con.ConvertTo(XMLSaved<int>.GetCurrentDir() + @"\Matrix6\G\Tula.rt");

            RMGraph gr = RMGraph.LoadToMemory(XMLSaved<int>.GetCurrentDir() + @"\Matrix6\G\Tula.rt", 17);
            gr.MinimizeRouteBy = MinimizeBy.Time;
            gr.CalculateRGNodesRoutes(17); // Расчет путей между точками стыковки межрайонных маршрутов
            gr.Close();

            shpf = XMLSaved<int>.GetCurrentDir() + @"\Matrix6\Kaluga.shp";
            con = new ShpToGraphConverter(shpf);
            con.WriteLinesNamesFile = true;
            con.ConvertTo(XMLSaved<int>.GetCurrentDir() + @"\Matrix6\G\Kaluga.rt");

            gr = RMGraph.LoadToMemory(XMLSaved<int>.GetCurrentDir() + @"\Matrix6\G\Kaluga.rt", 7);
            gr.MinimizeRouteBy = MinimizeBy.Time;
            gr.CalculateRGNodesRoutes(7); // Расчет путей между точками стыковки межрайонных маршрутов
            gr.Close();

            shpf = XMLSaved<int>.GetCurrentDir() + @"\Matrix6\Smolensk.shp";
            con = new ShpToGraphConverter(shpf);
            con.WriteLinesNamesFile = true;
            con.ConvertTo(XMLSaved<int>.GetCurrentDir() + @"\Matrix6\G\Smolensk.rt");

            gr = RMGraph.LoadToMemory(XMLSaved<int>.GetCurrentDir() + @"\Matrix6\G\Smolensk.rt",14);
            gr.MinimizeRouteBy = MinimizeBy.Time;
            gr.CalculateRGNodesRoutes(14); // Расчет путей между точками стыковки межрайонных маршрутов
            gr.Close();

            shpf = XMLSaved<int>.GetCurrentDir() + @"\Matrix6\Lipetsk.shp";
            con = new ShpToGraphConverter(shpf);
            con.WriteLinesNamesFile = true;
            con.ConvertTo(XMLSaved<int>.GetCurrentDir() + @"\Matrix6\G\Lipetsk.rt");

            gr = RMGraph.LoadToMemory(XMLSaved<int>.GetCurrentDir() + @"\Matrix6\G\Lipetsk.rt", 10);
            gr.MinimizeRouteBy = MinimizeBy.Time;
            gr.CalculateRGNodesRoutes(10); // Расчет путей между точками стыковки межрайонных маршрутов
            gr.Close();            


            Console.WriteLine("Press Enter key to continue...");
            Console.ReadLine();
        }

        public static void CreateMatrix()
        {
            string path = XMLSaved<int>.GetCurrentDir() + @"\Matrix6\G\";
            RMMatrix rm = RMMatrix.CreateInMemory(10);

            string[] files = System.IO.Directory.GetFiles(path, "*.rgnodes.xml");
            foreach (string file in files)
            {
                TRGNode[] nds = XMLSaved<TRGNode[]>.Load(file);
                for (int i = 0; i < nds.Length; i++)
                    for (int ito = 0; ito < nds[i].links.Length; ito++)
                        rm.AddWay((uint)nds[i].id, (uint)nds[i].links[ito], nds[i].costs[ito], nds[i].dists[ito], nds[i].times[ito],0);
            };

            rm.SaveToTextFile(path+"_matrix0.txt");
            rm.Solve(); // calculate matrix
            rm.SaveToFile(path + "_matrix.bin"); // save matrix to disk
            rm.SaveToTextFile(path + "_matrix1.txt");
            rm.Close(); // close matrix
        }

        public static void CheckRoute_TuKaSm_A()
        {
            string path = XMLSaved<int>.GetCurrentDir() + @"\Matrix5\G\";

            uint[] start = new uint[] { 1, 2, 3 }; // Tula
            uint[] sNodes = new uint[] { 34740 /*1*/, 34741 /*2*/, 34761 /*3*/};

            uint[] end = new uint[] { 4, 5 }; // Smolens
            uint[] eNodes = new uint[] { 65916 /*4*/, 25196 /*5*/};

            int s_elem = -1;
            int e_elem = -1;
            
            /////////////////////////

            PointInRegionUtils ru = new PointInRegionUtils();
            ru.LoadRegionsFromFile(XMLSaved<int>.GetCurrentDir() + @"\regions.shp");
            int from_reg = ru.PointInRegion(53.1517, 38.056);
            string from_file = ru.RegionFileByRegionId(from_reg);
            int to_reg = ru.PointInRegion(55.6030, 31.1754);
            string to_file = ru.RegionFileByRegionId(to_reg);


            // TULA
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " Загрузка Тулы в память");
            RMGraph grS = RMGraph.LoadToMemory(path + from_reg /*"Tula.rt"*/, 17);
            FindStartStopResult nodeStart = grS.FindNodeStart((float)53.1517, (float)38.056, (float)2000);

            DateTime s1 = DateTime.Now;
            Console.WriteLine(s1.ToString("HH:mm:ss") + " Begin Calc");

            grS.BeginSolve(true, null);
            grS.MinimizeRouteBy = MinimizeBy.Time;
            grS.SolveDeikstra(nodeStart.nodeN, sNodes); // NORMAL DEIKSTRA

            DateTime e1 = DateTime.Now;
            TimeSpan ts1 = e1.Subtract(s1);
            Console.WriteLine(e1.ToString("HH:mm:ss") + " End Calc");
            Console.WriteLine("Elapsed: " + (ts1.Minutes).ToString() + "m " + ts1.Seconds.ToString() + "s " + ts1.Milliseconds.ToString() + "ms");

            
            // SMOLENSK           
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " Загрузка Смоленска в память");
            RMGraph grE = RMGraph.LoadToMemory(path + to_reg /*"Smolensk.rt"*/, 14);
            FindStartStopResult nodeEnd = grE.FindNodeEnd((float)55.6030, (float)31.1754, (float)2000);

            DateTime s2 = DateTime.Now;
            Console.WriteLine(s2.ToString("HH:mm:ss") + " Begin Calc");

            grE.BeginSolve(true, null);
            grE.MinimizeRouteBy = MinimizeBy.Time;
            grE.SolveDeikstra(eNodes, nodeEnd.nodeN); // REVERSE DEIKSTRA

            DateTime e2 = DateTime.Now;
            TimeSpan ts2 = e2.Subtract(s2);
            Console.WriteLine(e2.ToString("HH:mm:ss") + " End Calc");
            Console.WriteLine("Elapsed: " + (ts2.Minutes).ToString() + "m " + ts2.Seconds.ToString() + "s " + ts2.Milliseconds.ToString() + "ms");

            // Matrix
            RMMatrix rm = RMMatrix.LoadToMemory(path + "_matrix.bin");

            // merge
            double time = double.MaxValue;
            for(int x=0;x<start.Length;x++)
                for (int y = 0; y < end.Length; y++)
                {
                    float time_segment_A = grS.GetRouteTime(nodeStart.nodeN, sNodes[x]);
                    float time_segment_B = rm.GetRouteTime(start[x], end[y]);
                    float time_segment_C = grE.GetRouteTime(eNodes[y], nodeEnd.nodeN);
                    double ttl_time = time_segment_A + time_segment_B + time_segment_C;
                    if (ttl_time < time)
                    {
                        time = ttl_time;
                        s_elem = x;
                        e_elem = y;
                    };
                };
            // end merge

            float time_A = grS.GetRouteTime(nodeStart.nodeN, sNodes[s_elem]);
            float dist_A = grS.GetRouteDistance(nodeStart.nodeN, sNodes[s_elem]);
            float cost_A = grS.GetRouteCost(nodeStart.nodeN, sNodes[s_elem]);
            uint[] nodes_A = grS.GetRouteNodes(nodeStart.nodeN, sNodes[s_elem]);
            PointF[] points_A = grS.GetRouteVector(nodeStart.nodeN, nodes_A, sNodes[s_elem]);

            float time_B = rm.GetRouteTime(start[s_elem], end[e_elem]);
            float dist_B = rm.GetRouteDist(start[s_elem], end[e_elem]);
            float cost_B = rm.GetRouteCost(start[s_elem], end[e_elem]);
            RouteResultStored nodes_B = XMLSaved<RouteResultStored>.Load(path + String.Format(@"RGWays\{0}T{1}.rgway.xml", start[s_elem], end[e_elem]));
            PointFL[] points_B = nodes_B.route.vector;

            float time_C = grE.GetRouteTime(eNodes[e_elem],nodeEnd.nodeN);
            float dist_C = grE.GetRouteDistance(eNodes[e_elem], nodeEnd.nodeN);
            float cost_C = grE.GetRouteCost(eNodes[e_elem], nodeEnd.nodeN);
            uint[] nodes_C = grE.GetRouteNodes(eNodes[e_elem], nodeEnd.nodeN);
            PointF[] points_C = grE.GetRouteVector(eNodes[e_elem], nodes_C, nodeEnd.nodeN);

            // Tula
            grS.EndSolve();
            grS.Close();

            // Smol
            grE.EndSolve();
            grE.Close();

            rm.Close(); // close matrix

            double time_ttl = time_A + time_B + time_C;
            double dist_ttl = dist_A + dist_B + dist_C;

            Console.WriteLine("Press Enter key to continue...");
            ToJS(points_A, points_B, points_C);
            Console.ReadLine();
        }

        public static void CheckRoute_TuKaSm_B()
        {
            string path = XMLSaved<int>.GetCurrentDir() + @"\Matrix6\G\";

            ///////////////////////
            // START - END
            ///////////////////////
            //double sLat = 53.1406; // tula
            //double sLon = 38.110; // tula
            //double sLat = 54.479; // kaluga
            //double sLon = 36.357; // kaluga
            //double sLat = 55.6030; // smolensk
            //double sLon = 31.1754; // smolensk 
            double sLat = 54.128; // smolensk2
            double sLon = 33.344; // smolensk2
            //double sLat = 52.160; // lipetsk
            //double sLon = 40.479; // lipetsk
            //double sLat = 52.105; // lipetsk2
            //double sLon = 39.176; // lipetsk2
            //double sLat = 53.365; // lipetsk3
            //double sLon = 40.10; // lipetsk3

            //double eLat = 53.793; // tula
            //double eLon = 36.158; // tula
            //double eLat = 54.236; // kaluga
            //double eLon = 33.71; // kaluga                        
            //double eLat = 54.977; // smolensk0
            //double eLon = 31.006; // smolensk0
            //double eLat = 55.6030; // smolensk1
            //double eLon = 31.1754; // smolensk1
            //double eLat = 53.933; // smolensk2
            //double eLon = 32.78; // smolensk2
            //double eLat = 55.915; // smolensk3
            //double eLon = 33.710; // smolensk3             
            //double eLat = 52.105; // lipetsk
            //double eLon = 39.176; // lipetsk
            double eLat = 52.446; // lipetsk2
            double eLon = 37.98; // lipetsk2
            ///////////////////////
            ///////////////////////

            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " Поиск оптимального маршрута");
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " "+sLat.ToString()+" "+sLon.ToString()+" -> "+eLat.ToString()+" "+eLon.ToString());
            Console.Write(DateTime.Now.ToString("HH:mm:ss") + " Попадание START: ");

            ///////////////////////
            // GET REGIONS POINTS IN
            ///////////////////////
            PointInRegionUtils ru = new PointInRegionUtils();
            ru.LoadRegionsFromFile(path + "regions.shp");
            int sReg = ru.PointInRegion(sLat, sLon);
            string sFile = ru.RegionFileByRegionId(sReg);
            string sName = ru.RegionNameByRegionId(sReg);
            Console.WriteLine(sReg.ToString() + " " + sName+" ["+sFile+"]");
            Console.Write(DateTime.Now.ToString("HH:mm:ss") + " Попадание FINISH: ");
            int eReg = ru.PointInRegion(eLat, eLon);
            string eFile = ru.RegionFileByRegionId(eReg);
            string eName = ru.RegionNameByRegionId(eReg);
            Console.WriteLine(eReg.ToString() + " " + eName + " [" + eFile + "]");
            ///////////////////////
            /////////////////////// 

            if (sReg < 1) throw new Exception("Точка старта не попадает в зону доступной карты!");
            if (sReg < 1) throw new Exception("Точка старта не попадает в зону доступной карты!");
            if (sFile == "") throw new Exception("Для региона `" + sName + "` маршруты недоступны!");
            if (eFile == "") throw new Exception("Для региона `" + eName + "` маршруты недоступны!");

            if (sReg != eReg)
            {
                Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " Поиск межрегиональных узлов в регионах "+sReg.ToString()+" и "+eReg.ToString());
                ///////////////////////
                // GET RGNODES
                ///////////////////////
                List<uint> sRGNodes = new List<uint>();
                List<uint> sGraphNodes = new List<uint>();
                List<float> sTimeToRGNode = new List<float>();
                foreach (TRGNode rgnode in XMLSaved<TRGNode[]>.Load(path + System.IO.Path.GetFileNameWithoutExtension(sFile) + ".rgnodes.xml"))
                    if (rgnode.outer)
                    {
                        sRGNodes.Add((uint)rgnode.id);
                        sGraphNodes.Add(rgnode.node);
                        sTimeToRGNode.Add(Utils.GetLengthMeters(sLat, sLon, rgnode.lat, rgnode.lon, false) / 1000); // 60 km/h
                    };
                List<uint> eRGNodes = new List<uint>();
                List<uint> eGraphNodes = new List<uint>();
                List<float> eTimeFromRGNode = new List<float>();
                foreach (TRGNode rgnode in XMLSaved<TRGNode[]>.Load(path + System.IO.Path.GetFileNameWithoutExtension(eFile) + ".rgnodes.xml"))
                    if (rgnode.inner)
                    {
                        eRGNodes.Add((uint)rgnode.id);
                        eGraphNodes.Add(rgnode.node);
                        eTimeFromRGNode.Add(Utils.GetLengthMeters(eLat, eLon, rgnode.lat, rgnode.lon, false) / 1000); // 60 km/h
                    };
                ///////////////////////
                ///////////////////////

                int start_matrix_element = -1;
                int end_matrix_element = -1;
                
                ///////////////////////
                // GET MATRIX ROUTE, & calculate shortest way (to_matrix+in_matrix)
                ///////////////////////
                Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " Загрузка матрицы межрегиональных маршрутов");
                RMMatrix rm = RMMatrix.LoadToMemory(path + "_matrix.bin");
                Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " Поиск маршрута " + sReg.ToString() + " -> " + eReg.ToString());
                double time_abc = double.MaxValue;
                for (int x = 0; x < sRGNodes.Count; x++)
                    for (int y = 0; y < eRGNodes.Count; y++)
                    {
                        float ttl_time = sTimeToRGNode[x] + rm.GetRouteTime(sRGNodes[x], eRGNodes[y]) + eTimeFromRGNode[y];
                        if (ttl_time < time_abc)
                        {
                            time_abc = ttl_time;
                            start_matrix_element = x;
                            end_matrix_element = y;
                        };
                    };
                Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " Кротчайший маршрут: [" + sRGNodes[start_matrix_element].ToString() + "] -> [" + eRGNodes[end_matrix_element].ToString()+"]");
                float dist_B = rm.GetRouteDist(sRGNodes[start_matrix_element], eRGNodes[end_matrix_element]);
                float time_B = rm.GetRouteTime(sRGNodes[start_matrix_element], eRGNodes[end_matrix_element]);
                float cost_B = rm.GetRouteCost(sRGNodes[start_matrix_element], eRGNodes[end_matrix_element]);
                RouteResultStored nodes_B = new RouteResultStored();
                List<PointFL> points_B = new List<PointFL>();
                if (dist_B > 0)
                {
                    List<uint> frt = new List<uint>();
                    frt.Add(sRGNodes[start_matrix_element]);
                    uint[] mdl = rm.GetRouteWay(sRGNodes[start_matrix_element], eRGNodes[end_matrix_element]);
                    frt.AddRange(mdl);
                    frt.Add(eRGNodes[end_matrix_element]);
                    for (int i = 1; i < frt.Count; i++)
                    {
                        nodes_B = XMLSaved<RouteResultStored>.Load(path + String.Format(@"RGWays\{0}T{1}.rgway.xml", frt[i-1], frt[i]));
                        points_B.AddRange(nodes_B.route.vector);
                    };
                };
                rm.Close();
                ///////////////////////
                ///////////////////////

                ///////////////////////
                // FROM ROUTE
                ///////////////////////
                DateTime s1 = DateTime.Now;
                Console.Write(s1.ToString("HH:mm:ss") + " " + sFile + ", загрузка");
                RMGraph grS = RMGraph.LoadToMemory(path + sFile /*"Tula.rt"*/, 17);                
                DateTime e1 = DateTime.Now;
                TimeSpan ts1 = e1.Subtract(s1);
                Console.WriteLine(" - " + (ts1.Minutes).ToString() + "m " + ts1.Seconds.ToString() + "s " + ts1.Milliseconds.ToString() + "ms (" + e1.ToString("HH:mm:ss") + ")");

                Console.Write(DateTime.Now.ToString("HH:mm:ss") + " Ближайший узел к " + sLat.ToString() + " " + sLon.ToString() + " - ");
                FindStartStopResult nodeStart = grS.FindNodeStart((float)sLat, (float)sLon, (float)2000);
                Console.WriteLine(nodeStart.nodeN);
                if (nodeStart.nodeN == 0) throw new Exception("Не найдены дороги вблизи точки старта");

                s1 = DateTime.Now;
                Console.Write(s1.ToString("HH:mm:ss") + " Поиск маршрута " + nodeStart.nodeN.ToString() + " -> [" + sRGNodes[start_matrix_element].ToString() + "]");

                grS.BeginSolve(true, null);
                grS.MinimizeRouteBy = MinimizeBy.Time;
                grS.SolveAstar(nodeStart.nodeN, sGraphNodes[start_matrix_element]);

                e1 = DateTime.Now;
                ts1 = e1.Subtract(s1);
                Console.WriteLine(" - " + (ts1.Minutes).ToString() + "m " + ts1.Seconds.ToString() + "s " + ts1.Milliseconds.ToString() + "ms (" + e1.ToString("HH:mm:ss") + ")");

                float time_A = grS.GetRouteTime(nodeStart.nodeN, sGraphNodes[start_matrix_element]);
                float dist_A = grS.GetRouteDistance(nodeStart.nodeN, sGraphNodes[start_matrix_element]);
                float cost_A = grS.GetRouteCost(nodeStart.nodeN, sGraphNodes[start_matrix_element]);
                uint[] nodes_A = grS.GetRouteNodes(nodeStart.nodeN, sGraphNodes[start_matrix_element]);
                PointF[] points_A = grS.GetRouteVector(nodeStart.nodeN, nodes_A, sGraphNodes[start_matrix_element]);
                grS.EndSolve();
                grS.Close();
                ///////////////////////
                ///////////////////////

                ///////////////////////
                // TO ROUTE
                ///////////////////////  
                s1 = DateTime.Now;
                Console.Write(s1.ToString("HH:mm:ss") + " " + eFile + ", загрузка");
                RMGraph grE = RMGraph.LoadToMemory(path + eFile /*"Smolensk.rt"*/, 14);                

                e1 = DateTime.Now;
                ts1 = e1.Subtract(s1);
                Console.WriteLine(" - " + (ts1.Minutes).ToString() + "m " + ts1.Seconds.ToString() + "s " + ts1.Milliseconds.ToString() + "ms (" + e1.ToString("HH:mm:ss") + ")");
                
                Console.Write(DateTime.Now.ToString("HH:mm:ss") + " Ближайший узел к " + eLat.ToString() + " " + eLon.ToString() + " - ");
                FindStartStopResult nodeEnd = grE.FindNodeEnd((float)eLat, (float)eLon, (float)2000);
                Console.WriteLine(nodeEnd.nodeN);
                if (nodeEnd.nodeN == 0) throw new Exception("Не найдены дороги вблизи точки финиша");

                s1 = DateTime.Now;
                Console.Write(s1.ToString("HH:mm:ss") + " Поиск маршрута [" + eRGNodes[end_matrix_element].ToString() + "] -> "+nodeEnd.nodeN.ToString());

                grE.BeginSolve(true, null);
                grE.MinimizeRouteBy = MinimizeBy.Time;
                grE.SolveAstar(eGraphNodes[end_matrix_element], nodeEnd.nodeN);

                e1 = DateTime.Now;
                ts1 = e1.Subtract(s1);
                Console.WriteLine(" - " + (ts1.Minutes).ToString() + "m " + ts1.Seconds.ToString() + "s " + ts1.Milliseconds.ToString() + "ms (" + e1.ToString("HH:mm:ss") + ")");
                Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " Маршрут построен");

                float time_C = grE.GetRouteTime(eGraphNodes[end_matrix_element], nodeEnd.nodeN);
                float dist_C = grE.GetRouteDistance(eGraphNodes[end_matrix_element], nodeEnd.nodeN);
                float cost_C = grE.GetRouteCost(eGraphNodes[end_matrix_element], nodeEnd.nodeN);
                uint[] nodes_C = grE.GetRouteNodes(eGraphNodes[end_matrix_element], nodeEnd.nodeN);
                PointF[] points_C = grE.GetRouteVector(eGraphNodes[end_matrix_element], nodes_C, nodeEnd.nodeN);
                grE.EndSolve();
                grE.Close();
                ///////////////////////
                ///////////////////////            

                double time_ttl = time_A + time_B + time_C;
                double dist_ttl = dist_A + dist_B + dist_C;
                Console.WriteLine("Общее время: " + time_ttl.ToString() + " мин");
                Console.WriteLine("Общее расстояние: " + (dist_ttl / 1000).ToString() + " км");

                Console.WriteLine("Press Enter key to continue...");
                ToJS(points_A, points_B.ToArray(), points_C);
            }
            else
            {
                ///////////////////////
                // IN-ONE-REGION ROUTE
                ///////////////////////  
                DateTime s1 = DateTime.Now;
                Console.Write(s1.ToString("HH:mm:ss") + " " + sFile + ", загрузка");
                RMGraph grE = RMGraph.LoadToMemory(path + sFile, 0);
                FindStartStopResult nodeStart = grE.FindNodeEnd((float)sLat, (float)sLon, (float)2000);
                FindStartStopResult nodeEnd = grE.FindNodeEnd((float)eLat, (float)eLon, (float)2000);

                DateTime e1 = DateTime.Now;
                TimeSpan ts1 = e1.Subtract(s1);
                Console.WriteLine(" - " + (ts1.Minutes).ToString() + "m " + ts1.Seconds.ToString() + "s " + ts1.Milliseconds.ToString() + "ms (" + e1.ToString("HH:mm:ss") + ")");

                s1 = DateTime.Now;
                Console.Write(s1.ToString("HH:mm:ss") + " Поиск маршрута");

                grE.BeginSolve(true, null);
                grE.MinimizeRouteBy = MinimizeBy.Time;
                grE.SolveAstar(nodeStart.nodeN, nodeEnd.nodeN);

                e1 = DateTime.Now;
                ts1 = e1.Subtract(s1);
                Console.WriteLine(" - " + (ts1.Minutes).ToString() + "m " + ts1.Seconds.ToString() + "s " + ts1.Milliseconds.ToString() + "ms (" + e1.ToString("HH:mm:ss") + ")");
                Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " Маршрут построен");

                float time_C = grE.GetRouteTime(nodeStart.nodeN, nodeEnd.nodeN);
                float dist_C = grE.GetRouteDistance(nodeStart.nodeN, nodeEnd.nodeN);
                float cost_C = grE.GetRouteCost(nodeStart.nodeN, nodeEnd.nodeN);
                uint[] nodes_C = grE.GetRouteNodes(nodeStart.nodeN, nodeEnd.nodeN);
                PointF[] points_C = grE.GetRouteVector(nodeStart.nodeN, nodes_C, nodeEnd.nodeN);
                grE.EndSolve();
                grE.Close();
                ///////////////////////
                /////////////////////// 

                Console.WriteLine("Общее время: " + time_C.ToString() + " мин");
                Console.WriteLine("Общее расстояние: " + (dist_C / 1000).ToString() + " км");

                Console.WriteLine("Press Enter key to continue...");
                ToJS(new PointF[0], new PointFL[0], points_C);
            };
            Console.ReadLine();
        }

        private static void ToJS(PointF[] A, PointFL[] B, PointF[] C)
        {
            string s = "";
            s += "var polyA = new GPolyline([\r\n";
            for (int i = 0; i < A.Length; i++)
                s += "new GLatLng(" + A[i].Y.ToString().Replace(",", ".") + ", " + A[i].X.ToString().Replace(",", ".") + ")" + (i < (A.Length - 1) ? "," : "") + "\r\n";
            s += "], \"red\", 5);\r\n";
            s += "map.addOverlay(polyA);\r\n\r\n";

            s += "var polyB = new GPolyline([\r\n";
            for (int i = 0; i < B.Length; i++)
                s += "new GLatLng(" + B[i].Y.ToString().Replace(",", ".") + ", " + B[i].X.ToString().Replace(",", ".") + ")" + (i < (B.Length - 1) ? "," : "") + "\r\n";
            s += "], \"blue\", 5);\r\n";
            s += "map.addOverlay(polyB);\r\n\r\n";

            s += "var polyC = new GPolyline([\r\n";
            for (int i = 0; i < C.Length; i++)
                s += "new GLatLng(" + C[i].Y.ToString().Replace(",", ".") + ", " + C[i].X.ToString().Replace(",", ".") + ")" + (i < (C.Length - 1) ? "," : "") + "\r\n";
            s += "], \"green\", 5);\r\n";
            s += "map.addOverlay(polyC);\r\n\r\n";

            System.Windows.Forms.Clipboard.SetText(s);
        }
    }
}
