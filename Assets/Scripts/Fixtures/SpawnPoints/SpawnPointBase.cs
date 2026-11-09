using UnityEngine;

namespace ColorMaze
{
    /// <summary>
    /// 출발점 계열(시작 지점, 체크포인트)의 기반. 플레이어는 이 오브젝트의 위치(발밑)에 서서 이 오브젝트가 보는
    /// 방향을 본다. 위아래 기울기는 쓰지 않는다. 씬 뷰에서는 기즈모로 보인다(겉모습은 나중에).
    /// </summary>
    public abstract class SpawnPointBase : FixtureBase
    {
        /// <summary>플레이어가 바라볼 방향(좌우 회전만).</summary>
        public Quaternion SpawnRotation => Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Vector3 feet = transform.position;
            Gizmos.DrawWireCube(feet + Vector3.up * 0.25f, Vector3.one * 0.5f); // 므엥 크기(0.5 × 0.5 × 0.5, 2026-11-09)
            Vector3 forward = SpawnRotation * Vector3.forward;
            Gizmos.DrawLine(feet + Vector3.up * 0.25f, feet + Vector3.up * 0.25f + forward * 0.5f);
        }
    }
}
