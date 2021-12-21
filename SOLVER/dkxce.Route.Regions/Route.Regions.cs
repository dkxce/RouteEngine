/* 
 * C# Class by Milok Zbrozek <milokz@gmail.com>
 * Модуль определяющий попадание
 * точек в регионы (в полигоны)
 * Author: Milok Zbrozek <milokz@gmail.com>
 * Версия: 13305C7
 */

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

using dkxce.Route.Classes;

namespace dkxce.Route.Regions
{
    /// <summary>
    ///     Класс для определения вхождение точки в регион 
    /// </summary>
    public class PointInRegionUtils
    {
        #region constants
        private const string RegionIDField = "REGION_ID";
        private const string RegionIDName = "NAME";
        private const string RegionCenterX = "X";
        private const string RegionCenterY = "Y";
        private const string RegionFileName = "FILENAME";
        #endregion

        List<Polygon> Regions = new List<Polygon>();

        public void LoadRegionsFromFile(string ShapeFileName)
        {
            string ShapeDBFName = ShapeFileName.Replace(System.IO.Path.GetExtension(ShapeFileName), ".dbf");

            // READING BOUNDS INTO MEMORY
            if (!System.IO.File.Exists(ShapeFileName))
                throw new System.IO.IOException(@"Couldn't load regions bounds. File Not Found: " + ShapeFileName);
            this.readShapeFile(ShapeFileName);

            // READING FIELDS INTO MEMORY
            if (this.Regions.Count > 0)
            {
                if (!System.IO.File.Exists(ShapeDBFName))
                    throw new System.IO.IOException(@"Couldn't load regions bounds. File Not Found: " + ShapeDBFName);
                this.readDBFFile(ShapeDBFName);
            };
        }
        public int RegionsCount { get { return this.Regions.Count; } }

        public void _TestRegions()
        {
            foreach (Polygon reg in this.Regions)
            {
                int cr = this.PointInRegion(reg.center.Y, reg.center.X);
                Console.WriteLine((reg.nmsRegionID == cr ? "OK" : "!!") + " center of " + reg.nmsRegionID + " in " + cr.ToString()
                    + "\t" + reg.nmsRegion + " [" + reg.center.Y.ToString() + " "
                    + reg.center.X.ToString() + "]");
            };
        }

        public Polygon GetRegionPoly(int index)
        {
            if (Regions.Count == 0) return null;
            if (index >= Regions.Count) return null;
            return Regions[index];
        }

        public int[] RegionsIDs
        {
            get
            {
                List<int> res = new List<int>();
                foreach (Polygon reg in this.Regions) res.Add(reg.nmsRegionID);
                return res.ToArray();
            }
        }

        public string[] RegionsNames
        {
            get
            {
                List<string> res = new List<string>();
                foreach (Polygon reg in this.Regions) res.Add(reg.nmsRegion);
                return res.ToArray();
            }
        }

        public string[] RegionsFiles
        {
            get
            {
                List<string> res = new List<string>();
                foreach (Polygon reg in this.Regions) res.Add(reg.FileName);
                return res.ToArray();
            }
        }

        public PointF[] RegionsCentres
        {
            get
            {
                List<PointF> res = new List<PointF>();
                foreach (Polygon reg in this.Regions) res.Add(reg.center);
                return res.ToArray();
            }
        }

        public string RegionNameByRegionId(int RegionID)
        {
            for (int i = 0; i < this.Regions.Count; i++)
                if (this.Regions[i].nmsRegionID == RegionID)
                    return this.Regions[i].nmsRegion;
            return String.Empty;
        }

        public string RegionFileByRegionId(int RegionID)
        {
            for (int i = 0; i < this.Regions.Count; i++)
                if (this.Regions[i].nmsRegionID == RegionID)
                    return this.Regions[i].FileName;
            return String.Empty;
        }

        public int PointInRegion(double Lat, double Lon)
        {
            PointF xy = new PointF((float)Lon, (float)Lat);

            for (int i = 0; i < this.Regions.Count; i++)
            {
                if (xy.X < this.Regions[i].box[0]) continue; // Xmin
                if (xy.Y < this.Regions[i].box[1]) continue; // Ymin
                if (xy.X > this.Regions[i].box[2]) continue; // Xmax
                if (xy.Y > this.Regions[i].box[3]) continue; // Ymax
                if (PointInMultiPartsPolygon(xy, this.Regions[i], MaxError))
                    return this.Regions[i].nmsRegionID;
            };
            return 0;
        }


        public int[] PointInRegions(double Lat, double Lon)
        {
            List<int> result = new List<int>();
            PointF xy = new PointF((float)Lon, (float)Lat);


            for (int i = 0; i < this.Regions.Count; i++)
            {
                if (xy.X < this.Regions[i].box[0]) continue; // Xmin
                if (xy.Y < this.Regions[i].box[1]) continue; // Ymin
                if (xy.X > this.Regions[i].box[2]) continue; // Xmax
                if (xy.Y > this.Regions[i].box[3]) continue; // Ymax
                if (PointInMultiPartsPolygon(xy, this.Regions[i], MaxError))
                    result.Add(this.Regions[i].nmsRegionID);
            };
            return result.ToArray();
        }

        private static bool PointInMultiPartsPolygon(PointF LonAsXLatAsY, Polygon Area, double MaxError)
        {
            // MODIFIED 06.11.2015
            if (Area.parts.Length < 2)
                return PointInPolygon(LonAsXLatAsY, Area, MaxError);
            else
            {
                for (int i = 0; i < Area.parts.Length; i++)
                {
                    Polygon tmpArea = new Polygon();
                    int si = Area.parts[i];
                    int se = (i + 1) == Area.parts.Length ? Area.points.Length : Area.parts[i + 1];
                    tmpArea.points = new PointF[se - si];
                    Array.Copy(Area.points, si, tmpArea.points, 0, tmpArea.points.Length);
                    if (i == 0)
                    {
                        if (!PointInPolygon(LonAsXLatAsY, tmpArea, MaxError)) return false;
                    }
                    else
                    {
                        if (PointInPolygon(LonAsXLatAsY, tmpArea, MaxError)) return false;
                    };
                };
            };
            return true;
        }

        #region read shape file
        private void readDBFFile(string filename)
        {
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InstalledUICulture;
            System.Globalization.NumberFormatInfo ni = (System.Globalization.NumberFormatInfo)ci.NumberFormat.Clone();
            ni.NumberDecimalSeparator = ".";

            System.IO.FileStream fs = new System.IO.FileStream(filename, System.IO.FileMode.Open);
            System.Text.Encoding FileReadEncoding = System.Text.Encoding.GetEncoding(1251);

            // Read File Version
            fs.Position = 0;
            int ver = fs.ReadByte();

            // Read Records Count
            fs.Position = 04;
            byte[] bb = new byte[4];
            fs.Read(bb, 0, 4);
            int total = BitConverter.ToInt32(bb, 0);

            // Read DataRecord 1st Position  
            fs.Position = 8;
            bb = new byte[2];
            fs.Read(bb, 0, 2);
            short dataRecord_1st_Pos = BitConverter.ToInt16(bb, 0);
            int FieldsCount = (((bb[0] + (bb[1] * 0x100)) - 1) / 32) - 1;

            // Read DataRecord Length
            fs.Position = 10;
            bb = new byte[2];
            fs.Read(bb, 0, 2);
            short dataRecord_Length = BitConverter.ToInt16(bb, 0);

            // Read Заголовки Полей
            fs.Position = 32;
            string[] Fields_Name = new string[FieldsCount]; // Массив названий полей
            Hashtable fieldsLength = new Hashtable();       // Массив размеров полей
            Hashtable fieldsType = new Hashtable();         // Массив типов полей
            byte[] Fields_Dig = new byte[FieldsCount];      // Массив размеров дробной части
            int[] Fields_Offset = new int[FieldsCount];     // Массив отступов
            bb = new byte[32 * FieldsCount]; // Описание полей: 32 байтa * кол-во, начиная с 33-го            
            fs.Read(bb, 0, bb.Length);
            int FieldsLength = 0;
            for (int x = 0; x < FieldsCount; x++)
            {
                Fields_Name[x] = System.Text.Encoding.Default.GetString(bb, x * 32, 10).TrimEnd(new char[] { (char)0x00 }).ToUpper();
                fieldsType.Add(Fields_Name[x], "" + (char)bb[x * 32 + 11]);
                fieldsLength.Add(Fields_Name[x], (int)bb[x * 32 + 16]);
                Fields_Dig[x] = bb[x * 32 + 17];
                Fields_Offset[x] = 1 + FieldsLength;
                FieldsLength = FieldsLength + (int)fieldsLength[Fields_Name[x]];
            };

            for (int record_no = 0; record_no < total; record_no++)
            {
                string[] FieldValues = new string[FieldsCount];
                Hashtable record = new Hashtable();
                for (int y = 0; y < FieldValues.Length; y++)
                {
                    fs.Position = dataRecord_1st_Pos + (dataRecord_Length * record_no) + Fields_Offset[y];
                    bb = new byte[(int)fieldsLength[Fields_Name[y]]];
                    fs.Read(bb, 0, bb.Length);
                    FieldValues[y] = FileReadEncoding.GetString(bb).Trim().TrimEnd(new char[] { (char)0x00 });
                    record.Add(Fields_Name[y], FieldValues[y]);
                };
                this.Regions[record_no].nmsRegionID = Convert.ToInt32(record[RegionIDField].ToString().Trim());
                this.Regions[record_no].nmsRegion = record[RegionIDName].ToString().Trim();
                this.Regions[record_no].center = new PointF((float)double.Parse(record[RegionCenterX].ToString().Trim(), ni), (float)double.Parse(record[RegionCenterY].ToString().Trim(), ni));
                this.Regions[record_no].FileName = "";
                try
                {
                    this.Regions[record_no].FileName = record[RegionFileName].ToString().Trim();
                }
                catch { };
                if (this.Regions[record_no].FileName == "")
                    this.Regions[record_no].FileName = String.Format("{0:000}", this.Regions[record_no].nmsRegionID) + ".rt";
            };

            fs.Close();
        }

        private void readShapeFile(string filename)
        {
            FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            long fileLength = fs.Length;
            Byte[] data = new Byte[fileLength];
            fs.Read(data, 0, (int)fileLength);
            fs.Close();

            int shapetype = readIntLittle(data, 32);
            if (shapetype != 5) return;

            int currentPosition = 100;
            while (currentPosition < fileLength)
            {
                int recordStart = currentPosition;
                int recordNumber = readIntBig(data, recordStart);
                int contentLength = readIntBig(data, recordStart + 4);
                int recordContentStart = recordStart + 8;

                Polygon Area = new Polygon();
                int recordShapeType = readIntLittle(data, recordContentStart);
                Area.box = new Double[4];
                Area.box[0] = readDoubleLittle(data, recordContentStart + 4);
                Area.box[1] = readDoubleLittle(data, recordContentStart + 12);
                Area.box[2] = readDoubleLittle(data, recordContentStart + 20);
                Area.box[3] = readDoubleLittle(data, recordContentStart + 28);
                Area.numParts = readIntLittle(data, recordContentStart + 36);
                Area.parts = new int[Area.numParts];
                Area.numPoints = readIntLittle(data, recordContentStart + 40);
                Area.points = new PointF[Area.numPoints];
                int partStart = recordContentStart + 44;
                for (int i = 0; i < Area.numParts; i++)
                {
                    Area.parts[i] = readIntLittle(data, partStart + i * 4);
                };
                int pointStart = recordContentStart + 44 + 4 * Area.numParts;
                for (int i = 0; i < Area.numPoints; i++)
                {
                    Area.points[i] = new PointF(0,0);
                    Area.points[i].X = (float)readDoubleLittle(data, pointStart + (i * 16));
                    Area.points[i].Y = (float)readDoubleLittle(data, pointStart + (i * 16) + 8);
                };
                this.Regions.Add(Area);

                currentPosition = recordStart + (4 + contentLength) * 2;
            };
        }

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

        private int readIntLittle(byte[] data, int pos)
        {
            byte[] bytes = new byte[4];
            bytes[0] = data[pos];
            bytes[1] = data[pos + 1];
            bytes[2] = data[pos + 2];
            bytes[3] = data[pos + 3];
            return BitConverter.ToInt32(bytes, 0);
        }

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
        #endregion

        public class Polygon
        {
            public string nmsRegion;
            public int nmsRegionID;
            public string FileName;

            public double[] box; // Xmin, Ymin, Xmax, Ymax
            public int numParts;
            public int numPoints;
            public int[] parts;
            public PointF[] points;

            public PointF center;
        }

        #region Проверка на вхождение в полигон
        private static double MaxError = 1E-09;

        private static int CRS(PointF P, PointF A1, PointF A2, double EPS)
        {
            double x;
            int res = 0;
            if (Math.Abs(A1.Y - A2.Y) < EPS)
            {
                if ((Math.Abs(P.Y - A1.Y) < EPS) && ((P.X - A1.X) * (P.X - A2.X) < 0.0)) res = -1;
                return res;
            };
            if ((A1.Y - P.Y) * (A2.Y - P.Y) > 0.0) return res;
            x = A2.X - (A2.Y - P.Y) / (A2.Y - A1.Y) * (A2.X - A1.X);
            if (Math.Abs(x - P.X) < EPS)
            {
                res = -1;
            }
            else
            {
                if (x < P.X)
                {
                    res = 1;
                    if ((Math.Abs(A1.Y - P.Y) < EPS) && (A1.Y < A2.Y)) res = 0;
                    else
                        if ((Math.Abs(A2.Y - P.Y) < EPS) && (A2.Y < A1.Y)) res = 0;
                };
            };
            return res;
        }

        private static bool PointInPolygon(PointF point, Polygon polygon, double EPS)
        {
            int count, up;
            count = 0;
            for (int i = 0; i < polygon.points.Length - 1; i++)
            {
                up = CRS(point, polygon.points[i], polygon.points[i + 1], EPS);
                if (up >= 0)
                    count += up;
                else
                    break;
            };
            up = CRS(point, polygon.points[polygon.points.Length - 1], polygon.points[0], EPS);
            if (up >= 0)
                return Convert.ToBoolean((count + up) & 1);
            else
                return false;
        }
        #endregion
    }
}
