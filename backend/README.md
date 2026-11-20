# NORSCOPE Ask AI Backend (Phase 4)

Minimal FastAPI service that accepts a technician question + JPEG from the HoloLens and returns a structured maintenance answer. The OpenAI API key stays on the server only.

## Environment

Copy `.env.example` to `.env` and set:

```
OPENAI_API_KEY=<your key>
OPENAI_MODEL=gpt-5.6-luna
```

Higher-quality alternative:

```
OPENAI_MODEL=gpt-5.6-terra
```

Never commit `.env`.

## Start (LAN-reachable)

From the `backend` directory:

```bash
python -m venv .venv
# Windows:
.venv\Scripts\activate
pip install -r requirements.txt
uvicorn app.main:app --host 0.0.0.0 --port 8000
```

## Local HoloLens testing

1. PC and HoloLens on the same Wi-Fi (disable client isolation).
2. Allow inbound TCP 8000 on the PC firewall.
3. Find the PC LAN IP (e.g. `192.168.1.42`).
4. On another device first open: `http://<PC-LAN-IP>:8000/health`
5. Point Unity `AskAiBackendConfig.baseUrl` at `http://<PC-LAN-IP>:8000`  
   Do **not** use `localhost` on the headset (that is the headset itself).

Editor Play Mode can use `http://127.0.0.1:8000` or the Editor mock analysis service.

Production must use HTTPS. This prototype does not disable TLS validation.

## Privacy

Uploaded images and transcripts are processed in memory only. They are not written to disk, stored in a database, or logged.
