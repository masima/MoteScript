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
  - [文字列](#文字列)
  - [正規表現](#正規表現)
  - [数値関数](#数値関数)
  - [配列](#配列)
    - [多次元配列](#多次元配列)
  - [辞書](#辞書)
  - [関数](#関数)
  - [条件分岐](#条件分岐)
  - [ループ](#ループ)
  - [処理の中断と再開](#処理の中断と再開)
    - [複数の実行単位で値を共有](#複数の実行単位で値を共有)
  - [数値型の選択](#数値型の選択)
- [開発](#開発)

## Git URLからインストール

UnityのPackage Managerで **Add package from git URL...** を選択し、次の形式で入力します。

```text
https://github.com/masima/MoteScript.git?path=/Assets/MoteScript
```

リリースタグを固定する場合:

```text
https://github.com/masima/MoteScript.git?path=/Assets/MoteScript#v0.5.0
```

または、利用側プロジェクトの`Packages/manifest.json`へ追加します。

```json
{
  "dependencies": {
    "io.github.masima.motescript": "https://github.com/masima/MoteScript.git?path=/Assets/MoteScript#v0.5.0"
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

float result = decoder.DecodeCached("a+b").Evaluate(context).Value;
```

従来の`Evalute()`は互換性のため残されていますが非推奨です。新しいコードでは`Evaluate()`を使用してください。

`decimal`を使用する場合は、`MoteDecoder<decimal>.Setup()`と`Context<decimal>`を使用します。

### デコーダーの再利用

`MoteDecoder`は、`Setup()`後に同じインスタンスを使い回して複数の式をデコードできます。

```csharp
MoteDecoder<float>.Setup();

var decoder = new MoteDecoder<float>();
var context = new Context<float>()
    .Set("a", 10f)
    .Set("b", 2f);

float sum = decoder.Decode("a+b").Evaluate(context).Value;
float product = decoder.Decode("a*b").Evaluate(context).Value;
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

float first = expression.Evaluate(context).Value;

context
    .Set("price", 120f)
    .Set("count", 3f);

float second = expression.Evaluate(context).Value;
```

この例では、`first`は`200`、`second`は`360`です。`expression`は同じインスタンスを再利用し、2回目は値の評価だけを行います。

### 関数定義を先に評価

同じ`Context`を使い回すことで、初期化時に関数定義だけを評価しておき、その関数が定義済みであることを前提とした別のスクリプトを後からデコードして実行できます。

```csharp
MoteDecoder<float>.Setup();

var decoder = new MoteDecoder<float>();
var context = new Context<float>();

var definitions = decoder.Decode("double=(value)=>{value*2}");
definitions.Evaluate(context);

var expression = decoder.Decode("double(3)+double(4)");
float result = expression.Evaluate(context).Value;
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
        result += parameter.Evaluate(context).Value;
    }
    return new MoteValue<float>(result);
}

MoteDecoder<float>.Setup();

var decoder = new MoteDecoder<float>();
var context = new Context<float>();
context["sum"] = new MoteValue<float>(Sum);

float result = decoder.Decode("sum(1,2,3)").Evaluate(context).Value;
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

`&&`と`||`は短絡評価されます。`&&`は左辺が偽の場合、`||`は左辺が真の場合に右辺を評価しません。そのため、右辺に関数呼び出しや代入が含まれていても、短絡した場合はその副作用は発生しません。

```text
callCount=0;0 && (callCount=callCount+1);callCount
```

この例では`&&`の左辺が偽なので右辺の代入は実行されず、結果は`0`です。

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

float result = decoder.DecodeCached("price*count").Evaluate(context).Value;
```

変数の型は最初の代入で確定します。同じ型の値は再代入できますが、数値、文字列、配列、辞書、関数など異なる型の値で上書きすると`InvalidOperationException`が発生します。この規則は通常変数、既存の配列要素、既存の辞書メンバーに適用されます。

### 文字列

文字列リテラルはダブルクォーテーションで囲みます。`\"`、`\\`、`\n`、`\r`、`\t`のエスケープを使用できます。`null`はnull文字列、`""`は空文字列であり、両者は区別されます。

```text
message="Hello\nMoteScript"
```

文字列は`+`、`==`、`<`などの数値演算子では操作できません。また、数値と文字列の暗黙変換は行われません。文字列操作を使用する前に、標準文字列ライブラリをContextへ登録します。

```csharp
var context = new Context<float>();
StandardStringLibrary<float>.Register(context);

string result = decoder.Decode(
    "message=string.concat(\"Hello \",\"MoteScript\");message")
    .Evaluate(context)
    .StringValue;
```

登録される関数は次のとおりです。比較はOrdinal、大文字・小文字変換と数値変換はInvariantCultureで行われます。

- `string.concat(values...)`
- `string.format(format,args...)`
- `string.length(value)`
- `string.substring(value,start[,length])`
- `string.replace(value,oldValue,newValue)`
- `string.trim(value)`
- `string.toLower(value)`、`string.toUpper(value)`
- `string.indexOf(value,search)`
- `string.compare(a,b)`、`string.equals(a,b)`
- `string.contains(value,search)`
- `string.startsWith(value,prefix)`、`string.endsWith(value,suffix)`
- `string.isEmpty(value)`、`string.isNull(value)`
- `string.insert(value,index,insertedValue)`
- `string.remove(value,start[,length])`
- `string.parseNumber(value)`
- `string.fromNumber(value)`

`parseNumber`と`fromNumber`だけが数値と文字列を明示的に変換します。文字列は不変オブジェクトであるため、文字列を返す操作では原則としてGC割り当てが発生します。

`format`はC#の複合書式指定をInvariantCultureで処理します。プレースホルダー、配置、数値書式、波括弧のエスケープをそのまま使用できます。

```text
string.format("{0}: {1:0.00}","score",12.5)
```

この例の結果は`"score: 12.50"`です。書式引数には数値、文字列、nullを指定できます。

`equals`、`compare`、`isNull`はnull文字列を受け取れます。`concat`ではnullを空文字列として連結します。それ以外の文字列操作へnullを渡すと`InvalidOperationException`が発生します。

### 正規表現

正規表現を使用する前に、標準正規表現ライブラリをContextへ登録します。既定では`RegexOptions.CultureInvariant`、100ミリ秒のタイムアウト、最大64パターンのLRUキャッシュが使用されます。

```csharp
StandardRegexLibrary<float>.Register(
    context,
    options: RegexOptions.IgnoreCase,
    matchTimeout: TimeSpan.FromMilliseconds(100),
    maxCachedPatterns: 64);
```

スクリプトでは次の関数を使用できます。

- `regex.isMatch(input,pattern)`
- `regex.match(input,pattern)`
- `regex.matches(input,pattern)`
- `regex.replace(input,pattern,replacement)`

```text
regex.isMatch("abc123","\\d+")
```

MoteScript文字列内では`\`をエスケープするため、正規表現の`\d`は`"\\d"`と記述します。`replace`の置換文字列ではC#と同じ`$1`などのキャプチャ参照を使用できます。

```text
regex.replace(
    "2026-07-25",
    "(\\d{4})-(\\d{2})-(\\d{2})",
    "$2/$3/$1")
```

`match`は次のメンバーを持つ辞書を返し、`matches`はその辞書の配列を返します。代入文として単独で使用する場合も文末に`;`を付けます。

- `success`: 成否
- `value`: 一致文字列
- `index`: 開始位置
- `length`: 長さ
- `groups`: グループ辞書の配列。各辞書も`success`、`value`、`index`、`length`を持ちます。

```text
result=regex.match("abc123","([a-z]+)(\\d+)");
groups=result.groups;
groups[2].value
```

Regexオブジェクトは登録した`regex`子Context内に保持され、同じパターンは再利用されます。キャッシュの確認と消去はC#から行えます。

```csharp
int count = StandardRegexLibrary<float>.GetCachedPatternCount(context);
StandardRegexLibrary<float>.ClearCache(context);
```

`RegexOptions.Compiled`と無限タイムアウトは使用できません。タイムアウト時には`RegexMatchTimeoutException`が発生します。Match結果の辞書・配列や置換文字列の生成ではGC割り当てが発生します。

### 数値関数

標準数値関数を使用する前に、使用するCalculatorと同じ型でMathライブラリをContextへ登録します。

```csharp
var context = new Context<float>();
StandardMathLibrary<float>.Register(context);
```

スクリプトでは次の関数を使用できます。

- `math.abs(value)`
- `math.min(a,b)`、`math.max(a,b)`
- `math.clamp(value,min,max)`
- `math.sign(value)`
- `math.pow(value,exponent)`
- `math.sqrt(value)`
- `math.exp(value)`
- `math.log(value[,base])`、`math.log10(value)`
- `math.sin(value)`、`math.cos(value)`、`math.tan(value)`
- `math.asin(value)`、`math.acos(value)`、`math.atan(value)`
- `math.atan2(y,x)`
- `math.floor(value)`
- `math.ceiling(value)`または`math.ceil(value)`
- `math.round(value[,digits])`
- `math.truncate(value)`
- `math.pi()`

```text
radius=3;
area=math.pi()*math.pow(radius,2)
```

`round`はC#と同じく既定で`MidpointRounding.ToEven`を使用します。`decimal`の指数対数・三角関数・πは、`System.Math`との互換性のため内部で一度`double`を経由するので、decimal固有の精度が必要な処理では注意してください。

`CalculatorInteger`では`abs`、`min`、`max`、`clamp`、`sign`、非負整数指数の`pow`、各丸め関数を使用できます。小数結果を必要とする平方根、指数対数、三角関数、負数指数の`pow`、`pi`は`NotImplementedException`を送出します。

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

### 処理の中断と再開

`yield(値)`を含むスクリプトは、`CreateExecution(context)`で実行単位となる`MoteExecution<T>`を作成します。最初の`Evaluate()`で実行を開始し、`YieldValue`で渡された値を取得して、`Resume()`で`yield`の次の文から再開できます。

`CreateExecution()`には`IExecutionContext<T>`を実装したContextが必要です。標準の`Context<T>`は`IExecutionContext<T>`を実装済みです。独自のContextを使用する場合は、`IContext<T>`ではなく`IExecutionContext<T>`を実装し、`TryAcquireExecution()`と`ReleaseExecution()`で実行の所有権を管理してください。`CurrentExecution`は読み取り専用です。実行中のContextを別の`MoteExecution<T>`で同時に使用することはできません。

```csharp
var script = decoder.Decode(
    "i=0;while(i<3){i=i+1;yield(i)};i");
MoteExecution<float> execution = script.CreateExecution(context);
execution.Evaluate();

while (execution.IsYielded)
{
    Debug.Log(execution.YieldValue.Value);
    execution.Resume();
}

MoteValue<float> result = execution.Result;
```

この例では`1`、`2`、`3`を順番に受け取り、最後の`result`は`3`になります。複数回Yieldしても同じ`MoteExecution<T>`を使用するため、Yieldごとに実行オブジェクトは生成されません。中断中も同じ`Context`を保持するため、再開前にC#側で変更した値は再開後の評価へ反映されます。関数内で中断した場合は、関数引数とループ位置も維持されます。

`MoteExecution<T>.State`は`Ready`、`Running`、`Yielded`、`Completed`、`Faulted`のいずれかです。`Resume()`は`Yielded`状態でのみ呼び出せます。完了後の結果は`Result`、例外終了時の例外は`Exception`から取得できます。

`CurrentExecution`には実行開始時から中断中まで現在の`MoteExecution<T>`が設定され、`Completed`または`Faulted`になると自動的に解除されます。

`yield`は式の一部ではなく、`yield(value);`のように独立した文として記述してください。`yield`を含むスクリプトを直接`script.Evaluate(context)`で実行すると例外になります。必ず`CreateExecution(context)`を使用してください。再開後に例外が発生した実行は`Faulted`となり、再開できません。

#### 複数の実行単位で値を共有

複数の`MoteExecution<T>`で値を共有する場合は、実行ごとに異なる`IExecutionContext<T>`を用意し、共有値を保持するContextを各実行Contextの変数として登録します。

```csharp
var shared = new Context<float>();
shared["score"] = new MoteValue<float>(0);

var contextA = new Context<float>();
var contextB = new Context<float>();

contextA["shared"] = new MoteValue<float>(shared);
contextB["shared"] = new MoteValue<float>(shared);

var executionA = decoder
    .Decode("shared.score=shared.score+1;yield(shared.score)")
    .CreateExecution(contextA);
var executionB = decoder
    .Decode("shared.score=shared.score+10;yield(shared.score)")
    .CreateExecution(contextB);

executionA.Evaluate(); // YieldValueは1
executionB.Evaluate(); // YieldValueは11
```

`contextA`と`contextB`はそれぞれの`CurrentExecution`を保持し、`shared`は同じインスタンスを参照します。これにより、ローカル変数と実行状態は分離したまま、`shared.score`などの値だけを共有できます。

共有専用のContext型を定義する場合は、`IExecutionContext<T>`ではなく`IContext<T>`だけを実装すると、誤って`CreateExecution()`の実行用Contextとして使用することを防げます。上の例で`Context<T>`を使用している`shared`も、共有値の格納用途に限定し、`CreateExecution()`には渡さないでください。

同一スレッドで`Evaluate()`と`Resume()`を順番に呼び出す運用では、通常のContextで共有できます。複数スレッドから同時に実行する場合、`shared.score=shared.score+1`のような読み取りと書き込みを組み合わせた処理は原子的ではありません。共有Contextの排他制御、またはC#側に登録した原子的な更新関数を使用してください。

`new shared`を評価すると別の辞書として複製されるため、共有を維持する場合は同じContextインスタンスを`MoteValue<T>`として各実行Contextへ登録します。

### 数値型の選択

用途に応じて`float`、`int`、`decimal`のCalculatorを選択できます。型ごとに最初の評価前に`Setup()`を呼び出します。

```csharp
MoteDecoder<float>.Setup();
var floatDecoder = new MoteDecoder<float>();

MoteDecoder<int>.Setup();
var intDecoder = new MoteDecoder<int>();

MoteDecoder<decimal>.Setup();
var decimalDecoder = new MoteDecoder<decimal>();
decimal result = decimalDecoder.Decode("0.1+0.2").Evaluate(new Context<decimal>()).Value;
```

MoteScriptの値は数値、文字列、配列、辞書、関数を扱えます。

## 開発

このリポジトリ自体はUnityプロジェクトです。パッケージルートを`Assets/MoteScript`に置くことで、従来どおりプロジェクト内で開発・テストしながら、Git URLではサブフォルダパッケージとして利用できます。
