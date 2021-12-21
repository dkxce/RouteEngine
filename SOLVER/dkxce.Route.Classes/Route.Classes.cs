/* 
 * C# Class by Milok Zbrozek <milokz@gmail.com>
 * ������ ��� �������� ��������� �� ������, 
 * Author: Milok Zbrozek <milokz@gmail.com>
 * ������: 13305C7
 */

#define SERVER // comment if dll goes to client

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.InteropServices;

namespace dkxce.Route.Classes
{
    /*
     * FILES IN GRAPH:
     * lines - region.lines.bin - LINES info (node1,node2,segments,flags)
     *       - region.lines.id - LINK_ID for LINES
     *       - region.lines.att - LINES Attributes
     *       - region.lines.tmc - TMC for LINES
     * segm  - region.segments.bin - LINE SEGMENTS
     * graph - region.graph.bin - GRAPH NODE INFOS
     *       - region.graph.bin.in - INDEX FOR NODE POS IN GRAPH
     *       - region.graph[r].bin - GRAPH NODE INFOS (FOR INVERSE SOLVE ALGORITHM)
     *       - region.graph[r].bin.in - INDEX FOR NODE POS IN GRAPH (FOR INVERSE SOLVE ALGORITHM)
     *       - region.graph.geo - NODES LAT LON
     *       - region.graph.geo.ll - INDEXED LAT LON OF NODES     
     * ways  - region.rgnodes.xml - ���������� � ������ ����������� ���������
     * info  - region.analyze.txt - ���������� �� ������� � �����
     * 
     * 
     * lines file: (lines.bin)
     *   RMLINES, count[int], 
     *   RECORDS: 2 + 4 + 1 + 4 + 4 = 15
     *      segments_count[word]; 2
     *      pos[int]; 4
     *      flags[byte]; 1
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
     * Lines Attributes (lines.att)
     *  RMLATTRIB, count[int], // lines count
     *  array[1..LINES] of BYTE[0..15]
     * * BYTE[0] 0x01 - �������� ������ / ����� ���� (5.21) GARMIN_TYPE = 
     * * BYTE[0] 0x02 - ��������� ������ / ������ ��� �������� GARMIN_TYPE = 
     * * BYTE[0] 0x04 - ������ � �������� ���������
     * * BYTE[0] 0x08 - ������ ���������� ������� (1.16)
     * * BYTE[0] 0x10 - ������ ���������� ������
     * * BYTE[0] 0x20 - ��������� ������
     * * BYTE[0] 0x40 - ������� (1.31)
     * * BYTE[0] 0x80 - ����
     * * BYTE[1] 0x01 - ��������� ���� (1.9)
     * * BYTE[1] 0x02 - ��������� ����
     * * BYTE[1] 0x04 - ����� / ���������
     * * BYTE[1] 0x08 - ��������������� ������� (1.1, 1.2)
     * * BYTE[1] 0x10 - ����
     * * BYTE[1] 0x20 - {unused}
     * * BYTE[1] 0x40 - {unused}
     * * BYTE[1] 0x80 - {unused}
     * * BYTE[2] 0x01 - ����������� �������� � ���� ������
     * * BYTE[2] 0x02 - ������ ��� ����������� (5.3)
     * * BYTE[2] 0x04 - �������������� (5.1)
     * * BYTE[2] 0x08 - ������� ������
     * * BYTE[2] 0x10 - �������� ��������� ���������� ��������� (3.4)     
     * * BYTE[2] 0x20 - �������� ���������� ��������� (3.5)
     * * BYTE[2] 0x40 - �������� ��������� ��������� (3.6)
     * * BYTE[2] 0x80 - �������� � �������� ��������� (3.7)
     * * BYTE[3] 0x01 - ������� / ���������� ������� (3.17.1)
     * * BYTE[3] 0x02 - ������ ����� (1.13)
     * * BYTE[3] 0x04 - ������ ������ (1.14)    
     * * BYTE[3] 0x08 - �������� ������
     * * BYTE[3] 0x10 - ����� �������� (3.20)
     * * BYTE[3] 0x20 - ����� �������� ����������� �������� (3.22)
     * * BYTE[3] 0x40 - ��������� ��������� (3.27)
     * * BYTE[3] 0x80 - ������� ��������� (3.28)
     * * BYTE[4] 0x01 - �������� � �������� ������� ��������� (3.32)
     * * BYTE[4] 0x02 - �������� ������������ ������� � ����������� � ������������ ������� ��������� (3.33)
     * * BYTE[4] 0x04 - ��������
     * * BYTE[4] - {unused, 5 bit}
     * * BYTE[5] - {unused, 8 bit}
     * * BYTE[6] - {unused, 8 bit}
     * * BYTE[7] - ����������� ����� �� 1..255 * 250��, 0 - ��� ���������� (3.11)
     * * BYTE[8] - ����������� �������� �� ��� �� 1..255 * 250��, 0 - ��� ���������� (3.12)          
     * * BYTE[9] - ������ ������ 1..255 * 10��, 0 - ��� ���������� (3.13)
     * * BYTE[10] - ������ ������ 1..255 * 10��, 0 - ��� ���������� (3.14)
     * * BYTE[11] - ����������� ����� �� 1..255 * 10��, 0 - ��� ���������� (3.15)
     * * BYTE[12] - ����������� ��������� ����� �� 1..255 * 1�, 0 - ��� ���������� (3.16)
     * * BYTE[13] - {unused, 8 bit}
     * * BYTE[14] - {unused, 8 bit}
     * * BYTE[15] - xxxxx... - speed_limit/5 ($F8 mask) each 5 km/h (0,5,10,15 .. 150,155)
     * * BYTE[15] - .....xxx - 0..5 - RouteLevel ($07 mask)
     * 
     * Lines tmc file (lines.tmc) // creates for making the traffic.costs & traffic.times files)
     * RMTMC, region_id[int], count[int] // lines count
     *  array[1..LINES] of 
     *      reversed[byte]; 1
     *      tmc_code[ushort]; 2
     * 
     * *
     * * Lines Costs External File (Traffic) -- not impemented yet // ���� �������� ������ ��� ������� ��������� (������)
     * * (traffic.costs & traffic.times) -- ������ ����������� �� ������� ����������, ���������� ���� lines.tmc
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

    public static class LA
    {
        public static bool Bit(byte[] ba, byte byteNo, byte bit)
        {
            return (ba[byteNo] & (byte)Math.Pow(2,bit)) > 0;
        }
        public static bool Byt(byte[] ba, byte byteNo, byte byt)
        {
            return (ba[byteNo] & byt) > 0;
        }
    }

    /// <summary>
    ///     ����� XY
    /// </summary>
    [Serializable]
    public class PointF
    {
        [XmlAttribute("x")]
        public float X;

        [XmlAttribute("y")]
        public float Y;
        public PointF()
        {
            this.X = 0;
            this.Y = 0;
        }
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
    ///     ����� XY � ��������� � ����� � ����
    ///     (���-�� ��� �������� ��������)
    /// </summary>
    [Serializable]
    public class PointFL : PointF
    {
        [XmlAttribute("s")]
        public float speed;

        // <summary>
        ///     ����� �����, �� ������� ��������� �����
        /// </summary>
        [XmlAttribute("l")]
        public uint line;
        /// <summary>
        ///     ����� ����, � �������� ���������� ���������
        ///     (��������� �� ����� � ����� ����� �� ������)
        /// </summary>
        [XmlAttribute("n")]
        public uint node;

        public PointFL():base()
        {
            this.node = 0;
            this.line = 0;
            this.speed = 60;
        }
        /// <summary>
        ///     ������� ����� � ���������
        /// </summary>
        /// <param name="point">���������� �����</param>
        /// <param name="node">����� �����</param>
        /// <param name="line">����� ����</param>
        public PointFL(PointF point, uint node, uint line): base(point.X,point.Y)
        {
            this.X = point.X;
            this.Y = point.Y;
            this.node = node;
            this.line = line;
            this.speed = 60;
        }
        /// <summary>
        ///     ������� ����� � ���������
        /// </summary>
        /// <param name="point">���������� �����</param>
        /// <param name="node">����� �����</param>
        /// <param name="line">����� ����</param>
        public PointFL(PointF point, uint node, uint line, float speed)
            : base(point.X, point.Y)
        {
            this.X = point.X;
            this.Y = point.Y;
            this.node = node;
            this.line = line;
            this.speed = speed;
        }
        public override string ToString()
        {
            return "X = " + X.ToString() + "; Y = " + Y.ToString() + "; N = " + node.ToString() + "; L = " + line.ToString();
        }
        public float Lat { get { return Y; } }
        public float Lon { get { return X; } }
    }

    #if SERVER

    /// <summary>
    ///     ��������� ������� ��������
    /// </summary>
    [Serializable]
    public struct RouteResult
    {
        /// <summary>
        ///     ������ ��������
        /// </summary>
        public float cost;
        /// <summary>
        ///     ����� �������� (�)
        /// </summary>
        public float length;
        /// <summary>
        ///     ����� � ���� (�)
        /// </summary>
        public float time;
        /// <summary>
        ///     ���� ������
        /// </summary>
        public uint nodeStart;
        /// <summary>
        ///     ������������� ����
        /// </summary>
        [XmlArrayItem("n")]
        public uint[] nodes;
        /// <summary>
        ///     ���� ������
        /// </summary>
        public uint nodeEnd;
        /// <summary>
        ///     ����� �������� (�����)
        /// </summary>
        [XmlArrayItem("l")]
        public uint[] lines;
        /// <summary>
        ///     ������ �����������
        /// </summary>
        [XmlArrayItem("p")]
        public PointFL[] vector;
        
        /// <summary>
        ///     ������ �� �������� �������
        /// </summary>
        public bool shrinkStart;
                
        /// <summary>
        ///     ������ �� �������� � �����
        /// </summary>
        public bool shrinkEnd;

        public float shrinkStartLength;
        public float shrinkStartTime;
        public float shrinkStartCost;
        public float shrinkEndLength;
        public float shrinkEndTime;
        public float shrinkEndCost;
    }        

    /// <summary>
    ///     ��������� ������� �������� ������ �
    ///     ��������, ������������ � ��������� ��� ����������
    /// </summary>
    [Serializable]
    public struct RouteResultStored
    {
        /// <summary>
        ///     ID �������
        /// </summary>
        [XmlElement("region")]
        public int Region;

        /// <summary>
        ///     �������
        /// </summary>
        [XmlElement("route")]
        public RouteResult route;
        /// <summary>
        ///     ���������� ��� ���� ����� ��������,
        ///     ������� ������ � ���������
        /// </summary>
        [XmlArray("distances")]
        [XmlArrayItem("d")]
        public float[] distances;
        /// <summary>
        ///     ������� ����������� ���� ����� ��������,
        ///     ������� ������ � ���������
        /// </summary>
        [XmlArray("times")]
        [XmlArrayItem("t")]
        public float[] times;
        /// <summary>
        ///     ������ ����������� ���� ����� ��������,
        ///     ������� ������ � ���������
        /// </summary>
        [XmlArray("costs")]
        [XmlArrayItem("c")]
        public float[] costs;

        public RouteResultStored(RouteResult route, float[] distances, float[] times, float[] costs)
        {
            this.Region = 0;
            this.route = route;
            this.distances = distances;
            this.times = times;
            this.costs = costs;
        }

        public RouteResultStored(RouteResult route, float[] distances, float[] times, float[] costs, int region)
        {
            this.Region = region;
            this.route = route;
            this.distances = distances;
            this.times = times;
            this.costs = costs;
        }
    }

    #endif

    /// <summary>
    ///     �������� ��������
    /// </summary>
    [Serializable]
    public struct RDPoint
    {
        public float Lat;
        public float Lon;
        public float dist;
        public float time;
        public string name;
        public string[] instructions;

        public override string ToString()
        {
            string result = String.Format("{0} {1}; {2} ���\r\n", this.Lat.ToString().Replace(",", "."), this.Lon.ToString().Replace(",", "."), this.time.ToString("0"));
            if (this.name != "") result += String.Format("\t{0}\r\n", this.name);

            string ins1 = this.name;
            if ((this.instructions != null) && (this.instructions.Length > 0))
                ins1 = this.instructions[0];
            result += String.Format("\t{0} ��. {1}\r\n", (this.dist / 1000).ToString("0.00").Replace(",", "."), ins1);

            if (this.instructions != null)
                for (int x = 1; x < this.instructions.Length; x++)
                    result += String.Format("\t{0}\r\n", this.instructions[x]);

            return result.Substring(0, result.Length - 2);
        }
    }
    
    

    /// <summary>
    ///     ����������� ����� ��������� �������
    /// </summary>
    [Serializable]
    [ComVisible(true)]
    public abstract class XYO
    {
        [XmlAttribute()]
        public double lat = 0;
        [XmlAttribute()]
        public double lon = 0;

        public XYO() {}

        public XYO(double lat, double lon)
        {
            this.lat = lat;
            this.lon = lon;
        }
    }

    #if SERVER // ������ ��� �������
    
    /// <summary>
    ///     ��������� ������ ���������/�������� ����� �� �����������
    /// </summary>
    public class FindStartStopResult
    {
        /// <summary>
        ///     ������ �� ����� �� ���� ����� ����� (��� end - ������)
        /// </summary>
        public PointF[] normal;

        /// <summary>
        ///     ������ �� ����� �� ���� ������ ����� (��� end - �����)
        /// </summary>
        public PointF[] revers;

        /// <summary>
        ///     ���������� �� ����� �� �����
        /// </summary>
        public float distToLine;

        /// <summary>
        ///     ���������� �� ����� �� ���� ����� ����� (��� end - ������)
        /// </summary>
        public float distToN;

        /// <summary>
        ///     ���������� �� ����� �� ���� ������ ����� (��� end - �����)
        /// </summary>
        public float distToR;

        /// <summary>
        ///     ��������� �����
        /// </summary>
        public uint line;

        /// <summary>
        ///     ��������� ���� (start - normal - last_point; end - normal - first_point)
        /// </summary>
        public uint nodeN;

        /// <summary>
        ///     ����� ���� �� ������ ����� �����
        /// </summary>
        public uint nodeR;

        public override string ToString()
        {
            return " N" + nodeN.ToString() + ", " + distToN.ToString() + " �, L" + line.ToString();
        }
    }

    /// <summary>
    ///     �������������� ������� ��
    /// </summary>
    public enum MinimizeBy : byte
    {
        /// <summary>
        ///     �� ������ (���-�� ������ ��� �������)
        /// </summary>        
        Cost = 0,

        /// <summary>
        ///     �� ����������
        /// </summary>
        Dist = 1,

        /// <summary>
        ///     �� �������
        /// </summary>
        Time = 2
    }

    //// /// // / // /// //// /// // / // /// ////
    //// /// // / // /// //// /// // / // /// ////
    //// /// // / // /// //// /// // / // /// ////
    
    /// <summary>
    ///     ����� �������� ����������� ���������
    /// </summary>
    [Serializable]
    public struct TRGNode
    {
        /// <summary>
        ///     ������������� ������������ ����
        /// </summary>
        [XmlAttribute("id")]
        public int id;

        /// <summary>
        ///     ����� ���� � �����
        /// </summary>
        [XmlAttribute("node")]
        public uint node;

        /// <summary>
        ///     ����� ������������ ���� �� ������ ��������
        /// </summary>
        [XmlAttribute("in")]
        public bool inner;

        /// <summary>
        ///     ����� ������������ ����� � ������ �������
        /// </summary>
        [XmlAttribute("out")]
        public bool outer;

        /// <summary>
        ///     ��� �������� � DBF ����
        /// </summary>
        [XmlAttribute("labal")]
        public string label;

        /// <summary>
        ///     ����� �������
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
        ///     ������ �� �������������� ������ ����������� �����, � ������� ����� ���������
        /// </summary>
        [XmlArrayItem("l")]
        public int[] links;

        /// <summary>
        ///     ������ �� ������ ����������� �����, � ������� ����� ���������
        /// </summary>
        [XmlArrayItem("c")]
        public float[] costs;

        /// <summary>
        ///     ��������� �� ������ ����������� �����, � ������� ����� ���������
        /// </summary>
        [XmlArrayItem("d")]
        public float[] dists;

        /// <summary>
        ///     ����� �� ������ ����������� �����, � ������� ����� ���������
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

    [Serializable]
    public class AdditionalInformation
    {
        public int regionID = 0;       

        public double minX = 0;
        public double minY = 0;
        public double maxX = 0;
        public double maxY = 0;

        public string TestMap = null;
        public string RegionName = null;
        public string SourceType = null;
        public string ConvertedWith = null;

        [XmlIgnore]
        public double centerX
        {
            get
            {
                return (minX + maxX) / 2;
            }
        }

        [XmlIgnore]
        public double centerY
        {
            get
            {
                return (minY + maxY) / 2;
            }
        }
    }

    /// <summary>
    ///     BitParser Line Flags
    /// </summary>
    public struct TLineFlags
    {
        private byte flag;
        private uint node0;
        private uint node1;

        public TLineFlags(byte flagByte, uint node0, uint node1)
        {
            this.flag = flagByte;
            this.node0 = node0;
            this.node1 = node1;
        }

        public uint Node0 { get { return node0; } }
        public uint Node1 { get { return node1; } }

        /// <summary>
        ///     ������������� �� �������� �� �������
        /// </summary>
        public bool IsOneWay { get { return (flag & 1) == 1; } }

        /// <summary>
        ///     �������� ��������
        /// </summary>
        public bool IsRoundAbout { get { return (flag & 2) == 2; } }

        /// <summary>
        ///     ������������� TMC
        /// </summary>
        public bool IsTMC { get { return (flag & 4) == 4; } }

        /// <summary>
        ///     ���� ��������
        /// </summary>
        public bool HasAttributes { get { return (flag & 8) == 8; } }

        public override string ToString()
        {
            string s = "";
            s += (s.Length > 0 ? " " : "") + (IsOneWay ? "OneWay" : "");
            s += (s.Length > 0 ? " " : "") + (IsRoundAbout ? "RoundAbout" : "");
            s += (s.Length > 0 ? " " : "") + (IsTMC ? "TMC" : "");
            s += (s.Length > 0 ? " " : "") + (HasAttributes ? "ATTR" : "");
            return s;
        }
    }

    public class Utils
    {
        // ������� ����������       
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
            double D2R = Math.PI / 180;     // �������������� �������� � �������

            double a = 6378137.0000;     // WGS-84 Equatorial Radius (a)
            double f = 1 / 298.257223563;  // WGS-84 Flattening (f)
            double b = (1 - f) * a;      // WGS-84 Polar Radius
            double e2 = (2 - f) * f;      // WGS-84 ������� ��������������� ����������  // 1-(b/a)^2

            // ����������, ������������ ��� ���������� �������� � ����������
            double fPhimean;                           // ������� ������
            double fdLambda;                           // ������� ����� ����� ���������� �������
            double fdPhi;                           // ������� ����� ����� ���������� ������
            double fAlpha;                           // ��������
            double fRho;                           // ������������ ������ ��������
            double fNu;                           // ���������� ������ ��������
            double fR;                           // ������ ����� �����
            double fz;                           // ������� ���������� �� ������ ��������
            double fTemp;                           // ��������� ����������, �������������� � �����������

            // ��������� ������� ����� ����� ��������� � �������� � �������� ������� ������
            // ���������������� ��� ���������� ����� ������� << ������� �����
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

            // ��������� ����������� � ���������� ������� �������� ������� ������
            fTemp = 1 - e2 * (sqr(Math.Sin(fPhimean)));
            fRho = (a * (1 - e2)) / Math.Pow(fTemp, 1.5);
            fNu = a / (Math.Sqrt(1 - e2 * (Math.Sin(fPhimean) * Math.Sin(fPhimean))));

            // ��������� ������� ����������
            if (!radians)
            {
                fz = Math.Sqrt(sqr(Math.Sin(fdPhi / 2.0)) + Math.Cos(EndLat * D2R) * Math.Cos(StartLat * D2R) * sqr(Math.Sin(fdLambda / 2.0)));
            }
            else
            {
                fz = Math.Sqrt(sqr(Math.Sin(fdPhi / 2.0)) + Math.Cos(EndLat) * Math.Cos(StartLat) * sqr(Math.Sin(fdLambda / 2.0)));
            };
            fz = 2 * Math.Asin(fz);

            // ��������� ��������
            if (!radians)
            {
                fAlpha = Math.Cos(EndLat * D2R) * Math.Sin(fdLambda) * 1 / Math.Sin(fz);
            }
            else
            {
                fAlpha = Math.Cos(EndLat) * Math.Sin(fdLambda) * 1 / Math.Sin(fz);
            };
            fAlpha = Math.Asin(fAlpha);

            // ��������� ������ �����
            fR = (fRho * fNu) / (fRho * sqr(Math.Sin(fAlpha)) + fNu * sqr(Math.Cos(fAlpha)));
            // �������� ����������
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

    /// <summary>
    ///     ������ ������������
    /// </summary>
    public class TSP
    {
        ///////////
        /// <summary>
        ///     ������� ������ ������������ �� ��������� ����������� ������ ����������
        ///     ����������� ������� N^2, ��� N - ����� �����
        ///     ���������� - Milok Zbrozek (c) milokz@gmail.com
        /// </summary>
        /// <param name="ODmatrix">������� ���������� ����������</param>
        /// <param name="length">����� ����� � �������</param>
        /// <param name="startIndex">��������� ���� ��������</param>
        /// <param name="endIndex">�������� ���� ��������</param>
        /// <returns>���������������� ������ �����</returns>
        // ** http://habrahabr.ru/post/151954/
        // ** http://math.semestr.ru/kom/index.php
        // https://ru.wikipedia.org/wiki/%D0%97%D0%B0%D0%B4%D0%B0%D1%87%D0%B0_%D0%BA%D0%BE%D0%BC%D0%BC%D0%B8%D0%B2%D0%BE%D1%8F%D0%B6%D1%91%D1%80%D0%B0
        // http://mech.math.msu.su/~shvetz/54/inf/perl-problems/chCommisVoyageur.xhtml
        // http://habrahabr.ru/post/151151/
        // http://habrahabr.ru/post/160167/
        public static int[] CalcTSP(float[,] ODmatrix, int length, int startIndex, int endIndex)
        {
            int nodes2go = length; // ����� �������� ��� ���� �� 1 ����

            List<int> nodes = new List<int>(); // ��� ������� ����
            nodes.Add(startIndex); // ��������� ������ �����
            nodes.Add(endIndex); // ��������� ��������� �����
            if (startIndex == endIndex) nodes2go++; // ���� ������� � ������, �� ���� ���� �������� ������, �.�. ������� � ������� ����� = length + 1

            // ����������� ��������� ���������� �� ������ � ����� �� ������� �������������� ���� (���������)
            float[] maxLength = new float[length];
            for (int eachNode = 0; eachNode < length; eachNode++)
                if (!nodes.Contains(eachNode))
                    for (int i = 0; i < nodes.Count; i++)
                        maxLength[eachNode] += ODmatrix[nodes[i], eachNode];

            // ������� ����� ��������� ���� �� ������� ����� ��������
            float maxVal = 0;
            int _farestNode = 0;
            for (int eachNode = 0; eachNode < length; eachNode++)
                if (maxLength[eachNode] > maxVal)
                {
                    maxVal = maxLength[eachNode];
                    _farestNode = eachNode;
                };
            nodes.Insert(1, _farestNode); // ��������� ����� ��������� ���� ��� �������������

            // ��� ���� ��������� �����
            while (nodes.Count < nodes2go)
            {
                // ����������� ��������� ���������� �� ��� ����������� ����� �� ������� �������������� ����
                for (int eachNode = 0; eachNode < length; eachNode++) maxLength[eachNode] = 0;
                for (int eachNode = 0; eachNode < length; eachNode++)
                    if ((!nodes.Contains(eachNode)))
                        for (int i = 0; i < nodes.Count; i++)
                            maxLength[eachNode] += ODmatrix[nodes[i], eachNode];

                // ������� ����� ��������� ����
                maxVal = 0;
                _farestNode = 0;
                for (int eachNode = 0; eachNode < length; eachNode++)
                    if (maxLength[eachNode] > maxVal)
                    {
                        maxVal = maxLength[eachNode];
                        _farestNode = eachNode;
                    };

                // ��� ������� ������ ������ �������� ���� ���������� ��� ���� 
                // �������� ����������� �������� � �������� �� ��� �� �����, 
                // ������ �������� ���� ����������� ������� �����
                float[] lineLength = new float[nodes.Count - 1];
                for (int i = 1; i < nodes.Count; i++)
                {
                    lineLength[i - 1] =
                        ODmatrix[nodes[i - 1], _farestNode] + ODmatrix[_farestNode, nodes[i]] - ODmatrix[nodes[i - 1], nodes[i]];
                };

                // ������� ����� ��������� ����������
                float minVal = float.MaxValue;
                int _i = 0;
                for (int i = 0; i < lineLength.Length; i++)
                    if (lineLength[i] < minVal)
                    {
                        minVal = lineLength[i];
                        _i = i;
                    };

                nodes.Insert(_i + 1, _farestNode); // ��������� ��������� �����
            };

            return nodes.ToArray();
        }        

        /// <summary>
        ///     �������������� �������
        ///     (������ ������������)
        /// </summary>
        /// <param name="way">������ �������� � �������/��������</param>
        public static void OptimizeWayRoute(XYO[] way)
        {
            if (way.Length < 4) return;

            // create matrix
            int l = way.Length;
            float[,] m = new float[l, l];

            // fill matrix
            for (int a = 0; a < l; a++)
                for (int b = 0; b < l; b++)
                    if (a == b)
                        m[a, b] = -1;
                    else
                        m[a, b] = (float)Utils.GetLengthMeters(way[a].lat, way[a].lon, way[b].lat, way[b].lon, false);

            // calc TSP
            int[] rt = CalcTSP(m, l, 0, l - 1);

            // reorder array
            object[] reord = (object[])way.Clone();
            for (int i = 0; i < rt.Length; i++)
                way[i] = (XYO)reord[rt[i]];
        }
    }

    /// <summary>
    ///     ������ �������� ����� �� ������ �� �����
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
            while ((read = fs.Read(ba, 0, ba.Length)) > 0)
            {
                string s = System.Text.Encoding.GetEncoding(1251).GetString(ba, 0, read);
                s = s.Substring(0, s.IndexOf("\r\n"));
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
                return s.Substring(0, s.IndexOf("\r\n")).Split(new string[] { ";" }, StringSplitOptions.None)[0];
            }
        }

        public string City(uint line)
        {
            fs.Position = pos[(int)line - 1];
            byte[] ba = new byte[1024];
            fs.Read(ba, 0, ba.Length);
            string s = System.Text.Encoding.GetEncoding(1251).GetString(ba, 0, ba.Length);
            string[] ss = s.Substring(0, s.IndexOf("\r\n")).Split(new string[] { ";" }, StringSplitOptions.None);
            if (ss.Length > 1)
                return ss[1];
            else
                return "";
        }

        public void Close()
        {
            fs.Close();
            fs = null;
        }

        ~LinesNamesFileReader()
        {
            if (fs != null) this.Close();
        }

    }

    /// <summary>
    ///     ���� �����
    /// </summary>
    public class TNode
    {
        /// <summary>
        ///     ������������� (����� ����) (��������� � 1)
        /// </summary>
        public uint node;
        /// <summary>
        ///     ������ � ����
        /// </summary>
        public float lat;
        /// <summary>
        ///     ������� � ����
        /// </summary>
        public float lon;
        /// <summary>
        ///     ��������� ����� (�����)
        /// </summary>
        public List<TLink> links = new List<TLink>();
        /// <summary>
        ///     �������� ����� (�����)
        /// </summary>
        public List<TLink> rlinks = new List<TLink>();

        /// <summary>
        ///     ������� ����
        /// </summary>
        /// <param name="node">�����</param>
        /// <param name="lat">������</param>
        /// <param name="lon">�������</param>
        public TNode(uint node, float lat, float lon)
        {
            this.node = node;
            this.lat = lat;
            this.lon = lon;
        }

        /// <summary>
        ///     ����� ��������� ������ (�����)
        /// </summary>
        public int Outs { get { return links.Count; } }

        /// <summary>
        ///     ����� �������� ������ (�����)
        /// </summary>
        public int Ins { get { return rlinks.Count; } }

        /// <summary>
        ///     N Y X I O    
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "{N" + node + "; Y" + lat.ToString() + " X" + lon.ToString() + " I" + Ins.ToString() + " O" + Outs.ToString() + "}";
        }

        /// <summary>
        ///     ��������� ��������� ����� (�����)
        /// </summary>
        /// <param name="node">����</param>
        /// <param name="cost">������</param>
        /// <param name="dist">���������� (�)</param>
        /// <param name="time">����� (���)</param>
        /// <param name="line">�����</param>
        /// <param name="line_rev">����� � �������� �������?</param>
        public void AddLink(uint node, float cost, float dist, float time, uint line, bool line_rev)
        {
            this.links.Add(new TLink(node, cost, dist, time, line, line_rev));
        }

        /// <summary>
        ///     ��������� �������� ����� (�����)
        /// </summary>
        /// <param name="node">����</param>
        /// <param name="cost">������</param>
        /// <param name="dist">���������� (�)</param>
        /// <param name="time">����� (���)</param>
        /// <param name="line">�����</param>
        /// <param name="line_rev">����� � �������� �������?</param>
        public void AddRLink(uint node, float cost, float dist, float time, uint line, bool line_rev)
        {
            this.rlinks.Add(new TLink(node, cost, dist, time, line, line_rev));
        }

        /// <summary>
        ///     ����������� ����� (��� ����������)
        /// </summary>
        public class Sorter : IComparer<TNode>
        {
            private byte sortBy = 0;
            private Sorter(byte sortBy) { this.sortBy = sortBy; }

            /// <summary>
            ///     ��������� �� ��������������
            /// </summary>
            /// <returns></returns>
            public static Sorter SortByNode() { return new Sorter(0); }

            /// <summary>
            ///     ��������� �� �����������
            /// </summary>
            /// <returns></returns>
            public static Sorter SortByLL() { return new Sorter(1); }

            /// <summary>
            ///     ��������� ��� ��������� �������
            /// </summary>
            /// <param name="a">1-�� �������</param>
            /// <param name="b">2-�� �������</param>
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
    ///     ���� � ����������� � ����������
    ///     �� ��������� ����� �� �������
    /// </summary>
    public class TNodeD : TNode
    {
        /// <summary>
        ///     ���������� (�)
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
    ///     ����� ����� ������ (�����)
    /// </summary>
    public class TLink
    {
        /// <summary>
        ///     ���� �����
        /// </summary>
        public uint node;

        /// <summary>
        ///     ������
        /// </summary>
        public float cost;

        /// <summary>
        ///     ���������� (�)
        /// </summary>
        public float dist;

        /// <summary>
        ///     ����� (���)
        /// </summary>
        public float time;

        /// <summary>
        ///     �����
        /// </summary>
        public uint line;

        /// <summary>
        ///     ����� ��������� � ��������� ����� �����
        ///     �������� �� ����� � �������� �������
        /// </summary>
        public bool inverse_dir;

        /// <summary>
        ///     ������� �����
        /// </summary>
        /// <param name="node">��������� ����</param>
        /// <param name="cost">������ ������������</param>
        /// <param name="dist">���������� � �</param>
        /// <param name="time">����� � ���</param>
        /// <param name="line">����� �����</param>
        /// <param name="inverse_dir">� �������� �����������</param>
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
    ///     ���������� � ����� shape �����
    /// </summary>
    [Serializable]
    public class ShapeFields : XMLSaved<ShapeFields>
    {
        /// <summary>
        ///     ���� ����������� �� ������������� �������� 0/1,
        ///     ���� �� ����� ���� ������
        /// </summary>
        [XmlElement("OneWay")]
        public string fldOneWay = "ONE_WAY";
        /// <summary>
        ///     ���� � ������������ ������������ �������� (��/�),
        ///     ���� �� ����� ���� ������
        /// </summary>
        [XmlElement("SpeedLimit")]
        public string fldSpeedLimit = "SPD_LIMIT";
        /// <summary>
        ///     ���� � ������������ ������������ �������� (��/�),
        ///     ���� �� ����� ���� ������
        /// </summary>
        [XmlElement("RouteSpeed")]
        public string fldRouteSpeed = null;
        /// <summary>
        ///     ���� � ����� �����,
        ///     ���� �� ����� ���� ������
        /// </summary>
        [XmlElement("GarminType")]
        public string fldGarminType = "GRMN_TYPE";
        /// <summary>
        ///     ���� � ��������������� �����,
        ///     ������������ ��� �������� ���������,
        ///     ���� �� ����� ���� ������
        /// </summary>
        [XmlElement("LinkId")]
        public string fldLinkId = "LINK_ID";
        /// <summary>
        ///     ����� ����� � ������,
        ///     ���� �� �������, �� ����� �������������
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
        ///     ��������� DBF ����� (1251/1252/866)
        /// </summary>
        [XmlElement("CodePageId")]
        public int CodePageId = 1251;
        /// <summary>
        ///     ������������� �� ������ ( Lat = -1*Lat )
        /// </summary>
        [XmlElement("InverseLat")]
        public bool InverseLat = false;
        /// <summary>
        ///     ������������� �� ������� ( Lon = -1*Lon )
        /// </summary>
        [XmlElement("InverseLon")]
        public bool InverseLon = false;
        /// <summary>
        ///     ���� � ����������� � TMC ����
        ///     @E0+000+00000;@E0-000-00000;
        ///     @E0+000+00000 or @E0-000-00000 (TMC ����������� � ������)
        ///     @E0+000-00000 or @E0-000+00000 (��� ���������������� ��������� �����)
        ///     Max TMC no: 65535 (2 bytes)
        /// </summary>
        [XmlElement("TMC")]
        public string fldTMC = null;
        /// <summary>
        ///     ���� � ���������� �����
        /// </summary>
        [XmlElement("ACC_MASK")]
        public string fldACCMask = null;

        /// <summary>
        ///     ���� � ������� �������
        /// </summary>
        [XmlElement("TOLL_ROAD")]
        public string fldTollRoad = null;

        /// <summary>
        ///     ���� � �������� �������
        /// </summary>
        [XmlElement("IS_TUNNEL")]
        public string fldIsTunnel = null;
        /// <summary>
        ///     ���� � ���������� �����
        /// </summary>
        [XmlElement("ATTR")]
        public string fldAttr = null;

        /// <summary>
        ///     ����������� �����, � ������
        /// </summary>
        [XmlElement("MaxWeight")]
        public string fldMaxWeight = null;
        /// <summary>
        ///     ������������ �������� �� ���, � ������
        /// </summary>
        [XmlElement("MaxAxle")]
        public string fldMaxAxle = null;
        /// <summary>
        ///     ����������� ������ � ������
        /// </summary>
        [XmlElement("MaxHeight")]
        public string fldMaxHeight = null;
        /// <summary>
        ///     ����������� ������ � ������
        /// </summary>
        [XmlElement("MaxWidth")]
        public string fldMaxWidth = null;
        /// <summary>
        ///     ����������� ����� � ������
        /// </summary>
        [XmlElement("MaxLength")]
        public string fldMaxLength = null;
        /// <summary>
        ///     ����������� ��������� � ������
        /// </summary>
        [XmlElement("MinDistance")]
        public string fldMinDistance = null;
        /// <summary>
        ///     Route Level
        /// </summary>
        [XmlElement("RouteLevel")]
        public string fldRouteLevel = "";

        /// <summary>
        ///     ���� � ������� ����� �������� ����������� ���������
        ///     ������: F0001IO / L0002I / F0003O; 
        ///     I (In) - ����� ���� ���� ����� ������� � ������; 
        ///     O (out) - ����� ���� ���� ����� ������� �� �������
        /// </summary>
        [XmlElement("RGNODE")]
        public string fldRGNODE = null;
        /// <summary>
        ///     ���� � ������������� �����
        /// </summary>
        [XmlElement("NAME")]
        public string fldName = null;

        ///////// OSM //////
        [XmlElement("SOURCE")]
        public string SOURCE = "GARMIN";
        [XmlElement("OSM_ID")]
        public string fldOSM_ID = "OSM_ID";
        [XmlElement("OSM_TYPE")]
        public string fldOSM_TYPE = "OSM_TYPE";
        [XmlElement("OSM_SURFACE")]
        public string fldOSM_SURFACE = "OSM_SURFACE";

        [XmlElement("OSM.ADDIT_DBF.NODES")]
        public string fldOSM_ADDIT_DBF_NODES = null;
        [XmlElement("OSM.ADDIT_DBF.NOTURN")]
        public string fldOSM_ADDIT_DBF_NOTURN = null;
        [XmlElement("OSM.ADDIT.SERVICE")]
        public string fldOSM_ADDIT_SERVICE = null;
        [XmlElement("OSM.ADDIT.JUNCTION")]
        public string fldOSM_ADDIT_JUNCTION = null;
        [XmlElement("OSM.ADDIT.LANES")]
        public string fldOSM_ADDIT_LANES = null;
        [XmlElement("OSM.ADDIT.MAXACTUAL")]
        public string fldOSM_ADDIT_MAXACTUAL = null;
        [XmlElement("OSM.ADDIT.PROCESS_AGG")]
        public bool fldOSM_ADDIT_PROCESSAGG = false;

        /// <summary>
        ///     ���������� �� ������� ���������
        /// </summary>
        public bool NoTurnRestrictions { get { return (fldTurnRstr == null) || (fldTurnRstr == String.Empty); } }

        /// <summary>
        ///     ��������� DBF �����
        /// </summary>
        public System.Text.Encoding CodePage { get { return System.Text.Encoding.GetEncoding(CodePageId); } }
    }

    #endif
}
