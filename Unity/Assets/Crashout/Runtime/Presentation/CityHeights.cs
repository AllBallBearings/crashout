namespace Crashout.Presentation
{
    /// <summary>
    /// Building heights for the downtown board, in metres. The board is seen from round a table at
    /// 1:32, where a seated viewer looks across it at roughly 23 degrees, so every metre of height
    /// hides more than two metres of road behind it. These profiles trade skyline for sightlines.
    /// </summary>
    public sealed class CityHeights
    {
        public string Name;
        /// <summary>The four blocks on the junction's corners.</summary>
        public float[] Corner;
        /// <summary>The eight frontage blocks along the roads, two per quadrant.</summary>
        public float[] Frontage;
        /// <summary>The four blocks deep in each quadrant, against the board edge.</summary>
        public float[] Inner;
        /// <summary>North, east and west portal buildings where traffic leaves the board.</summary>
        public float[] Portals;
        /// <summary>The two blocks either side of the runway's arrival on the south edge.</summary>
        public float[] Gateway;
        /// <summary>Centre height of the skybridge over the arrival; zero leaves it out.</summary>
        public float SkybridgeY;
        public bool GlassTowers;

        /// <summary>The original skyline: mid-rise frontages and towers up to 36 m.</summary>
        public static readonly CityHeights Towers = new CityHeights
        {
            Name = "towers",
            Corner = new[] { 14f, 19f, 12.5f, 16.5f },
            Frontage = new[] { 15f, 11.5f, 18f, 13f, 12f, 16.5f, 14f, 10.5f },
            Inner = new[] { 22f, 36f, 27f, 18f },
            Portals = new[] { 19f, 16f, 21f },
            Gateway = new[] { 24f, 20.5f },
            SkybridgeY = 9.9f,
            GlassTowers = true
        };

        /// <summary>Concept A: a three-storey town. Everything capped near 10–13 m.</summary>
        public static readonly CityHeights ThreeStorey = new CityHeights
        {
            Name = "three-storey",
            Corner = new[] { 8.5f, 10f, 8f, 9.5f },
            Frontage = new[] { 10f, 8.5f, 11f, 9f, 8.5f, 10.5f, 9.5f, 8f },
            Inner = new[] { 12f, 13f, 12.5f, 11.5f },
            Portals = new[] { 9.5f, 9f, 10f },
            Gateway = new[] { 11.5f, 10.5f },
            SkybridgeY = 7.6f,
            GlassTowers = false
        };

        /// <summary>Concept B, the board in use: single-storey shops on the corners, two storeys behind, nothing over the roads.</summary>
        public static readonly CityHeights MainStreet = new CityHeights
        {
            Name = "main-street",
            Corner = new[] { 4.6f, 5f, 4.6f, 5f },
            Frontage = new[] { 7f, 6.5f, 7.5f, 7f, 6.5f, 7.5f, 7f, 6.5f },
            Inner = new[] { 10f, 10.5f, 10f, 9.5f },
            Portals = new[] { 8f, 8f, 8f },
            Gateway = new[] { 8.5f, 8.5f },
            SkybridgeY = 0f,
            GlassTowers = false
        };

        /// <summary>Concept C: a stepped bowl. Low at the junction, rising to mid-rise at the board edge.</summary>
        public static readonly CityHeights SteppedBowl = new CityHeights
        {
            Name = "stepped-bowl",
            Corner = new[] { 4.6f, 5f, 4.6f, 5f },
            Frontage = new[] { 8f, 7f, 8.5f, 7.5f, 7f, 8.5f, 8f, 7f },
            Inner = new[] { 15f, 17f, 16f, 14f },
            Portals = new[] { 10f, 9.5f, 10.5f },
            Gateway = new[] { 11f, 10f },
            SkybridgeY = 7.6f,
            GlassTowers = true
        };

        public static readonly CityHeights[] Concepts = { Towers, ThreeStorey, MainStreet, SteppedBowl };

        /// <summary>The profile the next board build uses.</summary>
        public static CityHeights Active = MainStreet;
    }
}
