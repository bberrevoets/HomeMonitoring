#!/usr/bin/env python3
"""Replace every "InSecrets" marker in an appsettings.json with a real secret value.

For each JSON leaf whose value is exactly "InSecrets", the config-key path is joined
with "__" to form an environment-variable name (the same separator .NET uses for
hierarchical keys, and the only one GitHub Actions secret names allow). The value of
that env var is substituted in place.

The replacement is key-path aware on purpose: several leaves share the identical
"InSecrets" marker, so a blind text find/replace could not tell them apart.

If any marker has no matching (non-empty) env var, nothing is written for that file
and the script exits non-zero listing every unmatched key -- so an "InSecrets" literal
can never be shipped to production.

Usage:
    replace-secrets.py <appsettings.json> [<appsettings.json> ...]

Run against the *published* output (out/<proj>/appsettings.json), never the repo source.
"""
import json
import os
import sys

MARKER = "InSecrets"


def walk(node, path, missing):
    if isinstance(node, dict):
        return {k: walk(v, path + [k], missing) for k, v in node.items()}
    if isinstance(node, list):
        return [walk(v, path + [str(i)], missing) for i, v in enumerate(node)]
    if node == MARKER:
        name = "__".join(path)
        val = os.environ.get(name)
        if not val:
            missing.append(name)
            return node
        return val
    return node


def main(paths):
    bad = []
    for p in paths:
        with open(p, encoding="utf-8") as f:
            data = json.load(f)
        missing = []
        data = walk(data, [], missing)
        if missing:
            bad += [f"{p}: {name}" for name in missing]
            continue
        with open(p, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
            f.write("\n")
        print(f"tokenized {p}")
    if bad:
        print("ERROR: no secret provided for:", *(f"  {b}" for b in bad),
              sep="\n", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("usage: replace-secrets.py <appsettings.json> [...]", file=sys.stderr)
        sys.exit(2)
    main(sys.argv[1:])
