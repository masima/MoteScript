# MoteScript

Unity上で簡単な式、変数、配列、辞書、関数、条件分岐、ループを評価するための軽量スクリプトランタイムです。`float`、`int`、`decimal`向けのCalculatorを同梱しています。

## 目次

- [Git URLからインストール](#git-urlからインストール)
- [使用例](#使用例)
  - [デコーダーの再利用](#デコーダーの再利用)
  - [デコード済みの式を再評価](#デコード済みの式を再評価)
  - [関数定義を先に評価](#関数定義を先に評価)
  - [C#から関数を登録](#cから関数を登録)
- [構文サンプル](#構文サンプル)
  - [数値演算](#数値演算)
  - [比較・論理演算](#比較論理演算)
  - [変数](#変数)
  - [配列](#配列)
    - [多次元配列](#多次元配列)
  - [辞書](#辞書)
  - [関数](#関数)
  - [条件分岐](#条件分岐)
  - [ループ](#ループ)
  - [数値型の選択](#数値型の選択)
- [開発](#開発)

## Git URLからインストール

UnityのPackage Managerで **Add package from git URL...** を選択し、次の形式で入力します。

```text
https://github.com/masima/MoteScript.git?path=/Assets/MoteScript
```

リリースタグを固定する場合:

```text
https://github.com/masima/MoteScript.git?path=/Assets/MoteScript#v0.1.1
```

または、利用側プロジェクトの`Packages/manifest.json`へ追加します。

```json
{
  "dependencies": {
    "io.github.masima.motescript": "https://github.com/masima/MoteScript.git?path=/Assets/MoteScript#v0.1.1"
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

### デコーダーの再利用

`MoteDecoder`は、`Setup()`後に同じインスタンスを使い回して複数の式をデコードできます。

```csharp
MoteDecoder<float>.Setup();

var decoder = new MoteDecoder<float>();
var context = new Context<float>()
    .Set("a", 10f)
    .Set("b", 2f);

float sum = decoder.Decode("a+b").Evalute(context).Value;
float product = decoder.Decode("a*b").Evalute(context).Value;
```

この例では、同じ`decoder`から`sum`は`12`、`product`は`20`になります。

### デコード済みの式を再評価

式を一度デコードして保持すれば、以降は`Context`の値を変更して評価だけを繰り返せます。同じ式を頻繁に実行する場合は、毎回デコードする必要がありません。

```csharp
MoteDecoder<float>.Setup();

var decoder = new MoteDecoder<float>();
var expression = decoder.Decode("price*count");
var context = new Context<float>()
    .Set("price", 100f)
    .Set("count", 2f);

float first = expression.Evalute(context).Value;

context
    .Set("price", 120f)
    .Set("count", 3f);

float second = expression.Evalute(context).Value;
```

この例では、`first`は`200`、`second`は`360`です。`expression`は同じインスタンスを再利用し、2回目は値の評価だけを行います。

### 関数定義を先に評価

同じ`Context`を使い回すことで、初期化時に関数定義だけを評価しておき、その関数が定義済みであることを前提とした別のスクリプトを後からデコードして実行できます。

```csharp
MoteDecoder<float>.Setup();

var decoder = new MoteDecoder<float>();
var context = new Context<float>();

var definitions = decoder.Decode("double=(value)=>{value*2}");
definitions.Evalute(context);

var expression = decoder.Decode("double(3)+double(4)");
float result = expression.Evalute(context).Value;
```

この例では、最初のスクリプトを評価した時点で`double`関数が`context`に保持されます。そのため、後からデコードした別のスクリプトでも`double`を定義済みの関数として呼び出すことができ、`result`は`14`になります。C#側から引数の値を`Context`へ設定する必要はありません。

関数定義をデコードするだけでは`Context`へ登録されないため、定義スクリプトは一度`Evalute(context)`してください。また、関数の定義時と呼び出し時には同じ`Context`インスタンスを使用してください。

### C#から関数を登録

C#で実装した関数を`MoteValue<T>`に変換して`Context`へ登録すると、スクリプトから通常の関数と同じ構文で呼び出せます。

```csharp
using System.Collections.Generic;
using MoteScript;

static MoteValue<float> Sum(
    IContext<float> context,
    List<MoteValue<float>> parameters)
{
    float result = 0f;
    foreach (var parameter in parameters)
    {
        result += parameter.Evalute(context).Value;
    }
    return new MoteValue<float>(result);
}

MoteDecoder<float>.Setup();

var decoder = new MoteDecoder<float>();
var context = new Context<float>();
context["sum"] = new MoteValue<float>(Sum);

float result = decoder.Decode("sum(1,2,3)").Evalute(context).Value;
```

この例では、C#で登録した`sum`がスクリプトから呼び出され、`result`は`6`になります。`parameters`には引数の式が渡されるため、それぞれを`Evalute(context)`して値を取得します。登録後は同じ`Context`を使い回すことで、別途デコードしたスクリプトからも`sum`を呼び出せます。

## 構文サンプル

各サンプルは、最後に評価した式の値を結果として返します。複数の文は`;`で区切ります。

### 数値演算

`+`、`-`、`*`、`/`、`%`と丸括弧を使用できます。

```text
(1+2)*3
```

結果は`9`です。

### 比較・論理演算

比較には`<`、`>`、`<=`、`>=`、`==`、`!=`、論理演算には`&&`と`||`を使用できます。真は`1`、偽は`0`として扱われます。

```text
score>=80 && bonus==1
```

### 変数

代入した変数は、同じ`Context`内の後続の式から参照できます。

```text
price=120;count=3;price*count
```

結果は`360`です。C#から初期値を渡す場合は、次のように設定します。

```csharp
var context = new Context<float>()
    .Set("price", 120f)
    .Set("count", 3f);

float result = decoder.DecodeCached("price*count").Evalute(context).Value;
```

### 配列

`(...)`で配列を作成し、`[index]`で要素を参照または更新します。

```text
values=(10,20,30);values[1]=25;values[0]+values[1]
```

結果は`35`です。配列では次の操作も使用できます。

```text
values=();values.add(10);values.add(20,30);values.insert(1,15);values.removeat(0);values.pop()
```

- `add(...)`: 末尾へ要素を追加
- `insert(index,value)`: 指定位置へ要素を挿入
- `removeat(index)`: 指定位置の要素を削除
- `pop()`: 末尾の要素を削除して返す
- `clear()`: 全要素を削除

#### 多次元配列

配列を入れ子にすることで、2次元以上の配列を作成できます。`[index]`を続けて記述し、各階層の要素を参照または更新します。

```text
matrix=((1,2),(3,4));matrix[1][0]
```

結果は`3`です。要素への代入も同じ形式です。

```text
matrix=((1,2),(3,4));matrix[1][0]=9;matrix[1][0]
```

3次元配列では、次のようにアクセサーを3つ続けます。

```text
cube=(((1,2),(3,4)),((5,6),(7,8)));cube[1][0][1]
```

結果は`6`です。各階層は独立した配列なので、行ごとに異なる要素数を持つジャグ配列として扱えます。アクセサーで取得した配列には、通常の配列と同じ操作を使用できます。

```text
matrix=((1,2),(3,4));matrix[0].add(5);matrix[0]
```

`new`を使用すると、内側の配列を含めて再帰的にクローンできます。コピー先の要素を変更しても、コピー元には影響しません。

```text
matrix=((1,2),(3,4));copy=new matrix;copy[0][0]=9;matrix[0][0]
```

この例の結果は`1`です。`new`は新しい配列を生成するため、評価時にメモリ割り当てが発生します。

### 辞書

`[key:value,...]`で辞書を作成し、`.`で値へアクセスします。空の辞書は`[]`です。

```text
player=[hp:100,attack:20];player.hp=player.hp-15;player.hp
```

結果は`85`です。代入時に階層を作成することもできます。

```text
player.status.level=3;player.status.level
```

### 関数

`(引数)=>{処理}`で関数を定義します。

```text
add=(x,y)=>{x+y};add(2,3)
```

結果は`5`です。関数から明示的に値を返す場合は`return`を使用します。

```text
min=(x,y)=>{if(x<=y){return x};return y};min(4,2)
```

再帰呼び出しにも対応しています。

```text
factorial=(n)=>{if(n<=1){return 1};return factorial(n-1)*n};factorial(5)
```

結果は`120`です。

### 条件分岐

`if`、`else if`、`else`を使用できます。

```text
score=75;
rank=0;
if(score>=80){rank=2}
else if(score>=60){rank=1}
else{rank=-1};
rank
```

結果は`1`です。

### ループ

`while`で条件が真の間、処理を繰り返します。

```text
i=0;sum=0;while(i<5){sum=sum+i;i=i+1};sum
```

結果は`10`です。

`break`でループを終了し、`continue`で次の反復へ進めます。

```text
i=0;sum=0;
while(1){
    i=i+1;
    if(i==3){continue}
    if(i>5){break}
    sum=sum+i
};
sum
```

結果は`12`です。

### 数値型の選択

用途に応じて`float`、`int`、`decimal`のCalculatorを選択できます。型ごとに最初の評価前に`Setup()`を呼び出します。

```csharp
MoteDecoder<float>.Setup();
var floatDecoder = new MoteDecoder<float>();

MoteDecoder<int>.Setup();
var intDecoder = new MoteDecoder<int>();

MoteDecoder<decimal>.Setup();
var decimalDecoder = new MoteDecoder<decimal>();
decimal result = decimalDecoder.Decode("0.1+0.2").Evalute(new Context<decimal>()).Value;
```

MoteScriptの値は数値、配列、辞書、関数を対象としており、文字列値は扱いません。

## 開発

このリポジトリ自体はUnityプロジェクトです。パッケージルートを`Assets/MoteScript`に置くことで、従来どおりプロジェクト内で開発・テストしながら、Git URLではサブフォルダパッケージとして利用できます。
