using System;
using System.Collections;
namespace SCRIPT
{
    public class Script
    {
        public virtual Hashtable call()
        {
            Hashtable vars = new Hashtable();
            return vars;
        }

        public double[][] EmulateLatLon()
        {
            double StartLat = 52.927;
            double StartLon = 38.930;
            double EndLat = 52.488;
            double EndLon = 39.90;
            int Count = 100;
            return EmulateLatLon(StartLat, StartLon, EndLat, EndLon, Count);
        }

        public double[][] EmulateLatLon(double StartLat, double StartLon, double EndLat, double EndLon, int Count)
        {            
            Random rnd = new Random();
            double[][] res = new double[Count][];
            for (int i = 0; i < Count; i++)
                res[i] = new double[] { 
                    StartLat + 1 - rnd.NextDouble() * 2, 
                    StartLon + 1 - rnd.NextDouble() * 2,
                    EndLat + 1 - rnd.NextDouble() * 2, 
                    EndLon + 1 - rnd.NextDouble() * 2 };
            return res;
        }

        public string[] EmulateAddress()
        {
            return new string[]
            {
                "�����������",
                "����������",
                "��������",
                "������",
                "���������",
                "�����",
                "�������",
                "����",
                "������",
                "�������",
                "�������",
                "�������"
            };
        }

        public string[] EmulateHouses()
        {
            return new string[]
            {
                "����������� 1",
                "���������� 1",
                "�������� 1",
                "������ 1",
                "��������� 1",
                "����� 1",
                "������� 1",
                "���� 1",
                "������ 1",
                "������� 1",
                "������� 1",
                "������� 1"
            };
        }

        public int[][] EmulateTileXY()
        {
            int sX = 2484;
            int sY = 1336;
            int Count = 100;
            return EmulateTileXY(sX, sY, Count);
        }

        public int[][] EmulateTileXY(int sX, int sY, int Count)
        {
            Random rnd = new Random();
            int[][] res = new int[Count][];
            for (int i = 0; i < Count; i++)
                res[i] = new int[] { 
                    sX + 50 - rnd.Next(0,50), 
                    sY + 50 - rnd.Next(0,50)};
            return res;
        }
    }
}