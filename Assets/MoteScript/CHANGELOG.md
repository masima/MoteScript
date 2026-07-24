# Changelog

このプロジェクトの主な変更を記録します。

## [Unreleased]

### Added

- ダブルクォーテーション形式の文字列リテラル、エスケープ、null文字列
- Ordinal／InvariantCultureで動作する標準文字列ライブラリ
- `string.insert`、`string.remove`、`string.parseNumber`、`string.fromNumber`

### Changed

- 最初の代入で通常変数、配列要素、辞書メンバーの型を固定
- 数値演算子で文字列などの非数値型を使用した場合に明示的な例外を送出
- 数値と文字列の暗黙変換を禁止

## [0.1.2] - 2026-07-24

### Changed

- 正しい綴りの`Evaluate()`を公開APIとして追加し、従来の`Evalute()`を互換性を維持した非推奨APIへ変更
- 連続配列アクセサーを四則演算、比較、関数引数、条件式の中で評価できるよう修正
- 文末の`;`がない単文でも`new`が配列・辞書を正しくクローンするよう修正

## [0.1.1] - 2026-07-24

### Added

- `((1,2),(3,4))`形式による入れ子配列の生成
- `matrix[row][column]`および`cube[depth][row][column]`形式の連続アクセサー
- 2次元・3次元配列の生成、参照、代入、再評価テスト
- 入れ子配列の参照・代入に対する定常評価のGC割り当てテスト
- アクセサーで取得した入れ子配列に対する`add`、`insert`、`removeat`などの配列操作
- `new`による入れ子配列の再帰的なクローン

## [0.1.0] - 2026-07-22

### Added

- Unity Package Manager向けのGitサブフォルダパッケージ定義
- `float`、`int`、`decimal`のCalculator
- 式、配列、辞書、関数、条件分岐、ループの評価
- EditMode回帰テストと定常評価のGC割り当てテスト
