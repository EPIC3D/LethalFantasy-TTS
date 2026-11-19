"""Local TTS server for the LethalFantasy TTS plugin (Kokoro engine).

Run:  .tools\\tts-venv\\Scripts\\python server\\tts_server.py [--port 5123]

POST /tts  JSON {"text": str, "voice": str, "lang": "a"|"b", "speed": float, "pitch": float}
  -> 200 audio/wav (mono, 16-bit PCM, 24 kHz)
  voice: a Kokoro voice name or a weighted blend "af_bella*0.7+af_jessica*0.3"
  pitch: >1 raises the voice; tempo is preserved (speed is compensated before resampling)
GET /health -> 200 "ok"

Kokoro-82M and its voices are Apache-2.0 licensed (https://huggingface.co/hexgrad/Kokoro-82M).
"""
import argparse
import io
import json
import os
import threading
import time
import warnings
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

os.environ.setdefault("HF_HUB_DISABLE_SYMLINKS_WARNING", "1")
warnings.filterwarnings("ignore")

import numpy as np  # noqa: E402
import soundfile as sf  # noqa: E402
import torch  # noqa: E402
from kokoro import KPipeline  # noqa: E402

SAMPLE_RATE = 24000
REPO_ID = "hexgrad/Kokoro-82M"


class Engine:
    def __init__(self, device):
        self.device = device
        self.pipelines = {}
        self.voices = {}
        self.lock = threading.Lock()  # one synthesis at a time on the GPU

    def pipeline(self, lang):
        pipe = self.pipelines.get(lang)
        if pipe is None:
            pipe = self.pipelines[lang] = KPipeline(lang_code=lang, device=self.device, repo_id=REPO_ID)
        return pipe

    def voice(self, pipe, spec):
        key = (pipe.lang_code, spec)
        if key not in self.voices:
            parts = []
            for part in spec.split("+"):
                name, _, weight = part.partition("*")
                parts.append((name.strip(), float(weight) if weight else 1.0))
            total = sum(w for _, w in parts)
            self.voices[key] = sum(pipe.load_voice(n) * (w / total) for n, w in parts)
        return self.voices[key]

    def synth(self, text, spec, lang, speed, pitch):
        with self.lock:
            pipe = self.pipeline(lang)
            voice = self.voice(pipe, spec)
            chunks = [torch.as_tensor(a) for _, _, a in pipe(text, voice=voice, speed=speed / pitch)]
        if not chunks:
            return np.zeros(0, dtype=np.float32)
        wav = torch.cat(chunks).numpy().astype(np.float32)
        if pitch != 1.0:
            n = int(len(wav) / pitch)
            wav = np.interp(np.arange(n) * pitch, np.arange(len(wav)), wav).astype(np.float32)
        return wav


ENGINE = None


class Handler(BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):
        pass

    def do_GET(self):
        if self.path == "/health":
            self._send(200, b"ok", "text/plain")
        else:
            self._send(404, b"not found", "text/plain")

    def do_POST(self):
        if self.path != "/tts":
            self._send(404, b"not found", "text/plain")
            return
        try:
            req = json.loads(self.rfile.read(int(self.headers.get("Content-Length", 0))))
            text = (req.get("text") or "").strip()
            started = time.time()
            wav = ENGINE.synth(text, req.get("voice", "af_bella"), req.get("lang", "a"),
                               float(req.get("speed", 1.0)), float(req.get("pitch", 1.0))) if text else \
                np.zeros(0, dtype=np.float32)
            buf = io.BytesIO()
            sf.write(buf, wav, SAMPLE_RATE, format="WAV", subtype="PCM_16")
            print(f"[tts] {time.time() - started:.2f}s {len(wav) / SAMPLE_RATE:.1f}s audio: {text[:60]}", flush=True)
            self._send(200, buf.getvalue(), "audio/wav")
        except Exception as e:  # keep the server alive on bad input
            print("[tts] error:", repr(e), flush=True)
            self._send(500, str(e).encode("utf-8"), "text/plain")

    def _send(self, code, body, ctype):
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


def main():
    global ENGINE
    ap = argparse.ArgumentParser()
    ap.add_argument("--port", type=int, default=5123)
    # CPU by default: on the GPU the game's rendering made synthesis swing between 0.2 and 8 s.
    ap.add_argument("--device", default="cpu", help="cpu (default) or cuda")
    args = ap.parse_args()
    ENGINE = Engine(args.device)
    for lang in ("a", "b"):  # preload so the first line is not slow
        ENGINE.synth("Ready.", "af_bella" if lang == "a" else "bf_alice", lang, 1.0, 1.0)
    print(f"[tts] Kokoro ready on {args.device}, http://127.0.0.1:{args.port}", flush=True)
    ThreadingHTTPServer(("127.0.0.1", args.port), Handler).serve_forever()


if __name__ == "__main__":
    main()
