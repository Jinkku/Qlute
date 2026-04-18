using Godot;

public partial class SceneTransition : Node
{
    [Export] private float _time = 1.0f;
    [Export] private Tween.EaseType _easeType = Tween.EaseType.Out;
    [Export] private Tween.TransitionType _transitionType = Tween.TransitionType.Linear;

    public void Switch(string sceneName)
    {
        // Store the path, not the node reference — the C# wrapper for
        // CurrentScene can become stale/invalid even if the node is still alive
        Node oldScene = GetTree().CurrentScene;
        NodePath oldScenePath = oldScene.GetPath();

        // Load and add the new scene
        Node newScene = GD.Load<PackedScene>(sceneName).Instantiate();
        GetTree().Root.AddChild(newScene);
        GetTree().CurrentScene = newScene;
        // Start it invisible
        if (newScene is CanvasItem newCanvasItem)
            newCanvasItem.Modulate = new Color(1, 1, 1, 0);

        // Kill any in-progress transition
        Tween _tween = GetTree().CreateTween();
        _tween.SetEase(_easeType);
        _tween.SetTrans(_transitionType);

        // Run the two fades in parallel
        _tween.SetParallel(true);
        if (newScene is CanvasItem)
        {
            _tween.TweenProperty(newScene, "modulate:a", 1.0f, _time); 
            _tween.TweenProperty(newScene, "modulate", new Color(1f,1f,1f,1f), _time);
        }
        if (oldScene is CanvasItem)
            _tween.TweenProperty(oldScene, "modulate:a", 0.0f, _time + 0.1);

        // Step OUT of parallel mode so the callback is sequenced AFTER the fades
        _tween.SetParallel(false);
        _tween.TweenCallback(Callable.From(() =>
        {
            // Re-fetch by path so we always get a valid wrapper,
            // even if the original C# reference went stale
            Node old = GetTree().Root.GetNodeOrNull(oldScenePath);
            old?.QueueFree();
            if (newScene != null)
                newScene.Set("modulate", new Color(1f,1f,1f,1f));
        }));
        _tween.TweenProperty(newScene, "modulate", new Color(1f,1f,1f,1f), _time);
    }
}