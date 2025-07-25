using System.Collections.Generic;

public class ServerInfo
{
    public int ID { get; set; }
    public string Name { get; set; }
    public string Owner { get; set; }
    public List<PlayerList> PlayerList { get; set; }
}

public class PlayerList
{
    public int ID { get; set; }
    public string Username { get; set; }
    public int Rank { get; set; }
    public bool CurrentlyPlaying { get; set; }
}