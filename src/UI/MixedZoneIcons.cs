using System.Collections.Generic;
using ColossalFramework;
using ColossalFramework.UI;
using MixedZoning.Config;
using MixedZoning.Zones;
using UnityEngine;

namespace MixedZoning.UI
{
    /// <summary>
    /// Builds the zoning-panel icons once per game session from each zone's fixed checkerboard
    /// (see MixedZonePatterns). They never change with the configuration. No image files ship
    /// with the mod.
    /// </summary>
    internal static class MixedZoneIcons
    {
        private const int Width = 109;   // vanilla thumbnail size
        private const int Height = 100;
        private const int Cell = 18;     // icon pixels per checker square
        private const int Border = 3;

        private static UITextureAtlas atlas;

        public static string SpriteName(int slot) => "MixedZone" + (slot + 1);

        /// <summary>Zoning brush colour: the zone's two checker colours blended.</summary>
        public static Color BrushColor(int slot)
        {
            Color c = Color.Lerp(ZoneColor(MixedZonePatterns.First(slot)), ZoneColor(MixedZonePatterns.Second(slot)), 0.5f);
            c.a = 1f;
            return c;
        }

        /// <summary>Atlas holding every slot's sprite and its Focused/Hovered/Pressed/Disabled variants.</summary>
        public static UITextureAtlas Atlas
        {
            get
            {
                if (atlas == null) Build();
                return atlas;
            }
        }

        public static Color ZoneColor(ItemClass.Zone zone) => MixedZonePatterns.ZoneColor(zone);

        public static void Destroy()
        {
            if (atlas != null)
            {
                foreach (var sprite in atlas.sprites)
                {
                    if (sprite.texture != null) Object.Destroy(sprite.texture);
                }
                Object.Destroy(atlas.material.mainTexture);
                Object.Destroy(atlas.material);
                Object.Destroy(atlas);
                atlas = null;
            }
        }

        private static void Build()
        {
            var names = new List<string>();
            var textures = new List<Texture2D>();
            for (int slot = 0; slot < MixedZones.Count; slot++)
            {
                string baseName = SpriteName(slot);
                Add(names, textures, baseName,              Draw(slot, 0.85f, false, false));
                Add(names, textures, baseName + "Hovered",  Draw(slot, 1.00f, false, false));
                Add(names, textures, baseName + "Focused",  Draw(slot, 1.00f, true,  false));
                Add(names, textures, baseName + "Pressed",  Draw(slot, 0.70f, true,  false));
                Add(names, textures, baseName + "Disabled", Draw(slot, 0.85f, false, true));
            }

            var packed = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            Rect[] regions = packed.PackTextures(textures.ToArray(), 2, 2048);

            Material material = Object.Instantiate(UIView.GetAView().defaultAtlas.material);
            material.mainTexture = packed;

            atlas = ScriptableObject.CreateInstance<UITextureAtlas>();
            atlas.name = "MixedZoningIcons";
            atlas.material = material;
            for (int i = 0; i < names.Count; i++)
            {
                atlas.AddSprite(new UITextureAtlas.SpriteInfo
                {
                    name = names[i],
                    texture = textures[i],
                    region = regions[i],
                });
            }
        }

        private static void Add(List<string> names, List<Texture2D> textures, string name, Texture2D tex)
        {
            names.Add(name);
            textures.Add(tex);
        }

        private static Texture2D Draw(int slot, float brightness, bool highlight, bool disabled)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.ARGB32, false);
            var pixels = new Color[Width * Height];
            Color edge = highlight ? Color.white : new Color(0.1f, 0.1f, 0.1f, 1f);
            Color on = ZoneColor(MixedZonePatterns.First(slot)) * brightness;
            Color off = ZoneColor(MixedZonePatterns.Second(slot)) * brightness;
            Color digit = Color.white;
            on.a = off.a = 1f;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    bool isBorder = x < Border || y < Border || x >= Width - Border || y >= Height - Border;
                    Color c;
                    if (isBorder) c = edge;
                    else if (InDigit(slot + 1, x, y, out bool outline)) c = outline ? Color.black : digit;
                    else c = MixedZonePatterns.On(x / Cell, y / Cell) ? on : off;
                    if (disabled)
                    {
                        float g = c.grayscale * 0.6f;
                        c = new Color(g, g, g, 0.6f);
                    }
                    pixels[y * Width + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false);
            return tex;
        }

        // 3x5 bitmap digits 1..7, one row per string, '#' = filled.
        private static readonly string[][] Digits =
        {
            null,
            new[] { ".#.", "##.", ".#.", ".#.", "###" },
            new[] { "##.", "..#", ".#.", "#..", "###" },
            new[] { "##.", "..#", ".#.", "..#", "##." },
            new[] { "#.#", "#.#", "###", "..#", "..#" },
            new[] { "###", "#..", "##.", "..#", "##." },
            new[] { ".##", "#..", "##.", "#.#", ".#." },
            new[] { "###", "..#", ".#.", ".#.", ".#." },
        };

        private const int DigitScale = 11;   // pixels per bitmap dot

        /// <summary>
        /// Whether (x, y) falls on the centred number for this icon, with a 2px dark outline so it
        /// reads on any pattern. Texture y runs bottom-up, so rows are flipped.
        /// </summary>
        private static bool InDigit(int number, int x, int y, out bool outline)
        {
            outline = false;
            string[] rows = Digits[number];
            int w = 3 * DigitScale, h = 5 * DigitScale;
            int left = (Width - w) / 2, bottom = (Height - h) / 2;
            bool Filled(int px, int py)
            {
                int cx = px - left, cy = py - bottom;
                if (cx < 0 || cy < 0 || cx >= w || cy >= h) return false;
                return rows[4 - cy / DigitScale][cx / DigitScale] == '#';
            }
            if (Filled(x, y)) return true;
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    if (Filled(x + dx, y + dy)) { outline = true; return true; }
            return false;
        }
    }
}
