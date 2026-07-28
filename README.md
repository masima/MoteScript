# MoteScript

Unity向けの軽量スクリプトランタイムです。

## 特徴

- 数値・文字列、配列、辞書、関数ローカル変数、`ref`参照引数、条件分岐、ループを扱うコンパクトなスクリプト構文
- `float`、`int`、`decimal`から用途に合う数値型を選択可能
- デコード済みスクリプトとContextを再利用でき、繰り返し評価時のGC割り当てを抑制
- `yield`による中断・再開と、ステップ数、関数呼び出し深度、キャンセルによる実行制限
- コア実行系から分離された文字列、正規表現、数値演算の標準ライブラリと、C#関数の登録に対応

## Unity Package Manager

Package Managerの **Add package from git URL...** で、リポジトリURLにパッケージのサブフォルダを指定します。

```text
https://github.com/masima/MoteScript.git?path=/Assets/MoteScript
```

タグを指定して導入する場合:

```text
https://github.com/masima/MoteScript.git?path=/Assets/MoteScript#v0.8.0
```

詳しい使用方法は[パッケージREADME](Assets/MoteScript/README.md)を参照してください。
