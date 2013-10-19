﻿using UnityEngine;

public class GUIArea : System.IDisposable {
	public static float x = 0;
	public static float y = 0;
	public static float width = Screen.width;
	public static float height = Screen.height;
	
	public GUIArea(Rect screenRect) {
		GUIArea.x = screenRect.x;
		GUIArea.y = screenRect.y;
		GUIArea.width = screenRect.width;
		GUIArea.height = screenRect.height;
		
		GUILayout.BeginArea(new Rect(GUIArea.x, GUIArea.y, GUIArea.width, GUIArea.height));
	}
	
	public GUIArea(float width, float height) {
		GUIArea.x = Screen.width / 2 - width / 2;
		GUIArea.y = Screen.height / 2 - height / 2;
		GUIArea.width = width;
		GUIArea.height = height;
		
		GUILayout.BeginArea(new Rect(GUIArea.x, GUIArea.y, GUIArea.width, GUIArea.height));
	}
	
	public GUIArea(float x, float y, float width, float height) {
		if(width <= 1f && height <= 1f) {
			GUIArea.x = Screen.width * x;
			GUIArea.y = Screen.height * y;
			GUIArea.width = Screen.width * width;
			GUIArea.height = Screen.height * height;
		} else {
			GUIArea.x = x;
			GUIArea.y = y;
			GUIArea.width = (int)(width);
			GUIArea.height = (int)(height);
		}
		
		GUILayout.BeginArea(new Rect(GUIArea.x, GUIArea.y, GUIArea.width, GUIArea.height));
	}
	
	void System.IDisposable.Dispose() {
		GUILayout.EndArea();
		
		GUIArea.x = 0;
		GUIArea.y = 0;
		GUIArea.width = Screen.width;
		GUIArea.height = Screen.height;
	}
}