using Godot;
using System;

public partial class Player : CharacterBody2D
{
	public Label DisplayNameLabel;

	public override void _Ready()
	{
		DisplayNameLabel = GetNode<Label>("%DisplayName");
	}

	public void SetName(String name) {
		DisplayNameLabel.Text = name;
	}
}
