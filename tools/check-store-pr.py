# Checks a store upload without running anything from it. Allowed: up to 6 files, each either a valid style or
# widget in docs/store/styles|widgets (JSON, under 32 KB), or a preview picture in docs/store/previews (PNG or JPG,
# under 1 MB). Prints "ok" or the reason it was refused.
import json, re, sys
files = json.load(open(sys.argv[1], encoding="utf-8"))  # [{"filename","status","size","content"}]
if not files or len(files) > 6: sys.exit("refused: 1 to 6 files per upload")
for f in files:
    name, status = f["filename"], f["status"]
    if status not in ("added", "modified"): sys.exit(f"refused: {name} was {status}")
    if re.fullmatch(r"docs/store/previews/[a-z0-9-]{1,80}\.(png|jpg)", name):
        if f.get("size", 0) > 1024 * 1024: sys.exit(f"refused: {name} is larger than 1 MB")
        continue
    m = re.fullmatch(r"docs/store/(widgets|styles)/[A-Za-z0-9._ -]{1,80}\.windotra-(widget|style)", name)
    if not m or (m.group(1) == "widgets") != (m.group(2) == "widget"): sys.exit(f"refused: {name} is outside the store folders")
    text = f.get("content", "")
    if len(text.encode("utf-8")) > 32 * 1024: sys.exit(f"refused: {name} is larger than 32 KB")
    try: data = json.loads(text)
    except Exception as e: sys.exit(f"refused: {name} is not valid JSON ({e})")
    kind = m.group(2)
    if data.get("Format") != f"windotra-{kind}" or data.get("Version") != 1 or not str(data.get("Name", "")).strip(): sys.exit(f"refused: {name} is not a WinDotra {kind}")
    if len(str(data["Name"])) > 60: sys.exit(f"refused: {name} has a name over 60 characters")
    url = str(data.get("AuthorUrl", ""))
    if url and not re.fullmatch(r"https://github\.com/[A-Za-z0-9-]{1,39}", url): sys.exit(f"refused: {name} has an author link that isn't a GitHub profile")
    images = data.get("Images", []) or []
    if not isinstance(images, list) or len(images) > 4 or any(not re.fullmatch(r"previews/[a-z0-9-]{1,80}\.(png|jpg)", str(i)) for i in images): sys.exit(f"refused: {name} lists invalid pictures")
    if re.search(r"<script|javascript:", text, re.I): sys.exit(f"refused: {name} contains script text")
print("ok")
