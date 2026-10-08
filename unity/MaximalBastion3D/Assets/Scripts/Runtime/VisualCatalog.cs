using System;
using UnityEngine;

namespace Bastion3D
{
    [CreateAssetMenu(menuName = "Bastion/Visual catalog")]
    public sealed class VisualCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string id;
            public GameObject prefab;
            public Color energy = Color.cyan;
            public float aimHeight = 1;
        }
        public Entry[] towers = Array.Empty<Entry>();
        public Entry[] enemies = Array.Empty<Entry>();
        public Entry[] environment = Array.Empty<Entry>();
        public Entry[] tactics = Array.Empty<Entry>();
        public Entry[] enemyModules = Array.Empty<Entry>();
        public Material ground, road, platform, steel, trim, energy, copper, laneLights;
        public GameObject dressing;
        public Font interfaceFont;
        public Entry Find(string id)
        {
            foreach (var e in towers) if (e.id == id) return e;
            foreach (var e in enemies) if (e.id == id) return e;
            foreach (var e in environment) if (e.id == id) return e;
            foreach (var e in tactics) if (e.id == id) return e;
            foreach (var e in enemyModules) if (e.id == id) return e;
            return null;
        }
    }

    public static class Coordinates
    {
        public const float Scale = .05f;
        public static Vector3 World(MaximalBastion.Gameplay.Vector2 p, float y = .2f) =>
            new Vector3((p.X - 480) * Scale, y, (p.Y - 360) * Scale);
        public static MaximalBastion.Gameplay.Vector2 Logical(Vector3 p) =>
            new MaximalBastion.Gameplay.Vector2(p.x / Scale + 480, p.z / Scale + 360);
        public static Color Color(MaximalBastion.Gameplay.Color c) => new Color(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
    }
}
