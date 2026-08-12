// AlphaToSdfBaker.cs
// 에디터에서: 마스크(알파) 텍스처 -> SDF 텍스처(0.5=경계) PNG로 베이크
// 사용: Project 뷰에서 Texture2D 선택 -> 메뉴 "Tools/SDF/Bake SDF From Alpha (Radius=5)" 실행
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PWTA
{
    public static class SDFTexutreBaker
    {
        // 너 조건: 512, 1회 베이크, 최대 5px
        const int DefaultRadius = 5;
        const float DefaultAlphaThreshold = 0.5f;

        public enum TargetChannel
        {
            A,
            R,
            G,
            B,
        }

        struct Offset
        {
            public int dx, dy;
            public float dist; // sqrt
            public Offset(int dx, int dy, float dist) { this.dx = dx; this.dy = dy; this.dist = dist; }
        }

        private static byte GetPixelValue(Color32 pixel, TargetChannel channel)
        {
            switch (channel)
            {
                case TargetChannel.A: return pixel.a;
                case TargetChannel.R: return pixel.r;
                case TargetChannel.G: return pixel.g;
                case TargetChannel.B: return pixel.b;
                default: return 0;
            }            
        }

        private static void SetPixel32Value(float value, ref Color32 pixel, TargetChannel channel)
        {
            switch (channel)
            {
                case TargetChannel.A: pixel.a = (byte)Mathf.RoundToInt(value * 255f);
                    break;
                case TargetChannel.R: pixel.r = (byte)Mathf.RoundToInt(value * 255f);
                    break;
                case TargetChannel.G: pixel.g = (byte)Mathf.RoundToInt(value * 255f);
                    break;
                case TargetChannel.B: pixel.b = (byte)Mathf.RoundToInt(value * 255f);                
                    break;
            }
        }

        public static void BakeToPngAsset(Texture2D src, TargetChannel channel, int radius, float threshold, ref Color32[] outPixels)
        {
            if (radius <= 0) radius = 1;
            threshold = Mathf.Clamp01(threshold);

            int width = src.width;
            int height = src.height;
            
            Color32[] pixels32 = null;
            try
            {
                var data = src.GetPixelData<Color32>(0);
                pixels32 = data.ToArray(); // 한번만 복사
            }
            catch
            {
                // fallback
                pixels32 = src.GetPixels32();
            }

            // inside/outside bool
            var inside = new bool[width * height];
            byte thresholdValue = (byte)Mathf.RoundToInt(threshold * 255f);
            for (int i = 0; i < inside.Length; i++)
                inside[i] = GetPixelValue(pixels32[i], channel) > thresholdValue;

            // 반경 내 오프셋 미리 계산 (원형 커널)
            var offsets = BuildOffsets(radius);
            float inv2R = 1f / (2f * radius);

            // 베이크
            // 로직: 현재 픽셀 inside 여부 기준으로, 반경 R 내에서 "반대편" 픽셀까지 최소거리 찾기
            // 없으면 R로 클램프
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = row + x;
                    bool isIn = inside[idx];

                    float minDist = radius;
                    for (int k = 0; k < offsets.Count; k++)
                    {
                        var o = offsets[k];
                        int nx = x + o.dx;
                        int ny = y + o.dy;
                        if ((uint)nx >= (uint)width || (uint)ny >= (uint)height) continue;

                        int nIdx = ny * width + nx;
                        if (inside[nIdx] != isIn)
                        {
                            minDist = o.dist;
                            break; // offsets가 거리순이라 첫 발견이 최소
                        }
                    }

                    // signed: inside는 음수, outside는 양수
                    float signed = isIn ? -minDist : +minDist;

                    // 0.5 = 경계(0거리)
                    // sdf01 = 0.5 + signed/(2R)
                    float sdf01 = 0.5f + signed * inv2R;
                    sdf01 = Mathf.Clamp01(sdf01);

                    byte v = (byte)Mathf.RoundToInt(sdf01 * 255f);
                    SetPixel32Value(v, ref outPixels[idx], channel);
                }
            }
        }

        // R8 (single-channel) 지원: 입력은 R 값만 갖는 텍스처, 출력은 byte 배열
        public static void BakeToPngAsset(Texture2D src, int radius, float threshold, bool inverse, ref byte[] outBytes)
        {
            if (radius <= 0) radius = 1;
            threshold = Mathf.Clamp01(threshold);

            int width = src.width;
            int height = src.height;

            // 입력 데이터
            byte[] pixels;
            try
            {
                var data = src.GetPixelData<byte>(0);
                pixels = data.ToArray();
            }
            catch
            {
                // R8이 아닐 때 안전 fallback: R 채널만 사용
                var c32 = src.GetPixels32();
                pixels = new byte[c32.Length];
                for (int i = 0; i < c32.Length; i++)
                    pixels[i] = c32[i].r;
            }

            if (outBytes == null || outBytes.Length != width * height)
                outBytes = new byte[width * height];

            // inside/outside bool
            var inside = new bool[width * height];
            byte thresholdValue = (byte)Mathf.RoundToInt(threshold * 255f);
            for (int i = 0; i < inside.Length; i++)
                inside[i] = pixels[i] > thresholdValue;

            var offsets = BuildOffsets(radius);
            float inv2R = 1f / (2f * radius);

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = row + x;
                    bool isIn = inside[idx];

                    float minDist = radius;
                    for (int k = 0; k < offsets.Count; k++)
                    {
                        var o = offsets[k];
                        int nx = x + o.dx;
                        int ny = y + o.dy;
                        if ((uint)nx >= (uint)width || (uint)ny >= (uint)height) continue;

                        int nIdx = ny * width + nx;
                        if (inside[nIdx] != isIn)
                        {
                            minDist = o.dist;
                            break; // offsets가 거리순이라 첫 발견이 최소
                        }
                    }

                    float signed = isIn ? -minDist : +minDist;
                    float sdf01 = 0.5f + signed * inv2R;
                    sdf01 = inverse ? 1f - Mathf.Clamp01(sdf01) : Mathf.Clamp01(sdf01);

                    outBytes[idx] = (byte)Mathf.RoundToInt(sdf01 * 255f);
                }
            }
        }
      

        static List<Offset> BuildOffsets(int radius)
        {
            var list = new List<Offset>(radius * radius * 4);

            // (0,0)도 포함해야 경계에서 0이 가능해짐? -> 실제론 반대편 픽셀 체크라 (0,0)은 의미 없음
            // 그래도 "거리순 정렬"을 위해 포함 안함. 대신 바로 이웃에서 걸리게 됨.
            int r2 = radius * radius;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int d2 = dx * dx + dy * dy;
                    if (d2 > r2) continue;
                    float dist = Mathf.Sqrt(d2);
                    list.Add(new Offset(dx, dy, dist));
                }
            }

            // 거리 오름차순 정렬 (첫 히트가 최소거리)
            list.Sort((a, b) => a.dist.CompareTo(b.dist));
            return list;
        }
    }
#endif

}