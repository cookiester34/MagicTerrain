﻿using UnityEngine;

public static class MagicExtensions
{
	public static int RoundOff (this int i, int interval)
	{
		return Mathf.RoundToInt(i / (float)interval) * interval;
	}
}