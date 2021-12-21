using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;

using dkxce.Route.Classes;
using dkxce.Route.Shp2Rt;

namespace RouteGraphCreator
{
    class Program
    {
        static void Main(string[] args)
        {
            if ((args == null) || (args.Length < 3))
            {
                Console.WriteLine("Модуль создания графа из shape файла дорог");
                Console.WriteLine("User syntax: RouteGraphCreator.exe %region_id% %shape_filename% %route_filename%");
                Console.WriteLine("User syntax: RouteGraphCreator.exe %region_id% %shape_filename% %route_filename% attr");
                Console.WriteLine("Example: RouteGraphCreator.exe 11 DATA\\Roads_Lipetsk.shp DATA\\READY\\lipetsk.rt");
                Console.WriteLine("Example: RouteGraphCreator.exe 11 DATA\\Roads_Lipetsk.shp DATA\\READY\\lipetsk.rt attr");
                System.Threading.Thread.Sleep(5000);
                return;
            };

            string shpf = args[1];
            ShpToGraphConverter con = new ShpToGraphConverter(shpf);
            con.RegionID = Convert.ToInt32(args[0]);
            con.WriteLinesNamesFile = true;
            string rName = null;
            if (args.Length > 3)
            {
                for (int i = 3; i < args.Length; i++)
                {
                    if (args[i] == "attr")
                        con.analyse_attributes_do = true;
                    if (args[i].StartsWith("/regName="))
                        rName = args[i].Substring(9).Trim();
                };
            };
            con.ConvertTo(args[2], rName);
        }
    }
}
