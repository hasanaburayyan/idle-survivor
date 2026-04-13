using Godot;

/// <summary>
/// Fills the playfield SubViewport with a solid color (location tint).
/// </summary>
public partial class PlayfieldBackground : Node2D
{
	private Color _color = new(0.45f, 0.45f, 0.48f);

	public void SetColor(Color color)
	{
		_color = color;
		QueueRedraw();
	}

	public override void _Ready()
	{
		GetViewport().SizeChanged += () => QueueRedraw();
		QueueRedraw();
	}

	public override void _Draw()
	{
		var r = GetViewport().GetVisibleRect();
		DrawRect(r, _color);
	}
}
