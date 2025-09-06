#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;

[Tool]
public partial class RequireNode : EditorPlugin, ISerializationListener
{
	private EditorSelection _editorSelection;
	private static readonly string[] _allNodeChildClass = ClassDB.GetInheritersFromClass("Node");

	public override void _EnterTree()
	{		
		_editorSelection = EditorInterface.Singleton.GetSelection();
		_editorSelection.SelectionChanged += OnNodeSelected;
		SceneClosed += OnSceneClosed;
		SceneChanged += OnSceneChanged;
	}

	public override void _ExitTree()
	{
		_editorSelection.SelectionChanged -= OnNodeSelected;
		SceneChanged -= OnSceneChanged;
		SceneClosed -= OnSceneClosed;
		_editorSelection = null;
		ClearMap();
		_nodeConnections = null;
	}

	private Dictionary<Node, Callable> _nodeConnections = new();

	private void OnSceneChanged(Node _)
	{
		ClearMap();
	}

	private void OnSceneClosed(string _)
	{
		ClearMap();
	}

	private void ClearMap()
	{		
		foreach (var kvp in _nodeConnections)
		{
			var node = kvp.Key;
			var lambda = kvp.Value;
			// GD.Print("clearing, ", node);

			if (node != null && IsInstanceValid(node) && node.IsConnected(GodotObject.SignalName.ScriptChanged, lambda))
			{
				node.Disconnect(GodotObject.SignalName.ScriptChanged, lambda);
				node.TreeExiting -= ClearMap;
				node.ChildEnteredTree -= OnScriptChanged;
			}
		}

		_nodeConnections.Clear();
		
	}

	private void OnNodeSelected()
	{
		var nodes = _editorSelection.GetSelectedNodes();
		// GD.Print(nodes);
		foreach (var node in nodes)
		{

			if (_nodeConnections.ContainsKey(node))
				continue;

			var lambda = Callable.From(() =>
			{
				OnScriptChanged(node);
			});

			if (!node.IsConnected(GodotObject.SignalName.ScriptChanged, lambda))
			{
				node.Connect(GodotObject.SignalName.ScriptChanged,
					lambda);
				node.TreeExiting += ClearMap;
				node.ChildEnteredTree += OnScriptChanged;
			}

			_nodeConnections.Add(node, lambda);
			// GD.Print("连接信息：", node.GetSignalConnectionList(SignalName.ScriptChanged));
			// GD.Print("连接数（期望最大2），", node.GetSignalConnectionList(SignalName.ScriptChanged).Count);
			// GD.Print();			
		}
	}

	private void OnScriptChanged(Node node)
	{
		// GD.Print("changed, ", node, "has script: ", node.GetScript());
		if (
			!Engine.IsEditorHint() ||
			node == null ||
			!IsInstanceValid(node) ||
			(!string.IsNullOrEmpty(node.SceneFilePath) && node != GetTree().EditedSceneRoot)
		)
			return;

		var script = node.GetScript().As<Script>();
		// GD.Print("script: ", script);
		if (script == null || !IsInstanceValid(script))
			return;

		CheckAttribute(script, node);
	}

	private void CheckAttribute(Script script, Node node)
	{
		if (script is GDScript gdScript)
		{
			CheckGDScript(gdScript, node);
		}
		else if (script is CSharpScript csScript)
		{
			CheckCSharp(csScript, node);
		}
	}

	private void CheckCSharp(CSharpScript script, Node node)
	{
		// 可以被挂载到节点上的C#脚本，脚本文件中必须定义一个和文件名相同的类
		var type = Type.GetType(script.ResourcePath.GetFile().Replace(".cs", string.Empty));
		if (type == null)
			return;
		var requiredTypes = type
			.GetCustomAttributes(true)
			.OfType<IRequireNodeAttribute>()
			.Select(attr => (attr.RequiredType, attr.AsChild));

		foreach (var (require, asChild) in requiredTypes)
		{

            var requireNode = (Node)Activator.CreateInstance(require);

			if (requireNode != null)
			{
				if (asChild)
					AddChildNode(node, requireNode, require.Name);
				else
					AddParentNode(requireNode, node, require.Name);
			}
			else
			{
				GD.PrintErr("[RequireNode]: Unable to create the specified node");
			}

			}

		var requiredScenePaths = type.GetCustomAttributes<RequireNodeAttribute>()
			.Select(attr => (attr.RequiredScenePath, attr.AsChild));

		foreach (var (path, asChild) in requiredScenePaths)
		{

			var scene = ResourceLoader.Load<PackedScene>(path)?.Instantiate();
			if (scene == null)
			{
				GD.PrintErr("[RequireNode]: Unable to create the specified scene");
				continue;
			}
			if (asChild)
				AddChildNode(node, scene, scene.Name);
			else
				AddParentNode(scene, node, scene.Name);

		}

	}

	private void CheckGDScript(GDScript script, Node node)
	{
		var sourceCode = script.SourceCode;
		var lines = sourceCode.Split('\n');
		var globalClassList = ProjectSettings.GetGlobalClassList();
		foreach (var line in lines)
		{
			if (line.Contains("# require_node:"))
			{
				var context = line.Substring(15).Trim();
				var split = context.Split(',');
				bool asChild = true;
				if (split.Length < 1)
					continue;

				var require = split[0];
				if (split.Length > 1)
				{
					asChild = string.IsNullOrEmpty(split[1]) || split[1].Contains("as child");
				}

				if (_allNodeChildClass.Contains(require) || require == "Node")
				{
					var requireNode = ClassDB.Instantiate(require).As<Node>();
					if (node != null)
					{
						if (asChild)
							AddChildNode(node, requireNode, require);
						else
							AddParentNode(requireNode, node, require);
					}

				}
				else if (globalClassList.Any(
					info =>
						info["class"].ToString() == require &&
						info["language"].ToString() == "GDScript" &&
						(_allNodeChildClass.Contains(info["base"].ToString()) || info["base"].ToString() == "Node")
				))
				{
					var path = globalClassList.FirstOrDefault(info => info["class"].ToString() == require)?["path"].ToString();

					var requireNode = ResourceLoader.Load<GDScript>(path).New().As<Node>();
					if (requireNode != null)
					{
						if (asChild)
							AddChildNode(node, requireNode, require);
						else
							AddParentNode(requireNode, node, require);
					}
					else
					{
						GD.PrintErr("[RequireNode]: Unable to create the specified node");
					}


				}
				else if (require.StartsWith("res://"))
				{
					var scene = ResourceLoader.Load<PackedScene>(require)?.Instantiate();
					if (scene == null)
					{
						GD.PrintErr("[RequireNode]: Unable to create the specified scene");
						continue;
					}
					if (asChild)
						AddChildNode(node, scene, scene.Name);
					else
						AddParentNode(scene, node, scene.Name);
				}
			}

			if (line.Contains("extends"))
				break;
		}
	}

	private void AddChildNode(Node parent, Node child, string childName)
	{
		var parentName = parent.Name;
		parent.AddChild(child);
		child.Name = childName;
		parent.Name = parentName;
		child.Owner = GetTree().EditedSceneRoot;

	}

	private void AddParentNode(Node parent, Node child, string parentName)
	{
		if (child != GetTree().EditedSceneRoot)
		{
			var childName = child.Name;
			var sceneRoot = GetTree().EditedSceneRoot;
			sceneRoot.AddChild(parent);
			parent.Name = parentName;
			parent.Owner = sceneRoot;
			child.Reparent(parent);
			child.Name = childName;
		}
		else
		{
			GD.PrintErr("[RequireNode]: Can not change scene root node");
		}

	}

	public void OnBeforeSerialize()
	{
		// Reload assemblies
		ClearMap();
	}

    public void OnAfterDeserialize()
    {
		// After reload assemblies
    }

}


[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class RequireNodeAttribute<T> : Attribute, IRequireNodeAttribute
	where T : Node
{
	public Type RequiredType { get; }
	public bool AsChild { get; }
	public RequireNodeAttribute(bool asChild = true)
	{
		RequiredType = typeof(T);
		AsChild = asChild;
	}
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class RequireNodeAttribute : Attribute
{
	public string RequiredScenePath { get; }
	public bool AsChild { get; }

	public RequireNodeAttribute(string scenePath, bool asChild = true)
	{
		RequiredScenePath = scenePath;
		AsChild = asChild;
	}
}

file interface IRequireNodeAttribute
{
	Type RequiredType { get; }
	bool AsChild { get; }
}


#endif
