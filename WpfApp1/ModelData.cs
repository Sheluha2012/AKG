using System.Numerics;

namespace WpfApp1
{
    public struct Face
    {
        public int V1, V2, V3;

        public Face(int v1, int v2, int v3)
        {
            V1 = v1;
            V2 = v2;
            V3 = v3;
        }
    }

    public class ModelData
    {
        public Vector3[] Vertices;
        public Face[] Faces;
    }
}