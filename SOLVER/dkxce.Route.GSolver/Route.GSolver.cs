/* 
 * C# Class by Milok Zbrozek <milokz@gmail.com>
 * Модуль для рассчета маршрутов по графам, 
 * используя алгоритмы Дейкстры и A*
 * в памяти и через дисковые файлы,
 * с поддержкой расчета с учетом трафика
 * Author: Milok Zbrozek <milokz@gmail.com>
 * Версия: 211221DA
 * 
 * Поддерживает дорожные (GARMIN, OSM, OSM2SHP) и водные графы (WATER)
 * 
 */

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

using dkxce.Route.Classes;

namespace dkxce.Route.GSolver
{
    /// <summary>
    ///     Модуль расчета маршрутов графа
    ///     по Алгоритмам Дейкстры, Дейкстры(reversed) и A* (A* rev)
    /// </summary>
    public class RMGraph
    {
        public const string _GSolver = "dkxce.Route.GSolver/21.12.23.3-V4-win32";

        public class TMCJAMServerConfig
        {
            public int RegionID = 0;
            public string Server = "127.0.0.1";
            public int Port = 7756;
        }

        /// <summary>
        ///     Когда подгружать сегменты линий в память
        /// </summary>
        public enum SegmentsInMemoryPreLoad : byte
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
        private static byte[] header_RMGRAF2 = new byte[] { 0x52, 0x4D, 0x47, 0x52, 0x41, 0x46, 0x32 };
        private static byte[] header_RMGRAF3 = new byte[] { 0x52, 0x4D, 0x47, 0x52, 0x41, 0x46, 0x33 };
        private static byte[] header_RMINDEX = new byte[] { 0x52, 0x4D, 0x49, 0x4E, 0x44, 0x45, 0x58 };
        private static byte[] header_RMLINES = new byte[] { 0x52, 0x4D, 0x4C, 0x49, 0x4E, 0x45, 0x53 };
        private static byte[] header_RMSEGMENTS = new byte[] { 0x52, 0x4D, 0x53, 0x45, 0x47, 0x4D, 0x45, 0x4E, 0x54, 0x53 };
        private static byte[] header_RMPOINTNLL0 = new byte[] { 0x52, 0x4D, 0x50, 0x4F, 0x49, 0x4E, 0x54, 0x4E, 0x4C, 0x4C, 0x30 };
        private static byte[] header_RMPOINTNLL1 = new byte[] { 0x52, 0x4D, 0x50, 0x4F, 0x49, 0x4E, 0x54, 0x4E, 0x4C, 0x4C, 0x31 };
        private static byte[] header_RMLINKIDS = new byte[] { 0x52, 0x4D, 0x4C, 0x49, 0x4E, 0x4B, 0x49, 0x44, 0x53 };
        private static byte[] header_RMLATTRIB = new byte[] { 0x52, 0x4D, 0x4C, 0x41, 0x54, 0x54, 0x52, 0x49, 0x42 };
        private static byte[] header_RMTURNRSTR = new byte[] { 0x52, 0x4D, 0x54, 0x55, 0x52, 0x4E, 0x52, 0x53, 0x54, 0x52 };

        private const byte const_LineRecordLength = 15;
        private const byte const_SegmRecordLength = 30;

        /// <summary>
        ///     Загружать сегменты линий в память или нет
        /// </summary>
        private SegmentsInMemoryPreLoad stream_inMemSegPreload = SegmentsInMemoryPreLoad.inMemoryCalculations;
        /// <summary>
        ///     Загружать сегменты линий в память или нет
        /// </summary>
        public SegmentsInMemoryPreLoad PreLoadedLineSegments { get { return stream_inMemSegPreload; } }

        /// <summary>
        ///     Строим путь от обратного?
        /// </summary>
        private bool calc_Reversed = false;
        /// <summary>
        ///     Строим путь от обратного?
        ///     исп-ся для пути ИЗ МНОГИХ в ОДНУ (x[] --> y)
        /// </summary>
        public bool IsCalcReversed { get { return calc_Reversed; } }

        /// <summary>
        ///     Критерий оптимизации маршрута
        /// </summary>
        private MinimizeBy calc_minBy = MinimizeBy.Cost;
        /// <summary>
        ///     Критерий оптимизации маршрута
        /// </summary>
        public MinimizeBy MinimizeRouteBy { get { return calc_minBy; } set { calc_minBy = value; } }

        /// <summary>
        ///     Условия построения маршрута через аттрибуты
        /// </summary>
        private byte[] goThrough = null;
        /// <summary>
        ///     Условия построения маршрута через аттрибуты
        /// </summary>
        public byte[] GoThrough { get { return goThrough; } set { goThrough = value; } }

        /// <summary>
        ///     Избегать низкий ROUTE_LEVEL
        /// </summary>
        private bool avoidLowRouteLevel = false;
        /// <summary>
        ///     Избегать низкий Route_Level при построении маршрутов
        /// </summary>
        public bool AvoidLowRouteLevel { get { return avoidLowRouteLevel; } set { avoidLowRouteLevel = value; } }

        /// <summary>
        ///     Запрет прохождения маршрута через линии
        /// </summary>
        private List<uint> goExcept = new List<uint>();
        /// <summary>
        ///     Запрет прохождения маршрута через линии
        /// </summary>
        public List<uint> GoExcept { get { return goExcept; } set { goExcept = value; } }

        /// <summary>
        ///     Максимальная длина между узлами графа
        /// </summary>
        private Single graph_maxDistBetweenNodes = 0;
        /// <summary>
        ///     Максимальная длина между узлами графа
        /// </summary>
        public Single MaxDistanceBetweenNodes { get { return graph_maxDistBetweenNodes; } }

        /// <summary>
        ///     Максимальная ошибка для расчетов (исп-ся для определения незаполненных клеток)
        /// </summary>
        public const Single const_maxError = (Single)1e-6;
        /// <summary>
        ///     Максимальное значение оценки, больше которого оценка является бесконечной
        /// </summary>
        public const Single const_maxValue = (Single)1e+30;

        /// <summary>
        ///     Смещение следующего ближайшего узла в матрице относительно начала элемента массива
        /// </summary>
        private const byte const_offset_Next = 0;
        /// <summary>
        ///     Смещение оценки пути относительно начала элемента массива
        /// </summary>
        private const byte const_offset_Cost = 4;
        /// <summary>
        ///     Смещение длины пути относительно начала элемента массива
        /// </summary>
        private const byte const_offset_Dist = 8;
        /// <summary>
        ///     Смещение времени в пути относительно начала элемента массива
        /// </summary>
        private const byte const_offset_Time = 12;
        /// <summary>
        ///     Смещение линии до точки
        /// </summary>
        private const byte const_offset_Line = 16;
        /// <summary>
        ///     Смещение реверса линии до точки
        /// </summary>
        private const byte const_offset_Reverse = 20;


        /// <summary>
        ///     Размер ячейки графа
        /// </summary>
        private const int const_vtGraphElemLength = 4 + 4 + 4 + 4 + 4 + 1; // next_node cost dist time line rev

        /// <summary>
        ///     Размер ячейки вектора
        /// </summary>
        private const int const_vtArrElemLength = 4 + 4 + 4 + 4; // next_node cost dist time

        /// <summary>
        ///     В памяти ли граф или в файле
        /// </summary>
        private bool graph_InMemory = false;

        /// <summary>
        ///     Число узлов в графе
        /// </summary>
        private int graph_NodesCount = 0;
        /// <summary>
        ///     Число линий в графе
        /// </summary>
        private int graph_LinesCount = 0;
        /// <summary>
        ///     Число сегментов в графе
        /// </summary>
        private int graph_LinesSegmentsCount = 0;

        #region streams
        /// <summary>
        ///     Указатель на файл графа
        /// </summary>
        private Stream stream_Graph = null;
        private System.Threading.Mutex stream_Graph_Mtx = null;
        /// <summary>
        ///     Указатель на инверсный файл графа
        /// </summary>
        private Stream stream_Graph_R = null;
        private System.Threading.Mutex stream_Graph_R_Mtx = null;
        /// <summary>
        ///     Указатель на индексный файл графа
        /// </summary>
        private Stream stream_Index = null;
        private System.Threading.Mutex stream_Index_Mtx = null;
        /// <summary>
        ///     Указатель на индексный файл инверсного графа
        /// </summary>
        private Stream stream_Index_R = null;
        private System.Threading.Mutex stream_Index_R_Mtx = null;
        /// <summary>
        ///     Указатель на файл линий
        /// </summary>
        private Stream stream_Lines = null;
        private System.Threading.Mutex stream_Lines_Mtx = null;
        /// <summary>
        ///     Указатель на атрибуты линий
        /// </summary>
        private Stream stream_LAttr = null;
        private System.Threading.Mutex stream_LAttr_Mtx = null;
        /// <summary>
        ///     Указатель на файл сегментов
        /// </summary>
        private Stream stream_LinesSegments = null;
        private System.Threading.Mutex stream_LinesSegments_Mtx = null;
        /// <summary>
        ///     Указатель на файл координат графа
        /// </summary>
        private Stream stream_Geo = null;
        private System.Threading.Mutex stream_Geo_Mtx = null;
        /// <summary>
        ///     Указатель на индексный файл координат графа
        /// </summary>
        private Stream stream_Geo_LL = null;
        private System.Threading.Mutex stream_Geo_LL_Mtx = null;
        #endregion

        /// <summary>
        ///     Открытый вектор, содержащий информацию о всех конечныйх точках (узлах) и маршрутах
        /// </summary>
        private Stream stream_Vector = null;

        /// <summary>
        ///     Имя файла графа
        /// </summary>
        private string stream_FileName = null;
        public int RegionID = 0;
        /// <summary>
        ///     Префикс имен файла графа
        /// </summary>
        private string stream_FileMain = null;
        /// <summary>
        ///     Конфигурация сервера пробок для текущего графа
        /// </summary>
        public TMCJAMServerConfig TmcJamSvrCfg = new TMCJAMServerConfig();
        private byte TmcJamSvrErr = 0;

        /// <summary>
        ///     Число узлов в графе
        /// </summary>
        public int NodesCount { get { return graph_NodesCount; } }
        /// <summary>
        ///     Число линий в графе
        /// </summary>
        public int LinesCount { get { return graph_LinesCount; } }
        /// <summary>
        ///    Общее число сегментов в графе
        /// </summary>
        public int SegmentsCount { get { return graph_LinesSegmentsCount; } }

        /// <summary>
        ///     Private only
        /// </summary>
        private RMGraph() { }

        /// <summary>
        ///     В памяти ли граф или в файле
        /// </summary>
        public bool IsGraphInMemory { get { return graph_InMemory; } }

        /// <summary>
        ///     Имя файла графа
        /// </summary>
        public string FileName { get { return stream_FileName; } }

        ///// <summary>
        /////     Информация о поправках cost для расчета маршрута (трафик)
        ///// </summary>
        //private LineExtCostInfo[] extCostInfo = null;

        ///// <summary>
        /////     Используется ли внешняя поправочная информация для оценки пути (трафик)
        ///// </summary>
        //public bool ExternalCostInfoUsed { get { return extCostInfo != null && extCostInfo.Length > 0; } }

        ///// <summary>
        /////     Информация о поправках time для расчета маршрута (трафик)
        ///// </summary>
        //private LineExtCostInfo[] extTimeInfo = null;

        ///// <summary>
        /////     Используется ли внешняя поправочная информация для времени в пути (трафик)
        ///// </summary>
        //public bool ExternalTimeInfoUsed { get { return extTimeInfo != null && extTimeInfo.Length > 0; } }

        /// <summary>
        ///     Используется ли внешняя поправочная информация для построения маршрута      
        /// </summary>
        public bool TrafficUse { get { return traffic_UseCurrent || traffic_UseHistory; } set { traffic_UseCurrent = true; } }

        /// <summary>
        ///     Используется ли текущая внешняя поправочная информация для построения маршрута    
        /// </summary>
        private bool traffic_UseCurrent = false;

        /// <summary>
        ///     Используется ли текущая внешняя поправочная информация для построения маршрута    
        /// </summary>
        public bool TrafficUseCurrent { get { return traffic_UseCurrent; } set { traffic_UseCurrent = value; } }

        /// <summary>
        ///     Используется ли исторический трафик в качестве поправочной информации для построения маршрута    
        /// </summary>
        private bool traffic_UseHistory = false;

        /// <summary>
        ///     Используется ли исторический трафик в качестве поправочной информации для построения маршрута    
        /// </summary>
        public bool TrafficUseHistory { get { return traffic_UseHistory; } set { traffic_UseHistory = value; } }

        /// <summary>
        ///     Время начала движения для построения маршрута с учетом внешней попровочной информации
        /// </summary>
        private DateTime traffic_StartTime;// = DateTime.Now;

        /// <summary>
        ///     Время начала движения для построения маршрута с учетом внешней попровочной информации
        /// </summary>
        public DateTime TrafficStartTime { get { return traffic_StartTime; } set { traffic_StartTime = value; } }

        /// <summary>
        ///     В течении какого времени от начала движения используется
        ///     внешняя информация о добавочной оценки передвижения для
        ///     построения маршрута (информация о трафике используется
        ///     в первые extCostTimeLimit минут пути)
        /// </summary>
        private int traffic_CurrentTimeout = 25;

        /// <summary>
        ///     В течении какого времени от начала движения используется
        ///     внешняя информация о добавочной оценки передвижения для
        ///     построения маршрута (информация о трафике используется
        ///     в первые x минут пути)
        /// </summary>
        public int TrafficCurrentTimeout { get { return traffic_CurrentTimeout; } set { traffic_CurrentTimeout = value; } }

        /// <summary>
        ///     Загружаем граф из файла для работы с ним на диске
        /// </summary>
        /// <param name="fileName">Имя файла для хранения графа</param>
        /// <returns></returns>
        public static RMGraph WorkWithDisk(string fileName)
        {
            return WorkWithDisk(fileName, 0);
        }

        private static TMCJAMServerConfig loadTMCJamServerConfig(string fileMain)
        {
            TMCJAMServerConfig cfg = new TMCJAMServerConfig();
            string tmcjamcfgfile = fileMain + ".jam.cfg";
            if (File.Exists(tmcjamcfgfile))
            {
                FileStream jfs = new FileStream(tmcjamcfgfile, FileMode.Open, FileAccess.Read);
                StreamReader jsr = new StreamReader(jfs);
                string ln = jsr.ReadToEnd().Trim();
                jsr.Close();
                jfs.Close();
                try
                {
                    ln = ln.ToLower().Trim();
                    ln = ln.Replace("tcp://", "");
                    ln = ln.Replace("/", "");
                    cfg.RegionID = int.Parse(ln.Substring(0, ln.IndexOf("@")));
                    ln = ln.Remove(0, ln.IndexOf("@") + 1);
                    cfg.Server = ln.Substring(0, ln.IndexOf(":"));
                    cfg.Port = int.Parse(ln.Remove(0, ln.IndexOf(":") + 1));
                }
                catch
                {
                    return cfg;
                };
            };
            return cfg;
        }

        /// <summary>
        ///     Загружаем граф из файла для работы с ним на диске
        /// </summary>
        /// <param name="fileName">Имя файла для хранения графа</param>
        /// <returns></returns>
        public static RMGraph WorkWithDisk(string fileName, int RegionID)
        {
            RMGraph v = new RMGraph();
            v.stream_inMemSegPreload = SegmentsInMemoryPreLoad.onDiskCalculations;
            v.stream_FileName = fileName;
            v.stream_FileMain = fileName.Substring(0, fileName.LastIndexOf("."));
            v.TmcJamSvrCfg = loadTMCJamServerConfig(v.stream_FileMain);
            v.stream_Graph = new FileStream(v.stream_FileMain + ".graph.bin", FileMode.Open, FileAccess.Read);
            byte[] bb = new byte[header_RMGRAF2.Length];
            v.stream_Graph.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMGRAF2")
            {
                v.stream_Graph.Close();
                throw new IOException("Unknown file format:\r\n" + v.stream_FileMain + ".graph.bin");
            };
            bb = new byte[4];
            v.stream_Graph.Read(bb, 0, 4);
            v.graph_NodesCount = BitConverter.ToInt32(bb, 0);
            v.stream_Graph.Read(bb, 0, 4);
            v.graph_maxDistBetweenNodes = BitConverter.ToSingle(bb, 0);

            v.stream_Graph_R = new FileStream(v.stream_FileMain + ".graph[r].bin", FileMode.Open, FileAccess.Read);
            bb = new byte[header_RMGRAF3.Length];
            v.stream_Graph_R.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMGRAF3")
            {
                v.stream_Graph.Close();
                v.stream_Graph_R.Close();
                throw new IOException("Unknown file format:\r\n" + v.stream_FileMain + ".graph[r].bin");
            };

            v.stream_Index = new FileStream(v.stream_FileMain + ".graph.bin.in", FileMode.Open, FileAccess.Read);
            bb = new byte[header_RMINDEX.Length];
            v.stream_Index.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMINDEX")
            {
                v.stream_Graph.Close();
                v.stream_Graph_R.Close();
                v.stream_Index.Close();
                throw new Exception("Unknown file format:\r\n" + v.stream_FileMain + ".graph.bin.in");
            };

            v.stream_Index_R = new FileStream(v.stream_FileMain + ".graph[r].bin.in", FileMode.Open, FileAccess.Read);
            bb = new byte[header_RMINDEX.Length];
            v.stream_Index_R.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMINDEX")
            {
                v.stream_Graph.Close();
                v.stream_Graph_R.Close();
                v.stream_Index.Close();
                v.stream_Index_R.Close();
                throw new Exception("Unknown file format:\r\n" + v.stream_FileMain + ".graph[r].bin.in");
            };

            v.stream_Lines = new FileStream(v.stream_FileMain + ".lines.bin", FileMode.Open, FileAccess.Read);
            bb = new byte[header_RMLINES.Length];
            v.stream_Lines.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMLINES")
            {
                v.stream_Graph.Close();
                v.stream_Graph_R.Close();
                v.stream_Index.Close();
                v.stream_Index_R.Close();
                v.stream_Lines.Close();
                throw new Exception("Unknown file format:\r\n" + v.stream_FileMain + ".lines.bin");
            };
            bb = new byte[4];
            v.stream_Lines.Read(bb, 0, bb.Length);
            v.graph_LinesCount = BitConverter.ToInt32(bb, 0);


            v.stream_LinesSegments = new FileStream(v.stream_FileMain + ".segments.bin", FileMode.Open, FileAccess.Read);
            bb = new byte[header_RMSEGMENTS.Length];
            v.stream_LinesSegments.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMSEGMENTS")
            {
                v.stream_Graph.Close();
                v.stream_Graph_R.Close();
                v.stream_Index.Close();
                v.stream_Index_R.Close();
                v.stream_Lines.Close();
                v.stream_LinesSegments.Close();
                throw new Exception("Unknown file format:\r\n" + v.stream_FileMain + ".segments.bin");
            };
            bb = new byte[4];
            v.stream_LinesSegments.Read(bb, 0, bb.Length);
            v.graph_LinesSegmentsCount = BitConverter.ToInt32(bb, 0);

            //////////

            v.stream_Geo = new FileStream(v.stream_FileMain + ".graph.geo", FileMode.Open, FileAccess.Read);
            byte[] ba = new byte[header_RMPOINTNLL0.Length];
            v.stream_Geo.Read(ba, 0, ba.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(ba) != "RMPOINTNLL0")
            {
                v.stream_Graph.Close();
                v.stream_Graph_R.Close();
                v.stream_Index.Close();
                v.stream_Index_R.Close();
                v.stream_Lines.Close();
                v.stream_LinesSegments.Close();
                v.stream_Geo.Close();
                throw new IOException("Unknown file format:\r\n" + v.stream_FileMain + ".graph.geo");
            };

            v.stream_Geo_LL = new FileStream(v.stream_FileMain + ".graph.geo.ll", FileMode.Open, FileAccess.Read);
            ba = new byte[header_RMPOINTNLL1.Length];
            v.stream_Geo_LL.Read(ba, 0, ba.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(ba) != "RMPOINTNLL1")
            {
                v.stream_Graph.Close();
                v.stream_Graph_R.Close();
                v.stream_Index.Close();
                v.stream_Index_R.Close();
                v.stream_Lines.Close();
                v.stream_LinesSegments.Close();
                v.stream_Geo.Close();
                v.stream_Geo_LL.Close();
                throw new IOException("Unknown file format:\r\n" + v.stream_FileMain + ".graph.geo.ll");
            };

            if (File.Exists(v.stream_FileMain + ".lines.att"))
            {
                v.stream_LAttr = new FileStream(v.stream_FileMain + ".lines.att", FileMode.Open, FileAccess.Read);
                bb = new byte[header_RMLATTRIB.Length];
                v.stream_LAttr.Read(bb, 0, bb.Length);
                if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMLATTRIB")
                {
                    v.stream_Graph.Close();
                    v.stream_Graph_R.Close();
                    v.stream_Index.Close();
                    v.stream_Index_R.Close();
                    v.stream_Lines.Close();
                    v.stream_LinesSegments.Close();
                    v.stream_Geo.Close();
                    v.stream_Geo_LL.Close();
                    v.stream_LAttr.Close();
                    throw new Exception("Unknown file format:\r\n" + v.stream_FileMain + ".lines.att");
                };
            }
            else v.stream_LAttr = null;

            return v;
        }

        /// <summary>
        ///     Загружаем граф из файла в память для работы с ним
        /// </summary>
        /// <param name="fileName">Имя файла</param>
        /// <returns></returns>
        public static RMGraph LoadToMemory(string fileName)
        {
            return LoadToMemory(fileName, SegmentsInMemoryPreLoad.inMemoryCalculations, 0);
        }

        /// <summary>
        ///     Загружаем граф из файла в память для работы с ним
        /// </summary>
        /// <param name="fileName">Имя файла</param>
        /// <returns></returns>
        public static RMGraph LoadToMemory(string fileName, int RegionID)
        {
            return LoadToMemory(fileName, SegmentsInMemoryPreLoad.inMemoryCalculations, RegionID);
        }

        /// <summary>
        ///     Загружаем граф из файла в память для работы с ним
        ///     (используется, если к графу имеют доступ несколько объектов/потоков)
        /// </summary>
        /// <param name="fileName">Имя файла</param>
        /// <param name="globalObjectUnicalName">Уникальное имя графа в памяти</param>
        /// <returns></returns>
        public static RMGraph LoadToMemoryGlobal(string fileName, string globalObjectUnicalName)
        {
            return LoadToMemoryGlobal(fileName, globalObjectUnicalName, 0);
        }

        /// <summary>
        ///     Загружаем граф из файла в память для работы с ним
        ///     (используется, если к графу имеют доступ несколько объектов/потоков)
        /// </summary>
        /// <param name="fileName">Имя файла</param>
        /// <param name="globalObjectUnicalName">Уникальное имя графа в памяти</param>
        /// <returns></returns>
        public static RMGraph LoadToMemoryGlobal(string fileName, string globalObjectUnicalName, int RegionID)
        {
            if (globalObjectUnicalName == String.Empty)
                throw new Exception("You must set globalObjectUnicalName!");

            RMGraph g = LoadToMemory(fileName, SegmentsInMemoryPreLoad.inMemoryCalculations, RegionID);

            // mutexes
            g.PreparedToGlobalObject_UnicalName = globalObjectUnicalName;
            g.stream_Graph_Mtx = new System.Threading.Mutex(false, g.PreparedToGlobalObject_UnicalName + "_g");
            g.stream_Graph_R_Mtx = new System.Threading.Mutex(false, g.PreparedToGlobalObject_UnicalName + "_gr");
            g.stream_Index_Mtx = new System.Threading.Mutex(false, g.PreparedToGlobalObject_UnicalName + "_i");
            g.stream_Index_R_Mtx = new System.Threading.Mutex(false, g.PreparedToGlobalObject_UnicalName + "_ir");
            g.stream_Lines_Mtx = new System.Threading.Mutex(false, g.PreparedToGlobalObject_UnicalName + "_l");
            g.stream_LinesSegments_Mtx = new System.Threading.Mutex(false, g.PreparedToGlobalObject_UnicalName + "_ls");
            g.stream_Geo_Mtx = new System.Threading.Mutex(false, g.PreparedToGlobalObject_UnicalName + "_geo");
            g.stream_Geo_LL_Mtx = new System.Threading.Mutex(false, g.PreparedToGlobalObject_UnicalName + "_geoll");
            g.stream_LAttr_Mtx = new System.Threading.Mutex(false, g.PreparedToGlobalObject_UnicalName + "_la");

            return g;
        }

        /// <summary>
        ///     Создаем граф, подключенный к уже созданному объекту в памяти
        /// </summary>
        /// <param name="g"></param>
        /// <returns></returns>
        public static RMGraph IsolatedCopyFrom(RMGraph g)
        {
            if (!g.graph_InMemory)
                throw new Exception("Source Graph must be loaded in Memory!");
            if (g.PreparedToGlobalObject_UnicalName == String.Empty)
                throw new Exception("Source Graph must be prepared to Global Object, call PrepareToGlobalObject first!");

            RMGraph v = new RMGraph();
            v.RegionID = g.RegionID;
            v.IsIsolatedCopy = true;
            v.stream_inMemSegPreload = g.stream_inMemSegPreload;
            v.stream_FileName = g.stream_FileName;
            v.stream_FileMain = g.stream_FileMain;
            v.TmcJamSvrCfg = g.TmcJamSvrCfg;
            v.graph_InMemory = g.graph_InMemory;
            v.stream_Graph = MemoryStream.Synchronized(g.stream_Graph);

            v.graph_NodesCount = g.graph_NodesCount;
            v.graph_maxDistBetweenNodes = g.graph_maxDistBetweenNodes;

            v.stream_Graph_R = MemoryStream.Synchronized(g.stream_Graph_R);
            v.stream_Index = MemoryStream.Synchronized(g.stream_Index);
            v.stream_Index_R = MemoryStream.Synchronized(g.stream_Index_R);
            v.stream_Lines = MemoryStream.Synchronized(g.stream_Lines);
            if (g.stream_LAttr != null)
                v.stream_LAttr = MemoryStream.Synchronized(g.stream_LAttr);
            else
                v.stream_LAttr = null;
            v.graph_LinesCount = g.graph_LinesCount;

            if (v.stream_inMemSegPreload == SegmentsInMemoryPreLoad.inMemoryCalculations)
                v.stream_LinesSegments = MemoryStream.Synchronized(g.stream_LinesSegments);
            else
                v.stream_LinesSegments = new FileStream(v.stream_FileMain + ".segments.bin", FileMode.Open, FileAccess.Read);

            v.graph_LinesSegmentsCount = g.graph_LinesSegmentsCount;
            v.stream_Geo = MemoryStream.Synchronized(g.stream_Geo);
            v.stream_Geo_LL = MemoryStream.Synchronized(g.stream_Geo_LL);

            // mutexes
            v.stream_Graph_Mtx = System.Threading.Mutex.OpenExisting(g.PreparedToGlobalObject_UnicalName + "_g", System.Security.AccessControl.MutexRights.ReadPermissions | System.Security.AccessControl.MutexRights.Synchronize | System.Security.AccessControl.MutexRights.Modify);
            v.stream_Graph_R_Mtx = System.Threading.Mutex.OpenExisting(g.PreparedToGlobalObject_UnicalName + "_gr", System.Security.AccessControl.MutexRights.ReadPermissions | System.Security.AccessControl.MutexRights.Synchronize | System.Security.AccessControl.MutexRights.Modify);
            v.stream_Index_Mtx = System.Threading.Mutex.OpenExisting(g.PreparedToGlobalObject_UnicalName + "_i", System.Security.AccessControl.MutexRights.ReadPermissions | System.Security.AccessControl.MutexRights.Synchronize | System.Security.AccessControl.MutexRights.Modify);
            v.stream_Index_R_Mtx = System.Threading.Mutex.OpenExisting(g.PreparedToGlobalObject_UnicalName + "_ir", System.Security.AccessControl.MutexRights.ReadPermissions | System.Security.AccessControl.MutexRights.Synchronize | System.Security.AccessControl.MutexRights.Modify);
            v.stream_Lines_Mtx = System.Threading.Mutex.OpenExisting(g.PreparedToGlobalObject_UnicalName + "_l", System.Security.AccessControl.MutexRights.ReadPermissions | System.Security.AccessControl.MutexRights.Synchronize | System.Security.AccessControl.MutexRights.Modify);
            v.stream_LinesSegments_Mtx = System.Threading.Mutex.OpenExisting(g.PreparedToGlobalObject_UnicalName + "_ls", System.Security.AccessControl.MutexRights.ReadPermissions | System.Security.AccessControl.MutexRights.Synchronize | System.Security.AccessControl.MutexRights.Modify);
            v.stream_Geo_Mtx = System.Threading.Mutex.OpenExisting(g.PreparedToGlobalObject_UnicalName + "_geo", System.Security.AccessControl.MutexRights.ReadPermissions | System.Security.AccessControl.MutexRights.Synchronize | System.Security.AccessControl.MutexRights.Modify);
            v.stream_Geo_LL_Mtx = System.Threading.Mutex.OpenExisting(g.PreparedToGlobalObject_UnicalName + "_geoll", System.Security.AccessControl.MutexRights.ReadPermissions | System.Security.AccessControl.MutexRights.Synchronize | System.Security.AccessControl.MutexRights.Modify);
            v.stream_LAttr_Mtx = System.Threading.Mutex.OpenExisting(g.PreparedToGlobalObject_UnicalName + "_la", System.Security.AccessControl.MutexRights.ReadPermissions | System.Security.AccessControl.MutexRights.Synchronize | System.Security.AccessControl.MutexRights.Modify);

            return v;
        }
        private bool IsIsolatedCopy = false;
        private string PreparedToGlobalObject_UnicalName = String.Empty;

        /// <summary>
        ///     Graph Size in bytes
        /// </summary>
        public UInt64 GraphSize
        {
            get
            {
                ulong gs = 0
                    + (ulong)stream_Graph.Length + (ulong)stream_Graph_R.Length
                    + (ulong)stream_Index.Length + (ulong)stream_Index_R.Length
                    + (ulong)stream_Lines.Length
                    + (ulong)stream_LinesSegments.Length
                    + (ulong)stream_Geo.Length + (ulong)stream_Geo_LL.Length
                    ;
                return gs;

                //RuntimeTypeHandle th = this.GetType().TypeHandle;
                //return (ulong)(*(*(int**)&th + 1));
            }
        }

        /// <summary>
        ///     Загружаем граф из файла в память для работы с ним
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="preload"></param>
        /// <returns></returns>
        public static RMGraph LoadToMemory(string fileName, SegmentsInMemoryPreLoad preload)
        {
            return LoadToMemory(fileName, preload, 0);
        }

        /// <summary>
        ///     Загружаем граф из файла в память для работы с ним
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="preload"></param>
        /// <returns></returns>
        public static RMGraph LoadToMemory(string fileName, SegmentsInMemoryPreLoad preload, int RegionID)
        {
            RMGraph v = new RMGraph();
            v.RegionID = RegionID;
            v.stream_inMemSegPreload = preload;
            v.stream_FileName = fileName;
            v.stream_FileMain = fileName.Substring(0, fileName.LastIndexOf("."));
            v.TmcJamSvrCfg = loadTMCJamServerConfig(v.stream_FileMain);
            v.graph_InMemory = true;
            v.stream_Graph = new MemoryStream();

            FileStream fs = new FileStream(v.stream_FileMain + ".graph.bin", FileMode.Open, FileAccess.Read);
            byte[] block = new byte[8192];
            int read = 0;
            while ((read = fs.Read(block, 0, 8192)) > 0)
                v.stream_Graph.Write(block, 0, read);
            fs.Close();

            v.stream_Graph.Position = 0;
            byte[] bb = new byte[header_RMGRAF2.Length];
            v.stream_Graph.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMGRAF2")
            {
                v.stream_Graph.Close();
                throw new IOException("Unknown file format:\r\n" + v.stream_FileMain + ".graph.bin");
            };

            v.stream_Graph.Position = header_RMGRAF2.Length;
            v.stream_Graph.Read(block, 0, 4);
            v.graph_NodesCount = BitConverter.ToInt32(block, 0);
            v.stream_Graph.Read(block, 0, 4);
            v.graph_maxDistBetweenNodes = BitConverter.ToSingle(block, 0);

            v.stream_Graph_R = new MemoryStream();
            fs = new FileStream(v.stream_FileMain + ".graph[r].bin", FileMode.Open, FileAccess.Read);
            block = new byte[8192];
            read = 0;
            while ((read = fs.Read(block, 0, 8192)) > 0)
                v.stream_Graph_R.Write(block, 0, read);
            fs.Close();

            v.stream_Graph_R.Position = 0;
            bb = new byte[header_RMGRAF3.Length];
            v.stream_Graph_R.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMGRAF3")
            {
                v.stream_Graph.Close();
                v.stream_Graph_R.Close();
                throw new IOException("Unknown file format:\r\n" + v.stream_FileMain + ".graph[r].bin");
            };

            v.stream_Index = new MemoryStream();
            fs = new FileStream(v.stream_FileMain + ".graph.bin.in", FileMode.Open, FileAccess.Read);
            block = new byte[8192];
            read = 0;
            while ((read = fs.Read(block, 0, 8192)) > 0)
                v.stream_Index.Write(block, 0, read);
            fs.Close();

            v.stream_Index.Position = 0;
            bb = new byte[header_RMINDEX.Length];
            v.stream_Index.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMINDEX")
            {
                v.stream_Graph.Close();
                v.stream_Graph_R.Close();
                v.stream_Index.Close();
                throw new Exception("Unknown file format:\r\n" + v.stream_FileMain + ".graph.bin.in");
            };

            v.stream_Index_R = new MemoryStream();
            fs = new FileStream(v.stream_FileMain + ".graph[r].bin.in", FileMode.Open, FileAccess.Read);
            block = new byte[8192];
            read = 0;
            while ((read = fs.Read(block, 0, 8192)) > 0)
                v.stream_Index_R.Write(block, 0, read);
            fs.Close();

            v.stream_Index_R.Position = 0;
            bb = new byte[header_RMINDEX.Length];
            v.stream_Index_R.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMINDEX")
            {
                v.stream_Graph.Close();
                v.stream_Graph_R.Close();
                v.stream_Index.Close();
                v.stream_Index_R.Close();
                throw new Exception("Unknown file format:\r\n" + v.stream_FileMain + ".graph[r].bin.in");
            };

            v.stream_Lines = new MemoryStream();
            fs = new FileStream(v.stream_FileMain + ".lines.bin", FileMode.Open, FileAccess.Read);
            block = new byte[8192];
            read = 0;
            while ((read = fs.Read(block, 0, 8192)) > 0)
                v.stream_Lines.Write(block, 0, read);
            fs.Close();

            v.stream_Lines.Position = 0;
            bb = new byte[header_RMLINES.Length];
            v.stream_Lines.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMLINES")
            {
                v.stream_Graph.Close();
                v.stream_Graph_R.Close();
                v.stream_Index.Close();
                v.stream_Index_R.Close();
                v.stream_Lines.Close();
                throw new Exception("Unknown file format:\r\n" + v.stream_FileMain + ".lines.bin");
            };
            bb = new byte[4];
            v.stream_Lines.Read(bb, 0, bb.Length);
            v.graph_LinesCount = BitConverter.ToInt32(bb, 0);

            if (v.stream_inMemSegPreload == SegmentsInMemoryPreLoad.inMemoryCalculations)
            {
                v.stream_LinesSegments = new MemoryStream();
                fs = new FileStream(v.stream_FileMain + ".segments.bin", FileMode.Open, FileAccess.Read);
                block = new byte[8192];
                read = 0;
                while ((read = fs.Read(block, 0, 8192)) > 0)
                    v.stream_LinesSegments.Write(block, 0, read);
                fs.Close();
            }
            else
            {
                v.stream_LinesSegments = new FileStream(v.stream_FileMain + ".segments.bin", FileMode.Open, FileAccess.Read);
            };


            v.stream_LinesSegments.Position = 0;
            bb = new byte[header_RMSEGMENTS.Length];
            v.stream_LinesSegments.Read(bb, 0, bb.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMSEGMENTS")
            {
                v.stream_Graph.Close();
                v.stream_Graph_R.Close();
                v.stream_Index.Close();
                v.stream_Index_R.Close();
                v.stream_Lines.Close();
                v.stream_LinesSegments.Close();
                throw new Exception("Unknown file format:\r\n" + v.stream_FileMain + ".segments.bin");
            };
            bb = new byte[4];
            v.stream_LinesSegments.Read(bb, 0, bb.Length);
            v.graph_LinesSegmentsCount = BitConverter.ToInt32(bb, 0);

            //////////

            v.stream_Geo = new MemoryStream();
            v.stream_Geo_LL = new MemoryStream();

            fs = new FileStream(v.stream_FileMain + ".graph.geo", FileMode.Open, FileAccess.Read);
            block = new byte[8192];
            read = 0;
            while ((read = fs.Read(block, 0, 8192)) > 0)
                v.stream_Geo.Write(block, 0, read);
            fs.Close();

            v.stream_Geo.Position = 0;
            byte[] ba = new byte[header_RMPOINTNLL0.Length];
            v.stream_Geo.Read(ba, 0, ba.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(ba) != "RMPOINTNLL0")
            {
                v.stream_Graph.Close();
                v.stream_Graph_R.Close();
                v.stream_Index.Close();
                v.stream_Index_R.Close();
                v.stream_Lines.Close();
                v.stream_LinesSegments.Close();
                v.stream_Geo.Close();
                throw new IOException("Unknown file format:\r\n" + v.stream_FileMain + ".graph.geo");
            };

            fs = new FileStream(v.stream_FileMain + ".graph.geo.ll", FileMode.Open, FileAccess.Read);
            block = new byte[8192];
            read = 0;
            while ((read = fs.Read(block, 0, 8192)) > 0)
                v.stream_Geo_LL.Write(block, 0, read);
            fs.Close();

            v.stream_Geo_LL.Position = 0;
            ba = new byte[header_RMPOINTNLL1.Length];
            v.stream_Geo_LL.Read(ba, 0, ba.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(ba) != "RMPOINTNLL1")
            {
                v.stream_Graph.Close();
                v.stream_Graph_R.Close();
                v.stream_Index.Close();
                v.stream_Index_R.Close();
                v.stream_Lines.Close();
                v.stream_LinesSegments.Close();
                v.stream_Geo.Close();
                v.stream_Geo_LL.Close();
                throw new IOException("Unknown file format:\r\n" + v.stream_FileMain + ".graph.geo.ll");
            };

            if (File.Exists(v.stream_FileMain + ".lines.att"))
            {
                v.stream_LAttr = new MemoryStream();
                fs = new FileStream(v.stream_FileMain + ".lines.att", FileMode.Open, FileAccess.Read);
                block = new byte[8192];
                read = 0;
                while ((read = fs.Read(block, 0, 8192)) > 0)
                    v.stream_LAttr.Write(block, 0, read);
                fs.Close();

                v.stream_LAttr.Position = 0;
                bb = new byte[header_RMLATTRIB.Length];
                v.stream_LAttr.Read(bb, 0, bb.Length);
                if (System.Text.Encoding.GetEncoding(1251).GetString(bb) != "RMLATTRIB")
                {
                    v.stream_Graph.Close();
                    v.stream_Graph_R.Close();
                    v.stream_Index.Close();
                    v.stream_Index_R.Close();
                    v.stream_Lines.Close();
                    v.stream_LinesSegments.Close();
                    v.stream_Geo.Close();
                    v.stream_Geo_LL.Close();
                    v.stream_LAttr.Close();
                    throw new Exception("Unknown file format:\r\n" + v.stream_FileMain + ".lines.att");
                };
            }
            else v.stream_LAttr = null;

            return v;
        }

        /// <summary>
        ///     Вызвать после окончания работы с графом для высвобождения всех ресурсов
        /// </summary>
        public void Close()
        {
            // if already closed
            if (stream_Graph == null)
                return;

            // if this object to multithread memory
            if (IsIsolatedCopy)
                return;

            if (stream_Graph != null)
                stream_Graph.Close();
            stream_Graph = null;

            if (stream_Index != null)
                stream_Index.Close();
            stream_Index = null;

            if (stream_Graph_R != null)
                stream_Graph_R.Close();
            stream_Graph_R = null;

            if (stream_Index_R != null)
                stream_Index_R.Close();
            stream_Index_R = null;

            if (stream_Lines != null)
                stream_Lines.Close();
            stream_Lines = null;

            if (stream_LinesSegments != null)
                stream_LinesSegments.Close();
            stream_LinesSegments = null;

            if (stream_Geo != null)
                stream_Geo.Close();
            stream_Geo = null;

            if (stream_Geo_LL != null)
                stream_Geo_LL.Close();
            stream_Geo_LL = null;

            if (stream_LAttr != null)
                stream_LAttr.Close();
            stream_LAttr = null;
        }

        ~RMGraph()
        {
            if (stream_Graph != null) Close();
        }


        /// <summary>
        ///     Ищем заданный узел в графе и возвращаем информацию об исходящих связях
        /// </summary>
        /// <param name="i">Узел который надо найти</param>
        /// <param name="_n">Массив узлов в которые можно добраться</param>
        /// <param name="_c">Оценка путей в которые можно добраться</param>
        /// <param name="_d">Длины путей в которые можно добраться</param>
        /// <param name="_t">Время движения до узлов в которые можно добраться</param>
        /// <param name="_l">Линии по которым можно добраться</param>
        /// <param name="_r">Направление линий по которым можно добраться</param>
        /// <returns>Число ближайших исходящих связей</returns>
        public int SelectNode(uint i, out uint[] _n, out Single[] _c, out Single[] _d, out Single[] _t, out uint[] _l, out byte[] _r)
        {
            byte[] ib = new byte[4];
            if (stream_Index_Mtx != null) stream_Index_Mtx.WaitOne();
            stream_Index.Position = 4 * (i - 1) + header_RMINDEX.Length + 4;
            stream_Index.Read(ib, 0, ib.Length);
            if (stream_Index_Mtx != null) stream_Index_Mtx.ReleaseMutex();
            uint pos_in_graph = BitConverter.ToUInt32(ib, 0);

            byte nc = 0;
            byte[] dd = new byte[const_vtGraphElemLength];
            if (stream_Graph_Mtx != null) stream_Graph_Mtx.WaitOne();
            stream_Graph.Position = pos_in_graph;
            nc = (byte)stream_Graph.ReadByte();

            _n = new uint[nc];
            _c = new Single[nc];
            _d = new Single[nc];
            _t = new Single[nc];
            _l = new uint[nc];
            _r = new byte[nc];

            for (int x = 0; x < nc; x++)
            {
                stream_Graph.Read(dd, 0, dd.Length);
                _n[x] = BitConverter.ToUInt32(dd, const_offset_Next);
                _c[x] = BitConverter.ToSingle(dd, const_offset_Cost);
                _d[x] = BitConverter.ToSingle(dd, const_offset_Dist);
                _t[x] = BitConverter.ToSingle(dd, const_offset_Time);
                _l[x] = BitConverter.ToUInt32(dd, const_offset_Line);
                _r[x] = dd[const_offset_Reverse];
            };
            if (stream_Graph_Mtx != null) stream_Graph_Mtx.ReleaseMutex();

            return nc;
        }

        /// <summary>
        ///     Ищем заданный узел в графе и возвращаем информацию о входящая связях
        /// </summary>
        /// <param name="i">Узел который надо найти</param>
        /// <param name="_n">Массив узлов из которых можно добраться</param>
        /// <param name="_c">Оценка путей из которых можно добраться</param>
        /// <param name="_d">Длины путей из которых можно добраться</param>
        /// <param name="_t">Время движения из узлов из которых можно добраться</param>
        /// <param name="_l">Линии по которым можно добраться</param>
        /// <param name="_r">Направление линий по которым можно добраться</param>
        /// <returns>Число ближайших исходящих связей</returns>
        public int SelectNodeR(uint i, out uint[] _n, out Single[] _c, out Single[] _d, out Single[] _t, out uint[] _l, out byte[] _r)
        {

            byte[] ib = new byte[4];
            if (stream_Index_R_Mtx != null) stream_Index_R_Mtx.WaitOne();
            stream_Index_R.Position = 4 * (i - 1) + header_RMINDEX.Length + 4;
            stream_Index_R.Read(ib, 0, ib.Length);
            if (stream_Index_R_Mtx != null) stream_Index_R_Mtx.ReleaseMutex();
            uint pos_in_graph = BitConverter.ToUInt32(ib, 0);

            byte nc = 0;
            byte[] dd = new byte[const_vtGraphElemLength];
            if (stream_Graph_R_Mtx != null) stream_Graph_R_Mtx.WaitOne();
            stream_Graph_R.Position = pos_in_graph;
            nc = (byte)stream_Graph_R.ReadByte();

            _n = new uint[nc];
            _c = new Single[nc];
            _d = new Single[nc];
            _t = new Single[nc];
            _l = new uint[nc];
            _r = new byte[nc];

            for (int x = 0; x < nc; x++)
            {
                stream_Graph_R.Read(dd, 0, dd.Length);
                _n[x] = BitConverter.ToUInt32(dd, const_offset_Next);
                _c[x] = BitConverter.ToSingle(dd, const_offset_Cost);
                _d[x] = BitConverter.ToSingle(dd, const_offset_Dist);
                _t[x] = BitConverter.ToSingle(dd, const_offset_Time);
                _l[x] = BitConverter.ToUInt32(dd, const_offset_Line);
                _r[x] = dd[const_offset_Reverse];
            };
            if (stream_Graph_R_Mtx != null) stream_Graph_R_Mtx.ReleaseMutex();

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

            byte[] bb = new byte[4];
            stream_Vector.Position = (y - 1) * (const_vtArrElemLength) + const_offset_Cost;
            stream_Vector.Read(bb, 0, 4);
            Single d = BitConverter.ToSingle(bb, 0);
            if (d < const_maxError)
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
            stream_Vector.Position = (y - 1) * (const_vtArrElemLength) + const_offset_Cost;
            byte[] bb = BitConverter.GetBytes(cost);
            stream_Vector.Write(bb, 0, 4);
        }

        /// <summary>
        ///     Устанавливаем предшествующую точку previous пути из (X)->Y
        ///     Вызывается только между методами BeginSolve и EndSolve
        /// </summary>
        /// <param name="y">конечная точка</param>
        /// <param name="previous">конечная точка - 1</param>
        private void SetPrev(uint y, uint previous)
        {
            stream_Vector.Position = (y - 1) * (const_vtArrElemLength) + const_offset_Next;
            byte[] bb = BitConverter.GetBytes(previous);
            stream_Vector.Write(bb, 0, 4);
        }

        /// <summary>
        ///     Получаем предшествующую точку пути из (X)->Y
        ///     Вызывается только между методами BeginSolve и EndSolve
        /// </summary>
        /// <param name="y">конечная точка</param>
        /// <returns>конечная точка - 1</returns>
        private uint GetPrev(uint y)
        {
            stream_Vector.Position = (y - 1) * (const_vtArrElemLength) + const_offset_Next;
            byte[] bb = new byte[4];
            stream_Vector.Read(bb, 0, 4);
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
            if (/*TrafficUse && */(TrafficStartTime == DateTime.MinValue))
                TrafficStartTime = DateTime.Now;

            TmcJamSvrErr = 0;

            if (stream_Vector != null) stream_Vector.Close();

            if ((!inMemory) && (fileName == null)) throw new IOException("filename not specified");

            if (inMemory)
                stream_Vector = new MemoryStream();
            else
                stream_Vector = new FileStream(fileName, FileMode.Create);

            stream_Vector.SetLength(this.graph_NodesCount * (const_vtArrElemLength));
        }

        /// <summary>
        ///     Высвобождаем память и ресурсы после нахождения пути
        ///     Вызывается после метода Solve и всех Get... методов
        /// </summary>
        public void EndSolve()
        {
            if (stream_Vector == null) throw new Exception("Call BeginSolve first");
            stream_Vector.Close();
            stream_Vector = null;
        }

        public bool IsBeginSolveCalled { get { return stream_Vector != null; } }

        /// <summary>
        ///     Добавочная оценка и время для функции с учетом трафика   
        ///     [0] - оценка, [1] - время
        /// </summary>
        /// <param name="currCost">Текущая оценка отрезка</param>
        /// <param name="currTime">Текущее время отрезка</param>
        /// <param name="currDist">Текущая длина отрезка</param>
        /// <param name="line">ID линии</param>
        /// <param name="inverse">Обратное направление</param>
        /// <param name="elapsedTime">Время от начала движения</param>
        /// <returns>[0] - оценка, [1] - время</returns>
        private Single[] CallTraffic(float currCost, float currTime, float currDist, uint line, bool inverse, float elapsedTime)
        {
            // can call by TMC code (.lines.tmc) or LINK_ID (.lines.id) or line_no (.lines.bin)
            // now by .lines.bin

            float[] result = new float[] { currCost, currTime }; // in minutes  {by speed max,  by speed normal}
            // float speed = currDist / currTime * 60 / 1000;

            if (!TrafficUse)
                return result;

            TLineFlags flags = GetLineFlags(line);
            if (!flags.IsTMC)
                return result;

            if ((!TrafficUseHistory) && (elapsedTime > traffic_CurrentTimeout))
                return result;

            byte speed = 0;
            if (TrafficUseCurrent)
            {
                if (elapsedTime <= traffic_CurrentTimeout)
                    speed = CallTMCJamServer(line, inverse, true);
                else if (TrafficUseHistory)
                    speed = CallTMCJamServer(line, inverse, false);
            }
            else if (TrafficUseHistory)
            {
                speed = CallTMCJamServer(line, inverse, false);
            };

            if (speed == 0)  // no information
                return result;

            if (speed == 0xFF) // road closed = 255
                return new Single[] { 10080, 10080 }; // 7 days

            Single spMpM = speed * 1000 / 60; // meters per minute
            Single tM = currDist / spMpM; // time in minutes
            return new Single[] { tM, tM };

            // int[] lids = GetLinesLinkIDs(new uint[] { line });

            //_TrafficStartTime
        }

        /// <summary>
        ///     Получаем информацию о текущих скоростях на заданной дороге
        /// </summary>
        /// <param name="line"></param>
        /// <param name="inverse"></param>
        /// <param name="live"></param>
        /// <returns></returns>
        private byte CallTMCJamServer(uint line, bool inverse, bool live)
        {
            if (this.TmcJamSvrCfg == null) return 0;
            if (this.TmcJamSvrCfg.RegionID == 0) return 0;
            if (this.TmcJamSvrCfg.Port == 0) return 0;

            if (TmcJamSvrErr == 3) return 0; // пытаемся получить пробки только 3 раза для текущего расчета
            // счетчик сбрасывается в BeginSolve

            try
            {
                System.Net.Sockets.TcpClient cl = new System.Net.Sockets.TcpClient();
                cl.Connect(TmcJamSvrCfg.Server, TmcJamSvrCfg.Port);
                cl.ReceiveTimeout = 500;
                string cmd = String.Format("JAMTRAFFIC,GET,{0},{1},{2}\r\n", live ? "LIVE" : "HISTORY", TmcJamSvrCfg.RegionID, (int)line);
                byte[] ba = System.Text.Encoding.ASCII.GetBytes(cmd);
                cl.Client.Send(ba);

                byte[] buff = new byte[128];
                int read = cl.GetStream().Read(buff, 0, buff.Length);
                cl.Client.Close();
                string txt = System.Text.Encoding.GetEncoding(1251).GetString(buff, 0, read).Trim();
                if (txt.IndexOf("JAMTRAFFIC,GET") == 0)
                {
                    string[] splt = txt.Split(new string[] { "," }, StringSplitOptions.None);
                    if (!inverse)
                        return byte.Parse(splt[5]); // forward
                    else
                        return byte.Parse(splt[6]); // backward
                };
            }
            catch
            {
                TmcJamSvrErr++;
            };

            return 0;
        }

        /// <summary>
        ///     Расчет пути из точек starts в точку end
        /// </summary>
        /// <param name="starts">Точки старта</param>
        /// <param name="end">Точка назначения</param>
        public void SolveDeikstra(uint[] starts, uint end)
        {
            Stream tmp_g = stream_Graph;
            Stream tmp_i = stream_Index;
            stream_Graph = stream_Graph_R;
            stream_Index = stream_Index_R;

            System.Threading.Mutex mtx_g = stream_Graph_Mtx;
            System.Threading.Mutex mtx_i = stream_Index_Mtx;
            stream_Graph_Mtx = stream_Graph_R_Mtx;
            stream_Index_Mtx = stream_Index_R_Mtx;

            try
            {
                SolveDeikstra(end, starts, true);
            }
            catch (Exception ex) // try to map back
            {
                stream_Graph = tmp_g;
                stream_Index = tmp_i;

                stream_Graph_Mtx = mtx_g;
                stream_Index_Mtx = mtx_i;

                throw ex;
            };

            // map back
            stream_Graph = tmp_g;
            stream_Index = tmp_i;

            stream_Graph_Mtx = mtx_g;
            stream_Index_Mtx = mtx_i;
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

        // <summary>
        ///     Расчет пути из точки start во все или в каждую из ends
        /// </summary>
        /// <param name="start">Точка старта</param>
        /// <param name="end">Точка назначения</param>
        public void SolveDeikstra(uint start, uint end)
        {
            SolveDeikstra(start, new uint[] { end }, false);
        }

        /// <summary>
        ///     Расчет пути из точки start во все или в каждую из ends
        /// </summary>
        /// <param name="start">Точка старта</param>
        /// <param name="ends">Точки назначения</param>
        /// <param name="reversed">Использовать нахождение пути от обратного</param>
        public void SolveDeikstra(uint start, uint[] ends, bool reversed)
        {
            SetRouteDistance(start, const_maxError); // can be ERROR

            if (stream_Vector == null) throw new Exception("Call BeginSolve first");
            calc_Reversed = reversed;

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
                    Single curr_dist_xi = start == curr_node ? 0 : GetRouteDistance(curr_node, curr_node);
                    Single curr_time_xi = start == curr_node ? 0 : GetRouteTime(curr_node, curr_node);
                    for (uint next_node_i = 0; next_node_i < _n.Length; next_node_i++)
                    {
                        uint next_node = _n[next_node_i];
                        // skip logic here
                        if (SkipGoLogic(_l[next_node_i], Math.Min(curr_dist_xi, heuristValue))) continue;
                        if (wasList.Contains(next_node)) continue;

                        bool update = false;

                        Single[] ctctr = CallTraffic(_c[next_node_i], _t[next_node_i], _d[next_node_i], _l[next_node_i], _r[next_node_i] == 1 ? true : false, curr_time_xi);
                        Single cost_from_st = curr_cost_xi + ctctr[0]; // by speed max (in minutes)
                        Single dist_from_st = curr_dist_xi + _d[next_node_i];
                        Single time_from_st = curr_time_xi + ctctr[1]; // by speed normal (in minutes)

                        int index_in_togo = togoList.IndexOf(next_node);
                        if (index_in_togo < 0)
                        {
                            index_in_togo = togoList.Count;
                            togoList.Add(next_node);
                            togoHeurist.Add(0);
                            update = true;
                        };

                        switch (calc_minBy)
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
            Stream tmp_g = stream_Graph;
            Stream tmp_i = stream_Index;
            stream_Graph = stream_Graph_R;
            stream_Index = stream_Index_R;

            System.Threading.Mutex mtx_g = stream_Graph_Mtx;
            System.Threading.Mutex mtx_i = stream_Index_Mtx;
            stream_Graph_Mtx = stream_Graph_R_Mtx;
            stream_Index_Mtx = stream_Index_R_Mtx;

            try
            {
                SolveAstar(end, start, true);
            }
            catch (Exception ex) // try to map back
            {
                stream_Graph = tmp_g;
                stream_Index = tmp_i;

                stream_Graph_Mtx = mtx_g;
                stream_Index_Mtx = mtx_i;
                throw ex;
            };

            // map back
            stream_Graph = tmp_g;
            stream_Index = tmp_i;

            stream_Graph_Mtx = mtx_g;
            stream_Index_Mtx = mtx_i;
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
            SetRouteDistance(start, const_maxError); // can be ERROR

            if (stream_Vector == null) throw new Exception("Call BeginSolve first");
            calc_Reversed = reversed;

            PointF first_node_latlon = GetNodeLatLon(start);
            PointF last_node_latlon = GetNodeLatLon(end);

            uint[] _n; Single[] _c; Single[] _d; Single[] _t; uint[] _l; byte[] _r;

            float heuristValue = Utils.GetLengthMeters(first_node_latlon.Y, first_node_latlon.X, last_node_latlon.Y, last_node_latlon.X, false);
            if (calc_minBy == MinimizeBy.Time) heuristValue = heuristValue / 1000; // 60 kmph average speed            

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

                // added 24.02.2015 if all nodes in list are nearest
                if (index < 0)  // select first closest point to yy
                {
                    curr_heuVal = togoHeurist[0];
                    curr_node = togoList[0];
                    index = 0;
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
                        // skip logic here
                        if (SkipGoLogic(_l[next_node_i], Math.Min(curr_dist_xi, curr_heuVal))) continue;
                        if (wasList.Contains(next_node)) continue;

                        bool update = false;

                        Single[] ctctr = CallTraffic(_c[next_node_i], _t[next_node_i], _d[next_node_i], _l[next_node_i], _r[next_node_i] == 1 ? true : false, curr_time_xi);
                        Single cost_from_st = curr_cost_xi + ctctr[0]; // by speed max (in minutes)
                        Single dist_from_st = curr_dist_xi + _d[next_node_i];
                        Single time_from_st = curr_time_xi + ctctr[1]; // by speed normal (in minutes)

                        PointF next_node_latlon = GetNodeLatLon(next_node);
                        float next_node_dist_to_end = Utils.GetLengthMeters(next_node_latlon.Y, next_node_latlon.X, last_node_latlon.Y, last_node_latlon.X, false);

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
                            switch (calc_minBy)
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

                            if (calc_minBy == MinimizeBy.Time)
                            {
                                float fl = time_from_st + next_node_dist_to_end / 1000;
                                if (float.IsNaN(fl))
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
        ///     Logic to skip point (GoThrough, GoExcept)
        ///     Проверяем существует ли запрет на проезд по данной дороге
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private bool SkipGoLogic(uint line, float distFromStartOrToEndinMeters)
        {
            // distTofinishInMeters failed

            // skip logic here

            // GoExcept // если движение по этой линии под запретом в условиях
            if (goExcept.Contains(line))
                return true;

            // GoThrough
            if (((goThrough != null) && (goThrough.Length == 16)) || (AvoidLowRouteLevel))
            {
                TLineFlags flags = GetLineFlags(line);
                if (flags.HasAttributes) // если у линии есть атрибутивная информация
                {
                    byte[][] latta = GetLinesAttributes(new uint[] { line });
                    if ((goThrough != null) && (goThrough.Length == 16))
                    {
                        if (latta != null)
                        {
                            // bit mask
                            // смотрим какие биты обозначены в условиях и если они указаны,
                            // смотри обозначены ли они в атрибутивной информации
                            for (byte byt = 0; byt <= 4; byt++) // 4 last
                                for (byte bit = 0; bit < 8; bit++)
                                    if (LA.Bit(goThrough, byt, bit) && LA.Bit(latta[0], byt, bit))
                                        return true;
                            // less than
                            // смотрим какие значения указаны для сравнения в условиях
                            // смотрим какие значения есть в атрибутивной информации
                            // сравниваем
                            for (byte byt = 7; byt <= 12; byt++)
                                if ((goThrough[byt] > 0) && (latta[0][byt] > 0) && (goThrough[byt] > latta[0][byt]))
                                    return true;

                            if (goThrough[15] > 0)
                            {
                                byte RouteLevel = (byte)(latta[0][15] & 7);
                                byte throughLvl = (byte)(goThrough[15] & 7);
                                if ((throughLvl > 0) && (RouteLevel > 0) && (throughLvl >= RouteLevel))
                                    return true;
                            };
                        };
                    };
                    // AvoidLow
                    if ((avoidLowRouteLevel) && (distFromStartOrToEndinMeters >= 5000) && (latta != null) && ((latta[0][15] & 7) < 2))
                        return true;
                };
            };

            return false;
        }

        /// <summary>
        ///     Получаем координаты узла
        /// </summary>
        /// <param name="node">номер узла</param>
        /// <returns>Lat Lon</returns>
        public PointF GetNodeLatLon(uint node)
        {
            if (node == 0) throw new OverflowException("Node must be > 0");

            byte[] latlon = new byte[8];
            if (stream_Geo_Mtx != null) stream_Geo_Mtx.WaitOne();
            stream_Geo.Position = header_RMPOINTNLL0.Length + 4 + 8 * (node - 1);
            stream_Geo.Read(latlon, 0, 8);
            if (stream_Geo_Mtx != null) stream_Geo_Mtx.ReleaseMutex();
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
            uint y = calc_Reversed ? start : end;

            if (stream_Vector == null) throw new Exception("Call BeginSolve first");

            if (y == 0) return null; // NO WAY
            if (GetCost(y) == 0) return new uint[0]; // ALREADY

            uint intermediate = y;
            List<uint> arr = new List<uint>();
            while (((intermediate = GetPrev(intermediate)) > 0) && (intermediate != start))
                arr.Add(intermediate);
            uint[] a = arr.ToArray();
            if (this.calc_Reversed) return a;

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
        public uint[] GetRouteLines(uint start, uint[] way, uint end, out float[] linesDist, out float[] linesTime)
        {
            uint[] arr = new uint[way.Length + 1];
            linesDist = new float[way.Length + 1];
            linesTime = new float[way.Length + 1];

            uint[] _n; float[] _c; float[] _d; float[] _t; uint[] _l; byte[] _r;

            if (way.Length > 0)
            {
                SelectNode(start, out _n, out _c, out _d, out _t, out _l, out _r);
                for (int i = 0; i < _n.Length; i++)
                    if (_n[i] == way[0])
                    {
                        arr[0] = _l[i];
                        linesDist[0] = _d[i];
                        linesTime[0] = _t[i];
                    };

                for (int x = 1; x < way.Length; x++)
                {
                    SelectNode(way[x - 1], out _n, out _c, out _d, out _t, out _l, out _r);
                    for (int i = 0; i < _n.Length; i++)
                        if (_n[i] == way[x])
                        {
                            arr[x] = _l[i];
                            linesDist[x] = _d[i];
                            linesTime[x] = _t[i];
                        };
                };
                SelectNode(way[way.Length - 1], out _n, out _c, out _d, out _t, out _l, out _r);
                for (int i = 0; i < _n.Length; i++)
                    if (_n[i] == end)
                    {
                        arr[arr.Length - 1] = _l[i];
                        linesDist[arr.Length - 1] = _d[i];
                        linesTime[arr.Length - 1] = _t[i];
                    };
            }
            else
            {
                SelectNode(start, out _n, out _c, out _d, out _t, out _l, out _r);
                for (int i = 0; i < _n.Length; i++)
                    if (_n[i] == end)
                    {
                        arr[0] = _l[i];
                        linesDist[0] = _d[i];
                        linesTime[0] = _t[i];
                    };
            };

            return arr;
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
                    if (_n[i] == way[0])
                    {
                        arr[0] = _l[i];
                    };

                for (int x = 1; x < way.Length; x++)
                {
                    SelectNode(way[x - 1], out _n, out _c, out _d, out _t, out _l, out _r);
                    for (int i = 0; i < _n.Length; i++)
                        if (_n[i] == way[x])
                        {
                            arr[x] = _l[i];
                        };
                };
                SelectNode(way[way.Length - 1], out _n, out _c, out _d, out _t, out _l, out _r);
                for (int i = 0; i < _n.Length; i++)
                    if (_n[i] == end)
                    {
                        arr[arr.Length - 1] = _l[i];
                    };
            }
            else
            {
                SelectNode(start, out _n, out _c, out _d, out _t, out _l, out _r);
                for (int i = 0; i < _n.Length; i++)
                    if (_n[i] == end)
                    {
                        arr[0] = _l[i];
                    };

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
            ushort segments; int pos; byte flags; uint node1; uint node2;

            float prev_dist = GetRouteDistance(ww[0], ww[0]);
            if (prev_dist >= const_maxValue) prev_dist = 0;

            for (int x = 0; x < ww.Count - 1; x++)
            {
                SelectNode(ww[x], out _n, out _c, out _d, out _t, out _l, out _r);
                float curr_d = GetRouteDistance(ww[x + 1], ww[x + 1]);
                float delt_d = curr_d - prev_dist; // for out line
                for (int i = 0; i < _n.Length; i++)
                    if ((_n[i] == ww[x + 1]) && (Math.Abs(delt_d - _d[i]) < 1))
                    {
                        GetLine(_l[i], out segments, out pos, out flags, out node1, out node2);
                        PointF[] pts = GetLineSegments(_l[i], pos, segments, _r[i] == 1, false);
                        vec.Add(pts[0]);
                        for (int si = 1; si < pts.Length - 1; si++) vec.Add(pts[si]);
                        if (x == (ww.Count - 2))
                            vec.Add(pts[pts.Length - 1]);
                    };
                prev_dist = curr_d;
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
            ushort segments; int pos; byte fflags; uint node1; uint node2;

            float prev_dist = GetRouteDistance(ww[0], ww[0]);
            if (prev_dist >= const_maxValue) prev_dist = 0;

            for (int x = 0; x < ww.Count - 1; x++)
            {
                SelectNode(ww[x], out _n, out _c, out _d, out _t, out _l, out _r);
                float curr_d = GetRouteDistance(ww[x + 1], ww[x + 1]);
                float delt_d = curr_d - prev_dist; // for out line
                for (int i = 0; i < _n.Length; i++)
                    if ((_n[i] == ww[x + 1]) && (Math.Abs(delt_d - _d[i]) < 1))
                    {
                        float curr_s = (float)((_d[i] / _t[i]) * 60.0 / 1000.0);
                        GetLine(_l[i], out segments, out pos, out fflags, out node1, out node2);
                        PointF[] pts = GetLineSegments(_l[i], pos, segments, _r[i] == 1, false);
                        vec.Add(new PointFL(pts[0], ww[x], _l[i], curr_s));
                        for (int si = 1; si < pts.Length - 1; si++) vec.Add(new PointFL(pts[si], 0, _l[i], curr_s));
                        if (x == (ww.Count - 2))
                            vec.Add(new PointFL(pts[pts.Length - 1], end, _l[i], curr_s)); // was last 0 13.12.12
                    };
                prev_dist = curr_d;
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
        /// <param name="flags">флаги линии</param>
        /// <param name="nodeStart">точка начала линии</param>
        /// <param name="nodeEnd">точка конца линии</param>
        private void GetLine(uint line, out ushort segments, out int pos, out byte flags, out uint nodeStart, out uint nodeEnd)
        {
            byte[] ba = new byte[15];
            if (stream_Lines_Mtx != null) stream_Lines_Mtx.WaitOne();
            stream_Lines.Position = header_RMLINES.Length + 4 + const_LineRecordLength * (line - 1);
            stream_Lines.Read(ba, 0, 15);
            if (stream_Lines_Mtx != null) stream_Lines_Mtx.ReleaseMutex();
            segments = BitConverter.ToUInt16(ba, 0);
            pos = BitConverter.ToInt32(ba, 2);
            flags = ba[6];
            nodeStart = BitConverter.ToUInt32(ba, 7);
            nodeEnd = BitConverter.ToUInt32(ba, 11);
        }

        public bool GetLineLT(uint line, out float length, out float time)
        {
            length = float.MaxValue;
            time = float.MaxValue;

            ushort s; int p; byte f; uint st; uint fi;
            GetLine(line, out s, out p, out f, out st, out fi);
            uint[] _n; Single[] _c; Single[] _d; Single[] _t; uint[] _l; byte[] _r;
            SelectNode(st, out _n, out _c, out _d, out _t, out _l, out _r);
            for (uint i = 0; i < _n.Length; i++)
                if (_n[i] == fi)
                {
                    length = _d[i];
                    time = _t[i];
                    return true;
                };
            return false;
        }

        public bool GetLineLT(uint line, out float length, out float time, out ushort segments)
        {
            length = float.MaxValue;
            time = float.MaxValue;

            int p; byte f; uint st, fi;
            GetLine(line, out segments, out p, out f, out st, out fi);
            uint[] _n; Single[] _c; Single[] _d; Single[] _t; uint[] _l; byte[] _r;
            SelectNode(st, out _n, out _c, out _d, out _t, out _l, out _r);
            for (uint i = 0; i < _n.Length; i++)
                if (_n[i] == fi)
                {
                    length = _d[i];
                    time = _t[i];
                    return true;
                };
            return false;
        }

        /// <summary>
        ///     Получаем линию по номеру
        /// </summary>
        /// <param name="line">номер линии</param>
        /// <param name="segments">число сегментов линии</param>
        /// <param name="pos">позиция первого сегмента в файле сегментов</param>
        /// <param name="flags">флаги линии</param>
        /// <param name="nodeStart">точка начала линии</param>
        /// <param name="nodeEnd">точка конца линии</param>
        private void GetLine(uint line, out ushort segments, out int pos, out TLineFlags flags, out uint nodeStart, out uint nodeEnd)
        {
            byte bFlags;
            GetLine(line, out segments, out pos, out bFlags, out nodeStart, out nodeEnd);
            flags = new TLineFlags(bFlags, nodeStart, nodeEnd);
        }

        /// <summary>
        ///     Флаги линии
        /// </summary>
        /// <param name="line">номер линии</param>
        /// <returns></returns>
        public TLineFlags GetLineFlags(uint line)
        {
            byte[] ba = new byte[15];
            if (stream_Lines_Mtx != null) stream_Lines_Mtx.WaitOne();
            stream_Lines.Position = header_RMLINES.Length + 4 + const_LineRecordLength * (line - 1);
            stream_Lines.Read(ba, 0, 15);
            if (stream_Lines_Mtx != null) stream_Lines_Mtx.ReleaseMutex();
            return new TLineFlags(ba[6], BitConverter.ToUInt32(ba, 7), BitConverter.ToUInt32(ba, 11));
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

            if (stream_LinesSegments_Mtx != null) stream_LinesSegments_Mtx.WaitOne();
            stream_LinesSegments.Position = pos;
            byte[] ba = new byte[const_SegmRecordLength];
            for (int i = 0; i < segments; i++)
            {
                stream_LinesSegments.Read(ba, 0, ba.Length);
                uint line_no = BitConverter.ToUInt32(ba, 0);
                ushort segm = BitConverter.ToUInt16(ba, 4);
                float lat0 = BitConverter.ToSingle(ba, 6);
                float lon0 = BitConverter.ToSingle(ba, 10);
                float lat1 = BitConverter.ToSingle(ba, 14);
                float lon1 = BitConverter.ToSingle(ba, 18);
                //float k = BitConverter.ToSingle(ba, 22);
                //float b = BitConverter.ToSingle(ba, 26);

                if (i == 0) arr.Add(new PointF(lon0, lat0));
                arr.Add(new PointF(lon1, lat1));
            };
            if (stream_LinesSegments_Mtx != null) stream_LinesSegments_Mtx.ReleaseMutex();
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

            byte[] ba = new byte[15];
            if (stream_Lines_Mtx != null) stream_Lines_Mtx.WaitOne();
            stream_Lines.Position = header_RMLINES.Length + 4 + const_LineRecordLength * (line - 1);
            stream_Lines.Read(ba, 0, 15);
            if (stream_Lines_Mtx != null) stream_Lines_Mtx.ReleaseMutex();
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
            FileStream fs = new FileStream(stream_FileMain + ".lines.id", FileMode.Open, FileAccess.Read);
            byte[] ba = new byte[header_RMLINKIDS.Length];
            fs.Read(ba, 0, ba.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(ba) != "RMLINKIDS")
            {
                fs.Close();
                throw new IOException("Unknown file format:\r\n" + stream_FileMain + ".lines.id");
            };
            for (int i = 0; i < lines.Length; i++)
            {
                fs.Position = header_RMLINKIDS.Length + 4 + 4 * (lines[i] - 1);
                ba = new byte[4];
                fs.Read(ba, 0, ba.Length);
                res[i] = BitConverter.ToInt32(ba, 0);
            };
            fs.Close();
            return res;
        }

        public string AttributesToString(byte[] la)
        {
            if (la == null) return "";
            if (la.Length != 16) return "";
            string res = "";

            // avg.. - average speed
            // max.. - max speed
            // lvl - level
            // 1w - oneway
            if ((la[00] & 0x01) > 0) res += (res.Length > 0 ? " " : "") + "ca"; // civilarea
            if ((la[00] & 0x02) > 0) res += (res.Length > 0 ? " " : "") + "up"; // unpaved road
            if ((la[00] & 0x04) > 0) res += (res.Length > 0 ? " " : "") + "cc"; // concrete
            if ((la[00] & 0x08) > 0) res += (res.Length > 0 ? " " : "") + "st"; // stones
            // if ((la[00] & 0x10) > 0) res += (res.Length > 0 ? " " : "") + "as"; // sand
            // if ((la[00] & 0x20) > 0) res += (res.Length > 0 ? " " : "") + "tm"; // temporary
            if ((la[00] & 0x40) > 0) res += (res.Length > 0 ? " " : "") + "tn"; // tonnel
            if ((la[00] & 0x80) > 0) res += (res.Length > 0 ? " " : "") + "br"; // bridge
            if ((la[01] & 0x01) > 0) res += (res.Length > 0 ? " " : "") + "db"; // drawbridge
            if ((la[01] & 0x02) > 0) res += (res.Length > 0 ? " " : "") + "pt"; // pontoon
            if ((la[01] & 0x04) > 0) res += (res.Length > 0 ? " " : "") + "ft"; // ferry
            if ((la[01] & 0x08) > 0) res += (res.Length > 0 ? " " : "") + "rc"; // railcross
            if ((la[01] & 0x10) > 0) res += (res.Length > 0 ? " " : "") + "wd"; // wade (brod)
            // ??
            // ??
            // ??
            if ((la[02] & 0x01) > 0) res += (res.Length > 0 ? " " : "") + "r1"; // one line reverse
            if ((la[02] & 0x02) > 0) res += (res.Length > 0 ? " " : "") + "aa"; // car only
            if ((la[02] & 0x04) > 0) res += (res.Length > 0 ? " " : "") + "hw"; // highway
            if ((la[02] & 0x08) > 0) res += (res.Length > 0 ? " " : "") + "tr"; // toll road
            if ((la[02] & 0x10) > 0) res += (res.Length > 0 ? " " : "") + "nt"; // notrucks
            if ((la[02] & 0x20) > 0) res += (res.Length > 0 ? " " : "") + "nm"; // no motorcycles
            if ((la[02] & 0x40) > 0) res += (res.Length > 0 ? " " : "") + "na"; // no agro
            if ((la[02] & 0x80) > 0) res += (res.Length > 0 ? " " : "") + "nw"; // no tows
            if ((la[03] & 0x01) > 0) res += (res.Length > 0 ? " " : "") + "ct"; // customs         
            // skip
            // skip
            if ((la[03] & 0x08) > 0) res += (res.Length > 0 ? " " : "") + "rw"; // roadworks (repair)
            // skip
            // skip
            if ((la[03] & 0x40) > 0) res += (res.Length > 0 ? " " : "") + "ns"; // no stop
            if ((la[03] & 0x80) > 0) res += (res.Length > 0 ? " " : "") + "np"; // no parking
            if ((la[04] & 0x01) > 0) res += (res.Length > 0 ? " " : "") + "nd"; // no dangerous
            if ((la[04] & 0x02) > 0) res += (res.Length > 0 ? " " : "") + "ne"; // no explosives
            if ((la[04] & 0x04) > 0) res += (res.Length > 0 ? " " : "") + "tl"; // traffic light
            // skip

            // speed limit
            byte SL = (byte)(la[15] & 0xF8);
            if (SL > 0) res += (res.Length > 0 ? " max" : "max") + ((SL >> 3) * 5).ToString();

            // route level
            byte RL = (byte)(la[15] & 0x07);
            if (RL > 0) res += (res.Length > 0 ? " lvl" : "lvl") + RL.ToString();

            return res;
        }

        /// <summary>
        ///     Получаем LINE_ATTRIBUTES линий из файла .lines.att
        /// </summary>
        /// <param name="lines">номера линий</param>
        /// <returns></returns>
        public byte[][] GetLinesAttributes(uint[] lines)
        {
            //if (!File.Exists(stream_FileMain + ".lines.att")) return null;
            if (stream_LAttr == null) return null;
            if (stream_LAttr_Mtx != null) stream_LAttr_Mtx.WaitOne();

            List<byte[]> res = new List<byte[]>();
            //FileStream fs = new FileStream(stream_FileMain + ".lines.att", FileMode.Open, FileAccess.Read);
            Stream fs = stream_LAttr;
            //byte[] ba = new byte[header_RMLATTRIB.Length];
            byte[] ba;
            //fs.Read(ba, 0, ba.Length);
            //if (System.Text.Encoding.GetEncoding(1251).GetString(ba) != "RMLATTRIB")
            //{
            //    fs.Close();
            //    throw new IOException("Unknown file format:\r\n" + stream_FileMain + ".lines.att");
            //};
            for (int i = 0; i < lines.Length; i++)
            {
                fs.Position = header_RMLATTRIB.Length + 4 + 16 * (lines[i] - 1);
                ba = new byte[16];
                fs.Read(ba, 0, ba.Length);
                res.Add(ba);
            };
            //fs.Close();

            if (stream_LAttr_Mtx != null) stream_LAttr_Mtx.ReleaseMutex();
            return res.ToArray();
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

            // nodeStart == nodeEnd ? 0 :  ... - added 05.09.2013
            res.cost = nodeStart == nodeEnd ? 0 : GetRouteCost(nodeStart, nodeEnd);
            res.length = nodeStart == nodeEnd ? 0 : GetRouteDistance(nodeStart, nodeEnd);
            res.time = nodeStart == nodeEnd ? 0 : GetRouteTime(nodeStart, nodeEnd);
            res.nodes = GetRouteNodes(nodeStart, nodeEnd);
            res.shrinkStart = false;
            res.shrinkEnd = false;

            //ПРОВЕРЯЕМ на предмет, если уже проехались по линии
            if (res.nodes.Length > 0)
            {
                if (res.nodes[res.nodes.Length - 1] == nodeEndReversed)
                {
                    if (true) // если oneway, то маршрут не пройдет через эту линию в обратном направлении
                    {
                        res.shrinkEnd = true;
                        res.nodeEnd = nodeEndReversed;
                        if (calc_Reversed)
                        {
                            res.shrinkEndCost = GetRouteCost(res.nodeEnd, res.nodeEnd);
                            res.cost -= GetRouteCost(res.nodeEnd, res.nodeEnd);
                            res.shrinkEndLength = GetRouteDistance(res.nodeEnd, res.nodeEnd);
                            res.length -= GetRouteDistance(res.nodeEnd, res.nodeEnd);
                            res.shrinkEndTime = GetRouteTime(res.nodeEnd, res.nodeEnd);
                            res.time -= GetRouteTime(res.nodeEnd, res.nodeEnd);
                        }
                        else
                        {
                            res.shrinkEndCost = res.cost - GetRouteCost(res.nodeEnd, res.nodeEnd);
                            res.cost = GetRouteCost(res.nodeEnd, res.nodeEnd);
                            res.shrinkEndLength = res.length - GetRouteDistance(res.nodeEnd, res.nodeEnd);
                            res.length = GetRouteDistance(res.nodeEnd, res.nodeEnd);
                            res.shrinkEndTime = res.time - GetRouteTime(res.nodeEnd, res.nodeEnd);
                            res.time = GetRouteTime(res.nodeEnd, res.nodeEnd);
                        };
                    };
                };
                if (res.nodes[0] == nodeStartReversed)
                {
                    //TLineFlags lf = new TLineFlags(0,0,0); // односторонняя?
                    //uint[] _n; float[] _c; float[] _d; float[] _t; uint[] _l; byte[] _r;
                    //SelectNode(nodeStartReversed, out _n, out _c, out _d, out _t, out _l, out _r);
                    //for (int i = 0; i < _n.Length; i++)
                    //    if (_n[i] == (res.nodes.Length > 1 ? res.nodes[1] : nodeEnd))
                    //    {
                    //        ushort seg; int pos; uint n1; uint n2;
                    //        GetLine(_l[i], out seg, out pos, out lf, out n1, out n2);
                    //    };
                    //if (!lf.IsOneWay) // если oneway, то маршрут не пройдет через эту линию в обратном направлении
                    if (true) // если oneway, то маршрут не пройдет через эту линию в обратном направлении
                    {
                        res.shrinkStart = true;
                        res.nodeStart = nodeStartReversed;
                        if (calc_Reversed)
                        {
                            res.shrinkStartCost = res.cost - GetRouteCost(res.nodeStart, res.nodeStart);
                            res.cost = GetRouteCost(res.nodeStart, res.nodeStart);
                            res.shrinkStartLength = res.length - GetRouteDistance(res.nodeStart, res.nodeStart);
                            res.length = GetRouteDistance(res.nodeStart, res.nodeStart);
                            res.shrinkStartTime = res.time - GetRouteTime(res.nodeStart, res.nodeStart);
                            res.time = GetRouteTime(res.nodeStart, res.nodeStart);
                        }
                        else
                        {
                            res.shrinkStartCost = GetRouteCost(res.nodeStart, res.nodeStart);
                            res.cost -= GetRouteCost(res.nodeStart, res.nodeStart);
                            res.shrinkStartLength = GetRouteDistance(res.nodeStart, res.nodeStart);
                            res.length -= GetRouteDistance(res.nodeStart, res.nodeStart);
                            res.shrinkStartTime = GetRouteTime(res.nodeStart, res.nodeStart);
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

        // Added 21.12.2021 where 1 line in route for WATER (allowLeftTurns/overtaking=yes)
        /// <summary>
        ///     Расчет маршрута, если точка старта и финиша на 1 линии
        /// </summary>
        /// <param name="nodeStart">start node</param>
        /// <param name="nodeEnd">end node</param>
        /// <returns>Отчет</returns>
        public RouteResult GetFullRouteOneLine(FindStartStopResult nodeStart, FindStartStopResult nodeEnd)
        {
            RouteResult res = new RouteResult();
            res.nodeStart = nodeStart.nodeN;
            res.nodeEnd = nodeEnd.nodeN;
            res.nodes = new uint[0];
            res.lines = new uint[] { nodeStart.line };
            res.vector = new PointFL[0];

            res.cost = 0;
            res.length = 0;
            res.time = 0;
            
            res.shrinkStart = false;
            res.shrinkEnd = false;

            ushort s; int p; byte f; uint st; uint fi;
            GetLine(nodeStart.line, out s, out p, out f, out st, out fi);
            PointF[] line = GetLineSegments(nodeStart.line, false, false);

            int ins = (nodeStart.nodeN == fi ? nodeStart.revers.Length : nodeStart.normal.Length) - 2;
            int ine = (nodeEnd.nodeN == fi ? nodeEnd.revers.Length : nodeEnd.normal.Length) - 2;
            int ind = ine - ins;

            nodeStart.normal = nodeStart.revers = new PointF[] { nodeStart.normal[0], nodeStart.normal[1] };
            nodeEnd.normal = nodeEnd.revers = new PointF[] { nodeEnd.normal[nodeEnd.normal.Length - 2], nodeEnd.normal[nodeEnd.normal.Length - 1] };

            res.cost = 0;
            res.length = 0;
            res.time = 0;

            if (ind == 0)
            {
                res.length += Utils.GetLengthMeters(nodeStart.normal[1].Y, nodeStart.normal[1].X, nodeEnd.normal[0].Y, nodeEnd.normal[0].X, false);
            };
            if (ind > 0)
            {
                res.vector = new PointFL[ind];
                for (int i = 0; i < res.vector.Length; i++)
                {
                    res.vector[i] = new PointFL(line[i + ins], 0, nodeStart.line);
                    if(i > 0) res.length += Utils.GetLengthMeters(res.vector[i-1].Y, res.vector[i-1].X, res.vector[i].Y, res.vector[i].X, false);
                };
                res.length += Utils.GetLengthMeters(nodeStart.normal[1].Y, nodeStart.normal[1].X, res.vector[0].Y, res.vector[0].X, false);
                res.length += Utils.GetLengthMeters(res.vector[res.vector.Length - 1].Y, res.vector[res.vector.Length - 1].X, nodeEnd.normal[0].Y, nodeEnd.normal[0].X, false);
            }
            else if (ind < 0)
            {
                res.vector = new PointFL[-1 * ind];
                for (int i = 0; i < res.vector.Length; i++)
                {
                    res.vector[i] = new PointFL(line[i + ine], 0, nodeStart.line);
                    if(i > 0) res.length += Utils.GetLengthMeters(res.vector[i-1].Y, res.vector[i-1].X, res.vector[i].Y, res.vector[i].X, false);
                };
                Array.Reverse(res.vector);
                res.length += Utils.GetLengthMeters(nodeStart.normal[1].Y, nodeStart.normal[1].X, res.vector[0].Y, res.vector[0].X, false);
                res.length += Utils.GetLengthMeters(res.vector[res.vector.Length - 1].Y, res.vector[res.vector.Length - 1].X, nodeEnd.normal[0].Y, nodeEnd.normal[0].X, false);
            };

            res.length += nodeStart.distToLine + nodeEnd.distToLine;
            res.cost = res.length;
            res.time = (res.length / (float)1000.0) / (float)20.0 * (float)60.0;
            
            return res;
        }

        // Added 21.12.2021
        /// <summary>
        ///     Меняем местами информацию Normal и Reverse
        /// </summary>
        /// <param name="ssr"></param>
        private void ReplaceNormalAndReverse(ref FindStartStopResult ssr)
        {
            PointF[] tmp = ssr.revers;
            ssr.revers = ssr.normal;
            ssr.normal = tmp;

            uint tmu = ssr.nodeR;
            ssr.nodeR = ssr.nodeN;
            ssr.nodeN = tmu;

            float tmd = ssr.distToR;
            ssr.distToR = ssr.distToN;
            ssr.distToN = tmd;
        }

        // Modified 21.12.2021 add (allowLeftTurns/overtaking=yes)
        /// <summary>
        ///     Получаем маршрут между двумя узлами графа 
        /// </summary>
        /// <param name="nodeStart">start node</param>
        /// <param name="nodeEnd">end node</param>
        /// <param name="shrinkStartStopIfNeed">убирать ненужные развороты по краям</param>
        /// <param name="allowLeftTurn">опускать выезд на дорогу и съезд через встречную полосу</param>
        /// <returns>Отчет</returns>
        public RouteResult GetRouteFull(FindStartStopResult nodeStart, FindStartStopResult nodeEnd, bool shrinkStartStopIfNeed, bool allowLeftTurn)
        {
            RouteResult res = GetRoute(nodeStart.nodeN, nodeEnd.nodeN, shrinkStartStopIfNeed ? nodeStart.nodeR : 0, shrinkStartStopIfNeed ? nodeEnd.nodeR : 0);
            if (shrinkStartStopIfNeed)
            {
                if (res.shrinkStart) ReverseStop(nodeStart);
                if (res.shrinkEnd) ReverseStop(nodeEnd);

                // Added 21.12.2021 for one-line routing (WATER)
                if (allowLeftTurn)
                {
                    // возвращаемся в другую сторону по начальной линии
                    bool tfs = (res.vector.Length > 1) && (res.vector[0].line == nodeStart.line) && (res.vector[0].line == res.vector[1].line) && (nodeStart.nodeN == res.vector[0].node);
                    // возвращаемся в другую сторону по конечной линии
                    bool tfe = (res.vector.Length > 1) && (res.vector[res.vector.Length - 1].line == nodeEnd.line) && (res.vector[res.vector.Length - 1].line == res.vector[res.vector.Length - 2].line) && (nodeEnd.nodeN == res.vector[res.vector.Length - 1].node);

                    if (tfs)
                    {
                        float ll, lt; ushort ls;
                        GetLineLT(nodeStart.line, out ll, out lt, out ls);
                        ReplaceNormalAndReverse(ref nodeStart);                        
                        // minimize length & time
                        res.length -= ll;
                        res.time -= lt;
                        // remove fisrt line //
                        int shrinkes = res.vector.Length - (++ls);
                        PointFL[] shrinked = new PointFL[shrinkes];
                        if (shrinkes > 0) Array.Copy(res.vector, ls, shrinked, 0, shrinked.Length);
                        res.vector = shrinked;
                    };
                    
                    if (tfe)
                    {
                        float ll, lt; ushort ls;
                        GetLineLT(nodeEnd.line, out ll, out lt, out ls);
                        ReplaceNormalAndReverse(ref nodeEnd);
                        // minimize length & time
                        res.length -= ll;
                        res.time -= lt;
                        // remove last line //
                        int shrinkes = res.vector.Length - (++ls);
                        PointFL[] shrinked = new PointFL[shrinkes];
                        if (shrinkes > 0) Array.Copy(res.vector, 0, shrinked, 0, shrinked.Length);
                        res.vector = shrinked;
                    }; 
                };
                // end added
            };

            // CODE PSDA_A_#3_Xi
            res.length += nodeStart.distToN + nodeEnd.distToN;            
            res.time += (nodeStart.distToN + nodeEnd.distToN) / 700;            
            // CODE PSDA_A_#3_Xi

            return res;
        }

        /// <summary>
        ///     Получаем маршрут между двумя узлами графа
        /// </summary>
        /// <param name="nodeStart">start node</param>
        /// <param name="nodeEnd">end node</param>
        /// <param name="shrinkStartStopIfNeed">убирать ненужные развороты по краям</param>
        /// <param name="allowLeftTurn">опускать выезд на дорогу и съезд через встречную полосу</param>
        /// <returns>Отчет</returns>
        public RouteResult GetRouteFull(FindStartStopResult nodeStart, uint nodeEnd, bool shrinkStartStopIfNeed, bool allowLeftTurn)
        {
            RouteResult res = GetRoute(nodeStart.nodeN, nodeEnd, shrinkStartStopIfNeed ? nodeStart.nodeR : 0, 0);
            if (shrinkStartStopIfNeed)
            {
                if (res.shrinkStart) ReverseStop(nodeStart);
                // Added 20.12.2021 for one-line routing (WATER)
                if (allowLeftTurn || (res.lines.Length < 2))
                {
                    if ((res.vector.Length > 1) && (res.vector[0].line == res.vector[1].line) && (nodeStart.nodeN == res.vector[0].node)) // U-TURN
                    {
                        PointF[] tmp = nodeStart.revers;
                        nodeStart.revers = nodeStart.normal;
                        nodeStart.normal = tmp;
                        uint tmu = nodeStart.nodeR;
                        nodeStart.nodeR = nodeStart.nodeN;
                        nodeStart.nodeN = tmu;

                        List<PointFL> tmv = new List<PointFL>();
                        for (int i = 0; i < res.vector.Length; i++)
                            if (res.vector[i].line == nodeStart.line)
                                continue;
                            else
                                tmv.Add(res.vector[i]);
                        res.vector = tmv.ToArray();
                    };
                };
            };

            // CODE PSDA_A_#3_Xi
            res.length += nodeStart.distToN;
            res.time += (nodeStart.distToN) / 700;
            // CODE PSDA_A_#3_Xi

            return res;
        }

        /// <summary>
        ///     Получаем маршрут между двумя узлами графа
        /// </summary>
        /// <param name="nodeStart">start node</param>
        /// <param name="nodeEnd">end node</param>
        /// <param name="shrinkStartStopIfNeed">убирать ненужные развороты по краям</param>
        /// <param name="allowLeftTurn">опускать выезд на дорогу и съезд через встречную полосу</param>
        /// <returns>Отчет</returns>
        public RouteResult GetRouteFull(uint nodeStart, FindStartStopResult nodeEnd, bool shrinkStartStopIfNeed, bool allowLeftTurn)
        {
            RouteResult res = GetRoute(nodeStart, nodeEnd.nodeN, 0, shrinkStartStopIfNeed ? nodeEnd.nodeR : 0);
            if (shrinkStartStopIfNeed)
            {
                if (res.shrinkEnd) ReverseStop(nodeEnd);
                // Added 20.12.2021 for one-line routing (WATER)
                if (allowLeftTurn || (res.lines.Length < 2))
                {
                    if ((res.vector.Length > 1) && (res.vector[res.vector.Length - 1].line == res.vector[res.vector.Length - 2].line) && (nodeEnd.nodeN == res.vector[res.vector.Length - 1].node)) // U-TURN
                    {
                        PointF[] tmp = nodeEnd.revers;
                        nodeEnd.revers = nodeEnd.normal;
                        nodeEnd.normal = tmp;
                        uint tmu = nodeEnd.nodeR;
                        nodeEnd.nodeR = nodeEnd.nodeN;
                        nodeEnd.nodeN = tmu;

                        uint tmpu = res.vector[0].node;

                        List<PointFL> tmv = new List<PointFL>();
                        for (int i = 0; i < res.vector.Length; i++)
                            if (res.vector[i].line == nodeEnd.line)
                                continue;
                            else
                                tmv.Add(res.vector[i]);
                        res.vector = tmv.ToArray();
                    };
                };
            };

            // CODE PSDA_A_#3_Xi
            res.length += nodeEnd.distToN;
            res.time += (nodeEnd.distToN) / 700;
            // CODE PSDA_A_#3_Xi

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
            byte flags;
            int pos;
            uint ns1; uint ns2; uint ne1; uint ne2;
            GetLine(lineStart, out seg, out pos, out flags, out ns1, out ns2);
            GetLine(lineStart, out seg, out pos, out flags, out ne1, out ne2);
            return GetRoute(ns2, ne1, ns1, ne2);
        }

        /// <summary>
        ///     Получаем маршрут между двумя координатами
        /// </summary>
        /// <param name="start">начальный узел</param>
        /// <param name="end">конечный узел</param>
        /// <returns>Отчет</returns>
        public RouteResult SolveRoute(PointF start, PointF end, bool allowLeftTurn)
        {
            FindStartStopResult nodeStart = FindNodeStart(start.Y, start.X, 2000);
            FindStartStopResult nodeEnd = FindNodeStart(end.Y, end.X, 2000);
            BeginSolve(true, null);
            SolveAstar(nodeStart.nodeN, nodeEnd.nodeN);
            RouteResult res = GetRouteFull(nodeStart, nodeEnd, true, allowLeftTurn);
            EndSolve();

            List<PointFL> vec = new List<PointFL>();
            //vec.AddRange(nodeStart.normal);            
            for (int si = 0; si < nodeStart.normal.Length; si++) vec.Add(new PointFL(nodeStart.normal[si], 0, 0, (si > 0) && ((res.vector != null) && (res.vector.Length != 0)) ? res.vector[0].speed : 20));
            vec.AddRange(res.vector);
            //vec.AddRange(nodeEnd.normal);
            for (int si = 0; si < nodeEnd.normal.Length; si++) vec.Add(new PointFL(nodeEnd.normal[si], 0, 0, (si < (nodeEnd.normal.Length - 1)) && ((res.vector != null) && (res.vector.Length != 0)) ? res.vector[res.vector.Length - 1].speed : 20));
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
            uint y = calc_Reversed ? start : end;

            if (stream_Vector == null) throw new Exception("Call BeginSolve first");
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
            stream_Vector.Position = (y - 1) * (const_vtArrElemLength) + const_offset_Dist;
            byte[] bb = BitConverter.GetBytes(dist);
            stream_Vector.Write(bb, 0, 4);
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
            uint y = calc_Reversed ? start : end;

            if (stream_Vector == null) throw new Exception("Call BeginSolve first");

            stream_Vector.Position = (y - 1) * (const_vtArrElemLength) + const_offset_Dist;
            byte[] bb = new byte[4];
            stream_Vector.Read(bb, 0, 4);
            Single s = BitConverter.ToSingle(bb, 0);
            if (s < const_maxError)
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
            stream_Vector.Position = (y - 1) * (const_vtArrElemLength) + const_offset_Time;
            byte[] bb = BitConverter.GetBytes(time);
            stream_Vector.Write(bb, 0, 4);
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
            uint y = calc_Reversed ? start : end;

            if (stream_Vector == null) throw new Exception("Call BeginSolve first");

            stream_Vector.Position = (y - 1) * (const_vtArrElemLength) + const_offset_Time;
            byte[] bb = new byte[4];
            stream_Vector.Read(bb, 0, 4);
            Single s = BitConverter.ToSingle(bb, 0);
            if (s < const_maxError)
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
            stream_Vector.Position = 0;
            while ((read = stream_Vector.Read(bb, 0, bb.Length)) > 0)
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
            CalculateRGNodesRoutes(region_id, Path.GetDirectoryName(stream_FileName) + @"\RGWays\");
        }

        /// <summary>
        ///     Предварительный просчет путей между точками стыковки межрайонных маршрутов
        /// </summary>
        /// <param name="region_id">Номер региона</param>
        /// <param name="dir">каталог с файлами rgway.xml</param>
        public void CalculateRGNodesRoutes(int region_id, string dir)
        {
            if (!File.Exists(stream_FileMain + ".rgnodes.xml"))
            {
                Console.WriteLine("File " + Path.GetFileName(stream_FileMain) + ".rgnodes.xml doen't exists!");
                return;
            };

            Console.WriteLine("Loading RGNodes: " + Path.GetFileName(stream_FileMain) + ".rgnodes.xml");
            TRGNode[] nodes = XMLSaved<TRGNode[]>.Load(stream_FileMain + ".rgnodes.xml");

            if (nodes.Length == 0)
            {
                Console.WriteLine("RGNodes not found!");
                return;
            };

            if ((nodes == null) || (nodes.Length == 0)) return;

            int ttl_rts = nodes.Length * nodes.Length;
            int cur_rts = 0;

            if (!Path.IsPathRooted(dir))
                dir = XMLSaved<int>.GetCurrentDir() + dir;
            Directory.CreateDirectory(dir);

            Console.WriteLine("Предварительный просчет путей между точками стыковки межрайонных маршрутов");
            Console.WriteLine("Оптимизировать маршрут по: " + this.calc_minBy.ToString());
            string err_list = "";
            for (int x = 0; x < nodes.Length; x++)
            {
                if (region_id > 0) nodes[x].region = region_id;

                if (!nodes[x].inner) { cur_rts += nodes.Length; continue; };

                List<int> li = new List<int>();
                List<float> lc = new List<float>();
                List<float> ld = new List<float>();
                List<float> lt = new List<float>();

                for (int y = 0; y < nodes.Length; y++)
                {
                    cur_rts++;
                    if (x == y) continue;
                    if (!nodes[y].outer) continue;

                    DateTime st = DateTime.Now;

                    FindStartStopResult nodeStart = new FindStartStopResult();
                    nodeStart.nodeN = nodes[x].node;
                    FindStartStopResult nodeEnd = new FindStartStopResult();
                    nodeEnd.nodeN = nodes[y].node;

                    DateTime s = DateTime.Now;
                    Console.Write(cur_rts.ToString() + "/" + ttl_rts.ToString() + ": ");
                    Console.Write(nodes[x].node.ToString() + "(" + nodes[x].id + ")->" + nodes[y].node.ToString() + "(" + nodes[y].id + ")");

                    BeginSolve(true, null);
                    // MinimizeRouteBy = MinimizeBy.Cost // byCost (default)
                    // MinimizeRouteBy = MinimizeBy.Time;
                    System.Threading.Thread thr = new System.Threading.Thread(new System.Threading.ThreadStart(CalcTimed));
                    ctStart = nodeStart.nodeN;
                    ctEnd = nodeEnd.nodeN;
                    thr.Start();
                    thr.Join(9 * 60 * 1000); // 9 minutes max calc route timeout
                    thr.Abort();
                    thr = null;
                    // SolveAstar(nodeStart.nodeN, nodeEnd.nodeN); // A*
                    DateTime e = DateTime.Now;
                    TimeSpan ts = e.Subtract(s);

                    float c = GetRouteCost(nodeStart.nodeN, nodeEnd.nodeN);
                    float d = GetRouteDistance(nodeStart.nodeN, nodeEnd.nodeN);
                    float t = GetRouteTime(nodeStart.nodeN, nodeEnd.nodeN);
                    RouteResultStored rrs = new RouteResultStored();
                    rrs.Region = region_id;
                    rrs.route = GetRouteFull(nodeStart, nodeEnd, true, false);
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

                    string fN = dir + @"\" + String.Format("{0}T{1}B{2}.rgway.xml", nodes[x].id, nodes[y].id, rrs.Region);
                    if (d < const_maxValue)
                        XMLSaved<RouteResultStored>.Save(fN, rrs);
                    else
                        err_list += String.Format("{0} - {1}\r\n", nodes[x].id, nodes[y].id);

                    Console.Write(" - ");

                    TimeSpan ctd = DateTime.Now.Subtract(st);

                    if (d < const_maxValue)
                    {
                        Console.Write(String.Format("{0:0} km {1:0} min", d / 1000, t, rrs.route.nodes.Length));
                        Console.Write(String.Format(" - {0}:{1:00}:{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds));
                        Console.Write(" - ");
                        Console.WriteLine(String.Format("{0}T{1}B{2}.rgway.xml", nodes[x].id, nodes[y].id, rrs.Region));
                    }
                    else
                    {
                        Console.WriteLine(String.Format("!!!NO WAY!!! - {0}:{1:00}:{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds));
                    };
                    // Console.WriteLine(" ... Calc time: " + ctd.Hours.ToString("0,0") + ":" + ctd.Minutes.ToString("0,0") + ":" + ctd.Seconds.ToString("0,0")+"."+ctd.Milliseconds.ToString("0,00"));

                    li.Add(nodes[y].id); lc.Add(c); ld.Add(d); lt.Add(t);
                };

                nodes[x].links = li.ToArray();
                nodes[x].costs = lc.ToArray();
                nodes[x].dists = ld.ToArray();
                nodes[x].times = lt.ToArray();
            };

            // XML
            Console.WriteLine("Saving RGNodes: " + Path.GetFileName(stream_FileMain) + ".rgnodes.xml");
            XMLSaved<TRGNode[]>.Save(stream_FileMain + ".rgnodes.xml", nodes);

            // TXT
            System.IO.FileStream fs = new FileStream(stream_FileMain + ".rgnodes.txt", FileMode.Create);
            System.IO.StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.GetEncoding(1251));
            sw.WriteLine("RGNodes in region: ");
            for (int i = 0; i < nodes.Length; i++)
            {
                sw.Write(i > 0 ? "," : "");
                sw.Write(nodes[i].id.ToString());
            };
            if (err_list.Length > 0)
            {
                sw.WriteLine(); sw.WriteLine();
                sw.WriteLine("NO WAY:");
                sw.Write(err_list);
            };
            sw.Flush();
            sw.Close();

            // GPX
            fs = new FileStream(stream_FileMain + ".rgnodes.gpx", FileMode.Create);
            sw = new StreamWriter(fs, System.Text.Encoding.GetEncoding(1251));

            sw.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\" ?>");
            sw.WriteLine("<gpx xmlns=\"http://www.topografix.com/GPX/1/1/\" version=\"1.1\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd\">");

            for (uint i = 0; i < nodes.Length; i++)
                sw.WriteLine("<wpt lat=\"" + nodes[i].lat.ToString().Replace(",", ".") + "\" lon=\"" + nodes[i].lon.ToString().Replace(",", ".") + "\"><name>" + nodes[i].id.ToString() + "</name></wpt>");
            sw.WriteLine("</gpx>");

            sw.Flush();
            sw.Close();

            Console.WriteLine("Done");
        }

        public void CalculateRGNodesRoutesWithRouteLevel(int region_id, string dir)
        {
            if (!File.Exists(stream_FileMain + ".rgnodes.xml"))
            {
                Console.WriteLine("File " + Path.GetFileName(stream_FileMain) + ".rgnodes.xml doen't exists!");
                return;
            };

            Console.WriteLine("Loading RGNodes: " + Path.GetFileName(stream_FileMain) + ".rgnodes.xml");
            TRGNode[] nodes = XMLSaved<TRGNode[]>.Load(stream_FileMain + ".rgnodes.xml");

            if (nodes.Length == 0)
            {
                Console.WriteLine("RGNodes not found!");
                return;
            };

            if ((nodes == null) || (nodes.Length == 0)) return;

            int ttl_rts = nodes.Length * nodes.Length;
            int cur_rts = 0;

            if (!Path.IsPathRooted(dir))
                dir = XMLSaved<int>.GetCurrentDir() + dir;
            Directory.CreateDirectory(dir);

            Console.WriteLine("Предварительный просчет путей между точками стыковки межрайонных маршрутов");
            Console.WriteLine("Оптимизировать маршрут по: " + this.calc_minBy.ToString());
            string err_list = "";
            for (int x = 0; x < nodes.Length; x++)
            {
                if (region_id > 0) nodes[x].region = region_id;

                if (!nodes[x].inner) { cur_rts += nodes.Length; continue; };

                List<int> li = new List<int>();
                List<float> lc = new List<float>();
                List<float> ld = new List<float>();
                List<float> lt = new List<float>();

                for (int y = 0; y < nodes.Length; y++)
                {
                    cur_rts++;
                    if (x == y) continue;
                    if (!nodes[y].outer) continue;

                    DateTime st = DateTime.Now;

                    FindStartStopResult nodeStart = new FindStartStopResult();
                    nodeStart.nodeN = nodes[x].node;
                    FindStartStopResult nodeEnd = new FindStartStopResult();
                    nodeEnd.nodeN = nodes[y].node;

                    DateTime s = DateTime.Now;
                    Console.Write(cur_rts.ToString() + "/" + ttl_rts.ToString() + ": ");
                    Console.Write(nodes[x].node.ToString() + "(" + nodes[x].id + ")->" + nodes[y].node.ToString() + "(" + nodes[y].id + ")");

                    string sok = "-";
                    AvoidLowRouteLevel = false;
                    GoThrough = new byte[16];
                    GoThrough[0] = 3; // SKIP грунтовки, дворовые проезды
                    GoThrough[15] = 2; // SKIP ROUTE_LVL 1

                    byte tryC = 2; // 1st try skip, 2nd - all
                    while (tryC > 0)
                    {
                        tryC--;
                        BeginSolve(true, null);
                        // MinimizeRouteBy = MinimizeBy.Cost // byCost (default)
                        // MinimizeRouteBy = MinimizeBy.Time;                    
                        System.Threading.Thread thr = new System.Threading.Thread(new System.Threading.ThreadStart(CalcTimed));
                        ctStart = nodeStart.nodeN;
                        ctEnd = nodeEnd.nodeN;
                        thr.Start();
                        if (tryC == 1)
                            thr.Join(2 * 60 * 1000); // 2 minutes max calc route timeout
                        else
                            thr.Join(9 * 60 * 1000); // 9 minutes max calc route timeout
                        thr.Abort();
                        thr = null;
                        // SolveAstar(nodeStart.nodeN, nodeEnd.nodeN); // A*
                        DateTime e = DateTime.Now;
                        TimeSpan ts = e.Subtract(s);

                        float c = GetRouteCost(nodeStart.nodeN, nodeEnd.nodeN);
                        float d = GetRouteDistance(nodeStart.nodeN, nodeEnd.nodeN);
                        float t = GetRouteTime(nodeStart.nodeN, nodeEnd.nodeN);
                        RouteResultStored rrs = new RouteResultStored();
                        rrs.Region = region_id;
                        rrs.route = GetRouteFull(nodeStart, nodeEnd, true, false);
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

                        string fN = dir + @"\" + String.Format("{0}T{1}B{2}.rgway.xml", nodes[x].id, nodes[y].id, rrs.Region);
                        if (d < const_maxValue)
                            XMLSaved<RouteResultStored>.Save(fN, rrs);
                        else
                            err_list += String.Format("{0} - {1}\r\n", nodes[x].id, nodes[y].id);

                        EndSolve();

                        if ((d >= const_maxValue) && (tryC > 0))
                        {
                            GoThrough = null;
                            sok = "~";
                            continue;
                        }
                        else tryC = 0;

                        Console.Write(" " + sok + " ");

                        TimeSpan ctd = DateTime.Now.Subtract(st);

                        if (d < const_maxValue)
                        {
                            Console.Write(String.Format("{0:0} km {1:0} min", d / 1000, t, rrs.route.nodes.Length));
                            Console.Write(String.Format(" - {0}:{1:00}:{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds));
                            Console.Write(" - ");
                            Console.WriteLine(String.Format("{0}T{1}B{2}.rgway.xml", nodes[x].id, nodes[y].id, rrs.Region));
                        }
                        else
                        {
                            Console.WriteLine(String.Format("!!!NO WAY!!! - {0}:{1:00}:{2:000}", ts.Minutes, ts.Seconds, ts.Milliseconds));
                        };
                        // Console.WriteLine(" ... Calc time: " + ctd.Hours.ToString("0,0") + ":" + ctd.Minutes.ToString("0,0") + ":" + ctd.Seconds.ToString("0,0")+"."+ctd.Milliseconds.ToString("0,00"));

                        li.Add(nodes[y].id); lc.Add(c); ld.Add(d); lt.Add(t);
                    };
                };

                nodes[x].links = li.ToArray();
                nodes[x].costs = lc.ToArray();
                nodes[x].dists = ld.ToArray();
                nodes[x].times = lt.ToArray();
            };

            // XML
            Console.WriteLine("Saving RGNodes: " + Path.GetFileName(stream_FileMain) + ".rgnodes.xml");
            XMLSaved<TRGNode[]>.Save(stream_FileMain + ".rgnodes.xml", nodes);

            // TXT
            System.IO.FileStream fs = new FileStream(stream_FileMain + ".rgnodes.txt", FileMode.Create);
            System.IO.StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.GetEncoding(1251));
            sw.WriteLine("RGNodes in region: ");
            for (int i = 0; i < nodes.Length; i++)
            {
                sw.Write(i > 0 ? "," : "");
                sw.Write(nodes[i].id.ToString());
            };
            if (err_list.Length > 0)
            {
                sw.WriteLine(); sw.WriteLine();
                sw.WriteLine("NO WAY:");
                sw.Write(err_list);
            };
            sw.Flush();
            sw.Close();

            // GPX
            fs = new FileStream(stream_FileMain + ".rgnodes.gpx", FileMode.Create);
            sw = new StreamWriter(fs, System.Text.Encoding.GetEncoding(1251));

            sw.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\" ?>");
            sw.WriteLine("<gpx xmlns=\"http://www.topografix.com/GPX/1/1/\" version=\"1.1\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd\">");

            for (uint i = 0; i < nodes.Length; i++)
                sw.WriteLine("<wpt lat=\"" + nodes[i].lat.ToString().Replace(",", ".") + "\" lon=\"" + nodes[i].lon.ToString().Replace(",", ".") + "\"><name>" + nodes[i].id.ToString() + "</name></wpt>");
            sw.WriteLine("</gpx>");

            sw.Flush();
            sw.Close();

            Console.WriteLine("Done");
        }


        private uint ctStart = 0;
        private uint ctEnd = 0;
        private void CalcTimed()// FOR CALC RGNodes
        {
            SolveAstar(ctStart, ctEnd); // A* 
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
            for (uint y = 1; y <= graph_NodesCount; y++) sw.Write(y.ToString() + "\t\t\t");
            sw.WriteLine();

            sw.Write("COST \t");
            for (uint y = 1; y <= graph_NodesCount; y++)
                if (GetCost(y) > const_maxValue)
                    sw.Write(". . .\t");
                else
                    sw.Write(GetPrev(y).ToString() + "(" + GetCost(y).ToString("0.000").Replace(",", ".") + ") \t");
            sw.WriteLine();

            sw.Write("DIST \t");
            for (uint y = 1; y <= graph_NodesCount; y++)
                if (GetCost(y) > const_maxValue)
                    sw.Write(". . .\t");
                else
                    sw.Write(GetPrev(y).ToString() + "(" + GetRouteDistance(y, y).ToString("0.000").Replace(",", ".") + ") \t");
            sw.WriteLine(); ;

            sw.Write("TIME \t");
            for (uint y = 1; y <= graph_NodesCount; y++)
                if (GetCost(y) > const_maxValue)
                    sw.Write(". . .\t");
                else
                    sw.Write(GetPrev(y).ToString() + "(" + GetRouteTime(y, y).ToString("0.000").Replace(",", ".") + ") \t");
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

            byte[] buff = new byte[1020]; // 1020 bytes - 85 points // 8184 bytes - 682 points
            int read = 0;
            bool brk = false;
            if (stream_Geo_LL_Mtx != null) stream_Geo_LL_Mtx.WaitOne();
            long strPos = header_RMPOINTNLL1.Length + 4;
            stream_Geo_LL.Position = strPos;
            while ((read = stream_Geo_LL.Read(buff, 0, buff.Length)) > 0)
            {
                strPos = stream_Geo_LL.Position;
                if (stream_Geo_LL_Mtx != null) stream_Geo_LL_Mtx.ReleaseMutex();

                float lastlat = BitConverter.ToSingle(buff, read - 8);
                if (lastlat < StartLat)
                {
                    if (stream_Geo_LL_Mtx != null) stream_Geo_LL_Mtx.WaitOne();
                    stream_Geo_LL.Position = strPos;
                    continue; // goto next block if last lat < needed
                };

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

                if (stream_Geo_LL_Mtx != null) stream_Geo_LL_Mtx.WaitOne();
                stream_Geo_LL.Position = strPos;
            };
            if (stream_Geo_LL_Mtx != null) stream_Geo_LL_Mtx.ReleaseMutex();

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
            float dLat = metersRadius / Utils.GetLengthMeters(Lat, Lon, Lat + 1, Lon, false);
            float dLon = metersRadius / Utils.GetLengthMeters(Lat, Lon, Lat, Lon + 1, false);
            TNode[] res = FindNodesInRect(Lat - dLat, Lon - dLon, Lat + dLat, Lon + dLon, getLatLon || getDistance, fillLinks, maxResults);
            TNodeD[] wd = new TNodeD[res.Length];
            if (getDistance)
            {
                for (int i = 0; i < res.Length; i++)
                    wd[i] = new TNodeD(res[i], Utils.GetLengthMeters(Lat, Lon, res[i].lat, res[i].lon, false));
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
                if (stream_Geo_Mtx != null) stream_Geo_Mtx.WaitOne();
                stream_Geo.Position = header_RMPOINTNLL0.Length + 4 + (nodes[i] - 1) * 8;
                stream_Geo.Read(ba, 0, ba.Length);
                if (stream_Geo_Mtx != null) stream_Geo_Mtx.ReleaseMutex();
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
                if (stream_Geo_Mtx != null) stream_Geo_Mtx.WaitOne();
                stream_Geo.Position = header_RMPOINTNLL0.Length + 4 + (i - 1) * 8;
                stream_Geo.Read(ba, 0, ba.Length);
                if (stream_Geo_Mtx != null) stream_Geo_Mtx.ReleaseMutex();
                float lat = BitConverter.ToSingle(ba, 0);
                float lon = BitConverter.ToSingle(ba, 4);
                float dist = i == 0 ? 0 : Utils.GetLengthMeters(res[i - 1].lat, res[i - 1].lon, res[i].lat, res[i].lon, false);
                res[i] = new TNodeD(nodes[i], lat, lon, dist);
            };
            return res;
        }

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

            float dLat = metersRadius / Utils.GetLengthMeters(Lat, Lon, Lat + 1, Lon, false);
            float dLon = metersRadius / Utils.GetLengthMeters(Lat, Lon, Lat, Lon + 1, false);

            float LatMin = Lat - dLat;
            float LonMin = Lon - dLon;
            float LatMax = Lat + dLat;
            float LonMax = Lon + dLon;

            List<uint> lines = new List<uint>();

            long strPos = header_RMSEGMENTS.Length + 4;
            if (stream_LinesSegments_Mtx != null) stream_LinesSegments_Mtx.WaitOne();
            stream_LinesSegments.Position = strPos;

            byte[] ba = new byte[8190];
            int read = 0;

            // LINES in ZONE
            while ((read = stream_LinesSegments.Read(ba, 0, ba.Length)) > 0)
            {
                strPos = stream_LinesSegments.Position;
                if (stream_LinesSegments_Mtx != null) stream_LinesSegments_Mtx.ReleaseMutex();

                int count = read / const_SegmRecordLength;
                for (int i = 0; i < count; i++)
                {
                    int off = const_SegmRecordLength * i;
                    uint line_no = BitConverter.ToUInt32(ba, off + 0);
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
                        if ((lines.Count == 0) || (lines[lines.Count - 1] != line_no))
                            lines.Add(line_no);
                    };
                };

                if (stream_LinesSegments_Mtx != null) stream_LinesSegments_Mtx.WaitOne();
                stream_LinesSegments.Position = strPos;
            };
            if (stream_LinesSegments_Mtx != null) stream_LinesSegments_Mtx.ReleaseMutex();

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
                    float l = DistanceFromPointToLine(searchNearPoint, ss[x - 1], ss[x], out pol, out lor);
                    //float l = Utils.GetLengthMeters(Lat, Lon, pol.Y, pol.X, false);
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

            // select nearest line
            ushort ttlsegments;
            int pos;
            TLineFlags flags;
            uint node_first; // гарантированный исходными данными вход в линию
            uint node_last; // гарантированный исходными данными выход из линии
            GetLine(res.line, out ttlsegments, out pos, out flags, out node_first, out node_last);
            PointF[] polyline = GetLineSegments(res.line, pos, ttlsegments, false, false);

            List<PointF> vectorN = new List<PointF>();
            List<PointF> vectorR = new List<PointF>();
            uint nodeN = 0;
            uint nodeR = 0;

            // если начало //
            bool retN = flags.IsOneWay || (side * -1 > 0);
            if (TrueStartFalseEnd)
            {
                // ДЛЯ учета TRA //
                uint[] _n; Single[] _c; Single[] _d; Single[] _t; uint[] _l; byte[] _r;
                SelectNode(node_last, out _n, out _c, out _d, out _t, out _l, out _r); // node2 - гарантированная исходными данными выходная точка из конца линии (существует всегда)
                for (int i = 0; i < _l.Length; i++)
                    if (_l[i] == res.line) // если из node2 можно ехать по этой же линии обратно
                        node_first = _n[i]; // то node1 - выходная точка из начала линии, иначе - oneWay и ее не надо менять, т.к. она ни на что не влияет

                nodeN = node_last; // для начала нормальная точка выезда из линии - последняя точка
                nodeR = node_first;

                vectorN.Add(searchNearPoint);
                vectorR.Add(searchNearPoint);
                if ((!float.IsNaN(onLinePoint.X)) && (!float.IsNaN(onLinePoint.Y)))
                {
                    vectorN.Add(onLinePoint);
                    vectorR.Add(onLinePoint);
                };
                for (int i = segmentNo; i < polyline.Length; i++) vectorN.Add(polyline[i]);
                for (int i = segmentNo - 1; i >= 0; i--) vectorR.Add(polyline[i]);
            }
            else // если конец
            {
                // ДЛЯ учета TRA //
                uint[] _n; Single[] _c; Single[] _d; Single[] _t; uint[] _l; byte[] _r;
                SelectNodeR(node_first, out _n, out _c, out _d, out _t, out _l, out _r); // node1 - гарантированная исходными данными входная точка в начало линии (существует всегда)
                for (int i = 0; i < _l.Length; i++)
                    if (_l[i] == res.line) // если в node1 можно въехать по этой же линии
                        node_last = _n[i]; // то node2 - входная точка в конец линии, иначе - oneWay и ее не надо менять, т.к. она ни на что не влияет

                nodeN = node_first; // для конца нормальная точка възда в линию - первая точка
                nodeR = node_last;

                for (int i = 0; i < segmentNo; i++) vectorN.Add(polyline[i]);
                for (int i = polyline.Length - 1; i >= segmentNo; i--) vectorR.Add(polyline[i]);
                if ((!float.IsNaN(onLinePoint.X)) && (!float.IsNaN(onLinePoint.Y)))
                {
                    vectorN.Add(onLinePoint);
                    vectorR.Add(onLinePoint);
                };
                vectorN.Add(searchNearPoint);
                vectorR.Add(searchNearPoint);
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
                res.distToN += Utils.GetLengthMeters(res.normal[i - 1].Y, res.normal[i - 1].X, res.normal[i].Y, res.normal[i].X, false);
            for (int i = 1; i < res.revers.Length; i++)
                res.distToR += Utils.GetLengthMeters(res.revers[i - 1].Y, res.revers[i - 1].X, res.revers[i].Y, res.revers[i].X, false);

            return res;
        }

        /// <summary>
        ///     Находим проекцию точку на линию (ближайшая точка на ближайшей линии)
        /// </summary>
        /// <param name="Lat"></param>
        /// <param name="Lon"></param>
        /// <param name="metersRadius">Радиус поиска ближайшей линии в метрах</param>
        /// <param name="distance">Расстояние от искомой точки</param>
        /// <returns></returns>
        public PointF PointToNearestLine(float Lat, float Lon, float metersRadius, out double distance, out uint line)
        {
            distance = double.MaxValue;
            line = uint.MaxValue;

            float dLat = metersRadius / Utils.GetLengthMeters(Lat, Lon, Lat + 1, Lon, false);
            float dLon = metersRadius / Utils.GetLengthMeters(Lat, Lon, Lat, Lon + 1, false);

            float LatMin = Lat - dLat;
            float LonMin = Lon - dLon;
            float LatMax = Lat + dLat;
            float LonMax = Lon + dLon;

            List<uint> lines = new List<uint>();

            long strPos = header_RMSEGMENTS.Length + 4;
            if (stream_LinesSegments_Mtx != null) stream_LinesSegments_Mtx.WaitOne();
            stream_LinesSegments.Position = strPos;

            byte[] ba = new byte[8190];
            int read = 0;

            // LINES in ZONE
            while ((read = stream_LinesSegments.Read(ba, 0, ba.Length)) > 0)
            {
                strPos = stream_LinesSegments.Position;
                if (stream_LinesSegments_Mtx != null) stream_LinesSegments_Mtx.ReleaseMutex();

                int count = read / const_SegmRecordLength;
                for (int i = 0; i < count; i++)
                {
                    int off = const_SegmRecordLength * i;
                    uint line_no = BitConverter.ToUInt32(ba, off + 0);
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
                        if ((lines.Count == 0) || (lines[lines.Count - 1] != line_no))
                            lines.Add(line_no);
                    };
                };

                if (stream_LinesSegments_Mtx != null) stream_LinesSegments_Mtx.WaitOne();
                stream_LinesSegments.Position = strPos;
            };
            if (stream_LinesSegments_Mtx != null) stream_LinesSegments_Mtx.ReleaseMutex();

            PointF searchNearPoint = new PointF(Lon, Lat);
            PointF onLinePoint = new PointF(0, 0);

            // check nearest line from found
            for (int i = 0; i < lines.Count; i++)
            {
                PointF[] ss = GetLineSegments(lines[i], false, false);
                for (ushort x = 1; x < ss.Length; x++)
                {
                    PointF pol;
                    int lor = 0;
                    float l = DistanceFromPointToLine(searchNearPoint, ss[x - 1], ss[x], out pol, out lor);
                    //float l = Utils.GetLengthMeters(Lat, Lon, pol.Y, pol.X, false);
                    if (l < distance)
                    {
                        distance = l;
                        line = lines[i];
                        onLinePoint = pol;
                    };
                };
            };

            if ((line == 0) || (line == uint.MaxValue)) // not found
                return new PointF(Lon, Lat);
            else
                return onLinePoint;
        }

        /// <summary>
        ///     Находим расстояние от исходной/конечной координаты до каждой ближайшей линии
        /// </summary>
        /// <param name="Lat">Широта</param>
        /// <param name="Lon">Долгота</param>
        /// <param name="metersRadius">Радиус поиска ближайшей линии в метрах</param>
        /// <returns>array[0..NumberOfNearPoints-1][0: distance,1: X or Lon,2: Y or Lat,3: line_id]</returns>
        public float[][] FindNearestLines(float Lat, float Lon, float metersRadius)
        {
            if (metersRadius > 1000) metersRadius = 1000;

            float dLat = metersRadius / Utils.GetLengthMeters(Lat, Lon, Lat + 1, Lon, false);
            float dLon = metersRadius / Utils.GetLengthMeters(Lat, Lon, Lat, Lon + 1, false);

            float LatMin = Lat - dLat;
            float LonMin = Lon - dLon;
            float LatMax = Lat + dLat;
            float LonMax = Lon + dLon;

            List<uint> lines = new List<uint>();

            long strPos = header_RMSEGMENTS.Length + 4;
            if (stream_LinesSegments_Mtx != null) stream_LinesSegments_Mtx.WaitOne();
            stream_LinesSegments.Position = strPos;

            byte[] ba = new byte[8190];
            int read = 0;

            // LINES in ZONE
            while ((read = stream_LinesSegments.Read(ba, 0, ba.Length)) > 0)
            {
                strPos = stream_LinesSegments.Position;
                if (stream_LinesSegments_Mtx != null) stream_LinesSegments_Mtx.ReleaseMutex();

                int count = read / const_SegmRecordLength;
                for (int i = 0; i < count; i++)
                {
                    int off = const_SegmRecordLength * i;
                    uint line_no = BitConverter.ToUInt32(ba, off + 0);
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
                        if ((lines.Count == 0) || (lines[lines.Count - 1] != line_no))
                            lines.Add(line_no);
                    };
                };

                if (stream_LinesSegments_Mtx != null) stream_LinesSegments_Mtx.WaitOne();
                stream_LinesSegments.Position = strPos;
            };
            if (stream_LinesSegments_Mtx != null) stream_LinesSegments_Mtx.ReleaseMutex();

            FDXY sorter = new FDXY();
            PointF searchNearPoint = new PointF(Lon, Lat);
            List<float[]> res = new List<float[]>();
            // check nearest line from found
            for (int i = 0; i < lines.Count; i++)
            {
                float[] fbase = new float[] { float.MaxValue, 0, 0, 0 };
                PointF[] ss = GetLineSegments(lines[i], false, false);
                for (ushort x = 1; x < ss.Length; x++)
                {
                    PointF pol;
                    int lor = 0;
                    float l = DistanceFromPointToLine(searchNearPoint, ss[x - 1], ss[x], out pol, out lor);
                    //float l = Utils.GetLengthMeters(Lat, Lon, pol.Y, pol.X, false);
                    if (l < fbase[0]) fbase = new float[] { l, pol.X, pol.Y, lines[i] };
                };
                res.Add(fbase);
            };

            res.Sort(sorter);
            return res.ToArray();
        }
        private class FDXY : IComparer<float[]> { public int Compare(float[] a, float[] b) { return a[0].CompareTo(b[0]); } }

        /// <summary>
        ///     Получаем угол поворота двух линий
        /// </summary>
        /// <param name="prev">Начальная точка</param>
        /// <param name="turn">Точка поворота</param>
        /// <param name="next">Конечная точка</param>
        /// <returns>Угол, - левый, + правый</returns>
        public static float GetLinesTurnAngle(PointF prev, PointF turn, PointF next)
        {
            float dy0 = turn.Y - prev.Y;
            float dx0 = turn.X - prev.X;
            float dy1 = next.Y - turn.Y;
            float dx1 = next.X - turn.X;

            float res = (float)(-1 * Math.Acos(((dx0 * dx1) + (dy0 * dy1)) / ((Math.Sqrt(dx0 * dx0 + dy0 * dy0)) * (Math.Sqrt(dx1 * dx1 + dy1 * dy1)))) / Math.PI * 180);
            if (((Math.Abs(res) > 179) && (Math.Abs(res) < 181))) return 180;

            int side = Math.Sign((turn.X - prev.X) * (next.Y - prev.Y) - (turn.Y - prev.Y) * (next.X - prev.X));
            return res * side;
        }

        /// <summary>
        ///     Расчет расстояния от точки до линии
        /// </summary>
        /// <param name="pt">Искомая точка</param>
        /// <param name="lineStart">Нач точка линии</param>
        /// <param name="lineEnd">Кон точка линии</param>
        /// <param name="pointOnLine">точка на линии ближайшая к искомой</param>
        /// <param name="side">С какой стороны линии находится искомая точка (+ слева, - справа)</param>
        /// <returns>метры</returns>
        private static float DistanceFromPointToLine(PointF pt, PointF lineStart, PointF lineEnd, out PointF pointOnLine, out int side)
        {
            float dx = lineEnd.X - lineStart.X;
            float dy = lineEnd.Y - lineStart.Y;

            if ((dx == 0) && (dy == 0))
            {
                // line is a point
                // линия может быть с нулевой длиной после анализа TRA
                pointOnLine = lineStart;
                side = 0;
                //dx = pt.X - lineStart.X;
                //dy = pt.Y - lineStart.Y;                
                //return Math.Sqrt(dx * dx + dy * dy);
                return Utils.GetLengthMeters(pt.Y, pt.X, pointOnLine.Y, pointOnLine.X, false);
            };

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

            //return Math.Sqrt(dx * dx + dy * dy);
            return Utils.GetLengthMeters(pt.Y, pt.X, pointOnLine.Y, pointOnLine.X, false);
        }

    }
}
