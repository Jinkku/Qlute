public static class Check
{
	public static bool CheckBoolValue(string value)
	{
		if (value.ToLower() == "true" || value == "1" || value.ToLower() == "yes")
		{
			return true;
		}
		else if (value.ToLower() == "false" || value == "0" || value.ToLower() == "no")
		{
			return false;
		}
		else
		{
			return false;
		}
	}
}