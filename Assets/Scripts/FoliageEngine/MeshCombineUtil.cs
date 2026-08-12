using System.Collections.Generic;
using UnityEngine;

public static class MeshCombineUtil
{
    /// <summary>
    /// MeshFilter 배열을 하나의 Mesh로 합친다.
    /// - 각 MeshFilter의 transform을 bake해서 합침.
    /// - 출력 Mesh는 읽기/쓰기 가능한 새 Mesh.
    /// - 32bit index 자동 전환.
    /// </summary>
    /// <param name="meshFilters">합칠 MeshFilter들</param>
    /// <param name="root">
    /// 합쳐진 Mesh가 놓일 기준 Transform.
    /// null이면 첫 유효 MeshFilter의 transform을 기준으로 사용.
    /// </param>
    /// <param name="mergeSubMeshes">
    /// true면 submesh를 하나로 합침(보통 에디트/디버그용 추천).
    /// false면 원본 submesh 구조를 최대한 유지.
    /// </param>
    public static Mesh CombineMeshes(MeshFilter[] meshFilters, Transform root = null)
    {
        if (meshFilters == null || meshFilters.Length == 0)
            return null;

        // 유효한 첫 MeshFilter 찾기
        MeshFilter firstValid = null;
        for (int i = 0; i < meshFilters.Length; i++)
        {
            var mf = meshFilters[i];
            if (mf != null && mf.sharedMesh != null)
            {
                firstValid = mf;
                break;
            }
        }
        if (firstValid == null)
            return null;

        if (root == null)
            root = firstValid.transform;

        // root 로컬 기준으로 bake 하기 위한 역행렬
        // 각 타일의 localToWorld을 root worldToLocal로 변환해서 합친 메쉬 로컬로 가져옴
        Matrix4x4 worldToRoot = root.worldToLocalMatrix;

        var combines = new List<CombineInstance>(meshFilters.Length * 2);

        for (int i = 0; i < meshFilters.Length; i++)
        {
            var mf = meshFilters[i];
            if (mf == null) continue;

            var mesh = mf.sharedMesh;
            if (mesh == null) continue;

            // 각 submesh를 개별 CombineInstance로 넣으면 submesh 유지 가능
            int subMeshCount = Mathf.Max(1, mesh.subMeshCount);

            Matrix4x4 toRoot = worldToRoot * mf.transform.localToWorldMatrix;
            // submesh를 통째로 하나로 합치려면 mesh 전체를 한 번만 추가하면 됨
            combines.Add(new CombineInstance
            {
                mesh = mesh,
                subMeshIndex = 0, // mergeSubMeshes=true라도 CombineMeshes는 mesh의 submeshIndex를 참조함
                transform = toRoot
            });
        }

        if (combines.Count == 0)
            return null;

        // mergeSubMeshes=true일 때, 각 메쉬의 모든 submesh를 포함시키기 위해
        // 각 입력 mesh를 "단일 submesh"로 평탄화한 뒤 합친다.
        
        var flattened = new List<CombineInstance>(combines.Count * 2);
        flattened.Clear();

        for (int i = 0; i < meshFilters.Length; i++)
        {
            var mf = meshFilters[i];
            if (mf == null || mf.sharedMesh == null) continue;

            var src = mf.sharedMesh;
            var toRoot = worldToRoot * mf.transform.localToWorldMatrix;

            var flat = FlattenToSingleSubmesh(src);
            flattened.Add(new CombineInstance
            {
                mesh = flat,
                subMeshIndex = 0,
                transform = toRoot
            });
        }

        // 실제 결합
        var outMesh = new Mesh();
        outMesh.name = "CombinedMesh";

        // 32bit index 자동 전환
        // (Unity 2017.3+에서 지원. 요즘 유니티면 문제 없음)
        outMesh.indexFormat = (EstimateVertexCount(meshFilters) > 65535)
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        outMesh.CombineMeshes(flattened.ToArray(), true, true, false);
        outMesh.RecalculateBounds();
        // outMesh.RecalculateNormals(); // 필요하면 켜

        return outMesh;       
        
    }

    // 입력 Mesh의 모든 submesh를 하나로 평탄화한 복사본을 만든다.
    // CombineMeshes에서 subMeshIndex=0만 넣는 문제를 피하기 위한 용도.
    static Mesh FlattenToSingleSubmesh(Mesh src)
    {
        int sm = Mathf.Max(1, src.subMeshCount);
        if (sm == 1) return src;

        // src를 그대로 복사하되, triangles를 모든 submesh에서 모아서 하나로 만든다.
        var m = Object.Instantiate(src);
        m.name = src.name + "_Flat";

        // triangles 합치기
        int totalIndexCount = 0;
        for (int i = 0; i < sm; i++)
            totalIndexCount += (int)src.GetIndexCount(i);

        var tris = new int[totalIndexCount];
        int offset = 0;
        for (int i = 0; i < sm; i++)
        {
            var t = src.GetTriangles(i);
            t.CopyTo(tris, offset);
            offset += t.Length;
        }

        m.subMeshCount = 1;
        m.SetTriangles(tris, 0, true);
        return m;
    }

    static int EstimateVertexCount(MeshFilter[] mfs)
    {
        int v = 0;
        if (mfs == null) return 0;
        for (int i = 0; i < mfs.Length; i++)
        {
            var mf = mfs[i];
            if (mf == null || mf.sharedMesh == null) continue;
            v += mf.sharedMesh.vertexCount;
        }
        return v;
    }
}