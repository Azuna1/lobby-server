using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class InputControl {
	public string name;
	public string description;
	public KeyCode keyCode;
	public KeyCode altKeyCode;
	public bool active = true;
	
	public string keyCodeString {
		get {
			return InputControl.KeyCodeToString(keyCode);
		}
	}
	
	public string altKeyCodeString {
		get {
			return InputControl.KeyCodeToString(altKeyCode);
		}
	}
	
	public bool keyCodesActive {
		get {
			return keyCode != KeyCode.None || altKeyCode != KeyCode.None;
		}
	}
	
	public KeyCode Capture(bool altKey = false) {
		KeyCode kc = Event.current.keyCode;
		
		for(int i = 0; i < 6; i++) {
			if(Input.GetMouseButtonDown(i)) {
				kc = (KeyCode)(KeyCode.Mouse0 + i);
			}
		}
		
		if(kc == KeyCode.None)
			return KeyCode.None;
		
		if(altKey)
			altKeyCode = kc;
		else
			keyCode = kc;
		
		return kc;
	}
	
	public void Erase(bool altKey = false) {
		if(altKey)
			altKeyCode = KeyCode.None;
		else
			keyCode = KeyCode.None;
	}
	
	// Writer
	public static void JsonSerializer(Jboy.JsonWriter writer, object instance) {
		var control = (InputControl)instance;
		if(!control.active)
			return;
		
		var fieldFilter = new HashSet<string>() {
			"name",
			"keyCode",
			"altKeyCode",
		};
		GenericSerializer.WriteJSONClassInstance<InputControl>(writer, control, fieldFilter);
	}
	
	// Reader
	public static object JsonDeserializer(Jboy.JsonReader reader) {
		return GenericSerializer.ReadJSONClassInstance<InputControl>(reader);
	}
	
	// Key code to string
	public static string KeyCodeToString(KeyCode keyCode) {
		if(keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9)
			return (keyCode - KeyCode.Alpha0).ToString();
		
		if(keyCode >= KeyCode.Mouse0 && keyCode <= KeyCode.Mouse6) {
			switch(keyCode) {
			case KeyCode.Mouse0:
				return "Left mouse button";
			case KeyCode.Mouse1:
				return "Right mouse button";
			case KeyCode.Mouse2:
				return "Middle mouse button";
			default:
				return "Mouse extra button " + (keyCode - KeyCode.Mouse2).ToString();
			}
		}
		
		return keyCode.ToString();
	}
}