using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private WriteableBitmap _bitmap;
        private int _width;
        private int _height;
        private ModelData _model;

        private float _rotationX = 0f;
        private float _rotationY = 0f;
        private const float RotationSpeed = 0.1f;
        private Vector3 _cameraPos = new Vector3(0, 0, 5);
        private float _cameraSpeed = 0.2f;

        private bool _isRendering = false;

        private float[] _zBuffer;
        private Vector3 _lightDir = Vector3.Normalize(new Vector3(0.5f, 1f, 0.8f));

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += OnWindowLoaded;
            this.KeyDown += OnKeyDown;
        }

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (_isRendering) return;

            switch (e.Key)
            {
                case System.Windows.Input.Key.W: _cameraPos.Z -= _cameraSpeed; break;
                case System.Windows.Input.Key.S: _cameraPos.Z += _cameraSpeed; break;
                case System.Windows.Input.Key.A: _cameraPos.X -= _cameraSpeed; break;
                case System.Windows.Input.Key.D: _cameraPos.X += _cameraSpeed; break;

                case System.Windows.Input.Key.Q:
                    _cameraPos.Y -= _cameraSpeed; break;
                case System.Windows.Input.Key.E:
                    _cameraPos.Y += _cameraSpeed; break;

                case System.Windows.Input.Key.Up: _rotationX -= RotationSpeed; break;
                case System.Windows.Input.Key.Down: _rotationX += RotationSpeed; break;
                case System.Windows.Input.Key.Left: _rotationY -= RotationSpeed; break;
                case System.Windows.Input.Key.Right: _rotationY += RotationSpeed; break;
            }

            RenderModel(_model);
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _width = (int)gridDisplay.ActualWidth;
            _height = (int)gridDisplay.ActualHeight;

            if (_width <= 0 || _height <= 0) return;

            _bitmap = new WriteableBitmap(_width, _height, 96, 96, PixelFormats.Bgr32, null);
            imageContainer.Source = _bitmap;
            _zBuffer = new float[_width * _height];

            try
            {
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "human.obj");
                if (System.IO.File.Exists(path))
                {
                    _model = ObjLoader.Load(path);
                    RenderModel(_model);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private unsafe void DrawLineBresenham(int* buffer, int x1, int y1, int x2, int y2, int color, int stride)
        {
            if (Math.Abs(x1) > 10000 || Math.Abs(y1) > 10000 || Math.Abs(x2) > 10000 || Math.Abs(y2) > 10000) return;

            int dx = Math.Abs(x2 - x1);
            int dy = Math.Abs(y2 - y1);
            int sx = x1 < x2 ? 1 : -1;
            int sy = y1 < y2 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                if (x1 >= 0 && x1 < _width && y1 >= 0 && y1 < _height)
                {
                    buffer[y1 * stride + x1] = color;
                }

                if (x1 == x2 && y1 == y2) break;

                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x1 += sx; }
                if (e2 < dx) { err += dx; y1 += sy; }
            }
        }

        private unsafe void FillTriangle(int* buffer, int stride,
            int x1, int y1, int x2, int y2, int x3, int y3, int color)
        {
            if (y1 > y2) { (x1, x2) = (x2, x1); (y1, y2) = (y2, y1); }
            if (y1 > y3) { (x1, x3) = (x3, x1); (y1, y3) = (y3, y1); }
            if (y2 > y3) { (x2, x3) = (x3, x2); (y2, y3) = (y3, y2); }

            int totalHeight = y3 - y1;
            if (totalHeight == 0) return;

            int segmentHeight = y2 - y1;
            for (int y = y1; y <= y2; y++)
            {
                if (y < 0 || y >= _height) continue;
                float alpha = (float)(y - y1) / totalHeight;
                float beta = segmentHeight == 0 ? 1f : (float)(y - y1) / segmentHeight;
                int xA = x1 + (int)((x3 - x1) * alpha); 
                int xB = x1 + (int)((x2 - x1) * beta);  
                if (xA > xB) (xA, xB) = (xB, xA);
                for (int x = Math.Max(xA, 0); x <= Math.Min(xB, _width - 1); x++)
                    buffer[y * stride + x] = color;
            }

            segmentHeight = y3 - y2;
            for (int y = y2; y <= y3; y++)
            {
                if (y < 0 || y >= _height) continue;
                float alpha = (float)(y - y1) / totalHeight;
                float beta = segmentHeight == 0 ? 1f : (float)(y - y2) / segmentHeight;
                int xA = x1 + (int)((x3 - x1) * alpha); 
                int xB = x2 + (int)((x3 - x2) * beta);  
                if (xA > xB) (xA, xB) = (xB, xA);
                for (int x = Math.Max(xA, 0); x <= Math.Min(xB, _width - 1); x++)
                    buffer[y * stride + x] = color;
            }
        }

        private Matrix4x4 CreateModelMatrix()
        {
            return Matrix4x4.CreateRotationY(_rotationY) * Matrix4x4.CreateRotationX(_rotationX);
        }

        private Matrix4x4 CreateFullMatrix()
        {
            Matrix4x4 model = CreateModelMatrix();
            Vector3 target = new Vector3(_cameraPos.X, _cameraPos.Y, 0);
            Matrix4x4 view = Matrix4x4.CreateLookAt(_cameraPos, target, Vector3.UnitY);
            float aspect = (float)_width / _height;
            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, aspect, 0.1f, 100f);
            return model * view * projection;
        }

        private unsafe void RenderModel(ModelData model)
        {
            if (model == null || _isRendering) return;
            _isRendering = true;

            _bitmap.Lock();
            int* buffer = (int*)_bitmap.BackBuffer;
            int stride = _bitmap.BackBufferStride / 4;

            NativeMemory.Clear(buffer, (nuint)(_height * stride * sizeof(int)));
            Array.Fill(_zBuffer, float.MaxValue);

            Matrix4x4 modelMatrix = CreateModelMatrix();
            Matrix4x4 mvp = CreateFullMatrix();

            foreach (var face in model.Faces)
            {
                Vector4 c1 = ProjectToClip(model.Vertices[face.V1], mvp);
                Vector4 c2 = ProjectToClip(model.Vertices[face.V2], mvp);
                Vector4 c3 = ProjectToClip(model.Vertices[face.V3], mvp);

                if (c1.W < 0.1f || c2.W < 0.1f || c3.W < 0.1f) continue;

                float nx1 = c1.X / c1.W, ny1 = c1.Y / c1.W;
                float nx2 = c2.X / c2.W, ny2 = c2.Y / c2.W;
                float nx3 = c3.X / c3.W, ny3 = c3.Y / c3.W;

                float sx1 = (nx1 + 1f) * 0.5f * _width, sy1 = (1f - ny1) * 0.5f * _height;
                float sx2 = (nx2 + 1f) * 0.5f * _width, sy2 = (1f - ny2) * 0.5f * _height;
                float sx3 = (nx3 + 1f) * 0.5f * _width, sy3 = (1f - ny3) * 0.5f * _height;

                float area = (sx2 - sx1) * (sy3 - sy1) - (sx3 - sx1) * (sy2 - sy1);
                if (area == 0) continue;

                Vector3 w1 = Vector3.Transform(model.Vertices[face.V1], modelMatrix);
                Vector3 w2 = Vector3.Transform(model.Vertices[face.V2], modelMatrix);
                Vector3 w3 = Vector3.Transform(model.Vertices[face.V3], modelMatrix);
                Vector3 faceNormal = Vector3.Normalize(Vector3.Cross(w2 - w1, w3 - w1));
                Vector3 viewDir = Vector3.Normalize(w1 - _cameraPos);
                if (Vector3.Dot(faceNormal, viewDir) >= 0) continue;

                float z1 = c1.Z / c1.W;
                float z2 = c2.Z / c2.W;
                float z3 = c3.Z / c3.W;

                int faceColor = ComputeLambertColor(
                    model.Vertices[face.V1],
                    model.Vertices[face.V2],
                    model.Vertices[face.V3],
                    modelMatrix);

                FillTriangleZ(buffer, stride,
                    sx1, sy1, z1,
                    sx2, sy2, z2,
                    sx3, sy3, z3,
                    faceColor);
            }

            _bitmap.AddDirtyRect(new Int32Rect(0, 0, _width, _height));
            _bitmap.Unlock();
            _isRendering = false;
        }

        private Point Project(Vector3 vertex, Matrix4x4 mvp, out bool isVisible)
        {
            Vector4 clipSpace = Vector4.Transform(new Vector4(vertex, 1.0f), mvp);
            if (clipSpace.W < 0.1f)
            {
                isVisible = false;
                return new Point(0, 0);
            }

            isVisible = true;
            float x_ndc = clipSpace.X / clipSpace.W;
            float y_ndc = clipSpace.Y / clipSpace.W;

            float x_screen = (x_ndc + 1.0f) * 0.5f * _width;
            float y_screen = (1.0f - y_ndc) * 0.5f * _height;

            return new Point(x_screen, y_screen);
        }

        private Vector4 ProjectToClip(Vector3 vertex, Matrix4x4 mvp)
        {
            return Vector4.Transform(new Vector4(vertex, 1.0f), mvp);
        }

        private unsafe void FillTriangleZ(int* buffer, int stride,
    float x1, float y1, float z1,
    float x2, float y2, float z2,
    float x3, float y3, float z3,
    int color)
        {
            if (y1 > y2) { (x1, x2) = (x2, x1); (y1, y2) = (y2, y1); (z1, z2) = (z2, z1); }
            if (y1 > y3) { (x1, x3) = (x3, x1); (y1, y3) = (y3, y1); (z1, z3) = (z3, z1); }
            if (y2 > y3) { (x2, x3) = (x3, x2); (y2, y3) = (y3, y2); (z2, z3) = (z3, z2); }

            int iy1 = (int)y1, iy2 = (int)y2, iy3 = (int)y3;
            float totalH = y3 - y1;
            if (totalH < 1f) return;

            for (int y = iy1; y <= iy3; y++)
            {
                if (y < 0 || y >= _height) continue;

                bool inBottom = y <= iy2;
                float segH = inBottom ? (y2 - y1) : (y3 - y2);

                float alpha = (y - y1) / totalH;
                float beta = segH < 1f ? 0f : inBottom
                    ? (y - y1) / (y2 - y1)
                    : (y - y2) / (y3 - y2);

                float ax = x1 + (x3 - x1) * alpha, az = z1 + (z3 - z1) * alpha;
                float bx = inBottom
                    ? x1 + (x2 - x1) * beta
                    : x2 + (x3 - x2) * beta;
                float bz = inBottom
                    ? z1 + (z2 - z1) * beta
                    : z2 + (z3 - z2) * beta;

                if (ax > bx) { (ax, bx) = (bx, ax); (az, bz) = (bz, az); }

                int ixStart = Math.Max((int)ax, 0);
                int ixEnd = Math.Min((int)bx, _width - 1);
                float dx = bx - ax;

                for (int x = ixStart; x <= ixEnd; x++)
                {
                    float t = dx < 1f ? 0f : (x - ax) / dx;
                    float z = az + (bz - az) * t;

                    int idx = y * _width + x;
                    if (z < _zBuffer[idx])
                    {
                        _zBuffer[idx] = z;
                        buffer[y * stride + x] = color;
                    }
                }
            }
        }

        private int ComputeLambertColor(Vector3 v1, Vector3 v2, Vector3 v3, Matrix4x4 modelMatrix)
        {
            Vector3 w1 = Vector3.Transform(v1, modelMatrix);
            Vector3 w2 = Vector3.Transform(v2, modelMatrix);
            Vector3 w3 = Vector3.Transform(v3, modelMatrix);

            Vector3 edge1 = w2 - w1;
            Vector3 edge2 = w3 - w1;
            Vector3 normal = Vector3.Normalize(Vector3.Cross(edge1, edge2));

            float intensity = MathF.Max(0f, Vector3.Dot(normal, _lightDir));

            float ambient = 0.15f;
            float lit = ambient + (1f - ambient) * intensity;

            int c = (int)(lit * 255f);
            return unchecked((int)(0xFF000000 | ((uint)c << 16) | ((uint)c << 8) | (uint)c));
        }
    }
}