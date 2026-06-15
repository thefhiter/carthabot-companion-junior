using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace CarthaBotVPL.Models
{
    public enum MissionGoal
    {
        FreePlay,       // no goal — just drive around the playground
        GlowOnButton,   // make CarthaBot glow green while a button is pressed
        ReachFlag,      // drive to the checkered flag
        StopAtWall,     // stop in front of the obstacle without bumping it
        CollectCoins,   // pick up all three coins
        RainbowParty,   // reach the flag with the rainbow light show running
        FollowLine      // ride the black loop all the way around (the Thymio classic)
    }

    /// <summary>One guided challenge played inside the 3D simulator.</summary>
    public class Mission
    {
        public string Id { get; init; }
        public MissionGoal Goal { get; init; }
        public string Glyph { get; init; }          // big emoji on the mission card
        public string TitleKey { get; init; }       // vpl* resource keys (EN/FR)
        public string HintKey { get; init; }

        // scenario knobs for the simulator
        public bool ShowObstacle { get; init; } = true;
        public Point? ObstacleOverride { get; init; }
        public bool ShowCoins { get; init; }
        public bool ShowGoal { get; init; }

        public static readonly IReadOnlyList<Mission> All = new[]
        {
            new Mission
            {
                Id = "glow", Goal = MissionGoal.GlowOnButton, Glyph = "💡",
                TitleKey = "vplMisGlow", HintKey = "vplMisGlowHint",
                ShowObstacle = false
            },
            new Mission
            {
                Id = "flag", Goal = MissionGoal.ReachFlag, Glyph = "🏁",
                TitleKey = "vplMisFlag", HintKey = "vplMisFlagHint",
                ShowObstacle = false, ShowGoal = true
            },
            new Mission
            {
                Id = "wall", Goal = MissionGoal.StopAtWall, Glyph = "🛑",
                TitleKey = "vplMisWall", HintKey = "vplMisWallHint",
                // the brick sits on the robot's path, dead ahead of the start pad
                ObstacleOverride = new Point(2.5, -4.2)
            },
            new Mission
            {
                Id = "coins", Goal = MissionGoal.CollectCoins, Glyph = "💰",
                TitleKey = "vplMisCoins", HintKey = "vplMisCoinsHint",
                ShowObstacle = false, ShowCoins = true
            },
            new Mission
            {
                Id = "party", Goal = MissionGoal.RainbowParty, Glyph = "🌈",
                TitleKey = "vplMisParty", HintKey = "vplMisPartyHint",
                ShowObstacle = false, ShowGoal = true
            },
            new Mission
            {
                Id = "line", Goal = MissionGoal.FollowLine, Glyph = "➿",
                TitleKey = "vplMisLine", HintKey = "vplMisLineHint",
                ShowObstacle = false
            }
        };
    }

    /// <summary>Stars the child has earned, persisted per Windows user.</summary>
    public static class MissionProgress
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CarthaBot", "vpl_stars.json");

        private static HashSet<string> _done;

        private static HashSet<string> Done
        {
            get
            {
                if (_done == null)
                {
                    _done = new HashSet<string>();
                    try
                    {
                        if (File.Exists(FilePath))
                            _done = JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(FilePath))
                                    ?? new HashSet<string>();
                    }
                    catch { /* fresh start */ }
                }
                return _done;
            }
        }

        public static bool IsDone(string missionId) => Done.Contains(missionId);

        public static void MarkDone(string missionId)
        {
            if (!Done.Add(missionId)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonSerializer.Serialize(Done));
            }
            catch { /* stars are nice-to-have; never crash for them */ }
        }
    }
}
