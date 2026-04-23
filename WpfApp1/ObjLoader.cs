using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace WpfApp1
{
    public static class ObjLoader
    {
        public static ModelData Load(string filePath)
        {
            var vertices = new List<Vector3>();
            var fileNormals = new List<Vector3>();
            var texCoords = new List<Vector2>();
            var faces = new List<Face>();
            var culture = CultureInfo.InvariantCulture;

            foreach (string line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                switch (parts[0])
                {
                    case "v":
                        vertices.Add(new Vector3(float.Parse(parts[1], culture), float.Parse(parts[2], culture), float.Parse(parts[3], culture)));
                        break;
                    case "vt":
                        texCoords.Add(new Vector2(float.Parse(parts[1], culture), float.Parse(parts[2], culture)));
                        break;
                    case "vn":
                        fileNormals.Add(Vector3.Normalize(new Vector3(float.Parse(parts[1], culture), float.Parse(parts[2], culture), float.Parse(parts[3], culture))));
                        break;
                    case "f":
                        var t1 = ParseObjToken(parts[1]);
                        var t2 = ParseObjToken(parts[2]);
                        var t3 = ParseObjToken(parts[3]);
                        faces.Add(new Face(t1.v, t2.v, t3.v, t1.n, t2.n, t3.n, t1.t, t2.t, t3.t));
                        if (parts.Length > 4)
                        {
                            var t4 = ParseObjToken(parts[4]);
                            faces.Add(new Face(t1.v, t3.v, t4.v, t1.n, t3.n, t4.n, t1.t, t3.t, t4.t));
                        }
                        break;
                }
            }

            return new ModelData
            {
                Vertices = vertices.ToArray(),
                Normals = fileNormals.ToArray(),
                TexCoords = texCoords.ToArray(),
                Faces = faces.ToArray()
            };
        }

        private static (int v, int t, int n) ParseObjToken(string token)
        {
            string[] p = token.Split('/');
            int v = int.Parse(p[0]) - 1;
            int t = (p.Length > 1 && p[1] != "") ? int.Parse(p[1]) - 1 : 0;
            int n = (p.Length > 2 && p[2] != "") ? int.Parse(p[2]) - 1 : 0;
            return (v, t, n);
        }
    }
}