using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;

using dkxce.Route.Classes;
using dkxce.Route.Shp2Rt;
using dkxce.Route.GSolver;

namespace RouteGraphCalcRG
{
    class Program
    {
        static void Main(string[] args)
        {
            if ((args == null) || (args.Length < 3))
            {
                Console.WriteLine("Модуль предварительного просчета маршрутов между RGNodes (межрегиональными) точками");
                Console.WriteLine("User syntax: RouteGraphCalcRG.exe %region_id% %route_filename% rgway_dir%");
                Console.WriteLine("Example: RouteGraphCreator.exe 11 DATA\\READY\\lipetsk.rt DATA\\READY\\RGWAY");
                System.Threading.Thread.Sleep(5000);
                return;
            };

            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " Загрузка графа в память");
            RMGraph gr = RMGraph.LoadToMemory(args[1], Convert.ToInt32(args[0]));
            gr.MinimizeRouteBy = MinimizeBy.Time;                        
            gr.CalculateRGNodesRoutes(Convert.ToInt32(args[0]),args[2]); // Расчет путей между точками стыковки межрайонных маршрутов

            gr.Close();            
        }
    }
}
