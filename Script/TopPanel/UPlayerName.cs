using Godot;
public partial class UPlayerName : Label
{
	// Called when the node enters the scene tree for the first time.
	public static UPlayerName Instance { get; set; }
	public override void _Ready()
	{
		Instance = this;
	}
}
