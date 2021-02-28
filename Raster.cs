using System;

namespace SimdBench
{
    public class Raster
    {
        private const int Opaque = 0xFF << 24;

        private readonly int _width;
        private readonly int _height;

        public readonly int[] Data;

        public Raster(int width, int height)
        {
            _width = width;
            _height = height;
            Data = new int[width * height];
        }

        public int this[in int x, in int y]
        {
            set => Data[y * _width + x] = Opaque | value;
        }
        
        public void Clear(int color) => Array.Fill(Data, Opaque | color);
    }
}