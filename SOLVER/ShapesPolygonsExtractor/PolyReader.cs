using System;
using System.Drawing;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace ShapesPolygonsExtractor
{
    public class Polygon
    {
        public string nmsRegion;
        public int nmsRegionID;
        public string FileName;

        public double[] box; // Xmin, Ymin, Xmax, Ymax
        public double[] cbox = new double[4] { double.MaxValue, double.MaxValue, double.MinValue, double.MinValue }; // Xmin, Ymin, Xmax, Ymax
        public int numParts;
        public int numPoints;
        public int[] parts;
        public PointF[] points;

        public PointF center;
        public Dictionary<string, object> Attributes = null;
    }

    /// <summary>
    ///     Класс для определения вхождение точки в регион 
    /// </summary>
    public class PolyReader
    {       
        public List<Polygon> Regions = new List<Polygon>();

        public PolyReader(string filename, bool readDBF)
        {
            FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            long fileLength = fs.Length;
            Byte[] data = new Byte[fileLength];
            fs.Read(data, 0, (int)fileLength);
            fs.Close();

            int shapetype = readIntLittle(data, 32);
            if (shapetype != 5) return;

            DBFSharp.DBFFile dbff = null;
            try { if (readDBF) dbff = new DBFSharp.DBFFile(filename.Replace(Path.GetExtension(filename),".dbf"), FileMode.Open); } catch { };            

            int left = Console.CursorLeft;

            int currentPosition = 100;
            uint rCounter = 0;
            while (currentPosition < fileLength)
            {
                rCounter++;
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
                    Area.points[i] = new PointF(0, 0);
                    Area.points[i].X = (float)readDoubleLittle(data, pointStart + (i * 16));
                    Area.points[i].Y = (float)readDoubleLittle(data, pointStart + (i * 16) + 8);
                    if (Area.cbox[0] > Area.points[i].X) Area.cbox[0] = Area.points[i].X;
                    if (Area.cbox[1] > Area.points[i].Y) Area.cbox[1] = Area.points[i].Y;
                    if (Area.cbox[2] < Area.points[i].X) Area.cbox[2] = Area.points[i].X;
                    if (Area.cbox[3] < Area.points[i].Y) Area.cbox[3] = Area.points[i].Y;
                };
                if ((Area.box[0] + Area.box[1] + Area.box[2] + Area.box[3]) == 0) Area.box = Area.cbox;

                if (dbff != null) Area.Attributes = dbff.ReadRecord(rCounter);
                this.Regions.Add(Area);

                currentPosition = recordStart + (4 + contentLength) * 2;

                Console.SetCursorPosition(left, Console.CursorTop);
                Console.Write("reading {0:0.00}%", ((double)currentPosition)/((double)fileLength)*100.0);                
            };
            Console.SetCursorPosition(left, Console.CursorTop);
            if (dbff != null) dbff.Close();
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

        public static void ExtractPoly(Polygon poly, string fileName, int enlargeKm)
        {
            string fext = Path.GetExtension(fileName);

            string box = fileName.Replace(fext, "_[box].shp");
            string ext = fileName.Replace(fext, "_[ext].shp");

            string kml = fileName.Replace(fext, ".kml");
            string kmb = fileName.Replace(fext, "_[box].kml");
            string kme = fileName.Replace(fext, "_[ext].kml");

            string bbb = fileName.Replace(fext, "_[box].txt");
            string bbe = fileName.Replace(fext, "_[ext].txt");

            Save2Shape(fileName, poly.points);
            Save2KML(kml, poly.points);

            Save2TextUrl(bbb, Poly2TextUrl(poly.box));
            Save2Shape(box, poly.box);
            Save2KML(kmb, poly.box);

            if (enlargeKm > 0)
            {
                double[] enl = Enlarge(poly.box, enlargeKm);
                Save2TextUrl(bbe, Poly2TextUrl(enl));
                Save2Shape(ext, enl);
                Save2KML(kme, enl);
            };
        }

        private static double[] Enlarge(double[] box, double dist_in_km)
        {
            double[] res = new double[4];
            double lon_min = box[0];
            double lat_min = box[1];
            double lon_max = box[2];
            double lat_max = box[3];
            double d_buttom = 1.0 / (Utils.GetLengthMetersA(lat_min - 1, (lon_min + lon_max) / 2, lat_min, (lon_min + lon_max) / 2, false) / 1000.0) * dist_in_km;
            double d_top = 1.0 / (Utils.GetLengthMetersA(lat_max, (lon_min + lon_max) / 2, lat_max + 1, (lon_min + lon_max) / 2, false) / 1000.0) * dist_in_km;
            double d_left = 1.0 / (Utils.GetLengthMetersA((lat_min + lat_max) / 2, lon_min - 1, (lat_min + lat_max) / 2, lon_min, false) / 1000.0) * dist_in_km;
            double d_right = 1.0 / (Utils.GetLengthMetersA((lat_min + lat_max) / 2, lon_max, (lat_min + lat_max) / 2, lon_max + 1, false) / 1000.0) * dist_in_km;
            res = new double[4] { box[0] - d_buttom, box[1] - d_left, box[2] + d_top, box[3] + d_right };
            return res;
        }

        private static string Poly2TextUrl(double[] box)
        {
            PointF[] pass = new PointF[4] { new PointF((float)box[0], (float)box[1]), new PointF((float)box[2], (float)box[1]), new PointF((float)box[2], (float)box[3]), new PointF((float)box[0], (float)box[3]) };
            return Poly2TextUrl(pass);
        }

        private static string Poly2TextUrl(PointF[] poly)
        {
            // [BBIKE_EXTRACT_LINK] //
            double xmin = double.MaxValue;
            double ymin = double.MaxValue;
            double xmax = double.MinValue;
            double ymax = double.MinValue;

            string pll = "";
            for (int i = 0; i < poly.Length; i++)
            {
                xmin = Math.Min(xmin, poly[i].X);
                ymin = Math.Min(ymin, poly[i].Y);
                xmax = Math.Max(xmax, poly[i].X);
                ymax = Math.Max(ymax, poly[i].Y);
                if (pll.Length > 0)
                    pll += "|";
                pll += String.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0},{1}", poly[i].X, poly[i].Y);
            };

            string url = "https://extract.bbbike.org/";
            url += String.Format(System.Globalization.CultureInfo.InvariantCulture,
                "?sw_lng={0}&sw_lat={1}&ne_lng={2}&ne_lat={3}",
                xmin, ymin, xmax, ymax);
            url += "&format=mapsforge-osm.zip";
            url += "&coords=" + pll;
            url += "&city=Noname";
            url += "&lang=en";

            return url;
        }

        private static void Save2TextUrl(string filename, string url)
        {
            if (filename == null) return;

            FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs, Encoding.ASCII);
            sw.WriteLine("[BBIKE_EXTRACT_LINK]");
            sw.WriteLine(url);
            sw.Close();
            fs.Close();
        }

        private static void Save2Shape(string filename, double[] box)
        {
            PointF[] pass = new PointF[4] { new PointF((float)box[0], (float)box[1]), new PointF((float)box[2], (float)box[1]), new PointF((float)box[2], (float)box[3]), new PointF((float)box[0], (float)box[3]) };
            Save2Shape(filename, pass);
        }

        private static void Save2Shape(string filename, PointF[] poly)
        {
            double xmin = double.MaxValue;
            double ymin = double.MaxValue;
            double xmax = double.MinValue;
            double ymax = double.MinValue;

            for (int i = 0; i < poly.Length; i++)
            {
                xmin = Math.Min(xmin, poly[i].X);
                ymin = Math.Min(ymin, poly[i].Y);
                xmax = Math.Max(xmax, poly[i].X);
                ymax = Math.Max(ymax, poly[i].Y);
            };

            List<byte> arr = new List<byte>();
            arr.AddRange(Convert(BitConverter.GetBytes((int)9994), false)); // File Code
            arr.AddRange(new byte[20]);                                    // Not used
            arr.AddRange(Convert(BitConverter.GetBytes((int)((100 + 8 + 48 + 16 * poly.Length) / 2)), false)); // File_Length / 2
            arr.AddRange(Convert(BitConverter.GetBytes((int)1000), true)); // Version 1000
            arr.AddRange(Convert(BitConverter.GetBytes((int)5), true)); // Polygon Type
            arr.AddRange(Convert(BitConverter.GetBytes((double)xmin), true));
            arr.AddRange(Convert(BitConverter.GetBytes((double)ymin), true));
            arr.AddRange(Convert(BitConverter.GetBytes((double)xmax), true));
            arr.AddRange(Convert(BitConverter.GetBytes((double)ymax), true));
            arr.AddRange(new byte[32]); // end of header

            arr.AddRange(Convert(BitConverter.GetBytes((int)1), false)); // rec number
            arr.AddRange(Convert(BitConverter.GetBytes((int)((48 + 16 * poly.Length) / 2)), false));// rec_length / 2
            arr.AddRange(Convert(BitConverter.GetBytes((int)5), true)); // rec type polygon
            arr.AddRange(Convert(BitConverter.GetBytes((double)xmin), true));
            arr.AddRange(Convert(BitConverter.GetBytes((double)ymin), true));
            arr.AddRange(Convert(BitConverter.GetBytes((double)xmax), true));
            arr.AddRange(Convert(BitConverter.GetBytes((double)ymax), true));
            arr.AddRange(Convert(BitConverter.GetBytes((int)1), true)); // 1 part
            arr.AddRange(Convert(BitConverter.GetBytes((int)poly.Length), true)); // 4 points
            arr.AddRange(Convert(BitConverter.GetBytes((int)0), true)); // start at 0 point

            for (int i = 0; i < poly.Length; i++)
            {
                arr.AddRange(Convert(BitConverter.GetBytes((double)poly[i].X), true)); // point 0 x
                arr.AddRange(Convert(BitConverter.GetBytes((double)poly[i].Y), true)); // point 0 y
            };

            FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
            fs.Write(arr.ToArray(), 0, arr.Count);
            fs.Close();
        }

        private static void Save2KML(string filename, double[] box)
        {
            PointF[] pass = new PointF[4] { new PointF((float)box[0], (float)box[1]), new PointF((float)box[2], (float)box[1]), new PointF((float)box[2], (float)box[3]), new PointF((float)box[0], (float)box[3]) };
            Save2KML(filename, pass);
        }

        private static void Save2KML(string filename, PointF[] poly)
        {
            FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8);
            sw.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sw.WriteLine("<kml xmlns=\"http://www.opengis.net/kml/2.2\"><Document>");
            sw.WriteLine("<Placemark><name>My Polygon</name>");
            sw.Write("<Polygon><extrude>1</extrude><outerBoundaryIs><LinearRing><coordinates>");
            foreach (PointF p in poly)
                sw.Write(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1},0 ", p.X, p.Y));
            sw.WriteLine("</coordinates></LinearRing></outerBoundaryIs></Polygon></Placemark>");
            sw.WriteLine("</Document>");
            sw.WriteLine("</kml>");
            sw.Close();
            fs.Close();
        }

        private static byte[] Convert(byte[] ba, bool bigEndian)
        {
            if (BitConverter.IsLittleEndian != bigEndian) Array.Reverse(ba);
            return ba;
        }
    }

    public class Utils
    {
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
                    if (float.IsNaN(result))
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
    }
}
