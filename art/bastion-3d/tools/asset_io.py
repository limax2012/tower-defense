"""Atomic metadata writes for asset generation on indexed project directories."""
from __future__ import annotations
import json
import os
from pathlib import Path
import tempfile
import time


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = json.dumps(value, indent=2) + '\n'
    descriptor, temporary = tempfile.mkstemp(prefix='.' + path.name + '.', suffix='.tmp', dir=path.parent)
    try:
        with os.fdopen(descriptor, 'w', encoding='utf-8', newline='\n') as stream:
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        for attempt in range(8):
            try:
                os.replace(temporary, path)
                break
            except OSError:
                if attempt == 7:
                    raise
                time.sleep(min(.05 * 2 ** attempt, 1))
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)
