using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class CustomHierarchy
{
	private static bool Initialized;
	
	static CustomHierarchy()
	{
		// InitializeHierarchy();
	}

	static void InitializeHierarchy()
	{
		if (Initialized)
		{
			EditorApplication.hierarchyWindowItemOnGUI -= DrawUI;
			EditorApplication.hierarchyChanged -= RetrieveDataFromScene;
		}
		
		EditorApplication.hierarchyWindowItemOnGUI += DrawUI;
		EditorApplication.hierarchyChanged += RetrieveDataFromScene;
		Initialized = true;
		
		RetrieveDataFromScene();
		
		EditorApplication.RepaintHierarchyWindow();
	}

	static void DrawUI(int instanceID, Rect selectionRect)
	{
		
	}

	static void RetrieveDataFromScene()
	{
		if (Application.isPlaying)
			return;
	}
}