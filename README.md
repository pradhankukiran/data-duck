# DataDuck

**A browser-based SQL data lab. Drop a CSV. Query with AI. No server.**

![License: MIT](https://img.shields.io/badge/license-MIT-F4C430.svg)
![Avalonia 12](https://img.shields.io/badge/Avalonia-12.0.2-5C2D91.svg)
![Live demo](https://img.shields.io/badge/demo-data--duck--lab.vercel.app-success.svg)

> 🌐 **Live:** [data-duck-lab.vercel.app](https://data-duck-lab.vercel.app) &nbsp;·&nbsp; **Repo:** [github.com/pradhankukiran/data-duck](https://github.com/pradhankukiran/data-duck)

---

## What it is

DataDuck is a single-page SQL workbench that runs entirely in your browser. Drop a CSV, Parquet, JSON, or JSONL file onto the page and DuckDB-WASM registers it as a table. You write SQL, get auto-detected charts, profile columns, find joins between files, and pin queries as dashboard tiles. No server, no upload, no telemetry — your data never leaves the tab.

Optional Groq integration (BYOK, key in `localStorage`) translates English to SQL with `llama-3.3-70b` and produces a one-shot dataset summary plus three suggested queries from the schema and a sample of rows. The whole thing is shipped as a static Vercel site built from an Avalonia 12 WebAssembly app.

---

## 🦆 Demo loop

1. Open the [live URL](https://data-duck-lab.vercel.app).
2. Click **🎲 Try sample data** — a 500-row `sales.csv` registers instantly.
3. Run `SELECT region, COUNT(*), SUM(amount) FROM sales GROUP BY region`.
4. The bar chart auto-renders. Switch tabs for line / pie / big-number tiles.
5. Paste a Groq key in **⚙ Settings** and ask **"top 5 customers by revenue"** in plain English.
6. Click **AI explain dataset** for a generated summary plus three suggested queries.
7. **Pin** the query as a tile, hit **Refresh all**, then **Export → Markdown**.
8. Refresh the page. Tabs, history, dashboard, and key are all restored from `localStorage`.

---

## ✨ Features

### Ingest

| Feature | Notes |
|---|---|
| Drag-and-drop loader | Drop CSV / Parquet / JSON / JSONL anywhere on the window |
| File picker fallback | Native file dialog via Avalonia's storage provider |
| Sample dataset | Bundled 500-row `sales.csv` for instant demo |
| Multi-file workspace | Load several files; each becomes a queryable table |

### Query

| Feature | Notes |
|---|---|
| DuckDB-WASM engine | Full SQL, ~33 MB Brotli-compressed bundle |
| Multi-tab editor | IDE-style tabs, persisted across reloads |
| Query history | Last 50 runs, click to reload, persisted across reloads |
| JOIN suggester | Confidence-scored matches across loaded files; click to insert |

### Visualize

| Feature | Notes |
|---|---|
| Auto charts | Bar / line (time-series) / pie (categorical) / big-number tile, picked from query shape |
| Column profiling | Distinct, null, min/max, mean (μ), stdev (σ), top-5 per column |
| Anomaly highlight | Negative numbers render in red across the grid |
| Theme | Light gray with DataDuck yellow accent (`#F4C430`) |

### AI (BYOK Groq)

| Feature | Notes |
|---|---|
| English → SQL | `llama-3.3-70b` translates a question to a runnable query |
| Explain dataset | Auto summary plus three suggested queries from schema + sample rows |
| Key handling | Stored in `localStorage`, never sent anywhere except `api.groq.com` |

### Persist

| Feature | Notes |
|---|---|
| Saved dashboards | Pin queries as tiles, **Refresh all**, persisted across reloads |
| Tab state | Editor tabs survive a refresh |
| Export | Copy results as **CSV / TSV / Markdown** |

---

## 🧱 Tech stack

| Layer | Pieces |
|---|---|
| **UI** | Avalonia 12 (XAML) + Fluent theme + Inter fonts + DataGrid |
| **VM** | CommunityToolkit.Mvvm 8.4 (source-generated `[ObservableProperty]` / `[RelayCommand]`) |
| **Service abstractions** | `IDuckDbService` · `IAiService` · `IFileService` · `ILocalStore` · `IProfilingService` |
| **DI container** | `Microsoft.Extensions.DependencyInjection` 10.0 |
| **Browser platform** | `net10.0-browser` TFM, `Microsoft.NET.Sdk.WebAssembly`, `[JSImport]` interop |
| **Data engine** | `@duckdb/duckdb-wasm` 1.32, `eh` (single-threaded) bundle |
| **AI** | Groq REST (`llama-3.3-70b-versatile`), key in `localStorage` |
| **Hosting** | Vercel static, auto-deploy on push to `main` via `build.sh` |

13 ViewModels, 4 service interfaces (with two platform-specific implementations of each that vary), and a single shared `MainView.axaml` rendered by both heads.

---

## 🏗 Architecture notes

The shared `DataDuck/` core defines models, view-models, views, and **interfaces**. It contains zero platform-specific code. Each head registers its own implementations into the DI container at startup:

| Service | Browser head | Desktop head |
|---|---|---|
| `IDuckDbService` | `DuckDbBrowserService` (DuckDB-WASM via `[JSImport]`) | `NotSupportedDuckDbService` (throws — desktop is for XAML hot-reload only) |
| `IAiService` | `GroqAiService` (key from `localStorage`) | `EnvVarGroqAiService` (key from env var, dev only) |
| `ILocalStore` | `BrowserLocalStore` (`globalThis.localStorage`) | `InMemoryLocalStore` (per-process dict) |
| `IFileService` | `StorageProviderFileService` (shared) | `StorageProviderFileService` (shared) |

Why two heads? The Desktop head is purely a development convenience: XAML hot-reload on Linux/Win/Mac iterates in milliseconds, while every change to a `net10.0-browser` build triggers a full WASM recompile. The Desktop head deliberately can't load DuckDB — its job is to render the UI fast while you tweak XAML.

The Browser head is the only thing that ships. It publishes to `publish/wwwroot/`, ~33 MB Brotli-compressed, served as a static site behind Vercel's CDN.

---

## 📁 Project structure

```
DataDuck/
├── build.sh                       # Vercel build script (.NET install + publish)
├── DataDuck.slnx                  # Solution file
├── Directory.Packages.props       # Central package version pinning
├── package.json                   # Pulls @duckdb/duckdb-wasm
├── vercel.json                    # COOP/COEP headers + SPA rewrites
├── scripts/
│   └── copy-duckdb.mjs            # Copies DuckDB-WASM assets into wwwroot/
├── DataDuck/                      # Shared core (no platform code)
│   ├── App.axaml(.cs)
│   ├── ViewLocator.cs
│   ├── Assets/
│   ├── Models/
│   │   ├── ChartTypes.cs
│   │   ├── ColumnProfile.cs
│   │   ├── DashboardTile.cs
│   │   ├── DatasetInsight.cs
│   │   ├── JoinSuggestion.cs
│   │   ├── LoadedFile.cs
│   │   ├── QueryResult.cs
│   │   └── SavedQuery.cs
│   ├── Services/
│   │   ├── IAiService.cs
│   │   ├── IDuckDbService.cs
│   │   ├── IFileService.cs
│   │   ├── ILocalStore.cs
│   │   ├── IProfilingService.cs
│   │   ├── JoinSuggester.cs
│   │   ├── ProfilingService.cs
│   │   ├── ResultExporter.cs
│   │   ├── StorageProviderFileService.cs
│   │   └── TopLevelLocator.cs
│   ├── ViewModels/                # 13 view-models
│   │   ├── ChartViewModel.cs
│   │   ├── DashboardViewModel.cs
│   │   ├── ExportCommandsViewModel.cs
│   │   ├── FileListViewModel.cs
│   │   ├── InsightsViewModel.cs
│   │   ├── JoinBuilderViewModel.cs
│   │   ├── MainViewModel.cs
│   │   ├── ProfileViewModel.cs
│   │   ├── QueryHistoryViewModel.cs
│   │   ├── ResultsViewModel.cs
│   │   ├── SettingsViewModel.cs
│   │   ├── SqlEditorTabsViewModel.cs
│   │   ├── SqlEditorViewModel.cs
│   │   └── ViewModelBase.cs
│   └── Views/
│       ├── ChartView.axaml
│       ├── DashboardView.axaml
│       ├── EditorTabsView.axaml
│       ├── MainView.axaml
│       └── MainWindow.axaml
├── DataDuck.Browser/              # WebAssembly head (production)
│   ├── DataDuck.Browser.csproj    # net10.0-browser, Microsoft.NET.Sdk.WebAssembly
│   ├── Program.cs                 # DI registration
│   ├── Services/
│   │   ├── BrowserLocalStore.cs   # globalThis.localStorage via [JSImport]
│   │   ├── DuckDbBrowserService.cs # DuckDB-WASM via [JSImport]
│   │   └── GroqAiService.cs        # Groq REST, key from localStorage
│   └── wwwroot/
│       ├── app.css
│       ├── duckdb-shim.js
│       ├── index.html
│       ├── main.js
│       └── samples/                # sample sales.csv
└── DataDuck.Desktop/              # Dev head (hot-reload only)
    ├── DataDuck.Desktop.csproj
    ├── Program.cs
    └── Services/
        ├── EnvVarGroqAiService.cs
        ├── InMemoryLocalStore.cs
        └── NotSupportedDuckDbService.cs
```

---

## 🛠 Run locally

### Prerequisites

- .NET 10 SDK
- `wasm-tools` workload: `dotnet workload install wasm-tools`
- Node.js 20+ (for the DuckDB-WASM asset copy step)

### Desktop head — fast XAML hot-reload

The Desktop head is what you use while iterating on XAML or view-models. It loads instantly and reflects changes in milliseconds. DuckDB and AI are stubbed — the goal is UI feedback, not data work.

```bash
git clone https://github.com/pradhankukiran/data-duck.git
cd data-duck
dotnet run --project DataDuck.Desktop
```

### Browser head — production-shaped

The Browser head boots the same UI under WebAssembly with the real DuckDB-WASM and Groq services wired in. Slower to rebuild (full WASM compile) but matches what ships to Vercel.

```bash
npm install                         # pulls @duckdb/duckdb-wasm
dotnet run --project DataDuck.Browser
```

Open the URL the dev server prints. Chrome and Firefox both work; the page sets COOP / COEP headers locally to enable shared-array-buffer for DuckDB.

---

## 🚀 Deploy to Vercel

### Auto-deploy (recommended)

Push to `main`. The repo's `vercel.json` points Vercel at `build.sh`, which:

1. Installs the .NET 10 SDK into `$HOME/.dotnet`.
2. Installs the `wasm-tools` workload.
3. Patches the bundled emscripten Node binary to use Vercel's system Node (the bundled one is dynamically linked against `libatomic.so.1`, which Vercel's build image lacks).
4. Runs `npm ci` to fetch DuckDB-WASM assets.
5. Runs `dotnet publish DataDuck.Browser -c Release -o publish`.
6. Vercel serves `publish/wwwroot/` as a static site with COOP / COEP headers.

```bash
git push origin main                # auto-deploy fires
```

### Manual CLI deploy

```bash
npm install
dotnet publish DataDuck.Browser -c Release -o publish
cd publish/wwwroot
vercel --prod
```

The first run prompts for scope and project name; later runs reuse `.vercel/project.json`. The production alias becomes `https://<project-name>.vercel.app/`.

---

## 💡 Why this exists

DataDuck is a portfolio piece for the .NET freelance market — specifically the MAUI / cross-platform-XAML niche. The patterns it demonstrates transfer 1:1 to mobile MAUI work:

- **Strict MVVM** with CommunityToolkit source generators (zero hand-rolled `INotifyPropertyChanged`)
- **DI-first service layer** with platform-specific swaps (`IDuckDbService` → browser vs. desktop)
- **JS interop** via `[JSImport]` / `[JSExport]` (the same surface used in MAUI Blazor Hybrid)
- **AI integration** with a key-in-storage BYOK pattern that maps directly to Secure Storage on mobile
- **Static deploy pipeline** built from a managed-runtime stack — no Node or Webpack bundling

If you're hiring for MAUI, Avalonia, or cross-platform .NET work, this is what shipped code from me looks like.

---

## 🗺 Roadmap

Open ideas, not commitments:

- Stacked / grouped bar and scatter charts
- Drill-through from chart segment back to filtered SQL
- Pivot / crosstab builder (UI-driven `PIVOT` generation)
- Mobile-responsive layout for tablet-sized screens
- Audit log of every query run, exportable as a notebook
- Persistent file workspace (IndexedDB-backed) so reloads don't drop loaded files
- Offline service-worker bundle for true zero-network use

---

## 📜 License

MIT. See `LICENSE`.
