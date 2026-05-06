#!/usr/bin/env node
// Copies the DuckDB-WASM "eh" (single-threaded, exception-handling) bundle
// from node_modules into DataDuck.Browser/wwwroot/duckdb so the browser app
// can serve them as static assets. Idempotent — overwrites on re-run.

import { copyFile, mkdir, stat } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const repoRoot = resolve(__dirname, "..");

const srcDir = join(repoRoot, "node_modules", "@duckdb", "duckdb-wasm", "dist");
const destDir = join(repoRoot, "DataDuck.Browser", "wwwroot", "duckdb");

// Required files for the eh single-threaded bundle.
const required = [
    "duckdb-eh.wasm",
    "duckdb-browser-eh.worker.js",
    "duckdb-browser.mjs",
];

// Optional: type declarations for the main module entry. Harmless if absent.
const optional = [
    "duckdb-browser.d.ts",
];

async function copyOne(name, { optional: isOptional = false } = {}) {
    const src = join(srcDir, name);
    const dest = join(destDir, name);
    let info;
    try {
        info = await stat(src);
    } catch (err) {
        if (isOptional && err.code === "ENOENT") {
            return { name, skipped: true, bytes: 0 };
        }
        throw new Error(`Source not found: ${src} (${err.message})`);
    }
    await copyFile(src, dest);
    return { name, skipped: false, bytes: info.size };
}

async function main() {
    await mkdir(destDir, { recursive: true });

    const results = [];
    for (const name of required) {
        results.push(await copyOne(name));
    }
    for (const name of optional) {
        results.push(await copyOne(name, { optional: true }));
    }

    let total = 0;
    console.log(`copy-duckdb: ${srcDir}`);
    console.log(`         -> ${destDir}`);
    for (const r of results) {
        if (r.skipped) {
            console.log(`  skipped (not present): ${r.name}`);
            continue;
        }
        total += r.bytes;
        const kb = (r.bytes / 1024).toFixed(1).padStart(9);
        console.log(`  copied ${kb} KB  ${r.name}`);
    }
    const totalMb = (total / (1024 * 1024)).toFixed(2);
    console.log(`copy-duckdb: total ${total} bytes (${totalMb} MB)`);
}

try {
    await main();
} catch (err) {
    console.error(`copy-duckdb failed: ${err.message}`);
    process.exit(1);
}
