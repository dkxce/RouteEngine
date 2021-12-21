/* 
 * RouteLL c# class by Milok Zbrozek <milokz@gmail.com>
 * Модуль для индексации координат узлов графа
 * Author: Milok Zbrozek <milokz@gmail.com>
 */


using System;
using System.Collections.Generic;
using System.Text;

namespace dkxce.GraphUtils
{
    public class PointNLL
    {
        public uint node;
        public float lat;
        public float lon;
        public PointNLL(uint node, float lat, float lon)
        {
            this.node = node;
            this.lat = lat;
            this.lon = lon;
        }
        public override string ToString()
        {
            return "{" + node + "; " + lat.ToString() + " " + lon.ToString() + "}";
        }
    }

    public class PointNLLD : PointNLL
    {
        public float dist;
        public PointNLLD(uint node, float lat, float lon)
            : base(node,lat,lon)
        {
            this.dist = 0;
        }
        public PointNLLD(uint node, float lat, float lon, float dist)
            : base(node, lat, lon)
        {
            this.node = node;
            this.lat = lat;
            this.lon = lon;
            this.dist = dist;
        }
        public PointNLLD(PointNLL point)
            : base(point.node, point.lat, point.lon)
        {
            this.dist = 0;
        }
        public PointNLLD(PointNLL point, float dist)
            : base(point.node, point.lat, point.lon)
        {
            this.dist = dist;
        }
    }

    public class PointNLLIndexer
    {
        /*
         * main file [s0] geo:
         * HEADER
         * [int] nodes_count
         * array[1..nodes] of [[float][float]] - lat,lon = 8 bytes
         * 
         * 
         * lat lon file [s1] .ll:
         * HEADER
         * [int] nodes_count
         * array[1..nodes] of [[uint][float][float]] - node,lat,lon = 12 bytes
         * 
         */

        private static byte[] header0 = new byte[] { 0x52, 0x4D, 0x50, 0x4F, 0x49, 0x4E, 0x54, 0x4E, 0x4C, 0x4C, 0x30 };
        private static byte[] header1 = new byte[] { 0x52, 0x4D, 0x50, 0x4F, 0x49, 0x4E, 0x54, 0x4E, 0x4C, 0x4C, 0x31 };

        List<PointNLL> arr = new List<PointNLL>();

        public void Add(PointNLL point) { arr.Add(point);}

        /// <summary>
        ///     Add Lat Lon as Node & get a number
        /// </summary>
        /// <param name="lat">Latitude</param>
        /// <param name="lon">Longitude</param>
        public uint AddNew(float lat, float lon) 
        { 
            arr.Add(new PointNLL((uint)arr.Count + 1, lat, lon));
            return (uint)arr.Count;
        }

        /// <summary>
        ///     Find Bode By Coordinates
        /// </summary>
        /// <param name="lat">Latitude</param>
        /// <param name="lon">Longitude</param>
        /// <returns>node number</returns>
        public uint NodeByLatLon(float lat, float lon)
        {
            uint res = 0;
            for (int i = 0; i < arr.Count; i++)
                if ((arr[i].lat == lat) && (arr[i].lon == lon))
                {
                    res = arr[i].node;
                    break;
                };
            return res;
        }

        public void OptimizeSaveAndClose(string fileName)
        {
            arr.Sort(PointNLLSorter.SortByNode());
            System.IO.FileStream fs = new System.IO.FileStream(fileName, System.IO.FileMode.Create);
            fs.Write(header0, 0, header0.Length);
            byte[] ba = BitConverter.GetBytes(arr.Count);
            fs.Write(ba, 0, ba.Length);
            for (int i = 0; i < arr.Count; i++)
            {
                ba = BitConverter.GetBytes(arr[i].lat);
                fs.Write(ba, 0, ba.Length);
                ba = BitConverter.GetBytes(arr[i].lon);
                fs.Write(ba, 0, ba.Length);
            };
            fs.Flush();
            fs.Close();

            arr.Sort(PointNLLSorter.SortByLL());
            fs = new System.IO.FileStream(fileName+".ll", System.IO.FileMode.Create);
            fs.Write(header1, 0, header1.Length);
            ba = BitConverter.GetBytes(arr.Count);
            fs.Write(ba, 0, ba.Length);
            for (int i = 0; i < arr.Count; i++)
            {
                ba = BitConverter.GetBytes(arr[i].node);
                fs.Write(ba, 0, ba.Length);
                ba = BitConverter.GetBytes(arr[i].lat);
                fs.Write(ba, 0, ba.Length);
                ba = BitConverter.GetBytes(arr[i].lon);
                fs.Write(ba, 0, ba.Length);
            };
            fs.Flush();
            fs.Close();
        }

        private class PointNLLSorter: IComparer<PointNLL>
        {
            private byte sortBy = 0;
            private PointNLLSorter(byte sortBy) { this.sortBy = sortBy; }
            public static PointNLLSorter SortByNode() { return new PointNLLSorter(0); }
            public static PointNLLSorter SortByLL() { return new PointNLLSorter(1); }
            public int Compare(PointNLL a, PointNLL b)
            {
                if (sortBy == 0) return a.node.CompareTo(b.node);

                int res = a.lat.CompareTo(b.lat);
                if (res == 0) res = a.lon.CompareTo(b.lon);
                if (res == 0) res = a.node.CompareTo(b.node);
                return res;
            }
        }
    }

    public class PointNLLSearcher
    {
        private static byte[] header0 = new byte[] { 0x52, 0x4D, 0x50, 0x4F, 0x49, 0x4E, 0x54, 0x4E, 0x4C, 0x4C, 0x30 };
        private static byte[] header1 = new byte[] { 0x52, 0x4D, 0x50, 0x4F, 0x49, 0x4E, 0x54, 0x4E, 0x4C, 0x4C, 0x31 };

        private System.IO.Stream s0;
        private System.IO.Stream s1;
        private int size = 0;

        /// <summary>
        ///     Nodes count
        /// </summary>
        public int Nodes { get { return size; } }

        private PointNLLSearcher() { }
        
        public static PointNLLSearcher LoadToMemory(string fileName)
        {
            PointNLLSearcher schr = new PointNLLSearcher();
            schr.s0 = new System.IO.MemoryStream();
            schr.s1 = new System.IO.MemoryStream();

            System.IO.FileStream fs = new System.IO.FileStream(fileName, System.IO.FileMode.Open);
            byte[] block = new byte[8192];
            int read = 0;
            while ((read = fs.Read(block, 0, 8192)) > 0)
                schr.s0.Write(block, 0, read);
            fs.Close();

            schr.s0.Position = 0;
            byte[] ba = new byte[header0.Length];
            schr.s0.Read(ba, 0, ba.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(ba) != "RMPOINTNLL0")
            {
                schr.s0.Close();
                throw new System.IO.IOException("Unknown file format:\r\n" + fileName);
            };
            ba = new byte[4];
            schr.s0.Read(ba, 0, 4);
            schr.size = BitConverter.ToInt32(ba, 0);

            fs = new System.IO.FileStream(fileName + ".ll", System.IO.FileMode.Open);
            block = new byte[8192];
            read = 0;
            while ((read = fs.Read(block, 0, 8192)) > 0)
                schr.s1.Write(block, 0, read);
            fs.Close();

            schr.s1.Position = 0;
            ba = new byte[header1.Length];
            schr.s1.Read(ba, 0, ba.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(ba) != "RMPOINTNLL1")
            {
                schr.s0.Close();
                schr.s1.Close();
                throw new System.IO.IOException("Unknown file format:\r\n" + fileName + ".ll");
            };
            ba = new byte[4];
            schr.s1.Read(ba, 0, 4);
            int size = BitConverter.ToInt32(ba, 0);

            return schr;
        }

        public static PointNLLSearcher WorkWithDisk(string fileName)
        {
            PointNLLSearcher schr = new PointNLLSearcher();
            schr.s0 = new System.IO.FileStream(fileName, System.IO.FileMode.Open);
            schr.s1 = new System.IO.FileStream(fileName + ".ll", System.IO.FileMode.Open);

            schr.s0.Position = 0;
            byte[] ba = new byte[header0.Length];
            schr.s0.Read(ba, 0, ba.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(ba) != "RMPOINTNLL0")
            {
                schr.s0.Close();
                throw new System.IO.IOException("Unknown file format:\r\n" + fileName);
            };
            ba = new byte[4];
            schr.s0.Read(ba, 0, 4);
            schr.size = BitConverter.ToInt32(ba, 0);

            ba = new byte[header1.Length];
            schr.s1.Read(ba, 0, ba.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(ba) != "RMPOINTNLL1")
            {
                schr.s0.Close();
                schr.s1.Close();
                throw new System.IO.IOException("Unknown file format:\r\n" + fileName + ".ll");
            };
            ba = new byte[4];
            schr.s1.Read(ba, 0, 4);
            int size = BitConverter.ToInt32(ba, 0);

            return schr;
        }

        public void Close()
        {
            s0.Close();
            s1.Close();
        }

        /// <summary>
        ///     Ищем точки в заданной зоне
        /// </summary>
        /// <param name="StartLat"></param>
        /// <param name="StartLon"></param>
        /// <param name="EndLat"></param>
        /// <param name="EndLon"></param>
        /// <param name="getLatLon">возвращать ли координаты точек</param>
        /// <param name="maxResults">Максимальное число точек к ответе</param>
        /// <returns></returns>
        public PointNLL[] FindIn(float StartLat, float StartLon, float EndLat, float EndLon, bool getLatLon, int maxResults)
        {
            List<PointNLL> res = new List<PointNLL>(maxResults);

            s1.Position = header1.Length + 4;
            byte[] buff = new byte[1020]; // 1020 bytes - 85 points // 8184 bytes - 682 points
            int read = 0;
            bool brk = false;
            while ((read = s1.Read(buff,0,buff.Length)) > 0)
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
                        res.Add(new PointNLL(node, lat, lon));
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
            return res.ToArray();
        }

        /// <summary>
        ///     Ищем точки в заданной зоне
        /// </summary>
        /// <param name="StartLat"></param>
        /// <param name="StartLon"></param>
        /// <param name="EndLat"></param>
        /// <param name="EndLon"></param>
        /// <param name="getLatLon">возвращать ли координаты точек</param>
        /// <returns></returns>
        public PointNLL[] FindIn(float StartLat, float StartLon, float EndLat, float EndLon, bool getLatLon)
        {
            return FindIn(StartLat, StartLon, EndLat, EndLon, getLatLon, 1000);
        }

        /// <summary>
        ///     Ищем точки в радиусе
        /// </summary>
        /// <param name="Lat">Широта центра</param>
        /// <param name="Lon">Долгота центра</param>
        /// <param name="metersRadius">Радиус в метрах</param>
        /// <param name="getLatLon">Получать координаты точек</param>
        /// <param name="getDistance">Получать расстояние до точки</param>
        /// <param name="sortByDist">Сортировать точки по удаленности</param>
        /// <returns></returns>
        public PointNLL[] FindIn(float Lat, float Lon, float metersRadius, bool getLatLon, bool getDistance, bool sortByDist)
        {
            return FindIn(Lat, Lon, metersRadius, getLatLon, getDistance, sortByDist);
        }

        /// <summary>
        ///     Ищем точки в радиусе
        /// </summary>
        /// <param name="Lat">Широта центра</param>
        /// <param name="Lon">Долгота центра</param>
        /// <param name="metersRadius">Радиус в метрах</param>
        /// <param name="getLatLon">Получать координаты точек</param>
        /// <param name="getDistance">Получать расстояние до точки</param>
        /// <param name="sortByDist">Сортировать точки по удаленности</param>
        /// <param name="maxResults">Максимальное число точек в ответе</param>
        /// <returns></returns>
        public PointNLLD[] FindIn(float Lat, float Lon, float metersRadius, bool getLatLon, bool getDistance, bool sortByDist, int maxResults)
        {
            float dLat = metersRadius / GetLengthMetersC(Lat, Lon, Lat + 1, Lon, false);
            float dLon = metersRadius / GetLengthMetersC(Lat, Lon, Lat, Lon + 1, false);
            PointNLL[] res = FindIn(Lat - dLat, Lon - dLon, Lat + dLat, Lon + dLon, getLatLon || getDistance, maxResults);
            PointNLLD[] wd = new PointNLLD[res.Length];
            if (getDistance)
            {
                for (int i = 0; i < res.Length; i++)
                    wd[i] = new PointNLLD(res[i], GetLengthMetersC(Lat, Lon, res[i].lat, res[i].lon, false));
                if (sortByDist)
                    Array.Sort<PointNLLD>(wd, new PointNLLDSorter());
            }
            else
            {
                for (int i = 0; i < res.Length; i++)
                    wd[i] = new PointNLLD(res[i]);
            };
            return wd;
        }

        /// <summary>
        ///     Find node with Coordinates
        /// </summary>
        /// <param name="Lat">Latitude</param>
        /// <param name="Lon">Longitude</param>
        /// <returns>Node Id</returns>
        public uint FindNode(float Lat, float Lon)
        {
            PointNLL[] pnts = FindIn(Lat, Lon, Lat, Lon, false);
            if (pnts.Length == 0) return 0;
            else return pnts[0].node;
        }

        /// <summary>
        ///     Получаем координаты точек
        /// </summary>
        /// <param name="nodes">Номера точек</param>
        /// <returns></returns>
        public PointNLL[] GetCoordinates(uint[] nodes)
        {
            PointNLL[] res = new PointNLL[nodes.Length];
            byte[] ba = new byte[8];
            for (int i = 0; i < nodes.Length; i++)
            {
                s0.Position = header0.Length + 4 + (nodes[i] - 1) * 8;
                s0.Read(ba, 0, ba.Length);
                float lat = BitConverter.ToSingle(ba, 0);
                float lon = BitConverter.ToSingle(ba, 4);
                res[i] = new PointNLL(nodes[i], lat, lon);
            };
            return res;
        }

        /// <summary>
        ///     Получаем координаты точек и путь
        ///     по прямой из предыдущей точки в каждую
        /// </summary>
        /// <param name="nodes"></param>
        /// <returns></returns>
        public PointNLLD[] GetCoordinatesDist(uint[] nodes)
        {
            PointNLLD[] res = new PointNLLD[nodes.Length];
            byte[] ba = new byte[8];
            for (int i = 0; i < nodes.Length; i++)
            {
                s0.Position = header0.Length + 4 + (i - 1) * 8;
                s0.Read(ba, 0, ba.Length);
                float lat = BitConverter.ToSingle(ba, 0);
                float lon = BitConverter.ToSingle(ba, 4);
                float dist = i == 0 ? 0 : GetLengthMetersC(res[i-1].lat, res[i-1].lon, res[i].lat, res[i].lon, false);
                res[i] = new PointNLLD(nodes[i], lat, lon, dist);
            };
            return res;
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
        private static float GetLengthMetersC(double StartLat, double StartLong, double EndLat, double EndLong, bool radians)
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

            return (float)Math.Round(dDistance);
        }

        public class PointNLLDSorter : IComparer<PointNLLD>
        {
            public int Compare(PointNLLD a, PointNLLD b)
            {
                int res = a.dist.CompareTo(b.dist);
                if (res == 0) res = a.node.CompareTo(b.node);
                return res;
            }
        }
    }
}
