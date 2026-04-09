using Godot;
using System;

public partial class Main : Node2D
{
	private Button StartButton;
	private VBoxContainer IdentityPrompt;
	private Button UseSavedButton;
	private Button NewIdentityButton;

	private PackedScene WasteScene = GD.Load<PackedScene>("uid://dt6dxcbysqucx");

	public override void _Ready()
	{
		StartButton = GetNode<Button>("%StartButton");
		IdentityPrompt = GetNode<VBoxContainer>("%IdentityPrompt");
		UseSavedButton = GetNode<Button>("%UseSavedButton");
		NewIdentityButton = GetNode<Button>("%NewIdentityButton");

		var savedToken = SpacetimeNetworkManager.Instance.LoadToken();

		if (savedToken != null)
		{
			IdentityPrompt.Visible = true;

			UseSavedButton.Pressed += () =>
			{
				IdentityPrompt.Visible = false;
				SpacetimeNetworkManager.Instance.Connect(savedToken);
			};

			NewIdentityButton.Pressed += () =>
			{
				IdentityPrompt.Visible = false;
				SpacetimeNetworkManager.Instance.Connect(null);
			};
		}
		else
		{
			SpacetimeNetworkManager.Instance.Connect(null);
		}

		SpacetimeNetworkManager.Instance.BaseSubscriptionApplied += () =>
		{
			SpacetimeNetworkManager.Instance.Conn.Reducers.CreatePlayer();
			StartButton.Visible = true;
		};

		StartButton.Pressed += () =>
		{
			GetTree().ChangeSceneToPacked(WasteScene);
		};
	}

	public override void _Process(double delta)
	{
	}
}
