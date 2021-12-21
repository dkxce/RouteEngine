/* 
 * C# Class by Milok Zbrozek <milokz@gmail.com>
 * Модуль создания маршрутного листа по графу
 * Author: Milok Zbrozek <milokz@gmail.com>
 * Версия: 13305C7
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

using dkxce.Route.GSolver;
using dkxce.Route.Classes;

namespace dkxce.Route.WayList
{
    [Serializable]
    public class TextDescriptions
    {
        public string S_FILEHEADER = "*** Маршрутный лист ***";
        public string S_BEGIN = "Начало маршрута";
        public string S_END = "Конец маршрута";
        public string S_ROAD = "Выезд на дорогу";
        public string S_ROAD_TO = "Выезд на {0}";
        public string S_ROAD_TM = "Выезд на дорогу через {0} м";
        public string S_ROAD_FM = "Двигайтесь {0} м до пункта назначения";
        public string S_FROAD = "Съезд с дороги";
        public string S_FROAD_T = "Выезд на дорогу с пунктом назначения";
        public string S_FROAD_TO = "Выезд на дорогу ({0}) с пунктом назначения";

        public string S_NCROSS = "Пересечение {0} и {1}";
        public string S_NTRANS = "Переход {0} в {1}";

        public string S_TURN_T_ROUNDABOUT = "Выезд на круговое движение";
        public string S_TURN_TO_ROUNDABOUT = "Выезд на круговое движение на {0}";

        public string S_TURN_F_ROUNDABOUT = "Съезд с кругового движения";
        public string S_TURN_FROM_ROUNDABOUT = "Съезд с кругового движения на {0}";

        public string S_M_ROUNDABOUT = "Двигайтесь по кругу";
        public string S_MOVE_ROUNDABOUT = "Двигайтесь по кругу, {0}";

        public string S_TURN_T = "Поверните {0}";
        public string S_TURN_TO = "Поверните {0} на {1}";
        public string S_UTURN = "Выполните разворот";
        public string S_MOVE_TOWARD = "Следуйте далее {0}";

        public string S_MOVE_KM = "Двигайтесь {1} км {0}";
        public string S_MOVEON = "по";

        public string S_W_ROUNDABOUT = "по кругу";
        public string S_W_LEFT = "налево";
        public string S_W_RIGHT = "направо";

        public string S_NONAME = "Дорог[E] без названия";
    }

    /// <summary>
    ///     Класс создания описания к маршруту   
    /// </summary>
    public class RouteDescription
    {        
        private RMGraph rs;
        private TextDescriptions td;

        public TextDescriptions Templates { get { return td; } set { td = value; } }

        /// <summary>
        ///     Создаем описание к маршруту
        /// </summary>
        /// <param name="GraphSolver"></param>
        public RouteDescription(RMGraph GraphSolver)
        {
            td = new TextDescriptions();
            string wlt = XMLSaved<int>.GetCurrentDir() + @"\WayListTemplates.xml";
            // XMLSaved<TextDescriptions>.Save(wlt,td);
            if (File.Exists(wlt)) td = XMLSaved<TextDescriptions>.Load(wlt);
            rs = GraphSolver;
        }

        /// <summary>
        ///     Создание описания маршрута
        /// </summary>
        /// <param name="Route">Маршрут</param>
        /// <returns>Ключевые точки с описанием</returns>
        public RDPoint[] GetDescription(RouteResult Route)
        {
            return GetDescription(Route,null,null);
        }

        /// <summary>
        ///     Создание описания маршрута
        /// </summary>
        /// <param name="Route">Маршрут</param>
        /// <returns>Ключевые точки с описанием</returns>
        public RDPoint[] GetDescription(PointFL[] Points)
        {
            RouteResult rr = new RouteResult();
            rr.vector = Points;
            rr.cost = rs.GetRouteCost(Points[0].node, Points[Points.Length - 1].node);
            rr.time = rs.GetRouteTime(Points[0].node, Points[Points.Length - 1].node);
            rr.length = rs.GetRouteDistance(Points[0].node, Points[Points.Length - 1].node);
            return GetDescription(rr);
        }

        public string[] GetRoadName(uint[] lines)
        {
            // Lines Names
            LinesNamesFileReader lnms = new LinesNamesFileReader(rs.FileName.Remove(rs.FileName.LastIndexOf(".")) + ".lines.txt");
            string[] res = new string[lines.Length];
            if (res.Length > 0)
                for (int i = 0; i < res.Length; i++)
                    if ((lines[i] != 0) && (lines[i] != uint.MaxValue))
                        try
                        {
                            res[i] = lnms[lines[i]];
                        }
                        catch { };
            lnms.Close();
            return res;
        }

        /// <summary>
        ///     Создание описания маршрута
        /// </summary>
        /// <param name="Route">Маршрут</param>
        /// <returns>Ключевые точки с описанием</returns>
        public RDPoint[] GetDescription(RouteResult Route, FindStartStopResult beforeRoute, FindStartStopResult afterRoute)
        {
            if ((Route.vector == null) || (Route.vector.Length == 0)) return new RDPoint[0];

            List<RDPoint> rdps = new List<RDPoint>();
            RDPoint rdp;

            // Lines Names
            LinesNamesFileReader lnms = new LinesNamesFileReader(rs.FileName.Remove(rs.FileName.LastIndexOf(".")) + ".lines.txt");

            float dop_dist = 0;
            float dop_time = 0;

            // LAST
            string last_line = "";
            double last_dist = 0;
            bool last_rnda = false;

            //CURRENT            
            float curr_angle = 0;
            string curr_line = "";
            double curr_dist = 0;
            bool curr_rnda = false;
            double curr_time = 0;

            if ((beforeRoute != null) && (beforeRoute.distToN > 1))
            {
                curr_line = lnms[beforeRoute.line];

                rdp = new RDPoint();
                rdp.Lat = beforeRoute.normal[0].Lat;
                rdp.Lon = beforeRoute.normal[0].Lon;
                rdp.dist = 0;
                rdp.time = 0;
                rdp.name = td.S_BEGIN;
                rdp.instructions =
                    beforeRoute.distToLine == 0 ? new string[] { td.S_BEGIN } : new string[] { String.Format(td.S_ROAD_TM, beforeRoute.distToLine.ToString("0")) };
                rdps.Add(rdp);

                if (beforeRoute.distToLine > 1)
                {
                    rdp = new RDPoint();
                    rdp.Lat = beforeRoute.normal[1].Lat;
                    rdp.Lon = beforeRoute.normal[1].Lon;
                    rdp.dist = beforeRoute.distToLine;
                    rdp.time = beforeRoute.distToLine / 700;
                    rdp.name = curr_line == "" ? td.S_ROAD : String.Format(td.S_ROAD_TO, curr_line);
                    rdp.instructions = new string[0];
                    if ((beforeRoute.normal.Length > 2) && (beforeRoute.distToLine > 2))
                    {
                        curr_angle = RMGraph.GetLinesTurnAngle(beforeRoute.normal[0], beforeRoute.normal[1], beforeRoute.normal[2]);
                        rdp.instructions = new string[] { String.Format(curr_line == "" ? td.S_TURN_T : td.S_TURN_TO, curr_angle < 0 ? td.S_W_LEFT : td.S_W_RIGHT, curr_line) };
                    }
                    else
                        rdp.instructions = new string[] { String.Format(td.S_MOVE_KM, (curr_line == "") ? "" : td.S_MOVEON + " " + curr_line, ((beforeRoute.distToN - beforeRoute.distToLine) / 1000).ToString("0.00").Replace(",", ".")) };
                    rdps.Add(rdp);

                    last_dist = rdp.dist;
                };

                dop_dist = beforeRoute.distToN;
                dop_time = beforeRoute.distToN / 700;
                last_line = curr_line;
            }
            else
            {
                curr_line = lnms[Route.vector[0].line];

                rdp = new RDPoint();
                rdp.Lat = Route.vector[0].Lat;
                rdp.Lon = Route.vector[0].Lon;
                rdp.dist = 0;
                rdp.time = 0;
                rdp.name = td.S_BEGIN;
                rdp.instructions = new string[] { td.S_BEGIN };
                rdps.Add(rdp);

                last_line = curr_line;
            };
            
            for (int i = 0; i < Route.vector.Length; i++) // угол поворота относительно текущей линии в следующую
                if (Route.vector[i].node > 0)
                {
                    // CURRENT
                    curr_angle = ((i > 0) && (i < (Route.vector.Length - 1))) ? RMGraph.GetLinesTurnAngle(Route.vector[i - 1], Route.vector[i], Route.vector[i + 1]) : 0;
                    if ((i == 0) && (beforeRoute != null))
                    {
                        if (beforeRoute.normal.Length > 1)
                            curr_angle = RMGraph.GetLinesTurnAngle(beforeRoute.normal[beforeRoute.normal.Length - 2], Route.vector[i], Route.vector[i + 1]);
                    };                    
                    curr_line = (i==0) ? "" : lnms[Route.vector[i].line];
                    if (rs.IsBeginSolveCalled)
                    {
                        curr_dist = i > 0 ? dop_dist + (rs.IsCalcReversed ? rs.GetRouteDistance(Route.vector[0].node, Route.vector[Route.vector.Length - 1].node) : 0) + (rs.IsCalcReversed ? -1 : 1) * rs.GetRouteDistance(Route.vector[i].node, Route.vector[i].node) - Route.shrinkStartLength : dop_dist;
                        curr_time = i > 0 ? dop_time + (rs.IsCalcReversed ? rs.GetRouteTime(Route.vector[0].node, Route.vector[Route.vector.Length - 1].node) : 0) + (rs.IsCalcReversed ? -1 : 1) * rs.GetRouteTime(Route.vector[i].node, Route.vector[i].node) - Route.shrinkStartTime : dop_time;
                    }
                    else
                    {
                        if (i == 0)
                        {
                            curr_dist = dop_dist;
                            curr_time = dop_time;
                        }
                        else
                        {
                            uint[] _n; float[] _c; float[] _d; float[] _t; uint[] _l; byte[] _r;
                            int near = rs.SelectNodeR(Route.vector[i].node, out _n, out _c, out _d, out _t, out _l, out _r);
                            for (int x = 0; x < near; x++)
                                if (_l[x] == Route.vector[i-1].line)
                                {
                                    curr_dist += _d[x];
                                    curr_time += _t[x];
                                    break;
                                };
                        };
                    };                   

                    // curr_city = lnms.City(Route.vector[i].line);                                        
                    curr_rnda = rs.GetLineFlags(Route.vector[i].line).IsRoundAbout;
                    

                    // NEW LINE or BIG ANGLE //
                    bool isNewLine = last_line != curr_line;
                    bool isBigAngle = ((Math.Abs(curr_angle) > 45) || (last_rnda != curr_rnda));
                    if (isNewLine || isBigAngle)
                    {
                        // PREV DRIVE KM
                        if (rdps.Count > 0)
                        {
                            RDPoint prev_rdp = rdps[rdps.Count - 1];
                            List<string> sl = new List<string>(prev_rdp.instructions);
                            sl.Add(String.Format(td.S_MOVE_KM, (last_rnda ? td.S_W_ROUNDABOUT + (last_line == "" ? "" : " (" + last_line + ")") : (last_line == "" ? "" : td.S_MOVEON + " " + last_line)), ((curr_dist - last_dist) / 1000).ToString("0.00").Replace(",", ".")));
                            prev_rdp.instructions = sl.ToArray();
                            rdps[rdps.Count - 1] = prev_rdp;
                        };

                        // NEW INSTRUCTION PART
                        rdp = new RDPoint();
                        rdp.Lat = Route.vector[i].Lat;
                        rdp.Lon = Route.vector[i].Lon;
                        rdp.dist = (float)curr_dist;
                        rdp.time = (float)curr_time;
                        rdp.name = "";
                        rdp.instructions = new string[0];                        
                        
                        // CROSSROAD
                        if (isNewLine)
                            rdp.name = String.Format(Math.Abs(curr_angle) > 15 ? td.S_NCROSS : td.S_NTRANS, last_line == "" ? td.S_NONAME.Replace("[E]", "и") : last_line, curr_line == "" ? td.S_NONAME.Replace("[E]", Math.Abs(curr_angle) > 15 ? "и" : "у") : curr_line);
                        
                        // TURN INFO
                        if (curr_rnda && (!last_rnda))
                            rdp.instructions = new string[] { String.Format(isNewLine ? td.S_TURN_TO_ROUNDABOUT : td.S_TURN_T_ROUNDABOUT, curr_line) };
                        else if (last_rnda && (!curr_rnda))
                            rdp.instructions = new string[] { String.Format(isNewLine ? td.S_TURN_FROM_ROUNDABOUT : td.S_TURN_F_ROUNDABOUT, curr_line) };
                        else if (curr_rnda && last_rnda)
                            rdp.instructions = new string[] { String.Format(isNewLine ? td.S_MOVE_ROUNDABOUT : td.S_M_ROUNDABOUT, curr_line) };
                        else if (Math.Abs(curr_angle) > 165)
                            rdp.instructions = new string[] { td.S_UTURN };
                        else if (Math.Abs(curr_angle) > 10)
                            rdp.instructions = new string[] { String.Format(curr_line == "" ? td.S_TURN_T : td.S_TURN_TO, curr_angle < 0 ? td.S_W_LEFT : td.S_W_RIGHT, curr_line) };
                        else if (isNewLine)
                            rdp.instructions = new string[] { String.Format(td.S_MOVE_TOWARD, td.S_MOVEON + " " + (curr_line == "" ? td.S_NONAME.Replace("[E]", "e") : curr_line)) };
                        
                        // Add Info //
                        rdps.Add(rdp);

                        // UPDATE LAST //
                        last_line = curr_line;
                        last_dist = curr_dist;
                        last_rnda = curr_rnda;
                    };
                };

            /////////////////////////////////////////////////////
            if ((afterRoute != null) && (afterRoute.distToN > 1))
            {
                // PREV DRIVE KM //
                if (rdps.Count > 0)
                {
                    double ddd = ((Route.length) - last_dist);
                    RDPoint prev_rdp = rdps[rdps.Count - 1];
                    List<string> sl = new List<string>(prev_rdp.instructions);
                    sl.Add(String.Format(td.S_MOVE_KM, (last_rnda ? td.S_W_ROUNDABOUT + (last_line == "" ? "" : " (" + last_line + ")") : (last_line == "" ? "" : td.S_MOVEON + " " + last_line)), (ddd / 1000).ToString("0.00").Replace(",", ".")));
                    prev_rdp.instructions = sl.ToArray();
                    rdps[rdps.Count - 1] = prev_rdp;
                };

                curr_angle = afterRoute.normal.Length > 1 ? RMGraph.GetLinesTurnAngle(Route.vector[Route.vector.Length - 2], Route.vector[Route.vector.Length - 1], afterRoute.normal[1]) : 0;
                curr_line = lnms[afterRoute.line];                

                rdp = new RDPoint(); bool skipadd = false;
                rdp.Lat = Route.vector[Route.vector.Length - 1].Lat;
                rdp.Lon = Route.vector[Route.vector.Length - 1].Lon;
                rdp.dist = Route.length - afterRoute.distToN;
                rdp.time = Route.time - afterRoute.distToN / 700;
                rdp.name = "";
                rdp.instructions = new string[0];
                if ((curr_line == "") || (curr_line != last_line))
                    rdp.name = curr_line == "" ? td.S_FROAD_T : String.Format(td.S_FROAD_TO, curr_line);                

                // CROSSROAD
                if (last_line != curr_line)
                    rdp.name = String.Format(Math.Abs(curr_angle) > 15 ? td.S_NCROSS : td.S_NTRANS, last_line == "" ? td.S_NONAME.Replace("[E]", "и") : last_line, curr_line == "" ? td.S_NONAME.Replace("[E]", Math.Abs(curr_angle) > 15 ? "и" : "у") : curr_line);

                // TURN INFO
                if (Math.Abs(curr_angle) > 20)
                    rdp.instructions = new string[]
                    {
                        String.Format(curr_line == "" ? td.S_TURN_T : td.S_TURN_TO, curr_angle < 0 ? td.S_W_LEFT : td.S_W_RIGHT, curr_line),
                        String.Format(td.S_MOVE_KM, (curr_line == "") ? ""  : td.S_MOVEON + " " + curr_line, ((afterRoute.distToN - afterRoute.distToLine) / 1000).ToString("0.00").Replace(",", "."))
                    };
                else if ((Math.Abs(curr_angle) > 165) || (Route.vector[Route.vector.Length - 1].line == afterRoute.line))
                    rdp.instructions = new string[]
                    {
                        String.Format(td.S_UTURN),
                        String.Format(td.S_MOVE_KM, (curr_line == "") ? ""  : td.S_MOVEON + " " + curr_line, ((afterRoute.distToN - afterRoute.distToLine) / 1000).ToString("0.00").Replace(",", "."))
                    };
                else if (curr_line != last_line)
                    rdp.instructions = new string[]
                    {
                        String.Format(td.S_MOVE_TOWARD, td.S_MOVEON + " " + (curr_line == "" ? td.S_NONAME.Replace("[E]","e") : curr_line)),
                        String.Format(td.S_MOVE_KM, (curr_line == "") ? ""  : td.S_MOVEON + " " + curr_line, ((afterRoute.distToN - afterRoute.distToLine) / 1000).ToString("0.00").Replace(",", "."))
                    };
                else if(rdps.Count > 0)
                { // продляем путь из предыдущей точки до следующей, пропуская эту
                    RDPoint prev_rdp = rdps[rdps.Count - 1];
                    List<string> sl = new List<string>(prev_rdp.instructions);
                    sl[sl.Count - 1] = String.Format(td.S_MOVE_KM, (last_rnda ? td.S_W_ROUNDABOUT + (last_line == "" ? "" : " (" + last_line + ")") : (last_line == "" ? "" : td.S_MOVEON + " " + last_line)), ((curr_dist - rdps[rdps.Count - 1].dist + afterRoute.distToN - afterRoute.distToLine) / 1000).ToString("0.00").Replace(",", "."));
                    prev_rdp.instructions = sl.ToArray();
                    rdps[rdps.Count - 1] = prev_rdp;
                    skipadd = true; 
                };
                //rdp.instructions = new string[]
                //{
                //    //String.Format(td.S_MOVE_KM, (curr_line == "") ? ""  : td.S_MOVEON + " " + curr_line, ((afterRoute.distToN - afterRoute.distToLine) / 1000).ToString("0.00").Replace(",", "."))
                //};                        

                if (!skipadd)
                {
                    rdps.Add(rdp);
                }

                if (afterRoute.distToLine > 1)
                {
                    rdp = new RDPoint();
                    rdp.Lat = afterRoute.normal[afterRoute.normal.Length - 2].Lat;
                    rdp.Lon = afterRoute.normal[afterRoute.normal.Length - 2].Lon;
                    rdp.dist = Route.length - afterRoute.distToLine;
                    rdp.time = Route.time - afterRoute.distToLine / 700;
                    rdp.name = td.S_FROAD;
                    rdp.instructions = new string[0];
                    if ((afterRoute.normal.Length > 2) && (afterRoute.distToLine > 2))
                    {
                        curr_angle = RMGraph.GetLinesTurnAngle(afterRoute.normal[afterRoute.normal.Length - 3], afterRoute.normal[afterRoute.normal.Length - 2], afterRoute.normal[afterRoute.normal.Length - 1]);
                        rdp.instructions = new string[] 
                        { 
                            String.Format(td.S_TURN_T, curr_angle < 0 ? td.S_W_LEFT : td.S_W_RIGHT),
                            String.Format(td.S_ROAD_FM, afterRoute.distToLine.ToString("0"))
                        };
                    };
                    rdps.Add(rdp);
                };

                rdp = new RDPoint();
                rdp.Lat = afterRoute.normal[afterRoute.normal.Length - 1].Lat;
                rdp.Lon = afterRoute.normal[afterRoute.normal.Length - 1].Lon;
                rdp.dist = Route.length;
                rdp.time = Route.time;
                rdp.name = td.S_END;
                rdp.instructions = new string[0];
                rdps.Add(rdp);
            }
            else // NO AFTER ROUTE
            {
                // PREV DRIVE KM
                if (rdps.Count > 0)
                {
                    double ddd = ((Route.length) - last_dist);
                    RDPoint prev_rdp = rdps[rdps.Count - 1];
                    List<string> sl = new List<string>(prev_rdp.instructions);
                    sl.Add(String.Format(td.S_MOVE_KM, (last_rnda ? td.S_W_ROUNDABOUT + (last_line == "" ? "" : " (" + last_line + ")") : (last_line == "" ? "" : td.S_MOVEON+" "+last_line)), (ddd / 1000).ToString("0.00").Replace(",", ".")));
                    prev_rdp.instructions = sl.ToArray();
                    rdps[rdps.Count - 1] = prev_rdp;
                };

                // NEW INSTRUCTION PART            
                rdp = new RDPoint();
                rdp.Lat = Route.vector[Route.vector.Length - 1].Lat;
                rdp.Lon = Route.vector[Route.vector.Length - 1].Lon;
                rdp.dist = Route.length;
                rdp.time = Route.time;
                rdp.name = td.S_END;
                rdp.instructions = new string[0];
                rdps.Add(rdp);
            };

            lnms.Close();

            // // // // // // //
            return rdps.ToArray();
        }

        /// <summary>
        ///     Создание файла описания маршрута (Маршрутного листа)   
        /// </summary>
        /// <param name="Route">Маршрут</param>
        /// <param name="FileName">Файл куда сохранять</param>
        public void SaveTextDescription(PointFL[] Points, string FileName)
        {
            SaveTextDescription(GetDescription(Points), FileName);
        }

        /// <summary>
        ///     Создание файла описания маршрута (Маршрутного листа)   
        /// </summary>
        /// <param name="Route">Маршрут</param>
        /// <param name="FileName">Файл куда сохранять</param>
        public void SaveTextDescription(RouteResult Route, string FileName)
        {
            SaveTextDescription(GetDescription(Route), FileName);
        }

        /// <summary>
        ///     Создание файла описания маршрута (Маршрутного листа)   
        /// </summary>
        /// <param name="Route">Маршрут</param>
        /// <param name="FileName">Файл куда сохранять</param>
        public void SaveTextDescription(RouteResult Route, FindStartStopResult beforeRoute, FindStartStopResult afterRoute, string FileName)
        {
            SaveTextDescription(GetDescription(Route,beforeRoute,afterRoute), FileName);
        }

        /// <summary>
        ///     Создание файла описания маршрута (Маршрутного листа)
        /// </summary>
        /// <param name="RouteDescription">Описание маршрута</param>
        /// <param name="FileName">Файл куда сохранять</param>
        public void SaveTextDescription(RDPoint[] RouteDescription, string FileName)
        {
            RDPoint[] rdps = RouteDescription;
            System.IO.FileStream fs = new FileStream(FileName, FileMode.Create);
            System.IO.StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8);
            sw.WriteLine("\t\t" + td.S_FILEHEADER);
            sw.WriteLine();
            for (int i = 0; i < rdps.Length; i++)
            {
                sw.WriteLine(String.Format("[{0}] {1} {2}; {3} мин", new object[] { i, rdps[i].Lat.ToString().Replace(",", "."), rdps[i].Lon.ToString().Replace(",", "."), rdps[i].time.ToString("0") }));
                
                if (rdps[i].name != "")
                    sw.WriteLine("\t" + rdps[i].name);

                string ins1 = rdps[i].name;
                if ((rdps[i].instructions != null) && (rdps[i].instructions.Length > 0))
                    ins1 = rdps[i].instructions[0];
                sw.WriteLine(String.Format("\t{0} км. {1}", (rdps[i].dist/1000).ToString("0.00").Replace(",", "."), ins1));

                if (rdps[i].instructions != null)
                    for (int x = 1; x < rdps[i].instructions.Length; x++)
                        sw.WriteLine("\t" + rdps[i].instructions[x]);
            };
            sw.Flush();
            fs.Flush();
            sw.Close();
            fs.Close();            
        }
    }
}
