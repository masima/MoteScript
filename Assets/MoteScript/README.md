# MoteScript

Unity上で数値・文字列の式、配列、辞書、関数、条件分岐、ループ、Yieldによる中断と再開を扱う軽量スクリプトランタイムです。`float`、`int`、`decimal`向けのCalculatorと標準関数ライブラリを同梱しています。

## 目次

- [Git URLからインストール](#git-urlからインストール)
- [基本的な使い方](#基本的な使い方)
  - [クイックスタート](#クイックスタート)
  - [数値型の選択](#数値型の選択)
  - [デコーダーの再利用](#デコーダーの再利用)
  - [デコード済みの式を再評価](#デコード済みの式を再評価)
  - [定数式の最適化](#定数式の最適化)
- [基本構文](#基本構文)
  - [数値演算](#数値演算)
  - [比較・論理演算](#比較論理演算)
  - [変数](#変数)
- [標準ライブラリ](#標準ライブラリ)
  - [文字列](#文字列)
  - [正規表現](#正規表現)
  - [数値関数](#数値関数)
- [コレクションと制御構文](#コレクションと制御構文)
  - [配列](#配列)
    - [多次元配列](#多次元配列)
  - [辞書](#辞書)
  - [関数](#関数)
    - [辞書をselfとして受け取る](#辞書をselfとして受け取る)
    - [参照引数](#参照引数)
    - [ローカル変数](#ローカル変数)
  - [条件分岐](#条件分岐)
  - [ループ](#ループ)
- [C#連携と関数の再利用](#c連携と関数の再利用)
  - [関数定義を先に評価](#関数定義を先に評価)
  - [C#から関数を登録](#cから関数を登録)
- [実行量の制限](#実行量の制限)
- [Yield実行管理](#yield実行管理)
  - [yield構文と実行再開](#yield構文と実行再開)
  - [複数の実行単位で値を共有](#複数の実行単位で値を共有)
- [Unity Editorデバッガ](#unity-editorデバッガ)
  - [デバッガを有効化](#デバッガを有効化)
  - [.moteアセット](#moteアセット)
  - [EditorWindowから実行](#editorwindowから実行)
  - [C#からデバッグ実行](#cからデバッグ実行)
  - [行番号と実行状態](#行番号と実行状態)
- [開発](#開発)

## Git URLからインストール

UnityのPackage Managerで **Add package from git URL...** を選択し、次の形式で入力します。

```text
https://github.com/masima/MoteScript.git?path=/Assets/MoteScript
```

リリースタグを固定する場合:

```text
https://github.com/masima/MoteScript.git?path=/Assets/MoteScript#v0.12.0
```

または、利用側プロジェクトの`Packages/manifest.json`へ追加します。

```json
{
  "dependencies": {
    "io.github.masima.motescript": "https://github.com/masima/MoteScript.git?path=/Assets/MoteScript#v0.12.0"
  }
}
```

## 基本的な使い方

### クイックスタート

```csharp
MoteDecoder<float>.Setup();

var decoder = new MoteDecoder<float>();
var context = new Context<float>()
    .Set("a", 1f)
    .Set("b", 2f);

float result = decoder.Decode("a+b").Evaluate(context).Value;
```

従来の`Evalute()`は互換性のため残されていますが非推奨です。新しいコードでは`Evaluate()`を使用してください。

`decimal`を使用する場合は、`MoteDecoder<decimal>.Setup()`と`Context<decimal>`を使用します。

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

### 定数式の最適化

`DecodeOptimized()`はデコード後に副作用のない定数式を事前計算します。既にデコードした値には`MoteOptimizer<T>.Optimize()`を使用できます。元の構文木は変更されません。

```csharp
MoteValue<float> optimized = decoder.DecodeOptimized(
    "value+(2+3)*4");

MoteValue<float> decoded = decoder.Decode("value+2*3");
MoteValue<float> optimizedCopy =
    MoteOptimizer<float>.Optimize(decoded);
```

算術、比較、論理演算が対象です。変数を含む部分は実行時評価のまま残り、その内部にある定数部分だけが畳み込まれます。代入、配列、辞書、関数呼び出しを事前実行することはありません。

型不一致やゼロ除算など、定数式の評価で発生するエラーは最適化時に送出されます。ただし、`false && expression`や`true || expression`の右辺は通常の短絡評価と同様に評価されません。

`DecodeDebug()`は停止行とステップ順序を維持するため、自動では最適化されません。ソースレベルのデバッグが必要な場合は、最適化前の構文木を使用してください。

## 基本構文

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

## 標準ライブラリ

文字列リテラルは組み込み構文です。文字列操作、正規表現、数値関数は、対応する標準ライブラリをContextへ登録して使用します。

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

## コレクションと制御構文

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

#### 辞書をselfとして受け取る

関数宣言へ`;self 名前`を加え、その関数を辞書メンバーとして呼び出すと、関数を保持している辞書を指定した名前で受け取れます。

```text
addScore=(value;self owner)=>{
    owner.score=owner.score+value
};
player.score=10;
player.addScore=addScore;
player.addScore(5)
```

実行後の`player.score`は`15`です。`self`の名前は`owner`、`player`など任意の有効な識別子を指定できます。角括弧による`player["addScore"](5)`でも同じ辞書が渡されます。

`root.child.run()`では、関数メンバーを直接保持する`root.child`が渡されます。関数を別の辞書へコピーした場合は、定義元ではなく呼び出し先の辞書が渡されます。

```text
second.run=first.run;
second.run()
```

`self`を宣言した関数を通常変数から直接呼び出すと`InvalidOperationException`が発生します。`self`変数自体は読み取り専用のため再代入できませんが、`owner.value=1`のようなメンバーの追加・更新は通常の型固定規則に従って実行できます。

通常引数、`self`、ローカル変数は次のように併用できます。

```text
calculate=(value;self owner;local result)=>{
    result=owner.base+value;
    owner.result=result
}
```

再帰呼び出しでは呼び出しごとの所有辞書が保持されます。関数がYieldした場合も呼び出し時の辞書を捕捉したまま再開し、呼び出し元の変数へ別の辞書を代入しても`self`の参照先は変わりません。

#### 参照引数

関数定義と呼び出しの両方で`ref`を指定すると、呼び出し元の変数を関数内から直接更新できます。

```text
swap=(ref left,ref right;local temporary)=>{
    temporary=left;
    left=right;
    right=temporary
};
a=10;
b=20;
swap(ref a,ref b)
```

実行後の`a`は`20`、`b`は`10`です。通常変数に加えて、既存の配列要素と辞書メンバーも参照として渡せます。

```text
set=(ref target)=>{target=9};
values=(1,2,3);
data.value=1;
set(ref values[1]);
set(ref data.value)
```

対象は呼び出し時に存在する代入可能な値に限られます。未定義変数、存在しない配列要素・辞書メンバー、定数、計算式は渡せません。参照先の型は通常の代入規則に従い、異なる型への上書きや暗黙変換は行われません。定義側または呼び出し側の一方だけに`ref`を指定した場合も例外になります。

同じ対象を複数の参照引数へ渡すエイリアスは許容されます。再帰では`ref`を付けて参照をそのまま転送でき、関数がYieldした場合も参照先を保持したまま再開します。配列要素と辞書メンバーは呼び出した時点のコンテナを捕捉します。

#### ローカル変数

関数宣言の引数一覧に`; local`を加えると、その関数だけで使用するローカル変数を宣言できます。関数本体を追加で走査せず、関数のデコード時に宣言部分を解析します。

```text
calculate=(value;local doubled,result)=>{
    doubled=value*2;
    result=doubled+1;
    return result
};
calculate(4)
```

この例の結果は`9`です。`doubled`と`result`は関数終了時に削除され、呼び出し元のContextには残りません。

同名の変数がContextに存在する場合、関数実行中だけローカル変数がその値を隠し、関数の正常終了時または例外終了時に元の値が復元されます。ローカル変数は最初の代入で型が確定し、代入前に参照すると未定義変数の例外が発生します。

```text
result=100;
double=(value;local result)=>{
    result=value*2;
    return result
};
answer=double(5)
```

実行後の`result`は`100`、`answer`は`10`です。引数なしでローカル変数だけを宣言する場合は、`function=(;local temporary)=>{...}`と記述します。

ローカル変数のスコープは関数単位です。引数との同名宣言、ローカル変数の重複宣言、不正な識別子はデコード時に例外になります。再帰呼び出しでは呼び出しごとに値が退避され、関数内でYieldした場合は`Resume()`によって関数が終了するまでローカル値が保持されます。

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

## C#連携と関数の再利用

この節では、スクリプト関数の定義をContextへ保持する方法と、C#で実装した関数をスクリプトへ公開する方法を説明します。先に「[関数](#関数)」でスクリプト側の関数構文を確認してください。

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

関数定義をデコードするだけでは`Context`へ登録されないため、定義スクリプトは一度`Evaluate(context)`してください。また、関数の定義時と呼び出し時には同じ`Context`インスタンスを使用してください。

### C#から関数を登録

C#で実装した関数を`MoteValue<T>`に変換して`Context`へ登録すると、スクリプトから通常の関数と同じ構文で呼び出せます。

```csharp
using System.Collections.Generic;
using MoteScript;

static MoteValue<float> Sum(
    IContext<float> context,
    IContext<float> receiver,
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

この例では、C#で登録した`sum`がスクリプトから呼び出され、`result`は`6`になります。`parameters`には引数の式が渡されるため、それぞれを`Evaluate(context)`して値を取得します。通常変数から呼び出した場合、`receiver`は`null`です。登録後は同じ`Context`を使い回すことで、別途デコードしたスクリプトからも`sum`を呼び出せます。

辞書メンバーとして呼び出された場合、同じ`Function`の`receiver`引数から所有辞書を取得できます。

```csharp
static MoteValue<float> AddScore(
    IContext<float> context,
    IContext<float> receiver,
    List<MoteValue<float>> parameters)
{
    float amount = parameters[0].Evaluate(context).Value;
    float score = receiver["score"].Value + amount;
    receiver["score"] = new MoteValue<float>(score);
    return receiver["score"];
}

MoteValue<float>.Function addScore = AddScore;
context["addScore"] = new MoteValue<float>(addScore);

decoder.Decode(
    "player.score=10;"
    + "player.addScore=addScore;"
    + "player.addScore(5)")
    .Evaluate(context);
```

`receiver`には`player.addScore()`なら`player`、`player["addScore"]()`でも同じ`player`が渡されます。関数を別の辞書へコピーした場合は呼び出し先の辞書が渡されます。`addScore()`のように通常変数から直接呼び出した場合、`receiver`は`null`です。所有辞書を必須とするC#関数では、`null`を検査して用途に合った例外を送出してください。

## 実行量の制限

終了条件が外部入力に依存するループや再帰を安全に実行するため、ステップ数と関数呼び出し深度を制限できます。`CancellationToken`によるC#側からの中断にも対応しています。

```csharp
using System.Threading;

var options = new MoteExecutionOptions(
    maxSteps: 10000,
    maxFunctionDepth: 64,
    cancellationToken: cancellationToken);

try
{
    MoteValue<float> result = expression.Evaluate(context, options);
}
catch (MoteExecutionLimitException exception)
{
    // exception.Limit、Actual、Maximumから超過内容を確認できます。
}
catch (OperationCanceledException)
{
    // CancellationTokenによって中断されました。
}
```

`maxSteps`または`maxFunctionDepth`に`0`を指定すると、対応する制限は無効になります。従来の`Evaluate(context)`は無制限で実行されるため、外部から受け取ったスクリプトにループや再帰が含まれる場合は、制限付きオーバーロードを使用してください。制限状態は関数呼び出しやネストした評価にも引き継がれ、評価完了後または例外発生後にリセットされます。

Yieldを含むスクリプトでは、`CreateExecution(context, options)`へ同じオプションを渡します。

```csharp
var options = new MoteExecutionOptions(maxSteps: 10000);
MoteExecution<float> execution =
    script.CreateExecution(context, options);

execution.Evaluate();
while (execution.IsYielded)
{
    execution.Resume();
}
```

ステップ数は最初の`Evaluate()`からすべての`Resume()`まで累積されます。上限超過またはキャンセルが発生すると、`MoteExecution<T>`は`Faulted`状態になり、発生した例外を`Exception`から取得できます。

## Yield実行管理

### yield構文と実行再開

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

### 複数の実行単位で値を共有

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

## Unity Editorデバッガ

### デバッガを有効化

Unity Editorでは追加設定なしでデバッガが常に有効です。`MoteScript.Debugger.Editor`アセンブリ、デバッグ用Runtime API、行情報、停止判定、`.mote`アセット連携が自動的にコンパイルされます。

Playerビルドでは、デバッグ用Runtime APIと停止判定は標準ではコンパイル対象から外れます。Development Buildなどへ含める場合だけ、Unityの **Project Settings > Player > Other Settings > Scripting Define Symbols**へ`MOTESCRIPT_DEBUGGER`を追加してください。通常のリリースビルドではDefineを追加する必要はありません。

### .moteアセット

Unity Editorでは、`.mote`ファイルは`MoteScriptImporter`によってTextAssetとしてインポートされます。通常のTextAssetと同様にC#から参照でき、Debugger Windowの`Script`欄にも指定できます。

Projectビューで`.mote`アセットをダブルクリックすると、Unityで設定された外部テキストエディタで開きます。デバッガで開く場合は、アセットの右クリックメニューから **Open in MoteScript Debugger** を選択します。Debugger Windowのコード行をダブルクリックすると、外部エディタで該当行を開けます。

Package Managerから`Basic Usage`サンプルをインポートすると、関数、ローカル変数、複数行の処理を含む`BasicUsage.mote`を確認できます。

### EditorWindowから実行

Unityの **Window > MoteScript > Debugger** から専用ウィンドウを開けます。

1. `Script`の選択メニューから`.mote`アセットを指定します。メニュー上部の検索欄ではファイル名またはパスを検索できます。分岐や直下のスクリプトがないフォルダ階層は1項目にまとめて表示され、直近に選択したアセットは`Recent`から再選択できます。
2. `Float`、`Integer`、`Decimal`から数値型を選択します。
3. 行番号左の丸をクリックしてブレークポイントを設定します。
4. `Run`でブレークポイントまで実行するか、未実行状態で`Step Over`または`Step Into`を押して先頭行から停止します。

停止中は`Continue`、`Step Over`、`Step Into`、`Step Out`、`Stop`を使用できます。F5はRun／Continue、Shift+F5はStop、F10はStep Over、F11はStep Into、Shift+F11はStep Outです。数値・文字列の編集中はショートカットが無効になります。

コード、Context変数、コールスタックは個別にスクロールでき、境界をドラッグしてサイズを変更できます。変数はName、Type、Valueでソートでき、辞書と配列はTreeとして展開できます。停止中またはYield中は数値と文字列を編集でき、数値欄には`1+2*3`のようなMoteScript演算式を入力できます。実行完了時はResult、Yield中はYield値を表示します。ウォッチ式の評価機能はありません。

ブレークポイントは`.mote`アセットごとにSessionStateへ保存され、同じUnity Editorセッション内ではウィンドウを閉じても復元されます。外部エディタでソースを変更すると、未実行時は自動で再読み込みします。停止中またはYield中は変更通知を表示し、次回Run時に最新ソースを再デコードします。

例外はスクリプト名と行番号を含めてUnity Consoleへ出力されます。`.mote`アセットを使用した場合は、Consoleの位置情報から外部テキストエディタの対象行を開けます。

### C#からデバッグ実行

Runtime APIから使用する場合は、`DecodeDebug()`で行情報を持つドキュメントを作成し、`MoteDebugSession<T>`へブレークポイントを登録します。

```csharp
MoteDecoder<float>.Setup();

var decoder = new MoteDecoder<float>();
MoteValue<float> script = decoder.DecodeDebug(
    sourceText,
    "Assets/Scripts/example.mote",
    out MoteDebugDocument document);

var session = new MoteDebugSession<float>(document);
session.SetBreakpoint(3);

var context = new Context<float>();
MoteExecution<float> execution =
    script.CreateDebugExecution(context, session);

execution.Evaluate();
if (execution.IsPaused)
{
    Debug.Log(execution.CurrentLocation);
    execution.StepOver();
}
```

`MoteDebugSession<T>.Paused`と`ExceptionThrown`で停止・例外を受け取れます。停止後は`ContinueDebugging()`、`StepInto()`、`StepOver()`、`StepOut()`、`StopDebugging()`を呼び出します。

### 行番号と実行状態

ブレークポイントとステップ実行は文ではなく表示上の行を指定します。ただし、1つの文が複数行に分かれている場合、その文の途中の行へ設定したブレークポイントは文頭を含む行へ正規化されます。同じ行に複数の式がある場合も、その行を1つの停止位置として扱います。

コード欄では通常の選択行を薄い青で表示し、実行位置には専用の`▶`マーカーを表示します。停止中は黄色、Yield中は青緑、例外発生位置は赤色の背景となるため、選択行と現在の実行位置を区別できます。ブレークポイントの赤い丸は実行位置マーカーと同時に表示されます。

デバッガによる停止は`MoteExecutionState.Paused`、スクリプト自身の`yield(value)`は`MoteExecutionState.Yielded`として区別されます。`Paused`と`Yielded`のどちらからもContinue、Step Into、Step Over、Step Outで再開できます。従来どおり`Yielded`から`Resume()`を使用することもできます。`StopDebugging()`では継続フレームを破棄し、関数引数とローカル変数を復元してContextの実行所有権を解放します。

## 開発

このリポジトリ自体はUnityプロジェクトです。パッケージルートを`Assets/MoteScript`に置くことで、従来どおりプロジェクト内で開発・テストしながら、Git URLではサブフォルダパッケージとして利用できます。

パッケージ内は、最小実行機能を収めた`Runtime`、数値・文字列・正規表現の標準関数を収めた`Libraries`、責務別の`Tests`、Package Managerから導入できる`Samples~`に分かれています。実行コアは`MoteScript`、標準関数群は`MoteScript.StandardLibraries`の独立したアセンブリです。

Package ManagerのSamples欄から`Basic Usage`をインポートすると、基本的なセットアップと評価を確認できます。
