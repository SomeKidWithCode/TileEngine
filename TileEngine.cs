using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Linq;

public class TileEngine : MonoBehaviour
{
    public GameObject TilePrefabIn;
    public static GameObject TilePrefab;
    public string levelToRender = "";
    private string lTRBackStore = "";
    public static Dictionary<string, Texture2D> Atli = new();
    public static Tile[,] GameMap = new Tile[Level.StaticHeight, Level.StaticWidth];
    public static Dictionary<string, Sprite> CachedSprs = new();
    internal List<Level> LevelDesigns = new();

    void Start()
    {
        // Get the required game object components
        TilePrefab = 
            ((TileEngine)GameObject
            .Find("LevelEngine")
            .GetComponent("TileEngine"))
            .TilePrefabIn;

        // Load texture atli (Atlases? Atlante?)
        string[] AtlasPaths = Directory.GetFiles(Util.PathDirectory(@"Assets\Resources\Atli"), "*.png");
        foreach (string AtlasPath in AtlasPaths)
            Atli.Add(Path.GetFileNameWithoutExtension(AtlasPath), (Texture2D)Resources.Load(Path.Combine("Atli", Path.GetFileNameWithoutExtension(AtlasPath))));

        // Initalize static tiles
        int rowIndex = 0;
        for (int i = 0; i < Level.StaticWidth * Level.StaticHeight; i++)
        {
            if (i != 0 && i % Level.StaticWidth == 0)
                rowIndex++;

            GameMap[rowIndex, i - (rowIndex * Level.StaticWidth)] = new Tile(
                i - (rowIndex * Level.StaticWidth),
                rowIndex
            );
        }

        // Load maps
        string[] MapPaths = Directory.GetFiles(Util.PathDirectory(@"Assets\Maps"), "*.txt");
        foreach (string MapPath in MapPaths)
            LevelDesigns.Add(new Level(
                Path.GetFileNameWithoutExtension(MapPath),
                File.ReadAllLines(MapPath).Where(Str => !string.IsNullOrWhiteSpace(Str)).ToArray()
            ));
    }

    public void SetLevel(string levelName)
    {
        Level design = LevelDesigns.Find(levelDesign => levelDesign.Name == levelName);

        if (design == null)
            throw new Exception("No map with that name exists");

        LevelDesigns.ForEach(levelDesign => levelDesign.Active = levelDesign == design);

        Structures.PlaceAt(Structures.Multiblocks.Pillar, 0, 0);
    }

    private void Update()
    {
        if (levelToRender != lTRBackStore)
        {
            SetLevel(levelToRender);
            lTRBackStore = levelToRender;
        }
    }

    public class Tile
    {
        public bool IsSolid;
        public Vector2 Position;
        public List<(GameObject, SpriteRenderer)> Layers = new();

        public Tile(int X, int Y)
        {
            Position = new(X, -Y);
            SetLayerHeight(1);
        }

        private (GameObject, SpriteRenderer) CreateNewLayer()
        {
            GameObject GO = Instantiate(TilePrefab);
            Transform T = (Transform)GO.GetComponent("Transform");
            if (T)
                T.position = Position;
            else
                Debug.LogError("Unable to find Transform component of new layer");
            SpriteRenderer SR = (SpriteRenderer)GO.GetComponent("SpriteRenderer");
            if (!SR)
                Debug.LogError("Unable to find SpriteRender component of new layer");
            return (GO, SR);
        }

        private void SetLayerHeight(int Height)
        {
            if (Height <= 0)
                throw new Exception("Cannot set layer height to or below 0");
            while (Layers.Count > Height)
            {
                Destroy(Layers[^1].Item1);
                Layers.RemoveAt(Layers.Count - 1);
            }
            while (Layers.Count < Height)
                Layers.Add(CreateNewLayer());
        }

        /// <summary>
        /// Sets all layers of the tile.
        /// The first sprite in the array will always be the base tile, with each consecutive sprites stacking on top.
        /// </summary>
        /// <param name="texes"></param>
        public void SetTexes(List<Sprite> Sprs)
        {
            SetLayerHeight(Sprs.Count);
            for (int i = 0; i < Sprs.Count; i++)
                Layers[i].Item2.sprite = Sprs[i];
        }
    }

    public class Level
    {
        public string Name;
        public string[] LevelDesign;

        private bool _IsActive = false;
        public static readonly byte StaticWidth = 10;
        public static readonly byte StaticHeight = 10;

        public Level(string name, string[] Tiles)
        {
            if (Tiles.Length != StaticWidth * StaticHeight)
                throw new Exception("Level: Invalid level design length");

            Name = name;
            LevelDesign = Tiles;
        }

        public bool Active
        {
            get => _IsActive;
            set
            {
                if (value)
                    for (int i = 0; i < StaticHeight; i++)
                        for (int j = 0; j < StaticWidth; j++)
                            GameMap[i, j].SetTexes(Util.GetSprites(LevelDesign[j + i * StaticHeight]));
                _IsActive = value;
            }
        }
    }

    public class Util
    {
        public static Rect GetTexCoord(Vector2 vector) => new(vector.x * 16, vector.y * 16, 16, 16);
        public static Sprite GetSprite(string AtlasName, Vector2 Coord)
        {
            if (CachedSprs.TryGetValue($"{AtlasName},{Coord.x},{Coord.y}", out Sprite Spr))
                return Spr;
            else
            {
                if (Atli.TryGetValue(AtlasName, out Texture2D tex))
                {
                    Rect r = GetTexCoord(Coord);
                    r.y = tex.height - 16 - r.y;

                    Sprite sprite = Sprite.Create(
                        tex,
                        r,
                        new Vector2(0, 0),
                        16
                    );
                    CachedSprs.Add($"{AtlasName},{Coord.x},{Coord.y}", sprite);
                    return sprite;
                }
                else
                    throw new Exception($"GetSprite: Unable to find Atlas with name: {AtlasName}");
            }
        }
        public static List<Sprite> GetSprites(string SprPath)
        {
            string[] Parts = SprPath.Split(":");

            if (Parts.Length <= 2 || Parts.Length % 2 == 0)
                throw new Exception($"GetSprite: Invalid tile path thingy: {Parts.Length}");

            List<Sprite> Out = new();

            for (int i = 0, len = Parts.Length - 1; i < len; i += 2)
            {
                string[] strsA = Parts[i + 1].Split(",");
                Vector2 vA = new(int.Parse(strsA[0]), int.Parse(strsA[1]));

                Out.Add(GetSprite(Parts[i], vA));
            }

            return Out;
        }
        public static Tile GetTileAtPos(int x, int y) => GameMap[y, x];
        public static Tile GetTileAtPos(Vector2 vec2) => GetTileAtPos((int)vec2.x, (int)vec2.y);
        public static string PathDirectory(string SubPath) => Path.Combine(Environment.CurrentDirectory, SubPath);
    }
}

public class Structures
{
    public enum Multiblocks
    {
        NormalBigHouseOne,
        NormalBigHouseTwo,
        NormalBigHouseThree,

        NormalHouseOne,
        NormalHouseTwo,
        NormalHouseThree,

        Pillar,
        Mirror,
        ArenaT1,
        ArenaT2
    }

    /// <summary>
    /// Spawns structures using extremely hacky methods
    /// </summary>
    /// <param name="struc">The type of structure to place</param>
    /// <param name="x">X-axis position of the left of the structure</param>
    /// <param name="y">Y-axis position of the top of the structure</param>
    public static void PlaceAt(Multiblocks struc, int x, int y)
    {
        if (x < 0 || y < 0 || x > TileEngine.Level.StaticWidth || y > TileEngine.Level.StaticHeight)
            throw new Exception("Structures: Cannot place structure outside of world");

        // I apologise in advance to micah.
        // It's the only way I could think of in such little time

        Vector2 initPos = new(0, 0);
        int width;
        int height;
        string atlas;

        switch (struc)
        {
            case Multiblocks.NormalBigHouseOne:
                initPos = new(0, 18);
                width = 7;
                height = 7;
                atlas = "Atlas1";
                break;
            case Multiblocks.NormalBigHouseTwo:
                initPos = new(7, 18);
                width = 7;
                height = 7;
                atlas = "Atlas1";
                break;
            case Multiblocks.NormalBigHouseThree:
                initPos = new(14, 18);
                width = 7;
                height = 7;
                atlas = "Atlas1";
                break;
            case Multiblocks.NormalHouseOne:
                initPos = new(0, 18);
                width = 7;
                height = 6;
                atlas = "Atlas1";
                break;
            case Multiblocks.NormalHouseTwo:
                initPos = new(7, 18);
                width = 7;
                height = 6;
                atlas = "Atlas1";
                break;
            case Multiblocks.NormalHouseThree:
                initPos = new(14, 18);
                width = 7;
                height = 6;
                atlas = "Atlas1";
                break;
            case Multiblocks.Pillar:
                initPos = new(0, 0);
                width = 3;
                height = 5;
                atlas = "pillar";
                break;


            default:
                throw new Exception("Shutup thingy thing");
        }

        for (int j = 0; j < height; j++)
            for (int i = 0; i < width; i++)
            {
                TileEngine.Tile t = TileEngine.Util.GetTileAtPos(x + i, y + j);

                // paths of the current sprites
                List<string> paths = t.Layers.Select(a => TileEngine.CachedSprs.FirstOrDefault(x => x.Value == a.Item2.sprite).Key).ToList();

                Sprite s = TileEngine.Util.GetSprite(atlas, initPos + new Vector2(i, j));

                paths.Add(TileEngine.CachedSprs.FirstOrDefault(x => x.Value == s).Key);

                paths = paths.Select(s =>
                {
                    string[] parts = s.Split(",");
                    return $"{parts[0]}:{parts[1]},{parts[2]}";
                }).ToList();

                t.SetTexes(TileEngine.Util.GetSprites(string.Join(":", paths) + ":T"));
            }
    }
}
