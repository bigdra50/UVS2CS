# UVS2CS アーキテクチャ

Unity Visual Scripting のグラフと C# コードを双方向に変換するツール。

## 変換パイプライン

```
Direction 1: Graph → C#

  ScriptGraphAsset (.asset)
         |
         | YAML から _data._json を抽出
         v
  SerializedGraphParser
         |
         | Newtonsoft.Json で FullSerializer 形式をパース
         | $id/$ref 解決、$type 正規化 (Bolt.* → Unity.VisualScripting.*)
         v
  SerializedGraphSnapshot
  (UnitId, UnitKind, Member, DefaultValues, ControlEdges, ValueEdges)
         |
         | JsonGraphReader
         | PortRef(unitId, key) ベースの走査
         | Unit のポート Define() 状態に非依存
         v
       IRGraph
         |
         | CSharpEmitter (StringBuilder)
         v
    C# ソースコード


Direction 2: C# → Graph

    C# ソースコード
         |
         | CSharpParser (Roslyn 4.3.1)
         | CSharpSyntaxTree.ParseText → SemanticModel
         v
       IRGraph
         |
         | GraphWriter
         | UnitFactory + ConnectionBuilder
         v
  ScriptGraphAsset (.asset)
```

## IR (中間表現)

両方向の変換は IRGraph を介して行われる。IRGraph は Unity API にも Roslyn にも依存しない純粋な C# データ構造。

```
IRGraph
├── ClassName: string
├── Namespace: string
├── Usings[]: IRUsing
├── Fields[]: IRField           ← グラフ変数
│   ├── Name, Type, Modifier
│   ├── DefaultValue: IRExpression
│   └── Origin: Graph | Object | Scene | ...
└── Methods[]: IRMethod         ← イベントハンドラ
    ├── Name: "Start" | "Update" | ...
    ├── Kind: Lifecycle | Custom | Coroutine
    └── Body: IRBlock
        └── Statements[]: IRStatement
```

### IRStatement の種類

| IR型 | C# 構文 | VS ノード |
|------|---------|-----------|
| IRBlock | `{ ... }` | Sequence |
| IRIf | `if (cond) { } else { }` | If / Branch |
| IRFor | `for (var i = ...; ...; ...)` | For |
| IRForEach | `foreach (var item in coll)` | ForEach |
| IRWhile | `while (cond) { }` | While |
| IRAssignment | `target = value` | SetVariable / SetMember |
| IRExpressionStatement | `expr;` | InvokeMember |
| IRVariableDeclaration | `var x = expr` | Cache |
| IRSwitch | `switch (val) { case: }` | SwitchOnInteger/String/Enum |
| IRReturn | `return expr` | GraphOutput |
| IRBreak | `break` | Break |
| IRYieldReturn | `yield return expr` | WaitForSeconds 等 |
| IRTryCatch | `try { } catch { }` | TryCatch |
| IRThrow | `throw expr` | Throw |

### IRExpression の種類

| IR型 | C# 構文 | VS ノード |
|------|---------|-----------|
| IRLiteral | `42`, `"hello"`, `true` | Literal |
| IRIdentifier | `variableName` | GetVariable |
| IRThis | `gameObject` | This |
| IRNull | `null` | Null |
| IRMemberAccess | `obj.member` | GetMember |
| IRMethodCall | `obj.Method(args)` | InvokeMember |
| IRConstructorCall | `new Type(args)` | CreateStruct |
| IRBinaryOp | `a + b`, `a > b` | Add, Greater 等 |
| IRUnaryOp | `-x`, `!x` | Negate |
| IRCast | `(Type)x` | (型変換) |
| IRConditional | `cond ? a : b` | ToggleValue |
| IRIndexAccess | `list[i]` | GetListItem |
| IRNullCheck | `x == null` | NullCheck |
| IRNullCoalesce | `x ?? fallback` | NullCoalesce |

## Graph → C# パイプラインの詳細

### SerializedGraphParser

`.asset` ファイルの YAML から `_data._json` フィールドを正規表現で抽出し、Newtonsoft.Json の `JObject` でパースする。

Unity の `SerializedObject` API は使用しない。理由: `SerializedObject` 経由で読むと Unity が `MemberUnit.Define()` を再実行し、`defaultValues` がリフレクションで取得したデフォルト値にリセットされるため。

FullSerializer 形式の特殊フィールド:
- `$id` / `$ref`: オブジェクト参照（2パスで解決）
- `$type`: 型名（`Bolt.*` → `Unity.VisualScripting.*` に正規化）
- `$content` / `$type`: 値ラッパー（`{"$content": 42, "$type": "System.Int32"}`）

### SerializedGraphSnapshot

パース結果を保持するデータモデル。Unity API に依存しない。

```
SerializedGraphSnapshot
├── Units: Dictionary<id, SerializedUnit>
│   ├── Id, TypeName, Kind
│   ├── Member: { Name, TargetTypeName, ParameterTypes }
│   └── DefaultValues: Dictionary<portKey, object>
├── ControlEdges[]: { SourceUnitId, SourceKey, DestUnitId, DestKey }
├── ValueEdges[]: { SourceUnitId, SourceKey, DestUnitId, DestKey }
└── Variables: Dictionary<name, value>
```

ポイント: 接続は `PortRef(unitId, key)` で表現。Unity の `ControlOutput`/`ValueOutput` ポートオブジェクトに依存しない。これにより `Define()` 失敗の影響を完全に回避。

### JsonGraphReader

`SerializedGraphSnapshot` から `IRGraph` を構築する。

1. Variables → `IRField`
2. Event Unit（isControlRoot）を検出 → 各イベントが `IRMethod` に
3. `TraceControlFlow(unitId, outputKey)`: ControlEdge を辿って IRStatement チェーンを構築
4. `ResolveValueInput(unitId, portKey)`: ValueEdge を辿って IRExpression ツリーを構築
5. 接続がない場合は `DefaultValues` からリテラル値を取得

## C# → Graph パイプラインの詳細

### CSharpParser (Roslyn)

Roslyn の `CSharpSyntaxTree.ParseText()` で C# ソースを構文木にパース。`CSharpCompilation` で `SemanticModel` を取得し、型解決を行う。

`MonoBehaviourDetector` でライフサイクルメソッド（Start, Update 等）を識別。`SyntaxWalker` が構文木を走査して IRStatement/IRExpression に変換。

### GraphWriter

`IRGraph` から `FlowGraph` を構築する。

`UnitFactory` で各 IR 型に対応する VS Unit を生成し、`ConnectionBuilder` で制御フロー・値接続を作成。`LayoutCalculator` でノードの配置座標を自動計算。

## 同梱 DLL

| DLL | バージョン | 用途 |
|-----|-----------|------|
| Microsoft.CodeAnalysis.dll | 4.3.1 | Roslyn コア API |
| Microsoft.CodeAnalysis.CSharp.dll | 4.3.1 | C# 構文解析 |
| System.Collections.Immutable.dll | 6.0.0 | Roslyn 依存 |
| System.Reflection.Metadata.dll | 5.0.0 | Roslyn 依存 |

全 DLL は Editor 専用（`.meta` で `Editor: enabled: 1`、ビルドターゲットは全て `enabled: 0`）。

## 対応 Unit 型

### ハンドラ一覧（Graph → C# 方向）

| ハンドラ | 対応 Unit | 方式 |
|---------|----------|------|
| JsonGraphReader 内蔵 | InvokeMember, GetMember, SetMember, SetVariable, If/Branch, For, ForEach, While, Sequence, Break, TriggerCustomEvent, Timer/Wait | JSON データから直接変換 |
| MathHandlers | Add, Subtract, Multiply, Divide, Modulo, Sum, Lerp, Min, Max, Distance, Angle, Dot/Cross, Absolute, Normalize, Round, Root, PerSecond, Exponentiate, Average, MoveTowards | 型名パターンマッチ（Scalar/Vector2/3/4/Generic 全対応） |
| EventHandlers | Start, Update, FixedUpdate, LateUpdate, OnEnable, OnDisable, OnDestroy, OnCollision\*, OnTrigger\*, OnMouse\*, OnApplication\*, OnAnimator\*, OnGUI, OnTransform\*, InputSystem\* | IEventUnit + 型名フォールバック |
| ControlFlowHandlers | If, Sequence, For, ForEach, While, Break, TryCatch, Throw, Switch\*, Select\*, Cache, Once, ToggleFlow, ToggleValue | |
| VariableHandlers | GetVariable, SetVariable, IsVariableDefined, SaveVariables | |
| MemberHandlers | InvokeMember, GetMember, SetMember, CreateStruct, Expose | |
| LogicHandlers | And, Or, Negate, ExclusiveOr, Equal, NotEqual, Greater, >=, Less, <=, ApproximatelyEqual, NotApproximatelyEqual | |
| TimeHandlers | Timer, Cooldown, WaitForSeconds/NextFrame/EndOfFrame/Until/While | |
| CollectionHandlers | List操作(11種), Dictionary操作(8種), Count/First/Last | |
| NullHandlers | NullCheck, NullCoalesce | |
| NestingHandlers | GraphInput, GraphOutput, SubgraphUnit | |
| CustomEventHandlers | CustomEvent, TriggerCustomEvent, BoltUnityEvent | |

### 型名正規化

旧 Bolt 形式のアセットとの互換性:

| 旧名 | 正規化後 |
|------|---------|
| `Bolt.Branch` | `Unity.VisualScripting.If` |
| `Bolt.GetVariable` | `Unity.VisualScripting.GetVariable` |
| `Bolt.InvokeMember` | `Unity.VisualScripting.InvokeMember` |
| `Bolt.*` | `Unity.VisualScripting.*` |
| `Ludiq.*` | `Unity.VisualScripting.*` |

## ディレクトリ構成

```
Assets/Plugins/UVS2CS/
├── package.json
├── Editor/
│   ├── IR/                         # 共通中間表現
│   │   ├── IRGraph.cs
│   │   ├── IRStatement.cs
│   │   ├── IRExpression.cs
│   │   └── IRTypeRef.cs
│   ├── GraphToIR/                  # Graph → IR
│   │   ├── GraphReader.cs          # エントリポイント
│   │   ├── FlowTracer.cs          # 制御フロー走査 (Unity API)
│   │   ├── ValueResolver.cs       # 値式解決 (Unity API)
│   │   ├── ConnectionResolver.cs  # invalidConnection 辿り
│   │   ├── AssetJsonReader.cs     # JSON 補完データソース
│   │   ├── Serialized/
│   │   │   ├── SerializedGraphSnapshot.cs  # port key ベースモデル
│   │   │   ├── SerializedGraphParser.cs    # JSON パーサー
│   │   │   └── JsonGraphReader.cs          # JSON → IR (推奨パス)
│   │   └── UnitHandlers/          # Unit 種別ごとの変換 (Unity API)
│   ├── IRToCSharp/                 # IR → C#
│   │   ├── CSharpEmitter.cs
│   │   ├── StatementEmitter.cs
│   │   ├── ExpressionEmitter.cs
│   │   ├── TypeNameResolver.cs
│   │   └── IndentWriter.cs
│   ├── CSharpToIR/                 # C# → IR (Roslyn)
│   │   ├── CSharpParser.cs
│   │   ├── SyntaxWalker.cs
│   │   ├── SemanticResolver.cs
│   │   └── MonoBehaviourDetector.cs
│   ├── IRToGraph/                  # IR → Graph
│   │   ├── GraphWriter.cs
│   │   ├── UnitFactory.cs
│   │   ├── ConnectionBuilder.cs
│   │   └── LayoutCalculator.cs
│   ├── Plugins/                    # Roslyn DLL
│   └── UI/
│       ├── UVS2CSWindow.cs         # EditorWindow
│       └── BatchConverter.cs       # 一括変換
└── Tests/Editor/
```

## 設計判断

### JSON 直接パースを採用した理由

Unity の `FlowGraph` API を使うと、`MemberUnit.Define()` が失敗した場合にポートが空になり接続が辿れない。`Define()` 失敗の原因はカスタム型のリフレクション解決失敗で、プロジェクト固有の ScriptableObject 等が参照される場合に発生する。

JSON パーサーはこの問題を構造的に回避する。グラフの全情報（Unit 型、member 情報、接続、defaultValues）は `.asset` ファイルの JSON に完全に保存されており、`Define()` の成否に関係なく読める。

### 2つの Graph → IR パス

1. JSON パス（推奨）: `SerializedGraphParser` → `JsonGraphReader` → `IRGraph`
2. Unity API パス（フォールバック）: `GraphReader` → `FlowTracer` → `ValueResolver` → `IRGraph`

`GraphReader.Read(ScriptGraphAsset)` は JSON パスを優先し、JSON パースに失敗した場合のみ Unity API パスにフォールバックする。
