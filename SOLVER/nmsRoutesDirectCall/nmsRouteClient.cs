//
// Class For 3rd party Companies
// returns XML as through HTTP
// required licenseKey version
// mode: TCP
//

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.InteropServices;

namespace nmsRoutesDirectCall
{
    //
    // HowTo Create
    //   http://krez0n.org.ua/archives/248
    //
    // RegAsm.exe nmsRoutesWebTest.dll /codebase /tlb: xxx.tlb
    // RegAsm.exe nmsRoutesWebTest.dll /tlb: xxx.tlb
    // RegAsm.exe nmsRoutesWebTest.dll /unregister
    //

    [ComVisible(true)]
    [Guid("96C7DD1C-16A2-4bc5-92D5-CC5595ABEB97")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class nmsRouteClient
    {
        private string ip = "127.0.0.1";
        private int port = 7755;

        public nmsRouteClient() {}
        public nmsRouteClient(string IPAddress) { this.ip = IPAddress; }
        public nmsRouteClient(string IPAddress, int Port) { this.ip = IPAddress; this.port = Port; }
        public void SetServerIPAddress(string IPAddress) { this.ip = IPAddress; }
        public string GetServerIPAddress() { return this.ip; }
        public void SetServerPort(int Port) { this.port = Port; }
        public int GetServerPort() { return this.port; }

        /// <summary>
        /// Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик
        ///         0x08 - использовать исторический трафик
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        ///         0x20 - оптимизировать по расстоянию
        ///         0x40 - допускать выезд на дорогу и съезд через встречную полосу
        /// </summary>
        /// <param name="licenseKey"></param>
        /// <param name="stopNames"></param>
        /// <param name="latt"></param>
        /// <param name="lonn"></param>
        /// <param name="startTime"></param>
        /// <param name="flags">
        /// Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик
        ///         0x08 - использовать исторический трафик
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        ///         0x20 - оптимизировать по расстоянию
        ///         0x40 - допускать выезд на дорогу и съезд через встречную полосу
        /// </param>
        /// <returns></returns>
        public string GetRouteXML(string licenseKey, string[] stopNames, double[] latt, double[] lonn, DateTime startTime, long flags)
        {
            string res = "";

            System.Net.Sockets.TcpClient cl = new System.Net.Sockets.TcpClient();
            cl.Connect(ip, port);
            cl.ReceiveTimeout = 5 * 60 * 1000 + 10000; // 5 min as in server + 10s delay  // 5 minutes // 2.5 mins per start // 2.5 mins per end

            byte[] buff = System.Text.Encoding.GetEncoding(1251).GetBytes("dkxce.Route.TCPSolver");
            cl.GetStream().Write(buff, 0, buff.Length);
            cl.GetStream().WriteByte(3);

            buff = GetMethod3RequestData(licenseKey, stopNames, latt, lonn, startTime, flags, null);
            cl.GetStream().Write(buff, 0, buff.Length);

            int read = 0;
            buff = new byte[21];
            while ((read = cl.GetStream().Read(buff, 0, buff.Length)) == 0) ;
            if ((read == 21) && (System.Text.Encoding.GetEncoding(1251).GetString(buff) == "dkxce.Route.TCPSolver") && (cl.GetStream().ReadByte() == 3))
            {
                System.IO.StreamReader sr = new System.IO.StreamReader(cl.GetStream(), System.Text.Encoding.GetEncoding(1251));
                res = sr.ReadToEnd();
            };
            cl.GetStream().Close();
            cl.Close();
            return res;
        }

        /// <summary>
        /// Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик
        ///         0x08 - использовать исторический трафик
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        ///         0x20 - оптимизировать по расстоянию
        ///         0x40 - допускать выезд на дорогу и съезд через встречную полосу
        /// </summary>
        /// <param name="licenseKey"></param>
        /// <param name="stopNames"></param>
        /// <param name="latt"></param>
        /// <param name="lonn"></param>
        /// <param name="startTime"></param>
        /// <param name="flags">
        /// Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик
        ///         0x08 - использовать исторический трафик
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        ///         0x20 - оптимизировать по расстоянию
        ///         0x40 - допускать выезд на дорогу и съезд через встречную полосу
        /// </param>
        /// <returns></returns>
        [ComVisible(false)]
        public string GetRouteXML(string licenseKey, string[] stopNames, double[] latt, double[] lonn, DateTime startTime, long flags,
            System.Drawing.PointF[] roadsExcept, double roadsExceptRaduisInMeters, byte[] RoadsOnly) // ID 7
        {
            string res = "";

            System.Net.Sockets.TcpClient cl = new System.Net.Sockets.TcpClient();
            cl.Connect(ip, port);
            cl.ReceiveTimeout = 5 * 60 * 1000 + 10000; // 5 min as in server + 10s delay  // 5 minutes // 2.5 mins per start // 2.5 mins per end

            byte[] buff = System.Text.Encoding.GetEncoding(1251).GetBytes("dkxce.Route.TCPSolver");
            cl.GetStream().Write(buff, 0, buff.Length);
            cl.GetStream().WriteByte(7);

            buff = GetMethod3RequestData(licenseKey, stopNames, latt, lonn, startTime, flags, null);
            cl.GetStream().Write(buff, 0, buff.Length);

            // RoadsOnly
            byte[] wb = new byte[16];
            if ((RoadsOnly != null) && (RoadsOnly.Length == 16))
                cl.GetStream().Write(RoadsOnly, 0, RoadsOnly.Length);
            else
                cl.GetStream().Write(wb, 0, wb.Length);
            // roadsExceptRaduisInMeters
            wb = BitConverter.GetBytes(roadsExceptRaduisInMeters);
            cl.GetStream().Write(wb, 0, wb.Length);
            // pointsNo
            wb = BitConverter.GetBytes((ushort)(roadsExcept == null ? 0 : roadsExcept.Length));
            cl.GetStream().Write(wb, 0, wb.Length);
            // points
            if ((roadsExcept != null) && (roadsExcept.Length > 0))
            {
                byte[] btw = new byte[2 * 8 * roadsExcept.Length];
                for (int i = 0; i < roadsExcept.Length; i++)
                {
                    wb = BitConverter.GetBytes((double)roadsExcept[i].Y);
                    Array.Copy(wb, 0, btw, i * 2 * 8, 8);
                    wb = BitConverter.GetBytes((double)roadsExcept[i].X);
                    Array.Copy(wb, 0, btw, i * 2 * 8 + 8, 8);
                };
                cl.GetStream().Write(btw, 0, btw.Length);
            };
            // End RoadsOnly   

            int read = 0;
            buff = new byte[21];
            while ((read = cl.GetStream().Read(buff, 0, buff.Length)) == 0) ;
            if ((read == 21) && (System.Text.Encoding.GetEncoding(1251).GetString(buff) == "dkxce.Route.TCPSolver"))
            {
                byte RM = (byte)cl.GetStream().ReadByte();
                if ((RM == 3) || (RM == 7))
                {
                    System.IO.StreamReader sr = new System.IO.StreamReader(cl.GetStream(), System.Text.Encoding.GetEncoding(1251));
                    res = sr.ReadToEnd();
                };
            };
            cl.GetStream().Close();
            cl.Close();
            return res;
        }
        
        /// <summary>
        /// Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик
        ///         0x08 - использовать исторический трафик
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        ///         0x20 - оптимизировать по расстоянию
        ///         0x40 - допускать выезд на дорогу и съезд через встречную полосу
        /// </summary>
        /// <param name="licenseKey"></param>
        /// <param name="stopNames"></param>
        /// <param name="latt"></param>
        /// <param name="lonn"></param>
        /// <param name="startTime"></param>
        /// <param name="flags">
        /// Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик
        ///         0x08 - использовать исторический трафик
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        ///         0x20 - оптимизировать по расстоянию
        ///         0x40 - допускать выезд на дорогу и съезд через встречную полосу
        /// </param>
        /// <returns></returns>
        public string GetRouteXML(string licenseKey, string[] stopNames, double[] latt, double[] lonn, DateTime startTime, long flags,
            double[] roadsExceptLatt, double[] roadsExceptLonn, double roadsExceptRaduisInMeters, byte[] RoadsOnly)
        {
            System.Drawing.PointF[] roadsExcept = new System.Drawing.PointF[roadsExceptLatt.Length];
            for (int i = 0; i < roadsExcept.Length; i++)
                roadsExcept[i] = new System.Drawing.PointF((float)roadsExceptLonn[i], (float)roadsExceptLatt[i]);
            return GetRouteXML(licenseKey, stopNames, latt, lonn, startTime, flags, roadsExcept, roadsExceptRaduisInMeters, RoadsOnly);
        }

        /// <summary>
        /// Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик
        ///         0x08 - использовать исторический трафик
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        ///         0x20 - оптимизировать по расстоянию
        ///         0x40 - допускать выезд на дорогу и съезд через встречную полосу
        /// </summary>
        /// <param name="licenseKey"></param>
        /// <param name="stopList"></param>
        /// <param name="startTime"></param>
        /// <param name="flags">
        /// Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик
        ///         0x08 - использовать исторический трафик
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        ///         0x20 - оптимизировать по расстоянию
        ///         0x40 - допускать выезд на дорогу и съезд через встречную полосу
        /// </param>
        /// <returns></returns>
        [ComVisible(false)]
        public string GetRouteXML(string licenseKey, nmsRouteClientStopList stopList, DateTime startTime, long flags)
        {
            if (stopList == null) throw new Exception("You must set stopList first");
            return GetRouteXML(licenseKey, stopList.GetStopNames(), stopList.GetLatt(), stopList.GetLonn(), startTime, flags);
        }

        /// <summary>
        /// Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик
        ///         0x08 - использовать исторический трафик
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        ///         0x20 - оптимизировать по расстоянию
        ///         0x40 - допускать выезд на дорогу и съезд через встречную полосу
        /// </summary>
        /// <param name="licenseKey"></param>
        /// <param name="stopList"></param>
        /// <param name="startTime"></param>
        /// <param name="flags">
        /// Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик
        ///         0x08 - использовать исторический трафик
        ///         0x20 - оптимизировать по расстоянию
        ///         0x40 - допускать выезд на дорогу и съезд через встречную полосу
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        /// </param>
        /// <returns></returns>
        [ComVisible(false)]
        public string GetRouteXML(string licenseKey, nmsRouteClientStopList stopList, DateTime startTime, long flags,
            System.Drawing.PointF[] roadsExcept, double roadsExceptRaduisInMeters, byte[] RoadsOnly) // ID 7
        {
            if (stopList == null) throw new Exception("You must set stopList first");
            return GetRouteXML(licenseKey, stopList.GetStopNames(), stopList.GetLatt(), stopList.GetLonn(), startTime, flags, roadsExcept, roadsExceptRaduisInMeters, RoadsOnly);
        }

        /// <summary>
        /// Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик
        ///         0x08 - использовать исторический трафик
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        ///         0x20 - оптимизировать по расстоянию
        ///         0x40 - допускать выезд на дорогу и съезд через встречную полосу
        /// </summary>
        /// <param name="licenseKey"></param>
        /// <param name="stopList"></param>
        /// <param name="startTime"></param>
        /// <param name="flags">
        /// Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик
        ///         0x08 - использовать исторический трафик
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        ///         0x20 - оптимизировать по расстоянию
        ///         0x40 - допускать выезд на дорогу и съезд через встречную полосу
        /// </param>
        /// <param name="roadsExceptLatt"></param>
        /// <param name="roadsExceptLonn"></param>
        /// <param name="roadsExceptRaduisInMeters"></param>
        /// <param name="RoadsOnly"></param>
        /// <returns></returns>
        [ComVisible(false)]
        public string GetRouteXML(string licenseKey, nmsRouteClientStopList stopList, DateTime startTime, long flags,
            double[] roadsExceptLatt, double[] roadsExceptLonn, double roadsExceptRaduisInMeters, byte[] RoadsOnly) // ID 7
        {
            if (stopList == null) throw new Exception("You must set stopList first");
            return GetRouteXML(licenseKey, stopList.GetStopNames(), stopList.GetLatt(), stopList.GetLonn(), startTime, flags, roadsExceptLatt, roadsExceptLonn, roadsExceptRaduisInMeters, RoadsOnly);
        }

        /// <summary>
        ///     Create Route Result Object from XML Data
        /// </summary>
        /// <param name="xml">xml data from GetRouteXML</param>
        /// <returns>Route Result</returns>
        public Route XMLToObject(string xml)
        {
            if (String.IsNullOrEmpty(xml)) return null;
            System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(Route));
            System.IO.MemoryStream ms = new System.IO.MemoryStream();
            byte[] bb = System.Text.Encoding.UTF8.GetBytes(xml);
            ms.Write(bb, 0, bb.Length);
            ms.Flush();
            ms.Position = 0;
            System.IO.StreamReader reader = new System.IO.StreamReader(ms);
            Route c = (Route)xs.Deserialize(reader);
            reader.Close();
            return c;
        }


        public string GetNearRoadXML(string licenseKey, double[] lat, double[] lon, bool getNames)
        {
            string res = "";

            System.Net.Sockets.TcpClient cl = new System.Net.Sockets.TcpClient();
            cl.Connect(ip, port);
            cl.ReceiveTimeout = 5 * 60 * 1000 + 10000; // 5 min as in server + 10s delay  // 5 minutes // 2.5 mins per start // 2.5 mins per end

            byte[] buff = System.Text.Encoding.GetEncoding(1251).GetBytes("dkxce.Route.TCPSolver");
            cl.GetStream().Write(buff, 0, buff.Length);
            cl.GetStream().WriteByte(5);

            buff = GetMethod5RequestData(licenseKey, lat, lon, getNames);
            cl.GetStream().Write(buff, 0, buff.Length);

            int read = 0;
            buff = new byte[21];
            while ((read = cl.GetStream().Read(buff, 0, buff.Length)) == 0) ;
            if ((read == 21) && (System.Text.Encoding.GetEncoding(1251).GetString(buff) == "dkxce.Route.TCPSolver") && (cl.GetStream().ReadByte() == 5))
            {
                System.IO.StreamReader sr = new System.IO.StreamReader(cl.GetStream(), System.Text.Encoding.GetEncoding(1251));
                res = sr.ReadToEnd();
                System.Reflection.Assembly.LoadFrom("");
            };
            cl.GetStream().Close();
            cl.Close();
            return res;
        }          

        private byte[] GetMethod3RequestData(string licenseKey, string[] stopNames, double[] latt, double[] lonn, DateTime startTime, long flags, int[] regions)
        {
            if ((stopNames == null) || (latt == null) || (lonn == null)) return null;
            if ((stopNames.Length != latt.Length) || (stopNames.Length != lonn.Length)) return null;
            if ((stopNames.Length < 2) || ((stopNames.Length > 100))) return null;

            List<byte> ba = new List<byte>();
            ba.Add((byte)licenseKey.Length);
            ba.AddRange(System.Text.Encoding.GetEncoding(1251).GetBytes(licenseKey));
            ba.Add((byte)stopNames.Length);
            for (int i = 0; i < stopNames.Length; i++)
            {
                ba.Add((byte)stopNames[i].Length);
                ba.AddRange(System.Text.Encoding.GetEncoding(1251).GetBytes(stopNames[i]));
                ba.AddRange(BitConverter.GetBytes(latt[i]));
                ba.AddRange(BitConverter.GetBytes(lonn[i]));
            };
            ba.AddRange(BitConverter.GetBytes(startTime.ToOADate()));
            ba.AddRange(BitConverter.GetBytes(flags));
            if (regions == null)
                ba.Add(0);
            else
            {
                ba.Add((byte)regions.Length);
                for (int i = 0; i < regions.Length; i++)
                    ba.Add((byte)regions[i]);
            };
            return ba.ToArray();
        }

        private byte[] GetMethod5RequestData(string licenseKey, double[] lat, double[] lon, bool getNames)
        {
            List<byte> ba = new List<byte>();
            ba.Add((byte)licenseKey.Length);
            ba.AddRange(System.Text.Encoding.GetEncoding(1251).GetBytes(licenseKey));
            ba.Add((byte)(getNames ? 1 : 0));
            ba.AddRange(BitConverter.GetBytes((UInt16)lat.Length));
            for (int i = 0; i < lat.Length; i++)
            {
                ba.AddRange(BitConverter.GetBytes((double)lat[i]));
                ba.AddRange(BitConverter.GetBytes((double)lon[i]));
            };
            return ba.ToArray();
        }
    }

    [ComVisible(true)]
    [Guid("96C7DD1C-7F47-405b-9FC0-6E4CEF088E0E")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class nmsRouteClientStopList
    {
        private List<string> nam = new List<string>();
        private List<double> lat = new List<double>();
        private List<double> lon = new List<double>();

        public nmsRouteClientStopList() { }

        public int Count { get { return nam.Count; } }

        public void AddStop(string nam, double lat, double lon)
        {
            if (nam.Length > 200) throw new Exception("Stop name must be less 200 symbols length");
            this.nam.Add(nam);
            this.lat.Add(lat);
            this.lon.Add(lon);            
        }

        public string[] GetStopNames() { return nam.ToArray(); }
        public double[] GetLatt() { return lat.ToArray(); }
        public double[] GetLonn() { return lon.ToArray(); }
    }

    [ComVisible(true)]
    [Guid("96C7DD1C-B2F6-42bc-98C5-A0D6867CCECE")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class nmsRouteClientRoadsOnly
    {
        private List<byte> arr = new List<byte>(new byte[16]);

        public nmsRouteClientRoadsOnly() { }

        public void SetByte(byte No, byte value)
        {
            arr[No] = value;
        }

        public byte[] GetArray() { return arr.ToArray(); }
    }

    [ComVisible(true)]
    [Serializable]
    [Guid("96C7DD1C-8043-489d-8C27-3E5DC5FFEF77")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class Stop
    {
        /// <summary>
        ///     Имя
        /// </summary>
        [XmlText]
        public string name = "";
        /// <summary>
        ///     Широта
        /// </summary>
        [XmlAttribute()]
        public double lat = 0;
        /// <summary>
        ///     Долгота
        /// </summary>
        [XmlAttribute()]
        public double lon = 0;

        public Stop() { }

        public Stop(string name, double lat, double lon)
        {
            this.name = name;
            this.lat = lat;
            this.lon = lon;
        }
    }

    [ComVisible(true)]
    [Serializable]
    [Guid("96C7DD1C-9845-4ead-B202-729A0D23931E")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class XYPoint
    {
        /// <summary>
        ///     Долгота
        /// </summary>
        [XmlAttribute()]
        public double x = 0;
        /// <summary>
        ///     Широта
        /// </summary>
        [XmlAttribute()]
        public double y = 0;

        public XYPoint(double x, double y)
        {
            this.x = x;
            this.y = y;
        }

        public XYPoint() { }
    }

    [ComVisible(true)]
    [Serializable]
    [Guid("96C7DD1C-9845-4ead-B202-711A0D23031D")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class RoutePoint
    {
        public RoutePoint() { }

        public RoutePoint(int no, double x, double y, double segmentLength, double segmentTime, double totalLength, DateTime totalTime)
        {
            this.no = no;
            this.x = x;
            this.y = y;
            this.sLen = segmentLength;
            this.sTime = segmentTime;
            this.tLen = totalLength;
            this.tTime = totalTime;
        }

        /// <summary>
        ///     Нумрация с 1
        /// </summary>
        [XmlAttribute()]
        public int no = 0;

        /// <summary>
        ///     Инструкция1
        /// </summary>
        public string iToDo = "";
        /// <summary>
        ///     Инструкция2
        /// </summary>
        public string iToGo = "";
        /// <summary>
        ///     Инструкция1
        /// </summary>
        public string iStreet = "";

        /// <summary>
        ///     Долгота
        /// </summary>
        [XmlAttribute()]
        public double x = 0;
        /// <summary>
        ///     Широта
        /// </summary>
        [XmlAttribute()]
        public double y = 0;

        /// <summary>
        ///     Время текущего сегмента в мин
        /// </summary>
        [XmlAttribute()]
        public double sTime = 0;

        /// <summary>
        ///     Длина текущего сегмента в км
        /// </summary>
        [XmlAttribute()]
        public double sLen = 0;

        /// <summary>
        ///     Время прибытия в начало сегмента
        /// </summary>
        [XmlAttribute()]
        public DateTime tTime = DateTime.Now;

        /// <summary>
        ///     Длина от начала маршрута до сегмента
        /// </summary>
        [XmlAttribute()]
        public double tLen = 0;
    }

    [ComVisible(true)]
    [Serializable]
    [Guid("96C7DD1C-BD78-4b5b-88F8-851214C3ADBE")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class Route
    {
        public Route() { }

        /// <summary>
        ///     Длина маршрута в км
        /// </summary>
        public double driveLength = 0;
        /// <summary>
        ///      расстояние между промежуточными точками маршрута
        /// </summary>
        [XmlArrayItem("dls")]
        public double[] driveLengthSegments = new double[0];
        /// <summary>
        ///     Время в пути в мин
        /// </summary>
        public double driveTime = 0;
        /// <summary>
        ///      время между промежуточными точками маршрута
        /// </summary>
        [XmlArrayItem("dts")]
        public double[] driveTimeSegments = new double[0];
        /// <summary>
        ///     Время выезда
        /// </summary>
        public DateTime startTime = DateTime.Now;
        /// <summary>
        ///     Время прибытия
        /// </summary>
        public DateTime finishTime = DateTime.Now;
        /// <summary>
        ///     Маршрутные точки
        /// </summary>
        [XmlArrayItem("stop")]
        public Stop[] stops = new Stop[0];
        /// <summary>
        ///     полилиния маршрута
        /// </summary>
        [XmlArrayItem("p")]
        public XYPoint[] polyline = new XYPoint[0];
        /// <summary>
        ///      индекс, указывающий на элемент массива polyline для каждого
        ///      участка между промежуточными точками маршрута
        /// </summary>
        [XmlArrayItem("ps")]
        public int[] polylineSegments = new int[0];
        /// <summary>
        ///     инструкции
        /// </summary>
        [XmlArrayItem("i")]
        public RoutePoint[] instructions = new RoutePoint[0];
        /// <summary>
        ///      индекс, указывающий на элемент массива instructions для каждого
        ///      участка между промежуточными точками маршрута
        /// </summary>
        [XmlArrayItem("is")]
        public int[] instructionsSegments = new int[0];

        /// <summary>
        ///     Ошибка, если есть
        /// </summary>
        public string LastError = String.Empty;
    }
}
