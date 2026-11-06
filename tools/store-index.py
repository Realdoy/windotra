# Rebuilds docs/store/index.json from the style and widget files in docs/store, so sharing only needs the file.
import json, glob, os
items = []
for kind, folder, ext in (("widget", "widgets", ".windotra-widget"), ("style", "styles", ".windotra-style")):
    for path in sorted(glob.glob(f"docs/store/{folder}/*{ext}")):
        try:
            data = json.load(open(path, encoding="utf-8-sig"))
        except Exception as e:
            print("skipped", path, e); continue
        if data.get("Format") != f"windotra-{kind}" or not data.get("Name"):
            print("skipped", path); continue
        items.append({"Type": kind, "Name": str(data["Name"])[:60], "Author": str(data.get("Author", ""))[:40],
                      "Description": str(data.get("Description", ""))[:200], "File": f"{folder}/{os.path.basename(path)}"})
json.dump({"Items": items}, open("docs/store/index.json", "w", encoding="utf-8"), indent=1)
print(len(items), "items")
