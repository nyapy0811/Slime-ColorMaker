using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SlimeColorMaker
{
    /// <summary>
    /// 벽 빌더가 오브젝트를 만들고 고치고 지우는 곳. 에디터(Play 밖)에서는 프리팹 연결을 지키고 Undo에 기록한다.
    /// 런타임(UGC 맵을 불러올 때)에는 보통의 Instantiate·Destroy다.
    /// </summary>
    static class NodeBuildOps
    {
        const string UndoName = "벽 만들기";

        static bool Recording
        {
#if UNITY_EDITOR
            get => !Application.isPlaying;
#else
            get => false;
#endif
        }

        public static GameObject Spawn(GameObject prefab, Transform parent)
        {
#if UNITY_EDITOR
            if (Recording) return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
#endif
            return Object.Instantiate(prefab, parent);
        }

        public static Transform NewChild(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        /// <summary>
        /// 이름이 같은 자식을 지우고 빈 자식을 새로 만든다. 다 채운 뒤 <see cref="RegisterCreated"/>로 한 번에 Undo에 올린다.
        /// </summary>
        public static Transform ReplaceChild(Transform parent, string name)
        {
            var old = parent.Find(name);
            if (old != null) Destroy(old.gameObject);
            return NewChild(name, parent);
        }

        /// <summary>새로 만든 오브젝트를 자식까지 Undo에 올린다.</summary>
        public static void RegisterCreated(GameObject created)
        {
#if UNITY_EDITOR
            if (Recording) Undo.RegisterCreatedObjectUndo(created, UndoName);
#endif
        }

        /// <summary>남겨 두고 고칠 오브젝트를 고치기 전에 부른다.</summary>
        public static void Record(Object target)
        {
#if UNITY_EDITOR
            if (Recording) Undo.RecordObject(target, UndoName);
#endif
        }

        public static void Destroy(GameObject target)
        {
#if UNITY_EDITOR
            if (Recording)
            {
                Undo.DestroyObjectImmediate(target);
                return;
            }
#endif
            Object.Destroy(target);
        }
    }
}
