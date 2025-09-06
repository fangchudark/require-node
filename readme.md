# RequireNode

一个可以自动创建脚本依赖节点的Godot .NET 小插件

在场景树编辑器选中一个节点，然后将标记了RequireNode的脚本拖拽上去即可

如果是注册为全局类的节点，也标记了RequireNode，从编辑器创建时也会自动创建指定的依赖

支持为GDScript脚本创建节点依赖。

## RequireNode标记

C# 需要使用（泛型）RequireNodeAttribute类标记来指示该节点的依赖

（非泛型）RequireNodeAttribute也支持标记该节点依赖哪个场景

可以为标记传入一个布尔值，指示要将依赖节点添加到子级(true) 还是父级(false)，默认为子级

除此之外，如果创建的子节点挂载了具有RequireNodeAttribute标记的脚本（场景除外），会再根据子节点的需求进行一层创建，**但仅限一层** 

```csharp
[RequireNode<AnimatedSprite2D>]
[RequireNode<CollisionShape2D>]
public partial class Player : CharacterBody2D
{

}

[RequireNode("res://player.tscn", false)]
[GlobalClass]
public partial class MovementController : Node
{

}

```
---

GDScript 需要使用顶层注释，即写在`extends`之前的指定格式的注释来进行依赖标记

`# require_node: 依赖节点/场景路径, as child/parent`

- `require_node`和`:`之间不能有空格

- 支持内置节点类名和注册为全局类的节点类名、场景文件路径
  - `# require_node: Node2D`
  - `# require_node: Player`
  - `# require_node: res://path/to/scene.tscn`

- 同样支持指定添加位置，默认为子节点
  - `# require_node: Player, as child`
  - `# require_node: Player, as parent`  


```gdscript
# require_node: AnimatedSprite2D
# require_node: CollisionShape2D

class_name Player
extends CharacterBody2D
```