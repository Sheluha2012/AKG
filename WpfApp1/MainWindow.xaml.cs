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
        private int _width, _height;
        private ModelData _model;
        private float[] _zBuffer;

        private Texture _diffuseMap;
        private Texture _normalMap;
        private Texture _specularMap;

        private float _rotationX = 0, _rotationY = 0;
        private Vector3 _cameraPos = new Vector3(0, 0, 5);
        private Vector3 _lightPos = new Vector3(2f, 4f, 3f);
        private Vector3 _lightColor = new Vector3(1f, 1f, 1f);

        private const float Ka = 0.2f, Kd = 0.7f, Shininess = 30f;
        private bool _isRendering = false;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnWindowLoaded;
            KeyDown += OnKeyDown;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _width = (int)gridDisplay.ActualWidth;
            _height = (int)gridDisplay.ActualHeight;
            if (_width <= 0) return;

            _bitmap = new WriteableBitmap(_width, _height, 96, 96, PixelFormats.Bgr32, null);
            imageContainer.Source = _bitmap;
            _zBuffer = new float[_width * _height];

            try
            {
                _model = ObjLoader.Load("african_head.obj");
                _diffuseMap = new Texture("african_head_diffuse.png");
                _normalMap = new Texture("african_head_nm.png");
                _specularMap = new Texture("african_head_spec.png");
                RenderModel();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (_isRendering) return;
            switch (e.Key)
            {
                case System.Windows.Input.Key.W: _cameraPos.Z -= 0.2f; break;
                case System.Windows.Input.Key.S: _cameraPos.Z += 0.2f; break;
                case System.Windows.Input.Key.Left: _rotationY -= 0.1f; break;
                case System.Windows.Input.Key.Right: _rotationY += 0.1f; break;
                case System.Windows.Input.Key.Up: _rotationX -= 0.1f; break;
                case System.Windows.Input.Key.Down: _rotationX += 0.1f; break;
            }
            RenderModel();
        }

        private unsafe void RenderModel()
        {
            if (_model == null || _isRendering) return;
            _isRendering = true;

            _bitmap.Lock();
            int* buffer = (int*)_bitmap.BackBuffer;
            int stride = _bitmap.BackBufferStride / 4;

            NativeMemory.Clear(buffer, (nuint)(_height * stride * sizeof(int)));
            Array.Fill(_zBuffer, float.MaxValue);

            Matrix4x4 modelMat = Matrix4x4.CreateRotationY(_rotationY) * Matrix4x4.CreateRotationX(_rotationX);
            Matrix4x4 viewMat = Matrix4x4.CreateLookAt(_cameraPos, new Vector3(_cameraPos.X, _cameraPos.Y, 0), Vector3.UnitY);
            Matrix4x4 projMat = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, (float)_width / _height, 0.1f, 100f);
            Matrix4x4 mvp = modelMat * viewMat * projMat;

            foreach (var face in _model.Faces)
            {
                Vector4 c1 = Vector4.Transform(new Vector4(_model.Vertices[face.V1], 1), mvp);
                Vector4 c2 = Vector4.Transform(new Vector4(_model.Vertices[face.V2], 1), mvp);
                Vector4 c3 = Vector4.Transform(new Vector4(_model.Vertices[face.V3], 1), mvp);

                if (c1.W < 0.1f || c2.W < 0.1f || c3.W < 0.1f) continue;
                float sx1 = (c1.X / c1.W + 1) * 0.5f * _width, sy1 = (1 - c1.Y / c1.W) * 0.5f * _height;
                float sx2 = (c2.X / c2.W + 1) * 0.5f * _width, sy2 = (1 - c2.Y / c2.W) * 0.5f * _height;
                float sx3 = (c3.X / c3.W + 1) * 0.5f * _width, sy3 = (1 - c3.Y / c3.W) * 0.5f * _height;

                if ((sx2 - sx1) * (sy3 - sy1) - (sx3 - sx1) * (sy2 - sy1) >= 0) continue;

                FillTriangleCorrected(buffer, stride,
                    sx1, sy1, c1.Z / c1.W, c1.W, _model.TexCoords[face.T1], Vector3.Transform(_model.Vertices[face.V1], modelMat),
                    sx2, sy2, c2.Z / c2.W, c2.W, _model.TexCoords[face.T2], Vector3.Transform(_model.Vertices[face.V2], modelMat),
                    sx3, sy3, c3.Z / c3.W, c3.W, _model.TexCoords[face.T3], Vector3.Transform(_model.Vertices[face.V3], modelMat),
                    modelMat);
            }

            _bitmap.AddDirtyRect(new Int32Rect(0, 0, _width, _height));
            _bitmap.Unlock();
            _isRendering = false;
        }

        private unsafe void FillTriangleCorrected(int* buffer, int stride,
    float x1, float y1, float z1, float w1, Vector2 uv1, Vector3 p1,
    float x2, float y2, float z2, float w2, Vector2 uv2, Vector3 p2,
    float x3, float y3, float z3, float w3, Vector2 uv3, Vector3 p3, Matrix4x4 modelMat)
        {
            void Swap(ref float a, ref float b) { float t = a; a = b; b = t; }
            void SwapV2(ref Vector2 a, ref Vector2 b) { Vector2 t = a; a = b; b = t; }
            void SwapV3(ref Vector3 a, ref Vector3 b) { Vector3 t = a; a = b; b = t; }

            if (y1 > y2) { Swap(ref x1, ref x2); Swap(ref y1, ref y2); Swap(ref z1, ref z2); Swap(ref w1, ref w2); SwapV2(ref uv1, ref uv2); SwapV3(ref p1, ref p2); }
            if (y1 > y3) { Swap(ref x1, ref x3); Swap(ref y1, ref y3); Swap(ref z1, ref z3); Swap(ref w1, ref w3); SwapV2(ref uv1, ref uv3); SwapV3(ref p1, ref p3); }
            if (y2 > y3) { Swap(ref x2, ref x3); Swap(ref y2, ref y3); Swap(ref z2, ref z3); Swap(ref w2, ref w3); SwapV2(ref uv2, ref uv3); SwapV3(ref p2, ref p3); }

            float invW1 = 1f / w1, invW2 = 1f / w2, invW3 = 1f / w3;
            Vector2 uvW1 = uv1 * invW1, uvW2 = uv2 * invW2, uvW3 = uv3 * invW3;
            Vector3 pW1 = p1 * invW1, pW2 = p2 * invW2, pW3 = p3 * invW3;

            int totalHeight = (int)MathF.Round(y3) - (int)MathF.Round(y1);
            for (int y = (int)MathF.Round(y1); y <= (int)MathF.Round(y3); y++)
            {
                if (y < 0 || y >= _height) continue;

                bool inBottom = y < (int)MathF.Round(y2);
                float alpha = (float)(y - (int)MathF.Round(y1)) / (totalHeight == 0 ? 1 : totalHeight);

                float segmentHeight = inBottom ?
                    (int)MathF.Round(y2) - (int)MathF.Round(y1) :
                    (int)MathF.Round(y3) - (int)MathF.Round(y2);

                float beta = (float)(y - (inBottom ? (int)MathF.Round(y1) : (int)MathF.Round(y2))) / (segmentHeight == 0 ? 1 : segmentHeight);

                float ax = x1 + (x3 - x1) * alpha;
                float az = z1 + (z3 - z1) * alpha;
                float aw = invW1 + (invW3 - invW1) * alpha;
                Vector2 auv = uvW1 + (uvW3 - uvW1) * alpha;
                Vector3 ap = pW1 + (pW3 - pW1) * alpha;

                float bx, bz, bw; Vector2 buv; Vector3 bp;
                if (inBottom)
                {
                    bx = x1 + (x2 - x1) * beta;
                    bz = z1 + (z2 - z1) * beta;
                    bw = invW1 + (invW2 - invW1) * beta;
                    buv = uvW1 + (uvW2 - uvW1) * beta;
                    bp = pW1 + (pW2 - pW1) * beta;
                }
                else
                {
                    bx = x2 + (x3 - x2) * beta;
                    bz = z2 + (z3 - z2) * beta;
                    bw = invW2 + (invW3 - invW2) * beta;
                    buv = uvW2 + (uvW3 - uvW2) * beta;
                    bp = pW2 + (pW3 - pW2) * beta;
                }

                if (ax > bx)
                {
                    (ax, bx) = (bx, ax); (az, bz) = (bz, az); (aw, bw) = (bw, aw);
                    (auv, buv) = (buv, auv); (ap, bp) = (bp, ap);
                }

                int startX = (int)MathF.Ceiling(ax);
                int endX = (int)MathF.Floor(bx);

                for (int x = startX; x <= endX; x++)
                {
                    if (x < 0 || x >= _width) continue;
                    float t = (bx - ax) < 0.0001f ? 0 : (x - ax) / (bx - ax);
                    float z = az + (bz - az) * t;

                    if (z < _zBuffer[y * _width + x])
                    {
                        _zBuffer[y * _width + x] = z;

                        float currentInvW = aw + (bw - aw) * t;
                        float w = 1f / currentInvW;
                        Vector2 uv = (auv + (buv - auv) * t) * w;
                        Vector3 pos = (ap + (bp - ap) * t) * w;

                        Vector3 texColor = _diffuseMap.Sample(uv.X, uv.Y);
                        Vector3 normalSample = _normalMap.Sample(uv.X, uv.Y);

                        Vector3 modelNormal = Vector3.Normalize(normalSample * 2.0f - Vector3.One);
                        Vector3 worldNormal = Vector3.Normalize(Vector3.TransformNormal(modelNormal, modelMat));
                        float ks = _specularMap.Sample(uv.X, uv.Y).X;

                        buffer[y * stride + x] = CalculateColor(worldNormal, pos, texColor, ks);
                    }
                }
            }
        }

        private int CalculateColor(Vector3 normal, Vector3 pos, Vector3 diffuseColor, float ks)
        {
            Vector3 lightDir = Vector3.Normalize(_lightPos - pos);
            Vector3 viewDir = Vector3.Normalize(_cameraPos - pos);
            Vector3 reflectDir = Vector3.Reflect(-lightDir, normal);

            float diff = MathF.Max(Vector3.Dot(normal, lightDir), 0f);
            float spec = MathF.Pow(MathF.Max(Vector3.Dot(viewDir, reflectDir), 0f), Shininess);

            Vector3 final = (Ka * _lightColor + Kd * diff * _lightColor + ks * spec * _lightColor) * diffuseColor;

            int r = (int)(Math.Clamp(final.X, 0, 1) * 255);
            int g = (int)(Math.Clamp(final.Y, 0, 1) * 255);
            int b = (int)(Math.Clamp(final.Z, 0, 1) * 255);
            return unchecked((int)(0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b));
        }
    }
}