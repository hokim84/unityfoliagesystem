using UnityEngine;
using UnityEditor;

namespace PWTA
{
    [CustomEditor(typeof(FoliageManager))]
    public class FoliageManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _foliageComputeShaderProp;
        private SerializedProperty _paletteAssetProp;
        private SerializedProperty _minSizeProp;
        private SerializedProperty _minHeightProp;
        private SerializedProperty _reloadTriggerProp;
        private SerializedProperty _terrainRoot;
        private SerializedProperty _geometryNodeTypeProp;

        private void OnEnable()
        {
            _foliageComputeShaderProp = serializedObject.FindProperty("_foliageComputeShader");
            _paletteAssetProp = serializedObject.FindProperty("_paletteAsset");            
            _terrainRoot = serializedObject.FindProperty("_terrainRoot");
            _minSizeProp = serializedObject.FindProperty("splitSize");
            _minHeightProp = serializedObject.FindProperty("splitHeight");
            _reloadTriggerProp = serializedObject.FindProperty("reloadTrigger");
            _geometryNodeTypeProp = serializedObject.FindProperty("_geometryNodeType");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((FoliageManager)target), typeof(MonoScript), false);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_foliageComputeShaderProp);
            EditorGUILayout.PropertyField(_paletteAssetProp);

            EditorGUILayout.Space();

            using(new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("World Density", GUILayout.Width(100));
                FoliageManager.WORLD_DENSITY_LEVEL = EditorGUILayout.IntSlider(FoliageManager.WORLD_DENSITY_LEVEL, 0, 10);
            }

            using(new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Draw Distance", GUILayout.Width(100));
                FoliageManager.DrawDistance = EditorGUILayout.FloatField(FoliageManager.DrawDistance);                
            }
            
            //LayerMaskField
            EditorGUILayout.PropertyField(_terrainRoot);
            EditorGUILayout.PropertyField(_minSizeProp);
            EditorGUILayout.PropertyField(_minHeightProp);
            EditorGUI.BeginChangeCheck();            
            bool geometryNodeChanged = EditorGUI.EndChangeCheck();

            EditorGUILayout.Space();            
            if (GUILayout.Button("Open [Grass Builder]", GUILayout.Height(30)))
            {
                GrassBuilderWindow.ShowWindow();
            }            
            if (GUILayout.Button("Open [Foliage Day]", GUILayout.Height(30)))
            {
                FoliageDayWindow.ShowWindow();
            }
            if (GUILayout.Button("Reload Foliages", GUILayout.Height(30)))
            {
                _reloadTriggerProp.boolValue = true;
            }            
            if (GUILayout.Button("Clear Foliages", GUILayout.Height(30)))
            {
                (target as FoliageManager).ClearFoliages();
            }

            serializedObject.ApplyModifiedProperties();
            if (geometryNodeChanged)
            {
                var manager = target as FoliageManager;
                manager?.ApplyGeometryNodeSelection();
            }
        }


    }
}