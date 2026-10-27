#!/usr/bin/env python3
"""فحص ثابت لتماسك الطبقات: يقارن أعضاء الواجهات بتطبيقاتها، ويطابق حقن التبعيات.

لا يغني عن المترجم، لكنه يكشف الانحراف الأكثر شيوعاً هنا: خدمة أو مستودع لم يُحدَّث
بعد تغيّر واجهته، أو نوع غير مسجّل في حاوية DI.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"

IDENT = r"[A-Za-z_][A-Za-z0-9_]*"


def strip_comments(text: str) -> str:
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    text = re.sub(r"//[^\n]*", "", text)
    text = re.sub(r'"""..*?"""', '""', text, flags=re.S)
    return text


def block_after(text: str, start: int) -> str:
    """يعيد محتوى أول كتلة بين قوسين بعد الموضع start."""
    depth = 0
    for i in range(start, len(text)):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                return text[start : i + 1]
    return text[start:]


def members_of(body: str) -> set:
    """يستخرج أسماء الأعضاء العامة من جسم واجهة أو صنف (يتعامل مع الأسطر المتعددة)."""
    names = set()
    flat = re.sub(r"\s+", " ", body)
    # دوال: آخر معرّف قبل القوس
    for m in re.finditer(rf"({IDENT})\s*(?:<[^<>]*>)?\s*\(", flat):
        name = m.group(1)
        if name in {"if", "for", "foreach", "while", "switch", "return", "new", "catch", "using", "lock", "typeof"}:
            continue
        names.add(name)
    # خصائص: TYPE NAME { get  أو  TYPE NAME =>
    for m in re.finditer(rf"({IDENT})\s*(?:\{{ get|=>)", flat):
        names.add(m.group(1))
    return names


def parse_interfaces(files):
    """اسم الواجهة → مجموعة أسماء أعضائها."""
    out = {}
    for f in files:
        text = strip_comments(f.read_text(encoding="utf-8"))
        for m in re.finditer(rf"\bpublic\s+interface\s+({IDENT})", text):
            body = block_after(text, text.index("{", m.end()))
            out[m.group(1)] = members_of(body)
    return out


def parse_classes(files):
    """اسم الصنف → (الواجهات المعلنة، أعضاؤه العامة)."""
    out = {}
    for f in files:
        text = strip_comments(f.read_text(encoding="utf-8"))
        for m in re.finditer(rf"\bpublic\s+(?:sealed\s+|static\s+|abstract\s+|partial\s+)*class\s+({IDENT})\s*(?:\([^)]*\))?\s*(?::\s*([^\{{\n]+))?", text):
            name = m.group(1)
            bases = [b.strip() for b in (m.group(2) or "").split(",") if b.strip()]
            ifaces = [re.sub(r"<.*", "", b.split()[-1]) for b in bases if b[:1] == "I"]
            body = block_after(text, text.index("{", m.end()))
            out[name] = (ifaces, members_of(body))
    return out


def main() -> int:
    app_files = list((SRC / "ZU.Training.Application").rglob("*.cs"))
    infra_files = list((SRC / "ZU.Training.Infrastructure").rglob("*.cs"))
    api_files = list((SRC / "ZU.Training.WebApi").rglob("*.cs"))

    ifaces = parse_interfaces(app_files + infra_files)
    classes = parse_classes(infra_files + app_files + api_files)

    problems = []

    # 1) كل صنف يعلن تنفيذ واجهة يجب أن يغطي أعضاءها
    for cname, (ifaces_decl, members) in sorted(classes.items()):
        for iface in ifaces_decl:
            if iface not in ifaces:
                continue
            missing = sorted(ifaces[iface] - members)
            # الخصائص المنفّذة قد تُكتب بأسلوب expression-bodied
            if missing:
                problems.append(f"[members] {cname} لا ينفّذ {iface}: {', '.join(missing)}")

    # 2) الواجهات بلا أي تطبيق
    impl_map = {i: [] for i in ifaces}
    for cname, (ifaces_decl, _) in classes.items():
        for iface in ifaces_decl:
            if iface in impl_map:
                impl_map[iface].append(cname)
    for iface, impls in sorted(impl_map.items()):
        if not impls:
            problems.append(f"[impl] لا يوجد صنف ينفّذ {iface}")

    # 3) تسجيل DI لكل واجهة
    program = (SRC / "ZU.Training.WebApi" / "Program.cs").read_text(encoding="utf-8")
    for iface, impls in sorted(impl_map.items()):
        if not impls:
            continue
        if not re.search(rf"<{iface}\b|<{iface},", program):
            problems.append(f"[di] {iface} غير مسجّل في Program.cs (التطبيقات: {', '.join(impls)})")

    # 4) خدمات مستخدمة في المتحكمات دون تسجيل
    for f in api_files:
        text = strip_comments(f.read_text(encoding="utf-8"))
        for m in re.finditer(rf"\b(?:class)\s+({IDENT})\s*\(([^)]*)\)", text):
            params = m.group(2)
            for pm in re.finditer(rf"(?:^|,)\s*({IDENT}(?:<[^>]*>)?)\s+({IDENT})", params):
                typ = pm.group(1)
                if typ.endswith("Options") or typ.startswith("I"):
                    continue
                if typ in classes and not re.search(rf"<{typ}>|<{typ},", program) and not re.search(rf"\b{typ}\s*\(", program):
                    problems.append(f"[di] {m.group(1)} يستخدم {typ} وهو غير مسجّل في Program.cs")

    # 5) كل نداء خدمة من متحكم يجب أن يقابل عضواً في الخدمة
    for f in api_files:
        if "Controllers" not in str(f):
            continue
        text = strip_comments(f.read_text(encoding="utf-8"))
        for cm in re.finditer(rf"public\s+sealed\s+class\s+({IDENT})\s*\(([^)]*)\)", text):
            cname, params = cm.group(1), cm.group(2)
            body = block_after(text, text.index("{", text.index(")", cm.end())))
            for pm in re.finditer(rf"({IDENT}(?:<[^>]*>)?)\s+({IDENT})", params):
                typ, var = pm.group(1), pm.group(2)
                if typ not in classes:
                    continue
                for call in re.finditer(rf"\b{var}\.({IDENT})\s*\(", body):
                    if call.group(1) not in classes[typ][1]:
                        problems.append(f"[call] {cname}: {typ}.{call.group(1)}() غير موجودة")

    if problems:
        print(f"عدد الملاحظات: {len(problems)}\n")
        for p in problems:
            print(p)
        return 1
    print("لا ملاحظات: كل الواجهات منفّذة ومسجّلة.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
