# StorageServer – S3 互換機能一覧

AWS S3 REST API との互換性状況をまとめます。

> **本プロジェクトの位置づけ**: 実運用向けではなく、AWS SDK を使った開発時のローカルダミー S3 として利用することを目的としています。

## 実装済み機能

### バケット操作

| 操作 | メソッド / パス | 説明 |
|---|---|---|
| ListBuckets | `GET /` | 全バケットを一覧取得 |
| CreateBucket | `PUT /{bucket}` | バケットを作成 |
| HeadBucket | `HEAD /{bucket}` | バケットの存在確認 |
| DeleteBucket | `DELETE /{bucket}` | バケット・メタデータ・タグ・ACL・CORS を再帰削除 |
| GetBucketLocation | `GET /{bucket}?location` | バケットのリージョン情報を返却（固定値 `us-east-1`） |
| GetBucketTagging | `GET /{bucket}?tagging` | バケットのタグセットを取得 |
| PutBucketTagging | `PUT /{bucket}?tagging` | バケットにタグセットを設定 |
| DeleteBucketTagging | `DELETE /{bucket}?tagging` | バケットのタグセットを削除 |
| GetBucketAcl | `GET /{bucket}?acl` | バケットの ACL を取得 |
| PutBucketAcl | `PUT /{bucket}?acl` | バケットの ACL を設定（`x-amz-acl` ヘッダー対応） |
| GetBucketCors | `GET /{bucket}?cors` | バケットの CORS 設定を取得 |
| PutBucketCors | `PUT /{bucket}?cors` | バケットの CORS 設定を保存（ミドルウェアで実行時に適用） |
| DeleteBucketCors | `DELETE /{bucket}?cors` | バケットの CORS 設定を削除 |

### オブジェクト操作

| 操作 | メソッド / パス | 説明 |
|---|---|---|
| PutObject | `PUT /{bucket}/{key}` | オブジェクトをアップロード。Content-Type・StorageClass・ACL・`x-amz-meta-*` を保存 |
| GetObject | `GET /{bucket}/{key}` | オブジェクトをダウンロード。メタデータと `x-amz-storage-class` を返却 |
| HeadObject | `HEAD /{bucket}/{key}` | オブジェクトのメタデータを取得（StorageClass 含む） |
| DeleteObject | `DELETE /{bucket}/{key}` | オブジェクトとメタデータを削除（バージョン履歴にアーカイブ後） |
| CopyObject | `PUT /{bucket}/{key}` + `x-amz-copy-source` | サーバー側コピー。`COPY\|REPLACE` ディレクティブ対応（StorageClass も処理） |
| DeleteObjects | `POST /{bucket}?delete` | 複数オブジェクトの一括削除。`Quiet` モード対応 |
| GetObjectTagging | `GET /{bucket}/{key}?tagging` | オブジェクトのタグセットを取得 |
| PutObjectTagging | `PUT /{bucket}/{key}?tagging` | オブジェクトにタグセットを設定 |
| DeleteObjectTagging | `DELETE /{bucket}/{key}?tagging` | オブジェクトのタグセットを削除 |
| GetObjectAcl | `GET /{bucket}/{key}?acl` | オブジェクトの ACL を取得 |
| PutObjectAcl | `PUT /{bucket}/{key}?acl` | オブジェクトの ACL を設定 |

### ListObjectsV2

| 機能 | クエリパラメータ | 説明 |
|---|---|---|
| プレフィックス絞り込み | `prefix` | 指定プレフィックスに一致するキーのみ返却 |
| デリミタ（階層ブラウジング） | `delimiter` | `CommonPrefixes` によるディレクトリ風の一覧表示 |
| ページネーション | `max-keys`, `continuation-token` | `IsTruncated` / `NextContinuationToken` による結果分割 |
| 開始位置指定 | `start-after` | 辞書順で指定キーより後のオブジェクトのみ返却 |

### マルチパートアップロード

| 操作 | メソッド / パス | 説明 |
|---|---|---|
| CreateMultipartUpload | `POST /{bucket}/{key}?uploads` | 開始。Content-Type・StorageClass・`x-amz-meta-*` を一時保存 |
| UploadPart | `PUT /{bucket}/{key}?partNumber=N&uploadId=ID` | パートをアップロード |
| CompleteMultipartUpload | `POST /{bucket}/{key}?uploadId=ID` | パートを結合。メタデータを復元。合成 ETag を生成 |
| AbortMultipartUpload | `DELETE /{bucket}/{key}?uploadId=ID` | 中止しパートを破棄 |
| ListMultipartUploads | `GET /{bucket}?uploads` | 進行中のマルチパートアップロードを一覧 |
| ListParts | `GET /{bucket}/{key}?uploadId=ID` | パートを一覧 |

### 条件付きリクエスト / Range

| ヘッダー | レスポンス |
|---|---|
| `If-None-Match` | `304 Not Modified` |
| `If-Modified-Since` | `304 Not Modified` |
| `If-Match` | `412 Precondition Failed` |
| `If-Unmodified-Since` | `412 Precondition Failed` |
| `Range: bytes=start-end` | `206 Partial Content` |

### メタデータ・タグ・ACL

| 機能 | 説明 |
|---|---|
| Content-Type 保持 | PutObject / Multipart Upload で保存し GET/HEAD で返却 |
| Storage Class | `x-amz-storage-class` ヘッダーを保存し GET/HEAD/ListObjects で返却 |
| ユーザー定義メタデータ | `x-amz-meta-*` ヘッダーの保存と返却 |
| メタデータディレクティブ | CopyObject の `x-amz-metadata-directive: COPY\|REPLACE` |
| Object Tagging | オブジェクト単位のタグセット管理 |
| Bucket Tagging | バケット単位のタグセット管理 |
| Bucket/Object ACL | Canned ACL の保存と XML 応答生成（`private`, `public-read`, `public-read-write`, `authenticated-read`） |

### Bucket CORS

| 機能 | 説明 |
|---|---|
| CORS 設定の保存/取得/削除 | `GET/PUT/DELETE /{bucket}?cors` で S3 標準の CORS XML を管理 |
| ミドルウェアによる実行時適用 | `Origin` ヘッダーを検出し、保存された CORS ルールに基づいて `Access-Control-*` ヘッダーを自動付与 |
| OPTIONS プリフライト | `Access-Control-Request-Method` に基づくプリフライト応答 |

### バージョニング

| 機能 | 説明 |
|---|---|
| 自動バージョン作成 | PutObject / DeleteObject 時に旧バージョンを自動アーカイブ |
| バージョン一覧 | オブジェクトの全バージョン履歴を一覧表示 |
| バージョン指定取得 | versionId を指定して過去バージョンを取得 |
| バージョン復元 | 過去バージョンを最新として復元 |
| バージョン削除 | 特定バージョンの物理削除 |
| 削除マーカー | DeleteObject 時に削除マーカーを記録 |
| 最大バージョン数制限 | `MaxVersionsPerObject` 設定でバージョン数を制限 |
| バージョン保持日数 | `VersionRetentionDays` 設定で古いバージョンを自動削除 |

### アクセスパターン

| パターン | 説明 |
|---|---|
| パススタイル | `http://localhost:5280/{bucket}/{key}` — `ForcePathStyle=true` 設定時 |
| 仮想ホスト形式 | `http://{bucket}.s3.localhost:5280/{key}` — DNS 設定（hosts ファイル）が必要 |
| パススタイル (s3.localhost) | `http://s3.localhost:5280/{bucket}/{key}` |

### その他

| 機能 | 説明 |
|---|---|
| `x-amz-request-id` | 全レスポンスに一意のリクエストIDを付与（ミドルウェア） |
| ETag (MD5) | MD5 ハッシュを ETag として返却。マルチパートは合成 ETag (`-N` サフィックス) |
| S3 XML 名前空間準拠 | `http://s3.amazonaws.com/doc/2006-03-01/` |
| パストラバーサル防止 | バリデーションとパス正規化 |
| S3 エラーレスポンス | S3 標準の XML エラーフォーマット (ErrorCode, Message, Resource, RequestId) |
| Chunked Transfer-Encoding | `aws-chunked` エンコーディングのデコードに対応 (`S3ChunkedStream`) |

## 未実装機能

以下の S3 機能は現在未実装です。

| カテゴリ | 機能 | 必要性 |
|---|---|---|
| バケット | GetBucketVersioning / PutBucketVersioning (Enabled/Suspended の明示的な状態管理) | スタブ実装済 – `GET ?versioning` は常に `Enabled` を返却。`PUT ?versioning` は受け入れて無視 |
| バケット | Lifecycle configuration | スタブ実装済 – `GET/PUT ?lifecycle` は空レスポンス/OK を返却。実体の管理は不要 |
| バケット | Bucket policy | スタブ実装済 – `GET/PUT ?policy` は空レスポンス/OK を返却。実体の管理は不要 |
| バケット | Server access logging | スタブ実装済 – `GET/PUT ?logging` は空レスポンス/OK を返却。実体の管理は不要 |
| バケット | Notification configuration | スタブ実装済 – `GET/PUT ?notification` は空レスポンス/OK を返却。実体の管理は不要 |
| オブジェクト | S3 Select | 低 – 利用頻度が低く、実装コストが高い。必要になるまで未実装で可 |
| オブジェクト | Server-side encryption | スタブ実装済 – `GET/PUT ?encryption` は空レスポンス/OK を返却。ヘッダーを無視するだけで可 |
| オブジェクト | Object lock / retention | 不要 – ローカル環境ではコンプライアンス要件なし。無視して可 |
| 認証 | AWS Signature V4 検証 (リクエストを受け付けるが署名検証はスキップ) | 低 – 現状スキップで問題なし。テスト目的で検証したい場合のみ実装 |
| 認証 | IAM / Bucket policy による認可 | 不要 – ローカル開発では認可不要。無視して可 |

## Web UI 機能

S3 API とは別に、ブラウザベースの管理 UI を提供します。

| 機能 | 説明 |
|---|---|
| バケット一覧 | バケットの作成・削除。オブジェクト数・サイズの統計表示 |
| バケットタグ管理 | バケットのタグセットを Web UI から参照・編集 |
| ファイルブラウザ | 階層ナビゲーション、ソート、フォルダ作成 |
| サムネイル表示 | 画像ファイルの一覧表示時にサムネイルをインライン表示 |
| ドラッグ＆ドロップアップロード | ファイル・フォルダのドラッグ＆ドロップによる一括アップロード（バッチ送信） |
| 重複ファイル確認 | 同一ファイル名のアップロード時に確認ダイアログを表示し、バージョン管理として処理 |
| ファイルプレビュー | 画像・動画・音声・PDF・テキストファイルのインラインプレビュー |
| バージョン管理 | ファイルのバージョン一覧表示、バージョン指定ダウンロード、復元、削除 |
| メタデータダイアログ | オブジェクトのメタデータ・タグの参照と編集 |
| ファイルダウンロード | 直接ダウンロードリンク（バージョン指定ダウンロード対応） |
