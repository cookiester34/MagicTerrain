using Scripts;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ChunkEditingController : MonoBehaviour
{
	private Camera camera;
	private PlayerInput playerInput;
	private InputAction fireAction;
	private float editDelay = 0f;
	private bool allowEdit;
	private bool isRightMouseDown;
	private bool isLeftMouseDown;

	[SerializeField]
	private float radius = 2f;

	[SerializeField]
	private float editSpeed = 5f;

	private void Awake()
	{
		camera = GetComponentInChildren<Camera>();
		playerInput = GetComponent<PlayerInput>();

		if (playerInput == null) return;

		fireAction = playerInput.actions["LeftMouse"];
		fireAction.performed += LeftMousePerformed;
		fireAction.canceled += EditStopped;
		
		fireAction = playerInput.actions["RightMouse"];
		fireAction.performed += RightMousePerformed;
		fireAction.canceled += EditStopped;
	}

	private void Update()
	{
		if (editDelay >= 0)
		{
			editDelay -= Time.deltaTime;
		}

		allowEdit = editDelay <= 0;

		if (isRightMouseDown)
		{
			RightMousePerformed(default);
		}
		else if (isLeftMouseDown)
		{
			LeftMousePerformed(default);
		}
	}

	//TODO: Needs better logic, pressing both will break this
	private void EditStopped(InputAction.CallbackContext callbackContext)
	{
		isRightMouseDown = false;
		isLeftMouseDown = false;
	}

	private void RightMousePerformed(InputAction.CallbackContext callbackContext)
	{
		isRightMouseDown = true;
		if (!allowEdit) return;
		editDelay = editSpeed;

		PerformedEdit(true);
	}
	
	private void LeftMousePerformed(InputAction.CallbackContext callbackContext)
	{
		isLeftMouseDown = true;
		if (!allowEdit) return;
		editDelay = editSpeed;

		PerformedEdit(false);
	}
	
	private void PerformedEdit(bool add)
	{
		var cameraTransform = camera.transform;
		var ray = new Ray(cameraTransform.position, cameraTransform.forward);

		if (!Physics.SphereCast(ray, 0.5f, out var hit, 1000f)) return;
		
		var chunkContainer = hit.transform.GetComponent<ChunkContainer>();
		var hitInfoPoint = hit.point;
		var hitPoint = new Vector3Int((int)hitInfoPoint.x, (int)hitInfoPoint.y, (int)hitInfoPoint.z);
		chunkContainer.EditChunk(hitPoint, radius, add);
	}

	private List<EditedChunkPointValue> GetCirclePoints(Vector3 pos, bool add, float radius = 2f)
	{
		var posInt = new Vector3(pos.x, pos.y, pos.z);
		var gridPoints = new List<EditedChunkPointValue>();
		var radiusCeil = Mathf.CeilToInt(radius);
		for (var i = -radiusCeil; i <= radiusCeil; i++)
		{
			for (var j = -radiusCeil; j <= radiusCeil; j++)
			{
				for (var k = -radiusCeil; k <= radiusCeil; k++)
				{
					var gridPoint = new Vector3(posInt.x + i,posInt.y + j,posInt.z + k);
					var distance = Vector3.Distance(posInt, gridPoint);
					var radiusEffector = add ? 1.05f : 0.9f;
					var t = 1f - Mathf.Exp(2f * (distance - radius * radiusEffector));
					var lerpedValue = Mathf.Lerp(add ? 1 : 0, add ? 0 : 1, t);
					if (Vector3.Distance(posInt, gridPoint) <= radius)
					{
						gridPoints.Add(new EditedChunkPointValue(){PointPosition = new Vector3Int((int)gridPoint.x, (int)gridPoint.y, (int)gridPoint.z), PointValue = lerpedValue});
					}
				}
			}
		}
		return gridPoints;
	}

	public List<EditedChunkPointValue> GetSquarePoints(Vector3Int pos, float _width = 2f, float _length = 2f, float _hieght = 2f)
	{
		var gridPoints = new List<EditedChunkPointValue>();
		var width = Mathf.CeilToInt(_width);
		var length = Mathf.CeilToInt(_length);
		var height = Mathf.CeilToInt(_hieght);

		for (var i = -width; i <= width; i++)
		{
			for(var j = -height; j <= height; j++)
			{
				for(var k = -length; k <= length; k++)
				{
					var gridPoint = new Vector3Int(Mathf.FloorToInt(pos.x + i),
						Mathf.FloorToInt(pos.y + j),
						Mathf.FloorToInt(pos.z + k));

					gridPoints.Add(new EditedChunkPointValue(){PointPosition = gridPoint, PointValue = 1});
				}
			}
		}

		return gridPoints;
	}
}