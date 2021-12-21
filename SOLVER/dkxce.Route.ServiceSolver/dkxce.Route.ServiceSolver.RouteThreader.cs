#define noLICENSE   

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Net;
using System.Net.Sockets;
using System.Web;
using System.Xml.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;
using System.Security.Cryptography;

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using Microsoft.Win32.SafeHandles;
using System.Runtime.ConstrainedExecution;

using dkxce.Route.ISolver;

namespace dkxce.Route.ServiceSolver
{
    [StructLayout(LayoutKind.Auto)]
    [Serializable]
    public class RouteThreaderInfo
    {
        public DateTime startedAt = DateTime.UtcNow;

        public bool DynamicPool;

        public int MaxSolveTime;
        public int MaxWaitTime;

        public int ObjectsUsed;        
        public int ObjectsIdle;
        public int ThreadsAlive;
        public int ThreadsCounted;
        public int ThreadsMaxAlive; 

        public string Protocol;
        public string Area;
        public string Mode;
        public string GlobalRegionsCache;
        public string ThreadRegionsCache;

        public ulong GlobalRegionsCacheSize;
        public ulong ThreadRegionsCacheSize;

        public string ObjectsData;

        public SvcConfig config;
        public string confile;

        public string GetGraphsPath()
        {
            if (config == null) return null;

            if (config.defArea == "one")
            {
                string path = config.OneRegion.Trim();
                path = Path.IsPathRooted(path) ? path : XMLSaved<int>.GetCurrentDir() + path;
                path = Path.GetFullPath(path);
                path = Path.GetDirectoryName(path);
                return path;
            }
            else
            {
                string path = Path.GetFullPath(config.MultiRegion.GraphDirectory());
                return path;
            };
        }
    }

    /// <summary>
    ///     Класс для работы с созданными объектами, как для
    ///     каждого потока в отдельности, так и заранее известными.
    ///     Распределение по клиентам, работающих в одном или в отдельных потоках
    /// </summary>
    public class RouteThreader: MarshalByRefObject, IRoute
    {
        public const string _RouteThreader = "dkxce.Route.GSolver/21.12.21.23-V4-win32";
        
        public static RouteThreaderInfo mem = new RouteThreaderInfo();
        private static FileMappingServer fms = null;

        public static Type IsDynamic = null;
        // solve timeout, значение без учета построения маршрута с учетом атрибутивных данных линий!
        public static int MaxSolveTime = 5 * 60 * 1000; // 5 minutes // 2.5 mins per start // 2.5 mins per end
        public static int MaxWaitTime = 5 * 60 * 1000; //
        public static bool threadLog = false;
        public static bool threadLogMem = false; ///////////////
        public static List<string> IPBanList = new List<string>();

        public static bool authorization = false;
        public static List<UserPass> Users;        

        private static int _objCount = 0;
        private static List<IRoute> _objects = new List<IRoute>();
        private static Mutex _mtx = new Mutex();

        private static int _ttlCreated = 0;
        private static int _currAlive = 0;
        private int createdID = 0;        

        public RouteThreader()
        {
            createdID = _ttlCreated++;
            _currAlive++;
            UpMem();
        }

        ~RouteThreader()
        {
            _currAlive--;
            UpMem();
        }

        public static void UpMem()
        {
            if ((fms == null) && (threadLogMem))
            {
                fms = new FileMappingServer();
                fms.Connect();
            };
            if (threadLogMem)
            {
                mem.ThreadsAlive = _currAlive;
                mem.DynamicPool = IsDynamic != null;
                mem.MaxSolveTime = MaxSolveTime / 1000; 
                mem.MaxWaitTime = MaxWaitTime / 1000;
                mem.ObjectsUsed = _objCount;
                mem.ThreadsCounted = _ttlCreated;
                mem.ObjectsIdle = RouteThreader._objects.Count;

                mem.ObjectsData = "";
                mem.ThreadRegionsCacheSize = 0;
                int od = 0;
                for (int i = 0; i < _objects.Count; i++)
                {
                    try
                    {
                        if (_objects[i].GetType().ToString() == "dkxce.Route.ServiceSolver.RussiaSolver")
                        {
                            RussiaSolver rs = (RussiaSolver)_objects[i];
                            mem.ObjectsData += od.ToString() + ":" + IntArrayToStr(rs.GetRegionsGlobal) + ":" + IntArrayToStr(rs.GetRegionsPreload) + ":" + IntArrayToStr(rs.GetRegionsSession)+ " - "+(((double)rs.GetRegionsSessionSize)/1024/1024).ToString("0.00").Replace(",",".")+"MB;";
                            od++;
                            mem.ThreadRegionsCacheSize += rs.GetRegionsPreloadSize;                            
                        };

                        if (_objects[i].GetType().ToString() == "dkxce.Route.ServiceSolver.OneRegionSolver")
                        {
                            OneRegionSolver rs = (OneRegionSolver)_objects[i];
                            mem.ObjectsData += od.ToString() + "::" + mem.ThreadRegionsCache + ":;";
                            od++;
                            mem.ThreadRegionsCacheSize += rs.GraphSize;
                        };
                    }
                    catch { };
                };

                fms.WriteData(mem);
            };
        }

        public static string IntArrayToStr(int[] arr)
        {
            string res = "";
            foreach (int i in arr)
                res += (res.Length > 0 ? "," : "") + i.ToString();
            return res;
        }

        /// <summary>
        ///     Добавляем модуль расчета
        /// </summary>
        /// <param name="obj"></param>
        public static void AddObject(IRoute obj)
        {
            RouteThreader._objCount++;
            RouteThreader._objects.Add(obj);
            UpMem();
        }

        /// <summary>
        ///     Привязка точки к дороге
        /// </summary>
        /// <param name="Lat">Широта</param>
        /// <param name="Lon">Долгота</param>
        /// <param name="getNames">Запрашивать наименование дороги (работает дольше)</param>
        /// <returns>Привязанные к дороге точки</returns>
        public RNearRoad[] GetNearRoad(double[] lat, double[] lon, bool getNames)
        {
            RNearRoad[] rr = new RNearRoad[0];
            
            // dynamic
            if (IsDynamic != null)
            {
                _objCount++;
                UpMem();                
                try
                {
                    //rs = (IRoute)Activator.CreateInstance(RouteThreader.IsDynamic);
                    
                    ToCallNearRoad tc = new ToCallNearRoad((IRoute)Activator.CreateInstance(RouteThreader.IsDynamic), new RNearRoadAsk(null,lat,lon,getNames), null);
                    if (RouteThreader.threadLog)
                        XMLSaved<int>.Add2SysLog("RouteThreader TtlCreated " + _ttlCreated.ToString() + " CurrID " + createdID.ToString() + " CurrAlive " + _currAlive.ToString() + " TtlObjs " + _objCount.ToString() + " IdleObjs " + _objects.Count.ToString());
                    System.Threading.Thread thr = new System.Threading.Thread(CalcTimed);
                    thr.Start(tc);
                    thr.Join(RouteThreader.MaxSolveTime);
                    thr.Abort();
                    rr = tc.result;
                    tc.solver = null;
                }
                catch (Exception ex)
                {
                    XMLSaved<int>.AddErr2SysLog("RouteThreader Exception: " + ex.ToString());
                    rr = null;
                };
                _objCount--;
                UpMem();
                return rr;
            };

            // static
            DateTime wst = DateTime.UtcNow;
            IRoute rs = null;
            do
            {                
                if (!_mtx.WaitOne(RouteThreader.MaxWaitTime)) // если клиент итак слишком долго ждет, чтобы не нагружать сервер
                {
                    return null;
                };
                if (RouteThreader._objects.Count > 0)
                {
                    rs = RouteThreader._objects[0];
                    RouteThreader._objects.RemoveAt(0);
                    UpMem();
                };
                _mtx.ReleaseMutex();

                if (DateTime.UtcNow.Subtract(wst).TotalMilliseconds > RouteThreader.MaxWaitTime)
                {
                    return null;
                };

                if (rs == null) Thread.Sleep(50);
            }
            while (rs == null);

            try
            {
                ToCallNearRoad tc = new ToCallNearRoad(rs, new RNearRoadAsk(null,lat, lon, getNames), null);
                if (RouteThreader.threadLog)
                    XMLSaved<int>.Add2SysLog("RouteThreader TtlCreated " + _ttlCreated.ToString() + " CurrID " + createdID.ToString() + " CurrAlive " + _currAlive.ToString() + " TtlObjs " + _objCount.ToString() + " IdleObjs " + _objects.Count.ToString());
                System.Threading.Thread thr = new System.Threading.Thread(CalcTimed);
                thr.Priority = ThreadPriority.AboveNormal;
                thr.Start(tc);                
                thr.Join(RouteThreader.MaxSolveTime);                
                thr.Abort();
                thr = null;                
                rr = tc.result;                
            }
            catch (Exception)
            {
                rr = null;
            };

            _mtx.WaitOne();
            RouteThreader._objects.Add(rs);
            UpMem();
            _mtx.ReleaseMutex();
            
            return rr;
        }

        /// <summary>
        ///     Получение маршрута;
        ///     Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик
        ///         0x08 - использовать исторический трафик
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        ///         0x20 - оптимизировать по расстоянию
        /// </summary>
        /// <param name="stops">Точки маршрута</param>
        /// <param name="startTime">Начальное время</param>
        /// <param name="flags">флаги</param>
        /// <param name="RegionsAvailableToUser">Регионы доступные для пользователя (null = все)</param>
        /// <returns></returns>
        public RResult GetRoute(RStop[] stops, DateTime startTime, long flags, int[] RegionsAvailableToUser)
        {
            return GetRoute(stops, startTime, flags, RegionsAvailableToUser, null, 150, null);
        }

        /// <summary>
        ///     Получение маршрута;
        ///     Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик
        ///         0x08 - использовать исторический трафик
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        ///         0x20 - оптимизировать по расстоянию
        /// </summary>
        /// <param name="stops">Точки маршрута</param>
        /// <param name="startTime">Начальное время</param>
        /// <param name="flags">флаги</param>
        /// <param name="RegionsAvailableToUser">Регионы доступные для пользователя (null = все)</param>
        /// <returns></returns>
        public RResult GetRoute(RStop[] stops, DateTime startTime, long flags, int[] RegionsAvailableToUser,
            dkxce.Route.Classes.PointF[] roadsExcept, double roadsExceptRaduisInMeters, byte[] RoadsOnly)
        {            
            RResult rr = new RResult(stops);            

            // dynamic
            if (IsDynamic != null)
            {
                _objCount++;
                UpMem();
                try
                {
                    //rs = (IRoute)Activator.CreateInstance(RouteThreader.IsDynamic);
                    //rr = rs.GetRoute(stops, startTime, flags, RegionsAvailableToUser);

                    ToCall tc = new ToCall((IRoute)Activator.CreateInstance(RouteThreader.IsDynamic),new RAsk(null,stops,startTime,flags,RegionsAvailableToUser),null,roadsExcept,roadsExceptRaduisInMeters,RoadsOnly);
                    if (RouteThreader.threadLog)
                        XMLSaved<int>.Add2SysLog("RouteThreader TtlCreated " + _ttlCreated.ToString() + " CurrID " + createdID.ToString() + " CurrAlive " + _currAlive.ToString() + " TtlObjs " + _objCount.ToString() + " IdleObjs " + _objects.Count.ToString());
                    System.Threading.Thread thr = new System.Threading.Thread(CalcTimed);
                    thr.Start(tc);
                    thr.Join(RouteThreader.MaxSolveTime);
                    thr.Abort();
                    thr = null;
                    if (tc.result == null)
                        rr.LastError = "101 Маршрут не найден, превышено время расчета";
                    else
                        rr = tc.result;
                    tc.solver = null;
                }
                catch (Exception ex)
                {
                    XMLSaved<int>.AddErr2SysLog("RouteThreader Exception: " + ex.ToString());
                    rr.LastError = "051 RouteThreader Exception: "+ex.Message.ToString();
                };
                _objCount--;
                UpMem();
                return rr;
            };

            // static
            DateTime wst = DateTime.UtcNow;
            IRoute rs = null;
            do
            {
                if (!_mtx.WaitOne(RouteThreader.MaxWaitTime)) // если клиент итак слишком долго ждет, чтобы не нагружать сервер
                {
                    rr.LastError = "102 Расчет маршрута не начинался, превышено время ожидания";
                    return rr;
                };
                if (RouteThreader._objects.Count > 0)
                {                    
                    rs = RouteThreader._objects[0];
                    RouteThreader._objects.RemoveAt(0);
                    UpMem();
                };
                _mtx.ReleaseMutex();

                if(DateTime.UtcNow.Subtract(wst).TotalMilliseconds > RouteThreader.MaxWaitTime )
                {
                    rr.LastError = "102 Расчет маршрута не начинался, превышено время ожидания";
                    return rr;
                };

                if (rs == null) Thread.Sleep(50);
            }
            while(rs == null);

            try
            {
                // rr = rs.GetRoute(stops, startTime, flags, RegionsAvailableToUser);
                ToCall tc = new ToCall(rs, new RAsk(null, stops, startTime, flags, RegionsAvailableToUser), null, roadsExcept, roadsExceptRaduisInMeters, RoadsOnly);
                if(RouteThreader.threadLog) 
                    XMLSaved<int>.Add2SysLog("RouteThreader TtlCreated " + _ttlCreated.ToString() + " CurrID " + createdID.ToString() + " CurrAlive " + _currAlive.ToString() + " TtlObjs " + _objCount.ToString() + " IdleObjs " + _objects.Count.ToString());
                System.Threading.Thread thr = new System.Threading.Thread(CalcTimed);
                thr.Priority = ThreadPriority.AboveNormal;
                thr.Start(tc);
                thr.Join(RouteThreader.MaxSolveTime);
                thr.Abort();
                thr = null;
                if (tc.result == null)
                    rr.LastError = "101 Маршрут не найден, превышено время расчета";
                else
                    rr = tc.result;
            }
            catch (Exception ex)
            {
                XMLSaved<int>.AddErr2SysLog("RouteThreader Exception: " + ex.ToString());
                rr.LastError = "051 RouteThreader Exception: " + ex.Message.ToString();
            };            

            _mtx.WaitOne();
            RouteThreader._objects.Add(rs);
            UpMem();
            _mtx.ReleaseMutex();

            return rr;
        }

        /// <summary>
        ///     Используется для передачи параметров в отдельный поток с таймаутом выполнения
        /// </summary>
        private class ToCall
        {
            public IRoute solver = null;
            public RAsk data = null;
            public RResult result = null;
            //
            public dkxce.Route.Classes.PointF[] roadsExcept;
            public double roadsExceptRadiusInMeters = 50;
            public byte[] roadsOnly = new byte[16];
            //
            public ToCall(IRoute solver, RAsk data, RResult result,
                dkxce.Route.Classes.PointF[] roadsExcept, double roadsExceptRadiusInMeters, byte[] roadsOnly)
            {
                this.solver = solver;
                this.data = data;
                this.result = result;
                this.roadsExcept = roadsExcept;
                this.roadsExceptRadiusInMeters = roadsExceptRadiusInMeters;
                this.roadsOnly = roadsOnly;
            }
        }

        /// <summary>
        ///     Используется для передачи параметров в отдельный поток с таймаутом выполнения
        /// </summary>
        private class ToCallNearRoad
        {
            public IRoute solver = null;
            public RNearRoadAsk data = null;
            public RNearRoad[] result = null;
            public ToCallNearRoad(IRoute solver, RNearRoadAsk data, RNearRoad[] result)
            {
                this.solver = solver;
                this.data = data;
                this.result = result;
            }
        }

        /// <summary>
        ///     Поток с расчетом для использования с таймаутом
        /// </summary>
        /// <param name="obj"></param>
        private void CalcTimed(object obj)
        {
            ToCall tc = null;
            ToCallNearRoad nr = null;
            try
            {
                tc = (ToCall)obj;
                tc.result = tc.solver.GetRoute(tc.data.stops, tc.data.startTime, tc.data.flags, tc.data.RegionsAvailableToUser,tc.roadsExcept,tc.roadsExceptRadiusInMeters,tc.roadsOnly);            
            }
            catch { };
            try
            {
                nr = (ToCallNearRoad)obj;
                nr.result = nr.solver.GetNearRoad(nr.data.lat, nr.data.lon, nr.data.getNames);
            }
            catch { };
        }

        /// <summary>
        ///     Общее число потоков на сервере
        /// </summary>
        /// <returns></returns>
        public int GetThreadsCount()
        {
            return RouteThreader._objCount;
        }

        /// <summary>
        ///     Общее число потоков на сервере
        /// </summary>
        /// <returns></returns>
        public static int getThreadsCount()
        {
            return RouteThreader._objCount;
        }

        /// <summary>
        ///     Число свободных потоков на сервере
        /// </summary>
        /// <returns></returns>
        public static int getIdleThreadCount()
        {
            return RouteThreader._objects.Count;
        }

        /// <summary>
        ///     Число свободных потоков на сервере
        /// </summary>
        /// <returns></returns>
        public int GetIdleThreadCount()
        {
            return RouteThreader._objects.Count;
        }
    }

    /// <summary>
    ///     TCP сервер для входящих запросов
    /// </summary>
    public class RouteTCPListenter
    {
        private Thread mainThread = null;
        private TcpListener mainListener = null;
        private IPAddress ListenIP = IPAddress.Any;
        private int ListenPort = 7755;
        private bool isRunning = false;
        private int MaxThreads = 1;

        private int ThreadsCount = 0;

        private Licenses lics = null;

        public RouteTCPListenter(int Port, int MaxThreads)
        {
            //// if use ThreadPool
            //// Установим минимальное количество рабочих потоков
            //ThreadPool.SetMinThreads(2, 2);
            //// Установим максимальное количество рабочих потоков
            //// Максимальное количество потоков должно быть не меньше двух, 
            //// так как в это число входит основной поток. 
            //// Если установить единицу, то обработка клиента будет возможна лишь тогда, 
            //// когда основной поток приостановил работу (например, ожидает нового клиента или была вызвана процедура Sleep).
            //ThreadPool.SetMaxThreads(MaxThreads, MaxThreads);
            //// Принимаем новых клиентов. После того, как клиент был принят, он передается в новый поток (ClientThread)
            //// с использованием пула потоков.

            this.ListenPort = Port;
            this.MaxThreads = MaxThreads;

            lics = new Licenses();
            #if LICENSE
            {
                // default: `dkxce.Route.ServiceSolver.lic`
                string licFile = GetCurrentExe().Remove(GetCurrentExe().Length-4)+".lic";
                // Navicom Web HTTP RouteSolver.dll Key = D6AEA3EC848644258DCD06A781B2C036
                // Navicom Route Map Test Key = D6AEA3EC848644258DCD06A781B2C036
                // TO SAVE LICENSES USE `nmsRouteLicenseFileTool`
            
                // fix if system date changed wrong
                if (DateTime.UtcNow < System.IO.File.GetLastWriteTimeUtc(GetCurrentExe())) throw new Exception("License Error [A]");
                if (!File.Exists(licFile)) throw new Exception("License Error [B]");
                if (DateTime.UtcNow < System.IO.File.GetLastWriteTimeUtc(licFile)) throw new Exception("License Error [C]");

                System.IO.FileStream fs;
                try
                {
                    fs = new FileStream(licFile, FileMode.Open);
                }
                catch (Exception ex) { throw new Exception("License Error [D0]"+" ["+ex.ToString()+"]"); };

                try
                {
                    byte[] buffer = new byte[65536];
                    ICSharpCode.SharpZipLib.GZip.GZipInputStream ins = new ICSharpCode.SharpZipLib.GZip.GZipInputStream(fs);
                    int length = ins.Read(buffer, 0, buffer.Length);
                    ins.Close();
                    fs.Close();
                    string coded = System.Text.Encoding.ASCII.GetString(buffer, 0, length);
                    string xml = Licenses.Crypto.DecryptStringAES(coded, "zSx1b0Izlk");
                    lics = XMLSaved<Licenses>.LoadText(xml);                    
                }
                catch { throw new Exception("License Error [D]"); };

                if (lics.keys.Count == 0) throw new Exception("License Error [E]");
                if (lics.keys.IndexOf("") >= 0) throw new Exception("License Error [F]");
            };            
            #endif
        }        
        
        public bool Running { get { return isRunning; } }
        public IPAddress ServerIP { get { return ListenIP; } }
        public int ServerPort { get { return ListenPort; } }

        public void Dispose() { Stop(); }
        ~RouteTCPListenter() { Dispose(); }

        public virtual void Start()
        {
            if (isRunning) throw new Exception("Server Already Running!");

            isRunning = true;
            mainThread = new Thread(MainThread);
            mainThread.Start();
        }

        private void MainThread()
        {
            mainListener = new TcpListener(this.ListenIP, this.ListenPort);
            mainListener.Start();
            while (isRunning)
            {
                try
                {
                    TcpClient client = mainListener.AcceptTcpClient();
                    if (RouteThreader.IPBanList.Contains(((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString()))
                    {
                        client.Close();
                        continue;
                    };

                    if (this.MaxThreads > 1) // multithread or multiclient
                    {
                        while ((this.ThreadsCount >= this.MaxThreads) && isRunning) // wait for any closed thread
                            System.Threading.Thread.Sleep(5);
                        if (!isRunning) break; // break if stopped
                        Thread thr = new Thread(ClientThread); // create new thread for new client
                        thr.Start(client);
                    }
                    else // single thread
                        ClientThread(client);

                    //
                    // if use ThreadPool
                    // if (this.MaxThreads > 1) // multithread or multiclient
                    //     ThreadPool.QueueUserWorkItem(new WaitCallback(ClientThread), client);
                    // else // single
                    //     ClientThread(client);
                    //
                }
                catch { Thread.Sleep(1); };                                
            };
        }

        public virtual void Stop()
        {
            if (!isRunning) return;

            isRunning = false;

            if (mainListener != null) mainListener.Stop();
            mainListener = null;

            mainThread.Join();
            mainThread = null;
        }

        /// <summary>
        ///     Client exchange
        /// </summary>
        /// <param name="data">TcpClient</param>
        private void ClientThread(object data)
        {
            ThreadsCount++;
            TcpClient client = (TcpClient)data;
            byte[] buff = new byte[21];
            client.ReceiveTimeout = 9000; // 9 seconds

            int read = 0;            
            try { read = client.GetStream().Read(buff, 0, buff.Length); }
            catch { client.Close(); ThreadsCount--; return; };

            if (read != 21) // 21 (0..20) // не наш клиент
            {
                client.Close();
                ThreadsCount--;
                return;
            };
            
            string code = System.Text.Encoding.GetEncoding(1251).GetString(buff,0,21);
            if (code != "dkxce.Route.TCPSolver") // не наш клиент
            {
                client.Close();
                ThreadsCount--;
                return;
            };
            
            byte method = 0;
            try { method = (byte)client.GetStream().ReadByte(); }
            catch { client.Close(); ThreadsCount--; return; };
            if (method > 7)  // не поддерживаемый метод
            {
                client.Close();
                ThreadsCount--;
                return;
            };

            // response //
            try
            {
                client.GetStream().Write(buff, 0, buff.Length);
                client.GetStream().WriteByte(method);
                if (method == 0) // ThreadsCount
                    client.GetStream().WriteByte((byte)RouteThreader.getThreadsCount());
                if (method == 1) // IdleThreadCount
                    client.GetStream().WriteByte((byte)RouteThreader.getIdleThreadCount());
                if ((method == 2) || (method == 6)) // GetRoute (binary result) or 6
                {                    
                    BinaryFormatter bf = new BinaryFormatter();
                    RAsk ask = (RAsk)bf.Deserialize(client.GetStream());
                    RResult rr;

                    // roadsOnly
                    dkxce.Route.Classes.PointF[] roadsExcept = null;
                    double roadsExceptRadiusInMeters = 50;
                    byte[] roadsOnly = new byte[16];
                    if (method == 6)
                    {
                        byte[] indata = new byte[16];
                        client.GetStream().Read(indata, 0, indata.Length);
                        Array.Copy(indata, roadsOnly, indata.Length);
                        indata = new byte[8];
                        client.GetStream().Read(indata, 0, indata.Length);
                        roadsExceptRadiusInMeters = BitConverter.ToDouble(indata, 0);
                        indata = new byte[2];
                        client.GetStream().Read(indata, 0, indata.Length);
                        ushort points = BitConverter.ToUInt16(indata, 0);
                        roadsExcept = new dkxce.Route.Classes.PointF[points];
                        if (points > 0)
                        {
                            indata = new byte[points * 2 * 8];
                            client.GetStream().Read(indata, 0, indata.Length);
                            for (int i = 0; i < points; i++)
                            {
                                double Y = BitConverter.ToDouble(indata, i * 2 * 8);
                                double X = BitConverter.ToDouble(indata, i * 2 * 8 + 8);
                                roadsExcept[i] = new dkxce.Route.Classes.PointF((float)X, (float)Y);
                            };
                        };
                    };
                    // end roadsOnly
                    
                    #if LICENSE
                    // CHECK LICENSES                    
                    int lIndex = lics.keys.IndexOf(ask.licenseKey);
                    if (lIndex < 0)
                    {
                        rr = new RResult(null);
                        rr.LastError = "001 BAD KEY";
                    }
                    else if (DateTime.Now > lics.expires[lIndex])
                    {
                        rr = new RResult(null);
                        rr.LastError = "002 Support Expired";
                    }
                    else
                    #endif                    
                    // CALL SOLVER //
                    if(method == 6)
                        rr = (new RouteThreader()).GetRoute(ask.stops, ask.startTime, ask.flags, ask.RegionsAvailableToUser, roadsExcept,roadsExceptRadiusInMeters,roadsOnly);
                    else
                        rr = (new RouteThreader()).GetRoute(ask.stops, ask.startTime, ask.flags, ask.RegionsAvailableToUser);
                    bf.Serialize(client.GetStream(), rr);
                };
                if ((method == 3) || (method == 7)) // GetRoute (XML result) or 7
                {
                    buff = new byte[20480];
                    try { read = client.GetStream().Read(buff, 0, buff.Length); }
                    catch { client.Close(); ThreadsCount--; return; };
                    if (read == 0) { client.Close(); ThreadsCount--; return; };
                    RAsk ask = null;
                    int dataSize = 0;
                    try { ask = ParseMethod3Request(buff, out dataSize); }
                    catch (Exception ex)
                    {
                        XMLSaved<int>.Add2SysLog(ex.ToString());
                        buff = System.Text.Encoding.GetEncoding(1251).GetBytes("ERROR PARCING REQUEST");
                        client.GetStream().Write(buff,0,buff.Length); client.Close(); ThreadsCount--; return; 
                    };
                    RResult rr;

                    // roadsOnly
                    dkxce.Route.Classes.PointF[] roadsExcept = null;
                    double roadsExceptRadiusInMeters = 50;
                    byte[] roadsOnly = new byte[16];
                    if (method == 7)
                    {
                        Array.Copy(buff, dataSize, roadsOnly, 0, 16);
                        roadsExceptRadiusInMeters = BitConverter.ToDouble(buff, dataSize + 16);
                        ushort points = BitConverter.ToUInt16(buff, dataSize + 16 + 8);
                        if (points > 0)
                        {
                            roadsExcept = new dkxce.Route.Classes.PointF[points];
                            for (int i = 0; i < points; i++)
                            {
                                double Y = BitConverter.ToDouble(buff, dataSize + 16 + 8 + 2 + i * 2 * 8);
                                double X = BitConverter.ToDouble(buff, dataSize + 16 + 8 + 2 + i * 2 * 8 + 8);
                                roadsExcept[i] = new dkxce.Route.Classes.PointF((float)X, (float)Y);
                            };
                        };
                    };
                    // end roadsOnly

                    #if LICENSE
                    // CHECK LICENSES                    
                    int lIndex = lics.keys.IndexOf(ask.licenseKey);
                    if (lIndex < 0)
                    {
                        rr = new RResult(null);
                        rr.LastError = "001 BAD KEY";
                    }
                    else if (DateTime.Now > lics.expires[lIndex])
                    {
                        rr = new RResult(null);
                        rr.LastError = "002 Support Expired";
                    }
                    else
                    #endif
                    // CALL SOLVER
                    if (method == 7)
                        rr = (new RouteThreader()).GetRoute(ask.stops, ask.startTime, ask.flags, ask.RegionsAvailableToUser,
                            roadsExcept, roadsExceptRadiusInMeters, roadsOnly);
                    else
                        rr = (new RouteThreader()).GetRoute(ask.stops, ask.startTime, ask.flags, ask.RegionsAvailableToUser);

                    RouteFormatAsHTTPXML.Route route = RouteFormatAsHTTPXML.GetRoute(rr);
                    XmlWriterSettings s_ = new XmlWriterSettings();
                    s_.Encoding = defEnd;
                    s_.Indent = false;
                    s_.NewLineHandling = NewLineHandling.None;
                    XmlWriter writer = XmlWriter.Create(client.GetStream(),s_);
                    System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(RouteFormatAsHTTPXML.Route));
                    XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
                    ns.Add("", "");
                    xs.Serialize(writer, route, ns);
                };
                if (method == 4) // GetNearRoad (binary result)
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    RNearRoadAsk ask = (RNearRoadAsk)bf.Deserialize(client.GetStream());
                    RNearRoad[] nr;
                    #if LICENSE
                    // CHECK LICENSES                    
                    int lIndex = lics.keys.IndexOf(ask.licenseKey);
                    if (lIndex < 0)
                    {
                        nr = new RNearRoad[] { new RNearRoad(0, 0, 0, "001 BAD KEY") };
                    }
                    else if (DateTime.Now > lics.expires[lIndex])
                    {
                        nr = new RNearRoad[] { new RNearRoad(0, 0, 0, "002 Support Expired") };
                    }
                    else
                    #endif
                    // CALL SOLVER
                    nr = (new RouteThreader()).GetNearRoad(ask.lat, ask.lon, ask.getNames);
                    bf.Serialize(client.GetStream(), nr);;
                };
                if (method == 5) // GetNearRoad (XML result)
                {
                    buff = new byte[4096];
                    try { read = client.GetStream().Read(buff, 0, buff.Length); }
                    catch { client.Close(); ThreadsCount--; return; };
                    if (read == 0) { client.Close(); ThreadsCount--; return; };
                    RNearRoadAsk ask = null;
                    try { ask = ParseMethod5Request(buff); }
                    catch (Exception ex)
                    {
                        XMLSaved<int>.Add2SysLog(ex.ToString());
                        buff = System.Text.Encoding.GetEncoding(1251).GetBytes("ERROR PARCING REQUEST");
                        client.GetStream().Write(buff, 0, buff.Length); client.Close(); ThreadsCount--; return;
                    };
                    RNearRoad[] nr;
                    #if LICENSE
                    // CHECK LICENSES                    
                    int lIndex = lics.keys.IndexOf(ask.licenseKey);
                    if (lIndex < 0)
                    {
                        nr = new RNearRoad[] { new RNearRoad(0, 0, 0, "001 BAD KEY") };
                    }
                    else if (DateTime.Now > lics.expires[lIndex])
                    {
                        nr = new RNearRoad[] { new RNearRoad(0, 0, 0, "002 Support Expired") };
                    }
                    else
                    #endif
                    // CALL SOLVER
                    nr = (new RouteThreader()).GetNearRoad(ask.lat, ask.lon, ask.getNames);
                    RouteFormatAsHTTPXML.NearRoad[] oof = null;
                    if (nr != null) oof = new RouteFormatAsHTTPXML.NearRoad[nr.Length];
                    for (int i = 0; i < nr.Length; i++)
                    {
                        oof[i] = new RouteFormatAsHTTPXML.NearRoad();
                        oof[i].lat = nr[i].lat;
                        oof[i].lon = nr[i].lon;
                        oof[i].distance = nr[i].distance;
                        oof[i].name = nr[i].name;
                        oof[i].attributes = nr[i].attributes;
                        oof[i].region = nr[i].region;
                    };

                    XmlWriterSettings s_ = new XmlWriterSettings();
                    s_.Encoding = defEnd;
                    s_.Indent = false;
                    s_.NewLineHandling = NewLineHandling.None;
                    XmlWriter writer = XmlWriter.Create(client.GetStream(), s_);
                    System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(RouteFormatAsHTTPXML.NearRoad[]));
                    XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
                    ns.Add("", "");
                    xs.Serialize(writer, oof, ns);
                };
                
            }
            catch (Exception)
            { 
                client.Close(); ThreadsCount--; return; 
            };

            client.GetStream().Flush();
            client.GetStream().Close();
            client.Close();
            ThreadsCount--;
        }
        public static System.Text.Encoding defEnd = System.Text.Encoding.GetEncoding(1251);

        /*
                // PACKET
	            [BYTE] LicenseKey Length
	            [....] LicenseKey
	            [BYTE] Stops Count
	            {
		            [BYTE] STOP N Name Length
		            [....] STOP N Name
		            [SINGLE] LAT
		            [SINGLE] LON
	            }
	            [DOUBLE] StartTime
	            [LONG] FLAGS
	            [BYTE] Region Count
	            {
		            [BYTE] Region ID
	            }
        */
        /// <summary>
        ///     Method 3 Parse
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private RAsk ParseMethod3Request(byte[] data, out int dataSize)
        {
            RAsk ra = new RAsk();
            ra.licenseKey = System.Text.Encoding.GetEncoding(1251).GetString(data, 1, data[0]);
            int z = 1 + data[0];
            ra.stops = new RStop[data[z++]];
            for (int i = 0; i < ra.stops.Length; i++)
            {
                byte nl = data[z++];
                string n = System.Text.Encoding.GetEncoding(1251).GetString(data, z, nl);
                z += nl;
                double lat = BitConverter.ToDouble(data, z);
                z += 8;
                double lon = BitConverter.ToDouble(data, z);
                z += 8;
                ra.stops[i] = new RStop(n, lat, lon);
            };
            ra.startTime = DateTime.FromOADate(BitConverter.ToDouble(data, z));
            z += 8;
            ra.flags = BitConverter.ToUInt32(data, z);
            z += 8;
            ra.RegionsAvailableToUser = new int[data[z++]];
            for (int i = 0; i < ra.RegionsAvailableToUser.Length; i++)
                ra.RegionsAvailableToUser[i] = data[z++];
            dataSize = z;
            return ra;
        }
        /// <summary>
        ///     Method 5 Parse
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private RNearRoadAsk ParseMethod5Request(byte[] data)
        {
            RNearRoadAsk ra = new RNearRoadAsk(null,null,null,false);
            ra.licenseKey = System.Text.Encoding.GetEncoding(1251).GetString(data, 1, data[0]);
            int z = 1 + data[0];
            ra.getNames = BitConverter.ToBoolean(data, z);
            ushort count = BitConverter.ToUInt16(data, z+1);
            ra.lat = new double[count];
            ra.lon = new double[count];
            for (int i = 0; i < ra.lat.Length; i++)
            {
                ra.lat[i] = BitConverter.ToDouble(data, z + 3 + 16 * i);
                ra.lon[i] = BitConverter.ToDouble(data, z + 3 + 16 * i + 8);
            };
            return ra;
        }

        public class RouteFormatAsHTTPXML
        {

            public static Route GetRoute(RResult rr)
            {
                Route rt = new Route();
                rt.LastError = rr.LastError;
                rt.driveLength = rr.driveLength;
                rt.driveLengthSegments = rr.driveLengthSegments;
                rt.driveTime = rr.driveTime;
                rt.driveTimeSegments = rr.driveTimeSegments;
                rt.startTime = rr.startTime;
                rt.finishTime = rr.finishTime;
                rt.LastError = rr.LastError;
                rt.stops = null;
                if (rr.stops != null)
                {
                    rt.stops = new Stop[rr.stops.Length];
                    for (int i = 0; i < rr.stops.Length; i++)
                        rt.stops[i] = new Stop(rr.stops[i].name, rr.stops[i].lat, rr.stops[i].lon);
                };
                
                if (rr.vector != null)
                {
                    List<XYPoint> vec = new List<XYPoint>();
                    for (int i = 0; i < rr.vector.Length; i++)
                        vec.Add(new XYPoint(rr.vector[i].X, rr.vector[i].Y));
                    rt.polyline = vec.ToArray();
                    rt.polylineSegments = rr.vectorSegments;
                };
                if (rr.description != null)
                {
                    rt.instructions = new RoutePoint[rr.description.Length];
                    for (int i = 0; i < rt.instructions.Length; i++)
                    {
                        double segt = i == rt.instructions.Length - 1 ? 0 : (double)rr.description[i + 1].time - (double)rr.description[i].time;
                        double segl = i == rt.instructions.Length - 1 ? 0 : (double)rr.description[i + 1].dist - (double)rr.description[i].dist;
                        rt.instructions[i] = new RoutePoint(i, (double)rr.description[i].Lon, (double)rr.description[i].Lat, segl, segt, (double)rr.description[i].dist, rr.startTime.AddMinutes(rr.description[i].time));
                        rt.instructions[i].iStreet = rr.description[i].name;

                        if (rr.description[i].instructions.Length > 0)
                            rt.instructions[i].iToDo = rr.description[i].instructions[0];
                        if (rr.description[i].instructions.Length > 1)
                            rt.instructions[i].iToGo = rr.description[i].instructions[1];
                    };
                    rt.instructionsSegments = rr.descriptionSegments;
                };
                return rt;
            }

            /// <summary>
            ///     Маршруные точки
            /// </summary>
            [Serializable]
            public class Stop
            {
                /// <summary>
                ///     Имя
                /// </summary>
                [XmlText]
                public string name = "";
                /// <summary>
                ///     Широта
                /// </summary>
                [XmlAttribute()]
                public double lat = 0;
                /// <summary>
                ///     Долгота
                /// </summary>
                [XmlAttribute()]
                public double lon = 0;

                public Stop() { }

                public Stop(string name, double lat, double lon)
                {
                    this.name = name;
                    this.lat = lat;
                    this.lon = lon;
                }
            }

            /// <summary>
            ///     Сведения о маршруте
            /// </summary>
            [Serializable]
            public class Route
            {
                public Route() { }

                /// <summary>
                ///     Длина маршрута в км
                /// </summary>
                public double driveLength = 0;
                /// <summary>
                ///      расстояние между промежуточными точками маршрута
                /// </summary>
                [XmlArrayItem("dls")]
                public double[] driveLengthSegments = new double[0];
                /// <summary>
                ///     Время в пути в мин
                /// </summary>
                public double driveTime = 0;
                /// <summary>
                ///      время между промежуточными точками маршрута
                /// </summary>
                [XmlArrayItem("dts")]
                public double[] driveTimeSegments = new double[0];
                /// <summary>
                ///     Время выезда
                /// </summary>
                public DateTime startTime = DateTime.Now;
                /// <summary>
                ///     Время прибытия
                /// </summary>
                public DateTime finishTime = DateTime.Now;
                /// <summary>
                ///     Маршрутные точки
                /// </summary>
                [XmlArrayItem("stop")]
                public Stop[] stops = new Stop[0];
                /// <summary>
                ///     полилиния маршрута
                /// </summary>
                [XmlArrayItem("p")]
                public XYPoint[] polyline = new XYPoint[0];
                /// <summary>
                ///      индекс, указывающий на элемент массива polyline для каждого
                ///      участка между промежуточными точками маршрута
                /// </summary>
                [XmlArrayItem("ps")]
                public int[] polylineSegments = new int[0];
                /// <summary>
                ///     инструкции
                /// </summary>
                [XmlArrayItem("i")]
                public RoutePoint[] instructions = new RoutePoint[0];
                /// <summary>
                ///      индекс, указывающий на элемент массива instructions для каждого
                ///      участка между промежуточными точками маршрута
                /// </summary>
                [XmlArrayItem("is")]
                public int[] instructionsSegments = new int[0];

                /// <summary>
                ///     Ошибка, если есть
                /// </summary>
                public string LastError = String.Empty;                
            }
           
            [Serializable]
            public class NearRoad
            {
                public double distance;
                public double lat;
                public double lon;
                public string name;
                public string attributes;
                public int region;
            }

            /// <summary>
            ///     Точка маршрута
            /// </summary>
            [Serializable]
            public class XYPoint
            {
                /// <summary>
                ///     Долгота
                /// </summary>
                [XmlAttribute()]
                public double x = 0;
                /// <summary>
                ///     Широта
                /// </summary>
                [XmlAttribute()]
                public double y = 0;

                public XYPoint(double x, double y)
                {
                    this.x = x;
                    this.y = y;
                }

                public XYPoint() { }
            }

            /// <summary>
            ///     Инструкция маршрута
            /// </summary>
            [Serializable]
            public class RoutePoint
            {
                public RoutePoint() { }

                public RoutePoint(int no, double x, double y, double segmentLength, double segmentTime, double totalLength, DateTime totalTime)
                {
                    this.no = no;
                    this.x = x;
                    this.y = y;
                    this.sLen = segmentLength;
                    this.sTime = segmentTime;
                    this.tLen = totalLength;
                    this.tTime = totalTime;
                }

                /// <summary>
                ///     Нумрация с 1
                /// </summary>
                [XmlAttribute()]
                public int no = 0;

                /// <summary>
                ///     Инструкция1
                /// </summary>
                public string iToDo = "";
                /// <summary>
                ///     Инструкция2
                /// </summary>
                public string iToGo = "";
                /// <summary>
                ///     Инструкция1
                /// </summary>
                public string iStreet = "";

                /// <summary>
                ///     Долгота
                /// </summary>
                [XmlAttribute()]
                public double x = 0;
                /// <summary>
                ///     Широта
                /// </summary>
                [XmlAttribute()]
                public double y = 0;

                /// <summary>
                ///     Время текущего сегмента в мин
                /// </summary>
                [XmlAttribute()]
                public double sTime = 0;

                /// <summary>
                ///     Длина текущего сегмента в км
                /// </summary>
                [XmlAttribute()]
                public double sLen = 0;

                /// <summary>
                ///     Время прибытия в начало сегмента
                /// </summary>
                [XmlAttribute()]
                public DateTime tTime = DateTime.Now;

                /// <summary>
                ///     Длина от начала маршрута до сегмента
                /// </summary>
                [XmlAttribute()]
                public double tLen = 0;
            }
        }

        public static string GetCurrentExe()
        {
            string fname = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase.ToString();
            fname = fname.Replace("file:///", "");
            fname = fname.Replace("/", @"\");
            return fname;
        }
    }


    [Serializable]
    public class SvcConfig
    {
        /// <summary>
        ///     Exchange Protocol: tcp/remoting
        /// </summary>
        public string defProto = "tcp";// tcp/remoting
        /// <summary>
        ///     Default Use Area:
        ///     one - only one preloaded region;
        ///     multiple - full map;
        /// </summary>
        public string defArea = "multiple"; // one/multiple
        /// <summary>
        ///     Default URI
        /// </summary>
        // <defChannel desc="Remoting URI, currently not used" default="dkxce.Route.TCPSolver">dkxce.Route.TCPSolver</defChannel>
        public string defChannel = "dkxce.Route.TCPSolver";
        /// <summary>
        ///     Default RouteServer TCP Port
        /// </summary>
        public int defPort = 7755;
        /// <summary>
        ///     Default Mode:
        ///     single - one preloaded thread for everyone
        ///     multithread - creates new object for each client
        ///     multiclient - create & preload multiple objects
        /// </summary>
        public string defMode = "single"; // single/multithread/multiclient
        /// <summary>
        ///     multithread min threads count
        /// </summary>
        public byte minThreads = 2; // multithread min threads count
        /// <summary>
        ///     multithread max threads count
        /// </summary>
        public byte maxThreads = 8; // multithread max threads count
        /// <summary>
        ///     multithread/multiclient fixed threads count, use only if value more than 1
        /// </summary>
        public int fixThreads = -1;
        /// <summary>
        ///     multiclient objects count
        /// </summary>
        public byte mucObjects = 4; // multiclient objects count
        /// <summary>
        ///     preload regions to all threads, used for multithread & multiclient modes
        /// </summary>
        private string _globalRegions = "";
        /// <summary>
        ///     preload regions to current thread, used for multithread & multiclient modes
        /// </summary>
        private string _threadRegions = "10"; // 10 - lipetsk, 11 - msk ,24 - spb

        private int[] _gr = new int[0];
        private int[] _tr = new int[0];

        /// <summary>
        ///     Region IDs to preload for all threads (format: x,x,x,...,x)
        /// </summary>
        public string globalRegions
        {
            get
            {
                return _globalRegions;
            }
            set
            {
                _globalRegions = value;
                _gr = new int[0];
                if (globalRegions == String.Empty) return;

                string[] gr = globalRegions.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                _gr = new int[gr.Length];
                for (int i = 0; i < gr.Length; i++) _gr[i] = Convert.ToInt32(gr[i].Trim());
            }
        }
        /// <summary>
        ///     Region IDs to preload for current thread (format: x,x,x...,x)
        /// </summary>
        public string threadRegions
        {
            get
            {
                return _threadRegions;
            }
            set
            {
                _threadRegions = value;
                _tr = new int[0];
                if (threadRegions == String.Empty) return;

                string[] gr = threadRegions.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                _tr = new int[gr.Length];
                for (int i = 0; i < gr.Length; i++) _tr[i] = Convert.ToInt32(gr[i].Trim());
            }
        }

        /// <summary>
        ///     Вывод информации о потоках в системный лог
        /// </summary>
        public bool threadLog = false;

        /// <summary>
        ///     Вывод информации о потоках в память
        /// </summary>
        public bool threadLogMem = false;

        /// <summary>
        ///     Номер порта HTTP-сервера
        /// </summary>
        public int defHTTP = 80;

        public static SvcConfig Load()
        {
            return XMLSaved<SvcConfig>.Load(XMLSaved<int>.GetCurrentDir() + @"\dkxce.Route.ServiceSolver.xml");
        }

        public static SvcConfig Load(string fileName)
        {
            return XMLSaved<SvcConfig>.Load(fileName);
        }

        /// <summary>
        ///     TCP Protocol ?
        /// </summary>
        public bool ProtoTCP { get { return (defProto == "tcp") || (defProto == "dual"); } }
        /// <summary>
        ///     Remoting Protocol
        /// </summary>
        public bool ProtoRemoting { get { return defProto == "remoting"; } }
        /// <summary>
        ///     HTTP Protocol
        /// </summary>
        public bool ProtoHTTP { get { return (defProto == "http") || (defProto == "dual"); } }
        /// <summary>
        ///     single mode ?
        /// </summary>
        public bool IsSingle { get { return defMode == "single"; } }
        /// <summary>
        ///     multithread mode ?
        /// </summary>
        public bool IsMultiThread { get { return defMode == "multithread"; } }
        /// <summary>
        ///     multiclient mode ?
        /// </summary>
        public bool IsMultiClient { get { return defMode == "multiclient"; } }
        /// <summary>
        ///     get regions to preload for all threads
        /// </summary>
        public int[] IsGlobalRegions { get { return _gr; }}
        /// <summary>
        ///     get regions to preload for current thread
        /// </summary>
        public int[] IsThreadRegions { get { return _tr; }}
        /// <summary>
        ///     is region is preloaded to all threads ?
        /// </summary>
        /// <param name="regionId"></param>
        /// <returns></returns>
        public bool InGlobalRegions(int regionId)
        {
            if (_gr.Length == 0) return false;
            foreach (int rid in _gr) if (rid == regionId) return true;
            return false;
        }
        /// <summary>
        ///     is region is preloaded for current thread
        /// </summary>
        /// <param name="regionId"></param>
        /// <returns></returns>
        public bool InThreadRegions(int regionId)
        {
            if (_tr.Length == 0) return false;
            foreach (int rid in _tr) if (rid == regionId) return true;
            return false;
        }
        
        /// <summary>
        ///         
        /// </summary>
        public DirectoryXML MultiRegion = new DirectoryXML();

        /// <summary>
        ///     Path to single graph file (.rt)
        /// </summary>
        public string OneRegion = "017.rt"; //  

        [XmlArray("banlist"), XmlArrayItem("ip")]
        public List<string> IPBanList;

        [XmlElement("http.authorization")]
        public bool authorization = false;
        [XmlArray("http.users"), XmlArrayItem("user")]
        public List<UserPass> Users;
        [XmlElement("http.showhost")]
        public bool http_showhost = false;
        [XmlElement("http.showip")]
        public bool http_showip = false;
        [XmlElement("http.description")]
        public string http_description = null;
        [XmlElement("http.html")]
        public string http_html = null;
        [XmlArray("http.licenses"), XmlArrayItem("license")]
        public List<HTTPLicense> http_licenses;
    }

    [Serializable]
    public class UserPass
    {
        [XmlAttribute("name")]
        public string user;
        [XmlAttribute("pass")]
        public string pass;        
    }

    [Serializable]
    public class DirectoryXML
    {
        public string Graphs = "GRAPHS";
        public string Regions = "Regions";
        //public string RGNodes = "RGNodes";
        public string RGWays = "RGWays";

        public string GraphDirectory() { return Path.IsPathRooted(Graphs) ? Graphs : XMLSaved<int>.GetCurrentDir() + Graphs; }
        public string RegionsDirectory() { return Path.IsPathRooted(Regions) ? Regions : XMLSaved<int>.GetCurrentDir() + Regions; }
        //public string RGNodesDirectory() { return Path.IsPathRooted(RGNodes) ? RGNodes : XMLSaved<int>.GetCurrentDir() + RGNodes; }
        public string RGWaysDirectory() { return Path.IsPathRooted(RGWays) ? RGWays : XMLSaved<int>.GetCurrentDir() + RGWays; }
    }

    [Serializable]
    public class HTTPLicense
    {
        [XmlAttribute("key")]
        public string key;
        [XmlAttribute("expires")]
        public DateTime expires;
        [XmlAttribute("ip")]
        public string ip;
    }
    
    /// <summary>
    ///     Licenses Infos
    /// </summary>
    [Serializable]
    public class Licenses
    {
        /// <summary>
        ///     License Keys
        /// </summary>
        public List<string> keys = new List<string>();

        /// <summary>
        ///     Key Expires
        /// </summary>
        public List<DateTime> expires = new List<DateTime>();

        public class Crypto
        {
            private static byte[] _salt = Encoding.ASCII.GetBytes("o6820135kbM8c7");

            /// <summary>
            /// Encrypt the given string using AES.  The string can be decrypted using 
            /// DecryptStringAES().  The sharedSecret parameters must match.
            /// </summary>
            /// <param name="plainText">The text to encrypt.</param>
            /// <param name="sharedSecret">A password used to generate a key for encryption.</param>
            public static string EncryptStringAES(string plainText, string sharedSecret)
            {
                if (string.IsNullOrEmpty(plainText))
                    throw new ArgumentNullException("plainText");
                if (string.IsNullOrEmpty(sharedSecret))
                    throw new ArgumentNullException("sharedSecret");

                string outStr = null;                       // Encrypted string to return
                RijndaelManaged aesAlg = null;              // RijndaelManaged object used to encrypt the data.

                try
                {
                    // generate the key from the shared secret and the salt
                    Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(sharedSecret, _salt);

                    // Create a RijndaelManaged object
                    aesAlg = new RijndaelManaged();
                    aesAlg.Key = key.GetBytes(aesAlg.KeySize / 8);

                    // Create a decrytor to perform the stream transform
                    ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                    // Create the streams used for encryption
                    using (MemoryStream msEncrypt = new MemoryStream())
                    {
                        // prepend the IV
                        msEncrypt.Write(BitConverter.GetBytes(aesAlg.IV.Length), 0, sizeof(int));
                        msEncrypt.Write(aesAlg.IV, 0, aesAlg.IV.Length);
                        using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {
                            using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                            {
                                //Write all data to the stream
                                swEncrypt.Write(plainText);
                            }
                        }
                        outStr = Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }
                finally
                {
                    // Clear the RijndaelManaged object.
                    if (aesAlg != null)
                        aesAlg.Clear();
                }

                // Return the encrypted bytes from the memory stream.
                return outStr;
            }

            /// <summary>
            /// Decrypt the given string.  Assumes the string was encrypted using 
            /// EncryptStringAES(), using an identical sharedSecret.
            /// </summary>
            /// <param name="cipherText">The text to decrypt.</param>
            /// <param name="sharedSecret">A password used to generate a key for decryption.</param>
            public static string DecryptStringAES(string cipherText, string sharedSecret)
            {
                if (string.IsNullOrEmpty(cipherText))
                    throw new ArgumentNullException("cipherText");
                if (string.IsNullOrEmpty(sharedSecret))
                    throw new ArgumentNullException("sharedSecret");

                // Declare the RijndaelManaged object
                // used to decrypt the data.
                RijndaelManaged aesAlg = null;

                // Declare the string used to hold
                // the decrypted text.
                string plaintext = null;

                try
                {
                    // generate the key from the shared secret and the salt
                    Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(sharedSecret, _salt);

                    // Create the streams used for decryption.                
                    byte[] bytes = Convert.FromBase64String(cipherText);
                    using (MemoryStream msDecrypt = new MemoryStream(bytes))
                    {
                        // Create a RijndaelManaged object
                        // with the specified key and IV.
                        aesAlg = new RijndaelManaged();
                        aesAlg.Key = key.GetBytes(aesAlg.KeySize / 8);
                        // Get the initialization vector from the encrypted stream
                        aesAlg.IV = ReadByteArray(msDecrypt);
                        // Create a decrytor to perform the stream transform.
                        ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader srDecrypt = new StreamReader(csDecrypt))

                                // Read the decrypted bytes from the decrypting stream
                                // and place them in a string.
                                plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }
                finally
                {
                    // Clear the RijndaelManaged object.
                    if (aesAlg != null)
                        aesAlg.Clear();
                }

                return plaintext;
            }

            private static byte[] ReadByteArray(Stream s)
            {
                byte[] rawLength = new byte[sizeof(int)];
                if (s.Read(rawLength, 0, rawLength.Length) != rawLength.Length)
                {
                    throw new SystemException("Stream did not contain properly formatted byte array");
                }

                byte[] buffer = new byte[BitConverter.ToInt32(rawLength, 0)];
                if (s.Read(buffer, 0, buffer.Length) != buffer.Length)
                {
                    throw new SystemException("Did not read byte array properly");
                }

                return buffer;
            }
        }
    }    


    //////////////////////////////////////

    /// <summary>
    ///     Direct Route Solving HTTP Server (not through NMS Auth)
    /// </summary>
    public class RouteHTTPListener
    {
        public const string _ServerTree = "-V4-win32";
        public const string _Server = "dkxce.Route.Server/21.12.21.23-V4-win32";
        private const string _ServerName = "dkxce.Route.ServiceSolver";
        private const string _ServerOwner = "milokz@gmail.com";
        public const string _ServerCustomHeaders = "Service-Mods: GARMIN, OSM2SHP, WATER\r\nService-Info: Debug\r\n";

        private Thread mainThread = null;
        private TcpListener mainListener = null;
        private IPAddress ListenIP = IPAddress.Any;
        private int ListenPort = 80;
        private bool isRunning = false;
        private int MaxThreads = 1;
        private int ThreadsCount = 0;

        private IDictionary<string, MethodInfo> METHODS = new Dictionary<string, MethodInfo>();

        private Licenses lics = null;        

        public RouteHTTPListener(int Port, int MaxThreads) 
        {
            InitMethods();
            this.ListenPort = Port; 
            this.MaxThreads = MaxThreads;

            lics = new Licenses();
            #if LICENSE
            {
                // default: `dkxce.Route.ServiceSolver.lic`
                string licFile = RouteTCPListenter.GetCurrentExe().Remove(RouteTCPListenter.GetCurrentExe().Length - 4) + ".lic";
                // Navicom Web HTTP RouteSolver.dll Key = D6AEA3EC848644258DCD06A781B2C036
                // Navicom Route Map Test Key = D6AEA3EC848644258DCD06A781B2C036
                // TO SAVE LICENSES USE `nmsRouteLicenseFileTool`
            
                // fix if system date changed wrong
                if (DateTime.UtcNow < System.IO.File.GetLastWriteTimeUtc(RouteTCPListenter.GetCurrentExe())) throw new Exception("License Error [A]");
                if (!File.Exists(licFile)) throw new Exception("License Error [B]");
                if (DateTime.UtcNow < System.IO.File.GetLastWriteTimeUtc(licFile)) throw new Exception("License Error [C]");

                System.IO.FileStream fs;
                try
                {
                    fs = new FileStream(licFile, FileMode.Open);
                }
                catch { throw new Exception("License Error [D0]"); };

                try
                {
                    byte[] buffer = new byte[65536];
                    ICSharpCode.SharpZipLib.GZip.GZipInputStream ins = new ICSharpCode.SharpZipLib.GZip.GZipInputStream(fs);
                    int length = ins.Read(buffer, 0, buffer.Length);
                    ins.Close();
                    fs.Close();
                    string coded = System.Text.Encoding.ASCII.GetString(buffer, 0, length);
                    string xml = Licenses.Crypto.DecryptStringAES(coded, "zSx1b0Izlk");
                    lics = XMLSaved<Licenses>.LoadText(xml);
                }
                catch { throw new Exception("License Error [D]"); };


                if (lics.keys.Count == 0) throw new Exception("License Error [E]");
                if (lics.keys.IndexOf("") >= 0) throw new Exception("License Error [F]");
            };            
            #endif
        }
        
        public bool Running { get { return isRunning; } }
        public IPAddress ServerIP { get { return ListenIP; } }
        public int ServerPort { get { return ListenPort; } }

        public void Dispose() { Stop(); }
        ~RouteHTTPListener() { Dispose(); }

        public virtual void Start()
        {
            if (isRunning) throw new Exception("Server Already Running!");

            isRunning = true;
            mainThread = new Thread(MainThread);
            mainThread.Start();
        }

        private void MainThread()
        {
            mainListener = new TcpListener(this.ListenIP, this.ListenPort);
            mainListener.Start();
            while (isRunning)
            {
                try
                {
                    TcpClient client = mainListener.AcceptTcpClient();
                    if (RouteThreader.IPBanList.Contains(((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString()))
                    {
                        client.Close();
                        continue;
                    };

                    if (this.MaxThreads > 1) // multithread or multiclient
                    {
                        while ((this.ThreadsCount >= this.MaxThreads) && isRunning) // wait for any closed thread
                            System.Threading.Thread.Sleep(5);
                        if (!isRunning) break; // break if stopped
                        Thread thr = new Thread(GetClient); // create new thread for new client
                        thr.Start(client);
                    }
                    else // single thread
                        GetClient(client);
                }
                catch { Thread.Sleep(1); };    
            };
        }

        public virtual void Stop()
        {
            if (!isRunning) return;

            isRunning = false;

            if (mainListener != null) mainListener.Stop();
            mainListener = null;

            mainThread.Join();
            mainThread = null;
        }

        public void GetClient(object data)
        {
            this.ThreadsCount++;
            TcpClient Client = (TcpClient)data;
            System.Text.Encoding defEncoding = System.Text.Encoding.GetEncoding(1251);

            string Request = "";
            bool utf8 = false;
            byte[] Buffer = new byte[4096];
            int Count = 0;
            bool CRLFok = false;
            int CRLFaf = 0;

            try
            {
                while ((Count = Client.GetStream().Read(Buffer, 0, Buffer.Length)) > 0)
                {
                    int copyTo = Count;
                    if (Count > 4)
                    {
                        for (int i = 3; i < Count; i++)
                            if ((Buffer[i - 3] == 13) && (Buffer[i - 2] == 10) && (Buffer[i - 1] == 13) && (Buffer[i] == 10))
                            {
                                copyTo = i + 1;
                                CRLFok = true;
                                i = int.MaxValue - 1;
                            };
                    };
                    if (CRLFok && (copyTo != Count)) CRLFaf = Count - copyTo;
                    Request += Encoding.GetEncoding(1251).GetString(Buffer, 0, copyTo);

                    int posEncoding = -1;
                    bool hasEncoding = (posEncoding = Request.IndexOf("charset=")) >= 0;
                    if (hasEncoding)
                    {
                        try
                        {
                            int till = Request.IndexOf("\r", posEncoding);
                            if (till < 0) till = Request.IndexOf("\n", posEncoding);
                            string txtEncoding = Request.Substring(posEncoding + 8, till - posEncoding - 8).Trim();
                            int codEnc = 0;
                            if (int.TryParse(txtEncoding, out codEnc))
                                defEncoding = System.Text.Encoding.GetEncoding(codEnc);
                            else
                                defEncoding = System.Text.Encoding.GetEncoding(txtEncoding);
                            if (defEncoding == null) defEncoding = System.Text.Encoding.UTF8;
                        }
                        catch { defEncoding = System.Text.Encoding.UTF8; };
                    };

                    if (CRLFok)
                        Request += Encoding.GetEncoding(1251).GetString(Buffer, copyTo, Count - copyTo);
                    else
                        Request += defEncoding.GetString(Buffer, copyTo, Count - copyTo);
                    if (Request.IndexOf("\r\n\r\n") >= 0 || Request.Length > 4096) { break; };
                };
            }
            catch { }; // READ REQUEST ERROR

            if (Count > 0) // PARSE REQUEST ERROR
                try { ReceiveData(Client, Request, defEncoding, CRLFaf); }
                catch (Exception ex)
                {
                    XMLSaved<int>.AddErr2SysLog("ClientThread Exception: " + ex.ToString());
                    try
                    {
                        HttpClientSendError(Client, 500);
                    }
                    catch (Exception ex2)
                    {
                        XMLSaved<int>.AddErr2SysLog("ClientThread Exception: " + ex2.ToString());
                    };
                };

            Client.Close();
            this.ThreadsCount--;
        }

        #region Private ReceiveData Methods       
        private void ReceiveData_HTTPDescription(Stream outTo)
        {            
            byte[] Buffer = Encoding.ASCII.GetBytes(
                         "HTTP/1.1 200 " + ((HttpStatusCode)200).ToString() + "\r\n" +
                         "Server: " + _Server + "\r\n" +
                         "Service-Name: " + _ServerName + "\r\n" +
                         "Service-Owner: " + _ServerOwner + "\r\n" +
                         _ServerCustomHeaders + 
                         "Connection: close\r\n" +
                         "Content-Type: text/html; charset=windows-1251\r\n" +
                         "\r\n"); // charset=utf-8  
            outTo.Write(Buffer, 0, Buffer.Length);

            FileStream fs = new FileStream(XMLSaved<int>.GetCurrentDir() + @"\dkxce.Route.ServiceSolver.Web.desc", FileMode.Open, FileAccess.Read);
            byte[] buff = new byte[fs.Length];
            fs.Read(buff, 0, buff.Length);
            fs.Close();

            outTo.Write(buff, 0, buff.Length);
        }

        private void ReceiveDate_GetFile(Stream outTo, string fileName, string contentType)
        {
            byte[] Buffer = Encoding.ASCII.GetBytes(
                         "HTTP/1.1 200 " + ((HttpStatusCode)200).ToString() + "\r\n" +
                         "Server: " + _Server + "\r\n" +
                         "Service-Name: " + _ServerName + "\r\n" +
                         "Service-Owner: " + _ServerOwner + "\r\n" +
                         _ServerCustomHeaders + 
                         "Connection: close\r\n" +
                         "Content-Type: "+contentType+"\r\n" +
                         "\r\n"); // charset=utf-8  
            outTo.Write(Buffer, 0, Buffer.Length);

            FileStream fs = new FileStream(XMLSaved<int>.GetCurrentDir() + @"\" + fileName, FileMode.Open, FileAccess.Read);
            byte[] buff = new byte[fs.Length];
            fs.Read(buff, 0, buff.Length);
            fs.Close();

            outTo.Write(buff, 0, buff.Length);
        }

        private Dictionary<string, string> path2out = new Dictionary<string, string>();
        private void StoreRegionNames(string l2w)
        {            
            if (String.IsNullOrEmpty(l2w)) return;

            lock (path2out)
            {
                string[] spl = l2w.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < spl.Length; i++)
                {
                    string rt = spl[i];
                    if (rt.IndexOf("-") > 0)
                        rt = rt.Substring(0, rt.IndexOf("-")).Trim();
                    if (!path2out.ContainsKey(rt))
                        path2out.Add(rt, null);
                };
            };
        }        

        private string GetRegionsAreAdditRegInfoText(string path, string reg)
        {
            string s = reg;            
            int di = s.IndexOf(".");
            if (di > 0)
                s = s.Substring(0, di);
            else
                while (s.Length < 3) s = "0" + s;
            
            string fAddit = path + @"\" + s + ".addit.xml";
            if (File.Exists(fAddit))
            {
                string name = reg;
                string styp = "";
                try
                {
                    dkxce.Route.Classes.AdditionalInformation ai = XMLSaved<dkxce.Route.Classes.AdditionalInformation>.Load(fAddit);
                    if (!String.IsNullOrEmpty(ai.RegionName))
                        name = ai.RegionName;
                    if (!String.IsNullOrEmpty(ai.SourceType))
                        styp = " (" + ai.SourceType + ")";
                    if (!String.IsNullOrEmpty(ai.TestMap))
                    {
                        return String.Format("<a href=\"/nms/web/example1.html#{0}\" target=\"_blank\">{1}</a>{2}", ai.TestMap, name, styp);
                    };
                }
                catch (Exception)
                { 
                };
            };
            return "";
        }

        private string GetRegionsAreText()
        {
            if (path2out.Count == 0) return "";
            if (RouteThreader.mem.config == null) return "";
           
            string res = "";
            lock (path2out)
            {
                Dictionary<string, string> tmPath = new Dictionary<string, string>();
                bool updateValues = false;
                foreach (KeyValuePair<string, string> kvp in path2out)
                {
                    string val = kvp.Value;
                    if (val == null)
                    {
                        val = GetRegionsAreAdditRegInfoText(RouteThreader.mem.GetGraphsPath(), kvp.Key);
                        updateValues = true;
                    };
                    tmPath.Add(kvp.Key, val);
                    if (String.IsNullOrEmpty(val)) continue;
                    res += String.Format("{0} - {1}<br/>\r\n", kvp.Key, val);
                };
                if(updateValues) path2out = tmPath;
            };            
            if (String.IsNullOrEmpty(res)) return ""; else return "<br/>\r\n" + res;
        }

        private void ReceiveData_ServiceStatus(Stream outTo)
        {
            string modln = "<table><tr><td>ObjectID</td><td>GlobalRegions</td><td>ThreadRegions</td><td>SessionRegions</td></tr>";
            if (RouteThreader.mem.ObjectsData != null)
            {
                string[] mod = RouteThreader.mem.ObjectsData.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < mod.Length; i++)
                {
                    modln += "<tr>";
                    string[] ln = mod[i].Split(new string[] { ":" }, StringSplitOptions.None);
                    for (int l = 0; l < ln.Length; l++)
                    {
                        if(l > 0) StoreRegionNames(ln[l]);
                        modln += "<td>" + ln[l] + "</td>";
                    };
                    modln += "</tr>";
                };
                modln += "</table>";
            };
            string RA = "";
            try { RA = GetRegionsAreText(); }
            catch (Exception) { };

            string dInfo = "Service Params: <select style=\"border:0px;font-size:14;color:maroon;font-weight:bold;\">";
            {
                try
                {
                    if (RouteThreader.mem.config != null)
                    {
                        dInfo += "<option>Autorization: " + RouteThreader.mem.config.authorization.ToString() + "</option>";

                        if ((RouteThreader.mem.config.Users != null) && (RouteThreader.mem.config.Users.Count > 0))
                            dInfo += "<option>Auth Users: " + RouteThreader.mem.config.Users.Count.ToString() + "</option>";

                        if ((RouteThreader.mem.config.IPBanList != null) && (RouteThreader.mem.config.IPBanList.Count > 0))
                            dInfo += "<option>Ban List: " + RouteThreader.mem.config.IPBanList.Count.ToString() + "</option>";

                        if (!String.IsNullOrEmpty(RouteThreader.mem.config.http_description))
                            dInfo += "<option>Description: " + RouteThreader.mem.config.http_description + "</option>";

                        if ((RouteThreader.mem.config.http_licenses != null) && (RouteThreader.mem.config.http_licenses.Count > 0))
                            dInfo += "<option>Licenses: " + RouteThreader.mem.config.http_licenses.Count.ToString() + "</option>"; 

                        string SvcHost = "localhost";
                        if (RouteThreader.mem.config.http_showhost || RouteThreader.mem.config.http_showip)
                        {
                            SvcHost = Dns.GetHostName();
                            if (RouteThreader.mem.config.http_showhost)
                                dInfo += "<option>Listen Host: " + SvcHost + "</option>";
                        };

                        if (RouteThreader.mem.config.http_showip)
                        {
                            IPHostEntry ipEntry = Dns.GetHostEntry(SvcHost);
                            IPAddress[] addr = ipEntry.AddressList;

                            for (int i = 0; i < addr.Length; i++)
                                dInfo += "<option>Listen IP: " + addr[i].ToString() + "</option>";
                        };

                        dInfo += "<option>Thread Log: " + RouteThreader.mem.config.threadLog.ToString() + "</option>";
                        dInfo += "<option>Thread Log Mem: " + RouteThreader.mem.config.threadLogMem.ToString() + "</option>";
                    };
                }
                catch { };
                dInfo += "</select>";
            };

            byte[] Buffer = Encoding.GetEncoding(1251).GetBytes(
                    "HTTP/1.1 200 " + ((HttpStatusCode)200).ToString() + "\r\n" +
                    "Server: " + _Server + "\r\n" +
                    "Service-Name: " + _ServerName + "\r\n" +
                    "Service-Owner: " + _ServerOwner + "\r\n" +
                    _ServerCustomHeaders + 
                    "Connection: close\r\n" +
                    "Content-type: text/html; charset=windows-1251\r\n\r\n" +

                    "Server: <b>" + _Server + "</b><br/>\r\n" +
                    "Service-Name: <b>" + _ServerName + "</b><br/>\r\n" +
                    dInfo +
                    "<hr style=\"border:solid 1px navy;\"/>" +

                    "<table><tr>" +
                    "<td>Protocol:<br/>Area:<br/>Mode:<br/>Threads Regions Cache Size:<br/>Threads Regions Cache:<br/></td><td>" +
                    RouteThreader.mem.Protocol + "<br/>" +
                    RouteThreader.mem.Area + "<br/>" +
                    RouteThreader.mem.Mode + "<br/>" +
                    (((double)(RouteThreader.mem.ThreadRegionsCacheSize)) / 1024 / 1024).ToString("0.00").Replace(",", ".") + "MB<br/>" +
                    RouteThreader.mem.ThreadRegionsCache + "<br/>" +

                    "</td><td>&nbsp;&nbsp;&nbsp;</td><td>Dynamic Pool:<br/>Max Solve Time:<br/>Max Wait Time:<br/>Global Regions Cache Size:<br/>Global Regions Cache:<br/></td><td>" +
                    RouteThreader.mem.DynamicPool.ToString() + "<br/>" +
                    RouteThreader.mem.MaxSolveTime.ToString() + "<br/>" +
                    RouteThreader.mem.MaxWaitTime.ToString() + "<br/>" +
                    (((double)RouteThreader.mem.GlobalRegionsCacheSize) / 1024 / 1024).ToString("0.00").Replace(",", ".") + "MB<br/>" +
                    RouteThreader.mem.GlobalRegionsCache + "<br/>" +

                    "</td><td>&nbsp;&nbsp;&nbsp;</td><td>Objects Used:<br/>Objects Idle:<br/>Threads Alive:<br/>Threads Counted:<br/>Threads Max Alive:</td><td>" +
                    RouteThreader.mem.ObjectsUsed.ToString() + "<br/>" +
                    RouteThreader.mem.ObjectsIdle.ToString() + "<br/>" +
                    RouteThreader.mem.ThreadsAlive.ToString() + "<br/>" +
                    RouteThreader.mem.ThreadsCounted.ToString() + "<br/>" +
                    RouteThreader.mem.ThreadsMaxAlive.ToString() +

                    "</td></tr></table>" + 

                    "Objects Data: <br/>" + modln +

                    (String.IsNullOrEmpty(RA) ? "" : "<hr style=\"border:solid 1px maroon;\"/>Regions are: " + RA) +

                    (((RouteThreader.mem.config != null) && (!String.IsNullOrEmpty(RouteThreader.mem.config.http_html))) ? "<hr style=\"border:solid 1px navy;\"/>" + RouteThreader.mem.config.http_html : "") +

                    "<hr style=\"border:solid 1px black;\"/>"+
                    "<span style=\"color:maroon;\">Started Time: " + RouteThreader.mem.startedAt.ToString("HH:mm:ss dd.MM.yyyy") + " UTC</span><br/>\r\n" +
                    "<span style=\"color:navy;\">Current Time: " + DateTime.UtcNow.ToString("HH:mm:ss dd.MM.yyyy") + " UTC</span><br/>\r\n" +
                    "<span style=\"color:green;\">Running Time: " + RunningToString(DateTime.UtcNow, RouteThreader.mem.startedAt) + " </span><br/>\r\n" +
                    "<hr style=\"border:solid 1px silver;\"/>" +
                    "<a href=\"/nms/\">Main page</a> | <a href=\"/nms/help\">Documentation</a>" +

                    "");
            outTo.Write(Buffer, 0, Buffer.Length);
        }               
       
        #endregion

        private string RunningToString(DateTime till, DateTime since)
        {
            TimeSpan ts = till.Subtract(since);
            return String.Format("{4:00}:{5:00}:{6:00} {3:00}.{1:00}:{0:0000} UTC - {2} weeks", (int)(ts.Days / 365.2425), (int)(ts.Days / 30.436875), (int)ts.TotalDays / 7, ts.Days, ts.Hours, ts.Minutes, ts.Seconds);
        }

        // BEGIN METHOD RECEIVE DATE
        public void ReceiveData(TcpClient Client, string Request, System.Text.Encoding defEncoding, int bodyBytesCopied)
        {
            // PARSE REQUEST
            byte methodIS = 3; // 3-Get, 4-Post, 7-OPTIONS
            int x1 = Request.IndexOf("GET");
            int x2 = Request.IndexOf("HTTP");
            if ((x1 < 0) || (x1 > x2)) { x1 = Request.IndexOf("POST"); if (x1 >= 0) methodIS = 4; };
            if ((x1 < 0) || (x1 > x2)) { x1 = Request.IndexOf("OPTIONS"); if (x1 >= 0) methodIS = 7; };
            string query = "";
            if ((x1 >= 0) && (x2 >= 0) && (x2 > x1)) query = Request.Substring(x1 + methodIS, x2 - methodIS).Trim();

            // ONLY ALLOWED PATH
            string[] qpc = query.Split(new char[] { '?' }, 2);
            if ((qpc.Length == 0) || (qpc[0].ToLower().IndexOf("/nms") < 0))
            {
                HttpClientSendError(Client, 501);
                return;
            };

            qpc[0] = qpc[0].Remove(0, 4);
            if ((qpc[0].Length > 0) && (qpc[0][0] == '/')) qpc[0] = qpc[0].Remove(0, 1);
            if ((qpc[0].Length > 0) && (qpc[0][0] == '\\')) qpc[0] = qpc[0].Remove(0, 1);

            string authorize = "";
            string authUser = "";
            string authPass = "";
            string xFrw4 = "";

            IDictionary<string, string> Cookies = new Dictionary<string, string>();
            IDictionary<string, string> Headers = new Dictionary<string, string>();
            {
                int iofbr = Request.IndexOf("\r\n"); int iofprev = 0; string cline = "";
                while ((iofbr = Request.IndexOf("\r\n", iofprev = iofbr + 2)) > 0)
                {
                    cline = Request.Substring(iofprev, iofbr - iofprev);
                    if (!string.IsNullOrEmpty(cline))
                    {
                        string[] kv = cline.Split(new char[] { ':' }, 2);
                        Headers[kv[0]] = kv[1].Trim();
                        if (kv[0] == "X-Forwarded-For")
                        {
                            try
                            {
                                xFrw4 = kv[1].Trim().Split(new char[] { ':' }, 2)[0];
                            }
                            catch { };
                        };
                        if (kv[0] == "Authorization")
                        {
                            try
                            {
                                string cup = kv[1].Trim();
                                if (cup.IndexOf("Basic") == 0)
                                    authorize = Encoding.UTF8.GetString(Convert.FromBase64String(cup.Substring(6)));
                            }
                            catch { };
                        };
                        if ((kv[0] == "Cookie") && (kv.Length > 1) && (!string.IsNullOrEmpty(kv[1])))
                        {
                            string[] cks = kv[1].Trim().Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string ck in cks)
                            {
                                string[] c = ck.Trim().Split(new char[] { '=' }, 2);
                                if (c.Length == 2)
                                    Cookies[c[0].Trim()] = c[1].Trim();
                            };
                        };
                    };
                };
            };

            if (RouteThreader.authorization)
            {
                if (string.IsNullOrEmpty(authorize))
                {
                    HttpClientSendError(Client, 401);
                    return;
                }
                else
                {
                    string[] up = authorize.Split(new char[] { ':' }, 2);
                    if ((up == null) || (up.Length < 2))
                    {
                        HttpClientSendError(Client, 401);
                        return;
                    };
                    authUser = up[0];
                    authPass = up[1];
                    bool validu = false;
                    if ((RouteThreader.Users != null) && (RouteThreader.Users.Count > 0))
                        foreach (UserPass uspa in RouteThreader.Users)
                            if ((uspa.user == authUser) && (uspa.pass == authPass))
                            {
                                validu = true;
                                break;
                            };
                    if (!validu) // check valid user
                    {
                        HttpClientSendError(Client, 401);
                        return;
                    };
                };
            };

            string Body = "";
            int BL = bodyBytesCopied;
            {
                string cl = "Content-Length:";
                int bytesCount = 0;
                if (Request.IndexOf(cl) > 0)
                {
                    int iofcl = Request.IndexOf(cl);
                    cl = Request.Substring(iofcl + cl.Length, Request.IndexOf("\r", iofcl + cl.Length) - iofcl - cl.Length);
                    int.TryParse(cl.Trim(), out bytesCount);
                };
                if (bytesCount > 0)
                {
                    int iofbrbr = Request.IndexOf("\r\n\r\n");
                    if (iofbrbr > 0)
                        Body = Request.Substring(iofbrbr + 4);
                    if (iofbrbr < 0)
                    {
                        iofbrbr = Request.IndexOf("\n\n");
                        if (iofbrbr > 0)
                            Body = Request.Substring(iofbrbr + 2);
                    };

                    if (BL < bytesCount)
                    {
                        Stream stream = Client.GetStream();
                        byte[] Buffer = new byte[3145728];
                        int Count = 0;
                        try
                        {
                            while ((Count = stream.Read(Buffer, 0, ((bytesCount - BL) > Buffer.Length) ? Buffer.Length : bytesCount - BL)) > 0)
                            {
                                Body += defEncoding.GetString(Buffer, 0, Count);
                                BL += Count;
                                if (BL == bytesCount) break;
                            };
                        }
                        catch (Exception) { Body = ""; };
                    };
                };
            };

            ClientRequest clientRequest = new ClientRequest(Client, Headers, Cookies, Request, qpc[0].ToLower(), (qpc.Length > 1 ? qpc[1] : ""), Body);
            if (string.IsNullOrEmpty(clientRequest.method)) clientRequest.method = "null";

            // TEST
            {
                if (query.StartsWith("/nms/web"))
                {
                    PassFileToClientByRequest(clientRequest, query, XMLSaved<int>.GetCurrentDir() + @"\WEB\", "/nms/web/");
                    //HttpClientSendError(Client, 200, query);
                    return;
                };
            };

            // API,  call methods
            if (METHODS.Keys.Contains(clientRequest.method))
                if (METHODS[clientRequest.method].implementation(clientRequest))
                {
                    Client.Close();
                    return;
                }; 

            // method is not supported
            HttpClientSendError(Client, 406, "HTTP or API METHOD NOT SUPPORTED (Only GET/POST allowed)");
            return;
        }
        // END METHOD RECEIVE DATE        

        protected virtual void PassFileToClientByRequest(ClientRequest Request, string query, string HomeDirectory, string subPath)
        {
            string path = query;
            if (!String.IsNullOrEmpty(subPath))
            {
                int i = path.IndexOf(subPath);
                if (i >= 0) path = path.Remove(i, subPath.Length);
            };
            path = path.Replace("/", @"\");
            if (path.IndexOf("/./") >= 0) { HttpClientSendError(Request.client, 400); return; };
            if (path.IndexOf("/../") >= 0) { HttpClientSendError(Request.client, 400); return; };
            if (path.IndexOf("/.../") >= 0) { HttpClientSendError(Request.client, 400); return; };
            path = HomeDirectory + @"\" + path;
            while (path.IndexOf(@"\\") > 0) path = path.Replace(@"\\", @"\");
            string fName = System.IO.Path.GetFileName(path);
            string dName = System.IO.Path.GetDirectoryName(path);
            if ((String.IsNullOrEmpty(dName)) && (String.IsNullOrEmpty(fName)) && (path.EndsWith(@":\")) && (Path.IsPathRooted(path))) dName = path;
            if (!String.IsNullOrEmpty(fName))
            {
                if (!File.Exists(path))
                {
                    HttpClientSendError(Request.client, 404);
                    return;
                }
                else
                {
                    HttpClientSendFile(Request.client, path, null, 200, null);
                };
            }
            else if (!String.IsNullOrEmpty(dName))
            {
                if (!Directory.Exists(path))
                {
                    HttpClientSendError(Request.client, 404);
                    return;
                }
                else
                {
                    // load default file
                    {
                        List<string> files = new List<string>(Directory.GetFiles(path, "index.*", SearchOption.TopDirectoryOnly));
                        foreach (string file in files)
                        {
                            string fExt = Path.GetExtension(file);
                            if (fExt == ".html") { HttpClientSendFile(Request.client, file, null, 200, null); return; };
                            if (fExt == ".dhtml") { HttpClientSendFile(Request.client, file, null, 200, null); return; };
                            if (fExt == ".htmlx") { HttpClientSendFile(Request.client, file, null, 200, null); return; };
                            if (fExt == ".xhtml") { HttpClientSendFile(Request.client, file, null, 200, null); return; };
                            if (fExt == ".txt") { HttpClientSendFile(Request.client, file, null, 200, null); return; };
                        };
                    };
                    {
                        string html = "<html><body>";
                        if (true) // AllowGetDirs
                        {
                            html += String.Format("<a href=\"{0}/\"><b> {0} </b></a><br/>\n\r", "..");
                            string[] dirs = Directory.GetDirectories(path);
                            if (dirs != null) Array.Sort<string>(dirs);
                            foreach (string dir in dirs)
                            {
                                DirectoryInfo di = new DirectoryInfo(dir);
                                if ((di.Attributes & FileAttributes.Hidden) > 0) continue;
                                string sPath = dir.Substring(dir.LastIndexOf(@"\") + 1);
                                html += String.Format("<a href=\"{1}/\"><b>{0}</b></a><br/>\n\r", sPath, UrlEscape(sPath));
                            };
                        };
                        {
                            string[] files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);
                            if (files != null) Array.Sort<string>(files);
                            foreach (string file in files)
                            {
                                FileInfo fi = new FileInfo(file);
                                string sPath = Path.GetFileName(file);
                                html += String.Format("<a href=\"{1}\">{0}</a> - <span style=\"color:gray;\">{2}</span>, <span style=\"color:silver;\">MDF: {3}</span><br/>\n\r", sPath, UrlEscape(sPath), ToFileSize(fi.Length), fi.LastWriteTime);
                            };
                        };
                        html += "</body></html>";
                        HttpClientSendError(Request.client, 200, html);
                        return;
                    };
                };
            };
            HttpClientSendError(Request.client, 400);
        }

        protected virtual void HttpClientSendFile(TcpClient Client, string fileName, Dictionary<string, string> dopHeaders, int ResponseCode, string ContentType)
        {
            FileInfo fi = new FileInfo(fileName);

            string header = "HTTP/1.1 " + ResponseCode.ToString() + "\r\n";

            // Main Headers
            // Dop Headers
            if (dopHeaders != null)
                foreach (KeyValuePair<string, string> kvp in dopHeaders)
                    header += String.Format("{0}: {1}\r\n", kvp.Key, kvp.Value);

            ContentType = GetMemeType(fi.Extension.ToLower());
            header += "Content-type: " + ContentType + "\r\n";
            header += "Content-Length: " + fi.Length.ToString() + "\r\n";
            header += "\r\n";

            List<byte> response = new List<byte>();
            response.AddRange(Encoding.GetEncoding(1251).GetBytes(header));
            Client.GetStream().Write(response.ToArray(), 0, response.Count);

            // copy
            byte[] buff = new byte[65536];
            int bRead = 0;
            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            while (fs.Position < fs.Length)
            {
                bRead = fs.Read(buff, 0, buff.Length);
                Client.GetStream().Write(buff, 0, bRead);
            };
            fs.Close();
            //

            Client.GetStream().Flush();
            //Client.Client.Close();
            //Client.Close();
        }

        public static string UrlEscape(string str)
        {
            return System.Uri.EscapeDataString(str.Replace("+", "%2B"));
        }

        public static string UrlUnescape(string str)
        {
            return System.Uri.UnescapeDataString(str).Replace("%2B", "+");
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
                };
            };
            return ThreeNonZeroDigits(value / Math.Pow(1024, suffixes.Length - 1)) + " " + suffixes[suffixes.Length - 1];
        }

        private static string ThreeNonZeroDigits(double value)
        {
            if (value >= 100)
            {
                // No digits after the decimal.
                return value.ToString("0,0");
            }
            else if (value >= 10)
            {
                // One digit after the decimal.
                return value.ToString("0.0");
            }
            else
            {
                // Two digits after the decimal.
                return value.ToString("0.00");
            }
        }

        public static string GetMemeType(string fileExt)
        {
            // https://snipp.ru/handbk/mime-list
            switch (fileExt)
            {
                case ".pdf": return "application/pdf";
                case ".djvu": return "image/vnd.djvu";
                case ".zip": return "application/zip";
                case ".doc": return "application/msword";
                case ".docx": return "application/msword";
                case ".mp3": return "audio/mpeg";
                case ".m3u": return "audio/x-mpegurl";
                case ".wav": return "audio/x-wav";
                case ".gif": return "image/gif";
                case ".bmp": return "image/bmp";
                case ".psd": return "image/vnd.adobe.photoshop";
                case ".jpg": return "image/jpeg";
                case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                case ".svg": return "image/svg";
                case ".tiff": return "image/tiff";
                case ".css": return "text/css";
                case ".csv": return "text/csv";
                case ".html": return "text/html";
                case ".htmlx": return "text/html";
                case ".dhtml": return "text/html";
                case ".xhtml": return "text/html";
                case ".js": return "application/javascript";
                case ".json": return "application/json";
                case ".txt": return "text/plain";
                case ".md": return "text/plain";
                case ".php": return "text/php";
                case ".xml": return "text/xml";
                case ".mpg": return "video/mpeg";
                case ".mpeg": return "video/mpeg";
                case ".mp4": return "video/mp4";
                case ".ogg": return "video/ogg";
                case ".avi": return "video/x-msvideo";
                case ".rar": return "application/x-rar-compresse";
                default: return "application/octet-stream";
            };
        }

        #region HttpClientSend
        public virtual void HttpClientSendError(TcpClient Client, int Code)
        {
            HttpClientSendError(Client, Code, "");
        }
        public virtual void HttpClientSendError(TcpClient Client, int Code, string data)
        {
            // Получаем строку вида "200 OK"
            // HttpStatusCode хранит в себе все статус-коды HTTP/1.1
            string CodeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
            // Код простой HTML-странички
            string Html = "<html><body><h1>" + CodeStr + "</h1>" + data + "</body></html>";
            // Необходимые заголовки: ответ сервера, тип и длина содержимого. После двух пустых строк - само содержимое
            string Str = 
                "HTTP/1.1 " + CodeStr + "\r\n"+
                "Server: " + _Server + "\r\n" +
                "Service-Name: " + _ServerName + "\r\n" +
                "Service-Owner: " + _ServerOwner + "\r\n" +
                _ServerCustomHeaders + 
            #if LICENSE
                "Service-RLK: enabled\r\n"+
            #else
                "Service-RLK: disabled\r\n" +
            #endif
                (Code == 401 ? "WWW-Authenticate: Basic realm=\"Need authorizate\"\r\n" : "") +
                "Connection: close\r\n" +
                "Content-type: text/html\r\n"+
                "Content-Length:" + Html.Length.ToString() + "\r\n\r\n" + Html;
            // Приведем строку к виду массива байт
            byte[] Buffer = Encoding.ASCII.GetBytes(Str);
            // Отправим его клиенту
            Client.GetStream().Write(Buffer, 0, Buffer.Length);
            // Закроем соединение
            Client.Close();
        }

        public virtual void HttpClientSendData(ClientRequest Client, int Code, byte[] data, string headers, string contentType)
        {
            byte[] Buffer = Encoding.ASCII.GetBytes(
                         GetStandardHTTPResponseHeader(Client, Code) +
                         (string.IsNullOrEmpty(headers) ? "" : headers + (headers.EndsWith("\n") ? "" : "\r\n")) +
                         "Content-Type: " + contentType + "\r\n" +
                         "Content-Length:" + data.Length.ToString() + "\r\n" +
                         "\r\n");
            Client.client.GetStream().Write(Buffer, 0, Buffer.Length);
            Client.client.GetStream().Write(data, 0, data.Length);
        }

        public virtual void HttpClientSendData(ClientRequest clientRequest, int Code)
        {
            HttpClientSendData(clientRequest, Code, "");
        }
        public virtual void HttpClientSendData(ClientRequest clientRequest, int Code, string data)
        {
            HttpClientSendData(clientRequest, Code, data, "");
        }
        public virtual void HttpClientSendData(ClientRequest clientRequest, int Code, string data, string headers)
        {
            HttpClientSendData(clientRequest, Code, data, headers, "text/html");
        }
        public virtual void HttpClientSendData(ClientRequest clientRequest, int Code, string data, string headers, string ContentType)
        {
            string codeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
            if ((Code != 200) && (string.IsNullOrEmpty(data))) data = "<html><body><h1>" + codeStr + "</h1></body></html>";

            byte[] DataBuffer = clientRequest.Encoding.GetBytes(data);
            string charset = clientRequest.Encoding.WebName;//"utf-8";

            string Str =
                GetStandardHTTPResponseHeader(clientRequest, Code) +
                (string.IsNullOrEmpty(headers) ? "" : headers + (headers.EndsWith("\n") ? "" : "\r\n")) +
                "Content-Type: " + ContentType + "; charset=" + charset + "\r\n" +
                "Content-Length:" + DataBuffer.Length.ToString() + "\r\n\r\n";

            byte[] Buffer = Encoding.UTF8.GetBytes(Str);
            clientRequest.client.GetStream().Write(Buffer, 0, Buffer.Length);
            clientRequest.client.GetStream().Write(DataBuffer, 0, DataBuffer.Length);
            clientRequest.client.Close();
        }

        public virtual void HttpClientSendData<T>(ClientRequest clientRequest, int Code, string data)
        {
            HttpClientSendData<T>(clientRequest, Code, data);
        }
        public virtual void HttpClientSendData<T>(ClientRequest clientRequest, int Code, string data, string headers)
        {
            HttpClientSendData<T>(clientRequest, Code, data, headers, "text/html");
        }
        public virtual void HttpClientSendData<T>(ClientRequest clientRequest, int Code, string data, string headers, string ContentType)
        {
            HttpClientSendData<T>(clientRequest, Code, data, headers, ContentType, "");
        }
        public virtual void HttpClientSendData<T>(ClientRequest clientRequest, int Code, string data, string headers, string ContentType, string cookies)
        {
            HttpClientSendData(clientRequest, Code, data, headers, ContentType, cookies);
        }

        public virtual void HttpClientSendData<T>(ClientRequest clientRequest, int Code, T data)
        {
            HttpClientSendData<T>(clientRequest, Code, data);
        }
        public virtual void HttpClientSendData<T>(ClientRequest clientRequest, int Code, T data, string headers)
        {
            HttpClientSendData<T>(clientRequest, Code, data, headers, (clientRequest.IsXML || clientRequest.IsXMLRPC || clientRequest.IsSOAP) ? "text/xml" : "text/html");
        }
        public virtual void HttpClientSendData<T>(ClientRequest clientRequest, int Code, T data, string headers, string ContentType)
        {
            HttpClientSendData<T>(clientRequest, Code, data, headers, ContentType, "");
        }
        public virtual void HttpClientSendData<T>(ClientRequest clientRequest, int Code, T data, string headers, string ContentType, string cookies)
        {
            if (string.IsNullOrEmpty(cookies)) cookies = "";

            string outdata = "";
            string codeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
            if (data == null)
                outdata = "<html><body><h1>" + codeStr + "</h1></body></html>";
            else
            {
                if (clientRequest.IsXMLRPC)
                    outdata = XMLRPCObject<T>.Save(data);
                else if (clientRequest.IsXML)
                    outdata = XMLObject<T>.Save(data);
                else if (clientRequest.IsJSON)
                    outdata = JSONObject<T>.Save(data);
                else if (clientRequest.IsSOAP11)
                    outdata = XMLSOAP<T>.Save11(data, clientRequest.methodSOAP);
                else if (clientRequest.IsSOAP12)
                    outdata = XMLSOAP<T>.Save12(data, clientRequest.methodSOAP);
                else
                    outdata = "<html><body><h1>" + data.ToString() + "</h1></body></html>";
            };

            byte[] DataBuffer = clientRequest.Encoding.GetBytes(outdata);
            string charset = clientRequest.Encoding.WebName;//"utf-8";

            string Str =
                GetStandardHTTPResponseHeader(clientRequest, Code) +
                (string.IsNullOrEmpty(headers) ? "" : headers + (headers.EndsWith("\n") ? "" : "\r\n")) +
                "Content-Type: " + ContentType + "; charset=" + charset + "\r\n" +
                "Content-Length:" + DataBuffer.Length.ToString() + "\r\n\r\n";

            byte[] Buffer = Encoding.UTF8.GetBytes(Str);
            clientRequest.client.GetStream().Write(Buffer, 0, Buffer.Length);
            clientRequest.client.GetStream().Write(DataBuffer, 0, DataBuffer.Length);
            clientRequest.client.Close();
        }  

        private string GetStandardHTTPResponseHeader(ClientRequest Client, int Code)
        {
            // Code = 401
            // Получаем строку вида "200 OK"
            // HttpStatusCode хранит в себе все статус-коды HTTP/1.1
            string CodeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
            string Str =
               "HTTP/1.1 " + CodeStr + "\r\n" +
               "Server: " + _Server + "\r\n" +
                "Service-Name: " + _ServerName + "\r\n" +
                "Service-Owner: " + _ServerOwner + "\r\n" +
                _ServerCustomHeaders + 
            #if LICENSE
                "Service-RLK: enabled\r\n"+
            #else
                "Service-RLK: disabled\r\n" +
            #endif            
               (Code == 401 ? "WWW-Authenticate: Basic realm=\"Need authorizate\"\r\n" : "") +
               "Connection: close\r\n";
            return Str;
        }
        #endregion

        #region Standard Methods
        // soap wsdl
        private bool method_xmlwsdl(ClientRequest clientRequest)
        {
            string wsdl = WSDL.GenerateWSDL(typeof(SOAPWSDL), "http://127.0.0.1:" + this.ListenPort + "/nms/xmlsoap", false);
            HttpClientSendData(clientRequest, 200, wsdl, "Method-Name: wsdl\r\n", "text/xml");
            return true;
        }

        // soap forwarder page
        private bool method_xmlsoap(ClientRequest clientRequest)
        {
            if (string.IsNullOrEmpty(clientRequest.body))
            {
                string res =
                "<html><title>NMS ROUTE API</title><body>" +
                "<div style=\"font-size:11px;color:gray;\">This page only forwards SOAP data with specifed method to JSON original method page. Original JSON method page detects if input data in JSON, XML, XML-RPC, SOAP format and automatically convert it to source object.</div>" +
                "&nbsp;<br/>" +
                "<div style=\"font-size:11px;color:gray;\">Эта страница только пересылает SOAP запрос с указанным методом на страницу оригинального метода JSON. Оригинальная страница метода JSON обнаруживает входные данные в формате JSON, XML, XML-RPC, SOAP и автоматически преобразует их в исходный объект.</div>" +
                "&nbsp;<br/>" +
                "<a href=\"/nms/\">main page</a>" +
                "</body></html>";
                HttpClientSendData(clientRequest, 200, res);
                return true;
            };

            XMLSOAP<int> xro = XMLSOAP<int>.ParseMethod(clientRequest.body);
            if (xro.IsValid)
            {
                ClientRequest cr = new ClientRequest(clientRequest.client, clientRequest.headers, clientRequest.cookies, clientRequest.request, xro.method, clientRequest.query, clientRequest.body);
                if (METHODS.Keys.Contains(cr.method))
                    if (METHODS[cr.method].implementation(cr))
                        return true;

                xro.error = "method `" + xro.method + "` not found";
                xro.errorCode = 3005; // XMLRPC METHOD
            };

            HttpClientSendData(clientRequest, 200, xro.errorResponse, null, "text/xml");
            return true;
        }

        // xmlrpc forwarder page
        private bool method_xmlrpc(ClientRequest clientRequest)
        {
            if (string.IsNullOrEmpty(clientRequest.body))
            {
                string res =
                "<html><title>NMS ROUTE API</title><body>" +
                "<div style=\"font-size:11px;color:gray;\">This page only forwards XML-RPC data with specifed method to JSON original method page. Original JSON method page detects if input data in JSON, XML, XML-RPC or SOAP format and automatically convert it to source object.</div>" +
                "&nbsp;<br/>" +
                "<div style=\"font-size:11px;color:gray;\">Эта страница только пересылает XML-RPC запрос с указанным методом на страницу оригинального метода JSON. Оригинальная страница метода JSON обнаруживает входные данные в формате JSON, XML, XML-RPC, SOAP и автоматически преобразует их в исходный объект.</div>" +
                "&nbsp;<br/>" +
                "<a href=\"/nms/\">main page</a>" +
                "</body></html>";
                HttpClientSendData(clientRequest, 200, res);
                return true;
            };

            //
            // "<?xml version=\"1.0\"?><methodCall><methodName>test</methodName><params><param><struct><member><name>objA</name><value><i4>1111</i4></value></member><member><name>objB</name><value><array><data><value><i4>0</i4></value><value><i4>1</i4></value><value><i4>2</i4></value><value><i4>3</i4></value></data></array></value></member></struct></param></params></methodCall>"
            //
            XMLRPCObject<int> xro = XMLRPCObject<int>.ParseMethod(clientRequest.body);
            if (xro.IsValid)
            {
                ClientRequest cr = new ClientRequest(clientRequest.client, clientRequest.headers, clientRequest.cookies, clientRequest.request, xro.method, clientRequest.query, clientRequest.body);
                if (METHODS.Keys.Contains(cr.method))
                    if (METHODS[cr.method].implementation(cr))
                        return true;

                xro.error = "method `" + xro.method + "` not found";
                xro.errorCode = 3005; // XMLRPC METHOD
            };

            HttpClientSendData(clientRequest, 200, xro.errorResponse, null, "text/xml");
            return true;
        }

        private static string ObjTypeToString(Type t, string typeTo, string prefix, out string attributes)
        {
            string res = "";
            attributes = "";
            if (typeTo == "json") res += "{";
            if (typeTo == "xml") res += "";
            if (typeTo == "rpc") res += "";
            string maint = "";
            object[] maina = t.GetCustomAttributes(false);
            if (maina != null)
                foreach (object ma in maina)
                    if (ma is XmlRootAttribute)
                    {
                        XmlRootAttribute xra = (XmlRootAttribute)ma;
                        maint = "[XmlRoot(\"" + xra.ElementName + "\")]\r\n"; ;
                    };
            if (typeTo == "c#") res += maint + prefix + "&nbsp;&nbsp;&nbsp;" + "public class " + t.Name + " {";
            System.Reflection.MemberInfo[] m = t.GetMembers();
            foreach (System.Reflection.MemberInfo mi in m)
                if (mi.MemberType == System.Reflection.MemberTypes.Field)
                {
                    System.Reflection.FieldInfo fi = t.GetField(mi.Name);
                    object[] attrs = fi.GetCustomAttributes(false);
                    Type tp = fi.FieldType;
                    if (!tp.FullName.StartsWith("System"))
                    {
                        if (tp.BaseType.Name == "Array") // Array
                        {
                            Type et = tp.GetElementType();
                            string ca;
                            string rrr = ObjTypeToString(et, typeTo, prefix + "&nbsp;&nbsp;&nbsp;", out ca);
                            if (typeTo == "json") res += String.Format("\r\n{2}'{0}': \r\n{2}[{1}],", mi.Name, rrr, prefix + "&nbsp;&nbsp;&nbsp;");
                            if (typeTo == "c#")
                            {
                                string arr = "";
                                if (attrs != null)
                                    foreach (object attr in attrs)
                                    {
                                        if (attr is XmlElementAttribute)
                                        {
                                            XmlElementAttribute a = (XmlElementAttribute)attr;
                                            arr += String.Format("{0}[XmlElement(\"{1}\")]\r\n", prefix + "&nbsp;&nbsp;&nbsp;", a.ElementName);
                                        };
                                        if (attr is XmlArrayAttribute)
                                        {
                                            XmlArrayAttribute a = (XmlArrayAttribute)attr;
                                            arr += String.Format("{0}[XmlArray(\"{1}\")]\r\n", prefix + "&nbsp;&nbsp;&nbsp;", a.ElementName);
                                        };
                                        if (attr is XmlArrayItemAttribute)
                                        {
                                            XmlArrayItemAttribute a = (XmlArrayItemAttribute)attr;
                                            arr += String.Format("{0}[XmlArrayItem(\"{1}\")]\r\n", prefix + "&nbsp;&nbsp;&nbsp;", a.ElementName);
                                        };
                                        if (attr is XmlAttributeAttribute)
                                        {
                                            XmlAttributeAttribute a = (XmlAttributeAttribute)attr;
                                            arr += String.Format("{0}[XmlAttribute(\"{1}\")]\r\n", prefix + "&nbsp;&nbsp;&nbsp;", a.AttributeName);
                                        };
                                        if (attr is XmlTextAttribute)
                                        {
                                            arr += String.Format("{0}[XmlText]\r\n", prefix + "&nbsp;&nbsp;&nbsp;");
                                        };
                                    };
                                res += String.Format("\r\n\r\n{2}{1}\r\n\r\n" + arr + "{2}public {3} {0};", mi.Name, rrr, prefix + "&nbsp;&nbsp;&nbsp;", tp.Name);
                            };
                            if (typeTo == "xml")
                            {
                                bool els = false;
                                string elN = mi.Name;
                                string arN = rrr;
                                if (attrs != null)
                                    foreach (object attr in attrs)
                                    {
                                        if (attr is XmlElementAttribute)
                                        {
                                            XmlElementAttribute a = (XmlElementAttribute)attr;
                                            elN = a.ElementName;
                                        };
                                        if (attr is XmlArrayAttribute)
                                        {
                                            XmlArrayAttribute a = (XmlArrayAttribute)attr;
                                            elN = a.ElementName;
                                        };
                                        if (attr is XmlArrayItemAttribute)
                                        {
                                            els = true;
                                            XmlArrayItemAttribute a = (XmlArrayItemAttribute)attr;
                                            arN = "<" + a.ElementName + ca + ">" + rrr + "</" + a.ElementName + ">";
                                        };
                                    };
                                res += String.Format("\r\n{2}<{0}{3}>{1}\r\n{2}</{0}>", elN, arN, prefix + "&nbsp;&nbsp;&nbsp;", els ? "" : ca);
                            };
                            if (typeTo == "rpc")
                                res += String.Format("\r\n{2}<member><name>{0}</name>\r\n{2}<value><array><data><value><struct>\r\n{2}{1}\r\n{2}</struct></value></data></array></value></member>", mi.Name, rrr, prefix + "&nbsp;&nbsp;&nbsp;");
                        }
                        else
                        {
                            string ca;
                            string rrr = ObjTypeToString(tp, typeTo, prefix + "&nbsp;&nbsp;&nbsp;", out ca);
                            if (typeTo == "json") res += String.Format("\r\n{2}'{0}': \r\n{2}{1},", mi.Name, rrr, prefix + "&nbsp;&nbsp;&nbsp;");
                            if (typeTo == "c#")
                            {
                                string arr = "";
                                if (attrs != null)
                                    foreach (object attr in attrs)
                                    {
                                        if (attr is XmlElementAttribute)
                                        {
                                            XmlElementAttribute a = (XmlElementAttribute)attr;
                                            arr += String.Format("{0}[XmlElement(\"{1}\")]\r\n", prefix + "&nbsp;&nbsp;&nbsp;", a.ElementName);
                                        };
                                        if (attr is XmlArrayAttribute)
                                        {
                                            XmlArrayAttribute a = (XmlArrayAttribute)attr;
                                            arr += String.Format("{0}[XmlArray(\"{1}\")]\r\n", prefix + "&nbsp;&nbsp;&nbsp;", a.ElementName);
                                        };
                                        if (attr is XmlArrayItemAttribute)
                                        {
                                            XmlArrayItemAttribute a = (XmlArrayItemAttribute)attr;
                                            arr += String.Format("{0}[XmlArrayItem(\"{1}\")]\r\n", prefix + "&nbsp;&nbsp;&nbsp;", a.ElementName);
                                        };
                                        if (attr is XmlAttributeAttribute)
                                        {
                                            XmlAttributeAttribute a = (XmlAttributeAttribute)attr;
                                            arr += String.Format("{0}[XmlAttribute(\"{1}\")]\r\n", prefix + "&nbsp;&nbsp;&nbsp;", a.AttributeName);
                                        };
                                        if (attr is XmlTextAttribute)
                                        {
                                            arr += String.Format("{0}[XmlText]\r\n", prefix + "&nbsp;&nbsp;&nbsp;");
                                        };
                                    };
                                res += String.Format("\r\n\r\n{2}{1}\r\n\r\n" + arr + "{2}public {3} {0};", mi.Name, rrr, prefix + "&nbsp;&nbsp;&nbsp;", tp.Name);
                            };
                            if (typeTo == "xml") res += String.Format("\r\n{2}<{0}{3}>{1}\r\n{2}</{0}>", mi.Name, rrr, prefix + "&nbsp;&nbsp;&nbsp;", ca);
                            if (typeTo == "rpc")
                                res += String.Format("\r\n{2}<member><name>{0}</name>\r\n{2}<value><struct>{1}</struct></value></member>", mi.Name, rrr, prefix + "&nbsp;&nbsp;&nbsp;");
                        };
                    }
                    else
                    {
                        if (tp.BaseType.Name == "Array") // Array
                        {
                            Type et = tp.GetElementType();
                            if (typeTo == "json") res += String.Format("\r\n{2}'{0}': ['{1}'],", mi.Name, et.Name, prefix + "&nbsp;&nbsp;&nbsp;");
                            if (typeTo == "xml")
                            {
                                string elN = mi.Name;
                                string arN = et.Name;
                                if (attrs != null)
                                    foreach (object attr in attrs)
                                    {
                                        if (attr is XmlElementAttribute)
                                        {
                                            XmlElementAttribute a = (XmlElementAttribute)attr;
                                            elN = a.ElementName;
                                        };
                                        if (attr is XmlArrayAttribute)
                                        {
                                            XmlArrayAttribute a = (XmlArrayAttribute)attr;
                                            elN = a.ElementName;
                                        };
                                        if (attr is XmlArrayItemAttribute)
                                        {
                                            XmlArrayItemAttribute a = (XmlArrayItemAttribute)attr;
                                            arN = "<" + a.ElementName + ">" + et.Name + "</" + a.ElementName + ">";
                                        };
                                    };
                                res += String.Format("\r\n{2}<{0}>{1}</{0}>", elN, arN, prefix + "&nbsp;&nbsp;&nbsp;");
                            };
                            if (typeTo == "rpc")
                                res += String.Format("\r\n{2}<member><name>{0}</name>\r\n{2}<value><array><data><value>\r\n{2}<{1}>value</{1}>\r\n{2}</value></data></array></value></member>", mi.Name, et.Name, prefix + "&nbsp;&nbsp;&nbsp;");
                            if (typeTo == "c#")
                            {
                                string arr = "";
                                if (attrs != null)
                                    foreach (object attr in attrs)
                                    {
                                        if (attr is XmlElementAttribute)
                                        {
                                            XmlElementAttribute a = (XmlElementAttribute)attr;
                                            arr += String.Format("{0}[XmlElement(\"{1}\")]\r\n", prefix + "&nbsp;&nbsp;&nbsp;", a.ElementName);
                                        };
                                        if (attr is XmlArrayAttribute)
                                        {
                                            XmlArrayAttribute a = (XmlArrayAttribute)attr;
                                            arr += String.Format("{0}[XmlArray(\"{1}\")]\r\n", prefix + "&nbsp;&nbsp;&nbsp;", a.ElementName);
                                        };
                                        if (attr is XmlArrayItemAttribute)
                                        {
                                            XmlArrayItemAttribute a = (XmlArrayItemAttribute)attr;
                                            arr += String.Format("{0}[XmlArrayItem(\"{1}\")]\r\n", prefix + "&nbsp;&nbsp;&nbsp;", a.ElementName);
                                        };
                                        if (attr is XmlAttributeAttribute)
                                        {
                                            XmlAttributeAttribute a = (XmlAttributeAttribute)attr;
                                            arr += String.Format("{0}[XmlAttribute(\"{1}\")]\r\n", prefix + "&nbsp;&nbsp;&nbsp;", a.AttributeName);
                                        };
                                        if (attr is XmlTextAttribute)
                                        {
                                            arr += String.Format("{0}[XmlText]\r\n", prefix + "&nbsp;&nbsp;&nbsp;");
                                        };
                                    };
                                res += String.Format("\r\n" + arr + "{2}public {1} {0};", mi.Name, tp.Name, prefix + "&nbsp;&nbsp;&nbsp;");
                            };
                        }
                        else
                        {
                            if (typeTo == "json") res += String.Format("\r\n{2}'{0}': '{1}',", mi.Name, tp.Name, prefix + "&nbsp;&nbsp;&nbsp;");
                            if (typeTo == "xml")
                            {
                                string arr = "";
                                bool asTxt = false;
                                if (attrs != null)
                                    foreach (object attr in attrs)
                                    {
                                        if (attr is XmlAttributeAttribute)
                                        {
                                            XmlAttributeAttribute a = (XmlAttributeAttribute)attr;
                                            arr = a.AttributeName;
                                        };
                                        if (attr is XmlTextAttribute)
                                            asTxt = true;
                                    };
                                if (arr == "")
                                {
                                    if (asTxt)
                                        res += String.Format("{0} as {1}", mi.Name, tp.Name);
                                    else
                                        res += String.Format("\r\n{2}<{0}>{1}</{0}>", mi.Name, tp.Name, prefix + "&nbsp;&nbsp;&nbsp;");
                                }
                                else
                                    attributes += String.Format(" {0}=\"{1}\"", mi.Name, tp.Name);
                            };
                            if (typeTo == "rpc")
                                res += String.Format("\r\n{2}<member><name>{0}</name>\r\n{2}<value>\r\n{2}<{1}>value</{1}>\r\n{2}</member>", mi.Name, tp.Name, prefix + "&nbsp;&nbsp;&nbsp;");
                            if (typeTo == "c#")
                            {
                                string arr = "";
                                if (attrs != null)
                                    foreach (object attr in attrs)
                                    {
                                        if (attr is XmlElementAttribute)
                                        {
                                            XmlElementAttribute a = (XmlElementAttribute)attr;
                                            arr += String.Format("{0}[XmlElement(\"{1}\")]\r\n", prefix + "&nbsp;&nbsp;&nbsp;", a.ElementName);
                                        };
                                        if (attr is XmlArrayAttribute)
                                        {
                                            XmlArrayAttribute a = (XmlArrayAttribute)attr;
                                            arr += String.Format("{0}[XmlArray(\"{1}\")]\r\n", prefix + "&nbsp;&nbsp;&nbsp;", a.ElementName);
                                        };
                                        if (attr is XmlArrayItemAttribute)
                                        {
                                            XmlArrayItemAttribute a = (XmlArrayItemAttribute)attr;
                                            arr += String.Format("{0}[XmlArrayItem(\"{1}\")]\r\n", prefix + "&nbsp;&nbsp;&nbsp;", a.ElementName);
                                        };
                                        if (attr is XmlAttributeAttribute)
                                        {
                                            XmlAttributeAttribute a = (XmlAttributeAttribute)attr;
                                            arr += String.Format("{0}[XmlAttribute(\"{1}\")]\r\n", prefix + "&nbsp;&nbsp;&nbsp;", a.AttributeName);
                                        };
                                        if (attr is XmlTextAttribute)
                                        {
                                            arr += String.Format("{0}[XmlText]\r\n", prefix + "&nbsp;&nbsp;&nbsp;");
                                        };
                                    };
                                res += String.Format("\r\n" + arr + "{2}public {1} {0};", mi.Name, tp.Name, prefix + "&nbsp;&nbsp;&nbsp;");
                            };
                        };
                    };
                };
            if (res.EndsWith(",")) res = res.Remove(res.Length - 1);
            if (typeTo == "json") res += "\r\n" + prefix + "}";
            if (typeTo == "c#") res += "\r\n" + prefix + "}";
            return res;
        }

        private static string GetDocumentation<T>(string typeTo)
        {
            bool isSOAP = typeTo == "soap";

            if (string.IsNullOrEmpty(typeTo)) return "";
            string ca;
            string res = ObjTypeToString(typeof(T), isSOAP ? "xml" : typeTo, "", out ca);
            if (isSOAP)
            {
                string rootEl = (typeof(T)).Name;
                object[] maina = (typeof(T)).GetCustomAttributes(false);
                if (maina != null)
                    foreach (object ma in maina)
                        if (ma is XmlRootAttribute)
                        {
                            XmlRootAttribute xra = (XmlRootAttribute)ma;
                            rootEl = xra.ElementName;
                        };
                res = "<?xml version=\"1.0\" encoding=\"utf-8\"?><soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body>\r\n" +
                    "<{METHOD}>" +
                    "<" + rootEl + ca + ">" + res + "\r\n<" + rootEl + ">" +
                    "</{METHOD}>" +
                    "</soap:Body></soap:Envelope>";
                res = res.Replace("<", "&lt;").Replace(">", "&gt;").Replace(" ", "&nbsp;");
            };
            if (typeTo == "xml")
            {
                string rootEl = (typeof(T)).Name;
                object[] maina = (typeof(T)).GetCustomAttributes(false);
                if (maina != null)
                    foreach (object ma in maina)
                        if (ma is XmlRootAttribute)
                        {
                            XmlRootAttribute xra = (XmlRootAttribute)ma;
                            rootEl = xra.ElementName;
                        };
                res = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<" + rootEl + ca + ">" + res + "\r\n<" + rootEl + ">";
                res = res.Replace("<", "&lt;").Replace(">", "&gt;").Replace(" ", "&nbsp;");
            };
            if (typeTo == "rpc")
            {
                res = "<?xml version=\"1.0\"?><methodCall><methodName>{METHOD}</methodName><params><param><struct>\r\n" + res + "\r\n</struct></param></params></methodCall>";
                res = res.Replace("<", "&lt;").Replace(">", "&gt;").Replace(" ", "&nbsp;").Replace("Int32", "i4").Replace("Single", "Double");
            };
            return res.Replace("\r\n", "\r\n<br/>");
        }

        /// <summary>
        ///     Generate Auto Documentation
        /// </summary>
        /// <typeparam name="Tin">request type</typeparam>
        /// <typeparam name="Tout">response type</typeparam>
        /// <param name="methodName">method name</param>
        /// <returns>html text</returns>
        public string GetDocumentation<Tin, Tout>(string methodName)
        {
            string res =
                "<html><title>NMS ROUTE API</title><body><br/>" +
                "Method: <span style=\"color:maroon;\"><b><big><a href=\"/nms/" + methodName + "\">" + methodName + "</a></big></b></span><br/>" +
                "Input Format: <span style=\"color:green;\"><b>JSON, XML, XML-RPC or SOAP</b></span><br/>" +
                "Request Object:<br/>" +
                "<div style=\"border:solid 1px silver;background-color:#CCFFFF;\"><table cellspacing=\"1\" cellpadding=\"1\" style=\"width:100%;\"><tr valign=\"top\"><td align=\"center\" style=\"border-bottom:solid 1px gray;width:34%;\">JSON</td><td align=\"center\" style=\"border-bottom:solid 1px gray;width:34%;\">C#</td><td align=\"center\" style=\"border-bottom:solid 1px gray;width:33%;\">XML</td></tr>" +
                "<tr valign=\"top\"><td style=\"border-right:solid 1px gray;\">" + GetDocumentation<Tin>("json") + "</td><td style=\"border-right:solid 1px gray;\">" + GetDocumentation<Tin>("c#") + "</td><td>" + GetDocumentation<Tin>("xml") + "</td></tr>" +
                "</table></div>" +
                "Response Object:<br/>" +
                "<div style=\"border:solid 1px silver;background-color:#FFFFCC;\"><table cellspacing=\"1\" cellpadding=\"1\" style=\"width:100%;\"><tr valign=\"top\"><td align=\"center\" style=\"border-bottom:solid 1px gray;width:34%;\">JSON</td><td align=\"center\" style=\"border-bottom:solid 1px gray;width:34%;\">C#</td><td align=\"center\" style=\"border-bottom:solid 1px gray;width:33%;\">XML</td></tr>" +
                "<tr valign=\"top\"><td style=\"border-right:solid 1px gray;\">" + GetDocumentation<Tout>("json") + "</td><td style=\"border-right:solid 1px gray;\">" + GetDocumentation<Tout>("c#") + "</td><td>" + GetDocumentation<Tout>("xml") + "</td></tr>" +
                "</table></div><br/>" +
                "<a href=\"/nms/\">main page</a><br/><br/>" +
                "<span style=\"font-size:10px;\">This page was created automatically for method `" + methodName + "`</span>" +
                "</body></html>";
            return res;
        }

        /// <summary>
        ///     Parse Client Body to Specified Object
        /// </summary>
        /// <typeparam name="T">object type</typeparam>
        /// <param name="clientRequest">HTTP Web Client Request</param>
        /// <returns>null if error</returns>
        public CrossSendObject<TypeIn> ParseRequest<TypeIn, TypeOut>(ref ClientRequest clientRequest, string methodName)
        {
            return ParseRequest<TypeIn, TypeOut>(ref clientRequest, methodName, false);
        }

        /// <summary>
        ///     Parse Client Body to Specified Object
        /// </summary>
        /// <typeparam name="T">object type</typeparam>
        /// <param name="clientRequest">HTTP Web Client Request</param>
        /// <returns>null if error</returns>
        public CrossSendObject<TypeIn> ParseRequest<TypeIn, TypeOut>(ref ClientRequest clientRequest, string methodName, bool isValidOnNotEmptyQuery)
        {
            CrossSendObject<TypeIn> clo = null;

            if (clientRequest.IsJSON)
                clo = JSONObject<TypeIn>.Parse(clientRequest.body);
            else if (clientRequest.IsXML)
                clo = XMLObject<TypeIn>.Parse(clientRequest.body);
            else if (clientRequest.IsXMLRPC)
                clo = XMLRPCObject<TypeIn>.Parse(clientRequest.body);
            else if (clientRequest.IsSOAP11)
            {
                clo = XMLSOAP<TypeIn>.Parse(clientRequest.body);
                clientRequest.methodSOAP = ((XMLSOAP<TypeIn>)clo).methodSOAP;
            };

            if (clo == null)
            {
                if (clientRequest.IsHTTPGet)
                {
                    if (isValidOnNotEmptyQuery && (!string.IsNullOrEmpty(clientRequest.query))) return null;
                    HttpClientSendData(clientRequest, 200, GetDocumentation<TypeIn, TypeOut>(methodName), "Method-Name: " + methodName + "\r\n");
                }
                else
                    HttpClientSendData(clientRequest, 415, "Unsupported Media Type", "Method-Name: test\r\n");
                return null;
            }
            else if (!clo.IsValid)
            {
                HttpClientSendData(clientRequest, 400, clo.errorResponse, "Method-Name: test\r\n", (clientRequest.IsXML || clientRequest.IsXMLRPC || clientRequest.IsSOAP) ? "text/xml" : "text/html");
                return null;
            }
            else
            {
                return clo;
            };
        }        
        #endregion

        private bool method_null(ClientRequest clientRequest)
        {
            FileStream fs = new FileStream(XMLSaved<int>.GetCurrentDir() + @"\dkxce.Route.ServiceSolver.Web.index", FileMode.Open, FileAccess.Read);
            byte[] buff = new byte[fs.Length];
            fs.Read(buff, 0, buff.Length);
            fs.Close();

            HttpClientSendData(clientRequest, 200, buff, "", "text/html");
            return true;
        }        

        private bool method_status(ClientRequest clientRequest)
        {
            ReceiveData_ServiceStatus(clientRequest.client.GetStream());
            return true;
        }

        private bool method_help(ClientRequest clientRequest)
        {
            ReceiveData_HTTPDescription(clientRequest.client.GetStream());
            return true;
        }

        private bool method_getdocs(ClientRequest clientRequest)
        {
            if ((string.IsNullOrEmpty(clientRequest.query)) || (!clientRequest.Query.ContainsKey("method")) || (!clientRequest.Query.ContainsKey("io")) || (!clientRequest.Query.ContainsKey("type")))
                return method_null(clientRequest);

            string res = "DOCS NOT FOUND";

            string method = clientRequest.Query["method"];
            string io = clientRequest.Query["io"];
            string iotype = clientRequest.Query["type"];

            if (method == "route")
                res = io == "in" ? GetDocumentation<tcRoute>(iotype) : GetDocumentation<tsRoute>(iotype);
            else if (method == "nearroad")
                res = io == "in" ? GetDocumentation<tcNearRoad>(iotype) : GetDocumentation<tsNearRoads>(iotype);

            HttpClientSendData(clientRequest, 200, res, "Method-Name: getdocs\r\n", "text/html");
            return true;
        }


        private bool method_route(ClientRequest clientRequest)
        {
            tcRoute co = null;

            // parse input data
            CrossSendObject<tcRoute> clo = ParseRequest<tcRoute, tsRoute>(ref clientRequest, "route", true);
            if (clo == null) // parse query data
            {
                try
                {
                    co = CrossSendObject<tcRoute>.fromQuery(clientRequest.Query);
                }
                catch (Exception ex)
                {
                    HttpClientSendData(clientRequest, 400, ex.Message);
                    return true;
                };
                if (clientRequest.Query.ContainsKey("x") && (clientRequest.Query.ContainsKey("y")))
                {
                    string[] sx = clientRequest.Query["x"].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    string[] sy = clientRequest.Query["y"].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if ((sx != null) && (sx.Length > 0) && (sy != null) && (sx.Length == sy.Length))
                    {
                        co.xy = new XYN[sx.Length];
                        string[] xn = new string[0];
                        if (clientRequest.Query.ContainsKey("n"))
                        {
                            xn = clientRequest.Query["n"].Split(new char[] { ',' }, StringSplitOptions.None);
                            if (xn == null) xn = new string[0];
                        };
                        for (int i = 0; i < sx.Length; i++)
                        {
                            co.xy[i] = new XYN(double.Parse(sx[i], System.Globalization.CultureInfo.InvariantCulture), double.Parse(sy[i], System.Globalization.CultureInfo.InvariantCulture));
                            if (xn.Length > i) co.xy[i].n = System.Security.SecurityElement.Escape(HttpUtility.UrlDecode(xn[i]));
                        };
                    };
                };
                if (clientRequest.Query.ContainsKey("ex") && (clientRequest.Query.ContainsKey("ey")))
                {
                    string[] sx = clientRequest.Query["ex"].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    string[] sy = clientRequest.Query["ey"].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if ((sx != null) && (sx.Length > 0) && (sy != null) && (sx.Length == sy.Length))
                    {
                        co.exy = new XYN[sx.Length];
                        for (int i = 0; i < sx.Length; i++)
                            co.exy[i] = new XYN(double.Parse(sx[i], System.Globalization.CultureInfo.InvariantCulture), double.Parse(sy[i], System.Globalization.CultureInfo.InvariantCulture));
                    };
                };
            }
            else
                co = clo.obj;
            if (string.IsNullOrEmpty(co.i)) co.i = "0";
            if (string.IsNullOrEmpty(co.f)) co.f = "0";
            if (string.IsNullOrEmpty(co.v)) co.v = "0";

            if (string.IsNullOrEmpty(co.k))
            {
                co.k = co.key;
                if (string.IsNullOrEmpty(co.k))
                {
                    HttpClientSendData(clientRequest, 400, "USER KEY FAILED");
                    return true;
                };
            };

            if ((co.xy == null) || (co.xy.Length < 2))
            {
                HttpClientSendData(clientRequest, 400, "NEED AT LEAST 2 COORDINATES TO SEARCH ROUTE");
                return true;
            };            

            List<RStop> stops = new List<RStop>();

            foreach (XYN xy in co.xy)
                stops.Add(new RStop(xy.n, xy.y, xy.x));

            dkxce.Route.Classes.PointF[] el = null;
            if ((co.exy != null) && (co.exy.Length > 0))
            {
                el = new dkxce.Route.Classes.PointF[co.exy.Length];
                for (int i = 0; i < el.Length; i++)
                    el[i] = new dkxce.Route.Classes.PointF((float)co.exy[i].x, (float)co.exy[i].y);
            };
            double er = 50;
            double.TryParse(co.er, out er);
            byte[] ronly = null;
            if ((co.ra != String.Empty) && (co.ra.Length == 32))
            {
                ronly = new byte[16];
                for (int i = 0; i < ronly.Length; i++)
                {
                    string txt = co.ra.Substring(i * 2, 2);
                    byte b = 0;
                    try
                    {
                        b = byte.Parse(txt, System.Globalization.NumberStyles.HexNumber);
                    }
                    catch { };
                    ronly[i] = b;
                };
            };
            long otherFlags = 0;
            if (co.p == "1") otherFlags += 0x01;
            if (co.i == "1") otherFlags += 0x02;
            if (co.ct == "1") otherFlags += 0x04;
            if (co.ht == "1") otherFlags += 0x08;
            if (co.o == "1") otherFlags += 0x10;
            if (co.minby == "dist") otherFlags += 0x20;
            if (co.v == "1") otherFlags += 0x40;

            DateTime startTime = DateTime.Now;
            DateTime.TryParse(co.t, out startTime);

#if LICENSE
            int lIndex = lics.keys.IndexOf(co.k);
            if (lIndex < 0)
            {
                HttpClientSendData(clientRequest, 423, "001 BAD KEY", "Method-Name: route\r\n", "text/plain");
                return true;
            }
            else if (DateTime.Now > lics.expires[lIndex])
            {
                HttpClientSendData(clientRequest, 423, "002 Support Expired", "Method-Name: route\r\n", "text/plain");
                return true;
            };
#else

            if ((RouteThreader.mem.config != null) && (RouteThreader.mem.config.http_licenses != null) && (RouteThreader.mem.config.http_licenses.Count > 0))
            {
                co.k = co.k.ToUpper();
                HTTPLicense hlic = null;
                foreach(HTTPLicense tmpl in RouteThreader.mem.config.http_licenses)
                    if (tmpl.key == co.k) { hlic = tmpl; break; };
                if (hlic == null)
                {
                    HttpClientSendData(clientRequest, 423, "001 BAD KEY", "Method-Name: route\r\n", "text/plain");
                    return true;
                }
                else if (DateTime.Now > hlic.expires)
                {
                    HttpClientSendData(clientRequest, 423, "002 Support Expired", "Method-Name: route\r\n", "text/plain");
                    return true;
                } 
                else if (!String.IsNullOrEmpty(hlic.ip))
                {
                    bool ma = true;
                    string hlicip = hlic.ip;
                    if (hlicip.StartsWith("!")) { hlicip = hlic.ip.Substring(1); ma = false; };
                    Regex rx = new Regex(hlic.ip, RegexOptions.None);
                    string ip = ((IPEndPoint)clientRequest.client.Client.RemoteEndPoint).Address.ToString();
                    bool mb = rx.IsMatch(ip);
                    if((ma && !mb) || (!ma && mb))
                    {
                        HttpClientSendData(clientRequest, 423, "004 Client blocked by key rules", "Method-Name: route\r\n", "text/plain");
                        return true;
                    };
                };
            };
#endif

            RouteThreader rg = new RouteThreader();
            tsRoute rt = null;
            try
            {
                rt = tsRoute.FromRR(rg.GetRoute(stops.ToArray(), startTime, otherFlags, null, el, er, ronly));
            }
            catch (Exception ex)
            {
                HttpClientSendData(clientRequest, 500, ex.Message);
                return true;
            };
            
            
            if (clientRequest.IsHTTPGet)
            {
                //if ((co.f == "j") || (co.f == "json") || (co.f == "0") || (co.f == "")) 
                clientRequest.clientType = 1;

                if ((co.f == "xml") || (co.f == "x") || (co.f == "2")) clientRequest.clientType = 2;

                if ((co.f == "t") || (co.f == "txt") || (co.f == "1"))
                {
                    string outtxt = ("Длина пути: " + rt.driveLength.ToString().Replace(",", ".") + " км, Время в пути: " + rt.driveTime.ToString().Replace(",", ".") + " мин, Прибытие в " + rt.finishTime.ToString() + "\r\n\r\n");
                    if((rt.instructions != null) && (rt.instructions.Length > 0))
                        for (int i = 0; i < rt.instructions.Length; i++)
                            outtxt += ("[" + rt.instructions[i].no.ToString() + "] " + rt.instructions[i].tTime.ToString("HH:mm") + "\t\t\t " + rt.instructions[i].tLen.ToString().Replace(",", ".") + " км\r\n" + rt.instructions[i].iToDo + " " + rt.instructions[i].iToGo + "\r\n\r\n");
                    HttpClientSendData(clientRequest, 200, outtxt, "Method-Name: route\r\n", "text/plain");
                    return true;
                };
            };

            if ((co.f == "kml") || (co.f == "k") || (co.f == "3"))
            {
                string outxml = "";
                outxml += ("<?xml version=\"1.0\" encoding=\"UTF-8\"?><kml xmlns=\"http://earth.google.com/kml/2.2\"><Document xmlns=\"\"><name>Маршрут: " + System.Security.SecurityElement.Escape(rt.stops[0].name) + " - " + System.Security.SecurityElement.Escape(rt.stops[rt.stops.Length - 1].name) + "</name>\r\n");
                outxml += ("<Folder><name>Путь</name>\r\n");

                for (int i = 0; i < rt.stops.Length; i++)
                {
                    outxml += ("<Placemark><name>" + System.Security.SecurityElement.Escape(rt.stops[i].name) + "</name><Point><coordinates>");
                    outxml += (rt.stops[i].lon.ToString().Replace(",", ".") + "," + rt.stops[i].lat.ToString().Replace(",", ".") + ",0 ");
                    outxml += ("</coordinates></Point></Placemark>\r\n");
                };

                outxml += ("<Placemark><name>" + System.Security.SecurityElement.Escape(rt.stops[0].name) + " - " + System.Security.SecurityElement.Escape(rt.stops[rt.stops.Length - 1].name) + "</name>");
                outxml += ("<description>\r\n");
                outxml += ("Длина: " + (rt.driveLength / 1024).ToString().Replace(",", ".") + " км. \r\n");
                outxml += ("Время: " + rt.driveTime.ToString().Replace(",", ".") + " мин. \r\n");
                outxml += ("Выезд: " + rt.startTime.ToString("HH:mm:ss dd.MM.yyyy") + " \r\n");
                outxml += ("Прибытие: " + rt.finishTime.ToString("HH:mm:ss dd.MM.yyyy") + " \r\n");
                outxml += ("</description>\r\n");
                outxml += ("<LineString><coordinates>\r\n");
                if((rt.polyline != null) && (rt.polyline.Length > 0))
                    for (int i = 0; i < rt.polyline.Length; i++)
                    {
                        outxml += (rt.polyline[i].x.ToString().Replace(",", ".") + "," + rt.polyline[i].y.ToString().Replace(",", ".") + ",0 ");
                    };
                outxml += ("</coordinates></LineString></Placemark>");
                outxml += ("</Folder>");

                if ((rt.instructions != null) && (rt.instructions.Length > 0))
                {
                    outxml += ("<Folder><name>Инструкции</name>\r\n");
                    for (int i = 0; i < rt.instructions.Length; i++)
                    {
                        outxml += ("<Placemark><name>" + rt.instructions[i].no.ToString() + " (" + (rt.instructions[i].tLen / 1024).ToString("0.00").Replace(",", ".") + " км)" + (rt.instructions[i].iStreet.Length > 0 ? ", " + rt.instructions[i].iStreet : "") + "</name>");
                        if (i < (rt.instructions.Length - 1))
                        {
                            outxml += ("<description>");
                            outxml += (rt.instructions[i].iToDo + ", " + rt.instructions[i].iToGo + "; \r\n");
                            outxml += ("</description>\r\n");
                        };
                        outxml += ("<Point><coordinates>");
                        outxml += (rt.instructions[i].x.ToString().Replace(",", ".") + "," + rt.instructions[i].y.ToString().Replace(",", ".") + ",0 ");
                        outxml += ("</coordinates></Point></Placemark>\r\n");
                    };
                    outxml += ("</Folder>");
                };

                outxml += ("</Document></kml>");
                HttpClientSendData(clientRequest, 200, outxml, "Method-Name: route\r\n", "text/xml");
                return true;
            };

            if ((co.f == "geojson") || (co.f == "g") || (co.f == "4"))
            {
                string outxml = "";
                outxml += ("{ \"type\": \"FeatureCollection\", \"features\": [\r\n");
                int fCount = 0;

                for (int i = 0; i < rt.stops.Length; i++)
                {
                    if ((fCount++) > 0) outxml += (",\r\n");
                    outxml += ("\t{ \"type\": \"Feature\",\r\n");
                    outxml += ("\t\t\"geometry\": {\"type\": \"Point\", \"coordinates\": [" + rt.stops[i].lon.ToString().Replace(",", ".") + "," + rt.stops[i].lat.ToString().Replace(",", ".") + "]},\r\n");
                    outxml += ("\t\t\"properties\": {");
                    outxml += ("\"type\": \"stop\"");
                    outxml += (", \"name\": \"" + System.Security.SecurityElement.Escape(System.Security.SecurityElement.Escape(rt.stops[i].name)) + "\"");
                    outxml += (", \"driveLengthSegment\": " + rt.driveLengthSegments[i].ToString().Replace(",", "."));
                    outxml += (", \"driveTimeSegment\": " + rt.driveTimeSegments[i].ToString().Replace(",", "."));
                    if ((rt.polyline != null) && (rt.polyline.Length > 0))
                        outxml += (", \"polylineSegment\": " + rt.polylineSegments[i].ToString());
                    if ((rt.instructions != null) && (rt.instructions.Length > 0))
                        outxml += (", \"instructionsSegment\": " + rt.instructionsSegments[i].ToString());
                    outxml += ("}\r\n");
                    outxml += ("\t}");
                };
                if ((rt.polyline != null) && (rt.polyline.Length > 0))
                {
                    if ((fCount++) > 0) outxml += (",\r\n");
                    outxml += ("\t{ \"type\": \"Feature\",\r\n");
                    outxml += ("\t\t\"geometry\": {\"type\": \"LineString\", \"coordinates\": [");
                    for (int i = 0; i < rt.polyline.Length; i++)
                    {
                        if (i > 0) outxml += (",");
                        outxml += ("[" + rt.polyline[i].x.ToString().Replace(",", ".") + "," + rt.polyline[i].y.ToString().Replace(",", ".") + "]");
                    };
                    outxml += ("]},\r\n");
                    outxml += ("\t\t\"properties\": {");
                    outxml += ("\"type\": \"path\", ");
                    outxml += ("\"name\": \"route\", ");
                    outxml += ("\"driveLength\": " + rt.driveLength.ToString().Replace(",", ".") + ", ");
                    outxml += ("\"driveTime\": " + rt.driveTime.ToString().Replace(",", ".") + ", ");
                    outxml += ("\"startTime\": \"" + rt.startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz") + "\", ");
                    outxml += ("\"finishTime\": \"" + rt.finishTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz") + "\", ");
                    outxml += ("}\r\n");
                    outxml += ("\t}");
                };
                if ((rt.instructions != null) && (rt.instructions.Length > 0))
                {
                    for (int i = 0; i < rt.instructions.Length; i++)
                    {
                        if ((fCount++) > 0) outxml += (",\r\n");
                        outxml += ("\t{ \"type\": \"Feature\",\r\n");
                        outxml += ("\t\t\"geometry\": {\"type\": \"Point\", \"coordinates\": [" + rt.instructions[i].x.ToString().Replace(",", ".") + "," + rt.instructions[i].y.ToString().Replace(",", ".") + "]},\r\n");
                        outxml += ("\t\t\"properties\": {");
                        outxml += ("\"type\": \"instruction\"");
                        outxml += (", \"no\": " + rt.instructions[i].no.ToString());
                        outxml += (", \"iToDo\": \"" + System.Security.SecurityElement.Escape(rt.instructions[i].iToDo) + "\"");
                        outxml += (", \"iToGo\": \"" + System.Security.SecurityElement.Escape(rt.instructions[i].iToGo) + "\"");
                        outxml += (", \"iStreet\": \"" + System.Security.SecurityElement.Escape(rt.instructions[i].iStreet) + "\"");
                        outxml += (", \"sTime\": " + rt.instructions[i].sTime.ToString().Replace(",", "."));
                        outxml += (", \"sLen\": " + rt.instructions[i].sLen.ToString().Replace(",", "."));
                        outxml += (", \"tTime\": \"" + rt.instructions[i].tTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz") + "\"");
                        outxml += (", \"tLen\": " + rt.instructions[i].tLen.ToString().Replace(",", "."));
                        outxml += ("}\r\n");
                        outxml += ("\t}");
                    };
                };

                outxml += ("\r\n");
                outxml += ("]}\r\n");
                HttpClientSendData(clientRequest, 200, outxml, "Method-Name: route\r\n" + (rg == null ? "" : "Service-RTI: idle=" + rg.GetIdleThreadCount().ToString() + ",total=" + rg.GetThreadsCount().ToString() + "\r\n"), "text/plain");
                return true;
            };

            HttpClientSendData<tsRoute>(clientRequest, 200, rt, "Method-Name: route\r\n" + (rg == null ? "" : "Service-RTI: idle=" + rg.GetIdleThreadCount().ToString() + ",total=" + rg.GetThreadsCount().ToString() + "\r\n"));
            return true; // all is ok, close stream
        }

        private bool method_nearroad(ClientRequest clientRequest)
        {
            tcNearRoad co = null;

            // parse input data
            CrossSendObject<tcNearRoad> clo = ParseRequest<tcNearRoad, tsNearRoads>(ref clientRequest, "nearroad", true);
            if (clo == null) // parse query data
            {
                try
                {
                    co = CrossSendObject<tcNearRoad>.fromQuery(clientRequest.Query);
                }
                catch (Exception ex)
                {
                    HttpClientSendData(clientRequest, 400, ex.Message);
                    return true;
                };
            }
            else
                co = clo.obj;
            if (string.IsNullOrEmpty(co.f)) co.f = "0";

            if (string.IsNullOrEmpty(co.k))
            {
                co.k = co.key;
                if (string.IsNullOrEmpty(co.k))
                {
                    HttpClientSendData(clientRequest, 400, "USER KEY FAILED");
                    return true;
                };
            };

            if ((co.x == null) || (co.y == null) || (co.x.Length != co.y.Length) || (co.x.Length == 0))
            {
                HttpClientSendData(clientRequest, 400, "Error: COORDINATES FAIL");
                return true;
            };

#if LICENSE
            int lIndex = lics.keys.IndexOf(co.k);
            if (lIndex < 0)
            {
                HttpClientSendData(clientRequest, 423, "001 BAD KEY", "Method-Name: nearroad\r\n", "text/plain");
                return true;
            }
            else if (DateTime.Now > lics.expires[lIndex])
            {
                HttpClientSendData(clientRequest, 423, "002 Support Expired", "Method-Name: nearroad\r\n", "text/plain");
                return true;
            };
#else
            if ((RouteThreader.mem.config != null) && (RouteThreader.mem.config.http_licenses != null) && (RouteThreader.mem.config.http_licenses.Count > 0))
            {
                co.k = co.k.ToUpper();
                HTTPLicense hlic = null;
                foreach (HTTPLicense tmpl in RouteThreader.mem.config.http_licenses)
                    if (tmpl.key == co.k) { hlic = tmpl; break; };
                if (hlic == null)
                {
                    HttpClientSendData(clientRequest, 423, "001 BAD KEY", "Method-Name: nearroad\r\n", "text/plain");
                    return true;
                }
                else if (DateTime.Now > hlic.expires)
                {
                    HttpClientSendData(clientRequest, 423, "002 Support Expired", "Method-Name: nearroad\r\n", "text/plain");
                    return true;
                }
                else if (!String.IsNullOrEmpty(hlic.ip))
                {
                    bool ma = true;
                    string hlicip = hlic.ip;
                    if (hlicip.StartsWith("!")) { hlicip = hlic.ip.Substring(1); ma = false; };
                    Regex rx = new Regex(hlic.ip, RegexOptions.None);
                    string ip = ((IPEndPoint)clientRequest.client.Client.RemoteEndPoint).Address.ToString();
                    bool mb = rx.IsMatch(ip);
                    if ((ma && !mb) || (!ma && mb))
                    {
                        HttpClientSendData(clientRequest, 423, "004 Client blocked by key rules", "Method-Name: nearroad\r\n", "text/plain");
                        return true;
                    };
                };
            };
#endif

            RouteThreader rg = new RouteThreader();
            tsNearRoads res = null;
            try
            {
                res = tsNearRoads.FromRR(rg.GetNearRoad(co.y, co.x, co.n == "1"));
            }
            catch (Exception ex)
            {
                HttpClientSendData(clientRequest, 500, ex.Message);
                return true;
            };

            if (clientRequest.IsHTTPGet)
            {
                //if ((co.f == "j") || (co.f == "json") || (co.f == "0") || (co.f == "")) 
                clientRequest.clientType = 1;
                if ((co.f == "xml") || (co.f == "x") || (co.f == "2")) clientRequest.clientType = 2;
                if ((co.f == "t") || (co.f == "txt") || (co.f == "1"))
                {
                    string outtxt = "";
                    for (int i = 0; i < res.roads.Length; i++)
                        outtxt += (res.roads[i].lat + " " + res.roads[i].lon + "; " + res.roads[i].distance + "; " + res.roads[i].name + "\r\n");
                    HttpClientSendData(clientRequest, 200, outtxt, "Method-Name: nearroad\r\n" + (rg == null ? "" : "Service-RTI: idle=" + rg.GetIdleThreadCount().ToString() + ",total=" + rg.GetThreadsCount().ToString() + "\r\n"), "text/plain");
                    return true;
                };
            };

            HttpClientSendData<tsNearRoads>(clientRequest, 200, res, "Method-Name: nearroad\r\n" + (rg == null ? "" : "Service-RTI: idle=" + rg.GetIdleThreadCount().ToString() + ",total=" + rg.GetThreadsCount().ToString() + "\r\n"));
            return true; // all is ok, close stream
        }       
        
        public void RegisterMethod(MethodInfo method)
        {
            if ((method.implementation != null) && (!string.IsNullOrEmpty(method.name)))
                METHODS.Add(method.name, method);
        }

        private void InitMethods()
        {
            // Standard
            RegisterMethod(new MethodInfo("xmlwsdl", "0.0.0.1", method_xmlwsdl));
            RegisterMethod(new MethodInfo("xmlsoap", "0.0.0.1", method_xmlsoap));
            RegisterMethod(new MethodInfo("soap.asmx", "0.0.0.1", method_xmlsoap));
            RegisterMethod(new MethodInfo("xmlrpc", "0.0.0.1", method_xmlrpc));
            RegisterMethod(new MethodInfo("xmlrpc.asmx", "0.0.0.1", method_xmlrpc));
            // Main
            RegisterMethod(new MethodInfo("getdocs", "0.0.0.1", method_getdocs));
            RegisterMethod(new MethodInfo("null", "1.8.0.2", method_null)); // 
            RegisterMethod(new MethodInfo("status", "1.8.0.2", method_status)); // 
            RegisterMethod(new MethodInfo("help", "1.8.0.2", method_help)); // 
            RegisterMethod(new MethodInfo("route", "1.8.0.2", method_route)); // 
            RegisterMethod(new MethodInfo("sroute.ashx", "1.8.0.2", method_route));
            RegisterMethod(new MethodInfo("nearroad", "1.8.0.2", method_nearroad)); // 
            RegisterMethod(new MethodInfo("snearroad.ashx", "1.8.0.2", method_nearroad));
        }
   }

    internal class PrivilegeManager
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool OpenProcessToken(
            IntPtr ProcessHandle,
            UInt32 DesiredAccess, out IntPtr TokenHandle);

        private static uint STANDARD_RIGHTS_REQUIRED = 0x000F0000;
        private static uint STANDARD_RIGHTS_READ = 0x00020000;
        private static uint TOKEN_ASSIGN_PRIMARY = 0x0001;
        private static uint TOKEN_DUPLICATE = 0x0002;
        private static uint TOKEN_IMPERSONATE = 0x0004;
        private static uint TOKEN_QUERY = 0x0008;
        private static uint TOKEN_QUERY_SOURCE = 0x0010;
        private static uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private static uint TOKEN_ADJUST_GROUPS = 0x0040;
        private static uint TOKEN_ADJUST_DEFAULT = 0x0080;
        private static uint TOKEN_ADJUST_SESSIONID = 0x0100;
        private static uint TOKEN_READ = (STANDARD_RIGHTS_READ | TOKEN_QUERY);
        private static uint TOKEN_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED | TOKEN_ASSIGN_PRIMARY |
            TOKEN_DUPLICATE | TOKEN_IMPERSONATE | TOKEN_QUERY | TOKEN_QUERY_SOURCE |
            TOKEN_ADJUST_PRIVILEGES | TOKEN_ADJUST_GROUPS | TOKEN_ADJUST_DEFAULT |
            TOKEN_ADJUST_SESSIONID);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool LookupPrivilegeValue(
            string lpSystemName,
            string lpName,
            out LUID lpLuid);

        #region Privelege constants

        public const string SE_ASSIGNPRIMARYTOKEN_NAME = "SeAssignPrimaryTokenPrivilege";
        public const string SE_AUDIT_NAME = "SeAuditPrivilege";
        public const string SE_BACKUP_NAME = "SeBackupPrivilege";
        public const string SE_CHANGE_NOTIFY_NAME = "SeChangeNotifyPrivilege";
        public const string SE_CREATE_GLOBAL_NAME = "SeCreateGlobalPrivilege";
        public const string SE_CREATE_PAGEFILE_NAME = "SeCreatePagefilePrivilege";
        public const string SE_CREATE_PERMANENT_NAME = "SeCreatePermanentPrivilege";
        public const string SE_CREATE_SYMBOLIC_LINK_NAME = "SeCreateSymbolicLinkPrivilege";
        public const string SE_CREATE_TOKEN_NAME = "SeCreateTokenPrivilege";
        public const string SE_DEBUG_NAME = "SeDebugPrivilege";
        public const string SE_ENABLE_DELEGATION_NAME = "SeEnableDelegationPrivilege";
        public const string SE_IMPERSONATE_NAME = "SeImpersonatePrivilege";
        public const string SE_INC_BASE_PRIORITY_NAME = "SeIncreaseBasePriorityPrivilege";
        public const string SE_INCREASE_QUOTA_NAME = "SeIncreaseQuotaPrivilege";
        public const string SE_INC_WORKING_SET_NAME = "SeIncreaseWorkingSetPrivilege";
        public const string SE_LOAD_DRIVER_NAME = "SeLoadDriverPrivilege";
        public const string SE_LOCK_MEMORY_NAME = "SeLockMemoryPrivilege";
        public const string SE_MACHINE_ACCOUNT_NAME = "SeMachineAccountPrivilege";
        public const string SE_MANAGE_VOLUME_NAME = "SeManageVolumePrivilege";
        public const string SE_PROF_SINGLE_PROCESS_NAME = "SeProfileSingleProcessPrivilege";
        public const string SE_RELABEL_NAME = "SeRelabelPrivilege";
        public const string SE_REMOTE_SHUTDOWN_NAME = "SeRemoteShutdownPrivilege";
        public const string SE_RESTORE_NAME = "SeRestorePrivilege";
        public const string SE_SECURITY_NAME = "SeSecurityPrivilege";
        public const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
        public const string SE_SYNC_AGENT_NAME = "SeSyncAgentPrivilege";
        public const string SE_SYSTEM_ENVIRONMENT_NAME = "SeSystemEnvironmentPrivilege";
        public const string SE_SYSTEM_PROFILE_NAME = "SeSystemProfilePrivilege";
        public const string SE_SYSTEMTIME_NAME = "SeSystemtimePrivilege";
        public const string SE_TAKE_OWNERSHIP_NAME = "SeTakeOwnershipPrivilege";
        public const string SE_TCB_NAME = "SeTcbPrivilege";
        public const string SE_TIME_ZONE_NAME = "SeTimeZonePrivilege";
        public const string SE_TRUSTED_CREDMAN_ACCESS_NAME = "SeTrustedCredManAccessPrivilege";
        public const string SE_UNDOCK_NAME = "SeUndockPrivilege";
        public const string SE_UNSOLICITED_INPUT_NAME = "SeUnsolicitedInputPrivilege";
        #endregion

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public UInt32 LowPart;
            public Int32 HighPart;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hHandle);

        public const UInt32 SE_PRIVILEGE_ENABLED_BY_DEFAULT = 0x00000001;
        public const UInt32 SE_PRIVILEGE_ENABLED = 0x00000002;
        public const UInt32 SE_PRIVILEGE_REMOVED = 0x00000004;
        public const UInt32 SE_PRIVILEGE_USED_FOR_ACCESS = 0x80000000;

        [StructLayout(LayoutKind.Sequential)]
        public struct TOKEN_PRIVILEGES
        {
            public UInt32 PrivilegeCount;
            public LUID Luid;
            public UInt32 Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public UInt32 Attributes;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AdjustTokenPrivileges(
            IntPtr TokenHandle,
           [MarshalAs(UnmanagedType.Bool)]bool DisableAllPrivileges,
           ref TOKEN_PRIVILEGES NewState,
           UInt32 Zero,
           IntPtr Null1,
           IntPtr Null2);

        /// <summary>
        /// Меняет привилегию
        /// </summary>
        /// <param name="PID">ID процесса</param>
        /// <param name="privelege">Привилегия</param>
        public static void SetPrivilege(
            IntPtr PID,
            string privilege)
        {
            IntPtr hToken;
            LUID luidSEDebugNameValue;
            TOKEN_PRIVILEGES tkpPrivileges;

            if (!OpenProcessToken(PID, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out hToken))
            {
                throw new Exception("Произошла ошибка при выполнении OpenProcessToken(). Код ошибки "
                    + Marshal.GetLastWin32Error());
            }

            if (!LookupPrivilegeValue(null, privilege, out luidSEDebugNameValue))
            {
                CloseHandle(hToken);
                throw new Exception("Произошла ошибка при выполнении LookupPrivilegeValue(). Код ошибки "
                    + Marshal.GetLastWin32Error());
            }

            tkpPrivileges.PrivilegeCount = 1;
            tkpPrivileges.Luid = luidSEDebugNameValue;
            tkpPrivileges.Attributes = SE_PRIVILEGE_ENABLED;

            if (!AdjustTokenPrivileges(hToken, false, ref tkpPrivileges, 0, IntPtr.Zero, IntPtr.Zero))
            {
                throw new Exception("Произошла ошибка при выполнении LookupPrivilegeValue(). Код ошибки :"
                    + Marshal.GetLastWin32Error());
            }
            CloseHandle(hToken);
        }
    }

   internal class FileMappingServer
    {
        private SafeFileMappingHandle hMapFile = null;
        private IntPtr pView = IntPtr.Zero;

        private const string FullMapName = "Global\\dRSSThreads";
        private const uint MapSize = 65536;
        private const uint ViewOffset = 0;
        private const uint ViewSize = 15 * 1024;

        private bool connected = false;
        public bool Connected { get { return connected; } }        

        public void Connect()
        {
            try { PrivilegeManager.SetPrivilege(Process.GetCurrentProcess().Handle, PrivilegeManager.SE_CREATE_GLOBAL_NAME); }
            catch { };

            try
            {
                SECURITY_ATTRIBUTES sa = SECURITY_ATTRIBUTES.Empty;
                hMapFile = NativeMethod.CreateFileMapping(INVALID_HANDLE_VALUE, ref sa, FileProtection.PAGE_READWRITE, 0, MapSize, FullMapName);
                
                if (hMapFile.IsInvalid) throw new Win32Exception();

                //IntPtr sidPtr = IntPtr.Zero;
                //SECURITY_INFORMATION sFlags = SECURITY_INFORMATION.Owner;
                //System.Security.Principal.NTAccount user = new System.Security.Principal.NTAccount("P1R4T3\\Harris");
                //System.Security.Principal.SecurityIdentifier sid = (System.Security.Principal.SecurityIdentifier)user.Translate(typeof(System.Security.Principal.SecurityIdentifier));
                //ConvertStringSidToSid(sid.ToString(), ref sidPtr);
                SetNamedSecurityInfoW(FullMapName, SE_OBJECT_TYPE.SE_KERNEL_OBJECT, SECURITY_INFORMATION.Dacl, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                
                pView = NativeMethod.MapViewOfFile(hMapFile,FileMapAccess.FILE_MAP_ALL_ACCESS,0,ViewOffset,ViewSize);
                if (pView == IntPtr.Zero) throw new Win32Exception();
                connected = true;
                byte[] bMessage = Encoding.Unicode.GetBytes("BEGINS"+'\0');
                Marshal.Copy(bMessage, 0, pView, bMessage.Length);
            }
            catch (Exception ex)
            {
                XMLSaved<int>.Add2SysLog(ex.Message);
            };
        }

        public void WriteData(RouteThreaderInfo message)
        {
            if (connected)
            {
                byte[] bMessage = Encoding.Unicode.GetBytes(XMLSaved<RouteThreaderInfo>.Save(message) + '\0');
                Marshal.Copy(bMessage, 0, pView, bMessage.Length);                
            };
        }

        public void Close()
        {
            if (hMapFile != null)
            {
                if (pView != IntPtr.Zero)
                {
                    NativeMethod.UnmapViewOfFile(pView);
                    pView = IntPtr.Zero;
                };
                hMapFile.Close();
                hMapFile = null;
            };
            connected = false;
        }

        #region Native API Signatures and Types

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SetNamedSecurityInfoW(String pObjectName, SE_OBJECT_TYPE ObjectType, SECURITY_INFORMATION SecurityInfo, IntPtr psidOwner, IntPtr psidGroup, IntPtr pDacl, IntPtr pSacl);

        [DllImport("Advapi32.dll", SetLastError = true)]
        private static extern bool ConvertStringSidToSid(String StringSid, ref IntPtr Sid);

        private enum SE_OBJECT_TYPE
        {
            SE_UNKNOWN_OBJECT_TYPE = 0,
            SE_FILE_OBJECT,
            SE_SERVICE,
            SE_PRINTER,
            SE_REGISTRY_KEY,
            SE_LMSHARE,
            SE_KERNEL_OBJECT,
            SE_WINDOW_OBJECT,
            SE_DS_OBJECT,
            SE_DS_OBJECT_ALL,
            SE_PROVIDER_DEFINED_OBJECT,
            SE_WMIGUID_OBJECT,
            SE_REGISTRY_WOW64_32KEY
        }

        [Flags]
        private enum SECURITY_INFORMATION : uint
        {
            Owner = 0x00000001,
            Group = 0x00000002,
            Dacl = 0x00000004,
            Sacl = 0x00000008,
            ProtectedDacl = 0x80000000,
            ProtectedSacl = 0x40000000,
            UnprotectedDacl = 0x20000000,
            UnprotectedSacl = 0x10000000
        }
        

        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;

            public static SECURITY_ATTRIBUTES Empty
            {
                get 
                {
                    SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES();
                    sa.nLength = sizeof(int)*2 + IntPtr.Size;
                    sa.lpSecurityDescriptor = IntPtr.Zero;
                    sa.bInheritHandle = 0;
                    return sa;
                }
            }
        }
        
        /// <summary>
        /// Memory Protection Constants
        /// http://msdn.microsoft.com/en-us/library/aa366786.aspx
        /// </summary>
        [Flags]
        public enum FileProtection : uint
        {
            NONE = 0x00,
            PAGE_NOACCESS = 0x01,
            PAGE_READONLY = 0x02,
            PAGE_READWRITE = 0x04,
            PAGE_WRITECOPY = 0x08,
            PAGE_EXECUTE = 0x10,
            PAGE_EXECUTE_READ = 0x20,
            PAGE_EXECUTE_READWRITE = 0x40,
            PAGE_EXECUTE_WRITECOPY = 0x80,
            PAGE_GUARD = 0x100,
            PAGE_NOCACHE = 0x200,
            PAGE_WRITECOMBINE = 0x400,
            SEC_FILE = 0x800000,
            SEC_IMAGE = 0x1000000,
            SEC_RESERVE = 0x4000000,
            SEC_COMMIT = 0x8000000,
            SEC_NOCACHE = 0x10000000
        }


        /// <summary>
        /// Access rights for file mapping objects
        /// http://msdn.microsoft.com/en-us/library/aa366559.aspx
        /// </summary>
        [Flags]
        public enum FileMapAccess
        {
            FILE_MAP_COPY = 0x0001,
            FILE_MAP_WRITE = 0x0002,
            FILE_MAP_READ = 0x0004,
            FILE_MAP_ALL_ACCESS = 0x000F001F
        }


        /// <summary>
        /// Represents a wrapper class for a file mapping handle. 
        /// </summary>
        [SuppressUnmanagedCodeSecurity,
        HostProtection(SecurityAction.LinkDemand, MayLeakOnAbort = true)]
        internal sealed class SafeFileMappingHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
            private SafeFileMappingHandle()
                : base(true)
            {
            }

            [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
            public SafeFileMappingHandle(IntPtr handle, bool ownsHandle)
                : base(ownsHandle)
            {
                base.SetHandle(handle);
            }

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success),
            DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool CloseHandle(IntPtr handle);

            protected override bool ReleaseHandle()
            {
                return CloseHandle(base.handle);
            }
        }


        internal static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);


        /// <summary>
        /// The class exposes Windows APIs used in this code sample.
        /// </summary>
        [SuppressUnmanagedCodeSecurity]
        internal class NativeMethod
        {
            /// <summary>
            /// Creates or opens a named or unnamed file mapping object for a 
            /// specified file.
            /// </summary>
            /// <param name="hFile">
            /// A handle to the file from which to create a file mapping object.
            /// </param>
            /// <param name="lpAttributes">
            /// A pointer to a SECURITY_ATTRIBUTES structure that determines 
            /// whether a returned handle can be inherited by child processes.
            /// </param>
            /// <param name="flProtect">
            /// Specifies the page protection of the file mapping object. All 
            /// mapped views of the object must be compatible with this 
            /// protection.
            /// </param>
            /// <param name="dwMaximumSizeHigh">
            /// The high-order DWORD of the maximum size of the file mapping 
            /// object.
            /// </param>
            /// <param name="dwMaximumSizeLow">
            /// The low-order DWORD of the maximum size of the file mapping 
            /// object.
            /// </param>
            /// <param name="lpName">
            /// The name of the file mapping object.
            /// </param>
            /// <returns>
            /// If the function succeeds, the return value is a handle to the 
            /// newly created file mapping object.
            /// </returns>
            [DllImport("Kernel32.dll", SetLastError = true)]
            public static extern SafeFileMappingHandle CreateFileMapping(
                IntPtr hFile,
                ref SECURITY_ATTRIBUTES lpAttributes,
                FileProtection flProtect,
                uint dwMaximumSizeHigh,
                uint dwMaximumSizeLow,
                string lpName);


            /// <summary>
            /// Maps a view of a file mapping into the address space of a calling
            /// process.
            /// </summary>
            /// <param name="hFileMappingObject">
            /// A handle to a file mapping object. The CreateFileMapping and 
            /// OpenFileMapping functions return this handle.
            /// </param>
            /// <param name="dwDesiredAccess">
            /// The type of access to a file mapping object, which determines the 
            /// protection of the pages.
            /// </param>
            /// <param name="dwFileOffsetHigh">
            /// A high-order DWORD of the file offset where the view begins.
            /// </param>
            /// <param name="dwFileOffsetLow">
            /// A low-order DWORD of the file offset where the view is to begin.
            /// </param>
            /// <param name="dwNumberOfBytesToMap">
            /// The number of bytes of a file mapping to map to the view. All bytes 
            /// must be within the maximum size specified by CreateFileMapping.
            /// </param>
            /// <returns>
            /// If the function succeeds, the return value is the starting address 
            /// of the mapped view.
            /// </returns>
            [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr MapViewOfFile(
                SafeFileMappingHandle hFileMappingObject,
                FileMapAccess dwDesiredAccess,
                uint dwFileOffsetHigh,
                uint dwFileOffsetLow,
                uint dwNumberOfBytesToMap);


            /// <summary>
            /// Unmaps a mapped view of a file from the calling process's address 
            /// space.
            /// </summary>
            /// <param name="lpBaseAddress">
            /// A pointer to the base address of the mapped view of a file that 
            /// is to be unmapped.
            /// </param>
            /// <returns></returns>
            [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);
        }

        #endregion
    }
}


