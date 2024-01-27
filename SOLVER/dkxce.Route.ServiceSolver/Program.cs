#define LOCK
#define AllowEmptyLock

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
    static class Program
    {        
        static void Main(string[] args)
        {
            bool isVirt = false;// SYSID._IsVirtual;

            string MachineID = "";

            #if LOCK
            if (!validMachine(out MachineID))
            {
                Console.WriteLine(MachineID);
                Console.WriteLine("Machine is not supported (1)");
                System.Threading.Thread.Sleep(1000);
                return;
            };
            #endif

            if (isVirt && (keyFileDays() >= 14)) // 2 weeks start limit
            {
                Console.WriteLine(MachineID);
                Console.WriteLine("Machine is not supported (2)");
                System.Threading.Thread.Sleep(1000);
                return;
            };
            
            if (isVirt || RunConsole(args, "dkxce.Route.ServiceSolver"))
            {
                RouteServer rs = new RouteServer();
                try
                {
                    rs.Start(args);
                    if (isVirt)
                    {
                        Console.WriteLine("RouteEngine: started at console (5 days)");
                        Conso1e.ReadLine(1000 * 60 * 60 * 24 * 5); // 5 days runtime limit
                    }
                    else
                    {
                        Console.WriteLine("RouteEngine: started at console");
                        Console.ReadLine();
                    };
                }
                catch (Exception ex) { Console.WriteLine("RouteEngine: Launch Error: " + ex.Message); };
                try { rs.Stop(); } catch { };
                Console.WriteLine("RouteEngine: stopped");
                return;
            };
        }

        static bool RunConsole(string[] args, string ServiceName)
        {
            if (!Environment.UserInteractive) // As Service
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[] { new RouteServer() };
                ServiceBase.Run(ServicesToRun);
                return false;
            };

            if ((args == null) || (args.Length == 0))
                return true;

            switch (args[0])
            {
                case "-i":
                case "/i":
                case "-install":
                case "/install":
                    RouteServer.Install(false, args, ServiceName);
                    return false;
                case "-u":
                case "/u":
                case "-uninstall":
                case "/uninstall":
                    RouteServer.Install(true, args, ServiceName);
                    return false;
                case "-start":
                case "/start":
                    {
                        Console.WriteLine("Starting service {0}...", ServiceName);
                        ServiceController service = new ServiceController(ServiceName);
                        service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(5));
                        Console.WriteLine("Service {0} is {1}", ServiceName, service.Status.ToString());
                        return false;
                    };
                case "-stop":
                case "/stop":
                    {
                        Console.WriteLine("Starting service {0}...", ServiceName);
                        ServiceController service = new ServiceController(ServiceName);
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                        Console.WriteLine("Service {0} is {1}", ServiceName, service.Status.ToString());
                        return false;
                    };
                case "-restart":
                case "/restart":
                    {
                        Console.WriteLine("Starting service {0}...", ServiceName);
                        ServiceController service = new ServiceController(ServiceName);
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                        Console.WriteLine("Service {0} is {1}", ServiceName, service.Status.ToString());
                        service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(5));
                        Console.WriteLine("Service {0} is {1}", ServiceName, service.Status.ToString());
                        return false;
                    };
                case "-status":
                case "/status":
                    {
                        ServiceController service = new ServiceController(ServiceName);
                        Console.WriteLine("Service {0} is {1}", ServiceName, service.Status.ToString());
                        return false;
                    };
                case "/vip":
                    {
                        RouteServer rs = new RouteServer();
                        rs.Start(args);
                        Console.WriteLine("RouteEngine: started at console (vip)");
                        Console.ReadLine();
                        try { rs.Stop(); }
                        catch { };
                        Console.WriteLine("RouteEngine: stopped");
                        return false;
                    };
                default:
                    Console.WriteLine("Usage:");
                    Console.WriteLine("  dkxce.Route.ServiceSolver.exe    - running console");
                    Console.WriteLine("  dkxce.Route.ServiceSolver.exe /i   - install service");
                    Console.WriteLine("  dkxce.Route.ServiceSolver.exe /u   - uninstall service");
                    Console.WriteLine("  dkxce.Route.ServiceSolver.exe /start   - start service");
                    Console.WriteLine("  dkxce.Route.ServiceSolver.exe /stop   - stop service");
                    Console.WriteLine("  dkxce.Route.ServiceSolver.exe /restart   - restart service");
                    Console.WriteLine("  dkxce.Route.ServiceSolver.exe /status   - service status");
                    System.Threading.Thread.Sleep(1000);
                    return false;
            };
        }

        public static byte[] MACHINEID = new byte[] { 0x72, 0x65, 0x67, 0x62, 0x6b, 0x62, 0x64, 0x76, 0x66, 0x75, 0x66, 0x70, 0x62, 0x79, 0x74, 0x68, 0x74, 0x70, 0x62, 0x79, 0x6a, 0x64, 0x65, 0x2e, 0x70, 0x62, 0x79, 0x65, 0x3E, 0x3E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x3C, 0x3C };

        static bool validMachine(out string Machine_ID)
        {            
            string ccode = "dkxce.Route.ServiceSolver.exe";
            string HASH1 = Machine_ID = SYSID.GetSystemID(true, true, false, true, false);
            string HASH2 = SYSID.Encrypt(HASH1, ccode);
            string HASH3 = "";
            try
            {
                int sum = 0;
                HASH3 = "";
                int x = 0;
                for (int i = 0; i < MACHINEID.Length; i++) if (MACHINEID[i] == 0) { x = i; break; };
                #if AllowEmptyLock
                for (int i = 30; i < MACHINEID.Length; i++) sum += MACHINEID[i];
                if (sum == 120) return true;
                #endif
                HASH3 = System.Text.Encoding.ASCII.GetString(MACHINEID, 30, x - 30);
                //return true;
            }
            catch { return false; };
            string HASH4 = "";
            try
            {
                HASH4 = SYSID.Decrypt(HASH3, ccode);
            }
            catch { };
            return (HASH1 == HASH4);
        }

        static int keyFileDays()
        {
            try
            {
                //DateTime dt = System.IO.File.GetLastWriteTimeUtc(AppDomain.CurrentDomain.BaseDirectory + @"dkxce.Route.Biosid.dll");
                DateTime dt = System.IO.File.GetLastWriteTimeUtc(AppDomain.CurrentDomain.BaseDirectory + @"dkxce.Route.ServiceSolver.exe");                
                return (int)(DateTime.UtcNow.Subtract(dt)).TotalDays;
            }
            catch { };
            return int.MaxValue;
        }
    }    

    [RunInstaller(true)]
    public sealed class MyServiceInstallerProcess : ServiceProcessInstaller
    {
        public MyServiceInstallerProcess()
        {
            this.Account = ServiceAccount.NetworkService;
            this.Username = null;
            this.Password = null;
        }
    }

    [RunInstaller(true)]
    public sealed class MyServiceInstaller : ServiceInstaller
    {
        public MyServiceInstaller()
        {
            this.Description = "nms Сервис построения маршрута dkxce.Route";
            this.DisplayName = "dkxce.Route.ServiceSolver";
            this.ServiceName = "dkxce.Route.ServiceSolver";
            this.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
        }
    }

    class Conso1e
    {
        private static Thread inputThread;
        private static AutoResetEvent getInput, gotInput;
        private static string input;

        static Conso1e()
        {
            getInput = new AutoResetEvent(false);
            gotInput = new AutoResetEvent(false);
            inputThread = new Thread(reader);
            inputThread.IsBackground = true;
            inputThread.Start();
        }

        private static void reader()
        {
            while (true)
            {
                getInput.WaitOne();
                input = Console.ReadLine();
                gotInput.Set();
            }
        }

        public static string ReadLine(int timeOutMillisecs)
        {
            getInput.Set();
            bool success = gotInput.WaitOne(timeOutMillisecs);
            if (success)
                return input;
            else
                throw new System.TimeoutException("User did not provide input within the timelimit.");
        }
    }
}