/* 
 * C# Class by Milok Zbrozek <milokz@gmail.com>
 * Модуль подготовки файлов графа из Shape'ов
 * GARMIN или OSM2SHP
 * Author: Milok Zbrozek <milokz@gmail.com>
 * Версия: 211221DA
 * 
 * Поддерживает источники данных: GARMIN, OSM, OSM2SHP, WATER (OSM)
 * 
 */

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

using dkxce.Route.Classes;

namespace dkxce.Route.Shp2Rt
{
    /// <summary>
    ///     Garmin Shapes 2 Graph Files Converter
    /// </summary>
    public class ShpToGraphConverter
    {
        private const string _Converter = "dkxce.Route.Shp2Rt/21.12.21.3-V4-win32";
        
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
        private static byte[] header_RMLINES = new byte[] { 0x52, 0x4D, 0x4C, 0x49, 0x4E, 0x45, 0x53 };
        private static byte[] header_RMSEGMENTS = new byte[] { 0x52, 0x4D, 0x53, 0x45, 0x47, 0x4D, 0x45, 0x4E, 0x54, 0x53 };
        private static byte[] header_RMGRAF2 = new byte[] { 0x52, 0x4D, 0x47, 0x52, 0x41, 0x46, 0x32 };
        private static byte[] header_RMGRAF3 = new byte[] { 0x52, 0x4D, 0x47, 0x52, 0x41, 0x46, 0x33 };
        private static byte[] header_RMINDEX = new byte[] { 0x52, 0x4D, 0x49, 0x4E, 0x44, 0x45, 0x58 };
        private static byte[] header_RMPOINTNLL0 = new byte[] { 0x52, 0x4D, 0x50, 0x4F, 0x49, 0x4E, 0x54, 0x4E, 0x4C, 0x4C, 0x30 };
        private static byte[] header_RMPOINTNLL1 = new byte[] { 0x52, 0x4D, 0x50, 0x4F, 0x49, 0x4E, 0x54, 0x4E, 0x4C, 0x4C, 0x31 };
        private static byte[] header_RMLINKIDS = new byte[] { 0x52, 0x4D, 0x4C, 0x49, 0x4E, 0x4B, 0x49, 0x44, 0x53 };
        private static byte[] header_RMLATTRIB = new byte[] { 0x52, 0x4D, 0x4C, 0x41, 0x54, 0x54, 0x52, 0x49, 0x42 };
        private static byte[] header_RMTURNRSTR = new byte[] { 0x52, 0x4D, 0x54, 0x55, 0x52, 0x4E, 0x52, 0x53, 0x54, 0x52 };
        private static byte[] header_RMTMC = new byte[] { 0x52, 0x4D, 0x54, 0x4D, 0x43 };

        private const byte const_LineRecordLength = 15;
        private const byte const_SegmRecordLength = 30;

        private double[] bounds_box = new double[] { double.MaxValue, double.MaxValue, double.MinValue, double.MinValue };

        private bool writeLinesNamesFile = true;
        /// <summary>
        ///     Писать ли файл наименования дорог
        /// </summary>
        public bool WriteLinesNamesFile { get { return writeLinesNamesFile; } set { writeLinesNamesFile = true; } }

        /// <summary>
        ///     Список точек стыковки межрайонных маршрутов
        /// </summary>
        private List<TRGNode> analyse_RGNodes = new List<TRGNode>();

        /// <summary>
        ///     Имена полей DBF файла
        /// </summary>
        private ShapeFields stream_ShapeFile_FieldNames;

        /// <summary>
        ///     Максимальная ошибка в расчетах
        /// </summary>
        private const Single const_maxError = (Single)1e-6;

        /// <summary>
        ///     Кодировка DBF файла
        /// </summary>
        private System.Text.Encoding stream_DBFEncoding;

        /// <summary>
        ///     Имя shp файла
        /// </summary>
        private string stream_ShapeFileName;
        /// <summary>
        ///     Имя файла графа
        /// </summary>
        private string stream_FileName;
        /// <summary>
        ///     Имя префикса файлов графа
        /// </summary>
        private string stream_FileMain;

        /// <summary>
        ///     Общее число линий
        /// </summary>
        private int analyse_LinesCount = 0;
        private int analyse_LinesDCount = 0;
        /// <summary>
        ///     Общее число сегментов
        /// </summary>
        private int analyse_SegmentsCount = 0;

        /// <summary>
        ///     Pointer to Lines
        /// </summary>
        private Stream stream_Lines;
        /// <summary>
        ///     Pointer to Segments
        /// </summary>
        private Stream stream_LinesSegments;
        /// <summary>
        ///     Pointer to Line Index
        /// </summary>
        private Stream stream_LineIndex;
        /// <summary>
        ///     Pointer to Line Attributes
        /// </summary>
        private Stream stream_LineAttr;
        /// <summary>
        ///     Pointer to Line TMC
        /// </summary>
        private Stream stream_TMC;
        /// <summary>
        ///     Pointer to WriteLinesNamesFile
        /// </summary>
        private Stream stream_Names;

        /// <summary>
        ///     Узлы графа
        /// </summary>
        private List<TNode> analyse_Nodes = new List<TNode>();
        
        /// <summary>
        ///     Максимальное длина линии
        /// </summary>
        private float analyse_maxLengthBetweenNodes = 0;
        /// <summary>
        ///     Максимальное время движения по линии
        /// </summary>
        private float analyse_maxTimeBetweenNodes = 0;
        /// <summary>
        ///     Максимальная оценка между узлами
        /// </summary>
        private float analyse_maxCostBetweenNodes = 0;

        /// <summary>
        ///     Список найденных запретов поворотов
        /// </summary>
        private List<TurnRestriction> analyse_NoTurns = new List<TurnRestriction>();

        /// <summary>
        ///     Число добавленных запретов поворотов
        /// </summary>
        private int analyse_NoTurnsAdded = 0;

        /// <summary>
        ///     Подсчет атрибутивной информации
        /// </summary>
        public bool analyse_attributes_do = false;
        private int[] analyse_attributes_bit = new int[56];
        private int[] analyse_attributes_val = new int[10];

        /// <summary>
        ///     Номер региона
        /// </summary>
        private int analyse_regionID = 0;

        /// <summary>
        ///     Номер региона
        /// </summary>
        public int RegionID { get { return analyse_regionID; } set { analyse_regionID = value; } }

        /// <summary>
        ///     Конвертор Shape файла
        /// </summary>
        /// <param name="ShapeFileName">Полный путь</param>
        public ShpToGraphConverter(string ShapeFileName)
        {
            Create(ShapeFileName, 0);
        }

        /// <summary>
        ///     Конвертор Shape файла
        /// </summary>
        /// <param name="ShapeFileName">Полный путь</param>
        /// <param name="regionID">Номер региона</param>
        public ShpToGraphConverter(string ShapeFileName, int regionID)
        {
            Create(ShapeFileName, regionID);
        }

        /// <summary>
        ///     Конвертор Shape файла
        /// </summary>
        /// <param name="ShapeFileName">Полный путь</param>
        /// <param name="regionID">Номер региона</param>
        private void Create(string ShapeFileName, int regionID)
        {
            this.analyse_regionID = regionID;

            stream_NumberFormat = (System.Globalization.NumberFormatInfo)stream_CultureInfo.NumberFormat.Clone();
            stream_NumberFormat.NumberDecimalSeparator = ".";
            this.stream_ShapeFileName = ShapeFileName;

            //shpf = new ShapesFields();
            //ShapesFields.Save(ShapesFields.GetCurrentDir()+@"\fldcfg.xml", shpf);            

            string fieldCongifDefault = XMLSaved<int>.GetCurrentDir() + @"\default.fldcfg.xml";
            string fieldConfigFileName = Path.GetDirectoryName(stream_ShapeFileName) + @"\" + Path.GetFileNameWithoutExtension(stream_ShapeFileName) + ".fldcfg.xml";
            string fieldConfigFileDef = Path.GetDirectoryName(stream_ShapeFileName) + @"\default.fldcfg.xml";
            if (File.Exists(fieldConfigFileName))
                stream_ShapeFile_FieldNames = ShapeFields.Load(fieldConfigFileName);
            else if (File.Exists(fieldConfigFileDef))
                stream_ShapeFile_FieldNames = ShapeFields.Load(fieldConfigFileDef);
            else if (File.Exists(fieldCongifDefault))
                stream_ShapeFile_FieldNames = ShapeFields.Load(fieldCongifDefault);
            else
                stream_ShapeFile_FieldNames = new ShapeFields();

            stream_DBFEncoding = stream_ShapeFile_FieldNames.CodePage;
        }

        DateTime started = DateTime.MinValue;

        // todo = 0 - поля всегда должны быть (LinkID, GarminType, OSM_ID, OSM_SURFACE, SpeedLimit, OneWay); ошибка если полей нет
        // todo = 1 - желательные поля (OSM_TYPE, OSM_SURFACE, GarminType); нет ошибки если нет полей или не указаны
        // todo = 2 - проверяются, если указаны (ROUTE_LEVEL, ROUTE_SPEED, TMC, RGNODE, ...); ошибка если указаны, но нет полей
        // todo = 3 - желательные поля (Length, Name, TurnRestrictions); ошибка если указаны, но нет полей
        private void CheckFields(string name, ref string f, List<string> flds, ref string fieldsOk, ref string fieldsBad, ref string fieldsErr, ref string fieldsEdd, ref bool raiseNoFields, byte todo, char fType, Hashtable fTypes, ref string cautions)
        {
            if ((f != null) && (f != String.Empty) && (flds.Contains(f)) && (fType != '0') && ((string)fTypes[f]!= fType.ToString()))
                cautions += name + " is not `" + fType.ToString() + "`, ";

            string[] ff = new string[0];
            if ((f != null) && (f != String.Empty)) ff = f.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            bool fCont = false;
            foreach(string fff in ff)
                if (flds.Contains(fff))
                {
                    f = fff;
                    fCont = true;
                    break;
                };

            if (todo == 0)
            {
                if (fCont)
                    fieldsOk += name + ", ";
                else
                {
                    fieldsErr += name + ", ";
                    fieldsEdd += name + ", ";
                    raiseNoFields = true;
                };
            };
            if (todo == 1)
            {
                if (fCont)
                    fieldsOk += name + ", ";
                else
                    fieldsErr += name + ", ";
            };
            if (todo == 2)
            {
                if ((f != null) && (f != String.Empty))
                {
                    if (fCont)
                        fieldsOk += name + ", ";
                    else
                    {
                        fieldsErr += name + ", ";
                        fieldsEdd += name + ", ";
                        raiseNoFields = true;
                    };
                }
                else
                    fieldsBad += name + ", ";
            };
            if (todo == 3)
            {
                if ((f != null) && (f != String.Empty))
                {
                    if (fCont)
                        fieldsOk += name + ", ";
                    else
                    {
                        fieldsErr += name + ", ";
                        fieldsEdd += name + ", ";
                        raiseNoFields = true;
                    };
                }
                else
                    fieldsErr += name + ", ";
            };
        }

        /// <summary>
        ///     Конвертация Shape файла в граф
        /// </summary>
        /// <param name="GraphFileName"></param>
        public void ConvertTo(string GraphFileName)
        {
            ConvertTo(GraphFileName, null);
        }

        /// <summary>
        ///     Конвертация Shape файла в граф
        /// </summary>
        /// <param name="GraphFileName"></param>
        public void ConvertTo(string GraphFileName, string regionName)
        {
            ConsoleColor cc = Console.ForegroundColor;

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("dkxce Shape-Graph Converter");
            Console.WriteLine(_Converter);
            Console.WriteLine("Copyrights (c) 2014 - 2021 <milokz@gmail.com>");
            Console.WriteLine("Supported sources by *.fldcfg.xml: GARMIN/OSM/OSM2SHP/WATER");                        
            Console.WriteLine();
            Console.ForegroundColor = cc;

            Console.Write("Lines shape  : ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(Path.GetFileName(stream_ShapeFileName));
            Console.ForegroundColor = cc;
            Console.WriteLine(", " + GetFileSize(new FileInfo(stream_ShapeFileName).Length));

            string dbffile = stream_ShapeFileName.Substring(0, stream_ShapeFileName.Length - 4) + ".dbf";
            Console.Write("Lines fields : ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(Path.GetFileName(dbffile));
            Console.ForegroundColor = cc;
            Console.WriteLine(", " + GetFileSize(new FileInfo(dbffile).Length));
            
            string fieldConfigFileName = Path.GetDirectoryName(stream_ShapeFileName) + @"\" + Path.GetFileNameWithoutExtension(stream_ShapeFileName) + ".fldcfg.xml";
            if (File.Exists(fieldConfigFileName))
            {
                Console.Write("Config file  : ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(Path.GetFileName(fieldConfigFileName));
                Console.ForegroundColor = cc;
                Console.WriteLine(", " + GetFileSize(new FileInfo(fieldConfigFileName).Length));
            }
            else
            {
                fieldConfigFileName = Path.GetDirectoryName(stream_ShapeFileName) + @"\default.fldcfg.xml";
                Console.Write("Config file  : ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(Path.GetFileName(fieldConfigFileName));
                Console.ForegroundColor = cc;
                Console.WriteLine(", " + GetFileSize(new FileInfo(fieldConfigFileName).Length));
            };

            Console.ForegroundColor = cc;
            Console.Write("Source type  : ");
            Console.ForegroundColor = ConsoleColor.Yellow;

            List<string> flds = new List<string>();
            Hashtable fht = new Hashtable();
            string fieldsOk = "";
            string fieldsBad = "";
            string fieldsErr = "";
            string fieldsEdd = "";
            string cautions = "";
            bool raiseNoFields = false;
            // READ FIELDS
            {
                try
                {
                    string[] FIELDS = ReadDBFFields(this.stream_ShapeFileName, out fht);
                    if ((FIELDS == null) || (FIELDS.Length == 0))
                        throw new Exception("No Any Fields Found");
                    flds.AddRange(FIELDS);
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error: Cannot read DBF File");
                    Console.WriteLine(ex.Message);
                };


                if (stream_ShapeFile_FieldNames.SOURCE == "GARMIN")
                {
                    Console.WriteLine(stream_ShapeFile_FieldNames.SOURCE);

                    CheckFields("LinkId", ref stream_ShapeFile_FieldNames.fldLinkId, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 0, 'N', fht, ref cautions);
                    CheckFields("GarminType", ref stream_ShapeFile_FieldNames.fldGarminType, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 0, 'C', fht, ref cautions);
                }
                else if (stream_ShapeFile_FieldNames.SOURCE == "WATER")
                {
                    Console.WriteLine(stream_ShapeFile_FieldNames.SOURCE);

                    CheckFields("OSM_ID", ref stream_ShapeFile_FieldNames.fldOSM_ID, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 0, 'N', fht, ref cautions);
                }
                else if (stream_ShapeFile_FieldNames.SOURCE == "OSM")
                {
                    Console.WriteLine(stream_ShapeFile_FieldNames.SOURCE);

                    CheckFields("OSM_ID", ref stream_ShapeFile_FieldNames.fldOSM_ID, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 0, 'N', fht, ref cautions);
                    CheckFields("OSM_SURFACE", ref stream_ShapeFile_FieldNames.fldOSM_SURFACE, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 0, 'C', fht, ref cautions);
                }
                else if ((stream_ShapeFile_FieldNames.SOURCE == "OSM2") || (stream_ShapeFile_FieldNames.SOURCE == "OSM2SHP"))
                {                    
                    Console.WriteLine(stream_ShapeFile_FieldNames.SOURCE);
                    
                    string nodes_file = Path.GetDirectoryName(stream_ShapeFileName).Trim('\\') + @"\[NODES].shp";
                    string nodes_dbff = Path.GetDirectoryName(stream_ShapeFileName).Trim('\\') + @"\[NODES].dbf";
                    if (!String.IsNullOrEmpty(stream_ShapeFile_FieldNames.fldOSM_ADDIT_DBF_NODES))
                    {
                        nodes_file = Path.GetDirectoryName(stream_ShapeFileName).Trim('\\') + @"\" + stream_ShapeFile_FieldNames.fldOSM_ADDIT_DBF_NODES;
                        try { nodes_dbff = nodes_file.Replace(Path.GetExtension(nodes_file), ".dbf"); } catch { };
                    };
                    Console.ForegroundColor = cc;
                    Console.Write("Nodes shape  : ");                    
                    if (File.Exists(nodes_file))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write(Path.GetFileName(nodes_file));
                        Console.ForegroundColor = cc;
                        Console.WriteLine(", " + GetFileSize(new FileInfo(nodes_file).Length));
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write(Path.GetFileName(nodes_file));
                        Console.ForegroundColor = cc;
                        Console.WriteLine(" - no file");
                    };
                    Console.Write("Nodes fields : ");
                    if (File.Exists(nodes_dbff))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write(Path.GetFileName(nodes_dbff));
                        Console.ForegroundColor = cc;
                        Console.WriteLine(", " + GetFileSize(new FileInfo(nodes_dbff).Length));
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write(Path.GetFileName(nodes_dbff));
                        Console.ForegroundColor = cc;
                        Console.WriteLine(" - no file");
                    };

                    string noturns_file = Path.GetDirectoryName(stream_ShapeFileName).Trim('\\') + @"\[NOTURN].shp";
                    if (!String.IsNullOrEmpty(stream_ShapeFile_FieldNames.fldOSM_ADDIT_DBF_NOTURN))
                        noturns_file = Path.GetDirectoryName(stream_ShapeFileName).Trim('\\') + @"\" + stream_ShapeFile_FieldNames.fldOSM_ADDIT_DBF_NOTURN;
                    Console.ForegroundColor = cc;
                    Console.Write("NoTurn file  : ");
                    if (File.Exists(noturns_file))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write(Path.GetFileName(noturns_file));
                        Console.ForegroundColor = cc;
                        Console.WriteLine(", " + GetFileSize(new FileInfo(noturns_file).Length));
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write(Path.GetFileName(noturns_file));
                        Console.ForegroundColor = cc;
                        Console.WriteLine(" - no file");
                    };

                    CheckFields("OSM_ID", ref stream_ShapeFile_FieldNames.fldOSM_ID, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 0, 'N', fht, ref cautions);
                    CheckFields("OSM_TYPE", ref stream_ShapeFile_FieldNames.fldOSM_TYPE, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 1, 'C', fht, ref cautions);
                    CheckFields("OSM_SURFACE", ref stream_ShapeFile_FieldNames.fldOSM_SURFACE, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 1, 'C', fht, ref cautions);
                    CheckFields("GarminType", ref stream_ShapeFile_FieldNames.fldGarminType, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 1, 'C', fht, ref cautions);

                    CheckFields("SERVICE", ref stream_ShapeFile_FieldNames.fldOSM_ADDIT_SERVICE, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'C', fht, ref cautions);
                    CheckFields("JUNCTION", ref stream_ShapeFile_FieldNames.fldOSM_ADDIT_JUNCTION, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'C', fht, ref cautions);
                    CheckFields("LANES", ref stream_ShapeFile_FieldNames.fldOSM_ADDIT_LANES, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'C', fht, ref cautions);
                    CheckFields("MAXACTUAL", ref stream_ShapeFile_FieldNames.fldOSM_ADDIT_MAXACTUAL, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'C', fht, ref cautions);
                };
                // other fields
                if (stream_ShapeFile_FieldNames.SOURCE == "WATER")
                {
                    CheckFields("SpeedLimit", ref stream_ShapeFile_FieldNames.fldSpeedLimit, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 1, 'N', fht, ref cautions);
                    CheckFields("RouteSpeed", ref stream_ShapeFile_FieldNames.fldRouteSpeed, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 1, 'N', fht, ref cautions);
                    CheckFields("OneWay", ref stream_ShapeFile_FieldNames.fldOneWay, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 1, '0', fht, ref cautions);
                    CheckFields("Length", ref stream_ShapeFile_FieldNames.fldLength, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 1, 'F', fht, ref cautions);
                    CheckFields("Name", ref stream_ShapeFile_FieldNames.fldName, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 1, 'C', fht, ref cautions);                    
                }
                else 
                {
                    CheckFields("SpeedLimit", ref stream_ShapeFile_FieldNames.fldSpeedLimit, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 0, 'N', fht, ref cautions);
                    CheckFields("RouteLevel", ref stream_ShapeFile_FieldNames.fldRouteLevel, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'N', fht, ref cautions);
                    CheckFields("RouteSpeed", ref stream_ShapeFile_FieldNames.fldRouteSpeed, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'N', fht, ref cautions);
                    CheckFields("OneWay", ref stream_ShapeFile_FieldNames.fldOneWay, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 0, '0', fht, ref cautions);
                    CheckFields("Length", ref stream_ShapeFile_FieldNames.fldLength, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, (byte)( stream_ShapeFile_FieldNames.SOURCE == "GARMIN" ? 3 : 1), 'F', fht, ref cautions);
                    CheckFields("Name", ref stream_ShapeFile_FieldNames.fldName, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 3, 'C', fht, ref cautions);
                    CheckFields("TurnRestrictions", ref stream_ShapeFile_FieldNames.fldTurnRstr, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, (byte)(stream_ShapeFile_FieldNames.SOURCE == "GARMIN" ? 3 : 1), 'C', fht, ref cautions);
                    CheckFields("TMC", ref stream_ShapeFile_FieldNames.fldTMC, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'C', fht, ref cautions);
                    CheckFields("RGNODE", ref stream_ShapeFile_FieldNames.fldRGNODE, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'C', fht, ref cautions);
                    CheckFields("ACC_MASK", ref stream_ShapeFile_FieldNames.fldACCMask, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'C', fht, ref cautions);
                    CheckFields("ATTR", ref stream_ShapeFile_FieldNames.fldAttr, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'C', fht, ref cautions);
                    CheckFields("MaxWeight", ref stream_ShapeFile_FieldNames.fldMaxWeight, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'F', fht, ref cautions);
                    CheckFields("MaxAxle", ref stream_ShapeFile_FieldNames.fldMaxAxle, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'F', fht, ref cautions);
                    CheckFields("MaxHeight", ref stream_ShapeFile_FieldNames.fldMaxHeight, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'F', fht, ref cautions);
                    CheckFields("MaxWidth", ref stream_ShapeFile_FieldNames.fldMaxWidth, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'F', fht, ref cautions);
                    CheckFields("MaxLength", ref stream_ShapeFile_FieldNames.fldMaxLength, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'F', fht, ref cautions);
                    CheckFields("MinDistance", ref stream_ShapeFile_FieldNames.fldMinDistance, flds, ref fieldsOk, ref fieldsBad, ref fieldsErr, ref fieldsEdd, ref raiseNoFields, 2, 'F', fht, ref cautions);
                };
            };  // END READ FIELDS

            Console.ForegroundColor = cc;
            Console.Write("Output file  : ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(Path.GetFileName(GraphFileName));

            Console.ForegroundColor = cc;
            Console.Write("Region ID    : ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(RegionID.ToString());

            if (!String.IsNullOrEmpty(regionName))
            {
                Console.ForegroundColor = cc;
                Console.Write("Region Name  : ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(regionName);
            };

            Console.ForegroundColor = cc;
            Console.Write("DBF Encoding : ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(this.stream_DBFEncoding.CodePage.ToString() + " " + this.stream_DBFEncoding.EncodingName);

            Console.ForegroundColor = cc;
            Console.Write("Attrib stats : ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(analyse_attributes_do ? "calculate" : "no");

            if (cautions.Length > 0)
            {
                Console.ForegroundColor = cc;
                Console.Write("Cautions     : ");
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine(cautions.Trim().Trim(','));
            };

            Console.ForegroundColor = cc;
            Console.Write("Used Fields  : ");
            if (fieldsOk.Length > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(fieldsBad.Length > 0 || fieldsErr.Length > 0 ? fieldsOk : fieldsOk.Trim().Trim(','));
            };
            if (fieldsErr.Length > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(fieldsBad.Length > 0 ? fieldsErr : fieldsErr.Trim().Trim(','));
            };
            if (fieldsBad.Length > 0)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write(fieldsBad.Trim().Trim(','));
            };            
            if (raiseNoFields)
            {
                Console.WriteLine();
                Console.WriteLine("Error: Required Fields (" + fieldsEdd.Trim().Trim(',') + ") not Found");
                Console.ReadLine();
                return;
            };
            if (true) // -- ELSE FIELDS --
            {
                string elseF = "";
                string noelF = "";
                if (flds.Contains("BRIDGE")) elseF += (elseF.Length > 0 ? ", " : "") + "BRIDGE"; else noelF += (noelF.Length > 0 ? ", " : "") + "BRIDGE";
                if (flds.Contains("TUNNEL")) elseF += (elseF.Length > 0 ? ", " : "") + "TUNNEL"; else noelF += (noelF.Length > 0 ? ", " : "") + "TUNNEL";
                if (flds.Contains("FERRY")) elseF += (elseF.Length > 0 ? ", " : "") + "FERRY"; else noelF += (noelF.Length > 0 ? ", " : "") + "FERRY";
                if (flds.Contains("ROUTE")) elseF += (elseF.Length > 0 ? ", " : "") + "ROUTE"; else noelF += (noelF.Length > 0 ? ", " : "") + "ROUTE";
                if (flds.Contains("FORD")) elseF += (elseF.Length > 0 ? ", " : "") + "FORD"; else noelF += (noelF.Length > 0 ? ", " : "") + "FORD";
                if (flds.Contains("TOLL")) elseF += (elseF.Length > 0 ? ", " : "") + "TOLL"; else noelF += (noelF.Length > 0 ? ", " : "") + "TOLL";
                if (flds.Contains("FOOTWAY")) elseF += (elseF.Length > 0 ? ", " : "") + "FOOTWAY"; else noelF += (noelF.Length > 0 ? ", " : "") + "FOOTWAY";                
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                if (elseF != "") Console.WriteLine("{1}Addit Fields : {0}", elseF, "\r\n");
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                if (noelF != "") Console.WriteLine("{1}NoAdd Fields : {0}", noelF, elseF == "" ? "\r\n" : "");
            };
            Console.WriteLine();            
            Console.WriteLine();
            
            started = DateTime.Now;
            Console.ForegroundColor = cc;
            Console.Write("Started: ");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss dd.MM.yyyy"));
            Console.WriteLine();
            Console.ForegroundColor = cc;


            string tmp_dir_c = Path.GetDirectoryName(GraphFileName);
            Directory.CreateDirectory(tmp_dir_c);

            this.stream_FileName = GraphFileName;
            this.stream_FileMain = GraphFileName.Remove(GraphFileName.LastIndexOf("."));

            Console.WriteLine("Preparing: " + Path.GetFileName(stream_FileMain) + ".lines.bin");
            stream_Lines = new FileStream(stream_FileMain + ".lines.bin", FileMode.Create);
            stream_Lines.Write(header_RMLINES, 0, header_RMLINES.Length);
            byte[] ba = new byte[4];
            stream_Lines.Write(ba, 0, ba.Length);

            Console.WriteLine("Preparing: " + Path.GetFileName(stream_FileMain) + ".segments.bin");
            stream_LinesSegments = new FileStream(stream_FileMain + ".segments.bin", FileMode.Create);
            stream_LinesSegments.Write(header_RMSEGMENTS, 0, header_RMSEGMENTS.Length);
            ba = new byte[4];
            stream_LinesSegments.Write(ba, 0, ba.Length);

            Console.WriteLine("Preparing: " + Path.GetFileName(stream_FileMain) + ".lines.id");
            stream_LineIndex = new FileStream(stream_FileMain + ".lines.id", FileMode.Create);
            stream_LineIndex.Write(header_RMLINKIDS, 0, header_RMLINKIDS.Length);
            ba = new byte[4];
            stream_LineIndex.Write(ba, 0, ba.Length);

            Console.WriteLine("Preparing: " + Path.GetFileName(stream_FileMain) + ".lines.att");
            stream_LineAttr = new FileStream(stream_FileMain + ".lines.att", FileMode.Create);
            stream_LineAttr.Write(header_RMLATTRIB, 0, header_RMLATTRIB.Length);
            ba = new byte[4];
            stream_LineAttr.Write(ba, 0, ba.Length);

            if ((stream_ShapeFile_FieldNames.fldTMC != null) && (stream_ShapeFile_FieldNames.fldTMC != String.Empty))
            {
                Console.WriteLine("Preparing: " + Path.GetFileName(stream_FileMain) + ".lines.tmc");
                stream_TMC = new FileStream(stream_FileMain + ".lines.tmc", FileMode.Create);
                stream_TMC.Write(header_RMTMC, 0, header_RMTMC.Length);
                ba = BitConverter.GetBytes(analyse_regionID);
                stream_TMC.Write(ba, 0, ba.Length);
                ba = new byte[4];
                stream_TMC.Write(ba, 0, ba.Length);
            };

            if ((stream_ShapeFile_FieldNames.fldName != null) && (stream_ShapeFile_FieldNames.fldName != String.Empty) && (writeLinesNamesFile))
            {
                Console.WriteLine("Preparing: " + Path.GetFileName(stream_FileMain) + ".lines.txt");
                stream_Names = new FileStream(stream_FileMain + ".lines.txt", FileMode.Create);
                ba = System.Text.Encoding.GetEncoding(1251).GetBytes("Файл наименований линий\r\n");
                stream_Names.Write(ba, 0, ba.Length);
            };
            Console.WriteLine();


            ReadInnerFiles(this.stream_ShapeFileName);
            Console.WriteLine("Shape Files Reading Completed");

            // do not save yet, until TurnRestriction check
            //lnStr.Position = RMLINES.Length;
            //ba = BitConverter.GetBytes(linesCount);
            //lnStr.Write(ba, 0, ba.Length);
            //lnStr.Flush();
            //lnStr.Close(); // .lines.bin
            //Console.WriteLine("Write: " + Path.GetFileName(grMain) + ".lines.bin Completed");

            stream_LinesSegments.Position = header_RMSEGMENTS.Length;
            ba = BitConverter.GetBytes(analyse_SegmentsCount);
            stream_LinesSegments.Write(ba, 0, ba.Length);
            stream_LinesSegments.Flush();
            stream_LinesSegments.Close(); // .segments.bin
            Console.WriteLine("Write: " + Path.GetFileName(stream_FileMain) + ".segments.bin Completed");

            stream_LineIndex.Position = header_RMLINKIDS.Length;
            ba = BitConverter.GetBytes(analyse_LinesCount);
            stream_LineIndex.Write(ba, 0, ba.Length);
            stream_LineIndex.Flush();
            List<int> linkids = new List<int>(analyse_LinesCount);
            for (int i = 0; i < analyse_LinesCount; i++)
            {
                stream_LineIndex.Read(ba, 0, ba.Length);
                linkids.Add(BitConverter.ToInt32(ba, 0));
            };
            stream_LineIndex.Close();// .lines.id
            Console.WriteLine("Write: " + Path.GetFileName(stream_FileMain) + ".lines.id Completed");

            stream_LineAttr.Position = header_RMLATTRIB.Length;
            ba = BitConverter.GetBytes(analyse_LinesCount);
            stream_LineAttr.Write(ba, 0, ba.Length);
            stream_LineAttr.Flush();
            stream_LineAttr.Close();// .lines.att
            Console.WriteLine("Write: " + Path.GetFileName(stream_FileMain) + ".lines.att Completed");

            if ((stream_ShapeFile_FieldNames.fldTMC != null) && (stream_ShapeFile_FieldNames.fldTMC != String.Empty))
            {
                stream_TMC.Position = header_RMTMC.Length + 4;
                ba = BitConverter.GetBytes(analyse_LinesCount);
                stream_TMC.Write(ba, 0, ba.Length);
                stream_TMC.Flush();
                stream_TMC.Close();// .lines.tmc
                Console.WriteLine("Write: " + Path.GetFileName(stream_FileMain) + ".lines.tmc Completed");
            };

            if ((stream_ShapeFile_FieldNames.fldName != null) && (stream_ShapeFile_FieldNames.fldName != String.Empty) && (writeLinesNamesFile))
            {
                stream_Names.Flush();
                stream_Names.Close();
                Console.WriteLine("Write: " + Path.GetFileName(stream_FileMain) + ".lines.txt Completed");
            };

            try
            {
                SaveNodes(linkids);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.ToString());
                throw ex;
            };

            // TurnRestrictions Checked ->- Save Lines
            stream_Lines.Position = header_RMLINES.Length;
            ba = BitConverter.GetBytes(analyse_LinesCount);
            stream_Lines.Write(ba, 0, ba.Length);
            stream_Lines.Flush();
            stream_Lines.Close(); // .lines.bin
            Console.WriteLine("Write: " + Path.GetFileName(stream_FileMain) + ".lines.bin Completed");

            // Save Additional Information
            AdditionalInformation ai = new AdditionalInformation();
            ai.ConvertedWith = _Converter;
            ai.SourceType = stream_ShapeFile_FieldNames.SOURCE;
            ai.regionID = analyse_regionID;            
            ai.minX = bounds_box[0];
            ai.minY = bounds_box[1];
            ai.maxX = bounds_box[2];
            ai.maxY = bounds_box[3];
            ai.TestMap = "{lat:" + ai.centerY.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",lon:" + ai.centerX.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",zoom:12}";
            if (!String.IsNullOrEmpty(regionName)) ai.RegionName = regionName;
            XMLSaved<AdditionalInformation>.Save(stream_FileMain + ".addit.xml", ai);

            // DONE //

            DateTime ended = DateTime.Now;
            TimeSpan elapsed = ended.Subtract(started);

            FileStream rtStr = new FileStream(GraphFileName, FileMode.Create);
            StreamWriter sw = new StreamWriter(rtStr, Encoding.GetEncoding(1251));
            sw.WriteLine("dkxce shape-graph converter result main file");
            sw.WriteLine("Version: " + _Converter);
            sw.WriteLine("Started: " + started.ToString("yyyy-MM-dd HH:mm:ss"));
            sw.WriteLine("Converted: " + ended.ToString("yyyy-MM-dd HH:mm:ss"));
            sw.WriteLine("Elapsed: " + String.Format("{0:00} дн {1:00}:{2:00}:{3:00}", new object[] { elapsed.Days, elapsed.Hours, elapsed.Minutes, elapsed.Seconds }));
            sw.WriteLine("Source type: " + this.stream_ShapeFile_FieldNames.SOURCE);
            sw.WriteLine("Source path: " + Path.GetDirectoryName(stream_ShapeFileName));
            sw.WriteLine("Source file: " + Path.GetFileName(stream_ShapeFileName));            
            if (!String.IsNullOrEmpty(this.stream_ShapeFile_FieldNames.fldOSM_ADDIT_DBF_NODES)) sw.WriteLine("Nodes to Split file: " + this.stream_ShapeFile_FieldNames.fldOSM_ADDIT_DBF_NODES);
            if (!String.IsNullOrEmpty(this.stream_ShapeFile_FieldNames.fldOSM_ADDIT_DBF_NOTURN)) sw.WriteLine("External NoTurns file: " + this.stream_ShapeFile_FieldNames.fldOSM_ADDIT_DBF_NOTURN);
            sw.WriteLine("DBF CodePage: " + this.stream_DBFEncoding.CodePage.ToString());
            sw.WriteLine("DBF Encoding: " + this.stream_DBFEncoding.EncodingName);
            sw.WriteLine("Latitude Inversed: " + this.stream_ShapeFile_FieldNames.InverseLat.ToString());
            sw.WriteLine("Longitude Inversed: " + this.stream_ShapeFile_FieldNames.InverseLon.ToString());
            if (this.stream_ShapeFile_FieldNames.SOURCE == "OSM2SHP") sw.WriteLine("Process Aggreagated Tags: " + this.stream_ShapeFile_FieldNames.fldOSM_ADDIT_PROCESSAGG.ToString());
            sw.WriteLine("Max Cost Between Nodes: " + analyse_maxCostBetweenNodes.ToString(stream_NumberFormat));
            sw.WriteLine("Max Length Between Nodes: " + analyse_maxLengthBetweenNodes.ToString(stream_NumberFormat) + " m");
            sw.WriteLine("Max Time Between Nodes: " + analyse_maxTimeBetweenNodes.ToString(stream_NumberFormat) + " min");
            sw.WriteLine("Lines: " + analyse_LinesCount.ToString());
            sw.WriteLine("Nodes to Split: " + nodes_list.Count.ToString());            
            sw.WriteLine("Splitted Lines: " + analyse_LinesDCount.ToString());            
            sw.WriteLine("Segments: " + analyse_SegmentsCount.ToString());
            sw.WriteLine("Nodes: " + analyse_Nodes.Count.ToString());            
            sw.WriteLine("External NoTurns: " + noturns_list.Count.ToString());            
            sw.WriteLine("Turn Restrictions: " + analyse_NoTurnsAdded.ToString());

            if (analyse_attributes_do)
            {
                sw.WriteLine("Attributes...");
                string[] attnms = new string[56] 
                    {
                        // avg.. - average speed
                        // max.. - max speed
                        // lvl - level
                        // 1w - oneway
                        "00x01 ca Дворовый проезд / Жилая зона (5.21)", // 0x01
                        "00x02 up Грунтовая дорога / Дорога без покрытия", // 0x02
                        "00x04 cc Дорога с бетонным покрытием", // 0x04
                        "00x08 st Дорога отсыпанная гравием (1.16)", //0x08
                        "00x10 sa Дорога отсыпанная песком", // 0x10
                        "00x20 tm Временная дорога", // 0x20
                        "00x40 tn Тоннель (1.31)", //0x40
                        "00x80 br Мост", //0x80
                        "01x01 db Разводной мост (1.9)", //1x01
                        "01x02 pt Пантонный мост", //1x02
                        "01x04 ft Паром / переправа", //1x04
                        "01x08 rc Железнодорожный переезд (1.1, 1.2)", // 1x08
                        "01x10 wd Брод", // 1x10
                        "01x20 ??", // 1x20
                        "01x40 ??", // 1x40
                        "01x80 ??", // 1x80
                        "02x01 r1 Реверсивное движение в одну полосу", //2x01
                        "02x02 aa Дорога для автомобилей (5.3)", //2x02
                        "02x04 hw Автомагистраль (5.1)", //2x04
                        "02x08 tr Платная дорога", //2x08
                        "02x10 nt Движение грузового транспорта запрещено (3.4)", //2x10
                        "02x20 nm Движение мотоциклов запрещено (3.5)", //2x20
                        "02x40 na Движение тракторов запрещено (3.6)", //2x40
                        "02x80 nw Движение с прицепом запрещено (3.7)", //2x80
                        "03x01 ct Таможня / Таможенная граница (3.17.1)", //3x01
                        "03x02 iu Крутой спуск (1.13)", //3x02
                        "03x04 id Крутой подъем (1.14)", //3x04
                        "03x08 rw Дорожные работы", //3x08
                        "03x10 ot Обгон запрещен (3.20)", //3x10
                        "03x20 ol Обгон грузовым транспортом запрещен (3.22)", //3x20
                        "03x40 ns Остановка запрещена (3.27)", //3x40
                        "03x80 np Стоянка запрещена (3.28)", //3x80
                        "04x01 nd Движение с опасными грузами запрещено (3.32)", //4x01
                        "04x02 ne Движение транспортных средств с взрывчатыми и огнеопасными грузами запрещено (3.33)", //4x02
                        "04x04 tl Светофор", //4x04
                        "04x08 fw Дороги для пешеходов (OSM Pedestrian)", //4x08
                        "04x10 lt Неосвещенные дороги (OSM)", //4x10
                        "04x20 ??", //4x20
                        "04x40 ??", //4x40
                        "04x80 ww Водные пути (WATER)", //4x80
                        "05x01 ??", //5x01
                        "05x02 ??", //5x02
                        "05x04 ??", //5x04
                        "05x08 ??", //5x08
                        "05x10 ??", //5x10
                        "05x20 ??", //5x20
                        "05x40 ??", //5x40
                        "05x80 ??", //5x80
                        "06x01 ??", //6x01
                        "06x02 ??", //6x02
                        "06x04 ??", //6x04
                        "06x08 ??", //6x08
                        "06x10 ??", //6x10
                        "06x20 ??", //6x20
                        "06x40 ??", //6x40
                        "06x80 ??"  //6x80
                    };
                string[] attnms2 = new string[10]
                    {
                        "07xFF ?? Ограничение массы ТС", // 7 mwe..
                        "08xFF ?? Ограничение нагрузки на ось ТС", //8 mos..
                        "09xFF ?? Высота полосы", //9 mhe..
                        "10xFF ?? Ширина полосы", //10 mwi..
                        "11xFF ?? Ограничение длины ТС", //11 mle..
                        "12xFF ?? Минимальная дистанция между ТС", //12 mdi
                        "13xFF ??", //13
                        "14xFF ??", //14
                        "15xF8 max.. SpeedLimit", //15
                        "15x07 lvl.. RouteLevel" // 15
                    };

                for (int i = 0; i < 56; i++)
                    if (analyse_attributes_bit[i] > 0)
                        sw.WriteLine("\t" + attnms[i] + ": " + analyse_attributes_bit[i].ToString());

                for (int i = 7; i < 15; i++)
                    if (analyse_attributes_val[i - 7] > 0)
                        sw.WriteLine("\t" + attnms2[i - 7] + ": " + analyse_attributes_val[i - 7].ToString());

                if (analyse_attributes_val[8] > 0)
                    sw.WriteLine("\t" + attnms2[8] + ": " + analyse_attributes_val[8].ToString());
                if (analyse_attributes_val[9] > 0)
                    sw.WriteLine("\t" + attnms2[9] + ": " + analyse_attributes_val[9].ToString());
            };

            sw.WriteLine();
            sw.WriteLine("Files:");
            sw.WriteLine("  " + Path.GetFileName(stream_FileMain) + ".addit.xml - Additional information");
            sw.WriteLine("  " + Path.GetFileName(stream_FileMain) + ".lines.bin - Lines information (segments count, pos, oneway, node start, node stop)");
            sw.WriteLine("  " + Path.GetFileName(stream_FileMain) + ".lines.id - Lines LINK_ID/ROAD_ID");
            sw.WriteLine("  " + Path.GetFileName(stream_FileMain) + ".lines.att - Lines attributes");
            sw.WriteLine("  " + Path.GetFileName(stream_FileMain) + ".segments.bin - Lines segments");
            if ((stream_ShapeFile_FieldNames.fldTMC != null) && (stream_ShapeFile_FieldNames.fldTMC != String.Empty))
                sw.WriteLine("  " + Path.GetFileName(stream_FileMain) + ".lines.tmc - Lines TMC Codes");
            sw.WriteLine("  " + Path.GetFileName(stream_FileMain) + ".lines.txt - Lines names");
            sw.WriteLine("  " + Path.GetFileName(stream_FileMain) + ".graph.bin - Graph nodes information");
            sw.WriteLine("  " + Path.GetFileName(stream_FileMain) + ".graph.bin.in - Index for node position in graph");
            sw.WriteLine("  " + Path.GetFileName(stream_FileMain) + ".graph[r].bin - Graph nodes information for inverse solve");
            sw.WriteLine("  " + Path.GetFileName(stream_FileMain) + ".graph[r].bin.in - Index for node position in graph (inverse solve)");
            sw.WriteLine("  " + Path.GetFileName(stream_FileMain) + ".graph.geo - Nodes Lat Lon information");
            sw.WriteLine("  " + Path.GetFileName(stream_FileMain) + ".graph.geo.ll - Indexed Lat Lon for nodes");
            sw.WriteLine("  " + Path.GetFileName(stream_FileMain) + ".rgnodes.txt - Region Nodes Text Information");
            sw.WriteLine("  " + Path.GetFileName(stream_FileMain) + ".rgnodes.xml - Region Nodes Information");
            sw.WriteLine("  " + Path.GetFileName(stream_FileMain) + ".analyze.txt - Nodes In/Out fail information");
            sw.Flush();
            sw.Close();
            rtStr.Close();

            Console.WriteLine();
            Console.WriteLine("Convertion Done: " + ended.ToString("HH:mm:ss"));
            Console.WriteLine("Elapsed: " + String.Format("{0:00}:{1:00}:{2:00}", elapsed.TotalHours, elapsed.Minutes, elapsed.Seconds));
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

            int noTurnsWas = analyse_NoTurns.Count;
            int noTurnsSkipped = 0;
            int newNodesAdded = 0;
            while (analyse_NoTurns.Count > 0)
            {
                TurnRestriction ctr = analyse_NoTurns[0];
                TNode nd = analyse_Nodes[(int)ctr.throughNode - 1]; // узел throught
                List<TNode> nns = new List<TNode>(); // узлы подмены                    

                // для каждой входящей связи)
                for (int _in = 0; _in < nd.rlinks.Count; _in++)
                {
                    List<uint> canLine = new List<uint>(); // список линий куда можно ехать
                    for (int _ot = 0; _ot < nd.links.Count; _ot++) canLine.Add(nd.links[_ot].line);

                    for (int nt = analyse_NoTurns.Count - 1; nt >= 0; nt--) // удаляем куда нельзя ехать
                        if ((analyse_NoTurns[nt].fromLine == nd.rlinks[_in].line) && (analyse_NoTurns[nt].throughNode == nd.node))
                        {
                            try
                            {
                                int lid  = linkIds.IndexOf(analyse_NoTurns[nt].toLineLinkID); /// OSM_ID may be overflow !!! ///
                                if (lid >= 0)
                                {
                                    uint line = (uint)lid + 1; // find line by link_id/osm_id/road_id
                                    if (line == nd.rlinks[_in].line)
                                    {
                                        // No U-Turn
                                    };
                                    canLine.Remove(line); // remove line from can go list                                
                                    analyse_NoTurnsAdded++;
                                }
                                else
                                    noTurnsSkipped++;
                            }
                            catch 
                            {
                                noTurnsSkipped++;
                            };
                            analyse_NoTurns.RemoveAt(nt); // remove turn restriction                            
                        };
                    TNode nn = new TNode((uint)(analyse_Nodes.Count + _in), nd.lat, nd.lon); // создаем новый узел
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
                analyse_Nodes[(int)nd.node - 1] = nns[0];
                // add new
                for (int i = 1; i < nns.Count; i++)
                    analyse_Nodes.Add(nns[i]);
                // update near nodes
                for (int i = 0; i < nns.Count; i++)
                {
                    for (int _in = 0; _in < nns[i].rlinks.Count; _in++)
                    {
                        TNode upd = analyse_Nodes[(int)nns[i].rlinks[_in].node - 1];
                        for (int _ot = 0; _ot < upd.links.Count; _ot++)
                            if (upd.links[_ot].node == nd.node)
                                upd.links[_ot].node = nns[i].node;
                    };
                    for (int _ot = 0; _ot < nns[i].links.Count; _ot++)
                    {
                        TNode upd = analyse_Nodes[(int)nns[i].links[_ot].node - 1];
                        for (int _in = 0; _in < upd.rlinks.Count; _in++)
                            if (upd.rlinks[_in].node == nd.node)
                                upd.rlinks[_in].node = nns[i].node;
                    };
                };
                // update changes in noTurns
                if(analyse_NoTurns.Count > 0)
                    for (int i = analyse_NoTurns.Count - 1; i >= 0; i--)
                    {
                        // проверка сходимости
                        if (analyse_NoTurns[i].throughNode == nd.node)
                        {
                            string txt = "";
                            try /// OSM_ID may be overflow !!! ///
                            {
                                txt = "NoTurn from ROAD_ID " + linkIds[(int)analyse_NoTurns[i].fromLine - 1].ToString() + " to ROAD_ID " + analyse_NoTurns[i].toLineLinkID.ToString();
                            }
                            catch 
                            {
                                txt = "NoTurn from ROAD_ID ??? to ROAD_ID " + analyse_NoTurns[i].toLineLinkID.ToString();
                            };
                            // если в узле несколько запретов с поворотов и начальные
                            // линии не были найдены ранее, то возможно дорога односторонняя,
                            // а запрет поворота указан в запрещенном направлении движения.
                            // Поэтому сперва ищем линию в исходящих связях
                            bool deleted = false;
                            for (int _out = 0; _out < nd.links.Count; _out++)
                                if (analyse_NoTurns[i].fromLine == nd.links[_out].line)
                                    deleted = true;
                            // если линия найдена, то дорога одностороняя и повернуть 
                            // в указанном в запрете направлении нельзя - удаляем лишнюю информацию
                            if (deleted)
                            {
                                Console.SetCursorPosition(0, Console.CursorTop - 1);
                                Console.WriteLine(txt + " ignored - WRONG WAY DIRECTION");
                                Console.WriteLine("");
                                analyse_NoTurns.RemoveAt(i);
                            }
                            else
                            {
                                // если линия не найдена, то ее нет в исходном shp файле
                                Console.WriteLine("ERROR: Запреты поворотов не сходятся, проверьте исходные данные!!! " + txt);
                                Console.ReadLine();
                                throw new StackOverflowException("Запреты поворотов не сходятся, проверьте исходные данные!!! " + txt);
                            };
                        };
                    };
                // update incoming lines
                for (int _in = 0; _in < nd.rlinks.Count; _in++)
                {
                    stream_Lines.Position = header_RMLINES.Length + 4 + const_LineRecordLength * (nd.rlinks[_in].line) - 8;
                    byte[] ba = new byte[8];
                    stream_Lines.Read(ba, 0, 8);
                    uint node1 = BitConverter.ToUInt32(ba, 0);
                    uint node2 = BitConverter.ToUInt32(ba, 4);
                    if (node1 == nd.node) node1 = nns[_in].node;
                    if (node2 == nd.node) node2 = nns[_in].node;
                    stream_Lines.Position -= 8;
                    ba = BitConverter.GetBytes(node1);
                    stream_Lines.Write(ba, 0, ba.Length);
                    ba = BitConverter.GetBytes(node2);
                    stream_Lines.Write(ba, 0, ba.Length);
                };
                stream_Lines.Flush();
                //
                newNodesAdded += nns.Count - 1;
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.WriteLine(String.Format("NoTurns with new analyzed added: {0} from {1} found; skipped {2}", analyse_NoTurnsAdded, noTurnsWas, noTurnsSkipped));
            };
            // TRA DONE

            Console.WriteLine("New nodes to add (after Turn Restrictions Analyze): " + newNodesAdded.ToString());
            Console.WriteLine("Turn Restrictions Analyze (TRA) Completed");
            Console.WriteLine();
            /////

            // save graph            
            Console.WriteLine("Preparing: " + Path.GetFileName(stream_FileMain) + ".graph.bin");
            FileStream grStr = new FileStream(stream_FileMain + ".graph.bin", FileMode.Create);
            grStr.Write(header_RMGRAF2, 0, header_RMGRAF2.Length);
            byte[] nodesC = BitConverter.GetBytes(analyse_Nodes.Count);
            grStr.Write(nodesC, 0, nodesC.Length);
            byte[] maxl = BitConverter.GetBytes(analyse_maxLengthBetweenNodes);
            grStr.Write(maxl, 0, maxl.Length);

            // save reversed graph
            Console.WriteLine("Preparing: " + Path.GetFileName(stream_FileMain) + ".graph[r].bin");
            FileStream grStrR = new FileStream(stream_FileMain + ".graph[r].bin", FileMode.Create);
            grStrR.Write(header_RMGRAF3, 0, header_RMGRAF2.Length);
            BitConverter.GetBytes(analyse_Nodes.Count);
            grStrR.Write(nodesC, 0, nodesC.Length);
            BitConverter.GetBytes(analyse_maxLengthBetweenNodes);
            grStrR.Write(maxl, 0, maxl.Length);

            // save graph index
            Console.WriteLine("Preparing: " + Path.GetFileName(stream_FileMain) + ".graph.bin.in");
            FileStream giStr = new FileStream(stream_FileMain + ".graph.bin.in", FileMode.Create);
            giStr.Write(header_RMINDEX, 0, header_RMINDEX.Length);
            giStr.Write(nodesC, 0, nodesC.Length);

            // save reversed graph index
            Console.WriteLine("Preparing: " + Path.GetFileName(stream_FileMain) + ".graph[r].bin.in");
            FileStream giStrR = new FileStream(stream_FileMain + ".graph[r].bin.in", FileMode.Create);
            giStrR.Write(header_RMINDEX, 0, header_RMINDEX.Length);
            giStrR.Write(nodesC, 0, nodesC.Length);

            // graph nodes lat lon
            Console.WriteLine("Preparing: " + Path.GetFileName(stream_FileMain) + ".graph.geo");
            FileStream geoStr = new FileStream(stream_FileMain + ".graph.geo", FileMode.Create);
            geoStr.Write(header_RMPOINTNLL0, 0, header_RMPOINTNLL0.Length);
            geoStr.Write(nodesC, 0, nodesC.Length);

            Console.WriteLine("Write Node...");
            FileStream ninf = new FileStream(stream_FileMain + ".graph.analyze.txt", FileMode.Create);
            StreamWriter nw = new StreamWriter(ninf);
            nw.WriteLine("Анализ узлов графа на наличие ребер... ");
            for (int i = 0; i < analyse_Nodes.Count; i++)
            {
                bool nl = false;
                if (analyse_Nodes[i].Ins == 0)
                {
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                    string ln = String.Format("... в узел {0} нельзя попасть {1} {2} ", i + 1, analyse_Nodes[i].lat, analyse_Nodes[i].lon);
                    Console.WriteLine(ln);
                    nw.WriteLine(ln);
                    nl = true;
                };
                if (analyse_Nodes[i].Outs == 0)
                {
                    if (!nl) Console.SetCursorPosition(0, Console.CursorTop - 1);
                    string ln = String.Format("... из узла {0} нельзя выехать {1} {2} ", i + 1, analyse_Nodes[i].lat, analyse_Nodes[i].lon);
                    Console.WriteLine(ln);
                    nw.WriteLine(ln);
                    nl = true;
                };
                if (nl) Console.WriteLine();

                byte[] lat = BitConverter.GetBytes(analyse_Nodes[i].lat); // Write nodes Lat Lon
                geoStr.Write(lat, 0, lat.Length);
                byte[] lon = BitConverter.GetBytes(analyse_Nodes[i].lon);
                geoStr.Write(lon, 0, lon.Length);

                byte links = (byte)analyse_Nodes[i].links.Count; // Write Node links pos
                byte[] pos = BitConverter.GetBytes((uint)grStr.Position);
                giStr.Write(pos, 0, pos.Length);

                byte rlinks = (byte)analyse_Nodes[i].rlinks.Count;  // Write Node rlinks pos
                byte[] posR = BitConverter.GetBytes((uint)grStrR.Position);
                giStrR.Write(posR, 0, posR.Length);

                grStr.WriteByte(links);
                grStrR.WriteByte(rlinks);

                for (int x = 0; x < analyse_Nodes[i].links.Count; x++)  // Write Node links
                {
                    byte[] next = BitConverter.GetBytes(analyse_Nodes[i].links[x].node);// 4
                    grStr.Write(next, 0, next.Length);
                    byte[] cost = BitConverter.GetBytes(analyse_Nodes[i].links[x].cost);// 4
                    grStr.Write(cost, 0, cost.Length);
                    byte[] dist = BitConverter.GetBytes(analyse_Nodes[i].links[x].dist);// 4
                    grStr.Write(dist, 0, dist.Length);
                    byte[] time = BitConverter.GetBytes(analyse_Nodes[i].links[x].time);// 4
                    grStr.Write(time, 0, time.Length);
                    byte[] line = BitConverter.GetBytes(analyse_Nodes[i].links[x].line);// 4
                    grStr.Write(line, 0, line.Length);
                    byte rev = analyse_Nodes[i].links[x].inverse_dir ? (byte)1 : (byte)0;//// 1
                    grStr.WriteByte(rev);
                };
                grStr.Flush();

                for (int x = 0; x < analyse_Nodes[i].rlinks.Count; x++)  // Write Node rlinks
                {
                    byte[] next = BitConverter.GetBytes(analyse_Nodes[i].rlinks[x].node);// 4
                    grStrR.Write(next, 0, next.Length);
                    byte[] cost = BitConverter.GetBytes(analyse_Nodes[i].rlinks[x].cost);// 4
                    grStrR.Write(cost, 0, cost.Length);
                    byte[] dist = BitConverter.GetBytes(analyse_Nodes[i].rlinks[x].dist);// 4
                    grStrR.Write(dist, 0, dist.Length);
                    byte[] time = BitConverter.GetBytes(analyse_Nodes[i].rlinks[x].time);// 4
                    grStrR.Write(time, 0, time.Length);
                    byte[] line = BitConverter.GetBytes(analyse_Nodes[i].rlinks[x].line);// 4
                    grStrR.Write(line, 0, line.Length);
                    byte rev = analyse_Nodes[i].rlinks[x].inverse_dir ? (byte)1 : (byte)0;//// 1
                    grStrR.WriteByte(rev);
                };
                grStrR.Flush();
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.WriteLine("Nodes added: " + (i + 1).ToString());
            };
            nw.WriteLine("Завершено");
            nw.Flush();
            nw.Close();
            ninf.Close();

            Console.WriteLine("Write: " + Path.GetFileName(stream_FileMain) + ".graph.bin Completed");
            grStr.Flush();
            grStr.Close();

            Console.WriteLine("Write: " + Path.GetFileName(stream_FileMain) + ".graph[r].bin Completed");
            grStrR.Flush();
            grStrR.Close();

            Console.WriteLine("Write: " + Path.GetFileName(stream_FileMain) + ".graph.bin.in Completed");
            giStr.Flush();
            giStr.Close();

            Console.WriteLine("Write: " + Path.GetFileName(stream_FileMain) + ".graph[r].bin.in Completed");
            giStrR.Flush();
            giStrR.Close();

            Console.WriteLine("Write: " + Path.GetFileName(stream_FileMain) + ".graph.geo Completed");
            geoStr.Flush();
            geoStr.Close();

            Console.WriteLine();

            Console.Write("Saving " + Path.GetFileName(stream_FileMain) + ".graph.geo.ll ...");
            analyse_Nodes.Sort(TNode.Sorter.SortByLL());

            // graph nodes lat lon indexed
            FileStream gllStr = new FileStream(stream_FileMain + ".graph.geo.ll", FileMode.Create);
            gllStr.Write(header_RMPOINTNLL1, 0, header_RMPOINTNLL1.Length);
            gllStr.Write(nodesC, 0, nodesC.Length);
            for (int i = 0; i < analyse_Nodes.Count; i++)
            {
                byte[] ba = BitConverter.GetBytes(analyse_Nodes[i].node);  // Write Node Lat Lon Indexed
                gllStr.Write(ba, 0, ba.Length);
                ba = BitConverter.GetBytes(analyse_Nodes[i].lat);
                gllStr.Write(ba, 0, ba.Length);
                ba = BitConverter.GetBytes(analyse_Nodes[i].lon);
                gllStr.Write(ba, 0, ba.Length);
            };
            gllStr.Flush();
            gllStr.Close();
            Console.WriteLine(" Completed");

            Console.WriteLine();
            if (analyse_RGNodes.Count > 0)
            {
                Console.WriteLine("Saving RGNodes: " + analyse_RGNodes.Count.ToString() + " - in " + Path.GetFileName(stream_FileMain) + ".rgnodes.xml");
                XMLSaved<TRGNode[]>.Save(stream_FileMain + ".rgnodes.xml", analyse_RGNodes.ToArray());
                Console.WriteLine();
            };
        }
        
        private int total_lines = 0;        

        public static string GetFileSize(long size)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = size;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            };
            string res = String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##} {1}", len, sizes[order]);
            return res;
        }

        private string[] ReadDBFFields(string filename, out Hashtable fieldsType)
        {            
            string dbffile = filename.Substring(0, filename.Length - 4) + ".dbf";

            FileStream dbfFileStream = new FileStream(dbffile, FileMode.Open, FileAccess.Read);            
            
            // Read File Version
            dbfFileStream.Position = 0;
            int ver = dbfFileStream.ReadByte();

            // Read Records Count
            dbfFileStream.Position = 04;
            byte[] bb = new byte[4];
            dbfFileStream.Read(bb, 0, 4);
            total_lines = BitConverter.ToInt32(bb, 0);            

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
            fieldsType = new Hashtable();   // Массив типов полей
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
            dbfFileStream.Close();

            return Fields_Name;
        }

        /// <summary>
        ///     Читаем shp и dbf
        /// </summary>
        /// <param name="filename">Имя shp файла</param>
        private void ReadInnerFiles(string filename)
        {
            // PRE READ
            if ((stream_ShapeFile_FieldNames.SOURCE == "OSM2") || (stream_ShapeFile_FieldNames.SOURCE == "OSM2SHP"))
            {
                string method = "";
                try
                {
                    method = "Read_OSM_ADDIT_DBF_NODES: ";
                    Read_OSM_ADDIT_DBF_NODES(filename);
                    
                    method = "Read_OSM_ADDIT_DBF_NOTURN: ";
                    Read_OSM_ADDIT_DBF_NOTURN(filename);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: " + method + ex.ToString());
                    Console.ReadLine();
                    throw ex;
                };
            };

            Console.Write("Open Shape Files ");

            FileStream shapeFileStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            long shapeFileLength = shapeFileStream.Length;

            bool readAll = shapeFileLength < 1024 * 1024 * 250; // 250 MB
            if (readAll)
                Console.Write("to Memory ... ");
            else
                Console.WriteLine(" ...");


            Byte[] shapeFileData = new Byte[readAll ? shapeFileLength : 100];//new Byte[shapeFileLength];
            shapeFileStream.Read(shapeFileData, 0, shapeFileData.Length);
            if (readAll)
            {
                Console.WriteLine(GetFileSize(shapeFileLength));
                shapeFileStream.Close();
            };

            // check valid records type
            int shapetype = readIntLittle(shapeFileData, 32);
            if (shapetype != 3)
            {
                Console.WriteLine("ERROR: Shape doesn't contains roads (not polyline)");
                Console.ReadLine();
                throw new Exception("Shape doesn't contains roads (not polyline)");
            };            

            // read DBF
            string dbffile = filename.Substring(0, filename.Length - 4) + ".dbf";
            FileStream dbfFileStream = new FileStream(dbffile, FileMode.Open, FileAccess.Read);

            // Read File Version
            dbfFileStream.Position = 0;
            int ver = dbfFileStream.ReadByte();

            // Read Records Count
            dbfFileStream.Position = 04;
            byte[] bb = new byte[4];
            dbfFileStream.Read(bb, 0, 4);
            total_lines = BitConverter.ToInt32(bb, 0);
            Console.CursorTop = Console.CursorTop - 1;
            Console.Write("Lines in shape: ");
            ConsoleColor cc = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(total_lines.ToString());
            Console.ForegroundColor = cc;
            Console.Write((readAll ? " - in mem " : " - on disk ") + GetFileSize(shapeFileLength));
            {
                string s = "";
                for (int i = Console.CursorLeft; i < 60; i++) s += " ";
                Console.WriteLine(s);
            };

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

            long currentPosition = 100;
            int lines = 0;
            Console.WriteLine();
            Console.WriteLine("Read data...");
            Console.WriteLine();
            while (currentPosition < shapeFileLength)
            {
                // Read Shape
                int offset = 0;
                if (!readAll)
                {
                    shapeFileData = new byte[8];
                    shapeFileStream.Read(shapeFileData, 0, shapeFileData.Length);
                    currentPosition += shapeFileData.Length;
                }
                else
                {
                    offset = (int)currentPosition;
                    currentPosition += 8;
                };

                int recordNumber = readIntBig(shapeFileData, offset + 0);
                int contentLength = readIntBig(shapeFileData, offset + 4);

                if (!readAll)
                {
                    shapeFileData = new byte[contentLength * 2];
                    shapeFileStream.Read(shapeFileData, 0, shapeFileData.Length);
                    currentPosition += shapeFileData.Length;
                }
                else
                {
                    offset = (int)currentPosition;
                    currentPosition += contentLength * 2;
                };

                TLine vline = new TLine();
                int recordShapeType = readIntLittle(shapeFileData, 0);
                vline.box = new Double[4];
                vline.box[0] = readDoubleLittle(shapeFileData, offset + 4);
                vline.box[1] = readDoubleLittle(shapeFileData, offset + 12);
                vline.box[2] = readDoubleLittle(shapeFileData, offset + 20);
                vline.box[3] = readDoubleLittle(shapeFileData, offset + 28);
                vline.numParts = readIntLittle(shapeFileData, offset + 36);
                vline.parts = new int[vline.numParts];
                vline.numPoints = readIntLittle(shapeFileData, offset + 40);
                vline.points = new PointF[vline.numPoints];
                int partStart = offset + 44;
                for (int i = 0; i < vline.numParts; i++)
                {
                    vline.parts[i] = readIntLittle(shapeFileData, partStart + i * 4);
                }
                int pointStart = offset + 44 + 4 * vline.numParts;
                for (int i = 0; i < vline.numPoints; i++)
                {
                    vline.points[i] = new PointF(0, 0);
                    vline.points[i].X = (float)readDoubleLittle(shapeFileData, pointStart + (i * 16));
                    vline.points[i].Y = (float)readDoubleLittle(shapeFileData, pointStart + (i * 16) + 8);
                };

                // Read DBF
                string[] FieldValues = new string[FieldsCount];
                Hashtable recordData = new Hashtable();
                for (int y = 0; y < FieldValues.Length; y++)
                {
                    dbfFileStream.Position = (long)dataRecord_1st_Pos + (long)((long)dataRecord_Length * (long)lines) + (long)Fields_Offset[y];
                    bb = new byte[(int)fieldsLength[Fields_Name[y]]];
                    dbfFileStream.Read(bb, 0, bb.Length);
                    FieldValues[y] = stream_DBFEncoding.GetString(bb).Trim().TrimEnd(new char[] { (char)0x00 });
                    recordData.Add(Fields_Name[y], FieldValues[y]);
                };

                if ((vline.numParts == 0) || (vline.numPoints == 0))
                {
                    string err1 = recordData[stream_ShapeFile_FieldNames.fldLinkId].ToString();
                    err1 = "Line with ROAD_ID " + err1 + " have no points!!!";
                    Console.WriteLine("ERROR " + err1);
                    Console.ReadLine();
                    throw new Exception(err1);
                };

                try
                {
                    OnReadLine(vline);

                    if (stream_ShapeFile_FieldNames.SOURCE == "GARMIN")
                        OnReadLine_0_GARMIN(vline, recordData);
                    else if (stream_ShapeFile_FieldNames.SOURCE == "OSM")
                        OnReadLine_1_OSM(vline, recordData);
                    else if ((stream_ShapeFile_FieldNames.SOURCE == "OSM2") || (stream_ShapeFile_FieldNames.SOURCE == "OSM2SHP"))
                        OnReadLine_2_OSM2SHP(vline, recordData);
                    else if (stream_ShapeFile_FieldNames.SOURCE == "WATER")
                        OnReadLine_3_WATER(vline, recordData);
                }
                catch (Exception ex)
                {
                    string pref = "";
                    if ((stream_ShapeFile_FieldNames.fldLinkId != null) && (stream_ShapeFile_FieldNames.fldLinkId != String.Empty)
                        && (recordData[stream_ShapeFile_FieldNames.fldLinkId] != null) && (recordData[stream_ShapeFile_FieldNames.fldLinkId].ToString() != null)
                          && (recordData[stream_ShapeFile_FieldNames.fldLinkId].ToString() != String.Empty))
                        pref = " [LINKID:" + recordData[stream_ShapeFile_FieldNames.fldLinkId].ToString() + "] ";
                    Console.WriteLine("ERROR" + pref + ex.ToString());
                    Console.ReadLine();
                    throw ex;
                };
                lines++;
            };
            UpdateCounter(true);

            if (!readAll)
                shapeFileStream.Close();
            dbfFileStream.Close();
        }

        private Dictionary<int, string> noturns_list = new Dictionary<int, string>();
        private void Read_OSM_ADDIT_DBF_NOTURN(string filename)
        {
            if (String.IsNullOrEmpty(stream_ShapeFile_FieldNames.fldOSM_ADDIT_DBF_NOTURN)) return;
            string noturn_file = Path.GetDirectoryName(filename).Trim('\\') + @"\" + stream_ShapeFile_FieldNames.fldOSM_ADDIT_DBF_NOTURN;

            if (File.Exists(noturn_file))
            {
                Console.WriteLine("Read {0} file", Path.GetFileName(noturn_file));
                DBFSharp.DBFFile dbff = new DBFSharp.DBFFile(noturn_file);                
                Console.Write(" .. 0/0");
                int rdd = 0;
                foreach (Dictionary<string, object> rec in dbff.ReadAllRecords())
                {
                    rdd++;
                    if (rdd % 100 == 0)
                    {
                        Console.SetCursorPosition(0, Console.CursorTop);
                        Console.Write(" .. {0}/{1} of turn restrictions", rdd, dbff.RecordsCount);
                    };
                    int ROAD_ID = int.Parse(rec["LINE_ID"].ToString());
                    string FL = rec["FL"].ToString();
                    noturns_list.Add(ROAD_ID, FL);
                };
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write(" .. {0}/{1} of turn restrictions", rdd, dbff.RecordsCount);
                Console.WriteLine();
                Console.WriteLine();
                dbff.Close();                
            };
        }

        // // Must be replace with (No Hashset in .Net 2.0)
        // HashSet<ulong> nodes_list = HashSet<ulong>();
        private Dictionary<ulong, byte> nodes_list = new Dictionary<ulong, byte>(); 
        private void Read_OSM_ADDIT_DBF_NODES(string filename)
        {
            if (String.IsNullOrEmpty(stream_ShapeFile_FieldNames.fldOSM_ADDIT_DBF_NODES)) return;
            string nodes_file = Path.GetDirectoryName(filename).Trim('\\') + @"\" + stream_ShapeFile_FieldNames.fldOSM_ADDIT_DBF_NODES;

            if (File.Exists(nodes_file))
            {
                Console.Write("Read {0} file ", Path.GetFileName(nodes_file));
                FileStream shapeNodesStream = new FileStream(nodes_file, FileMode.Open, FileAccess.Read);                
                long shapeNodesLength = shapeNodesStream.Length;

                bool readAll = shapeNodesLength < 1024 * 1024 * 250; // 250 MB
                if (readAll)
                    Console.Write("to Memory ... ");
                else
                    Console.Write("... ");
                Console.WriteLine("{0}", GetFileSize(shapeNodesLength));

                byte[] shapeNodesData = new byte[readAll ? shapeNodesLength : 100];//new Byte[shapeFileLength];
                shapeNodesStream.Read(shapeNodesData, 0, shapeNodesData.Length);
                if (readAll) shapeNodesStream.Close();
                
                string dbf_file = nodes_file.Replace(Path.GetExtension(nodes_file), ".dbf");
                DBFSharp.DBFFile dbff = new DBFSharp.DBFFile(dbf_file);

                int nodescount = (readIntBig(shapeNodesData, 24) * 2 - 100) / 28;
                int shapentype = readIntLittle(shapeNodesData, 32);
                
                if (shapentype == 1)
                {
                    Console.Write(" .. 0/0");

                    int readCount = 0;                                        
                    long currentPosition = 100;                                
                    
                    unsafe
                    {
                        ulong xy = 0;
                        ulong* pxy = &xy;
                        float* px = (float*)&xy;
                        float* py = px + 1;
                        
                        while (currentPosition < shapeNodesLength)
                        {                            
                            int offset = 0;
                            if (!readAll)
                            {
                                shapeNodesData = new byte[28]; // Standard size of Point Record
                                shapeNodesStream.Read(shapeNodesData, 0, shapeNodesData.Length);                                
                            }
                            else
                            {
                                offset = (int)currentPosition;                                
                            };

                            // Read Shape
                            //int rn   = readIntBig(shapeNodesData, offset);              // Record Number
                            //int cl   = readIntBig(shapeNodesData, offset + 4);          // Content Length
                            //int t    = readIntLittle(shapeNodesData, offset + 8);       // Shape Type
                            double x = readDoubleLittle(shapeNodesData, offset + 8 + 4);  // X Coordinate
                            double y = readDoubleLittle(shapeNodesData, offset + 8 + 12); // Y Coordinate

                            *px = (float)x;
                            *py = (float)y;

                            // Read DBF
                            Dictionary<string, object> dbfrec = dbff.ReadNext();
                            ulong NODE_ID = ulong.Parse(dbfrec["NODE_ID"].ToString());
                            int L_COUNT = int.Parse(dbfrec["L_COUNT"].ToString());
                            int ML_COUNT = int.Parse(dbfrec["ML_COUNT"].ToString());
                            bool add = (ML_COUNT > 1) || ((L_COUNT > 1) && (ML_COUNT > 0));       

                            // KEEP
                            if (add)
                            {
                                if (!nodes_list.ContainsKey(xy))
                                    nodes_list.Add(xy, 0);
                                else
                                    nodes_list[xy]++;
                            };

                            currentPosition += 28; //  (4 + cl) * 2;
                            readCount++;

                            if (readCount % 5000 == 0)
                            {
                                Console.SetCursorPosition(0, Console.CursorTop);
                                Console.Write(" .. {0}/{1} of nodes", readCount, nodescount);
                            };
                        };
                    };
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write(" .. {0}/{1} of nodes", readCount, nodescount);
                    Console.WriteLine();                    
                };
                shapeNodesStream.Close();
                dbff.Close();

                Console.Write("Found {0} Nodes to Split Lines", nodes_list.Count);
                Console.WriteLine(" - in mem " + GetFileSize(30 * nodes_list.Count));
                Console.WriteLine();
            };
        }

        private void UpdateCounter(bool always)
        {
            if (always || (analyse_LinesCount == 1) || (analyse_LinesCount % 50 == 0))
            {
                Console.SetCursorPosition(0, Console.CursorTop - 2);
                ConsoleColor cc = Console.ForegroundColor;
                if (analyse_LinesDCount == analyse_LinesCount)
                {
                    Console.Write("Lines");
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.Write("/Segm");
                    Console.ForegroundColor = cc;
                    Console.Write(" Nodes/NoTurns: ");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write(String.Format("{0}", analyse_LinesCount));
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.Write(String.Format("/{0}", analyse_SegmentsCount));
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(String.Format(" {0}/{1}", analyse_Nodes.Count, analyse_NoTurns.Count));
                }
                else
                {
                    Console.Write("Lines");
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.Write("/Splitted/Segm");
                    Console.ForegroundColor = cc;
                    Console.Write(" Nodes/NoTurns: ");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write(String.Format("{0}", analyse_LinesCount));
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.Write(String.Format("/{0}/{1}",analyse_LinesDCount, analyse_SegmentsCount));
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(String.Format(" {0}/{1}",analyse_Nodes.Count, analyse_NoTurns.Count));
                };
                Console.ForegroundColor = cc;
                TimeSpan elapsed = DateTime.Now.Subtract(started);
                DateTime end = started.AddSeconds(elapsed.TotalSeconds / (double)analyse_LinesCount * (double)total_lines);
                TimeSpan more = end.Subtract(DateTime.Now);
                Console.Write("Shape read ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:P}", (float)analyse_LinesCount / (float)total_lines));
                Console.ForegroundColor = cc;
                Console.Write(", elapsed ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", (int)elapsed.TotalHours, elapsed.Minutes, elapsed.Seconds));
                Console.ForegroundColor = cc;
                Console.Write(", end at ");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write(String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:HH:mm dd.MM}", end));
                Console.ForegroundColor = cc;
                Console.Write(" - ");
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write(String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", (int)more.TotalHours, more.Minutes, more.Seconds));
                Console.ForegroundColor = cc;
                Console.WriteLine(" more    ");
            };
        }

        /// <summary>
        ///     Исп-ся для string-->float
        /// </summary>
        private System.Globalization.CultureInfo stream_CultureInfo = System.Globalization.CultureInfo.InstalledUICulture;
        /// <summary>
        ///     Исп-ся для string-->float
        /// </summary>
        private System.Globalization.NumberFormatInfo stream_NumberFormat;

        private void OnReadLine(TLine vline)
        {
            if (vline.box[0] < this.bounds_box[0]) this.bounds_box[0] = vline.box[0];
            if (vline.box[1] < this.bounds_box[1]) this.bounds_box[1] = vline.box[1];
            if (vline.box[2] > this.bounds_box[2]) this.bounds_box[2] = vline.box[2];
            if (vline.box[3] > this.bounds_box[3]) this.bounds_box[3] = vline.box[3];
        }

        // Get ROAD_ID as OSM_ID (WAY_ID) or LINK_ID
        private int GetRoadID(ref Hashtable data, string fldName, string idName)
        {
            try
            {
                string idd = data[fldName].ToString();
                if (idd.IndexOf(".") > 0) idd = idd.Substring(0, idd.IndexOf("."));
                return int.Parse(idd); // LINK_ID or OSM_ID
            }
            catch
            {
                try
                {
                    Console.WriteLine("ERROR: " + idName + " `" + data[fldName].ToString() + "` READ ERROR");
                }
                catch
                {
                    Console.WriteLine("ERROR: " + idName + " READ ERROR");
                };
                Console.ReadLine();
                throw new Exception(idName + " READ ERROR");
            };
        }

        // Get String Parameter from FieldName
        private string GetStringID(ref Hashtable data, string fldName, string idName)
        {
            try
            {
                if (String.IsNullOrEmpty(fldName) == false && data.ContainsKey(fldName))
                    return data[fldName].ToString().ToUpper();
            }
            catch
            {
                Console.WriteLine("ERROR: " + idName + " READ ERROR");
                Console.ReadLine();
                throw new Exception(idName + "OSM_TYPE READ ERROR");
            };
            return null;
        }

        // REQ: ONE_WAY, SPD_LIMIT, HWY, LINK_ID, LEN, TURN_RSTRS
        /// <summary>
        ///     Вызываем для каждой линии shp файла
        /// </summary>
        /// <param name="line">линия</param>
        /// <param name="data">поля DBF</param>
        private void OnReadLine_0_GARMIN(TLine vline, Hashtable data)
        {
            // Inverse Latitude
            if (stream_ShapeFile_FieldNames.InverseLat)
                for (int i = 0; i < vline.points.Length; i++) vline.points[i].Y *= -1;
            // Inverse Longitude
            if (stream_ShapeFile_FieldNames.InverseLon)
                for (int i = 0; i < vline.points.Length; i++) vline.points[i].X *= -1;            

            analyse_LinesCount++; // счетчик линий в файле
            analyse_LinesDCount++; // счетчик линий в файле
            analyse_SegmentsCount += vline.points.Length - 1; // счетчик сегментов линий в файле

            // Main Fields
            uint LINE = (uint)analyse_LinesCount;
            int ROAD_ID = GetRoadID(ref data, stream_ShapeFile_FieldNames.fldLinkId, "LINK_ID");            
            string GRMN_TYPE = data[stream_ShapeFile_FieldNames.fldGarminType].ToString();

            // Addit Fields
            string onewayvalue = data[stream_ShapeFile_FieldNames.fldOneWay].ToString().Trim();
            bool one_way = (onewayvalue == "yes") || (onewayvalue == "1") || (onewayvalue == "T"); // одностороннее движение
            bool roundabout = GRMN_TYPE == "ROUNDABOUT";

            // Speed Fields
            int ROUTE_SPEED = 0;            
            if ((stream_ShapeFile_FieldNames.fldRouteSpeed != null) && (stream_ShapeFile_FieldNames.fldRouteSpeed != String.Empty))
            {
                try
                {
                    string rsp = data[stream_ShapeFile_FieldNames.fldRouteSpeed].ToString();
                    if (rsp.Length > 0)
                        ROUTE_SPEED = int.Parse(rsp); // скорость на участке            
                }
                catch { Console.WriteLine("ERROR: ROUTE SPEED READ ERROR"); Console.ReadLine(); throw new Exception("ROUTE SPEED READ ERROR"); };
            };
            int SPEED_LIMIT = 60; 
            try
            {
                string spddlmt = data[stream_ShapeFile_FieldNames.fldSpeedLimit].ToString();
                if (spddlmt.IndexOf(".") > 0) spddlmt = spddlmt.Substring(0, spddlmt.IndexOf("."));
                SPEED_LIMIT = int.Parse(spddlmt); // ограничение скорости на участке            
            }
            catch { 
                Console.WriteLine("ERROR: SPEED LIMIT READ ERROR"); 
                Console.ReadLine(); 
                throw new Exception("SPEED LIMIT READ ERROR"); 
            };            

            // Recalculate Speed            
            if ((GRMN_TYPE == "UNPAVED_ROAD") && (SPEED_LIMIT > 30)) SPEED_LIMIT = 30;

            // Normalize Speed
            int speed_normal = ROUTE_SPEED;
            if (speed_normal == 0)
            {
                speed_normal = SPEED_LIMIT > 70 ? SPEED_LIMIT - 15 : SPEED_LIMIT - 10; // скорость движения на участке
                if (roundabout && (SPEED_LIMIT > 40)) SPEED_LIMIT = 40;
                if (speed_normal <= 0) speed_normal = 3; // если скорость отриц. или ноль - 3 км/ч (грунтовка)
            };
            if ((GRMN_TYPE.IndexOf("HWY") >= 0) && (SPEED_LIMIT < 110)) SPEED_LIMIT = 110; // магистраль/шоссе                        

            OnReadLine_Calculations(ref data, ref vline, speed_normal, SPEED_LIMIT, LINE, ROAD_ID, 0, vline.numPoints - 1, one_way, roundabout, GRMN_TYPE, null, null, null);
            UpdateCounter(false);
        }

        /// <summary>
        ///     Вызываем для каждой линии shp файла
        /// </summary>
        /// <param name="line">линия</param>
        /// <param name="data">поля DBF</param>
        private void OnReadLine_1_OSM(TLine vline, Hashtable data)
        {
            // Inverse Latitude
            if (stream_ShapeFile_FieldNames.InverseLat)
                for (int i = 0; i < vline.points.Length; i++) vline.points[i].Y *= -1;
            // Inverse Longitude
            if (stream_ShapeFile_FieldNames.InverseLon)
                for (int i = 0; i < vline.points.Length; i++) vline.points[i].X *= -1;            

            analyse_LinesCount++; // счетчик линий в файле
            analyse_LinesDCount++; // счетчик линий в файле
            analyse_SegmentsCount += vline.points.Length - 1; // счетчик сегментов линий в файле

            // Main Fields
            uint LINE = (uint)analyse_LinesCount;
            int ROAD_ID = GetRoadID(ref data, stream_ShapeFile_FieldNames.fldOSM_ID, "OSM_ID");
            string OSM_SURFACE = data[stream_ShapeFile_FieldNames.fldOSM_SURFACE].ToString();

            // Addit Fields
            string onewayvalue = data[stream_ShapeFile_FieldNames.fldOneWay].ToString().Trim();
            bool one_way = (onewayvalue == "yes") || (onewayvalue == "1") || (onewayvalue == "T"); // одностороннее движение

            // Speed Fields
            int SPEED_LIMIT = 60;
            try
            {
                string spddlmt = data[stream_ShapeFile_FieldNames.fldSpeedLimit].ToString();
                if (spddlmt.IndexOf(".") > 0) spddlmt = spddlmt.Substring(0, spddlmt.IndexOf("."));
                int.TryParse(spddlmt, out SPEED_LIMIT); // ограничение скорости на участке            
            }
            catch { Console.WriteLine("ERROR: SpeedLimit READ ERROR"); Console.ReadLine(); throw new Exception("SpeedLimit READ ERROR"); };

            // Recalculate Speed
            if ((OSM_SURFACE == "unpaved") && (SPEED_LIMIT > 30)) SPEED_LIMIT = 15;
            if ((OSM_SURFACE == "paving_stones") && (SPEED_LIMIT > 30)) SPEED_LIMIT = 30;
           
            // Normalize Speed
            int speed_normal = SPEED_LIMIT > 70 ? SPEED_LIMIT - 15 : SPEED_LIMIT - 10; // скорость движения на участке
            if (speed_normal <= 0) speed_normal = 3; // если скорость отриц. или ноль - 3 км/ч (грунтовка)

            OnReadLine_Calculations(ref data, ref vline, speed_normal, SPEED_LIMIT, LINE, ROAD_ID, 0, vline.numPoints - 1, one_way, false, null, null, OSM_SURFACE, null);
            UpdateCounter(false);
        }

        /// <summary>
        ///     Вызываем для каждой линии shp файла
        /// </summary>
        /// <param name="line">линия</param>
        /// <param name="data">поля DBF</param>
        private void OnReadLine_2_OSM2SHP(TLine vline, Hashtable data)
        {
            analyse_LinesCount++; // счетчик линий в файле

            if ((nodes_list == null) || (nodes_list.Count == 0)) // нет узлов в линиях
                OnReadLine_2_OSM2SHP_Splitted(vline, data, 0, vline.numPoints - 1);
            else
            {
                unsafe
                {
                    ulong xy = 0;
                    ulong* pxy = &xy;
                    float* px = (float*)&xy;
                    float* py = px + 1;

                    List<int> split_to = new List<int>();
                    for (int i = 1; i < vline.numPoints - 1; i++)
                    {
                        *px = (float)vline.points[i].X;
                        *py = (float)vline.points[i].Y;

                        if (nodes_list.ContainsKey(xy))
                            split_to.Add(i);
                    };
                    if (split_to.Count == 0) // нет узлов в линии
                        OnReadLine_2_OSM2SHP_Splitted(vline, data, 0, vline.numPoints - 1);
                    else
                    {
                        split_to.Insert(0, 0);
                        split_to.Add(vline.numPoints - 1);
                        for (int i = 1; i < split_to.Count; i++)
                        {
                            TLine l2 = new TLine();
                            l2.box = vline.box;
                            l2.numParts = 1;
                            l2.numPoints = split_to[i] - split_to[i-1] + 1;
                            l2.parts = new int[] { 0 };
                            l2.points = new PointF[l2.numPoints];
                            for (int y = split_to[i - 1]; y <= split_to[i]; y++)
                                l2.points[y - split_to[i - 1]] = vline.points[y];

                            OnReadLine_2_OSM2SHP_Splitted(l2, data, split_to[i - 1], split_to[i]);
                        };
                    };
                };
            };

            UpdateCounter(false);            
        }

        /// <summary>
        ///     Вызываем для каждого участка разделенной линии shp файла
        /// </summary>
        /// <param name="vline">линия</param>
        /// <param name="data">поля DBF</param>
        /// <param name="sFrom">индекс начальной точки</param>
        /// <param name="sTo">индекс конечной точки</param>
        private void OnReadLine_2_OSM2SHP_Splitted(TLine vline, Hashtable data, int sFrom, int sTo)
        {
            // Inverse Latitude
            if (stream_ShapeFile_FieldNames.InverseLat) for (int i = 0; i < vline.points.Length; i++) vline.points[i].Y *= -1;
            // Inverse Longitude
            if (stream_ShapeFile_FieldNames.InverseLon) for (int i = 0; i < vline.points.Length; i++) vline.points[i].X *= -1;

            analyse_LinesDCount++; // счетчик деленых линий в файле
            analyse_SegmentsCount += vline.points.Length - 1; // счетчик сегментов линий в файле

            // Main Fields
            uint LINE = (uint)analyse_LinesDCount;
            int ROAD_ID = GetRoadID(ref data, stream_ShapeFile_FieldNames.fldOSM_ID, "OSM_ID");
            string GRMN_TYPE = String.IsNullOrEmpty(stream_ShapeFile_FieldNames.fldGarminType) == false && data.ContainsKey(stream_ShapeFile_FieldNames.fldGarminType) ? data[stream_ShapeFile_FieldNames.fldGarminType].ToString().ToUpper() : "";
            string OSM_TYPE = GetStringID(ref data, stream_ShapeFile_FieldNames.fldOSM_TYPE, "OSM_TYPE");
            string OSM_SURFACE = GetStringID(ref data, stream_ShapeFile_FieldNames.fldOSM_SURFACE, "OSM_SURFACE");
            string OSM_SERVICE = GetStringID(ref data, stream_ShapeFile_FieldNames.fldOSM_ADDIT_SERVICE, "OSM_SERVICE");
            if (String.IsNullOrEmpty(OSM_TYPE)) return; // Not a Road
            
            // Addit Fields         
            string onewayvalue = data[stream_ShapeFile_FieldNames.fldOneWay].ToString().Trim();
            bool one_way = (onewayvalue == "yes") || (onewayvalue == "1") || (onewayvalue == "T"); // одностороннее движение
            bool roundabout = GRMN_TYPE == "ROUNDABOUT";
            try // ROUNDABOUT by OSM
            {
                if ((!String.IsNullOrEmpty(stream_ShapeFile_FieldNames.fldOSM_ADDIT_JUNCTION)) && data.ContainsKey(stream_ShapeFile_FieldNames.fldOSM_ADDIT_JUNCTION))
                    if (data[stream_ShapeFile_FieldNames.fldOSM_ADDIT_JUNCTION].ToString().ToUpper() == "ROUNDABOUT")
                        roundabout = true;
            }
            catch { };

            // Speed Fields, SPEED_LIMIT from MAXSPEED
            int SPEED_LIMIT = 60;
            bool SPEED_UP = false;
            try // MAXSPEED
            {
                if ((String.IsNullOrEmpty(stream_ShapeFile_FieldNames.fldSpeedLimit) == false) && data.ContainsKey(stream_ShapeFile_FieldNames.fldSpeedLimit))
                {
                    string spddlmt = data[stream_ShapeFile_FieldNames.fldSpeedLimit].ToString().ToUpper();
                    if (!String.IsNullOrEmpty(spddlmt))
                    {
                        if (spddlmt.IndexOf(".") > 0) spddlmt = spddlmt.Substring(0, spddlmt.IndexOf("."));
                        int sl = 0;
                        if (int.TryParse(spddlmt, out sl))
                        {
                            SPEED_LIMIT = sl; // ограничение скорости на участке            
                            SPEED_UP = true;
                        }
                        else
                        {
                            if (spddlmt == "RU:LIVING_STREET") { SPEED_LIMIT = 15; SPEED_UP = true; };
                            if (spddlmt == "RU:RURAL") { SPEED_LIMIT = 20; SPEED_UP = true; };
                            if (spddlmt == "RU:URBAN") { SPEED_LIMIT = 40; SPEED_UP = true; };      
                        };
                    };
                };
            }
            catch { };

            // Speed Fields, SPEED_LIMIT from MAXSPEED:PRACTICAL
            try // MAXSPEED:PRACTICAL
            {
                if ((!String.IsNullOrEmpty(stream_ShapeFile_FieldNames.fldOSM_ADDIT_MAXACTUAL)) && data.ContainsKey(stream_ShapeFile_FieldNames.fldOSM_ADDIT_MAXACTUAL))
                {
                    string spddlmt = data[stream_ShapeFile_FieldNames.fldOSM_ADDIT_MAXACTUAL].ToString().ToUpper();
                    if (!String.IsNullOrEmpty(spddlmt))
                    {
                        if (spddlmt.IndexOf(".") > 0) spddlmt = spddlmt.Substring(0, spddlmt.IndexOf("."));
                        int sl = 0;
                        if (int.TryParse(spddlmt, out sl))
                        {
                            SPEED_LIMIT = sl; // ограничение скорости на участке            
                            SPEED_UP = true;
                        }
                        else
                        {
                            if (spddlmt == "RU:LIVING_STREET") { SPEED_LIMIT = 15; SPEED_UP = true; };
                            if (spddlmt == "RU:RURAL") { SPEED_LIMIT = 20; SPEED_UP = true; };
                            if (spddlmt == "RU:URBAN") { SPEED_LIMIT = 40; SPEED_UP = true; };
                        };
                    };
                };
            }
            catch { };

            // Speed Fields, ROUTE_SPEED
            int ROUTE_SPEED = 0;
            if ((stream_ShapeFile_FieldNames.fldRouteSpeed != null) && (stream_ShapeFile_FieldNames.fldRouteSpeed != String.Empty))
            {
                try
                {
                    string rsp = data[stream_ShapeFile_FieldNames.fldRouteSpeed].ToString();
                    if (rsp.Length > 0) ROUTE_SPEED = int.Parse(rsp); // скорость на участке            
                }
                catch { };
            };

            // Recalculate Speed
            if (!SPEED_UP) // SPEED BY TYPE
            {
                switch (OSM_TYPE) // speed by OSM TYPE
                {
                    case "PATH": case "BRIDLEWAY": case "CYCLEWAY": case "FOOTWAY": case "STEPS": case "CROSSING": case "BUS_STOP": 
                    case "PEDESTRIAN": case "LIVING_STREET": case "BUS_GUIDEWAY": case "CORRIDOR": case "SIDEWALK":
                        SPEED_LIMIT = 3;
                        break;
                    case "REST_AREA": case "PLATFORM": case "CONSTRUCTION": case "PROPOSED":
                        SPEED_LIMIT = 5;
                        break;
                    case "PRIMARY_LINK": case "TERTIALY_LINK":
                        SPEED_LIMIT = 60;
                        break;
                    case "PRIMARY": case "TERTIALY":
                        SPEED_LIMIT = 80;
                        break;
                    case "SERVICE": case "SERVICES": case "BUSWAY":
                        SPEED_LIMIT = 15;
                        break;
                    case "ROAD": case "UNCLASSIFIED": case "TRACK":
                        SPEED_LIMIT = 20;
                        break;
                };
                if (!String.IsNullOrEmpty(OSM_SURFACE)) // speed by OSM SURFACE
                {
                    switch (OSM_SURFACE)
                    {
                        case "UNPAVED":
                        case "PAVED":
                        case "PAVING_STONES":
                        case "PUBBLESTONE":                        
                            if (SPEED_LIMIT > 15) SPEED_LIMIT = 15;
                            break;
                        case "MUD":
                        case "DIRT":
                        case "EARTH":
                        case "GROUND":
                        case "DIRT/SAND":
                        case "SAND":                        
                            SPEED_LIMIT = 20;
                            break;
                        case "GRAVEL":
                            SPEED_LIMIT = 30;
                            break;
                        case "FINE_GRAVEL":
                        case "WOOD":
                            SPEED_LIMIT = 40;
                            break;
                        case "GRASS":
                            SPEED_LIMIT = 15;
                            break;
                        case "METAL":
                            SPEED_LIMIT = 5;
                            break;
                        case "ASPHALT":
                            if (SPEED_LIMIT < 15) SPEED_LIMIT = 15;
                            break;
                    };
                };
                if (!String.IsNullOrEmpty(OSM_SERVICE)) // speed by OSM SERVICE
                {
                    switch (OSM_SERVICE)
                    {
                        case "ALLEY": 
                        case "DRIVEWAY":
                        case "PARKING_AISLE":
                        case "EMERGENCY_ACCESS":
                            SPEED_LIMIT = 5;
                            break;                        
                    };
                };
                if (!String.IsNullOrEmpty(GRMN_TYPE))  // speed by GARMIN TYPE
                {
                    switch (GRMN_TYPE)
                    {
                        case "ALLEY":
                        case "TRAIL":
                            SPEED_LIMIT = 5;
                            break;
                        case "UNPAVED_ROAD":
                        case "IMPROVED_UNPAVED_ROAD":
                            if (SPEED_LIMIT > 15) SPEED_LIMIT = 15;
                            break;

                    };
                };
            };

            if ((GRMN_TYPE.IndexOf("HWY") >= 0) && (SPEED_LIMIT < 110)) // speed by GRMN_TYPE
                SPEED_LIMIT = 110; // магистраль/шоссе 

            try
            {
                if ((!String.IsNullOrEmpty(stream_ShapeFile_FieldNames.fldOSM_ADDIT_LANES)) && data.ContainsKey(stream_ShapeFile_FieldNames.fldOSM_ADDIT_LANES))
                {
                    int lanes = int.Parse(data[stream_ShapeFile_FieldNames.fldOSM_ADDIT_LANES].ToString());
                    if ((lanes > 3) && (SPEED_LIMIT < 60)) SPEED_LIMIT = 60;
                    if ((lanes > 7) && (SPEED_LIMIT < 80)) SPEED_LIMIT = 80;
                };
            }
            catch { };
          
            if ((ROUTE_SPEED > 0) && (SPEED_LIMIT > ROUTE_SPEED)) ROUTE_SPEED = 0;

            // Normalize Speed
            int speed_normal = ROUTE_SPEED;
            if (speed_normal == 0)
            {
                speed_normal = SPEED_LIMIT > 70 ? SPEED_LIMIT - 15 : SPEED_LIMIT - 10; // скорость движения на участке
                if (roundabout && (SPEED_LIMIT > 40)) SPEED_LIMIT = 40;
                if (speed_normal <= 0) speed_normal = 3; // если скорость отриц. или ноль - 3 км/ч (грунтовка)
            };

            OnReadLine_Calculations(ref data, ref vline, speed_normal, SPEED_LIMIT, LINE, ROAD_ID, sFrom, sTo, one_way, roundabout, GRMN_TYPE, OSM_TYPE, OSM_SURFACE, OSM_SERVICE);
        }

        /// <summary>
        ///     Вызываем для каждой линии shp файла
        /// </summary>
        /// <param name="line">линия</param>
        /// <param name="data">поля DBF</param>
        private void OnReadLine_3_WATER(TLine vline, Hashtable data)
        {
            // Inverse Latitude
            if (stream_ShapeFile_FieldNames.InverseLat)
                for (int i = 0; i < vline.points.Length; i++) vline.points[i].Y *= -1;
            // Inverse Longitude
            if (stream_ShapeFile_FieldNames.InverseLon)
                for (int i = 0; i < vline.points.Length; i++) vline.points[i].X *= -1;

            analyse_LinesCount++; // счетчик линий в файле
            analyse_LinesDCount++; // счетчик линий в файле
            analyse_SegmentsCount += vline.points.Length - 1; // счетчик сегментов линий в файле

            // Main Fields
            uint LINE = (uint)analyse_LinesCount;
            int ROAD_ID = GetRoadID(ref data, stream_ShapeFile_FieldNames.fldOSM_ID, "OSM_ID");

            string onewayvalue = ""; try { onewayvalue = data[stream_ShapeFile_FieldNames.fldOneWay].ToString().Trim(); } catch { };
            bool one_way = (onewayvalue == "yes") || (onewayvalue == "1") || (onewayvalue == "T"); // одностороннее движение

            // Speed Fields
            int SPEED_LIMIT = 48; // 26 knots
            try
            {
                string spddlmt = data[stream_ShapeFile_FieldNames.fldSpeedLimit].ToString();
                if (spddlmt.IndexOf(".") > 0) spddlmt = spddlmt.Substring(0, spddlmt.IndexOf("."));
                int.TryParse(spddlmt, out SPEED_LIMIT); // ограничение скорости на участке            
            }
            catch { };

            int ROUTE_SPEED = 0;
            try
            {
                string routespd = data[stream_ShapeFile_FieldNames.fldRouteSpeed].ToString();
                if (routespd.IndexOf(".") > 0) routespd = routespd.Substring(0, routespd.IndexOf("."));
                int.TryParse(routespd, out ROUTE_SPEED); // ограничение скорости на участке            
            }
            catch { };
            

            /* CHECK ROUTE SPEED */
            if (data.ContainsKey("AMENITY"))
            {
                string rv = data["AMENITY"] == null ? "" : data["AMENITY"].ToString().ToLower();
                if ((!String.IsNullOrEmpty(rv)) && (rv == "ferry_terminal")) ROUTE_SPEED = 3;
            };
            if (data.ContainsKey("MAN_MAID"))
            {
                string rv = data["MAN_MAID"] == null ? "" : data["MAN_MAID"].ToString().ToLower();
                if ((!String.IsNullOrEmpty(rv)) && (rv == "pier")) ROUTE_SPEED = 3;
            };
            if (data.ContainsKey("HARBOUR"))
            {
                string rv = data["HARBOUR"] == null ? "" : data["HARBOUR"].ToString().ToLower();
                if ((!String.IsNullOrEmpty(rv)) && (rv == "yes")) ROUTE_SPEED = 3;
            };
            if (data.ContainsKey("INDUSTRIAL"))
            {
                string rv = data["INDUSTRIAL"] == null ? "" : data["INDUSTRIAL"].ToString().ToLower();
                if ((!String.IsNullOrEmpty(rv)) && (rv == "port")) ROUTE_SPEED = 3;
            };            

            /* Normalize Speed */
            if(ROUTE_SPEED == 0)
            {
                ROUTE_SPEED = SPEED_LIMIT > 26 ? SPEED_LIMIT - 15 : SPEED_LIMIT - 10;
                if (ROUTE_SPEED <= 0) ROUTE_SPEED = 5;
            };

            OnReadLine_Calculations(ref data, ref vline, ROUTE_SPEED, SPEED_LIMIT, LINE, ROAD_ID, 0, vline.numPoints - 1, one_way, false, null, "WATERWAY", null, null);
            UpdateCounter(false);
        }

        /// <summary>
        ///     Анилиз участка и его сохранение
        /// </summary>
        /// <param name="data">поля DBF</param>
        /// <param name="vline">линия</param>
        /// <param name="speed_normal"></param>
        /// <param name="SPEED_LIMIT"></param>
        /// <param name="LINE"></param>
        /// <param name="ROAD_ID"></param>
        /// <param name="sFrom"></param>
        /// <param name="sTo"></param>
        /// <param name="one_way"></param>
        /// <param name="roundabout"></param>
        /// <param name="GRMN_TYPE"></param>
        /// <param name="OSM_TYPE"></param>
        /// <param name="OSM_SURFACE"></param>
        /// <param name="OSM_SERVICE"></param>
        private void OnReadLine_Calculations(ref Hashtable data, ref TLine vline, int speed_normal, int SPEED_LIMIT, uint LINE, int ROAD_ID, int sFrom, int sTo,
            bool one_way, bool roundabout, string GRMN_TYPE, string OSM_TYPE, string OSM_SURFACE, string OSM_SERVICE)
        {
            float dist = CheckLineDist(ref data, ref vline);
            float lat1 = vline.points[0].Y;
            float lon1 = vline.points[0].X;
            float lat2 = vline.points[vline.points.Length - 1].Y;
            float lon2 = vline.points[vline.points.Length - 1].X;

            uint sn = NodeByLatLon(lat1, lon1); // начальный узел
            if (sn == 0) sn = AddNode(lat1, lon1);
            uint en = NodeByLatLon(lat2, lon2); // конечный узел            
            if (en == 0) en = AddNode(lat2, lon2);
            
            if (dist > analyse_maxLengthBetweenNodes) analyse_maxLengthBetweenNodes = dist; // макс длина участка
            if (dist < const_maxError) dist = const_maxError + (float)0.00001; // нужно для корректного расчета

            float time = dist / 1000 / (float)speed_normal * 60; // время движения по участку в минутах
            float cost = dist / 1000 / (float)SPEED_LIMIT * 60; // оценочное время движения по участку в минутах
            if (time > analyse_maxTimeBetweenNodes) analyse_maxTimeBetweenNodes = time;
            if (cost > analyse_maxCostBetweenNodes) analyse_maxCostBetweenNodes = cost;

            // Turn Restrictions // запреты поворотов
            CheckTurnRestrictions(ref data, LINE, sn, en, ROAD_ID, sFrom, sTo);                        

            analyse_Nodes[(int)sn - 1].AddLink(en, cost, dist, time, LINE, false); // доб. исх. связь в нач. узел
            analyse_Nodes[(int)en - 1].AddRLink(sn, cost, dist, time, LINE, false); // доб. вх. связь в кон. узел
            if (!one_way)
            {
                analyse_Nodes[(int)en - 1].AddLink(sn, cost, dist, time, LINE, true); // доб. исх. связь в кон. узел
                analyse_Nodes[(int)sn - 1].AddRLink(en, cost, dist, time, LINE, true); // доб. вх. связь в нач. узел
            };

            // tmc код
            ushort tmc_code = CheckTMCCode(ref data);

            // ++АТРИБУТИВНЫЕ ДАННЫЕ ЛИНИЙ
            bool hasattrib = CheckHasAttributes(ref data, GRMN_TYPE, SPEED_LIMIT, OSM_TYPE, OSM_SURFACE, OSM_SERVICE);            
            // --АТРИБУТИВНЫЕ ДАННЫЕ ЛИНИЙ

            byte[] seg = BitConverter.GetBytes((ushort)(vline.points.Length - 1)); // сегментов в линии
            byte[] pos = BitConverter.GetBytes((int)stream_LinesSegments.Position); // позиция первого сегмента линии в файле сегментов
            byte one = (byte)(0 +
                (one_way ? (byte)1 : (byte)0) + // односторонка ?
                (roundabout ? (byte)2 : (byte)0) + // круговое ?
                (tmc_code > 0 ? (byte)4 : (byte)0) + // tmc code exist ?
                (hasattrib ? (byte)8 : (byte)0) // есть ли атрибуты у линии ?
                );
            byte[] nd1 = BitConverter.GetBytes(sn); // нач. уз.
            byte[] nd2 = BitConverter.GetBytes(en); // кон. уз.

            // пишем линию в файл
            stream_Lines.Write(seg, 0, seg.Length);
            stream_Lines.Write(pos, 0, pos.Length);
            stream_Lines.WriteByte(one);
            stream_Lines.Write(nd1, 0, nd1.Length);
            stream_Lines.Write(nd2, 0, nd2.Length);
            stream_Lines.Flush();

            byte[] linkid = BitConverter.GetBytes(ROAD_ID); // LINK_ID or OSM_ID or ROAD_ID
            stream_LineIndex.Write(linkid, 0, linkid.Length); // пишем LINK_ID or OSM_ID or ROAD_ID                  

            // пишем lines names
            if ((stream_ShapeFile_FieldNames.fldName != null) && (stream_ShapeFile_FieldNames.fldName != String.Empty) && (writeLinesNamesFile))
            {
                string nam = data[stream_ShapeFile_FieldNames.fldName].ToString();
                byte[] btw = System.Text.Encoding.GetEncoding(1251).GetBytes(nam + "\r\n");
                stream_Names.Write(btw, 0, btw.Length);
            };

            // сохраняем RGNode
            if ((stream_ShapeFile_FieldNames.fldRGNODE != null) && (stream_ShapeFile_FieldNames.fldRGNODE != String.Empty))
            {
                string val = data[stream_ShapeFile_FieldNames.fldRGNODE].ToString();
                if (val.Length > 0)
                {
                    bool inner = val.IndexOf("I") >= 0;
                    bool outer = val.IndexOf("O") >= 0;
                    bool begin = val.IndexOf("F") >= 0;
                    int len = 1;
                    int id = 0; int number = 0;
                    try
                    {
                        while (int.TryParse(val.Substring(1, len++), out id)) number = id;
                    }
                    catch { Console.WriteLine("ERROR: RGNODE READ ERROR"); Console.ReadLine(); throw new Exception("RGNODE READ ERROR"); };
                    if (number == 0)
                    {
                        Console.WriteLine("ERROR: RGNODE COULDN'T BE ZERO(0) - ROAD_ID " + ROAD_ID.ToString());
                        Console.ReadLine();
                        throw new Exception("RGNODE READ ERROR");
                    };
                    TRGNode rgn = new TRGNode(begin ? sn : en, inner, outer, number, val, begin ? lat1 : lat2, begin ? lon1 : lon2);
                    analyse_RGNodes.Add(rgn);
                };
            };

            // пишем сегменты линии
            byte[] lno = BitConverter.GetBytes(LINE); // номер линии
            for (int i = 1; i < vline.points.Length; i++)
            {
                byte[] sno = BitConverter.GetBytes((ushort)i);
                byte[] lt0 = BitConverter.GetBytes((Single)vline.points[i - 1].Y);
                byte[] ln0 = BitConverter.GetBytes((Single)vline.points[i - 1].X);
                byte[] lt1 = BitConverter.GetBytes((Single)vline.points[i].Y);
                byte[] ln1 = BitConverter.GetBytes((Single)vline.points[i].X);
                float k = ((Single)vline.points[i].X - (Single)vline.points[i - 1].X) / (vline.points[i].Y - vline.points[i - 1].Y);
                byte[] ka = BitConverter.GetBytes((Single)k);
                float b = vline.points[i - 1].X - k * vline.points[i - 1].Y;
                byte[] ba = BitConverter.GetBytes((Single)b);

                stream_LinesSegments.Write(lno, 0, lno.Length);
                stream_LinesSegments.Write(sno, 0, sno.Length);
                stream_LinesSegments.Write(lt0, 0, lt0.Length);
                stream_LinesSegments.Write(ln0, 0, ln0.Length);
                stream_LinesSegments.Write(lt1, 0, lt1.Length);
                stream_LinesSegments.Write(ln1, 0, ln1.Length);
                stream_LinesSegments.Write(ka, 0, ka.Length);
                stream_LinesSegments.Write(ba, 0, ba.Length);
                stream_LinesSegments.Flush();
            };            
        }

        /// <summary>
        ///     Длина линии
        /// </summary>
        /// <param name="data"></param>
        /// <param name="line"></param>
        /// <returns></returns>
        private float CheckLineDist(ref Hashtable data, ref TLine line)
        {
            float dist = 0; // длина участка в метрах
            if ((stream_ShapeFile_FieldNames.fldLength != null) && (stream_ShapeFile_FieldNames.fldLength != String.Empty))
                dist = float.Parse(data[stream_ShapeFile_FieldNames.fldLength].ToString(), stream_NumberFormat); // faster // read from DBF
            else
                for (int i = 1; i < line.points.Length; i++) // slower // calculate
                {
                    float _lat1 = line.points[i - 1].Y;
                    float _lon1 = line.points[i - 1].X;
                    float _lat2 = line.points[i].Y;
                    float _lon2 = line.points[i].X;
                    dist += GetLengthMeters(_lat1, _lon1, _lat2, _lon2, false);
                };
            return dist;
        }

        /// <summary>
        ///     Проверка на наличие запретов поворота
        /// </summary>
        /// <param name="data"></param>
        /// <param name="LINE"></param>
        /// <param name="sn"></param>
        /// <param name="en"></param>
        /// <param name="ROAD_ID"></param>
        /// <param name="sFrom"></param>
        /// <param name="sTo"></param>
        private void CheckTurnRestrictions(ref Hashtable data, uint LINE, uint sn, uint en, int ROAD_ID, int sFrom, int sTo)
        {
            // Turn Restrictions // запреты поворотов из основного DBF файла
            if (!stream_ShapeFile_FieldNames.NoTurnRestrictions)
            {
                string tr = data[stream_ShapeFile_FieldNames.fldTurnRstr].ToString();
                if (tr.Length > 0)
                {
                    // FORMAT:L0001;F0002;L003:01100111;F8976:0100111; or M.....[..]  M0000001[01]
                    // M....[..] == Middle WAY_ID [PointNum]; where PointNumber is zero-indexed
                    string[] restrictions = tr.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string r in restrictions)
                        CheckTurnRestriction(r, LINE, sn, en, ROAD_ID, sFrom, sTo);
                };
            };

            // No turns from additional dbf file
            if (noturns_list.Count > 0)
            {
                if (noturns_list.ContainsKey(ROAD_ID))
                {
                    string tr = noturns_list[ROAD_ID];
                    // FORMAT:L0001;F0002;L003:01100111;F8976:0100111; or M.....[..]  M0000001[01]
                    // M....[..] == Middle WAY_ID [PointNum]; where PointNumber is zero-indexed
                    string[] restrictions = tr.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string r in restrictions)
                        CheckTurnRestriction(r, LINE, sn, en, ROAD_ID, sFrom, sTo);
                };
            };
        }

        /// <summary>
        ///     Разбор запрета поворота
        /// </summary>
        /// <param name="restriction"></param>
        /// <param name="LINE"></param>
        /// <param name="sn"></param>
        /// <param name="en"></param>
        /// <param name="ROAD_ID"></param>
        /// <param name="sFrom"></param>
        /// <param name="sTo"></param>
        private void CheckTurnRestriction(string restriction, uint LINE, uint sn, uint en, int ROAD_ID, int sFrom, int sTo)
        {
            // FORMAT:L0001;F0002;L003:01100111;F8976:0100111; or M.....[..]  M0000001[01]
            // M....[..] == Middle WAY_ID [PointNum]; where PointNumber is zero-indexed

            string rr = restriction;
            if (rr.IndexOf(":") > 0) rr = rr.Substring(0, rr.IndexOf(":"));
            if (rr.StartsWith("M")) // middle point
            {
                string[] qq = rr.Split(new char[] { 'M', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
                if (qq.Length > 1)
                {
                    int lineTo_linkId = 0;
                    int point_id = 0;
                    try
                    {
                        lineTo_linkId = int.Parse(qq[0]);
                        point_id = int.Parse(qq[1]);
                    }
                    catch { throw new Exception("Parse Turn Restriction M Error: " + restriction + " ROAD_ID: " + ROAD_ID.ToString()); };
                    if (point_id == sFrom) analyse_NoTurns.Add(new TurnRestriction(LINE, sn, lineTo_linkId));
                    if (point_id == sTo) analyse_NoTurns.Add(new TurnRestriction(LINE, en, lineTo_linkId));
                };
            }
            else
            {
                bool fromStart = rr.Substring(0, 1) == "F";
                int lineTo_linkId = 0;
                try
                {
                    lineTo_linkId = int.Parse(rr.Substring(1));
                }
                catch { throw new Exception("Parse Turn Restriction FL Error: " + restriction + " ROAD_ID: " + ROAD_ID.ToString()); };
                if (fromStart)
                    analyse_NoTurns.Add(new TurnRestriction(LINE, sn, lineTo_linkId));
                else
                    analyse_NoTurns.Add(new TurnRestriction(LINE, en, lineTo_linkId));
            };
        }

        /// <summary>
        ///     Получение TMC кода
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private ushort CheckTMCCode(ref Hashtable data)
        {
            ushort tmc_code = 0;
            if ((stream_ShapeFile_FieldNames.fldTMC != null) && (stream_ShapeFile_FieldNames.fldTMC != String.Empty)) // пишем TMC
            {
                // @E0+000+00000;@E0-000-00000;
                // @E0+000+00000@E0-000-00000;
                string tmc = data[stream_ShapeFile_FieldNames.fldTMC].ToString();
                bool inverse = false;
                if (tmc.Length > 10)
                {
                    tmc = tmc.Split(new string[] { ";", "@" }, StringSplitOptions.RemoveEmptyEntries)[0];
                    inverse = (tmc.IndexOf("+") > 0) && (tmc.IndexOf("-") > 0);
                    tmc_code = (ushort)(int.Parse(tmc.Substring(7)));
                };
                stream_TMC.WriteByte(inverse ? (byte)1 : (byte)0);
                byte[] btw = BitConverter.GetBytes(tmc_code);
                stream_TMC.Write(btw, 0, btw.Length);
            };
            return tmc_code;
        }

        /// <summary>
        ///     Получение атрибутивной информации о линии
        /// </summary>
        /// <param name="data"></param>
        /// <param name="GRMN_TYPE"></param>
        /// <param name="SPEED_LIMIT"></param>
        /// <param name="OSM_TYPE"></param>
        /// <param name="OSM_SURFACE"></param>
        /// <param name="OSM_SERVICE"></param>
        /// <returns></returns>
        private bool CheckHasAttributes(ref Hashtable data, string GRMN_TYPE, int SPEED_LIMIT, string OSM_TYPE, string OSM_SURFACE, string OSM_SERVICE)
        {
            bool hasattrib = false;
            byte[] attrib = new byte[16];

            if (!String.IsNullOrEmpty(GRMN_TYPE))
            {
                // BYTE[0] 0x01 - Дворовый проезд / Жилая зона (5.21)
                if (((GRMN_TYPE == "ALLEY") || (GRMN_TYPE == "DRIVEWAY")) && (SPEED_LIMIT == 5)) { SetRoadAttributesData(ref attrib, 0, 0x01); hasattrib = true; };
                // BYTE[0] 0x02 - Грунтовая дорога / Дорога без покрытия GARMIN_TYPE = 
                if (GRMN_TYPE == "UNPAVED_ROAD") { SetRoadAttributesData(ref attrib, 0, 0x02); hasattrib = true; };
                // BYTE[0] 0x08 - Дорога отсыпанная гравием (1.16)
                if (GRMN_TYPE == "IMPROVED_UNPAVED_ROAD") { SetRoadAttributesData(ref attrib, 0, 0x08); hasattrib = true; };
                // BYTE[1] 0x04 - Паром / переправа
                if (GRMN_TYPE == "FERRY") { SetRoadAttributesData(ref attrib, 1, 0x04); hasattrib = true; };
                // BYTE[2] 0x04 - Автомагистраль (5.1)
                if ((GRMN_TYPE == "MAJOR_HWY") && (SPEED_LIMIT >= 110)) { SetRoadAttributesData(ref attrib, 2, 0x04); hasattrib = true; };
            };

            if (!String.IsNullOrEmpty(OSM_TYPE))
            {
                // https://wiki.openstreetmap.org/wiki/OSM_tags_for_routing/Access_restrictions#Worldwide
                // https://wiki.openstreetmap.org/wiki/Key:highway
                switch (OSM_TYPE)
                {
                    // BYTE[4] 0x80 - WATER WAYS
                    case "WATERWAY" :
                        SetRoadAttributesData(ref attrib, 4, 0x80); hasattrib = true; break;
                    // BYTE[0] 0x01 - Дворовый проезд / Жилая зона (5.21) + BYTE[4] 0x08 - Дороги для пешеходов (OSM Pedestrian)
                    case "PEDESTRIAN":
                    case "FOOTWAY":
                    case "STEPS":
                        SetRoadAttributesData(ref attrib, 0, 0x01); SetRoadAttributesData(ref attrib, 4, 0x08); hasattrib = true; break;
                    // BYTE[4] 0x08 - Дороги для пешеходов (OSM Pedestrian)
                    case "CORRIDOR":
                    case "PLATFORM":
                    case "BRIDLEWAY":
                    case "CYCLEWAY":
                    case "SIDEWALK":
                    case "CROSSING":
                        SetRoadAttributesData(ref attrib, 4, 0x08); hasattrib = true; break;
                    // BYTE[0] 0x01 - Дворовый проезд / Жилая зона (5.21)
                    case "LIVING_STREET":
                        SetRoadAttributesData(ref attrib, 0, 0x01); hasattrib = true; break;
                    // BYTE[0] 0x20 Временная, 3x08 Дорожные работы
                    case "CONSTRUCTION":
                    case "PROPOSED":
                        SetRoadAttributesData(ref attrib, 0, 0x20); SetRoadAttributesData(ref attrib, 3, 0x08); hasattrib = true; break;
                    // BYTE[2] 0x02 Дорога для автомобилей (Знак 5.3)
                    case "MOTORWAY":
                    case "MOTORWAY_LINK":
                        SetRoadAttributesData(ref attrib, 2, 0x02); hasattrib = true; break;
                    // BYTE[0] 0x02 - Грунтовая дорога / Дорога без покрытия
                    case "ROAD":
                    case "UNCLASSIFIED":
                    case "TRACK":
                    case "PATH":
                        SetRoadAttributesData(ref  attrib, 0, 0x02); hasattrib = true; break;
                };
            };

            if (!String.IsNullOrEmpty(OSM_SURFACE))
            {
                switch (OSM_SURFACE)
                {
                    // BYTE[0] 0x02 - Грунтовая дорога / Дорога без покрытия
                    case "UNPAVED":
                    case "PAVED":
                    case "MUD":
                    case "DIRT":
                    case "EARTH":
                    case "GROUND":
                    case "WOOD":
                    case "GRASS":
                        SetRoadAttributesData(ref  attrib, 0, 0x02); hasattrib = true; break;
                    // BYTE[0] 0x08 - Дорога отсыпанная гравием
                    case "PAVING_STONES":
                    case "GRAVEL":
                    case "FINE_GRAVEL":
                    case "PUBBLESTONE":
                        SetRoadAttributesData(ref  attrib, 0, 0x08); hasattrib = true; break;
                    // BYTE[0] 0x10 - Песок
                    case "SAND":
                        SetRoadAttributesData(ref attrib, 0, 0x10); hasattrib = true; break;
                    // BYTE[0] 0x10 - Песок + BYTE[0] 0x02 - Грунтовая дорога / Дорога без покрытия
                    case "DIRT/SAND":
                        SetRoadAttributesData(ref attrib, 0, 0x02); SetRoadAttributesData(ref attrib, 0, 0x10); hasattrib = true; break;
                };
            };

            if (!String.IsNullOrEmpty(OSM_SERVICE))
            {
                // BYTE[0] 0x01 - Дворовый проезд / Жилая зона (5.21)
                if ((OSM_SERVICE == "EMERGENCY_ACCESS")) { SetRoadAttributesData(ref attrib, 0, 0x01); hasattrib = true; };
            };            

            bool noTrucks = false; bool isBridge = false; bool isTunnel = false; bool isFerry = false; bool isFord = false; bool isToll = false;
            try
            {
                // FIELDS: BRIDGE, TUNNEL, FERRY, ROUTE, FORD, TOLL
                // bridge=yes, tunnel=yes, route=ferry & ferry=*, ford=yes (брод), toll!=no
                if (data.ContainsKey("BRIDGE"))
                {
                    string rv = data["BRIDGE"] == null ? "" : data["BRIDGE"].ToString().ToLower();
                    if ((!String.IsNullOrEmpty(rv)) && (rv != "no"))
                    {
                        isBridge = true;
                        if (rv == "movable") SetRoadAttributesData(ref attrib, 1, 0x01);
                    };
                };
                if (data.ContainsKey("TUNNEL"))
                {
                    string rv = data["TUNNEL"] == null ? "" : data["TUNNEL"].ToString().ToLower();
                    if ((!String.IsNullOrEmpty(rv)) && (rv != "no")) isTunnel = true;
                };
                if (data.ContainsKey("FERRY"))
                {
                    string rv = data["FERRY"] == null ? "" : data["FERRY"].ToString().ToLower();
                    if ((!String.IsNullOrEmpty(rv)) && (rv != "no")) isFerry = true;
                };
                if (data.ContainsKey("ROUTE"))
                {
                    string rv = data["ROUTE"] == null ? "" : data["ROUTE"].ToString().ToLower();
                    if ((!String.IsNullOrEmpty(rv)) && (rv == "ferry")) isFerry = true;
                };
                if (data.ContainsKey("FORD"))
                {
                    string rv = data["FORD"] == null ? "" : data["FORD"].ToString().ToLower();
                    if ((!String.IsNullOrEmpty(rv)) && (rv != "no")) isFord = true;
                };
                if (data.ContainsKey("TOLL"))
                {
                    string rv = data["TOLL"] == null ? "" : data["TOLL"].ToString().ToLower();
                    if ((!String.IsNullOrEmpty(rv)) && (rv != "no")) isToll = true;
                };
                if (data.ContainsKey("FOOTWAY"))
                {
                    string rv = data["FOOTWAY"] == null ? "" : data["FOOTWAY"].ToString().ToLower();
                    if ((!String.IsNullOrEmpty(rv)) && (rv != "no")) { SetRoadAttributesData(ref attrib, 4, 0x08); hasattrib = true; };
                };
                if ((stream_ShapeFile_FieldNames.fldIsTunnel != null) && (stream_ShapeFile_FieldNames.fldIsTunnel != String.Empty) && (stream_ShapeFile_FieldNames.fldIsTunnel != "TUNNEL") && (data.ContainsKey(stream_ShapeFile_FieldNames.fldIsTunnel)))
                {
                    string rv = data[stream_ShapeFile_FieldNames.fldIsTunnel] == null ? "" : data[stream_ShapeFile_FieldNames.fldIsTunnel].ToString().ToLower();
                    if ((!String.IsNullOrEmpty(rv)) && (rv != "no")) isTunnel = true;
                };
                if ((stream_ShapeFile_FieldNames.fldTollRoad != null) && (stream_ShapeFile_FieldNames.fldTollRoad != String.Empty) && (stream_ShapeFile_FieldNames.fldTollRoad != "TOLL") && (data.ContainsKey(stream_ShapeFile_FieldNames.fldTollRoad)))
                {
                    string rv = data[stream_ShapeFile_FieldNames.fldTollRoad] == null ? "" : data[stream_ShapeFile_FieldNames.fldTollRoad].ToString().ToLower();
                    if ((!String.IsNullOrEmpty(rv)) && (rv != "no")) isToll = true;
                };
            }
            catch { };

            // AGGREGATED TAGS
            if (stream_ShapeFile_FieldNames.fldOSM_ADDIT_PROCESSAGG)
            {
                Dictionary<string,string> aggr_tags = CheckAggregatedTags_Get(ref data);
                CheckAggregatedTags_Analyse(ref aggr_tags, ref attrib, ref hasattrib);
            };

            // ACC_MASK
            if ((stream_ShapeFile_FieldNames.fldACCMask != null) && (stream_ShapeFile_FieldNames.fldACCMask != String.Empty))
            {
                string accm = data[stream_ShapeFile_FieldNames.fldACCMask].ToString();
                if (accm.Length == 10)
                {
                    // Движение грузового транспорта запрещено
                    if (accm.Substring(6, 1) == "1") noTrucks = true;
                    // BYTE[2] 0x02 Дорога для автомобилей (Знак 5.3)
                    if (accm == "0000110000") { SetRoadAttributesData(ref attrib, 2, 0x02); hasattrib = true; };
                };
            };

            // ATTR
            if ((stream_ShapeFile_FieldNames.fldAttr != null) && (stream_ShapeFile_FieldNames.fldAttr != String.Empty)) // пишем Attr
            {
                string attr = data[stream_ShapeFile_FieldNames.fldAttr].ToString().Replace(" ", "");
                if (attr.Length > 0)
                {
                    attr = "," + attr + ",";
                    // BYTE[0] 0x40 Тоннель (Знак 1.31)
                    if (attr.IndexOf(",92,") >= 0) isTunnel = true;
                    // BYTE[0] 0x80 Мост
                    if (attr.IndexOf(",501,") >= 0) { SetRoadAttributesData(ref attrib, 0, 0x80); hasattrib = true; };
                    // BYTE[1] 0x01 Разводной Мост
                    if (attr.IndexOf(",502,") >= 0) { SetRoadAttributesData(ref attrib, 1, 0x01); hasattrib = true; };
                    // BYTE[1] 0x02 Пантонный Мост
                    if (attr.IndexOf(",503,") >= 0) { SetRoadAttributesData(ref attrib, 1, 0x02); hasattrib = true; };
                    // BYTE[1] 0x04 Паром / Переправа
                    if (attr.IndexOf(",504,") >= 0) { SetRoadAttributesData(ref attrib, 1, 0x04); hasattrib = true; };
                    // BYTE[1] 0x08 Железнодорожный переезд (Знак 1.1, 1.2)
                    if (attr.IndexOf(",53,") >= 0) { SetRoadAttributesData(ref attrib, 1, 0x08); hasattrib = true; };
                    // BYTE[2] 0x08 Платная дорога
                    if (attr.IndexOf(",505,") >= 0) isToll = true;
                    // Движение грузового транспорта запрещено (Знак 3.4)     
                    if (attr.IndexOf(",93,") >= 0) noTrucks = true;
                    // BYTE[2] 0x80 Движение с прицепом запрещено (Знак 3.7)
                    if (attr.IndexOf(",95,") >= 0) { SetRoadAttributesData(ref attrib, 2, 0x80); hasattrib = true; };
                    // BYTE[3] 3x01 Таможня / Таможенная граница (Знак 3.17.1)
                    if (attr.IndexOf(",506,") >= 0) { SetRoadAttributesData(ref attrib, 3, 0x01); hasattrib = true; };
                    // BYTE[3] 3x02 Крутой спуск (Знак 1.13)
                    if (attr.IndexOf(",88,") >= 0) { SetRoadAttributesData(ref attrib, 3, 0x02); hasattrib = true; };
                    // BYTE[3] 3x04 Крутой подъем (Знак 1.14)    
                    if (attr.IndexOf(",89,") >= 0) { SetRoadAttributesData(ref attrib, 3, 0x04); hasattrib = true; };
                    // BYTE[3] 3x08 Дорожные работы
                    if (attr.IndexOf(",65,") >= 0) { SetRoadAttributesData(ref attrib, 3, 0x08); hasattrib = true; };
                    // BYTE[4] 4x01 Движениес опасными грузами запрещено (Знак 3.32)
                    // BYTE[4] 4x02 Движение транспортных средств с взрывчатыми и огнеопасными грузами запрещено (Знак 3.33)
                    if (attr.IndexOf(",96,") >= 0) { SetRoadAttributesData(ref attrib, 4, 0x01); SetRoadAttributesData(ref attrib, 4, 0x02); hasattrib = true; };
                    // BYTE[4] 4x04 Светофор
                    if (attr.IndexOf(",115,") >= 0) { SetRoadAttributesData(ref attrib, 4, 0x04); hasattrib = true; };
                };
            };

            if (noTrucks) { SetRoadAttributesData(ref attrib, 2, 0x10); hasattrib = true; }; // 2x10 Движение грузового транспорта запрещено (Знак 3.4)   
            if (isBridge) { SetRoadAttributesData(ref attrib, 0, 0x80); hasattrib = true; }; // 0x80 Мост
            if (isTunnel) { SetRoadAttributesData(ref attrib, 0, 0x40); hasattrib = true; }; // 0x40 Тоннель (Знак 1.31)
            if (isFerry) { SetRoadAttributesData(ref attrib, 1, 0x04); hasattrib = true; }; // 1x04 Паром / переправа
            if (isFord) { SetRoadAttributesData(ref attrib, 1, 0x10); hasattrib = true; }; // 1x10 Брод
            if (isToll) { SetRoadAttributesData(ref attrib, 2, 0x08); hasattrib = true; }; // 2x08 Платная дорога

            // Max Weight
            if ((stream_ShapeFile_FieldNames.fldMaxWeight != null) && (stream_ShapeFile_FieldNames.fldMaxWeight != String.Empty))
            {
                string ML = data[stream_ShapeFile_FieldNames.fldMaxWeight].ToString().Trim();
                if (ML != String.Empty)
                {
                    double ml = float.Parse(ML, stream_NumberFormat) * 1000 / 250;
                    if (ml > 0) { attrib[7] = (byte)ml; hasattrib = true; };
                };
            };

            // Max Axle Weight
            if ((stream_ShapeFile_FieldNames.fldMaxAxle != null) && (stream_ShapeFile_FieldNames.fldMaxAxle != String.Empty))
            {
                string AL = data[stream_ShapeFile_FieldNames.fldMaxAxle].ToString().Trim();
                if (AL != String.Empty)
                {
                    double al = float.Parse(AL, stream_NumberFormat) * 1000 / 250;
                    if (al > 0) { attrib[8] = (byte)al; hasattrib = true; };
                };
            };

            // Max Height
            if ((stream_ShapeFile_FieldNames.fldMaxHeight != null) && (stream_ShapeFile_FieldNames.fldMaxHeight != String.Empty))
            {
                string HL = data[stream_ShapeFile_FieldNames.fldMaxHeight].ToString().Trim();
                if (HL != String.Empty)
                {
                    double hl = float.Parse(HL, stream_NumberFormat) * 10;
                    if (hl > 0) { attrib[9] = (byte)hl; hasattrib = true; };
                };
            };

            // Max Width
            if ((stream_ShapeFile_FieldNames.fldMaxWidth != null) && (stream_ShapeFile_FieldNames.fldMaxWidth != String.Empty))
            {
                string WL = data[stream_ShapeFile_FieldNames.fldMaxWidth].ToString().Trim();
                if (WL != String.Empty)
                {
                    double wl = float.Parse(WL, stream_NumberFormat) * 10;
                    if (wl > 0) { attrib[10] = (byte)wl; hasattrib = true; };
                };
            };

            // Max Long/Length
            if ((stream_ShapeFile_FieldNames.fldMaxLength != null) && (stream_ShapeFile_FieldNames.fldMaxLength != String.Empty))
            {
                string LL = data[stream_ShapeFile_FieldNames.fldMaxLength].ToString().Trim();
                if (LL != String.Empty)
                {
                    double ll = float.Parse(LL, stream_NumberFormat) * 10;
                    if (ll > 0) { attrib[11] = (byte)ll; hasattrib = true; };
                };
            };

            // Min Distance
            if ((stream_ShapeFile_FieldNames.fldMinDistance != null) && (stream_ShapeFile_FieldNames.fldMinDistance != String.Empty))
            {
                string DL = data[stream_ShapeFile_FieldNames.fldMinDistance].ToString().Trim();
                if (DL != String.Empty)
                {
                    double dl = float.Parse(DL, stream_NumberFormat);
                    if (dl > 0) { attrib[12] = (byte)dl; hasattrib = true; };
                };
            };

            // Speed Limit [15] //
            byte s2w = (byte)(SPEED_LIMIT / 5);
            if (s2w > 31) s2w = 31;
            attrib[15] = (byte)(s2w << 3);

            // Route Level [15] //
            if ((stream_ShapeFile_FieldNames.fldRouteLevel != null) && (stream_ShapeFile_FieldNames.fldRouteLevel != String.Empty))
            {
                string DL = data[stream_ShapeFile_FieldNames.fldRouteLevel].ToString().Trim();
                if (DL != String.Empty)
                {
                    double dl = float.Parse(DL, stream_NumberFormat);
                    if (dl > 0) { attrib[15] += (byte)(((byte)dl) & 7); hasattrib = true; };
                };
            };

            stream_LineAttr.Write(attrib, 0, attrib.Length);
            CalculateAttributes(attrib);
            return hasattrib;
        }

        /// <summary>
        ///     Получение атрибутивной информации о линии из агрегированного поля TAGS_1 OSM2SHP
        /// </summary>
        private static Regex tag_agg_rx = new Regex(@"(?<item>(?<tag>[\w:]*)=(?<value>[^\n=]*));", RegexOptions.None);
        private Dictionary<string, string> CheckAggregatedTags_Get(ref Hashtable data)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            try
            {
                // tag_agg_rx
                if (data.ContainsKey("TAGS_1"))
                {
                    string tags = data["TAGS_1"] == null ? "" : data["TAGS_1"].ToString().ToLower();
                    if (!String.IsNullOrEmpty(tags))
                    {
                        if (!tags.EndsWith(";")) tags += ";";
                        MatchCollection mc = tag_agg_rx.Matches(tags);
                        foreach (Match mx in mc)
                            result.Add(mx.Groups["tag"].Value, mx.Groups["value"].Value);
                    };
                };
            }
            catch { };
            return result;
        }

        /// <summary>
        ///     Анализ атрибутивной информации о линии из агрегированного поля TAGS_1 OSM2SHP
        /// </summary>
        /// <param name="agg_tags"></param>
        /// <param name="attrib"></param>
        /// <param name="hasattrib"></param>
        private void CheckAggregatedTags_Analyse(ref Dictionary<string, string> agg_tags, ref byte[] attrib, ref bool hasattrib)
        {
            if (agg_tags == null) return;
            if (agg_tags.Count == 0) return;

            //if (agg_tags.ContainsKey("footway") && agg_tags["footway"] != "no") { SetRoadAttributesData(ref attrib, 4, 0x08); hasattrib = true; };
            if (agg_tags.ContainsKey("ford") && agg_tags["ford"] == "yes") { SetRoadAttributesData(ref attrib, 1, 0x10); hasattrib = true; };
            if (agg_tags.ContainsKey("motor_vehicle") && agg_tags["motor_vehicle"] == "yes") { SetRoadAttributesData(ref attrib, 2, 0x02); hasattrib = true; };
            if (agg_tags.ContainsKey("motor_vehicle") && agg_tags["motor_vehicle"] == "no") { SetRoadAttributesData(ref attrib, 4, 0x08); hasattrib = true; };
            if (agg_tags.ContainsKey("vehicle") && agg_tags["vehicle"] == "no") { SetRoadAttributesData(ref attrib, 4, 0x08); hasattrib = true; };
            if (agg_tags.ContainsKey("living_street") && agg_tags["living_street"] == "yes") { SetRoadAttributesData(ref attrib, 0, 0x01); hasattrib = true; };
            if (agg_tags.ContainsKey("crossing") && agg_tags["crossing"] == "traffic_signal") { SetRoadAttributesData(ref attrib, 4, 0x04); hasattrib = true; };
            if (agg_tags.ContainsKey("traffic_signals:direction")) { SetRoadAttributesData(ref attrib, 4, 0x04); hasattrib = true; };
            if (agg_tags.ContainsKey("motorcycle") && agg_tags["motorcycle"] == "no") { SetRoadAttributesData(ref attrib, 2, 0x20); hasattrib = true; };            
            if (agg_tags.ContainsKey("motorroad") && agg_tags["motorroad"] == "yes") { SetRoadAttributesData(ref attrib, 2, 0x02); hasattrib = true; };
            if (agg_tags.ContainsKey("lit") && (agg_tags["lit"] == "no" || agg_tags["lit"] == "disused")) { SetRoadAttributesData(ref attrib, 4, 0x10); hasattrib = true; };
            if (agg_tags.ContainsKey("electrified") && agg_tags["electrified"] == "no") { SetRoadAttributesData(ref attrib, 4, 0x10); hasattrib = true; };

            // https://wiki.openstreetmap.org/wiki/RU:Key:traffic_sign
            // https://wiki.openstreetmap.org/wiki/Proposed_features/Traffic_sign

            // 3x02 Крутой спуск (Знак 1.13)
            // 3x04 Крутой подъем (Знак 1.14)   
            if (agg_tags.ContainsKey("incline"))
            {
                Regex rx = new Regex(@"^-?(?<value>[\.\d]*)[%|°]?$");
                Match mc = rx.Match(agg_tags["incline"]);
                double val = 0;
                if (mc.Success && (!String.IsNullOrEmpty(mc.Groups["value"].Value)) && (double.TryParse(mc.Groups["value"].Value, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out val)) && (val >= 10))
                {
                    SetRoadAttributesData(ref attrib, 3, 0x02);
                    SetRoadAttributesData(ref attrib, 3, 0x04);
                    hasattrib = true;
                };
            };

            // 3x10 Обгон запрещен (Знак 3.20)
            if (agg_tags.ContainsKey("overtaking") && agg_tags["overtaking"] == "no") 
            { 
                SetRoadAttributesData(ref attrib, 3, 0x10);
                SetRoadAttributesData(ref attrib, 3, 0x20); 
                hasattrib = true; 
            };

            // [07,0xFF] ..............FF................ - Ограничение массы ТС
            if (agg_tags.ContainsKey("maxweight"))
            {
                double d = 0;
                if (double.TryParse(agg_tags["maxweight"], System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out d))
                {
                    double ml = d * 1000.0 / 250.0;
                    if (ml > 0) { attrib[7] = (byte)ml; hasattrib = true; }; // в 1/4 тонны (0x04 = 1 тонна; от 250 кг до 63750 кг)                           
                };
            };

            // [08,0xFF] ................FF.............. - Ограничение нагрузки на ось ТС
            if (agg_tags.ContainsKey("maxaxleload"))
            {
                double d = 0;
                if (double.TryParse(agg_tags["maxaxleload"], System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out d))
                {
                    double ml = d * 1000.0 / 250.0;
                    if (ml > 0) { attrib[8] = (byte)ml; hasattrib = true; }; // в 1/4 тонны (0x04 = 1 тонна; от 250 кг до 63750 кг)                           
                };
            };

            // [09,0xFF] ..................FF............ - Ограничение высоты
            if (agg_tags.ContainsKey("maxheight"))
            {
                double d = 0;
                if (double.TryParse(agg_tags["maxheight"], System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out d))
                {
                    double ml = d * 10.0;
                    if (ml > 0) { attrib[9] = (byte)ml; hasattrib = true; }; // в дециметрах (от 10 см до 25 м)
                };
            };

            // [10,0xFF] ....................FF.......... - Ограничение ширины
            if (agg_tags.ContainsKey("maxwidth"))
            {
                double d = 0;
                if (double.TryParse(agg_tags["maxwidth"], System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out d))
                {
                    double ml = d * 10.0;
                    if (ml > 0) { attrib[10] = (byte)ml; hasattrib = true; }; // в дециметрах (от 10 см до 25 м)
                };
            };

            // [11,0xFF] ......................FF........ - Ограничение длины ТС
            if (agg_tags.ContainsKey("maxlength"))
            {
                double d = 0;
                if (double.TryParse(agg_tags["maxlength"], System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out d))
                {
                    double ml = d * 10.0;
                    if (ml > 0) { attrib[11] = (byte)ml; hasattrib = true; }; // в дециметрах (от 10 см до 25 м)
                };
            };            

            // traffic_sign
            // ex: lit=yes;maxweight=2.8;motor_vehicle=destination;segregated=no;smoothness=good;traffic_sign=DE:240,1020-30;262[2.8];width=4;
            if (agg_tags.ContainsKey("traffic_sign"))
            {
                
            };
        }

        /// <summary>
        ///     Установка атрибутивной информации линии
        /// </summary>
        /// <param name="attr"></param>
        /// <param name="index"></param>
        /// <param name="code"></param>
        private void SetRoadAttributesData(ref byte[] attr, int index, int code)
        {
            if ((attr[index] & code) == code) return;
            attr[index] += (byte)code;
        }

        /// <summary>
        ///     Сохранение статистики атрибутивной информации
        /// </summary>
        /// <param name="ba"></param>
        private void CalculateAttributes(byte[] ba)
        {
            if (!analyse_attributes_do) return;

            if (ba == null) return;
            if (ba.Length != 16) return;
            for (int i = 0; i < 56; i++)
            {
                int ai = (int)(i / 8);
                int bi = i % 8;
                if ((ba[ai] & (byte)Math.Pow(2, bi)) > 0) analyse_attributes_bit[i]++;
            };
            for (int i = 7; i < 15; i++)
                if (ba[i] > 0)
                    analyse_attributes_val[i-7]++;
            if((ba[15] & 0xF8) > 0) analyse_attributes_val[8]++;
            if((ba[15] & 0x07) > 0) analyse_attributes_val[9]++;
        }

        /// <summary>
        ///     Добавляем узел в список
        /// </summary>
        /// <param name="lat">широта</param>
        /// <param name="lon">долгота</param>
        /// <returns>Номер узла</returns>
        public uint AddNode(float lat, float lon)
        {
            analyse_Nodes.Add(new TNode((uint)analyse_Nodes.Count + 1, lat, lon));
            return (uint)analyse_Nodes.Count;
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
            for (int i = 0; i < analyse_Nodes.Count; i++)
                if ((analyse_Nodes[i].lat == lat) && (analyse_Nodes[i].lon == lon))
                {
                    res = analyse_Nodes[i].node;
                    break;
                };
            return res;
        }
        //{
        //    uint res = 0;
        //    for (int i = 0; i < analyse_Nodes.Count; i++)
        //        if (EqualCoord(analyse_Nodes[i].lat, lat, 0.00000001f) && EqualCoord(analyse_Nodes[i].lon, lon, 0.00000001f))
        //        {
        //            res = analyse_Nodes[i].node;
        //            break;
        //        };
        //    return res;
        //}

        public static bool EqualCoord(float a, float b, float eps) 
        {
            if (a == b) return true;

            float diff = Math.Abs(a - b);
            if (diff < eps) return true;

            return false;
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
            return Utils.GetLengthMeters(StartLat, StartLong, EndLat, EndLong, radians);
        }
    }

    // https://programmingwithmosh.com/net/csharp-collections/
    // Hashtable  - Unical list
    // List<T>    - Represents a list of objects that can be accessed by an index
    // Dictionary<TKey, TValue> - Dictionary is a collection type that is useful when you need fast lookups by keys
    // HashSet<T> - Unical list
    // Stack<T>   - LIFO buffer
    // Queue<T>   - FIFO buffer
    public static class DigitConvertions
    {
        public static uint to4(float input)
        {
            unsafe
            {
                uint res = 0;
                float* p_val = (float*)&res;
                *p_val = input;
                return res;
            };
        }

        public static void from4(uint input, out float output)
        {
            unsafe
            {
                float res = 0;
                uint* p_val = (uint*)&res;
                *p_val = input;
                output = res;
            };
        }

        public static uint to4(short x, short y)
        {
            unsafe
            {
                uint xy = 0;
                short* px = (short*)&xy;
                short* py = px + 1;

                *px = x;
                *py = y;

                return xy;
            };
        }

        public static void from4(uint input, out short x, out short y)
        {
            unsafe
            {
                uint xy = input;
                short* px = (short*)&xy;
                short* py = px + 1;
                x = *px;
                y = *py;
            }
        }

        public static uint to4(ushort x, ushort y)
        {
            unsafe
            {
                uint xy = 0;
                ushort* px = (ushort*)&xy;
                ushort* py = px + 1;

                *px = x;
                *py = y;

                return xy;
            };
        }

        public static void from4(uint input, out ushort x, out ushort y)
        {
            unsafe
            {
                uint xy = input;
                ushort* px = (ushort*)&xy;
                ushort* py = px + 1;
                x = *px;
                y = *py;
            }
        }

        public static uint to4(short x, ushort y)
        {
            unsafe
            {
                uint xy = 0;
                short* px = (short*)&xy;
                ushort* py = (ushort*)px + 1;

                *px = x;
                *py = y;

                return xy;
            };
        }

        public static void from4(uint input, out short x, out ushort y)
        {
            unsafe
            {
                uint xy = input;
                short* px = (short*)&xy;
                ushort* py = (ushort*)px + 1;
                x = *px;
                y = *py;
            }
        }

        public static uint to4(short x, byte y, byte z)
        {
            unsafe
            {
                uint xy = 0;
                short* px = (short*)&xy;
                byte* py = (byte*)px + 1;
                byte* pz = py + 1;

                *px = x;
                *py = y;
                *pz = z;

                return xy;
            };
        }

        public static void from4(uint input, out short x, out byte y, out byte z)
        {
            unsafe
            {
                uint xy = input;
                short* px = (short*)&xy;
                byte* py = (byte*)px + 1;
                byte* pz = py + 1;
                x = *px;
                y = *py;
                z = *pz;
            }
        }

        public static uint to4(ushort x, byte y, byte z)
        {
            unsafe
            {
                uint xy = 0;
                ushort* px = (ushort*)&xy;
                byte* py = (byte*)px + 1;
                byte* pz = py + 1;

                *px = x;
                *py = y;
                *pz = z;

                return xy;
            };
        }

        public static void from4(uint input, out ushort x, out byte y, out byte z)
        {
            unsafe
            {
                uint xy = input;
                ushort* px = (ushort*)&xy;
                byte* py = (byte*)px + 1;
                byte* pz = py + 1;
                x = *px;
                y = *py;
                z = *pz;
            }
        }

        public static uint to4(byte[] input)
        {
            unsafe
            {
                uint val = 0;
                byte* pval = (byte*)&val;

                for (int i = 0; i < 4 && i < input.Length; i++)
                    *pval++ = input[i];

                return val;
            };
        }

        public static void from4(uint input, out byte[] output)
        {
            unsafe
            {
                uint v = input;
                byte* pv = (byte*)&v;

                byte[] res = new byte[4];
                for (int i = 0; i < 4 && i < res.Length; i++)
                    res[i] = *pv++;

                output = res;
            };
        }

        public static ulong to8(double input)
        {
            unsafe
            {
                ulong res = 0;
                double* p_val = (double*)&res;
                *p_val = input;
                return res;
            };
        }

        public static void from8(ulong input, out double output)
        {
            unsafe
            {
                double res = 0;
                ulong* p_val = (ulong*)&res;
                *p_val = input;
                output = res;
            };
        }

        public static ulong to8(float x, float y)
        {
            unsafe
            {
                ulong xy = 0;
                float* px = (float*)&xy;
                float* py = px + 1;

                *px = x;
                *py = y;

                return xy;
            };
        }

        public static void from8(ulong input, out float x, out float y)
        {
            unsafe
            {
                ulong xy = input;
                float* px = (float*)&xy;
                float* py = px + 1;
                x = *px;
                y = *py;
            }
        }

        public static ulong to8(float x, int y)
        {
            unsafe
            {
                ulong xy = 0;
                float* px = (float*)&xy;
                int* py = (int*)px + 1;

                *px = x;
                *py = y;

                return xy;
            };
        }

        public static void from8(ulong input, out float x, out int y)
        {
            unsafe
            {
                ulong xy = input;
                float* px = (float*)&xy;
                int* py = (int*)px + 1;
                x = *px;
                y = *py;
            }
        }

        public static ulong to8(float x, uint y)
        {
            unsafe
            {
                ulong xy = 0;
                float* px = (float*)&xy;
                uint* py = (uint*)px + 1;

                *px = x;
                *py = y;

                return xy;
            };
        }

        public static void from8(ulong input, out float x, out uint y)
        {
            unsafe
            {
                ulong xy = input;
                float* px = (float*)&xy;
                uint* py = (uint*)px + 1;
                x = *px;
                y = *py;
            }
        }

        public static ulong to8(float x, short y, short z)
        {
            unsafe
            {
                ulong xy = 0;
                float* px = (float*)&xy;
                short* py = (short*)px + 1;
                short* pz = py + 1;

                *px = x;
                *py = y;
                *pz = z;

                return xy;
            };
        }

        public static void from8(ulong input, out float x, out short y, out short z)
        {
            unsafe
            {
                ulong xy = input;
                float* px = (float*)&xy;
                short* py = (short*)px + 1;
                short* pz = py + 1;
                x = *px;
                y = *py;
                z = *pz;
            }
        }

        public static ulong to8(float x, ushort y, ushort z)
        {
            unsafe
            {
                ulong xy = 0;
                float* px = (float*)&xy;
                ushort* py = (ushort*)px + 1;
                ushort* pz = py + 1;

                *px = x;
                *py = y;
                *pz = z;

                return xy;
            };
        }

        public static void from8(ulong input, out float x, out ushort y, out ushort z)
        {
            unsafe
            {
                ulong xy = input;
                float* px = (float*)&xy;
                ushort* py = (ushort*)px + 1;
                ushort* pz = py + 1;
                x = *px;
                y = *py;
                z = *pz;
            }
        }

        public static ulong to8(float x, short y, ushort z)
        {
            unsafe
            {
                ulong xy = 0;
                float* px = (float*)&xy;
                short* py = (short*)px + 1;
                ushort* pz = (ushort*)py + 1;

                *px = x;
                *py = y;
                *pz = z;

                return xy;
            };
        }

        public static void from8(ulong input, out float x, out short y, out ushort z)
        {
            unsafe
            {
                ulong xy = input;
                float* px = (float*)&xy;
                short* py = (short*)px + 1;
                ushort* pz = (ushort*)py + 1;
                x = *px;
                y = *py;
                z = *pz;
            }
        }

        public static ulong to8(float x, short y, byte za, byte zb)
        {
            unsafe
            {
                ulong xy = 0;
                float* px = (float*)&xy;
                short* py = (short*)px + 1;
                byte* pza = (byte*)py + 1;
                byte* pzb = pza + 1;

                *px = x;
                *py = y;
                *pza = za;
                *pzb = zb;

                return xy;
            };
        }

        public static void from8(ulong input, out float x, out short y, out byte za, out byte zb)
        {
            unsafe
            {
                ulong xy = input;
                float* px = (float*)&xy;
                short* py = (short*)px + 1;
                byte* pza = (byte*)py + 1;
                byte* pzb = pza + 1;
                x = *px;
                y = *py;
                za = *pza;
                zb = *pzb;
            }
        }

        public static ulong to8(float x, ushort y, byte za, byte zb)
        {
            unsafe
            {
                ulong xy = 0;
                float* px = (float*)&xy;
                ushort* py = (ushort*)px + 1;
                byte* pza = (byte*)py + 1;
                byte* pzb = pza + 1;

                *px = x;
                *py = y;
                *pza = za;
                *pzb = zb;

                return xy;
            };
        }

        public static void from8(ulong input, out float x, out ushort y, out byte za, out byte zb)
        {
            unsafe
            {
                ulong xy = input;
                float* px = (float*)&xy;
                ushort* py = (ushort*)px + 1;
                byte* pza = (byte*)py + 1;
                byte* pzb = pza + 1;
                x = *px;
                y = *py;
                za = *pza;
                zb = *pzb;
            }
        }

        public static ulong to8(short[] input)
        {
            unsafe
            {
                ulong val = 0;
                short* pval = (short*)&val;

                for (int i = 0; i < 4 && i < input.Length; i++)
                    *pval++ = input[i];

                return val;
            };
        }

        public static void from8(ulong input, out short[] output)
        {
            unsafe
            {
                ulong v = input;
                short* pv = (short*)&v;

                short[] res = new short[4];
                for (int i = 0; i < 4 && i < res.Length; i++)
                    res[i] = *pv++;

                output = res;
            };
        }

        public static ulong to8(ushort[] input)
        {
            unsafe
            {
                ulong val = 0;
                ushort* pval = (ushort*)&val;

                for (int i = 0; i < 4 && i < input.Length; i++)
                    *pval++ = input[i];

                return val;
            };
        }

        public static void from8(ulong input, out ushort[] output)
        {
            unsafe
            {
                ulong v = input;
                ushort* pv = (ushort*)&v;

                ushort[] res = new ushort[4];
                for (int i = 0; i < 4 && i < res.Length; i++)
                    res[i] = *pv++;

                output = res;
            };
        }

        public static ulong to8(byte[] input)
        {
            unsafe
            {
                ulong val = 0;
                byte* pval = (byte*)&val;

                for (int i = 0; i < 8 && i < input.Length; i++)
                    *pval++ = input[i];

                return val;
            };
        }

        public static void from8(ulong input, out byte[] output)
        {
            unsafe
            {
                ulong v = input;
                byte* pv = (byte*)&v;

                byte[] res = new byte[8];
                for (int i = 0; i < 8 && i < res.Length; i++)
                    res[i] = *pv++;

                output = res;
            };
        }        
    }
}
