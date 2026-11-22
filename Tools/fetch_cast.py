#!/usr/bin/env python3
"""Downloads the new all-Indian character cast from Sketchfab (all CC BY 4.0: free, commercial use allowed,
redistribution allowed with credit - credits are written to Assets/Resources/Characters/CAST_CREDITS.txt).

Sketchfab only lets a logged-in account download. Run this yourself, with your own API token - it stays on your
machine (get it at https://sketchfab.com/settings/password -> "API Token"; a free account is enough):

    SKETCHFAB_TOKEN=paste_your_token_here python3 Tools/fetch_cast.py

Files go to ~/Downloads/pinnacle_cast/<slug>.glb (about 1-2 GB in all). Already downloaded files are skipped.
Then tell Claude they are there; it imports them, wires every role, and deletes the old character pack.
"""
import json, os, sys, urllib.request

CAST = [
    # (sketchfab uid,                    slug,               role,                                   creator)
    ("c28105203127478b8e26ad0474083367", "kamala_saree",      "Kamala Kapoor (mother, sari)",          "dk8026854"),
    ("e6c60cafd33c4d54b0b840c4146ed244", "priya_office",      "Priya Kapoor (advocate)",               "ar.jethin"),
    ("fa943206b3884b69aa71765837c555f4", "reddy_salwar",      "Insp. Sonal Reddy (plainclothes)",      "nepalvr360"),
    ("830d8ed3236a4221862290b81807f408", "deshpande_modern",  "Councilor Deshpande",                   "ar.jethin"),
    ("a311adcb82a3457db16a548a8d261114", "vikram_kurta",      "Vikram Sahu (chai seller)",             "Kalakar"),
    ("1c20d4f362d84953904428aa1696d69d", "pandit_kurta",      "Pandit Harishankar",                    "iamandeep361"),
    ("fe7de2e1fbb642c9aebb7c3f698bec5e", "bhaskar_casual",    "Bhaskar (auto driver)",                 "ar.jethin"),
    ("985dd9756d89464b9380414b5b12d8aa", "raj_suit",          "Raj Malhotra (builder)",                "ar.jethin"),
    ("aa1cb70d9c4f4573a3ff7ae0a5bf82dd", "rohan_hoodie",      "Rohan Kapoor",                          "ar.jethin"),
    ("0a6adb6858464e3eb4ffcd2a99d7ba91", "sharma_older",      "Sharma-ji (librarian), the judge",      "ar.jethin"),
    ("bad2760e94e547c689025dc0d2db7f29", "lawyer_suit",       "Deshpande's lawyer, the Curator",       "ap-school"),
    ("76553b4f0dc84c6386e997c80b9b0017", "thug",              "Deshpande's men",                       "ar.jethin"),
    ("43814ac9155b4cb08e1fecdebc7511ae", "police_constable",  "Police constables",                     "ayushcodemate"),
    # the crowd
    ("091381a4eeb14e3b9814e3aaf075ac59", "man_tshirt",        "crowd", "ar.jethin"),
    ("13b46cdd311043d7a460245276c9771b", "man_pattern_shirt", "crowd", "ar.jethin"),
    ("58027d132f814952adc9c8a9ee09d8b1", "man_party_wear",    "crowd", "ar.jethin"),
    ("5a76738261d4415ebd18cefcdd8e13e6", "man_dj",            "crowd", "ar.jethin"),
    ("6d5f8515232f426cae4cdbd1ae6997ea", "man_sunglasses",    "crowd", "ar.jethin"),
    ("cf50624deae649ddbfccae9f4f213e6c", "man_football_fan",  "crowd", "ar.jethin"),
    ("8408c80ccd9548e4bad84bc0dd8e0d7e", "man_green_tee",     "crowd", "ar.jethin"),
    ("b2804b40f671431ca7b5cc53b239175e", "man_teen",          "crowd", "ar.jethin"),
    ("23b1805e473d4e5cb3e439b576ca39a9", "man_red_kurta",     "crowd", "pankhkhan"),
    ("7cdbd4f668524e42982acb09cba57152", "man_blue_kurta",    "crowd", "Himanshuvaid"),
    ("e70a140ad4334c2e8648ac78d61545b3", "man_old",           "crowd", "anandmohan662"),
    ("1ab2e8ecceee4e6e897f8fddb17d2b02", "woman_modern",      "crowd", "ar.jethin"),
    ("3cffef3bcde9469aa3d1ad36755b363c", "woman_hoodie",      "crowd", "ar.jethin"),
    ("a9e39f6c8e214776a02bfce983ec093f", "woman_teen",        "crowd", "ar.jethin"),
    # children
    ("dfc451b66a8045a98dcc8297ef3282b1", "boy_1",             "children", "rohitkashyap88593"),
    ("503c30a46e524bbe8601f95bb48cf110", "boy_2",             "children", "direhacker188"),
]

OUT = os.path.expanduser("~/Downloads/pinnacle_cast")


def get(url, token=None):
    req = urllib.request.Request(url, headers={"Authorization": "Token " + token} if token else {})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.load(r)


def main():
    token = os.environ.get("SKETCHFAB_TOKEN", "").strip()
    if not token:
        sys.exit("Set SKETCHFAB_TOKEN first (see the top of this file).")
    os.makedirs(OUT, exist_ok=True)
    meta = []
    for uid, slug, role, creator in CAST:
        info = get("https://api.sketchfab.com/v3/models/" + uid)
        lic = (info.get("license") or {}).get("label", "?")
        meta.append({"uid": uid, "slug": slug, "role": role, "name": info.get("name"), "creator": creator,
                     "license": lic, "url": info.get("viewerUrl")})
        path = os.path.join(OUT, slug + ".glb")
        if os.path.exists(path) and os.path.getsize(path) > 0:
            print("have", slug); continue
        if "Attribution" not in lic or "NonCommercial" in lic or "NoDerivs" in lic:
            print("SKIP", slug, "- license is now", lic); continue
        links = get("https://api.sketchfab.com/v3/models/%s/download" % uid, token)
        fmt = "glb" if "glb" in links else "gltf"
        url = links[fmt]["url"]
        dest = path if fmt == "glb" else os.path.join(OUT, slug + ".zip")
        print("get ", slug, fmt, "%.1f MB" % (links[fmt].get("size", 0) / 1e6))
        urllib.request.urlretrieve(url, dest)
    with open(os.path.join(OUT, "cast.json"), "w") as f:
        json.dump(meta, f, indent=1)
    print("\nDone:", OUT)


if __name__ == "__main__":
    main()
