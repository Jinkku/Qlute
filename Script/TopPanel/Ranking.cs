using Godot;
public partial class Ranking : Label
{
	// Called when the node enters the scene tree for the first time.
	public static Ranking Instance { get; set; }
	public override void _Ready()
	{
		Instance = this;
	}
}