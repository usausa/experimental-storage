# Client CLI Reference

`StorageServer.Client` is a .NET console application that demonstrates S3-compatible API usage
via the AWS SDK (`AWSSDK.S3`).

## Prerequisites

Start the server before running the client:

```bash
dotnet run --project StorageServer
```

The client connects to `http://localhost:5280` with `ForcePathStyle = true` by default.

## Usage

```
StorageServer.Client <command> [subcommand] [options]
```

---

## `test` — End-to-end test suite

Runs all S3 operation scenarios sequentially and reports results.

```
StorageServer.Client test [--bucket <name>]
```

| Option | Default | Description |
|---|---|---|
| `--bucket` / `-b` | `test-bucket` | Bucket name used during the test run |

**Scenarios covered:**

| # | Scenario |
|---|---|
| 1 | Create bucket |
| 2 | Upload objects with hierarchical keys and user metadata |
| 3 | List all objects (flat) |
| 4 | List with delimiter (directory-style browse) |
| 5 | Pagination (`MaxKeys`) |
| 6 | Copy object |
| 7 | Content-Type and user metadata (COPY / REPLACE directive) |
| 8 | Range request (partial download) |
| 9 | Conditional request (`If-None-Match` → 304) |
| 10 | Multipart upload (3 parts) |
| 11 | Object tagging |
| 12 | Bucket tagging |
| 13 | ListMultipartUploads / ListParts / AbortMultipartUpload |
| 14 | Storage class (`STANDARD_IA`) |
| 15 | ACL (bucket and object) |
| 16 | Bucket CORS |
| 17 | Delete object (verify 404) |
| 18 | Bulk delete (`DeleteObjects`) |
| 19 | Delete bucket |
| 20 | Virtual-hosted style access (skipped if DNS not configured) |

---

## `bucket` — Bucket operations

### `bucket create`

```
StorageServer.Client bucket create --name <name>
```

| Option | Description |
|---|---|
| `--name` / `-n` | Bucket name (required) |

### `bucket delete`

```
StorageServer.Client bucket delete --name <name>
```

| Option | Description |
|---|---|
| `--name` / `-n` | Bucket name (required) |

---

## `object` — Object operations

### `object put`

```
StorageServer.Client object put --bucket <b> --key <k> --file <path> [--content-type <type>]
```

| Option | Description |
|---|---|
| `--bucket` / `-b` | Bucket name (required) |
| `--key` / `-k` | Object key (required) |
| `--file` / `-f` | Local file to upload (required) |
| `--content-type` / `-t` | Content-Type (auto-detected from extension if omitted) |

### `object get`

```
StorageServer.Client object get --bucket <b> --key <k> [--output <path>]
```

| Option | Description |
|---|---|
| `--bucket` / `-b` | Bucket name (required) |
| `--key` / `-k` | Object key (required) |
| `--output` / `-o` | Output file path (defaults to filename part of key) |

### `object delete`

```
StorageServer.Client object delete --bucket <b> --key <k>
```

| Option | Description |
|---|---|
| `--bucket` / `-b` | Bucket name (required) |
| `--key` / `-k` | Object key (required) |

### `object list`

```
StorageServer.Client object list --bucket <b> [--prefix <p>] [--delimiter <d>] [--max-keys <n>]
```

| Option | Default | Description |
|---|---|---|
| `--bucket` / `-b` | — | Bucket name (required) |
| `--prefix` / `-p` | — | Key prefix filter |
| `--delimiter` / `-d` | — | Hierarchy delimiter (e.g. `/`) |
| `--max-keys` / `-m` | `1000` | Maximum keys to return |

### `object copy`

```
StorageServer.Client object copy --source-bucket <sb> --source-key <sk> --bucket <b> --key <k>
```

| Option | Description |
|---|---|
| `--source-bucket` / `-sb` | Source bucket (required) |
| `--source-key` / `-sk` | Source key (required) |
| `--bucket` / `-b` | Destination bucket (required) |
| `--key` / `-k` | Destination key (required) |

### `object head`

```
StorageServer.Client object head --bucket <b> --key <k>
```

| Option | Description |
|---|---|
| `--bucket` / `-b` | Bucket name (required) |
| `--key` / `-k` | Object key (required) |

---

## `tag` — Tagging operations

`--tags` accepts comma-separated `key=value` pairs, e.g. `env=dev,team=backend`.

### `tag get-object`

```
StorageServer.Client tag get-object --bucket <b> --key <k>
```

### `tag put-object`

```
StorageServer.Client tag put-object --bucket <b> --key <k> --tags <key=value,...>
```

### `tag delete-object`

```
StorageServer.Client tag delete-object --bucket <b> --key <k>
```

### `tag get-bucket`

```
StorageServer.Client tag get-bucket --bucket <b>
```

### `tag put-bucket`

```
StorageServer.Client tag put-bucket --bucket <b> --tags <key=value,...>
```

### `tag delete-bucket`

```
StorageServer.Client tag delete-bucket --bucket <b>
```
