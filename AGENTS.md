# AGENTS.md

## 原則

- ユーザーには日本語で、明瞭かつ具体的に応答する。
- 成果物には、目的、読者、利用場面に必要な記述だけを残す。

## 構成と責務

- `src/` は配布する製品コード、`tests/` は公開 API から外部動作を確認するテストである。
- ライブラリ設計とリリース手順の正本は、`mackysoft/engineering-principles` の `moira/` に置く。`design.md` は公開 API、数値境界、状態、失敗時の保証、ホストとの所有境界を定める。
- `scripts/` はローカルと継続的インテグレーションが共通して使う検証入口である。
- 製品は他 project、外部 package、Unity API へ依存しない。Moira は入力 sample と entry の検証、固定分布、数量付き箱の在庫、残数の復元検証を所有する。

## 作業規則

- KISS と YAGNI を適用し、型とファイルは責務単位で配置する。変更時は依存するコードへの影響も同じ作業で確認する。
- 設計を小さな差分や過去の都合に合わせて歪めない。ユーザーが明示しない限り、互換維持だけを目的にコードや説明を追加しない。
- テストは公開 API から外部動作を確認する。private 型、内部配列、アルゴリズム途中の状態、ファイル配置を固定しない。

## 検証

.NET 10 SDK、dotmet の実行に必要な .NET 8 runtime、`jq`、`unzip` を使用する。
dotmet の版は `dotnet-tools.json` で固定し、標準検証がローカルツールを復元する。

変更完了前に次を順に実行する。format 後は対象の動作テストも実行する。

```bash
bash scripts/code-quality.sh format
bash scripts/code-quality.sh verify
bash scripts/verify.sh
```

`scripts/verify.sh` は、整形、ビルド、公開 API の動作テスト、dotmet、生成パッケージを検証する。
dotmet は `MackySoft.Moira.slnx` 全体へ `.dotmet/rules.json` を適用し、解析と rules の適用範囲が完全で、判定が `pass` の場合に成功する。
レポートは Git 管理対象外の `.dotmet/local/analysis.json` に出力する。

dotmet だけを実行する場合は、ツールと依存関係を復元してから解析する。

```bash
dotnet tool restore
dotnet restore MackySoft.Moira.slnx
bash scripts/analyze-dotnet.sh
```

## GitHub 操作と公開

- GitHub の接続、閲覧、更新には `gh` を使う。
- 通常変更では release tag を作成または push せず、NuGet 公開を実行しない。
