using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Web.Services;
using System.Web.Services.Description;
using System.Xml;
using System.Xml.Serialization;

namespace dkxce.Route.ServiceSolver
{
    /// <summary>
    ///     Type Client Request Basic
    /// </summary>
    [XmlRoot("basicRequest")]
    public class tcBasic
    {
        public string key;

        public tcBasic() { }
        public tcBasic(string key)
        {
            this.key = key;
        }
    }

    /// <summary>
    ///     Type Server Response Basic
    /// </summary>
    [XmlRoot("BasicResponse")]
    public class tsBasic
    {
        public string Error;
        public int ErrCode;
    }

    [XmlRoot("NamedPoint")]
    [Serializable]
    public class XYN
    {
        [XmlAttribute("x")]
        public double x;

        [XmlAttribute("y")]
        public double y;

        [XmlAttribute("n")]
        public string n;

        public XYN() { }

        public XYN(double x, double y)
        {
            this.x = x;
            this.y = y;
        }

        public XYN(double x, double y, string n)
        {
            this.x = x;
            this.y = y;
            this.n = n;
        }
    }

    [XmlRoot("cRoute")]
    [Serializable]
    public class tcRoute : tcBasic
    {
        public string k = "";
        public string f = "0";
        public string p = "1";
        public string i = "0";
        public string v = "0";
        public string t = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

        [XmlElement("xy")]
        public XYN[] xy = new XYN[0];

        [XmlElement("n")]
        public string[] n = new string[0];

        public string minby = "time";
        public string o = "0";
        public string ct = "0";
        public string ht = "0";

        public string md5 = "";

        [XmlElement("exy")]
        public XYN[] exy = null;
        public string er = "50";
        public string ra = "";
    }

    [XmlRoot("WayPoint")]
    [Serializable]
    public class tsStop
    {
        [XmlAttribute("lat")]
        public double lat;
        [XmlAttribute("lon")]
        public double lon;
        [XmlText]
        public string name;

        public static tsStop[] FromRR(dkxce.Route.ISolver.RStop[] stops)
        {
            if (stops == null) return null;
            if (stops.Length == 0) return new tsStop[0];
            tsStop[] res = new tsStop[stops.Length];
            for (int i = 0; i < res.Length; i++)
                res[i] = tsStop.FromRR(stops[i]);
            return res;
        }

        public static tsStop FromRR(dkxce.Route.ISolver.RStop stop)
        {
            if (stop == null) return null;
            tsStop res = new tsStop();
            res.lat = stop.lat;
            res.lon = stop.lon;
            res.name = stop.name;
            return res;
        }
    }

    [XmlRoot("Point")]
    [Serializable]
    public class tsXYPoint
    {
        [XmlAttribute("x")]
        public double x;
        [XmlAttribute("y")]
        public double y;
        [XmlAttribute("s")]
        public double s;

        public static tsXYPoint[] FromRR(dkxce.Route.Classes.PointFL[] xyp)
        {
            if (xyp == null) return null;
            if (xyp.Length == 0) return new tsXYPoint[0];
            tsXYPoint[] res = new tsXYPoint[xyp.Length];
            for (int i = 0; i < res.Length; i++)
                res[i] = tsXYPoint.FromRR(xyp[i]);
            return res;
        }

        public static tsXYPoint FromRR(dkxce.Route.Classes.PointFL xy)
        {
            if (xy == null) return null;
            tsXYPoint res = new tsXYPoint();
            res.x = xy.X;
            res.y = xy.Y;
            res.s = xy.speed;
            return res;
        }
    }

    [XmlRoot("RoutePoint")]
    [Serializable]
    public class tsRoutePoint
    {
        public string iStreet;
        public string iToDo;
        public string iToGo;
        [XmlAttribute("no")]
        public int no;
        [XmlAttribute("sLen")]
        public double sLen;
        [XmlAttribute("sTime")]
        public double sTime;
        [XmlAttribute("tLen")]
        public double tLen;
        [XmlAttribute("tTime")]
        public DateTime tTime;
        [XmlAttribute("x")]
        public double x;
        [XmlAttribute("y")]
        public double y;

        public static tsRoutePoint[] FromRR(dkxce.Route.Classes.RDPoint[] rps, DateTime startTime)
        {
            if (rps == null) return null;
            tsRoutePoint[] res = new tsRoutePoint[rps.Length];
            if (res.Length == 0) return res;
            res = new tsRoutePoint[rps.Length];
            for (int i = 0; i < rps.Length; i++)
            {
                tsRoutePoint r = new tsRoutePoint();
                r.iStreet = rps[i].name;
                if (rps[i].instructions.Length > 0)
                    r.iToDo = rps[i].instructions[0];
                if (rps[i].instructions.Length > 1)
                    r.iToGo = rps[i].instructions[1];
                r.no = i;
                r.sLen = i == rps.Length - 1 ? 0 : (double)rps[i + 1].dist - (double)rps[i].dist;
                r.sTime = i == rps.Length - 1 ? 0 : (double)rps[i + 1].time - (double)rps[i].time;
                r.tLen = rps[i].dist;
                r.tTime = startTime.AddMinutes(rps[i].time);                 
                r.x = rps[i].Lon;
                r.y = rps[i].Lat;
                res[i] = r;
            };
            return res;
        }        
    }

    [XmlRoot("Route")]
    [Serializable]
    public class tsRoute : tsBasic
    {
        public double driveLength;
        [XmlArrayItem("dls")]
        public double[] driveLengthSegments;
        public double driveTime;
        [XmlArrayItem("dts")]
        public double[] driveTimeSegments;
        public DateTime finishTime;
        [XmlArrayItem("i")]
        public tsRoutePoint[] instructions;
        [XmlArrayItem("is")]
        public int[] instructionsSegments;
        public string LastError;
        [XmlArrayItem("p")]
        public tsXYPoint[] polyline;
        [XmlArrayItem("ps")]
        public int[] polylineSegments;
        public DateTime startTime;
        [XmlArrayItem("stop")]
        public tsStop[] stops;

        public static tsRoute FromRR(dkxce.Route.ISolver.RResult rt)
        {
            tsRoute res = new tsRoute();
            res.driveLength = rt.driveLength;
            res.driveLengthSegments = rt.driveLengthSegments;
            if (res.driveLengthSegments == null) res.driveLengthSegments = new double[0];
            res.driveTime = rt.driveTime;
            res.driveTimeSegments = rt.driveTimeSegments;
            if (res.driveLengthSegments == null) res.driveLengthSegments = new double[0];
            res.finishTime = rt.finishTime;
            res.instructions = tsRoutePoint.FromRR(rt.description, rt.startTime);
            if (res.instructions == null) res.instructions = new tsRoutePoint[0];
            res.instructionsSegments = rt.descriptionSegments;
            if (res.instructionsSegments == null) res.instructionsSegments = new int[0];
            res.LastError = rt.LastError;
            if (res.LastError == null) res.LastError = "";
            res.polyline = tsXYPoint.FromRR(rt.vector);
            if (res.polyline == null) res.polyline = new tsXYPoint[0];
            res.polylineSegments = rt.vectorSegments;
            if (res.polylineSegments == null) res.polylineSegments = new int[0];
            res.startTime = rt.startTime;
            res.stops = tsStop.FromRR(rt.stops);
            return res;
        }
    }

    [XmlRoot("cNearRoad")]
    [Serializable]
    public class tcNearRoad : tcBasic
    {
        public string k = "";
        public string f = "0";
        public string n = "0";

        public string md5 = "";

        [XmlElement("x")]
        public double[] x;
        [XmlElement("y")]
        public double[] y;
    }

    [XmlRoot("Road")]
    [Serializable]
    public class tsNearRoad
    {
        public string attributes;
        public double distance;
        public double lat;
        public double lon;
        public string name;
        public int region;

        public static tsNearRoad FromRR(dkxce.Route.ISolver.RNearRoad road)
        {
            if (road == null) return null;
            tsNearRoad res = new tsNearRoad();
            res.attributes = road.attributes;
            res.distance = road.distance;
            res.lat = road.lat;
            res.lon = road.lon;
            res.name = road.name;
            res.region = road.region;
            return res;
        }
    }

    [XmlRoot("Roads")]
    public class tsNearRoads : tsBasic
    {
        [XmlElement("road")]
        public tsNearRoad[] roads;

        public static tsNearRoads FromRR(dkxce.Route.ISolver.RNearRoad[] roads)
        {
            if (roads == null) return null;
            tsNearRoads res = new tsNearRoads();
            res = new tsNearRoads();
            if (roads.Length == 0) return res;
            res.roads = new tsNearRoad[roads.Length];
            for (int i = 0; i < res.roads.Length; i++)
                res.roads[i] = tsNearRoad.FromRR(roads[i]);
            return res;
        }
    }

    // !!!!! // USE NO NAMESPACE
    [WebService(Name = "NMS ROUTE API", Description = "NMS Route Web API", Namespace = "")]
    public abstract class SOAPWSDL
    {        
        [WebMethod(Description = "Маршруты\r\nПоиск маршрута\r\n(f - воспринимается только 3 / k / kml / 4 / g / geojson) ")]
        public abstract tsRoute Route(tcRoute request);

        [WebMethod(Description = "Маршруты\r\nПоиск ближайшей дороги\r\n(f - игнорируется)")]
        public abstract tsNearRoads NearRoad(tcNearRoad request);
    }    
}
