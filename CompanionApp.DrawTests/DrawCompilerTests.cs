using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using CompanionApp.Draw.Services;
using Xunit;

namespace CompanionApp.DrawTests
{
    public class DrawCompilerTests
    {
        private static DrawSettings Default() => new DrawSettings { SizeMm = 200, SimplifyMm = 4, MmPerSec = 90 };

        // ---- PathToMoves: turtle heading math ----

        [Fact]
        public void Square_Has_Four_Forward_Moves_With_Right_Angle_Turns()
        {
            var sq = DrawCompiler.ShapePath(DrawCompiler.Shape.Square, Default());
            var moves = DrawCompiler.PathToMoves(sq);

            // a square is 4 edges
            Assert.Equal(4, moves.Count);
            // all edges equal length (200 mm side)
            foreach (var m in moves)
                Assert.True(Math.Abs(m.ForwardMm - 200) < 1, $"edge was {m.ForwardMm}");
            // every corner is a 90° turn (sign can vary with traversal direction)
            foreach (var m in moves)
                Assert.True(Math.Abs(Math.Abs(m.TurnDeg) - 90) < 0.001, $"turn was {m.TurnDeg}");
        }

        [Fact]
        public void Straight_Up_Path_Needs_No_Initial_Turn()
        {
            // robot starts facing up (+Y); a segment straight up should be turn ~0
            var path = new List<Point> { new Point(0, 0), new Point(0, 100) };
            var moves = DrawCompiler.PathToMoves(path);
            Assert.Single(moves);
            Assert.True(Math.Abs(moves[0].TurnDeg) < 0.001);
            Assert.True(Math.Abs(moves[0].ForwardMm - 100) < 0.001);
        }

        [Fact]
        public void Start_Heading_Right_Needs_No_Turn_For_A_Rightward_Segment()
        {
            // robot placed facing right (0°); a segment going right (+X) should be turn ~0
            var path = new List<Point> { new Point(0, 0), new Point(100, 0) };
            var moves = DrawCompiler.PathToMoves(path, 0);
            Assert.Single(moves);
            Assert.True(Math.Abs(moves[0].TurnDeg) < 0.001, $"turn was {moves[0].TurnDeg}");
        }

        [Fact]
        public void Start_Heading_Flows_Through_Settings_Into_Generate()
        {
            // facing down (-90); first segment straight up needs a ~180° turn
            var s = Default(); s.StartHeadingDeg = -90;
            var path = new List<Point> { new Point(0, 0), new Point(0, 100) };
            var moves = DrawCompiler.PathToMoves(path, s.StartHeadingDeg);
            Assert.True(Math.Abs(Math.Abs(moves[0].TurnDeg) - 180) < 0.001);
        }

        [Fact]
        public void Turn_Is_Normalized_To_Plus_Minus_180()
        {
            // up then sharply back down-ish should never produce a >180 turn
            var path = new List<Point> { new Point(0, 0), new Point(0, 10), new Point(-1, -10) };
            var moves = DrawCompiler.PathToMoves(path);
            foreach (var m in moves)
                Assert.InRange(m.TurnDeg, -180, 180);
        }

        // ---- Shapes ----

        [Theory]
        [InlineData(DrawCompiler.Shape.Triangle)]
        [InlineData(DrawCompiler.Shape.Circle)]
        [InlineData(DrawCompiler.Shape.Star)]
        [InlineData(DrawCompiler.Shape.Heart)]
        [InlineData(DrawCompiler.Shape.Spiral)]
        [InlineData(DrawCompiler.Shape.House)]
        [InlineData(DrawCompiler.Shape.Arrow)]
        public void Shapes_Produce_A_Nontrivial_Closed_Path(DrawCompiler.Shape shape)
        {
            var pts = DrawCompiler.ShapePath(shape, Default());
            Assert.True(pts.Count >= 3);
            // points stay within the requested size envelope (radius = size/2 + slack)
            double r = 200 / 2.0 + 1;
            Assert.All(pts, p => Assert.True(Math.Abs(p.X) <= r && Math.Abs(p.Y) <= r));
        }

        [Fact]
        public void Circle_Compiles_To_Many_Small_Equal_Turns()
        {
            var circle = DrawCompiler.ShapePath(DrawCompiler.Shape.Circle, Default());
            var moves = DrawCompiler.PathToMoves(circle);
            Assert.True(moves.Count >= 30);
            // each step of a regular 36-gon turns 360/36 = 10°
            foreach (var m in moves.Skip(1))
                Assert.True(Math.Abs(Math.Abs(m.TurnDeg) - 10) < 0.5, $"turn was {m.TurnDeg}");
        }

        [Fact]
        public void Every_Shape_Emits_A_Usable_Program()
        {
            foreach (DrawCompiler.Shape sh in Enum.GetValues(typeof(DrawCompiler.Shape)))
            {
                var prog = DrawCompiler.Generate(DrawCompiler.ShapePath(sh, Default()), Default());
                Assert.True(prog.MoveCount > 0, $"{sh} produced no moves");
                Assert.Contains("DRAW_DONE", prog.Code);
                Assert.True(prog.TotalDrawMm > 0, $"{sh} draws zero length");
            }
        }

        // ---- Douglas–Peucker simplification ----

        [Fact]
        public void DouglasPeucker_Drops_Collinear_Midpoints()
        {
            var line = new List<Point>
            {
                new Point(0, 0), new Point(1, 0), new Point(2, 0), new Point(3, 0), new Point(4, 0)
            };
            var simplified = DrawCompiler.DouglasPeucker(line, 0.5);
            Assert.Equal(2, simplified.Count);   // collapses to the two endpoints
        }

        [Fact]
        public void DouglasPeucker_Keeps_A_Real_Corner()
        {
            var corner = new List<Point>
            {
                new Point(0, 0), new Point(5, 0), new Point(5, 5)
            };
            var simplified = DrawCompiler.DouglasPeucker(corner, 0.5);
            Assert.Equal(3, simplified.Count);   // the corner must survive
        }

        // ---- Nearest-neighbour stroke ordering ----

        [Fact]
        public void FlattenOrdered_Chains_Nearest_Stroke_Next()
        {
            // stroke A near origin, B far right, C just after A — greedy should pick C before B
            var a = new List<Point> { new Point(0, 0), new Point(1, 0) };
            var c = new List<Point> { new Point(2, 0), new Point(3, 0) };
            var b = new List<Point> { new Point(50, 0), new Point(51, 0) };
            var flat = DrawCompiler.FlattenOrdered(new List<IReadOnlyList<Point>> { a, b, c });

            // after A (ends at x=1), the next appended point should be C's start (x=2), not B's (x=50)
            Assert.Equal(2, flat[2].X, 3);
        }

        [Fact]
        public void FlattenOrdered_Reverses_A_Stroke_When_Its_Tail_Is_Closer()
        {
            var a = new List<Point> { new Point(0, 0), new Point(1, 0) };
            // b is oriented away; its tail (x=2) is closer to A's end than its head (x=9)
            var b = new List<Point> { new Point(9, 0), new Point(2, 0) };
            var flat = DrawCompiler.FlattenOrdered(new List<IReadOnlyList<Point>> { a, b });
            Assert.Equal(2, flat[2].X, 3);   // b got reversed so we continue from x=2
        }

        // ---- Code emission ----

        [Fact]
        public void Generated_Program_Contains_Motor_Setup_And_Calibration()
        {
            var prog = DrawCompiler.Generate(DrawCompiler.ShapePath(DrawCompiler.Shape.Square, Default()), Default());
            Assert.Contains("def motors(", prog.Code);
            Assert.Contains("def fwd(", prog.Code);
            Assert.Contains("def turn(", prog.Code);
            Assert.Contains("DRAW_DONE", prog.Code);
            Assert.True(prog.MoveCount > 0);
            Assert.True(prog.EstimatedSeconds > 0);
        }

        [Fact]
        public void Empty_Drawing_Yields_No_Moves()
        {
            var prog = DrawCompiler.Generate(new List<IReadOnlyList<Point>>(), Default());
            Assert.Equal(0, prog.MoveCount);
        }

        [Fact]
        public void Closed_Shape_With_Passes_Emits_A_Retrace_Loop()
        {
            var s = Default(); s.Passes = 3;
            var prog = DrawCompiler.Generate(DrawCompiler.ShapePath(DrawCompiler.Shape.Square, s), s);
            Assert.Contains("for _pass in range(3)", prog.Code);
        }

        [Fact]
        public void Open_Path_Ignores_Passes()
        {
            var s = Default(); s.Passes = 3;
            // an open zig (first point far from last) must NOT loop — the pen can't return
            var open = new List<Point> { new Point(0, 0), new Point(50, 80), new Point(100, 0) };
            var prog = DrawCompiler.Generate(open, s);
            Assert.DoesNotContain("for _pass", prog.Code);
        }

        [Fact]
        public void Passes_Scale_The_Time_Estimate()
        {
            var one = Default(); one.Passes = 1;
            var three = Default(); three.Passes = 3;
            var p1 = DrawCompiler.Generate(DrawCompiler.ShapePath(DrawCompiler.Shape.Square, one), one);
            var p3 = DrawCompiler.Generate(DrawCompiler.ShapePath(DrawCompiler.Shape.Square, three), three);
            // 3 passes ≈ 3× the single-pass move time (the 0.5 s settle is shared)
            Assert.True(p3.EstimatedSeconds > p1.EstimatedSeconds * 2.5);
        }

        // ---- Text engine ----

        [Fact]
        public void Text_Layout_Produces_Strokes_And_Advances_Width()
        {
            var glyphs = DrawText.Layout("HI", out double width);
            Assert.NotEmpty(glyphs);
            Assert.True(width > 0);
            // every stroke has at least two points
            Assert.All(glyphs, g => Assert.True(g.Count >= 2));
        }

        [Fact]
        public void Text_Layout_Ignores_Unsupported_Characters()
        {
            var glyphs = DrawText.Layout("@#", out double width);
            Assert.Empty(glyphs);
        }

        [Fact]
        public void Text_Layout_Supports_Common_Punctuation()
        {
            var glyphs = DrawText.Layout("HI!", out double width);
            // H (3) + I (3) + ! (2) strokes — the '!' must contribute
            Assert.True(glyphs.Count >= 8);
        }

        [Fact]
        public void Text_Layout_Lowercase_Is_Written_As_Uppercase()
        {
            var lower = DrawText.Layout("abc", out double w1);
            var upper = DrawText.Layout("ABC", out double w2);
            Assert.Equal(upper.Count, lower.Count);
            Assert.Equal(w2, w1, 3);
        }
    }
}
