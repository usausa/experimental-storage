# Storage Server

S3-compatible local object storage server built with ASP.NET Core (.NET 10) and Blazor Server.
Provides an S3-compatible REST API and a web management UI.
Designed for development and testing — point your AWS SDK at this server instead of AWS S3.

## Web UI

Blazor Server management UI.

| Path | Screen |
|---|---|
| `/` | Dashboard — bucket list, create, delete, tags |
| `/browse/{bucket}` | File browser — upload, delete, preview, metadata, versioning |

![Web UI](Document/webui.jpg)

## S3 API Compatibility

**Legend:** ✅ Supported · ⚠️ Stub · ❌ Not supported

> **⚠️ Stub** — the request is accepted and returns a success response, but no data is stored or enforced.

### Service

| Operation | Status |
|---|---|
| ListBuckets | ✅ |

### Bucket

| Operation | Status | Notes |
|---|---|---|
| CreateBucket | ✅ | |
| DeleteBucket | ✅ | |
| HeadBucket | ✅ | |
| GetBucketLocation | ✅ | Always returns `us-east-1` |
| GetBucketVersioning | ✅ | Always returns `Enabled` |
| PutBucketVersioning | ⚠️ | Accepted; ignored |
| GetBucketTagging | ✅ | |
| PutBucketTagging | ✅ | |
| DeleteBucketTagging | ✅ | |
| GetBucketAcl | ✅ | |
| PutBucketAcl | ✅ | Canned ACL only |
| GetBucketCors | ✅ | |
| PutBucketCors | ✅ | |
| DeleteBucketCors | ✅ | |
| GetBucketPolicy | ⚠️ | Returns 204; no data stored |
| PutBucketPolicy | ⚠️ | Accepted; ignored |
| GetBucketLifecycle | ⚠️ | Returns 204; no data stored |
| PutBucketLifecycle | ⚠️ | Accepted; ignored |
| GetBucketLogging | ⚠️ | Returns 204; no data stored |
| PutBucketLogging | ⚠️ | Accepted; ignored |
| GetBucketNotification | ⚠️ | Returns 204; no data stored |
| PutBucketNotification | ⚠️ | Accepted; ignored |
| GetBucketEncryption | ⚠️ | Returns 204; no data stored |
| PutBucketEncryption | ⚠️ | Accepted; ignored |

### Object

| Operation | Status | Notes |
|---|---|---|
| ListObjectsV2 | ✅ | prefix, delimiter, pagination |
| PutObject | ✅ | user metadata, storage class, ACL header |
| GetObject | ✅ | Range, conditional headers |
| HeadObject | ✅ | |
| DeleteObject | ✅ | |
| DeleteObjects | ✅ | Bulk delete |
| CopyObject | ✅ | COPY / REPLACE metadata directive |
| GetObjectTagging | ✅ | |
| PutObjectTagging | ✅ | |
| DeleteObjectTagging | ✅ | |
| GetObjectAcl | ✅ | |
| PutObjectAcl | ✅ | Canned ACL only |

### Multipart Upload

| Operation | Status | Notes |
|---|---|---|
| CreateMultipartUpload | ✅ | |
| UploadPart | ✅ | |
| CompleteMultipartUpload | ✅ | |
| AbortMultipartUpload | ✅ | |
| ListMultipartUploads | ✅ | |
| ListParts | ✅ | |
| UploadPartCopy | ❌ | Not implemented |

### Authentication & Security

| Feature | Status | Notes |
|---|---|---|
| Signature V4 verification | ❌ | Dev tool; any credentials accepted |
| Presigned URLs | ❌ | No signature engine |
| Server-Side Encryption (SSE) | ❌ | Data stored as plaintext |
| Bucket Policy enforcement | ❌ | Policy stubbed; not enforced |
| Object Lock / WORM | ❌ | Not implemented |
| STS / IAM | ❌ | Not applicable |

### Advanced Features

| Feature | Status | Notes |
|---|---|---|
| S3 Select | ❌ | Not implemented |
| Transfer Acceleration | ❌ | Not applicable locally |
| Cross-Region Replication | ❌ | Single-node only |
| S3 Object Lambda | ❌ | Not applicable |
| S3 Batch Operations | ❌ | Not implemented |
| S3 Inventory | ❌ | Not implemented |

## Documentation

- [Storage Structure](Document/storage-structure.md)
- [Virtual Host Setup](Documents/virtual-host.md)
- [Client CLI Reference](Document/client.md)
