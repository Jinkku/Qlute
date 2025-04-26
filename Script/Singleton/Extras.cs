using Godot;
using System;
public class Extras
{
    public static double GetMilliseconds(){
        return (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
    }    
}
