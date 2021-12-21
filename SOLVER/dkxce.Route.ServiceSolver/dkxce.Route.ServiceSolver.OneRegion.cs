using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.IO;

using System.Xml;
using System.Xml.Serialization;
using System.Configuration.Install;

using System.Threading;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Serialization.Formatters;

using dkxce.Route.Classes;
using dkxce.Route.GSolver;
using dkxce.Route.ISolver;
using dkxce.Route.WayList;

namespace dkxce.Route.ServiceSolver
{
    public class OneRegionSolver : MarshalByRefObject, IRoute
    {
        // Multithread vars
        private bool keepAlive = false;

        public static string GraphFile = null;

        private RMGraph gr;
        private RouteDescription rd;

        public OneRegionSolver()
        {            
            Init();
        }
        
        public OneRegionSolver(bool keepAlive)
        {
            this.keepAlive = keepAlive;
            Init();
        }

        public ulong GraphSize
        {
            get
            {
                return gr.GraphSize;
            }
        }

        private void Init()
        {
            if ((GraphFile == null) || (GraphFile == ""))
            {
                XMLSaved<int>.AddErr2SysLog("[OneRegionSolver] You must set fileName to `GraphFile` value or call OneRegionSolver(fileName)");
                throw new Exception("You must set fileName to `GraphFile` value or call OneRegionSolver(fileName)");
            };
            if (!File.Exists(GraphFile)) GraphFile = System.IO.Path.GetFullPath(XMLSaved<int>.GetCurrentDir() + @"\" + GraphFile);
            if (!File.Exists(GraphFile))
            {
                XMLSaved<int>.AddErr2SysLog("[OneRegionSolver] Route file " + GraphFile + " not found!");
                throw new Exception("Route file " + GraphFile + " not found!");
            };

            gr = RMGraph.LoadToMemory(GraphFile, 0);
            rd = new RouteDescription(gr);
        }

        public RNearRoad[] GetNearRoadPrivate(double[] lat, double[] lon, bool getNames)
        {
            if ((lat == null) || (lon == null) || (lat.Length == 0) || (lon.Length == 0) || (lat.Length != lon.Length))
            {
                return new RNearRoad[] { new RNearRoad(0, 0, 0, "Error: 011 Неверно указаны точки") };
            };
            if (lat.Length > 1000)
            {
                return new RNearRoad[] { new RNearRoad(0, 0, 0, "Error: 015 Вы первысили максимально допустимое число точек - 1000") };
            };

            RNearRoad[] res = new RNearRoad[lat.Length];
            uint[] lines = new uint[lat.Length];
            if (res.Length > 0)
            {
                for (int i = 0; i < res.Length; i++)
                {
                    res[i] = new RNearRoad(0, 0, 0, "", "", gr.RegionID);
                    //float[][] NRS = gr.FindNearestLines((float)lat[i], (float)lon[i], (float)2000);
                    uint line;
                    PointF ll = gr.PointToNearestLine((float)lat[i], (float)lon[i], (float)2000, out res[i].distance, out line);
                    if ((line == 0) || (line == uint.MaxValue))
                    {
                        res[i].distance = double.MaxValue;
                        continue;
                    };                    
                    res[i].lat = ll.Y;
                    res[i].lon = ll.X;
                    res[i].attributes = "";
                    TLineFlags lf = gr.GetLineFlags(line);
                    if (lf.HasAttributes)
                        res[i].attributes = gr.AttributesToString(gr.GetLinesAttributes(new uint[] { line })[0]);
                    if (lf.IsOneWay)
                        res[i].attributes += (res[i].attributes.Length > 0 ? " 1w" : "1w");
                    float d, t;
                    if (gr.GetLineLT(line, out d, out t))
                    {
                        double speed = d / t / 1000.0 * 60;
                        res[i].attributes += (res[i].attributes.Length > 0 ? " avg" : "avg") +
                            speed.ToString("0.");
                    };
                    res[i].region = gr.RegionID;
                    lines[i] = line;
                };
                if (getNames)
                {
                    string[] names = rd.GetRoadName(lines);
                    for (int i = 0; i < res.Length; i++) 
                            res[i].name = names[i];
                };
            };
            return res;
        }

        /// <summary>
        ///     Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик
        ///         0x08 - использовать исторический трафик
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация) 
        ///         0x20 - оптимизировать по расстоянию
        ///         0x40 - допускать выезд на дорогу и съезд через встречную полосу
        /// </summary>
        /// <param name="stops"></param>
        /// <param name="startTime"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        private RResult GetRoutePrivate(RStop[] stops, DateTime startTime, long flags, int[] RegionsAvailableToUser,
            PointF[] roadsExcept, double roadsExceptRadiusInMeters, byte[] roadsOnly)
        {
            bool getPolyline = (flags & 0x01) > 0;
            bool getDirections = (flags & 0x02) > 0;
            bool getCurrentTraffic = (flags & 0x04) > 0;
            bool getHistoryTraffic = (flags & 0x08) > 0;
            bool optimiseTSP = (flags & 0x10) > 0;      
            bool optimizeByDist = (flags & 0x20) > 0;
            bool allowLeftTurns = (flags & 0x40) > 0;

            if (optimiseTSP)
                TSP.OptimizeWayRoute(stops);

            if ((roadsOnly != null) && (roadsOnly.Length == 16))
            {
                int c = 0;
                for (int i = 0; i < roadsOnly.Length; i++) c += roadsOnly[i];
                if (c == 0) roadsOnly = null;
            }
            else roadsOnly = null;
            
            gr.TrafficUseCurrent = getCurrentTraffic;
            gr.TrafficUseHistory = getHistoryTraffic;

            RResult res = new RResult(stops);
            if ((stops == null) || (stops.Length < 2))
            {
                res.LastError = "011 Необходимо минимум 2 точки";
                return res;
            };
            if (stops.Length > 100)
            {
                res.LastError = "015 Вы первысили максимально допустимое число точек - 100";
                return res;
            };

            res.driveLengthSegments = new double[stops.Length];
            res.driveTimeSegments = new double[stops.Length];

            res.startTime = startTime;
            res.finishTime = startTime;
            List<PointFL> vector = new List<PointFL>();
            List<RDPoint> description = new List<RDPoint>();

            for (int i = 1; i < stops.Length; i++)
            {
                gr.TrafficStartTime = res.finishTime;

                FindStartStopResult start = gr.FindNodeStart((float)stops[i - 1].lat, (float)stops[i - 1].lon, 2000);
                FindStartStopResult end = gr.FindNodeEnd((float)stops[i].lat, (float)stops[i].lon, 2000);

                if (start.nodeN == 0)
                {
                    res.LastError = "014 Не найдены дороги вблизи точки `" + stops[i - 1].name + "`";
                    return res;
                };
                if (end.nodeN == 0)
                {
                    res.LastError = "014 Не найдены дороги вблизи точки `" + stops[i].name + "`";
                    return res;
                };

                gr.GoExcept.Clear();
                gr.GoThrough = null;
                if (true) // UseGoLogic
                {
                    if ((roadsExcept != null) && (roadsExcept.Length != 0))
                        for (int r = 0; r < roadsExcept.Length; r++)
                        {
                            float[][] nl = gr.FindNearestLines(roadsExcept[r].Y, roadsExcept[r].X, (float)roadsExceptRadiusInMeters * 2);
                            if ((nl != null) && (nl.Length > 0))
                                foreach (float[] nll in nl)
                                    if (nll[0] <= roadsExceptRadiusInMeters)
                                        gr.GoExcept.Add((uint)(nll[3]));//0-distance,1-x,2-y,3-line
                        };
                    if((roadsOnly != null) && (roadsOnly.Length == 16)) gr.GoThrough = roadsOnly;
                };

                gr.BeginSolve(true, null);
                gr.MinimizeRouteBy = optimizeByDist ? MinimizeBy.Dist : MinimizeBy.Time;

                // Added 21.12.2021 where 1 line in route for WATER
                RouteResult rr;
                if (allowLeftTurns && (start.line == end.line))
                {
                    rr = gr.GetFullRouteOneLine(start, end);
                }
                else
                {
                    //gr.SolveDeikstra(new uint[] { start.nodeN }, end.nodeN);
                    gr.SolveAstar(start.nodeN, end.nodeN);
                    rr = gr.GetRouteFull(start, end, true, allowLeftTurns);
                };
                
                //gr.EndSolve();

                if (rr.length > RMGraph.const_maxValue)
                {
                    res.LastError = "100 Маршрут `" + stops[i - 1].name + "` - `" + stops[i].name + "` не найден";
                    return res;
                };

                if (getPolyline)
                {
                    for (int x = 0; x < start.normal.Length; x++)
                        vector.Add(new PointFL(start.normal[x], 0, 0, (x > 0) && ((rr.vector != null) && (rr.vector.Length != 0)) ? rr.vector[0].speed : 20));
                    vector.AddRange(rr.vector);
                    for (int x = 0; x < end.normal.Length; x++)
                        vector.Add(new PointFL(end.normal[x], 0, 0, (x < (end.normal.Length - 1)) && ((rr.vector != null) && (rr.vector.Length != 0)) ? rr.vector[rr.vector.Length-1].speed : 20));
                };

                // DESCRIPTION

                RDPoint[] desc;
                if (getDirections)
                {
                    desc = rd.GetDescription(rr, start, end);
                    if (desc.Length > 0)
                    {
                        desc[0].name = stops[i - 1].name;
                        desc[desc.Length - 1].name = stops[i].name;
                        for (int x = 0; x < desc.Length; x++)
                        {
                            desc[x].dist += (float)res.driveLength;
                            desc[x].time += (float)res.driveTime;
                            description.Add(desc[x]);
                        };
                    };
                };

                res.finishTime = res.finishTime.AddMinutes(rr.time);
                res.driveLength += rr.length;
                res.driveTime += rr.time;

                gr.EndSolve();

                if ((vector.Count > 0) && (res.vectorSegments == null)) res.vectorSegments = new int[stops.Length];
                if (res.vectorSegments != null) res.vectorSegments[i] = vector.Count;
                if ((description.Count > 0) && (res.descriptionSegments == null)) res.descriptionSegments = new int[stops.Length];
                if (res.descriptionSegments != null) res.descriptionSegments[i] = description.Count;

                res.driveLengthSegments[i] = res.driveLength;
                res.driveTimeSegments[i] = res.driveTime;
            };

            if (res.vectorSegments != null) res.vectorSegments[stops.Length - 1]--;
            if (res.descriptionSegments != null) res.descriptionSegments[stops.Length - 1]--;
            
            res.vector = vector.ToArray();
            res.description = description.ToArray();
            return res;
        }

        /// <summary>
        /// Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик
        ///         0x08 - использовать исторический трафик
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        ///         0x20 - оптимизировать по расстоянию
        ///         0x40 - допускать выезд на дорогу и съезд через встречную полосу
        /// </summary>
        /// <param name="stops"></param>
        /// <param name="startTime"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        public RResult GetRoute(RStop[] stops, DateTime startTime, long flags, int[] RegionsAvailableToUser)
        {
            return GetRoute(stops, startTime, flags, RegionsAvailableToUser, null, 0, null);
        }

        /// <summary>
        /// Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик
        ///         0x08 - использовать исторический трафик
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        ///         0x20 - оптимизировать по расстоянию
        ///         0x40 - допускать выезд на дорогу и съезд через встречную полосу
        /// </summary>
        /// <param name="stops"></param>
        /// <param name="startTime"></param>
        /// <param name="flags"></param>
        /// <param name="RegionsAvailableToUser"></param>
        /// <param name="roadsExcept">Избегая проезд по дорогам сквозь точки [X-Lon,Y-Lat]</param>
        /// <param name="roadsExceptRaduisInMeters">Избегая проезд по дорогам сквозь точки, радиус в метрах</param>
        /// <param name="RoadsOnly">Параметры построения маршрута используя атрибуты дорог</param>
        /// <returns></returns>
        public RResult GetRoute(RStop[] stops, DateTime startTime, long flags, int[] RegionsAvailableToUser,
            PointF[] roadsExcept, double roadsExceptRaduisInMeters, byte[] RoadsOnly)
        {
            mtx.WaitOne();
            RResult res = new RResult(null);
            try
            {
                res = GetRoutePrivate(stops, startTime, flags, RegionsAvailableToUser, roadsExcept, roadsExceptRaduisInMeters, RoadsOnly);
                mtx.ReleaseMutex();
                return res;
            }
            catch (System.Threading.ThreadAbortException)
            {
                res.LastError = "101 Маршрут не найден, превышено время расчета";
                mtx.ReleaseMutex();
                return res;
            }
            catch (Exception ex)
            {
                XMLSaved<int>.AddErr2SysLog("Engine Exception: " + ex.ToString());
                res.LastError = "050 Engine Exception: " + ex.Message.ToString();
                mtx.ReleaseMutex();
                return res;
            };
        }
        private Mutex mtx = new Mutex(); // на тот случай если вдруг к объекту пытается получить доступ другой поток
        
        /// <summary>
        ///     Привязка точки к дороге
        /// </summary>
        /// <param name="Lat">Широта</param>
        /// <param name="Lon">Долгота</param>
        /// <param name="getName">Запрашивать наименование дороги (работает дольше)</param>
        /// <returns>Привязанные к дороге точки</returns>
        public RNearRoad[] GetNearRoad(double[] lat, double[] lon, bool getNames)
        {
            mtx.WaitOne();
            RNearRoad[] res = new RNearRoad[0];
            try
            {
                res = GetNearRoadPrivate(lat, lon, getNames);
                mtx.ReleaseMutex();
                return res;
            }
            catch (System.Threading.ThreadAbortException)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            };
        }

        /// <summary>
        ///     Общее число потоков на сервере
        /// </summary>
        /// <returns></returns>
        public int GetThreadsCount() { return 1; }

        /// <summary>
        ///     Номер незанятого потока
        /// </summary>
        /// <returns></returns>
        public int GetIdleThreadCount() { return 0; }   

        /// <summary>
        ///     Находим расстояние от исходной/конечной координаты до каждой линии
        /// </summary>
        /// <param name="Lat">Широта</param>
        /// <param name="Lon">Долгота</param>
        /// <param name="metersRadius">Радиус поиска ближайшей линии в метрах</param>
        /// <returns>array[0..NumberOfNearPoints-1][0: distance,1: X or Lon,2: Y or Lat,3: line_id]</returns>
        public float[][] FindNearestLines(float Lat, float Lon, float metersRadius)
        {
            return gr.FindNearestLines(Lat, Lon, metersRadius);
        }
        /// <summary>
        ///     Находим расстояние от исходной/конечной координаты до каждой линии
        /// </summary>
        /// <param name="Lat">Широта</param>
        /// <param name="Lon">Долгота</param>
        /// <param name="metersRadius">Радиус поиска ближайшей линии в метрах</param>
        /// <returns>array[0..NumOfPoints-1][0..NumberOfNearPoints-1][0: distance,1: X or Lon,2: Y or Lat,3: line_id]</returns>
        public float[][][] FindNearestLines(float[] Lat, float[] Lon, float metersRadius)
        {
            if ((Lat == null) || (Lon == null) || (Lat.Length == 0) || (Lon.Length == 0) || (Lat.Length != Lon.Length))
            {
                throw new OverflowException();
            };

            List<float[][]> res = new List<float[][]>();
            for (int i = 0; i < Lat.Length; i++)
                res.Add(gr.FindNearestLines(Lat[i], Lon[i], metersRadius));
            return res.ToArray();
        }

        public void Close()
        {
            gr.Close();
            gr = null;
        }

        ~OneRegionSolver()
        {
            if(gr != null)
                gr.Close();
        }

        public static void OneRegion_Test_PreService()
        {
            OneRegionSolver.GraphFile = XMLSaved<int>.GetCurrentDir() + @"..\..\..\RGSolver\bin\Debug\Matrix3\lipetsk.rt";
            OneRegionSolver ls = new OneRegionSolver();
            RStop[] ss = new RStop[2] { new RStop("POINT A", 53.365, 40.10), new RStop("POINT B", 52.105, 39.176) };
            //ss = new RStop[2] { new RStop("POINT A", 52.62165451049805, 39.56116485595703 ), new RStop("POINT B", 52.60966873168945, 39.567176818847656) };
            //RStop[] ss = new RStop[2] { new RStop("POINT A", 52.64549811935865, 39.660279750823975), new RStop("POINT B", 52.57972714326297, 39.51683521270752)};
            //ss = new RStop[2] { new RStop("POINT A", 52.64341, 39.65921), new RStop("POINT B", 52.57972714326297, 39.51683521270752) };
            //RStop[] ss = new RStop[3] { new RStop("POINT A", 52.59708, 39.5685), new RStop("POINT B", 52.63228, 39.5788), new RStop("POINT C", 53.365, 40.10) };
            // new RStop("POINT C", 53.365, 40.10)
            RResult rr = ls.GetRoute(ss, DateTime.Now.AddHours(2), 3, null);
            ls.Close();

            // SAVE //
            RouteDescription rd = new RouteDescription(null);
            if(rr.description != null)
                rd.SaveTextDescription(rr.description, XMLSaved<int>.GetCurrentDir() + @"\Matrix3\WAY-LIST3.txt");
            if(rr.vector != null)
                ToJS(rr.vector);
        }

        public override object InitializeLifetimeService()
        {
            if (keepAlive)
                return null; // to make the object live indefinitely
            else
                return base.InitializeLifetimeService();
        }

        private static void ToJS(PointFL[] route)
        {
            string s = "";
            s += "var polylipets = new GPolyline([\r\n";
            for (int i = 0; i < route.Length; i++)
                s += "new GLatLng(" + route[i].Y.ToString().Replace(",", ".") + ", " + route[i].X.ToString().Replace(",", ".") + ")" + (i < (route.Length - 1) ? "," : "") + "\r\n";
            s += "], \"#0000FF\", 5);\r\n";
            s += "map.addOverlay(polylipets);\r\n\r\n";

            System.Windows.Forms.Clipboard.SetText(s);
        }

        public static void OneRegion_Test_SvcCmd()
        {
            OneRegionSolver.GraphFile = XMLSaved<int>.GetCurrentDir() + @"..\..\..\RGSolver\bin\Debug\Matrix3\lipetsk.rt";
            OneRegionSolver lip = new OneRegionSolver();

            BinaryServerFormatterSinkProvider provider = new BinaryServerFormatterSinkProvider();
            provider.TypeFilterLevel = TypeFilterLevel.Full;

            IDictionary RouteSearcherProps = new Hashtable();
            RouteSearcherProps["name"] = "dkxce.Route.TCPSolver";
            RouteSearcherProps["port"] = 7755;

            TcpChannel RouteSearcherChannel = new TcpChannel(RouteSearcherProps, null, provider);
            ChannelServices.RegisterChannel(RouteSearcherChannel, false);
            ObjRef reference = RemotingServices.Marshal(lip, "dkxce.Route.TCPSolver");

            Console.WriteLine("Listen at " + string.Format("tcp://{0}:{1}/dkxce.Route.TCPSolver", "localhost", 7755));
            Console.ReadLine();

            RemotingServices.Disconnect(lip);
            reference = null;
            ChannelServices.UnregisterChannel(RouteSearcherChannel);
            RouteSearcherChannel = null;
            lip = null;
        }
    }
}
