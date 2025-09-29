using Godot;
using System;

public partial class MultiplayerList : PanelContainer
{
	[Export]
	PacketPeerUdp broadcaster;
	[Export]
	PacketPeerUdp listener = new PacketPeerUdp();
	[Export]
	int listenPort = 8911;
	[Export]
	int hostPort = 8912;
	[Export]
	string broadcastAddress = "192.168.1.255"; 
	[Signal]
	public delegate void JoinGameEventHandler(string ip);
	[Export]
	PackedScene ServerInfo;
	Timer broadcastTimer;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
