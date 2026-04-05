# Virtual Host Setup

Storage Server supports three URL access patterns, handled by `VirtualHostStyleMiddleware`.
The base hostname is configurable via `S3:BaseHostname` in `appsettings.json` (default: `s3.localhost`).

## Access Patterns

| Pattern | Example URL |
|---|---|
| Path style on `localhost` | `http://localhost:5280/{bucket}/{key}` |
| Path style on `s3.localhost` | `http://s3.localhost:5280/{bucket}/{key}` |
| Virtual-hosted style | `http://{bucket}.s3.localhost:5280/{key}` |

## DNS Setup

Virtual-hosted style requires name resolution for `{bucket}.s3.localhost`. Add entries to your
hosts file for each bucket you want to access this way.

**Windows** — `C:\Windows\System32\drivers\etc\hosts`

```
127.0.0.1  s3.localhost
127.0.0.1  my-bucket.s3.localhost
```

**Linux / macOS** — `/etc/hosts`

```
127.0.0.1  s3.localhost
127.0.0.1  my-bucket.s3.localhost
```

> The hosts file does not support wildcards. Add one entry per bucket name, or use a local DNS
> resolver such as **dnsmasq** (Linux/macOS) or **Acrylic DNS Proxy** (Windows) with a wildcard
> rule for `*.s3.localhost → 127.0.0.1`.

## AWS SDK Configuration

```csharp
// Path style — works without any DNS changes
var config = new AmazonS3Config
{
    ServiceURL = "http://localhost:5280",
    ForcePathStyle = true
};

// Virtual-hosted style — requires DNS entries for each bucket
var config = new AmazonS3Config
{
    ServiceURL = "http://s3.localhost:5280",
    ForcePathStyle = false
};

using var client = new AmazonS3Client(
    new BasicAWSCredentials("any", "any"), config);
```

Authentication is not enforced. Any non-empty access key and secret are accepted.

## How the Middleware Works

`VirtualHostStyleMiddleware` rewrites the request `Path` before routing:

| Incoming host | Rewrite |
|---|---|
| `{bucket}.s3.localhost` | `/storage/{bucket}{path}` |
| `s3.localhost` | `/storage{path}` |
| `localhost` (non-app path) | `/storage{path}` |

Known app prefixes (`/storage`, `/api`, `/health`, `/_blazor`, etc.) are excluded from
rewriting so the Web UI and health-check endpoints continue to function normally.
