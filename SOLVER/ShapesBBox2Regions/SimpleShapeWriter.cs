using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace ShapesBBox2Regions
{
    public class SimpleSHPWriter: FileStream
    {
        private MyBitConverter bcle = new MyBitConverter(true);
        private MyBitConverter bcbe = new MyBitConverter(false);

        private double MinX = double.MaxValue;
        private double MinY = double.MaxValue;
        private double MaxX = double.MinValue;
        private double MaxY = double.MinValue;
        
        private int shape_type = 5;       // 1 - point; 3 - line; 5 - area
        private int objects_recorded = 0;

        private SimpleSHPWriter(string fileName, int shape_type)
            : base(fileName, FileMode.Create, FileAccess.ReadWrite)
        {
            this.shape_type = shape_type;
            WriteHeader();
        }

        public static SimpleSHPWriter CreatePointsFile(string fileName) { return new SimpleSHPWriter(fileName, 1); }
        public static SimpleSHPWriter CreateLinesFile(string fileName) { return new SimpleSHPWriter(fileName, 3); }        
        public static SimpleSHPWriter CreateAreasFile(string fileName) { return new SimpleSHPWriter(fileName, 5); }

        public override void Close()
        {
            if (objects_recorded > 0) WriteBounds();            
            WriteFileLength();
            base.Close();
        }

        private void WriteHeader()
        {
            this.Position = 0;
            this.Write(bcbe.GetBytes((int)9994), 0, 4);       // File Code
            this.Write(new byte[20], 0 , 20);                 // Not used
            this.Write(bcbe.GetBytes((int)0), 0, 4);          // File_Length / 2
            this.Write(bcle.GetBytes((int)1000), 0, 4);       // Version 1000
            this.Write(bcle.GetBytes((int)shape_type), 0, 4); // Shape Type
            this.Write(bcle.GetBytes((double)-180), 0, 8);    // min x
            this.Write(bcle.GetBytes((double)-90), 0, 8);     // min y
            this.Write(bcle.GetBytes((double)180), 0, 8);     // max x
            this.Write(bcle.GetBytes((double)90), 0, 8);      // max y
            this.Write(new byte[32], 0, 32);                  // end of header
        }

        private void WriteFileLength()
        {
            long pos = this.Position;
            this.Position = 24;
            this.Write(bcbe.GetBytes((int)(this.Length / 2)), 0, 4);
            this.Position = pos;
        }

        private void WriteBounds()
        {
            long pos = this.Position;
            this.Position = 36;
            this.Write(bcle.GetBytes((double)MinX), 0, 8);   // min x
            this.Write(bcle.GetBytes((double)MinY), 0, 8);   // min y
            this.Write(bcle.GetBytes((double)MaxX), 0, 8);   // max x
            this.Write(bcle.GetBytes((double)MaxY), 0, 8);   // max y
            this.Position = pos;
        }
       
        public void WritePoint(double x, double y)
        {
            if (shape_type != 1)
                throw new Exception("Shape file is not Point");

            objects_recorded++;

            this.Write(bcbe.GetBytes((int)objects_recorded), 0, 4); // record number
            this.Write(bcbe.GetBytes((int)10), 0, 4);             // content length / 2

            this.Write(bcle.GetBytes((int)1), 0, 4); // shape type      
            this.Write(bcle.GetBytes(x), 0, 8);      // x
            this.Write(bcle.GetBytes(y), 0, 8);      // y

            if (x < MinX) MinX = x;
            if (y < MinY) MinY = y;
            if (x > MaxX) MaxX = x;
            if (y > MaxY) MaxY = y;
        }

        public void WriteSingleLine(PointF[] line)
        {
            if (shape_type != 3)
                throw new Exception("Shape file is not Line");

            objects_recorded++;

            this.Write(bcbe.GetBytes((int)objects_recorded), 0, 4); // record number
            this.Write(bcbe.GetBytes((int)((48 + line.Length * 2 * 8) / 2)), 0, 4); // content length / 2

            this.Write(bcle.GetBytes((int)3), 0, 4); // shape type
            double[] bounds = GetBounds(line); // bounds
            this.Write(bcle.GetBytes((double)bounds[0]), 0, 8);   // min x
            this.Write(bcle.GetBytes((double)bounds[1]), 0, 8);   // min y
            this.Write(bcle.GetBytes((double)bounds[2]), 0, 8);   // max x
            this.Write(bcle.GetBytes((double)bounds[3]), 0, 8);   // max y
            this.Write(bcle.GetBytes((int)1), 0, 4);              // number of parts
            this.Write(bcle.GetBytes((int)line.Length), 0, 4);    // number of points
            this.Write(bcle.GetBytes((int)0), 0, 4);              // Parts starts
            for (int i = 0; i < line.Length; i++)                 // Points
            {
                this.Write(bcle.GetBytes((double)line[i].X), 0, 8);      // x
                this.Write(bcle.GetBytes((double)line[i].Y), 0, 8);      // y
            };

            if (bounds[0] < MinX) MinX = bounds[0];
            if (bounds[1] < MinY) MinY = bounds[1];
            if (bounds[2] > MaxX) MaxX = bounds[2];
            if (bounds[3] > MaxY) MaxY = bounds[3];
        }

        public void WriteSingleArea(PointF[] area)
        {
            if (shape_type != 5)
                throw new Exception("Shape file is not Area");

            objects_recorded++;

            this.Write(bcbe.GetBytes((int)objects_recorded), 0, 4); // record number
            this.Write(bcbe.GetBytes((int)((48 + area.Length * 2 * 8) / 2)), 0, 4); // content length / 2

            this.Write(bcle.GetBytes((int)5), 0, 4);              // shape type
            double[] bounds = GetBounds(area);                          // bounds
            this.Write(bcle.GetBytes((double)bounds[0]), 0, 8);   // min x
            this.Write(bcle.GetBytes((double)bounds[1]), 0, 8);   // min y
            this.Write(bcle.GetBytes((double)bounds[2]), 0, 8);   // max x
            this.Write(bcle.GetBytes((double)bounds[3]), 0, 8);   // max y
            this.Write(bcle.GetBytes((int)1), 0, 4);              // number of parts
            this.Write(bcle.GetBytes((int)area.Length), 0, 4);    // number of points
            this.Write(bcle.GetBytes((int)0), 0, 4);              // Parts starts
            for (int i = 0; i < area.Length; i++)                 // Points
            {
                this.Write(bcle.GetBytes((double)area[i].X), 0, 8);      // x
                this.Write(bcle.GetBytes((double)area[i].Y), 0, 8);      // y
            };

            if (bounds[0] < MinX) MinX = bounds[0];
            if (bounds[1] < MinY) MinY = bounds[1];
            if (bounds[2] > MaxX) MaxX = bounds[2];
            if (bounds[3] > MaxY) MaxY = bounds[3];
        }

        private static double[] GetBounds(PointF[] vector)
        {
            double[] res = new double[] {double.MaxValue,double.MaxValue,double.MinValue,double.MinValue};
            for (int i = 0; i < vector.Length; i++)
            {
                if (vector[i].X < res[0]) res[0] = vector[i].X;
                if (vector[i].Y < res[1]) res[1] = vector[i].Y;
                if (vector[i].X > res[2]) res[2] = vector[i].X;                
                if (vector[i].Y > res[3]) res[3] = vector[i].Y;
            };
            return res;
        }        
    }

    public class MyBitConverter
    {
        /// <summary>
        ///     Constructor
        /// </summary>
        public MyBitConverter()
        {

        }

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="IsLittleEndian">Indicates the byte order ("endianess") in which data is stored in this computer architecture.</param>
        public MyBitConverter(bool IsLittleEndian)
        {
            this.isLittleEndian = IsLittleEndian;
        }

        /// <summary>
        ///     Indicates the byte order ("endianess") in which data is stored in this computer
        /// architecture.
        /// </summary>
        private bool isLittleEndian = true;

        /// <summary>
        /// Indicates the byte order ("endianess") in which data is stored in this computer
        /// architecture.
        ///</summary>
        public bool IsLittleEndian { get { return isLittleEndian; } set { isLittleEndian = value; } } // should default to false, which is what we want for Empire

        /// <summary>
        /// Converts the specified double-precision floating point number to a 64-bit
        /// signed integer.
        ///
        /// Parameters:
        /// value:
        /// The number to convert.
        ///
        /// Returns:
        /// A 64-bit signed integer whose value is equivalent to value.
        ///</summary>
        public long DoubleToInt64Bits(double value) { throw new NotImplementedException(); }
        ///
        /// <summary>
        /// Returns the specified Boolean value as an array of bytes.
        ///
        /// Parameters:
        /// value:
        /// A Boolean value.
        ///
        /// Returns:
        /// An array of bytes with length 1.
        ///</summary>
        public byte[] GetBytes(bool value)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                byte[] res = System.BitConverter.GetBytes(value);
                Array.Reverse(res);
                return res;
            }
        }
        ///
        /// <summary>
        /// Returns the specified Unicode character value as an array of bytes.
        ///
        /// Parameters:
        /// value:
        /// A character to convert.
        ///
        /// Returns:
        /// An array of bytes with length 2.
        ///</summary>
        public byte[] GetBytes(char value)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                byte[] res = System.BitConverter.GetBytes(value);
                Array.Reverse(res);
                return res;
            }
        }
        ///
        /// <summary>
        /// Returns the specified double-precision floating point value as an array of
        /// bytes.
        ///
        /// Parameters:
        /// value:
        /// The number to convert.
        ///
        /// Returns:
        /// An array of bytes with length 8.
        ///</summary>
        public byte[] GetBytes(double value)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                byte[] res = System.BitConverter.GetBytes(value);
                Array.Reverse(res);
                return res;
            }
        }
        ///
        /// <summary>
        /// Returns the specified single-precision floating point value as an array of
        /// bytes.
        ///
        /// Parameters:
        /// value:
        /// The number to convert.
        ///
        /// Returns:
        /// An array of bytes with length 4.
        ///</summary>
        public byte[] GetBytes(float value)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                byte[] res = System.BitConverter.GetBytes(value);
                Array.Reverse(res);
                return res;
            }
        }
        ///
        /// <summary>
        /// Returns the specified 32-bit signed integer value as an array of bytes.
        ///
        /// Parameters:
        /// value:
        /// The number to convert.
        ///
        /// Returns:
        /// An array of bytes with length 4.
        ///</summary>
        public byte[] GetBytes(int value)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                byte[] res = System.BitConverter.GetBytes(value);
                Array.Reverse(res);
                return res;
            }
        }
        ///
        /// <summary>
        /// Returns the specified 64-bit signed integer value as an array of bytes.
        ///
        /// Parameters:
        /// value:
        /// The number to convert.
        ///
        /// Returns:
        /// An array of bytes with length 8.
        ///</summary>
        public byte[] GetBytes(long value)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                byte[] res = System.BitConverter.GetBytes(value);
                Array.Reverse(res);
                return res;
            }
        }
        ///
        /// <summary>
        /// Returns the specified 16-bit signed integer value as an array of bytes.
        ///
        /// Parameters:
        /// value:
        /// The number to convert.
        ///
        /// Returns:
        /// An array of bytes with length 2.
        ///</summary>
        public byte[] GetBytes(short value)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                byte[] res = System.BitConverter.GetBytes(value);
                Array.Reverse(res);
                return res;
            }
        }
        ///
        /// <summary>
        /// Returns the specified 32-bit unsigned integer value as an array of bytes.
        ///
        /// Parameters:
        /// value:
        /// The number to convert.
        ///
        /// Returns:
        /// An array of bytes with length 4.
        ///</summary>
        public byte[] GetBytes(uint value)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                byte[] res = System.BitConverter.GetBytes(value);
                Array.Reverse(res);
                return res;
            }
        }
        ///
        /// <summary>
        /// Returns the specified 64-bit unsigned integer value as an array of bytes.
        ///
        /// Parameters:
        /// value:
        /// The number to convert.
        ///
        /// Returns:
        /// An array of bytes with length 8.
        ///</summary>
        public byte[] GetBytes(ulong value)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                byte[] res = System.BitConverter.GetBytes(value);
                Array.Reverse(res);
                return res;
            }
        }
        ///
        /// <summary>
        /// Returns the specified 16-bit unsigned integer value as an array of bytes.
        ///
        /// Parameters:
        /// value:
        /// The number to convert.
        ///
        /// Returns:
        /// An array of bytes with length 2.
        ///</summary>
        public byte[] GetBytes(ushort value)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                byte[] res = System.BitConverter.GetBytes(value);
                Array.Reverse(res);
                return res;
            }
        }
        ///
        /// <summary>
        /// Converts the specified 64-bit signed integer to a double-precision floating
        /// point number.
        ///
        /// Parameters:
        /// value:
        /// The number to convert.
        ///
        /// Returns:
        /// A double-precision floating point number whose value is equivalent to value.
        ///</summary>
        public double Int64BitsToDouble(long value) { throw new NotImplementedException(); }
        ///
        /// <summary>
        /// Returns a Boolean value converted from one byte at a specified position in
        /// a byte array.
        ///
        /// Parameters:
        /// value:
        /// An array of bytes.
        ///
        /// startIndex:
        /// The starting position within value.
        ///
        /// Returns:
        /// true if the byte at startIndex in value is nonzero; otherwise, false.
        ///
        /// Exceptions:
        /// System.ArgumentNullException:
        /// value is null.
        ///
        /// System.ArgumentOutOfRangeException:
        /// startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public bool ToBoolean(byte[] value, int startIndex) { throw new NotImplementedException(); }
        ///
        /// <summary>
        /// Returns a Unicode character converted from two bytes at a specified position
        /// in a byte array.
        ///
        /// Parameters:
        /// value:
        /// An array.
        ///
        /// startIndex:
        /// The starting position within value.
        ///
        /// Returns:
        /// A character formed by two bytes beginning at startIndex.
        ///
        /// Exceptions:
        /// System.ArgumentException:
        /// startIndex equals the length of value minus 1.
        ///
        /// System.ArgumentNullException:
        /// value is null.
        ///
        /// System.ArgumentOutOfRangeException:
        /// startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public char ToChar(byte[] value, int startIndex) { throw new NotImplementedException(); }
        ///
        /// <summary>
        /// Returns a double-precision floating point number converted from eight bytes
        /// at a specified position in a byte array.
        ///
        /// Parameters:
        /// value:
        /// An array of bytes.
        ///
        /// startIndex:
        /// The starting position within value.
        ///
        /// Returns:
        /// A double precision floating point number formed by eight bytes beginning
        /// at startIndex.
        ///
        /// Exceptions:
        /// System.ArgumentException:
        /// startIndex is greater than or equal to the length of value minus 7, and is
        /// less than or equal to the length of value minus 1.
        ///
        /// System.ArgumentNullException:
        /// value is null.
        ///
        /// System.ArgumentOutOfRangeException:
        /// startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public double ToDouble(byte[] value, int startIndex) { throw new NotImplementedException(); }
        ///
        /// <summary>
        /// Returns a 16-bit signed integer converted from two bytes at a specified position
        /// in a byte array.
        ///
        /// Parameters:
        /// value:
        /// An array of bytes.
        ///
        /// startIndex:
        /// The starting position within value.
        ///
        /// Returns:
        /// A 16-bit signed integer formed by two bytes beginning at startIndex.
        ///
        /// Exceptions:
        /// System.ArgumentException:
        /// startIndex equals the length of value minus 1.
        ///
        /// System.ArgumentNullException:
        /// value is null.
        ///
        /// System.ArgumentOutOfRangeException:
        /// startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public short ToInt16(byte[] value, int startIndex)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.ToInt16(value, startIndex);
            }
            else
            {
                byte[] res = (byte[])value.Clone();
                Array.Reverse(res);
                return System.BitConverter.ToInt16(res, value.Length - sizeof(Int16) - startIndex);
            }
        }
        ///
        /// <summary>
        /// Returns a 32-bit signed integer converted from four bytes at a specified
        /// position in a byte array.
        ///
        /// Parameters:
        /// value:
        /// An array of bytes.
        ///
        /// startIndex:
        /// The starting position within value.
        ///
        /// Returns:
        /// A 32-bit signed integer formed by four bytes beginning at startIndex.
        ///
        /// Exceptions:
        /// System.ArgumentException:
        /// startIndex is greater than or equal to the length of value minus 3, and is
        /// less than or equal to the length of value minus 1.
        ///
        /// System.ArgumentNullException:
        /// value is null.
        ///
        /// System.ArgumentOutOfRangeException:
        /// startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public int ToInt32(byte[] value, int startIndex)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.ToInt32(value, startIndex);
            }
            else
            {
                byte[] res = (byte[])value.Clone();
                Array.Reverse(res);
                return System.BitConverter.ToInt32(res, value.Length - sizeof(Int32) - startIndex);
            }
        }
        ///
        /// <summary>
        /// Returns a 64-bit signed integer converted from eight bytes at a specified
        /// position in a byte array.
        ///
        /// Parameters:
        /// value:
        /// An array of bytes.
        ///
        /// startIndex:
        /// The starting position within value.
        ///
        /// Returns:
        /// A 64-bit signed integer formed by eight bytes beginning at startIndex.
        ///
        /// Exceptions:
        /// System.ArgumentException:
        /// startIndex is greater than or equal to the length of value minus 7, and is
        /// less than or equal to the length of value minus 1.
        ///
        /// System.ArgumentNullException:
        /// value is null.
        ///
        /// System.ArgumentOutOfRangeException:
        /// startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public long ToInt64(byte[] value, int startIndex)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.ToInt64(value, startIndex);
            }
            else
            {
                byte[] res = (byte[])value.Clone();
                Array.Reverse(res);
                return System.BitConverter.ToInt64(res, value.Length - sizeof(Int64) - startIndex);
            }
        }
        ///
        /// <summary>
        /// Returns a single-precision floating point number converted from four bytes
        /// at a specified position in a byte array.
        ///
        /// Parameters:
        /// value:
        /// An array of bytes.
        ///
        /// startIndex:
        /// The starting position within value.
        ///
        /// Returns:
        /// A single-precision floating point number formed by four bytes beginning at
        /// startIndex.
        ///
        /// Exceptions:
        /// System.ArgumentException:
        /// startIndex is greater than or equal to the length of value minus 3, and is
        /// less than or equal to the length of value minus 1.
        ///
        /// System.ArgumentNullException:
        /// value is null.
        ///
        /// System.ArgumentOutOfRangeException:
        /// startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public float ToSingle(byte[] value, int startIndex)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.ToSingle(value, startIndex);
            }
            else
            {
                byte[] res = (byte[])value.Clone();
                Array.Reverse(res);
                return System.BitConverter.ToSingle(res, value.Length - sizeof(Single) - startIndex);
            }
        }
        ///
        /// <summary>
        /// Converts the numeric value of each element of a specified array of bytes
        /// to its equivalent hexadecimal string representation.
        ///
        /// Parameters:
        /// value:
        /// An array of bytes.
        ///
        /// Returns:
        /// A System.String of hexadecimal pairs separated by hyphens, where each pair
        /// represents the corresponding element in value; for example, "7F-2C-4A".
        ///
        /// Exceptions:
        /// System.ArgumentNullException:
        /// value is null.
        ///</summary>
        public string ToString(byte[] value)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.ToString(value);
            }
            else
            {
                byte[] res = (byte[])value.Clone();
                Array.Reverse(res);
                return System.BitConverter.ToString(res);
            }
        }
        ///
        /// <summary>
        /// Converts the numeric value of each element of a specified subarray of bytes
        /// to its equivalent hexadecimal string representation.
        ///
        /// Parameters:
        /// value:
        /// An array of bytes.
        ///
        /// startIndex:
        /// The starting position within value.
        ///
        /// Returns:
        /// A System.String of hexadecimal pairs separated by hyphens, where each pair
        /// represents the corresponding element in a subarray of value; for example,
        /// "7F-2C-4A".
        ///
        /// Exceptions:
        /// System.ArgumentNullException:
        /// value is null.
        ///
        /// System.ArgumentOutOfRangeException:
        /// startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public string ToString(byte[] value, int startIndex)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.ToString(value, startIndex);
            }
            else
            {
                byte[] res = (byte[])value.Clone();
                Array.Reverse(res, startIndex, value.Length - startIndex);
                return System.BitConverter.ToString(res, startIndex);
            }
        }
        ///
        /// <summary>
        /// Converts the numeric value of each element of a specified subarray of bytes
        /// to its equivalent hexadecimal string representation.
        ///
        /// Parameters:
        /// value:
        /// An array of bytes.
        ///
        /// startIndex:
        /// The starting position within value.
        ///
        /// length:
        /// The number of array elements in value to convert.
        ///
        /// Returns:
        /// A System.String of hexadecimal pairs separated by hyphens, where each pair
        /// represents the corresponding element in a subarray of value; for example,
        /// "7F-2C-4A".
        ///
        /// Exceptions:
        /// System.ArgumentNullException:
        /// value is null.
        ///
        /// System.ArgumentOutOfRangeException:
        /// startIndex or length is less than zero. -or- startIndex is greater than
        /// zero and is greater than or equal to the length of value.
        ///
        /// System.ArgumentException:
        /// The combination of startIndex and length does not specify a position within
        /// value; that is, the startIndex parameter is greater than the length of value
        /// minus the length parameter.
        ///</summary>
        public string ToString(byte[] value, int startIndex, int length)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.ToString(value, startIndex, length);
            }
            else
            {
                byte[] res = (byte[])value.Clone();
                Array.Reverse(res, startIndex, length);
                return System.BitConverter.ToString(res, startIndex, length);
            }
        }
        ///
        /// <summary>
        /// Returns a 16-bit unsigned integer converted from two bytes at a specified
        /// position in a byte array.
        ///
        /// Parameters:
        /// value:
        /// The array of bytes.
        ///
        /// startIndex:
        /// The starting position within value.
        ///
        /// Returns:
        /// A 16-bit unsigned integer formed by two bytes beginning at startIndex.
        ///
        /// Exceptions:
        /// System.ArgumentException:
        /// startIndex equals the length of value minus 1.
        ///
        /// System.ArgumentNullException:
        /// value is null.
        ///
        /// System.ArgumentOutOfRangeException:
        /// startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public ushort ToUInt16(byte[] value, int startIndex)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.ToUInt16(value, startIndex);
            }
            else
            {
                byte[] res = (byte[])value.Clone();
                Array.Reverse(res);
                return System.BitConverter.ToUInt16(res, value.Length - sizeof(UInt16) - startIndex);
            }
        }
        ///
        /// <summary>
        /// Returns a 32-bit unsigned integer converted from four bytes at a specified
        /// position in a byte array.
        ///
        /// Parameters:
        /// value:
        /// An array of bytes.
        ///
        /// startIndex:
        /// The starting position within value.
        ///
        /// Returns:
        /// A 32-bit unsigned integer formed by four bytes beginning at startIndex.
        ///
        /// Exceptions:
        /// System.ArgumentException:
        /// startIndex is greater than or equal to the length of value minus 3, and is
        /// less than or equal to the length of value minus 1.
        ///
        /// System.ArgumentNullException:
        /// value is null.
        ///
        /// System.ArgumentOutOfRangeException:
        /// startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public uint ToUInt32(byte[] value, int startIndex)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.ToUInt32(value, startIndex);
            }
            else
            {
                byte[] res = (byte[])value.Clone();
                Array.Reverse(res);
                return System.BitConverter.ToUInt32(res, value.Length - sizeof(UInt32) - startIndex);
            }
        }
        ///
        /// <summary>
        /// Returns a 64-bit unsigned integer converted from eight bytes at a specified
        /// position in a byte array.
        ///
        /// Parameters:
        /// value:
        /// An array of bytes.
        ///
        /// startIndex:
        /// The starting position within value.
        ///
        /// Returns:
        /// A 64-bit unsigned integer formed by the eight bytes beginning at startIndex.
        ///
        /// Exceptions:
        /// System.ArgumentException:
        /// startIndex is greater than or equal to the length of value minus 7, and is
        /// less than or equal to the length of value minus 1.
        ///
        /// System.ArgumentNullException:
        /// value is null.
        ///
        /// System.ArgumentOutOfRangeException:
        /// startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public ulong ToUInt64(byte[] value, int startIndex)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.ToUInt64(value, startIndex);
            }
            else
            {
                byte[] res = (byte[])value.Clone();
                Array.Reverse(res);
                return System.BitConverter.ToUInt64(res, value.Length - sizeof(UInt64) - startIndex);
            }
        }
    }
}
