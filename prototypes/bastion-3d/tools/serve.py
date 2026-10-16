"""Serve the self-contained scene on the local loopback interface."""
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
from pathlib import Path
import argparse
import os

parser = argparse.ArgumentParser()
parser.add_argument("--port", type=int, default=5183)
args = parser.parse_args()
os.chdir(Path(__file__).resolve().parents[1])

class Handler(SimpleHTTPRequestHandler):
    extensions_map = {**SimpleHTTPRequestHandler.extensions_map, ".js": "text/javascript", ".glb": "model/gltf-binary"}
    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

print(f"Cinderworks 3D: http://127.0.0.1:{args.port}", flush=True)
ThreadingHTTPServer(("127.0.0.1", args.port), Handler).serve_forever()
