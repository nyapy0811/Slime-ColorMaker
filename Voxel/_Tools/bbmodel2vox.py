""".bbmodel → 단색 .vox (모양 원본 만들기, 아트 세션 도구, Docs/v2 블록벤치 모델링 규칙 7절).

쓰는 법:  python bbmodel2vox.py <입력.bbmodel> <출력.vox>

- 축이 맞는 큐브만 칸으로 채운다(회전한 큐브·메시는 경고하고 뺀다). Blockbench (X, Y, Z) → MagicaVoxel (X, −Z, Y).
  좌표가 반 칸 어긋난 파일(홀수 폭을 가운데에 둔 모델)은 파일 전체를 반 칸 옮겨 정수로 맞춘다.
- 부품 그룹(태그가 아닌 그룹, 가장 가까운 것) 하나 = MagicaVoxel 오브젝트 하나(이름 그대로).
- 부위 태그마다 대표 색 하나(단색): gray 16, color 33, glass 41, grayglass 49. light는 칸이 없어 gray로 두고 알린다.
- 팔레트는 colormaze(Docs/아트/ColorMaze 팔레트.png와 같은 값)를 넣는다.
"""
import json
import math
import pathlib
import struct
import sys

TAGS = {"gray": 16, "color": 33, "glass": 41, "grayglass": 49, "light": 16}


def palette():
    out = []
    for k in range(256):
        i = k + 1
        if i <= 32:
            v = round(255 * (1 - (i - 1) / 31))
            out.append((v, v, v, 255))
        elif i <= 56:
            v = round(255 * (1 - ((i - 33) % 8) / 7))
            out.append((v, 0, 0, 255) if i <= 40 else (0, v, v, 255) if i <= 48 else (v, v, v, 255))
        elif i == 256:
            out.append((0, 0, 0, 0))
        else:
            out.append((0, 0, 0, 255))
    return out


def s(text):
    b = text.encode("utf-8")
    return struct.pack("<i", len(b)) + b


def d(items):
    return struct.pack("<i", len(items)) + b"".join(s(k) + s(v) for k, v in items.items())


def chunk(cid, content, children=b""):
    return cid.encode("ascii") + struct.pack("<ii", len(content), len(children)) + content + children


def write_vox(path, objects):
    """objects: [(name, size(x, y, z), translation(x, y, z), [(x, y, z, color), ...])] — MagicaVoxel 좌표."""
    body = b""
    for _, size, _, voxels in objects:
        body += chunk("SIZE", struct.pack("<iii", *size))
        body += chunk("XYZI", struct.pack("<i", len(voxels)) + b"".join(bytes(v) for v in voxels))
    kids = [2 + 2 * k for k in range(len(objects))]
    body += chunk("nTRN", struct.pack("<i", 0) + d({}) + struct.pack("<iiii", 1, -1, -1, 1) + d({}))
    body += chunk("nGRP", struct.pack("<i", 1) + d({}) + struct.pack("<i", len(kids)) + b"".join(struct.pack("<i", c) for c in kids))
    for k, (name, _, t, _) in enumerate(objects):
        node = 2 + 2 * k
        body += chunk("nTRN", struct.pack("<i", node) + d({"_name": name}) + struct.pack("<iiii", node + 1, -1, 0, 1)
                      + d({"_t": f"{t[0]} {t[1]} {t[2]}"}))
        body += chunk("nSHP", struct.pack("<i", node + 1) + d({}) + struct.pack("<ii", 1, k) + d({}))
    for layer in range(8):
        body += chunk("LAYR", struct.pack("<i", layer) + d({}) + struct.pack("<i", -1))
    body += chunk("RGBA", b"".join(bytes(c) for c in palette()))
    out = pathlib.Path(path)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(b"VOX " + struct.pack("<i", 200) + chunk("MAIN", b"", body))


def cells_of(path):
    """.bbmodel → {부품 이름: {(x, y, z): 색 번호}} (MagicaVoxel 좌표), 알림 목록."""
    m = json.loads(pathlib.Path(path).read_text(encoding="utf-8"))
    groups = {g["uuid"]: g for g in m.get("groups", [])}
    owner = {}   # 큐브 uuid → (부품 이름, 태그)
    notes = []

    def walk(node, part, tag):
        if isinstance(node, str):
            owner[node] = (part, tag)
            return
        g = groups.get(node["uuid"], {"name": "Object"})
        name = g["name"]
        if name.lower() in TAGS:
            tag = name.lower()
        else:
            part = name
        for child in node.get("children", []):
            walk(child, part, tag)

    for node in m.get("outliner", []):
        walk(node, "Object 1", "gray")

    cubes = []
    for e in m.get("elements", []):
        if e.get("type", "cube") != "cube":
            notes.append(f"큐브가 아닌 요소 뺌: {e.get('name')}")
            continue
        if any(abs(v) > 1e-6 for v in e.get("rotation", [0, 0, 0])):
            notes.append(f"회전한 큐브 뺌: {e.get('name')}")
            continue
        if e.get("export") is False or e.get("visibility") is False:
            continue
        cubes.append(e)
    # 반 칸 어긋남: 축마다 소수 부분을 하나로 맞춰 옮긴다.
    shift = []
    for axis in range(3):
        fracs = {round(v - math.floor(v), 3) % 1 for e in cubes for v in (e["from"][axis], e["to"][axis])}
        if len(fracs) > 1:
            raise SystemExit(f"{path}: 축 {axis} 좌표의 소수 부분이 여러 가지 {fracs}")
        f = next(iter(fracs), 0)
        shift.append(-f if f else 0)
    if any(shift):
        notes.append(f"반 칸 맞춤 옮김 (Blockbench 축 X, Y, Z) = {shift}")

    parts = {}
    for e in cubes:
        part, tag = owner.get(e["uuid"], ("Object 1", "gray"))
        if tag == "light":
            notes.append("light 부위는 칸이 없어 gray(16)로 둠")
        color = TAGS[tag]
        lo = [round(e["from"][a] + shift[a]) for a in range(3)]
        hi = [round(e["to"][a] + shift[a]) for a in range(3)]
        cells = parts.setdefault(part, {})
        for x in range(min(lo[0], hi[0]), max(lo[0], hi[0])):
            for y in range(min(lo[1], hi[1]), max(lo[1], hi[1])):
                for z in range(min(lo[2], hi[2]), max(lo[2], hi[2])):
                    cells[(x, -z - 1, y)] = color      # 칸 [z, z+1) → MagicaVoxel y 칸 −z−1
    return parts, sorted(set(notes))


def convert(src, dst):
    parts, notes = cells_of(src)
    objects = []
    for name, cells in parts.items():
        mn = [min(c[a] for c in cells) for a in range(3)]
        mx = [max(c[a] for c in cells) + 1 for a in range(3)]
        size = [mx[a] - mn[a] for a in range(3)]
        if max(size) > 256:
            raise SystemExit(f"{src}: 부품 {name} 크기 {size}가 256칸을 넘음")
        voxels = [(x - mn[0], y - mn[1], z - mn[2], c) for (x, y, z), c in sorted(cells.items())]
        t = [mn[a] + size[a] // 2 for a in range(3)]            # _t = 모델 가운데 칸(크기/2 내림)
        objects.append((name, size, t, voxels))
    write_vox(dst, objects)
    return {"parts": {n: len(c) for n, c in parts.items()}, "notes": notes}


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    print(convert(sys.argv[1], sys.argv[2]))
