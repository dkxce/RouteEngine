using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Threading;
using System.Text;
using System.Web;
using System.IO;
using System.Net;
using System.Windows.Forms;
//using CSScriptLibrary;

namespace WorkingLoadTest
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (thr.isRunning)
                Stop();
            else
                Start();
        }

        private ThreadData thr = new ThreadData();

        double[][] EmulateLatLonValues = null;
        private void Start()
        {
            thr.isRunning = true;
            button1.Text = "Stop";

            string scriptText = "using System; using System.Collections; namespace SCRIPT { public class ScriptA : Script {\r\n" +
                                "public override Hashtable call() { Hashtable vars = new Hashtable();\r\n" +
                                inConsts.Text +
                                "\r\nreturn vars;}\r\n" +
                                "}}";

            System.Reflection.Assembly asm = CSScriptLibrary.CSScript.LoadCode(scriptText, null);
            CSScriptLibrary.AsmHelper script = new CSScriptLibrary.AsmHelper(asm);
            SCRIPT.Script obj = (SCRIPT.Script)script.CreateObject("SCRIPT.ScriptA");
            Hashtable cVars = obj.call();

            string url = inServer.Text + inMethod.Text.Replace("{k}", inKey.Text);
            foreach (DictionaryEntry entry in cVars)
                url = url.Replace("{" + (string)entry.Key + "}", (string)entry.Value);
            string[] urls = new string[] { url };

            if (inVars.SelectedIndex == 0)
            {
                EmulateLatLonValues = obj.EmulateLatLon();
                urls = new string[EmulateLatLonValues.Length];
                for (int i = 0; i < EmulateLatLonValues.Length; i++)
                {
                    urls[i] = url;
                    urls[i] = urls[i].Replace("{x}", EmulateLatLonValues[i][1].ToString().Replace(",", "."));
                    urls[i] = urls[i].Replace("{y}", EmulateLatLonValues[i][0].ToString().Replace(",", "."));
                    urls[i] = urls[i].Replace("{xx}", EmulateLatLonValues[i][1].ToString().Replace(",", ".") + "," + EmulateLatLonValues[i][3].ToString().Replace(",", "."));
                    urls[i] = urls[i].Replace("{yy}", EmulateLatLonValues[i][0].ToString().Replace(",", ".") + "," + EmulateLatLonValues[i][2].ToString().Replace(",", "."));
                };
            };
            if (inVars.SelectedIndex == 1)
            {
                string[] ad = obj.EmulateAddress();
                urls = new string[ad.Length];
                for (int i = 0; i < ad.Length; i++)
                    urls[i] = url.Replace("{s}", ad[i]);
            };
            if (inVars.SelectedIndex == 2)
            {
                string[] ad = obj.EmulateHouses();
                urls = new string[ad.Length];
                for (int i = 0; i < ad.Length; i++)
                    urls[i] = url.Replace("{s}", ad[i]);
            };
            if (inVars.SelectedIndex == 3)
            {
                int[][] xy = obj.EmulateTileXY();
                urls = new string[xy.Length];
                for (int i = 0; i < xy.Length; i++)
                {
                    urls[i] = url;
                    urls[i] = urls[i].Replace("{x}", xy[i][0].ToString());
                    urls[i] = urls[i].Replace("{y}", xy[i][1].ToString());
                };
            };         
   
            //////

            thr.mainUrl = url;
            thr.thread = new System.Threading.Thread(RunThread);
            thr.thread.Start(urls);                       
        }
        
        private void Stop()
        {
            thr.isRunning = false;
            thr.thread.Abort();
            thr.thread = null;
            thr.stopped = DateTime.Now;
            button1.Text = "Start";
        }

        private void RunThread(object data)
        {
            thr.data = (string[])data;
            thr.started = DateTime.Now;
            thr.stopped = DateTime.MinValue;
            thr.lastQuery = "";
            thr.lastResponse = "";
            thr.lastError = "";
            thr.lastErrorQuery = "";
            thr.proccessed = 0;
            thr.good = 0;
            thr.bad = 0;

            int dataCount = 0;
            while (thr.isRunning)
            {
                string currUrl = thr.data[dataCount];                
                string res = "";

                try
                {
                    if (currUrl.IndexOf("http:") == 0)
                    {
                        res = HTTPReq(currUrl);
                    }
                    else if (currUrl.IndexOf("tcp:") == 0)
                    {
                        System.Web.HttpRequest hr = new HttpRequest("", currUrl, currUrl.Substring(currUrl.IndexOf("?") + 1));
                        if (hr.Path.ToLower() == "/sroute.ashx")
                        {
                            dkxce.Route.ISolver.RStop[] ss = new dkxce.Route.ISolver.RStop[]{
                                new dkxce.Route.ISolver.RStop("start", EmulateLatLonValues[dataCount][0], EmulateLatLonValues[dataCount][1]),
                                new dkxce.Route.ISolver.RStop("end", EmulateLatLonValues[dataCount][2], EmulateLatLonValues[dataCount][3])};
                            long flags = 0;
                            if (hr["p"] == "1") flags += 0x01;
                            if (hr["i"] == "1") flags += 0x02;
                            dkxce.Route.ISolver.RouteClient rc = new dkxce.Route.ISolver.RouteClient(hr.Url.Host, 7755, hr["k"]);
                            
                            //dkxce.Route.ISolver.RResult rr = rc.GetRoute(ss, System.DateTime.Now, flags, new int[0]);
                            dkxce.Route.ISolver.RResult rr = rc.GetRoute(ss, System.DateTime.Now, flags, new int[0]);
                            // , new dkxce.Route.Classes.PointF[] { new dkxce.Route.Classes.PointF(1, 2), new dkxce.Route.Classes.PointF(3, 4), new dkxce.Route.Classes.PointF(5, 6) }, 999, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 24, 35 }
                            res = "driveLength: " + rr.driveLength.ToString();
                        };
                        if (hr.Path.ToLower() == "/snearroad.ashx")
                        {
                            dkxce.Route.ISolver.RouteClient rc = new dkxce.Route.ISolver.RouteClient(hr.Url.Host, 7755, hr["k"]);
                            dkxce.Route.ISolver.RNearRoad[] nr = rc.GetNearRoad(new double[] { EmulateLatLonValues[dataCount][0], EmulateLatLonValues[dataCount][2] }, new double[] { EmulateLatLonValues[dataCount][1], EmulateLatLonValues[dataCount][3] }, hr["n"] == "1");
                            res = nr[0].lat + "\r\n" + nr[0].lon + "\r\n" + nr[0].distance + "\r\n" + nr[0].name + "\r\n\r\n" +
                                nr[1].lat + "\r\n" + nr[1].lon + "\r\n" + nr[1].distance + "\r\n" + nr[1].name + "\r\n\r\n";
                        };
                    }
                    else if (currUrl.IndexOf("tcpxml:") == 0)
                    {
                        System.Web.HttpRequest hr = new HttpRequest("", currUrl, currUrl.Substring(currUrl.IndexOf("?") + 1));
                        if (hr.Path.ToLower() == "/sroute.ashx")
                        {
                            dkxce.Route.ISolver.RStop[] ss = new dkxce.Route.ISolver.RStop[]{
                                new dkxce.Route.ISolver.RStop("start", EmulateLatLonValues[dataCount][0], EmulateLatLonValues[dataCount][1]),
                                new dkxce.Route.ISolver.RStop("end", EmulateLatLonValues[dataCount][2], EmulateLatLonValues[dataCount][3])};
                            long flags = 0;
                            if (hr["p"] == "1") flags += 0x01;
                            if (hr["i"] == "1") flags += 0x02;
                            dkxce.Route.ISolver.RouteClient rc = new dkxce.Route.ISolver.RouteClient(hr.Url.Host, 7755, hr["k"]);
                            res = rc.GetRouteXML(ss, System.DateTime.Now, flags, new int[0]);
                            // , new dkxce.Route.Classes.PointF[] { new dkxce.Route.Classes.PointF(1, 2), new dkxce.Route.Classes.PointF(3, 4), new dkxce.Route.Classes.PointF(5, 6) }, 999, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 24, 35 }
                        };
                        if (hr.Path.ToLower() == "/snearroad.ashx")
                        {
                            dkxce.Route.ISolver.RouteClient rc = new dkxce.Route.ISolver.RouteClient(hr.Url.Host, 7755, hr["k"]);
                            res = rc.GetNearRoadXML(new double[] { EmulateLatLonValues[dataCount][0], EmulateLatLonValues[dataCount][2] }, new double[] { EmulateLatLonValues[dataCount][1], EmulateLatLonValues[dataCount][3] }, hr["n"] == "1");
                        };
                    }
                    else if (currUrl.IndexOf("dcall:") == 0)
                    {
                        System.Web.HttpRequest hr = new HttpRequest("", currUrl, currUrl.Substring(currUrl.IndexOf("?") + 1));
                        if (hr.Path.ToLower() == "/sroute.ashx")
                        {
                            long flags = 0;
                            if (hr["p"] == "1") flags += 0x01;
                            if (hr["i"] == "1") flags += 0x02;
                            nmsRoutesDirectCall.nmsRouteClient rc = new nmsRoutesDirectCall.nmsRouteClient(hr.Url.Host, 7755);
                            res = rc.GetRouteXML(hr["k"], new string[] { "start", "stop" }, new double[] { EmulateLatLonValues[dataCount][0], EmulateLatLonValues[dataCount][2] }, new double[] { EmulateLatLonValues[dataCount][1], EmulateLatLonValues[dataCount][3] } , System.DateTime.Now, flags);
                            // , new System.Drawing.PointF[] { new System.Drawing.PointF(1, 2), new System.Drawing.PointF(3, 4), new System.Drawing.PointF(5, 6) }, 999, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 24, 35 }
                        };
                        if (hr.Path.ToLower() == "/snearroad.ashx")
                        {
                            nmsRoutesDirectCall.nmsRouteClient rc = new nmsRoutesDirectCall.nmsRouteClient(hr.Url.Host, 7755);
                            res = rc.GetNearRoadXML(hr["k"], new double[] { EmulateLatLonValues[dataCount][0], EmulateLatLonValues[dataCount][2] }, new double[] { EmulateLatLonValues[dataCount][1], EmulateLatLonValues[dataCount][3] }, hr["n"] == "1");                            
                        };
                        //thr.tcpSolver.GetRoute
                    }
                    else if (currUrl.IndexOf("remoting:") == 0)
                    {
                        System.Web.HttpRequest hr = new HttpRequest("", currUrl, currUrl.Substring(currUrl.IndexOf("?") + 1));
                        if (hr.Path.ToLower() == "/sroute.ashx")
                        {
                            dkxce.Route.ISolver.RStop[] ss = new dkxce.Route.ISolver.RStop[]{
                                new dkxce.Route.ISolver.RStop("start", EmulateLatLonValues[dataCount][0], EmulateLatLonValues[dataCount][1]),
                                new dkxce.Route.ISolver.RStop("end", EmulateLatLonValues[dataCount][2], EmulateLatLonValues[dataCount][3])};
                            long flags = 0;
                            if (hr["p"] == "1") flags += 0x01;
                            if (hr["i"] == "1") flags += 0x02;
                            dkxce.Route.ISolver.IRoute rc = dkxce.Route.ISolver.IRouteClient.Connect(hr.Url.Host, 7755);
                            dkxce.Route.ISolver.RResult rr = rc.GetRoute(ss, System.DateTime.Now, flags, new int[0]);
                            res = "driveLength: " + rr.driveLength.ToString();
                        };
                        if (hr.Path.ToLower() == "/snearroad.ashx")
                        {
                            dkxce.Route.ISolver.IRoute rc = dkxce.Route.ISolver.IRouteClient.Connect(hr.Url.Host, 7755);
                            dkxce.Route.ISolver.RNearRoad[] nr = rc.GetNearRoad(new double[] { EmulateLatLonValues[dataCount][0], EmulateLatLonValues[dataCount][2] }, new double[] { EmulateLatLonValues[dataCount][1], EmulateLatLonValues[dataCount][3] }, hr["n"] == "1");
                            res = nr[0].lat + "\r\n" + nr[0].lon + "\r\n" + nr[0].distance + "\r\n" + nr[0].name + "\r\n\r\n" +
                                nr[1].lat + "\r\n" + nr[1].lon + "\r\n" + nr[1].distance + "\r\n" + nr[1].name + "\r\n\r\n";
                        };
                    };
                    thr.good++;
                }
                catch (ThreadAbortException)
                {

                }
                catch (Exception ex)
                {
                    thr.lastErrorQuery = currUrl;
                    thr.lastError = ex.ToString();
                    thr.bad++;
                };
                dataCount++;
                if (dataCount == thr.data.Length) dataCount = 0;

                thr.lastQuery = currUrl;
                thr.lastResponse = res;
                thr.proccessed++;                
                System.Threading.Thread.Sleep(1);
            };            
        }

        public string HTTPReq(string query)
        {
            string restxt = "";
            HttpWebRequest wr = (HttpWebRequest)HttpWebRequest.Create(query);
            wr.Timeout = 120 * 1000;
            HttpWebResponse res = (HttpWebResponse)wr.GetResponse();
            Stream rs = res.GetResponseStream();
            StreamReader sr = new StreamReader(rs);
            restxt = sr.ReadToEnd();
            sr.Close();
            rs.Close();
            res.Close();
            return restxt;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            inVars.SelectedIndex = 0;
            timer1.Enabled = true;
        }

        public class ThreadData
        {
            public string mainUrl = "";
            public Thread thread = null;
            public bool isRunning = false;
            public DateTime started;
            public DateTime stopped;
            public string[] data = new string[0];
            public string lastQuery = "";
            public string lastResponse = "";
            public string lastError = "";
            public string lastErrorQuery = "";
            public ulong proccessed = 0;
            public ulong good = 0;
            public ulong bad = 0;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            DateTime tn = thr.stopped == DateTime.MinValue ? DateTime.Now : thr.stopped;
            TimeSpan ts = tn.Subtract(thr.started);

            string txt = String.Format("Current status: {0}\r\n",thr.isRunning);
            txt += String.Format(" mainUrl:\t\t {0}\r\n", thr.mainUrl);
            txt += String.Format(" started:\t\t {0}\r\n", thr.started);
            txt += String.Format(" stopped:\t\t {0}\r\n", thr.stopped);
            txt += String.Format(" running:\t\t {0:000}:{1:00}:{2:00}\r\n", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            txt += String.Format(" lastQuery:\t {0}\r\n", thr.lastQuery);
            txt += String.Format(" lastError:\t\t {0}\r\n", thr.lastError);
            txt += String.Format(" lastErrorQuery:\t {0}\r\n", thr.lastErrorQuery);
            txt += String.Format(" proccessed:\t {0}\r\n", thr.proccessed);
            txt += String.Format(" good:\t\t {0}\r\n", thr.good);
            txt += String.Format(" bad:\t\t {0}\r\n", thr.bad);
            txt += String.Format(" speed:\t\t {0:0.00} /sec\r\n", thr.proccessed / ts.TotalSeconds);
            txt += String.Format(" speed:\t\t {0:0.00} /min\r\n", thr.proccessed / ts.TotalMinutes);
            txt += String.Format(" queryTime:\t {0:0.0000} s\r\n", ts.TotalSeconds / thr.proccessed);

            inAcc.Text = txt;
            txtLR.Text = thr.lastResponse;

            inAcc.Update();
            txtLR.Update();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (thr.isRunning) Stop();
        }

    }    
}