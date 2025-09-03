using System;
using UnityEditor;
using UnityEngine;

namespace VoodooPackages.TinySauce.Privacy.Localisation.Editor
{
    [CustomEditor(typeof(LocalisationDict))]
    public class LocalisationDictEditor : UnityEditor.Editor
    {

        
        private LocalisationDict localisationDict => target as LocalisationDict;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            GUILayout.Space(15);

            GUIStyle style = new GUIStyle(GUI.skin.button);
            
            if (GUILayout.Button("Test", style)) {
                localisationDict.LoadLocalisationString();
                EditorUtility.SetDirty(localisationDict);
            }
        }
    }
}