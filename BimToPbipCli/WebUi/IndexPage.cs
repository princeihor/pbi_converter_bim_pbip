namespace BimToPbipCli.WebUi;

/// <summary>The single-page web UI served at "/". Plain HTML/CSS/JS, no build step.</summary>
public static class IndexPage
{
    public const string Html = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8"/>
        <meta name="viewport" content="width=device-width, initial-scale=1"/>
        <title>BIM &#8594; PBIP Converter</title>
        <style>
          :root { color-scheme: light dark; }
          * { box-sizing: border-box; }
          body {
            font-family: Segoe UI, system-ui, sans-serif;
            margin: 0; padding: 32px 16px;
            background: #f3f4f6; color: #1f2937;
          }
          .card {
            max-width: 720px; margin: 0 auto; background: #fff;
            border-radius: 12px; padding: 28px 32px;
            box-shadow: 0 8px 28px rgba(0,0,0,.12);
          }
          h1 { margin: 0 0 4px; font-size: 22px; }
          .sub { margin: 0 0 22px; color: #6b7280; font-size: 14px; }
          code { background: #eef2ff; padding: 1px 5px; border-radius: 4px; font-size: 12px; }
          label { display: block; margin: 14px 0 4px; font-weight: 600; font-size: 13px; }
          .req { color: #dc2626; }
          input[type=text] {
            width: 100%; padding: 9px 10px; border: 1px solid #d1d5db;
            border-radius: 7px; font-size: 13px; font-family: Consolas, monospace;
          }
          .row { display: flex; gap: 8px; }
          .row input { flex: 1; }
          button {
            border: 0; border-radius: 7px; padding: 9px 14px;
            font-size: 13px; cursor: pointer; font-weight: 600;
          }
          .browse { background: #e5e7eb; color: #1f2937; white-space: nowrap; }
          .browse:hover { background: #d1d5db; }
          .check { font-weight: 500; display: flex; align-items: center; gap: 8px; margin-top: 16px; }
          .check input { margin: 0; }
          .primary {
            background: #4f46e5; color: #fff; width: 100%;
            margin-top: 22px; padding: 12px; font-size: 15px;
          }
          .primary:hover { background: #4338ca; }
          button:disabled { opacity: .55; cursor: default; }
          #status { margin-top: 18px; font-size: 14px; font-weight: 600; }
          #status.busy { color: #b45309; }
          #status.ok { color: #15803d; }
          #status.err { color: #dc2626; }
          .log {
            margin-top: 12px; background: #0f172a; color: #e2e8f0;
            border-radius: 8px; padding: 14px; font-size: 12px;
            font-family: Consolas, monospace; white-space: pre-wrap;
            max-height: 320px; overflow: auto;
          }
          .log:empty { display: none; }
          .lvl-error { color: #f87171; }
          .lvl-warn { color: #fbbf24; }
          .lvl-success { color: #4ade80; }
          .lvl-step { color: #93c5fd; }
          .lvl-detail { color: #cbd5e1; }
          .result {
            margin-top: 14px; padding: 14px; background: #ecfdf5;
            border: 1px solid #a7f3d0; border-radius: 8px;
          }
          .result code { background: #d1fae5; }
          .hidden { display: none; }
          #openFolder { background: #059669; color: #fff; margin-top: 10px; }
        </style>
        </head>
        <body>
        <div class="card">
          <h1>BIM &#8594; PBIP Converter</h1>
          <p class="sub">Convert a Tabular <code>model.bim</code> into a Power BI
            Desktop project (PBIP). No pbi-tools, no install, no internet needed.</p>

          <label>model.bim file <span class="req">*</span></label>
          <div class="row">
            <input id="bim" type="text" placeholder="C:\Models\MyModel.bim"/>
            <button class="browse" data-type="bim">Browse&#8230;</button>
          </div>

          <label>Output project folder</label>
          <div class="row">
            <input id="out" type="text" placeholder="(default: a folder next to the .bim file)"/>
            <button class="browse" data-type="folder">Browse&#8230;</button>
          </div>

          <label>Dataset / project name</label>
          <input id="dataset" type="text" placeholder="(default: the .bim file name)"/>

          <button id="convert" class="primary">Convert</button>

          <div id="status"></div>
          <pre id="log" class="log"></pre>

          <div id="result" class="result hidden">
            <div>PBIP project created at: <code id="resultPath"></code></div>
            <button id="openFolder">Open project folder</button>
          </div>
        </div>

        <script>
          const $ = id => document.getElementById(id);
          const inputFor = { bim: 'bim', folder: 'out' };

          async function postJson(url, body) {
            const r = await fetch(url, {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify(body || {})
            });
            return await r.json();
          }

          document.querySelectorAll('.browse').forEach(btn => {
            btn.addEventListener('click', async () => {
              btn.disabled = true;
              try {
                const res = await postJson('/api/pick', { type: btn.dataset.type });
                if (res && res.path) $(inputFor[btn.dataset.type]).value = res.path;
              } catch (e) {
                alert('File picker is unavailable: ' + e + '\nType the path manually.');
              } finally {
                btn.disabled = false;
              }
            });
          });

          $('convert').addEventListener('click', async () => {
            const bim = $('bim').value.trim();
            if (!bim) { alert('Please choose the model.bim file first.'); return; }

            $('convert').disabled = true;
            $('log').textContent = '';
            $('result').classList.add('hidden');
            $('status').className = 'busy';
            $('status').textContent = 'Converting…';

            try {
              const res = await postJson('/api/convert', {
                bim: bim,
                out: $('out').value.trim(),
                dataset: $('dataset').value.trim()
              });
              renderLog(res.log);
              if (res.success) {
                $('status').className = 'ok';
                $('status').textContent = 'Done.';
                $('resultPath').textContent = res.projectPath;
                $('openFolder').dataset.path = res.projectPath;
                $('result').classList.remove('hidden');
              } else {
                $('status').className = 'err';
                $('status').textContent = 'Failed (exit code ' + res.exitCode + ').';
              }
            } catch (e) {
              $('status').className = 'err';
              $('status').textContent = 'Error: ' + e;
            } finally {
              $('convert').disabled = false;
            }
          });

          $('openFolder').addEventListener('click', async () => {
            const p = $('openFolder').dataset.path;
            if (p) await postJson('/api/open', { path: p });
          });

          function renderLog(entries) {
            const log = $('log');
            log.textContent = '';
            (entries || []).forEach(e => {
              const span = document.createElement('span');
              span.className = 'lvl-' + e.level;
              const prefix = e.level === 'detail'
                ? ''
                : '[' + e.level.toUpperCase().padEnd(7) + '] ';
              span.textContent = prefix + e.message + '\n';
              log.appendChild(span);
            });
            log.scrollTop = log.scrollHeight;
          }
        </script>
        </body>
        </html>
        """;
}
