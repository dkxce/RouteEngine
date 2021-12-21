// http://www.realcoding.net/articles/struktura-dbf-failov-dlya-neprodvinutykh.html
// http://www.autopark.ru/ASBProgrammerGuide/DBFSTRUC.HTM#Table_9

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace DBFSharp
{        
    public class DBFFile: FileStream
    {
        private const byte MAIN_HEADER_SIZE = 32;
        private const byte FIELD_INFO_SIZE = 32;

        private bool _tenHeaderMode = true;
        private bool header_exists = false;
        private string filename;
        private uint records = 0;
        private uint _header_space = 0;

        private CodePageSet _cp = CodePageSet.Default;
        private FieldInfos _FieldInfos = new FieldInfos();
        public CodePageList CodePages = new CodePageList();
        private MyBitConverter bc = new MyBitConverter(true);

        public bool ShortenFieldNameMode { get { return _tenHeaderMode; } set { _tenHeaderMode = value; } }

        public DBFFile(string fileName) : base(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite)
        {
            filename = fileName;
            _cp = CodePageSet.Default;
            if (this.Length < MAIN_HEADER_SIZE)
                WriteHeader();
            else
                ReadHeader(0);
        }

        public DBFFile(string fileName, byte dbfCodePage) : base(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite)
        {
            filename = fileName;
            SetCodePage(dbfCodePage);
            if (this.Length == 0)
                WriteHeader();
            else
                ReadHeader(dbfCodePage);
        }        

        public DBFFile(string fileName, FileMode mode) : base(fileName, mode, FileAccess.ReadWrite)
        {
            filename = fileName;
            _cp = CodePageSet.Default;
            if (this.Length < MAIN_HEADER_SIZE)
                WriteHeader();
            else
                ReadHeader(0);
        }

        public DBFFile(string fileName, FileMode mode, byte dbfCodePage): base(fileName, mode, FileAccess.ReadWrite) 
        {
            filename = fileName;
            SetCodePage(dbfCodePage);
            if (this.Length == 0)
                WriteHeader();
            else
                ReadHeader(dbfCodePage);
        }

        public FieldInfos FieldInfos
        {
            get
            {
                return _FieldInfos;
            }
        }

        public byte FieldsCount
        {
            get
            {
                if (_FieldInfos == null) return 0;
                if (_FieldInfos.Count == 0) return 0;
                return (byte)_FieldInfos.Count;
            }
        }

        public ushort RecordSize
        {
            get
            {
                if (_FieldInfos == null) return 0;
                if (_FieldInfos.Count == 0) return 0;
                return _FieldInfos.RecordSize;
            }
        }

        public ushort HeaderSize
        {
            get
            {
                if (_FieldInfos == null) return 0;
                if (_FieldInfos.Count == 0) return 0;
                return (ushort)(_FieldInfos.Count * FIELD_INFO_SIZE + MAIN_HEADER_SIZE + 1 + _header_space);
            }
        }

        private ushort StdHeaderSize
        {
            get
            {
                if (_FieldInfos == null) return 0;
                if (_FieldInfos.Count == 0) return 0;
                return (ushort)(_FieldInfos.Count * FIELD_INFO_SIZE + MAIN_HEADER_SIZE + 1);
            }
        }

        public bool HeaderExists
        {
            get
            {
                return header_exists;
            }
        }

        public uint RecordsCount
        {
            get
            {
                return records;
            }
        }

        private void SetCodePage(byte dbfCodePage)
        {
            _cp = CodePages[dbfCodePage];
            if (_cp.headerCode == 0)
            {
                base.Close();
                throw new Exception("Unknown Code Page");
            };
            try
            {
                if (_cp.Encoding == null)
                {
                    base.Close();
                    throw new Exception("Unknown Code Page " + _cp.codePage);
                };
            }
            catch (Exception ex)
            {
                base.Close();
                throw new Exception("Unknown Code Page "+_cp.codePage+"\r\n"+ex.Message);
            };
        }

        private void ReadHeader(byte dbfCodePage)
        {
            byte[] buff;  
            // records count
            this.Position = 4;
            buff = new byte[4];
            this.Read(buff, 0, buff.Length);
            records = bc.ToUInt32(buff, 0);
            // header size
            buff = new byte[2];
            this.Read(buff, 0, buff.Length);
            ushort hdr_size = bc.ToUInt16(buff, 0);
            // record size
            this.Read(buff, 0, buff.Length);
            ushort rec_size = bc.ToUInt16(buff, 0);
            // code page
            this.Position = 29;
            byte cod_page = (byte)this.ReadByte();
            this.SetCodePage(dbfCodePage > 0 ? dbfCodePage : (cod_page > 0 ? cod_page : (byte)201));
            // read fields
            this.Position = 32;
            _FieldInfos = new FieldInfos();
            int tb = hdr_size - 2;
            while (this.Position < tb)
            {
                buff = new byte[11];
                this.Read(buff, 0, buff.Length);
                string fName = _cp.Encoding.GetString(buff).Trim('\0').Trim();
                byte ft = (byte)this.ReadByte();
                buff = new byte[4];
                this.Read(buff, 0, buff.Length);
                int offset = bc.ToInt32(buff, 0);
                byte fsize = (byte)this.ReadByte();
                byte dpnt = (byte)this.ReadByte();
                buff = new byte[14];
                this.Read(buff, 0, buff.Length);
                FieldInfo fi = new FieldInfo(fName, fsize, dpnt, (FieldType)ft);
                fi.offset = (ushort)offset;
                _FieldInfos.Add(fi);
            };
            this._header_space = (uint)hdr_size - this.StdHeaderSize;
            this.Position = hdr_size;
            header_exists = true;
        }

        private void WriteHeader()
        {
            byte[] buff = new byte[0];
            this.Position = 0;
            this.WriteByte(0x03);                               // 0 - TYPE + MEMO 0x83
            this.WriteByte((byte)(DateTime.UtcNow.Year % 100)); // 1 - YY
            this.WriteByte((byte)DateTime.UtcNow.Month);        // 2 - MM
            this.WriteByte((byte)DateTime.UtcNow.Day);          // 3 - DD
            buff = bc.GetBytes((uint)0); 
            this.Write(buff, 0, buff.Length);                   // 4 - records count
            buff = bc.GetBytes((ushort)0); 
            this.Write(buff, 0, buff.Length);                   // 8 - header size
            buff = bc.GetBytes((ushort)0); 
            this.Write(buff, 0, buff.Length);                   // 10 - record size
            this.WriteByte(0x00);                               // 12 - Reserved
            this.WriteByte(0x00);                               // 13 - Reserved
            this.WriteByte(0x00);                               // 14 - Ignored
            this.WriteByte(0x00);                               // 15 - Normal
            buff = new byte[12];
            this.Write(buff, 0, buff.Length);                   // 16 - reserved
            this.WriteByte(0x00);                               // 28 - No Index
            this.WriteByte((byte)_cp.headerCode);               // 29 - Code Page
            this.WriteByte(0x00);                               // 30 - Reserved
            this.WriteByte(0x00);                               // 31 - Reserved
        }

        public void WriteHeader(FieldInfos fields)
        {
            if (this.records > 0) throw new Exception("Can't write header if any records exists");
            _FieldInfos = fields;
            WriteHeader();

            byte[] buff = new byte[0];
            ushort hdr_size = MAIN_HEADER_SIZE + 1;
            int mhl = 0;

            fields.ReIndex();
            for (int i = 0; i < fields.Count; i++)
            {
                buff = fields[i].BName(_cp.Encoding);                
                if (fields[i].GName.Length > 11)
                    buff[10] = (byte)(0x41 + (mhl++));
                if(_tenHeaderMode) buff[10] = 0;
                this.Write(buff, 0, buff.Length);        // 0 - Field Name                
                buff = new byte[] { (byte)fields[i].FType };
                this.Write(buff, 0, buff.Length);        // 11 - Field Type
                buff = bc.GetBytes((int)fields[i].offset);
                this.Write(buff, 0, buff.Length);        // 12 - Field Offset
                this.WriteByte((byte)fields[i].FLength); // 16 - Field Size
                this.WriteByte((byte)fields[i].FDecimalPoint); // 17 - No Decimal Point
                buff = new byte[14];
                this.Write(buff, 0, buff.Length);        // 18 - Reserved

                hdr_size += FIELD_INFO_SIZE;
            };
            this.WriteByte(13); //TERMINAL BYTE    
            this.WriteByte(26); //TERMINAL BYTE

            this.Position = 8;
            buff = bc.GetBytes(hdr_size); // header size
            this.Write(buff, 0, buff.Length);
            buff = bc.GetBytes(fields.RecordSize); // record size
            this.Write(buff, 0, buff.Length);

            this.Position = hdr_size;
            header_exists = true;
        }

        private bool WriteData(Dictionary<string,object> record, long position)
        {
            if (!header_exists) throw new Exception("Header doesn't created or exists");
            if (record == null) return false;
            if (record.Count == 0) return false;
            if ((_FieldInfos == null) || (_FieldInfos.Count == 0)) return false;

            _FieldInfos.ZeroValues();
            foreach (KeyValuePair<string, object> kvp in record)
            {
                FieldInfo fi = _FieldInfos[kvp.Key];
                if (fi != null)
                    fi.FValue = kvp.Value;
            };

            this.Position = position;
            this.WriteByte(0x20); //_BEGIN RECORD_ //

            for (int i = 0; i < _FieldInfos.Count; i++)
            {
                byte[] def = new byte[_FieldInfos[i].FLength];
                if ((_FieldInfos[i].FType == FieldType.Numeric) || (_FieldInfos[i].FType == FieldType.Float))
                {
                    for (int x = 0; x < def.Length; x++)
                        def[x] = (byte)' ';                    
                    if (_FieldInfos[i].FValue != null)
                    {
                        string ddd = _FieldInfos[i].FLength.ToString();
                        string nf = "{0,-" + ddd + "}";
                        if (_FieldInfos[i].FDecimalPoint > 0) { nf = "{0,-" + ddd + ":0."; for (int x = 0; x < _FieldInfos[i].FDecimalPoint; x++) nf += "0"; nf += "}"; };
                        byte[] buff = _cp.Encoding.GetBytes(String.Format(System.Globalization.CultureInfo.InvariantCulture,nf, _FieldInfos[i].FValue));
                        if (buff.Length > def.Length)
                            throw new Exception("Numeric Value is too large: " + _FieldInfos[i].FValue.ToString());
                        Array.Copy(buff, 0, def, def.Length - buff.Length, buff.Length);
                    };
                };                
                if ((_FieldInfos[i].FType == FieldType.Memo) || (_FieldInfos[i].FType == FieldType.Binary) || (_FieldInfos[i].FType == FieldType.General) || (_FieldInfos[i].FType == FieldType.Picture))
                {
                    for (int x = 0; x < def.Length; x++)
                        def[x] = (byte)' ';
                    if (_FieldInfos[i].FValue != null)
                    {
                        byte[] buff = _cp.Encoding.GetBytes(_FieldInfos[i].FValue.ToString().Replace(",", "."));
                        if (buff.Length > def.Length)
                            throw new Exception("Meme Value is too large: " + _FieldInfos[i].FValue.ToString());
                        Array.Copy(buff, 0, def, def.Length - buff.Length, buff.Length);
                    };
                };
                if (_FieldInfos[i].FType == FieldType.Integer)
                {
                    def = bc.GetBytes((int)_FieldInfos[i].FValue);                    
                };
                if (_FieldInfos[i].FType == FieldType.Character)
                {
                    if (_FieldInfos[i].FValue != null)
                    {
                        byte[] buff = _cp.Encoding.GetBytes(_FieldInfos[i].FValue.ToString());
                        if (buff.Length > def.Length)
                            Array.Copy(buff, def, def.Length);
                        else
                            Array.Copy(buff, def, buff.Length);
                    }
                    else
                    {
                        byte[] buff = new byte[def.Length];
                        Array.Copy(buff, def, def.Length);                        
                    };
                };
                if (_FieldInfos[i].FType == FieldType.Logical)
                {
                    byte[] buff = new byte[] { (_FieldInfos[i].FValue == null) || (((bool)_FieldInfos[i].FValue) != true) ? (byte)((char)'F') : (byte)((char)'T')};
                    if (buff.Length > def.Length)
                        Array.Copy(buff, def, def.Length);
                    else
                        Array.Copy(buff, def, buff.Length);
                };
                if (_FieldInfos[i].FType == FieldType.Date)
                {
                    for (int x = 0; x < def.Length; x++)
                        def[x] = (byte)' ';
                    if ((_FieldInfos[i].FValue != null) && (_FieldInfos[i].FValue is DateTime))
                    {
                        byte[] buff = _cp.Encoding.GetBytes(((DateTime)_FieldInfos[i].FValue).ToString("yyyyMMdd")+"  ");
                        Array.Copy(buff, 0, def, def.Length - buff.Length, buff.Length);
                    };
                };
                if (_FieldInfos[i].FType == FieldType.DateTime)
                {
                    for (int x = 0; x < def.Length; x++)
                        def[x] = (byte)' ';
                    if ((_FieldInfos[i].FValue != null) && (_FieldInfos[i].FValue is DateTime))
                    {
                        byte[] buff = _cp.Encoding.GetBytes(((DateTime)_FieldInfos[i].FValue).ToString("yyyyMMddHHmmss"));
                        Array.Copy(buff, 0, def, def.Length - buff.Length, buff.Length);
                    };
                };
                this.Write(def, 0, def.Length);
            };
            return true;
        }

        public uint WriteRecord(Dictionary<string, object> record)
        {
            long pos = (long)this.HeaderSize + (long)this.records * (long)this.RecordSize;
            bool res = WriteData(record, pos);
            if (res) return this.records++;
            return 0;
        }

        public uint WriteRecord(object[] record)
        {
            if ((record == null) || (record.Length == 0)) return 0;
            if ((_FieldInfos == null) || (_FieldInfos.Count == 0)) return 0;
            Dictionary<string, object> rec = new Dictionary<string, object>();
            for (int i = 0; (i < record.Length) && (i < _FieldInfos.Count); i++)
                rec.Add(_FieldInfos[i].FName, record[i]);
            return WriteRecord(rec);
        }

        public uint WriteRecord(uint index, Dictionary<string, object> record)
        {
            if (index < 1) return 0;

            uint tmpi = index - 1;
            if (tmpi > this.records) return 0;
            long pos = (long)this.HeaderSize + (long)tmpi * (long)this.RecordSize;
            bool res = WriteData(record, pos);
            if (res) return tmpi == this.records ? ++this.records : index;
            return 0;
        }

        public uint WriteRecord(uint index, object[] record)
        {
            if ((record == null) || (record.Length == 0)) return 0;
            if ((_FieldInfos == null) || (_FieldInfos.Count == 0)) return 0;
            Dictionary<string, object> rec = new Dictionary<string, object>();
            for (int i = 0; (i < record.Length) && (i < _FieldInfos.Count); i++)
                rec.Add(_FieldInfos[i].FName, record[i]);
            return WriteRecord(index, rec);
        }

        public uint WriteRecord(Dictionary<string, object> record, long position)
        {
            ushort hs = HeaderSize;
            ushort rs = RecordSize;
            if (position < hs) return 0;
            if (position > this.Length) return 0;
            uint index = 1;
            for (long i = hs; i < this.Length; i += (long)RecordSize, index++)
            {
                if (position == i) return WriteRecord(record, index);
                if (position < i) break;
            };
            return 0;
        }

        public uint WriteRecord(object[] record, long position)
        {
            if ((record == null) || (record.Length == 0)) return 0;
            if ((_FieldInfos == null) || (_FieldInfos.Count == 0)) return 0;
            Dictionary<string, object> rec = new Dictionary<string, object>();
            for (int i = 0; (i < record.Length) && (i < _FieldInfos.Count); i++)
                rec.Add(_FieldInfos[i].FName, record[i]);
            return WriteRecord(rec, position);
        }

        private Dictionary<string, object> ReadData()
        {
            byte x20 = (byte)this.ReadByte(); //_BEGIN OF RECORD_ //
            if (x20 != 0x20) return null;

            Dictionary<string, object> result = new Dictionary<string, object>();

            for (int i = 0; i < _FieldInfos.Count; i++)
            {
                byte[] def = new byte[_FieldInfos[i].FLength];
                this.Read(def, 0, def.Length);
                string dv = _cp.Encoding.GetString(def).Trim('\0').Trim();
                object val = dv;

                if (_FieldInfos[i].FType == FieldType.Numeric)
                {
                    int tmi; long tml; float tmf; double tmd;
                    if (int.TryParse(dv, out tmi))
                        val = tmi;
                    else if (long.TryParse(dv, out tml))
                        val = tml;
                    else if (float.TryParse(dv, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out tmf))
                        val = tmf;
                    else if (double.TryParse(dv, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out tmd))
                        val = tmd;
                };
                if (_FieldInfos[i].FType == FieldType.Float)
                {
                    float tmf; double tmd;
                    if (float.TryParse(dv, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out tmf))
                        val = tmf;
                    else if (double.TryParse(dv, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out tmd))
                        val = tmd;
                };
                if ((_FieldInfos[i].FType == FieldType.Memo) || (_FieldInfos[i].FType == FieldType.Binary) || (_FieldInfos[i].FType == FieldType.General) || (_FieldInfos[i].FType == FieldType.Picture))
                {
                    int tmi; long tml;
                    if (int.TryParse(dv, out tmi))
                        val = tmi;
                    else if (long.TryParse(dv, out tml))
                        val = tml;                    
                };
                if (_FieldInfos[i].FType == FieldType.Integer) 
                {
                    val = bc.ToInt32(def, 0);
                };
                // if (_FieldInfos[i].FType == FieldType.Characters) { };
                if (_FieldInfos[i].FType == FieldType.Logical) val = dv == "T";
                if (_FieldInfos[i].FType == FieldType.Date)
                {
                    DateTime dt;
                    if(DateTime.TryParseExact(dv,"yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out dt))
                        val = dt;
                };
                if (_FieldInfos[i].FType == FieldType.DateTime)
                {
                    DateTime dt;
                    if (DateTime.TryParseExact(dv, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out dt))
                        val = dt;
                };
                result.Add(_FieldInfos[i].FName, val);
            };

            return result;
        }

        public Dictionary<string, object> ReadRecord(uint index)
        {
            if (records == 0) return null;

            uint tmpi = index - 1;
            if (tmpi > this.records) return null;

            this.Position = (long)this.HeaderSize + (long)tmpi * (long)this.RecordSize;
            return ReadData();
        }
		
		public Dictionary<string, object> ReadNext()
        {
            if (records == 0) return null;            
            if(this.Position < this.HeaderSize) this.Position = (long)this.HeaderSize;
            return ReadData();
        }

        public System.Collections.Generic.IEnumerable<Dictionary<string, object>> ReadAllRecords()
        {
            if (this.records > 0)
            {
                this.Position = this.HeaderSize;
                for (uint i = 1; i <= records; i++)
                    yield return ReadData();
            };
        }

        public Dictionary<string, object> this[uint index]
        {
            get { return ReadRecord(index); }
            set { WriteRecord(index, value); }
        }

        private void WriteRecordsCount()
        {
            long pos = this.Position;
            this.Position = 4;
            byte[] buff = bc.GetBytes(records);
            this.Write(buff, 0, buff.Length);
            this.Position = pos;
        }

        public override void Close()
        {            
            WriteRecordsCount();
            this.Position = (long)this.HeaderSize + (long)this.records * (long)this.RecordSize;
            WriteByte(26); // TERMINAL BYTE
            base.Close();
        }
    }    

    public enum FieldType : byte
    {
        Numeric = (byte)'N',
        Float = (byte)'F',
        Character = (byte)'C',
        Logical = (byte)'L',
        Date = (byte)'D',
        Memo = (byte)'M',
        Binary = (byte)'B',
        General = (byte)'G',
        Picture = (byte)'P',
        Integer = (byte)'I',
        DateTime = (byte)'T'
    }

    public class FieldInfo
    {
        public string FName;
        public byte FLength;
        public byte FDecimalPoint = 0;
        public FieldType FType;

        internal ushort offset = 0;
        internal object FValue = null;

        public FieldInfo(string fName, byte fLength, FieldType fType)
        {
            this.FName = fName;
            this.FLength = fLength;
            this.FType = fType;
        }

        public FieldInfo(string fName, byte fLength, byte fDecimalPoint, FieldType fType)
        {
            this.FName = fName;
            this.FLength = fLength;
            this.FType = fType;
            this.FDecimalPoint = fDecimalPoint;
        }

        public string GName
        {
            get
            {
                return this.FName.ToUpper();
            }
        }

        public byte[] BName(Encoding encoding)
        {
            byte[] res = new byte[11];
            byte[] bb = encoding.GetBytes(this.GName);
            for (int i = 0; (i < bb.Length) && (i < res.Length); i++)
                res[i] = bb[i];
            return res;
        }
    }

    public class FieldInfos : List<FieldInfo>
    {
        public void Add(FieldInfo fi)
        {
            if (this.Count > 0)
                foreach (FieldInfo ff in this)
                    if (ff.FName.ToUpper() == fi.FName.ToUpper())
                        throw new Exception(String.Format("Field with name {0} already exists.", ff.FName));
            CheckValidLength(fi.FType, ref fi.FLength, ref fi.FDecimalPoint);
            base.Add(fi);
        }

        public void Add(string fName, byte fLength, FieldType fType)
        {
            if (this.Count > 0)
                foreach (FieldInfo ff in this)
                    if (ff.FName.ToUpper() == fName.ToUpper())
                        throw new Exception(String.Format("Field with name {0} already exists.", ff.FName));
            CheckValidLength(fType, ref fLength);
            this.Add(new FieldInfo(fName, fLength, fType));
        }

        public void Add(string fName, byte fLength, byte fDecimalPoint, FieldType fType)
        {
            if (this.Count > 0)
                foreach (FieldInfo ff in this)
                    if (ff.FName.ToUpper() == fName.ToUpper())
                        throw new Exception(String.Format("Field with name {0} already exists.", ff.FName));
            CheckValidLength(fType, ref fLength, ref fDecimalPoint);
            this.Add(new FieldInfo(fName, fLength, fDecimalPoint, fType));
        }

        public ushort RecordSize
        {
            get
            {
                int res = 1;
                if (this.Count > 0)
                    for (int i = 0; i < this.Count; i++)
                        res += this[i].FLength;
                if (res > ushort.MaxValue)
                    throw new Exception("Record size is too big! Max allowed size is " + ushort.MaxValue.ToString());
                return (ushort)res;
            }
        }

        public FieldInfo this[string fieldName]
        {
            get
            {
                if (this.Count > 0)
                    for (int i = 0; i < this.Count; i++)
                        if (fieldName == this[i].FName)
                            return this[i];
                return null;
            }
        }

        internal void ReIndex()
        {
            ushort res = 1;
            if (this.Count > 0)
                for (int i = 0; i < this.Count; i++)
                {
                    this[i].offset = res;
                    res += this[i].FLength;
                };
        }

        internal void ZeroValues()
        {
            if (this.Count > 0)
                for (int i = 0; i < this.Count; i++)
                    this[i].FValue = null;
        }

        private void CheckValidLength(FieldType fType, ref byte fLength)
        {
            byte fdc = 0;
            CheckValidLength(fType, ref fLength, ref fdc);
        }

        private void CheckValidLength(FieldType fType, ref byte fLength, ref byte fDecimalPoint)
        {
            switch (fType)
            {
                case FieldType.Numeric:
                case FieldType.Float:
                    if (fLength > 20) fLength = 20;
                    if (fDecimalPoint >= fLength) fDecimalPoint = (byte)(fLength - 1);
                    break;
                case FieldType.Character:
                    fDecimalPoint = 0;
                    break;
                case FieldType.Logical:
                    fDecimalPoint = 0;
                    fLength = 1;
                    break;
                case FieldType.Date:
                case FieldType.Memo:
                case FieldType.Binary:
                case FieldType.General:
                case FieldType.Picture:
                    fDecimalPoint = 0;
                    fLength = 10;
                    break;
                case FieldType.Integer:
                    fDecimalPoint = 0;
                    fLength = 4;
                    break;
                case FieldType.DateTime:
                    fDecimalPoint = 0;
                    fLength = 14;
                    break;
            };
        }
    }    

    public class CodePageSet
    {
        public byte headerCode = 0;
        public int codePage = 0;
        public string codeName = "UNKNOWN";

        public Encoding Encoding
        {
            get
            {
                return System.Text.Encoding.GetEncoding(codePage);
            }
        }

        public CodePageSet(){}

        public static CodePageSet Default
        {
            get
            {
                CodePageSet result = new CodePageSet();
                result.headerCode = 201;
                result.codePage = 1251;
                result.codeName = @"Russian Windows \ Windows-1251 [0xC9]";
                return result;
            }
        }

        public override string ToString()
        {
            return codeName;
        }
    }

    public class CodePageList : List<CodePageSet>
    {
        public CodePageList()
        {
            this.Add(204, 01257, "Baltic Windows");
            this.Add(079, 00950, "Chinese Big5 (Taiwan)");
            this.Add(077, 00936, "Chinese GBK (PRC)");
            this.Add(122, 00936, "PRC GBK");
            this.Add(031, 00852, "Czech OEM");
            this.Add(008, 00865, "Danish OEM");
            this.Add(009, 00437, "Dutch OEM");
            this.Add(010, 00850, "Dutch OEM*");
            this.Add(025, 00437, "English OEM (Great Britain)");
            this.Add(026, 00850, "English OEM (Great Britain)*");
            this.Add(027, 00437, "English OEM (US)");
            this.Add(055, 00850, "English OEM (US)*");
            this.Add(200, 01250, "Eastern European Windows");
            this.Add(100, 00852, "Eastern European MS-DOS");
            this.Add(151, 10029, "Eastern European Macintosh");
            this.Add(011, 00437, "Finnish OEM");
            this.Add(013, 00437, "French OEM");
            this.Add(014, 00850, "French OEM*");
            this.Add(029, 00850, "French OEM*2");
            this.Add(028, 00863, "French OEM (Canada)");
            this.Add(108, 00863, "French-Canadian MS-DOS");
            this.Add(015, 00437, "German OEM");
            this.Add(016, 00850, "German OEM*");
            this.Add(203, 01253, "Greek Windows");
            this.Add(106, 00737, "Greek MS-DOS (437G)");
            this.Add(134, 00737, "Greek OEM");
            this.Add(152, 00006, "Greek Macintosh");
            this.Add(121, 00949, "Hangul (Wansung)");
            this.Add(034, 00852, "Hungarian OEM");
            this.Add(103, 00861, "Icelandic MS-DOS");
            this.Add(017, 00437, "Italian OEM");
            this.Add(018, 00850, "Italian OEM*");
            this.Add(019, 00932, "Japanese Shift-JIS");
            this.Add(123, 00932, "Japanese Shift-JIS 2");
            this.Add(104, 00895, "Kamenicky (Czech) MS-DOS");
            this.Add(078, 00949, "Korean (ANSI/OEM)");
            this.Add(105, 00620, "Mazovia (Polish) MS-DOS");
            this.Add(102, 00865, "Nordic MS-DOS");
            this.Add(023, 00865, "Norwegian OEM");
            this.Add(035, 00852, "Polish OEM");
            this.Add(036, 00860, "Portuguese OEM");
            this.Add(037, 00850, "Portuguese OEM*");
            this.Add(064, 00852, "Romanian OEM");
            this.Add(201, 01251, "Russian Windows");
            this.Add(101, 00866, "Russian MS-DOS");
            this.Add(038, 00866, "Russian OEM");
            this.Add(150, 10007, "Russian Macintosh");
            this.Add(135, 00852, "Slovenian OEM");
            this.Add(089, 01252, "Spanish ANSI");
            this.Add(020, 00850, "Spanish OEM*");
            this.Add(021, 00437, "Swedish OEM");
            this.Add(022, 00850, "Swedish OEM*");
            this.Add(024, 00437, "Spanish OEM");
            this.Add(087, 01250, "Standard ANSI");
            this.Add(003, 01252, "Standard Windows ANSI Latin I");
            this.Add(002, 00850, "Standard International MS-DOS");
            this.Add(004, 10000, "Standard Macintosh");
            this.Add(120, 00950, "Taiwan Big 5");
            this.Add(080, 00874, "Thai (ANSI/OEM)");
            this.Add(124, 00874, "Thai Windows/MS–DOS");
            this.Add(202, 01254, "Turkish Windows");
            this.Add(107, 00857, "Turkish MS-DOS");
            this.Add(136, 00857, "Turkish OEM");
            this.Add(001, 00437, "US MS-DOS");
            this.Add(088, 01252, "Western European ANSI");
            this.Add(255, 01251, "Default Unknown");
        }

        private void Add(byte headerCode, int codePage, string codeName)
        {
            CodePageSet cpc = new CodePageSet();
            cpc.headerCode = headerCode;
            cpc.codePage = codePage;
            try
            {
                cpc.codeName = codeName + " ";
                Encoding enc = System.Text.Encoding.GetEncoding(cpc.codePage);
                if ((enc.EncodingName.ToUpper().IndexOf("DOS") >= 0) && (enc.EncodingName.ToUpper().IndexOf("WINDOWS") < 0) && (enc.EncodingName.ToUpper().IndexOf("OEM") < 0))
                    cpc.codeName += @"\ DOS-" + cpc.codePage.ToString() + @" \ " + enc.EncodingName;
                else if ((enc.EncodingName.ToUpper().IndexOf("DOS") < 0) && (enc.EncodingName.ToUpper().IndexOf("WINDOWS") >= 0) && (enc.EncodingName.ToUpper().IndexOf("OEM") < 0))
                    cpc.codeName += @"\ Windows-" + cpc.codePage.ToString() + @" \ " + enc.EncodingName;
                else if ((enc.EncodingName.ToUpper().IndexOf("DOS") < 0) && (enc.EncodingName.ToUpper().IndexOf("WINDOWS") < 0) && (enc.EncodingName.ToUpper().IndexOf("OEM") >= 0))
                    cpc.codeName += @"\ OEM-" + cpc.codePage.ToString() + @" \ " + enc.EncodingName;
                else
                    cpc.codeName += @" \ " + enc.EncodingName;                
            }
            catch 
            {
                cpc.codeName = codeName + @" \ --**--UNKNOWN--**-- ";                
            };
            cpc.codeName += String.Format(@" -- 0x{0:X2}", cpc.headerCode);
            this.Add(cpc);
        }

        public CodePageSet this[byte headerCode]
        {
            get
            {
                if (this.Count == 0) return new CodePageSet();
                foreach (CodePageSet cpc in this)
                    if (cpc.headerCode == headerCode)
                        return cpc;
                return new CodePageSet();
            }
        }

        public CodePageSet this[int codePage]
        {
            get
            {
                if (this.Count == 0) return new CodePageSet();
                foreach (CodePageSet cpc in this)
                    if (cpc.codePage == codePage)
                        return cpc;
                return new CodePageSet();
            }
        }

        public CodePageSet this[string codeName]
        {
            get
            {
                if (this.Count == 0) return new CodePageSet();
                foreach (CodePageSet cpc in this)
                    if (cpc.codeName == codeName)
                        return cpc;
                return new CodePageSet();
            }
        }
    }

    public class MyBitConverter
    {
        public MyBitConverter() {}

        public MyBitConverter(bool IsLittleEndian) { this.isLittleEndian = IsLittleEndian; }

        private bool isLittleEndian = true;

        public bool IsLittleEndian { get { return isLittleEndian; } set { isLittleEndian = value; } } // should default to false, which is what we want for Empire

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
