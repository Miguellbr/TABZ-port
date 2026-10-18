#!/usr/bin/env python3
"""Read-only byte scan for WorldCreator references.

Usage: scan_strings.py <file-or-dir> [<file-or-dir> ...]

Searches every file for 'WorldCreator' (ASCII and UTF-16LE, any case) and for the GUID
8ad529edc4cb581d366ee49da4565275, and prints the printable text around each hit.
Files with identical content (same SHA-1) are scanned once.
Nothing is modified.
"""
import hashlib
import os
import re
import sys

PATTERNS = [
    ('ascii', re.compile(rb'worldcreator', re.I), 90),
    ('utf16le', re.compile(b'w\x00o\x00r\x00l\x00d\x00c\x00r\x00e\x00a\x00t\x00o\x00r\x00', re.I), 180),
    ('guid', re.compile(rb'8ad529edc4cb581d366ee49da4565275', re.I), 90),
]
MAX_BYTES = 200 * 1024 * 1024
MAX_CONTEXTS = 10


def printable(chunk: bytes) -> str:
    chunk = chunk.replace(b'\x00', b'')
    return re.sub(rb'[^\x20-\x7e]', b'.', chunk).decode('ascii')


def walk(paths):
    for p in paths:
        if os.path.isfile(p):
            yield p
        elif os.path.isdir(p):
            for root, dirs, files in os.walk(p):
                dirs[:] = sorted(d for d in dirs if d != '.git')
                for f in sorted(files):
                    yield os.path.join(root, f)
        else:
            print('MISSING PATH:', p)


def main(argv):
    seen = {}
    scanned = 0
    with_hits = 0
    for path in walk(argv):
        size = os.path.getsize(path)
        if size > MAX_BYTES:
            print('SKIPPED (too big):', path, size)
            continue
        with open(path, 'rb') as fh:
            data = fh.read()
        digest = hashlib.sha1(data).hexdigest()
        if digest in seen:
            if seen[digest][1]:
                print('--- %s\n    same content as %s (already reported)' % (path, seen[digest][0]))
            continue
        scanned += 1
        report = []
        for name, rx, pad in PATTERNS:
            hits = list(rx.finditer(data))
            if not hits:
                continue
            report.append('    [%s] %d hit(s)' % (name, len(hits)))
            contexts = []
            for m in hits:
                ctx = printable(data[max(0, m.start() - pad):m.end() + pad])
                if ctx not in contexts:
                    contexts.append(ctx)
                if len(contexts) >= MAX_CONTEXTS:
                    break
            for ctx in contexts:
                report.append('      ...' + ctx + '...')
        seen[digest] = (path, bool(report))
        if report:
            with_hits += 1
            print('--- %s (%d bytes)' % (path, size))
            print('\n'.join(report))
    print()
    print('FILES SCANNED (unique content): %d | FILES WITH HITS: %d' % (scanned, with_hits))


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(2)
    main(sys.argv[1:])
