using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using WpfApp1;

namespace WpfApp1
{
    public static class ObjLoader
    {
        public static ModelData Load(string filePath)
        {
            var vertices = new List<Vector3>();
            var faces = new List<Face>();
            var culture = CultureInfo.InvariantCulture;

            foreach (string line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                ReadOnlySpan<char> span = line.AsMemory().Span.Trim();
                if (span.IsEmpty || span[0] == '#') continue;

                if (span.StartsWith("v "))
                {
                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        vertices.Add(new Vector3(
                            float.Parse(parts[1], culture),
                            float.Parse(parts[2], culture),
                            float.Parse(parts[3], culture)
                        ));
                    }
                }
                else if (span.StartsWith("f "))
                {
                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        int i1 = ParseObjIndex(parts[1]);
                        int i2 = ParseObjIndex(parts[2]);
                        int i3 = ParseObjIndex(parts[3]);
                        faces.Add(new Face(i1, i2, i3));

                        if (parts.Length >= 5)
                        {
                            int i4 = ParseObjIndex(parts[4]);
                            faces.Add(new Face(i1, i3, i4));
                        }
                    }
                }
            }

            return new ModelData
            {
                Vertices = vertices.ToArray(),
                Faces = faces.ToArray()
            };
        }

        private static int ParseObjIndex(string part)
        {
            int slashIndex = part.IndexOf('/');
            if (slashIndex != -1)
            {
                part = part.Substring(0, slashIndex);
            }

            int index = int.Parse(part);
            return index > 0 ? index - 1 : 0;
        }
    }
}