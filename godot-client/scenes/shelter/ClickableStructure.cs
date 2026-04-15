using Godot;
using System;

public partial class ClickableStructure : StaticBody2D
{
	[Signal]
	public delegate void StructureClickedEventHandler();

	public override void _Ready()
	{
		InputPickable = true;
	}

	public override void _InputEvent(Viewport viewport, InputEvent @event, int shapeIdx)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			EmitSignal(SignalName.StructureClicked);
			viewport.SetInputAsHandled();
		}
	}
}
