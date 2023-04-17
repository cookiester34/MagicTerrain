using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SubModules.MagicTerrain.MagicTerrain_V2
{
	public class ChunkEditingController : MonoBehaviour
	{
		private Camera camera;
		private PlayerInput playerInput;
		private InputAction fireActionRight;
		private InputAction fireActionLeft;
		private InputAction scrollWheelUp;
		private InputAction scrollWheelDown;
		private float editDelay = 0f;
		private bool allowEdit;
		private bool isRightMouseDown;
		private bool isLeftMouseDown;
		private float radius = 1f;

		[SerializeField]
		private float editSpeed = 5f;

		[SerializeField]
		private GameObject sphere;

		private void Awake()
		{
			camera = GetComponentInChildren<Camera>();
			playerInput = GetComponent<PlayerInput>();

			if (playerInput == null) return;

			fireActionLeft = playerInput.actions["LeftMouse"];
			fireActionLeft.performed += LeftMousePerformed;
			fireActionLeft.canceled += EditStopped;

			fireActionRight = playerInput.actions["RightMouse"];
			fireActionRight.performed += RightMousePerformed;
			fireActionRight.canceled += EditStopped;

			scrollWheelUp = playerInput.actions["ScrollUp"];
			scrollWheelUp.performed += ScrollWheelPerformedUp;

			scrollWheelDown = playerInput.actions["ScrollDown"];
			scrollWheelDown.performed += ScrollWheelPerformedDown;
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

			var cameraTransform = camera.transform;
			var ray = new Ray(cameraTransform.position, cameraTransform.forward);

			if (Physics.SphereCast(ray, 0.5f, out var hit, 1000f))
			{
				sphere.SetActive(true);
				sphere.transform.position = hit.point;
			}
			else
			{
				sphere.SetActive(false);
			}
		}

		//TODO: Needs better logic, pressing both will break this
		private void EditStopped(InputAction.CallbackContext callbackContext)
		{
			isRightMouseDown = false;
			isLeftMouseDown = false;
		}

		private void ScrollWheelPerformedUp(InputAction.CallbackContext callbackContext)
		{
			sphere.transform.localScale += new Vector3(2, 2, 2);
			radius += 1;
		}

		private void ScrollWheelPerformedDown(InputAction.CallbackContext callbackContext)
		{
			sphere.transform.localScale -= new Vector3(2, 2, 2);
			radius -= 1;
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
			if (chunkContainer == null) return;
			var hitInfoPoint = hit.point;
			var hitPoint = new Vector3Int((int)hitInfoPoint.x, (int)hitInfoPoint.y, (int)hitInfoPoint.z);
			chunkContainer.EditChunk(hitPoint, radius, add);
		}
	}
}