using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;
using System.Web;

using NavicomMapServices.Routes;
using Newtonsoft.Json;

namespace TEST_MAP
{
    public partial class TestForm : Form
    {
        public ScriptManager sm;

        public TestForm()
        {
            InitializeComponent();
            comboBox2.SelectedIndex = 2;
            webBrowser1.ObjectForScripting = sm = new ScriptManager(this); 
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string ff = "file:///" + XMLSaved<int>.GetCurrentDir().Replace(@"\", "/") + "map.html";
            webBrowser1.Navigate(ff);
        }

        
        public void ConsoleWrite(string txt)
        {
            textBox1.Text += txt;
            Application.DoEvents();
        }

        public void ConsoleWriteLine(string txt)
        {
            textBox1.Text += txt + "\r\n";
            Application.DoEvents();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.DefaultExt = ".gpx";
            ofd.Filter = "Garmin GPX files (*.gpx)|*.gpx|All Types (*.*)|*.*";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                System.Xml.XmlDocument xd = new XmlDocument();
                xd.Load(ofd.FileName);
                foreach (XmlNode xn in xd.DocumentElement.ChildNodes)
                    if (xn.Name == "wpt")
                    {
                        if(comboBox2.SelectedIndex == 0)
                            webBrowser1.Document.InvokeScript("AddMarker1", new object[] { xn.Attributes["lat"].Value, xn.Attributes["lon"].Value, xn.ChildNodes[0].ChildNodes[0].Value });
                        else if (comboBox2.SelectedIndex == 1)
                            webBrowser1.Document.InvokeScript("AddMarker2", new object[] { xn.Attributes["lat"].Value, xn.Attributes["lon"].Value, xn.ChildNodes[0].ChildNodes[0].Value });
                        else
                            webBrowser1.Document.InvokeScript("AddMarker3", new object[] { xn.Attributes["lat"].Value, xn.Attributes["lon"].Value, xn.ChildNodes[0].ChildNodes[0].Value });
                    };
            };
            ofd.Dispose();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            webBrowser1.Document.InvokeScript("ClearMarkers");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            //webBrowser1.Document.InvokeScript("ClearMarkers");
            webBrowser1.Document.InvokeScript("AddStart", new object[] { sLat.Text.Replace(",", ","), sLon.Text.Replace(",", ","), "Start" });
            webBrowser1.Document.InvokeScript("AddEnd", new object[] { eLat.Text.Replace(",", ","), eLon.Text.Replace(",", ","), "End" });
            string grdata = sm.GetRoute(sLat.Text.Replace(".", ","), sLon.Text.Replace(".", ","), eLat.Text.Replace(".", ","), eLon.Text.Replace(".", ","));
            webBrowser1.Document.InvokeScript("ThroughRoute", new object[] { grdata });
            webBrowser1.Document.InvokeScript("CenterMap", new object[] { sLat.Text.Replace(",", ","), sLon.Text.Replace(",", ","), "Start" });
        }

        private void ParseURL(string url, bool calc)
        {
            url = String.IsNullOrEmpty(url) ? System.Windows.Forms.Clipboard.GetText() : url;
            if (String.IsNullOrEmpty(url)) return;
            // textBox2.Text = url.Trim();
            try
            {
                System.Collections.Specialized.NameValueCollection kvs = System.Web.HttpUtility.ParseQueryString(url);
                string xx = kvs["x"];
                string yy = kvs["y"];
                string[] xa = xx.Split(new char[] { ',' });
                string[] ya = yy.Split(new char[] { ',' });
                if ((xa.Length > 1) && (xa.Length == ya.Length))
                {
                    sLat.Text = ya[0].Trim();
                    sLon.Text = xa[0].Trim();
                    eLat.Text = ya[1].Trim();
                    eLon.Text = xa[1].Trim();
                    if (calc) button3_Click(this, null);
                };
            }
            catch { };
        }

        private void textBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar != (char)13) return;
            ParseURL(textBox2.Text.Trim(), true);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            ParseURL(textBox2.Text.Trim(), true);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            string xml;
            textBox4.Text = xml = sm.GetDirectRoute(sLat.Text, sLon.Text, eLat.Text, eLon.Text);
            nmsRoutesDirectCall.nmsRouteClient dc = new nmsRoutesDirectCall.nmsRouteClient();
            nmsRoutesDirectCall.Route rr = dc.XMLToObject(xml);
            label5.Text = "Length: " + rr.driveLength.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    [ComVisible(true)]
    public class ScriptManager
    {
        private TestForm form;

        public ScriptManager(TestForm form)
        {
            this.form = form;
        }

        public void Mouse(string lat, string lon)
        {
            form.mLat.Text = lat.Replace(",",".");
            form.mLon.Text = lon.Replace(",", ".");
        }

        public void Start(string lat, string lon)
        {
            form.sLat.Text = lat.Replace(",", ".");
            form.sLon.Text = lon.Replace(",", ".");
        }

        public void End(string lat, string lon)
        {
            form.eLat.Text = lat.Replace(",", ".");
            form.eLon.Text = lon.Replace(",", ".");
        }

        public string GetDirectRoute(string _sLat, string _sLon, string _eLat, string _eLon)
        {
            double sLat = double.Parse(_sLat, System.Globalization.CultureInfo.InvariantCulture);
            double sLon = double.Parse(_sLon, System.Globalization.CultureInfo.InvariantCulture);
            double eLat = double.Parse(_eLat, System.Globalization.CultureInfo.InvariantCulture);
            double eLon = double.Parse(_eLon, System.Globalization.CultureInfo.InvariantCulture);

            nmsRoutesDirectCall.nmsRouteClientStopList stc = new nmsRoutesDirectCall.nmsRouteClientStopList();
            stc.AddStop("СТАРТ", sLat, sLon);
            stc.AddStop("ФИНИШ", eLat, eLon);

            nmsRoutesDirectCall.nmsRouteClient rcc = new nmsRoutesDirectCall.nmsRouteClient(form.comboBox1.Text, Convert.ToInt32(form.sPort.Text));
            string xml = rcc.GetRouteXML("TEST", stc, DateTime.Now, 1 + (form.checkBox1.Checked ? 2 : 0));

            return xml;
        }

        public string GetRoute(string _sLat, string _sLon, string _eLat, string _eLon)
        {
            double sLat = double.Parse(_sLat);
            double sLon = double.Parse(_sLon);
            double eLat = double.Parse(_eLat);
            double eLon = double.Parse(_eLon);

            JsonSerializerSettings jss = new JsonSerializerSettings();
            jss.Formatting = Newtonsoft.Json.Formatting.Indented;

            NavicomMapServices.Routes.Route rt = new Route();

            dkxce.Route.ISolver.RStop[] st = new dkxce.Route.ISolver.RStop[2];
            st[0] = new dkxce.Route.ISolver.RStop("СТАРТ", sLat, sLon);
            st[1] = new dkxce.Route.ISolver.RStop("ФИНИШ", eLat, eLon);

            string txt = "Поиск маршрута:\r\nНачало в " + DateTime.Now.ToString("HH:mm:ss.ms");
            form.textBox1.Text = txt;
            form.textBox1.Refresh();
            DateTime sdt = DateTime.Now;

            dkxce.Route.ISolver.RouteClient rc = new dkxce.Route.ISolver.RouteClient(form.comboBox1.Text, Convert.ToInt32(form.sPort.Text),"D6AEA3EC848644258DCD06A781B2C036");
            dkxce.Route.ISolver.RResult rr = rc.GetRoute(st, DateTime.Now, 1 + (form.checkBox1.Checked ? 2 : 0), null);            
            
            txt += "\r\nКонец в " + DateTime.Now.ToString("HH:mm:ss.ms") + "\r\n";
            TimeSpan ts = DateTime.Now.Subtract(sdt);
            txt += "Выполнялось: " + String.Format("{0:00}:{1:00}:{2:00}.{3:000}", new object[] { ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds }) + "\r\n\r\n";
            form.textBox1.Text = txt;
            form.textBox1.Refresh();

            rt.driveLength = rr.driveLength;
            rt.driveTime = rr.driveTime;
            rt.startTime = rr.startTime;
            rt.finishTime = rr.finishTime;
            rt.LastError = rr.LastError;
            rt.stops = new Stop[rr.stops.Length];
            for (int i = 0; i < rr.stops.Length; i++)
                rt.stops[i] = new Stop(rr.stops[i].name, rr.stops[i].lat, rr.stops[i].lon);
            if (rt.LastError != "")
                return JsonConvert.SerializeObject(rt, jss);

            if (rr.vector != null)
            {
                List<XYPoint> vec = new List<XYPoint>();
                for (int i = 0; i < rr.vector.Length; i++)
                    vec.Add(new XYPoint(rr.vector[i].X, rr.vector[i].Y));
                rt.polyline = vec.ToArray();
                rt.polylineSegments = rr.vectorSegments;
            };
            txt += "Маршрут:\r\nДлина: " + (rt.driveLength / 1000).ToString().Replace(",", ".") + " км\r\nВремя: " + rt.driveTime.ToString().Replace(",", ".") + " мин\r\n" +
                "Старт: " + rt.startTime.ToString() + "\r\nФиниш: " + rt.finishTime.ToString() + "\r\n\r\n";

            if (rr.description != null)
            {
                rt.instructions = new RoutePoint[rr.description.Length];
                for (int i = 0; i < rt.instructions.Length; i++)
                {
                    rt.instructions[i] = new RoutePoint(i, (double)rr.description[i].Lon, (double)rr.description[i].Lat, 0, 0, (double)rr.description[i].dist, rr.startTime.AddMinutes(rr.description[i].time));
                    rt.instructions[i].iStreet = rr.description[i].name;

                    if (rr.description[i].instructions.Length > 0)
                        rt.instructions[i].iToDo = rr.description[i].instructions[0];
                    if (rr.description[i].instructions.Length > 1)
                        rt.instructions[i].iToGo = rr.description[i].instructions[1];
                };
                rt.instructionsSegments = rr.descriptionSegments;

                // // //

                txt += "Описание:\r\n";
                for (int i = 0; i < rr.description.Length; i++)
                {
                    txt += "[" + rt.instructions[i].no.ToString() + "] " + rt.instructions[i].iStreet + "\r\nLat: " + rt.instructions[i].y.ToString().Replace(",", ".") + "\r\nLon: " + rt.instructions[i].x.ToString().Replace(",", ".") + "\r\n";
                    if (rt.instructions[i].iToDo.Length > 0) txt += rt.instructions[i].iToDo + "\r\n";
                    if (rt.instructions[i].iToGo.Length > 0) txt += rt.instructions[i].iToGo + "\r\n";
                    txt += "\r\n";
                };
            };

            form.textBox1.Text = txt;

            string pp = "http://127.0.0.1:8080/nms/route?k=test&f=2&minby=time&x={0},{1}&y={2},{3}&n=S,F";
            pp = String.Format(pp, new object[] { form.sLon.Text.Trim().Replace(",", "."), form.eLon.Text.Trim().Replace(",", "."), form.sLat.Text.Trim().Replace(",", "."), form.eLat.Text.Trim().Replace(",", ".") });
            form.textBox2.Text = pp;
            pp = "http:///127.0.0.1:8080/nms/help/example_Routes2.html?{lat:" + form.sLat.Text.Trim().Replace(",", ".") + ",lon:" + form.sLon.Text.Trim().Replace(",", ".") + ",zoom:11,s:{lat:" + form.sLat.Text.Trim().Replace(",", ".") + ",lon:" + form.sLon.Text.Trim().Replace(",", ".") + "},f:{lat:" + form.eLat.Text.Trim().Replace(",", ".") + ",lon:" + form.eLon.Text.Trim().Replace(",", ".") + "}}#TEST";
            form.textBox3.Text = pp;

            return JsonConvert.SerializeObject(rt, jss);
        }
    }
}