using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace RoutesKeyGen
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            Gen();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Gen();
        }

        private void Gen()
        {
            txtBox.Clear();
            CRC32 crc32 = new CRC32();
            Random rnd = new Random();

            for (int x = 0; x < 5; x++)
            {
                byte[] ba = new byte[8];
                for (int i = 0; i < ba.Length; i++) ba[i] = (byte)rnd.Next(0, 255);
                string key = Base58.GetString(ba).ToUpper();

                ba = crc32.CRC32Arr(ba, true);
                key += BitConverter.ToString(ba).Replace("-", "");
                if (char.IsDigit(key[0])) key = "R" + key.Substring(1);
                txtBox.Text +=
                    String.Format("<!--  {0}  -->\r\n<license key=\"{0}\" expires=\"{1:yyyy-MM-ddTHH:mm:ss}\"/>\r\n", key, DateTime.Now.AddYears(1)) +
                    String.Format("<license key=\"{0}\" expires=\"{1:yyyy-MM-ddTHH:mm:ss}\"  ip=\"(^192.168.\\d*.\\d*$)|(^10.0.\\d*.\\d*$)|(^127.0.0.1*$)\"/>\r\n\r\n", key, DateTime.Now.AddYears(1));
            };
        }
    }

    public class Base58
    {
        //
        //  Max 8 bytes (CRC64 or 2xCRC32)
        //  https://ru.wikipedia.org/wiki/Base58
        //  Check: https://incoherency.co.uk/base58/
        //  Check: https://www.browserling.com/tools/base58-encode 
        //
        public static string GetString(byte[] data)
        {
            string digits = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

            ulong numberValue = 0;
            for (int i = 0; i < data.Length; i++)
                numberValue = numberValue * 256 + data[i];

            string result = "";
            while (numberValue > 0)
            {
                int remainder = (int)(numberValue % 58);
                numberValue /= 58;
                result = digits[remainder] + result;
            };

            // Append `1` for each leading 0 byte
            for (int i = 0; i < data.Length && data[i] == 0; i++)
                result = '1' + result;

            return result;
        }

        //
        //  Max 8 bytes (CRC64 or 2xCRC32)
        //  https://ru.wikipedia.org/wiki/Base58
        //  Check: https://incoherency.co.uk/base58/
        //  Check: https://www.browserling.com/tools/base58-encode
        //
        public static string GetString(ulong numberValue)
        {
            string digits = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

            ulong original = numberValue;
            string result = "";
            while (numberValue > 0)
            {
                int remainder = (int)(numberValue % 58);
                numberValue /= 58;
                result = digits[remainder] + result;
            };

            // Append `1` for each leading 0 byte
            for (int i = 0; i < 8 && (((byte)(original >> 56 - 8 * i) & 0xFF) == 0); i++)
                result = '1' + result;

            return result;
        }
    }

    public class CRC32
    {
        private const uint poly = 0xEDB88320;
        private uint[] checksumTable;

        public CRC32()
        {
            checksumTable = new uint[256];
            for (uint index = 0; index < 256; index++)
            {
                uint el = index;
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((el & 1) != 0)
                        el = (poly ^ (el >> 1));
                    else
                        el = (el >> 1);
                };
                checksumTable[index] = el;
            };
        }

        public uint CRC32Num(byte[] data)
        {
            uint res = 0xFFFFFFFF;
            for (int i = 0; i < data.Length; i++)
                res = checksumTable[(res & 0xFF) ^ (byte)data[i]] ^ (res >> 8);
            return ~res;
        }

        public byte[] CRC32Arr(byte[] data, bool isLittleEndian)
        {
            uint res = CRC32Num(data);
            byte[] hash = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                if (isLittleEndian)
                    hash[i] = (byte)((res >> (24 - i * 8)) & 0xFF);
                else
                    hash[i] = (byte)((res >> (i * 8)) & 0xFF);
            };
            return hash;
        }

        public ulong CRC32mod2Num(byte[] data)
        {
            uint res1 = 0xFFFFFFFF;
            uint res2 = 0xFFFFFFFF;

            for (int i = 0; i < data.Length; i++)
            {
                if (i % 2 == 0)
                    res1 = checksumTable[(res1 & 0xFF) ^ (byte)data[i]] ^ (res1 >> 8);
                else
                    res2 = checksumTable[(res2 & 0xFF) ^ (byte)data[i]] ^ (res2 >> 8);
            };

            res1 = ~res1;
            res2 = ~res2;

            ulong res = 0;
            for (int i = 0; i < 4; i++)
            {
                ulong u1 = ((res1 >> (24 - i * 8)) & 0xFF);
                ulong u2 = ((res2 >> (24 - i * 8)) & 0xFF);
                res += u1 << (56 - i * 16);
                res += u2 << (56 - i * 16 - 8);
            };

            return res;
        }

        public byte[] CRC32mod2Arr(byte[] data, bool isLittleEndian)
        {
            uint res1 = 0xFFFFFFFF;
            uint res2 = 0xFFFFFFFF;

            for (int i = 0; i < data.Length; i++)
            {
                if (i % 2 == 0)
                    res1 = checksumTable[(res1 & 0xFF) ^ (byte)data[i]] ^ (res1 >> 8);
                else
                    res2 = checksumTable[(res2 & 0xFF) ^ (byte)data[i]] ^ (res2 >> 8);
            };

            res1 = ~res1;
            res2 = ~res2;

            byte[] hash = new byte[8];
            for (int i = 0; i < 4; i++)
            {
                if (isLittleEndian)
                {
                    hash[i * 2] = (byte)((res1 >> (24 - i * 8)) & 0xFF);
                    hash[i * 2 + 1] = (byte)((res2 >> (24 - i * 8)) & 0xFF);
                }
                else
                {
                    hash[7 - i * 2] = (byte)((res1 >> (24 - i * 8)) & 0xFF);
                    hash[7 - i * 2 - 1] = (byte)((res2 >> (24 - i * 8)) & 0xFF);
                };
            };
            return hash;
        }
    }
}