# Changelog

このプロジェクトの主な変更を記録します。

## [Unreleased]

### Changed

- `IExecutionContext<T>.CurrentExecution`を読み取り専用にし、実行所有権の取得と解放を`TryAcquireExecution()`／`ReleaseExecution()`へ集約
- `Resume()`時に実行Contextの所有権を検証
- ブロック、while、代入、関数呼び出し、returnのYield継続情報を構造体バッファへ格納し、再開時のオブジェクト生成を削減
- ビットフラグだった`FlowControlFlag`を排他的な`FlowControlType`へ変更し、continue、break、returnの判定を明確化
- 内部評価APIの`EvaluteInner`を`EvaluateInner`、RPN構築APIの`Finailze`を`FinalizeOperator`へ修正
- `UnrayOperator`系の型名を正しい綴りの`UnaryOperator`へ変更
- `MoteExecution<T>`の開始、再開、中断、完了、例外終了に伴う状態更新を専用メソッドへ集約
- `MoteDecoder<T>`からデコードキャッシュ、字句読み取り、構造化式の分割判定を専用クラスへ分離

## [0.4.0] - 2026-07-26

### Added

- Yield実行中の`MoteExecution<T>`を保持する`IExecutionContext<T>`
- 実行Contextを分離しながら共通Context経由で値を共有する運用例

### Changed

- `MoteValue<T>.CreateExecution(context)`が受け取るContextを`IExecutionContext<T>`実装に限定
- 内部のContextラッパーを廃止し、実行中の`MoteExecution<T>`をContext自身が保持する方式へ変更
- 同じContextを使用する複数の`MoteExecution<T>`の同時実行を禁止
- 実行完了時または例外終了時にContextの`CurrentExecution`を自動解除

## [0.3.0] - 2026-07-26

### Added

- `yield(value)`によるスクリプト全体の中断
- `MoteValue<T>.CreateExecution(context)`で作成する`MoteExecution<T>`と、`Resume()`による実行再開
- `Ready`、`Running`、`Yielded`、`Completed`、`Faulted`による実行状態の取得
- ループ、分岐、関数、再帰呼び出しをまたいだ中断状態の保持
- 同一実行内の複数回のYieldで実行オブジェクトを再利用する仕組み

### Changed

- `IOperator<T>.Evalute()`を正しい綴りの`Evaluate()`へ変更

## [0.2.1] - 2026-07-25

### Added

- `StandardMathLibrary<T>.Register(context)`で登録できる標準数値関数ライブラリ
- 絶対値、最小値・最大値、クランプ、符号、累乗を扱う基本数値関数
- 平方根、指数・対数、三角・逆三角関数
- floor、ceiling、round、truncateによる丸め関数
- `math.pi()`によるπの取得
- `CalculatorInteger`向けの整数演算関数。小数結果を必要とする関数は`NotImplementedException`を送出

## [0.2.0] - 2026-07-25

### Added

- ダブルクォーテーション形式の文字列リテラル、エスケープ、null文字列
- Ordinal／InvariantCultureで動作する標準文字列ライブラリ
- `string.insert`、`string.remove`、`string.parseNumber`、`string.fromNumber`
- C#の複合書式指定をInvariantCultureで処理する`string.format`
- タイムアウトと上限付きLRUキャッシュを備えた`regex.isMatch`、`regex.match`、`regex.matches`、`regex.replace`
- 解析位置、行、列、該当行を取得できる`MoteScriptParseException`

### Changed

- 最初の代入で通常変数、配列要素、辞書メンバーの型を固定
- 数値演算子で文字列などの非数値型を使用した場合に明示的な例外を送出
- 数値と文字列の暗黙変換を禁止
- 未定義変数の例外にContext内で利用可能な変数名を追加
- `&&`と`||`の短絡評価動作をテストとREADMEで明文化

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
