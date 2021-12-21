/* 
 * C# Class by Milok Zbrozek <milokz@gmail.com>
 * XMLSaved
 */

#define SERVER // comment if dll goes to client
#if SERVER

using System;
using System.Xml;
using System.Xml.Serialization;
using System.Security;
using System.Security.Cryptography;
using System.IO;

/// <summary>
/// Summary description for Class1
/// </summary>
namespace System.Xml
{
    [Serializable]
    public class XMLSaved<T>
    {
        /// <summary>
        ///     Сохранение структуры в файл
        /// </summary>
        /// <param name="file">Полный путь к файлу</param>
        /// <param name="obj">Структура</param>
        public static void Save(string file, T obj)
        {
            System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(T));
            System.IO.StreamWriter writer = System.IO.File.CreateText(file);
            xs.Serialize(writer, obj);
            writer.Flush();
            writer.Close();
        }

        public static string Save(T obj)
        {
            System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(T));
            System.IO.MemoryStream ms = new MemoryStream();
            System.IO.StreamWriter writer = new StreamWriter(ms);
            xs.Serialize(writer, obj);
            writer.Flush();
            ms.Position = 0;
            byte[] bb = new byte[ms.Length];
            ms.Read(bb, 0, bb.Length);
            writer.Close();
            return System.Text.Encoding.UTF8.GetString(bb); ;
        }

        /// <summary>
        ///     Подключение структуры из файла
        /// </summary>
        /// <param name="file">Полный путь к файлу</param>
        /// <returns>Структура</returns>
        public static T Load(string file)
        {
            // if couldn't create file in temp - add credintals
            // http://support.microsoft.com/kb/908158/ru
            System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(T));
            System.IO.StreamReader reader = System.IO.File.OpenText(file);
            T c = (T)xs.Deserialize(reader);
            reader.Close();
            return c;
        }

        public static T LoadText(string text)
        {
            System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(T));
            MemoryStream ms = new MemoryStream();
            byte[] bb = System.Text.Encoding.UTF8.GetBytes(text);
            ms.Write(bb, 0, bb.Length);
            ms.Flush();
            ms.Position = 0;
            System.IO.StreamReader reader = new System.IO.StreamReader(ms);
            T c = (T)xs.Deserialize(reader);
            reader.Close();
            return c;
        }

        /// <summary>
        ///     Подключение структуры из файла
        /// </summary>
        /// <param name="file">Полный путь к файлу</param>
        /// <returns>Структура</returns>
        public static T LoadFile(string file) { return Load(file); }

        /// <summary>
        ///     Подключение структуры из URL
        /// </summary>
        /// <param name="url">Ссылка</param>
        /// <returns>Структура</returns>
        public static T LoadURL(string url)
        {
            System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(T));
            System.Net.WebRequest wr = System.Net.HttpWebRequest.Create(url);
            wr.Method = "GET";
            wr.Timeout = 30000;
            System.Net.WebResponse rp = wr.GetResponse();
            System.IO.Stream ss = rp.GetResponseStream();
            T c = (T)xs.Deserialize(ss);
            ss.Close();
            rp.Close();
            return c;
        }

        /// <summary>
        ///     Получение папки, из которой запущено приложение
        /// </summary>
        /// <returns>Полный путь к папки с \ на конце</returns>
        public static string GetCurrentDir()
        {
            string fname = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase.ToString();
            fname = fname.Replace("file:///", "");
            fname = fname.Replace("/", @"\");
            fname = fname.Substring(0, fname.LastIndexOf(@"\") + 1);
            return fname;
        }

        /// <summary>
        ///     Подключение объекта класса по интерфейсу из DLL
        ///     Конструктор класса должен быть без аргументов
        /// </summary>
        /// <param name="filename">Полный путь к файлу</param>
        /// <returns>Объект класса</returns>
        public static T LoadFromDLL(string filename)
        {
            System.Reflection.Assembly asm = System.Reflection.Assembly.LoadFile(filename);
            Type[] tps = asm.GetTypes();
            Type asmType = null;
            foreach (Type tp in tps) if (tp.GetInterface(typeof(T).ToString()) != null) asmType = tp;

            System.Reflection.ConstructorInfo ci = asmType.GetConstructor(new Type[] { });
            try
            {
                return (T)ci.Invoke(new object[] { });
            }
            catch (Exception ex)
            {
                string rr = GetReference(filename);
                if (rr != "") rr = "\r\nAssembly references: " + rr;
                throw new Exception(" Couldn't load assembly " + System.IO.Path.GetFileName(filename) + " - " + ex.Message + rr);
            };
        }

        public static string GetReference(string filename)
        {
            try
            {
                System.Reflection.Assembly asm = System.Reflection.Assembly.LoadFile(filename);
                Type[] tps = asm.GetTypes();
                Type asmType = null;
                foreach (Type tp in tps)
                    if (tp.Name == "References") asmType = tp;
                System.Reflection.MethodInfo mi = asmType.GetMethod("Reference");
                return (string)mi.Invoke(null, null);
            }
            catch
            {
                return "";
            };
        }

        /// <summary>
        ///     Подключение объекта класса по интерфейсу из DLL по URL
        ///     Конструктор класса должен быть без аргументов
        ///     (Используется для интерфейсов)
        /// </summary>
        /// <param name="url">Ссылка</param>
        /// <returns>Объект класса</returns>
        public static T LoadFromDLL_URL(string url)
        {
            System.Net.WebRequest wr = System.Net.HttpWebRequest.Create(url);
            wr.Method = "GET";
            wr.Timeout = 30000;
            System.Net.WebResponse rp = wr.GetResponse();
            System.IO.Stream ss = rp.GetResponseStream();

            string dd = System.Environment.SpecialFolder.ApplicationData.ToString() + @"\#tmplda\";
            if (!System.IO.Directory.Exists(dd)) System.IO.Directory.CreateDirectory(dd);
            string f = DateTime.UtcNow.Ticks.ToString();
            f = f.Substring(f.Length - 7);
            string ff = dd + f + ".dll";

            System.IO.FileStream fs = new FileStream(ff, FileMode.CreateNew);

            int rb = -1;
            while ((rb = ss.ReadByte()) >= 0) fs.WriteByte((byte)rb);
            ss.Close();
            fs.Close();
            rp.Close();

            System.Reflection.Assembly asm = System.Reflection.Assembly.LoadFile(ff);
            Type[] tps = asm.GetTypes();
            Type asmType = null;
            foreach (Type tp in tps) if (tp.GetInterface(typeof(T).ToString()) != null) asmType = tp;

            System.Reflection.ConstructorInfo ci = asmType.GetConstructor(new Type[] { });
            return (T)ci.Invoke(new object[] { });
        }

        /// <summary>
        ///     Подключение объекта класса по типу через Remoting
        ///     (Ex: tcp://dns_host_name:port/uri )
        /// </summary>
        /// <param name="url"></param>
        /// <returns>Объект класса</returns>
        public static T LoadRemoteDLL_URL(string url)
        {
            return (T)System.Runtime.Remoting.RemotingServices.Connect(typeof(T), url);
        }

        /// <summary>
        ///     Отключение объекта класса от Remoting
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Результат</returns>
        public static bool UnLoadRemoteDLL(object obj)
        {
            return System.Runtime.Remoting.RemotingServices.Disconnect((MarshalByRefObject)obj);
        }

        /// <summary>
        ///     Шифрование текста
        /// </summary>
        /// <param name="textIn">текст</param>
        /// <returns>HASH текста</returns>
        public static string CodeInString(string textIn)
        {
            string txt = textIn.Trim();
            while (txt.Length < 32) txt += " ";

            byte[] code_key = new byte[] { 194, 61, 213, 244, 141, 188, 172, 76, 251, 99, 252, 139, 89, 2, 205, 240, 27, 216, 181, 245, 240, 171, 239, 113, 22, 234, 66, 87, 246, 100, 87, 214 };
            byte[] code_IV = new byte[] { 149, 7, 196, 206, 172, 5, 33, 229, 55, 242, 113, 7, 75, 72, 170, 149 };

            System.Security.Cryptography.RijndaelManaged rm = new RijndaelManaged();
            rm.Padding = PaddingMode.None;
            ICryptoTransform ict = rm.CreateEncryptor(code_key, code_IV);

            System.IO.MemoryStream msEncrypt = new System.IO.MemoryStream();
            CryptoStream csEncrypt = new CryptoStream(msEncrypt, ict, CryptoStreamMode.Write);
            System.IO.StreamWriter swEncrypt = new System.IO.StreamWriter(csEncrypt);
            swEncrypt.Write(txt);
            swEncrypt.Flush();

            byte[] bb = msEncrypt.ToArray();

            swEncrypt.Close();
            csEncrypt.Close();
            msEncrypt.Close();

            string res = "";
            for (int i = 0; i < bb.Length; i++) res += String.Format("{0:X2}", bb[i]);
            return res;
        }

        /// <summary>
        ///     Дешифровка текста
        /// </summary>
        /// <param name="textIn">HASH текста</param>
        /// <returns>текст</returns>
        public static string CodeOutString(string textIn)
        {
            string txt = textIn;
            byte[] bytes_in = new byte[(int)(txt.Length / 2)];
            for (int i = 0; i < bytes_in.Length; i++)
            {
                string ct = txt.Substring(i * 2, 2);
                bytes_in[i] = (byte)int.Parse(ct, System.Globalization.NumberStyles.HexNumber);
            };

            byte[] code_key = new byte[] { 194, 61, 213, 244, 141, 188, 172, 76, 251, 99, 252, 139, 89, 2, 205, 240, 27, 216, 181, 245, 240, 171, 239, 113, 22, 234, 66, 87, 246, 100, 87, 214 };
            byte[] code_IV = new byte[] { 149, 7, 196, 206, 172, 5, 33, 229, 55, 242, 113, 7, 75, 72, 170, 149 };

            System.Security.Cryptography.RijndaelManaged rm = new RijndaelManaged();
            rm.Padding = PaddingMode.None;
            ICryptoTransform ict = rm.CreateDecryptor(code_key, code_IV);

            System.IO.MemoryStream msEncrypt = new System.IO.MemoryStream(bytes_in);
            CryptoStream csEncrypt = new CryptoStream(msEncrypt, ict, CryptoStreamMode.Read);
            System.IO.StreamReader swEncrypt = new System.IO.StreamReader(csEncrypt);
            string res = swEncrypt.ReadToEnd();

            swEncrypt.Close();
            csEncrypt.Close();
            msEncrypt.Close();

            return res.Trim();
        }

        /// <summary>
        ///     Добавляем ошибку в системный лог
        /// </summary>
        /// <param name="msg"></param>
        public static void AddErr2SysLog(string msg)
        {
            try
            {
                string sSource = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                if (!System.Diagnostics.EventLog.SourceExists(sSource))
                    System.Diagnostics.EventLog.CreateEventSource(sSource, "Application");
                System.Diagnostics.EventLog.WriteEntry(sSource, msg, System.Diagnostics.EventLogEntryType.Error);
            }
            catch { };
        }

        /// <summary>
        ///     Добавляем в системный лог
        /// </summary>
        /// <param name="msg"></param>
        public static void Add2SysLog(string msg)
        {
            try
            {
                string sSource = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                if (!System.Diagnostics.EventLog.SourceExists(sSource))
                    System.Diagnostics.EventLog.CreateEventSource(sSource, "Application");
                System.Diagnostics.EventLog.WriteEntry(sSource, msg, System.Diagnostics.EventLogEntryType.Information);
            }
            catch { };
        }
    }
}

#endif