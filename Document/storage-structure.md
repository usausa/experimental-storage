# Storage Structure

Storage Server persists all data under the directory specified by `Storage:BasePath` in `appsettings.json`.

## Configuration

```json
{
  "Storage": {
    "BasePath": "./storage-data",
    "MaxVersionsPerObject": 0,
    "VersionRetentionDays": 0
  }
}
```

| Setting | Description |
|---|---|
| `BasePath` | Root directory for all stored data |
| `MaxVersionsPerObject` | Maximum versions retained per object (`0` = unlimited) |
| `VersionRetentionDays` | Versions older than this are eligible for cleanup (`0` = disabled) |

## Directory Layout

```
{BasePath}/
├── buckets/
│   └── {bucket}/
│       ├── data/
│       │   └── path/to/key              # Object data (raw bytes)
│       ├── meta/
│       │   ├── _bucket.json             # Bucket info (name, region, created)
│       │   ├── _tags.json               # Bucket tags
│       │   ├── _acl.json                # Bucket ACL
│       │   ├── _cors.json               # CORS rules
│       │   └── objects/
│       │       └── path/to/key.meta.json  # Object metadata
│       └── versions/
│           └── path/to/key/
│               ├── _versions.json       # Version list
│               ├── {versionId}.data     # Version data snapshot
│               └── {versionId}.meta.json  # Version metadata snapshot
└── multipart/
    └── {uploadId}/
        ├── _info.json                   # Upload info (bucket, key, initiated)
        ├── _meta.json                   # Object metadata for the completed upload
        └── {partNumber}.part            # Individual part data
```

## Metadata Files

### `_bucket.json`

```json
{
  "name": "my-bucket",
  "region": "us-east-1",
  "createdAt": "2024-01-01T00:00:00Z"
}
```

### `{key}.meta.json`

```json
{
  "contentType": "application/json",
  "storageClass": "STANDARD",
  "acl": "private",
  "userMetadata": { "project": "demo" },
  "tags": { "env": "dev" },
  "etag": "d41d8cd98f00b204e9800998ecf8427e",
  "versionId": "v20240101-001"
}
```

## Versioning

Every PUT creates a version snapshot under `versions/`. Versioning is always active and cannot be disabled.

Versions are not accessed via the standard S3 `ListObjectVersions` API. Instead they are managed through the internal `/api/versions` endpoints, which the Web UI uses.
