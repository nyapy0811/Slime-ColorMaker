using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SlimeColorMaker
{
    /// <summary>
    /// Blockbench 프로젝트 파일(.bbmodel, JSON, 형식 5.0 확인)을 읽은 결과. 좌표는 파일 그대로(Blockbench 픽셀, 오른손 좌표)다.
    /// 큐브(elements), 그룹(groups + outliner 나무), 텍스처(파일 안 data URL 또는 옆 PNG 경로)만 읽는다(Docs/v2 블록벤치 모델링 규칙).
    /// 4.x 파일처럼 outliner 안에 그룹 내용이 바로 들어 있는 경우도 읽는다.
    /// </summary>
    public class BBModelFile
    {
        public static readonly string[] FaceNames = { "east", "west", "up", "down", "south", "north" };

        public class Face
        {
            public float[] uv = new float[4];
            /// <summary>텍스처 번호. -1이면 텍스처 없음(면은 그림).</summary>
            public int texture = -1;
            public int rotation;
        }

        public class Node
        {
            public string uuid, name;
            public Group parent;
            public bool export = true;
            public Vector3 origin, rotation;
        }

        public class Cube : Node
        {
            public Vector3 from, to;
            public float inflate;
            /// <summary>그리는 면(east, west, up, down, south, north 순서의 이름 → 면). 꺼진 면(texture null)은 없다.</summary>
            public readonly Dictionary<string, Face> faces = new();
        }

        public class Group : Node
        {
            public readonly List<Node> children = new();
        }

        public class Texture
        {
            public string name;
            public int width, height, uvWidth, uvHeight;
            /// <summary>PNG 바이트(파일 안에 넣은 경우). 없으면 <see cref="relativePath"/>의 파일.</summary>
            public byte[] png;
            public string relativePath;
        }

        public int resolutionWidth = 16, resolutionHeight = 16;
        /// <summary>맨 위 노드(outliner 순서).</summary>
        public readonly List<Node> roots = new();
        public readonly List<Texture> textures = new();
        public readonly List<string> warnings = new();

        public IEnumerable<Cube> Cubes => roots.SelectMany(Walk).OfType<Cube>();

        static IEnumerable<Node> Walk(Node node)
        {
            yield return node;
            if (node is Group group)
                foreach (var child in group.children.SelectMany(Walk)) yield return child;
        }

        public static BBModelFile Parse(string json)
        {
            var root = JObject.Parse(json);
            var file = new BBModelFile();
            if (root["resolution"] is JObject resolution)
            {
                file.resolutionWidth = resolution.Value<int?>("width") ?? 16;
                file.resolutionHeight = resolution.Value<int?>("height") ?? 16;
            }

            var cubes = new Dictionary<string, Cube>();
            foreach (var element in root["elements"] as JArray ?? new JArray())
            {
                string type = element.Value<string>("type") ?? "cube";
                if (type != "cube")
                {
                    file.warnings.Add($"큐브가 아닌 요소({type}, {element.Value<string>("name")})는 읽지 않는다.");
                    continue;
                }
                var cube = new Cube
                {
                    uuid = element.Value<string>("uuid"),
                    name = element.Value<string>("name"),
                    from = Vec(element["from"]),
                    to = Vec(element["to"]),
                    origin = Vec(element["origin"]),
                    rotation = Vec(element["rotation"]),
                    inflate = element.Value<float?>("inflate") ?? 0f,
                    export = element.Value<bool?>("export") ?? true,
                };
                if (element["faces"] is JObject faces)
                    foreach (string faceName in FaceNames)
                    {
                        if (faces[faceName] is not JObject data) continue;
                        var texture = data["texture"];
                        if (texture != null && texture.Type == JTokenType.Null) continue; // 꺼진 면
                        var face = new Face
                        {
                            texture = texture != null && texture.Type == JTokenType.Integer ? texture.Value<int>() : -1,
                            rotation = data.Value<int?>("rotation") ?? 0,
                        };
                        if (data["uv"] is JArray uv && uv.Count == 4) face.uv = uv.Select(v => v.Value<float>()).ToArray();
                        cube.faces[faceName] = face;
                    }
                if (cube.uuid != null) cubes[cube.uuid] = cube;
            }

            var groupData = new Dictionary<string, JObject>();
            foreach (var group in root["groups"] as JArray ?? new JArray())
                if (group is JObject data && data.Value<string>("uuid") is string uuid)
                    groupData[uuid] = data;

            foreach (var node in root["outliner"] as JArray ?? new JArray())
                if (ReadNode(node, null, cubes, groupData) is Node read) file.roots.Add(read);

            foreach (var texture in root["textures"] as JArray ?? new JArray())
            {
                var read = new Texture
                {
                    name = texture.Value<string>("name"),
                    width = texture.Value<int?>("width") ?? 0,
                    height = texture.Value<int?>("height") ?? 0,
                    uvWidth = texture.Value<int?>("uv_width") ?? file.resolutionWidth,
                    uvHeight = texture.Value<int?>("uv_height") ?? file.resolutionHeight,
                    relativePath = texture.Value<string>("relative_path"),
                };
                string source = texture.Value<string>("source");
                int comma = source?.IndexOf(',') ?? -1;
                if (source != null && source.StartsWith("data:image/png;base64") && comma > 0)
                    read.png = Convert.FromBase64String(source[(comma + 1)..]);
                file.textures.Add(read);
            }
            return file;
        }

        static Node ReadNode(JToken token, Group parent, Dictionary<string, Cube> cubes, Dictionary<string, JObject> groupData)
        {
            if (token.Type == JTokenType.String)
            {
                if (!cubes.TryGetValue(token.Value<string>(), out var cube)) return null;
                cube.parent = parent;
                return cube;
            }
            if (token is not JObject node) return null;
            string uuid = node.Value<string>("uuid");
            // 5.0: 그룹 내용은 groups에, outliner에는 uuid와 children만. 4.x: outliner에 그룹 내용이 바로 있음.
            var data = uuid != null && groupData.TryGetValue(uuid, out var found) ? found : node;
            var group = new Group
            {
                uuid = uuid,
                name = data.Value<string>("name") ?? "group",
                origin = Vec(data["origin"]),
                rotation = Vec(data["rotation"]),
                export = data.Value<bool?>("export") ?? true,
                parent = parent,
            };
            foreach (var child in node["children"] as JArray ?? new JArray())
                if (ReadNode(child, group, cubes, groupData) is Node read) group.children.Add(read);
            return group;
        }

        static Vector3 Vec(JToken token) =>
            token is JArray array && array.Count >= 3 ? new Vector3(array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>()) : Vector3.zero;

        /// <summary>텍스처 그림(RGBA32, 읽기 가능). 파일 안에 없으면 .bbmodel 옆 경로에서 읽는다. 없으면 null.</summary>
        public static Texture2D LoadImage(Texture texture, string modelPath, out string externalPath)
        {
            externalPath = null;
            byte[] png = texture.png;
            if (png == null && !string.IsNullOrEmpty(texture.relativePath))
            {
                externalPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(modelPath) ?? "", texture.relativePath));
                if (File.Exists(externalPath)) png = File.ReadAllBytes(externalPath);
            }
            if (png == null) return null;
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            return image.LoadImage(png) ? image : null;
        }
    }
}
