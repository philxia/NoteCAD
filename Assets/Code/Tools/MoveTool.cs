﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using System;

public class MoveTool : Tool {

	ICADObject current;
	Vector3 click;
	Vector3 worldClick;
	Vector3 firstClickCenter;
	double deltaR;
	List<Exp> drag = new List<Exp>();
	Param dragXP = new Param("dragX", reduceable: false);
	Param dragYP = new Param("dragY", reduceable: false);
	Param dragZP = new Param("dragZ", reduceable: false);
	ValueConstraint valueConstraint;
	bool shouldPushUndo = true;
	bool rectSelection = false;
	bool rectInvertedX = false;
	public InputField input;
	public static MoveTool instance;
	//bool canMove = true;
	public Texture2D rectSelectionImage;
	public Texture2D rectSelectionImageInverted;

	protected override void OnStart() {
		input.onEndEdit.AddListener(OnEndEdit);
		instance = this;
	}

	public static void ShiftObjects(IEnumerable<ICADObject> objs, Vector3 delta) {
		foreach(var obj in objs) {
			var sko = obj as SketchObject;
			if(sko == null) continue;
			if(!(sko is Entity) || (sko as Entity).type != IEntityType.Point) continue;
			sko.Drag(delta);
		}
	}

	protected override void OnMouseDown(Vector3 pos, ICADObject sko) {
		ClearDrag();
		if(!Input.GetKey(KeyCode.LeftShift) && !editor.IsSelected(sko)) editor.selection.Clear();
		if(valueConstraint != null) return;
		if(sko == null) {
			rectSelection = true;
			firstClickCenter = click = Camera.main.WorldToGuiPoint(pos);
			return;
		}
		if(Input.GetKey(KeyCode.LeftShift) && editor.selection.Contains(sko.id)) {
			editor.selection.Remove(sko.id);
		} else {
			editor.selection.Add(sko.id);
		}
		var entity = sko as IEntity;
		current = sko;
		click = pos;
		worldClick = WorldPlanePos;
		int count = 0;
		if(entity != null) count = entity.points.Count();
		if(count == 0) return;
		editor.PushUndo();

		editor.suppressCombine = true;
		editor.suppressHovering = true;

		if(editor.selection.Count > 1) {
		} else {
			dragXP.value = 0;
			dragYP.value = 0;
			dragZP.value = 0;
			if(entity.IsCircular()) {
				var dragR = entity.Radius().Drag(dragXP.exp);
				editor.AddDrag(dragR);
				drag.Add(dragR);
				firstClickCenter = entity.CenterInPlane(null).Eval();
				deltaR = entity.Radius().Eval() - (firstClickCenter - worldClick).magnitude;
			} else {
				foreach(var ptExp in entity.points) {
					var dragX = ptExp.x.Drag(dragXP.exp + ptExp.x.Eval());
					var dragY = ptExp.y.Drag(dragYP.exp + ptExp.y.Eval());
					var dragZ = ptExp.z.Drag(dragZP.exp + ptExp.z.Eval());
					drag.Add(dragX);
					drag.Add(dragY);
					drag.Add(dragZ);
					//Debug.Log("x: " + dragX);
					//Debug.Log("y: " + dragY);
					//Debug.Log("z: " + dragZ);
					editor.AddDrag(dragX);
					editor.AddDrag(dragY);
					editor.AddDrag(dragZ);
				}
			}
		}
	}

	void ClearDrag() {
		current = null;
		foreach(var d in drag) {
			editor.RemoveDrag(d);
		}
		editor.suppressCombine = false;
		editor.suppressHovering = false;
		drag.Clear();
		//canMove = true;
	}

	protected override void OnDeactivate() {
		ClearDrag();
		valueConstraint = null;
		input.gameObject.SetActive(false);
	}

	protected override void OnMouseMove(Vector3 pos, ICADObject sko) {
		if(rectSelection) {
			click = Camera.main.WorldToGuiPoint(pos);
			return;
		}
		if(current == null) return;
		var delta = pos - click;
		var worldDelta = WorldPlanePos - worldClick;
		
		if(drag.Count > 0) {
			if(current is IEntity && (current as IEntity).IsCircular()) {
				dragXP.value = (firstClickCenter - WorldPlanePos).magnitude + deltaR;
			} else {
				dragXP.value += delta.x;
				dragYP.value += delta.y;
				dragZP.value += delta.z;
			}
		} else if(editor.selection.Count > 1) {
			var objs = editor.selection.Select(s => editor.GetDetail().GetObjectById(s));
			ShiftObjects(objs, delta);
		} else
		if(current is Constraint) {
			(current as Constraint).Drag(worldDelta);
		}
		click = pos;
		worldClick = WorldPlanePos;
	}

	protected override void OnMouseUp(Vector3 pos, ICADObject sko) {
		if(rectSelection) {
			rectSelection = false;
			editor.MarqueeSelect(marqueeRect, rectInvertedX);
		}
		ClearDrag();
	}
	
	public void EditConstraintValue(ValueConstraint constraint, bool pushUndo = true) {
		valueConstraint = constraint;
		this.shouldPushUndo = pushUndo;
		input.gameObject.SetActive(true);
		input.text = Math.Abs(valueConstraint.GetValue()).ToStr();
		input.Select();
		UpdateInputPosition();
	}

	public bool IsConstraintEditing(ValueConstraint constraint) {
		return valueConstraint == constraint;
	}

	protected override void OnMouseDoubleClick(Vector3 pos, ICADObject sko) {
		if(sko is ValueConstraint) {
			EditConstraintValue(sko as ValueConstraint);
		}
	}

	void UpdateInputPosition() {
		if(valueConstraint != null) {
			input.gameObject.transform.position = Camera.main.WorldToScreenPoint(valueConstraint.pos);
		}
	}

	private void Update() {
		UpdateInputPosition();
	}

	void OnEndEdit(string value) {
		if(valueConstraint == null) return;
		var sign = Math.Sign(valueConstraint.GetValue());
		if(sign == 0) sign = 1;
		if(shouldPushUndo) editor.PushUndo();
		valueConstraint.SetValue(sign * value.ToDouble());
		valueConstraint = null;
		input.gameObject.SetActive(false);
	}

	protected override string OnGetDescription() {
		return "hover over an entity, hold down left mouse button to move it. Double click on any dimension to edit. Click on Help icon for additional info.";
	}

	Rect marqueeRectUI {
		get {
			var p0 = firstClickCenter;
			var p1 = click;
			rectInvertedX = (p0.x > p1.x);
			if(rectInvertedX) SystemExt.Swap(ref p0.x, ref p1.x);
			if(p0.y > p1.y) SystemExt.Swap(ref p0.y, ref p1.y);
			var size = p1 - p0;
			return new Rect(p0.x, p0.y, size.x, size.y);
		}
	}

	Rect marqueeRect {
		get {
			var rect = marqueeRectUI;
			var h = Camera.main.pixelHeight;
			return new Rect(rect.x, h - (rect.y + rect.height), rect.width, rect.height);
		}
	}

	private void OnGUI() {
		if(rectSelection) {
			var rect = marqueeRectUI;
			GUI.DrawTexture(rect, rectInvertedX ? rectSelectionImageInverted : rectSelectionImage, ScaleMode.StretchToFill, true);
			GUI.DrawTexture(rect, rectInvertedX ? rectSelectionImageInverted : rectSelectionImage, ScaleMode.StretchToFill, true, 0f, new Color(1f, 1f, 1f, 1f), 1f, 0f);
		}
	}

}
