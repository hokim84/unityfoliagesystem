// Assets/Editor/TerrainTileRenamer.cs 같은 경로에 두면 됨
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class TerrainTileRenamer
{
    // "이름 x[숫자] y[숫자]" 패턴 매칭
    // 예: "Geizan_Terrain_Data x[0] y[0]"
    private const string Pattern = @"^(?<base>.+)\s+x\[(?<x>\d+)\]\s+y\[(?<y>\d+)\]$";

    [MenuItem("Tools/Terrain/Rename Terrain Name")]
    public static void RenameSelected()
    {
        var selected = Selection.objects;
        if (selected == null || selected.Length == 0)
        {
            Debug.LogWarning("선택된 에셋이 없어. Project 뷰에서 변경할 에셋들을 선택해줘.");
            return;
        }

        var regex = new Regex(Pattern, RegexOptions.Compiled);
        int renamedCount = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var obj in selected)
            {
                if (obj == null)
                    continue;

                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path))
                    continue;

                var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                var match = regex.Match(fileName);
                if (!match.Success)
                    continue;

                var baseName = match.Groups["base"].Value.TrimEnd();
                var x = match.Groups["x"].Value;
                var y = match.Groups["y"].Value;

                // 새로운 이름: base_x_값__y_값_
                var newName = $"{baseName}_x_{x}__y_{y}_";

                if (newName == fileName)
                    continue;

                var error = AssetDatabase.RenameAsset(path, newName);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"이름 변경 실패: {fileName} → {newName} : {error}");
                }
                else
                {
                    renamedCount++;
                    Debug.Log($"이름 변경: {fileName} → {newName}");
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"TerrainTileRenamer: {renamedCount}개 에셋 이름 변경 완료.");
    }
}
