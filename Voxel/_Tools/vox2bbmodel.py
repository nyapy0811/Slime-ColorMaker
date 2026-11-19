"""MagicaVoxel .vox → Blockbench .bbmodel 변환 (아트 세션 도구, Docs/v2 블록벤치 모델링 규칙 7절).

쓰는 법:  python vox2bbmodel.py <입력.vox> <출력.bbmodel>
          출력 파일이 이미 있으면 Voxel/_Backup에 백업하고 칠을 새 모델로 옮긴다(bbmerge.transfer, 새로 생긴 면은 기본 색).

- 오브젝트 하나 = 부품 그룹 하나(이름 그대로, 없으면 'Object n'), 그 안에 부위 태그 그룹.
- 팔레트 칸 번호 → 태그: 33~40 color, 41~48 glass, 49~56 grayglass, 나머지 gray.
- 같은 태그의 칸을 큰 큐브로 합치고, 보이는 면마다 그 면에 닿은 칸의 색을 칠한 텍스처 영역을 준다(1픽셀 = 1칸).
  gray·grayglass는 팔레트 색 그대로, color·glass는 가장 큰 채널을 회색 밝기로 칠한다. 다른 칸이 완전히 덮는 면은 끈다.
- 축: MagicaVoxel (X, Y, Z) → Unity (X, Z, Y) → Blockbench (X, Y, −Z). MagicaVoxel +Y(앞)가 Blockbench 북쪽(Unity 앞).
- 기준: 모든 오브젝트를 합친 상자의 바닥면 가운데를 Blockbench 원점에 둔다(정수 픽셀이 되게 내림).
  부품 그룹의 기준점은 그 오브젝트 상자의 바닥면 가운데(내림). 가져오기 기준점은 기본값(바닥면 가운데)을 쓴다.
예전 Unity 변환 도구(VoxToBBModel.cs, 2026-11-15)와 같은 계산이다(기준점만 정수로 내림).
"""
import base64
import json
import pathlib
import struct
import sys
import uuid
import zlib

TAGS = ["gray", "color", "glass", "grayglass"]


def tag_of(c):
    if 33 <= c <= 40:
        return 1
    if 41 <= c <= 48:
        return 2
    if 49 <= c <= 56:
        return 3
    return 0


def is_glass(tag):
    return tag in (2, 3)


# ---------- .vox 읽기 ----------

def read_vox(data):
    assert data[:4] == b"VOX ", ".vox 파일이 아님"
    pos = 8
    assert data[pos:pos + 4] == b"MAIN"
    n, m = struct.unpack_from("<ii", data, pos + 4)
    pos += 12 + n
    end = pos + m
    models, palette = [], [(0, 0, 0, 0)] * 256
    transforms, groups, shapes = {}, {}, {}

    def rstr(p):
        k = struct.unpack_from("<i", data, p)[0]
        return data[p + 4:p + 4 + k].decode("utf-8", "replace"), p + 4 + k

    def rdict(p):
        k = struct.unpack_from("<i", data, p)[0]
        p += 4
        d = {}
        for _ in range(k):
            a, p = rstr(p)
            b, p = rstr(p)
            d[a] = b
        return d, p

    rotation = False
    while pos < end:
        cid = data[pos:pos + 4].decode("ascii")
        n, m = struct.unpack_from("<ii", data, pos + 4)
        c = pos + 12
        if cid == "SIZE":
            models.append({"size": struct.unpack_from("<iii", data, c), "voxels": []})
        elif cid == "XYZI":
            k = struct.unpack_from("<i", data, c)[0]
            models[-1]["voxels"] = [tuple(data[c + 4 + 4 * i:c + 8 + 4 * i]) for i in range(k)]
        elif cid == "RGBA":
            palette = [(0, 0, 0, 0)] + [tuple(data[c + 4 * i:c + 4 * i + 4]) for i in range(255)]
        elif cid == "nTRN":
            nid = struct.unpack_from("<i", data, c)[0]
            attr, p = rdict(c + 4)
            child, _, _, frames = struct.unpack_from("<iiii", data, p)
            frame, _ = rdict(p + 16) if frames > 0 else ({}, 0)
            t = tuple(int(v) for v in frame["_t"].split()) if "_t" in frame else (0, 0, 0)
            rotation |= "_r" in frame
            transforms[nid] = (attr.get("_name"), child, t)
        elif cid == "nGRP":
            nid = struct.unpack_from("<i", data, c)[0]
            _, p = rdict(c + 4)
            k = struct.unpack_from("<i", data, p)[0]
            groups[nid] = list(struct.unpack_from(f"<{k}i", data, p + 4))
        elif cid == "nSHP":
            nid = struct.unpack_from("<i", data, c)[0]
            _, p = rdict(c + 4)
            shapes[nid] = struct.unpack_from("<i", data, p + 4)[0]
        pos = c + n + m

    objects = []

    def visit(nid, off):
        name, child, t = transforms[nid]
        off = tuple(a + b for a, b in zip(off, t))
        if child in groups:
            for nxt in groups[child]:
                visit(nxt, off)
        elif child in shapes:
            model = models[shapes[child]]
            size = model["size"]
            mn = tuple(off[i] - size[i] // 2 for i in range(3))   # _t = 모델 가운데 칸(크기/2 내림)
            objects.append({"name": name, "model": model, "min": mn, "max": tuple(mn[i] + size[i] for i in range(3))})

    if transforms:
        visit(0, (0, 0, 0))
    else:  # 씬 그래프가 없는 옛 파일: 모델 하나를 원점에
        for mdl in models:
            objects.append({"name": None, "model": mdl, "min": (0, 0, 0), "max": tuple(mdl["size"])})
    return objects, palette, rotation


# ---------- 변환 ----------

def boxes(size, include):
    """조건에 맞는 칸을 큰 상자로 합친다(x → y → z 순서로 늘림). 상자는 [min, max) 칸 좌표."""
    sx, sy, sz = size
    used = set()

    def free(x, y, z):
        return include(x, y, z) and (x, y, z) not in used

    out = []
    for z in range(sz):
        for y in range(sy):
            for x in range(sx):
                if not free(x, y, z):
                    continue
                x1 = x + 1
                while x1 < sx and free(x1, y, z):
                    x1 += 1
                y1 = y + 1
                while y1 < sy and all(free(i, y1, z) for i in range(x, x1)):
                    y1 += 1
                z1 = z + 1
                while z1 < sz and all(free(i, j, z1) for i in range(x, x1) for j in range(y, y1)):
                    z1 += 1
                for k in range(z, z1):
                    for j in range(y, y1):
                        for i in range(x, x1):
                            used.add((i, j, k))
                out.append(((x, y, z), (x1, y1, z1)))
    return out


def face_frames(f, t):
    """면마다 꼭짓점 0(텍스처 왼쪽 위), 텍스처 오른쪽(du), 아래(dv), 바깥 법선(Blockbench 좌표)."""
    yield "east", (t[0], t[1], t[2]), (0, 0, f[2] - t[2]), (0, f[1] - t[1], 0), (1, 0, 0)
    yield "west", (f[0], t[1], f[2]), (0, 0, t[2] - f[2]), (0, f[1] - t[1], 0), (-1, 0, 0)
    yield "up", (f[0], t[1], f[2]), (t[0] - f[0], 0, 0), (0, 0, t[2] - f[2]), (0, 1, 0)
    yield "down", (f[0], f[1], t[2]), (t[0] - f[0], 0, 0), (0, 0, f[2] - t[2]), (0, -1, 0)
    yield "south", (f[0], t[1], t[2]), (t[0] - f[0], 0, 0), (0, f[1] - t[1], 0), (0, 0, 1)
    yield "north", (t[0], t[1], f[2]), (f[0] - t[0], 0, 0), (0, f[1] - t[1], 0), (0, 0, -1)


def paint(tag, rgba):
    r, g, b, _ = rgba
    if tag in (0, 3):
        return (r, g, b, 255)
    v = max(r, g, b)
    return (v, v, v, 255)


def bottom_center(mn, mx):
    """MagicaVoxel 상자 [mn, mx)의 바닥면 가운데 → Unity 칸 좌표 (x, 위, 깊이), 정수로 내림."""
    return ((mn[0] + mx[0]) // 2, mn[2], (mn[1] + mx[1]) // 2)


def png_bytes(width, height, rows):
    raw = b"".join(b"\x00" + bytes(v for px in row for v in px) for row in rows)

    def chunk(k, b):
        return struct.pack(">I", len(b)) + k + b + struct.pack(">I", zlib.crc32(k + b) & 0xFFFFFFFF)

    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def next_pow2(v):
    p = 1
    while p < v:
        p *= 2
    return p


def pack(faces):
    """면 영역 [(면, 폭, 높이, 픽셀)]을 선반 방식으로 한 장에 놓고 면의 uv를 정한다.
    폭 = 가장 넓은 면과 64 중 큰 것의 2의 거듭제곱. → (폭, 높이, 그림 줄 목록)"""
    width = next_pow2(max([64] + [f[1] for f in faces]))
    order = sorted(faces, key=lambda f: -f[2])
    x = y = shelf = 0
    placed = []
    for face, fw, fh, *_ in order:
        if x + fw > width:
            x, y, shelf = 0, y + shelf, 0
        placed.append((x, y))
        face["uv"] = [x, y, x + fw, y + fh]
        x += fw
        shelf = max(shelf, fh)
    height = next_pow2(max(1, y + shelf))
    image = [[(0, 0, 0, 0)] * width for _ in range(height)]
    for (face, fw, fh, pixels, *_), (px, py) in zip(order, placed):
        for row in range(fh):
            for col in range(fw):
                image[py + row][px + col] = pixels[row * fw + col]
    return width, height, image


def convert(vox_path, out_path):
    objects, palette, rotation = read_vox(pathlib.Path(vox_path).read_bytes())
    if not objects:
        raise SystemExit("오브젝트가 없음")
    fmin = tuple(min(o["min"][i] for o in objects) for i in range(3))
    fmax = tuple(max(o["max"][i] for o in objects) for i in range(3))
    root = bottom_center(fmin, fmax)

    elements, groups, outliner, faces = [], [], [], []
    stats = {"cubes": 0, "faces": 0}

    def group(name, origin):
        u = str(uuid.uuid4())
        groups.append({"name": name, "origin": list(origin), "color": 0, "uuid": u, "export": True,
                       "mirror_uv": False, "isOpen": True, "locked": False, "visibility": True, "autouv": 0})
        node = {"uuid": u, "isOpen": True, "children": []}
        return node

    for i, obj in enumerate(objects):
        sx, sy, sz = obj["model"]["size"]
        size = (sx, sz, sy)                                     # Unity 축 순서 (x, 위, 깊이)
        grid = {(x, z, y): c for x, y, z, c in obj["model"]["voxels"]}
        umin = (obj["min"][0] - root[0], obj["min"][2] - root[1], obj["min"][1] - root[2])

        def at(x, y, z):
            return grid.get((x, y, z), 0)

        pc = bottom_center(obj["min"], obj["max"])
        porigin = (pc[0] - root[0], pc[1] - root[1], -(pc[2] - root[2]))
        part = group(obj["name"] or f"Object {i + 1}", porigin)
        outliner.append(part)

        for tag in range(4):
            bxs = boxes(size, lambda x, y, z: at(x, y, z) != 0 and tag_of(at(x, y, z)) == tag)
            if not bxs:
                continue
            tnode = group(TAGS[tag], porigin)
            part["children"].append(tnode)
            for mn, mx in bxs:
                low = tuple(umin[k] + mn[k] for k in range(3))
                high = tuple(umin[k] + mx[k] for k in range(3))
                f = (low[0], low[1], -high[2])
                t = (high[0], high[1], -low[2])
                cube = {"name": TAGS[tag], "box_uv": False, "rescale": False, "locked": False,
                        "render_order": "default", "allow_mirror_modeling": True,
                        "from": list(f), "to": list(t), "autouv": 0, "color": tag,
                        "origin": list(porigin), "type": "cube", "uuid": str(uuid.uuid4()), "faces": {}}
                for name, c0, du, dv, nrm in face_frames(f, t):
                    w = round(abs(sum(du))) if sum(1 for v in du if v) else 0
                    h = round(abs(sum(dv))) if sum(1 for v in dv if v) else 0
                    su = tuple(v / max(w, 1) for v in du)
                    sv = tuple(v / max(h, 1) for v in dv)
                    pixels, visible = [], False
                    for py in range(h):
                        for px in range(w):
                            pt = tuple(c0[k] + su[k] * (px + 0.5) + sv[k] * (py + 0.5) for k in range(3))

                            def local(p):
                                return (int((p[0] - umin[0]) // 1), int((p[1] - umin[1]) // 1), int((-p[2] - umin[2]) // 1))
                            inside = local(tuple(pt[k] - nrm[k] * 0.5 for k in range(3)))
                            outside = local(tuple(pt[k] + nrm[k] * 0.5 for k in range(3)))
                            col = at(*inside)
                            nb = at(*outside)
                            covered = nb != 0 and (is_glass(tag) or not is_glass(tag_of(nb)))
                            visible |= not covered
                            pixels.append(paint(tag, palette[col]))
                    face = {"uv": [0, 0, w, h]}
                    if visible:
                        face["texture"] = 0
                        faces.append((face, w, h, pixels))
                        stats["faces"] += 1
                    else:
                        face["texture"] = None
                    cube["faces"][name] = face
                elements.append(cube)
                tnode["children"].append(cube["uuid"])
                stats["cubes"] += 1

    width, height, image = pack(faces)
    png = png_bytes(width, height, image)

    name = pathlib.Path(out_path).stem
    model = {
        "meta": {"format_version": "5.0", "model_format": "free", "box_uv": False},
        "name": name, "model_identifier": "", "visible_box": [1, 1, 0],
        "variable_placeholders": "", "variable_placeholder_buttons": [], "timeline_setups": [],
        "unhandled_root_fields": {}, "resolution": {"width": width, "height": height},
        "elements": elements, "groups": groups, "outliner": outliner,
        "textures": [{
            "path": "", "name": name + ".png", "folder": "", "namespace": "", "id": "0", "group": "",
            "width": width, "height": height, "uv_width": width, "uv_height": height,
            "particle": False, "use_as_default": False, "layers_enabled": False, "sync_to_project": "",
            "render_mode": "default", "render_sides": "auto", "pbr_channel": "color", "frame_time": 1,
            "frame_order_type": "loop", "frame_order": "", "frame_interpolate": False, "visible": True,
            "internal": True, "saved": False, "uuid": str(uuid.uuid4()),
            "source": "data:image/png;base64," + base64.b64encode(png).decode("ascii"),
        }],
    }
    out = pathlib.Path(out_path)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(model, ensure_ascii=False, indent=2), encoding="utf-8")
    return {"objects": len(objects), **stats, "texture": (width, height), "rotation": rotation}


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    out = pathlib.Path(sys.argv[2])
    if out.exists():                       # 칠한 모델이 이미 있으면 백업하고 칠을 옮긴다(bbmerge.py)
        import bbmerge
        old = bbmerge.backup(out)
        print(convert(sys.argv[1], out))
        print(bbmerge.transfer(out, old, out))
    else:
        print(convert(sys.argv[1], out))
