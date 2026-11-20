#!/usr/bin/env python3
"""Compiles the game scripts (and, with --editor, the Editor scripts) with Unity's own Roslyn, against the exact
references in the Unity-generated .csproj files, without touching the open Editor. Prints errors and warnings in
the scripts we changed; exit code 0 when there are no errors.

    python3 Tools/compile_check.py [--editor] [--all-warnings]
"""
import os, re, subprocess, sys, tempfile

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
UNITY = "/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents"
DOTNET = UNITY + "/Resources/Scripting/DotNetSdk/dotnet"
CSC = UNITY + "/Resources/Scripting/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll"


def project(csproj):
    txt = open(os.path.join(ROOT, csproj)).read()
    refs = [r for r in re.findall(r"<HintPath>([^<]+)</HintPath>", txt) if r.strip()]
    defines = re.findall(r"<DefineConstants>([^<]*)</DefineConstants>", txt)
    srcs = [os.path.join(ROOT, s.replace("\\", "/")) for s in re.findall(r'<Compile Include="([^"]+)"', txt)]
    return refs, (defines[0] if defines else ""), srcs


def compile_one(csproj, extra_refs, out_name, all_warn):
    refs, defines, srcs = project(csproj)
    # pick up files added since Unity last wrote the .csproj
    want_dir = "Assets/Editor" if "Editor" in csproj else "Assets/Scripts"
    for dp, _, fs in os.walk(os.path.join(ROOT, want_dir)):
        for f in fs:
            p = os.path.join(dp, f)
            if f.endswith(".cs") and p not in srcs and "/Editor/" not in p.replace(ROOT, "")[len(want_dir):]:
                srcs.append(p)
    srcs = [s for s in srcs if os.path.exists(s)]
    out = os.path.join(tempfile.gettempdir(), out_name)
    rsp = os.path.join(tempfile.gettempdir(), out_name + ".rsp")
    with open(rsp, "w") as f:
        f.write("-target:library -nostdlib -noconfig -langversion:9.0 -nowarn:0169,0649,0414,0219 -unsafe\n")
        f.write("-out:\"%s\"\n" % out)
        f.write("-define:%s\n" % defines)
        for r in refs + extra_refs:
            f.write("-r:\"%s\"\n" % r)
        for s in srcs:
            f.write("\"%s\"\n" % s)
    r = subprocess.run([DOTNET, CSC, "@" + rsp], capture_output=True, text=True)
    lines = [l for l in (r.stdout + r.stderr).splitlines() if ": error " in l or (": warning " in l and (all_warn or "Assets/" in l))]
    errs = [l for l in lines if ": error " in l]
    for l in lines:
        print(l.replace(ROOT + "/", ""))
    print("%s: %d sources, %d errors, %d warnings" % (csproj, len(srcs), len(errs), len(lines) - len(errs)))
    return out if not errs else None


def main():
    all_warn = "--all-warnings" in sys.argv
    game = compile_one("Assembly-CSharp.csproj", [], "PinnacleGame.dll", all_warn)
    ok = game is not None
    if "--editor" in sys.argv and ok:
        ok = compile_one("Assembly-CSharp-Editor.csproj", [game], "PinnacleEditor.dll", all_warn) is not None
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
