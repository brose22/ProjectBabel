"""Compile-check a ProjectBabel checkout or worktree without opening Unity.

Usage: python -I Tools/compile_check.py <checkout root> [<out dir>]

Builds each runtime asmdef, Assembly-CSharp (all Assets/**/*.cs outside Editor/ folders and
asmdef folders), Assembly-CSharp-Editor (Assets/**/Editor/*.cs) and each test asmdef, using the
response files Unity cached the last time it compiled the main project. Prints compiler output
and exits non-zero on any error.

The main project must have been compiled by Unity at least once (it supplies Library/Bee).
Set BABEL_PROJECT to point at the main project when running from elsewhere; by default it is
the folder above this script.
"""
import glob
import json
import os
import subprocess
import sys
import tempfile

PROJECT = os.environ.get("BABEL_PROJECT", os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
EDITOR = r"C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Data\DotNetSdk"
DOTNET = os.path.join(EDITOR, "dotnet.exe")
CSC = os.path.join(EDITOR, r"sdk\8.0.318\Roslyn\bincore\csc.dll")


def find_dag():
    # Unity names the build folder after a hash, so take whichever holds the editor's rsp files.
    # Player builds leave a folder with only the runtime rsp, so require the editor one too.
    candidates = [c for c in glob.glob(os.path.join(PROJECT, "Library", "Bee", "artifacts", "*.dag", "Assembly-CSharp.rsp"))
                  if os.path.exists(os.path.join(os.path.dirname(c), "Assembly-CSharp-Editor.rsp"))]
    if not candidates:
        sys.exit(f"No Assembly-CSharp.rsp under {PROJECT}\\Library\\Bee; open the project in Unity once first.")
    return os.path.dirname(max(candidates, key=os.path.getmtime))


DAG = find_dag()


def absolutize(value):
    quoted = value.startswith('"')
    v = value.strip('"')
    if not os.path.isabs(v):
        v = os.path.join(PROJECT, v.replace("/", "\\"))
    return f'"{v}"' if quoted or " " in v else v


def rewrite(rsp_name, sources, out_dll, extra_refs=(), drop_ref=None):
    lines = []
    with open(os.path.join(DAG, rsp_name), encoding="utf-8-sig") as f:
        for raw in f:
            line = raw.rstrip("\r\n")
            if not line:
                continue
            if line.startswith('"Assets/') or line.startswith("Assets/"):
                continue
            if line.startswith("-analyzer:") or line.startswith("/additionalfile:"):
                continue
            if line.startswith("-out:") or line.startswith("-refout:"):
                continue
            if line.startswith("-r:"):
                ref = line[3:]
                if drop_ref and drop_ref in ref:
                    continue
                line = "-r:" + absolutize(ref)
            lines.append(line)
    lines.append(f'-out:"{out_dll}"')
    for r in extra_refs:
        lines.append(f'-r:"{r}"')
    for s in sources:
        lines.append(f'"{s}"')
    return "\n".join(lines) + "\n"


def collect_asmdef(dirpath, asmdef):
    with open(os.path.join(dirpath, asmdef), encoding="utf-8-sig") as f:
        info = json.load(f)
    files = []
    for d, dn, fns in os.walk(dirpath):
        if d != dirpath and any(fn.endswith(".asmdef") for fn in fns):
            dn[:] = []
            continue
        files += [os.path.join(d, fn) for fn in fns if fn.endswith(".cs")]
    refs = " ".join(info.get("references", [])) + " " + " ".join(info.get("precompiledReferences", []))
    is_test = "TestRunner" in refs or "nunit" in refs.lower() or "UNITY_INCLUDE_TESTS" in info.get("defineConstraints", [])
    return {"name": info["name"], "dir": dirpath, "files": sorted(files), "test": is_test}


def collect(root):
    runtime, editor, asmdefs = [], [], []
    assets = os.path.join(root, "Assets")
    for dirpath, dirnames, filenames in os.walk(assets):
        parts = os.path.relpath(dirpath, assets).split(os.sep)
        # Folders with their own asmdef are separate assemblies, compiled on their own.
        own = [fn for fn in filenames if fn.endswith(".asmdef")]
        if own:
            asmdefs.append(collect_asmdef(dirpath, own[0]))
            dirnames[:] = []
            continue
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(dirpath, fn)
            (editor if "Editor" in parts else runtime).append(path)
    return sorted(runtime), sorted(editor), asmdefs


def run(rsp_text, out_dir, name):
    rsp = os.path.join(out_dir, name + ".rsp")
    with open(rsp, "w", encoding="utf-8") as f:
        f.write(rsp_text)
    proc = subprocess.run([DOTNET, "exec", CSC, "/nostdlib", "/noconfig", "@" + rsp],
                          cwd=PROJECT, capture_output=True, text=True)
    out = (proc.stdout + proc.stderr).strip()
    errors = [l for l in out.splitlines() if "error" in l]
    print(f"== {name}: exit {proc.returncode}, {len(errors)} error line(s)")
    if out:
        print(out)
    return proc.returncode == 0


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    root = os.path.abspath(sys.argv[1])
    out_dir = sys.argv[2] if len(sys.argv) > 2 else tempfile.mkdtemp(prefix="babel_csc_")
    os.makedirs(out_dir, exist_ok=True)
    runtime, editor, asmdefs = collect(root)
    print(f"runtime files: {len(runtime)}, editor files: {len(editor)}, "
          f"asmdefs: {[a['name'] for a in asmdefs]}, out: {out_dir}")
    ok = True
    # Runtime asmdefs first (in path order; they may reference earlier ones), then the
    # predefined assemblies, then test asmdefs built against the editor references
    # (which include NUnit and the test runner) but deliberately NOT Assembly-CSharp.
    asm_dlls = []
    for a in [a for a in asmdefs if not a["test"]]:
        dll = os.path.join(out_dir, a["name"] + ".dll")
        ok = ok and run(rewrite("Assembly-CSharp.rsp", a["files"], dll, extra_refs=asm_dlls), out_dir, a["name"])
        asm_dlls.append(dll)
    game_dll = os.path.join(out_dir, "Assembly-CSharp.dll")
    ok = ok and run(rewrite("Assembly-CSharp.rsp", runtime, game_dll, extra_refs=asm_dlls), out_dir, "Assembly-CSharp")
    if ok and editor:
        ok = run(rewrite("Assembly-CSharp-Editor.rsp", editor,
                         os.path.join(out_dir, "Assembly-CSharp-Editor.dll"),
                         extra_refs=[game_dll] + asm_dlls, drop_ref="Assembly-CSharp.ref.dll"),
                 out_dir, "Assembly-CSharp-Editor")
    for a in [a for a in asmdefs if a["test"]]:
        ok = ok and run(rewrite("Assembly-CSharp-Editor.rsp", a["files"], os.path.join(out_dir, a["name"] + ".dll"),
                                extra_refs=asm_dlls, drop_ref="Assembly-CSharp.ref.dll"), out_dir, a["name"])
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
