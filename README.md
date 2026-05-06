# DataDuck

**🌐 Live: https://dataduck-deploy.vercel.app**

A browser-based SQL data lab — drop a CSV / Parquet / JSONL file, run SQL on it instantly with **DuckDB-WASM**, optionally translate English questions to SQL using **Groq LLM**. Nothing uploads — your data stays in the browser.

Built with **Avalonia 12** (WebAssembly) and **CommunityToolkit.Mvvm**. Deploys as a static site to Vercel.

## Stack

- **UI**: Avalonia 12 + CommunityToolkit.Mvvm (MVVM with source generators)
- **SQL engine**: DuckDB-WASM, `eh` (single-threaded) bundle
- **AI**: Groq REST API, BYOK (English → SQL)
- **Persistence**: browser `localStorage` for query history and API key
- **Deploy**: Vercel static hosting

## Project layout

```
DataDuck/            shared core — ViewModels, Views, Services, Models
DataDuck.Desktop/    dev head (fast XAML hot-reload on Linux/Win/Mac)
DataDuck.Browser/    production WebAssembly target shipped to Vercel
```

## Run locally

**Desktop head** (fastest iteration):

```bash
dotnet run --project DataDuck.Desktop
```

**Browser head** (production-like, slower rebuild):

```bash
dotnet run --project DataDuck.Browser
```

The Browser head serves on a local port; open the URL printed in the terminal.

## Deploy to Vercel

```bash
dotnet publish DataDuck.Browser -c Release -o publish/
cd publish/wwwroot
vercel --prod
```

For a fresh project, the CLI will prompt to scope and name; the production alias becomes
`https://<project-name>.vercel.app/`.

## Demo script

1. Open the live URL in a private window (no cache).
2. Click **🎲 Try sample data** — wait ~5 s for DuckDB-WASM (~33 MB Brotli ≈ ~9 MB) to stream in.
3. Run the prefilled `SELECT region, COUNT(*), SUM(amount) FROM sales GROUP BY region` query.
4. Open **⚙ Settings**, paste a free Groq API key (from console.groq.com), close.
5. Type "top 5 customers by revenue" in the Ask-in-English bar, click **Generate SQL**, then Run.
6. Refresh the page — query history is restored from localStorage.
7. Drop your own CSV / Parquet anywhere on the window — instant analysis.

## License

MIT
