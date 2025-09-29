using Godot;
using System;

public partial class RatingnPointsTest : ColorRect
{
	private VBoxContainer Table { get; set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Table = GetNode<VBoxContainer>("Scroll/Rows");
		for (int i = 0; i < 12000;)
		{
			i = i + 100;
			var Row = new HBoxContainer();
			var Label1 = new Label();
			var Label2 = new Label();
			var Label3 = new Label();
			Label1.Text = i.ToString();
			Label2.Text = SettingsOperator.Get_ppvalue(i, 0, 0, 0, 1, i).ToString();
			Label3.Text = (i * SettingsOperator.levelweight).ToString();
			Label1.HorizontalAlignment = HorizontalAlignment.Center;
			Label2.HorizontalAlignment = HorizontalAlignment.Center;
			Label3.HorizontalAlignment = HorizontalAlignment.Center;
			Label1.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			Label2.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			Label3.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			Row.AddChild(Label1);
			Row.AddChild(Label2);
			Row.AddChild(Label3);
			Table.AddChild(Row);
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
