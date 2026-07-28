using UnityEngine;
using UnityEngine.UI;

namespace Norscope
{
    /// <summary>
    /// Wires Hand Test canvas buttons to required log messages.
    /// </summary>
    public sealed class HandTestButtonLogger : MonoBehaviour
    {
        private void Awake()
        {
            Wire("ButtonA", "Button A Pressed");
            Wire("ButtonB", "Button B Pressed");
            Wire("ButtonC", "Button C Pressed");
        }

        private void Wire(string objectName, string message)
        {
            Transform t = transform.Find("Panel/" + objectName);
            if (t == null)
            {
                Debug.LogWarning("[Norscope] Missing button: " + objectName);
                return;
            }

            Button button = t.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogWarning("[Norscope] Button component missing: " + objectName);
                return;
            }

            string captured = message;
            button.onClick.AddListener(() => Debug.Log(captured));
        }
    }
}
