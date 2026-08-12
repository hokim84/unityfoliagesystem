using System;
using UnityEngine;
using System.IO;

namespace PWTA
{

    public class SampleTexel
    {
        public static readonly float[] Area9WeightTable = new float[]
        {
            0.5f, 1.1f, 0.5f,
            1.1f, 3.9f, 1.1f,
            0.5f, 1.1f, 0.5f
        };

        public enum SampleChannel
        {
            R = 1 << 0,
            G = 1 << 1,
            B = 1 << 2,
            A = 1 << 3,
        }
        
        [SerializeField]
        private float[,] texels;
        private SampleTexel() { }

        public SampleTexel(int xCount, int yCount)
        {
            texels = new float[xCount, yCount];
        }

        public int width => texels.GetLength(0);
        public int height => texels.GetLength(1);

        public enum Area9
        {
            TopLeft,
            Top,
            TopRight,
            Left,
            Center,
            Right,
            BottomLeft,
            Bottom,
            BottomRight,
            Count,
        }

        public static SampleTexel Create(Texture2D texture, Vector2Int sampleResolution, SampleChannel channel, Vector2 uvMin, Vector2 uvMax)
        {
            int stepX = Mathf.Max(texture.width / sampleResolution.x, 1);
            int stepY = Mathf.Max(texture.height / sampleResolution.y, 1);

            int beginX = Mathf.FloorToInt(uvMin.x * texture.width);
            int beginY = Mathf.FloorToInt(uvMin.y * texture.height);

            int endX = Mathf.FloorToInt(uvMax.x * texture.width);
            int endY = Mathf.FloorToInt(uvMax.y * texture.height);

            int texelWidth = endX - beginX;
            int texelHeight = endY - beginY;

            SampleTexel texels = new SampleTexel(texelWidth, texelHeight);
            for (int y = beginY; y < endY; y += stepY)
            {
                for (int x = beginX; x < endX; x += stepX)
                {
                    var factor = texture.GetPixel(x, y);
                    texels[x - beginX, y - beginY] = GetChannelValue(factor, channel);
                }
            }
            return texels;
        }

        public static int GetArea9Index(Area9 area9)
        {
            return (int)area9;
        }

        public float this[int x, int y]
        {
            get => texels[x, y];
            set => texels[x, y] = value;
        }

        public float GetValidTexel(int x, int y)
        {
            if (x < 0 || x >= texels.GetLength(0) || y < 0 || y >= texels.GetLength(1))
                return default(float);

            return texels[x, y];
        }

        public Vector2Int GetTexelIndex(float u, float v)
        {
            var x = Mathf.FloorToInt(u * texels.GetLength(0));
            var y = Mathf.FloorToInt(v * texels.GetLength(1));
            return new Vector2Int(x, y);
        }

        public float[] GetArea9(int x, int y)
        {
            float[] area9 = new float[(int)Area9.Count];
            area9[(int)Area9.TopLeft] = GetValidTexel(x - 1, y - 1);
            area9[(int)Area9.Top] = GetValidTexel(x, y - 1);
            area9[(int)Area9.TopRight] = GetValidTexel(x + 1, y - 1);
            area9[(int)Area9.Left] = GetValidTexel(x - 1, y);
            area9[(int)Area9.Center] = GetValidTexel(x, y);
            area9[(int)Area9.Right] = GetValidTexel(x + 1, y);
            area9[(int)Area9.BottomLeft] = GetValidTexel(x - 1, y + 1);
            area9[(int)Area9.Bottom] = GetValidTexel(x, y + 1);
            area9[(int)Area9.BottomRight] = GetValidTexel(x + 1, y + 1);
            return area9;
        }

        public float GetArea9WeightedAverage(int x, int y)
        {
            if (null == texels)
                return 0f;

            var area9 = GetArea9(x, y);
            float sum = 0;
            for (int i = 0; i < area9.Length; i++)
            {
                sum += area9[i] * Area9WeightTable[i];
            }
            return Mathf.Min(1f, sum / 9f);
        }

        public static Area9 GetArea9Relative(float x, float y)
        {
            if (y > 0)
            {
                if (x < 0)
                    return Area9.TopLeft;
                else if (x > 0)
                    return Area9.TopRight;
                else
                    return Area9.Top;
            }
            else if (y <= 0)
            {
                if (x < 0)
                    return Area9.BottomLeft;
                else if (x > 0)
                    return Area9.BottomRight;
                else
                    return Area9.Bottom;
            }
            else
                return Area9.Center;
        }

        public static float DensityArea9Test(float[] outCellStates, Vector2 position, float radius)
        {
            var area9 = GetArea9Relative((int)position.x, (int)position.y);
            var sqrDist = position.SqrMagnitude();
            if (outCellStates[(int)area9] > 0f)
                return 1f;

            var rate = (radius * radius) - sqrDist;
            return rate;
        }

        public static float GetChannelValue(Color color, SampleChannel channel, bool inversed = false, bool squared = false)
        {
            float v = 0f;
            switch (channel)
            {
                case SampleChannel.R:
                    v = color.r;
                    break;
                case SampleChannel.G:
                    v = color.g;
                    break;
                case SampleChannel.B:
                    v = color.b;
                    break;
                case SampleChannel.A:
                    v = color.a;
                    break;
            }
            var result = inversed ? 1f - v : v;
            return squared ? result * result : result;
        }

        public Texture2D ToTexture2D()
        {
            var texture = new Texture2D(texels.GetLength(0), texels.GetLength(1));
            for (int y = 0; y < texels.GetLength(1); y++)
            {
                for (int x = 0; x < texels.GetLength(0); x++)
                {
                    var value = Convert.ToSingle(texels[x, y]);
                    texture.SetPixel(x, y, new Color(value, 0, 0, 1));
                }
            }
            texture.Apply();
            return texture;
        }

        public void SaveAsTexture(string path)
        {
            var texture = new Texture2D(texels.GetLength(0), texels.GetLength(1));
            for (int y = 0; y < texels.GetLength(1); y++)
            {
                for (int x = 0; x < texels.GetLength(0); x++)
                {
                    var value = Convert.ToSingle(texels[x, y]);
                    texture.SetPixel(x, y, new Color(value, 0, 0, 1));
                }
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Debug.Log($"Save texture to {path}");
        }
    }
}