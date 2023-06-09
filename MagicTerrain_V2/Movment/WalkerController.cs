using MagicTerrain_V2.Gravity;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MagicTerrain_V2.Movment
{
	[RequireComponent(typeof(Rigidbody))]
	[RequireComponent(typeof(PlayerInput))]
	public class WalkerController : GravitySimulatedObject
	{
		[SerializeField]
		private Transform camera;
		
		[SerializeField]
		private float walkSpeed = 25f;

		[SerializeField]
		private float sprintSpeed = 40f;

		[SerializeField]
		private float airMovementSpeed = 4f;
		
		[SerializeField]
		private float jumpStrength = 20f;
		
		[SerializeField]
		private float maxSlopeAngle = 75f;
		
		[SerializeField]
		private float maxVelocity = 10f;
		
		[SerializeField]
		private float maxSprintVelocity = 25f;

		[SerializeField]
		private float mouseSensitivity = 0.5f;

		[SerializeField]
		private float cameraMinAngle = -45f;
		
		[SerializeField]
		private float cameraMaxAngle = 65f;

		[SerializeField]
		private ChunkCore ChunkCore;

		private Rigidbody rigidbody;
		private PlayerInput playerInput;
		private InputAction jumpAction;
		private InputAction sprintAction;
		private InputAction moveAction;
		private InputAction lookAction;
		
		private float cameraRotationX;
		private bool mouseVisible = true;

		private bool isMoving;
		private bool isSprinting;
		private bool isGrounded;
		private bool pastMaxSlopeAngle;
		private GroundedRayData[] groundedRays;
		private int groundedRaysCount;

		private class GroundedRayData
		{
			public bool IsGrounded { get; set; }
			public float Angle { get; set; }
			public Vector3 SurfaceNormal { get; set; }
		}

		private void Awake()
		{
			rigidbody = GetComponent<Rigidbody>();
			playerInput = GetComponent<PlayerInput>();
			
			if (playerInput != null)
			{
				lookAction = playerInput.actions["Look"];
				moveAction = playerInput.actions["Move"];
				jumpAction = playerInput.actions["Jump"];
				jumpAction.performed += PerformedJump;
				sprintAction = playerInput.actions["Sprint"];
				sprintAction.performed += PerformedSprint;
				sprintAction.canceled += CanceledSprint;
			}

			groundedRays = new GroundedRayData[11];
			for (var index = 0; index < groundedRays.Length; index++)
			{
				groundedRays[index] = new GroundedRayData();
			}
			
			Cursor.visible = mouseVisible;
			Cursor.lockState = CursorLockMode.Locked;
			
			gameObject.SetActive(false);

			ChunkCore.OnQueuedChunksDone += HandlePlayerActivation;
		}

		private void HandlePlayerActivation()
		{
			gameObject.SetActive(true);
		}

		private void PerformedSprint(InputAction.CallbackContext context)
		{
			isSprinting = true;
		}

		private void CanceledSprint(InputAction.CallbackContext context)
		{
			isSprinting = false;
		}

		private void PerformedJump(InputAction.CallbackContext context)
		{
			if (!isGrounded || pastMaxSlopeAngle) return;
			rigidbody.AddForce(transform.up * jumpStrength, ForceMode.VelocityChange);
		}

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.Escape))
			{
				mouseVisible = !mouseVisible;
				Cursor.visible = mouseVisible;
				if (mouseVisible)
				{
					Cursor.lockState = CursorLockMode.None;
				}
				else
				{
					Cursor.lockState = CursorLockMode.Locked;
					Cursor.lockState = CursorLockMode.Confined;
					Cursor.lockState = CursorLockMode.Locked;
					Cursor.lockState = CursorLockMode.Confined;
				}
			}
			
			var inputVector = lookAction.ReadValue<Vector2>();

			transform.Rotate(0, inputVector.x * mouseSensitivity, 0);
			cameraRotationX += -inputVector.y * mouseSensitivity;
			
			cameraRotationX = Mathf.Clamp(cameraRotationX, cameraMinAngle, cameraMaxAngle);
			
			camera.transform.localRotation = Quaternion.Euler(cameraRotationX, 0, 0);
		}

		private void FixedUpdate()
		{
			//direction to the planet
			var direction = -GravityDirection.normalized;
			
			//Detect if is grounded
			CastCenterRay(0, -transform.up);
			var increment = 360f / 9;
			var angle = 0f;
			for (var i = 0; i <= 9; i++)
			{
				CastRay(angle, i + 1, -transform.up);
				angle += increment;
			}
			
			groundedRaysCount = groundedRays.Count(groundedRay => groundedRay.IsGrounded);
			isGrounded = groundedRaysCount > 1;
			// IsAffectedByGravity = !isGrounded;

			if (groundedRaysCount < 7)
			{
				//gonna say is standing on an edge
			}

			//rotate the player to the planet
			var rotation = Quaternion.FromToRotation(transform.up, direction) * transform.rotation;
			rigidbody.MoveRotation(rotation);
			
			var averageAngle = groundedRays.Where(data => data.IsGrounded).Sum(data => data.Angle) / groundedRaysCount;
			pastMaxSlopeAngle = averageAngle >= maxSlopeAngle;
			 
			 if (!pastMaxSlopeAngle)
			 {
				 PerformedMovement();
			 }

			 ClampRigidbodyVelocity();
		}

		private void ClampRigidbodyVelocity()
		{
			rigidbody.velocity = Vector3.ClampMagnitude(rigidbody.velocity, isSprinting ? maxSprintVelocity : maxVelocity);
		}
		
		private void PerformedMovement()
		{
			var surfaceNormal = groundedRays[0].SurfaceNormal;

			var inputVector = moveAction.ReadValue<Vector2>();
			isMoving = inputVector.x > 0 || inputVector.y > 0;
			var speed = !isGrounded ? airMovementSpeed : isSprinting ? sprintSpeed : walkSpeed;
			
			var inputVectorX = transform.right * inputVector.x;
			var inputVectorY = transform.forward * inputVector.y;
			var direction = (inputVectorX + inputVectorY) / 2;


			var movementDirection = Vector3.ProjectOnPlane(direction, surfaceNormal);
			
			rigidbody.AddForce((isGrounded ? movementDirection : direction) * speed, ForceMode.VelocityChange);
		}

		void CastCenterRay(int index, Vector3 direction)
		{
			var ray = new Ray(transform.position, direction);
			calculateRayCastInfo(index, ray);
		}

		void CastRay(float angle, int index, Vector3 direction) 
		{
			var rot = Quaternion.LookRotation(direction, transform.up);
			var originRot = Quaternion.AngleAxis(angle, transform.up);
			var origin = originRot * (0.5f * transform.forward) + transform.position;
			var ray = new Ray(origin, rot * Vector3.forward);
			calculateRayCastInfo(index, ray);
		}
		
		private void calculateRayCastInfo(int index, Ray ray)
		{
			RaycastHit hit;
			if (Physics.Raycast(ray, out hit, 1f))
			{
				groundedRays[index].IsGrounded = true;

				// Get the surface normal of the hit object
				Vector3 hitNormal = hit.normal;

				// Get the angle between the surface normal and the player's up direction
				groundedRays[index].Angle = Vector3.Angle(transform.up, hitNormal);
				groundedRays[index].SurfaceNormal = hit.normal;
				return;
			}

			groundedRays[index].IsGrounded = false;
			groundedRays[index].Angle = 0;
			groundedRays[index].SurfaceNormal = Vector3.zero;
		}
	}
}