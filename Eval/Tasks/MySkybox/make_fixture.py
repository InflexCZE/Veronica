import random
import struct
import sys
import zlib
from pathlib import Path

SIZE = 256
TINTS = {
    "px": (40, 8, 8),
    "nx": (8, 32, 32),
    "py": (8, 36, 8),
    "ny": (32, 8, 32),
    "pz": (8, 8, 44),
    "nz": (34, 30, 6),
}


def chunk(kind, data):
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)


def png(pixels):
    rows = b"".join(b"\x00" + bytes(row) for row in pixels)
    header = struct.pack(">IIBBBBB", SIZE, SIZE, 8, 2, 0, 0, 0)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(rows, 9)) + chunk(b"IEND", b"")


def face(suffix, seed):
    rng = random.Random(seed)
    tint = TINTS[suffix]
    pixels = [list(tint) * SIZE for _ in range(SIZE)]
    for _ in range(400):
        x, y = rng.randrange(SIZE), rng.randrange(SIZE)
        level = rng.randrange(120, 256)
        pixels[y][3 * x:3 * x + 3] = [level, level, level]
    return png(pixels)


def main():
    folder = Path(sys.argv[1]) / "Assets" / "MySkybox"
    folder.mkdir(parents=True, exist_ok=True)
    for seed, suffix in enumerate(TINTS):
        (folder / f"MySky_{suffix}.png").write_bytes(face(suffix, seed))


if __name__ == "__main__":
    main()
