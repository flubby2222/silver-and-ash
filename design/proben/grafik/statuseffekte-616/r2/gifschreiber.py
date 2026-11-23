"""Minimaler GIF-Schreiber ohne Fremdpakete (nur NumPy und die Standardbibliothek): Bilder (Zeilen von oben, RGB 0..255)
-> animiertes GIF89a mit einer gemeinsamen Palette aus den haeufigsten Farben."""

import struct

import numpy as np


def _lzw(idx, min_bits=8):
    clear, eoi = 1 << min_bits, (1 << min_bits) + 1
    out = bytearray()
    acc, nacc = 0, 0
    size = min_bits + 1
    table = {}
    nxt = eoi + 1

    def emit(code):
        nonlocal acc, nacc
        acc |= code << nacc
        nacc += size
        while nacc >= 8:
            out.append(acc & 255)
            acc >>= 8
            nacc -= 8

    emit(clear)
    prefix = idx[0]
    for k in idx[1:]:
        key = (prefix << 8) | k
        got = table.get(key)
        if got is not None:
            prefix = got
            continue
        emit(prefix)
        if nxt < 4096:
            table[key] = nxt
            if nxt == (1 << size) and size < 12:
                size += 1
            nxt += 1
        else:
            emit(clear)
            table = {}
            nxt = eoi + 1
            size = min_bits + 1
        prefix = k
    emit(prefix)
    emit(eoi)
    if nacc:
        out.append(acc & 255)
    return bytes(out)


def _blocks(data):
    res = bytearray()
    for i in range(0, len(data), 255):
        chunk = data[i:i + 255]
        res.append(len(chunk))
        res += chunk
    res.append(0)
    return bytes(res)


def write_gif(path, frames, delay_cs=12):
    arrs = [np.clip(np.asarray(f), 0, 255).astype(np.uint8) for f in frames]
    h, w = arrs[0].shape[:2]
    allpx = np.concatenate([a.reshape(-1, 3) for a in arrs])
    key = (allpx[:, 0].astype(np.int32) << 16) | (allpx[:, 1].astype(np.int32) << 8) | allpx[:, 2]
    uniq, counts = np.unique(key, return_counts=True)
    top = uniq[np.argsort(-counts)[:256]]
    pal = np.stack([(top >> 16) & 255, (top >> 8) & 255, top & 255], axis=1).astype(np.int32)
    lut = {int(k): i for i, k in enumerate(top)}
    data = bytearray(b"GIF89a")
    data += struct.pack("<HHBBB", w, h, 0xF7, 0, 0)
    full = np.zeros((256, 3), np.uint8)
    full[:len(pal)] = pal
    data += full.tobytes()
    data += b"\x21\xFF\x0BNETSCAPE2.0\x03\x01\x00\x00\x00"
    for a in arrs:
        flat = a.reshape(-1, 3).astype(np.int32)
        k = (flat[:, 0] << 16) | (flat[:, 1] << 8) | flat[:, 2]
        idx = np.empty(len(k), np.int64)
        uk, inv = np.unique(k, return_inverse=True)
        mapped = np.empty(len(uk), np.int64)
        for j, kk in enumerate(uk):
            if int(kk) in lut:
                mapped[j] = lut[int(kk)]
            else:
                c = np.array([(kk >> 16) & 255, (kk >> 8) & 255, kk & 255])
                mapped[j] = int(np.argmin(((pal - c) ** 2).sum(axis=1)))
        idx = mapped[inv]
        data += b"\x21\xF9\x04\x00" + struct.pack("<H", delay_cs) + b"\x00\x00"
        data += b"\x2C" + struct.pack("<HHHHB", 0, 0, w, h, 0) + b"\x08"
        data += _blocks(_lzw(idx.tolist()))
    data += b"\x3B"
    with open(path, "wb") as f:
        f.write(data)
    print("GIF:", path, len(data) // 1024, "KB")
