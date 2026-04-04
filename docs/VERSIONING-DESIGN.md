# バージョニング実装設計

StorageServer におけるオブジェクトバージョニングの実装方針をまとめます。

## 概要

S3 のバージョニングでは、同一キーに対する上書き・削除操作が過去のバージョンを保持したまま行われます。各バージョンにはユニークな `VersionId` が付与され、特定バージョンの取得・削除・復元が可能です。

## 現在の実装状態

StorageServer では「常時バージョニング有効」に近い動作を採用しています。
明示的な `Enabled` / `Suspended` の状態管理は行わず、すべての PutObject / DeleteObject 操作で自動的にバージョン履歴を保持します。

### 実装済み

| 機能 | 説明 |
|---|---|
| 自動バージョン作成 | PutObject 時に旧バージョンを versions ディレクトリにアーカイブ |
| 削除マーカー | DeleteObject 時に旧バージョンをアーカイブし削除マーカーを記録 |
| バージョン一覧 | `ListVersionsAsync` で全バージョン履歴を取得 |
| バージョン指定取得 | `GetObjectVersionAsync` で過去バージョンのデータを取得 |
| バージョン復元 | `RestoreVersionAsync` で過去バージョンを最新として復元 |
| バージョン削除 | `DeleteVersionAsync` で特定バージョンを物理削除 |
| 最大バージョン数制限 | `MaxVersionsPerObject` 設定 |
| バージョン保持日数 | `VersionRetentionDays` 設定 |

### 未実装

| 機能 | 説明 |
|---|---|
| バケットバージョニング状態 | `GET/PUT /{bucket}?versioning` (Enabled/Suspended の明示的な切り替え) |
| ListObjectVersions API | `GET /{bucket}?versions` (S3 API レベルでの全バージョン一覧) |
| Suspended 状態 | バージョニング一時停止時の VersionId=null 動作 |
| CopyObject の versionId ソース指定 | `x-amz-copy-source` に `?versionId=` を付与した取得 |

## ストレージ設計

### ディレクトリ構造

```
storage-data/
├── buckets/
│   └── {bucket}/
│       ├── data/                               # オブジェクトデータ (最新バージョン)
│       │   ├── readme.txt
│       │   └── docs/
│       │       └── guide.txt
│       │
│       ├── meta/                               # メタデータ
│       │   ├── _bucket.json                    # バケット情報 (作成日時)
│       │   ├── _tags.json                      # バケットタグ
│       │   ├── _acl.json                       # バケット ACL
│       │   ├── _cors.json                      # CORS 設定
│       │   └── objects/                        # オブジェクト個別メタデータ
│       │       ├── readme.txt.meta.json
│       │       └── docs/
│       │           └── guide.txt.meta.json
│       │
│       └── versions/                           # バージョン履歴
│           ├── readme.txt/
│           │   ├── _versions.json              # バージョンインデックス
│           │   ├── v_20250101T120000Z_abc123.data
│           │   ├── v_20250101T120000Z_abc123.meta.json
│           │   ├── v_20250102T090000Z_def456.data
│           │   └── v_20250102T090000Z_def456.meta.json
│           └── docs/
│               └── guide.txt/
│                   ├── _versions.json
│                   └── ...
│
└── multipart/                                  # マルチパートアップロード (バケット横断)
    └── {uploadId}/
        ├── _info.json
        ├── _meta.json
        ├── 1.part
        └── 2.part
```

### 設計方針

**バケット内完結型**: バケットに関連するデータ (`data`)、メタデータ (`meta`)、バージョン (`versions`) をすべてバケットディレクトリ配下に格納します。
これにより：

- バケット削除が単一ディレクトリの再帰削除で完了する
- バケット単位でのバックアップ・移動が容易
- ファイルシステム上の構造が論理構造と一致する

**マルチパートはバケット横断**: マルチパートアップロードの一時データは `storage-data/multipart/` に配置します。
アップロード中はバケットが特定されているため、完了時に正しいバケット配下に最終ファイルを書き込みます。

### VersionId の生成

`v_{timestamp}_{random}` 形式を使用します。

```
v_20250115T103000Z_aBcDeF
```

- **タイムスタンプ**: UTC で `yyyyMMddTHHmmssZ` 形式
- **ランダム部**: 英数字 6 文字

タイムスタンプにより大まかな時系列ソートが可能です。衝突回避のためランダム部を付与しています。

## バージョンインデックス (`_versions.json`)

各オブジェクトのバージョン履歴を管理する JSON ファイルです。

```json
{
  "versions": [
    {
      "versionId": "v_20250115T103000Z_aBcDeF",
      "lastModified": "2025-01-15T10:30:00+00:00",
      "size": 1234,
      "etag": "\"d41d8cd98f00b204e9800998ecf8427e\"",
      "isDeleteMarker": false
    },
    {
      "versionId": "v_20250114T090000Z_xYzWvU",
      "lastModified": "2025-01-14T09:00:00+00:00",
      "size": 567,
      "etag": "\"098f6bcd4621d373cade4e832627b4f6\"",
      "isDeleteMarker": false
    }
  ]
}
```

## バージョン操作フロー

### PutObject

```
1. 既存ファイルが存在する場合:
   a. 現在のファイルを versions/{key}/{versionId}.data にコピー
   b. 現在のメタデータを versions/{key}/{versionId}.meta.json にコピー
   c. _versions.json にエントリを追加
   d. MaxVersionsPerObject / VersionRetentionDays に基づく古いバージョンの削除

2. 新しいデータを data/{key} に書き込み
3. 新しいメタデータを meta/objects/{key}.meta.json に保存
4. 新しい VersionId を生成しメタデータに記録
```

### DeleteObject

```
1. 既存ファイルが存在する場合:
   a. 現在のファイルを versions/{key}/{versionId}.data にコピー
   b. _versions.json に isDeleteMarker=true のエントリを追加

2. data/{key} を削除
3. meta/objects/{key}.meta.json を削除
4. 空になったディレクトリを再帰的に削除
```

### RestoreVersion

```
1. _versions.json から指定 versionId のエントリを検索
2. 削除マーカーでないことを確認
3. 現在のファイルが存在する場合、まずアーカイブ
4. versions/{key}/{versionId}.data を data/{key} にコピー
5. バージョンのメタデータがあれば復元し、新しい VersionId を付与
```

## 設定

`appsettings.json` の `Storage` セクションで制御します。

```json
{
  "Storage": {
    "BasePath": "./storage-data",
    "MaxVersionsPerObject": 0,
    "VersionRetentionDays": 0
  }
}
```

| 設定 | デフォルト | 説明 |
|---|---|---|
| `MaxVersionsPerObject` | `0` (無制限) | オブジェクトあたりの最大バージョン数。超過した古いバージョンは PutObject 時に自動削除 |
| `VersionRetentionDays` | `0` (無制限) | バージョンの保持日数。超過したバージョンは PutObject 時に自動削除 |

## 今後の拡張計画

### Phase 1: バケットバージョニング状態管理

- `GET/PUT /{bucket}?versioning` API の追加
- バケット単位での `Enabled` / `Suspended` / `Unversioned` 状態管理
- Suspended 時は VersionId=null で上書き（バージョン生成なし）、既存バージョンは保持

### Phase 2: ListObjectVersions API

- `GET /{bucket}?versions` エンドポイント
- `prefix`, `delimiter`, `key-marker`, `version-id-marker` による絞り込み
- バージョン情報を含む S3 標準 XML レスポンス

### Phase 3: CopyObject バージョン指定

- `x-amz-copy-source: /{bucket}/{key}?versionId={id}` 対応
- 特定バージョンをソースとしたコピー

## 影響範囲

| 既存機能 | 影響 |
|---|---|
| PutObject | バージョニング時にアーカイブロジックが実行される |
| DeleteObject | 削除マーカー生成 + アーカイブ |
| GetObject | 通常は最新バージョン。versionId 指定で過去バージョン取得 |
| CopyObject | ソース/デスト双方でバージョン処理 |
| ListObjects | 変更なし（最新バージョンのみ = data/ 配下のファイル） |
| DeleteBucket | バケットディレクトリごと再帰削除（バージョン含む） |
