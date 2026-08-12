using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PWTA
{
    public class FoliageFileSchemaV1 : IFoliageFileSchema
    {
        public int Version { get => 1; }
        public int Signature { get => 0x46464646; }
        public int FoliageCount { get; set; }
        public List<IFoliageElement> Foliages { get; set; }
    }

    public class FoliageSerializerV1 : IFoliageSerializer
    {
        public int Version => 1;
        public int Signature => 0x46464646;

        public void Save(Stream writeStream, IFoliageFileSchema data)
        {
            using var writer = new BinaryWriter(writeStream);

            writer.Write(Signature);
            writer.Write(Version);           
            writer.Write(data.FoliageCount);
            for (int i = 0; i < data.FoliageCount; ++i)
            {
                var foliage = data.Foliages[i];
                writer.Write(foliage.PaletteSlotIdx);
                writer.Write(foliage.Position.x);
                writer.Write(foliage.Position.y);
                writer.Write(foliage.Position.z);
                writer.Write(foliage.RotationY);
                writer.Write(foliage.UniformScale);
            }
        }

        public IFoliageFileSchema Load(Stream readStream)
        {
            using var reader = new BinaryReader(readStream);
            var signature = reader.ReadInt32();
            if (signature != Signature)
            {
                Debug.LogError($"File Signature mismatch: {signature} != {Signature}");
                return null;
            }

            var version = reader.ReadInt32();
            if (version != Version)
            {
                Debug.LogError($"File Version mismatch: {version} != {Version}");
                return null;
            }

            var schema = new FoliageFileSchemaV1();
            schema.FoliageCount = reader.ReadInt32();
            schema.Foliages = new List<IFoliageElement>(schema.FoliageCount);
            for (int i = 0; i < schema.FoliageCount; ++i)
            {
                var palette = new FoliageInstance_Runtime();
                palette.PaletteSlotIdx = reader.ReadInt32();
                palette.Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                palette.RotationY = reader.ReadSingle();
                palette.UniformScale = reader.ReadSingle();
                schema.Foliages.Add(palette);
            }
            return schema;
        }
    }
}