using System.Numerics;

namespace WpfApp1
{
    public struct Face
    {
        public int V1, V2, V3;   
        public int N1, N2, N3;   

        public Face(int v1, int v2, int v3, int n1, int n2, int n3)
        {
            V1 = v1; V2 = v2; V3 = v3;
            N1 = n1; N2 = n2; N3 = n3;
        }
    }

    public class ModelData
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;   
        public Face[] Faces;
    }
}