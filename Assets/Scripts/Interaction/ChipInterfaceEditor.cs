﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Allows player to add/remove/move/rename inputs or outputs of a chip.
public class ChipInterfaceEditor : InteractionHandler {

	const int maxGroupSize = 8;

	public event System.Action<Chip> onDeleteChip;
	public enum EditorType { Input, Output }
	public enum HandleState { Default, Highlighted, Selected }
	const float forwardDepth = -0.1f;

	public List<ChipSignal> signals { get; private set; }

	public EditorType editorType;

	[Header ("References")]
	public Transform chipContainer;
	public ChipSignal signalPrefab;
	public TMPro.TMP_InputField nameField;
	public UnityEngine.UI.Button deleteButton;
	public Transform signalHolder;

	[Header ("Appearance")]
	public Vector2 handleSize;
	public Color handleCol;
	public Color highlightedHandleCol;
	public Color selectedHandleCol;
	public float renameFieldX = 2;
	public float deleteButtonX = 1;
	public bool showPreviewSignal;
	public float groupSpacing = 1;

	ChipSignal highlightedSignal;
	List<ChipSignal> selectedSignals;
	ChipSignal[] previewSignals;

	BoxCollider2D inputBounds;

	Mesh quadMesh;
	Material handleMat;
	Material highlightedHandleMat;
	Material selectedHandleMat;
	bool mouseInInputBounds;

	// Dragging
	bool isDragging;
	float dragHandleStartY;
	float dragMouseStartY;

	// Grouping
	int currentGroupSize = 1;
	int currentGroupID;

	void Awake () {
		signals = new List<ChipSignal> ();
		selectedSignals = new List<ChipSignal> ();
		inputBounds = GetComponent<BoxCollider2D> ();
		MeshShapeCreator.CreateQuadMesh (ref quadMesh);
		handleMat = CreateUnlitMaterial (handleCol);
		highlightedHandleMat = CreateUnlitMaterial (highlightedHandleCol);
		selectedHandleMat = CreateUnlitMaterial (selectedHandleCol);

		previewSignals = new ChipSignal[maxGroupSize];
		for (int i = 0; i < maxGroupSize; i++) {
			var previewSignal = Instantiate (signalPrefab);
			previewSignal.SetInteractable (false);
			previewSignal.gameObject.SetActive (false);
			previewSignal.signalName = "Preview";
			previewSignal.transform.SetParent (transform, true);
			previewSignals[i] = previewSignal;
		}

		deleteButton.onClick.AddListener (Delete);
	}

	public override void OrderedUpdate () {
		if (!InputHelper.MouseOverUIObject ()) {
			UpdateColours ();
			HandleInput ();
		}
		DrawSignalHandles ();
	}

	public void LoadSignal (ChipSignal signal) {
		signal.transform.parent = signalHolder;
		signals.Add (signal);
	}

	void HandleInput () {
		Vector2 mousePos = InputHelper.MouseWorldPos;

		mouseInInputBounds = inputBounds.OverlapPoint (mousePos);
		if (mouseInInputBounds) {
			RequestFocus ();
		}

		if (HasFocus) {

			highlightedSignal = GetSignalUnderMouse ();

			// If a signal is highlighted (mouse is over its handle), then select it on mouse press
			if (highlightedSignal) {
				if (Input.GetMouseButtonDown (0)) {
					SelectSignal (highlightedSignal);
				}
			}

			// If a signal is selected, handle movement/renaming/deletion
			if (selectedSignals.Count > 0) {
				if (isDragging) {
					float handleNewY = (mousePos.y + (dragHandleStartY - dragMouseStartY));
					bool cancel = Input.GetKeyDown (KeyCode.Escape);
					if (cancel) {
						handleNewY = dragHandleStartY;
					}

					for (int i = 0; i < selectedSignals.Count; i++) {
						float y = CalcY (handleNewY, selectedSignals.Count, i);
						SetYPos (selectedSignals[i].transform, y);
					}

					if (Input.GetMouseButtonUp (0)) {
						isDragging = false;
					}

					// Cancel drag and deselect
					if (cancel) {
						FocusLost ();
					}
				}

				UpdateButtonAndNameField ();

				// Finished with selected signal, so deselect it
				if (Input.GetKeyDown (KeyCode.Return)) {
					FocusLost ();
				}

			}

			HidePreviews ();
			if (highlightedSignal == null && !isDragging) {
				if (mouseInInputBounds) {

					if (InputHelper.AnyOfTheseKeysDown (KeyCode.Plus, KeyCode.KeypadPlus, KeyCode.Equals)) {
						currentGroupSize = Mathf.Clamp (currentGroupSize + 1, 1, maxGroupSize);
					} else if (InputHelper.AnyOfTheseKeysDown (KeyCode.Minus, KeyCode.KeypadMinus, KeyCode.Underscore)) {
						currentGroupSize = Mathf.Clamp (currentGroupSize - 1, 1, maxGroupSize);
					}

					HandleSpawning ();

				}
			}
		}
	}

	float CalcY (float mouseY, int groupSize, int index) {
		float centreY = mouseY;
		float halfExtent = groupSpacing * (groupSize - 1f);
		float maxY = centreY + halfExtent + handleSize.y / 2f;
		float minY = centreY - halfExtent - handleSize.y / 2f;

		if (maxY > BoundsTop) {
			centreY -= (maxY - BoundsTop);
		} else if (minY < BoundsBottom) {
			centreY += (BoundsBottom - minY);
		}

		float t = (groupSize > 1) ? index / (groupSize - 1f) : 0.5f;
		t = t * 2 - 1;
		float posY = centreY - t * halfExtent;
		return posY;
	}

	// Handles spawning if user clicks, otherwise displays preview
	void HandleSpawning () {
		bool isGroup = currentGroupSize > 1;
		float containerX = chipContainer.position.x + chipContainer.localScale.x / 2 * ((editorType == EditorType.Input) ? -1 : 1);
		float centreY = ClampY (InputHelper.MouseWorldPos.y);

		// Spawn on mouse down
		bool spawn = Input.GetMouseButtonDown (0);

		for (int i = 0; i < currentGroupSize; i++) {
			//float t = (currentGroupSize > 1) ? i / (currentGroupSize - 1f) : 0.5f;
			//t = t * 2 - 1;
			//float posY = centreY - t * groupSpacing * (currentGroupSize - 1f);
			float posY = CalcY (InputHelper.MouseWorldPos.y, currentGroupSize, i);
			Vector3 spawnPos = new Vector3 (containerX, posY, chipContainer.position.z + forwardDepth);
			DrawHandle (posY, HandleState.Highlighted);

			// Spawn signals
			if (spawn) {
				ChipSignal spawnedSignal = Instantiate (signalPrefab, spawnPos, Quaternion.identity, signalHolder);
				if (isGroup) {
					spawnedSignal.SetGroup (currentGroupID);
				}
				signals.Add (spawnedSignal);
			}
			// Display previews 
			else if (showPreviewSignal) {
				previewSignals[i].gameObject.SetActive (true);
				previewSignals[i].transform.position = spawnPos - Vector3.forward * forwardDepth;
			}
		}

		if (spawn) {
			if (isGroup) {
				// Reset group size after spawning
				currentGroupSize = 1;
				// Generate new ID for next group
				// This will be used to identify which signals were created together as a group
				currentGroupID++;
			}
			SelectSignal (signals[signals.Count - 1]);
		}
	}

	void HidePreviews () {
		for (int i = 0; i < previewSignals.Length; i++) {
			previewSignals[i].gameObject.SetActive (false);
		}
	}

	float BoundsTop {
		get {
			return transform.position.y + transform.localScale.y / 2;
		}
	}

	float BoundsBottom {
		get {
			return transform.position.y - transform.localScale.y / 2f;
		}
	}

	float ClampY (float y) {
		return Mathf.Clamp (y, BoundsBottom + handleSize.y / 2f, BoundsTop - handleSize.y / 2f);
	}

	protected override bool CanReleaseFocus () {
		if (isDragging) {
			return false;
		}
		if (mouseInInputBounds) {
			return false;
		}
		return true;
	}

	protected override void FocusLost () {
		highlightedSignal = null;
		selectedSignals.Clear ();

		deleteButton.gameObject.SetActive (false);
		nameField.gameObject.SetActive (false);
		HidePreviews ();
		currentGroupSize = 1;
	}

	void UpdateButtonAndNameField () {
		if (selectedSignals.Count > 0) {
			var selectedSignal = selectedSignals[0];
			deleteButton.transform.position = Camera.main.WorldToScreenPoint (selectedSignal.transform.position + Vector3.right * deleteButtonX);
			// Update signal name
			selectedSignal.UpdateSignalName (nameField.text);
			nameField.transform.position = Camera.main.WorldToScreenPoint (selectedSignal.transform.position + Vector3.right * renameFieldX);
		}
	}

	void DrawSignalHandles () {
		for (int i = 0; i < signals.Count; i++) {
			HandleState handleState = HandleState.Default;
			if (signals[i] == highlightedSignal) {
				handleState = HandleState.Highlighted;
			}
			if (selectedSignals.Contains (signals[i])) {
				handleState = HandleState.Selected;
			}

			DrawHandle (signals[i].transform.position.y, handleState);
		}
	}

	ChipSignal GetSignalUnderMouse () {
		ChipSignal signalUnderMouse = null;
		float nearestDst = float.MaxValue;

		for (int i = 0; i < signals.Count; i++) {
			ChipSignal currentSignal = signals[i];
			float handleY = currentSignal.transform.position.y;

			Vector2 handleCentre = new Vector2 (transform.position.x, handleY);
			Vector2 mousePos = InputHelper.MouseWorldPos;

			const float selectionBufferY = 0.1f;

			float halfSizeX = transform.localScale.x / 2f;
			float halfSizeY = (handleSize.y + selectionBufferY) / 2f;
			bool insideX = mousePos.x >= handleCentre.x - halfSizeX && mousePos.x <= handleCentre.x + halfSizeX;
			bool insideY = mousePos.y >= handleCentre.y - halfSizeY && mousePos.y <= handleCentre.y + halfSizeY;

			if (insideX && insideY) {
				float dst = Mathf.Abs (mousePos.y - handleY);
				if (dst < nearestDst) {
					nearestDst = dst;
					signalUnderMouse = currentSignal;
				}
			}
		}
		return signalUnderMouse;
	}

	// Select signal (starts dragging, shows rename field)
	void SelectSignal (ChipSignal signalToDrag) {
		// Dragging
		selectedSignals.Clear ();
		for (int i = 0; i < signals.Count; i++) {
			if (signals[i] == signalToDrag || ChipSignal.InSameGroup (signals[i], signalToDrag)) {
				selectedSignals.Add (signals[i]);
			}
		}

		isDragging = true;

		dragMouseStartY = InputHelper.MouseWorldPos.y;
		if (selectedSignals.Count % 2 == 0) {
			int indexA = Mathf.Max (0, selectedSignals.Count / 2 - 1);
			int indexB = selectedSignals.Count / 2;
			dragHandleStartY = (selectedSignals[indexA].transform.position.y + selectedSignals[indexB].transform.position.y) / 2f;
		} else {
			dragHandleStartY = selectedSignals[selectedSignals.Count / 2].transform.position.y;
		}

		// Name input field
		nameField.gameObject.SetActive (true);
		nameField.text = (selectedSignals[0]).signalName;
		nameField.Select ();
		// Delete button
		deleteButton.gameObject.SetActive (true);
		UpdateButtonAndNameField ();

	}

	void Delete () {
		for (int i = selectedSignals.Count - 1; i >= 0; i--) {
			onDeleteChip?.Invoke (selectedSignals[i]);
			signals.Remove (selectedSignals[i]);
			Destroy (selectedSignals[i].gameObject);
		}
		selectedSignals.Clear ();
		FocusLost ();
	}

	void DrawHandle (float y, HandleState handleState = HandleState.Default) {
		float renderZ = forwardDepth;
		Material currentHandleMat;
		switch (handleState) {
			case HandleState.Highlighted:
				currentHandleMat = highlightedHandleMat;
				break;
			case HandleState.Selected:
				currentHandleMat = selectedHandleMat;
				renderZ = forwardDepth * 2;
				break;
			default:
				currentHandleMat = handleMat;
				break;
		}

		Vector3 scale = new Vector3 (handleSize.x, handleSize.y, 1);
		Vector3 pos3D = new Vector3 (transform.position.x, y, transform.position.z + renderZ);
		Matrix4x4 handleMatrix = Matrix4x4.TRS (pos3D, Quaternion.identity, scale);
		Graphics.DrawMesh (quadMesh, handleMatrix, currentHandleMat, 0);
	}

	Material CreateUnlitMaterial (Color col) {
		var mat = new Material (Shader.Find ("Unlit/Color"));
		mat.color = col;
		return mat;
	}

	void SetYPos (Transform t, float y) {
		t.position = new Vector3 (t.position.x, y, t.position.z);
	}

	void UpdateColours () {
		handleMat.color = handleCol;
		highlightedHandleMat.color = highlightedHandleCol;
		selectedHandleMat.color = selectedHandleCol;
	}
}