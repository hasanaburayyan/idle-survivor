using Godot;
using System;
using SpacetimeDB;
using SpacetimeDB.Types;

public partial class SpacetimeNetworkManager : Node
{
	[Signal]
	public delegate void BaseSubscriptionAppliedEventHandler();

	private const string TokenPath = "user://spacetime_token.txt";

	public static SpacetimeNetworkManager Instance { get; private set; }

	public DbConnection Conn { get; private set; }
	public Identity LocalIdentity { get; private set; }

	public override void _Ready()
	{
		Instance = this;
	}

	public string? LoadToken()
	{
		if (!FileAccess.FileExists(TokenPath)) return null;
		using var file = FileAccess.Open(TokenPath, FileAccess.ModeFlags.Read);
		return file?.GetAsText().Trim();
	}

	private void SaveToken(string token)
	{
		using var file = FileAccess.Open(TokenPath, FileAccess.ModeFlags.Write);
		file?.StoreString(token);
	}

	public void Connect(string? token)
	{
		GD.Print("Attempting to connect to SpacetimeDB");

		var builder = DbConnection.Builder()
		//   .WithUri("http://127.0.0.1:3000")
		.WithUri("https://maincloud.spacetimedb.com")
		  .WithDatabaseName("idle-survivor")
		  .OnConnect(OnConnected)
		  .OnConnectError(OnConnectError)
		  .OnDisconnect(OnDisconnect);

		if (token != null)
		{
			builder = builder.WithToken(token);
		}

		Conn = builder.Build();
	}

	private void OnConnected(DbConnection conn, Identity identity, string token)
	{
		LocalIdentity = identity;
		SaveToken(token);
		GD.Print("Connected to Spacetime");

		Conn.SubscriptionBuilder()
		  .OnApplied((ctx) =>
		  {
			  GD.Print("Base subscription applied");
			  EmitSignal(SignalName.BaseSubscriptionApplied);
		  })
		  .OnError((ErrorContext ctx, Exception e) =>
		  {
			  GD.Print(e.Message);
		  })
		  .SubscribeToAllTables();
	}

	public void OnConnectError(Exception e)
	{
		GD.Print($"Error {e.Message}");
	}

	public void OnDisconnect(DbConnection conn, Exception? e)
	{
		if (e != null)
		{
			GD.Print($"Error {e.Message} on disconnect");
			return;
		}
		GD.Print("Disconnected");
	}

	public override void _Process(double delta)
	{
		Conn?.FrameTick();
	}
}
