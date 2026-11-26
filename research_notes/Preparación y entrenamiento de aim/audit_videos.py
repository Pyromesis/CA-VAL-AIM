import json
import re
import time
import urllib.parse
import urllib.request
from pathlib import Path

root = Path(__file__).resolve().parents[2]
source = (root / "CaValAim/src/Core/Academy.cs").read_text(encoding="utf-8")
pattern = re.compile(r'Yt\("([^"]+)",\s*"([^"]+)",\s*"((?:\\.|[^"])*)"')
results = []
for video_id, declared_author, declared_title in pattern.findall(source):
    url = "https://www.youtube.com/watch?v=" + video_id
    endpoint = "https://www.youtube.com/oembed?" + urllib.parse.urlencode(
        {"url": url, "format": "json"}
    )
    item = {
        "id": video_id,
        "url": url,
        "declared_author": declared_author,
        "declared_title": declared_title,
    }
    try:
        request = urllib.request.Request(
            endpoint, headers={"User-Agent": "CA-VAL-AIM-credit-audit/1.0"}
        )
        with urllib.request.urlopen(request, timeout=15) as response:
            data = json.load(response)
        item.update(
            status="verified",
            actual_author=data.get("author_name", ""),
            actual_title=data.get("title", ""),
        )
    except Exception as exc:
        item.update(status="unverified", error=f"{type(exc).__name__}: {exc}")
    item["author_match"] = (
        item.get("actual_author", "").casefold() == declared_author.casefold()
    )
    item["title_match"] = (
        item.get("actual_title", "").casefold() == declared_title.casefold()
    )
    results.append(item)
    time.sleep(0.2)

out = Path(__file__).resolve().parent
(out / "academy_video_audit.json").write_text(
    json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8"
)
lines = ["# Academy video audit", "", f"Checked: {len(results)} unique links", ""]
for item in results:
    lines.append(f"## {item['id']} — {item['status']}")
    lines.append(f"- URL: {item['url']}")
    lines.append(f"- Declared: {item['declared_author']} — {item['declared_title']}")
    if item["status"] == "verified":
        lines.append(f"- oEmbed: {item['actual_author']} — {item['actual_title']}")
        lines.append(
            f"- Exact author match: {item['author_match']}; exact title match: {item['title_match']}"
        )
    else:
        lines.append(f"- Error (not evidence of removal): {item['error']}")
    lines.append("")
(out / "academy_creditos_existentes.md").write_text("\n".join(lines), encoding="utf-8")
print(
    f"TOTAL={len(results)} VERIFIED={sum(x['status'] == 'verified' for x in results)} UNVERIFIED={sum(x['status'] != 'verified' for x in results)}"
)
for item in results:
    if (
        item["status"] != "verified"
        or not item["author_match"]
        or not item["title_match"]
    ):
        print(json.dumps(item, ensure_ascii=True))
