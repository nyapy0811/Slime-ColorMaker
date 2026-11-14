using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// 재질감(<see cref="SurfaceFinish"/>)의 복셀 재질을 만들고 맞춘다(에디터 전용).
    /// <see cref="VoxPaint"/>의 재질감이 바뀌면 그 모델의 렌더러마다 원래 재질(.vox 가져오기 재질)에 재질감을 입힌 재질로 바꿔 끼운다.
    /// 그 재질은 재질감 애셋의 하위 애셋('모델 파일 이름/재질 이름')이라 같은 모델·같은 재질감끼리 하나를 같이 쓴다(묶어 그리기 유지).
    /// 재질감 값이 바뀌면 그 하위 재질들만 다시 맞추므로 프리팹을 다시 저장할 필요가 없다. 재질감을 비우면 원래 재질로 돌린다.
    /// 인스펙터 변경 중(OnValidate)에는 애셋을 만들 수 없어 한 틱 미뤄 처리한다.
    /// </summary>
    [InitializeOnLoad]
    public static class SurfaceFinishMaterials
    {
        static readonly HashSet<VoxPaint> paints = new();
        static readonly HashSet<SurfaceFinish> finishes = new();
        static bool scheduled;

        static SurfaceFinishMaterials()
        {
            VoxPaint.FinishesChanged += paint =>
            {
                paints.Add(paint);
                Schedule();
            };
            SurfaceFinish.Changed += finish =>
            {
                finishes.Add(finish);
                Schedule();
            };
        }

        static void Schedule()
        {
            if (scheduled) return;
            scheduled = true;
            EditorApplication.delayCall += Flush;
        }

        static void Flush()
        {
            scheduled = false;
            foreach (var finish in finishes.ToList())
                if (finish != null) Refresh(finish);
            foreach (var paint in paints.ToList())
                if (paint != null) Apply(paint);
            finishes.Clear();
            paints.Clear();
        }

        /// <summary>재질감의 하위 재질들을 지금 값으로 맞춘다.</summary>
        static void Refresh(SurfaceFinish finish)
        {
            bool changed = false;
            foreach (var material in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(finish)).OfType<Material>())
            {
                if (finish.Matches(material)) continue;
                finish.ApplyTo(material);
                EditorUtility.SetDirty(material);
                changed = true;
            }
            if (changed) AssetDatabase.SaveAssetIfDirty(finish);
        }

        /// <summary>모델의 렌더러마다 그 부위의 재질감대로 재질을 끼운다(원래 재질은 .vox 가져오기 재질). 바로 처리한다(OnValidate 안에서는 부르지 않는다).</summary>
        public static void Apply(VoxPaint paint)
        {
            // 가져온 모델 애셋(.vox) 자체는 고칠 수 없다. 프리팹 애셋·프리팹 편집·씬의 오브젝트만 고친다.
            string path = AssetDatabase.GetAssetPath(paint);
            if (path.Length > 0 && !path.EndsWith(".prefab")) return;

            foreach (var renderer in paint.GetComponentsInChildren<Renderer>(true))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(renderer);
                if (source == null || source == renderer) continue; // 모델 애셋과 이어지지 않은 렌더러는 원래 재질을 모른다.
                var finish = paint.FinishFor(renderer);
                var wanted = source.sharedMaterials.Select(original => finish == null || original == null ? original : MaterialFor(finish, original)).ToArray();
                if (wanted.SequenceEqual(renderer.sharedMaterials)) continue;
                Undo.RecordObject(renderer, "재질감");
                renderer.sharedMaterials = wanted;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                EditorUtility.SetDirty(renderer);
            }
        }

        /// <summary>원래 재질에 재질감을 입힌 재질(재질감 애셋의 하위 애셋, 없으면 만듦).</summary>
        static Material MaterialFor(SurfaceFinish finish, Material original)
        {
            string name = $"{Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(original))}/{original.name}";
            string finishPath = AssetDatabase.GetAssetPath(finish);
            var existing = AssetDatabase.LoadAllAssetsAtPath(finishPath).OfType<Material>().FirstOrDefault(material => material.name == name);
            if (existing != null) return existing;
            var made = new Material(original) { name = name };
            finish.ApplyTo(made);
            AssetDatabase.AddObjectToAsset(made, finish);
            AssetDatabase.SaveAssetIfDirty(finish);
            return made;
        }
    }
}
