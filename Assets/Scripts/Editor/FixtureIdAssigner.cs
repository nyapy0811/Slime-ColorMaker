using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeColorMaker
{
    /// <summary>
    /// 맵 안 id를 발급한다. 빈 id(0 이하)와 겹친 id(계층에서 뒤에 있는 쪽)만 새 번호로 바꾸고,
    /// 새 번호는 지금 가장 큰 id + 1부터 준다. 첫 버전이라 놓거나 복제할 때 자동으로 주지 않고 메뉴로 고친다.
    /// </summary>
    public static class FixtureIdAssigner
    {
        [MenuItem("Slime ~ColorMaker~/맵 안 id 고치기")]
        static void FixActiveScene()
        {
            int changed = FixIds(SceneManager.GetActiveScene());
            Debug.Log(changed > 0 ? $"[맵 안 id] {changed}개에 새 id를 줬다." : "[맵 안 id] 고칠 id가 없다.");
        }

        /// <summary>씬의 빈 id와 겹친 id를 고치고 고친 개수를 돌려준다.</summary>
        public static int FixIds(Scene scene)
        {
            // 계층 순서대로 본다. 복제본은 원본 바로 뒤에 생기므로 복제본이 새 번호를 받는다.
            var fixtures = FixturesIn(scene).ToList();
            int next = fixtures.Select(f => f.FixtureId).DefaultIfEmpty(0).Max() + 1;
            var used = new HashSet<int>();
            int changed = 0;
            foreach (var fixture in fixtures)
            {
                if (fixture.FixtureId > 0 && used.Add(fixture.FixtureId)) continue;
                var serialized = new SerializedObject(fixture);
                serialized.FindProperty("fixtureId").intValue = next;
                serialized.ApplyModifiedProperties(); // 메뉴에서 되돌릴 수 있게 Undo에 남긴다.
                used.Add(next++);
                changed++;
            }
            if (changed > 0)
            {
                // 프리팹 인스턴스의 Undo 기록은 프레임 끝에 처리돼, 같은 프레임에 저장하면 저장 뒤에 씬이 다시 '바뀜'이 된다.
                // 여기서 기록을 마무리하고 씬 변경 표시도 직접 한다(인스턴스 값만 바뀌면 표시가 안 붙는다).
                Undo.FlushUndoRecordObjects();
                EditorSceneManager.MarkSceneDirty(scene);
            }
            return changed;
        }

        public static IEnumerable<FixtureBase> FixturesIn(Scene scene) =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FixtureBase>(true));
    }
}
