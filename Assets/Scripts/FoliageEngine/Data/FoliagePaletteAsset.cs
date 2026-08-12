using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using System.Linq;

namespace PWTA
{
    [CreateAssetMenu(fileName = "FoliagePaletteAsset", menuName = "Foliage/FoliagePaletteAsset")]
    public class FoliagePaletteAsset : ScriptableObject
    {
        public static readonly string DefaultFoliageAssetsPath = "Foliages/";

        #region SerializeField
        public List<FoliagePaletteSlotData> paletteSlots = new List<FoliagePaletteSlotData>();
        public TextAsset foliageAssets;
        #endregion

        public int PalleteCount => null != paletteSlots ? paletteSlots.Count : 0;
        public Dictionary<int, HashSet<IFoliageElement>> paletteSlotsDict = new Dictionary<int, HashSet<IFoliageElement>>();
        private Dictionary<int, HashSet<Matrix4x4>> allMatrices = new Dictionary<int, HashSet<Matrix4x4>>();
        public Matrix4x4[] GetMatrices(int paletteSlotIdx)
        {
            return allMatrices.ContainsKey(paletteSlotIdx) ? allMatrices[paletteSlotIdx].ToArray() : null;
        }

        private bool isInitialized = false;

        private int _initialFoliageCount = 0;
        public int InitialFoliageCount => _initialFoliageCount;
        private ulong _initialChecksum = 0;
        public ulong InitialChecksum => _initialChecksum;

        public void Initialize(bool force = false)
        {
            if (isInitialized && !force)
                return;

            paletteSlotsDict.Clear();
            allMatrices.Clear();
            foreach (var slot in paletteSlots)
            {
                slot.Initialize();
                paletteSlotsDict[slot.SlotIdx] = new HashSet<IFoliageElement>();
                allMatrices[slot.SlotIdx] = new HashSet<Matrix4x4>();
            }
            isInitialized = true;
        }

        public void LoadFoliages(float worldDensity = 1f)
        {
            if (!LoadFoliages(foliageAssets, worldDensity))
            {
                Debug.LogWarning("FoliagePaletteAsset.LoadFoliages: Failed to load foliage assets");
            }
        }        

        public ulong InitFoliageSchema(IFoliageFileSchema foliageData, float worldDensity = 1f)
        {
            HashSet<int> missingSlots = new HashSet<int>();
            foreach (var foliage in foliageData.Foliages)
            {
                if (!paletteSlotsDict.ContainsKey(foliage.PaletteSlotIdx))
                {
                    missingSlots.Add(foliage.PaletteSlotIdx);
                    continue;
                }
                if (!FoliageUtils.HashDensityTest(foliage.Position, worldDensity))
                {
                    continue;
                }
                paletteSlotsDict[foliage.PaletteSlotIdx].Add(foliage);
                allMatrices[foliage.PaletteSlotIdx].Add(foliage.GetMatrix());
            }

            foreach (var slotIdx in missingSlots)
            {
                Debug.LogWarning($"Foliage slot {slotIdx} is missing");
            }

            return FoliageUtils.ComputeChecksum(paletteSlotsDict);
        }

        public int GetFoliageCount()
        {
            int count = 0;
            foreach (var foliageSet in paletteSlotsDict.Values)
            {
                count += foliageSet.Count;
            }
            return count;
        }

        public bool LoadFoliages(TextAsset foliageAssets, float worldDensity = 1f)
        {
            if (worldDensity <= 0f || null == foliageAssets)
                return false;

            var foliageFileSchema = FoliageFileIO.Load(foliageAssets.bytes);
            _initialChecksum = InitFoliageSchema(foliageFileSchema, worldDensity);
            _initialFoliageCount = GetFoliageCount();

            return true;
        }

        public void SaveFoliages()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);

            var schema = new FoliageFileSchemaV1();
            var foliages = GetFoliages().ToList();

            schema.FoliageCount = foliages.Count;
            schema.Foliages = foliages;

            string fileSavePath = "";
            if (foliageAssets != null)
            {
                fileSavePath = AssetDatabase.GetAssetPath(foliageAssets);
            }
            else
            {
                fileSavePath = FoliageUtils.GetDefaultFoliagePath();
            }

            FoliageFileIO.Save(fileSavePath, schema);
            AssetDatabase.Refresh();
            fileSavePath = fileSavePath.Replace(Application.dataPath, "Assets");
            foliageAssets = AssetDatabase.LoadAssetAtPath<TextAsset>(fileSavePath);

            Debug.Log($"FoliageAssets saved to {fileSavePath}");
#endif
            _initialChecksum = GetChecksum();
        }

        public FoliagePaletteSlotData GetSlot(int slotIdx)
        {
            foreach (var slot in paletteSlots)
            {
                if (slot.SlotIdx == slotIdx)
                    return slot;
            }
            return null;
        }

        public Mesh GetSlotMesh(int slotIdx)
        {
            return GetSlot(slotIdx).Mesh;
        }

        public Material[] GetSlotMaterials(int slotIdx)
        {
            return GetSlot(slotIdx).Materials;
        }

        public bool HasSlot(int slotIdx)
        {
            return slotIdx >= 0 && slotIdx < paletteSlots.Count;
        }

        public FoliagePaletteSlotData[] ToArray()
        {
            return paletteSlots.ToArray();
        }

        public IEnumerable<IFoliageElement> GetFoliages(int slotIdx)
        {
            if (!paletteSlotsDict.ContainsKey(slotIdx))
                return Enumerable.Empty<IFoliageElement>();
            return paletteSlotsDict[slotIdx];
        }

        public IEnumerable<IFoliageElement> GetAllFoliages()
        {
            foreach (var foliageSet in paletteSlotsDict.Values)
            {
                foreach (var foliage in foliageSet)
                {
                    yield return foliage;
                }
            }
        }

        public int Count => paletteSlots.Count;

        public IEnumerable<FoliagePaletteSlotData> Slots => paletteSlots;

        public void SetSlot(int slotIdx, FoliagePaletteSlotData slot)
        {
            if (slotIdx >= paletteSlots.Count)
                Add(slot);
            else
            {
                paletteSlots[slotIdx] = slot;
                ReorderSlotIdx();
            }
        }

        public void Add(FoliagePaletteSlotData slot)
        {
            slot.slotIdx = paletteSlots.Count;
            paletteSlots.Add(slot);
        }

        public bool Remove(int slotIdx)
        {
            if (paletteSlots.Remove(GetSlot(slotIdx)))
            {
                paletteSlotsDict.Remove(slotIdx);
                ReorderSlotIdx();
                return true;
            }
            return false;
        }

        public bool Remove(FoliagePaletteSlotData slot)
        {
            return paletteSlots.Remove(slot);
        }

        public void ReorderSlotIdx()
        {
            for (int i = 0; i < paletteSlots.Count; i++)
            {
                paletteSlots[i].slotIdx = i;
            }
        }

        public void ClearPalette()
        {
            paletteSlots.Clear();
        }

        public void ClearFoliages()
        {
            if (null == paletteSlotsDict)
                return;

            for (int i = 0; i < paletteSlots.Count; i++)
            {
                paletteSlotsDict[i].Clear();
            }
            
            for (int i = 0; i < paletteSlots.Count; i++)
            {
                allMatrices[i].Clear();
            }
        }

        public bool ContainsKey(int paletteID)
        {
            return paletteSlots.Find(x => x.SlotIdx == paletteID) != null;
        }

        public bool TryGetValue(int paletteID, out FoliagePaletteSlotData slot)
        {
            slot = paletteSlots.Find(x => x.SlotIdx == paletteID);
            return slot != null;
        }

        public FoliagePaletteSlotData GetByPaletteID(int paletteID)
        {
            return paletteSlots.Find(x => x.SlotIdx == paletteID);
        }

        private bool CheckExistAndCreate(int paletteSlotIdx, bool createIfNotExists)
        {
            if (!paletteSlotsDict.ContainsKey(paletteSlotIdx))
            {
                if (createIfNotExists)
                {
                    paletteSlotsDict[paletteSlotIdx] = new HashSet<IFoliageElement>();
                    allMatrices[paletteSlotIdx] = new HashSet<Matrix4x4>();
                    return true;
                }
                Debug.LogWarning($"Foliage slot {paletteSlotIdx} is not found");
                return false;
            }
            return true;
        }

        public void AddFoliage(int paletteSlotIdx, IFoliageElement foliage, bool createIfNotExists = false)
        {
            if (CheckExistAndCreate(paletteSlotIdx, createIfNotExists))
            {
                paletteSlotsDict[paletteSlotIdx].Add(foliage);
                allMatrices[paletteSlotIdx].Add(foliage.GetMatrix());
            }
        }

        public void AddFoliages(int paletteSlotIdx, IEnumerable<IFoliageElement> foliages, bool createIfNotExists = false)
        {
            CodeTimer.Measure($"AddFoliages", () =>
            {
                if (CheckExistAndCreate(paletteSlotIdx, createIfNotExists))
                {
                    Debug.Log($"AddFoliages: {paletteSlotIdx} : {foliages.Count()}");
                    paletteSlotsDict[paletteSlotIdx].UnionWith(foliages);
                    foreach (var foliage in foliages)
                    {
                        allMatrices[paletteSlotIdx].Add(foliage.GetMatrix());
                    }
                }
            });
        }

        public void RemoveFoliage(int paletteSlotIdx, IFoliageElement foliage)
        {
            if (CheckExistAndCreate(paletteSlotIdx, false))
            {
                paletteSlotsDict[paletteSlotIdx].Remove(foliage);
                allMatrices[paletteSlotIdx].Remove(foliage.GetMatrix());
            }
        }

        public void RemoveFoliages(int paletteSlotIdx, IEnumerable<IFoliageElement> foliages)
        {
            if (CheckExistAndCreate(paletteSlotIdx, false))
            {
                paletteSlotsDict[paletteSlotIdx].ExceptWith(foliages);
                foreach (var foliage in foliages)
                {
                    allMatrices[paletteSlotIdx].Remove(foliage.GetMatrix());
                }
            }
        }

        public IEnumerable<IFoliageElement> GetFoliages()
        {
            foreach (var foliageSet in paletteSlotsDict.Values)
            {
                foreach (var foliage in foliageSet)
                {
                    yield return foliage;
                }
            }
        }

        public ulong GetChecksum()
        {
            return FoliageUtils.ComputeChecksum(paletteSlotsDict);
        }

        public void Dispose()
        {
            isInitialized = false;
        }
    }
}