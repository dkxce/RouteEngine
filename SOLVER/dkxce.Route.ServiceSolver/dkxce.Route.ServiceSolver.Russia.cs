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

using dkxce.Route.Regions;

namespace dkxce.Route.ServiceSolver
{
    public class RussiaSolver : MarshalByRefObject, IRoute
    {
        public class PreloadedRG
        {
            public bool forDescOnly = false;
            public int region;
            public RMGraph rmg;
            public PreloadedRG(int region, RMGraph g, bool forDescOnly)
            {
                this.region = region;
                this.rmg = g;
                this.forDescOnly = forDescOnly;
            }
        }

        // Multithread vars
        public static int[] _threadGraphsIds = new int[0];

        // Храним объекты для нескольких потоков        
        private static DirectoryXML _globalDirs = null;
        private static PointInRegionUtils _globalRegions = null;
        private static PreloadedRG[] _globalGraphs = new PreloadedRG[0];

        private bool keepAlive = false;

        /// <summary>
        ///     Кэшированые графы, загружаются при запуске службы
        /// </summary>
        private List<PreloadedRG> _rt_Cached = new List<PreloadedRG>();
        /// <summary>
        ///     Сессионные графы, загружаются по мере необходимости
        /// </summary>
        private List<PreloadedRG> _rt_Session = new List<PreloadedRG>();
        
        /// <summary>
        ///     Максимальное число сессионных графов, которые будут сохранены в памяти для следующей сессии
        /// </summary>
        private const byte _rt_Session_SaveNext = 2;
        /// <summary>
        ///     Максимальное число сессионных графов, которые могут быть сохранены в памяти для текущей сессии
        /// </summary>
        private const byte _rt_Session_SaveCurrent = 3;
        /// <summary>
        ///     Могут ли быть сохранены в текущей сессии графы только для описания
        /// </summary>
        private const bool _rt_Session_SaveDescripted = true;
        /// <summary>
        ///     Могут ли быть сохранены для следующей сессии графы только для описания
        /// </summary>
        private const bool _rt_Session_SaveNextDescripted = true;

        public RussiaSolver()
        {
            RussiaSolverCreate();
        }

        public RussiaSolver(bool keepAlive)
        {
            this.keepAlive = keepAlive;
            RussiaSolverCreate();
        }

        private void RussiaSolverCreate()
        {
            PreloadGlobalCache(null, null); // preload global cache if it still not
            PreloadGraphsToCache(); // current thread cache
            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
        }

        public static string PreloadGlobalCache(int[] regions, DirectoryXML initDirs)
        {
            if (_globalRegions != null) return "";

            if ((_globalDirs == null) && (initDirs != null)) _globalDirs = initDirs;            
            if (_globalDirs == null)
            {
                string tmpfile = XMLSaved<int>.GetCurrentDir() + @"\directories.xml";
                if (File.Exists(tmpfile))
                    _globalDirs = XMLSaved<DirectoryXML>.Load(tmpfile);
                else
                {
                    XMLSaved<int>.AddErr2SysLog("[RussiaSolver] Configuration file `directories.xml` not found!");
                    throw new Exception("Configuration file `directories.xml` not found!");
                };
            };

            _globalRegions = new PointInRegionUtils();
            _globalRegions.LoadRegionsFromFile(_globalDirs.RegionsDirectory() + @"\regions.shp");

            List<PreloadedRG> tmp = new List<PreloadedRG>();
            string regs = "";
            if(regions != null)
                for (int i = 0; i < _globalRegions.RegionsCount; i++)
                {
                    if (Array.IndexOf<int>(regions, _globalRegions.RegionsIDs[i]) >= 0)
                    //if ((_globalRegions.RegionsIDs[i] == 11) || (_globalRegions.RegionsIDs[i] == 24) || (_globalRegions.RegionsIDs[i] == 10)) // 10 lipetsk // 11 msk // 24 spb
                    {
                        string file = _globalDirs.GraphDirectory() + @"\" + _globalRegions.RegionFileByRegionId(_globalRegions.RegionsIDs[i]);
                        if (System.IO.File.Exists(file))
                        {
                            tmp.Add(new PreloadedRG(_globalRegions.RegionsIDs[i], RMGraph.LoadToMemoryGlobal(file, _globalRegions.RegionsIDs[i].ToString(), _globalRegions.RegionsIDs[i]), false));
                            regs += (regs.Length > 0 ? ", " : "") + _globalRegions.RegionsIDs[i].ToString();
                        };
                    };
                };
            _globalGraphs = tmp.ToArray();

           RouteThreader.mem.GlobalRegionsCache = _globalGraphs.Length.ToString() + "/" + _globalRegions.RegionsCount.ToString() + ": " + regs + " ... " + (_globalGraphs.Length == 0 ? "NONE" : "OK");
           RouteThreader.mem.GlobalRegionsCacheSize = 0;
           if (_globalGraphs.Length > 0)
               for (int i = 0; i < _globalGraphs.Length; i++)
                   RouteThreader.mem.GlobalRegionsCacheSize += _globalGraphs[i].rmg.GraphSize;
           return "Load " + _globalGraphs.Length.ToString() + "/"+_globalRegions.RegionsCount.ToString()+" regions to global cache: " + regs + "... "+(_globalGraphs.Length == 0 ? "NONE" : "OK");
        }

        /// <summary>
        ///     Загружаем графы в кэш при запуске (для текущего потока)
        /// </summary>
        private void PreloadGraphsToCache()
        {                        
            string sx = "";
            if(_threadGraphsIds!=null)
                for (int i = 0; i < _globalRegions.RegionsCount; i++)
                {
                    if(Array.IndexOf<int>(_threadGraphsIds,_globalRegions.RegionsIDs[i]) >= 0)
                    //if ((_globalRegions.RegionsIDs[i] == 11) || (_globalRegions.RegionsIDs[i] == 24)) // 10 lipetsk // 11 msk // 24 spb
                    {
                        string file = _globalDirs.GraphDirectory() + @"\" + _globalRegions.RegionFileByRegionId(_globalRegions.RegionsIDs[i]);
                        if (System.IO.File.Exists(file))
                        {
                            _rt_Cached.Add(new PreloadedRG(_globalRegions.RegionsIDs[i], RMGraph.LoadToMemory(file, _globalRegions.RegionsIDs[i]), false));
                            sx += (sx.Length > 0 ? ", " + _globalRegions.RegionsIDs[i].ToString() : _globalRegions.RegionsIDs[i].ToString());
                        };
                    };
                };
            RouteThreader.mem.ThreadRegionsCache = _threadGraphsIds.Length.ToString() + "/" + _globalRegions.RegionsCount.ToString() + ": " + sx + " ... " + (_rt_Cached.Count == 0 ? "NONE" : "OK");
            //if (this.keepAlive) 
            //    XMLSaved<int>.Add2SysLog("[RussiaSolver] Preload " + _rt_Cached.Count.ToString() + " regions: "+sx+" to client object");
        }

        /// <summary>
        ///     Подгружаем сессионный граф по мере необходимости
        /// </summary>
        /// <param name="region"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        private RMGraph LoadGraph(int region, string file, bool forDescriptionOnly)
        {            
            //look in static cache
            for (int i = 0; i < _globalGraphs.Length; i++)
                if (_globalGraphs[i].region == region)
                    //return _globalGraphs[i].rmg;
                    return RMGraph.IsolatedCopyFrom(_globalGraphs[i].rmg);

            // look in cached
            for (int i = 0; i < _rt_Cached.Count; i++)
                if (_rt_Cached[i].region == region)
                    return _rt_Cached[i].rmg;

            // look in session
            for (int i = 0; i < _rt_Session.Count; i++)
                if (_rt_Session[i].region == region)
                    return _rt_Session[i].rmg;

            // Если число открытых сессионных графов = 
            // максимальному числу сессионных графов, которые могут быть открыты в текущей сессии
            // то освобождаем память от одного графа, чтобы добавит в память новый
            if ((_rt_Session.Count == _rt_Session_SaveCurrent) && (_rt_Session.Count > 0))
            {
                // в первую очередь удаляем только для описания
                int delIndex = 0;
                for (int i = 0; i < _rt_Session.Count; i++)
                    if (_rt_Session[i].forDescOnly)
                    {
                        delIndex = i; 
                        break;
                    };
                _rt_Session[delIndex].rmg.Close();
                _rt_Session[delIndex].rmg = null;
                _rt_Session.RemoveAt(delIndex);
            };

            // if no in session, then load in session
            _rt_Session.Add(new PreloadedRG(region, RMGraph.LoadToMemory(file, region), forDescriptionOnly));
            return _rt_Session[_rt_Session.Count - 1].rmg;
        }

        /// <summary>
        ///     Выгружаем сессионный граф
        /// </summary>
        /// <param name="region"></param>
        private void UnloadGraph(int region)
        {
            //// CloseAllTemporary
            if(!_rt_Session_SaveDescripted)
                for (int i = _rt_Session.Count - 1; i >= 0; i--)
                    if ((_rt_Session[i].region == region) && (_rt_Session[i].forDescOnly))
                    {
                        _rt_Session[i].rmg.Close();
                        _rt_Session[i].rmg = null;
                        _rt_Session.RemoveAt(i);
                    };

            if (_rt_Session_SaveCurrent == 0)
                for (int i = _rt_Session.Count - 1; i >= 0; i--)
                    if (_rt_Session[i].region == region)
                    {
                        _rt_Session[i].rmg.Close();
                        _rt_Session[i].rmg = null;
                        _rt_Session.RemoveAt(i);
                    };
        }

        /// <summary>
        ///     Выгружаем все сессионные графы (Закрываем сессию)
        /// </summary>
        public void UnloadTemporaryGraphs()
        {
            if ((_rt_Session.Count > 0) && (!_rt_Session_SaveNextDescripted))
            {
                for(int i=_rt_Session.Count-1;i>=0;i--)
                    if (_rt_Session[i].forDescOnly)
                    {
                        _rt_Session[i].rmg.Close();
                        _rt_Session[i].rmg = null;
                        _rt_Session.RemoveAt(i);
                    };

            };
            if (_rt_Session.Count > 0)
            {
                //  Выгружаем графы, которые будут закрыты,
                //  остальные будут открыты до следующей сессии
                while (_rt_Session.Count > _rt_Session_SaveNext) // 
                {
                    _rt_Session[0].rmg.Close();
                    _rt_Session[0].rmg = null;
                    _rt_Session.RemoveAt(0);
                };
            };
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
            RMGraph grS = null;
            RouteDescription rd = null;            

            int[] _RegIDs = new int[lat.Length];
            string[] _RegFiles = new string[lat.Length];
            string[] _RegName = new string[lat.Length];
            for (int i = 0; i < _RegIDs.Length; i++)
            {
                res[i] = new RNearRoad(double.MaxValue, 0, 0, "", "", -1);

                _RegIDs[i] = _globalRegions.PointInRegion(lat[i], lon[i]);
                _RegFiles[i] = _globalDirs.GraphDirectory() + @"\" + _globalRegions.RegionFileByRegionId(_RegIDs[i]);
                _RegName[i] = _globalRegions.RegionNameByRegionId(_RegIDs[i]);

                if((i==0) || (_RegIDs[i] != _RegIDs[i - 1]) || (grS == null))
                {
                    if (grS != null) UnloadGraph(_RegIDs[i-1]);
                    grS = null;
                    if (_RegIDs[i] < 1)
                    {                        
                        res[i].distance = double.MaxValue;
                        continue;
                    };
                    try
                    {
                        grS = LoadGraph(_RegIDs[i], _RegFiles[i], false);
                    }
                    catch { grS = null; continue; };
                    if (getNames) 
                        rd = new RouteDescription(grS);
                };

                //float[][] NRS = grS.FindNearestLines((float)lat[i], (float)lon[i], (float)2000);
                uint line = 0;
                PointF ll = grS.PointToNearestLine((float)lat[i], (float)lon[i], (float)2000, out res[i].distance, out line);
                if ((line == 0) || (line == uint.MaxValue))
                {
                    res[i].distance = double.MaxValue;
                    continue;
                };
                res[i].lat = ll.Y;
                res[i].lon = ll.X;
                res[i].attributes = "";
                TLineFlags lf = grS.GetLineFlags(line);                
                if (lf.HasAttributes)
                    res[i].attributes = grS.AttributesToString(grS.GetLinesAttributes(new uint[] { line })[0]);
                if (lf.IsOneWay)
                    res[i].attributes += (res[i].attributes.Length > 0 ? " 1w" : "1w");
                float d, t;
                if (grS.GetLineLT(line, out d, out t))
                {
                    double speed = d / t / 1000.0 * 60;
                    res[i].attributes += (res[i].attributes.Length > 0 ? " avg" : "avg") +
                        speed.ToString("0.");
                };
                res[i].region = grS.RegionID;
                if (getNames)
                    res[i].name = rd.GetRoadName(new uint[] { line })[0];                                                    
            };
            if(_RegIDs.Length > 0)
                if (grS != null) 
                    if(_RegIDs[_RegIDs.Length-1] > 0)
                        UnloadGraph(_RegIDs[_RegIDs.Length-1]);
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

            RResult res = new RResult(stops);
            if ((stops == null) || (stops.Length < 2))
            {
                res.LastError = "011 Необходимо минимум 2 точки";
                UnloadTemporaryGraphs();
                return res;
            };

            if (stops.Length > 100)
            {
                res.LastError = "015 Вы первысили максимально допустимое число точек - 100";
                UnloadTemporaryGraphs();
                return res;
            };

            res.driveLengthSegments = new double[stops.Length];
            res.driveTimeSegments = new double[stops.Length];

            ///////////////////////
            // GET REGIONS POINTS IN
            ///////////////////////            
            int[] _RegIDs = new int[stops.Length];
            string[] _RegFiles = new string[stops.Length];
            string[] _RegName = new string[stops.Length];
            for (int i = 0; i < _RegIDs.Length; i++)
            {
                _RegIDs[i] = _globalRegions.PointInRegion(stops[i].lat, stops[i].lon);
                _RegFiles[i] = _globalDirs.GraphDirectory() + @"\" + _globalRegions.RegionFileByRegionId(_RegIDs[i]);
                _RegName[i] = _globalRegions.RegionNameByRegionId(_RegIDs[i]);

                if (_RegIDs[i] < 1)
                {
                    res.LastError = "012 Точка `"+stops[i].name+"` не попадает в зону доступной карты!";
                    UnloadTemporaryGraphs();
                    return res;
                };
                if (!File.Exists(_RegFiles[i]))
                {
                    res.LastError = "013 Для региона `" + _RegName[i] + "` маршруты недоступны!";
                    UnloadTemporaryGraphs();
                    return res;
                };
            };            
            ///////////////////////
            /////////////////////// 
            

            if ((RegionsAvailableToUser != null) && (RegionsAvailableToUser.Length > 0))
            {
                if (RegionsAvailableToUser[0] > 0)
                {
                    for (int i = 0; i < _RegIDs.Length; i++)
                        if (Array.IndexOf(RegionsAvailableToUser, _RegIDs[i]) < 0)
                        {
                            res.LastError = "003 " + stops[i].name + " - нет прав на построение маршрутов в регионе `" + _RegName[i] + "`";
                            UnloadTemporaryGraphs();
                            return res;
                        };
                }
                else
                {
                    for (int i = 0; i < _RegIDs.Length; i++)
                        if (Array.IndexOf(RegionsAvailableToUser, -1*_RegIDs[i]) >= 0)
                        {
                            res.LastError = "003 " + stops[i].name + " - нет прав на построение маршрутов в регионе `" + _RegName[i] + "`";
                            UnloadTemporaryGraphs();
                            return res;
                        };
                };
            };

            res.startTime = startTime;   
            res.finishTime = res.startTime;            

            for (int rNo = 1; rNo < _RegIDs.Length; rNo++)
            {                
                if (_RegIDs[rNo] != _RegIDs[rNo - 1])
                {
                    RDPoint[] desc;
                    List<RDPoint> descABC = new List<RDPoint>();
                    if (res.description != null) descABC.AddRange(res.description);
                    List<RDPoint[]> descB = new List<RDPoint[]>();
                    List<double[]> dtB = new List<double[]>();

                    ///////////////////////
                    // GET RGNODES
                    ///////////////////////
                    List<uint> sRGNodes = new List<uint>();
                    List<uint> sGraphNodes = new List<uint>();
                    List<float> sTimeToRGNode = new List<float>();
                    foreach (TRGNode rgnode in XMLSaved<TRGNode[]>.Load(_globalDirs.GraphDirectory()+ @"\" + String.Format("{0:000}",_RegIDs[rNo-1]) + ".rgnodes.xml"))
                        if (rgnode.outer)
                        {
                            sRGNodes.Add((uint)rgnode.id);
                            sGraphNodes.Add(rgnode.node);
                            sTimeToRGNode.Add(Utils.GetLengthMeters(stops[rNo - 1].lat, stops[rNo - 1].lon, rgnode.lat, rgnode.lon, false) / 1500); // 90 km/h //// 1000 - 60kmph // 1670 - 100 kmph // 1840 - 110 kmph // 2000 - 120 kmph
                        };
                    List<uint> eRGNodes = new List<uint>();
                    List<uint> eGraphNodes = new List<uint>();
                    List<float> eTimeFromRGNode = new List<float>();
                    foreach (TRGNode rgnode in XMLSaved<TRGNode[]>.Load(_globalDirs.GraphDirectory() + @"\" + String.Format("{0:000}", _RegIDs[rNo]) + ".rgnodes.xml"))
                        if (rgnode.inner)
                        {
                            eRGNodes.Add((uint)rgnode.id);
                            eGraphNodes.Add(rgnode.node);
                            eTimeFromRGNode.Add(Utils.GetLengthMeters(stops[rNo].lat, stops[rNo].lon, rgnode.lat, rgnode.lon, false) / 1500); // 90 km/h //// 1000 - 60kmph // 1670 - 100 kmph // 1840 - 110 kmph // 2000 - 120 kmph
                        };
                    ///////////////////////
                    ///////////////////////

                    int start_matrix_element = -1;
                    int end_matrix_element = -1;

                    ///////////////////////
                    // Calculate shortest way (to_matrix+in_matrix)
                    ///////////////////////
                    dkxce.Route.Matrix.RMMatrix rm = null;
                    try
                    {
                        rm = dkxce.Route.Matrix.RMMatrix.LoadToMemory(_globalDirs.GraphDirectory() + @"\000.bin");
                    }
                    catch(Exception)
                    {
                        res.LastError = "052 Межрегиональные маршруты недоступны! Основной файл не найден!";
                        UnloadTemporaryGraphs();
                        return res;
                    };
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
                                        
                    float dist_B = rm.GetRouteDist(sRGNodes[start_matrix_element], eRGNodes[end_matrix_element]);
                    float time_B = rm.GetRouteTime(sRGNodes[start_matrix_element], eRGNodes[end_matrix_element]);
                    float cost_B = rm.GetRouteCost(sRGNodes[start_matrix_element], eRGNodes[end_matrix_element]);

                    if (dist_B > RMGraph.const_maxValue)
                    {
                        res.LastError = String.Format("100 Невозможно построить маршрут {0} - {1}", stops[rNo - 1].name, stops[rNo].name);
                        UnloadTemporaryGraphs();
                        return res;
                    };

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
                            ushort bReg = rm.GetByReg(frt[i - 1], frt[i]);
                            string fN = _globalDirs.RGWaysDirectory() + @"\" + String.Format(@"{0}T{1}B{2}.rgway.xml", frt[i - 1], frt[i], bReg);
                            if(!File.Exists(fN)) fN = _globalDirs.RGWaysDirectory() + @"\" + String.Format(@"{0}T{1}.rgway.xml", frt[i - 1], frt[i]);
                            nodes_B = XMLSaved<RouteResultStored>.Load(fN);
                            points_B.AddRange(nodes_B.route.vector);
                            // DESC++
                            if (getDirections)
                            {
                                string regFN = _globalDirs.GraphDirectory() + @"\" + _globalRegions.RegionFileByRegionId(nodes_B.Region);

                                if (!File.Exists(regFN))
                                {
                                    res.LastError = "053 Для региона `" + _globalRegions.RegionNameByRegionId(nodes_B.Region) + "` описание не может быть загружено!";
                                    UnloadTemporaryGraphs();
                                    return res;
                                };

                                RMGraph tmpG = LoadGraph(nodes_B.Region, regFN, true);
                                RouteDescription td = new RouteDescription(tmpG);
                                td.Templates.S_BEGIN = "Продолжение маршрута";
                                desc = td.GetDescription(nodes_B.route, null, null);
                                if (desc.Length > 0)
                                {
                                    desc[0].name = _globalRegions.RegionNameByRegionId(nodes_B.Region);
                                    desc[desc.Length - 1].name = _globalRegions.RegionNameByRegionId(nodes_B.Region) + ", граница"; ;
                                };
                                descB.Add(desc);
                                dtB.Add(new double[] {  nodes_B.route.length, nodes_B.route.time });

                                UnloadGraph(nodes_B.Region); // tmpG.Close(); // close in preloaded overcount
                            };
                            // --DESC
                        };
                    };
                    rm.Close();
                    ///////////////////////
                    ///////////////////////

                    ///////////////////////
                    // FROM ROUTE
                    ///////////////////////
                    RMGraph grS = LoadGraph(_RegIDs[rNo - 1],_RegFiles[rNo - 1], false);
                    
                    grS.TrafficStartTime = res.finishTime;
                    grS.TrafficUseCurrent = getCurrentTraffic;
                    grS.TrafficUseHistory = getHistoryTraffic;
                    FindStartStopResult nodeStart = grS.FindNodeStart((float)stops[rNo-1].lat, (float)stops[rNo-1].lon, (float)2000);
                    if (nodeStart.nodeN == 0) 
                    {
                        res.LastError = "014 Не найдены дороги вблизи точки `" + stops[rNo - 1].name + "`";
                        UnloadTemporaryGraphs();
                        return res;
                    };

                    grS.GoExcept.Clear();
                    grS.GoThrough = null;
                    if (true) // UseGoLogic
                    {
                        if ((roadsExcept != null) && (roadsExcept.Length != 0))
                            for (int r = 0; r < roadsExcept.Length; r++)
                            {
                                float[][] nl = grS.FindNearestLines(roadsExcept[r].Y, roadsExcept[r].X, (float)roadsExceptRadiusInMeters * 2);
                                if ((nl != null) && (nl.Length > 0))
                                    foreach (float[] nll in nl)
                                        if (nll[0] <= roadsExceptRadiusInMeters)
                                            grS.GoExcept.Add((uint)(nll[3]));//0-distance,1-x,2-y,3-line
                            };
                        if ((roadsOnly != null) && (roadsOnly.Length == 16)) grS.GoThrough = roadsOnly;
                    };

                    grS.BeginSolve(true, null);
                    grS.MinimizeRouteBy = optimizeByDist ? MinimizeBy.Dist : MinimizeBy.Time;
                    grS.SolveAstar(nodeStart.nodeN, sGraphNodes[start_matrix_element]);
                    RouteResult resA = grS.GetRouteFull(nodeStart, sGraphNodes[start_matrix_element], true, allowLeftTurns);
                    if (resA.length > RMGraph.const_maxValue)
                    {
                        UnloadTemporaryGraphs();
                        res.LastError = "100 Маршрут `" + stops[rNo - 1].name + "` - `" + stops[rNo].name + "` не найден";
                        return res;
                    };
                    
                    if (getDirections)
                    {
                        RouteDescription td = new RouteDescription(grS);
                        desc = td.GetDescription(resA, nodeStart, null);
                        if (desc.Length > 0)
                        {
                            desc[0].name = stops[rNo - 1].name;
                            desc[desc.Length - 1].name = _RegName[rNo-1] + ", граница";
                            for (int x = 0; x < desc.Length; x++)
                            {
                                desc[x].dist += (float)res.driveLength;
                                desc[x].time += (float)res.driveTime;
                            };
                        };
                        descABC.AddRange(desc);
                    };

                    grS.EndSolve();
                    UnloadGraph(_RegIDs[rNo - 1]); // grS.Close();  // close in preloaded overcount

                    //////RResult rr = null; // External GetRoute(...
                    //////if (rr.LastError != "")
                    //////{
                    //////    res.LastError = rr.LastError;
                    //////    CloseAllTemporary();
                    //////    return res;
                    //////};
                    //////resA = new RouteResult();
                    //////resA.length = (float)rr.driveLength;
                    //////resA.time = (float)rr.driveTime;
                    //////resA.vector = rr.vector;
                    //////descABC.AddRange(rr.description);

                    ////////////////////////
                    // MATRIX DESCRIPTION //
                    //////////////////////// 
                    if ((getDirections) && (descB.Count > 0))
                    {
                        double pd = 0;
                        double pt = 0;
                        for (int i = 0; i < descB.Count; i++)
                        {                            
                            for (int x = 0; x < descB[i].Length; x++)
                            {
                                descB[i][x].dist += (float)res.driveLength + resA.length +(float)pd;
                                descB[i][x].time += (float)res.driveTime + resA.time + (float)pt;
                            };
                            descABC.AddRange(descB[i]);
                            pd += dtB[i][0];
                            pt += dtB[i][1];
                        };
                    };
                    
                    ///////////////////////
                    // TO ROUTE
                    ///////////////////////  
                    RMGraph grE = LoadGraph(_RegIDs[rNo], _RegFiles[rNo], false);
                    grE.TrafficStartTime = grS.TrafficStartTime.AddMinutes(resA.time + time_B);
                    grE.TrafficUseCurrent = getCurrentTraffic;
                    grE.TrafficUseHistory = getHistoryTraffic;
                    FindStartStopResult nodeEnd = grE.FindNodeEnd((float)stops[rNo].lat, (float)stops[rNo].lon, (float)2000);
                    if (nodeEnd.nodeN == 0) 
                    {
                        res.LastError = "014 Не найдены дороги вблизи точки `" + stops[rNo].name + "`";
                        UnloadTemporaryGraphs();
                        return res;
                    };

                    grE.GoExcept.Clear();
                    grE.GoThrough = null;
                    if (true) // UseGoLogic
                    {
                        if ((roadsExcept != null) && (roadsExcept.Length != 0))
                            for (int r = 0; r < roadsExcept.Length; r++)
                            {
                                float[][] nl = grE.FindNearestLines(roadsExcept[r].Y, roadsExcept[r].X, (float)roadsExceptRadiusInMeters * 2);
                                if ((nl != null) && (nl.Length > 0))
                                    foreach (float[] nll in nl)
                                        if (nll[0] <= roadsExceptRadiusInMeters)
                                            grE.GoExcept.Add((uint)(nll[3]));//0-distance,1-x,2-y,3-line
                            };
                        if ((roadsOnly != null) && (roadsOnly.Length == 16)) grE.GoThrough = roadsOnly;
                    };

                    grE.BeginSolve(true, null);
                    grE.MinimizeRouteBy = optimizeByDist ? MinimizeBy.Dist : MinimizeBy.Time;
                    grE.SolveAstar(eGraphNodes[end_matrix_element], nodeEnd.nodeN);
                    RouteResult resC = grE.GetRouteFull(eGraphNodes[end_matrix_element], nodeEnd, true, allowLeftTurns);
                    if (resC.length > RMGraph.const_maxValue)
                    {
                        UnloadTemporaryGraphs();
                        res.LastError = "100 Маршрут `" + stops[rNo-1].name + "` - `" + stops[rNo].name + "` не найден";
                        return res;
                    };

                    if (getDirections)
                    {
                        RouteDescription td = new RouteDescription(grE);
                        td.Templates.S_BEGIN = "Продолжение маршрута";
                        desc = td.GetDescription(resC, null, nodeEnd);
                        if (desc.Length > 0)
                        {
                            desc[0].name = _RegName[rNo];
                            desc[desc.Length - 1].name = stops[rNo].name;
                            for (int x = 0; x < desc.Length; x++)
                            {
                                desc[x].dist += (float)res.driveLength + resA.length + dist_B;
                                desc[x].time += (float)res.driveTime + resA.time + time_B;
                            };
                        };
                        descABC.AddRange(desc);
                    };

                    grE.EndSolve();

                    UnloadGraph(_RegIDs[rNo]); // grE.Close(); // close in preloaded overcount

                    //////RResult rr = null; // External GetRoute(...
                    //////if (rr.LastError != "")
                    //////{
                    //////    res.LastError = rr.LastError;
                    //////    CloseAllTemporary();
                    //////    return res;
                    //////};
                    //////resC = new RouteResult();
                    //////resC.length = (float)rr.driveLength;
                    //////resC.time = (float)rr.driveTime;
                    //////resC.vector = rr.vector;
                    //////descABC.AddRange(rr.description);

                    ///////////////////////
                    ///////////////////////            
                    
                    // SUM //                    
                    res.driveLength += resA.length + dist_B + resC.length;
                    res.driveTime += resA.time + time_B + resC.time;
                    res.finishTime = res.finishTime.AddMinutes(resA.time + time_B + resC.time);
                    
                    if (getPolyline)
                    {
                        List<PointFL> vec = new List<PointFL>();
                        if (res.vector != null) vec.AddRange(res.vector);
                        for (int x = 0; x < nodeStart.normal.Length; x++)
                            vec.Add(new PointFL(nodeStart.normal[x], 0, 0, (x > 0) && ((resA.vector != null) && (resA.vector.Length != 0)) ? resA.vector[0].speed : 20));
                        vec.AddRange(resA.vector);
                        vec.AddRange(points_B);
                        vec.AddRange(resC.vector);
                        for (int x = 0; x < nodeEnd.normal.Length; x++)
                            vec.Add(new PointFL(nodeEnd.normal[x], 0, 0, (x < (nodeEnd.normal.Length - 1)) && ((resC.vector != null) && (resC.vector.Length != 0)) ? resC.vector[resC.vector.Length-1].speed : 20));
                        res.vector = vec.ToArray();
                    };

                    if (getDirections)
                        res.description = descABC.ToArray();
                }
                else
                {
                    List<RDPoint> descABC = new List<RDPoint>();
                    if (res.description != null) descABC.AddRange(res.description);

                    /////////////////////////
                    // IN-ONE-REGION ROUTE //
                    /////////////////////////
                    RMGraph gr = LoadGraph(_RegIDs[rNo-1], _RegFiles[rNo - 1], false);
                    gr.TrafficStartTime = res.finishTime;
                    gr.TrafficUseCurrent = getCurrentTraffic;
                    gr.TrafficUseHistory = getHistoryTraffic;

                    FindStartStopResult nodeStart = gr.FindNodeStart((float)stops[rNo - 1].lat, (float)stops[rNo - 1].lon, (float)2000);
                    FindStartStopResult nodeEnd = gr.FindNodeEnd((float)stops[rNo].lat, (float)stops[rNo].lon, (float)2000);
                    if (nodeStart.nodeN == 0)
                    {
                        res.LastError = "014 Не найдены дороги вблизи точки `" + stops[rNo - 1].name + "`";
                        UnloadTemporaryGraphs();
                        return res;
                    };
                    if (nodeEnd.nodeN == 0)
                    {
                        res.LastError = "014 Не найдены дороги вблизи точки `" + stops[rNo].name + "`";
                        UnloadTemporaryGraphs();
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
                        if ((roadsOnly != null) && (roadsOnly.Length == 16)) gr.GoThrough = roadsOnly;
                    };

                    gr.BeginSolve(true, null);
                    gr.MinimizeRouteBy = optimizeByDist ? MinimizeBy.Dist : MinimizeBy.Time;

                    // Added 21.12.2021 where 1 line in route for WATER
                    RouteResult resF;
                    if (allowLeftTurns && (nodeStart.line == nodeEnd.line))
                    {
                        resF = gr.GetFullRouteOneLine(nodeStart, nodeEnd);
                    }
                    else
                    {
                        gr.SolveAstar(nodeStart.nodeN, nodeEnd.nodeN);
                        resF = gr.GetRouteFull(nodeStart, nodeEnd, true, allowLeftTurns);
                    };
                    
                    if (resF.length > RMGraph.const_maxValue)
                    {
                        UnloadTemporaryGraphs();
                        res.LastError = "100 Маршрут `" + stops[rNo-1].name + "` - `" + stops[rNo].name + "` не найден";
                        return res;
                    };

                    if (getPolyline)
                    {
                        List<PointFL> vec = new List<PointFL>();
                        if (res.vector != null) vec.AddRange(res.vector);
                        for (int x = 0; x < nodeStart.normal.Length; x++)
                            vec.Add(new PointFL(nodeStart.normal[x], 0, 0, (x > 0) && ((resF.vector != null) && (resF.vector.Length != 0)) ? resF.vector[0].speed : 20));
                        vec.AddRange(resF.vector);
                        for (int x = 0; x < nodeEnd.normal.Length; x++)
                            vec.Add(new PointFL(nodeEnd.normal[x], 0, 0, (x < (nodeEnd.normal.Length-1)) && ((resF.vector != null) && (resF.vector.Length != 0)) ? resF.vector[resF.vector.Length-1].speed : 20));
                        res.vector = vec.ToArray();
                    };

                    if (getDirections)
                    {
                        RouteDescription td = new RouteDescription(gr);
                        RDPoint[] desc = td.GetDescription(resF, nodeStart, nodeEnd);
                        if (desc.Length > 0)
                        {
                            desc[0].name = stops[rNo-1].name;
                            desc[desc.Length - 1].name = stops[rNo].name;
                            for (int x = 0; x < desc.Length; x++)
                            {
                                desc[x].dist += (float)res.driveLength;
                                desc[x].time += (float)res.driveTime;
                            };
                        };
                        descABC.AddRange(desc);

                        res.description = descABC.ToArray();                        
                    };

                    gr.EndSolve();
                    UnloadGraph(_RegIDs[rNo - 1]); // gr.Close(); // close in preloaded overcount

                    // SUM //                    
                    res.driveLength += resF.length;
                    res.driveTime += resF.time;
                    res.finishTime = res.finishTime.AddMinutes(resF.time);
                };

                if ((res.vector != null) && (res.vectorSegments == null)) res.vectorSegments = new int[stops.Length];
                if (res.vectorSegments != null) res.vectorSegments[rNo] = res.vector.Length;
                if ((res.description != null) && (res.descriptionSegments == null)) res.descriptionSegments = new int[stops.Length];
                if (res.descriptionSegments != null) res.descriptionSegments[rNo] = res.description.Length;

                res.driveLengthSegments[rNo] = res.driveLength;
                res.driveTimeSegments[rNo] = res.driveTime;                    
                
                //
                //
                // Нам ещё необходимы длина и время движения между каждыми двумя точками
                // res.driveLength
                // res.driveTime
                //
            };

            if (res.vectorSegments != null) res.vectorSegments[stops.Length - 1]--;
            if (res.descriptionSegments != null) res.descriptionSegments[stops.Length - 1]--;

            UnloadTemporaryGraphs();
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
                res.LastError = "050 Engine Exception: "+ex.Message.ToString();
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
                mtx.ReleaseMutex();
                return null;
            }
            catch (Exception)
            {
                mtx.ReleaseMutex();
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
            int reg = _globalRegions.PointInRegion(Lat, Lon);
            string file = _globalDirs.GraphDirectory() + @"\" + _globalRegions.RegionFileByRegionId(reg);
            if (!File.Exists(file)) return new float[][] { new float[0] };
            RMGraph g = LoadGraph(reg, file, false);
            float[][] ret = g.FindNearestLines(Lat, Lon, metersRadius);
            UnloadTemporaryGraphs();
            return ret;
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
                throw new OverflowException();

            List<int> regsTtl = new List<int>();

            List<float[][]> res = new List<float[][]>();
            for (int i = 0; i < Lat.Length; i++)
            {
                int reg = _globalRegions.PointInRegion(Lat[i], Lon[i]);
                if (!regsTtl.Contains(reg)) regsTtl.Add(reg);
                if (regsTtl.Count > 3) throw new OverflowException("Maximum allow only 3 regions in one query");

                string file = _globalDirs.GraphDirectory() + @"\" + _globalRegions.RegionFileByRegionId(reg);
                if (File.Exists(file))
                {
                    RMGraph g = LoadGraph(reg, file, false);
                    res.Add(g.FindNearestLines(Lat[i], Lon[i], metersRadius));
                    UnloadGraph(reg);
                }
                else
                    res.Add(new float[][] { new float[0] });
            };
            UnloadTemporaryGraphs();
            return res.ToArray();
        }

        public void Close()
        {
            if (_rt_Cached != null)
                foreach (PreloadedRG pp in _rt_Cached)
                    pp.rmg.Close();
            _rt_Cached = null;

            if (_rt_Session != null)
                foreach (PreloadedRG pp in _rt_Session)
                    pp.rmg.Close();
            _rt_Session = null;
        }

        ~RussiaSolver()
        {
            if (_rt_Cached != null)
                foreach (PreloadedRG pp in _rt_Cached)
                    pp.rmg.Close();
            _rt_Cached = null;

            if (_rt_Session != null)
                foreach (PreloadedRG pp in _rt_Session)
                    pp.rmg.Close();      
            _rt_Session = null;
        }

        //public static void RussiaSolver_Test_PreService()
        //{
        //    RussiaSolver ls = new RussiaSolver();
        //    // 4 REGS
        //    //RStop[] ss = new RStop[] { new RStop("POINT A", 52.6163902330, 38.51806640625), new RStop("POINT B", 55.178867663282, 34.310302734375) };

        //    // LIPETSK
        //    //RStop[] ss = new RStop[2] { new RStop("POINT A", 53.365, 40.10), new RStop("POINT B", 52.105, 39.176) };
            
        //    // 4 REGS + LIP
        //    RStop[] ss = new RStop[] { 
        //        new RStop("POINT A", 53.365, 40.10), 
        //        new RStop("POINT B", 52.105, 39.176), 
        //        new RStop("POINT C", 52.6163902330, 38.51806640625), 
        //        new RStop("POINT D", 55.178867663282, 34.310302734375) 
        //    };

        //    // NEREST LINES
        //    float[][][] nlns = ls.FindNearestLines(new float[] { (float)ss[0].lat, (float)ss[2].lat }, new float[] { (float)ss[0].lon, (float)ss[2].lon }, 100);

        //    DateTime dt = DateTime.Now;
        //    RResult rr = ls.GetRoute(ss, DateTime.Now.AddHours(2), 3, null);
        //    TimeSpan ts = DateTime.Now.Subtract(dt);
        //    ls.Close();

        //    // SAVE //
        //    RouteDescription rd = new RouteDescription(null);
        //    if(rr.description != null)
        //        rd.SaveTextDescription(rr.description, XMLSaved<int>.GetCurrentDir() + @"\Matrix6\WAY-LIST.txt");
        //    if(rr.vector != null)
        //        ToJS(rr.vector);
        //}

        public override object InitializeLifetimeService()
        {
            if (keepAlive)
                return null; // to make the object live indefinitely
            else
                return base.InitializeLifetimeService();
        }

        //private static void ToJS(PointFL[] route)
        //{
        //    string s = "";
        //    s += "var polyrussia = new GPolyline([\r\n";
        //    for (int i = 0; i < route.Length; i++)
        //        s += "new GLatLng(" + route[i].Y.ToString().Replace(",", ".") + ", " + route[i].X.ToString().Replace(",", ".") + ")" + (i < (route.Length - 1) ? "," : "") + "\r\n";
        //    s += "], \"#FF0000\", 5);\r\n";
        //    s += "map.addOverlay(polyrussia);\r\n\r\n";
            
        //    System.Windows.Forms.Clipboard.SetText(s);
        //}

        //public static void Russia_Test_SvcCmd()
        //{
        //    RussiaSolver rus = new RussiaSolver();

        //    //List<RStop> stops = new List<RStop>();
        //    //stops.Add(new RStop("U-18", 55.54757, 37.5489));
        //    //stops.Add(new RStop("Garage", 55.54166, 37.55245));
        //    //stops.Add(new RStop("MAK", 55.54852, 37.5426));
        //    //stops.Add(new RStop("I-46", 55.54569, 37.56069));
        //    //stops.Add(new RStop("Parking", 55.54968, 37.5447));
        //    //stops.Add(new RStop("I-50", 55.54378, 37.5599));
        //    //stops.Add(new RStop("Gasoline", 55.55619, 37.5537));
        //    //RResult rrtest = rus.GetRoute(stops.ToArray(), DateTime.Now, 1 + 2 + 0x10, null);

        //    BinaryServerFormatterSinkProvider provider = new BinaryServerFormatterSinkProvider();
        //    provider.TypeFilterLevel = TypeFilterLevel.Full;

        //    IDictionary RouteSearcherProps = new Hashtable();
        //    RouteSearcherProps["name"] = "dkxce.Route.TCPSolver";
        //    RouteSearcherProps["port"] = 7755;

        //    TcpChannel RouteSearcherChannel = new TcpChannel(RouteSearcherProps, null, provider);
        //    ChannelServices.RegisterChannel(RouteSearcherChannel, false);
        //    ObjRef reference = RemotingServices.Marshal(rus, "dkxce.Route.TCPSolver");

        //    Console.WriteLine("Listen at " + string.Format("tcp://{0}:{1}/dkxce.Route.TCPSolver", "localhost", 7755));
        //    Console.ReadLine();

        //    RemotingServices.Disconnect(rus);
        //    reference = null;
        //    ChannelServices.UnregisterChannel(RouteSearcherChannel);
        //    RouteSearcherChannel = null;
        //    rus = null;
        //}

        public int[] GetRegionsPreload
        {
            get
            {
                int[] result = new int[_rt_Cached.Count];
                for (int i = 0; i < result.Length; i++) result[i] = _rt_Cached[i].region;
                return result;
            }
        }

        public ulong GetRegionsPreloadSize
        {
            get
            {
                ulong sz = 0;
                for (int i = 0; i < _rt_Cached.Count; i++) sz += (ulong)_rt_Cached[i].rmg.GraphSize;
                return sz;
            }
        }

        public int[] GetRegionsSession
        {
            get
            {
                int[] result = new int[_rt_Session.Count];
                for (int i = 0; i < result.Length; i++) result[i] = _rt_Session[i].region;
                return result;
            }
        }

        public ulong GetRegionsSessionSize
        {
            get
            {
                ulong sz = 0;
                for (int i = 0; i < _rt_Session.Count; i++) sz += (ulong)_rt_Session[i].rmg.GraphSize;
                return sz;
            }
        }

        public int[] GetRegionsGlobal
        {
            get
            {
                int[] result = new int[_globalGraphs.Length];
                for (int i = 0; i < result.Length; i++) result[i] = _globalGraphs[i].region;
                return result;
            }
        }

        public ulong GetRegionsGlobalSize
        {
            get
            {
                ulong sz = 0;
                for (int i = 0; i < _globalGraphs.Length; i++) sz += (ulong)_globalGraphs[i].rmg.GraphSize;
                return sz;
            }
        }


        }
}
