# Storage Server

ローカルで動作する S3 互換オブジェクトストレージサーバーです。  
ASP.NET Core (.NET 10) + Blazor Server で実装されており、S3 互換 REST API と Web UI を提供します。  
開発・テスト用途を想定しており、AWS SDK（`AWSSDK.S3`）をそのまま向き先変更して利用できます。

---

## 機能一覧

| カテゴリ | 機能 |
|---|---|
| バケット | 作成 / 削除 / 一覧 / 存在確認 / 統計 |
| オブジェクト | PUT / GET / HEAD / DELETE / 一括削除 / コピー |
| レンジリクエスト | `Range` ヘッダーによる部分ダウンロード |
| 条件付きリクエスト | `If-Match` / `If-None-Match` / `If-Modified-Since` / `If-Unmodified-Since` |
| バージョン管理 | 自動バージョニング / バージョン一覧 / バージョン取得 / 復元 / 削除 |
| マルチパートアップロード | 開始 / パート送信 / 完了 / 中断 / 一覧 |
| メタデータ | ユーザーメタデータ (`x-amz-meta-*`) の読み書き |
| タグ | オブジェクトタグ / バケットタグの CRUD |
| ACL | オブジェクト / バケットの ACL 取得・設定 |
| CORS | バケット単位の CORS ルール設定 |
| アクセス方式 | パス形式 / 仮想ホスト形式 (`{bucket}.s3.localhost`) |
| AWS Chunked | `aws-chunked` 転送エンコーディングのデコード |
| Web UI | Blazor Server によるバケット・ファイル管理画面 |

---

## S3 互換 API

### アクセスパターン

3 種類のアクセス方式をサポートします。

| 方式 | URL 例 |
|---|---|
| パス形式 (ForcePathStyle) | `http://localhost:5280/my-bucket/path/to/file.txt` |
| s3.localhost パス形式 | `http://s3.localhost:5280/my-bucket/path/to/file.txt` |
| 仮想ホスト形式 | `http://my-bucket.s3.localhost:5280/path/to/file.txt` |

仮想ホスト形式を使用する場合は、`hosts` ファイルに以下を追加してください。

```
127.0.0.1  s3.localhost
127.0.0.1  my-bucket.s3.localhost
```

AWS SDK から接続する例：

```csharp
// パス形式 (ForcePathStyle)
var config = new AmazonS3Config
{
    ServiceURL = "http://localhost:5280",
    ForcePathStyle = true,
};
using var client = new AmazonS3Client(
    new BasicAWSCredentials("any", "any"), config);

// 仮想ホスト形式
var config = new AmazonS3Config
{
    ServiceURL = "http://s3.localhost:5280",
    ForcePathStyle = false,
};
```

認証は不要です（任意の文字列を指定してください）。

---

### バケット操作

| メソッド | パス | クエリ | 説明 |
|---|---|---|---|
| `GET` | `/storage/` | | バケット一覧 |
| `PUT` | `/storage/{bucket}` | | バケット作成 |
| `DELETE` | `/storage/{bucket}` | | バケット削除 |
| `HEAD` | `/storage/{bucket}` | | バケット存在確認 |
| `GET` | `/storage/{bucket}` | `location` | バケットのリージョン取得 |
| `GET` | `/storage/{bucket}` | `versioning` | バージョニング設定取得 |

オブジェクト一覧（`GET /storage/{bucket}`）のクエリパラメータ：

| パラメータ | 説明 |
|---|---|
| `prefix` | キープレフィックスでフィルタ |
| `delimiter` | 区切り文字（`/` でフォルダ階層を擬似表現） |
| `max-keys` | 最大取得件数（既定: 1000） |
| `start-after` | このキーより後から取得 |
| `continuation-token` | ページネーショントークン |

---

### オブジェクト操作

| メソッド | パス | 説明 |
|---|---|---|
| `GET` | `/storage/{bucket}/{key}` | オブジェクト取得 |
| `PUT` | `/storage/{bucket}/{key}` | オブジェクト配置 |
| `HEAD` | `/storage/{bucket}/{key}` | メタデータ取得 |
| `DELETE` | `/storage/{bucket}/{key}` | オブジェクト削除 |
| `POST` | `/storage/{bucket}?delete` | オブジェクト一括削除 |

PUT でのメタデータ指定：

```
x-amz-meta-{key}: {value}   # ユーザー定義メタデータ
x-amz-storage-class: STANDARD
x-amz-acl: private
```

オブジェクトコピー（`PUT` に `x-amz-copy-source` ヘッダーを付与）：

```
x-amz-copy-source: /source-bucket/source-key
x-amz-metadata-directive: COPY | REPLACE
```

---

### マルチパートアップロード

| メソッド | パス | クエリ | 説明 |
|---|---|---|---|
| `POST` | `/storage/{bucket}/{key}` | `uploads` | アップロード開始 |
| `PUT` | `/storage/{bucket}/{key}` | `uploadId`, `partNumber` | パート送信 |
| `POST` | `/storage/{bucket}/{key}` | `uploadId` | 完了 |
| `DELETE` | `/storage/{bucket}/{key}` | `uploadId` | 中断 |
| `GET` | `/storage/{bucket}` | `uploads` | 進行中のアップロード一覧 |
| `GET` | `/storage/{bucket}/{key}` | `uploadId` | パート一覧 |

---

### タグ・ACL・CORS

| メソッド | パス | クエリ | 説明 |
|---|---|---|---|
| `GET/PUT/DELETE` | `/storage/{bucket}` | `tagging` | バケットタグ |
| `GET/PUT/DELETE` | `/storage/{bucket}/{key}` | `tagging` | オブジェクトタグ |
| `GET/PUT` | `/storage/{bucket}` | `acl` | バケット ACL |
| `GET/PUT` | `/storage/{bucket}/{key}` | `acl` | オブジェクト ACL |
| `GET/PUT/DELETE` | `/storage/{bucket}` | `cors` | バケット CORS |

---

## Web UI

Blazor Server 製の管理画面を提供します。

| URL | 画面 |
|---|---|
| `http://localhost:5280/` | ダッシュボード（バケット一覧・作成・削除・タグ管理） |
| `http://localhost:5280/browse/{bucket}` | ファイルブラウザ（フォルダ表示・アップロード・削除・メタデータ編集・バージョン管理） |

**ファイルブラウザの機能：**

- ドラッグ＆ドロップ / ファイル選択によるアップロード
- フォルダ作成・削除
- ファイルのプレビュー（画像・動画・音声・テキスト・PDF）
- メタデータ・タグの編集
- バージョン一覧・バージョンのダウンロード・復元・削除

---

## クライアントサンプル

`StorageServer.Client` は以下の操作を網羅したコンソールアプリです。

1. バケット作成
2. オブジェクトのアップロード（メタデータ付き）
3. オブジェクト一覧（フラット・区切り文字・ページネーション）
4. オブジェクトコピー（`COPY` / `REPLACE` ディレクティブ）
5. レンジリクエスト（部分ダウンロード）
6. 条件付きリクエスト（`If-None-Match` → 304）
7. マルチパートアップロード
8. オブジェクトタグ / バケットタグ
9. マルチパートアップロード一覧・パート一覧
10. ストレージクラス指定
11. ACL 取得・設定
12. バケット CORS 設定
13. 一括削除
14. 仮想ホスト形式アクセス

---

## ストレージ構造

データは `BasePath` 配下にファイルシステムで保存されます。

```
{BasePath}/
├── buckets/
│   └── {bucket}/
│       ├── data/               # オブジェクト本体
│       │   └── path/to/key
│       ├── meta/               # バケット・オブジェクトメタデータ
│       │   ├── _bucket.json
│       │   ├── _tags.json
│       │   ├── _acl.json
│       │   ├── _cors.json
│       │   └── objects/
│       │       └── path/to/key.meta.json
│       └── versions/           # バージョン履歴
│           └── path/to/key/
│               ├── _versions.json
│               ├── {versionId}.data
│               └── {versionId}.meta.json
└── multipart/
    └── {uploadId}/             # マルチパートアップロード中間データ
        ├── _info.json
        ├── _meta.json
        └── {partNumber}.part
```
