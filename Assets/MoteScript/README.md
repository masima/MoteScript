# MoteScript

Unity上で簡単な式、変数、配列、辞書、関数、条件分岐、ループを評価するための軽量スクリプトランタイムです。`float`、`int`、`decimal`向けのCalculatorを同梱しています。

## Git URLからインストール

UnityのPackage Managerで **Add package from git URL...** を選択し、次の形式で入力します。

```text
https://github.com/masima/MoteScript.git?path=/Assets/MoteScript
```

リリースタグを固定する場合:

```text
https://github.com/masima/MoteScript.git?path=/Assets/MoteScript#v0.1.0
```

または、利用側プロジェクトの`Packages/manifest.json`へ追加します。

```json
{
  "dependencies": {
    "io.github.masima.motescript": "https://github.com/masima/MoteScript.git?path=/Assets/MoteScript#v0.1.0"
  }
}
```

## 使用例

```csharp
MoteDecoder<float>.Setup();

var decoder = new MoteDecoder<float>();
var context = new Context<float>()
    .Set("a", 1f)
    .Set("b", 2f);

float result = decoder.DecodeCached("a+b").Evalute(context).Value;
```

`decimal`を使用する場合は、`MoteDecoder<decimal>.Setup()`と`Context<decimal>`を使用します。

## 開発

このリポジトリ自体はUnityプロジェクトです。パッケージルートを`Assets/MoteScript`に置くことで、従来どおりプロジェクト内で開発・テストしながら、Git URLではサブフォルダパッケージとして利用できます。
