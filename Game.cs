using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Vector3 = System.Numerics.Vector3;

namespace SimdBench
{
    public class Game : Microsoft.Xna.Framework.Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private Vector128<float>[] _vertices;
        private SpriteBatch _spriteBatch;
        private Texture2D _surface;
        private Raster _colorRaster;

        private Game()
        {
            Content.RootDirectory = "Content";
            IsMouseVisible = false;

            var screenWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            var screenHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = screenWidth,
                PreferredBackBufferHeight = screenHeight,
                SynchronizeWithVerticalRetrace = true,
                IsFullScreen = true
            };
        }

        protected override void Initialize()
        {
            _colorRaster = new Raster(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _surface = new Texture2D(
                _graphics.GraphicsDevice,
                _graphics.PreferredBackBufferWidth,
                _graphics.PreferredBackBufferHeight,
                false,
                SurfaceFormat.Color
            );

            _vertices = LoadModel("Content/car.obj");
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
                Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();

            var width = _graphics.PreferredBackBufferWidth;
            var height = _graphics.PreferredBackBufferHeight;

            var ang1 = gameTime.TotalGameTime.Ticks / 9230000.0F;
            var aspect = (float) width / height;
            var look = Matrix4x4.CreateLookAt(new Vector3(0, 0, 60), Vector3.One, Vector3.UnitY);
            var proj = Matrix4x4.CreatePerspectiveFieldOfView((float) (Math.PI / 3.0), aspect, 1.0F, 1000.0F);
            var rotationY = Matrix4x4.CreateRotationY(ang1);
            var rotationX = Matrix4x4.CreateRotationX(0.5F);
            var comb = rotationY * rotationX * Matrix4x4.CreateScale(10.0F) * look * proj;
            var m0 = Vector128.Create(comb.M11, comb.M21, comb.M31, comb.M41);
            var m1 = Vector128.Create(comb.M12, comb.M22, comb.M32, comb.M42);
            var m2 = Vector128.Create(comb.M13, comb.M23, comb.M33, comb.M43);
            var m3 = Vector128.Create(comb.M14, comb.M24, comb.M34, comb.M44);

            var inv = Vector128.Create(1.0F, -1.0F, 1.0F, 1.0F);
            var half = Vector128.Create(0.5F);
            var screen = Vector128.Create(width, height, 0.0F, 0.0F);

            _colorRaster.Clear(-0x1000000);
            var chunks = _vertices.Length / Environment.ProcessorCount;
            Parallel.For(0, Environment.ProcessorCount, y =>
            {
                var offset = y * chunks;
                for (var l = offset; l < offset + chunks; l++)
                {
                    var vv = _vertices[l];
                    var h0 = Sse3.HorizontalAdd(Sse.Multiply(vv, m0), Sse.Multiply(vv, m1));
                    var h1 = Sse3.HorizontalAdd(Sse.Multiply(vv, m2), Sse.Multiply(vv, m3));
                    var h3 = Sse.Multiply(inv, Sse3.HorizontalAdd(h0, h1));
                    var vv2 = Sse.Divide(h3, Vector128.Create(h3.GetElement(3)));
                    var vv4 = Sse.Multiply(screen, Sse.Multiply(half, Sse.Add(Vector128.Create(1.0F), vv2)));
                    var f = Sse2.ConvertToVector128Int32(vv4);
                    var sx = f.GetElement(0);
                    var sy = f.GetElement(1);
                    _colorRaster[sx, sy] = 0xFFFFFF;
                }
            });

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            _surface.SetData(_colorRaster.Data);
            _spriteBatch.Begin();
            _spriteBatch.Draw(_surface, _graphics.GraphicsDevice.Viewport.Bounds, Color.White);
            _spriteBatch.End();
            base.Draw(gameTime);
        }

        private static Vector128<float>[] LoadModel(string filename)
        {
            var vertices = new List<Vector128<float>>();
            using var reader = new StreamReader(filename);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length <= 0 || !line.StartsWith("v ")) continue;
                var match = line.Split(' ');
                var x = float.Parse(match[1]);
                var y = float.Parse(match[2]);
                var z = float.Parse(match[3]);
                vertices.Add(Vector128.Create(x, y, z, 1F));
            }

            return vertices.ToArray();
        }

        private static void Main()
        {
            using var game = new Game();
            game.Run();
        }
    }
}