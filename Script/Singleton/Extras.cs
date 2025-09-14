using Godot;
using System;
public class Extras
{
    public static double GetMilliseconds()
    {
        return (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
    }    
    public static string GetDayWithSuffix(int day)
    {
        if (day >= 11 && day <= 13) 
            return day + "th";

        switch (day % 10)  // day is int, so % works
        {
            case 1: return day + "st";
            case 2: return day + "nd";
            case 3: return day + "rd";
            default: return day + "th";
        }
    }
}
