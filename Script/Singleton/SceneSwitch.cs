using Godot;
using System;

public partial class SceneSwitch : Node
{
    public Node CurrentScene { get; set; }

    public override void _Ready()
    {
        Viewport root = GetTree().Root;
        // Using a negative index counts from the end, so this gets the last child node of `root`.
        CurrentScene = root.GetChild(-1);
        Node FpsIndicator = GD.Load<PackedScene>("res://Panels/Overlays/fps_counter.tscn").Instantiate();
        AddChild(FpsIndicator);
    }
    public void GotoScene(string path)
{
    // This function will usually be called from a signal callback,
    // or some other function from the current scene.
    // Deleting the current scene at this point is
    // a bad idea, because it may still be executing code.
    // This will result in a crash or unexpected behavior.

    // The solution is to defer the load to a later time, when
    // we can be sure that no code from the current scene is running:

    CallDeferred(MethodName.DeferredGotoScene, path);
}

public void DeferredGotoScene(string path)
{
    // It is now safe to remove the current scene.
    CurrentScene.Free();

    // Load a new scene.
    var nextScene = GD.Load<PackedScene>(path);

    // Instance the new scene.
    CurrentScene = nextScene.Instantiate();

    // Add it to the active scene, as child of root.
    GetTree().Root.AddChild(CurrentScene);

    // Optionally, to make it compatible with the SceneTree.change_scene_to_file() API.
    GetTree().CurrentScene = CurrentScene;
}
}
