using System;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.ServiceProcess;
using System.Configuration.Install;
using System.ComponentModel;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;

namespace dkxce.Route.ServiceSolver
{
    public class RouteServer : ServiceBase
    {
        string configFile = null;
        bool pTCP = true;
        bool pRem = false;
        bool pHTTP = false;

        TcpChannel RouteSearcherChannel = null; // if use .Net Remoting
        RouteTCPListenter tcpServer = null;
        RouteHTTPListener httpServer = null;

        public string defHTTP = "";
        public string defTCP = "";
        public string defRem = "";
        public string ConfigFileInfo { get { return String.IsNullOrEmpty(this.configFile) ? "dkxce.Route.ServiceSolver.xml (Default)" : Path.GetFileName(configFile); } }

        public RouteServer() { }

        public void Start()
        {
            OnStart(null);
        }

        public void Start(string[] args)
        {
            OnStart(args);
        }

        private string ValidConfigFile(string[] args)
        {
            if (args == null) return null;
            if (args.Length == 0) return null;
            if (File.Exists(args[0])) return args[0];

            string f = args[0];
            f = Path.GetFullPath(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + @"\" + f);
            if (File.Exists(f)) return f;
            return null;
        }

        // cmd> sc delete dkxce.Route.ServiceSolver
        // cmd> sc create dkxce.Route.ServiceSolver binpath= "<filename> -param=5"
        protected override void OnStart(string[] args)
        {            
            // SVC CONFIG //
            SvcConfig scfg = !String.IsNullOrEmpty(this.configFile = ValidConfigFile(args)) ? SvcConfig.Load(this.configFile) : SvcConfig.Load();
            RouteThreader.mem.confile = this.ConfigFileInfo;
            RouteThreader.threadLog = scfg.threadLog;
            RouteThreader.threadLogMem = scfg.threadLogMem;
            RouteThreader.IPBanList = scfg.IPBanList;
            RouteThreader.authorization = scfg.authorization;
            RouteThreader.Users = scfg.Users;
            RouteThreader.UpMem();
            pTCP = scfg.ProtoTCP;
            pRem = scfg.ProtoRemoting;
            pHTTP = scfg.ProtoHTTP;

            if (pHTTP) defHTTP = String.Format("http://127.0.0.1:{0}/nms/",scfg.defHTTP);
            if (pTCP) defTCP = String.Format("tcp://127.0.0.1:{0}/", scfg.defPort);
            if (pRem) defRem = String.Format("remoting://127.0.0.1:{0}/", scfg.defPort);

            // on errors
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledException);

            // Max Thread Count In Client Pool
            int clientConnectionLimit = 1;
            if (scfg.IsMultiClient)
            {
                clientConnectionLimit = scfg.mucObjects + 1; // calcing + 1 wait
                if (scfg.fixThreads > 1) clientConnectionLimit = scfg.fixThreads;
            }
            else if (scfg.IsMultiThread)
            {
                clientConnectionLimit = Environment.ProcessorCount - 2;
                if (clientConnectionLimit < scfg.minThreads) clientConnectionLimit = scfg.minThreads;
                if (clientConnectionLimit > scfg.maxThreads) clientConnectionLimit = scfg.maxThreads;
                if (scfg.fixThreads > 1) clientConnectionLimit = scfg.fixThreads;
            };

            if (pRem)
            {
                //
                // if use .Net Remoting
                //
                // configure tcp channel
                BinaryServerFormatterSinkProvider provider = new BinaryServerFormatterSinkProvider();
                provider.TypeFilterLevel = TypeFilterLevel.Full;
                IDictionary RouteSearcherProps = new Hashtable();
                RouteSearcherProps["name"] = scfg.defChannel;
                RouteSearcherProps["port"] = scfg.defPort;
                RouteSearcherProps["clientConnectionLimit"] = clientConnectionLimit;
                RouteSearcherChannel = new TcpChannel(RouteSearcherProps, null, provider);
                //
                // Remoting config parameters
                // load configuration from `config.xml`
                // file example
                //<?xml version="1.0"?><configuration><system.runtime.remoting><application><channels><channel name="dkxce.Route.TCPSolver" ref="tcp" port="7755" clientConnectionLimit="8"></channel></channels></application></system.runtime.remoting></configuration>
                //if (File.Exists(configxml_file)) RemotingConfiguration.Configure(configxml_file);
                //
                // listen
                ChannelServices.RegisterChannel(RouteSearcherChannel, false);
            };

            RouteThreader.mem.config = scfg;

            string pass_to_log = "\r\n"; 
            pass_to_log += "dkxce.Route.ServiceSolver/" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + RouteHTTPListener._ServerTree + "\r\n";
            pass_to_log += RouteHTTPListener._Server + "\r\n";
            pass_to_log += "dkxce.Route.ServiceSolver.RouteThreader/" + System.Reflection.Assembly.GetAssembly(typeof(dkxce.Route.ServiceSolver.RouteThreader)).GetName().Version.ToString() + RouteHTTPListener._ServerTree + "\r\n";
            pass_to_log += "dkxce.Route.GSolver/" + System.Reflection.Assembly.GetAssembly(typeof(dkxce.Route.GSolver.RMGraph)).GetName().Version.ToString() + "\r\n";
            pass_to_log += RouteHTTPListener._ServerCustomHeaders;
            pass_to_log += "\r\n";
            pass_to_log += "Configuration: " + this.ConfigFileInfo + "\r\n";
            pass_to_log += "\r\n";

            string LogRecord = "";
            if (scfg.defArea == "multiple")
            {
                LogRecord += "Starting Service...\r\n";
                LogRecord += pass_to_log;
                if (pTCP)
                {
                    LogRecord += "Protocol: TCP" + (pHTTP ? ", HTTP" : "") + "\r\n";
                    RouteThreader.mem.Protocol = "TCP" + (pHTTP ? ", HTTP" : "");
                };
                if (pRem)
                {
                    LogRecord += "Protocol: Remoting\r\n";
                    RouteThreader.mem.Protocol = "Remoting";
                };
                if (pHTTP && (!pTCP))
                {
                    LogRecord += "Protocol: HTTP\r\n";
                    RouteThreader.mem.Protocol = "HTTP";
                };
                LogRecord += "Area: " + scfg.defArea + " [RussiaSolver]\r\n";
                LogRecord += "Mode: " + scfg.defMode + "\r\n";
                LogRecord += "Global Regions: " + (scfg.IsGlobalRegions.Length > 0 ? scfg.globalRegions : "none") + "\r\n";
                LogRecord += "Thread Regions: " + (scfg.IsThreadRegions.Length > 0 ? scfg.threadRegions : "none") + "\r\n";

                RouteThreader.mem.Area = scfg.defArea + " [RussiaSolver]";
                RouteThreader.mem.Mode = scfg.defMode;
                RouteThreader.mem.GlobalRegionsCache = (scfg.IsGlobalRegions.Length > 0 ? scfg.globalRegions : "none");
                RouteThreader.mem.ThreadRegionsCache = (scfg.IsThreadRegions.Length > 0 ? scfg.threadRegions : "none");

                LogRecord += RussiaSolver.PreloadGlobalCache(scfg.IsGlobalRegions, scfg.MultiRegion) + "\r\n";
                RussiaSolver._threadGraphsIds = scfg.IsThreadRegions;

                if (scfg.IsSingle)
                {
                    if (pTCP || pHTTP)
                    {
                        RouteThreader.IsDynamic = null;
                        RouteThreader.mem.DynamicPool = RouteThreader.IsDynamic != null;
                        RouteThreader.AddObject(new RussiaSolver(true));
                    };
                    if (pRem)
                    {
                        RemotingServices.Marshal(new RussiaSolver(true), "dkxce.Route.TCPSolver");
                        //RemotingConfiguration.RegisterWellKnownServiceType(typeof(RouteThreader), "dkxce.Route.TCPSolver", WellKnownObjectMode.Singleton);
                    };
                };
                if (scfg.IsMultiThread)
                {
                    LogRecord += "Max clients threads/objects count: " + clientConnectionLimit.ToString() + " (NoLP " + Environment.ProcessorCount.ToString() + ")\r\n";
                    RouteThreader.mem.ThreadsMaxAlive = clientConnectionLimit;
                    if (pTCP || pHTTP)
                    {
                        RouteThreader.IsDynamic = typeof(RussiaSolver);
                        RouteThreader.mem.DynamicPool = RouteThreader.IsDynamic != null;
                    };
                    if (pRem)
                    {
                        RemotingConfiguration.RegisterWellKnownServiceType(typeof(RussiaSolver), "dkxce.Route.TCPSolver", WellKnownObjectMode.SingleCall);
                    };
                };
                if (scfg.IsMultiClient)
                {
                    LogRecord += "Clients objects count: " + scfg.mucObjects.ToString() + "\r\n";
                    LogRecord += "Max clients threads count: " + clientConnectionLimit.ToString() + " (NoLP " + Environment.ProcessorCount.ToString() + ")\r\n";
                    RouteThreader.mem.ThreadsMaxAlive = clientConnectionLimit;
                    RouteThreader.IsDynamic = null;
                    RouteThreader.mem.DynamicPool = RouteThreader.IsDynamic != null;
                    for (byte i = 0; i < scfg.mucObjects; i++)
                        RouteThreader.AddObject(new RussiaSolver(true));
                    if (pRem)
                    {
                        RemotingConfiguration.RegisterWellKnownServiceType(typeof(RouteThreader), "dkxce.Route.TCPSolver", WellKnownObjectMode.SingleCall);
                    };
                };
            }
            else
            {
                LogRecord += "Starting Service...\r\n";
                LogRecord += pass_to_log;
                if (pTCP)
                {
                    LogRecord += "Protocol: TCP" + (pHTTP ? ", HTTP" : "") + "\r\n";
                    RouteThreader.mem.Protocol = "TCP" + (pHTTP ? ", HTTP" : "");
                };
                if (pRem)
                {
                    LogRecord += "Protocol: Remoting\r\n";
                    RouteThreader.mem.Protocol = "Remoting";
                };
                if (pHTTP && (!pTCP))
                {
                    LogRecord += "Protocol: HTTP\r\n";
                    RouteThreader.mem.Protocol = "HTTP";
                };
                LogRecord += "Area: " + scfg.defArea + " [OneRegionSolver]\r\n";
                LogRecord += "Mode: " + scfg.defMode + "\r\n";
                LogRecord += "Use Graph: " + System.IO.Path.GetFileName(scfg.OneRegion.Trim()) + "\r\n";

                RouteThreader.mem.Area = scfg.defArea + " [RussiaSolver]";
                RouteThreader.mem.Mode = scfg.defMode;
                RouteThreader.mem.GlobalRegionsCache = "none";
                RouteThreader.mem.ThreadRegionsCache = System.IO.Path.GetFileName(scfg.OneRegion.Trim());

                OneRegionSolver.GraphFile = scfg.OneRegion.Trim();

                if (scfg.IsSingle)
                {
                    if (pTCP || pHTTP)
                    {
                        RouteThreader.IsDynamic = null;
                        RouteThreader.mem.DynamicPool = RouteThreader.IsDynamic != null;
                        RouteThreader.AddObject(new OneRegionSolver(true));
                    };
                    if (pRem)
                    {
                        RemotingServices.Marshal(new OneRegionSolver(true), "dkxce.Route.TCPSolver");
                        //RemotingConfiguration.RegisterWellKnownServiceType(typeof(RouteThreader), "dkxce.Route.TCPSolver", WellKnownObjectMode.Singleton);
                    };
                };
                if (scfg.IsMultiThread)
                {
                    LogRecord += "Max clients threads/objects count: " + clientConnectionLimit.ToString() + " (NoLP " + Environment.ProcessorCount.ToString() + ")\r\n";
                    RouteThreader.mem.ThreadsMaxAlive = clientConnectionLimit;
                    RouteThreader.IsDynamic = typeof(OneRegionSolver);
                    RouteThreader.mem.DynamicPool = RouteThreader.IsDynamic != null;
                    if (pRem)
                    {
                        RemotingConfiguration.RegisterWellKnownServiceType(typeof(OneRegionSolver), "dkxce.Route.TCPSolver", WellKnownObjectMode.SingleCall);
                    };
                };
                if (scfg.IsMultiClient)
                {
                    LogRecord += "Clients objects count: " + scfg.mucObjects.ToString() + "\r\n";
                    LogRecord += "Max clients threads count: " + clientConnectionLimit.ToString() + " (NoLP " + Environment.ProcessorCount.ToString() + ")\r\n";
                    RouteThreader.mem.ThreadsMaxAlive = clientConnectionLimit;
                    RouteThreader.IsDynamic = null;
                    for (byte i = 0; i < scfg.mucObjects; i++)
                        RouteThreader.AddObject(new OneRegionSolver(true));
                    if (pRem)
                    {
                        RemotingConfiguration.RegisterWellKnownServiceType(typeof(RouteThreader), "dkxce.Route.TCPSolver", WellKnownObjectMode.SingleCall);
                    };
                };
            };

            if (pTCP)
            {
                tcpServer = new RouteTCPListenter(scfg.defPort, scfg.IsSingle ? 1 : clientConnectionLimit);
                tcpServer.Start();
            };
            if (pHTTP)
            {
                httpServer = new RouteHTTPListener(scfg.defHTTP, scfg.IsSingle ? 1 : clientConnectionLimit);
                httpServer.Start();
                LogRecord += "Listen at http://localhost:" + scfg.defHTTP.ToString() + "/\r\n";
            };

            if (pTCP || pRem)
                LogRecord += "Listen at " + string.Format("tcp://{0}:{1}/", "localhost", scfg.defPort.ToString()) + "\r\n";
            LogRecord += "Ready";

            XMLSaved<int>.Add2SysLog(StartupInfo = LogRecord);
        }
        public string StartupInfo = "";

        protected override void OnStop()
        {
            if (pTCP)
            {
                tcpServer.Stop();
            };
            if (pHTTP)
            {
                httpServer.Stop();
            };
            if (pRem)
            {
                ChannelServices.UnregisterChannel(RouteSearcherChannel);
                RouteSearcherChannel = null;
            };
        }

        void UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override object InitializeLifetimeService()
        {
            return null; // to make the object live indefinitely
        }

        public static void Install(bool undo, string[] args, string ServiceName)
        {
            try
            {
                Console.WriteLine(undo ? "Uninstalling service {0}..." : "Installing service {0}...", ServiceName);
                using (AssemblyInstaller inst = new AssemblyInstaller(typeof(Program).Assembly, args))
                {
                    IDictionary state = new Hashtable();
                    inst.UseNewContext = true;
                    try
                    {
                        if (undo)
                            inst.Uninstall(state);
                        else
                        {
                            inst.Install(state);
                            inst.Commit(state);
                        }
                    }
                    catch
                    {
                        try
                        {
                            inst.Rollback(state);
                        }
                        catch { }
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
            };
        }
    }    
}
