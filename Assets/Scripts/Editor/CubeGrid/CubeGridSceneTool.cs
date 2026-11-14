using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlimeColorMaker
{
    /// <summary>
    /// 씬 뷰의 큐브 맵 편집 도구(v1 MazeGeneratorEditor 조작). 큐브 격자를 고르면 씬 뷰 툴바에 나타난다(큐브 맵 편집 창으로도 켬).
    /// 클릭: 가리킨 면 쪽 칸에 고른 붓으로 놓기(계단은 카메라가 보는 쪽으로 오름) / Ctrl+클릭: 가리킨 칸 지우기 /
    /// Shift+드래그: 직사각형 범위에 블록 놓기 / Shift+Ctrl+드래그: 범위 지우기.
    /// 가리킨 칸은 콜라이더가 아니라 격자로 찾는다(블록·계단·기물·시험 모델이 든 칸). 아무것도 가리키지 않으면 높이 0의 바닥면을 쓴다
    /// (블록은 그 아래 y = −1 칸, 계단과 기물은 그 위 y = 0 칸). 범위는 시작한 칸을 지나고 가리킨 면에 수직인 평면에서 정한다(v1과 같음).
    /// </summary>
    [EditorTool("큐브 맵 편집", typeof(CubeGrid))]
    public class CubeGridSceneTool : EditorTool
    {
        const float MaxDistance = 500f;

        static readonly Color PlaceColor = new(0.3f, 1f, 0.4f, 1f);
        static readonly Color RemoveColor = new(1f, 0.25f, 0.25f, 1f);

        bool dragging;
        bool painting;      // 코팅 붓으로 칠하는(지우는) 중
        bool paintErase;
        int paintGroup;
        bool hasLastPaint;
        Vector3 lastPaint;
        bool dragRemove;
        int dragAxis;
        Vector3Int dragStart;
        Vector3Int dragEnd;

        public override GUIContent toolbarIcon => new("큐브", "큐브 맵 편집 (Slime~ColorMaker~)");

        public override void OnToolGUI(EditorWindow window)
        {
            if (window is not SceneView) return;
            var grid = (CubeGrid)target;
            var e = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);

            switch (e.GetTypeForControl(control))
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(control); // 클릭이 다른 오브젝트를 고르지 않게 한다.
                    break;
                case EventType.MouseMove:
                    window.Repaint();
                    break;
                case EventType.MouseDown when e.button == 0 && !e.alt:
                    OnMouseDown(grid, e, control);
                    break;
                case EventType.MouseDrag when painting && GUIUtility.hotControl == control:
                    PaintAt(grid, e.mousePosition);
                    window.Repaint();
                    e.Use();
                    break;
                case EventType.MouseUp when painting && GUIUtility.hotControl == control:
                    GUIUtility.hotControl = 0;
                    painting = false;
                    Undo.CollapseUndoOperations(paintGroup);
                    e.Use();
                    break;
                case EventType.MouseDrag when dragging && GUIUtility.hotControl == control:
                    UpdateDrag(e.mousePosition);
                    window.Repaint();
                    e.Use();
                    break;
                case EventType.MouseUp when dragging && GUIUtility.hotControl == control:
                    GUIUtility.hotControl = 0;
                    dragging = false;
                    var cells = CubeGridRules.Box(dragStart, dragEnd);
                    if (dragRemove) CubeGridEditing.Remove(grid, cells);
                    else CubeGridEditing.PlaceBlocks(grid, cells);
                    e.Use();
                    break;
                case EventType.Repaint:
                    Draw(grid, e);
                    break;
            }
        }

        void OnMouseDown(CubeGrid grid, Event e, int control)
        {
            // 코팅 붓: 드래그 한 번이 칠하기(Ctrl이면 지우기) 한 번이다.
            if (CubeGridEditing.SelectedBrush == CubeGridEditing.Brush.Coating)
            {
                Undo.IncrementCurrentGroup();
                paintGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(e.control ? "코팅 지우기" : "코팅 칠하기");
                painting = true;
                paintErase = e.control;
                hasLastPaint = false;
                GUIUtility.hotControl = control;
                PaintAt(grid, e.mousePosition);
                e.Use();
                return;
            }
            bool hit = Pick(grid, e.mousePosition, out var cell, out var normal);
            if (e.shift)
            {
                dragging = true;
                dragRemove = e.control;
                dragAxis = hit ? DominantAxis(normal) : 1;
                dragStart = hit ? cell : GroundCell(e.mousePosition, CubeGridEditing.Brush.Block) ?? Vector3Int.zero;
                dragEnd = dragStart;
                GUIUtility.hotControl = control;
            }
            else if (e.control)
            {
                if (hit) CubeGridEditing.Remove(grid, new[] { cell });
            }
            else
            {
                var placeAt = hit ? cell + normal : GroundCell(e.mousePosition, CubeGridEditing.SelectedBrush);
                if (placeAt.HasValue) CubeGridEditing.Place(grid, placeAt.Value, CubeGridEditing.StairTurn(CameraForward()));
            }
            e.Use();
        }

        // 가리킨 칸(블록이나 기물이 든 칸)과 들어간 면.
        static bool Pick(CubeGrid grid, Vector2 mouse, out Vector3Int cell, out Vector3Int normal)
        {
            var objects = CubeGridEditing.ObjectCells(grid);
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            return CubeGridRules.Raycast(ray.origin, ray.direction, MaxDistance, c => CubeGridEditing.IsFilled(grid, objects, c), out cell, out normal);
        }

        // 마우스가 가리킨 블록 윗면의 점(블록 윗면이 아니면 false). 블록만 막는 것으로 본다: 기물·기둥·계단 칸은 한 칸 상자로 잡혀
        // 그 옆 바닥을 가리켜도 시선을 막으므로, 코팅(블록 윗면만 칠함)은 그 칸을 지나 뒤의 블록 윗면을 찾는다.
        static bool PickTop(CubeGrid grid, Vector2 mouse, out Vector3 point)
        {
            point = default;
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            if (!CubeGridRules.Raycast(ray.origin, ray.direction, MaxDistance, c => CubeGridRules.HasBlock(grid.Data, c), out var cell, out var normal)
                || normal != Vector3Int.up) return false;
            if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;
            point = ray.GetPoint((cell.y + 1f - ray.origin.y) / ray.direction.y);
            return true;
        }

        // 가리킨 블록 윗면을 붓으로 칠한다. 같은 높이에서 이어 끌면 앞 점과의 사이도 채워 빠르게 끌어도 끊기지 않는다.
        void PaintAt(CubeGrid grid, Vector2 mouse)
        {
            if (!PickTop(grid, mouse, out var point)) return;
            int size = CubeGridEditing.CoatingBrushSize;
            var pixels = new List<(Vector3Int, int, int)>();
            if (hasLastPaint && Mathf.Abs(lastPaint.y - point.y) < 0.001f)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(lastPaint, point) * CubeGridRules.CoatingPixels * 2f));
                for (int i = 1; i <= steps; i++) pixels.AddRange(CubeGridRules.CoatingBrush(Vector3.Lerp(lastPaint, point, (float)i / steps), point.y, size));
            }
            else pixels.AddRange(CubeGridRules.CoatingBrush(point, point.y, size));
            lastPaint = point;
            hasLastPaint = true;
            CubeGridEditing.PaintCoating(grid, pixels, paintErase ? CoatingKind.None : CubeGridEditing.SelectedCoatingKind);
        }

        // 코팅 붓이 덮을 픽셀 범위의 테두리(픽셀 경계를 따라).
        static void DrawCoatingBrush(Vector3 point)
        {
            const float pixel = 1f / CubeGridRules.CoatingPixels;
            var covered = new HashSet<Vector2Int>();
            foreach (var (cell, x, z) in CubeGridRules.CoatingBrush(point, point.y, CubeGridEditing.CoatingBrushSize))
                covered.Add(new Vector2Int(cell.x * CubeGridRules.CoatingPixels + x, cell.z * CubeGridRules.CoatingPixels + z));
            float y = point.y + 0.002f;
            var lines = new List<Vector3>();
            foreach (var p in covered)
            {
                float x0 = p.x * pixel - 0.5f, z0 = p.y * pixel - 0.5f, x1 = x0 + pixel, z1 = z0 + pixel;
                if (!covered.Contains(p + Vector2Int.left)) { lines.Add(new Vector3(x0, y, z0)); lines.Add(new Vector3(x0, y, z1)); }
                if (!covered.Contains(p + Vector2Int.right)) { lines.Add(new Vector3(x1, y, z0)); lines.Add(new Vector3(x1, y, z1)); }
                if (!covered.Contains(p + Vector2Int.down)) { lines.Add(new Vector3(x0, y, z0)); lines.Add(new Vector3(x1, y, z0)); }
                if (!covered.Contains(p + Vector2Int.up)) { lines.Add(new Vector3(x0, y, z1)); lines.Add(new Vector3(x1, y, z1)); }
            }
            Handles.DrawLines(lines.ToArray());
        }

        // 높이 0의 바닥면 위 칸: 블록과 컨베이어는 바닥면 아래 칸(윗면이 0), 기물은 바닥면 위 칸.
        static Vector3Int? GroundCell(Vector2 mouse, CubeGridEditing.Brush brush)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance) || distance > MaxDistance) return null;
            var point = ray.GetPoint(distance);
            return new Vector3Int(Mathf.RoundToInt(point.x), brush == CubeGridEditing.Brush.Block || CubeGridEditing.IsConveyor(brush) ? -1 : 0, Mathf.RoundToInt(point.z));
        }

        static Vector3 CameraForward()
        {
            var camera = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : null;
            return camera != null ? camera.transform.forward : Vector3.forward;
        }

        static int DominantAxis(Vector3Int normal) => normal.x != 0 ? 0 : normal.y != 0 ? 1 : 2;

        // 시작한 칸의 중심을 지나고 고정축에 수직인 평면과 마우스 광선이 만나는 칸.
        void UpdateDrag(Vector2 mouse)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            var axis = Vector3.zero;
            axis[dragAxis] = 1f;
            if (!new Plane(axis, CubeGridRules.CellCenter(dragStart)).Raycast(ray, out float distance)) return;
            var cell = CubeGridRules.CellAt(ray.GetPoint(distance));
            cell[dragAxis] = dragStart[dragAxis];
            dragEnd = cell;
        }

        void Draw(CubeGrid grid, Event e)
        {
            if (CubeGridEditing.SelectedBrush == CubeGridEditing.Brush.Coating)
            {
                if (!PickTop(grid, e.mousePosition, out var point)) return;
                var zTestBefore = Handles.zTest;
                Handles.zTest = CompareFunction.LessEqual;
                Handles.color = (painting ? paintErase : e.control) ? RemoveColor : PlaceColor;
                DrawCoatingBrush(point);
                Handles.zTest = zTestBefore;
                return;
            }
            var cells = new List<Vector3Int>();
            bool remove;
            if (dragging)
            {
                cells.AddRange(CubeGridRules.Box(dragStart, dragEnd));
                remove = dragRemove;
            }
            else
            {
                remove = e.control;
                bool hit = Pick(grid, e.mousePosition, out var cell, out var normal);
                if (hit) cells.Add(remove ? cell : cell + normal);
                else if (!remove && GroundCell(e.mousePosition, CubeGridEditing.SelectedBrush) is { } ground) cells.Add(ground);
            }

            var zTest = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;
            Handles.color = remove ? RemoveColor : PlaceColor;
            foreach (var cell in cells) Handles.DrawWireCube(CubeGridRules.CellCenter(cell), Vector3.one * 1.001f);
            // 계단 붓이면 오르는 경사를 보인다(칸의 뒤쪽 아래 모서리 → 앞쪽 위 모서리).
            if (!dragging && !remove && CubeGridEditing.SelectedBrush == CubeGridEditing.Brush.Stair && cells.Count == 1)
            {
                var rotation = CubeGridRules.StairRotation(CubeGridEditing.StairTurn(CameraForward()));
                var center = CubeGridRules.CellCenter(cells[0]);
                foreach (float side in new[] { -0.4f, 0f, 0.4f })
                    Handles.DrawLine(center + rotation * new Vector3(side, -0.5f, -0.5f), center + rotation * new Vector3(side, 0.5f, 0.5f));
            }
            // 방벽 붓이면 판이 놓일 면에 사각형을 그린다.
            if (!dragging && !remove && CubeGridEditing.SelectedBrush == CubeGridEditing.Brush.Barrier && cells.Count == 1)
            {
                var rotation = CellFaceMount.Rotation(CubeGridEditing.BarrierFace(CubeGridEditing.StairTurn(CameraForward())));
                var center = CubeGridRules.CellCenter(cells[0]);
                var corners = new[] { new Vector3(-0.45f, -0.45f, -0.45f), new Vector3(-0.45f, 0.45f, -0.45f), new Vector3(-0.45f, 0.45f, 0.45f), new Vector3(-0.45f, -0.45f, 0.45f) };
                for (int i = 0; i < 4; i++) Handles.DrawLine(center + rotation * corners[i], center + rotation * corners[(i + 1) % 4]);
                Handles.DrawLine(center + rotation * corners[0], center + rotation * corners[2]);
            }
            // 차단기 붓이면 칸 뒤쪽(카메라 쪽) 가장자리에 막대 자리를 보인다.
            if (!dragging && !remove && CubeGridEditing.SelectedBrush == CubeGridEditing.Brush.BoomGate && cells.Count == 1)
            {
                var rotation = CubeGridRules.StairRotation(CubeGridEditing.StairTurn(CameraForward()));
                var center = CubeGridRules.CellCenter(cells[0]);
                foreach (float height in new[] { -0.3f, -0.25f })
                    Handles.DrawLine(center + rotation * new Vector3(-0.5f, height, -0.45f), center + rotation * new Vector3(0.5f, height, -0.45f));
            }
            // 컨베이어 붓이면 윗면에 흐르는 방향 화살표를 그린다.
            if (!dragging && !remove && CubeGridEditing.SelectedBrush == CubeGridEditing.Brush.ConveyorBelt && CubeGridEditing.SelectedConveyorShape == ConveyorShape.Straight && cells.Count == 1)
            {
                var rotation = CubeGridRules.StairRotation(CubeGridEditing.StairTurn(CameraForward()));
                var top = CubeGridRules.CellCenter(cells[0]) + Vector3.up * 0.5f;
                var tip = top + rotation * new Vector3(0f, 0f, 0.4f);
                Handles.DrawLine(top + rotation * new Vector3(0f, 0f, -0.4f), tip);
                Handles.DrawLine(tip, top + rotation * new Vector3(-0.2f, 0f, 0.2f));
                Handles.DrawLine(tip, top + rotation * new Vector3(0.2f, 0f, 0.2f));
            }
            // 컨베이어 꺾임 붓이면 윗면에 들어와 꺾여 나가는 화살표를 그린다(안쪽 모서리 중심, 반지름 0.5의 호).
            if (!dragging && !remove && CubeGridEditing.SelectedBrush == CubeGridEditing.Brush.ConveyorBelt && CubeGridEditing.SelectedConveyorShape != ConveyorShape.Straight && cells.Count == 1)
            {
                var rotation = CubeGridRules.StairRotation(CubeGridEditing.ConveyorExitTurn(CubeGridEditing.StairTurn(CameraForward())));
                var top = CubeGridRules.CellCenter(cells[0]) + Vector3.up * 0.5f;
                float side = CubeGridEditing.SelectedConveyorShape == ConveyorShape.Right ? 1f : -1f; // 들어오는 면(x)
                var corner = new Vector3(0.5f * side, 0f, 0.5f);
                var previous = top + rotation * (corner + new Vector3(0f, 0f, -0.5f));
                for (int i = 1; i <= 8; i++)
                {
                    float angle = i / 8f * Mathf.PI / 2f;
                    var next = top + rotation * (corner + new Vector3(-side * 0.5f * Mathf.Sin(angle), 0f, -0.5f * Mathf.Cos(angle)));
                    Handles.DrawLine(previous, next);
                    previous = next;
                }
                Handles.DrawLine(previous, top + rotation * new Vector3(-0.15f, 0f, 0.3f));
                Handles.DrawLine(previous, top + rotation * new Vector3(0.15f, 0f, 0.3f));
            }
            Handles.zTest = zTest;
        }
    }
}
