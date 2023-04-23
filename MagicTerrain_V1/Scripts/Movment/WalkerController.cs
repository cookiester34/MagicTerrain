using System;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Scripts.Planets
{
	[RequireComponent(typeof(Rigidbody))]
	[RequireComponent(typeof(PlayerInput))]
	public class WalkerController : MonoBehaviour
	{
		[SerializeField]
		private PlanetController planetController;

		[SerializeField]
		private Transform camera;
		
		[SerializeField]
		private float walkSpeed = 3f;

		[SerializeField]
		private float sprintSpeed = 5f;

		[SerializeField]
		private float jumpStrength = 30f;

		[SerializeField]
		private float gravityStrength = 30f;

		[SerializeField]
		private float deceleration = 0.5f;
		
		[SerializeField]
		private float airDeceleration = 0.1f;

		[SerializeField]
		private float maxVelocity = 10f;
		
		[SerializeField]
		private float maxSprintVelocity = 15f;

		[SerializeField]
		private float mouseSensitivity = 0.5f;

		private Rigidbody rigidbody;
		private PlayerInput playerInput;
		private InputAction jumpAction;
		private InputAction sprintAction;
		private InputAction moveAction;
		private InputAction lookAction;

		private bool isMoving;
		private bool isSprinting;
		private bool isGrounded;
		private bool[] groundedRays;

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

			groundedRays = new bool[11];
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
			rigidbody.AddForce(transform.up * jumpStrength, ForceMode.VelocityChange);
		}

		private void Update()
		{
			var inputVector = lookAction.ReadValue<Vector2>();
			transform.Rotate(0, inputVector.x * mouseSensitivity, 0);
			camera.Rotate(-inputVector.y * mouseSensitivity, 0, 0);
		}

		private void FixedUpdate()
		{
			//direction to the planet
			Vector3 direction = (transform.position - planetController.planetCenter).normalized;
			
			//Detect if is grounded
			CastCenterRay(0, -transform.up);
			var increment = 360f / 9;
			var angle = 0f;
			for (int i = 0; i <= 9; i++)
			{
				CastRay(angle, i + 1, -transform.up);
				angle += increment;
			}

			var groundedRaysCount = groundedRays.Count(groundedRay => groundedRay);
			isGrounded = groundedRaysCount > 0;

			if (groundedRaysCount < 9)
			{
				//gonna say is standing on an edge
			}

			//rotate the player to the planet
			Quaternion rotation = Quaternion.FromToRotation(transform.up, direction) * transform.rotation;
			rigidbody.MoveRotation(rotation);
			
			// Apply gravity
			 if (!isGrounded)
			 {
				 rigidbody.AddForce(-transform.up * Time.deltaTime * gravityStrength, ForceMode.VelocityChange);
			 }
			 
			 PerformedMovement();

			 ClampRigidbodyVelocity();
			 
			 //decelerate the player
			 if (!isMoving)
			 {
				 rigidbody.velocity = Vector3.Lerp(rigidbody.velocity, Vector3.zero,
					 (isGrounded ? deceleration : airDeceleration) * Time.fixedDeltaTime);
			 }
		}

		private void ClampRigidbodyVelocity()
		{
			rigidbody.velocity = Vector3.ClampMagnitude(rigidbody.velocity, isSprinting ? maxSprintVelocity : maxVelocity);
		}
		
		private void PerformedMovement()
		{
			var inputVector = moveAction.ReadValue<Vector2>();
			isMoving = inputVector.x > 0 || inputVector.y > 0;
			rigidbody.AddForce(camera.right * inputVector.x * Time.deltaTime * (isSprinting ? sprintSpeed : walkSpeed), ForceMode.VelocityChange);
			rigidbody.AddForce(camera.forward * inputVector.y * Time.deltaTime * (isSprinting ? sprintSpeed : walkSpeed), ForceMode.VelocityChange);
		}

		void CastCenterRay(int index, Vector3 direction)
		{
			Ray ray = new Ray(transform.position, direction);
			RaycastHit hit;
			if (Physics.Raycast(ray, out hit, 1f))
			{
				groundedRays[index] = true;
				return;
			}

			groundedRays[index] = false;
		}
		
		void CastRay(float angle, int index, Vector3 direction) 
		{
			Quaternion rot = Quaternion.LookRotation(direction, transform.up);
			Quaternion originRot = Quaternion.AngleAxis(angle, transform.up);
			Vector3 origin = originRot * (0.5f * transform.forward) + transform.position;
			Ray ray = new Ray(origin, rot * Vector3.forward);
			RaycastHit hit;
			if (Physics.Raycast(ray, out hit, 1f)) 
			{
				groundedRays[index] = true;
				return;
			}
			
			groundedRays[index] = false;
		}
	}
}