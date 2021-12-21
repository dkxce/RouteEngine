/* 
 * C# Class by Milok Zbrozek <milokz@gmail.com>
 * Модуль для рассчета маршрутов по графам, 
 * используя алгоритм Флойда — Уоршелла
 * в памяти и через дисковые файлы
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

namespace dkxce.Route.Matrix
{
    /// <summary>
    ///     Квадратная матрица расстояний
    ///     для предварительного просчета
    /// </summary>
    public class RMMatrix
    {
        /*
         * rm.main file:
         * 0x00 .... 0x07 : Header - RMHEADER
         * 4 bytes (int)   : Nodes
         * 4 bytes (single): max distance between modes
         * array of nodes:
         *          prev[uint]; 4
         *          cost[float]; 4
         *          dist[float]; 4
         *          time[float]; 4
         *          byreg[ushort]; 2
        */

        /// <summary>
        ///     Заголовок для файла матрицы
        /// </summary>
        private static byte[] rmheader = new byte[] { 0x52, 0x4d, 0x48, 0x45, 0x41, 0x44, 0x45, 0x52 };

        /// <summary>
        ///     Исп-ся для string-->float
        /// </summary>
        private System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InstalledUICulture;
        /// <summary>
        ///     Исп-ся для string-->float
        /// </summary>
        private System.Globalization.NumberFormatInfo ni;
        public System.Globalization.NumberFormatInfo DotDelimiter { get { return ni; } }


        /// <summary>
        ///     Максимальная длина между узлами
        /// </summary>
        private Single maxDistBetweenNodes = 0;

        /// <summary>
        ///     Максимальная длина между узлами
        /// </summary>
        public Single MaxDistanceBetweenNodes { get { return maxDistBetweenNodes; } }

        /// <summary>
        ///     Максимальная ошибка для расчетов (исп-ся для определения назаполненных клеток)
        /// </summary>
        private const Single maxError = (Single)1e-6;

        /// <summary>
        ///     Максимальное значение оценки, больше которого оценка является бесконечной
        /// </summary>
        private const Single maxValue = (Single)1e+30;

        /// <summary>
        ///     Смещение предыдущего узла пути в матрице относительно начала ячейки
        /// </summary>
        private const byte prevOffset = 0;

        /// <summary>
        ///     Смещение оценки в матрице относительно начала ячейки
        /// </summary>
        private const byte costOffset = 4;

        /// <summary>
        ///     Смещение дистанции в матрице относительно начала ячейки
        /// </summary>
        private const byte distOffset = 4 + 4;

        /// <summary>
        ///     Смещение времени в матрице относительно начала ячейки
        /// </summary>
        private const byte timeOffset = 4 + 4 + 4;

        /// <summary>
        ///     Смещение региона в матрице относительно начала ячейки
        /// </summary>
        private const ushort byRegOffset = 4 + 4 + 4 + 4;

        /// <summary>
        ///     Размер ячейки матрицы
        /// </summary>
        private const uint recordLength = 4 + 4 + 4 + 4 + 2; // prev_node + cost + dist + time + byreg
        private const uint max_nodes = 10000; // Max file size: 2GB

        /// <summary>
        ///     Матрица 
        /// </summary>
        private Stream matrixStream = null;

        /// <summary>
        ///     Файл матрицы
        /// </summary>
        private string matrixFile = null;

        /// <summary>
        ///     В памяти ли матрица или в файле
        /// </summary>
        private bool inMemory = false;

        /// <summary>
        ///     Если матрица уже просчитана и загружена для работы,
        ///     а не для добавления путей
        /// </summary>
        private bool inReadMode = false;


        /// <summary>
        ///     Число узлов матрицы
        /// </summary>
        private int size = 0;

        /// <summary>
        ///     Matrix File Name
        /// </summary>
        public string FileName { get { return matrixFile; } }

        /// <summary>
        ///     Matrix In Memory Or Not
        /// </summary>
        public bool InMemory { get { return inMemory; } }
        /// <summary>
        ///     Если матрица уже просчитана и загружена для работы,
        ///     а не для добавления путей
        /// </summary>
        public bool InReadMode { get { return inReadMode; } }

        /// <summary>
        ///     Nodes in Matrix
        /// </summary>
        public int NodesCount { get { return this.size; } }

        /// <summary>
        ///     PRIVATE CREATE ONLY
        /// </summary>
        private RMMatrix()
        {
            ni = (System.Globalization.NumberFormatInfo)ci.NumberFormat.Clone();
            ni.NumberDecimalSeparator = ".";
        }

        /// <summary>
        ///     Создаем матрицу в файле(ах)
        /// </summary>
        /// <param name="nodes">Число узлов матрицы</param>
        /// <param name="fileName">Путь к файлам</param>
        /// <returns></returns>
        public static RMMatrix CreateOnDisk(int nodes, string fileName)
        {
            if (nodes > max_nodes) throw new OverflowException("Maximum nodes count is " + max_nodes.ToString());

            RMMatrix rm = new RMMatrix();
            rm.inMemory = false;
            rm.matrixFile = fileName;
            rm.size = nodes;
            rm.matrixStream = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite);
            rm.matrixStream.SetLength(rm.size * rm.size * recordLength + rmheader.Length + 8); // header + count + max_dist + data
            rm.matrixStream.Write(rmheader, 0, rmheader.Length);
            byte[] ba = BitConverter.GetBytes(nodes);
            rm.matrixStream.Write(ba, 0, ba.Length);
            ba = BitConverter.GetBytes(float.MaxValue);
            rm.matrixStream.Write(ba, 0, ba.Length);
            rm.matrixStream.Flush();
            return rm;
        }

        /// <summary>
        ///     Создаем матрицу в памяти
        /// </summary>
        /// <param name="nodes">Число узлов матрицы</param>
        /// <returns></returns>
        public static RMMatrix CreateInMemory(int nodes)
        {
            if (nodes > max_nodes) throw new OverflowException("Maximum nodes count is " + max_nodes.ToString());

            RMMatrix rm = new RMMatrix();
            rm.inMemory = true;
            rm.matrixFile = "";
            rm.size = nodes;
            rm.matrixStream = new MemoryStream();
            rm.matrixStream.SetLength(rm.size * rm.size * recordLength + rmheader.Length + 8); // header + count + max_dist + data
            rm.matrixStream.Write(rmheader, 0, rmheader.Length);
            byte[] ba = BitConverter.GetBytes(nodes);
            rm.matrixStream.Write(ba, 0, ba.Length);
            ba = BitConverter.GetBytes(float.MaxValue);
            rm.matrixStream.Write(ba, 0, ba.Length);
            rm.matrixStream.Flush();
            return rm;
        }

        /// <summary>
        ///     Открываем файлы для работы с матрицей без загрузки в память
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static RMMatrix WorkWithDisk(string fileName)
        {
            RMMatrix rm = new RMMatrix();
            rm.inReadMode = true;
            rm.matrixStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            byte[] ba = new byte[rmheader.Length];
            rm.matrixStream.Read(ba, 0, ba.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(ba) != "RMHEADER")
            {
                rm.matrixStream.Close();
                throw new IOException("Unknown file format:\r\n" + fileName);
            };
            ba = new byte[4];
            rm.matrixStream.Read(ba, 0, ba.Length);
            rm.size = BitConverter.ToInt32(ba, 0);
            rm.matrixStream.Read(ba, 0, ba.Length);
            rm.maxDistBetweenNodes = BitConverter.ToSingle(ba, 0);
            rm.inMemory = false;
            rm.matrixFile = fileName;

            return rm;
        }

        /// <summary>
        ///     Загружаем матрицу из файла в память
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static RMMatrix LoadToMemory(string fileName)
        {
            RMMatrix rm = new RMMatrix();
            rm.inReadMode = true;
            rm.matrixStream = new MemoryStream();

            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            int read = 0;
            byte[] ba = new byte[8192];
            while ((read = fs.Read(ba, 0, ba.Length)) > 0)
            {
                rm.matrixStream.Write(ba, 0, read);
                rm.matrixStream.Flush();
            };
            fs.Close();

            rm.matrixStream.Position = 0;
            ba = new byte[rmheader.Length];
            rm.matrixStream.Read(ba, 0, ba.Length);
            if (System.Text.Encoding.GetEncoding(1251).GetString(ba) != "RMHEADER")
            {
                rm.matrixStream.Close();
                throw new IOException("Unknown file format:\r\n" + fileName);
            };
            ba = new byte[4];
            rm.matrixStream.Read(ba, 0, ba.Length);
            rm.size = BitConverter.ToInt32(ba, 0);
            rm.matrixStream.Read(ba, 0, ba.Length);
            rm.maxDistBetweenNodes = BitConverter.ToSingle(ba, 0);
            rm.inMemory = true;
            rm.matrixFile = fileName;

            return rm;
        }

        /// <summary>
        ///     Устанавливаем предшествующую точку next пути из X в Y
        /// </summary>
        /// <param name="x">начальная точка</param>
        /// <param name="y">конечная точка</param>
        /// <param name="next">конечная точка - 1</param>
        private void SetPrev(uint x, uint y, uint prev)
        {
            if (x == y) return;

            matrixStream.Position = rmheader.Length + 8 + (x - 1) * size * recordLength + (y - 1) * recordLength + prevOffset;
            byte[] arr = BitConverter.GetBytes(prev);
            matrixStream.Write(arr, 0, arr.Length);
        }

        /// <summary>
        ///     Получаем предшествующую точку пути из X в Y
        /// </summary>
        /// <param name="x">начальная точка</param>
        /// <param name="y">конечная точка</param>
        /// <returns>конечная точка - 1</returns>
        private uint GetPrev(uint x, uint y)
        {
            if (x == 0) return 0;
            if (y == 0) return 0;
            if (x == y) return y;

            matrixStream.Position = rmheader.Length + 8 + (x - 1) * size * recordLength + (y - 1) * recordLength + prevOffset;
            byte[] arr = new byte[4];
            matrixStream.Read(arr, 0, 4);
            return (uint)BitConverter.ToUInt32(arr, 0);
        }

        /// <summary>
        ///     Получаем регион маршрута пути из X в Y
        /// </summary>
        /// <param name="x">начальная точка</param>
        /// <param name="y">конечная точка</param>
        /// <returns>регион</returns>
        public ushort GetByReg(uint x, uint y)
        {
            if (x == 0) return 0;
            if (y == 0) return 0;
            if (x == y) return 0;

            matrixStream.Position = rmheader.Length + 8 + (x - 1) * size * recordLength + (y - 1) * recordLength + byRegOffset;
            byte[] arr = new byte[2];
            matrixStream.Read(arr, 0, 2);
            return (ushort)BitConverter.ToUInt16(arr, 0);
        }

        /// <summary>
        ///     Получаем расстояние из точки X в Y
        /// </summary>
        /// <param name="x">начальная точка</param>
        /// <param name="y">конечная точка</param>
        /// <returns>Расстояние</returns>
        public Single GetRouteDist(uint x, uint y)
        {
            if (x == 0) return 0;
            if (y == 0) return 0;
            if (x == y) return 0;

            matrixStream.Position = rmheader.Length + 8 + (x - 1) * size * recordLength + (y - 1) * recordLength + distOffset;
            byte[] arr = new byte[4];
            matrixStream.Read(arr, 0, 4);
            Single s = BitConverter.ToSingle(arr, 0);
            return s < maxError ? Single.MaxValue : s;
        }

        /// <summary>
        ///     Устанавливаем расстояние из точки X в Y
        /// </summary>
        /// <param name="x">начальная точка</param>
        /// <param name="y">конечная точка</param>
        /// <param name="distance">Расстояние</param>
        private void SetDist(uint x, uint y, Single distance)
        {
            matrixStream.Position = rmheader.Length + 8 + (x - 1) * size * recordLength + (y - 1) * recordLength + distOffset;
            byte[] arr = BitConverter.GetBytes(distance);
            matrixStream.Write(arr, 0, 4);
        }

        /// <summary>
        ///     Получаем время движения из точки X в Y
        /// </summary>
        /// <param name="x">начальная точка</param>
        /// <param name="y">конечная точка</param>
        /// <returns>Время движения</returns>
        public Single GetRouteTime(uint x, uint y)
        {
            if (x == 0) return 0;
            if (y == 0) return 0;
            if (x == y) return 0;

            matrixStream.Position = rmheader.Length + 8 + (x - 1) * size * recordLength + (y - 1) * recordLength + timeOffset;
            byte[] arr = new byte[4];
            matrixStream.Read(arr, 0, 4);
            Single d = BitConverter.ToSingle(arr, 0);
            return d < maxError ? Single.MaxValue : d;
        }

        // <summary>
        ///     Устанавливаем время движения из точки X в Y
        /// </summary>
        /// <param name="x">начальная точка</param>
        /// <param name="y">конечная точка</param>
        /// <param name="time">Время движения</param>
        private void SetTime(uint x, uint y, Single time)
        {
            matrixStream.Position = rmheader.Length + 8 + (x - 1) * size * recordLength + (y - 1) * recordLength + timeOffset;
            byte[] arr = BitConverter.GetBytes(time);
            matrixStream.Write(arr, 0, 4);
        }

        // <summary>
        ///     Устанавливаем через какой регион идет маршрут из точки X в Y
        /// </summary>
        /// <param name="x">начальная точка</param>
        /// <param name="y">конечная точка</param>
        /// <param name="byReg">Регион</param>
        private void SetReg(uint x, uint y, ushort byReg)
        {
            matrixStream.Position = rmheader.Length + 8 + (x - 1) * size * recordLength + (y - 1) * recordLength + byRegOffset;
            byte[] arr = BitConverter.GetBytes(byReg);
            matrixStream.Write(arr, 0, 2);
        }

        /// <summary>
        ///     Получаем оценку пути
        /// </summary>
        /// <param name="x">начальная точка</param>
        /// <param name="y">конечная точка</param>
        /// <returns>Оценка</returns>
        private Single GetCost(uint x, uint y)
        {
            if (x == 0) return Single.MaxValue;
            if (y == 0) return Single.MaxValue;
            if (x == y) return 0;

            matrixStream.Position = rmheader.Length + 8 + (x - 1) * size * recordLength + (y - 1) * recordLength + costOffset;
            byte[] arr = new byte[4];
            matrixStream.Read(arr, 0, 4);
            Single d = BitConverter.ToSingle(arr, 0);
            return d < maxError ? Single.MaxValue : d;
        }

        /// <summary>
        ///     Оценка пути из X в Y
        /// </summary>
        /// <param name="x">начальная точка</param>
        /// <param name="y">конечная точка</param>
        /// <returns>Оценка</returns>
        private void SetCost(uint x, uint y, Single cost)
        {
            if (x == 0) return;
            if (y == 0) return;
            if (x == y) return;

            matrixStream.Position = rmheader.Length + 8 + (x - 1) * size * recordLength + (y - 1) * recordLength + costOffset;
            byte[] arr = BitConverter.GetBytes(cost);
            matrixStream.Write(arr, 0, arr.Length);
        }

        /// <summary>
        ///     Добавляем путь в матрицу из точки X в Y
        /// </summary>
        /// <param name="x">начальная точка</param>
        /// <param name="y">конечная точка</param>
        /// <param name="cost">оценка пути</param>
        /// <param name="dist">длина пути</param>
        /// <param name="time">время движения в пути</param>
        /// <param name="byReg">через какой регион идет маршрут</param>
        public void AddWay(uint x, uint y, Single cost, Single dist, Single time, ushort byReg)
        {
            if (x == 0)
            {
                Console.WriteLine("ERROR: RGNode X Could n't be 0");
                throw new Exception("RGNode X Could n't be 0");
            };
            if (y == 0)
            {
                Console.WriteLine("ERROR: RGNode Y Could n't be 0");
                throw new Exception("RGNode Y Could n't be 0");
            };

            if (inReadMode) throw new IOException("Matrix in read-only mode");

            if (dist > maxDistBetweenNodes) maxDistBetweenNodes = dist;

            Single cc = GetCost(x, y);
            if (cc < cost)
            {
                //Console.WriteLine("cc < cost " + x.ToString() + " - > " + y.ToString());
                //Console.ReadLine();
                return;
            };

            SetCost(x, y, cost);
            SetPrev(x, y, x);
            SetDist(x, y, dist);
            SetTime(x, y, time);
            SetReg(x, y, byReg);

            matrixStream.Flush();
        }

        /// <summary>
        ///     Расчет матрицы по алгоритму Флойда — Уоршелла
        ///     (minimize by cost)
        /// </summary>
        public void Solve()
        {
            Console.WriteLine("Begin Solve...");
            Console.WriteLine("Initialize...");
            for (uint k = 1; k <= size; k++)
            {
                for (uint i = 1; i <= size; i++)
                {
                    for (uint j = 1; j <= size; j++)
                    {
                        float ikj = GetCost(i, k) + GetCost(k, j);
                        if (ikj < GetCost(i, j))
                        {
                            SetPrev(i, j, k); // SET THROUGH NODE
                            SetCost(i, j, ikj);                            
                            SetDist(i, j, GetRouteDist(i, k) + GetRouteDist(k, j));
                            SetTime(i, j, GetRouteTime(i, k) + GetRouteTime(k, j));
                        };
                        DoProgress(k, i, j);
                    };
                };
            };
            matrixStream.Flush();
            Console.WriteLine("Done");
        }


        /// <summary>
        ///     Индикация процесса анализа матрицы
        /// </summary>
        /// <param name="k"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        private void DoProgress(uint k, uint i, uint j)
        {
            if (i != last_i)
            {
                last_i = i;
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.WriteLine(String.Format("Optimize Route: {0:00000}->-{1:00000}", k, i, j));
            };
        }
        private uint last_i = 0;

        /// <summary>
        ///     Вовзращаем вектор промежуточных узлов из точки X в Y
        /// </summary>
        /// <param name="x">начальная точка</param>
        /// <param name="y">конечная точка</param>
        /// <returns></returns>
        public uint[] GetRouteWay(uint x, uint y)
        {
            if (x == 0) return null; // NO WAY
            if (y == 0) return null; // NO WAY
            if (x == y) return new uint[0]; // ALREADY
            if (GetCost(x, y) == 0) return new uint[0]; // ALREADY

            List<uint> arr = new List<uint>();
            uint through = GetPrev(x,y);
            if(through != x)
            {
                arr.AddRange(GetRouteWay(x,through));
                arr.Add(through);
                arr.AddRange(GetRouteWay(through,y));
            };         

            // OLD
            //uint intermediate = y;
            //List<uint> arr = new List<uint>();
            //while (
            //    ((intermediate = GetPrev(x, intermediate)) > 0) 
            //    && 
            //    (intermediate != x))
            //    arr.Add(intermediate);
            //uint[] a = arr.ToArray();
            //Array.Reverse(a);
            //return a;

            return arr.ToArray();
        }

        /// <summary>
        ///     Возвращаем оценку пути из точки X в Y
        /// </summary>
        /// <param name="x">начальная точка</param>
        /// <param name="y">конечная точка</param>
        /// <returns>Оценку</returns>
        public Single GetRouteCost(uint x, uint y)
        {
            return GetCost(x, y);
        }

        /// <summary>
        ///     Если матрица в памяти, то сохраняем ее в файл
        /// </summary>
        /// <param name="fileName"></param>
        public void SaveToFile(string fileName)
        {
            if (!inMemory) return;
            this.matrixFile = fileName;

            FileStream fs = new FileStream(fileName, FileMode.Create);
            byte[] ba = BitConverter.GetBytes(size);
            fs.Write(rmheader, 0, rmheader.Length);
            fs.Write(ba, 0, ba.Length);
            ba = BitConverter.GetBytes(maxDistBetweenNodes);
            fs.Write(ba, 0, ba.Length);
            int read = 0;
            ba = new byte[8192];
            matrixStream.Position = rmheader.Length + 8;
            while ((read = this.matrixStream.Read(ba, 0, ba.Length)) > 0)
            {
                fs.Write(ba, 0, read);
                fs.Flush();
            };
            fs.Flush();
            fs.Close();
        }

        /// <summary>
        ///     Для освобождения всех ресурсов
        ///     необходимо вызывать после конца всей работы с матрицей
        /// </summary>
        public void Close()
        {
            matrixStream.Position = rmheader.Length + 8;
            byte[] ba = BitConverter.GetBytes(maxDistBetweenNodes);
            matrixStream.Write(ba, 0, ba.Length);
            matrixStream.Flush();

            matrixStream.Close();
            matrixStream = null;

            inMemory = false;
            inReadMode = true;
        }

        ~RMMatrix()
        {
            if (matrixStream != null) Close();
        }

        /// <summary>
        ///     Выводит наглядную матрицу в текстовый файл
        ///     (рекомендуется только для небольших матриц)
        /// </summary>
        /// <param name="fn">Полный путь к файлу</param>
        public void SaveToTextFile(string fn)
        {
            FileStream fout = new FileStream(fn, FileMode.Create);
            StreamWriter sw = new StreamWriter(fout);
            sw.Write("COST;");
            for (uint y = 1; y <= size; y++) sw.Write(y.ToString() + ";");
            sw.WriteLine();
            for (uint x = 1; x <= size; x++)
            {
                sw.Write(x.ToString() + ";");
                for (uint y = 1; y <= size; y++)
                    if (GetCost(x, y) > maxValue)
                        sw.Write(". . .;");
                    else
                        sw.Write(GetPrev(x, y).ToString() + "(" + GetCost(x, y).ToString("0.000").Replace(",", ".") + ") R"+GetByReg(x,y).ToString()+" ;");
                sw.WriteLine();
            };
            sw.WriteLine();
            sw.Write("DIST;");
            for (uint y = 1; y <= size; y++) sw.Write(y.ToString() + ";");
            sw.WriteLine();
            for (uint x = 1; x <= size; x++)
            {
                sw.Write(x.ToString() + ";");
                for (uint y = 1; y <= size; y++)
                    if (GetCost(x, y) > maxValue)
                        sw.Write(". . .;");
                    else
                        sw.Write(GetPrev(x, y).ToString() + "(" + GetRouteDist(x, y).ToString("0.000").Replace(",", ".") + ") ;");
                sw.WriteLine();
            };
            sw.WriteLine();
            sw.Write("TIME;");
            for (uint y = 1; y <= size; y++) sw.Write(y.ToString() + ";");
            sw.WriteLine();
            for (uint x = 1; x <= size; x++)
            {
                sw.Write(x.ToString() + ";");
                for (uint y = 1; y <= size; y++)
                    if (GetCost(x, y) > maxValue)
                        sw.Write(". . .;");
                    else
                        sw.Write(GetPrev(x, y).ToString() + "(" + GetRouteTime(x, y).ToString("0.000").Replace(",", ".") + ") ;");
                sw.WriteLine();
            };
            sw.WriteLine();
            sw.Flush();
            fout.Close();
        }        
    }
}
