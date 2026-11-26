"""Exercise a running API using synthetic claims only. No external dependencies."""
import json
import sys
import time
from html.parser import HTMLParser
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


def request(path, body=None):
    req = Request(sys.argv[1].rstrip("/") + path, body,
                  {"Content-Type": "application/json"} if body is not None else {})
    try:
        response = urlopen(req, timeout=10)
    except HTTPError as err:
        response = err
    with response:
        return response.status, json.load(response)


def check(actual, expected):
    if actual != expected:
        raise RuntimeError(f"Expected {expected!r}, got {actual!r}")


for attempt in range(30):
    try:
        check(request("/healthz"), (200, {"status": "ok"}))
        break
    except (URLError, ConnectionError, ValueError, RuntimeError):
        if attempt == 29:
            raise
        time.sleep(2)

class PageAssets(HTMLParser):
    def __init__(self):
        super().__init__()
        self.assets = {}
        self.has_form = False

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "form" and attrs.get("id") == "claim-form":
            self.has_form = True
        if tag == "script" and attrs.get("src"):
            self.assets[attrs["src"]] = "text/javascript"
        if tag == "link" and attrs.get("rel") == "stylesheet":
            self.assets[attrs["href"]] = "text/css"


page = PageAssets()
with urlopen(sys.argv[1].rstrip("/") + "/", timeout=10) as response:
    check(response.status, 200)
    check(response.headers.get_content_type(), "text/html")
    page.feed(response.read().decode("utf-8"))
check(page.has_form, True)
check(len(page.assets), 2)
for path, content_type in page.assets.items():
    if not path.startswith("/") or path.startswith("//"):
        raise RuntimeError(f"Expected a same-origin asset: {path}")
    with urlopen(sys.argv[1].rstrip("/") + path, timeout=10) as response:
        check(response.status, 200)
        check(response.headers.get_content_type(), content_type)
        if not response.read():
            raise RuntimeError(f"Empty web asset: {path}")
check(request("/Web/index.html")[0], 404)

for loss, days, payable, queue, reasons in [
    (230000, 30, 180000, "standard", []),
    (50000, 0, 0, "adjuster-review", ["WITHIN_DEDUCTIBLE"]),
    (900000, 31, 500000, "adjuster-review", ["POLICY_LIMIT_APPLIED", "LATE_REPORT"]),
]:
    claim = dict(lossCents=loss, deductibleCents=50000, limitCents=500000, daysToReport=days)
    check(request("/claims/analyze", json.dumps(claim).encode()), (200, dict(
        estimatedPayableCents=payable, queue=queue, reasons=reasons)))
    print(f"loss={loss} days={days} -> payable={payable} queue={queue} reasons={reasons}")

for invalid in [b"{}", b"not-json", b'{"lossCents":0,"deductibleCents":0,"limitCents":1,"daysToReport":1}']:
    check(request("/claims/analyze", invalid)[0], 400)
check(request("/claims/analyze", b"x" * 2049)[0], 413)
print("Frontend assets, health, three claim outcomes, invalid input and body limit passed.")
