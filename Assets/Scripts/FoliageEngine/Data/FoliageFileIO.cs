using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System;

namespace PWTA
{
    public static class FoliageFileIO
    {
        private static readonly Dictionary<int, IFoliageSerializer> formats = new()
        {
            { 1, new FoliageSerializerV1() },
        };

        public static void Save(string path, IFoliageFileSchema data, int version = 1)
        {
            using (var writer = File.Open(path, FileMode.Create))
            {
                if (formats.TryGetValue(version, out var format))
                {
                    format.Save(writer, data);
                }
                else
                {
                    Debug.LogError($"Save, Unsupported version: {version}");
                }
            }
        }

        public static IFoliageFileSchema Load(byte[] bytes, int version = 1)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return LoadStream(stream, version);
            }            
        }

        public static IFoliageFileSchema Load(string path, int version = 1)
        {
#if !UNITY_EDITOR
            using (Stream stream = LoadDataAsync(path).GetAwaiter().GetResult())
#else
            using (Stream stream = File.OpenRead(path))
#endif
            {
                return LoadStream(stream, version);
            }
        }

        private static IFoliageFileSchema LoadStream(Stream stream, int version = 1)
        {
            if (formats.TryGetValue(version, out var format))
            {
                return format.Load(stream);
            }
            else
            {
                Debug.LogError($"Load, Unsupported version: {version}");
            }
            return null;
        }

        public static async Task<Stream> LoadDataAsync(string url)
        {
            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                var op = www.SendWebRequest();

                while (!op.isDone)
                    await Task.Yield();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    var data = www.downloadHandler.data;
                    return new MemoryStream(data);
                }
                else
                {
                    throw new Exception(www.error);
                }
            }
        }
    }
}