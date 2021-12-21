using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace ShapesBBox2Regions
{
    public class BoxRecord
    {
        public double[] BBox = new double[4];

        public int ID = 999;

        public string Name = "Unknown";

        public string File = "?";

        public BoxRecord(double[] box, int id, string name, string file)
        {
            this.BBox = box;
            this.ID = id;
            if(!String.IsNullOrEmpty(name)) this.Name = name;
            if (!String.IsNullOrEmpty(file)) this.File = file;
        }

        public double minX { get { return BBox[0]; } set { BBox[0] = value; } }

        public double minY { get { return BBox[1]; } set { BBox[1] = value; } }

        public double maxX { get { return BBox[2]; } set { BBox[2] = value; } }

        public double maxY { get { return BBox[3]; } set { BBox[3] = value; } }

        public double centerX { get { return (BBox[0] + BBox[2]) / 2; } }

        public double centerY { get { return (BBox[1] + BBox[3]) / 2; } }

        public PointF[] vector
        {
            get
            {
                return new PointF[] { 
                    new PointF((float)BBox[0],(float)BBox[1]),
                    new PointF((float)BBox[2],(float)BBox[1]),
                    new PointF((float)BBox[2],(float)BBox[3]),
                    new PointF((float)BBox[0],(float)BBox[3])
                };
            }
        }

        public PointF[] Enlarge(int km)
        {
            if (km <= 0) return vector;
            double[] box = Enlarge(BBox, km);
            return new PointF[] { 
                    new PointF((float)box[0],(float)box[1]),
                    new PointF((float)box[2],(float)box[1]),
                    new PointF((float)box[2],(float)box[3]),
                    new PointF((float)box[0],(float)box[3])
                };
        }

        public override string ToString()
        {
            return String.Format("{0,-3} - {1} - {2} {3} - {4}", ID, Name, SBox, SCXY, this.File );
        }

        public string SBox
        {
            get
            {
                return String.Format(System.Globalization.CultureInfo.InvariantCulture, "[{0:0.000000},{1:0.000000},{2:0.000000},{3:0.000000}]", BBox[0], BBox[1], BBox[2], BBox[3]);
            }
        }

        public string SCXY
        {
            get
            {
                return String.Format(System.Globalization.CultureInfo.InvariantCulture, "[{0:0.000000},{1:0.000000}]", centerX, centerY);
            }
        }

        private double[] Enlarge(double[] box, double dist_in_km)
        {
            double[] res = new double[4];
            double lon_min = box[0];
            double lat_min = box[1];
            double lon_max = box[2];
            double lat_max = box[3];
            double d_buttom = 1.0 / (dkxce.Route.Classes.Utils.GetLengthMetersA(lat_min - 1, (lon_min + lon_max) / 2, lat_min, (lon_min + lon_max) / 2, false) / 1000.0) * dist_in_km;
            double d_top = 1.0 / (dkxce.Route.Classes.Utils.GetLengthMetersA(lat_max, (lon_min + lon_max) / 2, lat_max + 1, (lon_min + lon_max) / 2, false) / 1000.0) * dist_in_km;
            double d_left = 1.0 / (dkxce.Route.Classes.Utils.GetLengthMetersA((lat_min + lat_max) / 2, lon_min - 1, (lat_min + lat_max) / 2, lon_min, false) / 1000.0) * dist_in_km;
            double d_right = 1.0 / (dkxce.Route.Classes.Utils.GetLengthMetersA((lat_min + lat_max) / 2, lon_max, (lat_min + lat_max) / 2, lon_max + 1, false) / 1000.0) * dist_in_km;
            res = new double[4] { box[0] - d_buttom, box[1] - d_left, box[2] + d_top, box[3] + d_right };
            return res;
        }

    }

    public class BoxSorter : IComparer<BoxRecord>
    {
        private byte sortBy = 0; // 0 - id , 1 - name

        public BoxSorter(byte sortBy) { this.sortBy = sortBy; }

        public int Compare(BoxRecord a, BoxRecord b)
        {
            if (sortBy == 1)
            {
                return a.Name.CompareTo(b.Name);
            }
            else
            {
                return a.ID.CompareTo(b.ID);
            };
        }
    }

    public class ShapeBoxReader
    {
        public double[] box = new double[4];        
        public int shpType = 0;

        private string fileName;

        public ShapeBoxReader(string fileName)
        {
            this.fileName = fileName;
            LoadShapeType();
        }

        private void LoadShapeType()
        {
            FileStream shapeFileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            long shapeFileLength = shapeFileStream.Length;
            if (shapeFileLength < 100)
            {
                shapeFileStream.Close();
                return;
            };

            byte[] shapeFileData = new byte[100];
            shapeFileStream.Read(shapeFileData, 0, shapeFileData.Length);
            shapeFileStream.Close();

            shpType = readIntLittle(shapeFileData, 32);
            box[0] = readDoubleLittle(shapeFileData, 36);
            box[1] = readDoubleLittle(shapeFileData, 44);
            box[2] = readDoubleLittle(shapeFileData, 52);
            box[3] = readDoubleLittle(shapeFileData, 60);
        }

        public static int readIntLittle(byte[] data, int pos)
        {
            byte[] bytes = new byte[4];
            bytes[0] = data[pos];
            bytes[1] = data[pos + 1];
            bytes[2] = data[pos + 2];
            bytes[3] = data[pos + 3];
            if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        public static double readDoubleLittle(byte[] data, int pos)
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
            if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToDouble(bytes, 0);
        }

        public bool ShapeOk
        {
            get
            {
                if (shpType == 3) return true;
                if (shpType == 5) return true;
                return false;
            }
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
}
