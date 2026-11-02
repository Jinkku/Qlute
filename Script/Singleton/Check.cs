using System;

public static class Check
{
	// Called every frame from several places, so avoid ToLower() allocations.
	public static bool CheckBoolValue(string value)
	{
		if (value == null) return false;
		return value == "1"
			|| value.Equals("true", StringComparison.OrdinalIgnoreCase)
			|| value.Equals("yes", StringComparison.OrdinalIgnoreCase);
	}
}
