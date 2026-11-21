"""A local stand-in for UGS Player Authentication, for testing Prisma's `COSMIC_SHORE_RELAY=ugs` path
without touching the live UGS project (Port/docs/MULTIPLAYER.md §6.8).

It answers the two calls Prisma makes, in the documented shape:
  POST /v1/authentication/anonymous       (header ProjectId)            -> a new player
  POST /v1/authentication/session-token   {"sessionToken": ...}         -> the same player again
Any other project id is a 400 and an unknown session token a 401, as UGS answers.

    python3 ugs_auth_standin.py [PORT] [PROJECT_ID]

prints `COSMIC_SHORE_UGS_AUTH_URL=http://127.0.0.1:PORT` once it listens, and a line per sign-in.
"""
import itertools
import json
import secrets
import sys
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

PROJECT = sys.argv[2] if len(sys.argv) > 2 else "00000000-1111-2222-3333-444444444444"
players = {}  # session token -> player id
counter = itertools.count(1)
lock = threading.Lock()


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def reply(self, status, body):
        data = json.dumps(body).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_POST(self):
        length = int(self.headers.get("Content-Length") or 0)
        req = json.loads(self.rfile.read(length) or b"{}") if length else {}
        if self.headers.get("ProjectId") != PROJECT:
            return self.reply(400, {"title": "BAD_REQUEST", "detail": "unknown project"})
        with lock:
            if self.path == "/v1/authentication/anonymous":
                player, how = f"p{next(counter)}", "anonymous"
            elif self.path == "/v1/authentication/session-token" and req.get("sessionToken") in players:
                player, how = players[req["sessionToken"]], "session-token"
            else:
                return self.reply(401, {"title": "INVALID_SESSION_TOKEN"})
            session = "st-" + secrets.token_hex(8)
            players[session] = player
        print(f"[ugs-auth] {how} sign-in: player {player}", flush=True)
        self.reply(200, {"expiresIn": 3600, "idToken": f"id-{player}-{secrets.token_hex(8)}", "sessionToken": session,
                         "user": {"id": player, "disabled": False}, "userId": player})


if __name__ == "__main__":
    server = ThreadingHTTPServer(("127.0.0.1", int(sys.argv[1]) if len(sys.argv) > 1 else 0), Handler)
    print(f"[ugs-auth] project {PROJECT} (COSMIC_SHORE_UGS_AUTH_URL=http://127.0.0.1:{server.server_address[1]})", flush=True)
    server.serve_forever()
