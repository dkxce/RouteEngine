/* 
 * C# Class by Milok Zbrozek <milokz@gmail.com>
 * Author: Milok Zbrozek <milokz@gmail.com> 
 */


using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.InteropServices;

using dkxce.Route.Classes;
using dkxce.Route.WayList;

namespace dkxce.Route.ISolver
{
    /// <summary>
    ///     Маршруные точки
    /// </summary>
    [Serializable]
    public class RStop : XYO
    {
        /// <summary>
        ///     Имя
        /// </summary>
        public string name = "";
        ///// <summary>
        /////     Широта
        ///// </summary>
        //[XmlAttribute()]
        //public double lat = 0;
        ///// <summary>
        /////     Долгота
        ///// </summary>
        //[XmlAttribute()]
        //public double lon = 0;

        public RStop(string name, double lat, double lon)
        {
            this.name = name;
            this.lat = lat;
            this.lon = lon;
        }
    }

    /// <summary>
    ///     Результат расчета маршрута
    /// </summary>
    [Serializable]
    public class RResult
    {
        public RResult(RStop[] stops)
        {
            this.stops = stops;
        }

        /// <summary>
        ///     Длина маршрута в м
        /// </summary>
        public double driveLength;        

        /// <summary>
        ///     Время в пути в мин
        /// </summary>
        public double driveTime;        

        /// <summary>
        ///     Время выезда
        /// </summary>
        public DateTime startTime = DateTime.Now;

        /// <summary>
        ///     Время прибытия
        /// </summary>
        public DateTime finishTime = DateTime.Now;

        /// <summary>
        ///     полилиния маршрута
        /// </summary>
        public PointFL[] vector;
        /// <summary>
        ///     Массив чисел, каждый элемент из которого
        ///     соответствует пункту промежуточной точки маршрута (массив stops),
        ///     а значение которого указывает на точку полилинии (ячейку в массиве vector),
        ///     обозначающий начало участка пути из этой промежуточной точки
        /// </summary>
        public int[] vectorSegments;

        /// <summary>
        ///     инструкции
        /// </summary>
        public RDPoint[] description;
        /// <summary>
        ///     Массив чисел, каждый элемент из которого
        ///     соответствует пункту промежуточной точки маршрута (массив stops),
        ///     а значение которого указывает на индекс инструкции (ячейку в массиве description),
        ///     обозначающий начало описания пути из этой промежуточной точки
        /// </summary>
        public int[] descriptionSegments;

        /// <summary>
        ///     Длины сегментов маршрута в м
        /// </summary>
        public double[] driveLengthSegments;

        /// <summary>
        ///     Время в пути по сегментам в мин
        /// </summary>
        public double[] driveTimeSegments;
        

        /// <summary>
        ///     Ошибка, если есть
        /// </summary>
        public string LastError = String.Empty;

        /// <summary>
        ///     Точки маршрута
        /// </summary>
        public RStop[] stops = new RStop[0];
    }

    /// <summary>
    ///     Привязка точки к дороге
    /// </summary>
    [Serializable]
    public class RNearRoad
    {
        /// <summary>
        ///     Расстояние от искомой точки до дороги в метрах
        /// </summary>
        public double distance;
        /// <summary>
        ///     Широта ближайшей дороги
        /// </summary>
        public double lat;
        /// <summary>
        ///     Долгота ближайшей дороги
        /// </summary>
        public double lon;
        /// <summary>
        ///     Наименование ближайшей дороги
        /// </summary>
        public string name;
        /// <summary>
        ///     Атрибуты дороги
        /// </summary>
        public string attributes;
        /// <summary>
        ///     Region ID
        /// </summary>
        public int region;

        public RNearRoad(double dist, double lat, double lon, string nam)
        {
            this.distance = dist;
            this.lat = lat;
            this.lon = lon;
            this.name = nam;
        }

        public RNearRoad(double dist, double lat, double lon, string nam, string attr, int region)
        {
            this.distance = dist;
            this.lat = lat;
            this.lon = lon;
            this.name = nam;
            this.attributes = attr;
            this.region = region;
        }
    }

    /// <summary>
    ///     Параметры для првязки точек к дорогам
    /// </summary>
    [Serializable]
    public class RNearRoadAsk
    {
        public string licenseKey;
        public double[] lat;
        public double[] lon;
        public bool getNames;

        public RNearRoadAsk(string licenseKey, double[] lat, double[] lon, bool getNames)
        {
            this.licenseKey = licenseKey;
            this.lat = lat;
            this.lon = lon;
            this.getNames = getNames;
        }
    }

    /// <summary>
    ///     Параметры для расчета маршрута
    /// </summary>
    [Serializable]
    public class RAsk
    {
        public string licenseKey;
        public RStop[] stops;
        public DateTime startTime;
        public long flags;
        public int[] RegionsAvailableToUser;

        public RAsk() { }
        public RAsk(string licenseKey, RStop[] stops, DateTime startTime, long flags, int[] RegionsAvailableToUser)
        {
            this.licenseKey = licenseKey;
            this.stops = stops;
            this.startTime = startTime;
            this.flags = flags;
            this.RegionsAvailableToUser = RegionsAvailableToUser;
        }
    }

    /// <summary>
    ///     Интерфейс описания для расчета маршрута
    /// </summary>
    public interface IRoute
    {
        /// <summary>
        ///     Общее число потоков на сервере
        /// </summary>
        /// <returns></returns>
        int GetThreadsCount(); // ID 0

        /// <summary>
        ///     Номер незанятого потока
        /// </summary>
        /// <returns></returns>
        int GetIdleThreadCount();  // ID 1            
        
        /// <summary>
        ///     Получение маршрута;
        ///     Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик
        ///         0x08 - использовать исторический трафик
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        /// </summary>
        /// <param name="stops">Точки маршрута</param>
        /// <param name="startTime">Начальное время</param>
        /// <param name="flags">флаги</param>
        /// <param name="RegionsAvailableToUser">Регионы доступные для пользователя (null = все)</param>
        /// <returns></returns>
        RResult GetRoute(RStop[] stops, DateTime startTime, long flags, int[] RegionsAvailableToUser); // ID 2

        /// <summary>
        ///     Привязка точки к дороге
        /// </summary>
        /// <param name="Lat">Широта</param>
        /// <param name="Lon">Долгота</param>
        /// <param name="getName">Запрашивать наименование дороги (работает дольше)</param>
        /// <returns>Привязанные к дороге точки</returns>
        RNearRoad[] GetNearRoad(double[] Lat, double[] Lon, bool getName); // ID 4

        /// <summary>
        ///     Получение маршрута;
        ///     Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик
        ///         0x08 - использовать исторический трафик
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        /// </summary>
        /// <param name="stops">Точки маршрута</param>
        /// <param name="startTime">Начальное время</param>
        /// <param name="flags">флаги</param>
        /// <param name="RegionsAvailableToUser">Регионы доступные для пользователя (null = все)</param>
        /// <param name="roadsExcept">Избегая проезд по дорогам сквозь точки [X-Lon,Y-Lat]</param>
        /// <param name="roadsExceptRaduisInMeters">Избегая проезд по дорогам сквозь точки, радиус в метрах</param>
        /// <param name="RoadsOnly">Параметры построения маршрута используя атрибуты дорог</param>
        /// <returns></returns>
        RResult GetRoute(RStop[] stops, DateTime startTime, long flags, int[] RegionsAvailableToUser,
            PointF[] roadsExcept, double roadsExceptRaduisInMeters, byte[] RoadsOnly); // ID 6

        /*
        /// <summary>
        ///     Находим расстояние от исходной/конечной координаты до каждой линии
        /// </summary>
        /// <param name="Lat">Широта</param>
        /// <param name="Lon">Долгота</param>
        /// <param name="metersRadius">Радиус поиска ближайшей линии в метрах</param>
        /// <returns>array[0..NumberOfNearPoints-1][0: distance,1: X or Lon,2: Y or Lat,3: line_id]</returns>
        float[][] FindNearestLines(float Lat, float Lon, float metersRadius);

        /// <summary>
        ///     Находим расстояние от исходной/конечной координаты до каждой линии
        /// </summary>
        /// <param name="Lat">Широта</param>
        /// <param name="Lon">Долгота</param>
        /// <param name="metersRadius">Радиус поиска ближайшей линии в метрах</param>
        /// <returns>array[0..NumOfPoints-1][0..NumberOfNearPoints-1][0: distance,1: X or Lon,2: Y or Lat,3: line_id]</returns>
        float[][][] FindNearestLines(float[] Lat, float[] Lon, float metersRadius);       
        */
    }   

    /// <summary>
    ///     Класс подключения к серверу построения маршрута
    ///     (Через .Net Remoting)
    /// </summary>
    public sealed class IRouteClient
    {       
        /// <summary>
        ///     Подключаеся к серверу построения маршрута   
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public static IRoute Connect(string host, int port)
        {
            return (IRoute)System.Runtime.Remoting.RemotingServices.Connect(typeof(IRoute), string.Format("tcp://{0}:{1}/dkxce.Route.TCPSolver", host, port));
        }

    }    

    /// <summary>
    ///     Клиент для сервиса расчета маршрутов
    ///     (Через TCP/IP протокол)
    /// </summary>
    public class RouteClient : IRoute
    {
        private int port;
        private string ip;
        private string licenseKey;

        public RouteClient(string ip, int port, string licenseKey)
        {
            this.port = port;
            this.ip = ip;
            this.licenseKey = licenseKey;
        }

        /// <summary>
        ///     Возращает общее число доступных(задействованных) потоков для расчета
        /// </summary>
        /// <returns></returns>
        public int GetThreadsCount() // ID 0
        {
            int result = -1;

            System.Net.Sockets.TcpClient cl = new System.Net.Sockets.TcpClient();
            cl.Connect(this.ip, this.port);
            cl.ReceiveBufferSize = 22;
            cl.ReceiveTimeout = 30 * 1000; // 30 sec

            byte[] buff = System.Text.Encoding.GetEncoding(1251).GetBytes("dkxce.Route.TCPSolver");
            cl.GetStream().Write(buff, 0, buff.Length);
            cl.GetStream().WriteByte(0);

            int read = 0;
            buff = new byte[21];
            while ((read = cl.GetStream().Read(buff, 0, buff.Length)) == 0) ;
            if ((read == 21) && (System.Text.Encoding.GetEncoding(1251).GetString(buff) == "dkxce.Route.TCPSolver") && (cl.GetStream().ReadByte() == 0))
                result = cl.GetStream().ReadByte();
            cl.GetStream().Close();
            cl.Close();
            return result;
        }

        /// <summary>
        ///     Возвращает число свободных (незадействованных) потоков для расчета
        /// </summary>
        /// <returns></returns>
        public int GetIdleThreadCount()  // ID 1            
        {
            int result = 0;

            System.Net.Sockets.TcpClient cl = new System.Net.Sockets.TcpClient();
            cl.Connect(this.ip, this.port);
            cl.ReceiveBufferSize = 22;
            cl.ReceiveTimeout = 30 * 1000; // 30 sec

            byte[] buff = System.Text.Encoding.GetEncoding(1251).GetBytes("dkxce.Route.TCPSolver");
            cl.GetStream().Write(buff, 0, buff.Length);
            cl.GetStream().WriteByte(1);

            int read = 0;
            buff = new byte[21];
            while ((read = cl.GetStream().Read(buff, 0, buff.Length)) == 0) ;
            if ((read == 21) && (System.Text.Encoding.GetEncoding(1251).GetString(buff) == "dkxce.Route.TCPSolver") && (cl.GetStream().ReadByte() == 1))
                result = cl.GetStream().ReadByte();
            cl.GetStream().Close();
            cl.Close();
            return result;
        }

        /// <summary>
        ///     Получение маршрута;
        ///     Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик (пока не используется)
        ///         0x08 - использовать исторический трафик (пока не используется)
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        ///         0x20 - оптимизировать по расстоянию
        ///         0x40 - допускать выезд на дорогу и съезд через встречную полосу
        /// </summary>
        /// <param name="stops">Точки маршрута</param>
        /// <param name="startTime">Начальное время</param>
        /// <param name="flags">флаги</param>
        /// <param name="RegionsAvailableToUser">Регионы доступные для пользователя (null = все)</param>
        /// <returns></returns>
        public RResult GetRoute(RStop[] stops, DateTime startTime, long flags, int[] RegionsAvailableToUser) // ID 2
        {
            RResult res = null;

            System.Net.Sockets.TcpClient cl = new System.Net.Sockets.TcpClient();
            cl.Connect(ip, port);
            cl.ReceiveTimeout = 5 * 60 * 1000 + 10000; // 5 min as in server + 10s delay  // 5 minutes // 2.5 mins per start // 2.5 mins per end
            
            byte[] buff = System.Text.Encoding.GetEncoding(1251).GetBytes("dkxce.Route.TCPSolver");
            cl.GetStream().Write(buff, 0, buff.Length);
            cl.GetStream().WriteByte(2);

            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(cl.GetStream(), new RAsk(licenseKey,stops, startTime, flags, RegionsAvailableToUser));

            int read = 0;
            buff = new byte[21];
            while ((read = cl.GetStream().Read(buff, 0, buff.Length)) == 0) ;
            if ((read == 21) && (System.Text.Encoding.GetEncoding(1251).GetString(buff) == "dkxce.Route.TCPSolver") && (cl.GetStream().ReadByte() == 2))
                res = (dkxce.Route.ISolver.RResult)bf.Deserialize(cl.GetStream());
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
        /// <param name="stops"></param>
        /// <param name="startTime"></param>
        /// <param name="flags"></param>
        /// <param name="RegionsAvailableToUser"></param>
        /// <param name="roadsExcept">Избегая проезд по дорогам сквозь точки [X-Lon,Y-Lat]</param>
        /// <param name="roadsExceptRaduisInMeters">Избегая проезд по дорогам сквозь точки, радиус в метрах</param>
        /// <param name="RoadsOnly">Параметры построения маршрута используя атрибуты дорог</param>
        /// <returns></returns>
        public RResult GetRoute(RStop[] stops, DateTime startTime, long flags, int[] RegionsAvailableToUser,
            PointF[] roadsExcept, double roadsExceptRaduisInMeters, byte[] RoadsOnly) // ID 6
        {
            RResult res = null;

            System.Net.Sockets.TcpClient cl = new System.Net.Sockets.TcpClient();
            cl.Connect(ip, port);
            cl.ReceiveTimeout = 5 * 60 * 1000 + 10000; // 5 min as in server + 10s delay  // 5 minutes // 2.5 mins per start // 2.5 mins per end

            byte[] buff = System.Text.Encoding.GetEncoding(1251).GetBytes("dkxce.Route.TCPSolver");
            cl.GetStream().Write(buff, 0, buff.Length);
            cl.GetStream().WriteByte(6);             

            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(cl.GetStream(), new RAsk(licenseKey, stops, startTime, flags, RegionsAvailableToUser));

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
                if((RM == 2) || (RM == 6))
                    res = (dkxce.Route.ISolver.RResult)bf.Deserialize(cl.GetStream());
            };
            cl.GetStream().Close();
            cl.Close();
            return res;
        }

        /// <summary>
        ///     Получение маршрута;
        ///     Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик (пока не используется)
        ///         0x08 - использовать исторический трафик (пока не используется)
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        ///         0x20 - оптимизировать по расстоянию
        ///         0x40 - допускать выезд на дорогу и съезд через встречную полосу
        /// </summary>
        /// <param name="stops">Точки маршрута</param>
        /// <param name="startTime">Начальное время</param>
        /// <param name="flags">флаги</param>
        /// <param name="RegionsAvailableToUser">Регионы доступные для пользователя (null = все)</param>
        /// <returns></returns>
        public string GetRouteXML(RStop[] stops, DateTime startTime, long flags, int[] RegionsAvailableToUser) // ID 3
        {
            string res = "";

            System.Net.Sockets.TcpClient cl = new System.Net.Sockets.TcpClient();
            cl.Connect(ip, port);
            cl.ReceiveTimeout = 5 * 60 * 1000 + 10000; // 5 min as in server + 10s delay  // 5 minutes // 2.5 mins per start // 2.5 mins per end

            byte[] buff = System.Text.Encoding.GetEncoding(1251).GetBytes("dkxce.Route.TCPSolver");
            cl.GetStream().Write(buff, 0, buff.Length);
            cl.GetStream().WriteByte(3);

            string[] ss = new string[stops.Length];
            double[] latt = new double[stops.Length];
            double[] lonn = new double[stops.Length];
            for(int i=0;i<stops.Length;i++)
            {
                ss[i] = stops[i].name;
                latt[i] = stops[i].lat;
                lonn[i] = stops[i].lon;
            };
            buff = GetMethod3RequestData(licenseKey, ss, latt, lonn, startTime, flags, RegionsAvailableToUser);
            cl.GetStream().Write(buff, 0, buff.Length);
            
            int read = 0;
            buff = new byte[21];
            while ((read = cl.GetStream().Read(buff, 0, buff.Length)) == 0) ;
            if ((read == 21) && (System.Text.Encoding.GetEncoding(1251).GetString(buff) == "dkxce.Route.TCPSolver") && (cl.GetStream().ReadByte() == 3))
            {
                System.IO.StreamReader sr = new System.IO.StreamReader(cl.GetStream());
                res = sr.ReadToEnd();
            };
            cl.GetStream().Close();
            cl.Close();
            return res;
        }

        /// <summary>
        ///     Получение маршрута;
        ///     Флаги:
        ///         0x01 - получать полилинию
        ///         0x02 - получать описание
        ///         0x04 - использовать текущий трафик (пока не используется)
        ///         0x08 - использовать исторический трафик (пока не используется)
        ///         0x10 - оптимизировать промежуточные точки маршрута (реорганизация)
        ///         0x20 - оптимизировать по расстоянию
        ///         0x40 - допускать выезд на дорогу и съезд через встречную полосу
        /// </summary>
        /// <param name="stops">Точки маршрута</param>
        /// <param name="startTime">Начальное время</param>
        /// <param name="flags">флаги</param>
        /// <param name="RegionsAvailableToUser">Регионы доступные для пользователя (null = все)</param>
        /// <returns></returns>
        public string GetRouteXML(RStop[] stops, DateTime startTime, long flags, int[] RegionsAvailableToUser,
            PointF[] roadsExcept, double roadsExceptRaduisInMeters, byte[] RoadsOnly) // ID 7
        {
            string res = "";

            System.Net.Sockets.TcpClient cl = new System.Net.Sockets.TcpClient();
            cl.Connect(ip, port);
            cl.ReceiveTimeout = 5 * 60 * 1000 + 10000; // 5 min as in server + 10s delay  // 5 minutes // 2.5 mins per start // 2.5 mins per end

            byte[] buff = System.Text.Encoding.GetEncoding(1251).GetBytes("dkxce.Route.TCPSolver");
            cl.GetStream().Write(buff, 0, buff.Length);
            cl.GetStream().WriteByte(7);

            string[] ss = new string[stops.Length];
            double[] latt = new double[stops.Length];
            double[] lonn = new double[stops.Length];
            for (int i = 0; i < stops.Length; i++)
            {
                ss[i] = stops[i].name;
                latt[i] = stops[i].lat;
                lonn[i] = stops[i].lon;
            };
            buff = GetMethod3RequestData(licenseKey, ss, latt, lonn, startTime, flags, RegionsAvailableToUser);
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
                    System.IO.StreamReader sr = new System.IO.StreamReader(cl.GetStream());
                    res = sr.ReadToEnd();
                };
            };
            cl.GetStream().Close();
            cl.Close();
            return res;
        }

        /// <summary>
        ///     Привязка точки к дороге
        /// </summary>
        /// <param name="Lat">Широта</param>
        /// <param name="Lon">Долгота</param>
        /// <param name="getName">Запрашивать наименование дороги (работает дольше)</param>
        /// <returns>Привязанные к дороге точки</returns>
        public RNearRoad[] GetNearRoad(double[] lat, double[] lon, bool getNames) // ID 4
        {
            RNearRoad[] res = null;

            System.Net.Sockets.TcpClient cl = new System.Net.Sockets.TcpClient();
            cl.Connect(ip, port);
            cl.ReceiveTimeout = 5 * 60 * 1000 + 10000; // 5 min as in server + 10s delay  // 5 minutes // 2.5 mins per start // 2.5 mins per end

            byte[] buff = System.Text.Encoding.GetEncoding(1251).GetBytes("dkxce.Route.TCPSolver");
            cl.GetStream().Write(buff, 0, buff.Length);
            cl.GetStream().WriteByte(4);

            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(cl.GetStream(), new RNearRoadAsk(licenseKey,lat, lon, getNames));

            int read = 0;
            buff = new byte[21];
            while ((read = cl.GetStream().Read(buff, 0, buff.Length)) == 0) ;
            if ((read == 21) && (System.Text.Encoding.GetEncoding(1251).GetString(buff) == "dkxce.Route.TCPSolver") && (cl.GetStream().ReadByte() == 4))
                res = (dkxce.Route.ISolver.RNearRoad[])bf.Deserialize(cl.GetStream());
            cl.GetStream().Close();
            cl.Close();
            return res;
        }

        /// <summary>
        ///     Привязка точки к дороге
        /// </summary>
        /// <param name="Lat">Широта</param>
        /// <param name="Lon">Долгота</param>
        /// <param name="getName">Запрашивать наименование дороги (работает дольше)</param>
        /// <returns>Привязанные к дороге точки</returns>
        public string GetNearRoadXML(double[] lat, double[] lon, bool getNames) // ID 5
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
                System.IO.StreamReader sr = new System.IO.StreamReader(cl.GetStream());
                res = sr.ReadToEnd();
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

}
