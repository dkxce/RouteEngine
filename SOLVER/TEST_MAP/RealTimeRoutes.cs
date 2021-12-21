/* 
 * C# Class by Milok Zbrozek <milokz@gmail.com>
 * Модули для рассчета маршрутов по графам, 
 * используя алгоритмы Дейкстры и A*
 * в памяти и через дисковые файлы,
 * с поддержкой расчета с учетом трафика
 * Author: Milok Zbrozek <milokz@gmail.com>
 * Версия: 13305C5
 */

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace dkxce.RealTimeRoutes
{
    /*
     * FILES IN GRAPH:
     * lines - region.lines.bin - LINES info (node1,node2,segments,oneway)
     *       - region.lines.id - LINK_ID for LINES
     * segm  - region.segments.bin - LINE SEGMENTS
     * graph - region.graph.bin - GRAPH NODE INFOS
     *       - region.graph.bin.in - INDEX FOR NODE POS IN GRAPH
     *       - region.graph[r].bin - GRAPH NODE INFOS (FOR INVERSE SOLVE ALGORITHM)
     *       - region.graph[r].bin.in - INDEX FOR NODE POS IN GRAPH (FOR INVERSE SOLVE ALGORITHM)
     *       - region.graph.geo - NODES LAT LON
     *       - region.graph.geo.ll - INDEXED LAT LON OF NODES     
     * ways  - region.rgnodes.xml - информация о точках межрайонных маршрутов
     * info  - region.analyze.txt - информация об ошибках в узлах
     * 
     * 
     * lines file: (lines.bin)
     *   RMLINES, count[int], 
     *   RECORDS: 2 + 4 + 1 + 4 + 4 = 15
     *      segments_count[word]; 2
     *      pos[int]; 4
     *      oneway[byte]; 1
     *      node1[uint]; 4
     *      node2[uint]; 4
     * 
     * lines segments file: (segments.bin)
     *   RMSEGMENTS, count[int], 
     *   RECORDS: 4 + 2 + 4 + 4 + 4 + 4 + 4 + 4 + 7 = 30 bytes
     *      line_no[uint]; 4
     *      segment[word]; 2
     *      lat0[single]; 4
     *      lon0[single]; 4
     *      lat1[single]; 4
     *      lon1[single]; 4
     *      k[single]; 4
     *      b[single]; 4
     * 
     * Lines link ids (lines.id)
     *  RMLINKIDS, count[int], // lines count
     *  array[1..LINES] of LINK_ID[int]
     * 
     * *
     * * Lines tmc file (lines.tmc) // creates for making the traffic.costs & traffic.times files)
     * * RMTMC, count[int] // lines count
     * *  array[1..LINES] of 
     * *      reversed[byte]; 1
     * *      tmc_code[ushort]; 2
     * * 
     * * Lines Costs External File (Traffic) -- not impemented yet // файл поправок оценки для расчета маршрутов (трафик)
     * * (traffic.costs & traffic.times) -- должен создаваться во внешнем приложении, анализируя файл lines.tmc
     * *  RMEXTCOSTS, count[int] // lines count
     * *  array[1..LINES] of 
     * *      normal_cost[single]; 4
     * *      inverse_cost[single]; 4
     * *
     * 
     * Graph file: (graph.bin)
     *  RMGRAF2, count[int], max length between nodes[single]
     *  RECORDS:
     *       next_nodes_count[byte]; 1
     *       IF(count>0) array[0..count-1] of next_nodes_count elements
     *       ARRAY ELEMENT: 4 + 4 + 4 + 4 + 4 = 21
     *         next[uint]; 4
     *         cost[single]; 4
     *         dist[single]; 4
     *         time[single]: 4
     *         line[uint]: 4
     *         inverseDirection[byte]: 1
     * 
     * Graph Index: (graph.bin.in)
     *  RMINDEX, count[int],
     *  RECORDS:
     *      pos_in_graph_file[uint]; 4
     * 
     * Graph geo: (graph.geo)
     *  RMPOINTNLL0, count[int]
     *  RECORDS: 4 + 4 = 8
     *      lat[float]; 4
     *      lon[float]; 4
     * 
     * Graph geo lat lon: (graph.geo.ll)
     *  RMPOINTNLL1, count[int]
     *  RECORDS: 4 + 4 + 4 = 12
     *      node[uint];4
     *      lat[float];4
     *      lon[float];4
     * 
     */

    /// <summary>
    ///     Точка XY
    /// </summary>
    [Serializable]
    public struct PointF
    {
        [XmlAttribute("x")]
        public float X;
        [XmlAttribute("y")]
        public float Y;
        public PointF(float X, float Y)
        {
            this.X = X;
            this.Y = Y;
        }
        public override string ToString()
        {
            return "X = " + X.ToString() + " Y = " + Y.ToString();
        }
        public float Lat { get { return Y; } }
        public float Lon { get { return X; } }
    }

    /// <summary>
    ///     Точка XY с привязкой к линии и узлу
    ///     (исп-ся для описания маршрута)
    /// </summary>
    [Serializable]
    public struct PointFL
    {
        [XmlAttribute("x")]
        public float X;
        [XmlAttribute("y")]
        public float Y;
        /// <summary>
        ///     Номер линии, на котором находится точка
        /// </summary>
        [XmlAttribute("l")]
        public uint line;
        /// <summary>
        ///     Номер узла, с которого начинается полилиния
        ///     (указывает на съезд с одной линии на другую)
        /// </summary>
        [XmlAttribute("n")]
        public uint node;
        /// <summary>
        ///     Содание точки с привязкой
        /// </summary>
        /// <param name="point">Координаты точки</param>
        /// <param name="node">Номер линии</param>
        /// <param name="line">Номер узла</param>
        public PointFL(PointF point, uint node, uint line)
        {
            this.X = point.X;
            this.Y = point.Y;
            this.node = node;
            this.line = line;
        }
        public override string ToString()
        {
            return "X = " + X.ToString()+"; Y = "+Y.ToString()+ "; N = " + node.ToString() + "; L = " + line.ToString();
        }
        public float Lat { get { return Y; } }
        public float Lon { get { return X; } }
    }

    /// <summary>
    ///     Результат расчета маршрута
    /// </summary>
    [Serializable]
    public struct RouteResult
    {
        /// <summary>
        ///     Оценка маршрута
        /// </summary>
        public float cost;
        /// <summary>
        ///     Длина маршрута (м)
        /// </summary>
        public float length;
        /// <summary>
        ///     Время в пути (м)
        /// </summary>
        public float time;
        /// <summary>
        ///     Узел старта
        /// </summary>
        public uint nodeStart;
        /// <summary>
        ///     Промежуточные узлы
        /// </summary>
        [XmlArrayItem("n")]
        public uint[] nodes;
        /// <summary>
        ///     Узел финиша
        /// </summary>
        public uint nodeEnd;
        /// <summary>
        ///     Линии движения (ребра)
        /// </summary>
        [XmlArrayItem("l")]
        public uint[] lines;
        /// <summary>
        ///     Вектор перемещения
        /// </summary>
        [XmlArrayItem("p")]
        public PointFL[] vector;
        /// <summary>
        ///     Урезан ли разворот вначале
        /// </summary>
        public bool shrinkStart;
        /// <summary>
        ///     Урезан ли разворот в конце
        /// </summary>
        public bool shrinkEnd;
    }

    /// <summary>
    ///     Результат расчета маршрута вместе с
    ///     оценками, расстояниями и временами для сохранения
    /// </summary>
    [Serializable]
    public struct RouteResultStored
    {
        /// <summary>
        ///     Маршрут
        /// </summary>
        [XmlElement("route")]        
        public RouteResult route;
        /// <summary>
        ///     Расстояния для всех узлов маршрута,
        ///     включая первый и последний
        /// </summary>
        [XmlArray("distances")]
        [XmlArrayItem("d")]
        public float[] distances;
        /// <summary>
        ///     Времена прохождения всех узлов маршрута,
        ///     включая первый и последний
        /// </summary>
        [XmlArray("times")]
        [XmlArrayItem("t")]
        public float[] times;
        /// <summary>
        ///     Оценки прохождения всех узлов маршрута,
        ///     включая первый и последний
        /// </summary>
        [XmlArray("costs")]
        [XmlArrayItem("c")]
        public float[] costs;

        public RouteResultStored(RouteResult route, float[] distances, float[] times, float[] costs)
        {
            this.route = route;
            this.distances = distances;
            this.times = times;
            this.costs = costs;
        }
    }

    /// <summary>
    ///     Узел графа
    /// </summary>
    public class TNode
    {
        /// <summary>
        ///     Идентификатор (номер узла) (нумерация с 1)
        /// </summary>
        public uint node;
        /// <summary>
        ///     Широта в град
        /// </summary>
        public float lat;
        /// <summary>
        ///     Долгота в град
        /// </summary>
        public float lon;
        /// <summary>
        ///     Исходящие связи (ребра)
        /// </summary>
        public List<TLink> links = new List<TLink>();
        /// <summary>
        ///     Входящие связи (ребра)
        /// </summary>
        public List<TLink> rlinks = new List<TLink>();

        /// <summary>
        ///     Создаем узел
        /// </summary>
        /// <param name="node">Номер</param>
        /// <param name="lat">Широта</param>
        /// <param name="lon">Долгота</param>
        public TNode(uint node, float lat, float lon)
        {
            this.node = node;
            this.lat = lat;
            this.lon = lon;
        }

        /// <summary>
        ///     Число выходящих связей (ребер)
        /// </summary>
        public int Outs { get { return links.Count; } }

        /// <summary>
        ///     Число входящих связей (ребер)
        /// </summary>
        public int Ins { get { return rlinks.Count; } }

        /// <summary>
        ///     N Y X I O    
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "{N" + node + "; Y" + lat.ToString() + " X" + lon.ToString() + " I" + Ins.ToString() + " O"+Outs.ToString() + "}";
        }

        /// <summary>
        ///     Добавляем исходящую связь (ребро)
        /// </summary>
        /// <param name="node">узел</param>
        /// <param name="cost">оценка</param>
        /// <param name="dist">расстояние (м)</param>
        /// <param name="time">время (мин)</param>
        /// <param name="line">линия</param>
        /// <param name="line_rev">линия в обратную сторону?</param>
        public void AddLink(uint node, float cost, float dist, float time, uint line, bool line_rev)
        {
            this.links.Add(new TLink(node, cost, dist, time, line, line_rev));
        }

        /// <summary>
        ///     Добавляем входящую связь (ребро)
        /// </summary>
        /// <param name="node">узел</param>
        /// <param name="cost">оценка</param>
        /// <param name="dist">расстояние (м)</param>
        /// <param name="time">время (мин)</param>
        /// <param name="line">линия</param>
        /// <param name="line_rev">линия в обратную сторону?</param>
        public void AddRLink(uint node, float cost, float dist, float time, uint line, bool line_rev)
        {
            this.rlinks.Add(new TLink(node, cost, dist, time, line, line_rev));
        }

        /// <summary>
        ///     Сортировщик узлов (для индексации)
        /// </summary>
        public class Sorter : IComparer<TNode>
        {
            private byte sortBy = 0;
            private Sorter(byte sortBy) { this.sortBy = sortBy; }
            
            /// <summary>
            ///     Сортируем по идентификатору
            /// </summary>
            /// <returns></returns>
            public static Sorter SortByNode() { return new Sorter(0); }
            
            /// <summary>
            ///     Сортируем по координатам
            /// </summary>
            /// <returns></returns>
            public static Sorter SortByLL() { return new Sorter(1); }

            /// <summary>
            ///     Сортируем два уэелемнта массива
            /// </summary>
            /// <param name="a">1-ый элемент</param>
            /// <param name="b">2-ой элемент</param>
            /// <returns></returns>
            public int Compare(TNode a, TNode b)
            {
                if (sortBy == 0) return a.node.CompareTo(b.node);

                int res = a.lat.CompareTo(b.lat);
                if (res == 0) res = a.lon.CompareTo(b.lon);
                if (res == 0) res = a.node.CompareTo(b.node);
                return res;
            }
        }
    }

    /// <summary>
    ///     Узел с информацией о расстоянии
    ///     от указанной точки до текущей
    /// </summary>
    public class TNodeD : TNode
    {        
        /// <summary>
        ///     Расстояние (м)
        /// </summary>
        public float dist;

        public TNodeD(uint node, float lat, float lon, float dist)
            : base(node, lat, lon)
        {
            this.dist = dist;
        }

        public TNodeD(TNode node)
            : base(node.node, node.lat, node.lon)
        {
            this.dist = 0;
        }

        public TNodeD(TNode node, float dist)
            : base(node.node, node.lat, node.lon)
        {
            this.dist = dist;
        }

        public class DSorter : IComparer<TNodeD>
        {
            public int Compare(TNodeD a, TNodeD b)
            {
                int res = a.dist.CompareTo(b.dist);
                if (res == 0) res = a.node.CompareTo(b.node);
                return res;
            }
        }
    }

    /// <summary>
    ///     Связь между узлами (ребро)
    /// </summary>
    public class TLink
    {
        /// <summary>
        ///     узел связи
        /// </summary>
        public uint node;

        /// <summary>
        ///     оценка
        /// </summary>
        public float cost;

        /// <summary>
        ///     расстояние (м)
        /// </summary>
        public float dist;

        /// <summary>
        ///     время (мин)
        /// </summary>
        public float time;

        /// <summary>
        ///     линия
        /// </summary>
        public uint line;

        /// <summary>
        ///     чтобы добраться в указанную точку нужно
        ///     проехать по линии в обратную сторону
        /// </summary>
        public bool inverse_dir;

        /// <summary>
        ///     Создаем связь
        /// </summary>
        /// <param name="node">связующий узел</param>
        /// <param name="cost">оценка передвижения</param>
        /// <param name="dist">расстояние в м</param>
        /// <param name="time">время в мин</param>
        /// <param name="line">номер линии</param>
        /// <param name="inverse_dir">в обратном направлении</param>
        public TLink(uint node, float cost, float dist, float time, uint line, bool inverse_dir)
        {
            this.node = node;
            this.cost = cost;
            this.dist = dist;
            this.time = time;
            this.line = line;
            this.inverse_dir = inverse_dir;
        }

        public override string ToString()
        {
            return node.ToString() + " " + dist.ToString() + "m " + time.ToString() + "min [line: " + line + (inverse_dir ? "R" : "") + "]";
        }
    }

    /// <summary>
    ///     Информация о поправке оценки для CallExternalCostService
    /// </summary>
    public struct LineExtCostInfo
    {
        /// <summary>
        ///     Поправка для прямого направления
        /// </summary>
        public float costN;

        /// <summary>
        ///     Поправка для обратного направления
        /// </summary>
        public float costR;

        /// <summary>
        ///     
        /// </summary>
        /// <param name="costN">Поправка для прямого направления</param>
        /// <param name="costR">Поправка для обратного направления</param>
        public LineExtCostInfo(float costN, float costR)
        {
            this.costN = costN;
            this.costR = costR;
        }

        public override string ToString()
        {
            return "N" + costN.ToString() + " R" + costR.ToString();
        }
    }

    /// <summary>
    ///     Результат поиска стартовой/конечной точки по координатам
    /// </summary>
    public struct FindStartStopResult
    {
        /// <summary>
        ///     Вектор от точки до узла конца линии (для end - начала)
        /// </summary>
        public PointF[] normal;
        
        /// <summary>
        ///     Вектор от точки до узла начала линии (для end - конца)
        /// </summary>
        public PointF[] revers;
        
        /// <summary>
        ///     расстояние от точки до линии
        /// </summary>
        public float distToLine;
        
        /// <summary>
        ///     расстояние от точки до узла конца линии (для end - начала)
        /// </summary>
        public float distToN;
        
        /// <summary>
        ///     расстояние от точки до узла начала линии (для end - конца)
        /// </summary>
        public float distToR;
        
        /// <summary>
        ///     ближайшая линия
        /// </summary>
        public uint line;

        /// <summary>
        ///     Стартовый узел (start - normal - last_point; end - normal - first_point)
        /// </summary>
        public uint nodeN;

        /// <summary>
        ///     Номер узла на другом конце линии
        /// </summary>
        public uint nodeR;

        public override string ToString()
        {
            return " N" + nodeN.ToString() + ", " + distToN.ToString() + " м, L" + line.ToString();
        }
    }

    /// <summary>
    ///     Оптимизировать маршрут по
    /// </summary>
    public enum MinimizeBy : byte 
    { 
        /// <summary>
        ///     По оценке (исп-ся только для Дейстры)
        /// </summary>        
        Cost = 0, 

        /// <summary>
        ///     По расстоянию
        /// </summary>
        Dist = 1,

        /// <summary>
        ///     По времени
        /// </summary>
        Time = 2
    }

    //// /// // / // /// //// /// // / // /// ////
    //// /// // / // /// //// /// // / // /// ////
    //// /// // / // /// //// /// // / // /// ////

    /// <summary>
    ///     Информация о полях shape файла
    /// </summary>
    [Serializable]
    public class ShapeFields: XMLSaved<ShapeFields>
    {
        /// <summary>
        ///     Поле указывающее на одностороннее движение 0/1,
        ///     поле не может быть пустым
        /// </summary>
        [XmlElement("OneWay")]
        public string fldOneWay = "ONE_WAY";
        /// <summary>
        ///     Поле с ограничением максимальной скорости (км/ч),
        ///     поле не может быть пустым
        /// </summary>
        [XmlElement("SpeedLimit")]
        public string fldSpeedLimit = "SPD_LIMIT";
        /// <summary>
        ///     Поле с типом линии,
        ///     поле не может быть пустым
        /// </summary>
        [XmlElement("GarminType")]
        public string fldGarminType = "GRMN_TYPE";
        /// <summary>
        ///     Поле с идентификатором линии,
        ///     используется для запретов поворотов,
        ///     поле не может быть пустым
        /// </summary>
        [XmlElement("LinkId")]
        public string fldLinkId = "LINK_ID";
        /// <summary>
        ///     Длина линии в метрах,
        ///     если не указано, то длина расчитывается
        /// </summary>
        [XmlElement("Length")]
        public string fldLength = "LEN";
        /// <summary>
        ///     Format: L0000;F0000;
        ///     F0000 - from first line point to link_id 0000
        ///     L0000 - from last line point to link_id 0000
        /// </summary>
        [XmlElement("TurnRestrictions")]
        public string fldTurnRstr = "TURN_RSTRS";
        /// <summary>
        ///     Кодировка DBF файла (1251/1252/866)
        /// </summary>
        [XmlElement("CodePageId")]
        public int CodePageId = 1251;
        /// <summary>
        ///     Инверсировать ли широту ( Lat = -1*Lat )
        /// </summary>
        [XmlElement("InverseLat")]
        public bool InverseLat = false;
        /// <summary>
        ///     Инверсировать ли долготу ( Lon = -1*Lon )
        /// </summary>
        [XmlElement("InverseLon")]
        public bool InverseLon = false;
        /// <summary>
        ///     Поле с информацией о TMC коде
        ///     @E0+000+00000;@E0-000-00000;
        ///     @E0+000+00000 or @E0-000-00000 (TMC сонаправлен с линией)
        ///     @E0+000-00000 or @E0-000+00000 (ТМС противоположенно направлен линии)
        ///     Max TMC no: 65535 (2 bytes)
        /// </summary>
        [XmlElement("TMC")]
        public string fldTMC = null;
        /// <summary>
        ///     Поле с номером точки стыковки межрайонных маршрутов
        ///     формат: F0001IO / L0002I / F0003O; 
        ///     I (In) - через этот узел можно въехать в регион; 
	    ///     O (out) - через этот узел можно выехать из региона
        /// </summary>
        [XmlElement("RGNODE")]
        public string fldRGNODE = null;
        /// <summary>
        ///     Поле с наименованием линии
        /// </summary>
        [XmlElement("NAME")]
        public string fldName = null;


        /// <summary>
        ///     Обозначены ли запреты поворотов
        /// </summary>
        public bool NoTurnRestrictions { get { return (fldTurnRstr == null) || (fldTurnRstr == String.Empty); } }

        /// <summary>
        ///     Кодировка DBF файла
        /// </summary>
        public System.Text.Encoding CodePage { get { return System.Text.Encoding.GetEncoding(CodePageId); } }
    }

    /// <summary>
    ///     Точка стыковки межрайонных маршрутов
    /// </summary>
    [Serializable]
    public struct TRGNode
    {
        /// <summary>
        ///     Идентификатор межрайонного узла
        /// </summary>
        [XmlAttribute("id")]
        public int id;

        /// <summary>
        ///     Номер узла в графе
        /// </summary>
        [XmlAttribute("node")]
        public uint node;

        /// <summary>
        ///     Точка поддерживает вход из других регионов
        /// </summary>
        [XmlAttribute("in")]
        public bool inner;

        /// <summary>
        ///     Точка поддерживает выход в другие регионы
        /// </summary>
        [XmlAttribute("out")]
        public bool outer;

        /// <summary>
        ///     Как хранится в DBF поле
        /// </summary>
        [XmlAttribute("labal")]
        public string label;

        /// <summary>
        ///     Номер региона
        /// </summary>
        [XmlAttribute("region")]
        public int region;

        /// <summary>
        ///     Lat
        /// </summary>
        [XmlAttribute("lat")]
        public float lat;

        /// <summary>
        ///     Lon
        /// </summary>
        [XmlAttribute("lon")]
        public float lon;

        /// <summary>
        ///     Ссылки на идентификаторы других межрайонных узлов, в которые можно добраться
        /// </summary>
        [XmlArrayItem("l")]
        public int[] links;

        /// <summary>
        ///     Оценки до других межрайонных узлов, в которые можно добраться
        /// </summary>
        [XmlArrayItem("c")]
        public float[] costs;

        /// <summary>
        ///     Дистанция до других межрайонных узлов, в которые можно добраться
        /// </summary>
        [XmlArrayItem("d")]
        public float[] dists;

        /// <summary>
        ///     Время до других межрайонных узлов, в которые можно добраться
        /// </summary>
        [XmlArrayItem("t")]
        public float[] times;

        public TRGNode(uint node, bool inner, bool outer, int id, string label, float lat, float lon)
        {
            this.node = node;
            this.inner = inner;
            this.outer = outer;
            this.id = id;
            this.label = label;

            this.lat = lat;
            this.lon = lon;

            this.region = 0;
            this.links = null;
            this.costs = null;
            this.dists = null;
            this.times = null;
        }
    }


    /// <summary>
    ///     Garmin Shapes 2 Graph Files Converter
    /// </summary>
    public class ShpToGraphConverter
    {
        /// <summary>
        ///     Информация о линии Shape файла
        /// </summary>
        private class TLine
        {
            /// <summary>
            ///     Bounds
            /// </summary>
            public double[] box;
            /// <summary>
            ///     Numer of line parts
            /// </summary>
            public int numParts;
            /// <summary>
            ///     Number of points
            /// </summary>
            public int numPoints;
            /// <summary>
            ///     Links To Parts Start Point
            /// </summary>
            public int[] parts;
            /// <summary>
            ///     Points
            /// </summary>
            public PointF[] points;

            public override string ToString()
            {
                return "Pts in line: " + numPoints.ToString();
            }
        }

        /// <summary>
        ///     Информация о запрете поворота
        /// </summary>
        private class TurnRestriction
        {
            /// <summary>
            ///     При движении по линии fromLine
            /// </summary>
            public uint fromLine = 0;
            /// <summary>
            ///     Через узел throughNode
            /// </summary>
            public uint throughNode = 0;
            /// <summary>
            ///     Нельзя повернуть в LINK_ID
            /// </summary>
            public int toLineLinkID = 0;            
            
            /// <summary>
            ///     Создаем запрет поворота    
            /// </summary>
            /// <param name="fromLine">При движении по линии fromLine</param>
            /// <param name="throughNode">Через узел throughNode</param>
            /// <param name="toLineLinkID">Нельзя повернуть в LINK_ID</param>
            public TurnRestriction(uint fromLine, uint throughNode, int toLineLinkID)
            {
                this.fromLine = fromLine;
                this.throughNode = throughNode;
                this.toLineLinkID = toLineLinkID;
            }

            public override string ToString()
            {
                return fromLine.ToString() + "->-" + throughNode.ToString() + "->-" + toLineLinkID;
            }
        }
        
        // заголовки файлов
        private static byte[] RMLINES = new byte[] { 0x52, 0x4D, 0x4C, 0x49, 0x4E, 0x45, 0x53 };
        private static byte[] RMSEGMENTS = new byte[] { 0x52, 0x4D, 0x53, 0x45, 0x47, 0x4D, 0x45, 0x4E, 0x54, 0x53 };
        private static byte[] RMGRAF2 = new byte[] { 0x52, 0x4D, 0x47, 0x52, 0x41, 0x46, 0x32 };
        private static byte[] RMGRAF3 = new byte[] { 0x52, 0x4D, 0x47, 0x52, 0x41, 0x46, 0x33 };
        private static byte[] RMINDEX = new byte[] { 0x52, 0x4D, 0x49, 0x4E, 0x44, 0x45, 0x58 };
        private static byte[] RMPOINTNLL0 = new byte[] { 0x52, 0x4D, 0x50, 0x4F, 0x49, 0x4E, 0x54, 0x4E, 0x4C, 0x4C, 0x30 };
        private static byte[] RMPOINTNLL1 = new byte[] { 0x52, 0x4D, 0x50, 0x4F, 0x49, 0x4E, 0x54, 0x4E, 0x4C, 0x4C, 0x31 };
        private static byte[] RMLINKIDS = new byte[] { 0x52, 0x4D, 0x4C, 0x49, 0x4E, 0x4B, 0x49, 0x44, 0x53 };
        private static byte[] RMTURNRSTR = new byte[] { 0x52, 0x4D, 0x54, 0x55, 0x52, 0x4E, 0x52, 0x53, 0x54, 0x52 };
        private static byte[] RMTMC = new byte[] { 0x52, 0x4D, 0x54, 0x4D, 0x43 };

        private const byte line_record_length = 15;
        private const byte segm_record_length = 30;

        private bool writeLinesNamesFile = false;
        /// <summary>
        ///     Писать ли файл наименования дорог
        /// </summary>
        public bool WriteLinesNamesFile { get { return writeLinesNamesFile; } set { writeLinesNamesFile = true; } }

        /// <summary>
        ///     Список точек стыковки межрайонных маршрутов
        /// </summary>
        private List<TRGNode> rgNodes = new List<TRGNode>();

        /// <summary>
        ///     Имена полей DBF файла
        /// </summary>
        private ShapeFields shpf;

        /// <summary>
        ///     Максимальная ошибка в расчетах
        /// </summary>
        private const Single maxError = (Single)1e-6;

        /// <summary>
        ///     Кодировка DBF файла
        /// </summary>
        private System.Text.Encoding DBFEncoding;

        /// <summary>
        ///     Имя shp файла
        /// </summary>
        private string shFile;
        /// <summary>
        ///     Имя файла графа
        /// </summary>
        private string grFile;
        /// <summary>
        ///     Имя префикса файлов графа
        /// </summary>
        private string grMain;

        /// <summary>
        ///     Общее число линий
        /// </summary>
        private int linesCount = 0;
        /// <summary>
        ///     Общее число сегментов
        /// </summary>
        private int segmCount = 0;

        /// <summary>
        ///     Pointer to Lines
        /// </summary>
        private Stream lnStr;
        /// <summary>
        ///     Pointer to Segments
        /// </summary>
        private Stream sgStr;
        /// <summary>
        ///     Pointer to Line Index
        /// </summary>
        private Stream liStr;
        /// <summary>
        ///     Pointer to Line TMC
        /// </summary>
        private Stream tmcStr;
        /// <summary>
        ///     Pointer to WriteLinesNamesFile
        /// </summary>
        private Stream lnmsStr;

        /// <summary>
        ///     Узлы графа
        /// </summary>
        private List<TNode> nodes = new List<TNode>();

        /// <summary>
        ///     Максимальное длина линии
        /// </summary>
        private float maxLengthBetweenNodes = 0;
        /// <summary>
        ///     Максимальное время движения по линии
        /// </summary>
        private float maxTimeBetweenNodes = 0;
        /// <summary>
        ///     Максимальная оценка между узлами
        /// </summary>
        private float maxCostBetweenNodes = 0;

        /// <summary>
        ///     Список найденных запретов поворотов
        /// </summary>
        private List<TurnRestriction> noTurns = new List<TurnRestriction>();

        /// <summary>
        ///     Число добавленных запретов поворотов
        /// </summary>
        private int noTurnsAdded = 0;

        /// <summary>
        ///     Конвертор Shape файла
        /// </summary>
        /// <param name="ShapeFileName">Полный путь</param>
        public ShpToGraphConverter(string ShapeFileName)
        {
            ni = (System.Globalization.NumberFormatInfo)ci.NumberFormat.Clone();
            ni.NumberDecimalSeparator = ".";            
            this.shFile = ShapeFileName;

            //shpf = new ShapesFields();
            //ShapesFields.Save(ShapesFields.GetCurrentDir()+@"\fldcfg.xml", shpf);            
            
            string fieldConfigFileName = Path.GetDirectoryName(shFile)+@"\"+ Path.GetFileNameWithoutExtension(shFile)+".fldcfg.xml";
            if (File.Exists(fieldConfigFileName))
                shpf = ShapeFields.Load(fieldConfigFileName);
            else
                shpf = new ShapeFields();

            DBFEncoding = shpf.CodePage;
        }

        /// <summary>
        ///     Конвертация Shape файла в граф
        /// </summary>
        /// <param name="GraphFileName"></param>
        public void ConvertTo(string GraphFileName)
        {
            Console.WriteLine("dkxce Shape-Graph Converter v0.3.2.9");
            Console.WriteLine("Copyrights (c) 2012 Karimov Artem <milokz@gmail.com>");
            Console.WriteLine();
            Console.WriteLine("Input file:  " + Path.GetFileName(shFile));
            Console.WriteLine("Output file: " + Path.GetFileName(GraphFileName));
            string fieldConfigFileName = Path.GetDirectoryName(shFile)+@"\"+ Path.GetFileNameWithoutExtension(shFile)+".fldcfg.xml";
            if (File.Exists(fieldConfigFileName))
                Console.WriteLine("Config file: " + Path.GetFileName(fieldConfigFileName));
            Console.WriteLine("DBF Encoding: " + this.DBFEncoding.CodePage.ToString()+" "+this.DBFEncoding.EncodingName);
            DateTime started = DateTime.Now;
            Console.WriteLine();
            Console.WriteLine("Started: " + DateTime.Now.ToString("HH:mm:ss"));
            Console.WriteLine();
            

            this.grFile = GraphFileName;
            this.grMain = GraphFileName.Remove(GraphFileName.LastIndexOf("."));

            Console.WriteLine("Preparing: " + Path.GetFileName(grMain) + ".lines.bin");
            lnStr = new FileStream(grMain + ".lines.bin", FileMode.Create);
            lnStr.Write(RMLINES, 0, RMLINES.Length);
            byte[] ba = new byte[4];
            lnStr.Write(ba, 0, ba.Length);

            Console.WriteLine("Preparing: " + Path.GetFileName(grMain) + ".segments.bin");
            sgStr = new FileStream(grMain + ".segments.bin", FileMode.Create);
            sgStr.Write(RMSEGMENTS, 0, RMSEGMENTS.Length);
            ba = new byte[4];
            sgStr.Write(ba, 0, ba.Length);

            Console.WriteLine("Preparing: " + Path.GetFileName(grMain) + ".lines.id");
            liStr = new FileStream(grMain + ".lines.id", FileMode.Create);
            liStr.Write(RMLINKIDS,0,RMLINKIDS.Length);
            ba = new byte[4];
            liStr.Write(ba, 0, ba.Length);

            if ((shpf.fldTMC != null) && (shpf.fldTMC != String.Empty))
            {
                Console.WriteLine("Preparing: " + Path.GetFileName(grMain) + ".lines.tmc");
                tmcStr = new FileStream(grMain + ".lines.tmc", FileMode.Create);
                tmcStr.Write(RMTMC, 0, RMTMC.Length);
                ba = new byte[4];
                tmcStr.Write(ba, 0, ba.Length);
            };

            if ((shpf.fldName != null) && (shpf.fldName != String.Empty) && (writeLinesNamesFile))
            {
                Console.WriteLine("Preparing: " + Path.GetFileName(grMain) + ".lines.txt");
                lnmsStr = new FileStream(grMain + ".lines.txt", FileMode.Create);
                ba = System.Text.Encoding.GetEncoding(1251).GetBytes("Файл наименований линий\r\n");
                lnmsStr.Write(ba, 0, ba.Length);
            };

            Console.WriteLine("Open Shape Files...");            
            ReadInnerFiles(this.shFile);
            Console.WriteLine("Shape Files Reading Completed");

            // do not save yet, until TurnRestriction check
            //lnStr.Position = RMLINES.Length;
            //ba = BitConverter.GetBytes(linesCount);
            //lnStr.Write(ba, 0, ba.Length);
            //lnStr.Flush();
            //lnStr.Close(); // .lines.bin
            //Console.WriteLine("Write: " + Path.GetFileName(grMain) + ".lines.bin Completed");

            sgStr.Position = RMSEGMENTS.Length;
            ba = BitConverter.GetBytes(segmCount);
            sgStr.Write(ba, 0, ba.Length);
            sgStr.Flush();
            sgStr.Close(); // .segments.bin
            Console.WriteLine("Write: " + Path.GetFileName(grMain) + ".segments.bin Completed");

            liStr.Position = RMLINKIDS.Length;
            ba = BitConverter.GetBytes(linesCount);
            liStr.Write(ba, 0, ba.Length);
            liStr.Flush();
            List<int> linkids = new List<int>(linesCount);
            for (int i = 0; i < linesCount; i++)
            {
                liStr.Read(ba, 0, ba.Length);
                linkids.Add(BitConverter.ToInt32(ba, 0));
            };
            liStr.Close();// .lines.id
            Console.WriteLine("Write: " + Path.GetFileName(grMain) + ".lines.id Completed");

            if ((shpf.fldTMC != null) && (shpf.fldTMC != String.Empty))
            {
                tmcStr.Position = RMTMC.Length;
                ba = BitConverter.GetBytes(linesCount);
                tmcStr.Write(ba, 0, ba.Length);
                tmcStr.Flush();
                tmcStr.Close();// .lines.tmc
                Console.WriteLine("Write: " + Path.GetFileName(grMain) + ".lines.tmc Completed");
            };

            if ((shpf.fldName != null) && (shpf.fldName != String.Empty) && (writeLinesNamesFile))
            {                
                lnmsStr.Flush();
                lnmsStr.Close();
                Console.WriteLine("Write: " + Path.GetFileName(grMain) + ".lines.txt Completed");
            };

            SaveNodes(linkids);
                        
            // TurnRestrictions Checked ->- Save Lines
            lnStr.Position = RMLINES.Length;
            ba = BitConverter.GetBytes(linesCount);
            lnStr.Write(ba, 0, ba.Length);
            lnStr.Flush();
            lnStr.Close(); // .lines.bin
            Console.WriteLine("Write: " + Path.GetFileName(grMain) + ".lines.bin Completed");

            // DONE //

            DateTime ended = DateTime.Now;
            TimeSpan elapsed = ended.Subtract(started);

            FileStream rtStr = new FileStream(GraphFileName, FileMode.Create);
            StreamWriter sw = new StreamWriter(rtStr);
            sw.WriteLine("dkxce shape-graph converter result main file");
            sw.WriteLine("Started: " + started.ToString("yyyy-MM-dd HH:mm:ss"));
            sw.WriteLine("Converted: " + ended.ToString("yyyy-MM-dd HH:mm:ss"));
            sw.WriteLine("Elapsed: " + String.Format("{0:00}:{1:00}:{2:00}", elapsed.TotalHours, elapsed.Minutes, elapsed.Seconds));
            sw.WriteLine("Source file: " + Path.GetFileName(shFile));
            sw.WriteLine("Source path: " + Path.GetDirectoryName(shFile));
            sw.WriteLine("DBF CodePage: " + this.DBFEncoding.CodePage.ToString());
            sw.WriteLine("DBF Encoding: " + this.DBFEncoding.EncodingName);
            sw.WriteLine("Latitude Inversed: " + this.shpf.InverseLat.ToString());
            sw.WriteLine("Longitude Inversed: " + this.shpf.InverseLon.ToString());
            sw.WriteLine("Max Cost Between Nodes: " + maxCostBetweenNodes.ToString(ni));
            sw.WriteLine("Max Length Between Nodes: " + maxLengthBetweenNodes.ToString(ni) + " m");
            sw.WriteLine("Max Time Between Nodes: " + maxTimeBetweenNodes.ToString(ni) + " min");
            sw.WriteLine("Lines: " + linesCount.ToString());
            sw.WriteLine("Segments: " + segmCount.ToString());
            sw.WriteLine("Nodes: " + nodes.Count.ToString());
            sw.WriteLine("Turn Restrictions: " + noTurnsAdded.ToString());
            sw.WriteLine();
            sw.WriteLine("Files:");
            sw.WriteLine("  " + Path.GetFileName(grMain) + ".lines.bin - Lines information (segments count, pos, oneway, node start, node stop)");
            sw.WriteLine("  " + Path.GetFileName(grMain) + ".lines.id - Lines LINK_ID");
            sw.WriteLine("  " + Path.GetFileName(grMain) + ".segments.bin - Lines segments");
            sw.WriteLine("  " + Path.GetFileName(grMain) + ".lines.txt - Lines names");
            sw.WriteLine("  " + Path.GetFileName(grMain) + ".graph.bin - Graph nodes information");
            sw.WriteLine("  " + Path.GetFileName(grMain) + ".graph.bin.in - Index for node position in graph");
            sw.WriteLine("  " + Path.GetFileName(grMain) + ".graph[r].bin - Graph nodes information for inverse solve");
            sw.WriteLine("  " + Path.GetFileName(grMain) + ".graph[r].bin.in - Index for node position in graph (inverse solve)");
            sw.WriteLine("  " + Path.GetFileName(grMain) + ".graph.geo - Nodes Lat Lon information");
            sw.WriteLine("  " + Path.GetFileName(grMain) + ".graph.geo.ll - Indexed Lat Lon for nodes");
            sw.WriteLine("  " + Path.GetFileName(grMain) + ".rgnodes.xml - Region Nodes Information");
            sw.WriteLine("  " + Path.GetFileName(grMain) + ".analyze.txt - Nodes In/Out fail information");
            sw.Flush();
            sw.Close();
            rtStr.Close();
                        
            Console.WriteLine();
            Console.WriteLine("Convertion Done: " + ended.ToString("HH:mm:ss"));
            Console.WriteLine("Elapsed: " + String.Format("{0:00}:{1:00}:{2:00}",elapsed.TotalHours,elapsed.Minutes,elapsed.Seconds));                       
        }

        /// <summary>
        ///     Сохраняем узлы и все что с ними связано
        /// </summary>
        private void SaveNodes(List<int> linkIds)
        {
            Console.WriteLine();

            // // // // TEST // // // //
            //nodes.Add(new TNode(1, 0, 0));
            //nodes[0].links.Add(new TLink(5, 0, 0, 0, 1, false));
            //nodes[0].rlinks.Add(new TLink(5, 0, 0, 0, 1, false));
            //nodes.Add(new TNode(2, 0, 0));
            //nodes[1].links.Add(new TLink(5, 0, 0, 0, 2, false));
            //nodes[1].rlinks.Add(new TLink(5, 0, 0, 0, 2, false));
            //nodes.Add(new TNode(3, 0, 0));
            //nodes[2].links.Add(new TLink(5, 0, 0, 0, 3, false));
            //nodes[2].links.Add(new TLink(6, 0, 0, 0, 6, false));
            //nodes[2].links.Add(new TLink(7, 0, 0, 0, 7, false));
            //nodes[2].rlinks.Add(new TLink(5, 0, 0, 0, 3, false));
            //nodes[2].rlinks.Add(new TLink(6, 0, 0, 0, 6, false));
            //nodes[2].rlinks.Add(new TLink(7, 0, 0, 0, 7, false));
            //nodes.Add(new TNode(4, 0, 0));
            //nodes[3].links.Add(new TLink(5, 0, 0, 0, 4, false));
            //nodes[3].rlinks.Add(new TLink(5, 0, 0, 0, 4, false));
            //nodes.Add(new TNode(5, 0, 0));
            //nodes[4].links.Add(new TLink(1, 0, 0, 0, 1, false));
            //nodes[4].links.Add(new TLink(2, 0, 0, 0, 2, false));
            //nodes[4].links.Add(new TLink(3, 0, 0, 0, 3, false));
            //nodes[4].links.Add(new TLink(4, 0, 0, 0, 4, false));
            //nodes[4].rlinks.Add(new TLink(1, 0, 0, 0, 1, false));
            //nodes[4].rlinks.Add(new TLink(2, 0, 0, 0, 2, false));
            //nodes[4].rlinks.Add(new TLink(3, 0, 0, 0, 3, false));
            //nodes[4].rlinks.Add(new TLink(4, 0, 0, 0, 4, false));
            //nodes.Add(new TNode(6, 0, 0));
            //nodes[5].links.Add(new TLink(3, 0, 0, 0, 6, false));
            //nodes[5].rlinks.Add(new TLink(3, 0, 0, 0, 6, false));
            //nodes.Add(new TNode(7, 0, 0));
            //nodes[6].links.Add(new TLink(3, 0, 0, 0, 7, false));
            //nodes[6].rlinks.Add(new TLink(3, 0, 0, 0, 7, false));
            //noTurns.Add(new TurnRestriction(1, 5, 1004));
            //noTurns.Add(new TurnRestriction(4, 5, 1003));
            //noTurns.Add(new TurnRestriction(3, 5, 1002));
            //noTurns.Add(new TurnRestriction(4, 5, 1001));
            //noTurns.Add(new TurnRestriction(3, 3, 1007));
            //linkIds.Clear();
            //linkIds.Add(1001);
            //linkIds.Add(1002);
            //linkIds.Add(1003);
            //linkIds.Add(1004);
            //linkIds.Add(1006);
            //linkIds.Add(1007);


            // save turn restrictions
            Console.WriteLine("Turn Restrictions Analyze (TRA)... ");
            Console.WriteLine("Found ... ");

            int noTurnsWas = noTurns.Count;
            int noTurnsSkipped = 0;
            int newNodesAdded = 0;
            while (noTurns.Count > 0)
            {
                TurnRestriction ctr = noTurns[0];
                TNode nd = nodes[(int)ctr.throughNode - 1]; // узел throught
                List<TNode> nns = new List<TNode>(); // узлы подмены                    

                // для каждой входящей связи)
                for (int _in = 0; _in < nd.rlinks.Count; _in++)
                {
                    List<uint> canLine = new List<uint>(); // список линий куда можно ехать
                    for (int _ot = 0; _ot < nd.links.Count; _ot++) canLine.Add(nd.links[_ot].line);

                    for (int nt = noTurns.Count - 1; nt >= 0; nt--) // удаляем куда нельзя ехать
                        if ((noTurns[nt].fromLine == nd.rlinks[_in].line) && (noTurns[nt].throughNode == nd.node))
                        {                            
                            int lid = linkIds.IndexOf(noTurns[nt].toLineLinkID);
                            if (lid >= 0)
                            {
                                uint line = (uint)lid + 1; // find line by link_id
                                if (line == nd.rlinks[_in].line)
                                {
                                    // No U-Turn
                                };
                                canLine.Remove(line); // remove line from can go list                                
                                noTurnsAdded++;
                            }
                            else
                                noTurnsSkipped++;
                            noTurns.RemoveAt(nt); // remove turn restriction                            
                        };
                    TNode nn = new TNode((uint)(nodes.Count + _in), nd.lat, nd.lon); // создаем новый узел
                    nn.rlinks.Add(nd.rlinks[_in]); // добвляем информацию о линии по которой пришли
                    for (int _ot = 0; _ot < nd.links.Count; _ot++) // добавляем куда можно пойти
                        if (canLine.Contains(nd.links[_ot].line))
                        {
                            TLink link = nd.links[_ot];
                            nn.links.Add(link);
                        };
                    nns.Add(nn);
                };
                // replace nd node
                nns[0].node = nd.node;
                nodes[(int)nd.node - 1] = nns[0];
                // add new
                for (int i = 1; i < nns.Count; i++)
                    nodes.Add(nns[i]);
                // update near nodes
                for (int i = 0; i < nns.Count; i++)
                {
                    for (int _in = 0; _in < nns[i].rlinks.Count; _in++)
                    {
                        TNode upd = nodes[(int)nns[i].rlinks[_in].node - 1];
                        for (int _ot = 0; _ot < upd.links.Count; _ot++)
                            if (upd.links[_ot].node == nd.node)
                                upd.links[_ot].node = nns[i].node;
                    };
                    for (int _ot = 0; _ot < nns[i].links.Count; _ot++)
                    {
                        TNode upd = nodes[(int)nns[i].links[_ot].node - 1];
                        for (int _in = 0; _in < upd.rlinks.Count; _in++)
                            if (upd.rlinks[_in].node == nd.node)
                                upd.rlinks[_in].node = nns[i].node;
                    };
                };
                // update changes in noTurns
                for (int i = noTurns.Count - 1; i >= 0; i--)
                {
                    // проверка сходимости
                    if (noTurns[i].throughNode == nd.node)
                    {
                        string txt = "NoTurn from LINK_ID " + linkIds[(int)noTurns[i].fromLine - 1].ToString() + " to LINK_ID " + noTurns[i].toLineLinkID.ToString();
                        // если в узле несколько запретов с поворотов и начальные
                        // линии не были найдены ранее, то возможно дорога односторонняя,
                        // а запрет поворота указан в запрещенном направлении движения.
                        // Поэтому сперва ищем линию в исходящих связях
                        bool deleted = false;
                        for (int _out = 0; _out < nd.links.Count; _out++)
                            if (noTurns[i].fromLine == nd.links[_out].line)
                                deleted = true;
                        // если линия найдена, то дорога одностороняя и повернуть 
                        // в указанном в запрете направлении нельзя - удаляем лишнюю информацию
                        if (deleted)
                        {
                            Console.SetCursorPosition(0, Console.CursorTop - 1);
                            Console.WriteLine(txt+" ignored - WRONG WAY DIRECTION");
                            Console.WriteLine("");
                            noTurns.RemoveAt(i);
                        }
                        else
                        {
                            // если линия не найдена, то ее нет в исходном shp файле
                            throw new StackOverflowException("Запреты поворотов не сходятся, проверьте исходные данные!!! " + txt);
                        };
                    };                                       
                };
                // update incoming lines
                for (int _in = 0; _in < nd.rlinks.Count; _in++)
                {
                    lnStr.Position = RMLINES.Length + 4 + line_record_length * (nd.rlinks[_in].line) - 8;
                    byte[] ba = new byte[8];
                    lnStr.Read(ba, 0, 8);
                    uint node1 = BitConverter.ToUInt32(ba, 0);
                    uint node2 = BitConverter.ToUInt32(ba, 4);
                    if (node1 == nd.node) node1 = nns[_in].node;
                    if (node2 == nd.node) node2 = nns[_in].node;
                    lnStr.Position -= 8;
                    ba = BitConverter.GetBytes(node1);
                    lnStr.Write(ba, 0, ba.Length);
                    ba = BitConverter.GetBytes(node2);
                    lnStr.Write(ba, 0, ba.Length);
                };
                lnStr.Flush();
                //
                newNodesAdded += nns.Count - 1;
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.WriteLine(String.Format("NoTurns with new analyzed added: {0} from {1} found; skipped {2}", noTurnsAdded, noTurnsWas, noTurnsSkipped));
            };
            // TRA DONE

            Console.WriteLine("New nodes to add (after Turn Restrictions Analyze): "+newNodesAdded.ToString());
            Console.WriteLine("Turn Restrictions Analyze (TRA) Completed");
            Console.WriteLine(); 
            /////

            // save graph            
            Console.WriteLine("Preparing: " + Path.GetFileName(grMain) + ".graph.bin");
            FileStream grStr = new FileStream(grMain + ".graph.bin", FileMode.Create);
            grStr.Write(RMGRAF2, 0, RMGRAF2.Length);
            byte[] nodesC = BitConverter.GetBytes(nodes.Count);
            grStr.Write(nodesC, 0, nodesC.Length);
            byte[] maxl = BitConverter.GetBytes(maxLengthBetweenNodes);
            grStr.Write(maxl, 0, maxl.Length);

            // save reversed graph
            Console.WriteLine("Preparing: " + Path.GetFileName(grMain) + ".graph[r].bin");
            FileStream grStrR = new FileStream(grMain + ".graph[r].bin", FileMode.Create);
            grStrR.Write(RMGRAF3, 0, RMGRAF2.Length);
            BitConverter.GetBytes(nodes.Count);
            grStrR.Write(nodesC, 0, nodesC.Length);
            BitConverter.GetBytes(maxLengthBetweenNodes);
            grStrR.Write(maxl, 0, maxl.Length);

            // save graph index
            Console.WriteLine("Preparing: " + Path.GetFileName(grMain) + ".graph.bin.in");
            FileStream giStr = new FileStream(grMain + ".graph.bin.in", FileMode.Create);
            giStr.Write(RMINDEX, 0, RMINDEX.Length);
            giStr.Write(nodesC, 0, nodesC.Length);

            // save reversed graph index
            Console.WriteLine("Preparing: " + Path.GetFileName(grMain) + ".graph[r].bin.in");
            FileStream giStrR = new FileStream(grMain + ".graph[r].bin.in", FileMode.Create);
            giStrR.Write(RMINDEX, 0, RMINDEX.Length);
            giStrR.Write(nodesC, 0, nodesC.Length);
            
            // graph nodes lat lon
            Console.WriteLine("Preparing: " + Path.GetFileName(grMain) + ".graph.geo");
            FileStream geoStr = new FileStream(grMain + ".graph.geo", FileMode.Create);
            geoStr.Write(RMPOINTNLL0, 0, RMPOINTNLL0.Length);
            geoStr.Write(nodesC, 0, nodesC.Length);

            Console.WriteLine("Write Node...");
            FileStream ninf = new FileStream(grMain + ".graph.analyze.txt", FileMode.Create);
            StreamWriter nw = new StreamWriter(ninf);
            nw.WriteLine("Анализ узлов графа на наличие ребер... ");
            for (int i = 0; i < nodes.Count; i++)
            {
                bool nl = false;
                if (nodes[i].Ins == 0)
                {
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                    string ln = String.Format("... в узел {0} нельзя попасть {1} {2} ", i + 1, nodes[i].lat, nodes[i].lon);
                    Console.WriteLine(ln);
                    nw.WriteLine(ln);
                    nl = true;
                };
                if (nodes[i].Outs == 0)
                {
                    if (!nl) Console.SetCursorPosition(0, Console.CursorTop - 1);
                    string ln = String.Format("... из узла {0} нельзя выехать {1} {2} ", i + 1, nodes[i].lat, nodes[i].lon);
                    Console.WriteLine(ln);
                    nw.WriteLine(ln);
                    nl = true;
                };
                if (nl) Console.WriteLine();

                byte[] lat = BitConverter.GetBytes(nodes[i].lat); // Write nodes Lat Lon
                geoStr.Write(lat, 0, lat.Length);
                byte[] lon = BitConverter.GetBytes(nodes[i].lon);
                geoStr.Write(lon, 0, lon.Length);

                byte links = (byte)nodes[i].links.Count; // Write Node links pos
                byte[] pos = BitConverter.GetBytes((uint)grStr.Position);
                giStr.Write(pos, 0, pos.Length);

                byte rlinks = (byte)nodes[i].rlinks.Count;  // Write Node rlinks pos
                byte[] posR = BitConverter.GetBytes((uint)grStrR.Position);
                giStrR.Write(posR, 0, posR.Length);
                                
                grStr.WriteByte(links);
                grStrR.WriteByte(rlinks);

                for (int x = 0; x < nodes[i].links.Count; x++)  // Write Node links
                {
                    byte[] next = BitConverter.GetBytes(nodes[i].links[x].node);// 4
                    grStr.Write(next, 0, next.Length);
                    byte[] cost = BitConverter.GetBytes(nodes[i].links[x].cost);// 4
                    grStr.Write(cost, 0, cost.Length);
                    byte[] dist = BitConverter.GetBytes(nodes[i].links[x].dist);// 4
                    grStr.Write(dist, 0, dist.Length);
                    byte[] time = BitConverter.GetBytes(nodes[i].links[x].time);// 4
                    grStr.Write(time, 0, time.Length);
                    byte[] line = BitConverter.GetBytes(nodes[i].links[x].line);// 4
                    grStr.Write(line, 0, line.Length);
                    byte rev = nodes[i].links[x].inverse_dir ? (byte)1 : (byte)0;//// 1
                    grStr.WriteByte(rev);
                };
                grStr.Flush();

                for (int x = 0; x < nodes[i].rlinks.Count; x++)  // Write Node rlinks
                {
                    byte[] next = BitConverter.GetBytes(nodes[i].rlinks[x].node);// 4
                    grStrR.Write(next, 0, next.Length);
                    byte[] cost = BitConverter.GetBytes(nodes[i].rlinks[x].cost);// 4
                    grStrR.Write(cost, 0, cost.Length);
                    byte[] dist = BitConverter.GetBytes(nodes[i].rlinks[x].dist);// 4
                    grStrR.Write(dist, 0, dist.Length);
                    byte[] time = BitConverter.GetBytes(nodes[i].rlinks[x].time);// 4
                    grStrR.Write(time, 0, time.Length);
                    byte[] line = BitConverter.GetBytes(nodes[i].rlinks[x].line);// 4
                    grStrR.Write(line, 0, line.Length);
                    byte rev = nodes[i].rlinks[x].inverse_dir ? (byte)1 : (byte)0;//// 1
                    grStrR.WriteByte(rev);
                };
                grStrR.Flush();
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.WriteLine("Nodes added: " + (i+1).ToString());
            };
            nw.WriteLine("Завершено");
            nw.Flush();
            nw.Close();
            ninf.Close();

            Console.WriteLine("Write: " + Path.GetFileName(grMain) + ".graph.bin Completed");
            grStr.Flush();
            grStr.Close();

            Console.WriteLine("Write: " + Path.GetFileName(grMain) + ".graph[r].bin Completed");
            grStrR.Flush();
            grStrR.Close();            
            
            Console.WriteLine("Write: " + Path.GetFileName(grMain) + ".graph.bin.in Completed");
            giStr.Flush();
            giStr.Close();

            Console.WriteLine("Write: " + Path.GetFileName(grMain) + ".graph[r].bin.in Completed");
            giStrR.Flush();
            giStrR.Close();

            Console.WriteLine("Write: " + Path.GetFileName(grMain) + ".graph.geo Completed");
            geoStr.Flush();
            geoStr.Close();

            Console.WriteLine();

            Console.Write("Saving " + Path.GetFileName(grMain) + ".graph.geo.ll ...");
            nodes.Sort(TNode.Sorter.SortByLL());

            // graph nodes lat lon indexed
            FileStream gllStr = new FileStream(grMain + ".graph.geo.ll", FileMode.Create);
            gllStr.Write(RMPOINTNLL1, 0, RMPOINTNLL1.Length);
            gllStr.Write(nodesC, 0, nodesC.Length);
            for (int i = 0; i < nodes.Count; i++)
            {
                byte[] ba = BitConverter.GetBytes(nodes[i].node);  // Write Node Lat Lon Indexed
                gllStr.Write(ba, 0, ba.Length);
                ba = BitConverter.GetBytes(nodes[i].lat);
                gllStr.Write(ba, 0, ba.Length);
                ba = BitConverter.GetBytes(nodes[i].lon);
                gllStr.Write(ba, 0, ba.Length);
            };
            gllStr.Flush();
            gllStr.Close();
            Console.WriteLine(" Completed");

            Console.WriteLine();
            if (rgNodes.Count > 0)
            {
                Console.WriteLine("Saving RGNodes: "+rgNodes.Count.ToString()+" - in "+ Path.GetFileName(grMain)+".rgnodes.xml");
                XMLSaved<TRGNode[]>.Save(grMain + ".rgnodes.xml", rgNodes.ToArray());
                Console.WriteLine();
            };
        }

        /// <summary>
        ///     Проверяем наличие в DBF файле всех необходимых полей
        /// </summary>
        /// <param name="fields">Список полей файла</param>
        private void CheckNeededFields(string[] fields)
        {
            List<string> flds = new List<string>(fields);
            bool exc = false;
            if (!flds.Contains(shpf.fldGarminType))
            {
                exc = true;
                Console.WriteLine(String.Format("Field '{0}' as {1} not found!", shpf.fldGarminType, "GarminType"));
            };
            if (!flds.Contains(shpf.fldLinkId))
            {
                exc = true;
                Console.WriteLine(String.Format("Field '{0}' as {1} not found!", shpf.fldLinkId, "LinkId"));
            };
            if (!flds.Contains(shpf.fldOneWay))
            {
                exc = true;
                Console.WriteLine(String.Format("Field '{0}' as {1} not found!", shpf.fldOneWay, "OneWay"));
            };
            if (!flds.Contains(shpf.fldSpeedLimit))
            {
                exc = true;
                Console.WriteLine(String.Format("Field '{0}' as {1} not found!", shpf.fldSpeedLimit, "SpeedLimit"));
            };
            if ((shpf.fldLength != null) && (shpf.fldLength != String.Empty))
                if (!flds.Contains(shpf.fldLength))
                {
                    exc = true;
                    Console.WriteLine(String.Format("Field '{0}' as {1} not found!", shpf.fldLength, "Length"));
                };
            if(!shpf.NoTurnRestrictions)
                if (!flds.Contains(shpf.fldTurnRstr))
                {
                    exc = true;
                    Console.WriteLine(String.Format("Field '{0}' as {1} not found!", shpf.fldTurnRstr, "TurnRestrictions"));
                };
            if ((shpf.fldTMC != null) && (shpf.fldTMC != String.Empty))
                if (!flds.Contains(shpf.fldTMC))
                {
                    exc = true;
                    Console.WriteLine(String.Format("Field '{0}' as {1} not found!", shpf.fldTMC, "TMC"));
                };
            if((shpf.fldName != null) && (shpf.fldTMC != String.Empty))
                if(!flds.Contains(shpf.fldName))
                {
                    exc = true;
                    Console.WriteLine(String.Format("Field '{0}' as {1} not found!", shpf.fldName, "NAME"));
                };
            if ((shpf.fldRGNODE != null) && (shpf.fldRGNODE != String.Empty))
                if (!flds.Contains(shpf.fldRGNODE))
                {
                    exc = true;
                    Console.WriteLine(String.Format("Field '{0}' as {1} not found!", shpf.fldRGNODE, "RGNODE"));
                };            
            if(exc)
                throw new Exception("Some fields in DBF not found");
        }

        /// <summary>
        ///     Читаем shp и dbf
        /// </summary>
        /// <param name="filename">Имя shp файла</param>
        private void ReadInnerFiles(string filename)
        {
            // Read shp file to buffer
            FileStream shapeFileStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            long shapeFileLength = shapeFileStream.Length;
            Byte[] shapeFileData = new Byte[shapeFileLength];
            shapeFileStream.Read(shapeFileData, 0, (int)shapeFileLength);
            shapeFileStream.Close();
            
            // check valid records type
            int shapetype = readIntLittle(shapeFileData, 32);
            if (shapetype != 3) throw new Exception("Shape doesn't contains roads (not polyline)");

            // read DBF
            string dbffile = filename.Substring(0,filename.Length - 4) + ".dbf";
            FileStream dbfFileStream = new FileStream(dbffile, FileMode.Open, FileAccess.Read);

            // Read File Version
            dbfFileStream.Position = 0;
            int ver = dbfFileStream.ReadByte();

            // Read Records Count
            dbfFileStream.Position = 04;
            byte[] bb = new byte[4];            
            dbfFileStream.Read(bb, 0, 4);
            int total = BitConverter.ToInt32(bb, 0);
            Console.WriteLine("Total lines: " + total.ToString());

            // Read DataRecord 1st Position
            dbfFileStream.Position = 8;
            bb = new byte[2];
            dbfFileStream.Read(bb, 0, 2);
            short dataRecord_1st_Pos = BitConverter.ToInt16(bb, 0);
            int FieldsCount = (((bb[0] + (bb[1] * 0x100)) - 1) / 32) - 1;

            // Read DataRecord Length
            dbfFileStream.Position = 10;
            bb = new byte[2];
            dbfFileStream.Read(bb, 0, 2);
            short dataRecord_Length = BitConverter.ToInt16(bb, 0);

            // Read Заголовки Полей
            dbfFileStream.Position = 32;
            string[] Fields_Name = new string[FieldsCount]; // Массив названий полей
            Hashtable fieldsLength = new Hashtable(); // Массив размеров полей
            Hashtable fieldsType = new Hashtable();   // Массив типов полей
            byte[] Fields_Dig = new byte[FieldsCount];   // Массив размеров дробной части
            int[] Fields_Offset = new int[FieldsCount];    // Массив отступов
            bb = new byte[32 * FieldsCount]; // Описание полей: 32 байтa * кол-во, начиная с 33-го            
            dbfFileStream.Read(bb, 0, bb.Length);
            int FieldsLength = 0;
            for (int x = 0; x < FieldsCount; x++)
            {
                Fields_Name[x] = System.Text.Encoding.Default.GetString(bb, x * 32, 10).TrimEnd(new char[] { (char)0x00 }).ToUpper();
                fieldsType.Add(Fields_Name[x], "" + (char)bb[x * 32 + 11]);
                fieldsLength.Add(Fields_Name[x], (int)bb[x * 32 + 16]);
                Fields_Dig[x] = bb[x * 32 + 17];
                Fields_Offset[x] = 1 + FieldsLength;
                FieldsLength = FieldsLength + (int)fieldsLength[Fields_Name[x]];

                // loadedScript.fieldsType[Fields_Name[x]] == "L" -- System.Boolean
                // loadedScript.fieldsType[Fields_Name[x]] == "D" -- System.DateTime
                // loadedScript.fieldsType[Fields_Name[x]] == "N" -- System.Int32 (FieldDigs[x] == 0) / System.Decimal (FieldDigs[x] != 0)
                // loadedScript.fieldsType[Fields_Name[x]] == "F" -- System.Double
                // loadedScript.fieldsType[Fields_Name[x]] == "C" -- System.String
                // loadedScript.fieldsType[Fields_Name[x]] == def -- System.String
            };
            // end read dbf

            CheckNeededFields(Fields_Name);

            int currentPosition = 100;
            int lines = 0;
            Console.WriteLine("Read data...");
            while (currentPosition < shapeFileLength)
            {
                // Read Shape
                int recordStart = currentPosition;
                int recordNumber = readIntBig(shapeFileData, recordStart);
                int contentLength = readIntBig(shapeFileData, recordStart + 4);
                int recordContentStart = recordStart + 8;

                TLine line = new TLine();
                int recordShapeType = readIntLittle(shapeFileData, recordContentStart);
                line.box = new Double[4];
                line.box[0] = readDoubleLittle(shapeFileData, recordContentStart + 4);
                line.box[1] = readDoubleLittle(shapeFileData, recordContentStart + 12);
                line.box[2] = readDoubleLittle(shapeFileData, recordContentStart + 20);
                line.box[3] = readDoubleLittle(shapeFileData, recordContentStart + 28);
                line.numParts = readIntLittle(shapeFileData, recordContentStart + 36);
                line.parts = new int[line.numParts];
                line.numPoints = readIntLittle(shapeFileData, recordContentStart + 40);
                line.points = new PointF[line.numPoints];
                int partStart = recordContentStart + 44;
                for (int i = 0; i < line.numParts; i++)
                {
                    line.parts[i] = readIntLittle(shapeFileData, partStart + i * 4);
                }
                int pointStart = recordContentStart + 44 + 4 * line.numParts;
                for (int i = 0; i < line.numPoints; i++)
                {
                    line.points[i] = new PointF(0, 0);
                    line.points[i].X = (float)readDoubleLittle(shapeFileData, pointStart + (i * 16));
                    line.points[i].Y = (float)readDoubleLittle(shapeFileData, pointStart + (i * 16) + 8);
                };                
                // move to next shape file record
                currentPosition = recordStart + (4 + contentLength) * 2;

                // Read DBF
                string[] FieldValues = new string[FieldsCount];
                Hashtable recordData = new Hashtable();
                for (int y = 0; y < FieldValues.Length; y++)
                {
                    dbfFileStream.Position = dataRecord_1st_Pos + (dataRecord_Length * lines) + Fields_Offset[y];
                    bb = new byte[(int)fieldsLength[Fields_Name[y]]];
                    dbfFileStream.Read(bb, 0, bb.Length);
                    FieldValues[y] = DBFEncoding.GetString(bb).Trim().TrimEnd(new char[] { (char)0x00 });
                    recordData.Add(Fields_Name[y], FieldValues[y]);
                };

                OnReadLine(line,recordData);
                lines++;
            };

            dbfFileStream.Close();
        }

        /// <summary>
        ///     Исп-ся для string-->float
        /// </summary>
        private System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InstalledUICulture;
        /// <summary>
        ///     Исп-ся для string-->float
        /// </summary>
        private System.Globalization.NumberFormatInfo ni;

        // REQ: ONE_WAY, SPD_LIMIT, HWY, LINK_ID, LEN, TURN_RSTRS
        /// <summary>
        ///     Вызываем для каждой линии shp файла
        /// </summary>
        /// <param name="line">линия</param>
        /// <param name="data">поля DBF</param>
        private void OnReadLine(TLine line, Hashtable data)
        {
            // Inverse Latitude
            if(shpf.InverseLat)
                for (int i = 0; i < line.points.Length; i++) line.points[i].Y *= -1;
            // Inverse Longitude
            if (shpf.InverseLon)
                for (int i = 0; i < line.points.Length; i++) line.points[i].X *= -1;

            linesCount++; // счетчик линий в файле
            segmCount += line.points.Length - 1; // счетчик сегментов линий в файле
            uint LINE = (uint)linesCount;

            bool one_way = data[shpf.fldOneWay].ToString() == "1"; // одностороннее движение
            int link_id = int.Parse(data[shpf.fldLinkId].ToString()); // LINK_ID

            int speed_max = int.Parse(data[shpf.fldSpeedLimit].ToString()); // ограничение скорости на участке            
            int speed_normal = speed_max > 60 ? speed_max - 15 : speed_max - 10; // скорость движения на участке
            if (speed_normal <= 0) speed_normal = 3; // если скорость отриц. или ноль - 3 км/ч (грунтовка)
            
            if ((data[shpf.fldGarminType].ToString().IndexOf("HWY") >= 0) && (speed_max < 110)) speed_max = 110; // магистраль/шоссе                        

            float dist = 0; // длина участка в метрах
            if ((shpf.fldLength != null) && (shpf.fldLength != String.Empty))
                dist = float.Parse(data[shpf.fldLength].ToString(), ni); // faster // read from DBF
            else
                for (int i = 1; i < line.points.Length; i++) // slower // calculate
                {
                    float _lat1 = line.points[i - 1].Y;
                    float _lon1 = line.points[i - 1].X;
                    float _lat2 = line.points[i].Y;
                    float _lon2 = line.points[i].X;
                    dist += GetLengthMeters(_lat1, _lon1, _lat2, _lon2, false);
                };

            float lat1 = line.points[0].Y;
            float lon1 = line.points[0].X;
            float lat2 = line.points[line.points.Length - 1].Y;
            float lon2 = line.points[line.points.Length - 1].X;

            uint sn = NodeByLatLon(lat1, lon1); // начальный узел
            if (sn == 0) sn = AddNode(lat1, lon1);            
            uint en = NodeByLatLon(lat2, lon2); // конечный узел
            if (en == 0) en = AddNode(lat2, lon2);
            
            if (dist > maxLengthBetweenNodes) maxLengthBetweenNodes = dist; // макс длина участка
            if (dist < maxError) dist = maxError + (float)0.00001; // нужно для корректного расчета

            float time = dist / 1000 / (float)speed_normal * 60; // время движения по участку в минутах
            float cost = dist / 1000 / (float)speed_max * 60; // оценочное время движения по участку в минутах
            if (time > maxTimeBetweenNodes) maxTimeBetweenNodes = time;            
            if (cost > maxCostBetweenNodes) maxCostBetweenNodes = cost;

            // Turn Restrictions // запреты поворотов
            if (!shpf.NoTurnRestrictions)
            {
                string tr = data[shpf.fldTurnRstr].ToString();
                if (tr.Length > 0)
                {
                    string[] restrictions = tr.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string r in restrictions)
                    {
                        bool fromStart = r.Substring(0, 1) == "F";
                        int lineTo_linkId = int.Parse(r.Substring(1));
                        if(fromStart)
                            noTurns.Add(new TurnRestriction(LINE, sn, lineTo_linkId));
                        else
                            noTurns.Add(new TurnRestriction(LINE, en, lineTo_linkId));
                    };
                };
            };

            nodes[(int)sn - 1].AddLink(en, cost, dist, time, LINE, false); // доб. исх. связь в нач. узел
            nodes[(int)en - 1].AddRLink(sn, cost, dist, time, LINE, false); // доб. вх. связь в кон. узел
            if (!one_way)
            {
                nodes[(int)en - 1].AddLink(sn, cost, dist, time, LINE, true); // доб. исх. связь в кон. узел
                nodes[(int)sn - 1].AddRLink(en, cost, dist, time, LINE, true); // доб. вх. связь в нач. узел
            };

            byte[] seg = BitConverter.GetBytes((ushort)(line.points.Length - 1)); // сегментов в линии
            byte[] pos = BitConverter.GetBytes((int)sgStr.Position); // позиция первого сегмента линии в файле сегментов
            byte one =  (byte)(0 +
                (one_way ? (byte)1 : (byte)0) // односторонка ?
                ); 
            byte[] nd1 = BitConverter.GetBytes(sn); // нач. уз.
            byte[] nd2 = BitConverter.GetBytes(en); // кон. уз.

            // пишем линию в файл
            lnStr.Write(seg, 0, seg.Length); 
            lnStr.Write(pos, 0, pos.Length);
            lnStr.WriteByte(one);
            lnStr.Write(nd1, 0, nd1.Length);
            lnStr.Write(nd2, 0, nd2.Length);
            lnStr.Flush();

            byte[] linkid = BitConverter.GetBytes(link_id); // LINK_ID
            liStr.Write(linkid, 0, linkid.Length); // пишем LINK_ID

            // пишем tmc код
            if ((shpf.fldTMC != null) && (shpf.fldTMC != String.Empty)) // пишем TMC
            {
                // @E0+000+00000;@E0-000-00000;
                string tmc = data[shpf.fldTMC].ToString();
                bool inverse = false;
                ushort code = 0;
                if (tmc.Length > 10)
                {
                    tmc = tmc.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries)[0];
                    inverse = (tmc.IndexOf("+") > 0) && (tmc.IndexOf("-") > 0);
                    code = (ushort)(int.Parse(tmc.Substring(8)));
                };
                tmcStr.WriteByte(inverse ? (byte)1 : (byte)0);
                byte[] btw = BitConverter.GetBytes(code);
                tmcStr.Write(btw,0,btw.Length);
            };

            // пишем lines names
            if ((shpf.fldName != null) && (shpf.fldName != String.Empty) && (writeLinesNamesFile))
            {
                string nam = data[shpf.fldName].ToString();
                byte[] btw = System.Text.Encoding.GetEncoding(1251).GetBytes(nam + "\r\n");
                lnmsStr.Write(btw, 0, btw.Length);
            };

            // сохраняем RGNode
            if((shpf.fldRGNODE != null) && (shpf.fldRGNODE != String.Empty))
            {                
                string val = data[shpf.fldRGNODE].ToString();
                if(val.Length > 0)
                {
                    bool inner = val.IndexOf("I") >= 0;
                    bool outer = val.IndexOf("O") >= 0;
                    bool begin = val.IndexOf("F") >= 0;
                    int len = 1;
                    int id = 0; int number = 0;
                    while (int.TryParse(val.Substring(1, len++), out id)) number = id;
                    TRGNode rgn = new TRGNode(begin ? sn : en, inner, outer, number, val, begin ? lat1 : lat2, begin ? lon1 : lon2);
                    rgNodes.Add(rgn);
                };
            };

            // пишем сегменты линии
            byte[] lno = BitConverter.GetBytes(LINE); // номер линии
            for (int i = 1; i < line.points.Length; i++)
            {                
                byte[] sno = BitConverter.GetBytes((ushort)i);
                byte[] lt0 = BitConverter.GetBytes((Single)line.points[i - 1].Y);
                byte[] ln0 = BitConverter.GetBytes((Single)line.points[i - 1].X);
                byte[] lt1 = BitConverter.GetBytes((Single)line.points[i].Y);
                byte[] ln1 = BitConverter.GetBytes((Single)line.points[i].X);
                float k = ((Single)line.points[i].X - (Single)line.points[i - 1].X) / (line.points[i].Y - line.points[i-1].Y);
                byte[] ka = BitConverter.GetBytes((Single)k);
                float b = line.points[i - 1].X - k * line.points[i - 1].Y;
                byte[] ba = BitConverter.GetBytes((Single)b);

                sgStr.Write(lno, 0, lno.Length);
                sgStr.Write(sno, 0, sno.Length);
                sgStr.Write(lt0, 0, lt0.Length);
                sgStr.Write(ln0, 0, ln0.Length);
                sgStr.Write(lt1, 0, lt1.Length);
                sgStr.Write(ln1, 0, ln1.Length);
                sgStr.Write(ka, 0, ka.Length);
                sgStr.Write(ba, 0, ba.Length);
                sgStr.Flush();
            };

            Console.SetCursorPosition(0, Console.CursorTop - 1);
            Console.WriteLine("Lines/Segments/Nodes/NoTurns found: " + String.Format("{0}/{1}/{2}/{3}", new object[] { linesCount, segmCount, nodes.Count, noTurns.Count }));
        }

        /// <summary>
        ///     Добавляем узел в список
        /// </summary>
        /// <param name="lat">широта</param>
        /// <param name="lon">долгота</param>
        /// <returns>Номер узла</returns>
        public uint AddNode(float lat, float lon)
        {
            nodes.Add(new TNode((uint)nodes.Count + 1, lat, lon));
            return (uint)nodes.Count;
        }

        /// <summary>
        ///     Находим узел в списке
        /// </summary>
        /// <param name="lat">широта</param>
        /// <param name="lon">долгота</param>
        /// <returns>Номер узла (если 0 - не сущ)</returns>
        public uint NodeByLatLon(float lat, float lon)
        {
            uint res = 0;
            for (int i = 0; i < nodes.Count; i++)
                if ((nodes[i].lat == lat) && (nodes[i].lon == lon))
                {
                    res = nodes[i].node;
                    break;
                };
            return res;
        }

        /// <summary>
        ///     Метод для чтения shp файла
        /// </summary>
        /// <param name="data"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        private int readIntBig(byte[] data, int pos)
        {
            byte[] bytes = new byte[4];
            bytes[0] = data[pos];
            bytes[1] = data[pos + 1];
            bytes[2] = data[pos + 2];
            bytes[3] = data[pos + 3];
            Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        /// <summary>
        ///     Метод для чтения shp файла
        /// </summary>
        /// <param name="data"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        private int readIntLittle(byte[] data, int pos)
        {
            byte[] bytes = new byte[4];
            bytes[0] = data[pos];
            bytes[1] = data[pos + 1];
            bytes[2] = data[pos + 2];
            bytes[3] = data[pos + 3];
            return BitConverter.ToInt32(bytes, 0);
        }

        /// <summary>
        ///     Метод для чтения shp файла
        /// </summary>
        /// <param name="data"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        private double readDoubleLittle(byte[] data, int pos)
        {
            byte[] bytes = new byte[8];
            bytes[0] = data[pos];
            bytes[1] = data[pos + 1];
            bytes[2] = data[pos + 2];
            bytes[3] = data[pos + 3];
            bytes[4] = data[pos + 4];
            bytes[5] = data[pos + 5];
            bytes[6] = data[pos + 6];
            bytes[7] = data[pos + 7];
            return BitConverter.ToDouble(bytes, 0);
        }

        /// <summary>
        ///     Рассчитываем длину в метрах от точки до точки
        /// </summary>
        /// <param name="StartLat">A lat</param>
        /// <param name="StartLong">A lon</param>
        /// <param name="EndLat">B lat</param>
        /// <param name="EndLong">B lon</param>
        /// <param name="radians">Radians or Degrees</param>
        /// <returns>length in meters</returns>
        private static float GetLengthMeters(double StartLat, double StartLong, double EndLat, double EndLong, bool radians)
        {
            return RMGraph.GetLengthMeters(StartLat, StartLong, EndLat, EndLong, radians);
        }
    }
    
    /// <summary>
    ///     Модуль расчета маршрутов графа
    ///     по Алгоритмам Дейкстры, Дейкстры(reversed) и A*  (A* rev)
    /// </summary>
    public class RMGraph
    {
        /// <summary>
        ///     Когда подгружать сегменты линий в память
        /// </summary>
        public enum SegmentsInMemoryPreLoad: byte
        {
            /// <summary>
            ///     Сегменты линий загрущаются в память
            /// </summary>
            inMemoryCalculations,
            /// <summary>
            ///     Сегменты линий не загружаются в память,
            ///     а идет работа с файлом на диске
            /// </summary>
            onDiskCalculations,
        }
                

        //  заголовки файлов
        private static byte[] RMGRAF2 = new byte[] { 0x52, 0x4D, 0x47, 0x52, 0x41, 0x46, 0x32 };
        private static byte[] RMGRAF3 = new byte[] { 0x52, 0x4D, 0x47, 0x52, 0x41, 0x46, 0x33 };
        private static byte[] RMINDEX = new byte[] { 0x52, 0x4D, 0x49, 0x4E, 0x44, 0x45, 0x58 };
        private static byte[] RMLINES = new byte[] { 0x52, 0x4D, 0x4C, 0x49, 0x4E, 0x45, 0x53 };
        private static byte[] RMSEGMENTS = new byte[] { 0x52, 0x4D, 0x53, 0x45, 0x47, 0x4D, 0x45, 0x4E, 0x54, 0x53 };
        private static byte[] RMPOINTNLL0 = new byte[] { 0x52, 0x4D, 0x50, 0x4F, 0x49, 0x4E, 0x54, 0x4E, 0x4C, 0x4C, 0x30 };
        private static byte[] RMPOINTNLL1 = new byte[] { 0x52, 0x4D, 0x50, 0x4F, 0x49, 0x4E, 0x54, 0x4E, 0x4C, 0x4C, 0x31 };
        private static byte[] RMLINKIDS = new byte[] { 0x52, 0x4D, 0x4C, 0x49, 0x4E, 0x4B, 0x49, 0x44, 0x53 };
        private static byte[] RMTURNRSTR = new byte[] { 0x52, 0x4D, 0x54, 0x55, 0x52, 0x4E, 0x52, 0x53, 0x54, 0x52 };
        private static byte[] RMEXTCOSTS = new byte[] { 0x52, 0x4D, 0x45, 0x58, 0x54, 0x43, 0x4F, 0x53, 0x54, 0x53 };

        private const byte line_record_length = 15;
        private const byte segm_record_length = 30;

        /// <summary>
        ///     Загружать сегменты линий в память или нет
        /// </summary>
        private SegmentsInMemoryPreLoad inMemSeg = SegmentsInMemoryPreLoad.inMemoryCalculations;
        /// <summary>
        ///     Загружать сегменты линий в память или нет
        /// </summary>
        public SegmentsInMemoryPreLoad PreLoadedLineSegments { get { return inMemSeg; } }

        /// <summary>
        ///     Строим путь от обратного?
        /// </summary>
        private bool calcReversed = false;
        /// <summary>
        ///     Строим путь от обратного?
        ///     исп-ся для пути ИЗ МНОГИХ в ОДНУ (x[] --> y)
        /// </summary>
        public bool CalcReversed { get { return calcReversed; } }

        /// <summary>
        ///     Критерий оптимизации маршрута
        /// </summary>
        private MinimizeBy minBy = MinimizeBy.Cost;
        /// <summary>
        ///     Критерий оптимизации маршрута
        /// </summary>
        public MinimizeBy MinimizeRouteBy { get { return minBy; } set { minBy = value; } }

        /// <summary>
        ///     Максимальная длина между узлами графа
        /// </summary>
        private Single maxDistBetweenNodes = 0;
        /// <summary>
        ///     Максимальная длина между узлами графа
        /// </summary>
        public Single MaxDistanceBetweenNodes { get { return maxDistBetweenNodes; } }

        /// <summary>
        ///     Максимальная ошибка для расчетов (исп-ся для определения незаполненных клеток)
        /// </summary>
        private const Single maxError = (Single)1e-6;
        /// <summary>
        ///     Максимальное значение оценки, больше которого оценка является бесконечной
        /// </summary>
        private const Single maxValue = (Single)1e+30;

        /// <summary>
        ///     Смещение следующего ближайшего узла в матрице относительно начала элемента массива
        /// </summary>
        private const byte nextOffset = 0;
        /// <summary>
        ///     Смещение оценки пути относительно начала элемента массива
        /// </summary>
        private const byte costOffset = 4;
        /// <summary>
        ///     Смещение длины пути относительно начала элемента массива
        /// </summary>
        private const byte distOffset = 8;
        /// <summary>
        ///     Смещение времени в пути относительно начала элемента массива
        /// </summary>
        private const byte timeOffset = 12;
        /// <summary>
        ///     Смещение линии до точки
        /// </summary>
        private const byte lineOffset = 16;
        /// <summary>
        ///     Смещение реверса линии до точки
        /// </summary>
        private const byte revOffset = 20;


        /// <summary>
        ///     Размер ячейки графа
        /// </summary>
        private const int vtGraphElemLength = 4 + 4 + 4 + 4 + 4 + 1; // next_node cost dist time line rev

        /// <summary>
        ///     Размер ячейки вектора
        /// </summary>
        private const int vtArrElemLength = 4 + 4 + 4 + 4; // next_node cost dist time

        /// <summary>
        ///     В памяти ли граф или в файле
        /// </summary>
        private bool inMemory = false;

        /// <summary>
        ///     Число узлов в графе
        /// </summary>
        private int size = 0;
        /// <summary>
        ///     Число точек в графе
        /// </summary>
        private int lines = 0;
        /// <summary>
        ///     Число сегментов в графе
        /// </summary>
        private int segments = 0;
        
        /// <summary>
        ///     Указатель на файл графа
        /// </summary>
        private Stream graph_str = null;
        /// <summary>
        ///     Указатель на инверсный файл графа
        /// </summary>
        private Stream graphR_str = null;
        /// <summary>
        ///     Указатель на индексный файл графа
        /// </summary>
        private Stream index_str = null;
        /// <summary>
        ///     Указатель на индексный файл инверсного графа
        /// </summary>
        private Stream indexR_str = null;
        /// <summary>
        ///     Указатель на файл линий
        /// </summary>
        private Stream lines_str = null;
        /// <summary>
        ///     Указатель на файл сегментов
        /// </summary>
        private Stream segm_str = null;
        /// <summary>
        ///     Указатель на файл координат графа
        /// </summary>
        private Stream geo_str = null;
        /// <summary>
        ///     Указатель на индексный файл координат графа
        /// </summary>
        private Stream geoll_str = null;
       
        /// <summary>
        ///     Открытый вектор, содержащий информацию о всех конечныйх точках (узлах) и маршрутах
        /// </summary>
        private Stream vector_str = null;      

        /// <summary>
        ///     Имя файла графа
        /// </summary>
        private string fileName = null;
        /// <summary>
        ///     Префикс имен файла графа
        /// </summary>
        private string fileMain = null;

        /// <summary>
        ///     Число узлов в графе
        /// </summary>
        public int NodesCount { get { return size; } }
        /// <summary>
        ///     Число линий в графе
        /// </summary>
        public int LinesCount { get { return lines; } }
        /// <summary>
        ///    Общее число сегментов в графе
        /// </summary>
        public int SegmentsCount { get { return segments; } }

        /// <summary>
        ///     Private only
        /// </summary>
        private RMGraph() { }

        /// <summary>
        ///     В памяти ли граф или в файле
        /// </summary>
        public bool InMemory { get { return inMemory; } }

        /// <summary>
        ///     Имя файла графа
        /// </summary>
        public string FileName { get { return fileName; } }

        /// <summary>
        ///     Информация о поправках cost для расчета маршрута (трафик)
        /// </summary>
        private LineExtCostInfo[] extCostInfo = null;

        /// <summary>
        ///     Используется ли внешняя поправочная информация для оценки пути (трафик)
        /// </summary>
        public bool ExternalCostInfoUsed { get { return extCostInfo != null && extCostInfo.Length > 0; } }

        /// <summary>
        ///     Информация о поправках time для расчета маршрута (трафик)
        /// </summary>
        private LineExtCostInfo[] extTimeInfo = null;

        /// <summary>
        ///     Используется ли внешняя поправочная информация для времени в пути (трафик)
        /// </summary>
        public bool ExternalTimeInfoUsed { get { return extTimeInfo != null && extTimeInfo.Length > 0; } }

        /// <summary>
        ///     В течении какого времени от начала движения используется
        ///     внешняя информация о добавочной оценки передвижения для
        ///     построения маршрута (информация о трафике используется
        ///     в первые extCostTimeLimit минут пути)
        /// </summary>
        private int extCostTimeLimit = 25;

        /// <summary>
        ///     В течении какого времени от начала движения используется
        ///     внешняя информация о добавочной оценки передвижения для
        ///     построения маршрута (информация о трафике используется
        ///     в первые ExternalCostTimeLimit минут пути)
        /// </summary>
        public int ExternalCostTimeLimit { get { return extCostTimeLimit; } set { extCostTimeLimit = value; } }

        /// <summary>
        ///     Загружаем граф из файла для работы с ним на диске
        /// </summary>
        /// <param name="fileName">Имя файла для хранения графа</param>
        /// <returns></returns>
        public static RMGraph WorkWithDisk(string fileName)
        {
            RMGraph v = new RMGraph();
            v.inMemSeg = SegmentsInMemoryPreLoad.onDiskCalculations;
            v.fileName = fileName;
            v.fileMain = fileName.Substring(0, fileName.LastIndexOf("."));
            v.graph_str = new FileStream(v.fileMain + ".graph.bin", FileMode.Open, FileAccess.Read);
            byte[] bb = new byte[RMGRAF2.Length];
            v.graph_str.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMGRAF2")
            {
                v.graph_str.Close();
                throw new IOException("Unknown file format:\r\n" + v.fileMain + ".graph.bin");
            };
            bb = new byte[4];
            v.graph_str.Read(bb, 0, 4);
            v.size = BitConverter.ToInt32(bb, 0);
            v.graph_str.Read(bb, 0, 4);
            v.maxDistBetweenNodes = BitConverter.ToSingle(bb, 0);

            v.graphR_str = new FileStream(v.fileMain + ".graph[r].bin", FileMode.Open, FileAccess.Read);
            bb = new byte[RMGRAF3.Length];
            v.graphR_str.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMGRAF3")
            {
                v.graph_str.Close();
                v.graphR_str.Close();
                throw new IOException("Unknown file format:\r\n" + v.fileMain + ".graph[r].bin");
            };

            v.index_str = new FileStream(v.fileMain + ".graph.bin.in", FileMode.Open, FileAccess.Read);
            bb = new byte[RMINDEX.Length];
            v.index_str.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMINDEX")
            {
                v.graph_str.Close();
                v.graphR_str.Close();
                v.index_str.Close();
                throw new Exception("Unknown file format:\r\n" + v.fileMain + ".graph.bin.in");
            };

            v.indexR_str = new FileStream(v.fileMain + ".graph[r].bin.in", FileMode.Open, FileAccess.Read);
            bb = new byte[RMINDEX.Length];
            v.indexR_str.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMINDEX")
            {
                v.graph_str.Close();
                v.graphR_str.Close();
                v.index_str.Close();
                v.indexR_str.Close();
                throw new Exception("Unknown file format:\r\n" + v.fileMain + ".graph[r].bin.in");
            };

            v.lines_str = new FileStream(v.fileMain + ".lines.bin", FileMode.Open, FileAccess.Read);
            bb = new byte[RMLINES.Length];
            v.lines_str.Read(bb,0,bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMLINES")
            {
                v.graph_str.Close();
                v.graphR_str.Close();
                v.index_str.Close();
                v.indexR_str.Close();
                v.lines_str.Close();
                throw new Exception("Unknown file format:\r\n" + v.fileMain + ".lines.bin");
            };
            bb = new byte[4];
            v.lines_str.Read(bb, 0, bb.Length);
            v.lines = BitConverter.ToInt32(bb, 0);

            v.segm_str = new FileStream(v.fileMain + ".segments.bin", FileMode.Open, FileAccess.Read);
            bb = new byte[RMSEGMENTS.Length];
            v.segm_str.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMSEGMENTS")
            {
                v.graph_str.Close();
                v.graphR_str.Close();
                v.index_str.Close();
                v.indexR_str.Close();
                v.lines_str.Close();
                v.segm_str.Close();
                throw new Exception("Unknown file format:\r\n" + v.fileMain + ".segments.bin");
            };
            bb = new byte[4];
            v.segm_str.Read(bb, 0, bb.Length);
            v.segments = BitConverter.ToInt32(bb, 0);

            //////////

            v.geo_str = new FileStream(v.fileMain + ".graph.geo", FileMode.Open, FileAccess.Read);
            byte[] ba = new byte[RMPOINTNLL0.Length];
            v.geo_str.Read(ba, 0, ba.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(ba) != "RMPOINTNLL0")
            {
                v.graph_str.Close();
                v.graphR_str.Close();
                v.index_str.Close();
                v.indexR_str.Close();
                v.lines_str.Close();
                v.segm_str.Close();
                v.geo_str.Close();
                throw new IOException("Unknown file format:\r\n" + v.fileMain + ".graph.geo");
            };

            v.geoll_str = new FileStream(v.fileMain + ".graph.geo.ll", FileMode.Open, FileAccess.Read);
            ba = new byte[RMPOINTNLL1.Length];
            v.geoll_str.Read(ba, 0, ba.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(ba) != "RMPOINTNLL1")
            {
                v.graph_str.Close();
                v.graphR_str.Close();
                v.index_str.Close();
                v.indexR_str.Close();
                v.lines_str.Close();
                v.segm_str.Close();
                v.geo_str.Close();
                throw new IOException("Unknown file format:\r\n" + v.fileMain + ".graph.geo.ll");
            };

            return v;
        }

        /// <summary>
        ///     Загружаем граф из файла в память для работы с ним
        /// </summary>
        /// <param name="fileName">Имя файла</param>
        /// <returns></returns>
        public static RMGraph LoadToMemory(string fileName)
        {
            return LoadToMemory(fileName, SegmentsInMemoryPreLoad.inMemoryCalculations);
        }

        /// <summary>
        ///     Загружаем граф из файла в память для работы с ним
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="preload"></param>
        /// <returns></returns>
        public static RMGraph LoadToMemory(string fileName, SegmentsInMemoryPreLoad preload)
        {
            RMGraph v = new RMGraph();
            v.inMemSeg = preload;
            v.fileName = fileName;
            v.fileMain = fileName.Substring(0, fileName.LastIndexOf("."));
            v.inMemory = true;
            v.graph_str = new MemoryStream();

            FileStream fs = new FileStream(v.fileMain + ".graph.bin", FileMode.Open, FileAccess.Read);
            byte[] block = new byte[8192];
            int read = 0;
            while ((read = fs.Read(block, 0, 8192)) > 0)
                v.graph_str.Write(block, 0, read);
            fs.Close();

            v.graph_str.Position = 0;
            byte[] bb = new byte[RMGRAF2.Length];
            v.graph_str.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMGRAF2")
            {
                v.graph_str.Close();
                throw new IOException("Unknown file format:\r\n" + v.fileMain + ".graph.bin");
            };

            v.graph_str.Position = RMGRAF2.Length;
            v.graph_str.Read(block, 0, 4);
            v.size = BitConverter.ToInt32(block, 0);
            v.graph_str.Read(block, 0, 4);
            v.maxDistBetweenNodes = BitConverter.ToSingle(block, 0);

            v.graphR_str = new MemoryStream();
            fs = new FileStream(v.fileMain + ".graph[r].bin", FileMode.Open, FileAccess.Read);
            block = new byte[8192];
            read = 0;
            while ((read = fs.Read(block, 0, 8192)) > 0)
                v.graphR_str.Write(block, 0, read);
            fs.Close();

            v.graphR_str.Position = 0;
            bb = new byte[RMGRAF3.Length];
            v.graphR_str.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMGRAF3")
            {
                v.graph_str.Close();
                v.graphR_str.Close();
                throw new IOException("Unknown file format:\r\n" + v.fileMain + ".graph[r].bin");
            };

            v.index_str = new MemoryStream();
            fs = new FileStream(v.fileMain + ".graph.bin.in", FileMode.Open, FileAccess.Read);
            block = new byte[8192];
            read = 0;
            while ((read = fs.Read(block, 0, 8192)) > 0)
                v.index_str.Write(block, 0, read);
            fs.Close();

            v.index_str.Position = 0;
            bb = new byte[RMINDEX.Length];
            v.index_str.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMINDEX")
            {
                v.graph_str.Close();
                v.graphR_str.Close();
                v.index_str.Close();
                throw new Exception("Unknown file format:\r\n" + v.fileMain + ".graph.bin.in");
            };

            v.indexR_str = new MemoryStream();
            fs = new FileStream(v.fileMain + ".graph[r].bin.in", FileMode.Open, FileAccess.Read);
            block = new byte[8192];
            read = 0;
            while ((read = fs.Read(block, 0, 8192)) > 0)
                v.indexR_str.Write(block, 0, read);
            fs.Close();

            v.indexR_str.Position = 0;
            bb = new byte[RMINDEX.Length];
            v.indexR_str.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMINDEX")
            {
                v.graph_str.Close();
                v.graphR_str.Close();
                v.index_str.Close();
                v.indexR_str.Close();
                throw new Exception("Unknown file format:\r\n" + v.fileMain + ".graph[r].bin.in");
            };

            v.lines_str = new MemoryStream();
            fs = new FileStream(v.fileMain + ".lines.bin", FileMode.Open, FileAccess.Read);
            block = new byte[8192];
            read = 0;
            while ((read = fs.Read(block, 0, 8192)) > 0)
                v.lines_str.Write(block, 0, read);
            fs.Close();

            v.lines_str.Position = 0;
            bb = new byte[RMLINES.Length];
            v.lines_str.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMLINES")
            {
                v.graph_str.Close();
                v.graphR_str.Close();
                v.index_str.Close();
                v.indexR_str.Close();
                v.lines_str.Close();
                throw new Exception("Unknown file format:\r\n" + v.fileMain + ".lines.bin" );
            };
            bb = new byte[4];
            v.lines_str.Read(bb, 0, bb.Length);
            v.lines = BitConverter.ToInt32(bb, 0);

            if (v.inMemSeg == SegmentsInMemoryPreLoad.inMemoryCalculations)
            {
                v.segm_str = new MemoryStream();
                fs = new FileStream(v.fileMain + ".segments.bin", FileMode.Open, FileAccess.Read);
                block = new byte[8192];
                read = 0;
                while ((read = fs.Read(block, 0, 8192)) > 0)
                    v.segm_str.Write(block, 0, read);
                fs.Close();
            }
            else
            {
                v.segm_str = new FileStream(v.fileMain + ".segments.bin", FileMode.Open, FileAccess.Read);
            };


            v.segm_str.Position = 0;
            bb = new byte[RMSEGMENTS.Length];
            v.segm_str.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMSEGMENTS")
            {
                v.graph_str.Close();
                v.graphR_str.Close();
                v.index_str.Close();
                v.indexR_str.Close();
                v.lines_str.Close();
                v.segm_str.Close();
                throw new Exception("Unknown file format:\r\n" + v.fileMain + ".segments.bin");
            };
            bb = new byte[4];
            v.segm_str.Read(bb, 0, bb.Length);
            v.segments = BitConverter.ToInt32(bb, 0);

            //////////

            v.geo_str = new MemoryStream();
            v.geoll_str = new MemoryStream();

            fs = new FileStream(v.fileMain + ".graph.geo", FileMode.Open, FileAccess.Read);
            block = new byte[8192];
            read = 0;
            while ((read = fs.Read(block, 0, 8192)) > 0)
                v.geo_str.Write(block, 0, read);
            fs.Close();

            v.geo_str.Position = 0;
            byte[] ba = new byte[RMPOINTNLL0.Length];
            v.geo_str.Read(ba, 0, ba.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(ba) != "RMPOINTNLL0")
            {
                v.graph_str.Close();
                v.graphR_str.Close();
                v.index_str.Close();
                v.indexR_str.Close();
                v.lines_str.Close();
                v.segm_str.Close();
                v.geo_str.Close();
                throw new IOException("Unknown file format:\r\n" + v.fileMain + ".graph.geo");
            };

            fs = new FileStream(v.fileMain + ".graph.geo.ll", FileMode.Open, FileAccess.Read);
            block = new byte[8192];
            read = 0;
            while ((read = fs.Read(block, 0, 8192)) > 0)
                v.geoll_str.Write(block, 0, read);
            fs.Close();

            v.geoll_str.Position = 0;
            ba = new byte[RMPOINTNLL1.Length];
            v.geoll_str.Read(ba, 0, ba.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(ba) != "RMPOINTNLL1")
            {
                v.graph_str.Close();
                v.graphR_str.Close();
                v.index_str.Close();
                v.indexR_str.Close();
                v.lines_str.Close();
                v.segm_str.Close();
                v.geo_str.Close();
                v.geoll_str.Close();
                throw new IOException("Unknown file format:\r\n" + v.fileMain + ".graph.geo.ll");
            };

            return v;
        }

        /// <summary>
        ///     Вызвать после окончания работы с графом для высвобождения всех ресурсов
        /// </summary>
        public void Close()
        {            
            graph_str.Close();
            graph_str = null;

            index_str.Close();
            index_str = null;

            graphR_str.Close();
            graphR_str = null;

            indexR_str.Close();
            indexR_str = null;

            lines_str.Close();
            lines_str = null;

            segm_str.Close();
            segm_str = null;

            geo_str.Close();
            geo_str = null;

            geoll_str.Close();
            geoll_str = null;
        }

        ~RMGraph()
        {
            if (graph_str != null) Close();
        }

        /// <summary>
        ///     Загружаем файл добавочных оценок для расчета маршрута (трафик)
        /// </summary>
        /// <param name="fileName"></param>
        public void LoadExternalCostInfoFile(string fileName)
        {
            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            byte[] ba = new byte[RMEXTCOSTS.Length];
            fs.Read(ba, 0, ba.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(ba) != "RMEXTCOSTS")
            {
                fs.Close();
                throw new IOException("Unknown file format:\r\n" + fileName);
            };
            ba = new byte[4];
            fs.Read(ba, 0, ba.Length);
            extCostInfo = new LineExtCostInfo[BitConverter.ToInt32(ba,0)];
            ba = new byte[8192];
            int i = 0;
            int read = 0;
            while ((read = fs.Read(ba, 0, ba.Length)) > 0)
            {
                int cp = 0;
                while (cp < read)
                {
                    extCostInfo[i++] = new LineExtCostInfo(BitConverter.ToSingle(ba, cp), BitConverter.ToSingle(ba, cp + 4));
                    cp += 8;
                };
            };
            fs.Close();
        }

        /// <summary>
        ///     Удаляем информацию о добавочных оценках для расчета маршрута (трафик)
        /// </summary>
        public void RemoveExternalCostInfo()
        {
            extCostInfo = null;
        }

        /// <summary>
        ///     Загружаем файл добавочного времени для расчета маршрута (трафик)
        /// </summary>
        /// <param name="fileName"></param>
        public void LoadExternalTimeInfoFile(string fileName)
        {
            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            byte[] ba = new byte[RMEXTCOSTS.Length];
            fs.Read(ba, 0, ba.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(ba) != "RMEXTCOSTS")
            {
                fs.Close();
                throw new IOException("Unknown file format:\r\n" + fileName);
            };
            ba = new byte[4];
            fs.Read(ba, 0, ba.Length);
            extTimeInfo = new LineExtCostInfo[BitConverter.ToInt32(ba, 0)];
            ba = new byte[8192];
            int i = 0;
            int read = 0;
            while ((read = fs.Read(ba, 0, ba.Length)) > 0)
            {
                int cp = 0;
                while (cp < read)
                {
                    extTimeInfo[i++] = new LineExtCostInfo(BitConverter.ToSingle(ba, cp), BitConverter.ToSingle(ba, cp + 4));
                    cp += 8;
                };
            };
            fs.Close();
        }

        /// <summary>
        ///     Удаляем информацию о добавочном времени для расчета маршрута (трафик)
        /// </summary>
        public void RemoveExternalTimeInfo()
        {
            extTimeInfo = null;
        }

        /// <summary>
        ///     Ищем заданный узел в графе и возвращаем информацию о ближайших точках
        /// </summary>
        /// <param name="i">Узел который надо найти</param>
        /// <param name="_n">Массив узлов в которые можно добраться</param>
        /// <param name="_c">Оценка путей в которые можно добраться</param>
        /// <param name="_d">Длины путей в которые можно добраться</param>
        /// <param name="_t">Время движения до узлов в которые можно добраться</param>
        /// <param name="_l">Линии по которым можно добраться</param>
        /// <param name="_r">Направление линий по которым можно добраться</param>
        /// <returns>Число ближайших исходящих связей</returns>
        private int SelectNode(uint i, out uint[] _n, out Single[] _c, out Single[] _d, out Single[] _t, out uint[] _l, out byte[] _r)
        {
            index_str.Position = 4 * (i - 1) + RMINDEX.Length + 4;
            byte[] ib = new byte[4];
            index_str.Read(ib, 0, ib.Length);
            uint pos_in_graph = BitConverter.ToUInt32(ib, 0);

            byte nc = 0;
            byte[] dd = new byte[vtGraphElemLength];
            graph_str.Position = pos_in_graph;
            nc = (byte)graph_str.ReadByte();

            _n = new uint[nc];
            _c = new Single[nc];
            _d = new Single[nc];
            _t = new Single[nc];
            _l = new uint[nc];
            _r = new byte[nc];

            for (int x = 0; x < nc; x++)
            {
                graph_str.Read(dd, 0, dd.Length);
                _n[x] = BitConverter.ToUInt32(dd, nextOffset);
                _c[x] = BitConverter.ToSingle(dd, costOffset);
                _d[x] = BitConverter.ToSingle(dd, distOffset);
                _t[x] = BitConverter.ToSingle(dd, timeOffset);
                _l[x] = BitConverter.ToUInt32(dd, lineOffset);
                _r[x] = dd[revOffset];                
            };

            return nc;
        }

        /// <summary>
        ///     Получаем оценку пути из точки (X)->Y
        ///     Вызывается только между методами BeginSolve и EndSolve
        /// </summary>
        /// <param name="y">конечная точка</param>
        /// <returns>оценка</returns>
        private Single GetCost(uint y)
        {
            vector_str.Position = (y - 1) * (vtArrElemLength) + costOffset;
            byte[] bb = new byte[4];
            vector_str.Read(bb, 0, 4);
            Single d = BitConverter.ToSingle(bb, 0);
            if (d < maxError)
                return Single.MaxValue;
            else
                return d;
        }

        /// <summary>
        ///     Устанавливаем оценку пути из точки (X)->Y
        ///     Вызывается только между методами BeginSolve и EndSolve
        /// </summary>
        /// <param name="y">конечная точка</param>
        private void SetCost(uint y, Single cost)
        {
            vector_str.Position = (y - 1) * (vtArrElemLength) + costOffset;
            byte[] bb = BitConverter.GetBytes(cost);
            vector_str.Write(bb, 0, 4);
        }
                
        /// <summary>
        ///     Устанавливаем предшествующую точку previous пути из (X)->Y
        ///     Вызывается только между методами BeginSolve и EndSolve
        /// </summary>
        /// <param name="y">конечная точка</param>
        /// <param name="previous">конечная точка - 1</param>
        private void SetPrev(uint y, uint previous)
        {
            vector_str.Position = (y - 1) * (vtArrElemLength) + nextOffset;
            byte[] bb = BitConverter.GetBytes(previous);
            vector_str.Write(bb, 0, 4);
        }

        /// <summary>
        ///     Получаем предшествующую точку пути из (X)->Y
        ///     Вызывается только между методами BeginSolve и EndSolve
        /// </summary>
        /// <param name="y">конечная точка</param>
        /// <returns>конечная точка - 1</returns>
        private uint GetPrev(uint y)
        {
            vector_str.Position = (y - 1) * (vtArrElemLength) + nextOffset;
            byte[] bb = new byte[4];
            vector_str.Read(bb, 0, 4);
            return BitConverter.ToUInt32(bb, 0);
        }

        /// <summary>
        ///     Получаем память или место на диске, а также
        ///     подготавливаем ресурсы для нахождения пути
        ///     Вызывается перед методом Solve и всеми Get... методами
        /// </summary>
        /// <param name="inMemory">Считаем в памяти или на диске</param>
        /// <param name="fileName">fileName? если inMemory = false</param>
        public void BeginSolve(bool inMemory, string fileName)
        {
            if (vector_str != null) vector_str.Close();

            if ((!inMemory) && (fileName == null)) throw new IOException("filename not specified");

            if (inMemory)
                vector_str = new MemoryStream();
            else
                vector_str = new FileStream(fileName, FileMode.Create);

            vector_str.SetLength(this.size * (vtArrElemLength));
        }

        /// <summary>
        ///     Высвобождаем память и ресурсы после нахождения пути
        ///     Вызывается после метода Solve и всех Get... методов
        /// </summary>
        public void EndSolve()
        {
            if (vector_str == null) throw new Exception("Call BeginSolve first");
            vector_str.Close();
            vector_str = null;
        }

        /// <summary>
        ///     Добавление поправки на оценку из внешнего источника
        /// </summary>
        /// <param name="x">Начало отрезка</param>
        /// <param name="y">Конец отрезка</param>
        /// <param name="line">Номер линии</param>
        /// <param name="reverse">В каком направлении двигаемя</param>
        /// <param name="elapsed">Сколько времени прошло от начала движения</param>
        /// <returns>поправка оценки</returns>
        private Single AddExternalCostError(uint x, uint y, uint line, bool reverse, Single elapsed)
        {
            if ((extCostInfo == null) || (extCostInfo.Length == 0)) return 0;
            if (elapsed > extCostTimeLimit) return 0;
            return reverse ? extCostInfo[line - 1].costR : extCostInfo[line - 1].costN;
        }

        /// <summary>
        ///     Добавление поправки на время из внешнего источника для отрезка
        /// </summary>
        /// <param name="x">Начало отрезка</param>
        /// <param name="y">Конец отрезка</param>
        /// <param name="line">Номер линии</param>
        /// <param name="reverse">В каком направлении двигаемя</param>
        /// <param name="elapsed">Сколько времени прошло от начала движения</param>
        /// <returns>поправка времени</returns>
        private Single AddExternalTimeDelay(uint x, uint y, uint line, bool reverse, Single elapsed)
        {
            if ((extTimeInfo == null) || (extTimeInfo.Length == 0)) return 0;
            if (elapsed > extCostTimeLimit) return 0;
            return reverse ? extTimeInfo[line - 1].costR : extTimeInfo[line - 1].costN;
        }

        /// <summary>
        ///     Расчет пути из точек starts в точку end
        /// </summary>
        /// <param name="starts">Точки старта</param>
        /// <param name="end">Точка назначения</param>
        public void SolveDeikstra(uint[] starts, uint end)
        {            
            Stream tmp_g = graph_str;
            Stream tmp_i = index_str;
            graph_str = graphR_str;
            index_str = indexR_str;

            SolveDeikstra(end, starts, true);
            
            graph_str = tmp_g;
            index_str = tmp_i;            
        }

        /// <summary>
        ///     Расчет пути из точки start во все или в каждую из ends
        /// </summary>
        /// <param name="start">Точка старта</param>
        /// <param name="ends">Точки назначения</param>
        public void SolveDeikstra(uint start, uint[] ends)
        {
            SolveDeikstra(start, ends, false);
        }

        /// <summary>
        ///     Расчет пути из точки start во все или в каждую из ends
        /// </summary>
        /// <param name="start">Точка старта</param>
        /// <param name="ends">Точки назначения</param>
        /// <param name="reversed">Использовать нахождение пути от обратного</param>
        public void SolveDeikstra(uint start, uint[] ends, bool reversed)
        {
            if (vector_str == null) throw new Exception("Call BeginSolve first");
            calcReversed = reversed;

            PointF start_latlon = GetNodeLatLon(start);                        

            uint[] _n; Single[] _c; Single[] _d; Single[] _t; uint[] _l; byte[] _r;            

            List<uint> togoList = new List<uint>();
            togoList.Add(start);
            
            List<float> togoHeurist = new List<float>();
            togoHeurist.Add(0);

            List<uint> wasList = new List<uint>();

            List<uint> finishList = new List<uint>();
            if (ends != null) finishList.AddRange(ends);
            int founded = 0;

            while (togoList.Count > 0)
            {
                // Блок оптимизации ((dkxce))
                // выбираем наиближайшую точку к началу из всех которые предстоит пройти по оценке
                // можно сказать что в каждой точке оптимальный маршрут именно для этой точки
                float heuristValue = float.MaxValue;
                uint curr_node = 0;
                int index = -1;
                for (int i = 0; i < togoList.Count; i++) // select closest point to yy
                    if (togoHeurist[i] < heuristValue)
                    {
                        heuristValue = togoHeurist[i];
                        curr_node = togoList[i];
                        index = i;
                    };

                togoList.RemoveAt(index); // remove closest point from togoList
                togoHeurist.RemoveAt(index);

                wasList.Add(curr_node); // add point to wasList
                if (SelectNode(curr_node, out _n, out _c, out _d, out _t, out _l, out _r) > 0)
                {                    
                    Single curr_cost_xi = start == curr_node ? 0 : GetCost(curr_node);
                    Single curr_dist_xi = start == curr_node ? 0 : GetRouteDistance(curr_node,curr_node);
                    Single curr_time_xi = start == curr_node ? 0 : GetRouteTime(curr_node,curr_node);
                    for (uint next_node_i = 0; next_node_i < _n.Length; next_node_i++)
                    {
                        uint next_node = _n[next_node_i];
                        if (wasList.Contains(next_node)) continue;

                        bool update = false;

                        Single cost_from_st = curr_cost_xi + _c[next_node_i];// + CallExternalCostService(curr_node, next_node, _l[next_node_i], _r[next_node_i] == 1 ? true : false, curr_time_xi);
                        Single dist_from_st = curr_dist_xi + _d[next_node_i];
                        Single time_from_st = curr_time_xi + _t[next_node_i];// + CallExternalTimeService(curr_node, next_node, _l[next_node_i], _r[next_node_i] == 1 ? true : false, curr_time_xi);

                        int index_in_togo = togoList.IndexOf(next_node);
                        if (index_in_togo < 0)
                        {
                            index_in_togo = togoList.Count;
                            togoList.Add(next_node);
                            togoHeurist.Add(0);
                            update = true;
                        };

                        switch (minBy)
                        {
                            case MinimizeBy.Cost:
                                Single xyc = start == next_node ? 0 : GetCost(next_node);
                                update = update || (cost_from_st < xyc);
                                heuristValue = cost_from_st;
                                break;
                            case MinimizeBy.Dist:
                                Single xyd = start == next_node ? 0 : GetRouteDistance(next_node, next_node);
                                update = update || (dist_from_st < xyd);
                                heuristValue = dist_from_st;
                                break;
                            case MinimizeBy.Time:
                                Single xyt = start == next_node ? 0 : GetRouteTime(next_node, next_node);
                                update = update || (time_from_st < xyt);
                                heuristValue = time_from_st;
                                break;
                        };    

                        if (update)
                        {
                            SetCost(next_node, cost_from_st);
                            SetPrev(next_node, curr_node);
                            SetRouteDistance(next_node, dist_from_st);
                            SetRouteTime(next_node, time_from_st);                            

                            togoHeurist[index_in_togo] = heuristValue;
                        };
                    };
                };
                if (finishList.Count > 0)
                {
                    if (finishList.Contains(curr_node)) 
                        founded++;
                    if (founded == finishList.Count) 
                        togoList.Clear();
                };
            };

            return;
        }

        /// <summary>
        ///     Расчет пути из точки start в точку end
        ///     по Алгоритму A*
        /// </summary>
        /// <param name="start">Точка старта</param>
        /// <param name="end">Точка конца</param>
        public void SolveAstar(uint start, uint end)
        {
            SolveAstar(start, end, false);
        }

        /// <summary>
        ///     Расчет пути из точки start в точку end от конца
        ///     по Алгоритму A* от конца
        /// </summary>
        /// <param name="start">Точка старта</param>
        /// <param name="end">Точка конца</param>
        public void SolveAstarRev(uint start, uint end)
        {
            Stream tmp_g = graph_str;
            Stream tmp_i = index_str;
            graph_str = graphR_str;
            index_str = indexR_str;

            SolveAstar(end, start, true);

            graph_str = tmp_g;
            index_str = tmp_i;    
        }

        // Алгоритм поиска A*
        // http://ru.wikipedia.org/wiki/%D0%90%D0%BB%D0%B3%D0%BE%D1%80%D0%B8%D1%82%D0%BC_%D0%BF%D0%BE%D0%B8%D1%81%D0%BA%D0%B0_A*
        /// <summary>
        ///     Расчет пути из точки start в точку end
        ///     по Алгоритму A*
        /// </summary>
        /// <param name="start">start</param>
        /// <param name="end">end</param>
        /// <param name="reversed">Использовать нахождение пути от обратного</param>
        private void SolveAstar(uint start, uint end, bool reversed)
        {
            if (vector_str == null) throw new Exception("Call BeginSolve first");
            calcReversed = reversed;

            PointF first_node_latlon = GetNodeLatLon(start);
            PointF last_node_latlon = GetNodeLatLon(end);

            uint[] _n; Single[] _c; Single[] _d; Single[] _t; uint[] _l; byte[] _r;

            float heuristValue = GetLengthMeters(first_node_latlon.Y, first_node_latlon.X, last_node_latlon.Y, last_node_latlon.X, false);
            if (minBy == MinimizeBy.Time) heuristValue = heuristValue / 1000; // 60 kmph average speed            

            List<uint> togoList = new List<uint>();
            togoList.Add(start);
            
            List<float> togoHeurist = new List<float>();
            togoHeurist.Add(heuristValue);
            
            List<uint> wasList = new List<uint>();

            while (togoList.Count > 0)
            {
                float curr_heuVal = float.MaxValue;
                uint curr_node = 0;
                int index = -1;
                for (int i = 0; i < togoList.Count; i++) // select closest point to yy
                    if (togoHeurist[i] < curr_heuVal)
                    {
                        curr_heuVal = togoHeurist[i];
                        curr_node = togoList[i];
                        index = i;
                    };

                togoList.RemoveAt(index); // remove closest point from togoList
                togoHeurist.RemoveAt(index);

                if (curr_node == end) return; // !!! DONE !!! // end node found

                wasList.Add(curr_node); // remove closest point from togoList
                if (SelectNode(curr_node, out _n, out _c, out _d, out _t, out _l, out _r) > 0)
                {
                    Single curr_cost_xi = start == curr_node ? 0 : GetCost(curr_node);
                    Single curr_dist_xi = start == curr_node ? 0 : GetRouteDistance(curr_node, curr_node);
                    Single curr_time_xi = start == curr_node ? 0 : GetRouteTime(curr_node, curr_node);

                    for (uint next_node_i = 0; next_node_i < _n.Length; next_node_i++)
                    {                        
                        uint next_node = _n[next_node_i];                        
                        if (wasList.Contains(next_node)) continue;

                        bool update = false;

                        Single cost_from_st = curr_cost_xi + _c[next_node_i];// + CallExternalCostService(curr_node, next_node, _l[next_node_i], _r[next_node_i] == 1 ? true : false, curr_time_xi);
                        Single dist_from_st = curr_dist_xi + _d[next_node_i];
                        Single time_from_st = curr_time_xi + _t[next_node_i];// + CallExternalTimeService(curr_node, next_node, _l[next_node_i], _r[next_node_i] == 1 ? true : false, curr_time_xi);

                        PointF next_node_latlon = GetNodeLatLon(next_node);
                        float next_node_dist_to_end = GetLengthMeters(next_node_latlon.Y, next_node_latlon.X, last_node_latlon.Y, last_node_latlon.X, false);

                        int index_in_togo = togoList.IndexOf(next_node);
                        if (index_in_togo < 0)
                        {
                            index_in_togo = togoList.Count;
                            togoList.Add(next_node);
                            togoHeurist.Add(0);
                            update = true;
                        }
                        else
                        {
                            switch (minBy)
                            {
                                case MinimizeBy.Cost: update = cost_from_st < GetCost(next_node); break;
                                case MinimizeBy.Dist: update = dist_from_st < GetRouteDistance(next_node, next_node); break;
                                case MinimizeBy.Time: update = time_from_st < GetRouteTime(next_node, next_node); break;
                            };
                        };

                        if (update)
                        {                                                                               
                            SetCost(next_node, cost_from_st);
                            SetPrev(next_node, curr_node);
                            SetRouteDistance(next_node, dist_from_st);
                            SetRouteTime(next_node, time_from_st);

                            if (minBy == MinimizeBy.Time)
                            {
                                float fl = time_from_st + next_node_dist_to_end / 1000;
                                if(float.IsNaN(fl))
                                {

                                };
                                togoHeurist[index_in_togo] = fl;
                            }
                            else
                            {
                                float fl = dist_from_st + next_node_dist_to_end;
                                if (float.IsNaN(fl))
                                {

                                };
                                togoHeurist[index_in_togo] = fl;
                            };
                        };
                    };
                };
            };

            return;
        }

        /// <summary>
        ///     Получаем координаты узла
        /// </summary>
        /// <param name="node">номер узла</param>
        /// <returns>Lat Lon</returns>
        public PointF GetNodeLatLon(uint node)
        {
            geo_str.Position = RMPOINTNLL0.Length + 4 + 8 * (node - 1);
            byte[] latlon = new byte[8];
            geo_str.Read(latlon, 0, 8);
            return new PointF(BitConverter.ToSingle(latlon, 4), BitConverter.ToSingle(latlon, 0));            
        }

        /// <summary>
        ///     Получаем вектор промежуточных точек пути из (X)->Y
        ///     Вызывается только между методами BeginSolve и EndSolve
        /// </summary>
        /// <param name="start">узел начала пути</param>
        /// <param name="end">узел конца пути</param>
        /// <returns>номера промежуточных узлов</returns>
        public uint[] GetRouteNodes(uint start, uint end)
        {
            uint y = calcReversed ? start : end;

            if (vector_str == null) throw new Exception("Call BeginSolve first");

            if (y == 0) return null; // NO WAY
            if (GetCost(y) == 0) return new uint[0]; // ALREADY

            uint intermediate = y;
            List<uint> arr = new List<uint>();
            while (((intermediate = GetPrev(intermediate)) > 0) && (intermediate != start))
                arr.Add(intermediate);
            uint[] a = arr.ToArray();
            if (this.calcReversed) return a;

            Array.Reverse(a);
            return a;
        }

        /// <summary>
        ///     Возращает номера линий маршрута
        /// </summary>
        /// <param name="start">нач</param>
        /// <param name="way">промеж</param>
        /// <param name="end">кон</param>
        /// <returns>список линий</returns>
        public uint[] GetRouteLines(uint start, uint[] way, uint end)
        {
            uint[] arr = new uint[way.Length + 1];

            uint[] _n; float[] _c; float[] _d; float[] _t; uint[] _l; byte[] _r;

            if (way.Length > 0)
            {
                SelectNode(start, out _n, out _c, out _d, out _t, out _l, out _r);
                for (int i = 0; i < _n.Length; i++)
                    if (_n[i] == way[0]) arr[0] = _l[i];

                for (int x = 1; x < way.Length; x++)
                {
                    SelectNode(way[x - 1], out _n, out _c, out _d, out _t, out _l, out _r);
                    for (int i = 0; i < _n.Length; i++)
                        if (_n[i] == way[x]) arr[x] = _l[i];
                };
                SelectNode(way[way.Length - 1], out _n, out _c, out _d, out _t, out _l, out _r);
                for (int i = 0; i < _n.Length; i++)
                    if (_n[i] == end) arr[arr.Length - 1] = _l[i];
            }
            else
            {
                SelectNode(start, out _n, out _c, out _d, out _t, out _l, out _r);
                for (int i = 0; i < _n.Length; i++)
                    if (_n[i] == end) arr[0] = _l[i];

            };

            return arr;
        }

        /// <summary>
        ///     Получаем вектор координат точек
        /// </summary>
        /// <param name="start">Начальная точка</param>
        /// <param name="way">Промежуточные точки</param>
        /// <param name="end">Конечная точка</param>
        /// <returns></returns>
        public PointF[] GetRouteVector(uint start, uint[] way, uint end)
        {
            List<uint> ww = new List<uint>();
            ww.Add(start);
            ww.AddRange(way);
            ww.Add(end);

            List<PointF> vec = new List<PointF>();

            uint[] _n; float[] _c; float[] _d; float[] _t; uint[] _l; byte[] _r;
            ushort segments; int pos; bool oneway; uint node1; uint node2;

            for (int x = 0; x < ww.Count - 1; x++)
            {
                SelectNode(ww[x], out _n, out _c, out _d, out _t, out _l, out _r);
                for (int i = 0; i < _n.Length; i++)
                    if (_n[i] == ww[x + 1])
                    {
                        GetLine(_l[i], out segments, out pos, out oneway, out node1, out node2);
                        PointF[] pts = GetLineSegments(_l[i], pos, segments, _r[i] == 1, false);
                        vec.Add(pts[0]);
                        for (int si = 1; si < pts.Length - 1; si++) vec.Add(pts[si]);
                        if (x == (ww.Count - 2))
                            vec.Add(pts[pts.Length - 1]);
                    };
            };

            ww.Clear();

            return vec.ToArray();
        }

        /// <summary>
        ///     Получаем вектор координат точек с привязкой
        ///     к узлам и линиям (вершинам и ребрам графа)
        ///     для детализации и описания маршрута
        /// </summary>
        /// <param name="start">Начальная точка</param>
        /// <param name="way">Промежуточные точки</param>
        /// <param name="end">Конечная точка</param>
        /// <returns></returns>
        public PointFL[] GetRouteVectorWNL(uint start, uint[] way, uint end)
        {
            List<uint> ww = new List<uint>();
            ww.Add(start);
            ww.AddRange(way);
            ww.Add(end);

            List<PointFL> vec = new List<PointFL>();

            uint[] _n; float[] _c; float[] _d; float[] _t; uint[] _l; byte[] _r;
            ushort segments; int pos; bool oneway; uint node1; uint node2;
            
            for (int x = 0; x < ww.Count-1; x++)
            {
                SelectNode(ww[x], out _n, out _c, out _d, out _t, out _l, out _r);
                for (int i = 0; i < _n.Length; i++)
                    if (_n[i] == ww[x+1])
                    {
                        GetLine(_l[i], out segments, out pos, out oneway, out node1, out node2);
                        PointF[] pts = GetLineSegments(_l[i], pos, segments, _r[i] == 1, false);
                        vec.Add(new PointFL(pts[0], ww[x], _l[i]));
                        for (int si = 1; si < pts.Length - 1; si++) vec.Add(new PointFL(pts[si], 0, _l[i]));
                        if(x == (ww.Count-2))
                            vec.Add(new PointFL(pts[pts.Length - 1], end, 0));
                    };
            };

            ww.Clear();

            return vec.ToArray();
        }

        /// <summary>
        ///     Получаем линию по номеру
        /// </summary>
        /// <param name="line">номер линии</param>
        /// <param name="segments">число сегментов линии</param>
        /// <param name="pos">позиция первого сегмента в файле сегментов</param>
        /// <param name="oneway">одностороннее движение</param>
        /// <param name="nodeStart">точка начала линии</param>
        /// <param name="nodeEnd">точка конца линии</param>
        private void GetLine(uint line, out ushort segments, out int pos, out bool oneway, out uint nodeStart, out uint nodeEnd)
        {
            lines_str.Position = RMLINES.Length + 4 + line_record_length * (line - 1);
            byte[] ba = new byte[15];
            lines_str.Read(ba, 0, 15);
            segments = BitConverter.ToUInt16(ba, 0);
            pos = BitConverter.ToInt32(ba, 2);
            oneway = (ba[6] & 1) == 1; // bit mask - 0 bit
            nodeStart = BitConverter.ToUInt32(ba, 7);
            nodeEnd = BitConverter.ToUInt32(ba, 11);
        }

        /// <summary>
        ///     Получаем все отрезки линии по номеру
        /// </summary>
        /// <param name="line">номер линии</param>
        /// <param name="pos">позиция первого сегмента в файле сегментов</param>
        /// <param name="segments">число сегментов в линии</param>
        /// <param name="reverse">развернуть</param>
        /// <param name="skip_first">не добавлять первый элемент</param>
        /// <returns>вектор линии</returns>
        private PointF[] GetLineSegments(uint line, int pos, ushort segments, bool reverse, bool skip_first)
        {
            List<PointF> arr = new List<PointF>();

            segm_str.Position = pos;
            byte[] ba = new byte[segm_record_length];
            for (int i = 0; i < segments; i++)
            {
                segm_str.Read(ba, 0, ba.Length);
                uint line_no = BitConverter.ToUInt32(ba, 0);
                ushort segm = BitConverter.ToUInt16(ba, 4);
                float lat0 = BitConverter.ToSingle(ba, 6);
                float lon0 = BitConverter.ToSingle(ba, 10);
                float lat1 = BitConverter.ToSingle(ba, 14);
                float lon1 = BitConverter.ToSingle(ba, 18);
                //float k = BitConverter.ToSingle(ba, 22);
                //float b = BitConverter.ToSingle(ba, 26);

                if(i == 0) arr.Add(new PointF(lon0,lat0));
                arr.Add(new PointF(lon1,lat1));
            };
            if (reverse) arr.Reverse();
            if (skip_first) arr.RemoveAt(0);
            return arr.ToArray();
        }

        /// <summary>
        ///     Получаем все отрезки линии по номеру
        ///     (используется для собирания вектора маршрута)
        /// </summary>
        /// <param name="line">номер линии</param>
        /// <param name="reverse">развернуть</param>
        /// <param name="skip_first">не добавлять первый элемент</param>
        /// <returns>вектор линии</returns>
        private PointF[] GetLineSegments(uint line, bool reverse, bool skip_first)
        {
            lines_str.Position = RMLINES.Length + 4 + line_record_length * (line - 1);
            byte[] ba = new byte[15];
            lines_str.Read(ba, 0, 15);
            ushort segments = BitConverter.ToUInt16(ba, 0);
            int pos = BitConverter.ToInt32(ba, 2);
            bool oneway = ba[6] == 1;
            uint node1 = BitConverter.ToUInt32(ba, 7);
            uint node2 = BitConverter.ToUInt32(ba, 11);
            return GetLineSegments(line, pos, segments, reverse, skip_first);
        }

        /// <summary>
        ///     Получаем LINK_ID линий из файла .lines.id
        /// </summary>
        /// <param name="lines">номера линий</param>
        /// <returns>LINK_ID[]</returns>
        public int[] GetLinesLinkIDs(uint[] lines)
        {
            int[] res = new int[lines.Length];
            FileStream fs = new FileStream(fileMain + ".lines.id", FileMode.Open, FileAccess.Read);
            byte[] ba = new byte[RMLINKIDS.Length];
            fs.Read(ba, 0, ba.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(ba) != "RMLINKIDS")
            {
                fs.Close();
                throw new IOException("Unknown file format:\r\n" + fileMain + ".lines.id");
            };
            for (int i = 0; i < lines.Length; i++)
            {
                fs.Position = RMLINKIDS.Length + 4 + 4 * (lines[i] - 1);
                ba = new byte[4];
                fs.Read(ba,0,ba.Length);
                res[i] = BitConverter.ToInt32(ba, 0);
            };
            fs.Close();
            return res;
        }        

        /// <summary>
        ///     Получаем маршрут между двумя узлами графа
        /// </summary>
        /// <param name="nodeStart">start node</param>
        /// <param name="nodeEnd">end node</param>
        /// <returns>Отчет</returns>
        public RouteResult GetRoute(uint nodeStart, uint nodeEnd, uint nodeStartReversed, uint nodeEndReversed)
        {
            RouteResult res = new RouteResult();
            res.nodeStart = nodeStart;
            res.nodeEnd = nodeEnd;
            res.nodes = new uint[0];
            res.lines = new uint[0];
            res.vector = new PointFL[0];

            res.cost = GetRouteCost(nodeStart,nodeEnd);
            res.length = GetRouteDistance(nodeStart, nodeEnd);
            res.time = GetRouteTime(nodeStart, nodeEnd);
            res.nodes = GetRouteNodes(nodeStart, nodeEnd);
            res.shrinkStart = false;
            res.shrinkEnd = false;

            //ПРОВЕРЯЕМ на предмет, если уже проехались по линии
            if (res.nodes.Length > 0)
            {                   
                if (res.nodes[res.nodes.Length - 1] == nodeEndReversed)
                {
                    if (true) // если oneway, то маршрут не пройдет через последний отрезок в обратном направлении
                    {
                        res.shrinkEnd = true;
                        res.nodeEnd = nodeEndReversed;
                        if (calcReversed)
                        {
                            res.cost -= GetRouteCost(res.nodeEnd, res.nodeEnd);
                            res.length -= GetRouteDistance(res.nodeEnd, res.nodeEnd);
                            res.time -= GetRouteTime(res.nodeEnd, res.nodeEnd);
                        }
                        else
                        {                            
                            res.cost = GetRouteCost(res.nodeEnd, res.nodeEnd);
                            res.length = GetRouteDistance(res.nodeEnd, res.nodeEnd);
                            res.time = GetRouteTime(res.nodeEnd, res.nodeEnd);
                        };
                    };
                };
                if (res.nodes[0] == nodeStartReversed)
                {
                    bool ow = false; // односторонняя?
                    uint[] _n; float[] _c; float[] _d; float[] _t; uint[] _l; byte[] _r;
                    SelectNode(nodeStartReversed, out _n, out _c, out _d, out _t, out _l, out _r);
                    for (int i = 0; i < _n.Length; i++)
                        if (_n[i] == (res.nodes.Length > 1 ? res.nodes[1] : nodeEnd))
                        {
                            ushort seg; int pos; uint n1; uint n2;
                            GetLine(_l[i], out seg, out pos, out ow, out n1, out n2);
                        };
                    if (!ow)
                    {
                        res.shrinkStart = true;
                        res.nodeStart = nodeStartReversed;
                        if (calcReversed)
                        {
                            res.cost = GetRouteCost(res.nodeStart, res.nodeStart);
                            res.length = GetRouteDistance(res.nodeStart, res.nodeStart);
                            res.time = GetRouteTime(res.nodeStart, res.nodeStart);
                        }
                        else
                        {
                            res.cost -= GetRouteCost(res.nodeStart, res.nodeStart);
                            res.length -= GetRouteDistance(res.nodeStart, res.nodeStart);
                            res.time -= GetRouteTime(res.nodeStart, res.nodeStart);
                        };
                    };
                };
                if (res.shrinkStart || res.shrinkEnd) // удаляем лишние развороты вначале или в конце, если есть
                {
                    List<uint> arr = new List<uint>(res.nodes);
                    if (res.shrinkStart && (arr.Count > 0)) arr.RemoveAt(0);
                    if (res.shrinkEnd && (arr.Count > 0)) arr.RemoveAt(arr.Count - 1);
                    res.nodes = arr.ToArray();
                    res.lines = GetRouteLines(res.nodeStart, res.nodes, res.nodeEnd);
                }
                else
                {
                    res.lines = GetRouteLines(nodeStart, res.nodes, nodeEnd);
                };
            };

            if (res.lines.Length == 0)
            {
                uint[] _n; float[] _c; float[] _d; float[] _t; uint[] _l; byte[] _r;
                SelectNode(res.nodeStart, out _n, out _c, out _d, out _t, out _l, out _r);
                for (int i = 0; i < _n.Length; i++)
                    if (_n[i] == res.nodeEnd)
                        res.lines = new uint[] { _l[i] };
            };
            res.vector = GetRouteVectorWNL(res.nodeStart, res.nodes, res.nodeEnd);

            return res;
        }

        /// <summary>
        ///     Получаем маршрут между двумя узлами графа
        /// </summary>
        /// <param name="nodeStart">start node</param>
        /// <param name="nodeEnd">end node</param>
        /// <param name="shrinkStartStopIfNeed">убирать ненужные развороты по краям</param>
        /// <returns>Отчет</returns>
        public RouteResult GetRoute(FindStartStopResult nodeStart, FindStartStopResult nodeEnd, bool shrinkStartStopIfNeed)
        {
            RouteResult res = GetRoute(nodeStart.nodeN, nodeEnd.nodeN, shrinkStartStopIfNeed ? nodeStart.nodeR : 0, shrinkStartStopIfNeed ? nodeEnd.nodeR : 0);
            if (shrinkStartStopIfNeed)
            {
                if (res.shrinkStart) ReverseStop(nodeStart);
                if (res.shrinkEnd) ReverseStop(nodeEnd);
            };
            return res;
        }

        /// <summary>
        ///     Получаем маршрут между двумя линиями графа    
        /// </summary>
        /// <param name="lineStart">нач линия</param>
        /// <param name="lineEnd">кон линия</param>
        /// <returns>Отчет</returns>
        public RouteResult GetRoute(uint lineStart, uint lineEnd)
        {
            ushort seg;
            bool ow;
            int pos;
            uint ns1; uint ns2; uint ne1; uint ne2;
            GetLine(lineStart, out seg, out pos, out ow, out ns1, out ns2);
            GetLine(lineStart, out seg, out pos, out ow, out ne1, out ne2);
            return GetRoute(ns2, ne1, ns1, ne2);
        }

        /// <summary>
        ///     Получаем маршрут между двумя координатами
        /// </summary>
        /// <param name="start">начальный узел</param>
        /// <param name="end">конечный узел</param>
        /// <returns>Отчет</returns>
        public RouteResult GetRoute(PointF start, PointF end)
        {
            FindStartStopResult nodeStart = FindNodeStart(start.Y, start.X, 2000);
            FindStartStopResult nodeEnd = FindNodeStart(end.Y, end.X, 2000);
            BeginSolve(true,null);
            SolveDeikstra(nodeStart.nodeN, new uint[] { nodeEnd.nodeN });
            RouteResult res = GetRoute(nodeStart.nodeN, nodeEnd.nodeN, nodeStart.nodeR, nodeEnd.nodeR);
            
            if (res.shrinkStart) ReverseStop(nodeStart);
            if (res.shrinkEnd) ReverseStop(nodeEnd);
            EndSolve();
            
            float dL = nodeStart.distToN + nodeEnd.distToN;
            res.length += dL;
            res.time += dL / 1000; // 60 km/h

            List<PointFL> vec = new List<PointFL>();
            //vec.AddRange(nodeStart.normal);
            for (int si = 0; si < nodeStart.normal.Length; si++) vec.Add(new PointFL(nodeStart.normal[si], 0, 0));
            vec.AddRange(res.vector);
            //vec.AddRange(nodeEnd.normal);
            for (int si = 0; si < nodeEnd.normal.Length; si++) vec.Add(new PointFL(nodeEnd.normal[si], 0, 0));
            res.vector = vec.ToArray();

            return res;
        }

        /// <summary>
        ///     Развернуть точку начала/конца в другую сторону линии
        ///     (используется для убирания разворотов по краям)
        /// </summary>
        /// <param name="nodeStart"></param>
        public void ReverseStop(FindStartStopResult StartStop)
        {
            float d = StartStop.distToN;
            StartStop.distToN = StartStop.distToR;
            StartStop.distToR = d;

            uint n = StartStop.nodeN;
            StartStop.nodeN = StartStop.nodeR;
            StartStop.nodeR = n;

            PointF[] p = StartStop.normal;
            StartStop.normal = StartStop.revers;
            StartStop.revers = p;
        }

        /// <summary>
        ///     Получаем оценку пути из (X)->Y
        ///     Вызывается только между методами BeginSolve и EndSolve
        /// </summary>
        /// <param name="start">начальный узел</param>
        /// <param name="end">конечный узел</param>
        /// <returns>оценка</returns>
        public Single GetRouteCost(uint start, uint end)
        {
            uint y = calcReversed ? start : end;

            if (vector_str == null) throw new Exception("Call BeginSolve first");
            return GetCost(y);
        }

        /// <summary>
        ///     Устанавливаем расстояние из (X)->Y
        ///     Вызывается только между методами BeginSolve и EndSolve
        /// </summary>
        /// <param name="y">конец пути</param>
        /// <param name="dist">длина пути в метрах</param>
        private void SetRouteDistance(uint y, Single dist)
        {
            vector_str.Position = (y - 1) * (vtArrElemLength) + distOffset;
            byte[] bb = BitConverter.GetBytes(dist);
            vector_str.Write(bb, 0, 4);
        }

        /// <summary>
        ///     Получаем длину пути из (X)->Y
        ///     Вызывается только между методами BeginSolve и EndSolve
        /// </summary>
        /// <param name="start">начальный узел</param>
        /// <param name="end">конечный узел</param>
        /// <returns>длина пути в метрах</returns>
        public Single GetRouteDistance(uint start, uint end)
        {
            uint y = calcReversed ? start : end;

            if (vector_str == null) throw new Exception("Call BeginSolve first");

            vector_str.Position = (y - 1) * (vtArrElemLength) + distOffset;
            byte[] bb = new byte[4];
            vector_str.Read(bb, 0, 4);
            Single s = BitConverter.ToSingle(bb, 0);
            if (s < maxError)
                return Single.MaxValue;
            else
                return s;
        }

        /// <summary>
        ///     Устанавливаем время движения из (X)->Y
        ///     Вызывается только между методами BeginSolve и EndSolve
        /// </summary>
        /// <param name="y">конец пути</param>
        /// <param name="dist">время движения</param>
        private void SetRouteTime(uint y, Single time)
        {
            vector_str.Position = (y - 1) * (vtArrElemLength) + timeOffset;
            byte[] bb = BitConverter.GetBytes(time);
            vector_str.Write(bb, 0, 4);
        }


        /// <summary>
        ///     Получаем время движения из (X)->Y
        ///     Вызывается только между методами BeginSolve и EndSolve
        /// </summary>
        /// <param name="start">начальный узел</param>
        /// <param name="end">конечный узел</param>
        /// <returns>Время движения в минутах</returns>
        public Single GetRouteTime(uint start, uint end)
        {
            uint y = calcReversed ? start : end;

            if (vector_str == null) throw new Exception("Call BeginSolve first");

            vector_str.Position = (y - 1) * (vtArrElemLength) + timeOffset;
            byte[] bb = new byte[4];
            vector_str.Read(bb, 0, 4);
            Single s = BitConverter.ToSingle(bb, 0);
            if (s < maxError)
                return Single.MaxValue;
            else
                return s;
        }

        /// <summary>
        ///     Сохраняет вектор расчета в файл
        /// </summary>
        /// <param name="fn">Имя файла</param>
        public void SaveSolvedVector(string fn)
        {
            FileStream fs = new FileStream(fn, FileMode.Create, FileAccess.ReadWrite);
            byte[] bb = new byte[8192];
            int read = 0;
            vector_str.Position = 0;
            while ((read = vector_str.Read(bb, 0, bb.Length)) > 0)
                fs.Write(bb, 0, read);
            fs.Flush();
            fs.Close();
        }

        /// <summary>
        ///     Предварительный просчет путей между точками стыковки межрайонных маршрутов
        /// </summary>
        public void CalculateRGNodesRoutes()
        {
            CalculateRGNodesRoutes(0);
        }

        /// <summary>
        ///     Предварительный просчет путей между точками стыковки межрайонных маршрутов
        /// </summary>
        /// <param name="region_id">Номер региона</param>
        public void CalculateRGNodesRoutes(int region_id)
        {
            if (!File.Exists(fileMain + ".rgnodes.xml"))
            {
                Console.WriteLine("File " + Path.GetFileName(fileMain) + ".rgnodes.xml doen't exists!");
                return;
            };

            Console.WriteLine("Loading RGNodes: " + Path.GetFileName(fileMain) + ".rgnodes.xml");
            TRGNode[] nodes = XMLSaved<TRGNode[]>.Load(fileMain + ".rgnodes.xml");

            if (nodes.Length == 0)
            {
                Console.WriteLine("RGNodes not found!");
                return;
            };
            
            if ((nodes == null) || (nodes.Length == 0)) return;

            string dir = Path.GetDirectoryName(fileName) + @"\RGWays\";
            Directory.CreateDirectory(dir);

            Console.WriteLine("Предварительный просчет путей между точками стыковки межрайонных маршрутов");
            Console.WriteLine("Оптимизировать маршрут по: "+this.minBy.ToString());
            for (int x = 0; x < nodes.Length; x++)
            {
                if (!nodes[x].inner) continue;

                List<int> li = new List<int>();
                List<float> lc = new List<float>();
                List<float> ld = new List<float>();
                List<float> lt = new List<float>();

                for (int y = 0; y < nodes.Length; y++)
                {
                    if (x == y) continue;
                    if (!nodes[y].outer) continue;

                    dkxce.RealTimeRoutes.FindStartStopResult nodeStart = new dkxce.RealTimeRoutes.FindStartStopResult();
                    nodeStart.nodeN = nodes[x].node;
                    dkxce.RealTimeRoutes.FindStartStopResult nodeEnd = new dkxce.RealTimeRoutes.FindStartStopResult();
                    nodeEnd.nodeN = nodes[y].node;

                    DateTime s = DateTime.Now;
                    Console.Write(nodes[x].node.ToString() + "(" + nodes[x].id + ") -> " + nodes[y].node.ToString() + "(" + nodes[y].id + ")");

                    BeginSolve(true, null);
                    //MinimizeRouteBy = dkxce.RealTimeRoutes.MinimizeBy.Time;
                    SolveAstar(nodeStart.nodeN, nodeEnd.nodeN); // A*                        
                    DateTime e = DateTime.Now;
                    TimeSpan ts = e.Subtract(s);
                    
                    Console.Write(" - ");

                    float c = GetRouteCost(nodeStart.nodeN, nodeEnd.nodeN);
                    float d = GetRouteDistance(nodeStart.nodeN, nodeEnd.nodeN);
                    float t = GetRouteTime(nodeStart.nodeN, nodeEnd.nodeN);
                    RouteResultStored rrs = new RouteResultStored();
                    rrs.route = GetRoute(nodeStart, nodeEnd, true);
                    rrs.costs = new float[rrs.route.nodes.Length + 2];
                    rrs.times = new float[rrs.route.nodes.Length + 2];
                    rrs.distances = new float[rrs.route.nodes.Length + 2];
                    for (int i = 0; i < rrs.costs.Length - 2; i++)
                    {
                        rrs.costs[i + 1] = GetRouteCost(nodeStart.nodeN, rrs.route.nodes[i]);
                        rrs.times[i + 1] = GetRouteTime(nodeStart.nodeN, rrs.route.nodes[i]);
                        rrs.distances[i + 1] = GetRouteDistance(nodeStart.nodeN, rrs.route.nodes[i]);
                    };
                    rrs.costs[rrs.costs.Length - 1] = GetRouteCost(nodeStart.nodeN, nodeEnd.nodeN);
                    rrs.times[rrs.times.Length - 1] = GetRouteTime(nodeStart.nodeN, nodeEnd.nodeN);
                    rrs.distances[rrs.distances.Length - 1] = GetRouteDistance(nodeStart.nodeN, nodeEnd.nodeN);

                    XMLSaved<RouteResultStored>.Save(dir + String.Format("{0}T{1}.rgway.xml",nodes[x].id,nodes[y].id),rrs);
                    
                    EndSolve();                                       

                    Console.Write(String.Format("{0:0} km, {1:0} min", d / 1000, t, rrs.route.nodes.Length));
                    Console.Write(String.Format(" - {0}:{1:00}:{2:000}", ts.Minutes,ts.Seconds,ts.Milliseconds));
                    Console.Write(" - ");
                    Console.WriteLine(String.Format("{0}T{1}.rgway.xml", nodes[x].id, nodes[y].id));

                    li.Add(nodes[y].id); lc.Add(c); ld.Add(d); lt.Add(t);
                };

                if(region_id > 0) nodes[x].region = region_id;
                nodes[x].links = li.ToArray();
                nodes[x].costs = lc.ToArray();
                nodes[x].dists = ld.ToArray();
                nodes[x].times = lt.ToArray();
            };

            Console.WriteLine("Saving RGNodes: " + Path.GetFileName(fileMain) + ".rgnodes.xml");
            XMLSaved<TRGNode[]>.Save(fileMain + ".rgnodes.xml", nodes);

            Console.WriteLine("Done");
        }

        /// <summary>
        ///     Выводит наглядную матрицу в текстовый файл
        ///     (рекомендуется только для небольших матриц)
        /// </summary>
        /// <param name="fn">Полный путь к файлу</param>
        public void ToTextFile(string fn)
        {
            FileStream fout = new FileStream(fn, FileMode.Create);
            StreamWriter sw = new StreamWriter(fout);

            sw.Write("NODE \t");
            for (uint y = 1; y <= size; y++) sw.Write(y.ToString() + "\t\t\t");
            sw.WriteLine();

            sw.Write("COST \t");
            for (uint y = 1; y <= size; y++)
                if (GetCost(y) > maxValue)
                    sw.Write(". . .\t");
                else
                    sw.Write(GetPrev(y).ToString() + "(" + GetCost(y).ToString("0.000").Replace(",", ".") + ") \t");
            sw.WriteLine();

            sw.Write("DIST \t");
            for (uint y = 1; y <= size; y++)
                if (GetCost(y) > maxValue)
                    sw.Write(". . .\t");
                else
                    sw.Write(GetPrev(y).ToString() + "(" + GetRouteDistance(y,y).ToString("0.000").Replace(",", ".") + ") \t");
            sw.WriteLine(); ;

            sw.Write("TIME \t");
            for (uint y = 1; y <= size; y++)
                if (GetCost(y) > maxValue)
                    sw.Write(". . .\t");
                else
                    sw.Write(GetPrev(y).ToString() + "(" + GetRouteTime(y,y).ToString("0.000").Replace(",", ".") + ") \t");
            sw.WriteLine(); ;

            sw.Flush();
            fout.Close();
        }

        ///////
        ///////

        /// <summary>
        ///     Ищем точки графа в заданной зоне
        /// </summary>
        /// <param name="StartLat"></param>
        /// <param name="StartLon"></param>
        /// <param name="EndLat"></param>
        /// <param name="EndLon"></param>
        /// <param name="getLatLon">возвращать ли координаты точек</param>
        /// <param name="fillLinks">заполнять связи</param>
        /// <param name="maxResults">Максимальное число точек к ответе</param>
        /// <returns>Узлы</returns>
        public TNode[] FindNodesInRect(float StartLat, float StartLon, float EndLat, float EndLon, bool getLatLon, bool fillLinks, int maxResults)
        {
            List<TNode> res = new List<TNode>(maxResults);

            geoll_str.Position = RMPOINTNLL1.Length + 4;
            byte[] buff = new byte[1020]; // 1020 bytes - 85 points // 8184 bytes - 682 points
            int read = 0;
            bool brk = false;
            while ((read = geoll_str.Read(buff, 0, buff.Length)) > 0)
            {
                float lastlat = BitConverter.ToSingle(buff, read - 8);
                if (lastlat < StartLat) continue; // goto next block if last lat < needed

                int offset = 0;
                while (offset < read)
                {
                    uint node = BitConverter.ToUInt32(buff, offset);
                    float lat = BitConverter.ToSingle(buff, offset += 4);
                    float lon = BitConverter.ToSingle(buff, offset += 4);
                    if (lat > EndLat) // stops find if lat > needed
                    {
                        brk = true;
                        break;
                    };
                    // (lat <= EndLat) - already checked
                    if ((lat >= StartLat) && (lon >= StartLon) && (lon <= EndLon))
                    {
                        res.Add(new TNode(node, lat, lon));
                        if (res.Count == maxResults) // if found points count = maxResults then return 
                        {
                            brk = true;
                            break;
                        };
                    };
                    offset += 4;
                };
                if (brk) break;
            };

            if (fillLinks)
            {
                uint[] _n; float[] _c; float[] _d; float[] _t; uint[] _l; byte[] _r;
                for (int i = 0; i < res.Count; i++)
                {
                    int ttl = SelectNode(res[i].node, out _n, out _c, out _d, out _t, out _l, out _r);
                    for (int x = 0; x < ttl; x++)
                        res[i].AddLink(_n[x], _c[x], _d[x], _t[x], _l[x], _r[x] == 1);
                };
            };

            return res.ToArray();
        }

        /// <summary>
        ///     Ищем точки графа в заданной зоне
        /// </summary>
        /// <param name="StartLat"></param>
        /// <param name="StartLon"></param>
        /// <param name="EndLat"></param>
        /// <param name="EndLon"></param>
        /// <param name="getLatLon">возвращать ли координаты точек</param>
        /// <param name="fillLinks">заполнять связи</param>
        /// <returns>Узлы</returns>
        public TNode[] FindNodesInRect(float StartLat, float StartLon, float EndLat, float EndLon, bool getLatLon, bool fillLinks)
        {
            return FindNodesInRect(StartLat, StartLon, EndLat, EndLon, getLatLon, fillLinks, 1000);
        }

        /// <summary>
        ///     Ищем точки графа в радиусе м
        /// </summary>
        /// <param name="Lat">Широта центра</param>
        /// <param name="Lon">Долгота центра</param>
        /// <param name="metersRadius">Радиус в метрах</param>
        /// <param name="getLatLon">Получать координаты точек</param>
        /// <param name="getDistance">Получать расстояние до точки</param>
        /// <param name="sortByDist">Сортировать точки по удаленности</param>
        /// <param name="fillLinks">заполнять связи</param>
        /// <returns>Узлы</returns>
        public TNodeD[] FindNodesInRadius(float Lat, float Lon, float metersRadius, bool getLatLon, bool getDistance, bool sortByDist, bool fillLinks)
        {
            return FindNodesInRadius(Lat, Lon, metersRadius, getLatLon, getDistance, sortByDist, fillLinks, 1000);
        }

        /// <summary>
        ///     Ищем точки графа в радиусе м
        /// </summary>
        /// <param name="Lat">Широта центра</param>
        /// <param name="Lon">Долгота центра</param>
        /// <param name="metersRadius">Радиус в метрах</param>
        /// <param name="getLatLon">Получать координаты точек</param>
        /// <param name="getDistance">Получать расстояние до точки</param>
        /// <param name="sortByDist">Сортировать точки по удаленности</param>
        /// <param name="fillLinks">заполнять связи</param>
        /// <param name="maxResults">Максимальное число точек в ответе</param>
        /// <returns>Узлы</returns>
        public TNodeD[] FindNodesInRadius(float Lat, float Lon, float metersRadius, bool getLatLon, bool getDistance, bool sortByDist, bool fillLinks, int maxResults)
        {
            float dLat = metersRadius / GetLengthMeters(Lat, Lon, Lat + 1, Lon, false);
            float dLon = metersRadius / GetLengthMeters(Lat, Lon, Lat, Lon + 1, false);
            TNode[] res = FindNodesInRect(Lat - dLat, Lon - dLon, Lat + dLat, Lon + dLon, getLatLon || getDistance, fillLinks, maxResults);
            TNodeD[] wd = new TNodeD[res.Length];
            if (getDistance)
            {
                for (int i = 0; i < res.Length; i++)
                    wd[i] = new TNodeD(res[i], GetLengthMeters(Lat, Lon, res[i].lat, res[i].lon, false));
                if (sortByDist)
                    Array.Sort<TNodeD>(wd, new TNodeD.DSorter());
            }
            else
            {
                for (int i = 0; i < res.Length; i++)
                    wd[i] = new TNodeD(res[i]);
            };

            if (fillLinks)
            {
                uint[] _n; float[] _c; float[] _d; float[] _t; uint[] _l; byte[] _r;
                for (int i = 0; i < wd.Length; i++)
                {
                    int ttl = SelectNode(res[i].node, out _n, out _c, out _d, out _t, out _l, out _r);
                    for (int x = 0; x < ttl; x++)
                        wd[i].AddLink(_n[x], _c[x], _d[x], _t[x], _l[x], _r[x] == 1);
                };
            };

            return wd;
        }

        /// <summary>
        ///     Ищем точку графа с указанными координатами
        /// </summary>
        /// <param name="Lat">Latitude</param>
        /// <param name="Lon">Longitude</param>
        /// <returns>Номер узла (если 0 - не сущ.)</returns>
        public uint FindNodesLatLon(float Lat, float Lon)
        {
            TNode[] pnts = FindNodesInRect(Lat, Lon, Lat, Lon, false, false);
            if (pnts.Length == 0) return 0;
            else return pnts[0].node;
        }

        /// <summary>
        ///     Получаем узлы графа по номерам
        /// </summary>
        /// <param name="nodes">Номера точек</param>
        /// <returns>Узлы</returns>
        public TNode[] SelectNodes(uint[] nodes)
        {
            TNode[] res = new TNode[nodes.Length];
            byte[] ba = new byte[8];
            for (int i = 0; i < nodes.Length; i++)
            {
                geo_str.Position = RMPOINTNLL0.Length + 4 + (nodes[i] - 1) * 8;
                geo_str.Read(ba, 0, ba.Length);
                float lat = BitConverter.ToSingle(ba, 0);
                float lon = BitConverter.ToSingle(ba, 4);
                res[i] = new TNode(nodes[i], lat, lon);
            };
            return res;
        }

        /// <summary>
        ///     Получаем узлы графа по номерам
        ///     с расстоянием по прямой из каждого предыдущего узла
        /// </summary>
        /// <param name="nodes">номера узлов</param>
        /// <returns>Узлы</returns>
        public TNodeD[] SelectNodesByDist(uint[] nodes)
        {
            TNodeD[] res = new TNodeD[nodes.Length];
            byte[] ba = new byte[8];
            for (int i = 0; i < nodes.Length; i++)
            {
                geo_str.Position = RMPOINTNLL0.Length + 4 + (i - 1) * 8;
                geo_str.Read(ba, 0, ba.Length);
                float lat = BitConverter.ToSingle(ba, 0);
                float lon = BitConverter.ToSingle(ba, 4);
                float dist = i == 0 ? 0 : GetLengthMeters(res[i - 1].lat, res[i - 1].lon, res[i].lat, res[i].lon, false);
                res[i] = new TNodeD(nodes[i], lat, lon, dist);
            };
            return res;
        }

        // Рассчет расстояния       
        #region LENGTH
        public static float GetLengthMeters(double StartLat, double StartLong, double EndLat, double EndLong, bool radians)
        {
            // use fastest
           float result = (float)GetLengthMetersD(StartLat, StartLong, EndLat, EndLong, radians);

           if (float.IsNaN(result))
           {
               result = (float)GetLengthMetersC(StartLat, StartLong, EndLat, EndLong, radians);
               if (float.IsNaN(result))
               {
                   result = (float)GetLengthMetersE(StartLat, StartLong, EndLat, EndLong, radians);
                   if(float.IsNaN(result))
                       result = 0;
               };
           };

           return result;
        }

        // Slower
        public static uint GetLengthMetersA(double StartLat, double StartLong, double EndLat, double EndLong, bool radians)
        {
            double D2R = Math.PI / 180;     // Преобразование градусов в радианы

            double a = 6378137.0000;     // WGS-84 Equatorial Radius (a)
            double f = 1 / 298.257223563;  // WGS-84 Flattening (f)
            double b = (1 - f) * a;      // WGS-84 Polar Radius
            double e2 = (2 - f) * f;      // WGS-84 Квадрат эксцентричности эллипсоида  // 1-(b/a)^2

            // Переменные, используемые для вычисления смещения и расстояния
            double fPhimean;                           // Средняя широта
            double fdLambda;                           // Разница между двумя значениями долготы
            double fdPhi;                           // Разница между двумя значениями широты
            double fAlpha;                           // Смещение
            double fRho;                           // Меридианский радиус кривизны
            double fNu;                           // Поперечный радиус кривизны
            double fR;                           // Радиус сферы Земли
            double fz;                           // Угловое расстояние от центра сфероида
            double fTemp;                           // Временная переменная, использующаяся в вычислениях

            // Вычисляем разницу между двумя долготами и широтами и получаем среднюю широту
            // предположительно что расстояние между точками << радиуса земли
            if (!radians)
            {
                fdLambda = (StartLong - EndLong) * D2R;
                fdPhi = (StartLat - EndLat) * D2R;
                fPhimean = ((StartLat + EndLat) / 2) * D2R;
            }
            else
            {
                fdLambda = StartLong - EndLong;
                fdPhi = StartLat - EndLat;
                fPhimean = (StartLat + EndLat) / 2;
            };

            // Вычисляем меридианные и поперечные радиусы кривизны средней широты
            fTemp = 1 - e2 * (sqr(Math.Sin(fPhimean)));
            fRho = (a * (1 - e2)) / Math.Pow(fTemp, 1.5);
            fNu = a / (Math.Sqrt(1 - e2 * (Math.Sin(fPhimean) * Math.Sin(fPhimean))));

            // Вычисляем угловое расстояние
            if (!radians)
            {
                fz = Math.Sqrt(sqr(Math.Sin(fdPhi / 2.0)) + Math.Cos(EndLat * D2R) * Math.Cos(StartLat * D2R) * sqr(Math.Sin(fdLambda / 2.0)));
            }
            else
            {
                fz = Math.Sqrt(sqr(Math.Sin(fdPhi / 2.0)) + Math.Cos(EndLat) * Math.Cos(StartLat) * sqr(Math.Sin(fdLambda / 2.0)));
            };
            fz = 2 * Math.Asin(fz);

            // Вычисляем смещение
            if (!radians)
            {
                fAlpha = Math.Cos(EndLat * D2R) * Math.Sin(fdLambda) * 1 / Math.Sin(fz);
            }
            else
            {
                fAlpha = Math.Cos(EndLat) * Math.Sin(fdLambda) * 1 / Math.Sin(fz);
            };
            fAlpha = Math.Asin(fAlpha);

            // Вычисляем радиус Земли
            fR = (fRho * fNu) / (fRho * sqr(Math.Sin(fAlpha)) + fNu * sqr(Math.Cos(fAlpha)));
            // Получаем расстояние
            return (uint)Math.Round(Math.Abs(fz * fR));
        }
        // Slowest
        public static uint GetLengthMetersB(double StartLat, double StartLong, double EndLat, double EndLong, bool radians)
        {
            double fPhimean, fdLambda, fdPhi, fAlpha, fRho, fNu, fR, fz, fTemp, Distance,
                D2R = Math.PI / 180,
                a = 6378137.0,
                e2 = 0.006739496742337;
            if (radians) D2R = 1;

            fdLambda = (StartLong - EndLong) * D2R;
            fdPhi = (StartLat - EndLat) * D2R;
            fPhimean = (StartLat + EndLat) / 2.0 * D2R;

            fTemp = 1 - e2 * Math.Pow(Math.Sin(fPhimean), 2);
            fRho = a * (1 - e2) / Math.Pow(fTemp, 1.5);
            fNu = a / Math.Sqrt(1 - e2 * Math.Sin(fPhimean) * Math.Sin(fPhimean));

            fz = 2 * Math.Asin(Math.Sqrt(Math.Pow(Math.Sin(fdPhi / 2.0), 2) +
              Math.Cos(EndLat * D2R) * Math.Cos(StartLat * D2R) * Math.Pow(Math.Sin(fdLambda / 2.0), 2)));
            fAlpha = Math.Asin(Math.Cos(EndLat * D2R) * Math.Sin(fdLambda) / Math.Sin(fz));
            fR = fRho * fNu / (fRho * Math.Pow(Math.Sin(fAlpha), 2) + fNu * Math.Pow(Math.Cos(fAlpha), 2));
            Distance = fz * fR;

            return (uint)Math.Round(Distance);
        }
        // Average
        public static uint GetLengthMetersC(double StartLat, double StartLong, double EndLat, double EndLong, bool radians)
        {
            double D2R = Math.PI / 180;
            if (radians) D2R = 1;
            double dDistance = Double.MinValue;
            double dLat1InRad = StartLat * D2R;
            double dLong1InRad = StartLong * D2R;
            double dLat2InRad = EndLat * D2R;
            double dLong2InRad = EndLong * D2R;

            double dLongitude = dLong2InRad - dLong1InRad;
            double dLatitude = dLat2InRad - dLat1InRad;

            // Intermediate result a.
            double a = Math.Pow(Math.Sin(dLatitude / 2.0), 2.0) +
                       Math.Cos(dLat1InRad) * Math.Cos(dLat2InRad) *
                       Math.Pow(Math.Sin(dLongitude / 2.0), 2.0);

            // Intermediate result c (great circle distance in Radians).
            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

            const double kEarthRadiusKms = 6378137.0000;
            dDistance = kEarthRadiusKms * c;

            return (uint)Math.Round(dDistance);
        }
        // Fastest
        public static double GetLengthMetersD(double sLat, double sLon, double eLat, double eLon, bool radians)
        {
            double EarthRadius = 6378137.0;

            double lon1 = radians ? sLon : DegToRad(sLon);
            double lon2 = radians ? eLon : DegToRad(eLon);
            double lat1 = radians ? sLat : DegToRad(sLat);
            double lat2 = radians ? eLat : DegToRad(eLat);

            return EarthRadius * (Math.Acos(Math.Sin(lat1) * Math.Sin(lat2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Cos(lon1 - lon2)));
        }
        // Fastest
        public static double GetLengthMetersE(double sLat, double sLon, double eLat, double eLon, bool radians)
        {
            double EarthRadius = 6378137.0;

            double lon1 = radians ? sLon : DegToRad(sLon);
            double lon2 = radians ? eLon : DegToRad(eLon);
            double lat1 = radians ? sLat : DegToRad(sLat);
            double lat2 = radians ? eLat : DegToRad(eLat);

            /* This algorithm is called Sinnott's Formula */
            double dlon = (lon2) - (lon1);
            double dlat = (lat2) - (lat1);
            double a = Math.Pow(Math.Sin(dlat / 2), 2.0) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dlon / 2), 2.0);
            double c = 2 * Math.Asin(Math.Sqrt(a));
            return EarthRadius * c;
        }
        private static double sqr(double val)
        {
            return val * val;
        }
        public static double DegToRad(double deg)
        {
            return (deg / 180.0 * Math.PI);
        }
        #endregion LENGTH        

        /// <summary>
        ///     Находим путь от исходной координаты до ближайшей линии в ближайшем узле
        /// </summary>
        /// <param name="Lat">Широта</param>
        /// <param name="Lon">Долгота</param>
        /// <param name="metersRadius">Радиус поиска ближайшей линии в метрах</param>
        /// <returns>Ближайшая линия и узел</returns>
        public FindStartStopResult FindNodeStart(float Lat, float Lon, float metersRadius)
        {
            return FindStartStop(Lat, Lon, metersRadius, true);
        }

        /// <summary>
        ///     Находим путь до конечной координаты от ближайшей линии в ближайшем узле
        /// </summary>
        /// <param name="Lat">Широта</param>
        /// <param name="Lon">Долгота</param>
        /// <param name="metersRadius">Радиус поиска ближайшей линии в метрах</param>
        /// <returns>Ближайшая линия и узел</returns>
        public FindStartStopResult FindNodeEnd(float Lat, float Lon, float metersRadius)
        {
            return FindStartStop(Lat, Lon, metersRadius, false);
        }

        /// <summary>
        ///     Находим путь от исходной/конечной координаты по ближайшей линии в ближайший узел
        /// </summary>
        /// <param name="Lat">Широта</param>
        /// <param name="Lon">Долгота</param>
        /// <param name="metersRadius">Радиус поиска ближайшей линии в метрах</param>
        /// <param name="TrueStartFalseEnd">true - если начало, false - если конец</param>
        /// <returns>Ближайшая линия и узел</returns>
        private FindStartStopResult FindStartStop(float Lat, float Lon, float metersRadius, bool TrueStartFalseEnd)
        {
            FindStartStopResult res = new FindStartStopResult();
            res.distToLine = float.MaxValue;
            res.distToN = 0;
            res.distToR = 0;
            res.normal = new PointF[0];
            res.revers = new PointF[0];
            res.line = 0;
            res.nodeN = 0;
            res.nodeR = 0;

            float dLat = metersRadius / GetLengthMeters(Lat, Lon, Lat + 1, Lon, false);
            float dLon = metersRadius / GetLengthMeters(Lat, Lon, Lat, Lon + 1, false);
            
            float LatMin = Lat - dLat;            
            float LonMin = Lon - dLon;
            float LatMax = Lat + dLat;
            float LonMax = Lon + dLon;

            List<uint> lines = new List<uint>();

            segm_str.Position = RMSEGMENTS.Length + 4;

            byte[] ba = new byte[8190];
            int read = 0;

            while ((read = segm_str.Read(ba, 0, ba.Length)) > 0)
            {
                int count = read / segm_record_length;
                for (int i = 0; i < count; i++)
                {
                    int off = segm_record_length * i;
                    uint line_no = BitConverter.ToUInt32(ba, off+0);
                    ushort segm = BitConverter.ToUInt16(ba, off + 4);
                    float lat0 = BitConverter.ToSingle(ba, off + 6);
                    float lon0 = BitConverter.ToSingle(ba, off + 10);
                    float lat1 = BitConverter.ToSingle(ba, off + 14);
                    float lon1 = BitConverter.ToSingle(ba, off + 18);
                    float k = BitConverter.ToSingle(ba, off + 22);
                    float b = BitConverter.ToSingle(ba, off + 26);

                    // check line outbounds
                    if ((lat0 < LatMin) && (lat1 < LatMin)) continue;
                    if ((lat0 > LatMax) && (lat1 > LatMax)) continue;
                    if ((lon0 < LonMin) && (lon1 < LonMin)) continue;
                    if ((lon0 > LonMax) && (lon1 > LonMax)) continue;

                    // check line cross bounds
                    float y1 = k * LatMin + b;
                    float y2 = k * LatMax + b;
                    float x1 = (LonMax - b) / k;
                    float x2 = (LonMin - b) / k;
                    if (
                        ((y1 >= LonMin) && (y1 <= LonMax))
                        ||
                        ((y2 >= LonMin) && (y2 <= LonMax))
                        ||
                        ((x1 >= LatMin) && (x1 <= LatMax))
                        ||
                        ((x2 >= LatMin) && (x2 <= LatMax))
                        )
                    {
                        lines.Add(line_no);
                    };
                };
            };
                        
            PointF searchNearPoint = new PointF(Lon, Lat);
            ushort segmentNo = 0;
            PointF onLinePoint = new PointF(0, 0);
            int side = 0;

            // check nearest line from found
            for (int i = 0; i < lines.Count; i++)
            {
                PointF[] ss = GetLineSegments(lines[i], false, false);
                for (ushort x = 1; x < ss.Length; x++)
                {
                    PointF pol;
                    int lor = 0;
                    DistanceFromPointToLine(searchNearPoint, ss[x - 1], ss[x], out pol, out lor);
                    float l = GetLengthMeters(Lat, Lon, pol.Y, pol.X, false);
                    if (l < res.distToLine)
                    {
                        res.distToLine = l;
                        res.line = lines[i];
                        segmentNo = x;
                        onLinePoint = pol;
                        side = lor;
                    };
                };                                               
            };

            if (res.line == 0) // not found
                return res;

            ushort ttlsegments;
            int pos;
            bool oneway;
            uint node1;
            uint node2;
            GetLine(res.line, out ttlsegments, out pos, out oneway, out node1, out node2);
            PointF[] polyline = GetLineSegments(res.line, pos, ttlsegments, false, false);

            List<PointF> vectorN = new List<PointF>();
            List<PointF> vectorR = new List<PointF>();
            uint nodeN = 0;
            uint nodeR = 0;
            
            // если начало
            bool retN = oneway || (side*-1 > 0);
            if (TrueStartFalseEnd)
            {                
                vectorN.Add(searchNearPoint);
                if ((!float.IsNaN(onLinePoint.X)) && (!float.IsNaN(onLinePoint.Y)))
                    vectorN.Add(onLinePoint);
                vectorR.Add(searchNearPoint);
                if ((!float.IsNaN(onLinePoint.X)) && (!float.IsNaN(onLinePoint.Y)))
                    vectorR.Add(onLinePoint);

                nodeN = node2;
                nodeR = node1;
                
                for (int i = segmentNo; i < polyline.Length; i++)
                    vectorN.Add(polyline[i]);
                for (int i = segmentNo - 1; i >= 0; i--)
                    vectorR.Add(polyline[i]);                
            }
            else // если конец
            {
                for (int i = 0; i < segmentNo; i++)
                    vectorN.Add(polyline[i]);
                for (int i = polyline.Length - 1; i >= segmentNo; i--)
                    vectorR.Add(polyline[i]);

                if ((!float.IsNaN(onLinePoint.X)) && (!float.IsNaN(onLinePoint.Y)))
                    vectorN.Add(onLinePoint);
                vectorN.Add(searchNearPoint);
                if ((!float.IsNaN(onLinePoint.X)) && (!float.IsNaN(onLinePoint.Y)))
                    vectorR.Add(onLinePoint);
                vectorR.Add(searchNearPoint);

                nodeN = node1;
                nodeR = node2; 
            };

            // fill_up vectors & nodes
            if (retN)
            {
                res.normal = vectorN.ToArray();
                res.revers = vectorR.ToArray();
                res.nodeN = nodeN;
                res.nodeR = nodeR;
            }
            else
            {
                res.normal = vectorR.ToArray();
                res.revers = vectorN.ToArray();
                res.nodeN = nodeR;
                res.nodeR = nodeN;
            };

            // dist to nodes
            for (int i = 1; i < res.normal.Length; i++)
                res.distToN += GetLengthMeters(res.normal[i - 1].Y, res.normal[i - 1].X, res.normal[i].Y, res.normal[i].X, false);
            for (int i = 1; i < res.revers.Length; i++)
                res.distToR += GetLengthMeters(res.revers[i - 1].Y, res.revers[i - 1].X, res.revers[i].Y, res.revers[i].X, false);

            return res;
        }

        /// <summary>
        ///     Получаем угол поворота двух линий
        /// </summary>
        /// <param name="prev">Начальная точка</param>
        /// <param name="turn">Точка поворота</param>
        /// <param name="next">Конечная точка</param>
        /// <returns>Угол, если меньше 0 то левый</returns>
        public static float GetLinesTurnAngle(PointF prev, PointF turn, PointF next)
        {
            float dy0 = turn.Y - prev.Y;
            float dx0 = turn.X - prev.X;
            float dy1 = next.Y - turn.Y;
            float dx1 = next.X - turn.X;

            int side = Math.Sign((turn.X - prev.X) * (next.Y - prev.Y) - (turn.Y - prev.Y) * (next.X - prev.X));
            return (float)(-1 * side * Math.Acos(((dx0 * dx1) + (dy0 * dy1)) / ((Math.Sqrt(dx0 * dx0 + dy0 * dy0)) * (Math.Sqrt(dx1 * dx1 + dy1 * dy1)))) / Math.PI * 180);
        }

        /// <summary>
        ///     Получаем угол поворота двух линий
        /// </summary>
        /// <param name="prev">Начальная точка</param>
        /// <param name="turn">Точка поворота</param>
        /// <param name="next">Конечная точка</param>
        /// <returns>Угол, если меньше 0 то левый</returns>
        public static float GetLinesTurnAngle(PointFL prev, PointFL turn, PointFL next)
        {
            return GetLinesTurnAngle(new PointF(prev.X, prev.Y), new PointF(turn.X, turn.Y), new PointF(next.X, next.Y));
        }

        /// <summary>
        ///     Расчет расстояния от точки до линии
        /// </summary>
        /// <param name="pt">Искомая точка</param>
        /// <param name="lineStart">Нач тчк линии</param>
        /// <param name="lineEnd">Кон тчк линии</param>
        /// <param name="pointOnLine">точка на линии ближайшая к искомой</param>
        /// <param name="side">С какой стороны линии находится искомая точка</param>
        /// <returns>метры</returns>
        private static double DistanceFromPointToLine(PointF pt, PointF lineStart, PointF lineEnd, out PointF pointOnLine, out int side)
        {
            float dx = lineEnd.X - lineStart.X;
            float dy = lineEnd.Y - lineStart.Y;

            side = Math.Sign((lineEnd.X - lineStart.X) * (pt.Y - lineStart.Y) - (lineEnd.Y - lineStart.Y) * (pt.X - lineStart.X));

            // Calculate the t that minimizes the distance.
            float t = ((pt.X - lineStart.X) * dx + (pt.Y - lineStart.Y) * dy) / (dx * dx + dy * dy);

            // See if this represents one of the segment's
            // end points or a point in the middle.
            if (t < 0)
            {
                pointOnLine = new PointF(lineStart.X, lineStart.Y);
                dx = pt.X - lineStart.X;
                dy = pt.Y - lineStart.Y;
            }
            else if (t > 1)
            {
                pointOnLine = new PointF(lineEnd.X, lineEnd.Y);
                dx = pt.X - lineEnd.X;
                dy = pt.Y - lineEnd.Y;
            }
            else
            {
                pointOnLine = new PointF(lineStart.X + t * dx, lineStart.Y + t * dy);
                dx = pt.X - pointOnLine.X;
                dy = pt.Y - pointOnLine.Y;
            };

            return Math.Sqrt(dx * dx + dy * dy);
        }
    }

    /// <summary>
    ///     Читает названия линий по номеру из файла
    /// </summary>
    public class LinesNamesFileReader
    {
        private string fileName;
        public string FileName { get { return fileName; } }
        private FileStream fs;
        private List<long> pos = new List<long>();

        public LinesNamesFileReader(string fileName)
        {
            this.fileName = fileName;
            fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            byte[] ba = new byte[1024];
            int read = 0;
            while ((read = fs.Read(ba,0,ba.Length)) > 0)
            {                
                string s = System.Text.Encoding.GetEncoding(1251).GetString(ba, 0, read);
                s = s.Substring(0,s.IndexOf("\r\n"));
                fs.Position = fs.Position - read + s.Length + 2;
                pos.Add(fs.Position);
            };
        }

        public string this[uint line]
        {
            get
            {
                fs.Position = pos[(int)line - 1];
                byte[] ba = new byte[1024];
                fs.Read(ba, 0, ba.Length);
                string s = System.Text.Encoding.GetEncoding(1251).GetString(ba, 0, ba.Length);
                return s.Substring(0, s.IndexOf("\r\n"));
            }
        }

        public void Close()
        {
            fs.Close();
            fs = null;
        }

        ~LinesNamesFileReader()
        {
            if(fs != null) this.Close();
        }

    }
}
