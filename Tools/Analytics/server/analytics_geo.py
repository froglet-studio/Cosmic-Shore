"""Country from an IP address, so the console can say where players play from. The IP itself is never stored: ingest
looks the country up and keeps only the two-letter code (Docs/Analytics/DATA_ARCHITECTURE.md §8.9, decided by
Shombith 2026-11-05).

The lookup reads DB-IP's free "IP to Country Lite" CSV (CC BY 4.0: pages that show its countries credit
"IP Geolocation by DB-IP", https://db-ip.com). Rows are `first_ip,last_ip,country`. `refresh()` downloads this
month's file; the nightly job calls it when the file is over 32 days old. Without the file, country is None and
nothing else changes. Standard library only.
"""
import bisect
import csv
import gzip
import io
import ipaddress
import os
import time
import urllib.request
from datetime import datetime, timezone

URL = "https://download.db-ip.com/free/dbip-country-lite-{month}.csv.gz"
MAX_AGE_S = 32 * 86400


class CountryLookup:
    def __init__(self, path):
        self.path = path
        self.ranges = {4: ([], [], []), 6: ([], [], [])}
        self.loaded = False
        if path and os.path.exists(path):
            self._load()

    def _load(self):
        opener = gzip.open if self.path.endswith(".gz") else open
        ranges = {4: [], 6: []}
        with opener(self.path, "rt", encoding="utf-8", newline="") as handle:
            for row in csv.reader(handle):
                if len(row) < 3:
                    continue
                try:
                    first, last = ipaddress.ip_address(row[0]), ipaddress.ip_address(row[1])
                except ValueError:
                    continue
                ranges[first.version].append((int(first), int(last), row[2].strip().upper()))
        for version, rows in ranges.items():
            rows.sort()
            self.ranges[version] = ([r[0] for r in rows], [r[1] for r in rows], [r[2] for r in rows])
        self.loaded = bool(ranges[4] or ranges[6])

    def country(self, ip):
        """Two-letter country code, or None (no file, a private address, or not in the table)."""
        if not self.loaded or not ip:
            return None
        try:
            address = ipaddress.ip_address(ip.strip())
        except ValueError:
            return None
        if address.is_private or address.is_loopback or address.is_reserved:
            return None
        starts, ends, codes = self.ranges[address.version]
        i = bisect.bisect_right(starts, int(address)) - 1
        if i >= 0 and int(address) <= ends[i] and codes[i] not in ("ZZ", ""):
            return codes[i]
        return None


def stale(path):
    return not os.path.exists(path) or time.time() - os.path.getmtime(path) > MAX_AGE_S


def refresh(path, opener=urllib.request.urlopen):
    """Download this month's file to `path` (gzip kept as is). Returns True when a new file was written."""
    month = datetime.now(timezone.utc).strftime("%Y-%m")
    with opener(URL.format(month=month), timeout=60) as response:
        data = response.read()
    gzip.GzipFile(fileobj=io.BytesIO(data)).read(64)       # refuse anything that is not a gzip file
    tmp = path + ".tmp"
    with open(tmp, "wb") as handle:
        handle.write(data)
    os.replace(tmp, path)
    return True
