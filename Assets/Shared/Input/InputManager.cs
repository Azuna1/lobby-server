using UnityEngine;
using System.Collections;

public class InputManager : MonoBehaviour {
	private static bool created = false;
	
	private float _mouseSensitivity = 5f;
	public InputControl[] controls;
	
	void Awake() {
		// Don't destroy this object on level loading
		if(!created) {
			DontDestroyOnLoad(this.gameObject);
			created = true;
		} else {
			Destroy(this.gameObject);
		}
	}
	
	public float mouseSensitivity {
		get { return _mouseSensitivity; }
		set {
			if(_mouseSensitivity != value) {
				_mouseSensitivity = value;
				PlayerPrefs.SetFloat("Input_MouseSensitivity", _mouseSensitivity);
				var camPivot = GameObject.FindGameObjectWithTag("CamPivot");
				
				if(camPivot != null) {
					var mouseLook = camPivot.GetComponent<MouseLook>();
					mouseLook.sensitivityX = _mouseSensitivity;
					mouseLook.sensitivityY = _mouseSensitivity;
				}
			}
		}
	}
	
	public bool GetButton(int index) {
		return Input.GetKey(controls[index].keyCode) || Input.GetKey(controls[index].altKeyCode);
	}
	
	public bool GetButtonDown(int index) {
		return Input.GetKeyDown(controls[index].keyCode) || Input.GetKeyDown(controls[index].altKeyCode);
	}
	
	public float GetButtonFloat(int index) {
		return Input.GetKey(controls[index].keyCode) || Input.GetKey(controls[index].altKeyCode) ? 1.0f : 0.0f;
	}
	
	public int GetButtonIndex(string id) {
		for(int i = 0; i < controls.Length; i++) {
			if(controls[i].id == id)
				return i;
		}
		
		LogManager.General.LogWarning("Control '" + id + "' doesn't exist");
		return -1;
	}
	
	public void CopySettingsFrom(InputSettings inputSettings) {
		foreach(var control in inputSettings.controls) {
			int index = GetButtonIndex(control.id);
			
			if(index != -1) {
				var myControl = controls[index];
				myControl.keyCode = control.keyCode;
				myControl.altKeyCode = control.altKeyCode;
			}
		}
	}
	
	public static Vector2 GetMousePosition() {
		return new Vector2(Input.mousePosition.x, (Screen.height - Input.mousePosition.y));
	}
}