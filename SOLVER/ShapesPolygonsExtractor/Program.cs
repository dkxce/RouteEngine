using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ShapesPolygonsExtractor
{
    class Program
    {
        private static string new_file_syntax = "$FILE$ - $INDEX$";

        static void Main(string[] args)
        {
            Console.WriteLine("Shapes Polygon Extractor by milokz@gmail.com\r\n");
            ConsoleColor cc = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("  run in folder with shapes files");
            Console.WriteLine("  or add files to extract in command line");
            Console.WriteLine("  use /km=.. to enlarge box in kilometers, ex: /km=5");
            Console.WriteLine("  use /path=.. where to find shape files, ex: /path=C:\\");
            Console.WriteLine("  use /f=.. as new file name syntax, ex: `/f=$FILE$ - $INDEX$`");
            Console.WriteLine("     where: $FILE$ - source file name without extention");
            Console.WriteLine("            $INDEX$ - polygon index in file from zero");
            Console.WriteLine("   or `/f={ID} - {NAME} - $INDEX$ in $FILE$`");
            Console.WriteLine("     where: {FIELD_NAME} - text from DBF field with FIELD_NAME");
            Console.WriteLine("            {ID,10} - padding zero to left while fld val len < 10 symbols");
            Console.WriteLine("            {ID,-8} - padding zero to right while fld val len < 8 symbols");
            Console.WriteLine("            {ID:LALB} - padding LALB to left while fld val len < 4 symbols");
            Console.WriteLine("            {ID*RABCD} - padding RABCD to right while fld val len < 5 symbols");
            Console.ForegroundColor = cc;
            Console.WriteLine();

            int[] ctr = new int[] { 0, 0 };
            int km = -1;
            bool addCRLF = false;
            string[] files = null;
            if ((args != null) && (args.Length > 0))
            {
                List<string> f2a = new List<string>();
                foreach (string arg in args)
                {
                    if (!arg.StartsWith("/"))
                        f2a.Add(arg);
                    else
                    {
                        if (arg.StartsWith("/km="))
                        {
                            int rkm = 0;
                            if (int.TryParse(arg.Substring(4), out rkm)) km = rkm;
                        };
                        if (arg.StartsWith("/path="))
                        {
                            string path = arg.Substring(6);
                            string[] ff = Directory.GetFiles(path, "*.shp", SearchOption.TopDirectoryOnly);
                            f2a.AddRange(ff);
                            Console.WriteLine("Added {1} files in {0}", path, ff.Length);
                            addCRLF = true;
                        };
                        if (arg.StartsWith("/f="))
                        {
                            new_file_syntax = arg.Substring(3).Trim();
                            Console.WriteLine("Set new file name syntax: {0}", new_file_syntax);
                            addCRLF = true;
                        };
                    };
                };
                if(f2a.Count > 0)
                    files = f2a.ToArray();
            };
            if (files == null)
            {
                files = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.shp", SearchOption.TopDirectoryOnly);
                Console.WriteLine("Added {1} files in {0}", AppDomain.CurrentDomain.BaseDirectory, files.Length);
                addCRLF = true;
            };
            if (addCRLF) Console.WriteLine();
            if ((files != null) && (files.Length > 0)) ctr = ProcessFiles(files, km);

            if(ctr[1] > 0)
                Console.ForegroundColor = ConsoleColor.Green;
            else
                Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("\r\nEXTRACTED {1} of {0} FILES\r\n", ctr[0], ctr[1]);
            Console.ForegroundColor = cc;

            int sec = 5;
            while (sec > 0)
            {
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write("Program will be closed in {0} seconds", sec--);
                System.Threading.Thread.Sleep(1000);
            };
        }

        static int[] ProcessFiles(string[] files, int km)
        {
            ConsoleColor cc = Console.ForegroundColor;            
            while (km < 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("Enter how to enlarge box in km: ");
                Console.ForegroundColor = cc;
                string rl = Console.ReadLine();
                int rkm = 0;
                if (int.TryParse(rl, out rkm)) km = rkm;
                if (km < 0)
                {
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                    Console.Write("{0,-40}", "");
                    Console.SetCursorPosition(0, Console.CursorTop);
                };
            };            
            Console.WriteLine("Enlarge each box to {0} km", km);
            Console.WriteLine("New file name syntax: {0}\r\n", new_file_syntax);            

            string dir = AppDomain.CurrentDomain.BaseDirectory + @"\EXTRACTED_DATA\";
            Directory.CreateDirectory(dir);

            int counter = 0;
            int counted = 0;
            Console.WriteLine("Processing files...");
            foreach (string file in files)
            {
                Console.Write(" {0} - ", Path.GetFileName(file));
                if(!File.Exists(file))
                    Console.WriteLine("Not Found");
                else
                {
                    int res = ProcessFile(file, dir, km);
                    Console.WriteLine("{0}", res > 0 ? "OK" : "Skipped");
                    counter++;
                    if (res > 0) counted++;
                };
            };
            return new int[] { counter, counted };
        }

        static int ProcessFile(string file, string dir, int km)
        {
            string dFile = dir + Path.GetFileNameWithoutExtension(file) +".shp";

            bool readDBF = (new Regex(@"\{[\D\}]+}", RegexOptions.None)).Match(new_file_syntax).Success;
            Regex rx = new Regex(@"(?<repl>\{(?<full>(?<name>\D\w+)(?<suffix>[^\}]*?))\})", RegexOptions.None);

            PolyReader pr = new PolyReader(file, readDBF);
            int left = Console.CursorLeft;
            Console.Write("total {0} polygons - ", pr.Regions.Count);
            if (pr.Regions.Count == 0) return 0;

            Console.SetCursorPosition(left, Console.CursorTop);
            Console.Write("converting {0} polygons ... ", pr.Regions.Count);
            int cCounter = 0;
            for (int i = 0; i < pr.Regions.Count; i++)
            {
                Console.SetCursorPosition(left, Console.CursorTop);
                Console.Write("converting {1}/{0} polygons ... ", pr.Regions.Count, i + 1);
                string addt = new_file_syntax;
                addt = addt.Replace("$FILE$", String.Format("{0}", Path.GetFileNameWithoutExtension(dFile)));
                addt = addt.Replace("$INDEX$", String.Format("{0:000000}", i+1));
                if (pr.Regions[i].Attributes != null)
                {
                    MatchCollection mx = rx.Matches(addt);
                    foreach (Match mc in mx)
                    {
                        string rsrt = pr.Regions[i].Attributes.ContainsKey(mc.Groups["name"].Value) ? pr.Regions[i].Attributes[mc.Groups["name"].Value].ToString().Trim() : "";
                        Padding(ref rsrt, mc.Groups["suffix"].Value);                        
                        addt = addt.Replace(mc.Groups["repl"].Value, rsrt);
                    };
                };
                string dest = Path.GetDirectoryName(dFile) + @"\" + addt + Path.GetExtension(dFile);
                PolyReader.ExtractPoly(pr.Regions[i], dest, km);
                cCounter++;
            };
            Console.SetCursorPosition(left, Console.CursorTop);
            Console.Write("converted {1} of {0} polygons - ", pr.Regions.Count, cCounter);
            return cCounter;
        }

        static void Padding(ref string rsrt, string suff)
        {
            if (String.IsNullOrEmpty(suff)) return;
            if (suff.Length < 2) return;
            if (suff.StartsWith(","))
            {
                int pl = 0;
                if (int.TryParse(suff.Substring(1), out pl))
                {
                    if (pl >= 0) rsrt = rsrt.PadLeft(pl, '0');
                    else rsrt = rsrt.PadRight(pl * -1, '0');
                };
            };
            if (suff.StartsWith(":"))
            {
                string bob = suff.Substring(1);
                if (bob.Length <= rsrt.Length) return;
                byte[] sa = System.Text.Encoding.UTF32.GetBytes(rsrt);
                byte[] da = System.Text.Encoding.UTF32.GetBytes(bob);
                Array.Copy(sa, 0, da, da.Length - sa.Length, sa.Length);
                rsrt = System.Text.Encoding.UTF32.GetString(da);
            };
            if (suff.StartsWith("*"))
            {
                string bob = suff.Substring(1);
                if (bob.Length <= rsrt.Length) return;
                byte[] sa = System.Text.Encoding.UTF32.GetBytes(rsrt);
                byte[] da = System.Text.Encoding.UTF32.GetBytes(bob);
                Array.Copy(sa, 0, da, 0, sa.Length);
                rsrt = System.Text.Encoding.UTF32.GetString(da);
            };
        }
    }
}
