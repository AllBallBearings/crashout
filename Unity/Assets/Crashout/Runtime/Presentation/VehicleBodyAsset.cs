using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashout.Presentation
{
    /// <summary>Finished body meshes exported from the vehicle authoring tools. Metres, Y up, +Z forward.</summary>
    public sealed class VehicleBodyAsset
    {
        public sealed class Section
        {
            public string Part, Material;
            public Vector3[] Vertices, Normals;
            public Vector2[] Uvs;
            public int[] Triangles;

            public Mesh CreateMesh()
            {
                var mesh = new Mesh { name = Part + " / " + Material,
                    indexFormat = Vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.vertices = Vertices; mesh.normals = Normals; mesh.uv = Uvs; mesh.triangles = Triangles;
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                return mesh;
            }
        }

        private static readonly Dictionary<string, VehicleBodyAsset> Cache = new Dictionary<string, VehicleBodyAsset>();
        public float Ground, HalfLength, WheelRadius, Track, TyreWidth;
        public Vector3 CabinCentre, CabinSize;
        public Section[] Sections;

        public static VehicleBodyAsset Load(string name)
        {
            if (Cache.TryGetValue(name, out VehicleBodyAsset body)) return body;
            TextAsset resource = Resources.Load<TextAsset>("Vehicles/" + name);
            if (resource == null) return null;
            body = Read(resource.bytes); Cache.Add(name, body);
            Resources.UnloadAsset(resource);
            return body;
        }

        public static VehicleBodyAsset Read(byte[] bytes)
        {
            using var reader = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8);
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "COV2") throw new InvalidDataException("Unknown vehicle mesh format.");
            Vector3 V3() => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            string Text()
            {
                int size = reader.ReadInt32();
                if (size < 1 || size > 256) throw new InvalidDataException("Invalid vehicle section name.");
                return Encoding.UTF8.GetString(reader.ReadBytes(size));
            }
            var body = new VehicleBodyAsset { Ground = reader.ReadSingle(), HalfLength = reader.ReadSingle(),
                WheelRadius = reader.ReadSingle(), Track = reader.ReadSingle(), TyreWidth = reader.ReadSingle(),
                CabinCentre = V3(), CabinSize = V3() };
            int count = reader.ReadInt32();
            if (count < 1 || count > 128) throw new InvalidDataException("Invalid vehicle section count.");
            body.Sections = new Section[count];
            for (int i = 0; i < count; i++)
            {
                var section = new Section { Part = Text(), Material = Text() };
                int vertices = reader.ReadInt32(), indices = reader.ReadInt32();
                if (vertices < 3 || vertices > 200000 || indices < 3 || indices > 1200000 || indices % 3 != 0)
                    throw new InvalidDataException("Invalid vehicle mesh size.");
                section.Vertices = new Vector3[vertices]; section.Normals = new Vector3[vertices]; section.Uvs = new Vector2[vertices];
                for (int v = 0; v < vertices; v++)
                {
                    section.Vertices[v] = V3(); section.Normals[v] = V3();
                    section.Uvs[v] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                }
                section.Triangles = new int[indices];
                for (int t = 0; t < indices; t++)
                {
                    int index = reader.ReadInt32();
                    if (index < 0 || index >= vertices) throw new InvalidDataException("Vehicle mesh index out of range.");
                    section.Triangles[t] = index;
                }
                body.Sections[i] = section;
            }
            if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Unexpected vehicle mesh data.");
            return body;
        }
    }
}
