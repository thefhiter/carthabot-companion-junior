using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace CarthaBotVPL.Services
{
    /// <summary>
    /// The simulator's playground geometry, loaded from carthabot_simworld.json — the manifest
    /// written by the same Blender script that authored carthabot_simworld.obj. Sensing therefore
    /// happens against exactly the geometry that is rendered.
    /// </summary>
    public class SimWorld
    {
        public Vector MatHalf { get; private set; } = new Vector(13, 10);
        public double TrackHalfWidth { get; private set; } = 0.45;
        public List<Point> Track { get; } = new List<Point>();

        public Point StartPos { get; private set; } = new Point(-3.5, -4.2);
        /// <summary>Spawn heading in degrees, 0 = +X, counter-clockwise positive.</summary>
        public double StartDeg { get; private set; }

        public Point ObstaclePos { get; private set; } = new Point(2.5, 0);
        public Vector ObstacleHalf { get; private set; } = new Vector(1.1, 0.5);
        public double ObstacleHeight { get; private set; } = 1.2;

        public Point GoalPos { get; private set; } = new Point(5, 2.5);
        public double GoalRadius { get; private set; } = 1.3;

        public List<Point> Coins { get; } = new List<Point>();
        public double CoinZ { get; private set; } = 0.95;
        public double CoinRadius { get; private set; } = 0.8;

        // robot geometry (matches the Blender-built model)
        public double RobotHalfWidth { get; private set; } = 1.4;
        public double RobotHalfLength { get; private set; } = 1.6;
        public double RobotFrontY { get; private set; } = 1.5;
        public double IrRange { get; private set; } = 2.6;
        public double GroundSensorY { get; private set; } = 1.2;

        public static SimWorld Load() => Load("carthabot_simworld.json");

        /// <summary>Load a map manifest by file name (in Vpl\Assets).</summary>
        public static SimWorld Load(string jsonFile)
        {
            var w = new SimWorld();
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Vpl", "Assets", jsonFile);
                if (!File.Exists(path)) return w;
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var r = doc.RootElement;

                Point P(JsonElement e) => new Point(e[0].GetDouble(), e[1].GetDouble());

                w.MatHalf = new Vector(r.GetProperty("matHalf")[0].GetDouble(), r.GetProperty("matHalf")[1].GetDouble());
                w.TrackHalfWidth = r.GetProperty("trackHalfWidth").GetDouble();
                foreach (var p in r.GetProperty("track").EnumerateArray()) w.Track.Add(P(p));

                var start = r.GetProperty("start");
                w.StartPos = P(start.GetProperty("pos"));
                w.StartDeg = start.GetProperty("deg").GetDouble();

                var ob = r.GetProperty("obstacle");
                w.ObstaclePos = P(ob.GetProperty("pos"));
                w.ObstacleHalf = new Vector(ob.GetProperty("half")[0].GetDouble(), ob.GetProperty("half")[1].GetDouble());
                w.ObstacleHeight = ob.GetProperty("height").GetDouble();

                var goal = r.GetProperty("goal");
                w.GoalPos = P(goal.GetProperty("pos"));
                w.GoalRadius = goal.GetProperty("radius").GetDouble();

                foreach (var c in r.GetProperty("coins").EnumerateArray()) w.Coins.Add(P(c));
                w.CoinZ = r.GetProperty("coinZ").GetDouble();
                w.CoinRadius = r.GetProperty("coinRadius").GetDouble();

                var rb = r.GetProperty("robot");
                w.RobotHalfWidth = rb.GetProperty("halfWidth").GetDouble();
                w.RobotHalfLength = rb.GetProperty("halfLength").GetDouble();
                w.RobotFrontY = rb.GetProperty("frontY").GetDouble();
                w.IrRange = rb.GetProperty("irRange").GetDouble();
                w.GroundSensorY = rb.GetProperty("groundY").GetDouble();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SimWorld manifest: " + ex.Message);
            }
            return w;
        }

        /// <summary>
        /// Quarter-way points of the line loop (for the follow-the-line mission):
        /// visiting all of them in any order then returning near the start = one lap.
        /// </summary>
        public List<Point> LineCheckpoints
        {
            get
            {
                var pts = new List<Point>();
                if (Track.Count >= 4)
                {
                    pts.Add(Track[Track.Count / 4]);
                    pts.Add(Track[Track.Count / 2]);
                    pts.Add(Track[3 * Track.Count / 4]);
                }
                return pts;
            }
        }

        // ---- geometry queries ----

        /// <summary>True when the point lies on the black line loop (within the sensing width).</summary>
        public bool OnTrack(Point p)
        {
            if (Track.Count < 2) return false;
            double best = double.MaxValue;
            for (int i = 0; i < Track.Count; i++)
            {
                var a = Track[i];
                var b = Track[(i + 1) % Track.Count];   // closed loop
                best = Math.Min(best, DistanceToSegment(p, a, b));
                if (best <= TrackHalfWidth) return true;
            }
            return false;
        }

        /// <summary>
        /// Distance along a ray (origin, unit dir) to the obstacle box at <paramref name="obstacle"/>,
        /// or double.MaxValue when the ray misses. Standard slab test.
        /// </summary>
        public double RayToObstacle(Point origin, Vector dir, Point obstacle)
        {
            double minX = obstacle.X - ObstacleHalf.X, maxX = obstacle.X + ObstacleHalf.X;
            double minY = obstacle.Y - ObstacleHalf.Y, maxY = obstacle.Y + ObstacleHalf.Y;

            double tMin = 0, tMax = double.MaxValue;
            if (Math.Abs(dir.X) < 1e-9)
            {
                if (origin.X < minX || origin.X > maxX) return double.MaxValue;
            }
            else
            {
                double t1 = (minX - origin.X) / dir.X, t2 = (maxX - origin.X) / dir.X;
                if (t1 > t2) (t1, t2) = (t2, t1);
                tMin = Math.Max(tMin, t1); tMax = Math.Min(tMax, t2);
            }
            if (Math.Abs(dir.Y) < 1e-9)
            {
                if (origin.Y < minY || origin.Y > maxY) return double.MaxValue;
            }
            else
            {
                double t1 = (minY - origin.Y) / dir.Y, t2 = (maxY - origin.Y) / dir.Y;
                if (t1 > t2) (t1, t2) = (t2, t1);
                tMin = Math.Max(tMin, t1); tMax = Math.Min(tMax, t2);
            }
            return tMin <= tMax ? tMin : double.MaxValue;
        }

        /// <summary>True when a circle of <paramref name="radius"/> at p overlaps the obstacle box.</summary>
        public bool CircleHitsObstacle(Point p, double radius, Point obstacle)
        {
            double dx = Math.Max(Math.Abs(p.X - obstacle.X) - ObstacleHalf.X, 0);
            double dy = Math.Max(Math.Abs(p.Y - obstacle.Y) - ObstacleHalf.Y, 0);
            return dx * dx + dy * dy <= radius * radius;
        }

        private static double DistanceToSegment(Point p, Point a, Point b)
        {
            var ab = b - a;
            double len2 = ab.X * ab.X + ab.Y * ab.Y;
            if (len2 < 1e-12) return (p - a).Length;
            double t = ((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / len2;
            t = Math.Max(0, Math.Min(1, t));
            var proj = new Point(a.X + ab.X * t, a.Y + ab.Y * t);
            return (p - proj).Length;
        }
    }
}
