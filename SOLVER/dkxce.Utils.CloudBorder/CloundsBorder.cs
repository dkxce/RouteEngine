using System;
using System.Drawing;
using System.Collections.Generic;
using System.Text;

namespace dkxce.Utils
{
    /// <summary>
    ///     Make Border (Bounds Polygon) of Cloud of Points
    /// </summary>
    public static class CloudBorder
    {
        /// <summary>
        ///     Get Bounds for Cloud of Points
        /// </summary>
        /// <param name="cloud">Cloud of Points</param>
        /// <param name="step">Accuracy steps (lower - more points, bigger - less points)</param>
        /// <returns>Polygon</returns>
        public static PointF[] GetCloudBorder(PointF[] cloud, double step)
        {
            return GetCloudBorder(cloud, step, 0);
        }

        /// <summary>
        ///     Get Bounds for Cloud of Points
        /// </summary>
        /// <param name="cloud">Cloud of Points</param>
        /// <param name="step">Accuracy steps (lower - more points, bigger - less points)</param>
        /// <param name="enlarge">Enlarge border value</param>
        /// <returns>Polygon</returns>
        public static PointF[] GetCloudBorder(PointF[] cloud, double step, double enlarge)
        {
            if (cloud == null) return null;
            if (cloud.Length < 3) return null;

            double[] box = new double[4] { double.MaxValue, double.MaxValue, double.MinValue, double.MinValue };
            foreach (PointF p in cloud)
            {
                if (p.X < box[0]) box[0] = p.X;
                if (p.Y < box[1]) box[1] = p.Y;
                if (p.X > box[2]) box[2] = p.X;
                if (p.Y > box[3]) box[3] = p.Y;
            };
            return GetCloudBorder(box, cloud, step, enlarge);
        }

        /// <summary>
        ///     Get Bounds for Cloud of Points
        /// </summary>
        /// <param name="box">Bounds box (minX, minY, maxX, maxY)</param>
        /// <param name="cloud">Cloud of Points</param>
        /// <param name="step">Accuracy steps (lower - more points, bigger - less points)</param>
        /// <returns>Polygon</returns>
        public static PointF[] GetCloudBorder(double[] box, PointF[] cloud, double step)
        {
            return GetCloudBorder(box, cloud, step, 0);
        }

        /// <summary>
        ///     Get Bounds for Cloud of Points
        /// </summary>
        /// <param name="box">Bounds box (minX, minY, maxX, maxY)</param>
        /// <param name="cloud">Cloud of Points</param>
        /// <param name="step">Accuracy steps (lower - more points, bigger - less points)</param>
        /// <param name="enlarge">Enlarge border value</param>
        /// <returns>Polygon</returns>
        public static PointF[] GetCloudBorder(double[] box, PointF[] cloud, double step, double enlarge)
        {
            if (box == null) return null;
            if (cloud == null) return null;
            if (cloud.Length < 3) return null;
            step = Math.Abs(step);
            enlarge = Math.Abs(enlarge);

            // Enlarge Box
            if (enlarge > 0) { box[0] -= enlarge; box[1] -= enlarge; box[2] += enlarge; box[3] += enlarge; };
          
            // Calculate array width size
            int iWidth = 0;
            {
                double dWidth = (box[2] - box[0]) / step;
                dWidth = Math.Truncate(dWidth) + 1;
                iWidth = (int)dWidth;
            };

            // width elements
            double[] top = new double[iWidth]; double[] btm = new double[iWidth];            
            for (int i = 0; i < iWidth; i++) { top[i] = double.MinValue; btm[i] = double.MaxValue; };

            // Search Min & Max
            for (int i = 0; i < cloud.Length; i++)
            {
                int cell = (int)((double)(cloud[i].X - box[0]) / (double)step);
                if (cloud[i].Y > top[cell]) top[cell] = cloud[i].Y;
                if (cloud[i].Y < btm[cell]) btm[cell] = cloud[i].Y;
            };

            if (enlarge > 0)
            {
                // if first element is +/- Inf
                int inf = 0;
                while (top[0] == double.MinValue)
                {
                    for (int i = inf++; i >= 0; i--)
                    {
                        top[i] = top[i + 1];
                        btm[i] = btm[i + 1];
                    };
                };
                // enlarge first element
                top[0] += enlarge;
                btm[0] -= enlarge;
            };

            // wedge off first
            double sdiv2 = step / 2.0;
            if (top[0] == btm[0])
            {
                top[0] += sdiv2;
                btm[0] -= sdiv2;
            };

            // add first line element
            List<PointF> resultTop = new List<PointF>();
            resultTop.Add(new PointF((float)(box[0]), (float)top[0]));
            resultTop.Add(new PointF((float)(box[0] + step), (float)top[0]));

            // add last line element
            List<PointF> resultBtm = new List<PointF>();
            resultBtm.Add(new PointF((float)(box[0]), (float)btm[0]));
            resultBtm.Add(new PointF((float)(box[0] + step), (float)btm[0]));

            // foreach width element
            for (int i = 1; i < iWidth; i++)
            {
                // prev or enlarge
                if (top[i] == double.MinValue) top[i] = top[i - 1]; else if (enlarge > 0) top[i] += enlarge;
                if (btm[i] == double.MaxValue) btm[i] = btm[i - 1]; else if (enlarge > 0) btm[i] -= enlarge;

                // wedge off
                if ((top[i] <= btm[i - 1])) top[i] = btm[i - 1] + (enlarge > 0 ? enlarge : sdiv2);
                if ((btm[i] >= top[i - 1])) btm[i] = top[i - 1] - (enlarge > 0 ? enlarge : sdiv2);
                if ((top[i] == btm[i])) { top[i] += (enlarge > 0 ? enlarge : sdiv2); btm[i] -= (enlarge > 0 ? enlarge : sdiv2); };

                // add lines for max
                if (top[i] != top[i - 1]) resultTop.Add(new PointF((float)(box[0] + (double)i * step), (float)top[i]));
                resultTop.Add(new PointF((float)(box[0] + (double)(i + 1) * step), (float)top[i]));

                // add lines for min
                if (btm[i] != btm[i - 1]) resultBtm.Add(new PointF((float)(box[0] + (double)i * step), (float)btm[i]));
                resultBtm.Add(new PointF((float)(box[0] + (double)(i + 1) * step), (float)btm[i]));
            };

            List<PointF> result = new List<PointF>();
            result.AddRange(resultTop);
            resultBtm.Reverse();
            result.AddRange(resultBtm);

            return result.ToArray();
        }

        public static PointF[] EnlargePolygon(PointF[] poly, float offset)
        {
            if (offset == 0) return null;
            if (poly == null) return null;
            if (poly.Length < 3) return null;
            PointF[] pll = new PointF[poly.Length + 1];
            for (int i = 0; i < poly.Length; i++) pll[i] = poly[i];
            pll[pll.Length - 1] = poly[0]; // closed line

            // get poly from line buffer
            PolyLineBuffer.PolyLineBufferCreator.PolyResult pr = PolyLineBuffer.PolyLineBufferCreator.GetLineBufferPolygon(pll, Math.Abs(offset), true, true, null, offset >= 0 ? 1 : 2); // 

            if (offset < 0) // reduce
            {
                List<int[]> theSame = new List<int[]>();
                Dictionary<PointF, int[]> lpc = new Dictionary<PointF, int[]>();
                if ((pr.polygon != null) && (pr.polygon.Length > 0))
                {
                    for (int i = 0; i < pr.polygon.Length; i++)
                    {
                        if (!lpc.ContainsKey(pr.polygon[i]))
                            lpc.Add(pr.polygon[i], new int[] { i });
                        else
                        {
                            List<int> ia = new List<int>(lpc[pr.polygon[i]]);
                            ia.Add(i);
                            lpc[pr.polygon[i]] = ia.ToArray();
                            theSame.Add(ia.ToArray());
                        };
                    };
                };
                if (theSame.Count > 0)
                {
                    // outer polygon // last polygon always bigger
                    //List<PointF> outer = new List<PointF>();
                    //for (int i = 0; i < theSame[theSame.Count - 1][0]; i++) outer.Add(pr.polygon[i]);
                    //for (int i = theSame[theSame.Count - 1][1]; i < pr.polygon.Length; i++) outer.Add(pr.polygon[i]);
                    //return outer.ToArray();

                    // inner polygon
                    List<PointF> inner = new List<PointF>();
                    for (int i = theSame[0][0]; i < theSame[0][1]; i++) inner.Add(pr.polygon[i]);
                    return inner.ToArray();
                };
            };

            return pr.polygon; // enlarge only
        } 
    }
}
