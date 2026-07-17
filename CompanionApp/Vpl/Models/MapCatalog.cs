using System.Collections.Generic;

namespace CarthaBotVPL.Models
{
    /// <summary>One selectable playground for the simulator.</summary>
    public class SimMap
    {
        public string Id { get; init; }
        public string Glyph { get; init; }       // emoji on the map-picker chip
        public string NameKey { get; init; }      // localized name (vpl* resource key)
        public string WorldObj { get; init; }     // floor/track OBJ in Vpl\Assets
        public string Manifest { get; init; }     // matching JSON manifest

        public static readonly IReadOnlyList<SimMap> All = new[]
        {
            new SimMap { Id = "classic",   Glyph = "🟢", NameKey = "vplMapClassic",
                         WorldObj = "carthabot_simworld.obj", Manifest = "carthabot_simworld.json" },
            new SimMap { Id = "adventure", Glyph = "🏕", NameKey = "vplMapAdventure",
                         WorldObj = "carthabot_map_adventure.obj", Manifest = "carthabot_map_adventure.json" },
            new SimMap { Id = "city",      Glyph = "🏙", NameKey = "vplMapCity",
                         WorldObj = "carthabot_map_city.obj", Manifest = "carthabot_map_city.json" },
            new SimMap { Id = "garden",    Glyph = "🌷", NameKey = "vplMapGarden",
                         WorldObj = "carthabot_map_garden.obj", Manifest = "carthabot_map_garden.json" },
            new SimMap { Id = "space",     Glyph = "🚀", NameKey = "vplMapSpace",
                         WorldObj = "carthabot_map_space.obj", Manifest = "carthabot_map_space.json" },
            new SimMap { Id = "ocean",     Glyph = "🌊", NameKey = "vplMapOcean",
                         WorldObj = "carthabot_map_ocean.obj", Manifest = "carthabot_map_ocean.json" },
            new SimMap { Id = "snow",      Glyph = "❄", NameKey = "vplMapSnow",
                         WorldObj = "carthabot_map_snow.obj", Manifest = "carthabot_map_snow.json" },
        };
    }
}
