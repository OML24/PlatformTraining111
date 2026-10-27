#!/usr/bin/env python3
"""مطابقة مسارات الـAPI التي تناديها الواجهة مع المسارات المعرّفة في المتحكمات."""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CONTROLLERS = ROOT / "src/ZU.Training.WebApi/Controllers"
WWW = ROOT / "src/ZU.Training.WebPortal/wwwroot"


def controller_routes() -> list[tuple[str, str]]:
    routes = []
    for f in CONTROLLERS.glob("*.cs"):
        text = f.read_text(encoding="utf-8")
        # كل متحكم يبدأ بسمة Route؛ الملف قد يحوي أكثر من متحكم.
        starts = [m.start() for m in re.finditer(r'\[Route\("', text)]
        for index, start in enumerate(starts):
            end = starts[index + 1] if index + 1 < len(starts) else len(text)
            segment = text[start:end]
            base = re.search(r'\[Route\("([^"]+)"\)\]', segment).group(1)
            for line in segment.splitlines():
                t = line.strip()
                hm = re.match(r'\[Http(Get|Post|Put|Delete|Patch)(?:\("([^"]*)"\))?\]', t)
                if not hm:
                    continue
                pattern = "/" + base.strip("/")
                if hm.group(2):
                    pattern += "/" + hm.group(2).strip("/")
                routes.append((hm.group(1).upper(), re.sub(r"\{[^}]*\}", "{}", pattern).replace("//", "/")))
    return routes


def normalize(url: str) -> tuple[str, str]:
    url = url.split("?")[0].rstrip("/")
    url = re.sub(r"\$\{.*?\}", "{}", url)
    url = re.sub(r"\{[^}]*\}", "{}", url)
    return url


def frontend_calls() -> list[tuple[str, str, str]]:
    calls = []
    for f in sorted(WWW.glob("js/*.js")):
        text = f.read_text(encoding="utf-8")
        pairs = []
        for m in re.finditer(r"ZU\.(api|apiForm)\(\s*`([^`]*)`", text):
            pairs.append((m.group(1), m.group(2)))
        for m in re.finditer(r"ZU\.(api|apiForm)\(\s*'([^']*)'", text):
            pairs.append((m.group(1), m.group(2)))
        for m in re.finditer(r'ZU\.(api|apiForm)\(\s*"([^"]*)"', text):
            pairs.append((m.group(1), m.group(2)))
        for kind, url in pairs:
            if not url.startswith("/api"):
                continue
            calls.append((f.name, m.group(1), normalize(url)))
    return calls


def main() -> int:
    routes = controller_routes()
    by_pattern = {}
    for method, pattern in routes:
        by_pattern.setdefault(normalize(pattern), set()).add(method)

    missing = []
    for file, kind, url in frontend_calls():
        methods = by_pattern.get(url)
        if methods is None:
            # مطابقة فضفاضة: مسارات المعرّف قد تُسمّى {id:guid}
            missing.append(f"{file}: {kind} {url} — لا يقابله مسار في المتحكمات")
            continue
        needed = "POST" if kind == "apiForm" else "GET"
        if kind == "apiForm":
            if not ({"POST", "PUT"} & methods):
                missing.append(f"{file}: apiForm {url} — لا يقبل POST/PUT ({sorted(methods)})")
    if missing:
        print(f"مسارات غير مطابقة ({len(missing)}):")
        for m in missing:
            print(" -", m)
        return 1
    print(f"كل مسارات الواجهة ({len(frontend_calls())} نداء) موجودة في المتحكمات.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
