"""Blockbench .bbmodel 큐브 합치기 + 칠 옮기기 (아트 세션 도구, Docs/v2 블록벤치 모델링 규칙 7절).

쓰는 법:  python bbmerge.py <모델.bbmodel> [...]
          같은 그룹 바로 아래의 큐브를 큰 큐브로 합친다(칠 유지). 원래 파일은 Voxel/_Backup에 백업한다.
          python bbmerge.py --changed   ('모델 확인' 요청) Assets/Models에서 처리 기록과 다른(새·수정) .bbmodel만 합친다.
          python bbmerge.py --record    지금 Assets/Models의 .bbmodel을 모두 처리한 것으로 기록만 한다.
          vox2bbmodel.py는 이미 있는 .bbmodel을 덮어쓸 때 transfer()로 옛 칠을 새 모델에 옮긴다.

- 칠은 "칸 하나의 면 하나 = 텍스처 1픽셀"로 읽는다: (칸 좌표, 면 방향) → 색. 면 uv와 면 회전은 파일에 적힌 그대로 읽는다.
- 합치는 큐브: 축이 맞고(회전 0) 좌표가 정수이고 inflate 0이고 숨기지 않은 큐브. 같은 그룹 바로 아래 큐브끼리만 합친다.
  나머지(회전한 큐브 등)는 그대로 두고 그 그림만 새 텍스처로 옮긴다. 메시는 지원하지 않는다(멈춤).
- 칠이 없는 면(새로 드러난 면)은 기본 색: gray 132 회색, 나머지 흰색(단색 .vox 변환과 같은 색).
- 같은 부품 안에서 다른 칸이 완전히 덮는 면은 끈다(vox2bbmodel과 같은 규칙).
- 결과는 면마다 UV(Box UV 끔)다. 텍스처는 한 장, 텍스처 크기 = UV 크기(1픽셀 = 1칸)여야 한다. 레이어는 합친 그림 하나로 바뀐다.
  텍스처가 없는 모델(모양만 만든 것)은 기본 색 텍스처를 새로 만든다(면마다 1픽셀 = 1칸 UV).
"""
import base64
import copy
import datetime
import hashlib
import json
import math
import pathlib
import shutil
import struct
import sys
import uuid
import zlib

from vox2bbmodel import boxes, face_frames, pack, png_bytes

TAGS = ("gray", "color", "glass", "grayglass", "light")
GLASS = ("glass", "grayglass")
WHITE = (255, 255, 255, 255)
DEFAULT = {"gray": (132, 132, 132, 255)}                      # 단색 .vox의 gray 칸(16) 색
BACKUP = pathlib.Path(__file__).resolve().parent.parent / "_Backup"
ROOT = BACKUP.parent.parent                                    # 프로젝트 루트
MODELS = ROOT / "Assets" / "Models"
RECORD = BACKUP / "처리 기록.json"                             # 모델 경로 → 마지막으로 처리한 파일 내용의 sha256
TEXTURE_DEFAULTS = {                                           # vox2bbmodel이 쓰는 텍스처 항목과 같음
    "path": "", "name": "", "folder": "", "namespace": "", "id": "0", "group": "", "particle": False,
    "use_as_default": False, "layers_enabled": False, "sync_to_project": "", "render_mode": "default",
    "render_sides": "auto", "pbr_channel": "color", "frame_time": 1, "frame_order_type": "loop", "frame_order": "",
    "frame_interpolate": False, "visible": True, "internal": True, "saved": False}


# ---------- 파일 ----------

def load(path):
    return json.loads(pathlib.Path(path).read_text(encoding="utf-8"))


def backup(path):
    path = pathlib.Path(path)
    BACKUP.mkdir(parents=True, exist_ok=True)
    dst = BACKUP / f"{path.stem}_{datetime.datetime.now():%Y%m%d-%H%M%S}{path.suffix}"
    shutil.copy2(path, dst)
    return dst


def read_png(data):
    """8비트 RGB/RGBA PNG → (폭, 높이, 줄마다 RGBA 튜플 목록)."""
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise SystemExit("텍스처가 PNG가 아님")
    pos, idat = 8, b""
    while pos < len(data):
        n = struct.unpack(">I", data[pos:pos + 4])[0]
        kind, body = data[pos + 4:pos + 8], data[pos + 8:pos + 8 + n]
        if kind == b"IHDR":
            w, h, depth, ctype, _, _, interlace = struct.unpack(">IIBBBBB", body)
        elif kind == b"IDAT":
            idat += body
        pos += 12 + n
    if depth != 8 or interlace or ctype not in (2, 6):
        raise SystemExit(f"읽을 수 없는 PNG 형식(비트 {depth}, 색 형식 {ctype}, 인터레이스 {interlace})")
    bpp = 4 if ctype == 6 else 3
    raw, stride = zlib.decompress(idat), w * bpp
    rows, prev, p = [], bytearray(stride), 0
    for _ in range(h):
        kind, line = raw[p], bytearray(raw[p + 1:p + 1 + stride])
        p += 1 + stride
        for i in range(stride):
            a = line[i - bpp] if i >= bpp else 0
            b = prev[i]
            c = prev[i - bpp] if i >= bpp else 0
            if kind == 1:
                line[i] = (line[i] + a) & 255
            elif kind == 2:
                line[i] = (line[i] + b) & 255
            elif kind == 3:
                line[i] = (line[i] + (a + b) // 2) & 255
            elif kind == 4:
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append([tuple(line[i:i + bpp]) + ((255,) if bpp == 3 else ()) for i in range(0, stride, bpp)])
        prev = line
    return w, h, rows


def load_image(model, path):
    """모델의 텍스처 한 장 → 줄 목록(없으면 None). 1픽셀 = 1 UV 칸이 아니면 멈춘다."""
    texs = model.get("textures", [])
    if not texs:
        return None
    if len(texs) > 1:
        raise SystemExit(f"{path}: 텍스처가 {len(texs)}장 — 한 장만 지원")
    t = texs[0]
    src = t.get("source") or ""
    if src.startswith("data:image/png;base64,"):
        data = base64.b64decode(src.split(",", 1)[1])
    elif t.get("relative_path") and (pathlib.Path(path).parent / t["relative_path"]).exists():
        data = (pathlib.Path(path).parent / t["relative_path"]).read_bytes()
    elif t.get("path") and pathlib.Path(t["path"]).exists():
        data = pathlib.Path(t["path"]).read_bytes()
    else:
        raise SystemExit(f"{path}: 텍스처 그림을 찾을 수 없음")
    w, h, rows = read_png(data)
    res = model.get("resolution", {})
    uw, uh = t.get("uv_width") or res.get("width") or w, t.get("uv_height") or res.get("height") or h
    if (w, h) != (uw, uh):
        raise SystemExit(f"{path}: 텍스처 {w}×{h}가 UV 크기 {uw}×{uh}와 달라 1픽셀 = 1칸이 아님")
    return rows


# ---------- 읽기 ----------

def integral(v):
    return abs(v - round(v)) < 1e-6


def box_of(e):
    f = [round(min(e["from"][a], e["to"][a])) for a in range(3)]
    t = [round(max(e["from"][a], e["to"][a])) for a in range(3)]
    return f, t


def mergeable(e):
    return (not any(abs(r) > 1e-6 for r in e.get("rotation", [0, 0, 0]))
            and all(integral(v) for v in e["from"] + e["to"])
            and abs(e.get("inflate", 0)) < 1e-6
            and all(abs(e["to"][a] - e["from"][a]) > 0.5 for a in range(3))
            and e.get("export", True) is not False and e.get("visibility", True) is not False)


def face_cells(f, t):
    """큐브 [f, t)의 면마다 (이름, 폭, 높이, [(안쪽 칸, 바깥 칸)] 픽셀 순서) — vox2bbmodel과 같은 면 틀."""
    for name, c0, du, dv, nrm in face_frames(f, t):
        w = round(sum(abs(v) for v in du))
        h = round(sum(abs(v) for v in dv))
        pix = []
        for py in range(h):
            for px in range(w):
                pt = [c0[k] + du[k] * (px + 0.5) / w + dv[k] * (py + 0.5) / h for k in range(3)]
                pix.append((tuple(math.floor(pt[k] - nrm[k] * 0.5) for k in range(3)),
                            tuple(math.floor(pt[k] + nrm[k] * 0.5) for k in range(3))))
        yield name, w, h, pix


def tex_index(model, v):
    if isinstance(v, str):
        return next((i for i, t in enumerate(model.get("textures", [])) if t.get("uuid") == v), -1)
    return v


def paint_map(model, rows):
    """{(칸, 면 이름): 색}과 1픽셀 = 1칸이 아닌 면 수. 면 회전은 Blockbench(가져오기 코드)와 같이 돌린다."""
    out, odd = {}, 0
    if rows is None:
        return out, odd
    th, tw = len(rows), len(rows[0])
    for e in model.get("elements", []):
        if e.get("type", "cube") != "cube" or not mergeable(e):
            continue
        f, t = box_of(e)
        for name, w, h, pix in face_cells(f, t):
            face = e.get("faces", {}).get(name)
            if not face or tex_index(model, face.get("texture")) != 0 or len(face.get("uv", [])) != 4:
                continue
            u1, v1, u2, v2 = face["uv"]
            c = [(u1, v1), (u2, v1), (u1, v2), (u2, v2)]          # 왼쪽 위, 오른쪽 위, 왼쪽 아래, 오른쪽 아래
            for _ in range(int(face.get("rotation", 0)) // 90):
                c[0], c[2], c[3], c[1] = c[2], c[3], c[1], c[0]
            if (abs(math.dist(c[0], c[1]) - w) > 1e-6 or abs(math.dist(c[0], c[2]) - h) > 1e-6):
                odd += 1
            for k, (inside, _) in enumerate(pix):
                s, r = (k % w + 0.5) / w, (k // w + 0.5) / h
                u = c[0][0] * (1 - s) * (1 - r) + c[1][0] * s * (1 - r) + c[2][0] * (1 - s) * r + c[3][0] * s * r
                v = c[0][1] * (1 - s) * (1 - r) + c[1][1] * s * (1 - r) + c[2][1] * (1 - s) * r + c[3][1] * s * r
                x, y = min(max(int(math.floor(u)), 0), tw - 1), min(max(int(math.floor(v)), 0), th - 1)
                out.setdefault((inside, name), rows[y][x])
    return out, odd


class Node:
    """그룹 하나(뿌리는 data 없음). items = 원래 순서의 ("group", Node) / ("keep", 요소) / ("merge", None)."""

    def __init__(self, data, outl, tag, part):
        self.data, self.outl, self.tag = data, outl, tag
        self.part = part if part is not None else self      # 가장 가까운 부품(태그가 아닌 그룹 또는 뿌리)
        self.items, self.cells, self.template = [], set(), None

    def walk(self):
        yield self
        for kind, x in self.items:
            if kind == "group":
                yield from x.walk()


def read_tree(model):
    els = {e["uuid"]: e for e in model.get("elements", [])}
    gdata = {g["uuid"]: g for g in model.get("groups", [])}
    root = Node(None, None, "gray", None)
    root.tagged = False                                  # 태그 그룹 안인지(밖의 큐브는 gray로 처리, 알림)

    def walk(children, parent):
        for ch in children:
            if isinstance(ch, str):
                e = els.get(ch)
                if e is None:
                    continue
                if e.get("type", "cube") != "cube":
                    raise SystemExit(f"큐브가 아닌 요소(메시 등) '{e.get('name')}' — 지원하지 않음")
                if mergeable(e):
                    if parent.template is None:
                        parent.template = e
                        parent.items.append(("merge", None))
                    f, t = box_of(e)
                    parent.cells.update((x, y, z) for x in range(f[0], t[0]) for y in range(f[1], t[1])
                                        for z in range(f[2], t[2]))
                else:
                    parent.items.append(("keep", e))
            else:
                if ch.get("uuid") not in gdata:
                    raise SystemExit("옛 형식(그룹 내용이 outliner에 있음) — Blockbench에서 다시 저장해 주세요")
                data = gdata[ch["uuid"]]
                name = (data.get("name") or "").lower()
                g = Node(data, ch, name if name in TAGS else parent.tag, parent.part if name in TAGS else None)
                g.tagged = name in TAGS or parent.tagged
                parent.items.append(("group", g))
                walk(ch.get("children", []), g)

    walk(model.get("outliner", []), root)
    return root


# ---------- 쓰기 ----------

def build(base, root, lookup, out_path):
    """root 트리를 합친 큐브로 다시 짓는다. lookup(칸, 면) → 색 또는 None(기본 색). → 통계"""
    grids, overlap = {}, 0
    for g in root.walk():
        grid = grids.setdefault(id(g.part), {})
        for cell in g.cells:
            overlap += cell in grid
            grid[cell] = g.tag
    stats = {"cubes": 0, "kept": 0, "painted_px": 0, "default_px": 0, "overlap_cells": overlap}
    elements, groups, faces = [], [], []

    def merged(g):
        grid = grids[id(g.part)]
        mn = [min(c[a] for c in g.cells) for a in range(3)]
        size = [max(c[a] for c in g.cells) + 1 - mn[a] for a in range(3)]
        for lo, hi in boxes(size, lambda x, y, z: (x + mn[0], y + mn[1], z + mn[2]) in g.cells):
            cube = copy.deepcopy(g.template)
            cube.pop("uv_offset", None)
            cube.update({"uuid": str(uuid.uuid4()), "from": [lo[a] + mn[a] for a in range(3)],
                         "to": [hi[a] + mn[a] for a in range(3)], "faces": {}, "box_uv": False})   # 면마다 UV
            for name, w, h, pix in face_cells(cube["from"], cube["to"]):
                visible, pixels = False, []
                for inside, outside in pix:
                    nb = grid.get(outside)
                    shown = not (nb is not None and (g.tag in GLASS or nb not in GLASS))
                    col = lookup(inside, name)
                    if shown:                                      # 보이는 픽셀만 센다
                        stats["painted_px" if col else "default_px"] += 1
                    visible |= shown
                    pixels.append(col or DEFAULT.get(g.tag, WHITE))
                face = {"uv": [0, 0, w, h], "texture": 0 if visible else None}
                if visible:
                    faces.append((face, w, h, pixels, False, False))
                cube["faces"][name] = face
            elements.append(cube)
            stats["cubes"] += 1
            yield cube["uuid"]

    def kept(e, rows):
        e = copy.deepcopy(e)
        e.pop("uv_offset", None)
        e["box_uv"] = False                                # 옮긴 면 uv를 그대로 쓰게 면마다 UV로
        for face in e.get("faces", {}).values():
            if face.get("texture") is None or len(face.get("uv", [])) != 4 or rows is None:
                continue
            u1, v1, u2, v2 = face["uv"]
            x0, x1 = math.floor(min(u1, u2)), math.ceil(max(u1, u2))
            y0, y1 = math.floor(min(v1, v2)), math.ceil(max(v1, v2))
            if x1 <= x0 or y1 <= y0:
                continue
            pixels = [rows[y][x] for y in range(y0, y1) for x in range(x0, x1)]
            faces.append((face, x1 - x0, y1 - y0, pixels, u2 < u1, v2 < v1))
        elements.append(e)
        stats["kept"] += 1
        return e["uuid"]

    def emit(g):
        if g.data is not None:
            groups.append(g.data)
        children = []
        for kind, x in g.items:
            if kind == "group":
                children.append(emit(x))
            elif kind == "keep":
                children.append(kept(x, base_rows))
            else:
                children.extend(merged(g))
        if g.data is None:
            return children
        return {**{k: v for k, v in g.outl.items() if k != "children"}, "children": children}

    base_rows = getattr(root, "rows", None)
    outliner = emit(root)
    width, height, image = pack(faces)
    for face, *_, flip_u, flip_v in faces:
        if flip_u:
            face["uv"][0], face["uv"][2] = face["uv"][2], face["uv"][0]
        if flip_v:
            face["uv"][1], face["uv"][3] = face["uv"][3], face["uv"][1]
    png = png_bytes(width, height, image)

    model = copy.deepcopy(base)
    tex = copy.deepcopy((base.get("textures") or [TEXTURE_DEFAULTS])[0])   # 텍스처가 없던 모델은 새로 만든다
    tex.pop("layers", None)
    tex.pop("relative_path", None)
    tex.update({"path": "", "name": tex.get("name") or pathlib.Path(out_path).stem + ".png",
                "width": width, "height": height, "uv_width": width, "uv_height": height,
                "layers_enabled": False, "internal": True, "uuid": tex.get("uuid") or str(uuid.uuid4()),
                "source": "data:image/png;base64," + base64.b64encode(png).decode("ascii")})
    model.setdefault("meta", {})["box_uv"] = False
    model.update({"elements": elements, "groups": groups, "outliner": outliner, "textures": [tex],
                  "resolution": {"width": width, "height": height}})
    pathlib.Path(out_path).write_text(json.dumps(model, ensure_ascii=False, indent=2), encoding="utf-8")
    stats["texture"] = (width, height)
    return stats


# ---------- 합치기 ----------

def merge(path, out_path=None):
    model = load(path)
    rows = load_image(model, path)
    root = read_tree(model)
    root.rows = rows
    paint, odd = paint_map(model, rows)
    before = len(model.get("elements", []))
    untagged = sum(len(g.cells) for g in root.walk() if not g.tagged)
    had_texture = bool(model.get("textures"))
    stats = build(model, root, lambda cell, face: paint.get((cell, face)), out_path or path)
    return {"cubes_before": before, **stats, "faces_not_1to1": odd, "untagged_cells": untagged,
            "new_texture": not had_texture}


def sha(path):
    return hashlib.sha256(pathlib.Path(path).read_bytes()).hexdigest()


def changed(record_only=False):
    """처리 기록과 비교해 새·수정된 .bbmodel만 백업하고 합친다. → [(경로, '새 모델'/'수정', 결과)]"""
    rec = json.loads(RECORD.read_text(encoding="utf-8")) if RECORD.exists() else {}
    done = []
    for p in sorted(MODELS.rglob("*.bbmodel")):
        key = p.relative_to(ROOT).as_posix()
        if rec.get(key) == sha(p):
            continue
        if not record_only:
            kind = "수정" if key in rec else "새 모델"
            saved = backup(p)
            done.append((key, kind, {"backup": saved.name, **merge(p)}))
        rec[key] = sha(p)
    BACKUP.mkdir(parents=True, exist_ok=True)
    RECORD.write_text(json.dumps(rec, ensure_ascii=False, indent=2), encoding="utf-8")
    return done


# ---------- 다시 변환할 때 칠 옮기기 ----------

def path_of(g, parents):
    names, n = [], g
    while n is not None and n.data is not None:
        names.append(n.data.get("name"))
        n = parents.get(id(n))
    return tuple(reversed(names))


def parent_map(root):
    out = {}
    for g in root.walk():
        for kind, x in g.items:
            if kind == "group":
                out[id(x)] = g
    return out


def best_offset(new, old):
    """새 칸을 얼마나 옮기면(새 + o = 옛) 가장 많이 겹치는지. 기준점(바닥 가운데)이 바뀐 만큼을 찾는다."""
    if not new or not old:
        return (0, 0, 0), 0
    cand = []
    for a in range(3):
        nmn, nmx = min(c[a] for c in new), max(c[a] for c in new) + 1
        omn, omx = min(c[a] for c in old), max(c[a] for c in old) + 1
        cand.append(sorted({0, omn - nmn, omx - nmx, (omn + omx - nmn - nmx) // 2, -((nmn + nmx - omn - omx) // 2)}))
    best = max(((sum((c[0] + dx, c[1] + dy, c[2] + dz) in old for c in new), (dx, dy, dz) != (0, 0, 0), (dx, dy, dz))
                for dx in cand[0] for dy in cand[1] for dz in cand[2]), key=lambda r: (r[0], not r[1]))
    return best[2], best[0]


def transfer(new_path, old_path, out_path):
    """new(.vox에서 막 변환)의 모양 + old(칠한 옛 모델)의 칠·그룹 설정·light 부위 → out."""
    new, old = load(new_path), load(old_path)
    old_rows = load_image(old, old_path)
    nroot, oroot = read_tree(new), read_tree(old)
    opaint, odd = paint_map(old, old_rows)

    ncells = {c for g in nroot.walk() for c in g.cells}
    ocells = {c for g in oroot.walk() for c in g.cells}
    o, hit = best_offset(ncells, ocells)
    sh = lambda c: (c[0] + o[0], c[1] + o[1], c[2] + o[2])

    oparents, nparents = parent_map(oroot), parent_map(nroot)
    old_groups = {path_of(g, oparents): g for g in oroot.walk() if g.data is not None}
    old_light = {}                                       # 옛 부품 경로 → (light 칸, light 그룹)
    for g in oroot.walk():
        if g.tag == "light" and g.cells:
            key = path_of(g.part, oparents) if g.part is not oroot else ()
            cells, first = old_light.get(key, (set(), g))
            old_light[key] = (cells | g.cells, first)

    matched, moved = 0, 0
    for g in list(nroot.walk()):
        g.cells = {sh(c) for c in g.cells}
        if g.template is not None:
            g.template = copy.deepcopy(g.template)
            g.template["origin"] = [v + o[a] for a, v in enumerate(g.template.get("origin", [0, 0, 0]))]
        if g.data is None:
            continue
        p = path_of(g, nparents)
        if p in old_groups:                              # 같은 이름 경로의 옛 그룹 설정(기준점·회전 등)을 그대로
            g.data, g.outl = old_groups[p].data, {**old_groups[p].outl}
            matched += 1
        else:
            g.data = {**g.data, "origin": [v + o[a] for a, v in enumerate(g.data.get("origin", [0, 0, 0]))]}
    for part in [g for g in nroot.walk() if g.part is g]:
        key = path_of(part, nparents) if part is not nroot else ()
        if key not in old_light:
            continue
        lcells, lgroup = old_light[key]
        take = set()
        for g in part.walk():
            if g.part is part and g.tag != "light":
                take |= g.cells & lcells
                g.cells -= lcells
        if take:
            light = Node(lgroup.data, {**lgroup.outl}, "light", part)
            light.template, light.cells = lgroup.template, take
            light.items.append(("merge", None))
            part.items.append(("group", light))
            moved += len(take)
    def prune(g):                                        # 칸이 다 빠진 그룹(light로 옮겨 비게 된 태그 그룹)은 없앤다
        g.items = [(k, x) for k, x in g.items if (k != "merge" or g.cells) and (k != "group" or prune(x))]
        return bool(g.items)
    prune(nroot)

    nroot.rows = None
    stats = build(old, nroot, lambda cell, face: opaint.get((cell, face)), out_path)   # 옛 칠이 없는 면은 기본 색
    return {"offset": o, "aligned_cells": f"{hit}/{len(ncells)}", "groups_matched": matched, "light_cells": moved,
            "dropped_old_kept": sum(1 for g in oroot.walk() for k, _ in g.items if k == "keep"),
            "old_faces_not_1to1": odd, **stats}


if __name__ == "__main__":
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    if sys.argv[1] in ("--changed", "--record"):
        result = changed(record_only=sys.argv[1] == "--record")
        for key, kind, r in result:
            print(kind, key, r)
        print(f"처리 {len(result)}개" if sys.argv[1] == "--changed" else "기록만 함")
        raise SystemExit
    for arg in sys.argv[1:]:
        saved = backup(arg)
        print(arg, "→ 백업", saved)
        print(merge(arg))
