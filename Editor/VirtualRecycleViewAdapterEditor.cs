#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine.UI;

namespace OSK.Bindings
{
        [CustomEditor(typeof(ScrollRect), true)]
        public class VirtualRecycleViewAdapterEditor : OdinEditor
        {
                public override void OnInspectorGUI()
                {
                        base.OnInspectorGUI();
                }
        }
}

#endif