using System;
using System.IO;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Net;
using System.Net.Sockets;
using System.Web;
using System.Web.Services;
using System.Web.Services.Description;
using System.Xml.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;
using System.Security.Cryptography;

using Newtonsoft.Json;

namespace dkxce.Route.ServiceSolver
{
    /// <summary>
    ///     Http Web Client Request
    /// </summary>
    public struct ClientRequest
    {
        public TcpClient client;
        public IDictionary<string, string> headers;
        public IDictionary<string, string> cookies;
        public string request;
        public string method;
        public string methodSOAP;
        public string query;
        private Dictionary<string, string> _Query;
        public string body;
        public string _guid;
        public string _cuid;
        public string apisid { get { return (string.IsNullOrEmpty(_cuid) ? _guid : _cuid); } }
        // 1 - GET, 2 - POST, 3 - PUSH, 4 - PUT
        public byte httpMethod;
        // 0 - unknown type, 1 - JSON, 2 - XML Object, 3 - XML-RPC, 4 - SOAP, 5 - SOAP 1.2, 6 - HTTP GET
        public byte clientType;
        //public object session_vars;
        public string authUser;
        public string authPass;
        public string xFwd4;
        public System.Text.Encoding Encoding;

        public ClientRequest(TcpClient client, IDictionary<string, string> headers, IDictionary<string, string> cookies, string request, string method, string query, string body)
        {
            this.httpMethod = 0; // GET
            if (!string.IsNullOrEmpty(request))
            {
                if (request.StartsWith("GET")) this.httpMethod = 1;
                if (request.StartsWith("POST")) this.httpMethod = 2;
                if (request.StartsWith("PUSH")) this.httpMethod = 3;
                if (request.StartsWith("PUT")) this.httpMethod = 4;
            };
            this.clientType = 0; // unknown type
            this.client = client;
            this.headers = headers;
            this.cookies = cookies;
            this.request = request;
            this.method = method;
            if (!string.IsNullOrEmpty(this.method))
                if (this.method.EndsWith("/")) this.method = this.method.Remove(this.method.Length - 1);
            this.methodSOAP = this.method;
            this.query = query;
            this._Query = null;
            this.body = body;
            if (!string.IsNullOrEmpty(this.body))
            {
                this.body = this.body.Trim();
                if ((this.body.StartsWith("{") && this.body.EndsWith("}"))) this.clientType = 1;
                //if ((this.body.StartsWith("[") && this.body.EndsWith("]"))) this.clientType = 1;
                if ((this.body.StartsWith("<?") && this.body.EndsWith(">"))) this.clientType = 2;
                if ((this.body.StartsWith("<?") && this.body.EndsWith("methodCall>"))) this.clientType = 3;
                if ((this.body.StartsWith("<?") && this.body.EndsWith("soap:Envelope>"))) this.clientType = 4;
                if ((this.body.StartsWith("<?") && this.body.EndsWith("soap12:Envelope>"))) this.clientType = 5;
            };
            this._guid = System.Guid.NewGuid().ToString().Replace("-", "");
            this._cuid = "";
            if ((this.cookies != null) && (this.cookies.Keys.Contains("apisid")))
                this._guid = this._cuid = this.cookies["apisid"];
            authUser = "";
            authPass = "";
            xFwd4 = "";
            Encoding = System.Text.Encoding.UTF8;
            if ((this.headers != null) && (this.headers.Count > 0) && (this.headers.Keys.Contains("Response-Encoding")))
            {
                if (this.headers["Response-Encoding"] == "windows-1251")
                    this.Encoding = System.Text.Encoding.GetEncoding(1251);
                else
                {
                    int enc = 0;
                    if (int.TryParse(this.headers["Response-Encoding"], out enc))
                    {
                        try
                        {
                            this.Encoding = System.Text.Encoding.GetEncoding(enc);
                        }
                        catch { this.Encoding = System.Text.Encoding.UTF8; };
                    }
                    else
                    {
                        try
                        {

                            this.Encoding = System.Text.Encoding.GetEncoding(this.headers["Response-Encoding"]);
                        }
                        catch { this.Encoding = System.Text.Encoding.UTF8; };
                    };
                    if (this.Encoding == null)
                        this.Encoding = System.Text.Encoding.UTF8;
                }
            };
            //session_vars = null;
        }

        public bool IsUnknown { get { return clientType == 0; } }
        public bool IsJSON { get { return clientType == 1; } }
        public bool IsXML { get { return clientType == 2; } }
        public bool IsXMLRPC { get { return clientType == 3; } }
        public bool IsSOAP { get { return (clientType == 4) || clientType == 5; } }
        public bool IsSOAP11 { get { return clientType == 4; } }
        public bool IsSOAP12 { get { return clientType == 5; } }


        public bool IsHTTPGet { get { return httpMethod == 1; } }
        public bool IsHTTPPost { get { return httpMethod == 2; } }
        public bool IsHTTPPush { get { return httpMethod == 3; } }
        public bool IsHTTPPut { get { return httpMethod == 4; } }

        public string UserAgent
        {
            get
            {
                if (!headers.Keys.Contains("User-Agent")) return "";
                return headers["User-Agent"];
            }
        }

        public string Referer
        {
            get
            {
                if (!headers.Keys.Contains("Referer")) return "";
                return headers["Referer"];
            }
        }

        public string RefererHost
        {
            get
            {
                if (!headers.Keys.Contains("Referer")) return "";
                try
                {
                    return (new Uri(headers["Referer"])).Host;
                }
                catch { };
                return "";
            }
        }

        public string ClientIP
        {
            get
            {
                //if (!string.IsNullOrEmpty(xFwd4)) return xFwd4;
                return ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            }
        }

        public string GetClientIP(bool proxied)
        {
            if (proxied && (!string.IsNullOrEmpty(xFwd4))) return xFwd4;
            return ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
        }

        public Dictionary<string, string> Query
        {
            get
            {
                if (_Query != null) return _Query;

                _Query = new Dictionary<string, string>();
                if (string.IsNullOrEmpty(query)) return _Query;
                string[] pars = query.Split(new char[] { '&' });
                if (pars.Length == 0) return _Query;
                foreach (string par in pars)
                {
                    string[] pv = par.Split(new char[] { '=' }, 2);
                    string p = pv[0];
                    string v = "";
                    if (p == null) p = ""; else p = HttpUtility.UrlDecode(p);
                    if (pv.Length > 1) v = pv[1];
                    if (v == null) v = ""; else v = HttpUtility.UrlDecode(v);
                    if (_Query.ContainsKey(p))
                        _Query[p] = _Query[p].ToString() + "," + v;
                    else
                        _Query.Add(p, v);
                };
                return _Query;
            }
        }
    }


    /// <summary>
    ///     Storing Server Methods
    /// </summary>
    public struct MethodInfo
    {
        /// <summary>
        ///     
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="body"></param>
        /// <param name="query"></param>
        /// <param name="request"></param>
        /// <returns>true if ok, false if 404</returns>
        public delegate bool CallMethod(ClientRequest clientRequest);

        public string name;
        public string version;
        public CallMethod implementation;

        public MethodInfo(string name, string version, CallMethod implementation)
        {
            this.name = name;
            this.version = version;
            this.implementation = implementation;
        }

        public bool IsAdminMethod { get { return this.name.IndexOf("admin") == 0; } }
    }


    /// <summary>
    ///     Main Client Object Holder
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class CrossSendObject<T>
    {
        /// <summary>
        ///    input text data
        /// </summary>
        public string data = null;

        /// <summary>
        ///     Parsed Object
        /// </summary>
        public T obj = default(T);

        /// <summary>
        ///     Error text
        /// </summary>
        public string error = null;

        /// <summary>
        ///     Error Code
        /// </summary>
        public int errorCode = 0;

        /// <summary>
        ///     Parsed ok?
        /// </summary>
        public bool IsValid
        {
            get
            {
                return string.IsNullOrEmpty(this.error);
            }
        }

        /// <summary>
        ///     Error response in specified format
        /// </summary>
        public string errorResponse
        {
            get
            {
                return error2ResponseObject();
            }
        }

        /// <summary>
        ///     Parsed ok text
        /// </summary>
        public string ValidationText
        {
            get
            {
                string obj_type = typeof(T).ToString();
                if (obj_type.IndexOf("+") > 0) obj_type = obj_type.Substring(obj_type.IndexOf("+") + 1);
                if (obj_type.IndexOf(".") > 0) obj_type = obj_type.Substring(obj_type.IndexOf(".") + 1);
                return "Input data is " + (this.IsValid ? "" : "not ") + "valid for object '" + obj_type + "'";
            }
        }

        /// <summary>
        ///     Return text in client specified format (JSON/XML-RPC/XML)
        /// </summary>
        /// <returns></returns>
        public virtual string error2ResponseObject()
        {
            return String.Format("{0}: {1}", errorCode, error);
        }

        public static T fromQuery(IDictionary<string, string> Query)
        {
            if (Query == null) return default(T);
            if (Query.Count == 0) return default(T);

            Type t = typeof(T);
            System.Reflection.ConstructorInfo ci = t.GetConstructor(new Type[0]);
            T obj = (T)ci.Invoke(null);
            foreach (KeyValuePair<string, string> kv in Query)
            {
                System.Reflection.FieldInfo fi = t.GetField(kv.Key);
                if (fi != null)
                {
                    if (fi.FieldType.Name == "String") fi.SetValue(obj, kv.Value.Replace("+", " "));
                    if (fi.FieldType.Name == "String[]") fi.SetValue(obj, kv.Value.Split(new char[] { ',' }, StringSplitOptions.None));
                    if (fi.FieldType.Name == "Single[]")
                    {
                        string[] rt = kv.Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        Single[] rs = new float[rt.Length];
                        for (int i = 0; i < rs.Length; i++) rs[i] = float.Parse(rt[i], System.Globalization.CultureInfo.InvariantCulture);
                        fi.SetValue(obj, rs);
                    };
                    if (fi.FieldType.Name == "Double[]")
                    {
                        string[] rt = kv.Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        Double[] rs = new double[rt.Length];
                        for (int i = 0; i < rs.Length; i++) rs[i] = double.Parse(rt[i], System.Globalization.CultureInfo.InvariantCulture);
                        fi.SetValue(obj, rs);
                    };
                    if (fi.FieldType.Name == "Byte") fi.SetValue(obj, Byte.Parse(kv.Value));
                    if (fi.FieldType.Name == "Int16") fi.SetValue(obj, Int16.Parse(kv.Value));
                    if (fi.FieldType.Name == "Int32") fi.SetValue(obj, Int32.Parse(kv.Value));
                    if (fi.FieldType.Name == "Int64") fi.SetValue(obj, Int64.Parse(kv.Value));
                    if (fi.FieldType.Name == "Single") fi.SetValue(obj, Single.Parse(kv.Value));
                    if (fi.FieldType.Name == "Double") fi.SetValue(obj, Double.Parse(kv.Value));
                };
            };
            return obj;
        }
    }

    /// <summary>
    ///     JSON Parser (Deserializer)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class JSONObject<T> : CrossSendObject<T>
    {
        /// <summary>
        ///     Serialize object to JSON
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string Save(T obj)
        {
#if ONLYTILES
            return "";
#else
            return JsonConvert.SerializeObject(obj);
#endif
        }

        /// <summary>
        ///     Parse (Deserialize) text
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static JSONObject<T> Parse(string data)
        {
            JSONObject<T> res = new JSONObject<T>();
            res.data = data;
            if (string.IsNullOrEmpty(data))
            {
                res.error = "Input data cannot be empty";
                res.errorCode = 1001; // JSON INPUT DATA ERROR
                return res;
            };

            try
            {
#if ONLYTILES
                res.obj = default(T);
#else
                res.obj = JsonConvert.DeserializeObject<T>(res.data);
#endif
            }
            catch (Exception ex)
            {
                res.error = ex.Message + (ex.InnerException == null ? "" : " " + ex.InnerException.Message);
                res.errorCode = 1002; // JSON PARSE DATA ERROR
            };
            return res;
        }

        /// <summary>
        ///     Error text in JSON object
        /// </summary>
        public override string error2ResponseObject()
        {
            if (IsValid)
                return null;
            else
                return "{Error:'" + this.error + "', ErrCode:" + this.errorCode.ToString() + "}";
        }
    }

    /// <summary>
    ///     XML-RPC -> JSON
    ///     Normal Object -> XML-RPC Params
    /// </summary>
    public class XMLRPCObject<T> : CrossSendObject<T>
    {
        /// <summary>
        ///     Serialize object to XML-RPC
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string Save(T obj)
        {
            Nwc.XmlRpc.XmlRpcResponse resp = new Nwc.XmlRpc.XmlRpcResponse();
            resp.Value = XMLRPCObject<T>.ObjToList(obj);
            Nwc.XmlRpc.XmlRpcResponseSerializer rs = new Nwc.XmlRpc.XmlRpcResponseSerializer();
            return rs.Serialize(resp);
        }

        /// <summary>
        ///     JSON output data
        /// </summary>
        public string jsondata = null;

        /// <summary>
        ///     Method Name
        /// </summary>
        public string method = null;

        public static XMLRPCObject<T> ParseMethod(string data)
        {
            XMLRPCObject<T> res = new XMLRPCObject<T>();
            res.data = data;
            if (string.IsNullOrEmpty(data))
            {
                res.error = "Input data cannot be empty";
                res.errorCode = 3001; // XMLRPC
                return res;
            };

            try
            {
                Nwc.XmlRpc.XmlRpcRequestDeserializer dds = new Nwc.XmlRpc.XmlRpcRequestDeserializer();
                Nwc.XmlRpc.XmlRpcRequest req = (Nwc.XmlRpc.XmlRpcRequest)dds.Deserialize(data);
                string json = "";
                if (req.Params.Count > 0)
                {
                    IDictionary id = (IDictionary)req.Params[0];
                    foreach (DictionaryEntry de in id)
                        json += (json.Length > 0 ? ", " : "") + "'" + de.Key.ToString() + "':" + ValueToJSON(de.Value);
                };
                res.jsondata = "{" + json + "}";
                res.method = req.MethodName.ToLower();
                if (string.IsNullOrEmpty(res.error) && string.IsNullOrEmpty(res.method))
                {
                    res.error = "Method could not be empty";
                    res.errorCode = 3003; // XMLRPC
                };
                if (string.IsNullOrEmpty(res.error) && string.IsNullOrEmpty(res.jsondata))
                {
                    res.error = "XML Parcing error";
                    res.errorCode = 3004;
                };
            }
            catch (Exception ex)
            {
                res.error = ex.Message + (ex.InnerException == null ? "" : " " + ex.InnerException.Message);
                res.errorCode = 3002;
            };

            return res;
        }

        public static XMLRPCObject<T> Parse(string data)
        {
            XMLRPCObject<T> res = ParseMethod(data);
            if (res.IsValid)
            {
                JSONObject<T> jo = JSONObject<T>.Parse(res.jsondata);
                if (!jo.IsValid)
                {
                    res.error = jo.error;
                    res.errorCode = 3006; // XMLRPC JSON
                }
                else
                    res.obj = jo.obj;
            };
            return res;
        }

        private static string ValueToJSON(object value)
        {
            Type tp = value.GetType();
            switch (tp.Name)
            {
                case "String": return "'" + value.ToString() + "'";
                case "Byte": return value.ToString();
                case "SByte": return value.ToString();
                case "Int16": return value.ToString();
                case "Int32": return value.ToString();
                case "UInt16": return value.ToString();
                case "UInt32": return value.ToString();
                case "Int64": return value.ToString();
                case "UInt64": return value.ToString();
                case "Boolean": return ((bool)value) ? "true" : "false";
                case "Double": return ((double)value).ToString(System.Globalization.CultureInfo.InvariantCulture);
                case "Single": return ((double)value).ToString(System.Globalization.CultureInfo.InvariantCulture);
                case "DateTime": return "'" + ((DateTime)value).ToString("yyyy-MM-ddTHH:mm:ss") + "'";
                case "ArrayList":
                    System.Collections.ArrayList val = (System.Collections.ArrayList)value;
                    if (val.Count == 0) return "[]";
                    string arrres = "[";
                    for (int i = 0; i < val.Count; i++)
                    {
                        if (i > 0) arrres += ", ";
                        arrres += ValueToJSON(val[i]);
                    }
                    return arrres + "]";
                case "Hashtable":
                    Hashtable ht = (Hashtable)value;
                    if (ht.Count == 0) return "{}";
                    string objres = "{";
                    foreach (DictionaryEntry de in ht)
                    {
                        if (objres.Length > 2) objres += ", ";
                        objres += "'" + de.Key + "':" + ValueToJSON(de.Value);
                    };
                    return objres + "}";
            };
            return "null";
        }

        /// <summary>
        ///     Create Data Object for XMLRPCSerializer
        /// </summary>
        /// <param name="value">normal object</param>
        /// <returns>object as params</returns>
        private static IDictionary ObjToList(object value)
        {
            IDictionary dict = (IDictionary)new Hashtable();
            Type t = value.GetType();
            System.Reflection.MemberInfo[] m = t.GetMembers();
            foreach (System.Reflection.MemberInfo mi in m)
                if (mi.MemberType == System.Reflection.MemberTypes.Field)
                {
                    System.Reflection.FieldInfo fi = t.GetField(mi.Name);
                    object oo = fi.GetValue(value);
                    if (oo != null)
                    {
                        Type tp = oo.GetType();
                        if (!tp.FullName.StartsWith("System"))
                        {
                            try
                            {
                                if (tp.Name.EndsWith("[]"))
                                {
                                    ArrayList al = new ArrayList();
                                    al.AddRange((IList)oo);
                                    if (al.Count > 0)
                                        for (int i = 0; i < al.Count; i++)
                                        {
                                            Type aat = al[i].GetType();
                                            if (!aat.FullName.StartsWith("System"))
                                            {
                                                try
                                                {
                                                    al[i] = ObjToList(al[i]);
                                                }
                                                catch { al[i] = null; };
                                            }
                                            else if (al[i] is string)
                                                al[i] = System.Security.SecurityElement.Escape(al[i].ToString());
                                        };
                                    dict.Add(mi.Name, al);
                                }
                                else
                                {
                                    IDictionary rrr = ObjToList(oo);
                                    dict.Add(mi.Name, rrr);
                                };
                            }
                            catch { oo = null; };
                        }
                        else if (oo is Array)
                        {
                            ArrayList al = new ArrayList();
                            al.AddRange((IList)oo);
                            if (al.Count > 0)
                                for (int i = 0; i < al.Count; i++)
                                {
                                    Type aat = al[i].GetType();
                                    if (!aat.FullName.StartsWith("System"))
                                    {
                                        try
                                        {
                                            al[i] = ObjToList(al[i]);
                                        }
                                        catch { al[i] = null; };
                                    }
                                    else if (al[i] is string)
                                        al[i] = System.Security.SecurityElement.Escape(al[i].ToString());
                                };
                            dict.Add(mi.Name, al);
                        }
                        else if (oo is string)
                            dict.Add(mi.Name, System.Security.SecurityElement.Escape(oo.ToString()));
                        else if (oo is uint)
                            dict.Add(mi.Name, int.Parse(oo.ToString()));
                        else if (oo is ulong)
                            dict.Add(mi.Name, int.Parse(oo.ToString()));
                        else if (oo is long)
                            dict.Add(mi.Name, int.Parse(oo.ToString()));
                        else if (oo is short)
                            dict.Add(mi.Name, int.Parse(oo.ToString()));
                        else if (oo is ushort)
                            dict.Add(mi.Name, int.Parse(oo.ToString()));
                        else
                            dict.Add(mi.Name, oo);
                    };
                };
            return dict;
        }

        /// <summary>
        ///     Error text in JSON object
        /// </summary>
        public override string error2ResponseObject()
        {
            if (IsValid)
                return null;
            else
            {
                Nwc.XmlRpc.XmlRpcResponse resp = new Nwc.XmlRpc.XmlRpcResponse();
                if (data is string)
                    resp.SetFault(errorCode, System.Security.SecurityElement.Escape(this.error));
                else
                    resp.Value = XMLRPCObject<T>.ObjToList(data);
                Nwc.XmlRpc.XmlRpcResponseSerializer rs = new Nwc.XmlRpc.XmlRpcResponseSerializer();
                return rs.Serialize(resp);
            }
        }
    }

    /// <summary>
    ///     Simple XML Object
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class XMLObject<T> : CrossSendObject<T>
    {
        /// <summary>
        ///     Serialize object to XML
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string Save(T obj)
        {
            System.Xml.Serialization.XmlSerializerNamespaces xns = new XmlSerializerNamespaces();
            xns.Add(String.Empty, String.Empty);
            System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(T));
            System.IO.MemoryStream ms = new MemoryStream();
            System.IO.StreamWriter writer = new StreamWriter(ms);
            xs.Serialize(writer, obj, xns);
            writer.Flush();
            ms.Position = 0;
            byte[] bb = new byte[ms.Length];
            ms.Read(bb, 0, bb.Length);
            writer.Close();
            string dta = System.Text.Encoding.UTF8.GetString(bb);
            return dta;
        }

        /// <summary>
        ///     Parse (Deserialize) text
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static XMLObject<T> Parse(string data)
        {
            XMLObject<T> res = new XMLObject<T>();
            res.data = data;
            if (string.IsNullOrEmpty(data))
            {
                res.error = "Input data cannot be empty";
                res.errorCode = 2001; // XML
                return res;
            };

            try
            {
                res.obj = XMLSaved<T>.LoadText(data);
            }
            catch (Exception ex)
            {
                res.error = ex.Message + (ex.InnerException == null ? "" : " " + ex.InnerException.Message);
                res.errorCode = 2002; // XML
            };
            return res;
        }

        /// <summary>
        ///     Error text in XML object
        /// </summary>
        public override string error2ResponseObject()
        {
            if (IsValid)
                return null;
            else
            {
                string rootEl = (typeof(T)).Name;
                object[] maina = (typeof(T)).GetCustomAttributes(false);
                if (maina != null)
                    foreach (object ma in maina)
                        if (ma is XmlRootAttribute)
                        {
                            XmlRootAttribute xra = (XmlRootAttribute)ma;
                            rootEl = xra.ElementName;
                        };
                return "<?xml version=\"1.0\" encoding=\"utf-8\"?><" + rootEl + "><Error>" + System.Security.SecurityElement.Escape(this.error) + "</Error><ErrCode>" + errorCode.ToString() + "</ErrCode></" + rootEl + ">";
            };
        }
    }

    /// <summary>
    ///     Simple XML Object
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class XMLSOAP<T> : CrossSendObject<T>
    {
        /// <summary>
        ///     SOAP output data
        /// </summary>
        public string soapdata = null;

        /// <summary>
        ///     Method Name
        /// </summary>
        public string method = null;
        public string methodSOAP = null;

        private const string bodyText = "soap:Body";
        private const string bodyText2 = "soap12:Body";

        /// <summary>
        ///     Serialize object to XML SOAP 1.1
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string Save11(T obj, string methodSOAP)
        {
            string prefix = "<?xml version=\"1.0\" encoding=\"utf-8\"?><soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body>" +
                "<" + methodSOAP + "Response>";
            string objdata = Save(obj);
            string postfix = "</" + methodSOAP + "Response></soap:Body></soap:Envelope>";
            return prefix + objdata + postfix;
        }

        /// <summary>
        ///     Serialize object to XML SOAP 1.2
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string Save12(T obj, string methodSOAP)
        {
            string prefix = "<?xml version=\"1.0\" encoding=\"utf-8\"?><soap12:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:soap12=\"http://www.w3.org/2003/05/soap-envelope\"><soap12:Body>" +
                "<" + methodSOAP + "Response>";
            string objdata = Save(obj);
            string postfix = "</" + methodSOAP + "Response></soap12:Body></soap12:Envelope>";
            return prefix + objdata + postfix;
        }

        /// <summary>
        ///     Serialize object to XML
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private static string Save(T obj)
        {
            System.Xml.Serialization.XmlSerializerNamespaces xns = new XmlSerializerNamespaces();
            xns.Add(String.Empty, String.Empty);
            System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(T));
            System.IO.MemoryStream ms = new MemoryStream();
            System.Xml.XmlWriterSettings xws = new XmlWriterSettings();
            xws.OmitXmlDeclaration = true;
            System.Xml.XmlWriter xw = System.Xml.XmlWriter.Create(ms, xws);
            xs.Serialize(xw, obj, xns);
            xw.Flush();
            ms.Position = 0;
            byte[] bb = new byte[ms.Length];
            ms.Read(bb, 0, bb.Length);
            xw.Close();
            string ret = System.Text.Encoding.UTF8.GetString(bb);
            return ret;
        }

        public static XMLSOAP<T> ParseMethod(string data)
        {
            XMLSOAP<T> res = new XMLSOAP<T>();
            res.data = data;
            if (string.IsNullOrEmpty(data))
            {
                res.error = "Input data cannot be empty";
                res.errorCode = 4001; // SOAP
                return res;
            };

            try
            {
                int bf = 0;
                int be = 0;
                if (data.IndexOf("<" + bodyText2 + ">") > 0)
                {
                    bf = data.IndexOf("<" + bodyText2 + ">") + bodyText.Length + 2;
                    be = data.IndexOf("</" + bodyText2 + ">", bf);
                }
                else
                {
                    bf = data.IndexOf("<" + bodyText + ">") + bodyText.Length + 2;
                    be = data.IndexOf("</" + bodyText + ">", bf);
                };
                string body = data.Substring(bf, be - bf);
                XmlDocument xd = new XmlDocument();
                xd.LoadXml(body);
                res.methodSOAP = xd.DocumentElement.Name;
                res.method = res.methodSOAP.ToLower();
                res.soapdata = xd.DocumentElement.InnerXml;
                if (string.IsNullOrEmpty(res.error) && string.IsNullOrEmpty(res.method))
                {
                    res.error = "Method could not be empty";
                    res.errorCode = 4003; // XMLRPC
                };
                if (string.IsNullOrEmpty(res.error) && string.IsNullOrEmpty(res.soapdata))
                {
                    res.error = "XML Parcing error";
                    res.errorCode = 4004;
                };
            }
            catch (Exception ex)
            {
                res.error = ex.Message + (ex.InnerException == null ? "" : " " + ex.InnerException.Message);
                res.errorCode = 4002;
            };

            return res;
        }

        public static XMLSOAP<T> Parse(string data)
        {
            XMLSOAP<T> res = ParseMethod(data);
            if (res.IsValid)
            {
                XMLObject<T> xo = XMLObject<T>.Parse(res.soapdata);
                if (!xo.IsValid)
                {
                    res.error = xo.error;
                    res.errorCode = 4006; // XML SOAP
                }
                else
                    res.obj = xo.obj;
            };
            return res;
        }

        /// <summary>
        ///     Error text in XML object
        /// </summary>
        public override string error2ResponseObject()
        {
            if (IsValid)
                return null;
            else
                return "<?xml version=\"1.0\" encoding=\"utf-8\"?><soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body>" +
                "<soap:Fault><faultcode>" + errorCode.ToString() + "</faultcode><faultstring>" + System.Security.SecurityElement.Escape(error) + "</faultstring></soap:Fault></soap:Body></soap:Envelope>";
        }
    }
}
