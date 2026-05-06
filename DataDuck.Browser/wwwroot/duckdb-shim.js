// Thin shim around DuckDB-WASM exposing 4 async functions that the C# side
// calls via [JSImport]. Kept deliberately small — see Phase 6 for the C#
// wrapper that consumes this surface.
//
//   initDuckDB(baseUrl?: string): Promise<void>
//   registerFile(name: string, bytes: Uint8Array): Promise<string>
//   query(sql: string): Promise<string>
//   close(): Promise<void>

import * as duckdb from "/duckdb/duckdb-browser.mjs";

let _db = null;       // duckdb.AsyncDuckDB
let _conn = null;     // duckdb.AsyncDuckDBConnection
let _worker = null;   // Worker
let _initPromise = null;

function buildBundles(baseUrl) {
    return {
        eh: {
            mainModule: `${baseUrl}/duckdb-eh.wasm`,
            mainWorker: `${baseUrl}/duckdb-browser-eh.worker.js`,
        },
    };
}

async function _doInit(baseUrl) {
    const bundles = buildBundles(baseUrl);
    const bundle = await duckdb.selectBundle(bundles);

    const worker = new Worker(bundle.mainWorker);
    const logger = new duckdb.ConsoleLogger();
    const db = new duckdb.AsyncDuckDB(logger, worker);
    await db.instantiate(bundle.mainModule, bundle.pthreadWorker);

    const conn = await db.connect();

    _db = db;
    _conn = conn;
    _worker = worker;

    console.log("DuckDB initialized");
}

export async function initDuckDB(baseUrl) {
    if (_db && _conn) return;
    if (_initPromise) return _initPromise;

    const url = (baseUrl && baseUrl.length > 0) ? baseUrl.replace(/\/+$/, "") : "/duckdb";
    _initPromise = _doInit(url).catch((err) => {
        // Reset so a later retry isn't stuck on a rejected promise.
        _initPromise = null;
        throw new Error(`DuckDB init failed: ${err && err.message ? err.message : err}`);
    });
    return _initPromise;
}

function _ensureReady() {
    if (!_db || !_conn) {
        throw new Error("DuckDB not initialized — call initDuckDB() first.");
    }
}

function _sanitizeTableName(fileName) {
    const lastSlash = Math.max(fileName.lastIndexOf("/"), fileName.lastIndexOf("\\"));
    const base = lastSlash >= 0 ? fileName.slice(lastSlash + 1) : fileName;
    const dot = base.lastIndexOf(".");
    const stem = dot > 0 ? base.slice(0, dot) : base;
    let cleaned = stem.replace(/[^a-zA-Z0-9_]/g, "_");
    if (cleaned.length === 0) cleaned = "table";
    if (/^[0-9]/.test(cleaned)) cleaned = `t_${cleaned}`;
    return cleaned;
}

function _viewSqlFor(fileName, registeredName, tableName) {
    const dot = fileName.lastIndexOf(".");
    const ext = dot >= 0 ? fileName.slice(dot + 1).toLowerCase() : "";
    const path = registeredName.replace(/'/g, "''");
    switch (ext) {
        case "csv":
        case "tsv":
        case "txt":
            return `CREATE OR REPLACE VIEW ${tableName} AS SELECT * FROM read_csv_auto('${path}')`;
        case "parquet":
            return `CREATE OR REPLACE VIEW ${tableName} AS SELECT * FROM read_parquet('${path}')`;
        case "json":
        case "jsonl":
        case "ndjson":
            return `CREATE OR REPLACE VIEW ${tableName} AS SELECT * FROM read_json_auto('${path}')`;
        default:
            throw new Error(`Unsupported file extension: .${ext} (file: ${fileName})`);
    }
}

function _arrowToPlainArray(table) {
    return table.toArray().map((row) =>
        Object.fromEntries(
            Object.entries(row).map(([k, v]) => [
                k,
                // JSON.stringify can't serialize BigInt; coerce to Number.
                typeof v === "bigint" ? Number(v) : v,
            ]),
        ),
    );
}

export async function registerFile(name, bytes) {
    _ensureReady();
    if (!name || typeof name !== "string") {
        throw new Error("registerFile: 'name' must be a non-empty string");
    }
    if (!(bytes instanceof Uint8Array)) {
        throw new Error("registerFile: 'bytes' must be a Uint8Array");
    }

    const tableName = _sanitizeTableName(name);

    try {
        await _db.registerFileBuffer(name, bytes);
    } catch (err) {
        throw new Error(`Failed to register file '${name}': ${err && err.message ? err.message : err}`);
    }

    const viewSql = _viewSqlFor(name, name, tableName);
    try {
        await _conn.query(viewSql);
    } catch (err) {
        throw new Error(`Failed to create view for '${name}': ${err && err.message ? err.message : err}`);
    }

    let rowCount = 0;
    try {
        const countResult = await _conn.query(`SELECT COUNT(*) AS n FROM ${tableName}`);
        const rows = _arrowToPlainArray(countResult);
        rowCount = rows.length > 0 ? Number(rows[0].n ?? 0) : 0;
    } catch (err) {
        throw new Error(`Failed to count rows in '${tableName}': ${err && err.message ? err.message : err}`);
    }

    let columns = [];
    try {
        const describeResult = await _conn.query(`DESCRIBE ${tableName}`);
        const rows = _arrowToPlainArray(describeResult);
        columns = rows.map((r) => ({
            name: String(r.column_name ?? r.name ?? ""),
            type: String(r.column_type ?? r.type ?? ""),
        }));
    } catch (err) {
        throw new Error(`Failed to describe '${tableName}': ${err && err.message ? err.message : err}`);
    }

    console.log(`Registered file: ${name} -> ${tableName} (${rowCount} rows, ${columns.length} cols)`);
    return JSON.stringify({ tableName, rowCount, columns });
}

export async function query(sql) {
    _ensureReady();
    if (typeof sql !== "string" || sql.length === 0) {
        throw new Error("query: 'sql' must be a non-empty string");
    }
    const t0 = performance.now();
    let table;
    try {
        table = await _conn.query(sql);
    } catch (err) {
        throw new Error(`Query failed: ${err && err.message ? err.message : err}`);
    }
    const rows = _arrowToPlainArray(table);
    const ms = (performance.now() - t0).toFixed(1);
    console.log(`Query took ${ms} ms (${rows.length} rows)`);
    return JSON.stringify(rows);
}

export async function close() {
    try {
        if (_conn) {
            await _conn.close();
        }
    } catch (err) {
        console.warn(`DuckDB connection close warning: ${err && err.message ? err.message : err}`);
    }
    try {
        if (_db) {
            await _db.terminate();
        }
    } catch (err) {
        console.warn(`DuckDB terminate warning: ${err && err.message ? err.message : err}`);
    }
    try {
        if (_worker) {
            _worker.terminate();
        }
    } catch (err) {
        // ignore
    }
    _conn = null;
    _db = null;
    _worker = null;
    _initPromise = null;
    console.log("DuckDB closed");
}
