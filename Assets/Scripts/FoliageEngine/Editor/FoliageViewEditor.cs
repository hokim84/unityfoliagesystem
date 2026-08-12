using UnityEngine;
using UnityEditor;

namespace PWTA
{
    public class FoliageViewEditor
    {
        public enum Mode
        {
            edit,
            display,
        }

        public Mode mode = Mode.edit;
        private string _prefabName = "empty prefab";
        private UnityEditor.Editor _prefabEditor;
        private string _title;
        public int SlotIdx => _plantInfo.SlotIdx; 
        public static readonly int HEIGHT = 300;

        private GUISkin _guiSkin;

        private string _boundInfo;
        private string _vertexInfo;

        public static readonly int PREVIEW_WIDTH = 150;
        public static readonly int PREVIEW_HEIGHT = 260;

        private FoliagePaletteSlotData _plantInfo = new FoliagePaletteSlotData();
        private System.Action<FoliagePaletteSlotData> _onRemoveCallback;
        private System.Action<bool, FoliagePaletteSlotData> _onToggleCallback;
        private System.Action<FoliagePaletteSlotData> _onChangePrefabCallback;

        public FoliageViewEditor(FoliagePaletteSlotData plantInfo,
            System.Action<bool, FoliagePaletteSlotData> onToggle = null,
            System.Action<FoliagePaletteSlotData> onChangePrefab = null,
            System.Action<FoliagePaletteSlotData> onRemove = null, GUISkin skin = null)
        {
            _plantInfo = plantInfo;
            _guiSkin = skin;
            _onToggleCallback = onToggle;
            _onRemoveCallback = onRemove;
            _onChangePrefabCallback = onChangePrefab;
            SetPrefab(_plantInfo.prefab);
        }

        public void SetPrefab(GameObject prefab)
        {
            ReleasePreview();

            if (null == prefab)
                return;

            _title = $"#{_plantInfo.SlotIdx}";

            _plantInfo.prefab = prefab;
            _plantInfo.Initialize();
            _prefabEditor = UnityEditor.Editor.CreateEditor(prefab);
            _prefabEditor.Repaint();
            _prefabName = prefab.name;

            _boundInfo = "";
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length > 0 && null != renderers[0])
            {
                var bound = renderers[0].bounds;
                _boundInfo = $"크기: {(bound.max.x - bound.min.x).ToString("n2")} / " +
                             $"{(bound.max.y - bound.min.y).ToString("n2")} / " +
                             $"{(bound.max.z - bound.min.z).ToString("n2")}";
            }

            _vertexInfo = "";
            var meshes = prefab.GetComponentsInChildren<MeshFilter>();
            if (meshes.Length > 0 && meshes[0].sharedMesh)
            {
                _vertexInfo = $"정점 수: {meshes[0].sharedMesh.vertexCount}";
            }
        }

        public GameObject OnDraw(Rect layout, int selectedIndex)
        {
            if (null == _plantInfo)
                return null;

            var px = layout.x;
            var py = layout.y;

            var guiStyle = new GUIStyle();
            if (null != _guiSkin)
                guiStyle = _guiSkin.box;

            bool isSelected = _plantInfo.slotIdx == selectedIndex;
            //if (!isSelected)
            //    GUI.color = Color.gray;

            if (null != _plantInfo.prefab)
            {
                if (null != _prefabEditor)
                {
                    _prefabEditor.OnInteractivePreviewGUI(new Rect(px, py + 20, PREVIEW_WIDTH, 130), guiStyle);
                }
                py += 120;
            }

            var toggleValue = GUI.Toggle(new Rect(px, layout.y, 20, 20), isSelected, "");
            if (toggleValue != isSelected)
            {
                _onToggleCallback?.Invoke(toggleValue, _plantInfo);
            }

            GUI.Label(new Rect(px, py, PREVIEW_WIDTH, 20), _vertexInfo);
            py += 15;
            GUI.Label(new Rect(px, py, PREVIEW_WIDTH, 20), _boundInfo);
            py += 20;

            var _plantPrefab = _plantInfo.prefab;
            if (mode == Mode.edit)
            {
                var lastPrefab = _plantPrefab;
                _plantPrefab = EditorGUI.ObjectField(new Rect(px, py, PREVIEW_WIDTH, 20), "",
                    _plantPrefab, typeof(GameObject), false) as GameObject;

                if (lastPrefab != _plantPrefab)
                {
                    OnChangePrefab(_plantPrefab);
                }
            }

            py += 25;
            Rect rectValues = new Rect(px, py, PREVIEW_WIDTH, 20);
            {
                // _plantInfo.positionOffset = EditorGUI.Vector3Field(new Rect(px, py, _previewWidth, 20),
                //     new GUIContent("랜덤 위치(옵셋)"), _plantInfo.positionOffset);
                // rectValues.y += 40;

                // rectValues.width = _previewWidth;
                // EditorGUI.LabelField(rectValues, "랜덤 스케일(Min~Max)");
                // rectValues.x += 30;
                // rectValues.y += 20;
                // rectValues.width = 40;
                // _plantInfo.scaleOffset = EditorGUI.FloatField(rectValues, _plantInfo.scaleOffset);
                // rectValues.x += 45;
                // EditorGUI.LabelField(rectValues, " ~ ");
                // rectValues.x += 20;
                // _plantInfo.randomScaleMax = EditorGUI.FloatField(rectValues, _plantInfo.randomScaleMax);

                // rectValues.x = px;
                // rectValues.y += 25;
                // rectValues.width = 50;
                // EditorGUI.LabelField(rectValues, "비율 : ");
                // rectValues.x += 50;
                // _plantInfo.ratio = EditorGUI.FloatField(rectValues, _plantInfo.ratio);

                // rectValues.x = 10;
                // rectValues.y += 25;
                rectValues.width = PREVIEW_WIDTH;
            }

            var boxRect = new Rect(layout.x, layout.y, PREVIEW_WIDTH, rectValues.y + 25);
            GUI.Box(boxRect, _title);

            GUI.color = Color.red;
            if (GUI.Button(new Rect(layout.x + 10, rectValues.y, PREVIEW_WIDTH - 20, 20), "삭제"))
            {
                _onRemoveCallback?.Invoke(_plantInfo);
            }
            GUI.color = Color.white;

            //Process click event on Rect area
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                if (boxRect.Contains(Event.current.mousePosition))
                {
                    _onToggleCallback?.Invoke(isSelected, _plantInfo);
                }
            }

            return _plantPrefab;
        }

        private void OnChangePrefab(GameObject newPlant)
        {
            SetPrefab(newPlant);
            _onChangePrefabCallback?.Invoke(_plantInfo);
        }

        public void ReleasePreview()
        {
            if (null != _prefabEditor)
            {
                _prefabEditor.ResetTarget();
                GameObject.DestroyImmediate((_prefabEditor));
            }
        }
    }
}