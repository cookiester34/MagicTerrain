using Scripts.Chunks;
using UnityEditor;
using UnityEngine.UIElements;

namespace SubModules.MagicTerrain.Editor
{
	[CustomEditor(typeof(PreGeneratePlanet))]
	public class PreGeneratePlanetEditor : UnityEditor.Editor
	{
		public override VisualElement CreateInspectorGUI()
		{
			var root = new VisualElement();

			var button = new Button();
			button.text = "Generate Planet";
			button.clicked += () => { ((PreGeneratePlanet) target).GeneratePlanet();};
			root.Add(button);

			root.Add( new IMGUIContainer(OnInspectorGUI));

			return root;
		}
	}
}